// ============================================================================
// StingBIM AI - Field Operations Module
// Inspection tracking, punch lists, progress capture, and site management
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.FieldOperations
{
    /// <summary>
    /// Field Operations Module for construction site management including
    /// inspections, punch lists, progress tracking, and quality capture.
    /// </summary>
    public sealed class FieldOperationsModule
    {
        private static readonly Lazy<FieldOperationsModule> _instance =
            new Lazy<FieldOperationsModule>(() => new FieldOperationsModule());
        public static FieldOperationsModule Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, Inspection> _inspections = new();
        private readonly Dictionary<string, PunchItem> _punchItems = new();
        private readonly Dictionary<string, DailyReport> _dailyReports = new();
        private readonly Dictionary<string, SafetyObservation> _safetyObservations = new();
        private readonly Dictionary<string, ProgressCapture> _progressCaptures = new();
        private readonly Dictionary<string, QualityRecord> _qualityRecords = new();
        private readonly List<ChecklistTemplate> _checklistTemplates = new();

        public event EventHandler<FieldEventArgs> InspectionCompleted;
        public event EventHandler<FieldEventArgs> PunchItemCreated;
        public event EventHandler<FieldEventArgs> SafetyIssueReported;

        private FieldOperationsModule()
        {
            InitializeDefaultChecklists();
        }

        #region Initialization

        private void InitializeDefaultChecklists()
        {
            _checklistTemplates.Add(new ChecklistTemplate
            {
                TemplateId = "STRUCT-CONCRETE",
                Name = "Concrete Placement Inspection",
                Category = "Structural",
                Items = new List<ChecklistItem>
                {
                    new() { ItemId = "1", Description = "Forms properly braced and aligned", Required = true },
                    new() { ItemId = "2", Description = "Reinforcement placed per drawings", Required = true },
                    new() { ItemId = "3", Description = "Cover blocks/chairs in place", Required = true },
                    new() { ItemId = "4", Description = "Embedded items positioned correctly", Required = true },
                    new() { ItemId = "5", Description = "Forms clean and free of debris", Required = true },
                    new() { ItemId = "6", Description = "Concrete mix design approved", Required = true },
                    new() { ItemId = "7", Description = "Weather conditions acceptable", Required = true },
                    new() { ItemId = "8", Description = "Slump test performed", Required = true },
                    new() { ItemId = "9", Description = "Test cylinders taken", Required = true },
                    new() { ItemId = "10", Description = "Vibration adequate", Required = false }
                }
            });

            _checklistTemplates.Add(new ChecklistTemplate
            {
                TemplateId = "MEP-ABOVE-CEILING",
                Name = "Above Ceiling MEP Inspection",
                Category = "MEP",
                Items = new List<ChecklistItem>
                {
                    new() { ItemId = "1", Description = "Ductwork properly supported", Required = true },
                    new() { ItemId = "2", Description = "Piping properly supported and insulated", Required = true },
                    new() { ItemId = "3", Description = "Electrical conduit properly supported", Required = true },
                    new() { ItemId = "4", Description = "Fire/smoke dampers installed", Required = true },
                    new() { ItemId = "5", Description = "Firestopping complete at penetrations", Required = true },
                    new() { ItemId = "6", Description = "Access panels provided where required", Required = true },
                    new() { ItemId = "7", Description = "Balancing dampers accessible", Required = false },
                    new() { ItemId = "8", Description = "Light fixtures properly supported", Required = true },
                    new() { ItemId = "9", Description = "No conflicts between trades", Required = true },
                    new() { ItemId = "10", Description = "Clearances maintained per code", Required = true }
                }
            });

            _checklistTemplates.Add(new ChecklistTemplate
            {
                TemplateId = "SAFETY-DAILY",
                Name = "Daily Safety Inspection",
                Category = "Safety",
                Items = new List<ChecklistItem>
                {
                    new() { ItemId = "1", Description = "PPE being worn by all workers", Required = true },
                    new() { ItemId = "2", Description = "Scaffolding properly erected and tagged", Required = true },
                    new() { ItemId = "3", Description = "Fall protection in place", Required = true },
                    new() { ItemId = "4", Description = "Excavations properly shored", Required = true },
                    new() { ItemId = "5", Description = "Fire extinguishers accessible", Required = true },
                    new() { ItemId = "6", Description = "First aid kit available", Required = true },
                    new() { ItemId = "7", Description = "Emergency exits clear", Required = true },
                    new() { ItemId = "8", Description = "Housekeeping acceptable", Required = true },
                    new() { ItemId = "9", Description = "Electrical cords in good condition", Required = true },
                    new() { ItemId = "10", Description = "Material storage proper", Required = true }
                }
            });

            _checklistTemplates.Add(new ChecklistTemplate
            {
                TemplateId = "WATERPROOF",
                Name = "Waterproofing Inspection",
                Category = "Envelope",
                Items = new List<ChecklistItem>
                {
                    new() { ItemId = "1", Description = "Substrate clean and dry", Required = true },
                    new() { ItemId = "2", Description = "Primer applied where required", Required = true },
                    new() { ItemId = "3", Description = "Membrane properly lapped", Required = true },
                    new() { ItemId = "4", Description = "Corners and penetrations detailed", Required = true },
                    new() { ItemId = "5", Description = "Protection board installed", Required = true },
                    new() { ItemId = "6", Description = "Drainage provisions correct", Required = true },
                    new() { ItemId = "7", Description = "Terminations properly detailed", Required = true },
                    new() { ItemId = "8", Description = "Flood test performed (if applicable)", Required = false }
                }
            });
        }

        #endregion

        #region Inspections

        /// <summary>
        /// Create a new inspection
        /// </summary>
        public Inspection CreateInspection(InspectionRequest request)
        {
            var template = _checklistTemplates.FirstOrDefault(t => t.TemplateId == request.TemplateId);

            var inspection = new Inspection
            {
                InspectionId = $"INS-{DateTime.UtcNow:yyyyMMdd}-{_inspections.Count + 1:D4}",
                Title = request.Title,
                Type = request.Type,
                TemplateId = request.TemplateId,
                Location = request.Location,
                Level = request.Level,
                Zone = request.Zone,
                ElementIds = request.ElementIds ?? new List<string>(),
                ScheduledDate = request.ScheduledDate,
                Inspector = request.Inspector,
                Status = InspectionStatus.Scheduled,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                ChecklistItems = template?.Items.Select(i => new InspectionChecklistItem
                {
                    ItemId = i.ItemId,
                    Description = i.Description,
                    Required = i.Required,
                    Status = ChecklistItemStatus.NotChecked
                }).ToList() ?? new List<InspectionChecklistItem>(),
                Photos = new List<InspectionPhoto>(),
                Notes = new List<InspectionNote>()
            };

            lock (_lock)
            {
                _inspections[inspection.InspectionId] = inspection;
            }

            return inspection;
        }

        /// <summary>
        /// Start an inspection
        /// </summary>
        public void StartInspection(string inspectionId, string inspector)
        {
            lock (_lock)
            {
                if (_inspections.TryGetValue(inspectionId, out var inspection))
                {
                    inspection.Status = InspectionStatus.InProgress;
                    inspection.StartedDate = DateTime.UtcNow;
                    inspection.Inspector = inspector;
                }
            }
        }

        /// <summary>
        /// Update checklist item status
        /// </summary>
        public void UpdateChecklistItem(string inspectionId, string itemId, ChecklistItemStatus status,
            string notes = null, List<string> photoIds = null)
        {
            lock (_lock)
            {
                if (_inspections.TryGetValue(inspectionId, out var inspection))
                {
                    var item = inspection.ChecklistItems.FirstOrDefault(i => i.ItemId == itemId);
                    if (item != null)
                    {
                        item.Status = status;
                        item.Notes = notes;
                        item.PhotoIds = photoIds ?? new List<string>();
                        item.CheckedDate = DateTime.UtcNow;
                    }
                }
            }
        }

        /// <summary>
        /// Add photo to inspection
        /// </summary>
        public InspectionPhoto AddPhoto(string inspectionId, PhotoUpload upload)
        {
            var photo = new InspectionPhoto
            {
                PhotoId = Guid.NewGuid().ToString(),
                FileName = upload.FileName,
                FilePath = upload.FilePath,
                Caption = upload.Caption,
                Location = upload.Location,
                ElementId = upload.ElementId,
                TakenDate = DateTime.UtcNow,
                TakenBy = upload.TakenBy,
                Latitude = upload.Latitude,
                Longitude = upload.Longitude
            };

            lock (_lock)
            {
                if (_inspections.TryGetValue(inspectionId, out var inspection))
                {
                    inspection.Photos.Add(photo);
                }
            }

            return photo;
        }

        /// <summary>
        /// Complete an inspection
        /// </summary>
        public InspectionResult CompleteInspection(string inspectionId, string completedBy, string signature = null)
        {
            lock (_lock)
            {
                if (!_inspections.TryGetValue(inspectionId, out var inspection))
                    throw new KeyNotFoundException($"Inspection {inspectionId} not found");

                inspection.CompletedDate = DateTime.UtcNow;
                inspection.CompletedBy = completedBy;
                inspection.Signature = signature;

                // Calculate result
                var requiredItems = inspection.ChecklistItems.Where(i => i.Required).ToList();
                var passedRequired = requiredItems.Count(i => i.Status == ChecklistItemStatus.Pass);
                var failedRequired = requiredItems.Count(i => i.Status == ChecklistItemStatus.Fail);

                inspection.Result = failedRequired == 0 ? InspectionResult.Pass :
                    failedRequired <= requiredItems.Count * 0.1 ? InspectionResult.ConditionalPass :
                    InspectionResult.Fail;

                inspection.Status = InspectionStatus.Completed;

                // Create punch items for failed items
                foreach (var item in inspection.ChecklistItems.Where(i => i.Status == ChecklistItemStatus.Fail))
                {
                    CreatePunchItem(new PunchItemRequest
                    {
                        Title = $"Failed inspection item: {item.Description}",
                        Description = item.Notes ?? $"Item failed during inspection {inspection.InspectionId}",
                        Location = inspection.Location,
                        Level = inspection.Level,
                        Zone = inspection.Zone,
                        Priority = item.Required ? PunchPriority.High : PunchPriority.Medium,
                        Category = PunchCategory.Inspection,
                        SourceInspectionId = inspectionId,
                        CreatedBy = completedBy
                    });
                }

                InspectionCompleted?.Invoke(this, new FieldEventArgs
                {
                    Type = FieldEventType.InspectionCompleted,
                    EntityId = inspectionId,
                    Message = $"Inspection {inspectionId} completed: {inspection.Result}"
                });

                return inspection.Result;
            }
        }

        #endregion

        #region Punch Lists

        /// <summary>
        /// Create a punch list item
        /// </summary>
        public PunchItem CreatePunchItem(PunchItemRequest request)
        {
            var item = new PunchItem
            {
                PunchId = $"PL-{DateTime.UtcNow:yyyyMMdd}-{_punchItems.Count + 1:D4}",
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                Priority = request.Priority,
                Status = PunchStatus.Open,
                Location = request.Location,
                Level = request.Level,
                Zone = request.Zone,
                ElementId = request.ElementId,
                AssignedTo = request.AssignedTo,
                AssignedCompany = request.AssignedCompany,
                DueDate = request.DueDate,
                SourceInspectionId = request.SourceInspectionId,
                CreatedBy = request.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                Photos = new List<PunchPhoto>(),
                Comments = new List<PunchComment>()
            };

            lock (_lock)
            {
                _punchItems[item.PunchId] = item;
            }

            PunchItemCreated?.Invoke(this, new FieldEventArgs
            {
                Type = FieldEventType.PunchItemCreated,
                EntityId = item.PunchId,
                Message = $"Punch item created: {item.Title}"
            });

            return item;
        }

        /// <summary>
        /// Update punch item status
        /// </summary>
        public void UpdatePunchItemStatus(string punchId, PunchStatus status, string updatedBy, string notes = null)
        {
            lock (_lock)
            {
                if (_punchItems.TryGetValue(punchId, out var item))
                {
                    item.Status = status;
                    item.LastUpdated = DateTime.UtcNow;

                    if (status == PunchStatus.Completed)
                    {
                        item.CompletedDate = DateTime.UtcNow;
                        item.CompletedBy = updatedBy;
                    }
                    else if (status == PunchStatus.Verified)
                    {
                        item.VerifiedDate = DateTime.UtcNow;
                        item.VerifiedBy = updatedBy;
                    }

                    item.Comments.Add(new PunchComment
                    {
                        CommentId = Guid.NewGuid().ToString(),
                        Author = updatedBy,
                        Text = notes ?? $"Status changed to {status}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        /// <summary>
        /// Get punch list summary
        /// </summary>
        public PunchListSummary GetPunchListSummary(string level = null, string zone = null)
        {
            lock (_lock)
            {
                var query = _punchItems.Values.AsEnumerable();

                if (!string.IsNullOrEmpty(level))
                    query = query.Where(p => p.Level == level);
                if (!string.IsNullOrEmpty(zone))
                    query = query.Where(p => p.Zone == zone);

                var items = query.ToList();

                return new PunchListSummary
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalItems = items.Count,
                    OpenItems = items.Count(p => p.Status == PunchStatus.Open),
                    InProgressItems = items.Count(p => p.Status == PunchStatus.InProgress),
                    CompletedItems = items.Count(p => p.Status == PunchStatus.Completed),
                    VerifiedItems = items.Count(p => p.Status == PunchStatus.Verified),
                    OverdueItems = items.Count(p => p.DueDate < DateTime.UtcNow &&
                        p.Status != PunchStatus.Completed && p.Status != PunchStatus.Verified),
                    ByCategory = items.GroupBy(p => p.Category).ToDictionary(g => g.Key, g => g.Count()),
                    ByPriority = items.GroupBy(p => p.Priority).ToDictionary(g => g.Key, g => g.Count()),
                    ByCompany = items.Where(p => !string.IsNullOrEmpty(p.AssignedCompany))
                        .GroupBy(p => p.AssignedCompany).ToDictionary(g => g.Key, g => g.Count()),
                    ByLevel = items.Where(p => !string.IsNullOrEmpty(p.Level))
                        .GroupBy(p => p.Level).ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        #endregion

        #region Daily Reports

        /// <summary>
        /// Create a daily report
        /// </summary>
        public DailyReport CreateDailyReport(DailyReportRequest request)
        {
            var report = new DailyReport
            {
                ReportId = $"DR-{request.ReportDate:yyyyMMdd}",
                ReportDate = request.ReportDate,
                Weather = request.Weather,
                Temperature = request.Temperature,
                WorkDescription = request.WorkDescription,
                Manpower = request.Manpower ?? new List<ManpowerEntry>(),
                Equipment = request.Equipment ?? new List<EquipmentEntry>(),
                Materials = request.Materials ?? new List<MaterialDelivery>(),
                Delays = request.Delays ?? new List<DelayEntry>(),
                SafetyIncidents = request.SafetyIncidents ?? new List<string>(),
                VisitorLog = request.VisitorLog ?? new List<VisitorEntry>(),
                Photos = new List<DailyPhoto>(),
                CreatedBy = request.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                Status = ReportStatus.Draft
            };

            // Calculate totals
            report.TotalManpower = report.Manpower.Sum(m => m.Count);
            report.TotalManHours = report.Manpower.Sum(m => m.Count * m.Hours);

            lock (_lock)
            {
                _dailyReports[report.ReportId] = report;
            }

            return report;
        }

        /// <summary>
        /// Submit daily report
        /// </summary>
        public void SubmitDailyReport(string reportId, string submittedBy)
        {
            lock (_lock)
            {
                if (_dailyReports.TryGetValue(reportId, out var report))
                {
                    report.Status = ReportStatus.Submitted;
                    report.SubmittedDate = DateTime.UtcNow;
                    report.SubmittedBy = submittedBy;
                }
            }
        }

        #endregion

        #region Safety Observations

        /// <summary>
        /// Report a safety observation
        /// </summary>
        public SafetyObservation ReportSafetyObservation(SafetyObservationRequest request)
        {
            var observation = new SafetyObservation
            {
                ObservationId = $"SO-{DateTime.UtcNow:yyyyMMdd}-{_safetyObservations.Count + 1:D4}",
                Type = request.Type,
                Severity = request.Severity,
                Description = request.Description,
                Location = request.Location,
                Level = request.Level,
                Zone = request.Zone,
                ObservedBy = request.ObservedBy,
                ObservedDate = DateTime.UtcNow,
                Status = SafetyStatus.Open,
                ImmediateAction = request.ImmediateAction,
                Photos = new List<string>()
            };

            lock (_lock)
            {
                _safetyObservations[observation.ObservationId] = observation;
            }

            if (observation.Severity == SafetySeverity.Critical || observation.Severity == SafetySeverity.High)
            {
                SafetyIssueReported?.Invoke(this, new FieldEventArgs
                {
                    Type = FieldEventType.SafetyIssue,
                    EntityId = observation.ObservationId,
                    Message = $"Safety issue reported: {observation.Description}"
                });
            }

            return observation;
        }

        /// <summary>
        /// Close a safety observation
        /// </summary>
        public void CloseSafetyObservation(string observationId, string closedBy, string resolution)
        {
            lock (_lock)
            {
                if (_safetyObservations.TryGetValue(observationId, out var observation))
                {
                    observation.Status = SafetyStatus.Closed;
                    observation.ClosedDate = DateTime.UtcNow;
                    observation.ClosedBy = closedBy;
                    observation.Resolution = resolution;
                }
            }
        }

        #endregion

        #region Progress Capture

        /// <summary>
        /// Capture construction progress
        /// </summary>
        public ProgressCapture CaptureProgress(ProgressCaptureRequest request)
        {
            var capture = new ProgressCapture
            {
                CaptureId = Guid.NewGuid().ToString(),
                CaptureDate = DateTime.UtcNow,
                CapturedBy = request.CapturedBy,
                Location = request.Location,
                Level = request.Level,
                Zone = request.Zone,
                ElementIds = request.ElementIds ?? new List<string>(),
                ActivityId = request.ActivityId,
                PercentComplete = request.PercentComplete,
                Notes = request.Notes,
                Photos = new List<ProgressPhoto>()
            };

            // Add photos
            foreach (var photo in request.Photos ?? new List<PhotoUpload>())
            {
                capture.Photos.Add(new ProgressPhoto
                {
                    PhotoId = Guid.NewGuid().ToString(),
                    FileName = photo.FileName,
                    FilePath = photo.FilePath,
                    Caption = photo.Caption,
                    TakenDate = DateTime.UtcNow
                });
            }

            lock (_lock)
            {
                _progressCaptures[capture.CaptureId] = capture;
            }

            return capture;
        }

        /// <summary>
        /// Get progress history for a location
        /// </summary>
        public List<ProgressCapture> GetProgressHistory(string level, string zone, DateTime? fromDate = null)
        {
            lock (_lock)
            {
                var query = _progressCaptures.Values
                    .Where(p => p.Level == level && p.Zone == zone);

                if (fromDate.HasValue)
                    query = query.Where(p => p.CaptureDate >= fromDate.Value);

                return query.OrderBy(p => p.CaptureDate).ToList();
            }
        }

        #endregion

        #region Quality Records

        /// <summary>
        /// Create a quality record (test result, certification, etc.)
        /// </summary>
        public QualityRecord CreateQualityRecord(QualityRecordRequest request)
        {
            var record = new QualityRecord
            {
                RecordId = $"QR-{DateTime.UtcNow:yyyyMMdd}-{_qualityRecords.Count + 1:D4}",
                Type = request.Type,
                Title = request.Title,
                Description = request.Description,
                Location = request.Location,
                ElementIds = request.ElementIds ?? new List<string>(),
                TestDate = request.TestDate,
                Result = request.Result,
                Specification = request.Specification,
                ActualValue = request.ActualValue,
                RequiredValue = request.RequiredValue,
                Unit = request.Unit,
                Pass = request.Pass,
                TestedBy = request.TestedBy,
                CertificateNumber = request.CertificateNumber,
                Attachments = request.Attachments ?? new List<string>(),
                CreatedDate = DateTime.UtcNow
            };

            lock (_lock)
            {
                _qualityRecords[record.RecordId] = record;
            }

            return record;
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate field operations dashboard
        /// </summary>
        public FieldOperationsDashboard GetDashboard()
        {
            lock (_lock)
            {
                return new FieldOperationsDashboard
                {
                    GeneratedAt = DateTime.UtcNow,
                    InspectionsSummary = new InspectionsSummary
                    {
                        Scheduled = _inspections.Values.Count(i => i.Status == InspectionStatus.Scheduled),
                        InProgress = _inspections.Values.Count(i => i.Status == InspectionStatus.InProgress),
                        Completed = _inspections.Values.Count(i => i.Status == InspectionStatus.Completed),
                        PassRate = _inspections.Values.Where(i => i.Status == InspectionStatus.Completed).Any() ?
                            _inspections.Values.Where(i => i.Status == InspectionStatus.Completed)
                                .Count(i => i.Result == InspectionResult.Pass) * 100.0 /
                            _inspections.Values.Count(i => i.Status == InspectionStatus.Completed) : 0
                    },
                    PunchListSummary = GetPunchListSummary(),
                    SafetySummary = new SafetySummary
                    {
                        OpenIssues = _safetyObservations.Values.Count(s => s.Status == SafetyStatus.Open),
                        CriticalIssues = _safetyObservations.Values.Count(s =>
                            s.Status == SafetyStatus.Open && s.Severity == SafetySeverity.Critical),
                        ClosedThisWeek = _safetyObservations.Values.Count(s =>
                            s.ClosedDate >= DateTime.UtcNow.AddDays(-7))
                    },
                    ProgressCapturesThisWeek = _progressCaptures.Values
                        .Count(p => p.CaptureDate >= DateTime.UtcNow.AddDays(-7)),
                    QualityRecordsThisMonth = _qualityRecords.Values
                        .Count(q => q.CreatedDate >= DateTime.UtcNow.AddDays(-30))
                };
            }
        }

        #endregion
    }

    #region Data Models

    public class ChecklistTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<ChecklistItem> Items { get; set; }
    }

    public class ChecklistItem
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
    }

    public class Inspection
    {
        public string InspectionId { get; set; }
        public string Title { get; set; }
        public InspectionType Type { get; set; }
        public string TemplateId { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> ElementIds { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Inspector { get; set; }
        public string CompletedBy { get; set; }
        public string Signature { get; set; }
        public InspectionStatus Status { get; set; }
        public InspectionResult Result { get; set; }
        public List<InspectionChecklistItem> ChecklistItems { get; set; }
        public List<InspectionPhoto> Photos { get; set; }
        public List<InspectionNote> Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InspectionRequest
    {
        public string Title { get; set; }
        public InspectionType Type { get; set; }
        public string TemplateId { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> ElementIds { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string Inspector { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InspectionChecklistItem
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public ChecklistItemStatus Status { get; set; }
        public string Notes { get; set; }
        public List<string> PhotoIds { get; set; }
        public DateTime? CheckedDate { get; set; }
    }

    public class InspectionPhoto
    {
        public string PhotoId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Caption { get; set; }
        public string Location { get; set; }
        public string ElementId { get; set; }
        public DateTime TakenDate { get; set; }
        public string TakenBy { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class InspectionNote
    {
        public string NoteId { get; set; }
        public string Text { get; set; }
        public string Author { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PhotoUpload
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Caption { get; set; }
        public string Location { get; set; }
        public string ElementId { get; set; }
        public string TakenBy { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class PunchItem
    {
        public string PunchId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public PunchCategory Category { get; set; }
        public PunchPriority Priority { get; set; }
        public PunchStatus Status { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string ElementId { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedCompany { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string CompletedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string VerifiedBy { get; set; }
        public string SourceInspectionId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<PunchPhoto> Photos { get; set; }
        public List<PunchComment> Comments { get; set; }
    }

    public class PunchItemRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public PunchCategory Category { get; set; }
        public PunchPriority Priority { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string ElementId { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedCompany { get; set; }
        public DateTime? DueDate { get; set; }
        public string SourceInspectionId { get; set; }
        public string CreatedBy { get; set; }
    }

    public class PunchPhoto
    {
        public string PhotoId { get; set; }
        public string FilePath { get; set; }
        public string Caption { get; set; }
        public DateTime TakenDate { get; set; }
        public PunchPhotoType Type { get; set; }
    }

    public class PunchComment
    {
        public string CommentId { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PunchListSummary
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalItems { get; set; }
        public int OpenItems { get; set; }
        public int InProgressItems { get; set; }
        public int CompletedItems { get; set; }
        public int VerifiedItems { get; set; }
        public int OverdueItems { get; set; }
        public Dictionary<PunchCategory, int> ByCategory { get; set; }
        public Dictionary<PunchPriority, int> ByPriority { get; set; }
        public Dictionary<string, int> ByCompany { get; set; }
        public Dictionary<string, int> ByLevel { get; set; }
    }

    public class DailyReport
    {
        public string ReportId { get; set; }
        public DateTime ReportDate { get; set; }
        public string Weather { get; set; }
        public double? Temperature { get; set; }
        public string WorkDescription { get; set; }
        public List<ManpowerEntry> Manpower { get; set; }
        public List<EquipmentEntry> Equipment { get; set; }
        public List<MaterialDelivery> Materials { get; set; }
        public List<DelayEntry> Delays { get; set; }
        public List<string> SafetyIncidents { get; set; }
        public List<VisitorEntry> VisitorLog { get; set; }
        public List<DailyPhoto> Photos { get; set; }
        public int TotalManpower { get; set; }
        public double TotalManHours { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public string SubmittedBy { get; set; }
    }

    public class DailyReportRequest
    {
        public DateTime ReportDate { get; set; }
        public string Weather { get; set; }
        public double? Temperature { get; set; }
        public string WorkDescription { get; set; }
        public List<ManpowerEntry> Manpower { get; set; }
        public List<EquipmentEntry> Equipment { get; set; }
        public List<MaterialDelivery> Materials { get; set; }
        public List<DelayEntry> Delays { get; set; }
        public List<string> SafetyIncidents { get; set; }
        public List<VisitorEntry> VisitorLog { get; set; }
        public string CreatedBy { get; set; }
    }

    public class ManpowerEntry
    {
        public string Trade { get; set; }
        public string Company { get; set; }
        public int Count { get; set; }
        public double Hours { get; set; }
    }

    public class EquipmentEntry
    {
        public string Equipment { get; set; }
        public int Quantity { get; set; }
        public double HoursUsed { get; set; }
    }

    public class MaterialDelivery
    {
        public string Material { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public string Supplier { get; set; }
    }

    public class DelayEntry
    {
        public string Reason { get; set; }
        public double HoursLost { get; set; }
        public string ResponsibleParty { get; set; }
    }

    public class VisitorEntry
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public string Purpose { get; set; }
        public DateTime TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
    }

    public class DailyPhoto
    {
        public string PhotoId { get; set; }
        public string FilePath { get; set; }
        public string Caption { get; set; }
    }

    public class SafetyObservation
    {
        public string ObservationId { get; set; }
        public SafetyObservationType Type { get; set; }
        public SafetySeverity Severity { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string ObservedBy { get; set; }
        public DateTime ObservedDate { get; set; }
        public SafetyStatus Status { get; set; }
        public string ImmediateAction { get; set; }
        public string Resolution { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string ClosedBy { get; set; }
        public List<string> Photos { get; set; }
    }

    public class SafetyObservationRequest
    {
        public SafetyObservationType Type { get; set; }
        public SafetySeverity Severity { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public string ObservedBy { get; set; }
        public string ImmediateAction { get; set; }
    }

    public class ProgressCapture
    {
        public string CaptureId { get; set; }
        public DateTime CaptureDate { get; set; }
        public string CapturedBy { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> ElementIds { get; set; }
        public string ActivityId { get; set; }
        public double PercentComplete { get; set; }
        public string Notes { get; set; }
        public List<ProgressPhoto> Photos { get; set; }
    }

    public class ProgressCaptureRequest
    {
        public string CapturedBy { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
        public List<string> ElementIds { get; set; }
        public string ActivityId { get; set; }
        public double PercentComplete { get; set; }
        public string Notes { get; set; }
        public List<PhotoUpload> Photos { get; set; }
    }

    public class ProgressPhoto
    {
        public string PhotoId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Caption { get; set; }
        public DateTime TakenDate { get; set; }
    }

    public class QualityRecord
    {
        public string RecordId { get; set; }
        public QualityRecordType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<string> ElementIds { get; set; }
        public DateTime TestDate { get; set; }
        public string Result { get; set; }
        public string Specification { get; set; }
        public double? ActualValue { get; set; }
        public double? RequiredValue { get; set; }
        public string Unit { get; set; }
        public bool Pass { get; set; }
        public string TestedBy { get; set; }
        public string CertificateNumber { get; set; }
        public List<string> Attachments { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class QualityRecordRequest
    {
        public QualityRecordType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<string> ElementIds { get; set; }
        public DateTime TestDate { get; set; }
        public string Result { get; set; }
        public string Specification { get; set; }
        public double? ActualValue { get; set; }
        public double? RequiredValue { get; set; }
        public string Unit { get; set; }
        public bool Pass { get; set; }
        public string TestedBy { get; set; }
        public string CertificateNumber { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class FieldOperationsDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public InspectionsSummary InspectionsSummary { get; set; }
        public PunchListSummary PunchListSummary { get; set; }
        public SafetySummary SafetySummary { get; set; }
        public int ProgressCapturesThisWeek { get; set; }
        public int QualityRecordsThisMonth { get; set; }
    }

    public class InspectionsSummary
    {
        public int Scheduled { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public double PassRate { get; set; }
    }

    public class SafetySummary
    {
        public int OpenIssues { get; set; }
        public int CriticalIssues { get; set; }
        public int ClosedThisWeek { get; set; }
    }

    public class FieldEventArgs : EventArgs
    {
        public FieldEventType Type { get; set; }
        public string EntityId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum InspectionType { PrePour, AboveCeiling, Rough, Final, Warranty, Special }
    public enum InspectionStatus { Scheduled, InProgress, Completed, Cancelled }
    public enum InspectionResult { Pass, ConditionalPass, Fail }
    public enum ChecklistItemStatus { NotChecked, Pass, Fail, NA }

    public enum PunchCategory { Inspection, Walkthrough, Warranty, Safety, Coordination }
    public enum PunchPriority { Low, Medium, High, Critical }
    public enum PunchStatus { Open, InProgress, Completed, Verified, Rejected }
    public enum PunchPhotoType { Before, After }

    public enum ReportStatus { Draft, Submitted, Approved }

    public enum SafetyObservationType { Hazard, NearMiss, Incident, Positive }
    public enum SafetySeverity { Low, Medium, High, Critical }
    public enum SafetyStatus { Open, InProgress, Closed }

    public enum QualityRecordType { ConcreteTest, SoilTest, WeldTest, PressureTest, AirTest, Certificate, Other }

    public enum FieldEventType { InspectionCompleted, PunchItemCreated, SafetyIssue, ProgressCaptured }

    #endregion
}
