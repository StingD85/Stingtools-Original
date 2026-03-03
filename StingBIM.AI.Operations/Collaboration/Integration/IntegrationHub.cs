// =============================================================================
// StingBIM.AI.Collaboration - Integration Hub
// Comprehensive integration platform connecting StingBIM with external systems:
// BIM 360, Procore, PlanGrid, Revizto, SAP, Oracle, Microsoft Project, Primavera,
// SharePoint, Slack, Microsoft Teams, Webhooks, and Custom APIs
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog;

namespace StingBIM.AI.Collaboration.Integration
{
    #region Enumerations

    /// <summary>
    /// Supported external integration types
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IntegrationType
    {
        BIM360,
        Procore,
        PlanGrid,
        Revizto,
        SAP,
        Oracle,
        MicrosoftProject,
        Primavera,
        SharePoint,
        Slack,
        MicrosoftTeams,
        Webhook,
        CustomAPI
    }

    /// <summary>
    /// Authentication methods for external systems
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AuthenticationType
    {
        None,
        ApiKey,
        BasicAuth,
        OAuth2ClientCredentials,
        OAuth2AuthorizationCode,
        OAuth2PKCE,
        BearerToken,
        HMAC,
        Certificate,
        SAML,
        Custom
    }

    /// <summary>
    /// Data synchronization direction
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SyncDirection
    {
        Import,
        Export,
        Bidirectional
    }

    /// <summary>
    /// Conflict resolution strategies
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConflictResolutionStrategy
    {
        SourceWins,
        TargetWins,
        NewerWins,
        Manual,
        MergeFields,
        Custom
    }

    /// <summary>
    /// Field mapping transformation types
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FieldTransformationType
    {
        Direct,
        ValueMapping,
        Concatenation,
        Lookup,
        Formula,
        Conditional,
        Split,
        Aggregate,
        DateFormat,
        NumberFormat,
        Custom
    }

    /// <summary>
    /// Sync job status
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SyncJobStatus
    {
        Pending,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled,
        PartialSuccess
    }

    /// <summary>
    /// Sync schedule frequency
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SyncFrequency
    {
        Manual,
        RealTime,
        Hourly,
        Daily,
        Weekly,
        Monthly,
        Custom
    }

    /// <summary>
    /// Webhook delivery status
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum WebhookDeliveryStatus
    {
        Pending,
        Delivered,
        Failed,
        Retrying
    }

    /// <summary>
    /// Integration configuration status
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConfigurationStatus
    {
        Draft,
        Testing,
        Active,
        Disabled,
        Error
    }

