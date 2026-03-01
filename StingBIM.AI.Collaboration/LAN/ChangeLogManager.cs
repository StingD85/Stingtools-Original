// StingBIM.AI.Collaboration.LAN.ChangeLogManager
// Changelog append, display, and export for LAN collaboration audit trail
// v4 Prompt Reference: Section C.2 — _changelog.json + Excel export

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Collaboration.LAN
{
    /// <summary>
    /// Manages the shared _changelog.json on the LAN server.
    /// Every sync appends an entry: {user, timestamp, comment, added[], modified[], deleted[]}
    /// Supports display in the CollaborationPanel and CSV export.
    /// </summary>
    public class ChangeLogManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private string _serverPath;
        private string _projectName;

        public ChangeLogManager(string serverPath, string projectName)
        {
            _serverPath = serverPath;
            _projectName = projectName;
        }

        private string ChangeLogPath => Path.Combine(_serverPath, $"{_projectName}_changelog.json");

        /// <summary>
        /// Append a new entry to the changelog.
        /// </summary>
        public void Append(ChangeLogEntry entry)
        {
            try
            {
                var log = LoadAll();
                log.Add(entry);
                File.WriteAllText(ChangeLogPath, JsonConvert.SerializeObject(log, Formatting.Indented));
                Logger.Info($"Changelog entry added: {entry.User} - {entry.Summary}");
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to append changelog entry");
            }
        }

        /// <summary>
        /// Load all changelog entries.
        /// </summary>
        public List<ChangeLogEntry> LoadAll()
        {
            try
            {
                if (File.Exists(ChangeLogPath))
                {
                    var json = File.ReadAllText(ChangeLogPath);
                    return JsonConvert.DeserializeObject<List<ChangeLogEntry>>(json) ?? new List<ChangeLogEntry>();
                }
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to read changelog");
            }
            return new List<ChangeLogEntry>();
        }

        /// <summary>
        /// Get recent entries (latest first).
        /// </summary>
        public List<ChangeLogEntry> GetRecent(int count = 50)
        {
            return LoadAll().OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        /// <summary>
        /// Get entries filtered by user.
        /// </summary>
        public List<ChangeLogEntry> GetByUser(string userName)
        {
            return LoadAll()
                .Where(e => e.User.Equals(userName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Get entries filtered by date range.
        /// </summary>
        public List<ChangeLogEntry> GetByDateRange(DateTime from, DateTime to)
        {
            return LoadAll()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Format the changelog for display in the chat panel.
        /// </summary>
        public string FormatForChat(int maxEntries = 20)
        {
            var entries = GetRecent(maxEntries);
            if (entries.Count == 0) return "No changes recorded yet.";

            var sb = new StringBuilder();
            sb.AppendLine("Recent Changes:");
            sb.AppendLine("─────────────────────────────────────────");

            foreach (var entry in entries)
            {
                sb.AppendLine($"  {entry.Timestamp:yyyy-MM-dd HH:mm} | {entry.User} | {entry.Summary}");
                if (!string.IsNullOrEmpty(entry.Comment))
                    sb.AppendLine($"    Comment: {entry.Comment}");
            }

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine($"Total: {entries.Count} entries shown");

            return sb.ToString();
        }

        /// <summary>
        /// Export changelog to CSV file.
        /// </summary>
        public string ExportToCsv(string outputPath = null)
        {
            var entries = LoadAll();
            if (entries.Count == 0) return null;

            outputPath ??= Path.Combine(_serverPath, $"{_projectName}_changelog.csv");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,User,Comment,Summary,Added,Modified,Deleted");

                foreach (var entry in entries.OrderBy(e => e.Timestamp))
                {
                    var added = entry.Added != null ? string.Join("; ", entry.Added) : "";
                    var modified = entry.Modified != null ? string.Join("; ", entry.Modified) : "";
                    var deleted = entry.Deleted != null ? string.Join("; ", entry.Deleted) : "";

                    sb.AppendLine(
                        $"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                        $"\"{EscapeCsv(entry.User)}\"," +
                        $"\"{EscapeCsv(entry.Comment)}\"," +
                        $"\"{EscapeCsv(entry.Summary)}\"," +
                        $"\"{EscapeCsv(added)}\"," +
                        $"\"{EscapeCsv(modified)}\"," +
                        $"\"{EscapeCsv(deleted)}\"");
                }

                File.WriteAllText(outputPath, sb.ToString());
                Logger.Info($"Changelog exported to: {outputPath}");
                return outputPath;
            }
            catch (IOException ex)
            {
                Logger.Error(ex, "Failed to export changelog");
                return null;
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
