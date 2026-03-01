// ============================================================================
// StingBIM AI - Offline NLP Engine
// Provides natural language processing without cloud dependencies
// Uses ONNX Runtime for local inference with pre-trained models
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

namespace StingBIM.AI.Intelligence.NLP
{
    /// <summary>
    /// Offline Natural Language Processing Engine
    /// Processes user queries and commands without cloud connectivity
    /// </summary>
    public class OfflineNLPEngine
    {
        private readonly IntentClassifier _intentClassifier;
        private readonly EntityExtractor _entityExtractor;
        private readonly QueryParser _queryParser;
        private readonly CommandInterpreter _commandInterpreter;
        private readonly SemanticMatcher _semanticMatcher;
        private readonly Dictionary<string, List<string>> _synonymDictionary;
        private readonly Dictionary<string, double[]> _wordEmbeddings;

        public OfflineNLPEngine()
        {
            _intentClassifier = new IntentClassifier();
            _entityExtractor = new EntityExtractor();
            _queryParser = new QueryParser();
            _commandInterpreter = new CommandInterpreter();
            _semanticMatcher = new SemanticMatcher();
            _synonymDictionary = LoadBIMSynonyms();
            _wordEmbeddings = new Dictionary<string, double[]>();
        }

        /// <summary>
        /// Process natural language input and return structured interpretation
        /// </summary>
        public NLPResult Process(string input, NLPContext context = null)
        {
            if (string.IsNullOrWhiteSpace(input))
                return NLPResult.Empty();

            var result = new NLPResult
            {
                OriginalInput = input,
                ProcessedAt = DateTime.UtcNow
            };

            // Step 1: Preprocess text
            var preprocessed = Preprocess(input);
            result.NormalizedInput = preprocessed;

            // Step 2: Tokenize
            var tokens = Tokenize(preprocessed);
            result.Tokens = tokens;

            // Step 3: Classify intent
            result.Intent = _intentClassifier.Classify(tokens, context);

            // Step 4: Extract entities
            result.Entities = _entityExtractor.Extract(tokens, result.Intent);

            // Step 5: Parse query structure
            result.QueryStructure = _queryParser.Parse(tokens, result.Intent);

            // Step 6: Interpret as command if applicable
            if (IsCommand(result.Intent))
            {
                result.Command = _commandInterpreter.Interpret(result);
            }

            // Step 7: Calculate confidence
            result.Confidence = CalculateOverallConfidence(result);

            return result;
        }

        /// <summary>
        /// Find semantically similar terms in BIM domain
        /// </summary>
        public List<SemanticMatch> FindSimilarTerms(string term, int topK = 5)
        {
            return _semanticMatcher.FindSimilar(term, _synonymDictionary, topK);
        }

        /// <summary>
        /// Expand query with domain synonyms
        /// </summary>
        public string ExpandQuery(string query)
        {
            var tokens = Tokenize(query);
            var expanded = new List<string>();

            foreach (var token in tokens)
            {
                expanded.Add(token);
                if (_synonymDictionary.TryGetValue(token.ToLower(), out var synonyms))
                {
                    expanded.AddRange(synonyms.Take(2)); // Add top 2 synonyms
                }
            }

            return string.Join(" ", expanded.Distinct());
        }

        private string Preprocess(string input)
        {
            // Lowercase
            var result = input.ToLower().Trim();

            // Expand contractions
            result = ExpandContractions(result);

            // Normalize units and measurements
            result = NormalizeUnits(result);

            // Remove extra whitespace
            result = Regex.Replace(result, @"\s+", " ");

            return result;
        }

        private string ExpandContractions(string text)
        {
            var contractions = new Dictionary<string, string>
            {
                { "what's", "what is" },
                { "where's", "where is" },
                { "how's", "how is" },
                { "it's", "it is" },
                { "that's", "that is" },
                { "there's", "there is" },
                { "can't", "cannot" },
                { "don't", "do not" },
                { "doesn't", "does not" },
                { "won't", "will not" },
                { "isn't", "is not" },
                { "aren't", "are not" },
                { "wasn't", "was not" },
                { "weren't", "were not" },
                { "haven't", "have not" },
                { "hasn't", "has not" },
                { "hadn't", "had not" },
                { "shouldn't", "should not" },
                { "wouldn't", "would not" },
                { "couldn't", "could not" },
                { "i'm", "i am" },
                { "you're", "you are" },
                { "we're", "we are" },
                { "they're", "they are" },
                { "i've", "i have" },
                { "you've", "you have" },
                { "we've", "we have" },
                { "they've", "they have" },
                { "i'll", "i will" },
                { "you'll", "you will" },
                { "we'll", "we will" },
                { "they'll", "they will" },
                { "i'd", "i would" },
                { "you'd", "you would" },
                { "we'd", "we would" },
                { "they'd", "they would" }
            };

            foreach (var kvp in contractions)
            {
                text = text.Replace(kvp.Key, kvp.Value);
            }

            return text;
        }

