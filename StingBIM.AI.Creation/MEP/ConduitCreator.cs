// StingBIM.AI.Creation.MEP.ConduitCreator
// Handles: conduit routing, conduit sizing (BS 7671 / IEC 60364), cable tray
// v4 Prompt Reference: Section A.3.2 CONDUITS & CABLE MANAGEMENT
// Strategy: spine-and-branch routing from DB to rooms

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates conduit runs and cable tray using spine-and-branch routing.
    ///
    /// Routing Algorithm:
    ///   1. Identify DB location → main spine conduit along corridor at ceiling
    ///   2. Branch to each room: horizontal sub-main → vertical drops
    ///   3. Fittings: elbows at direction changes, tees at branches
    ///
    /// Conduit Sizing (IEC 60364 / BS 7671):
    ///   2×2.5mm² → 16mm, 3-4×2.5mm² → 20mm, 5-6×2.5mm² → 25mm
    ///   Power rings 6mm² → 20mm, Sub-mains 10-16mm² → 25mm
    ///   Distribution 25-35mm² → 32-40mm, Main feeders 50-70mm² → 50mm
    ///   Data Cat6 → 20mm (max 3 cables, separated from power)
    ///
    /// Conduit Types:
    ///   PVC rigid (UPVC): standard in-wall/screed, 20-50mm
    ///   Steel rigid (IMC): plant rooms, high-impact, 20-100mm
    ///   Flexible corrugated: last-metre connections, 16-25mm
    ///   Surface trunking: office cable management, 50×25 to 100×50mm
    ///   Dado trunking: desktop height 750mm AFF
    ///
    /// Cable Tray Types:
    ///   Perforated: general use, good air circulation
    ///   Ladder tray: heavy cable, plant rooms, up to 4m between hangers
    ///   Wire mesh: data centers, IT, lightweight
    /// </summary>
    public class ConduitCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Conduit sizing table (cable size mm² → conduit diameter mm)
        private static readonly Dictionary<string, int> ConduitSizingTable =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["2x2.5"] = 16,
            ["3x2.5"] = 20,
            ["4x2.5"] = 20,
            ["5x2.5"] = 25,
            ["6x2.5"] = 25,
            ["1x6"] = 20,
            ["2x6"] = 20,
            ["1x10"] = 25,
            ["1x16"] = 25,
            ["1x25"] = 32,
            ["1x35"] = 40,
            ["1x50"] = 50,
            ["1x70"] = 50,
            ["data_cat6"] = 20,
        };

        // Standard conduit diameters (mm)
        private static readonly int[] StandardDiameters = { 16, 20, 25, 32, 40, 50 };

        // Cable tray standard widths (mm)
        private static readonly int[] StandardTrayWidths = { 100, 150, 200, 300, 450, 600 };
        private const int STANDARD_TRAY_DEPTH = 75; // mm

        public ConduitCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Conduit Routing

        /// <summary>
        /// Routes conduits from DB location to all rooms using spine-and-branch pattern.
        /// Main spine along corridor at ceiling height, branches to each room.
        /// </summary>
        public CreationPipelineResult RouteConduits(ConduitRoutingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Conduit" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Determine conduit diameter
                var diameterMm = cmd.DiameterMm;
                if (diameterMm <= 0)
                {
                    diameterMm = GetConduitDiameter(cmd.CableSpec ?? "2x2.5");
                }

                // Get conduit type
                var conduitTypeId = ResolveConduitType(cmd.ConduitType ?? "pvc");

                // Get start point (DB location) and calculate routing
                var startPt = cmd.StartPoint ?? FindDBLocation(level);
                if (startPt == null)
                {
                    result.SetError("Could not determine distribution board location. " +
                        "Place a DB first or specify a start point.");
                    return result;
                }

                // Route height: 50mm below ceiling (2.8m default)
                var routeHeightMm = cmd.RouteHeightMm > 0
                    ? cmd.RouteHeightMm
                    : 2750; // 50mm below 2800mm ceiling

                var placedIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();
                var diameterFt = diameterMm * MM_TO_FEET;

                if (cmd.ToAllRooms)
                {
                    // Spine-and-branch to all rooms
                    var rooms = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Autodesk.Revit.DB.Architecture.Room>()
                        .Where(r => r.Area > 0 && r.LevelId == level.Id)
                        .ToList();

                    if (rooms.Count == 0)
                    {
                        result.SetError("No rooms found on this level.");
                        return result;
                    }

                    using (var transaction = new Transaction(_document, "StingBIM: Route Conduits"))
                    {
                        var options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failureHandler);
                        transaction.SetFailureHandlingOptions(options);
                        transaction.Start();

                        try
                        {
                            var routeHeightFt = routeHeightMm * MM_TO_FEET;
                            var startFt = new XYZ(startPt.X * MM_TO_FEET,
                                startPt.Y * MM_TO_FEET, routeHeightFt);

                            foreach (var room in rooms)
                            {
                                var bb = room.get_BoundingBox(null);
                                if (bb == null) continue;

                                // Route to room centroid at ceiling height
                                var roomCenterX = (bb.Min.X + bb.Max.X) / 2;
                                var roomCenterY = (bb.Min.Y + bb.Max.Y) / 2;
                                var endFt = new XYZ(roomCenterX, roomCenterY, routeHeightFt);

                                // Create L-shaped route (X first, then Y)
                                var midFt = new XYZ(endFt.X, startFt.Y, routeHeightFt);

                                // Segment 1: horizontal along X
                                if (Math.Abs(startFt.X - midFt.X) > 0.1)
                                {
                                    var seg1 = CreateConduitSegment(conduitTypeId, diameterFt,
                                        startFt, midFt, level.Id);
                                    if (seg1 != null)
                                        placedIds.Add(seg1.Id);
                                }

                                // Segment 2: horizontal along Y
                                if (Math.Abs(midFt.Y - endFt.Y) > 0.1)
                                {
                                    var seg2 = CreateConduitSegment(conduitTypeId, diameterFt,
                                        midFt, endFt, level.Id);
                                    if (seg2 != null)
                                        placedIds.Add(seg2.Id);
                                }

                                // Segment 3: vertical drop to outlet height (300mm)
                                var dropEnd = new XYZ(endFt.X, endFt.Y, 300 * MM_TO_FEET);
                                if (Math.Abs(endFt.Z - dropEnd.Z) > 0.1)
                                {
                                    var drop = CreateConduitSegment(conduitTypeId, diameterFt,
                                        endFt, dropEnd, level.Id);
                                    if (drop != null)
                                        placedIds.Add(drop.Id);
                                }
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.RollBack();
                            result.SetError(ErrorExplainer.FormatCreationError("conduit", "route", ex));
                            return result;
                        }
                    }
                }
                else
                {
                    // Single run
                    if (cmd.EndPoint == null)
                    {
                        result.SetError("Specify an end point for the conduit run, " +
                            "or use 'route conduits to all rooms'.");
                        return result;
                    }

                    using (var transaction = new Transaction(_document, "StingBIM: Create Conduit"))
                    {
                        var options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failureHandler);
                        transaction.SetFailureHandlingOptions(options);
                        transaction.Start();

                        try
                        {
                            var routeHeightFt = routeHeightMm * MM_TO_FEET;
                            var startFt = new XYZ(startPt.X * MM_TO_FEET,
                                startPt.Y * MM_TO_FEET, routeHeightFt);
                            var endFt = new XYZ(cmd.EndPoint.X * MM_TO_FEET,
                                cmd.EndPoint.Y * MM_TO_FEET, routeHeightFt);

                            var conduit = CreateConduitSegment(conduitTypeId, diameterFt,
                                startFt, endFt, level.Id);
                            if (conduit != null)
                                placedIds.Add(conduit.Id);

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.RollBack();
                            result.SetError(ErrorExplainer.FormatCreationError("conduit", "create", ex));
                            return result;
                        }
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Routed {placedIds.Count} conduit segment(s), " +
                    $"{diameterMm}mm {cmd.ConduitType ?? "PVC"} at {routeHeightMm}mm AFF";
                result.CostEstimate = EstimateConduitCost(placedIds.Count, diameterMm, cmd.ConduitType);

                var warnings = new List<string>();
                if (cmd.ConduitType?.ToLowerInvariant() == "pvc" && diameterMm > 32)
                    warnings.Add("Consider steel conduit (IMC) for main feeders >32mm.");
                if (failureHandler.CapturedWarnings.Count > 0)
                    warnings.Add(ErrorExplainer.SummarizeFailures(failureHandler.CapturedWarnings));
                if (warnings.Count > 0)
                    result.Warnings = string.Join("\n", warnings);

                result.Suggestions = new List<string>
                {
                    "Add cable tray in plant room",
                    "Place lights and outlets",
                    "Add conduit fittings"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Conduit routing failed");
                result.SetError(ErrorExplainer.FormatCreationError("conduit", "route", ex));
            }

            return result;
        }

        #endregion

        #region Cable Tray

        /// <summary>
        /// Creates cable tray runs — for plant rooms, risers, and heavy cable routes.
        /// Standard sizes: 100, 150, 200, 300, 450, 600mm wide; 75mm deep.
        /// Types: perforated (standard), ladder (heavy), wire mesh (data).
        /// </summary>
        public CreationPipelineResult CreateCableTray(CableTrayCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Cable Tray" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Validate width
                var widthMm = cmd.WidthMm;
                if (widthMm <= 0) widthMm = 300; // Default
                // Snap to standard size
                widthMm = StandardTrayWidths.OrderBy(w => Math.Abs(w - widthMm)).First();

                var heightMm = cmd.RouteHeightMm > 0 ? cmd.RouteHeightMm : 2750;
                var lengthMm = cmd.LengthMm > 0 ? cmd.LengthMm : 5000;

                var trayTypeId = ResolveCableTrayType(cmd.TrayType ?? "perforated");
                var failureHandler = new StingBIMFailurePreprocessor();
                var placedIds = new List<ElementId>();

                using (var transaction = new Transaction(_document, "StingBIM: Create Cable Tray"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        var startFt = new XYZ(
                            (cmd.StartX ?? 0) * MM_TO_FEET,
                            (cmd.StartY ?? 0) * MM_TO_FEET,
                            heightMm * MM_TO_FEET);

                        // Default: run along X axis
                        var endFt = new XYZ(
                            startFt.X + lengthMm * MM_TO_FEET,
                            startFt.Y,
                            startFt.Z);

                        if (trayTypeId != ElementId.InvalidElementId)
                        {
                            var tray = CableTray.Create(_document, trayTypeId,
                                startFt, endFt, level.Id);
                            if (tray != null)
                            {
                                // Set width
                                var widthParam = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                                widthParam?.Set(widthMm * MM_TO_FEET);

                                // Set height
                                var heightParam = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                                heightParam?.Set(STANDARD_TRAY_DEPTH * MM_TO_FEET);

                                placedIds.Add(tray.Id);
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("cable tray", "create", ex));
                        return result;
                    }
                }

                var trayTypeName = cmd.TrayType ?? "perforated";
                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Created {widthMm}mm wide {trayTypeName} cable tray, " +
                    $"{lengthMm / 1000.0:F1}m long at {heightMm}mm AFF";

                var separationNotes = new List<string>();
                separationNotes.Add("Service separation: power/data min 300mm apart or use divider.");
                separationNotes.Add("Fire alarm cables: dedicated tray (no sharing with power).");
                separationNotes.Add("Supports: wall bracket or trapeze hanger every 1500mm.");
                result.Warnings = string.Join("\n", separationNotes);

                result.CostEstimate = EstimateCableTrayCost(widthMm, lengthMm, trayTypeName);
                result.Suggestions = new List<string>
                {
                    "Route conduits from tray to rooms",
                    "Add cable ladder for heavy feeders",
                    "Add cable tray supports"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Cable tray creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("cable tray", "create", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private Conduit CreateConduitSegment(ElementId typeId, double diameterFt,
            XYZ start, XYZ end, ElementId levelId)
        {
            try
            {
                if (typeId == null || typeId == ElementId.InvalidElementId)
                {
                    // Find any conduit type
                    typeId = new FilteredElementCollector(_document)
                        .OfClass(typeof(ConduitType))
                        .FirstElementId();
                }

                if (typeId == null || typeId == ElementId.InvalidElementId)
                    return null;

                var conduit = Conduit.Create(_document, typeId,
                    start, end, levelId);

                if (conduit != null)
                {
                    // Set diameter
                    var diaParam = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                    diaParam?.Set(diameterFt);
                }

                return conduit;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to create conduit segment");
                return null;
            }
        }

        private ElementId ResolveConduitType(string typeName)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(ConduitType))
                .ToList();

            if (types.Count == 0)
                return ElementId.InvalidElementId;

            // Try to match by name
            var lower = typeName?.ToLowerInvariant() ?? "pvc";
            var match = types.FirstOrDefault(t =>
                t.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                (lower.Contains("steel") && t.Name.Contains("Metal", StringComparison.OrdinalIgnoreCase)) ||
                (lower.Contains("imc") && t.Name.Contains("Metal", StringComparison.OrdinalIgnoreCase)) ||
                (lower.Contains("rigid") && t.Name.Contains("Rigid", StringComparison.OrdinalIgnoreCase)));

            return match?.Id ?? types.First().Id;
        }

        private ElementId ResolveCableTrayType(string typeName)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(CableTrayType))
                .ToList();

            if (types.Count == 0)
                return ElementId.InvalidElementId;

            var lower = typeName?.ToLowerInvariant() ?? "";
            var match = types.FirstOrDefault(t =>
                t.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                (lower.Contains("ladder") && t.Name.Contains("Ladder", StringComparison.OrdinalIgnoreCase)) ||
                (lower.Contains("mesh") && t.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase)));

            return match?.Id ?? types.First().Id;
        }

        private int GetConduitDiameter(string cableSpec)
        {
            if (ConduitSizingTable.TryGetValue(cableSpec, out var size))
                return size;

            // Default for unknown specs
            return 20;
        }

        private XYZPoint FindDBLocation(Level level)
        {
            // Try to find an existing distribution board
            var db = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .FirstOrDefault(e => e.LevelId == level.Id);

            if (db != null)
            {
                var pt = (db.Location as LocationPoint)?.Point;
                if (pt != null)
                {
                    return new XYZPoint(pt.X / MM_TO_FEET, pt.Y / MM_TO_FEET);
                }
            }

            // Default: origin point
            return new XYZPoint(0, 0);
        }

        #endregion

        #region Cost Estimation

        private CostEstimate EstimateConduitCost(int segments, int diameterMm, string conduitType)
        {
            // Approximate 3m per segment, rate per metre
            var ratePerMetre = conduitType?.ToLowerInvariant() switch
            {
                "steel" or "imc" => diameterMm <= 25 ? 12000 : 18000,
                "flexible" => 8000,
                "trunking" => 15000,
                _ => diameterMm <= 25 ? 5000 : 8000, // PVC
            };
            var estimatedMetres = segments * 3;
            return new CostEstimate
            {
                TotalUGX = estimatedMetres * ratePerMetre,
                Description = $"~{estimatedMetres}m of {diameterMm}mm {conduitType ?? "PVC"} conduit " +
                    $"@ UGX {ratePerMetre:N0}/m"
            };
        }

        private CostEstimate EstimateCableTrayCost(int widthMm, double lengthMm, string trayType)
        {
            var ratePerMetre = trayType?.ToLowerInvariant() switch
            {
                "ladder" => widthMm <= 300 ? 65000 : 95000,
                "mesh" or "wire" => widthMm <= 300 ? 45000 : 60000,
                _ => widthMm <= 300 ? 35000 : 55000, // Perforated
            };
            var lengthM = lengthMm / 1000.0;
            return new CostEstimate
            {
                TotalUGX = (long)(lengthM * ratePerMetre),
                Description = $"{lengthM:F1}m of {widthMm}mm {trayType} cable tray " +
                    $"@ UGX {ratePerMetre:N0}/m"
            };
        }

        #endregion
    }

    #region Command DTOs

    public class ConduitRoutingCommand
    {
        public string LevelName { get; set; }
        public string ConduitType { get; set; }  // pvc, steel, imc, flexible, trunking
        public int DiameterMm { get; set; }
        public string CableSpec { get; set; }  // e.g. "2x2.5" → auto-sizes to 16mm
        public double RouteHeightMm { get; set; }
        public bool ToAllRooms { get; set; }
        public XYZPoint StartPoint { get; set; }
        public XYZPoint EndPoint { get; set; }
    }

    public class CableTrayCommand
    {
        public string LevelName { get; set; }
        public string TrayType { get; set; }  // perforated, ladder, mesh
        public int WidthMm { get; set; }
        public double LengthMm { get; set; }
        public double RouteHeightMm { get; set; }
        public double? StartX { get; set; }
        public double? StartY { get; set; }
    }

    #endregion
}
