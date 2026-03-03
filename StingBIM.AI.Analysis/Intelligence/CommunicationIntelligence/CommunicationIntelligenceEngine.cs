// ===================================================================
// StingBIM Communication Intelligence Engine - RFI/Submittal Management
// RFI tracking, submittal workflows, response time analytics, correspondence logs
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CommunicationIntelligence
{
    /// <summary>
    /// Comprehensive communication management for RFIs, submittals, and correspondence
    /// with analytics, ball-in-court tracking, and automated workflows
    /// </summary>
    public sealed class CommunicationIntelligenceEngine
    {
        private static readonly Lazy<CommunicationIntelligenceEngine> _instance =
            new Lazy<CommunicationIntelligenceEngine>(() => new CommunicationIntelligenceEngine());
        public static CommunicationIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, CommunicationProject> _projects;
        private readonly Dictionary<string, RFI> _rfis;
        private readonly Dictionary<string, Submittal> _submittals;
        private readonly Dictionary<string, Transmittal> _transmittals;
        private readonly Dictionary<string, Correspondence> _correspondence;
        private readonly List<SubmittalPackageTemplate> _packageTemplates;
        private readonly object _lockObject = new object();

        public event EventHandler<RFIAlertEventArgs> RFIAlert;
        public event EventHandler<SubmittalAlertEventArgs> SubmittalAlert;
        public event EventHandler<ResponseOverdueEventArgs> ResponseOverdue;

        private CommunicationIntelligenceEngine()
        {
            _projects = new Dictionary<string, CommunicationProject>();
            _rfis = new Dictionary<string, RFI>();
            _submittals = new Dictionary<string, Submittal>();
            _transmittals = new Dictionary<string, Transmittal>();
            _correspondence = new Dictionary<string, Correspondence>();
            _packageTemplates = new List<SubmittalPackageTemplate>();
            InitializePackageTemplates();
        }

        #region Project Management

        public CommunicationProject CreateProject(string projectId, string projectName)
        {
            var project = new CommunicationProject
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                CreatedDate = DateTime.Now,
                Status = ProjectStatus.Active,
                RFIs = new List<string>(),
                Submittals = new List<string>(),
                Transmittals = new List<string>(),
                Correspondence = new List<string>(),
                Settings = new ProjectCommunicationSettings
                {
                    DefaultRFIResponseDays = 7,
                    DefaultSubmittalReviewDays = 14,
                    AutoEscalateDays = 3,
                    RequireAcknowledgement = true
                }
            };

            lock (_lockObject)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        #endregion

        #region RFI Management

        public RFI CreateRFI(RFIInput input)
        {
            var rfi = new RFI
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateRFINumber(input.ProjectId),
                Subject = input.Subject,
                Question = input.Question,
                Category = input.Category,
                Priority = input.Priority,
                RequestedBy = input.RequestedBy,
                RequestedByCompany = input.RequestedByCompany,
                AssignedTo = input.AssignedTo,
                AssignedToCompany = input.AssignedToCompany,
                CopiedTo = input.CopiedTo ?? new List<string>(),
                DateCreated = DateTime.Now,
                DateRequired = input.DateRequired ?? DateTime.Now.AddDays(7),
                Status = RFIStatus.Draft,
                ImpactCost = input.ImpactCost,
                ImpactSchedule = input.ImpactSchedule,
                AffectedDrawings = input.AffectedDrawings ?? new List<string>(),
                AffectedSpecSections = input.AffectedSpecSections ?? new List<string>(),
                Location = input.Location,
                Attachments = input.Attachments ?? new List<RFIAttachment>(),
                History = new List<RFIHistoryEntry>()
            };

            rfi.History.Add(new RFIHistoryEntry
            {
                Date = DateTime.Now,
                Action = "Created",
                User = input.RequestedBy,
                Notes = "RFI created"
            });

            lock (_lockObject)
            {
                _rfis[rfi.Id] = rfi;

                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.RFIs.Add(rfi.Id);
                }
            }

            return rfi;
        }

        public void SubmitRFI(string rfiId, string submitter)
        {
            lock (_lockObject)
            {
                if (_rfis.TryGetValue(rfiId, out var rfi))
                {
                    rfi.Status = RFIStatus.Open;
                    rfi.DateSubmitted = DateTime.Now;
                    rfi.BallInCourt = rfi.AssignedTo;

                    rfi.History.Add(new RFIHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Submitted",
                        User = submitter,
                        Notes = $"RFI submitted to {rfi.AssignedTo}"
                    });

                    OnRFIAlert(new RFIAlertEventArgs
                    {
                        RFIId = rfi.Id,
                        RFINumber = rfi.Number,
                        AlertType = RFIAlertType.NewRFI,
                        Message = $"New RFI {rfi.Number}: {rfi.Subject}"
                    });
                }
            }
        }

        public void RespondToRFI(string rfiId, RFIResponse response)
        {
            lock (_lockObject)
            {
                if (_rfis.TryGetValue(rfiId, out var rfi))
                {
                    rfi.Response = response.ResponseText;
                    rfi.RespondedBy = response.RespondedBy;
                    rfi.DateResponded = DateTime.Now;
                    rfi.ResponseAttachments = response.Attachments ?? new List<RFIAttachment>();

                    // Calculate response time
                    if (rfi.DateSubmitted != default)
                    {
                        rfi.ResponseDays = (DateTime.Now - rfi.DateSubmitted).Days;
                    }

                    // Determine status based on response type
                    rfi.Status = response.ResponseType switch
                    {
                        RFIResponseType.Answered => RFIStatus.Answered,
                        RFIResponseType.ForReview => RFIStatus.PendingReview,
                        RFIResponseType.NeedMoreInfo => RFIStatus.NeedsInfo,
                        RFIResponseType.Deferred => RFIStatus.Deferred,
                        _ => RFIStatus.Answered
                    };

                    rfi.BallInCourt = response.ResponseType == RFIResponseType.Answered
                        ? rfi.RequestedBy
                        : rfi.AssignedTo;

                    rfi.History.Add(new RFIHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Response Provided",
                        User = response.RespondedBy,
                        Notes = $"Response type: {response.ResponseType}"
                    });
                }
            }
        }

        public void CloseRFI(string rfiId, string closedBy, string closeNotes)
        {
            lock (_lockObject)
            {
                if (_rfis.TryGetValue(rfiId, out var rfi))
                {
                    rfi.Status = RFIStatus.Closed;
                    rfi.DateClosed = DateTime.Now;
                    rfi.ClosedBy = closedBy;

                    rfi.History.Add(new RFIHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Closed",
                        User = closedBy,
                        Notes = closeNotes
                    });
                }
            }
        }

        private string GenerateRFINumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _rfis.Values.Count(r => r.ProjectId == projectId) + 1;
                return $"RFI-{count:D4}";
            }
        }

        #endregion

        #region Submittal Management

        public Submittal CreateSubmittal(SubmittalInput input)
        {
            var submittal = new Submittal
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateSubmittalNumber(input.ProjectId),
                Title = input.Title,
                Description = input.Description,
                SpecSection = input.SpecSection,
                Category = input.Category,
                Type = input.Type,
                Priority = input.Priority,
                SubmittedBy = input.SubmittedBy,
                SubmittedByCompany = input.SubmittedByCompany,
                DateCreated = DateTime.Now,
                DateRequired = input.DateRequired ?? DateTime.Now.AddDays(14),
                Status = SubmittalStatus.Draft,
                ReviewCycle = 1,
                Items = input.Items ?? new List<SubmittalItem>(),
                ReviewPath = input.ReviewPath ?? new List<ReviewPathStep>(),
                History = new List<SubmittalHistoryEntry>()
            };

            // Set up default review path if not provided
            if (!submittal.ReviewPath.Any())
            {
                submittal.ReviewPath = new List<ReviewPathStep>
                {
                    new ReviewPathStep { Order = 1, Role = "Contractor", Company = input.SubmittedByCompany, Status = ReviewStepStatus.Completed },
                    new ReviewPathStep { Order = 2, Role = "Architect", Company = "Design Team", Status = ReviewStepStatus.Pending },
                    new ReviewPathStep { Order = 3, Role = "Engineer", Company = "Engineering Team", Status = ReviewStepStatus.Pending }
                };
            }

            submittal.History.Add(new SubmittalHistoryEntry
            {
                Date = DateTime.Now,
                Action = "Created",
                User = input.SubmittedBy,
                Notes = "Submittal created"
            });

            lock (_lockObject)
            {
                _submittals[submittal.Id] = submittal;

                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.Submittals.Add(submittal.Id);
                }
            }

            return submittal;
        }

        public void SubmitForReview(string submittalId, string submitter)
        {
            lock (_lockObject)
            {
                if (_submittals.TryGetValue(submittalId, out var submittal))
                {
                    submittal.Status = SubmittalStatus.UnderReview;
                    submittal.DateSubmitted = DateTime.Now;
                    submittal.CurrentReviewStep = 1;

                    // Update review path
                    var currentStep = submittal.ReviewPath.FirstOrDefault(s => s.Order == submittal.CurrentReviewStep);
                    if (currentStep != null)
                    {
                        currentStep.Status = ReviewStepStatus.InProgress;
                        currentStep.ReceivedDate = DateTime.Now;
                        submittal.BallInCourt = currentStep.Role;
                    }

                    submittal.History.Add(new SubmittalHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Submitted for Review",
                        User = submitter,
                        ReviewCycle = submittal.ReviewCycle
                    });

                    OnSubmittalAlert(new SubmittalAlertEventArgs
                    {
                        SubmittalId = submittal.Id,
                        SubmittalNumber = submittal.Number,
                        AlertType = SubmittalAlertType.NewSubmittal,
                        Message = $"New submittal {submittal.Number}: {submittal.Title}"
                    });
                }
            }
        }

        public void ReviewSubmittal(string submittalId, SubmittalReview review)
        {
            lock (_lockObject)
            {
                if (_submittals.TryGetValue(submittalId, out var submittal))
                {
                    // Update current review step
                    var currentStep = submittal.ReviewPath.FirstOrDefault(s => s.Order == submittal.CurrentReviewStep);
                    if (currentStep != null)
                    {
                        currentStep.Status = ReviewStepStatus.Completed;
                        currentStep.ReviewedBy = review.ReviewedBy;
                        currentStep.ReviewedDate = DateTime.Now;
                        currentStep.Decision = review.Decision;
                        currentStep.Comments = review.Comments;
                    }

                    // Add review to history
                    submittal.Reviews = submittal.Reviews ?? new List<SubmittalReview>();
                    review.ReviewDate = DateTime.Now;
                    review.ReviewCycle = submittal.ReviewCycle;
                    submittal.Reviews.Add(review);

                    // Determine next action based on decision
                    switch (review.Decision)
                    {
                        case ReviewDecision.Approved:
                        case ReviewDecision.ApprovedAsNoted:
                            // Move to next reviewer or close
                            var nextStep = submittal.ReviewPath.FirstOrDefault(s => s.Order == submittal.CurrentReviewStep + 1);
                            if (nextStep != null)
                            {
                                submittal.CurrentReviewStep++;
                                nextStep.Status = ReviewStepStatus.InProgress;
                                nextStep.ReceivedDate = DateTime.Now;
                                submittal.BallInCourt = nextStep.Role;
                            }
                            else
                            {
                                // All reviews complete
                                submittal.Status = review.Decision == ReviewDecision.Approved
                                    ? SubmittalStatus.Approved
                                    : SubmittalStatus.ApprovedAsNoted;
                                submittal.DateApproved = DateTime.Now;
                                submittal.BallInCourt = submittal.SubmittedBy;
                            }
                            break;

                        case ReviewDecision.ReviseResubmit:
                            submittal.Status = SubmittalStatus.ReviseResubmit;
                            submittal.BallInCourt = submittal.SubmittedBy;
                            break;

                        case ReviewDecision.Rejected:
                            submittal.Status = SubmittalStatus.Rejected;
                            submittal.BallInCourt = submittal.SubmittedBy;
                            break;
                    }

                    submittal.History.Add(new SubmittalHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = $"Reviewed - {review.Decision}",
                        User = review.ReviewedBy,
                        ReviewCycle = submittal.ReviewCycle,
                        Notes = review.Comments
                    });
                }
            }
        }

        public void ResubmitSubmittal(string submittalId, string submitter, List<SubmittalItem> updatedItems)
        {
            lock (_lockObject)
            {
                if (_submittals.TryGetValue(submittalId, out var submittal))
                {
                    submittal.ReviewCycle++;
                    submittal.Items = updatedItems;
                    submittal.Status = SubmittalStatus.UnderReview;
                    submittal.DateSubmitted = DateTime.Now;
                    submittal.CurrentReviewStep = 1;

                    // Reset review path
                    foreach (var step in submittal.ReviewPath)
                    {
                        if (step.Order == 1)
                        {
                            step.Status = ReviewStepStatus.InProgress;
                            step.ReceivedDate = DateTime.Now;
                        }
                        else
                        {
                            step.Status = ReviewStepStatus.Pending;
                            step.ReceivedDate = default;
                            step.ReviewedDate = default;
                            step.ReviewedBy = null;
                            step.Decision = ReviewDecision.None;
                            step.Comments = null;
                        }
                    }

                    submittal.History.Add(new SubmittalHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Resubmitted",
                        User = submitter,
                        ReviewCycle = submittal.ReviewCycle,
                        Notes = $"Resubmitted for review cycle {submittal.ReviewCycle}"
                    });
                }
            }
        }

        private string GenerateSubmittalNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _submittals.Values.Count(s => s.ProjectId == projectId) + 1;
                return $"SUB-{count:D4}";
            }
        }

        #endregion

        #region Transmittal Management

        public Transmittal CreateTransmittal(TransmittalInput input)
        {
            var transmittal = new Transmittal
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateTransmittalNumber(input.ProjectId),
                Subject = input.Subject,
                From = input.From,
                FromCompany = input.FromCompany,
                To = input.To ?? new List<string>(),
                ToCompanies = input.ToCompanies ?? new List<string>(),
                CC = input.CC ?? new List<string>(),
                DateSent = DateTime.Now,
                Purpose = input.Purpose,
                Notes = input.Notes,
                Items = input.Items ?? new List<TransmittalItem>(),
                RequiresAcknowledgement = input.RequiresAcknowledgement,
                Acknowledgements = new List<TransmittalAcknowledgement>()
            };

            lock (_lockObject)
            {
                _transmittals[transmittal.Id] = transmittal;

                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.Transmittals.Add(transmittal.Id);
                }
            }

            return transmittal;
        }

        public void AcknowledgeTransmittal(string transmittalId, string acknowledgedBy, string company)
        {
            lock (_lockObject)
            {
                if (_transmittals.TryGetValue(transmittalId, out var transmittal))
                {
                    transmittal.Acknowledgements.Add(new TransmittalAcknowledgement
                    {
                        AcknowledgedBy = acknowledgedBy,
                        Company = company,
                        AcknowledgedDate = DateTime.Now
                    });
                }
            }
        }

        private string GenerateTransmittalNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _transmittals.Values.Count(t => t.ProjectId == projectId) + 1;
                return $"TR-{count:D4}";
            }
        }

        #endregion

        #region Ball-in-Court Tracking

        public BallInCourtReport GetBallInCourtReport(string projectId)
        {
            lock (_lockObject)
            {
                var report = new BallInCourtReport
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    ByCompany = new Dictionary<string, BallInCourtSummary>(),
                    ByPerson = new Dictionary<string, BallInCourtSummary>(),
                    OverdueItems = new List<OverdueItem>()
                };

                // RFIs
                var projectRFIs = _rfis.Values
                    .Where(r => r.ProjectId == projectId &&
                               r.Status != RFIStatus.Closed &&
                               r.Status != RFIStatus.Void)
                    .ToList();

                foreach (var rfi in projectRFIs)
                {
                    UpdateBallInCourtSummary(report, rfi.BallInCourt, "RFI", rfi.Priority.ToString());

                    if (rfi.DateRequired < DateTime.Now && rfi.Status != RFIStatus.Answered)
                    {
                        report.OverdueItems.Add(new OverdueItem
                        {
                            Type = "RFI",
                            Number = rfi.Number,
                            Subject = rfi.Subject,
                            DueDate = rfi.DateRequired,
                            DaysOverdue = (DateTime.Now - rfi.DateRequired).Days,
                            BallInCourt = rfi.BallInCourt
                        });
                    }
                }

                // Submittals
                var projectSubmittals = _submittals.Values
                    .Where(s => s.ProjectId == projectId &&
                               s.Status != SubmittalStatus.Approved &&
                               s.Status != SubmittalStatus.ApprovedAsNoted &&
                               s.Status != SubmittalStatus.Void)
                    .ToList();

                foreach (var submittal in projectSubmittals)
                {
                    UpdateBallInCourtSummary(report, submittal.BallInCourt, "Submittal", submittal.Priority.ToString());

                    if (submittal.DateRequired < DateTime.Now)
                    {
                        report.OverdueItems.Add(new OverdueItem
                        {
                            Type = "Submittal",
                            Number = submittal.Number,
                            Subject = submittal.Title,
                            DueDate = submittal.DateRequired,
                            DaysOverdue = (DateTime.Now - submittal.DateRequired).Days,
                            BallInCourt = submittal.BallInCourt
                        });
                    }
                }

                // Sort overdue items
                report.OverdueItems = report.OverdueItems.OrderByDescending(i => i.DaysOverdue).ToList();

                return report;
            }
        }

        private void UpdateBallInCourtSummary(BallInCourtReport report, string ballInCourt, string itemType, string priority)
        {
            if (string.IsNullOrEmpty(ballInCourt)) return;

            if (!report.ByPerson.ContainsKey(ballInCourt))
            {
                report.ByPerson[ballInCourt] = new BallInCourtSummary
                {
                    Name = ballInCourt,
                    TotalItems = 0,
                    RFICount = 0,
                    SubmittalCount = 0,
                    HighPriorityCount = 0
                };
            }

            var summary = report.ByPerson[ballInCourt];
            summary.TotalItems++;

            if (itemType == "RFI") summary.RFICount++;
            else if (itemType == "Submittal") summary.SubmittalCount++;

            if (priority == "High" || priority == "Critical")
                summary.HighPriorityCount++;
        }

        #endregion

        #region Analytics

        public CommunicationAnalytics GetAnalytics(string projectId)
        {
            lock (_lockObject)
            {
                var projectRFIs = _rfis.Values.Where(r => r.ProjectId == projectId).ToList();
                var projectSubmittals = _submittals.Values.Where(s => s.ProjectId == projectId).ToList();

                var analytics = new CommunicationAnalytics
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,

                    // RFI Statistics
                    TotalRFIs = projectRFIs.Count,
                    OpenRFIs = projectRFIs.Count(r => r.Status == RFIStatus.Open ||
                                                     r.Status == RFIStatus.PendingReview),
                    ClosedRFIs = projectRFIs.Count(r => r.Status == RFIStatus.Closed),
                    OverdueRFIs = projectRFIs.Count(r => r.DateRequired < DateTime.Now &&
                                                        r.Status != RFIStatus.Closed &&
                                                        r.Status != RFIStatus.Answered),

                    // Submittal Statistics
                    TotalSubmittals = projectSubmittals.Count,
                    PendingSubmittals = projectSubmittals.Count(s => s.Status == SubmittalStatus.UnderReview ||
                                                                    s.Status == SubmittalStatus.Draft),
                    ApprovedSubmittals = projectSubmittals.Count(s => s.Status == SubmittalStatus.Approved ||
                                                                     s.Status == SubmittalStatus.ApprovedAsNoted),
                    RejectedSubmittals = projectSubmittals.Count(s => s.Status == SubmittalStatus.Rejected),

                    // Response time analytics
                    RFIResponseTimeStats = new ResponseTimeStats(),
                    SubmittalReviewTimeStats = new ResponseTimeStats(),

                    // Breakdown by category
                    RFIsByCategory = new Dictionary<RFICategory, int>(),
                    SubmittalsByCategory = new Dictionary<SubmittalCategory, int>(),

                    // Monthly trends
                    MonthlyTrend = new List<MonthlyCommTrend>()
                };

                // RFI response time
                var answeredRFIs = projectRFIs
                    .Where(r => r.DateResponded != default && r.DateSubmitted != default)
                    .ToList();
                if (answeredRFIs.Any())
                {
                    var responseTimes = answeredRFIs.Select(r => (r.DateResponded - r.DateSubmitted).Days).ToList();
                    analytics.RFIResponseTimeStats = new ResponseTimeStats
                    {
                        Average = responseTimes.Average(),
                        Min = responseTimes.Min(),
                        Max = responseTimes.Max(),
                        Median = responseTimes.OrderBy(t => t).ElementAt(responseTimes.Count / 2)
                    };
                }

                // Submittal review time
                var reviewedSubmittals = projectSubmittals
                    .Where(s => s.DateApproved != default && s.DateSubmitted != default)
                    .ToList();
                if (reviewedSubmittals.Any())
                {
                    var reviewTimes = reviewedSubmittals.Select(s => (s.DateApproved - s.DateSubmitted).Days).ToList();
                    analytics.SubmittalReviewTimeStats = new ResponseTimeStats
                    {
                        Average = reviewTimes.Average(),
                        Min = reviewTimes.Min(),
                        Max = reviewTimes.Max(),
                        Median = reviewTimes.OrderBy(t => t).ElementAt(reviewTimes.Count / 2)
                    };
                }

                // Category breakdown
                foreach (RFICategory category in Enum.GetValues<RFICategory>())
                {
                    analytics.RFIsByCategory[category] = projectRFIs.Count(r => r.Category == category);
                }

                foreach (SubmittalCategory category in Enum.GetValues<SubmittalCategory>())
                {
                    analytics.SubmittalsByCategory[category] = projectSubmittals.Count(s => s.Category == category);
                }

                // Monthly trend
                var allDates = projectRFIs.Select(r => r.DateCreated)
                    .Concat(projectSubmittals.Select(s => s.DateCreated))
                    .Where(d => d != default)
                    .ToList();

                if (allDates.Any())
                {
                    var minDate = allDates.Min();
                    var maxDate = allDates.Max();

                    for (var date = new DateTime(minDate.Year, minDate.Month, 1);
                         date <= maxDate;
                         date = date.AddMonths(1))
                    {
                        analytics.MonthlyTrend.Add(new MonthlyCommTrend
                        {
                            Year = date.Year,
                            Month = date.Month,
                            RFICount = projectRFIs.Count(r => r.DateCreated.Year == date.Year &&
                                                             r.DateCreated.Month == date.Month),
                            SubmittalCount = projectSubmittals.Count(s => s.DateCreated.Year == date.Year &&
                                                                         s.DateCreated.Month == date.Month)
                        });
                    }
                }

                // Calculate averages
                analytics.AverageRFIResponseDays = analytics.RFIResponseTimeStats.Average;
                analytics.AverageSubmittalReviewDays = analytics.SubmittalReviewTimeStats.Average;
                analytics.SubmittalApprovalRate = projectSubmittals.Any()
                    ? (analytics.ApprovedSubmittals * 100.0 / projectSubmittals.Count) : 0;

                return analytics;
            }
        }

        public async Task<ResponseTimeAnalysis> AnalyzeResponseTimesAsync(string projectId, string entityId = null)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var analysis = new ResponseTimeAnalysis
                    {
                        ProjectId = projectId,
                        AnalysisDate = DateTime.Now,
                        ByEntity = new Dictionary<string, EntityResponseMetrics>(),
                        Trends = new List<ResponseTimeTrend>(),
                        Recommendations = new List<string>()
                    };

                    var projectRFIs = _rfis.Values
                        .Where(r => r.ProjectId == projectId)
                        .Where(r => string.IsNullOrEmpty(entityId) || r.AssignedTo == entityId)
                        .ToList();

                    // Group by assigned entity
                    var entityGroups = projectRFIs
                        .Where(r => !string.IsNullOrEmpty(r.AssignedTo))
                        .GroupBy(r => r.AssignedTo);

                    foreach (var group in entityGroups)
                    {
                        var answered = group.Where(r => r.DateResponded != default && r.DateSubmitted != default).ToList();
                        var onTime = answered.Count(r => r.DateResponded <= r.DateRequired);

                        analysis.ByEntity[group.Key] = new EntityResponseMetrics
                        {
                            EntityName = group.Key,
                            TotalAssigned = group.Count(),
                            TotalAnswered = answered.Count,
                            OnTimeResponses = onTime,
                            OnTimePercentage = answered.Any() ? (onTime * 100.0 / answered.Count) : 0,
                            AverageResponseDays = answered.Any()
                                ? answered.Average(r => (r.DateResponded - r.DateSubmitted).Days) : 0,
                            CurrentPending = group.Count(r => r.Status == RFIStatus.Open)
                        };
                    }

                    // Generate recommendations
                    foreach (var entity in analysis.ByEntity.Values)
                    {
                        if (entity.OnTimePercentage < 70)
                        {
                            analysis.Recommendations.Add($"{entity.EntityName}: On-time response rate is {entity.OnTimePercentage:F0}% - needs improvement");
                        }
                        if (entity.AverageResponseDays > 10)
                        {
                            analysis.Recommendations.Add($"{entity.EntityName}: Average response time is {entity.AverageResponseDays:F1} days - consider process improvements");
                        }
                    }

                    return analysis;
                }
            });
        }

        #endregion

        #region Submittal Scheduling

        public SubmittalSchedule GenerateSubmittalSchedule(string projectId, DateTime projectStart, DateTime projectEnd)
        {
            lock (_lockObject)
            {
                var schedule = new SubmittalSchedule
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    ProjectStart = projectStart,
                    ProjectEnd = projectEnd,
                    ScheduledItems = new List<ScheduledSubmittal>(),
                    Milestones = new List<SubmittalMilestone>()
                };

                // Generate schedule based on templates
                foreach (var template in _packageTemplates)
                {
                    var leadTime = template.LeadTimeDays;
                    var requiredDate = projectStart.AddDays(leadTime);

                    if (requiredDate <= projectEnd)
                    {
                        schedule.ScheduledItems.Add(new ScheduledSubmittal
                        {
                            SpecSection = template.SpecSection,
                            Description = template.Name,
                            Category = template.Category,
                            RequiredOnSiteDate = requiredDate,
                            SubmittalDueDate = requiredDate.AddDays(-14), // 14 days review time
                            ReviewDays = 14,
                            Priority = template.Priority
                        });
                    }
                }

                // Add milestones
                schedule.Milestones.Add(new SubmittalMilestone
                {
                    Name = "Foundation Submittals Complete",
                    Date = projectStart.AddDays(30),
                    SubmittalsRequired = schedule.ScheduledItems.Count(s => s.Category == SubmittalCategory.Structural)
                });

                schedule.Milestones.Add(new SubmittalMilestone
                {
                    Name = "Enclosure Submittals Complete",
                    Date = projectStart.AddDays(90),
                    SubmittalsRequired = schedule.ScheduledItems.Count(s => s.Category == SubmittalCategory.Architectural)
                });

                schedule.Milestones.Add(new SubmittalMilestone
                {
                    Name = "MEP Submittals Complete",
                    Date = projectStart.AddDays(120),
                    SubmittalsRequired = schedule.ScheduledItems.Count(s =>
                        s.Category == SubmittalCategory.Mechanical ||
                        s.Category == SubmittalCategory.Electrical ||
                        s.Category == SubmittalCategory.Plumbing)
                });

                // Sort by due date
                schedule.ScheduledItems = schedule.ScheduledItems.OrderBy(s => s.SubmittalDueDate).ToList();

                return schedule;
            }
        }

        #endregion

        #region Helper Methods

        private void InitializePackageTemplates()
        {
            _packageTemplates.AddRange(new[]
            {
                new SubmittalPackageTemplate { SpecSection = "03300", Name = "Cast-in-Place Concrete", Category = SubmittalCategory.Structural, LeadTimeDays = 45, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "05100", Name = "Structural Steel", Category = SubmittalCategory.Structural, LeadTimeDays = 60, Priority = SubmittalPriority.Critical },
                new SubmittalPackageTemplate { SpecSection = "07200", Name = "Thermal Insulation", Category = SubmittalCategory.Architectural, LeadTimeDays = 30, Priority = SubmittalPriority.Medium },
                new SubmittalPackageTemplate { SpecSection = "08100", Name = "Metal Doors and Frames", Category = SubmittalCategory.Architectural, LeadTimeDays = 90, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "08400", Name = "Entrances and Storefronts", Category = SubmittalCategory.Architectural, LeadTimeDays = 120, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "08800", Name = "Glazing", Category = SubmittalCategory.Architectural, LeadTimeDays = 90, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "09200", Name = "Plaster and Gypsum Board", Category = SubmittalCategory.Architectural, LeadTimeDays = 30, Priority = SubmittalPriority.Medium },
                new SubmittalPackageTemplate { SpecSection = "15400", Name = "Plumbing Fixtures", Category = SubmittalCategory.Plumbing, LeadTimeDays = 60, Priority = SubmittalPriority.Medium },
                new SubmittalPackageTemplate { SpecSection = "15500", Name = "Fire Protection", Category = SubmittalCategory.FireProtection, LeadTimeDays = 45, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "15700", Name = "HVAC Equipment", Category = SubmittalCategory.Mechanical, LeadTimeDays = 90, Priority = SubmittalPriority.Critical },
                new SubmittalPackageTemplate { SpecSection = "15800", Name = "Air Distribution", Category = SubmittalCategory.Mechanical, LeadTimeDays = 60, Priority = SubmittalPriority.High },
                new SubmittalPackageTemplate { SpecSection = "16100", Name = "Electrical Wiring", Category = SubmittalCategory.Electrical, LeadTimeDays = 30, Priority = SubmittalPriority.Medium },
                new SubmittalPackageTemplate { SpecSection = "16400", Name = "Switchgear", Category = SubmittalCategory.Electrical, LeadTimeDays = 120, Priority = SubmittalPriority.Critical },
                new SubmittalPackageTemplate { SpecSection = "16500", Name = "Lighting", Category = SubmittalCategory.Electrical, LeadTimeDays = 60, Priority = SubmittalPriority.High }
            });
        }

        public List<string> CheckOverdueItems(string projectId)
        {
            var overdueMessages = new List<string>();

            lock (_lockObject)
            {
                var overdueRFIs = _rfis.Values
                    .Where(r => r.ProjectId == projectId &&
                               r.DateRequired < DateTime.Now &&
                               r.Status != RFIStatus.Closed &&
                               r.Status != RFIStatus.Answered)
                    .ToList();

                foreach (var rfi in overdueRFIs)
                {
                    var daysOverdue = (DateTime.Now - rfi.DateRequired).Days;
                    overdueMessages.Add($"RFI {rfi.Number} is {daysOverdue} days overdue");

                    OnResponseOverdue(new ResponseOverdueEventArgs
                    {
                        ItemType = "RFI",
                        ItemNumber = rfi.Number,
                        DaysOverdue = daysOverdue,
                        BallInCourt = rfi.BallInCourt
                    });
                }

                var overdueSubmittals = _submittals.Values
                    .Where(s => s.ProjectId == projectId &&
                               s.DateRequired < DateTime.Now &&
                               s.Status != SubmittalStatus.Approved &&
                               s.Status != SubmittalStatus.ApprovedAsNoted)
                    .ToList();

                foreach (var submittal in overdueSubmittals)
                {
                    var daysOverdue = (DateTime.Now - submittal.DateRequired).Days;
                    overdueMessages.Add($"Submittal {submittal.Number} is {daysOverdue} days overdue");

                    OnResponseOverdue(new ResponseOverdueEventArgs
                    {
                        ItemType = "Submittal",
                        ItemNumber = submittal.Number,
                        DaysOverdue = daysOverdue,
                        BallInCourt = submittal.BallInCourt
                    });
                }
            }

            return overdueMessages;
        }

        #endregion

        #region Events

        private void OnRFIAlert(RFIAlertEventArgs e) => RFIAlert?.Invoke(this, e);
        private void OnSubmittalAlert(SubmittalAlertEventArgs e) => SubmittalAlert?.Invoke(this, e);
        private void OnResponseOverdue(ResponseOverdueEventArgs e) => ResponseOverdue?.Invoke(this, e);

        #endregion
    }

    #region Data Models

    public class CommunicationProject
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> RFIs { get; set; }
        public List<string> Submittals { get; set; }
        public List<string> Transmittals { get; set; }
        public List<string> Correspondence { get; set; }
        public ProjectCommunicationSettings Settings { get; set; }
    }

    public class ProjectCommunicationSettings
    {
        public int DefaultRFIResponseDays { get; set; }
        public int DefaultSubmittalReviewDays { get; set; }
        public int AutoEscalateDays { get; set; }
        public bool RequireAcknowledgement { get; set; }
    }

    public class RFI
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string Subject { get; set; }
        public string Question { get; set; }
        public RFICategory Category { get; set; }
        public RFIPriority Priority { get; set; }
        public string RequestedBy { get; set; }
        public string RequestedByCompany { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedToCompany { get; set; }
        public List<string> CopiedTo { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateSubmitted { get; set; }
        public DateTime DateRequired { get; set; }
        public DateTime DateResponded { get; set; }
        public DateTime DateClosed { get; set; }
        public RFIStatus Status { get; set; }
        public string BallInCourt { get; set; }
        public bool? ImpactCost { get; set; }
        public bool? ImpactSchedule { get; set; }
        public List<string> AffectedDrawings { get; set; }
        public List<string> AffectedSpecSections { get; set; }
        public string Location { get; set; }
        public List<RFIAttachment> Attachments { get; set; }
        public string Response { get; set; }
        public string RespondedBy { get; set; }
        public List<RFIAttachment> ResponseAttachments { get; set; }
        public string ClosedBy { get; set; }
        public int ResponseDays { get; set; }
        public List<RFIHistoryEntry> History { get; set; }
    }

    public class RFIInput
    {
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public string Question { get; set; }
        public RFICategory Category { get; set; }
        public RFIPriority Priority { get; set; }
        public string RequestedBy { get; set; }
        public string RequestedByCompany { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedToCompany { get; set; }
        public List<string> CopiedTo { get; set; }
        public DateTime? DateRequired { get; set; }
        public bool? ImpactCost { get; set; }
        public bool? ImpactSchedule { get; set; }
        public List<string> AffectedDrawings { get; set; }
        public List<string> AffectedSpecSections { get; set; }
        public string Location { get; set; }
        public List<RFIAttachment> Attachments { get; set; }
    }

    public class RFIResponse
    {
        public string ResponseText { get; set; }
        public string RespondedBy { get; set; }
        public RFIResponseType ResponseType { get; set; }
        public List<RFIAttachment> Attachments { get; set; }
    }

    public class RFIAttachment
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedDate { get; set; }
    }

    public class RFIHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public string Notes { get; set; }
    }

    public class Submittal
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SpecSection { get; set; }
        public SubmittalCategory Category { get; set; }
        public SubmittalType Type { get; set; }
        public SubmittalPriority Priority { get; set; }
        public string SubmittedBy { get; set; }
        public string SubmittedByCompany { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateSubmitted { get; set; }
        public DateTime DateRequired { get; set; }
        public DateTime DateApproved { get; set; }
        public SubmittalStatus Status { get; set; }
        public string BallInCourt { get; set; }
        public int ReviewCycle { get; set; }
        public int CurrentReviewStep { get; set; }
        public List<SubmittalItem> Items { get; set; }
        public List<ReviewPathStep> ReviewPath { get; set; }
        public List<SubmittalReview> Reviews { get; set; }
        public List<SubmittalHistoryEntry> History { get; set; }
    }

    public class SubmittalInput
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SpecSection { get; set; }
        public SubmittalCategory Category { get; set; }
        public SubmittalType Type { get; set; }
        public SubmittalPriority Priority { get; set; }
        public string SubmittedBy { get; set; }
        public string SubmittedByCompany { get; set; }
        public DateTime? DateRequired { get; set; }
        public List<SubmittalItem> Items { get; set; }
        public List<ReviewPathStep> ReviewPath { get; set; }
    }

    public class SubmittalItem
    {
        public string ItemNumber { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class ReviewPathStep
    {
        public int Order { get; set; }
        public string Role { get; set; }
        public string Company { get; set; }
        public ReviewStepStatus Status { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime ReviewedDate { get; set; }
        public string ReviewedBy { get; set; }
        public ReviewDecision Decision { get; set; }
        public string Comments { get; set; }
    }

    public class SubmittalReview
    {
        public string ReviewedBy { get; set; }
        public DateTime ReviewDate { get; set; }
        public int ReviewCycle { get; set; }
        public ReviewDecision Decision { get; set; }
        public string Comments { get; set; }
        public List<SubmittalComment> ItemComments { get; set; }
    }

    public class SubmittalComment
    {
        public string ItemNumber { get; set; }
        public string Comment { get; set; }
    }

    public class SubmittalHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public int ReviewCycle { get; set; }
        public string Notes { get; set; }
    }

    public class Transmittal
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string FromCompany { get; set; }
        public List<string> To { get; set; }
        public List<string> ToCompanies { get; set; }
        public List<string> CC { get; set; }
        public DateTime DateSent { get; set; }
        public TransmittalPurpose Purpose { get; set; }
        public string Notes { get; set; }
        public List<TransmittalItem> Items { get; set; }
        public bool RequiresAcknowledgement { get; set; }
        public List<TransmittalAcknowledgement> Acknowledgements { get; set; }
    }

    public class TransmittalInput
    {
        public string ProjectId { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string FromCompany { get; set; }
        public List<string> To { get; set; }
        public List<string> ToCompanies { get; set; }
        public List<string> CC { get; set; }
        public TransmittalPurpose Purpose { get; set; }
        public string Notes { get; set; }
        public List<TransmittalItem> Items { get; set; }
        public bool RequiresAcknowledgement { get; set; }
    }

    public class TransmittalItem
    {
        public string DocumentNumber { get; set; }
        public string Description { get; set; }
        public string Revision { get; set; }
        public int Copies { get; set; }
        public string Format { get; set; }
    }

    public class TransmittalAcknowledgement
    {
        public string AcknowledgedBy { get; set; }
        public string Company { get; set; }
        public DateTime AcknowledgedDate { get; set; }
    }

    public class Correspondence
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public CorrespondenceType Type { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public List<string> To { get; set; }
        public DateTime Date { get; set; }
        public string Content { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class BallInCourtReport
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public Dictionary<string, BallInCourtSummary> ByCompany { get; set; }
        public Dictionary<string, BallInCourtSummary> ByPerson { get; set; }
        public List<OverdueItem> OverdueItems { get; set; }
    }

    public class BallInCourtSummary
    {
        public string Name { get; set; }
        public int TotalItems { get; set; }
        public int RFICount { get; set; }
        public int SubmittalCount { get; set; }
        public int HighPriorityCount { get; set; }
    }

    public class OverdueItem
    {
        public string Type { get; set; }
        public string Number { get; set; }
        public string Subject { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public string BallInCourt { get; set; }
    }

    public class CommunicationAnalytics
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalRFIs { get; set; }
        public int OpenRFIs { get; set; }
        public int ClosedRFIs { get; set; }
        public int OverdueRFIs { get; set; }
        public int TotalSubmittals { get; set; }
        public int PendingSubmittals { get; set; }
        public int ApprovedSubmittals { get; set; }
        public int RejectedSubmittals { get; set; }
        public double AverageRFIResponseDays { get; set; }
        public double AverageSubmittalReviewDays { get; set; }
        public double SubmittalApprovalRate { get; set; }
        public ResponseTimeStats RFIResponseTimeStats { get; set; }
        public ResponseTimeStats SubmittalReviewTimeStats { get; set; }
        public Dictionary<RFICategory, int> RFIsByCategory { get; set; }
        public Dictionary<SubmittalCategory, int> SubmittalsByCategory { get; set; }
        public List<MonthlyCommTrend> MonthlyTrend { get; set; }
    }

    public class ResponseTimeStats
    {
        public double Average { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Median { get; set; }
    }

    public class MonthlyCommTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int RFICount { get; set; }
        public int SubmittalCount { get; set; }
    }

    public class ResponseTimeAnalysis
    {
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public Dictionary<string, EntityResponseMetrics> ByEntity { get; set; }
        public List<ResponseTimeTrend> Trends { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class EntityResponseMetrics
    {
        public string EntityName { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalAnswered { get; set; }
        public int OnTimeResponses { get; set; }
        public double OnTimePercentage { get; set; }
        public double AverageResponseDays { get; set; }
        public int CurrentPending { get; set; }
    }

    public class ResponseTimeTrend
    {
        public DateTime Date { get; set; }
        public double AverageResponseDays { get; set; }
    }

    public class SubmittalSchedule
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public DateTime ProjectStart { get; set; }
        public DateTime ProjectEnd { get; set; }
        public List<ScheduledSubmittal> ScheduledItems { get; set; }
        public List<SubmittalMilestone> Milestones { get; set; }
    }

    public class ScheduledSubmittal
    {
        public string SpecSection { get; set; }
        public string Description { get; set; }
        public SubmittalCategory Category { get; set; }
        public DateTime RequiredOnSiteDate { get; set; }
        public DateTime SubmittalDueDate { get; set; }
        public int ReviewDays { get; set; }
        public SubmittalPriority Priority { get; set; }
    }

    public class SubmittalMilestone
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int SubmittalsRequired { get; set; }
    }

    public class SubmittalPackageTemplate
    {
        public string SpecSection { get; set; }
        public string Name { get; set; }
        public SubmittalCategory Category { get; set; }
        public int LeadTimeDays { get; set; }
        public SubmittalPriority Priority { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectStatus { Active, OnHold, Completed, Closed }

    public enum RFICategory { Design, Clarification, Substitution, Coordination, SiteCondition, CodeCompliance, Other }

    public enum RFIPriority { Low, Medium, High, Critical }

    public enum RFIStatus { Draft, Open, Answered, PendingReview, NeedsInfo, Deferred, Closed, Void }

    public enum RFIResponseType { Answered, ForReview, NeedMoreInfo, Deferred }

    public enum SubmittalCategory { Architectural, Structural, Mechanical, Electrical, Plumbing, FireProtection, Civil, Other }

    public enum SubmittalType { ProductData, ShopDrawing, Sample, MockUp, Certificate, Manual, Warranty, Other }

    public enum SubmittalPriority { Low, Medium, High, Critical }

    public enum SubmittalStatus { Draft, UnderReview, Approved, ApprovedAsNoted, ReviseResubmit, Rejected, Void }

    public enum ReviewStepStatus { Pending, InProgress, Completed, Skipped }

    public enum ReviewDecision { None, Approved, ApprovedAsNoted, ReviseResubmit, Rejected, SeeComments }

    public enum TransmittalPurpose { ForApproval, ForInformation, ForReview, ForConstruction, AsRequested, ForRecord }

    public enum CorrespondenceType { Letter, Email, Memo, Notice, Meeting }

    public enum RFIAlertType { NewRFI, ResponseReceived, Overdue, Escalated }

    public enum SubmittalAlertType { NewSubmittal, ReviewComplete, Overdue, Resubmitted }

    #endregion

    #region Event Args

    public class RFIAlertEventArgs : EventArgs
    {
        public string RFIId { get; set; }
        public string RFINumber { get; set; }
        public RFIAlertType AlertType { get; set; }
        public string Message { get; set; }
    }

    public class SubmittalAlertEventArgs : EventArgs
    {
        public string SubmittalId { get; set; }
        public string SubmittalNumber { get; set; }
        public SubmittalAlertType AlertType { get; set; }
        public string Message { get; set; }
    }

    public class ResponseOverdueEventArgs : EventArgs
    {
        public string ItemType { get; set; }
        public string ItemNumber { get; set; }
        public int DaysOverdue { get; set; }
        public string BallInCourt { get; set; }
    }

    #endregion
}
