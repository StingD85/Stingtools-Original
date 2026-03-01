// =============================================================================
// StingBIM.AI.Collaboration - Team Chat Service
// Integrated team chat with BIM context awareness and worksharing features
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Commands;
using StingBIM.AI.Collaboration.Communication;
using StingBIM.AI.Collaboration.Discovery;
using StingBIM.AI.Collaboration.Models;
using StingBIM.AI.Collaboration.Worksharing;

namespace StingBIM.AI.Collaboration.Services
{
    /// <summary>
    /// Main service coordinating team chat, worksharing, and collaboration features.
    /// Acts as the central hub for all real-time team collaboration in StingBIM.
    /// </summary>
    public class TeamChatService : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Core components
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly CollaborationHub _collaborationHub;
        private readonly WorksharingMonitor _worksharingMonitor;
        private readonly CommandProcessor _commandProcessor;

        // State
        private readonly TeamMember _localUser;
        private readonly string _projectName;
        private readonly string _projectGuid;
        private readonly ConcurrentDictionary<string, ChatSession> _chatSessions = new();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // Configuration
        private string _serverUrl = "http://localhost:5150/collaboration";
        private bool _useP2PMode = true; // Default to P2P for LAN

        // Events
        public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
        public event EventHandler<TeamUpdateEventArgs>? TeamUpdated;
        public event EventHandler<WorksharingAlertEventArgs>? WorksharingAlert;
        public event EventHandler<AIResponseEventArgs>? AIResponseGenerated;

        public bool IsRunning => _isRunning;
        public TeamMember LocalUser => _localUser;
        public IReadOnlyCollection<TeamMember> TeamMembers => _collaborationHub.TeamMembers;
        public IReadOnlyCollection<DiscoveredPeer> DiscoveredPeers => _discoveryService.Peers;

        public TeamChatService(string username, string displayName, string projectName, string projectGuid)
        {
            _localUser = new TeamMember
            {
                Username = username,
                DisplayName = displayName,
                CurrentProject = projectName
            };

            _projectName = projectName;
            _projectGuid = projectGuid;

            // Initialize components
            _discoveryService = new NetworkDiscoveryService();
            _collaborationHub = new CollaborationHub(_serverUrl, _localUser);
            _worksharingMonitor = new WorksharingMonitor(_collaborationHub);
            _commandProcessor = new CommandProcessor(_collaborationHub, _worksharingMonitor);

            WireUpEvents();

            Logger.Info($"TeamChatService initialized for {displayName} on project {projectName}");
        }

        #region Initialization

        private void WireUpEvents()
        {
            // Discovery events
            _discoveryService.PeerDiscovered += OnPeerDiscovered;
            _discoveryService.PeerLost += OnPeerLost;

            // Hub events
            _collaborationHub.MessageReceived += OnMessageReceived;
            _collaborationHub.UserJoined += OnUserJoined;
            _collaborationHub.UserLeft += OnUserLeft;
            _collaborationHub.ConflictAlertReceived += OnConflictAlert;
            _collaborationHub.ElementActivityReceived += OnElementActivity;

            // Worksharing events
            _worksharingMonitor.ConflictPredicted += OnConflictPredicted;
            _worksharingMonitor.SyncRecommended += OnSyncRecommended;
            _worksharingMonitor.HotspotDetected += OnHotspotDetected;

            // Command events
            _commandProcessor.CommandExecuted += OnCommandExecuted;
            _commandProcessor.AIQueryRequested += OnAIQueryRequested;
        }

        /// <summary>
        /// Start the team chat service
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                Logger.Warn("TeamChatService is already running");
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                Logger.Info("Starting TeamChatService...");

                // Start network discovery (always runs for P2P)
                await _discoveryService.StartAsync(_localUser.Username, _projectName, _projectGuid);

                // Try to connect to central server if not in P2P-only mode
                if (!_useP2PMode)
                {
                    try
                    {
                        await _collaborationHub.ConnectAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to connect to central server, falling back to P2P mode");
                        _useP2PMode = true;
                    }
                }

                // Start worksharing monitor
                await _worksharingMonitor.StartMonitoringAsync(_projectName, _projectGuid);

                _isRunning = true;
                Logger.Info("TeamChatService started successfully");

