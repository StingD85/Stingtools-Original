// =============================================================================
// StingBIM.AI.Collaboration - Core Models
// Data models for worksharing collaboration system
// =============================================================================

using System;
using System.Collections.Generic;

namespace StingBIM.AI.Collaboration.Models
{
    #region User & Presence Models

    /// <summary>
    /// Represents a team member in the collaboration system
    /// </summary>
    public class TeamMember
    {
        public string UserId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpper();
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty; // Architectural, Structural, MEP, etc.
        public string Role { get; set; } = string.Empty; // Designer, Coordinator, Manager
        public string WorkstationName { get; set; } = Environment.MachineName;
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 51234;
        public PresenceStatus Status { get; set; } = PresenceStatus.Online;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public string CurrentProject { get; set; } = string.Empty;
        public string CurrentWorkset { get; set; } = string.Empty;
        public List<string> ActiveElements { get; set; } = new();
        public string AvatarColor { get; set; } = GenerateAvatarColor();

        private static string GenerateAvatarColor()
        {
            var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F" };
            return colors[new Random().Next(colors.Length)];
        }
    }

    /// <summary>
    /// User presence status
    /// </summary>
    public enum PresenceStatus
    {
        Online,
        Away,
        Busy,
        DoNotDisturb,
        InSync,
        Offline
    }

    #endregion

    #region Chat & Messaging Models

    /// <summary>
    /// Chat message in the collaboration system
    /// </summary>
    public class ChatMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N")[..16].ToUpper();
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public MessageType Type { get; set; } = MessageType.Text;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? TargetUserId { get; set; } // For direct messages
        public string? ElementId { get; set; } // For element-related messages
        public string? ViewId { get; set; } // For view sharing
        public List<string> Mentions { get; set; } = new(); // @username mentions
        public List<MessageAttachment> Attachments { get; set; } = new();
        public bool IsSystemMessage { get; set; }
        public bool IsAIResponse { get; set; }
    }

    /// <summary>
    /// Message types
    /// </summary>
    public enum MessageType
    {
        Text,
        Command,
        SystemNotification,
        AIResponse,
        ElementShare,
        ViewShare,
        ConflictAlert,
        SyncNotification,
        WorksetRequest,
        WorksetTransfer
    }

    /// <summary>
    /// Message attachment (screenshot, element reference, etc.)
    /// </summary>
    public class MessageAttachment
    {
        public string AttachmentId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public AttachmentType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty; // Base64 for images, JSON for elements
        public long SizeBytes { get; set; }
    }

    public enum AttachmentType
    {
        Screenshot,
        ElementReference,
        ViewReference,
        Document
    }

    #endregion

    #region Worksharing Models

    /// <summary>
    /// Represents a workset in the project
    /// </summary>
    public class WorksetInfo
    {
        public int WorksetId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public bool IsEditable { get; set; }
        public bool IsOpen { get; set; }
        public bool IsDefault { get; set; }
        public int ElementCount { get; set; }
        public DateTime LastModified { get; set; }
        public List<string> RecentEditors { get; set; } = new();
    }

    /// <summary>
    /// Element activity tracking
    /// </summary>
    public class ElementActivity
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public ActivityType ActivityType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? WorksetName { get; set; }
        public string? LevelName { get; set; }
        public Dictionary<string, object> Changes { get; set; } = new();
    }

    public enum ActivityType
    {
        Created,
        Modified,
        Deleted,
        Moved,
        CheckedOut,
        CheckedIn,
        Viewed
    }

    /// <summary>
    /// Sync status information
    /// </summary>
    public class SyncStatus
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime LastSyncTime { get; set; }
        public int LocalChangesCount { get; set; }
        public int CentralChangesCount { get; set; }
        public int PotentialConflicts { get; set; }
        public SyncState State { get; set; }
        public List<PendingChange> PendingChanges { get; set; } = new();
        public List<ConflictPrediction> PredictedConflicts { get; set; } = new();
        public string? SyncRecommendation { get; set; }
    }

    public enum SyncState
    {
        UpToDate,
        LocalChanges,
        CentralChanges,
        BothChanged,
        Syncing,
        Error
    }

    /// <summary>
    /// Pending change awaiting sync
    /// </summary>
    public class PendingChange
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public DateTime ModifiedAt { get; set; }
        public string? Description { get; set; }
    }

    #endregion

    #region Conflict Models

    /// <summary>
    /// AI-predicted conflict before sync
    /// </summary>
    public class ConflictPrediction
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString("N")[..10].ToUpper();
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string LocalUserId { get; set; } = string.Empty;
        public string LocalUserName { get; set; } = string.Empty;
        public string RemoteUserId { get; set; } = string.Empty;
        public string RemoteUserName { get; set; } = string.Empty;
        public ConflictSeverity Severity { get; set; }
        public double Probability { get; set; } // 0.0 to 1.0
        public string Description { get; set; } = string.Empty;
        public string? AIResolutionSuggestion { get; set; }
        public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ConflictSeverity
    {
        Low,      // Same parameter, similar values
        Medium,   // Different parameters on same element
        High,     // Conflicting geometry or critical parameters
        Critical  // Element deleted by one user, modified by another
    }

    #endregion

    #region Command Models

    /// <summary>
    /// Parsed command from chat
    /// </summary>
    public class ParsedCommand
    {
        public string CommandName { get; set; } = string.Empty;
        public List<string> Arguments { get; set; } = new();
        public Dictionary<string, string> Options { get; set; } = new();
        public string RawInput { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Command execution result
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<ChatMessage> ResponseMessages { get; set; } = new();
    }

    #endregion

    #region Analytics Models

    /// <summary>
    /// Team activity summary
    /// </summary>
    public class TeamActivitySummary
    {
        public string ProjectName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalEdits { get; set; }
        public int SyncsPerformed { get; set; }
        public int ConflictsResolved { get; set; }
        public int MessagesExchanged { get; set; }
        public Dictionary<string, int> EditsByUser { get; set; } = new();
        public Dictionary<string, int> EditsByCategory { get; set; } = new();
        public Dictionary<string, int> EditsByLevel { get; set; } = new();
        public List<ActivityHotspot> Hotspots { get; set; } = new();
    }

    /// <summary>
    /// Area with high activity
    /// </summary>
    public class ActivityHotspot
    {
        public string AreaName { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public List<string> ActiveUsers { get; set; } = new();
        public string? ConflictRisk { get; set; }
    }

    #endregion

    #region Network Models

    /// <summary>
    /// Discovered peer on the network
    /// </summary>
    public class DiscoveredPeer
    {
        public string PeerId { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// Network discovery broadcast packet
    /// </summary>
    public class DiscoveryPacket
    {
        public string PacketType { get; set; } = "STINGBIM_DISCOVERY";
        public string Version { get; set; } = "7.0";
        public string PeerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectGuid { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    #endregion
}
