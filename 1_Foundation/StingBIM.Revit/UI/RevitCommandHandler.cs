// ============================================================================
// StingBIM AI - Revit Command Handler
// IExternalEventHandler for operations that modify the Revit document
// Used by the chat panel to execute creation commands via ExternalEvent
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.Revit.UI
{
    /// <summary>
    /// Handles queued Revit API commands that require document transactions.
    /// Registered as an ExternalEvent at startup, triggered by the chat panel.
    /// </summary>
    internal class RevitCommandHandler : IExternalEventHandler
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentQueue<RevitCommand> _pendingCommands = new();

        /// <summary>Fires when a command completes, delivering the result message.</summary>
        public event Action<string> CommandCompleted;

        /// <summary>Fires when a command fails.</summary>
        public event Action<string> CommandFailed;

        /// <summary>Queue a command for execution on the Revit main thread.</summary>
        public void QueueCommand(RevitCommand command)
        {
            _pendingCommands.Enqueue(command);
        }

        public void Execute(UIApplication app)
        {
            while (_pendingCommands.TryDequeue(out var cmd))
            {
                try
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null)
                    {
                        CommandFailed?.Invoke("No active document.");
                        continue;
                    }

                    var result = ExecuteCommand(doc, cmd);
                    CommandCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to execute command: {cmd.Type}");
                    CommandFailed?.Invoke($"Error: {ex.Message}");
                }
            }
        }

        private string ExecuteCommand(Document doc, RevitCommand cmd)
        {
            switch (cmd.Type)
            {
                case RevitCommandType.CreateWall:
                    return CreateWall(doc, cmd);
                case RevitCommandType.CreateFloor:
                    return CreateFloor(doc, cmd);
                case RevitCommandType.CreateRoom:
                    return CreateRoom(doc, cmd);
                case RevitCommandType.AutoPopulateParameters:
                    return AutoPopulateParameters(doc, cmd);
                default:
                    return $"Unknown command type: {cmd.Type}";
            }
        }

        #region Creation Commands

        private string CreateWall(Document doc, RevitCommand cmd)
        {
            double length = cmd.GetDouble("length", 5.0); // meters
            double height = cmd.GetDouble("height", 3.0); // meters
            string wallTypeName = cmd.GetString("type", null);

            // Convert meters to feet (Revit internal units)
            double lengthFt = length / 0.3048;
            double heightFt = height / 0.3048;

            using (var tx = new Transaction(doc, "StingBIM AI: Create Wall"))
            {
                tx.Start();

                // Get default wall type
                var wallType = GetWallType(doc, wallTypeName);
                if (wallType == null)
                    return "No wall type found in the project. Load a wall family first.";

                // Get the lowest level
                var level = GetLowestLevel(doc);
                if (level == null)
                    return "No levels found in the project. Create a level first.";

                // Create wall along X axis from origin
                var start = new XYZ(0, 0, 0);
                var end = new XYZ(lengthFt, 0, 0);
                var line = Line.CreateBound(start, end);

                var wall = Wall.Create(doc, line, wallType.Id, level.Id, heightFt, 0, false, false);

                tx.Commit();

                var thickness = wallType.Width * 0.3048; // ft to m
                return $"Wall created successfully!\n\n" +
                       $"  Type: {wallType.Name}\n" +
                       $"  Length: {length:F1} m\n" +
                       $"  Height: {height:F1} m\n" +
                       $"  Thickness: {thickness * 1000:F0} mm\n" +
                       $"  Level: {level.Name}\n" +
                       $"  Element ID: {wall.Id.Value}";
            }
        }

        private string CreateFloor(Document doc, RevitCommand cmd)
        {
            double width = cmd.GetDouble("width", 5.0);
            double depth = cmd.GetDouble("depth", 5.0);
            string floorTypeName = cmd.GetString("type", null);

            double widthFt = width / 0.3048;
            double depthFt = depth / 0.3048;

            using (var tx = new Transaction(doc, "StingBIM AI: Create Floor"))
            {
                tx.Start();

                var floorType = GetFloorType(doc, floorTypeName);
                if (floorType == null)
                    return "No floor type found in the project.";

                var level = GetLowestLevel(doc);
                if (level == null)
                    return "No levels found.";

                // Create rectangular profile
                var profile = new List<Curve>
                {
                    Line.CreateBound(new XYZ(0, 0, 0), new XYZ(widthFt, 0, 0)),
                    Line.CreateBound(new XYZ(widthFt, 0, 0), new XYZ(widthFt, depthFt, 0)),
                    Line.CreateBound(new XYZ(widthFt, depthFt, 0), new XYZ(0, depthFt, 0)),
                    Line.CreateBound(new XYZ(0, depthFt, 0), new XYZ(0, 0, 0))
                };

                var curveLoop = CurveLoop.Create(profile);
                var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, level.Id);

                tx.Commit();

                return $"Floor created successfully!\n\n" +
                       $"  Type: {floorType.Name}\n" +
                       $"  Size: {width:F1} x {depth:F1} m\n" +
                       $"  Area: {width * depth:F1} m\u00B2\n" +
                       $"  Level: {level.Name}\n" +
                       $"  Element ID: {floor.Id.Value}";
            }
        }

        private string CreateRoom(Document doc, RevitCommand cmd)
        {
            double width = cmd.GetDouble("width", 4.0);
            double depth = cmd.GetDouble("depth", 5.0);
            double wallHeight = cmd.GetDouble("height", 3.0);
            string roomName = cmd.GetString("name", "Room");

            double widthFt = width / 0.3048;
            double depthFt = depth / 0.3048;
            double heightFt = wallHeight / 0.3048;

            using (var tx = new Transaction(doc, $"StingBIM AI: Create {roomName}"))
            {
                tx.Start();

                var wallType = GetWallType(doc, null);
                if (wallType == null)
                    return "No wall type found. Load a wall family first.";

                var level = GetLowestLevel(doc);
                if (level == null)
                    return "No levels found. Create a level first.";

                // Create 4 walls to form a room
                var p1 = new XYZ(0, 0, 0);
                var p2 = new XYZ(widthFt, 0, 0);
                var p3 = new XYZ(widthFt, depthFt, 0);
                var p4 = new XYZ(0, depthFt, 0);

                Wall.Create(doc, Line.CreateBound(p1, p2), wallType.Id, level.Id, heightFt, 0, false, false);
                Wall.Create(doc, Line.CreateBound(p2, p3), wallType.Id, level.Id, heightFt, 0, false, false);
                Wall.Create(doc, Line.CreateBound(p3, p4), wallType.Id, level.Id, heightFt, 0, false, false);
                Wall.Create(doc, Line.CreateBound(p4, p1), wallType.Id, level.Id, heightFt, 0, false, false);

                tx.Commit();

                return $"{roomName} created successfully!\n\n" +
                       $"  Size: {width:F1} x {depth:F1} m\n" +
                       $"  Wall height: {wallHeight:F1} m\n" +
                       $"  Area: {width * depth:F1} m\u00B2\n" +
                       $"  Walls: 4 x {wallType.Name}\n" +
                       $"  Level: {level.Name}\n\n" +
                       "Tip: Place a Room element inside the\n" +
                       "enclosed area to tag it.";
            }
        }

        private string AutoPopulateParameters(Document doc, RevitCommand cmd)
        {
            string category = cmd.GetString("category", "all");
            int populated = 0;

            using (var tx = new Transaction(doc, "StingBIM AI: Auto-Populate Parameters"))
            {
                tx.Start();

                // Auto-populate common parameters based on element data
                var categories = new[] {
                    BuiltInCategory.OST_Walls, BuiltInCategory.OST_Doors,
                    BuiltInCategory.OST_Windows, BuiltInCategory.OST_Rooms
                };

                foreach (var cat in categories)
                {
                    var elements = new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    foreach (var elem in elements)
                    {
                        // Try to populate "Comments" with type info if empty
                        var comments = elem.LookupParameter("Comments");
                        if (comments != null && !comments.IsReadOnly &&
                            string.IsNullOrWhiteSpace(comments.AsString()))
                        {
                            var typeName = doc.GetElement(elem.GetTypeId())?.Name ?? "";
                            if (!string.IsNullOrEmpty(typeName))
                            {
                                comments.Set($"Auto: {typeName}");
                                populated++;
                            }
                        }

                        // Try to populate "Mark" if empty
                        var mark = elem.LookupParameter("Mark");
                        if (mark != null && !mark.IsReadOnly &&
                            string.IsNullOrWhiteSpace(mark.AsString()))
                        {
                            mark.Set($"{cat.ToString().Replace("OST_", "")[0]}-{elem.Id.Value}");
                            populated++;
                        }
                    }
                }

                if (populated > 0)
                    tx.Commit();
                else
                    tx.RollBack();

                return populated > 0
                    ? $"Parameters auto-populated!\n\n  {populated} parameter values set\n\nPopulated: Comments, Mark fields\nacross walls, doors, windows, rooms."
                    : "No empty parameters found to populate.\nAll common fields already have values.";
            }
        }

        #endregion

        #region Helpers

        private WallType GetWallType(Document doc, string typeName)
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                var byName = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .FirstOrDefault(e => e.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase));
                if (byName is WallType found) return found;
            }

            // Get first basic wall type
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);
        }

        private FloorType GetFloorType(Document doc, string typeName)
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                var byName = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .FirstOrDefault(e => e.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase));
                if (byName is FloorType found) return found;
            }

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault();
        }

        private Level GetLowestLevel(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        #endregion

        public string GetName() => "StingBIM AI Command Handler";
    }

    #region Command Types

    internal enum RevitCommandType
    {
        CreateWall,
        CreateFloor,
        CreateRoom,
        AutoPopulateParameters
    }

    internal class RevitCommand
    {
        public RevitCommandType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();

        public double GetDouble(string key, double defaultValue)
        {
            if (Parameters.TryGetValue(key, out var val))
            {
                if (val is double d) return d;
                if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            if (Parameters.TryGetValue(key, out var val))
                return val?.ToString() ?? defaultValue;
            return defaultValue;
        }
    }

    #endregion
}