    /// <summary>
    /// External system health status
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SystemHealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }

    #endregion

    #region Data Models - Connection Settings

    /// <summary>
    /// Connection settings for external system authentication
    /// </summary>
    public class ConnectionSettings
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public IntegrationType IntegrationType { get; set; }
        public AuthenticationType AuthType { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? TenantId { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public string? AuthorizationEndpoint { get; set; }
        public string? TokenEndpoint { get; set; }
        public string? Scope { get; set; }
        public string? CertificatePath { get; set; }
        public string? CertificatePassword { get; set; }
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        public Dictionary<string, string> CustomParameters { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public bool ValidateCertificates { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public bool LastTestSuccessful { get; set; }
    }

    /// <summary>
    /// OAuth token response
    /// </summary>
    public class OAuthTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("scope")]
        public string? Scope { get; set; }
    }

    #endregion

    #region Data Models - Sync Mapping

    /// <summary>
    /// Mapping configuration for data synchronization between systems
    /// </summary>
    public class SyncMapping
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ConfigurationId { get; set; } = string.Empty;
        public string SourceEntity { get; set; } = string.Empty;
        public string TargetEntity { get; set; } = string.Empty;
        public SyncDirection Direction { get; set; } = SyncDirection.Bidirectional;
        public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.NewerWins;
        public List<FieldMapping> FieldMappings { get; set; } = new();
        public List<SyncFilter> Filters { get; set; } = new();
        public string? KeyField { get; set; }
        public string? TimestampField { get; set; }
        public bool SyncDeletes { get; set; } = false;
        public bool CreateMissing { get; set; } = true;
        public bool UpdateExisting { get; set; } = true;
        public int BatchSize { get; set; } = 100;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncAt { get; set; }
    }

    /// <summary>
    /// Field-level mapping configuration
    /// </summary>
    public class FieldMapping
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceField { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
        public FieldTransformationType TransformationType { get; set; } = FieldTransformationType.Direct;
        public Dictionary<string, string>? ValueMappings { get; set; }
        public List<string>? ConcatenationFields { get; set; }
        public string? ConcatenationSeparator { get; set; }
        public string? LookupTable { get; set; }
        public string? LookupKeyField { get; set; }
        public string? LookupValueField { get; set; }
        public string? Formula { get; set; }
        public string? ConditionalExpression { get; set; }
        public string? DateFormat { get; set; }
        public string? NumberFormat { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public bool SkipIfNull { get; set; } = true;
        public string? CustomTransformerType { get; set; }
    }

    /// <summary>
    /// Filter criteria for sync operations
    /// </summary>
    public class SyncFilter
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = "equals";
        public object? Value { get; set; }
        public List<object>? Values { get; set; }
    }

    #endregion

    #region Data Models - Sync Job

    /// <summary>
    /// Represents a data synchronization job
    /// </summary>
    public class SyncJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConfigurationId { get; set; } = string.Empty;
        public string MappingId { get; set; } = string.Empty;
        public SyncJobStatus Status { get; set; } = SyncJobStatus.Pending;
        public SyncDirection Direction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? InitiatedBy { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
        public int SkippedRecords { get; set; }
        public int ConflictRecords { get; set; }
        public double ProgressPercentage => TotalRecords > 0 ? (ProcessedRecords * 100.0 / TotalRecords) : 0;
        public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : (StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : null);
        public List<SyncError> Errors { get; set; } = new();
        public List<SyncLog> Logs { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public CancellationTokenSource? CancellationSource { get; set; }
    }

    /// <summary>
    /// Error details for sync operations
    /// </summary>
    public class SyncError
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RecordId { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? FieldName { get; set; }
        public object? SourceValue { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public bool IsRetryable { get; set; }
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// Log entry for sync operations
    /// </summary>
    public class SyncLog
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Level { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
        public string? RecordId { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// Progress update for sync operations
    /// </summary>
    public class SyncProgress
    {
        public string JobId { get; set; } = string.Empty;
        public int ProcessedRecords { get; set; }
        public int TotalRecords { get; set; }
        public double PercentComplete { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    #endregion

    #region Data Models - Webhook

    /// <summary>
    /// Webhook configuration
    /// </summary>
    public class WebhookConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public List<string> EventTypes { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 60;
        public string? FilterExpression { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTriggeredAt { get; set; }
        public int TotalDeliveries { get; set; }
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
    }

    /// <summary>
    /// Webhook delivery record
    /// </summary>
    public class WebhookDelivery
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WebhookId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
        public int HttpStatusCode { get; set; }
        public string? ResponseBody { get; set; }
        public string? ErrorMessage { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public TimeSpan? ResponseTime { get; set; }
    }

    /// <summary>
    /// Webhook event payload
    /// </summary>
    public class WebhookEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Source { get; set; } = "StingBIM";
        public object? Data { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    #endregion

    #region Data Models - External System Info

    /// <summary>
    /// Information about an external system
    /// </summary>
    public class ExternalSystemInfo
    {
        public IntegrationType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? ApiVersion { get; set; }
        public SystemHealthStatus HealthStatus { get; set; } = SystemHealthStatus.Unknown;
        public DateTime? LastHealthCheck { get; set; }
        public TimeSpan? AverageResponseTime { get; set; }
        public Dictionary<string, string> Capabilities { get; set; } = new();
        public Dictionary<string, int> RateLimits { get; set; } = new();
        public List<string> SupportedEntities { get; set; } = new();
    }

    #endregion

    #region Data Models - Integration Configuration

    /// <summary>
    /// Complete integration configuration
    /// </summary>
    public class IntegrationConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IntegrationType IntegrationType { get; set; }
        public ConfigurationStatus Status { get; set; } = ConfigurationStatus.Draft;
        public ConnectionSettings ConnectionSettings { get; set; } = new();
        public List<SyncMapping> SyncMappings { get; set; } = new();
        public SyncSchedule? Schedule { get; set; }
        public NotificationSettings? Notifications { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? LastModifiedBy { get; set; }
    }

    /// <summary>
    /// Sync schedule configuration
    /// </summary>
    public class SyncSchedule
    {
        public SyncFrequency Frequency { get; set; } = SyncFrequency.Manual;
        public string? CronExpression { get; set; }
        public TimeSpan? Interval { get; set; }
        public List<DayOfWeek>? DaysOfWeek { get; set; }
        public TimeSpan? TimeOfDay { get; set; }
        public string? TimeZone { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
    }

    /// <summary>
    /// Notification settings for integration events
    /// </summary>
    public class NotificationSettings
    {
        public bool NotifyOnSuccess { get; set; } = false;
        public bool NotifyOnFailure { get; set; } = true;
        public bool NotifyOnConflict { get; set; } = true;
        public List<string> EmailRecipients { get; set; } = new();
        public List<string> SlackChannels { get; set; } = new();
        public List<string> TeamsChannels { get; set; } = new();
        public List<string> WebhookIds { get; set; } = new();
    }

    #endregion

    #region Data Models - Statistics

    /// <summary>
    /// Integration statistics
    /// </summary>
    public class IntegrationStatistics
    {
        public string ConfigurationId { get; set; } = string.Empty;
        public IntegrationType IntegrationType { get; set; }
        public int TotalSyncJobs { get; set; }
        public int SuccessfulSyncJobs { get; set; }
        public int FailedSyncJobs { get; set; }
        public int TotalRecordsSynced { get; set; }
        public int TotalErrors { get; set; }
        public int TotalConflicts { get; set; }
        public TimeSpan AverageSyncDuration { get; set; }
        public DateTime? LastSyncAt { get; set; }
        public DateTime? NextScheduledSync { get; set; }
        public double UptimePercentage { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public Dictionary<string, int> RecordsByEntity { get; set; } = new();
        public List<DailyStats> DailyStatistics { get; set; } = new();
    }

    /// <summary>
    /// Daily statistics snapshot
    /// </summary>
    public class DailyStats
    {
        public DateTime Date { get; set; }
        public int SyncJobs { get; set; }
        public int RecordsSynced { get; set; }
        public int Errors { get; set; }
        public TimeSpan AverageDuration { get; set; }
    }

    #endregion

    #region Data Models - Test Results

    /// <summary>
    /// Connection test result
    /// </summary>
    public class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? ErrorDetails { get; set; }
        public ExternalSystemInfo? SystemInfo { get; set; }
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    /// <summary>
    /// Central hub for managing integrations with external systems.
    /// Provides configuration management, synchronization, webhook delivery,
    /// and comprehensive monitoring capabilities.
    /// </summary>
    public class IntegrationHub : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Configuration storage
        private readonly ConcurrentDictionary<string, IntegrationConfiguration> _configurations = new();
        private readonly ConcurrentDictionary<string, SyncJob> _syncJobs = new();
        private readonly ConcurrentDictionary<string, WebhookConfig> _webhooks = new();
        private readonly ConcurrentDictionary<string, List<WebhookDelivery>> _deliveryHistory = new();

        // Statistics tracking
        private readonly ConcurrentDictionary<string, IntegrationStatistics> _statistics = new();

        // HTTP clients per integration type
        private readonly ConcurrentDictionary<IntegrationType, HttpClient> _httpClients = new();
        private readonly HttpClient _defaultHttpClient;

        // Rate limiting
        private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters = new();

        // Background task management
        private CancellationTokenSource? _backgroundCts;
        private Task? _schedulerTask;
        private Task? _webhookDeliveryTask;

        // Lookup tables for transformations
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _lookupTables = new();

        // Event handlers
        public event EventHandler<SyncProgress>? SyncProgressChanged;
        public event EventHandler<SyncJob>? SyncJobCompleted;
        public event EventHandler<WebhookDelivery>? WebhookDelivered;

        public IntegrationHub()
        {
            _defaultHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            InitializeHttpClients();
            StartBackgroundTasks();
        }

        #region Initialization

        private void InitializeHttpClients()
        {
            foreach (IntegrationType type in Enum.GetValues(typeof(IntegrationType)))
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(60)
                };

                // Set default headers based on integration type
                switch (type)
                {
                    case IntegrationType.BIM360:
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        break;
                    case IntegrationType.Procore:
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        break;
                    case IntegrationType.MicrosoftTeams:
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        break;
                    case IntegrationType.Slack:
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        break;
                }

                _httpClients[type] = client;
            }
        }

        private void StartBackgroundTasks()
        {
            _backgroundCts = new CancellationTokenSource();

            _schedulerTask = Task.Run(() => RunSchedulerAsync(_backgroundCts.Token));
            _webhookDeliveryTask = Task.Run(() => RunWebhookDeliveryAsync(_backgroundCts.Token));
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Create a new integration configuration
        /// </summary>
        public async Task<IntegrationConfiguration> CreateConfigurationAsync(
            IntegrationConfiguration config,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(config.Id))
                config.Id = Guid.NewGuid().ToString();

            config.CreatedAt = DateTime.UtcNow;
            config.Status = ConfigurationStatus.Draft;

            _configurations[config.Id] = config;

            // Initialize statistics for this configuration
            _statistics[config.Id] = new IntegrationStatistics
            {
                ConfigurationId = config.Id,
                IntegrationType = config.IntegrationType
            };

            Logger.Info($"Created integration configuration: {config.Name} ({config.IntegrationType})");

            return config;
        }

        /// <summary>
        /// Update an existing integration configuration
        /// </summary>
        public async Task<IntegrationConfiguration?> UpdateConfigurationAsync(
            string configId,
            Action<IntegrationConfiguration> updateAction,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryGetValue(configId, out var config))
            {
                Logger.Warn($"Configuration not found: {configId}");
                return null;
            }

            updateAction(config);
            config.LastModifiedAt = DateTime.UtcNow;

            Logger.Info($"Updated integration configuration: {config.Name}");

            return config;
        }

        /// <summary>
        /// Delete an integration configuration
        /// </summary>
        public async Task<bool> DeleteConfigurationAsync(
            string configId,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryRemove(configId, out var config))
            {
                Logger.Warn($"Configuration not found for deletion: {configId}");
                return false;
            }

            // Cancel any active sync jobs
            foreach (var job in _syncJobs.Values.Where(j => j.ConfigurationId == configId))
            {
                job.CancellationSource?.Cancel();
            }

            // Clean up statistics
            _statistics.TryRemove(configId, out _);

            Logger.Info($"Deleted integration configuration: {config.Name}");

            return true;
        }

        /// <summary>
        /// Get a configuration by ID
        /// </summary>
        public IntegrationConfiguration? GetConfiguration(string configId)
        {
            _configurations.TryGetValue(configId, out var config);
            return config;
        }

        /// <summary>
        /// Get all configurations
        /// </summary>
        public IEnumerable<IntegrationConfiguration> GetAllConfigurations()
        {
            return _configurations.Values.ToList();
        }

        /// <summary>
        /// Get configurations by integration type
        /// </summary>
        public IEnumerable<IntegrationConfiguration> GetConfigurationsByType(IntegrationType type)
        {
            return _configurations.Values.Where(c => c.IntegrationType == type).ToList();
        }

        #endregion

        #region Connection Testing

        /// <summary>
        /// Test connection to external system
        /// </summary>
        public async Task<ConnectionTestResult> TestConnectionAsync(
            string configId,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryGetValue(configId, out var config))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Configuration not found"
                };
            }

            return await TestConnectionAsync(config.ConnectionSettings, cancellationToken);
        }

        /// <summary>
        /// Test connection with specific settings
        /// </summary>
        public async Task<ConnectionTestResult> TestConnectionAsync(
            ConnectionSettings settings,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new ConnectionTestResult();

            try
            {
                var client = GetHttpClient(settings.IntegrationType);
                await ConfigureClientAuthAsync(client, settings, cancellationToken);

                var testEndpoint = GetTestEndpoint(settings);
                var response = await client.GetAsync(testEndpoint, cancellationToken);

                result.HttpStatusCode = (int)response.StatusCode;
                result.ResponseTime = DateTime.UtcNow - startTime;

                if (response.IsSuccessStatusCode)
                {
                    result.Success = true;
                    result.Message = "Connection successful";

                    // Try to get system info
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    result.SystemInfo = ParseSystemInfo(settings.IntegrationType, content);
                }
                else
                {
                    result.Success = false;
                    result.Message = $"Connection failed: {response.StatusCode}";
                    result.ErrorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                }

                // Update connection settings with test result
                settings.LastTestedAt = DateTime.UtcNow;
                settings.LastTestSuccessful = result.Success;
            }
            catch (TaskCanceledException)
            {
                result.Success = false;
                result.Message = "Connection timed out";
                result.ResponseTime = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Connection error: {ex.Message}";
                result.ErrorDetails = ex.ToString();
                result.ResponseTime = DateTime.UtcNow - startTime;

                Logger.Error(ex, $"Connection test failed for {settings.IntegrationType}");
            }

            return result;
        }

        private string GetTestEndpoint(ConnectionSettings settings)
        {
            return settings.IntegrationType switch
            {
                IntegrationType.BIM360 => $"{settings.BaseUrl}/project/v1/hubs",
                IntegrationType.Procore => $"{settings.BaseUrl}/rest/v1.0/me",
                IntegrationType.PlanGrid => $"{settings.BaseUrl}/projects",
                IntegrationType.Revizto => $"{settings.BaseUrl}/api/v1/projects",
                IntegrationType.SAP => $"{settings.BaseUrl}/sap/opu/odata/sap/API_BUSINESS_PARTNER/A_BusinessPartner?$top=1",
                IntegrationType.Oracle => $"{settings.BaseUrl}/fscmRestApi/resources/11.13.18.05/projects?limit=1",
                IntegrationType.MicrosoftProject => $"{settings.BaseUrl}/v1.0/me/projects",
                IntegrationType.Primavera => $"{settings.BaseUrl}/api/v1/projects?limit=1",
                IntegrationType.SharePoint => $"{settings.BaseUrl}/_api/web",
                IntegrationType.Slack => "https://slack.com/api/auth.test",
                IntegrationType.MicrosoftTeams => $"{settings.BaseUrl}/v1.0/me/joinedTeams",
                IntegrationType.Webhook => settings.BaseUrl,
                IntegrationType.CustomAPI => settings.BaseUrl,
                _ => settings.BaseUrl
            };
        }

        private ExternalSystemInfo? ParseSystemInfo(IntegrationType type, string content)
        {
            try
            {
                var json = JObject.Parse(content);
                return new ExternalSystemInfo
                {
                    Type = type,
                    Name = type.ToString(),
                    Version = json["version"]?.ToString() ?? "Unknown",
                    HealthStatus = SystemHealthStatus.Healthy
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Configuration Activation

        /// <summary>
        /// Activate an integration configuration
        /// </summary>
        public async Task<bool> ActivateConfigurationAsync(
            string configId,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryGetValue(configId, out var config))
            {
                Logger.Warn($"Configuration not found for activation: {configId}");
                return false;
            }

            // Test connection first
            var testResult = await TestConnectionAsync(configId, cancellationToken);
            if (!testResult.Success)
            {
                Logger.Warn($"Cannot activate configuration {configId}: Connection test failed");
                config.Status = ConfigurationStatus.Error;
                return false;
            }

            config.Status = ConfigurationStatus.Active;
            config.LastModifiedAt = DateTime.UtcNow;

            // Schedule initial sync if configured
            if (config.Schedule?.IsEnabled == true && config.Schedule.Frequency != SyncFrequency.Manual)
            {
                CalculateNextRunTime(config.Schedule);
            }

            Logger.Info($"Activated integration configuration: {config.Name}");

            return true;
        }

        /// <summary>
        /// Deactivate an integration configuration
        /// </summary>
        public async Task<bool> DeactivateConfigurationAsync(
            string configId,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryGetValue(configId, out var config))
            {
                return false;
            }

            config.Status = ConfigurationStatus.Disabled;
            config.LastModifiedAt = DateTime.UtcNow;

            // Cancel any running sync jobs
            foreach (var job in _syncJobs.Values.Where(j => j.ConfigurationId == configId && j.Status == SyncJobStatus.Running))
            {
                await CancelSyncJobAsync(job.Id, cancellationToken);
            }

            Logger.Info($"Deactivated integration configuration: {config.Name}");

            return true;
        }

        #endregion

        #region Sync Job Management

        /// <summary>
        /// Start a new synchronization job
        /// </summary>
        public async Task<SyncJob> StartSyncJobAsync(
            string configId,
            string? mappingId = null,
            string? initiatedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!_configurations.TryGetValue(configId, out var config))
            {
                throw new InvalidOperationException($"Configuration not found: {configId}");
            }

            if (config.Status != ConfigurationStatus.Active)
            {
                throw new InvalidOperationException($"Configuration is not active: {config.Status}");
            }

            var mappings = string.IsNullOrEmpty(mappingId)
                ? config.SyncMappings.Where(m => m.IsEnabled).ToList()
                : config.SyncMappings.Where(m => m.Id == mappingId && m.IsEnabled).ToList();

            if (!mappings.Any())
            {
                throw new InvalidOperationException("No enabled mappings found");
            }

            var job = new SyncJob
            {
                ConfigurationId = configId,
                MappingId = mappingId ?? "all",
                Status = SyncJobStatus.Pending,
                InitiatedBy = initiatedBy,
                CancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            };

            _syncJobs[job.Id] = job;

            // Start the sync in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteSyncJobAsync(job, config, mappings, job.CancellationSource!.Token);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Sync job failed: {job.Id}");
                    job.Status = SyncJobStatus.Failed;
                    job.Errors.Add(new SyncError
                    {
                        ErrorCode = "SYNC_FAILED",
                        ErrorMessage = ex.Message,
                        StackTrace = ex.StackTrace
                    });
                }
                finally
                {
                    job.CompletedAt = DateTime.UtcNow;
                    SyncJobCompleted?.Invoke(this, job);
                    UpdateStatistics(job);
                }
            });

            Logger.Info($"Started sync job: {job.Id} for configuration: {config.Name}");

            return job;
        }

        private async Task ExecuteSyncJobAsync(
            SyncJob job,
            IntegrationConfiguration config,
            List<SyncMapping> mappings,
            CancellationToken cancellationToken)
        {
            job.Status = SyncJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;

            var client = GetHttpClient(config.IntegrationType);
            await ConfigureClientAuthAsync(client, config.ConnectionSettings, cancellationToken);

            foreach (var mapping in mappings)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    job.Status = SyncJobStatus.Cancelled;
                    return;
                }

                job.Logs.Add(new SyncLog
                {
                    Level = "Info",
                    Message = $"Starting sync for mapping: {mapping.Name}"
                });

                try
                {
                    await SyncMappingAsync(job, config, mapping, client, cancellationToken);
                }
                catch (Exception ex)
                {
                    job.Errors.Add(new SyncError
                    {
                        ErrorCode = "MAPPING_FAILED",
                        ErrorMessage = $"Failed to sync mapping {mapping.Name}: {ex.Message}",
                        StackTrace = ex.StackTrace,
                        IsRetryable = true
                    });

                    Logger.Error(ex, $"Failed to sync mapping: {mapping.Name}");
                }

                mapping.LastSyncAt = DateTime.UtcNow;
            }

            job.Status = job.FailedRecords > 0 && job.SuccessfulRecords > 0
                ? SyncJobStatus.PartialSuccess
                : job.FailedRecords > 0
                    ? SyncJobStatus.Failed
                    : SyncJobStatus.Completed;
        }

        private async Task SyncMappingAsync(
            SyncJob job,
            IntegrationConfiguration config,
            SyncMapping mapping,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            // Fetch source data
            var sourceData = await FetchSourceDataAsync(config, mapping, client, cancellationToken);
            job.TotalRecords += sourceData.Count;

            // Process in batches
            var batches = sourceData
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / mapping.BatchSize)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (var record in batch)
                {
                    try
                    {
                        var transformed = TransformRecord(record, mapping);
                        await SendToTargetAsync(config, mapping, transformed, client, cancellationToken);
                        job.SuccessfulRecords++;
                    }
                    catch (Exception ex)
                    {
                        job.FailedRecords++;
                        job.Errors.Add(new SyncError
                        {
                            RecordId = record["id"]?.ToString() ?? "unknown",
                            ErrorCode = "TRANSFORM_FAILED",
                            ErrorMessage = ex.Message,
                            IsRetryable = true
                        });
                    }

                    job.ProcessedRecords++;

                    // Report progress
                    var progress = new SyncProgress
                    {
                        JobId = job.Id,
                        ProcessedRecords = job.ProcessedRecords,
                        TotalRecords = job.TotalRecords,
                        PercentComplete = job.ProgressPercentage,
                        CurrentOperation = $"Processing {mapping.SourceEntity}"
                    };

                    SyncProgressChanged?.Invoke(this, progress);
                }

                // Rate limiting between batches
                await Task.Delay(100, cancellationToken);
            }
        }

        private async Task<List<Dictionary<string, object>>> FetchSourceDataAsync(
            IntegrationConfiguration config,
            SyncMapping mapping,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            var endpoint = GetEntityEndpoint(config.IntegrationType, config.ConnectionSettings.BaseUrl, mapping.SourceEntity);
            var response = await client.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content);

            return data ?? new List<Dictionary<string, object>>();
        }

        private Dictionary<string, object> TransformRecord(
            Dictionary<string, object> source,
            SyncMapping mapping)
        {
            var result = new Dictionary<string, object>();

            foreach (var fieldMapping in mapping.FieldMappings)
            {
                if (!source.TryGetValue(fieldMapping.SourceField, out var sourceValue))
                {
                    if (fieldMapping.IsRequired)
                        throw new InvalidOperationException($"Required field missing: {fieldMapping.SourceField}");

                    if (fieldMapping.DefaultValue != null)
                        sourceValue = fieldMapping.DefaultValue;
                    else if (fieldMapping.SkipIfNull)
                        continue;
                }

                var transformedValue = ApplyTransformation(sourceValue, fieldMapping, source);
                result[fieldMapping.TargetField] = transformedValue!;
            }

            return result;
        }

        private object? ApplyTransformation(
            object? value,
            FieldMapping mapping,
            Dictionary<string, object> sourceRecord)
        {
            return mapping.TransformationType switch
            {
                FieldTransformationType.Direct => value,

                FieldTransformationType.ValueMapping => mapping.ValueMappings?.TryGetValue(
                    value?.ToString() ?? "", out var mapped) == true ? mapped : value,

                FieldTransformationType.Concatenation => string.Join(
                    mapping.ConcatenationSeparator ?? " ",
                    (mapping.ConcatenationFields ?? new List<string>())
                        .Select(f => sourceRecord.TryGetValue(f, out var v) ? v?.ToString() : "")
                        .Where(s => !string.IsNullOrEmpty(s))),

                FieldTransformationType.Lookup => LookupValue(value, mapping),

                FieldTransformationType.DateFormat => value is DateTime dt
                    ? dt.ToString(mapping.DateFormat ?? "yyyy-MM-dd")
                    : value,

                FieldTransformationType.NumberFormat => value is double num
                    ? num.ToString(mapping.NumberFormat ?? "N2")
                    : value,

                FieldTransformationType.Formula => EvaluateFormula(mapping.Formula, sourceRecord),

                FieldTransformationType.Conditional => EvaluateCondition(mapping.ConditionalExpression, value, sourceRecord),

                FieldTransformationType.Split => value?.ToString()?.Split(
                    mapping.ConcatenationSeparator?[0] ?? ',').FirstOrDefault(),

                FieldTransformationType.Custom => ApplyCustomTransformation(value, mapping, sourceRecord),

                _ => value
            };
        }

        private object? LookupValue(object? value, FieldMapping mapping)
        {
            if (value == null || string.IsNullOrEmpty(mapping.LookupTable))
                return value;

            if (_lookupTables.TryGetValue(mapping.LookupTable, out var table))
            {
                if (table.TryGetValue(value.ToString()!, out var lookupValue))
                    return lookupValue;
            }

            return value;
        }

        private object? EvaluateFormula(string? formula, Dictionary<string, object> record)
        {
            if (string.IsNullOrEmpty(formula))
                return null;

            // Simple formula evaluation - in production would use expression parser
            // Format: {field1} + {field2} or {field1} * 2
            var result = formula;
            foreach (var kvp in record)
            {
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? "0");
            }

            return result;
        }

        private object? EvaluateCondition(string? expression, object? value, Dictionary<string, object> record)
        {
            // Simple conditional - in production would use expression evaluator
            // Format: if({field} == 'value', 'trueResult', 'falseResult')
            return value;
        }

        private object? ApplyCustomTransformation(object? value, FieldMapping mapping, Dictionary<string, object> record)
        {
            // Custom transformation logic based on mapping.CustomTransformerType
            return value;
        }

        private async Task SendToTargetAsync(
            IntegrationConfiguration config,
            SyncMapping mapping,
            Dictionary<string, object> data,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            var endpoint = GetEntityEndpoint(config.IntegrationType, config.ConnectionSettings.BaseUrl, mapping.TargetEntity);
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private string GetEntityEndpoint(IntegrationType type, string baseUrl, string entity)
        {
            return type switch
            {
                IntegrationType.BIM360 => $"{baseUrl}/data/v1/{entity}",
                IntegrationType.Procore => $"{baseUrl}/rest/v1.0/{entity}",
                IntegrationType.SAP => $"{baseUrl}/sap/opu/odata/sap/{entity}",
                IntegrationType.Oracle => $"{baseUrl}/fscmRestApi/resources/11.13.18.05/{entity}",
                IntegrationType.SharePoint => $"{baseUrl}/_api/lists/getbytitle('{entity}')/items",
                _ => $"{baseUrl}/{entity}"
            };
        }

        /// <summary>
        /// Get sync job by ID
        /// </summary>
        public SyncJob? GetSyncJob(string jobId)
        {
            _syncJobs.TryGetValue(jobId, out var job);
            return job;
        }

        /// <summary>
        /// Get all sync jobs for a configuration
        /// </summary>
        public IEnumerable<SyncJob> GetSyncJobs(string configId)
        {
            return _syncJobs.Values.Where(j => j.ConfigurationId == configId).ToList();
        }

        /// <summary>
        /// Cancel a running sync job
        /// </summary>
        public async Task<bool> CancelSyncJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            if (!_syncJobs.TryGetValue(jobId, out var job))
                return false;

            if (job.Status != SyncJobStatus.Running && job.Status != SyncJobStatus.Paused)
                return false;

            job.CancellationSource?.Cancel();
            job.Status = SyncJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;

            job.Logs.Add(new SyncLog
            {
                Level = "Warning",
                Message = "Sync job cancelled by user"
            });

            Logger.Info($"Cancelled sync job: {jobId}");

            return true;
        }

        #endregion

        #region Webhook Management

        /// <summary>
        /// Register a new webhook
        /// </summary>
        public WebhookConfig RegisterWebhook(
            string name,
            string url,
            string secret,
            params string[] eventTypes)
        {
            var webhook = new WebhookConfig
            {
                Name = name,
                Url = url,
                Secret = secret,
                EventTypes = eventTypes.ToList()
            };

            _webhooks[webhook.Id] = webhook;
            _deliveryHistory[webhook.Id] = new List<WebhookDelivery>();

            Logger.Info($"Registered webhook: {name} for events: {string.Join(", ", eventTypes)}");

            return webhook;
        }

        /// <summary>
        /// Update webhook configuration
        /// </summary>
        public WebhookConfig? UpdateWebhook(string webhookId, Action<WebhookConfig> updateAction)
        {
            if (!_webhooks.TryGetValue(webhookId, out var webhook))
                return null;

            updateAction(webhook);
            return webhook;
        }

        /// <summary>
        /// Unregister a webhook
        /// </summary>
        public bool UnregisterWebhook(string webhookId)
        {
            return _webhooks.TryRemove(webhookId, out _);
        }

        /// <summary>
        /// Get all registered webhooks
        /// </summary>
        public IEnumerable<WebhookConfig> GetWebhooks()
        {
            return _webhooks.Values.ToList();
        }

        /// <summary>
        /// Get webhook delivery history
        /// </summary>
        public IEnumerable<WebhookDelivery> GetWebhookDeliveries(string webhookId, int limit = 100)
        {
            if (!_deliveryHistory.TryGetValue(webhookId, out var deliveries))
                return Enumerable.Empty<WebhookDelivery>();

            return deliveries.OrderByDescending(d => d.CreatedAt).Take(limit).ToList();
        }

        /// <summary>
        /// Trigger webhook for an event
        /// </summary>
        public async Task TriggerWebhookAsync(
            string eventType,
            object data,
            CancellationToken cancellationToken = default)
        {
            var webhookEvent = new WebhookEvent
            {
                EventType = eventType,
                Data = data
            };

            var matchingWebhooks = _webhooks.Values
                .Where(w => w.IsActive && w.EventTypes.Contains(eventType))
                .ToList();

            foreach (var webhook in matchingWebhooks)
            {
                var delivery = new WebhookDelivery
                {
                    WebhookId = webhook.Id,
                    EventType = eventType,
                    Payload = JsonConvert.SerializeObject(webhookEvent)
                };

                if (_deliveryHistory.TryGetValue(webhook.Id, out var history))
                {
                    history.Add(delivery);
                }

                await DeliverWebhookAsync(webhook, delivery, cancellationToken);
            }
        }

        private async Task DeliverWebhookAsync(
            WebhookConfig webhook,
            WebhookDelivery delivery,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(webhook.TimeoutSeconds) };

                var content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");

                // Add signature
                var signature = ComputeWebhookSignature(delivery.Payload, webhook.Secret);
                content.Headers.Add("X-StingBIM-Signature", signature);
                content.Headers.Add("X-StingBIM-Event", delivery.EventType);
                content.Headers.Add("X-StingBIM-Delivery", delivery.Id);

                // Add custom headers
                foreach (var header in webhook.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                var response = await client.PostAsync(webhook.Url, content, cancellationToken);

                delivery.HttpStatusCode = (int)response.StatusCode;
                delivery.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                delivery.ResponseTime = DateTime.UtcNow - startTime;
                delivery.AttemptCount++;

                if (response.IsSuccessStatusCode)
                {
                    delivery.Status = WebhookDeliveryStatus.Delivered;
                    delivery.DeliveredAt = DateTime.UtcNow;
                    webhook.SuccessfulDeliveries++;

                    WebhookDelivered?.Invoke(this, delivery);
                }
                else
                {
                    delivery.Status = WebhookDeliveryStatus.Failed;
                    delivery.ErrorMessage = $"HTTP {delivery.HttpStatusCode}";
                    webhook.FailedDeliveries++;

                    // Schedule retry if applicable
                    if (delivery.AttemptCount < webhook.MaxRetries)
                    {
                        delivery.Status = WebhookDeliveryStatus.Retrying;
                        delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(webhook.RetryDelaySeconds * delivery.AttemptCount);
                    }
                }

                webhook.TotalDeliveries++;
                webhook.LastTriggeredAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.ErrorMessage = ex.Message;
                delivery.AttemptCount++;

                if (delivery.AttemptCount < webhook.MaxRetries)
                {
                    delivery.Status = WebhookDeliveryStatus.Retrying;
                    delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(webhook.RetryDelaySeconds * delivery.AttemptCount);
                }

                Logger.Error(ex, $"Webhook delivery failed: {webhook.Name}");
            }
        }

        private string ComputeWebhookSignature(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics for an integration configuration
        /// </summary>
        public IntegrationStatistics? GetStatistics(string configId)
        {
            _statistics.TryGetValue(configId, out var stats);
            return stats;
        }

        /// <summary>
        /// Get all integration statistics
        /// </summary>
        public IEnumerable<IntegrationStatistics> GetAllStatistics()
        {
            return _statistics.Values.ToList();
        }

        private void UpdateStatistics(SyncJob job)
        {
            if (!_statistics.TryGetValue(job.ConfigurationId, out var stats))
                return;

            stats.TotalSyncJobs++;
            stats.TotalRecordsSynced += job.SuccessfulRecords;
            stats.TotalErrors += job.FailedRecords;
            stats.TotalConflicts += job.ConflictRecords;
            stats.LastSyncAt = job.CompletedAt;

            if (job.Status == SyncJobStatus.Completed)
                stats.SuccessfulSyncJobs++;
            else if (job.Status == SyncJobStatus.Failed)
                stats.FailedSyncJobs++;

            // Update average duration
            if (job.Duration.HasValue)
            {
                var totalDuration = stats.AverageSyncDuration.TotalSeconds * (stats.TotalSyncJobs - 1) + job.Duration.Value.TotalSeconds;
                stats.AverageSyncDuration = TimeSpan.FromSeconds(totalDuration / stats.TotalSyncJobs);
            }

            // Update errors by type
            foreach (var error in job.Errors)
            {
                if (!stats.ErrorsByType.ContainsKey(error.ErrorCode))
                    stats.ErrorsByType[error.ErrorCode] = 0;
                stats.ErrorsByType[error.ErrorCode]++;
            }

            // Update daily statistics
            var today = DateTime.UtcNow.Date;
            var dailyStat = stats.DailyStatistics.FirstOrDefault(d => d.Date == today);
            if (dailyStat == null)
            {
                dailyStat = new DailyStats { Date = today };
                stats.DailyStatistics.Add(dailyStat);
            }
            dailyStat.SyncJobs++;
            dailyStat.RecordsSynced += job.SuccessfulRecords;
            dailyStat.Errors += job.FailedRecords;

            // Keep only last 30 days
            stats.DailyStatistics = stats.DailyStatistics
                .OrderByDescending(d => d.Date)
                .Take(30)
                .ToList();

            // Calculate uptime
            if (stats.TotalSyncJobs > 0)
            {
                stats.UptimePercentage = (stats.SuccessfulSyncJobs * 100.0) / stats.TotalSyncJobs;
            }

            // Update next scheduled sync
            if (_configurations.TryGetValue(job.ConfigurationId, out var config) && config.Schedule != null)
            {
                stats.NextScheduledSync = config.Schedule.NextRunAt;
            }
        }

        #endregion

        #region Background Tasks

        private async Task RunSchedulerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    foreach (var config in _configurations.Values)
                    {
                        if (config.Status != ConfigurationStatus.Active)
                            continue;

                        if (config.Schedule?.IsEnabled != true)
                            continue;

                        if (config.Schedule.NextRunAt.HasValue && config.Schedule.NextRunAt <= now)
                        {
                            Logger.Info($"Scheduled sync triggered for: {config.Name}");

                            try
                            {
                                await StartSyncJobAsync(config.Id, initiatedBy: "Scheduler", cancellationToken: cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, $"Scheduled sync failed for: {config.Name}");
                            }

                            CalculateNextRunTime(config.Schedule);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Scheduler error");
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                }
            }
        }

        private void CalculateNextRunTime(SyncSchedule schedule)
        {
            var now = DateTime.UtcNow;

            schedule.LastRunAt = now;

            schedule.NextRunAt = schedule.Frequency switch
            {
                SyncFrequency.RealTime => now.AddMinutes(5),
                SyncFrequency.Hourly => now.AddHours(1),
                SyncFrequency.Daily => now.Date.AddDays(1).Add(schedule.TimeOfDay ?? TimeSpan.Zero),
                SyncFrequency.Weekly => GetNextWeeklyRun(now, schedule),
                SyncFrequency.Monthly => now.Date.AddMonths(1).Add(schedule.TimeOfDay ?? TimeSpan.Zero),
                SyncFrequency.Custom => schedule.Interval.HasValue ? now.Add(schedule.Interval.Value) : now.AddHours(1),
                _ => null
            };
        }

        private DateTime GetNextWeeklyRun(DateTime now, SyncSchedule schedule)
        {
            var daysToAdd = 7;
            if (schedule.DaysOfWeek?.Any() == true)
            {
                for (int i = 1; i <= 7; i++)
                {
                    var checkDay = now.AddDays(i).DayOfWeek;
                    if (schedule.DaysOfWeek.Contains(checkDay))
                    {
                        daysToAdd = i;
                        break;
                    }
                }
            }

            return now.Date.AddDays(daysToAdd).Add(schedule.TimeOfDay ?? TimeSpan.Zero);
        }

        private async Task RunWebhookDeliveryAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    foreach (var historyEntry in _deliveryHistory)
                    {
                        var pendingRetries = historyEntry.Value
                            .Where(d => d.Status == WebhookDeliveryStatus.Retrying && d.NextRetryAt <= now)
                            .ToList();

                        foreach (var delivery in pendingRetries)
                        {
                            if (_webhooks.TryGetValue(delivery.WebhookId, out var webhook))
                            {
                                Logger.Info($"Retrying webhook delivery: {delivery.Id}");
                                await DeliverWebhookAsync(webhook, delivery, cancellationToken);
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Webhook delivery error");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }
        }

        #endregion

        #region Lookup Table Management

        /// <summary>
        /// Register a lookup table for field transformations
        /// </summary>
        public void RegisterLookupTable(string tableName, Dictionary<string, object> values)
        {
            _lookupTables[tableName] = values;
            Logger.Debug($"Registered lookup table: {tableName} with {values.Count} entries");
        }

        /// <summary>
        /// Remove a lookup table
        /// </summary>
        public bool RemoveLookupTable(string tableName)
        {
            return _lookupTables.TryRemove(tableName, out _);
        }

        /// <summary>
        /// Get all lookup table names
        /// </summary>
        public IEnumerable<string> GetLookupTableNames()
        {
            return _lookupTables.Keys.ToList();
        }

        #endregion

        #region HTTP Client Helpers

        private HttpClient GetHttpClient(IntegrationType type)
        {
            return _httpClients.TryGetValue(type, out var client) ? client : _defaultHttpClient;
        }

        private async Task ConfigureClientAuthAsync(
            HttpClient client,
            ConnectionSettings settings,
            CancellationToken cancellationToken)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            switch (settings.AuthType)
            {
                case AuthenticationType.ApiKey:
                    if (!string.IsNullOrEmpty(settings.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add("X-API-Key", settings.ApiKey);
                    }
                    break;

                case AuthenticationType.BasicAuth:
                    if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
                    {
                        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    }
                    break;

                case AuthenticationType.BearerToken:
                    if (!string.IsNullOrEmpty(settings.AccessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
                    }
                    break;

                case AuthenticationType.OAuth2ClientCredentials:
                    await EnsureValidOAuthTokenAsync(settings, cancellationToken);
                    if (!string.IsNullOrEmpty(settings.AccessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
                    }
                    break;

                case AuthenticationType.OAuth2AuthorizationCode:
                case AuthenticationType.OAuth2PKCE:
                    await EnsureValidOAuthTokenAsync(settings, cancellationToken);
                    if (!string.IsNullOrEmpty(settings.AccessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
                    }
                    break;
            }

            // Add custom headers
            foreach (var header in settings.CustomHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        private async Task EnsureValidOAuthTokenAsync(
            ConnectionSettings settings,
            CancellationToken cancellationToken)
        {
            // Check if token is still valid
            if (!string.IsNullOrEmpty(settings.AccessToken) &&
                settings.TokenExpiresAt.HasValue &&
                settings.TokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                return; // Token is still valid
            }

            // Try to refresh token
            if (!string.IsNullOrEmpty(settings.RefreshToken))
            {
                await RefreshOAuthTokenAsync(settings, cancellationToken);
                return;
            }

            // Get new token using client credentials
            if (settings.AuthType == AuthenticationType.OAuth2ClientCredentials)
            {
                await GetClientCredentialsTokenAsync(settings, cancellationToken);
            }
        }

        private async Task GetClientCredentialsTokenAsync(
            ConnectionSettings settings,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(settings.TokenEndpoint))
                return;

            using var client = new HttpClient();

            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId ?? "",
                ["client_secret"] = settings.ClientSecret ?? ""
            };

            if (!string.IsNullOrEmpty(settings.Scope))
                requestData["scope"] = settings.Scope;

            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync(settings.TokenEndpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonConvert.DeserializeObject<OAuthTokenResponse>(json);

                if (tokenResponse != null)
                {
                    settings.AccessToken = tokenResponse.AccessToken;
                    settings.RefreshToken = tokenResponse.RefreshToken;
                    settings.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                }
            }
        }

        private async Task RefreshOAuthTokenAsync(
            ConnectionSettings settings,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(settings.TokenEndpoint) || string.IsNullOrEmpty(settings.RefreshToken))
                return;

            using var client = new HttpClient();

            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = settings.RefreshToken,
                ["client_id"] = settings.ClientId ?? "",
                ["client_secret"] = settings.ClientSecret ?? ""
            };

            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync(settings.TokenEndpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonConvert.DeserializeObject<OAuthTokenResponse>(json);

                if (tokenResponse != null)
                {
                    settings.AccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        settings.RefreshToken = tokenResponse.RefreshToken;
                    settings.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                }
            }
        }

        #endregion

        #region Rate Limiting

        private bool CheckRateLimit(string key, int maxRequests = 60, TimeSpan? window = null)
        {
            var limiter = _rateLimiters.GetOrAdd(key, _ => new RateLimiter(maxRequests, window ?? TimeSpan.FromMinutes(1)));
            return limiter.TryAcquire();
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            _backgroundCts?.Cancel();

            if (_schedulerTask != null)
            {
                try
                {
                    await _schedulerTask;
                }
                catch (TaskCanceledException) { }
            }

            if (_webhookDeliveryTask != null)
            {
                try
                {
                    await _webhookDeliveryTask;
                }
                catch (TaskCanceledException) { }
            }

            // Cancel all running sync jobs
            foreach (var job in _syncJobs.Values.Where(j => j.Status == SyncJobStatus.Running))
            {
                job.CancellationSource?.Cancel();
            }

            // Dispose HTTP clients
            _defaultHttpClient.Dispose();
            foreach (var client in _httpClients.Values)
            {
                client.Dispose();
            }

            _backgroundCts?.Dispose();

            Logger.Info("IntegrationHub disposed");
        }

        #endregion
    }

    #region Utilities

    /// <summary>
    /// Token bucket rate limiter for API calls
    /// </summary>
    internal class RateLimiter
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _requests = new();
        private readonly object _lock = new();

        public RateLimiter(int maxRequests, TimeSpan window)
        {
            _maxRequests = maxRequests;
            _window = window;
        }

        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - _window;

                // Remove expired requests
                while (_requests.Count > 0 && _requests.Peek() < cutoff)
                {
                    _requests.Dequeue();
                }

                if (_requests.Count < _maxRequests)
                {
                    _requests.Enqueue(now);
                    return true;
                }

                return false;
            }
        }

        public TimeSpan? GetTimeUntilNextSlot()
        {
            lock (_lock)
            {
                if (_requests.Count < _maxRequests)
                    return TimeSpan.Zero;

                var oldest = _requests.Peek();
                var waitUntil = oldest + _window;
                var wait = waitUntil - DateTime.UtcNow;

                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }
        }
    }

    #endregion
}
