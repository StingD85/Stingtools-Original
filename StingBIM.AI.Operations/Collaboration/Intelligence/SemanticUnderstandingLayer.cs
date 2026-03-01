// ===================================================================
// StingBIM.AI.Collaboration - Semantic Understanding Intelligence Layer
// Provides deep understanding of BIM domain terminology, relationships,
// intent recognition, and natural language understanding
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Semantic Models

    /// <summary>
    /// Semantic entity extracted from text
    /// </summary>
    public class SemanticEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string NormalizedValue { get; set; } = string.Empty;
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
        public List<string> Aliases { get; set; } = new();
    }

    /// <summary>
    /// Entity type in BIM domain
    /// </summary>
    public static class BIMEntityTypes
    {
        public const string Element = "element";
        public const string Parameter = "parameter";
        public const string Material = "material";
        public const string Room = "room";
        public const string Level = "level";
        public const string Phase = "phase";
        public const string Discipline = "discipline";
        public const string System = "system";
        public const string Family = "family";
        public const string Type = "type";
        public const string View = "view";
        public const string Sheet = "sheet";
        public const string Dimension = "dimension";
        public const string Unit = "unit";
        public const string Standard = "standard";
        public const string Person = "person";
        public const string Organization = "organization";
        public const string Date = "date";
        public const string Location = "location";
        public const string Issue = "issue";
        public const string Document = "document";
    }

    /// <summary>
    /// Recognized intent from text
    /// </summary>
    public class RecognizedIntent
    {
        public string Intent { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, object> Slots { get; set; } = new();
        public List<SemanticEntity> Entities { get; set; } = new();
        public string? Clarification { get; set; }
        public List<string> SuggestedActions { get; set; } = new();
    }

    /// <summary>
    /// Intent types in BIM collaboration
    /// </summary>
    public static class BIMIntents
    {
        // Query intents
        public const string QueryElement = "query.element";
        public const string QueryParameter = "query.parameter";
        public const string QuerySchedule = "query.schedule";
        public const string QueryIssue = "query.issue";
        public const string QueryDocument = "query.document";
        public const string QueryStatus = "query.status";
        public const string QueryComparison = "query.comparison";

        // Command intents
        public const string CreateIssue = "command.create_issue";
        public const string UpdateElement = "command.update_element";
        public const string AssignTask = "command.assign_task";
        public const string ApproveDocument = "command.approve_document";
        public const string ScheduleMeeting = "command.schedule_meeting";
        public const string RunClashDetection = "command.run_clash";
        public const string GenerateReport = "command.generate_report";
        public const string ExportData = "command.export_data";

        // Navigation intents
        public const string NavigateToElement = "navigate.element";
        public const string NavigateToView = "navigate.view";
        public const string NavigateToSheet = "navigate.sheet";
        public const string NavigateToIssue = "navigate.issue";

        // Analysis intents
        public const string AnalyzeClash = "analyze.clash";
        public const string AnalyzeCompliance = "analyze.compliance";
        public const string AnalyzeCost = "analyze.cost";
        public const string AnalyzeSchedule = "analyze.schedule";

        // Collaboration intents
        public const string ShareDocument = "collaborate.share";
        public const string RequestReview = "collaborate.request_review";
        public const string MentionUser = "collaborate.mention";
        public const string CommentOnIssue = "collaborate.comment";
    }

    /// <summary>
    /// Semantic relationship between entities
    /// </summary>
    public class SemanticRelationship
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceEntityId { get; set; } = string.Empty;
        public string TargetEntityId { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Relationship types
    /// </summary>
    public static class RelationTypes
    {
        public const string Contains = "contains";
        public const string PartOf = "part_of";
        public const string ConnectedTo = "connected_to";
        public const string AdjacentTo = "adjacent_to";
        public const string Above = "above";
        public const string Below = "below";
        public const string HostedBy = "hosted_by";
        public const string Hosts = "hosts";
        public const string References = "references";
        public const string DependsOn = "depends_on";
        public const string Conflicts = "conflicts";
        public const string SameAs = "same_as";
        public const string SimilarTo = "similar_to";
        public const string CreatedBy = "created_by";
        public const string AssignedTo = "assigned_to";
        public const string LinkedTo = "linked_to";
    }

    /// <summary>
    /// Semantic understanding result
    /// </summary>
    public class SemanticAnalysisResult
    {
        public string OriginalText { get; set; } = string.Empty;
        public string NormalizedText { get; set; } = string.Empty;
        public List<SemanticEntity> Entities { get; set; } = new();
        public List<RecognizedIntent> Intents { get; set; } = new();
        public List<SemanticRelationship> Relationships { get; set; } = new();
        public double OverallConfidence { get; set; }
        public string? Summary { get; set; }
        public List<string> Keywords { get; set; } = new();
        public string? DetectedLanguage { get; set; }
        public SentimentResult? Sentiment { get; set; }
    }

    /// <summary>
    /// Sentiment analysis result
    /// </summary>
    public class SentimentResult
    {
        public string Sentiment { get; set; } = "neutral"; // positive, negative, neutral
        public double Score { get; set; }
        public double Urgency { get; set; }
        public string? Tone { get; set; } // formal, informal, technical, urgent
    }

    /// <summary>
    /// Concept definition in ontology
    /// </summary>
    public class BIMConcept
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Synonyms { get; set; } = new();
        public List<string> RelatedConcepts { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public string? ParentConcept { get; set; }
        public List<string> ChildConcepts { get; set; } = new();
    }

    #endregion

    #region Semantic Understanding Layer

    /// <summary>
    /// Semantic understanding intelligence layer
    /// </summary>
    public class SemanticUnderstandingLayer : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, BIMConcept> _ontology = new();
        private readonly ConcurrentDictionary<string, List<string>> _synonymMap = new();
        private readonly ConcurrentDictionary<string, double[]> _wordEmbeddings = new();
        private readonly List<IntentPattern> _intentPatterns = new();
        private readonly List<EntityPattern> _entityPatterns = new();

        public SemanticUnderstandingLayer(ILogger? logger = null)
        {
            _logger = logger;
            InitializeOntology();
            InitializePatterns();
            _logger?.LogInformation("SemanticUnderstandingLayer initialized with {Concepts} concepts",
                _ontology.Count);
        }

        #region Initialization

        private void InitializeOntology()
        {
            // Building elements
            AddConcept("wall", "Wall", "A vertical building element that encloses or divides spaces",
                "Element", new[] { "partition", "barrier" });
            AddConcept("floor", "Floor", "A horizontal structural element that provides a walking surface",
                "Element", new[] { "slab", "deck" });
            AddConcept("ceiling", "Ceiling", "The overhead interior surface of a room",
                "Element", new[] { "soffit" });
            AddConcept("roof", "Roof", "The covering on the uppermost part of a building",
                "Element", new[] { "roofing" });
            AddConcept("door", "Door", "An opening in a wall for passage",
                "Element", new[] { "entrance", "entry", "exit" });
            AddConcept("window", "Window", "An opening in a wall for light and ventilation",
                "Element", new[] { "glazing", "fenestration" });
            AddConcept("column", "Column", "A vertical structural member that transfers loads",
                "Element", new[] { "pillar", "post" });
            AddConcept("beam", "Beam", "A horizontal structural member that spans between supports",
                "Element", new[] { "girder", "joist" });
            AddConcept("stair", "Stair", "A set of steps for moving between levels",
                "Element", new[] { "stairway", "staircase", "steps" });

            // MEP elements
            AddConcept("duct", "Duct", "A conduit for air distribution in HVAC systems",
                "MEP", new[] { "ductwork", "air duct" });
            AddConcept("pipe", "Pipe", "A tubular conduit for fluids",
                "MEP", new[] { "piping", "conduit" });
            AddConcept("cable_tray", "Cable Tray", "A support system for electrical cables",
                "MEP", new[] { "cable ladder", "wire tray" });

            // Disciplines
            AddConcept("architecture", "Architecture", "Design of buildings and spaces",
                "Discipline", new[] { "arch", "architectural" });
            AddConcept("structure", "Structure", "Structural engineering discipline",
                "Discipline", new[] { "structural", "str" });
            AddConcept("mechanical", "Mechanical", "HVAC and mechanical systems",
                "Discipline", new[] { "mech", "hvac" });
            AddConcept("electrical", "Electrical", "Electrical systems and power distribution",
                "Discipline", new[] { "elec", "power" });
            AddConcept("plumbing", "Plumbing", "Water supply and drainage systems",
                "Discipline", new[] { "plumb", "sanitary" });

            // Parameters
            AddConcept("area", "Area", "The extent of a two-dimensional surface",
                "Parameter", new[] { "square footage", "sqft", "sq ft" });
            AddConcept("volume", "Volume", "The amount of three-dimensional space",
                "Parameter", new[] { "cubic" });
            AddConcept("height", "Height", "Vertical measurement",
                "Parameter", new[] { "elevation", "tall" });
            AddConcept("width", "Width", "Horizontal measurement",
                "Parameter", new[] { "breadth" });
            AddConcept("length", "Length", "Linear measurement",
                "Parameter", new[] { "long" });

            // Actions/Status
            AddConcept("clash", "Clash", "Geometric interference between elements",
                "Issue", new[] { "conflict", "interference", "collision" });
            AddConcept("issue", "Issue", "A problem or concern requiring attention",
                "Issue", new[] { "problem", "defect", "punch", "snag" });
            AddConcept("rfi", "RFI", "Request for Information",
                "Document", new[] { "request for info", "information request" });
            AddConcept("submittal", "Submittal", "Documentation submitted for approval",
                "Document", new[] { "submission", "shop drawing" });
        }

        private void AddConcept(string id, string name, string definition, string category, string[] synonyms)
        {
            var concept = new BIMConcept
            {
                Id = id,
                Name = name,
                Definition = definition,
                Category = category,
                Synonyms = synonyms.ToList()
            };

            _ontology[id] = concept;

            // Build synonym map
            _synonymMap[id.ToLower()] = new List<string> { id };
            _synonymMap[name.ToLower()] = new List<string> { id };
            foreach (var syn in synonyms)
            {
                var synLower = syn.ToLower();
                if (!_synonymMap.ContainsKey(synLower))
                    _synonymMap[synLower] = new List<string>();
                _synonymMap[synLower].Add(id);
            }
        }

        private void InitializePatterns()
        {
            // Intent patterns
            _intentPatterns.AddRange(new[]
            {
                // Query patterns
                new IntentPattern(BIMIntents.QueryElement,
                    @"(show|find|list|get|where)\s+(all\s+)?(the\s+)?(\w+s?)\s*(in|on|at)?",
                    new[] { "element_type" }),
                new IntentPattern(BIMIntents.QueryParameter,
                    @"what\s+is\s+the\s+(\w+)\s+of\s+(the\s+)?(\w+)",
                    new[] { "parameter", "element" }),
                new IntentPattern(BIMIntents.QueryIssue,
                    @"(show|list|find)\s+(open|all|my|pending)\s+(issues?|problems?|clashes?)",
                    new[] { "status", "type" }),
                new IntentPattern(BIMIntents.QueryStatus,
                    @"(what\s+is|show|get)\s+(the\s+)?(status|progress)\s+(of|for)\s+(.+)",
                    new[] { "target" }),

                // Command patterns
                new IntentPattern(BIMIntents.CreateIssue,
                    @"(create|add|log|report)\s+(a\s+)?(new\s+)?(issue|problem|defect)\s*(for|about|regarding)?",
                    new[] { "target" }),
                new IntentPattern(BIMIntents.AssignTask,
                    @"(assign|give|delegate)\s+(this|the)?\s*(\w+)?\s*to\s+(\w+)",
                    new[] { "task", "assignee" }),
                new IntentPattern(BIMIntents.RunClashDetection,
                    @"(run|start|execute)\s+(clash|interference)\s+(detection|check|test)",
                    new[] { "scope" }),
                new IntentPattern(BIMIntents.GenerateReport,
                    @"(generate|create|produce)\s+(a\s+)?(\w+\s+)?report",
                    new[] { "report_type" }),

                // Navigation patterns
                new IntentPattern(BIMIntents.NavigateToElement,
                    @"(go\s+to|navigate\s+to|show\s+me|zoom\s+to)\s+(the\s+)?(\w+)",
                    new[] { "element" }),
                new IntentPattern(BIMIntents.NavigateToView,
                    @"(open|switch\s+to|go\s+to)\s+(the\s+)?(\w+)\s+(view|plan|section|elevation)",
                    new[] { "view_name" }),

                // Analysis patterns
                new IntentPattern(BIMIntents.AnalyzeCompliance,
                    @"(check|verify|analyze)\s+(code\s+)?(compliance|requirements)\s*(for|of)?",
                    new[] { "scope" }),
                new IntentPattern(BIMIntents.AnalyzeCost,
                    @"(calculate|estimate|analyze)\s+(the\s+)?(cost|budget|price)\s*(of|for)?",
                    new[] { "scope" }),

                // Collaboration patterns
                new IntentPattern(BIMIntents.ShareDocument,
                    @"(share|send)\s+(this|the)?\s*(\w+)?\s*(with|to)\s+(\w+)",
                    new[] { "document", "recipient" }),
                new IntentPattern(BIMIntents.RequestReview,
                    @"(request|ask\s+for)\s+(a\s+)?review\s*(of|for|from)?",
                    new[] { "target", "reviewer" }),
            });

            // Entity patterns
            _entityPatterns.AddRange(new[]
            {
                // Dimensions with units
                new EntityPattern(BIMEntityTypes.Dimension,
                    @"(\d+(?:\.\d+)?)\s*(mm|cm|m|ft|in|inches?|feet|meters?|millimeters?)",
                    new[] { "value", "unit" }),

                // Element references
                new EntityPattern(BIMEntityTypes.Element,
                    @"(wall|floor|ceiling|door|window|column|beam|duct|pipe)\s*#?\s*(\d+)?",
                    new[] { "type", "id" }),

                // Level references
                new EntityPattern(BIMEntityTypes.Level,
                    @"(level|floor|storey)\s*#?\s*(\d+|[A-Z]|\w+)",
                    new[] { "prefix", "number" }),

                // Room references
                new EntityPattern(BIMEntityTypes.Room,
                    @"room\s*#?\s*(\d+[A-Z]?|\w+)",
                    new[] { "number" }),

                // Date patterns
                new EntityPattern(BIMEntityTypes.Date,
                    @"(\d{1,2})[/\-](\d{1,2})[/\-](\d{2,4})",
                    new[] { "day", "month", "year" }),

                // Person mentions
                new EntityPattern(BIMEntityTypes.Person,
                    @"@(\w+)",
                    new[] { "username" }),

                // Issue references
                new EntityPattern(BIMEntityTypes.Issue,
                    @"(issue|clash|rfi|submittal)\s*#?\s*(\d+)",
                    new[] { "type", "number" }),
            });
        }

        #endregion

        #region Semantic Analysis

        /// <summary>
        /// Perform full semantic analysis on text
        /// </summary>
        public async Task<SemanticAnalysisResult> AnalyzeAsync(
            string text,
            CancellationToken ct = default)
        {
            var result = new SemanticAnalysisResult
            {
                OriginalText = text,
                NormalizedText = NormalizeText(text)
            };

            // Extract entities
            result.Entities = await ExtractEntitiesAsync(text, ct);

            // Recognize intents
            result.Intents = await RecognizeIntentsAsync(text, result.Entities, ct);

            // Extract relationships
            result.Relationships = await ExtractRelationshipsAsync(text, result.Entities, ct);

            // Analyze sentiment
            result.Sentiment = AnalyzeSentiment(text);

            // Extract keywords
            result.Keywords = ExtractKeywords(text);

            // Calculate overall confidence
            result.OverallConfidence = CalculateOverallConfidence(result);

            // Generate summary
            result.Summary = GenerateSummary(result);

            _logger?.LogDebug("Semantic analysis: {Entities} entities, {Intents} intents, confidence={Confidence:F2}",
                result.Entities.Count, result.Intents.Count, result.OverallConfidence);

            return result;
        }

        /// <summary>
        /// Extract entities from text
        /// </summary>
        public async Task<List<SemanticEntity>> ExtractEntitiesAsync(
            string text,
            CancellationToken ct = default)
        {
            var entities = new List<SemanticEntity>();
            var textLower = text.ToLower();

            // Pattern-based extraction
            foreach (var pattern in _entityPatterns)
            {
                var matches = Regex.Matches(text, pattern.Pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var entity = new SemanticEntity
                    {
                        Text = match.Value,
                        Type = pattern.EntityType,
                        StartPosition = match.Index,
                        EndPosition = match.Index + match.Length,
                        Confidence = 0.9
                    };

                    // Extract slot values
                    for (int i = 0; i < pattern.Slots.Length && i < match.Groups.Count - 1; i++)
                    {
                        entity.Attributes[pattern.Slots[i]] = match.Groups[i + 1].Value;
                    }

                    entities.Add(entity);
                }
            }

            // Ontology-based extraction
            var words = Regex.Split(textLower, @"\W+").Where(w => w.Length > 2);
            foreach (var word in words)
            {
                if (_synonymMap.TryGetValue(word, out var conceptIds))
                {
                    foreach (var conceptId in conceptIds)
                    {
                        if (_ontology.TryGetValue(conceptId, out var concept))
                        {
                            // Check if already extracted by pattern
                            if (entities.Any(e => e.NormalizedValue == conceptId))
                                continue;

                            var startIndex = textLower.IndexOf(word);
                            entities.Add(new SemanticEntity
                            {
                                Text = word,
                                Type = concept.Category.ToLower(),
                                NormalizedValue = conceptId,
                                StartPosition = startIndex,
                                EndPosition = startIndex + word.Length,
                                Confidence = 0.85,
                                Aliases = concept.Synonyms
                            });
                        }
                    }
                }
            }

            return entities.OrderBy(e => e.StartPosition).ToList();
        }

        /// <summary>
        /// Recognize intents from text
        /// </summary>
        public async Task<List<RecognizedIntent>> RecognizeIntentsAsync(
            string text,
            List<SemanticEntity>? entities = null,
            CancellationToken ct = default)
        {
            var intents = new List<RecognizedIntent>();
            entities ??= await ExtractEntitiesAsync(text, ct);

            foreach (var pattern in _intentPatterns)
            {
                var match = Regex.Match(text, pattern.Pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var intent = new RecognizedIntent
                    {
                        Intent = pattern.IntentType,
                        Confidence = 0.85,
                        Entities = entities
                    };

                    // Extract slot values
                    for (int i = 0; i < pattern.Slots.Length && i < match.Groups.Count - 1; i++)
                    {
                        intent.Slots[pattern.Slots[i]] = match.Groups[i + 1].Value;
                    }

                    // Add context from entities
                    foreach (var entity in entities)
                    {
                        if (!intent.Slots.ContainsKey(entity.Type))
                        {
                            intent.Slots[entity.Type] = entity.NormalizedValue ?? entity.Text;
                        }
                    }

                    // Generate suggested actions
                    intent.SuggestedActions = GenerateSuggestedActions(intent);

                    intents.Add(intent);
                }
            }

            // If no pattern matched, try to infer from entities
            if (!intents.Any() && entities.Any())
            {
                var inferredIntent = InferIntentFromEntities(entities);
                if (inferredIntent != null)
                {
                    intents.Add(inferredIntent);
                }
            }

            return intents.OrderByDescending(i => i.Confidence).ToList();
        }

        private RecognizedIntent? InferIntentFromEntities(List<SemanticEntity> entities)
        {
            // Infer query intent if we have element references
            if (entities.Any(e => e.Type == BIMEntityTypes.Element))
            {
                return new RecognizedIntent
                {
                    Intent = BIMIntents.QueryElement,
                    Confidence = 0.6,
                    Entities = entities,
                    Clarification = "Did you want to find information about this element?"
                };
            }

            // Infer issue intent if we have issue reference
            if (entities.Any(e => e.Type == BIMEntityTypes.Issue))
            {
                return new RecognizedIntent
                {
                    Intent = BIMIntents.QueryIssue,
                    Confidence = 0.6,
                    Entities = entities
                };
            }

            return null;
        }

        private List<string> GenerateSuggestedActions(RecognizedIntent intent)
        {
            return intent.Intent switch
            {
                BIMIntents.QueryElement => new List<string>
                {
                    "View element properties",
                    "Navigate to element",
                    "Show in schedule"
                },
                BIMIntents.QueryIssue => new List<string>
                {
                    "View issue details",
                    "Assign issue",
                    "Add comment"
                },
                BIMIntents.CreateIssue => new List<string>
                {
                    "Create issue",
                    "Take screenshot",
                    "Add location"
                },
                BIMIntents.RunClashDetection => new List<string>
                {
                    "Run clash test",
                    "Configure scope",
                    "View previous results"
                },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Extract relationships between entities
        /// </summary>
        public async Task<List<SemanticRelationship>> ExtractRelationshipsAsync(
            string text,
            List<SemanticEntity> entities,
            CancellationToken ct = default)
        {
            var relationships = new List<SemanticRelationship>();
            var textLower = text.ToLower();

            // Spatial relationships
            var spatialPatterns = new Dictionary<string, string>
            {
                [@"(\w+)\s+(above|over)\s+(\w+)"] = RelationTypes.Above,
                [@"(\w+)\s+(below|under|beneath)\s+(\w+)"] = RelationTypes.Below,
                [@"(\w+)\s+(next\s+to|adjacent\s+to|beside)\s+(\w+)"] = RelationTypes.AdjacentTo,
                [@"(\w+)\s+(connected\s+to|joins|meets)\s+(\w+)"] = RelationTypes.ConnectedTo,
                [@"(\w+)\s+(in|inside|within)\s+(\w+)"] = RelationTypes.PartOf,
                [@"(\w+)\s+(contains|holds|has)\s+(\w+)"] = RelationTypes.Contains,
            };

            foreach (var pattern in spatialPatterns)
            {
                var matches = Regex.Matches(textLower, pattern.Key, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var source = FindEntityByText(entities, match.Groups[1].Value);
                    var target = FindEntityByText(entities, match.Groups[3].Value);

                    if (source != null && target != null)
                    {
                        relationships.Add(new SemanticRelationship
                        {
                            SourceEntityId = source.Id,
                            TargetEntityId = target.Id,
                            RelationType = pattern.Value,
                            Confidence = 0.8
                        });
                    }
                }
            }

            // Assignment relationships
            var assignMatch = Regex.Match(textLower, @"assign(?:ed)?\s+(?:to\s+)?@?(\w+)");
            if (assignMatch.Success)
            {
                var person = entities.FirstOrDefault(e => e.Type == BIMEntityTypes.Person);
                var task = entities.FirstOrDefault(e =>
                    e.Type == BIMEntityTypes.Issue || e.Type == BIMEntityTypes.Document);

                if (person != null && task != null)
                {
                    relationships.Add(new SemanticRelationship
                    {
                        SourceEntityId = task.Id,
                        TargetEntityId = person.Id,
                        RelationType = RelationTypes.AssignedTo,
                        Confidence = 0.9
                    });
                }
            }

            return relationships;
        }

        private SemanticEntity? FindEntityByText(List<SemanticEntity> entities, string text)
        {
            return entities.FirstOrDefault(e =>
                e.Text.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                e.NormalizedValue.Equals(text, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Sentiment Analysis

        /// <summary>
        /// Analyze sentiment of text
        /// </summary>
        public SentimentResult AnalyzeSentiment(string text)
        {
            var textLower = text.ToLower();

            // Positive indicators
            var positiveWords = new[] { "good", "great", "excellent", "approved", "complete", "resolved", "fixed", "done", "thanks", "perfect" };
            var positiveCount = positiveWords.Count(w => textLower.Contains(w));

            // Negative indicators
            var negativeWords = new[] { "bad", "wrong", "issue", "problem", "error", "failed", "delayed", "blocked", "urgent", "critical", "missing" };
            var negativeCount = negativeWords.Count(w => textLower.Contains(w));

            // Urgency indicators
            var urgentWords = new[] { "urgent", "asap", "immediately", "critical", "emergency", "today", "now", "deadline" };
            var urgencyScore = urgentWords.Count(w => textLower.Contains(w)) / (double)urgentWords.Length;

            // Calculate sentiment
            var total = positiveCount + negativeCount;
            double score;
            string sentiment;

            if (total == 0)
            {
                score = 0.5;
                sentiment = "neutral";
            }
            else
            {
                score = positiveCount / (double)total;
                sentiment = score > 0.6 ? "positive" : score < 0.4 ? "negative" : "neutral";
            }

            // Determine tone
            string tone;
            if (urgencyScore > 0.2)
                tone = "urgent";
            else if (textLower.Contains("please") || textLower.Contains("thank"))
                tone = "formal";
            else if (Regex.IsMatch(textLower, @"[!]{2,}|\b(hey|hi|yo)\b"))
                tone = "informal";
            else
                tone = "technical";

            return new SentimentResult
            {
                Sentiment = sentiment,
                Score = score,
                Urgency = urgencyScore,
                Tone = tone
            };
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Normalize text for analysis
        /// </summary>
        public string NormalizeText(string text)
        {
            // Convert to lowercase
            var normalized = text.ToLower();

            // Expand common abbreviations
            var abbreviations = new Dictionary<string, string>
            {
                [@"\barch\b"] = "architecture",
                [@"\bstruct\b"] = "structure",
                [@"\bmech\b"] = "mechanical",
                [@"\belec\b"] = "electrical",
                [@"\bplumb\b"] = "plumbing",
                [@"\blvl\b"] = "level",
                [@"\bflr\b"] = "floor",
                [@"\bclg\b"] = "ceiling",
                [@"\bdwg\b"] = "drawing",
                [@"\bspec\b"] = "specification",
                [@"\bsqft\b"] = "square feet",
                [@"\bft\b"] = "feet",
                [@"\bin\b"] = "inches",
            };

            foreach (var abbr in abbreviations)
            {
                normalized = Regex.Replace(normalized, abbr.Key, abbr.Value);
            }

            return normalized.Trim();
        }

        /// <summary>
        /// Extract keywords from text
        /// </summary>
        public List<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string>
            {
                "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "could",
                "should", "may", "might", "must", "can", "to", "of", "in", "for",
                "on", "with", "at", "by", "from", "as", "into", "through", "during",
                "before", "after", "above", "below", "between", "under", "again",
                "further", "then", "once", "here", "there", "when", "where", "why",
                "how", "all", "each", "few", "more", "most", "other", "some", "such",
                "no", "nor", "not", "only", "own", "same", "so", "than", "too", "very"
            };

            var words = Regex.Split(text.ToLower(), @"\W+")
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(10)
                .ToList();

            return words;
        }

        /// <summary>
        /// Calculate semantic similarity between two texts
        /// </summary>
        public double CalculateSimilarity(string text1, string text2)
        {
            var words1 = new HashSet<string>(ExtractKeywords(text1));
            var words2 = new HashSet<string>(ExtractKeywords(text2));

            if (!words1.Any() || !words2.Any())
                return 0;

            // Jaccard similarity
            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return intersection / (double)union;
        }

        /// <summary>
        /// Resolve concept from text
        /// </summary>
        public BIMConcept? ResolveConcept(string text)
        {
            var textLower = text.ToLower().Trim();

            if (_synonymMap.TryGetValue(textLower, out var conceptIds))
            {
                var conceptId = conceptIds.First();
                if (_ontology.TryGetValue(conceptId, out var concept))
                {
                    return concept;
                }
            }

            return null;
        }

        /// <summary>
        /// Get related concepts
        /// </summary>
        public List<BIMConcept> GetRelatedConcepts(string conceptId, int maxResults = 5)
        {
            if (!_ontology.TryGetValue(conceptId, out var concept))
                return new List<BIMConcept>();

            var related = new List<BIMConcept>();

            // Same category
            related.AddRange(_ontology.Values
                .Where(c => c.Id != conceptId && c.Category == concept.Category)
                .Take(maxResults));

            return related;
        }

        private double CalculateOverallConfidence(SemanticAnalysisResult result)
        {
            var scores = new List<double>();

            if (result.Entities.Any())
                scores.Add(result.Entities.Average(e => e.Confidence));
            if (result.Intents.Any())
                scores.Add(result.Intents.Average(i => i.Confidence));
            if (result.Relationships.Any())
                scores.Add(result.Relationships.Average(r => r.Confidence));

            return scores.Any() ? scores.Average() : 0.5;
        }

        private string GenerateSummary(SemanticAnalysisResult result)
        {
            var parts = new List<string>();

            if (result.Intents.Any())
            {
                var intent = result.Intents.First();
                parts.Add($"Intent: {intent.Intent.Split('.').Last()}");
            }

            if (result.Entities.Any())
            {
                var entityTypes = result.Entities
                    .GroupBy(e => e.Type)
                    .Select(g => $"{g.Count()} {g.Key}(s)");
                parts.Add($"Entities: {string.Join(", ", entityTypes)}");
            }

            if (result.Sentiment != null && result.Sentiment.Sentiment != "neutral")
            {
                parts.Add($"Sentiment: {result.Sentiment.Sentiment}");
            }

            return string.Join("; ", parts);
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _logger?.LogInformation("SemanticUnderstandingLayer disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Pattern Classes

    internal class IntentPattern
    {
        public string IntentType { get; }
        public string Pattern { get; }
        public string[] Slots { get; }

        public IntentPattern(string intentType, string pattern, string[] slots)
        {
            IntentType = intentType;
            Pattern = pattern;
            Slots = slots;
        }
    }

    internal class EntityPattern
    {
        public string EntityType { get; }
        public string Pattern { get; }
        public string[] Slots { get; }

        public EntityPattern(string entityType, string pattern, string[] slots)
        {
            EntityType = entityType;
            Pattern = pattern;
            Slots = slots;
        }
    }

    #endregion
}
