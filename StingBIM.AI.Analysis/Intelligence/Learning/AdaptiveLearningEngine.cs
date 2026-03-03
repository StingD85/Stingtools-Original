// StingBIM.AI.Intelligence.Learning.AdaptiveLearningEngine
// Machine learning and adaptation capabilities for BIM design intelligence
// Master Proposal Reference: Part 2.2 Strategy 6 - Adaptive Learning

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Intelligence.Learning
{
    #region Pattern Learning

    /// <summary>
    /// Learns design patterns from user actions and project history.
    /// </summary>
    public class PatternLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, DesignPattern> _learnedPatterns;
        private readonly ConcurrentDictionary<string, int> _patternUsageCount;
        private readonly ConcurrentDictionary<string, ParameterDistribution> _parameterDistributions;
        private readonly SequenceAnalyzer _sequenceAnalyzer;
        private readonly CoOccurrenceTracker _coOccurrenceTracker;

        public PatternLearner()
        {
            _learnedPatterns = new ConcurrentDictionary<string, DesignPattern>();
            _patternUsageCount = new ConcurrentDictionary<string, int>();
            _parameterDistributions = new ConcurrentDictionary<string, ParameterDistribution>();
            _sequenceAnalyzer = new SequenceAnalyzer();
            _coOccurrenceTracker = new CoOccurrenceTracker();
        }

        /// <summary>
        /// Gets the parameter distribution for a given context key.
        /// Key format: "{ElementCategory}:{RoomType}:{ParameterName}"
        /// </summary>
        public ParameterDistribution GetParameterDistribution(string key)
        {
            return _parameterDistributions.GetValueOrDefault(key);
        }

        /// <summary>
        /// Observes a design action to learn patterns.
        /// </summary>
        public void Observe(LearningObservation observation)
        {
            // Track sequence patterns
            _sequenceAnalyzer.AddToSequence(observation.ElementCategory, observation.Context);

            // Track co-occurrence patterns
            if (observation.NearbyElements != null)
            {
                foreach (var nearby in observation.NearbyElements)
                {
                    _coOccurrenceTracker.RecordCoOccurrence(
                        observation.ElementCategory,
                        nearby.Category,
                        observation.Context.RoomType);
                }
            }

            // Track parameter patterns
            TrackParameterPatterns(observation);

            // Check if a pattern has emerged
            var emergingPatterns = DetectEmergingPatterns();
            foreach (var pattern in emergingPatterns)
            {
                if (!_learnedPatterns.ContainsKey(pattern.PatternId))
                {
                    _learnedPatterns[pattern.PatternId] = pattern;
                    Logger.Info($"Learned new pattern: {pattern.Name}");
                }
            }
        }

        /// <summary>
        /// Gets learned patterns matching the current context.
        /// </summary>
        public List<DesignPattern> GetApplicablePatterns(LearningContext context, int maxPatterns = 5)
        {
            return _learnedPatterns.Values
                .Where(p => p.IsApplicable(context))
                .OrderByDescending(p => p.Confidence * GetPatternScore(p.PatternId))
                .Take(maxPatterns)
                .ToList();
        }

        /// <summary>
        /// Records that a pattern was successfully used.
        /// </summary>
        public void RecordPatternUsage(string patternId, bool wasHelpful)
        {
            _patternUsageCount.AddOrUpdate(patternId, wasHelpful ? 1 : 0, (_, count) => wasHelpful ? count + 1 : count);

            if (_learnedPatterns.TryGetValue(patternId, out var pattern))
            {
                pattern.UsageCount++;
                if (wasHelpful)
                {
                    pattern.SuccessCount++;
                    pattern.Confidence = Math.Min(1.0f, pattern.Confidence + 0.01f);
                }
                else
                {
                    pattern.Confidence = Math.Max(0.1f, pattern.Confidence - 0.02f);
                }
            }
        }

        private void TrackParameterPatterns(LearningObservation observation)
        {
            if (observation.Parameters == null) return;

            var contextKey = $"{observation.ElementCategory}:{observation.Context?.RoomType ?? "General"}";

            foreach (var param in observation.Parameters)
            {
                var paramKey = $"{contextKey}:{param.Key}";

                var dist = _parameterDistributions.GetOrAdd(paramKey, _ => new ParameterDistribution());
                dist.TotalObservations++;

                // Track numeric values for statistical analysis
                if (param.Value is double d)
                    dist.NumericValues.Add(d);
                else if (param.Value is int i)
                    dist.NumericValues.Add(i);
                else if (param.Value is float f)
                    dist.NumericValues.Add(f);
                else if (param.Value is decimal dec)
                    dist.NumericValues.Add((double)dec);
                else if (param.Value is long l)
                    dist.NumericValues.Add(l);
                else
                {
                    // Track categorical values for frequency analysis
                    var valueStr = param.Value?.ToString() ?? "null";
                    dist.CategoricalCounts.AddOrUpdate(valueStr, 1, (_, count) => count + 1);
                }
            }
        }

        private List<DesignPattern> DetectEmergingPatterns()
        {
            var patterns = new List<DesignPattern>();

            // Detect sequence patterns
            var sequences = _sequenceAnalyzer.GetFrequentSequences(minSupport: 3);
            foreach (var seq in sequences)
            {
                patterns.Add(new DesignPattern
                {
                    PatternId = $"SEQ_{string.Join("_", seq.Elements)}",
                    Name = $"Sequence: {string.Join(" → ", seq.Elements)}",
                    Type = PatternType.Sequence,
                    Elements = seq.Elements.ToList(),
                    Confidence = seq.Support / 10.0f,
                    Context = seq.Context
                });
            }

            // Detect co-occurrence patterns
            var coOccurrences = _coOccurrenceTracker.GetStrongCoOccurrences(minStrength: 0.3);
            foreach (var co in coOccurrences)
            {
                patterns.Add(new DesignPattern
                {
                    PatternId = $"CO_{co.Element1}_{co.Element2}",
                    Name = $"{co.Element1} often with {co.Element2}",
                    Type = PatternType.CoOccurrence,
                    Elements = new List<string> { co.Element1, co.Element2 },
                    Confidence = (float)co.Strength,
                    Context = co.Context
                });
            }

            // Detect parameter value patterns from tracked distributions
            foreach (var kvp in _parameterDistributions)
            {
                var dist = kvp.Value;
                if (dist.TotalObservations < 5) continue;

                var parts = kvp.Key.Split(':');
                if (parts.Length < 3) continue;
                var category = parts[0];
                var roomType = parts[1];
                var paramName = parts[2];

                // Numeric parameter patterns: detect consistent values
                if (dist.NumericValues.Count >= 5)
                {
                    var mean = dist.NumericValues.Average();
                    var variance = dist.NumericValues.Average(v => Math.Pow(v - mean, 2));
                    var stdDev = Math.Sqrt(variance);
                    var cv = mean != 0 ? stdDev / Math.Abs(mean) : double.MaxValue;

                    // Low coefficient of variation (< 0.2) indicates consistent values
                    if (cv < 0.2)
                    {
                        var sampleBoost = Math.Min(dist.NumericValues.Count, 20) / 20.0f;
                        patterns.Add(new DesignPattern
                        {
                            PatternId = $"PARAM_{kvp.Key}",
                            Name = $"{paramName} ~ {mean:F1} for {category} in {roomType}",
                            Type = PatternType.Parameter,
                            Elements = new List<string> { category },
                            Parameters = new Dictionary<string, object>
                            {
                                [paramName] = mean,
                                ["StdDev"] = stdDev,
                                ["SampleSize"] = dist.NumericValues.Count
                            },
                            Confidence = Math.Min(0.95f, (float)(1.0 - cv) * sampleBoost),
                            Context = roomType
                        });
                    }
                }

                // Categorical parameter patterns: detect dominant values
                var categoricalTotal = dist.CategoricalCounts.Values.Sum();
                if (categoricalTotal >= 5)
                {
                    var dominant = dist.CategoricalCounts
                        .OrderByDescending(kv => kv.Value)
                        .First();
                    var dominance = (float)dominant.Value / categoricalTotal;

                    if (dominance > 0.6f)
                    {
                        var sampleBoost = Math.Min(categoricalTotal, 20) / 20.0f;
                        patterns.Add(new DesignPattern
                        {
                            PatternId = $"PARAM_{kvp.Key}_{dominant.Key}",
                            Name = $"{paramName} = {dominant.Key} for {category} in {roomType}",
                            Type = PatternType.Parameter,
                            Elements = new List<string> { category },
                            Parameters = new Dictionary<string, object>
                            {
                                [paramName] = dominant.Key,
                                ["Dominance"] = dominance,
                                ["SampleSize"] = categoricalTotal
                            },
                            Confidence = dominance * sampleBoost,
                            Context = roomType
                        });
                    }
                }
            }

            return patterns;
        }

        /// <summary>
        /// Gets all learned design patterns.
        /// </summary>
        public IEnumerable<DesignPattern> GetAllPatterns()
        {
            return _learnedPatterns.Values.ToList();
        }

        /// <summary>
        /// Analyzes a user session to extract design patterns.
        /// Adapts Core PatternLearner's session analysis for the Intelligence layer.
        /// </summary>
        public IEnumerable<DesignPattern> AnalyzeSession(StingBIM.AI.Core.Learning.ProjectSession session)
        {
            if (session?.Actions == null || session.Actions.Count == 0)
                return Enumerable.Empty<DesignPattern>();

            Logger.Info($"Analyzing session with {session.Actions.Count} actions for design patterns");

            var extractedPatterns = new List<DesignPattern>();

            // Extract sequential patterns from session actions
            for (int i = 0; i < session.Actions.Count - 1; i++)
            {
                var current = session.Actions[i];
                var next = session.Actions[i + 1];

                var patternId = $"SESSION_SEQ_{current.ActionType}_{next.ActionType}";
                if (!_learnedPatterns.ContainsKey(patternId))
                {
                    var pattern = new DesignPattern
                    {
                        PatternId = patternId,
                        Name = $"Session: {current.ActionType} -> {next.ActionType}",
                        Type = PatternType.Sequence,
                        Elements = new List<string> { current.ActionType, next.ActionType },
                        Confidence = 0.5f,
                        Context = "Session"
                    };
                    _learnedPatterns[patternId] = pattern;
                    extractedPatterns.Add(pattern);
                }
                else
                {
                    var existing = _learnedPatterns[patternId];
                    existing.UsageCount++;
                    existing.Confidence = Math.Min(0.95f, existing.Confidence + 0.05f);
                    extractedPatterns.Add(existing);
                }
            }

            // Extract preference patterns from repeated parameter values
            var parameterGroups = session.Actions
                .SelectMany(a => a.Parameters)
                .GroupBy(p => p.Key);

            foreach (var group in parameterGroups)
            {
                var mostCommon = group
                    .GroupBy(p => p.Value?.ToString())
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (mostCommon != null && mostCommon.Count() >= 2)
                {
                    var patternId = $"SESSION_PREF_{group.Key}_{mostCommon.Key}";
                    if (!_learnedPatterns.ContainsKey(patternId))
                    {
                        var pattern = new DesignPattern
                        {
                            PatternId = patternId,
                            Name = $"Preference: {group.Key} = {mostCommon.Key}",
                            Type = PatternType.Preference,
                            Parameters = new Dictionary<string, object>
                            {
                                [group.Key] = mostCommon.Key
                            },
                            Confidence = mostCommon.Count() / (float)group.Count(),
                            Context = "Session"
                        };
                        _learnedPatterns[patternId] = pattern;
                        extractedPatterns.Add(pattern);
                    }
                }
            }

            Logger.Info($"Extracted {extractedPatterns.Count} patterns from session");
            return extractedPatterns;
        }

        /// <summary>
        /// Predicts the next likely action based on learned sequential patterns.
        /// </summary>
        public string PredictNextAction(string currentAction)
        {
            var sequentialPatterns = _learnedPatterns.Values
                .Where(p => p.Type == PatternType.Sequence || p.Type == PatternType.Sequential)
                .Where(p => p.Elements?.Count >= 2 && p.Elements[0] == currentAction)
                .OrderByDescending(p => p.Confidence * (p.UsageCount + 1))
                .FirstOrDefault();

            return sequentialPatterns?.Elements?.Count >= 2 ? sequentialPatterns.Elements[1] : null;
        }

        private float GetPatternScore(string patternId)
        {
            if (_patternUsageCount.TryGetValue(patternId, out var count))
            {
                return 1.0f + Math.Min(count * 0.1f, 1.0f);
            }
            return 1.0f;
        }
    }

    /// <summary>
    /// Analyzes action sequences to find patterns.
    /// </summary>
    public class SequenceAnalyzer
    {
        private readonly List<SequenceEntry> _sequenceHistory;
        private readonly int _maxHistorySize;

        public SequenceAnalyzer(int maxHistorySize = 1000)
        {
            _sequenceHistory = new List<SequenceEntry>();
            _maxHistorySize = maxHistorySize;
        }

        public void AddToSequence(string element, LearningContext context)
        {
            _sequenceHistory.Add(new SequenceEntry
            {
                Element = element,
                Context = context?.RoomType ?? "General",
                Timestamp = DateTime.UtcNow
            });

            while (_sequenceHistory.Count > _maxHistorySize)
            {
                _sequenceHistory.RemoveAt(0);
            }
        }

        public List<FrequentSequence> GetFrequentSequences(int minSupport = 3, int maxLength = 4)
        {
            var sequences = new Dictionary<string, FrequentSequence>();

            // Find all subsequences of length 2 to maxLength
            for (int len = 2; len <= maxLength; len++)
            {
                for (int i = 0; i <= _sequenceHistory.Count - len; i++)
                {
                    var subseq = _sequenceHistory.Skip(i).Take(len).ToList();

                    // Only count if within reasonable time window (5 minutes)
                    if ((subseq.Last().Timestamp - subseq.First().Timestamp).TotalMinutes > 5)
                        continue;

                    var key = string.Join("|", subseq.Select(s => s.Element));
                    var context = subseq.First().Context;

                    if (!sequences.ContainsKey(key))
                    {
                        sequences[key] = new FrequentSequence
                        {
                            Elements = subseq.Select(s => s.Element).ToArray(),
                            Context = context,
                            Support = 0
                        };
                    }
                    sequences[key].Support++;
                }
            }

            return sequences.Values.Where(s => s.Support >= minSupport).ToList();
        }

        private class SequenceEntry
        {
            public string Element { get; set; }
            public string Context { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }

    public class FrequentSequence
    {
        public string[] Elements { get; set; }
        public string Context { get; set; }
        public int Support { get; set; }
    }

    /// <summary>
    /// Tracks which elements commonly occur together.
    /// </summary>
    public class CoOccurrenceTracker
    {
        private readonly ConcurrentDictionary<string, CoOccurrenceData> _coOccurrences;

        public CoOccurrenceTracker()
        {
            _coOccurrences = new ConcurrentDictionary<string, CoOccurrenceData>();
        }

        public void RecordCoOccurrence(string element1, string element2, string context)
        {
            var key = GetKey(element1, element2, context);

            _coOccurrences.AddOrUpdate(key,
                new CoOccurrenceData { Element1 = element1, Element2 = element2, Context = context, Count = 1 },
                (_, data) => { data.Count++; return data; });
        }

        public List<CoOccurrenceData> GetStrongCoOccurrences(double minStrength = 0.3)
        {
            var totalCounts = _coOccurrences.Values.Sum(c => c.Count);
            if (totalCounts == 0) return new List<CoOccurrenceData>();

            return _coOccurrences.Values
                .Where(c => (double)c.Count / totalCounts >= minStrength / 100)
                .Select(c => { c.Strength = (double)c.Count / totalCounts; return c; })
                .Where(c => c.Strength >= minStrength)
                .ToList();
        }

        private string GetKey(string e1, string e2, string context)
        {
            var sorted = new[] { e1, e2 }.OrderBy(e => e).ToArray();
            return $"{sorted[0]}|{sorted[1]}|{context}";
        }
    }

    public class CoOccurrenceData
    {
        public string Element1 { get; set; }
        public string Element2 { get; set; }
        public string Context { get; set; }
        public int Count { get; set; }
        public double Strength { get; set; }
    }

    #endregion

    #region Design Patterns

    /// <summary>
    /// A learned design pattern.
    /// </summary>
    public class DesignPattern
    {
        public string PatternId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PatternType Type { get; set; }
        public List<string> Elements { get; set; } = new List<string>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public float Confidence { get; set; }
        public string Context { get; set; }
        public int UsageCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime LearnedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Alias for PatternId, provides compatibility with LearnedPattern API.</summary>
        public string Key
        {
            get => PatternId;
            set => PatternId = value;
        }

        /// <summary>Alias for Type, provides compatibility with LearnedPattern API.</summary>
        public PatternType PatternType
        {
            get => Type;
            set => Type = value;
        }

        /// <summary>Alias for UsageCount, provides compatibility with LearnedPattern API.</summary>
        public int Occurrences
        {
            get => UsageCount;
            set => UsageCount = value;
        }

        /// <summary>First time this pattern was observed.</summary>
        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

        /// <summary>Most recent time this pattern was observed.</summary>
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public bool IsApplicable(LearningContext context)
        {
            if (string.IsNullOrEmpty(Context)) return true;
            return Context == context?.RoomType || Context == context?.BuildingType;
        }
    }

    public enum PatternType
    {
        Sequence,           // Elements placed in order
        Sequential,         // Alias for Sequence (Core compatibility) - A followed by B
        CoOccurrence,       // Elements found together
        Parameter,          // Parameter value patterns
        Preference,         // User prefers X value
        Correction,         // User often corrects Y
        Spatial,            // Spatial arrangement patterns
        Temporal,           // Time-based patterns
        Contextual          // Context-dependent pattern
    }

    #endregion

    #region User Preference Learning

    /// <summary>
    /// Learns user preferences from their choices.
    /// </summary>
    public class PreferenceLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, UserPreference> _preferences;
        private readonly string _preferencesPath;

        public PreferenceLearner(string storagePath = null)
        {
            _preferences = new ConcurrentDictionary<string, UserPreference>();
            _preferencesPath = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "user_preferences.json");

            LoadPreferences();
        }

        /// <summary>
        /// Records a user choice to learn preferences.
        /// </summary>
        public void RecordChoice(string category, string chosenOption, List<string> availableOptions)
        {
            var key = $"choice:{category}";

            _preferences.AddOrUpdate(key,
                new UserPreference
                {
                    Category = category,
                    PreferredValues = new Dictionary<string, int> { [chosenOption] = 1 },
                    LastUpdated = DateTime.UtcNow
                },
                (_, pref) =>
                {
                    if (!pref.PreferredValues.ContainsKey(chosenOption))
                        pref.PreferredValues[chosenOption] = 0;
                    pref.PreferredValues[chosenOption]++;
                    pref.LastUpdated = DateTime.UtcNow;
                    return pref;
                });
        }

        /// <summary>
        /// Records a parameter value preference.
        /// </summary>
        public void RecordParameterValue(string elementCategory, string parameterName, object value)
        {
            var key = $"param:{elementCategory}:{parameterName}";
            var valueStr = value?.ToString() ?? "null";

            _preferences.AddOrUpdate(key,
                new UserPreference
                {
                    Category = $"{elementCategory}.{parameterName}",
                    PreferredValues = new Dictionary<string, int> { [valueStr] = 1 },
                    LastUpdated = DateTime.UtcNow
                },
                (_, pref) =>
                {
                    if (!pref.PreferredValues.ContainsKey(valueStr))
                        pref.PreferredValues[valueStr] = 0;
                    pref.PreferredValues[valueStr]++;
                    pref.LastUpdated = DateTime.UtcNow;
                    return pref;
                });
        }

        /// <summary>
        /// Gets the most preferred option for a category.
        /// </summary>
        public string GetPreferredOption(string category, List<string> availableOptions)
        {
            var key = $"choice:{category}";

            if (_preferences.TryGetValue(key, out var pref))
            {
                var ranked = availableOptions
                    .Select(o => new { Option = o, Score = pref.PreferredValues.TryGetValue(o, out var s) ? s : 0 })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (ranked?.Score > 0)
                    return ranked.Option;
            }

            return availableOptions.FirstOrDefault();
        }

        /// <summary>
        /// Gets preferred parameter value.
        /// </summary>
        public object GetPreferredParameterValue(string elementCategory, string parameterName)
        {
            var key = $"param:{elementCategory}:{parameterName}";

            if (_preferences.TryGetValue(key, out var pref))
            {
                return pref.PreferredValues
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Saves preferences to storage.
        /// </summary>
        public void SavePreferences()
        {
            try
            {
                var dir = Path.GetDirectoryName(_preferencesPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_preferences.ToDictionary(kv => kv.Key, kv => kv.Value));
                File.WriteAllText(_preferencesPath, json);
                Logger.Debug("Saved user preferences");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save preferences");
            }
        }

        private void LoadPreferences()
        {
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = File.ReadAllText(_preferencesPath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, UserPreference>>(json);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded)
                        {
                            _preferences[kv.Key] = kv.Value;
                        }
                    }
                    Logger.Debug($"Loaded {_preferences.Count} user preferences");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load preferences");
            }
        }
    }

    /// <summary>
    /// A user preference record.
    /// </summary>
    public class UserPreference
    {
        public string Category { get; set; }
        public Dictionary<string, int> PreferredValues { get; set; } = new Dictionary<string, int>();
        public DateTime LastUpdated { get; set; }
    }

    #endregion

    #region Project Style Learning

    /// <summary>
    /// Learns the style and conventions of a specific project.
    /// </summary>
    public class ProjectStyleLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, StyleRule> _styleRules;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _namingConventions;
        private readonly ConcurrentDictionary<string, List<double>> _dimensionPatterns;
        private readonly object _dimensionLock = new object();

        public ProjectStyleLearner()
        {
            _styleRules = new ConcurrentDictionary<string, StyleRule>();
            _namingConventions = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
            _dimensionPatterns = new ConcurrentDictionary<string, List<double>>();
        }

        /// <summary>
        /// Analyzes existing elements to learn project style.
        /// </summary>
        public void AnalyzeProjectElements(IEnumerable<ProjectElement> elements)
        {
            Logger.Info("Analyzing project elements for style learning...");

            foreach (var element in elements)
            {
                // Learn naming conventions
                LearnNamingConvention(element);

                // Learn dimension patterns
                LearnDimensionPatterns(element);

                // Learn material preferences
                LearnMaterialPreferences(element);
            }

            // Derive style rules from learned data
            DeriveStyleRules();

            Logger.Info($"Learned {_styleRules.Count} style rules");
        }

        /// <summary>
        /// Checks if an element conforms to project style.
        /// </summary>
        public StyleCheckResult CheckStyle(ProjectElement element)
        {
            var result = new StyleCheckResult { Element = element };

            foreach (var rule in _styleRules.Values)
            {
                if (rule.Category != element.Category) continue;

                var violation = rule.Check(element);
                if (violation != null)
                {
                    result.Violations.Add(violation);
                }
            }

            result.ConformsToStyle = !result.Violations.Any();
            return result;
        }

        /// <summary>
        /// Suggests style-conforming values.
        /// </summary>
        public Dictionary<string, object> SuggestStyledValues(string category, string parameterName)
        {
            var suggestions = new Dictionary<string, object>();

            // Suggest from dimension patterns
            var dimKey = $"{category}:{parameterName}";
            if (_dimensionPatterns.TryGetValue(dimKey, out var dimensions))
            {
                List<double> snapshot;
                lock (_dimensionLock)
                {
                    snapshot = dimensions.ToList();
                }

                if (snapshot.Any())
                {
                    var mode = snapshot.GroupBy(d => Math.Round(d, 2))
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key;

                    if (mode.HasValue)
                        suggestions["MostCommon"] = mode.Value;

                    suggestions["Average"] = snapshot.Average();
                    suggestions["Min"] = snapshot.Min();
                    suggestions["Max"] = snapshot.Max();
                }
            }

            return suggestions;
        }

        private void LearnNamingConvention(ProjectElement element)
        {
            if (string.IsNullOrEmpty(element.Name)) return;

            var category = element.Category;
            var categoryDict = _namingConventions.GetOrAdd(category, _ => new ConcurrentDictionary<string, int>());

            // Extract naming pattern (e.g., "Room 101" -> "Room {number}")
            var pattern = ExtractNamingPattern(element.Name);
            categoryDict.AddOrUpdate(pattern, 1, (_, count) => count + 1);
        }

        private void LearnDimensionPatterns(ProjectElement element)
        {
            if (element.Parameters == null) return;

            var dimensionParams = new[] { "Width", "Height", "Length", "Depth", "Thickness" };

            foreach (var param in dimensionParams)
            {
                if (element.Parameters.TryGetValue(param, out var value) && value is double d)
                {
                    var key = $"{element.Category}:{param}";
                    var list = _dimensionPatterns.GetOrAdd(key, _ => new List<double>());
                    lock (_dimensionLock)
                    {
                        list.Add(d);
                    }
                }
            }
        }

        private void LearnMaterialPreferences(ProjectElement element)
        {
            if (element.Parameters == null) return;

            if (element.Parameters.TryGetValue("Material", out var material))
            {
                var key = $"material:{element.Category}";
                var materialDict = _namingConventions.GetOrAdd(key, _ => new ConcurrentDictionary<string, int>());

                var matStr = material?.ToString() ?? "Unknown";
                materialDict.AddOrUpdate(matStr, 1, (_, count) => count + 1);
            }
        }

        private void DeriveStyleRules()
        {
            // Create rules from dimension patterns (snapshot for thread safety)
            foreach (var kvp in _dimensionPatterns.ToArray())
            {
                List<double> snapshot;
                lock (_dimensionLock)
                {
                    snapshot = kvp.Value.ToList();
                }

                if (snapshot.Count < 5) continue; // Need enough samples

                var parts = kvp.Key.Split(':');
                var category = parts[0];
                var param = parts[1];

                var mean = snapshot.Average();
                var stdDev = Math.Sqrt(snapshot.Average(v => Math.Pow(v - mean, 2)));

                _styleRules[$"dim:{kvp.Key}"] = new StyleRule
                {
                    Category = category,
                    RuleName = $"{param} consistency",
                    Description = $"{category} {param} typically {mean:F0}mm (±{stdDev:F0})",
                    CheckFunc = element =>
                    {
                        if (element.Parameters?.TryGetValue(param, out var val) == true && val is double d)
                        {
                            if (Math.Abs(d - mean) > stdDev * 2)
                            {
                                return new StyleViolation
                                {
                                    Rule = $"dim:{kvp.Key}",
                                    Message = $"{param} of {d:F0} differs from project standard {mean:F0}±{stdDev:F0}"
                                };
                            }
                        }
                        return null;
                    }
                };
            }

            // Create rules from material preferences (snapshot for thread safety)
            foreach (var kvp in _namingConventions.ToArray().Where(k => k.Key.StartsWith("material:")))
            {
                var category = kvp.Key.Replace("material:", "");
                var preferredMaterial = kvp.Value.OrderByDescending(x => x.Value).FirstOrDefault().Key;

                if (string.IsNullOrEmpty(preferredMaterial)) continue;

                _styleRules[$"mat:{category}"] = new StyleRule
                {
                    Category = category,
                    RuleName = "Material consistency",
                    Description = $"{category} typically uses {preferredMaterial}",
                    CheckFunc = element =>
                    {
                        if (element.Parameters?.TryGetValue("Material", out var mat) == true)
                        {
                            if (mat?.ToString() != preferredMaterial)
                            {
                                return new StyleViolation
                                {
                                    Rule = $"mat:{category}",
                                    Message = $"Material differs from project preference ({preferredMaterial})",
                                    Severity = StyleViolationSeverity.Info
                                };
                            }
                        }
                        return null;
                    }
                };
            }
        }

        private string ExtractNamingPattern(string name)
        {
            // Simple pattern extraction - replace numbers with {number}
            var pattern = System.Text.RegularExpressions.Regex.Replace(name, @"\d+", "{N}");
            return pattern;
        }
    }

    /// <summary>
    /// A style rule derived from project analysis.
    /// </summary>
    public class StyleRule
    {
        public string Category { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public Func<ProjectElement, StyleViolation> CheckFunc { get; set; }

        public StyleViolation Check(ProjectElement element) => CheckFunc?.Invoke(element);
    }

    /// <summary>
    /// A style violation.
    /// </summary>
    public class StyleViolation
    {
        public string Rule { get; set; }
        public string Message { get; set; }
        public StyleViolationSeverity Severity { get; set; } = StyleViolationSeverity.Warning;
        public Dictionary<string, object> SuggestedFix { get; set; }
    }

    public enum StyleViolationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Result of style check.
    /// </summary>
    public class StyleCheckResult
    {
        public ProjectElement Element { get; set; }
        public bool ConformsToStyle { get; set; }
        public List<StyleViolation> Violations { get; set; } = new List<StyleViolation>();
    }

    #endregion

    #region Main Engine

    /// <summary>
    /// Main adaptive learning engine combining all learning capabilities.
    /// </summary>
    public class AdaptiveLearningEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public PatternLearner PatternLearner { get; }
        public PreferenceLearner PreferenceLearner { get; }
        public ProjectStyleLearner ProjectStyleLearner { get; }

        private readonly ConcurrentQueue<LearningObservation> _observationQueue;
        private readonly CancellationTokenSource _processingCts;
        private Task _processingTask;

        public bool IsLearning => _processingTask != null && !_processingTask.IsCompleted;

        public AdaptiveLearningEngine(string storagePath = null)
        {
            PatternLearner = new PatternLearner();
            PreferenceLearner = new PreferenceLearner(storagePath);
            ProjectStyleLearner = new ProjectStyleLearner();
            _observationQueue = new ConcurrentQueue<LearningObservation>();
            _processingCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts background learning processing.
        /// </summary>
        public void StartLearning()
        {
            if (IsLearning) return;

            _processingTask = Task.Run(async () =>
            {
                Logger.Info("Started adaptive learning engine");

                while (!_processingCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_observationQueue.TryDequeue(out var observation))
                        {
                            ProcessObservation(observation);
                        }
                        else
                        {
                            await Task.Delay(100, _processingCts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Error processing observation");
                    }
                }

                Logger.Info("Stopped adaptive learning engine");
            }, _processingCts.Token);
        }

        /// <summary>
        /// Stops learning and saves state.
        /// </summary>
        public async Task StopLearningAsync()
        {
            _processingCts.Cancel();

            if (_processingTask != null)
            {
                await _processingTask;
            }

            PreferenceLearner.SavePreferences();
        }

        /// <summary>
        /// Records an observation for learning.
        /// </summary>
        public void RecordObservation(LearningObservation observation)
        {
            _observationQueue.Enqueue(observation);
        }

        /// <summary>
        /// Gets intelligent suggestions based on learned patterns.
        /// </summary>
        public List<LearnedSuggestion> GetSuggestions(LearningContext context, int maxSuggestions = 5)
        {
            var suggestions = new List<LearnedSuggestion>();

            // Get pattern-based suggestions
            var patterns = PatternLearner.GetApplicablePatterns(context, maxSuggestions);
            foreach (var pattern in patterns)
            {
                suggestions.Add(new LearnedSuggestion
                {
                    Type = SuggestionType.Pattern,
                    Title = pattern.Name,
                    Description = pattern.Description ?? $"Learned from {pattern.UsageCount} occurrences",
                    Confidence = pattern.Confidence,
                    Elements = pattern.Elements,
                    Parameters = pattern.Parameters
                });
            }

            // Get preference-based suggestions
            var preferredOption = PreferenceLearner.GetPreferredOption(
                context.CurrentCategory ?? "General",
                context.AvailableOptions ?? new List<string>());

            if (!string.IsNullOrEmpty(preferredOption))
            {
                suggestions.Add(new LearnedSuggestion
                {
                    Type = SuggestionType.Preference,
                    Title = $"Preferred: {preferredOption}",
                    Description = "Based on your previous choices",
                    Confidence = 0.8f
                });
            }

            return suggestions.OrderByDescending(s => s.Confidence).Take(maxSuggestions).ToList();
        }

        /// <summary>
        /// Analyzes a project to learn its style.
        /// </summary>
        public void LearnFromProject(IEnumerable<ProjectElement> elements)
        {
            ProjectStyleLearner.AnalyzeProjectElements(elements);
        }

        private void ProcessObservation(LearningObservation observation)
        {
            // Learn patterns
            PatternLearner.Observe(observation);

            // Learn preferences
            if (!string.IsNullOrEmpty(observation.ChosenOption) && observation.AvailableOptions != null)
            {
                PreferenceLearner.RecordChoice(
                    observation.ElementCategory,
                    observation.ChosenOption,
                    observation.AvailableOptions);
            }

            // Learn parameter preferences
            if (observation.Parameters != null)
            {
                foreach (var param in observation.Parameters)
                {
                    PreferenceLearner.RecordParameterValue(
                        observation.ElementCategory,
                        param.Key,
                        param.Value);
                }
            }
        }
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Tracks the distribution of values for a parameter in a given context.
    /// Used by PatternLearner to detect consistent parameter value patterns.
    /// </summary>
    public class ParameterDistribution
    {
        public List<double> NumericValues { get; } = new List<double>();
        public ConcurrentDictionary<string, int> CategoricalCounts { get; } = new ConcurrentDictionary<string, int>();
        public int TotalObservations { get; set; }

        /// <summary>
        /// Gets the most common numeric value (mode), or null if no numeric data.
        /// </summary>
        public double? GetNumericMode()
        {
            if (!NumericValues.Any()) return null;
            return NumericValues
                .GroupBy(d => Math.Round(d, 2))
                .OrderByDescending(g => g.Count())
                .First().Key;
        }

        /// <summary>
        /// Gets the most common categorical value, or null if no categorical data.
        /// </summary>
        public string GetCategoricalMode()
        {
            if (!CategoricalCounts.Any()) return null;
            return CategoricalCounts
                .OrderByDescending(kv => kv.Value)
                .First().Key;
        }
    }

    /// <summary>
    /// An observation for learning.
    /// </summary>
    public class LearningObservation
    {
        public string ElementId { get; set; }
        public string ElementCategory { get; set; }
        public string ElementType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public LearningContext Context { get; set; }
        public List<NearbyElement> NearbyElements { get; set; }
        public string ChosenOption { get; set; }
        public List<string> AvailableOptions { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Context for learning.
    /// </summary>
    public class LearningContext
    {
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public string ProjectPhase { get; set; }
        public string CurrentCategory { get; set; }
        public List<string> AvailableOptions { get; set; }
    }

    /// <summary>
    /// A nearby element for co-occurrence learning.
    /// </summary>
    public class NearbyElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public double Distance { get; set; }
    }

    /// <summary>
    /// A project element for style learning.
    /// </summary>
    public class ProjectElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// A suggestion derived from learning.
    /// </summary>
    public class LearnedSuggestion
    {
        public SuggestionType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public List<string> Elements { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public enum SuggestionType
    {
        Pattern,
        Preference,
        Style,
        Historical
    }

    /// <summary>
    /// Possible outcomes of a learning episode from user's perspective.
    /// </summary>
    public enum UserEpisodeOutcome
    {
        Accepted,    // User accepted the action
        Corrected,   // User made modifications
        Undone,      // User undid the action
        Failed,      // Action failed to execute
        Abandoned    // User abandoned the flow
    }

    #endregion
}
