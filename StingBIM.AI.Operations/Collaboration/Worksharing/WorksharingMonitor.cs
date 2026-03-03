// =============================================================================
// StingBIM.AI.Collaboration - Worksharing Monitor
// AI-powered monitoring of worksharing activities with conflict prediction
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;
using StingBIM.AI.Collaboration.Communication;

namespace StingBIM.AI.Collaboration.Worksharing
{
    /// <summary>
    /// Core engine for monitoring worksharing activities, predicting conflicts,
    /// and providing intelligent sync recommendations.
    /// </summary>
    public class WorksharingMonitor : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Dependencies
        private readonly CollaborationHub? _collaborationHub;

        // Activity tracking
        private readonly ConcurrentDictionary<string, ElementActivity> _elementActivities = new();
        private readonly ConcurrentDictionary<string, WorksetInfo> _worksets = new();
        private readonly ConcurrentQueue<ElementActivity> _activityHistory = new();
        private const int MaxActivityHistory = 1000;

        // Sync tracking
        private readonly ConcurrentDictionary<string, SyncStatus> _userSyncStatus = new();
        private readonly ConcurrentDictionary<string, List<PendingChange>> _pendingChanges = new();

        // Monitoring
        private CancellationTokenSource? _cts;
        private Task? _monitoringTask;
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(5);

        // Events
        public event EventHandler<ConflictPredictedEventArgs>? ConflictPredicted;
        public event EventHandler<SyncRecommendedEventArgs>? SyncRecommended;
        public event EventHandler<ElementOwnershipChangedEventArgs>? ElementOwnershipChanged;
        public event EventHandler<ActivityHotspotDetectedEventArgs>? HotspotDetected;

        public bool IsMonitoring { get; private set; }
        public string CurrentProjectPath { get; private set; } = string.Empty;
        public string CurrentProjectGuid { get; private set; } = string.Empty;

        public WorksharingMonitor(CollaborationHub? collaborationHub = null)
        {
            _collaborationHub = collaborationHub;

            if (_collaborationHub != null)
            {
                _collaborationHub.ElementActivityReceived += OnRemoteElementActivity;
            }

            Logger.Info("WorksharingMonitor initialized");
        }

        #region Monitoring Control

