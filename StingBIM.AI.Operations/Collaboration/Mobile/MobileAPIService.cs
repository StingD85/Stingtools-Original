// ===================================================================
// StingBIM.AI.Collaboration - Mobile API Service
// Provides RESTful endpoints for mobile applications
// Optimized for bandwidth and battery efficiency
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StingBIM.AI.Collaboration.Infrastructure;
using StingBIM.AI.Collaboration.Offline;
using StingBIM.AI.Collaboration.Security;

namespace StingBIM.AI.Collaboration.Mobile
{
    #region API Configuration

    /// <summary>
    /// Mobile API configuration
    /// </summary>
    public class MobileAPIConfiguration
    {
        public int MaxPageSize { get; set; } = 50;
        public int DefaultPageSize { get; set; } = 20;
        public int MaxUploadSizeMB { get; set; } = 50;
        public int ThumbnailSize { get; set; } = 200;
        public bool EnableCompression { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationMinutes { get; set; } = 5;
        public bool EnableOfflineSupport { get; set; } = true;
        public List<string> SupportedImageFormats { get; set; } = new() { ".jpg", ".jpeg", ".png", ".webp" };
        public int MaxConcurrentUploads { get; set; } = 3;
    }

    #endregion

    #region API Request/Response Models

    /// <summary>
    /// Base API response
    /// </summary>
    public class APIResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public APIError? Error { get; set; }
        public APIMeta? Meta { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        public static APIResponse<T> Ok(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            Message = message
        };

        public static APIResponse<T> Fail(string errorCode, string message) => new()
        {
            Success = false,
            Error = new APIError { Code = errorCode, Message = message }
        };

        public static APIResponse<T> Fail(CollaborationException ex) => new()
        {
            Success = false,
            Error = new APIError
            {
                Code = ex.ErrorCode,
                Message = ex.Message,
                CorrelationId = ex.CorrelationId,
                Details = ex.Context
            }
        };
    }

    /// <summary>
    /// API error details
    /// </summary>
    public class APIError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CorrelationId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// API metadata for pagination
    /// </summary>
    public class APIMeta
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContinuationToken { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Paginated request
    /// </summary>
    public class PagedRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = true;
        public string? Filter { get; set; }
        public string? Search { get; set; }
        public DateTime? ModifiedSince { get; set; }
    }

    /// <summary>
    /// Paginated response
    /// </summary>
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public APIMeta Meta { get; set; } = new();
    }

    #endregion

    #region Mobile DTOs

    /// <summary>
    /// Lightweight project DTO for mobile
    /// </summary>
    public class MobileProjectDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int OpenIssueCount { get; set; }
        public int PendingRFICount { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsAvailableOffline { get; set; }
    }

    /// <summary>
    /// Lightweight issue DTO for mobile
    /// </summary>
    public class MobileIssueDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string IssueType { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime? DueDate { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int CommentCount { get; set; }
        public int AttachmentCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool HasLocation { get; set; }
    }

    /// <summary>
    /// Lightweight document DTO for mobile
    /// </summary>
    public class MobileDocumentDTO
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Version { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public bool IsDownloaded { get; set; }
    }

    /// <summary>
    /// Lightweight RFI DTO for mobile
    /// </summary>
    public class MobileRFIDTO
    {
        public string Id { get; set; } = string.Empty;
        public string RFINumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public int AttachmentCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Lightweight inspection DTO for mobile
    /// </summary>
    public class MobileInspectionDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string InspectionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string? AssignedTo { get; set; }
        public string? Location { get; set; }
        public int ChecklistItemCount { get; set; }
        public int CompletedItemCount { get; set; }
        public int PhotoCount { get; set; }
    }

    /// <summary>
    /// Photo capture request from mobile
    /// </summary>
    public class PhotoCaptureRequest
    {
        public string ProjectId { get; set; } = string.Empty;
        public string? LinkedEntityType { get; set; }
        public string? LinkedEntityId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Quick action for mobile home screen
    /// </summary>
    public class QuickAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public int? BadgeCount { get; set; }
        public string? TargetUrl { get; set; }
    }

