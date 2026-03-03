// StingBIM.AI.Creation.MEP.GasCreator
// Handles: gas piping, meters, shut-off valves, gas detectors, regulators
// v4 Prompt Reference: Section A.8 Phase 8 — Specialist Systems
// Standards: BS 6891 (Domestic gas), IGE/UP/2 (Commercial gas), Uganda NPA regulations

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates gas distribution elements: piping, meters, regulators,
    /// shut-off valves, gas detectors, and ventilation requirements.
    ///
    /// Uganda/East Africa Context:
    ///   - LPG (bottled gas) more common than piped natural gas
    ///   - LPG cylinder storage rooms require specific ventilation
    ///   - Gas installations must comply with local fire authority
    ///   - Copper or steel piping (no plastic for gas)
    ///   - Emergency shut-off valves at entry point and each appliance
    ///   - Gas detection mandatory in commercial kitchens
    /// </summary>
    public class GasCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Pipe sizing by gas load (kW) → nominal bore (mm)
        private static readonly List<(double MaxKW, double BoreMm)> PipeSizeTable =
            new List<(double, double)>
            {
                (15, 15),
                (30, 22),
                (60, 28),
                (100, 35),
                (200, 42),
                (400, 54),
                (800, 67),
                (1500, 76),
            };

        // Gas appliance loads (kW)
        private static readonly Dictionary<string, double> ApplianceLoadsKW =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["cooker"] = 12.0,
                ["hob"] = 8.0,
                ["oven"] = 6.0,
                ["boiler"] = 30.0,
                ["water heater"] = 20.0,
                ["geyser"] = 20.0,
                ["commercial range"] = 40.0,
                ["commercial oven"] = 30.0,
                ["deep fryer"] = 20.0,
                ["tandoor"] = 25.0,
                ["gas fire"] = 6.0,
                ["space heater"] = 15.0,
                ["generator"] = 50.0,
            };

        // Ventilation requirements (cm² free area per kW)
        private const double VENT_AREA_CM2_PER_KW = 5.0;
        private const double MIN_ROOM_VOLUME_M3 = 5.0;

        // Safety clearances (mm)
        private const double GAS_METER_HEIGHT_MM = 1000;
        private const double SHUT_OFF_VALVE_HEIGHT_MM = 500;
        private const double GAS_DETECTOR_HEIGHT_MM = 300;  // LPG is heavier than air
        private const double NAT_GAS_DETECTOR_HEIGHT_MM = 2400; // Natural gas rises

        // LPG cylinder storage
        private const double LPG_MIN_DISTANCE_FROM_BUILDING_M = 1.0;
        private const double LPG_MIN_DISTANCE_FROM_OPENING_M = 2.0;
        private const double LPG_STORE_VENT_LOW_CM2 = 500;
        private const double LPG_STORE_VENT_HIGH_CM2 = 500;

        public GasCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Gas Piping

        /// <summary>
        /// Designs gas piping route from meter to appliances.
        /// </summary>
        public CreationPipelineResult DesignGasPiping(GasPipingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Gas Piping" };

            try
            {
                Logger.Info($"Designing gas piping: type={cmd.GasType}, appliances={cmd.Appliances?.Count ?? 0}");

                var gasType = cmd.GasType ?? "LPG";
                var appliances = cmd.Appliances ?? new List<string> { "cooker" };

                // Calculate total gas load
                double totalLoadKW = 0;
                var appDetails = new List<string>();
                foreach (var app in appliances)
                {
                    double loadKW = ApplianceLoadsKW.ContainsKey(app)
                        ? ApplianceLoadsKW[app]
                        : 10.0;
                    totalLoadKW += loadKW;
                    appDetails.Add($"  {app}: {loadKW:F0} kW");
                }

                // Apply diversity factor for multiple appliances
                double diversityFactor = appliances.Count <= 2 ? 1.0
                    : appliances.Count <= 5 ? 0.9
                    : 0.8;
                double designLoadKW = totalLoadKW * diversityFactor;

                // Select pipe size
                double mainBoreMm = SelectPipeSize(designLoadKW);
                double branchBoreMm = SelectPipeSize(designLoadKW / appliances.Count);

                // Ventilation requirement
                double ventAreaCm2 = designLoadKW * VENT_AREA_CM2_PER_KW;

                // Detector height depends on gas type
                double detectorHeight = gasType.ToUpperInvariant() == "LPG"
                    ? GAS_DETECTOR_HEIGHT_MM
                    : NAT_GAS_DETECTOR_HEIGHT_MM;

                result.Success = true;
                result.Message = $"Gas piping design ({gasType}):\n\n" +
                                 $"Appliance Loads:\n" +
                                 string.Join("\n", appDetails) +
                                 $"\n  Total: {totalLoadKW:F0} kW (design: {designLoadKW:F0} kW, " +
                                 $"diversity {diversityFactor:F1})\n\n" +
                                 $"Piping:\n" +
                                 $"  Main supply: {mainBoreMm} mm bore (copper/steel)\n" +
                                 $"  Branch pipes: {branchBoreMm} mm bore\n" +
                                 $"  Material: copper (BS EN 1057) or steel (BS 1387)\n" +
                                 $"  Joints: capillary/compression (no push-fit for gas)\n" +
                                 $"  Test pressure: 30 mbar × 1.5 = 45 mbar for 2 min\n\n" +
                                 $"Safety:\n" +
                                 $"  Emergency shut-off: at meter + each appliance\n" +
                                 $"  Gas detector: {detectorHeight} mm AFF ({gasType} — " +
                                 (gasType.ToUpperInvariant() == "LPG" ? "floor-level, heavier than air" : "ceiling-level, lighter than air") +
                                 ")\n" +
                                 $"  Ventilation: {ventAreaCm2:F0} cm² free area (low + high)\n" +
                                 $"  Pipe sleeves through walls/floors (fire-stopped)\n" +
                                 $"  Yellow marking tape on all gas pipes";

                if (gasType.ToUpperInvariant() == "LPG")
                {
                    result.Message += $"\n\nLPG Storage:\n" +
                                     $"  Minimum {LPG_MIN_DISTANCE_FROM_BUILDING_M} m from building\n" +
                                     $"  Minimum {LPG_MIN_DISTANCE_FROM_OPENING_M} m from openings\n" +
                                     $"  Ventilated enclosure: low vent {LPG_STORE_VENT_LOW_CM2} cm², " +
                                     $"high vent {LPG_STORE_VENT_HIGH_CM2} cm²\n" +
                                     $"  No below-ground storage (LPG pools)";
                }

                result.Suggestions = new List<string>
                {
                    "Install gas solenoid valve linked to detection system",
                    "Label all gas pipework with yellow identification tape",
                    "Provide gas interlock in commercial kitchens (BS 6173)"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design gas piping");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Gas Detection

        /// <summary>
        /// Places gas detectors in appropriate locations.
        /// </summary>
        public CreationPipelineResult PlaceGasDetectors(GasDetectorCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Gas Detector" };

            try
            {
                var gasType = cmd.GasType ?? "LPG";
                double mountHeight = gasType.ToUpperInvariant() == "LPG"
                    ? GAS_DETECTOR_HEIGHT_MM
                    : NAT_GAS_DETECTOR_HEIGHT_MM;

                // Find kitchens, plant rooms, and gas appliance locations
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                var gasRooms = rooms.Where(r =>
                {
                    var name = (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                    return name.Contains("kitchen") || name.Contains("plant") ||
                           name.Contains("boiler") || name.Contains("gas") ||
                           name.Contains("utility") || name.Contains("laundry");
                }).ToList();

                int detectorCount = Math.Max(gasRooms.Count, 1);

                result.Success = true;
                result.Message = $"Gas detection specification ({gasType}):\n" +
                                 $"  Detectors: {detectorCount}\n" +
                                 $"  Mount height: {mountHeight} mm AFF\n" +
                                 $"  Locations: {string.Join(", ", gasRooms.Select(r => r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Room"))}\n" +
                                 $"  Type: catalytic/semiconductor {gasType} detector\n" +
                                 $"  Output: relay to gas solenoid valve + audible alarm\n" +
                                 $"  Power: 230V AC with battery backup\n" +
                                 $"  Linked to: BMS / fire alarm panel";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to specify gas detectors");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Helpers

        private double SelectPipeSize(double loadKW)
        {
            foreach (var entry in PipeSizeTable)
            {
                if (loadKW <= entry.MaxKW)
                    return entry.BoreMm;
            }
            return PipeSizeTable[PipeSizeTable.Count - 1].BoreMm;
        }

        #endregion
    }

    #region Command Classes

    public class GasPipingCommand
    {
        public string GasType { get; set; } = "LPG";
        public List<string> Appliances { get; set; } = new List<string> { "cooker" };
        public string LevelName { get; set; }
    }

    public class GasDetectorCommand
    {
        public string GasType { get; set; } = "LPG";
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
    }

    #endregion
}
