// StingBIM.AI.Spatial.Analysis.SpatialAnalyzer3D
// 3D spatial analysis for collision detection, clearances, orientations, and views
// Master Proposal Reference: Part 2.2 Strategy 2 - Spatial Intelligence (3D Analysis)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Spatial.Analysis
{
    #region Geometric Primitives

    /// <summary>
    /// 3D point for spatial analysis. Local definition to support vector arithmetic.
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D() { }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double DistanceTo(Point3D other)
        {
            if (other == null) return double.MaxValue;
            var dx = other.X - X;
            var dy = other.Y - Y;
            var dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Adds a vector offset to this point.</summary>
        public Point3D Add(Vector3D offset) =>
            new Point3D(X + offset.X, Y + offset.Y, Z + offset.Z);

        /// <summary>Adds another point's coordinates.</summary>
        public Point3D Add(Point3D other) =>
            other == null ? new Point3D(X, Y, Z) : new Point3D(X + other.X, Y + other.Y, Z + other.Z);

        /// <summary>Subtracts another point, returning a direction vector.</summary>
        public Vector3D Subtract(Point3D other) =>
            other == null ? new Vector3D(X, Y, Z) : new Vector3D(X - other.X, Y - other.Y, Z - other.Z);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>
    /// 3D vector for directions and offsets.
    /// </summary>
    public struct Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthXY => Math.Sqrt(X * X + Y * Y);

        public Vector3D Normalize()
        {
            var len = Length;
            return len > 0 ? new Vector3D(X / len, Y / len, Z / len) : this;
        }

        public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vector3D Cross(Vector3D other) => new Vector3D(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);

        public Vector3D Scale(double factor) => new Vector3D(X * factor, Y * factor, Z * factor);

        public Point3D ToPoint3D() => new Point3D(X, Y, Z);

        public static Vector3D UnitX => new Vector3D(1, 0, 0);
        public static Vector3D UnitY => new Vector3D(0, 1, 0);
        public static Vector3D UnitZ => new Vector3D(0, 0, 1);

        public override string ToString() => $"<{X:F2}, {Y:F2}, {Z:F2}>";
    }

    /// <summary>
    /// Axis-aligned bounding box for spatial queries.
    /// </summary>
    public class BoundingBox3D
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public BoundingBox3D(Point3D min, Point3D max)
        {
            Min = min;
            Max = max;
        }

        public double Width => Max.X - Min.X;
        public double Depth => Max.Y - Min.Y;
        public double Height => Max.Z - Min.Z;
        public double Volume => Width * Depth * Height;

        public Point3D Center => new Point3D(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2);

        public bool Contains(Point3D point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Intersects(BoundingBox3D other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }

        public BoundingBox3D Expand(double margin)
        {
            return new BoundingBox3D(
                new Point3D(Min.X - margin, Min.Y - margin, Min.Z - margin),
                new Point3D(Max.X + margin, Max.Y + margin, Max.Z + margin));
        }

        public BoundingBox3D Union(BoundingBox3D other)
        {
            return new BoundingBox3D(
                new Point3D(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
                new Point3D(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));
        }

        public BoundingBox3D Intersection(BoundingBox3D other)
        {
            if (!Intersects(other)) return null;
            return new BoundingBox3D(
                new Point3D(Math.Max(Min.X, other.Min.X), Math.Max(Min.Y, other.Min.Y), Math.Max(Min.Z, other.Min.Z)),
                new Point3D(Math.Min(Max.X, other.Max.X), Math.Min(Max.Y, other.Max.Y), Math.Min(Max.Z, other.Max.Z)));
        }
    }

    #endregion

    #region Spatial Elements

    /// <summary>
    /// Represents a BIM element in 3D space.
    /// </summary>
    public class SpatialElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public BoundingBox3D Bounds { get; set; }
        public Point3D Location { get; set; }
        public Vector3D FacingDirection { get; set; }
        public double Rotation { get; set; } // Degrees around Z-axis
        public Dictionary<string, object> Properties { get; set; }
        public List<ClearanceZone> RequiredClearances { get; set; }

        public SpatialElement()
        {
            Properties = new Dictionary<string, object>();
            RequiredClearances = new List<ClearanceZone>();
            FacingDirection = Vector3D.UnitY;
        }

        public Point3D Center => Bounds?.Center ?? Location;
    }

    /// <summary>
    /// Clearance zone required around an element.
    /// </summary>
    public class ClearanceZone
    {
        public string Name { get; set; }
        public ClearanceType Type { get; set; }
        public double Front { get; set; }
        public double Back { get; set; }
        public double Left { get; set; }
        public double Right { get; set; }
        public double Above { get; set; }
        public double Below { get; set; }
        public bool IsRequired { get; set; } = true;
        public string Standard { get; set; } // e.g., "ADA", "NFPA", "Local"

        /// <summary>
        /// Gets the bounding box for this clearance zone relative to an element.
        /// </summary>
        public BoundingBox3D GetBounds(SpatialElement element)
        {
            var bounds = element.Bounds;
            if (bounds == null) return null;

            // Calculate clearance box based on element orientation
            var cos = Math.Cos(element.Rotation * Math.PI / 180);
            var sin = Math.Sin(element.Rotation * Math.PI / 180);

            var frontOffset = Front * cos;
            var sideOffset = Right * sin;

            return new BoundingBox3D(
                new Point3D(bounds.Min.X - Left, bounds.Min.Y - Back, bounds.Min.Z - Below),
                new Point3D(bounds.Max.X + Right, bounds.Max.Y + Front, bounds.Max.Z + Above));
        }
    }

    public enum ClearanceType
    {
        Access,         // User access clearance
        Maintenance,    // Maintenance access
        Safety,         // Fire/safety clearance
        Circulation,    // Passage clearance
        Wheelchair,     // ADA wheelchair clearance
        Operation,      // Operational clearance (doors, equipment)
        Service         // Service/utility clearance
    }

    #endregion

    #region Collision Detection

    /// <summary>
    /// Result of collision detection between elements.
    /// </summary>
    public class CollisionResult
    {
        public SpatialElement Element1 { get; set; }
        public SpatialElement Element2 { get; set; }
        public CollisionType Type { get; set; }
        public CollisionSeverity Severity { get; set; }
        public BoundingBox3D IntersectionVolume { get; set; }
        public double PenetrationDepth { get; set; }
        public Point3D CollisionPoint { get; set; }
        public string Description { get; set; }
        public List<string> ResolutionOptions { get; set; }

        public CollisionResult()
        {
            ResolutionOptions = new List<string>();
        }
    }

    public enum CollisionType
    {
        Hard,           // Physical intersection
        Soft,           // Clearance violation
        Clearance,      // Required clearance not met
        Interference,   // System interference (MEP clash)
        Duplicate       // Duplicate elements
    }

    public enum CollisionSeverity
    {
        Critical,       // Must fix before construction
        Major,          // Should fix, affects functionality
        Minor,          // Should fix, affects quality
        Warning         // May not need fixing
    }

    /// <summary>
    /// Detects collisions between BIM elements.
    /// </summary>
    public class CollisionDetector
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpatialIndex _spatialIndex;
        private readonly Dictionary<string, List<string>> _allowedIntersections;

        public CollisionDetector()
        {
            _spatialIndex = new SpatialIndex();
            _allowedIntersections = new Dictionary<string, List<string>>
            {
                // Categories that can legally intersect
                ["Walls"] = new List<string> { "Doors", "Windows", "CurtainPanels", "Ducts", "Pipes", "Conduit" },
                ["Floors"] = new List<string> { "Columns", "Stairs", "Ducts", "Pipes", "Conduit" },
                ["Ceilings"] = new List<string> { "Lighting", "Diffusers", "Sprinklers", "Ducts" },
                ["Roofs"] = new List<string> { "Skylights", "Ducts", "Pipes" }
            };
        }

        /// <summary>
        /// Builds the spatial index from elements.
        /// </summary>
        public void BuildIndex(IEnumerable<SpatialElement> elements)
        {
            _spatialIndex.Clear();
            foreach (var element in elements)
            {
                _spatialIndex.Insert(element);
            }
            Logger.Info($"Built spatial index with {_spatialIndex.Count} elements");
        }

        /// <summary>
        /// Detects all collisions in the model.
        /// </summary>
        public async Task<List<CollisionResult>> DetectAllCollisionsAsync(
            IEnumerable<SpatialElement> elements,
            CollisionSettings settings = null,
            IProgress<CollisionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            settings = settings ?? new CollisionSettings();
            var collisions = new List<CollisionResult>();
            var elementList = elements.ToList();

            BuildIndex(elementList);

            var total = elementList.Count;
            var processed = 0;

            foreach (var element in elementList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find potential collisions using spatial index
                var candidates = _spatialIndex.Query(element.Bounds.Expand(settings.SearchRadius));

                foreach (var candidate in candidates)
                {
                    if (candidate.ElementId == element.ElementId) continue;
                    if (IsCollisionChecked(element.ElementId, candidate.ElementId, collisions)) continue;

                    var collision = CheckCollision(element, candidate, settings);
                    if (collision != null)
                    {
                        collisions.Add(collision);
                    }
                }

                processed++;
                progress?.Report(new CollisionProgress
                {
                    ProcessedElements = processed,
                    TotalElements = total,
                    CollisionsFound = collisions.Count
                });
            }

            Logger.Info($"Detected {collisions.Count} collisions in {total} elements");
            return collisions;
        }

        /// <summary>
        /// Checks collision between two specific elements.
        /// </summary>
        public CollisionResult CheckCollision(SpatialElement element1, SpatialElement element2, CollisionSettings settings)
        {
            // Skip if intersection is allowed
            if (IsIntersectionAllowed(element1.Category, element2.Category))
            {
                return null;
            }

            // Check bounding box intersection
            if (!element1.Bounds.Intersects(element2.Bounds))
            {
                return null;
            }

            var intersection = element1.Bounds.Intersection(element2.Bounds);
            if (intersection == null) return null;

            // Determine collision type and severity
            var penetration = Math.Min(Math.Min(intersection.Width, intersection.Depth), intersection.Height);

            // Skip if penetration is below tolerance
            if (penetration < settings.Tolerance)
            {
                return null;
            }

            var collision = new CollisionResult
            {
                Element1 = element1,
                Element2 = element2,
                IntersectionVolume = intersection,
                PenetrationDepth = penetration,
                CollisionPoint = intersection.Center
            };

            // Classify collision
            ClassifyCollision(collision, settings);

            // Generate resolution options
            GenerateResolutionOptions(collision);

            return collision;
        }

        /// <summary>
        /// Checks clearance violations for an element.
        /// </summary>
        public List<CollisionResult> CheckClearances(SpatialElement element, IEnumerable<SpatialElement> neighbors)
        {
            var violations = new List<CollisionResult>();

            foreach (var clearance in element.RequiredClearances)
            {
                var clearanceBounds = clearance.GetBounds(element);
                if (clearanceBounds == null) continue;

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.ElementId == element.ElementId) continue;

                    if (clearanceBounds.Intersects(neighbor.Bounds))
                    {
                        var intersection = clearanceBounds.Intersection(neighbor.Bounds);
                        violations.Add(new CollisionResult
                        {
                            Element1 = element,
                            Element2 = neighbor,
                            Type = CollisionType.Clearance,
                            Severity = clearance.IsRequired ? CollisionSeverity.Major : CollisionSeverity.Warning,
                            IntersectionVolume = intersection,
                            Description = $"Clearance violation: {clearance.Name} ({clearance.Standard})"
                        });
                    }
                }
            }

            return violations;
        }

        private bool IsCollisionChecked(string id1, string id2, List<CollisionResult> existing)
        {
            return existing.Any(c =>
                (c.Element1.ElementId == id1 && c.Element2.ElementId == id2) ||
                (c.Element1.ElementId == id2 && c.Element2.ElementId == id1));
        }

        private bool IsIntersectionAllowed(string category1, string category2)
        {
            if (_allowedIntersections.TryGetValue(category1, out var allowed1) && allowed1.Contains(category2))
                return true;
            if (_allowedIntersections.TryGetValue(category2, out var allowed2) && allowed2.Contains(category1))
                return true;
            return false;
        }

        private void ClassifyCollision(CollisionResult collision, CollisionSettings settings)
        {
            var cat1 = collision.Element1.Category;
            var cat2 = collision.Element2.Category;

            // Determine type
            if (cat1 == cat2 && collision.PenetrationDepth > collision.Element1.Bounds.Width * 0.8)
            {
                collision.Type = CollisionType.Duplicate;
                collision.Severity = CollisionSeverity.Warning;
            }
            else if (IsMEPCategory(cat1) && IsMEPCategory(cat2))
            {
                collision.Type = CollisionType.Interference;
                collision.Severity = CollisionSeverity.Major;
            }
            else if (IsStructuralCategory(cat1) || IsStructuralCategory(cat2))
            {
                collision.Type = CollisionType.Hard;
                collision.Severity = CollisionSeverity.Critical;
            }
            else
            {
                collision.Type = CollisionType.Hard;
                collision.Severity = collision.PenetrationDepth > settings.MajorThreshold
                    ? CollisionSeverity.Major
                    : CollisionSeverity.Minor;
            }

            collision.Description = $"{collision.Type} collision between {cat1} and {cat2}";
        }

        private void GenerateResolutionOptions(CollisionResult collision)
        {
            var options = collision.ResolutionOptions;

            switch (collision.Type)
            {
                case CollisionType.Duplicate:
                    options.Add($"Delete duplicate element: {collision.Element2.ElementId}");
                    break;

                case CollisionType.Interference:
                    options.Add($"Reroute {collision.Element1.Category} around {collision.Element2.Category}");
                    options.Add($"Reroute {collision.Element2.Category} around {collision.Element1.Category}");
                    options.Add("Add coordination sleeve or penetration");
                    break;

                case CollisionType.Hard:
                    var moveDir = collision.Element2.Center.Subtract(collision.Element1.Center).Normalize();
                    options.Add($"Move {collision.Element2.ElementId} by {collision.PenetrationDepth:F2} in direction {moveDir}");
                    options.Add($"Move {collision.Element1.ElementId} by {collision.PenetrationDepth:F2} in direction {moveDir.Scale(-1)}");
                    break;

                case CollisionType.Clearance:
                    options.Add($"Increase distance between elements by {collision.PenetrationDepth:F2}");
                    options.Add("Review clearance requirements");
                    break;
            }
        }

        private bool IsMEPCategory(string category)
        {
            return category == "Ducts" || category == "Pipes" || category == "Conduit" ||
                   category == "CableTray" || category == "MechanicalEquipment" ||
                   category == "PlumbingFixtures" || category == "ElectricalEquipment";
        }

        private bool IsStructuralCategory(string category)
        {
            return category == "Columns" || category == "Beams" || category == "Foundations" ||
                   category == "StructuralFraming" || category == "StructuralColumns";
        }
    }

    /// <summary>
    /// Settings for collision detection.
    /// </summary>
    public class CollisionSettings
    {
        public double Tolerance { get; set; } = 0.001; // 1mm
        public double SearchRadius { get; set; } = 1.0; // 1m
        public double MajorThreshold { get; set; } = 0.05; // 50mm
        public bool IncludeClearances { get; set; } = true;
        public bool IncludeMEPInterference { get; set; } = true;
        public HashSet<string> ExcludeCategories { get; set; } = new HashSet<string>();
    }

    public class CollisionProgress
    {
        public int ProcessedElements { get; set; }
        public int TotalElements { get; set; }
        public int CollisionsFound { get; set; }
        public float PercentComplete => TotalElements > 0 ? (float)ProcessedElements / TotalElements : 0;
    }

    #endregion

    #region Spatial Index

    /// <summary>
    /// Spatial index for efficient 3D queries using octree subdivision.
    /// </summary>
    public class SpatialIndex
    {
        private OctreeNode _root;
        private readonly Dictionary<string, SpatialElement> _elements;
        private BoundingBox3D _bounds;

        public int Count => _elements.Count;

        public SpatialIndex()
        {
            _elements = new Dictionary<string, SpatialElement>();
        }

        public void Clear()
        {
            _elements.Clear();
            _root = null;
            _bounds = null;
        }

        public void Insert(SpatialElement element)
        {
            if (element.Bounds == null) return;

            _elements[element.ElementId] = element;

            // Expand bounds
            if (_bounds == null)
            {
                _bounds = new BoundingBox3D(element.Bounds.Min, element.Bounds.Max);
            }
            else
            {
                _bounds = _bounds.Union(element.Bounds);
            }

            // Rebuild octree (in production, use incremental insert)
            RebuildOctree();
        }

        public IEnumerable<SpatialElement> Query(BoundingBox3D searchBounds)
        {
            if (_root == null) return Enumerable.Empty<SpatialElement>();
            return _root.Query(searchBounds);
        }

        public IEnumerable<SpatialElement> QueryRadius(Point3D center, double radius)
        {
            var searchBounds = new BoundingBox3D(
                new Point3D(center.X - radius, center.Y - radius, center.Z - radius),
                new Point3D(center.X + radius, center.Y + radius, center.Z + radius));

            return Query(searchBounds)
                .Where(e => e.Center.DistanceTo(center) <= radius);
        }

        public SpatialElement FindNearest(Point3D point, string category = null)
        {
            var candidates = _elements.Values.AsEnumerable();
            if (category != null)
            {
                candidates = candidates.Where(e => e.Category == category);
            }

            return candidates
                .OrderBy(e => e.Center.DistanceTo(point))
                .FirstOrDefault();
        }

        private void RebuildOctree()
        {
            if (_bounds == null || _elements.Count == 0) return;

            // Expand bounds slightly for safety
            var expandedBounds = _bounds.Expand(1.0);
            _root = new OctreeNode(expandedBounds, 0);

            foreach (var element in _elements.Values)
            {
                _root.Insert(element);
            }
        }

        private class OctreeNode
        {
            private const int MaxDepth = 8;
            private const int MaxElementsPerNode = 10;

            private readonly BoundingBox3D _bounds;
            private readonly int _depth;
            private readonly List<SpatialElement> _elements;
            private OctreeNode[] _children;

            public OctreeNode(BoundingBox3D bounds, int depth)
            {
                _bounds = bounds;
                _depth = depth;
                _elements = new List<SpatialElement>();
            }

            public void Insert(SpatialElement element)
            {
                if (!_bounds.Intersects(element.Bounds)) return;

                if (_children != null)
                {
                    // Insert into children
                    foreach (var child in _children)
                    {
                        child.Insert(element);
                    }
                }
                else if (_depth < MaxDepth && _elements.Count >= MaxElementsPerNode)
                {
                    // Subdivide
                    Subdivide();
                    foreach (var existing in _elements)
                    {
                        foreach (var child in _children)
                        {
                            child.Insert(existing);
                        }
                    }
                    _elements.Clear();
                    foreach (var child in _children)
                    {
                        child.Insert(element);
                    }
                }
                else
                {
                    _elements.Add(element);
                }
            }

            public IEnumerable<SpatialElement> Query(BoundingBox3D searchBounds)
            {
                if (!_bounds.Intersects(searchBounds))
                {
                    return Enumerable.Empty<SpatialElement>();
                }

                if (_children != null)
                {
                    return _children.SelectMany(c => c.Query(searchBounds)).Distinct();
                }

                return _elements.Where(e => e.Bounds.Intersects(searchBounds));
            }

            private void Subdivide()
            {
                var center = _bounds.Center;
                _children = new OctreeNode[8];

                for (int i = 0; i < 8; i++)
                {
                    var min = new Point3D(
                        (i & 1) == 0 ? _bounds.Min.X : center.X,
                        (i & 2) == 0 ? _bounds.Min.Y : center.Y,
                        (i & 4) == 0 ? _bounds.Min.Z : center.Z);
                    var max = new Point3D(
                        (i & 1) == 0 ? center.X : _bounds.Max.X,
                        (i & 2) == 0 ? center.Y : _bounds.Max.Y,
                        (i & 4) == 0 ? center.Z : _bounds.Max.Z);
                    _children[i] = new OctreeNode(new BoundingBox3D(min, max), _depth + 1);
                }
            }
        }
    }

    #endregion

    #region View Analysis

    /// <summary>
    /// Analyzes views, sightlines, and visibility in the model.
    /// </summary>
    public class ViewAnalyzer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpatialIndex _spatialIndex;

        public ViewAnalyzer(SpatialIndex spatialIndex)
        {
            _spatialIndex = spatialIndex;
        }

        /// <summary>
        /// Checks if there's a clear line of sight between two points.
        /// </summary>
        public bool HasLineOfSight(Point3D from, Point3D to, IEnumerable<SpatialElement> obstacles)
        {
            var diff = to.Subtract(from);
            var direction = new Vector3D(diff.X, diff.Y, diff.Z).Normalize();
            var ray = new Ray3D(from, direction);
            var maxDistance = from.DistanceTo(to);

            foreach (var obstacle in obstacles)
            {
                if (RayIntersectsBox(ray, obstacle.Bounds, out var hitDistance))
                {
                    if (hitDistance < maxDistance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Analyzes the view from a specific point.
        /// </summary>
        public ViewAnalysisResult AnalyzeView(Point3D viewPoint, Vector3D viewDirection, double fov, double maxDistance)
        {
            var result = new ViewAnalysisResult
            {
                ViewPoint = viewPoint,
                ViewDirection = viewDirection,
                FieldOfView = fov,
                MaxDistance = maxDistance
            };

            // Query elements in view cone
            var searchBounds = CreateViewConeBounds(viewPoint, viewDirection, fov, maxDistance);
            var candidates = _spatialIndex.Query(searchBounds);

            foreach (var element in candidates)
            {
                var toElementPt = element.Center.Subtract(viewPoint);
                var toElementVec = new Vector3D(toElementPt.X, toElementPt.Y, toElementPt.Z);
                var distance = toElementVec.Length;

                if (distance > maxDistance) continue;

                // Check if element is within field of view
                var angle = Math.Acos(toElementVec.Normalize().Dot(viewDirection)) * 180 / Math.PI;
                if (angle > fov / 2) continue;

                result.VisibleElements.Add(new VisibleElement
                {
                    Element = element,
                    Distance = distance,
                    AngleFromCenter = angle
                });
            }

            // Sort by distance
            result.VisibleElements = result.VisibleElements.OrderBy(v => v.Distance).ToList();

            return result;
        }

        /// <summary>
        /// Finds window views and their quality.
        /// </summary>
        public List<WindowViewResult> AnalyzeWindowViews(IEnumerable<SpatialElement> windows, IEnumerable<SpatialElement> allElements)
        {
            var results = new List<WindowViewResult>();
            var obstacles = allElements.Where(e => e.Category != "Windows").ToList();

            foreach (var window in windows.Where(e => e.Category == "Windows"))
            {
                var viewResult = new WindowViewResult
                {
                    Window = window,
                    ViewDirection = window.FacingDirection
                };

                // Cast rays through window to assess view quality
                var center = window.Center;
                var viewDistance = 50.0; // 50m view distance

                // Sample multiple rays
                var rayCount = 9;
                var obstructedRays = 0;
                var totalSkyVisible = 0.0;

                for (int i = 0; i < rayCount; i++)
                {
                    // Spread rays across window surface
                    var offset = new Vector3D(
                        (i % 3 - 1) * window.Bounds.Width * 0.3,
                        0,
                        (i / 3 - 1) * window.Bounds.Height * 0.3);

                    var rayStart = center.Add(offset.ToPoint3D());
                    var rayEnd = rayStart.Add(window.FacingDirection.Scale(viewDistance).ToPoint3D());

                    if (!HasLineOfSight(rayStart, rayEnd, obstacles))
                    {
                        obstructedRays++;
                    }

                    // Check sky visibility (rays going upward)
                    var skyRay = rayStart.Add(new Point3D(
                        window.FacingDirection.X * 10,
                        window.FacingDirection.Y * 10,
                        10));
                    if (HasLineOfSight(rayStart, skyRay, obstacles))
                    {
                        totalSkyVisible++;
                    }
                }

                viewResult.ObstructionPercentage = (double)obstructedRays / rayCount * 100;
                viewResult.SkyVisibilityPercentage = totalSkyVisible / rayCount * 100;
                viewResult.ViewQuality = CalculateViewQuality(viewResult);

                results.Add(viewResult);
            }

            return results;
        }

        private BoundingBox3D CreateViewConeBounds(Point3D origin, Vector3D direction, double fov, double distance)
        {
            // Simplified: create a box that encompasses the view cone
            var halfAngle = fov / 2 * Math.PI / 180;
            var spread = distance * Math.Tan(halfAngle);

            var endPoint = origin.Add(direction.Scale(distance).ToPoint3D());

            return new BoundingBox3D(
                new Point3D(
                    Math.Min(origin.X, endPoint.X) - spread,
                    Math.Min(origin.Y, endPoint.Y) - spread,
                    Math.Min(origin.Z, endPoint.Z) - spread),
                new Point3D(
                    Math.Max(origin.X, endPoint.X) + spread,
                    Math.Max(origin.Y, endPoint.Y) + spread,
                    Math.Max(origin.Z, endPoint.Z) + spread));
        }

        private bool RayIntersectsBox(Ray3D ray, BoundingBox3D box, out double hitDistance)
        {
            hitDistance = double.MaxValue;

            var tMin = (box.Min.X - ray.Origin.X) / ray.Direction.X;
            var tMax = (box.Max.X - ray.Origin.X) / ray.Direction.X;
            if (tMin > tMax) Swap(ref tMin, ref tMax);

            var tyMin = (box.Min.Y - ray.Origin.Y) / ray.Direction.Y;
            var tyMax = (box.Max.Y - ray.Origin.Y) / ray.Direction.Y;
            if (tyMin > tyMax) Swap(ref tyMin, ref tyMax);

            if (tMin > tyMax || tyMin > tMax) return false;

            tMin = Math.Max(tMin, tyMin);
            tMax = Math.Min(tMax, tyMax);

            var tzMin = (box.Min.Z - ray.Origin.Z) / ray.Direction.Z;
            var tzMax = (box.Max.Z - ray.Origin.Z) / ray.Direction.Z;
            if (tzMin > tzMax) Swap(ref tzMin, ref tzMax);

            if (tMin > tzMax || tzMin > tMax) return false;

            hitDistance = Math.Max(tMin, tzMin);
            return hitDistance >= 0;
        }

        private void Swap(ref double a, ref double b)
        {
            var temp = a;
            a = b;
            b = temp;
        }

        private ViewQuality CalculateViewQuality(WindowViewResult result)
        {
            if (result.ObstructionPercentage < 20 && result.SkyVisibilityPercentage > 60)
                return ViewQuality.Excellent;
            if (result.ObstructionPercentage < 40 && result.SkyVisibilityPercentage > 40)
                return ViewQuality.Good;
            if (result.ObstructionPercentage < 60 && result.SkyVisibilityPercentage > 20)
                return ViewQuality.Fair;
            if (result.ObstructionPercentage < 80)
                return ViewQuality.Poor;
            return ViewQuality.Obstructed;
        }
    }

    public class Ray3D
    {
        public Point3D Origin { get; set; }
        public Vector3D Direction { get; set; }

        public Ray3D(Point3D origin, Vector3D direction)
        {
            Origin = origin;
            Direction = direction.Normalize();
        }

        public Point3D GetPoint(double t) => Origin.Add(Direction.Scale(t).ToPoint3D());
    }

    public class ViewAnalysisResult
    {
        public Point3D ViewPoint { get; set; }
        public Vector3D ViewDirection { get; set; }
        public double FieldOfView { get; set; }
        public double MaxDistance { get; set; }
        public List<VisibleElement> VisibleElements { get; set; } = new List<VisibleElement>();
    }

    public class VisibleElement
    {
        public SpatialElement Element { get; set; }
        public double Distance { get; set; }
        public double AngleFromCenter { get; set; }
    }

    public class WindowViewResult
    {
        public SpatialElement Window { get; set; }
        public Vector3D ViewDirection { get; set; }
        public double ObstructionPercentage { get; set; }
        public double SkyVisibilityPercentage { get; set; }
        public ViewQuality ViewQuality { get; set; }
    }

    public enum ViewQuality
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Obstructed
    }

    #endregion

    #region Orientation Analysis

    /// <summary>
    /// Analyzes element orientations and solar exposure.
    /// </summary>
    public class OrientationAnalyzer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Cardinal direction vectors (Y = North in Revit convention)
        private static readonly Vector3D North = new Vector3D(0, 1, 0);
        private static readonly Vector3D South = new Vector3D(0, -1, 0);
        private static readonly Vector3D East = new Vector3D(1, 0, 0);
        private static readonly Vector3D West = new Vector3D(-1, 0, 0);

        /// <summary>
        /// Gets the cardinal direction an element faces.
        /// </summary>
        public CardinalDirection GetFacingDirection(SpatialElement element)
        {
            var facing = element.FacingDirection;
            if (facing.Length < 0.01) return CardinalDirection.Unknown;

            var normalized = new Vector3D(facing.X, facing.Y, 0).Normalize();

            var dotN = normalized.Dot(North);
            var dotE = normalized.Dot(East);

            if (Math.Abs(dotN) > Math.Abs(dotE))
            {
                return dotN > 0 ? CardinalDirection.North : CardinalDirection.South;
            }
            else
            {
                return dotE > 0 ? CardinalDirection.East : CardinalDirection.West;
            }
        }

        /// <summary>
        /// Gets the precise compass bearing (0-360 degrees, 0 = North).
        /// </summary>
        public double GetCompassBearing(SpatialElement element)
        {
            var facing = new Vector3D(element.FacingDirection.X, element.FacingDirection.Y, 0).Normalize();
            var angle = Math.Atan2(facing.X, facing.Y) * 180 / Math.PI;
            return (angle + 360) % 360;
        }

        /// <summary>
        /// Analyzes solar exposure for an element.
        /// </summary>
        public SolarExposureResult AnalyzeSolarExposure(
            SpatialElement element,
            double latitude,
            int dayOfYear = 172) // Default to summer solstice
        {
            var direction = GetFacingDirection(element);
            var bearing = GetCompassBearing(element);

            var result = new SolarExposureResult
            {
                Element = element,
                FacingDirection = direction,
                Bearing = bearing
            };

            // Calculate sun angles for the location
            var declination = 23.45 * Math.Sin(2 * Math.PI * (284 + dayOfYear) / 365);
            var solarNoon = 90 - latitude + declination;

            // Estimate solar exposure by orientation
            switch (direction)
            {
                case CardinalDirection.South:
                    result.MorningSunHours = 2;
                    result.MiddaySunHours = 6;
                    result.AfternoonSunHours = 2;
                    result.HeatGainFactor = 1.0;
                    break;
                case CardinalDirection.East:
                    result.MorningSunHours = 5;
                    result.MiddaySunHours = 1;
                    result.AfternoonSunHours = 0;
                    result.HeatGainFactor = 0.7;
                    break;
                case CardinalDirection.West:
                    result.MorningSunHours = 0;
                    result.MiddaySunHours = 1;
                    result.AfternoonSunHours = 5;
                    result.HeatGainFactor = 0.8; // Afternoon sun is hotter
                    break;
                case CardinalDirection.North:
                    result.MorningSunHours = 1;
                    result.MiddaySunHours = 0;
                    result.AfternoonSunHours = 1;
                    result.HeatGainFactor = 0.3;
                    break;
            }

            // Adjust for latitude (tropical regions have different patterns)
            if (Math.Abs(latitude) < 23.5)
            {
                result.HeatGainFactor *= 1.2; // More direct sun in tropics
            }

            result.TotalSunHours = result.MorningSunHours + result.MiddaySunHours + result.AfternoonSunHours;

            return result;
        }

        /// <summary>
        /// Recommends optimal orientation for a room type.
        /// </summary>
        public OrientationRecommendation GetOptimalOrientation(
            string roomType,
            double latitude,
            string climate)
        {
            var recommendation = new OrientationRecommendation
            {
                RoomType = roomType,
                Climate = climate
            };

            // General rules (can be expanded with knowledge graph integration)
            switch (roomType.ToLower())
            {
                case "living room":
                case "lounge":
                    recommendation.PreferredDirection = climate == "hot" ? CardinalDirection.North : CardinalDirection.South;
                    recommendation.Reason = climate == "hot"
                        ? "North-facing reduces heat gain in hot climates"
                        : "South-facing maximizes natural light and passive solar heating";
                    break;

                case "bedroom":
                    recommendation.PreferredDirection = CardinalDirection.East;
                    recommendation.Reason = "East-facing provides morning light and stays cool in evenings";
                    break;

                case "kitchen":
                    recommendation.PreferredDirection = CardinalDirection.North;
                    recommendation.Reason = "North-facing reduces heat buildup from cooking combined with solar gain";
                    break;

                case "bathroom":
                    recommendation.PreferredDirection = CardinalDirection.East;
                    recommendation.Reason = "Morning sun helps with moisture control";
                    break;

                case "office":
                case "study":
                    recommendation.PreferredDirection = CardinalDirection.North;
                    recommendation.Reason = "North light is consistent and reduces glare on screens";
                    break;

                default:
                    recommendation.PreferredDirection = CardinalDirection.South;
                    recommendation.Reason = "South orientation balances light and thermal performance";
                    break;
            }

            // Flip for southern hemisphere
            if (latitude < 0)
            {
                recommendation.PreferredDirection = FlipNorthSouth(recommendation.PreferredDirection);
            }

            return recommendation;
        }

        private CardinalDirection FlipNorthSouth(CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.North => CardinalDirection.South,
                CardinalDirection.South => CardinalDirection.North,
                _ => direction
            };
        }
    }

    public enum CardinalDirection
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest,
        Unknown
    }

    public class SolarExposureResult
    {
        public SpatialElement Element { get; set; }
        public CardinalDirection FacingDirection { get; set; }
        public double Bearing { get; set; }
        public double MorningSunHours { get; set; }
        public double MiddaySunHours { get; set; }
        public double AfternoonSunHours { get; set; }
        public double TotalSunHours { get; set; }
        public double HeatGainFactor { get; set; }
    }

    public class OrientationRecommendation
    {
        public string RoomType { get; set; }
        public string Climate { get; set; }
        public CardinalDirection PreferredDirection { get; set; }
        public string Reason { get; set; }
        public List<CardinalDirection> AcceptableAlternatives { get; set; } = new List<CardinalDirection>();
    }

    #endregion

    #region Main Analyzer

    /// <summary>
    /// Main 3D spatial analyzer combining all analysis capabilities.
    /// </summary>
    public class SpatialAnalyzer3D
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public CollisionDetector CollisionDetector { get; }
        public SpatialIndex SpatialIndex { get; }
        public ViewAnalyzer ViewAnalyzer { get; }
        public OrientationAnalyzer OrientationAnalyzer { get; }

        public SpatialAnalyzer3D()
        {
            SpatialIndex = new SpatialIndex();
            CollisionDetector = new CollisionDetector();
            ViewAnalyzer = new ViewAnalyzer(SpatialIndex);
            OrientationAnalyzer = new OrientationAnalyzer();
        }

        /// <summary>
        /// Performs comprehensive spatial analysis on a set of elements.
        /// </summary>
        public async Task<SpatialAnalysisResult> AnalyzeAsync(
            IEnumerable<SpatialElement> elements,
            SpatialAnalysisSettings settings = null,
            IProgress<SpatialAnalysisProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            settings = settings ?? new SpatialAnalysisSettings();
            var result = new SpatialAnalysisResult();
            var elementList = elements.ToList();

            Logger.Info($"Starting spatial analysis of {elementList.Count} elements");

            // Build spatial index
            foreach (var element in elementList)
            {
                SpatialIndex.Insert(element);
            }

            // 1. Collision Detection
            if (settings.IncludeCollisions)
            {
                result.Collisions = await CollisionDetector.DetectAllCollisionsAsync(
                    elementList,
                    settings.CollisionSettings,
                    new Progress<CollisionProgress>(p => progress?.Report(new SpatialAnalysisProgress
                    {
                        Phase = "Collision Detection",
                        Progress = p.PercentComplete
                    })),
                    cancellationToken);
            }

            // 2. Clearance Analysis
            if (settings.IncludeClearances)
            {
                foreach (var element in elementList.Where(e => e.RequiredClearances.Any()))
                {
                    var neighbors = SpatialIndex.QueryRadius(element.Center, 5.0);
                    var violations = CollisionDetector.CheckClearances(element, neighbors);
                    result.ClearanceViolations.AddRange(violations);
                }
            }

            // 3. View Analysis
            if (settings.IncludeViews)
            {
                var windows = elementList.Where(e => e.Category == "Windows");
                result.WindowViews = ViewAnalyzer.AnalyzeWindowViews(windows, elementList);
            }

            // 4. Orientation Analysis
            if (settings.IncludeOrientation)
            {
                foreach (var element in elementList.Where(e => e.Category == "Rooms" || e.Category == "Windows"))
                {
                    var exposure = OrientationAnalyzer.AnalyzeSolarExposure(element, settings.Latitude, settings.DayOfYear);
                    result.SolarExposures.Add(exposure);
                }
            }

            // Calculate statistics
            result.TotalElements = elementList.Count;
            result.CollisionCount = result.Collisions.Count;
            result.ClearanceViolationCount = result.ClearanceViolations.Count;
            result.CriticalIssueCount = result.Collisions.Count(c => c.Severity == CollisionSeverity.Critical);

            Logger.Info($"Spatial analysis complete: {result.CollisionCount} collisions, {result.ClearanceViolationCount} clearance violations");

            return result;
        }
    }

    /// <summary>
    /// Settings for spatial analysis.
    /// </summary>
    public class SpatialAnalysisSettings
    {
        public bool IncludeCollisions { get; set; } = true;
        public bool IncludeClearances { get; set; } = true;
        public bool IncludeViews { get; set; } = true;
        public bool IncludeOrientation { get; set; } = true;
        public double Latitude { get; set; } = 0; // Equator by default
        public int DayOfYear { get; set; } = 172; // Summer solstice
        public CollisionSettings CollisionSettings { get; set; } = new CollisionSettings();
    }

    /// <summary>
    /// Result of comprehensive spatial analysis.
    /// </summary>
    public class SpatialAnalysisResult
    {
        public int TotalElements { get; set; }
        public int CollisionCount { get; set; }
        public int ClearanceViolationCount { get; set; }
        public int CriticalIssueCount { get; set; }

        public List<CollisionResult> Collisions { get; set; } = new List<CollisionResult>();
        public List<CollisionResult> ClearanceViolations { get; set; } = new List<CollisionResult>();
        public List<WindowViewResult> WindowViews { get; set; } = new List<WindowViewResult>();
        public List<SolarExposureResult> SolarExposures { get; set; } = new List<SolarExposureResult>();

        public bool HasCriticalIssues => CriticalIssueCount > 0;
    }

    public class SpatialAnalysisProgress
    {
        public string Phase { get; set; }
        public float Progress { get; set; }
    }

    #endregion
}
