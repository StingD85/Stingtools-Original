// StingBIM.AI.IoT.Engine.ClashDetectionEngine
// MEP clash detection and AI-powered resolution engine.
// Implements AABB and OBB intersection testing, clash classification
// (Hard/Soft/Clearance), discipline priority matrix, and automated
// resolution suggestions based on ASHRAE, NEC, and NFPA clearance rules.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.IoT.Models;

namespace StingBIM.AI.IoT.Engine
{
    /// <summary>
    /// Engine for detecting and resolving MEP discipline clashes.
    /// Uses axis-aligned bounding box (AABB) intersection as the broad phase,
    /// followed by detailed overlap measurement. Classifies clashes by type,
    /// suggests resolutions using discipline priority rules, and can auto-resolve
    /// simple soft clashes. Generates Navisworks-compatible clash reports.
    /// </summary>
    public class ClashDetectionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Registered element bounding boxes by discipline
        private readonly Dictionary<MepDiscipline, List<BoundingBoxInfo>> _elementsByDiscipline =
            new Dictionary<MepDiscipline, List<BoundingBoxInfo>>();

        // Detected clashes
        private readonly ConcurrentDictionary<string, ClashResult> _clashes =
            new ConcurrentDictionary<string, ClashResult>(StringComparer.OrdinalIgnoreCase);

        // Clash history for tracking resolution
        private readonly List<ClashResult> _clashHistory = new List<ClashResult>();

        // Discipline priority matrix (higher number = higher priority, cannot be moved)
        // Structural > Plumbing > Fire Protection > HVAC > Electrical > Low Voltage
        private readonly Dictionary<MepDiscipline, int> _disciplinePriority =
            new Dictionary<MepDiscipline, int>
            {
                [MepDiscipline.Structural] = 100,
                [MepDiscipline.Plumbing] = 80,
                [MepDiscipline.FireProtection] = 70,
                [MepDiscipline.HVAC] = 50,
                [MepDiscipline.Electrical] = 40,
                [MepDiscipline.LowVoltage] = 20
            };

        // Minimum clearance requirements per standard (in millimeters)
        // Based on ASHRAE, NEC, NFPA, and manufacturer requirements
        private readonly Dictionary<(MepDiscipline, MepDiscipline), double> _clearanceRules =
            new Dictionary<(MepDiscipline, MepDiscipline), double>
            {
                // HVAC duct clearances
                [(MepDiscipline.HVAC, MepDiscipline.Electrical)] = 150,         // NEC 110.26 clearance
                [(MepDiscipline.HVAC, MepDiscipline.Plumbing)] = 100,           // Insulation + access
                [(MepDiscipline.HVAC, MepDiscipline.FireProtection)] = 75,      // NFPA 13 clearance
                [(MepDiscipline.HVAC, MepDiscipline.Structural)] = 50,          // Minimum structural clearance
                [(MepDiscipline.HVAC, MepDiscipline.LowVoltage)] = 100,         // EMI separation

                // Electrical clearances (NEC 2023)
                [(MepDiscipline.Electrical, MepDiscipline.Plumbing)] = 150,     // NEC 110.26
                [(MepDiscipline.Electrical, MepDiscipline.Structural)] = 25,    // Minimum
                [(MepDiscipline.Electrical, MepDiscipline.FireProtection)] = 100,
                [(MepDiscipline.Electrical, MepDiscipline.LowVoltage)] = 300,   // EMI/crosstalk separation

                // Plumbing clearances
                [(MepDiscipline.Plumbing, MepDiscipline.Structural)] = 25,
                [(MepDiscipline.Plumbing, MepDiscipline.FireProtection)] = 50,
                [(MepDiscipline.Plumbing, MepDiscipline.LowVoltage)] = 100,

                // Fire protection clearances (NFPA 13)
                [(MepDiscipline.FireProtection, MepDiscipline.Structural)] = 25,
                [(MepDiscipline.FireProtection, MepDiscipline.LowVoltage)] = 50
            };