    /// <summary>
    /// Mobile dashboard summary
    /// </summary>
    public class MobileDashboardDTO
    {
        public List<MobileProjectDTO> RecentProjects { get; set; } = new();
        public List<QuickAction> QuickActions { get; set; } = new();
        public MobileNotificationSummary Notifications { get; set; } = new();
        public MobileSyncStatus SyncStatus { get; set; } = new();
    }

    /// <summary>
    /// Notification summary
    /// </summary>
    public class MobileNotificationSummary
    {
        public int UnreadCount { get; set; }
        public int MentionCount { get; set; }
        public int AssignmentCount { get; set; }
        public int OverdueCount { get; set; }
    }

    /// <summary>
    /// Sync status for mobile
    /// </summary>
    public class MobileSyncStatus
    {
        public bool IsSyncing { get; set; }
        public DateTime? LastSyncAt { get; set; }
        public int PendingChanges { get; set; }
        public int ConflictCount { get; set; }
        public double? DownloadProgress { get; set; }
    }

    #endregion

    #region Mobile API Service

    /// <summary>
    /// Mobile API service - main entry point
    /// </summary>
    public class MobileAPIService : IAsyncDisposable
    {
        private readonly MobileAPIConfiguration _config;
        private readonly ILogger? _logger;
        private readonly RBACSystem? _rbac;
        private readonly OfflineSyncManager? _offlineManager;
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly ResiliencePipeline _pipeline;
        private readonly ConcurrentDictionary<string, string> _userSessions = new();

        public MobileAPIService(
            MobileAPIConfiguration? config = null,
            RBACSystem? rbac = null,
            OfflineSyncManager? offlineManager = null,
            ILogger? logger = null)
        {
            _config = config ?? new MobileAPIConfiguration();
            _rbac = rbac;
            _offlineManager = offlineManager;
            _logger = logger;

            _pipeline = new ResiliencePipelineBuilder()
                .WithName("mobile-api")
                .WithRetry(new RetryOptions { MaxRetries = 2 })
                .WithTimeout(TimeSpan.FromSeconds(30))
                .WithLogger(logger!)
                .Build();

            _logger?.LogInformation("MobileAPIService initialized");
        }

        #region Authentication

        /// <summary>
        /// Authenticate mobile device
        /// </summary>
        public async Task<APIResponse<AuthenticationResult>> AuthenticateAsync(
            AuthenticationRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return APIResponse<AuthenticationResult>.Fail("AUTH_INVALID", "Email and password required");
                }

                // Authenticate (simulated - integrate with actual auth provider)
                var user = _rbac?.GetUserByEmail(request.Email);
                if (user == null || !user.IsActive)
                {
                    return APIResponse<AuthenticationResult>.Fail("AUTH_FAILED", "Invalid credentials");
                }

                // Generate session token
                var token = GenerateSessionToken();
                _userSessions[token] = user.Id;

                var result = new AuthenticationResult
                {
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    User = new MobileUserDTO
                    {
                        Id = user.Id,
                        Email = user.Email,
                        DisplayName = user.DisplayName,
                        Company = user.Company
                    }
                };

                _logger?.LogInformation("User {Email} authenticated on mobile", request.Email);
                return APIResponse<AuthenticationResult>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Authentication failed");
                return APIResponse<AuthenticationResult>.Fail("AUTH_ERROR", "Authentication error");
            }
        }

        /// <summary>
        /// Validate session token
        /// </summary>
        public bool ValidateToken(string token, out string? userId)
        {
            return _userSessions.TryGetValue(token, out userId);
        }

