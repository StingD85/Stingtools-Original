// StingBIM.AI.Intelligence.Memory.DecisionMemory
// Episodic memory for design decisions - remembers what, why, and context
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Enhancement

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Intelligence.Memory
{
    #region Decision Journal

    /// <summary>
    /// Records and retrieves design decisions with full context.
    /// Enables learning from past decisions and explaining reasoning.
    /// </summary>
    public class DecisionJournal
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, DecisionRecord> _decisions;
        private readonly List<DecisionRecord> _chronologicalHistory;
        private readonly DecisionIndex _index;
        private readonly string _storagePath;
        private readonly object _historyLock = new object();

        public DecisionJournal(string storagePath = null)
        {
            _decisions = new ConcurrentDictionary<string, DecisionRecord>();
            _chronologicalHistory = new List<DecisionRecord>();
            _index = new DecisionIndex();
            _storagePath = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "decision_journal.json");

            LoadJournal();
        }

        /// <summary>
        /// Records a new design decision.
        /// </summary>
        public DecisionRecord RecordDecision(DecisionInput input)
        {
            var record = new DecisionRecord
            {
                DecisionId = Guid.NewGuid().ToString("N").Substring(0, 12),
                Timestamp = DateTime.UtcNow,
                ProjectId = input.ProjectId,
                UserId = input.UserId,

                // What was decided
                DecisionType = input.DecisionType,
                ElementCategory = input.ElementCategory,
                ElementId = input.ElementId,
                ChosenOption = input.ChosenOption,
                AlternativesConsidered = input.Alternatives ?? new List<Alternative>(),

                // Why it was decided
                Rationale = input.Rationale,
                Constraints = input.Constraints ?? new List<Constraint>(),
                Goals = input.Goals ?? new List<string>(),

                // Context
                Context = input.Context ?? new DecisionContext(),
                RelatedDecisions = input.RelatedDecisionIds ?? new List<string>(),

                // Outcome tracking
                Status = DecisionStatus.Made,
                ConfidenceLevel = input.ConfidenceLevel
            };

            _decisions[record.DecisionId] = record;

            lock (_historyLock)
            {
                _chronologicalHistory.Add(record);
            }

            // Update indices
            _index.IndexDecision(record);

            Logger.Info($"Recorded decision: {record.DecisionId} - {record.DecisionType}");

            return record;
        }

        /// <summary>
        /// Updates the outcome of a decision after implementation.
        /// </summary>
        public void RecordOutcome(string decisionId, DecisionOutcome outcome)
        {
            if (_decisions.TryGetValue(decisionId, out var record))
            {
                record.Outcome = outcome;
                record.Status = outcome.WasSuccessful ? DecisionStatus.Successful : DecisionStatus.Revised;
                record.OutcomeRecordedAt = DateTime.UtcNow;

                Logger.Info($"Recorded outcome for decision {decisionId}: {(outcome.WasSuccessful ? "Successful" : "Revised")}");
            }
        }

        /// <summary>
        /// Finds similar past decisions.
        /// </summary>
        public List<DecisionRecord> FindSimilarDecisions(DecisionQuery query, int maxResults = 10)
        {
            var candidates = _decisions.Values.AsEnumerable();

            // Filter by decision type
            if (!string.IsNullOrEmpty(query.DecisionType))
            {
                candidates = candidates.Where(d => d.DecisionType == query.DecisionType);
            }

            // Filter by element category
            if (!string.IsNullOrEmpty(query.ElementCategory))
            {
                candidates = candidates.Where(d => d.ElementCategory == query.ElementCategory);
            }

            // Filter by context
            if (!string.IsNullOrEmpty(query.RoomType))
            {
                candidates = candidates.Where(d => d.Context?.RoomType == query.RoomType);
            }

            if (!string.IsNullOrEmpty(query.BuildingType))
            {
                candidates = candidates.Where(d => d.Context?.BuildingType == query.BuildingType);
            }

            // Score by similarity
            var scored = candidates.Select(d => new
            {
                Decision = d,
                Score = CalculateSimilarity(d, query)
            })
            .Where(x => x.Score > 0.3f)
            .OrderByDescending(x => x.Score)
            .Take(maxResults);

            return scored.Select(x => x.Decision).ToList();
        }

        /// <summary>
        /// Gets decisions that led to successful outcomes in similar situations.
        /// </summary>
        public List<DecisionRecommendation> GetRecommendationsFromHistory(DecisionQuery query)
        {
            var similar = FindSimilarDecisions(query, 20);
            var successful = similar.Where(d => d.Status == DecisionStatus.Successful).ToList();

            if (!successful.Any())
            {
                return new List<DecisionRecommendation>();
            }

            // Group by chosen option and rank by success
            var recommendations = successful
                .GroupBy(d => d.ChosenOption)
                .Select(g => new DecisionRecommendation
                {
                    RecommendedOption = g.Key,
                    BasedOnDecisions = g.Select(d => d.DecisionId).ToList(),
                    SuccessRate = 1.0f, // All in this group were successful
                    TypicalRationale = g.First().Rationale,
                    SampleContexts = g.Select(d => d.Context).Take(3).ToList(),
                    Confidence = Math.Min(g.Count() * 0.2f, 1.0f)
                })
                .OrderByDescending(r => r.Confidence)
                .ToList();

            return recommendations;
        }

        /// <summary>
        /// Gets the full decision trail for an element.
        /// </summary>
        public List<DecisionRecord> GetElementHistory(string elementId)
        {
            return _index.GetByElement(elementId)
                .Select(id => _decisions.TryGetValue(id, out var d) ? d : null)
                .Where(d => d != null)
                .OrderBy(d => d.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Explains why a decision was made.
        /// </summary>
        public DecisionExplanation ExplainDecision(string decisionId)
        {
            if (!_decisions.TryGetValue(decisionId, out var record))
            {
                return null;
            }

            var explanation = new DecisionExplanation
            {
                DecisionId = decisionId,
                Summary = $"Chose '{record.ChosenOption}' for {record.DecisionType}",
                DetailedRationale = record.Rationale,
                ConstraintsConsidered = record.Constraints,
                GoalsAddressed = record.Goals,
                AlternativesRejected = record.AlternativesConsidered
                    .Where(a => a.Option != record.ChosenOption)
                    .Select(a => new RejectedAlternative
                    {
                        Option = a.Option,
                        RejectionReason = a.RejectionReason ?? "Not selected",
                        Score = a.Score
                    })
                    .ToList()
            };

            // Add context explanation
            if (record.Context != null)
            {
                explanation.ContextFactors = new List<string>();

                if (!string.IsNullOrEmpty(record.Context.RoomType))
                    explanation.ContextFactors.Add($"Room type: {record.Context.RoomType}");

                if (!string.IsNullOrEmpty(record.Context.BuildingType))
                    explanation.ContextFactors.Add($"Building type: {record.Context.BuildingType}");

                if (record.Context.NearbyElements?.Any() == true)
                    explanation.ContextFactors.Add($"Adjacent to: {string.Join(", ", record.Context.NearbyElements.Take(3))}");
            }

            // Add outcome if available
            if (record.Outcome != null)
            {
                explanation.OutcomeSummary = record.Outcome.WasSuccessful
                    ? "This decision proved successful"
                    : $"This decision was later revised: {record.Outcome.LessonsLearned}";
            }

            return explanation;
        }

        /// <summary>
        /// Finds decisions that were later revised (learning opportunities).
        /// </summary>
        public List<DecisionRecord> FindRevisedDecisions(string decisionType = null)
        {
            var revised = _decisions.Values.Where(d => d.Status == DecisionStatus.Revised);

            if (!string.IsNullOrEmpty(decisionType))
            {
                revised = revised.Where(d => d.DecisionType == decisionType);
            }

            return revised.OrderByDescending(d => d.Timestamp).ToList();
        }

        /// <summary>
        /// Gets statistics about decision patterns.
        /// </summary>
        public DecisionStatistics GetStatistics()
        {
            var allDecisions = _decisions.Values.ToList();

            return new DecisionStatistics
            {
                TotalDecisions = allDecisions.Count,
                SuccessfulDecisions = allDecisions.Count(d => d.Status == DecisionStatus.Successful),
                RevisedDecisions = allDecisions.Count(d => d.Status == DecisionStatus.Revised),
                DecisionsByType = allDecisions
                    .GroupBy(d => d.DecisionType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                DecisionsByCategory = allDecisions
                    .GroupBy(d => d.ElementCategory)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
                MostCommonChoices = allDecisions
                    .GroupBy(d => $"{d.DecisionType}:{d.ChosenOption}")
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageConfidence = allDecisions.Any()
                    ? allDecisions.Average(d => d.ConfidenceLevel)
                    : 0f
            };
        }

        /// <summary>
        /// Saves the journal to storage.
        /// </summary>
        public void SaveJournal()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new JournalData
                {
                    Decisions = _decisions.Values.ToList(),
                    LastSaved = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_storagePath, json);

                Logger.Debug($"Saved {_decisions.Count} decisions to journal");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save decision journal");
            }
        }

        private void LoadJournal()
        {
            try
            {
                if (!File.Exists(_storagePath))
                    return;

                var json = File.ReadAllText(_storagePath);
                var data = JsonSerializer.Deserialize<JournalData>(json);

                if (data?.Decisions != null)
                {
                    foreach (var decision in data.Decisions)
                    {
                        _decisions[decision.DecisionId] = decision;
                        _chronologicalHistory.Add(decision);
                        _index.IndexDecision(decision);
                    }

                    Logger.Info($"Loaded {_decisions.Count} decisions from journal");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load decision journal");
            }
        }

        private float CalculateSimilarity(DecisionRecord record, DecisionQuery query)
        {
            float score = 0f;
            int factors = 0;

            if (!string.IsNullOrEmpty(query.DecisionType) && record.DecisionType == query.DecisionType)
            {
                score += 1.0f;
                factors++;
            }

            if (!string.IsNullOrEmpty(query.ElementCategory) && record.ElementCategory == query.ElementCategory)
            {
                score += 0.8f;
                factors++;
            }

            if (!string.IsNullOrEmpty(query.RoomType) && record.Context?.RoomType == query.RoomType)
            {
                score += 0.6f;
                factors++;
            }

            if (!string.IsNullOrEmpty(query.BuildingType) && record.Context?.BuildingType == query.BuildingType)
            {
                score += 0.5f;
                factors++;
            }

            if (query.Constraints?.Any() == true && record.Constraints?.Any() == true)
            {
                var matchingConstraints = query.Constraints
                    .Count(qc => record.Constraints.Any(rc => rc.Type == qc.Type));
                score += matchingConstraints * 0.3f;
                factors++;
            }

            return factors > 0 ? score / factors : 0f;
        }

        private class JournalData
        {
            public List<DecisionRecord> Decisions { get; set; }
            public DateTime LastSaved { get; set; }
        }
    }

    /// <summary>
    /// Index for fast decision lookups.
    /// </summary>
    public class DecisionIndex
    {
        private readonly ConcurrentDictionary<string, List<string>> _byType;
        private readonly ConcurrentDictionary<string, List<string>> _byCategory;
        private readonly ConcurrentDictionary<string, List<string>> _byElement;
        private readonly ConcurrentDictionary<string, List<string>> _byRoomType;

        public DecisionIndex()
        {
            _byType = new ConcurrentDictionary<string, List<string>>();
            _byCategory = new ConcurrentDictionary<string, List<string>>();
            _byElement = new ConcurrentDictionary<string, List<string>>();
            _byRoomType = new ConcurrentDictionary<string, List<string>>();
        }

        public void IndexDecision(DecisionRecord record)
        {
            AddToIndex(_byType, record.DecisionType, record.DecisionId);
            AddToIndex(_byCategory, record.ElementCategory ?? "Unknown", record.DecisionId);

            if (!string.IsNullOrEmpty(record.ElementId))
                AddToIndex(_byElement, record.ElementId, record.DecisionId);

            if (!string.IsNullOrEmpty(record.Context?.RoomType))
                AddToIndex(_byRoomType, record.Context.RoomType, record.DecisionId);
        }

        public List<string> GetByType(string type) =>
            _byType.TryGetValue(type, out var list) ? list : new List<string>();

        public List<string> GetByCategory(string category) =>
            _byCategory.TryGetValue(category, out var list) ? list : new List<string>();

        public List<string> GetByElement(string elementId) =>
            _byElement.TryGetValue(elementId, out var list) ? list : new List<string>();

        public List<string> GetByRoomType(string roomType) =>
            _byRoomType.TryGetValue(roomType, out var list) ? list : new List<string>();

        private void AddToIndex(ConcurrentDictionary<string, List<string>> index, string key, string decisionId)
        {
            index.AddOrUpdate(key,
                new List<string> { decisionId },
                (_, list) => { list.Add(decisionId); return list; });
        }
    }

    #endregion

    #region Decision Records

    /// <summary>
    /// Input for recording a decision.
    /// </summary>
    public class DecisionInput
    {
        public string ProjectId { get; set; }
        public string UserId { get; set; }
        public string DecisionType { get; set; }
        public string ElementCategory { get; set; }
        public string ElementId { get; set; }
        public string ChosenOption { get; set; }
        public List<Alternative> Alternatives { get; set; }
        public string Rationale { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<string> Goals { get; set; }
        public DecisionContext Context { get; set; }
        public List<string> RelatedDecisionIds { get; set; }
        public float ConfidenceLevel { get; set; } = 0.8f;
    }

    /// <summary>
    /// A recorded design decision.
    /// </summary>
    public class DecisionRecord
    {
        public string DecisionId { get; set; }
        public DateTime Timestamp { get; set; }
        public string ProjectId { get; set; }
        public string UserId { get; set; }

        // What
        public string DecisionType { get; set; }
        public string ElementCategory { get; set; }
        public string ElementId { get; set; }
        public string ChosenOption { get; set; }
        public List<Alternative> AlternativesConsidered { get; set; }

        // Why
        public string Rationale { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<string> Goals { get; set; }

        // Context
        public DecisionContext Context { get; set; }
        public List<string> RelatedDecisions { get; set; }

        // Outcome
        public DecisionStatus Status { get; set; }
        public DecisionOutcome Outcome { get; set; }
        public DateTime? OutcomeRecordedAt { get; set; }
        public float ConfidenceLevel { get; set; }
    }

    /// <summary>
    /// An alternative that was considered.
    /// </summary>
    public class Alternative
    {
        public string Option { get; set; }
        public float Score { get; set; }
        public string RejectionReason { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// A constraint that influenced the decision.
    /// </summary>
    public class Constraint
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public bool IsHardConstraint { get; set; }
    }

    /// <summary>
    /// Context surrounding a decision.
    /// </summary>
    public class DecisionContext
    {
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public string ProjectPhase { get; set; }
        public List<string> NearbyElements { get; set; }
        public Dictionary<string, object> SpatialContext { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; }
    }

    /// <summary>
    /// Outcome of a decision after implementation.
    /// </summary>
    public class DecisionOutcome
    {
        public bool WasSuccessful { get; set; }
        public string ActualResult { get; set; }
        public string LessonsLearned { get; set; }
        public string RevisedTo { get; set; }
        public List<string> IssuesEncountered { get; set; }
    }

    public enum DecisionStatus
    {
        Made,           // Decision recorded
        Implemented,    // Decision implemented
        Successful,     // Verified successful
        Revised,        // Had to be changed
        Reverted        // Completely undone
    }

    #endregion

    #region Query and Results

    /// <summary>
    /// Query for finding similar decisions.
    /// </summary>
    public class DecisionQuery
    {
        public string DecisionType { get; set; }
        public string ElementCategory { get; set; }
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<string> Goals { get; set; }
    }

    /// <summary>
    /// A recommendation based on past decisions.
    /// </summary>
    public class DecisionRecommendation
    {
        public string RecommendedOption { get; set; }
        public List<string> BasedOnDecisions { get; set; }
        public float SuccessRate { get; set; }
        public float Confidence { get; set; }
        public string TypicalRationale { get; set; }
        public List<DecisionContext> SampleContexts { get; set; }
    }

    /// <summary>
    /// Explanation of a decision.
    /// </summary>
    public class DecisionExplanation
    {
        public string DecisionId { get; set; }
        public string Summary { get; set; }
        public string DetailedRationale { get; set; }
        public List<Constraint> ConstraintsConsidered { get; set; }
        public List<string> GoalsAddressed { get; set; }
        public List<string> ContextFactors { get; set; }
        public List<RejectedAlternative> AlternativesRejected { get; set; }
        public string OutcomeSummary { get; set; }
    }

    /// <summary>
    /// An alternative that was rejected.
    /// </summary>
    public class RejectedAlternative
    {
        public string Option { get; set; }
        public string RejectionReason { get; set; }
        public float Score { get; set; }
    }

    /// <summary>
    /// Statistics about decisions.
    /// </summary>
    public class DecisionStatistics
    {
        public int TotalDecisions { get; set; }
        public int SuccessfulDecisions { get; set; }
        public int RevisedDecisions { get; set; }
        public Dictionary<string, int> DecisionsByType { get; set; }
        public Dictionary<string, int> DecisionsByCategory { get; set; }
        public Dictionary<string, int> MostCommonChoices { get; set; }
        public float AverageConfidence { get; set; }
    }

    #endregion
}
