// StingBIM.AI.Core.Memory.SemanticMemory
// Fact-based knowledge storage with semantic search
// Master Proposal Reference: Part 2.2 Strategy 5 - Contextual Memory Networks (Long-term: Semantic)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Core.Memory
{
    /// <summary>
    /// Semantic memory stores facts and knowledge.
    /// "Kitchens need 2m² counter" - domain knowledge and learned facts.
    /// Capacity: Unlimited | Duration: Forever (Part 2.2)
    /// </summary>
    public class SemanticMemory
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _storagePath;
        private readonly object _lock = new object();
        private Dictionary<string, SemanticFact> _facts;
        private Dictionary<string, List<string>> _categoryIndex;

        public SemanticMemory()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "semantic_memory.json"))
        {
        }

        public SemanticMemory(string storagePath)
        {
            _storagePath = storagePath;
            _facts = new Dictionary<string, SemanticFact>(StringComparer.OrdinalIgnoreCase);
            _categoryIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads semantic memory from storage.
        /// </summary>
        public async Task LoadAsync()
        {
            if (File.Exists(_storagePath))
            {
                try
                {
                    var json = await Task.Run(() => File.ReadAllText(_storagePath));
                    var facts = JsonConvert.DeserializeObject<List<SemanticFact>>(json) ?? new List<SemanticFact>();

                    lock (_lock)
                    {
                        _facts.Clear();
                        _categoryIndex.Clear();

                        foreach (var fact in facts)
                        {
                            _facts[fact.Id] = fact;
                            IndexFact(fact);
                        }
                    }

                    Logger.Info($"Loaded {_facts.Count} semantic facts");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load semantic memory");
                }
            }
        }

        /// <summary>
        /// Saves semantic memory to storage.
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    json = JsonConvert.SerializeObject(_facts.Values.ToList(), Formatting.Indented);
                }
                var directory = Path.GetDirectoryName(_storagePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await Task.Run(() => File.WriteAllText(_storagePath, json));
                Logger.Debug("Semantic memory saved");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save semantic memory");
            }
        }

        /// <summary>
        /// Stores a new fact in semantic memory.
        /// </summary>
        public void StoreFact(SemanticFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (string.IsNullOrWhiteSpace(fact.Subject)) throw new ArgumentException("Fact subject cannot be null or empty.", nameof(fact));
            if (string.IsNullOrWhiteSpace(fact.Predicate)) throw new ArgumentException("Fact predicate cannot be null or empty.", nameof(fact));

            lock (_lock)
            {
                if (string.IsNullOrEmpty(fact.Id))
                {
                    fact.Id = Guid.NewGuid().ToString();
                }
                fact.LastUpdated = DateTime.Now;

                _facts[fact.Id] = fact;
                IndexFact(fact);
            }
            Logger.Debug($"Stored fact: {fact.Subject} - {fact.Predicate} - {fact.Object}");
        }

        /// <summary>
        /// Retrieves a fact by ID.
        /// </summary>
        public SemanticFact GetFact(string id)
        {
            lock (_lock)
            {
                return _facts.GetValueOrDefault(id);
            }
        }

        /// <summary>
        /// Queries facts by subject.
        /// </summary>
        public IEnumerable<SemanticFact> QueryBySubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject cannot be null or empty.", nameof(subject));

            lock (_lock)
            {
                return _facts.Values
                    .Where(f => f.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Queries facts by category.
        /// </summary>
        public IEnumerable<SemanticFact> QueryByCategory(string category)
        {
            lock (_lock)
            {
                if (_categoryIndex.TryGetValue(category, out var factIds))
                {
                    return factIds
                        .Select(id => _facts.GetValueOrDefault(id))
                        .Where(f => f != null)
                        .ToList();
                }
                return Enumerable.Empty<SemanticFact>();
            }
        }

        /// <summary>
        /// Searches facts using text query.
        /// </summary>
        public IEnumerable<SemanticFact> Search(string query, int maxResults = 10)
        {
            lock (_lock)
            {
                var terms = query.ToLowerInvariant().Split(' ');

                return _facts.Values
                    .Select(f => new
                    {
                        Fact = f,
                        Score = CalculateRelevance(f, terms)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Fact.Confidence)
                    .Take(maxResults)
                    .Select(x => x.Fact)
                    .ToList();
            }
        }

        /// <summary>
        /// Queries facts using semantic triple pattern.
        /// </summary>
        public IEnumerable<SemanticFact> QueryTriple(string subject = null, string predicate = null, string obj = null)
        {
            lock (_lock)
            {
                IEnumerable<SemanticFact> query = _facts.Values;

                if (!string.IsNullOrEmpty(subject))
                    query = query.Where(f => f.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(predicate))
                    query = query.Where(f => f.Predicate.Equals(predicate, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(obj))
                    query = query.Where(f => f.Object.Equals(obj, StringComparison.OrdinalIgnoreCase));

                return query.ToList();
            }
        }

        /// <summary>
        /// Updates the confidence of a fact based on reinforcement.
        /// </summary>
        public void ReinforceFact(string factId, float reinforcement)
        {
            lock (_lock)
            {
                if (_facts.TryGetValue(factId, out var fact))
                {
                    fact.Confidence = Math.Clamp(fact.Confidence + reinforcement, 0f, 1f);
                    fact.ReinforcementCount++;
                    fact.LastUpdated = DateTime.Now;
                }
            }
        }

        private void IndexFact(SemanticFact fact)
        {
            if (!string.IsNullOrEmpty(fact.Category))
            {
                if (!_categoryIndex.ContainsKey(fact.Category))
                {
                    _categoryIndex[fact.Category] = new List<string>();
                }
                if (!_categoryIndex[fact.Category].Contains(fact.Id))
                {
                    _categoryIndex[fact.Category].Add(fact.Id);
                }
            }
        }

        private float CalculateRelevance(SemanticFact fact, string[] terms)
        {
            float score = 0;
            var searchable = $"{fact.Subject} {fact.Predicate} {fact.Object} {fact.Description}".ToLowerInvariant();

            foreach (var term in terms)
            {
                if (searchable.Contains(term))
                {
                    score += 1f;
                }
            }

            return score * fact.Confidence;
        }

        /// <summary>
        /// Gets the total number of facts stored.
        /// </summary>
        public int Count
        {
            get { lock (_lock) { return _facts.Count; } }
        }
    }

    /// <summary>
    /// Represents a semantic fact (knowledge triple).
    /// </summary>
    public class SemanticFact
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Object { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Source { get; set; }
        public float Confidence { get; set; } = 0.8f;
        public int ReinforcementCount { get; set; }
        public DateTime LastUpdated { get; set; }
        public float[] Embedding { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
