// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagCollaborationManager.cs - Multi-user collaboration for concurrent tag editing
// Handles user sessions, workset coordination, conflict resolution, review workflows,
// standards enforcement, team analytics, and real-time communication.
//
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Collaboration
{
    #region Enumerations

    /// <summary>
    /// Role classification for users working with the tagging system.
    /// </summary>
    public enum TagUserRole
    {
        /// <summary>Owner of a tag set; full permissions including deletion.</summary>
        TagOwner,

        /// <summary>Can create and modify tags but cannot delete others' tags.</summary>
        TagEditor,

        /// <summary>Can review and approve/reject tags but not create them.</summary>
        TagReviewer,

        /// <summary>Full administrative access including standards override.</summary>
        TagAdmin
    }

    /// <summary>
    /// Review lifecycle states for tags undergoing collaborative review.
    /// </summary>
    public enum ReviewState
    {
        /// <summary>Tag is in initial draft state, not yet submitted for review.</summary>
        Draft,

        /// <summary>Tag has been submitted and is awaiting reviewer action.</summary>
        PendingReview,

        /// <summary>Tag has been approved by a reviewer.</summary>
        Approved,

        /// <summary>Tag has been rejected by a reviewer.</summary>
        Rejected,

        /// <summary>Reviewer requested changes; awaiting author revision.</summary>
        NeedsRevision
    }

    /// <summary>
    /// Types of conflicts that can occur during concurrent tag editing.
    /// </summary>
    public enum TagConflictType
    {
        /// <summary>Two users moved the same tag to different positions.</summary>
        PositionConflict,

        /// <summary>Two users changed the same tag's content expression or text.</summary>
        ContentConflict,

        /// <summary>Two users applied different styles/families to the same tag.</summary>
        StyleConflict,

        /// <summary>One user deleted a tag that another user modified.</summary>
        DeletionConflict,

        /// <summary>Two users edited tags in the same spatial region causing overlap.</summary>
        RegionConflict,

        /// <summary>Workset ownership prevents the requested operation.</summary>
        WorksetConflict
    }

    /// <summary>
    /// Strategy for resolving detected conflicts.
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>Most recent write overwrites previous changes.</summary>
        LastWriteWins,

        /// <summary>Attempt to merge non-overlapping changes automatically.</summary>
        MergeChanges,

        /// <summary>Prompt the user to choose which version to keep.</summary>
        PromptUser,

        /// <summary>Higher-role user's changes take priority.</summary>
        RoleBasedPriority,

        /// <summary>Escalate to a TagAdmin for resolution.</summary>
        Escalate
    }

    /// <summary>
    /// Notification event types broadcast to connected users.
    /// </summary>
    public enum TagNotificationType
    {
        TagCreated,
        TagMoved,
        TagDeleted,
        TagModified,
        TagReviewRequested,
        TagApproved,
        TagRejected,
        ConflictDetected,
        RegionLocked,
        RegionUnlocked,
        StandardViolation,
        CommentAdded,
        MentionReceived,
        IssueFlagged,
        WorksetBorrowed,
        WorksetReleased,
        SessionStarted,
        SessionEnded
    }

    /// <summary>
    /// Severity of a standards violation.
    /// </summary>
    public enum ViolationSeverity
    {
        /// <summary>Informational; does not block operations.</summary>
        Advisory,

        /// <summary>Should be corrected but does not block.</summary>
        Warning,

        /// <summary>Must be corrected; blocks approval.</summary>
        Error,

        /// <summary>Critical deviation; blocks all further edits until resolved.</summary>
        Critical
    }

    #endregion

    #region Supporting Data Classes

    /// <summary>
    /// Represents an active user session in the tagging collaboration system.
    /// </summary>
    public class UserSession
    {
        /// <summary>Unique session identifier.</summary>
        public string SessionId { get; set; }

        /// <summary>Authenticated user identifier (Revit username or SSO identity).</summary>
        public string UserId { get; set; }

        /// <summary>Display name for UI presentation.</summary>
        public string DisplayName { get; set; }

        /// <summary>Assigned role for this project context.</summary>
        public TagUserRole Role { get; set; }

        /// <summary>Revit view IDs currently open by this user.</summary>
        public List<int> ActiveViewIds { get; set; } = new List<int>();

        /// <summary>Element IDs of the user's current selection.</summary>
        public List<int> CurrentSelection { get; set; } = new List<int>();

        /// <summary>Tag IDs the user is actively editing (locked for this session).</summary>
        public HashSet<string> LockedTagIds { get; set; } = new HashSet<string>();

        /// <summary>Timestamp when the session was created.</summary>
        public DateTime SessionStart { get; set; }

        /// <summary>Last time any activity was recorded for this session.</summary>
        public DateTime LastActivity { get; set; }

        /// <summary>Whether the session is still active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Per-user tag placement preferences.</summary>
        public Dictionary<string, object> Preferences { get; set; } = new Dictionary<string, object>();

        /// <summary>Activity log entries for this session.</summary>
        public List<ActivityLogEntry> ActivityLog { get; set; } = new List<ActivityLogEntry>();

        /// <summary>Notification delivery preferences.</summary>
        public NotificationPreferences NotificationSettings { get; set; } = new NotificationPreferences();

        /// <summary>Workset IDs currently owned/borrowed by this user.</summary>
        public HashSet<int> OwnedWorksetIds { get; set; } = new HashSet<int>();
    }

    /// <summary>
    /// Timestamped record of a user action for audit trail.
    /// </summary>
    public class ActivityLogEntry
    {
        public string EntryId { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public string TagId { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
        public int ViewId { get; set; }
    }

    /// <summary>
    /// Per-user notification delivery preferences.
    /// </summary>
    public class NotificationPreferences
    {
        public bool NotifyOnTagCreated { get; set; } = true;
        public bool NotifyOnTagDeleted { get; set; } = true;
        public bool NotifyOnConflict { get; set; } = true;
        public bool NotifyOnReviewRequest { get; set; } = true;
        public bool NotifyOnMention { get; set; } = true;
        public bool NotifyOnStandardViolation { get; set; } = true;
        public bool NotifyOnWorksetChange { get; set; } = true;
        public bool SuppressOwnActions { get; set; } = true;
    }

    /// <summary>
    /// Represents a detected conflict between concurrent tag edits.
    /// </summary>
    public class CollaborationConflict
    {
        /// <summary>Unique conflict identifier.</summary>
        public string ConflictId { get; set; }

        /// <summary>Type of conflict detected.</summary>
        public TagConflictType ConflictType { get; set; }

        /// <summary>Tag ID involved in the conflict.</summary>
        public string TagId { get; set; }

        /// <summary>User who made the first change.</summary>
        public string FirstUserId { get; set; }

        /// <summary>User who made the conflicting change.</summary>
        public string SecondUserId { get; set; }

        /// <summary>Snapshot of the tag state before the first change.</summary>
        public TagInstance OriginalState { get; set; }

        /// <summary>State after the first user's change.</summary>
        public TagInstance FirstUserState { get; set; }

        /// <summary>State after the second user's change.</summary>
        public TagInstance SecondUserState { get; set; }

        /// <summary>When the conflict was detected.</summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>Whether this conflict has been resolved.</summary>
        public bool IsResolved { get; set; }

        /// <summary>Resolution applied, if any.</summary>
        public ConflictResolution Resolution { get; set; }

        /// <summary>Priority for resolution (higher = more urgent).</summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Result of resolving a collaboration conflict.
    /// </summary>
    public class ConflictResolution
    {
        /// <summary>Strategy used for resolution.</summary>
        public ConflictResolutionStrategy Strategy { get; set; }

        /// <summary>The winning tag state after resolution.</summary>
        public TagInstance ResolvedState { get; set; }

        /// <summary>User who performed or approved the resolution.</summary>
        public string ResolvedByUserId { get; set; }

        /// <summary>When the resolution was applied.</summary>
        public DateTime ResolvedAt { get; set; }

        /// <summary>Human-readable explanation of the resolution.</summary>
        public string Explanation { get; set; }

        /// <summary>Whether the resolution was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Changes that were discarded during resolution.</summary>
        public List<string> DiscardedChanges { get; set; } = new List<string>();
    }

    /// <summary>
    /// A review workflow item tracking a tag through the review lifecycle.
    /// </summary>
    public class ReviewWorkflow
    {
        /// <summary>Unique review identifier.</summary>
        public string ReviewId { get; set; }

        /// <summary>Tag ID under review.</summary>
        public string TagId { get; set; }

        /// <summary>Current review state.</summary>
        public ReviewState State { get; set; }

        /// <summary>User who submitted the tag for review.</summary>
        public string AuthorUserId { get; set; }

        /// <summary>User assigned to review the tag.</summary>
        public string ReviewerUserId { get; set; }

        /// <summary>When the review was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the review was last updated.</summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>When the review was completed (approved/rejected).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Comments attached to this review.</summary>
        public List<TagComment> Comments { get; set; } = new List<TagComment>();

        /// <summary>Review template used for evaluation, if any.</summary>
        public string ReviewTemplateName { get; set; }

        /// <summary>Standards violations found during review.</summary>
        public List<StandardViolation> Violations { get; set; } = new List<StandardViolation>();

        /// <summary>Number of revision cycles completed.</summary>
        public int RevisionCount { get; set; }
    }

    /// <summary>
    /// Comment or annotation attached to a tag for communication.
    /// </summary>
    public class TagComment
    {
        /// <summary>Unique comment identifier.</summary>
        public string CommentId { get; set; }

        /// <summary>Tag ID this comment is attached to.</summary>
        public string TagId { get; set; }

        /// <summary>User who authored the comment.</summary>
        public string AuthorUserId { get; set; }

        /// <summary>Display name of the author.</summary>
        public string AuthorDisplayName { get; set; }

        /// <summary>Comment text content. Supports @mention syntax.</summary>
        public string Text { get; set; }

        /// <summary>When the comment was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the comment was last edited.</summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>User IDs mentioned via @mention syntax.</summary>
        public List<string> MentionedUserIds { get; set; } = new List<string>();

        /// <summary>Whether this comment flags an issue.</summary>
        public bool IsIssueFlagged { get; set; }

        /// <summary>Parent comment ID for threaded replies.</summary>
        public string ParentCommentId { get; set; }

        /// <summary>Associated review ID, if part of a review workflow.</summary>
        public string ReviewId { get; set; }
    }

    /// <summary>
    /// Standards violation detected during enforcement checks.
    /// </summary>
    public class StandardViolation
    {
        public string ViolationId { get; set; }
        public string TagId { get; set; }
        public string StandardName { get; set; }
        public string RuleDescription { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string Details { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool IsOverridden { get; set; }
        public string OverriddenByUserId { get; set; }
        public string OverrideReason { get; set; }
    }

    /// <summary>
    /// Tag standard definition enforced at project or company level.
    /// </summary>
    public class TagStandard
    {
        public string StandardId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; } // "Project" or "Company"
        public string Version { get; set; }
        public List<TagStandardRule> Rules { get; set; } = new List<TagStandardRule>();
        public bool IsActive { get; set; }
        public DateTime EffectiveDate { get; set; }
        public List<TagUserRole> OverridePermittedRoles { get; set; } = new List<TagUserRole>();
    }

    /// <summary>
    /// Individual rule within a tag standard.
    /// </summary>
    public class TagStandardRule
    {
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string CategoryFilter { get; set; }
        public string PropertyPath { get; set; }
        public string ExpectedPattern { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string FixSuggestion { get; set; }
    }

    /// <summary>
    /// Workset-level coordination information for tag ownership.
    /// </summary>
    public class WorksetCoordinator
    {
        /// <summary>Workset ID from Revit.</summary>
        public int WorksetId { get; set; }

        /// <summary>Human-readable workset name.</summary>
        public string WorksetName { get; set; }

        /// <summary>User who currently owns (has checked out) this workset.</summary>
        public string OwnerUserId { get; set; }

        /// <summary>Tag IDs that belong to this workset.</summary>
        public HashSet<string> TagIds { get; set; } = new HashSet<string>();

        /// <summary>Whether this workset is currently borrowed by someone.</summary>
        public bool IsBorrowed { get; set; }

        /// <summary>User who currently has this workset borrowed.</summary>
        public string BorrowedByUserId { get; set; }

        /// <summary>When the current borrow started.</summary>
        public DateTime? BorrowStartTime { get; set; }

        /// <summary>Pending borrow requests from other users.</summary>
        public List<WorksetBorrowRequest> PendingRequests { get; set; } = new List<WorksetBorrowRequest>();
    }

    /// <summary>
    /// Request to borrow a workset for editing.
    /// </summary>
    public class WorksetBorrowRequest
    {
        public string RequestId { get; set; }
        public string RequestingUserId { get; set; }
        public int WorksetId { get; set; }
        public string Reason { get; set; }
        public DateTime RequestedAt { get; set; }
        public bool IsGranted { get; set; }
    }

    /// <summary>
    /// A locked spatial region preventing concurrent edits.
    /// </summary>
    public class RegionLock
    {
        public string LockId { get; set; }
        public string UserId { get; set; }
        public int ViewId { get; set; }
        public TagBounds2D Region { get; set; }
        public DateTime AcquiredAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Notification message broadcast to users.
    /// </summary>
    public class TagNotification
    {
        public string NotificationId { get; set; }
        public TagNotificationType Type { get; set; }
        public string SourceUserId { get; set; }
        public string TargetUserId { get; set; }
        public string TagId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Progress reporting during collaboration operations.
    /// </summary>
    public class CollaborationProgress
    {
        public string Operation { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
        public double PercentComplete => Total > 0 ? (double)Current / Total * 100.0 : 0.0;
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// Team-level analytics report for tagging collaboration.
    /// </summary>
    public class TeamAnalyticsReport
    {
        /// <summary>When this report was generated.</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Reporting period start.</summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>Reporting period end.</summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>Total active users in the period.</summary>
        public int ActiveUserCount { get; set; }

        /// <summary>Tags created per user.</summary>
        public Dictionary<string, int> TagsCreatedPerUser { get; set; } = new Dictionary<string, int>();

        /// <summary>Tags modified per user.</summary>
        public Dictionary<string, int> TagsModifiedPerUser { get; set; } = new Dictionary<string, int>();

        /// <summary>Average quality score per user (0-100).</summary>
        public Dictionary<string, double> QualityScorePerUser { get; set; } = new Dictionary<string, double>();

        /// <summary>Conflicts detected per user pair.</summary>
        public Dictionary<string, int> ConflictsPerUserPair { get; set; } = new Dictionary<string, int>();

        /// <summary>Average review turnaround time.</summary>
        public TimeSpan AverageReviewTurnaround { get; set; }

        /// <summary>Review rejection rate (0.0 to 1.0).</summary>
        public double ReviewRejectionRate { get; set; }

        /// <summary>Standards compliance rate per user (0.0 to 1.0).</summary>
        public Dictionary<string, double> ComplianceRatePerUser { get; set; } = new Dictionary<string, double>();

        /// <summary>Total conflicts detected in the period.</summary>
        public int TotalConflicts { get; set; }

        /// <summary>Total conflicts auto-resolved.</summary>
        public int AutoResolvedConflicts { get; set; }

        /// <summary>Bottleneck summary: areas with review delays or high conflict frequency.</summary>
        public List<string> Bottlenecks { get; set; } = new List<string>();

        /// <summary>Team coverage: which view/area is assigned to whom.</summary>
        public Dictionary<string, List<int>> UserViewAssignments { get; set; } = new Dictionary<string, List<int>>();

        /// <summary>Productivity index per user (tags per hour, anonymized).</summary>
        public Dictionary<string, double> ProductivityIndex { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Batch synchronization record for offline-then-online scenarios.
    /// </summary>
    public class SyncBatch
    {
        public string BatchId { get; set; }
        public string UserId { get; set; }
        public DateTime OfflineSince { get; set; }
        public DateTime SyncedAt { get; set; }
        public List<TagOperation> PendingOperations { get; set; } = new List<TagOperation>();
        public List<CollaborationConflict> DetectedConflicts { get; set; } = new List<CollaborationConflict>();
        public int AppliedCount { get; set; }
        public int ConflictCount { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Review template for standardized reviewer feedback.
    /// </summary>
    public class ReviewTemplate
    {
        public string TemplateName { get; set; }
        public string Description { get; set; }
        public List<ReviewChecklistItem> Checklist { get; set; } = new List<ReviewChecklistItem>();
    }

    /// <summary>
    /// Single checklist item in a review template.
    /// </summary>
    public class ReviewChecklistItem
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public bool IsChecked { get; set; }
    }

    #endregion

    /// <summary>
    /// Central coordination manager for multi-user tag collaboration.
    /// Handles session management, workset awareness, conflict detection and resolution,
    /// review workflows, standards enforcement, real-time notifications, and team analytics.
    /// Thread-safe for concurrent access from multiple Revit sessions.
    /// </summary>
    public class TagCollaborationManager
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lockObject = new object();

        // Session management
        private readonly Dictionary<string, UserSession> _activeSessions;
        private readonly Dictionary<string, UserSession> _sessionsByUser;
        private readonly List<ActivityLogEntry> _globalActivityLog;
        private readonly TimeSpan _sessionTimeout;

        // Workset coordination
        private readonly Dictionary<int, WorksetCoordinator> _worksets;
        private readonly Dictionary<string, int> _tagToWorksetMap;

        // Conflict management
        private readonly Dictionary<string, CollaborationConflict> _activeConflicts;
        private readonly List<CollaborationConflict> _conflictHistory;
        private readonly Dictionary<TagConflictType, ConflictResolutionStrategy> _autoResolutionRules;

        // Region locks
        private readonly Dictionary<string, RegionLock> _regionLocks;

        // Review workflow
        private readonly Dictionary<string, ReviewWorkflow> _activeReviews;
        private readonly List<ReviewWorkflow> _reviewHistory;
        private readonly Dictionary<string, ReviewTemplate> _reviewTemplates;

        // Standards
        private readonly List<TagStandard> _activeStandards;
        private readonly List<StandardViolation> _violationHistory;

        // Communication
        private readonly Dictionary<string, List<TagComment>> _commentsByTag;
        private readonly List<TagNotification> _notificationQueue;
        private readonly List<TagNotification> _notificationHistory;

        // Analytics
        private readonly Dictionary<string, int> _tagsCreatedCount;
        private readonly Dictionary<string, int> _tagsModifiedCount;
        private readonly Dictionary<string, List<double>> _qualityScores;

        // Tag version tracking for optimistic concurrency
        private readonly Dictionary<string, long> _tagVersions;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the TagCollaborationManager with default settings.
        /// </summary>
        /// <param name="sessionTimeoutMinutes">Minutes of inactivity before a session expires.</param>
        public TagCollaborationManager(int sessionTimeoutMinutes = 30)
        {
            _sessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes);

            _activeSessions = new Dictionary<string, UserSession>(StringComparer.OrdinalIgnoreCase);
            _sessionsByUser = new Dictionary<string, UserSession>(StringComparer.OrdinalIgnoreCase);
            _globalActivityLog = new List<ActivityLogEntry>();

            _worksets = new Dictionary<int, WorksetCoordinator>();
            _tagToWorksetMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            _activeConflicts = new Dictionary<string, CollaborationConflict>(StringComparer.OrdinalIgnoreCase);
            _conflictHistory = new List<CollaborationConflict>();
            _autoResolutionRules = new Dictionary<TagConflictType, ConflictResolutionStrategy>();

            _regionLocks = new Dictionary<string, RegionLock>(StringComparer.OrdinalIgnoreCase);

            _activeReviews = new Dictionary<string, ReviewWorkflow>(StringComparer.OrdinalIgnoreCase);
            _reviewHistory = new List<ReviewWorkflow>();
            _reviewTemplates = new Dictionary<string, ReviewTemplate>(StringComparer.OrdinalIgnoreCase);

            _activeStandards = new List<TagStandard>();
            _violationHistory = new List<StandardViolation>();

            _commentsByTag = new Dictionary<string, List<TagComment>>(StringComparer.OrdinalIgnoreCase);
            _notificationQueue = new List<TagNotification>();
            _notificationHistory = new List<TagNotification>();

            _tagsCreatedCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _tagsModifiedCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _qualityScores = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

            _tagVersions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            InitializeDefaultAutoResolutionRules();
            InitializeDefaultReviewTemplates();

            Logger.Info("TagCollaborationManager initialized with session timeout of {0} minutes", sessionTimeoutMinutes);
        }

        #endregion

        #region Initialization

        private void InitializeDefaultAutoResolutionRules()
        {
            _autoResolutionRules[TagConflictType.PositionConflict] = ConflictResolutionStrategy.LastWriteWins;
            _autoResolutionRules[TagConflictType.ContentConflict] = ConflictResolutionStrategy.PromptUser;
            _autoResolutionRules[TagConflictType.StyleConflict] = ConflictResolutionStrategy.LastWriteWins;
            _autoResolutionRules[TagConflictType.DeletionConflict] = ConflictResolutionStrategy.PromptUser;
            _autoResolutionRules[TagConflictType.RegionConflict] = ConflictResolutionStrategy.MergeChanges;
            _autoResolutionRules[TagConflictType.WorksetConflict] = ConflictResolutionStrategy.RoleBasedPriority;

            Logger.Debug("Default auto-resolution rules initialized for {0} conflict types", _autoResolutionRules.Count);
        }

        private void InitializeDefaultReviewTemplates()
        {
            _reviewTemplates["StandardReview"] = new ReviewTemplate
            {
                TemplateName = "StandardReview",
                Description = "Default review checklist for tag quality assurance",
                Checklist = new List<ReviewChecklistItem>
                {
                    new ReviewChecklistItem { ItemId = "CHK_POS", Description = "Tag position is clear and readable", IsRequired = true },
                    new ReviewChecklistItem { ItemId = "CHK_CONTENT", Description = "Tag content matches element data", IsRequired = true },
                    new ReviewChecklistItem { ItemId = "CHK_STYLE", Description = "Tag family/type follows project standards", IsRequired = true },
                    new ReviewChecklistItem { ItemId = "CHK_LEADER", Description = "Leader line is clean and not crossing other elements", IsRequired = false },
                    new ReviewChecklistItem { ItemId = "CHK_ALIGN", Description = "Tag alignment with neighboring tags", IsRequired = false },
                    new ReviewChecklistItem { ItemId = "CHK_OVERLAP", Description = "No overlapping with other annotations", IsRequired = true }
                }
            };

            _reviewTemplates["QuickReview"] = new ReviewTemplate
            {
                TemplateName = "QuickReview",
                Description = "Abbreviated review for minor tag changes",
                Checklist = new List<ReviewChecklistItem>
                {
                    new ReviewChecklistItem { ItemId = "CHK_CONTENT", Description = "Tag content is correct", IsRequired = true },
                    new ReviewChecklistItem { ItemId = "CHK_OVERLAP", Description = "No overlapping annotations", IsRequired = true }
                }
            };

            Logger.Debug("Initialized {0} default review templates", _reviewTemplates.Count);
        }

        #endregion

        #region User Session Management

        /// <summary>
        /// Creates a new user session and registers the user as active.
        /// </summary>
        /// <param name="userId">Unique user identifier.</param>
        /// <param name="displayName">User display name for UI.</param>
        /// <param name="role">Role assigned to the user for this project.</param>
        /// <returns>The created session, or the existing session if the user already has one.</returns>
        public UserSession StartSession(string userId, string displayName, TagUserRole role)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentNullException(nameof(userId));

            lock (_lockObject)
            {
                // Return existing session if user already has one
                if (_sessionsByUser.TryGetValue(userId, out var existing) && existing.IsActive)
                {
                    Logger.Info("User '{0}' already has active session '{1}'; returning existing", userId, existing.SessionId);
                    existing.LastActivity = DateTime.Now;
                    return existing;
                }

                var session = new UserSession
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    DisplayName = displayName ?? userId,
                    Role = role,
                    SessionStart = DateTime.Now,
                    LastActivity = DateTime.Now,
                    IsActive = true
                };

                _activeSessions[session.SessionId] = session;
                _sessionsByUser[userId] = session;

                LogActivity(userId, "SessionStarted", null, $"Session started with role {role}", 0);
                BroadcastNotification(TagNotificationType.SessionStarted, userId, null,
                    $"{session.DisplayName} joined the tagging session");

                Logger.Info("Session '{0}' created for user '{1}' with role {2}", session.SessionId, userId, role);
                return session;
            }
        }

        /// <summary>
        /// Ends a user session and releases all held locks and borrows.
        /// </summary>
        /// <param name="sessionId">Session identifier to end.</param>
        public void EndSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    Logger.Warn("Attempted to end non-existent session '{0}'", sessionId);
                    return;
                }

                // Release all locks held by this user
                ReleaseAllUserLocks(session.UserId);

                // Release all borrowed worksets
                ReleaseAllUserWorksets(session.UserId);

                session.IsActive = false;

                LogActivity(session.UserId, "SessionEnded", null,
                    $"Session ended after {(DateTime.Now - session.SessionStart).TotalMinutes:F1} minutes", 0);
                BroadcastNotification(TagNotificationType.SessionEnded, session.UserId, null,
                    $"{session.DisplayName} left the tagging session");

                Logger.Info("Session '{0}' ended for user '{1}'", sessionId, session.UserId);
            }
        }

        /// <summary>
        /// Gets all currently active user sessions.
        /// </summary>
        public List<UserSession> GetActiveSessions()
        {
            lock (_lockObject)
            {
                return _activeSessions.Values.Where(s => s.IsActive).ToList();
            }
        }

        /// <summary>
        /// Gets the session for a specific user, or null if not active.
        /// </summary>
        public UserSession GetSessionByUser(string userId)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session) && session.IsActive)
                    return session;
                return null;
            }
        }

        /// <summary>
        /// Updates the user's active view list (called when they switch views).
        /// </summary>
        public void UpdateUserActiveViews(string userId, List<int> viewIds)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session) && session.IsActive)
                {
                    session.ActiveViewIds = viewIds ?? new List<int>();
                    session.LastActivity = DateTime.Now;
                    Logger.Trace("Updated active views for user '{0}': {1} views", userId, viewIds?.Count ?? 0);
                }
            }
        }

        /// <summary>
        /// Updates the user's current element selection.
        /// </summary>
        public void UpdateUserSelection(string userId, List<int> elementIds)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session) && session.IsActive)
                {
                    session.CurrentSelection = elementIds ?? new List<int>();
                    session.LastActivity = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Updates per-user tag preferences.
        /// </summary>
        public void UpdateUserPreferences(string userId, Dictionary<string, object> preferences)
        {
            if (preferences == null) return;

            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session) && session.IsActive)
                {
                    foreach (var kvp in preferences)
                    {
                        session.Preferences[kvp.Key] = kvp.Value;
                    }
                    session.LastActivity = DateTime.Now;
                    Logger.Debug("Updated {0} preferences for user '{1}'", preferences.Count, userId);
                }
            }
        }

        /// <summary>
        /// Cleans up expired sessions and releases their resources.
        /// </summary>
        /// <returns>Number of sessions cleaned up.</returns>
        public int CleanupExpiredSessions()
        {
            int cleaned = 0;
            var now = DateTime.Now;

            lock (_lockObject)
            {
                var expired = _activeSessions.Values
                    .Where(s => s.IsActive && (now - s.LastActivity) > _sessionTimeout)
                    .ToList();

                foreach (var session in expired)
                {
                    session.IsActive = false;
                    ReleaseAllUserLocks(session.UserId);
                    ReleaseAllUserWorksets(session.UserId);

                    LogActivity(session.UserId, "SessionTimeout", null,
                        $"Session timed out after {_sessionTimeout.TotalMinutes:F0} minutes of inactivity", 0);

                    Logger.Info("Session '{0}' for user '{1}' expired due to inactivity", session.SessionId, session.UserId);
                    cleaned++;
                }
            }

            if (cleaned > 0)
                Logger.Info("Cleaned up {0} expired sessions", cleaned);

            return cleaned;
        }

        /// <summary>
        /// Gets the activity log for a specific user.
        /// </summary>
        public List<ActivityLogEntry> GetUserActivityLog(string userId, int maxEntries = 100)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session))
                {
                    return session.ActivityLog
                        .OrderByDescending(e => e.Timestamp)
                        .Take(maxEntries)
                        .ToList();
                }
                return new List<ActivityLogEntry>();
            }
        }

        /// <summary>
        /// Gets the global activity log across all users.
        /// </summary>
        public List<ActivityLogEntry> GetGlobalActivityLog(int maxEntries = 500)
        {
            lock (_lockObject)
            {
                return _globalActivityLog
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxEntries)
                    .ToList();
            }
        }

        #endregion

        #region Workset Integration

        /// <summary>
        /// Registers a workset for collaboration tracking.
        /// </summary>
        public void RegisterWorkset(int worksetId, string worksetName, string ownerUserId)
        {
            lock (_lockObject)
            {
                _worksets[worksetId] = new WorksetCoordinator
                {
                    WorksetId = worksetId,
                    WorksetName = worksetName ?? $"Workset_{worksetId}",
                    OwnerUserId = ownerUserId
                };
                Logger.Debug("Registered workset {0} ('{1}') owned by '{2}'", worksetId, worksetName, ownerUserId);
            }
        }

        /// <summary>
        /// Assigns a tag to a workset, establishing ownership.
        /// </summary>
        public void AssignTagToWorkset(string tagId, int worksetId)
        {
            lock (_lockObject)
            {
                if (_worksets.TryGetValue(worksetId, out var workset))
                {
                    // Remove from previous workset if any
                    if (_tagToWorksetMap.TryGetValue(tagId, out int oldWorksetId) && oldWorksetId != worksetId)
                    {
                        if (_worksets.TryGetValue(oldWorksetId, out var oldWorkset))
                        {
                            oldWorkset.TagIds.Remove(tagId);
                        }
                    }

                    workset.TagIds.Add(tagId);
                    _tagToWorksetMap[tagId] = worksetId;
                    Logger.Trace("Tag '{0}' assigned to workset {1}", tagId, worksetId);
                }
                else
                {
                    Logger.Warn("Cannot assign tag '{0}' to non-existent workset {1}", tagId, worksetId);
                }
            }
        }

        /// <summary>
        /// Auto-assigns a tag to the same workset as its host element.
        /// </summary>
        /// <param name="tagId">Tag identifier.</param>
        /// <param name="hostElementWorksetId">Workset ID of the tag's host element.</param>
        /// <returns>True if assignment was made.</returns>
        public bool AutoAssignTagToHostWorkset(string tagId, int hostElementWorksetId)
        {
            lock (_lockObject)
            {
                if (_worksets.ContainsKey(hostElementWorksetId))
                {
                    AssignTagToWorkset(tagId, hostElementWorksetId);
                    Logger.Debug("Auto-assigned tag '{0}' to host element workset {1}", tagId, hostElementWorksetId);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Checks whether a user can modify a tag based on workset ownership.
        /// </summary>
        /// <param name="userId">User attempting the modification.</param>
        /// <param name="tagId">Tag to modify.</param>
        /// <returns>True if the user has workset permission.</returns>
        public bool CanUserModifyTag(string userId, string tagId)
        {
            lock (_lockObject)
            {
                // Admins can modify anything
                if (_sessionsByUser.TryGetValue(userId, out var session) && session.Role == TagUserRole.TagAdmin)
                    return true;

                // Check workset ownership
                if (_tagToWorksetMap.TryGetValue(tagId, out int worksetId))
                {
                    if (_worksets.TryGetValue(worksetId, out var workset))
                    {
                        // Owner or borrower can edit
                        if (workset.OwnerUserId == userId)
                            return true;
                        if (workset.IsBorrowed && workset.BorrowedByUserId == userId)
                            return true;
                        return false;
                    }
                }

                // If no workset assignment, allow modification
                return true;
            }
        }

        /// <summary>
        /// Requests to borrow a workset for editing.
        /// </summary>
        /// <returns>True if the borrow was granted immediately.</returns>
        public bool RequestWorksetBorrow(string requestingUserId, int worksetId, string reason)
        {
            lock (_lockObject)
            {
                if (!_worksets.TryGetValue(worksetId, out var workset))
                {
                    Logger.Warn("Cannot borrow non-existent workset {0}", worksetId);
                    return false;
                }

                if (workset.OwnerUserId == requestingUserId)
                {
                    Logger.Debug("User '{0}' already owns workset {1}", requestingUserId, worksetId);
                    return true;
                }

                if (workset.IsBorrowed)
                {
                    // Queue the request
                    workset.PendingRequests.Add(new WorksetBorrowRequest
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        RequestingUserId = requestingUserId,
                        WorksetId = worksetId,
                        Reason = reason,
                        RequestedAt = DateTime.Now,
                        IsGranted = false
                    });

                    Logger.Info("Workset {0} borrow request from '{1}' queued (currently borrowed by '{2}')",
                        worksetId, requestingUserId, workset.BorrowedByUserId);
                    return false;
                }

                // Grant the borrow
                workset.IsBorrowed = true;
                workset.BorrowedByUserId = requestingUserId;
                workset.BorrowStartTime = DateTime.Now;

                if (_sessionsByUser.TryGetValue(requestingUserId, out var session))
                {
                    session.OwnedWorksetIds.Add(worksetId);
                }

                BroadcastNotification(TagNotificationType.WorksetBorrowed, requestingUserId, workset.OwnerUserId,
                    $"{requestingUserId} borrowed workset '{workset.WorksetName}'");

                Logger.Info("Workset {0} ('{1}') borrowed by '{2}'", worksetId, workset.WorksetName, requestingUserId);
                return true;
            }
        }

        /// <summary>
        /// Releases a borrowed workset and processes the next pending request.
        /// </summary>
        public void ReleaseWorksetBorrow(string userId, int worksetId)
        {
            lock (_lockObject)
            {
                if (!_worksets.TryGetValue(worksetId, out var workset))
                    return;

                if (workset.BorrowedByUserId != userId)
                {
                    Logger.Warn("User '{0}' cannot release workset {1} borrowed by '{2}'",
                        userId, worksetId, workset.BorrowedByUserId);
                    return;
                }

                workset.IsBorrowed = false;
                workset.BorrowedByUserId = null;
                workset.BorrowStartTime = null;

                if (_sessionsByUser.TryGetValue(userId, out var session))
                {
                    session.OwnedWorksetIds.Remove(worksetId);
                }

                BroadcastNotification(TagNotificationType.WorksetReleased, userId, workset.OwnerUserId,
                    $"{userId} released workset '{workset.WorksetName}'");

                // Process next pending request
                var nextRequest = workset.PendingRequests.FirstOrDefault();
                if (nextRequest != null)
                {
                    workset.PendingRequests.Remove(nextRequest);
                    RequestWorksetBorrow(nextRequest.RequestingUserId, worksetId, nextRequest.Reason);
                }

                Logger.Info("Workset {0} released by '{1}'", worksetId, userId);
            }
        }

        /// <summary>
        /// Detects whether a tag operation would cross workset boundaries.
        /// </summary>
        /// <returns>Warning message if crossing worksets, null otherwise.</returns>
        public string DetectWorksetBoundaryCrossing(string tagId, int targetHostElementWorksetId)
        {
            lock (_lockObject)
            {
                if (_tagToWorksetMap.TryGetValue(tagId, out int currentWorksetId))
                {
                    if (currentWorksetId != targetHostElementWorksetId)
                    {
                        string currentName = _worksets.TryGetValue(currentWorksetId, out var cw) ? cw.WorksetName : currentWorksetId.ToString();
                        string targetName = _worksets.TryGetValue(targetHostElementWorksetId, out var tw) ? tw.WorksetName : targetHostElementWorksetId.ToString();

                        string warning = $"Tag '{tagId}' belongs to workset '{currentName}' but host element is in workset '{targetName}'. Cross-workset tagging may cause coordination issues.";
                        Logger.Warn(warning);
                        return warning;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Pre-detects workset conflicts before an edit attempt.
        /// </summary>
        /// <returns>Conflict description, or null if no conflict.</returns>
        public string PreDetectWorksetConflict(string userId, string tagId)
        {
            lock (_lockObject)
            {
                if (!_tagToWorksetMap.TryGetValue(tagId, out int worksetId))
                    return null;

                if (!_worksets.TryGetValue(worksetId, out var workset))
                    return null;

                if (workset.OwnerUserId != userId &&
                    !(workset.IsBorrowed && workset.BorrowedByUserId == userId))
                {
                    // Check if user is admin
                    if (_sessionsByUser.TryGetValue(userId, out var session) && session.Role == TagUserRole.TagAdmin)
                        return null;

                    return $"Tag '{tagId}' is in workset '{workset.WorksetName}' owned by '{workset.OwnerUserId}'. " +
                           $"You must borrow the workset before editing.";
                }

                return null;
            }
        }

        /// <summary>
        /// Gets all worksets and their coordination state.
        /// </summary>
        public List<WorksetCoordinator> GetAllWorksets()
        {
            lock (_lockObject)
            {
                return _worksets.Values.ToList();
            }
        }

        private void ReleaseAllUserWorksets(string userId)
        {
            var borrowedWorksets = _worksets.Values
                .Where(w => w.IsBorrowed && w.BorrowedByUserId == userId)
                .Select(w => w.WorksetId)
                .ToList();

            foreach (int wsId in borrowedWorksets)
            {
                ReleaseWorksetBorrow(userId, wsId);
            }
        }

        #endregion

        #region Conflict Resolution

        /// <summary>
        /// Detects a conflict when two users modify the same tag concurrently.
        /// </summary>
        public CollaborationConflict DetectConflict(
            string tagId,
            string userId,
            TagInstance currentState,
            TagInstance proposedState,
            TagConflictType conflictType)
        {
            lock (_lockObject)
            {
                // Check if there is already an unresolved conflict for this tag
                var existingConflict = _activeConflicts.Values
                    .FirstOrDefault(c => c.TagId == tagId && !c.IsResolved);

                if (existingConflict != null)
                {
                    existingConflict.SecondUserId = userId;
                    existingConflict.SecondUserState = proposedState;
                    Logger.Warn("Extended existing conflict '{0}' for tag '{1}' with second user '{2}'",
                        existingConflict.ConflictId, tagId, userId);
                    return existingConflict;
                }

                var conflict = new CollaborationConflict
                {
                    ConflictId = Guid.NewGuid().ToString("N"),
                    ConflictType = conflictType,
                    TagId = tagId,
                    FirstUserId = currentState?.Metadata?.ContainsKey("LastModifiedBy") == true
                        ? currentState.Metadata["LastModifiedBy"]?.ToString()
                        : "unknown",
                    SecondUserId = userId,
                    OriginalState = currentState,
                    SecondUserState = proposedState,
                    DetectedAt = DateTime.Now,
                    IsResolved = false,
                    Priority = conflictType == TagConflictType.DeletionConflict ? 10 : 5
                };

                _activeConflicts[conflict.ConflictId] = conflict;

                BroadcastNotification(TagNotificationType.ConflictDetected, userId, conflict.FirstUserId,
                    $"Conflict detected on tag '{tagId}': {conflictType}");

                Logger.Warn("Conflict '{0}' detected: {1} on tag '{2}' between '{3}' and '{4}'",
                    conflict.ConflictId, conflictType, tagId, conflict.FirstUserId, userId);

                return conflict;
            }
        }

        /// <summary>
        /// Attempts automatic resolution of a conflict using configured rules.
        /// </summary>
        public async Task<ConflictResolution> AutoResolveConflictAsync(
            string conflictId,
            CancellationToken cancellationToken = default)
        {
            CollaborationConflict conflict;
            ConflictResolutionStrategy strategy;

            lock (_lockObject)
            {
                if (!_activeConflicts.TryGetValue(conflictId, out conflict))
                {
                    Logger.Warn("Cannot resolve non-existent conflict '{0}'", conflictId);
                    return new ConflictResolution { Success = false, Explanation = "Conflict not found" };
                }

                if (!_autoResolutionRules.TryGetValue(conflict.ConflictType, out strategy))
                {
                    strategy = ConflictResolutionStrategy.PromptUser;
                }
            }

            Logger.Info("Attempting auto-resolution of conflict '{0}' using strategy {1}", conflictId, strategy);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ApplyResolutionStrategy(conflict, strategy);
            }, cancellationToken);
        }

        /// <summary>
        /// Manually resolves a conflict with the specified resolution.
        /// </summary>
        public ConflictResolution ResolveConflict(string conflictId, string resolvedByUserId,
            TagInstance resolvedState, string explanation)
        {
            lock (_lockObject)
            {
                if (!_activeConflicts.TryGetValue(conflictId, out var conflict))
                {
                    Logger.Warn("Cannot resolve non-existent conflict '{0}'", conflictId);
                    return new ConflictResolution { Success = false, Explanation = "Conflict not found" };
                }

                var resolution = new ConflictResolution
                {
                    Strategy = ConflictResolutionStrategy.PromptUser,
                    ResolvedState = resolvedState,
                    ResolvedByUserId = resolvedByUserId,
                    ResolvedAt = DateTime.Now,
                    Explanation = explanation,
                    Success = true
                };

                conflict.Resolution = resolution;
                conflict.IsResolved = true;

                _activeConflicts.Remove(conflictId);
                _conflictHistory.Add(conflict);

                LogActivity(resolvedByUserId, "ConflictResolved", conflict.TagId,
                    $"Resolved {conflict.ConflictType} conflict: {explanation}", 0);

                Logger.Info("Conflict '{0}' manually resolved by '{1}'", conflictId, resolvedByUserId);
                return resolution;
            }
        }

        /// <summary>
        /// Performs a three-way merge for complex conflicts where both users made non-overlapping changes.
        /// </summary>
        public ConflictResolution ThreeWayMerge(CollaborationConflict conflict)
        {
            if (conflict == null)
                throw new ArgumentNullException(nameof(conflict));

            lock (_lockObject)
            {
                Logger.Info("Attempting three-way merge for conflict '{0}'", conflict.ConflictId);

                var baseState = conflict.OriginalState;
                var stateA = conflict.FirstUserState;
                var stateB = conflict.SecondUserState;

                if (baseState == null || stateB == null)
                {
                    return new ConflictResolution
                    {
                        Success = false,
                        Strategy = ConflictResolutionStrategy.MergeChanges,
                        Explanation = "Cannot merge: missing base or variant state"
                    };
                }

                // Build merged state starting from base
                var merged = new TagInstance
                {
                    TagId = baseState.TagId,
                    RevitElementId = baseState.RevitElementId,
                    HostElementId = baseState.HostElementId,
                    ViewId = baseState.ViewId,
                    CategoryName = baseState.CategoryName,
                    FamilyName = baseState.FamilyName,
                    TypeName = baseState.TypeName,
                    State = baseState.State,
                    CreationSource = baseState.CreationSource,
                    LastModified = DateTime.Now,
                    Metadata = new Dictionary<string, object>(baseState.Metadata)
                };

                var discarded = new List<string>();
                bool canMerge = true;

                // Merge position: if only one user changed it, take that change
                bool aChangedPosition = stateA?.Placement != null && baseState.Placement != null &&
                    (stateA.Placement.Position.X != baseState.Placement.Position.X ||
                     stateA.Placement.Position.Y != baseState.Placement.Position.Y);
                bool bChangedPosition = stateB.Placement != null && baseState.Placement != null &&
                    (stateB.Placement.Position.X != baseState.Placement.Position.X ||
                     stateB.Placement.Position.Y != baseState.Placement.Position.Y);

                if (aChangedPosition && bChangedPosition)
                {
                    // Both changed position - cannot auto-merge
                    canMerge = false;
                    discarded.Add("Both users changed position; cannot auto-merge");
                }
                else if (aChangedPosition && stateA?.Placement != null)
                {
                    merged.Placement = stateA.Placement;
                }
                else if (bChangedPosition)
                {
                    merged.Placement = stateB.Placement;
                }
                else
                {
                    merged.Placement = baseState.Placement;
                }

                // Merge content: if only one changed, take that change
                bool aChangedContent = stateA?.DisplayText != baseState.DisplayText;
                bool bChangedContent = stateB.DisplayText != baseState.DisplayText;

                if (aChangedContent && bChangedContent)
                {
                    canMerge = false;
                    discarded.Add("Both users changed content; cannot auto-merge");
                }
                else if (aChangedContent)
                {
                    merged.DisplayText = stateA?.DisplayText;
                    merged.ContentExpression = stateA?.ContentExpression;
                }
                else if (bChangedContent)
                {
                    merged.DisplayText = stateB.DisplayText;
                    merged.ContentExpression = stateB.ContentExpression;
                }
                else
                {
                    merged.DisplayText = baseState.DisplayText;
                    merged.ContentExpression = baseState.ContentExpression;
                }

                // Merge style: if only one changed, take that change
                bool aChangedStyle = stateA?.TagFamilyName != baseState.TagFamilyName ||
                                     stateA?.TagTypeName != baseState.TagTypeName;
                bool bChangedStyle = stateB.TagFamilyName != baseState.TagFamilyName ||
                                     stateB.TagTypeName != baseState.TagTypeName;

                if (aChangedStyle && bChangedStyle)
                {
                    canMerge = false;
                    discarded.Add("Both users changed style; cannot auto-merge");
                }
                else if (aChangedStyle)
                {
                    merged.TagFamilyName = stateA?.TagFamilyName;
                    merged.TagTypeName = stateA?.TagTypeName;
                }
                else if (bChangedStyle)
                {
                    merged.TagFamilyName = stateB.TagFamilyName;
                    merged.TagTypeName = stateB.TagTypeName;
                }
                else
                {
                    merged.TagFamilyName = baseState.TagFamilyName;
                    merged.TagTypeName = baseState.TagTypeName;
                }

                merged.Metadata["MergedAt"] = DateTime.Now;
                merged.Metadata["MergedBy"] = "ThreeWayMerge";

                var resolution = new ConflictResolution
                {
                    Strategy = ConflictResolutionStrategy.MergeChanges,
                    ResolvedState = canMerge ? merged : null,
                    ResolvedAt = DateTime.Now,
                    Explanation = canMerge
                        ? "Three-way merge successful: non-overlapping changes combined"
                        : $"Three-way merge failed: {string.Join("; ", discarded)}",
                    Success = canMerge,
                    DiscardedChanges = discarded
                };

                if (canMerge)
                {
                    conflict.Resolution = resolution;
                    conflict.IsResolved = true;
                    _activeConflicts.Remove(conflict.ConflictId);
                    _conflictHistory.Add(conflict);
                    Logger.Info("Three-way merge succeeded for conflict '{0}'", conflict.ConflictId);
                }
                else
                {
                    Logger.Warn("Three-way merge failed for conflict '{0}': {1}", conflict.ConflictId, resolution.Explanation);
                }

                return resolution;
            }
        }

        /// <summary>
        /// Escalates an unresolvable conflict to a TagAdmin.
        /// </summary>
        public bool EscalateConflict(string conflictId, string reason)
        {
            lock (_lockObject)
            {
                if (!_activeConflicts.TryGetValue(conflictId, out var conflict))
                    return false;

                // Find an admin user
                var admin = _sessionsByUser.Values
                    .FirstOrDefault(s => s.IsActive && s.Role == TagUserRole.TagAdmin);

                if (admin == null)
                {
                    Logger.Warn("No active TagAdmin to escalate conflict '{0}'", conflictId);
                    return false;
                }

                conflict.Priority = 20; // Escalated priority

                BroadcastNotification(TagNotificationType.ConflictDetected, conflict.SecondUserId, admin.UserId,
                    $"Escalated conflict on tag '{conflict.TagId}': {reason}");

                LogActivity("System", "ConflictEscalated", conflict.TagId,
                    $"Conflict '{conflictId}' escalated to admin '{admin.UserId}': {reason}", 0);

                Logger.Info("Conflict '{0}' escalated to admin '{1}'", conflictId, admin.UserId);
                return true;
            }
        }

        /// <summary>
        /// Gets all active (unresolved) conflicts.
        /// </summary>
        public List<CollaborationConflict> GetActiveConflicts()
        {
            lock (_lockObject)
            {
                return _activeConflicts.Values.OrderByDescending(c => c.Priority).ToList();
            }
        }

        /// <summary>
        /// Gets the conflict history with optional filtering.
        /// </summary>
        public List<CollaborationConflict> GetConflictHistory(string userId = null, int maxEntries = 200)
        {
            lock (_lockObject)
            {
                IEnumerable<CollaborationConflict> query = _conflictHistory;

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(c => c.FirstUserId == userId || c.SecondUserId == userId);
                }

                return query.OrderByDescending(c => c.DetectedAt).Take(maxEntries).ToList();
            }
        }

        /// <summary>
        /// Sets an auto-resolution rule for a specific conflict type.
        /// </summary>
        public void SetAutoResolutionRule(TagConflictType conflictType, ConflictResolutionStrategy strategy)
        {
            lock (_lockObject)
            {
                _autoResolutionRules[conflictType] = strategy;
                Logger.Info("Auto-resolution rule set: {0} -> {1}", conflictType, strategy);
            }
        }

        private ConflictResolution ApplyResolutionStrategy(CollaborationConflict conflict, ConflictResolutionStrategy strategy)
        {
            switch (strategy)
            {
                case ConflictResolutionStrategy.LastWriteWins:
                    return ApplyLastWriteWins(conflict);

                case ConflictResolutionStrategy.MergeChanges:
                    return ThreeWayMerge(conflict);

                case ConflictResolutionStrategy.RoleBasedPriority:
                    return ApplyRoleBasedPriority(conflict);

                case ConflictResolutionStrategy.Escalate:
                    EscalateConflict(conflict.ConflictId, "Auto-escalated by resolution rules");
                    return new ConflictResolution
                    {
                        Success = false,
                        Strategy = strategy,
                        Explanation = "Escalated to administrator"
                    };

                case ConflictResolutionStrategy.PromptUser:
                default:
                    return new ConflictResolution
                    {
                        Success = false,
                        Strategy = strategy,
                        Explanation = "User prompt required; cannot auto-resolve"
                    };
            }
        }

        private ConflictResolution ApplyLastWriteWins(CollaborationConflict conflict)
        {
            lock (_lockObject)
            {
                var resolution = new ConflictResolution
                {
                    Strategy = ConflictResolutionStrategy.LastWriteWins,
                    ResolvedState = conflict.SecondUserState,
                    ResolvedByUserId = "System",
                    ResolvedAt = DateTime.Now,
                    Explanation = $"Last write wins: accepted changes from '{conflict.SecondUserId}'",
                    Success = true,
                    DiscardedChanges = new List<string> { $"Changes by '{conflict.FirstUserId}' overwritten" }
                };

                conflict.Resolution = resolution;
                conflict.IsResolved = true;
                _activeConflicts.Remove(conflict.ConflictId);
                _conflictHistory.Add(conflict);

                Logger.Info("Conflict '{0}' resolved via LastWriteWins: '{1}' wins", conflict.ConflictId, conflict.SecondUserId);
                return resolution;
            }
        }

        private ConflictResolution ApplyRoleBasedPriority(CollaborationConflict conflict)
        {
            lock (_lockObject)
            {
                TagUserRole firstRole = TagUserRole.TagEditor;
                TagUserRole secondRole = TagUserRole.TagEditor;

                if (_sessionsByUser.TryGetValue(conflict.FirstUserId, out var firstSession))
                    firstRole = firstSession.Role;
                if (_sessionsByUser.TryGetValue(conflict.SecondUserId, out var secondSession))
                    secondRole = secondSession.Role;

                // Higher role wins (Admin > Owner > Editor > Reviewer)
                int firstPriority = GetRolePriority(firstRole);
                int secondPriority = GetRolePriority(secondRole);

                string winnerId;
                TagInstance winnerState;

                if (firstPriority >= secondPriority)
                {
                    winnerId = conflict.FirstUserId;
                    winnerState = conflict.FirstUserState ?? conflict.OriginalState;
                }
                else
                {
                    winnerId = conflict.SecondUserId;
                    winnerState = conflict.SecondUserState;
                }

                var resolution = new ConflictResolution
                {
                    Strategy = ConflictResolutionStrategy.RoleBasedPriority,
                    ResolvedState = winnerState,
                    ResolvedByUserId = "System",
                    ResolvedAt = DateTime.Now,
                    Explanation = $"Role-based priority: '{winnerId}' (role priority {Math.Max(firstPriority, secondPriority)}) wins",
                    Success = true
                };

                conflict.Resolution = resolution;
                conflict.IsResolved = true;
                _activeConflicts.Remove(conflict.ConflictId);
                _conflictHistory.Add(conflict);

                Logger.Info("Conflict '{0}' resolved via RoleBasedPriority: '{1}' wins", conflict.ConflictId, winnerId);
                return resolution;
            }
        }

        private static int GetRolePriority(TagUserRole role)
        {
            return role switch
            {
                TagUserRole.TagAdmin => 100,
                TagUserRole.TagOwner => 75,
                TagUserRole.TagEditor => 50,
                TagUserRole.TagReviewer => 25,
                _ => 0
            };
        }

        #endregion

        #region Real-Time Coordination

        /// <summary>
        /// Acquires a region lock preventing other users from editing tags in the area.
        /// </summary>
        /// <param name="userId">User requesting the lock.</param>
        /// <param name="viewId">View containing the region.</param>
        /// <param name="region">Bounding box of the locked region.</param>
        /// <param name="durationMinutes">Lock duration before auto-expiry.</param>
        /// <returns>Lock ID if granted, null if the region overlaps an existing lock.</returns>
        public string AcquireRegionLock(string userId, int viewId, TagBounds2D region, int durationMinutes = 15)
        {
            lock (_lockObject)
            {
                // Check for overlapping locks in the same view
                var overlapping = _regionLocks.Values
                    .Where(r => r.ViewId == viewId && r.UserId != userId && r.ExpiresAt > DateTime.Now)
                    .FirstOrDefault(r => r.Region.Intersects(region));

                if (overlapping != null)
                {
                    Logger.Warn("Region lock denied for '{0}' in view {1}: overlaps lock '{2}' held by '{3}'",
                        userId, viewId, overlapping.LockId, overlapping.UserId);
                    return null;
                }

                var lockEntry = new RegionLock
                {
                    LockId = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    ViewId = viewId,
                    Region = region,
                    AcquiredAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMinutes(durationMinutes),
                    Reason = "Editing tags in region"
                };

                _regionLocks[lockEntry.LockId] = lockEntry;

                BroadcastNotification(TagNotificationType.RegionLocked, userId, null,
                    $"Region locked in view {viewId} by {userId}");

                Logger.Debug("Region lock '{0}' acquired by '{1}' in view {2}, expires {3}",
                    lockEntry.LockId, userId, viewId, lockEntry.ExpiresAt);

                return lockEntry.LockId;
            }
        }

        /// <summary>
        /// Releases a region lock.
        /// </summary>
        public void ReleaseRegionLock(string lockId)
        {
            lock (_lockObject)
            {
                if (_regionLocks.TryGetValue(lockId, out var lockEntry))
                {
                    _regionLocks.Remove(lockId);

                    BroadcastNotification(TagNotificationType.RegionUnlocked, lockEntry.UserId, null,
                        $"Region unlocked in view {lockEntry.ViewId}");

                    Logger.Debug("Region lock '{0}' released by '{1}'", lockId, lockEntry.UserId);
                }
            }
        }

        /// <summary>
        /// Cleans up expired region locks.
        /// </summary>
        /// <returns>Number of locks cleaned up.</returns>
        public int CleanupExpiredLocks()
        {
            int cleaned = 0;
            var now = DateTime.Now;

            lock (_lockObject)
            {
                var expired = _regionLocks.Where(kvp => kvp.Value.ExpiresAt <= now).Select(kvp => kvp.Key).ToList();
                foreach (var lockId in expired)
                {
                    _regionLocks.Remove(lockId);
                    cleaned++;
                }
            }

            if (cleaned > 0)
                Logger.Debug("Cleaned up {0} expired region locks", cleaned);

            return cleaned;
        }

        /// <summary>
        /// Gets visual indicators of other users' active areas for the given view.
        /// </summary>
        public List<RegionLock> GetActiveRegionLocks(int viewId, string excludeUserId = null)
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                return _regionLocks.Values
                    .Where(r => r.ViewId == viewId && r.ExpiresAt > now &&
                               (excludeUserId == null || r.UserId != excludeUserId))
                    .ToList();
            }
        }

        /// <summary>
        /// Locks a specific tag for exclusive editing by a user.
        /// </summary>
        /// <returns>True if the lock was acquired.</returns>
        public bool LockTag(string userId, string tagId)
        {
            lock (_lockObject)
            {
                // Check if another user holds the lock
                foreach (var session in _activeSessions.Values)
                {
                    if (session.IsActive && session.UserId != userId && session.LockedTagIds.Contains(tagId))
                    {
                        Logger.Debug("Tag '{0}' lock denied for '{1}': held by '{2}'", tagId, userId, session.UserId);
                        return false;
                    }
                }

                if (_sessionsByUser.TryGetValue(userId, out var userSession) && userSession.IsActive)
                {
                    userSession.LockedTagIds.Add(tagId);
                    userSession.LastActivity = DateTime.Now;
                    Logger.Trace("Tag '{0}' locked by '{1}'", tagId, userId);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Releases a tag lock.
        /// </summary>
        public void UnlockTag(string userId, string tagId)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session))
                {
                    session.LockedTagIds.Remove(tagId);
                    Logger.Trace("Tag '{0}' unlocked by '{1}'", tagId, userId);
                }
            }
        }

        /// <summary>
        /// Registers a tag version for optimistic concurrency control.
        /// </summary>
        public void RegisterTagVersion(string tagId, long version)
        {
            lock (_lockObject)
            {
                _tagVersions[tagId] = version;
            }
        }

        /// <summary>
        /// Checks whether a tag version is current (optimistic concurrency).
        /// </summary>
        /// <returns>True if the provided version matches the current version.</returns>
        public bool CheckTagVersion(string tagId, long expectedVersion)
        {
            lock (_lockObject)
            {
                if (_tagVersions.TryGetValue(tagId, out long currentVersion))
                {
                    return currentVersion == expectedVersion;
                }
                // No version tracked; assume current
                return true;
            }
        }

        /// <summary>
        /// Increments a tag version after a successful modification.
        /// </summary>
        /// <returns>The new version number.</returns>
        public long IncrementTagVersion(string tagId)
        {
            lock (_lockObject)
            {
                if (!_tagVersions.TryGetValue(tagId, out long current))
                    current = 0;

                long newVersion = current + 1;
                _tagVersions[tagId] = newVersion;
                return newVersion;
            }
        }

        /// <summary>
        /// Broadcasts a tag change notification to all connected users.
        /// </summary>
        public void NotifyTagChange(string sourceUserId, string tagId, TagNotificationType type, string message)
        {
            BroadcastNotification(type, sourceUserId, null, message, tagId);
        }

        /// <summary>
        /// Synchronizes a batch of offline operations back into the collaboration state.
        /// </summary>
        public async Task<SyncBatch> SynchronizeBatchAsync(
            string userId,
            List<TagOperation> offlineOperations,
            DateTime offlineSince,
            IProgress<CollaborationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (offlineOperations == null || offlineOperations.Count == 0)
            {
                return new SyncBatch
                {
                    BatchId = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    OfflineSince = offlineSince,
                    SyncedAt = DateTime.Now,
                    Success = true
                };
            }

            Logger.Info("Synchronizing {0} offline operations for user '{1}' (offline since {2})",
                offlineOperations.Count, userId, offlineSince);

            var batch = new SyncBatch
            {
                BatchId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                OfflineSince = offlineSince,
                PendingOperations = offlineOperations
            };

            int applied = 0;
            int total = offlineOperations.Count;

            foreach (var operation in offlineOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check version for optimistic concurrency
                bool versionOk = CheckTagVersion(operation.TagId, operation.PreviousState?.RevitElementId ?? 0);

                if (!versionOk)
                {
                    // Detect conflict
                    var conflict = DetectConflict(
                        operation.TagId, userId,
                        operation.PreviousState, operation.NewState,
                        MapOperationToConflictType(operation.Type));

                    batch.DetectedConflicts.Add(conflict);
                }
                else
                {
                    IncrementTagVersion(operation.TagId);
                    applied++;
                }

                progress?.Report(new CollaborationProgress
                {
                    Operation = "Synchronizing offline changes",
                    Current = applied + batch.DetectedConflicts.Count,
                    Total = total,
                    StatusMessage = $"Processed {applied} of {total} operations"
                });
            }

            batch.AppliedCount = applied;
            batch.ConflictCount = batch.DetectedConflicts.Count;
            batch.SyncedAt = DateTime.Now;
            batch.Success = batch.ConflictCount == 0;

            Logger.Info("Batch sync complete: {0} applied, {1} conflicts", applied, batch.ConflictCount);
            return batch;
        }

        private void ReleaseAllUserLocks(string userId)
        {
            // Release tag locks
            if (_sessionsByUser.TryGetValue(userId, out var session))
            {
                session.LockedTagIds.Clear();
            }

            // Release region locks
            var userRegionLocks = _regionLocks
                .Where(kvp => kvp.Value.UserId == userId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var lockId in userRegionLocks)
            {
                _regionLocks.Remove(lockId);
            }

            Logger.Debug("Released all locks for user '{0}' ({1} region locks)", userId, userRegionLocks.Count);
        }

        private static TagConflictType MapOperationToConflictType(TagOperationType operationType)
        {
            return operationType switch
            {
                TagOperationType.Move => TagConflictType.PositionConflict,
                TagOperationType.ReText => TagConflictType.ContentConflict,
                TagOperationType.Restyle => TagConflictType.StyleConflict,
                TagOperationType.Delete => TagConflictType.DeletionConflict,
                _ => TagConflictType.ContentConflict
            };
        }

        #endregion

        #region Review Workflow

        /// <summary>
        /// Submits a tag for review.
        /// </summary>
        public ReviewWorkflow SubmitForReview(string tagId, string authorUserId,
            string reviewerUserId = null, string templateName = null)
        {
            lock (_lockObject)
            {
                var review = new ReviewWorkflow
                {
                    ReviewId = Guid.NewGuid().ToString("N"),
                    TagId = tagId,
                    State = ReviewState.PendingReview,
                    AuthorUserId = authorUserId,
                    ReviewerUserId = reviewerUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    ReviewTemplateName = templateName ?? "StandardReview"
                };

                _activeReviews[review.ReviewId] = review;

                LogActivity(authorUserId, "SubmittedForReview", tagId,
                    $"Tag submitted for review (reviewer: {reviewerUserId ?? "unassigned"})", 0);

                if (!string.IsNullOrEmpty(reviewerUserId))
                {
                    BroadcastNotification(TagNotificationType.TagReviewRequested, authorUserId, reviewerUserId,
                        $"Tag '{tagId}' needs your review");
                }

                Logger.Info("Review '{0}' created for tag '{1}' by '{2}'", review.ReviewId, tagId, authorUserId);
                return review;
            }
        }

        /// <summary>
        /// Approves a tag in review.
        /// </summary>
        public bool ApproveReview(string reviewId, string reviewerUserId, string comment = null)
        {
            lock (_lockObject)
            {
                if (!_activeReviews.TryGetValue(reviewId, out var review))
                {
                    Logger.Warn("Cannot approve non-existent review '{0}'", reviewId);
                    return false;
                }

                if (review.ReviewerUserId != null && review.ReviewerUserId != reviewerUserId)
                {
                    // Check if user is admin
                    if (!(_sessionsByUser.TryGetValue(reviewerUserId, out var session) && session.Role == TagUserRole.TagAdmin))
                    {
                        Logger.Warn("User '{0}' is not the assigned reviewer for '{1}'", reviewerUserId, reviewId);
                        return false;
                    }
                }

                review.State = ReviewState.Approved;
                review.UpdatedAt = DateTime.Now;
                review.CompletedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(comment))
                {
                    AddReviewComment(review, reviewerUserId, comment);
                }

                _activeReviews.Remove(reviewId);
                _reviewHistory.Add(review);

                BroadcastNotification(TagNotificationType.TagApproved, reviewerUserId, review.AuthorUserId,
                    $"Tag '{review.TagId}' approved");

                LogActivity(reviewerUserId, "ReviewApproved", review.TagId, "Tag approved", 0);
                Logger.Info("Review '{0}' approved by '{1}'", reviewId, reviewerUserId);
                return true;
            }
        }

        /// <summary>
        /// Rejects a tag in review.
        /// </summary>
        public bool RejectReview(string reviewId, string reviewerUserId, string reason)
        {
            lock (_lockObject)
            {
                if (!_activeReviews.TryGetValue(reviewId, out var review))
                {
                    Logger.Warn("Cannot reject non-existent review '{0}'", reviewId);
                    return false;
                }

                review.State = ReviewState.Rejected;
                review.UpdatedAt = DateTime.Now;
                review.CompletedAt = DateTime.Now;

                AddReviewComment(review, reviewerUserId, $"Rejected: {reason}");

                _activeReviews.Remove(reviewId);
                _reviewHistory.Add(review);

                BroadcastNotification(TagNotificationType.TagRejected, reviewerUserId, review.AuthorUserId,
                    $"Tag '{review.TagId}' rejected: {reason}");

                LogActivity(reviewerUserId, "ReviewRejected", review.TagId, $"Tag rejected: {reason}", 0);
                Logger.Info("Review '{0}' rejected by '{1}': {2}", reviewId, reviewerUserId, reason);
                return true;
            }
        }

        /// <summary>
        /// Requests revision on a tag in review.
        /// </summary>
        public bool RequestRevision(string reviewId, string reviewerUserId, string feedback)
        {
            lock (_lockObject)
            {
                if (!_activeReviews.TryGetValue(reviewId, out var review))
                {
                    Logger.Warn("Cannot request revision on non-existent review '{0}'", reviewId);
                    return false;
                }

                review.State = ReviewState.NeedsRevision;
                review.UpdatedAt = DateTime.Now;
                review.RevisionCount++;

                AddReviewComment(review, reviewerUserId, $"Revision requested: {feedback}");

                BroadcastNotification(TagNotificationType.TagRejected, reviewerUserId, review.AuthorUserId,
                    $"Tag '{review.TagId}' needs revision: {feedback}");

                LogActivity(reviewerUserId, "RevisionRequested", review.TagId, $"Revision #{review.RevisionCount}: {feedback}", 0);
                Logger.Info("Revision #{0} requested on review '{1}'", review.RevisionCount, reviewId);
                return true;
            }
        }

        /// <summary>
        /// Resubmits a revised tag for review.
        /// </summary>
        public bool ResubmitForReview(string reviewId, string authorUserId, string revisionNotes)
        {
            lock (_lockObject)
            {
                if (!_activeReviews.TryGetValue(reviewId, out var review))
                    return false;

                if (review.State != ReviewState.NeedsRevision && review.State != ReviewState.Rejected)
                {
                    Logger.Warn("Cannot resubmit review '{0}' in state {1}", reviewId, review.State);
                    return false;
                }

                review.State = ReviewState.PendingReview;
                review.UpdatedAt = DateTime.Now;

                AddReviewComment(review, authorUserId, $"Resubmitted: {revisionNotes}");

                if (!string.IsNullOrEmpty(review.ReviewerUserId))
                {
                    BroadcastNotification(TagNotificationType.TagReviewRequested, authorUserId, review.ReviewerUserId,
                        $"Tag '{review.TagId}' resubmitted for review");
                }

                Logger.Info("Review '{0}' resubmitted by '{1}'", reviewId, authorUserId);
                return true;
            }
        }

        /// <summary>
        /// Batch approves multiple reviews.
        /// </summary>
        public async Task<int> BatchApproveAsync(
            List<string> reviewIds,
            string reviewerUserId,
            string comment = null,
            IProgress<CollaborationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            int approved = 0;
            int total = reviewIds?.Count ?? 0;

            if (total == 0) return 0;

            Logger.Info("Batch approval started: {0} reviews by '{1}'", total, reviewerUserId);

            foreach (var reviewId in reviewIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ApproveReview(reviewId, reviewerUserId, comment))
                    approved++;

                progress?.Report(new CollaborationProgress
                {
                    Operation = "Batch approval",
                    Current = approved,
                    Total = total,
                    StatusMessage = $"Approved {approved} of {total}"
                });

                // Yield to avoid blocking UI
                await Task.Yield();
            }

            Logger.Info("Batch approval complete: {0}/{1} approved", approved, total);
            return approved;
        }

        /// <summary>
        /// Gets all active reviews, optionally filtered by reviewer.
        /// </summary>
        public List<ReviewWorkflow> GetActiveReviews(string reviewerUserId = null)
        {
            lock (_lockObject)
            {
                IEnumerable<ReviewWorkflow> query = _activeReviews.Values;

                if (!string.IsNullOrEmpty(reviewerUserId))
                    query = query.Where(r => r.ReviewerUserId == reviewerUserId);

                return query.OrderBy(r => r.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Gets review metrics for analytics.
        /// </summary>
        public (TimeSpan averageTurnaround, double rejectionRate, int totalReviews) GetReviewMetrics(
            DateTime? since = null)
        {
            lock (_lockObject)
            {
                var completed = _reviewHistory.AsEnumerable();
                if (since.HasValue)
                    completed = completed.Where(r => r.CreatedAt >= since.Value);

                var list = completed.ToList();
                if (list.Count == 0)
                    return (TimeSpan.Zero, 0.0, 0);

                var turnarounds = list
                    .Where(r => r.CompletedAt.HasValue)
                    .Select(r => r.CompletedAt.Value - r.CreatedAt)
                    .ToList();

                var avgTurnaround = turnarounds.Count > 0
                    ? TimeSpan.FromTicks((long)turnarounds.Average(t => t.Ticks))
                    : TimeSpan.Zero;

                int rejected = list.Count(r => r.State == ReviewState.Rejected);
                double rejectionRate = (double)rejected / list.Count;

                return (avgTurnaround, rejectionRate, list.Count);
            }
        }

        /// <summary>
        /// Performs automated review suggestions by checking for obvious issues.
        /// </summary>
        public List<string> GetAutomatedReviewSuggestions(string tagId, TagInstance tagState)
        {
            var suggestions = new List<string>();

            if (tagState == null) return suggestions;

            if (string.IsNullOrWhiteSpace(tagState.DisplayText))
                suggestions.Add("Tag has blank or empty display text");

            if (tagState.Placement == null)
                suggestions.Add("Tag has no placement data");

            if (tagState.State == TagState.Orphaned)
                suggestions.Add("Tag is orphaned - host element no longer exists");

            if (tagState.State == TagState.Stale)
                suggestions.Add("Tag text is stale - does not match current parameter values");

            if (tagState.PlacementScore < 0.3)
                suggestions.Add($"Low placement score ({tagState.PlacementScore:F2}); consider repositioning");

            if (string.IsNullOrEmpty(tagState.TagFamilyName))
                suggestions.Add("No tag family assigned");

            // Check for standards violations
            lock (_lockObject)
            {
                foreach (var standard in _activeStandards.Where(s => s.IsActive))
                {
                    foreach (var rule in standard.Rules)
                    {
                        if (!string.IsNullOrEmpty(rule.CategoryFilter) &&
                            rule.CategoryFilter != tagState.CategoryName)
                            continue;

                        if (!string.IsNullOrEmpty(rule.ExpectedPattern) &&
                            !string.IsNullOrEmpty(tagState.DisplayText))
                        {
                            try
                            {
                                if (!System.Text.RegularExpressions.Regex.IsMatch(tagState.DisplayText, rule.ExpectedPattern))
                                {
                                    suggestions.Add($"Standards violation ({standard.Name}): {rule.Description}");
                                }
                            }
                            catch (System.Text.RegularExpressions.RegexParseException)
                            {
                                // Invalid pattern in rule; skip
                            }
                        }
                    }
                }
            }

            return suggestions;
        }

        /// <summary>
        /// Gets available review templates.
        /// </summary>
        public List<ReviewTemplate> GetReviewTemplates()
        {
            lock (_lockObject)
            {
                return _reviewTemplates.Values.ToList();
            }
        }

        /// <summary>
        /// Registers a custom review template.
        /// </summary>
        public void RegisterReviewTemplate(ReviewTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateName))
                throw new ArgumentException("Template must have a non-empty name");

            lock (_lockObject)
            {
                _reviewTemplates[template.TemplateName] = template;
                Logger.Info("Registered review template '{0}'", template.TemplateName);
            }
        }

        private void AddReviewComment(ReviewWorkflow review, string authorUserId, string text)
        {
            var comment = new TagComment
            {
                CommentId = Guid.NewGuid().ToString("N"),
                TagId = review.TagId,
                AuthorUserId = authorUserId,
                AuthorDisplayName = _sessionsByUser.TryGetValue(authorUserId, out var session)
                    ? session.DisplayName : authorUserId,
                Text = text,
                CreatedAt = DateTime.Now,
                ReviewId = review.ReviewId,
                MentionedUserIds = ExtractMentions(text)
            };

            review.Comments.Add(comment);
        }

        #endregion

        #region Standards Enforcement

        /// <summary>
        /// Registers a tag standard for enforcement.
        /// </summary>
        public void RegisterStandard(TagStandard standard)
        {
            if (standard == null)
                throw new ArgumentNullException(nameof(standard));

            lock (_lockObject)
            {
                // Deactivate any existing version of the same standard
                foreach (var existing in _activeStandards.Where(s => s.Name == standard.Name))
                {
                    existing.IsActive = false;
                }

                _activeStandards.Add(standard);
                Logger.Info("Registered tag standard '{0}' v{1} (scope: {2}, {3} rules)",
                    standard.Name, standard.Version, standard.Scope, standard.Rules.Count);
            }
        }

        /// <summary>
        /// Checks a tag against all active standards.
        /// </summary>
        public List<StandardViolation> CheckStandardsCompliance(string tagId, TagInstance tagState)
        {
            var violations = new List<StandardViolation>();

            if (tagState == null) return violations;

            lock (_lockObject)
            {
                foreach (var standard in _activeStandards.Where(s => s.IsActive))
                {
                    foreach (var rule in standard.Rules)
                    {
                        if (!string.IsNullOrEmpty(rule.CategoryFilter) &&
                            !string.Equals(rule.CategoryFilter, tagState.CategoryName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        bool isCompliant = EvaluateStandardRule(rule, tagState);

                        if (!isCompliant)
                        {
                            var violation = new StandardViolation
                            {
                                ViolationId = Guid.NewGuid().ToString("N"),
                                TagId = tagId,
                                StandardName = standard.Name,
                                RuleDescription = rule.Description,
                                Severity = rule.Severity,
                                Details = $"Tag value '{tagState.DisplayText}' does not match expected pattern '{rule.ExpectedPattern}'",
                                DetectedAt = DateTime.Now
                            };

                            violations.Add(violation);
                            _violationHistory.Add(violation);
                        }
                    }
                }
            }

            if (violations.Count > 0)
            {
                Logger.Debug("Tag '{0}' has {1} standards violations", tagId, violations.Count);
            }

            return violations;
        }

        /// <summary>
        /// Checks whether a user has permission to override a standards violation.
        /// </summary>
        public bool CanOverrideViolation(string userId, string violationId)
        {
            lock (_lockObject)
            {
                var violation = _violationHistory.FirstOrDefault(v => v.ViolationId == violationId);
                if (violation == null) return false;

                if (!_sessionsByUser.TryGetValue(userId, out var session)) return false;

                var standard = _activeStandards.FirstOrDefault(s => s.Name == violation.StandardName && s.IsActive);
                if (standard == null) return false;

                return standard.OverridePermittedRoles.Contains(session.Role) || session.Role == TagUserRole.TagAdmin;
            }
        }

        /// <summary>
        /// Records a standards override with reason.
        /// </summary>
        public bool OverrideViolation(string userId, string violationId, string reason)
        {
            lock (_lockObject)
            {
                if (!CanOverrideViolation(userId, violationId))
                {
                    Logger.Warn("User '{0}' does not have permission to override violation '{1}'", userId, violationId);
                    return false;
                }

                var violation = _violationHistory.FirstOrDefault(v => v.ViolationId == violationId);
                if (violation == null) return false;

                violation.IsOverridden = true;
                violation.OverriddenByUserId = userId;
                violation.OverrideReason = reason;

                LogActivity(userId, "StandardOverride", violation.TagId,
                    $"Overrode {violation.StandardName} violation: {reason}", 0);

                Logger.Info("Violation '{0}' overridden by '{1}': {2}", violationId, userId, reason);
                return true;
            }
        }

        /// <summary>
        /// Gets all active standards.
        /// </summary>
        public List<TagStandard> GetActiveStandards()
        {
            lock (_lockObject)
            {
                return _activeStandards.Where(s => s.IsActive).ToList();
            }
        }

        /// <summary>
        /// Generates a compliance report for a specific user.
        /// </summary>
        public Dictionary<string, double> GetComplianceReport(string userId = null)
        {
            lock (_lockObject)
            {
                var report = new Dictionary<string, double>();
                IEnumerable<StandardViolation> violations = _violationHistory;

                // Group by standard
                var byStandard = violations.GroupBy(v => v.StandardName);

                foreach (var group in byStandard)
                {
                    int total = group.Count();
                    int overridden = group.Count(v => v.IsOverridden);
                    int unresolved = total - overridden;

                    // Compliance rate: 1.0 = no violations; 0.0 = all violations
                    double rate = total > 0 ? 1.0 - ((double)unresolved / Math.Max(total * 2, 1)) : 1.0;
                    report[group.Key] = Math.Max(0.0, Math.Min(1.0, rate));
                }

                return report;
            }
        }

        private bool EvaluateStandardRule(TagStandardRule rule, TagInstance tagState)
        {
            if (string.IsNullOrEmpty(rule.ExpectedPattern))
                return true;

            string valueToCheck = null;

            switch (rule.PropertyPath?.ToLowerInvariant())
            {
                case "displaytext":
                case "text":
                    valueToCheck = tagState.DisplayText;
                    break;
                case "tagfamilyname":
                case "family":
                    valueToCheck = tagState.TagFamilyName;
                    break;
                case "tagtypename":
                case "type":
                    valueToCheck = tagState.TagTypeName;
                    break;
                case "contentexpression":
                case "expression":
                    valueToCheck = tagState.ContentExpression;
                    break;
                default:
                    valueToCheck = tagState.DisplayText;
                    break;
            }

            if (string.IsNullOrEmpty(valueToCheck))
                return false;

            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(valueToCheck, rule.ExpectedPattern);
            }
            catch (System.Text.RegularExpressions.RegexParseException ex)
            {
                Logger.Warn("Invalid regex pattern in standard rule '{0}': {1}", rule.RuleId, ex.Message);
                return true; // Assume compliant if pattern is invalid
            }
        }

        #endregion

        #region Team Analytics

        /// <summary>
        /// Generates a comprehensive team analytics report for the specified period.
        /// </summary>
        public async Task<TeamAnalyticsReport> GenerateTeamAnalyticsAsync(
            DateTime periodStart,
            DateTime periodEnd,
            IProgress<CollaborationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating team analytics report for {0} to {1}", periodStart, periodEnd);
            var sw = Stopwatch.StartNew();

            var report = new TeamAnalyticsReport
            {
                GeneratedAt = DateTime.Now,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Analyzing user activity",
                        Current = 1, Total = 6,
                        StatusMessage = "Counting user contributions"
                    });

                    // Active users
                    var periodEntries = _globalActivityLog
                        .Where(e => e.Timestamp >= periodStart && e.Timestamp <= periodEnd)
                        .ToList();

                    var activeUsers = periodEntries.Select(e => e.UserId).Distinct().ToList();
                    report.ActiveUserCount = activeUsers.Count;

                    // Tags created/modified per user
                    foreach (var userId in activeUsers)
                    {
                        var userEntries = periodEntries.Where(e => e.UserId == userId).ToList();

                        report.TagsCreatedPerUser[userId] = userEntries.Count(e => e.Action == "TagCreated");
                        report.TagsModifiedPerUser[userId] = userEntries.Count(e =>
                            e.Action == "TagModified" || e.Action == "TagMoved");
                    }

                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Computing quality scores",
                        Current = 2, Total = 6,
                        StatusMessage = "Analyzing per-user quality"
                    });

                    // Quality scores per user
                    foreach (var kvp in _qualityScores)
                    {
                        if (kvp.Value.Count > 0)
                        {
                            report.QualityScorePerUser[kvp.Key] = kvp.Value.Average();
                        }
                    }

                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Analyzing conflicts",
                        Current = 3, Total = 6,
                        StatusMessage = "Counting conflict patterns"
                    });

                    // Conflict analysis
                    var periodConflicts = _conflictHistory
                        .Where(c => c.DetectedAt >= periodStart && c.DetectedAt <= periodEnd)
                        .ToList();

                    report.TotalConflicts = periodConflicts.Count;
                    report.AutoResolvedConflicts = periodConflicts
                        .Count(c => c.Resolution?.ResolvedByUserId == "System");

                    // Conflict frequency per user pair
                    foreach (var conflict in periodConflicts)
                    {
                        string pairKey = string.Compare(conflict.FirstUserId, conflict.SecondUserId, StringComparison.OrdinalIgnoreCase) < 0
                            ? $"{conflict.FirstUserId}|{conflict.SecondUserId}"
                            : $"{conflict.SecondUserId}|{conflict.FirstUserId}";

                        if (!report.ConflictsPerUserPair.ContainsKey(pairKey))
                            report.ConflictsPerUserPair[pairKey] = 0;
                        report.ConflictsPerUserPair[pairKey]++;
                    }

                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Review metrics",
                        Current = 4, Total = 6,
                        StatusMessage = "Computing review turnaround"
                    });

                    // Review metrics
                    var (avgTurnaround, rejectionRate, _) = GetReviewMetrics(periodStart);
                    report.AverageReviewTurnaround = avgTurnaround;
                    report.ReviewRejectionRate = rejectionRate;

                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Compliance analysis",
                        Current = 5, Total = 6,
                        StatusMessage = "Computing compliance rates"
                    });

                    // Compliance rates per user
                    var periodViolations = _violationHistory
                        .Where(v => v.DetectedAt >= periodStart && v.DetectedAt <= periodEnd)
                        .ToList();

                    foreach (var userId in activeUsers)
                    {
                        int userCreated = report.TagsCreatedPerUser.GetValueOrDefault(userId, 0) +
                                          report.TagsModifiedPerUser.GetValueOrDefault(userId, 0);
                        // Approximate: fewer violations per action = higher compliance
                        int userViolations = periodViolations.Count(); // Simplified: full per-user tracking needs tag-to-user mapping
                        double compliance = userCreated > 0
                            ? Math.Max(0, 1.0 - (double)userViolations / (userCreated * activeUsers.Count))
                            : 1.0;
                        report.ComplianceRatePerUser[userId] = Math.Min(1.0, compliance);
                    }

                    progress?.Report(new CollaborationProgress
                    {
                        Operation = "Bottleneck detection",
                        Current = 6, Total = 6,
                        StatusMessage = "Identifying bottlenecks"
                    });

                    // Bottleneck detection
                    if (report.AverageReviewTurnaround > TimeSpan.FromHours(24))
                    {
                        report.Bottlenecks.Add($"Review turnaround exceeds 24 hours (avg: {report.AverageReviewTurnaround.TotalHours:F1}h)");
                    }

                    if (report.ReviewRejectionRate > 0.3)
                    {
                        report.Bottlenecks.Add($"High rejection rate: {report.ReviewRejectionRate:P0}");
                    }

                    var highConflictPairs = report.ConflictsPerUserPair
                        .Where(kvp => kvp.Value > 5)
                        .ToList();
                    foreach (var pair in highConflictPairs)
                    {
                        report.Bottlenecks.Add($"Frequent conflicts between {pair.Key} ({pair.Value} conflicts)");
                    }

                    // View assignments (approximate from activity)
                    foreach (var userId in activeUsers)
                    {
                        var userViews = periodEntries
                            .Where(e => e.UserId == userId && e.ViewId != 0)
                            .Select(e => e.ViewId)
                            .Distinct()
                            .ToList();
                        report.UserViewAssignments[userId] = userViews;
                    }

                    // Productivity index (tags per hour, approximate)
                    foreach (var userId in activeUsers)
                    {
                        if (_sessionsByUser.TryGetValue(userId, out var session))
                        {
                            double hoursActive = (session.LastActivity - session.SessionStart).TotalHours;
                            if (hoursActive > 0)
                            {
                                int totalTags = report.TagsCreatedPerUser.GetValueOrDefault(userId, 0) +
                                                report.TagsModifiedPerUser.GetValueOrDefault(userId, 0);
                                report.ProductivityIndex[userId] = totalTags / hoursActive;
                            }
                        }
                    }
                }
            }, cancellationToken);

            sw.Stop();
            Logger.Info("Team analytics report generated in {0}ms: {1} users, {2} conflicts",
                sw.ElapsedMilliseconds, report.ActiveUserCount, report.TotalConflicts);

            return report;
        }

        /// <summary>
        /// Records a quality score for a user's tag operation.
        /// </summary>
        public void RecordQualityScore(string userId, double score)
        {
            lock (_lockObject)
            {
                if (!_qualityScores.TryGetValue(userId, out var scores))
                {
                    scores = new List<double>();
                    _qualityScores[userId] = scores;
                }

                scores.Add(Math.Max(0, Math.Min(100, score)));
            }
        }

        /// <summary>
        /// Records a tag creation event for analytics tracking.
        /// </summary>
        public void RecordTagCreated(string userId, string tagId, int viewId)
        {
            lock (_lockObject)
            {
                if (!_tagsCreatedCount.ContainsKey(userId))
                    _tagsCreatedCount[userId] = 0;
                _tagsCreatedCount[userId]++;
            }

            LogActivity(userId, "TagCreated", tagId, "Tag created", viewId);
        }

        /// <summary>
        /// Records a tag modification event for analytics tracking.
        /// </summary>
        public void RecordTagModified(string userId, string tagId, int viewId, string changeDescription)
        {
            lock (_lockObject)
            {
                if (!_tagsModifiedCount.ContainsKey(userId))
                    _tagsModifiedCount[userId] = 0;
                _tagsModifiedCount[userId]++;
            }

            LogActivity(userId, "TagModified", tagId, changeDescription, viewId);
        }

        #endregion

        #region Communication

        /// <summary>
        /// Adds a comment to a specific tag.
        /// </summary>
        public TagComment AddComment(string tagId, string authorUserId, string text, bool flagAsIssue = false)
        {
            if (string.IsNullOrWhiteSpace(tagId))
                throw new ArgumentNullException(nameof(tagId));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            lock (_lockObject)
            {
                var comment = new TagComment
                {
                    CommentId = Guid.NewGuid().ToString("N"),
                    TagId = tagId,
                    AuthorUserId = authorUserId,
                    AuthorDisplayName = _sessionsByUser.TryGetValue(authorUserId, out var session)
                        ? session.DisplayName : authorUserId,
                    Text = text,
                    CreatedAt = DateTime.Now,
                    IsIssueFlagged = flagAsIssue,
                    MentionedUserIds = ExtractMentions(text)
                };

                if (!_commentsByTag.TryGetValue(tagId, out var comments))
                {
                    comments = new List<TagComment>();
                    _commentsByTag[tagId] = comments;
                }
                comments.Add(comment);

                // Notify mentioned users
                foreach (var mentionedUserId in comment.MentionedUserIds)
                {
                    BroadcastNotification(TagNotificationType.MentionReceived, authorUserId, mentionedUserId,
                        $"{comment.AuthorDisplayName} mentioned you in a comment on tag '{tagId}'", tagId);
                }

                if (flagAsIssue)
                {
                    BroadcastNotification(TagNotificationType.IssueFlagged, authorUserId, null,
                        $"Issue flagged on tag '{tagId}': {text}", tagId);
                }
                else
                {
                    BroadcastNotification(TagNotificationType.CommentAdded, authorUserId, null,
                        $"Comment added on tag '{tagId}'", tagId);
                }

                LogActivity(authorUserId, flagAsIssue ? "IssueFlagged" : "CommentAdded", tagId, text, 0);
                Logger.Debug("Comment '{0}' added to tag '{1}' by '{2}'", comment.CommentId, tagId, authorUserId);

                return comment;
            }
        }

        /// <summary>
        /// Adds a threaded reply to an existing comment.
        /// </summary>
        public TagComment ReplyToComment(string parentCommentId, string authorUserId, string text)
        {
            lock (_lockObject)
            {
                // Find the parent comment
                string tagId = null;
                foreach (var kvp in _commentsByTag)
                {
                    if (kvp.Value.Any(c => c.CommentId == parentCommentId))
                    {
                        tagId = kvp.Key;
                        break;
                    }
                }

                if (tagId == null)
                {
                    Logger.Warn("Cannot reply to non-existent comment '{0}'", parentCommentId);
                    return null;
                }

                var reply = new TagComment
                {
                    CommentId = Guid.NewGuid().ToString("N"),
                    TagId = tagId,
                    AuthorUserId = authorUserId,
                    AuthorDisplayName = _sessionsByUser.TryGetValue(authorUserId, out var session)
                        ? session.DisplayName : authorUserId,
                    Text = text,
                    CreatedAt = DateTime.Now,
                    ParentCommentId = parentCommentId,
                    MentionedUserIds = ExtractMentions(text)
                };

                _commentsByTag[tagId].Add(reply);

                // Notify mentioned users
                foreach (var mentionedUserId in reply.MentionedUserIds)
                {
                    BroadcastNotification(TagNotificationType.MentionReceived, authorUserId, mentionedUserId,
                        $"{reply.AuthorDisplayName} mentioned you in a reply on tag '{tagId}'", tagId);
                }

                Logger.Debug("Reply '{0}' added to comment '{1}' on tag '{2}'", reply.CommentId, parentCommentId, tagId);
                return reply;
            }
        }

        /// <summary>
        /// Gets all comments for a specific tag.
        /// </summary>
        public List<TagComment> GetTagComments(string tagId)
        {
            lock (_lockObject)
            {
                if (_commentsByTag.TryGetValue(tagId, out var comments))
                {
                    return comments.OrderBy(c => c.CreatedAt).ToList();
                }
                return new List<TagComment>();
            }
        }

        /// <summary>
        /// Gets all flagged issues across tags.
        /// </summary>
        public List<TagComment> GetFlaggedIssues()
        {
            lock (_lockObject)
            {
                return _commentsByTag.Values
                    .SelectMany(c => c)
                    .Where(c => c.IsIssueFlagged)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets unread notifications for a user.
        /// </summary>
        public List<TagNotification> GetUnreadNotifications(string userId)
        {
            lock (_lockObject)
            {
                return _notificationHistory
                    .Where(n => n.TargetUserId == userId && !n.IsRead)
                    .OrderByDescending(n => n.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all notifications for a user.
        /// </summary>
        public List<TagNotification> GetNotifications(string userId, int maxEntries = 100)
        {
            lock (_lockObject)
            {
                return _notificationHistory
                    .Where(n => n.TargetUserId == userId || n.TargetUserId == null)
                    .OrderByDescending(n => n.Timestamp)
                    .Take(maxEntries)
                    .ToList();
            }
        }

        /// <summary>
        /// Marks a notification as read.
        /// </summary>
        public void MarkNotificationRead(string notificationId)
        {
            lock (_lockObject)
            {
                var notification = _notificationHistory.FirstOrDefault(n => n.NotificationId == notificationId);
                if (notification != null)
                    notification.IsRead = true;
            }
        }

        /// <summary>
        /// Marks all notifications for a user as read.
        /// </summary>
        public int MarkAllNotificationsRead(string userId)
        {
            int count = 0;
            lock (_lockObject)
            {
                foreach (var n in _notificationHistory.Where(n => n.TargetUserId == userId && !n.IsRead))
                {
                    n.IsRead = true;
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Updates notification preferences for a user.
        /// </summary>
        public void UpdateNotificationPreferences(string userId, NotificationPreferences preferences)
        {
            lock (_lockObject)
            {
                if (_sessionsByUser.TryGetValue(userId, out var session))
                {
                    session.NotificationSettings = preferences ?? new NotificationPreferences();
                    Logger.Debug("Updated notification preferences for user '{0}'", userId);
                }
            }
        }

        /// <summary>
        /// Gets the message history for a specific tag.
        /// </summary>
        public List<TagNotification> GetTagMessageHistory(string tagId, int maxEntries = 50)
        {
            lock (_lockObject)
            {
                return _notificationHistory
                    .Where(n => n.TagId == tagId)
                    .OrderByDescending(n => n.Timestamp)
                    .Take(maxEntries)
                    .ToList();
            }
        }

        #endregion

        #region Private Helpers

        private void LogActivity(string userId, string action, string tagId, string details, int viewId)
        {
            var entry = new ActivityLogEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Action = action,
                TagId = tagId,
                Details = details,
                Timestamp = DateTime.Now,
                ViewId = viewId
            };

            // Add to user session log
            if (_sessionsByUser.TryGetValue(userId, out var session))
            {
                session.ActivityLog.Add(entry);
                session.LastActivity = DateTime.Now;
            }

            // Add to global log
            _globalActivityLog.Add(entry);

            // Trim global log if too large
            if (_globalActivityLog.Count > 10000)
            {
                _globalActivityLog.RemoveRange(0, _globalActivityLog.Count - 10000);
            }
        }

        private void BroadcastNotification(TagNotificationType type, string sourceUserId,
            string targetUserId, string message, string tagId = null)
        {
            var notification = new TagNotification
            {
                NotificationId = Guid.NewGuid().ToString("N"),
                Type = type,
                SourceUserId = sourceUserId,
                TargetUserId = targetUserId,
                TagId = tagId,
                Message = message,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            if (targetUserId != null)
            {
                // Targeted notification
                if (ShouldDeliverNotification(targetUserId, type, sourceUserId))
                {
                    _notificationHistory.Add(notification);
                }
            }
            else
            {
                // Broadcast to all active users
                foreach (var session in _activeSessions.Values.Where(s => s.IsActive))
                {
                    if (ShouldDeliverNotification(session.UserId, type, sourceUserId))
                    {
                        var userNotification = new TagNotification
                        {
                            NotificationId = Guid.NewGuid().ToString("N"),
                            Type = type,
                            SourceUserId = sourceUserId,
                            TargetUserId = session.UserId,
                            TagId = tagId,
                            Message = message,
                            Timestamp = DateTime.Now,
                            IsRead = false
                        };
                        _notificationHistory.Add(userNotification);
                    }
                }
            }

            // Trim notification history if too large
            if (_notificationHistory.Count > 5000)
            {
                _notificationHistory.RemoveRange(0, _notificationHistory.Count - 5000);
            }
        }

        private bool ShouldDeliverNotification(string targetUserId, TagNotificationType type, string sourceUserId)
        {
            if (!_sessionsByUser.TryGetValue(targetUserId, out var session) || !session.IsActive)
                return false;

            var prefs = session.NotificationSettings;

            // Suppress own actions
            if (prefs.SuppressOwnActions && targetUserId == sourceUserId)
                return false;

            return type switch
            {
                TagNotificationType.TagCreated => prefs.NotifyOnTagCreated,
                TagNotificationType.TagDeleted => prefs.NotifyOnTagDeleted,
                TagNotificationType.ConflictDetected => prefs.NotifyOnConflict,
                TagNotificationType.TagReviewRequested => prefs.NotifyOnReviewRequest,
                TagNotificationType.MentionReceived => prefs.NotifyOnMention,
                TagNotificationType.StandardViolation => prefs.NotifyOnStandardViolation,
                TagNotificationType.WorksetBorrowed => prefs.NotifyOnWorksetChange,
                TagNotificationType.WorksetReleased => prefs.NotifyOnWorksetChange,
                _ => true
            };
        }

        private static List<string> ExtractMentions(string text)
        {
            var mentions = new List<string>();
            if (string.IsNullOrEmpty(text)) return mentions;

            // Extract @mention patterns
            int index = 0;
            while (index < text.Length)
            {
                int atIndex = text.IndexOf('@', index);
                if (atIndex < 0) break;

                int start = atIndex + 1;
                int end = start;

                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_' || text[end] == '.'))
                {
                    end++;
                }

                if (end > start)
                {
                    mentions.Add(text.Substring(start, end - start));
                }

                index = end;
            }

            return mentions.Distinct().ToList();
        }

        #endregion
    }
}