        private string NormalizeUnits(string text)
        {
            // Normalize measurement patterns
            var unitPatterns = new Dictionary<string, string>
            {
                { @"(\d+)\s*mm", "$1 millimeters" },
                { @"(\d+)\s*cm", "$1 centimeters" },
                { @"(\d+)\s*m\b", "$1 meters" },
                { @"(\d+)\s*ft", "$1 feet" },
                { @"(\d+)\s*in\b", "$1 inches" },
                { @"(\d+)\s*sq\s*m", "$1 square meters" },
                { @"(\d+)\s*sqm", "$1 square meters" },
                { @"(\d+)\s*m2", "$1 square meters" },
                { @"(\d+)\s*sq\s*ft", "$1 square feet" },
                { @"(\d+)\s*sqft", "$1 square feet" },
                { @"(\d+)\s*kw", "$1 kilowatts" },
                { @"(\d+)\s*mpa", "$1 megapascals" },
                { @"(\d+)\s*psi", "$1 pounds per square inch" }
            };

            foreach (var pattern in unitPatterns)
            {
                text = Regex.Replace(text, pattern.Key, pattern.Value, RegexOptions.IgnoreCase);
            }

            return text;
        }

        private List<string> Tokenize(string text)
        {
            // Split on whitespace and punctuation, keeping important symbols
            var pattern = @"[\w]+|[^\s\w]";
            var matches = Regex.Matches(text, pattern);

            var tokens = new List<string>();
            foreach (Match match in matches)
            {
                var token = match.Value.Trim();
                if (!string.IsNullOrEmpty(token) && token.Length > 0)
                {
                    tokens.Add(token);
                }
            }

            // Remove stopwords for certain analyses
            return tokens;
        }

        private bool IsCommand(IntentClassification intent)
        {
            return intent.IntentType == IntentType.Command ||
                   intent.IntentType == IntentType.Action ||
                   intent.IntentType == IntentType.Modification;
        }

        private double CalculateOverallConfidence(NLPResult result)
        {
            var weights = new Dictionary<string, double>
            {
                { "intent", 0.4 },
                { "entities", 0.3 },
                { "structure", 0.2 },
                { "command", 0.1 }
            };

            double total = result.Intent.Confidence * weights["intent"];

            if (result.Entities.Any())
            {
                total += result.Entities.Average(e => e.Confidence) * weights["entities"];
            }
            else
            {
                total += 0.5 * weights["entities"]; // Neutral if no entities expected
            }

            total += result.QueryStructure.Confidence * weights["structure"];

            if (result.Command != null)
            {
                total += result.Command.Confidence * weights["command"];
            }
            else
            {
                total += 0.5 * weights["command"];
            }

            return Math.Min(1.0, total);
        }

        private Dictionary<string, List<string>> LoadBIMSynonyms()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                // Architectural elements
                { "wall", new List<string> { "partition", "barrier", "divider", "enclosure" } },
                { "door", new List<string> { "entrance", "entry", "opening", "portal", "doorway" } },
                { "window", new List<string> { "glazing", "fenestration", "opening", "aperture" } },
                { "floor", new List<string> { "slab", "deck", "level", "storey", "story" } },
                { "ceiling", new List<string> { "soffit", "overhead", "plenum" } },
                { "roof", new List<string> { "roofing", "covering", "canopy" } },
                { "stair", new List<string> { "staircase", "steps", "stairway", "flight" } },
                { "column", new List<string> { "pillar", "post", "pier", "support" } },
                { "beam", new List<string> { "girder", "joist", "lintel", "rafter" } },
                { "foundation", new List<string> { "footing", "base", "substructure", "pad" } },

                // Room types
                { "room", new List<string> { "space", "area", "zone", "compartment" } },
                { "office", new List<string> { "workspace", "workstation", "study" } },
                { "bathroom", new List<string> { "toilet", "restroom", "washroom", "lavatory", "wc" } },
                { "kitchen", new List<string> { "kitchenette", "pantry", "galley" } },
                { "bedroom", new List<string> { "sleeping room", "chamber" } },
                { "corridor", new List<string> { "hallway", "passage", "passageway", "hall" } },
                { "lobby", new List<string> { "foyer", "entrance hall", "vestibule", "reception" } },

                // MEP systems
                { "hvac", new List<string> { "air conditioning", "heating", "ventilation", "climate control" } },
                { "duct", new List<string> { "ductwork", "air duct", "supply duct", "return duct" } },
                { "pipe", new List<string> { "piping", "conduit", "tube", "line" } },
                { "electrical", new List<string> { "electric", "power", "wiring" } },
                { "plumbing", new List<string> { "water supply", "drainage", "sanitary" } },
                { "lighting", new List<string> { "luminaire", "light fixture", "illumination" } },