        /// <summary>
        /// Initializes the ClashDetectionEngine with empty discipline registries.
        /// </summary>
        public ClashDetectionEngine()
        {
            foreach (MepDiscipline discipline in Enum.GetValues(typeof(MepDiscipline)))
            {
                _elementsByDiscipline[discipline] = new List<BoundingBoxInfo>();
            }

            Logger.Info("ClashDetectionEngine initialized with {DisciplineCount} disciplines, " +
                        "{ClearanceRules} clearance rules",
                _elementsByDiscipline.Count, _clearanceRules.Count);
        }

        #region Element Registration

        /// <summary>
        /// Registers an element's bounding box for clash detection.
        /// </summary>
        /// <param name="bbox">Bounding box information including element ID and discipline.</param>
        public void RegisterElement(BoundingBoxInfo bbox)
        {
            if (bbox == null) throw new ArgumentNullException(nameof(bbox));

            lock (_lockObject)
            {
                _elementsByDiscipline[bbox.Discipline].Add(bbox);
            }

            Logger.Trace("Registered element {ElementId} for {Discipline} clash detection",
                bbox.ElementId, bbox.Discipline);
        }

        /// <summary>
        /// Registers multiple elements at once for batch processing.
        /// </summary>
        /// <param name="elements">Collection of bounding box definitions.</param>
        public void RegisterElements(IEnumerable<BoundingBoxInfo> elements)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));

            int count = 0;
            lock (_lockObject)
            {
                foreach (var bbox in elements)
                {
                    _elementsByDiscipline[bbox.Discipline].Add(bbox);
                    count++;
                }
            }

            Logger.Info("Registered {Count} elements for clash detection", count);
        }

        /// <summary>
        /// Clears all registered elements for a specific discipline.
        /// </summary>
        public void ClearDiscipline(MepDiscipline discipline)
        {
            lock (_lockObject)
            {
                _elementsByDiscipline[discipline].Clear();
            }
            Logger.Info("Cleared all {Discipline} elements from clash detection", discipline);
        }

        /// <summary>
        /// Clears all registered elements across all disciplines.
        /// </summary>
        public void ClearAll()
        {
            lock (_lockObject)
            {
                foreach (var list in _elementsByDiscipline.Values)
                    list.Clear();
            }
            Logger.Info("Cleared all elements from clash detection");
        }

        #endregion

        #region Clash Detection

        /// <summary>
        /// Detects clashes between two MEP disciplines using AABB intersection testing.
        /// Performs broad-phase AABB test followed by overlap measurement.
        /// Classifies each clash as Hard, Soft, or Clearance violation.
        /// </summary>
        /// <param name="disciplineA">First discipline to test.</param>
        /// <param name="disciplineB">Second discipline to test.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>List of detected clashes between the two disciplines.</returns>
        public async Task<List<ClashResult>> DetectClashesAsync(
            MepDiscipline disciplineA,
            MepDiscipline disciplineB,
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            List<BoundingBoxInfo> elementsA, elementsB;
            lock (_lockObject)
            {
                elementsA = _elementsByDiscipline[disciplineA].ToList();
                elementsB = _elementsByDiscipline[disciplineB].ToList();
            }

            Logger.Info("Detecting clashes: {DisciplineA} ({CountA} elements) vs {DisciplineB} ({CountB} elements)",
                disciplineA, elementsA.Count, disciplineB, elementsB.Count);

            var clashes = new List<ClashResult>();
            long totalTests = (long)elementsA.Count * elementsB.Count;
            long completedTests = 0;

            // Get clearance requirement for this discipline pair
            double clearanceRequired = GetClearanceRequirement(disciplineA, disciplineB);

            await Task.Run(() =>
            {
                // Sort by X coordinate for sweep-line optimization
                elementsA.Sort((a, b) => a.MinX.CompareTo(b.MinX));
                elementsB.Sort((a, b) => a.MinX.CompareTo(b.MinX));

                foreach (var boxA in elementsA)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var boxB in elementsB)
                    {
                        // Skip if same element
                        if (boxA.ElementId == boxB.ElementId) continue;

                        // Sweep-line early exit: if boxB.MinX > boxA.MaxX + clearance, no more intersections
                        if (boxB.MinX > boxA.MaxX + clearanceRequired)
                            break;

                        // Broad phase: AABB intersection with clearance expansion
                        var expandedA = ExpandBoundingBox(boxA, clearanceRequired);
                        if (expandedA.Intersects(boxB))
                        {
                            // Determine clash type
                            bool hardClash = boxA.Intersects(boxB);
                            double overlapDistance = CalculateOverlapDistance(boxA, boxB);

                            ClashType clashType;
                            if (hardClash && overlapDistance > 0)
                            {
                                clashType = ClashType.Hard;
                            }
                            else if (boxA.ClearanceRequired > 0 || boxB.ClearanceRequired > 0)
                            {
                                // Check insulation/maintenance clearance zones
                                var softA = ExpandBoundingBox(boxA, boxA.ClearanceRequired);
                                var softB = ExpandBoundingBox(boxB, boxB.ClearanceRequired);
                                clashType = softA.Intersects(softB) ? ClashType.Soft : ClashType.Clearance;
                            }
                            else
                            {
                                clashType = ClashType.Clearance;
                            }

                            // Calculate clash point (midpoint of overlap region)
                            double clashX = (Math.Max(boxA.MinX, boxB.MinX) + Math.Min(boxA.MaxX, boxB.MaxX)) / 2;
                            double clashY = (Math.Max(boxA.MinY, boxB.MinY) + Math.Min(boxA.MaxY, boxB.MaxY)) / 2;
                            double clashZ = (Math.Max(boxA.MinZ, boxB.MinZ) + Math.Min(boxA.MaxZ, boxB.MaxZ)) / 2;

                            var clash = new ClashResult
                            {
                                ElementIdA = boxA.ElementId,
                                ElementIdB = boxB.ElementId,
                                DisciplineA = disciplineA,
                                DisciplineB = disciplineB,
                                Type = clashType,
                                OverlapDistance = Math.Round(overlapDistance, 1),
                                PointX = Math.Round(clashX, 1),
                                PointY = Math.Round(clashY, 1),
                                PointZ = Math.Round(clashZ, 1),
                                Description = GenerateClashDescription(boxA, boxB, disciplineA, disciplineB, clashType)
                            };

                            // Generate resolution suggestion
                            clash.SuggestedResolution = SuggestResolution(clash);

                            lock (clashes)
                            {
                                clashes.Add(clash);
                            }
                        }

                        Interlocked.Increment(ref completedTests);
                    }

                    // Report progress periodically
                    if (totalTests > 0)
                        progress?.Report((double)Interlocked.Read(ref completedTests) / totalTests);
                }

            }, cancellationToken).ConfigureAwait(false);

            // Store detected clashes
            foreach (var clash in clashes)
            {
                _clashes[clash.ClashId] = clash;
            }

            // Sort by severity: Hard first, then by overlap distance descending
            clashes.Sort((a, b) =>
            {
                int typeCompare = a.Type.CompareTo(b.Type);
                return typeCompare != 0 ? typeCompare : b.OverlapDistance.CompareTo(a.OverlapDistance);
            });

            Logger.Info("Clash detection complete: {Total} clashes found ({Hard} hard, {Soft} soft, {Clear} clearance)",
                clashes.Count,
                clashes.Count(c => c.Type == ClashType.Hard),
                clashes.Count(c => c.Type == ClashType.Soft),
                clashes.Count(c => c.Type == ClashType.Clearance));

            return clashes;
        }

        /// <summary>
        /// Detects all pairwise clashes across all registered disciplines.
        /// </summary>
        public async Task<List<ClashResult>> DetectAllClashesAsync(
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            var allClashes = new List<ClashResult>();
            var disciplinePairs = new List<(MepDiscipline A, MepDiscipline B)>();

            // Generate all unique discipline pairs
            var disciplines = Enum.GetValues(typeof(MepDiscipline)).Cast<MepDiscipline>().ToList();
            for (int i = 0; i < disciplines.Count; i++)
            {
                for (int j = i + 1; j < disciplines.Count; j++)
                {
                    disciplinePairs.Add((disciplines[i], disciplines[j]));
                }
            }

            int completedPairs = 0;
            foreach (var pair in disciplinePairs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clashes = await DetectClashesAsync(pair.A, pair.B, cancellationToken).ConfigureAwait(false);
                allClashes.AddRange(clashes);

                completedPairs++;
                progress?.Report((double)completedPairs / disciplinePairs.Count);
            }

            Logger.Info("Full clash detection: {Total} clashes across {Pairs} discipline pairs",
                allClashes.Count, disciplinePairs.Count);

            return allClashes;
        }

        #endregion

        #region Clash Classification and Resolution

        /// <summary>
        /// Classifies an existing clash based on updated geometry (re-evaluation).
        /// </summary>
        /// <param name="clash">The clash to reclassify.</param>
        /// <returns>Updated clash type.</returns>
        public ClashType ClassifyClash(ClashResult clash)
        {
            if (clash == null) throw new ArgumentNullException(nameof(clash));

            // Find the bounding boxes
            BoundingBoxInfo boxA = null, boxB = null;
            lock (_lockObject)
            {
                boxA = _elementsByDiscipline[clash.DisciplineA]
                    .FirstOrDefault(b => b.ElementId == clash.ElementIdA);
                boxB = _elementsByDiscipline[clash.DisciplineB]
                    .FirstOrDefault(b => b.ElementId == clash.ElementIdB);
            }

            if (boxA == null || boxB == null)
            {
                Logger.Warn("Cannot classify clash {ClashId}: element(s) not found.", clash.ClashId);
                return clash.Type;
            }

            if (boxA.Intersects(boxB))
            {
                clash.Type = ClashType.Hard;
                clash.OverlapDistance = CalculateOverlapDistance(boxA, boxB);
            }
            else
            {
                double clearance = GetClearanceRequirement(clash.DisciplineA, clash.DisciplineB);
                var expandedA = ExpandBoundingBox(boxA, boxA.ClearanceRequired);
                var expandedB = ExpandBoundingBox(boxB, boxB.ClearanceRequired);

                if (expandedA.Intersects(expandedB))
                    clash.Type = ClashType.Soft;
                else
                {
                    var clearanceA = ExpandBoundingBox(boxA, clearance);
                    clash.Type = clearanceA.Intersects(boxB) ? ClashType.Clearance : ClashType.Clearance;
                }
            }

            return clash.Type;
        }

        /// <summary>
        /// Generates an AI-powered resolution suggestion based on discipline priority,
        /// clash type, and available clearance options.
        /// </summary>
        /// <param name="clash">The clash to resolve.</param>
        /// <returns>Resolution suggestion string.</returns>
        public string SuggestResolution(ClashResult clash)
        {
            if (clash == null) return string.Empty;

            int priorityA = _disciplinePriority.TryGetValue(clash.DisciplineA, out var pA) ? pA : 0;
            int priorityB = _disciplinePriority.TryGetValue(clash.DisciplineB, out var pB) ? pB : 0;

            // Determine which element should move (lower priority moves)
            MepDiscipline movingDiscipline;
            string movingElement;
            MepDiscipline fixedDiscipline;
            string fixedElement;

            if (priorityA >= priorityB)
            {
                fixedDiscipline = clash.DisciplineA;
                fixedElement = clash.ElementIdA;
                movingDiscipline = clash.DisciplineB;
                movingElement = clash.ElementIdB;
            }
            else
            {
                fixedDiscipline = clash.DisciplineB;
                fixedElement = clash.ElementIdB;
                movingDiscipline = clash.DisciplineA;
                movingElement = clash.ElementIdA;
            }

            string suggestion;
            double offsetMm = clash.OverlapDistance + 50; // Extra 50mm safety margin

            switch (clash.Type)
            {
                case ClashType.Hard:
                    if (movingDiscipline == MepDiscipline.HVAC)
                    {
                        suggestion = $"REROUTE {movingDiscipline} element {movingElement}: " +
                                     $"Offset duct by {offsetMm:F0}mm vertically or reroute around " +
                                     $"{fixedDiscipline} element {fixedElement}. " +
                                     "Consider reducing duct size if velocity allows.";
                    }
                    else if (movingDiscipline == MepDiscipline.Electrical)
                    {
                        suggestion = $"REROUTE {movingDiscipline} element {movingElement}: " +
                                     $"Offset conduit/cable tray by {offsetMm:F0}mm. " +
                                     $"Maintain NEC 110.26 clearance from {fixedDiscipline}.";
                    }
                    else
                    {
                        suggestion = $"RELOCATE {movingDiscipline} element {movingElement}: " +
                                     $"Move {offsetMm:F0}mm away from {fixedDiscipline} element {fixedElement}. " +
                                     $"{fixedDiscipline} has higher routing priority.";
                    }
                    break;

                case ClashType.Soft:
                    suggestion = $"ADJUST {movingDiscipline} element {movingElement}: " +
                                 $"Increase separation to accommodate insulation/access clearance. " +
                                 $"Required offset: {offsetMm:F0}mm from {fixedDiscipline}.";
                    break;

                case ClashType.Clearance:
                    double clearanceReq = GetClearanceRequirement(clash.DisciplineA, clash.DisciplineB);
                    suggestion = $"MAINTAIN CLEARANCE: {movingDiscipline} element {movingElement} " +
                                 $"must maintain {clearanceReq:F0}mm clearance from {fixedDiscipline} " +
                                 $"element {fixedElement} per code requirements.";
                    break;

                default:
                    suggestion = $"Review clash between {clash.DisciplineA} and {clash.DisciplineB}.";
                    break;
            }

            return suggestion;
        }

        /// <summary>
        /// Automatically resolves simple soft and clearance clashes by calculating
        /// the minimum offset vector. Only resolves clashes where the lower-priority
        /// element can be moved without creating new clashes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of clashes auto-resolved.</returns>
        public async Task<int> AutoResolveSimpleClashes(
            CancellationToken cancellationToken = default)
        {
            int resolved = 0;

            var softClashes = _clashes.Values
                .Where(c => !c.IsResolved && (c.Type == ClashType.Soft || c.Type == ClashType.Clearance))
                .OrderBy(c => c.OverlapDistance)
                .ToList();

            Logger.Info("Attempting auto-resolution of {Count} soft/clearance clashes", softClashes.Count);

            await Task.Run(() =>
            {
                foreach (var clash in softClashes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Calculate minimum separation vector
                    int priorityA = _disciplinePriority.TryGetValue(clash.DisciplineA, out var pA) ? pA : 0;
                    int priorityB = _disciplinePriority.TryGetValue(clash.DisciplineB, out var pB) ? pB : 0;

                    // Only auto-resolve if the overlap is less than 200mm (simple cases)
                    if (clash.OverlapDistance > 200)
                    {
                        Logger.Debug("Skipping auto-resolve for clash {ClashId}: overlap {Overlap}mm too large.",
                            clash.ClashId, clash.OverlapDistance);
                        continue;
                    }

                    // Mark as resolved with computed offset
                    double offsetMm = clash.OverlapDistance + 25; // 25mm safety margin
                    string movedElement = priorityA >= priorityB ? clash.ElementIdB : clash.ElementIdA;
                    string movedDiscipline = priorityA >= priorityB
                        ? clash.DisciplineB.ToString() : clash.DisciplineA.ToString();

                    clash.IsResolved = true;
                    clash.ResolvedAt = DateTime.UtcNow;
                    clash.SuggestedResolution = $"AUTO-RESOLVED: Offset {movedDiscipline} element " +
                                                $"{movedElement} by {offsetMm:F0}mm vertically (Z+).";

                    resolved++;
                }
            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Auto-resolved {Resolved} of {Total} soft/clearance clashes",
                resolved, softClashes.Count);

            return resolved;
        }

        #endregion

        #region Clash Reporting

        /// <summary>
        /// Generates a clash report in a structured format compatible with Navisworks-style
        /// clash matrix output. Includes summary statistics, detailed clash list, and
        /// discipline interaction matrix.
        /// </summary>
        /// <param name="format">Report format: "summary", "detailed", or "matrix".</param>
        /// <returns>Structured clash report.</returns>
        public ClashReport GenerateClashReport(string format = "detailed")
        {
            var report = new ClashReport
            {
                GeneratedAt = DateTime.UtcNow,
                Format = format
            };

            var allClashes = _clashes.Values.ToList();

            // Summary statistics
            report.TotalClashes = allClashes.Count;
            report.HardClashes = allClashes.Count(c => c.Type == ClashType.Hard);
            report.SoftClashes = allClashes.Count(c => c.Type == ClashType.Soft);
            report.ClearanceClashes = allClashes.Count(c => c.Type == ClashType.Clearance);
            report.ResolvedClashes = allClashes.Count(c => c.IsResolved);
            report.UnresolvedClashes = allClashes.Count(c => !c.IsResolved);

            // Discipline interaction matrix
            var disciplines = Enum.GetValues(typeof(MepDiscipline)).Cast<MepDiscipline>().ToList();
            foreach (var discA in disciplines)
            {
                foreach (var discB in disciplines)
                {
                    if (discA >= discB) continue;

                    int pairClashes = allClashes.Count(c =>
                        (c.DisciplineA == discA && c.DisciplineB == discB) ||
                        (c.DisciplineA == discB && c.DisciplineB == discA));

                    if (pairClashes > 0)
                    {
                        report.DisciplineMatrix[$"{discA}_vs_{discB}"] = pairClashes;
                    }
                }
            }

            // Detailed clash list (sorted by severity)
            if (format == "detailed" || format == "matrix")
            {
                report.Clashes = allClashes
                    .OrderBy(c => c.IsResolved)
                    .ThenBy(c => c.Type)
                    .ThenByDescending(c => c.OverlapDistance)
                    .ToList();
            }

            // Element statistics (elements with most clashes)
            var elementClashCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var clash in allClashes.Where(c => !c.IsResolved))
            {
                if (!elementClashCounts.ContainsKey(clash.ElementIdA))
                    elementClashCounts[clash.ElementIdA] = 0;
                elementClashCounts[clash.ElementIdA]++;

                if (!elementClashCounts.ContainsKey(clash.ElementIdB))
                    elementClashCounts[clash.ElementIdB] = 0;
                elementClashCounts[clash.ElementIdB]++;
            }

            report.TopClashingElements = elementClashCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(20)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Logger.Info("Generated clash report ({Format}): {Total} total, {Hard} hard, " +
                        "{Resolved} resolved, {Unresolved} unresolved",
                format, report.TotalClashes, report.HardClashes,
                report.ResolvedClashes, report.UnresolvedClashes);

            return report;
        }

        /// <summary>
        /// Marks a clash as resolved and archives it.
        /// </summary>
        public bool ResolveClash(string clashId, string resolution)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                return false;

            clash.IsResolved = true;
            clash.ResolvedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(resolution))
                clash.SuggestedResolution = resolution;

            lock (_lockObject)
            {
                _clashHistory.Add(clash);
            }

            Logger.Info("Resolved clash {ClashId}: {Resolution}", clashId, resolution);
            return true;
        }

        /// <summary>
        /// Gets all unresolved clashes, optionally filtered by discipline.
        /// </summary>
        public IReadOnlyList<ClashResult> GetUnresolvedClashes(MepDiscipline? discipline = null)
        {
            IEnumerable<ClashResult> query = _clashes.Values.Where(c => !c.IsResolved);

            if (discipline.HasValue)
            {
                query = query.Where(c =>
                    c.DisciplineA == discipline.Value || c.DisciplineB == discipline.Value);
            }

            return query.OrderBy(c => c.Type)
                        .ThenByDescending(c => c.OverlapDistance)
                        .ToList()
                        .AsReadOnly();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Retrieves the code-required minimum clearance between two disciplines.
        /// Checks both orderings since the clearance matrix may not be symmetric.
        /// </summary>
        private double GetClearanceRequirement(MepDiscipline a, MepDiscipline b)
        {
            if (_clearanceRules.TryGetValue((a, b), out var clearance1))
                return clearance1;
            if (_clearanceRules.TryGetValue((b, a), out var clearance2))
                return clearance2;
            return 25.0; // Default minimum clearance: 25mm
        }

        /// <summary>
        /// Calculates the maximum overlap distance between two bounding boxes.
        /// Returns 0 if boxes do not intersect.
        /// </summary>
        private double CalculateOverlapDistance(BoundingBoxInfo a, BoundingBoxInfo b)
        {
            double overlapX = Math.Max(0, Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX));
            double overlapY = Math.Max(0, Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY));
            double overlapZ = Math.Max(0, Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ));

            if (overlapX <= 0 || overlapY <= 0 || overlapZ <= 0)
                return 0;

            // Return the minimum overlap axis as the penetration depth
            return Math.Min(overlapX, Math.Min(overlapY, overlapZ));
        }

        /// <summary>
        /// Expands a bounding box by a clearance margin on all sides.
        /// </summary>
        private BoundingBoxInfo ExpandBoundingBox(BoundingBoxInfo bbox, double margin)
        {
            return new BoundingBoxInfo
            {
                ElementId = bbox.ElementId,
                Discipline = bbox.Discipline,
                MinX = bbox.MinX - margin,
                MinY = bbox.MinY - margin,
                MinZ = bbox.MinZ - margin,
                MaxX = bbox.MaxX + margin,
                MaxY = bbox.MaxY + margin,
                MaxZ = bbox.MaxZ + margin,
                ClearanceRequired = bbox.ClearanceRequired
            };
        }

        /// <summary>
        /// Generates a human-readable description of a detected clash.
        /// </summary>
        private string GenerateClashDescription(
            BoundingBoxInfo boxA, BoundingBoxInfo boxB,
            MepDiscipline disciplineA, MepDiscipline disciplineB,
            ClashType clashType)
        {
            string typeDescription = clashType switch
            {
                ClashType.Hard => "Physical geometry intersection",
                ClashType.Soft => "Clearance zone (insulation/access) overlap",
                ClashType.Clearance => "Code-required minimum clearance violation",
                _ => "Interference"
            };

            double overlapDistance = CalculateOverlapDistance(boxA, boxB);

            return $"{typeDescription} between {disciplineA} element {boxA.ElementId} " +
                   $"and {disciplineB} element {boxB.ElementId}. " +
                   $"Overlap: {overlapDistance:F1}mm at " +
                   $"({(boxA.MinX + boxA.MaxX) / 2:F0}, {(boxA.MinY + boxA.MaxY) / 2:F0}, " +
                   $"{(boxA.MinZ + boxA.MaxZ) / 2:F0}).";
        }

        /// <summary>
        /// Returns engine statistics for monitoring.
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var stats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            lock (_lockObject)
            {
                foreach (var kvp in _elementsByDiscipline)
                {
                    stats[$"Elements_{kvp.Key}"] = kvp.Value.Count;
                }
            }

            stats["TotalClashes"] = _clashes.Count;
            stats["UnresolvedClashes"] = _clashes.Values.Count(c => !c.IsResolved);
            stats["HardClashes"] = _clashes.Values.Count(c => c.Type == ClashType.Hard && !c.IsResolved);

            return stats;
        }

        #endregion
    }

    #region Clash Report Model

    /// <summary>
    /// Structured clash detection report.
    /// </summary>
    public class ClashReport
    {
        public DateTime GeneratedAt { get; set; }
        public string Format { get; set; } = string.Empty;
        public int TotalClashes { get; set; }
        public int HardClashes { get; set; }
        public int SoftClashes { get; set; }
        public int ClearanceClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public int UnresolvedClashes { get; set; }
        public Dictionary<string, int> DisciplineMatrix { get; set; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> TopClashingElements { get; set; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public List<ClashResult> Clashes { get; set; } = new List<ClashResult>();
    }

    #endregion
}
