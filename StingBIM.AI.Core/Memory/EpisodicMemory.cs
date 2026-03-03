// StingBIM.AI.Core.Memory.EpisodicMemory
// Event-based memory for recording user actions and outcomes
// Master Proposal Reference: Part 2.2 Strategy 5 - Contextual Memory Networks (Long-term: Episodic)

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
    /// Episodic memory stores events and experiences.
    /// "User moved the door 3 times" - learns from repeated actions.
    /// Capacity: Unlimited | Duration: Forever (Part 2.2)
    /// </summary>
    public class EpisodicMemory
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _storagePath;
        private readonly object _lock = new object();
        private List<Episode> _episodes;
        private readonly int _maxEpisodesInMemory;

        public EpisodicMemory()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "episodic_memory.json"))
        {
        }

        public EpisodicMemory(string storagePath, int maxEpisodesInMemory = 1000)
        {
            _storagePath = storagePath;
            _maxEpisodesInMemory = maxEpisodesInMemory;
            _episodes = new List<Episode>();
        }

        /// <summary>
        /// Loads episodes from persistent storage.
        /// </summary>
        public async Task LoadAsync()
        {
            if (File.Exists(_storagePath))
            {
                try
                {
                    var json = await Task.Run(() => File.ReadAllText(_storagePath));
                    lock (_lock)
                    {
                        _episodes = JsonConvert.DeserializeObject<List<Episode>>(json) ?? new List<Episode>();
                    }
                    Logger.Info($"Loaded {_episodes.Count} episodes from storage");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load episodic memory");
                    _episodes = new List<Episode>();
                }
            }
        }

        /// <summary>
        /// Saves episodes to persistent storage.
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    json = JsonConvert.SerializeObject(_episodes, Formatting.Indented);
                }
                var directory = Path.GetDirectoryName(_storagePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await Task.Run(() => File.WriteAllText(_storagePath, json));
                Logger.Debug("Episodic memory saved");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save episodic memory");
            }
        }

        /// <summary>
        /// Records a new episode.
        /// </summary>
        public void RecordEpisode(Episode episode)
        {
            lock (_lock)
            {
                episode.Id = Guid.NewGuid().ToString();
                episode.Timestamp = DateTime.Now;
                _episodes.Add(episode);

                // Trim if exceeds memory limit
                if (_episodes.Count > _maxEpisodesInMemory)
                {
                    // Keep most recent and most important
                    _episodes = _episodes
                        .OrderByDescending(e => e.Importance)
                        .ThenByDescending(e => e.Timestamp)
                        .Take(_maxEpisodesInMemory)
                        .ToList();
                }
            }
            Logger.Debug($"Recorded episode: {episode.Action} - {episode.Outcome}");
        }

        /// <summary>
        /// Finds similar past episodes for learning.
        /// </summary>
        public IEnumerable<Episode> FindSimilarEpisodes(string action, string context = null, int maxResults = 10)
        {
            lock (_lock)
            {
                var query = _episodes.Where(e => e.Action.Contains(action, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(context))
                {
                    query = query.Where(e => e.Context?.Contains(context, StringComparison.OrdinalIgnoreCase) == true);
                }

                return query
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxResults)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets episodes where the user made corrections.
        /// Used for learning from mistakes.
        /// </summary>
        public IEnumerable<Episode> GetCorrectionEpisodes()
        {
            lock (_lock)
            {
                return _episodes
                    .Where(e => e.Outcome == EpisodeOutcome.Corrected || e.Outcome == EpisodeOutcome.Undone)
                    .OrderByDescending(e => e.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets repeated action patterns (e.g., "user moved door 3 times").
        /// </summary>
        public IEnumerable<ActionPattern> GetRepeatedPatterns(TimeSpan window, int minOccurrences = 3)
        {
            lock (_lock)
            {
                var cutoff = DateTime.Now - window;
                var recentEpisodes = _episodes.Where(e => e.Timestamp >= cutoff);

                return recentEpisodes
                    .GroupBy(e => e.Action)
                    .Where(g => g.Count() >= minOccurrences)
                    .Select(g => new ActionPattern
                    {
                        Action = g.Key,
                        Occurrences = g.Count(),
                        MostRecentTime = g.Max(e => e.Timestamp),
                        SuccessRate = g.Count(e => e.Outcome == EpisodeOutcome.Accepted) / (float)g.Count()
                    })
                    .OrderByDescending(p => p.Occurrences)
                    .ToList();
            }
        }

        /// <summary>
        /// Calculates success rate for a specific action type.
        /// </summary>
        public float GetActionSuccessRate(string action)
        {
            lock (_lock)
            {
                var relevant = _episodes.Where(e => e.Action.Contains(action, StringComparison.OrdinalIgnoreCase)).ToList();
                if (relevant.Count == 0) return 0.5f; // No data, assume neutral

                return relevant.Count(e => e.Outcome == EpisodeOutcome.Accepted) / (float)relevant.Count;
            }
        }

        /// <summary>
        /// Gets total episode count.
        /// </summary>
        public int Count
        {
            get { lock (_lock) { return _episodes.Count; } }
        }
    }

    /// <summary>
    /// Represents a single episode (event) in memory.
    /// </summary>
    public class Episode
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string ProjectId { get; set; }
        public string Action { get; set; }
        public string Context { get; set; }
        public EpisodeOutcome Outcome { get; set; }
        public string UserCorrection { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public float Importance { get; set; } = 0.5f;
        public List<string> RelatedElementIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// Possible outcomes of an episode.
    /// </summary>
    public enum EpisodeOutcome
    {
        Accepted,    // User accepted the action
        Corrected,   // User made modifications
        Undone,      // User undid the action
        Failed,      // Action failed to execute
        Abandoned    // User abandoned the flow
    }

    /// <summary>
    /// Represents a repeated action pattern.
    /// </summary>
    public class ActionPattern
    {
        public string Action { get; set; }
        public int Occurrences { get; set; }
        public DateTime MostRecentTime { get; set; }
        public float SuccessRate { get; set; }
    }
}
