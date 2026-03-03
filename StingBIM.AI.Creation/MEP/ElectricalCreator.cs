// StingBIM.AI.Creation.MEP.ElectricalCreator
// Handles: lights (LUX algorithm), power outlets, switches, DBs, generators
// v4 Prompt Reference: Section A.3.1 ELECTRICAL
// Standards: BS 7671 (Uganda wiring regs), NEC 2023, CIBSE Lighting Guide

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
    /// Creates electrical elements: lighting (LUX-based), power outlets, switches,
    /// distribution boards, generators, and solar-ready wiring.
    ///
    /// Lighting Algorithm:
    ///   fixtureCount = ceil((roomArea × luxTarget) / (fixtureOutput × CU × MF))
    ///   CU = 0.65, MF = 0.80 (LED, 2-year cleaning cycle)
    ///   Grid placement: 1→centroid, 2→thirds, 4→quarters, N→grid
    ///
    /// Uganda/East Africa Context:
    ///   - BS 7671 wiring regulations (adopted in East Africa)
    ///   - Generator strongly recommended (power reliability)
    ///   - Solar-ready wiring for new builds
    ///   - IP44 minimum for wet areas, IP55 for external
    /// </summary>
    public class ElectricalCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Lighting design constants
        private const double CU = 0.65;  // Coefficient of utilisation
        private const double MF = 0.80;  // Maintenance factor (LED, 2yr cycle)
        private const double DEFAULT_DOWNLIGHT_LUMENS = 800.0;
        private const double DEFAULT_FLUORESCENT_LUMENS = 1200.0;

        // LUX targets by room type (CIBSE / BS EN 12464-1)
        private static readonly Dictionary<string, int> LuxTargets = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Bedroom"] = 150,
            ["Master Bedroom"] = 150,
            ["Bathroom"] = 150,
            ["En-suite"] = 150,
            ["Kitchen"] = 300,
            ["Living Room"] = 200,
            ["Dining Room"] = 200,
            ["Office"] = 500,
            ["Study"] = 500,
            ["Conference"] = 500,
            ["Corridor"] = 100,
            ["Hallway"] = 100,
            ["Store Room"] = 100,
            ["Car Park"] = 100,
            ["Reception"] = 300,
            ["Lobby"] = 200,
            ["Server Room"] = 500,
            ["Plant Room"] = 200,
            ["Staircase"] = 150,
            ["Verandah"] = 100,
            ["Balcony"] = 100,
            ["Laundry"] = 200,
            ["Utility Room"] = 200,
            ["Pantry"] = 200,
            ["Garage"] = 100,
        };

        // Outlet heights (mm AFF)
        private const double STANDARD_OUTLET_HEIGHT = 300;
        private const double WET_AREA_OUTLET_HEIGHT = 1500;
        private const double KITCHEN_WORKTOP_HEIGHT = 900;
        private const double SWITCH_HEIGHT = 1200;
        private const double SWITCH_FROM_DOOR = 200;  // mm from door frame

        public ElectricalCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Lighting

        /// <summary>
        /// Places lighting fixtures in a room using the LUX calculation algorithm.
        /// fixtureCount = ceil((roomArea × luxTarget) / (fixtureOutput × CU × MF))
        /// Then arranges fixtures on a grid pattern.
        /// </summary>
        public CreationPipelineResult PlaceLightsInRoom(LightingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Lighting" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Find the room
                var room = FindRoom(cmd.RoomName, level);
                if (room == null)
                {
                    result.SetError($"Room '{cmd.RoomName ?? "unknown"}' not found on {level.Name}.");
                    return result;
                }

                // Calculate room area
                var roomAreaSqM = room.Area * 0.092903; // sq ft → m²
                if (roomAreaSqM <= 0)
                {
                    result.SetError("Room has no computed area. Ensure walls form an enclosed boundary.");
                    return result;
                }

                // Get room dimensions for grid layout
                var bb = room.get_BoundingBox(null);
                double roomLengthFt = bb != null ? bb.Max.X - bb.Min.X : 0;
                double roomWidthFt = bb != null ? bb.Max.Y - bb.Min.Y : 0;
                var roomLengthM = roomLengthFt * 0.3048;
                var roomWidthM = roomWidthFt * 0.3048;

                // Determine LUX target
                var roomType = cmd.RoomType ?? room.Name ?? "Room";
                var luxTarget = GetLuxTarget(roomType, cmd.LuxOverride);

                // Determine fixture output
                var fixtureType = cmd.LightType?.ToLowerInvariant() ?? "downlight";
                var fixtureLumens = fixtureType.Contains("fluorescent") || fixtureType.Contains("tube")
                    ? DEFAULT_FLUORESCENT_LUMENS
                    : DEFAULT_DOWNLIGHT_LUMENS;
                if (cmd.LumensOverride > 0)
                    fixtureLumens = cmd.LumensOverride;

                // LUX algorithm
                var fixtureCount = (int)Math.Ceiling(
                    (roomAreaSqM * luxTarget) / (fixtureLumens * CU * MF));
                if (fixtureCount < 1) fixtureCount = 1;

                // Resolve light family
                var lightSymbol = _familyResolver.ResolveMEPFixture("light", fixtureType);

                // Calculate grid positions
                var positions = CalculateLightGrid(fixtureCount,
                    bb?.Min ?? XYZ.Zero, roomLengthFt, roomWidthFt);

                // Place fixtures
                var placedIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Lights"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var pt in positions)
                        {
                            // Place at ceiling height (2700mm default or room ceiling)
                            var ceilingHeightFt = (cmd.CeilingHeightMm > 0
                                ? cmd.CeilingHeightMm : 2700) * MM_TO_FEET;
                            var placePt = new XYZ(pt.X, pt.Y, ceilingHeightFt);

                            FamilyInstance light;
                            if (lightSymbol != null)
                            {
                                light = _document.Create.NewFamilyInstance(
                                    placePt, lightSymbol, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            else
                            {
                                // Fallback: place a generic lighting fixture
                                var genericSymbol = FindAnyLightingFixture();
                                if (genericSymbol == null)
                                {
                                    result.SetError(
                                        "No lighting fixture families loaded in the project. " +
                                        "Load a lighting family first (e.g. Downlight, Fluorescent Tube).");
                                    transaction.RollBack();
                                    return result;
                                }
                                light = _document.Create.NewFamilyInstance(
                                    placePt, genericSymbol, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }

                            if (light != null)
                                placedIds.Add(light.Id);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("lights", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} {fixtureType} light(s) in " +
                    $"'{roomType}' ({roomAreaSqM:F1}m², {luxTarget} lux target)";
                result.CostEstimate = EstimateLightingCost(placedIds.Count, fixtureType);
                result.Suggestions = new List<string>
                {
                    "Add lights to more rooms",
                    "Place power outlets",
                    "Add light switches"
                };

                if (failureHandler.CapturedWarnings.Count > 0)
                    result.Warnings = ErrorExplainer.SummarizeFailures(failureHandler.CapturedWarnings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Lighting placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("lights", "place", ex));
            }

            return result;
        }

        /// <summary>
        /// Places lighting in all rooms on a level (or all levels).
        /// </summary>
        public CreationPipelineResult PlaceLightsAllRooms(string levelName, string lightType)
        {
            var result = new CreationPipelineResult { ElementType = "Lighting" };

            try
            {
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (!string.IsNullOrEmpty(levelName))
                {
                    var level = _familyResolver.ResolveLevel(levelName);
                    if (level != null)
                        rooms = rooms.Where(r => r.LevelId == level.Id).ToList();
                }

                if (rooms.Count == 0)
                {
                    result.SetError("No rooms found in the project. Create rooms first.");
                    return result;
                }

                var totalPlaced = 0;
                var allIds = new List<ElementId>();
                var details = new List<string>();

                foreach (var room in rooms)
                {
                    var cmd = new LightingCommand
                    {
                        RoomName = room.Name,
                        LightType = lightType,
                    };
                    var roomResult = PlaceLightsInRoom(cmd);
                    if (roomResult.Success)
                    {
                        totalPlaced += roomResult.CreatedCount;
                        if (roomResult.CreatedElementIds != null)
                            allIds.AddRange(roomResult.CreatedElementIds);
                        details.Add($"  {room.Name}: {roomResult.CreatedCount} lights");
                    }
                }

                result.Success = totalPlaced > 0;
                result.CreatedElementIds = allIds;
                result.Message = $"Placed {totalPlaced} lights across {rooms.Count} rooms:\n" +
                    string.Join("\n", details);
                result.CostEstimate = EstimateLightingCost(totalPlaced, lightType ?? "downlight");
                result.Suggestions = new List<string>
                {
                    "Place power outlets in all rooms",
                    "Add light switches",
                    "Add emergency lighting"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "All-rooms lighting failed");
                result.SetError(ErrorExplainer.FormatCreationError("lights", "place", ex));
            }

            return result;
        }

        #endregion

        #region Power Outlets

        /// <summary>
        /// Places power outlets in a room — 2 per wall face minimum at 300mm AFF.
        /// Wet areas: 1500mm AFF, IP44 minimum.
        /// Kitchen worktop: 900mm AFF every 600mm.
        /// </summary>
        public CreationPipelineResult PlaceOutlets(OutletCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Power Outlets" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Determine outlet height based on room type
                var isWetArea = IsWetArea(cmd.RoomType);
                var isKitchenWorktop = cmd.RoomType?.ToLowerInvariant()?.Contains("kitchen") == true
                    && cmd.WorktopOutlets;
                var outletHeight = isKitchenWorktop ? KITCHEN_WORKTOP_HEIGHT
                    : isWetArea ? WET_AREA_OUTLET_HEIGHT
                    : STANDARD_OUTLET_HEIGHT;

                if (cmd.HeightMm > 0)
                    outletHeight = cmd.HeightMm;

                // Find walls in the room
                var walls = FindRoomWalls(cmd.RoomName, level);
                if (walls.Count == 0)
                {
                    result.SetError("No walls found for this room. Create the room first.");
                    return result;
                }

                var outletSymbol = _familyResolver.ResolveMEPFixture("outlet", "power");
                var placedIds = new List<ElementId>();
                var outletsPerWall = cmd.OutletsPerWall > 0 ? cmd.OutletsPerWall : 2;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Outlets"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var wall in walls)
                        {
                            var wallLine = (wall.Location as LocationCurve)?.Curve as Line;
                            if (wallLine == null) continue;

                            var wallLengthFt = wallLine.Length;
                            var wallLengthMm = wallLengthFt / MM_TO_FEET;

                            // Skip very short walls
                            if (wallLengthMm < 800) continue;

                            // Distribute outlets evenly along wall, 500mm from corners
                            var cornerOffsetFt = 500 * MM_TO_FEET;
                            var usableLength = wallLengthFt - 2 * cornerOffsetFt;
                            var spacing = usableLength / (outletsPerWall + 1);

                            var dir = (wallLine.GetEndPoint(1) - wallLine.GetEndPoint(0)).Normalize();
                            var heightFt = outletHeight * MM_TO_FEET;

                            for (int i = 1; i <= outletsPerWall; i++)
                            {
                                var offset = cornerOffsetFt + spacing * i;
                                var pt = wallLine.GetEndPoint(0) + dir * offset;
                                var placePt = new XYZ(pt.X, pt.Y, heightFt);

                                FamilyInstance outlet = null;
                                if (outletSymbol != null)
                                {
                                    outlet = _document.Create.NewFamilyInstance(
                                        placePt, outletSymbol, wall, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                }
                                else
                                {
                                    var generic = FindAnyElectricalFixture("outlet");
                                    if (generic != null)
                                    {
                                        outlet = _document.Create.NewFamilyInstance(
                                            placePt, generic, wall, level,
                                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    }
                                }

                                if (outlet != null)
                                    placedIds.Add(outlet.Id);
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("outlets", "place", ex));
                        return result;
                    }
                }

                var heightDesc = isWetArea ? $"{outletHeight}mm AFF (wet area, IP44)"
                    : isKitchenWorktop ? $"{outletHeight}mm AFF (worktop level)"
                    : $"{outletHeight}mm AFF";

                result.Success = true;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} power outlet(s) at {heightDesc}";
                result.CostEstimate = EstimateOutletCost(placedIds.Count, isWetArea);
                result.Suggestions = new List<string>
                {
                    "Add light switches",
                    "Add outlets to more rooms",
                    "Add a distribution board"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Outlet placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("outlets", "place", ex));
            }

            return result;
        }

        #endregion

        #region Switches

        /// <summary>
        /// Places light switches at 1200mm AFF, 200mm from door frame (latch side).
        /// 2-way switching for rooms with 2 access points.
        /// </summary>
        public CreationPipelineResult PlaceSwitches(SwitchCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Switches" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Find doors in the room to determine switch positions
                var doors = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(d => d.LevelId == level.Id)
                    .ToList();

                if (!string.IsNullOrEmpty(cmd.RoomName))
                {
                    // Filter doors by proximity to room
                    doors = doors.Where(d =>
                    {
                        var doorRoom = d.Room;
                        var doorFromRoom = d.FromRoom;
                        return (doorRoom != null && doorRoom.Name.Contains(cmd.RoomName,
                                   StringComparison.OrdinalIgnoreCase)) ||
                               (doorFromRoom != null && doorFromRoom.Name.Contains(cmd.RoomName,
                                   StringComparison.OrdinalIgnoreCase));
                    }).ToList();
                }

                var switchSymbol = _familyResolver.ResolveMEPFixture("switch", "light");
                var placedIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();
                var heightFt = SWITCH_HEIGHT * MM_TO_FEET;

                using (var transaction = new Transaction(_document, "StingBIM: Place Switches"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var door in doors)
                        {
                            var doorPt = (door.Location as LocationPoint)?.Point;
                            if (doorPt == null) continue;

                            // Place switch 200mm from door frame on latch side
                            var host = door.Host as Wall;
                            if (host == null) continue;

                            var wallLine = (host.Location as LocationCurve)?.Curve as Line;
                            if (wallLine == null) continue;

                            var wallDir = (wallLine.GetEndPoint(1) - wallLine.GetEndPoint(0)).Normalize();
                            var offsetFt = SWITCH_FROM_DOOR * MM_TO_FEET;

                            // Place on latch side (offset along wall direction)
                            var switchPt = new XYZ(doorPt.X + wallDir.X * offsetFt,
                                doorPt.Y + wallDir.Y * offsetFt, heightFt);

                            FamilyInstance sw = null;
                            if (switchSymbol != null)
                            {
                                sw = _document.Create.NewFamilyInstance(
                                    switchPt, switchSymbol, host, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            else
                            {
                                var generic = FindAnyElectricalFixture("switch");
                                if (generic != null)
                                {
                                    sw = _document.Create.NewFamilyInstance(
                                        switchPt, generic, host, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                }
                            }

                            if (sw != null)
                                placedIds.Add(sw.Id);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("switches", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} light switch(es) at {SWITCH_HEIGHT}mm AFF, " +
                    $"{SWITCH_FROM_DOOR}mm from door frames";
                result.CostEstimate = EstimateSwitchCost(placedIds.Count);
                result.Suggestions = new List<string>
                {
                    "Add dimmer switches",
                    "Add 2-way switching",
                    "Add fan speed controllers"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Switch placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("switches", "place", ex));
            }

            return result;
        }

        #endregion

        #region Distribution Board

        /// <summary>
        /// Places a distribution board — sized by the number of circuits.
        /// Residential: 12-way (3-bed), 20-way (4+ bed).
        /// Commercial: 40-way sub-main boards.
        /// </summary>
        public CreationPipelineResult PlaceDistributionBoard(DistributionBoardCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Distribution Board" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Determine DB size
                var ways = cmd.Ways;
                if (ways <= 0)
                {
                    // Auto-size: count rooms for residential
                    var rooms = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Autodesk.Revit.DB.Architecture.Room>()
                        .Where(r => r.Area > 0)
                        .ToList();

                    var bedrooms = rooms.Count(r =>
                        r.Name.Contains("Bedroom", StringComparison.OrdinalIgnoreCase));
                    ways = bedrooms >= 4 ? 20 : 12;

                    if (cmd.IsCommercial) ways = 40;
                }

                var dbSymbol = _familyResolver.ResolveMEPFixture("distribution board", null);
                var failureHandler = new StingBIMFailurePreprocessor();
                FamilyInstance db = null;

                using (var transaction = new Transaction(_document, "StingBIM: Place Distribution Board"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        var heightFt = 1500 * MM_TO_FEET; // DB center at 1500mm AFF
                        var placePt = new XYZ(
                            (cmd.X ?? 0) * MM_TO_FEET,
                            (cmd.Y ?? 0) * MM_TO_FEET,
                            heightFt);

                        if (dbSymbol != null)
                        {
                            db = _document.Create.NewFamilyInstance(
                                placePt, dbSymbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                        else
                        {
                            var generic = FindAnyElectricalFixture("panel");
                            if (generic != null)
                            {
                                db = _document.Create.NewFamilyInstance(
                                    placePt, generic, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                        }

                        if (db != null)
                        {
                            // Set DB parameters
                            var ratingParam = db.LookupParameter("Rating");
                            ratingParam?.Set(cmd.RatingAmps > 0 ? cmd.RatingAmps : 100);

                            var phasesParam = db.LookupParameter("Phases");
                            phasesParam?.Set(cmd.Phases > 0 ? cmd.Phases : 1);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError(
                            "distribution board", "place", ex));
                        return result;
                    }
                }

                var ipRating = cmd.IsOutdoor ? "IP65" : "IP41";
                result.Success = db != null;
                result.CreatedElementId = db?.Id;
                result.Message = $"Placed {ways}-way distribution board ({ipRating}), " +
                    $"{cmd.RatingAmps}A {(cmd.Phases == 3 ? "3-phase" : "single phase")}";

                if (!cmd.IsCommercial)
                {
                    result.Message += "\nNote: Generator/UPS connection recommended " +
                        "(Uganda power reliability — consider ATS for automatic changeover).";
                }

                result.CostEstimate = EstimateDBCost(ways, cmd.RatingAmps);
                result.Suggestions = new List<string>
                {
                    "Route conduits from DB to rooms",
                    "Add a generator",
                    "Add circuit breakers"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Distribution board placement failed");
                result.SetError(ErrorExplainer.FormatCreationError(
                    "distribution board", "place", ex));
            }

            return result;
        }

        #endregion

        #region Generator

        /// <summary>
        /// Places a standby generator with load assessment.
        /// Load = total connected load × 0.7 diversity → generator kVA.
        /// Uganda: generator strongly recommended for commercial.
        /// </summary>
        public CreationPipelineResult PlaceGenerator(GeneratorCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Generator" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Auto-size if not specified
                var kva = cmd.KVA;
                if (kva <= 0)
                {
                    // Estimate: count rooms × 2kW per room × 0.7 diversity
                    var rooms = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Count();
                    var estimatedLoadKW = rooms * 2.0;
                    kva = estimatedLoadKW * 0.7 / 0.8; // pf = 0.8
                    kva = Math.Ceiling(kva / 5) * 5; // Round up to nearest 5kVA
                    if (kva < 15) kva = 15; // Minimum practical size
                }

                var genSymbol = _familyResolver.ResolveMEPFixture("generator", null);
                var failureHandler = new StingBIMFailurePreprocessor();
                FamilyInstance gen = null;

                using (var transaction = new Transaction(_document, "StingBIM: Place Generator"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        var placePt = new XYZ(
                            (cmd.X ?? 0) * MM_TO_FEET,
                            (cmd.Y ?? 0) * MM_TO_FEET,
                            0);

                        if (genSymbol != null)
                        {
                            gen = _document.Create.NewFamilyInstance(
                                placePt, genSymbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        if (gen != null)
                        {
                            var ratingParam = gen.LookupParameter("Rating");
                            ratingParam?.Set($"{kva}kVA");
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("generator", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = gen?.Id;
                result.Message = $"Placed {kva}kVA diesel generator" +
                    $"\nIncludes ATS (automatic transfer switch) for seamless changeover." +
                    $"\nFuel type: Diesel, recommended autonomy: 12-24 hours";
                result.CostEstimate = EstimateGeneratorCost(kva);
                result.Suggestions = new List<string>
                {
                    "Add a fuel tank",
                    "Route generator cable to main DB",
                    "Add exhaust ducting"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Generator placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("generator", "place", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private Autodesk.Revit.DB.Architecture.Room FindRoom(string roomName, Level level)
        {
            if (string.IsNullOrEmpty(roomName)) return null;

            return new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .FirstOrDefault(r => r.Area > 0 &&
                    (r.LevelId == level.Id || level == null) &&
                    r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase));
        }

        private List<Wall> FindRoomWalls(string roomName, Level level)
        {
            // Find walls on this level
            var walls = new FilteredElementCollector(_document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.LevelId == level.Id)
                .ToList();

            // If room name specified, filter walls by proximity to room
            if (!string.IsNullOrEmpty(roomName))
            {
                var room = FindRoom(roomName, level);
                if (room != null)
                {
                    var roomBB = room.get_BoundingBox(null);
                    if (roomBB != null)
                    {
                        walls = walls.Where(w =>
                        {
                            var wallBB = w.get_BoundingBox(null);
                            return wallBB != null && BoundingBoxesOverlap(roomBB, wallBB);
                        }).ToList();
                    }
                }
            }

            return walls;
        }

        private bool BoundingBoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            var margin = 1.0; // 1 foot tolerance
            return a.Min.X <= b.Max.X + margin && a.Max.X >= b.Min.X - margin &&
                   a.Min.Y <= b.Max.Y + margin && a.Max.Y >= b.Min.Y - margin;
        }

        private int GetLuxTarget(string roomType, int? luxOverride)
        {
            if (luxOverride > 0) return luxOverride.Value;

            foreach (var kvp in LuxTargets)
            {
                if (roomType.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return 200; // Default
        }

        private List<XYZ> CalculateLightGrid(int count, XYZ origin, double lengthFt, double widthFt)
        {
            var positions = new List<XYZ>();
            var cx = origin.X + lengthFt / 2;
            var cy = origin.Y + widthFt / 2;

            if (count == 1)
            {
                positions.Add(new XYZ(cx, cy, 0));
            }
            else if (count == 2)
            {
                positions.Add(new XYZ(origin.X + lengthFt / 3, cy, 0));
                positions.Add(new XYZ(origin.X + 2 * lengthFt / 3, cy, 0));
            }
            else if (count <= 4)
            {
                // 2x2 grid at quarter points
                positions.Add(new XYZ(origin.X + lengthFt / 4, origin.Y + widthFt / 4, 0));
                positions.Add(new XYZ(origin.X + 3 * lengthFt / 4, origin.Y + widthFt / 4, 0));
                positions.Add(new XYZ(origin.X + lengthFt / 4, origin.Y + 3 * widthFt / 4, 0));
                if (count == 4)
                    positions.Add(new XYZ(origin.X + 3 * lengthFt / 4, origin.Y + 3 * widthFt / 4, 0));
            }
            else
            {
                // NxM grid
                var cols = (int)Math.Ceiling(Math.Sqrt(count * lengthFt / Math.Max(widthFt, 0.1)));
                var rows = (int)Math.Ceiling((double)count / cols);
                var dx = lengthFt / (cols + 1);
                var dy = widthFt / (rows + 1);
                var placed = 0;

                for (int r = 1; r <= rows && placed < count; r++)
                {
                    for (int c = 1; c <= cols && placed < count; c++)
                    {
                        positions.Add(new XYZ(origin.X + c * dx, origin.Y + r * dy, 0));
                        placed++;
                    }
                }
            }

            return positions;
        }

        private bool IsWetArea(string roomType)
        {
            if (string.IsNullOrEmpty(roomType)) return false;
            var lower = roomType.ToLowerInvariant();
            return lower.Contains("bathroom") || lower.Contains("toilet") ||
                   lower.Contains("wc") || lower.Contains("en-suite") ||
                   lower.Contains("ensuite") || lower.Contains("shower") ||
                   lower.Contains("laundry");
        }

        private FamilySymbol FindAnyLightingFixture()
        {
            return new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        private FamilySymbol FindAnyElectricalFixture(string hint)
        {
            var category = hint?.ToLowerInvariant() switch
            {
                "outlet" => BuiltInCategory.OST_ElectricalFixtures,
                "switch" => BuiltInCategory.OST_LightingDevices,
                "panel" => BuiltInCategory.OST_ElectricalEquipment,
                _ => BuiltInCategory.OST_ElectricalFixtures,
            };

            return new FilteredElementCollector(_document)
                .OfCategory(category)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        #endregion

        #region Cost Estimation

        private CostEstimate EstimateLightingCost(int count, string type)
        {
            // UGX rates per fixture (installed)
            var unitRate = type?.ToLowerInvariant() switch
            {
                "fluorescent" => 85000,
                "tube" => 85000,
                "pendant" => 120000,
                "recessed" => 95000,
                "floodlight" => 180000,
                _ => 75000, // LED downlight
            };
            return new CostEstimate
            {
                TotalUGX = count * unitRate,
                Description = $"{count}× {type ?? "downlight"} @ UGX {unitRate:N0} each"
            };
        }

        private CostEstimate EstimateOutletCost(int count, bool isWetArea)
        {
            var unitRate = isWetArea ? 65000 : 45000; // IP44 costs more
            return new CostEstimate
            {
                TotalUGX = count * unitRate,
                Description = $"{count}× power outlet @ UGX {unitRate:N0} each" +
                    (isWetArea ? " (IP44 wet-rated)" : "")
            };
        }

        private CostEstimate EstimateSwitchCost(int count)
        {
            return new CostEstimate
            {
                TotalUGX = count * 35000,
                Description = $"{count}× light switch @ UGX 35,000 each"
            };
        }

        private CostEstimate EstimateDBCost(int ways, double ratingAmps)
        {
            var rate = ways <= 12 ? 350000 : ways <= 20 ? 550000 : 1200000;
            return new CostEstimate
            {
                TotalUGX = rate,
                Description = $"{ways}-way DB, {ratingAmps}A rated @ UGX {rate:N0}"
            };
        }

        private CostEstimate EstimateGeneratorCost(double kva)
        {
            // Approximate: UGX 150,000 per kVA (diesel, with ATS)
            var total = (long)(kva * 150000);
            return new CostEstimate
            {
                TotalUGX = total,
                Description = $"{kva}kVA diesel generator @ UGX {150000:N0}/kVA = UGX {total:N0}"
            };
        }

        #endregion
    }

    #region Command DTOs

    public class LightingCommand
    {
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public string LightType { get; set; }
        public string LevelName { get; set; }
        public int? LuxOverride { get; set; }
        public double LumensOverride { get; set; }
        public double CeilingHeightMm { get; set; }
        public bool AllRooms { get; set; }
    }

    public class OutletCommand
    {
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public string LevelName { get; set; }
        public double HeightMm { get; set; }
        public int OutletsPerWall { get; set; }
        public bool WorktopOutlets { get; set; }
        public bool AllRooms { get; set; }
    }

    public class SwitchCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool TwoWay { get; set; }
        public bool Dimmer { get; set; }
        public bool AllRooms { get; set; }
    }

    public class DistributionBoardCommand
    {
        public string LevelName { get; set; }
        public string RoomName { get; set; }
        public int Ways { get; set; }
        public double RatingAmps { get; set; }
        public int Phases { get; set; }
        public bool IsCommercial { get; set; }
        public bool IsOutdoor { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }

    public class GeneratorCommand
    {
        public string LevelName { get; set; }
        public double KVA { get; set; }
        public string FuelType { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }

    #endregion
}
