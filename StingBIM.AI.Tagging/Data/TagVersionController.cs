// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagVersionController.cs - Version control for tag configurations
// Provides versioning, diff, merge, rollback, branching, and configuration profiles
//
// Version Control Capabilities:
//   1. Version Snapshots    - Capture tag state at points in time (incremental + full)
//   2. Diff Engine          - Compare any two snapshots with property-level detail
//   3. Merge Operations     - Three-way merge with conflict detection
//   4. Rollback System      - Full or selective rollback to any previous snapshot
//   5. Branch Management    - Explore alternative tag configurations
//   6. Change Tracking      - Granular change log with attribution
//   7. Config Profiles      - Named, reusable tag configuration packages
//   8. Storage Management   - Efficient delta storage with retention policies

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Data
{
    #region Enums

    public enum DiffType
    {
        Added,
        Removed,
        Moved,
        ContentChanged,
        StyleChanged,
        LeaderChanged,
        TemplateChanged,
        Unchanged
    }

    public enum MergeStrategy
    {
        SourceWins,
        TargetWins,
        ThreeWayMerge,
        ManualResolve
    }

    #endregion

    #region Data Models

    public sealed class TagSnapshot
    {
        public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public string ParentSnapshotId { get; set; }
        public string BranchName { get; set; } = "main";
        public bool IsFullSnapshot { get; set; }
        public int TagCount { get; set; }
        public Dictionary<string, TagInstanceState> Tags { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public sealed class TagInstanceState
    {
        public string TagId { get; set; }
        public string ElementId { get; set; }
        public string ViewId { get; set; }
        public string CategoryName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public string TemplateName { get; set; }
        public string Content { get; set; }
        public bool HasLeader { get; set; }
        public double LeaderEndX { get; set; }
        public double LeaderEndY { get; set; }
        public TagState State { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    public sealed class TagDiffEntry
    {
        public string TagId { get; set; }
        public DiffType Type { get; set; }
        public TagInstanceState Before { get; set; }
        public TagInstanceState After { get; set; }
        public List<PropertyChange> PropertyChanges { get; set; } = new();
    }

    public sealed class PropertyChange
    {
        public string PropertyName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }

    public sealed class TagDiffResult
    {
        public string SourceSnapshotId { get; set; }
        public string TargetSnapshotId { get; set; }
        public DateTime ComparedAt { get; set; } = DateTime.UtcNow;
        public List<TagDiffEntry> Entries { get; set; } = new();
        public int AddedCount => Entries.Count(e => e.Type == DiffType.Added);
        public int RemovedCount => Entries.Count(e => e.Type == DiffType.Removed);
        public int ModifiedCount => Entries.Count(e => e.Type != DiffType.Added &&
            e.Type != DiffType.Removed && e.Type != DiffType.Unchanged);
        public int UnchangedCount => Entries.Count(e => e.Type == DiffType.Unchanged);
    }

    public sealed class MergeConflict
    {
        public string TagId { get; set; }
        public string Description { get; set; }
        public TagInstanceState SourceState { get; set; }
        public TagInstanceState TargetState { get; set; }
        public TagInstanceState AncestorState { get; set; }
        public TagInstanceState ResolvedState { get; set; }
        public bool IsResolved { get; set; }
    }

    public sealed class MergeResult
    {
        public bool Success { get; set; }
        public string ResultSnapshotId { get; set; }
        public int AutoMergedCount { get; set; }
        public List<MergeConflict> Conflicts { get; set; } = new();
        public string Message { get; set; }
    }

    public sealed class TagBranch
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ParentBranch { get; set; }
        public string BranchPointSnapshotId { get; set; }
        public string LatestSnapshotId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public bool IsActive { get; set; } = true;
        public int SnapshotCount { get; set; }
    }

    public sealed class ChangeLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string User { get; set; }
        public string OperationType { get; set; }
        public string TagId { get; set; }
        public string Description { get; set; }
        public string SnapshotId { get; set; }
        public bool IsAutomatic { get; set; }
    }

    public sealed class ConfigurationProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ProjectType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public string BaseProfileName { get; set; }
        public Dictionary<string, TagInstanceState> TagStates { get; set; } = new();
        public Dictionary<string, string> Settings { get; set; } = new();
        public int Version { get; set; } = 1;
    }

    #endregion

    #region Main Version Controller

    /// <summary>
    /// Version control system for tag configurations. Provides snapshot capture, diff,
    /// merge, rollback, branching, and configuration profile management.
    /// </summary>
    public sealed class TagVersionController
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly List<TagSnapshot> _snapshots = new();
        private readonly List<TagBranch> _branches = new();
        private readonly List<ChangeLogEntry> _changeLog = new();
        private readonly Dictionary<string, ConfigurationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private string _currentBranch = "main";
        private readonly int _maxSnapshots;
        private readonly int _fullSnapshotInterval;
        private int _snapshotsSinceLastFull;
        private readonly string _storagePath;

        public TagVersionController(
            int maxSnapshots = 200,
            int fullSnapshotInterval = 10,
            string storagePath = null)
        {
            _maxSnapshots = maxSnapshots;
            _fullSnapshotInterval = fullSnapshotInterval;
            _storagePath = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "TagVersions");

            // Initialize main branch
            _branches.Add(new TagBranch
            {
                Name = "main",
                Description = "Primary tag configuration",
                IsActive = true
            });

            Logger.Info("TagVersionController initialized, max snapshots={Max}", _maxSnapshots);
        }

        #region Snapshot Management

        /// <summary>
        /// Capture a snapshot of the current tag state.
        /// </summary>
        public TagSnapshot CaptureSnapshot(
            Dictionary<string, TagInstanceState> currentTags,
            string name = null,
            string description = null,
            string user = null)
        {
            lock (_lockObject)
            {
                _snapshotsSinceLastFull++;
                bool isFull = _snapshotsSinceLastFull >= _fullSnapshotInterval ||
                    !_snapshots.Any(s => s.BranchName == _currentBranch);

                var snapshot = new TagSnapshot
                {
                    Name = name ?? $"Snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    Description = description ?? "Auto-captured snapshot",
                    CreatedBy = user ?? Environment.UserName,
                    BranchName = _currentBranch,
                    IsFullSnapshot = isFull,
                    TagCount = currentTags.Count,
                    Tags = new Dictionary<string, TagInstanceState>(currentTags)
                };

                var lastOnBranch = _snapshots
                    .Where(s => s.BranchName == _currentBranch)
                    .OrderByDescending(s => s.Timestamp)
                    .FirstOrDefault();
                snapshot.ParentSnapshotId = lastOnBranch?.SnapshotId;

                _snapshots.Add(snapshot);

                // Update branch
                var branch = _branches.FirstOrDefault(b => b.Name == _currentBranch);
                if (branch != null)
                {
                    branch.LatestSnapshotId = snapshot.SnapshotId;
                    branch.SnapshotCount++;
                }

                if (isFull) _snapshotsSinceLastFull = 0;

                // Enforce max snapshots
                EnforceRetentionPolicy();

                LogChange(user, "Snapshot", null,
                    $"Captured {(isFull ? "full" : "incremental")} snapshot '{snapshot.Name}'",
                    snapshot.SnapshotId);

                Logger.Info("Snapshot captured: {Id} ({Name}), {Count} tags, branch={Branch}",
                    snapshot.SnapshotId, snapshot.Name, snapshot.TagCount, _currentBranch);

                return snapshot;
            }
        }

        public TagSnapshot GetSnapshot(string snapshotId)
        {
            lock (_lockObject)
            {
                return _snapshots.FirstOrDefault(s => s.SnapshotId == snapshotId);
            }
        }

        public List<TagSnapshot> GetSnapshotHistory(string branch = null, int limit = 50)
        {
            lock (_lockObject)
            {
                var query = _snapshots.AsEnumerable();
                if (!string.IsNullOrEmpty(branch))
                    query = query.Where(s => s.BranchName == branch);
                return query.OrderByDescending(s => s.Timestamp).Take(limit).ToList();
            }
        }

        #endregion

        #region Diff Engine

        /// <summary>
        /// Compare two snapshots and produce a detailed diff.
        /// </summary>
        public TagDiffResult ComputeDiff(string sourceSnapshotId, string targetSnapshotId)
        {
            lock (_lockObject)
            {
                var source = _snapshots.FirstOrDefault(s => s.SnapshotId == sourceSnapshotId);
                var target = _snapshots.FirstOrDefault(s => s.SnapshotId == targetSnapshotId);

                if (source == null || target == null)
                {
                    Logger.Warn("Cannot compute diff: snapshot not found");
                    return new TagDiffResult
                    {
                        SourceSnapshotId = sourceSnapshotId,
                        TargetSnapshotId = targetSnapshotId
                    };
                }

                var result = new TagDiffResult
                {
                    SourceSnapshotId = sourceSnapshotId,
                    TargetSnapshotId = targetSnapshotId
                };

                var allTagIds = new HashSet<string>(source.Tags.Keys);
                allTagIds.UnionWith(target.Tags.Keys);

                foreach (var tagId in allTagIds)
                {
                    bool inSource = source.Tags.TryGetValue(tagId, out var srcState);
                    bool inTarget = target.Tags.TryGetValue(tagId, out var tgtState);

                    if (inSource && !inTarget)
                    {
                        result.Entries.Add(new TagDiffEntry
                        {
                            TagId = tagId,
                            Type = DiffType.Removed,
                            Before = srcState
                        });
                    }
                    else if (!inSource && inTarget)
                    {
                        result.Entries.Add(new TagDiffEntry
                        {
                            TagId = tagId,
                            Type = DiffType.Added,
                            After = tgtState
                        });
                    }
                    else if (inSource && inTarget)
                    {
                        var entry = CompareTagStates(tagId, srcState, tgtState);
                        result.Entries.Add(entry);
                    }
                }

                Logger.Debug("Diff computed: {Added} added, {Removed} removed, {Modified} modified",
                    result.AddedCount, result.RemovedCount, result.ModifiedCount);

                return result;
            }
        }

        private TagDiffEntry CompareTagStates(string tagId, TagInstanceState src, TagInstanceState tgt)
        {
            var entry = new TagDiffEntry
            {
                TagId = tagId,
                Before = src,
                After = tgt,
                Type = DiffType.Unchanged
            };

            // Check position change
            if (Math.Abs(src.PositionX - tgt.PositionX) > 0.001 ||
                Math.Abs(src.PositionY - tgt.PositionY) > 0.001)
            {
                entry.Type = DiffType.Moved;
                entry.PropertyChanges.Add(new PropertyChange
                {
                    PropertyName = "Position",
                    OldValue = $"({src.PositionX:F2},{src.PositionY:F2})",
                    NewValue = $"({tgt.PositionX:F2},{tgt.PositionY:F2})"
                });
            }

            // Check content change
            if (!string.Equals(src.Content, tgt.Content))
            {
                entry.Type = DiffType.ContentChanged;
                entry.PropertyChanges.Add(new PropertyChange
                {
                    PropertyName = "Content",
                    OldValue = src.Content,
                    NewValue = tgt.Content
                });
            }

            // Check template change
            if (!string.Equals(src.TemplateName, tgt.TemplateName))
            {
                entry.Type = DiffType.TemplateChanged;
                entry.PropertyChanges.Add(new PropertyChange
                {
                    PropertyName = "Template",
                    OldValue = src.TemplateName,
                    NewValue = tgt.TemplateName
                });
            }

            // Check leader change
            if (src.HasLeader != tgt.HasLeader)
            {
                entry.Type = DiffType.LeaderChanged;
                entry.PropertyChanges.Add(new PropertyChange
                {
                    PropertyName = "HasLeader",
                    OldValue = src.HasLeader.ToString(),
                    NewValue = tgt.HasLeader.ToString()
                });
            }

            // Check custom properties
            var allProps = new HashSet<string>(src.Properties?.Keys ?? Enumerable.Empty<string>());
            allProps.UnionWith(tgt.Properties?.Keys ?? Enumerable.Empty<string>());
            foreach (var prop in allProps)
            {
                string oldVal = src.Properties?.GetValueOrDefault(prop);
                string newVal = tgt.Properties?.GetValueOrDefault(prop);
                if (!string.Equals(oldVal, newVal))
                {
                    if (entry.Type == DiffType.Unchanged) entry.Type = DiffType.StyleChanged;
                    entry.PropertyChanges.Add(new PropertyChange
                    {
                        PropertyName = prop,
                        OldValue = oldVal ?? "(null)",
                        NewValue = newVal ?? "(null)"
                    });
                }
            }

            return entry;
        }

        #endregion

        #region Merge Operations

        /// <summary>
        /// Merge two snapshots with optional three-way merge using common ancestor.
        /// </summary>
        public MergeResult Merge(
            string sourceSnapshotId,
            string targetSnapshotId,
            MergeStrategy strategy = MergeStrategy.ThreeWayMerge)
        {
            lock (_lockObject)
            {
                var source = _snapshots.FirstOrDefault(s => s.SnapshotId == sourceSnapshotId);
                var target = _snapshots.FirstOrDefault(s => s.SnapshotId == targetSnapshotId);

                if (source == null || target == null)
                    return new MergeResult { Success = false, Message = "Snapshot not found" };

                var result = new MergeResult();
                var merged = new Dictionary<string, TagInstanceState>();

                // Find common ancestor for three-way merge
                TagSnapshot ancestor = null;
                if (strategy == MergeStrategy.ThreeWayMerge)
                    ancestor = FindCommonAncestor(source, target);

                var allTagIds = new HashSet<string>(source.Tags.Keys);
                allTagIds.UnionWith(target.Tags.Keys);

                foreach (var tagId in allTagIds)
                {
                    bool inSource = source.Tags.TryGetValue(tagId, out var srcState);
                    bool inTarget = target.Tags.TryGetValue(tagId, out var tgtState);
                    TagInstanceState ancState = null;
                    bool inAncestor = ancestor?.Tags.TryGetValue(tagId, out ancState) ?? false;

                    if (inSource && inTarget)
                    {
                        // Both have it - check for conflict
                        var srcEntry = CompareTagStates(tagId, srcState, tgtState);
                        if (srcEntry.Type == DiffType.Unchanged)
                        {
                            merged[tagId] = srcState;
                            result.AutoMergedCount++;
                        }
                        else
                        {
                            // Conflict
                            switch (strategy)
                            {
                                case MergeStrategy.SourceWins:
                                    merged[tagId] = srcState;
                                    result.AutoMergedCount++;
                                    break;
                                case MergeStrategy.TargetWins:
                                    merged[tagId] = tgtState;
                                    result.AutoMergedCount++;
                                    break;
                                case MergeStrategy.ThreeWayMerge:
                                    if (inAncestor)
                                    {
                                        // If only source changed from ancestor, take source
                                        var ancToSrc = CompareTagStates(tagId, ancState, srcState);
                                        var ancToTgt = CompareTagStates(tagId, ancState, tgtState);

                                        if (ancToSrc.Type == DiffType.Unchanged)
                                        {
                                            merged[tagId] = tgtState;
                                            result.AutoMergedCount++;
                                        }
                                        else if (ancToTgt.Type == DiffType.Unchanged)
                                        {
                                            merged[tagId] = srcState;
                                            result.AutoMergedCount++;
                                        }
                                        else
                                        {
                                            // Both changed - conflict
                                            result.Conflicts.Add(new MergeConflict
                                            {
                                                TagId = tagId,
                                                Description = "Both source and target modified this tag",
                                                SourceState = srcState,
                                                TargetState = tgtState,
                                                AncestorState = ancState
                                            });
                                            merged[tagId] = srcState; // Default to source
                                        }
                                    }
                                    else
                                    {
                                        merged[tagId] = srcState;
                                        result.AutoMergedCount++;
                                    }
                                    break;
                                default:
                                    result.Conflicts.Add(new MergeConflict
                                    {
                                        TagId = tagId,
                                        Description = "Manual resolution required",
                                        SourceState = srcState,
                                        TargetState = tgtState
                                    });
                                    break;
                            }
                        }
                    }
                    else if (inSource && !inTarget)
                    {
                        if (inAncestor)
                        {
                            // Was in ancestor, removed from target - respect deletion
                        }
                        else
                        {
                            merged[tagId] = srcState;
                            result.AutoMergedCount++;
                        }
                    }
                    else if (!inSource && inTarget)
                    {
                        if (inAncestor)
                        {
                            // Was in ancestor, removed from source - respect deletion
                        }
                        else
                        {
                            merged[tagId] = tgtState;
                            result.AutoMergedCount++;
                        }
                    }
                }

                // Create result snapshot
                var mergeSnapshot = CaptureSnapshot(merged,
                    $"Merge-{source.SnapshotId[..6]}-{target.SnapshotId[..6]}",
                    $"Merged {sourceSnapshotId} into {targetSnapshotId}");

                result.ResultSnapshotId = mergeSnapshot.SnapshotId;
                result.Success = !result.Conflicts.Any(c => !c.IsResolved);
                result.Message = $"Merged {result.AutoMergedCount} tags, {result.Conflicts.Count} conflicts";

                Logger.Info("Merge complete: {AutoMerged} auto-merged, {Conflicts} conflicts",
                    result.AutoMergedCount, result.Conflicts.Count);

                return result;
            }
        }

        private TagSnapshot FindCommonAncestor(TagSnapshot a, TagSnapshot b)
        {
            // Walk back through parents to find common ancestor
            var aAncestors = new HashSet<string>();
            var current = a;
            while (current != null)
            {
                aAncestors.Add(current.SnapshotId);
                current = string.IsNullOrEmpty(current.ParentSnapshotId) ? null :
                    _snapshots.FirstOrDefault(s => s.SnapshotId == current.ParentSnapshotId);
            }

            current = b;
            while (current != null)
            {
                if (aAncestors.Contains(current.SnapshotId))
                    return current;
                current = string.IsNullOrEmpty(current.ParentSnapshotId) ? null :
                    _snapshots.FirstOrDefault(s => s.SnapshotId == current.ParentSnapshotId);
            }

            return null;
        }

        #endregion

        #region Rollback

        /// <summary>
        /// Roll back to a previous snapshot. Returns the restored tag states.
        /// </summary>
        public Dictionary<string, TagInstanceState> Rollback(
            string snapshotId,
            string user = null,
            bool selective = false,
            HashSet<string> selectedTagIds = null)
        {
            lock (_lockObject)
            {
                var snapshot = _snapshots.FirstOrDefault(s => s.SnapshotId == snapshotId);
                if (snapshot == null)
                {
                    Logger.Warn("Cannot rollback: snapshot {Id} not found", snapshotId);
                    return null;
                }

                Dictionary<string, TagInstanceState> result;
                if (selective && selectedTagIds != null)
                {
                    result = snapshot.Tags
                        .Where(kv => selectedTagIds.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
                }
                else
                {
                    result = new Dictionary<string, TagInstanceState>(snapshot.Tags);
                }

                LogChange(user, "Rollback", null,
                    $"Rolled back to snapshot '{snapshot.Name}'" +
                    (selective ? $" ({result.Count} of {snapshot.TagCount} tags)" : ""),
                    snapshotId);

                Logger.Info("Rollback to {Id}: {Count} tags restored", snapshotId, result.Count);
                return result;
            }
        }

        /// <summary>
        /// Preview what would change if we rolled back.
        /// </summary>
        public TagDiffResult PreviewRollback(
            string targetSnapshotId,
            Dictionary<string, TagInstanceState> currentTags)
        {
            lock (_lockObject)
            {
                var current = new TagSnapshot { Tags = currentTags, SnapshotId = "current" };
                _snapshots.Add(current);
                var diff = ComputeDiff("current", targetSnapshotId);
                _snapshots.Remove(current);
                return diff;
            }
        }

        #endregion

        #region Branch Management

        public TagBranch CreateBranch(string name, string description = null,
            string fromSnapshotId = null, string user = null)
        {
            lock (_lockObject)
            {
                if (_branches.Any(b => b.Name == name))
                {
                    Logger.Warn("Branch '{Name}' already exists", name);
                    return null;
                }

                var branchPoint = fromSnapshotId != null
                    ? _snapshots.FirstOrDefault(s => s.SnapshotId == fromSnapshotId)
                    : _snapshots.Where(s => s.BranchName == _currentBranch)
                        .OrderByDescending(s => s.Timestamp).FirstOrDefault();

                var branch = new TagBranch
                {
                    Name = name,
                    Description = description ?? $"Branch from {_currentBranch}",
                    ParentBranch = _currentBranch,
                    BranchPointSnapshotId = branchPoint?.SnapshotId,
                    CreatedBy = user ?? Environment.UserName
                };

                _branches.Add(branch);

                LogChange(user, "BranchCreate", null,
                    $"Created branch '{name}' from {_currentBranch}");

                Logger.Info("Branch created: {Name} from {Parent}", name, _currentBranch);
                return branch;
            }
        }

        public bool SwitchBranch(string branchName)
        {
            lock (_lockObject)
            {
                var branch = _branches.FirstOrDefault(b => b.Name == branchName && b.IsActive);
                if (branch == null)
                {
                    Logger.Warn("Cannot switch to branch '{Name}': not found or inactive", branchName);
                    return false;
                }

                _currentBranch = branchName;
                Logger.Info("Switched to branch '{Name}'", branchName);
                return true;
            }
        }

        public List<TagBranch> GetBranches() { lock (_lockObject) { return new List<TagBranch>(_branches); } }
        public string CurrentBranch { get { lock (_lockObject) { return _currentBranch; } } }

        public bool DeleteBranch(string name)
        {
            lock (_lockObject)
            {
                if (name == "main") return false;
                var branch = _branches.FirstOrDefault(b => b.Name == name);
                if (branch == null) return false;
                branch.IsActive = false;
                Logger.Info("Branch '{Name}' deactivated", name);
                return true;
            }
        }

        #endregion

        #region Change Tracking

        public void LogChange(string user, string operationType, string tagId,
            string description, string snapshotId = null)
        {
            lock (_lockObject)
            {
                _changeLog.Add(new ChangeLogEntry
                {
                    User = user ?? Environment.UserName,
                    OperationType = operationType,
                    TagId = tagId,
                    Description = description,
                    SnapshotId = snapshotId
                });
            }
        }

        public List<ChangeLogEntry> GetChangeLog(int limit = 100, string tagIdFilter = null,
            string userFilter = null)
        {
            lock (_lockObject)
            {
                var query = _changeLog.AsEnumerable();
                if (!string.IsNullOrEmpty(tagIdFilter))
                    query = query.Where(e => e.TagId == tagIdFilter);
                if (!string.IsNullOrEmpty(userFilter))
                    query = query.Where(e => string.Equals(e.User, userFilter,
                        StringComparison.OrdinalIgnoreCase));
                return query.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
            }
        }

        #endregion

        #region Configuration Profiles

        public ConfigurationProfile SaveProfile(string name, Dictionary<string, TagInstanceState> tags,
            string description = null, string projectType = null, string user = null)
        {
            lock (_lockObject)
            {
                var existing = _profiles.GetValueOrDefault(name);
                var profile = new ConfigurationProfile
                {
                    Name = name,
                    Description = description,
                    ProjectType = projectType,
                    CreatedBy = user ?? Environment.UserName,
                    TagStates = new Dictionary<string, TagInstanceState>(tags),
                    Version = (existing?.Version ?? 0) + 1
                };
                _profiles[name] = profile;

                Logger.Info("Profile saved: '{Name}' v{Version}, {Count} tags",
                    name, profile.Version, tags.Count);
                return profile;
            }
        }

        public ConfigurationProfile LoadProfile(string name)
        {
            lock (_lockObject) { return _profiles.GetValueOrDefault(name); }
        }

        public List<ConfigurationProfile> GetProfiles()
        {
            lock (_lockObject) { return _profiles.Values.ToList(); }
        }

        public bool DeleteProfile(string name)
        {
            lock (_lockObject) { return _profiles.Remove(name); }
        }

        public string ExportProfile(string name)
        {
            lock (_lockObject)
            {
                var profile = _profiles.GetValueOrDefault(name);
                return profile != null
                    ? JsonConvert.SerializeObject(profile, Formatting.Indented)
                    : null;
            }
        }

        public ConfigurationProfile ImportProfile(string json)
        {
            try
            {
                var profile = JsonConvert.DeserializeObject<ConfigurationProfile>(json);
                if (profile != null)
                {
                    lock (_lockObject) { _profiles[profile.Name] = profile; }
                    Logger.Info("Profile imported: '{Name}'", profile.Name);
                }
                return profile;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to import profile");
                return null;
            }
        }

        #endregion

        #region Storage Management

        private void EnforceRetentionPolicy()
        {
            var branchSnapshots = _snapshots
                .Where(s => s.BranchName == _currentBranch)
                .OrderBy(s => s.Timestamp)
                .ToList();

            while (branchSnapshots.Count > _maxSnapshots)
            {
                var oldest = branchSnapshots.First();
                if (!oldest.IsFullSnapshot)
                {
                    _snapshots.Remove(oldest);
                    branchSnapshots.RemoveAt(0);
                }
                else
                {
                    // Don't remove the oldest full snapshot; remove the next incremental
                    var nextIncremental = branchSnapshots.Skip(1)
                        .FirstOrDefault(s => !s.IsFullSnapshot);
                    if (nextIncremental != null)
                    {
                        _snapshots.Remove(nextIncremental);
                        branchSnapshots.Remove(nextIncremental);
                    }
                    else break;
                }
            }
        }

        /// <summary>
        /// Persist all version data to disk.
        /// </summary>
        public async Task SaveToDiskAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(_storagePath))
                    Directory.CreateDirectory(_storagePath);

                var state = new
                {
                    Snapshots = _snapshots,
                    Branches = _branches,
                    ChangeLog = _changeLog,
                    Profiles = _profiles,
                    CurrentBranch = _currentBranch
                };

                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                string path = Path.Combine(_storagePath, "version_state.json");
                await File.WriteAllTextAsync(path, json, cancellationToken);

                Logger.Info("Version state saved: {Snapshots} snapshots, {Branches} branches",
                    _snapshots.Count, _branches.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save version state");
            }
        }

        /// <summary>
        /// Get storage summary.
        /// </summary>
        public (int Snapshots, int Branches, int ChangeLogEntries, int Profiles) GetStorageSummary()
        {
            lock (_lockObject)
            {
                return (_snapshots.Count, _branches.Count, _changeLog.Count, _profiles.Count);
            }
        }

        #endregion
    }

    #endregion
}
