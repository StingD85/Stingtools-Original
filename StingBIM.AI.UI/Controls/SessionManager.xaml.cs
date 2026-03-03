// StingBIM.AI.UI.Controls.SessionManager
// Session management for saving and restoring conversations
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Manages conversation sessions - save, load, rename, delete.
    /// </summary>
    public partial class SessionManager : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly string _sessionsPath;
        private ObservableCollection<SessionInfo> _sessions;
        private SessionInfo _currentSession;

        /// <summary>
        /// Event fired when a session is selected to load.
        /// </summary>
        public event EventHandler<SessionInfo> SessionLoadRequested;

        /// <summary>
        /// Event fired when a new session is requested.
        /// </summary>
        public event EventHandler NewSessionRequested;

        /// <summary>
        /// Event fired when the panel should close.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// Event fired when the current session should be saved.
        /// </summary>
        public event EventHandler<SessionInfo> SaveSessionRequested;

        public SessionManager()
        {
            InitializeComponent();

            _sessionsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "Sessions");

            _sessions = new ObservableCollection<SessionInfo>();
            SessionsList.ItemsSource = _sessions;

            EnsureSessionsDirectory();
            LoadSessions();
        }

        #region Public Methods

        /// <summary>
        /// Sets the current active session info.
        /// </summary>
        public void SetCurrentSession(SessionInfo session)
        {
            _currentSession = session;
            UpdateCurrentSessionDisplay();
        }

        /// <summary>
        /// Updates the current session's message count.
        /// </summary>
        public void UpdateCurrentSessionMessageCount(int count)
        {
            if (_currentSession != null)
            {
                _currentSession.MessageCount = count;
                CurrentSessionMessages.Text = $"{count} message{(count == 1 ? "" : "s")}";
            }
        }

        /// <summary>
        /// Saves a session to disk.
        /// </summary>
        public void SaveSession(SessionInfo session, List<ChatMessage> messages)
        {
            try
            {
                var sessionData = new SessionData
                {
                    Info = session,
                    Messages = messages
                };

                var filePath = GetSessionFilePath(session.Id);
                var json = JsonConvert.SerializeObject(sessionData, Formatting.Indented);
                File.WriteAllText(filePath, json);

                // Update sessions list
                var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
                if (existing != null)
                {
                    var index = _sessions.IndexOf(existing);
                    _sessions[index] = session;
                }
                else
                {
                    _sessions.Insert(0, session);
                }

                UpdateEmptyState();
                Logger.Info($"Session saved: {session.Name} ({session.Id})");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to save session: {session.Name}");
            }
        }

        /// <summary>
        /// Loads a session from disk.
        /// </summary>
        public SessionData LoadSession(string sessionId)
        {
            try
            {
                var filePath = GetSessionFilePath(sessionId);
                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Session file not found: {sessionId}");
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var sessionData = JsonConvert.DeserializeObject<SessionData>(json);

                // Update last accessed time
                sessionData.Info.LastModified = DateTime.Now;
                SaveSessionInfo(sessionData.Info);

                Logger.Info($"Session loaded: {sessionData.Info.Name}");
                return sessionData;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load session: {sessionId}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a session from disk.
        /// </summary>
        public void DeleteSession(string sessionId)
        {
            try
            {
                var filePath = GetSessionFilePath(sessionId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    _sessions.Remove(session);
                }

                UpdateEmptyState();
                Logger.Info($"Session deleted: {sessionId}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to delete session: {sessionId}");
            }
        }

        /// <summary>
        /// Renames a session.
        /// </summary>
        public void RenameSession(string sessionId, string newName)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.Name = newName;
                SaveSessionInfo(session);

                // Refresh display
                var index = _sessions.IndexOf(session);
                _sessions[index] = session;

                if (_currentSession?.Id == sessionId)
                {
                    _currentSession.Name = newName;
                    CurrentSessionName.Text = newName;
                }

                Logger.Info($"Session renamed: {sessionId} -> {newName}");
            }
        }

        /// <summary>
        /// Refreshes the sessions list.
        /// </summary>
        public void Refresh()
        {
            LoadSessions();
        }

        #endregion

        #region Private Methods

        private void EnsureSessionsDirectory()
        {
            if (!Directory.Exists(_sessionsPath))
            {
                Directory.CreateDirectory(_sessionsPath);
            }
        }

        private void LoadSessions()
        {
            _sessions.Clear();

            try
            {
                var files = Directory.GetFiles(_sessionsPath, "*.json");
                foreach (var file in files.OrderByDescending(f => File.GetLastWriteTime(f)))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var sessionData = JsonConvert.DeserializeObject<SessionData>(json);
                        if (sessionData?.Info != null)
                        {
                            _sessions.Add(sessionData.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to load session file: {file} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load sessions");
            }

            UpdateEmptyState();
            Logger.Debug($"Loaded {_sessions.Count} sessions");
        }

        private void SaveSessionInfo(SessionInfo info)
        {
            try
            {
                var filePath = GetSessionFilePath(info.Id);
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var sessionData = JsonConvert.DeserializeObject<SessionData>(json);
                    sessionData.Info = info;
                    json = JsonConvert.SerializeObject(sessionData, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save session info: {ex.Message}");
            }
        }

        private string GetSessionFilePath(string sessionId)
        {
            return Path.Combine(_sessionsPath, $"{sessionId}.json");
        }

        private void UpdateCurrentSessionDisplay()
        {
            if (_currentSession != null)
            {
                CurrentSessionCard.Visibility = Visibility.Visible;
                CurrentSessionName.Text = _currentSession.Name;
                CurrentSessionMessages.Text = $"{_currentSession.MessageCount} message{(_currentSession.MessageCount == 1 ? "" : "s")}";
                CurrentSessionTime.Text = GetRelativeTime(_currentSession.CreatedAt);
            }
            else
            {
                CurrentSessionCard.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = _sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FilterSessions(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                LoadSessions();
                return;
            }

            searchText = searchText.ToLowerInvariant();
            var filtered = _sessions.Where(s =>
                s.Name.ToLowerInvariant().Contains(searchText) ||
                (s.ProjectName?.ToLowerInvariant().Contains(searchText) ?? false)
            ).ToList();

            _sessions.Clear();
            foreach (var session in filtered)
            {
                _sessions.Add(session);
            }

            UpdateEmptyState();
        }

        private static string GetRelativeTime(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;

            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)}w ago";

            return dateTime.ToString("MMM d, yyyy");
        }

        #endregion

        #region Event Handlers

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterSessions(SearchTextBox.Text);
        }

        private void NewSessionButton_Click(object sender, RoutedEventArgs e)
        {
            NewSessionRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SaveCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession != null)
            {
                SaveSessionRequested?.Invoke(this, _currentSession);
            }
        }

        private void RenameCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession != null)
            {
                // Simple rename dialog - in production use a proper dialog
                var newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new session name:",
                    "Rename Session",
                    _currentSession.Name);

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    RenameSession(_currentSession.Id, newName);
                }
            }
        }

        private void SessionCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string sessionId)
            {
                var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    SessionLoadRequested?.Invoke(this, session);
                }
            }
        }

        private void LoadSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string sessionId)
            {
                var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    SessionLoadRequested?.Invoke(this, session);
                }
            }
        }

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string sessionId)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete this session?",
                    "Delete Session",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DeleteSession(sessionId);
                }
            }
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Information about a saved session.
    /// </summary>
    public class SessionInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Untitled Session";
        public string ProjectName { get; set; }
        public string ProjectPath { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        public string LastModifiedRelative => GetRelativeTime(LastModified);

        public Visibility ProjectVisibility =>
            string.IsNullOrEmpty(ProjectName) ? Visibility.Collapsed : Visibility.Visible;

        private static string GetRelativeTime(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;

            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";

            return dateTime.ToString("MMM d");
        }
    }

    /// <summary>
    /// Complete session data including messages.
    /// </summary>
    public class SessionData
    {
        public SessionInfo Info { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    /// <summary>
    /// A chat message in a session.
    /// </summary>
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Content { get; set; }
        public bool IsUser { get; set; }
        public bool IsError { get; set; }
        public bool IsSystem { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ErrorCode { get; set; }
        public string ErrorDetails { get; set; }
    }

    #endregion
}
