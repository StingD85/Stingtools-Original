// StingBIM.AI.NLP.Pipeline.IntentClassifier
// Classifies user intent from natural language input
// Master Proposal Reference: Part 1.1 Language Understanding - Intent Classifier

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Models;

namespace StingBIM.AI.NLP.Pipeline
{
    /// <summary>
    /// Classifies user intents from natural language commands.
    /// Combines built-in BIM domain patterns with optional ML-based classification.
    /// Target: < 200ms command understanding (Part 5.2)
    /// </summary>
    public class IntentClassifier
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly EmbeddingModel _embeddingModel;
        private readonly Tokenizer _tokenizer;

        private Dictionary<string, IntentDefinition> _intents;
        private Dictionary<string, float[]> _intentEmbeddings;
        private List<IntentPattern> _externalPatterns;
        private List<IntentPattern> _builtInPatterns;
        private Dictionary<string, List<string>> _builtInIntentExamples;
        private bool _isLoaded;

        // Classification thresholds
        public float ConfidenceThreshold { get; set; } = 0.6f;
        public float AmbiguityThreshold { get; set; } = 0.15f;

        public IntentClassifier(EmbeddingModel embeddingModel = null, Tokenizer tokenizer = null)
        {
            _embeddingModel = embeddingModel;
            _tokenizer = tokenizer;
            _intents = new Dictionary<string, IntentDefinition>(StringComparer.OrdinalIgnoreCase);
            _intentEmbeddings = new Dictionary<string, float[]>();
            _externalPatterns = new List<IntentPattern>();
            _builtInPatterns = new List<IntentPattern>();
            _patterns = new List<IntentPattern>();

            InitializeBuiltInPatterns();
            InitializeBuiltInIntentExamples();
        }

        /// <summary>
        /// Loads intent definitions and patterns from external files.
        /// Optional — the classifier works with built-in patterns if not called.
        /// </summary>
        public async Task LoadAsync(string intentsPath, string patternsPath = null)
        {
            Logger.Info("Loading intent classifier...");

            try
            {
                if (File.Exists(intentsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(intentsPath));
                    var intents = JsonConvert.DeserializeObject<List<IntentDefinition>>(json);
                    _intents = intents.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(patternsPath) && File.Exists(patternsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(patternsPath));
                    _externalPatterns = JsonConvert.DeserializeObject<List<IntentPattern>>(json) ?? new List<IntentPattern>();
                }

                if (_embeddingModel != null && _tokenizer != null)
                {
                    await ComputeIntentEmbeddingsAsync();
                }

                _isLoaded = true;
                Logger.Info($"Intent classifier loaded: {_intents.Count} intents, {_externalPatterns.Count} external patterns");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load intent classifier");
                throw;
            }
        }

        /// <summary>
        /// Classifies the intent of a user command.
        /// Returns IntentResult compatible with ConversationManager and ContextTracker.
        /// Works with built-in BIM patterns even without LoadAsync.
        /// </summary>
        public async Task<IntentResult> ClassifyAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startTime = DateTime.Now;
            var normalizedText = text.ToLowerInvariant().Trim();

            // 1. Try built-in BIM patterns (always available, fast)
            var builtInMatch = TryPatternMatch(normalizedText, _builtInPatterns);

            // 1b. Try consulting/management patterns if registered
            if (_patterns.Count > 0)
            {
                var consultingMatch = TryPatternMatch(normalizedText, _patterns);
                if (consultingMatch != null && (builtInMatch == null || consultingMatch.Confidence > builtInMatch.Confidence))
                {
                    builtInMatch = consultingMatch;
                }
            }

            // 2. Try external patterns if loaded
            IntentClassificationResult externalMatch = null;
            if (_isLoaded && _externalPatterns.Count > 0)
            {
                externalMatch = TryPatternMatch(normalizedText, _externalPatterns);
            }

            // 3. Try semantic similarity if ML models available
            IntentClassificationResult semanticMatch = null;
            if (_embeddingModel != null && _tokenizer != null && _intentEmbeddings.Count > 0)
            {
                try
                {
                    semanticMatch = await ClassifyBySemanticSimilarityAsync(text);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Semantic classification failed, using pattern match only");
                }
            }

