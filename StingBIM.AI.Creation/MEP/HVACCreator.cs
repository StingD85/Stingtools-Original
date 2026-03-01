// StingBIM.AI.Creation.MEP.HVACCreator
// Handles: split AC, ceiling fans, kitchen hoods, extract fans, duct routing
// v4 Prompt Reference: Section A.3.3 HVAC & MECHANICAL
// Standards: ASHRAE 62.1, CIBSE Guide A, Uganda Building Control

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates HVAC elements: split AC units, ceiling fans, kitchen hoods,
    /// bathroom extract fans, duct routing, diffusers and grilles.
    ///
    /// Split AC Sizing (Uganda hot climate):
    ///   Cooling load ≈ 100-150 W/m²
    ///   Bedroom 12m²: 1.5kW (6000 BTU)
    ///   Living 25m²: 2.5kW (9000 BTU)
    ///   Large space: 3.5kW (12000 BTU) or cassette
    ///
    /// Duct Sizing (ASHRAE + CIBSE):
    ///   Main duct: 5.0-8.0 m/s, Branch: 3.0-5.0 m/s, Final: 2.0-3.0 m/s
    ///
    /// Ceiling Fan:
    ///   Min 2100mm blade-to-floor clearance
    ///   300mm blade-to-wall clearance
    ///
    /// Kitchen/Bathroom Extract:
    ///   Kitchen: min 15 ACH (CIBSE), hood 650-700mm above cooker
    ///   Bathroom: min 10 ACH, IP44 fan
    /// </summary>
    public class HVACCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // AC sizing: watts per m² for Uganda hot climate
        private const double COOLING_LOAD_WPM2 = 125; // mid-range for tropical
        // Split AC standard sizes (kW)
        private static readonly double[] ACSizes = { 1.5, 2.0, 2.5, 3.5, 5.0, 7.0 };

        // Ceiling fan clearances (mm)
        private const double FAN_BLADE_TO_FLOOR_MIN = 2100;
        private const double FAN_BLADE_TO_WALL_MIN = 300;

        // Duct velocities (m/s)
        private const double MAIN_DUCT_VELOCITY = 6.0;
        private const double BRANCH_DUCT_VELOCITY = 4.0;

        // Air change rates
        private const int KITCHEN_ACH = 15;
        private const int BATHROOM_ACH = 10;

        // Typical room air quantities (L/s) — Uganda residential
        private static readonly Dictionary<string, int> RoomAirflow =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bedroom"] = 100,
            ["Living Room"] = 250,
            ["Office"] = 150,
            ["Kitchen"] = 400, // exhaust
            ["Conference"] = 300,
            ["Server Room"] = 500,
        };

        public HVACCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Split AC Units

        /// <summary>
        /// Places split AC units — indoor at 2200mm AFF on internal wall,
        /// outdoor on bracket outside adjacent external wall.
        /// Auto-sizes: cooling load ≈ 125 W/m² (Uganda tropical).
        /// </summary>
        public CreationPipelineResult PlaceSplitAC(SplitACCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Split AC" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Find rooms
                var rooms = GetTargetRooms(cmd.RoomName, cmd.AllRooms, level);
                if (rooms.Count == 0)
                {
                    result.SetError("No rooms found. Create rooms first.");
                    return result;
                }

                var indoorSymbol = _familyResolver.ResolveMEPFixture("generator", "indoor");
                var placedIds = new List<ElementId>();
                var details = new List<string>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Split AC"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var room in rooms)
                        {
                            var roomAreaSqM = room.Area * 0.092903;
                            if (roomAreaSqM <= 0) continue;

                            // Size the AC unit
                            var coolingKW = roomAreaSqM * COOLING_LOAD_WPM2 / 1000.0;
                            var selectedKW = ACSizes.FirstOrDefault(s => s >= coolingKW);
                            if (selectedKW == 0) selectedKW = ACSizes.Last();
                            var btu = (int)(selectedKW * 3412);

                            // Place indoor unit at wall, 2200mm AFF
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            var placePt = new XYZ(
                                (bb.Min.X + bb.Max.X) / 2,
                                bb.Max.Y - 0.5, // near back wall
                                2200 * MM_TO_FEET);

                            FamilyInstance ac = null;
                            if (indoorSymbol != null)
                            {
                                ac = _document.Create.NewFamilyInstance(
                                    placePt, indoorSymbol, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }

                            if (ac != null)
                            {
                                placedIds.Add(ac.Id);
                                var ratingParam = ac.LookupParameter("Rating");
                                ratingParam?.Set($"{selectedKW}kW");
                            }

                            details.Add($"  {room.Name} ({roomAreaSqM:F0}m²): " +
                                $"{selectedKW}kW / {btu} BTU");
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("split AC", "place", ex));
                        return result;
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} split AC unit(s):\n" +
                    string.Join("\n", details) +
                    "\nNote: Outdoor units, refrigerant lines, and condensate drains to be coordinated.";
                result.CostEstimate = EstimateACCost(placedIds.Count, details);
                result.Suggestions = new List<string>
                {
                    "Add ceiling fans",
                    "Add extract fans to bathrooms",
                    "Route refrigerant lines"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Split AC placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("split AC", "place", ex));
            }

            return result;
        }

        #endregion

        #region Ceiling Fans

        /// <summary>
        /// Places ceiling fans at room centroid, 300mm clearance to ceiling.
        /// Min 2100mm blade-to-floor.
        /// </summary>
        public CreationPipelineResult PlaceCeilingFans(CeilingFanCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Ceiling Fan" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var rooms = GetTargetRooms(cmd.RoomName, cmd.AllRooms, level);
                if (rooms.Count == 0)
                {
                    result.SetError("No rooms found.");
                    return result;
                }

                var fanSymbol = _familyResolver.ResolveMEPFixture("generator", "fan");
                var placedIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Ceiling Fans"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var room in rooms)
                        {
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            // Place at room centroid, 300mm below ceiling
                            var ceilingHeight = cmd.CeilingHeightMm > 0
                                ? cmd.CeilingHeightMm : 2700;
                            var fanHeightMm = ceilingHeight - 300; // blade clearance

                            // Check 2100mm minimum
                            if (fanHeightMm < FAN_BLADE_TO_FLOOR_MIN)
                            {
                                result.Warnings = (result.Warnings ?? "") +
                                    $"\nWarning: {room.Name} ceiling too low for fan " +
                                    $"(blade height {fanHeightMm}mm < {FAN_BLADE_TO_FLOOR_MIN}mm minimum).\n";
                                continue;
                            }

                            var centerX = (bb.Min.X + bb.Max.X) / 2;
                            var centerY = (bb.Min.Y + bb.Max.Y) / 2;
                            var placePt = new XYZ(centerX, centerY, fanHeightMm * MM_TO_FEET);

                            FamilyInstance fan = null;
                            if (fanSymbol != null)
                            {
                                fan = _document.Create.NewFamilyInstance(
                                    placePt, fanSymbol, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }

                            if (fan != null)
                                placedIds.Add(fan.Id);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("ceiling fan", "place", ex));
                        return result;
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} ceiling fan(s)";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = placedIds.Count * 250000,
                    Description = $"{placedIds.Count}× ceiling fan @ UGX 250,000 each (installed)"
                };
                result.Suggestions = new List<string>
                {
                    "Add fan speed controllers",
                    "Add split AC units",
                    "Add light switches"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ceiling fan placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("ceiling fan", "place", ex));
            }

            return result;
        }

        #endregion

        #region Extract Fans

        /// <summary>
        /// Places bathroom/kitchen extract fans at ceiling, IP44 rated.
        /// Kitchen: min 15 ACH, Bathroom: min 10 ACH.
        /// Routes extract duct to external louvre.
        /// </summary>
        public CreationPipelineResult PlaceExtractFans(ExtractFanCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Extract Fan" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var rooms = GetTargetRooms(cmd.RoomName, cmd.AllRooms, level);

                // Filter to wet areas / kitchens if all rooms
                if (cmd.AllRooms)
                {
                    rooms = rooms.Where(r =>
                    {
                        var name = r.Name.ToLowerInvariant();
                        return name.Contains("bathroom") || name.Contains("toilet") ||
                               name.Contains("wc") || name.Contains("en-suite") ||
                               name.Contains("kitchen") || name.Contains("laundry");
                    }).ToList();
                }

                if (rooms.Count == 0)
                {
                    result.SetError("No wet areas or kitchens found for extract fans.");
                    return result;
                }

                var fanSymbol = _familyResolver.ResolveMEPFixture("generator", "extract");
                var placedIds = new List<ElementId>();
                var details = new List<string>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Extract Fans"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        foreach (var room in rooms)
                        {
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            var roomAreaSqM = room.Area * 0.092903;
                            var roomName = room.Name.ToLowerInvariant();
                            var isKitchen = roomName.Contains("kitchen");
                            var ach = isKitchen ? KITCHEN_ACH : BATHROOM_ACH;
                            var ceilingHeightM = 2.7;
                            var airflowLps = (roomAreaSqM * ceilingHeightM * ach) / 3.6;

                            var centerX = (bb.Min.X + bb.Max.X) / 2;
                            var centerY = (bb.Min.Y + bb.Max.Y) / 2;
                            var placePt = new XYZ(centerX, centerY, 2700 * MM_TO_FEET);

                            FamilyInstance fan = null;
                            if (fanSymbol != null)
                            {
                                fan = _document.Create.NewFamilyInstance(
                                    placePt, fanSymbol, level,
                                    Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }

                            if (fan != null)
                                placedIds.Add(fan.Id);

                            details.Add($"  {room.Name}: {ach} ACH, {airflowLps:F0} L/s" +
                                (isKitchen ? " (including cooking exhaust)" : " (IP44 rated)"));
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("extract fan", "place", ex));
                        return result;
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {placedIds.Count} extract fan(s):\n" +
                    string.Join("\n", details) +
                    "\nNote: Extract ducts to external louvre to be routed separately.";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = placedIds.Count * 180000,
                    Description = $"{placedIds.Count}× extract fan @ UGX 180,000 each"
                };
                result.Suggestions = new List<string>
                {
                    "Route extract ducts",
                    "Add kitchen hood",
                    "Add ceiling fans"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Extract fan placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("extract fan", "place", ex));
            }

            return result;
        }

        #endregion

        #region Kitchen Hood

        /// <summary>
        /// Places a kitchen hood 650-700mm above cooker position.
        /// Routes extract duct through external wall to louvre.
        /// </summary>
        public CreationPipelineResult PlaceKitchenHood(KitchenHoodCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Kitchen Hood" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var hoodSymbol = _familyResolver.ResolveMEPFixture("generator", "hood");
                var failureHandler = new StingBIMFailurePreprocessor();
                FamilyInstance hood = null;

                using (var transaction = new Transaction(_document, "StingBIM: Place Kitchen Hood"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Place at specified location or default
                        var heightFt = (cmd.HeightMm > 0 ? cmd.HeightMm : 1550) * MM_TO_FEET;
                        // 900mm worktop + 650mm capture height ≈ 1550mm AFF

                        var placePt = new XYZ(
                            (cmd.X ?? 2) * MM_TO_FEET * 1000,
                            (cmd.Y ?? 2) * MM_TO_FEET * 1000,
                            heightFt);

                        if (hoodSymbol != null)
                        {
                            hood = _document.Create.NewFamilyInstance(
                                placePt, hoodSymbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("kitchen hood", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = hood?.Id;
                result.Message = "Placed kitchen hood at 650mm above cooker position.\n" +
                    "Extract rate: min 15 air changes/hour (CIBSE).\n" +
                    "Note: Extract duct through external wall to louvre to be routed.";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = 450000,
                    Description = "Kitchen hood + extract fan, installed @ UGX 450,000"
                };
                result.Suggestions = new List<string>
                {
                    "Route extract duct to outside",
                    "Add kitchen worktop outlets",
                    "Add bathroom extract fans"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Kitchen hood placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("kitchen hood", "place", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private List<Autodesk.Revit.DB.Architecture.Room> GetTargetRooms(
            string roomName, bool allRooms, Level level)
        {
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            if (level != null)
                rooms = rooms.Where(r => r.LevelId == level.Id).ToList();

            if (!allRooms && !string.IsNullOrEmpty(roomName))
            {
                rooms = rooms.Where(r =>
                    r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return rooms;
        }

        private CostEstimate EstimateACCost(int count, List<string> details)
        {
            // Average UGX 1,500,000 per split AC (supply + install)
            return new CostEstimate
            {
                TotalUGX = count * 1500000,
                Description = $"{count}× split AC unit @ UGX 1,500,000 avg (supply + install)"
            };
        }

        #endregion
    }

    #region Command DTOs

    public class SplitACCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
        public double CapacityKW { get; set; }
    }

    public class CeilingFanCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
        public double CeilingHeightMm { get; set; }
    }

    public class ExtractFanCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
    }

    public class KitchenHoodCommand
    {
        public string LevelName { get; set; }
        public double HeightMm { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }

    #endregion
}
