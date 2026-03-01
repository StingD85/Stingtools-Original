// StingBIM.AI.Creation.Elements.DoorWindowPlacer
// Places doors and windows in walls with standards-compliant sizing
// v4 Prompt Reference: Section A.1.6 — DoorWindowPlacer (4 door modes + 4 window modes)

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Places doors and windows in walls with intelligent family resolution,
    /// Uganda/East Africa size standards, and auto-parameter assignment.
    ///
    /// Door Modes:
    ///   1. Door in specific wall/room side — "Add a door to the south wall of Bedroom 1"
    ///   2. Door between two rooms — "Add a door connecting the kitchen and the dining room"
    ///   3. External/entrance door — "Add the main entrance door on the north façade"
    ///   4. Specialty doors — sliding, bifold, garage, fire, security
    ///
    /// Window Modes:
    ///   1. Window in specific wall — "Add a window in the north wall of the bedroom"
    ///   2. Windows by room type standard — "Add standard windows to all bedrooms"
    ///   3. High window (bathrooms) — "Add a high-level window to the bathroom"
    ///   4. Full-height window — "Add floor-to-ceiling windows on the east wall"
    /// </summary>
    public class DoorWindowPlacer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Uganda/East Africa door size standards
        private static readonly Dictionary<string, (double WidthMm, double HeightMm)> DoorSizeStandards =
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
            {
                ["external"] = (900, 2100),
                ["main entrance"] = (1000, 2100),
                ["internal"] = (800, 2100),
                ["bedroom"] = (800, 2100),
                ["bathroom"] = (700, 2100),
                ["wc"] = (700, 2100),
                ["accessible"] = (900, 2100),
                ["double"] = (1800, 2100),
                ["sliding"] = (1800, 2100),
                ["garage"] = (2400, 2400),
                ["fire"] = (900, 2100),
                ["security"] = (900, 2100),
            };

        // Window sill height standards
        private static readonly Dictionary<string, double> SillHeightMm =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["living room"] = 900,
                ["bedroom"] = 900,
                ["kitchen"] = 900,
                ["dining"] = 900,
                ["office"] = 900,
                ["bathroom"] = 1500,
                ["wc"] = 1500,
                ["stairwell"] = 1200,
                ["corridor"] = 900,
                ["default"] = 900,
                ["full-height"] = 0,
                ["high"] = 1500,
            };

        // Door mark counter per level
        private readonly Dictionary<string, int> _doorSequence = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _windowSequence = new Dictionary<string, int>();

        public DoorWindowPlacer(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        #region Door Placement

        /// <summary>
        /// Places a door in a wall at the specified position.
        /// Mode 1: Door in specific wall (wall ID known).
        /// </summary>
        public CreationPipelineResult PlaceDoorInWall(DoorPlacementCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Door" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                // Resolve host wall
                Wall hostWall = null;
                if (cmd.HostWallId != null)
                {
                    hostWall = _document.GetElement(cmd.HostWallId) as Wall;
                }
                else if (!string.IsNullOrEmpty(cmd.RoomName) && !string.IsNullOrEmpty(cmd.WallSide))
                {
                    hostWall = FindWallByRoomSide(cmd.RoomName, cmd.WallSide);
                }

                if (hostWall == null)
                {
                    result.SetError("Could not find the target wall. Please specify which wall to place the door in.");
                    return result;
                }

                // Get door size from standards
                var doorSize = GetDoorSize(cmd.DoorType, cmd.WidthMm, cmd.HeightMm);

                // Check wall is long enough
                var wallLength = hostWall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
                var wallLengthMm = wallLength / MM_TO_FEET;
                if (wallLengthMm < doorSize.WidthMm + 200)
                {
                    result.SetError($"Wall is too short ({wallLengthMm / 1000:F1}m) for a " +
                        $"{doorSize.WidthMm}mm door. Minimum wall length: {(doorSize.WidthMm + 200) / 1000:F1}m.");
                    return result;
                }

                // Resolve door family
                var familyResult = _familyResolver.ResolveFamilySymbol(
                    BuiltInCategory.OST_Doors,
                    cmd.DoorType,
                    doorSize.WidthMm,
                    doorSize.HeightMm);

                if (!familyResult.Success)
                {
                    result.SetError($"No suitable door family found. {familyResult.Message}");
                    return result;
                }

                var symbol = familyResult.ResolvedType as FamilySymbol;
                if (symbol == null)
                {
                    result.SetError("Resolved element is not a valid door family.");
                    return result;
                }

                // Calculate placement point
                var placementPoint = CalculateDoorPlacementPoint(hostWall, cmd.PositionAlongWall);

                // Create door
                FamilyInstance door = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Door"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();

                        var level = _familyResolver.ResolveLevel(cmd.LevelName);
                        door = _document.Create.NewFamilyInstance(
                            placementPoint, symbol, hostWall, level,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                        if (door != null)
                        {
                            // Set mark
                            var mark = GenerateDoorMark(level?.Name ?? "L0");
                            var markParam = door.LookupParameter("Mark");
                            markParam?.Set(mark);

                            // Set comments
                            var commentsParam = door.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(door);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("door", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = door?.Id;
                result.Message = $"Placed {doorSize.WidthMm}×{doorSize.HeightMm}mm " +
                    $"{cmd.DoorType ?? "single"} door in wall";
                result.Suggestions = new List<string>
                {
                    "Add a window",
                    "Add another door",
                    "Create the next room"
                };

                Logger.Info($"Door placed: {door?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Door placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("door", "place", ex));
            }

            return result;
        }

        #endregion

        #region Window Placement

        /// <summary>
        /// Places a window in a wall with appropriate sill height.
        /// Mode 1: Window in specific wall.
        /// </summary>
        public CreationPipelineResult PlaceWindowInWall(WindowPlacementCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Window" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                Wall hostWall = null;
                if (cmd.HostWallId != null)
                {
                    hostWall = _document.GetElement(cmd.HostWallId) as Wall;
                }
                else if (!string.IsNullOrEmpty(cmd.RoomName) && !string.IsNullOrEmpty(cmd.WallSide))
                {
                    hostWall = FindWallByRoomSide(cmd.RoomName, cmd.WallSide);
                }

                if (hostWall == null)
                {
                    result.SetError("Could not find the target wall for the window.");
                    return result;
                }

                // Get window size
                var widthMm = cmd.WidthMm > 0 ? cmd.WidthMm : 1200;
                var heightMm = cmd.HeightMm > 0 ? cmd.HeightMm : 1000;

                // Get sill height based on room type
                var sillMm = cmd.SillHeightMm > 0
                    ? cmd.SillHeightMm
                    : GetSillHeight(cmd.RoomType, cmd.WindowMode);

                // Resolve window family
                var familyResult = _familyResolver.ResolveFamilySymbol(
                    BuiltInCategory.OST_Windows,
                    cmd.WindowType,
                    widthMm,
                    heightMm);

                if (!familyResult.Success)
                {
                    result.SetError($"No suitable window family found. {familyResult.Message}");
                    return result;
                }

                var symbol = familyResult.ResolvedType as FamilySymbol;
                if (symbol == null)
                {
                    result.SetError("Resolved element is not a valid window family.");
                    return result;
                }

                // Calculate placement point
                var placementPoint = CalculateWindowPlacementPoint(hostWall, sillMm, cmd.PositionAlongWall);

                FamilyInstance window = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Window"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();

                        var level = _familyResolver.ResolveLevel(cmd.LevelName);
                        window = _document.Create.NewFamilyInstance(
                            placementPoint, symbol, hostWall, level,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                        if (window != null)
                        {
                            // Set sill height
                            var sillParam = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                            sillParam?.Set(sillMm * MM_TO_FEET);

                            var mark = GenerateWindowMark(level?.Name ?? "L0");
                            var markParam = window.LookupParameter("Mark");
                            markParam?.Set(mark);

                            var commentsParam = window.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(window);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("window", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = window?.Id;
                result.Message = $"Placed {widthMm}×{heightMm}mm window at {sillMm}mm sill height";
                result.Suggestions = new List<string>
                {
                    "Add another window",
                    "Add a door",
                    "Check natural light compliance"
                };

                Logger.Info($"Window placed: {window?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Window placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("window", "place", ex));
            }

            return result;
        }

        /// <summary>
        /// Places standard windows in all rooms of a given type on a level.
        /// Mode 2: Windows by room type standard.
        /// </summary>
        public CreationPipelineResult PlaceStandardWindowsByRoomType(string roomType, string levelName = null)
        {
            var result = new CreationPipelineResult { ElementType = "Windows (Batch)" };

            try
            {
                var level = _familyResolver.ResolveLevel(levelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Collect rooms of the specified type on this level
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Level?.Id == level.Id &&
                                r.Area > 0 &&
                                (string.IsNullOrEmpty(roomType) ||
                                 r.Name.IndexOf(roomType, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();

                if (rooms.Count == 0)
                {
                    result.SetError($"No '{roomType}' rooms found on {level.Name}.");
                    return result;
                }

                var totalPlaced = 0;
                var messages = new List<string>();

                foreach (var room in rooms)
                {
                    // Calculate minimum window area: 10% of floor area (UNBS ventilation)
                    var areaSqM = room.Area * 0.092903;
                    var minWindowAreaSqM = areaSqM * 0.10;
                    var windowWidth = Math.Max(1200, Math.Sqrt(minWindowAreaSqM) * 1000);
                    var windowHeight = Math.Max(1000, minWindowAreaSqM * 1000000 / windowWidth);

                    // Find external walls of this room
                    var exteriorWalls = GetRoomExteriorWalls(room);
                    if (exteriorWalls.Count == 0) continue;

                    var wall = exteriorWalls.First();
                    var windowCmd = new WindowPlacementCommand
                    {
                        HostWallId = wall.Id,
                        WidthMm = windowWidth,
                        HeightMm = windowHeight,
                        RoomType = roomType,
                        LevelName = levelName
                    };

                    var windowResult = PlaceWindowInWall(windowCmd);
                    if (windowResult.Success)
                    {
                        totalPlaced++;
                        messages.Add($"  {room.Name}: {windowWidth:F0}×{windowHeight:F0}mm window");
                    }
                }

                result.Success = totalPlaced > 0;
                result.Message = $"Placed {totalPlaced} windows in {rooms.Count} {roomType} rooms:\n" +
                    string.Join("\n", messages);
                result.Suggestions = new List<string>
                {
                    "Add doors to all rooms",
                    "Check natural light compliance",
                    "Review the model"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Batch window placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("windows", "batch place", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private Wall FindWallByRoomSide(string roomName, string side)
        {
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Name.IndexOf(roomName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            r.Area > 0)
                .ToList();

            if (rooms.Count == 0) return null;
            var room = rooms.First();

            // Get direction vector for the specified side
            var direction = GetDirectionVector(side);
            if (direction == null) return null;

            // Get boundary walls and find the one closest to the specified direction
            var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (boundaries == null || boundaries.Count == 0) return null;

            Wall bestWall = null;
            double bestDot = -1;

            foreach (var segList in boundaries)
            {
                foreach (var seg in segList)
                {
                    var wallId = seg.ElementId;
                    if (wallId == ElementId.InvalidElementId) continue;

                    var wall = _document.GetElement(wallId) as Wall;
                    if (wall == null) continue;

                    // Check wall orientation against desired direction
                    var wallOrientation = wall.Orientation;
                    var dot = Math.Abs(wallOrientation.X * direction.Value.X +
                                      wallOrientation.Y * direction.Value.Y);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestWall = wall;
                    }
                }
            }

            return bestWall;
        }

        private XYZ GetDirectionVector(string side)
        {
            var lower = side?.ToLowerInvariant() ?? "";
            if (lower.Contains("north")) return new XYZ(0, 1, 0);
            if (lower.Contains("south")) return new XYZ(0, -1, 0);
            if (lower.Contains("east")) return new XYZ(1, 0, 0);
            if (lower.Contains("west")) return new XYZ(-1, 0, 0);
            return null;
        }

        private (double WidthMm, double HeightMm) GetDoorSize(string doorType, double userWidth, double userHeight)
        {
            var width = userWidth;
            var height = userHeight;

            if (width <= 0 || height <= 0)
            {
                var key = doorType ?? "internal";
                if (DoorSizeStandards.TryGetValue(key, out var standard))
                {
                    if (width <= 0) width = standard.WidthMm;
                    if (height <= 0) height = standard.HeightMm;
                }
                else
                {
                    if (width <= 0) width = 800;
                    if (height <= 0) height = 2100;
                }
            }

            return (width, height);
        }

        private double GetSillHeight(string roomType, string windowMode)
        {
            if (!string.IsNullOrEmpty(windowMode))
            {
                if (SillHeightMm.TryGetValue(windowMode, out var modeSill))
                    return modeSill;
            }

            if (!string.IsNullOrEmpty(roomType))
            {
                if (SillHeightMm.TryGetValue(roomType, out var typeSill))
                    return typeSill;
            }

            return 900; // default
        }

        private XYZ CalculateDoorPlacementPoint(Wall wall, double? positionAlongWall)
        {
            var curve = (wall.Location as LocationCurve)?.Curve;
            if (curve == null) return XYZ.Zero;

            var param = positionAlongWall ?? 0.5; // default: center of wall
            return curve.Evaluate(param, true);
        }

        private XYZ CalculateWindowPlacementPoint(Wall wall, double sillMm, double? positionAlongWall)
        {
            var curve = (wall.Location as LocationCurve)?.Curve;
            if (curve == null) return XYZ.Zero;

            var param = positionAlongWall ?? 0.5;
            var basePoint = curve.Evaluate(param, true);
            return new XYZ(basePoint.X, basePoint.Y, basePoint.Z + sillMm * MM_TO_FEET);
        }

        private List<Wall> GetRoomExteriorWalls(Room room)
        {
            var walls = new List<Wall>();
            var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (boundaries == null) return walls;

            foreach (var segList in boundaries)
            {
                foreach (var seg in segList)
                {
                    var wall = _document.GetElement(seg.ElementId) as Wall;
                    if (wall == null) continue;

                    // Check if wall is exterior (has exterior function or is on building perimeter)
                    var wallFunction = wall.WallType?.Function;
                    if (wallFunction == WallFunction.Exterior)
                    {
                        walls.Add(wall);
                    }
                }
            }

            // If no explicitly exterior walls found, return all boundary walls
            if (walls.Count == 0)
            {
                foreach (var segList in boundaries)
                {
                    foreach (var seg in segList)
                    {
                        var wall = _document.GetElement(seg.ElementId) as Wall;
                        if (wall != null && !walls.Contains(wall))
                            walls.Add(wall);
                    }
                }
            }

            return walls;
        }

        private string GenerateDoorMark(string levelName)
        {
            if (!_doorSequence.TryGetValue(levelName, out var seq))
                seq = 0;
            seq++;
            _doorSequence[levelName] = seq;
            return $"D{levelName.Replace("Level ", "").Replace("level ", "")}{seq:D2}";
        }

        private string GenerateWindowMark(string levelName)
        {
            if (!_windowSequence.TryGetValue(levelName, out var seq))
                seq = 0;
            seq++;
            _windowSequence[levelName] = seq;
            return $"W{levelName.Replace("Level ", "").Replace("level ", "")}{seq:D2}";
        }

        #endregion
    }

    #region Command DTOs

    public class DoorPlacementCommand
    {
        public ElementId HostWallId { get; set; }
        public string RoomName { get; set; }
        public string WallSide { get; set; }
        public string DoorType { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public string LevelName { get; set; }
        public double? PositionAlongWall { get; set; }
    }

    public class WindowPlacementCommand
    {
        public ElementId HostWallId { get; set; }
        public string RoomName { get; set; }
        public string WallSide { get; set; }
        public string WindowType { get; set; }
        public string WindowMode { get; set; }
        public string RoomType { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double SillHeightMm { get; set; }
        public string LevelName { get; set; }
        public double? PositionAlongWall { get; set; }
    }

    #endregion
}
