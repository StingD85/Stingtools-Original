// =============================================================================
// StingBIM.AI.Collaboration - Live Collaboration Service
// Real-time collaboration features: view sharing, live cursors, annotations,
// voice chat integration, and screen sharing
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Communication;
using StingBIM.AI.Collaboration.Models;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Collaboration.Live
{
    /// <summary>
    /// Provides real-time live collaboration features for synchronized team work.
    /// </summary>
    public class LiveCollaborationService : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Core components
        private readonly CollaborationHub _hub;
        private readonly string _userId;
        private readonly string _username;

        // State
        private readonly ConcurrentDictionary<string, LiveCursor> _cursors = new();
        private readonly ConcurrentDictionary<string, SharedView> _sharedViews = new();
        private readonly ConcurrentDictionary<string, LiveAnnotation> _annotations = new();
        private readonly ConcurrentDictionary<string, FollowSession> _followSessions = new();
        private readonly ConcurrentDictionary<string, VoiceParticipant> _voiceParticipants = new();

        private Timer? _cursorBroadcastTimer;
        private CancellationTokenSource? _cts;
        private bool _isActive;

        // Current state
        private LiveCursor? _myCursor;
        private string? _currentViewId;
        private string? _followingUserId;

        // Configuration
        private readonly int _cursorUpdateRate = 100; // ms
        private readonly int _viewUpdateRate = 500; // ms

        // Events
        public event EventHandler<CursorUpdatedEventArgs>? CursorUpdated;
        public event EventHandler<ViewSharedEventArgs>? ViewShared;
        public event EventHandler<AnnotationAddedEventArgs>? AnnotationAdded;
        public event EventHandler<AnnotationRemovedEventArgs>? AnnotationRemoved;
        public event EventHandler<FollowRequestedEventArgs>? FollowRequested;
        public event EventHandler<VoiceChatEventArgs>? VoiceChatUpdated;
        public event EventHandler<ScreenShareEventArgs>? ScreenShareUpdated;

        public bool IsActive => _isActive;
        public IReadOnlyCollection<LiveCursor> ActiveCursors => _cursors.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<SharedView> SharedViews => _sharedViews.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<LiveAnnotation> Annotations => _annotations.Values.ToList().AsReadOnly();

        public LiveCollaborationService(CollaborationHub hub, string userId, string username)
        {
            _hub = hub;
            _userId = userId;
            _username = username;

            WireUpEvents();
        }

        #region Initialization

        private void WireUpEvents()
        {
            _hub.MessageReceived += OnHubMessage;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isActive) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start cursor broadcasting
            _cursorBroadcastTimer = new Timer(
                BroadcastCursorPosition,
                null,
                _cursorUpdateRate,
                _cursorUpdateRate);

            _isActive = true;
            Logger.Info("LiveCollaborationService started");
        }

        public async Task StopAsync()
        {
            if (!_isActive) return;

            _cts?.Cancel();
            _cursorBroadcastTimer?.Dispose();

            // Notify others we're leaving
            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.CursorRemoved,
                SenderId = _userId
            });

            _isActive = false;
            Logger.Info("LiveCollaborationService stopped");
        }

        #endregion

        #region Live Cursors

        /// <summary>
        /// Update local cursor position (call frequently as cursor moves)
        /// </summary>
        public void UpdateCursorPosition(double x, double y, double z, string? viewId = null)
        {
            _myCursor = new LiveCursor
            {
                UserId = _userId,
                Username = _username,
                X = x,
                Y = y,
                Z = z,
                ViewId = viewId ?? _currentViewId,
                Color = GetUserColor(_userId),
                LastUpdate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Update cursor with selected element context
        /// </summary>
        public void UpdateCursorWithContext(
            double x, double y, double z,
            string? elementId = null,
            string? elementName = null,
            string? viewId = null)
        {
            _myCursor = new LiveCursor
            {
                UserId = _userId,
                Username = _username,
                X = x,
                Y = y,
                Z = z,
                ViewId = viewId ?? _currentViewId,
                SelectedElementId = elementId,
                SelectedElementName = elementName,
                Color = GetUserColor(_userId),
                LastUpdate = DateTime.UtcNow
            };
        }

        private async void BroadcastCursorPosition(object? state)
        {
            if (_myCursor == null || !_isActive) return;

            try
            {
                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.CursorUpdate,
                    SenderId = _userId,
                    Payload = _myCursor
                });
            }
            catch (Exception ex)
            {
                Logger.Trace(ex, "Error broadcasting cursor");
            }
        }

        private void HandleCursorUpdate(LiveCursor cursor)
        {
            if (cursor.UserId == _userId) return;

            _cursors[cursor.UserId] = cursor;

            // Clean up stale cursors
            var staleTime = DateTime.UtcNow.AddSeconds(-5);
            foreach (var kvp in _cursors.ToList())
            {
                if (kvp.Value.LastUpdate < staleTime)
                {
                    _cursors.TryRemove(kvp.Key, out _);
                }
            }

            CursorUpdated?.Invoke(this, new CursorUpdatedEventArgs(cursor));
        }

        private void HandleCursorRemoved(string userId)
        {
            _cursors.TryRemove(userId, out _);
            CursorUpdated?.Invoke(this, new CursorUpdatedEventArgs(new LiveCursor
            {
                UserId = userId,
                IsRemoved = true
            }));
        }

        #endregion

        #region View Sharing

        /// <summary>
        /// Share current view with team
        /// </summary>
        public async Task ShareViewAsync(
            string viewId,
            string viewName,
            ViewType viewType,
            CameraPosition camera,
            string? description = null)
        {
            var sharedView = new SharedView
            {
                ShareId = Guid.NewGuid().ToString(),
                ViewId = viewId,
                ViewName = viewName,
                ViewType = viewType,
                SharedBy = _userId,
                SharedByName = _username,
                Camera = camera,
                Description = description,
                SharedAt = DateTime.UtcNow,
                IsLive = true
            };

            _sharedViews[sharedView.ShareId] = sharedView;
            _currentViewId = viewId;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.ViewShared,
                SenderId = _userId,
                Payload = sharedView
            });

            Logger.Info($"Shared view: {viewName}");
        }

        /// <summary>
        /// Update shared view (for live streaming)
        /// </summary>
        public async Task UpdateSharedViewAsync(string shareId, CameraPosition newCamera)
        {
            if (!_sharedViews.TryGetValue(shareId, out var view)) return;

            view.Camera = newCamera;
            view.LastUpdate = DateTime.UtcNow;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.ViewUpdated,
                SenderId = _userId,
                Payload = view
            });
        }

        /// <summary>
        /// Stop sharing a view
        /// </summary>
        public async Task StopSharingViewAsync(string shareId)
        {
            if (_sharedViews.TryRemove(shareId, out var view))
            {
                view.IsLive = false;

                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.ViewShareStopped,
                    SenderId = _userId,
                    Payload = new { ShareId = shareId }
                });

                Logger.Info($"Stopped sharing view: {view.ViewName}");
            }
        }

        /// <summary>
        /// Navigate to a shared view
        /// </summary>
        public async Task<SharedView?> GoToSharedViewAsync(string shareId)
        {
            if (_sharedViews.TryGetValue(shareId, out var view))
            {
                return view;
            }

            // Request view from sharer
            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.ViewRequest,
                SenderId = _userId,
                Payload = new { ShareId = shareId }
            });

            return null;
        }

        private void HandleViewShared(SharedView view)
        {
            _sharedViews[view.ShareId] = view;
            ViewShared?.Invoke(this, new ViewSharedEventArgs(view, true));
        }

        private void HandleViewUpdated(SharedView view)
        {
            _sharedViews[view.ShareId] = view;
            ViewShared?.Invoke(this, new ViewSharedEventArgs(view, false));
        }

        #endregion

        #region Follow Mode

        /// <summary>
        /// Start following another user's view
        /// </summary>
        public async Task StartFollowingAsync(string targetUserId)
        {
            if (targetUserId == _userId) return;

            _followingUserId = targetUserId;

            var session = new FollowSession
            {
                SessionId = Guid.NewGuid().ToString(),
                FollowerId = _userId,
                FollowerName = _username,
                LeaderId = targetUserId,
                StartedAt = DateTime.UtcNow
            };

            _followSessions[session.SessionId] = session;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.FollowStarted,
                SenderId = _userId,
                Payload = session
            });

            Logger.Info($"Started following {targetUserId}");
        }

        /// <summary>
        /// Stop following
        /// </summary>
        public async Task StopFollowingAsync()
        {
            if (string.IsNullOrEmpty(_followingUserId)) return;

            var session = _followSessions.Values
                .FirstOrDefault(s => s.FollowerId == _userId && s.LeaderId == _followingUserId);

            if (session != null)
            {
                _followSessions.TryRemove(session.SessionId, out _);

                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.FollowStopped,
                    SenderId = _userId,
                    Payload = new { SessionId = session.SessionId }
                });
            }

            _followingUserId = null;
        }

        /// <summary>
        /// Get users currently following you
        /// </summary>
        public IEnumerable<FollowSession> GetFollowers()
        {
            return _followSessions.Values.Where(s => s.LeaderId == _userId);
        }

        /// <summary>
        /// Request someone to follow you (useful for presentations)
        /// </summary>
        public async Task RequestFollowAsync(string targetUserId, string? message = null)
        {
            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.FollowRequest,
                SenderId = _userId,
                TargetId = targetUserId,
                Payload = new FollowRequest
                {
                    RequesterId = _userId,
                    RequesterName = _username,
                    Message = message ?? $"{_username} wants you to follow their view"
                }
            });

            FollowRequested?.Invoke(this, new FollowRequestedEventArgs(
                new FollowRequest
                {
                    RequesterId = _userId,
                    RequesterName = _username,
                    Message = message ?? ""
                },
                false));
        }

        private void HandleFollowRequest(FollowRequest request)
        {
            FollowRequested?.Invoke(this, new FollowRequestedEventArgs(request, true));
        }

        #endregion

        #region Annotations

        /// <summary>
        /// Add a temporary annotation visible to team
        /// </summary>
        public async Task AddAnnotationAsync(
            string elementId,
            string comment,
            AnnotationType type = AnnotationType.Comment,
            Point3D? position = null)
        {
            var annotation = new LiveAnnotation
            {
                AnnotationId = Guid.NewGuid().ToString(),
                ElementId = elementId,
                CreatedBy = _userId,
                CreatedByName = _username,
                Comment = comment,
                Type = type,
                Position = position,
                Color = GetUserColor(_userId),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(8) // Annotations expire after 8 hours
            };

            _annotations[annotation.AnnotationId] = annotation;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.AnnotationAdded,
                SenderId = _userId,
                Payload = annotation
            });

            AnnotationAdded?.Invoke(this, new AnnotationAddedEventArgs(annotation));
            Logger.Info($"Added annotation: {comment}");
        }

        /// <summary>
        /// Add a markup annotation (arrow, circle, highlight)
        /// </summary>
        public async Task AddMarkupAsync(
            MarkupType markupType,
            Point3D startPoint,
            Point3D? endPoint = null,
            string? label = null)
        {
            var annotation = new LiveAnnotation
            {
                AnnotationId = Guid.NewGuid().ToString(),
                CreatedBy = _userId,
                CreatedByName = _username,
                Type = AnnotationType.Markup,
                MarkupType = markupType,
                Position = startPoint,
                EndPosition = endPoint,
                Comment = label ?? string.Empty,
                Color = GetUserColor(_userId),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(4)
            };

            _annotations[annotation.AnnotationId] = annotation;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.AnnotationAdded,
                SenderId = _userId,
                Payload = annotation
            });

            AnnotationAdded?.Invoke(this, new AnnotationAddedEventArgs(annotation));
        }

        /// <summary>
        /// Remove an annotation
        /// </summary>
        public async Task RemoveAnnotationAsync(string annotationId)
        {
            if (_annotations.TryRemove(annotationId, out var annotation))
            {
                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.AnnotationRemoved,
                    SenderId = _userId,
                    Payload = new { AnnotationId = annotationId }
                });

                AnnotationRemoved?.Invoke(this, new AnnotationRemovedEventArgs(annotationId));
            }
        }

        /// <summary>
        /// Clear all annotations by me
        /// </summary>
        public async Task ClearMyAnnotationsAsync()
        {
            var myAnnotations = _annotations.Values
                .Where(a => a.CreatedBy == _userId)
                .ToList();

            foreach (var annotation in myAnnotations)
            {
                await RemoveAnnotationAsync(annotation.AnnotationId);
            }
        }

        /// <summary>
        /// Get annotations for a specific element
        /// </summary>
        public IEnumerable<LiveAnnotation> GetAnnotationsForElement(string elementId)
        {
            return _annotations.Values
                .Where(a => a.ElementId == elementId && a.ExpiresAt > DateTime.UtcNow);
        }

        private void HandleAnnotationAdded(LiveAnnotation annotation)
        {
            _annotations[annotation.AnnotationId] = annotation;
            AnnotationAdded?.Invoke(this, new AnnotationAddedEventArgs(annotation));
        }

        private void HandleAnnotationRemoved(string annotationId)
        {
            _annotations.TryRemove(annotationId, out _);
            AnnotationRemoved?.Invoke(this, new AnnotationRemovedEventArgs(annotationId));
        }

        #endregion

        #region Voice Chat

        /// <summary>
        /// Join voice chat room
        /// </summary>
        public async Task JoinVoiceChatAsync(string roomId = "default")
        {
            var participant = new VoiceParticipant
            {
                UserId = _userId,
                Username = _username,
                RoomId = roomId,
                JoinedAt = DateTime.UtcNow,
                IsMuted = false,
                IsSpeaking = false
            };

            _voiceParticipants[_userId] = participant;

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.VoiceJoined,
                SenderId = _userId,
                Payload = participant
            });

            VoiceChatUpdated?.Invoke(this, new VoiceChatEventArgs(
                VoiceChatAction.Joined, participant));

            Logger.Info($"Joined voice chat: {roomId}");
        }

        /// <summary>
        /// Leave voice chat
        /// </summary>
        public async Task LeaveVoiceChatAsync()
        {
            if (_voiceParticipants.TryRemove(_userId, out var participant))
            {
                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.VoiceLeft,
                    SenderId = _userId
                });

                VoiceChatUpdated?.Invoke(this, new VoiceChatEventArgs(
                    VoiceChatAction.Left, participant));
            }
        }

        /// <summary>
        /// Toggle mute status
        /// </summary>
        public async Task ToggleMuteAsync()
        {
            if (_voiceParticipants.TryGetValue(_userId, out var participant))
            {
                participant.IsMuted = !participant.IsMuted;

                await BroadcastLiveMessageAsync(new LiveMessage
                {
                    Type = LiveMessageType.VoiceMuteChanged,
                    SenderId = _userId,
                    Payload = new { IsMuted = participant.IsMuted }
                });

                VoiceChatUpdated?.Invoke(this, new VoiceChatEventArgs(
                    participant.IsMuted ? VoiceChatAction.Muted : VoiceChatAction.Unmuted,
                    participant));
            }
        }

        /// <summary>
        /// Get voice chat participants
        /// </summary>
        public IEnumerable<VoiceParticipant> GetVoiceParticipants(string? roomId = null)
        {
            var query = _voiceParticipants.Values.AsEnumerable();
            if (roomId != null)
            {
                query = query.Where(p => p.RoomId == roomId);
            }
            return query;
        }

        private void HandleVoiceUpdate(VoiceParticipant participant, LiveMessageType type)
        {
            switch (type)
            {
                case LiveMessageType.VoiceJoined:
                    _voiceParticipants[participant.UserId] = participant;
                    VoiceChatUpdated?.Invoke(this, new VoiceChatEventArgs(
                        VoiceChatAction.Joined, participant));
                    break;

                case LiveMessageType.VoiceLeft:
                    _voiceParticipants.TryRemove(participant.UserId, out _);
                    VoiceChatUpdated?.Invoke(this, new VoiceChatEventArgs(
                        VoiceChatAction.Left, participant));
                    break;
            }
        }

        #endregion

        #region Screen Sharing

        /// <summary>
        /// Start screen sharing
        /// </summary>
        public async Task StartScreenShareAsync(string? region = null)
        {
            var session = new ScreenShareSession
            {
                SessionId = Guid.NewGuid().ToString(),
                SharerId = _userId,
                SharerName = _username,
                Region = region,
                StartedAt = DateTime.UtcNow,
                IsActive = true
            };

            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.ScreenShareStarted,
                SenderId = _userId,
                Payload = session
            });

            ScreenShareUpdated?.Invoke(this, new ScreenShareEventArgs(
                ScreenShareAction.Started, session));

            Logger.Info("Started screen sharing");
        }

        /// <summary>
        /// Stop screen sharing
        /// </summary>
        public async Task StopScreenShareAsync(string sessionId)
        {
            await BroadcastLiveMessageAsync(new LiveMessage
            {
                Type = LiveMessageType.ScreenShareStopped,
                SenderId = _userId,
                Payload = new { SessionId = sessionId }
            });

            ScreenShareUpdated?.Invoke(this, new ScreenShareEventArgs(
                ScreenShareAction.Stopped,
                new ScreenShareSession { SessionId = sessionId }));

            Logger.Info("Stopped screen sharing");
        }

        #endregion

        #region Message Handling

        private void OnHubMessage(object? sender, MessageReceivedEventArgs e)
        {
            // Handle live collaboration messages
            if (e.Message.Type == MessageType.LiveCollaboration)
            {
                ProcessLiveMessage(e.Message.Content);
            }
        }

        private void ProcessLiveMessage(string json)
        {
            try
            {
                var message = Newtonsoft.Json.JsonConvert.DeserializeObject<LiveMessage>(json);
                if (message == null || message.SenderId == _userId) return;

                switch (message.Type)
                {
                    case LiveMessageType.CursorUpdate:
                        var cursor = Newtonsoft.Json.JsonConvert.DeserializeObject<LiveCursor>(
                            message.Payload?.ToString() ?? "");
                        if (cursor != null) HandleCursorUpdate(cursor);
                        break;

                    case LiveMessageType.CursorRemoved:
                        HandleCursorRemoved(message.SenderId);
                        break;

                    case LiveMessageType.ViewShared:
                        var view = Newtonsoft.Json.JsonConvert.DeserializeObject<SharedView>(
                            message.Payload?.ToString() ?? "");
                        if (view != null) HandleViewShared(view);
                        break;

                    case LiveMessageType.ViewUpdated:
                        var updatedView = Newtonsoft.Json.JsonConvert.DeserializeObject<SharedView>(
                            message.Payload?.ToString() ?? "");
                        if (updatedView != null) HandleViewUpdated(updatedView);
                        break;

                    case LiveMessageType.AnnotationAdded:
                        var annotation = Newtonsoft.Json.JsonConvert.DeserializeObject<LiveAnnotation>(
                            message.Payload?.ToString() ?? "");
                        if (annotation != null) HandleAnnotationAdded(annotation);
                        break;

                    case LiveMessageType.AnnotationRemoved:
                        var removeData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(
                            message.Payload?.ToString() ?? "");
                        HandleAnnotationRemoved(removeData?.AnnotationId?.ToString() ?? "");
                        break;

                    case LiveMessageType.FollowRequest:
                        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FollowRequest>(
                            message.Payload?.ToString() ?? "");
                        if (request != null) HandleFollowRequest(request);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Trace(ex, "Error processing live message");
            }
        }

        private async Task BroadcastLiveMessageAsync(LiveMessage message)
        {
            if (!_hub.IsConnected) return;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            await _hub.SendMessageAsync(json);
        }

        #endregion

        #region Utilities

        private string GetUserColor(string userId)
        {
            // Generate consistent color for user
            var hash = userId.GetHashCode();
            var colors = new[]
            {
                "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
                "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F",
                "#BB8FCE", "#85C1E9", "#F8B500", "#58D68D"
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cts?.Dispose();
        }

        #endregion
    }

    #region Data Models

    public class LiveCursor
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string? ViewId { get; set; }
        public string? SelectedElementId { get; set; }
        public string? SelectedElementName { get; set; }
        public string Color { get; set; } = "#FF6B6B";
        public DateTime LastUpdate { get; set; }
        public bool IsRemoved { get; set; }
    }

    public class SharedView
    {
        public string ShareId { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public ViewType ViewType { get; set; }
        public string SharedBy { get; set; } = string.Empty;
        public string SharedByName { get; set; } = string.Empty;
        public CameraPosition? Camera { get; set; }
        public string? Description { get; set; }
        public DateTime SharedAt { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsLive { get; set; }
    }

    public enum ViewType
    {
        FloorPlan,
        CeilingPlan,
        Section,
        Elevation,
        ThreeD,
        Drafting,
        Sheet,
        Schedule
    }

    public class CameraPosition
    {
        public double EyeX { get; set; }
        public double EyeY { get; set; }
        public double EyeZ { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double TargetZ { get; set; }
        public double UpX { get; set; }
        public double UpY { get; set; }
        public double UpZ { get; set; }
        public double Scale { get; set; } = 1.0;
    }

    public class LiveAnnotation
    {
        public string AnnotationId { get; set; } = string.Empty;
        public string? ElementId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public AnnotationType Type { get; set; }
        public MarkupType? MarkupType { get; set; }
        public Point3D? Position { get; set; }
        public Point3D? EndPosition { get; set; }
        public string Color { get; set; } = "#FF6B6B";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public enum AnnotationType
    {
        Comment,
        Question,
        Issue,
        Markup,
        Dimension
    }

    public enum MarkupType
    {
        Arrow,
        Circle,
        Rectangle,
        Line,
        Cloud,
        Text,
        Highlight
    }

    public class FollowSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string FollowerId { get; set; } = string.Empty;
        public string FollowerName { get; set; } = string.Empty;
        public string LeaderId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
    }

    public class FollowRequest
    {
        public string RequesterId { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class VoiceParticipant
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsMuted { get; set; }
        public bool IsSpeaking { get; set; }
    }

    public class ScreenShareSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string SharerId { get; set; } = string.Empty;
        public string SharerName { get; set; } = string.Empty;
        public string? Region { get; set; }
        public DateTime StartedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class LiveMessage
    {
        public LiveMessageType Type { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public object? Payload { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum LiveMessageType
    {
        CursorUpdate,
        CursorRemoved,
        ViewShared,
        ViewUpdated,
        ViewShareStopped,
        ViewRequest,
        AnnotationAdded,
        AnnotationRemoved,
        FollowStarted,
        FollowStopped,
        FollowRequest,
        VoiceJoined,
        VoiceLeft,
        VoiceMuteChanged,
        ScreenShareStarted,
        ScreenShareStopped
    }

    #endregion

    #region Event Args

    public class CursorUpdatedEventArgs : EventArgs
    {
        public LiveCursor Cursor { get; }
        public CursorUpdatedEventArgs(LiveCursor cursor) => Cursor = cursor;
    }

    public class ViewSharedEventArgs : EventArgs
    {
        public SharedView View { get; }
        public bool IsNew { get; }
        public ViewSharedEventArgs(SharedView view, bool isNew)
        {
            View = view;
            IsNew = isNew;
        }
    }

    public class AnnotationAddedEventArgs : EventArgs
    {
        public LiveAnnotation Annotation { get; }
        public AnnotationAddedEventArgs(LiveAnnotation annotation) => Annotation = annotation;
    }

    public class AnnotationRemovedEventArgs : EventArgs
    {
        public string AnnotationId { get; }
        public AnnotationRemovedEventArgs(string id) => AnnotationId = id;
    }

    public class FollowRequestedEventArgs : EventArgs
    {
        public FollowRequest Request { get; }
        public bool IsIncoming { get; }
        public FollowRequestedEventArgs(FollowRequest request, bool isIncoming)
        {
            Request = request;
            IsIncoming = isIncoming;
        }
    }

    public class VoiceChatEventArgs : EventArgs
    {
        public VoiceChatAction Action { get; }
        public VoiceParticipant Participant { get; }
        public VoiceChatEventArgs(VoiceChatAction action, VoiceParticipant participant)
        {
            Action = action;
            Participant = participant;
        }
    }

    public enum VoiceChatAction
    {
        Joined,
        Left,
        Muted,
        Unmuted,
        Speaking
    }

    public class ScreenShareEventArgs : EventArgs
    {
        public ScreenShareAction Action { get; }
        public ScreenShareSession Session { get; }
        public ScreenShareEventArgs(ScreenShareAction action, ScreenShareSession session)
        {
            Action = action;
            Session = session;
        }
    }

    public enum ScreenShareAction
    {
        Started,
        Stopped,
        Paused,
        Resumed
    }

    #endregion
}
