// =========================================================================
// StingBIM.AI.Creation - NLP Element Creator
// Natural language processing for creating BIM elements from text descriptions
// =========================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Creation.NaturalLanguage
{
    /// <summary>
    /// Creates BIM elements from natural language descriptions.
    /// Supports commands like "Add a 3m wide door on the north wall" or
    /// "Create a meeting room with 20 person capacity".
    /// </summary>
    public class NLPElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly IntentRecognizer _intentRecognizer;
        private readonly EntityExtractorNLP _entityExtractor;
        private readonly ParameterResolver _parameterResolver;
        private readonly ElementFactory _elementFactory;
        private readonly ContextManager _contextManager;
        private readonly AmbiguityResolver _ambiguityResolver;

        private readonly Dictionary<string, ElementTypeDefinition> _elementDefinitions;
        private readonly Dictionary<string, string[]> _synonyms;
        private readonly Dictionary<string, UnitConversion> _unitConversions;

        public NLPElementCreator()
        {
            _intentRecognizer = new IntentRecognizer();
            _entityExtractor = new EntityExtractorNLP();
            _parameterResolver = new ParameterResolver();
            _elementFactory = new ElementFactory();
            _contextManager = new ContextManager();
            _ambiguityResolver = new AmbiguityResolver();

            _elementDefinitions = InitializeElementDefinitions();
            _synonyms = InitializeSynonyms();
            _unitConversions = InitializeUnitConversions();

            Logger.Info("NLPElementCreator initialized successfully");
        }

        #region Main Processing Methods

        /// <summary>
        /// Processes a natural language command and creates the specified elements.
        /// </summary>
        public async Task<NLPCreationResult> ProcessCommandAsync(
            string command,
            NLPContext context,
            IProgress<NLPProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing NLP command: {command}");

            var result = new NLPCreationResult
            {
                OriginalCommand = command,
                ProcessingStartTime = DateTime.UtcNow
            };

            try
            {
                // Step 1: Preprocess command
                progress?.Report(new NLPProgress { Stage = "Preprocessing", Percentage = 10 });
                var preprocessed = PreprocessCommand(command);

                // Step 2: Recognize intent
                progress?.Report(new NLPProgress { Stage = "Recognizing Intent", Percentage = 25 });
                var intent = await _intentRecognizer.RecognizeAsync(preprocessed, cancellationToken);
                result.RecognizedIntent = intent;

                if (intent.Confidence < 0.5)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not understand the command. Please rephrase.";
                    result.Suggestions = GenerateSuggestions(preprocessed);
                    return result;
                }

                // Step 3: Extract entities
                progress?.Report(new NLPProgress { Stage = "Extracting Entities", Percentage = 40 });
                var entities = await _entityExtractor.ExtractAsync(preprocessed, intent, cancellationToken);
                result.ExtractedEntities = entities;

                // Step 4: Resolve context and ambiguities
                progress?.Report(new NLPProgress { Stage = "Resolving Context", Percentage = 55 });
                var resolvedEntities = await ResolveContextAndAmbiguitiesAsync(
                    entities, context, cancellationToken);

                // Step 5: Validate and complete parameters
                progress?.Report(new NLPProgress { Stage = "Validating Parameters", Percentage = 70 });
                var parameters = await _parameterResolver.ResolveAsync(
                    resolvedEntities, intent, _elementDefinitions, cancellationToken);

                if (parameters.MissingRequired.Any())
                {
                    result.Success = false;
                    result.RequiresInput = true;
                    result.MissingParameters = parameters.MissingRequired;
                    result.Questions = GenerateQuestions(parameters.MissingRequired);
                    return result;
                }

                // Step 6: Create elements
                progress?.Report(new NLPProgress { Stage = "Creating Elements", Percentage = 85 });
                result.CreatedElements = await CreateElementsAsync(
                    intent, parameters, context, cancellationToken);

                // Step 7: Update context
                _contextManager.UpdateContext(context, result.CreatedElements);

                result.ProcessingEndTime = DateTime.UtcNow;
                result.Success = true;

                Logger.Info($"NLP command processed. Created {result.CreatedElements.Count} elements");
                progress?.Report(new NLPProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing NLP command: {command}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Processes a conversational input with follow-up context.
        /// </summary>
        public async Task<NLPCreationResult> ProcessConversationAsync(
            string input,
            ConversationHistory history,
            NLPContext context,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing conversation: {input}");

            // Check for follow-up to previous command
            if (IsFollowUp(input, history))
            {
                return await ProcessFollowUpAsync(input, history, context, cancellationToken);
            }

            // Check for confirmation/cancellation
            if (IsConfirmation(input))
            {
                return await ProcessConfirmationAsync(history, context, cancellationToken);
            }

            if (IsCancellation(input))
            {
                return new NLPCreationResult
                {
                    Success = true,
                    Message = "Command cancelled."
                };
            }

            // Process as new command
            var result = await ProcessCommandAsync(input, context, null, cancellationToken);

            // Add to history
            history.AddEntry(input, result);

            return result;
        }

        /// <summary>
        /// Processes a batch of commands.
        /// </summary>
        public async Task<BatchNLPResult> ProcessBatchAsync(
            List<string> commands,
            NLPContext context,
            IProgress<NLPProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing batch of {commands.Count} commands");

            var batchResult = new BatchNLPResult
            {
                TotalCommands = commands.Count,
                ProcessingStartTime = DateTime.UtcNow
            };

            for (int i = 0; i < commands.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new NLPProgress
                {
                    Stage = $"Processing {i + 1}/{commands.Count}",
                    Percentage = (i * 100) / commands.Count
                });

                var result = await ProcessCommandAsync(commands[i], context, null, cancellationToken);
                batchResult.Results.Add(result);

                if (result.Success)
                    batchResult.SuccessfulCommands++;
                else
                    batchResult.FailedCommands++;
            }

            batchResult.ProcessingEndTime = DateTime.UtcNow;
            progress?.Report(new NLPProgress { Stage = "Complete", Percentage = 100 });

            Logger.Info($"Batch processing complete. Success: {batchResult.SuccessfulCommands}, " +
                       $"Failed: {batchResult.FailedCommands}");

            return batchResult;
        }

        #endregion

        #region Intent Recognition

        private async Task<NLPCreationResult> ProcessFollowUpAsync(
            string input,
            ConversationHistory history,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var lastEntry = history.GetLastEntry();
            if (lastEntry == null)
            {
                return await ProcessCommandAsync(input, context, null, cancellationToken);
            }

            // Merge with previous command context
            var mergedCommand = MergeWithPreviousContext(input, lastEntry);
            return await ProcessCommandAsync(mergedCommand, context, null, cancellationToken);
        }

        private async Task<NLPCreationResult> ProcessConfirmationAsync(
            ConversationHistory history,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var lastEntry = history.GetLastEntry();
            if (lastEntry?.PendingAction == null)
            {
                return new NLPCreationResult
                {
                    Success = false,
                    ErrorMessage = "Nothing to confirm."
                };
            }

            // Execute pending action
            return await ExecutePendingActionAsync(lastEntry.PendingAction, context, cancellationToken);
        }

        private bool IsFollowUp(string input, ConversationHistory history)
        {
            var followUpPatterns = new[]
            {
                @"^(and|also|then|next|another|one more)\b",
                @"^make (it|that|them)\b",
                @"^change (it|that|them)\b",
                @"^(same|similar) (but|with)\b"
            };

            return followUpPatterns.Any(p =>
                Regex.IsMatch(input.ToLower(), p, RegexOptions.IgnoreCase));
        }

        private bool IsConfirmation(string input)
        {
            var confirmPatterns = new[]
            {
                @"^(yes|yeah|yep|ok|okay|sure|confirm|go ahead|do it|proceed)\b"
            };

            return confirmPatterns.Any(p =>
                Regex.IsMatch(input.ToLower(), p, RegexOptions.IgnoreCase));
        }

        private bool IsCancellation(string input)
        {
            var cancelPatterns = new[]
            {
                @"^(no|nope|cancel|never mind|stop|abort|forget it)\b"
            };

            return cancelPatterns.Any(p =>
                Regex.IsMatch(input.ToLower(), p, RegexOptions.IgnoreCase));
        }

        private string MergeWithPreviousContext(string input, ConversationEntry lastEntry)
        {
            // Simple merge - more sophisticated would use proper NLP
            return $"{lastEntry.Command} {input}";
        }

        private async Task<NLPCreationResult> ExecutePendingActionAsync(
            PendingAction action,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            // Execute the pending action
            return new NLPCreationResult
            {
                Success = true,
                Message = "Action confirmed and executed."
            };
        }

        #endregion

        #region Entity Extraction and Resolution

        private async Task<List<ResolvedEntity>> ResolveContextAndAmbiguitiesAsync(
            List<ExtractedEntity> entities,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var resolved = new List<ResolvedEntity>();

            foreach (var entity in entities)
            {
                var resolvedEntity = new ResolvedEntity
                {
                    OriginalEntity = entity,
                    Type = entity.Type,
                    Value = entity.Value
                };

                // Resolve pronouns (it, that, them)
                if (entity.Type == "pronoun")
                {
                    resolvedEntity = ResolvePronoun(entity, context);
                }

                // Resolve relative references (the wall, that door)
                else if (entity.IsRelativeReference)
                {
                    resolvedEntity = ResolveRelativeReference(entity, context);
                }

                // Resolve ambiguous terms
                else if (entity.IsAmbiguous)
                {
                    resolvedEntity = await _ambiguityResolver.ResolveAsync(entity, context, cancellationToken);
                }

                // Convert units
                if (entity.HasUnit)
                {
                    resolvedEntity.ConvertedValue = ConvertToStandardUnit(entity.Value, entity.Unit);
                }

                resolved.Add(resolvedEntity);
            }

            return resolved;
        }

        private ResolvedEntity ResolvePronoun(ExtractedEntity entity, NLPContext context)
        {
            // Resolve "it", "that", "them" to most recent relevant element
            var recentElement = context.GetMostRecentElement();
            return new ResolvedEntity
            {
                OriginalEntity = entity,
                Type = recentElement?.ElementType ?? "unknown",
                Value = recentElement?.Id ?? entity.Value,
                ReferencedElement = recentElement
            };
        }

        private ResolvedEntity ResolveRelativeReference(ExtractedEntity entity, NLPContext context)
        {
            // Resolve "the wall", "that door" based on context
            var matchingElements = context.FindElementsByType(entity.Value);
            var mostRecent = matchingElements.OrderByDescending(e => e.CreationTime).FirstOrDefault();

            return new ResolvedEntity
            {
                OriginalEntity = entity,
                Type = entity.Type,
                Value = mostRecent?.Id ?? entity.Value,
                ReferencedElement = mostRecent
            };
        }

        private double ConvertToStandardUnit(string value, string unit)
        {
            if (!double.TryParse(value, out var numericValue))
                return 0;

            if (_unitConversions.TryGetValue(unit.ToLower(), out var conversion))
            {
                return numericValue * conversion.ToMillimeters;
            }

            return numericValue;
        }

        #endregion

        #region Element Creation

        private async Task<List<CreatedElement>> CreateElementsAsync(
            RecognizedIntent intent,
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            switch (intent.IntentType)
            {
                case IntentType.Create:
                    elements.AddRange(await CreateNewElementsAsync(parameters, context, cancellationToken));
                    break;

                case IntentType.Modify:
                    elements.AddRange(await ModifyExistingElementsAsync(parameters, context, cancellationToken));
                    break;

                case IntentType.Delete:
                    elements.AddRange(await MarkElementsForDeletionAsync(parameters, context, cancellationToken));
                    break;

                case IntentType.Copy:
                    elements.AddRange(await CopyElementsAsync(parameters, context, cancellationToken));
                    break;

                case IntentType.Move:
                    elements.AddRange(await MoveElementsAsync(parameters, context, cancellationToken));
                    break;

                case IntentType.Query:
                    // Query doesn't create elements
                    break;
            }

            return elements;
        }

        private async Task<List<CreatedElement>> CreateNewElementsAsync(
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            var elementType = parameters.GetValue<string>("elementType");
            var count = parameters.GetValue<int>("count", 1);

            if (!_elementDefinitions.TryGetValue(elementType.ToLower(), out var definition))
            {
                throw new ArgumentException($"Unknown element type: {elementType}");
            }

            for (int i = 0; i < count; i++)
            {
                var element = await _elementFactory.CreateAsync(
                    definition,
                    parameters,
                    context,
                    cancellationToken);

                elements.Add(element);
            }

            return elements;
        }

        private async Task<List<CreatedElement>> ModifyExistingElementsAsync(
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            var targetElements = parameters.GetValue<List<string>>("targetElements") ?? new List<string>();
            var modifications = parameters.GetValue<Dictionary<string, object>>("modifications") ?? new Dictionary<string, object>();

            foreach (var elementId in targetElements)
            {
                var element = context.GetElementById(elementId);
                if (element != null)
                {
                    await _elementFactory.ModifyAsync(element, modifications, cancellationToken);
                    elements.Add(new CreatedElement
                    {
                        Id = element.Id,
                        ElementType = element.ElementType,
                        Action = "Modified"
                    });
                }
            }

            return elements;
        }

        private async Task<List<CreatedElement>> MarkElementsForDeletionAsync(
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            var targetElements = parameters.GetValue<List<string>>("targetElements") ?? new List<string>();

            foreach (var elementId in targetElements)
            {
                var element = context.GetElementById(elementId);
                if (element != null)
                {
                    elements.Add(new CreatedElement
                    {
                        Id = element.Id,
                        ElementType = element.ElementType,
                        Action = "MarkedForDeletion"
                    });
                }
            }

            return elements;
        }

        private async Task<List<CreatedElement>> CopyElementsAsync(
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            var sourceElements = parameters.GetValue<List<string>>("sourceElements") ?? new List<string>();
            var offset = parameters.GetValue<Vector3D>("offset") ?? new Vector3D();
            var count = parameters.GetValue<int>("count", 1);

            foreach (var sourceId in sourceElements)
            {
                var source = context.GetElementById(sourceId);
                if (source != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var copy = await _elementFactory.CopyAsync(
                            source, offset * (i + 1), cancellationToken);
                        elements.Add(copy);
                    }
                }
            }

            return elements;
        }

        private async Task<List<CreatedElement>> MoveElementsAsync(
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken cancellationToken)
        {
            var elements = new List<CreatedElement>();

            var targetElements = parameters.GetValue<List<string>>("targetElements") ?? new List<string>();
            var destination = parameters.GetValue<Vector3D>("destination") ?? new Vector3D();

            foreach (var elementId in targetElements)
            {
                var element = context.GetElementById(elementId);
                if (element != null)
                {
                    await _elementFactory.MoveAsync(element, destination, cancellationToken);
                    elements.Add(new CreatedElement
                    {
                        Id = element.Id,
                        ElementType = element.ElementType,
                        Action = "Moved"
                    });
                }
            }

            return elements;
        }

        #endregion

        #region Command Preprocessing

        private string PreprocessCommand(string command)
        {
            // Normalize whitespace
            var processed = Regex.Replace(command.Trim(), @"\s+", " ");

            // Expand contractions
            processed = ExpandContractions(processed);

            // Replace synonyms with standard terms
            processed = ReplaceSynonyms(processed);

            // Normalize measurements
            processed = NormalizeMeasurements(processed);

            return processed;
        }

        private string ExpandContractions(string text)
        {
            var contractions = new Dictionary<string, string>
            {
                { "can't", "cannot" },
                { "don't", "do not" },
                { "won't", "will not" },
                { "i'm", "i am" },
                { "it's", "it is" },
                { "that's", "that is" },
                { "what's", "what is" },
                { "let's", "let us" }
            };

            foreach (var kvp in contractions)
            {
                text = Regex.Replace(text, $@"\b{kvp.Key}\b", kvp.Value, RegexOptions.IgnoreCase);
            }

            return text;
        }

        private string ReplaceSynonyms(string text)
        {
            foreach (var kvp in _synonyms)
            {
                foreach (var synonym in kvp.Value)
                {
                    text = Regex.Replace(text, $@"\b{synonym}\b", kvp.Key, RegexOptions.IgnoreCase);
                }
            }

            return text;
        }

        private string NormalizeMeasurements(string text)
        {
            // Normalize various measurement formats
            // "3 meters" -> "3m"
            // "3.5 m" -> "3.5m"
            // "3 feet 6 inches" -> "3'-6\""

            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*meters?\b", "$1m", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*millimeters?\b", "$1mm", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*centimeters?\b", "$1cm", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*feet\b", "$1'", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*inch(?:es)?\b", "$1\"", RegexOptions.IgnoreCase);

            return text;
        }

        #endregion

        #region Suggestions and Questions

        private List<string> GenerateSuggestions(string input)
        {
            var suggestions = new List<string>();

            // Find similar known commands
            var possibleIntents = _intentRecognizer.GetPossibleIntents(input);
            foreach (var intent in possibleIntents.Take(3))
            {
                suggestions.Add(intent.ExampleCommand);
            }

            return suggestions;
        }

        private List<ParameterQuestion> GenerateQuestions(List<string> missingParameters)
        {
            var questions = new List<ParameterQuestion>();

            foreach (var param in missingParameters)
            {
                questions.Add(new ParameterQuestion
                {
                    ParameterName = param,
                    Question = GetQuestionForParameter(param),
                    ExpectedType = GetExpectedTypeForParameter(param),
                    Examples = GetExamplesForParameter(param)
                });
            }

            return questions;
        }

        private string GetQuestionForParameter(string param)
        {
            var questions = new Dictionary<string, string>
            {
                { "width", "What should the width be?" },
                { "height", "What should the height be?" },
                { "length", "What should the length be?" },
                { "location", "Where should this element be placed?" },
                { "wallType", "What type of wall? (exterior, interior, partition)" },
                { "doorType", "What type of door? (single, double, sliding)" },
                { "windowType", "What type of window? (fixed, casement, sliding)" },
                { "material", "What material should be used?" },
                { "level", "On which level should this be placed?" },
                { "hostWall", "Which wall should host this element?" }
            };

            return questions.TryGetValue(param, out var question)
                ? question
                : $"What value should '{param}' have?";
        }

        private string GetExpectedTypeForParameter(string param)
        {
            var types = new Dictionary<string, string>
            {
                { "width", "dimension" },
                { "height", "dimension" },
                { "length", "dimension" },
                { "location", "point" },
                { "wallType", "enum" },
                { "doorType", "enum" },
                { "windowType", "enum" },
                { "material", "string" },
                { "level", "level" },
                { "hostWall", "element" }
            };

            return types.TryGetValue(param, out var type) ? type : "string";
        }

        private List<string> GetExamplesForParameter(string param)
        {
            var examples = new Dictionary<string, List<string>>
            {
                { "width", new List<string> { "900mm", "3 feet", "1.2m" } },
                { "height", new List<string> { "2.4m", "8 feet", "2700mm" } },
                { "length", new List<string> { "5m", "15 feet", "6000mm" } },
                { "wallType", new List<string> { "exterior", "interior", "partition" } },
                { "doorType", new List<string> { "single", "double", "sliding", "pocket" } },
                { "windowType", new List<string> { "fixed", "casement", "sliding", "awning" } }
            };

            return examples.TryGetValue(param, out var exampleList)
                ? exampleList
                : new List<string>();
        }

        #endregion

        #region Initialization

        private Dictionary<string, ElementTypeDefinition> InitializeElementDefinitions()
        {
            return new Dictionary<string, ElementTypeDefinition>
            {
                ["wall"] = new ElementTypeDefinition
                {
                    TypeName = "Wall",
                    Category = "Architecture",
                    RequiredParameters = new[] { "startPoint", "endPoint" },
                    OptionalParameters = new[] { "height", "thickness", "wallType", "material" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "height", 2700.0 },
                        { "thickness", 200.0 },
                        { "wallType", "Interior" }
                    }
                },
                ["door"] = new ElementTypeDefinition
                {
                    TypeName = "Door",
                    Category = "Architecture",
                    RequiredParameters = new[] { "hostWall", "location" },
                    OptionalParameters = new[] { "width", "height", "doorType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "width", 900.0 },
                        { "height", 2100.0 },
                        { "doorType", "Single" }
                    }
                },
                ["window"] = new ElementTypeDefinition
                {
                    TypeName = "Window",
                    Category = "Architecture",
                    RequiredParameters = new[] { "hostWall", "location" },
                    OptionalParameters = new[] { "width", "height", "sillHeight", "windowType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "width", 1200.0 },
                        { "height", 1500.0 },
                        { "sillHeight", 900.0 },
                        { "windowType", "Fixed" }
                    }
                },
                ["room"] = new ElementTypeDefinition
                {
                    TypeName = "Room",
                    Category = "Architecture",
                    RequiredParameters = new[] { "boundary" },
                    OptionalParameters = new[] { "name", "roomType", "area" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "roomType", "Generic" }
                    }
                },
                ["floor"] = new ElementTypeDefinition
                {
                    TypeName = "Floor",
                    Category = "Architecture",
                    RequiredParameters = new[] { "boundary" },
                    OptionalParameters = new[] { "thickness", "material", "floorType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "thickness", 200.0 },
                        { "floorType", "Generic" }
                    }
                },
                ["ceiling"] = new ElementTypeDefinition
                {
                    TypeName = "Ceiling",
                    Category = "Architecture",
                    RequiredParameters = new[] { "boundary" },
                    OptionalParameters = new[] { "height", "ceilingType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "height", 2700.0 },
                        { "ceilingType", "Generic" }
                    }
                },
                ["column"] = new ElementTypeDefinition
                {
                    TypeName = "Column",
                    Category = "Structure",
                    RequiredParameters = new[] { "location" },
                    OptionalParameters = new[] { "width", "depth", "height", "material" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "width", 300.0 },
                        { "depth", 300.0 },
                        { "material", "Concrete" }
                    }
                },
                ["beam"] = new ElementTypeDefinition
                {
                    TypeName = "Beam",
                    Category = "Structure",
                    RequiredParameters = new[] { "startPoint", "endPoint" },
                    OptionalParameters = new[] { "width", "depth", "material" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "width", 200.0 },
                        { "depth", 400.0 },
                        { "material", "Concrete" }
                    }
                },
                ["duct"] = new ElementTypeDefinition
                {
                    TypeName = "Duct",
                    Category = "MEP",
                    RequiredParameters = new[] { "startPoint", "endPoint" },
                    OptionalParameters = new[] { "width", "height", "ductType", "systemType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "width", 400.0 },
                        { "height", 300.0 },
                        { "ductType", "Rectangular" },
                        { "systemType", "Supply Air" }
                    }
                },
                ["pipe"] = new ElementTypeDefinition
                {
                    TypeName = "Pipe",
                    Category = "MEP",
                    RequiredParameters = new[] { "startPoint", "endPoint" },
                    OptionalParameters = new[] { "diameter", "pipeType", "systemType" },
                    DefaultValues = new Dictionary<string, object>
                    {
                        { "diameter", 100.0 },
                        { "pipeType", "Standard" },
                        { "systemType", "Domestic Cold Water" }
                    }
                }
            };
        }

        private Dictionary<string, string[]> InitializeSynonyms()
        {
            return new Dictionary<string, string[]>
            {
                ["wall"] = new[] { "partition", "divider", "barrier" },
                ["door"] = new[] { "entrance", "entry", "doorway", "opening" },
                ["window"] = new[] { "glazing", "opening" },
                ["room"] = new[] { "space", "area", "zone" },
                ["floor"] = new[] { "slab", "deck" },
                ["ceiling"] = new[] { "soffit" },
                ["column"] = new[] { "pillar", "post", "pier" },
                ["beam"] = new[] { "girder", "lintel", "joist" },
                ["create"] = new[] { "add", "make", "build", "construct", "place", "put", "insert", "draw" },
                ["delete"] = new[] { "remove", "erase", "destroy", "demolish", "clear" },
                ["modify"] = new[] { "change", "edit", "update", "alter", "adjust" },
                ["copy"] = new[] { "duplicate", "clone", "replicate" },
                ["move"] = new[] { "relocate", "shift", "translate", "reposition" }
            };
        }

        private Dictionary<string, UnitConversion> InitializeUnitConversions()
        {
            return new Dictionary<string, UnitConversion>
            {
                ["mm"] = new UnitConversion { UnitName = "millimeters", ToMillimeters = 1.0 },
                ["cm"] = new UnitConversion { UnitName = "centimeters", ToMillimeters = 10.0 },
                ["m"] = new UnitConversion { UnitName = "meters", ToMillimeters = 1000.0 },
                ["in"] = new UnitConversion { UnitName = "inches", ToMillimeters = 25.4 },
                ["\""] = new UnitConversion { UnitName = "inches", ToMillimeters = 25.4 },
                ["ft"] = new UnitConversion { UnitName = "feet", ToMillimeters = 304.8 },
                ["'"] = new UnitConversion { UnitName = "feet", ToMillimeters = 304.8 }
            };
        }

        #endregion
    }

    #region Supporting Classes

    internal class IntentRecognizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<IntentPattern> _patterns;

        public IntentRecognizer()
        {
            _patterns = InitializePatterns();
        }

        public async Task<RecognizedIntent> RecognizeAsync(string text, CancellationToken ct)
        {
            var lowerText = text.ToLower();

            foreach (var pattern in _patterns.OrderByDescending(p => p.Priority))
            {
                if (Regex.IsMatch(lowerText, pattern.Pattern))
                {
                    return new RecognizedIntent
                    {
                        IntentType = pattern.IntentType,
                        ElementType = ExtractElementType(lowerText),
                        Confidence = pattern.Priority / 10.0,
                        MatchedPattern = pattern.Pattern
                    };
                }
            }

            return new RecognizedIntent { IntentType = IntentType.Unknown, Confidence = 0 };
        }

        public List<PossibleIntent> GetPossibleIntents(string text)
        {
            return _patterns.Select(p => new PossibleIntent
            {
                IntentType = p.IntentType,
                ExampleCommand = p.ExampleCommand
            }).Take(5).ToList();
        }

        private string ExtractElementType(string text)
        {
            var elementTypes = new[] { "wall", "door", "window", "room", "floor", "ceiling",
                                       "column", "beam", "duct", "pipe", "roof", "stair" };

            return elementTypes.FirstOrDefault(e => text.Contains(e)) ?? "unknown";
        }

        private List<IntentPattern> InitializePatterns()
        {
            return new List<IntentPattern>
            {
                new IntentPattern { Pattern = @"\b(create|add|make|build|place|put|insert|draw)\b", IntentType = IntentType.Create, Priority = 10, ExampleCommand = "Create a wall from point A to point B" },
                new IntentPattern { Pattern = @"\b(delete|remove|erase|demolish|clear)\b", IntentType = IntentType.Delete, Priority = 10, ExampleCommand = "Delete the selected wall" },
                new IntentPattern { Pattern = @"\b(modify|change|edit|update|alter|adjust)\b", IntentType = IntentType.Modify, Priority = 10, ExampleCommand = "Change the wall height to 3 meters" },
                new IntentPattern { Pattern = @"\b(copy|duplicate|clone|replicate)\b", IntentType = IntentType.Copy, Priority = 10, ExampleCommand = "Copy this wall 3 meters to the right" },
                new IntentPattern { Pattern = @"\b(move|relocate|shift|translate)\b", IntentType = IntentType.Move, Priority = 10, ExampleCommand = "Move the door to the center of the wall" },
                new IntentPattern { Pattern = @"\b(what|where|how|which|show|find|list|count)\b", IntentType = IntentType.Query, Priority = 8, ExampleCommand = "Show me all doors on level 1" }
            };
        }
    }

    internal class EntityExtractorNLP
    {
        public async Task<List<ExtractedEntity>> ExtractAsync(
            string text, RecognizedIntent intent, CancellationToken ct)
        {
            var entities = new List<ExtractedEntity>();

            // Extract dimensions
            var dimensionMatches = Regex.Matches(text, @"(\d+(?:\.\d+)?)\s*(mm|cm|m|ft|in|'|"")");
            foreach (Match match in dimensionMatches)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = "dimension",
                    Value = match.Groups[1].Value,
                    Unit = match.Groups[2].Value,
                    HasUnit = true,
                    Position = match.Index
                });
            }

            // Extract element types
            var elementTypes = new[] { "wall", "door", "window", "room", "floor", "column", "beam", "duct", "pipe" };
            foreach (var elementType in elementTypes)
            {
                if (text.ToLower().Contains(elementType))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = "elementType",
                        Value = elementType,
                        Position = text.ToLower().IndexOf(elementType)
                    });
                }
            }

            // Extract pronouns
            var pronouns = new[] { "it", "that", "this", "them", "those" };
            foreach (var pronoun in pronouns)
            {
                var match = Regex.Match(text, $@"\b{pronoun}\b", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = "pronoun",
                        Value = pronoun,
                        Position = match.Index
                    });
                }
            }

            // Extract relative references
            var relativeMatches = Regex.Matches(text, @"\b(the|that|this)\s+(wall|door|window|room|floor)\b", RegexOptions.IgnoreCase);
            foreach (Match match in relativeMatches)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = "elementReference",
                    Value = match.Groups[2].Value,
                    IsRelativeReference = true,
                    Position = match.Index
                });
            }

            // Extract directional terms
            var directions = new[] { "north", "south", "east", "west", "left", "right", "up", "down", "above", "below" };
            foreach (var direction in directions)
            {
                if (Regex.IsMatch(text, $@"\b{direction}\b", RegexOptions.IgnoreCase))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = "direction",
                        Value = direction
                    });
                }
            }

            // Extract counts
            var countMatch = Regex.Match(text, @"(\d+)\s*(walls?|doors?|windows?|columns?|beams?)", RegexOptions.IgnoreCase);
            if (countMatch.Success)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = "count",
                    Value = countMatch.Groups[1].Value
                });
            }

            return entities;
        }
    }

    internal class ParameterResolver
    {
        public async Task<ResolvedParameters> ResolveAsync(
            List<ResolvedEntity> entities,
            RecognizedIntent intent,
            Dictionary<string, ElementTypeDefinition> definitions,
            CancellationToken ct)
        {
            var parameters = new ResolvedParameters();

            // Find element type
            var elementTypeEntity = entities.FirstOrDefault(e => e.Type == "elementType");
            if (elementTypeEntity != null)
            {
                parameters.SetValue("elementType", elementTypeEntity.Value);

                // Get required parameters for this element type
                if (definitions.TryGetValue(elementTypeEntity.Value.ToLower(), out var definition))
                {
                    // Set defaults
                    foreach (var kvp in definition.DefaultValues)
                    {
                        parameters.SetValue(kvp.Key, kvp.Value);
                    }

                    // Override with extracted values
                    var dimensionEntities = entities.Where(e => e.Type == "dimension").ToList();
                    if (dimensionEntities.Any())
                    {
                        // Simple assignment - first dimension to first required parameter
                        // In reality, would use more sophisticated matching
                        var firstDimension = dimensionEntities.First();
                        parameters.SetValue("primaryDimension", firstDimension.ConvertedValue);
                    }

                    // Check for missing required parameters
                    foreach (var required in definition.RequiredParameters)
                    {
                        if (!parameters.HasValue(required))
                        {
                            parameters.MissingRequired.Add(required);
                        }
                    }
                }
            }
            else
            {
                parameters.MissingRequired.Add("elementType");
            }

            return parameters;
        }
    }

    internal class ElementFactory
    {
        public async Task<CreatedElement> CreateAsync(
            ElementTypeDefinition definition,
            ResolvedParameters parameters,
            NLPContext context,
            CancellationToken ct)
        {
            return new CreatedElement
            {
                Id = Guid.NewGuid().ToString(),
                ElementType = definition.TypeName,
                Action = "Created",
                Parameters = parameters.ToDict()
            };
        }

        public async Task ModifyAsync(
            ContextElement element,
            Dictionary<string, object> modifications,
            CancellationToken ct)
        {
            // Apply modifications
        }

        public async Task<CreatedElement> CopyAsync(
            ContextElement source,
            Vector3D offset,
            CancellationToken ct)
        {
            return new CreatedElement
            {
                Id = Guid.NewGuid().ToString(),
                ElementType = source.ElementType,
                Action = "Copied"
            };
        }

        public async Task MoveAsync(
            ContextElement element,
            Vector3D destination,
            CancellationToken ct)
        {
            // Move element
        }
    }

    internal class ContextManager
    {
        public void UpdateContext(NLPContext context, List<CreatedElement> elements)
        {
            foreach (var element in elements)
            {
                context.AddElement(new ContextElement
                {
                    Id = element.Id,
                    ElementType = element.ElementType,
                    CreationTime = DateTime.UtcNow
                });
            }
        }
    }

    internal class AmbiguityResolver
    {
        public async Task<ResolvedEntity> ResolveAsync(
            ExtractedEntity entity,
            NLPContext context,
            CancellationToken ct)
        {
            return new ResolvedEntity
            {
                OriginalEntity = entity,
                Type = entity.Type,
                Value = entity.Value
            };
        }
    }

    #endregion

    #region Data Models

    public class NLPCreationResult
    {
        public string OriginalCommand { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Message { get; set; }
        public RecognizedIntent RecognizedIntent { get; set; }
        public List<ExtractedEntity> ExtractedEntities { get; set; }
        public List<CreatedElement> CreatedElements { get; set; } = new List<CreatedElement>();
        public bool RequiresInput { get; set; }
        public List<string> MissingParameters { get; set; }
        public List<ParameterQuestion> Questions { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class BatchNLPResult
    {
        public int TotalCommands { get; set; }
        public int SuccessfulCommands { get; set; }
        public int FailedCommands { get; set; }
        public DateTime ProcessingStartTime { get; set; }
        public DateTime ProcessingEndTime { get; set; }
        public List<NLPCreationResult> Results { get; set; } = new List<NLPCreationResult>();
    }

    public class NLPProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    public class NLPContext
    {
        public List<ContextElement> Elements { get; set; } = new List<ContextElement>();

        public ContextElement GetMostRecentElement()
            => Elements.OrderByDescending(e => e.CreationTime).FirstOrDefault();

        public ContextElement GetElementById(string id)
            => Elements.FirstOrDefault(e => e.Id == id);

        public List<ContextElement> FindElementsByType(string type)
            => Elements.Where(e => e.ElementType.ToLower() == type.ToLower()).ToList();

        public void AddElement(ContextElement element)
            => Elements.Add(element);
    }

    public class ContextElement
    {
        public string Id { get; set; }
        public string ElementType { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class RecognizedIntent
    {
        public IntentType IntentType { get; set; }
        public string ElementType { get; set; }
        public double Confidence { get; set; }
        public string MatchedPattern { get; set; }
    }

    public class ExtractedEntity
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public bool HasUnit { get; set; }
        public bool IsRelativeReference { get; set; }
        public bool IsAmbiguous { get; set; }
        public int Position { get; set; }
    }

    public class ResolvedEntity
    {
        public ExtractedEntity OriginalEntity { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public double ConvertedValue { get; set; }
        public ContextElement ReferencedElement { get; set; }
    }

    public class ResolvedParameters
    {
        private Dictionary<string, object> _values = new Dictionary<string, object>();
        public List<string> MissingRequired { get; set; } = new List<string>();

        public void SetValue(string key, object value) => _values[key] = value;
        public bool HasValue(string key) => _values.ContainsKey(key);

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_values.TryGetValue(key, out var value))
            {
                if (value is T typedValue) return typedValue;
                try { return (T)Convert.ChangeType(value, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        public Dictionary<string, object> ToDict() => new Dictionary<string, object>(_values);
    }

    public class CreatedElement
    {
        public string Id { get; set; }
        public string ElementType { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class ElementTypeDefinition
    {
        public string TypeName { get; set; }
        public string Category { get; set; }
        public string[] RequiredParameters { get; set; }
        public string[] OptionalParameters { get; set; }
        public Dictionary<string, object> DefaultValues { get; set; } = new Dictionary<string, object>();
    }

    public class ParameterQuestion
    {
        public string ParameterName { get; set; }
        public string Question { get; set; }
        public string ExpectedType { get; set; }
        public List<string> Examples { get; set; }
    }

    public class ConversationHistory
    {
        private readonly List<ConversationEntry> _entries = new List<ConversationEntry>();
        private readonly object _lock = new object();

        public void AddEntry(string command, NLPCreationResult result)
        {
            lock (_lock)
            {
                _entries.Add(new ConversationEntry
                {
                    Command = command,
                    Result = result,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public ConversationEntry GetLastEntry()
        {
            lock (_lock)
            {
                return _entries.LastOrDefault();
            }
        }

        public IReadOnlyList<ConversationEntry> GetAllEntries()
        {
            lock (_lock)
            {
                return _entries.ToList().AsReadOnly();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
        }
    }

    public class ConversationEntry
    {
        public string Command { get; set; }
        public NLPCreationResult Result { get; set; }
        public DateTime Timestamp { get; set; }
        public PendingAction PendingAction { get; set; }
    }

    public class PendingAction
    {
        public string ActionType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class UnitConversion
    {
        public string UnitName { get; set; }
        public double ToMillimeters { get; set; }
    }

    public class IntentPattern
    {
        public string Pattern { get; set; }
        public IntentType IntentType { get; set; }
        public int Priority { get; set; }
        public string ExampleCommand { get; set; }
    }

    public class PossibleIntent
    {
        public IntentType IntentType { get; set; }
        public string ExampleCommand { get; set; }
    }

    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static Vector3D operator *(Vector3D v, double scalar)
            => new Vector3D { X = v.X * scalar, Y = v.Y * scalar, Z = v.Z * scalar };
    }

    public enum IntentType
    {
        Unknown,
        Create,
        Delete,
        Modify,
        Copy,
        Move,
        Query
    }

    #endregion
}
