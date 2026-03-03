// ===================================================================
// StingBIM.AI.Collaboration - Offline Synchronization System
// Enables field workers to work offline with automatic sync
// Implements conflict resolution and delta sync
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StingBIM.AI.Collaboration.Persistence;

namespace StingBIM.AI.Collaboration.Offline
{
    #region Enums and Configuration

    /// <summary>
    /// Sync operation type
    /// </summary>
    public enum SyncOperation
    {
        Create,
        Update,
        Delete
    }

    /// <summary>
    /// Sync status
    /// </summary>
    public enum SyncStatus
    {
        Pending,
        Syncing,
        Synced,
        Conflict,
        Failed,
        Retrying
    }

    /// <summary>
    /// Conflict resolution strategy
    /// </summary>
    public enum ConflictResolution
    {
        ServerWins,
        ClientWins,
        LatestWins,
        Manual,
        Merge
    }

    /// <summary>
    /// Offline sync configuration
    /// </summary>
    public class OfflineSyncConfiguration
    {
        public string LocalStoragePath { get; set; } = "./offline_data";
        public string DeviceId { get; set; } = Guid.NewGuid().ToString();
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;
        public int SyncIntervalMs { get; set; } = 30000;
        public int MaxPendingChanges { get; set; } = 1000;
        public long MaxLocalStorageBytes { get; set; } = 500 * 1024 * 1024; // 500 MB
        public ConflictResolution DefaultConflictResolution { get; set; } = ConflictResolution.LatestWins;
        public bool AutoSync { get; set; } = true;
        public bool CompressLocalData { get; set; } = true;
        public List<string> PrioritySyncTypes { get; set; } = new() { "Issue", "Photo", "Inspection" };
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Change entry for sync queue
    /// </summary>
    public class SyncChange
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public SyncOperation Operation { get; set; }
        public string DataJson { get; set; } = "{}";
        public string DataHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public SyncStatus Status { get; set; } = SyncStatus.Pending;
        public int RetryCount { get; set; } = 0;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime? SyncedAt { get; set; }
        public long Version { get; set; } = 0;
        public string ProjectId { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Sync conflict
    /// </summary>
    public class SyncConflict
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string LocalDataJson { get; set; } = "{}";
        public string ServerDataJson { get; set; } = "{}";
        public long LocalVersion { get; set; }
        public long ServerVersion { get; set; }
        public DateTime LocalModifiedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public string LocalModifiedBy { get; set; } = string.Empty;
        public string ServerModifiedBy { get; set; } = string.Empty;
        public ConflictResolution? Resolution { get; set; }
        public string ResolvedDataJson { get; set; } = "{}";
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sync session result
    /// </summary>
    public class SyncResult
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool Success { get; set; }
        public int ChangesPushed { get; set; }
        public int ChangesPulled { get; set; }
        public int ConflictsDetected { get; set; }
        public int ConflictsResolved { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public long BytesUploaded { get; set; }
        public long BytesDownloaded { get; set; }
        public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
    }

    /// <summary>
    /// Local storage statistics
    /// </summary>
    public class StorageStats
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes => TotalBytes - UsedBytes;
        public int PendingChanges { get; set; }
        public int SyncedItems { get; set; }
        public int ConflictCount { get; set; }
        public DateTime LastSyncAt { get; set; }
        public DateTime? NextScheduledSync { get; set; }
        public Dictionary<string, int> ItemCountsByType { get; set; } = new();
    }

    /// <summary>
    /// Offline-capable entity interface
    /// </summary>
    public interface IOfflineEntity
    {
        string Id { get; set; }
        long Version { get; set; }
        DateTime ModifiedAt { get; set; }
        string ModifiedBy { get; set; }
        bool IsDeleted { get; set; }
        string GetContentHash();
    }

    /// <summary>
    /// Delta sync request
    /// </summary>
    public class DeltaSyncRequest
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime LastSyncAt { get; set; }
        public Dictionary<string, long> EntityVersions { get; set; } = new();
        public List<SyncChange> LocalChanges { get; set; } = new();
    }

