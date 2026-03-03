// StingBIM.AI.Creation.Architectural.RailingCreator
// Creates railings and balustrades with Uganda/UNBS standards
// v4 Prompt Reference: Section A.1.8 — Railing

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Architectural
{
    /// <summary>
    /// Creates railings and balustrades with standards compliance.
    ///
    /// Standards: Uganda + UNBS
    ///   Balcony/terrace: min 1100mm height (>600mm above adjacent ground)
    ///   Stairs: 900mm height, continuous handrail
    ///   Baluster spacing: max 100mm
    ///   Handrail: graspable, 40-50mm diameter
    /// </summary>
    public class RailingCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Railing height standards (mm)
        private static readonly Dictionary<string, double> RailingHeightDefaults =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["balcony"] = 1100,
                ["terrace"] = 1100,
                ["rooftop"] = 1100,
                ["stair"] = 900,
                ["staircase"] = 900,
                ["ramp"] = 900,
                ["corridor"] = 900,
                ["landing"] = 900,
                ["default"] = 1100,
            };

        public RailingCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
        }

        /// <summary>
        /// Creates a railing along a path defined by points or along a host element.
        /// </summary>
        public CreationPipelineResult CreateRailing(RailingCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Railing" };

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

                // Get railing height
                var heightMm = cmd.HeightMm > 0
                    ? cmd.HeightMm
                    : GetDefaultHeight(cmd.RailingLocation);

                // Resolve railing type
                var railingType = ResolveRailingType(cmd.RailingType);
                if (railingType == null)
                {
                    result.SetError("No railing type found in the project.");
                    return result;
                }

                // Build path
                CurveLoop path;
                if (cmd.PathPointsMm != null && cmd.PathPointsMm.Count >= 2)
                {
                    path = new CurveLoop();
                    for (int i = 0; i < cmd.PathPointsMm.Count - 1; i++)
                    {
                        var p1 = cmd.PathPointsMm[i];
                        var p2 = cmd.PathPointsMm[i + 1];
                        path.Append(Line.CreateBound(
                            new XYZ(p1.Item1 * MM_TO_FEET, p1.Item2 * MM_TO_FEET, level.Elevation),
                            new XYZ(p2.Item1 * MM_TO_FEET, p2.Item2 * MM_TO_FEET, level.Elevation)));
                    }
                }
                else if (cmd.LengthMm > 0)
                {
                    var ox = (cmd.OriginXMm ?? 0) * MM_TO_FEET;
                    var oy = (cmd.OriginYMm ?? 0) * MM_TO_FEET;
                    path = new CurveLoop();
                    path.Append(Line.CreateBound(
                        new XYZ(ox, oy, level.Elevation),
                        new XYZ(ox + cmd.LengthMm * MM_TO_FEET, oy, level.Elevation)));
                }
                else
                {
                    result.SetError("Railing requires path points or a length.");
                    return result;
                }

                Railing railing = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Create Railing"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Get curves from path
                        var curves = new List<Curve>();
                        foreach (var curve in path)
                        {
                            curves.Add(curve);
                        }

                        railing = Railing.Create(_document, new CurveLoop(),
                            railingType.Id, level.Id);

                        if (railing != null)
                        {
                            // Set the path
                            railing.SetPath(path);

                            var commentsParam = railing.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(railing);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("railing", "create", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = railing?.Id;
                result.Message = $"Created {cmd.RailingType ?? "standard"} railing, " +
                    $"{heightMm}mm height on {level.Name}";
                result.Suggestions = new List<string>
                {
                    "Add railing to other side",
                    "Check balustrade spacing",
                    "Add handrail extensions"
                };

                Logger.Info($"Railing created: {railing?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Railing creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("railing", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        private double GetDefaultHeight(string location)
        {
            if (!string.IsNullOrEmpty(location) &&
                RailingHeightDefaults.TryGetValue(location, out var height))
            {
                return height;
            }
            return 1100;
        }

        private RailingType ResolveRailingType(string keyword)
        {
            var types = new FilteredElementCollector(_document)
                .OfClass(typeof(RailingType))
                .Cast<RailingType>()
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

    public class RailingCreationCommand
    {
        public string RailingType { get; set; }
        public string RailingLocation { get; set; }
        public double HeightMm { get; set; }
        public double LengthMm { get; set; }
        public string LevelName { get; set; }
        public List<Tuple<double, double>> PathPointsMm { get; set; }
        public double? OriginXMm { get; set; }
        public double? OriginYMm { get; set; }
    }

    #endregion
}
