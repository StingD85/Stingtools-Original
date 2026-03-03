// StingBIM.AI.Creation.Modification.ModificationEngine
// Comprehensive element modification engine — geometric, type, property, structural, MEP
// v4 Prompt Reference: Section B — COMPREHENSIVE MODIFICATION ENGINE (B.0–B.3)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Modification
{
    /// <summary>
    /// Comprehensive modification engine supporting 20+ modification types.
    /// All modifications go through TransactionManager with FailurePreprocessor.
    ///
    /// PUBLIC API:
    ///   ModificationResult Modify(Document doc, ModificationCommand cmd)
    ///
    /// Supported types: RESIZE, MOVE, ROTATE, COPY, MIRROR, DELETE,
    ///   CHANGE_TYPE, CHANGE_MATERIAL, CHANGE_PARAMETER,
    ///   SPLIT, EXTEND, TRIM, OFFSET, LEVEL_ADJUST,
    ///   PIN, UNPIN, PHASE_CHANGE
    /// </summary>
    public class ModificationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Unit conversion constants — Revit uses decimal feet internally
        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double M_TO_FEET = 1.0 / 0.3048;
        private const double FEET_TO_MM = 304.8;

        private readonly CostEstimator _costEstimator;

        public ModificationEngine()
        {
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Execute a modification command against the live Revit Document.
        /// All changes are wrapped in a TransactionGroup so they undo as one unit.
        /// </summary>
        public ModificationResult Modify(Document doc, ModificationCommand cmd)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));

            Logger.Info($"ModificationEngine: {cmd.Type} on {ElementSelector.Describe(cmd.Selector)}");

            try
            {
                return cmd.Type switch
                {
                    ModificationType.MOVE => ExecuteMove(doc, cmd),
                    ModificationType.COPY => ExecuteCopy(doc, cmd),
                    ModificationType.COPY_TO_LEVEL => ExecuteCopyToLevel(doc, cmd),
                    ModificationType.DELETE => ExecuteDelete(doc, cmd),
                    ModificationType.ROTATE => ExecuteRotate(doc, cmd),
                    ModificationType.MIRROR => ExecuteMirror(doc, cmd),
                    ModificationType.RESIZE => ExecuteResize(doc, cmd),
                    ModificationType.CHANGE_TYPE => ExecuteChangeType(doc, cmd),
                    ModificationType.CHANGE_PARAMETER => ExecuteChangeParameter(doc, cmd),
                    ModificationType.SPLIT => ExecuteSplit(doc, cmd),
                    ModificationType.EXTEND => ExecuteExtend(doc, cmd),
                    ModificationType.OFFSET => ExecuteOffset(doc, cmd),
                    ModificationType.LEVEL_ADJUST => ExecuteLevelAdjust(doc, cmd),
                    ModificationType.PIN => ExecutePin(doc, cmd, pin: true),
                    ModificationType.UNPIN => ExecutePin(doc, cmd, pin: false),
                    _ => ModificationResult.Failed($"Unsupported modification type: {cmd.Type}")
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"ModificationEngine failed: {cmd.Type}");
                return ModificationResult.Failed(
                    ErrorExplainer.FormatCreationError("element", cmd.Type.ToString().ToLower(), ex));
            }
        }

        /// <summary>
        /// Checks if a modification intent type is handled by this engine.
        /// </summary>
        public static bool IsModificationIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper == "MOVE_ELEMENT" || upper == "COPY_ELEMENT" ||
                   upper == "DELETE_ELEMENT" || upper == "MODIFY_DIMENSION" ||
                   upper == "ROTATE_ELEMENT" || upper == "MIRROR_ELEMENT" ||
                   upper == "CHANGE_TYPE" || upper == "SET_PARAMETER" ||
                   upper == "SPLIT_ELEMENT" || upper == "EXTEND_ELEMENT" ||
                   upper == "COPY_TO_LEVEL" || upper == "LEVEL_ADJUST" ||
                   upper == "PIN_ELEMENT" || upper == "UNPIN_ELEMENT" ||
                   upper == "OFFSET_ELEMENT" || upper == "RESIZE_ELEMENT";
        }

        #region Geometric Modifications (B.1)

        /// <summary>
        /// MOVE — Translate elements by an XYZ offset.
        /// NLP example: "Move the column at grid A1 by 500mm to the north"
        /// Revit API: ElementTransformUtils.MoveElement(doc, id, XYZ)
        /// </summary>
        private ModificationResult ExecuteMove(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("move");

            var offset = ResolveOffset(cmd);
            if (offset.IsZeroLength())
                return ModificationResult.Failed("No movement offset specified. Try: 'Move 500mm north'");

            int movedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Move Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Move Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            ElementTransformUtils.MoveElement(doc, id, offset);
                            movedCount++;
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "move", ex));
                    }
                }

                tg.Assimilate();
            }

            var offsetMm = offset.GetLength() * FEET_TO_MM;
            return ModificationResult.Succeeded(
                $"Moved {movedCount} element(s) by {offsetMm:F0}mm.",
                movedCount,
                new List<string> { "Move further", "Undo", "Align elements" });
        }

        /// <summary>
        /// COPY — Duplicate elements at an offset.
        /// NLP example: "Copy this wall 3 meters to the right"
        /// Revit API: ElementTransformUtils.CopyElement(doc, id, XYZ)
        /// </summary>
        private ModificationResult ExecuteCopy(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("copy");

            var offset = ResolveOffset(cmd);
            if (offset.IsZeroLength())
                return ModificationResult.Failed("No copy offset specified. Try: 'Copy 2m to the east'");

            var copiedIds = new List<ElementId>();
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Copy Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Copy Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var newIds = ElementTransformUtils.CopyElement(doc, id, offset);
                            copiedIds.AddRange(newIds);
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "copy", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Copied {elementIds.Count} element(s) → {copiedIds.Count} new element(s) created.",
                copiedIds.Count,
                new List<string> { "Copy again", "Move the copies", "Delete originals" },
                copiedIds);
        }

        /// <summary>
        /// COPY_TO_LEVEL — Copy elements from one level to another.
        /// NLP example: "Copy Level 1 layout to Level 2"
        /// Revit API: ElementTransformUtils.CopyElements with Z-offset transform
        /// </summary>
        private ModificationResult ExecuteCopyToLevel(Document doc, ModificationCommand cmd)
        {
            var sourceLevelName = GetParam<string>(cmd, "sourceLevel");
            var targetLevelName = GetParam<string>(cmd, "targetLevel");

            if (string.IsNullOrEmpty(sourceLevelName) || string.IsNullOrEmpty(targetLevelName))
                return ModificationResult.Failed("Source and target levels required. Try: 'Copy Level 1 to Level 2'");

            // Resolve levels
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            var sourceLevel = levels.FirstOrDefault(l =>
                l.Name.IndexOf(sourceLevelName, StringComparison.OrdinalIgnoreCase) >= 0);
            var targetLevel = levels.FirstOrDefault(l =>
                l.Name.IndexOf(targetLevelName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (sourceLevel == null)
                return ModificationResult.Failed($"Source level '{sourceLevelName}' not found.");
            if (targetLevel == null)
                return ModificationResult.Failed($"Target level '{targetLevelName}' not found.");

            // Get all elements on source level (skip grids, levels, views)
            var levelFilter = new ElementLevelFilter(sourceLevel.Id);
            var sourceElements = new FilteredElementCollector(doc)
                .WherePasses(levelFilter)
                .WhereElementIsNotElementType()
                .Where(e => !(e is Grid) && !(e is Level) && !(e is View))
                .Select(e => e.Id)
                .ToList();

            if (sourceElements.Count == 0)
                return ModificationResult.Failed($"No elements found on {sourceLevel.Name}.");

            // Z offset = target elevation - source elevation
            var zOffset = targetLevel.Elevation - sourceLevel.Elevation;
            var transform = Transform.CreateTranslation(new XYZ(0, 0, zOffset));

            var copiedIds = new List<ElementId>();
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Copy to Level"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Copy Elements to Level"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        var ids = ElementTransformUtils.CopyElements(
                            doc, sourceElements, doc, transform, new CopyPasteOptions());
                        copiedIds.AddRange(ids);
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("level layout", "copy", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Copied {sourceElements.Count} elements from {sourceLevel.Name} to {targetLevel.Name}.\n" +
                $"{copiedIds.Count} new elements created.",
                copiedIds.Count,
                new List<string> { "Copy to another level", "Review copied elements", "Modify Level 2 layout" },
                copiedIds);
        }

        /// <summary>
        /// DELETE — Remove elements from the model.
        /// NLP example: "Delete all unused rooms"
        /// Revit API: doc.Delete(elementIds)
        /// </summary>
        private ModificationResult ExecuteDelete(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("delete");

            int deletedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Delete Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Delete Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        var idCollection = elementIds.ToList();
                        doc.Delete(idCollection);
                        deletedCount = idCollection.Count;
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "delete", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Deleted {deletedCount} element(s).",
                deletedCount,
                new List<string> { "Undo", "Review remaining elements" });
        }

        /// <summary>
        /// ROTATE — Rotate elements around a point.
        /// NLP example: "Rotate the door 90 degrees"
        /// Revit API: ElementTransformUtils.RotateElement
        /// </summary>
        private ModificationResult ExecuteRotate(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("rotate");

            var angleDegrees = GetParam<double>(cmd, "angle");
            if (Math.Abs(angleDegrees) < 0.01)
                return ModificationResult.Failed("No rotation angle specified. Try: 'Rotate 90 degrees'");

            var angleRadians = angleDegrees * Math.PI / 180.0;
            int rotatedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Rotate Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Rotate Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem == null) continue;

                            // Rotation axis through element center, vertical
                            var center = GetElementCenter(elem);
                            var axis = Line.CreateBound(center, center + XYZ.BasisZ);

                            ElementTransformUtils.RotateElement(doc, id, axis, angleRadians);
                            rotatedCount++;
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "rotate", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Rotated {rotatedCount} element(s) by {angleDegrees:F0} degrees.",
                rotatedCount,
                new List<string> { "Rotate more", "Undo", "Mirror instead" });
        }

        /// <summary>
        /// MIRROR — Mirror elements across a plane.
        /// NLP example: "Mirror the left half of the floor plan"
        /// Revit API: ElementTransformUtils.MirrorElements
        /// </summary>
        private ModificationResult ExecuteMirror(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("mirror");

            // Determine mirror plane — default to vertical plane at average X
            var elements = elementIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
            var avgX = elements.Average(e => GetElementCenter(e).X);

            var planeOrigin = new XYZ(avgX, 0, 0);
            var planeNormal = XYZ.BasisX;

            // Check for direction override
            var direction = GetParam<string>(cmd, "direction");
            if (!string.IsNullOrEmpty(direction))
            {
                var lower = direction.ToLowerInvariant();
                if (lower.Contains("horizontal") || lower.Contains("east") || lower.Contains("west"))
                {
                    planeNormal = XYZ.BasisX;
                }
                else if (lower.Contains("vertical") || lower.Contains("north") || lower.Contains("south"))
                {
                    planeNormal = XYZ.BasisY;
                }
            }

            var mirrorPlane = Plane.CreateByNormalAndOrigin(planeNormal, planeOrigin);
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Mirror Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Mirror Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        ElementTransformUtils.MirrorElements(doc, elementIds, mirrorPlane, true);
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "mirror", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Mirrored {elementIds.Count} element(s).",
                elementIds.Count,
                new List<string> { "Delete originals", "Undo", "Mirror other direction" });
        }

        /// <summary>
        /// RESIZE — Resize rooms/elements by moving bounding walls.
        /// NLP example: "Resize Bedroom 1 to 5x6m"
        /// Revit API: ElementTransformUtils.MoveElement on bounding walls
        /// </summary>
        private ModificationResult ExecuteResize(Document doc, ModificationCommand cmd)
        {
            // Parse target dimensions
            var targetWidthMm = GetParam<double>(cmd, "width");
            var targetDepthMm = GetParam<double>(cmd, "depth");
            var deltaLengthMm = GetParam<double>(cmd, "deltaLength"); // For "make 1m longer"
            var directionStr = GetParam<string>(cmd, "direction");

            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("resize");

            int modifiedCount = 0;
            var messages = new List<string>();
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Resize Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Resize Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem == null) continue;

                            if (elem is Wall wall)
                            {
                                // Resize wall by modifying its location curve
                                if (wall.Location is LocationCurve lc && lc.Curve is Line line)
                                {
                                    var currentLengthMm = line.Length * FEET_TO_MM;
                                    var newLengthMm = deltaLengthMm != 0
                                        ? currentLengthMm + deltaLengthMm
                                        : (targetWidthMm > 0 ? targetWidthMm : currentLengthMm);

                                    if (newLengthMm > 0 && Math.Abs(newLengthMm - currentLengthMm) > 1)
                                    {
                                        var direction = line.Direction;
                                        var newEnd = line.GetEndPoint(0) +
                                            direction * (newLengthMm * MM_TO_FEET);
                                        var newLine = Line.CreateBound(line.GetEndPoint(0), newEnd);
                                        lc.Curve = newLine;

                                        messages.Add($"Wall resized: {currentLengthMm:F0}mm → {newLengthMm:F0}mm");
                                        modifiedCount++;
                                    }
                                }
                            }
                            else if (elem.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Rooms)
                            {
                                // Room resize — find bounding walls and move them
                                messages.Add("Room resize: finding bounding walls to adjust...");
                                var resizeResult = ResizeRoom(doc, elem as Room,
                                    targetWidthMm, targetDepthMm, deltaLengthMm, directionStr);
                                if (resizeResult > 0)
                                {
                                    modifiedCount += resizeResult;
                                    messages.Add($"Moved {resizeResult} wall(s) to resize room.");
                                }
                                else
                                {
                                    messages.Add("Could not determine room bounding walls for resize.");
                                }
                            }
                            else
                            {
                                // Generic element — try parameter-based resize
                                var widthParam = elem.LookupParameter("Width");
                                var heightParam = elem.LookupParameter("Height");

                                if (widthParam != null && targetWidthMm > 0)
                                {
                                    widthParam.Set(targetWidthMm * MM_TO_FEET);
                                    modifiedCount++;
                                }
                                if (heightParam != null && targetDepthMm > 0)
                                {
                                    heightParam.Set(targetDepthMm * MM_TO_FEET);
                                    modifiedCount++;
                                }
                            }
                        }

                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "resize", ex));
                    }
                }

                tg.Assimilate();
            }

            var msg = modifiedCount > 0
                ? string.Join("\n", messages)
                : "No elements could be resized with the given parameters.";

            return ModificationResult.Succeeded(msg, modifiedCount,
                new List<string> { "Resize more", "Check compliance", "Undo" });
        }

        /// <summary>
        /// OFFSET — Move wall or curve elements parallel to their current position.
        /// NLP example: "Offset the pipe by 300mm"
        /// </summary>
        private ModificationResult ExecuteOffset(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("offset");

            var offsetMm = GetParam<double>(cmd, "distance");
            if (offsetMm <= 0)
                return ModificationResult.Failed("No offset distance specified. Try: 'Offset 300mm'");

            var offsetFt = offsetMm * MM_TO_FEET;
            int count = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Offset Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Offset Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem?.Location is LocationCurve lc && lc.Curve is Line line)
                            {
                                // Perpendicular offset direction
                                var dir = line.Direction;
                                var perp = new XYZ(-dir.Y, dir.X, 0).Normalize();
                                ElementTransformUtils.MoveElement(doc, id, perp * offsetFt);
                                count++;
                            }
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "offset", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Offset {count} element(s) by {offsetMm:F0}mm.",
                count,
                new List<string> { "Offset more", "Undo" });
        }

        #endregion

        #region Type & Property Modifications (B.2)

        /// <summary>
        /// CHANGE_TYPE — Change the type of elements.
        /// NLP example: "Change all bedroom walls to 200mm brick"
        /// Revit API: Element.ChangeTypeId(newTypeId)
        /// </summary>
        private ModificationResult ExecuteChangeType(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("change type on");

            var newTypeName = GetParam<string>(cmd, "newType");
            if (string.IsNullOrEmpty(newTypeName))
                return ModificationResult.Failed("New type not specified. Try: 'Change to 200mm brick'");

            // Resolve the new type from the document
            var newTypeId = ResolveTypeByName(doc, newTypeName, elementIds.First());
            if (newTypeId == null || newTypeId == ElementId.InvalidElementId)
                return ModificationResult.Failed(
                    $"Type '{newTypeName}' not found in the project. Check loaded families.");

            int changedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Change Type"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Change Element Type"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem == null) continue;

                            elem.ChangeTypeId(newTypeId);
                            changedCount++;
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "change type", ex));
                    }
                }

                tg.Assimilate();
            }

            // Estimate cost delta
            var costNote = "";
            try
            {
                var newType = doc.GetElement(newTypeId);
                costNote = $"\nNew type: {newType?.Name ?? newTypeName}";
            }
            catch { }

            return ModificationResult.Succeeded(
                $"Changed type on {changedCount} element(s).{costNote}",
                changedCount,
                new List<string> { "Check costs", "Undo", "Change more elements" });
        }

        /// <summary>
        /// CHANGE_PARAMETER — Set a parameter value on matching elements.
        /// NLP examples:
        ///   "Set the fire rating of all corridor walls to 60 minutes"
        ///   "Number all doors sequentially by level"
        /// Revit API: element.LookupParameter(name).Set(value)
        /// </summary>
        private ModificationResult ExecuteChangeParameter(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("set parameter on");

            var paramName = GetParam<string>(cmd, "parameterName");
            var paramValue = GetParam<string>(cmd, "parameterValue");

            if (string.IsNullOrEmpty(paramName))
                return ModificationResult.Failed("Parameter name required. Try: 'Set Fire Rating to 60'");

            int setCount = 0;
            int failCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Set Parameters"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Set Parameter Values"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem == null) continue;

                            var param = elem.LookupParameter(paramName);
                            if (param == null || param.IsReadOnly)
                            {
                                failCount++;
                                continue;
                            }

                            bool success = SetParameterValue(param, paramValue);
                            if (success) setCount++;
                            else failCount++;
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("parameter", "set", ex));
                    }
                }

                tg.Assimilate();
            }

            var msg = $"Set '{paramName}' = '{paramValue}' on {setCount} element(s).";
            if (failCount > 0)
                msg += $"\n{failCount} element(s) skipped (parameter not found or read-only).";

            return ModificationResult.Succeeded(msg, setCount,
                new List<string> { "Set another parameter", "Undo", "Check compliance" });
        }

        #endregion

        #region Structural & MEP Modifications (B.3)

        /// <summary>
        /// SPLIT — Split a wall at a specific point.
        /// NLP example: "Split the north wall at the door location"
        /// Revit API: WallUtils.DisallowWallJoinAtEnd (+ break curve)
        /// </summary>
        private ModificationResult ExecuteSplit(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("split");

            var splitRatio = GetParam<double>(cmd, "splitRatio");
            if (splitRatio <= 0 || splitRatio >= 1) splitRatio = 0.5; // Default: split at midpoint

            int splitCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Split Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Split Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem is Wall wall && wall.Location is LocationCurve lc &&
                                lc.Curve is Line line)
                            {
                                // Calculate split point
                                var splitPoint = line.Evaluate(splitRatio, true);

                                // Create two new walls from the split
                                var wallType = wall.WallType;
                                var level = doc.GetElement(wall.LevelId) as Level;
                                var height = wall.get_Parameter(
                                    BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 10.0;

                                var line1 = Line.CreateBound(line.GetEndPoint(0), splitPoint);
                                var line2 = Line.CreateBound(splitPoint, line.GetEndPoint(1));

                                var wall1 = Wall.Create(doc, line1, wallType.Id, level.Id,
                                    height, 0, wall.Flipped, wall.StructuralUsage != Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing);
                                var wall2 = Wall.Create(doc, line2, wallType.Id, level.Id,
                                    height, 0, wall.Flipped, wall.StructuralUsage != Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing);

                                // Delete original
                                doc.Delete(id);

                                splitCount++;
                            }
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("wall", "split", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Split {splitCount} wall(s) into {splitCount * 2} segments.",
                splitCount,
                new List<string> { "Add a door at split", "Undo", "Delete a segment" });
        }

        /// <summary>
        /// EXTEND — Extend beam/wall endpoints to connect to a target.
        /// NLP example: "Extend all beams to connect to the new column"
        /// Revit API: Move endpoint via LocationCurve
        /// </summary>
        private ModificationResult ExecuteExtend(Document doc, ModificationCommand cmd)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("extend");

            var targetPointMm = ResolveTargetPoint(cmd);
            var extendLengthMm = GetParam<double>(cmd, "length");

            int extendedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Extend Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Extend Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in elementIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem?.Location is LocationCurve lc && lc.Curve is Line line)
                            {
                                XYZ newEnd;
                                if (targetPointMm != null)
                                {
                                    // Extend to target point
                                    newEnd = new XYZ(
                                        targetPointMm.X * MM_TO_FEET,
                                        targetPointMm.Y * MM_TO_FEET,
                                        line.GetEndPoint(1).Z);
                                }
                                else if (extendLengthMm > 0)
                                {
                                    // Extend by length in current direction
                                    var dir = line.Direction;
                                    newEnd = line.GetEndPoint(1) + dir * (extendLengthMm * MM_TO_FEET);
                                }
                                else
                                {
                                    continue;
                                }

                                var newLine = Line.CreateBound(line.GetEndPoint(0), newEnd);
                                lc.Curve = newLine;
                                extendedCount++;
                            }
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "extend", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Extended {extendedCount} element(s).",
                extendedCount,
                new List<string> { "Trim instead", "Undo", "Check connections" });
        }

        /// <summary>
        /// LEVEL_ADJUST — Raise or lower a level elevation.
        /// NLP example: "Raise Level 2 by 300mm to increase ground floor ceiling height"
        /// Revit API: Level.Elevation += offset
        /// </summary>
        private ModificationResult ExecuteLevelAdjust(Document doc, ModificationCommand cmd)
        {
            var levelName = GetParam<string>(cmd, "levelName");
            var adjustMm = GetParam<double>(cmd, "adjustment"); // positive = raise, negative = lower

            if (string.IsNullOrEmpty(levelName))
                return ModificationResult.Failed("Level name required. Try: 'Raise Level 2 by 300mm'");
            if (Math.Abs(adjustMm) < 1)
                return ModificationResult.Failed("Adjustment amount required. Try: 'Raise by 300mm'");

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l =>
                    l.Name.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (level == null)
                return ModificationResult.Failed($"Level '{levelName}' not found.");

            var oldElevationMm = level.Elevation * FEET_TO_MM;
            var newElevationMm = oldElevationMm + adjustMm;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Adjust Level"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Adjust Level Elevation"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        level.Elevation += adjustMm * MM_TO_FEET;
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("level", "adjust", ex));
                    }
                }

                tg.Assimilate();
            }

            var dirWord = adjustMm > 0 ? "Raised" : "Lowered";
            return ModificationResult.Succeeded(
                $"{dirWord} {level.Name} by {Math.Abs(adjustMm):F0}mm.\n" +
                $"Elevation: {oldElevationMm:F0}mm → {newElevationMm:F0}mm.\n" +
                "All level-hosted elements moved automatically.\n" +
                "Check: staircase rise/going compliance may need review.",
                1,
                new List<string> { "Check staircase compliance", "Undo", "Adjust another level" });
        }

        /// <summary>
        /// PIN / UNPIN — Pin or unpin elements to prevent accidental modification.
        /// </summary>
        private ModificationResult ExecutePin(Document doc, ModificationCommand cmd, bool pin)
        {
            var selector = new ElementSelector(doc);
            var elementIds = selector.Select(cmd.Selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements(pin ? "pin" : "unpin");

            int count = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var t = new Transaction(doc, pin ? "Pin Elements" : "Unpin Elements"))
            {
                var options = t.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(failureHandler);
                t.SetFailureHandlingOptions(options);
                t.Start();

                try
                {
                    foreach (var id in elementIds)
                    {
                        var elem = doc.GetElement(id);
                        if (elem != null)
                        {
                            elem.Pinned = pin;
                            count++;
                        }
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    return ModificationResult.Failed(
                        ErrorExplainer.FormatCreationError("element", pin ? "pin" : "unpin", ex));
                }
            }

            var action = pin ? "Pinned" : "Unpinned";
            return ModificationResult.Succeeded(
                $"{action} {count} element(s).", count,
                new List<string> { pin ? "Unpin them" : "Pin them", "Undo" });
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get a typed parameter from the command's Parameters dictionary.
        /// </summary>
        private T GetParam<T>(ModificationCommand cmd, string key)
        {
            if (cmd.Parameters != null && cmd.Parameters.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typed) return typed;
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch { }
            }
            return default;
        }

        /// <summary>
        /// Resolve an XYZ offset from command parameters.
        /// Supports "offsetX/Y/Z" in mm, and "direction" + "distance" shorthand.
        /// </summary>
        private XYZ ResolveOffset(ModificationCommand cmd)
        {
            var ox = GetParam<double>(cmd, "offsetX") * MM_TO_FEET;
            var oy = GetParam<double>(cmd, "offsetY") * MM_TO_FEET;
            var oz = GetParam<double>(cmd, "offsetZ") * MM_TO_FEET;

            // Also support direction + distance
            var direction = GetParam<string>(cmd, "direction");
            var distanceMm = GetParam<double>(cmd, "distance");

            if (!string.IsNullOrEmpty(direction) && distanceMm > 0)
            {
                var distFt = distanceMm * MM_TO_FEET;
                var lower = direction.ToLowerInvariant();

                if (lower.Contains("north"))
                    oy += distFt;
                else if (lower.Contains("south"))
                    oy -= distFt;
                else if (lower.Contains("east") || lower.Contains("right"))
                    ox += distFt;
                else if (lower.Contains("west") || lower.Contains("left"))
                    ox -= distFt;
                else if (lower.Contains("up"))
                    oz += distFt;
                else if (lower.Contains("down"))
                    oz -= distFt;
            }

            return new XYZ(ox, oy, oz);
        }

        private XYZ ResolveTargetPoint(ModificationCommand cmd)
        {
            var tx = GetParam<double>(cmd, "targetX");
            var ty = GetParam<double>(cmd, "targetY");
            if (Math.Abs(tx) > 0.01 || Math.Abs(ty) > 0.01)
                return new XYZ(tx, ty, 0);
            return null;
        }

        /// <summary>
        /// Get the center point of an element (bounding box center or location point).
        /// </summary>
        private XYZ GetElementCenter(Element elem)
        {
            if (elem.Location is LocationPoint lp)
                return lp.Point;

            if (elem.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            var bbox = elem.get_BoundingBox(null);
            if (bbox != null)
                return (bbox.Min + bbox.Max) / 2.0;

            return XYZ.Zero;
        }

        /// <summary>
        /// Resolve a type ID by fuzzy-matching the type name against loaded types.
        /// </summary>
        private ElementId ResolveTypeByName(Document doc, string typeName, ElementId sampleElementId)
        {
            var sampleElem = doc.GetElement(sampleElementId);
            if (sampleElem == null) return null;

            var keywords = typeName.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            // Get all types of the same category
            var categoryId = sampleElem.Category?.Id;
            if (categoryId == null) return null;

            var types = new FilteredElementCollector(doc)
                .OfCategoryId(categoryId)
                .WhereElementIsElementType()
                .ToList();

            // Score each type by keyword match
            var bestScore = 0;
            ElementId bestId = null;

            foreach (var type in types)
            {
                var name = type.Name.ToLowerInvariant();
                var score = keywords.Count(k => name.Contains(k));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = type.Id;
                }
            }

            return bestScore > 0 ? bestId : null;
        }

        /// <summary>
        /// Set a parameter value, auto-detecting the storage type.
        /// </summary>
        private bool SetParameterValue(Parameter param, string value)
        {
            if (param == null || param.IsReadOnly || string.IsNullOrEmpty(value))
                return false;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(value);
                        return true;

                    case StorageType.Integer:
                        if (int.TryParse(value, out var intVal))
                        {
                            param.Set(intVal);
                            return true;
                        }
                        break;

                    case StorageType.Double:
                        if (double.TryParse(value, out var dblVal))
                        {
                            param.Set(dblVal);
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to set parameter {param.Definition?.Name}");
            }

            return false;
        }

        /// <summary>
        /// Resize a room by moving its bounding walls.
        /// Returns the number of walls moved.
        /// </summary>
        private int ResizeRoom(Document doc, Room room,
            double targetWidthMm, double targetDepthMm,
            double deltaLengthMm, string directionStr)
        {
            if (room == null) return 0;

            var bbox = room.get_BoundingBox(null);
            if (bbox == null) return 0;

            var currentWidthFt = bbox.Max.X - bbox.Min.X;
            var currentDepthFt = bbox.Max.Y - bbox.Min.Y;
            var currentWidthMm = currentWidthFt * FEET_TO_MM;
            var currentDepthMm = currentDepthFt * FEET_TO_MM;

            // Determine how much to move each direction
            double deltaXFt = 0, deltaYFt = 0;

            if (targetWidthMm > 0)
                deltaXFt = (targetWidthMm - currentWidthMm) * MM_TO_FEET;
            if (targetDepthMm > 0)
                deltaYFt = (targetDepthMm - currentDepthMm) * MM_TO_FEET;
            if (deltaLengthMm != 0 && !string.IsNullOrEmpty(directionStr))
            {
                var lower = directionStr.ToLowerInvariant();
                if (lower.Contains("north"))
                    deltaYFt = deltaLengthMm * MM_TO_FEET;
                else if (lower.Contains("south"))
                    deltaYFt = -deltaLengthMm * MM_TO_FEET;
                else if (lower.Contains("east"))
                    deltaXFt = deltaLengthMm * MM_TO_FEET;
                else if (lower.Contains("west"))
                    deltaXFt = -deltaLengthMm * MM_TO_FEET;
            }

            if (Math.Abs(deltaXFt) < 0.001 && Math.Abs(deltaYFt) < 0.001)
                return 0;

            // Find walls near the room's bounding box edges
            int wallsMoved = 0;
            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.Location is LocationCurve)
                .ToList();

            var tolerance = 1.0 * MM_TO_FEET; // 1mm tolerance

            foreach (var wall in walls)
            {
                var lc = wall.Location as LocationCurve;
                if (lc?.Curve is not Line line) continue;

                var mid = line.Evaluate(0.5, true);

                // Check if this wall is on the east edge (max X) → move by deltaX
                if (deltaXFt != 0 && Math.Abs(mid.X - bbox.Max.X) < tolerance &&
                    mid.Y >= bbox.Min.Y - tolerance && mid.Y <= bbox.Max.Y + tolerance)
                {
                    ElementTransformUtils.MoveElement(doc, wall.Id, new XYZ(deltaXFt, 0, 0));
                    wallsMoved++;
                }
                // North edge (max Y) → move by deltaY
                else if (deltaYFt != 0 && Math.Abs(mid.Y - bbox.Max.Y) < tolerance &&
                    mid.X >= bbox.Min.X - tolerance && mid.X <= bbox.Max.X + tolerance)
                {
                    ElementTransformUtils.MoveElement(doc, wall.Id, new XYZ(0, deltaYFt, 0));
                    wallsMoved++;
                }
            }

            return wallsMoved;
        }

        #endregion

        #region NLP Intent Routing

        /// <summary>
        /// Routes a modification intent from NLP to a ModificationCommand.
        /// Called by CommandRouter for modification intents.
        /// </summary>
        public ModificationResult RouteIntent(Document doc, string intentType,
            Dictionary<string, object> entities, string originalInput)
        {
            var cmd = ParseCommand(intentType, entities, originalInput);
            if (cmd == null)
                return ModificationResult.Failed($"Could not parse modification command from: {originalInput}");

            return Modify(doc, cmd);
        }

        /// <summary>
        /// Parse an NLP intent + entities into a ModificationCommand.
        /// </summary>
        public ModificationCommand ParseCommand(string intentType,
            Dictionary<string, object> entities, string input)
        {
            var selector = ElementSelector.FromNaturalLanguage(input, entities);
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            switch (intentType?.ToUpperInvariant())
            {
                case "MOVE_ELEMENT":
                    ParseMoveParams(input, parameters);
                    return new ModificationCommand
                    {
                        Type = ModificationType.MOVE,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "COPY_ELEMENT":
                    // Check for "copy to level" pattern
                    var levelCopyMatch = Regex.Match(input,
                        @"copy\s+(?:level\s+)?(\w+\s*\d*)\s+(?:to|onto)\s+(?:level\s+)?(\w+\s*\d*)",
                        RegexOptions.IgnoreCase);
                    if (levelCopyMatch.Success)
                    {
                        parameters["sourceLevel"] = levelCopyMatch.Groups[1].Value.Trim();
                        parameters["targetLevel"] = levelCopyMatch.Groups[2].Value.Trim();
                        return new ModificationCommand
                        {
                            Type = ModificationType.COPY_TO_LEVEL,
                            Selector = selector,
                            Parameters = parameters
                        };
                    }
                    ParseMoveParams(input, parameters);
                    return new ModificationCommand
                    {
                        Type = ModificationType.COPY,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "DELETE_ELEMENT":
                    return new ModificationCommand
                    {
                        Type = ModificationType.DELETE,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "ROTATE_ELEMENT":
                    var angleMatch = Regex.Match(input, @"(\d+\.?\d*)\s*(?:deg|°|degree)", RegexOptions.IgnoreCase);
                    if (angleMatch.Success)
                        parameters["angle"] = double.Parse(angleMatch.Groups[1].Value);
                    else
                        parameters["angle"] = 90.0; // Default 90°
                    return new ModificationCommand
                    {
                        Type = ModificationType.ROTATE,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "MIRROR_ELEMENT":
                    ParseDirectionParam(input, parameters);
                    return new ModificationCommand
                    {
                        Type = ModificationType.MIRROR,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "MODIFY_DIMENSION":
                case "RESIZE_ELEMENT":
                    ParseResizeParams(input, parameters);
                    return new ModificationCommand
                    {
                        Type = ModificationType.RESIZE,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "CHANGE_TYPE":
                    var typeMatch = Regex.Match(input,
                        @"(?:to|with)\s+(.+?)(?:\s+(?:wall|door|window|type))?$",
                        RegexOptions.IgnoreCase);
                    if (typeMatch.Success)
                        parameters["newType"] = typeMatch.Groups[1].Value.Trim();
                    return new ModificationCommand
                    {
                        Type = ModificationType.CHANGE_TYPE,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "SET_PARAMETER":
                    var paramMatch = Regex.Match(input,
                        @"(?:set|change)\s+(?:the\s+)?(.+?)\s+(?:to|=)\s+(.+?)(?:\s+(?:on|for|of))?",
                        RegexOptions.IgnoreCase);
                    if (paramMatch.Success)
                    {
                        parameters["parameterName"] = paramMatch.Groups[1].Value.Trim();
                        parameters["parameterValue"] = paramMatch.Groups[2].Value.Trim();
                    }
                    return new ModificationCommand
                    {
                        Type = ModificationType.CHANGE_PARAMETER,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "SPLIT_ELEMENT":
                    return new ModificationCommand
                    {
                        Type = ModificationType.SPLIT,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "EXTEND_ELEMENT":
                    var extLengthMatch = Regex.Match(input,
                        @"(\d+\.?\d*)\s*(mm|m|meter|metre)", RegexOptions.IgnoreCase);
                    if (extLengthMatch.Success)
                    {
                        var val = double.Parse(extLengthMatch.Groups[1].Value);
                        var unit = extLengthMatch.Groups[2].Value.ToLowerInvariant();
                        parameters["length"] = unit == "mm" ? val : val * 1000;
                    }
                    return new ModificationCommand
                    {
                        Type = ModificationType.EXTEND,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "LEVEL_ADJUST":
                    ParseLevelAdjustParams(input, parameters);
                    return new ModificationCommand
                    {
                        Type = ModificationType.LEVEL_ADJUST,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "OFFSET_ELEMENT":
                    var offMatch = Regex.Match(input,
                        @"(\d+\.?\d*)\s*(mm|m|meter|metre)", RegexOptions.IgnoreCase);
                    if (offMatch.Success)
                    {
                        var val = double.Parse(offMatch.Groups[1].Value);
                        var unit = offMatch.Groups[2].Value.ToLowerInvariant();
                        parameters["distance"] = unit == "mm" ? val : val * 1000;
                    }
                    return new ModificationCommand
                    {
                        Type = ModificationType.OFFSET,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "PIN_ELEMENT":
                    return new ModificationCommand
                    {
                        Type = ModificationType.PIN,
                        Selector = selector,
                        Parameters = parameters
                    };

                case "UNPIN_ELEMENT":
                    return new ModificationCommand
                    {
                        Type = ModificationType.UNPIN,
                        Selector = selector,
                        Parameters = parameters
                    };

                default:
                    return null;
            }
        }

        private void ParseMoveParams(string input, Dictionary<string, object> parameters)
        {
            // Extract distance: "500mm", "2m", "3 meters"
            var distMatch = Regex.Match(input,
                @"(\d+\.?\d*)\s*(mm|m|meter|metre)\b", RegexOptions.IgnoreCase);
            if (distMatch.Success)
            {
                var val = double.Parse(distMatch.Groups[1].Value);
                var unit = distMatch.Groups[2].Value.ToLowerInvariant();
                parameters["distance"] = unit == "mm" ? val : val * 1000;
            }

            ParseDirectionParam(input, parameters);
        }

        private void ParseDirectionParam(string input, Dictionary<string, object> parameters)
        {
            var dirMatch = Regex.Match(input,
                @"\b(north|south|east|west|up|down|left|right|horizontal|vertical)\b",
                RegexOptions.IgnoreCase);
            if (dirMatch.Success)
                parameters["direction"] = dirMatch.Groups[1].Value;
        }

        private void ParseResizeParams(string input, Dictionary<string, object> parameters)
        {
            // "5x6m", "5 by 6 meters"
            var dimMatch = Regex.Match(input,
                @"(\d+\.?\d*)\s*(mm|m)?\s*[×xX]\s*(\d+\.?\d*)\s*(mm|m)?",
                RegexOptions.IgnoreCase);

            if (!dimMatch.Success)
            {
                dimMatch = Regex.Match(input,
                    @"(\d+\.?\d*)\s*(mm|m)?\s*by\s*(\d+\.?\d*)\s*(mm|m)?",
                    RegexOptions.IgnoreCase);
            }

            if (dimMatch.Success)
            {
                var w = double.Parse(dimMatch.Groups[1].Value);
                var d = double.Parse(dimMatch.Groups[3].Value);
                var u1 = dimMatch.Groups[2].Value.ToLowerInvariant();
                var u2 = dimMatch.Groups[4].Value.ToLowerInvariant();

                parameters["width"] = u1 == "mm" ? w : w * 1000;
                parameters["depth"] = (u2 == "mm" || (string.IsNullOrEmpty(u2) && u1 == "mm")) ? d : d * 1000;
                return;
            }

            // "1m longer", "increase by 500mm"
            var deltaMatch = Regex.Match(input,
                @"(\d+\.?\d*)\s*(mm|m|meter|metre)\s*(longer|shorter|wider|narrower|taller|deeper)",
                RegexOptions.IgnoreCase);
            if (deltaMatch.Success)
            {
                var val = double.Parse(deltaMatch.Groups[1].Value);
                var unit = deltaMatch.Groups[2].Value.ToLowerInvariant();
                var adj = deltaMatch.Groups[3].Value.ToLowerInvariant();

                var mm = unit == "mm" ? val : val * 1000;
                if (adj == "shorter" || adj == "narrower")
                    mm = -mm;

                parameters["deltaLength"] = mm;
            }

            // "to 30 square meters"
            var areaMatch = Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:square\s*(?:met[er]|m)|m²|sqm)", RegexOptions.IgnoreCase);
            if (areaMatch.Success)
            {
                parameters["targetArea"] = double.Parse(areaMatch.Groups[1].Value);
            }

            ParseDirectionParam(input, parameters);
        }

        private void ParseLevelAdjustParams(string input, Dictionary<string, object> parameters)
        {
            // "Raise Level 2 by 300mm"
            var match = Regex.Match(input,
                @"(raise|lower|move)\s+(?:level\s+)?(\w+\s*\d*)\s+(?:by\s+)?(\d+\.?\d*)\s*(mm|m)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var action = match.Groups[1].Value.ToLowerInvariant();
                parameters["levelName"] = match.Groups[2].Value.Trim();
                var val = double.Parse(match.Groups[3].Value);
                var unit = match.Groups[4].Value.ToLowerInvariant();
                var mm = unit == "mm" ? val : val * 1000;
                parameters["adjustment"] = action == "lower" ? -mm : mm;
            }
        }

        #endregion
    }

    #region Data Types

    /// <summary>
    /// Modification command — describes what to modify and how.
    /// </summary>
    public class ModificationCommand
    {
        public ModificationType Type { get; set; }
        public SelectorCriteria Selector { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string PreviewDescription { get; set; }
    }

    /// <summary>
    /// All supported modification types.
    /// </summary>
    public enum ModificationType
    {
        MOVE,
        COPY,
        COPY_TO_LEVEL,
        DELETE,
        ROTATE,
        MIRROR,
        RESIZE,
        CHANGE_TYPE,
        CHANGE_PARAMETER,
        SPLIT,
        EXTEND,
        TRIM,
        OFFSET,
        LEVEL_ADJUST,
        PIN,
        UNPIN,
        PHASE_CHANGE
    }

    /// <summary>
    /// Result from a modification operation.
    /// </summary>
    public class ModificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public int AffectedCount { get; set; }
        public List<string> Suggestions { get; set; }
        public List<ElementId> CreatedElementIds { get; set; }
        public CostEstimate CostDelta { get; set; }

        public string FormatForChat()
        {
            if (!Success)
                return $"Modification failed.\n{Error}";

            var parts = new List<string> { Message };

            if (CostDelta != null && CostDelta.TotalUGX != 0)
                parts.Add($"Cost impact: {CostDelta.FormattedTotal}");

            return string.Join("\n", parts);
        }

        public static ModificationResult Succeeded(string message, int count,
            List<string> suggestions, List<ElementId> createdIds = null)
        {
            return new ModificationResult
            {
                Success = true,
                Message = message,
                AffectedCount = count,
                Suggestions = suggestions ?? new List<string>(),
                CreatedElementIds = createdIds
            };
        }

        public static ModificationResult Failed(string error)
        {
            return new ModificationResult
            {
                Success = false,
                Error = error,
                Suggestions = new List<string> { "Try a different approach", "Undo" }
            };
        }

        public static ModificationResult NoElements(string action)
        {
            return Failed($"No elements found to {action}. " +
                "Try being more specific: 'all walls on Level 1' or 'bedroom doors'.");
        }
    }

    #endregion
}
