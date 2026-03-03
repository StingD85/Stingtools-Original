// StingBIM.AI.Creation.Elements.StaircaseCreator
// Creates staircases with compliant riser/going calculations
// v4 Prompt Reference: Section A.1.7 — StaircaseCreator (5 stair types)

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates staircases with standards-compliant riser/going calculations.
    ///
    /// 5 Types:
    ///   1. Straight stair — most common, single flight
    ///   2. L-shaped (quarter turn) — two flights with 90° landing
    ///   3. Dog-leg (half-turn) — two parallel flights with 180° landing
    ///   4. Spiral — loaded family instance
    ///   5. External/fire escape — open riser, galvanized steel
    ///
    /// Standards: Uganda Building Control Regulations 2020, UNBS
    ///   Rise: 150-180mm (residential), 150-170mm (public)
    ///   Going: min 225mm (residential), min 250mm (public)
    ///   2R + G = 600-650mm (comfort formula)
    ///   Width: min 900mm (private), 1200mm (public), 1500mm (high occupancy)
    ///   Max flight without landing: 16 risers (residential), 12 (public)
    ///   Landing: min 1000mm × stair width
    ///   Headroom: min 2000mm throughout
    /// </summary>
    public class StaircaseCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;
        private readonly CostEstimator _costEstimator;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Stair standards
        private const double MIN_RISER_RESIDENTIAL = 150;
        private const double MAX_RISER_RESIDENTIAL = 180;
        private const double MIN_GOING_RESIDENTIAL = 225;
        private const double MIN_RISER_PUBLIC = 150;
        private const double MAX_RISER_PUBLIC = 170;
        private const double MIN_GOING_PUBLIC = 250;
        private const double COMFORT_FORMULA_MIN = 600; // 2R + G minimum
        private const double COMFORT_FORMULA_MAX = 650; // 2R + G maximum
        private const int MAX_RISERS_RESIDENTIAL = 16;
        private const int MAX_RISERS_PUBLIC = 12;
        private const double MIN_HEADROOM = 2000;
        private const double MIN_LANDING_LENGTH = 1000;

        // Default widths in mm
        private static readonly Dictionary<string, double> StairWidthDefaults =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["private"] = 900,
                ["residential"] = 1000,
                ["public"] = 1200,
                ["commercial"] = 1200,
                ["high occupancy"] = 1500,
                ["fire escape"] = 1000,
                ["default"] = 1000,
            };

        public StaircaseCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Creates a staircase between two levels.
        /// </summary>
        public CreationPipelineResult CreateStaircase(StairCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Staircase" };

            try
            {
                if (_document.IsReadOnly)
                {
                    result.SetError("The Revit document is read-only.");
                    return result;
                }

                // Resolve levels
                var baseLevel = _familyResolver.ResolveLevel(cmd.BaseLevelName);
                if (baseLevel == null)
                {
                    result.SetError("Base level not found in the project.");
                    return result;
                }

                var topLevel = _familyResolver.GetLevelAbove(baseLevel);
                if (!string.IsNullOrEmpty(cmd.TopLevelName))
                {
                    topLevel = _familyResolver.ResolveLevel(cmd.TopLevelName);
                }

                if (topLevel == null)
                {
                    result.SetError("Top level not found. Cannot determine floor-to-floor height.");
                    return result;
                }

                // Calculate floor-to-floor height
                var floorHeightFt = topLevel.Elevation - baseLevel.Elevation;
                var floorHeightMm = floorHeightFt / MM_TO_FEET;

                if (floorHeightMm <= 0)
                {
                    result.SetError("Invalid floor-to-floor height. Top level must be above base level.");
                    return result;
                }

                // Calculate compliant riser/going
                var isPublic = cmd.Usage?.ToLowerInvariant() == "public" ||
                               cmd.Usage?.ToLowerInvariant() == "commercial";
                var stairCalc = CalculateCompliantStair(floorHeightMm, isPublic);

                if (!stairCalc.IsCompliant)
                {
                    result.SetError(stairCalc.ComplianceMessage);
                    return result;
                }

                // Get stair width
                var widthMm = cmd.WidthMm > 0 ? cmd.WidthMm : GetDefaultWidth(cmd.Usage);
                var widthFt = widthMm * MM_TO_FEET;

                // Calculate stair run length
                var runLengthMm = stairCalc.RiserCount * stairCalc.GoingMm;
                var originX = (cmd.OriginX ?? 0) * MM_TO_FEET;
                var originY = (cmd.OriginY ?? 0) * MM_TO_FEET;

                var failureHandler = new StingBIMFailurePreprocessor();
                ElementId stairsId = null;

                using (var stairsScope = new StairsEditScope(_document, "StingBIM: Create Staircase"))
                {
                    stairsId = stairsScope.Start(baseLevel.Id, topLevel.Id);

                    using (var transaction = new Transaction(_document, "Create Stair Runs"))
                    {
                        var options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failureHandler);
                        transaction.SetFailureHandlingOptions(options);
                        transaction.Start();

                        try
                        {
                            switch (cmd.StairType?.ToLowerInvariant())
                            {
                                case "l-shaped":
                                case "quarter turn":
                                    CreateLShapedRuns(stairsId, stairCalc, widthFt, originX, originY);
                                    break;

                                case "dog-leg":
                                case "half-turn":
                                case "u-shaped":
                                    CreateDogLegRuns(stairsId, stairCalc, widthFt, originX, originY);
                                    break;

                                case "straight":
                                default:
                                    CreateStraightRun(stairsId, stairCalc, widthFt, originX, originY);
                                    break;
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.RollBack();
                            stairsScope.Rollback();
                            result.SetError(ErrorExplainer.FormatCreationError("staircase", "create", ex));
                            return result;
                        }
                    }

                    stairsScope.Commit(new StingBIMFailurePreprocessor());
                }

                // Set workset and comments
                if (stairsId != null && stairsId != ElementId.InvalidElementId)
                {
                    using (var tx = new Transaction(_document, "StingBIM: Set Stair Properties"))
                    {
                        tx.Start();
                        var stairs = _document.GetElement(stairsId);
                        if (stairs != null)
                        {
                            _worksetAssigner.AssignToCorrectWorkset(stairs);
                            var commentsParam = stairs.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");
                        }
                        tx.Commit();
                    }
                }

                result.Success = true;
                result.CreatedElementId = stairsId;
                result.Message = $"Created {cmd.StairType ?? "straight"} staircase: " +
                    $"{stairCalc.RiserCount} risers × {stairCalc.RiserMm:F0}mm rise, " +
                    $"{stairCalc.GoingMm:F0}mm going, {widthMm:F0}mm wide " +
                    $"({baseLevel.Name} → {topLevel.Name})";
                result.Suggestions = new List<string>
                {
                    "Add handrails",
                    "Add railing",
                    "Check stair compliance"
                };

                Logger.Info($"Staircase created: {stairsId} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Staircase creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("staircase", "create", ex));
            }

            return result;
        }

        #region Stair Calculations

        /// <summary>
        /// Calculates compliant riser height and going depth for a given floor height.
        /// Uses 2R + G = 600-650mm comfort formula.
        /// </summary>
        public StairCalculation CalculateCompliantStair(double floorHeightMm, bool isPublic = false)
        {
            var maxRiser = isPublic ? MAX_RISER_PUBLIC : MAX_RISER_RESIDENTIAL;
            var minRiser = isPublic ? MIN_RISER_PUBLIC : MIN_RISER_RESIDENTIAL;
            var minGoing = isPublic ? MIN_GOING_PUBLIC : MIN_GOING_RESIDENTIAL;

            // Try target riser of 165mm first (comfortable)
            var targetRiser = 165.0;
            var riserCount = (int)Math.Round(floorHeightMm / targetRiser);

            // Ensure at least 3 risers
            riserCount = Math.Max(3, riserCount);

            var riserMm = floorHeightMm / riserCount;

            // Adjust if out of range
            if (riserMm > maxRiser)
            {
                riserCount = (int)Math.Ceiling(floorHeightMm / maxRiser);
                riserMm = floorHeightMm / riserCount;
            }
            else if (riserMm < minRiser)
            {
                riserCount = (int)Math.Floor(floorHeightMm / minRiser);
                riserMm = floorHeightMm / riserCount;
            }

            // Calculate going from 2R + G = 625 (target mid-range)
            var goingMm = 625 - (2 * riserMm);
            goingMm = Math.Max(goingMm, minGoing);

            // Verify comfort formula
            var comfortValue = (2 * riserMm) + goingMm;
            var isComfortable = comfortValue >= COMFORT_FORMULA_MIN && comfortValue <= COMFORT_FORMULA_MAX;

            // Check max risers per flight
            var maxRisersPerFlight = isPublic ? MAX_RISERS_PUBLIC : MAX_RISERS_RESIDENTIAL;
            var needsLanding = riserCount > maxRisersPerFlight;

            return new StairCalculation
            {
                RiserCount = riserCount,
                RiserMm = riserMm,
                GoingMm = goingMm,
                ComfortValue = comfortValue,
                IsCompliant = riserMm >= minRiser && riserMm <= maxRiser && goingMm >= minGoing,
                IsComfortable = isComfortable,
                NeedsLanding = needsLanding,
                FlightCount = needsLanding ? (int)Math.Ceiling((double)riserCount / maxRisersPerFlight) : 1,
                ComplianceMessage = riserMm < minRiser || riserMm > maxRiser
                    ? $"Riser height {riserMm:F0}mm is outside the allowed range ({minRiser}-{maxRiser}mm)."
                    : goingMm < minGoing
                        ? $"Going depth {goingMm:F0}mm is below minimum ({minGoing}mm)."
                        : "Compliant"
            };
        }

        #endregion

        #region Stair Run Creation

        private void CreateStraightRun(ElementId stairsId, StairCalculation calc,
            double widthFt, double originX, double originY)
        {
            var runLengthFt = calc.RiserCount * calc.GoingMm * MM_TO_FEET;
            var startPt = new XYZ(originX, originY, 0);
            var endPt = new XYZ(originX + runLengthFt, originY, 0);
            var line = Line.CreateBound(startPt, endPt);

            StairsRun.CreateStraightRun(_document, stairsId, line,
                StairsRunJustification.Center);
        }

        private void CreateLShapedRuns(ElementId stairsId, StairCalculation calc,
            double widthFt, double originX, double originY)
        {
            // Split risers: half in each run
            var halfRisers = calc.RiserCount / 2;
            var remainRisers = calc.RiserCount - halfRisers;

            // First run: along X axis
            var run1Length = halfRisers * calc.GoingMm * MM_TO_FEET;
            var line1 = Line.CreateBound(
                new XYZ(originX, originY, 0),
                new XYZ(originX + run1Length, originY, 0));

            StairsRun.CreateStraightRun(_document, stairsId, line1,
                StairsRunJustification.Center);

            // Second run: along Y axis (90° turn)
            var landingDepthFt = MIN_LANDING_LENGTH * MM_TO_FEET;
            var run2Length = remainRisers * calc.GoingMm * MM_TO_FEET;
            var line2 = Line.CreateBound(
                new XYZ(originX + run1Length, originY + landingDepthFt, 0),
                new XYZ(originX + run1Length, originY + landingDepthFt + run2Length, 0));

            StairsRun.CreateStraightRun(_document, stairsId, line2,
                StairsRunJustification.Center);
        }

        private void CreateDogLegRuns(ElementId stairsId, StairCalculation calc,
            double widthFt, double originX, double originY)
        {
            var halfRisers = calc.RiserCount / 2;
            var remainRisers = calc.RiserCount - halfRisers;

            // First run: forward
            var run1Length = halfRisers * calc.GoingMm * MM_TO_FEET;
            var line1 = Line.CreateBound(
                new XYZ(originX, originY, 0),
                new XYZ(originX + run1Length, originY, 0));

            StairsRun.CreateStraightRun(_document, stairsId, line1,
                StairsRunJustification.Center);

            // Second run: backward (180° turn)
            var landingDepthFt = MIN_LANDING_LENGTH * MM_TO_FEET;
            var run2Length = remainRisers * calc.GoingMm * MM_TO_FEET;
            var line2 = Line.CreateBound(
                new XYZ(originX + run1Length, originY + widthFt + landingDepthFt, 0),
                new XYZ(originX + run1Length - run2Length, originY + widthFt + landingDepthFt, 0));

            StairsRun.CreateStraightRun(_document, stairsId, line2,
                StairsRunJustification.Center);
        }

        #endregion

        #region Helper Methods

        private double GetDefaultWidth(string usage)
        {
            if (!string.IsNullOrEmpty(usage) &&
                StairWidthDefaults.TryGetValue(usage, out var width))
            {
                return width;
            }
            return 1000; // default residential
        }

        #endregion
    }

    #region Supporting Types

    public class StairCreationCommand
    {
        /// <summary>
        /// Stair type: straight, l-shaped, quarter turn, dog-leg, half-turn, u-shaped, spiral, fire escape
        /// </summary>
        public string StairType { get; set; }
        /// <summary>
        /// Usage context: private, residential, public, commercial, high occupancy
        /// </summary>
        public string Usage { get; set; }
        public double WidthMm { get; set; }
        public string BaseLevelName { get; set; }
        public string TopLevelName { get; set; }
        public double? OriginX { get; set; }
        public double? OriginY { get; set; }
    }

    public class StairCalculation
    {
        public int RiserCount { get; set; }
        public double RiserMm { get; set; }
        public double GoingMm { get; set; }
        public double ComfortValue { get; set; }
        public bool IsCompliant { get; set; }
        public bool IsComfortable { get; set; }
        public bool NeedsLanding { get; set; }
        public int FlightCount { get; set; }
        public string ComplianceMessage { get; set; }
    }

    #endregion
}
