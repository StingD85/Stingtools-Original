// ============================================================================
// StingBIM AI - Document Control System
// Comprehensive BIM document management, versioning, and transmittal tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DocumentControl
{
    /// <summary>
    /// Central document control system for BIM project management.
    /// Handles document registration, versioning, transmittals, and audit trails.
    /// </summary>
    public sealed class DocumentControlSystem
    {
        private static readonly Lazy<DocumentControlSystem> _instance =
            new Lazy<DocumentControlSystem>(() => new DocumentControlSystem());
        public static DocumentControlSystem Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, BIMDocument> _documents = new();
        private readonly Dictionary<string, List<DocumentVersion>> _versionHistory = new();
        private readonly List<Transmittal> _transmittals = new();
        private readonly List<DocumentAuditEntry> _auditTrail = new();
        private readonly Dictionary<string, DocumentApproval> _approvals = new();

        public event EventHandler<DocumentEventArgs> DocumentRegistered;
        public event EventHandler<DocumentEventArgs> DocumentUpdated;
        public event EventHandler<TransmittalEventArgs> TransmittalCreated;
        public event EventHandler<ApprovalEventArgs> ApprovalRequired;

        private DocumentControlSystem() { }

        #region Document Registration

        /// <summary>
        /// Register a new BIM document in the control system
        /// </summary>
        public BIMDocument RegisterDocument(DocumentRegistrationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
                throw new ArgumentException("Document number is required");

            lock (_lock)
            {
                var document = new BIMDocument
                {
                    DocumentId = Guid.NewGuid().ToString(),
                    DocumentNumber = request.DocumentNumber,
                    Title = request.Title,
                    Description = request.Description,
                    DocumentType = request.DocumentType,
                    Discipline = request.Discipline,
                    Phase = request.Phase,
                    Status = DocumentStatus.Draft,
                    CurrentRevision = "A",
                    RevisionNumber = 1,
                    CreatedBy = request.Author,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow,
                    FilePath = request.FilePath,
                    FileSize = request.FileSize,
                    Checksum = request.Checksum,
                    Metadata = request.Metadata ?? new Dictionary<string, string>(),
                    Tags = request.Tags ?? new List<string>()
                };

                _documents[document.DocumentId] = document;

                // Create initial version
                var version = new DocumentVersion
                {
                    VersionId = Guid.NewGuid().ToString(),
                    DocumentId = document.DocumentId,
                    Revision = document.CurrentRevision,
                    RevisionNumber = document.RevisionNumber,
                    CreatedBy = document.CreatedBy,
                    CreatedDate = document.CreatedDate,
                    Description = "Initial version",
                    FilePath = document.FilePath,
                    FileSize = document.FileSize,
                    Checksum = document.Checksum
                };

                _versionHistory[document.DocumentId] = new List<DocumentVersion> { version };

                // Audit trail
                AddAuditEntry(document.DocumentId, AuditAction.Created, request.Author, "Document registered");

                DocumentRegistered?.Invoke(this, new DocumentEventArgs(document));

                return document;
            }
        }

        /// <summary>
        /// Update an existing document with a new revision
        /// </summary>
        public DocumentVersion CreateRevision(RevisionRequest request)
        {
            lock (_lock)
            {
                if (!_documents.TryGetValue(request.DocumentId, out var document))
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");

                // Increment revision
                var newRevision = IncrementRevision(document.CurrentRevision);
                var newRevisionNumber = document.RevisionNumber + 1;

                var version = new DocumentVersion
                {
                    VersionId = Guid.NewGuid().ToString(),
                    DocumentId = document.DocumentId,
                    Revision = newRevision,
                    RevisionNumber = newRevisionNumber,
                    CreatedBy = request.Author,
                    CreatedDate = DateTime.UtcNow,
                    Description = request.Description,
                    ChangesSummary = request.ChangesSummary,
                    FilePath = request.FilePath,
                    FileSize = request.FileSize,
                    Checksum = request.Checksum
                };

                _versionHistory[document.DocumentId].Add(version);

                // Update document
                document.CurrentRevision = newRevision;
                document.RevisionNumber = newRevisionNumber;
                document.LastModifiedDate = DateTime.UtcNow;
                document.LastModifiedBy = request.Author;
                document.FilePath = request.FilePath;
                document.FileSize = request.FileSize;
                document.Checksum = request.Checksum;

                // Reset approval if required
                if (document.RequiresApproval)
                {
                    document.Status = DocumentStatus.PendingReview;
                    ApprovalRequired?.Invoke(this, new ApprovalEventArgs(document));
                }

                AddAuditEntry(document.DocumentId, AuditAction.Revised, request.Author,
                    $"Revision {newRevision}: {request.Description}");

                DocumentUpdated?.Invoke(this, new DocumentEventArgs(document));

                return version;
            }
        }

        private string IncrementRevision(string currentRevision)
        {
            if (string.IsNullOrEmpty(currentRevision) || currentRevision == "P")
                return "A";

            var lastChar = currentRevision[^1];
            if (lastChar == 'Z')
                return currentRevision + "A";

            return currentRevision[..^1] + (char)(lastChar + 1);
        }

        #endregion

        #region Document Search & Retrieval

        /// <summary>
        /// Search documents by various criteria
        /// </summary>
        public List<BIMDocument> SearchDocuments(DocumentSearchCriteria criteria)
        {
            lock (_lock)
            {
                var query = _documents.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(criteria.SearchText))
                {
                    var searchLower = criteria.SearchText.ToLower();
                    query = query.Where(d =>
                        d.DocumentNumber.ToLower().Contains(searchLower) ||
                        d.Title.ToLower().Contains(searchLower) ||
                        (d.Description?.ToLower().Contains(searchLower) ?? false));
                }

                if (criteria.DocumentType.HasValue)
                    query = query.Where(d => d.DocumentType == criteria.DocumentType.Value);

                if (criteria.Discipline.HasValue)
                    query = query.Where(d => d.Discipline == criteria.Discipline.Value);

                if (criteria.Status.HasValue)
                    query = query.Where(d => d.Status == criteria.Status.Value);

                if (!string.IsNullOrWhiteSpace(criteria.Phase))
                    query = query.Where(d => d.Phase == criteria.Phase);

                if (criteria.Tags?.Any() == true)
                    query = query.Where(d => d.Tags.Intersect(criteria.Tags).Any());

                if (criteria.ModifiedAfter.HasValue)
                    query = query.Where(d => d.LastModifiedDate >= criteria.ModifiedAfter.Value);

                if (criteria.ModifiedBefore.HasValue)
                    query = query.Where(d => d.LastModifiedDate <= criteria.ModifiedBefore.Value);

                return query.OrderByDescending(d => d.LastModifiedDate).ToList();
            }
        }

        /// <summary>
        /// Get a specific document by ID
        /// </summary>
        public BIMDocument GetDocument(string documentId)
        {
            lock (_lock)
            {
                return _documents.TryGetValue(documentId, out var doc) ? doc : null;
            }
        }

        /// <summary>
        /// Get document by document number
        /// </summary>
        public BIMDocument GetDocumentByNumber(string documentNumber)
        {
            lock (_lock)
            {
                return _documents.Values.FirstOrDefault(d =>
                    d.DocumentNumber.Equals(documentNumber, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Get version history for a document
        /// </summary>
        public List<DocumentVersion> GetVersionHistory(string documentId)
        {
            lock (_lock)
            {
                return _versionHistory.TryGetValue(documentId, out var history)
                    ? history.OrderByDescending(v => v.RevisionNumber).ToList()
                    : new List<DocumentVersion>();
            }
        }

        #endregion

        #region Transmittals

        /// <summary>
        /// Create a new transmittal package
        /// </summary>
        public Transmittal CreateTransmittal(TransmittalRequest request)
        {
            lock (_lock)
            {
                var transmittal = new Transmittal
                {
                    TransmittalId = Guid.NewGuid().ToString(),
                    TransmittalNumber = GenerateTransmittalNumber(),
                    Subject = request.Subject,
                    Purpose = request.Purpose,
                    FromCompany = request.FromCompany,
                    ToCompany = request.ToCompany,
                    ToContact = request.ToContact,
                    CcContacts = request.CcContacts ?? new List<string>(),
                    CreatedBy = request.CreatedBy,
                    CreatedDate = DateTime.UtcNow,
                    DueDate = request.DueDate,
                    Status = TransmittalStatus.Draft,
                    Documents = new List<TransmittalDocument>(),
                    Notes = request.Notes
                };

                // Add documents to transmittal
                foreach (var docId in request.DocumentIds)
                {
                    if (_documents.TryGetValue(docId, out var doc))
                    {
                        transmittal.Documents.Add(new TransmittalDocument
                        {
                            DocumentId = doc.DocumentId,
                            DocumentNumber = doc.DocumentNumber,
                            Title = doc.Title,
                            Revision = doc.CurrentRevision,
                            Purpose = request.Purpose
                        });

                        // Update document status
                        doc.Status = DocumentStatus.Issued;
                        AddAuditEntry(doc.DocumentId, AuditAction.Transmitted,
                            request.CreatedBy, $"Added to transmittal {transmittal.TransmittalNumber}");
                    }
                }

                _transmittals.Add(transmittal);
                TransmittalCreated?.Invoke(this, new TransmittalEventArgs(transmittal));

                return transmittal;
            }
        }

        /// <summary>
        /// Issue a transmittal (send to recipients)
        /// </summary>
        public void IssueTransmittal(string transmittalId, string issuedBy)
        {
            lock (_lock)
            {
                var transmittal = _transmittals.FirstOrDefault(t => t.TransmittalId == transmittalId);
                if (transmittal == null)
                    throw new KeyNotFoundException($"Transmittal {transmittalId} not found");

                transmittal.Status = TransmittalStatus.Issued;
                transmittal.IssuedDate = DateTime.UtcNow;
                transmittal.IssuedBy = issuedBy;

                foreach (var doc in transmittal.Documents)
                {
                    AddAuditEntry(doc.DocumentId, AuditAction.Issued,
                        issuedBy, $"Issued via transmittal {transmittal.TransmittalNumber}");
                }
            }
        }

        private string GenerateTransmittalNumber()
        {
            var count = _transmittals.Count + 1;
            return $"TX-{DateTime.UtcNow:yyyyMM}-{count:D4}";
        }

        /// <summary>
        /// Get all transmittals with optional filtering
        /// </summary>
        public List<Transmittal> GetTransmittals(TransmittalStatus? status = null)
        {
            lock (_lock)
            {
                var query = _transmittals.AsEnumerable();
                if (status.HasValue)
                    query = query.Where(t => t.Status == status.Value);
                return query.OrderByDescending(t => t.CreatedDate).ToList();
            }
        }

        #endregion

        #region Approvals

        /// <summary>
        /// Request approval for a document
        /// </summary>
        public DocumentApproval RequestApproval(ApprovalRequest request)
        {
            lock (_lock)
            {
                if (!_documents.TryGetValue(request.DocumentId, out var document))
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");

                var approval = new DocumentApproval
                {
                    ApprovalId = Guid.NewGuid().ToString(),
                    DocumentId = request.DocumentId,
                    RequestedBy = request.RequestedBy,
                    RequestedDate = DateTime.UtcNow,
                    Approvers = request.Approvers.Select(a => new ApproverEntry
                    {
                        ApproverId = a,
                        Status = ApprovalStatus.Pending
                    }).ToList(),
                    DueDate = request.DueDate,
                    Comments = request.Comments
                };

                _approvals[approval.ApprovalId] = approval;
                document.Status = DocumentStatus.PendingReview;
                document.RequiresApproval = true;

                AddAuditEntry(document.DocumentId, AuditAction.ApprovalRequested,
                    request.RequestedBy, $"Approval requested from: {string.Join(", ", request.Approvers)}");

                ApprovalRequired?.Invoke(this, new ApprovalEventArgs(document));

                return approval;
            }
        }

        /// <summary>
        /// Submit an approval decision
        /// </summary>
        public void SubmitApproval(string approvalId, string approverId, bool approved, string comments)
        {
            lock (_lock)
            {
                if (!_approvals.TryGetValue(approvalId, out var approval))
                    throw new KeyNotFoundException($"Approval {approvalId} not found");

                var approver = approval.Approvers.FirstOrDefault(a => a.ApproverId == approverId);
                if (approver == null)
                    throw new InvalidOperationException($"{approverId} is not an approver for this document");

                approver.Status = approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
                approver.DecisionDate = DateTime.UtcNow;
                approver.Comments = comments;

                if (_documents.TryGetValue(approval.DocumentId, out var document))
                {
                    // Check if all approvers have responded
                    if (approval.Approvers.All(a => a.Status != ApprovalStatus.Pending))
                    {
                        var allApproved = approval.Approvers.All(a => a.Status == ApprovalStatus.Approved);
                        document.Status = allApproved ? DocumentStatus.Approved : DocumentStatus.Rejected;

                        AddAuditEntry(document.DocumentId,
                            allApproved ? AuditAction.Approved : AuditAction.Rejected,
                            approverId, comments);
                    }
                    else
                    {
                        AddAuditEntry(document.DocumentId,
                            approved ? AuditAction.Approved : AuditAction.Rejected,
                            approverId, $"Partial approval: {comments}");
                    }
                }
            }
        }

        #endregion

        #region Audit Trail

        private void AddAuditEntry(string documentId, AuditAction action, string user, string description)
        {
            _auditTrail.Add(new DocumentAuditEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                DocumentId = documentId,
                Action = action,
                User = user,
                Timestamp = DateTime.UtcNow,
                Description = description
            });
        }

        /// <summary>
        /// Get audit trail for a document
        /// </summary>
        public List<DocumentAuditEntry> GetAuditTrail(string documentId)
        {
            lock (_lock)
            {
                return _auditTrail
                    .Where(a => a.DocumentId == documentId)
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Get full audit trail with optional filtering
        /// </summary>
        public List<DocumentAuditEntry> GetFullAuditTrail(DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (_lock)
            {
                var query = _auditTrail.AsEnumerable();

                if (fromDate.HasValue)
                    query = query.Where(a => a.Timestamp >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(a => a.Timestamp <= toDate.Value);

                return query.OrderByDescending(a => a.Timestamp).ToList();
            }
        }

        #endregion

        #region Statistics & Reporting

        /// <summary>
        /// Get document control statistics
        /// </summary>
        public DocumentControlStats GetStatistics()
        {
            lock (_lock)
            {
                return new DocumentControlStats
                {
                    TotalDocuments = _documents.Count,
                    DocumentsByStatus = _documents.Values
                        .GroupBy(d => d.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    DocumentsByType = _documents.Values
                        .GroupBy(d => d.DocumentType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    DocumentsByDiscipline = _documents.Values
                        .GroupBy(d => d.Discipline)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TotalTransmittals = _transmittals.Count,
                    PendingApprovals = _approvals.Values
                        .Count(a => a.Approvers.Any(ap => ap.Status == ApprovalStatus.Pending)),
                    RecentActivity = _auditTrail
                        .OrderByDescending(a => a.Timestamp)
                        .Take(10)
                        .ToList()
                };
            }
        }

        /// <summary>
        /// Generate document register report
        /// </summary>
        public DocumentRegisterReport GenerateDocumentRegister(string discipline = null)
        {
            lock (_lock)
            {
                var query = _documents.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(discipline))
                {
                    if (Enum.TryParse<Discipline>(discipline, true, out var disc))
                        query = query.Where(d => d.Discipline == disc);
                }

                return new DocumentRegisterReport
                {
                    GeneratedDate = DateTime.UtcNow,
                    TotalDocuments = query.Count(),
                    Documents = query.Select(d => new DocumentRegisterEntry
                    {
                        DocumentNumber = d.DocumentNumber,
                        Title = d.Title,
                        Revision = d.CurrentRevision,
                        Status = d.Status.ToString(),
                        Discipline = d.Discipline.ToString(),
                        Type = d.DocumentType.ToString(),
                        LastModified = d.LastModifiedDate,
                        Author = d.CreatedBy
                    }).OrderBy(d => d.DocumentNumber).ToList()
                };
            }
        }

        #endregion
    }

    #region Data Models

    public class BIMDocument
    {
        public string DocumentId { get; set; }
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DocumentType DocumentType { get; set; }
        public Discipline Discipline { get; set; }
        public string Phase { get; set; }
        public DocumentStatus Status { get; set; }
        public string CurrentRevision { get; set; }
        public int RevisionNumber { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public bool RequiresApproval { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public List<string> Tags { get; set; }
    }

    public class DocumentVersion
    {
        public string VersionId { get; set; }
        public string DocumentId { get; set; }
        public string Revision { get; set; }
        public int RevisionNumber { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; }
        public string ChangesSummary { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
    }

    public class Transmittal
    {
        public string TransmittalId { get; set; }
        public string TransmittalNumber { get; set; }
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public TransmittalPurpose Purpose { get; set; }
        public TransmittalPriority Priority { get; set; }
        public string FromCompany { get; set; }
        public string From { get => FromCompany; set => FromCompany = value; }
        public string ToCompany { get; set; }
        public string ToContact { get; set; }
        public List<string> To { get; set; }
        public List<string> CcContacts { get; set; }
        public List<string> CC { get => CcContacts; set => CcContacts = value; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string IssuedBy { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? SentDate { get; set; }
        public string SentBy { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string ReceivedBy { get; set; }
        public TransmittalStatus Status { get; set; }
        public List<TransmittalDocument> Documents { get; set; }
        public string Notes { get; set; }
    }

    public class TransmittalDocument
    {
        public string DocumentId { get; set; }
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string Revision { get; set; }
        public TransmittalPurpose Purpose { get; set; }
        public int Copies { get; set; }
    }

    public class DocumentApproval
    {
        public string ApprovalId { get; set; }
        public string DocumentId { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedDate { get; set; }
        public List<ApproverEntry> Approvers { get; set; }
        public DateTime? DueDate { get; set; }
        public string Comments { get; set; }
    }

    public class ApproverEntry
    {
        public string ApproverId { get; set; }
        public ApprovalStatus Status { get; set; }
        public DateTime? DecisionDate { get; set; }
        public string Comments { get; set; }
    }

    public class DocumentAuditEntry
    {
        public string EntryId { get; set; }
        public string DocumentId { get; set; }
        public AuditAction Action { get; set; }
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
    }

    public class DocumentControlStats
    {
        public int TotalDocuments { get; set; }
        public Dictionary<DocumentStatus, int> DocumentsByStatus { get; set; }
        public Dictionary<DocumentType, int> DocumentsByType { get; set; }
        public Dictionary<Discipline, int> DocumentsByDiscipline { get; set; }
        public int TotalTransmittals { get; set; }
        public int PendingApprovals { get; set; }
        public List<DocumentAuditEntry> RecentActivity { get; set; }
    }

    public class DocumentRegisterReport
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalDocuments { get; set; }
        public List<DocumentRegisterEntry> Documents { get; set; }
    }

    public class DocumentRegisterEntry
    {
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string Revision { get; set; }
        public string Status { get; set; }
        public string Discipline { get; set; }
        public string Type { get; set; }
        public DateTime LastModified { get; set; }
        public string Author { get; set; }
    }

    #endregion

    #region Request/Response Models

    public class DocumentRegistrationRequest
    {
        public string DocumentNumber { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DocumentType DocumentType { get; set; }
        public Discipline Discipline { get; set; }
        public string Phase { get; set; }
        public string Author { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public List<string> Tags { get; set; }
    }

    public class RevisionRequest
    {
        public string DocumentId { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string ChangesSummary { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public string IssuedBy { get; set; }
        public List<string> CloudMarks { get; set; }
    }

    public class DocumentSearchCriteria
    {
        public string SearchText { get; set; }
        public DocumentType? DocumentType { get; set; }
        public Discipline? Discipline { get; set; }
        public DocumentStatus? Status { get; set; }
        public string Phase { get; set; }
        public List<string> Tags { get; set; }
        public DateTime? ModifiedAfter { get; set; }
        public DateTime? ModifiedBefore { get; set; }
    }

    public class TransmittalRequest
    {
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public TransmittalPurpose Purpose { get; set; }
        public TransmittalPriority Priority { get; set; }
        public string FromCompany { get; set; }
        public string From { get => FromCompany; set => FromCompany = value; }
        public string ToCompany { get; set; }
        public string ToContact { get; set; }
        public List<string> To { get; set; }
        public List<string> CcContacts { get; set; }
        public List<string> CC { get => CcContacts; set => CcContacts = value; }
        public string CreatedBy { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> DocumentIds { get; set; }
        public List<TransmittalDocument> Documents { get; set; }
        public string Notes { get; set; }
    }

    public class ApprovalRequest
    {
        public string DocumentId { get; set; }
        public string RequestedBy { get; set; }
        public List<string> Approvers { get; set; }
        public DateTime? DueDate { get; set; }
        public string Comments { get; set; }
        public string ApprovedBy { get; set; }
        public ApprovalDecision Decision { get; set; }
    }

    #endregion

    #region Event Args

    public class DocumentEventArgs : EventArgs
    {
        public BIMDocument Document { get; }
        public string DocumentType { get; set; }
        public string DocumentId { get; set; }
        public string Message { get; set; }
        public string NewStatus { get; set; }
        public DocumentEventArgs() { }
        public DocumentEventArgs(BIMDocument document) => Document = document;
    }

    public class TransmittalEventArgs : EventArgs
    {
        public Transmittal Transmittal { get; }
        public TransmittalEventArgs(Transmittal transmittal) => Transmittal = transmittal;
    }

    public class ApprovalEventArgs : EventArgs
    {
        public BIMDocument Document { get; }
        public ApprovalEventArgs(BIMDocument document) => Document = document;
    }

    #endregion

    #region Enums

    public enum DocumentType
    {
        Drawing,
        Model,
        Specification,
        Report,
        Schedule,
        Calculation,
        Correspondence,
        SubmittalData,
        AsBuilt,
        OperationManual,
        MaintenanceManual,
        Photograph,
        RFI,
        ChangeOrder,
        ClashReport,
        CostEstimate
    }

    public enum Discipline
    {
        Architectural,
        Structural,
        Mechanical,
        Electrical,
        Plumbing,
        FireProtection,
        Civil,
        Landscape,
        Interior,
        Facade,
        MultiDiscipline
    }

    public enum DocumentStatus
    {
        Draft,
        PendingReview,
        Approved,
        Rejected,
        Issued,
        Superseded,
        Archived
    }

    public enum TransmittalPurpose
    {
        ForApproval,
        ForReview,
        ForInformation,
        ForConstruction,
        AsRequested,
        ForRecord
    }

    public enum TransmittalStatus
    {
        Draft,
        Issued,
        Acknowledged,
        Closed,
        Sent,
        Received
    }

    public enum TransmittalPriority
    {
        Normal,
        Urgent
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        ApprovedWithComments
    }

    public enum AuditAction
    {
        Created,
        Revised,
        ApprovalRequested,
        Approved,
        Rejected,
        Transmitted,
        Issued,
        Superseded,
        Archived,
        Downloaded,
        Viewed
    }

    #endregion
}
