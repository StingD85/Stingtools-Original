using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Automation.FacilityManagement
{
    /// <summary>
    /// Comprehensive facility inspection management.
    /// Handles inspection scheduling, checklists, compliance tracking, and reporting.
    /// </summary>
    public class FacilityInspectionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InspectionSettings _settings;
        private readonly InspectionRepository _repository;
        private readonly ComplianceTracker _complianceTracker;
        private readonly SchedulingEngine _schedulingEngine;

        public FacilityInspectionEngine(InspectionSettings settings = null)
        {
            _settings = settings ?? new InspectionSettings();
            _repository = new InspectionRepository();
            _complianceTracker = new ComplianceTracker();
            _schedulingEngine = new SchedulingEngine(_settings);
        }

        #region Inspection Templates

        /// <summary>
        /// Create an inspection template/checklist.
        /// </summary>
        public async Task<InspectionTemplate> CreateTemplateAsync(
            InspectionTemplateRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating inspection template: {request.Name}");

            var template = new InspectionTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                InspectionType = request.InspectionType,
                Frequency = request.Frequency,
                ApplicableAssetTypes = request.ApplicableAssetTypes,
                ApplicableSpaceTypes = request.ApplicableSpaceTypes,
                RequiredCertifications = request.RequiredCertifications,
                EstimatedDuration = request.EstimatedDuration,
                Version = 1,
                Status = TemplateStatus.Active,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _settings.CurrentUserId
            };

            // Add sections and items
            foreach (var sectionReq in request.Sections)
            {
                var section = new InspectionSection
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = sectionReq.Name,
                    Description = sectionReq.Description,
                    Order = sectionReq.Order,
                    IsRequired = sectionReq.IsRequired
                };

                foreach (var itemReq in sectionReq.Items)
                {
                    section.Items.Add(new InspectionItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Question = itemReq.Question,
                        Description = itemReq.Description,
                        ResponseType = itemReq.ResponseType,
                        Options = itemReq.Options,
                        IsRequired = itemReq.IsRequired,
                        IsCritical = itemReq.IsCritical,
                        FailureTriggersWorkOrder = itemReq.FailureTriggersWorkOrder,
                        RegulatoryReference = itemReq.RegulatoryReference,
                        Order = itemReq.Order
                    });
                }

                template.Sections.Add(section);
            }

            await _repository.AddTemplateAsync(template, cancellationToken);
            return template;
        }

        /// <summary>
        /// Get inspection templates by category.
        /// </summary>
        public async Task<List<InspectionTemplate>> GetTemplatesAsync(
            InspectionCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetTemplatesAsync(category, cancellationToken);
        }

        /// <summary>
        /// Get predefined templates for common inspections.
        /// </summary>
        public List<InspectionTemplate> GetStandardTemplates()
        {
            return new List<InspectionTemplate>
            {
                CreateFireSafetyTemplate(),
                CreateHVACTemplate(),
                CreateElectricalTemplate(),
                CreatePlumbingTemplate(),
                CreateElevatorTemplate(),
                CreateADAComplianceTemplate(),
                CreateGeneralBuildingTemplate()
            };
        }

        #endregion

        #region Inspection Scheduling

        /// <summary>
        /// Schedule an inspection.
        /// </summary>
        public async Task<ScheduledInspection> ScheduleInspectionAsync(
            InspectionScheduleRequest request,
            CancellationToken cancellationToken = default)
        {
            var template = await _repository.GetTemplateAsync(request.TemplateId, cancellationToken);
            if (template == null)
                throw new ArgumentException($"Template {request.TemplateId} not found");

            var scheduled = new ScheduledInspection
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = template.Id,
                TemplateName = template.Name,
                AssetId = request.AssetId,
                SpaceId = request.SpaceId,
                Location = request.Location,
                ScheduledDate = request.ScheduledDate,
                AssignedTo = request.AssignedTo,
                Priority = request.Priority,
                Status = InspectionStatus.Scheduled,
                Notes = request.Notes,
                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddScheduledInspectionAsync(scheduled, cancellationToken);
            Logger.Info($"Scheduled inspection {scheduled.Id} for {scheduled.ScheduledDate:d}");

            return scheduled;
        }

        /// <summary>
        /// Generate inspection schedule based on templates and frequencies.
        /// </summary>
        public async Task<List<ScheduledInspection>> GenerateScheduleAsync(
            ScheduleGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating inspection schedule from {request.StartDate:d} to {request.EndDate:d}");

            var templates = await _repository.GetTemplatesAsync(null, cancellationToken);
            var scheduled = new List<ScheduledInspection>();

            foreach (var template in templates.Where(t => t.Status == TemplateStatus.Active))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dates = _schedulingEngine.CalculateScheduleDates(
                    template.Frequency, request.StartDate, request.EndDate);

                foreach (var date in dates)
                {
                    var inspection = new ScheduledInspection
                    {
                        Id = Guid.NewGuid().ToString(),
                        TemplateId = template.Id,
                        TemplateName = template.Name,
                        ScheduledDate = date,
                        Status = InspectionStatus.Scheduled,
                        CreatedDate = DateTime.UtcNow
                    };

                    scheduled.Add(inspection);
                    await _repository.AddScheduledInspectionAsync(inspection, cancellationToken);
                }
            }

            return scheduled;
        }

        /// <summary>
        /// Get upcoming inspections.
        /// </summary>
        public async Task<List<ScheduledInspection>> GetUpcomingInspectionsAsync(
            int daysAhead = 30,
            CancellationToken cancellationToken = default)
        {
            var endDate = DateTime.UtcNow.AddDays(daysAhead);
            return await _repository.GetScheduledInspectionsAsync(
                DateTime.UtcNow, endDate, cancellationToken);
        }

        /// <summary>
        /// Get overdue inspections.
        /// </summary>
        public async Task<List<ScheduledInspection>> GetOverdueInspectionsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetOverdueInspectionsAsync(cancellationToken);
        }

        #endregion

        #region Inspection Execution

        /// <summary>
        /// Start an inspection.
        /// </summary>
        public async Task<Inspection> StartInspectionAsync(
            string scheduledInspectionId,
            string inspectorId = null,
            CancellationToken cancellationToken = default)
        {
            var scheduled = await _repository.GetScheduledInspectionAsync(scheduledInspectionId, cancellationToken);
            if (scheduled == null)
                throw new ArgumentException($"Scheduled inspection {scheduledInspectionId} not found");

            var template = await _repository.GetTemplateAsync(scheduled.TemplateId, cancellationToken);

            var inspection = new Inspection
            {
                Id = Guid.NewGuid().ToString(),
                ScheduledInspectionId = scheduledInspectionId,
                TemplateId = template.Id,
                TemplateName = template.Name,
                AssetId = scheduled.AssetId,
                SpaceId = scheduled.SpaceId,
                Location = scheduled.Location,
                InspectorId = inspectorId ?? _settings.CurrentUserId,
                StartTime = DateTime.UtcNow,
                Status = InspectionStatus.InProgress
            };

            // Copy sections and items from template
            foreach (var section in template.Sections)
            {
                var resultSection = new InspectionResultSection
                {
                    SectionId = section.Id,
                    SectionName = section.Name
                };

                foreach (var item in section.Items)
                {
                    resultSection.ItemResults.Add(new InspectionItemResult
                    {
                        ItemId = item.Id,
                        Question = item.Question,
                        ResponseType = item.ResponseType,
                        Options = item.Options,
                        IsRequired = item.IsRequired,
                        IsCritical = item.IsCritical
                    });
                }

                inspection.SectionResults.Add(resultSection);
            }

            // Update scheduled inspection status
            scheduled.Status = InspectionStatus.InProgress;
            await _repository.UpdateScheduledInspectionAsync(scheduled, cancellationToken);

            await _repository.AddInspectionAsync(inspection, cancellationToken);
            Logger.Info($"Started inspection {inspection.Id}");

            return inspection;
        }

        /// <summary>
        /// Record response for an inspection item.
        /// </summary>
        public async Task<bool> RecordResponseAsync(
            string inspectionId,
            string itemId,
            InspectionResponse response,
            CancellationToken cancellationToken = default)
        {
            var inspection = await _repository.GetInspectionAsync(inspectionId, cancellationToken);
            if (inspection == null) return false;

            var itemResult = inspection.SectionResults
                .SelectMany(s => s.ItemResults)
                .FirstOrDefault(i => i.ItemId == itemId);

            if (itemResult == null) return false;

            itemResult.Response = response.Value;
            itemResult.ResponseText = response.Text;
            itemResult.Pass = DeterminePassFail(itemResult, response);
            itemResult.Notes = response.Notes;
            itemResult.PhotoUrls = response.PhotoUrls;
            itemResult.ResponseTime = DateTime.UtcNow;

            await _repository.UpdateInspectionAsync(inspection, cancellationToken);
            return true;
        }

        /// <summary>
        /// Complete an inspection.
        /// </summary>
        public async Task<InspectionCompletionResult> CompleteInspectionAsync(
            string inspectionId,
            InspectionCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var inspection = await _repository.GetInspectionAsync(inspectionId, cancellationToken);
            if (inspection == null)
                return new InspectionCompletionResult { Success = false, Error = "Inspection not found" };

            // Validate all required items are answered
            var unansweredRequired = inspection.SectionResults
                .SelectMany(s => s.ItemResults)
                .Where(i => i.IsRequired && i.Response == null)
                .ToList();

            if (unansweredRequired.Any() && !request.ForceComplete)
            {
                return new InspectionCompletionResult
                {
                    Success = false,
                    Error = $"{unansweredRequired.Count} required items not answered",
                    UnansweredItems = unansweredRequired.Select(i => i.ItemId).ToList()
                };
            }

            inspection.EndTime = DateTime.UtcNow;
            inspection.Status = InspectionStatus.Completed;
            inspection.OverallNotes = request.OverallNotes;
            inspection.InspectorSignature = request.InspectorSignature;

            // Calculate results
            var allItems = inspection.SectionResults.SelectMany(s => s.ItemResults).ToList();
            inspection.TotalItems = allItems.Count;
            inspection.PassedItems = allItems.Count(i => i.Pass == true);
            inspection.FailedItems = allItems.Count(i => i.Pass == false);
            inspection.NAItems = allItems.Count(i => i.Response?.ToString() == "N/A");

            inspection.OverallResult = inspection.FailedItems == 0
                ? InspectionResult.Pass
                : allItems.Any(i => i.IsCritical && i.Pass == false)
                    ? InspectionResult.CriticalFail
                    : InspectionResult.Fail;

            // Update scheduled inspection
            var scheduled = await _repository.GetScheduledInspectionAsync(
                inspection.ScheduledInspectionId, cancellationToken);
            if (scheduled != null)
            {
                scheduled.Status = InspectionStatus.Completed;
                scheduled.CompletedDate = DateTime.UtcNow;
                await _repository.UpdateScheduledInspectionAsync(scheduled, cancellationToken);
            }

            await _repository.UpdateInspectionAsync(inspection, cancellationToken);

            // Generate work orders for failed items if configured
            var workOrdersCreated = new List<string>();
            if (_settings.AutoCreateWorkOrders)
            {
                workOrdersCreated = await CreateWorkOrdersForFailuresAsync(inspection, cancellationToken);
            }

            // Update compliance records
            await _complianceTracker.RecordInspectionResultAsync(inspection, cancellationToken);

            Logger.Info($"Completed inspection {inspectionId}: {inspection.OverallResult}");

            return new InspectionCompletionResult
            {
                Success = true,
                Inspection = inspection,
                WorkOrdersCreated = workOrdersCreated
            };
        }

        #endregion

        #region Compliance Tracking

        /// <summary>
        /// Get compliance status for an asset/space.
        /// </summary>
        public async Task<ComplianceStatus> GetComplianceStatusAsync(
            string assetId = null,
            string spaceId = null,
            CancellationToken cancellationToken = default)
        {
            return await _complianceTracker.GetStatusAsync(assetId, spaceId, cancellationToken);
        }

        /// <summary>
        /// Get compliance dashboard for a building.
        /// </summary>
        public async Task<ComplianceDashboard> GetComplianceDashboardAsync(
            string building,
            CancellationToken cancellationToken = default)
        {
            var inspections = await _repository.GetInspectionsByBuildingAsync(building, cancellationToken);
            var scheduled = await _repository.GetScheduledByBuildingAsync(building, cancellationToken);

            var dashboard = new ComplianceDashboard
            {
                Building = building,
                GeneratedDate = DateTime.UtcNow,
                TotalInspections = inspections.Count,
                CompletedInspections = inspections.Count(i => i.Status == InspectionStatus.Completed),
                PassedInspections = inspections.Count(i => i.OverallResult == InspectionResult.Pass),
                FailedInspections = inspections.Count(i => i.OverallResult == InspectionResult.Fail ||
                                                           i.OverallResult == InspectionResult.CriticalFail),
                OverdueInspections = scheduled.Count(s => s.Status == InspectionStatus.Scheduled &&
                                                           s.ScheduledDate < DateTime.UtcNow),
                UpcomingInspections = scheduled.Count(s => s.Status == InspectionStatus.Scheduled &&
                                                            s.ScheduledDate >= DateTime.UtcNow &&
                                                            s.ScheduledDate <= DateTime.UtcNow.AddDays(30))
            };

            dashboard.ComplianceRate = dashboard.TotalInspections > 0
                ? (dashboard.PassedInspections * 100.0) / dashboard.CompletedInspections
                : 100;

            // By category
            dashboard.ByCategory = inspections
                .GroupBy(i => GetTemplateCategory(i.TemplateId))
                .Select(g => new CategoryCompliance
                {
                    Category = g.Key,
                    Total = g.Count(),
                    Passed = g.Count(i => i.OverallResult == InspectionResult.Pass),
                    Failed = g.Count(i => i.OverallResult != InspectionResult.Pass)
                }).ToList();

            return dashboard;
        }

        /// <summary>
        /// Get regulatory compliance report.
        /// </summary>
        public async Task<RegulatoryComplianceReport> GetRegulatoryReportAsync(
            string regulation,
            CancellationToken cancellationToken = default)
        {
            var templates = await _repository.GetTemplatesAsync(null, cancellationToken);
            var relevantTemplates = templates
                .Where(t => t.Sections.Any(s => s.Items.Any(i =>
                    i.RegulatoryReference?.Contains(regulation, StringComparison.OrdinalIgnoreCase) == true)))
                .ToList();

            var report = new RegulatoryComplianceReport
            {
                Regulation = regulation,
                GeneratedDate = DateTime.UtcNow
            };

            foreach (var template in relevantTemplates)
            {
                var inspections = await _repository.GetInspectionsByTemplateAsync(template.Id, cancellationToken);
                var latestInspection = inspections.OrderByDescending(i => i.EndTime).FirstOrDefault();

                report.Requirements.Add(new RegulatoryRequirement
                {
                    TemplateName = template.Name,
                    RequiredFrequency = template.Frequency,
                    LastInspectionDate = latestInspection?.EndTime,
                    LastResult = latestInspection?.OverallResult,
                    IsCompliant = IsRegulatoryCompliant(template, latestInspection)
                });
            }

            report.OverallCompliant = report.Requirements.All(r => r.IsCompliant);
            return report;
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate inspection report.
        /// </summary>
        public async Task<InspectionReport> GenerateReportAsync(
            string inspectionId,
            CancellationToken cancellationToken = default)
        {
            var inspection = await _repository.GetInspectionAsync(inspectionId, cancellationToken);
            if (inspection == null) return null;

            var template = await _repository.GetTemplateAsync(inspection.TemplateId, cancellationToken);

            return new InspectionReport
            {
                InspectionId = inspection.Id,
                TemplateName = template?.Name,
                Location = inspection.Location,
                InspectorId = inspection.InspectorId,
                InspectionDate = inspection.StartTime,
                CompletionDate = inspection.EndTime,
                Duration = inspection.EndTime.HasValue
                    ? inspection.EndTime.Value - inspection.StartTime
                    : TimeSpan.Zero,
                OverallResult = inspection.OverallResult,
                TotalItems = inspection.TotalItems,
                PassedItems = inspection.PassedItems,
                FailedItems = inspection.FailedItems,
                Sections = inspection.SectionResults.Select(s => new ReportSection
                {
                    SectionName = s.SectionName,
                    Items = s.ItemResults.Select(i => new ReportItem
                    {
                        Question = i.Question,
                        Response = i.ResponseText ?? i.Response?.ToString(),
                        Pass = i.Pass,
                        Notes = i.Notes,
                        Photos = i.PhotoUrls
                    }).ToList()
                }).ToList(),
                OverallNotes = inspection.OverallNotes,
                Signature = inspection.InspectorSignature
            };
        }

        /// <summary>
        /// Generate summary report for a period.
        /// </summary>
        public async Task<InspectionSummaryReport> GenerateSummaryReportAsync(
            DateTime fromDate,
            DateTime toDate,
            string building = null,
            CancellationToken cancellationToken = default)
        {
            var inspections = await _repository.GetInspectionsInPeriodAsync(fromDate, toDate, cancellationToken);

            if (!string.IsNullOrEmpty(building))
                inspections = inspections.Where(i => i.Location?.Building == building).ToList();

            var report = new InspectionSummaryReport
            {
                Period = $"{fromDate:d} - {toDate:d}",
                Building = building,
                GeneratedDate = DateTime.UtcNow,
                TotalScheduled = inspections.Count,
                Completed = inspections.Count(i => i.Status == InspectionStatus.Completed),
                Passed = inspections.Count(i => i.OverallResult == InspectionResult.Pass),
                Failed = inspections.Count(i => i.OverallResult == InspectionResult.Fail),
                CriticalFailed = inspections.Count(i => i.OverallResult == InspectionResult.CriticalFail),
                AverageDuration = TimeSpan.FromMinutes(
                    inspections.Where(i => i.EndTime.HasValue)
                        .Select(i => (i.EndTime.Value - i.StartTime).TotalMinutes)
                        .DefaultIfEmpty(0)
                        .Average())
            };

            report.ComplianceRate = report.Completed > 0
                ? (report.Passed * 100.0) / report.Completed
                : 100;

            report.ByInspector = inspections
                .GroupBy(i => i.InspectorId)
                .Select(g => new InspectorSummary
                {
                    InspectorId = g.Key,
                    InspectionsCompleted = g.Count(i => i.Status == InspectionStatus.Completed),
                    PassRate = g.Any() ? (g.Count(i => i.OverallResult == InspectionResult.Pass) * 100.0) / g.Count() : 0
                }).ToList();

            return report;
        }

        #endregion

        #region Private Methods

        private bool DeterminePassFail(InspectionItemResult item, InspectionResponse response)
        {
            if (response.Value == null) return false;

            return item.ResponseType switch
            {
                ResponseType.YesNo => response.Value.ToString().Equals("Yes", StringComparison.OrdinalIgnoreCase),
                ResponseType.PassFail => response.Value.ToString().Equals("Pass", StringComparison.OrdinalIgnoreCase),
                ResponseType.Rating => Convert.ToInt32(response.Value) >= 3,
                ResponseType.Numeric => IsNumericInRange(response.Value, item.Options),
                _ => true
            };
        }

        private bool IsNumericInRange(object value, List<string> options)
        {
            if (!double.TryParse(value?.ToString(), out var num)) return false;
            if (options == null || options.Count < 2) return true;

            if (double.TryParse(options[0], out var min) && double.TryParse(options[1], out var max))
                return num >= min && num <= max;

            return true;
        }

        private async Task<List<string>> CreateWorkOrdersForFailuresAsync(
            Inspection inspection, CancellationToken ct)
        {
            var workOrderIds = new List<string>();

            var failedItems = inspection.SectionResults
                .SelectMany(s => s.ItemResults)
                .Where(i => i.Pass == false && i.IsCritical);

            foreach (var item in failedItems)
            {
                // Would integrate with WorkOrderManager
                var workOrderId = $"WO-{Guid.NewGuid().ToString().Substring(0, 8)}";
                workOrderIds.Add(workOrderId);
                Logger.Info($"Created work order {workOrderId} for failed inspection item {item.ItemId}");
            }

            return await Task.FromResult(workOrderIds);
        }

        private InspectionCategory GetTemplateCategory(string templateId)
        {
            // Would look up from repository
            return InspectionCategory.General;
        }

        private bool IsRegulatoryCompliant(InspectionTemplate template, Inspection lastInspection)
        {
            if (lastInspection == null) return false;
            if (lastInspection.OverallResult != InspectionResult.Pass) return false;

            var maxAge = template.Frequency switch
            {
                InspectionFrequency.Daily => TimeSpan.FromDays(1),
                InspectionFrequency.Weekly => TimeSpan.FromDays(7),
                InspectionFrequency.Monthly => TimeSpan.FromDays(31),
                InspectionFrequency.Quarterly => TimeSpan.FromDays(92),
                InspectionFrequency.SemiAnnual => TimeSpan.FromDays(183),
                InspectionFrequency.Annual => TimeSpan.FromDays(365),
                _ => TimeSpan.FromDays(365)
            };

            return lastInspection.EndTime.HasValue &&
                   (DateTime.UtcNow - lastInspection.EndTime.Value) <= maxAge;
        }

        #region Standard Templates

        private InspectionTemplate CreateFireSafetyTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-FIRE-001",
                Name = "Fire Safety Inspection",
                Category = InspectionCategory.FireSafety,
                Frequency = InspectionFrequency.Monthly,
                Sections = new List<InspectionSection>
                {
                    new()
                    {
                        Name = "Fire Extinguishers",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Extinguisher present and accessible", ResponseType = ResponseType.YesNo, IsRequired = true, IsCritical = true },
                            new() { Question = "Pressure gauge in green zone", ResponseType = ResponseType.YesNo, IsRequired = true },
                            new() { Question = "Inspection tag current", ResponseType = ResponseType.YesNo, IsRequired = true },
                            new() { Question = "No visible damage or corrosion", ResponseType = ResponseType.YesNo, IsRequired = true }
                        }
                    },
                    new()
                    {
                        Name = "Emergency Exits",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Exit signs illuminated", ResponseType = ResponseType.YesNo, IsRequired = true, IsCritical = true },
                            new() { Question = "Exit paths clear of obstruction", ResponseType = ResponseType.YesNo, IsRequired = true, IsCritical = true },
                            new() { Question = "Emergency lighting functional", ResponseType = ResponseType.YesNo, IsRequired = true }
                        }
                    },
                    new()
                    {
                        Name = "Fire Alarm System",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Panel shows normal status", ResponseType = ResponseType.YesNo, IsRequired = true, IsCritical = true },
                            new() { Question = "No trouble signals", ResponseType = ResponseType.YesNo, IsRequired = true }
                        }
                    }
                }
            };
        }

        private InspectionTemplate CreateHVACTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-HVAC-001",
                Name = "HVAC System Inspection",
                Category = InspectionCategory.HVAC,
                Frequency = InspectionFrequency.Monthly,
                Sections = new List<InspectionSection>
                {
                    new()
                    {
                        Name = "Air Handling Units",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Filter condition", ResponseType = ResponseType.Rating, IsRequired = true },
                            new() { Question = "Belt condition", ResponseType = ResponseType.PassFail, IsRequired = true },
                            new() { Question = "No unusual noise/vibration", ResponseType = ResponseType.YesNo, IsRequired = true },
                            new() { Question = "Coils clean", ResponseType = ResponseType.YesNo, IsRequired = true }
                        }
                    },
                    new()
                    {
                        Name = "Temperature Control",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Supply air temperature (°F)", ResponseType = ResponseType.Numeric, Options = new List<string> { "50", "65" } },
                            new() { Question = "Return air temperature (°F)", ResponseType = ResponseType.Numeric, Options = new List<string> { "70", "78" } }
                        }
                    }
                }
            };
        }

        private InspectionTemplate CreateElectricalTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-ELEC-001",
                Name = "Electrical System Inspection",
                Category = InspectionCategory.Electrical,
                Frequency = InspectionFrequency.Quarterly,
                Sections = new List<InspectionSection>
                {
                    new()
                    {
                        Name = "Electrical Panels",
                        Items = new List<InspectionItem>
                        {
                            new() { Question = "Panel covers secure", ResponseType = ResponseType.YesNo, IsRequired = true },
                            new() { Question = "No evidence of overheating", ResponseType = ResponseType.YesNo, IsRequired = true, IsCritical = true },
                            new() { Question = "Breakers labeled correctly", ResponseType = ResponseType.YesNo, IsRequired = true },
                            new() { Question = "Adequate clearance maintained", ResponseType = ResponseType.YesNo, IsRequired = true }
                        }
                    }
                }
            };
        }

        private InspectionTemplate CreatePlumbingTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-PLMB-001",
                Name = "Plumbing System Inspection",
                Category = InspectionCategory.Plumbing,
                Frequency = InspectionFrequency.Monthly
            };
        }

        private InspectionTemplate CreateElevatorTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-ELEV-001",
                Name = "Elevator Inspection",
                Category = InspectionCategory.Elevator,
                Frequency = InspectionFrequency.Monthly
            };
        }

        private InspectionTemplate CreateADAComplianceTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-ADA-001",
                Name = "ADA Compliance Inspection",
                Category = InspectionCategory.Accessibility,
                Frequency = InspectionFrequency.Annual
            };
        }

        private InspectionTemplate CreateGeneralBuildingTemplate()
        {
            return new InspectionTemplate
            {
                Id = "TMPL-GEN-001",
                Name = "General Building Inspection",
                Category = InspectionCategory.General,
                Frequency = InspectionFrequency.Weekly
            };
        }

        #endregion

        #endregion
    }

    #region Supporting Classes

    internal class InspectionRepository
    {
        private readonly List<InspectionTemplate> _templates = new();
        private readonly List<ScheduledInspection> _scheduled = new();
        private readonly List<Inspection> _inspections = new();

        public Task AddTemplateAsync(InspectionTemplate t, CancellationToken ct) { _templates.Add(t); return Task.CompletedTask; }
        public Task<InspectionTemplate> GetTemplateAsync(string id, CancellationToken ct) => Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));
        public Task<List<InspectionTemplate>> GetTemplatesAsync(InspectionCategory? cat, CancellationToken ct) =>
            Task.FromResult(cat.HasValue ? _templates.Where(t => t.Category == cat).ToList() : _templates.ToList());

        public Task AddScheduledInspectionAsync(ScheduledInspection s, CancellationToken ct) { _scheduled.Add(s); return Task.CompletedTask; }
        public Task<ScheduledInspection> GetScheduledInspectionAsync(string id, CancellationToken ct) => Task.FromResult(_scheduled.FirstOrDefault(s => s.Id == id));
        public Task UpdateScheduledInspectionAsync(ScheduledInspection s, CancellationToken ct) { var idx = _scheduled.FindIndex(x => x.Id == s.Id); if (idx >= 0) _scheduled[idx] = s; return Task.CompletedTask; }
        public Task<List<ScheduledInspection>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken ct) =>
            Task.FromResult(_scheduled.Where(s => s.ScheduledDate >= from && s.ScheduledDate <= to).ToList());
        public Task<List<ScheduledInspection>> GetOverdueInspectionsAsync(CancellationToken ct) =>
            Task.FromResult(_scheduled.Where(s => s.Status == InspectionStatus.Scheduled && s.ScheduledDate < DateTime.UtcNow).ToList());
        public Task<List<ScheduledInspection>> GetScheduledByBuildingAsync(string building, CancellationToken ct) =>
            Task.FromResult(_scheduled.Where(s => s.Location?.Building == building).ToList());

        public Task AddInspectionAsync(Inspection i, CancellationToken ct) { _inspections.Add(i); return Task.CompletedTask; }
        public Task<Inspection> GetInspectionAsync(string id, CancellationToken ct) => Task.FromResult(_inspections.FirstOrDefault(i => i.Id == id));
        public Task UpdateInspectionAsync(Inspection i, CancellationToken ct) { var idx = _inspections.FindIndex(x => x.Id == i.Id); if (idx >= 0) _inspections[idx] = i; return Task.CompletedTask; }
        public Task<List<Inspection>> GetInspectionsByBuildingAsync(string building, CancellationToken ct) =>
            Task.FromResult(_inspections.Where(i => i.Location?.Building == building).ToList());
        public Task<List<Inspection>> GetInspectionsByTemplateAsync(string templateId, CancellationToken ct) =>
            Task.FromResult(_inspections.Where(i => i.TemplateId == templateId).ToList());
        public Task<List<Inspection>> GetInspectionsInPeriodAsync(DateTime from, DateTime to, CancellationToken ct) =>
            Task.FromResult(_inspections.Where(i => i.StartTime >= from && i.StartTime <= to).ToList());
    }

    internal class ComplianceTracker
    {
        public Task RecordInspectionResultAsync(Inspection i, CancellationToken ct) => Task.CompletedTask;
        public Task<ComplianceStatus> GetStatusAsync(string assetId, string spaceId, CancellationToken ct) =>
            Task.FromResult(new ComplianceStatus { IsCompliant = true });
    }

    internal class SchedulingEngine
    {
        private readonly InspectionSettings _settings;
        public SchedulingEngine(InspectionSettings settings) => _settings = settings;

        public List<DateTime> CalculateScheduleDates(InspectionFrequency freq, DateTime start, DateTime end)
        {
            var dates = new List<DateTime>();
            var interval = freq switch
            {
                InspectionFrequency.Daily => TimeSpan.FromDays(1),
                InspectionFrequency.Weekly => TimeSpan.FromDays(7),
                InspectionFrequency.Monthly => TimeSpan.FromDays(30),
                InspectionFrequency.Quarterly => TimeSpan.FromDays(91),
                InspectionFrequency.SemiAnnual => TimeSpan.FromDays(182),
                InspectionFrequency.Annual => TimeSpan.FromDays(365),
                _ => TimeSpan.FromDays(30)
            };

            var current = start;
            while (current <= end) { dates.Add(current); current = current.Add(interval); }
            return dates;
        }
    }

    #endregion

    #region Data Models

    public class InspectionSettings
    {
        public string CurrentUserId { get; set; } = "System";
        public bool AutoCreateWorkOrders { get; set; } = true;
    }

    public class InspectionTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public InspectionCategory Category { get; set; }
        public InspectionType InspectionType { get; set; }
        public InspectionFrequency Frequency { get; set; }
        public List<string> ApplicableAssetTypes { get; set; } = new();
        public List<string> ApplicableSpaceTypes { get; set; } = new();
        public List<string> RequiredCertifications { get; set; } = new();
        public TimeSpan EstimatedDuration { get; set; }
        public List<InspectionSection> Sections { get; set; } = new();
        public int Version { get; set; }
        public TemplateStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InspectionSection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public List<InspectionItem> Items { get; set; } = new();
    }

    public class InspectionItem
    {
        public string Id { get; set; }
        public string Question { get; set; }
        public string Description { get; set; }
        public ResponseType ResponseType { get; set; }
        public List<string> Options { get; set; } = new();
        public bool IsRequired { get; set; }
        public bool IsCritical { get; set; }
        public bool FailureTriggersWorkOrder { get; set; }
        public string RegulatoryReference { get; set; }
        public int Order { get; set; }
    }

    public class InspectionTemplateRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public InspectionCategory Category { get; set; }
        public InspectionType InspectionType { get; set; }
        public InspectionFrequency Frequency { get; set; }
        public List<string> ApplicableAssetTypes { get; set; }
        public List<string> ApplicableSpaceTypes { get; set; }
        public List<string> RequiredCertifications { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public List<SectionRequest> Sections { get; set; } = new();
    }

    public class SectionRequest { public string Name { get; set; } public string Description { get; set; } public int Order { get; set; } public bool IsRequired { get; set; } public List<ItemRequest> Items { get; set; } = new(); }
    public class ItemRequest { public string Question { get; set; } public string Description { get; set; } public ResponseType ResponseType { get; set; } public List<string> Options { get; set; } public bool IsRequired { get; set; } public bool IsCritical { get; set; } public bool FailureTriggersWorkOrder { get; set; } public string RegulatoryReference { get; set; } public int Order { get; set; } }

    public class ScheduledInspection
    {
        public string Id { get; set; }
        public string TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string AssetId { get; set; }
        public string SpaceId { get; set; }
        public AssetLocation Location { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedTo { get; set; }
        public InspectionPriority Priority { get; set; }
        public InspectionStatus Status { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class InspectionScheduleRequest { public string TemplateId { get; set; } public string AssetId { get; set; } public string SpaceId { get; set; } public AssetLocation Location { get; set; } public DateTime ScheduledDate { get; set; } public string AssignedTo { get; set; } public InspectionPriority Priority { get; set; } public string Notes { get; set; } }
    public class ScheduleGenerationRequest { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string Building { get; set; } }

    public class Inspection
    {
        public string Id { get; set; }
        public string ScheduledInspectionId { get; set; }
        public string TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string AssetId { get; set; }
        public string SpaceId { get; set; }
        public AssetLocation Location { get; set; }
        public string InspectorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public InspectionStatus Status { get; set; }
        public InspectionResult OverallResult { get; set; }
        public int TotalItems { get; set; }
        public int PassedItems { get; set; }
        public int FailedItems { get; set; }
        public int NAItems { get; set; }
        public string OverallNotes { get; set; }
        public string InspectorSignature { get; set; }
        public List<InspectionResultSection> SectionResults { get; } = new();
    }

    public class InspectionResultSection { public string SectionId { get; set; } public string SectionName { get; set; } public List<InspectionItemResult> ItemResults { get; } = new(); }
    public class InspectionItemResult { public string ItemId { get; set; } public string Question { get; set; } public ResponseType ResponseType { get; set; } public List<string> Options { get; set; } public bool IsRequired { get; set; } public bool IsCritical { get; set; } public object Response { get; set; } public string ResponseText { get; set; } public bool? Pass { get; set; } public string Notes { get; set; } public List<string> PhotoUrls { get; set; } public DateTime? ResponseTime { get; set; } }

    public class InspectionResponse { public object Value { get; set; } public string Text { get; set; } public string Notes { get; set; } public List<string> PhotoUrls { get; set; } }
    public class InspectionCompletionRequest { public string OverallNotes { get; set; } public string InspectorSignature { get; set; } public bool ForceComplete { get; set; } }
    public class InspectionCompletionResult { public bool Success { get; set; } public Inspection Inspection { get; set; } public List<string> WorkOrdersCreated { get; set; } public string Error { get; set; } public List<string> UnansweredItems { get; set; } }

    public class ComplianceStatus { public bool IsCompliant { get; set; } public DateTime? LastInspection { get; set; } public DateTime? NextDue { get; set; } }
    public class ComplianceDashboard { public string Building { get; set; } public DateTime GeneratedDate { get; set; } public int TotalInspections { get; set; } public int CompletedInspections { get; set; } public int PassedInspections { get; set; } public int FailedInspections { get; set; } public int OverdueInspections { get; set; } public int UpcomingInspections { get; set; } public double ComplianceRate { get; set; } public List<CategoryCompliance> ByCategory { get; set; } }
    public class CategoryCompliance { public InspectionCategory Category { get; set; } public int Total { get; set; } public int Passed { get; set; } public int Failed { get; set; } }
    public class RegulatoryComplianceReport { public string Regulation { get; set; } public DateTime GeneratedDate { get; set; } public bool OverallCompliant { get; set; } public List<RegulatoryRequirement> Requirements { get; } = new(); }
    public class RegulatoryRequirement { public string TemplateName { get; set; } public InspectionFrequency RequiredFrequency { get; set; } public DateTime? LastInspectionDate { get; set; } public InspectionResult? LastResult { get; set; } public bool IsCompliant { get; set; } }

    public class InspectionReport { public string InspectionId { get; set; } public string TemplateName { get; set; } public AssetLocation Location { get; set; } public string InspectorId { get; set; } public DateTime InspectionDate { get; set; } public DateTime? CompletionDate { get; set; } public TimeSpan Duration { get; set; } public InspectionResult OverallResult { get; set; } public int TotalItems { get; set; } public int PassedItems { get; set; } public int FailedItems { get; set; } public List<ReportSection> Sections { get; set; } public string OverallNotes { get; set; } public string Signature { get; set; } }
    public class InspectionSummaryReport { public string Period { get; set; } public string Building { get; set; } public DateTime GeneratedDate { get; set; } public int TotalScheduled { get; set; } public int Completed { get; set; } public int Passed { get; set; } public int Failed { get; set; } public int CriticalFailed { get; set; } public TimeSpan AverageDuration { get; set; } public double ComplianceRate { get; set; } public List<InspectorSummary> ByInspector { get; set; } }
    public class InspectorSummary { public string InspectorId { get; set; } public int InspectionsCompleted { get; set; } public double PassRate { get; set; } }

    // Enums
    public enum InspectionCategory { General, FireSafety, HVAC, Electrical, Plumbing, Elevator, Roofing, Structural, Accessibility, Security, Environmental }
    public enum InspectionType { Routine, Preventive, Corrective, Certification, Regulatory, Safety }
    public enum InspectionFrequency { Daily, Weekly, BiWeekly, Monthly, Quarterly, SemiAnnual, Annual, AsNeeded }
    public enum ResponseType { YesNo, PassFail, Rating, Numeric, Text, MultiChoice, Photo }
    public enum InspectionStatus { Scheduled, InProgress, Completed, Cancelled, Overdue }
    public enum InspectionResult { Pass, Fail, CriticalFail, Incomplete }
    public enum InspectionPriority { Low, Medium, High, Critical }
    public enum TemplateStatus { Draft, Active, Archived }

    #endregion
}
