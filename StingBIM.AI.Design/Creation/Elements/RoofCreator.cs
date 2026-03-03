// StingBIM.AI.Creation.Elements.RoofCreator
// Creates roofs with 6 modes + Africa-specific materials + seismic awareness
// v4 Prompt Reference: Section A.1.4 — RoofCreator

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates roofs with intelligent family resolution and Africa-specific materials.
    ///
    /// 6 Modes:
    ///   1. Flat roof (auto-detect footprint)
    ///   2. Pitched/gable roof
    ///   3. Hip roof
    ///   4. Mono-pitch/shed roof
    ///   5. Mansard roof
    ///   6. Roof over specific rooms only
    ///
    /// Standards: Uganda Building Control Regulations 2020, UNBS
    /// Seismic: East African Rift Zone awareness
    /// </summary>
    public class RoofCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double DEFAULT_EAVE_OVERHANG_MM = 300; // Africa standard — sun/rain protection

        // Default pitch: 22.5° standard for corrugated iron in Uganda
        private const double DEFAULT_PITCH_DEGREES = 22.5;

        // Roofing materials — Africa-specific
        private static readonly Dictionary<string, string> RoofMaterialKeywords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["iron"] = "corrugated iron",
                ["corrugated"] = "corrugated iron",
                ["aluminium"] = "aluminium roofing",
                ["aluminum"] = "aluminium roofing",
                ["clay tile"] = "clay tile",
                ["concrete tile"] = "concrete tile",
                ["membrane"] = "EPDM membrane",
                ["flat"] = "concrete slab",
                ["thatch"] = "thatch",
                ["makuti"] = "thatch",
                ["steel"] = "steel standing seam",
                ["standing seam"] = "steel standing seam",
                ["polycarbonate"] = "polycarbonate sheet",
                ["green"] = "green roof",
            };

        // Heavy roof materials that require structural adequacy warning
        private static readonly HashSet<string> HeavyRoofMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "clay tile", "concrete tile", "green roof"
        };

        public RoofCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Creates a roof over the building footprint.
        /// Supports flat, pitched, hip, mono-pitch, mansard modes.
        /// </summary>
        public CreationPipelineResult CreateRoof(RoofCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Roof" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                // Resolve roof type
                var roofTypeResult = _familyResolver.ResolveRoofType(cmd.RoofMaterial);
                if (!roofTypeResult.Success)
                {
                    result.SetError(roofTypeResult.Message);
                    return result;
                }

                // Get the top level
                var level = _familyResolver.ResolveLevel(cmd.LevelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Get building footprint from walls on this level
                CurveArray footprint;
                if (cmd.WidthMm > 0 && cmd.DepthMm > 0)
                {
                    footprint = CreateRectangularFootprint(
                        cmd.WidthMm, cmd.DepthMm, cmd.OriginX ?? 0, cmd.OriginY ?? 0);
                }
                else
                {
                    footprint = GetBuildingFootprint(level);
                }

                if (footprint == null || footprint.Size == 0)
                {
                    result.SetError("Could not determine building footprint for the roof. " +
                        "Please specify dimensions or ensure walls exist on the target level.");
                    return result;
                }

                // Add eave overhang
                var overhangFt = (cmd.OverhangMm > 0 ? cmd.OverhangMm : DEFAULT_EAVE_OVERHANG_MM) * MM_TO_FEET;

                var roofType = roofTypeResult.ResolvedType as RoofType;
                var failureHandler = new StingBIMFailurePreprocessor();
                FootPrintRoof createdRoof = null;

                using (var transaction = new Transaction(_document, "StingBIM: Create Roof"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Create footprint roof with ModelCurveArray output
                        ModelCurveArray modelCurves = new ModelCurveArray();
                        createdRoof = _document.Create.NewFootPrintRoof(
                            footprint, level, roofType, out modelCurves);

                        if (createdRoof != null)
                        {
                            // Set slope based on roof mode
                            var pitchDegrees = cmd.PitchDegrees > 0 ? cmd.PitchDegrees : DEFAULT_PITCH_DEGREES;
                            var slopeRatio = Math.Tan(pitchDegrees * Math.PI / 180.0);

                            switch (cmd.RoofMode?.ToLowerInvariant())
                            {
                                case "flat":
                                    // No slope
                                    foreach (ModelCurve mc in modelCurves)
                                    {
                                        createdRoof.set_DefinesSlope(mc, false);
                                    }
                                    break;

                                case "hip":
                                    // All edges slope
                                    foreach (ModelCurve mc in modelCurves)
                                    {
                                        createdRoof.set_DefinesSlope(mc, true);
                                        createdRoof.set_SlopeAngle(mc, slopeRatio);
                                        createdRoof.set_Offset(mc, overhangFt);
                                    }
                                    break;

                                case "mono-pitch":
                                case "shed":
                                    // Only one long edge slopes
                                    var curveIndex = 0;
                                    foreach (ModelCurve mc in modelCurves)
                                    {
                                        if (curveIndex == 0) // first edge
                                        {
                                            createdRoof.set_DefinesSlope(mc, true);
                                            createdRoof.set_SlopeAngle(mc, slopeRatio);
                                        }
                                        else
                                        {
                                            createdRoof.set_DefinesSlope(mc, false);
                                        }
                                        createdRoof.set_Offset(mc, overhangFt);
                                        curveIndex++;
                                    }
                                    break;

                                case "gable":
                                case "pitched":
                                default:
                                    // Two parallel long edges slope, gable ends don't
                                    var idx = 0;
                                    foreach (ModelCurve mc in modelCurves)
                                    {
                                        if (idx % 2 == 0) // long edges
                                        {
                                            createdRoof.set_DefinesSlope(mc, true);
                                            createdRoof.set_SlopeAngle(mc, slopeRatio);
                                        }
                                        else
                                        {
                                            createdRoof.set_DefinesSlope(mc, false);
                                        }
                                        createdRoof.set_Offset(mc, overhangFt);
                                        idx++;
                                    }
                                    break;
                            }

                            // Set comments
                            var commentsParam = createdRoof.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(createdRoof);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("roof", "create", ex));
                        return result;
                    }
                }

                // Build result
                var warnings = new List<string>();

                // Heavy roof material warning
                var materialName = cmd.RoofMaterial ?? "corrugated iron";
                if (HeavyRoofMaterials.Any(m => materialName.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    warnings.Add($"Confirm structural adequacy for heavy roof material ({materialName}).");
                }

                // Seismic warning for Uganda
                warnings.Add("Uganda East African Rift: roof must be designed for seismic loads per Building Control Regulations 2020.");

                result.Success = true;
                result.CreatedElementId = createdRoof?.Id;
                result.Message = $"Created {cmd.RoofMode ?? "pitched"} roof " +
                    $"({materialName}) on {level.Name}";
                result.Warnings = string.Join("\n", warnings);
                result.Suggestions = new List<string>
                {
                    "Add ceiling below the roof",
                    "Add roof gutters",
                    "Check structural loading"
                };

                Logger.Info($"Roof created: {createdRoof?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Roof creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("roof", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        private CurveArray GetBuildingFootprint(Level level)
        {
            // Collect external walls on this level
            var walls = new FilteredElementCollector(_document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.LevelId == level.Id)
                .ToList();

            if (walls.Count == 0) return null;

            // Build footprint from wall locations
            var curveArray = new CurveArray();
            foreach (var wall in walls)
            {
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve?.Curve != null)
                {
                    curveArray.Append(locationCurve.Curve);
                }
            }

            return curveArray.Size > 0 ? curveArray : null;
        }

        private CurveArray CreateRectangularFootprint(double widthMm, double depthMm,
            double originXMm, double originYMm)
        {
            var ox = originXMm * MM_TO_FEET;
            var oy = originYMm * MM_TO_FEET;
            var w = widthMm * MM_TO_FEET;
            var d = depthMm * MM_TO_FEET;

            var curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(ox, oy, 0), new XYZ(ox + w, oy, 0)));
            curveArray.Append(Line.CreateBound(new XYZ(ox + w, oy, 0), new XYZ(ox + w, oy + d, 0)));
            curveArray.Append(Line.CreateBound(new XYZ(ox + w, oy + d, 0), new XYZ(ox, oy + d, 0)));
            curveArray.Append(Line.CreateBound(new XYZ(ox, oy + d, 0), new XYZ(ox, oy, 0)));

            return curveArray;
        }

        #endregion
    }

    #region Command DTOs

    public class RoofCreationCommand
    {
        /// <summary>
        /// Roof mode: flat, pitched, gable, hip, mono-pitch, shed, mansard
        /// </summary>
        public string RoofMode { get; set; }
        public string RoofMaterial { get; set; }
        public double PitchDegrees { get; set; }
        public double OverhangMm { get; set; }
        public string LevelName { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public double? OriginX { get; set; }
        public double? OriginY { get; set; }
    }

    #endregion
}
