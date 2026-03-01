// StingBIM.AI.Collaboration.LAN.OfflineLANCollaborationManager
// LAN worksharing setup, workset management, sync-to-central, auto-sync
// v4 Prompt Reference: Section C.1–C.2 — Offline LAN Worksharing Collaboration

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Collaboration.LAN
{
    /// <summary>
    /// Manages Revit Worksharing on a local area network with no internet dependency.
    /// Central model lives on a mapped LAN drive (\\SERVER\Projects\ or Z:\Projects\).
    /// All team communication via shared JSON files on the server.
    ///
    /// Eight responsibilities:
    /// 1. Enable Revit Worksharing on a LAN server path
    /// 2. Create and manage worksets by discipline
    /// 3. Monitor element borrowing status per team member
    /// 4. Orchestrate sync-to-central workflow
    /// 5. Detect and present edit conflicts for resolution
    /// 6. Broadcast notifications via JSON files on the shared drive
    /// 7. Manage model backups and recovery
    /// 8. Track all changes with team attribution
    /// </summary>
    public class OfflineLANCollaborationManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _syncLock = new object();

        private Timer _autoSyncTimer;
        private Timer _autoBackupTimer;
        private bool _autoSyncEnabled;
        private string _serverPath;
        private string _projectName;
        private string _currentUser;

        // Configurable intervals (milliseconds)
        private const int DEFAULT_AUTO_SYNC_INTERVAL = 30 * 60 * 1000;  // 30 minutes
        private const int DEFAULT_AUTO_BACKUP_INTERVAL = 2 * 60 * 60 * 1000; // 2 hours
        private const int MAX_BACKUPS = 10;
        private const int NETWORK_RETRY_COUNT = 3;
        private const int NETWORK_RETRY_DELAY_MS = 2000;

        /// <summary>
        /// Event raised when a sync completes (for UI notification).
        /// </summary>
        public event EventHandler<SyncCompletedEventArgs> SyncCompleted;

        /// <summary>
        /// Event raised when a conflict is detected during sync.
        /// </summary>
        public event EventHandler<ConflictDetectedEventArgs> ConflictDetected;

        /// <summary>
        /// Event raised when a team member syncs (from FileSystemWatcher).
        /// </summary>
        public event EventHandler<TeamActivityEventArgs> TeamActivity;

        public OfflineLANCollaborationManager()
        {
            _currentUser = Environment.UserName;
        }

        #region C.1 Worksharing Setup

        /// <summary>
        /// Enable Revit Worksharing on the LAN server.
        /// Creates default worksets, saves as central, initializes team JSON files.
        /// </summary>
        public CollaborationResult SetupWorksharing(Document doc, string serverPath, string projectName)
        {
            _serverPath = serverPath;
            _projectName = projectName;

            Logger.Info($"Setting up worksharing: {serverPath}/{projectName}");

            // Step 1: Check server accessibility
            if (!IsServerAccessible(serverPath))
            {
                return CollaborationResult.Failed(
                    "Cannot reach server. Check LAN connection and verify the path is correct.");
            }

            try
            {
                // Step 2: Check if already workshared
                if (doc.IsWorkshared)
                {
                    Logger.Info("Document already workshared — creating local copy");
                    return CollaborationResult.Succeeded(
                        $"Document is already workshared.\n" +
                        $"Central model: {GetCentralModelPath()}\n" +
                        "Use 'Sync to Central' to push your changes.",
                        new List<string> { "Sync to Central", "View team members", "Check worksets" });
                }

                // Step 3: Enable worksharing
                doc.EnableWorksharing("Shared Levels and Grids", "Architecture");
                Logger.Info("Worksharing enabled on document");

                // Step 4: Create default worksets
                var worksetCount = CreateDefaultWorksets(doc);

                // Step 5: Save as central
                var centralPath = GetCentralModelPath();
                var saveOptions = new SaveAsOptions
                {
                    OverwriteExistingFile = true
                };
                var wsOptions = new WorksharingSaveAsOptions
                {
                    SaveAsCentral = true
                };
                saveOptions.SetWorksharingOptions(wsOptions);

                var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(centralPath);
                doc.SaveAs(modelPath, saveOptions);

                // Step 6: Initialize team JSON files
                InitializeTeamFiles();

                // Step 7: Register current user
                RegisterTeamMember(_currentUser);

                Logger.Info($"Worksharing setup complete: {centralPath}");

                return CollaborationResult.Succeeded(
                    $"Worksharing enabled. Central model: {centralPath}\n" +
                    $"{worksetCount} discipline worksets created.\n" +
                    $"Team registered as: {_currentUser}",
                    new List<string> { "View worksets", "Invite team member", "Sync to Central" });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Worksharing setup failed");
                return CollaborationResult.Failed($"Worksharing setup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create 10 default discipline worksets per v4 spec.
        /// </summary>
        private int CreateDefaultWorksets(Document doc)
        {
            var worksetNames = new[]
            {
                "Shared Levels & Grids",
                "Architecture",
                "Structure",
                "MEP - Electrical",
                "MEP - Plumbing",
                "MEP - HVAC",
                "MEP - Fire Protection",
                "Interior Finishes",
                "Site & Landscape",
                "Furniture & Equipment"
            };

            int created = 0;

            using (var t = new Transaction(doc, "StingBIM: Create Worksets"))
            {
                t.Start();
                try
                {
                    foreach (var name in worksetNames)
                    {
                        if (!WorksetExists(doc, name))
                        {
                            Workset.Create(doc, name);
                            created++;
                            Logger.Debug($"Created workset: {name}");
                        }
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    Logger.Error(ex, "Failed to create worksets");
                }
            }

            return created;
        }

        private bool WorksetExists(Document doc, string name)
        {
            return new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Any(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region C.2 Sync Algorithm

        /// <summary>
        /// Synchronize local changes with the central model.
        /// Pre-checks for server access, lock files, and conflicts.
        /// </summary>
        public CollaborationResult SyncToCentral(Document doc, string syncComment = "")
        {
            lock (_syncLock)
            {
                Logger.Info($"Sync to Central: {syncComment}");

                // PRE-SYNC CHECK 1: Server accessible
                if (!IsServerAccessibleWithRetry(_serverPath))
                {
                    return CollaborationResult.Failed(
                        "Cannot reach the server after 3 attempts. Check LAN connection.");
                }

                // PRE-SYNC CHECK 2: No lock file
                var lockFile = GetCentralModelPath() + ".lock";
                if (File.Exists(lockFile))
                {
                    var lockInfo = TryReadLockFile(lockFile);
                    return CollaborationResult.Failed(
                        $"Another user is syncing: {lockInfo}. Please wait and try again.");
                }

                // PRE-SYNC CHECK 3: Detect conflicts
                var conflicts = DetectConflicts(doc);
                if (conflicts.Count > 0)
                {
                    ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs(conflicts));
                    return CollaborationResult.ConflictsFound(conflicts);
                }

                try
                {
                    // Write lock file
                    WriteLockFile(lockFile);

                    // EXECUTE SYNC
                    var transOpts = new TransactWithCentralOptions();
                    var syncOpts = new SynchronizeWithCentralOptions();
                    var relinquishOpts = new RelinquishOptions(false); // Keep borrowed elements

                    syncOpts.SetRelinquishOptions(relinquishOpts);
                    syncOpts.Comment = string.IsNullOrEmpty(syncComment)
                        ? $"[StingBIM Auto] {_currentUser}"
                        : $"{syncComment} [StingBIM Auto]";
                    syncOpts.SaveLocalBefore = true;
                    syncOpts.SaveLocalAfter = true;
                    syncOpts.Compact = false;

                    doc.SynchronizeWithCentral(transOpts, syncOpts);

                    // POST-SYNC: Update JSON files
                    var changeEntry = BuildChangeLogEntry(doc, syncComment);
                    AppendToChangeLog(changeEntry);
                    BroadcastNotification($"{_currentUser} synced: {changeEntry.Summary}");
                    UpdateTeamMemberSync(_currentUser);

                    Logger.Info("Sync to Central completed successfully");

                    SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(true, changeEntry.Summary));

                    return CollaborationResult.Succeeded(
                        $"Synced successfully!\n{changeEntry.Summary}",
                        new List<string> { "View changelog", "Check team status", "Continue editing" });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Sync to Central failed");
                    SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(false, ex.Message));
                    return CollaborationResult.Failed($"Sync failed: {ex.Message}");
                }
                finally
                {
                    // Always clean up lock file
                    TryDeleteFile(lockFile);
                }
            }
        }

        /// <summary>
        /// Detect elements modified both locally and in the central model.
        /// </summary>
        private List<SyncConflict> DetectConflicts(Document doc)
        {
            var conflicts = new List<SyncConflict>();

            try
            {
                // Get model update status for all elements
                var modelPath = doc.GetWorksharingCentralModelPath();
                if (modelPath == null) return conflicts;

                // Check for elements owned by others that we've also modified
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                foreach (var elem in collector)
                {
                    try
                    {
                        var status = WorksharingUtils.GetCheckoutStatus(doc, elem.Id);
                        if (status == CheckoutStatus.OwnedByOtherUser)
                        {
                            var tooltipInfo = WorksharingUtils.GetWorksharingTooltipInfo(doc, elem.Id);
                            conflicts.Add(new SyncConflict
                            {
                                ElementId = elem.Id,
                                ElementCategory = elem.Category?.Name ?? "Unknown",
                                OwnedBy = tooltipInfo.Owner,
                                LastChangedBy = tooltipInfo.LastChangedBy,
                                Description = $"{elem.Category?.Name} owned by {tooltipInfo.Owner}"
                            });
                        }
                    }
                    catch
                    {
                        // Skip elements that can't be checked
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Conflict detection encountered an error");
            }

            return conflicts;
        }

        #endregion

        #region Auto-Sync Timer

        /// <summary>
        /// Start the auto-sync timer (default: every 30 minutes).
        /// </summary>
        public void StartAutoSync(Document doc, int intervalMs = DEFAULT_AUTO_SYNC_INTERVAL)
        {
            StopAutoSync();

            _autoSyncEnabled = true;
            _autoSyncTimer = new Timer(intervalMs);
            _autoSyncTimer.Elapsed += (sender, e) =>
            {
                if (!_autoSyncEnabled) return;

                Logger.Info("Auto-sync timer triggered");

                var conflicts = DetectConflicts(doc);
                if (conflicts.Count > 0)
                {
                    _autoSyncEnabled = false;
                    ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs(conflicts));
                    Logger.Warn("Auto-sync paused — conflicts detected");
                    return;
                }

                var result = SyncToCentral(doc, "Auto-sync");
                if (!result.Success)
                {
                    Logger.Warn($"Auto-sync failed: {result.Error}");
                    // Queue for retry — don't disable
                }
            };
            _autoSyncTimer.AutoReset = true;
            _autoSyncTimer.Start();

            Logger.Info($"Auto-sync started: every {intervalMs / 60000} minutes");
        }

        /// <summary>
        /// Stop the auto-sync timer.
        /// </summary>
        public void StopAutoSync()
        {
            _autoSyncEnabled = false;
            _autoSyncTimer?.Stop();
            _autoSyncTimer?.Dispose();
            _autoSyncTimer = null;
        }

        /// <summary>
        /// Resume auto-sync after conflict resolution.
        /// </summary>
        public void ResumeAutoSync()
        {
            _autoSyncEnabled = true;
            Logger.Info("Auto-sync resumed");
        }

        #endregion

        #region Team Management

        /// <summary>
        /// Register a team member in the shared _team.json.
        /// </summary>
        public void RegisterTeamMember(string userName)
        {
            var teamFile = GetTeamFilePath();
            var team = LoadTeamFile(teamFile);

            var existing = team.FirstOrDefault(m =>
                m.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastSeen = DateTime.Now;
                existing.IsOnline = true;
            }
            else
            {
                team.Add(new TeamMember
                {
                    UserName = userName,
                    MachineName = Environment.MachineName,
                    JoinedAt = DateTime.Now,
                    LastSeen = DateTime.Now,
                    LastSync = DateTime.MinValue,
                    IsOnline = true
                });
            }

            SaveTeamFile(teamFile, team);
        }

        /// <summary>
        /// Get current team members from _team.json.
        /// </summary>
        public List<TeamMember> GetTeamMembers()
        {
            return LoadTeamFile(GetTeamFilePath());
        }

        /// <summary>
        /// Update the last sync timestamp for a team member.
        /// </summary>
        private void UpdateTeamMemberSync(string userName)
        {
            var teamFile = GetTeamFilePath();
            var team = LoadTeamFile(teamFile);

            var member = team.FirstOrDefault(m =>
                m.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (member != null)
            {
                member.LastSync = DateTime.Now;
                member.LastSeen = DateTime.Now;
                SaveTeamFile(teamFile, team);
            }
        }

        #endregion

        #region Notifications

        /// <summary>
        /// Broadcast a notification to all team members via _notifications.json.
        /// </summary>
        public void BroadcastNotification(string message)
        {
            try
            {
                var notifFile = GetNotificationsFilePath();
                var notifications = LoadNotifications(notifFile);

                notifications.Add(new LANNotification
                {
                    User = _currentUser,
                    Message = message,
                    Timestamp = DateTime.Now
                });

                // Keep last 100 notifications
                if (notifications.Count > 100)
                    notifications = notifications.Skip(notifications.Count - 100).ToList();

                SaveNotifications(notifFile, notifications);
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to broadcast notification — will retry on next sync");
            }
        }

        #endregion

        #region File Helpers

        private string GetCentralModelPath()
        {
            return Path.Combine(_serverPath, $"{_projectName}_Central.rvt");
        }

        private string GetTeamFilePath()
        {
            return Path.Combine(_serverPath, $"{_projectName}_team.json");
        }

        private string GetNotificationsFilePath()
        {
            return Path.Combine(_serverPath, $"{_projectName}_notifications.json");
        }

        private string GetChangeLogFilePath()
        {
            return Path.Combine(_serverPath, $"{_projectName}_changelog.json");
        }

        private void InitializeTeamFiles()
        {
            try
            {
                var teamFile = GetTeamFilePath();
                if (!File.Exists(teamFile))
                    File.WriteAllText(teamFile, "[]");

                var notifFile = GetNotificationsFilePath();
                if (!File.Exists(notifFile))
                    File.WriteAllText(notifFile, "[]");

                var changeLog = GetChangeLogFilePath();
                if (!File.Exists(changeLog))
                    File.WriteAllText(changeLog, "[]");
            }
            catch (IOException ex)
            {
                Logger.Error(ex, "Failed to initialize team files");
            }
        }

        private bool IsServerAccessible(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private bool IsServerAccessibleWithRetry(string path)
        {
            for (int i = 0; i < NETWORK_RETRY_COUNT; i++)
            {
                if (IsServerAccessible(path)) return true;
                if (i < NETWORK_RETRY_COUNT - 1)
                    System.Threading.Thread.Sleep(NETWORK_RETRY_DELAY_MS);
            }
            return false;
        }

        private void WriteLockFile(string lockPath)
        {
            try
            {
                var lockInfo = new { User = _currentUser, Machine = Environment.MachineName, Time = DateTime.Now };
                File.WriteAllText(lockPath, JsonConvert.SerializeObject(lockInfo));
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to write lock file");
            }
        }

        private string TryReadLockFile(string lockPath)
        {
            try
            {
                if (File.Exists(lockPath))
                    return File.ReadAllText(lockPath);
            }
            catch { }
            return "Unknown user";
        }

        private void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private List<TeamMember> LoadTeamFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<List<TeamMember>>(json) ?? new List<TeamMember>();
                }
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to read team file");
            }
            return new List<TeamMember>();
        }

        private void SaveTeamFile(string path, List<TeamMember> team)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(team, Formatting.Indented));
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to save team file");
            }
        }

        private List<LANNotification> LoadNotifications(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<List<LANNotification>>(json) ?? new List<LANNotification>();
                }
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to read notifications");
            }
            return new List<LANNotification>();
        }

        private void SaveNotifications(string path, List<LANNotification> notifications)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(notifications, Formatting.Indented));
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to save notifications");
            }
        }

        private void AppendToChangeLog(ChangeLogEntry entry)
        {
            try
            {
                var path = GetChangeLogFilePath();
                var log = new List<ChangeLogEntry>();

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    log = JsonConvert.DeserializeObject<List<ChangeLogEntry>>(json) ?? new List<ChangeLogEntry>();
                }

                log.Add(entry);
                File.WriteAllText(path, JsonConvert.SerializeObject(log, Formatting.Indented));
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to append to changelog");
            }
        }

        private ChangeLogEntry BuildChangeLogEntry(Document doc, string comment)
        {
            return new ChangeLogEntry
            {
                User = _currentUser,
                Timestamp = DateTime.Now,
                Comment = comment,
                Summary = $"{_currentUser} synced changes"
            };
        }

        #endregion
    }

    #region Data Types

    public class CollaborationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public List<string> Suggestions { get; set; }
        public List<SyncConflict> Conflicts { get; set; }

        public string FormatForChat()
        {
            if (!Success)
                return string.IsNullOrEmpty(Error) ? Message : $"Error: {Error}";
            return Message;
        }

        public static CollaborationResult Succeeded(string message, List<string> suggestions = null)
        {
            return new CollaborationResult
            {
                Success = true,
                Message = message,
                Suggestions = suggestions ?? new List<string>()
            };
        }

        public static CollaborationResult Failed(string error)
        {
            return new CollaborationResult
            {
                Success = false,
                Error = error,
                Suggestions = new List<string> { "Check LAN connection", "Try again" }
            };
        }

        public static CollaborationResult ConflictsFound(List<SyncConflict> conflicts)
        {
            var msg = $"Found {conflicts.Count} conflict(s):\n" +
                string.Join("\n", conflicts.Select(c =>
                    $"  - {c.ElementCategory} owned by {c.OwnedBy}"));
            return new CollaborationResult
            {
                Success = false,
                Message = msg,
                Conflicts = conflicts,
                Suggestions = new List<string> { "Resolve conflicts", "Force sync", "Cancel" }
            };
        }
    }

    public class TeamMember
    {
        public string UserName { get; set; }
        public string MachineName { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastSync { get; set; }
        public bool IsOnline { get; set; }
        public string AssignedWorkset { get; set; }
    }

    public class LANNotification
    {
        public string User { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChangeLogEntry
    {
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
        public string Comment { get; set; }
        public string Summary { get; set; }
        public List<string> Added { get; set; }
        public List<string> Modified { get; set; }
        public List<string> Deleted { get; set; }
    }

    public class SyncConflict
    {
        public ElementId ElementId { get; set; }
        public string ElementCategory { get; set; }
        public string OwnedBy { get; set; }
        public string LastChangedBy { get; set; }
        public string Description { get; set; }
    }

    public class SyncCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public string Summary { get; }
        public SyncCompletedEventArgs(bool success, string summary)
        {
            Success = success;
            Summary = summary;
        }
    }

    public class ConflictDetectedEventArgs : EventArgs
    {
        public List<SyncConflict> Conflicts { get; }
        public ConflictDetectedEventArgs(List<SyncConflict> conflicts)
        {
            Conflicts = conflicts;
        }
    }

    public class TeamActivityEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public string Activity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
