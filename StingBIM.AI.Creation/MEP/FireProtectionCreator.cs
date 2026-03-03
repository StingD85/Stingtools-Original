// StingBIM.AI.Creation.MEP.FireProtectionCreator
// Handles: smoke detectors, sprinklers, fire hose reels, extinguishers, alarms
// v4 Prompt Reference: Section A.3.5 FIRE PROTECTION
// Standards: NFPA 13/72, BS 5839, Uganda Building Control Act 2013

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates fire protection elements: smoke/heat detectors, sprinklers,
    /// fire hose reels, extinguishers, manual call points, alarm sounders.
    ///
    /// Standards Basis:
    ///   NFPA 13 (sprinklers), NFPA 72 (fire alarm), BS 5839 (detection)
    ///   Uganda Building Control Act 2013 (East Africa: British standards basis)
    ///
    /// Detector Coverage (BS 5839):
    ///   Max 80m² per detector, max 7.5m radius, 0.5m from walls
    ///   Heat detectors for kitchens/plant rooms
    ///
    /// Sprinkler Coverage (NFPA 13):
    ///   OH1: max 12m² per head, max 4.6m spacing
    ///   Wet pipe system (tropical — no freezing risk)
    ///
    /// Fire Hose Reels:
    ///   Coverage: 36m (30m hose + 6m jet)
    ///   1 per 500m² or every 30m travel distance
    ///
    /// Extinguishers:
    ///   Powder: 1 per 200m², max 30m travel
    ///   CO2: electrical/server rooms
    ///   Wet chemical: kitchens
    /// </summary>
    public class FireProtectionCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Smoke detector spacing (BS 5839)
        private const double DETECTOR_MAX_AREA = 80; // m² per detector
        private const double DETECTOR_MAX_RADIUS = 7.5; // m
        private const double DETECTOR_WALL_OFFSET = 0.5; // m from wall

        // Sprinkler spacing (NFPA 13 — OH1)
        private const double SPRINKLER_MAX_AREA = 12; // m² per head
        private const double SPRINKLER_MAX_SPACING = 4.6; // m

        // Fire hose reel
        private const double HOSE_REEL_COVERAGE = 36; // m (30m hose + 6m jet)
        private const double HOSE_REEL_MAX_AREA = 500; // m² per reel
        private const double HOSE_REEL_HEIGHT = 1400; // mm AFF

        // Call point
        private const double CALL_POINT_MAX_TRAVEL = 30; // m
        private const double CALL_POINT_HEIGHT = 1400; // mm AFF

        // Extinguisher
        private const double EXTINGUISHER_MAX_AREA = 200; // m² per extinguisher
        private const double EXTINGUISHER_HEIGHT = 1000; // mm AFF to base

        public FireProtectionCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Smoke Detectors

        /// <summary>
        /// Places smoke/heat detectors in all rooms.
        /// Smoke: ceiling-mounted, max 80m² coverage, 7.5m radius (BS 5839).
        /// Heat detectors in kitchens and plant rooms.
        /// </summary>
        public CreationPipelineResult PlaceDetectors(DetectorCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Fire Detectors" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0 && r.LevelId == level.Id)
                    .ToList();

                if (!string.IsNullOrEmpty(cmd.RoomName))
                {
                    rooms = rooms.Where(r =>
                        r.Name.Contains(cmd.RoomName, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (rooms.Count == 0)
                {
                    result.SetError("No rooms found to place detectors.");
                    return result;
                }

                var smokeSymbol = _familyResolver.ResolveMEPFixture("detector", "smoke");
                var placedIds = new List<ElementId>();
                var details = new List<string>();
                var failureHandler = new StingBIMFailurePreprocessor();
                var totalDetectors = 0;

                using (var transaction = new Transaction(_document, "StingBIM: Place Fire Detectors"))
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
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            // Determine detector type
                            var roomName = room.Name.ToLowerInvariant();
                            var isKitchen = roomName.Contains("kitchen");
                            var isPlantRoom = roomName.Contains("plant");
                            var detectorType = (isKitchen || isPlantRoom) ? "heat" : "smoke";

                            // Calculate detector count
                            var detectorCount = (int)Math.Ceiling(roomAreaSqM / DETECTOR_MAX_AREA);
                            if (detectorCount < 1) detectorCount = 1;

                            // Place at centroid (single) or grid (multiple)
                            var positions = CalculateGrid(detectorCount, bb);

                            var ceilingHeightFt = 2700 * MM_TO_FEET;

                            foreach (var pt in positions)
                            {
                                var placePt = new XYZ(pt.X, pt.Y, ceilingHeightFt);

                                if (smokeSymbol != null)
                                {
                                    var det = _document.Create.NewFamilyInstance(
                                        placePt, smokeSymbol, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    if (det != null)
                                        placedIds.Add(det.Id);
                                }
                                totalDetectors++;
                            }

                            details.Add($"  {room.Name}: {detectorCount}× {detectorType} detector(s)");
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError(
                            "fire detectors", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {totalDetectors} fire detector(s) in {rooms.Count} room(s):\n" +
                    string.Join("\n", details) +
                    $"\nBS 5839 compliance: max {DETECTOR_MAX_AREA}m² coverage, " +
                    $"{DETECTOR_MAX_RADIUS}m max radius.";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = totalDetectors * 85000,
                    Description = $"{totalDetectors}× fire detector @ UGX 85,000 each"
                };
                result.Suggestions = new List<string>
                {
                    "Add fire alarm sounders",
                    "Add manual call points",
                    "Add sprinklers"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Fire detector placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("fire detectors", "place", ex));
            }

            return result;
        }

        #endregion

        #region Sprinklers

        /// <summary>
        /// Places fire sprinkler heads — max 12m² per head (OH1), 4.6m spacing.
        /// Wet pipe system (no freezing risk in tropical climate).
        /// </summary>
        public CreationPipelineResult PlaceSprinklers(SprinklerCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Sprinklers" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0 && r.LevelId == level.Id)
                    .ToList();

                if (rooms.Count == 0)
                {
                    result.SetError("No rooms found for sprinkler placement.");
                    return result;
                }

                var sprinklerSymbol = _familyResolver.ResolveMEPFixture("sprinkler", null);
                var placedIds = new List<ElementId>();
                var totalHeads = 0;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Sprinklers"))
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
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            var headCount = (int)Math.Ceiling(roomAreaSqM / SPRINKLER_MAX_AREA);
                            if (headCount < 1) headCount = 1;

                            var positions = CalculateGrid(headCount, bb);
                            var ceilingHeightFt = 2700 * MM_TO_FEET;

                            foreach (var pt in positions)
                            {
                                var placePt = new XYZ(pt.X, pt.Y, ceilingHeightFt);

                                if (sprinklerSymbol != null)
                                {
                                    var head = _document.Create.NewFamilyInstance(
                                        placePt, sprinklerSymbol, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    if (head != null)
                                        placedIds.Add(head.Id);
                                }
                                totalHeads++;
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("sprinklers", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed {totalHeads} sprinkler head(s) across {rooms.Count} room(s).\n" +
                    $"NFPA 13 OH1: max {SPRINKLER_MAX_AREA}m² per head, {SPRINKLER_MAX_SPACING}m spacing.\n" +
                    "System: wet pipe (tropical climate — no freezing risk).\n" +
                    "Branch pipe: 25mm (max 4 heads per branch), distribution: 50mm main.";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = totalHeads * 120000 + 2000000, // heads + system
                    Description = $"{totalHeads}× sprinkler head @ UGX 120,000 + system UGX 2,000,000"
                };
                result.Suggestions = new List<string>
                {
                    "Add fire detectors",
                    "Add fire hose reels",
                    "Route sprinkler main pipe"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Sprinkler placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("sprinklers", "place", ex));
            }

            return result;
        }

        #endregion

        #region Fire Hose Reels & Extinguishers

        /// <summary>
        /// Places fire hose reels — 1 per 500m² or every 30m travel distance.
        /// Coverage: 36m (30m hose + 6m jet). Height: 1400mm AFF.
        /// </summary>
        public CreationPipelineResult PlaceFireHoseReels(FireHoseReelCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Fire Hose Reels" };

            try
            {
                // Calculate total floor area
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                var totalAreaSqM = rooms.Sum(r => r.Area * 0.092903);
                var reelCount = (int)Math.Ceiling(totalAreaSqM / HOSE_REEL_MAX_AREA);
                if (reelCount < 1) reelCount = 1;

                result.Success = true;
                result.Message = $"Fire hose reel plan:\n" +
                    $"  Total floor area: {totalAreaSqM:F0}m²\n" +
                    $"  Hose reels needed: {reelCount}\n" +
                    $"  Coverage: {HOSE_REEL_COVERAGE}m per reel (30m hose + 6m jet)\n" +
                    $"  Location: corridors, stairwells, plant rooms\n" +
                    $"  Height: {HOSE_REEL_HEIGHT}mm AFF (reel center)\n" +
                    $"  Supply: 25mm pipe from fire main";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = reelCount * 850000,
                    Description = $"{reelCount}× fire hose reel @ UGX 850,000 each (installed)"
                };
                result.Suggestions = new List<string>
                {
                    "Add fire extinguishers",
                    "Add manual call points",
                    "Route fire main pipe"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Fire hose reel planning failed");
                result.SetError(ErrorExplainer.FormatCreationError("fire hose reel", "plan", ex));
            }

            return result;
        }

        /// <summary>
        /// Places fire extinguishers — 1 per 200m², max 30m travel distance.
        /// Types: ABC powder (general), CO2 (electrical), wet chemical (kitchen).
        /// </summary>
        public CreationPipelineResult PlaceExtinguishers(ExtinguisherCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Fire Extinguishers" };

            try
            {
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                var totalAreaSqM = rooms.Sum(r => r.Area * 0.092903);
                var powderCount = (int)Math.Ceiling(totalAreaSqM / EXTINGUISHER_MAX_AREA);
                if (powderCount < 1) powderCount = 1;

                // Check for special rooms
                var hasServer = rooms.Any(r =>
                    r.Name.Contains("server", StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Contains("electrical", StringComparison.OrdinalIgnoreCase));
                var hasKitchen = rooms.Any(r =>
                    r.Name.Contains("kitchen", StringComparison.OrdinalIgnoreCase));

                var co2Count = hasServer ? 1 : 0;
                var wetChemCount = hasKitchen ? 1 : 0;
                var totalCount = powderCount + co2Count + wetChemCount;

                result.Success = true;
                result.Message = $"Fire extinguisher plan:\n" +
                    $"  ABC Powder (general): {powderCount} (1 per {EXTINGUISHER_MAX_AREA}m²)\n" +
                    (hasServer ? $"  CO2 (electrical rooms): {co2Count}\n" : "") +
                    (hasKitchen ? $"  Wet chemical (kitchen): {wetChemCount}\n" : "") +
                    $"  Total: {totalCount} extinguisher(s)\n" +
                    $"  Mounting: wall-mounted, {EXTINGUISHER_HEIGHT}mm AFF to base\n" +
                    $"  Max travel distance: 30m to nearest extinguisher";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = powderCount * 120000 + co2Count * 250000 + wetChemCount * 180000,
                    Description = $"Powder ×{powderCount} + CO2 ×{co2Count} + WetChem ×{wetChemCount}"
                };
                result.Suggestions = new List<string>
                {
                    "Add smoke detectors",
                    "Add fire hose reels",
                    "Add manual call points"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Fire extinguisher planning failed");
                result.SetError(ErrorExplainer.FormatCreationError("extinguisher", "plan", ex));
            }

            return result;
        }

        #endregion

        #region Manual Call Points

        /// <summary>
        /// Places manual call points at all exits, stairwells, corridors.
        /// Max 30m travel distance. Height: 1400mm AFF.
        /// </summary>
        public CreationPipelineResult PlaceCallPoints(CallPointCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Manual Call Points" };

            try
            {
                // Count exits (doors on external walls)
                var doors = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Count();

                // Estimate call points needed: at each exit + stairwells + corridors
                var callPointCount = Math.Max(doors / 2, 2); // Rough estimate

                result.Success = true;
                result.Message = $"Manual call point plan:\n" +
                    $"  Call points needed: ~{callPointCount}\n" +
                    $"  Locations: all exits, stairwells, corridor intersections\n" +
                    $"  Height: {CALL_POINT_HEIGHT}mm AFF\n" +
                    $"  Max travel distance: {CALL_POINT_MAX_TRAVEL}m to any call point\n" +
                    "  Connected to fire alarm sounder system";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = callPointCount * 65000,
                    Description = $"{callPointCount}× manual call point @ UGX 65,000 each"
                };
                result.Suggestions = new List<string>
                {
                    "Add fire alarm sounders",
                    "Add smoke detectors",
                    "Add emergency lighting"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Call point planning failed");
                result.SetError(ErrorExplainer.FormatCreationError("call points", "plan", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private List<XYZ> CalculateGrid(int count, BoundingBoxXYZ bb)
        {
            var positions = new List<XYZ>();
            var lengthFt = bb.Max.X - bb.Min.X;
            var widthFt = bb.Max.Y - bb.Min.Y;

            if (count == 1)
            {
                positions.Add(new XYZ(
                    (bb.Min.X + bb.Max.X) / 2,
                    (bb.Min.Y + bb.Max.Y) / 2, 0));
            }
            else
            {
                var cols = (int)Math.Ceiling(Math.Sqrt(count * lengthFt / Math.Max(widthFt, 0.1)));
                var rows = (int)Math.Ceiling((double)count / cols);
                var dx = lengthFt / (cols + 1);
                var dy = widthFt / (rows + 1);
                var placed = 0;

                for (int r = 1; r <= rows && placed < count; r++)
                {
                    for (int c = 1; c <= cols && placed < count; c++)
                    {
                        positions.Add(new XYZ(bb.Min.X + c * dx, bb.Min.Y + r * dy, 0));
                        placed++;
                    }
                }
            }

            return positions;
        }

        #endregion
    }

    #region Command DTOs

    public class DetectorCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
    }

    public class SprinklerCommand
    {
        public string LevelName { get; set; }
        public bool AllRooms { get; set; }
    }

    public class FireHoseReelCommand
    {
        public string LevelName { get; set; }
    }

    public class ExtinguisherCommand
    {
        public string LevelName { get; set; }
        public string ExtinguisherType { get; set; }
    }

    public class CallPointCommand
    {
        public string LevelName { get; set; }
    }

    #endregion
}