        /// <summary>
        /// Logout
        /// </summary>
        public void Logout(string token)
        {
            _userSessions.TryRemove(token, out _);
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Get mobile dashboard
        /// </summary>
        public async Task<APIResponse<MobileDashboardDTO>> GetDashboardAsync(
            string userId,
            CancellationToken ct = default)
        {
            try
            {
                var dashboard = new MobileDashboardDTO
                {
                    QuickActions = GetQuickActions(userId),
                    Notifications = await GetNotificationSummaryAsync(userId, ct),
                    SyncStatus = GetSyncStatus()
                };

                // Get recent projects (cached)
                var cacheKey = $"dashboard:{userId}";
                if (!_cache.TryGetValue(cacheKey, out var cached))
                {
                    dashboard.RecentProjects = await GetRecentProjectsAsync(userId, 5, ct);
                    if (_config.EnableCaching)
                    {
                        _cache[cacheKey] = dashboard.RecentProjects;
                    }
                }
                else
                {
                    dashboard.RecentProjects = (List<MobileProjectDTO>)cached;
                }

                return APIResponse<MobileDashboardDTO>.Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get dashboard for user {UserId}", userId);
                return APIResponse<MobileDashboardDTO>.Fail("DASHBOARD_ERROR", "Failed to load dashboard");
            }
        }

        private List<QuickAction> GetQuickActions(string userId)
        {
            return new List<QuickAction>
            {
                new QuickAction
                {
                    Id = "create-issue",
                    Title = "Create Issue",
                    Icon = "plus-circle",
                    ActionType = "create",
                    TargetUrl = "/issues/new"
                },
                new QuickAction
                {
                    Id = "capture-photo",
                    Title = "Take Photo",
                    Icon = "camera",
                    ActionType = "capture",
                    TargetUrl = "/photo/capture"
                },
                new QuickAction
                {
                    Id = "my-tasks",
                    Title = "My Tasks",
                    Icon = "check-square",
                    ActionType = "list",
                    BadgeCount = 5, // Would be calculated
                    TargetUrl = "/tasks/mine"
                },
                new QuickAction
                {
                    Id = "scan-qr",
                    Title = "Scan QR",
                    Icon = "qr-code",
                    ActionType = "scan",
                    TargetUrl = "/scan"
                }
            };
        }

        private async Task<MobileNotificationSummary> GetNotificationSummaryAsync(
            string userId,
            CancellationToken ct)
        {
            // Would query actual notification system
            return new MobileNotificationSummary
            {
                UnreadCount = 12,
                MentionCount = 3,
                AssignmentCount = 5,
                OverdueCount = 2
            };
        }

        private MobileSyncStatus GetSyncStatus()
        {
            if (_offlineManager == null)
            {
                return new MobileSyncStatus { IsSyncing = false };
            }

            var stats = _offlineManager.GetStorageStats();
            return new MobileSyncStatus
            {
                IsSyncing = _offlineManager.IsSyncing,
                LastSyncAt = _offlineManager.LastSyncAt,
                PendingChanges = stats.PendingChanges,
                ConflictCount = stats.ConflictCount
            };
        }

        private async Task<List<MobileProjectDTO>> GetRecentProjectsAsync(
            string userId,
            int count,
            CancellationToken ct)
        {
            // Would query actual project data
            return new List<MobileProjectDTO>();
        }

        #endregion

        #region Projects

        /// <summary>
        /// Get projects list
        /// </summary>
        public async Task<APIResponse<PagedResponse<MobileProjectDTO>>> GetProjectsAsync(
            string userId,
            PagedRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Validate page size
                request.PageSize = Math.Min(request.PageSize, _config.MaxPageSize);

                // Query projects (simulated)
                var projects = new List<MobileProjectDTO>();

                var response = new PagedResponse<MobileProjectDTO>
                {
                    Items = projects,
                    Meta = new APIMeta
                    {
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalItems = projects.Count,
                        TotalPages = (int)Math.Ceiling(projects.Count / (double)request.PageSize),
                        HasNextPage = false,
                        HasPreviousPage = request.Page > 1
                    }
                };

                return APIResponse<PagedResponse<MobileProjectDTO>>.Ok(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get projects");
                return APIResponse<PagedResponse<MobileProjectDTO>>.Fail("PROJECTS_ERROR", "Failed to load projects");
            }
        }

        #endregion

        #region Issues

        /// <summary>
        /// Get issues list
        /// </summary>
        public async Task<APIResponse<PagedResponse<MobileIssueDTO>>> GetIssuesAsync(
            string userId,
            string projectId,
            PagedRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Check permission
                if (_rbac != null)
                {
                    var access = _rbac.CheckAccess(userId, Actions.ViewIssue, ResourceType.Issue, projectId);
                    if (!access.IsAllowed)
                    {
                        return APIResponse<PagedResponse<MobileIssueDTO>>.Fail("FORBIDDEN", "Access denied");
                    }
                }

                request.PageSize = Math.Min(request.PageSize, _config.MaxPageSize);

                // Query issues (simulated)
                var issues = new List<MobileIssueDTO>();

                var response = new PagedResponse<MobileIssueDTO>
                {
                    Items = issues,
                    Meta = new APIMeta
                    {
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalItems = 0,
                        TotalPages = 0,
                        HasNextPage = false,
                        HasPreviousPage = false,
                        LastModified = DateTime.UtcNow
                    }
                };

                return APIResponse<PagedResponse<MobileIssueDTO>>.Ok(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get issues");
                return APIResponse<PagedResponse<MobileIssueDTO>>.Fail("ISSUES_ERROR", "Failed to load issues");
            }
        }

        /// <summary>
        /// Create issue from mobile
        /// </summary>
        public async Task<APIResponse<MobileIssueDTO>> CreateIssueAsync(
            string userId,
            CreateMobileIssueRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Validate
                var errors = new List<ValidationError>();
                if (string.IsNullOrWhiteSpace(request.Title))
                    errors.Add(new ValidationError { Field = "title", Message = "Title is required" });
                if (string.IsNullOrWhiteSpace(request.ProjectId))
                    errors.Add(new ValidationError { Field = "projectId", Message = "Project is required" });

                if (errors.Any())
                {
                    throw new ValidationException(errors);
                }

                // Check permission
                if (_rbac != null)
                {
                    _rbac.RequireAccess(userId, Actions.CreateIssue, ResourceType.Issue, request.ProjectId);
                }

                // Create issue
                var issue = new MobileIssueDTO
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = request.Title,
                    Status = "Open",
                    Priority = request.Priority ?? "Normal",
                    IssueType = request.IssueType ?? "General",
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    HasLocation = request.Latitude.HasValue && request.Longitude.HasValue
                };

                // Save offline if needed
                if (_offlineManager != null)
                {
                    await _offlineManager.SaveAsync(
                        "Issue",
                        issue.Id,
                        issue,
                        userId,
                        request.ProjectId,
                        ct);
                }

                _logger?.LogInformation("Issue {IssueId} created by {UserId}", issue.Id, userId);
                return APIResponse<MobileIssueDTO>.Ok(issue, "Issue created successfully");
            }
            catch (CollaborationException ex)
            {
                return APIResponse<MobileIssueDTO>.Fail(ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create issue");
                return APIResponse<MobileIssueDTO>.Fail("CREATE_ERROR", "Failed to create issue");
            }
        }

