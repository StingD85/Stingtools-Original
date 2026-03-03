// ===================================================================
// StingBIM.AI.Collaboration - Role-Based Access Control (RBAC) System
// Enterprise-grade security with fine-grained permissions
// Supports project-level, document-level, and model-level access
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Security
{
    #region Enums and Constants

    /// <summary>
    /// Standard permission levels
    /// </summary>
    public enum PermissionLevel
    {
        None = 0,
        View = 1,
        Comment = 2,
        Edit = 3,
        Create = 4,
        Delete = 5,
        Manage = 6,
        Admin = 7
    }

    /// <summary>
    /// Resource types that can be secured
    /// </summary>
    public enum ResourceType
    {
        Project,
        Folder,
        Document,
        Model,
        Issue,
        RFI,
        Submittal,
        Inspection,
        ClashTest,
        Report,
        User,
        Role,
        Team,
        Workflow,
        Setting
    }

    /// <summary>
    /// Built-in role templates
    /// </summary>
    public static class BuiltInRoles
    {
        public const string ProjectAdmin = "Project Administrator";
        public const string ProjectManager = "Project Manager";
        public const string BIMManager = "BIM Manager";
        public const string BIMCoordinator = "BIM Coordinator";
        public const string Designer = "Designer";
        public const string Engineer = "Engineer";
        public const string Contractor = "Contractor";
        public const string Subcontractor = "Subcontractor";
        public const string Client = "Client";
        public const string Viewer = "Viewer";
        public const string ExternalReviewer = "External Reviewer";
    }

    /// <summary>
    /// Permission actions
    /// </summary>
    public static class Actions
    {
        // Document actions
        public const string ViewDocument = "document:view";
        public const string DownloadDocument = "document:download";
        public const string UploadDocument = "document:upload";
        public const string EditDocument = "document:edit";
        public const string DeleteDocument = "document:delete";
        public const string ApproveDocument = "document:approve";
        public const string ShareDocument = "document:share";
        public const string ManageVersions = "document:versions";

        // Model actions
        public const string ViewModel = "model:view";
        public const string DownloadModel = "model:download";
        public const string UploadModel = "model:upload";
        public const string EditModel = "model:edit";
        public const string DeleteModel = "model:delete";
        public const string RunClashDetection = "model:clash";
        public const string Federate = "model:federate";

        // Issue actions
        public const string ViewIssue = "issue:view";
        public const string CreateIssue = "issue:create";
        public const string EditIssue = "issue:edit";
        public const string AssignIssue = "issue:assign";
        public const string ResolveIssue = "issue:resolve";
        public const string CloseIssue = "issue:close";
        public const string DeleteIssue = "issue:delete";
        public const string CommentIssue = "issue:comment";

        // RFI actions
        public const string ViewRFI = "rfi:view";
        public const string CreateRFI = "rfi:create";
        public const string RespondRFI = "rfi:respond";
        public const string CloseRFI = "rfi:close";

        // Submittal actions
        public const string ViewSubmittal = "submittal:view";
        public const string CreateSubmittal = "submittal:create";
        public const string ReviewSubmittal = "submittal:review";
        public const string ApproveSubmittal = "submittal:approve";

        // Field actions
        public const string ViewInspection = "inspection:view";
        public const string PerformInspection = "inspection:perform";
        public const string ApproveInspection = "inspection:approve";
        public const string UploadPhoto = "field:photo";
        public const string CreateDailyLog = "field:dailylog";

        // Admin actions
        public const string ManageUsers = "admin:users";
        public const string ManageRoles = "admin:roles";
        public const string ManageTeams = "admin:teams";
        public const string ManageSettings = "admin:settings";
        public const string ViewAuditLog = "admin:audit";
        public const string ManageWorkflows = "admin:workflows";
    }

    #endregion

    #region Data Models

    /// <summary>
    /// User identity
    /// </summary>
    public class UserIdentity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsSystemAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public string TenantId { get; set; } = "default";
        public Dictionary<string, string> Claims { get; set; } = new();
        public List<string> GroupIds { get; set; } = new();
    }

    /// <summary>
    /// Role definition
    /// </summary>
    public class Role
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsBuiltIn { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public string TenantId { get; set; } = "default";
        public List<Permission> Permissions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Permission definition
    /// </summary>
    public class Permission
    {
        public string Action { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public PermissionLevel Level { get; set; } = PermissionLevel.View;
        public List<string> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Role assignment (user to role in context)
    /// </summary>
    public class RoleAssignment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? FolderId { get; set; }
        public string? ResourceId { get; set; }
        public ResourceType? ResourceType { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public string AssignedBy { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Team/group definition
    /// </summary>
    public class Team
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TenantId { get; set; } = "default";
        public List<string> MemberIds { get; set; } = new();
        public List<string> RoleIds { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Access check result
    /// </summary>
    public class AccessCheckResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> MatchedRoles { get; set; } = new();
        public List<string> MatchedPermissions { get; set; } = new();
        public PermissionLevel EffectiveLevel { get; set; } = PermissionLevel.None;
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Audit log entry
    /// </summary>
    public class AuditLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Details { get; set; } = string.Empty;
        public string ChangeDataJson { get; set; } = "{}";
    }

    #endregion

    #region RBAC System

    /// <summary>
    /// Main RBAC system for access control
    /// </summary>
    public class RBACSystem : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, UserIdentity> _users = new();
        private readonly ConcurrentDictionary<string, Role> _roles = new();
        private readonly ConcurrentDictionary<string, RoleAssignment> _assignments = new();
        private readonly ConcurrentDictionary<string, Team> _teams = new();
        private readonly ConcurrentDictionary<string, List<AuditLogEntry>> _auditLogs = new();
        private readonly ConcurrentDictionary<string, AccessCheckResult> _accessCache = new();
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        public RBACSystem(ILogger? logger = null)
        {
            _logger = logger;
            InitializeBuiltInRoles();
            _logger?.LogInformation("RBACSystem initialized with {Roles} built-in roles",
                _roles.Count);
        }

        #region Initialization

        private void InitializeBuiltInRoles()
        {
            // Project Administrator - full access
            CreateBuiltInRole(BuiltInRoles.ProjectAdmin, "Full administrative access to project",
                CreateAllPermissions());

            // Project Manager - manage team and workflows
            CreateBuiltInRole(BuiltInRoles.ProjectManager, "Project management access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.EditDocument, ResourceType.Document, PermissionLevel.Edit),
                    CreatePermission(Actions.ApproveDocument, ResourceType.Document, PermissionLevel.Manage),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.UploadModel, ResourceType.Model, PermissionLevel.Create),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.AssignIssue, ResourceType.Issue, PermissionLevel.Manage),
                    CreatePermission(Actions.CloseIssue, ResourceType.Issue, PermissionLevel.Manage),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.CreateRFI, ResourceType.RFI, PermissionLevel.Create),
                    CreatePermission(Actions.RespondRFI, ResourceType.RFI, PermissionLevel.Edit),
                    CreatePermission(Actions.ViewSubmittal, ResourceType.Submittal, PermissionLevel.View),
                    CreatePermission(Actions.ApproveSubmittal, ResourceType.Submittal, PermissionLevel.Manage),
                    CreatePermission(Actions.ManageUsers, ResourceType.User, PermissionLevel.Manage),
                    CreatePermission(Actions.ManageTeams, ResourceType.Team, PermissionLevel.Manage),
                });

            // BIM Manager - model coordination focus
            CreateBuiltInRole(BuiltInRoles.BIMManager, "BIM management and coordination",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.UploadModel, ResourceType.Model, PermissionLevel.Create),
                    CreatePermission(Actions.EditModel, ResourceType.Model, PermissionLevel.Edit),
                    CreatePermission(Actions.DeleteModel, ResourceType.Model, PermissionLevel.Delete),
                    CreatePermission(Actions.RunClashDetection, ResourceType.Model, PermissionLevel.Manage),
                    CreatePermission(Actions.Federate, ResourceType.Model, PermissionLevel.Manage),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.EditIssue, ResourceType.Issue, PermissionLevel.Edit),
                    CreatePermission(Actions.AssignIssue, ResourceType.Issue, PermissionLevel.Manage),
                });

            // BIM Coordinator - coordination tasks
            CreateBuiltInRole(BuiltInRoles.BIMCoordinator, "BIM coordination access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.UploadModel, ResourceType.Model, PermissionLevel.Create),
                    CreatePermission(Actions.RunClashDetection, ResourceType.Model, PermissionLevel.Edit),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ResolveIssue, ResourceType.Issue, PermissionLevel.Edit),
                });

            // Designer - design document access
            CreateBuiltInRole(BuiltInRoles.Designer, "Design team access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.EditDocument, ResourceType.Document, PermissionLevel.Edit),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.UploadModel, ResourceType.Model, PermissionLevel.Create),
                    CreatePermission(Actions.EditModel, ResourceType.Model, PermissionLevel.Edit),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.RespondRFI, ResourceType.RFI, PermissionLevel.Edit),
                });

            // Engineer - engineering focus
            CreateBuiltInRole(BuiltInRoles.Engineer, "Engineering team access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.EditDocument, ResourceType.Document, PermissionLevel.Edit),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.UploadModel, ResourceType.Model, PermissionLevel.Create),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.RespondRFI, ResourceType.RFI, PermissionLevel.Edit),
                });

            // Contractor - construction access
            CreateBuiltInRole(BuiltInRoles.Contractor, "Contractor access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.DownloadDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.UploadDocument, ResourceType.Document, PermissionLevel.Create),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.DownloadModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.CreateRFI, ResourceType.RFI, PermissionLevel.Create),
                    CreatePermission(Actions.ViewSubmittal, ResourceType.Submittal, PermissionLevel.View),
                    CreatePermission(Actions.CreateSubmittal, ResourceType.Submittal, PermissionLevel.Create),
                    CreatePermission(Actions.ViewInspection, ResourceType.Inspection, PermissionLevel.View),
                    CreatePermission(Actions.PerformInspection, ResourceType.Inspection, PermissionLevel.Edit),
                    CreatePermission(Actions.UploadPhoto, ResourceType.Inspection, PermissionLevel.Create),
                    CreatePermission(Actions.CreateDailyLog, ResourceType.Inspection, PermissionLevel.Create),
                });

            // Subcontractor - limited access
            CreateBuiltInRole(BuiltInRoles.Subcontractor, "Subcontractor limited access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.DownloadDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.ViewSubmittal, ResourceType.Submittal, PermissionLevel.View),
                    CreatePermission(Actions.UploadPhoto, ResourceType.Inspection, PermissionLevel.Create),
                });

            // Client - view-only with specific approvals
            CreateBuiltInRole(BuiltInRoles.Client, "Client view and approval access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.DownloadDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.ApproveDocument, ResourceType.Document, PermissionLevel.Manage),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.ViewSubmittal, ResourceType.Submittal, PermissionLevel.View),
                    CreatePermission(Actions.ApproveSubmittal, ResourceType.Submittal, PermissionLevel.Manage),
                });

            // Viewer - read-only
            CreateBuiltInRole(BuiltInRoles.Viewer, "Read-only access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.ViewRFI, ResourceType.RFI, PermissionLevel.View),
                    CreatePermission(Actions.ViewSubmittal, ResourceType.Submittal, PermissionLevel.View),
                    CreatePermission(Actions.ViewInspection, ResourceType.Inspection, PermissionLevel.View),
                });

            // External Reviewer - comment only
            CreateBuiltInRole(BuiltInRoles.ExternalReviewer, "External review access",
                new List<Permission>
                {
                    CreatePermission(Actions.ViewDocument, ResourceType.Document, PermissionLevel.View),
                    CreatePermission(Actions.ViewModel, ResourceType.Model, PermissionLevel.View),
                    CreatePermission(Actions.ViewIssue, ResourceType.Issue, PermissionLevel.View),
                    CreatePermission(Actions.CreateIssue, ResourceType.Issue, PermissionLevel.Create),
                    CreatePermission(Actions.CommentIssue, ResourceType.Issue, PermissionLevel.Comment),
                });
        }

        private void CreateBuiltInRole(string name, string description, List<Permission> permissions)
        {
            var role = new Role
            {
                Id = GenerateRoleId(name),
                Name = name,
                Description = description,
                IsBuiltIn = true,
                Permissions = permissions,
                CreatedBy = "system"
            };
            _roles[role.Id] = role;
        }

        private List<Permission> CreateAllPermissions()
        {
            var permissions = new List<Permission>();

            // Add all actions for all resource types
            foreach (var field in typeof(Actions).GetFields())
            {
                var action = field.GetValue(null)?.ToString() ?? "";
                permissions.Add(CreatePermission(action, ResourceType.Project, PermissionLevel.Admin));
            }

            return permissions;
        }

        private Permission CreatePermission(string action, ResourceType resourceType, PermissionLevel level)
        {
            return new Permission
            {
                Action = action,
                ResourceType = resourceType,
                Level = level
            };
        }

        private string GenerateRoleId(string name)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(name.ToLowerInvariant()));
            return $"role_{BitConverter.ToString(hash).Replace("-", "").Substring(0, 12).ToLowerInvariant()}";
        }

        #endregion

        #region User Management

        /// <summary>
        /// Register a new user
        /// </summary>
        public UserIdentity RegisterUser(string email, string displayName, string company = "")
        {
            var user = new UserIdentity
            {
                Email = email.ToLowerInvariant(),
                DisplayName = displayName,
                Company = company
            };

            if (!_users.TryAdd(user.Id, user))
            {
                throw new InvalidOperationException($"User with ID {user.Id} already exists");
            }

            _logger?.LogInformation("Registered user {Email} with ID {Id}", email, user.Id);
            return user;
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        public UserIdentity? GetUser(string userId)
        {
            _users.TryGetValue(userId, out var user);
            return user;
        }

        /// <summary>
        /// Get user by email
        /// </summary>
        public UserIdentity? GetUserByEmail(string email)
        {
            return _users.Values.FirstOrDefault(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Update user
        /// </summary>
        public UserIdentity UpdateUser(UserIdentity user)
        {
            if (!_users.ContainsKey(user.Id))
            {
                throw new KeyNotFoundException($"User {user.Id} not found");
            }

            _users[user.Id] = user;
            InvalidateUserCache(user.Id);

            _logger?.LogInformation("Updated user {Id}", user.Id);
            return user;
        }

        /// <summary>
        /// Deactivate user
        /// </summary>
        public void DeactivateUser(string userId)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                user.IsActive = false;
                InvalidateUserCache(userId);
                _logger?.LogInformation("Deactivated user {Id}", userId);
            }
        }

        #endregion

        #region Role Management

        /// <summary>
        /// Create a custom role
        /// </summary>
        public Role CreateRole(string name, string description, List<Permission> permissions, string createdBy)
        {
            var role = new Role
            {
                Name = name,
                Description = description,
                Permissions = permissions,
                CreatedBy = createdBy,
                IsBuiltIn = false
            };

            if (!_roles.TryAdd(role.Id, role))
            {
                throw new InvalidOperationException($"Role with ID {role.Id} already exists");
            }

            _logger?.LogInformation("Created role {Name} with {Permissions} permissions",
                name, permissions.Count);
            return role;
        }

        /// <summary>
        /// Get role by ID
        /// </summary>
        public Role? GetRole(string roleId)
        {
            _roles.TryGetValue(roleId, out var role);
            return role;
        }

        /// <summary>
        /// Get role by name
        /// </summary>
        public Role? GetRoleByName(string name)
        {
            return _roles.Values.FirstOrDefault(r =>
                r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all roles
        /// </summary>
        public List<Role> GetAllRoles()
        {
            return _roles.Values.Where(r => r.IsActive).ToList();
        }

        /// <summary>
        /// Update role
        /// </summary>
        public Role UpdateRole(Role role)
        {
            if (!_roles.ContainsKey(role.Id))
            {
                throw new KeyNotFoundException($"Role {role.Id} not found");
            }

            if (role.IsBuiltIn)
            {
                throw new InvalidOperationException("Cannot modify built-in roles");
            }

            _roles[role.Id] = role;
            ClearCache();

            _logger?.LogInformation("Updated role {Id}", role.Id);
            return role;
        }

        /// <summary>
        /// Delete role (soft delete)
        /// </summary>
        public void DeleteRole(string roleId)
        {
            if (_roles.TryGetValue(roleId, out var role))
            {
                if (role.IsBuiltIn)
                {
                    throw new InvalidOperationException("Cannot delete built-in roles");
                }

                role.IsActive = false;
                ClearCache();
                _logger?.LogInformation("Deleted role {Id}", roleId);
            }
        }

        #endregion

        #region Role Assignment

        /// <summary>
        /// Assign role to user
        /// </summary>
        public RoleAssignment AssignRole(
            string userId,
            string roleId,
            string assignedBy,
            string? projectId = null,
            string? resourceId = null,
            ResourceType? resourceType = null,
            DateTime? expiresAt = null)
        {
            if (!_users.ContainsKey(userId))
            {
                throw new KeyNotFoundException($"User {userId} not found");
            }

            if (!_roles.ContainsKey(roleId))
            {
                throw new KeyNotFoundException($"Role {roleId} not found");
            }

            var assignment = new RoleAssignment
            {
                UserId = userId,
                RoleId = roleId,
                ProjectId = projectId,
                ResourceId = resourceId,
                ResourceType = resourceType,
                AssignedBy = assignedBy,
                ExpiresAt = expiresAt
            };

            _assignments[assignment.Id] = assignment;
            InvalidateUserCache(userId);

            _logger?.LogInformation(
                "Assigned role {RoleId} to user {UserId} for project {ProjectId}",
                roleId, userId, projectId ?? "all");

            return assignment;
        }

        /// <summary>
        /// Revoke role from user
        /// </summary>
        public void RevokeRole(string assignmentId)
        {
            if (_assignments.TryGetValue(assignmentId, out var assignment))
            {
                assignment.IsActive = false;
                InvalidateUserCache(assignment.UserId);

                _logger?.LogInformation("Revoked assignment {Id}", assignmentId);
            }
        }

        /// <summary>
        /// Get user's role assignments
        /// </summary>
        public List<RoleAssignment> GetUserAssignments(string userId, string? projectId = null)
        {
            return _assignments.Values
                .Where(a => a.UserId == userId &&
                           a.IsActive &&
                           (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) &&
                           (projectId == null || a.ProjectId == null || a.ProjectId == projectId))
                .ToList();
        }

        /// <summary>
        /// Get users with specific role
        /// </summary>
        public List<UserIdentity> GetUsersWithRole(string roleId, string? projectId = null)
        {
            var userIds = _assignments.Values
                .Where(a => a.RoleId == roleId &&
                           a.IsActive &&
                           (projectId == null || a.ProjectId == null || a.ProjectId == projectId))
                .Select(a => a.UserId)
                .Distinct();

            return _users.Values
                .Where(u => userIds.Contains(u.Id) && u.IsActive)
                .ToList();
        }

        #endregion

        #region Access Control

        /// <summary>
        /// Check if user has permission
        /// </summary>
        public AccessCheckResult CheckAccess(
            string userId,
            string action,
            ResourceType resourceType,
            string? projectId = null,
            string? resourceId = null)
        {
            var cacheKey = $"{userId}:{action}:{resourceType}:{projectId}:{resourceId}";

            if (_accessCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var result = PerformAccessCheck(userId, action, resourceType, projectId, resourceId);

            _accessCache.TryAdd(cacheKey, result);

            return result;
        }

        private AccessCheckResult PerformAccessCheck(
            string userId,
            string action,
            ResourceType resourceType,
            string? projectId,
            string? resourceId)
        {
            var result = new AccessCheckResult();

            // Check if user exists and is active
            if (!_users.TryGetValue(userId, out var user) || !user.IsActive)
            {
                result.Reason = "User not found or inactive";
                return result;
            }

            // System admin bypass
            if (user.IsSystemAdmin)
            {
                result.IsAllowed = true;
                result.Reason = "System administrator";
                result.EffectiveLevel = PermissionLevel.Admin;
                return result;
            }

            // Get user's role assignments (including team memberships)
            var assignments = GetEffectiveAssignments(userId, projectId, resourceId, resourceType);

            if (!assignments.Any())
            {
                result.Reason = "No role assignments found";
                return result;
            }

            // Check each assignment for permission
            foreach (var assignment in assignments)
            {
                if (!_roles.TryGetValue(assignment.RoleId, out var role) || !role.IsActive)
                    continue;

                var matchingPermission = role.Permissions
                    .FirstOrDefault(p => p.Action == action ||
                                        p.Action == "*" ||
                                        MatchWildcard(p.Action, action));

                if (matchingPermission != null)
                {
                    result.MatchedRoles.Add(role.Name);
                    result.MatchedPermissions.Add(matchingPermission.Action);

                    if (matchingPermission.Level > result.EffectiveLevel)
                    {
                        result.EffectiveLevel = matchingPermission.Level;
                    }
                }
            }

            result.IsAllowed = result.MatchedPermissions.Any();
            result.Reason = result.IsAllowed
                ? $"Access granted via {string.Join(", ", result.MatchedRoles)}"
                : "No matching permissions found";

            return result;
        }

        private List<RoleAssignment> GetEffectiveAssignments(
            string userId,
            string? projectId,
            string? resourceId,
            ResourceType? resourceType)
        {
            var assignments = new List<RoleAssignment>();

            // Direct user assignments
            assignments.AddRange(_assignments.Values.Where(a =>
                a.UserId == userId &&
                a.IsActive &&
                (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow) &&
                MatchesContext(a, projectId, resourceId, resourceType)));

            // Team-based assignments
            if (_users.TryGetValue(userId, out var user))
            {
                foreach (var groupId in user.GroupIds)
                {
                    if (_teams.TryGetValue(groupId, out var team))
                    {
                        foreach (var roleId in team.RoleIds)
                        {
                            assignments.Add(new RoleAssignment
                            {
                                UserId = userId,
                                RoleId = roleId,
                                ProjectId = projectId,
                                IsActive = true
                            });
                        }
                    }
                }
            }

            return assignments;
        }

        private bool MatchesContext(
            RoleAssignment assignment,
            string? projectId,
            string? resourceId,
            ResourceType? resourceType)
        {
            // Global assignment matches all
            if (assignment.ProjectId == null && assignment.ResourceId == null)
                return true;

            // Project-level assignment
            if (assignment.ProjectId != null && assignment.ProjectId == projectId &&
                assignment.ResourceId == null)
                return true;

            // Resource-level assignment
            if (assignment.ResourceId != null && assignment.ResourceId == resourceId &&
                assignment.ResourceType == resourceType)
                return true;

            return false;
        }

        private bool MatchWildcard(string pattern, string value)
        {
            // Simple prefix matching (e.g., "document:*" matches "document:view")
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Check access and throw if denied
        /// </summary>
        public void RequireAccess(
            string userId,
            string action,
            ResourceType resourceType,
            string? projectId = null,
            string? resourceId = null)
        {
            var result = CheckAccess(userId, action, resourceType, projectId, resourceId);

            if (!result.IsAllowed)
            {
                LogAudit(userId, action, resourceType, resourceId ?? "", projectId ?? "",
                    false, $"Access denied: {result.Reason}");

                throw new UnauthorizedAccessException(
                    $"Access denied: {action} on {resourceType}. {result.Reason}");
            }

            LogAudit(userId, action, resourceType, resourceId ?? "", projectId ?? "",
                true, "Access granted");
        }

        /// <summary>
        /// Get all permissions for a user in a project
        /// </summary>
        public Dictionary<string, PermissionLevel> GetUserPermissions(string userId, string? projectId = null)
        {
            var permissions = new Dictionary<string, PermissionLevel>();

            var assignments = GetEffectiveAssignments(userId, projectId, null, null);

            foreach (var assignment in assignments)
            {
                if (!_roles.TryGetValue(assignment.RoleId, out var role) || !role.IsActive)
                    continue;

                foreach (var permission in role.Permissions)
                {
                    if (!permissions.ContainsKey(permission.Action) ||
                        permissions[permission.Action] < permission.Level)
                    {
                        permissions[permission.Action] = permission.Level;
                    }
                }
            }

            return permissions;
        }

        #endregion

        #region Team Management

        /// <summary>
        /// Create a team
        /// </summary>
        public Team CreateTeam(string name, string description, string createdBy)
        {
            var team = new Team
            {
                Name = name,
                Description = description,
                CreatedBy = createdBy
            };

            _teams[team.Id] = team;
            _logger?.LogInformation("Created team {Name}", name);

            return team;
        }

        /// <summary>
        /// Add member to team
        /// </summary>
        public void AddTeamMember(string teamId, string userId)
        {
            if (!_teams.TryGetValue(teamId, out var team))
            {
                throw new KeyNotFoundException($"Team {teamId} not found");
            }

            if (!team.MemberIds.Contains(userId))
            {
                team.MemberIds.Add(userId);

                if (_users.TryGetValue(userId, out var user) && !user.GroupIds.Contains(teamId))
                {
                    user.GroupIds.Add(teamId);
                }

                InvalidateUserCache(userId);
                _logger?.LogInformation("Added user {UserId} to team {TeamId}", userId, teamId);
            }
        }

        /// <summary>
        /// Remove member from team
        /// </summary>
        public void RemoveTeamMember(string teamId, string userId)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                team.MemberIds.Remove(userId);

                if (_users.TryGetValue(userId, out var user))
                {
                    user.GroupIds.Remove(teamId);
                }

                InvalidateUserCache(userId);
                _logger?.LogInformation("Removed user {UserId} from team {TeamId}", userId, teamId);
            }
        }

        /// <summary>
        /// Assign role to team
        /// </summary>
        public void AssignRoleToTeam(string teamId, string roleId)
        {
            if (!_teams.TryGetValue(teamId, out var team))
            {
                throw new KeyNotFoundException($"Team {teamId} not found");
            }

            if (!team.RoleIds.Contains(roleId))
            {
                team.RoleIds.Add(roleId);

                // Invalidate cache for all team members
                foreach (var memberId in team.MemberIds)
                {
                    InvalidateUserCache(memberId);
                }

                _logger?.LogInformation("Assigned role {RoleId} to team {TeamId}", roleId, teamId);
            }
        }

        #endregion

        #region Audit Logging

        /// <summary>
        /// Log an audit entry
        /// </summary>
        public void LogAudit(
            string userId,
            string action,
            ResourceType resourceType,
            string resourceId,
            string projectId,
            bool success,
            string details = "",
            string? ipAddress = null,
            string? userAgent = null,
            object? changeData = null)
        {
            var entry = new AuditLogEntry
            {
                UserId = userId,
                UserEmail = _users.TryGetValue(userId, out var user) ? user.Email : "",
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                ProjectId = projectId,
                Success = success,
                Details = details,
                IpAddress = ipAddress ?? "",
                UserAgent = userAgent ?? "",
                ChangeDataJson = changeData != null
                    ? JsonSerializer.Serialize(changeData)
                    : "{}"
            };

            var logKey = $"{projectId}:{DateTime.UtcNow:yyyyMMdd}";
            var logs = _auditLogs.GetOrAdd(logKey, _ => new List<AuditLogEntry>());

            lock (logs)
            {
                logs.Add(entry);
            }

            _logger?.LogDebug("Audit: {Action} on {ResourceType} by {UserId}: {Success}",
                action, resourceType, userId, success);
        }

        /// <summary>
        /// Get audit logs
        /// </summary>
        public List<AuditLogEntry> GetAuditLogs(
            string? projectId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? userId = null,
            string? action = null,
            int limit = 100)
        {
            var allLogs = _auditLogs.Values.SelectMany(l => l);

            if (projectId != null)
                allLogs = allLogs.Where(l => l.ProjectId == projectId);

            if (fromDate != null)
                allLogs = allLogs.Where(l => l.Timestamp >= fromDate);

            if (toDate != null)
                allLogs = allLogs.Where(l => l.Timestamp <= toDate);

            if (userId != null)
                allLogs = allLogs.Where(l => l.UserId == userId);

            if (action != null)
                allLogs = allLogs.Where(l => l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));

            return allLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToList();
        }

        #endregion

        #region Cache Management

        private void InvalidateUserCache(string userId)
        {
            var keysToRemove = _accessCache.Keys
                .Where(k => k.StartsWith($"{userId}:"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _accessCache.TryRemove(key, out _);
            }
        }

        private void ClearCache()
        {
            _accessCache.Clear();
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _cacheLock.Dispose();
            _logger?.LogInformation("RBACSystem disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Security Helpers

    /// <summary>
    /// Security context for current request
    /// </summary>
    public class SecurityContext
    {
        public UserIdentity? CurrentUser { get; set; }
        public string? ProjectId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        private readonly RBACSystem _rbac;

        public SecurityContext(RBACSystem rbac, UserIdentity? user = null)
        {
            _rbac = rbac;
            CurrentUser = user;
        }

        /// <summary>
        /// Check permission in current context
        /// </summary>
        public bool CanPerform(string action, ResourceType resourceType, string? resourceId = null)
        {
            if (CurrentUser == null) return false;

            var result = _rbac.CheckAccess(
                CurrentUser.Id,
                action,
                resourceType,
                ProjectId,
                resourceId);

            return result.IsAllowed;
        }

        /// <summary>
        /// Require permission or throw
        /// </summary>
        public void Require(string action, ResourceType resourceType, string? resourceId = null)
        {
            if (CurrentUser == null)
            {
                throw new UnauthorizedAccessException("Authentication required");
            }

            _rbac.RequireAccess(CurrentUser.Id, action, resourceType, ProjectId, resourceId);
        }
    }

    #endregion
}
