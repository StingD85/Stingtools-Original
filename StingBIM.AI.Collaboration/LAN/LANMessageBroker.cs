// StingBIM.AI.Collaboration.LAN.LANMessageBroker
// FileSystemWatcher-based notification system for LAN team communication
// v4 Prompt Reference: Section C.2 — Broadcast notifications via JSON files

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Collaboration.LAN
{
    /// <summary>
    /// Monitors the shared LAN drive for team activity using FileSystemWatcher.
    /// Watches _notifications.json and _team.json for real-time updates.
    /// No internet, no cloud — pure LAN file system events.
    /// </summary>
    public class LANMessageBroker : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private FileSystemWatcher _notificationWatcher;
        private FileSystemWatcher _teamWatcher;
        private readonly object _watcherLock = new object();

        private string _serverPath;
        private string _projectName;
        private string _currentUser;
        private bool _isWatching;

        /// <summary>
        /// Raised when a new notification arrives from another team member.
        /// </summary>
        public event EventHandler<LANNotification> NotificationReceived;

        /// <summary>
        /// Raised when a team member's status changes (joins, syncs, goes offline).
        /// </summary>
        public event EventHandler<TeamActivityEventArgs> TeamStatusChanged;

        public LANMessageBroker()
        {
            _currentUser = Environment.UserName;
        }

        /// <summary>
        /// Start watching the shared LAN folder for team activity.
        /// </summary>
        public void StartWatching(string serverPath, string projectName)
        {
            lock (_watcherLock)
            {
                _serverPath = serverPath;
                _projectName = projectName;

                StopWatching();

                try
                {
                    // Watch _notifications.json
                    var notifFile = $"{projectName}_notifications.json";
                    _notificationWatcher = new FileSystemWatcher(serverPath, notifFile)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };
                    _notificationWatcher.Changed += OnNotificationFileChanged;

                    // Watch _team.json
                    var teamFile = $"{projectName}_team.json";
                    _teamWatcher = new FileSystemWatcher(serverPath, teamFile)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };
                    _teamWatcher.Changed += OnTeamFileChanged;

                    _isWatching = true;
                    Logger.Info($"LANMessageBroker watching: {serverPath}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to start FileSystemWatcher");
                }
            }
        }

        /// <summary>
        /// Stop watching the shared LAN folder.
        /// </summary>
        public void StopWatching()
        {
            lock (_watcherLock)
            {
                _isWatching = false;

                if (_notificationWatcher != null)
                {
                    _notificationWatcher.EnableRaisingEvents = false;
                    _notificationWatcher.Changed -= OnNotificationFileChanged;
                    _notificationWatcher.Dispose();
                    _notificationWatcher = null;
                }

                if (_teamWatcher != null)
                {
                    _teamWatcher.EnableRaisingEvents = false;
                    _teamWatcher.Changed -= OnTeamFileChanged;
                    _teamWatcher.Dispose();
                    _teamWatcher = null;
                }
            }
        }

        /// <summary>
        /// Get recent notifications (last N).
        /// </summary>
        public List<LANNotification> GetRecentNotifications(int count = 20)
        {
            try
            {
                var path = Path.Combine(_serverPath, $"{_projectName}_notifications.json");
                if (!File.Exists(path)) return new List<LANNotification>();

                var json = File.ReadAllText(path);
                var all = JsonConvert.DeserializeObject<List<LANNotification>>(json) ?? new List<LANNotification>();

                return all.OrderByDescending(n => n.Timestamp).Take(count).ToList();
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Failed to read notifications");
                return new List<LANNotification>();
            }
        }

        private void OnNotificationFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!_isWatching) return;

            try
            {
                // Small delay to let the file write complete
                System.Threading.Thread.Sleep(200);

                var notifications = GetRecentNotifications(1);
                if (notifications.Count > 0)
                {
                    var latest = notifications[0];
                    // Only fire for notifications from OTHER users
                    if (!latest.User.Equals(_currentUser, StringComparison.OrdinalIgnoreCase))
                    {
                        NotificationReceived?.Invoke(this, latest);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error processing notification change");
            }
        }

        private void OnTeamFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!_isWatching) return;

            try
            {
                System.Threading.Thread.Sleep(200);

                var path = Path.Combine(_serverPath, $"{_projectName}_team.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var team = JsonConvert.DeserializeObject<List<TeamMember>>(json) ?? new List<TeamMember>();

                // Find most recently changed member (not self)
                var latest = team
                    .Where(m => !m.UserName.Equals(_currentUser, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.LastSeen)
                    .FirstOrDefault();

                if (latest != null)
                {
                    TeamStatusChanged?.Invoke(this, new TeamActivityEventArgs
                    {
                        UserName = latest.UserName,
                        Activity = latest.IsOnline ? "came online" : "went offline",
                        Timestamp = latest.LastSeen
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error processing team file change");
            }
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
