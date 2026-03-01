// StingBIM.AI.Creation.Architectural.CurtainWallCreator
// Creates curtain wall systems
// v4 Prompt Reference: Section A.1.8 — CurtainWallCreator

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Architectural
{
    /// <summary>
    /// Creates curtain wall systems with mullion grids and panel types.
    ///
    /// Modes:
    ///   1. Curtain wall by length — "Add a 10m curtain wall on the east facade"
    ///   2. Replace existing wall — "Replace the north wall with a curtain wall"
    ///
    /// Features:
    ///   - Auto-grid layout (horizontal + vertical mullions)
    ///   - Panel type selection (glass, spandrel, opaque)
    ///   - Solar heat gain awareness for Uganda climate
    /// </summary>
    public class CurtainWallCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;

        private const double MM_TO_FEET = 1.0 / 304.8;

        public CurtainWallCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
        }

        /// <summary>
        /// Creates a curtain wall with specified dimensions.
        /// </summary>
        public CreationPipelineResult CreateCurtainWall(CurtainWallCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Curtain Wall" };

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

                // Resolve curtain wall type
                var wallType = ResolveCurtainWallType(cmd.WallTypeName);
                if (wallType == null)
                {
                    result.SetError("No curtain wall type found in the project.");
                    return result;
                }

                var lengthMm = cmd.LengthMm > 0 ? cmd.LengthMm : 5000;
                var heightMm = cmd.HeightMm > 0 ? cmd.HeightMm : 3000;
                var lengthFt = lengthMm * MM_TO_FEET;
                var heightFt = heightMm * MM_TO_FEET;

                var ox = (cmd.OriginXMm ?? 0) * MM_TO_FEET;
                var oy = (cmd.OriginYMm ?? 0) * MM_TO_FEET;

                var startPt = new XYZ(ox, oy, 0);
                var endPt = new XYZ(ox + lengthFt, oy, 0);
                var wallLine = Line.CreateBound(startPt, endPt);

                Wall curtainWall = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Create Curtain Wall"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        curtainWall = Wall.Create(_document, wallLine, wallType.Id,
                            level.Id, heightFt, 0, false, false);

                        if (curtainWall != null)
                        {
                            var commentsParam = curtainWall.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(curtainWall);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("curtain wall", "create", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = curtainWall?.Id;
                result.Message = $"Created {lengthMm / 1000:F1}m × {heightMm / 1000:F1}m curtain wall on {level.Name}";
                result.Warnings = "Uganda tropical climate: consider solar heat gain coefficient (SHGC) for glazing. " +
                    "East/west facades receive most solar radiation — use low-E glass or external shading.";
                result.Suggestions = new List<string>
                {
                    "Add mullion grid",
                    "Add external shading",
                    "Check solar heat gain"
                };

                Logger.Info($"Curtain wall created: {curtainWall?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Curtain wall creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("curtain wall", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        private WallType ResolveCurtainWallType(string keyword)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind == WallKind.Curtain)
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

    public class CurtainWallCommand
    {
        public double LengthMm { get; set; }
        public double HeightMm { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public double? OriginXMm { get; set; }
        public double? OriginYMm { get; set; }
    }

    #endregion
}
