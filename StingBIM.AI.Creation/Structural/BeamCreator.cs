// StingBIM.AI.Creation.Structural.BeamCreator
// Creates structural beams with grid integration
// v4 Prompt Reference: Section A.2.2 — BeamCreator (4 modes)

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Structural
{
    /// <summary>
    /// Creates structural beams with intelligent sizing and grid integration.
    ///
    /// 4 Modes:
    ///   1. Single beam between two points — "Place a 250×450mm beam spanning 6m"
    ///   2. Beams between columns in a row — "Add beams between all columns on Level 2"
    ///   3. All beams for a complete level — "Create all beams for this level"
    ///   4. Lintel beam — "Add a lintel over the window"
    ///
    /// Size Guide (span/depth rule of thumb):
    ///   Concrete: depth ≈ span / 12 to 15
    ///   Steel: depth ≈ span / 20
    ///   200×300mm (3-4m), 250×450mm (5-6m), 300×600mm (7-8m)
    /// </summary>
    public class BeamCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Beam size recommendations by span (mm)
        private static readonly (double MaxSpanMm, double WidthMm, double DepthMm)[] ConcreteBeamSizes =
        {
            (4000, 200, 300),
            (5000, 200, 350),
            (6000, 250, 450),
            (7000, 250, 500),
            (8000, 300, 600),
            (10000, 300, 700),
            (12000, 350, 800),
        };

        public BeamCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Mode 1: Places a single structural beam between two points.
        /// </summary>
        public CreationPipelineResult PlaceBeam(BeamPlacementCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Beam" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("Level not found.");
                    return result;
                }

                // Calculate span
                XYZ startPt, endPt;
                if (cmd.StartPoint != null && cmd.EndPoint != null)
                {
                    startPt = new XYZ(cmd.StartPoint.X * MM_TO_FEET,
                        cmd.StartPoint.Y * MM_TO_FEET, level.Elevation);
                    endPt = new XYZ(cmd.EndPoint.X * MM_TO_FEET,
                        cmd.EndPoint.Y * MM_TO_FEET, level.Elevation);
                }
                else if (cmd.SpanMm > 0)
                {
                    var ox = (cmd.OriginXMm ?? 0) * MM_TO_FEET;
                    var oy = (cmd.OriginYMm ?? 0) * MM_TO_FEET;
                    startPt = new XYZ(ox, oy, level.Elevation);
                    endPt = new XYZ(ox + cmd.SpanMm * MM_TO_FEET, oy, level.Elevation);
                }
                else
                {
                    result.SetError("Beam requires start/end points or a span length.");
                    return result;
                }

                var spanFt = startPt.DistanceTo(endPt);
                var spanMm = spanFt / MM_TO_FEET;

                // Auto-size beam if dimensions not specified
                var widthMm = cmd.WidthMm;
                var depthMm = cmd.DepthMm;
                if (widthMm <= 0 || depthMm <= 0)
                {
                    var recommended = RecommendBeamSize(spanMm, cmd.BeamType);
                    if (widthMm <= 0) widthMm = recommended.WidthMm;
                    if (depthMm <= 0) depthMm = recommended.DepthMm;
                }

                // Resolve beam family
                var familyResult = _familyResolver.ResolveFamilySymbol(
                    BuiltInCategory.OST_StructuralFraming,
                    cmd.BeamType ?? "concrete",
                    widthMm, depthMm);

                if (!familyResult.Success)
                {
                    result.SetError($"No beam family found. {familyResult.Message}");
                    return result;
                }

                var symbol = familyResult.ResolvedType as FamilySymbol;
                if (symbol == null)
                {
                    result.SetError("Resolved element is not a valid beam family.");
                    return result;
                }

                FamilyInstance beam = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Beam"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();

                        var line = Line.CreateBound(startPt, endPt);
                        beam = _document.Create.NewFamilyInstance(
                            line, symbol, level, StructuralType.Beam);

                        if (beam != null)
                        {
                            var markParam = beam.LookupParameter("Mark");
                            markParam?.Set(cmd.Mark ?? "B-01");

                            var commentsParam = beam.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(beam);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("beam", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = beam?.Id;
                result.Message = $"Placed {widthMm:F0}×{depthMm:F0}mm beam, {spanMm / 1000:F1}m span on {level.Name}";
                result.Warnings = "Uganda East African Rift: beams must be designed for seismic loads per Building Control Regulations 2020.";
                result.Suggestions = new List<string>
                {
                    "Add more beams",
                    "Add columns",
                    "Add a floor slab"
                };

                Logger.Info($"Beam placed: {beam?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Beam placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("beam", "place", ex));
            }

            return result;
        }

        /// <summary>
        /// Mode 2: Creates beams between all columns on a level.
        /// Connects adjacent columns with beams.
        /// </summary>
        public CreationPipelineResult CreateBeamsBetweenColumns(string levelName,
            double widthMm = 0, double depthMm = 0, string beamType = "concrete")
        {
            var result = new CreationPipelineResult { ElementType = "Beams (Between Columns)" };

            try
            {
                var level = _familyResolver.ResolveLevel(levelName);
                if (level == null)
                {
                    result.SetError("Level not found.");
                    return result;
                }

                // Collect all columns on this level
                var columns = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(c =>
                    {
                        var baseLevelParam = c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                        return baseLevelParam != null && baseLevelParam.AsElementId() == level.Id;
                    })
                    .ToList();

                if (columns.Count < 2)
                {
                    result.SetError($"Need at least 2 columns on {level.Name} to create beams. Found: {columns.Count}");
                    return result;
                }

                // Get column positions
                var columnPoints = columns.Select(c =>
                {
                    var loc = c.Location as LocationPoint;
                    return (Column: c, Point: loc?.Point ?? XYZ.Zero);
                }).OrderBy(cp => cp.Point.X).ThenBy(cp => cp.Point.Y).ToList();

                var placedCount = 0;
                var createdIds = new List<ElementId>();
                var existingBeams = new HashSet<string>();

                // Connect columns that share X or Y coordinates (same grid line)
                for (int i = 0; i < columnPoints.Count; i++)
                {
                    for (int j = i + 1; j < columnPoints.Count; j++)
                    {
                        var p1 = columnPoints[i].Point;
                        var p2 = columnPoints[j].Point;

                        // Only connect columns on the same grid line (same X or same Y within tolerance)
                        var tolerance = 0.1; // feet
                        var sameX = Math.Abs(p1.X - p2.X) < tolerance;
                        var sameY = Math.Abs(p1.Y - p2.Y) < tolerance;

                        if (!sameX && !sameY) continue;

                        // Avoid duplicate beams
                        var beamKey = $"{Math.Min(p1.X, p2.X):F2},{Math.Min(p1.Y, p2.Y):F2}-{Math.Max(p1.X, p2.X):F2},{Math.Max(p1.Y, p2.Y):F2}";
                        if (existingBeams.Contains(beamKey)) continue;
                        existingBeams.Add(beamKey);

                        var spanMm = p1.DistanceTo(p2) / MM_TO_FEET;
                        var cmd = new BeamPlacementCommand
                        {
                            StartPoint = new XYZPoint(p1.X / MM_TO_FEET, p1.Y / MM_TO_FEET),
                            EndPoint = new XYZPoint(p2.X / MM_TO_FEET, p2.Y / MM_TO_FEET),
                            WidthMm = widthMm,
                            DepthMm = depthMm,
                            BeamType = beamType,
                            LevelName = levelName,
                            Mark = $"B-{placedCount + 1:D2}"
                        };

                        var beamResult = PlaceBeam(cmd);
                        if (beamResult.Success)
                        {
                            placedCount++;
                            if (beamResult.CreatedElementId != null)
                                createdIds.Add(beamResult.CreatedElementId);
                        }
                    }
                }

                result.Success = placedCount > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Created {placedCount} beams between {columns.Count} columns on {level.Name}";
                result.Suggestions = new List<string>
                {
                    "Add a floor slab",
                    "Add bracing",
                    "Review structural frame"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Beam creation between columns failed");
                result.SetError(ErrorExplainer.FormatCreationError("beams", "create", ex));
            }

            return result;
        }

        /// <summary>
        /// Mode 4: Creates a lintel beam over a door or window opening.
        /// </summary>
        public CreationPipelineResult CreateLintel(LintelCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Lintel" };

            try
            {
                // Find the opening (door/window)
                Element opening = null;
                if (cmd.OpeningId != null)
                {
                    opening = _document.GetElement(cmd.OpeningId);
                }

                if (opening == null)
                {
                    result.SetError("Could not find the door/window opening for the lintel.");
                    return result;
                }

                // Get opening bounding box
                var bbox = opening.get_BoundingBox(null);
                if (bbox == null)
                {
                    result.SetError("Could not determine opening dimensions.");
                    return result;
                }

                // Lintel extends 150mm (bearing) beyond opening on each side
                var bearingMm = 150;
                var bearingFt = bearingMm * MM_TO_FEET;

                var openingWidthFt = bbox.Max.X - bbox.Min.X;
                var openingWidthMm = openingWidthFt / MM_TO_FEET;

                // Place beam at top of opening
                var topZ = bbox.Max.Z;
                var startPt = new XYZ(bbox.Min.X - bearingFt, (bbox.Min.Y + bbox.Max.Y) / 2, topZ);
                var endPt = new XYZ(bbox.Max.X + bearingFt, (bbox.Min.Y + bbox.Max.Y) / 2, topZ);

                var totalSpanMm = openingWidthMm + 2 * bearingMm;

                var beamCmd = new BeamPlacementCommand
                {
                    StartPoint = new XYZPoint(startPt.X / MM_TO_FEET, startPt.Y / MM_TO_FEET),
                    EndPoint = new XYZPoint(endPt.X / MM_TO_FEET, endPt.Y / MM_TO_FEET),
                    WidthMm = cmd.WidthMm > 0 ? cmd.WidthMm : 200,
                    DepthMm = cmd.DepthMm > 0 ? cmd.DepthMm : Math.Max(150, totalSpanMm / 10),
                    BeamType = cmd.BeamType ?? "concrete",
                    LevelName = cmd.LevelName,
                    Mark = $"LNT-{opening.Id.Value}"
                };

                return PlaceBeam(beamCmd);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Lintel creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("lintel", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        /// <summary>
        /// Recommends beam size based on span length.
        /// Rule of thumb: concrete depth ≈ span/12 to span/15, steel depth ≈ span/20
        /// </summary>
        private (double WidthMm, double DepthMm) RecommendBeamSize(double spanMm, string beamType)
        {
            var isSteel = beamType?.ToLowerInvariant()?.Contains("steel") == true;

            if (isSteel)
            {
                var depth = spanMm / 20;
                var width = depth * 0.5;
                return (Math.Max(150, Math.Round(width / 50) * 50),
                        Math.Max(200, Math.Round(depth / 50) * 50));
            }

            // Concrete — use lookup table
            foreach (var (maxSpan, w, d) in ConcreteBeamSizes)
            {
                if (spanMm <= maxSpan)
                    return (w, d);
            }

            // Large span — calculate
            var calcDepth = spanMm / 12;
            var calcWidth = calcDepth * 0.5;
            return (Math.Max(300, Math.Round(calcWidth / 50) * 50),
                    Math.Max(600, Math.Round(calcDepth / 50) * 50));
        }

        #endregion
    }

    #region Command DTOs

    public class BeamPlacementCommand
    {
        public XYZPoint StartPoint { get; set; }
        public XYZPoint EndPoint { get; set; }
        public double SpanMm { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public string BeamType { get; set; }
        public string LevelName { get; set; }
        public string Mark { get; set; }
        public double? OriginXMm { get; set; }
        public double? OriginYMm { get; set; }
    }

    public class LintelCommand
    {
        public ElementId OpeningId { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public string BeamType { get; set; }
        public string LevelName { get; set; }
    }

    #endregion
}