        #endregion

        #region Photos

        /// <summary>
        /// Upload photo from mobile
        /// </summary>
        public async Task<APIResponse<PhotoUploadResult>> UploadPhotoAsync(
            string userId,
            PhotoCaptureRequest request,
            Stream photoStream,
            string fileName,
            CancellationToken ct = default)
        {
            try
            {
                // Validate file size
                if (photoStream.Length > _config.MaxUploadSizeMB * 1024 * 1024)
                {
                    return APIResponse<PhotoUploadResult>.Fail(
                        "FILE_TOO_LARGE",
                        $"Maximum file size is {_config.MaxUploadSizeMB}MB");
                }

                // Validate file type
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!_config.SupportedImageFormats.Contains(extension))
                {
                    return APIResponse<PhotoUploadResult>.Fail(
                        "INVALID_FORMAT",
                        $"Supported formats: {string.Join(", ", _config.SupportedImageFormats)}");
                }

                // Save photo
                var photoId = Guid.NewGuid().ToString();
                var savedPath = Path.Combine("photos", request.ProjectId, $"{photoId}{extension}");

                // In real implementation, would save to blob storage
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms, ct);
                var photoData = ms.ToArray();

                var result = new PhotoUploadResult
                {
                    PhotoId = photoId,
                    Url = $"/api/photos/{photoId}",
                    ThumbnailUrl = $"/api/photos/{photoId}/thumbnail",
                    FileSize = photoData.Length,
                    UploadedAt = DateTime.UtcNow
                };

                // Store offline
                if (_offlineManager != null)
                {
                    await _offlineManager.SaveAsync(
                        "Photo",
                        photoId,
                        new { PhotoId = photoId, Data = Convert.ToBase64String(photoData), request },
                        userId,
                        request.ProjectId,
                        ct);
                }

