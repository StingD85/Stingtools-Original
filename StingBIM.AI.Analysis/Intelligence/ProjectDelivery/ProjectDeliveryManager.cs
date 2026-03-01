// ============================================================================
// StingBIM AI - Project Delivery Manager
// Milestone tracking, deliverable management, and project lifecycle
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ProjectDelivery
{
    /// <summary>
    /// Project Delivery Manager for tracking milestones, deliverables,
    /// design stages, and project lifecycle management.
    /// </summary>
    public sealed class ProjectDeliveryManager
    {
        private static readonly Lazy<ProjectDeliveryManager> _instance =
            new Lazy<ProjectDeliveryManager>(() => new ProjectDeliveryManager());
        public static ProjectDeliveryManager Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, DeliveryProject> _projects = new();
        private readonly Dictionary<string, DeliveryMilestone> _milestones = new();
        private readonly Dictionary<string, Deliverable> _deliverables = new();
        private readonly Dictionary<string, DesignStage> _stages = new();
        private readonly Dictionary<string, GateReview> _reviews = new();
        private readonly List<DeliveryTemplate> _templates = new();

        public event EventHandler<DeliveryEventArgs> MilestoneApproaching;
        public event EventHandler<DeliveryEventArgs> DeliverableOverdue;
        public event EventHandler<DeliveryEventArgs> StageCompleted;

        private ProjectDeliveryManager()
        {
            InitializeTemplates();
        }

        #region Initialization

        private void InitializeTemplates()
        {
            _templates.AddRange(new[]
            {
                new DeliveryTemplate
                {
                    TemplateId = "TPL-RIBA",
                    Name = "RIBA Plan of Work 2020",
                    Description = "UK RIBA stages template",
                    Region = "UK",
                    Stages = new List<StageTemplate>
                    {
                        new StageTemplate { StageNumber = 0, Name = "Strategic Definition", DurationPercent = 5, LOD = "LOD 100" },
                        new StageTemplate { StageNumber = 1, Name = "Preparation and Briefing", DurationPercent = 10, LOD = "LOD 100" },
                        new StageTemplate { StageNumber = 2, Name = "Concept Design", DurationPercent = 10, LOD = "LOD 200" },
                        new StageTemplate { StageNumber = 3, Name = "Spatial Coordination", DurationPercent = 15, LOD = "LOD 300" },
                        new StageTemplate { StageNumber = 4, Name = "Technical Design", DurationPercent = 20, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 5, Name = "Manufacturing and Construction", DurationPercent = 30, LOD = "LOD 400" },
                        new StageTemplate { StageNumber = 6, Name = "Handover", DurationPercent = 5, LOD = "LOD 500" },
                        new StageTemplate { StageNumber = 7, Name = "Use", DurationPercent = 5, LOD = "LOD 500" }
                    }
                },
                new DeliveryTemplate
                {
                    TemplateId = "TPL-AIA",
                    Name = "AIA Design Phases",
                    Description = "US AIA traditional phases",
                    Region = "US",
                    Stages = new List<StageTemplate>
                    {
                        new StageTemplate { StageNumber = 1, Name = "Pre-Design", DurationPercent = 5, LOD = "LOD 100" },
                        new StageTemplate { StageNumber = 2, Name = "Schematic Design", DurationPercent = 15, LOD = "LOD 200" },
                        new StageTemplate { StageNumber = 3, Name = "Design Development", DurationPercent = 20, LOD = "LOD 300" },
                        new StageTemplate { StageNumber = 4, Name = "Construction Documents", DurationPercent = 25, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 5, Name = "Bidding/Negotiation", DurationPercent = 5, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 6, Name = "Construction Administration", DurationPercent = 30, LOD = "LOD 400" }
                    }
                },
                new DeliveryTemplate
                {
                    TemplateId = "TPL-IPD",
                    Name = "IPD Phases",
                    Description = "Integrated Project Delivery phases",
                    Region = "Global",
                    Stages = new List<StageTemplate>
                    {
                        new StageTemplate { StageNumber = 1, Name = "Conceptualization", DurationPercent = 10, LOD = "LOD 100" },
                        new StageTemplate { StageNumber = 2, Name = "Criteria Design", DurationPercent = 15, LOD = "LOD 200" },
                        new StageTemplate { StageNumber = 3, Name = "Detailed Design", DurationPercent = 25, LOD = "LOD 300" },
                        new StageTemplate { StageNumber = 4, Name = "Implementation Documents", DurationPercent = 20, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 5, Name = "Agency Review", DurationPercent = 5, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 6, Name = "Construction", DurationPercent = 20, LOD = "LOD 400" },
                        new StageTemplate { StageNumber = 7, Name = "Closeout", DurationPercent = 5, LOD = "LOD 500" }
                    }
                },
                new DeliveryTemplate
                {
                    TemplateId = "TPL-DB",
                    Name = "Design-Build Phases",
                    Description = "Design-Build delivery method",
                    Region = "Global",
                    Stages = new List<StageTemplate>
                    {
                        new StageTemplate { StageNumber = 1, Name = "Programming", DurationPercent = 10, LOD = "LOD 100" },
                        new StageTemplate { StageNumber = 2, Name = "Schematic Design", DurationPercent = 15, LOD = "LOD 200" },
                        new StageTemplate { StageNumber = 3, Name = "Design-Build Coordination", DurationPercent = 25, LOD = "LOD 300" },
                        new StageTemplate { StageNumber = 4, Name = "Construction Documents", DurationPercent = 20, LOD = "LOD 350" },
                        new StageTemplate { StageNumber = 5, Name = "Construction", DurationPercent = 25, LOD = "LOD 400" },
                        new StageTemplate { StageNumber = 6, Name = "Closeout", DurationPercent = 5, LOD = "LOD 500" }
                    }
                }
            });
        }

        #endregion

        #region Project Management

        /// <summary>
        /// Create a delivery project
        /// </summary>
        public DeliveryProject CreateProject(DeliveryProjectRequest request)
        {
            var template = _templates.FirstOrDefault(t => t.TemplateId == request.TemplateId)
                ?? _templates.First();

            var project = new DeliveryProject
            {
                ProjectId = Guid.NewGuid().ToString(),
                ProjectName = request.ProjectName,
                ProjectCode = request.ProjectCode,
                ClientName = request.ClientName,
                StartDate = request.StartDate,
                PlannedEndDate = request.PlannedEndDate,
                TemplateId = template.TemplateId,
                DeliveryMethod = request.DeliveryMethod,
                ContractType = request.ContractType,
                Status = ProjectDeliveryStatus.Planning,
                CreatedDate = DateTime.UtcNow,
                Stages = new List<string>(),
                Milestones = new List<string>(),
                TeamMembers = request.TeamMembers ?? new List<DeliveryTeamMember>()
            };

            // Generate stages from template
            var totalDays = (request.PlannedEndDate - request.StartDate).TotalDays;
            var currentDate = request.StartDate;

            foreach (var stageTemplate in template.Stages)
            {
                var stageDuration = (int)(totalDays * stageTemplate.DurationPercent / 100);
                var stage = CreateStage(new StageRequest
                {
                    ProjectId = project.ProjectId,
                    StageNumber = stageTemplate.StageNumber,
                    StageName = stageTemplate.Name,
                    StartDate = currentDate,
                    EndDate = currentDate.AddDays(stageDuration),
                    RequiredLOD = stageTemplate.LOD
                });

                project.Stages.Add(stage.StageId);
                currentDate = currentDate.AddDays(stageDuration);
            }

            lock (_lock)
            {
                _projects[project.ProjectId] = project;
            }

            return project;
        }

        /// <summary>
        /// Update project status
        /// </summary>
        public void UpdateProjectStatus(string projectId, ProjectDeliveryStatus status)
        {
            lock (_lock)
            {
                if (_projects.TryGetValue(projectId, out var project))
                {
                    project.Status = status;
                    project.LastUpdated = DateTime.UtcNow;

                    if (status == ProjectDeliveryStatus.Completed)
                    {
                        project.ActualEndDate = DateTime.UtcNow;
                    }
                }
            }
        }

        #endregion

        #region Stage Management

        /// <summary>
        /// Create a design stage
        /// </summary>
        public DesignStage CreateStage(StageRequest request)
        {
            var stage = new DesignStage
            {
                StageId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                StageNumber = request.StageNumber,
                StageName = request.StageName,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                RequiredLOD = request.RequiredLOD,
                Status = StageStatus.NotStarted,
                Deliverables = new List<string>(),
                GateReviews = new List<string>(),
                Tasks = new List<StageTask>()
            };

            // Add standard deliverables for stage
            var standardDeliverables = GetStandardDeliverables(request.StageName);
            foreach (var deliverable in standardDeliverables)
            {
                var del = CreateDeliverable(new DeliverableRequest
                {
                    ProjectId = request.ProjectId,
                    StageId = stage.StageId,
                    Name = deliverable.Name,
                    Type = deliverable.Type,
                    Description = deliverable.Description,
                    DueDate = request.EndDate,
                    ResponsibleParty = deliverable.ResponsibleParty
                });
                stage.Deliverables.Add(del.DeliverableId);
            }

            lock (_lock)
            {
                _stages[stage.StageId] = stage;
            }

            return stage;
        }

        private List<DeliverableTemplate> GetStandardDeliverables(string stageName)
        {
            return stageName switch
            {
                "Concept Design" or "Schematic Design" or "Conceptualization" => new List<DeliverableTemplate>
                {
                    new DeliverableTemplate { Name = "Concept Model", Type = DeliverableType.Model, ResponsibleParty = "Architect" },
                    new DeliverableTemplate { Name = "Design Intent Document", Type = DeliverableType.Report, ResponsibleParty = "Architect" },
                    new DeliverableTemplate { Name = "Preliminary Cost Estimate", Type = DeliverableType.Report, ResponsibleParty = "Cost Consultant" },
                    new DeliverableTemplate { Name = "Site Analysis", Type = DeliverableType.Drawing, ResponsibleParty = "Architect" }
                },

                "Spatial Coordination" or "Design Development" or "Detailed Design" => new List<DeliverableTemplate>
                {
                    new DeliverableTemplate { Name = "Coordinated Design Model", Type = DeliverableType.Model, ResponsibleParty = "All Disciplines" },
                    new DeliverableTemplate { Name = "Clash Resolution Report", Type = DeliverableType.Report, ResponsibleParty = "BIM Coordinator" },
                    new DeliverableTemplate { Name = "Specification Outline", Type = DeliverableType.Specification, ResponsibleParty = "Architect" },
                    new DeliverableTemplate { Name = "Updated Cost Estimate", Type = DeliverableType.Report, ResponsibleParty = "Cost Consultant" },
                    new DeliverableTemplate { Name = "Room Data Sheets", Type = DeliverableType.Data, ResponsibleParty = "Architect" }
                },

                "Technical Design" or "Construction Documents" or "Implementation Documents" => new List<DeliverableTemplate>
                {
                    new DeliverableTemplate { Name = "Construction Model", Type = DeliverableType.Model, ResponsibleParty = "All Disciplines" },
                    new DeliverableTemplate { Name = "Construction Drawings", Type = DeliverableType.Drawing, ResponsibleParty = "All Disciplines" },
                    new DeliverableTemplate { Name = "Technical Specifications", Type = DeliverableType.Specification, ResponsibleParty = "Architect" },
                    new DeliverableTemplate { Name = "Final Cost Estimate", Type = DeliverableType.Report, ResponsibleParty = "Cost Consultant" },
                    new DeliverableTemplate { Name = "4D Construction Sequence", Type = DeliverableType.Model, ResponsibleParty = "BIM Manager" }
                },

                "Manufacturing and Construction" or "Construction" or "Construction Administration" => new List<DeliverableTemplate>
                {
                    new DeliverableTemplate { Name = "Shop Drawing Coordination", Type = DeliverableType.Drawing, ResponsibleParty = "BIM Coordinator" },
                    new DeliverableTemplate { Name = "As-Built Updates", Type = DeliverableType.Model, ResponsibleParty = "Contractor" },
                    new DeliverableTemplate { Name = "Progress Reports", Type = DeliverableType.Report, ResponsibleParty = "Project Manager" },
                    new DeliverableTemplate { Name = "Site Instructions", Type = DeliverableType.Document, ResponsibleParty = "Architect" }
                },

                "Handover" or "Closeout" => new List<DeliverableTemplate>
                {
                    new DeliverableTemplate { Name = "As-Built Model", Type = DeliverableType.Model, ResponsibleParty = "BIM Manager" },
                    new DeliverableTemplate { Name = "COBie Data", Type = DeliverableType.Data, ResponsibleParty = "Information Manager" },
                    new DeliverableTemplate { Name = "O&M Manuals", Type = DeliverableType.Document, ResponsibleParty = "Contractor" },
                    new DeliverableTemplate { Name = "Warranties & Certificates", Type = DeliverableType.Document, ResponsibleParty = "Contractor" },
                    new DeliverableTemplate { Name = "Training Documentation", Type = DeliverableType.Document, ResponsibleParty = "Contractor" }
                },

                _ => new List<DeliverableTemplate>()
            };
        }

        /// <summary>
        /// Start a stage
        /// </summary>
        public void StartStage(string stageId)
        {
            lock (_lock)
            {
                if (_stages.TryGetValue(stageId, out var stage))
                {
                    stage.Status = StageStatus.InProgress;
                    stage.ActualStartDate = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Complete a stage
        /// </summary>
        public StageCompletionResult CompleteStage(string stageId, string completedBy)
        {
            lock (_lock)
            {
                if (!_stages.TryGetValue(stageId, out var stage))
                    throw new KeyNotFoundException($"Stage {stageId} not found");

                // Check all deliverables are complete
                var incompleteDeliverables = stage.Deliverables
                    .Select(id => _deliverables.GetValueOrDefault(id))
                    .Where(d => d != null && d.Status != DeliverableStatus.Approved)
                    .ToList();

                if (incompleteDeliverables.Any())
                {
                    return new StageCompletionResult
                    {
                        Success = false,
                        StageId = stageId,
                        Message = "Cannot complete stage - deliverables pending",
                        PendingDeliverables = incompleteDeliverables.Select(d => d.Name).ToList()
                    };
                }

                stage.Status = StageStatus.Completed;
                stage.ActualEndDate = DateTime.UtcNow;
                stage.CompletedBy = completedBy;

                StageCompleted?.Invoke(this, new DeliveryEventArgs
                {
                    EventType = DeliveryEventType.StageCompleted,
                    ProjectId = stage.ProjectId,
                    EntityId = stageId,
                    Message = $"Stage '{stage.StageName}' completed"
                });

                return new StageCompletionResult
                {
                    Success = true,
                    StageId = stageId,
                    Message = "Stage completed successfully",
                    CompletionDate = stage.ActualEndDate.Value
                };
            }
        }

        #endregion

        #region Milestone Management

        /// <summary>
        /// Create a milestone
        /// </summary>
        public DeliveryMilestone CreateMilestone(MilestoneRequest request)
        {
            var milestone = new DeliveryMilestone
            {
                MilestoneId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                Description = request.Description,
                PlannedDate = request.PlannedDate,
                Type = request.Type,
                IsCritical = request.IsCritical,
                RequiredDeliverables = request.RequiredDeliverables ?? new List<string>(),
                Status = MilestoneStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };

            lock (_lock)
            {
                _milestones[milestone.MilestoneId] = milestone;

                if (_projects.TryGetValue(request.ProjectId, out var project))
                {
                    project.Milestones.Add(milestone.MilestoneId);
                }
            }

            return milestone;
        }

        /// <summary>
        /// Complete a milestone
        /// </summary>
        public MilestoneCompletionResult CompleteMilestone(string milestoneId, string completedBy, string notes = null)
        {
            lock (_lock)
            {
                if (!_milestones.TryGetValue(milestoneId, out var milestone))
                    throw new KeyNotFoundException($"Milestone {milestoneId} not found");

                // Check required deliverables
                var pendingDeliverables = milestone.RequiredDeliverables
                    .Select(id => _deliverables.GetValueOrDefault(id))
                    .Where(d => d != null && d.Status != DeliverableStatus.Approved)
                    .ToList();

                if (pendingDeliverables.Any())
                {
                    return new MilestoneCompletionResult
                    {
                        Success = false,
                        MilestoneId = milestoneId,
                        Message = "Cannot complete milestone - required deliverables pending",
                        PendingItems = pendingDeliverables.Select(d => d.Name).ToList()
                    };
                }

                milestone.Status = MilestoneStatus.Achieved;
                milestone.ActualDate = DateTime.UtcNow;
                milestone.CompletedBy = completedBy;
                milestone.CompletionNotes = notes;

                // Calculate variance
                milestone.VarianceDays = (int)(milestone.ActualDate.Value - milestone.PlannedDate).TotalDays;

                return new MilestoneCompletionResult
                {
                    Success = true,
                    MilestoneId = milestoneId,
                    Message = "Milestone achieved",
                    CompletionDate = milestone.ActualDate.Value,
                    VarianceDays = milestone.VarianceDays
                };
            }
        }

        /// <summary>
        /// Check upcoming milestones
        /// </summary>
        public List<DeliveryMilestone> GetUpcomingMilestones(string projectId, int daysAhead = 14)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddDays(daysAhead);

                return _milestones.Values
                    .Where(m => m.ProjectId == projectId)
                    .Where(m => m.Status == MilestoneStatus.Pending)
                    .Where(m => m.PlannedDate <= cutoff)
                    .OrderBy(m => m.PlannedDate)
                    .ToList();
            }
        }

        #endregion

        #region Deliverable Management

        /// <summary>
        /// Create a deliverable
        /// </summary>
        public Deliverable CreateDeliverable(DeliverableRequest request)
        {
            var deliverable = new Deliverable
            {
                DeliverableId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                StageId = request.StageId,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Format = request.Format,
                DueDate = request.DueDate,
                ResponsibleParty = request.ResponsibleParty,
                Discipline = request.Discipline,
                Status = DeliverableStatus.NotStarted,
                CreatedDate = DateTime.UtcNow,
                Submissions = new List<DeliverableSubmission>(),
                Reviews = new List<DeliverableReview>()
            };

            lock (_lock)
            {
                _deliverables[deliverable.DeliverableId] = deliverable;
            }

            return deliverable;
        }

        /// <summary>
        /// Submit a deliverable
        /// </summary>
        public DeliverableSubmission SubmitDeliverable(string deliverableId, SubmissionRequest request)
        {
            lock (_lock)
            {
                if (!_deliverables.TryGetValue(deliverableId, out var deliverable))
                    throw new KeyNotFoundException($"Deliverable {deliverableId} not found");

                var submission = new DeliverableSubmission
                {
                    SubmissionId = Guid.NewGuid().ToString(),
                    SubmissionNumber = deliverable.Submissions.Count + 1,
                    SubmittedBy = request.SubmittedBy,
                    SubmittedDate = DateTime.UtcNow,
                    FilePath = request.FilePath,
                    FileName = request.FileName,
                    Notes = request.Notes
                };

                deliverable.Submissions.Add(submission);
                deliverable.Status = DeliverableStatus.Submitted;
                deliverable.LastUpdated = DateTime.UtcNow;

                return submission;
            }
        }

        /// <summary>
        /// Review a deliverable
        /// </summary>
        public DeliverableReview ReviewDeliverable(string deliverableId, ReviewRequest request)
        {
            lock (_lock)
            {
                if (!_deliverables.TryGetValue(deliverableId, out var deliverable))
                    throw new KeyNotFoundException($"Deliverable {deliverableId} not found");

                var review = new DeliverableReview
                {
                    ReviewId = Guid.NewGuid().ToString(),
                    ReviewedBy = request.ReviewedBy,
                    ReviewDate = DateTime.UtcNow,
                    Result = request.Result,
                    Comments = request.Comments,
                    MarkedUpFile = request.MarkedUpFile
                };

                deliverable.Reviews.Add(review);

                deliverable.Status = request.Result switch
                {
                    DeliverableReviewResult.Approved => DeliverableStatus.Approved,
                    DeliverableReviewResult.ApprovedWithComments => DeliverableStatus.Approved,
                    DeliverableReviewResult.ReviseResubmit => DeliverableStatus.RequiresRevision,
                    DeliverableReviewResult.Rejected => DeliverableStatus.Rejected,
                    _ => deliverable.Status
                };

                if (deliverable.Status == DeliverableStatus.Approved)
                {
                    deliverable.ApprovedDate = DateTime.UtcNow;
                    deliverable.ApprovedBy = request.ReviewedBy;
                }

                return review;
            }
        }

        /// <summary>
        /// Get overdue deliverables
        /// </summary>
        public List<Deliverable> GetOverdueDeliverables(string projectId)
        {
            lock (_lock)
            {
                return _deliverables.Values
                    .Where(d => d.ProjectId == projectId)
                    .Where(d => d.Status != DeliverableStatus.Approved)
                    .Where(d => d.DueDate < DateTime.UtcNow)
                    .OrderBy(d => d.DueDate)
                    .ToList();
            }
        }

        #endregion

        #region Gate Reviews

        /// <summary>
        /// Create a gate review
        /// </summary>
        public GateReview CreateGateReview(GateReviewRequest request)
        {
            var review = new GateReview
            {
                ReviewId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                StageId = request.StageId,
                ReviewName = request.ReviewName,
                ScheduledDate = request.ScheduledDate,
                ReviewType = request.ReviewType,
                Reviewers = request.Reviewers ?? new List<GateReviewer>(),
                Criteria = request.Criteria ?? GetDefaultGateCriteria(request.ReviewType),
                Status = GateReviewStatus.Scheduled,
                Outcomes = new List<ReviewOutcome>()
            };

            lock (_lock)
            {
                _reviews[review.ReviewId] = review;

                if (_stages.TryGetValue(request.StageId, out var stage))
                {
                    stage.GateReviews.Add(review.ReviewId);
                }
            }

            return review;
        }

        private List<GateCriterion> GetDefaultGateCriteria(GateReviewType type)
        {
            var criteria = new List<GateCriterion>
            {
                new GateCriterion { Name = "Design completeness meets stage requirements", IsMandatory = true },
                new GateCriterion { Name = "All deliverables submitted", IsMandatory = true },
                new GateCriterion { Name = "Model coordination complete", IsMandatory = true },
                new GateCriterion { Name = "LOD requirements met", IsMandatory = true },
                new GateCriterion { Name = "Client comments addressed", IsMandatory = true }
            };

            if (type == GateReviewType.StageGate)
            {
                criteria.Add(new GateCriterion { Name = "Cost within budget", IsMandatory = false });
                criteria.Add(new GateCriterion { Name = "Schedule on track", IsMandatory = false });
            }

            return criteria;
        }

        /// <summary>
        /// Conduct gate review
        /// </summary>
        public GateReviewResult ConductGateReview(string reviewId, GateReviewConductRequest request)
        {
            lock (_lock)
            {
                if (!_reviews.TryGetValue(reviewId, out var review))
                    throw new KeyNotFoundException($"Gate review {reviewId} not found");

                review.ActualDate = DateTime.UtcNow;
                review.ConductedBy = request.ConductedBy;
                review.Attendees = request.Attendees;

                // Record outcomes for each criterion
                foreach (var outcome in request.Outcomes)
                {
                    review.Outcomes.Add(outcome);
                }

                // Determine overall result
                var mandatoryFailed = review.Outcomes
                    .Where(o => review.Criteria.Any(c => c.Name == o.CriterionName && c.IsMandatory))
                    .Any(o => !o.Passed);

                review.OverallResult = mandatoryFailed ? GateReviewResult.NotApproved :
                    review.Outcomes.All(o => o.Passed) ? GateReviewResult.Approved :
                    GateReviewResult.ConditionalApproval;

                review.Status = GateReviewStatus.Completed;
                review.Notes = request.Notes;
                review.ActionItems = request.ActionItems ?? new List<GateActionItem>();

                return review.OverallResult;
            }
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Get project delivery dashboard
        /// </summary>
        public DeliveryDashboard GetDashboard(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    throw new KeyNotFoundException($"Project {projectId} not found");

                var stages = project.Stages
                    .Select(id => _stages.GetValueOrDefault(id))
                    .Where(s => s != null)
                    .ToList();

                var deliverables = _deliverables.Values
                    .Where(d => d.ProjectId == projectId)
                    .ToList();

                var milestones = project.Milestones
                    .Select(id => _milestones.GetValueOrDefault(id))
                    .Where(m => m != null)
                    .ToList();

                var currentStage = stages.FirstOrDefault(s => s.Status == StageStatus.InProgress);

                return new DeliveryDashboard
                {
                    ProjectId = projectId,
                    ProjectName = project.ProjectName,
                    GeneratedDate = DateTime.UtcNow,

                    // Overall progress
                    OverallProgress = CalculateOverallProgress(stages),
                    CurrentStage = currentStage?.StageName,
                    DaysRemaining = (project.PlannedEndDate - DateTime.UtcNow).Days,

                    // Stage summary
                    StageSummary = new StageSummary
                    {
                        TotalStages = stages.Count,
                        CompletedStages = stages.Count(s => s.Status == StageStatus.Completed),
                        InProgressStages = stages.Count(s => s.Status == StageStatus.InProgress),
                        CurrentStageLOD = currentStage?.RequiredLOD
                    },

                    // Deliverable summary
                    DeliverableSummary = new DeliverableSummary
                    {
                        TotalDeliverables = deliverables.Count,
                        Approved = deliverables.Count(d => d.Status == DeliverableStatus.Approved),
                        Submitted = deliverables.Count(d => d.Status == DeliverableStatus.Submitted),
                        InProgress = deliverables.Count(d => d.Status == DeliverableStatus.InProgress),
                        Overdue = deliverables.Count(d => d.DueDate < DateTime.UtcNow &&
                            d.Status != DeliverableStatus.Approved)
                    },

                    // Milestone summary
                    MilestoneSummary = new MilestoneSummary
                    {
                        TotalMilestones = milestones.Count,
                        Achieved = milestones.Count(m => m.Status == MilestoneStatus.Achieved),
                        Upcoming = milestones.Count(m => m.Status == MilestoneStatus.Pending &&
                            m.PlannedDate <= DateTime.UtcNow.AddDays(14)),
                        Overdue = milestones.Count(m => m.Status == MilestoneStatus.Pending &&
                            m.PlannedDate < DateTime.UtcNow)
                    },

                    // Upcoming items
                    UpcomingDeliverables = deliverables
                        .Where(d => d.Status != DeliverableStatus.Approved)
                        .Where(d => d.DueDate <= DateTime.UtcNow.AddDays(14))
                        .OrderBy(d => d.DueDate)
                        .Take(10)
                        .ToList(),

                    UpcomingMilestones = milestones
                        .Where(m => m.Status == MilestoneStatus.Pending)
                        .Where(m => m.PlannedDate <= DateTime.UtcNow.AddDays(30))
                        .OrderBy(m => m.PlannedDate)
                        .Take(5)
                        .ToList()
                };
            }
        }

        private double CalculateOverallProgress(List<DesignStage> stages)
        {
            if (!stages.Any())
                return 0;

            var completedWeight = stages.Count(s => s.Status == StageStatus.Completed) * 100;
            var inProgressWeight = stages.Count(s => s.Status == StageStatus.InProgress) * 50;

            return (double)(completedWeight + inProgressWeight) / stages.Count;
        }

        /// <summary>
        /// Generate delivery report
        /// </summary>
        public string GenerateDeliveryReport(string projectId)
        {
            var dashboard = GetDashboard(projectId);

            return $@"
================================================================================
                        PROJECT DELIVERY REPORT
================================================================================

Project: {dashboard.ProjectName}
Generated: {dashboard.GeneratedDate:yyyy-MM-dd HH:mm}

--------------------------------------------------------------------------------
OVERALL STATUS
--------------------------------------------------------------------------------
Progress:        {dashboard.OverallProgress:F1}%
Current Stage:   {dashboard.CurrentStage ?? "N/A"}
Days Remaining:  {dashboard.DaysRemaining}

--------------------------------------------------------------------------------
STAGE PROGRESS
--------------------------------------------------------------------------------
Total Stages:    {dashboard.StageSummary.TotalStages}
Completed:       {dashboard.StageSummary.CompletedStages}
In Progress:     {dashboard.StageSummary.InProgressStages}
Current LOD:     {dashboard.StageSummary.CurrentStageLOD ?? "N/A"}

--------------------------------------------------------------------------------
DELIVERABLES
--------------------------------------------------------------------------------
Total:           {dashboard.DeliverableSummary.TotalDeliverables}
Approved:        {dashboard.DeliverableSummary.Approved}
Submitted:       {dashboard.DeliverableSummary.Submitted}
In Progress:     {dashboard.DeliverableSummary.InProgress}
Overdue:         {dashboard.DeliverableSummary.Overdue}

--------------------------------------------------------------------------------
MILESTONES
--------------------------------------------------------------------------------
Total:           {dashboard.MilestoneSummary.TotalMilestones}
Achieved:        {dashboard.MilestoneSummary.Achieved}
Upcoming (14d):  {dashboard.MilestoneSummary.Upcoming}
Overdue:         {dashboard.MilestoneSummary.Overdue}

================================================================================
";
        }

        #endregion

        #region Queries

        public DeliveryProject GetProject(string projectId)
        {
            lock (_lock)
            {
                return _projects.TryGetValue(projectId, out var project) ? project : null;
            }
        }

        public DesignStage GetStage(string stageId)
        {
            lock (_lock)
            {
                return _stages.TryGetValue(stageId, out var stage) ? stage : null;
            }
        }

        public Deliverable GetDeliverable(string deliverableId)
        {
            lock (_lock)
            {
                return _deliverables.TryGetValue(deliverableId, out var del) ? del : null;
            }
        }

        public List<DeliveryTemplate> GetTemplates() => _templates.ToList();

        #endregion
    }

    #region Data Models

    public class DeliveryProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectCode { get; set; }
        public string ClientName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public string TemplateId { get; set; }
        public string DeliveryMethod { get; set; }
        public string ContractType { get; set; }
        public ProjectDeliveryStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<string> Stages { get; set; }
        public List<string> Milestones { get; set; }
        public List<DeliveryTeamMember> TeamMembers { get; set; }
    }

    public class DeliveryProjectRequest
    {
        public string ProjectName { get; set; }
        public string ProjectCode { get; set; }
        public string ClientName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public string TemplateId { get; set; }
        public string DeliveryMethod { get; set; }
        public string ContractType { get; set; }
        public List<DeliveryTeamMember> TeamMembers { get; set; }
    }

    public class DeliveryTeamMember
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Organization { get; set; }
        public string Email { get; set; }
    }

    public class DeliveryTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Region { get; set; }
        public List<StageTemplate> Stages { get; set; }
    }

    public class StageTemplate
    {
        public int StageNumber { get; set; }
        public string Name { get; set; }
        public int DurationPercent { get; set; }
        public string LOD { get; set; }
    }

    public class DesignStage
    {
        public string StageId { get; set; }
        public string ProjectId { get; set; }
        public int StageNumber { get; set; }
        public string StageName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public string RequiredLOD { get; set; }
        public StageStatus Status { get; set; }
        public string CompletedBy { get; set; }
        public List<string> Deliverables { get; set; }
        public List<string> GateReviews { get; set; }
        public List<StageTask> Tasks { get; set; }
    }

    public class StageRequest
    {
        public string ProjectId { get; set; }
        public int StageNumber { get; set; }
        public string StageName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string RequiredLOD { get; set; }
    }

    public class StageTask
    {
        public string TaskId { get; set; }
        public string Name { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public DeliveryTaskStatus Status { get; set; }
    }

    public class StageCompletionResult
    {
        public bool Success { get; set; }
        public string StageId { get; set; }
        public string Message { get; set; }
        public DateTime CompletionDate { get; set; }
        public List<string> PendingDeliverables { get; set; }
    }

    public class DeliveryMilestone
    {
        public string MilestoneId { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public MilestoneType Type { get; set; }
        public bool IsCritical { get; set; }
        public List<string> RequiredDeliverables { get; set; }
        public MilestoneStatus Status { get; set; }
        public string CompletedBy { get; set; }
        public string CompletionNotes { get; set; }
        public int VarianceDays { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MilestoneRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime PlannedDate { get; set; }
        public MilestoneType Type { get; set; }
        public bool IsCritical { get; set; }
        public List<string> RequiredDeliverables { get; set; }
    }

    public class MilestoneCompletionResult
    {
        public bool Success { get; set; }
        public string MilestoneId { get; set; }
        public string Message { get; set; }
        public DateTime CompletionDate { get; set; }
        public int VarianceDays { get; set; }
        public List<string> PendingItems { get; set; }
    }

    public class Deliverable
    {
        public string DeliverableId { get; set; }
        public string ProjectId { get; set; }
        public string StageId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DeliverableType Type { get; set; }
        public string Format { get; set; }
        public DateTime DueDate { get; set; }
        public string ResponsibleParty { get; set; }
        public string Discipline { get; set; }
        public DeliverableStatus Status { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<DeliverableSubmission> Submissions { get; set; }
        public List<DeliverableReview> Reviews { get; set; }
    }

    public class DeliverableRequest
    {
        public string ProjectId { get; set; }
        public string StageId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DeliverableType Type { get; set; }
        public string Format { get; set; }
        public DateTime DueDate { get; set; }
        public string ResponsibleParty { get; set; }
        public string Discipline { get; set; }
    }

    public class DeliverableTemplate
    {
        public string Name { get; set; }
        public DeliverableType Type { get; set; }
        public string Description { get; set; }
        public string ResponsibleParty { get; set; }
    }

    public class DeliverableSubmission
    {
        public string SubmissionId { get; set; }
        public int SubmissionNumber { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Notes { get; set; }
    }

    public class SubmissionRequest
    {
        public string SubmittedBy { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Notes { get; set; }
    }

    public class DeliverableReview
    {
        public string ReviewId { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime ReviewDate { get; set; }
        public DeliverableReviewResult Result { get; set; }
        public string Comments { get; set; }
        public string MarkedUpFile { get; set; }
    }

    public class ReviewRequest
    {
        public string ReviewedBy { get; set; }
        public DeliverableReviewResult Result { get; set; }
        public string Comments { get; set; }
        public string MarkedUpFile { get; set; }
    }

    public class GateReview
    {
        public string ReviewId { get; set; }
        public string ProjectId { get; set; }
        public string StageId { get; set; }
        public string ReviewName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public GateReviewType ReviewType { get; set; }
        public List<GateReviewer> Reviewers { get; set; }
        public List<GateCriterion> Criteria { get; set; }
        public GateReviewStatus Status { get; set; }
        public GateReviewResult OverallResult { get; set; }
        public string ConductedBy { get; set; }
        public List<string> Attendees { get; set; }
        public List<ReviewOutcome> Outcomes { get; set; }
        public string Notes { get; set; }
        public List<GateActionItem> ActionItems { get; set; }
    }

    public class GateReviewRequest
    {
        public string ProjectId { get; set; }
        public string StageId { get; set; }
        public string ReviewName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public GateReviewType ReviewType { get; set; }
        public List<GateReviewer> Reviewers { get; set; }
        public List<GateCriterion> Criteria { get; set; }
    }

    public class GateReviewer
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Organization { get; set; }
    }

    public class GateCriterion
    {
        public string Name { get; set; }
        public bool IsMandatory { get; set; }
    }

    public class ReviewOutcome
    {
        public string CriterionName { get; set; }
        public bool Passed { get; set; }
        public string Comments { get; set; }
    }

    public class GateActionItem
    {
        public string ActionId { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class GateReviewConductRequest
    {
        public string ConductedBy { get; set; }
        public List<string> Attendees { get; set; }
        public List<ReviewOutcome> Outcomes { get; set; }
        public string Notes { get; set; }
        public List<GateActionItem> ActionItems { get; set; }
    }

    public class DeliveryDashboard
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedDate { get; set; }
        public double OverallProgress { get; set; }
        public string CurrentStage { get; set; }
        public int DaysRemaining { get; set; }
        public StageSummary StageSummary { get; set; }
        public DeliverableSummary DeliverableSummary { get; set; }
        public MilestoneSummary MilestoneSummary { get; set; }
        public List<Deliverable> UpcomingDeliverables { get; set; }
        public List<DeliveryMilestone> UpcomingMilestones { get; set; }
    }

    public class StageSummary
    {
        public int TotalStages { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public string CurrentStageLOD { get; set; }
    }

    public class DeliverableSummary
    {
        public int TotalDeliverables { get; set; }
        public int Approved { get; set; }
        public int Submitted { get; set; }
        public int InProgress { get; set; }
        public int Overdue { get; set; }
    }

    public class MilestoneSummary
    {
        public int TotalMilestones { get; set; }
        public int Achieved { get; set; }
        public int Upcoming { get; set; }
        public int Overdue { get; set; }
    }

    public class DeliveryEventArgs : EventArgs
    {
        public DeliveryEventType EventType { get; set; }
        public string ProjectId { get; set; }
        public string EntityId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectDeliveryStatus { Planning, Active, OnHold, Completed, Cancelled }
    public enum StageStatus { NotStarted, InProgress, Completed, OnHold }
    public enum MilestoneStatus { Pending, Achieved, Missed, Cancelled }
    public enum MilestoneType { ClientApproval, StageGate, RegulatorySubmission, Payment, Delivery }
    public enum DeliverableStatus { NotStarted, InProgress, Submitted, UnderReview, RequiresRevision, Approved, Rejected }
    public enum DeliverableType { Model, Drawing, Specification, Report, Data, Document, Other }
    public enum DeliverableReviewResult { Approved, ApprovedWithComments, ReviseResubmit, Rejected }
    public enum GateReviewType { StageGate, ClientReview, RegulatoryReview, QualityAudit }
    public enum GateReviewStatus { Scheduled, InProgress, Completed, Cancelled }
    public enum GateReviewResult { Approved, ConditionalApproval, NotApproved }
    public enum DeliveryTaskStatus { NotStarted, InProgress, Completed, OnHold }
    public enum DeliveryEventType { MilestoneApproaching, DeliverableOverdue, StageCompleted, GateReviewScheduled }

    #endregion
}
