// StingBIM.AI.Intelligence.Prediction.PredictiveCompletionEngine
// Predictive completion and suggestion system for BIM design
// Master Proposal Reference: Part 2.2 Strategy 4 - Predictive Intelligence

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Intelligence.Prediction
{
    #region Prediction Models

    /// <summary>
    /// Predicts likely next elements based on design patterns.
    /// </summary>
    public class ElementPredictor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, List<PatternSequence>> _sequencePatterns;
        private readonly Dictionary<string, Dictionary<string, double>> _transitionProbabilities;
        private readonly Dictionary<string, ContextualDefault> _contextualDefaults;

        public ElementPredictor()
        {
            _sequencePatterns = new Dictionary<string, List<PatternSequence>>();
            _transitionProbabilities = new Dictionary<string, Dictionary<string, double>>();
            _contextualDefaults = new Dictionary<string, ContextualDefault>();

            InitializeBuiltInPatterns();
        }

        /// <summary>
        /// Predicts the next likely elements based on recent actions.
        /// </summary>
        public List<ElementPrediction> PredictNextElements(
            List<DesignAction> recentActions,
            DesignContext context,
            int maxPredictions = 5)
        {
            var predictions = new List<ElementPrediction>();

            // Strategy 1: Sequence pattern matching
            var sequencePredictions = PredictFromSequences(recentActions);
            predictions.AddRange(sequencePredictions);

            // Strategy 2: Transition probability
            if (recentActions.Any())
            {
                var lastCategory = recentActions.Last().ElementCategory;
                var transitionPredictions = PredictFromTransitions(lastCategory);
                predictions.AddRange(transitionPredictions);
            }

            // Strategy 3: Context-based defaults
            var contextPredictions = PredictFromContext(context);
            predictions.AddRange(contextPredictions);

            // Strategy 4: Spatial proximity suggestions
            if (context.CurrentLocation != null)
            {
                var spatialPredictions = PredictFromSpatialContext(context);
                predictions.AddRange(spatialPredictions);
            }

            // Merge, deduplicate, and rank
            return MergeAndRankPredictions(predictions, maxPredictions);
        }

        /// <summary>
        /// Predicts parameter values for an element.
        /// </summary>
        public List<ParameterPrediction> PredictParameters(
            string elementCategory,
            string elementType,
            DesignContext context)
        {
            var predictions = new List<ParameterPrediction>();

            // Get default values from context
            if (_contextualDefaults.TryGetValue($"{elementCategory}:{context.RoomType}", out var defaults))
            {
                foreach (var param in defaults.Parameters)
                {
                    predictions.Add(new ParameterPrediction
                    {
                        ParameterName = param.Key,
                        PredictedValue = param.Value,
                        Confidence = 0.8f,
                        Source = PredictionSource.ContextualDefault
                    });
                }
            }

            // Get values from similar elements in project
            if (context.ExistingElements != null)
            {
                var similar = context.ExistingElements
                    .Where(e => e.Category == elementCategory && e.Type == elementType)
                    .ToList();

                if (similar.Any())
                {
                    var parameterValues = AggregateParameterValues(similar);
                    foreach (var param in parameterValues)
                    {
                        // Don't override higher-confidence predictions
                        if (!predictions.Any(p => p.ParameterName == param.Key))
                        {
                            predictions.Add(new ParameterPrediction
                            {
                                ParameterName = param.Key,
                                PredictedValue = param.Value.MostCommon,
                                Confidence = param.Value.Confidence,
                                Source = PredictionSource.ProjectPattern,
                                Alternatives = param.Value.Alternatives
                            });
                        }
                    }
                }
            }

            return predictions;
        }

        /// <summary>
        /// Learns from a completed action to improve future predictions.
        /// </summary>
        public void LearnFromAction(DesignAction action, DesignContext context)
        {
            var key = action.ElementCategory;

            // Update transition probabilities
            if (context.PreviousAction != null)
            {
                var fromCategory = context.PreviousAction.ElementCategory;
                UpdateTransitionProbability(fromCategory, key);
            }

            // Update contextual defaults
            if (!string.IsNullOrEmpty(context.RoomType))
            {
                UpdateContextualDefault(key, context.RoomType, action.Parameters);
            }

            Logger.Trace($"Learned from action: {action.ActionType} {key}");
        }

        private void InitializeBuiltInPatterns()
        {
            // Common design sequences
            _sequencePatterns["Residential"] = new List<PatternSequence>
            {
                new PatternSequence { Elements = new[] { "Walls", "Doors", "Windows" }, Weight = 1.0 },
                new PatternSequence { Elements = new[] { "Floors", "Walls", "Ceilings" }, Weight = 0.9 },
                new PatternSequence { Elements = new[] { "Rooms", "Walls", "Doors" }, Weight = 0.85 }
            };

            _sequencePatterns["MEP"] = new List<PatternSequence>
            {
                new PatternSequence { Elements = new[] { "Ducts", "DuctFittings", "AirTerminals" }, Weight = 1.0 },
                new PatternSequence { Elements = new[] { "Pipes", "PipeFittings", "PlumbingFixtures" }, Weight = 1.0 },
                new PatternSequence { Elements = new[] { "Conduit", "ConduitFittings", "ElectricalFixtures" }, Weight = 0.9 }
            };

            // Built-in transition probabilities
            _transitionProbabilities["Walls"] = new Dictionary<string, double>
            {
                ["Doors"] = 0.35,
                ["Windows"] = 0.30,
                ["Walls"] = 0.20,
                ["Rooms"] = 0.15
            };

            _transitionProbabilities["Doors"] = new Dictionary<string, double>
            {
                ["Walls"] = 0.40,
                ["Doors"] = 0.25,
                ["Windows"] = 0.20,
                ["Rooms"] = 0.15
            };

            _transitionProbabilities["Rooms"] = new Dictionary<string, double>
            {
                ["Walls"] = 0.45,
                ["Floors"] = 0.25,
                ["Doors"] = 0.20,
                ["Ceilings"] = 0.10
            };

            // Contextual defaults
            _contextualDefaults["Doors:Bedroom"] = new ContextualDefault
            {
                Parameters = new Dictionary<string, object>
                {
                    ["Width"] = 900, // mm
                    ["Height"] = 2100,
                    ["Type"] = "Interior Single"
                }
            };

            _contextualDefaults["Doors:Bathroom"] = new ContextualDefault
            {
                Parameters = new Dictionary<string, object>
                {
                    ["Width"] = 800,
                    ["Height"] = 2100,
                    ["Type"] = "Interior Single Privacy"
                }
            };

            _contextualDefaults["Windows:Living Room"] = new ContextualDefault
            {
                Parameters = new Dictionary<string, object>
                {
                    ["Width"] = 1800,
                    ["Height"] = 1500,
                    ["SillHeight"] = 900,
                    ["Type"] = "Casement Double"
                }
            };
        }

        private List<ElementPrediction> PredictFromSequences(List<DesignAction> recentActions)
        {
            var predictions = new List<ElementPrediction>();
            if (recentActions.Count < 2) return predictions;

            var recent = recentActions.TakeLast(3).Select(a => a.ElementCategory).ToArray();

            foreach (var patternGroup in _sequencePatterns.Values)
            {
                foreach (var pattern in patternGroup)
                {
                    var matchIndex = FindPatternMatch(recent, pattern.Elements);
                    if (matchIndex >= 0 && matchIndex < pattern.Elements.Length - 1)
                    {
                        predictions.Add(new ElementPrediction
                        {
                            Category = pattern.Elements[matchIndex + 1],
                            Confidence = (float)(pattern.Weight * 0.8),
                            Source = PredictionSource.SequencePattern,
                            Reason = $"Follows pattern: {string.Join(" → ", pattern.Elements)}"
                        });
                    }
                }
            }

            return predictions;
        }

        private int FindPatternMatch(string[] recent, string[] pattern)
        {
            for (int i = 0; i < pattern.Length - 1; i++)
            {
                if (recent.Length >= pattern.Length - i - 1)
                {
                    var match = true;
                    for (int j = 0; j <= i && j < recent.Length; j++)
                    {
                        if (recent[recent.Length - 1 - j] != pattern[i - j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return i;
                }
            }
            return -1;
        }

        private List<ElementPrediction> PredictFromTransitions(string fromCategory)
        {
            var predictions = new List<ElementPrediction>();

            if (_transitionProbabilities.TryGetValue(fromCategory, out var transitions))
            {
                foreach (var transition in transitions.OrderByDescending(t => t.Value))
                {
                    predictions.Add(new ElementPrediction
                    {
                        Category = transition.Key,
                        Confidence = (float)transition.Value,
                        Source = PredictionSource.TransitionProbability,
                        Reason = $"{transition.Value:P0} probability after {fromCategory}"
                    });
                }
            }

            return predictions;
        }

        private List<ElementPrediction> PredictFromContext(DesignContext context)
        {
            var predictions = new List<ElementPrediction>();

            // Room-specific predictions
            if (!string.IsNullOrEmpty(context.RoomType))
            {
                var roomDefaults = GetRoomRequirements(context.RoomType);
                var existingCategories = context.ExistingElements?
                    .Select(e => e.Category)
                    .ToHashSet() ?? new HashSet<string>();

                foreach (var required in roomDefaults.Where(r => !existingCategories.Contains(r.Category)))
                {
                    predictions.Add(new ElementPrediction
                    {
                        Category = required.Category,
                        Type = required.DefaultType,
                        Confidence = required.Importance,
                        Source = PredictionSource.ContextualDefault,
                        Reason = $"Required for {context.RoomType}"
                    });
                }
            }

            return predictions;
        }

        private List<ElementPrediction> PredictFromSpatialContext(DesignContext context)
        {
            var predictions = new List<ElementPrediction>();

            // Find nearby incomplete elements
            if (context.NearbyElements != null)
            {
                // If near a wall without a door, suggest door
                var nearbyWalls = context.NearbyElements.Where(e => e.Category == "Walls").ToList();
                var nearbyDoors = context.NearbyElements.Where(e => e.Category == "Doors").ToList();

                if (nearbyWalls.Any() && !nearbyDoors.Any())
                {
                    predictions.Add(new ElementPrediction
                    {
                        Category = "Doors",
                        Confidence = 0.6f,
                        Source = PredictionSource.SpatialContext,
                        Reason = "Wall nearby without door"
                    });
                }
            }

            return predictions;
        }

        private List<ElementPrediction> MergeAndRankPredictions(List<ElementPrediction> predictions, int max)
        {
            return predictions
                .GroupBy(p => p.Category)
                .Select(g => new ElementPrediction
                {
                    Category = g.Key,
                    Type = g.OrderByDescending(p => p.Confidence).FirstOrDefault()?.Type,
                    Confidence = g.Max(p => p.Confidence),
                    Source = g.OrderByDescending(p => p.Confidence).First().Source,
                    Reason = g.OrderByDescending(p => p.Confidence).First().Reason
                })
                .OrderByDescending(p => p.Confidence)
                .Take(max)
                .ToList();
        }

        private Dictionary<string, ParameterAggregate> AggregateParameterValues(List<DesignElement> elements)
        {
            var aggregates = new Dictionary<string, ParameterAggregate>();

            foreach (var element in elements)
            {
                if (element.Parameters == null) continue;

                foreach (var param in element.Parameters)
                {
                    if (!aggregates.ContainsKey(param.Key))
                    {
                        aggregates[param.Key] = new ParameterAggregate();
                    }
                    aggregates[param.Key].AddValue(param.Value);
                }
            }

            return aggregates;
        }

        private List<RoomRequirement> GetRoomRequirements(string roomType)
        {
            // Built-in room requirements
            var requirements = new Dictionary<string, List<RoomRequirement>>
            {
                ["Bedroom"] = new List<RoomRequirement>
                {
                    new RoomRequirement { Category = "Doors", DefaultType = "Interior Single", Importance = 1.0f },
                    new RoomRequirement { Category = "Windows", DefaultType = "Casement", Importance = 0.9f },
                    new RoomRequirement { Category = "Lighting", DefaultType = "Ceiling Light", Importance = 0.8f }
                },
                ["Bathroom"] = new List<RoomRequirement>
                {
                    new RoomRequirement { Category = "Doors", DefaultType = "Interior Privacy", Importance = 1.0f },
                    new RoomRequirement { Category = "PlumbingFixtures", DefaultType = "Toilet", Importance = 1.0f },
                    new RoomRequirement { Category = "PlumbingFixtures", DefaultType = "Sink", Importance = 0.95f },
                    new RoomRequirement { Category = "Ventilation", DefaultType = "Exhaust Fan", Importance = 0.9f }
                },
                ["Kitchen"] = new List<RoomRequirement>
                {
                    new RoomRequirement { Category = "Casework", DefaultType = "Base Cabinet", Importance = 0.95f },
                    new RoomRequirement { Category = "Casework", DefaultType = "Upper Cabinet", Importance = 0.9f },
                    new RoomRequirement { Category = "PlumbingFixtures", DefaultType = "Kitchen Sink", Importance = 0.95f },
                    new RoomRequirement { Category = "Appliances", DefaultType = "Cooking Range", Importance = 0.9f }
                }
            };

            return requirements.TryGetValue(roomType, out var reqs) ? reqs : new List<RoomRequirement>();
        }

        private void UpdateTransitionProbability(string from, string to)
        {
            if (!_transitionProbabilities.ContainsKey(from))
            {
                _transitionProbabilities[from] = new Dictionary<string, double>();
            }

            var transitions = _transitionProbabilities[from];
            if (!transitions.ContainsKey(to))
            {
                transitions[to] = 0.01;
            }

            // Increment and normalize
            transitions[to] += 0.01;
            var total = transitions.Values.Sum();
            foreach (var key in transitions.Keys.ToList())
            {
                transitions[key] /= total;
            }
        }

        private void UpdateContextualDefault(string category, string roomType, Dictionary<string, object> parameters)
        {
            var key = $"{category}:{roomType}";
            if (!_contextualDefaults.ContainsKey(key))
            {
                _contextualDefaults[key] = new ContextualDefault { Parameters = new Dictionary<string, object>() };
            }

            // Merge parameters (newer values override)
            foreach (var param in parameters)
            {
                _contextualDefaults[key].Parameters[param.Key] = param.Value;
            }
        }

        private class PatternSequence
        {
            public string[] Elements { get; set; }
            public double Weight { get; set; }
        }

        private class ContextualDefault
        {
            public Dictionary<string, object> Parameters { get; set; }
        }

        private class RoomRequirement
        {
            public string Category { get; set; }
            public string DefaultType { get; set; }
            public float Importance { get; set; }
        }

        private class ParameterAggregate
        {
            private readonly List<object> _values = new List<object>();

            public void AddValue(object value) => _values.Add(value);

            public object MostCommon => _values
                .GroupBy(v => v?.ToString() ?? "")
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.First();

            public float Confidence => _values.Count > 0
                ? (float)_values.GroupBy(v => v?.ToString()).Max(g => g.Count()) / _values.Count
                : 0;

            public List<object> Alternatives => _values
                .GroupBy(v => v?.ToString() ?? "")
                .OrderByDescending(g => g.Count())
                .Skip(1)
                .Take(3)
                .Select(g => g.First())
                .ToList();
        }
    }

    #endregion

    #region Prediction Results

    /// <summary>
    /// Prediction for the next element to place.
    /// </summary>
    public class ElementPrediction
    {
        public string Category { get; set; }
        public string Type { get; set; }
        public string Family { get; set; }
        public float Confidence { get; set; }
        public PredictionSource Source { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, object> SuggestedParameters { get; set; }
    }

    /// <summary>
    /// Prediction for a parameter value.
    /// </summary>
    public class ParameterPrediction
    {
        public string ParameterName { get; set; }
        public object PredictedValue { get; set; }
        public float Confidence { get; set; }
        public PredictionSource Source { get; set; }
        public List<object> Alternatives { get; set; }
    }

    public enum PredictionSource
    {
        SequencePattern,        // From learned design sequences
        TransitionProbability,  // Statistical likelihood
        ContextualDefault,      // Room/area-specific defaults
        ProjectPattern,         // Pattern from current project
        SpatialContext,         // Based on nearby elements
        UserHistory,            // From user's past actions
        AIModel                 // From ML model
    }

    #endregion

    #region Pre-computation Cache

    /// <summary>
    /// Pre-computes and caches predictions for faster response.
    /// </summary>
    public class PredictionCache
    {
        private readonly ConcurrentDictionary<string, CachedPrediction> _cache;
        private readonly TimeSpan _cacheExpiry;
        private readonly int _maxCacheSize;

        public PredictionCache(TimeSpan? cacheExpiry = null, int maxCacheSize = 10000)
        {
            _cache = new ConcurrentDictionary<string, CachedPrediction>();
            _cacheExpiry = cacheExpiry ?? TimeSpan.FromMinutes(5);
            _maxCacheSize = maxCacheSize;
        }

        public bool TryGet(string key, out List<ElementPrediction> predictions)
        {
            predictions = null;

            if (_cache.TryGetValue(key, out var cached))
            {
                if (DateTime.UtcNow - cached.Timestamp < _cacheExpiry)
                {
                    predictions = cached.Predictions;
                    return true;
                }
                _cache.TryRemove(key, out _);
            }

            return false;
        }

        public void Set(string key, List<ElementPrediction> predictions)
        {
            // Evict oldest entries if cache is full
            while (_cache.Count >= _maxCacheSize)
            {
                var oldest = _cache.OrderBy(c => c.Value.Timestamp).FirstOrDefault();
                if (oldest.Key != null)
                {
                    _cache.TryRemove(oldest.Key, out _);
                }
            }

            _cache[key] = new CachedPrediction
            {
                Predictions = predictions,
                Timestamp = DateTime.UtcNow
            };
        }

        public void Invalidate(string keyPrefix = null)
        {
            if (string.IsNullOrEmpty(keyPrefix))
            {
                _cache.Clear();
            }
            else
            {
                var keysToRemove = _cache.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        private class CachedPrediction
        {
            public List<ElementPrediction> Predictions { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }

    #endregion

    #region Auto-completion Engine

    /// <summary>
    /// Provides auto-completion suggestions as user types.
    /// </summary>
    public class AutoCompletionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly TrieNode _familyTrie;
        private readonly TrieNode _typeTrie;
        private readonly TrieNode _parameterTrie;
        private readonly Dictionary<string, List<string>> _categoryFamilies;

        public AutoCompletionEngine()
        {
            _familyTrie = new TrieNode();
            _typeTrie = new TrieNode();
            _parameterTrie = new TrieNode();
            _categoryFamilies = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Adds a family to the auto-completion index.
        /// </summary>
        public void IndexFamily(string category, string familyName, IEnumerable<string> types)
        {
            _familyTrie.Insert(familyName.ToLower(), familyName);

            if (!_categoryFamilies.ContainsKey(category))
            {
                _categoryFamilies[category] = new List<string>();
            }
            _categoryFamilies[category].Add(familyName);

            foreach (var type in types)
            {
                _typeTrie.Insert(type.ToLower(), type);
            }
        }

        /// <summary>
        /// Adds a parameter to the auto-completion index.
        /// </summary>
        public void IndexParameter(string parameterName)
        {
            _parameterTrie.Insert(parameterName.ToLower(), parameterName);
        }

        /// <summary>
        /// Gets family completions for partial input.
        /// </summary>
        public List<CompletionSuggestion> GetFamilyCompletions(string partial, string category = null, int maxResults = 10)
        {
            var results = _familyTrie.Search(partial.ToLower(), maxResults * 2);

            // Filter by category if specified
            if (!string.IsNullOrEmpty(category) && _categoryFamilies.TryGetValue(category, out var families))
            {
                results = results.Where(r => families.Contains(r)).ToList();
            }

            return results.Take(maxResults).Select(r => new CompletionSuggestion
            {
                Text = r,
                Type = CompletionType.Family,
                MatchScore = CalculateMatchScore(partial, r)
            }).OrderByDescending(s => s.MatchScore).ToList();
        }

        /// <summary>
        /// Gets type completions for partial input.
        /// </summary>
        public List<CompletionSuggestion> GetTypeCompletions(string partial, int maxResults = 10)
        {
            var results = _typeTrie.Search(partial.ToLower(), maxResults);

            return results.Select(r => new CompletionSuggestion
            {
                Text = r,
                Type = CompletionType.Type,
                MatchScore = CalculateMatchScore(partial, r)
            }).OrderByDescending(s => s.MatchScore).ToList();
        }

        /// <summary>
        /// Gets parameter completions for partial input.
        /// </summary>
        public List<CompletionSuggestion> GetParameterCompletions(string partial, int maxResults = 10)
        {
            var results = _parameterTrie.Search(partial.ToLower(), maxResults);

            return results.Select(r => new CompletionSuggestion
            {
                Text = r,
                Type = CompletionType.Parameter,
                MatchScore = CalculateMatchScore(partial, r)
            }).OrderByDescending(s => s.MatchScore).ToList();
        }

        private float CalculateMatchScore(string partial, string candidate)
        {
            var lower = candidate.ToLower();
            var partialLower = partial.ToLower();

            // Exact prefix match is best
            if (lower.StartsWith(partialLower))
            {
                return 1.0f - (candidate.Length - partial.Length) * 0.01f;
            }

            // Word boundary match is good
            var words = candidate.Split(' ', '_', '-');
            foreach (var word in words)
            {
                if (word.ToLower().StartsWith(partialLower))
                {
                    return 0.8f - (word.Length - partial.Length) * 0.01f;
                }
            }

            // Contains match
            if (lower.Contains(partialLower))
            {
                return 0.5f;
            }

            return 0.1f;
        }

        /// <summary>
        /// Trie node for efficient prefix search.
        /// </summary>
        private class TrieNode
        {
            private readonly Dictionary<char, TrieNode> _children = new Dictionary<char, TrieNode>();
            private readonly List<string> _values = new List<string>();
            public void Insert(string key, string value)
            {
                var node = this;
                foreach (var c in key)
                {
                    if (!node._children.ContainsKey(c))
                    {
                        node._children[c] = new TrieNode();
                    }
                    node = node._children[c];
                }
                if (!node._values.Contains(value))
                {
                    node._values.Add(value);
                }
            }

            public List<string> Search(string prefix, int maxResults)
            {
                var node = this;
                foreach (var c in prefix)
                {
                    if (!node._children.TryGetValue(c, out node))
                    {
                        return new List<string>();
                    }
                }

                var results = new List<string>();
                CollectValues(node, results, maxResults);
                return results;
            }

            private void CollectValues(TrieNode node, List<string> results, int max)
            {
                if (results.Count >= max) return;

                results.AddRange(node._values.Take(max - results.Count));

                foreach (var child in node._children.Values)
                {
                    if (results.Count >= max) break;
                    CollectValues(child, results, max);
                }
            }
        }
    }

    /// <summary>
    /// Auto-completion suggestion.
    /// </summary>
    public class CompletionSuggestion
    {
        public string Text { get; set; }
        public CompletionType Type { get; set; }
        public float MatchScore { get; set; }
        public string Description { get; set; }
    }

    public enum CompletionType
    {
        Family,
        Type,
        Parameter,
        Value,
        Command
    }

    #endregion

    #region Main Engine

    /// <summary>
    /// Main predictive completion engine combining all prediction capabilities.
    /// </summary>
    public class PredictiveCompletionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public ElementPredictor ElementPredictor { get; }
        public PredictionCache Cache { get; }
        public AutoCompletionEngine AutoCompletion { get; }

        private readonly List<DesignAction> _recentActions;
        private readonly int _maxRecentActions;

        public PredictiveCompletionEngine(int maxRecentActions = 100)
        {
            ElementPredictor = new ElementPredictor();
            Cache = new PredictionCache();
            AutoCompletion = new AutoCompletionEngine();

            _recentActions = new List<DesignAction>();
            _maxRecentActions = maxRecentActions;

            InitializeBuiltInData();
        }

        /// <summary>
        /// Records an action for learning.
        /// </summary>
        public void RecordAction(DesignAction action, DesignContext context)
        {
            _recentActions.Add(action);
            while (_recentActions.Count > _maxRecentActions)
            {
                _recentActions.RemoveAt(0);
            }

            ElementPredictor.LearnFromAction(action, context);

            // Invalidate relevant cache entries
            Cache.Invalidate(action.ElementCategory);

            Logger.Debug($"Recorded action: {action.ActionType} {action.ElementCategory}");
        }

        /// <summary>
        /// Gets predictions for next likely elements.
        /// </summary>
        public List<ElementPrediction> GetNextElementPredictions(DesignContext context, int maxResults = 5)
        {
            var cacheKey = GenerateCacheKey(context);

            if (Cache.TryGet(cacheKey, out var cached))
            {
                return cached;
            }

            var predictions = ElementPredictor.PredictNextElements(_recentActions, context, maxResults);
            Cache.Set(cacheKey, predictions);

            return predictions;
        }

        /// <summary>
        /// Gets parameter value predictions for an element.
        /// </summary>
        public List<ParameterPrediction> GetParameterPredictions(
            string category,
            string type,
            DesignContext context)
        {
            return ElementPredictor.PredictParameters(category, type, context);
        }

        /// <summary>
        /// Gets auto-completion suggestions.
        /// </summary>
        public List<CompletionSuggestion> GetCompletions(
            string partial,
            CompletionType targetType,
            string context = null,
            int maxResults = 10)
        {
            switch (targetType)
            {
                case CompletionType.Family:
                    return AutoCompletion.GetFamilyCompletions(partial, context, maxResults);
                case CompletionType.Type:
                    return AutoCompletion.GetTypeCompletions(partial, maxResults);
                case CompletionType.Parameter:
                    return AutoCompletion.GetParameterCompletions(partial, maxResults);
                default:
                    return new List<CompletionSuggestion>();
            }
        }

        /// <summary>
        /// Pre-computes predictions for common scenarios.
        /// </summary>
        public async Task PrecomputePredictionsAsync(
            IEnumerable<string> categories,
            IEnumerable<string> roomTypes,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Pre-computing predictions...");

            foreach (var category in categories)
            {
                foreach (var roomType in roomTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var context = new DesignContext { RoomType = roomType };
                    var predictions = ElementPredictor.PredictNextElements(
                        new List<DesignAction> { new DesignAction { ElementCategory = category } },
                        context,
                        10);

                    var cacheKey = $"{category}:{roomType}";
                    Cache.Set(cacheKey, predictions);
                }
            }

            Logger.Info("Pre-computation complete");
        }

        private void InitializeBuiltInData()
        {
            // Index common Revit families
            AutoCompletion.IndexFamily("Walls", "Basic Wall", new[] { "Generic - 200mm", "Exterior - Brick", "Interior - Partition" });
            AutoCompletion.IndexFamily("Walls", "Curtain Wall", new[] { "Curtain Wall 1", "Storefront" });
            AutoCompletion.IndexFamily("Doors", "Single-Flush", new[] { "900 x 2100mm", "800 x 2100mm", "700 x 2100mm" });
            AutoCompletion.IndexFamily("Doors", "Double-Flush", new[] { "1800 x 2100mm", "1600 x 2100mm" });
            AutoCompletion.IndexFamily("Windows", "Fixed", new[] { "600 x 900mm", "900 x 1200mm", "1200 x 1500mm" });
            AutoCompletion.IndexFamily("Windows", "Casement", new[] { "600 x 1200mm", "900 x 1500mm" });

            // Index common parameters
            var commonParams = new[]
            {
                "Width", "Height", "Length", "Thickness", "Area", "Volume",
                "Mark", "Comments", "Phase Created", "Phase Demolished",
                "Fire Rating", "Acoustic Rating", "Thermal Transmittance"
            };
            foreach (var param in commonParams)
            {
                AutoCompletion.IndexParameter(param);
            }
        }

        private string GenerateCacheKey(DesignContext context)
        {
            var parts = new List<string>();

            if (_recentActions.Any())
            {
                parts.Add(_recentActions.Last().ElementCategory);
            }

            if (!string.IsNullOrEmpty(context.RoomType))
            {
                parts.Add(context.RoomType);
            }

            if (context.CurrentLocation != null)
            {
                parts.Add($"{context.CurrentLocation.X:F0},{context.CurrentLocation.Y:F0}");
            }

            return string.Join(":", parts);
        }
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Represents a design action taken by the user.
    /// </summary>
    public class DesignAction
    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString();
        public ActionType ActionType { get; set; }
        public string ElementCategory { get; set; }
        public string ElementType { get; set; }
        public string ElementId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum ActionType
    {
        Create,
        Modify,
        Delete,
        Move,
        Copy,
        Select
    }

    /// <summary>
    /// Context for predictions.
    /// </summary>
    public class DesignContext
    {
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public Point3D CurrentLocation { get; set; }
        public List<DesignElement> ExistingElements { get; set; }
        public List<DesignElement> NearbyElements { get; set; }
        public DesignAction PreviousAction { get; set; }
        public ConcurrentDictionary<string, object> SessionState { get; set; }
        public object CurrentProposal { get; set; }
        public object ConsensusResult { get; set; }
    }

    /// <summary>
    /// Simplified design element for context.
    /// </summary>
    public class DesignElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Family { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    #endregion
}
