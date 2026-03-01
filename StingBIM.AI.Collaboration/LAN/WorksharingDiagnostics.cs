// StingBIM.AI.Collaboration.LAN.WorksharingDiagnostics
// "Why can't I edit this wall?" + recovery + auto-backup
// v4 Prompt Reference: Section C.4 — Worksharing Diagnostics & Recovery

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
    /// Worksharing diagnostics and recovery:
    /// - "Why can't I edit this wall?" — checkout status + tooltips
    /// - Model recovery from backup
    /// - Auto-backup timer (every 2 hours)
    /// - Model health checks
    /// </summary>
    public class WorksharingDiagnostics
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private Timer _autoBackupTimer;
        private string _serverPath;
        private string _projectName;
        private const int MAX_BACKUPS = 10;
        private const int DEFAULT_BACKUP_INTERVAL = 2 * 60 * 60 * 1000; // 2 hours

        public WorksharingDiagnostics(string serverPath, string projectName)
        {
            _serverPath = serverPath;
            _projectName = projectName;
        }

        #region "Why can't I edit?" Diagnostics

        /// <summary>
        /// Diagnose why a specific element cannot be edited.
        /// Returns a human-readable explanation with resolution steps.
        /// </summary>
        public string DiagnoseEditStatus(Document doc, ElementId elementId)
        {
            if (!doc.IsWorkshared)
                return "The document is not workshared — you should be able to edit all elements.";

            var elem = doc.GetElement(elementId);
            if (elem == null)
                return "Element not found in the model. It may have been deleted.";

            try
            {
                var status = WorksharingUtils.GetCheckoutStatus(doc, elementId);
                var tooltip = WorksharingUtils.GetWorksharingTooltipInfo(doc, elementId);

                switch (status)
                {
                    case CheckoutStatus.OwnedByCurrentUser:
                        return $"You already own this {elem.Category?.Name ?? "element"}. " +
                            "You should be able to edit it.";

                    case CheckoutStatus.OwnedByOtherUser:
                        return $"This {elem.Category?.Name ?? "element"} is owned by {tooltip.Owner}.\n" +
                            $"Last changed by: {tooltip.LastChangedBy}\n\n" +
                            "Options:\n" +
                            $"  1. Ask {tooltip.Owner} to release it (sync to central)\n" +
                            "  2. Wait for them to sync and then reload latest\n" +
                            "  3. Work on a different element in the meantime";

                    case CheckoutStatus.NotOwned:
                        // Check workset
                        var worksetParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (worksetParam != null)
                        {
                            var worksetId = worksetParam.AsElementId();
                            var workset = doc.GetElement(worksetId);
                            return $"This element is in workset '{workset?.Name ?? "Unknown"}'.\n" +
                                "You may need to open this workset for editing.\n" +
                                "Try: 'Open workset' from the Worksets dialog.";
                        }
                        return "Element is not checked out by anyone. Try editing it directly.";

                    default:
                        return "Unable to determine edit status. Try syncing with central first.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Edit status diagnosis failed");
                return $"Could not check edit status: {ex.Message}\n" +
                    "Try syncing with central first.";
            }
        }

        /// <summary>
        /// Run a full model health check for worksharing issues.
        /// </summary>
        public ModelHealthReport RunHealthCheck(Document doc)
        {
            var report = new ModelHealthReport();

            if (!doc.IsWorkshared)
            {
                report.Summary = "Document is not workshared.";
                return report;
            }

            try
            {
                // Check 1: Elements without workset
                var noWorksetCount = 0;
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                foreach (var elem in collector)
                {
                    try
                    {
                        var wsParam = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (wsParam == null || wsParam.AsElementId() == ElementId.InvalidElementId)
                            noWorksetCount++;
                    }
                    catch { }
                }

                if (noWorksetCount > 0)
                    report.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Description = $"{noWorksetCount} element(s) have no workset assigned.",
                        Recommendation = "Assign these to the 'Architecture' workset."
                    });

                // Check 2: File size
                var fileInfo = new FileInfo(doc.PathName);
                if (fileInfo.Exists && fileInfo.Length > 500 * 1024 * 1024) // >500MB
                    report.Issues.Add(new HealthIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Description = $"Model file is large ({fileInfo.Length / (1024 * 1024):F0} MB).",
                        Recommendation = "Consider purging unused families to reduce file size."
                    });

                // Check 3: Team sync status
                var teamFile = Path.Combine(_serverPath, $"{_projectName}_team.json");
                if (File.Exists(teamFile))
                {
                    var json = File.ReadAllText(teamFile);
                    var team = JsonConvert.DeserializeObject<List<TeamMember>>(json) ?? new List<TeamMember>();

                    foreach (var member in team)
                    {
                        if (member.IsOnline && member.LastSync != DateTime.MinValue)
                        {
                            var hoursSinceSync = (DateTime.Now - member.LastSync).TotalHours;
                            if (hoursSinceSync > 8)
                            {
                                report.Issues.Add(new HealthIssue
                                {
                                    Severity = IssueSeverity.Warning,
                                    Description = $"{member.UserName} has not synced in {hoursSinceSync:F0} hours.",
                                    Recommendation = "Their changes may conflict. Ask them to sync."
                                });
                            }
                        }
                    }
                }

                // Check 4: Central model sync age
                var centralPath = Path.Combine(_serverPath, $"{_projectName}_Central.rvt");
                if (File.Exists(centralPath))
                {
                    var centralAge = DateTime.Now - File.GetLastWriteTime(centralPath);
                    if (centralAge.TotalHours > 2)
                    {
                        report.Issues.Add(new HealthIssue
                        {
                            Severity = IssueSeverity.Info,
                            Description = $"Central model not synced in {centralAge.TotalHours:F0}h {centralAge.Minutes}m.",
                            Recommendation = "Recommend syncing to keep central up to date."
                        });
                    }
                }

                report.Summary = report.Issues.Count > 0
                    ? $"Found {report.Issues.Count} issue(s)."
                    : "Model health is good. No issues detected.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Health check failed");
                report.Summary = $"Health check error: {ex.Message}";
            }

            return report;
        }

        #endregion

        #region Backup & Recovery

        /// <summary>
        /// Start auto-backup timer (default: every 2 hours).
        /// Backs up the central model, retains last 10 backups.
        /// </summary>
        public void StartAutoBackup(int intervalMs = DEFAULT_BACKUP_INTERVAL)
        {
            StopAutoBackup();

            _autoBackupTimer = new Timer(intervalMs);
            _autoBackupTimer.Elapsed += (sender, e) =>
            {
                try
                {
                    CreateBackup();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Auto-backup failed");
                }
            };
            _autoBackupTimer.AutoReset = true;
            _autoBackupTimer.Start();

            Logger.Info($"Auto-backup started: every {intervalMs / 3600000} hours");
        }

        /// <summary>
        /// Stop auto-backup timer.
        /// </summary>
        public void StopAutoBackup()
        {
            _autoBackupTimer?.Stop();
            _autoBackupTimer?.Dispose();
            _autoBackupTimer = null;
        }

        /// <summary>
        /// Create a backup of the central model.
        /// </summary>
        public CollaborationResult CreateBackup()
        {
            var centralPath = Path.Combine(_serverPath, $"{_projectName}_Central.rvt");
            if (!File.Exists(centralPath))
                return CollaborationResult.Failed("Central model not found.");

            var backupDir = Path.Combine(_serverPath, "_Backup");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
                var backupName = $"{_projectName}_Central_{timestamp}.rvt";
                var backupPath = Path.Combine(backupDir, backupName);

                File.Copy(centralPath, backupPath, true);
                Logger.Info($"Backup created: {backupPath}");

                // Prune old backups — keep last MAX_BACKUPS
                PruneBackups(backupDir);

                // Log to _autobackup.json
                LogBackup(backupPath);

                return CollaborationResult.Succeeded($"Backup created: {backupName}");
            }
            catch (IOException ex)
            {
                Logger.Error(ex, "Backup failed");
                return CollaborationResult.Failed($"Backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// List available backups.
        /// </summary>
        public List<BackupInfo> ListBackups()
        {
            var backupDir = Path.Combine(_serverPath, "_Backup");
            if (!Directory.Exists(backupDir))
                return new List<BackupInfo>();

            return Directory.GetFiles(backupDir, $"{_projectName}_Central_*.rvt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new BackupInfo
                {
                    FileName = f.Name,
                    FullPath = f.FullName,
                    CreatedAt = f.LastWriteTime,
                    SizeMB = f.Length / (1024.0 * 1024.0)
                })
                .ToList();
        }

        /// <summary>
        /// Restore the central model from a backup.
        /// WARNING: This replaces the central model — all changes since backup are lost.
        /// </summary>
        public CollaborationResult RestoreFromBackup(string backupPath)
        {
            var centralPath = Path.Combine(_serverPath, $"{_projectName}_Central.rvt");

            if (!File.Exists(backupPath))
                return CollaborationResult.Failed("Backup file not found.");

            try
            {
                // Remove any lock files first
                var lockFile = centralPath + ".lock";
                if (File.Exists(lockFile))
                    File.Delete(lockFile);

                // Replace central model
                File.Copy(backupPath, centralPath, true);

                Logger.Info($"Model restored from backup: {backupPath}");

                return CollaborationResult.Succeeded(
                    $"Model restored from backup.\n" +
                    $"Source: {Path.GetFileName(backupPath)}\n" +
                    "All team members must re-sync to get the restored version.",
                    new List<string> { "Notify team", "Sync now", "Check model health" });
            }
            catch (IOException ex)
            {
                Logger.Error(ex, "Restore failed");
                return CollaborationResult.Failed($"Restore failed: {ex.Message}");
            }
        }

        private void PruneBackups(string backupDir)
        {
            try
            {
                var files = Directory.GetFiles(backupDir, $"{_projectName}_Central_*.rvt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (files.Count > MAX_BACKUPS)
                {
                    foreach (var old in files.Skip(MAX_BACKUPS))
                    {
                        old.Delete();
                        Logger.Debug($"Pruned old backup: {old.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Backup pruning failed");
            }
        }

        private void LogBackup(string backupPath)
        {
            try
            {
                var logPath = Path.Combine(_serverPath, $"{_projectName}_autobackup.json");
                var log = new List<object>();

                if (File.Exists(logPath))
                {
                    var json = File.ReadAllText(logPath);
                    log = JsonConvert.DeserializeObject<List<object>>(json) ?? new List<object>();
                }

                log.Add(new { Path = backupPath, Timestamp = DateTime.Now, User = Environment.UserName });
                File.WriteAllText(logPath, JsonConvert.SerializeObject(log, Formatting.Indented));
            }
            catch { }
        }

        /// <summary>
        /// Format health report for the chat panel.
        /// </summary>
        public string FormatHealthReport(ModelHealthReport report)
        {
            var lines = new List<string>
            {
                "Model Health Report",
                "─────────────────────────────────────────",
                report.Summary
            };

            foreach (var issue in report.Issues)
            {
                var icon = issue.Severity == IssueSeverity.Warning ? "Warning" : "Info";
                lines.Add($"  [{icon}] {issue.Description}");
                lines.Add($"    → {issue.Recommendation}");
            }

            lines.Add("─────────────────────────────────────────");
            return string.Join("\n", lines);
        }

        #endregion
    }

    #region Health Report Types

    public class ModelHealthReport
    {
        public string Summary { get; set; } = "";
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }

    public class HealthIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public class BackupInfo
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public double SizeMB { get; set; }
    }

    #endregion
}
