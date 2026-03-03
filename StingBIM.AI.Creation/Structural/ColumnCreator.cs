// StingBIM.AI.Creation.Structural.ColumnCreator
// Places structural columns with grid integration
// v4 Prompt Reference: Section A.2.1 — ColumnCreator (3 modes)

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
    /// Places structural columns with intelligent sizing and grid integration.
    ///
    /// 3 Modes:
    ///   1. Single column at point — "Place a 400×400mm column at grid A1"
    ///   2. Column grid — "Create a 4×3 column grid at 5m × 6m spacing"
    ///   3. Columns at all grid intersections — "Place columns at all grid intersections"
    ///
    /// Standards: Uganda Building Control Regulations 2020
    /// Seismic: East African Rift Zone — all structural elements designed for seismic loads
    ///
    /// Size Guide:
    ///   Residential: 300×300 (1-2 storey), 400×400 (3-4 storey)
    ///   Commercial: 400×400 (3-5 storey), 500×500 (6-10 storey)
    ///   Circular: 350mm dia (residential), 450mm dia (commercial)
    /// </summary>
    public class ColumnCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Default column sizes (mm)
        private static readonly Dictionary<string, (double Width, double Depth)> DefaultColumnSizes =
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
            {
                ["residential small"] = (300, 300),
                ["residential"] = (400, 400),
                ["commercial"] = (400, 400),
                ["commercial large"] = (500, 500),
                ["high-rise"] = (600, 600),
                ["default"] = (400, 400),
            };

        public ColumnCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Mode 1: Places a single structural column at a specific point.
        /// </summary>
        public CreationPipelineResult PlaceColumn(ColumnPlacementCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Column" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                var baseLevel = _familyResolver.ResolveLevel(cmd.BaseLevelName);
                if (baseLevel == null)
                {
                    result.SetError("Base level not found.");
                    return result;
                }

                var topLevel = _familyResolver.GetLevelAbove(baseLevel);

                // Resolve column family
                var widthMm = cmd.WidthMm > 0 ? cmd.WidthMm : 400;
                var depthMm = cmd.DepthMm > 0 ? cmd.DepthMm : widthMm;

                var familyResult = _familyResolver.ResolveFamilySymbol(
                    BuiltInCategory.OST_StructuralColumns,
                    cmd.ColumnType ?? "concrete",
                    widthMm, depthMm);

                if (!familyResult.Success)
                {
                    result.SetError($"No column family found. {familyResult.Message}");
                    return result;
                }

                var symbol = familyResult.ResolvedType as FamilySymbol;
                if (symbol == null)
                {
                    result.SetError("Resolved element is not a valid column family.");
                    return result;
                }

                // Determine placement point
                XYZ point;
                if (!string.IsNullOrEmpty(cmd.GridIntersection))
                {
                    point = FindGridIntersection(cmd.GridIntersection);
                    if (point == null)
                    {
                        result.SetError($"Grid intersection '{cmd.GridIntersection}' not found.");
                        return result;
                    }
                }
                else
                {
                    point = new XYZ(
                        (cmd.XMm ?? 0) * MM_TO_FEET,
                        (cmd.YMm ?? 0) * MM_TO_FEET,
                        baseLevel.Elevation);
                }

                FamilyInstance column = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Place Column"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();

                        column = _document.Create.NewFamilyInstance(
                            point, symbol, baseLevel, StructuralType.Column);

                        if (column != null && topLevel != null)
                        {
                            // Set top constraint
                            var topConstraint = column.get_Parameter(
                                BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            topConstraint?.Set(topLevel.Id);

                            var topOffset = column.get_Parameter(
                                BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            topOffset?.Set(0);
                        }

                        if (column != null)
                        {
                            var markParam = column.LookupParameter("Mark");
                            markParam?.Set(cmd.Mark ?? $"C-{cmd.GridIntersection ?? "01"}");

                            var commentsParam = column.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(column);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("column", "place", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = column?.Id;
                result.Message = $"Placed {widthMm}×{depthMm}mm structural column on {baseLevel.Name}";

                // Seismic warning
                result.Warnings = "Uganda East African Rift: structural columns must be designed for seismic loads per Building Control Regulations 2020.";

                result.Suggestions = new List<string>
                {
                    "Place more columns",
                    "Add beams between columns",
                    "Create a column grid"
                };

                Logger.Info($"Column placed: {column?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Column placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("column", "place", ex));
            }

            return result;
        }

        /// <summary>
        /// Mode 2: Creates a column grid with specified spacing.
        /// </summary>
        public CreationPipelineResult CreateColumnGrid(ColumnGridCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Column Grid" };

            try
            {
                var baseLevel = _familyResolver.ResolveLevel(cmd.BaseLevelName);
                if (baseLevel == null)
                {
                    result.SetError("Base level not found.");
                    return result;
                }

                var colsX = cmd.CountX > 0 ? cmd.CountX : 4;
                var colsY = cmd.CountY > 0 ? cmd.CountY : 3;
                var spacingXMm = cmd.SpacingXMm > 0 ? cmd.SpacingXMm : 5000;
                var spacingYMm = cmd.SpacingYMm > 0 ? cmd.SpacingYMm : 6000;

                var totalColumns = colsX * colsY;
                var placedCount = 0;
                var createdIds = new List<ElementId>();

                for (int ix = 0; ix < colsX; ix++)
                {
                    for (int iy = 0; iy < colsY; iy++)
                    {
                        var colCmd = new ColumnPlacementCommand
                        {
                            XMm = (cmd.OriginXMm ?? 0) + ix * spacingXMm,
                            YMm = (cmd.OriginYMm ?? 0) + iy * spacingYMm,
                            WidthMm = cmd.ColumnWidthMm,
                            DepthMm = cmd.ColumnDepthMm,
                            ColumnType = cmd.ColumnType,
                            BaseLevelName = cmd.BaseLevelName,
                            Mark = $"C-{(char)('A' + ix)}{iy + 1}"
                        };

                        var colResult = PlaceColumn(colCmd);
                        if (colResult.Success)
                        {
                            placedCount++;
                            if (colResult.CreatedElementId != null)
                                createdIds.Add(colResult.CreatedElementId);
                        }
                    }
                }

                result.Success = placedCount > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Placed {placedCount} of {totalColumns} columns in {colsX}×{colsY} grid " +
                    $"at {spacingXMm / 1000:F1}m × {spacingYMm / 1000:F1}m spacing on {baseLevel.Name}";
                result.Warnings = "Uganda East African Rift: all structural elements must be designed for seismic loads.";
                result.Suggestions = new List<string>
                {
                    "Add structural grids",
                    "Add beams between columns",
                    "Add foundations under columns"
                };

                Logger.Info($"Column grid created: {placedCount} columns");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Column grid creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("column grid", "create", ex));
            }

            return result;
        }

        /// <summary>
        /// Mode 3: Places columns at all grid intersections on a level.
        /// </summary>
        public CreationPipelineResult PlaceColumnsAtGridIntersections(string levelName,
            double widthMm = 400, double depthMm = 0, string columnType = "concrete")
        {
            var result = new CreationPipelineResult { ElementType = "Columns at Grids" };

            try
            {
                var intersections = GetAllGridIntersections();
                if (intersections.Count == 0)
                {
                    result.SetError("No grid intersections found in the project. Create structural grids first.");
                    return result;
                }

                var placedCount = 0;
                var createdIds = new List<ElementId>();

                foreach (var (name, point) in intersections)
                {
                    var colCmd = new ColumnPlacementCommand
                    {
                        XMm = point.X / MM_TO_FEET,
                        YMm = point.Y / MM_TO_FEET,
                        WidthMm = widthMm,
                        DepthMm = depthMm > 0 ? depthMm : widthMm,
                        ColumnType = columnType,
                        BaseLevelName = levelName,
                        GridIntersection = name,
                        Mark = $"C-{name}"
                    };

                    var colResult = PlaceColumn(colCmd);
                    if (colResult.Success)
                    {
                        placedCount++;
                        if (colResult.CreatedElementId != null)
                            createdIds.Add(colResult.CreatedElementId);
                    }
                }

                result.Success = placedCount > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Placed {placedCount} columns at {intersections.Count} grid intersections.";
                result.Suggestions = new List<string>
                {
                    "Add beams between columns",
                    "Add foundations",
                    "Review structural layout"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Grid columns placement failed");
                result.SetError(ErrorExplainer.FormatCreationError("columns", "place at grids", ex));
            }

            return result;
        }

        #region Grid Helpers

        private XYZ FindGridIntersection(string gridName)
        {
            // Parse "A1" → Grid "A" intersects Grid "1"
            if (string.IsNullOrEmpty(gridName) || gridName.Length < 2) return null;

            var grids = new FilteredElementCollector(_document)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            if (grids.Count < 2) return null;

            // Try to find grids by name parts
            var letterPart = new string(gridName.TakeWhile(char.IsLetter).ToArray());
            var numberPart = new string(gridName.SkipWhile(char.IsLetter).ToArray());

            var grid1 = grids.FirstOrDefault(g =>
                g.Name.Equals(letterPart, StringComparison.OrdinalIgnoreCase));
            var grid2 = grids.FirstOrDefault(g =>
                g.Name.Equals(numberPart, StringComparison.OrdinalIgnoreCase));

            if (grid1 == null || grid2 == null) return null;

            // Find intersection point
            var curve1 = grid1.Curve;
            var curve2 = grid2.Curve;

            var results = new IntersectionResultArray();
            var setCompResult = curve1.Intersect(curve2, out results);

            if (setCompResult == SetComparisonResult.Overlap && results != null && results.Size > 0)
            {
                return results.get_Item(0).XYZPoint;
            }

            return null;
        }

        private List<(string Name, XYZ Point)> GetAllGridIntersections()
        {
            var intersections = new List<(string, XYZ)>();

            var grids = new FilteredElementCollector(_document)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            if (grids.Count < 2) return intersections;

            for (int i = 0; i < grids.Count; i++)
            {
                for (int j = i + 1; j < grids.Count; j++)
                {
                    var results = new IntersectionResultArray();
                    var setCompResult = grids[i].Curve.Intersect(grids[j].Curve, out results);

                    if (setCompResult == SetComparisonResult.Overlap && results != null && results.Size > 0)
                    {
                        var point = results.get_Item(0).XYZPoint;
                        var name = $"{grids[i].Name}{grids[j].Name}";
                        intersections.Add((name, point));
                    }
                }
            }

            return intersections;
        }

        #endregion
    }

    #region Command DTOs

    public class ColumnPlacementCommand
    {
        public double? XMm { get; set; }
        public double? YMm { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public string ColumnType { get; set; }
        public string BaseLevelName { get; set; }
        public string GridIntersection { get; set; }
        public string Mark { get; set; }
    }

    public class ColumnGridCommand
    {
        public int CountX { get; set; }
        public int CountY { get; set; }
        public double SpacingXMm { get; set; }
        public double SpacingYMm { get; set; }
        public double ColumnWidthMm { get; set; }
        public double ColumnDepthMm { get; set; }
        public string ColumnType { get; set; }
        public string BaseLevelName { get; set; }
        public double? OriginXMm { get; set; }
        public double? OriginYMm { get; set; }
    }

    #endregion
}
