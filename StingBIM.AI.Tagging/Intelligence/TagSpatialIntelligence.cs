// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagSpatialIntelligence.cs - 3D spatial reasoning engine for intelligent tag placement
// Provides deep spatial analysis: context, density, visibility, relationships, topology
//
// Spatial Intelligence Capabilities:
//   1. 3D Spatial Context     - Level, room, zone, building section classification
//   2. View-Space Analysis    - 2D projection, safe zones, danger zones
//   3. Relationship Mapping   - Hosting, adjacency, containment, MEP connectivity
//   4. Density Analysis       - Local density, heat maps, gradient detection
//   5. Visibility Analysis    - Occlusion, partial visibility, phase/workset filtering
//   6. Optimal Placement Zones - Whitespace detection, scored region identification
//   7. Spatial Clustering     - Natural grouping with boundary detection
//   8. Building Topology      - Circulation, core/perimeter, fire compartments
//   9. Spatial Metrics        - Nearest-neighbor, autocorrelation, Voronoi

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Models;
using StingBIM.AI.Core.Common;
using Point2D = StingBIM.AI.Tagging.Models.Point2D;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Supporting Types

    public sealed class BoundingBox3D
    {
        public Point3D Min { get; set; } = new();
        public Point3D Max { get; set; } = new();
        public Point3D Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);
        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public double Depth => Max.Z - Min.Z;
        public double Volume => Width * Height * Depth;

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
    }

    public sealed class SpatialContext3D
    {
        public string ElementId { get; set; }
        public Point3D Position { get; set; }
        public BoundingBox3D BoundingBox { get; set; }
        public string LevelName { get; set; }
        public double LevelElevation { get; set; }
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public string ZoneClassification { get; set; } // Public, Private, Service, Circulation
        public bool IsExterior { get; set; }
        public string BuildingWing { get; set; }
        public string StructuralBay { get; set; }
        public List<string> NearbyElementIds { get; set; } = new();
        public double LocalDensity { get; set; }
    }

    public sealed class ViewSpaceAnalysis
    {
        public string ViewId { get; set; }
        public string ViewType { get; set; }
        public double ViewScale { get; set; }
        public double ViewWidth { get; set; }
        public double ViewHeight { get; set; }
        public Point2D ViewCenter { get; set; }
        public List<PlacementZone> SafeZones { get; set; } = new();
        public List<PlacementZone> DangerZones { get; set; } = new();
        public double[,] DensityGrid { get; set; }
        public int GridResolutionX { get; set; }
        public int GridResolutionY { get; set; }
    }

    public sealed class PlacementZone
    {
        public Point2D TopLeft { get; set; }
        public Point2D BottomRight { get; set; }
        public double Score { get; set; }
        public string ZoneType { get; set; } // "Safe", "Danger", "Moderate"
        public double Density { get; set; }
        public bool NearElement { get; set; }
        public bool NearEdge { get; set; }
    }

    public sealed class SpatialRelationship
    {
        public string ElementIdA { get; set; }
        public string ElementIdB { get; set; }
        public string RelationType { get; set; } // "Hosts", "Adjacent", "Contains", "Connected"
        public double Distance { get; set; }
        public double Strength { get; set; } = 1.0;
    }

    public sealed class DensityMap
    {
        public double[,] Values { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double CellSize { get; set; }
        public Point2D Origin { get; set; }
        public double MaxDensity { get; set; }
        public double AverageDensity { get; set; }
        public List<(int X, int Y, double Value)> Peaks { get; set; } = new();
    }

    public sealed class SpatialClusterResult
    {
        public string ClusterId { get; set; }
        public List<string> ElementIds { get; set; } = new();
        public Point2D Centroid { get; set; }
        public string Shape { get; set; } // "Linear", "Circular", "Grid", "Irregular"
        public double Spread { get; set; }
        public string BoundaryType { get; set; } // "Wall", "Grid", "Room", "None"
        public string RepresentativeElementId { get; set; }
    }

    public sealed class BuildingTopologyInfo
    {
        public string ElementId { get; set; }
        public bool IsOnCirculationPath { get; set; }
        public bool IsInCore { get; set; }
        public bool IsPerimeter { get; set; }
        public bool IsServiceSpace { get; set; }
        public bool IsPublicSpace { get; set; }
        public bool IsWetArea { get; set; }
        public string FireCompartment { get; set; }
        public List<string> AccessiblePaths { get; set; } = new();
    }

    public sealed class SpatialMetricsResult
    {
        public double AverageNearestNeighborDistance { get; set; }
        public double NearestNeighborRatio { get; set; } // <1 = clustered, >1 = dispersed
        public double CoverageCompleteness { get; set; } // 0-1
        public double SpatialAutocorrelation { get; set; } // Moran's I approximation
        public int ClusterCount { get; set; }
        public Dictionary<string, double> ZoneCoverage { get; set; } = new();
    }

    #endregion

    #region Main Engine

    /// <summary>
    /// 3D spatial reasoning engine providing deep spatial analysis to inform
    /// optimal tag positioning, clustering, and placement decisions.
    /// </summary>
    public sealed class TagSpatialIntelligence
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Spatial data caches
        private readonly Dictionary<string, SpatialContext3D> _elementContexts = new();
        private readonly Dictionary<string, List<SpatialRelationship>> _relationships = new();
        private readonly Dictionary<string, BuildingTopologyInfo> _topology = new();
        private DensityMap _currentDensityMap;
        private readonly double _defaultGridCellSize;

        public TagSpatialIntelligence(double defaultGridCellSize = 1.0)
        {
            _defaultGridCellSize = defaultGridCellSize;
            Logger.Info("TagSpatialIntelligence initialized, grid cell size={CellSize}",
                defaultGridCellSize);
        }

        #region Spatial Context Analysis

        /// <summary>
        /// Register element spatial context for analysis.
        /// </summary>
        public void RegisterElement(SpatialContext3D context)
        {
            if (context?.ElementId == null) return;
            lock (_lockObject)
            {
                _elementContexts[context.ElementId] = context;
            }
        }

        /// <summary>
        /// Batch register elements.
        /// </summary>
        public void RegisterElements(IEnumerable<SpatialContext3D> contexts)
        {
            lock (_lockObject)
            {
                foreach (var ctx in contexts)
                {
                    if (ctx?.ElementId != null)
                        _elementContexts[ctx.ElementId] = ctx;
                }
                Logger.Debug("Registered {Count} elements for spatial analysis",
                    _elementContexts.Count);
            }
        }

        /// <summary>
        /// Classify zone type based on room name patterns.
        /// </summary>
        public string ClassifyZone(string roomName)
        {
            if (string.IsNullOrEmpty(roomName)) return "Unknown";
            var lower = roomName.ToLowerInvariant();

            if (lower.Contains("corridor") || lower.Contains("hallway") ||
                lower.Contains("stair") || lower.Contains("elevator") ||
                lower.Contains("lobby") || lower.Contains("entrance"))
                return "Circulation";

            if (lower.Contains("mechanical") || lower.Contains("electrical") ||
                lower.Contains("janitor") || lower.Contains("storage") ||
                lower.Contains("server") || lower.Contains("utility"))
                return "Service";

            if (lower.Contains("reception") || lower.Contains("lounge") ||
                lower.Contains("cafe") || lower.Contains("restaurant") ||
                lower.Contains("retail") || lower.Contains("gallery"))
                return "Public";

            if (lower.Contains("office") || lower.Contains("meeting") ||
                lower.Contains("conference") || lower.Contains("board"))
                return "Private";

            if (lower.Contains("toilet") || lower.Contains("bathroom") ||
                lower.Contains("washroom") || lower.Contains("kitchen") ||
                lower.Contains("laundry"))
                return "WetArea";

            return "General";
        }

        /// <summary>
        /// Get spatial context for an element.
        /// </summary>
        public SpatialContext3D GetElementContext(string elementId)
        {
            lock (_lockObject)
            {
                return _elementContexts.GetValueOrDefault(elementId);
            }
        }

        /// <summary>
        /// Find elements near a point within radius.
        /// </summary>
        public List<SpatialContext3D> FindElementsNear(Point3D point, double radius)
        {
            lock (_lockObject)
            {
                return _elementContexts.Values
                    .Where(e => e.Position != null && e.Position.DistanceTo(point) <= radius)
                    .OrderBy(e => e.Position.DistanceTo(point))
                    .ToList();
            }
        }

        /// <summary>
        /// Find elements in the same room.
        /// </summary>
        public List<SpatialContext3D> FindElementsInRoom(string roomNumber)
        {
            lock (_lockObject)
            {
                return _elementContexts.Values
                    .Where(e => string.Equals(e.RoomNumber, roomNumber,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Find elements on the same level.
        /// </summary>
        public List<SpatialContext3D> FindElementsOnLevel(string levelName)
        {
            lock (_lockObject)
            {
                return _elementContexts.Values
                    .Where(e => string.Equals(e.LevelName, levelName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        #endregion

        #region View-Space Analysis

        /// <summary>
        /// Analyze a view's 2D space for tag placement opportunities.
        /// </summary>
        public ViewSpaceAnalysis AnalyzeViewSpace(
            string viewId,
            string viewType,
            double viewScale,
            double viewWidth,
            double viewHeight,
            List<(Point2D Center, TagBounds2D Bounds)> existingElements,
            List<TagBounds2D> existingTags)
        {
            var analysis = new ViewSpaceAnalysis
            {
                ViewId = viewId,
                ViewType = viewType,
                ViewScale = viewScale,
                ViewWidth = viewWidth,
                ViewHeight = viewHeight,
                ViewCenter = new Point2D { X = viewWidth / 2, Y = viewHeight / 2 }
            };

            // Build density grid
            int gridX = Math.Max(10, (int)(viewWidth / _defaultGridCellSize));
            int gridY = Math.Max(10, (int)(viewHeight / _defaultGridCellSize));
            var densityGrid = new double[gridX, gridY];
            double cellW = viewWidth / gridX;
            double cellH = viewHeight / gridY;

            // Accumulate density from elements and existing tags
            foreach (var elem in existingElements)
            {
                int cx = Math.Clamp((int)(elem.Center.X / cellW), 0, gridX - 1);
                int cy = Math.Clamp((int)(elem.Center.Y / cellH), 0, gridY - 1);

                // Spread density in a 3x3 kernel
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx >= 0 && nx < gridX && ny >= 0 && ny < gridY)
                    {
                        double weight = (dx == 0 && dy == 0) ? 1.0 : 0.5;
                        densityGrid[nx, ny] += weight;
                    }
                }
            }

            foreach (var tag in existingTags)
            {
                double tagCX = (tag.MinX + tag.MaxX) / 2;
                double tagCY = (tag.MinY + tag.MaxY) / 2;
                int cx = Math.Clamp((int)(tagCX / cellW), 0, gridX - 1);
                int cy = Math.Clamp((int)(tagCY / cellH), 0, gridY - 1);
                if (cx >= 0 && cx < gridX && cy >= 0 && cy < gridY)
                    densityGrid[cx, cy] += 2.0; // Tags count more for crowding
            }

            analysis.DensityGrid = densityGrid;
            analysis.GridResolutionX = gridX;
            analysis.GridResolutionY = gridY;

            // Identify safe zones (low density) and danger zones (high density)
            double maxDens = 0;
            for (int x = 0; x < gridX; x++)
            for (int y = 0; y < gridY; y++)
                maxDens = Math.Max(maxDens, densityGrid[x, y]);

            for (int x = 0; x < gridX; x++)
            for (int y = 0; y < gridY; y++)
            {
                double normalized = maxDens > 0 ? densityGrid[x, y] / maxDens : 0;

                if (normalized < 0.2)
                {
                    analysis.SafeZones.Add(new PlacementZone
                    {
                        TopLeft = new Point2D { X = x * cellW, Y = y * cellH },
                        BottomRight = new Point2D { X = (x + 1) * cellW, Y = (y + 1) * cellH },
                        Score = 1.0 - normalized,
                        ZoneType = "Safe",
                        Density = densityGrid[x, y],
                        NearEdge = x <= 1 || y <= 1 || x >= gridX - 2 || y >= gridY - 2
                    });
                }
                else if (normalized > 0.7)
                {
                    analysis.DangerZones.Add(new PlacementZone
                    {
                        TopLeft = new Point2D { X = x * cellW, Y = y * cellH },
                        BottomRight = new Point2D { X = (x + 1) * cellW, Y = (y + 1) * cellH },
                        Score = normalized,
                        ZoneType = "Danger",
                        Density = densityGrid[x, y]
                    });
                }
            }

            Logger.Debug("View space analyzed: {SafeZones} safe zones, {DangerZones} danger zones",
                analysis.SafeZones.Count, analysis.DangerZones.Count);

            return analysis;
        }

        /// <summary>
        /// Find the best placement zone near a target element.
        /// </summary>
        public PlacementZone FindBestPlacementZone(
            ViewSpaceAnalysis viewAnalysis,
            Point2D targetElementCenter,
            double maxSearchRadius)
        {
            return viewAnalysis.SafeZones
                .Where(z =>
                {
                    double zCX = (z.TopLeft.X + z.BottomRight.X) / 2;
                    double zCY = (z.TopLeft.Y + z.BottomRight.Y) / 2;
                    double dx = zCX - targetElementCenter.X;
                    double dy = zCY - targetElementCenter.Y;
                    return Math.Sqrt(dx * dx + dy * dy) <= maxSearchRadius;
                })
                .OrderByDescending(z => z.Score)
                .ThenBy(z =>
                {
                    double zCX = (z.TopLeft.X + z.BottomRight.X) / 2;
                    double zCY = (z.TopLeft.Y + z.BottomRight.Y) / 2;
                    double dx = zCX - targetElementCenter.X;
                    double dy = zCY - targetElementCenter.Y;
                    return Math.Sqrt(dx * dx + dy * dy);
                })
                .FirstOrDefault();
        }

        #endregion

        #region Density Analysis

        /// <summary>
        /// Compute a full density map for the current set of elements.
        /// </summary>
        public DensityMap ComputeDensityMap(
            List<Point2D> elementPositions,
            double areaWidth, double areaHeight,
            double cellSize = 0)
        {
            if (cellSize <= 0) cellSize = _defaultGridCellSize;

            int w = Math.Max(5, (int)(areaWidth / cellSize));
            int h = Math.Max(5, (int)(areaHeight / cellSize));
            var grid = new double[w, h];

            foreach (var pos in elementPositions)
            {
                int cx = Math.Clamp((int)(pos.X / cellSize), 0, w - 1);
                int cy = Math.Clamp((int)(pos.Y / cellSize), 0, h - 1);

                // Gaussian spread with sigma = 1.5 cells
                for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                    {
                        double dist2 = dx * dx + dy * dy;
                        double weight = Math.Exp(-dist2 / (2 * 1.5 * 1.5));
                        grid[nx, ny] += weight;
                    }
                }
            }

            // Find stats and peaks
            double maxVal = 0, sum = 0;
            var peaks = new List<(int X, int Y, double Value)>();

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                maxVal = Math.Max(maxVal, grid[x, y]);
                sum += grid[x, y];
            }

            // Peak detection (local maxima)
            for (int x = 1; x < w - 1; x++)
            for (int y = 1; y < h - 1; y++)
            {
                double val = grid[x, y];
                if (val > 0.5 * maxVal &&
                    val >= grid[x - 1, y] && val >= grid[x + 1, y] &&
                    val >= grid[x, y - 1] && val >= grid[x, y + 1])
                {
                    peaks.Add((x, y, val));
                }
            }

            var map = new DensityMap
            {
                Values = grid,
                Width = w,
                Height = h,
                CellSize = cellSize,
                Origin = new Point2D { X = 0, Y = 0 },
                MaxDensity = maxVal,
                AverageDensity = sum / (w * h),
                Peaks = peaks.OrderByDescending(p => p.Value).Take(20).ToList()
            };

            lock (_lockObject) { _currentDensityMap = map; }
            return map;
        }

        /// <summary>
        /// Get density at a specific point.
        /// </summary>
        public double GetDensityAt(Point2D position)
        {
            lock (_lockObject)
            {
                if (_currentDensityMap == null) return 0;
                int x = Math.Clamp((int)(position.X / _currentDensityMap.CellSize),
                    0, _currentDensityMap.Width - 1);
                int y = Math.Clamp((int)(position.Y / _currentDensityMap.CellSize),
                    0, _currentDensityMap.Height - 1);
                return _currentDensityMap.Values[x, y];
            }
        }

        #endregion

        #region Relationship Mapping

        /// <summary>
        /// Register a spatial relationship between two elements.
        /// </summary>
        public void RegisterRelationship(SpatialRelationship relationship)
        {
            lock (_lockObject)
            {
                if (!_relationships.ContainsKey(relationship.ElementIdA))
                    _relationships[relationship.ElementIdA] = new();
                _relationships[relationship.ElementIdA].Add(relationship);

                // Bidirectional for adjacency and connectivity
                if (relationship.RelationType == "Adjacent" ||
                    relationship.RelationType == "Connected")
                {
                    if (!_relationships.ContainsKey(relationship.ElementIdB))
                        _relationships[relationship.ElementIdB] = new();
                    _relationships[relationship.ElementIdB].Add(new SpatialRelationship
                    {
                        ElementIdA = relationship.ElementIdB,
                        ElementIdB = relationship.ElementIdA,
                        RelationType = relationship.RelationType,
                        Distance = relationship.Distance,
                        Strength = relationship.Strength
                    });
                }
            }
        }

        /// <summary>
        /// Get all relationships for an element.
        /// </summary>
        public List<SpatialRelationship> GetRelationships(string elementId,
            string relationTypeFilter = null)
        {
            lock (_lockObject)
            {
                if (!_relationships.TryGetValue(elementId, out var rels))
                    return new List<SpatialRelationship>();

                if (!string.IsNullOrEmpty(relationTypeFilter))
                    return rels.Where(r => r.RelationType == relationTypeFilter).ToList();

                return new List<SpatialRelationship>(rels);
            }
        }

        /// <summary>
        /// Find connected elements along a system chain (e.g., MEP system).
        /// </summary>
        public List<string> TraceSystemChain(string startElementId, int maxDepth = 10)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<(string Id, int Depth)>();
            queue.Enqueue((startElementId, 0));
            var result = new List<string>();

            while (queue.Count > 0)
            {
                var (id, depth) = queue.Dequeue();
                if (visited.Contains(id) || depth > maxDepth) continue;
                visited.Add(id);
                result.Add(id);

                var connected = GetRelationships(id, "Connected");
                foreach (var rel in connected)
                {
                    if (!visited.Contains(rel.ElementIdB))
                        queue.Enqueue((rel.ElementIdB, depth + 1));
                }
            }

            return result;
        }

        #endregion

        #region Spatial Clustering

        /// <summary>
        /// Cluster elements spatially using DBSCAN-style algorithm.
        /// </summary>
        public List<SpatialClusterResult> ClusterElements(
            List<(string ElementId, Point2D Position)> elements,
            double epsilon,
            int minPoints = 2)
        {
            var clusters = new List<SpatialClusterResult>();
            var visited = new HashSet<string>();
            var noise = new HashSet<string>();
            int clusterId = 0;

            foreach (var elem in elements)
            {
                if (visited.Contains(elem.ElementId)) continue;
                visited.Add(elem.ElementId);

                var neighbors = elements
                    .Where(e => e.ElementId != elem.ElementId &&
                        Distance2D(elem.Position, e.Position) <= epsilon)
                    .ToList();

                if (neighbors.Count < minPoints)
                {
                    noise.Add(elem.ElementId);
                    continue;
                }

                clusterId++;
                var cluster = new SpatialClusterResult
                {
                    ClusterId = $"C{clusterId}",
                    ElementIds = new List<string> { elem.ElementId }
                };

                var expandQueue = new Queue<(string ElementId, Point2D Position)>(neighbors);
                while (expandQueue.Count > 0)
                {
                    var next = expandQueue.Dequeue();
                    if (!visited.Contains(next.ElementId))
                    {
                        visited.Add(next.ElementId);
                        var nextNeighbors = elements
                            .Where(e => e.ElementId != next.ElementId &&
                                Distance2D(next.Position, e.Position) <= epsilon)
                            .ToList();
                        if (nextNeighbors.Count >= minPoints)
                        {
                            foreach (var nn in nextNeighbors)
                                expandQueue.Enqueue(nn);
                        }
                    }

                    if (!cluster.ElementIds.Contains(next.ElementId))
                    {
                        cluster.ElementIds.Add(next.ElementId);
                        noise.Remove(next.ElementId);
                    }
                }

                // Compute cluster centroid
                var clusterPositions = elements
                    .Where(e => cluster.ElementIds.Contains(e.ElementId))
                    .Select(e => e.Position).ToList();
                cluster.Centroid = new Point2D
                {
                    X = clusterPositions.Average(p => p.X),
                    Y = clusterPositions.Average(p => p.Y)
                };

                // Classify shape
                cluster.Shape = ClassifyClusterShape(clusterPositions);
                cluster.Spread = ComputeClusterSpread(clusterPositions, cluster.Centroid);

                // Select representative (closest to centroid)
                cluster.RepresentativeElementId = elements
                    .Where(e => cluster.ElementIds.Contains(e.ElementId))
                    .OrderBy(e => Distance2D(e.Position, cluster.Centroid))
                    .First().ElementId;

                clusters.Add(cluster);
            }

            Logger.Debug("Clustering: {Clusters} clusters, {Noise} noise points from {Total} elements",
                clusters.Count, noise.Count, elements.Count);

            return clusters;
        }

        private string ClassifyClusterShape(List<Point2D> positions)
        {
            if (positions.Count < 3) return "Linear";

            // PCA-like analysis: compute covariance and check eigenvalue ratio
            double cx = positions.Average(p => p.X);
            double cy = positions.Average(p => p.Y);

            double cxx = 0, cyy = 0, cxy = 0;
            foreach (var p in positions)
            {
                double dx = p.X - cx, dy = p.Y - cy;
                cxx += dx * dx;
                cyy += dy * dy;
                cxy += dx * dy;
            }
            cxx /= positions.Count;
            cyy /= positions.Count;
            cxy /= positions.Count;

            // Eigenvalues of 2x2 covariance matrix
            double trace = cxx + cyy;
            double det = cxx * cyy - cxy * cxy;
            double disc = Math.Sqrt(Math.Max(0, trace * trace / 4 - det));
            double lambda1 = trace / 2 + disc;
            double lambda2 = trace / 2 - disc;

            double ratio = lambda2 > 0.001 ? lambda1 / lambda2 : 100;

            if (ratio > 5) return "Linear";
            if (ratio < 1.5) return "Circular";

            // Check for grid pattern
            var sortedX = positions.Select(p => p.X).OrderBy(x => x).ToList();
            var diffs = sortedX.Zip(sortedX.Skip(1), (a, b) => b - a).ToList();
            if (diffs.Any())
            {
                double avgDiff = diffs.Average();
                double diffVariance = diffs.Average(d => (d - avgDiff) * (d - avgDiff));
                if (diffVariance / (avgDiff * avgDiff + 0.001) < 0.1)
                    return "Grid";
            }

            return "Irregular";
        }

        private double ComputeClusterSpread(List<Point2D> positions, Point2D centroid)
        {
            return positions.Average(p => Distance2D(p, centroid));
        }

        #endregion

        #region Building Topology

        /// <summary>
        /// Register building topology info for an element.
        /// </summary>
        public void RegisterTopology(BuildingTopologyInfo topology)
        {
            lock (_lockObject)
            {
                _topology[topology.ElementId] = topology;
            }
        }

        /// <summary>
        /// Auto-classify topology based on room name and zone.
        /// </summary>
        public BuildingTopologyInfo ClassifyTopology(string elementId, string roomName,
            string zoneName, bool isExterior)
        {
            var zone = ClassifyZone(roomName);
            var topo = new BuildingTopologyInfo
            {
                ElementId = elementId,
                IsOnCirculationPath = zone == "Circulation",
                IsServiceSpace = zone == "Service",
                IsPublicSpace = zone == "Public",
                IsWetArea = zone == "WetArea",
                IsPerimeter = isExterior,
                IsInCore = !isExterior && (zone == "Service" || zone == "Circulation")
            };

            lock (_lockObject) { _topology[elementId] = topo; }
            return topo;
        }

        public BuildingTopologyInfo GetTopology(string elementId)
        {
            lock (_lockObject) { return _topology.GetValueOrDefault(elementId); }
        }

        #endregion

        #region Spatial Metrics

        /// <summary>
        /// Compute comprehensive spatial metrics for tagged elements.
        /// </summary>
        public SpatialMetricsResult ComputeMetrics(
            List<(string ElementId, Point2D Position, bool IsTagged)> elements)
        {
            var result = new SpatialMetricsResult();

            if (!elements.Any()) return result;

            var tagged = elements.Where(e => e.IsTagged).ToList();
            var all = elements.ToList();

            // Coverage completeness
            result.CoverageCompleteness = all.Count > 0
                ? (double)tagged.Count / all.Count : 0;

            // Average nearest-neighbor distance for tagged elements
            if (tagged.Count >= 2)
            {
                var nnDistances = new List<double>();
                foreach (var t in tagged)
                {
                    double minDist = tagged
                        .Where(o => o.ElementId != t.ElementId)
                        .Min(o => Distance2D(t.Position, o.Position));
                    nnDistances.Add(minDist);
                }
                result.AverageNearestNeighborDistance = nnDistances.Average();

                // Expected NN distance for random distribution in area
                double minX = all.Min(e => e.Position.X);
                double maxX = all.Max(e => e.Position.X);
                double minY = all.Min(e => e.Position.Y);
                double maxY = all.Max(e => e.Position.Y);
                double area = (maxX - minX) * (maxY - minY);
                double density = tagged.Count / Math.Max(area, 1);
                double expectedNN = 0.5 / Math.Sqrt(Math.Max(density, 0.001));
                result.NearestNeighborRatio = result.AverageNearestNeighborDistance / expectedNN;
            }

            // Spatial autocorrelation (simplified Moran's I)
            if (tagged.Count >= 3)
            {
                double mean = 0.5; // Binary tagged/not-tagged
                double numerator = 0, denominator = 0, weightSum = 0;
                foreach (var i in all)
                {
                    double xi = i.IsTagged ? 1 : 0;
                    denominator += (xi - mean) * (xi - mean);

                    foreach (var j in all)
                    {
                        if (i.ElementId == j.ElementId) continue;
                        double dist = Distance2D(i.Position, j.Position);
                        if (dist < 5.0) // Only consider nearby elements
                        {
                            double weight = 1.0 / (1.0 + dist);
                            double xj = j.IsTagged ? 1 : 0;
                            numerator += weight * (xi - mean) * (xj - mean);
                            weightSum += weight;
                        }
                    }
                }
                result.SpatialAutocorrelation = weightSum > 0 && denominator > 0
                    ? (all.Count * numerator) / (weightSum * denominator) : 0;
            }

            // Zone coverage
            lock (_lockObject)
            {
                var zones = _elementContexts.Values
                    .Where(e => !string.IsNullOrEmpty(e.ZoneClassification))
                    .GroupBy(e => e.ZoneClassification);

                foreach (var zone in zones)
                {
                    int zoneTotal = zone.Count();
                    int zoneTagged = zone.Count(e =>
                        tagged.Any(t => t.ElementId == e.ElementId));
                    result.ZoneCoverage[zone.Key] = zoneTotal > 0
                        ? (double)zoneTagged / zoneTotal : 0;
                }
            }

            return result;
        }

        #endregion

        #region Utility

        private static double Distance2D(Point2D a, Point2D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Clear all cached spatial data.
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _elementContexts.Clear();
                _relationships.Clear();
                _topology.Clear();
                _currentDensityMap = null;
                Logger.Debug("Spatial intelligence cache cleared");
            }
        }

        public int RegisteredElementCount
        {
            get { lock (_lockObject) { return _elementContexts.Count; } }
        }

        #endregion
    }

    #endregion
}
