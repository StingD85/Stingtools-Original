// ============================================================================
// StingBIM.AI.Creation - Prompt-to-Model Orchestrator
// Natural language to BIM element creation pipeline
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Creation.Orchestration
{
    /// <summary>
    /// Prompt-to-Model Orchestrator
    /// Converts natural language commands into BIM element creation
    /// </summary>
    public sealed class PromptToModelOrchestrator
    {
        private static readonly Lazy<PromptToModelOrchestrator> _instance =
            new Lazy<PromptToModelOrchestrator>(() => new PromptToModelOrchestrator());
        public static PromptToModelOrchestrator Instance => _instance.Value;

        // ISO 19650 Parameter Constants
        private const string PARAM_CREATED_BY = "MR_CREATED_BY";
        private const string PARAM_CREATION_DATE = "MR_CREATION_DATE";
        private const string PARAM_CREATION_METHOD = "MR_CREATION_METHOD";

        private readonly Dictionary<string, IntentHandler> _intentHandlers;
        private readonly Dictionary<string, string[]> _elementSynonyms;
        private readonly Dictionary<string, UnitConversion> _unitConversions;
        private readonly List<ConversationContext> _contextHistory;
        private readonly object _lockObject = new object();

        public event EventHandler<ModelCreationEventArgs> ModelCreated;
        public event EventHandler<ModelCreationEventArgs> CreationFailed;
        public event EventHandler<ClarificationEventArgs> ClarificationNeeded;

        private PromptToModelOrchestrator()
        {
            _intentHandlers = InitializeIntentHandlers();
            _elementSynonyms = InitializeElementSynonyms();
            _unitConversions = InitializeUnitConversions();
            _contextHistory = new List<ConversationContext>();
        }

        #region Initialization

        private Dictionary<string, IntentHandler> InitializeIntentHandlers()
        {
            return new Dictionary<string, IntentHandler>(StringComparer.OrdinalIgnoreCase)
            {
                ["CREATE"] = new IntentHandler
                {
                    Intent = ModelIntent.Create,
                    Patterns = new[]
                    {
                        @"(?:create|make|add|draw|build)\s+(?:a\s+)?(.+)",
                        @"(?:i\s+(?:want|need))\s+(?:a\s+)?(.+)",
                        @"(?:place|put)\s+(?:a\s+)?(.+)"
                    },
                    Handler = HandleCreateIntent
                },
                ["MODIFY"] = new IntentHandler
                {
                    Intent = ModelIntent.Modify,
                    Patterns = new[]
                    {
                        @"(?:change|modify|edit|update)\s+(?:the\s+)?(.+)",
                        @"(?:make|set)\s+(?:the\s+)?(.+)\s+(?:to|=)\s+(.+)"
                    },
                    Handler = HandleModifyIntent
                },
                ["DELETE"] = new IntentHandler
                {
                    Intent = ModelIntent.Delete,
                    Patterns = new[]
                    {
                        @"(?:delete|remove|erase)\s+(?:the\s+)?(.+)",
                        @"(?:get\s+rid\s+of)\s+(?:the\s+)?(.+)"
                    },
                    Handler = HandleDeleteIntent
                },
                ["COPY"] = new IntentHandler
                {
                    Intent = ModelIntent.Copy,
                    Patterns = new[]
                    {
                        @"(?:copy|duplicate|clone)\s+(?:the\s+)?(.+?)(?:\s+to\s+(.+))?$",
                        @"(?:make\s+(?:a\s+)?copy\s+of)\s+(?:the\s+)?(.+)"
                    },
                    Handler = HandleCopyIntent
                },
                ["MOVE"] = new IntentHandler
                {
                    Intent = ModelIntent.Move,
                    Patterns = new[]
                    {
                        @"(?:move|relocate|shift)\s+(?:the\s+)?(.+?)\s+(?:to|by)\s+(.+)",
                        @"(?:drag)\s+(?:the\s+)?(.+?)\s+(.+)"
                    },
                    Handler = HandleMoveIntent
                },
                ["ARRAY"] = new IntentHandler
                {
                    Intent = ModelIntent.Array,
                    Patterns = new[]
                    {
                        @"(?:array|repeat)\s+(?:the\s+)?(.+?)\s+(\d+)\s+times",
                        @"(?:create|make)\s+(\d+)\s+(?:copies?\s+of\s+)?(.+)"
                    },
                    Handler = HandleArrayIntent
                },
                ["QUERY"] = new IntentHandler
                {
                    Intent = ModelIntent.Query,
                    Patterns = new[]
                    {
                        @"(?:what\s+is|show\s+me|list)\s+(?:the\s+)?(.+)",
                        @"(?:how\s+many|count)\s+(.+)",
                        @"(?:find|search\s+for)\s+(.+)"
                    },
                    Handler = HandleQueryIntent
                },
                ["LAYOUT"] = new IntentHandler
                {
                    Intent = ModelIntent.Layout,
                    Patterns = new[]
                    {
                        @"(?:layout|arrange|design)\s+(?:a\s+)?(.+)",
                        @"(?:generate|auto-?generate)\s+(?:a\s+)?(.+)"
                    },
                    Handler = HandleLayoutIntent
                }
            };
        }

        private Dictionary<string, string[]> InitializeElementSynonyms()
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Walls
                ["Wall"] = new[] { "wall", "partition", "barrier", "divider" },
                ["ExteriorWall"] = new[] { "exterior wall", "outer wall", "external wall", "facade wall" },
                ["CurtainWall"] = new[] { "curtain wall", "glass wall", "glazed wall", "curtain" },

                // Floors
                ["Floor"] = new[] { "floor", "slab", "deck", "flooring" },
                ["Roof"] = new[] { "roof", "roofing", "cover", "top" },
                ["Ceiling"] = new[] { "ceiling", "soffit", "suspended ceiling", "drop ceiling" },

                // Openings
                ["Door"] = new[] { "door", "entry", "entrance", "doorway", "opening" },
                ["Window"] = new[] { "window", "glazing", "fenestration" },

                // Structural
                ["Column"] = new[] { "column", "pillar", "post", "pier", "support" },
                ["Beam"] = new[] { "beam", "girder", "joist", "lintel", "header" },
                ["Brace"] = new[] { "brace", "bracing", "strut" },

                // Stairs
                ["Stair"] = new[] { "stair", "stairs", "staircase", "stairway", "steps" },
                ["Ramp"] = new[] { "ramp", "ramp", "slope", "incline" },
                ["Railing"] = new[] { "railing", "rail", "handrail", "guardrail", "balustrade" },

                // Rooms
                ["Room"] = new[] { "room", "space", "area" },
                ["Bedroom"] = new[] { "bedroom", "bed room", "sleeping room", "master bedroom" },
                ["Bathroom"] = new[] { "bathroom", "bath room", "restroom", "toilet room", "washroom", "wc" },
                ["Kitchen"] = new[] { "kitchen", "kitchenette" },
                ["LivingRoom"] = new[] { "living room", "lounge", "sitting room", "family room" },
                ["Office"] = new[] { "office", "study", "home office", "workspace" },
                ["Corridor"] = new[] { "corridor", "hallway", "passage", "passageway" },

                // MEP
                ["Duct"] = new[] { "duct", "ductwork", "air duct", "hvac duct" },
                ["Pipe"] = new[] { "pipe", "piping", "plumbing" },
                ["CableTray"] = new[] { "cable tray", "tray", "wire tray" },
                ["Conduit"] = new[] { "conduit", "electrical conduit" },

                // Fixtures
                ["Toilet"] = new[] { "toilet", "wc", "water closet", "commode" },
                ["Sink"] = new[] { "sink", "basin", "lavatory", "wash basin" },
                ["Shower"] = new[] { "shower", "shower unit", "shower enclosure" },
                ["Bathtub"] = new[] { "bathtub", "bath", "tub" },
                ["LightFixture"] = new[] { "light", "light fixture", "lighting", "lamp", "luminaire" },
                ["Receptacle"] = new[] { "receptacle", "outlet", "socket", "plug", "power outlet" },
                ["Switch"] = new[] { "switch", "light switch" },

                // Furniture
                ["Desk"] = new[] { "desk", "table", "work table", "workstation" },
                ["Chair"] = new[] { "chair", "seat", "seating" },
                ["Sofa"] = new[] { "sofa", "couch", "settee" },
                ["Bed"] = new[] { "bed", "double bed", "single bed", "queen bed", "king bed" },
                ["Cabinet"] = new[] { "cabinet", "cupboard", "storage", "wardrobe", "closet" }
            };
        }

        private Dictionary<string, UnitConversion> InitializeUnitConversions()
        {
            return new Dictionary<string, UnitConversion>(StringComparer.OrdinalIgnoreCase)
            {
                // Length
                ["m"] = new UnitConversion { Factor = 1000, Unit = "mm" },
                ["meter"] = new UnitConversion { Factor = 1000, Unit = "mm" },
                ["meters"] = new UnitConversion { Factor = 1000, Unit = "mm" },
                ["metre"] = new UnitConversion { Factor = 1000, Unit = "mm" },
                ["metres"] = new UnitConversion { Factor = 1000, Unit = "mm" },
                ["cm"] = new UnitConversion { Factor = 10, Unit = "mm" },
                ["centimeter"] = new UnitConversion { Factor = 10, Unit = "mm" },
                ["centimeters"] = new UnitConversion { Factor = 10, Unit = "mm" },
                ["mm"] = new UnitConversion { Factor = 1, Unit = "mm" },
                ["millimeter"] = new UnitConversion { Factor = 1, Unit = "mm" },
                ["millimeters"] = new UnitConversion { Factor = 1, Unit = "mm" },
                ["ft"] = new UnitConversion { Factor = 304.8, Unit = "mm" },
                ["foot"] = new UnitConversion { Factor = 304.8, Unit = "mm" },
                ["feet"] = new UnitConversion { Factor = 304.8, Unit = "mm" },
                ["'"] = new UnitConversion { Factor = 304.8, Unit = "mm" },
                ["in"] = new UnitConversion { Factor = 25.4, Unit = "mm" },
                ["inch"] = new UnitConversion { Factor = 25.4, Unit = "mm" },
                ["inches"] = new UnitConversion { Factor = 25.4, Unit = "mm" },
                ["\""] = new UnitConversion { Factor = 25.4, Unit = "mm" },

                // Area
                ["sqm"] = new UnitConversion { Factor = 1000000, Unit = "mm²" },
                ["sq m"] = new UnitConversion { Factor = 1000000, Unit = "mm²" },
                ["m2"] = new UnitConversion { Factor = 1000000, Unit = "mm²" },
                ["m²"] = new UnitConversion { Factor = 1000000, Unit = "mm²" },
                ["sqft"] = new UnitConversion { Factor = 92903.04, Unit = "mm²" },
                ["sq ft"] = new UnitConversion { Factor = 92903.04, Unit = "mm²" },
                ["ft2"] = new UnitConversion { Factor = 92903.04, Unit = "mm²" }
            };
        }

        #endregion

        #region Main Processing

        /// <summary>
        /// Process a natural language prompt and create BIM elements
        /// </summary>
        public async Task<OrchestratorResult> ProcessPromptAsync(
            string prompt,
            ProcessingOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ProcessingOptions();
            var result = new OrchestratorResult { OriginalPrompt = prompt };

            try
            {
                // 1. Preprocess prompt
                var cleanedPrompt = PreprocessPrompt(prompt);

                // 2. Detect intent
                var intentResult = DetectIntent(cleanedPrompt);
                result.DetectedIntent = intentResult.Intent;

                if (intentResult.Intent == ModelIntent.Unknown)
                {
                    result.Success = false;
                    result.Message = "Could not understand the request. Please try rephrasing.";
                    return result;
                }

                // 3. Extract entities
                var entities = ExtractEntities(cleanedPrompt, intentResult);
                result.ExtractedEntities = entities;

                // 4. Resolve references from context
                if (options.UseContext)
                {
                    ResolveContextReferences(entities);
                }

                // 5. Validate entities
                var validation = ValidateEntities(entities, intentResult.Intent);
                if (!validation.IsValid)
                {
                    if (validation.MissingInfo.Any())
                    {
                        // Request clarification
                        OnClarificationNeeded(new ClarificationRequest
                        {
                            OriginalPrompt = prompt,
                            MissingInformation = validation.MissingInfo,
                            SuggestedQuestions = GenerateClarificationQuestions(validation.MissingInfo)
                        });

                        result.Success = false;
                        result.RequiresClarification = true;
                        result.ClarificationQuestions = GenerateClarificationQuestions(validation.MissingInfo);
                        return result;
                    }
                }

                // 6. Execute the intent
                var executionResult = await ExecuteIntentAsync(intentResult, entities, options, cancellationToken);
                result.Success = executionResult.Success;
                result.CreatedElements = executionResult.CreatedElements;
                result.Message = executionResult.Message;

                // 7. Update context
                if (result.Success && options.UseContext)
                {
                    UpdateContext(prompt, intentResult, entities, executionResult);
                }

                if (result.Success)
                {
                    OnModelCreated(result);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                OnCreationFailed(result);
            }

            return result;
        }

        /// <summary>
        /// Process multiple prompts in sequence
        /// </summary>
        public async Task<BatchResult> ProcessBatchAsync(
            IEnumerable<string> prompts,
            ProcessingOptions options = null,
            IProgress<BatchProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ProcessingOptions { UseContext = true };
            var results = new List<OrchestratorResult>();
            var promptList = prompts.ToList();
            int total = promptList.Count;
            int current = 0;

            foreach (var prompt in promptList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new BatchProgress(++current, total, prompt));

                var result = await ProcessPromptAsync(prompt, options, cancellationToken);
                results.Add(result);
            }

            return new BatchResult
            {
                Results = results,
                TotalPrompts = total,
                SuccessfulCount = results.Count(r => r.Success),
                FailedCount = results.Count(r => !r.Success)
            };
        }

        #endregion

        #region Intent Detection

        private IntentResult DetectIntent(string prompt)
        {
            foreach (var handler in _intentHandlers.Values)
            {
                foreach (var pattern in handler.Patterns)
                {
                    var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return new IntentResult
                        {
                            Intent = handler.Intent,
                            Handler = handler,
                            Match = match,
                            Confidence = CalculateConfidence(match, pattern)
                        };
                    }
                }
            }

            return new IntentResult { Intent = ModelIntent.Unknown };
        }

        private double CalculateConfidence(Match match, string pattern)
        {
            // Simple confidence based on match coverage
            double coverage = (double)match.Length / match.Value.Length;
            return Math.Min(1.0, coverage * 1.2);
        }

        #endregion

        #region Entity Extraction

        private ExtractedEntities ExtractEntities(string prompt, IntentResult intentResult)
        {
            var entities = new ExtractedEntities();

            // Extract element type
            entities.ElementType = ExtractElementType(prompt);

            // Extract dimensions
            entities.Dimensions = ExtractDimensions(prompt);

            // Extract location
            entities.Location = ExtractLocation(prompt);

            // Extract quantity
            entities.Quantity = ExtractQuantity(prompt);

            // Extract properties
            entities.Properties = ExtractProperties(prompt);

            // Extract relationships
            entities.Relationships = ExtractRelationships(prompt);

            return entities;
        }

        private string ExtractElementType(string prompt)
        {
            var promptLower = prompt.ToLowerInvariant();

            foreach (var kvp in _elementSynonyms)
            {
                foreach (var synonym in kvp.Value)
                {
                    if (promptLower.Contains(synonym))
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }

        private DimensionSet ExtractDimensions(string prompt)
        {
            var dimensions = new DimensionSet();

            // Pattern: 3m x 4m, 3x4m, 3 by 4 meters
            var sizePattern = @"(\d+(?:\.\d+)?)\s*(?:x|by|×)\s*(\d+(?:\.\d+)?)\s*(\w+)?";
            var sizeMatch = Regex.Match(prompt, sizePattern, RegexOptions.IgnoreCase);

            if (sizeMatch.Success)
            {
                double value1 = double.Parse(sizeMatch.Groups[1].Value);
                double value2 = double.Parse(sizeMatch.Groups[2].Value);
                string unit = sizeMatch.Groups[3].Value;

                double factor = GetUnitFactor(unit);
                dimensions.Length = value1 * factor;
                dimensions.Width = value2 * factor;
            }

            // Pattern: 3m wall, 2.5m high
            var singleDimPattern = @"(\d+(?:\.\d+)?)\s*(\w+)?\s*(?:tall|high|long|wide|thick)";
            var singleMatch = Regex.Match(prompt, singleDimPattern, RegexOptions.IgnoreCase);

            if (singleMatch.Success)
            {
                double value = double.Parse(singleMatch.Groups[1].Value);
                string unit = singleMatch.Groups[2].Value;
                double factor = GetUnitFactor(unit);

                if (prompt.ToLower().Contains("tall") || prompt.ToLower().Contains("high"))
                {
                    dimensions.Height = value * factor;
                }
                else if (prompt.ToLower().Contains("long"))
                {
                    dimensions.Length = value * factor;
                }
                else if (prompt.ToLower().Contains("wide"))
                {
                    dimensions.Width = value * factor;
                }
                else if (prompt.ToLower().Contains("thick"))
                {
                    dimensions.Thickness = value * factor;
                }
            }

            // Height pattern: height of 3m, 3m height
            var heightPattern = @"(?:height\s+(?:of\s+)?|(\d+(?:\.\d+)?)\s*(\w+)?\s+height)(\d+(?:\.\d+)?)?\s*(\w+)?";
            var heightMatch = Regex.Match(prompt, heightPattern, RegexOptions.IgnoreCase);

            if (heightMatch.Success)
            {
                if (!string.IsNullOrEmpty(heightMatch.Groups[3].Value))
                {
                    double value = double.Parse(heightMatch.Groups[3].Value);
                    string unit = heightMatch.Groups[4].Value;
                    dimensions.Height = value * GetUnitFactor(unit);
                }
                else if (!string.IsNullOrEmpty(heightMatch.Groups[1].Value))
                {
                    double value = double.Parse(heightMatch.Groups[1].Value);
                    string unit = heightMatch.Groups[2].Value;
                    dimensions.Height = value * GetUnitFactor(unit);
                }
            }

            return dimensions;
        }

        private LocationInfo ExtractLocation(string prompt)
        {
            var location = new LocationInfo();
            var promptLower = prompt.ToLowerInvariant();

            // At coordinates
            var coordPattern = @"at\s*\(?\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*(?:,\s*(\d+(?:\.\d+)?))?\s*\)?";
            var coordMatch = Regex.Match(prompt, coordPattern);

            if (coordMatch.Success)
            {
                location.X = double.Parse(coordMatch.Groups[1].Value);
                location.Y = double.Parse(coordMatch.Groups[2].Value);
                if (!string.IsNullOrEmpty(coordMatch.Groups[3].Value))
                {
                    location.Z = double.Parse(coordMatch.Groups[3].Value);
                }
                location.IsAbsolute = true;
            }

            // Relative locations
            if (promptLower.Contains("next to") || promptLower.Contains("beside"))
            {
                location.RelationType = LocationRelation.Adjacent;
                location.ReferenceElement = ExtractReferenceElement(prompt, "next to|beside");
            }
            else if (promptLower.Contains("in front of"))
            {
                location.RelationType = LocationRelation.InFrontOf;
                location.ReferenceElement = ExtractReferenceElement(prompt, "in front of");
            }
            else if (promptLower.Contains("behind"))
            {
                location.RelationType = LocationRelation.Behind;
                location.ReferenceElement = ExtractReferenceElement(prompt, "behind");
            }
            else if (promptLower.Contains("above") || promptLower.Contains("over"))
            {
                location.RelationType = LocationRelation.Above;
                location.ReferenceElement = ExtractReferenceElement(prompt, "above|over");
            }
            else if (promptLower.Contains("below") || promptLower.Contains("under"))
            {
                location.RelationType = LocationRelation.Below;
                location.ReferenceElement = ExtractReferenceElement(prompt, "below|under");
            }
            else if (promptLower.Contains("inside") || promptLower.Contains("in the"))
            {
                location.RelationType = LocationRelation.Inside;
                location.ReferenceElement = ExtractReferenceElement(prompt, "inside|in the");
            }

            // Level reference
            var levelPattern = @"(?:on\s+)?level\s+(\d+|ground|first|second|third|basement)";
            var levelMatch = Regex.Match(prompt, levelPattern, RegexOptions.IgnoreCase);
            if (levelMatch.Success)
            {
                location.Level = ParseLevel(levelMatch.Groups[1].Value);
            }

            return location;
        }

        private string ExtractReferenceElement(string prompt, string preposition)
        {
            var pattern = $@"(?:{preposition})\s+(?:the\s+)?(\w+(?:\s+\w+)?)";
            var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private int ParseLevel(string levelStr)
        {
            return levelStr.ToLower() switch
            {
                "ground" => 0,
                "first" => 1,
                "second" => 2,
                "third" => 3,
                "basement" => -1,
                _ => int.TryParse(levelStr, out var num) ? num : 0
            };
        }

        private int ExtractQuantity(string prompt)
        {
            // "5 windows", "five doors"
            var numericPattern = @"(\d+)\s+(?:\w+)";
            var numericMatch = Regex.Match(prompt, numericPattern);
            if (numericMatch.Success)
            {
                return int.Parse(numericMatch.Groups[1].Value);
            }

            // Word numbers
            var wordNumbers = new Dictionary<string, int>
            {
                ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
                ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
                ["a"] = 1, ["an"] = 1
            };

            foreach (var kvp in wordNumbers)
            {
                if (prompt.ToLower().Contains($"{kvp.Key} "))
                {
                    return kvp.Value;
                }
            }

            return 1; // Default to 1
        }

        private Dictionary<string, string> ExtractProperties(string prompt)
        {
            var properties = new Dictionary<string, string>();
            var promptLower = prompt.ToLowerInvariant();

            // Fire rating
            var firePattern = @"(\d+)\s*(?:hour|hr)\s*fire\s*(?:rating|rated)?";
            var fireMatch = Regex.Match(prompt, firePattern, RegexOptions.IgnoreCase);
            if (fireMatch.Success)
            {
                properties["FireRating"] = $"{fireMatch.Groups[1].Value} Hour";
            }

            // Material
            var materials = new[] { "concrete", "steel", "wood", "glass", "brick", "masonry", "gypsum", "drywall", "aluminum" };
            foreach (var material in materials)
            {
                if (promptLower.Contains(material))
                {
                    properties["Material"] = material;
                    break;
                }
            }

            // Color
            var colors = new[] { "white", "black", "gray", "grey", "brown", "red", "blue", "green" };
            foreach (var color in colors)
            {
                if (promptLower.Contains(color))
                {
                    properties["Color"] = color;
                    break;
                }
            }

            // Door swing
            if (promptLower.Contains("left swing") || promptLower.Contains("left-hand"))
            {
                properties["SwingDirection"] = "Left";
            }
            else if (promptLower.Contains("right swing") || promptLower.Contains("right-hand"))
            {
                properties["SwingDirection"] = "Right";
            }
            else if (promptLower.Contains("double swing") || promptLower.Contains("double door"))
            {
                properties["SwingDirection"] = "Double";
            }

            // Window type
            if (promptLower.Contains("fixed"))
            {
                properties["WindowOperation"] = "Fixed";
            }
            else if (promptLower.Contains("casement"))
            {
                properties["WindowOperation"] = "Casement";
            }
            else if (promptLower.Contains("sliding"))
            {
                properties["WindowOperation"] = "Sliding";
            }

            return properties;
        }

        private List<RelationshipInfo> ExtractRelationships(string prompt)
        {
            var relationships = new List<RelationshipInfo>();
            var promptLower = prompt.ToLowerInvariant();

            // Connected to
            if (promptLower.Contains("connected to") || promptLower.Contains("connects to"))
            {
                var pattern = @"connect(?:ed|s)?\s+to\s+(?:the\s+)?(\w+)";
                var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    relationships.Add(new RelationshipInfo
                    {
                        Type = RelationType.ConnectedTo,
                        TargetElement = match.Groups[1].Value
                    });
                }
            }

            // Hosted by
            if (promptLower.Contains("in the wall") || promptLower.Contains("on the wall"))
            {
                relationships.Add(new RelationshipInfo
                {
                    Type = RelationType.HostedBy,
                    TargetElement = "Wall"
                });
            }

            return relationships;
        }

        private double GetUnitFactor(string unit)
        {
            if (string.IsNullOrEmpty(unit)) return 1000; // Default to meters
            return _unitConversions.TryGetValue(unit, out var conv) ? conv.Factor : 1000;
        }

        #endregion

        #region Intent Handlers

        private async Task<ExecutionResult> ExecuteIntentAsync(
            IntentResult intent,
            ExtractedEntities entities,
            ProcessingOptions options,
            CancellationToken cancellationToken)
        {
            return intent.Handler.Handler(intent, entities, options);
        }

        private ExecutionResult HandleCreateIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            var result = new ExecutionResult();

            if (string.IsNullOrEmpty(entities.ElementType))
            {
                result.Success = false;
                result.Message = "Could not determine what element to create.";
                return result;
            }

            // Create the element specification
            var spec = new ElementSpecification
            {
                ElementType = entities.ElementType,
                Dimensions = entities.Dimensions,
                Location = entities.Location,
                Properties = entities.Properties,
                Quantity = entities.Quantity
            };

            // Add ISO 19650 parameters
            spec.Properties[PARAM_CREATED_BY] = options.UserName ?? "StingBIM";
            spec.Properties[PARAM_CREATION_DATE] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            spec.Properties[PARAM_CREATION_METHOD] = "NLP-Prompt";

            // Would dispatch to actual creator here
            var createdElement = new CreatedElement
            {
                Id = Guid.NewGuid().ToString(),
                ElementType = spec.ElementType,
                Dimensions = spec.Dimensions,
                Location = spec.Location,
                Properties = spec.Properties
            };

            result.Success = true;
            result.CreatedElements = new List<CreatedElement> { createdElement };
            result.Message = $"Created {entities.ElementType}";

            if (entities.Quantity > 1)
            {
                for (int i = 1; i < entities.Quantity; i++)
                {
                    result.CreatedElements.Add(new CreatedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        ElementType = spec.ElementType,
                        Dimensions = spec.Dimensions,
                        Properties = spec.Properties
                    });
                }
                result.Message = $"Created {entities.Quantity} {entities.ElementType}s";
            }

            return result;
        }

        private ExecutionResult HandleModifyIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Modified {entities.ElementType ?? "element"}"
            };
        }

        private ExecutionResult HandleDeleteIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Deleted {entities.ElementType ?? "element"}"
            };
        }

        private ExecutionResult HandleCopyIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Copied {entities.ElementType ?? "element"}"
            };
        }

        private ExecutionResult HandleMoveIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Moved {entities.ElementType ?? "element"}"
            };
        }

        private ExecutionResult HandleArrayIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            var createdElements = new List<CreatedElement>();
            for (int i = 0; i < entities.Quantity; i++)
            {
                createdElements.Add(new CreatedElement
                {
                    Id = Guid.NewGuid().ToString(),
                    ElementType = entities.ElementType
                });
            }

            return new ExecutionResult
            {
                Success = true,
                CreatedElements = createdElements,
                Message = $"Created array of {entities.Quantity} {entities.ElementType}s"
            };
        }

        private ExecutionResult HandleQueryIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Query results for {entities.ElementType ?? "all elements"}"
            };
        }

        private ExecutionResult HandleLayoutIntent(IntentResult intent, ExtractedEntities entities, ProcessingOptions options)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = $"Generated layout for {entities.ElementType ?? "space"}"
            };
        }

        #endregion

        #region Helper Methods

        private string PreprocessPrompt(string prompt)
        {
            // Normalize whitespace
            prompt = Regex.Replace(prompt, @"\s+", " ").Trim();

            // Expand contractions
            prompt = prompt.Replace("don't", "do not")
                          .Replace("can't", "cannot")
                          .Replace("won't", "will not")
                          .Replace("I'd", "I would")
                          .Replace("I'll", "I will");

            return prompt;
        }

        private ValidationResult ValidateEntities(ExtractedEntities entities, ModelIntent intent)
        {
            var result = new ValidationResult { IsValid = true };

            if (intent == ModelIntent.Create && string.IsNullOrEmpty(entities.ElementType))
            {
                result.IsValid = false;
                result.MissingInfo.Add("ElementType");
            }

            return result;
        }

        private void ResolveContextReferences(ExtractedEntities entities)
        {
            lock (_lockObject)
            {
                if (_contextHistory.Count > 0)
                {
                    var lastContext = _contextHistory.Last();

                    // Resolve "it", "that", "the same"
                    if (string.IsNullOrEmpty(entities.ElementType) && lastContext.ElementType != null)
                    {
                        entities.ElementType = lastContext.ElementType;
                    }
                }
            }
        }

        private void UpdateContext(string prompt, IntentResult intent, ExtractedEntities entities, ExecutionResult result)
        {
            lock (_lockObject)
            {
                _contextHistory.Add(new ConversationContext
                {
                    Prompt = prompt,
                    Intent = intent.Intent,
                    ElementType = entities.ElementType,
                    CreatedElementIds = result.CreatedElements?.Select(e => e.Id).ToList(),
                    Timestamp = DateTime.Now
                });

                // Keep last 10 contexts
                while (_contextHistory.Count > 10)
                {
                    _contextHistory.RemoveAt(0);
                }
            }
        }

        private List<string> GenerateClarificationQuestions(List<string> missingInfo)
        {
            var questions = new List<string>();

            foreach (var info in missingInfo)
            {
                var question = info switch
                {
                    "ElementType" => "What type of element would you like to create? (e.g., wall, door, window)",
                    "Dimensions" => "What dimensions should the element have?",
                    "Location" => "Where should the element be placed?",
                    _ => $"Please specify the {info}."
                };
                questions.Add(question);
            }

            return questions;
        }

        private void OnModelCreated(OrchestratorResult result)
        {
            ModelCreated?.Invoke(this, new ModelCreationEventArgs { Result = result });
        }

        private void OnCreationFailed(OrchestratorResult result)
        {
            CreationFailed?.Invoke(this, new ModelCreationEventArgs { Result = result });
        }

        private void OnClarificationNeeded(ClarificationRequest request)
        {
            ClarificationNeeded?.Invoke(this, new ClarificationEventArgs { Request = request });
        }

        #endregion
    }

    #region Data Models

    public enum ModelIntent { Unknown, Create, Modify, Delete, Copy, Move, Array, Query, Layout }
    public enum LocationRelation { None, Adjacent, InFrontOf, Behind, Above, Below, Inside }
    public enum RelationType { ConnectedTo, HostedBy, Contains, Adjacent }

    public class IntentHandler
    {
        public ModelIntent Intent { get; set; }
        public string[] Patterns { get; set; }
        public Func<IntentResult, ExtractedEntities, ProcessingOptions, ExecutionResult> Handler { get; set; }
    }

    public class IntentResult
    {
        public ModelIntent Intent { get; set; }
        public IntentHandler Handler { get; set; }
        public Match Match { get; set; }
        public double Confidence { get; set; }
    }

    public class UnitConversion
    {
        public double Factor { get; set; }
        public string Unit { get; set; }
    }

    public class ExtractedEntities
    {
        public string ElementType { get; set; }
        public DimensionSet Dimensions { get; set; } = new DimensionSet();
        public LocationInfo Location { get; set; } = new LocationInfo();
        public int Quantity { get; set; } = 1;
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        public List<RelationshipInfo> Relationships { get; set; } = new List<RelationshipInfo>();
    }

    public class DimensionSet
    {
        public double? Length { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? Thickness { get; set; }
        public double? Diameter { get; set; }
    }

    public class LocationInfo
    {
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public bool IsAbsolute { get; set; }
        public LocationRelation RelationType { get; set; }
        public string ReferenceElement { get; set; }
        public int Level { get; set; }
    }

    public class RelationshipInfo
    {
        public RelationType Type { get; set; }
        public string TargetElement { get; set; }
    }

    public class ElementSpecification
    {
        public string ElementType { get; set; }
        public DimensionSet Dimensions { get; set; }
        public LocationInfo Location { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        public int Quantity { get; set; } = 1;
    }

    public class CreatedElement
    {
        public string Id { get; set; }
        public string ElementType { get; set; }
        public DimensionSet Dimensions { get; set; }
        public LocationInfo Location { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    public class ExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<CreatedElement> CreatedElements { get; set; } = new List<CreatedElement>();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> MissingInfo { get; set; } = new List<string>();
    }

    public class OrchestratorResult
    {
        public bool Success { get; set; }
        public string OriginalPrompt { get; set; }
        public ModelIntent DetectedIntent { get; set; }
        public ExtractedEntities ExtractedEntities { get; set; }
        public List<CreatedElement> CreatedElements { get; set; } = new List<CreatedElement>();
        public string Message { get; set; }
        public bool RequiresClarification { get; set; }
        public List<string> ClarificationQuestions { get; set; }
    }

    public class BatchResult
    {
        public List<OrchestratorResult> Results { get; set; }
        public int TotalPrompts { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class ProcessingOptions
    {
        public bool UseContext { get; set; } = true;
        public string UserName { get; set; }
        public string DefaultLevel { get; set; }
        public string DefaultUnit { get; set; } = "m";
    }

    public class BatchProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentPrompt { get; set; }
        public BatchProgress(int current, int total, string prompt)
        {
            Current = current;
            Total = total;
            CurrentPrompt = prompt;
        }
    }

    public class ConversationContext
    {
        public string Prompt { get; set; }
        public ModelIntent Intent { get; set; }
        public string ElementType { get; set; }
        public List<string> CreatedElementIds { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ClarificationRequest
    {
        public string OriginalPrompt { get; set; }
        public List<string> MissingInformation { get; set; }
        public List<string> SuggestedQuestions { get; set; }
    }

    public class ModelCreationEventArgs : EventArgs
    {
        public OrchestratorResult Result { get; set; }
    }

    public class ClarificationEventArgs : EventArgs
    {
        public ClarificationRequest Request { get; set; }
    }

    #endregion
}
