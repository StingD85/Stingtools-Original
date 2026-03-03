// ============================================================================
// StingBIM AI - Document Control Center
// Comprehensive document management, RFI, submittals, and transmittals
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Intelligence.DocumentControl
{
    /// <summary>
    /// Document Control Center for BIM projects managing drawings,
    /// RFIs, submittals, transmittals, and document workflows.
    /// </summary>
    public sealed class DocumentControlCenter
    {
        private static readonly Lazy<DocumentControlCenter> _instance =
            new Lazy<DocumentControlCenter>(() => new DocumentControlCenter());
        public static DocumentControlCenter Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, Document> _documents = new();
        private readonly Dictionary<string, DrawingRegister> _drawingRegisters = new();
        private readonly Dictionary<string, RFI> _rfis = new();
        private readonly Dictionary<string, Submittal> _submittals = new();
        private readonly Dictionary<string, Transmittal> _transmittals = new();
        private readonly Dictionary<string, ChangeOrder> _changeOrders = new();
        private readonly List<DocumentWorkflow> _workflows = new();

        public event EventHandler<DocumentEventArgs> DocumentStatusChanged;
        public event EventHandler<DocumentEventArgs> DeadlineApproaching;

        private DocumentControlCenter()
        {
            InitializeWorkflows();
        }

        #region Initialization

        private void InitializeWorkflows()
        {
            _workflows.AddRange(new[]
            {
                new DocumentWorkflow
                {
                    WorkflowId = "WF-DOC",
                    Name = "Document Review",
                    Type = WorkflowType.Document,
                    Stages = new List<WorkflowStage>
                    {
                        new WorkflowStage { StageId = "S1", Name = "Draft", Order = 1 },
                        new WorkflowStage { StageId = "S2", Name = "Internal Review", Order = 2 },
                        new WorkflowStage { StageId = "S3", Name = "For Comment", Order = 3 },
                        new WorkflowStage { StageId = "S4", Name = "For Approval", Order = 4 },
                        new WorkflowStage { StageId = "S5", Name = "Approved", Order = 5, IsFinal = true },
                        new WorkflowStage { StageId = "S6", Name = "Superseded", Order = 6, IsFinal = true }
                    }
                },
                new DocumentWorkflow
                {
                    WorkflowId = "WF-RFI",
                    Name = "RFI Process",
                    Type = WorkflowType.RFI,
                    Stages = new List<WorkflowStage>
                    {
                        new WorkflowStage { StageId = "R1", Name = "Draft", Order = 1 },
                        new WorkflowStage { StageId = "R2", Name = "Submitted", Order = 2 },
                        new WorkflowStage { StageId = "R3", Name = "Under Review", Order = 3 },
                        new WorkflowStage { StageId = "R4", Name = "Answered", Order = 4 },
                        new WorkflowStage { StageId = "R5", Name = "Closed", Order = 5, IsFinal = true }
                    }
                },
                new DocumentWorkflow
                {
                    WorkflowId = "WF-SUB",
                    Name = "Submittal Process",
                    Type = WorkflowType.Submittal,
                    Stages = new List<WorkflowStage>
                    {
                        new WorkflowStage { StageId = "SB1", Name = "Pending", Order = 1 },
                        new WorkflowStage { StageId = "SB2", Name = "Submitted", Order = 2 },
                        new WorkflowStage { StageId = "SB3", Name = "Under Review", Order = 3 },
                        new WorkflowStage { StageId = "SB4", Name = "Approved", Order = 4, IsFinal = true },
                        new WorkflowStage { StageId = "SB5", Name = "Approved as Noted", Order = 5, IsFinal = true },
                        new WorkflowStage { StageId = "SB6", Name = "Revise and Resubmit", Order = 6 },
                        new WorkflowStage { StageId = "SB7", Name = "Rejected", Order = 7, IsFinal = true }
                    }
                }
            });
        }

        #endregion

        #region Drawing Register

        /// <summary>
        /// Create a drawing register for a project
        /// </summary>
        public DrawingRegister CreateDrawingRegister(DrawingRegisterRequest request)
        {
            var register = new DrawingRegister
            {
                RegisterId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                CreatedDate = DateTime.UtcNow,
                Drawings = new List<Drawing>(),
                NamingConvention = request.NamingConvention ?? GetDefaultNamingConvention()
            };

            lock (_lock)
            {
                _drawingRegisters[register.RegisterId] = register;
            }

            return register;
        }

        private DrawingNamingConvention GetDefaultNamingConvention()
        {
            return new DrawingNamingConvention
            {
                Pattern = "{Project}-{Discipline}-{Type}-{Level}-{Sequence}",
                Fields = new Dictionary<string, List<string>>
                {
                    { "Discipline", new List<string> { "AR", "ST", "ME", "EL", "PL", "FP", "CV" } },
                    { "Type", new List<string> { "P", "E", "S", "D", "SC" } } // Plan, Elevation, Section, Detail, Schedule
                }
            };
        }

        /// <summary>
        /// Add drawing to register
        /// </summary>
        public Drawing AddDrawing(string registerId, DrawingRequest request)
        {
            var drawing = new Drawing
            {
                DrawingId = Guid.NewGuid().ToString(),
                DrawingNumber = request.DrawingNumber,
                Title = request.Title,
                Discipline = request.Discipline,
                DrawingType = request.DrawingType,
                Level = request.Level,
                Zone = request.Zone,
                Scale = request.Scale,
                SheetSize = request.SheetSize,
                CurrentRevision = "A",
                Status = DrawingStatus.WorkInProgress,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                Revisions = new List<DrawingRevision>
                {
                    new DrawingRevision
                    {
                        RevisionId = Guid.NewGuid().ToString(),
                        Revision = "A",
                        Description = "Initial Issue",
                        Date = DateTime.UtcNow,
                        IssuedBy = request.CreatedBy
                    }
                }
            };

            lock (_lock)
            {
                if (_drawingRegisters.TryGetValue(registerId, out var register))
                {
                    register.Drawings.Add(drawing);
                    register.LastUpdated = DateTime.UtcNow;
                }
            }

            return drawing;
        }

        /// <summary>
        /// Issue new revision
        /// </summary>
        public DrawingRevision IssueRevision(string registerId, string drawingId, RevisionRequest request)
        {
            lock (_lock)
            {
                if (_drawingRegisters.TryGetValue(registerId, out var register))
                {
                    var drawing = register.Drawings.FirstOrDefault(d => d.DrawingId == drawingId);
                    if (drawing != null)
                    {
                        var newRev = GetNextRevision(drawing.CurrentRevision);
                        var revision = new DrawingRevision
                        {
                            RevisionId = Guid.NewGuid().ToString(),
                            Revision = newRev,
                            Description = request.Description,
                            Date = DateTime.UtcNow,
                            IssuedBy = request.IssuedBy,
                            CloudMarks = request.CloudMarks
                        };

                        drawing.Revisions.Add(revision);
                        drawing.CurrentRevision = newRev;
                        drawing.LastUpdated = DateTime.UtcNow;

                        return revision;
                    }
                }
            }

            return null;
        }

        private string GetNextRevision(string current)
        {
            if (string.IsNullOrEmpty(current))
                return "A";

            char lastChar = current[current.Length - 1];
            if (lastChar == 'Z')
                return current + "A";

            return current.Substring(0, current.Length - 1) + (char)(lastChar + 1);
        }

        /// <summary>
        /// Get drawing register summary
        /// </summary>
        public DrawingRegisterSummary GetRegisterSummary(string registerId)
        {
            lock (_lock)
            {
                if (!_drawingRegisters.TryGetValue(registerId, out var register))
                    return null;

                return new DrawingRegisterSummary
                {
                    RegisterId = registerId,
                    TotalDrawings = register.Drawings.Count,
                    ByDiscipline = register.Drawings.GroupBy(d => d.Discipline)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByStatus = register.Drawings.GroupBy(d => d.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    RecentRevisions = register.Drawings
                        .SelectMany(d => d.Revisions)
                        .OrderByDescending(r => r.Date)
                        .Take(10)
                        .ToList()
                };
            }
        }

        #endregion

        #region RFI Management

        /// <summary>
        /// Create an RFI
        /// </summary>
        public RFI CreateRFI(RFIRequest request)
        {
            var rfi = new RFI
            {
                RFIId = $"RFI-{DateTime.UtcNow:yyyyMMdd}-{_rfis.Count + 1:D4}",
                ProjectId = request.ProjectId,
                Subject = request.Subject,
                Question = request.Question,
                Priority = request.Priority,
                Discipline = request.Discipline,
                SpecSection = request.SpecSection,
                DrawingReference = request.DrawingReference,
                RelatedElementIds = request.RelatedElementIds ?? new List<string>(),
                SubmittedBy = request.SubmittedBy,
                SubmittedDate = DateTime.UtcNow,
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(7),
                Status = RFIStatus.Open,
                CostImpact = request.CostImpact,
                ScheduleImpact = request.ScheduleImpact,
                Attachments = request.Attachments ?? new List<Attachment>(),
                Responses = new List<RFIResponse>()
            };

            lock (_lock)
            {
                _rfis[rfi.RFIId] = rfi;
            }

            // Check deadline
            if ((rfi.DueDate - DateTime.UtcNow).TotalDays <= 2)
            {
                DeadlineApproaching?.Invoke(this, new DocumentEventArgs
                {
                    DocumentType = "RFI",
                    DocumentId = rfi.RFIId,
                    Message = $"RFI {rfi.RFIId} due in {(rfi.DueDate - DateTime.UtcNow).Days} days"
                });
            }

            return rfi;
        }

        /// <summary>
        /// Add response to RFI
        /// </summary>
        public RFIResponse RespondToRFI(string rfiId, RFIResponseRequest request)
        {
            lock (_lock)
            {
                if (!_rfis.TryGetValue(rfiId, out var rfi))
                    throw new KeyNotFoundException($"RFI {rfiId} not found");

                var response = new RFIResponse
                {
                    ResponseId = Guid.NewGuid().ToString(),
                    ResponseBy = request.ResponseBy,
                    ResponseDate = DateTime.UtcNow,
                    Answer = request.Answer,
                    IsFinal = request.IsFinal,
                    Attachments = request.Attachments ?? new List<Attachment>()
                };

                rfi.Responses.Add(response);

                if (request.IsFinal)
                {
                    rfi.Status = RFIStatus.Answered;
                    rfi.AnsweredDate = DateTime.UtcNow;

                    DocumentStatusChanged?.Invoke(this, new DocumentEventArgs
                    {
                        DocumentType = "RFI",
                        DocumentId = rfiId,
                        NewStatus = "Answered"
                    });
                }

                return response;
            }
        }

        /// <summary>
        /// Close RFI
        /// </summary>
        public void CloseRFI(string rfiId, string closedBy, string notes = null)
        {
            lock (_lock)
            {
                if (_rfis.TryGetValue(rfiId, out var rfi))
                {
                    rfi.Status = RFIStatus.Closed;
                    rfi.ClosedDate = DateTime.UtcNow;
                    rfi.ClosedBy = closedBy;
                    rfi.ClosingNotes = notes;
                }
            }
        }

        /// <summary>
        /// Get RFI log
        /// </summary>
        public RFILog GetRFILog(string projectId)
        {
            lock (_lock)
            {
                var rfis = _rfis.Values.Where(r => r.ProjectId == projectId).ToList();

                return new RFILog
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.UtcNow,
                    TotalRFIs = rfis.Count,
                    OpenRFIs = rfis.Count(r => r.Status == RFIStatus.Open),
                    AnsweredRFIs = rfis.Count(r => r.Status == RFIStatus.Answered),
                    ClosedRFIs = rfis.Count(r => r.Status == RFIStatus.Closed),
                    OverdueRFIs = rfis.Count(r => r.DueDate < DateTime.UtcNow && r.Status == RFIStatus.Open),
                    AverageResponseTime = CalculateAverageResponseTime(rfis),
                    ByDiscipline = rfis.GroupBy(r => r.Discipline).ToDictionary(g => g.Key, g => g.Count()),
                    ByPriority = rfis.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count()),
                    RFIs = rfis.OrderByDescending(r => r.SubmittedDate).ToList()
                };
            }
        }

        private double CalculateAverageResponseTime(List<RFI> rfis)
        {
            var answeredRfis = rfis.Where(r => r.AnsweredDate.HasValue).ToList();
            if (!answeredRfis.Any())
                return 0;

            return answeredRfis.Average(r => (r.AnsweredDate.Value - r.SubmittedDate).TotalDays);
        }

        #endregion

        #region Submittal Management

        /// <summary>
        /// Create a submittal
        /// </summary>
        public Submittal CreateSubmittal(SubmittalRequest request)
        {
            var submittal = new Submittal
            {
                SubmittalId = $"SUB-{DateTime.UtcNow:yyyyMMdd}-{_submittals.Count + 1:D4}",
                ProjectId = request.ProjectId,
                SpecSection = request.SpecSection,
                Description = request.Description,
                Type = request.Type,
                Category = request.Category,
                SubmittedBy = request.SubmittedBy,
                Contractor = request.Contractor,
                SubmittedDate = DateTime.UtcNow,
                RequiredDate = request.RequiredDate,
                ReviewDueDate = request.ReviewDueDate ?? DateTime.UtcNow.AddDays(14),
                Status = SubmittalStatus.Pending,
                CurrentRevision = 1,
                Revisions = new List<SubmittalRevision>
                {
                    new SubmittalRevision
                    {
                        RevisionId = Guid.NewGuid().ToString(),
                        RevisionNumber = 1,
                        SubmittedDate = DateTime.UtcNow,
                        Attachments = request.Attachments ?? new List<Attachment>()
                    }
                },
                ReviewHistory = new List<SubmittalReview>()
            };

            lock (_lock)
            {
                _submittals[submittal.SubmittalId] = submittal;
            }

            return submittal;
        }

        /// <summary>
        /// Review submittal
        /// </summary>
        public SubmittalReview ReviewSubmittal(string submittalId, SubmittalReviewRequest request)
        {
            lock (_lock)
            {
                if (!_submittals.TryGetValue(submittalId, out var submittal))
                    throw new KeyNotFoundException($"Submittal {submittalId} not found");

                var review = new SubmittalReview
                {
                    ReviewId = Guid.NewGuid().ToString(),
                    ReviewedBy = request.ReviewedBy,
                    ReviewDate = DateTime.UtcNow,
                    Result = request.Result,
                    Comments = request.Comments,
                    MarkedUpDocument = request.MarkedUpDocument
                };

                submittal.ReviewHistory.Add(review);
                submittal.Status = request.Result switch
                {
                    ReviewResult.Approved => SubmittalStatus.Approved,
                    ReviewResult.ApprovedAsNoted => SubmittalStatus.ApprovedAsNoted,
                    ReviewResult.ReviseResubmit => SubmittalStatus.ReviseResubmit,
                    ReviewResult.Rejected => SubmittalStatus.Rejected,
                    _ => submittal.Status
                };

                DocumentStatusChanged?.Invoke(this, new DocumentEventArgs
                {
                    DocumentType = "Submittal",
                    DocumentId = submittalId,
                    NewStatus = submittal.Status.ToString()
                });

                return review;
            }
        }

        /// <summary>
        /// Resubmit submittal
        /// </summary>
        public SubmittalRevision ResubmitSubmittal(string submittalId, ResubmittalRequest request)
        {
            lock (_lock)
            {
                if (!_submittals.TryGetValue(submittalId, out var submittal))
                    throw new KeyNotFoundException($"Submittal {submittalId} not found");

                var newRevision = new SubmittalRevision
                {
                    RevisionId = Guid.NewGuid().ToString(),
                    RevisionNumber = submittal.CurrentRevision + 1,
                    SubmittedDate = DateTime.UtcNow,
                    ResponseToComments = request.ResponseToComments,
                    Attachments = request.Attachments ?? new List<Attachment>()
                };

                submittal.Revisions.Add(newRevision);
                submittal.CurrentRevision++;
                submittal.Status = SubmittalStatus.Resubmitted;

                return newRevision;
            }
        }

        /// <summary>
        /// Get submittal log
        /// </summary>
        public SubmittalLog GetSubmittalLog(string projectId)
        {
            lock (_lock)
            {
                var submittals = _submittals.Values.Where(s => s.ProjectId == projectId).ToList();

                return new SubmittalLog
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.UtcNow,
                    TotalSubmittals = submittals.Count,
                    Pending = submittals.Count(s => s.Status == SubmittalStatus.Pending),
                    UnderReview = submittals.Count(s => s.Status == SubmittalStatus.UnderReview),
                    Approved = submittals.Count(s => s.Status == SubmittalStatus.Approved ||
                        s.Status == SubmittalStatus.ApprovedAsNoted),
                    ReviseResubmit = submittals.Count(s => s.Status == SubmittalStatus.ReviseResubmit),
                    Rejected = submittals.Count(s => s.Status == SubmittalStatus.Rejected),
                    BySpecSection = submittals.GroupBy(s => s.SpecSection).ToDictionary(g => g.Key, g => g.Count()),
                    ByType = submittals.GroupBy(s => s.Type).ToDictionary(g => g.Key, g => g.Count()),
                    Submittals = submittals.OrderByDescending(s => s.SubmittedDate).ToList()
                };
            }
        }

        #endregion

        #region Transmittal Management

        /// <summary>
        /// Create a transmittal
        /// </summary>
        public Transmittal CreateTransmittal(TransmittalRequest request)
        {
            var transmittal = new Transmittal
            {
                TransmittalId = $"TRN-{DateTime.UtcNow:yyyyMMdd}-{_transmittals.Count + 1:D4}",
                ProjectId = request.ProjectId,
                Subject = request.Subject,
                From = request.From,
                To = request.To,
                CC = request.CC ?? new List<string>(),
                Purpose = request.Purpose,
                Priority = request.Priority,
                Documents = request.Documents ?? new List<TransmittalDocument>(),
                Notes = request.Notes,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                SentDate = null,
                Status = TransmittalStatus.Draft
            };

            lock (_lock)
            {
                _transmittals[transmittal.TransmittalId] = transmittal;
            }

            return transmittal;
        }

        /// <summary>
        /// Add documents to transmittal
        /// </summary>
        public void AddDocumentsToTransmittal(string transmittalId, List<TransmittalDocument> documents)
        {
            lock (_lock)
            {
                if (_transmittals.TryGetValue(transmittalId, out var transmittal))
                {
                    transmittal.Documents.AddRange(documents);
                }
            }
        }

        /// <summary>
        /// Send transmittal
        /// </summary>
        public void SendTransmittal(string transmittalId, string sentBy)
        {
            lock (_lock)
            {
                if (_transmittals.TryGetValue(transmittalId, out var transmittal))
                {
                    transmittal.SentDate = DateTime.UtcNow;
                    transmittal.SentBy = sentBy;
                    transmittal.Status = TransmittalStatus.Sent;

                    DocumentStatusChanged?.Invoke(this, new DocumentEventArgs
                    {
                        DocumentType = "Transmittal",
                        DocumentId = transmittalId,
                        NewStatus = "Sent"
                    });
                }
            }
        }

        /// <summary>
        /// Record transmittal receipt
        /// </summary>
        public void RecordReceipt(string transmittalId, string receivedBy)
        {
            lock (_lock)
            {
                if (_transmittals.TryGetValue(transmittalId, out var transmittal))
                {
                    transmittal.ReceivedDate = DateTime.UtcNow;
                    transmittal.ReceivedBy = receivedBy;
                    transmittal.Status = TransmittalStatus.Received;
                }
            }
        }

        #endregion

        #region Change Order Management

        /// <summary>
        /// Create a change order
        /// </summary>
        public ChangeOrder CreateChangeOrder(ChangeOrderRequest request)
        {
            var changeOrder = new ChangeOrder
            {
                ChangeOrderId = $"CO-{DateTime.UtcNow:yyyyMMdd}-{_changeOrders.Count + 1:D4}",
                ProjectId = request.ProjectId,
                Title = request.Title,
                Description = request.Description,
                Reason = request.Reason,
                InitiatedBy = request.InitiatedBy,
                InitiatedDate = DateTime.UtcNow,
                RelatedRFIs = request.RelatedRFIs ?? new List<string>(),
                AffectedDrawings = request.AffectedDrawings ?? new List<string>(),
                AffectedElementIds = request.AffectedElementIds ?? new List<string>(),
                CostImpact = request.CostImpact,
                ScheduleImpact = request.ScheduleImpact,
                Status = ChangeOrderStatus.Draft,
                Approvals = new List<ChangeOrderApproval>()
            };

            lock (_lock)
            {
                _changeOrders[changeOrder.ChangeOrderId] = changeOrder;
            }

            return changeOrder;
        }

        /// <summary>
        /// Submit change order for approval
        /// </summary>
        public void SubmitChangeOrder(string changeOrderId, string submittedBy)
        {
            lock (_lock)
            {
                if (_changeOrders.TryGetValue(changeOrderId, out var co))
                {
                    co.SubmittedDate = DateTime.UtcNow;
                    co.SubmittedBy = submittedBy;
                    co.Status = ChangeOrderStatus.Pending;
                }
            }
        }

        /// <summary>
        /// Approve or reject change order
        /// </summary>
        public ChangeOrderApproval ProcessChangeOrderApproval(string changeOrderId, ApprovalRequest request)
        {
            lock (_lock)
            {
                if (!_changeOrders.TryGetValue(changeOrderId, out var co))
                    throw new KeyNotFoundException($"Change order {changeOrderId} not found");

                var approval = new ChangeOrderApproval
                {
                    ApprovalId = Guid.NewGuid().ToString(),
                    ApprovedBy = request.ApprovedBy,
                    ApprovalDate = DateTime.UtcNow,
                    Decision = request.Decision,
                    Comments = request.Comments
                };

                co.Approvals.Add(approval);

                if (request.Decision == ApprovalDecision.Approved)
                {
                    co.Status = ChangeOrderStatus.Approved;
                    co.ApprovedDate = DateTime.UtcNow;
                }
                else if (request.Decision == ApprovalDecision.Rejected)
                {
                    co.Status = ChangeOrderStatus.Rejected;
                }

                return approval;
            }
        }

        /// <summary>
        /// Get change order log
        /// </summary>
        public ChangeOrderLog GetChangeOrderLog(string projectId)
        {
            lock (_lock)
            {
                var orders = _changeOrders.Values.Where(c => c.ProjectId == projectId).ToList();

                return new ChangeOrderLog
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.UtcNow,
                    TotalChangeOrders = orders.Count,
                    Pending = orders.Count(c => c.Status == ChangeOrderStatus.Pending),
                    Approved = orders.Count(c => c.Status == ChangeOrderStatus.Approved),
                    Rejected = orders.Count(c => c.Status == ChangeOrderStatus.Rejected),
                    TotalCostImpact = orders.Where(c => c.Status == ChangeOrderStatus.Approved)
                        .Sum(c => c.CostImpact),
                    TotalScheduleImpact = orders.Where(c => c.Status == ChangeOrderStatus.Approved)
                        .Sum(c => c.ScheduleImpact),
                    ChangeOrders = orders.OrderByDescending(c => c.InitiatedDate).ToList()
                };
            }
        }

        #endregion

        #region Document Search and Reporting

        /// <summary>
        /// Search documents
        /// </summary>
        public List<SearchResult> SearchDocuments(DocumentSearchRequest request)
        {
            var results = new List<SearchResult>();

            lock (_lock)
            {
                // Search drawings
                if (request.IncludeDrawings)
                {
                    foreach (var register in _drawingRegisters.Values.Where(r => r.ProjectId == request.ProjectId))
                    {
                        foreach (var drawing in register.Drawings)
                        {
                            if (MatchesSearch(drawing, request))
                            {
                                results.Add(new SearchResult
                                {
                                    DocumentType = "Drawing",
                                    DocumentId = drawing.DrawingId,
                                    Number = drawing.DrawingNumber,
                                    Title = drawing.Title,
                                    Status = drawing.Status.ToString(),
                                    Date = drawing.LastUpdated ?? drawing.CreatedDate
                                });
                            }
                        }
                    }
                }

                // Search RFIs
                if (request.IncludeRFIs)
                {
                    foreach (var rfi in _rfis.Values.Where(r => r.ProjectId == request.ProjectId))
                    {
                        if (MatchesSearch(rfi, request))
                        {
                            results.Add(new SearchResult
                            {
                                DocumentType = "RFI",
                                DocumentId = rfi.RFIId,
                                Number = rfi.RFIId,
                                Title = rfi.Subject,
                                Status = rfi.Status.ToString(),
                                Date = rfi.SubmittedDate
                            });
                        }
                    }
                }

                // Search Submittals
                if (request.IncludeSubmittals)
                {
                    foreach (var sub in _submittals.Values.Where(s => s.ProjectId == request.ProjectId))
                    {
                        if (MatchesSearch(sub, request))
                        {
                            results.Add(new SearchResult
                            {
                                DocumentType = "Submittal",
                                DocumentId = sub.SubmittalId,
                                Number = sub.SubmittalId,
                                Title = sub.Description,
                                Status = sub.Status.ToString(),
                                Date = sub.SubmittedDate
                            });
                        }
                    }
                }
            }

            return results.OrderByDescending(r => r.Date).ToList();
        }

        private bool MatchesSearch(Drawing drawing, DocumentSearchRequest request)
        {
            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                if (!drawing.Title.ToLower().Contains(term) &&
                    !drawing.DrawingNumber.ToLower().Contains(term))
                    return false;
            }

            if (!string.IsNullOrEmpty(request.Discipline) && drawing.Discipline != request.Discipline)
                return false;

            return true;
        }

        private bool MatchesSearch(RFI rfi, DocumentSearchRequest request)
        {
            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                if (!rfi.Subject.ToLower().Contains(term) &&
                    !rfi.Question.ToLower().Contains(term))
                    return false;
            }

            if (!string.IsNullOrEmpty(request.Discipline) && rfi.Discipline != request.Discipline)
                return false;

            return true;
        }

        private bool MatchesSearch(Submittal sub, DocumentSearchRequest request)
        {
            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                if (!sub.Description.ToLower().Contains(term))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Generate document control report
        /// </summary>
        public DocumentControlReport GenerateReport(string projectId)
        {
            lock (_lock)
            {
                return new DocumentControlReport
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.UtcNow,
                    DrawingSummary = GetDrawingSummary(projectId),
                    RFISummary = GetRFISummary(projectId),
                    SubmittalSummary = GetSubmittalSummary(projectId),
                    ChangeOrderSummary = GetChangeOrderSummary(projectId)
                };
            }
        }

        private object GetDrawingSummary(string projectId)
        {
            var drawings = _drawingRegisters.Values
                .Where(r => r.ProjectId == projectId)
                .SelectMany(r => r.Drawings);

            return new
            {
                Total = drawings.Count(),
                ByStatus = drawings.GroupBy(d => d.Status).ToDictionary(g => g.Key.ToString(), g => g.Count())
            };
        }

        private object GetRFISummary(string projectId)
        {
            var rfis = _rfis.Values.Where(r => r.ProjectId == projectId);
            return new
            {
                Total = rfis.Count(),
                Open = rfis.Count(r => r.Status == RFIStatus.Open),
                Overdue = rfis.Count(r => r.Status == RFIStatus.Open && r.DueDate < DateTime.UtcNow)
            };
        }

        private object GetSubmittalSummary(string projectId)
        {
            var subs = _submittals.Values.Where(s => s.ProjectId == projectId);
            return new
            {
                Total = subs.Count(),
                Pending = subs.Count(s => s.Status == SubmittalStatus.Pending),
                Approved = subs.Count(s => s.Status == SubmittalStatus.Approved)
            };
        }

        private object GetChangeOrderSummary(string projectId)
        {
            var orders = _changeOrders.Values.Where(c => c.ProjectId == projectId);
            return new
            {
                Total = orders.Count(),
                TotalCostImpact = orders.Where(c => c.Status == ChangeOrderStatus.Approved).Sum(c => c.CostImpact)
            };
        }

        #endregion
    }

    #region Data Models

    public class Document
    {
        public string DocumentId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string DocumentType { get; set; }
        public string Status { get; set; }
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; }
    }

    public class DrawingRegister
    {
        public string RegisterId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<Drawing> Drawings { get; set; }
        public DrawingNamingConvention NamingConvention { get; set; }
    }

    public class DrawingRegisterRequest
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DrawingNamingConvention NamingConvention { get; set; }
    }

    public class DrawingNamingConvention
    {
        public string Pattern { get; set; }
        public Dictionary<string, List<string>> Fields { get; set; }
    }

    public class Drawing
    {
        public string DrawingId { get; set; }
        public string DrawingNumber { get; set; }
        public string Title { get; set; }
        public string Discipline { get; set; }
        public string DrawingType { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string Scale { get; set; }
        public string SheetSize { get; set; }
        public string CurrentRevision { get; set; }
        public DrawingStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<DrawingRevision> Revisions { get; set; }
    }

    public class DrawingRequest
    {
        public string DrawingNumber { get; set; }
        public string Title { get; set; }
        public string Discipline { get; set; }
        public string DrawingType { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string Scale { get; set; }
        public string SheetSize { get; set; }
        public string CreatedBy { get; set; }
    }

    public class DrawingRevision
    {
        public string RevisionId { get; set; }
        public string Revision { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string IssuedBy { get; set; }
        public List<string> CloudMarks { get; set; }
    }

    public class DrawingRegisterSummary
    {
        public string RegisterId { get; set; }
        public int TotalDrawings { get; set; }
        public Dictionary<string, int> ByDiscipline { get; set; }
        public Dictionary<DrawingStatus, int> ByStatus { get; set; }
        public List<DrawingRevision> RecentRevisions { get; set; }
    }

    public class RFI
    {
        public string RFIId { get; set; }
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public string Question { get; set; }
        public RFIPriority Priority { get; set; }
        public string Discipline { get; set; }
        public string SpecSection { get; set; }
        public string DrawingReference { get; set; }
        public List<string> RelatedElementIds { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public RFIStatus Status { get; set; }
        public DateTime? AnsweredDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string ClosedBy { get; set; }
        public string ClosingNotes { get; set; }
        public bool CostImpact { get; set; }
        public bool ScheduleImpact { get; set; }
        public List<Attachment> Attachments { get; set; }
        public List<RFIResponse> Responses { get; set; }
    }

    public class RFIRequest
    {
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public string Question { get; set; }
        public RFIPriority Priority { get; set; }
        public string Discipline { get; set; }
        public string SpecSection { get; set; }
        public string DrawingReference { get; set; }
        public List<string> RelatedElementIds { get; set; }
        public string SubmittedBy { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public bool CostImpact { get; set; }
        public bool ScheduleImpact { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class RFIResponse
    {
        public string ResponseId { get; set; }
        public string ResponseBy { get; set; }
        public DateTime ResponseDate { get; set; }
        public string Answer { get; set; }
        public bool IsFinal { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class RFIResponseRequest
    {
        public string ResponseBy { get; set; }
        public string Answer { get; set; }
        public bool IsFinal { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class RFILog
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalRFIs { get; set; }
        public int OpenRFIs { get; set; }
        public int AnsweredRFIs { get; set; }
        public int ClosedRFIs { get; set; }
        public int OverdueRFIs { get; set; }
        public double AverageResponseTime { get; set; }
        public Dictionary<string, int> ByDiscipline { get; set; }
        public Dictionary<RFIPriority, int> ByPriority { get; set; }
        public List<RFI> RFIs { get; set; }
    }

    public class Submittal
    {
        public string SubmittalId { get; set; }
        public string ProjectId { get; set; }
        public string SpecSection { get; set; }
        public string Description { get; set; }
        public SubmittalType Type { get; set; }
        public string Category { get; set; }
        public string SubmittedBy { get; set; }
        public string Contractor { get; set; }
        public DateTime SubmittedDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime ReviewDueDate { get; set; }
        public SubmittalStatus Status { get; set; }
        public int CurrentRevision { get; set; }
        public List<SubmittalRevision> Revisions { get; set; }
        public List<SubmittalReview> ReviewHistory { get; set; }
    }

    public class SubmittalRequest
    {
        public string ProjectId { get; set; }
        public string SpecSection { get; set; }
        public string Description { get; set; }
        public SubmittalType Type { get; set; }
        public string Category { get; set; }
        public string SubmittedBy { get; set; }
        public string Contractor { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime? ReviewDueDate { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class SubmittalRevision
    {
        public string RevisionId { get; set; }
        public int RevisionNumber { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string ResponseToComments { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class SubmittalReview
    {
        public string ReviewId { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime ReviewDate { get; set; }
        public ReviewResult Result { get; set; }
        public string Comments { get; set; }
        public string MarkedUpDocument { get; set; }
    }

    public class SubmittalReviewRequest
    {
        public string ReviewedBy { get; set; }
        public ReviewResult Result { get; set; }
        public string Comments { get; set; }
        public string MarkedUpDocument { get; set; }
    }

    public class ResubmittalRequest
    {
        public string ResponseToComments { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class SubmittalLog
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalSubmittals { get; set; }
        public int Pending { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int ReviseResubmit { get; set; }
        public int Rejected { get; set; }
        public Dictionary<string, int> BySpecSection { get; set; }
        public Dictionary<SubmittalType, int> ByType { get; set; }
        public List<Submittal> Submittals { get; set; }
    }

    public class ChangeOrder
    {
        public string ChangeOrderId { get; set; }
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ChangeOrderReason Reason { get; set; }
        public string InitiatedBy { get; set; }
        public DateTime InitiatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public List<string> RelatedRFIs { get; set; }
        public List<string> AffectedDrawings { get; set; }
        public List<string> AffectedElementIds { get; set; }
        public decimal CostImpact { get; set; }
        public int ScheduleImpact { get; set; }
        public ChangeOrderStatus Status { get; set; }
        public List<ChangeOrderApproval> Approvals { get; set; }
    }

    public class ChangeOrderRequest
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ChangeOrderReason Reason { get; set; }
        public string InitiatedBy { get; set; }
        public List<string> RelatedRFIs { get; set; }
        public List<string> AffectedDrawings { get; set; }
        public List<string> AffectedElementIds { get; set; }
        public decimal CostImpact { get; set; }
        public int ScheduleImpact { get; set; }
    }

    public class ChangeOrderApproval
    {
        public string ApprovalId { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime ApprovalDate { get; set; }
        public ApprovalDecision Decision { get; set; }
        public string Comments { get; set; }
    }

    public class ChangeOrderLog
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalChangeOrders { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public decimal TotalCostImpact { get; set; }
        public int TotalScheduleImpact { get; set; }
        public List<ChangeOrder> ChangeOrders { get; set; }
    }

    public class Attachment
    {
        public string AttachmentId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public class DocumentWorkflow
    {
        public string WorkflowId { get; set; }
        public string Name { get; set; }
        public WorkflowType Type { get; set; }
        public List<WorkflowStage> Stages { get; set; }
    }

    public class WorkflowStage
    {
        public string StageId { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public bool IsFinal { get; set; }
    }

    public class DocumentSearchRequest
    {
        public string ProjectId { get; set; }
        public string SearchTerm { get; set; }
        public string Discipline { get; set; }
        public bool IncludeDrawings { get; set; } = true;
        public bool IncludeRFIs { get; set; } = true;
        public bool IncludeSubmittals { get; set; } = true;
    }

    public class SearchResult
    {
        public string DocumentType { get; set; }
        public string DocumentId { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }
    }

    public class DocumentControlReport
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public object DrawingSummary { get; set; }
        public object RFISummary { get; set; }
        public object SubmittalSummary { get; set; }
        public object ChangeOrderSummary { get; set; }
    }

    #endregion

    #region Enums

    public enum DrawingStatus { WorkInProgress, ForReview, Approved, Issued, Superseded }
    public enum RFIStatus { Open, Answered, Closed }
    public enum RFIPriority { Low, Normal, High, Urgent }
    public enum SubmittalStatus { Pending, Submitted, UnderReview, Approved, ApprovedAsNoted, ReviseResubmit, Resubmitted, Rejected }
    public enum SubmittalType { ShopDrawing, ProductData, Sample, MockUp, Certificate, Other }
    public enum ReviewResult { Approved, ApprovedAsNoted, ReviseResubmit, Rejected }
    public enum ChangeOrderStatus { Draft, Pending, Approved, Rejected, Implemented }
    public enum ChangeOrderReason { OwnerRequest, DesignError, SiteCondition, ValueEngineering, Regulatory, Other }
    public enum ApprovalDecision { Approved, Rejected, DeferredForReview }
    public enum WorkflowType { Document, RFI, Submittal, ChangeOrder }

    #endregion
}
