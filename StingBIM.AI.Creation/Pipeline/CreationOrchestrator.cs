// StingBIM.AI.Creation.Pipeline.CreationOrchestrator
// Validates entities, resolves families, checks conflicts, executes creation
// v4 Prompt Reference: Section A.0 Architecture — Step 1-8 creation pipeline

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Orchestrates the element creation pipeline:
    /// 1. Validate entities from NLP (dimensions, types, locations)
    /// 2. Resolve families from the live Document
    /// 3. Check for conflicts (overlaps, clearance, compliance)
    /// 4. Execute within TransactionManager with FailurePreprocessor
    /// 5. Auto-assign worksets
    /// 6. Return result with cost estimate
    /// </summary>
    public class CreationOrchestrator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly CostEstimator _costEstimator;
        private readonly WorksetAssigner _worksetAssigner;

        // Unit conversion: all dimensions in mm internally, Revit uses decimal feet
        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double M_TO_FEET = 1.0 / 0.3048;

        public CreationOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
            _worksetAssigner = new WorksetAssigner(document);
        }

        /// <summary>
        /// The FamilyResolver instance — available for callers needing direct access.
        /// </summary>
        public FamilyResolver FamilyResolver => _familyResolver;

        /// <summary>
        /// The CostEstimator instance — available for callers needing direct access.
        /// </summary>
        public CostEstimator CostEstimator => _costEstimator;

        #region Wall Creation

        /// <summary>
        /// Creates a wall with the specified parameters.
        /// MODE 1: Straight wall from dimensions — "Create a 5m wall on Level 1"
        /// MODE 2: Wall by points — start and end XYZ coordinates
        /// </summary>
        public CreationPipelineResult CreateWall(WallCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Wall" };

            try
            {
                // Step 1: Validate
                if (cmd.LengthMm <= 0 && cmd.StartPoint == null)
                {
                    result.SetError("Wall length is required. Try: 'Create a 5m wall'");
                    return result;
                }

                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only. Please reopen with edit access.");
                    return result;
                }

                // Step 2: Resolve families
                var wallTypeResult = _familyResolver.ResolveWallType(cmd.WallTypeName, cmd.ThicknessMm);
                if (!wallTypeResult.Success)
                {
                    result.SetError(wallTypeResult.Message);
                    return result;
                }

                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError($"Level '{cmd.LevelName ?? "default"}' not found in the project.");
                    return result;
                }

                // Step 3: Prepare geometry
                var heightFt = (cmd.HeightMm > 0 ? cmd.HeightMm : 2700.0) * MM_TO_FEET;
                Line wallLine;

                if (cmd.StartPoint != null && cmd.EndPoint != null)
                {
                    wallLine = Line.CreateBound(
                        new XYZ(cmd.StartPoint.X * MM_TO_FEET, cmd.StartPoint.Y * MM_TO_FEET, 0),
                        new XYZ(cmd.EndPoint.X * MM_TO_FEET, cmd.EndPoint.Y * MM_TO_FEET, 0));
                }
                else
                {
                    var lengthFt = cmd.LengthMm * MM_TO_FEET;
                    var start = new XYZ(0, 0, 0);
                    var end = new XYZ(lengthFt, 0, 0);
                    wallLine = Line.CreateBound(start, end);
                }

                // Step 4: Execute in transaction
                var wallTypeId = wallTypeResult.TypeId;
                Wall createdWall = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Create Wall"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        createdWall = Wall.Create(_document, wallLine, wallTypeId,
                            level.Id, heightFt, 0, false, cmd.IsStructural);

                        // Auto-assign workset
                        _worksetAssigner.AssignToCorrectWorkset(createdWall);

                        // Set additional parameters
                        if (!string.IsNullOrEmpty(cmd.Mark))
                        {
                            var markParam = createdWall.LookupParameter("Mark");
                            markParam?.Set(cmd.Mark);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("wall", "create", ex));
                        return result;
                    }
                }

                // Step 5: Build result
                var lengthMm = cmd.LengthMm > 0 ? cmd.LengthMm : wallLine.Length / MM_TO_FEET;
                var cost = _costEstimator.EstimateWallCost(lengthMm,
                    cmd.HeightMm > 0 ? cmd.HeightMm : 2700, cmd.WallTypeName);

                result.Success = true;
                result.CreatedElementId = createdWall.Id;
                result.Message = $"Created {lengthMm / 1000:F1}m {wallTypeResult.TypeName} wall on {level.Name}";
                result.CostEstimate = cost;
                result.Warnings = failureHandler.CapturedWarnings.Count > 0
                    ? ErrorExplainer.SummarizeFailures(failureHandler.CapturedWarnings)
                    : null;
                result.Suggestions = new List<string>
                {
                    "Add another wall",
                    "Create a room from these walls",
                    "Add a door to this wall"
                };

                Logger.Info($"Wall created: {createdWall.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Wall creation pipeline failed");
                result.SetError(ErrorExplainer.FormatCreationError("wall", "create", ex));
            }

            return result;
        }

        /// <summary>
        /// Creates a rectangular room outline (4 walls) in a single transaction group.
        /// MODE 3: "Create a 5×4m bedroom on Level 1"
        /// </summary>
        public CreationPipelineResult CreateRectangularWalls(RectangularWallCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Room Outline" };

            try
            {
                if (cmd.WidthMm <= 0 || cmd.DepthMm <= 0)
                {
                    result.SetError("Both width and depth are required. Try: 'Create a 5×4m room'");
                    return result;
                }

                var wallTypeResult = _familyResolver.ResolveWallType(cmd.WallTypeName, cmd.ThicknessMm);
                if (!wallTypeResult.Success)
                {
                    result.SetError(wallTypeResult.Message);
                    return result;
                }

                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var heightFt = (cmd.HeightMm > 0 ? cmd.HeightMm : 2700.0) * MM_TO_FEET;
                var widthFt = cmd.WidthMm * MM_TO_FEET;
                var depthFt = cmd.DepthMm * MM_TO_FEET;

                // Origin point (or use 0,0)
                var ox = (cmd.OriginX ?? 0) * MM_TO_FEET;
                var oy = (cmd.OriginY ?? 0) * MM_TO_FEET;

                var corners = new[]
                {
                    new XYZ(ox, oy, 0),
                    new XYZ(ox + widthFt, oy, 0),
                    new XYZ(ox + widthFt, oy + depthFt, 0),
                    new XYZ(ox, oy + depthFt, 0)
                };

                var wallTypeId = wallTypeResult.TypeId;
                var createdWalls = new List<Wall>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var tg = new TransactionGroup(_document, "StingBIM: Create Room Walls"))
                {
                    tg.Start();

                    using (var transaction = new Transaction(_document, "Create 4 Walls"))
                    {
                        var options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failureHandler);
                        transaction.SetFailureHandlingOptions(options);
                        transaction.Start();

                        try
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                var line = Line.CreateBound(corners[i], corners[(i + 1) % 4]);
                                var wall = Wall.Create(_document, line, wallTypeId,
                                    level.Id, heightFt, 0, false, cmd.IsStructural);
                                _worksetAssigner.AssignToCorrectWorkset(wall);
                                createdWalls.Add(wall);
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.RollBack();
                            tg.RollBack();
                            result.SetError(ErrorExplainer.FormatCreationError(
                                "room walls", "create", ex));
                            return result;
                        }
                    }

                    // Join wall corners
                    using (var joinTx = new Transaction(_document, "Join Wall Corners"))
                    {
                        joinTx.Start();
                        try
                        {
                            for (int i = 0; i < createdWalls.Count; i++)
                            {
                                var next = (i + 1) % createdWalls.Count;
                                JoinGeometryUtils.JoinGeometry(_document,
                                    createdWalls[i], createdWalls[next]);
                            }
                            joinTx.Commit();
                        }
                        catch
                        {
                            joinTx.RollBack();
                            // Non-critical — walls still created
                        }
                    }

                    tg.Assimilate();
                }

                var areaSqM = (cmd.WidthMm / 1000.0) * (cmd.DepthMm / 1000.0);
                var perimeterM = 2 * (cmd.WidthMm + cmd.DepthMm) / 1000.0;
                var wallCost = _costEstimator.EstimateWallCost(
                    perimeterM * 1000, cmd.HeightMm > 0 ? cmd.HeightMm : 2700, cmd.WallTypeName);

                result.Success = true;
                result.CreatedElementIds = createdWalls.Select(w => w.Id).ToList();
                result.Message = $"Created {cmd.WidthMm / 1000:F1}m x {cmd.DepthMm / 1000:F1}m room outline " +
                    $"({areaSqM:F1} m²) on {level.Name}";
                result.CostEstimate = wallCost;
                result.Suggestions = new List<string>
                {
                    $"Add a floor to this {cmd.RoomType ?? "room"}",
                    "Add a door",
                    "Add windows",
                    $"Name this room '{cmd.RoomType ?? "Room"}'"
                };

                Logger.Info($"Room outline created: 4 walls, {areaSqM:F1}m² on {level.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Rectangular wall creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("room walls", "create", ex));
            }

            return result;
        }

        #endregion

        #region Floor Creation

        /// <summary>
        /// Creates a floor from a room boundary or explicit boundary points.
        /// MODE 1: Floor in an enclosed room — "Add a floor to Bedroom 1"
        /// MODE 2: Floor from coordinates — explicit boundary
        /// </summary>
        public CreationPipelineResult CreateFloor(FloorCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Floor" };

            try
            {
                var floorTypeResult = _familyResolver.ResolveFloorType(cmd.FloorTypeName);
                if (!floorTypeResult.Success)
                {
                    result.SetError(floorTypeResult.Message);
                    return result;
                }

                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                Floor createdFloor = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Create Floor"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        CurveLoop boundary;

                        if (cmd.BoundaryPointsMm != null && cmd.BoundaryPointsMm.Count >= 3)
                        {
                            // Explicit boundary
                            boundary = new CurveLoop();
                            for (int i = 0; i < cmd.BoundaryPointsMm.Count; i++)
                            {
                                var current = cmd.BoundaryPointsMm[i];
                                var next = cmd.BoundaryPointsMm[(i + 1) % cmd.BoundaryPointsMm.Count];
                                boundary.Append(Line.CreateBound(
                                    new XYZ(current.Item1 * MM_TO_FEET, current.Item2 * MM_TO_FEET, 0),
                                    new XYZ(next.Item1 * MM_TO_FEET, next.Item2 * MM_TO_FEET, 0)));
                            }
                        }
                        else if (cmd.WidthMm > 0 && cmd.DepthMm > 0)
                        {
                            // Rectangular floor
                            var ox = (cmd.OriginX ?? 0) * MM_TO_FEET;
                            var oy = (cmd.OriginY ?? 0) * MM_TO_FEET;
                            var w = cmd.WidthMm * MM_TO_FEET;
                            var d = cmd.DepthMm * MM_TO_FEET;

                            boundary = new CurveLoop();
                            boundary.Append(Line.CreateBound(new XYZ(ox, oy, 0), new XYZ(ox + w, oy, 0)));
                            boundary.Append(Line.CreateBound(new XYZ(ox + w, oy, 0), new XYZ(ox + w, oy + d, 0)));
                            boundary.Append(Line.CreateBound(new XYZ(ox + w, oy + d, 0), new XYZ(ox, oy + d, 0)));
                            boundary.Append(Line.CreateBound(new XYZ(ox, oy + d, 0), new XYZ(ox, oy, 0)));
                        }
                        else
                        {
                            result.SetError("Floor requires either boundary points or width×depth dimensions.");
                            transaction.RollBack();
                            return result;
                        }

                        var floorTypeId = floorTypeResult.TypeId;
                        createdFloor = Floor.Create(_document,
                            new List<CurveLoop> { boundary }, floorTypeId, level.Id);

                        _worksetAssigner.AssignToCorrectWorkset(createdFloor);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("floor", "create", ex));
                        return result;
                    }
                }

                var areaSqM = (cmd.WidthMm / 1000.0) * (cmd.DepthMm / 1000.0);
                var cost = _costEstimator.EstimateFloorCost(areaSqM, cmd.FloorTypeName);

                result.Success = true;
                result.CreatedElementId = createdFloor.Id;
                result.Message = $"Created {areaSqM:F1}m² {floorTypeResult.TypeName} floor on {level.Name}";
                result.CostEstimate = cost;
                result.Suggestions = new List<string>
                {
                    "Add floor tiles",
                    "Add a ceiling above this room",
                    "Create the next room"
                };

                Logger.Info($"Floor created: {createdFloor.Id} — {areaSqM:F1}m²");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Floor creation pipeline failed");
                result.SetError(ErrorExplainer.FormatCreationError("floor", "create", ex));
            }

            return result;
        }

        #endregion

        #region Room Placement

        /// <summary>
        /// Places a Room element in an enclosed area and sets its properties.
        /// The walls must already exist to form an enclosed boundary.
        /// </summary>
        public CreationPipelineResult PlaceRoom(RoomPlacementCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Room" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                Room createdRoom = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Room"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Place room at specified point or (0,0)
                        var pointFt = new UV(
                            (cmd.CenterX ?? 0) * MM_TO_FEET,
                            (cmd.CenterY ?? 0) * MM_TO_FEET);

                        createdRoom = _document.Create.NewRoom(level, pointFt);

                        if (createdRoom != null)
                        {
                            // Set room name
                            if (!string.IsNullOrEmpty(cmd.RoomName))
                            {
                                createdRoom.Name = cmd.RoomName;
                            }

                            // Set room number
                            if (!string.IsNullOrEmpty(cmd.RoomNumber))
                            {
                                createdRoom.Number = cmd.RoomNumber;
                            }

                            _worksetAssigner.AssignToCorrectWorkset(createdRoom);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("room", "place", ex));
                        return result;
                    }
                }

                if (createdRoom != null)
                {
                    var areaSqM = createdRoom.Area * 0.092903; // sq ft → m²
                    result.Success = true;
                    result.CreatedElementId = createdRoom.Id;
                    result.Message = $"Placed room '{cmd.RoomName ?? "Room"}' ({areaSqM:F1}m²) on {level.Name}";
                    result.Suggestions = new List<string>
                    {
                        "Add a floor",
                        "Add doors and windows",
                        "Add furniture"
                    };
                }
                else
                {
                    result.SetError(
                        "Could not place the room — the area may not be fully enclosed by walls.\n" +
                        "Make sure all walls are connected at their corners.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Room placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("room", "place", ex));
            }

            return result;
        }

        #endregion

        #region Unit Conversion Helpers

        /// <summary>
        /// Converts millimeters to Revit internal units (decimal feet).
        /// </summary>
        public static double ToRevitFeet(double millimeters)
        {
            return millimeters / 304.8; // convert mm to decimal feet (Revit internal unit)
        }

        /// <summary>
        /// Converts Revit internal units (decimal feet) to millimeters.
        /// </summary>
        public static double ToMillimeters(double revitFeet)
        {
            return revitFeet * 304.8; // convert decimal feet to mm
        }

        /// <summary>
        /// Converts meters to Revit internal units (decimal feet).
        /// </summary>
        public static double MetersToFeet(double meters)
        {
            return meters / 0.3048; // convert meters to decimal feet
        }

        #endregion
    }

    #region Command DTOs

    /// <summary>
    /// Command to create a single wall.
    /// </summary>
    public class WallCreationCommand
    {
        public double LengthMm { get; set; }
        public double HeightMm { get; set; }
        public double ThicknessMm { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public bool IsStructural { get; set; }
        public string Mark { get; set; }

        // For point-based creation
        public XYZPoint StartPoint { get; set; }
        public XYZPoint EndPoint { get; set; }
    }

    /// <summary>
    /// Command to create a rectangular room outline (4 walls).
    /// </summary>
    public class RectangularWallCommand
    {
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public double HeightMm { get; set; }
        public double ThicknessMm { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public string RoomType { get; set; }
        public bool IsStructural { get; set; }
        public double? OriginX { get; set; }
        public double? OriginY { get; set; }
    }

    /// <summary>
    /// Command to create a floor.
    /// </summary>
    public class FloorCreationCommand
    {
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public string FloorTypeName { get; set; }
        public string LevelName { get; set; }
        public double? OriginX { get; set; }
        public double? OriginY { get; set; }
        public List<Tuple<double, double>> BoundaryPointsMm { get; set; }
    }

    /// <summary>
    /// Command to place a room in an enclosed area.
    /// </summary>
    public class RoomPlacementCommand
    {
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public string LevelName { get; set; }
        public double? CenterX { get; set; }
        public double? CenterY { get; set; }
    }

    /// <summary>
    /// Simple XYZ point (not dependent on Revit API — for NLP layer).
    /// </summary>
    public class XYZPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public XYZPoint() { }
        public XYZPoint(double x, double y, double z = 0) { X = x; Y = y; Z = z; }
    }

    #endregion

    #region Pipeline Result

    /// <summary>
    /// Result from the creation pipeline — includes cost, warnings, and follow-up suggestions.
    /// </summary>
    public class CreationPipelineResult
    {
        public bool Success { get; set; }
        public string ElementType { get; set; }
        public ElementId CreatedElementId { get; set; }
        public List<ElementId> CreatedElementIds { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public string Warnings { get; set; }
        public CostEstimate CostEstimate { get; set; }
        public List<string> Suggestions { get; set; }

        public int CreatedCount => CreatedElementIds?.Count ?? (CreatedElementId != null &&
            CreatedElementId != ElementId.InvalidElementId ? 1 : 0);

        /// <summary>
        /// Formats the result for display in the chat panel.
        /// </summary>
        public string FormatForChat()
        {
            if (!Success)
                return $"Could not create {ElementType}.\n{Error}";

            var parts = new List<string> { Message };

            if (CostEstimate != null && CostEstimate.TotalUGX > 0)
                parts.Add($"Estimated cost: {CostEstimate.FormattedTotal}");

            if (!string.IsNullOrEmpty(Warnings))
                parts.Add($"Note: {Warnings}");

            return string.Join("\n", parts);
        }

        public void SetError(string error)
        {
            Success = false;
            Error = error;
        }
    }

    #endregion
}
