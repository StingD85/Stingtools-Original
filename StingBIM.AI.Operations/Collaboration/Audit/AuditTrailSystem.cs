// ============================================================================
// StingBIM.AI.Collaboration - Audit Trail System
// Comprehensive audit logging, compliance reporting, and tamper detection
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Collaboration.Audit
{
    #region Enums

    /// <summary>
    /// Types of audit events that can be logged
    /// </summary>
    public enum AuditEventType
    {
        Create,
        Read,
        Update,
        Delete,
        Login,
        Logout,
        Export,
        Import,
        Share,
        Approve,
        Reject,
        Comment,
        Upload,
        Download,
        PermissionChange,
        SystemEvent,
        SecurityAlert,
        ConfigurationChange,
        BatchOperation,
        Synchronization,
        Backup,
        Restore,
        Archive,
        Purge,
        Lock,
        Unlock,
        Checkout,
        Checkin,
        Publish,
        Unpublish,
        Subscribe,
        Unsubscribe
    }

    /// <summary>
    /// Severity levels for audit events
    /// </summary>
    public enum AuditSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical,
        Security
    }

    /// <summary>
    /// Entity types that can be audited
    /// </summary>
    public enum AuditEntityType
    {
        User,
        Document,
        Model,
        Element,
        Parameter,
        Schedule,
        Material,
        View,
        Sheet,
        Family,
        Project,
        Workset,
        Phase,
        Level,
        Grid,
        Issue,
        Comment,
        Markup,
        Permission,
        Role,
        Team,
        Workflow,
        Task,
        Notification,
        Configuration,
        System,
        Integration,
        Report,
        Export,
        Import
    }

    /// <summary>
    /// Compliance frameworks supported by the audit system
    /// </summary>
    public enum ComplianceFramework
    {
        SOX,           // Sarbanes-Oxley
        GDPR,          // General Data Protection Regulation
        ISO27001,      // Information Security Management
        ISO19650,      // BIM Information Management
        HIPAA,         // Health Insurance Portability
        PCI_DSS,       // Payment Card Industry
        NIST,          // National Institute of Standards
        SOC2,          // Service Organization Control
        CCPA,          // California Consumer Privacy Act
        Custom
    }

    /// <summary>
    /// Retention policy actions
    /// </summary>
    public enum RetentionAction
    {
        Keep,
        Archive,
        Delete,
        Anonymize
    }

    /// <summary>
    /// Export formats for audit logs
    /// </summary>
    public enum AuditExportFormat
    {
        Json,
        Csv,
        Xml,
        Html,
        Pdf
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Represents a single audit trail entry
    /// </summary>
    public class AuditEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public AuditEventType Action { get; set; }
        public AuditEntityType EntityType { get; set; }
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string SessionId { get; set; }
        public AuditSeverity Severity { get; set; } = AuditSeverity.Info;
        public string Description { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public List<ChangeRecord> Changes { get; set; } = new List<ChangeRecord>();
        public string ParentAuditId { get; set; }
        public List<string> ChildAuditIds { get; set; } = new List<string>();
        public string CorrelationId { get; set; }
        public bool IsSystemGenerated { get; set; }
        public string ApplicationVersion { get; set; }
        public string ModuleName { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string ErrorStackTrace { get; set; }

        // Hash chain for tamper detection
        public string PreviousHash { get; set; }
        public string CurrentHash { get; set; }
        public long SequenceNumber { get; set; }

        // Compliance flags
        public bool ContainsPII { get; set; }
        public bool ContainsSensitiveData { get; set; }
        public List<ComplianceFramework> ApplicableFrameworks { get; set; } = new List<ComplianceFramework>();

        /// <summary>
        /// Create a masked copy of this entry for external sharing
        /// </summary>
        public AuditEntry CreateMaskedCopy(SensitiveDataMasker masker)
        {
            var copy = (AuditEntry)MemberwiseClone();
            copy.IpAddress = masker.MaskIpAddress(IpAddress);
            copy.UserEmail = masker.MaskEmail(UserEmail);
            copy.Metadata = masker.MaskDictionary(Metadata);
            copy.Changes = Changes.Select(c => c.CreateMaskedCopy(masker)).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Represents a data change within an audit entry
    /// </summary>
    public class ChangeRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FieldName { get; set; }
        public string FieldPath { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string DataType { get; set; }
        public bool IsSensitive { get; set; }
        public string ChangeReason { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public ChangeRecord CreateMaskedCopy(SensitiveDataMasker masker)
        {
            var copy = (ChangeRecord)MemberwiseClone();
            if (IsSensitive)
            {
                copy.OldValue = masker.MaskSensitiveValue(OldValue);
                copy.NewValue = masker.MaskSensitiveValue(NewValue);
            }
            return copy;
        }
    }

    /// <summary>
    /// Query parameters for searching audit trails
    /// </summary>
    public class AuditQuery
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string> UserIds { get; set; } = new List<string>();
        public List<AuditEventType> Actions { get; set; } = new List<AuditEventType>();
        public List<AuditEntityType> EntityTypes { get; set; } = new List<AuditEntityType>();
        public List<string> EntityIds { get; set; } = new List<string>();
        public List<AuditSeverity> Severities { get; set; } = new List<AuditSeverity>();
        public string SearchText { get; set; }
        public bool IncludeSystemEvents { get; set; } = true;
        public bool SuccessOnly { get; set; }
        public bool FailuresOnly { get; set; }
        public string IpAddressPattern { get; set; }
        public string SessionId { get; set; }
        public string CorrelationId { get; set; }
        public List<ComplianceFramework> Frameworks { get; set; } = new List<ComplianceFramework>();
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
        public string SortBy { get; set; } = "Timestamp";
        public bool SortDescending { get; set; } = true;

        public bool Matches(AuditEntry entry)
        {
            if (StartDate.HasValue && entry.Timestamp < StartDate.Value)
                return false;
            if (EndDate.HasValue && entry.Timestamp > EndDate.Value)
                return false;
            if (UserIds.Any() && !UserIds.Contains(entry.UserId))
                return false;
            if (Actions.Any() && !Actions.Contains(entry.Action))
                return false;
            if (EntityTypes.Any() && !EntityTypes.Contains(entry.EntityType))
                return false;
            if (EntityIds.Any() && !EntityIds.Contains(entry.EntityId))
                return false;
            if (Severities.Any() && !Severities.Contains(entry.Severity))
                return false;
            if (!IncludeSystemEvents && entry.IsSystemGenerated)
                return false;
            if (SuccessOnly && !entry.Success)
                return false;
            if (FailuresOnly && entry.Success)
                return false;
            if (!string.IsNullOrEmpty(SessionId) && entry.SessionId != SessionId)
                return false;
            if (!string.IsNullOrEmpty(CorrelationId) && entry.CorrelationId != CorrelationId)
                return false;
            if (Frameworks.Any() && !entry.ApplicableFrameworks.Intersect(Frameworks).Any())
                return false;
            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                if (!entry.Description?.ToLowerInvariant().Contains(searchLower) == true &&
                    !entry.EntityName?.ToLowerInvariant().Contains(searchLower) == true &&
                    !entry.UserName?.ToLowerInvariant().Contains(searchLower) == true)
                    return false;
            }
            if (!string.IsNullOrEmpty(IpAddressPattern))
            {
                try
                {
                    if (!Regex.IsMatch(entry.IpAddress ?? "", IpAddressPattern))
                        return false;
                }
                catch { return false; }
            }

            return true;
        }
    }

    /// <summary>
    /// Audit report with summary statistics
    /// </summary>
    public class AuditReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string GeneratedBy { get; set; }
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }
        public ComplianceFramework? Framework { get; set; }

        // Summary statistics
        public long TotalEvents { get; set; }
        public long UniqueUsers { get; set; }
        public long UniqueEntities { get; set; }
        public long SuccessfulEvents { get; set; }
        public long FailedEvents { get; set; }
        public long SecurityEvents { get; set; }
        public long DataChanges { get; set; }

        // Breakdowns
        public Dictionary<AuditEventType, long> EventsByType { get; set; } = new Dictionary<AuditEventType, long>();
        public Dictionary<AuditEntityType, long> EventsByEntity { get; set; } = new Dictionary<AuditEntityType, long>();
        public Dictionary<AuditSeverity, long> EventsBySeverity { get; set; } = new Dictionary<AuditSeverity, long>();
        public Dictionary<string, long> EventsByUser { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> EventsByHour { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> EventsByDay { get; set; } = new Dictionary<string, long>();

        // Top items
        public List<UserActivitySummary> TopActiveUsers { get; set; } = new List<UserActivitySummary>();
        public List<EntityActivitySummary> MostAccessedEntities { get; set; } = new List<EntityActivitySummary>();
        public List<AuditEntry> RecentSecurityEvents { get; set; } = new List<AuditEntry>();
        public List<AuditEntry> RecentFailures { get; set; } = new List<AuditEntry>();

        // Compliance checks
        public List<ComplianceCheckResult> ComplianceResults { get; set; } = new List<ComplianceCheckResult>();
        public double OverallComplianceScore { get; set; }
        public List<string> ComplianceIssues { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();

        // Data integrity
        public bool HashChainValid { get; set; }
        public long TamperedRecords { get; set; }
        public DateTime? LastIntegrityCheck { get; set; }
    }

    /// <summary>
    /// Summary of user activity
    /// </summary>
    public class UserActivitySummary
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public long TotalActions { get; set; }
        public long Creates { get; set; }
        public long Reads { get; set; }
        public long Updates { get; set; }
        public long Deletes { get; set; }
        public DateTime FirstActivity { get; set; }
        public DateTime LastActivity { get; set; }
        public List<string> IpAddresses { get; set; } = new List<string>();
        public long FailedActions { get; set; }
        public long SecurityEvents { get; set; }
    }

    /// <summary>
    /// Summary of entity activity
    /// </summary>
    public class EntityActivitySummary
    {
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public AuditEntityType EntityType { get; set; }
        public long TotalAccesses { get; set; }
        public long UniqueUsers { get; set; }
        public DateTime FirstAccess { get; set; }
        public DateTime LastAccess { get; set; }
        public long Modifications { get; set; }
    }

    /// <summary>
    /// Result of a compliance check
    /// </summary>
    public class ComplianceCheckResult
    {
        public string CheckId { get; set; }
        public string CheckName { get; set; }
        public string Description { get; set; }
        public ComplianceFramework Framework { get; set; }
        public bool Passed { get; set; }
        public string Details { get; set; }
        public List<string> AffectedRecords { get; set; } = new List<string>();
        public string Remediation { get; set; }
        public AuditSeverity Severity { get; set; }
    }

    /// <summary>
    /// Retention policy configuration
    /// </summary>
    public class RetentionPolicy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int RetentionDays { get; set; } = 365;
        public RetentionAction ActionAfterRetention { get; set; } = RetentionAction.Archive;
        public List<AuditEventType> ApplicableEvents { get; set; } = new List<AuditEventType>();
        public List<AuditEntityType> ApplicableEntities { get; set; } = new List<AuditEntityType>();
        public List<AuditSeverity> ExcludedSeverities { get; set; } = new List<AuditSeverity>();
        public List<ComplianceFramework> RequiredFrameworks { get; set; } = new List<ComplianceFramework>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; }
        public DateTime? LastExecuted { get; set; }
        public long LastExecutionRecordsProcessed { get; set; }

        public bool AppliesToEntry(AuditEntry entry)
        {
            if (ApplicableEvents.Any() && !ApplicableEvents.Contains(entry.Action))
                return false;
            if (ApplicableEntities.Any() && !ApplicableEntities.Contains(entry.EntityType))
                return false;
            if (ExcludedSeverities.Contains(entry.Severity))
                return false;
            if (RequiredFrameworks.Any() && entry.ApplicableFrameworks.Intersect(RequiredFrameworks).Any())
                return false; // Don't apply to compliance-required records
            return true;
        }
    }

    /// <summary>
    /// Result of applying a retention policy
    /// </summary>
    public class RetentionResult
    {
        public string PolicyId { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public long RecordsProcessed { get; set; }
        public long RecordsArchived { get; set; }
        public long RecordsDeleted { get; set; }
        public long RecordsAnonymized { get; set; }
        public long RecordsKept { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Archived audit records batch
    /// </summary>
    public class AuditArchive
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
        public string ArchivedBy { get; set; }
        public DateTime OriginalStartDate { get; set; }
        public DateTime OriginalEndDate { get; set; }
        public long RecordCount { get; set; }
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public string Checksum { get; set; }
        public bool IsCompressed { get; set; }
        public bool IsEncrypted { get; set; }
        public string EncryptionKeyId { get; set; }
    }

    /// <summary>
    /// Security context for audit operations
    /// </summary>
    public class AuditSecurityContext
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> Permissions { get; set; } = new List<string>();
        public string IpAddress { get; set; }
        public string SessionId { get; set; }

        public bool CanViewAuditLogs => Permissions.Contains("audit.read") || Roles.Contains("AuditAdmin");
        public bool CanExportAuditLogs => Permissions.Contains("audit.export") || Roles.Contains("AuditAdmin");
        public bool CanManageRetention => Permissions.Contains("audit.manage") || Roles.Contains("AuditAdmin");
        public bool CanViewSensitiveData => Permissions.Contains("audit.sensitive") || Roles.Contains("SecurityAdmin");
    }

    #endregion

    #region Sensitive Data Masking

    /// <summary>
    /// Utility for masking sensitive data in audit logs
    /// </summary>
    public class SensitiveDataMasker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly HashSet<string> _sensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "secret", "token", "key", "credential", "ssn", "socialsecurity",
            "creditcard", "cardnumber", "cvv", "pin", "accountnumber", "routingnumber",
            "apikey", "privatekey", "encryptionkey", "authtoken", "accesstoken", "refreshtoken"
        };

        private readonly Regex _emailRegex = new Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

        private readonly Regex _ipv4Regex = new Regex(
            @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
            RegexOptions.Compiled);

        private readonly Regex _phoneRegex = new Regex(
            @"\b[\+]?[(]?[0-9]{1,3}[)]?[-\s\.]?[(]?[0-9]{1,4}[)]?[-\s\.]?[0-9]{1,4}[-\s\.]?[0-9]{1,9}\b",
            RegexOptions.Compiled);

        public string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return email;

            var atIndex = email.IndexOf('@');
            if (atIndex <= 1) return "***@***";

            var localPart = email.Substring(0, atIndex);
            var domain = email.Substring(atIndex);

            if (localPart.Length <= 2)
                return "**" + domain;

            return localPart[0] + new string('*', localPart.Length - 2) + localPart[localPart.Length - 1] + domain;
        }

        public string MaskIpAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return ipAddress;

            var parts = ipAddress.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.xxx.xxx";
            }

            // IPv6 - mask last half
            if (ipAddress.Contains(':'))
            {
                var colonIndex = ipAddress.LastIndexOf(':');
                if (colonIndex > 0)
                    return ipAddress.Substring(0, colonIndex) + ":xxxx:xxxx";
            }

            return ipAddress;
        }

        public string MaskSensitiveValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= 4) return new string('*', value.Length);

            return value.Substring(0, 2) + new string('*', value.Length - 4) + value.Substring(value.Length - 2);
        }

        public string MaskPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return phone;

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return new string('*', phone.Length);

            return new string('*', digits.Length - 4) + digits.Substring(digits.Length - 4);
        }

        public Dictionary<string, object> MaskDictionary(Dictionary<string, object> data)
        {
            if (data == null) return null;

            var masked = new Dictionary<string, object>();
            foreach (var kvp in data)
            {
                if (IsSensitiveFieldName(kvp.Key))
                {
                    masked[kvp.Key] = "[REDACTED]";
                }
                else if (kvp.Value is string strValue)
                {
                    masked[kvp.Key] = MaskStringValue(strValue);
                }
                else if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    masked[kvp.Key] = MaskDictionary(nestedDict);
                }
                else
                {
                    masked[kvp.Key] = kvp.Value;
                }
            }
            return masked;
        }

        public bool IsSensitiveFieldName(string fieldName)
        {
            return _sensitiveFieldNames.Any(s =>
                fieldName.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        private string MaskStringValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            // Mask emails
            value = _emailRegex.Replace(value, m => MaskEmail(m.Value));

            // Mask phone numbers
            value = _phoneRegex.Replace(value, m => MaskPhoneNumber(m.Value));

            return value;
        }
    }

    #endregion

    #region Hash Chain Management

    /// <summary>
    /// Manages the cryptographic hash chain for tamper detection
    /// </summary>
    public class AuditHashChain
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _hashLock = new object();
        private string _lastHash = string.Empty;
        private long _sequenceNumber = 0;

        public string ComputeEntryHash(AuditEntry entry)
        {
            lock (_hashLock)
            {
                var dataToHash = new StringBuilder();
                dataToHash.Append(entry.Id);
                dataToHash.Append(entry.UserId);
                dataToHash.Append(entry.Action.ToString());
                dataToHash.Append(entry.EntityType.ToString());
                dataToHash.Append(entry.EntityId);
                dataToHash.Append(entry.Timestamp.ToString("O"));
                dataToHash.Append(entry.Description);
                dataToHash.Append(_lastHash);

                // Include changes in hash
                foreach (var change in entry.Changes.OrderBy(c => c.FieldName))
                {
                    dataToHash.Append(change.FieldName);
                    dataToHash.Append(change.OldValue);
                    dataToHash.Append(change.NewValue);
                }

                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash.ToString()));
                var hash = Convert.ToBase64String(hashBytes);

                entry.PreviousHash = _lastHash;
                entry.CurrentHash = hash;
                entry.SequenceNumber = ++_sequenceNumber;

                _lastHash = hash;

                return hash;
            }
        }

        public bool VerifyEntryHash(AuditEntry entry, string expectedPreviousHash)
        {
            var dataToHash = new StringBuilder();
            dataToHash.Append(entry.Id);
            dataToHash.Append(entry.UserId);
            dataToHash.Append(entry.Action.ToString());
            dataToHash.Append(entry.EntityType.ToString());
            dataToHash.Append(entry.EntityId);
            dataToHash.Append(entry.Timestamp.ToString("O"));
            dataToHash.Append(entry.Description);
            dataToHash.Append(expectedPreviousHash);

            foreach (var change in entry.Changes.OrderBy(c => c.FieldName))
            {
                dataToHash.Append(change.FieldName);
                dataToHash.Append(change.OldValue);
                dataToHash.Append(change.NewValue);
            }

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash.ToString()));
            var computedHash = Convert.ToBase64String(hashBytes);

            return computedHash == entry.CurrentHash && entry.PreviousHash == expectedPreviousHash;
        }

        public void Initialize(string lastHash, long sequenceNumber)
        {
            lock (_hashLock)
            {
                _lastHash = lastHash;
                _sequenceNumber = sequenceNumber;
                Logger.Info($"Hash chain initialized at sequence {sequenceNumber}");
            }
        }

        public (string Hash, long Sequence) GetCurrentState()
        {
            lock (_hashLock)
            {
                return (_lastHash, _sequenceNumber);
            }
        }
    }

    #endregion

    #region Main Audit Trail System

    /// <summary>
    /// Comprehensive audit trail system for StingBIM collaboration platform
    /// Provides activity logging, compliance reporting, and tamper-proof storage
    /// </summary>
    public sealed class AuditTrailSystem : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<AuditTrailSystem> _instance =
            new Lazy<AuditTrailSystem>(() => new AuditTrailSystem());

        public static AuditTrailSystem Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, AuditEntry> _auditLog;
        private readonly ConcurrentDictionary<string, RetentionPolicy> _retentionPolicies;
        private readonly ConcurrentDictionary<string, AuditArchive> _archives;
        private readonly ConcurrentQueue<AuditEntry> _pendingWrites;

        private readonly AuditHashChain _hashChain;
        private readonly SensitiveDataMasker _dataMasker;

        private readonly SemaphoreSlim _writeSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundWriteTask;

        private readonly string _storageBasePath;
        private readonly string _archivePath;

        private bool _isDisposed;
        private bool _isInitialized;

        // Configuration
        public int BatchWriteSize { get; set; } = 100;
        public int BatchWriteIntervalMs { get; set; } = 5000;
        public bool EnableAsyncWriting { get; set; } = true;
        public bool EnableHashChain { get; set; } = true;
        public string ApplicationVersion { get; set; } = "7.0.0";

        private AuditTrailSystem()
        {
            _auditLog = new ConcurrentDictionary<string, AuditEntry>();
            _retentionPolicies = new ConcurrentDictionary<string, RetentionPolicy>();
            _archives = new ConcurrentDictionary<string, AuditArchive>();
            _pendingWrites = new ConcurrentQueue<AuditEntry>();

            _hashChain = new AuditHashChain();
            _dataMasker = new SensitiveDataMasker();

            _writeSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storageBasePath = Path.Combine(appData, "StingBIM", "AuditLogs");
            _archivePath = Path.Combine(_storageBasePath, "Archives");

            Directory.CreateDirectory(_storageBasePath);
            Directory.CreateDirectory(_archivePath);

            _backgroundWriteTask = Task.Run(BackgroundWriteLoop);

            Logger.Info("AuditTrailSystem initialized");
        }

        #region Initialization

        /// <summary>
        /// Initialize the audit system with optional existing state
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            try
            {
                // Load existing audit logs from storage
                await LoadAuditLogsFromStorageAsync(cancellationToken);

                // Load retention policies
                await LoadRetentionPoliciesAsync(cancellationToken);

                // Initialize hash chain from last entry
                var lastEntry = _auditLog.Values
                    .OrderByDescending(e => e.SequenceNumber)
                    .FirstOrDefault();

                if (lastEntry != null)
                {
                    _hashChain.Initialize(lastEntry.CurrentHash, lastEntry.SequenceNumber);
                }

                _isInitialized = true;
                Logger.Info($"Audit system initialized with {_auditLog.Count} existing entries");

                // Log system start
                await LogSecurityEventAsync(
                    null, "System", AuditSeverity.Info,
                    "Audit trail system initialized",
                    new Dictionary<string, object>
                    {
                        ["ExistingEntries"] = _auditLog.Count,
                        ["RetentionPolicies"] = _retentionPolicies.Count
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize audit system");
                throw;
            }
        }

        private async Task LoadAuditLogsFromStorageAsync(CancellationToken cancellationToken)
        {
            var logFiles = Directory.GetFiles(_storageBasePath, "audit_*.json");

            foreach (var file in logFiles.OrderBy(f => f))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var entries = JsonSerializer.Deserialize<List<AuditEntry>>(json);

                    foreach (var entry in entries ?? Enumerable.Empty<AuditEntry>())
                    {
                        _auditLog.TryAdd(entry.Id, entry);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to load audit log file: {file}");
                }
            }
        }

        private async Task LoadRetentionPoliciesAsync(CancellationToken cancellationToken)
        {
            var policyFile = Path.Combine(_storageBasePath, "retention_policies.json");

            if (File.Exists(policyFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(policyFile, cancellationToken);
                    var policies = JsonSerializer.Deserialize<List<RetentionPolicy>>(json);

                    foreach (var policy in policies ?? Enumerable.Empty<RetentionPolicy>())
                    {
                        _retentionPolicies.TryAdd(policy.Id, policy);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to load retention policies");
                }
            }

            // Create default policies if none exist
            if (!_retentionPolicies.Any())
            {
                CreateDefaultRetentionPolicies();
            }
        }

        private void CreateDefaultRetentionPolicies()
        {
            // Default policy - 1 year retention
            var defaultPolicy = new RetentionPolicy
            {
                Id = "default",
                Name = "Default Retention",
                Description = "Default 1-year retention for all audit events",
                RetentionDays = 365,
                ActionAfterRetention = RetentionAction.Archive
            };

            // Security events - 7 years
            var securityPolicy = new RetentionPolicy
            {
                Id = "security",
                Name = "Security Event Retention",
                Description = "7-year retention for security events (SOX compliance)",
                RetentionDays = 2555,
                ActionAfterRetention = RetentionAction.Archive,
                ApplicableEvents = new List<AuditEventType>
                {
                    AuditEventType.Login, AuditEventType.Logout,
                    AuditEventType.PermissionChange, AuditEventType.SecurityAlert
                },
                RequiredFrameworks = new List<ComplianceFramework> { ComplianceFramework.SOX }
            };

            // GDPR data - special handling
            var gdprPolicy = new RetentionPolicy
            {
                Id = "gdpr",
                Name = "GDPR Data Retention",
                Description = "GDPR-compliant data retention with anonymization",
                RetentionDays = 730,
                ActionAfterRetention = RetentionAction.Anonymize,
                RequiredFrameworks = new List<ComplianceFramework> { ComplianceFramework.GDPR }
            };

            _retentionPolicies.TryAdd(defaultPolicy.Id, defaultPolicy);
            _retentionPolicies.TryAdd(securityPolicy.Id, securityPolicy);
            _retentionPolicies.TryAdd(gdprPolicy.Id, gdprPolicy);
        }

        #endregion

        #region Activity Logging

        /// <summary>
        /// Log a general activity
        /// </summary>
        public async Task<string> LogActivityAsync(
            string userId,
            string userName,
            AuditEventType action,
            AuditEntityType entityType,
            string entityId,
            string entityName = null,
            string description = null,
            Dictionary<string, object> metadata = null,
            string ipAddress = null,
            string sessionId = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Description = description ?? GenerateDefaultDescription(action, entityType, entityName),
                Metadata = metadata ?? new Dictionary<string, object>(),
                IpAddress = ipAddress,
                SessionId = sessionId,
                ApplicationVersion = ApplicationVersion,
                ModuleName = "StingBIM.AI.Collaboration"
            };

            // Determine applicable compliance frameworks
            DetermineComplianceFrameworks(entry);

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a data change with before/after values
        /// </summary>
        public async Task<string> LogDataChangeAsync(
            string userId,
            string userName,
            AuditEntityType entityType,
            string entityId,
            string entityName,
            List<ChangeRecord> changes,
            string description = null,
            Dictionary<string, object> metadata = null,
            string ipAddress = null,
            string sessionId = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.Update,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Description = description ?? $"Modified {entityType} '{entityName}'",
                Changes = changes ?? new List<ChangeRecord>(),
                Metadata = metadata ?? new Dictionary<string, object>(),
                IpAddress = ipAddress,
                SessionId = sessionId,
                ApplicationVersion = ApplicationVersion,
                ModuleName = "StingBIM.AI.Collaboration"
            };

            // Check for sensitive data in changes
            entry.ContainsSensitiveData = changes?.Any(c => c.IsSensitive) ?? false;

            DetermineComplianceFrameworks(entry);

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a security-related event
        /// </summary>
        public async Task<string> LogSecurityEventAsync(
            string userId,
            string userName,
            AuditSeverity severity,
            string description,
            Dictionary<string, object> metadata = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.SecurityAlert,
                EntityType = AuditEntityType.System,
                EntityId = "system",
                Severity = severity,
                Description = description,
                Metadata = metadata ?? new Dictionary<string, object>(),
                IsSystemGenerated = string.IsNullOrEmpty(userId),
                ApplicationVersion = ApplicationVersion,
                ModuleName = "StingBIM.AI.Collaboration"
            };

            entry.ApplicableFrameworks.Add(ComplianceFramework.SOX);
            entry.ApplicableFrameworks.Add(ComplianceFramework.ISO27001);

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a login event
        /// </summary>
        public async Task<string> LogLoginAsync(
            string userId,
            string userName,
            string ipAddress,
            string userAgent,
            string sessionId,
            bool success,
            string failureReason = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.Login,
                EntityType = AuditEntityType.User,
                EntityId = userId,
                EntityName = userName,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                SessionId = sessionId,
                Success = success,
                ErrorMessage = failureReason,
                Severity = success ? AuditSeverity.Info : AuditSeverity.Warning,
                Description = success
                    ? $"User '{userName}' logged in successfully"
                    : $"Failed login attempt for user '{userName}': {failureReason}",
                ApplicationVersion = ApplicationVersion
            };

            entry.ApplicableFrameworks.Add(ComplianceFramework.SOX);
            entry.ApplicableFrameworks.Add(ComplianceFramework.ISO27001);

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a logout event
        /// </summary>
        public async Task<string> LogLogoutAsync(
            string userId,
            string userName,
            string sessionId,
            TimeSpan sessionDuration,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.Logout,
                EntityType = AuditEntityType.User,
                EntityId = userId,
                EntityName = userName,
                SessionId = sessionId,
                Duration = sessionDuration,
                Description = $"User '{userName}' logged out after {sessionDuration.TotalMinutes:F1} minutes",
                ApplicationVersion = ApplicationVersion
            };

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a permission change
        /// </summary>
        public async Task<string> LogPermissionChangeAsync(
            string userId,
            string userName,
            string targetUserId,
            string targetUserName,
            List<ChangeRecord> permissionChanges,
            string ipAddress = null,
            CancellationToken cancellationToken = default)
        {
            var entry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.PermissionChange,
                EntityType = AuditEntityType.Permission,
                EntityId = targetUserId,
                EntityName = targetUserName,
                Changes = permissionChanges,
                IpAddress = ipAddress,
                Severity = AuditSeverity.Warning,
                Description = $"User '{userName}' modified permissions for '{targetUserName}'",
                ApplicationVersion = ApplicationVersion
            };

            entry.ApplicableFrameworks.Add(ComplianceFramework.SOX);
            entry.ApplicableFrameworks.Add(ComplianceFramework.ISO27001);

            return await AddEntryAsync(entry, cancellationToken);
        }

        /// <summary>
        /// Log a batch operation
        /// </summary>
        public async Task<string> LogBatchOperationAsync(
            string userId,
            string userName,
            AuditEventType operationType,
            AuditEntityType entityType,
            List<string> entityIds,
            string description,
            Dictionary<string, object> metadata = null,
            CancellationToken cancellationToken = default)
        {
            var parentEntry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                Action = AuditEventType.BatchOperation,
                EntityType = entityType,
                EntityId = "batch_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Description = description,
                Metadata = metadata ?? new Dictionary<string, object>
                {
                    ["BatchSize"] = entityIds.Count,
                    ["OperationType"] = operationType.ToString()
                },
                ApplicationVersion = ApplicationVersion
            };

            parentEntry.ChildAuditIds = new List<string>();

            var parentId = await AddEntryAsync(parentEntry, cancellationToken);

            // Log individual operations as children
            var correlationId = Guid.NewGuid().ToString("N");
            foreach (var entityId in entityIds)
            {
                var childEntry = new AuditEntry
                {
                    UserId = userId,
                    UserName = userName,
                    Action = operationType,
                    EntityType = entityType,
                    EntityId = entityId,
                    ParentAuditId = parentId,
                    CorrelationId = correlationId,
                    Description = $"Batch operation: {operationType} on {entityType}",
                    ApplicationVersion = ApplicationVersion
                };

                var childId = await AddEntryAsync(childEntry, cancellationToken);
                parentEntry.ChildAuditIds.Add(childId);
            }

            return parentId;
        }

        private async Task<string> AddEntryAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            // Compute hash chain if enabled
            if (EnableHashChain)
            {
                _hashChain.ComputeEntryHash(entry);
            }

            // Add to in-memory store
            _auditLog.TryAdd(entry.Id, entry);

            // Queue for async writing if enabled
            if (EnableAsyncWriting)
            {
                _pendingWrites.Enqueue(entry);
            }
            else
            {
                await PersistEntryAsync(entry, cancellationToken);
            }

            Logger.Debug($"Audit entry logged: {entry.Action} on {entry.EntityType} by {entry.UserName}");

            return entry.Id;
        }

        private string GenerateDefaultDescription(AuditEventType action, AuditEntityType entityType, string entityName)
        {
            var name = string.IsNullOrEmpty(entityName) ? entityType.ToString() : entityName;

            return action switch
            {
                AuditEventType.Create => $"Created {entityType} '{name}'",
                AuditEventType.Read => $"Accessed {entityType} '{name}'",
                AuditEventType.Update => $"Updated {entityType} '{name}'",
                AuditEventType.Delete => $"Deleted {entityType} '{name}'",
                AuditEventType.Export => $"Exported {entityType} '{name}'",
                AuditEventType.Import => $"Imported {entityType} '{name}'",
                AuditEventType.Share => $"Shared {entityType} '{name}'",
                AuditEventType.Approve => $"Approved {entityType} '{name}'",
                AuditEventType.Reject => $"Rejected {entityType} '{name}'",
                AuditEventType.Comment => $"Commented on {entityType} '{name}'",
                AuditEventType.Upload => $"Uploaded {entityType} '{name}'",
                AuditEventType.Download => $"Downloaded {entityType} '{name}'",
                _ => $"{action} performed on {entityType} '{name}'"
            };
        }

        private void DetermineComplianceFrameworks(AuditEntry entry)
        {
            // Always applicable
            entry.ApplicableFrameworks.Add(ComplianceFramework.ISO19650);

            // Security-related events
            if (entry.Action == AuditEventType.Login ||
                entry.Action == AuditEventType.Logout ||
                entry.Action == AuditEventType.PermissionChange ||
                entry.Action == AuditEventType.SecurityAlert)
            {
                entry.ApplicableFrameworks.Add(ComplianceFramework.SOX);
                entry.ApplicableFrameworks.Add(ComplianceFramework.ISO27001);
            }

            // Data-related events
            if (entry.EntityType == AuditEntityType.User ||
                entry.ContainsPII)
            {
                entry.ApplicableFrameworks.Add(ComplianceFramework.GDPR);
                entry.ApplicableFrameworks.Add(ComplianceFramework.CCPA);
            }

            // Financial data
            if (entry.Metadata?.ContainsKey("IsFinancial") == true)
            {
                entry.ApplicableFrameworks.Add(ComplianceFramework.SOX);
            }
        }

        #endregion

        #region Search and Query

        /// <summary>
        /// Search audit trail with filters
        /// </summary>
        public async Task<List<AuditEntry>> SearchAuditTrailAsync(
            AuditQuery query,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanViewAuditLogs)
            {
                Logger.Warn($"User {context.UserId} attempted unauthorized audit log access");
                throw new UnauthorizedAccessException("User does not have permission to view audit logs");
            }

            await Task.Yield(); // Allow for async operation

            var results = _auditLog.Values
                .Where(e => query.Matches(e))
                .AsEnumerable();

            // Apply sorting
            results = query.SortBy?.ToLower() switch
            {
                "timestamp" => query.SortDescending
                    ? results.OrderByDescending(e => e.Timestamp)
                    : results.OrderBy(e => e.Timestamp),
                "user" => query.SortDescending
                    ? results.OrderByDescending(e => e.UserName)
                    : results.OrderBy(e => e.UserName),
                "action" => query.SortDescending
                    ? results.OrderByDescending(e => e.Action)
                    : results.OrderBy(e => e.Action),
                "severity" => query.SortDescending
                    ? results.OrderByDescending(e => e.Severity)
                    : results.OrderBy(e => e.Severity),
                _ => query.SortDescending
                    ? results.OrderByDescending(e => e.Timestamp)
                    : results.OrderBy(e => e.Timestamp)
            };

            // Apply pagination
            var pagedResults = results
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList();

            // Mask sensitive data if user doesn't have permission
            if (!context.CanViewSensitiveData)
            {
                pagedResults = pagedResults
                    .Select(e => e.CreateMaskedCopy(_dataMasker))
                    .ToList();
            }

            // Log the search itself
            await LogActivityAsync(
                context.UserId, context.UserName,
                AuditEventType.Read, AuditEntityType.System,
                "audit_search",
                description: $"Searched audit logs with {pagedResults.Count} results",
                metadata: new Dictionary<string, object>
                {
                    ["Query"] = JsonSerializer.Serialize(query),
                    ["ResultCount"] = pagedResults.Count
                },
                ipAddress: context.IpAddress,
                sessionId: context.SessionId,
                cancellationToken: cancellationToken);

            return pagedResults;
        }

        /// <summary>
        /// Get history of a specific entity
        /// </summary>
        public async Task<List<AuditEntry>> GetEntityHistoryAsync(
            AuditEntityType entityType,
            string entityId,
            AuditSecurityContext context,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var query = new AuditQuery
            {
                EntityTypes = new List<AuditEntityType> { entityType },
                EntityIds = new List<string> { entityId },
                Take = limit,
                SortDescending = true
            };

            return await SearchAuditTrailAsync(query, context, cancellationToken);
        }

        /// <summary>
        /// Get activity for a specific user
        /// </summary>
        public async Task<UserActivitySummary> GetUserActivityAsync(
            string userId,
            DateTime? startDate,
            DateTime? endDate,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanViewAuditLogs)
            {
                throw new UnauthorizedAccessException("User does not have permission to view audit logs");
            }

            var userEntries = _auditLog.Values
                .Where(e => e.UserId == userId)
                .Where(e => !startDate.HasValue || e.Timestamp >= startDate.Value)
                .Where(e => !endDate.HasValue || e.Timestamp <= endDate.Value)
                .ToList();

            if (!userEntries.Any())
            {
                return new UserActivitySummary { UserId = userId };
            }

            var summary = new UserActivitySummary
            {
                UserId = userId,
                UserName = userEntries.First().UserName,
                TotalActions = userEntries.Count,
                Creates = userEntries.Count(e => e.Action == AuditEventType.Create),
                Reads = userEntries.Count(e => e.Action == AuditEventType.Read),
                Updates = userEntries.Count(e => e.Action == AuditEventType.Update),
                Deletes = userEntries.Count(e => e.Action == AuditEventType.Delete),
                FirstActivity = userEntries.Min(e => e.Timestamp),
                LastActivity = userEntries.Max(e => e.Timestamp),
                IpAddresses = userEntries
                    .Where(e => !string.IsNullOrEmpty(e.IpAddress))
                    .Select(e => context.CanViewSensitiveData ? e.IpAddress : _dataMasker.MaskIpAddress(e.IpAddress))
                    .Distinct()
                    .ToList(),
                FailedActions = userEntries.Count(e => !e.Success),
                SecurityEvents = userEntries.Count(e => e.Severity == AuditSeverity.Security)
            };

            return summary;
        }

        /// <summary>
        /// Get recent activity across the system
        /// </summary>
        public async Task<List<AuditEntry>> GetRecentActivityAsync(
            int count,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            var query = new AuditQuery
            {
                Take = count,
                SortDescending = true
            };

            return await SearchAuditTrailAsync(query, context, cancellationToken);
        }

        #endregion

        #region Compliance Reporting

        /// <summary>
        /// Generate a comprehensive compliance report
        /// </summary>
        public async Task<AuditReport> GenerateComplianceReportAsync(
            ComplianceFramework framework,
            DateTime startDate,
            DateTime endDate,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanViewAuditLogs)
            {
                throw new UnauthorizedAccessException("User does not have permission to generate audit reports");
            }

            var relevantEntries = _auditLog.Values
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .Where(e => e.ApplicableFrameworks.Contains(framework) || framework == ComplianceFramework.Custom)
                .ToList();

            var report = new AuditReport
            {
                Title = $"{framework} Compliance Report",
                Description = $"Audit trail analysis for {framework} compliance from {startDate:d} to {endDate:d}",
                GeneratedBy = context.UserName,
                ReportStartDate = startDate,
                ReportEndDate = endDate,
                Framework = framework,

                // Summary statistics
                TotalEvents = relevantEntries.Count,
                UniqueUsers = relevantEntries.Select(e => e.UserId).Distinct().Count(),
                UniqueEntities = relevantEntries.Select(e => e.EntityId).Distinct().Count(),
                SuccessfulEvents = relevantEntries.Count(e => e.Success),
                FailedEvents = relevantEntries.Count(e => !e.Success),
                SecurityEvents = relevantEntries.Count(e => e.Severity == AuditSeverity.Security),
                DataChanges = relevantEntries.Sum(e => e.Changes.Count)
            };

            // Event breakdowns
            report.EventsByType = relevantEntries
                .GroupBy(e => e.Action)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            report.EventsByEntity = relevantEntries
                .GroupBy(e => e.EntityType)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            report.EventsBySeverity = relevantEntries
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            report.EventsByUser = relevantEntries
                .GroupBy(e => e.UserName ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(20)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            report.EventsByHour = relevantEntries
                .GroupBy(e => e.Timestamp.Hour.ToString("D2"))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            report.EventsByDay = relevantEntries
                .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Top active users
            report.TopActiveUsers = relevantEntries
                .GroupBy(e => e.UserId)
                .Select(g => new UserActivitySummary
                {
                    UserId = g.Key,
                    UserName = g.First().UserName,
                    TotalActions = g.Count(),
                    Creates = g.Count(e => e.Action == AuditEventType.Create),
                    Reads = g.Count(e => e.Action == AuditEventType.Read),
                    Updates = g.Count(e => e.Action == AuditEventType.Update),
                    Deletes = g.Count(e => e.Action == AuditEventType.Delete),
                    FirstActivity = g.Min(e => e.Timestamp),
                    LastActivity = g.Max(e => e.Timestamp)
                })
                .OrderByDescending(u => u.TotalActions)
                .Take(10)
                .ToList();

            // Most accessed entities
            report.MostAccessedEntities = relevantEntries
                .GroupBy(e => new { e.EntityId, e.EntityType })
                .Select(g => new EntityActivitySummary
                {
                    EntityId = g.Key.EntityId,
                    EntityType = g.Key.EntityType,
                    EntityName = g.First().EntityName,
                    TotalAccesses = g.Count(),
                    UniqueUsers = g.Select(e => e.UserId).Distinct().Count(),
                    FirstAccess = g.Min(e => e.Timestamp),
                    LastAccess = g.Max(e => e.Timestamp),
                    Modifications = g.Count(e => e.Action == AuditEventType.Update)
                })
                .OrderByDescending(e => e.TotalAccesses)
                .Take(10)
                .ToList();

            // Recent security events
            report.RecentSecurityEvents = relevantEntries
                .Where(e => e.Severity == AuditSeverity.Security || e.Severity == AuditSeverity.Critical)
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .Select(e => context.CanViewSensitiveData ? e : e.CreateMaskedCopy(_dataMasker))
                .ToList();

            // Recent failures
            report.RecentFailures = relevantEntries
                .Where(e => !e.Success)
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .Select(e => context.CanViewSensitiveData ? e : e.CreateMaskedCopy(_dataMasker))
                .ToList();

            // Run compliance checks
            report.ComplianceResults = await RunComplianceChecksAsync(framework, relevantEntries, cancellationToken);

            // Calculate overall compliance score
            var passedChecks = report.ComplianceResults.Count(c => c.Passed);
            var totalChecks = report.ComplianceResults.Count;
            report.OverallComplianceScore = totalChecks > 0
                ? (double)passedChecks / totalChecks * 100
                : 100;

            // Generate issues and recommendations
            report.ComplianceIssues = report.ComplianceResults
                .Where(c => !c.Passed)
                .Select(c => c.Details)
                .ToList();

            report.Recommendations = GenerateComplianceRecommendations(framework, report);

            // Verify hash chain integrity
            var (isValid, tamperedCount) = await VerifyHashChainAsync(relevantEntries, cancellationToken);
            report.HashChainValid = isValid;
            report.TamperedRecords = tamperedCount;
            report.LastIntegrityCheck = DateTime.UtcNow;

            // Log report generation
            await LogActivityAsync(
                context.UserId, context.UserName,
                AuditEventType.Export, AuditEntityType.Report,
                report.Id, report.Title,
                $"Generated {framework} compliance report",
                new Dictionary<string, object>
                {
                    ["Framework"] = framework.ToString(),
                    ["StartDate"] = startDate,
                    ["EndDate"] = endDate,
                    ["TotalEvents"] = report.TotalEvents,
                    ["ComplianceScore"] = report.OverallComplianceScore
                },
                context.IpAddress, context.SessionId,
                cancellationToken);

            return report;
        }

        private async Task<List<ComplianceCheckResult>> RunComplianceChecksAsync(
            ComplianceFramework framework,
            List<AuditEntry> entries,
            CancellationToken cancellationToken)
        {
            var results = new List<ComplianceCheckResult>();

            // Common checks for all frameworks
            results.Add(CheckHashChainIntegrity(entries));
            results.Add(CheckAuditCoverage(entries));
            results.Add(CheckTimestampConsistency(entries));

            // Framework-specific checks
            switch (framework)
            {
                case ComplianceFramework.SOX:
                    results.AddRange(RunSOXChecks(entries));
                    break;
                case ComplianceFramework.GDPR:
                    results.AddRange(RunGDPRChecks(entries));
                    break;
                case ComplianceFramework.ISO27001:
                    results.AddRange(RunISO27001Checks(entries));
                    break;
                case ComplianceFramework.ISO19650:
                    results.AddRange(RunISO19650Checks(entries));
                    break;
            }

            return results;
        }

        private ComplianceCheckResult CheckHashChainIntegrity(List<AuditEntry> entries)
        {
            var ordered = entries.OrderBy(e => e.SequenceNumber).ToList();
            var tamperedCount = 0;
            string previousHash = "";

            foreach (var entry in ordered)
            {
                if (entry.PreviousHash != previousHash)
                {
                    tamperedCount++;
                }
                previousHash = entry.CurrentHash;
            }

            return new ComplianceCheckResult
            {
                CheckId = "HASH_CHAIN",
                CheckName = "Hash Chain Integrity",
                Description = "Verify that the audit log hash chain has not been tampered with",
                Passed = tamperedCount == 0,
                Details = tamperedCount == 0
                    ? "Hash chain integrity verified"
                    : $"Found {tamperedCount} potentially tampered records",
                Severity = AuditSeverity.Critical
            };
        }

        private ComplianceCheckResult CheckAuditCoverage(List<AuditEntry> entries)
        {
            var requiredEvents = new[]
            {
                AuditEventType.Login, AuditEventType.Logout,
                AuditEventType.Create, AuditEventType.Update, AuditEventType.Delete
            };

            var coveredEvents = entries.Select(e => e.Action).Distinct().ToHashSet();
            var missingEvents = requiredEvents.Where(e => !coveredEvents.Contains(e)).ToList();

            return new ComplianceCheckResult
            {
                CheckId = "AUDIT_COVERAGE",
                CheckName = "Audit Event Coverage",
                Description = "Verify all required event types are being logged",
                Passed = !missingEvents.Any(),
                Details = missingEvents.Any()
                    ? $"Missing audit coverage for: {string.Join(", ", missingEvents)}"
                    : "All required event types are being audited",
                Severity = AuditSeverity.Warning
            };
        }

        private ComplianceCheckResult CheckTimestampConsistency(List<AuditEntry> entries)
        {
            var ordered = entries.OrderBy(e => e.SequenceNumber).ToList();
            var outOfOrder = 0;

            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Timestamp < ordered[i - 1].Timestamp)
                {
                    outOfOrder++;
                }
            }

            return new ComplianceCheckResult
            {
                CheckId = "TIMESTAMP_CONSISTENCY",
                CheckName = "Timestamp Consistency",
                Description = "Verify audit entries have consistent timestamps",
                Passed = outOfOrder == 0,
                Details = outOfOrder == 0
                    ? "All timestamps are consistent"
                    : $"Found {outOfOrder} entries with inconsistent timestamps",
                Severity = AuditSeverity.Warning
            };
        }

        private List<ComplianceCheckResult> RunSOXChecks(List<AuditEntry> entries)
        {
            var results = new List<ComplianceCheckResult>();

            // Check for access control logging
            var accessControlEvents = entries.Count(e =>
                e.Action == AuditEventType.Login ||
                e.Action == AuditEventType.Logout ||
                e.Action == AuditEventType.PermissionChange);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "SOX_ACCESS_CONTROL",
                CheckName = "SOX Access Control Logging",
                Description = "Verify access control events are properly logged",
                Framework = ComplianceFramework.SOX,
                Passed = accessControlEvents > 0,
                Details = $"Found {accessControlEvents} access control events",
                Severity = AuditSeverity.Error
            });

            // Check for financial data changes
            var financialChanges = entries.Count(e =>
                e.Metadata?.ContainsKey("IsFinancial") == true);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "SOX_FINANCIAL_CHANGES",
                CheckName = "SOX Financial Data Tracking",
                Description = "Verify financial data changes are tracked",
                Framework = ComplianceFramework.SOX,
                Passed = true, // Informational
                Details = $"Found {financialChanges} financial-related changes",
                Severity = AuditSeverity.Info
            });

            return results;
        }

        private List<ComplianceCheckResult> RunGDPRChecks(List<AuditEntry> entries)
        {
            var results = new List<ComplianceCheckResult>();

            // Check for PII access logging
            var piiAccess = entries.Count(e => e.ContainsPII);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "GDPR_PII_ACCESS",
                CheckName = "GDPR PII Access Logging",
                Description = "Verify PII access is properly logged",
                Framework = ComplianceFramework.GDPR,
                Passed = true,
                Details = $"Found {piiAccess} entries involving personal data",
                Severity = AuditSeverity.Info
            });

            // Check for data export logging
            var exports = entries.Count(e => e.Action == AuditEventType.Export);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "GDPR_DATA_EXPORT",
                CheckName = "GDPR Data Export Logging",
                Description = "Verify data exports are logged for data portability compliance",
                Framework = ComplianceFramework.GDPR,
                Passed = true,
                Details = $"Found {exports} data export events",
                Severity = AuditSeverity.Info
            });

            // Check for deletion logging
            var deletions = entries.Count(e => e.Action == AuditEventType.Delete);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "GDPR_RIGHT_TO_ERASURE",
                CheckName = "GDPR Right to Erasure Logging",
                Description = "Verify data deletions are logged for erasure compliance",
                Framework = ComplianceFramework.GDPR,
                Passed = true,
                Details = $"Found {deletions} deletion events",
                Severity = AuditSeverity.Info
            });

            return results;
        }

        private List<ComplianceCheckResult> RunISO27001Checks(List<AuditEntry> entries)
        {
            var results = new List<ComplianceCheckResult>();

            // Check for security event logging
            var securityEvents = entries.Count(e =>
                e.Severity == AuditSeverity.Security ||
                e.Action == AuditEventType.SecurityAlert);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "ISO27001_SECURITY_EVENTS",
                CheckName = "ISO 27001 Security Event Logging",
                Description = "Verify security events are properly logged",
                Framework = ComplianceFramework.ISO27001,
                Passed = true,
                Details = $"Found {securityEvents} security-related events",
                Severity = AuditSeverity.Info
            });

            // Check for failed access attempts
            var failedLogins = entries.Count(e =>
                e.Action == AuditEventType.Login && !e.Success);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "ISO27001_FAILED_ACCESS",
                CheckName = "ISO 27001 Failed Access Monitoring",
                Description = "Monitor failed access attempts for intrusion detection",
                Framework = ComplianceFramework.ISO27001,
                Passed = true,
                Details = $"Found {failedLogins} failed login attempts",
                Severity = failedLogins > 10 ? AuditSeverity.Warning : AuditSeverity.Info
            });

            return results;
        }

        private List<ComplianceCheckResult> RunISO19650Checks(List<AuditEntry> entries)
        {
            var results = new List<ComplianceCheckResult>();

            // Check for document control
            var documentEvents = entries.Count(e =>
                e.EntityType == AuditEntityType.Document ||
                e.EntityType == AuditEntityType.Model);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "ISO19650_DOCUMENT_CONTROL",
                CheckName = "ISO 19650 Document Control",
                Description = "Verify BIM document changes are tracked",
                Framework = ComplianceFramework.ISO19650,
                Passed = documentEvents > 0,
                Details = $"Found {documentEvents} document/model events",
                Severity = AuditSeverity.Info
            });

            // Check for approval workflow
            var approvals = entries.Count(e =>
                e.Action == AuditEventType.Approve ||
                e.Action == AuditEventType.Reject);

            results.Add(new ComplianceCheckResult
            {
                CheckId = "ISO19650_APPROVAL_WORKFLOW",
                CheckName = "ISO 19650 Approval Workflow",
                Description = "Verify approval workflows are being tracked",
                Framework = ComplianceFramework.ISO19650,
                Passed = true,
                Details = $"Found {approvals} approval/rejection events",
                Severity = AuditSeverity.Info
            });

            return results;
        }

        private List<string> GenerateComplianceRecommendations(ComplianceFramework framework, AuditReport report)
        {
            var recommendations = new List<string>();

            if (report.FailedEvents > report.TotalEvents * 0.1)
            {
                recommendations.Add("High failure rate detected. Review system stability and user training.");
            }

            if (!report.HashChainValid)
            {
                recommendations.Add("CRITICAL: Hash chain integrity compromised. Investigate potential tampering immediately.");
            }

            if (report.SecurityEvents > 0)
            {
                recommendations.Add("Security events detected. Review security policies and access controls.");
            }

            switch (framework)
            {
                case ComplianceFramework.SOX:
                    if (report.EventsByType.GetValueOrDefault(AuditEventType.PermissionChange) > 50)
                    {
                        recommendations.Add("High number of permission changes. Review access control procedures.");
                    }
                    break;

                case ComplianceFramework.GDPR:
                    recommendations.Add("Ensure data subject access requests are processed within 30 days.");
                    recommendations.Add("Review data retention policies for compliance with minimization principle.");
                    break;

                case ComplianceFramework.ISO19650:
                    recommendations.Add("Ensure all BIM deliverables follow the Common Data Environment workflow.");
                    recommendations.Add("Verify model coordination events are properly logged.");
                    break;
            }

            return recommendations;
        }

        #endregion

        #region Hash Chain Verification

        /// <summary>
        /// Verify the integrity of the hash chain
        /// </summary>
        public async Task<(bool IsValid, long TamperedCount)> VerifyHashChainAsync(
            IEnumerable<AuditEntry> entries = null,
            CancellationToken cancellationToken = default)
        {
            var entriesToCheck = (entries ?? _auditLog.Values)
                .OrderBy(e => e.SequenceNumber)
                .ToList();

            if (!entriesToCheck.Any())
            {
                return (true, 0);
            }

            long tamperedCount = 0;
            string expectedPreviousHash = "";

            foreach (var entry in entriesToCheck)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_hashChain.VerifyEntryHash(entry, expectedPreviousHash))
                {
                    tamperedCount++;
                    Logger.Warn($"Hash chain verification failed for entry {entry.Id}");
                }

                expectedPreviousHash = entry.CurrentHash;
            }

            return (tamperedCount == 0, tamperedCount);
        }

        #endregion

        #region Retention Policy Management

        /// <summary>
        /// Apply retention policies to audit logs
        /// </summary>
        public async Task<RetentionResult> ApplyRetentionPolicyAsync(
            string policyId,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanManageRetention)
            {
                throw new UnauthorizedAccessException("User does not have permission to manage retention policies");
            }

            if (!_retentionPolicies.TryGetValue(policyId, out var policy))
            {
                throw new ArgumentException($"Policy not found: {policyId}");
            }

            var result = new RetentionResult
            {
                PolicyId = policyId
            };

            var startTime = DateTime.UtcNow;
            var cutoffDate = DateTime.UtcNow.AddDays(-policy.RetentionDays);

            try
            {
                var eligibleEntries = _auditLog.Values
                    .Where(e => e.Timestamp < cutoffDate)
                    .Where(e => policy.AppliesToEntry(e))
                    .ToList();

                result.RecordsProcessed = eligibleEntries.Count;

                foreach (var entry in eligibleEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (policy.ActionAfterRetention)
                    {
                        case RetentionAction.Archive:
                            // Entry will be included in archive batch
                            result.RecordsArchived++;
                            break;

                        case RetentionAction.Delete:
                            if (_auditLog.TryRemove(entry.Id, out _))
                            {
                                result.RecordsDeleted++;
                            }
                            break;

                        case RetentionAction.Anonymize:
                            AnonymizeEntry(entry);
                            result.RecordsAnonymized++;
                            break;

                        case RetentionAction.Keep:
                        default:
                            result.RecordsKept++;
                            break;
                    }
                }

                // Archive if needed
                if (result.RecordsArchived > 0)
                {
                    await ArchiveEntriesAsync(
                        eligibleEntries.Where(e => policy.ActionAfterRetention == RetentionAction.Archive),
                        context,
                        cancellationToken);

                    // Remove archived entries from active log
                    foreach (var entry in eligibleEntries.Where(e => policy.ActionAfterRetention == RetentionAction.Archive))
                    {
                        _auditLog.TryRemove(entry.Id, out _);
                    }
                }

                result.Success = true;
                result.Duration = DateTime.UtcNow - startTime;

                // Update policy execution info
                policy.LastExecuted = DateTime.UtcNow;
                policy.LastExecutionRecordsProcessed = result.RecordsProcessed;

                // Log policy execution
                await LogActivityAsync(
                    context.UserId, context.UserName,
                    AuditEventType.SystemEvent, AuditEntityType.System,
                    policyId, policy.Name,
                    $"Applied retention policy: {policy.Name}",
                    new Dictionary<string, object>
                    {
                        ["RecordsProcessed"] = result.RecordsProcessed,
                        ["RecordsArchived"] = result.RecordsArchived,
                        ["RecordsDeleted"] = result.RecordsDeleted,
                        ["RecordsAnonymized"] = result.RecordsAnonymized
                    },
                    context.IpAddress, context.SessionId,
                    cancellationToken);

                Logger.Info($"Retention policy {policyId} applied: {result.RecordsProcessed} records processed");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.UtcNow - startTime;

                Logger.Error(ex, $"Failed to apply retention policy {policyId}");
            }

            return result;
        }

        private void AnonymizeEntry(AuditEntry entry)
        {
            entry.UserId = "anonymized_" + entry.UserId.GetHashCode().ToString("X8");
            entry.UserName = "Anonymized User";
            entry.UserEmail = null;
            entry.IpAddress = null;
            entry.UserAgent = null;
            entry.Metadata = new Dictionary<string, object>();

            foreach (var change in entry.Changes)
            {
                if (change.IsSensitive)
                {
                    change.OldValue = "[ANONYMIZED]";
                    change.NewValue = "[ANONYMIZED]";
                }
            }
        }

        /// <summary>
        /// Archive old records to compressed storage
        /// </summary>
        public async Task<AuditArchive> ArchiveOldRecordsAsync(
            DateTime cutoffDate,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            var entriesToArchive = _auditLog.Values
                .Where(e => e.Timestamp < cutoffDate)
                .OrderBy(e => e.Timestamp)
                .ToList();

            if (!entriesToArchive.Any())
            {
                return null;
            }

            return await ArchiveEntriesAsync(entriesToArchive, context, cancellationToken);
        }

        private async Task<AuditArchive> ArchiveEntriesAsync(
            IEnumerable<AuditEntry> entries,
            AuditSecurityContext context,
            CancellationToken cancellationToken)
        {
            var entryList = entries.ToList();
            if (!entryList.Any()) return null;

            var archive = new AuditArchive
            {
                ArchivedBy = context.UserName,
                OriginalStartDate = entryList.Min(e => e.Timestamp),
                OriginalEndDate = entryList.Max(e => e.Timestamp),
                RecordCount = entryList.Count,
                FilePath = Path.Combine(_archivePath, $"audit_archive_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json.gz"),
                IsCompressed = true
            };

            // Serialize and compress
            var json = JsonSerializer.Serialize(entryList, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            using var fileStream = File.Create(archive.FilePath);
            using var gzipStream = new System.IO.Compression.GZipStream(
                fileStream, System.IO.Compression.CompressionLevel.Optimal);

            var bytes = Encoding.UTF8.GetBytes(json);
            await gzipStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            await gzipStream.FlushAsync(cancellationToken);

            archive.FileSizeBytes = new FileInfo(archive.FilePath).Length;

            // Calculate checksum
            using var sha256 = SHA256.Create();
            archive.Checksum = Convert.ToBase64String(
                sha256.ComputeHash(Encoding.UTF8.GetBytes(json)));

            _archives.TryAdd(archive.Id, archive);

            Logger.Info($"Archived {entryList.Count} audit entries to {archive.FilePath}");

            return archive;
        }

        /// <summary>
        /// Add a new retention policy
        /// </summary>
        public async Task<string> AddRetentionPolicyAsync(
            RetentionPolicy policy,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanManageRetention)
            {
                throw new UnauthorizedAccessException("User does not have permission to manage retention policies");
            }

            policy.CreatedBy = context.UserName;
            policy.CreatedAt = DateTime.UtcNow;

            _retentionPolicies.TryAdd(policy.Id, policy);

            await SaveRetentionPoliciesAsync(cancellationToken);

            await LogActivityAsync(
                context.UserId, context.UserName,
                AuditEventType.Create, AuditEntityType.Configuration,
                policy.Id, policy.Name,
                $"Created retention policy: {policy.Name}",
                null, context.IpAddress, context.SessionId,
                cancellationToken);

            return policy.Id;
        }

        /// <summary>
        /// Get all retention policies
        /// </summary>
        public IEnumerable<RetentionPolicy> GetRetentionPolicies()
        {
            return _retentionPolicies.Values.ToList();
        }

        private async Task SaveRetentionPoliciesAsync(CancellationToken cancellationToken)
        {
            var policyFile = Path.Combine(_storageBasePath, "retention_policies.json");
            var json = JsonSerializer.Serialize(_retentionPolicies.Values.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(policyFile, json, cancellationToken);
        }

        #endregion

        #region Export

        /// <summary>
        /// Export audit log to various formats
        /// </summary>
        public async Task<string> ExportAuditLogAsync(
            AuditQuery query,
            AuditExportFormat format,
            string outputPath,
            AuditSecurityContext context,
            CancellationToken cancellationToken = default)
        {
            if (!context.CanExportAuditLogs)
            {
                throw new UnauthorizedAccessException("User does not have permission to export audit logs");
            }

            var entries = await SearchAuditTrailAsync(query, context, cancellationToken);

            string content;
            string extension;

            switch (format)
            {
                case AuditExportFormat.Json:
                    content = JsonSerializer.Serialize(entries, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    extension = ".json";
                    break;

                case AuditExportFormat.Csv:
                    content = ExportToCsv(entries);
                    extension = ".csv";
                    break;

                case AuditExportFormat.Xml:
                    content = ExportToXml(entries);
                    extension = ".xml";
                    break;

                case AuditExportFormat.Html:
                    content = ExportToHtml(entries);
                    extension = ".html";
                    break;

                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }

            var filePath = Path.ChangeExtension(outputPath, extension);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);

            await LogActivityAsync(
                context.UserId, context.UserName,
                AuditEventType.Export, AuditEntityType.System,
                "audit_export", $"Audit Export {format}",
                $"Exported {entries.Count} audit entries to {format}",
                new Dictionary<string, object>
                {
                    ["Format"] = format.ToString(),
                    ["RecordCount"] = entries.Count,
                    ["FilePath"] = filePath
                },
                context.IpAddress, context.SessionId,
                cancellationToken);

            return filePath;
        }

        private string ExportToCsv(List<AuditEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Timestamp,UserId,UserName,Action,EntityType,EntityId,EntityName,Description,Severity,Success,IpAddress,SessionId");

            foreach (var entry in entries)
            {
                sb.AppendLine($"\"{entry.Id}\",\"{entry.Timestamp:O}\",\"{entry.UserId}\",\"{EscapeCsv(entry.UserName)}\",\"{entry.Action}\",\"{entry.EntityType}\",\"{entry.EntityId}\",\"{EscapeCsv(entry.EntityName)}\",\"{EscapeCsv(entry.Description)}\",\"{entry.Severity}\",\"{entry.Success}\",\"{entry.IpAddress}\",\"{entry.SessionId}\"");
            }

            return sb.ToString();
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }

        private string ExportToXml(List<AuditEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<AuditLog>");

            foreach (var entry in entries)
            {
                sb.AppendLine("  <Entry>");
                sb.AppendLine($"    <Id>{entry.Id}</Id>");
                sb.AppendLine($"    <Timestamp>{entry.Timestamp:O}</Timestamp>");
                sb.AppendLine($"    <UserId>{entry.UserId}</UserId>");
                sb.AppendLine($"    <UserName>{System.Security.SecurityElement.Escape(entry.UserName)}</UserName>");
                sb.AppendLine($"    <Action>{entry.Action}</Action>");
                sb.AppendLine($"    <EntityType>{entry.EntityType}</EntityType>");
                sb.AppendLine($"    <EntityId>{entry.EntityId}</EntityId>");
                sb.AppendLine($"    <EntityName>{System.Security.SecurityElement.Escape(entry.EntityName)}</EntityName>");
                sb.AppendLine($"    <Description>{System.Security.SecurityElement.Escape(entry.Description)}</Description>");
                sb.AppendLine($"    <Severity>{entry.Severity}</Severity>");
                sb.AppendLine($"    <Success>{entry.Success}</Success>");
                sb.AppendLine("  </Entry>");
            }

            sb.AppendLine("</AuditLog>");
            return sb.ToString();
        }

        private string ExportToHtml(List<AuditEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><title>Audit Log Export</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #4CAF50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".severity-warning { color: #ff9800; }");
            sb.AppendLine(".severity-error { color: #f44336; }");
            sb.AppendLine(".severity-critical { color: #9c27b0; font-weight: bold; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>StingBIM Audit Log Export</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine($"<p>Total Records: {entries.Count}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Timestamp</th><th>User</th><th>Action</th><th>Entity</th><th>Description</th><th>Severity</th><th>Status</th></tr>");

            foreach (var entry in entries)
            {
                var severityClass = entry.Severity switch
                {
                    AuditSeverity.Warning => "severity-warning",
                    AuditSeverity.Error => "severity-error",
                    AuditSeverity.Critical => "severity-critical",
                    _ => ""
                };

                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{entry.Timestamp:yyyy-MM-dd HH:mm:ss}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(entry.UserName)}</td>");
                sb.AppendLine($"<td>{entry.Action}</td>");
                sb.AppendLine($"<td>{entry.EntityType}: {System.Net.WebUtility.HtmlEncode(entry.EntityName)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(entry.Description)}</td>");
                sb.AppendLine($"<td class=\"{severityClass}\">{entry.Severity}</td>");
                sb.AppendLine($"<td>{(entry.Success ? "Success" : "Failed")}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        #endregion

        #region Background Processing

        private async Task BackgroundWriteLoop()
        {
            Logger.Info("Audit background write loop started");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(BatchWriteIntervalMs, _cancellationTokenSource.Token);
                    await FlushPendingWritesAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in audit background write loop");
                }
            }

            // Final flush on shutdown
            await FlushPendingWritesAsync(CancellationToken.None);
            Logger.Info("Audit background write loop stopped");
        }

        private async Task FlushPendingWritesAsync(CancellationToken cancellationToken)
        {
            if (_pendingWrites.IsEmpty) return;

            await _writeSemaphore.WaitAsync(cancellationToken);
            try
            {
                var entries = new List<AuditEntry>();

                while (entries.Count < BatchWriteSize && _pendingWrites.TryDequeue(out var entry))
                {
                    entries.Add(entry);
                }

                if (entries.Any())
                {
                    await PersistEntriesAsync(entries, cancellationToken);
                }
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async Task PersistEntryAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            await PersistEntriesAsync(new[] { entry }, cancellationToken);
        }

        private async Task PersistEntriesAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken)
        {
            var fileName = $"audit_{DateTime.UtcNow:yyyyMMdd}.json";
            var filePath = Path.Combine(_storageBasePath, fileName);

            List<AuditEntry> existingEntries = new List<AuditEntry>();

            if (File.Exists(filePath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(filePath, cancellationToken);
                    existingEntries = JsonSerializer.Deserialize<List<AuditEntry>>(existingJson)
                        ?? new List<AuditEntry>();
                }
                catch
                {
                    // If file is corrupted, start fresh
                }
            }

            existingEntries.AddRange(entries);

            var json = JsonSerializer.Serialize(existingEntries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            Logger.Info("Disposing AuditTrailSystem");

            _cancellationTokenSource.Cancel();

            try
            {
                await _backgroundWriteTask;
            }
            catch (OperationCanceledException) { }

            // Final flush
            await FlushPendingWritesAsync(CancellationToken.None);

            // Save retention policies
            await SaveRetentionPoliciesAsync(CancellationToken.None);

            _writeSemaphore.Dispose();
            _cancellationTokenSource.Dispose();

            _isDisposed = true;
            Logger.Info("AuditTrailSystem disposed");
        }

        #endregion
    }

    #endregion
}
