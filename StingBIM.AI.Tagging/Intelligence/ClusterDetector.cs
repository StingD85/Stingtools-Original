// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// ClusterDetector.cs - Detects clusters/arrays of similar elements for intelligent representative tagging
// Surpasses BIMLOGIQ's "Typical Tag Filter" with DBSCAN clustering, pattern classification,
// and intelligent representative selection for optimal annotation density

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    /// <summary>
    /// Detects clusters of similar elements within a view and classifies their geometric patterns
    /// to recommend intelligent tagging strategies. This enables "typical" tagging where only a
    /// representative element in a group is tagged, dramatically reducing annotation clutter while
    /// maintaining full documentation coverage.
    ///
    /// <para>
    /// Surpasses BIMLOGIQ's "Typical Tag Filter" by providing:
    /// <list type="bullet">
    ///   <item>DBSCAN-based density clustering instead of simple proximity grouping</item>
    ///   <item>Pattern classification (Linear, Grid, Radial, Irregular) for context-aware tagging</item>
    ///   <item>Intelligent representative selection based on geometric centrality</item>
    ///   <item>Strategy recommendation tuned to cluster size and pattern type</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Algorithm overview:
    /// <list type="number">
    ///   <item>Group input elements by category and family/type combination</item>
    ///   <item>Within each group, run DBSCAN to find density-connected clusters</item>
    ///   <item>For each cluster, classify its geometric pattern via regression and symmetry analysis</item>
    ///   <item>Select the best representative element per cluster</item>
    ///   <item>Recommend a tagging strategy based on cluster characteristics</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ClusterDetector
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Inner Types

        /// <summary>
        /// Lightweight descriptor for an element being evaluated for clustering.
        /// Holds only the data needed for spatial and categorical grouping.
        /// </summary>
        public class ElementInfo
        {
            /// <summary>Revit ElementId of the element.</summary>
            public int ElementId { get; set; }

            /// <summary>2D position of the element in view coordinates.</summary>
            public Point2D Position { get; set; }

            /// <summary>Revit category name (e.g., "Doors", "Walls").</summary>
            public string Category { get; set; }

            /// <summary>
            /// Combined family and type identifier (e.g., "Single-Flush:900x2100mm").
            /// Used for same-type grouping before spatial clustering.
            /// </summary>
            public string FamilyType { get; set; }

            /// <summary>
            /// DBSCAN cluster label. -1 indicates noise (unassigned).
            /// Set during the clustering pass.
            /// </summary>
            internal int ClusterLabel { get; set; } = -1;

            /// <summary>
            /// Whether this point has been visited by the DBSCAN algorithm.
            /// </summary>
            internal bool Visited { get; set; }

            /// <summary>
            /// Creates a new ElementInfo instance.
            /// </summary>
            /// <param name="elementId">Revit ElementId.</param>
            /// <param name="position">2D position in view coordinates.</param>
            /// <param name="category">Revit category name.</param>
            /// <param name="familyType">Combined family:type string.</param>
            public ElementInfo(int elementId, Point2D position, string category, string familyType)
            {
                ElementId = elementId;
                Position = position;
                Category = category ?? string.Empty;
                FamilyType = familyType ?? string.Empty;
            }
        }

        /// <summary>
        /// Result of the full cluster detection pipeline for a set of elements.
        /// </summary>
        public class ClusterDetectionResult
        {
            /// <summary>All detected clusters, ordered by size descending.</summary>
            public List<ElementCluster> Clusters { get; set; } = new List<ElementCluster>();

            /// <summary>Element IDs that were not assigned to any cluster (noise points).</summary>
            public List<int> UnclusteredElementIds { get; set; } = new List<int>();

            /// <summary>Total elements analyzed.</summary>
            public int TotalElementsAnalyzed { get; set; }

            /// <summary>Number of distinct category/family-type groups evaluated.</summary>
            public int GroupsEvaluated { get; set; }

            /// <summary>Duration of the detection operation.</summary>
            public TimeSpan Duration { get; set; }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Distance threshold for DBSCAN neighborhood queries, in model units.
        /// Two elements within this distance are considered neighbors.
        /// Defaults to <see cref="TagSettings.ClusterEpsilon"/>.
        /// </summary>
        public double Epsilon { get; set; }

        /// <summary>
        /// Minimum number of elements required to form a valid cluster.
        /// Groups smaller than this are treated as noise.
        /// Defaults to <see cref="TagSettings.ClusterMinPoints"/>.
        /// </summary>
        public int MinPoints { get; set; }

        /// <summary>
        /// R-squared threshold for classifying a cluster as linear.
        /// A value above this indicates strong collinearity.
        /// </summary>
        public double LinearRSquaredThreshold { get; set; } = 0.9;

        /// <summary>
        /// Maximum coefficient of variation for radial distances to classify as radial.
        /// Lower values require more uniform distances from center.
        /// </summary>
        public double RadialCvThreshold { get; set; } = 0.15;

        /// <summary>
        /// Tolerance factor for grid spacing regularity (fraction of average spacing).
        /// Grid classification requires row and column spacings to be within this tolerance.
        /// </summary>
        public double GridSpacingTolerance { get; set; } = 0.2;

        /// <summary>
        /// Optional view center point used for grid representative selection.
        /// When set, the corner element closest to this point is preferred.
        /// </summary>
        public Point2D? ViewCenter { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new <see cref="ClusterDetector"/> with settings from <see cref="TagConfiguration"/>.
        /// </summary>
        public ClusterDetector()
        {
            var settings = TagConfiguration.Instance.Settings;
            Epsilon = settings.ClusterEpsilon;
            MinPoints = settings.ClusterMinPoints;
        }

        /// <summary>
        /// Creates a new <see cref="ClusterDetector"/> with explicit parameters.
        /// </summary>
        /// <param name="epsilon">Distance threshold for neighbor queries (model units).</param>
        /// <param name="minPoints">Minimum cluster size.</param>
        public ClusterDetector(double epsilon, int minPoints)
        {
            if (epsilon <= 0)
                throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be positive.");
            if (minPoints < 2)
                throw new ArgumentOutOfRangeException(nameof(minPoints), "MinPoints must be at least 2.");

            Epsilon = epsilon;
            MinPoints = minPoints;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Runs the full cluster detection pipeline on the provided elements.
        /// Elements are first grouped by category and family/type, then each group
        /// is spatially clustered using DBSCAN. Detected clusters are classified
        /// by pattern and assigned a recommended tagging strategy.
        /// </summary>
        /// <param name="elements">Elements to analyze. Must not be null.</param>
        /// <returns>Detection result containing all clusters and unclustered elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
        public ClusterDetectionResult DetectClusters(IReadOnlyList<ElementInfo> elements)
        {
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));

            var startTime = DateTime.UtcNow;
            var result = new ClusterDetectionResult
            {
                TotalElementsAnalyzed = elements.Count
            };

            if (elements.Count == 0)
            {
                Logger.Debug("No elements provided for cluster detection");
                return result;
            }

            Logger.Info("Starting cluster detection on {0} elements (epsilon={1:F3}, minPts={2})",
                elements.Count, Epsilon, MinPoints);

            // Step 1: Group elements by category + family/type
            var groups = GroupElementsByCategoryAndType(elements);
            result.GroupsEvaluated = groups.Count;

            Logger.Debug("Grouped elements into {0} category/type groups", groups.Count);

            int clusterIndex = 0;

            // Step 2: Run DBSCAN on each group independently
            foreach (var group in groups)
            {
                string groupKey = group.Key;
                List<ElementInfo> groupElements = group.Value;

                if (groupElements.Count < MinPoints)
                {
                    // Group too small to form any cluster; all are noise
                    foreach (var elem in groupElements)
                        result.UnclusteredElementIds.Add(elem.ElementId);
                    continue;
                }

                // Reset DBSCAN state for this group
                foreach (var elem in groupElements)
                {
                    elem.ClusterLabel = -1;
                    elem.Visited = false;
                }

                // Run DBSCAN
                var clusterLabels = RunDBSCAN(groupElements);

                // Extract clusters from labels
                var labelGroups = groupElements
                    .Where(e => e.ClusterLabel >= 0)
                    .GroupBy(e => e.ClusterLabel);

                foreach (var labelGroup in labelGroups)
                {
                    var clusterElements = labelGroup.ToList();

                    if (clusterElements.Count < MinPoints)
                    {
                        foreach (var elem in clusterElements)
                            result.UnclusteredElementIds.Add(elem.ElementId);
                        continue;
                    }

                    // Parse category and family type from the group key
                    string category = clusterElements[0].Category;
                    string familyType = clusterElements[0].FamilyType;

                    // Step 3: Classify pattern
                    ClusterType patternType = ClassifyPattern(clusterElements);

                    // Step 4: Compute cluster center
                    Point2D center = ComputeCentroid(clusterElements);

                    // Step 5: Compute average spacing
                    double avgSpacing = ComputeAverageNearestNeighborDistance(clusterElements);

                    // Step 6: Compute pattern direction for linear clusters
                    Vector2D direction = patternType == ClusterType.Linear
                        ? ComputeLinearDirection(clusterElements)
                        : new Vector2D(0, 0);

                    // Step 7: Select representative element
                    int representativeId = SelectRepresentative(clusterElements, patternType, center);

                    // Step 8: Recommend strategy
                    ClusterTagStrategy strategy = RecommendStrategy(clusterElements.Count, patternType);

                    var cluster = new ElementCluster
                    {
                        ClusterId = $"CLU-{clusterIndex:D4}",
                        ElementIds = clusterElements.Select(e => e.ElementId).ToList(),
                        CategoryName = category,
                        SharedFamilyType = familyType,
                        Type = patternType,
                        RecommendedStrategy = strategy,
                        RepresentativeElementId = representativeId,
                        ClusterCenter = center,
                        AverageSpacing = avgSpacing,
                        PatternDirection = direction
                    };

                    result.Clusters.Add(cluster);
                    clusterIndex++;

                    Logger.Debug("Cluster {0}: {1} elements, type={2}, strategy={3}, representative={4}",
                        cluster.ClusterId, cluster.Count, patternType, strategy, representativeId);
                }

                // Collect noise points from this group
                foreach (var elem in groupElements.Where(e => e.ClusterLabel < 0))
                    result.UnclusteredElementIds.Add(elem.ElementId);
            }

            // Sort clusters by size descending
            result.Clusters.Sort((a, b) => b.Count.CompareTo(a.Count));

            result.Duration = DateTime.UtcNow - startTime;

            Logger.Info("Cluster detection complete: {0} clusters found, {1} unclustered elements, duration={2:F0}ms",
                result.Clusters.Count, result.UnclusteredElementIds.Count, result.Duration.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// Classifies the geometric pattern of a pre-existing cluster of elements.
        /// Useful when clusters are defined externally (e.g., by selection sets).
        /// </summary>
        /// <param name="elements">Elements in the cluster. Must contain at least 3 elements.</param>
        /// <returns>The classified pattern type.</returns>
        /// <exception cref="ArgumentException">Thrown when fewer than 3 elements are provided.</exception>
        public ClusterType ClassifyExistingCluster(IReadOnlyList<ElementInfo> elements)
        {
            if (elements == null || elements.Count < 3)
                throw new ArgumentException("At least 3 elements are required for pattern classification.", nameof(elements));

            return ClassifyPattern(elements.ToList());
        }

        /// <summary>
        /// Selects the best representative element from a cluster for typical tagging.
        /// </summary>
        /// <param name="elements">Elements in the cluster.</param>
        /// <param name="clusterType">The classified cluster pattern type.</param>
        /// <returns>ElementId of the recommended representative.</returns>
        public int SelectRepresentativeElement(IReadOnlyList<ElementInfo> elements, ClusterType clusterType)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Elements list must not be empty.", nameof(elements));

            Point2D center = ComputeCentroid(elements.ToList());
            return SelectRepresentative(elements.ToList(), clusterType, center);
        }

        /// <summary>
        /// Gets the recommended tagging strategy for a cluster with the given characteristics.
        /// </summary>
        /// <param name="elementCount">Number of elements in the cluster.</param>
        /// <param name="clusterType">Pattern type of the cluster.</param>
        /// <returns>Recommended tagging strategy.</returns>
        public ClusterTagStrategy GetRecommendedStrategy(int elementCount, ClusterType clusterType)
        {
            return RecommendStrategy(elementCount, clusterType);
        }

        #endregion

        #region DBSCAN Clustering

        /// <summary>
        /// Runs the DBSCAN (Density-Based Spatial Clustering of Applications with Noise) algorithm
        /// on the provided elements. Assigns cluster labels to each element's ClusterLabel property.
        /// Noise points retain label -1.
        /// </summary>
        /// <param name="elements">Elements to cluster. Must share the same category/type.</param>
        /// <returns>The number of clusters found.</returns>
        private int RunDBSCAN(List<ElementInfo> elements)
        {
            int clusterCount = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                ElementInfo point = elements[i];

                if (point.Visited)
                    continue;

                point.Visited = true;

                List<int> neighborIndices = RangeQuery(elements, i);

                if (neighborIndices.Count < MinPoints)
                {
                    // Mark as noise (label stays -1)
                    continue;
                }

                // Start a new cluster
                point.ClusterLabel = clusterCount;

                // Expand cluster from this core point
                ExpandCluster(elements, i, neighborIndices, clusterCount);

                clusterCount++;
            }

            Logger.Debug("DBSCAN found {0} clusters in {1} elements", clusterCount, elements.Count);
            return clusterCount;
        }

        /// <summary>
        /// Expands a cluster by iteratively adding density-reachable points.
        /// </summary>
        /// <param name="elements">All elements in the group.</param>
        /// <param name="pointIndex">Index of the seed point.</param>
        /// <param name="neighborIndices">Initial neighborhood of the seed.</param>
        /// <param name="clusterLabel">Label for this cluster.</param>
        private void ExpandCluster(List<ElementInfo> elements, int pointIndex,
            List<int> neighborIndices, int clusterLabel)
        {
            // Use a queue for breadth-first expansion to avoid deep recursion
            var expansionQueue = new Queue<int>(neighborIndices);

            while (expansionQueue.Count > 0)
            {
                int neighborIndex = expansionQueue.Dequeue();
                ElementInfo neighbor = elements[neighborIndex];

                if (!neighbor.Visited)
                {
                    neighbor.Visited = true;

                    List<int> neighborNeighbors = RangeQuery(elements, neighborIndex);

                    if (neighborNeighbors.Count >= MinPoints)
                    {
                        // This neighbor is also a core point; expand through it
                        foreach (int nn in neighborNeighbors)
                        {
                            if (elements[nn].ClusterLabel < 0)
                                expansionQueue.Enqueue(nn);
                        }
                    }
                }

                // Assign to cluster if not already assigned to another cluster
                if (neighbor.ClusterLabel < 0)
                    neighbor.ClusterLabel = clusterLabel;
            }
        }

        /// <summary>
        /// Finds all elements within epsilon distance of the element at the given index.
        /// </summary>
        /// <param name="elements">All elements in the group.</param>
        /// <param name="pointIndex">Index of the query point.</param>
        /// <returns>List of indices of neighboring elements (including the query point itself).</returns>
        private List<int> RangeQuery(List<ElementInfo> elements, int pointIndex)
        {
            var neighbors = new List<int>();
            Point2D queryPoint = elements[pointIndex].Position;
            double epsilonSq = Epsilon * Epsilon;

            for (int i = 0; i < elements.Count; i++)
            {
                double dx = elements[i].Position.X - queryPoint.X;
                double dy = elements[i].Position.Y - queryPoint.Y;
                double distSq = dx * dx + dy * dy;

                if (distSq <= epsilonSq)
                    neighbors.Add(i);
            }

            return neighbors;
        }

        #endregion

        #region Pattern Classification

        /// <summary>
        /// Classifies the geometric pattern of a cluster by testing for linear, grid,
        /// and radial arrangements in order of specificity.
        /// </summary>
        /// <param name="clusterElements">Elements in the cluster (minimum 3).</param>
        /// <returns>The best-matching pattern type.</returns>
        private ClusterType ClassifyPattern(List<ElementInfo> clusterElements)
        {
            if (clusterElements.Count < 3)
                return ClusterType.Irregular;

            // Test for linear pattern first (most specific single-dimension pattern)
            if (IsLinearPattern(clusterElements))
                return ClusterType.Linear;

            // Test for grid pattern (requires at least 4 elements for a 2x2 grid)
            if (clusterElements.Count >= 4 && IsGridPattern(clusterElements))
                return ClusterType.Grid;

            // Test for radial pattern
            if (IsRadialPattern(clusterElements))
                return ClusterType.Radial;

            return ClusterType.Irregular;
        }

        /// <summary>
        /// Tests whether elements are approximately collinear using least-squares linear regression.
        /// Computes R-squared for both Y~X and X~Y regressions to handle vertical lines,
        /// and returns true if either exceeds <see cref="LinearRSquaredThreshold"/>.
        /// </summary>
        /// <param name="elements">Elements to test.</param>
        /// <returns>True if elements form a linear pattern.</returns>
        private bool IsLinearPattern(List<ElementInfo> elements)
        {
            int n = elements.Count;
            if (n < 3) return false;

            // Compute R-squared for Y = aX + b
            double rSquaredYX = ComputeRSquared(
                elements.Select(e => e.Position.X).ToArray(),
                elements.Select(e => e.Position.Y).ToArray());

            if (rSquaredYX >= LinearRSquaredThreshold)
                return true;

            // Also test X = aY + b for near-vertical lines
            double rSquaredXY = ComputeRSquared(
                elements.Select(e => e.Position.Y).ToArray(),
                elements.Select(e => e.Position.X).ToArray());

            return rSquaredXY >= LinearRSquaredThreshold;
        }

        /// <summary>
        /// Computes the coefficient of determination (R-squared) for a simple linear regression
        /// of dependent on independent values.
        /// </summary>
        /// <param name="independent">Independent variable values.</param>
        /// <param name="dependent">Dependent variable values.</param>
        /// <returns>R-squared value between 0.0 and 1.0.</returns>
        private double ComputeRSquared(double[] independent, double[] dependent)
        {
            int n = independent.Length;
            if (n < 3) return 0.0;

            double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0, sumYY = 0;

            for (int i = 0; i < n; i++)
            {
                double x = independent[i];
                double y = dependent[i];
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
                sumYY += y * y;
            }

            double meanY = sumY / n;

            // Total sum of squares
            double ssTot = sumYY - n * meanY * meanY;
            if (Math.Abs(ssTot) < 1e-12)
                return 1.0; // All Y values are identical -> perfect fit on horizontal line

            // Regression sum of squares via normal equations
            double denominator = n * sumXX - sumX * sumX;
            if (Math.Abs(denominator) < 1e-12)
                return 0.0; // All X values are identical -> regression undefined for Y~X

            double slope = (n * sumXY - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;

            // Residual sum of squares
            double ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                double predicted = slope * independent[i] + intercept;
                double residual = dependent[i] - predicted;
                ssRes += residual * residual;
            }

            double rSquared = 1.0 - ssRes / ssTot;

            // Clamp to [0, 1] to guard against floating-point drift
            return Math.Max(0.0, Math.Min(1.0, rSquared));
        }

        /// <summary>
        /// Tests whether elements form a rectangular grid pattern by projecting onto
        /// two orthogonal axes and checking for regular spacing in both directions.
        /// </summary>
        /// <param name="elements">Elements to test (minimum 4).</param>
        /// <returns>True if elements form a grid pattern.</returns>
        private bool IsGridPattern(List<ElementInfo> elements)
        {
            if (elements.Count < 4)
                return false;

            // Find the principal direction of the point cloud using PCA-like approach
            Point2D centroid = ComputeCentroid(elements);
            double primaryAngle = ComputePrincipalAngle(elements, centroid);

            double cosA = Math.Cos(primaryAngle);
            double sinA = Math.Sin(primaryAngle);

            // Project all points onto the principal axis and its perpendicular
            var projections = elements.Select(e => new
            {
                Element = e,
                U = (e.Position.X - centroid.X) * cosA + (e.Position.Y - centroid.Y) * sinA,
                V = -(e.Position.X - centroid.X) * sinA + (e.Position.Y - centroid.Y) * cosA
            }).ToList();

            // Cluster U values to find columns
            var uValues = projections.Select(p => p.U).OrderBy(v => v).ToList();
            var uGroups = ClusterOneDimensional(uValues);

            // Cluster V values to find rows
            var vValues = projections.Select(p => p.V).OrderBy(v => v).ToList();
            var vGroups = ClusterOneDimensional(vValues);

            // A grid needs at least 2 rows and 2 columns
            if (uGroups.Count < 2 || vGroups.Count < 2)
                return false;

            // Check that row count * column count approximately equals element count
            // (allows some missing elements in an incomplete grid)
            int expectedCount = uGroups.Count * vGroups.Count;
            double fillRatio = (double)elements.Count / expectedCount;
            if (fillRatio < 0.6)
                return false;

            // Check spacing regularity along each axis
            bool uSpacingRegular = IsSpacingRegular(uGroups);
            bool vSpacingRegular = IsSpacingRegular(vGroups);

            return uSpacingRegular && vSpacingRegular;
        }

        /// <summary>
        /// Computes the principal axis angle of a set of 2D points relative to a centroid,
        /// using the covariance matrix eigenvector approach.
        /// </summary>
        /// <param name="elements">Elements to analyze.</param>
        /// <param name="centroid">Center of the point cloud.</param>
        /// <returns>Angle in radians of the principal axis.</returns>
        private double ComputePrincipalAngle(List<ElementInfo> elements, Point2D centroid)
        {
            double covXX = 0, covXY = 0, covYY = 0;

            foreach (var elem in elements)
            {
                double dx = elem.Position.X - centroid.X;
                double dy = elem.Position.Y - centroid.Y;
                covXX += dx * dx;
                covXY += dx * dy;
                covYY += dy * dy;
            }

            // Principal angle from the 2x2 covariance matrix
            // theta = 0.5 * atan2(2*covXY, covXX - covYY)
            double angle = 0.5 * Math.Atan2(2.0 * covXY, covXX - covYY);
            return angle;
        }

        /// <summary>
        /// Groups a sorted list of 1D values into clusters where consecutive values
        /// within a tolerance are merged. Returns the centroid of each group.
        /// </summary>
        /// <param name="sortedValues">Sorted 1D values.</param>
        /// <returns>List of group centroid values.</returns>
        private List<double> ClusterOneDimensional(List<double> sortedValues)
        {
            if (sortedValues.Count == 0)
                return new List<double>();

            // Compute a tolerance based on the overall spread
            double range = sortedValues[sortedValues.Count - 1] - sortedValues[0];
            double tolerance = range > 1e-10
                ? range * GridSpacingTolerance * 0.5
                : Epsilon * 0.1;

            var groups = new List<List<double>>();
            var currentGroup = new List<double> { sortedValues[0] };

            for (int i = 1; i < sortedValues.Count; i++)
            {
                if (sortedValues[i] - sortedValues[i - 1] <= tolerance)
                {
                    currentGroup.Add(sortedValues[i]);
                }
                else
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<double> { sortedValues[i] };
                }
            }
            groups.Add(currentGroup);

            return groups.Select(g => g.Average()).ToList();
        }

        /// <summary>
        /// Checks whether a list of 1D group centroids are regularly spaced.
        /// Regular spacing means the coefficient of variation of inter-group distances
        /// is below <see cref="GridSpacingTolerance"/>.
        /// </summary>
        /// <param name="groupCentroids">Sorted group centroid values.</param>
        /// <returns>True if spacing is regular.</returns>
        private bool IsSpacingRegular(List<double> groupCentroids)
        {
            if (groupCentroids.Count < 2)
                return true; // Single group is trivially regular

            var spacings = new List<double>();
            for (int i = 1; i < groupCentroids.Count; i++)
                spacings.Add(groupCentroids[i] - groupCentroids[i - 1]);

            if (spacings.Count == 0)
                return true;

            double meanSpacing = spacings.Average();
            if (Math.Abs(meanSpacing) < 1e-12)
                return true; // All at same position

            double variance = spacings.Sum(s => (s - meanSpacing) * (s - meanSpacing)) / spacings.Count;
            double stdDev = Math.Sqrt(variance);
            double cv = stdDev / Math.Abs(meanSpacing);

            return cv <= GridSpacingTolerance;
        }

        /// <summary>
        /// Tests whether elements are arranged radially around a common center point.
        /// Elements are radial if their distances from the centroid have a low
        /// coefficient of variation (below <see cref="RadialCvThreshold"/>).
        /// </summary>
        /// <param name="elements">Elements to test (minimum 3).</param>
        /// <returns>True if elements form a radial pattern.</returns>
        private bool IsRadialPattern(List<ElementInfo> elements)
        {
            if (elements.Count < 3)
                return false;

            Point2D centroid = ComputeCentroid(elements);

            // Compute distances from centroid
            var distances = elements.Select(e => e.Position.DistanceTo(centroid)).ToList();

            double meanDist = distances.Average();
            if (meanDist < 1e-10)
                return false; // All points at the same location; not radial

            double variance = distances.Sum(d => (d - meanDist) * (d - meanDist)) / distances.Count;
            double stdDev = Math.Sqrt(variance);
            double cv = stdDev / meanDist;

            // Additionally check that elements are spread angularly (not all in one direction)
            if (cv <= RadialCvThreshold)
            {
                double angularSpread = ComputeAngularSpread(elements, centroid);
                // Require at least 180 degrees of angular coverage
                return angularSpread >= Math.PI;
            }

            return false;
        }

        /// <summary>
        /// Computes the angular spread of elements around a center point.
        /// Returns the range (max - min) of angles, accounting for the circular nature of angles.
        /// </summary>
        /// <param name="elements">Elements to analyze.</param>
        /// <param name="center">Center point.</param>
        /// <returns>Angular spread in radians (0 to 2*PI).</returns>
        private double ComputeAngularSpread(List<ElementInfo> elements, Point2D center)
        {
            var angles = new List<double>();

            foreach (var elem in elements)
            {
                double dx = elem.Position.X - center.X;
                double dy = elem.Position.Y - center.Y;
                if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
                    continue;
                angles.Add(Math.Atan2(dy, dx));
            }

            if (angles.Count < 2)
                return 0;

            angles.Sort();

            // Find the largest gap between consecutive angles
            double maxGap = 0;
            for (int i = 1; i < angles.Count; i++)
            {
                double gap = angles[i] - angles[i - 1];
                maxGap = Math.Max(maxGap, gap);
            }

            // Include the wrap-around gap
            double wrapGap = (2 * Math.PI) - (angles[angles.Count - 1] - angles[0]);
            maxGap = Math.Max(maxGap, wrapGap);

            // Angular spread is the complement of the largest gap
            return (2 * Math.PI) - maxGap;
        }

        #endregion

        #region Representative Selection

        /// <summary>
        /// Selects the best representative element from a cluster based on the pattern type.
        /// <list type="bullet">
        ///   <item><b>Linear</b>: The middle element when sorted along the line direction.</item>
        ///   <item><b>Grid</b>: The corner element closest to the view center (or centroid if no view center set).</item>
        ///   <item><b>Radial</b>: The element closest to the cluster centroid.</item>
        ///   <item><b>Irregular/default</b>: The element closest to the cluster centroid (most central).</item>
        /// </list>
        /// </summary>
        /// <param name="clusterElements">Elements in the cluster.</param>
        /// <param name="patternType">Classified pattern type.</param>
        /// <param name="centroid">Pre-computed cluster centroid.</param>
        /// <returns>ElementId of the selected representative.</returns>
        private int SelectRepresentative(List<ElementInfo> clusterElements, ClusterType patternType, Point2D centroid)
        {
            switch (patternType)
            {
                case ClusterType.Linear:
                    return SelectLinearRepresentative(clusterElements);

                case ClusterType.Grid:
                    return SelectGridRepresentative(clusterElements, centroid);

                case ClusterType.Radial:
                    return SelectMostCentralElement(clusterElements, centroid);

                case ClusterType.Irregular:
                default:
                    return SelectMostCentralElement(clusterElements, centroid);
            }
        }

        /// <summary>
        /// For linear clusters, selects the middle element when projected onto the line of best fit.
        /// </summary>
        /// <param name="elements">Elements in the linear cluster.</param>
        /// <returns>ElementId of the middle element.</returns>
        private int SelectLinearRepresentative(List<ElementInfo> elements)
        {
            // Project elements onto the linear direction and pick the median
            Vector2D direction = ComputeLinearDirection(elements);

            // Use the first element as reference for projection
            Point2D reference = elements[0].Position;

            var projections = elements.Select(e => new
            {
                Element = e,
                Projection = (e.Position.X - reference.X) * direction.X +
                             (e.Position.Y - reference.Y) * direction.Y
            })
            .OrderBy(p => p.Projection)
            .ToList();

            // Select the median element
            int medianIndex = projections.Count / 2;
            return projections[medianIndex].Element.ElementId;
        }

        /// <summary>
        /// For grid clusters, selects the corner element closest to the view center.
        /// Corners are identified as elements with extreme coordinates in both grid axes.
        /// </summary>
        /// <param name="elements">Elements in the grid cluster.</param>
        /// <param name="centroid">Cluster centroid.</param>
        /// <returns>ElementId of the selected corner element.</returns>
        private int SelectGridRepresentative(List<ElementInfo> elements, Point2D centroid)
        {
            // Find the bounding elements (corners of the axis-aligned bounding box)
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var elem in elements)
            {
                if (elem.Position.X < minX) minX = elem.Position.X;
                if (elem.Position.X > maxX) maxX = elem.Position.X;
                if (elem.Position.Y < minY) minY = elem.Position.Y;
                if (elem.Position.Y > maxY) maxY = elem.Position.Y;
            }

            // Define the four corners of the bounding box
            var corners = new[]
            {
                new Point2D(minX, minY),
                new Point2D(minX, maxY),
                new Point2D(maxX, minY),
                new Point2D(maxX, maxY)
            };

            // For each corner, find the closest element
            var cornerElements = new List<ElementInfo>();
            foreach (var corner in corners)
            {
                ElementInfo closest = null;
                double closestDist = double.MaxValue;

                foreach (var elem in elements)
                {
                    double dist = elem.Position.DistanceTo(corner);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = elem;
                    }
                }

                if (closest != null && !cornerElements.Contains(closest))
                    cornerElements.Add(closest);
            }

            // Select the corner element closest to the reference point
            Point2D referencePoint = ViewCenter ?? centroid;

            ElementInfo bestCorner = null;
            double bestDistance = double.MaxValue;

            foreach (var cornerElem in cornerElements)
            {
                double dist = cornerElem.Position.DistanceTo(referencePoint);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestCorner = cornerElem;
                }
            }

            return bestCorner?.ElementId ?? elements[0].ElementId;
        }

        /// <summary>
        /// Selects the element closest to the cluster centroid (most central element).
        /// </summary>
        /// <param name="elements">Elements in the cluster.</param>
        /// <param name="centroid">Cluster centroid.</param>
        /// <returns>ElementId of the most central element.</returns>
        private int SelectMostCentralElement(List<ElementInfo> elements, Point2D centroid)
        {
            ElementInfo closest = null;
            double closestDist = double.MaxValue;

            foreach (var elem in elements)
            {
                double dist = elem.Position.DistanceTo(centroid);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = elem;
                }
            }

            return closest?.ElementId ?? elements[0].ElementId;
        }

        #endregion

        #region Strategy Recommendation

        /// <summary>
        /// Recommends a tagging strategy based on cluster size and geometric pattern.
        ///
        /// <para>Strategy logic:</para>
        /// <list type="table">
        ///   <listheader>
        ///     <term>Cluster Size</term>
        ///     <description>Strategy</description>
        ///   </listheader>
        ///   <item>
        ///     <term>Small (3-5)</term>
        ///     <description>TagAll or TagGrouped depending on pattern</description>
        ///   </item>
        ///   <item>
        ///     <term>Medium (6-15)</term>
        ///     <description>TagTypical with count annotation</description>
        ///   </item>
        ///   <item>
        ///     <term>Large (16+)</term>
        ///     <description>TagTypical or TagRange for linear patterns</description>
        ///   </item>
        /// </list>
        ///
        /// <para>Pattern overrides:</para>
        /// <list type="bullet">
        ///   <item>Linear clusters always prefer TagRange regardless of size</item>
        ///   <item>Grid clusters with 6+ elements prefer TagTypical with count</item>
        /// </list>
        /// </summary>
        /// <param name="elementCount">Number of elements in the cluster.</param>
        /// <param name="clusterType">Classified pattern type.</param>
        /// <returns>Recommended tagging strategy.</returns>
        private ClusterTagStrategy RecommendStrategy(int elementCount, ClusterType clusterType)
        {
            // Linear patterns: prefer range tagging (tag first and last)
            if (clusterType == ClusterType.Linear)
            {
                if (elementCount <= 5)
                    return ClusterTagStrategy.TagAll;
                return ClusterTagStrategy.TagRange;
            }

            // Small clusters: tag all or group them
            if (elementCount <= 5)
            {
                if (clusterType == ClusterType.Grid || clusterType == ClusterType.Radial)
                    return ClusterTagStrategy.TagGrouped;
                return ClusterTagStrategy.TagAll;
            }

            // Medium clusters: typical with count
            if (elementCount <= 15)
            {
                return ClusterTagStrategy.TagWithCount;
            }

            // Large clusters: typical or range
            if (clusterType == ClusterType.Grid)
                return ClusterTagStrategy.TagWithCount;

            return ClusterTagStrategy.TagTypical;
        }

        #endregion

        #region Geometry Utilities

        /// <summary>
        /// Groups elements by their combined category and family/type key.
        /// DBSCAN runs independently within each group so that only elements
        /// of the same type can cluster together.
        /// </summary>
        /// <param name="elements">All elements to group.</param>
        /// <returns>Dictionary mapping group key to element list.</returns>
        private Dictionary<string, List<ElementInfo>> GroupElementsByCategoryAndType(
            IReadOnlyList<ElementInfo> elements)
        {
            var groups = new Dictionary<string, List<ElementInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var elem in elements)
            {
                string key = $"{elem.Category}|{elem.FamilyType}";

                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<ElementInfo>();
                    groups[key] = list;
                }

                list.Add(elem);
            }

            return groups;
        }

        /// <summary>
        /// Computes the centroid (arithmetic mean position) of a set of elements.
        /// </summary>
        /// <param name="elements">Elements to compute centroid for.</param>
        /// <returns>Centroid position.</returns>
        private Point2D ComputeCentroid(List<ElementInfo> elements)
        {
            double sumX = 0, sumY = 0;

            foreach (var elem in elements)
            {
                sumX += elem.Position.X;
                sumY += elem.Position.Y;
            }

            int count = elements.Count;
            return new Point2D(sumX / count, sumY / count);
        }

        /// <summary>
        /// Computes the average nearest-neighbor distance for elements in a cluster.
        /// This provides a measure of typical spacing between elements.
        /// </summary>
        /// <param name="elements">Elements in the cluster.</param>
        /// <returns>Average distance to nearest neighbor, or 0 if fewer than 2 elements.</returns>
        private double ComputeAverageNearestNeighborDistance(List<ElementInfo> elements)
        {
            if (elements.Count < 2)
                return 0;

            double totalNearestDist = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                double minDist = double.MaxValue;

                for (int j = 0; j < elements.Count; j++)
                {
                    if (i == j) continue;

                    double dist = elements[i].Position.DistanceTo(elements[j].Position);
                    if (dist < minDist)
                        minDist = dist;
                }

                totalNearestDist += minDist;
            }

            return totalNearestDist / elements.Count;
        }

        /// <summary>
        /// Computes the principal direction vector of a set of elements using linear regression.
        /// For linear clusters, this gives the line direction; for other patterns, the dominant axis.
        /// The returned vector is normalized.
        /// </summary>
        /// <param name="elements">Elements to compute direction for.</param>
        /// <returns>Normalized direction vector along the principal axis.</returns>
        private Vector2D ComputeLinearDirection(List<ElementInfo> elements)
        {
            if (elements.Count < 2)
                return new Vector2D(1, 0);

            Point2D centroid = ComputeCentroid(elements);

            // Use covariance matrix to find principal direction
            double covXX = 0, covXY = 0, covYY = 0;

            foreach (var elem in elements)
            {
                double dx = elem.Position.X - centroid.X;
                double dy = elem.Position.Y - centroid.Y;
                covXX += dx * dx;
                covXY += dx * dy;
                covYY += dy * dy;
            }

            // The principal eigenvector of the 2x2 covariance matrix
            // For [[covXX, covXY], [covXY, covYY]], the eigenvector for the larger eigenvalue
            double angle = 0.5 * Math.Atan2(2.0 * covXY, covXX - covYY);

            var direction = new Vector2D(Math.Cos(angle), Math.Sin(angle));
            return direction.Normalize();
        }

        /// <summary>
        /// For a linear cluster, identifies the first and last elements along the line direction.
        /// Useful for the TagRange strategy which tags endpoints.
        /// </summary>
        /// <param name="elements">Elements in the linear cluster.</param>
        /// <returns>
        /// Tuple containing (firstElementId, lastElementId) ordered by projection
        /// along the linear direction.
        /// </returns>
        public (int FirstElementId, int LastElementId) GetLinearEndpoints(IReadOnlyList<ElementInfo> elements)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Elements list must not be empty.", nameof(elements));

            if (elements.Count == 1)
                return (elements[0].ElementId, elements[0].ElementId);

            Vector2D direction = ComputeLinearDirection(elements.ToList());
            Point2D reference = elements[0].Position;

            double minProj = double.MaxValue;
            double maxProj = double.MinValue;
            int firstId = elements[0].ElementId;
            int lastId = elements[0].ElementId;

            foreach (var elem in elements)
            {
                double proj = (elem.Position.X - reference.X) * direction.X +
                              (elem.Position.Y - reference.Y) * direction.Y;

                if (proj < minProj)
                {
                    minProj = proj;
                    firstId = elem.ElementId;
                }

                if (proj > maxProj)
                {
                    maxProj = proj;
                    lastId = elem.ElementId;
                }
            }

            return (firstId, lastId);
        }

        /// <summary>
        /// For a grid cluster, computes the row and column counts.
        /// Returns (rows, columns) based on principal axis analysis.
        /// </summary>
        /// <param name="elements">Elements in the grid cluster.</param>
        /// <returns>Tuple of (row count, column count).</returns>
        public (int Rows, int Columns) GetGridDimensions(IReadOnlyList<ElementInfo> elements)
        {
            if (elements == null || elements.Count < 4)
                return (1, elements?.Count ?? 0);

            var elementList = elements.ToList();
            Point2D centroid = ComputeCentroid(elementList);
            double angle = ComputePrincipalAngle(elementList, centroid);

            double cosA = Math.Cos(angle);
            double sinA = Math.Sin(angle);

            var uValues = elements.Select(e =>
                (e.Position.X - centroid.X) * cosA + (e.Position.Y - centroid.Y) * sinA
            ).OrderBy(v => v).ToList();

            var vValues = elements.Select(e =>
                -(e.Position.X - centroid.X) * sinA + (e.Position.Y - centroid.Y) * cosA
            ).OrderBy(v => v).ToList();

            int columns = ClusterOneDimensional(uValues).Count;
            int rows = ClusterOneDimensional(vValues).Count;

            return (rows, columns);
        }

        /// <summary>
        /// For a radial cluster, computes the center point and average radius.
        /// </summary>
        /// <param name="elements">Elements in the radial cluster.</param>
        /// <returns>Tuple of (center point, average radius).</returns>
        public (Point2D Center, double Radius) GetRadialGeometry(IReadOnlyList<ElementInfo> elements)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Elements list must not be empty.", nameof(elements));

            var elementList = elements.ToList();
            Point2D center = ComputeCentroid(elementList);
            double avgRadius = elements.Average(e => e.Position.DistanceTo(center));

            return (center, avgRadius);
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Generates a human-readable summary of a cluster detection result for logging
        /// and diagnostic purposes.
        /// </summary>
        /// <param name="result">Detection result to summarize.</param>
        /// <returns>Multi-line summary string.</returns>
        public string GenerateSummary(ClusterDetectionResult result)
        {
            if (result == null)
                return "No detection result available.";

            var lines = new List<string>
            {
                $"Cluster Detection Summary",
                $"========================",
                $"  Elements analyzed: {result.TotalElementsAnalyzed}",
                $"  Groups evaluated:  {result.GroupsEvaluated}",
                $"  Clusters found:    {result.Clusters.Count}",
                $"  Unclustered:       {result.UnclusteredElementIds.Count}",
                $"  Duration:          {result.Duration.TotalMilliseconds:F1}ms",
                $"  Parameters:        epsilon={Epsilon:F3}, minPts={MinPoints}",
                string.Empty
            };

            foreach (var cluster in result.Clusters)
            {
                lines.Add($"  [{cluster.ClusterId}] {cluster.CategoryName} / {cluster.SharedFamilyType}");
                lines.Add($"    Elements:        {cluster.Count}");
                lines.Add($"    Pattern:         {cluster.Type}");
                lines.Add($"    Strategy:        {cluster.RecommendedStrategy}");
                lines.Add($"    Representative:  {cluster.RepresentativeElementId}");
                lines.Add($"    Center:          {cluster.ClusterCenter}");
                lines.Add($"    Avg spacing:     {cluster.AverageSpacing:F4}");

                if (cluster.Type == ClusterType.Linear)
                    lines.Add($"    Direction:       ({cluster.PatternDirection.X:F3}, {cluster.PatternDirection.Y:F3})");

                lines.Add(string.Empty);
            }

            return string.Join(Environment.NewLine, lines);
        }

        #endregion
    }
}
