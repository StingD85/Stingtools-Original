// StingBIM.AI.NLP.Voice.CommandParser
// Parses voice transcriptions into executable commands
// Master Proposal Reference: Part 1.1 Language Understanding

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.NLP.Pipeline;
using StingBIM.AI.NLP.Dialogue;

namespace StingBIM.AI.NLP.Voice
{
    /// <summary>
    /// Parses voice commands and text input into design commands.
    /// Handles speech-specific patterns like filler words, corrections, and numbers.
    /// </summary>
    public class CommandParser
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly IntentClassifier _intentClassifier;
        private readonly EntityExtractor _entityExtractor;

        // Speech-specific patterns
        private static readonly string[] FillerWords = {
            "um", "uh", "er", "ah", "like", "you know", "basically", "actually",
            "so", "well", "okay", "right", "hmm"
        };

        private static readonly Dictionary<string, string> SpokenNumbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "zero", "0" }, { "one", "1" }, { "two", "2" }, { "three", "3" }, { "four", "4" },
            { "five", "5" }, { "six", "6" }, { "seven", "7" }, { "eight", "8" }, { "nine", "9" },
            { "ten", "10" }, { "eleven", "11" }, { "twelve", "12" }, { "thirteen", "13" },
            { "fourteen", "14" }, { "fifteen", "15" }, { "sixteen", "16" }, { "seventeen", "17" },
            { "eighteen", "18" }, { "nineteen", "19" }, { "twenty", "20" },
            { "thirty", "30" }, { "forty", "40" }, { "fifty", "50" },
            { "sixty", "60" }, { "seventy", "70" }, { "eighty", "80" }, { "ninety", "90" },
            { "hundred", "100" }, { "thousand", "1000" },
            { "half", "0.5" }, { "quarter", "0.25" }, { "third", "0.333" }
        };

        private static readonly Dictionary<string, string> SpokenUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "meters", "m" }, { "meter", "m" }, { "metres", "m" }, { "metre", "m" },
            { "centimeters", "cm" }, { "centimeter", "cm" }, { "centimetres", "cm" },
            { "millimeters", "mm" }, { "millimeter", "mm" }, { "millimetres", "mm" },
            { "feet", "ft" }, { "foot", "ft" },
            { "inches", "in" }, { "inch", "in" },
            { "degrees", "deg" }, { "degree", "deg" }
        };

        // Correction patterns (user corrections mid-speech)
        private static readonly string[] CorrectionPhrases = {
            "no wait", "i mean", "sorry", "correction", "actually", "no no", "let me rephrase"
        };

        public CommandParser(IntentClassifier intentClassifier, EntityExtractor entityExtractor)
        {
            _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
            _entityExtractor = entityExtractor ?? throw new ArgumentNullException(nameof(entityExtractor));
        }

        /// <summary>
        /// Parses a voice transcription into a design command.
        /// </summary>
        public async Task<ParsedCommand> ParseAsync(
            string transcription,
            ConversationContext context = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Parsing voice command: {transcription}");

            var result = new ParsedCommand
            {
                OriginalText = transcription,
                Timestamp = DateTime.Now
            };

            try
            {
                // Step 1: Clean the transcription
                var cleaned = CleanTranscription(transcription);
                result.CleanedText = cleaned;

                // Step 2: Handle corrections (user changed mind mid-speech)
                cleaned = HandleCorrections(cleaned);

                // Step 3: Normalize numbers and units
                cleaned = NormalizeNumbersAndUnits(cleaned);
                result.NormalizedText = cleaned;

                // Step 4: Check for special commands
                var specialCommand = DetectSpecialCommand(cleaned);
                if (specialCommand.HasValue)
                {
                    result.IsSpecialCommand = true;
                    result.SpecialCommand = specialCommand.Value;
                    result.Confidence = 0.95f;
                    return result;
                }

                // Step 5: Classify intent
                var intentResult = await _intentClassifier.ClassifyAsync(cleaned);
                result.Intent = intentResult.Intent;
                result.Confidence = intentResult.Confidence;
                result.AlternativeIntents = intentResult.Alternatives?
                    .Select(a => (a.Intent, a.Confidence))
                    .ToList() ?? new List<(string, float)>();

                // Step 6: Extract entities
                result.Entities = _entityExtractor.Extract(cleaned);

                // Step 7: Resolve references using context
                if (context != null)
                {
                    ResolveReferences(result, context);
                }

                // Step 8: Validate completeness
                result.IsComplete = ValidateCompleteness(result);

                Logger.Info($"Parsed command: {result.Intent} (confidence: {result.Confidence:P0}, complete: {result.IsComplete})");

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error parsing command: {transcription}");
                result.Error = ex.Message;
                result.Confidence = 0;
                return result;
            }
        }

        /// <summary>
        /// Parses a sequence of voice commands (for batch operations).
        /// </summary>
        public async Task<IEnumerable<ParsedCommand>> ParseBatchAsync(
            IEnumerable<string> transcriptions,
            ConversationContext context = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ParsedCommand>();

            foreach (var transcription in transcriptions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ParseAsync(transcription, context, cancellationToken);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Cleans speech transcription by removing filler words and normalizing.
        /// </summary>
        private string CleanTranscription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var cleaned = text.ToLowerInvariant().Trim();

            // Remove filler words
            foreach (var filler in FillerWords)
            {
                cleaned = Regex.Replace(cleaned, $@"\b{Regex.Escape(filler)}\b", " ", RegexOptions.IgnoreCase);
            }

            // Remove repeated words (stuttering)
            cleaned = Regex.Replace(cleaned, @"\b(\w+)(\s+\1)+\b", "$1", RegexOptions.IgnoreCase);

            // Normalize whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }

        /// <summary>
        /// Handles mid-speech corrections.
        /// </summary>
        private string HandleCorrections(string text)
        {
            foreach (var correction in CorrectionPhrases)
            {
                var index = text.LastIndexOf(correction, StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    // Take only the part after the correction
                    text = text.Substring(index + correction.Length).Trim();
                    Logger.Debug($"Detected correction, using: {text}");
                    break;
                }
            }

            return text;
        }

        /// <summary>
        /// Normalizes spoken numbers and units to standard format.
        /// </summary>
        private string NormalizeNumbersAndUnits(string text)
        {
            var result = text;

            // Handle compound numbers like "twenty three" -> "23"
            result = NormalizeCompoundNumbers(result);

            // Replace spoken numbers with digits
            foreach (var (word, digit) in SpokenNumbers)
            {
                result = Regex.Replace(result, $@"\b{word}\b", digit, RegexOptions.IgnoreCase);
            }

            // Handle "X and a half" patterns
            result = Regex.Replace(result, @"(\d+)\s+and\s+a\s+half", m =>
            {
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    return (num + 0.5).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                }
                return m.Value;
            });

            // Handle "X point Y" patterns
            result = Regex.Replace(result, @"(\d+)\s+point\s+(\d+)", "$1.$2");

            // Normalize units
            foreach (var (spoken, standard) in SpokenUnits)
            {
                result = Regex.Replace(result, $@"\b{spoken}\b", standard, RegexOptions.IgnoreCase);
            }

            // Handle "by" as dimension separator: "4 by 5 meters" -> "4m x 5m"
            result = Regex.Replace(result, @"(\d+(?:\.\d+)?)\s*(?:by|x)\s*(\d+(?:\.\d+)?)\s*(m|ft|cm|mm)?",
                m =>
                {
                    var unit = m.Groups[3].Success ? m.Groups[3].Value : "m";
                    return $"{m.Groups[1].Value}{unit} x {m.Groups[2].Value}{unit}";
                }, RegexOptions.IgnoreCase);

            return result;
        }

        /// <summary>
        /// Normalizes compound spoken numbers.
        /// </summary>
        private string NormalizeCompoundNumbers(string text)
        {
            // Handle "twenty three" -> "23", "forty five" -> "45"
            var tensPattern = @"\b(twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)\s+(one|two|three|four|five|six|seven|eight|nine)\b";

            return Regex.Replace(text, tensPattern, m =>
            {
                var tens = m.Groups[1].Value.ToLowerInvariant() switch
                {
                    "twenty" => 20, "thirty" => 30, "forty" => 40, "fifty" => 50,
                    "sixty" => 60, "seventy" => 70, "eighty" => 80, "ninety" => 90,
                    _ => 0
                };

                var ones = m.Groups[2].Value.ToLowerInvariant() switch
                {
                    "one" => 1, "two" => 2, "three" => 3, "four" => 4, "five" => 5,
                    "six" => 6, "seven" => 7, "eight" => 8, "nine" => 9,
                    _ => 0
                };

                return (tens + ones).ToString();
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Detects special commands like undo, redo, help.
        /// </summary>
        private SpecialCommand? DetectSpecialCommand(string text)
        {
            text = text.Trim().ToLowerInvariant();

            // Undo patterns
            if (Regex.IsMatch(text, @"^(undo|go back|reverse|take that back|undo that)$"))
                return SpecialCommand.Undo;

            // Redo patterns
            if (Regex.IsMatch(text, @"^(redo|do that again|repeat)$"))
                return SpecialCommand.Redo;

            // Cancel patterns
            if (Regex.IsMatch(text, @"^(cancel|stop|never mind|forget it|abort)$"))
                return SpecialCommand.Cancel;

            // Help patterns
            if (Regex.IsMatch(text, @"^(help|what can you do|how do i|commands)"))
                return SpecialCommand.Help;

            // Status patterns
            if (Regex.IsMatch(text, @"^(status|where are we|what's happening)"))
                return SpecialCommand.Status;

            return null;
        }

        /// <summary>
        /// Resolves pronouns and references using context.
        /// </summary>
        private void ResolveReferences(ParsedCommand result, ConversationContext context)
        {
            // Look for pronouns in the text
            var pronounPatterns = new[] { "it", "this", "that", "them", "these", "those", "the last one" };

            foreach (var pronoun in pronounPatterns)
            {
                if (result.NormalizedText.Contains(pronoun, StringComparison.OrdinalIgnoreCase))
                {
                    var resolved = new ContextTracker().ResolvePronoun(context, pronoun);
                    if (resolved != null)
                    {
                        result.ResolvedReferences[pronoun] = resolved;
                    }
                }
            }

            // Look for location references
            if (result.NormalizedText.Contains("here", StringComparison.OrdinalIgnoreCase) ||
                result.NormalizedText.Contains("cursor", StringComparison.OrdinalIgnoreCase))
            {
                result.LocationReference = new ContextTracker().ResolveLocation(context, "here");
            }
        }

        /// <summary>
        /// Validates that the command has all required information.
        /// </summary>
        private bool ValidateCompleteness(ParsedCommand result)
        {
            // Check confidence threshold
            if (result.Confidence < 0.5f)
                return false;

            // Check required entities for specific intents
            var requiredEntities = GetRequiredEntities(result.Intent);
            var extractedTypes = result.Entities?.Select(e => e.Type).ToHashSet() ?? new HashSet<EntityType>();

            foreach (var required in requiredEntities)
            {
                if (!extractedTypes.Contains(required) && !result.ResolvedReferences.Any())
                {
                    result.MissingEntities.Add(required);
                }
            }

            return result.MissingEntities.Count == 0;
        }

        private IEnumerable<EntityType> GetRequiredEntities(string intent)
        {
            return intent?.ToUpperInvariant() switch
            {
                "CREATE_WALL" => new[] { EntityType.DIMENSION },
                "CREATE_ROOM" => new[] { EntityType.ROOM_TYPE },
                "MOVE_ELEMENT" => new[] { EntityType.DIRECTION },
                "SET_DIMENSION" => new[] { EntityType.DIMENSION },
                "SET_PARAMETER" => new[] { EntityType.PARAMETER_NAME },
                _ => Enumerable.Empty<EntityType>()
            };
        }
    }

    /// <summary>
    /// Result of parsing a voice command.
    /// </summary>
    public class ParsedCommand
    {
        public string OriginalText { get; set; }
        public string CleanedText { get; set; }
        public string NormalizedText { get; set; }

        public string Intent { get; set; }
        public float Confidence { get; set; }
        public List<(string Intent, float Confidence)> AlternativeIntents { get; set; } = new List<(string, float)>();

        public List<ExtractedEntity> Entities { get; set; } = new List<ExtractedEntity>();
        public Dictionary<string, ElementReference> ResolvedReferences { get; set; } = new Dictionary<string, ElementReference>();
        public LocationReference LocationReference { get; set; }

        public bool IsComplete { get; set; }
        public List<EntityType> MissingEntities { get; set; } = new List<EntityType>();

        public bool IsSpecialCommand { get; set; }
        public SpecialCommand SpecialCommand { get; set; }

        public string Error { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
