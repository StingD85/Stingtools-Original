// =========================================================================
// StingBIM.AI.NLP - Semantic Understanding Layer
// Deep natural language understanding for BIM domain
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Core.Models;

namespace StingBIM.AI.NLP.Semantic
{
    /// <summary>
    /// Advanced semantic understanding for BIM-specific natural language processing.
    /// Provides deep intent parsing, entity extraction, and contextual understanding.
    /// </summary>
    public class SemanticUnderstanding
    {
        private readonly Dictionary<string, SemanticFrame> _frames;
        private readonly Dictionary<string, List<string>> _synonyms;
        private readonly Dictionary<string, EntityDefinition> _entities;
        private readonly List<SemanticRule> _disambiguationRules;
        private readonly Dictionary<string, ConceptRelation> _conceptGraph;
        private readonly Dictionary<string, double> _termFrequency;

        // Embedding-based semantic understanding (T2-7)
        private EmbeddingModel _embeddingModel;
        private Func<string, long[]> _tokenizeFunc;
        private Func<long[], long[]> _attentionFunc;
        private Dictionary<string, float[]> _frameEmbeddings;
        private Dictionary<string, float[]> _conceptEmbeddings;
        private bool _embeddingsInitialized;

        public SemanticUnderstanding()
        {
            _frames = new Dictionary<string, SemanticFrame>();
            _synonyms = new Dictionary<string, List<string>>();
            _entities = new Dictionary<string, EntityDefinition>();
            _disambiguationRules = new List<SemanticRule>();
            _conceptGraph = new Dictionary<string, ConceptRelation>();
            _termFrequency = new Dictionary<string, double>();

            InitializeSemanticFrames();
            InitializeSynonyms();
            InitializeEntities();
            InitializeDisambiguationRules();
            InitializeConceptGraph();
        }

        /// <summary>
        /// Configures the embedding model for neural semantic understanding.
        /// Once configured and initialized, Analyze/AnalyzeAsync use embedding similarity
        /// for frame identification and confidence scoring.
        /// </summary>
        public void SetEmbeddingModel(EmbeddingModel model, Func<string, long[]> tokenize, Func<long[], long[]> createAttention)
        {
            _embeddingModel = model;
            _tokenizeFunc = tokenize;
            _attentionFunc = createAttention;
            _frameEmbeddings = new Dictionary<string, float[]>();
            _conceptEmbeddings = new Dictionary<string, float[]>();
            _embeddingsInitialized = false;
        }

        /// <summary>
        /// Pre-computes embeddings for all semantic frames, their trigger descriptions,
        /// and concept graph nodes. Must be called after SetEmbeddingModel.
        /// </summary>
        public async Task InitializeEmbeddingsAsync(CancellationToken cancellationToken = default)
        {
            if (_embeddingModel == null || _tokenizeFunc == null)
                return;

            _frameEmbeddings = new Dictionary<string, float[]>();
            _conceptEmbeddings = new Dictionary<string, float[]>();

            // Compute frame embeddings from description + representative phrases
            var frameTexts = new Dictionary<string, List<string>>
            {
                ["Creation"] = new List<string>
                {
                    "create a new building element",
                    "add a wall door window floor",
                    "place an element in the model",
                    "build construct make draw insert"
                },
                ["Modification"] = new List<string>
                {
                    "change modify update existing element",
                    "make it taller shorter wider",
                    "adjust the size height width",
                    "edit properties of the element"
                },
                ["Query"] = new List<string>
                {
                    "what is the area height dimension",
                    "how many elements are there",
                    "show me list count find",
                    "tell me about this element"
                },
                ["Analysis"] = new List<string>
                {
                    "check compliance validation code",
                    "analyze verify review assess",
                    "run analysis on the model",
                    "is this compliant with building code"
                },
                ["Navigation"] = new List<string>
                {
                    "go to show zoom navigate view",
                    "switch to floor plan section",
                    "open level 2 view",
                    "take me to the elevation"
                },
                ["Scheduling"] = new List<string>
                {
                    "create generate schedule quantity",
                    "make a door window room schedule",
                    "produce takeoff bill report",
                    "list all elements in a table"
                },
                ["Compliance"] = new List<string>
                {
                    "check building code fire safety",
                    "verify against standards regulations",
                    "is this up to code compliant",
                    "validate egress accessibility structural"
                }
            };

            foreach (var (frameId, texts) in frameTexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var embeddings = new List<float[]>();
                foreach (var text in texts)
                {
                    var tokens = _tokenizeFunc(text);
                    var attention = _attentionFunc(tokens);
                    var emb = await _embeddingModel.GetEmbeddingAsync(tokens, attention, cancellationToken);
                    embeddings.Add(emb);
                }
                _frameEmbeddings[frameId] = AverageAndNormalizeEmbeddings(embeddings);
            }

            // Compute concept embeddings for key BIM terms
            var concepts = new[] { "Wall", "Door", "Window", "Floor", "Ceiling", "Roof",
                "Column", "Beam", "Stair", "Room", "Concrete", "Steel", "Timber",
                "HVAC", "Plumbing", "Electrical", "Fire", "Foundation" };

            foreach (var concept in concepts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tokens = _tokenizeFunc(concept.ToLower());
                var attention = _attentionFunc(tokens);
                _conceptEmbeddings[concept] = await _embeddingModel.GetEmbeddingAsync(tokens, attention, cancellationToken);
            }

            _embeddingsInitialized = true;
        }

        private float[] AverageAndNormalizeEmbeddings(List<float[]> embeddings)
        {
            if (embeddings.Count == 0) return Array.Empty<float>();
            var dim = embeddings[0].Length;
            var result = new float[dim];
            foreach (var emb in embeddings)
            {
                for (int i = 0; i < dim; i++) result[i] += emb[i];
            }
            for (int i = 0; i < dim; i++) result[i] /= embeddings.Count;
            var norm = (float)Math.Sqrt(result.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < dim; i++) result[i] /= norm;
            }
            return result;
        }