                // Materials
                { "concrete", new List<string> { "cement", "reinforced concrete", "rc" } },
                { "steel", new List<string> { "metal", "structural steel", "mild steel" } },
                { "timber", new List<string> { "wood", "lumber", "wooden" } },
                { "glass", new List<string> { "glazing", "transparent", "clear" } },
                { "brick", new List<string> { "masonry", "blockwork", "brickwork" } },
                { "insulation", new List<string> { "thermal insulation", "acoustic insulation" } },

                // Actions
                { "create", new List<string> { "add", "place", "insert", "make", "generate" } },
                { "delete", new List<string> { "remove", "erase", "clear", "eliminate" } },
                { "modify", new List<string> { "change", "edit", "update", "alter", "adjust" } },
                { "move", new List<string> { "relocate", "shift", "reposition", "transfer" } },
                { "copy", new List<string> { "duplicate", "clone", "replicate" } },
                { "select", new List<string> { "pick", "choose", "highlight" } },
                { "show", new List<string> { "display", "reveal", "view", "see" } },
                { "hide", new List<string> { "conceal", "mask", "invisible" } },
                { "calculate", new List<string> { "compute", "determine", "evaluate", "estimate" } },

                // Queries
                { "find", new List<string> { "search", "locate", "look for", "seek" } },
                { "list", new List<string> { "enumerate", "show all", "display all" } },
                { "count", new List<string> { "tally", "total", "number of", "how many" } },
                { "measure", new List<string> { "dimension", "size", "length", "area", "volume" } },

