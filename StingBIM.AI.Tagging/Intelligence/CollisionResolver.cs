// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// CollisionResolver.cs - 2D collision detection and resolution for Revit annotation tags
// Surpasses BIMLOGIQ collision avoidance with grid-based spatial indexing,
// multi-strategy resolution, configurable clearance, and batch analysis

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Supporting Types

    /// <summary>
    /// Entry stored in the spatial index representing an annotation's bounds and identifier.
    /// </summary>
    public class SpatialEntry
    {
        /// <summary>Unique identifier (TagId or annotation element id).</summary>
        public string Id { get; set; }

        /// <summary>Annotation type: "Tag", "Dimension", "TextNote", "DetailItem".</summary>
        public string AnnotationType { get; set; }

        /// <summary>Axis-aligned bounding box in view coordinates.</summary>
        public TagBounds2D Bounds { get; set; }

        /// <summary>The tag instance if this entry is a managed tag; null otherwise.</summary>
        public TagInstance Tag { get; set; }
    }

    /// <summary>
    /// Result of attempting to resolve collisions for a single tag.
    /// </summary>
    public class CollisionResolutionResult
    {
        public string TagId { get; set; }
        public bool FullyResolved { get; set; }
        public CollisionAction AppliedAction { get; set; }
        public TagPlacement ResolvedPlacement { get; set; }
        public TagBounds2D ResolvedBounds { get; set; }
        public bool RequiresAbbreviation { get; set; }
        public List<TagCollision> RemainingCollisions { get; set; } = new List<TagCollision>();
        public int StrategiesAttempted { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Summary of a batch collision analysis across all tags in a view.
    /// </summary>
    public class BatchCollisionReport
    {
        public int ViewId { get; set; }
        public int TotalAnnotations { get; set; }
        public int CollidingTagCount { get; set; }
        public int TotalCollisionPairs { get; set; }
        public Dictionary<string, List<TagCollision>> CollisionsByTag { get; set; }
            = new Dictionary<string, List<TagCollision>>();
        public Dictionary<IssueSeverity, int> CountBySeverity { get; set; }
            = new Dictionary<IssueSeverity, int>();
        public double CollisionDensity { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Configuration for the collision resolver.
    /// </summary>
    public class CollisionResolverSettings
    {
        /// <summary>Minimum clearance margin around each tag (model units). Default ~2mm.</summary>
        public double MinimumClearance { get; set; } = 0.002;

        /// <summary>Grid cell size. Zero = auto-compute from average tag width.</summary>
        public double GridCellSize { get; set; } = 0.0;

        /// <summary>Multiplier on average tag width for auto cell size. Default 2.0.</summary>
        public double AutoCellSizeMultiplier { get; set; } = 2.0;

        /// <summary>Max resolution attempts per tag before flagging manual. Default 20.</summary>
        public int MaxResolutionAttempts { get; set; } = 20;

        /// <summary>Leader extension step when rerouting (model units). Default ~10mm.</summary>
        public double LeaderExtensionIncrement { get; set; } = 0.01;

        /// <summary>Maximum leader length after rerouting. Default ~150mm.</summary>
        public double MaxLeaderLength { get; set; } = 0.15;

        /// <summary>Angular directions to probe when rerouting a leader. Default 8.</summary>
        public int LeaderRerouteDirections { get; set; } = 8;

        /// <summary>Vertical gap between stacked tags (model units). Default ~3mm.</summary>
        public double StackingGap { get; set; } = 0.003;

        /// <summary>Overlap % below which severity is Info. Default 5%.</summary>
        public double InfoOverlapThreshold { get; set; } = 0.05;

        /// <summary>Overlap % above which severity is Critical. Default 40%.</summary>
        public double CriticalOverlapThreshold { get; set; } = 0.40;

        /// <summary>Width reduction factor for abbreviation (0-1). Default 0.6.</summary>
        public double AbbreviationWidthFactor { get; set; } = 0.6;
    }

    #endregion

    /// <summary>
    /// 2D collision detection and resolution engine for Revit annotation tags.
    ///
    /// Uses a uniform-grid spatial index for O(1) average-case collision queries,
    /// replacing brute-force O(n^2) pairwise checks. Implements five resolution
    /// strategies (Nudge, Reposition, Stack, Abbreviate, LeaderReroute) tried in
    /// the order specified by a template's fallback chain.
    ///
    /// Surpasses BIMLOGIQ collision avoidance:
    /// - Spatial indexing instead of brute-force pairwise checks
    /// - Five resolution strategies vs. single nudge
    /// - Configurable clearance margins and severity thresholds
    /// - Batch analysis with severity classification
    /// - Stacking support for clustered elements
    /// - Leader rerouting with angular probing
    /// </summary>
    public class CollisionResolver
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        #region Fields

        private CollisionResolverSettings _settings;
        private Dictionary<(int Col, int Row), List<SpatialEntry>> _grid;
        private readonly List<SpatialEntry> _entries;
        private readonly Dictionary<string, SpatialEntry> _entryById;
        private double _cellSize;
        private double _gridOriginX;
        private double _gridOriginY;
        private int _gridCols;
        private int _gridRows;
        private bool _gridBuilt;

        #endregion

        #region Construction

        /// <summary>
        /// Initializes a new <see cref="CollisionResolver"/> with default settings.
        /// </summary>
        public CollisionResolver() : this(new CollisionResolverSettings()) { }

        /// <summary>
        /// Initializes a new <see cref="CollisionResolver"/> with explicit settings.
        /// </summary>
        /// <param name="settings">Configuration for clearance, grid sizing, and resolution.</param>
        public CollisionResolver(CollisionResolverSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _entries = new List<SpatialEntry>();
            _entryById = new Dictionary<string, SpatialEntry>(StringComparer.OrdinalIgnoreCase);
            _grid = new Dictionary<(int Col, int Row), List<SpatialEntry>>();
            _gridBuilt = false;
            Logger.Debug("CollisionResolver created with clearance={Clearance}, cellSize={CellSize}",
                _settings.MinimumClearance, _settings.GridCellSize);
        }

        #endregion

        #region Settings

        /// <summary>Gets the current resolver settings. Thread-safe.</summary>
        public CollisionResolverSettings Settings
        {
            get { lock (_lockObject) { return _settings; } }
        }

        /// <summary>
        /// Updates settings. Invalidates the spatial index; rebuild after changing grid settings.
        /// </summary>
        public void UpdateSettings(CollisionResolverSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            lock (_lockObject)
            {
                _settings = settings;
                _gridBuilt = false;
                Logger.Info("CollisionResolver settings updated; spatial index invalidated");
            }
        }

        #endregion

        #region Spatial Index Management

        /// <summary>Clears all entries from the spatial index.</summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _entries.Clear();
                _entryById.Clear();
                _grid.Clear();
                _gridBuilt = false;
            }
        }

        /// <summary>
        /// Adds a spatial entry. The grid is not rebuilt; call <see cref="BuildSpatialIndex"/> after.
        /// </summary>
        public void AddEntry(SpatialEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Bounds == null) throw new ArgumentException("Bounds must be non-null", nameof(entry));
            if (string.IsNullOrEmpty(entry.Id)) throw new ArgumentException("Id must be non-empty", nameof(entry));

            lock (_lockObject)
            {
                _entryById[entry.Id] = entry;
                _entries.Add(entry);
                _gridBuilt = false;
            }
        }

        /// <summary>Removes an entry by Id. Invalidates the grid.</summary>
        public bool RemoveEntry(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return false;
            lock (_lockObject)
            {
                if (_entryById.TryGetValue(entryId, out var entry))
                {
                    _entryById.Remove(entryId);
                    _entries.Remove(entry);
                    _gridBuilt = false;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Populates the spatial index from managed tags and a view context, then builds the grid.
        /// </summary>
        public void PopulateFromView(List<TagInstance> tags, ViewTagContext context)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (context == null) throw new ArgumentNullException(nameof(context));

            lock (_lockObject)
            {
                _entries.Clear();
                _entryById.Clear();
                _grid.Clear();
                _gridBuilt = false;

                foreach (var tag in tags)
                {
                    if (tag?.Bounds == null) continue;
                    var entry = new SpatialEntry
                    {
                        Id = tag.TagId ?? $"tag_{tag.RevitElementId}",
                        AnnotationType = "Tag",
                        Bounds = tag.Bounds,
                        Tag = tag
                    };
                    _entries.Add(entry);
                    _entryById[entry.Id] = entry;
                }

                int annotIdx = 0;
                foreach (var bounds in context.ExistingAnnotationBounds)
                {
                    if (bounds == null) continue;
                    string id = $"annotation_{annotIdx}";
                    _entries.Add(new SpatialEntry { Id = id, AnnotationType = "Annotation", Bounds = bounds });
                    _entryById[id] = _entries[_entries.Count - 1];
                    annotIdx++;
                }

                Logger.Debug("Populated {TagCount} tags + {AnnotCount} annotations from view {ViewId}",
                    tags.Count, annotIdx, context.ViewId);
            }
            BuildSpatialIndex();
        }

        /// <summary>
        /// Builds the uniform grid spatial index. Cell size is configured or auto-computed
        /// from average tag width.
        /// </summary>
        public void BuildSpatialIndex()
        {
            lock (_lockObject)
            {
                _grid.Clear();
                if (_entries.Count == 0) { _gridBuilt = true; return; }

                // World-space bounding box of all entries
                double wMinX = double.MaxValue, wMinY = double.MaxValue;
                double wMaxX = double.MinValue, wMaxY = double.MinValue;
                double totalW = 0; int wCount = 0;

                foreach (var e in _entries)
                {
                    var b = e.Bounds;
                    if (b.MinX < wMinX) wMinX = b.MinX;
                    if (b.MinY < wMinY) wMinY = b.MinY;
                    if (b.MaxX > wMaxX) wMaxX = b.MaxX;
                    if (b.MaxY > wMaxY) wMaxY = b.MaxY;
                    double w = b.Width;
                    if (w > 1e-10) { totalW += w; wCount++; }
                }

                double pad = _settings.MinimumClearance * 2.0;
                wMinX -= pad; wMinY -= pad; wMaxX += pad; wMaxY += pad;
                _gridOriginX = wMinX;
                _gridOriginY = wMinY;

                // Cell size: explicit or auto from average width
                if (_settings.GridCellSize > 1e-10)
                    _cellSize = _settings.GridCellSize;
                else
                {
                    double avg = wCount > 0 ? totalW / wCount : 0.02;
                    _cellSize = avg * _settings.AutoCellSizeMultiplier;
                    if (_cellSize < 1e-6) _cellSize = 0.02;
                }

                double worldW = wMaxX - wMinX, worldH = wMaxY - wMinY;
                _gridCols = Math.Max(1, (int)Math.Ceiling(worldW / _cellSize));
                _gridRows = Math.Max(1, (int)Math.Ceiling(worldH / _cellSize));

                const int maxDim = 2000;
                if (_gridCols > maxDim) _gridCols = maxDim;
                if (_gridRows > maxDim) _gridRows = maxDim;

                _cellSize = Math.Max(worldW / _gridCols, worldH / _gridRows);
                if (_cellSize < 1e-10) _cellSize = 0.02;

                foreach (var entry in _entries) InsertIntoGrid(entry);
                _gridBuilt = true;

                Logger.Debug("Grid built: {C}x{R}, cell={S:F6}, entries={N}",
                    _gridCols, _gridRows, _cellSize, _entries.Count);
            }
        }

        /// <summary>
        /// Updates the bounds of an existing entry in-place without a full rebuild.
        /// </summary>
        public bool UpdateEntryBounds(string entryId, TagBounds2D newBounds)
        {
            if (string.IsNullOrEmpty(entryId) || newBounds == null) return false;
            lock (_lockObject)
            {
                if (!_entryById.TryGetValue(entryId, out var entry)) return false;
                RemoveFromGrid(entry);
                entry.Bounds = newBounds;
                if (_gridBuilt) InsertIntoGrid(entry);
                return true;
            }
        }

        /// <summary>Total entries in the index.</summary>
        public int EntryCount { get { lock (_lockObject) { return _entries.Count; } } }

        /// <summary>Grid dimensions (columns, rows).</summary>
        public (int Cols, int Rows) GridDimensions { get { lock (_lockObject) { return (_gridCols, _gridRows); } } }

        /// <summary>Computed cell size of the grid.</summary>
        public double ComputedCellSize { get { lock (_lockObject) { return _cellSize; } } }

        #endregion

        #region Grid Helpers (caller must hold _lockObject)

        private void InsertIntoGrid(SpatialEntry entry)
        {
            double cl = _settings.MinimumClearance;
            var b = entry.Bounds;
            int cMin = CellCol(b.MinX - cl), cMax = CellCol(b.MaxX + cl);
            int rMin = CellRow(b.MinY - cl), rMax = CellRow(b.MaxY + cl);
            for (int c = cMin; c <= cMax; c++)
                for (int r = rMin; r <= rMax; r++)
                {
                    var key = (c, r);
                    if (!_grid.TryGetValue(key, out var list)) { list = new List<SpatialEntry>(4); _grid[key] = list; }
                    list.Add(entry);
                }
        }

        private void RemoveFromGrid(SpatialEntry entry)
        {
            double cl = _settings.MinimumClearance;
            var b = entry.Bounds;
            int cMin = CellCol(b.MinX - cl), cMax = CellCol(b.MaxX + cl);
            int rMin = CellRow(b.MinY - cl), rMax = CellRow(b.MaxY + cl);
            for (int c = cMin; c <= cMax; c++)
                for (int r = rMin; r <= rMax; r++)
                {
                    var key = (c, r);
                    if (_grid.TryGetValue(key, out var list)) { list.Remove(entry); if (list.Count == 0) _grid.Remove(key); }
                }
        }

        private int CellCol(double x)
        {
            int c = (int)((x - _gridOriginX) / _cellSize);
            return c < 0 ? 0 : c >= _gridCols ? _gridCols - 1 : c;
        }

        private int CellRow(double y)
        {
            int r = (int)((y - _gridOriginY) / _cellSize);
            return r < 0 ? 0 : r >= _gridRows ? _gridRows - 1 : r;
        }

        #endregion

        #region Collision Detection

        /// <summary>
        /// Queries the spatial index for all entries that collide with the given bounds,
        /// respecting the configured clearance margin.
        /// </summary>
        /// <param name="queryBounds">Bounding box to test against all indexed entries.</param>
        /// <param name="excludeId">Optional entry Id to exclude (typically the queried tag itself).</param>
        /// <returns>List of collisions with overlap area, percentage, separation vector, and severity.</returns>
        public List<TagCollision> DetectCollisions(TagBounds2D queryBounds, string excludeId = null)
        {
            if (queryBounds == null) throw new ArgumentNullException(nameof(queryBounds));
            lock (_lockObject)
            {
                if (!_gridBuilt)
                    throw new InvalidOperationException("Spatial index not built. Call BuildSpatialIndex() first.");
                return QueryCollisionsCore(queryBounds, excludeId);
            }
        }

        /// <summary>
        /// Detects collisions between a candidate bounding box and a set of existing tags in a view,
        /// using the specified clearance distance.
        /// </summary>
        /// <param name="queryBounds">Bounding box to test for collisions.</param>
        /// <param name="viewContext">The view context (used for spatial filtering).</param>
        /// <param name="existingTags">Existing tags in the view to test against.</param>
        /// <param name="clearance">Minimum clearance distance between tags.</param>
        /// <returns>List of detected collisions.</returns>
        public List<TagCollision> DetectCollisions(
            TagBounds2D queryBounds,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            double clearance)
        {
            if (queryBounds == null) return new List<TagCollision>();

            var collisions = new List<TagCollision>();
            var expandedQuery = new TagBounds2D(
                queryBounds.MinX - clearance,
                queryBounds.MinY - clearance,
                queryBounds.MaxX + clearance,
                queryBounds.MaxY + clearance);

            foreach (var tag in existingTags)
            {
                if (tag?.Bounds == null) continue;

                if (expandedQuery.Intersects(tag.Bounds))
                {
                    double overlapX = Math.Max(0,
                        Math.Min(queryBounds.MaxX, tag.Bounds.MaxX) -
                        Math.Max(queryBounds.MinX, tag.Bounds.MinX));
                    double overlapY = Math.Max(0,
                        Math.Min(queryBounds.MaxY, tag.Bounds.MaxY) -
                        Math.Max(queryBounds.MinY, tag.Bounds.MinY));
                    double overlapArea = overlapX * overlapY;

                    collisions.Add(new TagCollision
                    {
                        TagId = tag.TagId,
                        OverlapArea = overlapArea,
                        Severity = overlapArea > 0 ? IssueSeverity.Warning : IssueSeverity.Info
                    });
                }
            }

            return collisions;
        }

        /// <summary>
        /// Searches for a clear zone near the host element where a tag of the given size
        /// can be placed without collisions.
        /// </summary>
        /// <param name="hostBounds">Bounding box of the host element.</param>
        /// <param name="tagSize">Estimated bounding box dimensions of the tag.</param>
        /// <param name="viewContext">The view context for spatial filtering.</param>
        /// <param name="existingTags">Existing tags in the view to avoid.</param>
        /// <param name="maxLeaderLength">Maximum allowed leader length.</param>
        /// <returns>A clear position, or null if no clear zone was found.</returns>
        public Point2D? FindClearZone(
            TagBounds2D hostBounds,
            TagBounds2D tagSize,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            double maxLeaderLength)
        {
            if (hostBounds == null || tagSize == null) return null;

            var hostCenter = hostBounds.Center;
            double tagWidth = tagSize.Width;
            double tagHeight = tagSize.Height;
            double clearance = _settings.MinimumClearance;

            // Search in a spiral pattern around the host element
            double step = Math.Max(tagWidth, tagHeight) * 0.5;
            int maxSteps = 16;
            double[] angles = { 0, 90, 180, 270, 45, 135, 225, 315 };

            for (int ring = 1; ring <= maxSteps; ring++)
            {
                double distance = step * ring;
                if (distance > maxLeaderLength) break;

                foreach (double angleDeg in angles)
                {
                    double angleRad = angleDeg * Math.PI / 180.0;
                    double candidateX = hostCenter.X + distance * Math.Cos(angleRad);
                    double candidateY = hostCenter.Y + distance * Math.Sin(angleRad);

                    var candidateBounds = new TagBounds2D(
                        candidateX - tagWidth / 2,
                        candidateY - tagHeight / 2,
                        candidateX + tagWidth / 2,
                        candidateY + tagHeight / 2);

                    // Check against existing tags
                    bool clear = true;
                    foreach (var existingTag in existingTags)
                    {
                        if (existingTag?.Bounds == null) continue;
                        var expanded = new TagBounds2D(
                            candidateBounds.MinX - clearance,
                            candidateBounds.MinY - clearance,
                            candidateBounds.MaxX + clearance,
                            candidateBounds.MaxY + clearance);
                        if (expanded.Intersects(existingTag.Bounds))
                        {
                            clear = false;
                            break;
                        }
                    }

                    if (clear)
                        return new Point2D(candidateX, candidateY);
                }
            }

            return null;
        }

        /// <summary>
        /// Computes a displacement vector that would move the tag away from its colliding neighbors.
        /// Uses the average separation vector from all collisions.
        /// </summary>
        /// <param name="tagBounds">The tag's current bounding box.</param>
        /// <param name="collisions">List of detected collisions to resolve.</param>
        /// <returns>A nudge vector with X and Y components.</returns>
        public Vector2D ComputeNudgeVector(TagBounds2D tagBounds, List<TagCollision> collisions)
        {
            if (collisions == null || collisions.Count == 0 || tagBounds == null)
                return new Vector2D { X = 0, Y = 0 };

            double totalDx = 0, totalDy = 0;
            int count = 0;

            foreach (var collision in collisions)
            {
                if (collision.SeparationVector != null)
                {
                    totalDx += collision.SeparationVector.X;
                    totalDy += collision.SeparationVector.Y;
                    count++;
                }
            }

            if (count == 0)
            {
                // No separation vectors available; nudge slightly to the right and up
                double nudgeDist = _settings.MinimumClearance * 1.5;
                return new Vector2D { X = nudgeDist, Y = nudgeDist };
            }

            return new Vector2D { X = totalDx / count, Y = totalDy / count };
        }

        /// <summary>
        /// Detects collisions for a specific tag instance using its current bounds.
        /// </summary>
        public List<TagCollision> DetectCollisionsForTag(TagInstance tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (tag.Bounds == null) return new List<TagCollision>();
            string tagId = tag.TagId ?? $"tag_{tag.RevitElementId}";
            var collisions = DetectCollisions(tag.Bounds, tagId);
            foreach (var c in collisions) c.TagId = tagId;
            return collisions;
        }

        /// <summary>
        /// Tests whether a candidate bounding box is collision-free in the current index.
        /// </summary>
        public bool IsCollisionFree(TagBounds2D candidateBounds, string excludeId = null)
        {
            if (candidateBounds == null) return true;
            lock (_lockObject)
            {
                if (!_gridBuilt) return true;
                return IsCollisionFreeCore(candidateBounds, excludeId);
            }
        }

        /// <summary>
        /// Core collision query. Caller must hold <see cref="_lockObject"/>.
        /// Returns all collisions between queryBounds and indexed entries.
        /// </summary>
        private List<TagCollision> QueryCollisionsCore(TagBounds2D queryBounds, string excludeId)
        {
            double clearance = _settings.MinimumClearance;
            var collisions = new List<TagCollision>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int cMin = CellCol(queryBounds.MinX - clearance), cMax = CellCol(queryBounds.MaxX + clearance);
            int rMin = CellRow(queryBounds.MinY - clearance), rMax = CellRow(queryBounds.MaxY + clearance);
            TagBounds2D inflatedQ = queryBounds.Expand(clearance);

            for (int c = cMin; c <= cMax; c++)
            {
                for (int r = rMin; r <= rMax; r++)
                {
                    if (!_grid.TryGetValue((c, r), out var cellEntries)) continue;

                    foreach (var entry in cellEntries)
                    {
                        if (excludeId != null &&
                            string.Equals(entry.Id, excludeId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!visited.Add(entry.Id)) continue;

                        TagBounds2D inflatedE = entry.Bounds.Expand(clearance);
                        if (!inflatedQ.Intersects(inflatedE, 0.0)) continue;

                        double rawOverlap = queryBounds.OverlapArea(entry.Bounds);
                        double clearOverlap = inflatedQ.OverlapArea(inflatedE);
                        double effective = Math.Max(rawOverlap, clearOverlap * 0.5);
                        double qArea = queryBounds.Area;
                        double pct = qArea > 1e-14 ? effective / qArea : 1.0;

                        Vector2D sep = queryBounds.MinimumSeparationVector(entry.Bounds);
                        if (rawOverlap < 1e-14 && clearOverlap > 1e-14)
                        {
                            var norm = sep.Normalize();
                            double needed = clearance - sep.Length;
                            if (needed > 0) sep = norm.Scale(needed + clearance);
                        }

                        collisions.Add(new TagCollision
                        {
                            TagId = excludeId ?? string.Empty,
                            ConflictId = entry.Id,
                            ConflictType = entry.AnnotationType,
                            OverlapArea = effective,
                            OverlapPercentage = pct,
                            SeparationVector = sep,
                            Severity = ClassifySeverity(pct)
                        });
                    }
                }
            }
            return collisions;
        }

        /// <summary>
        /// Core collision-free check. Caller must hold <see cref="_lockObject"/>.
        /// Returns true if no indexed entry collides with candidateBounds.
        /// </summary>
        private bool IsCollisionFreeCore(TagBounds2D bounds, string excludeId)
        {
            double cl = _settings.MinimumClearance;
            int cMin = CellCol(bounds.MinX - cl), cMax = CellCol(bounds.MaxX + cl);
            int rMin = CellRow(bounds.MinY - cl), rMax = CellRow(bounds.MaxY + cl);
            TagBounds2D inflated = bounds.Expand(cl);

            for (int c = cMin; c <= cMax; c++)
                for (int r = rMin; r <= rMax; r++)
                {
                    if (!_grid.TryGetValue((c, r), out var list)) continue;
                    foreach (var e in list)
                    {
                        if (excludeId != null &&
                            string.Equals(e.Id, excludeId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (inflated.Intersects(e.Bounds.Expand(cl), 0.0)) return false;
                    }
                }
            return true;
        }

        private IssueSeverity ClassifySeverity(double pct)
        {
            if (pct >= _settings.CriticalOverlapThreshold) return IssueSeverity.Critical;
            if (pct >= _settings.InfoOverlapThreshold) return IssueSeverity.Warning;
            return IssueSeverity.Info;
        }

        #endregion

        #region Collision Resolution

        /// <summary>
        /// Attempts to resolve collisions for a tag by trying the template's fallback
        /// chain of collision actions in order. Stops on the first collision-free result.
        /// </summary>
        /// <param name="tag">The tag with collisions.</param>
        /// <param name="collisions">Current collisions for the tag.</param>
        /// <param name="template">Template providing fallback chain and placement preferences.</param>
        /// <param name="hostElementBounds">Host element bounds for leader/reposition calculations.</param>
        /// <returns>Resolution result describing outcome and new placement.</returns>
        public CollisionResolutionResult ResolveCollisions(
            TagInstance tag,
            List<TagCollision> collisions,
            TagTemplateDefinition template,
            TagBounds2D hostElementBounds)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (collisions == null) throw new ArgumentNullException(nameof(collisions));
            if (template == null) throw new ArgumentNullException(nameof(template));

            string tagId = tag.TagId ?? $"tag_{tag.RevitElementId}";

            if (collisions.Count == 0)
                return new CollisionResolutionResult
                {
                    TagId = tagId, FullyResolved = true, AppliedAction = CollisionAction.Nudge,
                    ResolvedPlacement = tag.Placement, ResolvedBounds = tag.Bounds,
                    Message = "No collisions to resolve"
                };

            Logger.Debug("Resolving {Count} collisions for tag {TagId}", collisions.Count, tagId);

            var chain = template.FallbackChain != null && template.FallbackChain.Count > 0
                ? template.FallbackChain
                : new List<CollisionAction>
                {
                    CollisionAction.Reposition, CollisionAction.Nudge, CollisionAction.LeaderReroute,
                    CollisionAction.Stack, CollisionAction.Abbreviate, CollisionAction.FlagManual
                };

            int attempts = 0;

            lock (_lockObject)
            {
                foreach (var action in chain)
                {
                    if (attempts >= _settings.MaxResolutionAttempts) break;
                    attempts++;

                    CollisionResolutionResult result;
                    switch (action)
                    {
                        case CollisionAction.Nudge:
                            result = TryNudge(tag, collisions, tagId); break;
                        case CollisionAction.Reposition:
                            result = TryReposition(tag, template, hostElementBounds, tagId); break;
                        case CollisionAction.Stack:
                            result = TryStack(tag, collisions, tagId); break;
                        case CollisionAction.Abbreviate:
                            result = TryAbbreviate(tag, tagId); break;
                        case CollisionAction.LeaderReroute:
                            result = TryLeaderReroute(tag, hostElementBounds, tagId); break;
                        case CollisionAction.FlagManual:
                            return new CollisionResolutionResult
                            {
                                TagId = tagId, FullyResolved = false, AppliedAction = CollisionAction.FlagManual,
                                ResolvedPlacement = tag.Placement, ResolvedBounds = tag.Bounds,
                                RemainingCollisions = new List<TagCollision>(collisions),
                                StrategiesAttempted = attempts,
                                Message = "All strategies exhausted; flagged for manual review"
                            };
                        default: continue;
                    }

                    if (result != null && result.FullyResolved)
                    {
                        result.StrategiesAttempted = attempts;
                        Logger.Debug("Tag {TagId} resolved via {Action}", tagId, action);
                        return result;
                    }
                }
            }

            return new CollisionResolutionResult
            {
                TagId = tagId, FullyResolved = false, AppliedAction = CollisionAction.FlagManual,
                ResolvedPlacement = tag.Placement, ResolvedBounds = tag.Bounds,
                RemainingCollisions = _gridBuilt ? QueryCollisionsCore(tag.Bounds, tagId) : new List<TagCollision>(),
                StrategiesAttempted = attempts,
                Message = $"Unable to resolve after {attempts} strategies"
            };
        }

        #endregion

        #region Strategy: Nudge

        /// <summary>
        /// Iteratively shifts the tag by the minimum displacement vector to separate it
        /// from the worst collision. Caller must hold <see cref="_lockObject"/>.
        /// </summary>
        private CollisionResolutionResult TryNudge(
            TagInstance tag, List<TagCollision> collisions, string tagId)
        {
            TagBounds2D cur = CloneBounds(tag.Bounds);
            Point2D pos = tag.Placement != null ? tag.Placement.Position : cur.Center;
            double cl = _settings.MinimumClearance;
            int maxIter = Math.Min(_settings.MaxResolutionAttempts, 10);

            for (int i = 0; i < maxIter; i++)
            {
                var hits = QueryCollisionsCore(cur, tagId);
                if (hits.Count == 0)
                    return SuccessResult(tagId, CollisionAction.Nudge, pos, cur, "Nudge cleared collisions");

                var worst = hits.OrderByDescending(h => h.OverlapArea).First();
                Vector2D sep = worst.SeparationVector;
                double len = sep.Length;

                Vector2D disp;
                if (len > 1e-10)
                {
                    disp = sep.Normalize().Scale(len + cl * 1.5);
                }
                else
                {
                    double push = Math.Max(cur.Width, cur.Height) * 0.5 + cl;
                    disp = new Vector2D(push, -push * 0.5);
                }

                pos = pos.Offset(disp.X, disp.Y);
                cur = OffsetBounds(cur, disp.X, disp.Y);
            }

            if (QueryCollisionsCore(cur, tagId).Count == 0)
                return SuccessResult(tagId, CollisionAction.Nudge, pos, cur, "Nudge resolved after max iterations");

            return null;
        }

        #endregion

        #region Strategy: Reposition

        /// <summary>
        /// Tries each candidate position from the template's preferred positions.
        /// Caller must hold <see cref="_lockObject"/>.
        /// </summary>
        private CollisionResolutionResult TryReposition(
            TagInstance tag, TagTemplateDefinition template,
            TagBounds2D hostBounds, string tagId)
        {
            if (hostBounds == null) return null;

            var positions = template.PreferredPositions != null && template.PreferredPositions.Count > 0
                ? template.PreferredPositions
                : new List<TagPosition>
                {
                    TagPosition.Top, TagPosition.Right, TagPosition.Bottom, TagPosition.Left,
                    TagPosition.TopRight, TagPosition.TopLeft,
                    TagPosition.BottomRight, TagPosition.BottomLeft, TagPosition.Center
                };

            double tw = tag.Bounds?.Width ?? 0.02;
            double th = tag.Bounds?.Height ?? 0.005;
            double cl = _settings.MinimumClearance;
            Point2D hc = hostBounds.Center;
            double hhw = hostBounds.Width / 2.0, hhh = hostBounds.Height / 2.0;

            foreach (var p in positions)
            {
                Point2D center = ComputePositionOffset(hc, hhw, hhh, tw, th, cl, p,
                    template.OffsetX, template.OffsetY);
                TagBounds2D cb = BoundsFromCenter(center, tw, th);

                if (IsCollisionFreeCore(cb, tagId))
                    return SuccessResult(tagId, CollisionAction.Reposition, center, cb,
                        $"Repositioned to {p}");
            }
            return null;
        }

        /// <summary>
        /// Computes center point for a tag at a specific position relative to host element.
        /// </summary>
        private static Point2D ComputePositionOffset(
            Point2D hc, double hhw, double hhh,
            double tw, double th, double cl,
            TagPosition pos, double offX, double offY)
        {
            double gapX = hhw + tw / 2.0 + cl;
            double gapY = hhh + th / 2.0 + cl;
            double dx = 0, dy = 0;

            switch (pos)
            {
                case TagPosition.TopLeft:     dx = -gapX; dy = gapY;  break;
                case TagPosition.Top:         dx = 0;     dy = gapY;  break;
                case TagPosition.TopRight:    dx = gapX;  dy = gapY;  break;
                case TagPosition.Left:        dx = -gapX; dy = 0;     break;
                case TagPosition.Center:      dx = 0;     dy = 0;     break;
                case TagPosition.Right:       dx = gapX;  dy = 0;     break;
                case TagPosition.BottomLeft:  dx = -gapX; dy = -gapY; break;
                case TagPosition.Bottom:      dx = 0;     dy = -gapY; break;
                case TagPosition.BottomRight: dx = gapX;  dy = -gapY; break;
                case TagPosition.InsertionPoint: break;
                case TagPosition.Auto:        dx = gapX;  dy = 0;     break;
            }
            return new Point2D(hc.X + dx + offX, hc.Y + dy + offY);
        }

        #endregion

        #region Strategy: Stack

        /// <summary>
        /// Places the tag vertically adjacent to the worst conflicting tag.
        /// Tries below, above, then two rows in each direction.
        /// Caller must hold <see cref="_lockObject"/>.
        /// </summary>
        private CollisionResolutionResult TryStack(
            TagInstance tag, List<TagCollision> collisions, string tagId)
        {
            if (collisions.Count == 0) return null;

            var worst = collisions.OrderByDescending(c => c.OverlapArea).First();
            if (!_entryById.TryGetValue(worst.ConflictId, out var conflict) || conflict.Bounds == null)
                return null;

            double th = tag.Bounds?.Height ?? 0.005;
            double tw = tag.Bounds?.Width ?? 0.02;
            double gap = _settings.StackingGap;
            double cx = conflict.Bounds.Center.X;

            // Try: below, above, far below, far above
            double[] yOffsets =
            {
                conflict.Bounds.MinY - gap - th / 2.0,
                conflict.Bounds.MaxY + gap + th / 2.0,
                conflict.Bounds.MinY - gap * 2 - th * 1.5,
                conflict.Bounds.MaxY + gap * 2 + th * 1.5
            };

            foreach (double y in yOffsets)
            {
                var center = new Point2D(cx, y);
                var bounds = BoundsFromCenter(center, tw, th);
                if (IsCollisionFreeCore(bounds, tagId))
                {
                    return new CollisionResolutionResult
                    {
                        TagId = tagId, FullyResolved = true, AppliedAction = CollisionAction.Stack,
                        ResolvedPlacement = new TagPlacement
                        {
                            Position = center, IsStacked = true,
                            StackedWithTagId = worst.ConflictId,
                            ResolvedPosition = TagPosition.Bottom
                        },
                        ResolvedBounds = bounds,
                        Message = "Stacked adjacent to conflicting tag"
                    };
                }
            }
            return null;
        }

        #endregion

        #region Strategy: Abbreviate

        /// <summary>
        /// Reduces tag width by the abbreviation factor and checks if collisions clear.
        /// Caller must hold <see cref="_lockObject"/>.
        /// </summary>
        private CollisionResolutionResult TryAbbreviate(TagInstance tag, string tagId)
        {
            if (tag.Bounds == null) return null;

            double origW = tag.Bounds.Width;
            double reducedW = origW * _settings.AbbreviationWidthFactor;
            double delta = (origW - reducedW) / 2.0;

            var abbrevBounds = new TagBounds2D(
                tag.Bounds.MinX + delta, tag.Bounds.MinY,
                tag.Bounds.MaxX - delta, tag.Bounds.MaxY);

            if (IsCollisionFreeCore(abbrevBounds, tagId))
            {
                var placement = tag.Placement != null ? ClonePlacement(tag.Placement)
                    : new TagPlacement { Position = abbrevBounds.Center };

                return new CollisionResolutionResult
                {
                    TagId = tagId, FullyResolved = true, AppliedAction = CollisionAction.Abbreviate,
                    ResolvedPlacement = placement, ResolvedBounds = abbrevBounds,
                    RequiresAbbreviation = true,
                    Message = $"Abbreviation reduces width {origW:F4} -> {reducedW:F4}"
                };
            }
            return null;
        }

        #endregion

        #region Strategy: Leader Reroute

        /// <summary>
        /// Probes angular directions at increasing radii from the host element center
        /// until a collision-free position is found. Caller must hold <see cref="_lockObject"/>.
        /// </summary>
        private CollisionResolutionResult TryLeaderReroute(
            TagInstance tag, TagBounds2D hostBounds, string tagId)
        {
            if (hostBounds == null) return null;

            double tw = tag.Bounds?.Width ?? 0.02;
            double th = tag.Bounds?.Height ?? 0.005;
            Point2D hc = hostBounds.Center;

            int dirs = Math.Max(4, _settings.LeaderRerouteDirections);
            double step = 2.0 * Math.PI / dirs;
            double radius = _settings.LeaderExtensionIncrement;
            double maxR = _settings.MaxLeaderLength;

            // Base angle: prefer direction from host to current tag position
            double baseAngle = 0.0;
            if (tag.Bounds != null)
            {
                var toTag = Vector2D.FromPoints(hc, tag.Bounds.Center);
                if (toTag.Length > 1e-10) baseAngle = Math.Atan2(toTag.Y, toTag.X);
            }

            while (radius <= maxR)
            {
                for (int d = 0; d < dirs; d++)
                {
                    double angle = baseAngle + d * step;
                    double dx = Math.Cos(angle) * radius;
                    double dy = Math.Sin(angle) * radius;

                    var center = new Point2D(hc.X + dx, hc.Y + dy);
                    var bounds = BoundsFromCenter(center, tw, th);

                    if (IsCollisionFreeCore(bounds, tagId))
                    {
                        Point2D leaderEnd = ComputeLeaderEndPoint(hc, hostBounds, angle);
                        return new CollisionResolutionResult
                        {
                            TagId = tagId, FullyResolved = true,
                            AppliedAction = CollisionAction.LeaderReroute,
                            ResolvedPlacement = new TagPlacement
                            {
                                Position = center, LeaderEndPoint = leaderEnd,
                                LeaderType = radius > _settings.LeaderExtensionIncrement * 2
                                    ? LeaderType.Elbow : LeaderType.Straight,
                                LeaderLength = radius,
                                ResolvedPosition = AngleToTagPosition(angle)
                            },
                            ResolvedBounds = bounds,
                            Message = $"Leader rerouted: angle {angle * 180.0 / Math.PI:F0} deg, radius {radius:F4}"
                        };
                    }
                }
                radius += _settings.LeaderExtensionIncrement;
            }
            return null;
        }

        /// <summary>
        /// Computes the leader endpoint on the host boundary via ray-AABB intersection.
        /// </summary>
        private static Point2D ComputeLeaderEndPoint(Point2D hc, TagBounds2D hb, double angle)
        {
            double cosA = Math.Cos(angle), sinA = Math.Sin(angle);
            double hw = hb.Width / 2.0, hh = hb.Height / 2.0;
            double tX = Math.Abs(cosA) > 1e-10 ? hw / Math.Abs(cosA) : double.MaxValue;
            double tY = Math.Abs(sinA) > 1e-10 ? hh / Math.Abs(sinA) : double.MaxValue;
            double t = Math.Min(tX, tY);
            return new Point2D(hc.X + cosA * t, hc.Y + sinA * t);
        }

        /// <summary>
        /// Maps an angle in radians to the closest TagPosition enum value.
        /// </summary>
        private static TagPosition AngleToTagPosition(double angle)
        {
            double deg = (angle % (2.0 * Math.PI)) * 180.0 / Math.PI;
            if (deg < 0) deg += 360.0;
            if (deg >= 337.5 || deg < 22.5) return TagPosition.Right;
            if (deg < 67.5)  return TagPosition.TopRight;
            if (deg < 112.5) return TagPosition.Top;
            if (deg < 157.5) return TagPosition.TopLeft;
            if (deg < 202.5) return TagPosition.Left;
            if (deg < 247.5) return TagPosition.BottomLeft;
            if (deg < 292.5) return TagPosition.Bottom;
            return TagPosition.BottomRight;
        }

        #endregion

        #region Batch Analysis

        /// <summary>
        /// Checks all annotations in the index for pairwise collisions.
        /// Returns a comprehensive report with severity counts and collision density.
        /// </summary>
        public BatchCollisionReport CheckAllCollisions()
        {
            var start = DateTime.UtcNow;
            lock (_lockObject)
            {
                if (!_gridBuilt)
                    throw new InvalidOperationException("Spatial index not built.");

                var report = new BatchCollisionReport
                {
                    TotalAnnotations = _entries.Count,
                    AnalyzedAt = DateTime.UtcNow,
                    CountBySeverity = new Dictionary<IssueSeverity, int>
                    { { IssueSeverity.Info, 0 }, { IssueSeverity.Warning, 0 }, { IssueSeverity.Critical, 0 } }
                };

                var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in _entries)
                {
                    var hits = QueryCollisionsCore(entry.Bounds, entry.Id);
                    if (hits.Count == 0) continue;

                    var unique = new List<TagCollision>();
                    foreach (var h in hits)
                    {
                        string pk = StringComparer.OrdinalIgnoreCase.Compare(entry.Id, h.ConflictId) <= 0
                            ? $"{entry.Id}||{h.ConflictId}" : $"{h.ConflictId}||{entry.Id}";
                        if (pairs.Add(pk)) { h.TagId = entry.Id; unique.Add(h); }
                    }
                    if (unique.Count > 0)
                    {
                        report.CollisionsByTag[entry.Id] = unique;
                        foreach (var u in unique) { report.CountBySeverity[u.Severity]++; report.TotalCollisionPairs++; }
                    }
                }

                report.CollidingTagCount = report.CollisionsByTag.Count;
                report.CollisionDensity = _entries.Count > 0
                    ? (double)report.TotalCollisionPairs / _entries.Count : 0.0;
                report.Duration = DateTime.UtcNow - start;

                Logger.Info("Batch check: {N} annots, {C} colliding, {P} pairs, density={D:F3}",
                    report.TotalAnnotations, report.CollidingTagCount,
                    report.TotalCollisionPairs, report.CollisionDensity);
                return report;
            }
        }

        /// <summary>
        /// Performs batch collision check scoped to a specific view.
        /// </summary>
        public BatchCollisionReport CheckAllCollisionsForView(int viewId)
        {
            var report = CheckAllCollisions();
            report.ViewId = viewId;
            return report;
        }

        /// <summary>
        /// Resolves all collisions in the index. Processes tags in descending severity order
        /// so worst offenders get priority for the best positions.
        /// </summary>
        /// <param name="tags">All managed tags in the view.</param>
        /// <param name="template">Template providing fallback chain.</param>
        /// <param name="hostBoundsLookup">Returns host element bounds for a tag (may return null).</param>
        public List<CollisionResolutionResult> ResolveAllCollisions(
            List<TagInstance> tags,
            TagTemplateDefinition template,
            Func<TagInstance, TagBounds2D> hostBoundsLookup)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (hostBoundsLookup == null) throw new ArgumentNullException(nameof(hostBoundsLookup));

            var results = new List<CollisionResolutionResult>();
            var tagHits = new Dictionary<string, List<TagCollision>>();

            foreach (var tag in tags)
            {
                if (tag?.Bounds == null) continue;
                string id = tag.TagId ?? $"tag_{tag.RevitElementId}";
                var hits = DetectCollisionsForTag(tag);
                if (hits.Count > 0) tagHits[id] = hits;
            }

            if (tagHits.Count == 0) return results;

            // Sort: Critical first, then by total overlap area descending
            var sorted = tagHits
                .OrderByDescending(kv => kv.Value.Max(c => (int)c.Severity))
                .ThenByDescending(kv => kv.Value.Sum(c => c.OverlapArea))
                .Select(kv => kv.Key).ToList();

            Logger.Info("ResolveAllCollisions: {Count} tags with collisions", sorted.Count);

            foreach (var id in sorted)
            {
                var tag = tags.FirstOrDefault(t =>
                    string.Equals(t.TagId, id, StringComparison.OrdinalIgnoreCase) ||
                    $"tag_{t.RevitElementId}" == id);
                if (tag == null) continue;

                // Re-detect: prior resolutions may have cleared this tag's collisions
                var current = DetectCollisionsForTag(tag);
                if (current.Count == 0) continue;

                var result = ResolveCollisions(tag, current, template, hostBoundsLookup(tag));
                results.Add(result);

                if (result.FullyResolved && result.ResolvedBounds != null)
                    UpdateEntryBounds(id, result.ResolvedBounds);
            }

            Logger.Info("ResolveAllCollisions: {R}/{T} resolved",
                results.Count(r => r.FullyResolved), results.Count);
            return results;
        }

        #endregion

        #region Geometry Utilities

        private static TagBounds2D CloneBounds(TagBounds2D s)
        {
            return s == null ? null : new TagBounds2D(s.MinX, s.MinY, s.MaxX, s.MaxY);
        }

        private static TagBounds2D OffsetBounds(TagBounds2D s, double dx, double dy)
        {
            return new TagBounds2D(s.MinX + dx, s.MinY + dy, s.MaxX + dx, s.MaxY + dy);
        }

        private static TagBounds2D BoundsFromCenter(Point2D c, double w, double h)
        {
            double hw = w / 2.0, hh = h / 2.0;
            return new TagBounds2D(c.X - hw, c.Y - hh, c.X + hw, c.Y + hh);
        }

        private static TagPlacement ClonePlacement(TagPlacement s)
        {
            if (s == null) return null;
            return new TagPlacement
            {
                Position = s.Position, LeaderEndPoint = s.LeaderEndPoint,
                LeaderElbowPoint = s.LeaderElbowPoint, LeaderType = s.LeaderType,
                LeaderLength = s.LeaderLength, Rotation = s.Rotation,
                PreferredPosition = s.PreferredPosition, ResolvedPosition = s.ResolvedPosition,
                Orientation = s.Orientation, OffsetX = s.OffsetX, OffsetY = s.OffsetY,
                IsStacked = s.IsStacked, StackedWithTagId = s.StackedWithTagId
            };
        }

        private static CollisionResolutionResult SuccessResult(
            string tagId, CollisionAction action, Point2D pos, TagBounds2D bounds, string msg)
        {
            return new CollisionResolutionResult
            {
                TagId = tagId, FullyResolved = true, AppliedAction = action,
                ResolvedPlacement = new TagPlacement { Position = pos },
                ResolvedBounds = bounds, Message = msg
            };
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Returns a diagnostic summary of the spatial index state for logging and debugging.
        /// </summary>
        public string GetDiagnostics()
        {
            lock (_lockObject)
            {
                if (!_gridBuilt) return "Spatial index not built";

                int occ = _grid.Count;
                int total = _gridCols * _gridRows;
                int maxPer = 0; int sumPer = 0;
                foreach (var cell in _grid.Values)
                {
                    if (cell.Count > maxPer) maxPer = cell.Count;
                    sumPer += cell.Count;
                }
                double avg = occ > 0 ? (double)sumPer / occ : 0;

                return $"CollisionResolver: {_entries.Count} entries, " +
                       $"{_gridCols}x{_gridRows} grid ({occ}/{total} occupied), " +
                       $"cell={_cellSize:F6}, max/cell={maxPer}, avg/cell={avg:F2}, " +
                       $"clearance={_settings.MinimumClearance:F6}";
            }
        }

        /// <summary>Returns a copy of all entries in the index (for testing/diagnostics).</summary>
        public List<SpatialEntry> GetAllEntries()
        {
            lock (_lockObject) { return new List<SpatialEntry>(_entries); }
        }

        /// <summary>Retrieves a specific entry by identifier.</summary>
        public SpatialEntry GetEntry(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;
            lock (_lockObject) { _entryById.TryGetValue(entryId, out var e); return e; }
        }

        #endregion
    }
}