        /// <summary>
        /// Async semantic analysis using embedding similarity for frame identification.
        /// Falls back to rule-based analysis if embeddings are not initialized.
        /// </summary>
        public async Task<SemanticAnalysis> AnalyzeAsync(string input, AnalysisContext context = null,
            CancellationToken cancellationToken = default)
        {
            context ??= new AnalysisContext();

            var analysis = new SemanticAnalysis
            {
                OriginalInput = input,
                NormalizedInput = NormalizeInput(input),
                Timestamp = DateTime.UtcNow
            };

            // Extract entities (rule-based, works synchronously)
            analysis.Entities = ExtractEntities(input, context);

            // Identify frame using embedding similarity when available
            if (_embeddingsInitialized && _embeddingModel != null)
            {
                analysis.Frame = await IdentifyFrameByEmbeddingAsync(input, analysis.Entities, cancellationToken);
            }
            else
            {
                analysis.Frame = IdentifyFrame(input, analysis.Entities);
            }

            // Fill frame roles
            analysis.FilledRoles = FillFrameRoles(analysis.Frame, analysis.Entities, input);

            // Resolve ambiguities
            analysis.Disambiguations = ResolveAmbiguities(input, context);

            // Extract relationships - enhanced with embeddings when available
            analysis.Relationships = _embeddingsInitialized
                ? await ExtractRelationshipsByEmbeddingAsync(analysis.Entities, cancellationToken)
                : ExtractRelationships(analysis.Entities);

            // Calculate confidence with embedding boost
            analysis.Confidence = CalculateConfidence(analysis);
            if (_embeddingsInitialized && analysis.Frame != null)
            {
                analysis.Confidence = Math.Min(1.0, analysis.Confidence + 0.1);
            }

            analysis.Interpretation = GenerateInterpretation(analysis);
            return analysis;
        }

        /// <summary>
        /// Identifies the best matching semantic frame using cosine similarity with pre-computed frame embeddings.
        /// </summary>
        private async Task<SemanticFrame> IdentifyFrameByEmbeddingAsync(string input, List<SemanticEntity> entities,
            CancellationToken cancellationToken)
        {
            var tokens = _tokenizeFunc(input);
            var attention = _attentionFunc(tokens);
            var inputEmbedding = await _embeddingModel.GetEmbeddingAsync(tokens, attention, cancellationToken);

            var frameScores = new Dictionary<string, double>();

            foreach (var (frameId, frameEmbedding) in _frameEmbeddings)
            {
                var similarity = EmbeddingModel.CosineSimilarity(inputEmbedding, frameEmbedding);

                // Combine embedding similarity (70%) with rule-based score (30%)
                double ruleScore = 0;
                if (_frames.TryGetValue(frameId, out var frame))
                {
                    foreach (var pattern in frame.TriggerPatterns)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            ruleScore = 0.5;
                            break;
                        }
                    }
                    var entityTypes = entities.Select(e => e.EntityType).ToHashSet();
                    var coreRoleMatch = frame.CoreRoles.Count(r =>
                        entityTypes.Any(e => IsRoleCompatible(r, e)));
                    ruleScore += coreRoleMatch * 0.3 / frame.CoreRoles.Length;
                }

