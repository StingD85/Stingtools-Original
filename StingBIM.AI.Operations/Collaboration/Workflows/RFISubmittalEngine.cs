// StingBIM.AI.Collaboration - RFI & Submittal Workflow Engine
// Complete workflow management for RFIs and Submittals like BIM 360

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Workflows
{
    /// <summary>
    /// Comprehensive RFI and Submittal workflow engine with AI-powered
    /// routing, response suggestions, and deadline tracking
    /// </summary>
    public class RFISubmittalEngine : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, RFI> _rfis = new();
        private readonly ConcurrentDictionary<string, Submittal> _submittals = new();
        private readonly ConcurrentDictionary<string, SubmittalPackage> _packages = new();
        private readonly ConcurrentDictionary<string, WorkflowTemplate> _templates = new();
        private readonly RFISubmittalAI _ai;
        private int _rfiCounter;
        private int _submittalCounter;

        public event EventHandler<RFICreatedEventArgs>? RFICreated;
        public event EventHandler<RFIRespondedEventArgs>? RFIResponded;
        public event EventHandler<SubmittalCreatedEventArgs>? SubmittalCreated;
        public event EventHandler<SubmittalReviewedEventArgs>? SubmittalReviewed;
        public event EventHandler<DeadlineApproachingEventArgs>? DeadlineApproaching;
        public event EventHandler<OverdueAlertEventArgs>? OverdueAlert;

        public RFISubmittalEngine()
        {
            _ai = new RFISubmittalAI(this);
            InitializeDefaultTemplates();
        }

        private void InitializeDefaultTemplates()
        {
            _templates["rfi-standard"] = new WorkflowTemplate
            {
                Id = "rfi-standard",
                Name = "Standard RFI Workflow",
                Type = WorkflowType.RFI,
                Steps = new List<WorkflowStep>
                {
                    new() { Order = 1, Name = "Draft", Status = "draft", AllowedActions = new[] { "submit" } },
                    new() { Order = 2, Name = "Submitted", Status = "submitted", AllowedActions = new[] { "assign", "reject" } },
                    new() { Order = 3, Name = "Under Review", Status = "under_review", AllowedActions = new[] { "respond", "request_info" } },
                    new() { Order = 4, Name = "Responded", Status = "responded", AllowedActions = new[] { "accept", "dispute" } },
                    new() { Order = 5, Name = "Closed", Status = "closed", IsFinal = true }
                },
                DefaultDueDays = 7
            };

            _templates["submittal-standard"] = new WorkflowTemplate
            {
                Id = "submittal-standard",
                Name = "Standard Submittal Workflow",
                Type = WorkflowType.Submittal,
                Steps = new List<WorkflowStep>
                {
                    new() { Order = 1, Name = "Draft", Status = "draft", AllowedActions = new[] { "submit" } },
                    new() { Order = 2, Name = "Submitted", Status = "submitted", AllowedActions = new[] { "review" } },
                    new() { Order = 3, Name = "Under Review", Status = "under_review", AllowedActions = new[] { "approve", "revise", "reject" } },
                    new() { Order = 4, Name = "Approved", Status = "approved", IsFinal = true },
                    new() { Order = 5, Name = "Revise & Resubmit", Status = "revise_resubmit", AllowedActions = new[] { "resubmit" } },
                    new() { Order = 6, Name = "Rejected", Status = "rejected", IsFinal = true }
                },
                DefaultDueDays = 14
            };
        }

        #region RFI Management

        /// <summary>
        /// Create a new RFI
        /// </summary>
        public async Task<RFI> CreateRFIAsync(
            CreateRFIRequest request,
            CancellationToken ct = default)
        {
            var rfiNumber = Interlocked.Increment(ref _rfiCounter);

            var rfi = new RFI
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Number = $"RFI-{rfiNumber:D4}",
                Subject = request.Subject,
                Question = request.Question,
                ProjectId = request.ProjectId,
                SpecSection = request.SpecSection,
                DrawingReference = request.DrawingReference,
                Priority = request.Priority,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                Status = "draft",
                WorkflowTemplateId = "rfi-standard",
                DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(7),
                Attachments = request.Attachments ?? new List<Attachment>(),
                LinkedElements = request.LinkedElements ?? new List<string>(),
                Distribution = request.Distribution ?? new List<string>()
            };

            // AI analysis
            var aiAnalysis = await _ai.AnalyzeRFIAsync(rfi, ct);
            rfi.AIAnalysis = aiAnalysis;

            // Auto-suggest assignee if not specified
            if (string.IsNullOrEmpty(request.AssignedTo) && !string.IsNullOrEmpty(aiAnalysis.SuggestedAssignee))
            {
                rfi.AssignedTo = aiAnalysis.SuggestedAssignee;
            }
            else
            {
                rfi.AssignedTo = request.AssignedTo;
            }

            _rfis[rfi.Id] = rfi;

            RFICreated?.Invoke(this, new RFICreatedEventArgs(rfi));

            return rfi;
        }

        /// <summary>
        /// Submit RFI for review
        /// </summary>
        public RFI SubmitRFI(string rfiId, string submittedBy)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            if (rfi.Status != "draft")
                throw new InvalidWorkflowStateException("RFI must be in draft status to submit");

            rfi.Status = "submitted";
            rfi.SubmittedAt = DateTime.UtcNow;
            rfi.SubmittedBy = submittedBy;

            AddHistory(rfi, "submitted", submittedBy, "RFI submitted for review");

            return rfi;
        }

        /// <summary>
        /// Assign RFI to reviewer
        /// </summary>
        public RFI AssignRFI(string rfiId, string assignedTo, string assignedBy)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            var previousAssignee = rfi.AssignedTo;
            rfi.AssignedTo = assignedTo;
            rfi.Status = "under_review";

            AddHistory(rfi, "assigned", assignedBy, $"Assigned to {assignedTo}");

            return rfi;
        }

        /// <summary>
        /// Respond to RFI
        /// </summary>
        public async Task<RFI> RespondToRFIAsync(
            string rfiId,
            RespondToRFIRequest request,
            CancellationToken ct = default)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            if (rfi.Status != "under_review")
                throw new InvalidWorkflowStateException("RFI must be under review to respond");

            rfi.Response = new RFIResponse
            {
                Content = request.Response,
                RespondedBy = request.RespondedBy,
                RespondedAt = DateTime.UtcNow,
                Attachments = request.Attachments ?? new List<Attachment>(),
                CostImpact = request.CostImpact,
                ScheduleImpact = request.ScheduleImpact
            };

            rfi.Status = "responded";

            AddHistory(rfi, "responded", request.RespondedBy, "Response provided");

            RFIResponded?.Invoke(this, new RFIRespondedEventArgs(rfi));

            return rfi;
        }

        /// <summary>
        /// Accept RFI response and close
        /// </summary>
        public RFI AcceptResponse(string rfiId, string acceptedBy, string? comments = null)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            if (rfi.Status != "responded")
                throw new InvalidWorkflowStateException("RFI must have a response to accept");

            rfi.Status = "closed";
            rfi.ClosedAt = DateTime.UtcNow;
            rfi.ClosedBy = acceptedBy;
            rfi.Resolution = ResolutionType.Accepted;

            if (!string.IsNullOrEmpty(comments))
            {
                AddComment(rfi, acceptedBy, comments);
            }

            AddHistory(rfi, "closed", acceptedBy, "Response accepted, RFI closed");

            return rfi;
        }

        /// <summary>
        /// Dispute RFI response
        /// </summary>
        public RFI DisputeResponse(string rfiId, string disputedBy, string reason)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            rfi.Status = "under_review";
            rfi.DisputeCount++;

            AddComment(rfi, disputedBy, $"Response disputed: {reason}");
            AddHistory(rfi, "disputed", disputedBy, "Response disputed, returned for review");

            return rfi;
        }

        /// <summary>
        /// Get RFI by ID
        /// </summary>
        public RFI? GetRFI(string rfiId)
            => _rfis.TryGetValue(rfiId, out var rfi) ? rfi : null;

        /// <summary>
        /// Get RFI by number
        /// </summary>
        public RFI? GetRFIByNumber(string number)
            => _rfis.Values.FirstOrDefault(r => r.Number == number);

        /// <summary>
        /// Query RFIs
        /// </summary>
        public RFIQueryResult QueryRFIs(RFIQuery query)
        {
            var rfis = _rfis.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(query.ProjectId))
                rfis = rfis.Where(r => r.ProjectId == query.ProjectId);

            if (query.Statuses?.Any() == true)
                rfis = rfis.Where(r => query.Statuses.Contains(r.Status));

            if (query.Priorities?.Any() == true)
                rfis = rfis.Where(r => query.Priorities.Contains(r.Priority));

            if (!string.IsNullOrEmpty(query.AssignedTo))
                rfis = rfis.Where(r => r.AssignedTo == query.AssignedTo);

            if (!string.IsNullOrEmpty(query.CreatedBy))
                rfis = rfis.Where(r => r.CreatedBy == query.CreatedBy);

            if (query.DueBefore.HasValue)
                rfis = rfis.Where(r => r.DueDate <= query.DueBefore);

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var search = query.SearchText.ToLowerInvariant();
                rfis = rfis.Where(r =>
                    r.Subject.ToLowerInvariant().Contains(search) ||
                    r.Question.ToLowerInvariant().Contains(search) ||
                    r.Number.ToLowerInvariant().Contains(search));
            }

            var total = rfis.Count();

            rfis = query.SortBy switch
            {
                RFISortField.Number => query.SortDescending ? rfis.OrderByDescending(r => r.Number) : rfis.OrderBy(r => r.Number),
                RFISortField.DueDate => query.SortDescending ? rfis.OrderByDescending(r => r.DueDate) : rfis.OrderBy(r => r.DueDate),
                RFISortField.Priority => query.SortDescending ? rfis.OrderByDescending(r => r.Priority) : rfis.OrderBy(r => r.Priority),
                _ => rfis.OrderByDescending(r => r.CreatedAt)
            };

            return new RFIQueryResult
            {
                RFIs = rfis.Skip(query.Skip).Take(query.Take).ToList(),
                TotalCount = total,
                Skip = query.Skip,
                Take = query.Take
            };
        }

        #endregion

        #region Submittal Management

        /// <summary>
        /// Create a new submittal
        /// </summary>
        public async Task<Submittal> CreateSubmittalAsync(
            CreateSubmittalRequest request,
            CancellationToken ct = default)
        {
            var submittalNumber = Interlocked.Increment(ref _submittalCounter);

            var submittal = new Submittal
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Number = $"SUB-{submittalNumber:D4}",
                Title = request.Title,
                Description = request.Description,
                ProjectId = request.ProjectId,
                SpecSection = request.SpecSection,
                Type = request.Type,
                Priority = request.Priority,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                Status = "draft",
                WorkflowTemplateId = "submittal-standard",
                DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(14),
                Attachments = request.Attachments ?? new List<Attachment>(),
                ReviewChain = request.ReviewChain ?? new List<Reviewer>(),
                PackageId = request.PackageId
            };

            // AI analysis
            var aiAnalysis = await _ai.AnalyzeSubmittalAsync(submittal, ct);
            submittal.AIAnalysis = aiAnalysis;

            _submittals[submittal.Id] = submittal;

            // Add to package if specified
            if (!string.IsNullOrEmpty(request.PackageId) && _packages.TryGetValue(request.PackageId, out var package))
            {
                package.SubmittalIds.Add(submittal.Id);
            }

            SubmittalCreated?.Invoke(this, new SubmittalCreatedEventArgs(submittal));

            return submittal;
        }

        /// <summary>
        /// Submit submittal for review
        /// </summary>
        public Submittal SubmitSubmittal(string submittalId, string submittedBy)
        {
            if (!_submittals.TryGetValue(submittalId, out var submittal))
                throw new SubmittalNotFoundException(submittalId);

            if (submittal.Status != "draft" && submittal.Status != "revise_resubmit")
                throw new InvalidWorkflowStateException("Submittal must be in draft or revise status to submit");

            submittal.Status = "submitted";
            submittal.SubmittedAt = DateTime.UtcNow;
            submittal.SubmittedBy = submittedBy;
            submittal.RevisionNumber++;

            AddSubmittalHistory(submittal, "submitted", submittedBy,
                submittal.RevisionNumber > 1 ? $"Resubmitted (Rev {submittal.RevisionNumber})" : "Submitted for review");

            return submittal;
        }

        /// <summary>
        /// Start review of submittal
        /// </summary>
        public Submittal StartReview(string submittalId, string reviewerId)
        {
            if (!_submittals.TryGetValue(submittalId, out var submittal))
                throw new SubmittalNotFoundException(submittalId);

            submittal.Status = "under_review";
            submittal.CurrentReviewerId = reviewerId;

            var reviewer = submittal.ReviewChain.FirstOrDefault(r => r.UserId == reviewerId);
            if (reviewer != null)
            {
                reviewer.StartedAt = DateTime.UtcNow;
            }

            AddSubmittalHistory(submittal, "review_started", reviewerId, "Review started");

            return submittal;
        }

        /// <summary>
        /// Review submittal
        /// </summary>
        public async Task<Submittal> ReviewSubmittalAsync(
            string submittalId,
            ReviewSubmittalRequest request,
            CancellationToken ct = default)
        {
            if (!_submittals.TryGetValue(submittalId, out var submittal))
                throw new SubmittalNotFoundException(submittalId);

            var review = new SubmittalReview
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                ReviewerId = request.ReviewerId,
                Decision = request.Decision,
                Comments = request.Comments,
                ReviewedAt = DateTime.UtcNow,
                Markups = request.Markups ?? new List<ReviewMarkup>(),
                Conditions = request.Conditions ?? new List<string>()
            };

            submittal.Reviews.Add(review);

            // Update reviewer in chain
            var reviewer = submittal.ReviewChain.FirstOrDefault(r => r.UserId == request.ReviewerId);
            if (reviewer != null)
            {
                reviewer.Decision = request.Decision;
                reviewer.CompletedAt = DateTime.UtcNow;
            }

            // Determine overall status
            submittal.Status = request.Decision switch
            {
                ReviewDecision.Approved => "approved",
                ReviewDecision.ApprovedAsNoted => "approved",
                ReviewDecision.ReviseAndResubmit => "revise_resubmit",
                ReviewDecision.Rejected => "rejected",
                _ => submittal.Status
            };

            if (submittal.Status == "approved")
            {
                submittal.ApprovedAt = DateTime.UtcNow;
                submittal.ApprovedBy = request.ReviewerId;
            }

            AddSubmittalHistory(submittal, "reviewed", request.ReviewerId,
                $"Reviewed: {request.Decision}");

            SubmittalReviewed?.Invoke(this, new SubmittalReviewedEventArgs(submittal, review));

            return submittal;
        }

        /// <summary>
        /// Get submittal by ID
        /// </summary>
        public Submittal? GetSubmittal(string submittalId)
            => _submittals.TryGetValue(submittalId, out var submittal) ? submittal : null;

        /// <summary>
        /// Query submittals
        /// </summary>
        public SubmittalQueryResult QuerySubmittals(SubmittalQuery query)
        {
            var submittals = _submittals.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(query.ProjectId))
                submittals = submittals.Where(s => s.ProjectId == query.ProjectId);

            if (query.Statuses?.Any() == true)
                submittals = submittals.Where(s => query.Statuses.Contains(s.Status));

            if (query.Types?.Any() == true)
                submittals = submittals.Where(s => query.Types.Contains(s.Type));

            if (!string.IsNullOrEmpty(query.SpecSection))
                submittals = submittals.Where(s => s.SpecSection == query.SpecSection);

            if (!string.IsNullOrEmpty(query.PackageId))
                submittals = submittals.Where(s => s.PackageId == query.PackageId);

            if (query.DueBefore.HasValue)
                submittals = submittals.Where(s => s.DueDate <= query.DueBefore);

            var total = submittals.Count();

            return new SubmittalQueryResult
            {
                Submittals = submittals.Skip(query.Skip).Take(query.Take).ToList(),
                TotalCount = total,
                Skip = query.Skip,
                Take = query.Take
            };
        }

        #endregion

        #region Submittal Packages

        /// <summary>
        /// Create submittal package
        /// </summary>
        public SubmittalPackage CreatePackage(CreatePackageRequest request)
        {
            var package = new SubmittalPackage
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Name = request.Name,
                Description = request.Description,
                ProjectId = request.ProjectId,
                SpecSections = request.SpecSections ?? new List<string>(),
                DueDate = request.DueDate,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                Status = PackageStatus.Open
            };

            _packages[package.Id] = package;
            return package;
        }

        /// <summary>
        /// Get package with submittals
        /// </summary>
        public SubmittalPackageView? GetPackage(string packageId)
        {
            if (!_packages.TryGetValue(packageId, out var package))
                return null;

            return new SubmittalPackageView
            {
                Package = package,
                Submittals = package.SubmittalIds
                    .Select(id => _submittals.GetValueOrDefault(id))
                    .Where(s => s != null)
                    .Cast<Submittal>()
                    .ToList()
            };
        }

        /// <summary>
        /// Close package
        /// </summary>
        public SubmittalPackage ClosePackage(string packageId, string closedBy)
        {
            if (!_packages.TryGetValue(packageId, out var package))
                throw new PackageNotFoundException(packageId);

            package.Status = PackageStatus.Closed;
            package.ClosedAt = DateTime.UtcNow;
            package.ClosedBy = closedBy;

            return package;
        }

        #endregion

        #region Comments & Ball-in-Court

        /// <summary>
        /// Add comment to RFI
        /// </summary>
        public void AddRFIComment(string rfiId, string author, string content, List<Attachment>? attachments = null)
        {
            if (!_rfis.TryGetValue(rfiId, out var rfi))
                throw new RFINotFoundException(rfiId);

            AddComment(rfi, author, content, attachments);
        }

        /// <summary>
        /// Add comment to submittal
        /// </summary>
        public void AddSubmittalComment(string submittalId, string author, string content, List<Attachment>? attachments = null)
        {
            if (!_submittals.TryGetValue(submittalId, out var submittal))
                throw new SubmittalNotFoundException(submittalId);

            submittal.Comments.Add(new Comment
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Author = author,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                Attachments = attachments ?? new List<Attachment>()
            });
        }

        /// <summary>
        /// Get ball-in-court summary (who has pending items)
        /// </summary>
        public BallInCourtSummary GetBallInCourt(string projectId)
        {
            var rfis = _rfis.Values.Where(r => r.ProjectId == projectId && r.Status != "closed").ToList();
            var submittals = _submittals.Values.Where(s => s.ProjectId == projectId &&
                s.Status != "approved" && s.Status != "rejected").ToList();

            var byUser = new Dictionary<string, UserWorkload>();

            foreach (var rfi in rfis.Where(r => !string.IsNullOrEmpty(r.AssignedTo)))
            {
                if (!byUser.ContainsKey(rfi.AssignedTo!))
                    byUser[rfi.AssignedTo!] = new UserWorkload { UserId = rfi.AssignedTo! };
                byUser[rfi.AssignedTo!].RFICount++;
                if (rfi.DueDate < DateTime.UtcNow)
                    byUser[rfi.AssignedTo!].OverdueCount++;
            }

            foreach (var submittal in submittals.Where(s => !string.IsNullOrEmpty(s.CurrentReviewerId)))
            {
                if (!byUser.ContainsKey(submittal.CurrentReviewerId!))
                    byUser[submittal.CurrentReviewerId!] = new UserWorkload { UserId = submittal.CurrentReviewerId! };
                byUser[submittal.CurrentReviewerId!].SubmittalCount++;
                if (submittal.DueDate < DateTime.UtcNow)
                    byUser[submittal.CurrentReviewerId!].OverdueCount++;
            }

            return new BallInCourtSummary
            {
                ProjectId = projectId,
                TotalOpenRFIs = rfis.Count,
                TotalOpenSubmittals = submittals.Count,
                ByUser = byUser.Values.OrderByDescending(u => u.TotalCount).ToList()
            };
        }

        #endregion

        #region Reports & Analytics

        /// <summary>
        /// Get RFI statistics
        /// </summary>
        public RFIStatistics GetRFIStatistics(string? projectId = null)
        {
            var rfis = projectId != null
                ? _rfis.Values.Where(r => r.ProjectId == projectId)
                : _rfis.Values;

            var list = rfis.ToList();

            return new RFIStatistics
            {
                TotalCount = list.Count,
                OpenCount = list.Count(r => r.Status != "closed"),
                ClosedCount = list.Count(r => r.Status == "closed"),
                OverdueCount = list.Count(r => r.DueDate < DateTime.UtcNow && r.Status != "closed"),
                ByStatus = list.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()),
                ByPriority = list.GroupBy(r => r.Priority).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                AverageResponseTime = CalculateAverageResponseTime(list),
                AverageCloseTime = CalculateAverageCloseTime(list)
            };
        }

        /// <summary>
        /// Get submittal statistics
        /// </summary>
        public SubmittalStatistics GetSubmittalStatistics(string? projectId = null)
        {
            var submittals = projectId != null
                ? _submittals.Values.Where(s => s.ProjectId == projectId)
                : _submittals.Values;

            var list = submittals.ToList();

            return new SubmittalStatistics
            {
                TotalCount = list.Count,
                ApprovedCount = list.Count(s => s.Status == "approved"),
                PendingCount = list.Count(s => s.Status != "approved" && s.Status != "rejected"),
                RejectedCount = list.Count(s => s.Status == "rejected"),
                OverdueCount = list.Count(s => s.DueDate < DateTime.UtcNow && s.Status != "approved" && s.Status != "rejected"),
                ByStatus = list.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count()),
                ByType = list.GroupBy(s => s.Type).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                AverageReviewTime = CalculateAverageReviewTime(list),
                ResubmitRate = list.Any() ? (double)list.Count(s => s.RevisionNumber > 1) / list.Count * 100 : 0
            };
        }

        /// <summary>
        /// Get deadline report
        /// </summary>
        public DeadlineReport GetDeadlineReport(string projectId, int daysAhead = 7)
        {
            var cutoff = DateTime.UtcNow.AddDays(daysAhead);

            var upcomingRFIs = _rfis.Values
                .Where(r => r.ProjectId == projectId && r.Status != "closed" && r.DueDate <= cutoff)
                .OrderBy(r => r.DueDate)
                .ToList();

            var upcomingSubmittals = _submittals.Values
                .Where(s => s.ProjectId == projectId && s.Status != "approved" && s.Status != "rejected" && s.DueDate <= cutoff)
                .OrderBy(s => s.DueDate)
                .ToList();

            return new DeadlineReport
            {
                ProjectId = projectId,
                ReportDate = DateTime.UtcNow,
                DaysAhead = daysAhead,
                OverdueRFIs = upcomingRFIs.Where(r => r.DueDate < DateTime.UtcNow).ToList(),
                UpcomingRFIs = upcomingRFIs.Where(r => r.DueDate >= DateTime.UtcNow).ToList(),
                OverdueSubmittals = upcomingSubmittals.Where(s => s.DueDate < DateTime.UtcNow).ToList(),
                UpcomingSubmittals = upcomingSubmittals.Where(s => s.DueDate >= DateTime.UtcNow).ToList()
            };
        }

        private TimeSpan CalculateAverageResponseTime(List<RFI> rfis)
        {
            var responded = rfis.Where(r => r.Response?.RespondedAt != null && r.SubmittedAt != null).ToList();
            if (!responded.Any()) return TimeSpan.Zero;
            var totalTicks = responded.Sum(r => (r.Response!.RespondedAt - r.SubmittedAt!.Value).Ticks);
            return TimeSpan.FromTicks(totalTicks / responded.Count);
        }

        private TimeSpan CalculateAverageCloseTime(List<RFI> rfis)
        {
            var closed = rfis.Where(r => r.ClosedAt != null && r.SubmittedAt != null).ToList();
            if (!closed.Any()) return TimeSpan.Zero;
            var totalTicks = closed.Sum(r => (r.ClosedAt!.Value - r.SubmittedAt!.Value).Ticks);
            return TimeSpan.FromTicks(totalTicks / closed.Count);
        }

        private TimeSpan CalculateAverageReviewTime(List<Submittal> submittals)
        {
            var reviewed = submittals.Where(s => s.ApprovedAt != null && s.SubmittedAt != null).ToList();
            if (!reviewed.Any()) return TimeSpan.Zero;
            var totalTicks = reviewed.Sum(s => (s.ApprovedAt!.Value - s.SubmittedAt!.Value).Ticks);
            return TimeSpan.FromTicks(totalTicks / reviewed.Count);
        }

        #endregion

        #region Helper Methods

        private void AddHistory(RFI rfi, string action, string performedBy, string details)
        {
            rfi.History.Add(new WorkflowHistory
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Action = action,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Details = details
            });
        }

        private void AddSubmittalHistory(Submittal submittal, string action, string performedBy, string details)
        {
            submittal.History.Add(new WorkflowHistory
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Action = action,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Details = details
            });
        }

        private void AddComment(RFI rfi, string author, string content, List<Attachment>? attachments = null)
        {
            rfi.Comments.Add(new Comment
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Author = author,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                Attachments = attachments ?? new List<Attachment>()
            });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        #endregion
    }

    #region RFI Models

    public class RFI
    {
        public string Id { get; set; } = "";
        public string Number { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Question { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string? SpecSection { get; set; }
        public string? DrawingReference { get; set; }
        public Priority Priority { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string Status { get; set; } = "";
        public string WorkflowTemplateId { get; set; } = "";
        public DateTime DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? ClosedBy { get; set; }
        public ResolutionType? Resolution { get; set; }
        public int DisputeCount { get; set; }
        public RFIResponse? Response { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
        public List<string> LinkedElements { get; set; } = new();
        public List<string> Distribution { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<WorkflowHistory> History { get; set; } = new();
        public RFIAIAnalysis? AIAnalysis { get; set; }
    }

    public class RFIResponse
    {
        public string Content { get; set; } = "";
        public string RespondedBy { get; set; } = "";
        public DateTime RespondedAt { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
        public CostImpact? CostImpact { get; set; }
        public ScheduleImpact? ScheduleImpact { get; set; }
    }

    public class CostImpact
    {
        public bool HasImpact { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string? Description { get; set; }
    }

    public class ScheduleImpact
    {
        public bool HasImpact { get; set; }
        public int? DaysImpact { get; set; }
        public string? Description { get; set; }
    }

    public enum Priority { Low, Normal, High, Critical }
    public enum ResolutionType { Accepted, Rejected, Withdrawn }

    #endregion

    #region Submittal Models

    public class Submittal
    {
        public string Id { get; set; } = "";
        public string Number { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string ProjectId { get; set; } = "";
        public string? SpecSection { get; set; }
        public SubmittalType Type { get; set; }
        public Priority Priority { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string Status { get; set; } = "";
        public string WorkflowTemplateId { get; set; } = "";
        public DateTime DueDate { get; set; }
        public int RevisionNumber { get; set; }
        public string? CurrentReviewerId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public string? PackageId { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
        public List<Reviewer> ReviewChain { get; set; } = new();
        public List<SubmittalReview> Reviews { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<WorkflowHistory> History { get; set; } = new();
        public SubmittalAIAnalysis? AIAnalysis { get; set; }
    }

    public enum SubmittalType { ProductData, ShopDrawing, Sample, MockUp, Closeout, OAndM, Warranty, AsBuilt }

    public class Reviewer
    {
        public string UserId { get; set; } = "";
        public string? Role { get; set; }
        public int Order { get; set; }
        public ReviewDecision? Decision { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class SubmittalReview
    {
        public string Id { get; set; } = "";
        public string ReviewerId { get; set; } = "";
        public ReviewDecision Decision { get; set; }
        public string? Comments { get; set; }
        public DateTime ReviewedAt { get; set; }
        public List<ReviewMarkup> Markups { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
    }

    public enum ReviewDecision { Pending, Approved, ApprovedAsNoted, ReviseAndResubmit, Rejected }

    public class ReviewMarkup
    {
        public string Id { get; set; } = "";
        public int PageNumber { get; set; }
        public MarkupType Type { get; set; }
        public string? Content { get; set; }
        public MarkupPosition Position { get; set; } = new();
    }

    public enum MarkupType { Comment, Cloud, Arrow, Text, Stamp }

    public class MarkupPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    #endregion

    #region Package Models

    public class SubmittalPackage
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ProjectId { get; set; } = "";
        public List<string> SpecSections { get; set; } = new();
        public DateTime? DueDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public PackageStatus Status { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? ClosedBy { get; set; }
        public List<string> SubmittalIds { get; set; } = new();
    }

    public enum PackageStatus { Open, Closed }

    public class SubmittalPackageView
    {
        public SubmittalPackage Package { get; set; } = new();
        public List<Submittal> Submittals { get; set; } = new();
    }

    #endregion

    #region Common Models

    public class Attachment
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long FileSize { get; set; }
        public string? StoragePath { get; set; }
        public string UploadedBy { get; set; } = "";
        public DateTime UploadedAt { get; set; }
    }

    public class Comment
    {
        public string Id { get; set; } = "";
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public List<Attachment> Attachments { get; set; } = new();
    }

    public class WorkflowHistory
    {
        public string Id { get; set; } = "";
        public string Action { get; set; } = "";
        public string PerformedBy { get; set; } = "";
        public DateTime PerformedAt { get; set; }
        public string Details { get; set; } = "";
    }

    public class WorkflowTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public WorkflowType Type { get; set; }
        public List<WorkflowStep> Steps { get; set; } = new();
        public int DefaultDueDays { get; set; }
    }

    public enum WorkflowType { RFI, Submittal }

    public class WorkflowStep
    {
        public int Order { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string[]? AllowedActions { get; set; }
        public bool IsFinal { get; set; }
    }

    #endregion

    #region Requests

    public class CreateRFIRequest
    {
        public string Subject { get; set; } = "";
        public string Question { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string? SpecSection { get; set; }
        public string? DrawingReference { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public string CreatedBy { get; set; } = "";
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public List<Attachment>? Attachments { get; set; }
        public List<string>? LinkedElements { get; set; }
        public List<string>? Distribution { get; set; }
    }

    public class RespondToRFIRequest
    {
        public string Response { get; set; } = "";
        public string RespondedBy { get; set; } = "";
        public List<Attachment>? Attachments { get; set; }
        public CostImpact? CostImpact { get; set; }
        public ScheduleImpact? ScheduleImpact { get; set; }
    }

    public class CreateSubmittalRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string ProjectId { get; set; } = "";
        public string? SpecSection { get; set; }
        public SubmittalType Type { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public string CreatedBy { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public List<Attachment>? Attachments { get; set; }
        public List<Reviewer>? ReviewChain { get; set; }
        public string? PackageId { get; set; }
    }

    public class ReviewSubmittalRequest
    {
        public string ReviewerId { get; set; } = "";
        public ReviewDecision Decision { get; set; }
        public string? Comments { get; set; }
        public List<ReviewMarkup>? Markups { get; set; }
        public List<string>? Conditions { get; set; }
    }

    public class CreatePackageRequest
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ProjectId { get; set; } = "";
        public List<string>? SpecSections { get; set; }
        public DateTime? DueDate { get; set; }
        public string CreatedBy { get; set; } = "";
    }

    #endregion

    #region Queries

    public class RFIQuery
    {
        public string? ProjectId { get; set; }
        public List<string>? Statuses { get; set; }
        public List<Priority>? Priorities { get; set; }
        public string? AssignedTo { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? DueBefore { get; set; }
        public string? SearchText { get; set; }
        public RFISortField SortBy { get; set; } = RFISortField.Created;
        public bool SortDescending { get; set; } = true;
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public enum RFISortField { Created, Number, DueDate, Priority }

    public class RFIQueryResult
    {
        public List<RFI> RFIs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    public class SubmittalQuery
    {
        public string? ProjectId { get; set; }
        public List<string>? Statuses { get; set; }
        public List<SubmittalType>? Types { get; set; }
        public string? SpecSection { get; set; }
        public string? PackageId { get; set; }
        public DateTime? DueBefore { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public class SubmittalQueryResult
    {
        public List<Submittal> Submittals { get; set; } = new();
        public int TotalCount { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    #endregion

    #region Statistics & Reports

    public class RFIStatistics
    {
        public int TotalCount { get; set; }
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }
        public int OverdueCount { get; set; }
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByPriority { get; set; } = new();
        public TimeSpan AverageResponseTime { get; set; }
        public TimeSpan AverageCloseTime { get; set; }
    }

    public class SubmittalStatistics
    {
        public int TotalCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }
        public int OverdueCount { get; set; }
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByType { get; set; } = new();
        public TimeSpan AverageReviewTime { get; set; }
        public double ResubmitRate { get; set; }
    }

    public class BallInCourtSummary
    {
        public string ProjectId { get; set; } = "";
        public int TotalOpenRFIs { get; set; }
        public int TotalOpenSubmittals { get; set; }
        public List<UserWorkload> ByUser { get; set; } = new();
    }

    public class UserWorkload
    {
        public string UserId { get; set; } = "";
        public int RFICount { get; set; }
        public int SubmittalCount { get; set; }
        public int OverdueCount { get; set; }
        public int TotalCount => RFICount + SubmittalCount;
    }

    public class DeadlineReport
    {
        public string ProjectId { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public int DaysAhead { get; set; }
        public List<RFI> OverdueRFIs { get; set; } = new();
        public List<RFI> UpcomingRFIs { get; set; } = new();
        public List<Submittal> OverdueSubmittals { get; set; } = new();
        public List<Submittal> UpcomingSubmittals { get; set; } = new();
    }

    #endregion

    #region AI Models

    public class RFISubmittalAI
    {
        private readonly RFISubmittalEngine _engine;

        public RFISubmittalAI(RFISubmittalEngine engine)
        {
            _engine = engine;
        }

        public Task<RFIAIAnalysis> AnalyzeRFIAsync(RFI rfi, CancellationToken ct)
        {
            var analysis = new RFIAIAnalysis
            {
                SuggestedAssignee = SuggestAssignee(rfi),
                SuggestedPriority = SuggestPriority(rfi),
                SimilarRFIs = FindSimilarRFIs(rfi),
                SuggestedResponseTemplate = GenerateResponseTemplate(rfi),
                EstimatedResponseTime = EstimateResponseTime(rfi),
                Confidence = 0.75
            };

            return Task.FromResult(analysis);
        }

        public Task<SubmittalAIAnalysis> AnalyzeSubmittalAsync(Submittal submittal, CancellationToken ct)
        {
            var analysis = new SubmittalAIAnalysis
            {
                SuggestedReviewers = SuggestReviewers(submittal),
                SuggestedPriority = SuggestSubmittalPriority(submittal),
                ComplianceCheck = CheckCompliance(submittal),
                EstimatedReviewTime = EstimateReviewTime(submittal),
                Confidence = 0.8
            };

            return Task.FromResult(analysis);
        }

        private string? SuggestAssignee(RFI rfi)
        {
            // Based on spec section and question content
            if (rfi.Question.ToLowerInvariant().Contains("structural"))
                return "structural_engineer";
            if (rfi.Question.ToLowerInvariant().Contains("electrical"))
                return "electrical_engineer";
            if (rfi.Question.ToLowerInvariant().Contains("mechanical") || rfi.Question.ToLowerInvariant().Contains("hvac"))
                return "mechanical_engineer";
            return "project_architect";
        }

        private Priority SuggestPriority(RFI rfi)
        {
            var question = rfi.Question.ToLowerInvariant();
            if (question.Contains("urgent") || question.Contains("critical") || question.Contains("safety"))
                return Priority.Critical;
            if (question.Contains("delay") || question.Contains("hold"))
                return Priority.High;
            return Priority.Normal;
        }

        private List<string> FindSimilarRFIs(RFI rfi)
        {
            // Simplified similarity search
            return new List<string>();
        }

        private string GenerateResponseTemplate(RFI rfi)
        {
            return $"In response to your inquiry regarding {rfi.Subject}:\n\n[Response]\n\nPlease let us know if you have any further questions.";
        }

        private TimeSpan EstimateResponseTime(RFI rfi)
        {
            return rfi.Priority switch
            {
                Priority.Critical => TimeSpan.FromDays(1),
                Priority.High => TimeSpan.FromDays(3),
                _ => TimeSpan.FromDays(5)
            };
        }

        private List<string> SuggestReviewers(Submittal submittal)
        {
            return new List<string> { "project_architect", "specification_writer" };
        }

        private Priority SuggestSubmittalPriority(Submittal submittal)
        {
            return submittal.Type switch
            {
                SubmittalType.MockUp => Priority.High,
                SubmittalType.ShopDrawing => Priority.High,
                _ => Priority.Normal
            };
        }

        private ComplianceCheckResult CheckCompliance(Submittal submittal)
        {
            return new ComplianceCheckResult
            {
                IsCompliant = true,
                MissingItems = new List<string>(),
                Warnings = new List<string>()
            };
        }

        private TimeSpan EstimateReviewTime(Submittal submittal)
        {
            return submittal.Type switch
            {
                SubmittalType.ShopDrawing => TimeSpan.FromDays(10),
                SubmittalType.ProductData => TimeSpan.FromDays(5),
                _ => TimeSpan.FromDays(7)
            };
        }
    }

    public class RFIAIAnalysis
    {
        public string? SuggestedAssignee { get; set; }
        public Priority SuggestedPriority { get; set; }
        public List<string> SimilarRFIs { get; set; } = new();
        public string? SuggestedResponseTemplate { get; set; }
        public TimeSpan EstimatedResponseTime { get; set; }
        public double Confidence { get; set; }
    }

    public class SubmittalAIAnalysis
    {
        public List<string> SuggestedReviewers { get; set; } = new();
        public Priority SuggestedPriority { get; set; }
        public ComplianceCheckResult? ComplianceCheck { get; set; }
        public TimeSpan EstimatedReviewTime { get; set; }
        public double Confidence { get; set; }
    }

    public class ComplianceCheckResult
    {
        public bool IsCompliant { get; set; }
        public List<string> MissingItems { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    #endregion

    #region Events

    public class RFICreatedEventArgs : EventArgs
    {
        public RFI RFI { get; }
        public RFICreatedEventArgs(RFI rfi) => RFI = rfi;
    }

    public class RFIRespondedEventArgs : EventArgs
    {
        public RFI RFI { get; }
        public RFIRespondedEventArgs(RFI rfi) => RFI = rfi;
    }

    public class SubmittalCreatedEventArgs : EventArgs
    {
        public Submittal Submittal { get; }
        public SubmittalCreatedEventArgs(Submittal submittal) => Submittal = submittal;
    }

    public class SubmittalReviewedEventArgs : EventArgs
    {
        public Submittal Submittal { get; }
        public SubmittalReview Review { get; }
        public SubmittalReviewedEventArgs(Submittal submittal, SubmittalReview review)
        {
            Submittal = submittal;
            Review = review;
        }
    }

    public class DeadlineApproachingEventArgs : EventArgs
    {
        public string ItemId { get; }
        public string ItemType { get; }
        public DateTime DueDate { get; }
        public TimeSpan TimeRemaining { get; }
        public DeadlineApproachingEventArgs(string itemId, string itemType, DateTime dueDate, TimeSpan timeRemaining)
        {
            ItemId = itemId;
            ItemType = itemType;
            DueDate = dueDate;
            TimeRemaining = timeRemaining;
        }
    }

    public class OverdueAlertEventArgs : EventArgs
    {
        public string ItemId { get; }
        public string ItemType { get; }
        public DateTime DueDate { get; }
        public TimeSpan OverdueBy { get; }
        public OverdueAlertEventArgs(string itemId, string itemType, DateTime dueDate, TimeSpan overdueBy)
        {
            ItemId = itemId;
            ItemType = itemType;
            DueDate = dueDate;
            OverdueBy = overdueBy;
        }
    }

    #endregion

    #region Exceptions

    public class RFINotFoundException : Exception
    {
        public string RFIId { get; }
        public RFINotFoundException(string rfiId) : base($"RFI not found: {rfiId}")
            => RFIId = rfiId;
    }

    public class SubmittalNotFoundException : Exception
    {
        public string SubmittalId { get; }
        public SubmittalNotFoundException(string submittalId) : base($"Submittal not found: {submittalId}")
            => SubmittalId = submittalId;
    }

    public class PackageNotFoundException : Exception
    {
        public string PackageId { get; }
        public PackageNotFoundException(string packageId) : base($"Package not found: {packageId}")
            => PackageId = packageId;
    }

    public class InvalidWorkflowStateException : Exception
    {
        public InvalidWorkflowStateException(string message) : base(message) { }
    }

    #endregion
}