        /// <summary>
        /// Start monitoring worksharing activities
        /// </summary>
        public async Task StartMonitoringAsync(string projectPath, string projectGuid)
        {
            if (IsMonitoring)
            {
                Logger.Warn("Monitoring is already active");
                return;
            }

            CurrentProjectPath = projectPath;
            CurrentProjectGuid = projectGuid;
            _cts = new CancellationTokenSource();

            IsMonitoring = true;
            _monitoringTask = MonitorLoopAsync(_cts.Token);

            Logger.Info($"Started monitoring project: {projectPath}");

            // Initial workset scan
            await ScanWorksetsAsync();
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (!IsMonitoring) return;

            _cts?.Cancel();

            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask;
                }
                catch (OperationCanceledException) { }
            }

            IsMonitoring = false;
            Logger.Info("Stopped monitoring");
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_monitoringInterval, ct);

                    // Analyze current state
                    await AnalyzeActivityPatternsAsync();
                    await CheckForConflictsAsync();
                    await GenerateSyncRecommendationsAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in monitoring loop");
                }
            }
        }

        #endregion

        #region Element Activity Tracking

        /// <summary>
        /// Record local element modification
        /// </summary>
        public void RecordElementActivity(string elementId, string elementName, string category,
            ActivityType activityType, Dictionary<string, object>? changes = null)
        {
            var activity = new ElementActivity
            {
                ElementId = elementId,
                ElementName = elementName,
                Category = category,
                ActivityType = activityType,
                Changes = changes ?? new Dictionary<string, object>()
            };

            _elementActivities[elementId] = activity;
            AddToActivityHistory(activity);

            // Broadcast to team if connected
            _collaborationHub?.BroadcastElementActivityAsync(activity);

            Logger.Debug($"Recorded activity: {activityType} on {elementName}");
        }

        /// <summary>
        /// Get recent activities for an element
        /// </summary>
        public IEnumerable<ElementActivity> GetElementHistory(string elementId, int count = 10)
        {
            return _activityHistory
                .Where(a => a.ElementId == elementId)
                .OrderByDescending(a => a.Timestamp)
                .Take(count);
        }

        /// <summary>
        /// Get all elements currently being edited
        /// </summary>
        public IEnumerable<ElementActivity> GetActiveElements()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            return _elementActivities.Values
                .Where(a => a.Timestamp > cutoff)
                .OrderByDescending(a => a.Timestamp);
        }

        /// <summary>
        /// Get elements edited by a specific user
        /// </summary>
        public IEnumerable<ElementActivity> GetUserElements(string userId)
        {
            return _elementActivities.Values
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp);
        }

        private void OnRemoteElementActivity(object? sender, ElementActivityEventArgs e)
        {
            _elementActivities[e.Activity.ElementId] = e.Activity;
            AddToActivityHistory(e.Activity);

            // Check for conflicts with our pending changes
            CheckElementForConflict(e.Activity);
        }

        private void AddToActivityHistory(ElementActivity activity)
        {
            _activityHistory.Enqueue(activity);

            while (_activityHistory.Count > MaxActivityHistory)
            {
                _activityHistory.TryDequeue(out _);
            }
        }

        #endregion

        #region Conflict Detection & Prediction

        /// <summary>
        /// Check for potential conflicts before sync
        /// </summary>
        public async Task<List<ConflictPrediction>> PredictConflictsAsync(string userId)
        {
            var predictions = new List<ConflictPrediction>();

            if (!_pendingChanges.TryGetValue(userId, out var myChanges))
                return predictions;

            // Get team member activities
            var recentActivities = _activityHistory
                .Where(a => a.UserId != userId && a.Timestamp > DateTime.UtcNow.AddMinutes(-30))
                .GroupBy(a => a.ElementId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Timestamp).First());

            foreach (var myChange in myChanges)
            {
                if (recentActivities.TryGetValue(myChange.ElementId, out var otherActivity))
                {
                    var severity = CalculateConflictSeverity(myChange, otherActivity);
                    var probability = CalculateConflictProbability(myChange, otherActivity);

                    if (probability > 0.3)
                    {
                        var prediction = new ConflictPrediction
                        {
                            ElementId = myChange.ElementId,
                            ElementName = myChange.ElementName,
                            LocalUserId = userId,
                            RemoteUserId = otherActivity.UserId,
                            RemoteUserName = otherActivity.Username,
                            Severity = severity,
                            Probability = probability,
                            Description = GenerateConflictDescription(myChange, otherActivity),
                            AIResolutionSuggestion = GenerateResolutionSuggestion(myChange, otherActivity, severity)
                        };

                        predictions.Add(prediction);
                    }
                }
            }

            return predictions;
        }

        private void CheckElementForConflict(ElementActivity remoteActivity)
        {
            foreach (var userChanges in _pendingChanges)
            {
                var myChange = userChanges.Value.FirstOrDefault(c => c.ElementId == remoteActivity.ElementId);
                if (myChange != null)
                {
                    var severity = CalculateConflictSeverity(myChange, remoteActivity);
                    if (severity >= ConflictSeverity.Medium)
                    {
                        var prediction = new ConflictPrediction
                        {
                            ElementId = myChange.ElementId,
                            ElementName = myChange.ElementName,
                            LocalUserId = userChanges.Key,
                            RemoteUserId = remoteActivity.UserId,
                            RemoteUserName = remoteActivity.Username,
                            Severity = severity,
                            Probability = 0.9,
                            Description = $"Real-time conflict: {remoteActivity.Username} is also editing {myChange.ElementName}",
                            AIResolutionSuggestion = GenerateResolutionSuggestion(myChange, remoteActivity, severity)
                        };

                        ConflictPredicted?.Invoke(this, new ConflictPredictedEventArgs(prediction));
                    }
                }
            }
        }

        private async Task CheckForConflictsAsync()
        {
            foreach (var userChanges in _pendingChanges)
            {
                var conflicts = await PredictConflictsAsync(userChanges.Key);
                foreach (var conflict in conflicts.Where(c => c.Severity >= ConflictSeverity.High))
                {
                    ConflictPredicted?.Invoke(this, new ConflictPredictedEventArgs(conflict));
                }
            }
        }

        private ConflictSeverity CalculateConflictSeverity(PendingChange myChange, ElementActivity otherActivity)
        {
            // Same element modified by multiple users
            if (myChange.ChangeType == "Geometry" || otherActivity.ActivityType == ActivityType.Moved)
                return ConflictSeverity.High;

            if (otherActivity.ActivityType == ActivityType.Deleted)
                return ConflictSeverity.Critical;

            if (myChange.ChangeType == "Parameters" && otherActivity.ActivityType == ActivityType.Modified)
                return ConflictSeverity.Medium;

            return ConflictSeverity.Low;
        }

        private double CalculateConflictProbability(PendingChange myChange, ElementActivity otherActivity)
        {
            var timeDiff = (DateTime.UtcNow - otherActivity.Timestamp).TotalMinutes;

            // Recent activity = higher probability
            if (timeDiff < 5) return 0.9;
            if (timeDiff < 15) return 0.7;
            if (timeDiff < 30) return 0.5;
            return 0.3;
        }

        private string GenerateConflictDescription(PendingChange myChange, ElementActivity otherActivity)
        {
            return $"Element '{myChange.ElementName}' was {otherActivity.ActivityType.ToString().ToLower()} " +
                   $"by {otherActivity.Username} at {otherActivity.Timestamp:HH:mm}. " +
                   $"Your change: {myChange.ChangeType}";
        }

        private string GenerateResolutionSuggestion(PendingChange myChange, ElementActivity otherActivity, ConflictSeverity severity)
        {
            return severity switch
            {
                ConflictSeverity.Critical =>
                    $"URGENT: {otherActivity.Username} deleted this element. Contact them before syncing.",
                ConflictSeverity.High =>
                    $"Coordinate with {otherActivity.Username} - they're making geometry changes. Consider syncing now to get their changes first.",
                ConflictSeverity.Medium =>
                    $"Minor conflict likely. Review {otherActivity.Username}'s changes after sync.",
                _ =>
                    "Low risk conflict. Safe to sync."
            };
        }

        #endregion

        #region Sync Recommendations

        /// <summary>
        /// Get sync status and recommendations for a user
        /// </summary>
        public SyncStatus GetSyncStatus(string userId)
        {
            if (!_userSyncStatus.TryGetValue(userId, out var status))
            {
                status = new SyncStatus { UserId = userId };
                _userSyncStatus[userId] = status;
            }

            return status;
        }

        /// <summary>
        /// Update user's pending changes
        /// </summary>
        public void UpdatePendingChanges(string userId, List<PendingChange> changes)
        {
            _pendingChanges[userId] = changes;

            var status = GetSyncStatus(userId);
            status.LocalChangesCount = changes.Count;
            status.State = changes.Count > 0 ? SyncState.LocalChanges : SyncState.UpToDate;

            // Analyze for conflicts
            Task.Run(async () =>
            {
                var conflicts = await PredictConflictsAsync(userId);
                status.PredictedConflicts = conflicts;
                status.PotentialConflicts = conflicts.Count;

                // Generate recommendation
                status.SyncRecommendation = GenerateSyncRecommendation(status);
            });
        }

        /// <summary>
        /// Record sync completion
        /// </summary>
        public void RecordSync(string userId)
        {
            var status = GetSyncStatus(userId);
            status.LastSyncTime = DateTime.UtcNow;
            status.LocalChangesCount = 0;
            status.CentralChangesCount = 0;
            status.State = SyncState.UpToDate;
            status.PendingChanges.Clear();
            status.PredictedConflicts.Clear();

            _pendingChanges.TryRemove(userId, out _);
        }

        private async Task GenerateSyncRecommendationsAsync()
        {
            foreach (var userStatus in _userSyncStatus.Values)
            {
                var recommendation = GenerateSyncRecommendation(userStatus);

                if (recommendation != userStatus.SyncRecommendation)
                {
                    userStatus.SyncRecommendation = recommendation;

                    if (ShouldNotifyForRecommendation(recommendation))
                    {
                        SyncRecommended?.Invoke(this, new SyncRecommendedEventArgs(userStatus.UserId, recommendation));
                    }
                }
            }
        }

        private string GenerateSyncRecommendation(SyncStatus status)
        {
            if (status.PotentialConflicts > 0 && status.PredictedConflicts.Any(c => c.Severity >= ConflictSeverity.High))
            {
                return "⚠️ High-risk conflicts detected. Review conflicts before syncing.";
            }

            if (status.LocalChangesCount > 50)
            {
                return "📦 Large number of local changes. Consider syncing soon to avoid major conflicts.";
            }

            if (status.LocalChangesCount > 0 && (DateTime.UtcNow - status.LastSyncTime).TotalMinutes > 30)
            {
                return "⏰ You haven't synced in 30+ minutes. Consider syncing to get team updates.";
            }

            if (status.CentralChangesCount > 20)
            {
                return "📥 Many central changes available. Sync recommended to stay current.";
            }

            if (status.LocalChangesCount == 0 && status.CentralChangesCount == 0)
            {
                return "✅ All up to date.";
            }

            return "👍 Safe to sync when ready.";
        }

        private bool ShouldNotifyForRecommendation(string recommendation)
        {
            return recommendation.Contains("⚠️") || recommendation.Contains("URGENT");
        }

        #endregion

        #region Activity Analysis

        private async Task AnalyzeActivityPatternsAsync()
        {
            var recentActivities = _activityHistory
                .Where(a => a.Timestamp > DateTime.UtcNow.AddMinutes(-15))
                .ToList();

            // Detect hotspots (areas with high activity)
            var hotspots = recentActivities
                .GroupBy(a => a.LevelName ?? "Unknown")
                .Where(g => g.Count() >= 5)
                .Select(g => new ActivityHotspot
                {
                    AreaName = $"Level: {g.Key}",
                    Level = g.Key ?? "Unknown",
                    ActivityCount = g.Count(),
                    ActiveUsers = g.Select(a => a.Username).Distinct().ToList(),
                    ConflictRisk = g.Select(a => a.Username).Distinct().Count() > 2 ? "High" : "Normal"
                })
                .ToList();

            foreach (var hotspot in hotspots.Where(h => h.ConflictRisk == "High"))
            {
                HotspotDetected?.Invoke(this, new ActivityHotspotDetectedEventArgs(hotspot));
            }
        }

        /// <summary>
        /// Get activity summary for dashboard
        /// </summary>
        public TeamActivitySummary GetActivitySummary(TimeSpan period)
        {
            var cutoff = DateTime.UtcNow - period;
            var activities = _activityHistory.Where(a => a.Timestamp > cutoff).ToList();

            return new TeamActivitySummary
            {
                PeriodStart = cutoff,
                PeriodEnd = DateTime.UtcNow,
                ActiveUsers = activities.Select(a => a.UserId).Distinct().Count(),
                TotalEdits = activities.Count,
                EditsByUser = activities.GroupBy(a => a.Username).ToDictionary(g => g.Key, g => g.Count()),
                EditsByCategory = activities.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count()),
                EditsByLevel = activities.GroupBy(a => a.LevelName ?? "Unknown").ToDictionary(g => g.Key, g => g.Count())
            };
        }

        #endregion

        #region Workset Management

        /// <summary>
        /// Scan and update workset information
        /// </summary>
        public async Task ScanWorksetsAsync()
        {
            // This would integrate with Revit API
            // For now, this is a placeholder for the interface
            Logger.Debug("Scanning worksets...");
        }

        /// <summary>
        /// Get all worksets
        /// </summary>
        public IEnumerable<WorksetInfo> GetWorksets()
        {
            return _worksets.Values;
        }

        /// <summary>
        /// Get workset by name
        /// </summary>
        public WorksetInfo? GetWorkset(string name)
        {
            return _worksets.Values.FirstOrDefault(w => w.Name == name);
        }

        /// <summary>
        /// Update workset information
        /// </summary>
        public void UpdateWorkset(WorksetInfo workset)
        {
            _worksets[workset.Name] = workset;
        }

        /// <summary>
        /// Find who owns a workset
        /// </summary>
        public string? FindWorksetOwner(string worksetName)
        {
            return _worksets.Values.FirstOrDefault(w => w.Name == worksetName)?.Owner;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopMonitoringAsync().Wait(TimeSpan.FromSeconds(5));
            _cts?.Dispose();

            if (_collaborationHub != null)
            {
                _collaborationHub.ElementActivityReceived -= OnRemoteElementActivity;
            }
        }

        #endregion
    }

    #region Event Args

    public class ConflictPredictedEventArgs : EventArgs
    {
        public ConflictPrediction Conflict { get; }
        public ConflictPredictedEventArgs(ConflictPrediction conflict) => Conflict = conflict;
    }

    public class SyncRecommendedEventArgs : EventArgs
    {
        public string UserId { get; }
        public string Recommendation { get; }
        public SyncRecommendedEventArgs(string userId, string recommendation)
        {
            UserId = userId;
            Recommendation = recommendation;
        }
    }

    public class ElementOwnershipChangedEventArgs : EventArgs
    {
        public string ElementId { get; }
        public string? PreviousOwner { get; }
        public string? NewOwner { get; }
        public ElementOwnershipChangedEventArgs(string elementId, string? previousOwner, string? newOwner)
        {
            ElementId = elementId;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
        }
    }

    public class ActivityHotspotDetectedEventArgs : EventArgs
    {
        public ActivityHotspot Hotspot { get; }
        public ActivityHotspotDetectedEventArgs(ActivityHotspot hotspot) => Hotspot = hotspot;
    }

    #endregion
}
