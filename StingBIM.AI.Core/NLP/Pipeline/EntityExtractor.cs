// StingBIM.AI.NLP.Pipeline.EntityExtractor
// Extracts entities from natural language input
// Master Proposal Reference: Part 1.1 Language Understanding - Entity Extractor

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.NLP.Pipeline
{
    /// <summary>
    /// Extracts named entities from user commands.
    /// Recognizes dimensions, room types, materials, directions, etc.
    /// </summary>
    public class EntityExtractor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private Dictionary<string, EntityType> _entityDictionary;
        private List<EntityPattern> _patterns;
        private Dictionary<string, List<string>> _synonyms;

        // Built-in patterns for common entity types
        private static readonly Dictionary<string, Regex> BuiltInPatterns = new Dictionary<string, Regex>
        {
            // Dimensions: "4m", "3.5 meters", "4000mm", "10 feet", "4'-6\""
            { "DIMENSION", new Regex(@"(\d+(?:\.\d+)?)\s*(m|meters?|mm|millimeters?|cm|centimeters?|ft|feet|foot|'|in|inches?|"")", RegexOptions.IgnoreCase | RegexOptions.Compiled) },

            // Numbers: "3", "4.5", "twelve"
            { "NUMBER", new Regex(@"\b(\d+(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled) },

            // Directions: "left", "right", "north", "up"
            { "DIRECTION", new Regex(@"\b(left|right|up|down|north|south|east|west|above|below|beside|next to|adjacent|opposite)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled) },

            // Positions: "here", "there", "at the cursor"
            { "POSITION", new Regex(@"\b(here|there|at the cursor|at cursor|selected|current position|this location)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled) },

            // Colors: "red", "blue", "#FF0000", "RGB(255,0,0)"
            { "COLOR", new Regex(@"\b(red|blue|green|yellow|white|black|gray|grey|brown|orange|purple|pink|#[0-9A-Fa-f]{6}|rgb\(\d+,\s*\d+,\s*\d+\))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled) }
        };

        public EntityExtractor()
        {
            _entityDictionary = new Dictionary<string, EntityType>(StringComparer.OrdinalIgnoreCase);
            _patterns = new List<EntityPattern>();
            _synonyms = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads entity definitions from file.
        /// </summary>
        public async Task LoadAsync(string dictionaryPath, string synonymsPath = null)
        {
            Logger.Info("Loading entity extractor...");

            try
            {
                // Load entity dictionary
                if (File.Exists(dictionaryPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(dictionaryPath));
                    var entities = JsonConvert.DeserializeObject<List<EntityDefinition>>(json);

                    foreach (var entity in entities)
                    {
                        _entityDictionary[entity.Value] = entity.Type;

                        // Add pattern if provided
                        if (!string.IsNullOrEmpty(entity.Pattern))
                        {
                            _patterns.Add(new EntityPattern
                            {
                                Type = entity.Type,
                                Pattern = new Regex(entity.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled),
                                Normalizer = entity.Normalizer
                            });
                        }
                    }
                }

                // Load synonyms
                if (!string.IsNullOrEmpty(synonymsPath) && File.Exists(synonymsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(synonymsPath));
                    _synonyms = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                        ?? new Dictionary<string, List<string>>();
                }

                Logger.Info($"Entity extractor loaded: {_entityDictionary.Count} entities, {_synonyms.Count} synonym groups");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load entity extractor");
                throw;
            }
        }

        /// <summary>
        /// Extracts all entities from text.
        /// </summary>
        public List<ExtractedEntity> Extract(string text)
        {
            var entities = new List<ExtractedEntity>();

            // Apply built-in patterns first
            foreach (var (typeName, pattern) in BuiltInPatterns)
            {
                var matches = pattern.Matches(text);
                foreach (Match match in matches)
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = Enum.Parse<EntityType>(typeName),
                        Value = match.Value,
                        NormalizedValue = NormalizeBuiltInEntity(typeName, match.Value),
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Confidence = 0.95f
                    });
                }
            }

            // Apply custom patterns
            foreach (var entityPattern in _patterns)
            {
                var matches = entityPattern.Pattern.Matches(text);
                foreach (Match match in matches)
                {
                    var normalizedValue = entityPattern.Normalizer != null
                        ? ApplyNormalizer(match.Value, entityPattern.Normalizer)
                        : match.Value;

                    entities.Add(new ExtractedEntity
                    {
                        Type = entityPattern.Type,
                        Value = match.Value,
                        NormalizedValue = normalizedValue,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Confidence = 0.9f
                    });
                }
            }

            // Dictionary lookup for known entities
            var words = Tokenize(text);
            foreach (var (word, start, end) in words)
            {
                // Check dictionary
                if (_entityDictionary.TryGetValue(word, out var entityType))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = entityType,
                        Value = word,
                        NormalizedValue = word,
                        StartIndex = start,
                        EndIndex = end,
                        Confidence = 0.85f
                    });
                }

                // Check synonyms
                var normalizedWord = ResolveSynonym(word);
                if (normalizedWord != word && _entityDictionary.TryGetValue(normalizedWord, out entityType))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = entityType,
                        Value = word,
                        NormalizedValue = normalizedWord,
                        StartIndex = start,
                        EndIndex = end,
                        Confidence = 0.8f
                    });
                }
            }

            // Remove duplicates and overlapping entities
            entities = ResolveOverlaps(entities);

            Logger.Debug($"Extracted {entities.Count} entities from: {text}");
            return entities;
        }

        /// <summary>
        /// Extracts entities of a specific type.
        /// </summary>
        public List<ExtractedEntity> Extract(string text, EntityType type)
        {
            return Extract(text).Where(e => e.Type == type).ToList();
        }

        /// <summary>
        /// Resolves a word to its canonical form using synonyms.
        /// </summary>
        public string ResolveSynonym(string word)
        {
            foreach (var (canonical, synonymList) in _synonyms)
            {
                if (synonymList.Contains(word, StringComparer.OrdinalIgnoreCase))
                {
                    return canonical;
                }
            }
            return word;
        }

        private string NormalizeBuiltInEntity(string typeName, string value)
        {
            switch (typeName)
            {
                case "DIMENSION":
                    return NormalizeDimension(value);
                case "NUMBER":
                    return NormalizeNumber(value);
                default:
                    return value.ToLowerInvariant();
            }
        }

        private string NormalizeDimension(string value)
        {
            // Convert all dimensions to millimeters for consistency
            var match = Regex.Match(value, @"(\d+(?:\.\d+)?)\s*(m|meters?|mm|millimeters?|cm|centimeters?|ft|feet|foot|'|in|inches?|"")", RegexOptions.IgnoreCase);
            if (!match.Success) return value;

            var number = double.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLowerInvariant();

            double mm = unit switch
            {
                "m" or "meter" or "meters" => number * 1000,
                "cm" or "centimeter" or "centimeters" => number * 10,
                "mm" or "millimeter" or "millimeters" => number,
                "ft" or "feet" or "foot" or "'" => number * 304.8,
                "in" or "inch" or "inches" or "\"" => number * 25.4,
                _ => number
            };

            return $"{mm}mm";
        }

        private string NormalizeNumber(string value)
        {
            var wordNumbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "one", "1" }, { "two", "2" }, { "three", "3" }, { "four", "4" }, { "five", "5" },
                { "six", "6" }, { "seven", "7" }, { "eight", "8" }, { "nine", "9" }, { "ten", "10" },
                { "eleven", "11" }, { "twelve", "12" }
            };

            return wordNumbers.TryGetValue(value, out var numStr) ? numStr : value;
        }

        private string ApplyNormalizer(string value, string normalizer)
        {
            if (string.IsNullOrEmpty(normalizer))
                return value;

            switch (normalizer.ToLowerInvariant())
            {
                case "lowercase":
                    return value.ToLowerInvariant();

                case "uppercase":
                    return value.ToUpperInvariant();

                case "titlecase":
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

                case "trim":
                    return value.Trim();

                case "number":
                    return NormalizeNumber(value);

                case "dimension":
                    return NormalizeDimensionText(value);

                case "ordinal":
                    return NormalizeOrdinal(value);

                case "elementtype":
                    return NormalizeElementType(value);

                case "direction":
                    return NormalizeDirection(value);

                case "level":
                    return NormalizeLevel(value);

                case "material":
                    return NormalizeMaterial(value);

                default:
                    Logger.Warn($"Unknown normalizer: {normalizer}");
                    return value;
            }
        }

        private string NormalizeDimensionText(string value)
        {
            // Normalize dimension expressions (text-based normalization)
            var normalized = value.ToLowerInvariant().Trim();

            // Convert word-based measurements
            var dimensionWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "half", "0.5" }, { "quarter", "0.25" }, { "third", "0.33" },
                { "double", "2" }, { "triple", "3" }
            };

            foreach (var kvp in dimensionWords)
            {
                if (normalized.Contains(kvp.Key))
                {
                    normalized = normalized.Replace(kvp.Key, kvp.Value);
                }
            }

            // Standardize unit abbreviations
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(meter|meters|metre|metres)\b", "m");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(centimeter|centimeters|centimetre|centimetres|cm)\b", "cm");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(millimeter|millimeters|millimetre|millimetres|mm)\b", "mm");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(foot|feet|ft)\b", "ft");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(inch|inches|in)\b", "in");

            return normalized;
        }

        private string NormalizeOrdinal(string value)
        {
            var ordinalMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "first", "1" }, { "second", "2" }, { "third", "3" }, { "fourth", "4" },
                { "fifth", "5" }, { "sixth", "6" }, { "seventh", "7" }, { "eighth", "8" },
                { "ninth", "9" }, { "tenth", "10" }, { "1st", "1" }, { "2nd", "2" },
                { "3rd", "3" }, { "4th", "4" }, { "5th", "5" }, { "6th", "6" },
                { "7th", "7" }, { "8th", "8" }, { "9th", "9" }, { "10th", "10" }
            };

            return ordinalMap.TryGetValue(value, out var normalized) ? normalized : value;
        }

        private string NormalizeElementType(string value)
        {
            var elementTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Walls
                { "walls", "wall" }, { "partition", "wall" }, { "partitions", "wall" },
                { "divider", "wall" }, { "barrier", "wall" },
                // Doors
                { "doors", "door" }, { "entrance", "door" }, { "entry", "door" },
                { "doorway", "door" }, { "gateway", "door" },
                // Windows
                { "windows", "window" }, { "glazing", "window" }, { "opening", "window" },
                // Floors
                { "floors", "floor" }, { "slab", "floor" }, { "deck", "floor" },
                // Ceilings
                { "ceilings", "ceiling" }, { "soffit", "ceiling" },
                // Columns
                { "columns", "column" }, { "pillar", "column" }, { "pillars", "column" },
                { "post", "column" }, { "posts", "column" }, { "pier", "column" },
                // Beams
                { "beams", "beam" }, { "girder", "beam" }, { "girders", "beam" },
                { "lintel", "beam" }, { "joist", "beam" }, { "joists", "beam" },
                // Rooms
                { "rooms", "room" }, { "space", "room" }, { "spaces", "room" },
                { "area", "room" }, { "zone", "room" }
            };

            return elementTypeMap.TryGetValue(value, out var normalized) ? normalized : value.ToLowerInvariant();
        }

        private string NormalizeDirection(string value)
        {
            var directionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "n", "north" }, { "s", "south" }, { "e", "east" }, { "w", "west" },
                { "ne", "northeast" }, { "nw", "northwest" }, { "se", "southeast" }, { "sw", "southwest" },
                { "l", "left" }, { "r", "right" }, { "u", "up" }, { "d", "down" },
                { "upward", "up" }, { "upwards", "up" }, { "downward", "down" }, { "downwards", "down" },
                { "leftward", "left" }, { "rightward", "right" }, { "above", "up" }, { "below", "down" },
                { "forward", "front" }, { "backward", "back" }, { "forwards", "front" }, { "backwards", "back" }
            };

            return directionMap.TryGetValue(value, out var normalized) ? normalized : value.ToLowerInvariant();
        }

        private string NormalizeLevel(string value)
        {
            var levelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ground", "0" }, { "ground floor", "0" }, { "ground level", "0" },
                { "basement", "-1" }, { "lower ground", "-1" },
                { "first floor", "1" }, { "second floor", "2" }, { "third floor", "3" },
                { "roof", "roof" }, { "rooftop", "roof" }, { "top", "roof" },
                { "attic", "attic" }, { "loft", "attic" }
            };

            return levelMap.TryGetValue(value, out var normalized) ? normalized : value;
        }

        private string NormalizeMaterial(string value)
        {
            var materialMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Concrete variations
                { "conc", "concrete" }, { "reinforced concrete", "concrete" }, { "rc", "concrete" },
                // Steel variations
                { "stl", "steel" }, { "structural steel", "steel" }, { "mild steel", "steel" },
                // Wood variations
                { "timber", "wood" }, { "lumber", "wood" }, { "wooden", "wood" },
                // Brick variations
                { "bricks", "brick" }, { "masonry", "brick" }, { "blockwork", "brick" },
                // Glass variations
                { "glazed", "glass" }, { "glazing", "glass" },
                // Gypsum variations
                { "drywall", "gypsum" }, { "plasterboard", "gypsum" }, { "gyp", "gypsum" }
            };

            return materialMap.TryGetValue(value, out var normalized) ? normalized : value.ToLowerInvariant();
        }

        private IEnumerable<(string Word, int Start, int End)> Tokenize(string text)
        {
            var pattern = new Regex(@"\b\w+\b");
            var matches = pattern.Matches(text);

            foreach (Match match in matches)
            {
                yield return (match.Value, match.Index, match.Index + match.Length);
            }
        }

        private List<ExtractedEntity> ResolveOverlaps(List<ExtractedEntity> entities)
        {
            // Sort by start index, then by confidence (descending)
            var sorted = entities.OrderBy(e => e.StartIndex).ThenByDescending(e => e.Confidence).ToList();
            var result = new List<ExtractedEntity>();

            foreach (var entity in sorted)
            {
                // Check if this entity overlaps with any already added
                var overlaps = result.Any(e =>
                    (entity.StartIndex >= e.StartIndex && entity.StartIndex < e.EndIndex) ||
                    (entity.EndIndex > e.StartIndex && entity.EndIndex <= e.EndIndex));

                if (!overlaps)
                {
                    result.Add(entity);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Represents an extracted entity.
    /// </summary>
    public class ExtractedEntity
    {
        public EntityType Type { get; set; }
        public string Value { get; set; }
        public string NormalizedValue { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Types of entities that can be extracted.
    /// </summary>
    public enum EntityType
    {
        // Measurements
        DIMENSION,
        NUMBER,
        ANGLE,
        AREA,
        VOLUME,

        // Spatial
        DIRECTION,
        POSITION,
        LEVEL,

        // Building elements
        ROOM_TYPE,
        ELEMENT_TYPE,
        MATERIAL,
        WALL_TYPE,
        FLOOR_TYPE,
        DOOR_TYPE,
        WINDOW_TYPE,

        // Properties
        COLOR,
        PARAMETER_NAME,
        PARAMETER_VALUE,

        // References
        ELEMENT_REFERENCE,
        VIEW_NAME,
        FAMILY_NAME,

        // Other
        PROJECT_NAME,
        USER_NAME,
        DATE,
        TIME
    }

    /// <summary>
    /// Entity definition from dictionary.
    /// </summary>
    public class EntityDefinition
    {
        public string Value { get; set; }
        public EntityType Type { get; set; }
        public string Pattern { get; set; }
        public string Normalizer { get; set; }
    }

    /// <summary>
    /// Pattern for entity extraction.
    /// </summary>
    internal class EntityPattern
    {
        public EntityType Type { get; set; }
        public Regex Pattern { get; set; }
        public string Normalizer { get; set; }
    }
}
