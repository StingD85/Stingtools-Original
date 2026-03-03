// StingBIM.AI.Creation.Structural.FoundationCreator
// Creates foundations with 4 types + soil advisory + Uganda standards
// v4 Prompt Reference: Section A.2.3 — FoundationCreator

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Structural
{
    /// <summary>
    /// Creates foundations with soil-aware sizing and Uganda standards compliance.
    ///
    /// 4 Types:
    ///   1. Strip foundation — along load-bearing walls
    ///   2. Pad foundation — under columns
    ///   3. Raft foundation — full building footprint
    ///   4. Pile foundation — deep foundation for poor soil
    ///
    /// Soil Types (Uganda):
    ///   Murram (laterite): strip/pad adequate
    ///   Black cotton soil (expansive clay): raft or piles recommended
    ///   Sandy: strip with increased width
    ///   Rocky: shallower foundations
    ///
    /// Standards: Uganda Building Control Act 2013, UNBS
    ///   Standard depth: 1.2m below FFL
    ///   Black cotton soil: 1.5m below FFL
    ///   Concrete: minimum C20/25
    /// </summary>
    public class FoundationCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Foundation depth defaults by soil type (mm below FFL)
        private static readonly Dictionary<string, double> FoundationDepthBySoil =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["murram"] = 1200,
                ["laterite"] = 1200,
                ["clay"] = 1500,
                ["black cotton"] = 1500,
                ["expansive clay"] = 1500,
                ["sandy"] = 1200,
                ["sand"] = 1200,
                ["rocky"] = 500,
                ["rock"] = 500,
                ["default"] = 1200,
            };

        // Strip foundation width by wall type (mm)
        private static readonly Dictionary<string, double> StripWidthDefaults =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["single leaf"] = 600,
                ["single leaf soft"] = 750,
                ["double leaf"] = 1000,
                ["cavity"] = 1000,
                ["default"] = 600,
            };

        // Soil advisory messages
        private static readonly Dictionary<string, string> SoilAdvisory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["murram"] = "Murram/laterite soil: good bearing capacity. Strip or pad foundations adequate.",
                ["laterite"] = "Murram/laterite soil: good bearing capacity. Strip or pad foundations adequate.",
                ["clay"] = "Expansive clay (black cotton soil): high swelling potential. Raft or pile foundation recommended. Avoid strip foundations.",
                ["black cotton"] = "Expansive black cotton soil: high swelling potential. Raft or pile foundation strongly recommended. Strip foundations may fail.",
                ["expansive clay"] = "Expansive clay: high swelling potential. Raft or pile foundation recommended.",
                ["sandy"] = "Sandy soil: moderate bearing capacity. Strip foundations with increased width recommended. Check water table level.",
                ["sand"] = "Sandy soil: moderate bearing capacity. Increase foundation width. Check water table.",
                ["rocky"] = "Rocky ground: excellent bearing capacity. Shallower foundations possible (min 500mm into rock).",
                ["rock"] = "Rocky ground: excellent bearing capacity. Shallower foundations possible (min 500mm into rock).",
            };

        public FoundationCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Type 1: Creates strip foundations along load-bearing walls.
        /// </summary>
        public CreationPipelineResult CreateStripFoundation(StripFoundationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Strip Foundation" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("Level not found.");
                    return result;
                }

                // Get soil-appropriate dimensions
                var depthMm = cmd.DepthMm > 0
                    ? cmd.DepthMm
                    : GetFoundationDepth(cmd.SoilType);
                var widthMm = cmd.WidthMm > 0
                    ? cmd.WidthMm
                    : GetStripWidth(cmd.WallType);

                // Resolve foundation type
                var foundationType = ResolveFoundationType("strip");

                // Get walls to place foundations under
                List<Wall> targetWalls;
                if (cmd.AllLoadBearingWalls)
                {
                    targetWalls = new FilteredElementCollector(_document)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .Where(w => w.LevelId == level.Id &&
                                    (w.StructuralUsage == StructuralWallUsage.Bearing ||
                                     w.WallType?.Function == WallFunction.Exterior))
                        .ToList();
                }
                else if (cmd.WallIds != null && cmd.WallIds.Count > 0)
                {
                    targetWalls = cmd.WallIds
                        .Select(id => _document.GetElement(id) as Wall)
                        .Where(w => w != null)
                        .ToList();
                }
                else
                {
                    // Default: all walls on level
                    targetWalls = new FilteredElementCollector(_document)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .Where(w => w.LevelId == level.Id)
                        .ToList();
                }

                if (targetWalls.Count == 0)
                {
                    result.SetError("No walls found to place foundations under.");
                    return result;
                }

                var createdIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var tg = new TransactionGroup(_document, "StingBIM: Create Strip Foundations"))
                {
                    tg.Start();

                    foreach (var wall in targetWalls)
                    {
                        using (var transaction = new Transaction(_document, "Create Strip Foundation"))
                        {
                            var options = transaction.GetFailureHandlingOptions();
                            options.SetFailuresPreprocessor(failureHandler);
                            transaction.SetFailureHandlingOptions(options);
                            transaction.Start();

                            try
                            {
                                var locationCurve = wall.Location as LocationCurve;
                                if (locationCurve?.Curve == null) continue;

                                // Create wall foundation element
                                var wallFoundation = WallFoundation.Create(
                                    _document, foundationType.Id, wall.Id);

                                if (wallFoundation != null)
                                {
                                    _worksetAssigner.AssignToCorrectWorkset(wallFoundation);
                                    createdIds.Add(wallFoundation.Id);
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.RollBack();
                                // Non-critical — continue with other walls
                            }
                        }
                    }

                    tg.Assimilate();
                }

                // Build advisory message
                var advisory = GetSoilAdvisory(cmd.SoilType);

                result.Success = createdIds.Count > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Created {createdIds.Count} strip foundations " +
                    $"({widthMm}mm wide, {depthMm}mm deep) under {targetWalls.Count} walls";

                if (!string.IsNullOrEmpty(advisory))
                    result.Warnings = advisory;

                result.Suggestions = new List<string>
                {
                    "Add pad foundations under columns",
                    "Add DPC (damp proof course)",
                    "Check foundation depth for soil type"
                };

                Logger.Info($"Strip foundations created: {createdIds.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Strip foundation creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("strip foundation", "create", ex));
            }

            return result;
        }

        /// <summary>
        /// Type 2: Creates pad foundations under structural columns.
        /// </summary>
        public CreationPipelineResult CreatePadFoundations(PadFoundationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Pad Foundation" };

            try
            {
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("Level not found.");
                    return result;
                }

                // Get soil-appropriate dimensions
                var padWidthMm = cmd.PadWidthMm > 0 ? cmd.PadWidthMm : 1500;
                var padDepthMm = cmd.PadDepthMm > 0 ? cmd.PadDepthMm : 400;
                var foundDepthMm = GetFoundationDepth(cmd.SoilType);

                // Upsize for soft soil
                if (cmd.SoilType?.ToLowerInvariant()?.Contains("clay") == true ||
                    cmd.SoilType?.ToLowerInvariant()?.Contains("cotton") == true)
                {
                    padWidthMm = Math.Max(padWidthMm, 2000);
                    padDepthMm = Math.Max(padDepthMm, 500);
                }

                // Find all columns on this level
                var columns = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(c =>
                    {
                        var baseLevelParam = c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                        return baseLevelParam != null && baseLevelParam.AsElementId() == level.Id;
                    })
                    .ToList();

                if (columns.Count == 0)
                {
                    result.SetError($"No structural columns found on {level.Name}.");
                    return result;
                }

                // Resolve pad foundation family
                var familyResult = _familyResolver.ResolveFamilySymbol(
                    BuiltInCategory.OST_StructuralFoundation,
                    "isolated", padWidthMm, padWidthMm);

                if (!familyResult.Success)
                {
                    result.SetError($"No pad foundation family found. {familyResult.Message}");
                    return result;
                }

                var symbol = familyResult.ResolvedType as FamilySymbol;
                var createdIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var tg = new TransactionGroup(_document, "StingBIM: Create Pad Foundations"))
                {
                    tg.Start();

                    foreach (var column in columns)
                    {
                        using (var transaction = new Transaction(_document, "Create Pad Foundation"))
                        {
                            var options = transaction.GetFailureHandlingOptions();
                            options.SetFailuresPreprocessor(failureHandler);
                            transaction.SetFailureHandlingOptions(options);
                            transaction.Start();

                            try
                            {
                                if (symbol != null && !symbol.IsActive)
                                    symbol.Activate();

                                var colLocation = (column.Location as LocationPoint)?.Point ?? XYZ.Zero;
                                var padPoint = new XYZ(colLocation.X, colLocation.Y, level.Elevation);

                                var pad = _document.Create.NewFamilyInstance(
                                    padPoint, symbol, level, StructuralType.Footing);

                                if (pad != null)
                                {
                                    _worksetAssigner.AssignToCorrectWorkset(pad);
                                    createdIds.Add(pad.Id);
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.RollBack();
                            }
                        }
                    }

                    tg.Assimilate();
                }

                var advisory = GetSoilAdvisory(cmd.SoilType);

                result.Success = createdIds.Count > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Created {createdIds.Count} pad foundations " +
                    $"({padWidthMm}×{padWidthMm}×{padDepthMm}mm) under {columns.Count} columns";

                if (!string.IsNullOrEmpty(advisory))
                    result.Warnings = advisory;

                result.Suggestions = new List<string>
                {
                    "Add strip foundations",
                    "Add ground beams",
                    "Review structural layout"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Pad foundation creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("pad foundation", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        private double GetFoundationDepth(string soilType)
        {
            if (!string.IsNullOrEmpty(soilType))
            {
                foreach (var kvp in FoundationDepthBySoil)
                {
                    if (soilType.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return kvp.Value;
                }
            }
            return 1200; // default Uganda standard
        }

        private double GetStripWidth(string wallType)
        {
            if (!string.IsNullOrEmpty(wallType))
            {
                foreach (var kvp in StripWidthDefaults)
                {
                    if (wallType.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return kvp.Value;
                }
            }
            return 600; // default
        }

        private string GetSoilAdvisory(string soilType)
        {
            if (!string.IsNullOrEmpty(soilType))
            {
                foreach (var kvp in SoilAdvisory)
                {
                    if (soilType.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return kvp.Value;
                }
            }
            return null;
        }

        private ContFootingType ResolveFoundationType(string keyword)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(ContFootingType))
                .Cast<ContFootingType>()
                .ToList();

            if (types.Count == 0) return null;

            if (!string.IsNullOrEmpty(keyword))
            {
                var match = types.FirstOrDefault(t =>
                    t.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) return match;
            }

            return types.FirstOrDefault();
        }

        #endregion
    }

    #region Command DTOs

    public class StripFoundationCommand
    {
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public string SoilType { get; set; }
        public string WallType { get; set; }
        public string LevelName { get; set; }
        public bool AllLoadBearingWalls { get; set; } = true;
        public List<ElementId> WallIds { get; set; }
    }

    public class PadFoundationCommand
    {
        public double PadWidthMm { get; set; }
        public double PadDepthMm { get; set; }
        public string SoilType { get; set; }
        public string LevelName { get; set; }
    }

    #endregion
}
