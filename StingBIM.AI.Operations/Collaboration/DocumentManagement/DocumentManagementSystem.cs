// StingBIM.AI.Collaboration - Document Management System
// Replicates BIM 360 Docs functionality with AI enhancements

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.DocumentManagement
{
    /// <summary>
    /// Comprehensive document management system inspired by BIM 360 Docs
    /// with AI-powered organization, search, and version control
    /// </summary>
    public class DocumentManagementSystem : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ManagedDocument> _documents = new();
        private readonly ConcurrentDictionary<string, DocumentFolder> _folders = new();
        private readonly ConcurrentDictionary<string, List<DocumentVersion>> _versions = new();
        private readonly ConcurrentDictionary<string, DocumentPermission> _permissions = new();
        private readonly ConcurrentDictionary<string, List<DocumentReview>> _reviews = new();
        private readonly DocumentIndexer _indexer;
        private readonly DocumentAI _documentAI;
        private readonly string _storageRoot;
        private readonly object _lockObject = new();

        public event EventHandler<DocumentUploadedEventArgs>? DocumentUploaded;
        public event EventHandler<DocumentVersionCreatedEventArgs>? VersionCreated;
        public event EventHandler<DocumentApprovalEventArgs>? ApprovalStatusChanged;
        public event EventHandler<DocumentSharedEventArgs>? DocumentShared;

        public DocumentManagementSystem(string storageRoot)
        {
            _storageRoot = storageRoot;
            _indexer = new DocumentIndexer();
            _documentAI = new DocumentAI();
            InitializeRootFolders();
        }

        private void InitializeRootFolders()
        {
            // BIM 360-style folder structure
            var rootFolders = new[]
            {
                ("project-files", "Project Files", FolderType.ProjectFiles),
                ("plans", "Plans", FolderType.Plans),
                ("project-specs", "Project Specifications", FolderType.Specifications),
                ("submittals", "Submittals", FolderType.Submittals),
                ("rfis", "RFIs", FolderType.RFIs),
                ("photos", "Photos", FolderType.Photos),
                ("reports", "Reports", FolderType.Reports),
                ("models", "Models", FolderType.Models),
                ("drawings", "Drawings", FolderType.Drawings),
                ("schedules", "Schedules", FolderType.Schedules)
            };

            foreach (var (id, name, type) in rootFolders)
            {
                _folders[id] = new DocumentFolder
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    ParentId = null,
                    CreatedAt = DateTime.UtcNow,
                    Permissions = new FolderPermissions { IsPublic = true }
                };
            }
        }

        #region Document Operations

        /// <summary>
        /// Upload a new document with automatic AI categorization
        /// </summary>
        public async Task<ManagedDocument> UploadDocumentAsync(
            Stream content,
            string fileName,
            string folderId,
            string uploadedBy,
            DocumentMetadata? metadata = null,
            CancellationToken ct = default)
        {
            var documentId = GenerateDocumentId();
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

            // Calculate hash for deduplication
            var contentBytes = await ReadStreamAsync(content, ct);
            var hash = ComputeHash(contentBytes);

            // Check for duplicate
            var existingDoc = _documents.Values.FirstOrDefault(d => d.ContentHash == hash);
            if (existingDoc != null)
            {
                // Return reference to existing document instead of duplicate
                return existingDoc;
            }

            // AI-powered categorization
            var aiAnalysis = await _documentAI.AnalyzeDocumentAsync(contentBytes, fileName, ct);

            var document = new ManagedDocument
            {
                Id = documentId,
                FileName = fileName,
                FileExtension = fileExtension,
                FolderId = folderId,
                ContentHash = hash,
                FileSize = contentBytes.Length,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow,
                CurrentVersion = 1,
                Status = DocumentStatus.Active,
                Metadata = metadata ?? new DocumentMetadata(),
                AIAnalysis = aiAnalysis
            };

            // Auto-populate metadata from AI
            if (string.IsNullOrEmpty(document.Metadata.Title))
                document.Metadata.Title = aiAnalysis.SuggestedTitle;
            if (document.Metadata.Tags == null || !document.Metadata.Tags.Any())
                document.Metadata.Tags = aiAnalysis.SuggestedTags;
            if (string.IsNullOrEmpty(document.Metadata.Category))
                document.Metadata.Category = aiAnalysis.DetectedCategory;

            // Store document
            var storagePath = GetStoragePath(documentId, 1);
            await SaveToStorageAsync(storagePath, contentBytes, ct);
            document.StoragePath = storagePath;

            // Create initial version
            var version = new DocumentVersion
            {
                VersionNumber = 1,
                CreatedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow,
                FileSize = contentBytes.Length,
                ContentHash = hash,
                StoragePath = storagePath,
                Comment = "Initial upload"
            };

            _documents[documentId] = document;
            _versions[documentId] = new List<DocumentVersion> { version };

            // Index for search
            await _indexer.IndexDocumentAsync(document, contentBytes, ct);

            DocumentUploaded?.Invoke(this, new DocumentUploadedEventArgs(document));

            return document;
        }

        /// <summary>
        /// Create a new version of an existing document
        /// </summary>
        public async Task<DocumentVersion> CreateVersionAsync(
            string documentId,
            Stream content,
            string createdBy,
            string comment,
            CancellationToken ct = default)
        {
            if (!_documents.TryGetValue(documentId, out var document))
                throw new DocumentNotFoundException(documentId);

            var contentBytes = await ReadStreamAsync(content, ct);
            var hash = ComputeHash(contentBytes);

            var newVersion = document.CurrentVersion + 1;
            var storagePath = GetStoragePath(documentId, newVersion);

            await SaveToStorageAsync(storagePath, contentBytes, ct);

            var version = new DocumentVersion
            {
                VersionNumber = newVersion,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                FileSize = contentBytes.Length,
                ContentHash = hash,
                StoragePath = storagePath,
                Comment = comment,
                PreviousVersionHash = document.ContentHash
            };

            // AI comparison with previous version
            var previousContent = await GetVersionContentAsync(documentId, document.CurrentVersion, ct);
            version.ChangeAnalysis = await _documentAI.CompareVersionsAsync(previousContent, contentBytes, ct);

            lock (_lockObject)
            {
                document.CurrentVersion = newVersion;
                document.ContentHash = hash;
                document.FileSize = contentBytes.Length;
                document.StoragePath = storagePath;
                document.ModifiedAt = DateTime.UtcNow;
                document.ModifiedBy = createdBy;

                if (!_versions.ContainsKey(documentId))
                    _versions[documentId] = new List<DocumentVersion>();
                _versions[documentId].Add(version);
            }

            // Re-index
            await _indexer.IndexDocumentAsync(document, contentBytes, ct);

            VersionCreated?.Invoke(this, new DocumentVersionCreatedEventArgs(documentId, version));

            return version;
        }

        /// <summary>
        /// Download document content
        /// </summary>
        public async Task<byte[]> DownloadDocumentAsync(
            string documentId,
            int? version = null,
            CancellationToken ct = default)
        {
            if (!_documents.TryGetValue(documentId, out var document))
                throw new DocumentNotFoundException(documentId);

            var targetVersion = version ?? document.CurrentVersion;
            return await GetVersionContentAsync(documentId, targetVersion, ct);
        }

        /// <summary>
        /// Get document with all metadata
        /// </summary>
        public ManagedDocument? GetDocument(string documentId)
        {
            return _documents.TryGetValue(documentId, out var doc) ? doc : null;
        }

        /// <summary>
        /// Get all versions of a document
        /// </summary>
        public List<DocumentVersion> GetVersionHistory(string documentId)
        {
            return _versions.TryGetValue(documentId, out var versions)
                ? versions.OrderByDescending(v => v.VersionNumber).ToList()
                : new List<DocumentVersion>();
        }

        /// <summary>
        /// Delete document (soft delete)
        /// </summary>
        public void DeleteDocument(string documentId, string deletedBy)
        {
            if (_documents.TryGetValue(documentId, out var document))
            {
                document.Status = DocumentStatus.Deleted;
                document.DeletedAt = DateTime.UtcNow;
                document.DeletedBy = deletedBy;
            }
        }

        /// <summary>
        /// Restore deleted document
        /// </summary>
        public void RestoreDocument(string documentId)
        {
            if (_documents.TryGetValue(documentId, out var document))
            {
                document.Status = DocumentStatus.Active;
                document.DeletedAt = null;
                document.DeletedBy = null;
            }
        }

        #endregion

        #region Folder Operations

        /// <summary>
        /// Create a new folder
        /// </summary>
        public DocumentFolder CreateFolder(string name, string? parentId, string createdBy, FolderType type = FolderType.General)
        {
            var folderId = Guid.NewGuid().ToString("N")[..12];

            var folder = new DocumentFolder
            {
                Id = folderId,
                Name = name,
                ParentId = parentId,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Permissions = new FolderPermissions()
            };

            _folders[folderId] = folder;
            return folder;
        }

        /// <summary>
        /// Get folder contents
        /// </summary>
        public FolderContents GetFolderContents(string folderId)
        {
            var subfolders = _folders.Values
                .Where(f => f.ParentId == folderId)
                .OrderBy(f => f.Name)
                .ToList();

            var documents = _documents.Values
                .Where(d => d.FolderId == folderId && d.Status == DocumentStatus.Active)
                .OrderBy(d => d.FileName)
                .ToList();

            return new FolderContents
            {
                FolderId = folderId,
                Subfolders = subfolders,
                Documents = documents,
                TotalSize = documents.Sum(d => d.FileSize),
                DocumentCount = documents.Count
            };
        }

        /// <summary>
        /// Get folder tree structure
        /// </summary>
        public List<FolderTreeNode> GetFolderTree()
        {
            var rootFolders = _folders.Values.Where(f => f.ParentId == null).ToList();
            return rootFolders.Select(BuildFolderTree).ToList();
        }

        private FolderTreeNode BuildFolderTree(DocumentFolder folder)
        {
            var children = _folders.Values
                .Where(f => f.ParentId == folder.Id)
                .Select(BuildFolderTree)
                .ToList();

            var docCount = _documents.Values.Count(d => d.FolderId == folder.Id && d.Status == DocumentStatus.Active);

            return new FolderTreeNode
            {
                Folder = folder,
                Children = children,
                DocumentCount = docCount,
                TotalDocumentCount = docCount + children.Sum(c => c.TotalDocumentCount)
            };
        }

        #endregion

        #region Search & Discovery

        /// <summary>
        /// Full-text search across all documents
        /// </summary>
        public async Task<SearchResults> SearchAsync(
            DocumentSearchQuery query,
            CancellationToken ct = default)
        {
            var results = await _indexer.SearchAsync(query, ct);

            // AI-enhanced ranking
            if (query.UseAIRanking)
            {
                results = await _documentAI.RerankResultsAsync(results, query.Query, ct);
            }

            return results;
        }

        /// <summary>
        /// Find similar documents using AI
        /// </summary>
        public async Task<List<SimilarDocument>> FindSimilarDocumentsAsync(
            string documentId,
            int maxResults = 10,
            CancellationToken ct = default)
        {
            if (!_documents.TryGetValue(documentId, out var document))
                throw new DocumentNotFoundException(documentId);

            return await _documentAI.FindSimilarAsync(document, _documents.Values.ToList(), maxResults, ct);
        }

        /// <summary>
        /// Get AI-powered document recommendations
        /// </summary>
        public async Task<List<DocumentRecommendation>> GetRecommendationsAsync(
            string userId,
            string? context = null,
            CancellationToken ct = default)
        {
            var userHistory = GetUserDocumentHistory(userId);
            return await _documentAI.GetRecommendationsAsync(userHistory, _documents.Values.ToList(), context, ct);
        }

        #endregion

        #region Review & Approval Workflow

        /// <summary>
        /// Submit document for review
        /// </summary>
        public DocumentReview SubmitForReview(
            string documentId,
            string submittedBy,
            List<string> reviewers,
            ReviewType reviewType,
            DateTime? dueDate = null)
        {
            if (!_documents.TryGetValue(documentId, out var document))
                throw new DocumentNotFoundException(documentId);

            var review = new DocumentReview
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                DocumentId = documentId,
                DocumentVersion = document.CurrentVersion,
                SubmittedBy = submittedBy,
                SubmittedAt = DateTime.UtcNow,
                ReviewType = reviewType,
                DueDate = dueDate,
                Status = ReviewStatus.Pending,
                Reviewers = reviewers.Select(r => new ReviewerStatus
                {
                    UserId = r,
                    Status = ReviewerDecision.Pending
                }).ToList()
            };

            if (!_reviews.ContainsKey(documentId))
                _reviews[documentId] = new List<DocumentReview>();
            _reviews[documentId].Add(review);

            document.Status = DocumentStatus.InReview;

            return review;
        }

        /// <summary>
        /// Submit reviewer decision
        /// </summary>
        public void SubmitReviewDecision(
            string reviewId,
            string reviewerId,
            ReviewerDecision decision,
            string? comments = null,
            List<ReviewMarkup>? markups = null)
        {
            var review = _reviews.Values
                .SelectMany(r => r)
                .FirstOrDefault(r => r.Id == reviewId);

            if (review == null)
                throw new ReviewNotFoundException(reviewId);

            var reviewer = review.Reviewers.FirstOrDefault(r => r.UserId == reviewerId);
            if (reviewer == null)
                throw new InvalidOperationException("User is not a reviewer");

            reviewer.Status = decision;
            reviewer.Comments = comments;
            reviewer.Markups = markups ?? new List<ReviewMarkup>();
            reviewer.DecisionAt = DateTime.UtcNow;

            // Check if all reviewers have responded
            UpdateReviewStatus(review);
        }

        private void UpdateReviewStatus(DocumentReview review)
        {
            var allDecided = review.Reviewers.All(r => r.Status != ReviewerDecision.Pending);
            if (!allDecided) return;

            var approved = review.Reviewers.All(r =>
                r.Status == ReviewerDecision.Approved ||
                r.Status == ReviewerDecision.ApprovedWithComments);

            review.Status = approved ? ReviewStatus.Approved : ReviewStatus.Rejected;
            review.CompletedAt = DateTime.UtcNow;

            if (_documents.TryGetValue(review.DocumentId, out var document))
            {
                document.Status = approved ? DocumentStatus.Approved : DocumentStatus.RequiresRevision;
                document.ApprovalInfo = new ApprovalInfo
                {
                    Status = review.Status,
                    ApprovedAt = approved ? DateTime.UtcNow : null,
                    ReviewId = review.Id
                };
            }

            ApprovalStatusChanged?.Invoke(this, new DocumentApprovalEventArgs(review));
        }

        /// <summary>
        /// Get pending reviews for user
        /// </summary>
        public List<DocumentReview> GetPendingReviews(string userId)
        {
            return _reviews.Values
                .SelectMany(r => r)
                .Where(r => r.Status == ReviewStatus.Pending &&
                           r.Reviewers.Any(rev => rev.UserId == userId && rev.Status == ReviewerDecision.Pending))
                .ToList();
        }

        #endregion

        #region Sharing & Permissions

        /// <summary>
        /// Share document with users or external parties
        /// </summary>
        public ShareLink ShareDocument(
            string documentId,
            string sharedBy,
            ShareSettings settings)
        {
            if (!_documents.TryGetValue(documentId, out var document))
                throw new DocumentNotFoundException(documentId);

            var shareLink = new ShareLink
            {
                Id = Guid.NewGuid().ToString("N"),
                DocumentId = documentId,
                CreatedBy = sharedBy,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = settings.ExpiresAt,
                AllowDownload = settings.AllowDownload,
                AllowComments = settings.AllowComments,
                RequireAuthentication = settings.RequireAuthentication,
                Password = settings.Password,
                MaxViews = settings.MaxViews,
                SharedWith = settings.SharedWith
            };

            document.ShareLinks ??= new List<ShareLink>();
            document.ShareLinks.Add(shareLink);

            DocumentShared?.Invoke(this, new DocumentSharedEventArgs(documentId, shareLink));

            return shareLink;
        }

        /// <summary>
        /// Set document permissions
        /// </summary>
        public void SetPermissions(string documentId, DocumentPermission permission)
        {
            _permissions[documentId] = permission;
        }

        /// <summary>
        /// Check if user has permission
        /// </summary>
        public bool HasPermission(string documentId, string userId, PermissionLevel requiredLevel)
        {
            if (!_permissions.TryGetValue(documentId, out var permission))
                return true; // No explicit permission = public access

            var userLevel = permission.GetUserLevel(userId);
            return userLevel >= requiredLevel;
        }

        #endregion

        #region Transmittals

        /// <summary>
        /// Create a transmittal package (BIM 360 feature)
        /// </summary>
        public Transmittal CreateTransmittal(
            string title,
            List<string> documentIds,
            string createdBy,
            TransmittalSettings settings)
        {
            var transmittal = new Transmittal
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Title = title,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Status = TransmittalStatus.Draft,
                Settings = settings,
                Documents = documentIds.Select(id =>
                {
                    var doc = _documents.GetValueOrDefault(id);
                    return new TransmittalDocument
                    {
                        DocumentId = id,
                        FileName = doc?.FileName ?? "Unknown",
                        Version = doc?.CurrentVersion ?? 0,
                        IncludeMarkups = true
                    };
                }).ToList()
            };

            return transmittal;
        }

        /// <summary>
        /// Send transmittal to recipients
        /// </summary>
        public async Task SendTransmittalAsync(
            Transmittal transmittal,
            List<string> recipients,
            CancellationToken ct = default)
        {
            transmittal.Status = TransmittalStatus.Sent;
            transmittal.SentAt = DateTime.UtcNow;
            transmittal.Recipients = recipients;

            // In production, this would send emails/notifications
            await Task.Delay(100, ct);
        }

        #endregion

        #region Helper Methods

        private string GenerateDocumentId() => Guid.NewGuid().ToString("N")[..16];

        private string GetStoragePath(string documentId, int version)
            => Path.Combine(_storageRoot, documentId[..2], documentId, $"v{version}");

        private static string ComputeHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            return Convert.ToBase64String(hash);
        }

        private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        private async Task SaveToStorageAsync(string path, byte[] content, CancellationToken ct)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllBytesAsync(path, content, ct);
        }

        private async Task<byte[]> GetVersionContentAsync(string documentId, int version, CancellationToken ct)
        {
            var path = GetStoragePath(documentId, version);
            if (!File.Exists(path))
                throw new DocumentVersionNotFoundException(documentId, version);

            return await File.ReadAllBytesAsync(path, ct);
        }

        private List<DocumentAccess> GetUserDocumentHistory(string userId)
        {
            // Simplified - in production would track actual access history
            return _documents.Values
                .Where(d => d.UploadedBy == userId || d.ModifiedBy == userId)
                .Select(d => new DocumentAccess
                {
                    DocumentId = d.Id,
                    UserId = userId,
                    AccessedAt = d.ModifiedAt ?? d.UploadedAt,
                    AccessType = d.UploadedBy == userId ? AccessType.Upload : AccessType.Edit
                })
                .ToList();
        }

        public async ValueTask DisposeAsync()
        {
            await _indexer.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Document Models

    public class ManagedDocument
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileExtension { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string ContentHash { get; set; } = "";
        public long FileSize { get; set; }
        public string StoragePath { get; set; } = "";
        public string UploadedBy { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int CurrentVersion { get; set; }
        public DocumentStatus Status { get; set; }
        public DocumentMetadata Metadata { get; set; } = new();
        public DocumentAIAnalysis? AIAnalysis { get; set; }
        public ApprovalInfo? ApprovalInfo { get; set; }
        public List<ShareLink>? ShareLinks { get; set; }
    }

    public class DocumentMetadata
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public string? Discipline { get; set; }
        public string? Phase { get; set; }
        public string? Level { get; set; }
        public string? Zone { get; set; }
        public Dictionary<string, string>? CustomFields { get; set; }
    }

    public class DocumentVersion
    {
        public int VersionNumber { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long FileSize { get; set; }
        public string ContentHash { get; set; } = "";
        public string StoragePath { get; set; } = "";
        public string Comment { get; set; } = "";
        public string? PreviousVersionHash { get; set; }
        public VersionChangeAnalysis? ChangeAnalysis { get; set; }
    }

    public class VersionChangeAnalysis
    {
        public double SimilarityScore { get; set; }
        public List<string> Changes { get; set; } = new();
        public ChangeType ChangeType { get; set; }
        public string Summary { get; set; } = "";
    }

    public enum ChangeType { Minor, Moderate, Major, Complete }
    public enum DocumentStatus { Active, InReview, Approved, RequiresRevision, Archived, Deleted }
    public enum FolderType { General, ProjectFiles, Plans, Specifications, Submittals, RFIs, Photos, Reports, Models, Drawings, Schedules }
    public enum ReviewType { Approval, Comment, Coordination }
    public enum ReviewStatus { Pending, Approved, Rejected, Cancelled }
    public enum ReviewerDecision { Pending, Approved, ApprovedWithComments, Rejected, RequestChanges }
    public enum PermissionLevel { None = 0, View = 1, Comment = 2, Edit = 3, Admin = 4 }
    public enum AccessType { View, Download, Edit, Upload, Share }
    public enum TransmittalStatus { Draft, Sent, Acknowledged, Completed }

    #endregion

    #region Folder Models

    public class DocumentFolder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ParentId { get; set; }
        public FolderType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public FolderPermissions Permissions { get; set; } = new();
    }

    public class FolderPermissions
    {
        public bool IsPublic { get; set; } = true;
        public List<FolderUserPermission> UserPermissions { get; set; } = new();
    }

    public class FolderUserPermission
    {
        public string UserId { get; set; } = "";
        public PermissionLevel Level { get; set; }
    }

    public class FolderContents
    {
        public string FolderId { get; set; } = "";
        public List<DocumentFolder> Subfolders { get; set; } = new();
        public List<ManagedDocument> Documents { get; set; } = new();
        public long TotalSize { get; set; }
        public int DocumentCount { get; set; }
    }

    public class FolderTreeNode
    {
        public DocumentFolder Folder { get; set; } = new();
        public List<FolderTreeNode> Children { get; set; } = new();
        public int DocumentCount { get; set; }
        public int TotalDocumentCount { get; set; }
    }

    #endregion

    #region Review Models

    public class DocumentReview
    {
        public string Id { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public int DocumentVersion { get; set; }
        public string SubmittedBy { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
        public ReviewType ReviewType { get; set; }
        public DateTime? DueDate { get; set; }
        public ReviewStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<ReviewerStatus> Reviewers { get; set; } = new();
    }

    public class ReviewerStatus
    {
        public string UserId { get; set; } = "";
        public ReviewerDecision Status { get; set; }
        public string? Comments { get; set; }
        public List<ReviewMarkup> Markups { get; set; } = new();
        public DateTime? DecisionAt { get; set; }
    }

    public class ReviewMarkup
    {
        public string Id { get; set; } = "";
        public MarkupType Type { get; set; }
        public string Content { get; set; } = "";
        public MarkupPosition Position { get; set; } = new();
        public string? PageNumber { get; set; }
    }

    public class MarkupPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public enum MarkupType { Comment, Cloud, Arrow, Rectangle, Text, Stamp }

    public class ApprovalInfo
    {
        public ReviewStatus Status { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ReviewId { get; set; }
    }

    #endregion

    #region Sharing Models

    public class ShareLink
    {
        public string Id { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool AllowDownload { get; set; }
        public bool AllowComments { get; set; }
        public bool RequireAuthentication { get; set; }
        public string? Password { get; set; }
        public int? MaxViews { get; set; }
        public int ViewCount { get; set; }
        public List<string>? SharedWith { get; set; }

        public string GetUrl() => $"https://share.stingbim.com/d/{Id}";
    }

    public class ShareSettings
    {
        public DateTime? ExpiresAt { get; set; }
        public bool AllowDownload { get; set; } = true;
        public bool AllowComments { get; set; } = true;
        public bool RequireAuthentication { get; set; }
        public string? Password { get; set; }
        public int? MaxViews { get; set; }
        public List<string>? SharedWith { get; set; }
    }

    public class DocumentPermission
    {
        public string DocumentId { get; set; } = "";
        public bool IsPublic { get; set; }
        public List<UserPermission> UserPermissions { get; set; } = new();
        public List<RolePermission> RolePermissions { get; set; } = new();

        public PermissionLevel GetUserLevel(string userId)
        {
            var userPerm = UserPermissions.FirstOrDefault(p => p.UserId == userId);
            if (userPerm != null) return userPerm.Level;
            return IsPublic ? PermissionLevel.View : PermissionLevel.None;
        }
    }

    public class UserPermission
    {
        public string UserId { get; set; } = "";
        public PermissionLevel Level { get; set; }
    }

    public class RolePermission
    {
        public string Role { get; set; } = "";
        public PermissionLevel Level { get; set; }
    }

    #endregion

    #region Transmittal Models

    public class Transmittal
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public TransmittalStatus Status { get; set; }
        public TransmittalSettings Settings { get; set; } = new();
        public List<TransmittalDocument> Documents { get; set; } = new();
        public List<string>? Recipients { get; set; }
    }

    public class TransmittalSettings
    {
        public string? CoverLetter { get; set; }
        public bool IncludeVersionHistory { get; set; }
        public bool RequireAcknowledgment { get; set; }
        public string? Purpose { get; set; }
        public Priority Priority { get; set; }
    }

    public class TransmittalDocument
    {
        public string DocumentId { get; set; } = "";
        public string FileName { get; set; } = "";
        public int Version { get; set; }
        public bool IncludeMarkups { get; set; }
    }

    public enum Priority { Low, Normal, High, Urgent }

    #endregion

    #region Search Models

    public class DocumentSearchQuery
    {
        public string Query { get; set; } = "";
        public List<string>? FolderIds { get; set; }
        public List<string>? FileTypes { get; set; }
        public List<string>? Tags { get; set; }
        public string? Category { get; set; }
        public string? Discipline { get; set; }
        public DateTime? ModifiedAfter { get; set; }
        public DateTime? ModifiedBefore { get; set; }
        public string? UploadedBy { get; set; }
        public DocumentStatus? Status { get; set; }
        public bool UseAIRanking { get; set; } = true;
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }

    public class SearchResults
    {
        public List<SearchResult> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public double SearchTime { get; set; }
        public List<SearchFacet> Facets { get; set; } = new();
    }

    public class SearchResult
    {
        public ManagedDocument Document { get; set; } = new();
        public double Score { get; set; }
        public List<string> Highlights { get; set; } = new();
        public string? MatchedContent { get; set; }
    }

    public class SearchFacet
    {
        public string Name { get; set; } = "";
        public List<FacetValue> Values { get; set; } = new();
    }

    public class FacetValue
    {
        public string Value { get; set; } = "";
        public int Count { get; set; }
    }

    public class SimilarDocument
    {
        public ManagedDocument Document { get; set; } = new();
        public double Similarity { get; set; }
        public string Reason { get; set; } = "";
    }

    public class DocumentRecommendation
    {
        public ManagedDocument Document { get; set; } = new();
        public double Relevance { get; set; }
        public string Reason { get; set; } = "";
        public RecommendationType Type { get; set; }
    }

    public enum RecommendationType { RecentlyAccessed, FrequentlyUsed, Related, Trending, AIRecommended }

    public class DocumentAccess
    {
        public string DocumentId { get; set; } = "";
        public string UserId { get; set; } = "";
        public DateTime AccessedAt { get; set; }
        public AccessType AccessType { get; set; }
    }

    #endregion

    #region AI Models

    public class DocumentAIAnalysis
    {
        public string SuggestedTitle { get; set; } = "";
        public List<string> SuggestedTags { get; set; } = new();
        public string DetectedCategory { get; set; } = "";
        public string DetectedDiscipline { get; set; } = "";
        public DocumentType DetectedType { get; set; }
        public double Confidence { get; set; }
        public List<string> ExtractedEntities { get; set; } = new();
        public string Summary { get; set; } = "";
        public string? DetectedLanguage { get; set; }
    }

    public enum DocumentType { Drawing, Specification, Schedule, Report, Photo, Model, Contract, Other }

    #endregion

    #region Supporting Classes

    public class DocumentIndexer : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, IndexedDocument> _index = new();

        public Task IndexDocumentAsync(ManagedDocument document, byte[] content, CancellationToken ct)
        {
            _index[document.Id] = new IndexedDocument
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Content = ExtractText(content, document.FileExtension),
                Tags = document.Metadata.Tags ?? new List<string>(),
                Category = document.Metadata.Category ?? "",
                IndexedAt = DateTime.UtcNow
            };
            return Task.CompletedTask;
        }

        public Task<SearchResults> SearchAsync(DocumentSearchQuery query, CancellationToken ct)
        {
            var queryLower = query.Query.ToLowerInvariant();
            var results = _index.Values
                .Where(idx => idx.FileName.ToLowerInvariant().Contains(queryLower) ||
                             idx.Content.ToLowerInvariant().Contains(queryLower) ||
                             idx.Tags.Any(t => t.ToLowerInvariant().Contains(queryLower)))
                .Select(idx => new SearchResult
                {
                    Document = new ManagedDocument { Id = idx.DocumentId, FileName = idx.FileName },
                    Score = CalculateScore(idx, queryLower)
                })
                .OrderByDescending(r => r.Score)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList();

            return Task.FromResult(new SearchResults
            {
                Results = results,
                TotalCount = results.Count
            });
        }

        private static string ExtractText(byte[] content, string extension) =>
            extension switch
            {
                ".txt" or ".csv" => Encoding.UTF8.GetString(content),
                _ => "" // Would use proper extractors for PDF, Office, etc.
            };

        private static double CalculateScore(IndexedDocument doc, string query)
        {
            var score = 0.0;
            if (doc.FileName.ToLowerInvariant().Contains(query)) score += 2.0;
            if (doc.Content.ToLowerInvariant().Contains(query)) score += 1.0;
            if (doc.Tags.Any(t => t.ToLowerInvariant() == query)) score += 1.5;
            return score;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class IndexedDocument
    {
        public string DocumentId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Content { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string Category { get; set; } = "";
        public DateTime IndexedAt { get; set; }
    }

    public class DocumentAI
    {
        public Task<DocumentAIAnalysis> AnalyzeDocumentAsync(byte[] content, string fileName, CancellationToken ct)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var analysis = new DocumentAIAnalysis
            {
                SuggestedTitle = Path.GetFileNameWithoutExtension(fileName),
                DetectedType = DetectDocumentType(extension),
                DetectedCategory = DetectCategory(fileName),
                DetectedDiscipline = DetectDiscipline(fileName),
                SuggestedTags = GenerateTags(fileName),
                Confidence = 0.85
            };
            return Task.FromResult(analysis);
        }

        public Task<VersionChangeAnalysis> CompareVersionsAsync(byte[] oldContent, byte[] newContent, CancellationToken ct)
        {
            var similarity = CalculateSimilarity(oldContent, newContent);
            return Task.FromResult(new VersionChangeAnalysis
            {
                SimilarityScore = similarity,
                ChangeType = similarity > 0.9 ? ChangeType.Minor :
                            similarity > 0.7 ? ChangeType.Moderate :
                            similarity > 0.3 ? ChangeType.Major : ChangeType.Complete,
                Summary = $"Document similarity: {similarity:P0}"
            });
        }

        public Task<SearchResults> RerankResultsAsync(SearchResults results, string query, CancellationToken ct)
            => Task.FromResult(results);

        public Task<List<SimilarDocument>> FindSimilarAsync(ManagedDocument doc, List<ManagedDocument> all, int max, CancellationToken ct)
            => Task.FromResult(new List<SimilarDocument>());

        public Task<List<DocumentRecommendation>> GetRecommendationsAsync(List<DocumentAccess> history, List<ManagedDocument> all, string? context, CancellationToken ct)
            => Task.FromResult(new List<DocumentRecommendation>());

        private static DocumentType DetectDocumentType(string ext) => ext switch
        {
            ".dwg" or ".dxf" or ".pdf" => DocumentType.Drawing,
            ".rvt" or ".rfa" or ".ifc" => DocumentType.Model,
            ".doc" or ".docx" => DocumentType.Specification,
            ".xls" or ".xlsx" => DocumentType.Schedule,
            ".jpg" or ".png" or ".jpeg" => DocumentType.Photo,
            _ => DocumentType.Other
        };

        private static string DetectCategory(string fileName)
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("arch") || lower.Contains("plan")) return "Architectural";
            if (lower.Contains("struct")) return "Structural";
            if (lower.Contains("mep") || lower.Contains("mech") || lower.Contains("elec") || lower.Contains("plumb")) return "MEP";
            return "General";
        }

        private static string DetectDiscipline(string fileName)
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("arch")) return "Architecture";
            if (lower.Contains("struct")) return "Structure";
            if (lower.Contains("mech") || lower.Contains("hvac")) return "Mechanical";
            if (lower.Contains("elec")) return "Electrical";
            if (lower.Contains("plumb")) return "Plumbing";
            return "Multi-Discipline";
        }

        private static List<string> GenerateTags(string fileName)
        {
            var tags = new List<string>();
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("floor")) tags.Add("floor-plan");
            if (lower.Contains("section")) tags.Add("section");
            if (lower.Contains("detail")) tags.Add("detail");
            if (lower.Contains("schedule")) tags.Add("schedule");
            return tags;
        }

        private static double CalculateSimilarity(byte[] a, byte[] b)
        {
            if (a.Length == 0 || b.Length == 0) return 0;
            var sameBytes = a.Zip(b, (x, y) => x == y).Count(same => same);
            return (double)sameBytes / Math.Max(a.Length, b.Length);
        }
    }

    #endregion

    #region Event Args

    public class DocumentUploadedEventArgs : EventArgs
    {
        public ManagedDocument Document { get; }
        public DocumentUploadedEventArgs(ManagedDocument document) => Document = document;
    }

    public class DocumentVersionCreatedEventArgs : EventArgs
    {
        public string DocumentId { get; }
        public DocumentVersion Version { get; }
        public DocumentVersionCreatedEventArgs(string documentId, DocumentVersion version)
        {
            DocumentId = documentId;
            Version = version;
        }
    }

    public class DocumentApprovalEventArgs : EventArgs
    {
        public DocumentReview Review { get; }
        public DocumentApprovalEventArgs(DocumentReview review) => Review = review;
    }

    public class DocumentSharedEventArgs : EventArgs
    {
        public string DocumentId { get; }
        public ShareLink ShareLink { get; }
        public DocumentSharedEventArgs(string documentId, ShareLink shareLink)
        {
            DocumentId = documentId;
            ShareLink = shareLink;
        }
    }

    #endregion

    #region Exceptions

    public class DocumentNotFoundException : Exception
    {
        public string DocumentId { get; }
        public DocumentNotFoundException(string documentId) : base($"Document not found: {documentId}")
            => DocumentId = documentId;
    }

    public class DocumentVersionNotFoundException : Exception
    {
        public string DocumentId { get; }
        public int Version { get; }
        public DocumentVersionNotFoundException(string documentId, int version)
            : base($"Version {version} not found for document: {documentId}")
        {
            DocumentId = documentId;
            Version = version;
        }
    }

    public class ReviewNotFoundException : Exception
    {
        public string ReviewId { get; }
        public ReviewNotFoundException(string reviewId) : base($"Review not found: {reviewId}")
            => ReviewId = reviewId;
    }

    #endregion
}
