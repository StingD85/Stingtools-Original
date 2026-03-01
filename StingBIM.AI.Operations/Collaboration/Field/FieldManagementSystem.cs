// StingBIM.AI.Collaboration - Field Management System
// Inspired by BIM 360 Field with AI enhancements for site operations

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Field
{
    /// <summary>
    /// Comprehensive field management system for construction site operations
    /// with AI-powered inspections, checklists, and quality control
    /// </summary>
    public class FieldManagementSystem : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, Inspection> _inspections = new();
        private readonly ConcurrentDictionary<string, ChecklistTemplate> _checklistTemplates = new();
        private readonly ConcurrentDictionary<string, ChecklistInstance> _checklists = new();
        private readonly ConcurrentDictionary<string, DailyLog> _dailyLogs = new();
        private readonly ConcurrentDictionary<string, SitePhoto> _photos = new();
        private readonly ConcurrentDictionary<string, SafetyObservation> _safetyObservations = new();
        private readonly ConcurrentDictionary<string, QualityRecord> _qualityRecords = new();
        private readonly FieldAI _fieldAI;
        private readonly object _lockObject = new();

        public event EventHandler<InspectionCompletedEventArgs>? InspectionCompleted;
        public event EventHandler<SafetyAlertEventArgs>? SafetyAlert;
        public event EventHandler<QualityIssueEventArgs>? QualityIssue;
        public event EventHandler<ChecklistCompletedEventArgs>? ChecklistCompleted;

        public FieldManagementSystem()
        {
            _fieldAI = new FieldAI(this);
            InitializeDefaultTemplates();
        }

        private void InitializeDefaultTemplates()
        {
            // Standard inspection checklists
            _checklistTemplates["concrete-pour"] = new ChecklistTemplate
            {
                Id = "concrete-pour",
                Name = "Concrete Pour Inspection",
                Category = "Structural",
                Items = new List<ChecklistItemTemplate>
                {
                    new() { Id = "1", Question = "Formwork properly aligned and secured?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "2", Question = "Rebar placement verified per drawings?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "3", Question = "Concrete slump test performed?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "4", Question = "Slump test result (inches)", Type = ChecklistItemType.Number, Required = true },
                    new() { Id = "5", Question = "Concrete temperature (°F)", Type = ChecklistItemType.Number },
                    new() { Id = "6", Question = "Weather conditions", Type = ChecklistItemType.Select, Options = new[] { "Clear", "Cloudy", "Rain", "Snow" } },
                    new() { Id = "7", Question = "Photos attached?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "8", Question = "Additional comments", Type = ChecklistItemType.Text }
                }
            };

            _checklistTemplates["mep-rough-in"] = new ChecklistTemplate
            {
                Id = "mep-rough-in",
                Name = "MEP Rough-In Inspection",
                Category = "MEP",
                Items = new List<ChecklistItemTemplate>
                {
                    new() { Id = "1", Question = "All penetrations properly sleeved?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "2", Question = "Pipe supports at correct intervals?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "3", Question = "Electrical boxes at correct heights?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "4", Question = "Ductwork properly sealed?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "5", Question = "Fire stopping complete?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "6", Question = "Deficiencies noted", Type = ChecklistItemType.Text }
                }
            };

            _checklistTemplates["safety-walk"] = new ChecklistTemplate
            {
                Id = "safety-walk",
                Name = "Daily Safety Walk",
                Category = "Safety",
                Items = new List<ChecklistItemTemplate>
                {
                    new() { Id = "1", Question = "All workers wearing required PPE?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "2", Question = "Fall protection in place where required?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "3", Question = "Excavations properly shored?", Type = ChecklistItemType.YesNo },
                    new() { Id = "4", Question = "Fire extinguishers accessible?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "5", Question = "Housekeeping satisfactory?", Type = ChecklistItemType.Rating, Required = true },
                    new() { Id = "6", Question = "Number of workers on site", Type = ChecklistItemType.Number, Required = true },
                    new() { Id = "7", Question = "Safety concerns observed", Type = ChecklistItemType.Text }
                }
            };

            _checklistTemplates["final-inspection"] = new ChecklistTemplate
            {
                Id = "final-inspection",
                Name = "Final Inspection Checklist",
                Category = "Quality",
                Items = new List<ChecklistItemTemplate>
                {
                    new() { Id = "1", Question = "All punch list items complete?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "2", Question = "Systems tested and commissioned?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "3", Question = "As-built drawings submitted?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "4", Question = "O&M manuals delivered?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "5", Question = "Warranty documentation complete?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "6", Question = "Final cleaning complete?", Type = ChecklistItemType.YesNo, Required = true },
                    new() { Id = "7", Question = "Overall quality rating", Type = ChecklistItemType.Rating, Required = true }
                }
            };
        }

        #region Inspections

        /// <summary>
        /// Schedule a new inspection
        /// </summary>
        public Inspection ScheduleInspection(CreateInspectionRequest request)
        {
            var inspection = new Inspection
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Title = request.Title,
                Type = request.Type,
                Category = request.Category,
                ScheduledDate = request.ScheduledDate,
                Location = request.Location,
                AssignedTo = request.AssignedTo,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                Status = InspectionStatus.Scheduled,
                ChecklistTemplateId = request.ChecklistTemplateId,
                LinkedElements = request.LinkedElements ?? new List<string>(),
                Priority = request.Priority
            };

            // Create checklist instance if template specified
            if (!string.IsNullOrEmpty(request.ChecklistTemplateId) &&
                _checklistTemplates.TryGetValue(request.ChecklistTemplateId, out var template))
            {
                var checklist = CreateChecklistFromTemplate(template, request.CreatedBy);
                inspection.ChecklistId = checklist.Id;
            }

            _inspections[inspection.Id] = inspection;
            return inspection;
        }

        /// <summary>
        /// Start an inspection
        /// </summary>
        public Inspection StartInspection(string inspectionId, string startedBy)
        {
            if (!_inspections.TryGetValue(inspectionId, out var inspection))
                throw new InspectionNotFoundException(inspectionId);

            inspection.Status = InspectionStatus.InProgress;
            inspection.StartedAt = DateTime.UtcNow;
            inspection.StartedBy = startedBy;

            return inspection;
        }

        /// <summary>
        /// Complete an inspection with results
        /// </summary>
        public async Task<Inspection> CompleteInspectionAsync(
            string inspectionId,
            CompleteInspectionRequest request,
            CancellationToken ct = default)
        {
            if (!_inspections.TryGetValue(inspectionId, out var inspection))
                throw new InspectionNotFoundException(inspectionId);

            inspection.Status = request.Passed ? InspectionStatus.Passed : InspectionStatus.Failed;
            inspection.CompletedAt = DateTime.UtcNow;
            inspection.CompletedBy = request.CompletedBy;
            inspection.Result = new InspectionResult
            {
                Passed = request.Passed,
                Score = request.Score,
                Comments = request.Comments,
                Deficiencies = request.Deficiencies ?? new List<Deficiency>(),
                PhotoIds = request.PhotoIds ?? new List<string>(),
                Signature = request.Signature
            };

            // AI analysis of inspection results
            var aiAnalysis = await _fieldAI.AnalyzeInspectionAsync(inspection, ct);
            inspection.AIAnalysis = aiAnalysis;

            // Raise quality issues if deficiencies found
            if (inspection.Result.Deficiencies.Any())
            {
                foreach (var deficiency in inspection.Result.Deficiencies.Where(d => d.Severity >= DeficiencySeverity.Major))
                {
                    QualityIssue?.Invoke(this, new QualityIssueEventArgs(inspection, deficiency));
                }
            }

            InspectionCompleted?.Invoke(this, new InspectionCompletedEventArgs(inspection));

            return inspection;
        }

        /// <summary>
        /// Add deficiency to inspection
        /// </summary>
        public Deficiency AddDeficiency(
            string inspectionId,
            CreateDeficiencyRequest request)
        {
            if (!_inspections.TryGetValue(inspectionId, out var inspection))
                throw new InspectionNotFoundException(inspectionId);

            var deficiency = new Deficiency
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Title = request.Title,
                Description = request.Description,
                Severity = request.Severity,
                Location = request.Location,
                ElementId = request.ElementId,
                PhotoIds = request.PhotoIds ?? new List<string>(),
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate,
                Status = DeficiencyStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy
            };

            inspection.Result ??= new InspectionResult();
            inspection.Result.Deficiencies.Add(deficiency);

            if (deficiency.Severity >= DeficiencySeverity.Major)
            {
                QualityIssue?.Invoke(this, new QualityIssueEventArgs(inspection, deficiency));
            }

            return deficiency;
        }

        /// <summary>
        /// Get inspection by ID
        /// </summary>
        public Inspection? GetInspection(string inspectionId)
            => _inspections.TryGetValue(inspectionId, out var inspection) ? inspection : null;

        /// <summary>
        /// Query inspections
        /// </summary>
        public List<Inspection> QueryInspections(InspectionQuery query)
        {
            var inspections = _inspections.Values.AsEnumerable();

            if (query.Statuses?.Any() == true)
                inspections = inspections.Where(i => query.Statuses.Contains(i.Status));

            if (!string.IsNullOrEmpty(query.AssignedTo))
                inspections = inspections.Where(i => i.AssignedTo == query.AssignedTo);

            if (!string.IsNullOrEmpty(query.Category))
                inspections = inspections.Where(i => i.Category == query.Category);

            if (query.ScheduledAfter.HasValue)
                inspections = inspections.Where(i => i.ScheduledDate >= query.ScheduledAfter);

            if (query.ScheduledBefore.HasValue)
                inspections = inspections.Where(i => i.ScheduledDate <= query.ScheduledBefore);

            return inspections
                .OrderBy(i => i.ScheduledDate)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList();
        }

        #endregion

        #region Checklists

        /// <summary>
        /// Create checklist from template
        /// </summary>
        public ChecklistInstance CreateChecklistFromTemplate(ChecklistTemplate template, string createdBy)
        {
            var instance = new ChecklistInstance
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                TemplateId = template.Id,
                TemplateName = template.Name,
                Category = template.Category,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                Status = ChecklistStatus.NotStarted,
                Items = template.Items.Select(t => new ChecklistItem
                {
                    Id = t.Id,
                    Question = t.Question,
                    Type = t.Type,
                    Required = t.Required,
                    Options = t.Options
                }).ToList()
            };

            _checklists[instance.Id] = instance;
            return instance;
        }

        /// <summary>
        /// Update checklist item response
        /// </summary>
        public void UpdateChecklistItem(
            string checklistId,
            string itemId,
            object response,
            string updatedBy)
        {
            if (!_checklists.TryGetValue(checklistId, out var checklist))
                throw new ChecklistNotFoundException(checklistId);

            var item = checklist.Items.FirstOrDefault(i => i.Id == itemId)
                ?? throw new ChecklistItemNotFoundException(itemId);

            item.Response = response;
            item.AnsweredAt = DateTime.UtcNow;
            item.AnsweredBy = updatedBy;

            // Update checklist status
            if (checklist.Status == ChecklistStatus.NotStarted)
            {
                checklist.Status = ChecklistStatus.InProgress;
                checklist.StartedAt = DateTime.UtcNow;
            }

            // Check if complete
            var requiredItems = checklist.Items.Where(i => i.Required).ToList();
            if (requiredItems.All(i => i.Response != null))
            {
                var allItemsAnswered = checklist.Items.All(i => i.Response != null);
                checklist.Status = allItemsAnswered ? ChecklistStatus.Completed : ChecklistStatus.InProgress;

                if (checklist.Status == ChecklistStatus.Completed)
                {
                    checklist.CompletedAt = DateTime.UtcNow;
                    checklist.CompletedBy = updatedBy;

                    // Calculate score
                    checklist.Score = CalculateChecklistScore(checklist);

                    ChecklistCompleted?.Invoke(this, new ChecklistCompletedEventArgs(checklist));
                }
            }
        }

        private double CalculateChecklistScore(ChecklistInstance checklist)
        {
            var scorableItems = checklist.Items.Where(i =>
                i.Type == ChecklistItemType.YesNo ||
                i.Type == ChecklistItemType.Rating).ToList();

            if (!scorableItems.Any()) return 100;

            var totalScore = 0.0;
            foreach (var item in scorableItems)
            {
                if (item.Type == ChecklistItemType.YesNo && item.Response is bool yesNo && yesNo)
                    totalScore += 100;
                else if (item.Type == ChecklistItemType.Rating && item.Response is int rating)
                    totalScore += rating * 20; // 1-5 scale to 0-100
            }

            return totalScore / scorableItems.Count;
        }

        /// <summary>
        /// Get checklist templates
        /// </summary>
        public List<ChecklistTemplate> GetChecklistTemplates()
            => _checklistTemplates.Values.ToList();

        /// <summary>
        /// Create custom checklist template
        /// </summary>
        public ChecklistTemplate CreateTemplate(ChecklistTemplate template)
        {
            template.Id = Guid.NewGuid().ToString("N")[..12];
            _checklistTemplates[template.Id] = template;
            return template;
        }

        #endregion

        #region Daily Logs

        /// <summary>
        /// Create or update daily log
        /// </summary>
        public DailyLog UpdateDailyLog(string projectId, DateTime date, DailyLogUpdate update)
        {
            var logId = $"{projectId}_{date:yyyyMMdd}";

            if (!_dailyLogs.TryGetValue(logId, out var log))
            {
                log = new DailyLog
                {
                    Id = logId,
                    ProjectId = projectId,
                    Date = date.Date,
                    CreatedAt = DateTime.UtcNow
                };
                _dailyLogs[logId] = log;
            }

            // Update fields
            if (update.WeatherConditions != null)
                log.WeatherConditions = update.WeatherConditions;

            if (update.ManpowerCount.HasValue)
                log.ManpowerCount = update.ManpowerCount.Value;

            if (update.WorkPerformed != null)
                log.WorkPerformed = update.WorkPerformed;

            if (update.MaterialsReceived != null)
            {
                log.MaterialsReceived ??= new List<MaterialDelivery>();
                log.MaterialsReceived.AddRange(update.MaterialsReceived);
            }

            if (update.EquipmentOnSite != null)
            {
                log.EquipmentOnSite ??= new List<Equipment>();
                foreach (var equip in update.EquipmentOnSite)
                {
                    var existing = log.EquipmentOnSite.FirstOrDefault(e => e.Id == equip.Id);
                    if (existing != null)
                    {
                        existing.Status = equip.Status;
                        existing.HoursUsed = equip.HoursUsed;
                    }
                    else
                    {
                        log.EquipmentOnSite.Add(equip);
                    }
                }
            }

            if (update.Visitors != null)
            {
                log.Visitors ??= new List<SiteVisitor>();
                log.Visitors.AddRange(update.Visitors);
            }

            if (update.DelayNotes != null)
                log.DelayNotes = update.DelayNotes;

            if (update.SafetyNotes != null)
                log.SafetyNotes = update.SafetyNotes;

            log.UpdatedAt = DateTime.UtcNow;
            log.UpdatedBy = update.UpdatedBy;

            return log;
        }

        /// <summary>
        /// Get daily log
        /// </summary>
        public DailyLog? GetDailyLog(string projectId, DateTime date)
        {
            var logId = $"{projectId}_{date:yyyyMMdd}";
            return _dailyLogs.TryGetValue(logId, out var log) ? log : null;
        }

        /// <summary>
        /// Get daily logs for date range
        /// </summary>
        public List<DailyLog> GetDailyLogs(string projectId, DateTime startDate, DateTime endDate)
        {
            return _dailyLogs.Values
                .Where(l => l.ProjectId == projectId && l.Date >= startDate && l.Date <= endDate)
                .OrderBy(l => l.Date)
                .ToList();
        }

        #endregion

        #region Site Photos

        /// <summary>
        /// Upload site photo with AI analysis
        /// </summary>
        public async Task<SitePhoto> UploadPhotoAsync(
            UploadPhotoRequest request,
            CancellationToken ct = default)
        {
            var photo = new SitePhoto
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                FileName = request.FileName,
                ContentType = request.ContentType,
                FileSize = request.Content.Length,
                Location = request.Location,
                GpsCoordinates = request.GpsCoordinates,
                TakenAt = request.TakenAt ?? DateTime.UtcNow,
                UploadedBy = request.UploadedBy,
                UploadedAt = DateTime.UtcNow,
                Tags = request.Tags ?? new List<string>(),
                LinkedElementId = request.LinkedElementId,
                InspectionId = request.InspectionId,
                IssueId = request.IssueId
            };

            // AI analysis of photo
            var aiAnalysis = await _fieldAI.AnalyzePhotoAsync(request.Content, ct);
            photo.AIAnalysis = aiAnalysis;

            // Auto-tag based on AI
            if (aiAnalysis.DetectedObjects?.Any() == true)
            {
                photo.Tags.AddRange(aiAnalysis.DetectedObjects.Where(o => o.Confidence > 0.7).Select(o => o.Label));
            }

            // Safety alert if hazards detected
            if (aiAnalysis.DetectedHazards?.Any() == true)
            {
                foreach (var hazard in aiAnalysis.DetectedHazards)
                {
                    SafetyAlert?.Invoke(this, new SafetyAlertEventArgs(
                        AlertType.PhotoHazard,
                        hazard.Description,
                        photo.Id));
                }
            }

            _photos[photo.Id] = photo;
            return photo;
        }

        /// <summary>
        /// Get photo by ID
        /// </summary>
        public SitePhoto? GetPhoto(string photoId)
            => _photos.TryGetValue(photoId, out var photo) ? photo : null;

        /// <summary>
        /// Query photos
        /// </summary>
        public List<SitePhoto> QueryPhotos(PhotoQuery query)
        {
            var photos = _photos.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(query.Location))
                photos = photos.Where(p => p.Location?.Contains(query.Location) == true);

            if (query.TakenAfter.HasValue)
                photos = photos.Where(p => p.TakenAt >= query.TakenAfter);

            if (query.Tags?.Any() == true)
                photos = photos.Where(p => p.Tags.Any(t => query.Tags.Contains(t)));

            if (!string.IsNullOrEmpty(query.InspectionId))
                photos = photos.Where(p => p.InspectionId == query.InspectionId);

            return photos
                .OrderByDescending(p => p.TakenAt)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList();
        }

        #endregion

        #region Safety Observations

        /// <summary>
        /// Report safety observation
        /// </summary>
        public async Task<SafetyObservation> ReportSafetyObservationAsync(
            CreateSafetyObservationRequest request,
            CancellationToken ct = default)
        {
            var observation = new SafetyObservation
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Type = request.Type,
                Category = request.Category,
                Description = request.Description,
                Location = request.Location,
                Severity = request.Severity,
                PhotoIds = request.PhotoIds ?? new List<string>(),
                ReportedBy = request.ReportedBy,
                ReportedAt = DateTime.UtcNow,
                Status = SafetyStatus.Open,
                ImmediateActionRequired = request.Severity >= SafetySeverity.High
            };

            // AI risk assessment
            var riskAssessment = await _fieldAI.AssessSafetyRiskAsync(observation, ct);
            observation.AIRiskAssessment = riskAssessment;

            if (observation.Severity >= SafetySeverity.Critical || riskAssessment.RiskLevel >= RiskLevel.High)
            {
                SafetyAlert?.Invoke(this, new SafetyAlertEventArgs(
                    AlertType.SafetyObservation,
                    observation.Description,
                    observation.Id));
            }

            _safetyObservations[observation.Id] = observation;
            return observation;
        }

        /// <summary>
        /// Resolve safety observation
        /// </summary>
        public void ResolveSafetyObservation(
            string observationId,
            string resolution,
            string resolvedBy)
        {
            if (!_safetyObservations.TryGetValue(observationId, out var observation))
                throw new SafetyObservationNotFoundException(observationId);

            observation.Status = SafetyStatus.Resolved;
            observation.Resolution = resolution;
            observation.ResolvedAt = DateTime.UtcNow;
            observation.ResolvedBy = resolvedBy;
        }

        /// <summary>
        /// Get safety statistics
        /// </summary>
        public SafetyStatistics GetSafetyStatistics(string? projectId = null)
        {
            var observations = projectId != null
                ? _safetyObservations.Values.Where(o => o.ProjectId == projectId)
                : _safetyObservations.Values;

            var list = observations.ToList();

            return new SafetyStatistics
            {
                TotalObservations = list.Count,
                OpenObservations = list.Count(o => o.Status == SafetyStatus.Open),
                ResolvedObservations = list.Count(o => o.Status == SafetyStatus.Resolved),
                BySeverity = list.GroupBy(o => o.Severity).ToDictionary(g => g.Key, g => g.Count()),
                ByCategory = list.GroupBy(o => o.Category).ToDictionary(g => g.Key, g => g.Count()),
                IncidentFreedays = CalculateIncidentFreeDays(list),
                AverageResolutionTime = CalculateAverageResolutionTime(list)
            };
        }

        private int CalculateIncidentFreeDays(List<SafetyObservation> observations)
        {
            var lastIncident = observations
                .Where(o => o.Severity >= SafetySeverity.High)
                .OrderByDescending(o => o.ReportedAt)
                .FirstOrDefault();

            if (lastIncident == null) return 365; // Default if no incidents

            return (int)(DateTime.UtcNow - lastIncident.ReportedAt).TotalDays;
        }

        private TimeSpan CalculateAverageResolutionTime(List<SafetyObservation> observations)
        {
            var resolved = observations.Where(o => o.ResolvedAt.HasValue).ToList();
            if (!resolved.Any()) return TimeSpan.Zero;

            var totalTicks = resolved.Sum(o => (o.ResolvedAt!.Value - o.ReportedAt).Ticks);
            return TimeSpan.FromTicks(totalTicks / resolved.Count);
        }

        #endregion

        #region Quality Control

        /// <summary>
        /// Record quality measurement
        /// </summary>
        public QualityRecord RecordQualityMeasurement(CreateQualityRecordRequest request)
        {
            var record = new QualityRecord
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Type = request.Type,
                Category = request.Category,
                Location = request.Location,
                ElementId = request.ElementId,
                Specification = request.Specification,
                MeasuredValue = request.MeasuredValue,
                Unit = request.Unit,
                PassFail = DeterminePassFail(request),
                MeasuredBy = request.MeasuredBy,
                MeasuredAt = DateTime.UtcNow,
                PhotoIds = request.PhotoIds ?? new List<string>(),
                Notes = request.Notes
            };

            _qualityRecords[record.Id] = record;

            if (!record.PassFail)
            {
                QualityIssue?.Invoke(this, new QualityIssueEventArgs(null, new Deficiency
                {
                    Id = record.Id,
                    Title = $"Quality failure: {record.Type}",
                    Description = $"Measured value {record.MeasuredValue} {record.Unit} outside specification {record.Specification}",
                    Severity = DeficiencySeverity.Major,
                    Location = record.Location
                }));
            }

            return record;
        }

        private bool DeterminePassFail(CreateQualityRecordRequest request)
        {
            if (request.SpecificationMin.HasValue && request.MeasuredValue < request.SpecificationMin)
                return false;
            if (request.SpecificationMax.HasValue && request.MeasuredValue > request.SpecificationMax)
                return false;
            return true;
        }

        /// <summary>
        /// Get quality statistics
        /// </summary>
        public QualityStatistics GetQualityStatistics(string? projectId = null)
        {
            var records = projectId != null
                ? _qualityRecords.Values.Where(r => r.ProjectId == projectId)
                : _qualityRecords.Values;

            var list = records.ToList();

            return new QualityStatistics
            {
                TotalMeasurements = list.Count,
                PassCount = list.Count(r => r.PassFail),
                FailCount = list.Count(r => !r.PassFail),
                PassRate = list.Any() ? (double)list.Count(r => r.PassFail) / list.Count * 100 : 100,
                ByCategory = list.GroupBy(r => r.Category).ToDictionary(
                    g => g.Key,
                    g => new CategoryQualityStats
                    {
                        Total = g.Count(),
                        Pass = g.Count(r => r.PassFail),
                        Fail = g.Count(r => !r.PassFail)
                    })
            };
        }

        #endregion

        #region Reports

        /// <summary>
        /// Generate field report
        /// </summary>
        public async Task<FieldReport> GenerateReportAsync(
            FieldReportRequest request,
            CancellationToken ct = default)
        {
            var inspections = QueryInspections(new InspectionQuery
            {
                ScheduledAfter = request.StartDate,
                ScheduledBefore = request.EndDate
            });

            var safetyObs = _safetyObservations.Values
                .Where(o => o.ReportedAt >= request.StartDate && o.ReportedAt <= request.EndDate)
                .ToList();

            var qualityRecords = _qualityRecords.Values
                .Where(r => r.MeasuredAt >= request.StartDate && r.MeasuredAt <= request.EndDate)
                .ToList();

            var report = new FieldReport
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Title = request.Title ?? $"Field Report {request.StartDate:MMM dd} - {request.EndDate:MMM dd, yyyy}",
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = request.GeneratedBy,
                Period = new DateRange { Start = request.StartDate, End = request.EndDate },

                InspectionSummary = new InspectionSummary
                {
                    Total = inspections.Count,
                    Passed = inspections.Count(i => i.Status == InspectionStatus.Passed),
                    Failed = inspections.Count(i => i.Status == InspectionStatus.Failed),
                    Pending = inspections.Count(i => i.Status == InspectionStatus.Scheduled)
                },

                SafetySummary = new SafetySummary
                {
                    TotalObservations = safetyObs.Count,
                    OpenIssues = safetyObs.Count(o => o.Status == SafetyStatus.Open),
                    CriticalIssues = safetyObs.Count(o => o.Severity >= SafetySeverity.Critical)
                },

                QualitySummary = new QualitySummary
                {
                    TotalTests = qualityRecords.Count,
                    PassRate = qualityRecords.Any()
                        ? (double)qualityRecords.Count(r => r.PassFail) / qualityRecords.Count * 100
                        : 100
                }
            };

            // AI insights
            report.AIInsights = await _fieldAI.GenerateFieldInsightsAsync(
                inspections, safetyObs, qualityRecords, ct);

            return report;
        }

        #endregion

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #region Inspection Models

    public class Inspection
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public InspectionType Type { get; set; }
        public string Category { get; set; } = "";
        public DateTime ScheduledDate { get; set; }
        public string? Location { get; set; }
        public string? AssignedTo { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public InspectionStatus Status { get; set; }
        public InspectionPriority Priority { get; set; }
        public string? ChecklistTemplateId { get; set; }
        public string? ChecklistId { get; set; }
        public List<string> LinkedElements { get; set; } = new();
        public DateTime? StartedAt { get; set; }
        public string? StartedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        public InspectionResult? Result { get; set; }
        public InspectionAIAnalysis? AIAnalysis { get; set; }
    }

    public enum InspectionType { Routine, PrePour, PostPour, RoughIn, Final, Safety, Quality, Milestone }
    public enum InspectionStatus { Scheduled, InProgress, Passed, Failed, Cancelled }
    public enum InspectionPriority { Low, Normal, High, Critical }

    public class InspectionResult
    {
        public bool Passed { get; set; }
        public double? Score { get; set; }
        public string? Comments { get; set; }
        public List<Deficiency> Deficiencies { get; set; } = new();
        public List<string> PhotoIds { get; set; } = new();
        public byte[]? Signature { get; set; }
    }

    public class Deficiency
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DeficiencySeverity Severity { get; set; }
        public string? Location { get; set; }
        public string? ElementId { get; set; }
        public List<string> PhotoIds { get; set; } = new();
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public DeficiencyStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
    }

    public enum DeficiencySeverity { Minor, Moderate, Major, Critical }
    public enum DeficiencyStatus { Open, InProgress, Resolved, Verified }

    public class CreateInspectionRequest
    {
        public string Title { get; set; } = "";
        public InspectionType Type { get; set; }
        public string Category { get; set; } = "";
        public DateTime ScheduledDate { get; set; }
        public string? Location { get; set; }
        public string? AssignedTo { get; set; }
        public string CreatedBy { get; set; } = "";
        public string? ChecklistTemplateId { get; set; }
        public List<string>? LinkedElements { get; set; }
        public InspectionPriority Priority { get; set; }
    }

    public class CompleteInspectionRequest
    {
        public string CompletedBy { get; set; } = "";
        public bool Passed { get; set; }
        public double? Score { get; set; }
        public string? Comments { get; set; }
        public List<Deficiency>? Deficiencies { get; set; }
        public List<string>? PhotoIds { get; set; }
        public byte[]? Signature { get; set; }
    }

    public class CreateDeficiencyRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DeficiencySeverity Severity { get; set; }
        public string? Location { get; set; }
        public string? ElementId { get; set; }
        public List<string>? PhotoIds { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public string CreatedBy { get; set; } = "";
    }

    public class InspectionQuery
    {
        public List<InspectionStatus>? Statuses { get; set; }
        public string? AssignedTo { get; set; }
        public string? Category { get; set; }
        public DateTime? ScheduledAfter { get; set; }
        public DateTime? ScheduledBefore { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    #endregion

    #region Checklist Models

    public class ChecklistTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public List<ChecklistItemTemplate> Items { get; set; } = new();
    }

    public class ChecklistItemTemplate
    {
        public string Id { get; set; } = "";
        public string Question { get; set; } = "";
        public ChecklistItemType Type { get; set; }
        public bool Required { get; set; }
        public string[]? Options { get; set; }
    }

    public enum ChecklistItemType { YesNo, Text, Number, Select, MultiSelect, Rating, Photo, Signature }

    public class ChecklistInstance
    {
        public string Id { get; set; } = "";
        public string TemplateId { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string Category { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public ChecklistStatus Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        public double? Score { get; set; }
        public List<ChecklistItem> Items { get; set; } = new();
    }

    public class ChecklistItem
    {
        public string Id { get; set; } = "";
        public string Question { get; set; } = "";
        public ChecklistItemType Type { get; set; }
        public bool Required { get; set; }
        public string[]? Options { get; set; }
        public object? Response { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public string? AnsweredBy { get; set; }
        public string? Notes { get; set; }
    }

    public enum ChecklistStatus { NotStarted, InProgress, Completed }

    #endregion

    #region Daily Log Models

    public class DailyLog
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public DateTime Date { get; set; }
        public WeatherConditions? WeatherConditions { get; set; }
        public int ManpowerCount { get; set; }
        public string? WorkPerformed { get; set; }
        public List<MaterialDelivery>? MaterialsReceived { get; set; }
        public List<Equipment>? EquipmentOnSite { get; set; }
        public List<SiteVisitor>? Visitors { get; set; }
        public string? DelayNotes { get; set; }
        public string? SafetyNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class WeatherConditions
    {
        public double Temperature { get; set; }
        public string TemperatureUnit { get; set; } = "F";
        public string Conditions { get; set; } = "";
        public double? WindSpeed { get; set; }
        public double? Humidity { get; set; }
        public double? Precipitation { get; set; }
    }

    public class MaterialDelivery
    {
        public string Material { get; set; } = "";
        public double Quantity { get; set; }
        public string Unit { get; set; } = "";
        public string? Supplier { get; set; }
        public string? DeliveryTicket { get; set; }
    }

    public class Equipment
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public EquipmentStatus Status { get; set; }
        public double? HoursUsed { get; set; }
    }

    public enum EquipmentStatus { OnSite, InUse, Idle, Maintenance, OffSite }

    public class SiteVisitor
    {
        public string Name { get; set; } = "";
        public string Company { get; set; } = "";
        public string Purpose { get; set; } = "";
        public DateTime ArrivalTime { get; set; }
        public DateTime? DepartureTime { get; set; }
    }

    public class DailyLogUpdate
    {
        public string UpdatedBy { get; set; } = "";
        public WeatherConditions? WeatherConditions { get; set; }
        public int? ManpowerCount { get; set; }
        public string? WorkPerformed { get; set; }
        public List<MaterialDelivery>? MaterialsReceived { get; set; }
        public List<Equipment>? EquipmentOnSite { get; set; }
        public List<SiteVisitor>? Visitors { get; set; }
        public string? DelayNotes { get; set; }
        public string? SafetyNotes { get; set; }
    }

    #endregion

    #region Photo Models

    public class SitePhoto
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long FileSize { get; set; }
        public string? Location { get; set; }
        public GpsCoordinates? GpsCoordinates { get; set; }
        public DateTime TakenAt { get; set; }
        public string UploadedBy { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public List<string> Tags { get; set; } = new();
        public string? LinkedElementId { get; set; }
        public string? InspectionId { get; set; }
        public string? IssueId { get; set; }
        public PhotoAIAnalysis? AIAnalysis { get; set; }
    }

    public class GpsCoordinates
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Altitude { get; set; }
    }

    public class UploadPhotoRequest
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string? Location { get; set; }
        public GpsCoordinates? GpsCoordinates { get; set; }
        public DateTime? TakenAt { get; set; }
        public string UploadedBy { get; set; } = "";
        public List<string>? Tags { get; set; }
        public string? LinkedElementId { get; set; }
        public string? InspectionId { get; set; }
        public string? IssueId { get; set; }
    }

    public class PhotoQuery
    {
        public string? Location { get; set; }
        public DateTime? TakenAfter { get; set; }
        public List<string>? Tags { get; set; }
        public string? InspectionId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    #endregion

    #region Safety Models

    public class SafetyObservation
    {
        public string Id { get; set; } = "";
        public string? ProjectId { get; set; }
        public SafetyObservationType Type { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Location { get; set; }
        public SafetySeverity Severity { get; set; }
        public List<string> PhotoIds { get; set; } = new();
        public string ReportedBy { get; set; } = "";
        public DateTime ReportedAt { get; set; }
        public SafetyStatus Status { get; set; }
        public bool ImmediateActionRequired { get; set; }
        public string? AssignedTo { get; set; }
        public string? Resolution { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public SafetyRiskAssessment? AIRiskAssessment { get; set; }
    }

    public enum SafetyObservationType { Hazard, NearMiss, Incident, PositiveObservation }
    public enum SafetySeverity { Low, Medium, High, Critical }
    public enum SafetyStatus { Open, InProgress, Resolved, Closed }

    public class CreateSafetyObservationRequest
    {
        public SafetyObservationType Type { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Location { get; set; }
        public SafetySeverity Severity { get; set; }
        public List<string>? PhotoIds { get; set; }
        public string ReportedBy { get; set; } = "";
    }

    public class SafetyStatistics
    {
        public int TotalObservations { get; set; }
        public int OpenObservations { get; set; }
        public int ResolvedObservations { get; set; }
        public Dictionary<SafetySeverity, int> BySeverity { get; set; } = new();
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public int IncidentFreedays { get; set; }
        public TimeSpan AverageResolutionTime { get; set; }
    }

    #endregion

    #region Quality Models

    public class QualityRecord
    {
        public string Id { get; set; } = "";
        public string? ProjectId { get; set; }
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Location { get; set; }
        public string? ElementId { get; set; }
        public string Specification { get; set; } = "";
        public double MeasuredValue { get; set; }
        public string Unit { get; set; } = "";
        public bool PassFail { get; set; }
        public string MeasuredBy { get; set; } = "";
        public DateTime MeasuredAt { get; set; }
        public List<string> PhotoIds { get; set; } = new();
        public string? Notes { get; set; }
    }

    public class CreateQualityRecordRequest
    {
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Location { get; set; }
        public string? ElementId { get; set; }
        public string Specification { get; set; } = "";
        public double MeasuredValue { get; set; }
        public string Unit { get; set; } = "";
        public double? SpecificationMin { get; set; }
        public double? SpecificationMax { get; set; }
        public string MeasuredBy { get; set; } = "";
        public List<string>? PhotoIds { get; set; }
        public string? Notes { get; set; }
    }

    public class QualityStatistics
    {
        public int TotalMeasurements { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public double PassRate { get; set; }
        public Dictionary<string, CategoryQualityStats> ByCategory { get; set; } = new();
    }

    public class CategoryQualityStats
    {
        public int Total { get; set; }
        public int Pass { get; set; }
        public int Fail { get; set; }
    }

    #endregion

    #region Report Models

    public class FieldReport
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = "";
        public DateRange Period { get; set; } = new();
        public InspectionSummary InspectionSummary { get; set; } = new();
        public SafetySummary SafetySummary { get; set; } = new();
        public QualitySummary QualitySummary { get; set; } = new();
        public FieldAIInsights? AIInsights { get; set; }
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class InspectionSummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Pending { get; set; }
    }

    public class SafetySummary
    {
        public int TotalObservations { get; set; }
        public int OpenIssues { get; set; }
        public int CriticalIssues { get; set; }
    }

    public class QualitySummary
    {
        public int TotalTests { get; set; }
        public double PassRate { get; set; }
    }

    public class FieldReportRequest
    {
        public string? Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GeneratedBy { get; set; } = "";
    }

    #endregion

    #region AI Models

    public class FieldAI
    {
        private readonly FieldManagementSystem _system;

        public FieldAI(FieldManagementSystem system)
        {
            _system = system;
        }

        public Task<InspectionAIAnalysis> AnalyzeInspectionAsync(Inspection inspection, CancellationToken ct)
        {
            var analysis = new InspectionAIAnalysis
            {
                RiskLevel = inspection.Result?.Deficiencies.Any(d => d.Severity >= DeficiencySeverity.Major) == true
                    ? RiskLevel.High : RiskLevel.Low,
                Recommendations = new List<string>
                {
                    "Review deficiencies with project team",
                    "Schedule follow-up inspection if needed"
                },
                PredictedImpact = inspection.Status == InspectionStatus.Failed ? "Schedule delay possible" : "On track",
                Confidence = 0.8
            };

            return Task.FromResult(analysis);
        }

        public Task<PhotoAIAnalysis> AnalyzePhotoAsync(byte[] content, CancellationToken ct)
        {
            // Simulated AI analysis - in production would use computer vision
            var analysis = new PhotoAIAnalysis
            {
                DetectedObjects = new List<DetectedObject>
                {
                    new() { Label = "construction", Confidence = 0.95 }
                },
                DetectedHazards = new List<DetectedHazard>(),
                QualityScore = 0.85
            };

            return Task.FromResult(analysis);
        }

        public Task<SafetyRiskAssessment> AssessSafetyRiskAsync(SafetyObservation observation, CancellationToken ct)
        {
            var assessment = new SafetyRiskAssessment
            {
                RiskLevel = observation.Severity switch
                {
                    SafetySeverity.Critical => RiskLevel.Critical,
                    SafetySeverity.High => RiskLevel.High,
                    SafetySeverity.Medium => RiskLevel.Medium,
                    _ => RiskLevel.Low
                },
                Recommendations = new List<string>
                {
                    "Document the observation thoroughly",
                    "Notify site supervisor immediately if critical"
                },
                SimilarIncidents = new List<string>(),
                PreventiveMeasures = new List<string>
                {
                    "Conduct toolbox talk on this topic",
                    "Review safety procedures with crew"
                }
            };

            return Task.FromResult(assessment);
        }

        public Task<FieldAIInsights> GenerateFieldInsightsAsync(
            List<Inspection> inspections,
            List<SafetyObservation> safetyObs,
            List<QualityRecord> qualityRecords,
            CancellationToken ct)
        {
            var insights = new FieldAIInsights
            {
                KeyFindings = new List<string>
                {
                    $"{inspections.Count(i => i.Status == InspectionStatus.Passed)} of {inspections.Count} inspections passed",
                    $"{safetyObs.Count(o => o.Status == SafetyStatus.Open)} open safety observations",
                    $"Quality pass rate: {(qualityRecords.Any() ? qualityRecords.Count(r => r.PassFail) * 100.0 / qualityRecords.Count : 100):F1}%"
                },
                Recommendations = new List<string>
                {
                    "Continue monitoring quality metrics",
                    "Address open safety observations promptly"
                },
                Trends = new List<FieldTrend>(),
                RiskAreas = new List<FieldRiskArea>()
            };

            return Task.FromResult(insights);
        }
    }

    public class InspectionAIAnalysis
    {
        public RiskLevel RiskLevel { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public string? PredictedImpact { get; set; }
        public double Confidence { get; set; }
    }

    public class PhotoAIAnalysis
    {
        public List<DetectedObject>? DetectedObjects { get; set; }
        public List<DetectedHazard>? DetectedHazards { get; set; }
        public double QualityScore { get; set; }
    }

    public class DetectedObject
    {
        public string Label { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class DetectedHazard
    {
        public string Description { get; set; } = "";
        public HazardType Type { get; set; }
        public double Confidence { get; set; }
    }

    public enum HazardType { FallHazard, Electrical, Chemical, Equipment, Other }

    public class SafetyRiskAssessment
    {
        public RiskLevel RiskLevel { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public List<string> SimilarIncidents { get; set; } = new();
        public List<string> PreventiveMeasures { get; set; } = new();
    }

    public enum RiskLevel { Low, Medium, High, Critical }

    public class FieldAIInsights
    {
        public List<string> KeyFindings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<FieldTrend> Trends { get; set; } = new();
        public List<FieldRiskArea> RiskAreas { get; set; } = new();
    }

    public class FieldTrend
    {
        public string Area { get; set; } = "";
        public TrendDirection Direction { get; set; }
        public string Description { get; set; } = "";
    }

    public enum TrendDirection { Improving, Stable, Declining }

    public class FieldRiskArea
    {
        public string Area { get; set; } = "";
        public RiskLevel Level { get; set; }
        public string Description { get; set; } = "";
    }

    #endregion

    #region Event Args

    public class InspectionCompletedEventArgs : EventArgs
    {
        public Inspection Inspection { get; }
        public InspectionCompletedEventArgs(Inspection inspection) => Inspection = inspection;
    }

    public class SafetyAlertEventArgs : EventArgs
    {
        public AlertType Type { get; }
        public string Message { get; }
        public string SourceId { get; }
        public SafetyAlertEventArgs(AlertType type, string message, string sourceId)
        {
            Type = type;
            Message = message;
            SourceId = sourceId;
        }
    }

    public enum AlertType { SafetyObservation, PhotoHazard, InspectionFailure }

    public class QualityIssueEventArgs : EventArgs
    {
        public Inspection? Inspection { get; }
        public Deficiency Deficiency { get; }
        public QualityIssueEventArgs(Inspection? inspection, Deficiency deficiency)
        {
            Inspection = inspection;
            Deficiency = deficiency;
        }
    }

    public class ChecklistCompletedEventArgs : EventArgs
    {
        public ChecklistInstance Checklist { get; }
        public ChecklistCompletedEventArgs(ChecklistInstance checklist) => Checklist = checklist;
    }

    #endregion

    #region Exceptions

    public class InspectionNotFoundException : Exception
    {
        public string InspectionId { get; }
        public InspectionNotFoundException(string inspectionId) : base($"Inspection not found: {inspectionId}")
            => InspectionId = inspectionId;
    }

    public class ChecklistNotFoundException : Exception
    {
        public string ChecklistId { get; }
        public ChecklistNotFoundException(string checklistId) : base($"Checklist not found: {checklistId}")
            => ChecklistId = checklistId;
    }

    public class ChecklistItemNotFoundException : Exception
    {
        public string ItemId { get; }
        public ChecklistItemNotFoundException(string itemId) : base($"Checklist item not found: {itemId}")
            => ItemId = itemId;
    }

    public class SafetyObservationNotFoundException : Exception
    {
        public string ObservationId { get; }
        public SafetyObservationNotFoundException(string observationId) : base($"Safety observation not found: {observationId}")
            => ObservationId = observationId;
    }

    #endregion
}
