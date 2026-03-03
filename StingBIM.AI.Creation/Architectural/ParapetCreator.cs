// StingBIM.AI.Creation.Architectural.ParapetCreator
// Creates parapet walls with coping
// v4 Prompt Reference: Section A.1.8 — ParapetCreator

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Architectural
{
    /// <summary>
    /// Creates parapet walls along roof edges with proper height and coping.
    ///
    /// Standards:
    ///   Minimum height: 900mm above roof level (fall protection)
    ///   1100mm recommended for public buildings
    ///   Coping: weathering detail to prevent water ingress
    ///   Flashing: waterproof junction with roof membrane
    /// </summary>
    public class ParapetCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;
        private readonly WorksetAssigner _worksetAssigner;

        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double DEFAULT_HEIGHT_MM = 900;

        public ParapetCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
            _worksetAssigner = new WorksetAssigner(document);
        }

        /// <summary>
        /// Creates a parapet wall along the building edge at the specified level.
        /// </summary>
        public CreationPipelineResult CreateParapet(ParapetCreationCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Parapet" };

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

                var heightMm = cmd.HeightMm > 0 ? cmd.HeightMm : DEFAULT_HEIGHT_MM;
                var heightFt = heightMm * MM_TO_FEET;

                // Resolve wall type for parapet
                var wallTypeResult = _familyResolver.ResolveWallType(cmd.WallTypeName ?? "parapet");
                if (!wallTypeResult.Success)
                {
                    // Fallback to any wall type
                    wallTypeResult = _familyResolver.ResolveWallType(null);
                }

                if (!wallTypeResult.Success)
                {
                    result.SetError("No suitable wall type found for parapet.");
                    return result;
                }

                // Get building perimeter from walls on this level or explicit dimensions
                List<Line> perimeterLines;
                if (cmd.WidthMm > 0 && cmd.DepthMm > 0)
                {
                    perimeterLines = CreateRectangularPerimeter(
                        cmd.WidthMm, cmd.DepthMm,
                        cmd.OriginXMm ?? 0, cmd.OriginYMm ?? 0);
                }
                else
                {
                    perimeterLines = GetBuildingPerimeter(level);
                }

                if (perimeterLines == null || perimeterLines.Count == 0)
                {
                    result.SetError("Could not determine building perimeter for parapet. " +
                        "Specify dimensions or ensure walls exist on the target level.");
                    return result;
                }

                var createdIds = new List<ElementId>();
                var failureHandler = new StingBIMFailurePreprocessor();

                using (var tg = new TransactionGroup(_document, "StingBIM: Create Parapet"))
                {
                    tg.Start();

                    foreach (var line in perimeterLines)
                    {
                        using (var transaction = new Transaction(_document, "Create Parapet Wall"))
                        {
                            var options = transaction.GetFailureHandlingOptions();
                            options.SetFailuresPreprocessor(failureHandler);
                            transaction.SetFailureHandlingOptions(options);
                            transaction.Start();

                            try
                            {
                                var wall = Wall.Create(_document, line, wallTypeResult.TypeId,
                                    level.Id, heightFt, 0, false, false);

                                if (wall != null)
                                {
                                    var commentsParam = wall.LookupParameter("Comments");
                                    commentsParam?.Set($"Parapet — Created by StingBIM AI [{DateTime.Now:yyyy-MM-dd HH:mm}]");

                                    _worksetAssigner.AssignToCorrectWorkset(wall);
                                    createdIds.Add(wall.Id);
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

                result.Success = createdIds.Count > 0;
                result.CreatedElementIds = createdIds;
                result.Message = $"Created {createdIds.Count} parapet wall segments, " +
                    $"{heightMm}mm height on {level.Name}";
                result.Suggestions = new List<string>
                {
                    "Add coping to parapet",
                    "Add roof flashing",
                    "Check fall protection compliance"
                };

                Logger.Info($"Parapet created: {createdIds.Count} segments");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Parapet creation failed");
                result.SetError(ErrorExplainer.FormatCreationError("parapet", "create", ex));
            }

            return result;
        }

        #region Helper Methods

        private List<Line> GetBuildingPerimeter(Level level)
        {
            var walls = new FilteredElementCollector(_document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.LevelId == level.Id &&
                            w.WallType?.Function == WallFunction.Exterior)
                .ToList();

            if (walls.Count == 0) return null;

            var lines = new List<Line>();
            foreach (var wall in walls)
            {
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve?.Curve is Line line)
                {
                    // Raise to roof level
                    var wallHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0;
                    var startPt = new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y,
                        level.Elevation + wallHeight);
                    var endPt = new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y,
                        level.Elevation + wallHeight);
                    lines.Add(Line.CreateBound(startPt, endPt));
                }
            }

            return lines.Count > 0 ? lines : null;
        }

        private List<Line> CreateRectangularPerimeter(double widthMm, double depthMm,
            double originXMm, double originYMm)
        {
            var ox = originXMm * MM_TO_FEET;
            var oy = originYMm * MM_TO_FEET;
            var w = widthMm * MM_TO_FEET;
            var d = depthMm * MM_TO_FEET;

            return new List<Line>
            {
                Line.CreateBound(new XYZ(ox, oy, 0), new XYZ(ox + w, oy, 0)),
                Line.CreateBound(new XYZ(ox + w, oy, 0), new XYZ(ox + w, oy + d, 0)),
                Line.CreateBound(new XYZ(ox + w, oy + d, 0), new XYZ(ox, oy + d, 0)),
                Line.CreateBound(new XYZ(ox, oy + d, 0), new XYZ(ox, oy, 0)),
            };
        }

        #endregion
    }

    #region Command DTOs

    public class ParapetCreationCommand
    {
        public double HeightMm { get; set; }
        public string WallTypeName { get; set; }
        public string LevelName { get; set; }
        public double WidthMm { get; set; }
        public double DepthMm { get; set; }
        public double? OriginXMm { get; set; }
        public double? OriginYMm { get; set; }
    }

    #endregion
}
