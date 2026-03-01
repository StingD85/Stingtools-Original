// StingBIM.AI.Creation.Modification.ElementSelector
// Finds and filters Revit elements using 10 selector types with AND/OR combination
// v4 Prompt Reference: Section B.0 — ElementSelector Variants

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Creation.Modification
{
    /// <summary>
    /// Flexible element selection engine supporting 10 selector types
    /// that can be combined with AND/OR logic.
    /// </summary>
    public class ElementSelector
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;

        public ElementSelector(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Resolves a SelectorCriteria into a concrete list of Element IDs.
        /// </summary>
        public List<ElementId> Select(SelectorCriteria criteria)
        {
            if (criteria == null)
                return new List<ElementId>();

            try
            {
                var result = ResolveSelector(criteria);
                Logger.Info($"ElementSelector resolved {result.Count} elements for {criteria.Type}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"ElementSelector failed for {criteria.Type}");
                return new List<ElementId>();
            }
        }

        /// <summary>
        /// Resolves a single element from natural language description.
        /// Returns null if no match or ambiguous.
        /// </summary>
        public ElementId SelectSingle(SelectorCriteria criteria)
        {
            var results = Select(criteria);
            return results.Count == 1 ? results[0] : null;
        }

        private List<ElementId> ResolveSelector(SelectorCriteria criteria)
        {
            switch (criteria.Type)
            {
                case SelectorType.ByElementId:
                    return ResolveById(criteria);

                case SelectorType.ByCategory:
                    return ResolveByCategory(criteria);

                case SelectorType.ByRoomName:
                    return ResolveByRoomName(criteria);

                case SelectorType.ByLevel:
                    return ResolveByLevel(criteria);

                case SelectorType.ByType:
                    return ResolveByType(criteria);

                case SelectorType.ByParameter:
                    return ResolveByParameter(criteria);

                case SelectorType.BySelection:
                    return ResolveBySelection(criteria);

                case SelectorType.BySpatialFilter:
                    return ResolveBySpatialFilter(criteria);

                case SelectorType.ByPhase:
                    return ResolveByPhase(criteria);

                case SelectorType.ByWorkset:
                    return ResolveByWorkset(criteria);

                case SelectorType.ByHost:
                    return ResolveByHost(criteria);

                case SelectorType.Combined:
                    return ResolveCombined(criteria);

                default:
                    Logger.Warn($"Unknown selector type: {criteria.Type}");
                    return new List<ElementId>();
            }
        }

        #region Selector Implementations

        private List<ElementId> ResolveById(SelectorCriteria criteria)
        {
            if (criteria.ElementId != null && criteria.ElementId != ElementId.InvalidElementId)
            {
                var elem = _document.GetElement(criteria.ElementId);
                if (elem != null)
                    return new List<ElementId> { criteria.ElementId };
            }
            return new List<ElementId>();
        }

        private List<ElementId> ResolveByCategory(SelectorCriteria criteria)
        {
            if (criteria.Category == null)
                return new List<ElementId>();

            return new FilteredElementCollector(_document)
                .OfCategory(criteria.Category.Value)
                .WhereElementIsNotElementType()
                .Select(e => e.Id)
                .ToList();
        }

        private List<ElementId> ResolveByRoomName(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.NamePattern))
                return new List<ElementId>();

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .ToList();

            Regex regex;
            try
            {
                regex = new Regex(criteria.NamePattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // Fall back to contains match if not valid regex
                return rooms
                    .Where(r => r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()?
                        .IndexOf(criteria.NamePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(r => r.Id)
                    .ToList();
            }

            return rooms
                .Where(r =>
                {
                    var name = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                    return regex.IsMatch(name);
                })
                .Select(r => r.Id)
                .ToList();
        }

        private List<ElementId> ResolveByLevel(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.LevelName))
                return new List<ElementId>();

            // Find the level
            var level = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l =>
                    l.Name.IndexOf(criteria.LevelName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (level == null)
                return new List<ElementId>();

            // Get all elements on that level
            var levelFilter = new ElementLevelFilter(level.Id);
            return new FilteredElementCollector(_document)
                .WherePasses(levelFilter)
                .WhereElementIsNotElementType()
                .Select(e => e.Id)
                .ToList();
        }

        private List<ElementId> ResolveByType(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.NamePattern))
                return new List<ElementId>();

            var pattern = criteria.NamePattern.ToLowerInvariant();
            var keywords = pattern.Split(new[] { ' ', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries);

            var results = new List<ElementId>();

            // Search across common categories
            var categories = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_Railings,
            };

            foreach (var cat in categories)
            {
                try
                {
                    var elements = new FilteredElementCollector(_document)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var elem in elements)
                    {
                        var typeName = elem.Name?.ToLowerInvariant() ?? "";
                        if (keywords.All(k => typeName.Contains(k)))
                        {
                            results.Add(elem.Id);
                        }
                    }
                }
                catch
                {
                    // Category may not exist in document
                }
            }

            return results;
        }

        private List<ElementId> ResolveByParameter(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.ParameterName))
                return new List<ElementId>();

            var results = new List<ElementId>();

            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType();

            foreach (var elem in collector)
            {
                try
                {
                    var param = elem.LookupParameter(criteria.ParameterName);
                    if (param == null) continue;

                    if (criteria.ParameterValue == null)
                    {
                        // Just check parameter exists
                        results.Add(elem.Id);
                        continue;
                    }

                    // Compare values
                    var paramValueStr = GetParameterValueAsString(param);
                    if (paramValueStr != null &&
                        paramValueStr.Equals(criteria.ParameterValue.ToString(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(elem.Id);
                    }
                }
                catch
                {
                    // Skip elements that cause issues
                }
            }

            return results;
        }

        private List<ElementId> ResolveBySelection(SelectorCriteria criteria)
        {
            // Current Revit selection — requires UIDocument access
            // In chat context, we don't have UIDocument, so return pre-set IDs
            return criteria.PreSelectedIds ?? new List<ElementId>();
        }

        private List<ElementId> ResolveBySpatialFilter(SelectorCriteria criteria)
        {
            if (criteria.Center == null || criteria.Radius <= 0)
                return new List<ElementId>();

            var radiusFt = criteria.Radius / 304.8; // mm to feet

            var results = new List<ElementId>();
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType();

            foreach (var elem in collector)
            {
                try
                {
                    var loc = elem.Location;
                    if (loc is LocationPoint lp)
                    {
                        if (lp.Point.DistanceTo(criteria.Center) <= radiusFt)
                            results.Add(elem.Id);
                    }
                    else if (loc is LocationCurve lc)
                    {
                        var midpoint = lc.Curve.Evaluate(0.5, true);
                        if (midpoint.DistanceTo(criteria.Center) <= radiusFt)
                            results.Add(elem.Id);
                    }
                }
                catch
                {
                    // Skip problematic elements
                }
            }

            return results;
        }

        private List<ElementId> ResolveByPhase(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.PhaseName))
                return new List<ElementId>();

            var phase = new FilteredElementCollector(_document)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(p =>
                    p.Name.IndexOf(criteria.PhaseName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (phase == null)
                return new List<ElementId>();

            return new FilteredElementCollector(_document)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    try
                    {
                        var createdPhaseId = e.CreatedPhaseId;
                        return createdPhaseId == phase.Id;
                    }
                    catch { return false; }
                })
                .Select(e => e.Id)
                .ToList();
        }

        private List<ElementId> ResolveByWorkset(SelectorCriteria criteria)
        {
            if (string.IsNullOrEmpty(criteria.WorksetName))
                return new List<ElementId>();

            if (!_document.IsWorkshared) return new List<ElementId>();

            var worksetTable = _document.GetWorksetTable();
            var worksets = new FilteredWorksetCollector(_document)
                .OfKind(WorksetKind.UserWorkset)
                .ToList();

            var workset = worksets.FirstOrDefault(w =>
                w.Name.IndexOf(criteria.WorksetName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (workset == null) return new List<ElementId>();

            var filter = new ElementWorksetFilter(workset.Id);
            return new FilteredElementCollector(_document)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .Select(e => e.Id)
                .ToList();
        }

        private List<ElementId> ResolveByHost(SelectorCriteria criteria)
        {
            if (criteria.HostElementId == null || criteria.HostElementId == ElementId.InvalidElementId)
                return new List<ElementId>();

            var results = new List<ElementId>();
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType();

            foreach (var elem in collector)
            {
                try
                {
                    if (elem is FamilyInstance fi && fi.Host?.Id == criteria.HostElementId)
                    {
                        results.Add(elem.Id);
                    }
                }
                catch
                {
                    // Skip
                }
            }

            return results;
        }

        private List<ElementId> ResolveCombined(SelectorCriteria criteria)
        {
            if (criteria.SubSelectors == null || criteria.SubSelectors.Count == 0)
                return new List<ElementId>();

            if (criteria.CombineMode == CombineMode.AND)
            {
                // Intersection — start with first, intersect with rest
                HashSet<ElementId> result = null;
                foreach (var sub in criteria.SubSelectors)
                {
                    var subResult = new HashSet<ElementId>(Select(sub));
                    if (result == null)
                        result = subResult;
                    else
                        result.IntersectWith(subResult);
                }
                return result?.ToList() ?? new List<ElementId>();
            }
            else
            {
                // Union
                var result = new HashSet<ElementId>();
                foreach (var sub in criteria.SubSelectors)
                {
                    result.UnionWith(Select(sub));
                }
                return result.ToList();
            }
        }

        #endregion

        #region NLP Parsing

        /// <summary>
        /// Parses a SelectorCriteria from NLP-extracted entities and user input text.
        /// Recognizes patterns like:
        ///   "all walls on Level 1" → ByCategory(Walls) AND ByLevel("Level 1")
        ///   "bedroom walls" → ByRoomName("Bedroom") AND ByCategory(Walls)
        ///   "200mm brick walls" → ByType("200mm brick")
        ///   "corridor and stairwell walls" → ByRoomName("Corridor|Stairwell") AND ByCategory(Walls)
        /// </summary>
        public static SelectorCriteria FromNaturalLanguage(string input, Dictionary<string, object> entities)
        {
            var selectors = new List<SelectorCriteria>();

            // 1. Check for explicit element ID in entities
            if (entities != null && entities.TryGetValue("elementId", out var idObj))
            {
                if (long.TryParse(idObj.ToString(), out var id))
                {
                    return SelectorCriteria.ById(new ElementId(id));
                }
            }

            // 2. Determine category from input
            var categoryName = ExtractCategoryFromInput(input);
            if (!string.IsNullOrEmpty(categoryName))
            {
                var cat = ResolveCategoryName(categoryName);
                if (cat != BuiltInCategory.INVALID)
                {
                    selectors.Add(SelectorCriteria.ByCategory(cat));
                }
            }

            // 3. Check for level filter: "on Level 1", "at Level 2"
            var levelMatch = Regex.Match(input,
                @"(?:on|at)\s+level\s+([\w\s]*\d+)", RegexOptions.IgnoreCase);
            if (levelMatch.Success)
            {
                selectors.Add(SelectorCriteria.ByLevel(levelMatch.Groups[1].Value.Trim()));
            }

            // 4. Check for room-based filter: "bedroom walls", "in the corridor"
            var roomName = ExtractRoomFromInput(input);
            if (!string.IsNullOrEmpty(roomName))
            {
                selectors.Add(SelectorCriteria.ByRoomName(roomName));
            }

            // 5. Check for type pattern: "200mm brick", "fire-rated", "timber"
            var typePattern = ExtractTypePatternFromInput(input);
            if (!string.IsNullOrEmpty(typePattern))
            {
                selectors.Add(SelectorCriteria.ByTypeName(typePattern));
            }

            // 6. Check for parameter filter from entities
            if (entities != null && entities.TryGetValue("parameterName", out var pName) &&
                entities.TryGetValue("parameterValue", out var pValue))
            {
                selectors.Add(SelectorCriteria.ByParam(pName.ToString(), pValue));
            }

            // 7. Check for workset filter: "in workset X"
            var worksetMatch = Regex.Match(input,
                @"(?:in\s+)?workset\s+[""']?(\w+)[""']?", RegexOptions.IgnoreCase);
            if (worksetMatch.Success)
            {
                selectors.Add(SelectorCriteria.ByWorksetName(worksetMatch.Groups[1].Value));
            }

            // Combine selectors
            if (selectors.Count == 0)
                return null; // Caller decides fallback

            if (selectors.Count == 1)
                return selectors[0];

            return SelectorCriteria.And(selectors.ToArray());
        }

        /// <summary>
        /// Returns a human-readable description of what this criteria will match.
        /// </summary>
        public static string Describe(SelectorCriteria criteria)
        {
            if (criteria == null) return "no elements";

            return criteria.Type switch
            {
                SelectorType.ByElementId => $"element #{criteria.ElementId}",
                SelectorType.ByCategory => $"all {criteria.Category} elements",
                SelectorType.ByRoomName => $"elements in rooms matching '{criteria.NamePattern}'",
                SelectorType.ByLevel => $"elements on '{criteria.LevelName}'",
                SelectorType.ByType => $"elements of type '{criteria.NamePattern}'",
                SelectorType.ByParameter =>
                    $"elements where {criteria.ParameterName} = {criteria.ParameterValue}",
                SelectorType.BySelection => "currently selected elements",
                SelectorType.BySpatialFilter =>
                    $"elements within {criteria.Radius:F0}mm radius",
                SelectorType.ByPhase => $"elements in phase '{criteria.PhaseName}'",
                SelectorType.ByWorkset => $"elements in workset '{criteria.WorksetName}'",
                SelectorType.ByHost => $"elements hosted on #{criteria.HostElementId}",
                SelectorType.Combined =>
                    criteria.CombineMode == CombineMode.AND
                        ? string.Join(" AND ", criteria.SubSelectors.Select(s => Describe(s)))
                        : string.Join(" OR ", criteria.SubSelectors.Select(s => Describe(s))),
                _ => "unknown selector"
            };
        }

        private static string ExtractCategoryFromInput(string input)
        {
            var lower = input.ToLowerInvariant();
            // Ordered longest-first to match "curtain wall" before "wall"
            var categories = new[]
            {
                "curtain wall", "cable tray", "fire alarm", "structural column",
                "mechanical equipment", "plumbing fixture", "electrical fixture",
                "wall", "door", "window", "floor", "ceiling", "roof", "room",
                "column", "beam", "foundation", "stair", "staircase", "railing",
                "ramp", "furniture", "pipe", "duct", "conduit", "light",
                "sprinkler", "element"
            };

            foreach (var cat in categories)
            {
                if (lower.Contains(cat) || lower.Contains(cat + "s"))
                    return cat;
            }

            return null;
        }

        private static string ExtractRoomFromInput(string input)
        {
            // "bedroom walls", "corridor and stairwell walls", "in the living room"
            var multiRoomMatch = Regex.Match(input,
                @"(bedroom|kitchen|bathroom|living\s*room|corridor|stairwell|lobby|office|dining|store)(?:\s*(?:and|or|,)\s*(bedroom|kitchen|bathroom|living\s*room|corridor|stairwell|lobby|office|dining|store))*",
                RegexOptions.IgnoreCase);

            if (multiRoomMatch.Success)
            {
                var rooms = new List<string>();
                foreach (Capture cap in multiRoomMatch.Groups[1].Captures)
                    rooms.Add(cap.Value.Trim());
                foreach (Capture cap in multiRoomMatch.Groups[2].Captures)
                    rooms.Add(cap.Value.Trim());

                rooms = rooms.Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
                if (rooms.Count > 0)
                    return string.Join("|", rooms);
            }

            // "in the X" pattern
            var inRoomMatch = Regex.Match(input,
                @"in\s+(?:the\s+)?(bedroom|kitchen|bathroom|living\s*room|corridor|stairwell|lobby|office|dining|store)\s*(\d*)",
                RegexOptions.IgnoreCase);
            if (inRoomMatch.Success)
            {
                var name = inRoomMatch.Groups[1].Value.Trim();
                var num = inRoomMatch.Groups[2].Value;
                return string.IsNullOrEmpty(num) ? name : $"{name} {num}";
            }

            return null;
        }

        private static string ExtractTypePatternFromInput(string input)
        {
            // Match "200mm brick", "fire-rated", "timber frame", etc.
            var match = Regex.Match(input,
                @"(\d+\s*mm\s+\w+|fire[\s-]rated|timber|steel|concrete|brick|glass|aluminium|aluminum|gypsum|plasterboard)",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// Resolves a plain-language category name to BuiltInCategory.
        /// </summary>
        public static BuiltInCategory ResolveCategoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BuiltInCategory.INVALID;

            var lower = name.Trim().ToLowerInvariant();
            var singular = lower.EndsWith("s") && lower.Length > 3
                ? lower.TrimEnd('s') : lower;

            var map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
            {
                ["wall"] = BuiltInCategory.OST_Walls,
                ["door"] = BuiltInCategory.OST_Doors,
                ["window"] = BuiltInCategory.OST_Windows,
                ["floor"] = BuiltInCategory.OST_Floors,
                ["ceiling"] = BuiltInCategory.OST_Ceilings,
                ["roof"] = BuiltInCategory.OST_Roofs,
                ["room"] = BuiltInCategory.OST_Rooms,
                ["column"] = BuiltInCategory.OST_StructuralColumns,
                ["structural column"] = BuiltInCategory.OST_StructuralColumns,
                ["beam"] = BuiltInCategory.OST_StructuralFraming,
                ["structural framing"] = BuiltInCategory.OST_StructuralFraming,
                ["foundation"] = BuiltInCategory.OST_StructuralFoundation,
                ["stair"] = BuiltInCategory.OST_Stairs,
                ["staircase"] = BuiltInCategory.OST_Stairs,
                ["railing"] = BuiltInCategory.OST_StairsRailing,
                ["ramp"] = BuiltInCategory.OST_Ramps,
                ["curtain wall"] = BuiltInCategory.OST_CurtainWallPanels,
                ["furniture"] = BuiltInCategory.OST_Furniture,
                ["plumbing"] = BuiltInCategory.OST_PlumbingFixtures,
                ["plumbing fixture"] = BuiltInCategory.OST_PlumbingFixtures,
                ["mechanical equipment"] = BuiltInCategory.OST_MechanicalEquipment,
                ["lighting"] = BuiltInCategory.OST_LightingFixtures,
                ["light"] = BuiltInCategory.OST_LightingFixtures,
                ["electrical fixture"] = BuiltInCategory.OST_ElectricalFixtures,
                ["electrical equipment"] = BuiltInCategory.OST_ElectricalEquipment,
                ["pipe"] = BuiltInCategory.OST_PipeCurves,
                ["duct"] = BuiltInCategory.OST_DuctCurves,
                ["conduit"] = BuiltInCategory.OST_Conduit,
                ["cable tray"] = BuiltInCategory.OST_CableTray,
                ["fire alarm"] = BuiltInCategory.OST_FireAlarmDevices,
                ["sprinkler"] = BuiltInCategory.OST_Sprinklers
            };

            if (map.TryGetValue(lower, out var cat)) return cat;
            if (map.TryGetValue(singular, out cat)) return cat;

            return BuiltInCategory.INVALID;
        }

        #endregion

        #region Helpers

        private string GetParameterValueAsString(Parameter param)
        {
            try
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.AsString();
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                    case StorageType.Double:
                        return param.AsDouble().ToString("F2");
                    case StorageType.ElementId:
                        return param.AsElementId().IntegerValue.ToString();
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    #region Selector Data Types

    /// <summary>
    /// Criteria for selecting elements. Supports 10 selector types
    /// plus Combined (AND/OR) composition.
    /// </summary>
    public class SelectorCriteria
    {
        public SelectorType Type { get; set; }

        // ByElementId
        public ElementId ElementId { get; set; }

        // ByCategory
        public BuiltInCategory? Category { get; set; }

        // ByRoomName, ByType
        public string NamePattern { get; set; }

        // ByLevel
        public string LevelName { get; set; }

        // ByParameter
        public string ParameterName { get; set; }
        public object ParameterValue { get; set; }

        // BySelection
        public List<ElementId> PreSelectedIds { get; set; }

        // BySpatialFilter
        public XYZ Center { get; set; }
        public double Radius { get; set; } // mm

        // ByPhase
        public string PhaseName { get; set; }

        // ByWorkset
        public string WorksetName { get; set; }

        // ByHost
        public ElementId HostElementId { get; set; }

        // Combined
        public CombineMode CombineMode { get; set; } = CombineMode.AND;
        public List<SelectorCriteria> SubSelectors { get; set; }

        #region Factory Methods

        public static SelectorCriteria ById(ElementId id) =>
            new SelectorCriteria { Type = SelectorType.ByElementId, ElementId = id };

        public static SelectorCriteria ByCategory(BuiltInCategory cat) =>
            new SelectorCriteria { Type = SelectorType.ByCategory, Category = cat };

        public static SelectorCriteria ByRoomName(string pattern) =>
            new SelectorCriteria { Type = SelectorType.ByRoomName, NamePattern = pattern };

        public static SelectorCriteria ByLevel(string levelName) =>
            new SelectorCriteria { Type = SelectorType.ByLevel, LevelName = levelName };

        public static SelectorCriteria ByTypeName(string pattern) =>
            new SelectorCriteria { Type = SelectorType.ByType, NamePattern = pattern };

        public static SelectorCriteria ByParam(string paramName, object value = null) =>
            new SelectorCriteria
            {
                Type = SelectorType.ByParameter,
                ParameterName = paramName,
                ParameterValue = value
            };

        public static SelectorCriteria ByCurrentSelection(List<ElementId> ids) =>
            new SelectorCriteria { Type = SelectorType.BySelection, PreSelectedIds = ids };

        public static SelectorCriteria BySpatial(XYZ center, double radiusMm) =>
            new SelectorCriteria
            {
                Type = SelectorType.BySpatialFilter,
                Center = center,
                Radius = radiusMm
            };

        public static SelectorCriteria ByPhaseName(string phase) =>
            new SelectorCriteria { Type = SelectorType.ByPhase, PhaseName = phase };

        public static SelectorCriteria ByWorksetName(string workset) =>
            new SelectorCriteria { Type = SelectorType.ByWorkset, WorksetName = workset };

        public static SelectorCriteria ByHostElement(ElementId hostId) =>
            new SelectorCriteria { Type = SelectorType.ByHost, HostElementId = hostId };

        public static SelectorCriteria And(params SelectorCriteria[] selectors) =>
            new SelectorCriteria
            {
                Type = SelectorType.Combined,
                CombineMode = CombineMode.AND,
                SubSelectors = selectors.ToList()
            };

        public static SelectorCriteria Or(params SelectorCriteria[] selectors) =>
            new SelectorCriteria
            {
                Type = SelectorType.Combined,
                CombineMode = CombineMode.OR,
                SubSelectors = selectors.ToList()
            };

        #endregion
    }

    public enum SelectorType
    {
        ByElementId,
        ByCategory,
        ByRoomName,
        ByLevel,
        ByType,
        ByParameter,
        BySelection,
        BySpatialFilter,
        ByPhase,
        ByWorkset,
        ByHost,
        Combined
    }

    public enum CombineMode
    {
        AND,
        OR
    }

    #endregion
}
