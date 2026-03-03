// StingBIM.AI.Creation.MEP.SecurityCreator
// Handles: CCTV cameras, access control, alarm panels, intercom, motion detectors
// v4 Prompt Reference: Section A.8 Phase 8 — Specialist Systems
// Standards: BS EN 62676 (CCTV), BS EN 60839 (Alarm), BS 8418 (Remote monitoring)

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates security system elements: CCTV cameras, access control readers,
    /// alarm panels, intercom stations, motion/PIR detectors, and panic buttons.
    ///
    /// Uganda/East Africa Context:
    ///   - Physical security emphasis (perimeter walls, gates)
    ///   - CCTV with local NVR storage (internet reliability)
    ///   - Access control for commercial/institutional buildings
    ///   - Integration with guard stations
    ///   - Lightning protection for external devices
    /// </summary>
    public class SecurityCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // CCTV camera mounting heights (mm AFF)
        private static readonly Dictionary<string, double> CameraMountHeightMm =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["indoor"] = 2700,
                ["outdoor"] = 3500,
                ["car park"] = 4000,
                ["corridor"] = 2700,
                ["entrance"] = 3000,
                ["perimeter"] = 4000,
                ["reception"] = 2700,
                ["stairwell"] = 3000,
            };

        // Camera types and coverage
        private static readonly Dictionary<string, (string Type, double FovDeg, double RangeM)> CameraSpecs =
            new Dictionary<string, (string, double, double)>(StringComparer.OrdinalIgnoreCase)
            {
                ["dome"] = ("Dome Camera", 90, 15),
                ["bullet"] = ("Bullet Camera", 60, 30),
                ["ptz"] = ("PTZ Camera", 360, 50),
                ["fisheye"] = ("Fisheye Camera", 360, 10),
                ["anpr"] = ("ANPR Camera", 30, 25),
            };

        // Access control zones
        private static readonly Dictionary<string, string> AccessControlLevels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["public"] = "No restriction",
                ["staff"] = "Card/PIN access",
                ["restricted"] = "Card + PIN access",
                ["secure"] = "Biometric + Card",
                ["critical"] = "Dual authentication + CCTV verification",
            };

        // Motion detector spacing (metres)
        private const double PIR_COVERAGE_RADIUS_M = 12.0;
        private const double PIR_MOUNT_HEIGHT_MM = 2200;
        private const double PANIC_BUTTON_HEIGHT_MM = 1200;
        private const double INTERCOM_HEIGHT_MM = 1500;
        private const double CARD_READER_HEIGHT_MM = 1100;

        // NVR storage calculation constants
        private const double MBPS_PER_CAMERA_HD = 4.0;    // 1080p H.265
        private const double MBPS_PER_CAMERA_4K = 12.0;   // 4K H.265
        private const int DEFAULT_RETENTION_DAYS = 30;

        public SecurityCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region CCTV Placement

        /// <summary>
        /// Places CCTV cameras at building entry/exit points and key areas.
        /// </summary>
        public CreationPipelineResult PlaceCCTV(CCTVCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "CCTV Camera" };

            try
            {
                Logger.Info($"Placing CCTV: type={cmd.CameraType}, location={cmd.LocationType}");

                var cameraType = cmd.CameraType ?? "dome";
                var spec = CameraSpecs.ContainsKey(cameraType)
                    ? CameraSpecs[cameraType]
                    : CameraSpecs["dome"];

                var mountHeight = CameraMountHeightMm.ContainsKey(cmd.LocationType ?? "indoor")
                    ? CameraMountHeightMm[cmd.LocationType]
                    : CameraMountHeightMm["indoor"];

                int cameraCount = cmd.CameraCount > 0 ? cmd.CameraCount : EstimateCameraCount(cmd.LocationType);

                // NVR storage calculation
                double bitsPerSec = cameraCount * (cmd.Resolution == "4K" ? MBPS_PER_CAMERA_4K : MBPS_PER_CAMERA_HD);
                int retentionDays = cmd.RetentionDays > 0 ? cmd.RetentionDays : DEFAULT_RETENTION_DAYS;
                double storageTB = (bitsPerSec * 86400.0 * retentionDays) / (8.0 * 1024 * 1024);

                result.Success = true;
                result.Message = $"CCTV specification:\n" +
                                 $"  Camera: {spec.Type} × {cameraCount}\n" +
                                 $"  Resolution: {cmd.Resolution ?? "1080p"} H.265\n" +
                                 $"  FOV: {spec.FovDeg}°, Range: {spec.RangeM} m\n" +
                                 $"  Mount height: {mountHeight / 1000:F1} m AFF\n" +
                                 $"  Power: PoE (IEEE 802.3af)\n\n" +
                                 $"Recording:\n" +
                                 $"  NVR storage: {storageTB:F1} TB ({retentionDays}-day retention)\n" +
                                 $"  Recording: 24/7 continuous + motion-triggered\n" +
                                 $"  Backup: local NVR primary, cloud secondary\n\n" +
                                 $"Cabling:\n" +
                                 $"  Cat6 from camera to NVR (PoE)\n" +
                                 $"  Outdoor: UV-rated, IP67 connectors\n" +
                                 $"  Lightning protection on external cameras";

                result.Suggestions = new List<string>
                {
                    "Add IR illumination for night coverage on external cameras",
                    "Place ANPR camera at vehicle entrance for plate recognition",
                    "Consider UPS backup for NVR (minimum 4-hour runtime)"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to specify CCTV");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Places CCTV at all entry/exit points automatically.
        /// </summary>
        public CreationPipelineResult PlaceCCTVAllEntries(string levelName)
        {
            var result = new CreationPipelineResult { ElementType = "CCTV Camera" };

            try
            {
                // Find all external doors
                var doors = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                var externalDoors = doors.Where(d =>
                {
                    var typeName = (d.Symbol?.Name ?? "").ToLowerInvariant();
                    return typeName.Contains("external") || typeName.Contains("entrance") ||
                           typeName.Contains("main") || typeName.Contains("fire exit");
                }).ToList();

                int entryCount = Math.Max(externalDoors.Count, 2); // minimum 2 entries
                int totalCameras = entryCount * 2; // 2 cameras per entry (inside + outside)

                // Add corridor and car park cameras
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                int corridorCams = rooms.Count(r =>
                {
                    var name = (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                    return name.Contains("corridor") || name.Contains("hallway") || name.Contains("lobby");
                });

                totalCameras += corridorCams;

                double storageTB = (totalCameras * MBPS_PER_CAMERA_HD * 86400.0 * DEFAULT_RETENTION_DAYS) /
                                   (8.0 * 1024 * 1024);

                result.Success = true;
                result.Message = $"CCTV coverage plan:\n" +
                                 $"  Entry/exit cameras: {entryCount * 2} ({entryCount} entry points × 2)\n" +
                                 $"  Corridor/lobby cameras: {corridorCams}\n" +
                                 $"  Total cameras: {totalCameras}\n" +
                                 $"  NVR storage: {storageTB:F1} TB (30-day retention)\n" +
                                 $"  All cameras PoE (Cat6 cabling)";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to plan CCTV coverage");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Access Control

        /// <summary>
        /// Specifies access control for doors.
        /// </summary>
        public CreationPipelineResult PlaceAccessControl(AccessControlCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Access Control" };

            try
            {
                Logger.Info($"Specifying access control: level={cmd.SecurityLevel}");

                var level = cmd.SecurityLevel ?? "staff";
                var accessDesc = AccessControlLevels.ContainsKey(level)
                    ? AccessControlLevels[level]
                    : AccessControlLevels["staff"];

                result.Success = true;
                result.Message = $"Access control specification:\n" +
                                 $"  Security level: {level.ToUpper()} — {accessDesc}\n" +
                                 $"  Reader type: {GetReaderType(level)}\n" +
                                 $"  Reader height: {CARD_READER_HEIGHT_MM} mm AFF\n" +
                                 $"  Lock: {GetLockType(level)}\n" +
                                 $"  Location: {cmd.DoorName ?? "all controlled doors"}\n\n" +
                                 $"System:\n" +
                                 $"  Controller: TCP/IP networked\n" +
                                 $"  Power: 12V DC with battery backup\n" +
                                 $"  Fail-safe: unlock on fire alarm\n" +
                                 $"  Audit: all events logged with timestamp\n" +
                                 $"  Integration: CCTV snapshot on access events";

                result.Suggestions = new List<string>
                {
                    "Add request-to-exit (REX) button inside controlled doors",
                    "Consider anti-passback for high-security zones",
                    "Install break-glass unit for emergency egress"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to specify access control");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Alarm System

        /// <summary>
        /// Designs intruder alarm system for the building.
        /// </summary>
        public CreationPipelineResult DesignAlarmSystem(AlarmSystemCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Alarm System" };

            try
            {
                Logger.Info("Designing alarm system");

                // Count rooms for PIR sensors
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                int pirCount = rooms.Count; // 1 PIR per room minimum
                int doorContacts = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                // External PIR/beam detectors for perimeter
                int perimeterSensors = cmd.HasPerimeter ? 4 : 0; // 4 sides minimum

                int keypads = Math.Max(2, rooms.Count / 20); // 1 keypad per 20 rooms, minimum 2
                int panicButtons = cmd.PanicButtonCount > 0
                    ? cmd.PanicButtonCount
                    : Math.Max(2, rooms.Count / 10);

                result.Success = true;
                result.Message = $"Intruder alarm system specification:\n\n" +
                                 $"Detection:\n" +
                                 $"  PIR motion sensors: {pirCount} (1 per room)\n" +
                                 $"  Door contacts: {doorContacts}\n" +
                                 $"  Perimeter beams: {perimeterSensors}\n\n" +
                                 $"Control:\n" +
                                 $"  Alarm panel: {GetPanelSize(pirCount + doorContacts)}-zone\n" +
                                 $"  Keypads: {keypads}\n" +
                                 $"  Panic buttons: {panicButtons} ({PANIC_BUTTON_HEIGHT_MM} mm AFF)\n\n" +
                                 $"Notification:\n" +
                                 $"  Internal sounder: 1 per floor\n" +
                                 $"  External sounder/strobe: 1 (visible from road)\n" +
                                 $"  SMS/call notification to owner\n" +
                                 $"  Optional: ARC monitoring (Alarm Receiving Centre)\n\n" +
                                 $"PIR mount height: {PIR_MOUNT_HEIGHT_MM} mm AFF, corner-mounted";

                result.Suggestions = new List<string>
                {
                    "Zone alarm by floor/wing for partial arming",
                    "Add glass-break detectors on ground-floor windows",
                    "Consider pet-immune PIRs if building allows animals"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design alarm system");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Intercom

        /// <summary>
        /// Specifies intercom/door entry system.
        /// </summary>
        public CreationPipelineResult PlaceIntercom(IntercomCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Intercom" };

            try
            {
                var systemType = cmd.SystemType ?? "audio-video";
                int stations = cmd.StationCount > 0 ? cmd.StationCount : 2;

                result.Success = true;
                result.Message = $"Intercom/door entry system:\n" +
                                 $"  Type: {systemType} intercom\n" +
                                 $"  External stations: {stations}\n" +
                                 $"  Height: {INTERCOM_HEIGHT_MM} mm AFF\n" +
                                 $"  Internal monitors: 1 per apartment/reception\n" +
                                 $"  Electric lock release integration\n" +
                                 $"  Cabling: Cat5e (IP-based) or 2-wire";

                if (systemType.Contains("video"))
                {
                    result.Message += "\n  Camera: wide-angle colour with IR night vision";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to specify intercom");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Helpers

        private int EstimateCameraCount(string locationType)
        {
            switch ((locationType ?? "").ToLowerInvariant())
            {
                case "car park": return 8;
                case "perimeter": return 6;
                case "entrance": return 2;
                case "corridor": return 4;
                default: return 4;
            }
        }

        private string GetReaderType(string securityLevel)
        {
            switch ((securityLevel ?? "").ToLowerInvariant())
            {
                case "staff": return "Proximity card reader (HID iCLASS)";
                case "restricted": return "Card reader + PIN keypad";
                case "secure": return "Fingerprint + card reader";
                case "critical": return "Iris/facial recognition + card";
                default: return "Proximity card reader";
            }
        }

        private string GetLockType(string securityLevel)
        {
            switch ((securityLevel ?? "").ToLowerInvariant())
            {
                case "staff": return "Electric strike (fail-safe)";
                case "restricted": return "Magnetic lock (fail-safe)";
                case "secure": return "Magnetic lock + dead bolt";
                case "critical": return "Multi-point locking + interlock";
                default: return "Electric strike";
            }
        }

        private string GetPanelSize(int zones)
        {
            if (zones <= 16) return "16";
            if (zones <= 32) return "32";
            if (zones <= 64) return "64";
            if (zones <= 128) return "128";
            return "256+";
        }

        #endregion
    }

    #region Command Classes

    public class CCTVCommand
    {
        public string CameraType { get; set; } = "dome";
        public string LocationType { get; set; } = "indoor";
        public string Resolution { get; set; } = "1080p";
        public int CameraCount { get; set; }
        public int RetentionDays { get; set; } = 30;
        public string LevelName { get; set; }
        public bool AllEntries { get; set; }
    }

    public class AccessControlCommand
    {
        public string DoorName { get; set; }
        public string SecurityLevel { get; set; } = "staff";
        public string LevelName { get; set; }
    }

    public class AlarmSystemCommand
    {
        public bool HasPerimeter { get; set; } = true;
        public int PanicButtonCount { get; set; }
        public string LevelName { get; set; }
    }

    public class IntercomCommand
    {
        public string SystemType { get; set; } = "audio-video";
        public int StationCount { get; set; } = 2;
        public string LevelName { get; set; }
    }

    #endregion
}
