// StingBIM.AI.Creation.MEP.DataITCreator
// Handles: structured cabling, data outlets, server racks, WiFi APs, patch panels
// v4 Prompt Reference: Section A.8 Phase 8 — Specialist Systems
// Standards: TIA-568, ISO/IEC 11801, BS EN 50173

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates Data/IT infrastructure elements: structured cabling, data outlets,
    /// server racks, WiFi access points, patch panels, and cable management.
    ///
    /// Uganda/East Africa Context:
    ///   - TIA-568 structured cabling (widely adopted)
    ///   - Cat6A recommended for new builds (future-proofing)
    ///   - UPS provision for server rooms (power reliability)
    ///   - Minimum 2 data outlets per office workstation
    ///   - WiFi coverage: 15–20 m radius per AP in commercial
    /// </summary>
    public class DataITCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Data outlet standards per room type
        private static readonly Dictionary<string, int> DataOutletsPerRoom =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 4,
                ["Open Plan Office"] = 6,
                ["Conference"] = 4,
                ["Meeting Room"] = 4,
                ["Reception"] = 2,
                ["Server Room"] = 24,
                ["Comms Room"] = 12,
                ["Bedroom"] = 2,
                ["Living Room"] = 2,
                ["Kitchen"] = 1,
                ["Master Bedroom"] = 2,
                ["Study"] = 4,
                ["Lobby"] = 1,
                ["Classroom"] = 4,
                ["Library"] = 6,
                ["Laboratory"] = 6,
            };

        // WiFi AP coverage radius (metres) by environment
        private static readonly Dictionary<string, double> WiFiCoverageRadiusM =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Open Plan"] = 20.0,
                ["Office"] = 15.0,
                ["Warehouse"] = 30.0,
                ["Corridor"] = 25.0,
                ["Conference"] = 12.0,
                ["Classroom"] = 15.0,
                ["default"] = 15.0,
            };

        // Outlet heights (mm AFF)
        private const double DATA_OUTLET_HEIGHT = 300;     // Standard desk-height outlet
        private const double FLOOR_BOX_HEIGHT = 0;          // Floor box for open plan
        private const double WORKTOP_HEIGHT = 1050;         // Above-desk outlet
        private const double SERVER_RACK_HEIGHT = 0;        // Floor-mounted

        // Cable categories
        private static readonly Dictionary<string, string> CableCategories =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cat5e"] = "Cat 5e — 1 Gbps, 100m max",
                ["cat6"] = "Cat 6 — 1 Gbps (10 Gbps at 55m), 100m max",
                ["cat6a"] = "Cat 6A — 10 Gbps, 100m max (Recommended)",
                ["cat7"] = "Cat 7 — 10 Gbps, 100m max, shielded",
                ["fibre"] = "OM4 Multimode Fibre — 100 Gbps, 150m",
            };

        // Server rack standards (42U standard)
        private const double RACK_WIDTH_MM = 600;
        private const double RACK_DEPTH_MM = 1000;
        private const double RACK_HEIGHT_MM = 2000;
        private const double RACK_CLEARANCE_FRONT_MM = 1200;
        private const double RACK_CLEARANCE_REAR_MM = 900;
        private const double RACK_CLEARANCE_SIDE_MM = 600;

        // Server room environmental requirements
        private const double SERVER_ROOM_TEMP_MIN = 18.0;
        private const double SERVER_ROOM_TEMP_MAX = 27.0;
        private const double SERVER_ROOM_HUMIDITY_MIN = 40.0;
        private const double SERVER_ROOM_HUMIDITY_MAX = 60.0;

        public DataITCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Data Outlet Placement

        /// <summary>
        /// Places data outlets in a room based on room type standards.
        /// </summary>
        public CreationPipelineResult PlaceDataOutlets(DataOutletCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Data Outlet" };

            try
            {
                Logger.Info($"Placing data outlets: room={cmd.RoomName}, type={cmd.RoomType}");

                var outletCount = cmd.OutletCount > 0
                    ? cmd.OutletCount
                    : GetOutletCount(cmd.RoomType);

                var cableType = string.IsNullOrEmpty(cmd.CableCategory) ? "cat6a" : cmd.CableCategory;
                var cableDesc = CableCategories.ContainsKey(cableType)
                    ? CableCategories[cableType]
                    : CableCategories["cat6a"];

                // Find room for placement
                var room = FindRoom(cmd.RoomName, cmd.LevelName);
                if (room == null)
                {
                    result.Success = true;
                    result.Message = $"Data outlet specification: {outletCount}× {cableDesc} " +
                                     $"outlets for {cmd.RoomType ?? cmd.RoomName}.\n" +
                                     "Height: 300mm AFF (standard) or floor box for open plan.\n" +
                                     "Each outlet = double faceplate (2× RJ45 ports).";
                    return result;
                }

                // Calculate positions — distribute along walls
                var positions = CalculateOutletPositions(room, outletCount);

                using (var tx = new Transaction(_document, "Place Data Outlets"))
                {
                    tx.Start();

                    var placedIds = new List<ElementId>();
                    foreach (var pos in positions)
                    {
                        var symbol = _familyResolver.ResolveCommunicationsDevice("Data Outlet");
                        if (symbol != null)
                        {
                            var instance = _document.Create.NewFamilyInstance(
                                pos, symbol, StructuralType.NonStructural);

                            if (instance != null)
                            {
                                SetParameter(instance, "Cable Category", cableType.ToUpper());
                                SetParameter(instance, "Ports", "2");
                                placedIds.Add(instance.Id);
                            }
                        }
                    }

                    tx.Commit();

                    result.Success = true;
                    result.CreatedElementIds = placedIds;
                    result.Message = $"Placed {placedIds.Count}× data outlets ({cableDesc}) in " +
                                     $"{cmd.RoomName ?? cmd.RoomType}.\n" +
                                     $"Total ports: {placedIds.Count * 2} (double faceplate).\n" +
                                     "Each run to nearest comms room/patch panel.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to place data outlets");
                result.Success = false;
                result.Error = $"Data outlet placement failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Places data outlets in all rooms on a level based on room type standards.
        /// </summary>
        public CreationPipelineResult PlaceDataOutletsAllRooms(string levelName)
        {
            var result = new CreationPipelineResult { ElementType = "Data Outlet" };

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
                    rooms = rooms.Where(r =>
                        r.Level?.Name?.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                int totalOutlets = 0;
                var details = new List<string>();

                foreach (var room in rooms)
                {
                    var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Room";
                    var count = GetOutletCount(roomName);
                    if (count > 0)
                    {
                        totalOutlets += count;
                        details.Add($"  {roomName}: {count}× Cat6A outlets ({count * 2} ports)");
                    }
                }

                result.Success = true;
                result.Message = $"Data outlet specification for {rooms.Count} rooms:\n" +
                                 string.Join("\n", details) +
                                 $"\n\nTotal: {totalOutlets} outlets, {totalOutlets * 2} ports.\n" +
                                 "Cable: Cat6A recommended (10 Gbps, future-proof).";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to specify data outlets for all rooms");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region WiFi Access Point Placement

        /// <summary>
        /// Calculates and places WiFi access points for coverage.
        /// </summary>
        public CreationPipelineResult PlaceWiFiAccessPoints(WiFiAPCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "WiFi Access Point" };

            try
            {
                Logger.Info($"Placing WiFi APs: level={cmd.LevelName}, type={cmd.EnvironmentType}");

                var radius = WiFiCoverageRadiusM.ContainsKey(cmd.EnvironmentType ?? "default")
                    ? WiFiCoverageRadiusM[cmd.EnvironmentType]
                    : WiFiCoverageRadiusM["default"];

                // Get floor area for AP count calculation
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (!string.IsNullOrEmpty(cmd.LevelName))
                {
                    rooms = rooms.Where(r =>
                        r.Level?.Name?.IndexOf(cmd.LevelName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                double totalAreaSqFt = rooms.Sum(r => r.Area);
                double totalAreaM2 = totalAreaSqFt * 0.0929;
                double coverageArea = Math.PI * radius * radius;
                int apCount = cmd.APCount > 0
                    ? cmd.APCount
                    : Math.Max(1, (int)Math.Ceiling(totalAreaM2 / coverageArea));

                // Add 20% overlap factor for reliability
                apCount = (int)Math.Ceiling(apCount * 1.2);

                result.Success = true;
                result.Message = $"WiFi coverage plan:\n" +
                                 $"  Floor area: {totalAreaM2:F0} m²\n" +
                                 $"  Coverage radius: {radius:F0} m per AP\n" +
                                 $"  Access points required: {apCount}\n" +
                                 $"  Mounting: ceiling-mounted, 2.7 m AFF\n" +
                                 $"  PoE: IEEE 802.3af/at from nearest switch\n" +
                                 $"  Backhaul: Cat6A to comms room\n" +
                                 $"  Channels: Auto (2.4 GHz + 5 GHz dual-band)";

                result.Suggestions = new List<string>
                {
                    "Avoid placing APs near metal objects or thick walls",
                    "Use site survey for optimal placement in complex layouts",
                    "Consider WiFi 6E (6 GHz) APs for high-density areas"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to plan WiFi APs");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Server Room / Comms Room

        /// <summary>
        /// Designs server room layout with rack placement and environmental requirements.
        /// </summary>
        public CreationPipelineResult DesignServerRoom(ServerRoomCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Server Room" };

            try
            {
                Logger.Info($"Designing server room: racks={cmd.RackCount}");

                int rackCount = cmd.RackCount > 0 ? cmd.RackCount : 2;

                // Calculate minimum room dimensions
                double rackRowWidthMm = rackCount * (RACK_WIDTH_MM + RACK_CLEARANCE_SIDE_MM);
                double rackRowDepthMm = RACK_DEPTH_MM + RACK_CLEARANCE_FRONT_MM + RACK_CLEARANCE_REAR_MM;
                double minWidthMm = rackRowWidthMm + 2 * RACK_CLEARANCE_SIDE_MM;
                double minDepthMm = rackRowDepthMm + 1000; // extra for UPS/PDU

                // Power requirements (estimate 3kW per rack)
                double totalPowerKW = rackCount * 3.0;
                double coolingBTU = totalPowerKW * 3412; // 1 kW ≈ 3412 BTU/hr
                double coolingTons = coolingBTU / 12000;

                // UPS sizing (add 30% headroom)
                double upsKVA = totalPowerKW * 1.3;

                result.Success = true;
                result.Message = $"Server room design ({rackCount}× 42U racks):\n\n" +
                                 $"Room Requirements:\n" +
                                 $"  Minimum size: {minWidthMm / 1000:F1} × {minDepthMm / 1000:F1} m\n" +
                                 $"  Raised floor: 300 mm (cable management)\n" +
                                 $"  Door: 1200 mm wide, outward-opening, access-controlled\n" +
                                 $"  No windows (security + environmental control)\n\n" +
                                 $"Electrical:\n" +
                                 $"  Total load: {totalPowerKW:F0} kW ({rackCount}× 3 kW/rack)\n" +
                                 $"  UPS: {upsKVA:F0} kVA online double-conversion\n" +
                                 $"  Dual power feeds from separate DBs\n" +
                                 $"  Generator backup essential\n\n" +
                                 $"Cooling:\n" +
                                 $"  Cooling load: {coolingBTU:F0} BTU/hr ({coolingTons:F1} tons)\n" +
                                 $"  Temperature: {SERVER_ROOM_TEMP_MIN}–{SERVER_ROOM_TEMP_MAX}°C\n" +
                                 $"  Humidity: {SERVER_ROOM_HUMIDITY_MIN}–{SERVER_ROOM_HUMIDITY_MAX}% RH\n" +
                                 $"  Precision AC recommended (N+1 redundancy)\n\n" +
                                 $"Fire Protection:\n" +
                                 $"  Clean agent suppression (FM-200 / Novec 1230)\n" +
                                 $"  VESDA smoke detection\n" +
                                 $"  No water-based sprinklers";

                result.Suggestions = new List<string>
                {
                    "Hot-aisle/cold-aisle arrangement for rack rows > 4",
                    "Add environmental monitoring (temperature, humidity, water leak)",
                    "Install CCTV inside server room for audit trail"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design server room");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Helpers

        private int GetOutletCount(string roomType)
        {
            if (string.IsNullOrEmpty(roomType)) return 2;

            foreach (var kvp in DataOutletsPerRoom)
            {
                if (roomType.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }
            return 2; // default
        }

        private Autodesk.Revit.DB.Architecture.Room FindRoom(string roomName, string levelName)
        {
            if (string.IsNullOrEmpty(roomName)) return null;

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0);

            return rooms.FirstOrDefault(r =>
            {
                var name = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                return name.IndexOf(roomName, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private List<XYZ> CalculateOutletPositions(Autodesk.Revit.DB.Architecture.Room room, int count)
        {
            var positions = new List<XYZ>();
            var center = (room.Location as LocationPoint)?.Point ?? XYZ.Zero;
            double spacing = 2.0; // feet between outlets

            for (int i = 0; i < count; i++)
            {
                double angle = (2.0 * Math.PI * i) / count;
                double offsetX = Math.Cos(angle) * spacing;
                double offsetY = Math.Sin(angle) * spacing;
                positions.Add(new XYZ(
                    center.X + offsetX,
                    center.Y + offsetY,
                    DATA_OUTLET_HEIGHT * MM_TO_FEET));
            }

            return positions;
        }

        private void SetParameter(FamilyInstance instance, string paramName, string value)
        {
            var param = instance.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
            {
                param.Set(value);
            }
        }

        #endregion
    }

    #region Command Classes

    public class DataOutletCommand
    {
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public string LevelName { get; set; }
        public int OutletCount { get; set; }
        public string CableCategory { get; set; } = "cat6a";
        public bool AllRooms { get; set; }
    }

    public class WiFiAPCommand
    {
        public string LevelName { get; set; }
        public string EnvironmentType { get; set; } = "Office";
        public int APCount { get; set; }
    }

    public class ServerRoomCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public int RackCount { get; set; } = 2;
    }

    #endregion
}