                _logger?.LogInformation("Photo {PhotoId} uploaded by {UserId}", photoId, userId);
                return APIResponse<PhotoUploadResult>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload photo");
                return APIResponse<PhotoUploadResult>.Fail("UPLOAD_ERROR", "Failed to upload photo");
            }
        }

        #endregion

        #region Sync

        /// <summary>
        /// Trigger manual sync
        /// </summary>
        public async Task<APIResponse<SyncResult>> SyncAsync(
            string userId,
            CancellationToken ct = default)
        {
            try
            {
                if (_offlineManager == null)
                {
                    return APIResponse<SyncResult>.Fail("SYNC_UNAVAILABLE", "Offline sync not enabled");
                }

                var result = await _offlineManager.SyncAsync(ct);
                return APIResponse<SyncResult>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Sync failed");
                return APIResponse<SyncResult>.Fail("SYNC_ERROR", "Sync failed");
            }
        }

        /// <summary>
        /// Make project available offline
        /// </summary>
        public async Task<APIResponse<OfflineProjectResult>> MakeProjectOfflineAsync(
            string userId,
            string projectId,
            OfflineProjectOptions options,
            CancellationToken ct = default)
        {
            try
            {
                if (_offlineManager == null)
                {
                    return APIResponse<OfflineProjectResult>.Fail("OFFLINE_UNAVAILABLE", "Offline not enabled");
                }

                // Check permission
                if (_rbac != null)
                {
                    var access = _rbac.CheckAccess(userId, Actions.ViewDocument, ResourceType.Project, projectId);
                    if (!access.IsAllowed)
                    {
                        return APIResponse<OfflineProjectResult>.Fail("FORBIDDEN", "Access denied");
                    }
                }

                // Download project data for offline use
                var entities = new List<(string, string, object)>();

                // Would query and download project data here
                var downloadedCount = await _offlineManager.MakeAvailableOfflineAsync(
                    projectId, entities, ct);

                var result = new OfflineProjectResult
                {
                    ProjectId = projectId,
                    DownloadedItems = downloadedCount,
                    TotalSize = 0, // Would calculate
                    AvailableAt = DateTime.UtcNow
                };

                return APIResponse<OfflineProjectResult>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to make project offline");
                return APIResponse<OfflineProjectResult>.Fail("OFFLINE_ERROR", "Failed to download for offline");
            }
        }

        #endregion

        #region Push Notifications

        /// <summary>
        /// Register device for push notifications
        /// </summary>
        public async Task<APIResponse<bool>> RegisterPushTokenAsync(
            string userId,
            PushRegistration registration,
            CancellationToken ct = default)
        {
            try
            {
                // Store push token for user
                // Would integrate with FCM, APNS, etc.

                _logger?.LogInformation(
                    "Push token registered for user {UserId} on {Platform}",
                    userId, registration.Platform);

                return APIResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to register push token");
                return APIResponse<bool>.Fail("PUSH_ERROR", "Failed to register for notifications");
            }
        }

        #endregion

        #region Helpers

        private string GenerateSessionToken()
        {
            var bytes = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _cache.Clear();
            _userSessions.Clear();
            _logger?.LogInformation("MobileAPIService disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Additional DTOs

    public class AuthenticationRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
    }

    public class AuthenticationResult
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public MobileUserDTO User { get; set; } = new();
    }

    public class MobileUserDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public class CreateMobileIssueRequest
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IssueType { get; set; }
        public string? Priority { get; set; }
        public string? AssignedTo { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<string>? PhotoIds { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class PhotoUploadResult
    {
        public string PhotoId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class OfflineProjectOptions
    {
        public bool IncludeDocuments { get; set; } = true;
        public bool IncludePhotos { get; set; } = true;
        public bool IncludeModels { get; set; } = false;
        public int MaxPhotos { get; set; } = 100;
        public int DocumentMaxSizeMB { get; set; } = 50;
    }

    public class OfflineProjectResult
    {
        public string ProjectId { get; set; } = string.Empty;
        public int DownloadedItems { get; set; }
        public long TotalSize { get; set; }
        public DateTime AvailableAt { get; set; }
    }

    public class PushRegistration
    {
        public string Token { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // ios, android, web
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public int ChangesPushed { get; set; }
        public int ChangesPulled { get; set; }
        public int ConflictsResolved { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    #endregion
}
