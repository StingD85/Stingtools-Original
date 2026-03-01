// StingBIM.AI.Creation.Modification.BulkOperationsEngine
// Bulk element operations — array, align, distribute, purge, value engineer, auto-tag, renumber
// v4 Prompt Reference: Section B.4 — BULK SMART OPERATIONS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.Modification
{
    /// <summary>
    /// Engine for bulk element operations that span many elements:
    ///   ARRAY — linear/radial array of elements
    ///   ALIGN — align elements to a common reference
    ///   DISTRIBUTE — evenly distribute elements
    ///   PURGE — remove unused families and types
    ///   VALUE_ENGINEER — find cost-saving type alternatives
    ///   AUTO_TAG — tag rooms/doors/windows with labels
    ///   RENUMBER — sequential numbering by level
    /// </summary>
    public class BulkOperationsEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double FEET_TO_MM = 304.8;

        private readonly CostEstimator _costEstimator;

        public BulkOperationsEngine()
        {
            _costEstimator = new CostEstimator();
            _costEstimator.LoadRates();
        }

        /// <summary>
        /// Checks if an intent type is a bulk operation handled by this engine.
        /// </summary>
        public static bool IsBulkIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper == "ARRAY_ELEMENT" || upper == "ALIGN_ELEMENT" ||
                   upper == "DISTRIBUTE_ELEMENT" || upper == "PURGE_UNUSED" ||
                   upper == "VALUE_ENGINEER" || upper == "AUTO_TAG" ||
                   upper == "RENUMBER_ELEMENT";
        }

        /// <summary>
        /// Route a bulk NLP intent to the correct operation.
        /// </summary>
        public ModificationResult RouteIntent(Document doc, string intentType,
            Dictionary<string, object> entities, string originalInput)
        {
            try
            {
                return intentType?.ToUpperInvariant() switch
                {
                    "ARRAY_ELEMENT" => ExecuteArray(doc, entities, originalInput),
                    "ALIGN_ELEMENT" => ExecuteAlign(doc, entities, originalInput),
                    "DISTRIBUTE_ELEMENT" => ExecuteDistribute(doc, entities, originalInput),
                    "PURGE_UNUSED" => ExecutePurge(doc, entities, originalInput),
                    "VALUE_ENGINEER" => ExecuteValueEngineer(doc, entities, originalInput),
                    "AUTO_TAG" => ExecuteAutoTag(doc, entities, originalInput),
                    "RENUMBER_ELEMENT" => ExecuteRenumber(doc, entities, originalInput),
                    _ => ModificationResult.Failed($"Unknown bulk operation: {intentType}")
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"BulkOperationsEngine failed: {intentType}");
                return ModificationResult.Failed(
                    ErrorExplainer.FormatCreationError("element", "bulk operation", ex));
            }
        }

        #region ARRAY (B.1)

        /// <summary>
        /// ARRAY — Create a linear or radial array of elements.
        /// NLP example: "Array 5 columns at 4m spacing along grid A"
        /// Revit API: Copy element (count-1) times with offset
        /// </summary>
        private ModificationResult ExecuteArray(Document doc,
            Dictionary<string, object> entities, string input)
        {
            var selector = ElementSelector.FromNaturalLanguage(input, entities);
            if (selector == null)
                return ModificationResult.NoElements("array");

            var es = new ElementSelector(doc);
            var elementIds = es.Select(selector);
            if (elementIds.Count == 0)
                return ModificationResult.NoElements("array");

            // Parse count and spacing
            var count = ExtractCount(input);
            if (count <= 1)
                return ModificationResult.Failed("Array count must be > 1. Try: 'Array 5 columns at 4m spacing'");

            var spacingMm = ExtractSpacing(input);
            if (spacingMm <= 0)
                return ModificationResult.Failed("Array spacing required. Try: 'at 4m spacing'");

            var spacingFt = spacingMm * MM_TO_FEET;

            // Determine direction from element or input
            var direction = ExtractArrayDirection(doc, elementIds.First(), input);

            var copiedIds = new List<ElementId>();
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Array Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Array Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        for (int i = 1; i < count; i++)
                        {
                            var offset = direction * (spacingFt * i);
                            foreach (var id in elementIds)
                            {
                                var newIds = ElementTransformUtils.CopyElement(doc, id, offset);
                                copiedIds.AddRange(newIds);
                            }
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "array", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Arrayed {elementIds.Count} element(s) × {count} copies at {spacingMm:F0}mm spacing.\n" +
                $"{copiedIds.Count} new elements created.",
                copiedIds.Count,
                new List<string> { "Number them sequentially", "Change spacing", "Delete array" },
                copiedIds);
        }

        #endregion

        #region ALIGN (B.1)

        /// <summary>
        /// ALIGN — Align elements to a common value (position, parameter).
        /// NLP example: "Align all windows on the north facade to the same sill height"
        /// Revit API: LookupParameter().Set() or MoveElement
        /// </summary>
        private ModificationResult ExecuteAlign(Document doc,
            Dictionary<string, object> entities, string input)
        {
            var selector = ElementSelector.FromNaturalLanguage(input, entities);
            if (selector == null)
                return ModificationResult.NoElements("align");

            var es = new ElementSelector(doc);
            var elementIds = es.Select(selector);
            if (elementIds.Count < 2)
                return ModificationResult.Failed("Need at least 2 elements to align.");

            // Determine alignment type
            var alignType = DetectAlignmentType(input);
            int alignedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Align Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Align Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        switch (alignType)
                        {
                            case AlignmentType.SillHeight:
                                alignedCount = AlignBySillHeight(doc, elementIds);
                                break;

                            case AlignmentType.HeadHeight:
                                alignedCount = AlignByParameter(doc, elementIds, "Head Height");
                                break;

                            case AlignmentType.CenterX:
                                alignedCount = AlignByPosition(doc, elementIds, AlignAxis.X);
                                break;

                            case AlignmentType.CenterY:
                                alignedCount = AlignByPosition(doc, elementIds, AlignAxis.Y);
                                break;

                            case AlignmentType.Top:
                                alignedCount = AlignByPosition(doc, elementIds, AlignAxis.ZTop);
                                break;

                            default:
                                // Default: align by center X position
                                alignedCount = AlignByPosition(doc, elementIds, AlignAxis.X);
                                break;
                        }

                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "align", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Aligned {alignedCount} element(s) by {alignType}.",
                alignedCount,
                new List<string> { "Distribute evenly", "Undo", "Align differently" });
        }

        #endregion

        #region DISTRIBUTE

        /// <summary>
        /// DISTRIBUTE — Evenly distribute elements along an axis.
        /// NLP example: "Distribute all columns evenly between grid A and B"
        /// </summary>
        private ModificationResult ExecuteDistribute(Document doc,
            Dictionary<string, object> entities, string input)
        {
            var selector = ElementSelector.FromNaturalLanguage(input, entities);
            if (selector == null)
                return ModificationResult.NoElements("distribute");

            var es = new ElementSelector(doc);
            var elementIds = es.Select(selector);
            if (elementIds.Count < 3)
                return ModificationResult.Failed("Need at least 3 elements to distribute evenly.");

            // Determine axis
            var axis = input.ToLowerInvariant().Contains("vertical") ||
                       input.ToLowerInvariant().Contains("north") ||
                       input.ToLowerInvariant().Contains("south")
                ? AlignAxis.Y : AlignAxis.X;

            int count = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Distribute Elements"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Distribute Elements"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        // Get positions sorted by axis
                        var positions = elementIds
                            .Select(id => new { Id = id, Center = GetElementCenter(doc.GetElement(id)) })
                            .OrderBy(e => axis == AlignAxis.X ? e.Center.X : e.Center.Y)
                            .ToList();

                        // Calculate even spacing between first and last
                        var first = axis == AlignAxis.X ? positions.First().Center.X : positions.First().Center.Y;
                        var last = axis == AlignAxis.X ? positions.Last().Center.X : positions.Last().Center.Y;
                        var spacing = (last - first) / (positions.Count - 1);

                        // Move interior elements to evenly spaced positions
                        for (int i = 1; i < positions.Count - 1; i++)
                        {
                            var targetPos = first + spacing * i;
                            var currentPos = axis == AlignAxis.X
                                ? positions[i].Center.X : positions[i].Center.Y;
                            var delta = targetPos - currentPos;

                            if (Math.Abs(delta) > 0.001)
                            {
                                var offset = axis == AlignAxis.X
                                    ? new XYZ(delta, 0, 0) : new XYZ(0, delta, 0);
                                ElementTransformUtils.MoveElement(doc, positions[i].Id, offset);
                                count++;
                            }
                        }

                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("element", "distribute", ex));
                    }
                }

                tg.Assimilate();
            }

            var spacingMm = 0.0;
            if (count > 0)
            {
                var allPositions = elementIds
                    .Select(id => GetElementCenter(doc.GetElement(id)))
                    .OrderBy(c => axis == AlignAxis.X ? c.X : c.Y)
                    .ToList();
                var firstPos = axis == AlignAxis.X ? allPositions.First().X : allPositions.First().Y;
                var lastPos = axis == AlignAxis.X ? allPositions.Last().X : allPositions.Last().Y;
                spacingMm = ((lastPos - firstPos) / (allPositions.Count - 1)) * FEET_TO_MM;
            }

            return ModificationResult.Succeeded(
                $"Distributed {elementIds.Count} elements evenly ({spacingMm:F0}mm spacing).",
                count,
                new List<string> { "Align to grid", "Undo", "Number them" });
        }

        #endregion

        #region PURGE (B.4)

        /// <summary>
        /// PURGE — Remove all unused families and types from the project.
        /// NLP example: "Remove all unused families and types"
        /// Revit API: doc.GetUnusedElements + PerformanceAdviser
        /// </summary>
        private ModificationResult ExecutePurge(Document doc,
            Dictionary<string, object> entities, string input)
        {
            // Find unused elements
            var allTypeIds = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .Select(e => e.Id)
                .ToList();

            // Check which types are actually used
            var usedTypeIds = new HashSet<ElementId>();
            var allInstances = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var inst in allInstances)
            {
                var typeId = inst.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                    usedTypeIds.Add(typeId);
            }

            var unusedTypeIds = allTypeIds.Where(id => !usedTypeIds.Contains(id)).ToList();

            if (unusedTypeIds.Count == 0)
                return ModificationResult.Succeeded(
                    "No unused types found. The project is clean.",
                    0, new List<string> { "Review model health", "Generate BOQ" });

            // Delete unused types
            int deletedCount = 0;
            int failedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Purge Unused"))
            {
                tg.Start();

                using (var t = new Transaction(doc, "Purge Unused Types"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        foreach (var id in unusedTypeIds)
                        {
                            try
                            {
                                doc.Delete(id);
                                deletedCount++;
                            }
                            catch
                            {
                                // Some types can't be deleted (system types, last of kind)
                                failedCount++;
                            }
                        }
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError("type", "purge", ex));
                    }
                }

                tg.Assimilate();
            }

            return ModificationResult.Succeeded(
                $"Purged {deletedCount} unused type(s).\n" +
                (failedCount > 0 ? $"{failedCount} system types could not be removed.\n" : "") +
                "Project file size should be reduced after saving.",
                deletedCount,
                new List<string> { "Purge unused views", "Review model health", "Save project" });
        }

        #endregion

        #region VALUE ENGINEER (B.4)

        /// <summary>
        /// VALUE_ENGINEER — Find cheaper alternatives for expensive elements.
        /// NLP example: "Find elements I can downgrade to save 10% of construction cost"
        /// </summary>
        private ModificationResult ExecuteValueEngineer(Document doc,
            Dictionary<string, object> entities, string input)
        {
            // Parse target savings percentage
            var savingsMatch = Regex.Match(input, @"(\d+)\s*%", RegexOptions.IgnoreCase);
            var targetPercent = savingsMatch.Success ? double.Parse(savingsMatch.Groups[1].Value) : 10.0;

            // Build cost breakdown by category + type
            var costBreakdown = new List<ValueEngineerOption>();

            var categories = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Ceilings
            };

            foreach (var cat in categories)
            {
                try
                {
                    var elements = new FilteredElementCollector(doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();

                    // Group by type
                    var groups = elements.GroupBy(e => e.GetTypeId());

                    foreach (var group in groups)
                    {
                        var typeName = doc.GetElement(group.Key)?.Name ?? "Unknown";
                        var count = group.Count();

                        // Estimate cost using cost estimator
                        var unitCost = _costEstimator.GetUnitRate(cat.ToString(), typeName);

                        // Find cheaper alternatives of the same category
                        var alternatives = new FilteredElementCollector(doc)
                            .OfCategoryId(new ElementId((int)cat))
                            .WhereElementIsElementType()
                            .Where(t => t.Id != group.Key)
                            .ToList();

                        foreach (var alt in alternatives)
                        {
                            var altCost = _costEstimator.GetUnitRate(cat.ToString(), alt.Name);
                            if (altCost > 0 && altCost < unitCost)
                            {
                                var savingPerUnit = unitCost - altCost;
                                costBreakdown.Add(new ValueEngineerOption
                                {
                                    CurrentType = typeName,
                                    AlternativeType = alt.Name,
                                    Category = cat.ToString().Replace("OST_", ""),
                                    ElementCount = count,
                                    SavingPerUnit = savingPerUnit,
                                    TotalSaving = savingPerUnit * count,
                                    AlternativeTypeId = alt.Id
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Category may not exist
                }
            }

            if (costBreakdown.Count == 0)
            {
                return ModificationResult.Succeeded(
                    "No cost-saving alternatives found with loaded types.\n" +
                    "Load additional family types to explore more options.",
                    0,
                    new List<string> { "Load more families", "Review current costs", "Generate BOQ" });
            }

            // Sort by total saving, take top options
            var ranked = costBreakdown.OrderByDescending(o => o.TotalSaving).Take(10).ToList();
            var totalPotentialSaving = ranked.Sum(o => o.TotalSaving);

            var lines = new List<string>();
            lines.Add($"Value Engineering Options (target: {targetPercent}% savings):");
            lines.Add("─────────────────────────────────────────");

            int rank = 1;
            foreach (var option in ranked)
            {
                lines.Add($"{rank}. {option.Category}: {option.CurrentType} → {option.AlternativeType}");
                lines.Add($"   {option.ElementCount} elements, saves UGX {option.TotalSaving:N0} " +
                    $"(UGX {option.SavingPerUnit:N0}/unit)");
                rank++;
            }

            lines.Add("─────────────────────────────────────────");
            lines.Add($"Total potential saving: UGX {totalPotentialSaving:N0}");

            return ModificationResult.Succeeded(
                string.Join("\n", lines),
                ranked.Count,
                new List<string> { "Apply all changes", "Review one by one", "Generate full BOQ" });
        }

        #endregion

        #region AUTO_TAG (B.4)

        /// <summary>
        /// AUTO_TAG — Tag rooms, doors, windows with labels.
        /// NLP examples:
        ///   "Tag all rooms with area and name"
        ///   "Add door marks to all doors"
        /// Revit API: IndependentTag.Create() at element centroid
        /// </summary>
        private ModificationResult ExecuteAutoTag(Document doc,
            Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Determine what to tag
            var tagCategory = BuiltInCategory.INVALID;
            string tagDescription;

            if (lower.Contains("room"))
            {
                tagCategory = BuiltInCategory.OST_Rooms;
                tagDescription = "rooms";
            }
            else if (lower.Contains("door"))
            {
                tagCategory = BuiltInCategory.OST_Doors;
                tagDescription = "doors";
            }
            else if (lower.Contains("window"))
            {
                tagCategory = BuiltInCategory.OST_Windows;
                tagDescription = "windows";
            }
            else if (lower.Contains("wall"))
            {
                tagCategory = BuiltInCategory.OST_Walls;
                tagDescription = "walls";
            }
            else
            {
                return ModificationResult.Failed(
                    "What elements should I tag? Try: 'Tag all rooms' or 'Tag all doors'");
            }

            // Find elements to tag
            var elements = new FilteredElementCollector(doc)
                .OfCategory(tagCategory)
                .WhereElementIsNotElementType()
                .ToList();

            if (elements.Count == 0)
                return ModificationResult.Failed($"No {tagDescription} found in the model.");

            // Find an active view for tag placement
            var view = doc.ActiveView;
            if (view == null)
                return ModificationResult.Failed("No active view. Open a plan view first.");

            // Find appropriate tag family
            var tagType = FindTagType(doc, tagCategory);

            int taggedCount = 0;
            int numberedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var tg = new TransactionGroup(doc, "StingBIM: Auto-Tag"))
            {
                tg.Start();

                using (var t = new Transaction(doc, $"Tag All {tagDescription}"))
                {
                    var options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failureHandler);
                    t.SetFailureHandlingOptions(options);
                    t.Start();

                    try
                    {
                        // Auto-number if doors/windows
                        if (tagCategory == BuiltInCategory.OST_Doors ||
                            tagCategory == BuiltInCategory.OST_Windows)
                        {
                            var prefix = tagCategory == BuiltInCategory.OST_Doors ? "D" : "W";

                            // Group by level for level-prefixed numbering
                            var byLevel = elements
                                .GroupBy(e => e.LevelId)
                                .OrderBy(g => (doc.GetElement(g.Key) as Level)?.Elevation ?? 0);

                            foreach (var levelGroup in byLevel)
                            {
                                var level = doc.GetElement(levelGroup.Key) as Level;
                                var levelNum = level != null
                                    ? Regex.Match(level.Name, @"\d+").Value : "0";
                                if (string.IsNullOrEmpty(levelNum)) levelNum = "0";

                                int seq = 1;
                                foreach (var elem in levelGroup)
                                {
                                    var mark = $"{prefix}{levelNum}-{seq:D3}";
                                    var markParam = elem.LookupParameter("Mark");
                                    if (markParam != null && !markParam.IsReadOnly)
                                    {
                                        markParam.Set(mark);
                                        numberedCount++;
                                    }
                                    seq++;
                                }
                            }
                        }

                        // Place tags at element centroids
                        if (tagType != null)
                        {
                            foreach (var elem in elements)
                            {
                                try
                                {
                                    var center = GetElementCenter(elem);
                                    var tagRef = new Reference(elem);
                                    IndependentTag.Create(doc, tagType.Id, view.Id,
                                        tagRef, false, TagOrientation.Horizontal,
                                        new XYZ(center.X, center.Y, center.Z));
                                    taggedCount++;
                                }
                                catch
                                {
                                    // Tag may already exist or element can't be tagged in this view
                                }
                            }
                        }

                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        return ModificationResult.Failed(
                            ErrorExplainer.FormatCreationError(tagDescription, "tag", ex));
                    }
                }

                tg.Assimilate();
            }

            var msg = $"Auto-tagged {tagDescription}:";
            if (taggedCount > 0) msg += $"\n  Tags placed: {taggedCount}";
            if (numberedCount > 0) msg += $"\n  Elements numbered: {numberedCount}";
            if (taggedCount == 0 && numberedCount == 0)
                msg += "\n  No tags placed (tag family may not be loaded).";

            return ModificationResult.Succeeded(msg,
                Math.Max(taggedCount, numberedCount),
                new List<string> { "Tag another category", "Generate schedule", "Review tags" });
        }

        #endregion

        #region RENUMBER (B.4)

        /// <summary>
        /// RENUMBER — Sequentially renumber elements by level.
        /// NLP example: "Number all doors sequentially by level"
        /// Pattern: Level 1: D1-001, D1-002... Level 2: D2-001, D2-002...
        /// </summary>
        private ModificationResult ExecuteRenumber(Document doc,
            Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Determine category to renumber
            BuiltInCategory category;
            string prefix;
            string description;

            if (lower.Contains("door"))
            {
                category = BuiltInCategory.OST_Doors;
                prefix = "D";
                description = "doors";
            }
            else if (lower.Contains("window"))
            {
                category = BuiltInCategory.OST_Windows;
                prefix = "W";
                description = "windows";
            }
            else if (lower.Contains("room"))
            {
                category = BuiltInCategory.OST_Rooms;
                prefix = "R";
                description = "rooms";
            }
            else if (lower.Contains("column"))
            {
                category = BuiltInCategory.OST_StructuralColumns;
                prefix = "C";
                description = "columns";
            }
            else
            {
                return ModificationResult.Failed(
                    "What should I renumber? Try: 'Number all doors by level'");
            }

            var elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToList();

            if (elements.Count == 0)
                return ModificationResult.Failed($"No {description} found in the model.");

            int numberedCount = 0;
            var failureHandler = new StingBIMFailurePreprocessor();

            using (var t = new Transaction(doc, $"Renumber {description}"))
            {
                var options = t.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(failureHandler);
                t.SetFailureHandlingOptions(options);
                t.Start();

                try
                {
                    // Group by level, sorted by elevation
                    var byLevel = elements
                        .GroupBy(e => e.LevelId)
                        .OrderBy(g => (doc.GetElement(g.Key) as Level)?.Elevation ?? 0);

                    foreach (var levelGroup in byLevel)
                    {
                        var level = doc.GetElement(levelGroup.Key) as Level;
                        var levelNum = level != null
                            ? Regex.Match(level.Name, @"\d+").Value : "0";
                        if (string.IsNullOrEmpty(levelNum)) levelNum = "0";

                        // Sort elements within level by position (X then Y)
                        var sorted = levelGroup
                            .OrderBy(e => GetElementCenter(e).X)
                            .ThenBy(e => GetElementCenter(e).Y)
                            .ToList();

                        int seq = 1;
                        foreach (var elem in sorted)
                        {
                            var mark = $"{prefix}{levelNum}-{seq:D3}";

                            var markParam = elem.LookupParameter("Mark");
                            if (markParam != null && !markParam.IsReadOnly)
                            {
                                markParam.Set(mark);
                                numberedCount++;
                            }
                            else if (category == BuiltInCategory.OST_Rooms)
                            {
                                // Rooms use Number parameter
                                var numParam = elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                                if (numParam != null && !numParam.IsReadOnly)
                                {
                                    numParam.Set(mark);
                                    numberedCount++;
                                }
                            }

                            seq++;
                        }
                    }

                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    return ModificationResult.Failed(
                        ErrorExplainer.FormatCreationError(description, "renumber", ex));
                }
            }

            return ModificationResult.Succeeded(
                $"Renumbered {numberedCount} {description}.\n" +
                $"Pattern: {prefix}[Level]-[Seq] (e.g., {prefix}1-001, {prefix}1-002, {prefix}2-001...)",
                numberedCount,
                new List<string> { "Generate schedule", "Tag elements", "Renumber another category" });
        }

        #endregion

        #region Helpers

        private XYZ GetElementCenter(Element elem)
        {
            if (elem == null) return XYZ.Zero;

            if (elem.Location is LocationPoint lp)
                return lp.Point;

            if (elem.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            var bbox = elem.get_BoundingBox(null);
            if (bbox != null)
                return (bbox.Min + bbox.Max) / 2.0;

            return XYZ.Zero;
        }

        private int ExtractCount(string input)
        {
            // "Array 5 columns", "create 10 copies"
            var match = Regex.Match(input, @"(\d+)\s*(?:copies|copy|elements?|columns?|beams?|walls?)",
                RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                return count;

            // "array of 5"
            match = Regex.Match(input, @"(?:array|repeat)\s+(?:of\s+)?(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out count))
                return count;

            return 0;
        }

        private double ExtractSpacing(string input)
        {
            // "at 4m spacing", "4000mm apart", "spaced 4 meters"
            var match = Regex.Match(input,
                @"(?:at\s+|spac\w+\s+)?(\d+\.?\d*)\s*(mm|m|meter|metre)\s*(?:spacing|apart|between)?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var val = double.Parse(match.Groups[1].Value);
                var unit = match.Groups[2].Value.ToLowerInvariant();
                return unit == "mm" ? val : val * 1000;
            }

            return 0;
        }

        private XYZ ExtractArrayDirection(Document doc, ElementId sourceId, string input)
        {
            var lower = input.ToLowerInvariant();

            if (lower.Contains("north")) return XYZ.BasisY;
            if (lower.Contains("south")) return -XYZ.BasisY;
            if (lower.Contains("east") || lower.Contains("right")) return XYZ.BasisX;
            if (lower.Contains("west") || lower.Contains("left")) return -XYZ.BasisX;
            if (lower.Contains("vertical") || lower.Contains("up")) return XYZ.BasisZ;

            // Try to infer from element direction (e.g., along a grid)
            var elem = doc.GetElement(sourceId);
            if (elem?.Location is LocationCurve lc && lc.Curve is Line line)
                return line.Direction;

            // Default: along X axis
            return XYZ.BasisX;
        }

        private AlignmentType DetectAlignmentType(string input)
        {
            var lower = input.ToLowerInvariant();

            if (lower.Contains("sill")) return AlignmentType.SillHeight;
            if (lower.Contains("head")) return AlignmentType.HeadHeight;
            if (lower.Contains("top")) return AlignmentType.Top;
            if (lower.Contains("center") && lower.Contains("horizontal"))
                return AlignmentType.CenterX;
            if (lower.Contains("center") && lower.Contains("vertical"))
                return AlignmentType.CenterY;

            // Default based on element type
            if (lower.Contains("window") || lower.Contains("door"))
                return AlignmentType.SillHeight;

            return AlignmentType.CenterX;
        }

        private int AlignBySillHeight(Document doc, List<ElementId> elementIds)
        {
            // Find median sill height
            var sillHeights = new List<double>();
            foreach (var id in elementIds)
            {
                var elem = doc.GetElement(id);
                var sillParam = elem?.LookupParameter("Sill Height") ??
                    elem?.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                if (sillParam != null)
                    sillHeights.Add(sillParam.AsDouble());
            }

            if (sillHeights.Count == 0) return 0;

            sillHeights.Sort();
            var targetHeight = sillHeights[sillHeights.Count / 2]; // Median

            int count = 0;
            foreach (var id in elementIds)
            {
                var elem = doc.GetElement(id);
                var sillParam = elem?.LookupParameter("Sill Height") ??
                    elem?.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                if (sillParam != null && !sillParam.IsReadOnly &&
                    Math.Abs(sillParam.AsDouble() - targetHeight) > 0.001)
                {
                    sillParam.Set(targetHeight);
                    count++;
                }
            }

            return count;
        }

        private int AlignByParameter(Document doc, List<ElementId> elementIds, string paramName)
        {
            var values = new List<double>();
            foreach (var id in elementIds)
            {
                var param = doc.GetElement(id)?.LookupParameter(paramName);
                if (param != null) values.Add(param.AsDouble());
            }

            if (values.Count == 0) return 0;

            values.Sort();
            var target = values[values.Count / 2];

            int count = 0;
            foreach (var id in elementIds)
            {
                var param = doc.GetElement(id)?.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly &&
                    Math.Abs(param.AsDouble() - target) > 0.001)
                {
                    param.Set(target);
                    count++;
                }
            }

            return count;
        }

        private int AlignByPosition(Document doc, List<ElementId> elementIds, AlignAxis axis)
        {
            // Get positions along the given axis
            var positions = elementIds
                .Select(id => new
                {
                    Id = id,
                    Pos = GetAxisValue(GetElementCenter(doc.GetElement(id)), axis)
                })
                .ToList();

            if (positions.Count == 0) return 0;

            // Target = median position
            var sorted = positions.OrderBy(p => p.Pos).ToList();
            var target = sorted[sorted.Count / 2].Pos;

            int count = 0;
            foreach (var item in positions)
            {
                var delta = target - item.Pos;
                if (Math.Abs(delta) > 0.001)
                {
                    var offset = axis switch
                    {
                        AlignAxis.X => new XYZ(delta, 0, 0),
                        AlignAxis.Y => new XYZ(0, delta, 0),
                        AlignAxis.ZTop => new XYZ(0, 0, delta),
                        _ => XYZ.Zero
                    };

                    ElementTransformUtils.MoveElement(doc, item.Id, offset);
                    count++;
                }
            }

            return count;
        }

        private double GetAxisValue(XYZ point, AlignAxis axis)
        {
            return axis switch
            {
                AlignAxis.X => point.X,
                AlignAxis.Y => point.Y,
                AlignAxis.ZTop => point.Z,
                _ => point.X
            };
        }

        private FamilySymbol FindTagType(Document doc, BuiltInCategory elementCategory)
        {
            // Map element category to tag category
            var tagCat = elementCategory switch
            {
                BuiltInCategory.OST_Rooms => BuiltInCategory.OST_RoomTags,
                BuiltInCategory.OST_Doors => BuiltInCategory.OST_DoorTags,
                BuiltInCategory.OST_Windows => BuiltInCategory.OST_WindowTags,
                BuiltInCategory.OST_Walls => BuiltInCategory.OST_WallTags,
                _ => BuiltInCategory.INVALID
            };

            if (tagCat == BuiltInCategory.INVALID) return null;

            return new FilteredElementCollector(doc)
                .OfCategory(tagCat)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.IsActive) ??
                new FilteredElementCollector(doc)
                .OfCategory(tagCat)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        #endregion
    }

    #region Supporting Types

    internal enum AlignmentType
    {
        SillHeight,
        HeadHeight,
        CenterX,
        CenterY,
        Top
    }

    internal enum AlignAxis
    {
        X,
        Y,
        ZTop
    }

    internal class ValueEngineerOption
    {
        public string CurrentType { get; set; }
        public string AlternativeType { get; set; }
        public string Category { get; set; }
        public int ElementCount { get; set; }
        public double SavingPerUnit { get; set; }
        public double TotalSaving { get; set; }
        public ElementId AlternativeTypeId { get; set; }
    }

    #endregion
}
