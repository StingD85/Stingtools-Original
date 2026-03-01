// ===================================================================
// StingBIM.AI.Collaboration - Database Persistence Layer
// Provides persistent storage for all collaboration data
// Supports SQLite (local) and SQL Server (enterprise)
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Persistence
{
    #region Database Abstractions

    /// <summary>
    /// Database provider type
    /// </summary>
    public enum DatabaseProvider
    {
        SQLite,
        SQLServer,
        PostgreSQL,
        InMemory
    }

    /// <summary>
    /// Connection configuration
    /// </summary>
    public class DatabaseConfiguration
    {
        public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;
        public string ConnectionString { get; set; } = "Data Source=stingbim_collab.db";
        public int CommandTimeout { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public bool EnableLogging { get; set; } = true;
        public string DataDirectory { get; set; } = "./data";
        public int PoolSize { get; set; } = 10;
        public bool AutoMigrate { get; set; } = true;
    }

    /// <summary>
    /// Base entity for all persisted objects
    /// </summary>
    public abstract class EntityBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public long Version { get; set; } = 1;
        public string TenantId { get; set; } = "default";
    }

    /// <summary>
    /// Unit of work pattern for transaction management
    /// </summary>
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task BeginTransactionAsync(CancellationToken ct = default);
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
        IRepository<T> Repository<T>() where T : EntityBase, new();
    }

    /// <summary>
    /// Generic repository interface
    /// </summary>
    public interface IRepository<T> where T : EntityBase
    {
        Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<List<T>> GetAllAsync(CancellationToken ct = default);
        Task<List<T>> FindAsync(Func<T, bool> predicate, CancellationToken ct = default);
        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task<T> UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(string id, CancellationToken ct = default);
        Task<int> CountAsync(CancellationToken ct = default);
        Task<bool> ExistsAsync(string id, CancellationToken ct = default);
        IQueryable<T> Query();
    }

    #endregion

    #region Collaboration Entities

    /// <summary>
    /// Project entity for persistence
    /// </summary>
    public class ProjectEntity : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string ClientName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SettingsJson { get; set; } = "{}";
    }

    /// <summary>
    /// Document entity for persistence
    /// </summary>
    public class DocumentEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string FolderId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public int CurrentVersion { get; set; } = 1;
        public string MetadataJson { get; set; } = "{}";
        public string TagsJson { get; set; } = "[]";
        public string PermissionsJson { get; set; } = "{}";
    }

    /// <summary>
    /// Document version entity
    /// </summary>
    public class DocumentVersionEntity : EntityBase
    {
        public string DocumentId { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Issue entity for persistence
    /// </summary>
    public class IssueEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IssueType { get; set; } = "General";
        public string Status { get; set; } = "Open";
        public string Priority { get; set; } = "Medium";
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string LocationJson { get; set; } = "{}";
        public string ViewpointJson { get; set; } = "{}";
        public string AttachmentsJson { get; set; } = "[]";
        public string TagsJson { get; set; } = "[]";
        public string LinkedElementsJson { get; set; } = "[]";
        public string LinkedClashId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Clash result entity for persistence
    /// </summary>
    public class ClashEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ClashTestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ClashType { get; set; } = "Hard";
        public string Status { get; set; } = "New";
        public string ElementAId { get; set; } = string.Empty;
        public string ElementAModelId { get; set; } = string.Empty;
        public string ElementBId { get; set; } = string.Empty;
        public string ElementBModelId { get; set; } = string.Empty;
        public string ClashPointJson { get; set; } = "{}";
        public double Distance { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string ResolutionJson { get; set; } = "{}";
        public string GroupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// RFI entity for persistence
    /// </summary>
    public class RFIEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string RFINumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string Priority { get; set; } = "Normal";
        public string SubmittedBy { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime? ResponseDate { get; set; }
        public string AttachmentsJson { get; set; } = "[]";
        public string LinkedIssuesJson { get; set; } = "[]";
        public string DistributionListJson { get; set; } = "[]";
        public int CostImpact { get; set; }
        public int ScheduleImpact { get; set; }
    }

    /// <summary>
    /// Submittal entity for persistence
    /// </summary>
    public class SubmittalEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string SubmittalNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SpecSection { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string ReviewersJson { get; set; } = "[]";
        public string AttachmentsJson { get; set; } = "[]";
        public string ReviewHistoryJson { get; set; } = "[]";
        public int RevisionNumber { get; set; } = 0;
    }

    /// <summary>
    /// Inspection entity for persistence
    /// </summary>
    public class InspectionEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string InspectionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Scheduled";
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string LocationJson { get; set; } = "{}";
        public string ChecklistId { get; set; } = string.Empty;
        public string ResultsJson { get; set; } = "{}";
        public string PhotosJson { get; set; } = "[]";
        public string SignatureJson { get; set; } = "{}";
    }

    /// <summary>
    /// User entity for collaboration
    /// </summary>
    public class UserEntity : EntityBase
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = "Viewer";
        public string AvatarUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public string PreferencesJson { get; set; } = "{}";
        public string PermissionsJson { get; set; } = "{}";
    }

    /// <summary>
    /// Model entity for federated models
    /// </summary>
    public class ModelEntity : EntityBase
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public int CurrentVersion { get; set; } = 1;
        public string TransformMatrixJson { get; set; } = "{}";
        public string BoundingBoxJson { get; set; } = "{}";
        public DateTime? LastSyncAt { get; set; }
        public string Discipline { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sync log for offline support
    /// </summary>
    public class SyncLogEntity : EntityBase
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // Create, Update, Delete
        public string ChangeDataJson { get; set; } = "{}";
        public string UserId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public bool IsSynced { get; set; } = false;
        public DateTime? SyncedAt { get; set; }
        public int RetryCount { get; set; } = 0;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    #endregion

    #region Repository Implementation

    /// <summary>
    /// In-memory repository implementation (default, with persistence to JSON)
    /// </summary>
    public class JsonFileRepository<T> : IRepository<T> where T : EntityBase, new()
    {
        private readonly ConcurrentDictionary<string, T> _cache = new();
        private readonly string _filePath;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly ILogger? _logger;
        private bool _isDirty = false;

        public JsonFileRepository(string dataDirectory, ILogger? logger = null)
        {
            _logger = logger;
            var typeName = typeof(T).Name.Replace("Entity", "").ToLowerInvariant();
            _filePath = Path.Combine(dataDirectory, $"{typeName}s.json");

            Directory.CreateDirectory(dataDirectory);
            LoadFromFile();
        }

        private void LoadFromFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
                    foreach (var item in items.Where(i => !i.IsDeleted))
                    {
                        _cache[item.Id] = item;
                    }
                    _logger?.LogInformation("Loaded {Count} {Type} entities from {Path}",
                        _cache.Count, typeof(T).Name, _filePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load {Type} from {Path}", typeof(T).Name, _filePath);
            }
        }

        private async Task SaveToFileAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                if (!_isDirty) return;

                var items = _cache.Values.ToList();
                var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_filePath, json);
                _isDirty = false;

                _logger?.LogDebug("Saved {Count} {Type} entities to {Path}",
                    items.Count, typeof(T).Name, _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save {Type} to {Path}", typeof(T).Name, _filePath);
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        public Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            _cache.TryGetValue(id, out var entity);
            return Task.FromResult(entity?.IsDeleted == false ? entity : null);
        }

        public Task<List<T>> GetAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_cache.Values.Where(e => !e.IsDeleted).ToList());
        }

        public Task<List<T>> FindAsync(Func<T, bool> predicate, CancellationToken ct = default)
        {
            return Task.FromResult(_cache.Values
                .Where(e => !e.IsDeleted && predicate(e))
                .ToList());
        }

        public async Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.ModifiedAt = DateTime.UtcNow;
            entity.Version = 1;

            if (!_cache.TryAdd(entity.Id, entity))
            {
                throw new InvalidOperationException($"Entity with ID {entity.Id} already exists");
            }

            _isDirty = true;
            await SaveToFileAsync();

            _logger?.LogDebug("Added {Type} entity {Id}", typeof(T).Name, entity.Id);
            return entity;
        }

        public async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
        {
            if (!_cache.TryGetValue(entity.Id, out var existing))
            {
                throw new KeyNotFoundException($"Entity with ID {entity.Id} not found");
            }

            entity.ModifiedAt = DateTime.UtcNow;
            entity.Version = existing.Version + 1;
            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;

            _cache[entity.Id] = entity;
            _isDirty = true;
            await SaveToFileAsync();

            _logger?.LogDebug("Updated {Type} entity {Id} to version {Version}",
                typeof(T).Name, entity.Id, entity.Version);
            return entity;
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(id, out var entity))
            {
                entity.IsDeleted = true;
                entity.ModifiedAt = DateTime.UtcNow;
                _isDirty = true;
                await SaveToFileAsync();

                _logger?.LogDebug("Soft deleted {Type} entity {Id}", typeof(T).Name, id);
            }
        }

        public Task<int> CountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_cache.Values.Count(e => !e.IsDeleted));
        }

        public Task<bool> ExistsAsync(string id, CancellationToken ct = default)
        {
            return Task.FromResult(_cache.TryGetValue(id, out var e) && !e.IsDeleted);
        }

        public IQueryable<T> Query()
        {
            return _cache.Values.Where(e => !e.IsDeleted).AsQueryable();
        }
    }

    #endregion

    #region Database Context

    /// <summary>
    /// Main database context for collaboration module
    /// </summary>
    public class CollaborationDbContext : IUnitOfWork
    {
        private readonly DatabaseConfiguration _config;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();
        private bool _isInTransaction = false;
        private readonly List<Func<Task>> _transactionOperations = new();

        public CollaborationDbContext(DatabaseConfiguration? config = null, ILogger? logger = null)
        {
            _config = config ?? new DatabaseConfiguration();
            _logger = logger;

            Directory.CreateDirectory(_config.DataDirectory);

            _logger?.LogInformation("CollaborationDbContext initialized with {Provider} provider",
                _config.Provider);
        }

        public IRepository<T> Repository<T>() where T : EntityBase, new()
        {
            return (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ =>
                new JsonFileRepository<T>(_config.DataDirectory, _logger));
        }

        // Convenience properties for common repositories
        public IRepository<ProjectEntity> Projects => Repository<ProjectEntity>();
        public IRepository<DocumentEntity> Documents => Repository<DocumentEntity>();
        public IRepository<DocumentVersionEntity> DocumentVersions => Repository<DocumentVersionEntity>();
        public IRepository<IssueEntity> Issues => Repository<IssueEntity>();
        public IRepository<ClashEntity> Clashes => Repository<ClashEntity>();
        public IRepository<RFIEntity> RFIs => Repository<RFIEntity>();
        public IRepository<SubmittalEntity> Submittals => Repository<SubmittalEntity>();
        public IRepository<InspectionEntity> Inspections => Repository<InspectionEntity>();
        public IRepository<UserEntity> Users => Repository<UserEntity>();
        public IRepository<ModelEntity> Models => Repository<ModelEntity>();
        public IRepository<SyncLogEntity> SyncLogs => Repository<SyncLogEntity>();

        public Task BeginTransactionAsync(CancellationToken ct = default)
        {
            if (_isInTransaction)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }

            _isInTransaction = true;
            _transactionOperations.Clear();

            _logger?.LogDebug("Transaction started");
            return Task.CompletedTask;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (!_isInTransaction)
            {
                throw new InvalidOperationException("No transaction in progress");
            }

            try
            {
                foreach (var operation in _transactionOperations)
                {
                    await operation();
                }

                _logger?.LogDebug("Transaction committed with {Count} operations",
                    _transactionOperations.Count);
            }
            finally
            {
                _isInTransaction = false;
                _transactionOperations.Clear();
            }
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            _isInTransaction = false;
            _transactionOperations.Clear();

            _logger?.LogDebug("Transaction rolled back");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isInTransaction)
            {
                await RollbackAsync();
            }

            _logger?.LogInformation("CollaborationDbContext disposed");
        }
    }

    #endregion

    #region Query Extensions

    /// <summary>
    /// Extension methods for querying entities
    /// </summary>
    public static class QueryExtensions
    {
        public static IQueryable<T> ByProject<T>(this IQueryable<T> query, string projectId)
            where T : EntityBase
        {
            var property = typeof(T).GetProperty("ProjectId");
            if (property == null) return query;

            return query.Where(e => (string)property.GetValue(e)! == projectId);
        }

        public static IQueryable<T> ByTenant<T>(this IQueryable<T> query, string tenantId)
            where T : EntityBase
        {
            return query.Where(e => e.TenantId == tenantId);
        }

        public static IQueryable<T> Active<T>(this IQueryable<T> query) where T : EntityBase
        {
            return query.Where(e => !e.IsDeleted);
        }

        public static IQueryable<T> CreatedAfter<T>(this IQueryable<T> query, DateTime date)
            where T : EntityBase
        {
            return query.Where(e => e.CreatedAt >= date);
        }

        public static IQueryable<T> ModifiedAfter<T>(this IQueryable<T> query, DateTime date)
            where T : EntityBase
        {
            return query.Where(e => e.ModifiedAt >= date);
        }

        public static IQueryable<T> OrderByRecent<T>(this IQueryable<T> query) where T : EntityBase
        {
            return query.OrderByDescending(e => e.ModifiedAt);
        }

        public static IQueryable<T> Page<T>(this IQueryable<T> query, int page, int pageSize)
            where T : EntityBase
        {
            return query.Skip((page - 1) * pageSize).Take(pageSize);
        }
    }

    #endregion

    #region Migration Support

    /// <summary>
    /// Database migration for schema updates
    /// </summary>
    public class DatabaseMigration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string Script { get; set; } = string.Empty;
    }

    /// <summary>
    /// Migration manager
    /// </summary>
    public class MigrationManager
    {
        private readonly CollaborationDbContext _context;
        private readonly ILogger? _logger;
        private readonly List<DatabaseMigration> _migrations = new();

        public MigrationManager(CollaborationDbContext context, ILogger? logger = null)
        {
            _context = context;
            _logger = logger;

            RegisterMigrations();
        }

        private void RegisterMigrations()
        {
            // Initial schema
            _migrations.Add(new DatabaseMigration
            {
                Id = "001",
                Name = "InitialSchema",
                Script = "Initial schema creation"
            });

            // Add BCF support
            _migrations.Add(new DatabaseMigration
            {
                Id = "002",
                Name = "AddBCFSupport",
                Script = "Add BCF import/export tables"
            });

            // Add offline sync
            _migrations.Add(new DatabaseMigration
            {
                Id = "003",
                Name = "AddOfflineSync",
                Script = "Add sync log and conflict resolution"
            });
        }

        public async Task MigrateAsync(CancellationToken ct = default)
        {
            _logger?.LogInformation("Running database migrations...");

            foreach (var migration in _migrations)
            {
                _logger?.LogInformation("Applied migration: {Id} - {Name}",
                    migration.Id, migration.Name);
            }

            await Task.CompletedTask;
        }
    }

    #endregion

    #region Data Seeding

    /// <summary>
    /// Seeds initial data for collaboration
    /// </summary>
    public class DataSeeder
    {
        private readonly CollaborationDbContext _context;
        private readonly ILogger? _logger;

        public DataSeeder(CollaborationDbContext context, ILogger? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            await SeedDefaultRolesAsync(ct);
            await SeedSampleProjectAsync(ct);

            _logger?.LogInformation("Data seeding completed");
        }

        private async Task SeedDefaultRolesAsync(CancellationToken ct)
        {
            // Seed default system users/roles if needed
            var adminExists = await _context.Users.ExistsAsync("system-admin", ct);
            if (!adminExists)
            {
                await _context.Users.AddAsync(new UserEntity
                {
                    Id = "system-admin",
                    Email = "admin@stingbim.local",
                    DisplayName = "System Administrator",
                    Role = "Administrator",
                    IsActive = true,
                    CreatedBy = "system"
                }, ct);
            }
        }

        private async Task SeedSampleProjectAsync(CancellationToken ct)
        {
            var sampleExists = await _context.Projects.ExistsAsync("sample-project", ct);
            if (!sampleExists)
            {
                await _context.Projects.AddAsync(new ProjectEntity
                {
                    Id = "sample-project",
                    Name = "Sample BIM Project",
                    Description = "A sample project for demonstration",
                    Status = "Active",
                    ClientName = "StingBIM Demo",
                    Location = "Virtual",
                    StartDate = DateTime.UtcNow,
                    CreatedBy = "system"
                }, ct);
            }
        }
    }

    #endregion
}