            // 4. Pick best result
            var best = SelectBestResult(builtInMatch, externalMatch, semanticMatch);

            // 5. Build alternatives list
            var alternatives = BuildAlternatives(builtInMatch, externalMatch, semanticMatch, best?.Intent);

            var result = new IntentResult
            {
                Intent = best?.Intent ?? "UNKNOWN",
                Confidence = best?.Confidence ?? 0.0f,
                Alternatives = alternatives,
                ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds
            };

            Logger.Debug($"Intent classified: {result.Intent} ({result.Confidence:P0}) in {result.ProcessingTimeMs:F0}ms");
            return result;
        }

        /// <summary>
        /// Registers 22 consulting and management intent patterns for domain-specific
        /// queries (structural, MEP, compliance, cost, sustainability, etc.) and
        /// management operations (analysis, optimization, validation, generative design).
        /// </summary>
        public void RegisterConsultingPatterns()
        {
            _patterns.Clear();

            // 12 Consulting patterns
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_STRUCTURAL", Pattern = @"\b(beam|column|structural|load|foundation|footing)\b.*\b(siz|design|check|calculat|analys)", Type = PatternType.Regex, Confidence = 0.90f, Priority = 12 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_MEP", Pattern = @"\b(duct|pipe|hvac|mep|mechanical|plumbing|electrical)\b.*\b(siz|design|rout|calculat)", Type = PatternType.Regex, Confidence = 0.90f, Priority = 12 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_COMPLIANCE", Pattern = @"\b(complian|code|regulation|standard|building\s*code)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_MATERIALS", Pattern = @"\b(material|recommend|suggest)\b.*\b(wall|floor|roof|facade|exterior|interior)", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_COST", Pattern = @"\b(cost|estimat|budget|pric|expense)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_SUSTAINABILITY", Pattern = @"\b(sustainab|leed|green|breeam|well\s*certif|carbon\s*footprint)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_FIRE_SAFETY", Pattern = @"\b(fire|flame|smoke|fire\s*rat|egress|sprinkler)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_ACCESSIBILITY", Pattern = @"\b(accessib|ada|disab|wheelchair|ramp|universal\s*design)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_ENERGY", Pattern = @"\b(energy|thermal|insulation|u-value|r-value|efficien)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_ACOUSTICS", Pattern = @"\b(acoustic|sound|noise|insulation\s*rat|stc|iic|decibel)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_DAYLIGHTING", Pattern = @"\b(daylight|natural\s*light|daylight\s*factor|solar|glare)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_SITE_PLANNING", Pattern = @"\b(site|parking|setback|zoning|landscap|grading)\b", Type = PatternType.Regex, Confidence = 0.88f, Priority = 11 });

            // 10 Management patterns
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_DESIGN_ANALYSIS", Pattern = @"\b(analy[sz]e|review|assess|evaluat)\b.*\b(design|layout|model|plan)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_OPTIMIZATION", Pattern = @"\b(optimi[sz]e|improve|enhance|refine)\b.*\b(layout|design|plan|performance)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_DECISION_SUPPORT", Pattern = @"\b(compare|decide|choose|option|alternative|trade-?off)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_VALIDATION", Pattern = @"\b(validat|verify|check|inspect|audit)\b.*\b(design|model)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_DESIGN_PATTERNS", Pattern = @"\b(design\s*pattern|pattern|template|best\s*practice|suggest.*pattern)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_PREDICTIVE", Pattern = @"\b(predict|what\s*should|next\s*step|recommend|forecast)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_BEP", Pattern = @"\b(bep|bim\s*execution\s*plan|execution\s*plan)\b", Type = PatternType.Regex, Confidence = 0.90f, Priority = 12 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_DWG_TO_BIM", Pattern = @"\b(dwg|autocad|cad)\b.*\b(bim|convert|import|transform)\b", Type = PatternType.Regex, Confidence = 0.90f, Priority = 12 });
            _patterns.Add(new IntentPattern { IntentName = "CONSULT_IMAGE_TO_BIM", Pattern = @"\b(image|photo|picture|scan|pdf)\b.*\b(bim|convert|import|transform)\b", Type = PatternType.Regex, Confidence = 0.90f, Priority = 12 });
            _patterns.Add(new IntentPattern { IntentName = "MANAGE_GENERATIVE_DESIGN", Pattern = @"\b(generative|generat)\b.*\b(design|option|alternative|layout)\b", Type = PatternType.Regex, Confidence = 0.85f, Priority = 10 });
        }

        /// <summary>
        /// Initializes built-in BIM domain patterns that work without external files.
        /// </summary>
        private void InitializeBuiltInPatterns()
        {
            // Special commands (highest priority)
            AddBuiltIn("UNDO", PatternType.Regex, @"\b(undo|reverse|revert|go\s*back|take\s*back)\b", 0.95f, 20);
            AddBuiltIn("REDO", PatternType.Regex, @"\b(redo)\b", 0.95f, 20);
            AddBuiltIn("CANCEL", PatternType.Regex, @"\b(cancel|stop|abort|nevermind|never\s*mind|forget\s*it)\b", 0.95f, 20);
            AddBuiltIn("HELP", PatternType.Regex, @"\b(help|assist|what\s*can\s*you\s*do|how\s*do\s*i|tutorial|guide|commands?)\b", 0.90f, 18);
            AddBuiltIn("STATUS", PatternType.Regex, @"\b(status|progress|session|where\s*are\s*we)\b", 0.90f, 18);

            // Greetings / conversational
            AddBuiltIn("GREET", PatternType.Regex, @"^(hi|hello|hey|good\s*(morning|afternoon|evening)|howdy|greetings)\b", 0.92f, 16);
            AddBuiltIn("THANKS", PatternType.Regex, @"\b(thanks?|thank\s*you|appreciate|cheers)\b", 0.92f, 16);
            AddBuiltIn("GOODBYE", PatternType.Regex, @"\b(bye|goodbye|see\s*you|good\s*night|exit|quit)\b", 0.90f, 16);
            AddBuiltIn("AFFIRM", PatternType.Regex, @"^(yes|yeah|yep|sure|ok|okay|correct|right|absolutely|go\s*ahead|proceed|do\s*it)\b", 0.88f, 15);
            AddBuiltIn("DENY", PatternType.Regex, @"^(no|nope|nah|don'?t|stop|wrong|incorrect)\b", 0.88f, 15);

            // CREATE commands
            AddBuiltIn("CREATE_WALL", PatternType.Regex,
                @"\b(create|build|add|draw|place|make|construct)\b.*\b(wall|partition|divider)\b", 0.90f, 12);
            AddBuiltIn("CREATE_ROOM", PatternType.Regex,
                @"\b(create|build|add|make|design|set\s*up)\b.*\b(room|bedroom|kitchen|bathroom|living\s*room|dining\s*room|office|corridor|hallway|lobby|garage|closet|pantry|laundry|studio|loft|attic|basement|store\s*room|storage|nursery|study|library|utility|guest\s*room|master\s*bedroom|ensuite|en-suite|toilet|restroom|washroom|foyer|vestibule|reception|lounge|den|family\s*room)\b", 0.90f, 12);
            AddBuiltIn("CREATE_DOOR", PatternType.Regex,
                @"\b(create|add|place|insert|put|install)\b.*\b(door|entrance|exit|opening|doorway|entry)\b", 0.90f, 12);
            AddBuiltIn("CREATE_WINDOW", PatternType.Regex,
                @"\b(create|add|place|insert|put|install)\b.*\b(window|glazing|skylight|glass)\b", 0.90f, 12);
            AddBuiltIn("CREATE_FLOOR", PatternType.Regex,
                @"\b(create|add|make|build)\b.*\b(floor|slab|flooring)\b", 0.88f, 11);
            AddBuiltIn("CREATE_CEILING", PatternType.Regex,
                @"\b(create|add|make|build)\b.*\b(ceiling|soffit)\b", 0.88f, 11);
            AddBuiltIn("CREATE_ROOF", PatternType.Regex,
                @"\b(create|add|make|build)\b.*\b(roof|roofing)\b", 0.88f, 11);
            AddBuiltIn("CREATE_COLUMN", PatternType.Regex,
                @"\b(create|add|place|make)\b.*\b(column|pillar|post)\b", 0.88f, 11);
            AddBuiltIn("CREATE_BEAM", PatternType.Regex,
                @"\b(create|add|place|make)\b.*\b(beam|lintel|joist)\b", 0.88f, 11);
            AddBuiltIn("CREATE_STAIR", PatternType.Regex,
                @"\b(create|add|make|build)\b.*\b(stair|stairs|staircase|stairway|steps)\b", 0.88f, 11);

            // MODIFY commands
            AddBuiltIn("MOVE_ELEMENT", PatternType.Regex,
                @"\b(move|shift|reposition|relocate|slide|push|pull|nudge|drag)\b", 0.85f, 10);
            AddBuiltIn("DELETE_ELEMENT", PatternType.Regex,
                @"\b(delete|remove|erase|destroy|demolish|get\s*rid|clear|tear\s*down)\b", 0.85f, 10);
            AddBuiltIn("COPY_ELEMENT", PatternType.Regex,
                @"\b(copy|duplicate|clone|replicate|mirror)\b", 0.85f, 10);
            AddBuiltIn("MODIFY_DIMENSION", PatternType.Regex,
                @"\b(resize|scale|stretch|change.*size|make.*(bigger|smaller|taller|shorter|wider|narrower)|adjust.*dimension|set.*(height|width|length|depth))\b", 0.85f, 10);
            AddBuiltIn("SET_PARAMETER", PatternType.Regex,
                @"\b(set|change|update|modify|edit|assign)\b.*\b(parameter|property|value|material|type|name|color|colour)\b", 0.85f, 9);
            AddBuiltIn("SET_MATERIAL", PatternType.Regex,
                @"\b(set|change|apply|use|assign)\b.*\b(material|finish|surface|texture)\b", 0.85f, 9);
            AddBuiltIn("ROTATE_ELEMENT", PatternType.Regex,
                @"\b(rotate|turn|spin|angle|orient)\b", 0.83f, 9);
            AddBuiltIn("ALIGN_ELEMENT", PatternType.Regex,
                @"\b(align|line\s*up|center|centre|snap)\b", 0.83f, 9);

            // SELECTION commands
            AddBuiltIn("SELECT_ELEMENT", PatternType.Regex,
                @"\b(select|pick|choose|highlight|find|show\s*me|point\s*to)\b.*\b(wall|door|window|floor|room|element|column|beam|all|every)\b", 0.82f, 8);

            // QUERY commands
            AddBuiltIn("QUERY", PatternType.Regex,
                @"\b(what|how|show|tell|list|count|measure|calculate|find|get|display|what'?s)\b.*\b(area|volume|height|width|length|dimension|size|count|number|parameter|property|material|type|level|floor|cost|weight|load)\b", 0.85f, 10);
            AddBuiltIn("QUERY", PatternType.Regex,
                @"^(what|how\s*much|how\s*many|what\s*is|what\s*are|where|which)\b", 0.75f, 7);

            // COMPLIANCE commands
            AddBuiltIn("CHECK_COMPLIANCE", PatternType.Regex,
                @"\b(check|verify|validate|ensure|inspect|audit|review)\b.*\b(compliance|code|standard|regulation|fire|safety|structural|building\s*code|egress|accessibility)\b", 0.88f, 11);

            // NAVIGATION commands
            AddBuiltIn("NAVIGATE", PatternType.Regex,
                @"\b(go\s*to|navigate|switch\s*to|open|show|zoom\s*to|view)\b.*\b(level|floor|plan|section|elevation|3d|view|sheet)\b", 0.82f, 8);

            // SCHEDULE / REPORT commands
            AddBuiltIn("GENERATE_SCHEDULE", PatternType.Regex,
                @"\b(generate|create|make|produce|export)\b.*\b(schedule|report|takeoff|take-off|quantity|bill|list)\b", 0.85f, 10);
        }

        /// <summary>
        /// Initializes built-in intent examples for embedding-based classification.
        /// These enable semantic matching without loading external intent definition files.
        /// </summary>
        private void InitializeBuiltInIntentExamples()
        {
            _builtInIntentExamples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["UNDO"] = new List<string> { "undo that", "reverse the last action", "go back", "take that back" },
                ["REDO"] = new List<string> { "redo that", "do it again", "redo the last action" },
                ["CANCEL"] = new List<string> { "cancel this", "stop what you are doing", "abort", "never mind" },
                ["HELP"] = new List<string> { "help me", "what can you do", "show me how", "I need assistance" },
                ["STATUS"] = new List<string> { "what is the status", "show progress", "where are we" },
                ["GREET"] = new List<string> { "hello", "hi there", "good morning", "hey" },
                ["THANKS"] = new List<string> { "thanks", "thank you", "I appreciate it" },
                ["GOODBYE"] = new List<string> { "goodbye", "bye", "see you later", "good night" },
                ["AFFIRM"] = new List<string> { "yes", "sure", "go ahead", "that is correct", "proceed" },
                ["DENY"] = new List<string> { "no", "do not do that", "stop", "that is wrong" },
                ["CREATE_WALL"] = new List<string> { "create a wall", "add a wall here", "build a partition", "draw a wall along this line" },
                ["CREATE_ROOM"] = new List<string> { "create a room", "add a bedroom", "make a kitchen", "design an office space" },
                ["CREATE_DOOR"] = new List<string> { "add a door", "place a door here", "insert a door in the wall", "put a doorway" },
                ["CREATE_WINDOW"] = new List<string> { "add a window", "place a window here", "insert glazing in the wall", "put a window" },
                ["CREATE_FLOOR"] = new List<string> { "create a floor", "add a floor slab", "make a floor on this level" },
                ["CREATE_CEILING"] = new List<string> { "create a ceiling", "add a ceiling", "make a suspended ceiling" },
                ["CREATE_ROOF"] = new List<string> { "create a roof", "add a roof", "build a pitched roof" },
                ["CREATE_COLUMN"] = new List<string> { "create a column", "add a column", "place a structural pillar" },
                ["CREATE_BEAM"] = new List<string> { "add a beam", "create a beam", "place a lintel above the opening" },
                ["CREATE_STAIR"] = new List<string> { "create stairs", "add a staircase", "build steps between levels" },
                ["MOVE_ELEMENT"] = new List<string> { "move this element", "shift it north", "relocate the wall", "nudge it left" },
                ["DELETE_ELEMENT"] = new List<string> { "delete this", "remove that wall", "erase the element", "demolish it" },
                ["COPY_ELEMENT"] = new List<string> { "copy this", "duplicate the door", "clone it", "replicate the element" },
                ["MODIFY_DIMENSION"] = new List<string> { "make it taller", "change the width to 2 meters", "resize to 3 meters", "adjust the height" },
                ["SET_PARAMETER"] = new List<string> { "set the height to 3 meters", "change the type parameter", "update the property value" },
                ["SET_MATERIAL"] = new List<string> { "set material to concrete", "apply brick finish", "change the surface to steel" },
                ["ROTATE_ELEMENT"] = new List<string> { "rotate it 90 degrees", "turn the element", "spin it clockwise" },
                ["ALIGN_ELEMENT"] = new List<string> { "align these walls", "line them up", "center the column", "snap to grid" },
                ["SELECT_ELEMENT"] = new List<string> { "select that wall", "pick the door", "highlight all windows", "choose this element" },
                ["QUERY"] = new List<string> { "what is the area", "how many rooms are there", "show me the dimensions", "list all walls" },
                ["CHECK_COMPLIANCE"] = new List<string> { "check building code compliance", "verify fire safety", "validate against standards", "audit the design" },
                ["NAVIGATE"] = new List<string> { "go to level 2", "show the floor plan", "switch to 3D view", "zoom to the kitchen" },
                ["GENERATE_SCHEDULE"] = new List<string> { "generate a door schedule", "create a quantity takeoff", "make a room schedule report" }
            };
        }

        /// <summary>
        /// Initializes semantic embeddings from built-in intent examples.
        /// Call after construction when embedding model and tokenizer are available.
        /// Enables semantic classification without LoadAsync or external files.
        /// </summary>
        public async Task InitializeEmbeddingsAsync()
        {
            if (_embeddingModel == null || _tokenizer == null)
            {
                Logger.Warn("Cannot initialize embeddings: embedding model or tokenizer not available");
                return;
            }

            await ComputeIntentEmbeddingsAsync();
            Logger.Info($"Initialized embeddings for {_intentEmbeddings.Count} intents from built-in examples");
        }

        private void AddBuiltIn(string intentName, PatternType type, string pattern, float confidence, int priority)
        {
            _builtInPatterns.Add(new IntentPattern
            {
                IntentName = intentName,
                Pattern = pattern,
                Type = type,
                Confidence = confidence,
                Priority = priority
            });
        }

        private IntentClassificationResult TryPatternMatch(string text, List<IntentPattern> patterns)
        {
            var matches = new List<(IntentPattern Pattern, float Score)>();

            foreach (var pattern in patterns.OrderByDescending(p => p.Priority))
            {
                if (pattern.Matches(text))
                {
                    matches.Add((pattern, pattern.Confidence));
                }
            }

            if (matches.Count == 0)
                return null;

            var best = matches.OrderByDescending(m => m.Score).ThenByDescending(m => m.Pattern.Priority).First();

            return new IntentClassificationResult
            {
                Intent = best.Pattern.IntentName,
                Confidence = best.Score,
                MatchedPattern = best.Pattern.Pattern,
                Source = ClassificationSource.Pattern,
                AlternativeIntents = matches
                    .Where(m => m.Pattern.IntentName != best.Pattern.IntentName)
                    .Select(m => m.Pattern.IntentName)
                    .Distinct()
                    .Take(3)
                    .ToList()
            };
        }

        private IntentClassificationResult SelectBestResult(
            IntentClassificationResult builtIn,
            IntentClassificationResult external,
            IntentClassificationResult semantic)
        {
            var candidates = new List<IntentClassificationResult> { builtIn, external, semantic }
                .Where(c => c != null)
                .ToList();

            if (candidates.Count == 0) return null;

            // If multiple agree on the same intent, boost confidence
            var grouped = candidates.GroupBy(c => c.Intent).OrderByDescending(g => g.Count()).First();
            if (grouped.Count() > 1)
            {
                var best = grouped.OrderByDescending(c => c.Confidence).First();
                best.Confidence = Math.Min(0.99f, best.Confidence + 0.1f);
                return best;
            }

            return candidates.OrderByDescending(c => c.Confidence).First();
        }

        private List<IntentAlternative> BuildAlternatives(
            IntentClassificationResult builtIn,
            IntentClassificationResult external,
            IntentClassificationResult semantic,
            string bestIntent)
        {
            var alternatives = new List<IntentAlternative>();

            void AddAlternative(IntentClassificationResult result)
            {
                if (result == null || result.Intent == bestIntent) return;
                if (alternatives.All(a => a.Intent != result.Intent))
                {
                    alternatives.Add(new IntentAlternative
                    {
                        Intent = result.Intent,
                        Confidence = result.Confidence
                    });
                }
                // Also add that result's alternatives
                foreach (var alt in result.AlternativeIntents ?? Enumerable.Empty<string>())
                {
                    if (alt != bestIntent && alternatives.All(a => a.Intent != alt))
                    {
                        alternatives.Add(new IntentAlternative { Intent = alt, Confidence = result.Confidence * 0.8f });
                    }
                }
            }

            AddAlternative(builtIn);
            AddAlternative(external);
            AddAlternative(semantic);

            return alternatives.OrderByDescending(a => a.Confidence).Take(3).ToList();
        }

        /// <summary>
        /// Classifies using semantic similarity with intent examples (requires LoadAsync).
        /// </summary>
        private async Task<IntentClassificationResult> ClassifyBySemanticSimilarityAsync(string text)
        {
            var tokens = _tokenizer.Encode(text);
            var attention = _tokenizer.CreateAttentionMask(tokens);
            var inputEmbedding = await _embeddingModel.GetEmbeddingAsync(tokens, attention);

            var similarities = new List<(string Intent, float Similarity)>();

            foreach (var (intentName, embedding) in _intentEmbeddings)
            {
                var similarity = EmbeddingModel.CosineSimilarity(inputEmbedding, embedding);
                similarities.Add((intentName, similarity));
            }

            var sorted = similarities.OrderByDescending(s => s.Similarity).ToList();
            var best = sorted.FirstOrDefault();

            var result = new IntentClassificationResult
            {
                Intent = best.Intent,
                Confidence = best.Similarity,
                Source = ClassificationSource.Semantic,
                AlternativeIntents = sorted.Skip(1)
                    .Take(3)
                    .Where(s => s.Similarity >= ConfidenceThreshold * 0.7f)
                    .Select(s => s.Intent)
                    .ToList()
            };

            if (sorted.Count > 1)
            {
                var second = sorted[1];
                if (best.Similarity - second.Similarity < AmbiguityThreshold)
                {
                    result.IsAmbiguous = true;
                }
            }

            return result;
        }

        private async Task ComputeIntentEmbeddingsAsync()
        {
            _intentEmbeddings.Clear();

            // Compute from loaded intents (external files)
            foreach (var (intentName, intent) in _intents)
            {
                if (intent.Examples == null || intent.Examples.Count == 0) continue;
                await ComputeEmbeddingForExamplesAsync(intentName, intent.Examples);
            }

            // Compute from built-in examples (fills gaps not covered by loaded intents)
            if (_builtInIntentExamples != null)
            {
                foreach (var (intentName, examples) in _builtInIntentExamples)
                {
                    if (!_intentEmbeddings.ContainsKey(intentName) && examples.Count > 0)
                    {
                        await ComputeEmbeddingForExamplesAsync(intentName, examples);
                    }
                }
            }
        }

        private async Task ComputeEmbeddingForExamplesAsync(string intentName, List<string> examples)
        {
            var embeddings = new List<float[]>();

            foreach (var example in examples)
            {
                var tokens = _tokenizer.Encode(example);
                var attention = _tokenizer.CreateAttentionMask(tokens);
                var embedding = await _embeddingModel.GetEmbeddingAsync(tokens, attention);
                embeddings.Add(embedding);
            }

            var avgEmbedding = AverageEmbeddings(embeddings);
            _intentEmbeddings[intentName] = avgEmbedding;
        }

        private float[] AverageEmbeddings(List<float[]> embeddings)
        {
            if (embeddings.Count == 0) return Array.Empty<float>();

            var dim = embeddings[0].Length;
            var result = new float[dim];

            foreach (var embedding in embeddings)
            {
                for (int i = 0; i < dim; i++)
                {
                    result[i] += embedding[i];
                }
            }

            for (int i = 0; i < dim; i++)
            {
                result[i] /= embeddings.Count;
            }

            var norm = (float)Math.Sqrt(result.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < dim; i++)
                {
                    result[i] /= norm;
                }
            }

            return result;
        }
    }

    #region Result Types

    /// <summary>
    /// Intent classification result used by ConversationManager and ContextTracker.
    /// </summary>
    public class IntentResult
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
        public List<IntentAlternative> Alternatives { get; set; } = new List<IntentAlternative>();
        public double ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// An alternative intent with confidence score.
    /// </summary>
    public class IntentAlternative
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Internal classification result with additional metadata.
    /// </summary>
    public class IntentClassificationResult
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
        public bool IsAmbiguous { get; set; }
        public List<string> AlternativeIntents { get; set; } = new List<string>();
        public string MatchedPattern { get; set; }
        public ClassificationSource Source { get; set; }
        public double ProcessingTimeMs { get; set; }
    }

    public enum ClassificationSource
    {
        Pattern,
        Semantic,
        Combined
    }

    #endregion

    #region Definitions

    public class IntentDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Examples { get; set; } = new List<string>();
        public List<string> RequiredEntities { get; set; } = new List<string>();
        public List<string> OptionalEntities { get; set; } = new List<string>();
    }

    public class IntentPattern
    {
        public string IntentName { get; set; }
        public string Pattern { get; set; }
        public PatternType Type { get; set; } = PatternType.Contains;
        public float Confidence { get; set; } = 0.9f;
        public int Priority { get; set; } = 0;

        public bool Matches(string text)
        {
            switch (Type)
            {
                case PatternType.Exact:
                    return text.Equals(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.StartsWith:
                    return text.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.Contains:
                    return text.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.Regex:
                    return Regex.IsMatch(text, Pattern, RegexOptions.IgnoreCase);
                default:
                    return false;
            }
        }
    }

    public enum PatternType
    {
        Exact,
        StartsWith,
        Contains,
        Regex
    }

    #endregion
}
