// ============================================================================
// StingBIM AI - Training Data Loader
// Loads and indexes training data from JSONL files for Q&A and task execution
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.Training
{
    /// <summary>
    /// Loads and manages training data for AI knowledge base and executive tasks
    /// </summary>
    public class TrainingDataLoader
    {
        private static readonly Lazy<TrainingDataLoader> _instance =
            new Lazy<TrainingDataLoader>(() => new TrainingDataLoader());
        public static TrainingDataLoader Instance => _instance.Value;

        private readonly Dictionary<string, KnowledgeEntry> _knowledgeBase;
        private readonly Dictionary<string, List<TaskTemplate>> _taskTemplates;
        private readonly Dictionary<string, List<KnowledgeEntry>> _categoryIndex;
        private readonly object _lock = new object();
        private bool _isLoaded;

        public event EventHandler<LoadProgressEventArgs> LoadProgress;

        public int KnowledgeEntryCount => _knowledgeBase.Count;
        public int TaskTemplateCount => _taskTemplates.Values.Sum(t => t.Count);
        public bool IsLoaded => _isLoaded;

        private TrainingDataLoader()
        {
            _knowledgeBase = new Dictionary<string, KnowledgeEntry>(StringComparer.OrdinalIgnoreCase);
            _taskTemplates = new Dictionary<string, List<TaskTemplate>>(StringComparer.OrdinalIgnoreCase);
            _categoryIndex = new Dictionary<string, List<KnowledgeEntry>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load all training data from the docs directory
        /// </summary>
        public async Task LoadAllAsync(string basePath, CancellationToken cancellationToken = default)
        {
            var docsPath = Path.Combine(basePath, "docs");

            // Load knowledge base (Q&A data)
            var knowledgeFile = Path.Combine(docsPath, "STINGBIM_COMPLETE_TRAINING_DATA.jsonl");
            if (File.Exists(knowledgeFile))
            {
                await LoadKnowledgeBaseAsync(knowledgeFile, cancellationToken);
            }

            // Load executive task templates
            var taskFiles = new[]
            {
                "01_project_brief_parsing.jsonl",
                "02_bep_generation.jsonl",
                "03_schedule_creation.jsonl",
                "04_cost_tracking.jsonl",
                "05_progress_management.jsonl",
                "06_deliverable_generation.jsonl",
                "07_recommendations_engine.jsonl"
            };

            foreach (var taskFile in taskFiles)
            {
                var filePath = Path.Combine(docsPath, taskFile);
                if (File.Exists(filePath))
                {
                    await LoadTaskTemplatesAsync(filePath, cancellationToken);
                }
            }

            _isLoaded = true;
            OnLoadProgress(100, "Training data loaded successfully");
        }

        /// <summary>
        /// Load knowledge base Q&A entries from JSONL file
        /// </summary>
        public async Task LoadKnowledgeBaseAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            var totalLines = lines.Length;
            var processed = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var entry = ParseKnowledgeEntry(line);
                    if (entry != null)
                    {
                        lock (_lock)
                        {
                            // Index by question for exact match
                            var key = NormalizeQuestion(entry.Question);
                            _knowledgeBase[key] = entry;

                            // Index by category
                            if (!string.IsNullOrEmpty(entry.Category))
                            {
                                if (!_categoryIndex.ContainsKey(entry.Category))
                                    _categoryIndex[entry.Category] = new List<KnowledgeEntry>();
                                _categoryIndex[entry.Category].Add(entry);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed entries
                }

                processed++;
                if (processed % 100 == 0)
                {
                    OnLoadProgress((int)(processed * 50.0 / totalLines), $"Loading knowledge: {processed}/{totalLines}");
                }
            }

            OnLoadProgress(50, $"Loaded {_knowledgeBase.Count} knowledge entries");
        }

        /// <summary>
        /// Load task templates from JSONL file
        /// </summary>
        public async Task LoadTaskTemplatesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var template = ParseTaskTemplate(line);
                    if (template != null)
                    {
                        lock (_lock)
                        {
                            if (!_taskTemplates.ContainsKey(template.TaskType))
                                _taskTemplates[template.TaskType] = new List<TaskTemplate>();
                            _taskTemplates[template.TaskType].Add(template);
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed entries
                }
            }
        }

        /// <summary>
        /// Find best matching answer for a question
        /// </summary>
        public KnowledgeMatch FindAnswer(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return null;

            var normalizedQuestion = NormalizeQuestion(question);

            // Try exact match first
            if (_knowledgeBase.TryGetValue(normalizedQuestion, out var exactMatch))
            {
                return new KnowledgeMatch
                {
                    Entry = exactMatch,
                    Score = 1.0,
                    MatchType = "exact"
                };
            }

            // Try fuzzy matching
            var bestMatch = FindBestFuzzyMatch(normalizedQuestion);
            if (bestMatch != null && bestMatch.Score > 0.6)
            {
                return bestMatch;
            }

            // Try keyword search
            return FindByKeywords(normalizedQuestion);
        }

        /// <summary>
        /// Find entries by category
        /// </summary>
        public List<KnowledgeEntry> FindByCategory(string category)
        {
            if (_categoryIndex.TryGetValue(category, out var entries))
                return entries.ToList();
            return new List<KnowledgeEntry>();
        }

        /// <summary>
        /// Get task template for a specific task type
        /// </summary>
        public TaskTemplate GetTaskTemplate(string taskType)
        {
            var normalizedType = NormalizeTaskType(taskType);

            if (_taskTemplates.TryGetValue(normalizedType, out var templates) && templates.Any())
            {
                return templates.First();
            }

            // Try partial match
            var partialMatch = _taskTemplates
                .Where(kvp => kvp.Key.Contains(normalizedType) || normalizedType.Contains(kvp.Key))
                .SelectMany(kvp => kvp.Value)
                .FirstOrDefault();

            return partialMatch;
        }

        /// <summary>
        /// Get all task types available
        /// </summary>
        public List<string> GetAvailableTaskTypes()
        {
            return _taskTemplates.Keys.ToList();
        }

        /// <summary>
        /// Search knowledge base with multiple terms
        /// </summary>
        public List<KnowledgeMatch> Search(string query, int maxResults = 10)
        {
            var results = new List<KnowledgeMatch>();
            var queryTerms = Tokenize(query.ToLower());

            foreach (var entry in _knowledgeBase.Values)
            {
                var score = CalculateRelevanceScore(entry, queryTerms);
                if (score > 0.3)
                {
                    results.Add(new KnowledgeMatch
                    {
                        Entry = entry,
                        Score = score,
                        MatchType = "search"
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();
        }

        private KnowledgeEntry ParseKnowledgeEntry(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var entry = new KnowledgeEntry
            {
                Question = GetStringProperty(root, "question"),
                Answer = GetStringProperty(root, "answer")
            };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                entry.Category = GetStringProperty(metadata, "category");
                entry.Type = GetStringProperty(metadata, "type");
                entry.Discipline = GetStringProperty(metadata, "discipline");
                entry.Guid = GetStringProperty(metadata, "guid");
            }

            return entry;
        }

        private TaskTemplate ParseTaskTemplate(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var template = new TaskTemplate
            {
                TaskType = GetStringProperty(root, "task"),
                RawJson = json
            };

            if (root.TryGetProperty("input", out var input))
            {
                template.InputSchema = input.Clone().ToString();
            }

            if (root.TryGetProperty("output", out var output))
            {
                template.OutputSchema = output.Clone().ToString();
            }

            if (root.TryGetProperty("metadata", out var metadata))
            {
                template.Category = GetStringProperty(metadata, "category");
            }

            return template;
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private string NormalizeQuestion(string question)
        {
            return question
                .ToLower()
                .Trim()
                .TrimEnd('?', '.', '!');
        }

        private string NormalizeTaskType(string taskType)
        {
            return taskType
                .ToLower()
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim();
        }

        private KnowledgeMatch FindBestFuzzyMatch(string query)
        {
            KnowledgeMatch bestMatch = null;
            double bestScore = 0;

            var queryTerms = Tokenize(query);

            foreach (var kvp in _knowledgeBase)
            {
                var entryTerms = Tokenize(kvp.Key);
                var score = CalculateJaccardSimilarity(queryTerms, entryTerms);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = new KnowledgeMatch
                    {
                        Entry = kvp.Value,
                        Score = score,
                        MatchType = "fuzzy"
                    };
                }
            }

            return bestMatch;
        }

        private KnowledgeMatch FindByKeywords(string query)
        {
            var queryTerms = Tokenize(query);
            KnowledgeMatch bestMatch = null;
            double bestScore = 0;

            foreach (var entry in _knowledgeBase.Values)
            {
                var score = CalculateRelevanceScore(entry, queryTerms);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = new KnowledgeMatch
                    {
                        Entry = entry,
                        Score = score,
                        MatchType = "keyword"
                    };
                }
            }

            return bestMatch;
        }

        private double CalculateRelevanceScore(KnowledgeEntry entry, HashSet<string> queryTerms)
        {
            var questionTerms = Tokenize(entry.Question.ToLower());
            var answerTerms = Tokenize(entry.Answer.ToLower());

            // Weight question matches higher than answer matches
            var questionMatches = queryTerms.Count(t => questionTerms.Contains(t));
            var answerMatches = queryTerms.Count(t => answerTerms.Contains(t));

            var questionScore = queryTerms.Count > 0 ? (double)questionMatches / queryTerms.Count : 0;
            var answerScore = queryTerms.Count > 0 ? (double)answerMatches / queryTerms.Count : 0;

            return questionScore * 0.7 + answerScore * 0.3;
        }

        private double CalculateJaccardSimilarity(HashSet<string> set1, HashSet<string> set2)
        {
            var intersection = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();
            return union > 0 ? (double)intersection / union : 0;
        }

        private HashSet<string> Tokenize(string text)
        {
            var stopWords = new HashSet<string>
            {
                "a", "an", "the", "is", "are", "was", "were", "be", "been",
                "being", "have", "has", "had", "do", "does", "did", "will",
                "would", "could", "should", "may", "might", "must", "shall",
                "to", "of", "in", "for", "on", "with", "at", "by", "from",
                "as", "into", "through", "during", "before", "after", "above",
                "below", "between", "under", "again", "further", "then", "once",
                "what", "which", "who", "whom", "this", "that", "these", "those",
                "i", "me", "my", "myself", "we", "our", "ours", "ourselves",
                "you", "your", "yours", "yourself", "yourselves", "he", "him",
                "his", "himself", "she", "her", "hers", "herself", "it", "its",
                "itself", "they", "them", "their", "theirs", "themselves"
            };

            var tokens = text
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '_', '(', ')', '[', ']', '{', '}' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .Where(t => t.Length > 2 && !stopWords.Contains(t))
                .ToHashSet();

            return tokens;
        }

        private void OnLoadProgress(int percent, string message)
        {
            LoadProgress?.Invoke(this, new LoadProgressEventArgs { Percent = percent, Message = message });
        }
    }

    #region Data Models

    public class KnowledgeEntry
    {
        public string Question { get; set; }
        public string Answer { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Discipline { get; set; }
        public string Guid { get; set; }
    }

    public class KnowledgeMatch
    {
        public KnowledgeEntry Entry { get; set; }
        public double Score { get; set; }
        public string MatchType { get; set; }
    }

    public class TaskTemplate
    {
        public string TaskType { get; set; }
        public string Category { get; set; }
        public string InputSchema { get; set; }
        public string OutputSchema { get; set; }
        public string RawJson { get; set; }
    }

    public class LoadProgressEventArgs : EventArgs
    {
        public int Percent { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
