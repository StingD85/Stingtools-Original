// StingBIM.AI.Core.Learning.FeedbackCollector
// Collects user feedback for continuous learning
// Master Proposal Reference: Part 2.2 Strategy 1 - Compound Learning Loops

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Core.Learning
{
    /// <summary>
    /// Collects and processes user feedback for learning.
    /// Implements Loop 1 (Immediate) of the Triple Learning Loop.
    /// </summary>
    public class FeedbackCollector
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _storagePath;
        private readonly object _lock = new object();
        private readonly Queue<FeedbackEntry> _pendingFeedback;
        private readonly int _batchSize;

        public FeedbackCollector()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "feedback.json"))
        {
        }

        public FeedbackCollector(string storagePath, int batchSize = 100)
        {
            _storagePath = storagePath;
            _batchSize = batchSize;
            _pendingFeedback = new Queue<FeedbackEntry>();
        }

        /// <summary>
        /// Event fired when new feedback is collected.
        /// </summary>
        public event EventHandler<FeedbackEntry> FeedbackReceived;

        /// <summary>
        /// Records user acceptance of an AI action.
        /// </summary>
        public void RecordAcceptance(string actionId, string action, Dictionary<string, object> context = null)
        {
            var feedback = new FeedbackEntry
            {
                Id = Guid.NewGuid().ToString(),
                ActionId = actionId,
                Action = action,
                Reaction = UserReaction.Accepted,
                Timestamp = DateTime.Now,
                Context = context ?? new Dictionary<string, object>()
            };

            EnqueueFeedback(feedback);
            Logger.Debug($"Feedback: Accepted - {action}");
        }

        /// <summary>
        /// Records user modification of an AI action.
        /// </summary>
        public void RecordModification(string actionId, string originalAction, string modifiedAction, Dictionary<string, object> context = null)
        {
            var feedback = new FeedbackEntry
            {
                Id = Guid.NewGuid().ToString(),
                ActionId = actionId,
                Action = originalAction,
                ModifiedAction = modifiedAction,
                Reaction = UserReaction.Modified,
                Timestamp = DateTime.Now,
                Context = context ?? new Dictionary<string, object>()
            };

            EnqueueFeedback(feedback);
            Logger.Debug($"Feedback: Modified - {originalAction} -> {modifiedAction}");
        }

        /// <summary>
        /// Records user undo of an AI action.
        /// </summary>
        public void RecordUndo(string actionId, string action, Dictionary<string, object> context = null)
        {
            var feedback = new FeedbackEntry
            {
                Id = Guid.NewGuid().ToString(),
                ActionId = actionId,
                Action = action,
                Reaction = UserReaction.Undone,
                Timestamp = DateTime.Now,
                Context = context ?? new Dictionary<string, object>()
            };

            EnqueueFeedback(feedback);
            Logger.Debug($"Feedback: Undone - {action}");
        }

        /// <summary>
        /// Records explicit user rating.
        /// </summary>
        public void RecordRating(string actionId, string action, int rating, string comment = null)
        {
            var feedback = new FeedbackEntry
            {
                Id = Guid.NewGuid().ToString(),
                ActionId = actionId,
                Action = action,
                Reaction = UserReaction.Rated,
                Rating = rating,
                Comment = comment,
                Timestamp = DateTime.Now
            };

            EnqueueFeedback(feedback);
            Logger.Debug($"Feedback: Rated {rating}/5 - {action}");
        }

        /// <summary>
        /// Records that the user asked for clarification.
        /// </summary>
        public void RecordClarificationRequest(string actionId, string action, string clarificationQuestion)
        {
            var feedback = new FeedbackEntry
            {
                Id = Guid.NewGuid().ToString(),
                ActionId = actionId,
                Action = action,
                Reaction = UserReaction.Confused,
                Comment = clarificationQuestion,
                Timestamp = DateTime.Now
            };

            EnqueueFeedback(feedback);
            Logger.Debug($"Feedback: Clarification requested - {action}");
        }

        /// <summary>
        /// Gets all pending feedback entries.
        /// </summary>
        public IEnumerable<FeedbackEntry> GetPendingFeedback()
        {
            lock (_lock)
            {
                return _pendingFeedback.ToArray();
            }
        }

        /// <summary>
        /// Flushes pending feedback to storage.
        /// </summary>
        public async Task FlushAsync()
        {
            FeedbackEntry[] toFlush;
            lock (_lock)
            {
                toFlush = _pendingFeedback.ToArray();
                _pendingFeedback.Clear();
            }

            if (toFlush.Length == 0) return;

            try
            {
                var directory = Path.GetDirectoryName(_storagePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Append to existing file
                var existingData = new List<FeedbackEntry>();
                if (File.Exists(_storagePath))
                {
                    var json = await Task.Run(() => File.ReadAllText(_storagePath));
                    existingData = JsonConvert.DeserializeObject<List<FeedbackEntry>>(json) ?? new List<FeedbackEntry>();
                }

                existingData.AddRange(toFlush);

                var outputJson = JsonConvert.SerializeObject(existingData, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(_storagePath, outputJson));

                Logger.Info($"Flushed {toFlush.Length} feedback entries to storage");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to flush feedback");
                // Re-queue the feedback
                lock (_lock)
                {
                    foreach (var entry in toFlush)
                    {
                        _pendingFeedback.Enqueue(entry);
                    }
                }
            }
        }

        private void EnqueueFeedback(FeedbackEntry feedback)
        {
            lock (_lock)
            {
                _pendingFeedback.Enqueue(feedback);

                // Auto-flush when batch size reached
                if (_pendingFeedback.Count >= _batchSize)
                {
                    Task.Run(FlushAsync);
                }
            }

            FeedbackReceived?.Invoke(this, feedback);
        }
    }

    /// <summary>
    /// Represents a single feedback entry.
    /// </summary>
    public class FeedbackEntry
    {
        public string Id { get; set; }
        public string ActionId { get; set; }
        public string Action { get; set; }
        public string ModifiedAction { get; set; }
        public UserReaction Reaction { get; set; }
        public int? Rating { get; set; }
        public string Comment { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Types of user reactions to AI actions.
    /// </summary>
    public enum UserReaction
    {
        Accepted,   // User accepted without changes
        Modified,   // User made modifications
        Undone,     // User undid the action
        Rated,      // User provided explicit rating
        Confused,   // User asked for clarification
        Ignored     // User did not interact
    }
}
