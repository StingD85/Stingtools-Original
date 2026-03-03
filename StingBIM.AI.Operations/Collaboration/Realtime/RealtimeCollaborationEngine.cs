// ================================================================================================
// STINGBIM AI COLLABORATION - REAL-TIME COLLABORATION ENGINE
// Live collaboration features including presence, cursors, co-editing, and real-time sync
// ================================================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Realtime
{
    #region Enums

    public enum UserPresenceStatus
    {
        Online,
        Away,
        Busy,
        InMeeting,
        DoNotDisturb,
        Offline
    }

    public enum CollaborationEventType
    {
        UserJoined,
        UserLeft,
        UserStatusChanged,
        CursorMoved,
        SelectionChanged,
        ElementModified,
        ElementCreated,
        ElementDeleted,
        ViewChanged,
        CommentAdded,
        AnnotationAdded,
        MarkupAdded,
        LockAcquired,
        LockReleased,
        Conflict,
        Chat,
        VoiceStarted,
        VoiceStopped,
        ScreenSharing,
        FileUploaded
    }

    public enum LockType
    {
        None,
        Soft,    // Warning but allows override
        Hard,    // Prevents editing
        Exclusive // Only lock holder can edit
    }

    public enum ConflictResolution
    {
        LastWriteWins,
        FirstWriteWins,
        Merge,
        Manual,
        ServerAuthoritative
    }

    #endregion

    #region Data Models

    public class CollaborationSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string? ModelId { get; set; }
        public string? ViewId { get; set; }
        public string HostUserId { get; set; } = string.Empty;
        public List<SessionParticipant> Participants { get; set; } = new();
        public SessionSettings Settings { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public bool IsActive => !EndedAt.HasValue;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class SessionParticipant
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Color { get; set; } = string.Empty;
        public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Online;
        public ParticipantRole Role { get; set; }
        public UserCursor? Cursor { get; set; }
        public UserSelection? Selection { get; set; }
        public UserView? CurrentView { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public bool IsSpeaking { get; set; }
        public bool IsScreenSharing { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new();
    }

    public enum ParticipantRole
    {
        Host,
        Editor,
        Commenter,
        Viewer
    }

    public class SessionSettings
    {
        public bool AllowEditing { get; set; } = true;
        public bool AllowComments { get; set; } = true;
        public bool AllowMarkups { get; set; } = true;
        public bool RequireLock { get; set; } = false;
        public LockType DefaultLockType { get; set; } = LockType.Soft;
        public ConflictResolution ConflictStrategy { get; set; } = ConflictResolution.LastWriteWins;
        public bool ShowCursors { get; set; } = true;
        public bool ShowSelections { get; set; } = true;
        public bool EnableVoice { get; set; } = true;
        public bool EnableScreenShare { get; set; } = true;
        public int MaxParticipants { get; set; } = 50;
        public int InactivityTimeoutMinutes { get; set; } = 30;
    }

    public class UserCursor
    {
        public string UserId { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string ViewId { get; set; } = string.Empty;
        public CursorMode Mode { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum CursorMode
    {
        Select,
        Pan,
        Zoom,
        Measure,
        Annotate,
        Markup,
        Section,
        Walk
    }

    public class UserSelection
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> SelectedElementIds { get; set; } = new();
        public SelectionType Type { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum SelectionType
    {
        Single,
        Multiple,
        Box,
        Filter,
        All
    }

    public class UserView
    {
        public string UserId { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public ViewCamera? Camera { get; set; }
        public List<string> VisibleCategories { get; set; } = new();
        public List<string> HiddenElements { get; set; } = new();
        public double ZoomLevel { get; set; } = 1.0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ViewCamera
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
        public bool IsPerspective { get; set; }
        public double FieldOfView { get; set; }
    }

    public class CollaborationEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public CollaborationEventType Type { get; set; }
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long SequenceNumber { get; set; }
    }

    public class ElementLock
    {
        public string LockId { get; set; } = Guid.NewGuid().ToString();
        public string ElementId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public LockType Type { get; set; }
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public string? Reason { get; set; }
    }

    public class ElementChange
    {
        public string ChangeId { get; set; } = Guid.NewGuid().ToString();
        public string ElementId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public ChangeType Type { get; set; }
        public Dictionary<string, object> OldValues { get; set; } = new();
        public Dictionary<string, object> NewValues { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long SequenceNumber { get; set; }
        public bool IsConflict { get; set; }
    }

    public enum ChangeType
    {
        Create,
        Modify,
        Delete,
        Move,
        Copy,
        Mirror
    }

    public class Conflict
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString();
        public string ElementId { get; set; } = string.Empty;
        public List<ElementChange> ConflictingChanges { get; set; } = new();
        public ConflictStatus Status { get; set; } = ConflictStatus.Pending;
        public string? ResolvedBy { get; set; }
        public ElementChange? Resolution { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    public enum ConflictStatus
    {
        Pending,
        AutoResolved,
        ManuallyResolved,
        Rejected
    }

    public class ChatMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public MessageType Type { get; set; } = MessageType.Text;
        public string? ReplyToId { get; set; }
        public List<string>? MentionedUserIds { get; set; }
        public List<string>? ElementReferences { get; set; }
        public List<MessageAttachment>? Attachments { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }
    }

    public enum MessageType
    {
        Text,
        Image,
        File,
        Link,
        System,
        Reaction
    }

    public class MessageAttachment
    {
        public string AttachmentId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
    }

    public class LiveAnnotation
    {
        public string AnnotationId { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public AnnotationType Type { get; set; }
        public List<AnnotationPoint> Points { get; set; } = new();
        public string? Text { get; set; }
        public string? ViewId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPersistent { get; set; }
    }

    public enum AnnotationType
    {
        Freehand,
        Line,
        Arrow,
        Rectangle,
        Circle,
        Text,
        Callout,
        Highlight,
        Dimension,
        Cloud
    }

    public class AnnotationPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double? Z { get; set; }
        public double? Pressure { get; set; }
    }

    public class FollowRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string FollowerUserId { get; set; } = string.Empty;
        public string LeaderUserId { get; set; } = string.Empty;
        public bool IsAccepted { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class SessionRecording
    {
        public string RecordingId { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public List<RecordedEvent> Events { get; set; } = new();
        public long TotalEvents { get; set; }
        public TimeSpan Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : TimeSpan.Zero;
    }

    public class RecordedEvent
    {
        public long SequenceNumber { get; set; }
        public TimeSpan Offset { get; set; }
        public CollaborationEvent Event { get; set; } = null!;
    }

    #endregion

    /// <summary>
    /// Real-time Collaboration Engine providing live collaboration features
    /// including presence awareness, live cursors, co-editing, and synchronization
    /// </summary>
    public class RealtimeCollaborationEngine : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, CollaborationSession> _sessions = new();
        private readonly ConcurrentDictionary<string, Channel<CollaborationEvent>> _eventChannels = new();
        private readonly ConcurrentDictionary<string, ElementLock> _locks = new();
        private readonly ConcurrentDictionary<string, List<ElementChange>> _changeHistory = new();
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistory = new();
        private readonly ConcurrentDictionary<string, List<LiveAnnotation>> _annotations = new();
        private readonly ConcurrentDictionary<string, Conflict> _conflicts = new();
        private readonly ConcurrentDictionary<string, FollowRequest> _followRequests = new();
        private readonly ConcurrentDictionary<string, SessionRecording> _recordings = new();
        private readonly SemaphoreSlim _sessionSemaphore = new(100);
        private long _sequenceNumber;
        private bool _disposed;

        #region Session Management

        /// <summary>
        /// Create a new collaboration session
        /// </summary>
        public async Task<CollaborationSession> CreateSessionAsync(
            string projectId,
            string hostUserId,
            string hostDisplayName,
            SessionSettings? settings = null,
            CancellationToken ct = default)
        {
            await _sessionSemaphore.WaitAsync(ct);
            try
            {
                var session = new CollaborationSession
                {
                    ProjectId = projectId,
                    HostUserId = hostUserId,
                    Settings = settings ?? new SessionSettings(),
                    Participants = new List<SessionParticipant>
                    {
                        new()
                        {
                            UserId = hostUserId,
                            DisplayName = hostDisplayName,
                            Color = GenerateUserColor(0),
                            Role = ParticipantRole.Host,
                            Status = UserPresenceStatus.Online
                        }
                    }
                };

                _sessions[session.SessionId] = session;
                _eventChannels[session.SessionId] = Channel.CreateUnbounded<CollaborationEvent>();
                _chatHistory[session.SessionId] = new List<ChatMessage>();
                _annotations[session.SessionId] = new List<LiveAnnotation>();

                await BroadcastEventAsync(session.SessionId, new CollaborationEvent
                {
                    SessionId = session.SessionId,
                    UserId = hostUserId,
                    Type = CollaborationEventType.UserJoined,
                    Data = session.Participants.First()
                }, ct);

                return session;
            }
            finally
            {
                _sessionSemaphore.Release();
            }
        }

        /// <summary>
        /// Join an existing collaboration session
        /// </summary>
        public async Task<SessionParticipant> JoinSessionAsync(
            string sessionId,
            string userId,
            string displayName,
            ParticipantRole role = ParticipantRole.Editor,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session {sessionId} not found");

            if (!session.IsActive)
                throw new InvalidOperationException("Session has ended");

            if (session.Participants.Count >= session.Settings.MaxParticipants)
                throw new InvalidOperationException("Session is full");

            var participant = new SessionParticipant
            {
                UserId = userId,
                DisplayName = displayName,
                Color = GenerateUserColor(session.Participants.Count),
                Role = role,
                Status = UserPresenceStatus.Online
            };

            session.Participants.Add(participant);

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = CollaborationEventType.UserJoined,
                Data = participant
            }, ct);

            return participant;
        }

        /// <summary>
        /// Leave a collaboration session
        /// </summary>
        public async Task LeaveSessionAsync(
            string sessionId,
            string userId,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                session.Participants.Remove(participant);

                // Release any locks held by this user
                var userLocks = _locks.Values.Where(l => l.UserId == userId).ToList();
                foreach (var lockItem in userLocks)
                {
                    _locks.TryRemove(lockItem.ElementId, out _);
                }

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Type = CollaborationEventType.UserLeft,
                    Data = participant
                }, ct);
            }

            // End session if host leaves and no participants remain
            if (userId == session.HostUserId || !session.Participants.Any())
            {
                await EndSessionAsync(sessionId, ct);
            }
        }

        /// <summary>
        /// End a collaboration session
        /// </summary>
        public async Task EndSessionAsync(string sessionId, CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            session.EndedAt = DateTime.UtcNow;

            // Notify all participants
            foreach (var participant in session.Participants.ToList())
            {
                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = session.HostUserId,
                    Type = CollaborationEventType.UserLeft,
                    Data = new { Reason = "Session ended" }
                }, ct);
            }

            // Clean up resources
            if (_eventChannels.TryRemove(sessionId, out var channel))
            {
                channel.Writer.Complete();
            }
        }

        /// <summary>
        /// Get session by ID
        /// </summary>
        public CollaborationSession? GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        /// <summary>
        /// Get active sessions for a project
        /// </summary>
        public List<CollaborationSession> GetProjectSessions(string projectId)
        {
            return _sessions.Values
                .Where(s => s.ProjectId == projectId && s.IsActive)
                .ToList();
        }

        #endregion

        #region Presence and Cursor

        /// <summary>
        /// Update user presence status
        /// </summary>
        public async Task UpdatePresenceAsync(
            string sessionId,
            string userId,
            UserPresenceStatus status,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                participant.Status = status;
                participant.LastActivityAt = DateTime.UtcNow;

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Type = CollaborationEventType.UserStatusChanged,
                    Data = new { Status = status }
                }, ct);
            }
        }

        /// <summary>
        /// Update user cursor position
        /// </summary>
        public async Task UpdateCursorAsync(
            string sessionId,
            string userId,
            UserCursor cursor,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            if (!session.Settings.ShowCursors)
                return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                cursor.UserId = userId;
                cursor.UpdatedAt = DateTime.UtcNow;
                participant.Cursor = cursor;
                participant.LastActivityAt = DateTime.UtcNow;

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Type = CollaborationEventType.CursorMoved,
                    Data = cursor
                }, ct);
            }
        }

        /// <summary>
        /// Update user selection
        /// </summary>
        public async Task UpdateSelectionAsync(
            string sessionId,
            string userId,
            UserSelection selection,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            if (!session.Settings.ShowSelections)
                return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                selection.UserId = userId;
                selection.UpdatedAt = DateTime.UtcNow;
                participant.Selection = selection;
                participant.LastActivityAt = DateTime.UtcNow;

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Type = CollaborationEventType.SelectionChanged,
                    Data = selection
                }, ct);
            }
        }

        /// <summary>
        /// Update user view
        /// </summary>
        public async Task UpdateViewAsync(
            string sessionId,
            string userId,
            UserView view,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                view.UserId = userId;
                view.UpdatedAt = DateTime.UtcNow;
                participant.CurrentView = view;
                participant.LastActivityAt = DateTime.UtcNow;

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Type = CollaborationEventType.ViewChanged,
                    Data = view
                }, ct);
            }
        }

        #endregion

        #region Element Locking

        /// <summary>
        /// Acquire a lock on an element
        /// </summary>
        public async Task<ElementLock?> AcquireLockAsync(
            string sessionId,
            string userId,
            string elementId,
            LockType lockType = LockType.Soft,
            string? reason = null,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return null;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                return null;

            // Check if element is already locked
            if (_locks.TryGetValue(elementId, out var existingLock))
            {
                if (existingLock.UserId != userId && existingLock.Type == LockType.Exclusive)
                {
                    return null; // Cannot acquire lock
                }
            }

            var newLock = new ElementLock
            {
                ElementId = elementId,
                UserId = userId,
                UserName = participant.DisplayName,
                Type = lockType,
                Reason = reason,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            _locks[elementId] = newLock;

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = CollaborationEventType.LockAcquired,
                Data = newLock
            }, ct);

            return newLock;
        }

        /// <summary>
        /// Release a lock on an element
        /// </summary>
        public async Task ReleaseLockAsync(
            string sessionId,
            string userId,
            string elementId,
            CancellationToken ct = default)
        {
            if (!_locks.TryGetValue(elementId, out var lockItem))
                return;

            if (lockItem.UserId != userId)
                return; // Can only release own locks

            _locks.TryRemove(elementId, out _);

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = CollaborationEventType.LockReleased,
                Data = new { ElementId = elementId }
            }, ct);
        }

        /// <summary>
        /// Get lock status for an element
        /// </summary>
        public ElementLock? GetLock(string elementId)
        {
            return _locks.TryGetValue(elementId, out var lockItem) ? lockItem : null;
        }

        /// <summary>
        /// Get all locks in a session
        /// </summary>
        public List<ElementLock> GetSessionLocks(string sessionId)
        {
            return _locks.Values.ToList();
        }

        #endregion

        #region Change Tracking

        /// <summary>
        /// Submit an element change
        /// </summary>
        public async Task<ElementChange> SubmitChangeAsync(
            string sessionId,
            string userId,
            ElementChange change,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException("Session not found");

            if (!session.Settings.AllowEditing)
                throw new InvalidOperationException("Editing not allowed in this session");

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null || participant.Role == ParticipantRole.Viewer)
                throw new InvalidOperationException("User cannot edit");

            // Check for lock
            if (_locks.TryGetValue(change.ElementId, out var lockItem))
            {
                if (lockItem.UserId != userId && lockItem.Type == LockType.Exclusive)
                    throw new InvalidOperationException("Element is locked by another user");
            }

            change.UserId = userId;
            change.Timestamp = DateTime.UtcNow;
            change.SequenceNumber = Interlocked.Increment(ref _sequenceNumber);

            // Check for conflicts
            if (!_changeHistory.TryGetValue(change.ElementId, out var history))
            {
                history = new List<ElementChange>();
                _changeHistory[change.ElementId] = history;
            }

            var recentChanges = history
                .Where(c => c.UserId != userId && c.Timestamp > DateTime.UtcNow.AddSeconds(-5))
                .ToList();

            if (recentChanges.Any())
            {
                change.IsConflict = true;
                await HandleConflictAsync(sessionId, change, recentChanges, session.Settings.ConflictStrategy, ct);
            }

            history.Add(change);

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = change.Type switch
                {
                    ChangeType.Create => CollaborationEventType.ElementCreated,
                    ChangeType.Delete => CollaborationEventType.ElementDeleted,
                    _ => CollaborationEventType.ElementModified
                },
                Data = change
            }, ct);

            return change;
        }

        private async Task HandleConflictAsync(
            string sessionId,
            ElementChange newChange,
            List<ElementChange> conflictingChanges,
            ConflictResolution strategy,
            CancellationToken ct)
        {
            var conflict = new Conflict
            {
                ElementId = newChange.ElementId,
                ConflictingChanges = conflictingChanges.Append(newChange).ToList()
            };

            switch (strategy)
            {
                case ConflictResolution.LastWriteWins:
                    conflict.Status = ConflictStatus.AutoResolved;
                    conflict.Resolution = newChange;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    break;

                case ConflictResolution.FirstWriteWins:
                    conflict.Status = ConflictStatus.AutoResolved;
                    conflict.Resolution = conflictingChanges.First();
                    conflict.ResolvedAt = DateTime.UtcNow;
                    break;

                case ConflictResolution.Manual:
                    conflict.Status = ConflictStatus.Pending;
                    _conflicts[conflict.ConflictId] = conflict;
                    await BroadcastEventAsync(sessionId, new CollaborationEvent
                    {
                        SessionId = sessionId,
                        UserId = newChange.UserId,
                        Type = CollaborationEventType.Conflict,
                        Data = conflict
                    }, ct);
                    break;
            }
        }

        #endregion

        #region Chat and Annotations

        /// <summary>
        /// Send a chat message
        /// </summary>
        public async Task<ChatMessage> SendChatMessageAsync(
            string sessionId,
            string userId,
            string content,
            MessageType type = MessageType.Text,
            string? replyToId = null,
            List<string>? mentions = null,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException("Session not found");

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                throw new InvalidOperationException("User not in session");

            var message = new ChatMessage
            {
                SessionId = sessionId,
                UserId = userId,
                UserName = participant.DisplayName,
                Content = content,
                Type = type,
                ReplyToId = replyToId,
                MentionedUserIds = mentions
            };

            if (!_chatHistory.TryGetValue(sessionId, out var history))
            {
                history = new List<ChatMessage>();
                _chatHistory[sessionId] = history;
            }
            history.Add(message);

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = CollaborationEventType.Chat,
                Data = message
            }, ct);

            return message;
        }

        /// <summary>
        /// Get chat history for a session
        /// </summary>
        public List<ChatMessage> GetChatHistory(string sessionId, int limit = 100)
        {
            if (!_chatHistory.TryGetValue(sessionId, out var history))
                return new List<ChatMessage>();

            return history.TakeLast(limit).ToList();
        }

        /// <summary>
        /// Add a live annotation
        /// </summary>
        public async Task<LiveAnnotation> AddAnnotationAsync(
            string sessionId,
            string userId,
            LiveAnnotation annotation,
            CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException("Session not found");

            if (!session.Settings.AllowMarkups)
                throw new InvalidOperationException("Markups not allowed in this session");

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                throw new InvalidOperationException("User not in session");

            annotation.SessionId = sessionId;
            annotation.UserId = userId;
            annotation.UserName = participant.DisplayName;
            annotation.Color = participant.Color;
            annotation.CreatedAt = DateTime.UtcNow;

            if (!_annotations.TryGetValue(sessionId, out var annotations))
            {
                annotations = new List<LiveAnnotation>();
                _annotations[sessionId] = annotations;
            }
            annotations.Add(annotation);

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = userId,
                Type = CollaborationEventType.AnnotationAdded,
                Data = annotation
            }, ct);

            return annotation;
        }

        /// <summary>
        /// Get annotations for a session
        /// </summary>
        public List<LiveAnnotation> GetAnnotations(string sessionId)
        {
            if (!_annotations.TryGetValue(sessionId, out var annotations))
                return new List<LiveAnnotation>();

            return annotations.ToList();
        }

        #endregion

        #region Follow Mode

        /// <summary>
        /// Request to follow another user's view
        /// </summary>
        public async Task<FollowRequest> RequestFollowAsync(
            string sessionId,
            string followerUserId,
            string leaderUserId,
            CancellationToken ct = default)
        {
            var request = new FollowRequest
            {
                FollowerUserId = followerUserId,
                LeaderUserId = leaderUserId
            };

            _followRequests[request.RequestId] = request;

            await BroadcastEventAsync(sessionId, new CollaborationEvent
            {
                SessionId = sessionId,
                UserId = followerUserId,
                Type = CollaborationEventType.ViewChanged,
                Data = new { FollowRequest = request }
            }, ct);

            return request;
        }

        /// <summary>
        /// Accept a follow request
        /// </summary>
        public async Task AcceptFollowAsync(
            string sessionId,
            string requestId,
            CancellationToken ct = default)
        {
            if (_followRequests.TryGetValue(requestId, out var request))
            {
                request.IsAccepted = true;

                await BroadcastEventAsync(sessionId, new CollaborationEvent
                {
                    SessionId = sessionId,
                    UserId = request.LeaderUserId,
                    Type = CollaborationEventType.ViewChanged,
                    Data = new { FollowAccepted = request }
                }, ct);
            }
        }

        #endregion

        #region Event Broadcasting

        /// <summary>
        /// Broadcast an event to all session participants
        /// </summary>
        private async Task BroadcastEventAsync(
            string sessionId,
            CollaborationEvent collaborationEvent,
            CancellationToken ct = default)
        {
            collaborationEvent.SequenceNumber = Interlocked.Increment(ref _sequenceNumber);
            collaborationEvent.Timestamp = DateTime.UtcNow;

            if (_eventChannels.TryGetValue(sessionId, out var channel))
            {
                await channel.Writer.WriteAsync(collaborationEvent, ct);
            }
        }

        /// <summary>
        /// Subscribe to session events
        /// </summary>
        public async IAsyncEnumerable<CollaborationEvent> SubscribeToEventsAsync(
            string sessionId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!_eventChannels.TryGetValue(sessionId, out var channel))
                yield break;

            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }

        #endregion

        #region Utilities

        private string GenerateUserColor(int index)
        {
            var colors = new[]
            {
                "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
                "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9",
                "#F1948A", "#82E0AA", "#F8C471", "#D7BDE2", "#A3E4D7"
            };
            return colors[index % colors.Length];
        }

        /// <summary>
        /// Get session statistics
        /// </summary>
        public Dictionary<string, object> GetSessionStats(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                ["ParticipantCount"] = session.Participants.Count,
                ["ActiveLocks"] = _locks.Count,
                ["ChatMessages"] = _chatHistory.TryGetValue(sessionId, out var chat) ? chat.Count : 0,
                ["Annotations"] = _annotations.TryGetValue(sessionId, out var ann) ? ann.Count : 0,
                ["Duration"] = DateTime.UtcNow - session.StartedAt,
                ["IsActive"] = session.IsActive
            };
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var channel in _eventChannels.Values)
            {
                channel.Writer.Complete();
            }

            _sessionSemaphore.Dispose();
            _sessions.Clear();
            _eventChannels.Clear();
            _locks.Clear();
            _changeHistory.Clear();
            _chatHistory.Clear();
            _annotations.Clear();
            _conflicts.Clear();
            _followRequests.Clear();
            _recordings.Clear();

            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
