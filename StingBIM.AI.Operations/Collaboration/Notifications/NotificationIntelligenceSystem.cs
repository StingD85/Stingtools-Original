// StingBIM.AI.Collaboration - Notification Intelligence System
// Smart notification routing, batching, prioritization, and multi-channel delivery

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Notifications
{
    #region Enums

    /// <summary>
    /// Types of notifications supported by the system
    /// </summary>
    public enum NotificationType
    {
        Alert,
        Warning,
        Info,
        ActionRequired,
        Reminder,
        Escalation,
        Approval,
        Milestone,
        Deadline,
        Mention,
        Assignment,
        Comment,
        Update,
        Completion,
        Error,
        System,
        ClashDetected,
        ModelSync,
        ReviewRequest,
        ChangeOrder
    }

    /// <summary>
    /// Priority levels for notifications
    /// </summary>
    public enum NotificationPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Urgent = 4,
        Critical = 5
    }

    /// <summary>
    /// Available delivery channels
    /// </summary>
    public enum DeliveryChannel
    {
        InApp,
        Email,
        Push,
        SMS,
        Slack,
        Teams,
        Webhook,
        Desktop
    }

    /// <summary>
    /// Status of a notification or delivery
    /// </summary>
    public enum NotificationStatus
    {
        Pending,
        Queued,
        Sent,
        Delivered,
        Read,
        Dismissed,
        Failed,
        Expired,
        Escalated
    }

    /// <summary>
    /// Frequency options for digest generation
    /// </summary>
    public enum DigestFrequency
    {
        Realtime,
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>
    /// User roles for priority calculation
    /// </summary>
    public enum UserRole
    {
        Viewer,
        Contributor,
        Editor,
        Reviewer,
        Approver,
        ProjectManager,
        Administrator,
        Executive
    }

    #endregion

    #region Core Data Models

    /// <summary>
    /// Represents a notification in the system
    /// </summary>
    public class Notification
    {
        public string NotificationId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public NotificationPriority CalculatedPriority { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? HtmlMessage { get; set; }
        public string? PlainTextMessage { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string? SenderName { get; set; }
        public string? SenderAvatar { get; set; }
        public List<string> RecipientIds { get; set; } = new();
        public List<DeliveryChannel> Channels { get; set; } = new() { DeliveryChannel.InApp };
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public List<NotificationAction>? Actions { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<string>? MentionedUserIds { get; set; }
        public List<string>? Tags { get; set; }
        public string? GroupKey { get; set; }
        public string? ThreadId { get; set; }
        public string? ParentNotificationId { get; set; }
        public bool IsBatchable { get; set; } = true;
        public bool IsImportant { get; set; }
        public bool RequiresAcknowledgement { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledFor { get; set; }
        public string? TemplateId { get; set; }
        public int EscalationLevel { get; set; }
        public DateTime? LastEscalatedAt { get; set; }
        public string? SourceSystem { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Action that can be taken on a notification
    /// </summary>
    public class NotificationAction
    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string ActionType { get; set; } = "link";
        public string? Url { get; set; }
        public Dictionary<string, object>? Payload { get; set; }
        public bool IsPrimary { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    /// <summary>
    /// Tracks delivery of a notification to a recipient via a channel
    /// </summary>
    public class NotificationDelivery
    {
        public string DeliveryId { get; set; } = Guid.NewGuid().ToString();
        public string NotificationId { get; set; } = string.Empty;
        public string RecipientId { get; set; } = string.Empty;
        public DeliveryChannel Channel { get; set; }
        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? QueuedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? DismissedAt { get; set; }
        public string? Error { get; set; }
        public string? ErrorCode { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public string? ExternalMessageId { get; set; }
        public Dictionary<string, object>? DeliveryMetadata { get; set; }
    }

    /// <summary>
    /// Template for generating notifications
    /// </summary>
    public class NotificationTemplate
    {
        public string TemplateId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "general";
        public NotificationType Type { get; set; }
        public string TitleTemplate { get; set; } = string.Empty;
        public string MessageTemplate { get; set; } = string.Empty;
        public string? HtmlTemplate { get; set; }
        public string? PlainTextTemplate { get; set; }
        public string? EmailSubjectTemplate { get; set; }
        public string? EmailBodyTemplate { get; set; }
        public string? PushTitleTemplate { get; set; }
        public string? PushBodyTemplate { get; set; }
        public string? SlackTemplate { get; set; }
        public string? TeamsTemplate { get; set; }
        public string? SmsTemplate { get; set; }
        public List<TemplatePlaceholder> Placeholders { get; set; } = new();
        public NotificationPriority DefaultPriority { get; set; }
        public List<DeliveryChannel> DefaultChannels { get; set; } = new();
        public bool RequiresAcknowledgement { get; set; }
        public TimeSpan? AutoExpireAfter { get; set; }
        public string? IconUrl { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSystem { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// Placeholder definition for templates
    /// </summary>
    public class TemplatePlaceholder
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";
        public bool IsRequired { get; set; }
        public string? DefaultValue { get; set; }
        public string? Format { get; set; }
    }

    /// <summary>
    /// User notification preferences
    /// </summary>
    public class NotificationPreferences
    {
        public string UserId { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? SlackUserId { get; set; }
        public string? TeamsUserId { get; set; }
        public UserRole Role { get; set; } = UserRole.Contributor;
        public DigestFrequency DigestFrequency { get; set; } = DigestFrequency.Daily;
        public TimeSpan? DigestTime { get; set; } = TimeSpan.FromHours(9);
        public DayOfWeek DigestDayOfWeek { get; set; } = DayOfWeek.Monday;
        public bool GlobalEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
        public bool SmsEnabled { get; set; } = false;
        public bool SlackEnabled { get; set; } = false;
        public bool TeamsEnabled { get; set; } = false;
        public bool DesktopEnabled { get; set; } = true;
        public bool WebhookEnabled { get; set; } = false;
        public string? WebhookUrl { get; set; }
        public Dictionary<NotificationType, TypePreferences> TypePreferences { get; set; } = new();
        public Dictionary<string, bool> ProjectMuted { get; set; } = new();
        public List<string> MutedSenderIds { get; set; } = new();
        public List<string> MutedThreadIds { get; set; } = new();
        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }
        public List<DayOfWeek>? QuietDays { get; set; }
        public bool OverrideQuietHoursForUrgent { get; set; } = true;
        public string Timezone { get; set; } = "UTC";
        public string Language { get; set; } = "en";
        public int MaxNotificationsPerHour { get; set; } = 50;
        public int MaxEmailsPerDay { get; set; } = 20;
        public bool BatchSimilarNotifications { get; set; } = true;
        public TimeSpan BatchingWindow { get; set; } = TimeSpan.FromMinutes(5);
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Per-type notification preferences
    /// </summary>
    public class TypePreferences
    {
        public bool Enabled { get; set; } = true;
        public bool InApp { get; set; } = true;
        public bool Email { get; set; } = true;
        public bool Push { get; set; } = true;
        public bool Sms { get; set; } = false;
        public bool Slack { get; set; } = false;
        public bool Teams { get; set; } = false;
        public NotificationPriority MinPriority { get; set; } = NotificationPriority.Low;
        public bool BatchEnabled { get; set; } = true;
    }

    /// <summary>
    /// Notification digest summary
    /// </summary>
    public class NotificationDigest
    {
        public string DigestId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public DigestFrequency Frequency { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<DigestSection> Sections { get; set; } = new();
        public int TotalNotifications { get; set; }
        public int UnreadCount { get; set; }
        public int ActionRequiredCount { get; set; }
        public int HighPriorityCount { get; set; }
        public string? HtmlContent { get; set; }
        public string? PlainTextContent { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public bool IsSent { get; set; }
        public DateTime? SentAt { get; set; }
        public List<DigestHighlight>? Highlights { get; set; }
    }

    /// <summary>
    /// Section within a digest
    /// </summary>
    public class DigestSection
    {
        public string SectionId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public NotificationType Type { get; set; }
        public List<DigestItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int DisplayedCount { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int Priority { get; set; }
    }

    /// <summary>
    /// Individual item in a digest section
    /// </summary>
    public class DigestItem
    {
        public string NotificationId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? SenderName { get; set; }
        public string? ActionUrl { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsImportant { get; set; }
        public NotificationPriority Priority { get; set; }
    }

    /// <summary>
    /// Highlighted item in a digest
    /// </summary>
    public class DigestHighlight
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; }
        public string? ActionUrl { get; set; }
    }

    /// <summary>
    /// Group of related notifications
    /// </summary>
    public class NotificationGroup
    {
        public string GroupKey { get; set; } = string.Empty;
        public string GroupType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public List<string> NotificationIds { get; set; } = new();
        public int Count { get; set; }
        public DateTime FirstAt { get; set; }
        public DateTime LastAt { get; set; }
        public NotificationPriority HighestPriority { get; set; }
        public bool IsCollapsed { get; set; } = true;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
    }

    /// <summary>
    /// Escalation rule configuration
    /// </summary>
    public class EscalationRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; }
        public List<NotificationType> TriggerTypes { get; set; } = new();
        public NotificationPriority? MinPriority { get; set; }
        public TimeSpan EscalateAfter { get; set; }
        public int MaxEscalationLevel { get; set; } = 3;
        public List<EscalationLevel> Levels { get; set; } = new();
        public bool AutoResolveOnAcknowledge { get; set; } = true;
        public string? ProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Level within an escalation rule
    /// </summary>
    public class EscalationLevel
    {
        public int Level { get; set; }
        public TimeSpan EscalateAfter { get; set; }
        public List<string> NotifyUserIds { get; set; } = new();
        public List<UserRole> NotifyRoles { get; set; } = new();
        public List<DeliveryChannel> AdditionalChannels { get; set; } = new();
        public NotificationPriority NewPriority { get; set; }
        public string? CustomMessage { get; set; }
    }

    #endregion

    #region Query and Result Models

    /// <summary>
    /// Query parameters for retrieving notifications
    /// </summary>
    public class NotificationQuery
    {
        public string UserId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public List<NotificationType>? Types { get; set; }
        public List<NotificationStatus>? Statuses { get; set; }
        public NotificationPriority? MinPriority { get; set; }
        public NotificationPriority? MaxPriority { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool UnreadOnly { get; set; }
        public bool ActionRequiredOnly { get; set; }
        public bool ImportantOnly { get; set; }
        public string? GroupKey { get; set; }
        public string? ThreadId { get; set; }
        public string? SenderId { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public List<string>? Tags { get; set; }
        public string? SearchText { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public bool SortDescending { get; set; } = true;
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
        public bool IncludeExpired { get; set; }
        public bool IncludeDismissed { get; set; }
    }

    /// <summary>
    /// Result of a notification query
    /// </summary>
    public class NotificationResult
    {
        public List<Notification> Notifications { get; set; } = new();
        public List<NotificationDelivery> Deliveries { get; set; } = new();
        public List<NotificationGroup>? Groups { get; set; }
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int ActionRequiredCount { get; set; }
        public bool HasMore { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    /// <summary>
    /// Statistics about notifications for a user
    /// </summary>
    public class NotificationStats
    {
        public string UserId { get; set; } = string.Empty;
        public int TotalUnread { get; set; }
        public int TotalToday { get; set; }
        public int TotalThisWeek { get; set; }
        public Dictionary<NotificationType, int> UnreadByType { get; set; } = new();
        public Dictionary<NotificationPriority, int> UnreadByPriority { get; set; } = new();
        public Dictionary<string, int> UnreadByProject { get; set; } = new();
        public int ActionRequired { get; set; }
        public int AwaitingAcknowledgement { get; set; }
        public DateTime? LastNotificationAt { get; set; }
        public DateTime? LastReadAt { get; set; }
        public double AverageResponseTime { get; set; }
        public int EscalatedCount { get; set; }
    }

    /// <summary>
    /// History of notifications for audit purposes
    /// </summary>
    public class NotificationHistory
    {
        public string HistoryId { get; set; } = Guid.NewGuid().ToString();
        public string NotificationId { get; set; } = string.Empty;
        public string RecipientId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? PerformedBy { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    #endregion

    /// <summary>
    /// Notification Intelligence System providing smart notification routing,
    /// batching, prioritization, and multi-channel delivery
    /// </summary>
    public class NotificationIntelligenceSystem : IAsyncDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<string, Notification> _notifications = new();
        private readonly ConcurrentDictionary<string, List<NotificationDelivery>> _deliveries = new();
        private readonly ConcurrentDictionary<string, NotificationTemplate> _templates = new();
        private readonly ConcurrentDictionary<string, NotificationPreferences> _preferences = new();
        private readonly ConcurrentDictionary<string, NotificationGroup> _groups = new();
        private readonly ConcurrentDictionary<string, EscalationRule> _escalationRules = new();
        private readonly ConcurrentDictionary<string, NotificationDigest> _digests = new();
        private readonly ConcurrentDictionary<string, List<NotificationHistory>> _history = new();
        private readonly ConcurrentDictionary<string, int> _userHourlyCount = new();
        private readonly ConcurrentDictionary<string, int> _userDailyEmailCount = new();

        private readonly Channel<Notification> _notificationQueue;
        private readonly Channel<EscalationCheck> _escalationQueue;
        private readonly Channel<DigestRequest> _digestQueue;

        private readonly SemaphoreSlim _deliverySemaphore = new(10);
        private readonly CancellationTokenSource _processorCts = new();
        private readonly Task _notificationProcessor;
        private readonly Task _escalationProcessor;
        private readonly Task _digestProcessor;
        private readonly Timer _hourlyResetTimer;
        private readonly Timer _dailyResetTimer;
        private readonly Timer _escalationCheckTimer;
        private readonly object _lockObject = new();
        private bool _disposed;

        #endregion

        #region Events

        public event EventHandler<NotificationSentEventArgs>? NotificationSent;
        public event EventHandler<NotificationDeliveredEventArgs>? NotificationDelivered;
        public event EventHandler<NotificationReadEventArgs>? NotificationRead;
        public event EventHandler<NotificationEscalatedEventArgs>? NotificationEscalated;
        public event EventHandler<DigestGeneratedEventArgs>? DigestGenerated;

        #endregion

        #region Constructor

        public NotificationIntelligenceSystem()
        {
            _notificationQueue = Channel.CreateUnbounded<Notification>();
            _escalationQueue = Channel.CreateUnbounded<EscalationCheck>();
            _digestQueue = Channel.CreateUnbounded<DigestRequest>();

            _notificationProcessor = ProcessNotificationsAsync(_processorCts.Token);
            _escalationProcessor = ProcessEscalationsAsync(_processorCts.Token);
            _digestProcessor = ProcessDigestsAsync(_processorCts.Token);

            _hourlyResetTimer = new Timer(ResetHourlyCounts, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            _dailyResetTimer = new Timer(ResetDailyCounts, null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
            _escalationCheckTimer = new Timer(CheckEscalations, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            InitializeDefaultTemplates();
            InitializeDefaultEscalationRules();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultTemplates()
        {
            var templates = new[]
            {
                new NotificationTemplate
                {
                    Name = "Task Assignment",
                    Type = NotificationType.Assignment,
                    Category = "tasks",
                    TitleTemplate = "New Task Assigned: {{taskName}}",
                    MessageTemplate = "{{assignerName}} has assigned you a new task: {{taskName}}",
                    HtmlTemplate = "<p><strong>{{assignerName}}</strong> has assigned you a new task: <a href=\"{{actionUrl}}\">{{taskName}}</a></p>",
                    EmailSubjectTemplate = "[{{projectName}}] Task Assigned: {{taskName}}",
                    PushTitleTemplate = "New Task",
                    PushBodyTemplate = "{{taskName}}",
                    DefaultPriority = NotificationPriority.Normal,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push },
                    Placeholders = new()
                    {
                        new() { Key = "taskName", Description = "Name of the task", IsRequired = true },
                        new() { Key = "assignerName", Description = "Name of the person assigning", IsRequired = true },
                        new() { Key = "projectName", Description = "Project name", IsRequired = false },
                        new() { Key = "actionUrl", Description = "URL to view the task", IsRequired = false }
                    }
                },
                new NotificationTemplate
                {
                    Name = "Approval Request",
                    Type = NotificationType.Approval,
                    Category = "approvals",
                    TitleTemplate = "Approval Required: {{itemName}}",
                    MessageTemplate = "{{requesterName}} is requesting your approval for {{itemName}}. Please review and respond.",
                    HtmlTemplate = "<p><strong>{{requesterName}}</strong> is requesting your approval for <a href=\"{{actionUrl}}\">{{itemName}}</a>.</p><p>{{description}}</p>",
                    EmailSubjectTemplate = "[ACTION REQUIRED] Approval Needed: {{itemName}}",
                    PushTitleTemplate = "Approval Needed",
                    PushBodyTemplate = "{{itemName}} - {{requesterName}}",
                    DefaultPriority = NotificationPriority.High,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push },
                    RequiresAcknowledgement = true,
                    AutoExpireAfter = TimeSpan.FromDays(7)
                },
                new NotificationTemplate
                {
                    Name = "Deadline Reminder",
                    Type = NotificationType.Deadline,
                    Category = "reminders",
                    TitleTemplate = "Deadline Approaching: {{itemName}}",
                    MessageTemplate = "{{itemName}} is due in {{timeRemaining}}. Please ensure it is completed on time.",
                    HtmlTemplate = "<p><strong>{{itemName}}</strong> is due in <strong>{{timeRemaining}}</strong>.</p><p><a href=\"{{actionUrl}}\">View Details</a></p>",
                    EmailSubjectTemplate = "[REMINDER] {{itemName}} Due {{dueDate}}",
                    DefaultPriority = NotificationPriority.High,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push }
                },
                new NotificationTemplate
                {
                    Name = "Mention",
                    Type = NotificationType.Mention,
                    Category = "social",
                    TitleTemplate = "{{mentionerName}} mentioned you",
                    MessageTemplate = "{{mentionerName}} mentioned you in {{context}}: \"{{excerpt}}\"",
                    HtmlTemplate = "<p><strong>{{mentionerName}}</strong> mentioned you in <a href=\"{{actionUrl}}\">{{context}}</a>:</p><blockquote>{{excerpt}}</blockquote>",
                    DefaultPriority = NotificationPriority.Normal,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Push }
                },
                new NotificationTemplate
                {
                    Name = "Issue Alert",
                    Type = NotificationType.Alert,
                    Category = "issues",
                    TitleTemplate = "Issue: {{issueTitle}}",
                    MessageTemplate = "A new issue has been reported: {{issueDescription}}",
                    HtmlTemplate = "<p><strong>Issue:</strong> {{issueTitle}}</p><p>{{issueDescription}}</p><p><a href=\"{{actionUrl}}\">View Issue</a></p>",
                    EmailSubjectTemplate = "[ALERT] {{issueTitle}}",
                    DefaultPriority = NotificationPriority.High,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push },
                    Color = "#dc3545"
                },
                new NotificationTemplate
                {
                    Name = "Milestone Reached",
                    Type = NotificationType.Milestone,
                    Category = "progress",
                    TitleTemplate = "Milestone Reached: {{milestoneName}}",
                    MessageTemplate = "The project has reached milestone: {{milestoneName}}. {{description}}",
                    HtmlTemplate = "<h3>Milestone Reached!</h3><p><strong>{{milestoneName}}</strong></p><p>{{description}}</p>",
                    DefaultPriority = NotificationPriority.Normal,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email },
                    Color = "#28a745"
                },
                new NotificationTemplate
                {
                    Name = "Comment Added",
                    Type = NotificationType.Comment,
                    Category = "social",
                    TitleTemplate = "New Comment on {{itemName}}",
                    MessageTemplate = "{{commenterName}} commented: {{commentPreview}}",
                    HtmlTemplate = "<p><strong>{{commenterName}}</strong> commented on <a href=\"{{actionUrl}}\">{{itemName}}</a>:</p><p>{{commentPreview}}</p>",
                    DefaultPriority = NotificationPriority.Low,
                    DefaultChannels = new() { DeliveryChannel.InApp }
                },
                new NotificationTemplate
                {
                    Name = "Escalation",
                    Type = NotificationType.Escalation,
                    Category = "urgent",
                    TitleTemplate = "[ESCALATED Level {{level}}] {{itemName}}",
                    MessageTemplate = "{{itemName}} has been escalated to level {{level}} and requires immediate attention. Original due: {{originalDue}}",
                    HtmlTemplate = "<div style=\"border-left: 4px solid #dc3545; padding-left: 16px;\"><h3>ESCALATION - Level {{level}}</h3><p><strong>{{itemName}}</strong></p><p>This item requires immediate attention.</p><p><a href=\"{{actionUrl}}\">Take Action</a></p></div>",
                    EmailSubjectTemplate = "[ESCALATED] {{itemName}} - Immediate Action Required",
                    DefaultPriority = NotificationPriority.Urgent,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push, DeliveryChannel.SMS },
                    RequiresAcknowledgement = true,
                    Color = "#dc3545"
                },
                new NotificationTemplate
                {
                    Name = "Clash Detected",
                    Type = NotificationType.ClashDetected,
                    Category = "bim",
                    TitleTemplate = "{{clashCount}} Clashes Detected",
                    MessageTemplate = "{{clashCount}} new clashes detected in {{modelName}}. {{criticalCount}} critical clashes require attention.",
                    HtmlTemplate = "<p><strong>{{clashCount}}</strong> new clashes detected in <strong>{{modelName}}</strong></p><ul><li>Critical: {{criticalCount}}</li><li>Major: {{majorCount}}</li><li>Minor: {{minorCount}}</li></ul><p><a href=\"{{actionUrl}}\">View Clash Report</a></p>",
                    DefaultPriority = NotificationPriority.High,
                    DefaultChannels = new() { DeliveryChannel.InApp, DeliveryChannel.Email }
                },
                new NotificationTemplate
                {
                    Name = "Model Sync",
                    Type = NotificationType.ModelSync,
                    Category = "bim",
                    TitleTemplate = "Model Updated: {{modelName}}",
                    MessageTemplate = "{{userName}} has synchronized changes to {{modelName}}. {{changeCount}} elements modified.",
                    DefaultPriority = NotificationPriority.Low,
                    DefaultChannels = new() { DeliveryChannel.InApp }
                }
            };

            foreach (var template in templates)
            {
                template.IsSystem = true;
                _templates[template.TemplateId] = template;
            }
        }

        private void InitializeDefaultEscalationRules()
        {
            var rules = new[]
            {
                new EscalationRule
                {
                    Name = "Approval Escalation",
                    Description = "Escalate approval requests that haven't been addressed",
                    TriggerTypes = new() { NotificationType.Approval },
                    MinPriority = NotificationPriority.Normal,
                    EscalateAfter = TimeSpan.FromHours(24),
                    MaxEscalationLevel = 3,
                    Levels = new()
                    {
                        new EscalationLevel
                        {
                            Level = 1,
                            EscalateAfter = TimeSpan.FromHours(24),
                            NotifyRoles = new() { UserRole.ProjectManager },
                            NewPriority = NotificationPriority.High,
                            CustomMessage = "This approval has been pending for 24 hours"
                        },
                        new EscalationLevel
                        {
                            Level = 2,
                            EscalateAfter = TimeSpan.FromHours(48),
                            NotifyRoles = new() { UserRole.Administrator },
                            AdditionalChannels = new() { DeliveryChannel.SMS },
                            NewPriority = NotificationPriority.Urgent,
                            CustomMessage = "This approval has been pending for 48 hours"
                        },
                        new EscalationLevel
                        {
                            Level = 3,
                            EscalateAfter = TimeSpan.FromHours(72),
                            NotifyRoles = new() { UserRole.Executive },
                            AdditionalChannels = new() { DeliveryChannel.SMS },
                            NewPriority = NotificationPriority.Critical,
                            CustomMessage = "CRITICAL: This approval has been pending for 72 hours"
                        }
                    }
                },
                new EscalationRule
                {
                    Name = "Critical Alert Escalation",
                    Description = "Escalate unacknowledged critical alerts",
                    TriggerTypes = new() { NotificationType.Alert },
                    MinPriority = NotificationPriority.High,
                    EscalateAfter = TimeSpan.FromHours(4),
                    MaxEscalationLevel = 2,
                    Levels = new()
                    {
                        new EscalationLevel
                        {
                            Level = 1,
                            EscalateAfter = TimeSpan.FromHours(4),
                            NotifyRoles = new() { UserRole.ProjectManager },
                            AdditionalChannels = new() { DeliveryChannel.SMS },
                            NewPriority = NotificationPriority.Urgent
                        },
                        new EscalationLevel
                        {
                            Level = 2,
                            EscalateAfter = TimeSpan.FromHours(8),
                            NotifyRoles = new() { UserRole.Administrator, UserRole.Executive },
                            AdditionalChannels = new() { DeliveryChannel.SMS },
                            NewPriority = NotificationPriority.Critical
                        }
                    }
                }
            };

            foreach (var rule in rules)
            {
                _escalationRules[rule.RuleId] = rule;
            }
        }

        #endregion

        #region Send Notifications

        /// <summary>
        /// Send a notification with smart routing
        /// </summary>
        public async Task<Notification> SendNotificationAsync(
            Notification notification,
            CancellationToken ct = default)
        {
            notification.NotificationId = Guid.NewGuid().ToString();
            notification.CreatedAt = DateTime.UtcNow;

            // Calculate priority based on multiple factors
            notification.CalculatedPriority = CalculatePriority(notification);

            // Apply grouping if batchable
            if (notification.IsBatchable && !string.IsNullOrEmpty(notification.GroupKey))
            {
                ApplyGrouping(notification);
            }

            _notifications[notification.NotificationId] = notification;

            // Queue for processing
            await _notificationQueue.Writer.WriteAsync(notification, ct);

            return notification;
        }

        /// <summary>
        /// Send bulk notifications efficiently
        /// </summary>
        public async Task<List<Notification>> SendBulkNotificationsAsync(
            List<Notification> notifications,
            CancellationToken ct = default)
        {
            var results = new List<Notification>();
            var tasks = new List<Task<Notification>>();

            foreach (var notification in notifications)
            {
                tasks.Add(SendNotificationAsync(notification, ct));
            }

            var completed = await Task.WhenAll(tasks);
            results.AddRange(completed);

            return results;
        }

        /// <summary>
        /// Send notification from a template
        /// </summary>
        public async Task<Notification> SendFromTemplateAsync(
            string templateId,
            string senderId,
            List<string> recipientIds,
            Dictionary<string, string> placeholders,
            string? projectId = null,
            string? entityType = null,
            string? entityId = null,
            string? actionUrl = null,
            CancellationToken ct = default)
        {
            if (!_templates.TryGetValue(templateId, out var template))
                throw new InvalidOperationException($"Template {templateId} not found");

            var notification = new Notification
            {
                ProjectId = projectId ?? string.Empty,
                Type = template.Type,
                Priority = template.DefaultPriority,
                Title = ApplyPlaceholders(template.TitleTemplate, placeholders),
                Message = ApplyPlaceholders(template.MessageTemplate, placeholders),
                HtmlMessage = template.HtmlTemplate != null
                    ? ApplyPlaceholders(template.HtmlTemplate, placeholders)
                    : null,
                SenderId = senderId,
                RecipientIds = recipientIds,
                Channels = template.DefaultChannels,
                EntityType = entityType,
                EntityId = entityId,
                ActionUrl = actionUrl,
                TemplateId = templateId,
                RequiresAcknowledgement = template.RequiresAcknowledgement,
                ExpiresAt = template.AutoExpireAfter.HasValue
                    ? DateTime.UtcNow.Add(template.AutoExpireAfter.Value)
                    : null
            };

            return await SendNotificationAsync(notification, ct);
        }

        private string ApplyPlaceholders(string template, Dictionary<string, string> placeholders)
        {
            var result = template;
            foreach (var (key, value) in placeholders)
            {
                result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty);
            }
            // Remove any remaining placeholders
            result = Regex.Replace(result, @"\{\{[^}]+\}\}", string.Empty);
            return result;
        }

        private NotificationPriority CalculatePriority(Notification notification)
        {
            var basePriority = notification.Priority;

            // Factor in notification type
            var typePriority = notification.Type switch
            {
                NotificationType.Alert or NotificationType.Error => NotificationPriority.High,
                NotificationType.Escalation => NotificationPriority.Urgent,
                NotificationType.ActionRequired or NotificationType.Approval => NotificationPriority.High,
                NotificationType.Deadline when IsUrgentDeadline(notification) => NotificationPriority.Urgent,
                NotificationType.ClashDetected => NotificationPriority.High,
                NotificationType.Assignment or NotificationType.Mention => NotificationPriority.Normal,
                NotificationType.Comment or NotificationType.Update or NotificationType.Info => NotificationPriority.Low,
                _ => NotificationPriority.Normal
            };

            // Factor in importance flag
            if (notification.IsImportant)
            {
                typePriority = (NotificationPriority)Math.Min((int)typePriority + 1, (int)NotificationPriority.Critical);
            }

            // Factor in requires acknowledgement
            if (notification.RequiresAcknowledgement)
            {
                typePriority = (NotificationPriority)Math.Min((int)typePriority + 1, (int)NotificationPriority.Critical);
            }

            // Return the higher of base and calculated priority
            return (NotificationPriority)Math.Max((int)basePriority, (int)typePriority);
        }

        private bool IsUrgentDeadline(Notification notification)
        {
            if (notification.Data.TryGetValue("dueDate", out var dueDateObj))
            {
                if (dueDateObj is DateTime dueDate)
                {
                    return (dueDate - DateTime.UtcNow).TotalHours <= 24;
                }
            }
            return false;
        }

        private void ApplyGrouping(Notification notification)
        {
            if (!_groups.TryGetValue(notification.GroupKey!, out var group))
            {
                group = new NotificationGroup
                {
                    GroupKey = notification.GroupKey!,
                    GroupType = notification.Type.ToString(),
                    Title = notification.Title,
                    FirstAt = notification.CreatedAt,
                    HighestPriority = notification.CalculatedPriority,
                    EntityType = notification.EntityType,
                    EntityId = notification.EntityId
                };
                _groups[notification.GroupKey!] = group;
            }

            group.NotificationIds.Add(notification.NotificationId);
            group.Count = group.NotificationIds.Count;
            group.LastAt = notification.CreatedAt;

            if (notification.CalculatedPriority > group.HighestPriority)
            {
                group.HighestPriority = notification.CalculatedPriority;
            }

            group.Summary = $"{group.Count} {group.GroupType} notifications";
        }

        #endregion

        #region Processing

        private async Task ProcessNotificationsAsync(CancellationToken ct)
        {
            await foreach (var notification in _notificationQueue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await DeliverNotificationAsync(notification, ct);
                    NotificationSent?.Invoke(this, new NotificationSentEventArgs(notification));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to deliver notification {notification.NotificationId}: {ex.Message}");
                }
            }
        }

        private async Task DeliverNotificationAsync(Notification notification, CancellationToken ct)
        {
            await _deliverySemaphore.WaitAsync(ct);
            try
            {
                var deliveries = new List<NotificationDelivery>();

                foreach (var recipientId in notification.RecipientIds)
                {
                    var preferences = GetPreferences(recipientId);

                    // Check if globally disabled or muted
                    if (!preferences.GlobalEnabled) continue;
                    if (preferences.ProjectMuted.TryGetValue(notification.ProjectId, out var muted) && muted) continue;
                    if (preferences.MutedSenderIds.Contains(notification.SenderId)) continue;
                    if (notification.ThreadId != null && preferences.MutedThreadIds.Contains(notification.ThreadId)) continue;

                    // Check rate limits
                    if (!CheckRateLimits(recipientId, preferences)) continue;

                    var channels = DetermineChannels(notification, preferences);

                    foreach (var channel in channels)
                    {
                        var delivery = new NotificationDelivery
                        {
                            NotificationId = notification.NotificationId,
                            RecipientId = recipientId,
                            Channel = channel,
                            QueuedAt = DateTime.UtcNow
                        };

                        await DeliverToChannelAsync(delivery, notification, preferences, ct);
                        deliveries.Add(delivery);

                        if (delivery.Status == NotificationStatus.Delivered)
                        {
                            NotificationDelivered?.Invoke(this, new NotificationDeliveredEventArgs(notification, delivery));
                        }
                    }

                    IncrementRateCounts(recipientId, deliveries.Count(d => d.Channel == DeliveryChannel.Email));
                }

                _deliveries[notification.NotificationId] = deliveries;
                AddHistory(notification.NotificationId, "sent", $"Delivered to {deliveries.Count} channels");
            }
            finally
            {
                _deliverySemaphore.Release();
            }
        }

        private bool CheckRateLimits(string userId, NotificationPreferences preferences)
        {
            var hourlyKey = $"{userId}_{DateTime.UtcNow:yyyyMMddHH}";
            var dailyKey = $"{userId}_{DateTime.UtcNow:yyyyMMdd}";

            if (_userHourlyCount.TryGetValue(hourlyKey, out var hourlyCount))
            {
                if (hourlyCount >= preferences.MaxNotificationsPerHour) return false;
            }

            return true;
        }

        private void IncrementRateCounts(string userId, int emailCount)
        {
            var hourlyKey = $"{userId}_{DateTime.UtcNow:yyyyMMddHH}";
            var dailyKey = $"{userId}_{DateTime.UtcNow:yyyyMMdd}";

            _userHourlyCount.AddOrUpdate(hourlyKey, 1, (_, count) => count + 1);
            if (emailCount > 0)
            {
                _userDailyEmailCount.AddOrUpdate(dailyKey, emailCount, (_, count) => count + emailCount);
            }
        }

        private List<DeliveryChannel> DetermineChannels(Notification notification, NotificationPreferences preferences)
        {
            var channels = new List<DeliveryChannel>();
            var isQuietHours = IsInQuietHours(preferences);
            var overrideQuiet = notification.CalculatedPriority >= NotificationPriority.Urgent &&
                               preferences.OverrideQuietHoursForUrgent;

            // Always check InApp first
            if (notification.Channels.Contains(DeliveryChannel.InApp))
            {
                channels.Add(DeliveryChannel.InApp);
            }

            // During quiet hours, only allow InApp unless urgent override
            if (isQuietHours && !overrideQuiet)
            {
                return channels;
            }

            // Check type-specific preferences
            if (preferences.TypePreferences.TryGetValue(notification.Type, out var typePrefs))
            {
                if (!typePrefs.Enabled) return channels;
                if (notification.CalculatedPriority < typePrefs.MinPriority) return channels;

                if (typePrefs.Email && preferences.EmailEnabled && notification.Channels.Contains(DeliveryChannel.Email))
                    channels.Add(DeliveryChannel.Email);
                if (typePrefs.Push && preferences.PushEnabled && notification.Channels.Contains(DeliveryChannel.Push))
                    channels.Add(DeliveryChannel.Push);
                if (typePrefs.Sms && preferences.SmsEnabled && notification.Channels.Contains(DeliveryChannel.SMS))
                    channels.Add(DeliveryChannel.SMS);
                if (typePrefs.Slack && preferences.SlackEnabled && notification.Channels.Contains(DeliveryChannel.Slack))
                    channels.Add(DeliveryChannel.Slack);
                if (typePrefs.Teams && preferences.TeamsEnabled && notification.Channels.Contains(DeliveryChannel.Teams))
                    channels.Add(DeliveryChannel.Teams);
            }
            else
            {
                // Use global preferences
                foreach (var channel in notification.Channels)
                {
                    var enabled = channel switch
                    {
                        DeliveryChannel.Email => preferences.EmailEnabled,
                        DeliveryChannel.Push => preferences.PushEnabled,
                        DeliveryChannel.SMS => preferences.SmsEnabled,
                        DeliveryChannel.Slack => preferences.SlackEnabled,
                        DeliveryChannel.Teams => preferences.TeamsEnabled,
                        DeliveryChannel.Desktop => preferences.DesktopEnabled,
                        DeliveryChannel.Webhook => preferences.WebhookEnabled,
                        _ => true
                    };

                    if (enabled && !channels.Contains(channel))
                    {
                        channels.Add(channel);
                    }
                }
            }

            return channels.Distinct().ToList();
        }

        private bool IsInQuietHours(NotificationPreferences preferences)
        {
            if (!preferences.QuietHoursStart.HasValue || !preferences.QuietHoursEnd.HasValue)
                return false;

            var now = DateTime.UtcNow.TimeOfDay;
            var start = preferences.QuietHoursStart.Value;
            var end = preferences.QuietHoursEnd.Value;

            // Check quiet days
            if (preferences.QuietDays?.Contains(DateTime.UtcNow.DayOfWeek) == true)
                return true;

            if (start < end)
            {
                return now >= start && now <= end;
            }
            else
            {
                return now >= start || now <= end;
            }
        }

        private async Task DeliverToChannelAsync(
            NotificationDelivery delivery,
            Notification notification,
            NotificationPreferences preferences,
            CancellationToken ct)
        {
            delivery.AttemptCount++;
            delivery.LastAttemptAt = DateTime.UtcNow;

            try
            {
                // Simulate delivery - in production, integrate with actual services
                await Task.Delay(20, ct);

                delivery.Status = NotificationStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;

                // Simulate delivery confirmation
                await Task.Delay(20, ct);
                delivery.Status = NotificationStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                delivery.Status = NotificationStatus.Failed;
                delivery.Error = ex.Message;
                delivery.ErrorCode = "DELIVERY_FAILED";

                // Schedule retry if under limit
                if (delivery.AttemptCount < 3)
                {
                    delivery.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, delivery.AttemptCount));
                }
            }
        }

        #endregion

        #region Query and Read

        /// <summary>
        /// Get notifications based on query parameters
        /// </summary>
        public NotificationResult GetNotifications(NotificationQuery query)
        {
            var notifications = _notifications.Values
                .Where(n => n.RecipientIds.Contains(query.UserId))
                .Where(n => query.ProjectId == null || n.ProjectId == query.ProjectId)
                .Where(n => query.Types == null || query.Types.Contains(n.Type))
                .Where(n => query.MinPriority == null || n.CalculatedPriority >= query.MinPriority)
                .Where(n => query.MaxPriority == null || n.CalculatedPriority <= query.MaxPriority)
                .Where(n => query.FromDate == null || n.CreatedAt >= query.FromDate)
                .Where(n => query.ToDate == null || n.CreatedAt <= query.ToDate)
                .Where(n => query.GroupKey == null || n.GroupKey == query.GroupKey)
                .Where(n => query.ThreadId == null || n.ThreadId == query.ThreadId)
                .Where(n => query.SenderId == null || n.SenderId == query.SenderId)
                .Where(n => query.EntityType == null || n.EntityType == query.EntityType)
                .Where(n => query.EntityId == null || n.EntityId == query.EntityId)
                .Where(n => query.IncludeExpired || n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow);

            if (query.ImportantOnly)
            {
                notifications = notifications.Where(n => n.IsImportant);
            }

            if (query.ActionRequiredOnly)
            {
                notifications = notifications.Where(n => n.RequiresAcknowledgement);
            }

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var search = query.SearchText.ToLowerInvariant();
                notifications = notifications.Where(n =>
                    n.Title.ToLowerInvariant().Contains(search) ||
                    n.Message.ToLowerInvariant().Contains(search));
            }

            if (query.Tags?.Any() == true)
            {
                notifications = notifications.Where(n =>
                    n.Tags != null && n.Tags.Any(t => query.Tags.Contains(t)));
            }

            // Get deliveries for filtering read status
            var deliveriesForUser = new List<NotificationDelivery>();
            foreach (var notification in notifications)
            {
                if (_deliveries.TryGetValue(notification.NotificationId, out var deliveries))
                {
                    deliveriesForUser.AddRange(deliveries.Where(d => d.RecipientId == query.UserId));
                }
            }

            if (query.UnreadOnly)
            {
                var unreadIds = deliveriesForUser
                    .Where(d => d.Status != NotificationStatus.Read && d.Status != NotificationStatus.Dismissed)
                    .Select(d => d.NotificationId)
                    .ToHashSet();

                notifications = notifications.Where(n => unreadIds.Contains(n.NotificationId));
            }

            if (!query.IncludeDismissed)
            {
                var dismissedIds = deliveriesForUser
                    .Where(d => d.Status == NotificationStatus.Dismissed)
                    .Select(d => d.NotificationId)
                    .ToHashSet();

                notifications = notifications.Where(n => !dismissedIds.Contains(n.NotificationId));
            }

            var notificationList = notifications.ToList();
            var totalCount = notificationList.Count;

            // Sort
            notificationList = query.SortBy switch
            {
                "Priority" => query.SortDescending
                    ? notificationList.OrderByDescending(n => n.CalculatedPriority).ThenByDescending(n => n.CreatedAt).ToList()
                    : notificationList.OrderBy(n => n.CalculatedPriority).ThenBy(n => n.CreatedAt).ToList(),
                "Type" => query.SortDescending
                    ? notificationList.OrderByDescending(n => n.Type).ThenByDescending(n => n.CreatedAt).ToList()
                    : notificationList.OrderBy(n => n.Type).ThenBy(n => n.CreatedAt).ToList(),
                _ => query.SortDescending
                    ? notificationList.OrderByDescending(n => n.CreatedAt).ToList()
                    : notificationList.OrderBy(n => n.CreatedAt).ToList()
            };

            var pagedNotifications = notificationList.Skip(query.Skip).Take(query.Take).ToList();

            // Build groups if applicable
            var groups = pagedNotifications
                .Where(n => !string.IsNullOrEmpty(n.GroupKey))
                .GroupBy(n => n.GroupKey)
                .Select(g => _groups.TryGetValue(g.Key!, out var group) ? group : null)
                .Where(g => g != null)
                .Cast<NotificationGroup>()
                .ToList();

            return new NotificationResult
            {
                Notifications = pagedNotifications,
                Deliveries = deliveriesForUser,
                Groups = groups.Any() ? groups : null,
                TotalCount = totalCount,
                UnreadCount = deliveriesForUser.Count(d => d.Status != NotificationStatus.Read),
                ActionRequiredCount = notificationList.Count(n => n.RequiresAcknowledgement),
                HasMore = totalCount > query.Skip + query.Take,
                Skip = query.Skip,
                Take = query.Take
            };
        }

        /// <summary>
        /// Get notification statistics for a user
        /// </summary>
        public NotificationStats GetNotificationStats(string userId)
        {
            var userNotifications = _notifications.Values
                .Where(n => n.RecipientIds.Contains(userId))
                .ToList();

            var allDeliveries = new List<NotificationDelivery>();
            foreach (var notification in userNotifications)
            {
                if (_deliveries.TryGetValue(notification.NotificationId, out var deliveries))
                {
                    allDeliveries.AddRange(deliveries.Where(d => d.RecipientId == userId));
                }
            }

            var unreadDeliveries = allDeliveries
                .Where(d => d.Status != NotificationStatus.Read && d.Status != NotificationStatus.Dismissed)
                .ToList();

            var unreadNotificationIds = unreadDeliveries.Select(d => d.NotificationId).ToHashSet();
            var unreadNotifications = userNotifications
                .Where(n => unreadNotificationIds.Contains(n.NotificationId))
                .ToList();

            var today = DateTime.UtcNow.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);

            return new NotificationStats
            {
                UserId = userId,
                TotalUnread = unreadNotifications.Count,
                TotalToday = userNotifications.Count(n => n.CreatedAt.Date == today),
                TotalThisWeek = userNotifications.Count(n => n.CreatedAt >= weekStart),
                UnreadByType = unreadNotifications.GroupBy(n => n.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UnreadByPriority = unreadNotifications.GroupBy(n => n.CalculatedPriority)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UnreadByProject = unreadNotifications.Where(n => !string.IsNullOrEmpty(n.ProjectId))
                    .GroupBy(n => n.ProjectId)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ActionRequired = unreadNotifications.Count(n => n.RequiresAcknowledgement),
                AwaitingAcknowledgement = unreadNotifications.Count(n =>
                    n.RequiresAcknowledgement &&
                    !allDeliveries.Any(d => d.NotificationId == n.NotificationId && d.AcknowledgedAt.HasValue)),
                LastNotificationAt = userNotifications.OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefault()?.CreatedAt,
                LastReadAt = allDeliveries.Where(d => d.ReadAt.HasValue)
                    .OrderByDescending(d => d.ReadAt)
                    .FirstOrDefault()?.ReadAt,
                EscalatedCount = userNotifications.Count(n => n.EscalationLevel > 0)
            };
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        public int GetUnreadCount(string userId)
        {
            return GetNotificationStats(userId).TotalUnread;
        }

        /// <summary>
        /// Get notification history for a specific notification
        /// </summary>
        public List<NotificationHistory> GetNotificationHistory(string notificationId)
        {
            return _history.TryGetValue(notificationId, out var history)
                ? history.OrderByDescending(h => h.Timestamp).ToList()
                : new List<NotificationHistory>();
        }

        #endregion

        #region Mark As Read

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        public async Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_deliveries.TryGetValue(notificationId, out var deliveries))
            {
                foreach (var delivery in deliveries.Where(d => d.RecipientId == userId))
                {
                    delivery.Status = NotificationStatus.Read;
                    delivery.ReadAt = DateTime.UtcNow;
                }

                AddHistory(notificationId, "read", $"Read by {userId}");

                if (_notifications.TryGetValue(notificationId, out var notification))
                {
                    NotificationRead?.Invoke(this, new NotificationReadEventArgs(notification, userId));
                }
            }
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        public async Task MarkAllAsReadAsync(string userId, string? projectId = null, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            var notifications = _notifications.Values
                .Where(n => n.RecipientIds.Contains(userId))
                .Where(n => projectId == null || n.ProjectId == projectId);

            foreach (var notification in notifications)
            {
                if (_deliveries.TryGetValue(notification.NotificationId, out var deliveries))
                {
                    foreach (var delivery in deliveries.Where(d => d.RecipientId == userId))
                    {
                        if (delivery.Status != NotificationStatus.Read)
                        {
                            delivery.Status = NotificationStatus.Read;
                            delivery.ReadAt = DateTime.UtcNow;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dismiss a notification
        /// </summary>
        public async Task DismissNotificationAsync(string notificationId, string userId, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_deliveries.TryGetValue(notificationId, out var deliveries))
            {
                foreach (var delivery in deliveries.Where(d => d.RecipientId == userId))
                {
                    delivery.Status = NotificationStatus.Dismissed;
                    delivery.DismissedAt = DateTime.UtcNow;
                }

                AddHistory(notificationId, "dismissed", $"Dismissed by {userId}");
            }
        }

        /// <summary>
        /// Acknowledge a notification
        /// </summary>
        public async Task AcknowledgeNotificationAsync(string notificationId, string userId, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_deliveries.TryGetValue(notificationId, out var deliveries))
            {
                foreach (var delivery in deliveries.Where(d => d.RecipientId == userId))
                {
                    delivery.AcknowledgedAt = DateTime.UtcNow;
                }

                AddHistory(notificationId, "acknowledged", $"Acknowledged by {userId}");
            }
        }

        #endregion

        #region Preferences

        /// <summary>
        /// Get user preferences
        /// </summary>
        public NotificationPreferences GetPreferences(string userId)
        {
            if (!_preferences.TryGetValue(userId, out var prefs))
            {
                prefs = new NotificationPreferences { UserId = userId };
                _preferences[userId] = prefs;
            }
            return prefs;
        }

        /// <summary>
        /// Update user preferences
        /// </summary>
        public async Task<NotificationPreferences> UpdatePreferencesAsync(
            string userId,
            Action<NotificationPreferences> updateAction,
            CancellationToken ct = default)
        {
            await Task.CompletedTask;

            var prefs = GetPreferences(userId);
            updateAction(prefs);
            prefs.UpdatedAt = DateTime.UtcNow;
            return prefs;
        }

        /// <summary>
        /// Mute notifications for a project
        /// </summary>
        public async Task MuteProjectAsync(string userId, string projectId, CancellationToken ct = default)
        {
            await UpdatePreferencesAsync(userId, prefs =>
            {
                prefs.ProjectMuted[projectId] = true;
            }, ct);
        }

        /// <summary>
        /// Unmute notifications for a project
        /// </summary>
        public async Task UnmuteProjectAsync(string userId, string projectId, CancellationToken ct = default)
        {
            await UpdatePreferencesAsync(userId, prefs =>
            {
                prefs.ProjectMuted.Remove(projectId);
            }, ct);
        }

        /// <summary>
        /// Mute a thread
        /// </summary>
        public async Task MuteThreadAsync(string userId, string threadId, CancellationToken ct = default)
        {
            await UpdatePreferencesAsync(userId, prefs =>
            {
                if (!prefs.MutedThreadIds.Contains(threadId))
                {
                    prefs.MutedThreadIds.Add(threadId);
                }
            }, ct);
        }

        #endregion

        #region Digest

        /// <summary>
        /// Create a digest for a user
        /// </summary>
        public async Task<NotificationDigest> CreateDigestAsync(
            string userId,
            DigestFrequency frequency,
            CancellationToken ct = default)
        {
            await Task.CompletedTask;

            var periodEnd = DateTime.UtcNow;
            var periodStart = frequency switch
            {
                DigestFrequency.Hourly => periodEnd.AddHours(-1),
                DigestFrequency.Daily => periodEnd.AddDays(-1),
                DigestFrequency.Weekly => periodEnd.AddDays(-7),
                DigestFrequency.Monthly => periodEnd.AddMonths(-1),
                _ => periodEnd.AddDays(-1)
            };

            var notifications = _notifications.Values
                .Where(n => n.RecipientIds.Contains(userId))
                .Where(n => n.CreatedAt >= periodStart && n.CreatedAt <= periodEnd)
                .OrderByDescending(n => n.CalculatedPriority)
                .ThenByDescending(n => n.CreatedAt)
                .ToList();

            var deliveries = new List<NotificationDelivery>();
            foreach (var notification in notifications)
            {
                if (_deliveries.TryGetValue(notification.NotificationId, out var del))
                {
                    deliveries.AddRange(del.Where(d => d.RecipientId == userId));
                }
            }

            var sections = notifications
                .GroupBy(n => n.Type)
                .Select(g => new DigestSection
                {
                    Title = GetTypeDisplayName(g.Key),
                    Type = g.Key,
                    TotalCount = g.Count(),
                    DisplayedCount = Math.Min(g.Count(), 5),
                    Priority = GetTypePriority(g.Key),
                    Icon = GetTypeIcon(g.Key),
                    Color = GetTypeColor(g.Key),
                    Items = g.Take(5).Select(n => new DigestItem
                    {
                        NotificationId = n.NotificationId,
                        Title = n.Title,
                        Summary = n.Message.Length > 100 ? n.Message[..100] + "..." : n.Message,
                        SenderName = n.SenderName,
                        ActionUrl = n.ActionUrl,
                        Timestamp = n.CreatedAt,
                        Priority = n.CalculatedPriority,
                        IsImportant = n.IsImportant,
                        IsRead = deliveries.Any(d =>
                            d.NotificationId == n.NotificationId && d.Status == NotificationStatus.Read)
                    }).ToList()
                })
                .OrderByDescending(s => s.Priority)
                .ToList();

            var highlights = notifications
                .Where(n => n.CalculatedPriority >= NotificationPriority.High || n.IsImportant)
                .Take(3)
                .Select(n => new DigestHighlight
                {
                    Title = n.Title,
                    Message = n.Message.Length > 50 ? n.Message[..50] + "..." : n.Message,
                    Type = n.Type,
                    Priority = n.CalculatedPriority,
                    ActionUrl = n.ActionUrl
                })
                .ToList();

            var digest = new NotificationDigest
            {
                UserId = userId,
                Frequency = frequency,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Sections = sections,
                TotalNotifications = notifications.Count,
                UnreadCount = deliveries.Count(d => d.Status != NotificationStatus.Read),
                ActionRequiredCount = notifications.Count(n => n.RequiresAcknowledgement),
                HighPriorityCount = notifications.Count(n => n.CalculatedPriority >= NotificationPriority.High),
                Highlights = highlights,
                HtmlContent = GenerateDigestHtml(sections, highlights),
                PlainTextContent = GenerateDigestText(sections)
            };

            _digests[digest.DigestId] = digest;
            DigestGenerated?.Invoke(this, new DigestGeneratedEventArgs(digest));

            return digest;
        }

        /// <summary>
        /// Get user's most recent digest
        /// </summary>
        public NotificationDigest? GetUserDigest(string userId, DigestFrequency? frequency = null)
        {
            return _digests.Values
                .Where(d => d.UserId == userId)
                .Where(d => frequency == null || d.Frequency == frequency)
                .OrderByDescending(d => d.GeneratedAt)
                .FirstOrDefault();
        }

        private string GenerateDigestHtml(List<DigestSection> sections, List<DigestHighlight>? highlights)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body>");
            sb.AppendLine("<h1>Notification Digest</h1>");

            if (highlights?.Any() == true)
            {
                sb.AppendLine("<div class=\"highlights\">");
                sb.AppendLine("<h2>Highlights</h2>");
                foreach (var highlight in highlights)
                {
                    sb.AppendLine($"<div class=\"highlight {highlight.Priority}\">");
                    sb.AppendLine($"<strong>{highlight.Title}</strong>");
                    sb.AppendLine($"<p>{highlight.Message}</p>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            foreach (var section in sections)
            {
                sb.AppendLine($"<div class=\"section\">");
                sb.AppendLine($"<h2>{section.Title} ({section.TotalCount})</h2>");
                sb.AppendLine("<ul>");
                foreach (var item in section.Items)
                {
                    sb.AppendLine($"<li><a href=\"{item.ActionUrl}\">{item.Title}</a></li>");
                }
                sb.AppendLine("</ul>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerateDigestText(List<DigestSection> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Notification Digest");
            sb.AppendLine(new string('=', 50));

            foreach (var section in sections)
            {
                sb.AppendLine();
                sb.AppendLine($"{section.Title} ({section.TotalCount})");
                sb.AppendLine(new string('-', 30));
                foreach (var item in section.Items)
                {
                    sb.AppendLine($"  * {item.Title}");
                }
            }

            return sb.ToString();
        }

        private string GetTypeDisplayName(NotificationType type) => type switch
        {
            NotificationType.ActionRequired => "Action Required",
            NotificationType.ClashDetected => "Clash Detection",
            NotificationType.ModelSync => "Model Sync",
            _ => type.ToString()
        };

        private int GetTypePriority(NotificationType type) => type switch
        {
            NotificationType.ActionRequired or NotificationType.Approval => 100,
            NotificationType.Alert or NotificationType.Escalation => 90,
            NotificationType.Deadline => 80,
            NotificationType.ClashDetected => 75,
            NotificationType.Assignment => 70,
            NotificationType.Mention => 60,
            NotificationType.Comment => 50,
            _ => 0
        };

        private string GetTypeIcon(NotificationType type) => type switch
        {
            NotificationType.Alert => "warning",
            NotificationType.Approval => "check-circle",
            NotificationType.Deadline => "clock",
            NotificationType.Mention => "at",
            NotificationType.Comment => "message",
            _ => "bell"
        };

        private string GetTypeColor(NotificationType type) => type switch
        {
            NotificationType.Alert or NotificationType.Escalation => "#dc3545",
            NotificationType.Approval or NotificationType.ActionRequired => "#fd7e14",
            NotificationType.Deadline => "#ffc107",
            NotificationType.Milestone or NotificationType.Completion => "#28a745",
            NotificationType.Info => "#17a2b8",
            _ => "#6c757d"
        };

        private async Task ProcessDigestsAsync(CancellationToken ct)
        {
            await foreach (var request in _digestQueue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await CreateDigestAsync(request.UserId, request.Frequency, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to generate digest for {request.UserId}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Escalation

        /// <summary>
        /// Add an escalation rule
        /// </summary>
        public async Task<EscalationRule> AddEscalationRuleAsync(EscalationRule rule, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            _escalationRules[rule.RuleId] = rule;
            return rule;
        }

        /// <summary>
        /// Manually escalate a notification
        /// </summary>
        public async Task<Notification> EscalateNotificationAsync(
            string notificationId,
            string? customMessage = null,
            CancellationToken ct = default)
        {
            if (!_notifications.TryGetValue(notificationId, out var notification))
                throw new InvalidOperationException("Notification not found");

            notification.EscalationLevel++;
            notification.LastEscalatedAt = DateTime.UtcNow;

            var escalatedNotification = new Notification
            {
                ProjectId = notification.ProjectId,
                Type = NotificationType.Escalation,
                Priority = NotificationPriority.Urgent,
                CalculatedPriority = NotificationPriority.Urgent,
                Title = $"[ESCALATED Level {notification.EscalationLevel}] {notification.Title}",
                Message = customMessage ?? $"This item has been escalated to level {notification.EscalationLevel} and requires immediate attention.",
                SenderId = "system",
                RecipientIds = notification.RecipientIds,
                Channels = new() { DeliveryChannel.InApp, DeliveryChannel.Email, DeliveryChannel.Push, DeliveryChannel.SMS },
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                ActionUrl = notification.ActionUrl,
                ParentNotificationId = notificationId,
                RequiresAcknowledgement = true,
                Data = new Dictionary<string, object>
                {
                    ["originalNotificationId"] = notificationId,
                    ["escalationLevel"] = notification.EscalationLevel
                }
            };

            var result = await SendNotificationAsync(escalatedNotification, ct);

            AddHistory(notificationId, "escalated", $"Escalated to level {notification.EscalationLevel}");
            NotificationEscalated?.Invoke(this, new NotificationEscalatedEventArgs(notification, notification.EscalationLevel));

            return result;
        }

        private async Task ProcessEscalationsAsync(CancellationToken ct)
        {
            await foreach (var check in _escalationQueue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await EscalateNotificationAsync(check.NotificationId, check.Message, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to escalate {check.NotificationId}: {ex.Message}");
                }
            }
        }

        private void CheckEscalations(object? state)
        {
            foreach (var rule in _escalationRules.Values.Where(r => r.IsActive))
            {
                var notifications = _notifications.Values
                    .Where(n => rule.TriggerTypes.Contains(n.Type))
                    .Where(n => rule.MinPriority == null || n.CalculatedPriority >= rule.MinPriority)
                    .Where(n => n.RequiresAcknowledgement)
                    .Where(n => n.EscalationLevel < rule.MaxEscalationLevel);

                foreach (var notification in notifications)
                {
                    var deliveries = _deliveries.TryGetValue(notification.NotificationId, out var del) ? del : new List<NotificationDelivery>();
                    var isAcknowledged = deliveries.Any(d => d.AcknowledgedAt.HasValue);

                    if (!isAcknowledged)
                    {
                        var currentLevel = rule.Levels.FirstOrDefault(l => l.Level == notification.EscalationLevel + 1);
                        if (currentLevel != null)
                        {
                            var timeSinceCreation = DateTime.UtcNow - notification.CreatedAt;
                            var timeSinceLastEscalation = notification.LastEscalatedAt.HasValue
                                ? DateTime.UtcNow - notification.LastEscalatedAt.Value
                                : timeSinceCreation;

                            if (timeSinceLastEscalation >= currentLevel.EscalateAfter)
                            {
                                _escalationQueue.Writer.TryWrite(new EscalationCheck
                                {
                                    NotificationId = notification.NotificationId,
                                    RuleId = rule.RuleId,
                                    Level = currentLevel.Level,
                                    Message = currentLevel.CustomMessage
                                });
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Templates

        /// <summary>
        /// Get all notification templates
        /// </summary>
        public List<NotificationTemplate> GetTemplates(string? category = null)
        {
            return _templates.Values
                .Where(t => t.IsActive)
                .Where(t => category == null || t.Category == category)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Name)
                .ToList();
        }

        /// <summary>
        /// Get template by ID
        /// </summary>
        public NotificationTemplate? GetTemplate(string templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        /// <summary>
        /// Create or update a template
        /// </summary>
        public async Task<NotificationTemplate> SaveTemplateAsync(NotificationTemplate template, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_templates.TryGetValue(template.TemplateId, out var existing) && existing.IsSystem)
            {
                throw new InvalidOperationException("Cannot modify system templates");
            }

            template.UpdatedAt = DateTime.UtcNow;
            _templates[template.TemplateId] = template;
            return template;
        }

        /// <summary>
        /// Delete a template
        /// </summary>
        public async Task DeleteTemplateAsync(string templateId, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_templates.TryGetValue(templateId, out var template) && template.IsSystem)
            {
                throw new InvalidOperationException("Cannot delete system templates");
            }

            _templates.TryRemove(templateId, out _);
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get system statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var allDeliveries = _deliveries.Values.SelectMany(d => d).ToList();

            return new Dictionary<string, object>
            {
                ["TotalNotifications"] = _notifications.Count,
                ["TotalDeliveries"] = allDeliveries.Count,
                ["PendingDeliveries"] = allDeliveries.Count(d => d.Status == NotificationStatus.Pending),
                ["SentDeliveries"] = allDeliveries.Count(d => d.Status == NotificationStatus.Sent),
                ["DeliveredDeliveries"] = allDeliveries.Count(d => d.Status == NotificationStatus.Delivered),
                ["ReadDeliveries"] = allDeliveries.Count(d => d.Status == NotificationStatus.Read),
                ["FailedDeliveries"] = allDeliveries.Count(d => d.Status == NotificationStatus.Failed),
                ["ActiveTemplates"] = _templates.Values.Count(t => t.IsActive),
                ["SystemTemplates"] = _templates.Values.Count(t => t.IsSystem),
                ["CustomTemplates"] = _templates.Values.Count(t => !t.IsSystem),
                ["ActiveEscalationRules"] = _escalationRules.Values.Count(r => r.IsActive),
                ["TotalDigests"] = _digests.Count,
                ["NotificationGroups"] = _groups.Count,
                ["RegisteredUsers"] = _preferences.Count
            };
        }

        #endregion

        #region Helper Methods

        private void ResetHourlyCounts(object? state)
        {
            _userHourlyCount.Clear();
        }

        private void ResetDailyCounts(object? state)
        {
            _userDailyEmailCount.Clear();
        }

        private void AddHistory(string notificationId, string action, string? details = null)
        {
            lock (_lockObject)
            {
                if (!_history.ContainsKey(notificationId))
                {
                    _history[notificationId] = new List<NotificationHistory>();
                }

                _history[notificationId].Add(new NotificationHistory
                {
                    NotificationId = notificationId,
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _processorCts.Cancel();
            _notificationQueue.Writer.Complete();
            _escalationQueue.Writer.Complete();
            _digestQueue.Writer.Complete();

            try
            {
                await Task.WhenAll(_notificationProcessor, _escalationProcessor, _digestProcessor);
            }
            catch (OperationCanceledException) { }

            _hourlyResetTimer.Dispose();
            _dailyResetTimer.Dispose();
            _escalationCheckTimer.Dispose();
            _processorCts.Dispose();
            _deliverySemaphore.Dispose();

            _notifications.Clear();
            _deliveries.Clear();
            _templates.Clear();
            _preferences.Clear();
            _groups.Clear();
            _escalationRules.Clear();
            _digests.Clear();
            _history.Clear();

            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Internal Models

    internal class EscalationCheck
    {
        public string NotificationId { get; set; } = string.Empty;
        public string RuleId { get; set; } = string.Empty;
        public int Level { get; set; }
        public string? Message { get; set; }
    }

    internal class DigestRequest
    {
        public string UserId { get; set; } = string.Empty;
        public DigestFrequency Frequency { get; set; }
    }

    #endregion

    #region Event Args

    public class NotificationSentEventArgs : EventArgs
    {
        public Notification Notification { get; }
        public NotificationSentEventArgs(Notification notification) => Notification = notification;
    }

    public class NotificationDeliveredEventArgs : EventArgs
    {
        public Notification Notification { get; }
        public NotificationDelivery Delivery { get; }
        public NotificationDeliveredEventArgs(Notification notification, NotificationDelivery delivery)
        {
            Notification = notification;
            Delivery = delivery;
        }
    }

    public class NotificationReadEventArgs : EventArgs
    {
        public Notification Notification { get; }
        public string UserId { get; }
        public NotificationReadEventArgs(Notification notification, string userId)
        {
            Notification = notification;
            UserId = userId;
        }
    }

    public class NotificationEscalatedEventArgs : EventArgs
    {
        public Notification Notification { get; }
        public int NewLevel { get; }
        public NotificationEscalatedEventArgs(Notification notification, int newLevel)
        {
            Notification = notification;
            NewLevel = newLevel;
        }
    }

    public class DigestGeneratedEventArgs : EventArgs
    {
        public NotificationDigest Digest { get; }
        public DigestGeneratedEventArgs(NotificationDigest digest) => Digest = digest;
    }

    #endregion
}
