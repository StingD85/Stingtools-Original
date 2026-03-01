// ===================================================================
// StingBIM.AI.Collaboration - BCF (BIM Collaboration Format) Support
// Implements BCF 3.0 standard for cross-platform issue exchange
// Supports import/export with Revit, Navisworks, Solibri, etc.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.BCF
{
    #region BCF Version Support

    /// <summary>
    /// Supported BCF versions
    /// </summary>
    public enum BCFVersion
    {
        BCF_2_0,
        BCF_2_1,
        BCF_3_0
    }

    /// <summary>
    /// BCF configuration
    /// </summary>
    public class BCFConfiguration
    {
        public BCFVersion DefaultVersion { get; set; } = BCFVersion.BCF_3_0;
        public string ExportDirectory { get; set; } = "./exports";
        public string ImportDirectory { get; set; } = "./imports";
        public bool IncludeSnapshots { get; set; } = true;
        public bool IncludeViewpoints { get; set; } = true;
        public int MaxSnapshotSize { get; set; } = 1920; // Max width in pixels
        public string DefaultAuthor { get; set; } = "StingBIM";
        public bool CompressOutput { get; set; } = true;
    }

    #endregion

    #region BCF Data Models (BCF 3.0 Schema)

    /// <summary>
    /// BCF project information
    /// </summary>
    public class BCFProject
    {
        public string ProjectId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ExtensionSchema { get; set; } = string.Empty;
    }

    /// <summary>
    /// BCF topic (issue/clash)
    /// </summary>
    public class BCFTopic
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string TopicType { get; set; } = "Issue";
        public string TopicStatus { get; set; } = "Open";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public int Index { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public string CreationAuthor { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string ModifiedAuthor { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new();
        public List<string> ReferenceLinks { get; set; } = new();
        public List<BCFBimSnippet> BimSnippets { get; set; } = new();
        public List<BCFDocumentReference> DocumentReferences { get; set; } = new();
        public List<BCFRelatedTopic> RelatedTopics { get; set; } = new();
        public List<BCFComment> Comments { get; set; } = new();
        public List<BCFViewpoint> Viewpoints { get; set; } = new();
        public string ServerAssignedId { get; set; } = string.Empty;
    }

    /// <summary>
    /// BCF comment
    /// </summary>
    public class BCFComment
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Author { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string ViewpointGuid { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string ModifiedAuthor { get; set; } = string.Empty;
    }

    /// <summary>
    /// BCF viewpoint (camera position + component visibility)
    /// </summary>
    public class BCFViewpoint
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public BCFOrthogonalCamera? OrthogonalCamera { get; set; }
        public BCFPerspectiveCamera? PerspectiveCamera { get; set; }
        public BCFComponents? Components { get; set; }
        public List<BCFLine> Lines { get; set; } = new();
        public List<BCFClippingPlane> ClippingPlanes { get; set; } = new();
        public List<BCFBitmap> Bitmaps { get; set; } = new();
        public string SnapshotFileName { get; set; } = string.Empty;
        public byte[]? SnapshotData { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// Orthogonal camera
    /// </summary>
    public class BCFOrthogonalCamera
    {
        public BCFPoint CameraViewPoint { get; set; } = new();
        public BCFDirection CameraDirection { get; set; } = new();
        public BCFDirection CameraUpVector { get; set; } = new();
        public double ViewToWorldScale { get; set; } = 1.0;
        public double AspectRatio { get; set; } = 1.0;
    }

    /// <summary>
    /// Perspective camera
    /// </summary>
    public class BCFPerspectiveCamera
    {
        public BCFPoint CameraViewPoint { get; set; } = new();
        public BCFDirection CameraDirection { get; set; } = new();
        public BCFDirection CameraUpVector { get; set; } = new();
        public double FieldOfView { get; set; } = 60.0;
        public double AspectRatio { get; set; } = 1.0;
    }

    /// <summary>
    /// 3D point
    /// </summary>
    public class BCFPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public BCFPoint() { }
        public BCFPoint(double x, double y, double z) => (X, Y, Z) = (x, y, z);
    }

    /// <summary>
    /// 3D direction
    /// </summary>
    public class BCFDirection
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public BCFDirection() { }
        public BCFDirection(double x, double y, double z) => (X, Y, Z) = (x, y, z);
    }

    /// <summary>
    /// Component visibility and selection
    /// </summary>
    public class BCFComponents
    {
        public BCFViewSetupHints? ViewSetupHints { get; set; }
        public BCFComponentSelection? Selection { get; set; }
        public BCFComponentVisibility? Visibility { get; set; }
        public BCFComponentColoring? Coloring { get; set; }
    }

    /// <summary>
    /// View setup hints
    /// </summary>
    public class BCFViewSetupHints
    {
        public bool SpacesVisible { get; set; } = false;
        public bool SpaceBoundariesVisible { get; set; } = false;
        public bool OpeningsVisible { get; set; } = false;
    }

    /// <summary>
    /// Component selection
    /// </summary>
    public class BCFComponentSelection
    {
        public List<BCFComponent> Components { get; set; } = new();
    }

    /// <summary>
    /// Component visibility
    /// </summary>
    public class BCFComponentVisibility
    {
        public bool DefaultVisibility { get; set; } = true;
        public List<BCFComponent> Exceptions { get; set; } = new();
    }

    /// <summary>
    /// Component coloring
    /// </summary>
    public class BCFComponentColoring
    {
        public List<BCFColorGroup> ColorGroups { get; set; } = new();
    }

    /// <summary>
    /// Color group for components
    /// </summary>
    public class BCFColorGroup
    {
        public string Color { get; set; } = "#FF0000"; // ARGB hex
        public List<BCFComponent> Components { get; set; } = new();
    }

    /// <summary>
    /// BCF component reference
    /// </summary>
    public class BCFComponent
    {
        public string IfcGuid { get; set; } = string.Empty;
        public string OriginatingSystem { get; set; } = string.Empty;
        public string AuthoringToolId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Markup line
    /// </summary>
    public class BCFLine
    {
        public BCFPoint StartPoint { get; set; } = new();
        public BCFPoint EndPoint { get; set; } = new();
    }

    /// <summary>
    /// Clipping plane
    /// </summary>
    public class BCFClippingPlane
    {
        public BCFPoint Location { get; set; } = new();
        public BCFDirection Direction { get; set; } = new();
    }

    /// <summary>
    /// Bitmap overlay
    /// </summary>
    public class BCFBitmap
    {
        public string Reference { get; set; } = string.Empty;
        public BCFPoint Location { get; set; } = new();
        public BCFDirection Normal { get; set; } = new();
        public BCFDirection Up { get; set; } = new();
        public double Height { get; set; }
    }

    /// <summary>
    /// BIM snippet reference
    /// </summary>
    public class BCFBimSnippet
    {
        public string SnippetType { get; set; } = string.Empty;
        public bool IsExternal { get; set; } = false;
        public string Reference { get; set; } = string.Empty;
        public string ReferenceSchema { get; set; } = string.Empty;
    }

    /// <summary>
    /// Document reference
    /// </summary>
    public class BCFDocumentReference
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Related topic reference
    /// </summary>
    public class BCFRelatedTopic
    {
        public string Guid { get; set; } = string.Empty;
    }

    #endregion

    #region BCF Import/Export

    /// <summary>
    /// BCF file container
    /// </summary>
    public class BCFFile
    {
        public BCFVersion Version { get; set; } = BCFVersion.BCF_3_0;
        public BCFProject? Project { get; set; }
        public BCFExtensions? Extensions { get; set; }
        public List<BCFTopic> Topics { get; set; } = new();
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// BCF extensions (custom field definitions)
    /// </summary>
    public class BCFExtensions
    {
        public List<string> TopicTypes { get; set; } = new()
        {
            "Issue", "Clash", "Request", "Remark", "Fault", "Inquiry", "Solution"
        };

        public List<string> TopicStatuses { get; set; } = new()
        {
            "Open", "In Progress", "Resolved", "Closed", "ReOpened"
        };

        public List<string> Priorities { get; set; } = new()
        {
            "Critical", "High", "Normal", "Low"
        };

        public List<string> TopicLabels { get; set; } = new()
        {
            "Architecture", "Structure", "MEP", "Coordination", "Design", "Construction"
        };

        public List<string> Stages { get; set; } = new()
        {
            "Design", "Construction", "Commissioning", "Handover"
        };

        public List<string> Users { get; set; } = new();
        public List<string> SnippetTypes { get; set; } = new();
    }

    /// <summary>
    /// Import result
    /// </summary>
    public class BCFImportResult
    {
        public bool Success { get; set; }
        public BCFFile? File { get; set; }
        public int TopicsImported { get; set; }
        public int ViewpointsImported { get; set; }
        public int CommentsImported { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Export result
    /// </summary>
    public class BCFExportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TopicsExported { get; set; }
        public int ViewpointsExported { get; set; }
        public int SnapshotsExported { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    #endregion

    #region BCF Manager

    /// <summary>
    /// Main BCF manager for import/export operations
    /// </summary>
    public class BCFManager : IAsyncDisposable
    {
        private readonly BCFConfiguration _config;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, BCFFile> _loadedFiles = new();

        public BCFManager(BCFConfiguration? config = null, ILogger? logger = null)
        {
            _config = config ?? new BCFConfiguration();
            _logger = logger;

            Directory.CreateDirectory(_config.ExportDirectory);
            Directory.CreateDirectory(_config.ImportDirectory);

            _logger?.LogInformation("BCFManager initialized with version {Version}",
                _config.DefaultVersion);
        }

        #region Import

        /// <summary>
        /// Import BCF file from disk
        /// </summary>
        public async Task<BCFImportResult> ImportAsync(
            string filePath,
            CancellationToken ct = default)
        {
            var result = new BCFImportResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Errors.Add($"File not found: {filePath}");
                    return result;
                }

                var bcfFile = new BCFFile
                {
                    FileName = Path.GetFileName(filePath)
                };

                using var fileStream = File.OpenRead(filePath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

                // Detect version from bcf.version file
                bcfFile.Version = await DetectVersionAsync(archive, ct);

                // Read project info
                bcfFile.Project = await ReadProjectAsync(archive, ct);

                // Read extensions
                bcfFile.Extensions = await ReadExtensionsAsync(archive, ct);

                // Read all topics
                var topicFolders = archive.Entries
                    .Where(e => e.FullName.Contains("/markup.bcf") ||
                               e.FullName.EndsWith("markup.bcf"))
                    .Select(e => Path.GetDirectoryName(e.FullName)?.Replace("\\", "/") ?? "")
                    .Distinct()
                    .ToList();

                foreach (var folder in topicFolders)
                {
                    if (ct.IsCancellationRequested) break;

                    var topic = await ReadTopicAsync(archive, folder, bcfFile.Version, ct);
                    if (topic != null)
                    {
                        bcfFile.Topics.Add(topic);
                        result.TopicsImported++;
                        result.ViewpointsImported += topic.Viewpoints.Count;
                        result.CommentsImported += topic.Comments.Count;
                    }
                }

                _loadedFiles[bcfFile.FileName] = bcfFile;
                result.File = bcfFile;
                result.Success = true;

                _logger?.LogInformation(
                    "Imported BCF file {FileName}: {Topics} topics, {Viewpoints} viewpoints",
                    bcfFile.FileName, result.TopicsImported, result.ViewpointsImported);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import BCF file {FilePath}", filePath);
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Import from stream
        /// </summary>
        public async Task<BCFImportResult> ImportFromStreamAsync(
            Stream stream,
            string fileName,
            CancellationToken ct = default)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            await using (var fileStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(fileStream, ct);
            }

            try
            {
                return await ImportAsync(tempPath, ct);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        private async Task<BCFVersion> DetectVersionAsync(ZipArchive archive, CancellationToken ct)
        {
            var versionEntry = archive.GetEntry("bcf.version");
            if (versionEntry == null)
            {
                return BCFVersion.BCF_2_1; // Default to 2.1 for older files
            }

            using var reader = new StreamReader(versionEntry.Open());
            var content = await reader.ReadToEndAsync();

            if (content.Contains("3.0")) return BCFVersion.BCF_3_0;
            if (content.Contains("2.1")) return BCFVersion.BCF_2_1;
            return BCFVersion.BCF_2_0;
        }

        private async Task<BCFProject?> ReadProjectAsync(ZipArchive archive, CancellationToken ct)
        {
            var entry = archive.GetEntry("project.bcfp") ??
                       archive.Entries.FirstOrDefault(e => e.Name == "project.bcfp");

            if (entry == null) return null;

            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();

            var doc = XDocument.Parse(xml);
            var projectElem = doc.Root?.Element("Project");

            if (projectElem == null) return null;

            return new BCFProject
            {
                ProjectId = projectElem.Attribute("ProjectId")?.Value ?? Guid.NewGuid().ToString(),
                Name = projectElem.Element("Name")?.Value ?? string.Empty
            };
        }

        private async Task<BCFExtensions?> ReadExtensionsAsync(ZipArchive archive, CancellationToken ct)
        {
            var entry = archive.GetEntry("extensions.xml") ??
                       archive.Entries.FirstOrDefault(e => e.Name == "extensions.xml");

            if (entry == null) return new BCFExtensions();

            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();

            var doc = XDocument.Parse(xml);
            var extensions = new BCFExtensions();

            var root = doc.Root;
            if (root == null) return extensions;

            extensions.TopicTypes = root.Element("TopicTypes")?
                .Elements("TopicType").Select(e => e.Value).ToList() ?? extensions.TopicTypes;
            extensions.TopicStatuses = root.Element("TopicStatuses")?
                .Elements("TopicStatus").Select(e => e.Value).ToList() ?? extensions.TopicStatuses;
            extensions.Priorities = root.Element("Priorities")?
                .Elements("Priority").Select(e => e.Value).ToList() ?? extensions.Priorities;
            extensions.TopicLabels = root.Element("TopicLabels")?
                .Elements("TopicLabel").Select(e => e.Value).ToList() ?? extensions.TopicLabels;

            return extensions;
        }

        private async Task<BCFTopic?> ReadTopicAsync(
            ZipArchive archive,
            string folder,
            BCFVersion version,
            CancellationToken ct)
        {
            var markupPath = string.IsNullOrEmpty(folder)
                ? "markup.bcf"
                : $"{folder}/markup.bcf";

            var entry = archive.GetEntry(markupPath);
            if (entry == null) return null;

            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();

            var doc = XDocument.Parse(xml);
            var topicElem = doc.Root?.Element("Topic");

            if (topicElem == null) return null;

            var topic = new BCFTopic
            {
                Guid = topicElem.Attribute("Guid")?.Value ?? Guid.NewGuid().ToString(),
                TopicType = topicElem.Attribute("TopicType")?.Value ?? "Issue",
                TopicStatus = topicElem.Attribute("TopicStatus")?.Value ?? "Open",
                Title = topicElem.Element("Title")?.Value ?? string.Empty,
                Description = topicElem.Element("Description")?.Value ?? string.Empty,
                Priority = topicElem.Element("Priority")?.Value ?? "Normal",
                CreationAuthor = topicElem.Element("CreationAuthor")?.Value ?? string.Empty,
                AssignedTo = topicElem.Element("AssignedTo")?.Value ?? string.Empty,
                Stage = topicElem.Element("Stage")?.Value ?? string.Empty
            };

            // Parse dates
            if (DateTime.TryParse(topicElem.Element("CreationDate")?.Value, out var creationDate))
                topic.CreationDate = creationDate;
            if (DateTime.TryParse(topicElem.Element("ModifiedDate")?.Value, out var modifiedDate))
                topic.ModifiedDate = modifiedDate;
            if (DateTime.TryParse(topicElem.Element("DueDate")?.Value, out var dueDate))
                topic.DueDate = dueDate;

            // Parse labels
            topic.Labels = topicElem.Elements("Labels")
                .Select(e => e.Value)
                .ToList();

            // Parse comments
            foreach (var commentElem in doc.Root?.Elements("Comment") ?? Enumerable.Empty<XElement>())
            {
                var comment = new BCFComment
                {
                    Guid = commentElem.Attribute("Guid")?.Value ?? Guid.NewGuid().ToString(),
                    Author = commentElem.Element("Author")?.Value ?? string.Empty,
                    Comment = commentElem.Element("Comment")?.Value ?? string.Empty
                };

                if (DateTime.TryParse(commentElem.Element("Date")?.Value, out var commentDate))
                    comment.Date = commentDate;

                comment.ViewpointGuid = commentElem.Element("Viewpoint")?.Attribute("Guid")?.Value ?? string.Empty;

                topic.Comments.Add(comment);
            }

            // Parse viewpoints
            foreach (var vpElem in doc.Root?.Elements("Viewpoints") ?? Enumerable.Empty<XElement>())
            {
                var vpGuid = vpElem.Attribute("Guid")?.Value ?? Guid.NewGuid().ToString();
                var viewpoint = await ReadViewpointAsync(archive, folder, vpGuid, version, ct);
                if (viewpoint != null)
                {
                    topic.Viewpoints.Add(viewpoint);
                }
            }

            return topic;
        }

        private async Task<BCFViewpoint?> ReadViewpointAsync(
            ZipArchive archive,
            string folder,
            string guid,
            BCFVersion version,
            CancellationToken ct)
        {
            var vpPath = string.IsNullOrEmpty(folder)
                ? $"{guid}/viewpoint.bcfv"
                : $"{folder}/{guid}.bcfv";

            // Try alternate path
            var entry = archive.GetEntry(vpPath) ??
                       archive.Entries.FirstOrDefault(e =>
                           e.FullName.Contains(guid) && e.Name.EndsWith(".bcfv"));

            if (entry == null) return new BCFViewpoint { Guid = guid };

            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();

            var doc = XDocument.Parse(xml);
            var root = doc.Root;

            if (root == null) return new BCFViewpoint { Guid = guid };

            var viewpoint = new BCFViewpoint
            {
                Guid = root.Attribute("Guid")?.Value ?? guid
            };

            // Parse perspective camera
            var perspCam = root.Element("PerspectiveCamera");
            if (perspCam != null)
            {
                viewpoint.PerspectiveCamera = new BCFPerspectiveCamera
                {
                    CameraViewPoint = ParsePoint(perspCam.Element("CameraViewPoint")),
                    CameraDirection = ParseDirection(perspCam.Element("CameraDirection")),
                    CameraUpVector = ParseDirection(perspCam.Element("CameraUpVector")),
                    FieldOfView = ParseDouble(perspCam.Element("FieldOfView")?.Value, 60.0)
                };
            }

            // Parse orthogonal camera
            var orthCam = root.Element("OrthogonalCamera");
            if (orthCam != null)
            {
                viewpoint.OrthogonalCamera = new BCFOrthogonalCamera
                {
                    CameraViewPoint = ParsePoint(orthCam.Element("CameraViewPoint")),
                    CameraDirection = ParseDirection(orthCam.Element("CameraDirection")),
                    CameraUpVector = ParseDirection(orthCam.Element("CameraUpVector")),
                    ViewToWorldScale = ParseDouble(orthCam.Element("ViewToWorldScale")?.Value, 1.0)
                };
            }

            // Parse components
            var componentsElem = root.Element("Components");
            if (componentsElem != null)
            {
                viewpoint.Components = new BCFComponents();

                var selectionElem = componentsElem.Element("Selection");
                if (selectionElem != null)
                {
                    viewpoint.Components.Selection = new BCFComponentSelection
                    {
                        Components = selectionElem.Elements("Component")
                            .Select(c => new BCFComponent
                            {
                                IfcGuid = c.Attribute("IfcGuid")?.Value ?? string.Empty,
                                OriginatingSystem = c.Element("OriginatingSystem")?.Value ?? string.Empty,
                                AuthoringToolId = c.Element("AuthoringToolId")?.Value ?? string.Empty
                            })
                            .ToList()
                    };
                }

                var visibilityElem = componentsElem.Element("Visibility");
                if (visibilityElem != null)
                {
                    viewpoint.Components.Visibility = new BCFComponentVisibility
                    {
                        DefaultVisibility = visibilityElem.Attribute("DefaultVisibility")?.Value == "true",
                        Exceptions = visibilityElem.Element("Exceptions")?.Elements("Component")
                            .Select(c => new BCFComponent
                            {
                                IfcGuid = c.Attribute("IfcGuid")?.Value ?? string.Empty
                            })
                            .ToList() ?? new()
                    };
                }
            }

            // Read snapshot
            var snapshotPath = string.IsNullOrEmpty(folder)
                ? $"{guid}/snapshot.png"
                : $"{folder}/{guid}.png";

            var snapshotEntry = archive.GetEntry(snapshotPath) ??
                               archive.Entries.FirstOrDefault(e =>
                                   e.FullName.Contains(guid) &&
                                   (e.Name.EndsWith(".png") || e.Name.EndsWith(".jpg")));

            if (snapshotEntry != null)
            {
                using var ms = new MemoryStream();
                await snapshotEntry.Open().CopyToAsync(ms, ct);
                viewpoint.SnapshotData = ms.ToArray();
                viewpoint.SnapshotFileName = snapshotEntry.Name;
            }

            return viewpoint;
        }

        private BCFPoint ParsePoint(XElement? elem)
        {
            if (elem == null) return new BCFPoint();
            return new BCFPoint(
                ParseDouble(elem.Element("X")?.Value, 0),
                ParseDouble(elem.Element("Y")?.Value, 0),
                ParseDouble(elem.Element("Z")?.Value, 0)
            );
        }

        private BCFDirection ParseDirection(XElement? elem)
        {
            if (elem == null) return new BCFDirection();
            return new BCFDirection(
                ParseDouble(elem.Element("X")?.Value, 0),
                ParseDouble(elem.Element("Y")?.Value, 0),
                ParseDouble(elem.Element("Z")?.Value, 0)
            );
        }

        private double ParseDouble(string? value, double defaultValue)
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result : defaultValue;
        }

        #endregion

        #region Export

        /// <summary>
        /// Export topics to BCF file
        /// </summary>
        public async Task<BCFExportResult> ExportAsync(
            BCFFile bcfFile,
            string? outputPath = null,
            CancellationToken ct = default)
        {
            var result = new BCFExportResult();

            try
            {
                outputPath ??= Path.Combine(
                    _config.ExportDirectory,
                    $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bcf");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using var fileStream = File.Create(outputPath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

                // Write version file
                await WriteVersionFileAsync(archive, bcfFile.Version, ct);

                // Write project file
                if (bcfFile.Project != null)
                {
                    await WriteProjectFileAsync(archive, bcfFile.Project, ct);
                }

                // Write extensions
                if (bcfFile.Extensions != null)
                {
                    await WriteExtensionsFileAsync(archive, bcfFile.Extensions, ct);
                }

                // Write each topic
                foreach (var topic in bcfFile.Topics)
                {
                    if (ct.IsCancellationRequested) break;

                    await WriteTopicAsync(archive, topic, bcfFile.Version, ct);
                    result.TopicsExported++;
                    result.ViewpointsExported += topic.Viewpoints.Count;
                    result.SnapshotsExported += topic.Viewpoints.Count(v => v.SnapshotData != null);
                }

                result.FilePath = outputPath;
                result.FileSize = new FileInfo(outputPath).Length;
                result.Success = true;

                _logger?.LogInformation(
                    "Exported BCF file {FileName}: {Topics} topics, {Size} bytes",
                    Path.GetFileName(outputPath), result.TopicsExported, result.FileSize);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export BCF file");
                result.Warnings.Add($"Export failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Export to stream
        /// </summary>
        public async Task<MemoryStream> ExportToStreamAsync(
            BCFFile bcfFile,
            CancellationToken ct = default)
        {
            var ms = new MemoryStream();
            using var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

            await WriteVersionFileAsync(archive, bcfFile.Version, ct);

            if (bcfFile.Project != null)
                await WriteProjectFileAsync(archive, bcfFile.Project, ct);

            if (bcfFile.Extensions != null)
                await WriteExtensionsFileAsync(archive, bcfFile.Extensions, ct);

            foreach (var topic in bcfFile.Topics)
            {
                await WriteTopicAsync(archive, topic, bcfFile.Version, ct);
            }

            ms.Position = 0;
            return ms;
        }

        private async Task WriteVersionFileAsync(ZipArchive archive, BCFVersion version, CancellationToken ct)
        {
            var entry = archive.CreateEntry("bcf.version");
            await using var writer = new StreamWriter(entry.Open());

            var versionString = version switch
            {
                BCFVersion.BCF_3_0 => "3.0",
                BCFVersion.BCF_2_1 => "2.1",
                _ => "2.0"
            };

            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Version VersionId=""{versionString}"" xmlns=""http://www.buildingsmart-tech.org/bcf/version_{versionString.Replace(".", "_")}"">
    <DetailedVersion>{versionString}</DetailedVersion>
</Version>";

            await writer.WriteAsync(xml);
        }

        private async Task WriteProjectFileAsync(ZipArchive archive, BCFProject project, CancellationToken ct)
        {
            var entry = archive.CreateEntry("project.bcfp");
            await using var writer = new StreamWriter(entry.Open());

            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ProjectExtension xmlns=""http://www.buildingsmart-tech.org/bcf/project"">
    <Project ProjectId=""{project.ProjectId}"">
        <Name>{EscapeXml(project.Name)}</Name>
    </Project>
</ProjectExtension>";

            await writer.WriteAsync(xml);
        }

        private async Task WriteExtensionsFileAsync(ZipArchive archive, BCFExtensions extensions, CancellationToken ct)
        {
            var entry = archive.CreateEntry("extensions.xml");
            await using var writer = new StreamWriter(entry.Open());

            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine(@"<Extensions xmlns=""http://www.buildingsmart-tech.org/bcf/extensions"">");

            sb.AppendLine("    <TopicTypes>");
            foreach (var type in extensions.TopicTypes)
                sb.AppendLine($"        <TopicType>{EscapeXml(type)}</TopicType>");
            sb.AppendLine("    </TopicTypes>");

            sb.AppendLine("    <TopicStatuses>");
            foreach (var status in extensions.TopicStatuses)
                sb.AppendLine($"        <TopicStatus>{EscapeXml(status)}</TopicStatus>");
            sb.AppendLine("    </TopicStatuses>");

            sb.AppendLine("    <Priorities>");
            foreach (var priority in extensions.Priorities)
                sb.AppendLine($"        <Priority>{EscapeXml(priority)}</Priority>");
            sb.AppendLine("    </Priorities>");

            sb.AppendLine("    <TopicLabels>");
            foreach (var label in extensions.TopicLabels)
                sb.AppendLine($"        <TopicLabel>{EscapeXml(label)}</TopicLabel>");
            sb.AppendLine("    </TopicLabels>");

            sb.AppendLine("</Extensions>");

            await writer.WriteAsync(sb.ToString());
        }

        private async Task WriteTopicAsync(ZipArchive archive, BCFTopic topic, BCFVersion version, CancellationToken ct)
        {
            var folderPrefix = $"{topic.Guid}/";

            // Write markup.bcf
            var markupEntry = archive.CreateEntry($"{folderPrefix}markup.bcf");
            await using (var writer = new StreamWriter(markupEntry.Open()))
            {
                var markup = GenerateMarkupXml(topic, version);
                await writer.WriteAsync(markup);
            }

            // Write viewpoints
            foreach (var viewpoint in topic.Viewpoints)
            {
                var vpEntry = archive.CreateEntry($"{folderPrefix}viewpoint_{viewpoint.Guid}.bcfv");
                await using (var writer = new StreamWriter(vpEntry.Open()))
                {
                    var vpXml = GenerateViewpointXml(viewpoint, version);
                    await writer.WriteAsync(vpXml);
                }

                // Write snapshot
                if (viewpoint.SnapshotData != null)
                {
                    var extension = Path.GetExtension(viewpoint.SnapshotFileName);
                    if (string.IsNullOrEmpty(extension)) extension = ".png";

                    var snapEntry = archive.CreateEntry($"{folderPrefix}snapshot_{viewpoint.Guid}{extension}");
                    await using var snapStream = snapEntry.Open();
                    await snapStream.WriteAsync(viewpoint.SnapshotData, 0, viewpoint.SnapshotData.Length, ct);
                }
            }
        }

        private string GenerateMarkupXml(BCFTopic topic, BCFVersion version)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine($@"<Markup xmlns=""http://www.buildingsmart-tech.org/bcf/markup"">");

            // Header
            sb.AppendLine("    <Header/>");

            // Topic
            sb.AppendLine($@"    <Topic Guid=""{topic.Guid}"" TopicType=""{EscapeXml(topic.TopicType)}"" TopicStatus=""{EscapeXml(topic.TopicStatus)}"">");
            sb.AppendLine($"        <Title>{EscapeXml(topic.Title)}</Title>");

            if (!string.IsNullOrEmpty(topic.Description))
                sb.AppendLine($"        <Description>{EscapeXml(topic.Description)}</Description>");

            if (!string.IsNullOrEmpty(topic.Priority))
                sb.AppendLine($"        <Priority>{EscapeXml(topic.Priority)}</Priority>");

            sb.AppendLine($"        <CreationDate>{topic.CreationDate:O}</CreationDate>");
            sb.AppendLine($"        <CreationAuthor>{EscapeXml(topic.CreationAuthor)}</CreationAuthor>");

            if (topic.ModifiedDate.HasValue)
                sb.AppendLine($"        <ModifiedDate>{topic.ModifiedDate.Value:O}</ModifiedDate>");

            if (!string.IsNullOrEmpty(topic.ModifiedAuthor))
                sb.AppendLine($"        <ModifiedAuthor>{EscapeXml(topic.ModifiedAuthor)}</ModifiedAuthor>");

            if (topic.DueDate.HasValue)
                sb.AppendLine($"        <DueDate>{topic.DueDate.Value:O}</DueDate>");

            if (!string.IsNullOrEmpty(topic.AssignedTo))
                sb.AppendLine($"        <AssignedTo>{EscapeXml(topic.AssignedTo)}</AssignedTo>");

            foreach (var label in topic.Labels)
                sb.AppendLine($"        <Labels>{EscapeXml(label)}</Labels>");

            sb.AppendLine("    </Topic>");

            // Comments
            foreach (var comment in topic.Comments)
            {
                sb.AppendLine($@"    <Comment Guid=""{comment.Guid}"">");
                sb.AppendLine($"        <Date>{comment.Date:O}</Date>");
                sb.AppendLine($"        <Author>{EscapeXml(comment.Author)}</Author>");
                sb.AppendLine($"        <Comment>{EscapeXml(comment.Comment)}</Comment>");

                if (!string.IsNullOrEmpty(comment.ViewpointGuid))
                    sb.AppendLine($@"        <Viewpoint Guid=""{comment.ViewpointGuid}""/>");

                sb.AppendLine("    </Comment>");
            }

            // Viewpoint references
            foreach (var vp in topic.Viewpoints)
            {
                sb.AppendLine($@"    <Viewpoints Guid=""{vp.Guid}"">");
                sb.AppendLine($"        <Viewpoint>viewpoint_{vp.Guid}.bcfv</Viewpoint>");

                if (vp.SnapshotData != null)
                {
                    var ext = Path.GetExtension(vp.SnapshotFileName);
                    if (string.IsNullOrEmpty(ext)) ext = ".png";
                    sb.AppendLine($"        <Snapshot>snapshot_{vp.Guid}{ext}</Snapshot>");
                }

                sb.AppendLine("    </Viewpoints>");
            }

            sb.AppendLine("</Markup>");
            return sb.ToString();
        }

        private string GenerateViewpointXml(BCFViewpoint viewpoint, BCFVersion version)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine($@"<VisualizationInfo Guid=""{viewpoint.Guid}"" xmlns=""http://www.buildingsmart-tech.org/bcf/viewpoint"">");

            // Camera
            if (viewpoint.PerspectiveCamera != null)
            {
                var cam = viewpoint.PerspectiveCamera;
                sb.AppendLine("    <PerspectiveCamera>");
                sb.AppendLine("        <CameraViewPoint>");
                sb.AppendLine($"            <X>{cam.CameraViewPoint.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraViewPoint.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraViewPoint.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraViewPoint>");
                sb.AppendLine("        <CameraDirection>");
                sb.AppendLine($"            <X>{cam.CameraDirection.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraDirection.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraDirection.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraDirection>");
                sb.AppendLine("        <CameraUpVector>");
                sb.AppendLine($"            <X>{cam.CameraUpVector.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraUpVector.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraUpVector.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraUpVector>");
                sb.AppendLine($"        <FieldOfView>{cam.FieldOfView.ToString(CultureInfo.InvariantCulture)}</FieldOfView>");
                sb.AppendLine("    </PerspectiveCamera>");
            }

            if (viewpoint.OrthogonalCamera != null)
            {
                var cam = viewpoint.OrthogonalCamera;
                sb.AppendLine("    <OrthogonalCamera>");
                sb.AppendLine("        <CameraViewPoint>");
                sb.AppendLine($"            <X>{cam.CameraViewPoint.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraViewPoint.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraViewPoint.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraViewPoint>");
                sb.AppendLine("        <CameraDirection>");
                sb.AppendLine($"            <X>{cam.CameraDirection.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraDirection.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraDirection.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraDirection>");
                sb.AppendLine("        <CameraUpVector>");
                sb.AppendLine($"            <X>{cam.CameraUpVector.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"            <Y>{cam.CameraUpVector.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"            <Z>{cam.CameraUpVector.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("        </CameraUpVector>");
                sb.AppendLine($"        <ViewToWorldScale>{cam.ViewToWorldScale.ToString(CultureInfo.InvariantCulture)}</ViewToWorldScale>");
                sb.AppendLine("    </OrthogonalCamera>");
            }

            // Components
            if (viewpoint.Components != null)
            {
                sb.AppendLine("    <Components>");

                if (viewpoint.Components.Selection?.Components.Any() == true)
                {
                    sb.AppendLine("        <Selection>");
                    foreach (var comp in viewpoint.Components.Selection.Components)
                    {
                        sb.AppendLine($@"            <Component IfcGuid=""{comp.IfcGuid}"">");
                        if (!string.IsNullOrEmpty(comp.OriginatingSystem))
                            sb.AppendLine($"                <OriginatingSystem>{EscapeXml(comp.OriginatingSystem)}</OriginatingSystem>");
                        if (!string.IsNullOrEmpty(comp.AuthoringToolId))
                            sb.AppendLine($"                <AuthoringToolId>{EscapeXml(comp.AuthoringToolId)}</AuthoringToolId>");
                        sb.AppendLine("            </Component>");
                    }
                    sb.AppendLine("        </Selection>");
                }

                if (viewpoint.Components.Visibility != null)
                {
                    sb.AppendLine($@"        <Visibility DefaultVisibility=""{viewpoint.Components.Visibility.DefaultVisibility.ToString().ToLower()}"">");
                    if (viewpoint.Components.Visibility.Exceptions.Any())
                    {
                        sb.AppendLine("            <Exceptions>");
                        foreach (var exc in viewpoint.Components.Visibility.Exceptions)
                        {
                            sb.AppendLine($@"                <Component IfcGuid=""{exc.IfcGuid}""/>");
                        }
                        sb.AppendLine("            </Exceptions>");
                    }
                    sb.AppendLine("        </Visibility>");
                }

                sb.AppendLine("    </Components>");
            }

            // Clipping planes
            foreach (var plane in viewpoint.ClippingPlanes)
            {
                sb.AppendLine("    <ClippingPlanes>");
                sb.AppendLine("        <ClippingPlane>");
                sb.AppendLine("            <Location>");
                sb.AppendLine($"                <X>{plane.Location.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"                <Y>{plane.Location.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"                <Z>{plane.Location.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("            </Location>");
                sb.AppendLine("            <Direction>");
                sb.AppendLine($"                <X>{plane.Direction.X.ToString(CultureInfo.InvariantCulture)}</X>");
                sb.AppendLine($"                <Y>{plane.Direction.Y.ToString(CultureInfo.InvariantCulture)}</Y>");
                sb.AppendLine($"                <Z>{plane.Direction.Z.ToString(CultureInfo.InvariantCulture)}</Z>");
                sb.AppendLine("            </Direction>");
                sb.AppendLine("        </ClippingPlane>");
                sb.AppendLine("    </ClippingPlanes>");
            }

            sb.AppendLine("</VisualizationInfo>");
            return sb.ToString();
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Convert internal issues to BCF topics
        /// </summary>
        public BCFTopic ConvertIssueToBCFTopic(
            dynamic issue,
            string authorName = "StingBIM")
        {
            return new BCFTopic
            {
                Guid = issue.Id?.ToString() ?? Guid.NewGuid().ToString(),
                Title = issue.Title ?? "Untitled",
                Description = issue.Description ?? "",
                TopicType = issue.IssueType ?? "Issue",
                TopicStatus = issue.Status ?? "Open",
                Priority = issue.Priority ?? "Normal",
                AssignedTo = issue.AssignedTo ?? "",
                CreationDate = issue.CreatedAt ?? DateTime.UtcNow,
                CreationAuthor = issue.CreatedBy ?? authorName,
                ModifiedDate = issue.ModifiedAt,
                DueDate = issue.DueDate
            };
        }

        /// <summary>
        /// Convert BCF topics to internal issues
        /// </summary>
        public dynamic ConvertBCFTopicToIssue(BCFTopic topic, string projectId)
        {
            return new
            {
                Id = topic.Guid,
                ProjectId = projectId,
                Title = topic.Title,
                Description = topic.Description,
                IssueType = topic.TopicType,
                Status = topic.TopicStatus,
                Priority = topic.Priority,
                AssignedTo = topic.AssignedTo,
                CreatedAt = topic.CreationDate,
                CreatedBy = topic.CreationAuthor,
                ModifiedAt = topic.ModifiedDate ?? topic.CreationDate,
                DueDate = topic.DueDate,
                Comments = topic.Comments.Select(c => new
                {
                    Id = c.Guid,
                    Text = c.Comment,
                    Author = c.Author,
                    CreatedAt = c.Date
                }).ToList(),
                ViewpointCount = topic.Viewpoints.Count
            };
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _loadedFiles.Clear();
            _logger?.LogInformation("BCFManager disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region BCF API Service

    /// <summary>
    /// BCF API service for REST integration (BCF API 3.0)
    /// </summary>
    public class BCFApiService
    {
        private readonly BCFManager _manager;
        private readonly ILogger? _logger;

        public BCFApiService(BCFManager manager, ILogger? logger = null)
        {
            _manager = manager;
            _logger = logger;
        }

        /// <summary>
        /// Get BCF version info (GET /bcf/version)
        /// </summary>
        public object GetVersions()
        {
            return new
            {
                versions = new[]
                {
                    new { version_id = "3.0", detailed_version = "3.0" },
                    new { version_id = "2.1", detailed_version = "2.1" }
                }
            };
        }

        /// <summary>
        /// Get authentication info (GET /bcf/{version}/auth)
        /// </summary>
        public object GetAuth(string version)
        {
            return new
            {
                oauth2_auth_url = "",
                oauth2_token_url = "",
                oauth2_dynamic_client_reg_url = "",
                http_basic_supported = true,
                supported_oauth2_flows = Array.Empty<string>()
            };
        }

        /// <summary>
        /// Get current user (GET /bcf/{version}/current-user)
        /// </summary>
        public object GetCurrentUser(string version, string userId)
        {
            return new
            {
                id = userId,
                name = "StingBIM User"
            };
        }

        /// <summary>
        /// List projects (GET /bcf/{version}/projects)
        /// </summary>
        public List<object> GetProjects(string version)
        {
            return new List<object>();
        }

        /// <summary>
        /// Get project (GET /bcf/{version}/projects/{project_id})
        /// </summary>
        public object? GetProject(string version, string projectId)
        {
            return null;
        }

        /// <summary>
        /// Get project extensions (GET /bcf/{version}/projects/{project_id}/extensions)
        /// </summary>
        public BCFExtensions GetExtensions(string version, string projectId)
        {
            return new BCFExtensions();
        }
    }

    #endregion
}