                frameScores[frameId] = (similarity * 0.7) + (ruleScore * 0.3);
            }

            var bestFrame = frameScores.OrderByDescending(f => f.Value).FirstOrDefault();
            return bestFrame.Value > 0.25 && _frames.ContainsKey(bestFrame.Key)
                ? _frames[bestFrame.Key]
                : null;
        }

        /// <summary>
        /// Uses embedding similarity to infer relationships between extracted entities.
        /// Supplements rule-based relationship extraction with semantic proximity.
        /// </summary>
        private async Task<List<SemanticRelationship>> ExtractRelationshipsByEmbeddingAsync(
            List<SemanticEntity> entities, CancellationToken cancellationToken)
        {
            var relationships = ExtractRelationships(entities);

            // Add embedding-based relationships for entity pairs not already connected
            for (int i = 0; i < entities.Count - 1; i++)
            {
                for (int j = i + 1; j < entities.Count; j++)
                {
                    if (relationships.Any(r =>
                        r.Subject == entities[i].NormalizedValue && r.Object == entities[j].NormalizedValue))
                        continue;

                    var e1Name = entities[i].NormalizedValue;
                    var e2Name = entities[j].NormalizedValue;

                    float[] emb1, emb2;
                    if (!_conceptEmbeddings.TryGetValue(e1Name, out emb1))
                    {
                        var t1 = _tokenizeFunc(e1Name);
                        emb1 = await _embeddingModel.GetEmbeddingAsync(t1, _attentionFunc(t1), cancellationToken);
                    }
                    if (!_conceptEmbeddings.TryGetValue(e2Name, out emb2))
                    {
                        var t2 = _tokenizeFunc(e2Name);
                        emb2 = await _embeddingModel.GetEmbeddingAsync(t2, _attentionFunc(t2), cancellationToken);
                    }

                    var similarity = EmbeddingModel.CosineSimilarity(emb1, emb2);
                    if (similarity > 0.6)
                    {
                        relationships.Add(new SemanticRelationship
                        {
                            Subject = e1Name,
                            Relation = "semantically_related",
                            Object = e2Name,
                            Confidence = similarity
                        });
                    }
                }
            }

            return relationships.OrderByDescending(r => r.Confidence).ToList();
        }

        #region Initialization

        private void InitializeSemanticFrames()
        {
            // Creation frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Creation",
                Description = "Creating new elements in the model",
                CoreRoles = new[] { "ElementType", "Location" },
                OptionalRoles = new[] { "Dimensions", "Material", "Level", "Properties", "Style" },
                TriggerPatterns = new[]
                {
                    @"(?i)(create|add|place|insert|draw|make|build|construct)\s+",
                    @"(?i)I\s+(want|need|would like)\s+(a|an|to create|to add)\s+",
                    @"(?i)can you (create|add|make|put)\s+"
                },
                Constraints = new Dictionary<string, string>
                {
                    ["ElementType"] = "Must be a valid Revit element type",
                    ["Location"] = "Must be within model bounds"
                }
            });

            // Modification frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Modification",
                Description = "Modifying existing elements",
                CoreRoles = new[] { "Target", "Property", "NewValue" },
                OptionalRoles = new[] { "OldValue", "Scope", "Conditions" },
                TriggerPatterns = new[]
                {
                    @"(?i)(change|modify|update|set|adjust|alter|edit)\s+",
                    @"(?i)make\s+(the|this|it)\s+\w+\s+(larger|smaller|taller|shorter|wider)",
                    @"(?i)(increase|decrease|raise|lower)\s+(the\s+)?\w+"
                },
                Constraints = new Dictionary<string, string>
                {
                    ["Target"] = "Element must exist and be modifiable"
                }
            });

            // Query frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Query",
                Description = "Requesting information about the model",
                CoreRoles = new[] { "QueryType", "Subject" },
                OptionalRoles = new[] { "Filters", "Format", "Scope" },
                TriggerPatterns = new[]
                {
                    @"(?i)(what|which|where|how many|how much|show me|list|find|get)\s+",
                    @"(?i)(tell me|I want to know|can you show)\s+",
                    @"(?i)is\s+(there|the|this)\s+\w+\s*\?"
                },
                Constraints = new Dictionary<string, string>()
            });

            // Analysis frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Analysis",
                Description = "Performing analysis on the model",
                CoreRoles = new[] { "AnalysisType", "Scope" },
                OptionalRoles = new[] { "Parameters", "OutputFormat", "Criteria" },
                TriggerPatterns = new[]
                {
                    @"(?i)(analyze|check|validate|verify|review|assess|evaluate)\s+",
                    @"(?i)run\s+(a|an|the)?\s*(analysis|check|validation)",
                    @"(?i)is\s+(this|the|it)\s+(compliant|valid|correct)"
                },
                Constraints = new Dictionary<string, string>()
            });

            // Navigation frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Navigation",
                Description = "Navigating the model view",
                CoreRoles = new[] { "Target" },
                OptionalRoles = new[] { "ViewType", "ZoomLevel", "Section" },
                TriggerPatterns = new[]
                {
                    @"(?i)(go to|show|zoom|navigate|jump|open|switch)\s+",
                    @"(?i)(take me|bring me|let me see)\s+(to\s+)?",
                    @"(?i)view\s+(the\s+)?"
                },
                Constraints = new Dictionary<string, string>()
            });

            // Scheduling frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Scheduling",
                Description = "Creating and managing schedules",
                CoreRoles = new[] { "ScheduleType" },
                OptionalRoles = new[] { "Fields", "Filters", "Grouping", "Sorting", "Format" },
                TriggerPatterns = new[]
                {
                    @"(?i)(create|generate|make|build)\s+(a\s+)?(schedule|quantity|takeoff)",
                    @"(?i)schedule\s+(all|the)\s+\w+",
                    @"(?i)(list|tabulate|count)\s+(all\s+)?\w+"
                },
                Constraints = new Dictionary<string, string>()
            });

            // Compliance frame
            AddFrame(new SemanticFrame
            {
                FrameId = "Compliance",
                Description = "Checking code compliance",
                CoreRoles = new[] { "Standard", "Scope" },
                OptionalRoles = new[] { "Criteria", "Exceptions", "Report" },
                TriggerPatterns = new[]
                {
                    @"(?i)check\s+(for\s+)?(compliance|code|standard)",
                    @"(?i)is\s+(this|it)\s+(compliant|up to code|meeting)",
                    @"(?i)(verify|validate)\s+(against|with)\s+"
                },
                Constraints = new Dictionary<string, string>()
            });
        }

        private void InitializeSynonyms()
        {
            // Element type synonyms
            _synonyms["wall"] = new List<string> { "partition", "divider", "barrier" };
            _synonyms["door"] = new List<string> { "entrance", "entry", "opening", "doorway" };
            _synonyms["window"] = new List<string> { "glazing", "fenestration", "opening" };
            _synonyms["room"] = new List<string> { "space", "area", "chamber", "enclosure" };
            _synonyms["floor"] = new List<string> { "slab", "deck", "level", "storey" };
            _synonyms["ceiling"] = new List<string> { "soffit", "overhead" };
            _synonyms["roof"] = new List<string> { "roofing", "top", "cover" };
            _synonyms["column"] = new List<string> { "pillar", "post", "pier", "support" };
            _synonyms["beam"] = new List<string> { "joist", "girder", "lintel", "header" };
            _synonyms["stair"] = new List<string> { "staircase", "steps", "stairway" };

            // Action synonyms
            _synonyms["create"] = new List<string> { "add", "place", "insert", "make", "build", "draw", "put" };
            _synonyms["delete"] = new List<string> { "remove", "erase", "clear", "eliminate" };
            _synonyms["modify"] = new List<string> { "change", "edit", "update", "alter", "adjust" };
            _synonyms["move"] = new List<string> { "relocate", "shift", "reposition", "transfer" };
            _synonyms["copy"] = new List<string> { "duplicate", "clone", "replicate" };
            _synonyms["select"] = new List<string> { "pick", "choose", "highlight" };

            // Property synonyms
            _synonyms["height"] = new List<string> { "elevation", "altitude", "vertical" };
            _synonyms["width"] = new List<string> { "breadth", "span" };
            _synonyms["length"] = new List<string> { "extent", "dimension" };
            _synonyms["thickness"] = new List<string> { "depth", "gauge" };
            _synonyms["material"] = new List<string> { "finish", "substance", "composition" };

            // Location synonyms
            _synonyms["here"] = new List<string> { "this location", "this spot", "this place" };
            _synonyms["there"] = new List<string> { "that location", "that spot", "that place" };
            _synonyms["between"] = new List<string> { "among", "amid", "in the middle" };
            _synonyms["next to"] = new List<string> { "beside", "adjacent to", "alongside", "near" };
        }

        private void InitializeEntities()
        {
            // Element entities
            AddEntity(new EntityDefinition
            {
                EntityId = "ElementType",
                Category = EntityCategory.Element,
                Patterns = new[]
                {
                    @"(?i)\b(wall|door|window|floor|ceiling|roof|column|beam|stair|room|space|furniture)\b",
                    @"(?i)\b(curtain\s*wall|structural\s*column|generic\s*model)\b"
                },
                Normalizer = NormalizeElementType,
                Validators = new Func<string, bool>[] { IsValidElementType }
            });

            // Dimension entities
            AddEntity(new EntityDefinition
            {
                EntityId = "Dimension",
                Category = EntityCategory.Measurement,
                Patterns = new[]
                {
                    @"(\d+(?:\.\d+)?)\s*(m|mm|cm|ft|in|'|""|meters?|feet|inches?|millimeters?|centimeters?)",
                    @"(\d+)\s*(?:foot|feet)\s*(\d+)?\s*(?:inch(?:es)?)?",
                    @"(\d+)\s*[x×]\s*(\d+)(?:\s*[x×]\s*(\d+))?"
                },
                Normalizer = NormalizeDimension,
                Validators = new Func<string, bool>[] { IsValidDimension }
            });

            // Level entities
            AddEntity(new EntityDefinition
            {
                EntityId = "Level",
                Category = EntityCategory.Spatial,
                Patterns = new[]
                {
                    @"(?i)\b(level|floor|storey|story)\s*(\d+|one|two|three|four|five|ground|basement|roof)",
                    @"(?i)\b(first|second|third|fourth|fifth|ground|top|bottom)\s*(floor|level)",
                    @"(?i)\bL(\d+)\b"
                },
                Normalizer = NormalizeLevel,
                Validators = new Func<string, bool>[] { IsValidLevel }
            });

            // Material entities
            AddEntity(new EntityDefinition
            {
                EntityId = "Material",
                Category = EntityCategory.Material,
                Patterns = new[]
                {
                    @"(?i)\b(concrete|steel|timber|wood|brick|glass|aluminum|stone|marble|granite)\b",
                    @"(?i)\b(painted|stained|polished|brushed|matt|glossy)\s+\w+",
                    @"(?i)\bRAL\s*\d{4}\b"
                },
                Normalizer = NormalizeMaterial,
                Validators = new Func<string, bool>[] { IsValidMaterial }
            });

            // Direction entities
            AddEntity(new EntityDefinition
            {
                EntityId = "Direction",
                Category = EntityCategory.Spatial,
                Patterns = new[]
                {
                    @"(?i)\b(north|south|east|west|up|down|left|right|forward|backward)\b",
                    @"(?i)\b(horizontal|vertical|diagonal|perpendicular|parallel)\b",
                    @"(?i)\b(clockwise|counter-?clockwise|cw|ccw)\b"
                },
                Normalizer = NormalizeDirection,
                Validators = new Func<string, bool>[] { _ => true }
            });

            // Reference entities
            AddEntity(new EntityDefinition
            {
                EntityId = "Reference",
                Category = EntityCategory.Reference,
                Patterns = new[]
                {
                    @"(?i)\b(this|that|these|those|the|selected|current|last|previous)\s+\w+",
                    @"(?i)\b(it|them|here|there)\b",
                    @"(?i)\bthe\s+(same|other|adjacent|nearby)\s+\w+"
                },
                Normalizer = NormalizeReference,
                Validators = new Func<string, bool>[] { _ => true }
            });
        }

        private void InitializeDisambiguationRules()
        {
            // Window vs window opening
            _disambiguationRules.Add(new SemanticRule
            {
                RuleId = "WindowDisambiguation",
                Condition = ctx => ctx.ContainsWord("window") && ctx.ContainsWord("opening"),
                Resolution = ctx =>
                {
                    if (ctx.ContainsWord("create") || ctx.ContainsWord("add"))
                        return "WindowElement";
                    if (ctx.ContainsWord("cut") || ctx.ContainsWord("in wall"))
                        return "WallOpening";
                    return "WindowElement";
                }
            });

            // Level as floor vs level line
            _disambiguationRules.Add(new SemanticRule
            {
                RuleId = "LevelDisambiguation",
                Condition = ctx => ctx.ContainsWord("level"),
                Resolution = ctx =>
                {
                    if (ctx.ContainsPhrase("create level") || ctx.ContainsPhrase("add level"))
                        return "LevelDatum";
                    if (ctx.ContainsPhrase("on level") || ctx.ContainsPhrase("at level"))
                        return "FloorLevel";
                    return "FloorLevel";
                }
            });

            // Area as room vs area calculation
            _disambiguationRules.Add(new SemanticRule
            {
                RuleId = "AreaDisambiguation",
                Condition = ctx => ctx.ContainsWord("area"),
                Resolution = ctx =>
                {
                    if (ctx.ContainsWord("calculate") || ctx.ContainsWord("measure") || ctx.ContainsWord("what"))
                        return "AreaMeasurement";
                    if (ctx.ContainsWord("create") || ctx.ContainsWord("define"))
                        return "AreaElement";
                    return "AreaMeasurement";
                }
            });

            // Column as structural vs schedule column
            _disambiguationRules.Add(new SemanticRule
            {
                RuleId = "ColumnDisambiguation",
                Condition = ctx => ctx.ContainsWord("column"),
                Resolution = ctx =>
                {
                    if (ctx.ContainsWord("schedule") || ctx.ContainsWord("table") || ctx.ContainsWord("add column"))
                        return "ScheduleColumn";
                    return "StructuralColumn";
                }
            });
        }

        private void InitializeConceptGraph()
        {
            // Spatial relationships
            AddConceptRelation("Wall", "contains", new[] { "Door", "Window" });
            AddConceptRelation("Room", "bounded_by", new[] { "Wall", "Floor", "Ceiling" });
            AddConceptRelation("Floor", "supports", new[] { "Wall", "Column", "Furniture" });
            AddConceptRelation("Level", "contains", new[] { "Room", "Floor" });

            // Functional relationships
            AddConceptRelation("Door", "provides_access", new[] { "Room" });
            AddConceptRelation("Window", "provides_light", new[] { "Room" });
            AddConceptRelation("Stair", "connects", new[] { "Level" });

            // Structural relationships
            AddConceptRelation("Column", "supports", new[] { "Beam", "Floor" });
            AddConceptRelation("Beam", "supports", new[] { "Floor", "Roof" });
            AddConceptRelation("Foundation", "supports", new[] { "Wall", "Column" });

            // Material relationships
            AddConceptRelation("Concrete", "used_in", new[] { "Foundation", "Column", "Beam", "Floor" });
            AddConceptRelation("Steel", "used_in", new[] { "Column", "Beam", "Connection" });
            AddConceptRelation("Timber", "used_in", new[] { "Wall", "Floor", "Roof", "Door" });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Perform deep semantic analysis of user input.
        /// </summary>
        public SemanticAnalysis Analyze(string input, AnalysisContext context = null)
        {
            context ??= new AnalysisContext();

            var analysis = new SemanticAnalysis
            {
                OriginalInput = input,
                NormalizedInput = NormalizeInput(input),
                Timestamp = DateTime.UtcNow
            };

            // Extract entities
            analysis.Entities = ExtractEntities(input, context);

            // Identify semantic frame
            analysis.Frame = IdentifyFrame(input, analysis.Entities);

            // Fill frame roles
            analysis.FilledRoles = FillFrameRoles(analysis.Frame, analysis.Entities, input);

            // Resolve ambiguities
            analysis.Disambiguations = ResolveAmbiguities(input, context);

            // Extract relationships
            analysis.Relationships = ExtractRelationships(analysis.Entities);

            // Calculate confidence
            analysis.Confidence = CalculateConfidence(analysis);

            // Generate interpretation
            analysis.Interpretation = GenerateInterpretation(analysis);

            return analysis;
        }

        /// <summary>
        /// Resolve coreferences in the input.
        /// </summary>
        public CoreferenceResolution ResolveCoreferences(string input, SemanticConversationContext context)
        {
            var resolution = new CoreferenceResolution
            {
                OriginalInput = input
            };

            var referencePatterns = new[]
            {
                (@"(?i)\b(it|this|that)\b", ReferenceType.Singular),
                (@"(?i)\b(them|these|those)\b", ReferenceType.Plural),
                (@"(?i)\bthe\s+(same|other)\s+(\w+)\b", ReferenceType.Comparative),
                (@"(?i)\b(here|there)\b", ReferenceType.Locative)
            };

            foreach (var (pattern, refType) in referencePatterns)
            {
                var matches = Regex.Matches(input, pattern);
                foreach (Match match in matches)
                {
                    var antecedent = FindAntecedent(match.Value, refType, context);
                    if (antecedent != null)
                    {
                        resolution.Resolutions.Add(new CoreferenceLink
                        {
                            Pronoun = match.Value,
                            Position = match.Index,
                            ReferenceType = refType,
                            Antecedent = antecedent
                        });
                    }
                }
            }

            // Generate resolved text
            resolution.ResolvedInput = ApplyResolutions(input, resolution.Resolutions);

            return resolution;
        }

        /// <summary>
        /// Extract semantic slots from input.
        /// </summary>
        public SlotFilling ExtractSlots(string input, string expectedFrame)
        {
            var slotFilling = new SlotFilling
            {
                Input = input,
                FrameId = expectedFrame
            };

            if (!_frames.TryGetValue(expectedFrame, out var frame))
            {
                slotFilling.Success = false;
                slotFilling.MissingSlots = new List<string> { "Unknown frame" };
                return slotFilling;
            }

            var entities = ExtractEntities(input, new AnalysisContext());
            var roles = FillFrameRoles(frame, entities, input);

            slotFilling.FilledSlots = roles;
            slotFilling.MissingSlots = frame.CoreRoles
                .Where(r => !roles.ContainsKey(r) || string.IsNullOrEmpty(roles[r]?.ToString()))
                .ToList();

            slotFilling.OptionalSlots = frame.OptionalRoles
                .Where(r => !roles.ContainsKey(r))
                .ToList();

            slotFilling.Success = !slotFilling.MissingSlots.Any();

            // Generate clarification questions for missing slots
            if (!slotFilling.Success)
            {
                slotFilling.ClarificationQuestions = GenerateClarificationQuestions(
                    frame, slotFilling.MissingSlots);
            }

            return slotFilling;
        }

        /// <summary>
        /// Get related concepts for a given term.
        /// </summary>
        public List<RelatedConcept> GetRelatedConcepts(string concept)
        {
            var related = new List<RelatedConcept>();

            if (_conceptGraph.TryGetValue(concept, out var relations))
            {
                foreach (var relation in relations.Relations)
                {
                    foreach (var target in relation.Value)
                    {
                        related.Add(new RelatedConcept
                        {
                            Concept = target,
                            Relation = relation.Key,
                            Strength = 1.0
                        });
                    }
                }
            }

            // Add synonym relationships
            if (_synonyms.TryGetValue(concept.ToLower(), out var synonymList))
            {
                foreach (var syn in synonymList)
                {
                    related.Add(new RelatedConcept
                    {
                        Concept = syn,
                        Relation = "synonym",
                        Strength = 0.9
                    });
                }
            }

            // Find reverse relationships
            foreach (var entry in _conceptGraph)
            {
                foreach (var rel in entry.Value.Relations)
                {
                    if (rel.Value.Contains(concept))
                    {
                        related.Add(new RelatedConcept
                        {
                            Concept = entry.Key,
                            Relation = $"inverse_{rel.Key}",
                            Strength = 0.8
                        });
                    }
                }
            }

            return related.OrderByDescending(r => r.Strength).ToList();
        }

        /// <summary>
        /// Expand a query with semantic understanding.
        /// </summary>
        public QueryExpansion ExpandQuery(string query)
        {
            var expansion = new QueryExpansion
            {
                OriginalQuery = query,
                ExpandedTerms = new List<ExpandedTerm>()
            };

            var words = Tokenize(query);

            foreach (var word in words)
            {
                var expandedTerm = new ExpandedTerm
                {
                    Original = word,
                    Synonyms = new List<string>(),
                    RelatedConcepts = new List<string>()
                };

                // Add synonyms
                var normalizedWord = word.ToLower();
                if (_synonyms.TryGetValue(normalizedWord, out var syns))
                {
                    expandedTerm.Synonyms.AddRange(syns);
                }

                // Add related concepts
                var related = GetRelatedConcepts(word);
                expandedTerm.RelatedConcepts.AddRange(
                    related.Where(r => r.Strength > 0.7).Select(r => r.Concept));

                if (expandedTerm.Synonyms.Any() || expandedTerm.RelatedConcepts.Any())
                {
                    expansion.ExpandedTerms.Add(expandedTerm);
                }
            }

            // Generate expanded query
            expansion.ExpandedQuery = GenerateExpandedQuery(query, expansion.ExpandedTerms);

            return expansion;
        }

        #endregion

        #region Private Methods

        private void AddFrame(SemanticFrame frame)
        {
            _frames[frame.FrameId] = frame;
        }

        private void AddEntity(EntityDefinition entity)
        {
            _entities[entity.EntityId] = entity;
        }

        private void AddConceptRelation(string concept, string relation, string[] targets)
        {
            if (!_conceptGraph.ContainsKey(concept))
            {
                _conceptGraph[concept] = new ConceptRelation
                {
                    Concept = concept,
                    Relations = new Dictionary<string, List<string>>()
                };
            }

            _conceptGraph[concept].Relations[relation] = targets.ToList();
        }

        private string NormalizeInput(string input)
        {
            // Basic normalization
            var normalized = input.Trim().ToLower();

            // Expand contractions
            normalized = normalized.Replace("don't", "do not");
            normalized = normalized.Replace("can't", "cannot");
            normalized = normalized.Replace("won't", "will not");
            normalized = normalized.Replace("i'm", "i am");
            normalized = normalized.Replace("it's", "it is");

            // Normalize whitespace
            normalized = Regex.Replace(normalized, @"\s+", " ");

            return normalized;
        }

        private List<SemanticEntity> ExtractEntities(string input, AnalysisContext context)
        {
            var entities = new List<SemanticEntity>();

            foreach (var entityDef in _entities.Values)
            {
                foreach (var pattern in entityDef.Patterns)
                {
                    var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var normalized = entityDef.Normalizer?.Invoke(match.Value) ?? match.Value;

                        var valid = entityDef.Validators?.All(v => v(normalized)) ?? true;
                        if (valid)
                        {
                            entities.Add(new SemanticEntity
                            {
                                EntityType = entityDef.EntityId,
                                Category = entityDef.Category,
                                RawValue = match.Value,
                                NormalizedValue = normalized,
                                Position = match.Index,
                                Confidence = 0.9
                            });
                        }
                    }
                }
            }

            // Remove overlapping entities, keeping highest confidence
            return RemoveOverlaps(entities);
        }

        private SemanticFrame IdentifyFrame(string input, List<SemanticEntity> entities)
        {
            var frameScores = new Dictionary<string, double>();

            foreach (var frame in _frames.Values)
            {
                double score = 0;

                // Check trigger patterns
                foreach (var pattern in frame.TriggerPatterns)
                {
                    if (Regex.IsMatch(input, pattern))
                    {
                        score += 0.5;
                        break;
                    }
                }

                // Check entity compatibility
                var entityTypes = entities.Select(e => e.EntityType).ToHashSet();
                var coreRoleMatch = frame.CoreRoles.Count(r =>
                    entityTypes.Any(e => IsRoleCompatible(r, e)));
                score += coreRoleMatch * 0.3 / frame.CoreRoles.Length;

                frameScores[frame.FrameId] = score;
            }

            var bestFrame = frameScores.OrderByDescending(f => f.Value).FirstOrDefault();
            return bestFrame.Value > 0.3 ? _frames[bestFrame.Key] : null;
        }

        private Dictionary<string, object> FillFrameRoles(
            SemanticFrame frame,
            List<SemanticEntity> entities,
            string input)
        {
            var roles = new Dictionary<string, object>();

            if (frame == null) return roles;

            foreach (var role in frame.CoreRoles.Concat(frame.OptionalRoles))
            {
                var matchingEntity = entities.FirstOrDefault(e => IsRoleCompatible(role, e.EntityType));
                if (matchingEntity != null)
                {
                    roles[role] = matchingEntity.NormalizedValue;
                }
                else
                {
                    // Try to infer from context
                    var inferred = InferRoleValue(role, input);
                    if (inferred != null)
                    {
                        roles[role] = inferred;
                    }
                }
            }

            return roles;
        }

        private bool IsRoleCompatible(string role, string entityType)
        {
            var compatibility = new Dictionary<string, string[]>
            {
                ["ElementType"] = new[] { "ElementType" },
                ["Target"] = new[] { "ElementType", "Reference" },
                ["Location"] = new[] { "Level", "Direction", "Reference" },
                ["Dimensions"] = new[] { "Dimension" },
                ["Material"] = new[] { "Material" },
                ["Level"] = new[] { "Level" },
                ["Property"] = new[] { "Dimension" },
                ["NewValue"] = new[] { "Dimension", "Material" },
                ["Subject"] = new[] { "ElementType", "Reference" },
                ["QueryType"] = new[] { "ElementType" },
                ["AnalysisType"] = new[] { "ElementType" },
                ["Direction"] = new[] { "Direction" },
                ["Standard"] = new[] { "Reference" },
                ["Scope"] = new[] { "Level", "Reference" },
                ["ScheduleType"] = new[] { "ElementType" }
            };

            if (compatibility.TryGetValue(role, out var types))
            {
                return types.Contains(entityType);
            }

            return false;
        }

        private object InferRoleValue(string role, string input)
        {
            // Simple rule-based inference
            switch (role)
            {
                case "QueryType":
                    if (input.Contains("how many", StringComparison.OrdinalIgnoreCase))
                        return "Count";
                    if (input.Contains("where", StringComparison.OrdinalIgnoreCase))
                        return "Location";
                    if (input.Contains("what", StringComparison.OrdinalIgnoreCase))
                        return "Property";
                    break;

                case "AnalysisType":
                    if (input.Contains("clash", StringComparison.OrdinalIgnoreCase))
                        return "ClashDetection";
                    if (input.Contains("comply", StringComparison.OrdinalIgnoreCase) ||
                        input.Contains("code", StringComparison.OrdinalIgnoreCase))
                        return "Compliance";
                    break;
            }

            return null;
        }

        private List<DisambiguationResult> ResolveAmbiguities(string input, AnalysisContext context)
        {
            var results = new List<DisambiguationResult>();
            var ctx = new DisambiguationContext(input);

            foreach (var rule in _disambiguationRules)
            {
                if (rule.Condition(ctx))
                {
                    var resolution = rule.Resolution(ctx);
                    results.Add(new DisambiguationResult
                    {
                        RuleId = rule.RuleId,
                        ResolvedMeaning = resolution,
                        Confidence = 0.85
                    });
                }
            }

            return results;
        }

        private List<SemanticRelationship> ExtractRelationships(List<SemanticEntity> entities)
        {
            var relationships = new List<SemanticRelationship>();

            for (int i = 0; i < entities.Count - 1; i++)
            {
                for (int j = i + 1; j < entities.Count; j++)
                {
                    var rel = InferRelationship(entities[i], entities[j]);
                    if (rel != null)
                    {
                        relationships.Add(rel);
                    }
                }
            }

            return relationships;
        }

        private SemanticRelationship InferRelationship(SemanticEntity e1, SemanticEntity e2)
        {
            // Check concept graph for known relationships
            if (_conceptGraph.TryGetValue(e1.NormalizedValue, out var relations))
            {
                foreach (var rel in relations.Relations)
                {
                    if (rel.Value.Any(v =>
                        v.Equals(e2.NormalizedValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new SemanticRelationship
                        {
                            Subject = e1.NormalizedValue,
                            Relation = rel.Key,
                            Object = e2.NormalizedValue,
                            Confidence = 0.8
                        };
                    }
                }
            }

            // Infer from position
            if (e1.Position < e2.Position && e1.Category == EntityCategory.Element &&
                e2.Category == EntityCategory.Measurement)
            {
                return new SemanticRelationship
                {
                    Subject = e1.NormalizedValue,
                    Relation = "has_dimension",
                    Object = e2.NormalizedValue,
                    Confidence = 0.6
                };
            }

            return null;
        }

        private double CalculateConfidence(SemanticAnalysis analysis)
        {
            double confidence = 0.5;

            // Frame identification confidence
            if (analysis.Frame != null)
                confidence += 0.2;

            // Entity extraction confidence
            if (analysis.Entities.Any())
                confidence += Math.Min(0.2, analysis.Entities.Count * 0.05);

            // Role filling confidence
            if (analysis.Frame != null && analysis.FilledRoles.Any())
            {
                var fillRate = (double)analysis.FilledRoles.Count / analysis.Frame.CoreRoles.Length;
                confidence += fillRate * 0.1;
            }

            return Math.Min(1.0, confidence);
        }

        private string GenerateInterpretation(SemanticAnalysis analysis)
        {
            if (analysis.Frame == null)
                return "Unable to determine intent from input";

            var parts = new List<string>
            {
                $"Intent: {analysis.Frame.FrameId}"
            };

            foreach (var role in analysis.FilledRoles)
            {
                parts.Add($"{role.Key}: {role.Value}");
            }

            if (analysis.MissingRoles.Any())
            {
                parts.Add($"Missing: {string.Join(", ", analysis.MissingRoles)}");
            }

            return string.Join("; ", parts);
        }

        private List<string> Tokenize(string input)
        {
            return Regex.Split(input, @"\W+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
        }

        private List<SemanticEntity> RemoveOverlaps(List<SemanticEntity> entities)
        {
            var sorted = entities.OrderBy(e => e.Position).ThenByDescending(e => e.Confidence).ToList();
            var result = new List<SemanticEntity>();

            foreach (var entity in sorted)
            {
                var overlaps = result.Any(e =>
                    (entity.Position >= e.Position &&
                     entity.Position < e.Position + e.RawValue.Length) ||
                    (e.Position >= entity.Position &&
                     e.Position < entity.Position + entity.RawValue.Length));

                if (!overlaps)
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        private string FindAntecedent(string pronoun, ReferenceType refType, SemanticConversationContext context)
        {
            if (context?.RecentEntities == null || !context.RecentEntities.Any())
                return null;

            switch (refType)
            {
                case ReferenceType.Singular:
                    return context.RecentEntities.LastOrDefault();
                case ReferenceType.Plural:
                    return string.Join(", ", context.RecentEntities.TakeLast(3));
                case ReferenceType.Locative:
                    return context.CurrentLocation;
                default:
                    return context.RecentEntities.LastOrDefault();
            }
        }

        private string ApplyResolutions(string input, List<CoreferenceLink> resolutions)
        {
            var result = input;

            foreach (var resolution in resolutions.OrderByDescending(r => r.Position))
            {
                if (resolution.Antecedent != null)
                {
                    result = result.Remove(resolution.Position, resolution.Pronoun.Length);
                    result = result.Insert(resolution.Position, resolution.Antecedent);
                }
            }

            return result;
        }

        private List<string> GenerateClarificationQuestions(SemanticFrame frame, List<string> missingSlots)
        {
            var questions = new List<string>();

            var questionTemplates = new Dictionary<string, string>
            {
                ["ElementType"] = "What type of element would you like to {action}?",
                ["Location"] = "Where would you like to place it?",
                ["Dimensions"] = "What size should it be?",
                ["Material"] = "What material should it be made of?",
                ["Level"] = "Which level should it be on?",
                ["Target"] = "Which element are you referring to?",
                ["Property"] = "Which property would you like to change?",
                ["NewValue"] = "What should the new value be?"
            };

            foreach (var slot in missingSlots)
            {
                if (questionTemplates.TryGetValue(slot, out var template))
                {
                    var action = frame.FrameId.ToLower();
                    questions.Add(template.Replace("{action}", action));
                }
                else
                {
                    questions.Add($"Please specify the {slot.ToLower()}.");
                }
            }

            return questions;
        }

        private string GenerateExpandedQuery(string query, List<ExpandedTerm> expansions)
        {
            var parts = new List<string> { query };

            foreach (var expansion in expansions)
            {
                if (expansion.Synonyms.Any())
                {
                    parts.Add($"({string.Join(" OR ", expansion.Synonyms)})");
                }
            }

            return string.Join(" ", parts);
        }

        // Normalizer functions
        private string NormalizeElementType(string value)
        {
            var normalized = value.ToLower().Trim();

            // Check synonyms
            foreach (var entry in _synonyms)
            {
                if (entry.Value.Contains(normalized))
                {
                    return char.ToUpper(entry.Key[0]) + entry.Key.Substring(1);
                }
            }

            return char.ToUpper(normalized[0]) + normalized.Substring(1);
        }

        private string NormalizeDimension(string value)
        {
            // Extract numeric value and unit
            var match = Regex.Match(value, @"(\d+(?:\.\d+)?)\s*(m|mm|cm|ft|in|'|""|meters?|feet|inches?)?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
                {
                    return value;
                }
                var unit = match.Groups[2].Value.ToLower();

                // Convert to millimeters as standard
                var mm = unit switch
                {
                    "m" or "meter" or "meters" => number * 1000,
                    "cm" or "centimeter" or "centimeters" => number * 10,
                    "ft" or "'" or "foot" or "feet" => number * 304.8,
                    "in" or "\"" or "inch" or "inches" => number * 25.4,
                    _ => number // Assume mm
                };

                return $"{mm}mm";
            }

            return value;
        }

        private string NormalizeLevel(string value)
        {
            var match = Regex.Match(value,
                @"(?i)(level|floor|storey|story|L)?\s*(\d+|ground|basement|one|two|three|four|five|roof)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var levelValue = match.Groups[2].Value.ToLower();
                var levelNumber = levelValue switch
                {
                    "ground" or "one" or "first" => 0,
                    "basement" => -1,
                    "two" or "second" => 1,
                    "three" or "third" => 2,
                    "four" or "fourth" => 3,
                    "five" or "fifth" => 4,
                    "roof" or "top" => 99,
                    _ when int.TryParse(levelValue, out var n) => n,
                    _ => 0
                };

                return $"Level {levelNumber}";
            }

            return value;
        }

        private string NormalizeMaterial(string value)
        {
            return char.ToUpper(value[0]) + value.Substring(1).ToLower();
        }

        private string NormalizeDirection(string value)
        {
            return value.ToLower() switch
            {
                "n" or "north" => "North",
                "s" or "south" => "South",
                "e" or "east" => "East",
                "w" or "west" => "West",
                "cw" or "clockwise" => "Clockwise",
                "ccw" or "counter-clockwise" or "counterclockwise" => "CounterClockwise",
                _ => char.ToUpper(value[0]) + value.Substring(1).ToLower()
            };
        }

        private string NormalizeReference(string value)
        {
            return value.ToLower();
        }

        // Validator functions
        private bool IsValidElementType(string value)
        {
            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Wall", "Door", "Window", "Floor", "Ceiling", "Roof", "Column", "Beam",
                "Stair", "Room", "Space", "Furniture", "CurtainWall", "GenericModel"
            };
            return validTypes.Contains(value);
        }

        private bool IsValidDimension(string value)
        {
            return Regex.IsMatch(value, @"\d+(\.\d+)?mm");
        }

        private bool IsValidLevel(string value)
        {
            return Regex.IsMatch(value, @"Level\s*-?\d+");
        }

        private bool IsValidMaterial(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        #endregion
    }

    #region Supporting Types

    public class SemanticFrame
    {
        public string FrameId { get; set; }
        public string Description { get; set; }
        public string[] CoreRoles { get; set; }
        public string[] OptionalRoles { get; set; }
        public string[] TriggerPatterns { get; set; }
        public Dictionary<string, string> Constraints { get; set; }
    }

    public class EntityDefinition
    {
        public string EntityId { get; set; }
        public EntityCategory Category { get; set; }
        public string[] Patterns { get; set; }
        public Func<string, string> Normalizer { get; set; }
        public Func<string, bool>[] Validators { get; set; }
    }

    public class SemanticRule
    {
        public string RuleId { get; set; }
        public Func<DisambiguationContext, bool> Condition { get; set; }
        public Func<DisambiguationContext, string> Resolution { get; set; }
    }

    public class ConceptRelation
    {
        public string Concept { get; set; }
        public Dictionary<string, List<string>> Relations { get; set; }
    }

    public class AnalysisContext
    {
        public List<string> RecentEntities { get; set; } = new();
        public string CurrentLevel { get; set; }
        public string LastCommand { get; set; }
    }

    public class SemanticAnalysis
    {
        public string OriginalInput { get; set; }
        public string NormalizedInput { get; set; }
        public DateTime Timestamp { get; set; }
        public SemanticFrame Frame { get; set; }
        public List<SemanticEntity> Entities { get; set; } = new();
        public Dictionary<string, object> FilledRoles { get; set; } = new();
        public List<string> MissingRoles => Frame?.CoreRoles
            .Where(r => !FilledRoles.ContainsKey(r)).ToList() ?? new List<string>();
        public List<DisambiguationResult> Disambiguations { get; set; } = new();
        public List<SemanticRelationship> Relationships { get; set; } = new();
        public double Confidence { get; set; }
        public string Interpretation { get; set; }
    }

    public class SemanticEntity
    {
        public string EntityType { get; set; }
        public EntityCategory Category { get; set; }
        public string RawValue { get; set; }
        public string NormalizedValue { get; set; }
        public int Position { get; set; }
        public double Confidence { get; set; }
    }

    public class DisambiguationContext
    {
        private readonly string _input;

        public DisambiguationContext(string input)
        {
            _input = input.ToLower();
        }

        public bool ContainsWord(string word) =>
            Regex.IsMatch(_input, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase);

        public bool ContainsPhrase(string phrase) =>
            _input.Contains(phrase, StringComparison.OrdinalIgnoreCase);
    }

    public class DisambiguationResult
    {
        public string RuleId { get; set; }
        public string ResolvedMeaning { get; set; }
        public double Confidence { get; set; }
    }

    public class SemanticRelationship
    {
        public string Subject { get; set; }
        public string Relation { get; set; }
        public string Object { get; set; }
        public double Confidence { get; set; }
    }

    public class SemanticConversationContext
    {
        public List<string> RecentEntities { get; set; } = new();
        public string CurrentLocation { get; set; }
        public string LastCommand { get; set; }
    }

    public class CoreferenceResolution
    {
        public string OriginalInput { get; set; }
        public string ResolvedInput { get; set; }
        public List<CoreferenceLink> Resolutions { get; set; } = new();
    }

    public class CoreferenceLink
    {
        public string Pronoun { get; set; }
        public int Position { get; set; }
        public ReferenceType ReferenceType { get; set; }
        public string Antecedent { get; set; }
    }

    public class SlotFilling
    {
        public string Input { get; set; }
        public string FrameId { get; set; }
        public Dictionary<string, object> FilledSlots { get; set; } = new();
        public List<string> MissingSlots { get; set; } = new();
        public List<string> OptionalSlots { get; set; } = new();
        public bool Success { get; set; }
        public List<string> ClarificationQuestions { get; set; } = new();
    }

    public class RelatedConcept
    {
        public string Concept { get; set; }
        public string Relation { get; set; }
        public double Strength { get; set; }
    }

    public class QueryExpansion
    {
        public string OriginalQuery { get; set; }
        public string ExpandedQuery { get; set; }
        public List<ExpandedTerm> ExpandedTerms { get; set; } = new();
    }

    public class ExpandedTerm
    {
        public string Original { get; set; }
        public List<string> Synonyms { get; set; } = new();
        public List<string> RelatedConcepts { get; set; } = new();
    }

    public enum EntityCategory
    {
        Element,
        Measurement,
        Spatial,
        Material,
        Reference,
        Action,
        Property
    }

    public enum ReferenceType
    {
        Singular,
        Plural,
        Comparative,
        Locative
    }

    #endregion
}
