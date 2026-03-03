// StingBIM.AI.Creation.Pipeline.FamilyResolver
// Resolves family types from the live Revit Document — never hardcoded names
// v4 Prompt Reference: Section A.0.2 Step 2 — Family Resolution

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Resolves family types from the live Revit Document based on user keywords.
    /// Priority: exact match → keyword match → closest dimension → first available.
    /// Never hardcodes family names.
    /// </summary>
    public class FamilyResolver
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly object _cacheLock = new object();
        private Dictionary<BuiltInCategory, List<ElementType>> _typeCache;

        public FamilyResolver(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// The Revit Document — exposed for callers that need to pass it to other creators.
        /// </summary>
        public Document Document => _document;

        /// <summary>
        /// Resolves a WallType from keyword and optional thickness.
        /// </summary>
        public FamilyResolveResult ResolveWallType(string keyword = null, double? thicknessMm = null,
            string function = null)
        {
            var wallTypes = GetElementTypes<WallType>(BuiltInCategory.OST_Walls);
            if (wallTypes.Count == 0)
                return FamilyResolveResult.NotFound("No wall types available in the document");

            // Step 1: Exact name match
            if (!string.IsNullOrEmpty(keyword))
            {
                var exact = wallTypes.FirstOrDefault(t =>
                    t.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return FamilyResolveResult.Resolved(exact, MatchConfidence.Exact);
            }

            // Step 2: All keywords match
            if (!string.IsNullOrEmpty(keyword))
            {
                var keywords = keyword.ToLowerInvariant().Split(new[] { ' ', '-', '_' },
                    StringSplitOptions.RemoveEmptyEntries);
                var keywordMatches = wallTypes.Where(t =>
                    keywords.All(k => t.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (keywordMatches.Count == 1)
                    return FamilyResolveResult.Resolved(keywordMatches[0], MatchConfidence.KeywordMatch);
                if (keywordMatches.Count > 1 && thicknessMm.HasValue)
                {
                    var best = FindClosestByThickness(keywordMatches, thicknessMm.Value);
                    if (best != null)
                        return FamilyResolveResult.Resolved(best, MatchConfidence.KeywordMatch,
                            $"Using {best.Name} — closest match to {thicknessMm}mm");
                }
                if (keywordMatches.Count > 0)
                    return FamilyResolveResult.Resolved(keywordMatches[0], MatchConfidence.KeywordMatch);
            }

            // Step 3: Function match (external, partition, fire-rated, structural)
            if (!string.IsNullOrEmpty(function))
            {
                var funcMatches = wallTypes.Where(t =>
                    t.Name.IndexOf(function, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (funcMatches.Count > 0)
                {
                    var best = thicknessMm.HasValue
                        ? FindClosestByThickness(funcMatches, thicknessMm.Value) ?? funcMatches[0]
                        : funcMatches[0];
                    return FamilyResolveResult.Resolved(best, MatchConfidence.FunctionMatch);
                }
            }

            // Step 4: Closest thickness match
            if (thicknessMm.HasValue)
            {
                var best = FindClosestByThickness(wallTypes, thicknessMm.Value);
                if (best != null)
                    return FamilyResolveResult.Resolved(best, MatchConfidence.DimensionMatch,
                        $"No keyword match. Using {best.Name} — closest thickness to {thicknessMm}mm");
            }

            // Step 5: First available (fallback)
            var fallback = wallTypes[0];
            return FamilyResolveResult.Resolved(fallback, MatchConfidence.Fallback,
                $"No matching wall type found. Using {fallback.Name}");
        }

        /// <summary>
        /// Resolves a FloorType from keyword.
        /// </summary>
        public FamilyResolveResult ResolveFloorType(string keyword = null, string roomType = null)
        {
            var floorTypes = GetElementTypes<FloorType>(BuiltInCategory.OST_Floors);
            if (floorTypes.Count == 0)
                return FamilyResolveResult.NotFound("No floor types available in the document");

            return ResolveByKeyword(floorTypes, keyword, "floor");
        }

        /// <summary>
        /// Resolves a RoofType from keyword.
        /// </summary>
        public FamilyResolveResult ResolveRoofType(string keyword = null)
        {
            var roofTypes = GetElementTypes<RoofType>(BuiltInCategory.OST_Roofs);
            if (roofTypes.Count == 0)
                return FamilyResolveResult.NotFound("No roof types available in the document");

            return ResolveByKeyword(roofTypes, keyword, "roof");
        }

        /// <summary>
        /// Resolves a FamilySymbol (door, window, column, beam, etc.) by category and keyword.
        /// </summary>
        public FamilyResolveResult ResolveFamilySymbol(BuiltInCategory category, string keyword = null,
            double? widthMm = null, double? heightMm = null)
        {
            var symbols = new FilteredElementCollector(_document)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            if (symbols.Count == 0)
                return FamilyResolveResult.NotFound(
                    $"No family symbols for category {category} available in the document");

            // Exact name match
            if (!string.IsNullOrEmpty(keyword))
            {
                var exact = symbols.FirstOrDefault(s =>
                    s.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return FamilyResolveResult.Resolved(exact, MatchConfidence.Exact);

                // Keyword contains match
                var keywords = keyword.ToLowerInvariant().Split(new[] { ' ', '-', '_' },
                    StringSplitOptions.RemoveEmptyEntries);
                var matches = symbols.Where(s =>
                    keywords.Any(k => s.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      s.FamilyName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                if (matches.Count > 0)
                    return FamilyResolveResult.Resolved(matches[0], MatchConfidence.KeywordMatch);
            }

            // Size match for doors/windows
            if (widthMm.HasValue && (category == BuiltInCategory.OST_Doors ||
                                      category == BuiltInCategory.OST_Windows))
            {
                var bestMatch = FindClosestBySize(symbols, widthMm.Value, heightMm);
                if (bestMatch != null)
                    return FamilyResolveResult.Resolved(bestMatch, MatchConfidence.DimensionMatch);
            }

            // Fallback
            return FamilyResolveResult.Resolved(symbols[0], MatchConfidence.Fallback,
                $"No matching family found. Using {symbols[0].FamilyName}: {symbols[0].Name}");
        }

        /// <summary>
        /// Resolves a structural column family symbol.
        /// </summary>
        public FamilyResolveResult ResolveColumnType(string keyword = null, double? sizeMm = null)
        {
            return ResolveFamilySymbol(BuiltInCategory.OST_StructuralColumns, keyword, sizeMm, sizeMm);
        }

        /// <summary>
        /// Resolves a structural framing (beam) family symbol.
        /// </summary>
        public FamilyResolveResult ResolveBeamType(string keyword = null)
        {
            return ResolveFamilySymbol(BuiltInCategory.OST_StructuralFraming, keyword);
        }

        /// <summary>
        /// Resolves a Level by name or elevation.
        /// </summary>
        public Level ResolveLevel(string levelName = null, double? elevationM = null)
        {
            var levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count == 0) return null;

            if (!string.IsNullOrEmpty(levelName))
            {
                // Exact match
                var exact = levels.FirstOrDefault(l =>
                    l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                // Partial match: "level 1", "ground", "first floor"
                var partial = levels.FirstOrDefault(l =>
                    l.Name.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (partial != null) return partial;

                // Number extraction: "1" → "Level 1", "2" → "Level 2"
                if (int.TryParse(levelName, out var num) && num > 0 && num <= levels.Count)
                    return levels[num - 1];
            }

            if (elevationM.HasValue)
            {
                var elevFt = elevationM.Value / 0.3048;
                return levels.OrderBy(l => Math.Abs(l.Elevation - elevFt)).First();
            }

            // Default: lowest level
            return levels[0];
        }

        /// <summary>
        /// Gets the level above a given level, if one exists.
        /// </summary>
        public Level GetLevelAbove(Level currentLevel)
        {
            var levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            var idx = levels.FindIndex(l => l.Id == currentLevel.Id);
            if (idx >= 0 && idx < levels.Count - 1)
                return levels[idx + 1];
            return null;
        }

        /// <summary>
        /// Resolves an MEP fixture family symbol by category hint and optional keyword.
        /// Categories: light → OST_LightingFixtures, outlet → OST_ElectricalFixtures,
        /// switch → OST_LightingDevices, panel/DB → OST_ElectricalEquipment,
        /// generator → OST_MechanicalEquipment.
        /// Returns null if no matching family is loaded.
        /// </summary>
        public FamilySymbol ResolveMEPFixture(string categoryHint, string keyword = null)
        {
            var category = categoryHint?.ToLowerInvariant() switch
            {
                "light" or "lighting" => BuiltInCategory.OST_LightingFixtures,
                "outlet" or "power" or "socket" => BuiltInCategory.OST_ElectricalFixtures,
                "switch" => BuiltInCategory.OST_LightingDevices,
                "distribution board" or "panel" or "db" => BuiltInCategory.OST_ElectricalEquipment,
                "generator" or "genset" => BuiltInCategory.OST_MechanicalEquipment,
                "sprinkler" => BuiltInCategory.OST_Sprinklers,
                "detector" or "smoke" => BuiltInCategory.OST_FireAlarmDevices,
                _ => BuiltInCategory.OST_ElectricalFixtures,
            };

            var symbols = new FilteredElementCollector(_document)
                .OfCategory(category)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            if (symbols.Count == 0)
            {
                Logger.Warn($"No MEP fixture families for category {category} in document");
                return null;
            }

            // Try keyword match
            if (!string.IsNullOrEmpty(keyword))
            {
                var kw = keyword.ToLowerInvariant();
                var match = symbols.FirstOrDefault(s =>
                    s.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.FamilyName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    if (!match.IsActive) match.Activate();
                    return match;
                }
            }

            // Fallback: first available
            var fallback = symbols[0];
            if (!fallback.IsActive) fallback.Activate();
            return fallback;
        }

        #region Private Methods

        private List<T> GetElementTypes<T>(BuiltInCategory category) where T : ElementType
        {
            return new FilteredElementCollector(_document)
                .OfCategory(category)
                .OfClass(typeof(T))
                .Cast<T>()
                .ToList();
        }

        private FamilyResolveResult ResolveByKeyword<T>(List<T> types, string keyword, string typeName)
            where T : ElementType
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                var exact = types.FirstOrDefault(t =>
                    t.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return FamilyResolveResult.Resolved(exact, MatchConfidence.Exact);

                var keywords = keyword.ToLowerInvariant().Split(new[] { ' ', '-', '_' },
                    StringSplitOptions.RemoveEmptyEntries);
                var matches = types.Where(t =>
                    keywords.Any(k => t.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (matches.Count > 0)
                    return FamilyResolveResult.Resolved(matches[0], MatchConfidence.KeywordMatch);
            }

            return FamilyResolveResult.Resolved(types[0], MatchConfidence.Fallback,
                $"No matching {typeName} type found. Using {types[0].Name}");
        }

        private WallType FindClosestByThickness(List<WallType> types, double targetThicknessMm)
        {
            var targetFt = targetThicknessMm / 304.8;
            return types.OrderBy(t =>
            {
                try
                {
                    var width = t.Width; // Revit internal: decimal feet
                    return Math.Abs(width - targetFt);
                }
                catch
                {
                    return double.MaxValue;
                }
            }).FirstOrDefault();
        }

        private FamilySymbol FindClosestBySize(List<FamilySymbol> symbols, double targetWidthMm,
            double? targetHeightMm)
        {
            var targetWidthFt = targetWidthMm / 304.8;
            return symbols.OrderBy(s =>
            {
                try
                {
                    var widthParam = s.LookupParameter("Width") ?? s.LookupParameter("Rough Width");
                    if (widthParam != null)
                    {
                        var width = widthParam.AsDouble(); // decimal feet
                        return Math.Abs(width - targetWidthFt);
                    }
                    return double.MaxValue;
                }
                catch
                {
                    return double.MaxValue;
                }
            }).FirstOrDefault();
        }

        #endregion
    }

    #region Result Types

    /// <summary>
    /// Result of a family resolution attempt.
    /// </summary>
    public class FamilyResolveResult
    {
        public bool Success { get; set; }
        public Element ResolvedType { get; set; }
        public ElementId TypeId => ResolvedType?.Id ?? ElementId.InvalidElementId;
        public string TypeName => ResolvedType is ElementType et ? et.Name : "";
        public MatchConfidence Confidence { get; set; }
        public string Message { get; set; }

        public static FamilyResolveResult Resolved(Element type, MatchConfidence confidence,
            string message = null)
        {
            return new FamilyResolveResult
            {
                Success = true,
                ResolvedType = type,
                Confidence = confidence,
                Message = message
            };
        }

        public static FamilyResolveResult NotFound(string message)
        {
            return new FamilyResolveResult
            {
                Success = false,
                Confidence = MatchConfidence.None,
                Message = message
            };
        }
    }

    public enum MatchConfidence
    {
        None = 0,
        Fallback = 1,
        DimensionMatch = 2,
        FunctionMatch = 3,
        KeywordMatch = 4,
        Exact = 5
    }

    #endregion
}