    /// <summary>
    /// Delta sync response
    /// </summary>
    public class DeltaSyncResponse
    {
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
        public List<SyncChange> ServerChanges { get; set; } = new();
        public List<SyncConflict> Conflicts { get; set; } = new();
        public List<string> AcknowledgedIds { get; set; } = new();
        public bool HasMoreChanges { get; set; }
        public string ContinuationToken { get; set; } = string.Empty;
    }

    #endregion

    #region Local Storage

    /// <summary>
    /// Local storage manager for offline data
    /// </summary>
    public class LocalStorageManager : IAsyncDisposable
    {
        private readonly OfflineSyncConfiguration _config;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly string _dataPath;
        private readonly string _queuePath;
        private readonly string _metaPath;

        public LocalStorageManager(OfflineSyncConfiguration? config = null, ILogger? logger = null)
        {
            _config = config ?? new OfflineSyncConfiguration();
            _logger = logger;

            _dataPath = Path.Combine(_config.LocalStoragePath, "data");
            _queuePath = Path.Combine(_config.LocalStoragePath, "queue");
            _metaPath = Path.Combine(_config.LocalStoragePath, "meta");

            Directory.CreateDirectory(_dataPath);
            Directory.CreateDirectory(_queuePath);
            Directory.CreateDirectory(_metaPath);

            _logger?.LogInformation("LocalStorageManager initialized at {Path}", _config.LocalStoragePath);
        }

        /// <summary>
        /// Store entity locally
        /// </summary>
        public async Task<bool> StoreAsync<T>(string key, T data, CancellationToken ct = default)
            where T : class
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                var json = JsonSerializer.Serialize(data);
                var filePath = GetDataPath(key);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await File.WriteAllTextAsync(filePath, json, ct);

                _cache[key] = data;

                _logger?.LogDebug("Stored {Key} locally", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to store {Key}", key);
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Retrieve entity from local storage
        /// </summary>
        public async Task<T?> RetrieveAsync<T>(string key, CancellationToken ct = default)
            where T : class
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached as T;
            }

            var filePath = GetDataPath(key);
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, ct);
                var data = JsonSerializer.Deserialize<T>(json);

