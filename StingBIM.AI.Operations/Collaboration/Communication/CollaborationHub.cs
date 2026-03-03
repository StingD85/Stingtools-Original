// =============================================================================
// StingBIM.AI.Collaboration - Collaboration Hub
// Real-time communication hub using SignalR for team collaboration
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Communication
{
    /// <summary>
    /// Real-time collaboration hub for team communication.
    /// Supports chat, presence, and worksharing notifications.
    /// Can work in server mode (with SignalR server) or P2P mode (direct TCP).
    /// </summary>
    public class CollaborationHub : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Connection
        private HubConnection? _hubConnection;
        private readonly string _serverUrl;
        private readonly TeamMember _localUser;
        private CancellationTokenSource? _cts;

        // State
        private readonly ConcurrentDictionary<string, TeamMember> _teamMembers = new();
        private readonly ConcurrentQueue<ChatMessage> _messageHistory = new();
        private const int MaxMessageHistory = 500;

        // Events
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<UserJoinedEventArgs>? UserJoined;
        public event EventHandler<UserLeftEventArgs>? UserLeft;
        public event EventHandler<UserStatusChangedEventArgs>? UserStatusChanged;
        public event EventHandler<ElementActivityEventArgs>? ElementActivityReceived;
        public event EventHandler<ConflictAlertEventArgs>? ConflictAlertReceived;
        public event EventHandler<WorksetRequestEventArgs>? WorksetRequestReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public TeamMember LocalUser => _localUser;
        public IReadOnlyCollection<TeamMember> TeamMembers => _teamMembers.Values.ToList().AsReadOnly();

        public CollaborationHub(string serverUrl, TeamMember localUser)
        {
            _serverUrl = serverUrl;
            _localUser = localUser;
            Logger.Info($"CollaborationHub created for user {localUser.DisplayName}");
        }

        #region Connection Management

        /// <summary>
        /// Connect to the collaboration server
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                Logger.Warn("Already connected to collaboration hub");
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_serverUrl)
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                RegisterHandlers();

                Logger.Info($"Connecting to collaboration hub at {_serverUrl}...");
                await _hubConnection.StartAsync(cancellationToken);

                // Announce presence
                await _hubConnection.InvokeAsync("JoinProject", _localUser, cancellationToken);

                Logger.Info($"Connected to collaboration hub as {_localUser.DisplayName}");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true, "Connected"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to collaboration hub");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, ex.Message));
                throw;
            }
        }

        /// <summary>
        /// Disconnect from the collaboration server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_hubConnection == null) return;

            try
            {
                if (IsConnected)
                {
                    await _hubConnection.InvokeAsync("LeaveProject", _localUser.UserId);
                }

                await _hubConnection.StopAsync();
                Logger.Info("Disconnected from collaboration hub");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during disconnect");
            }
            finally
            {
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, "Disconnected"));
            }
        }

        private void RegisterHandlers()
        {
            if (_hubConnection == null) return;

            // Chat messages
            _hubConnection.On<ChatMessage>("ReceiveMessage", message =>
            {
                AddToHistory(message);
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
            });

            // User presence
            _hubConnection.On<TeamMember>("UserJoined", user =>
            {
                _teamMembers[user.UserId] = user;
                UserJoined?.Invoke(this, new UserJoinedEventArgs(user));
                Logger.Info($"User joined: {user.DisplayName}");
            });

            _hubConnection.On<string>("UserLeft", userId =>
            {
                if (_teamMembers.TryRemove(userId, out var user))
                {
                    UserLeft?.Invoke(this, new UserLeftEventArgs(user));
                    Logger.Info($"User left: {user.DisplayName}");
                }
            });

            _hubConnection.On<string, PresenceStatus>("UserStatusChanged", (userId, status) =>
            {
                if (_teamMembers.TryGetValue(userId, out var user))
                {
                    user.Status = status;
                    UserStatusChanged?.Invoke(this, new UserStatusChangedEventArgs(user, status));
                }
            });

            // Element activity
            _hubConnection.On<ElementActivity>("ElementActivity", activity =>
            {
                ElementActivityReceived?.Invoke(this, new ElementActivityEventArgs(activity));
            });

            // Conflict alerts
            _hubConnection.On<ConflictPrediction>("ConflictAlert", conflict =>
            {
                ConflictAlertReceived?.Invoke(this, new ConflictAlertEventArgs(conflict));
                Logger.Warn($"Conflict alert: {conflict.Description}");
            });

            // Workset requests
            _hubConnection.On<string, string, string>("WorksetRequest", (fromUserId, worksetName, message) =>
            {
                WorksetRequestReceived?.Invoke(this, new WorksetRequestEventArgs(fromUserId, worksetName, message));
            });

            // Team list sync
            _hubConnection.On<List<TeamMember>>("SyncTeamMembers", members =>
            {
                _teamMembers.Clear();
                foreach (var member in members)
                {
                    _teamMembers[member.UserId] = member;
                }
                Logger.Debug($"Synced {members.Count} team members");
            });

            // Reconnection handling
            _hubConnection.Reconnecting += error =>
            {
                Logger.Warn(error, "Connection lost, attempting to reconnect...");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, "Reconnecting..."));
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                Logger.Info($"Reconnected with connection ID: {connectionId}");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true, "Reconnected"));
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                Logger.Info("Connection closed");
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, "Connection closed"));
                return Task.CompletedTask;
            };
        }

        #endregion

        #region Messaging

        /// <summary>
        /// Send a chat message to all team members
        /// </summary>
        public async Task SendMessageAsync(string content)
        {
            if (!IsConnected)
            {
                Logger.Warn("Cannot send message - not connected");
                return;
            }

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = content,
                Type = MessageType.Text
            };

            await _hubConnection!.InvokeAsync("SendMessage", message);
            AddToHistory(message);
        }

        /// <summary>
        /// Send a direct message to a specific user
        /// </summary>
        public async Task SendDirectMessageAsync(string targetUserId, string content)
        {
            if (!IsConnected) return;

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = content,
                TargetUserId = targetUserId,
                Type = MessageType.Text
            };

            await _hubConnection!.InvokeAsync("SendDirectMessage", targetUserId, message);
            AddToHistory(message);
        }

        /// <summary>
        /// Send a command message
        /// </summary>
        public async Task SendCommandAsync(string command)
        {
            if (!IsConnected) return;

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = command,
                Type = MessageType.Command
            };

            await _hubConnection!.InvokeAsync("SendCommand", message);
        }

        /// <summary>
        /// Share an element reference in chat
        /// </summary>
        public async Task ShareElementAsync(string elementId, string elementName, string? comment = null)
        {
            if (!IsConnected) return;

            var message = new ChatMessage
            {
                SenderId = _localUser.UserId,
                SenderName = _localUser.DisplayName,
                Content = comment ?? $"Shared element: {elementName}",
                ElementId = elementId,
                Type = MessageType.ElementShare
            };

            await _hubConnection!.InvokeAsync("SendMessage", message);
            AddToHistory(message);
        }

        #endregion

        #region Presence & Status

        /// <summary>
        /// Update local user's status
        /// </summary>
        public async Task UpdateStatusAsync(PresenceStatus status)
        {
            if (!IsConnected) return;

            _localUser.Status = status;
            await _hubConnection!.InvokeAsync("UpdateStatus", _localUser.UserId, status);
        }

        /// <summary>
        /// Update which elements the user is currently editing
        /// </summary>
        public async Task UpdateActiveElementsAsync(List<string> elementIds)
        {
            if (!IsConnected) return;

            _localUser.ActiveElements = elementIds;
            await _hubConnection!.InvokeAsync("UpdateActiveElements", _localUser.UserId, elementIds);
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        public TeamMember? GetUser(string userId)
        {
            _teamMembers.TryGetValue(userId, out var user);
            return user;
        }

        /// <summary>
        /// Get users currently editing a specific element
        /// </summary>
        public IEnumerable<TeamMember> GetUsersEditingElement(string elementId)
        {
            return _teamMembers.Values.Where(u => u.ActiveElements.Contains(elementId));
        }

        #endregion

        #region Worksharing

        /// <summary>
        /// Broadcast element activity to team
        /// </summary>
        public async Task BroadcastElementActivityAsync(ElementActivity activity)
        {
            if (!IsConnected) return;

            activity.UserId = _localUser.UserId;
            activity.Username = _localUser.DisplayName;

            await _hubConnection!.InvokeAsync("BroadcastElementActivity", activity);
        }

        /// <summary>
        /// Request access to a workset
        /// </summary>
        public async Task RequestWorksetAsync(string worksetName, string? message = null)
        {
            if (!IsConnected) return;

            await _hubConnection!.InvokeAsync("RequestWorkset", _localUser.UserId, worksetName, message ?? "Requesting access");
            Logger.Info($"Requested workset: {worksetName}");
        }

        /// <summary>
        /// Transfer workset ownership to another user
        /// </summary>
        public async Task TransferWorksetAsync(string worksetName, string targetUserId)
        {
            if (!IsConnected) return;

            await _hubConnection!.InvokeAsync("TransferWorkset", worksetName, _localUser.UserId, targetUserId);
            Logger.Info($"Transferred workset {worksetName} to {targetUserId}");
        }

        /// <summary>
        /// Broadcast a conflict alert
        /// </summary>
        public async Task BroadcastConflictAlertAsync(ConflictPrediction conflict)
        {
            if (!IsConnected) return;

            await _hubConnection!.InvokeAsync("BroadcastConflictAlert", conflict);
        }

        #endregion

        #region History

        private void AddToHistory(ChatMessage message)
        {
            _messageHistory.Enqueue(message);

            while (_messageHistory.Count > MaxMessageHistory)
            {
                _messageHistory.TryDequeue(out _);
            }
        }

        public IEnumerable<ChatMessage> GetMessageHistory(int count = 50)
        {
            return _messageHistory.TakeLast(count);
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _cts?.Dispose();
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }

        #endregion
    }

    #region Event Args

    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; }
        public MessageReceivedEventArgs(ChatMessage message) => Message = message;
    }

    public class UserJoinedEventArgs : EventArgs
    {
        public TeamMember User { get; }
        public UserJoinedEventArgs(TeamMember user) => User = user;
    }

    public class UserLeftEventArgs : EventArgs
    {
        public TeamMember User { get; }
        public UserLeftEventArgs(TeamMember user) => User = user;
    }

    public class UserStatusChangedEventArgs : EventArgs
    {
        public TeamMember User { get; }
        public PresenceStatus NewStatus { get; }
        public UserStatusChangedEventArgs(TeamMember user, PresenceStatus status)
        {
            User = user;
            NewStatus = status;
        }
    }

    public class ElementActivityEventArgs : EventArgs
    {
        public ElementActivity Activity { get; }
        public ElementActivityEventArgs(ElementActivity activity) => Activity = activity;
    }

    public class ConflictAlertEventArgs : EventArgs
    {
        public ConflictPrediction Conflict { get; }
        public ConflictAlertEventArgs(ConflictPrediction conflict) => Conflict = conflict;
    }

    public class WorksetRequestEventArgs : EventArgs
    {
        public string FromUserId { get; }
        public string WorksetName { get; }
        public string Message { get; }
        public WorksetRequestEventArgs(string fromUserId, string worksetName, string message)
        {
            FromUserId = fromUserId;
            WorksetName = worksetName;
            Message = message;
        }
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Message { get; }
        public ConnectionStateChangedEventArgs(bool isConnected, string message)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }

    #endregion
}