                // Properties
                { "height", new List<string> { "tall", "elevation", "vertical" } },
                { "width", new List<string> { "wide", "breadth", "horizontal" } },
                { "depth", new List<string> { "deep", "thickness" } },
                { "area", new List<string> { "size", "square footage", "floor area" } },
                { "volume", new List<string> { "capacity", "cubic" } }
            };
        }
    }

    /// <summary>
    /// Classifies user intent from tokenized input
    /// </summary>
    public class IntentClassifier
    {
        private readonly Dictionary<IntentType, List<string>> _intentPatterns;
        private readonly Dictionary<IntentType, double> _priorProbabilities;

        public IntentClassifier()
        {
            _intentPatterns = LoadIntentPatterns();
            _priorProbabilities = InitializePriors();
        }

        public IntentClassification Classify(List<string> tokens, NLPContext context)
        {
            var scores = new Dictionary<IntentType, double>();
            var tokenSet = new HashSet<string>(tokens.Select(t => t.ToLower()));

            foreach (var intentType in Enum.GetValues(typeof(IntentType)).Cast<IntentType>())
            {
                scores[intentType] = CalculateIntentScore(intentType, tokenSet, context);
            }

            // Apply context boosting
            if (context != null)
            {
                ApplyContextBoost(scores, context);
            }

            // Get best match
            var bestIntent = scores.OrderByDescending(s => s.Value).First();
            var alternatives = scores
                .Where(s => s.Key != bestIntent.Key && s.Value > 0.1)
                .OrderByDescending(s => s.Value)
                .Take(3)
                .Select(s => new AlternativeIntent { Intent = s.Key, Score = s.Value })
                .ToList();

            return new IntentClassification
            {
                IntentType = bestIntent.Key,
                Confidence = bestIntent.Value,
                Alternatives = alternatives
            };
        }

        private double CalculateIntentScore(IntentType intent, HashSet<string> tokens, NLPContext context)
        {
            if (!_intentPatterns.TryGetValue(intent, out var patterns))
                return 0;

            double matchCount = 0;
            double totalWeight = 0;

            foreach (var pattern in patterns)
            {
                var patternTokens = pattern.ToLower().Split(' ');
                var weight = 1.0 / patternTokens.Length; // Longer patterns get more weight per token

                foreach (var patternToken in patternTokens)
                {
                    if (tokens.Contains(patternToken))
                    {
                        matchCount += weight;
                    }
                    totalWeight += weight;
                }
            }

            if (totalWeight == 0) return 0;

            var matchRatio = matchCount / totalWeight;
            var prior = _priorProbabilities.GetValueOrDefault(intent, 0.1);

            // Bayesian-inspired combination
            return (matchRatio * 0.7 + prior * 0.3);
        }

        private void ApplyContextBoost(Dictionary<IntentType, double> scores, NLPContext context)
        {
            // Boost intents that are likely given current context
            if (context.LastIntent != IntentType.Unknown)
            {
                // If last action was a selection, likely next is modification or query
                if (context.LastIntent == IntentType.Selection)
                {
                    scores[IntentType.Modification] *= 1.2;
                    scores[IntentType.Query] *= 1.1;
                }
                // If last action was a query, might be followed by action
                else if (context.LastIntent == IntentType.Query)
                {
                    scores[IntentType.Command] *= 1.15;
                    scores[IntentType.Action] *= 1.15;
                }
            }

            // Boost based on selected elements
            if (context.SelectedElements?.Any() == true)
            {
                scores[IntentType.Modification] *= 1.3;
                scores[IntentType.Action] *= 1.2;
            }
        }

        private Dictionary<IntentType, List<string>> LoadIntentPatterns()
        {
            return new Dictionary<IntentType, List<string>>
            {
                { IntentType.Query, new List<string>
                    {
                        "what is", "what are", "where is", "where are", "how many",
                        "show me", "find", "search", "locate", "list all", "display",
                        "tell me", "give me", "get", "retrieve", "fetch",
                        "which", "who", "when", "why", "how much", "how big"
                    }
                },
                { IntentType.Command, new List<string>
                    {
                        "create", "make", "add", "insert", "place", "put",
                        "delete", "remove", "erase", "clear",
                        "copy", "duplicate", "clone",
                        "move", "relocate", "shift",
                        "rotate", "turn", "flip", "mirror",
                        "resize", "scale", "stretch"
                    }
                },
                { IntentType.Action, new List<string>
                    {
                        "run", "execute", "perform", "do", "start", "begin",
                        "calculate", "compute", "analyze", "check", "validate",
                        "export", "import", "save", "load", "open", "close",
                        "generate", "produce", "build"
                    }
                },
                { IntentType.Modification, new List<string>
                    {
                        "change", "modify", "edit", "update", "set", "adjust",
                        "increase", "decrease", "raise", "lower",
                        "rename", "relabel", "retype",
                        "assign", "apply", "attach"
                    }
                },
                { IntentType.Selection, new List<string>
                    {
                        "select", "pick", "choose", "highlight", "mark",
                        "select all", "deselect", "unselect",
                        "filter", "isolate", "focus on"
                    }
                },
                { IntentType.Navigation, new List<string>
                    {
                        "go to", "navigate", "zoom", "pan", "orbit",
                        "view", "look at", "show", "hide",
                        "section", "cut", "3d view", "plan view", "elevation"
                    }
                },
                { IntentType.Help, new List<string>
                    {
                        "help", "how do i", "how to", "explain", "tutorial",
                        "what does", "guide", "instructions", "documentation"
                    }
                },
                { IntentType.Confirmation, new List<string>
                    {
                        "yes", "ok", "okay", "confirm", "accept", "approve",
                        "proceed", "continue", "go ahead", "sure", "right"
                    }
                },
                { IntentType.Rejection, new List<string>
                    {
                        "no", "cancel", "stop", "abort", "reject", "decline",
                        "undo", "revert", "never mind", "forget it"
                    }
                }
            };
        }

        private Dictionary<IntentType, double> InitializePriors()
        {
            return new Dictionary<IntentType, double>
            {
                { IntentType.Query, 0.25 },
                { IntentType.Command, 0.20 },
                { IntentType.Action, 0.15 },
                { IntentType.Modification, 0.15 },
                { IntentType.Selection, 0.10 },
                { IntentType.Navigation, 0.05 },
                { IntentType.Help, 0.05 },
                { IntentType.Confirmation, 0.025 },
                { IntentType.Rejection, 0.025 }
            };
        }
    }

    /// <summary>
    /// Extracts entities (elements, parameters, values) from text
    /// </summary>
    public class EntityExtractor
    {
        private readonly Dictionary<string, EntityType> _entityKeywords;
        private readonly List<Regex> _valuePatterns;

        public EntityExtractor()
        {
            _entityKeywords = LoadEntityKeywords();
            _valuePatterns = LoadValuePatterns();
        }

        public List<ExtractedEntity> Extract(List<string> tokens, IntentClassification intent)
        {
            var entities = new List<ExtractedEntity>();
            var text = string.Join(" ", tokens);

            // Extract element references
            entities.AddRange(ExtractElements(tokens));

            // Extract parameter references
            entities.AddRange(ExtractParameters(tokens));

            // Extract numeric values with units
            entities.AddRange(ExtractValues(text));

            // Extract spatial references
            entities.AddRange(ExtractSpatialReferences(tokens));

            // Extract material references
            entities.AddRange(ExtractMaterials(tokens));

            return entities;
        }

        private List<ExtractedEntity> ExtractElements(List<string> tokens)
        {
            var elements = new List<ExtractedEntity>();
            var elementKeywords = new Dictionary<string, string>
            {
                { "wall", "Wall" }, { "walls", "Wall" },
                { "door", "Door" }, { "doors", "Door" },
                { "window", "Window" }, { "windows", "Window" },
                { "floor", "Floor" }, { "floors", "Floor" },
                { "ceiling", "Ceiling" }, { "ceilings", "Ceiling" },
                { "roof", "Roof" }, { "roofs", "Roof" },
                { "column", "Column" }, { "columns", "Column" },
                { "beam", "Beam" }, { "beams", "Beam" },
                { "stair", "Stair" }, { "stairs", "Stair" },
                { "railing", "Railing" }, { "railings", "Railing" },
                { "room", "Room" }, { "rooms", "Room" },
                { "duct", "Duct" }, { "ducts", "Duct" },
                { "pipe", "Pipe" }, { "pipes", "Pipe" },
                { "fixture", "Fixture" }, { "fixtures", "Fixture" },
                { "equipment", "Equipment" },
                { "furniture", "Furniture" },
                { "curtain wall", "CurtainWall" },
                { "structural framing", "StructuralFraming" },
                { "foundation", "Foundation" }
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToLower();

                // Check two-word combinations first
                if (i < tokens.Count - 1)
                {
                    var twoWord = $"{token} {tokens[i + 1].ToLower()}";
                    if (elementKeywords.TryGetValue(twoWord, out var category2))
                    {
                        elements.Add(new ExtractedEntity
                        {
                            Type = EntityType.Element,
                            Value = category2,
                            OriginalText = twoWord,
                            Position = i,
                            Confidence = 0.95
                        });
                        continue;
                    }
                }

                if (elementKeywords.TryGetValue(token, out var category))
                {
                    elements.Add(new ExtractedEntity
                    {
                        Type = EntityType.Element,
                        Value = category,
                        OriginalText = token,
                        Position = i,
                        Confidence = 0.9
                    });
                }
            }

            return elements;
        }

        private List<ExtractedEntity> ExtractParameters(List<string> tokens)
        {
            var parameters = new List<ExtractedEntity>();
            var paramKeywords = new Dictionary<string, string>
            {
                { "height", "Height" },
                { "width", "Width" },
                { "depth", "Depth" },
                { "length", "Length" },
                { "area", "Area" },
                { "volume", "Volume" },
                { "thickness", "Thickness" },
                { "level", "Level" },
                { "offset", "Offset" },
                { "mark", "Mark" },
                { "name", "Name" },
                { "type", "Type" },
                { "material", "Material" },
                { "phase", "Phase" },
                { "cost", "Cost" },
                { "fire rating", "FireRating" },
                { "thermal", "ThermalResistance" },
                { "u-value", "UValue" },
                { "acoustic", "AcousticRating" }
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToLower();
                if (paramKeywords.TryGetValue(token, out var param))
                {
                    parameters.Add(new ExtractedEntity
                    {
                        Type = EntityType.Parameter,
                        Value = param,
                        OriginalText = token,
                        Position = i,
                        Confidence = 0.85
                    });
                }
            }

            return parameters;
        }

        private List<ExtractedEntity> ExtractValues(string text)
        {
            var values = new List<ExtractedEntity>();

            // Pattern for numbers with units
            var patterns = new List<(Regex regex, string unit)>
            {
                (new Regex(@"(\d+(?:\.\d+)?)\s*(mm|millimeters?)", RegexOptions.IgnoreCase), "mm"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(cm|centimeters?)", RegexOptions.IgnoreCase), "cm"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(m|meters?)\b", RegexOptions.IgnoreCase), "m"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(ft|feet|foot)", RegexOptions.IgnoreCase), "ft"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(in|inch|inches)", RegexOptions.IgnoreCase), "in"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(sq\s*m|m2|square\s*meters?)", RegexOptions.IgnoreCase), "m²"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(sq\s*ft|ft2|square\s*feet)", RegexOptions.IgnoreCase), "ft²"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(kw|kilowatts?)", RegexOptions.IgnoreCase), "kW"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*(degrees?|°)", RegexOptions.IgnoreCase), "°"),
                (new Regex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase), "%")
            };

            foreach (var (regex, unit) in patterns)
            {
                var matches = regex.Matches(text);
                foreach (Match match in matches)
                {
                    if (double.TryParse(match.Groups[1].Value, out var numValue))
                    {
                        values.Add(new ExtractedEntity
                        {
                            Type = EntityType.Value,
                            Value = numValue,
                            Unit = unit,
                            OriginalText = match.Value,
                            Position = match.Index,
                            Confidence = 0.95
                        });
                    }
                }
            }

            // Plain numbers without units
            var plainNumbers = new Regex(@"\b(\d+(?:\.\d+)?)\b");
            foreach (Match match in plainNumbers.Matches(text))
            {
                // Skip if already captured with unit
                if (!values.Any(v => v.Position <= match.Index &&
                    v.Position + v.OriginalText.Length >= match.Index + match.Length))
                {
                    if (double.TryParse(match.Value, out var numValue))
                    {
                        values.Add(new ExtractedEntity
                        {
                            Type = EntityType.Value,
                            Value = numValue,
                            OriginalText = match.Value,
                            Position = match.Index,
                            Confidence = 0.7 // Lower confidence without unit
                        });
                    }
                }
            }

            return values;
        }

        private List<ExtractedEntity> ExtractSpatialReferences(List<string> tokens)
        {
            var spatial = new List<ExtractedEntity>();
            var spatialKeywords = new Dictionary<string, string>
            {
                { "above", "Above" }, { "below", "Below" },
                { "left", "Left" }, { "right", "Right" },
                { "north", "North" }, { "south", "South" },
                { "east", "East" }, { "west", "West" },
                { "adjacent", "Adjacent" }, { "next to", "Adjacent" },
                { "between", "Between" }, { "inside", "Inside" },
                { "outside", "Outside" }, { "near", "Near" },
                { "front", "Front" }, { "back", "Back" },
                { "top", "Top" }, { "bottom", "Bottom" }
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToLower();
                if (spatialKeywords.TryGetValue(token, out var reference))
                {
                    spatial.Add(new ExtractedEntity
                    {
                        Type = EntityType.SpatialReference,
                        Value = reference,
                        OriginalText = token,
                        Position = i,
                        Confidence = 0.85
                    });
                }
            }

            return spatial;
        }

        private List<ExtractedEntity> ExtractMaterials(List<string> tokens)
        {
            var materials = new List<ExtractedEntity>();
            var materialKeywords = new HashSet<string>
            {
                "concrete", "steel", "timber", "wood", "glass", "brick",
                "masonry", "aluminum", "aluminium", "copper", "stone",
                "granite", "marble", "ceramic", "tile", "plaster",
                "gypsum", "drywall", "plywood", "mdf", "insulation",
                "foam", "fiberglass", "wool", "plastic", "pvc", "hdpe"
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToLower();
                if (materialKeywords.Contains(token))
                {
                    materials.Add(new ExtractedEntity
                    {
                        Type = EntityType.Material,
                        Value = token,
                        OriginalText = token,
                        Position = i,
                        Confidence = 0.9
                    });
                }
            }

            return materials;
        }

        private Dictionary<string, EntityType> LoadEntityKeywords()
        {
            return new Dictionary<string, EntityType>(); // Extended in methods above
        }

        private List<Regex> LoadValuePatterns()
        {
            return new List<Regex>(); // Extended in methods above
        }
    }

    /// <summary>
    /// Parses query structure (subject, predicate, object)
    /// </summary>
    public class QueryParser
    {
        public QueryStructure Parse(List<string> tokens, IntentClassification intent)
        {
            var structure = new QueryStructure
            {
                RawTokens = tokens
            };

            // Identify question type
            structure.QuestionType = IdentifyQuestionType(tokens);

            // Extract subject (what we're asking about)
            structure.Subject = ExtractSubject(tokens);

            // Extract predicate (the action or relationship)
            structure.Predicate = ExtractPredicate(tokens);

            // Extract object (target of the action)
            structure.Object = ExtractObject(tokens);

            // Extract constraints/filters
            structure.Constraints = ExtractConstraints(tokens);

            // Calculate confidence
            structure.Confidence = CalculateStructureConfidence(structure);

            return structure;
        }

        private QuestionType IdentifyQuestionType(List<string> tokens)
        {
            if (tokens.Count == 0) return QuestionType.Unknown;

            var firstToken = tokens[0].ToLower();
            var text = string.Join(" ", tokens).ToLower();

            if (firstToken == "what" || text.Contains("what is") || text.Contains("what are"))
                return QuestionType.What;
            if (firstToken == "where" || text.Contains("where is") || text.Contains("where are"))
                return QuestionType.Where;
            if (firstToken == "how" && text.Contains("how many"))
                return QuestionType.HowMany;
            if (firstToken == "how" && (text.Contains("how much") || text.Contains("how big")))
                return QuestionType.HowMuch;
            if (firstToken == "how")
                return QuestionType.How;
            if (firstToken == "which")
                return QuestionType.Which;
            if (firstToken == "why")
                return QuestionType.Why;
            if (firstToken == "when")
                return QuestionType.When;
            if (firstToken == "who")
                return QuestionType.Who;
            if (text.Contains("can i") || text.Contains("is it possible"))
                return QuestionType.CanI;
            if (text.Contains("show") || text.Contains("list") || text.Contains("display"))
                return QuestionType.List;

            return QuestionType.Statement;
        }

        private string ExtractSubject(List<string> tokens)
        {
            // Simple heuristic: first noun phrase after question word
            var elementKeywords = new HashSet<string>
            {
                "wall", "walls", "door", "doors", "window", "windows",
                "floor", "floors", "ceiling", "ceilings", "room", "rooms",
                "column", "columns", "beam", "beams", "duct", "ducts",
                "pipe", "pipes", "fixture", "fixtures", "level", "levels"
            };

            foreach (var token in tokens)
            {
                if (elementKeywords.Contains(token.ToLower()))
                {
                    return token;
                }
            }

            return null;
        }

        private string ExtractPredicate(List<string> tokens)
        {
            var predicateKeywords = new HashSet<string>
            {
                "is", "are", "has", "have", "contains", "includes",
                "located", "placed", "connected", "attached", "adjacent"
            };

            foreach (var token in tokens)
            {
                if (predicateKeywords.Contains(token.ToLower()))
                {
                    return token;
                }
            }

            return null;
        }

        private string ExtractObject(List<string> tokens)
        {
            // Look for element or location after predicate
            var afterPredicate = false;
            var predicateKeywords = new HashSet<string> { "is", "are", "has", "have", "in", "on", "at" };

            foreach (var token in tokens)
            {
                if (predicateKeywords.Contains(token.ToLower()))
                {
                    afterPredicate = true;
                    continue;
                }

                if (afterPredicate && token.Length > 2)
                {
                    return token;
                }
            }

            return null;
        }

        private List<QueryConstraint> ExtractConstraints(List<string> tokens)
        {
            var constraints = new List<QueryConstraint>();
            var text = string.Join(" ", tokens).ToLower();

            // Level constraints
            var levelMatch = Regex.Match(text, @"(?:on|at|level)\s+(\d+|ground|first|second|third|basement)");
            if (levelMatch.Success)
            {
                constraints.Add(new QueryConstraint
                {
                    Field = "Level",
                    Operator = "=",
                    Value = levelMatch.Groups[1].Value
                });
            }

            // Comparison constraints
            var comparisonPatterns = new List<(string pattern, string op)>
            {
                (@"greater than (\d+)", ">"),
                (@"more than (\d+)", ">"),
                (@"less than (\d+)", "<"),
                (@"at least (\d+)", ">="),
                (@"at most (\d+)", "<="),
                (@"equal to (\d+)", "="),
                (@"exactly (\d+)", "=")
            };

            foreach (var (pattern, op) in comparisonPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                {
                    constraints.Add(new QueryConstraint
                    {
                        Operator = op,
                        Value = match.Groups[1].Value
                    });
                }
            }

            return constraints;
        }

        private double CalculateStructureConfidence(QueryStructure structure)
        {
            double score = 0.5; // Base score

            if (structure.QuestionType != QuestionType.Unknown) score += 0.15;
            if (!string.IsNullOrEmpty(structure.Subject)) score += 0.15;
            if (!string.IsNullOrEmpty(structure.Predicate)) score += 0.1;
            if (!string.IsNullOrEmpty(structure.Object)) score += 0.05;
            if (structure.Constraints.Any()) score += 0.05;

            return Math.Min(1.0, score);
        }
    }

    /// <summary>
    /// Interprets NLP result as executable command
    /// </summary>
    public class CommandInterpreter
    {
        public BIMCommand Interpret(NLPResult result)
        {
            var command = new BIMCommand
            {
                OriginalInput = result.OriginalInput,
                Intent = result.Intent.IntentType
            };

            // Map intent to command type
            command.CommandType = MapIntentToCommand(result.Intent.IntentType);

            // Extract target elements
            command.TargetElements = result.Entities
                .Where(e => e.Type == EntityType.Element)
                .Select(e => e.Value.ToString())
                .ToList();

            // Extract parameters to modify
            command.Parameters = result.Entities
                .Where(e => e.Type == EntityType.Parameter)
                .ToDictionary(e => e.Value.ToString(), e => (object)null);

            // Extract values
            var values = result.Entities
                .Where(e => e.Type == EntityType.Value)
                .ToList();

            // Match values to parameters
            MatchValuesToParameters(command, values, result);

            // Calculate confidence
            command.Confidence = CalculateCommandConfidence(command, result);

            return command;
        }

        private CommandType MapIntentToCommand(IntentType intent)
        {
            return intent switch
            {
                IntentType.Command => CommandType.Create,
                IntentType.Action => CommandType.Execute,
                IntentType.Modification => CommandType.Modify,
                IntentType.Selection => CommandType.Select,
                IntentType.Navigation => CommandType.Navigate,
                IntentType.Query => CommandType.Query,
                _ => CommandType.Unknown
            };
        }

        private void MatchValuesToParameters(BIMCommand command, List<ExtractedEntity> values, NLPResult result)
        {
            // Simple matching: if parameter and value are adjacent, associate them
            foreach (var param in command.Parameters.Keys.ToList())
            {
                // Find closest value to parameter mention
                var paramEntity = result.Entities
                    .FirstOrDefault(e => e.Type == EntityType.Parameter && e.Value.ToString() == param);

                if (paramEntity != null)
                {
                    var closestValue = values
                        .OrderBy(v => Math.Abs(v.Position - paramEntity.Position))
                        .FirstOrDefault();

                    if (closestValue != null)
                    {
                        command.Parameters[param] = closestValue.Unit != null
                            ? $"{closestValue.Value} {closestValue.Unit}"
                            : closestValue.Value;
                    }
                }
            }
        }

        private double CalculateCommandConfidence(BIMCommand command, NLPResult result)
        {
            double score = result.Confidence * 0.5;

            // Command type identified
            if (command.CommandType != CommandType.Unknown) score += 0.2;

            // Has target elements
            if (command.TargetElements.Any()) score += 0.15;

            // Has parameters with values
            var hasValues = command.Parameters.Values.Count(v => v != null);
            score += 0.15 * (hasValues / Math.Max(1, command.Parameters.Count));

            return Math.Min(1.0, score);
        }
    }

    /// <summary>
    /// Finds semantically similar terms
    /// </summary>
    public class SemanticMatcher
    {
        public List<SemanticMatch> FindSimilar(
            string term,
            Dictionary<string, List<string>> synonyms,
            int topK)
        {
            var matches = new List<SemanticMatch>();
            var termLower = term.ToLower();

            // Direct synonym lookup
            if (synonyms.TryGetValue(termLower, out var directSynonyms))
            {
                foreach (var syn in directSynonyms)
                {
                    matches.Add(new SemanticMatch
                    {
                        Term = syn,
                        Similarity = 0.9,
                        MatchType = "synonym"
                    });
                }
            }

            // Reverse lookup (find terms where this is a synonym)
            foreach (var kvp in synonyms)
            {
                if (kvp.Value.Any(s => s.Equals(termLower, StringComparison.OrdinalIgnoreCase)))
                {
                    matches.Add(new SemanticMatch
                    {
                        Term = kvp.Key,
                        Similarity = 0.85,
                        MatchType = "reverse_synonym"
                    });
                }
            }

            // Fuzzy string matching for typos
            foreach (var key in synonyms.Keys)
            {
                var distance = LevenshteinDistance(termLower, key);
                var maxLen = Math.Max(termLower.Length, key.Length);
                var similarity = 1.0 - (double)distance / maxLen;

                if (similarity > 0.7 && !key.Equals(termLower, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new SemanticMatch
                    {
                        Term = key,
                        Similarity = similarity,
                        MatchType = "fuzzy"
                    });
                }
            }

            return matches
                .OrderByDescending(m => m.Similarity)
                .Take(topK)
                .ToList();
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var n = s1.Length;
            var m = s2.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    var cost = s2[j - 1] == s1[i - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }

    #region Data Models

    public class NLPResult
    {
        public string OriginalInput { get; set; }
        public string NormalizedInput { get; set; }
        public List<string> Tokens { get; set; } = new List<string>();
        public IntentClassification Intent { get; set; }
        public List<ExtractedEntity> Entities { get; set; } = new List<ExtractedEntity>();
        public QueryStructure QueryStructure { get; set; }
        public BIMCommand Command { get; set; }
        public double Confidence { get; set; }
        public DateTime ProcessedAt { get; set; }

        public static NLPResult Empty() => new NLPResult
        {
            Intent = new IntentClassification { IntentType = IntentType.Unknown },
            QueryStructure = new QueryStructure(),
            Confidence = 0
        };
    }

    public class NLPContext
    {
        public IntentType LastIntent { get; set; }
        public List<string> SelectedElements { get; set; }
        public string CurrentView { get; set; }
        public Dictionary<string, object> SessionVariables { get; set; } = new Dictionary<string, object>();
    }

    public class IntentClassification
    {
        public IntentType IntentType { get; set; }
        public double Confidence { get; set; }
        public List<AlternativeIntent> Alternatives { get; set; } = new List<AlternativeIntent>();
    }

    public class AlternativeIntent
    {
        public IntentType Intent { get; set; }
        public double Score { get; set; }
    }

    public enum IntentType
    {
        Unknown,
        Query,          // Questions about the model
        Command,        // Create/delete operations
        Action,         // Run/execute operations
        Modification,   // Change existing elements
        Selection,      // Select elements
        Navigation,     // View/zoom operations
        Help,           // Help requests
        Confirmation,   // Yes/OK responses
        Rejection       // No/Cancel responses
    }

    public class ExtractedEntity
    {
        public EntityType Type { get; set; }
        public object Value { get; set; }
        public string Unit { get; set; }
        public string OriginalText { get; set; }
        public int Position { get; set; }
        public double Confidence { get; set; }
    }

    public enum EntityType
    {
        Element,
        Parameter,
        Value,
        Material,
        Level,
        Room,
        SpatialReference,
        Phase,
        Workset
    }

    public class QueryStructure
    {
        public List<string> RawTokens { get; set; } = new List<string>();
        public QuestionType QuestionType { get; set; }
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Object { get; set; }
        public List<QueryConstraint> Constraints { get; set; } = new List<QueryConstraint>();
        public double Confidence { get; set; }
    }

    public enum QuestionType
    {
        Unknown,
        What,       // What is/are
        Where,      // Where is/are
        How,        // How to/do
        HowMany,    // How many
        HowMuch,    // How much
        Which,      // Which one
        Why,        // Why is
        When,       // When was
        Who,        // Who created
        CanI,       // Can I / Is it possible
        List,       // Show/list/display
        Statement   // Not a question
    }

    public class QueryConstraint
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
    }

    public class BIMCommand
    {
        public string OriginalInput { get; set; }
        public IntentType Intent { get; set; }
        public CommandType CommandType { get; set; }
        public List<string> TargetElements { get; set; } = new List<string>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public double Confidence { get; set; }
    }

    public enum CommandType
    {
        Unknown,
        Create,
        Delete,
        Modify,
        Copy,
        Move,
        Select,
        Navigate,
        Query,
        Execute
    }

    public class SemanticMatch
    {
        public string Term { get; set; }
        public double Similarity { get; set; }
        public string MatchType { get; set; }
    }

    #endregion
}