                if (data != null)
                {
                    _cache[key] = data;
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Delete entity from local storage
        /// </summary>
        public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                _cache.TryRemove(key, out _);

                var filePath = GetDataPath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete {Key}", key);
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Queue a change for sync
        /// </summary>
        public async Task<bool> QueueChangeAsync(SyncChange change, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                var filePath = Path.Combine(_queuePath, $"{change.Id}.json");
                var json = JsonSerializer.Serialize(change);
                await File.WriteAllTextAsync(filePath, json, ct);

                _logger?.LogDebug("Queued change {Id} for {EntityType}:{EntityId}",
                    change.Id, change.EntityType, change.EntityId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to queue change {Id}", change.Id);
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Get pending changes
        /// </summary>
        public async Task<List<SyncChange>> GetPendingChangesAsync(CancellationToken ct = default)
        {
            var changes = new List<SyncChange>();

            foreach (var file in Directory.GetFiles(_queuePath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var change = JsonSerializer.Deserialize<SyncChange>(json);

                    if (change != null && change.Status == SyncStatus.Pending)
                    {
                        changes.Add(change);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read queue file {File}", file);
                }
            }

            return changes
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Mark change as synced
        /// </summary>
        public async Task MarkSyncedAsync(string changeId, CancellationToken ct = default)
        {
            var filePath = Path.Combine(_queuePath, $"{changeId}.json");
            if (File.Exists(filePath))
            {
                // Move to archive or delete
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Update change status
        /// </summary>
        public async Task UpdateChangeStatusAsync(
            string changeId,
            SyncStatus status,
            string? errorMessage = null,
            CancellationToken ct = default)
        {
            var filePath = Path.Combine(_queuePath, $"{changeId}.json");
            if (!File.Exists(filePath)) return;

            await _writeLock.WaitAsync(ct);
            try
            {
                var json = await File.ReadAllTextAsync(filePath, ct);
                var change = JsonSerializer.Deserialize<SyncChange>(json);

                if (change != null)
                {
                    change.Status = status;
                    change.ErrorMessage = errorMessage ?? change.ErrorMessage;

                    if (status == SyncStatus.Synced)
                    {
                        change.SyncedAt = DateTime.UtcNow;
                    }
                    else if (status == SyncStatus.Retrying)
                    {
                        change.RetryCount++;
                    }

                    json = JsonSerializer.Serialize(change);
                    await File.WriteAllTextAsync(filePath, json, ct);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Get storage statistics
        /// </summary>
        public StorageStats GetStorageStats()
        {
            var stats = new StorageStats
            {
                TotalBytes = _config.MaxLocalStorageBytes
            };

            // Calculate used bytes
            if (Directory.Exists(_config.LocalStoragePath))
            {
                var dirInfo = new DirectoryInfo(_config.LocalStoragePath);
                stats.UsedBytes = dirInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }

            // Count pending changes
            if (Directory.Exists(_queuePath))
            {
                stats.PendingChanges = Directory.GetFiles(_queuePath, "*.json").Length;
            }

            // Get last sync time from meta
            var lastSyncPath = Path.Combine(_metaPath, "last_sync.txt");
            if (File.Exists(lastSyncPath))
            {
                if (DateTime.TryParse(File.ReadAllText(lastSyncPath), out var lastSync))
                {
                    stats.LastSyncAt = lastSync;
                }
            }

            return stats;
        }

        /// <summary>
        /// Save last sync timestamp
        /// </summary>
        public async Task SaveLastSyncAsync(DateTime timestamp, CancellationToken ct = default)
        {
            var filePath = Path.Combine(_metaPath, "last_sync.txt");
            await File.WriteAllTextAsync(filePath, timestamp.ToString("O"), ct);
        }

        /// <summary>
        /// Clear all local data
        /// </summary>
        public async Task ClearAsync(CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                _cache.Clear();

                if (Directory.Exists(_config.LocalStoragePath))
                {
                    Directory.Delete(_config.LocalStoragePath, recursive: true);
                    Directory.CreateDirectory(_dataPath);
                    Directory.CreateDirectory(_queuePath);
                    Directory.CreateDirectory(_metaPath);
                }

                _logger?.LogInformation("Cleared all local storage");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private string GetDataPath(string key)
        {
            // Use first two chars as directory for better file distribution
            var prefix = key.Length >= 2 ? key.Substring(0, 2) : "00";
            return Path.Combine(_dataPath, prefix, $"{key}.json");
        }

        public ValueTask DisposeAsync()
        {
            _writeLock.Dispose();
            _logger?.LogInformation("LocalStorageManager disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Conflict Resolution

    /// <summary>
    /// Conflict resolver
    /// </summary>
    public class ConflictResolver
    {
        private readonly OfflineSyncConfiguration _config;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, SyncConflict> _unresolvedConflicts = new();

        public ConflictResolver(OfflineSyncConfiguration? config = null, ILogger? logger = null)
        {
            _config = config ?? new OfflineSyncConfiguration();
            _logger = logger;
        }

        /// <summary>
        /// Detect conflict between local and server data
        /// </summary>
        public SyncConflict? DetectConflict(
            SyncChange localChange,
            SyncChange serverChange)
        {
            if (localChange.EntityId != serverChange.EntityId)
                return null;

            // No conflict if same version
            if (localChange.Version >= serverChange.Version)
                return null;

            // No conflict if data is identical
            if (localChange.DataHash == serverChange.DataHash)
                return null;

            var conflict = new SyncConflict
            {
                EntityType = localChange.EntityType,
                EntityId = localChange.EntityId,
                LocalDataJson = localChange.DataJson,
                ServerDataJson = serverChange.DataJson,
                LocalVersion = localChange.Version,
                ServerVersion = serverChange.Version,
                LocalModifiedAt = localChange.CreatedAt,
                ServerModifiedAt = serverChange.CreatedAt,
                LocalModifiedBy = localChange.CreatedBy,
                ServerModifiedBy = serverChange.CreatedBy
            };

            _unresolvedConflicts[conflict.Id] = conflict;
            _logger?.LogWarning("Detected conflict for {EntityType}:{EntityId}",
                conflict.EntityType, conflict.EntityId);

            return conflict;
        }

        /// <summary>
        /// Resolve conflict automatically based on strategy
        /// </summary>
        public SyncConflict ResolveConflict(
            SyncConflict conflict,
            ConflictResolution? strategy = null)
        {
            strategy ??= _config.DefaultConflictResolution;
            conflict.Resolution = strategy;

            switch (strategy)
            {
                case ConflictResolution.ServerWins:
                    conflict.ResolvedDataJson = conflict.ServerDataJson;
                    break;

                case ConflictResolution.ClientWins:
                    conflict.ResolvedDataJson = conflict.LocalDataJson;
                    break;

                case ConflictResolution.LatestWins:
                    conflict.ResolvedDataJson = conflict.ServerModifiedAt > conflict.LocalModifiedAt
                        ? conflict.ServerDataJson
                        : conflict.LocalDataJson;
                    break;

                case ConflictResolution.Merge:
                    conflict.ResolvedDataJson = MergeData(
                        conflict.LocalDataJson,
                        conflict.ServerDataJson);
                    break;

                case ConflictResolution.Manual:
                    // Leave unresolved for manual intervention
                    return conflict;
            }

            conflict.ResolvedAt = DateTime.UtcNow;
            _unresolvedConflicts.TryRemove(conflict.Id, out _);

            _logger?.LogInformation("Resolved conflict {Id} using {Strategy}",
                conflict.Id, strategy);

            return conflict;
        }

        /// <summary>
        /// Merge two JSON objects (field-level merge)
        /// </summary>
        private string MergeData(string localJson, string serverJson)
        {
            try
            {
                var local = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(localJson)
                           ?? new Dictionary<string, JsonElement>();
                var server = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serverJson)
                            ?? new Dictionary<string, JsonElement>();

                var merged = new Dictionary<string, object?>();

                // Start with server data
                foreach (var kvp in server)
                {
                    merged[kvp.Key] = kvp.Value;
                }

                // Overlay local changes (prefer local for modified fields)
                foreach (var kvp in local)
                {
                    if (!server.ContainsKey(kvp.Key))
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                    // For arrays, could implement smarter merging
                }

                return JsonSerializer.Serialize(merged);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Merge failed, falling back to server data");
                return serverJson;
            }
        }

        /// <summary>
        /// Manually resolve conflict
        /// </summary>
        public SyncConflict ManualResolve(string conflictId, string resolvedDataJson, string resolvedBy)
        {
            if (!_unresolvedConflicts.TryGetValue(conflictId, out var conflict))
            {
                throw new KeyNotFoundException($"Conflict {conflictId} not found");
            }

            conflict.ResolvedDataJson = resolvedDataJson;
            conflict.ResolvedAt = DateTime.UtcNow;
            conflict.ResolvedBy = resolvedBy;
            conflict.Resolution = ConflictResolution.Manual;

            _unresolvedConflicts.TryRemove(conflictId, out _);

            _logger?.LogInformation("Manually resolved conflict {Id} by {User}",
                conflictId, resolvedBy);

            return conflict;
        }

        /// <summary>
        /// Get unresolved conflicts
        /// </summary>
        public List<SyncConflict> GetUnresolvedConflicts()
        {
            return _unresolvedConflicts.Values.ToList();
        }
    }

    #endregion

    #region Offline Sync Manager

    /// <summary>
    /// Main offline synchronization manager
    /// </summary>
    public class OfflineSyncManager : IAsyncDisposable
    {
        private readonly OfflineSyncConfiguration _config;
        private readonly LocalStorageManager _localStorage;
        private readonly ConflictResolver _conflictResolver;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, long> _entityVersions = new();
        private Timer? _autoSyncTimer;
        private bool _isOnline = true;
        private bool _isSyncing = false;
        private DateTime _lastSyncAt = DateTime.MinValue;
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        public event EventHandler<SyncResult>? SyncCompleted;
        public event EventHandler<SyncConflict>? ConflictDetected;
        public event EventHandler<bool>? ConnectivityChanged;

        public bool IsOnline => _isOnline;
        public bool IsSyncing => _isSyncing;
        public DateTime LastSyncAt => _lastSyncAt;

        public OfflineSyncManager(
            OfflineSyncConfiguration? config = null,
            ILogger? logger = null)
        {
            _config = config ?? new OfflineSyncConfiguration();
            _logger = logger;
            _localStorage = new LocalStorageManager(_config, logger);
            _conflictResolver = new ConflictResolver(_config, logger);

            if (_config.AutoSync)
            {
                StartAutoSync();
            }

            _logger?.LogInformation("OfflineSyncManager initialized for device {DeviceId}",
                _config.DeviceId);
        }

        #region Online/Offline Management

        /// <summary>
        /// Set online status
        /// </summary>
        public void SetOnline(bool online)
        {
            if (_isOnline != online)
            {
                _isOnline = online;
                ConnectivityChanged?.Invoke(this, online);

                _logger?.LogInformation("Connectivity changed: {Status}",
                    online ? "Online" : "Offline");

                if (online)
                {
                    // Trigger sync when coming back online
                    _ = Task.Run(() => SyncAsync());
                }
            }
        }

        /// <summary>
        /// Check if entity is available offline
        /// </summary>
        public async Task<bool> IsAvailableOfflineAsync(string entityType, string entityId)
        {
            var key = $"{entityType}:{entityId}";
            return await _localStorage.RetrieveAsync<object>(key) != null;
        }

        #endregion

        #region Data Operations

        /// <summary>
        /// Save entity (works offline)
        /// </summary>
        public async Task<bool> SaveAsync<T>(
            string entityType,
            string entityId,
            T data,
            string userId,
            string projectId,
            CancellationToken ct = default)
            where T : class
        {
            var key = $"{entityType}:{entityId}";

            // Store locally
            await _localStorage.StoreAsync(key, data, ct);

            // Get current version
            var version = _entityVersions.GetOrAdd(key, 0) + 1;
            _entityVersions[key] = version;

            // Calculate hash
            var json = JsonSerializer.Serialize(data);
            var hash = ComputeHash(json);

            // Queue for sync
            var change = new SyncChange
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = version == 1 ? SyncOperation.Create : SyncOperation.Update,
                DataJson = json,
                DataHash = hash,
                CreatedBy = userId,
                DeviceId = _config.DeviceId,
                Version = version,
                ProjectId = projectId,
                Priority = _config.PrioritySyncTypes.Contains(entityType) ? 0 : 1
            };

            await _localStorage.QueueChangeAsync(change, ct);

            _logger?.LogDebug("Saved {EntityType}:{EntityId} (offline: {Offline})",
                entityType, entityId, !_isOnline);

            // Trigger sync if online
            if (_isOnline && !_isSyncing)
            {
                _ = Task.Run(() => SyncAsync(ct), ct);
            }

            return true;
        }

        /// <summary>
        /// Get entity (works offline)
        /// </summary>
        public async Task<T?> GetAsync<T>(
            string entityType,
            string entityId,
            CancellationToken ct = default)
            where T : class
        {
            var key = $"{entityType}:{entityId}";
            return await _localStorage.RetrieveAsync<T>(key, ct);
        }

        /// <summary>
        /// Delete entity (works offline)
        /// </summary>
        public async Task<bool> DeleteAsync(
            string entityType,
            string entityId,
            string userId,
            string projectId,
            CancellationToken ct = default)
        {
            var key = $"{entityType}:{entityId}";

            // Mark as deleted locally
            await _localStorage.DeleteAsync(key, ct);

            // Queue delete for sync
            var change = new SyncChange
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = SyncOperation.Delete,
                CreatedBy = userId,
                DeviceId = _config.DeviceId,
                ProjectId = projectId
            };

            await _localStorage.QueueChangeAsync(change, ct);

            _logger?.LogDebug("Deleted {EntityType}:{EntityId}", entityType, entityId);

            return true;
        }

        #endregion

        #region Synchronization

        /// <summary>
        /// Perform full sync
        /// </summary>
        public async Task<SyncResult> SyncAsync(CancellationToken ct = default)
        {
            if (!_isOnline)
            {
                return new SyncResult
                {
                    Success = false,
                    ErrorMessages = { "Device is offline" }
                };
            }

            if (!await _syncLock.WaitAsync(0, ct))
            {
                return new SyncResult
                {
                    Success = false,
                    ErrorMessages = { "Sync already in progress" }
                };
            }

            _isSyncing = true;
            var result = new SyncResult();

            try
            {
                _logger?.LogInformation("Starting sync session {SessionId}", result.SessionId);

                // Get pending changes
                var pendingChanges = await _localStorage.GetPendingChangesAsync(ct);

                // Push local changes
                foreach (var change in pendingChanges)
                {
                    if (ct.IsCancellationRequested) break;

                    var pushed = await PushChangeAsync(change, result, ct);
                    if (pushed)
                    {
                        result.ChangesPushed++;
                        await _localStorage.MarkSyncedAsync(change.Id, ct);
                    }
                }

                // Pull server changes
                var pulled = await PullChangesAsync(result, ct);
                result.ChangesPulled = pulled;

                // Update last sync time
                _lastSyncAt = DateTime.UtcNow;
                await _localStorage.SaveLastSyncAsync(_lastSyncAt, ct);

                result.Success = result.Errors == 0;
                result.CompletedAt = DateTime.UtcNow;

                _logger?.LogInformation(
                    "Sync completed: {Pushed} pushed, {Pulled} pulled, {Conflicts} conflicts in {Duration}ms",
                    result.ChangesPushed, result.ChangesPulled,
                    result.ConflictsDetected, result.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Sync failed");
                result.Success = false;
                result.ErrorMessages.Add(ex.Message);
                result.Errors++;
            }
            finally
            {
                _isSyncing = false;
                _syncLock.Release();
                SyncCompleted?.Invoke(this, result);
            }

            return result;
        }

        private async Task<bool> PushChangeAsync(
            SyncChange change,
            SyncResult result,
            CancellationToken ct)
        {
            try
            {
                await _localStorage.UpdateChangeStatusAsync(
                    change.Id, SyncStatus.Syncing, ct: ct);

                // Simulate server push (replace with actual API call)
                await Task.Delay(50, ct);

                // Check for conflicts (simulate server response)
                // In real implementation, this would come from server

                result.BytesUploaded += change.DataJson.Length;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to push change {Id}", change.Id);

                if (change.RetryCount < _config.MaxRetries)
                {
                    await _localStorage.UpdateChangeStatusAsync(
                        change.Id, SyncStatus.Retrying, ex.Message, ct);
                }
                else
                {
                    await _localStorage.UpdateChangeStatusAsync(
                        change.Id, SyncStatus.Failed, ex.Message, ct);
                    result.Errors++;
                    result.ErrorMessages.Add($"Failed to push {change.EntityId}: {ex.Message}");
                }

                return false;
            }
        }

        private async Task<int> PullChangesAsync(SyncResult result, CancellationToken ct)
        {
            try
            {
                // Simulate pulling changes from server
                // In real implementation, this would be an API call

                // var response = await _api.GetDeltaChangesAsync(new DeltaSyncRequest
                // {
                //     DeviceId = _config.DeviceId,
                //     LastSyncAt = _lastSyncAt,
                //     EntityVersions = _entityVersions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                // }, ct);

                // Process server changes
                // foreach (var serverChange in response.ServerChanges)
                // {
                //     await ApplyServerChangeAsync(serverChange, ct);
                //     pulled++;
                // }

                await Task.Delay(50, ct);
                return 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to pull changes");
                result.Errors++;
                result.ErrorMessages.Add($"Pull failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Apply a server change locally
        /// </summary>
        private async Task ApplyServerChangeAsync(SyncChange serverChange, CancellationToken ct)
        {
            var key = $"{serverChange.EntityType}:{serverChange.EntityId}";

            // Check for local conflict
            var localVersion = _entityVersions.GetOrAdd(key, 0);
            if (localVersion > serverChange.Version)
            {
                // Local is newer - potential conflict
                var pendingChanges = await _localStorage.GetPendingChangesAsync(ct);
                var localChange = pendingChanges.FirstOrDefault(c =>
                    c.EntityType == serverChange.EntityType &&
                    c.EntityId == serverChange.EntityId);

                if (localChange != null)
                {
                    var conflict = _conflictResolver.DetectConflict(localChange, serverChange);
                    if (conflict != null)
                    {
                        ConflictDetected?.Invoke(this, conflict);

                        // Auto-resolve based on strategy
                        var resolved = _conflictResolver.ResolveConflict(conflict);
                        if (resolved.ResolvedAt.HasValue)
                        {
                            serverChange.DataJson = resolved.ResolvedDataJson;
                        }
                        else
                        {
                            // Manual resolution needed - skip this change
                            return;
                        }
                    }
                }
            }

            // Apply the change
            switch (serverChange.Operation)
            {
                case SyncOperation.Create:
                case SyncOperation.Update:
                    var data = JsonSerializer.Deserialize<object>(serverChange.DataJson);
                    if (data != null)
                    {
                        await _localStorage.StoreAsync(key, data, ct);
                        _entityVersions[key] = serverChange.Version;
                    }
                    break;

                case SyncOperation.Delete:
                    await _localStorage.DeleteAsync(key, ct);
                    _entityVersions.TryRemove(key, out _);
                    break;
            }

            _logger?.LogDebug("Applied server change: {Operation} {EntityType}:{EntityId}",
                serverChange.Operation, serverChange.EntityType, serverChange.EntityId);
        }

        #endregion

        #region Auto Sync

        private void StartAutoSync()
        {
            _autoSyncTimer = new Timer(
                async _ => await SyncAsync(),
                null,
                _config.SyncIntervalMs,
                _config.SyncIntervalMs);

            _logger?.LogInformation("Auto-sync enabled every {Interval}ms",
                _config.SyncIntervalMs);
        }

        private void StopAutoSync()
        {
            _autoSyncTimer?.Change(Timeout.Infinite, 0);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get storage statistics
        /// </summary>
        public StorageStats GetStorageStats()
        {
            var stats = _localStorage.GetStorageStats();
            stats.ConflictCount = _conflictResolver.GetUnresolvedConflicts().Count;
            stats.LastSyncAt = _lastSyncAt;

            if (_config.AutoSync && _isOnline)
            {
                stats.NextScheduledSync = _lastSyncAt.AddMilliseconds(_config.SyncIntervalMs);
            }

            return stats;
        }

        /// <summary>
        /// Get unresolved conflicts
        /// </summary>
        public List<SyncConflict> GetUnresolvedConflicts()
        {
            return _conflictResolver.GetUnresolvedConflicts();
        }

        /// <summary>
        /// Manually resolve a conflict
        /// </summary>
        public async Task<SyncConflict> ResolveConflictAsync(
            string conflictId,
            string resolvedDataJson,
            string resolvedBy,
            CancellationToken ct = default)
        {
            var resolved = _conflictResolver.ManualResolve(conflictId, resolvedDataJson, resolvedBy);

            // Apply the resolution
            var key = $"{resolved.EntityType}:{resolved.EntityId}";
            var data = JsonSerializer.Deserialize<object>(resolved.ResolvedDataJson);
            if (data != null)
            {
                await _localStorage.StoreAsync(key, data, ct);
            }

            // Re-queue for sync
            var change = new SyncChange
            {
                EntityType = resolved.EntityType,
                EntityId = resolved.EntityId,
                Operation = SyncOperation.Update,
                DataJson = resolved.ResolvedDataJson,
                DataHash = ComputeHash(resolved.ResolvedDataJson),
                CreatedBy = resolvedBy,
                DeviceId = _config.DeviceId,
                Version = resolved.ServerVersion + 1
            };

            await _localStorage.QueueChangeAsync(change, ct);

            return resolved;
        }

        /// <summary>
        /// Force full resync
        /// </summary>
        public async Task<SyncResult> ForceResyncAsync(CancellationToken ct = default)
        {
            _logger?.LogInformation("Forcing full resync...");

            // Clear local cache
            await _localStorage.ClearAsync(ct);
            _entityVersions.Clear();
            _lastSyncAt = DateTime.MinValue;

            return await SyncAsync(ct);
        }

        /// <summary>
        /// Make entities available offline
        /// </summary>
        public async Task<int> MakeAvailableOfflineAsync(
            string projectId,
            IEnumerable<(string EntityType, string EntityId, object Data)> entities,
            CancellationToken ct = default)
        {
            var count = 0;

            foreach (var (entityType, entityId, data) in entities)
            {
                var key = $"{entityType}:{entityId}";
                await _localStorage.StoreAsync(key, data, ct);
                count++;
            }

            _logger?.LogInformation("Made {Count} entities available offline for project {ProjectId}",
                count, projectId);

            return count;
        }

        private string ComputeHash(string data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            StopAutoSync();
            _autoSyncTimer?.Dispose();
            _syncLock.Dispose();
            await _localStorage.DisposeAsync();
            _logger?.LogInformation("OfflineSyncManager disposed");
        }
    }

    #endregion
}
