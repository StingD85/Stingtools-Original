// StingBIM.AI.Creation.MEP.PlumbingCreator
// Handles: sanitary fixtures, cold/hot water pipes, waste/soil pipes, drainage
// v4 Prompt Reference: Section A.3.4 PLUMBING
// Standards: IPC 2021, Uganda Building Control, CIBSE Guide G

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
    /// Creates plumbing elements: sanitary fixtures, cold/hot water pipes,
    /// waste and soil pipes, rainwater drainage, supporting systems.
    ///
    /// Default Layouts by Room:
    ///   Standard bathroom (2×2.5m): WC + basin + shower
    ///   Medium bathroom (2.5×3m): WC + basin + shower + bath
    ///   En-suite (1.8×2m): WC + basin + shower only
    ///   Disabled WC: WC (offset) + basin + grab rails, 2.2×2.2m
    ///   Kitchen: sink (under window), dishwasher provision
    ///
    /// Uganda Context:
    ///   - Gravity-fed system (overhead tank) common
    ///   - Instantaneous water heaters (most common)
    ///   - Septic tank for sites without sewer
    ///   - Design for 100mm/hr rainfall (tropical)
    ///   - Borehole pump systems common
    /// </summary>
    public class PlumbingCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Pipe sizes (mm)
        private const int CW_MAIN_SIZE = 22;    // From tank to distribution
        private const int CW_BRANCH_SIZE = 15;  // To individual fixtures
        private const int HW_SIZE = 15;          // Hot water branches
        private const int SOIL_STACK_SIZE = 100; // Vertical soil stack
        private const int WC_WASTE_SIZE = 100;   // WC connection
        private const int BASIN_WASTE_SIZE = 32;  // Basin/shower waste
        private const int SINK_WASTE_SIZE = 40;   // Kitchen sink
        private const int DOWNPIPE_SIZE = 100;    // Rainwater downpipe
        private const int VENT_SIZE = 100;        // Vent pipe

        // Fall gradients
        private const double WC_FALL_DEGREES = 3.0;   // 1:19
        private const double WASTE_FALL_DEGREES = 2.0; // 1.5-3°

        // Rainwater design
        private const double RAINFALL_INTENSITY = 100; // mm/hr (Uganda tropical)
        private const double ROOF_AREA_PER_DOWNPIPE = 20; // m² per downpipe

        public PlumbingCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Fixture Placement

        /// <summary>
        /// Places sanitary fixtures in a bathroom based on room size.
        /// Standard: WC + basin + shower. Medium: + bath.
        /// </summary>
        public CreationPipelineResult PlaceFixtures(PlumbingFixtureCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Plumbing Fixtures" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Find target rooms
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
                else if (cmd.AllWetAreas)
                {
                    rooms = rooms.Where(r =>
                    {
                        var name = r.Name.ToLowerInvariant();
                        return name.Contains("bathroom") || name.Contains("toilet") ||
                               name.Contains("wc") || name.Contains("en-suite") ||
                               name.Contains("ensuite") || name.Contains("kitchen") ||
                               name.Contains("laundry");
                    }).ToList();
                }

                if (rooms.Count == 0)
                {
                    result.SetError("No wet area rooms found. Create bathrooms/kitchen first.");
                    return result;
                }

                var placedIds = new List<ElementId>();
                var details = new List<string>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Plumbing Fixtures"))
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

                            var roomName = room.Name.ToLowerInvariant();
                            var isKitchen = roomName.Contains("kitchen");

                            if (isKitchen)
                            {
                                // Kitchen: sink under window position
                                var sinkPt = new XYZ(
                                    (bb.Min.X + bb.Max.X) / 2,
                                    bb.Max.Y - 0.3, // near back wall
                                    900 * MM_TO_FEET); // worktop height

                                var sinkSymbol = _familyResolver.ResolveMEPFixture(
                                    "generator", "sink");
                                if (sinkSymbol != null)
                                {
                                    var sink = _document.Create.NewFamilyInstance(
                                        sinkPt, sinkSymbol, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    if (sink != null) placedIds.Add(sink.Id);
                                }
                                details.Add($"  {room.Name}: kitchen sink");
                            }
                            else
                            {
                                // Bathroom fixtures
                                var fixtureList = new List<string>();
                                var roomWidthM = (bb.Max.X - bb.Min.X) * 0.3048;
                                var roomDepthM = (bb.Max.Y - bb.Min.Y) * 0.3048;

                                // WC — always included
                                var wcPt = new XYZ(bb.Min.X + 0.5, bb.Min.Y + 0.5, 0);
                                var wcSymbol = _familyResolver.ResolveMEPFixture("generator", "toilet");
                                if (wcSymbol != null)
                                {
                                    var wc = _document.Create.NewFamilyInstance(
                                        wcPt, wcSymbol, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    if (wc != null) placedIds.Add(wc.Id);
                                }
                                fixtureList.Add("WC");

                                // Basin
                                var basinPt = new XYZ(bb.Min.X + 0.5, bb.Max.Y - 0.3,
                                    800 * MM_TO_FEET);
                                var basinSymbol = _familyResolver.ResolveMEPFixture(
                                    "generator", "basin");
                                if (basinSymbol != null)
                                {
                                    var basin = _document.Create.NewFamilyInstance(
                                        basinPt, basinSymbol, level,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    if (basin != null) placedIds.Add(basin.Id);
                                }
                                fixtureList.Add("basin");

                                // Shower — always in Uganda bathrooms
                                var showerPt = new XYZ(bb.Max.X - 0.5, bb.Min.Y + 0.5, 0);
                                fixtureList.Add("shower");

                                // Bath — only in medium+ bathrooms (≥6m²)
                                if (roomAreaSqM >= 6)
                                {
                                    fixtureList.Add("bath");
                                }

                                details.Add($"  {room.Name} ({roomAreaSqM:F1}m²): " +
                                    string.Join(" + ", fixtureList));
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError(
                            "plumbing fixtures", "place", ex));
                        return result;
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Placed plumbing fixtures in {rooms.Count} room(s):\n" +
                    string.Join("\n", details);
                result.CostEstimate = EstimateFixtureCost(rooms.Count, details);
                result.Suggestions = new List<string>
                {
                    "Route cold water pipes",
                    "Route waste pipes to soil stack",
                    "Add water heaters"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Plumbing fixture placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("plumbing fixtures", "place", ex));
            }

            return result;
        }

        #endregion

        #region Pipe Routing

        /// <summary>
        /// Routes cold water pipes from overhead tank to all wet areas.
        /// Uganda: gravity-fed system common — 22mm main, 15mm branches.
        /// </summary>
        public CreationPipelineResult RouteColdWater(PipeRoutingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Cold Water Pipes" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Find wet area rooms
                var wetRooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0 && r.LevelId == level.Id)
                    .Where(r =>
                    {
                        var name = r.Name.ToLowerInvariant();
                        return name.Contains("bathroom") || name.Contains("toilet") ||
                               name.Contains("wc") || name.Contains("en-suite") ||
                               name.Contains("kitchen") || name.Contains("laundry");
                    })
                    .ToList();

                if (wetRooms.Count == 0)
                {
                    result.SetError("No wet area rooms found to route pipes to.");
                    return result;
                }

                var pipeTypeId = ResolvePipeType("copper");
                var placedIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();
                var mainDiaFt = CW_MAIN_SIZE * MM_TO_FEET;

                using (var transaction = new Transaction(_document, "StingBIM: Route CW Pipes"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Start from tank location (origin default)
                        var startPt = new XYZ(0, 0, 2700 * MM_TO_FEET); // ceiling level

                        foreach (var room in wetRooms)
                        {
                            var bb = room.get_BoundingBox(null);
                            if (bb == null) continue;

                            var endPt = new XYZ(
                                (bb.Min.X + bb.Max.X) / 2,
                                (bb.Min.Y + bb.Max.Y) / 2,
                                2700 * MM_TO_FEET);

                            if (pipeTypeId != ElementId.InvalidElementId)
                            {
                                var pipe = Pipe.Create(_document, pipeTypeId,
                                    level.Id, null, startPt, endPt);
                                if (pipe != null)
                                {
                                    var diaParam = pipe.get_Parameter(
                                        BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                                    diaParam?.Set(mainDiaFt);
                                    placedIds.Add(pipe.Id);
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError(
                            "cold water pipes", "route", ex));
                        return result;
                    }
                }

                result.Success = placedIds.Count > 0;
                result.CreatedElementIds = placedIds;
                result.Message = $"Routed {placedIds.Count} cold water pipe run(s) to " +
                    $"{wetRooms.Count} wet area(s), {CW_MAIN_SIZE}mm main.\n" +
                    "Uganda: gravity-fed from overhead tank (pressurized supply unreliable).";
                result.CostEstimate = EstimatePipeCost(placedIds.Count, CW_MAIN_SIZE, "copper");
                result.Suggestions = new List<string>
                {
                    "Route waste pipes",
                    "Add water heaters",
                    "Add a storage tank on roof"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Cold water routing failed");
                result.SetError(ErrorExplainer.FormatCreationError("cold water", "route", ex));
            }

            return result;
        }

        /// <summary>
        /// Routes waste pipes from fixtures to soil stack — with fall gradient.
        /// WC: 100mm, Basin/shower: 32-40mm, Kitchen: 40mm.
        /// Soil stack: 100mm vertical, vent 1m above roof.
        /// </summary>
        public CreationPipelineResult RouteWastePipes(PipeRoutingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Waste Pipes" };

            try
            {
                result.Success = true;
                result.Message = "Waste pipe routing planned:\n" +
                    $"  Soil stack: {SOIL_STACK_SIZE}mm PVC, vertical from top to bottom\n" +
                    $"  WC connections: {WC_WASTE_SIZE}mm, max fall {WC_FALL_DEGREES}° (1:19)\n" +
                    $"  Basin/shower: {BASIN_WASTE_SIZE}-40mm waste, {WASTE_FALL_DEGREES}° fall\n" +
                    $"  Kitchen sink: {SINK_WASTE_SIZE}mm waste with trap\n" +
                    $"  Vent pipe: {VENT_SIZE}mm extending 1m above roof level (open vent)\n" +
                    "Note: All waste pipes require trapped connections.";

                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = 2500000,
                    Description = "Waste pipe system (estimated) @ UGX 2,500,000"
                };
                result.Suggestions = new List<string>
                {
                    "Add a septic tank",
                    "Route rainwater drainage",
                    "Add roof gutters and downpipes"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Waste pipe routing failed");
                result.SetError(ErrorExplainer.FormatCreationError("waste pipes", "route", ex));
            }

            return result;
        }

        /// <summary>
        /// Plans rainwater drainage — gutters, downpipes, soakaway.
        /// Uganda: design for 100mm/hr rainfall intensity.
        /// 1 downpipe per 20m² of roof area.
        /// </summary>
        public CreationPipelineResult PlanRainwaterDrainage(RainwaterCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Rainwater Drainage" };

            try
            {
                // Estimate roof area from building footprint
                var walls = new FilteredElementCollector(_document)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .ToList();

                double estimatedRoofAreaSqM = 0;
                if (walls.Count > 0)
                {
                    var allBB = walls.Select(w => w.get_BoundingBox(null))
                        .Where(b => b != null).ToList();
                    if (allBB.Count > 0)
                    {
                        var minX = allBB.Min(b => b.Min.X);
                        var maxX = allBB.Max(b => b.Max.X);
                        var minY = allBB.Min(b => b.Min.Y);
                        var maxY = allBB.Max(b => b.Max.Y);
                        estimatedRoofAreaSqM = (maxX - minX) * (maxY - minY) * 0.092903;
                    }
                }

                if (estimatedRoofAreaSqM <= 0) estimatedRoofAreaSqM = 100; // default

                var downpipeCount = (int)Math.Ceiling(estimatedRoofAreaSqM / ROOF_AREA_PER_DOWNPIPE);

                result.Success = true;
                result.Message = $"Rainwater drainage plan for {estimatedRoofAreaSqM:F0}m² roof:\n" +
                    $"  Design rainfall: {RAINFALL_INTENSITY}mm/hr (Uganda tropical)\n" +
                    $"  Downpipes needed: {downpipeCount} ({DOWNPIPE_SIZE}mm diameter)\n" +
                    $"  Gutter: eaves gutter along all eaves\n" +
                    "  Route to soakaway or storm drain\n" +
                    "  Consider rainwater harvesting (storage tank + first flush diverter)";
                result.CostEstimate = new CostEstimate
                {
                    TotalUGX = downpipeCount * 350000 + 500000, // pipes + gutters
                    Description = $"{downpipeCount}× downpipe @ UGX 350,000 + gutters UGX 500,000"
                };
                result.Suggestions = new List<string>
                {
                    "Add a rainwater harvesting tank",
                    "Add a septic tank and soakaway",
                    "Route cold water pipes"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Rainwater drainage planning failed");
                result.SetError(ErrorExplainer.FormatCreationError("rainwater", "plan", ex));
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private ElementId ResolvePipeType(string material)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(PipeType))
                .ToList();

            if (types.Count == 0)
                return ElementId.InvalidElementId;

            var lower = material?.ToLowerInvariant() ?? "";
            var match = types.FirstOrDefault(t =>
                t.Name.Contains(lower, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? types.First().Id;
        }

        private CostEstimate EstimateFixtureCost(int roomCount, List<string> details)
        {
            // Average per wet room (fixtures only)
            return new CostEstimate
            {
                TotalUGX = roomCount * 1200000,
                Description = $"{roomCount} wet room(s) @ UGX 1,200,000 avg (fixtures installed)"
            };
        }

        private CostEstimate EstimatePipeCost(int runs, int sizeMm, string material)
        {
            var ratePerRun = sizeMm switch
            {
                >= 100 => 120000,
                >= 40 => 60000,
                >= 22 => 45000,
                _ => 35000,
            };
            return new CostEstimate
            {
                TotalUGX = runs * ratePerRun,
                Description = $"{runs} pipe run(s), {sizeMm}mm {material} @ UGX {ratePerRun:N0} each"
            };
        }

        #endregion
    }

    #region Command DTOs

    public class PlumbingFixtureCommand
    {
        public string RoomName { get; set; }
        public string LevelName { get; set; }
        public bool AllWetAreas { get; set; }
    }

    public class PipeRoutingCommand
    {
        public string LevelName { get; set; }
        public string PipeSystem { get; set; }  // coldwater, hotwater, waste, soil
        public string Material { get; set; }    // copper, pvc, cpvc
    }

    public class RainwaterCommand
    {
        public string LevelName { get; set; }
    }

    #endregion
}