                // Send system notification
                BroadcastSystemMessage($"{_localUser.DisplayName} joined the project");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start TeamChatService");
                throw;
            }
        }

        /// <summary>
        /// Stop the team chat service
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            Logger.Info("Stopping TeamChatService...");

            BroadcastSystemMessage($"{_localUser.DisplayName} left the project");

            _cts?.Cancel();

            await _worksharingMonitor.StopMonitoringAsync();
            await _discoveryService.StopAsync();
            await _collaborationHub.DisconnectAsync();

            _isRunning = false;
            Logger.Info("TeamChatService stopped");
        }

        #endregion

        #region Chat Operations

        /// <summary>
        /// Send a chat message to all team members
        /// </summary>
        public async Task SendMessageAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            // Check if it's a command
            if (content.StartsWith("/"))
            {
                await _commandProcessor.ProcessAsync(content, _localUser.UserId);
                return;
            }

            // Check for @mentions
            var mentions = ExtractMentions(content);

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = content,
                Type = MessageType.Text,
                Mentions = mentions
            };

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.SendMessageAsync(content);
            }

            // Store in local session
            AddToSession("main", message);

            Logger.Debug($"Sent message: {content}");
        }

        /// <summary>
        /// Send a direct message to a specific user
        /// </summary>
        public async Task SendDirectMessageAsync(string targetUserId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.SendDirectMessageAsync(targetUserId, content);
            }

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = content,
                TargetUserId = targetUserId,
                Type = MessageType.Text
            };

            AddToSession($"dm_{targetUserId}", message);
        }

        /// <summary>
        /// Share an element in the chat
        /// </summary>
        public async Task ShareElementAsync(string elementId, string elementName, string? comment = null)
        {
            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.ShareElementAsync(elementId, elementName, comment);
            }

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = comment ?? $"Shared element: {elementName}",
                ElementId = elementId,
                Type = MessageType.ElementShare
            };

            AddToSession("main", message);
            ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(message, false));
        }

        /// <summary>
        /// Share current view in chat
        /// </summary>
        public async Task ShareViewAsync(string viewId, string viewName, string? comment = null)
        {
            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = comment ?? $"Shared view: {viewName}",
                ViewId = viewId,
                Type = MessageType.ViewShare
            };

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.SendMessageAsync(message.Content);
            }

            AddToSession("main", message);
            ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(message, false));
        }

        /// <summary>
        /// Get chat history
        /// </summary>
        public IEnumerable<ChatMessage> GetChatHistory(string sessionId = "main", int count = 50)
        {
            if (_chatSessions.TryGetValue(sessionId, out var session))
            {
                return session.Messages.TakeLast(count);
            }

            // Also get from hub history
            return _collaborationHub.GetMessageHistory(count);
        }

        private void AddToSession(string sessionId, ChatMessage message)
        {
            var session = _chatSessions.GetOrAdd(sessionId, _ => new ChatSession { SessionId = sessionId });
            session.Messages.Add(message);
            session.LastActivity = DateTime.UtcNow;

            // Limit session size
            while (session.Messages.Count > 1000)
            {
                session.Messages.RemoveAt(0);
            }
        }

        private List<string> ExtractMentions(string content)
        {
            var mentions = new List<string>();
            var words = content.Split(' ');

            foreach (var word in words)
            {
                if (word.StartsWith("@") && word.Length > 1)
                {
                    var username = word.Substring(1).TrimEnd(',', '.', '!', '?');
                    mentions.Add(username);
                }
            }

            return mentions;
        }

        #endregion

        #region Worksharing Operations

        /// <summary>
        /// Report element activity (editing, viewing)
        /// </summary>
        public async Task ReportElementActivityAsync(string elementId, string elementName, string category, ActivityType activityType)
        {
            var activity = new ElementActivity
            {
                ElementId = elementId,
                ElementName = elementName,
                Category = category,
                UserId = _localUser.UserId,
                Username = _localUser.DisplayName,
                ActivityType = activityType
            };

            _worksharingMonitor.RecordElementActivity(activity.ElementId, activity.ElementName, activity.Category, activity.ActivityType, activity.Changes);

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.BroadcastElementActivityAsync(activity);
            }
        }

        /// <summary>
        /// Update list of elements currently being edited
        /// </summary>
        public async Task UpdateActiveElementsAsync(List<string> elementIds)
        {
            _localUser.ActiveElements = elementIds;

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.UpdateActiveElementsAsync(elementIds);
            }
        }

        /// <summary>
        /// Request a workset from its current owner
        /// </summary>
        public async Task RequestWorksetAsync(string worksetName, string? message = null)
        {
            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.RequestWorksetAsync(worksetName, message);
            }

            BroadcastSystemMessage($"{_localUser.DisplayName} is requesting workset: {worksetName}");
        }

        /// <summary>
        /// Get current sync status
        /// </summary>
        public SyncStatus GetSyncStatus()
        {
            return _worksharingMonitor.GetSyncStatus(_localUser.UserId);
        }

        /// <summary>
        /// Get predicted conflicts
        /// </summary>
        public IEnumerable<ConflictPrediction> GetPredictedConflicts()
        {
            return _worksharingMonitor.GetActiveConflictPredictions();
        }

        /// <summary>
        /// Get team activity summary
        /// </summary>
        public TeamActivitySummary GetTeamActivitySummary(TimeSpan? period = null)
        {
            return _worksharingMonitor.GetActivitySummary(period ?? TimeSpan.FromHours(8));
        }

        #endregion

        #region Status Operations

        /// <summary>
        /// Update user status
        /// </summary>
        public async Task UpdateStatusAsync(PresenceStatus status)
        {
            _localUser.Status = status;

            if (_collaborationHub.IsConnected)
            {
                await _collaborationHub.UpdateStatusAsync(status);
            }

            Logger.Debug($"Status updated to {status}");
        }

        /// <summary>
        /// Get users currently editing specific element
        /// </summary>
        public IEnumerable<TeamMember> GetUsersEditingElement(string elementId)
        {
            return _collaborationHub.GetUsersEditingElement(elementId);
        }

        /// <summary>
        /// Check if element is being edited by someone else
        /// </summary>
        public bool IsElementBeingEdited(string elementId, out string? editorName)
        {
            var editors = GetUsersEditingElement(elementId)
                .Where(u => u.UserId != _localUser.UserId)
                .ToList();

            if (editors.Any())
            {
                editorName = editors.First().DisplayName;
                return true;
            }

            editorName = null;
            return false;
        }

        #endregion

        #region Event Handlers

        private void OnPeerDiscovered(object? sender, PeerDiscoveredEventArgs e)
        {
            Logger.Info($"Discovered peer: {e.Peer.Username}@{e.Peer.Hostname}");

            // If same project, notify team
            if (e.Peer.ProjectName == _projectName)
            {
                BroadcastSystemMessage($"Team member online: {e.Peer.Username} on {e.Peer.Hostname}");
            }

            TeamUpdated?.Invoke(this, new TeamUpdateEventArgs(TeamUpdateType.PeerDiscovered, e.Peer.Username));
        }

        private void OnPeerLost(object? sender, PeerLostEventArgs e)
        {
            Logger.Info($"Lost peer: {e.Peer.Username}@{e.Peer.Hostname}");

            if (e.Peer.ProjectName == _projectName)
            {
                BroadcastSystemMessage($"Team member offline: {e.Peer.Username}");
            }

            TeamUpdated?.Invoke(this, new TeamUpdateEventArgs(TeamUpdateType.PeerLost, e.Peer.Username));
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            AddToSession("main", e.Message);
            ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(e.Message, true));
        }

        private void OnUserJoined(object? sender, UserJoinedEventArgs e)
        {
            BroadcastSystemMessage($"{e.User.DisplayName} joined the session");
            TeamUpdated?.Invoke(this, new TeamUpdateEventArgs(TeamUpdateType.UserJoined, e.User.DisplayName));
        }

        private void OnUserLeft(object? sender, UserLeftEventArgs e)
        {
            BroadcastSystemMessage($"{e.User.DisplayName} left the session");
            TeamUpdated?.Invoke(this, new TeamUpdateEventArgs(TeamUpdateType.UserLeft, e.User.DisplayName));
        }

        private void OnConflictAlert(object? sender, ConflictAlertEventArgs e)
        {
            var alert = $"⚠️ CONFLICT DETECTED: {e.Conflict.Description}";
            BroadcastSystemMessage(alert);
            WorksharingAlert?.Invoke(this, new WorksharingAlertEventArgs(WorksharingAlertType.Conflict, e.Conflict.Description));
        }

        private void OnElementActivity(object? sender, ElementActivityEventArgs e)
        {
            // Let worksharing monitor handle it
            _worksharingMonitor.RecordActivity(e.Activity);
        }

        private void OnConflictPredicted(object? sender, ConflictPredictedEventArgs e)
        {
            var severity = e.Conflict.Severity switch
            {
                ConflictSeverity.Critical => "🔴",
                ConflictSeverity.High => "🟠",
                ConflictSeverity.Medium => "🟡",
                _ => "🟢"
            };

            var alert = $"{severity} Potential conflict predicted: {e.Conflict.Description}";
            BroadcastSystemMessage(alert);

            if (e.Conflict.AIResolutionSuggestion != null)
            {
                BroadcastSystemMessage($"💡 AI Suggestion: {e.Conflict.AIResolutionSuggestion}");
            }

            WorksharingAlert?.Invoke(this, new WorksharingAlertEventArgs(WorksharingAlertType.PredictedConflict, e.Conflict.Description));
        }

        private void OnSyncRecommended(object? sender, SyncRecommendedEventArgs e)
        {
            BroadcastSystemMessage($"📥 Sync recommended: {e.Recommendation}");
            WorksharingAlert?.Invoke(this, new WorksharingAlertEventArgs(WorksharingAlertType.SyncRecommendation, e.Recommendation));
        }

        private void OnHotspotDetected(object? sender, ActivityHotspotDetectedEventArgs e)
        {
            BroadcastSystemMessage($"🔥 High activity area: {e.Hotspot.AreaName} ({e.Hotspot.ActiveUsers.Count} users)");
            WorksharingAlert?.Invoke(this, new WorksharingAlertEventArgs(WorksharingAlertType.Hotspot, e.Hotspot.AreaName));
        }

        private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
        {
            foreach (var response in e.Result.ResponseMessages)
            {
                AddToSession("main", response);
                ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(response, false));
            }
        }

        private void OnAIQueryRequested(object? sender, AIQueryEventArgs e)
        {
            // Trigger AI response generation
            AIResponseGenerated?.Invoke(this, new AIResponseEventArgs(e.Query, "Processing..."));

            // In a full implementation, this would call the AI engine
            Logger.Info($"AI query requested: {e.Query}");
        }

        private void BroadcastSystemMessage(string content)
        {
            var message = new ChatMessage
            {
                SenderId = "SYSTEM",
                SenderName = "StingBIM",
                Content = content,
                Type = MessageType.SystemNotification,
                IsSystemMessage = true
            };

            AddToSession("main", message);
            ChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(message, false));
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Configure server URL for central mode
        /// </summary>
        public void ConfigureServer(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        /// <summary>
        /// Enable or disable P2P mode
        /// </summary>
        public void SetP2PMode(bool enabled)
        {
            _useP2PMode = enabled;
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await _collaborationHub.DisposeAsync();
            _discoveryService.Dispose();
            _cts?.Dispose();
        }

        #endregion
    }

    #region Helper Classes

    internal class ChatSession
    {
        public string SessionId { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Event Args

    public class ChatMessageEventArgs : EventArgs
    {
        public ChatMessage Message { get; }
        public bool IsRemote { get; }
        public ChatMessageEventArgs(ChatMessage message, bool isRemote)
        {
            Message = message;
            IsRemote = isRemote;
        }
    }

    public class TeamUpdateEventArgs : EventArgs
    {
        public TeamUpdateType UpdateType { get; }
        public string Username { get; }
        public TeamUpdateEventArgs(TeamUpdateType type, string username)
        {
            UpdateType = type;
            Username = username;
        }
    }

    public enum TeamUpdateType
    {
        PeerDiscovered,
        PeerLost,
        UserJoined,
        UserLeft,
        StatusChanged
    }

    public class WorksharingAlertEventArgs : EventArgs
    {
        public WorksharingAlertType AlertType { get; }
        public string Description { get; }
        public WorksharingAlertEventArgs(WorksharingAlertType type, string description)
        {
            AlertType = type;
            Description = description;
        }
    }

    public enum WorksharingAlertType
    {
        Conflict,
        PredictedConflict,
        SyncRecommendation,
        Hotspot,
        WorksetRequest
    }

    public class AIResponseEventArgs : EventArgs
    {
        public string Query { get; }
        public string Response { get; }
        public AIResponseEventArgs(string query, string response)
        {
            Query = query;
            Response = response;
        }
    }

    #endregion
}
