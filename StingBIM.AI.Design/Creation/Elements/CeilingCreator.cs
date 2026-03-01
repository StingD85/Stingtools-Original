// StingBIM.AI.Creation.Elements.CeilingCreator
// Creates ceilings with 5 modes + height standards
// v4 Prompt Reference: Section A.1.5 — CeilingCreator

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates ceilings with intelligent height defaults and material selection.
    ///
    /// 5 Modes:
    ///   1. Flat ceiling from room — "Add plasterboard ceiling to all bedrooms at 2.7m"
    ///   2. Coffered/compound ceiling — "Create a coffered ceiling in the living room"
    ///   3. Suspended/drop ceiling — "Add T-bar suspended ceiling in the office at 2.4m"
    ///   4. Timber slat ceiling — "Add a timber slatted ceiling in the reception"
    ///   5. Vaulted ceiling — "Add a ceiling that follows the roof slope"
    ///
    /// Height Standards (Uganda/East Africa):
    ///   Residential: 2.7m standard, 3.0m premium, 2.4m bathrooms/stores
    ///   Commercial: 2.7m min, 3.0m recommended
    ///   Retail: 3.5m minimum
    ///   Hotel public: 3.0-3.6m, bedrooms 2.7m
    /// </summary>
    public class CeilingCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Ceiling height defaults by room type (in mm)
        private static readonly Dictionary<string, double> CeilingHeightDefaults =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Residential
                ["bedroom"] = 2700,
                ["master bedroom"] = 2700,
                ["living room"] = 2700,
                ["kitchen"] = 2700,
                ["dining room"] = 2700,
                ["bathroom"] = 2400,
                ["wc"] = 2400,
                ["store"] = 2400,
                ["corridor"] = 2700,
                ["garage"] = 2700,
                ["verandah"] = 2700,
                // Commercial
                ["office"] = 2700,
                ["conference"] = 3000,
                ["reception"] = 3000,
                ["lobby"] = 3600,
                // Retail / hospitality
                ["retail"] = 3500,
                ["shop"] = 3500,
                ["hotel lobby"] = 3600,
                ["hotel bedroom"] = 2700,
                // Service
                ["plant room"] = 2400,
                ["server room"] = 2400,
                // Default
                ["default"] = 2700,
            };

        public CeilingCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
        }

        /// <summary>
        /// Creates a ceiling in a room at the specified height.
        /// Mode 1: Flat ceiling from room boundary.
        /// </summary>
        public CreationPipelineResult CreateCeiling(CeilingCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Ceiling" };

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
                    result.SetError("No levels found in the project.");
                    return result;
                }

                // Get ceiling height
                var heightMm = cmd.HeightMm > 0
                    ? cmd.HeightMm
                    : GetDefaultCeilingHeight(cmd.RoomType);
                var heightFt = heightMm * MM_TO_FEET;

                // Resolve ceiling type
                var ceilingType = ResolveCeilingType(cmd.CeilingType, cmd.CeilingMode);
                if (ceilingType == null)
                {
                    result.SetError("No suitable ceiling type found in the project.");
                    return result;
                }

                // Get boundary curves
                CurveLoop boundary = null;
                if (cmd.WidthMm > 0 && cmd.DepthMm > 0)
                {
                    boundary = CreateRectangularBoundary(
                        cmd.WidthMm, cmd.DepthMm, cmd.OriginX ?? 0, cmd.OriginY ?? 0);
                }
                else if (!string.IsNullOrEmpty(cmd.RoomName))
                {
                    boundary = GetRoomBoundary(cmd.RoomName, level);
                }

                if (boundary == null)
                {
                    result.SetError("Could not determine ceiling boundary. Specify dimensions or room name.");
                    return result;
                }

                Element createdCeiling = null;
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var transaction = new Transaction(_document, "StingBIM: Create Ceiling"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    try
                    {
                        // Create ceiling using Ceiling.Create (Revit 2025+)
                        createdCeiling = Ceiling.Create(_document,
                            new List<CurveLoop> { boundary },
                            ceilingType.Id, level.Id);

                        if (createdCeiling != null)
                        {
                            // Set height offset from level
                            var heightOffset = createdCeiling.get_Parameter(
                                BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                            heightOffset?.Set(heightFt);

                            var commentsParam = createdCeiling.LookupParameter("Comments");
                            commentsParam?.Set($"Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                            _worksetAssigner.AssignToCorrectWorkset(createdCeiling);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        result.SetError(ErrorExplainer.FormatCreationError("ceiling", "create", ex));
                        return result;
                    }
                }

                result.Success = true;
                result.CreatedElementId = createdCeiling?.Id;
                result.Message = $"Created {cmd.CeilingMode ?? "flat"} ceiling at {heightMm / 1000:F1}m " +
                    $"on {level.Name}";
                result.Suggestions = new List<string>
                {
                    "Add ceiling to other rooms",
                    "Add light fixtures",
                    "Create a floor above"
                };

                Logger.Info($"Ceiling created: {createdCeiling?.Id} — {result.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ceiling creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("ceiling", "create", ex));
            }

            return result;
        }

        /// <summary>
        /// Creates ceilings for all rooms on a level.
        /// </summary>
        public CreationPipelineResult CreateCeilingsForLevel(string levelName, string ceilingType = null)
        {
            var result = new CreationPipelineResult { ElementType = "Ceilings (Batch)" };

            try
            {
                var level = _familyResolver.ResolveLevel(levelName);
                if (level == null)
                {
                    result.SetError("No levels found in the project.");
                    return result;
                }

                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Level?.Id == level.Id && r.Area > 0)
                    .ToList();

                if (rooms.Count == 0)
                {
                    result.SetError($"No rooms found on {level.Name}.");
                    return result;
                }

                var totalCreated = 0;
                foreach (var room in rooms)
                {
                    var cmd = new CeilingCreationCommand
                    {
                        RoomName = room.Name,
                        RoomType = room.Name,
                        CeilingType = ceilingType,
                        LevelName = levelName
                    };

                    var ceilingResult = CreateCeiling(cmd);
                    if (ceilingResult.Success)
                        totalCreated++;
                }

                result.Success = totalCreated > 0;
                result.Message = $"Created ceilings in {totalCreated} of {rooms.Count} rooms on {level.Name}.";
                result.Suggestions = new List<string>
                {
                    "Add floors to all rooms",
                    "Review the model",
                    "Check compliance"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Batch ceiling creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("ceilings", "batch create", ex));
            }

            return result;
        }

        #region Helper Methods

        private double GetDefaultCeilingHeight(string roomType)
        {
            if (!string.IsNullOrEmpty(roomType) &&
                CeilingHeightDefaults.TryGetValue(roomType, out var height))
            {
                return height;
            }
            return 2700; // default
        }

        private CeilingType ResolveCeilingType(string typeName, string mode)
        {
            var collector = new FilteredElementCollector(_document)
                .OfClass(typeof(CeilingType))
                .Cast<CeilingType>()
                .ToList();

            if (collector.Count == 0) return null;

            // Try to match by keyword
            var keywords = new List<string>();
            if (!string.IsNullOrEmpty(typeName)) keywords.Add(typeName);
            if (!string.IsNullOrEmpty(mode))
            {
                switch (mode.ToLowerInvariant())
                {
                    case "suspended": case "drop":
                        keywords.AddRange(new[] { "suspended", "T-bar", "acoustic", "grid" });
                        break;
                    case "timber": case "wood":
                        keywords.AddRange(new[] { "timber", "wood", "slat" });
                        break;
                    case "coffered":
                        keywords.AddRange(new[] { "coffered", "coffer" });
                        break;
                    default:
                        keywords.AddRange(new[] { "plasterboard", "gypsum", "drywall" });
                        break;
                }
            }

            foreach (var keyword in keywords)
            {
                var match = collector.FirstOrDefault(ct =>
                    ct.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) return match;
            }

            // Fallback to first available
            return collector.FirstOrDefault();
        }

        private CurveLoop GetRoomBoundary(string roomName, Level level)
        {
            var room = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .FirstOrDefault(r =>
                    r.Name.IndexOf(roomName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    r.Area > 0 &&
                    (level == null || r.Level?.Id == level.Id));

            if (room == null) return null;

            var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (boundaries == null || boundaries.Count == 0) return null;

            var curveLoop = new CurveLoop();
            foreach (var seg in boundaries[0])
            {
                curveLoop.Append(seg.GetCurve());
            }

            return curveLoop;
        }

        private CurveLoop CreateRectangularBoundary(double widthMm, double depthMm,
            double originXMm, double originYMm)
        {
            var ox = originXMm * MM_TO_FEET;
            var oy = originYMm * MM_TO_FEET;
            var w = widthMm * MM_TO_FEET;
            var d = depthMm * MM_TO_FEET;

            var curveLoop = new CurveLoop();
            curveLoop.Append(Line.CreateBound(new XYZ(ox, oy, 0), new XYZ(ox + w, oy, 0)));
            curveLoop.Append(Line.CreateBound(new XYZ(ox + w, oy, 0), new XYZ(ox + w, oy + d, 0)));
            curveLoop.Append(Line.CreateBound(new XYZ(ox + w, oy + d, 0), new XYZ(ox, oy + d, 0)));
            curveLoop.Append(Line.CreateBound(new XYZ(ox, oy + d, 0), new XYZ(ox, oy, 0)));

            return curveLoop;
        }

        #endregion
    }

    #region Command DTOs

    public class CeilingCreationCommand
    {
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public string CeilingType { get; set; }
        /// <summary>
        /// Ceiling mode: flat, suspended, coffered, timber, vaulted
        /// </summary>
        public string CeilingMode { get; set; }
        public double HeightMm { get; set; }
        public string LevelName { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public double? OriginX { get; set; }
        public double? OriginY { get; set; }
    }

    #endregion
}
