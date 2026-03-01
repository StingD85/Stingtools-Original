// ============================================================================
// StingBIM AI - Change Management System
// Comprehensive change order tracking, impact analysis, and workflow management
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ChangeManagement
{
    /// <summary>
    /// Complete change management system for BIM projects.
    /// Tracks change orders, analyzes impacts, and manages approval workflows.
    /// </summary>
    public sealed class ChangeManagementSystem
    {
        private static readonly Lazy<ChangeManagementSystem> _instance =
            new Lazy<ChangeManagementSystem>(() => new ChangeManagementSystem());
        public static ChangeManagementSystem Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ChangeOrder> _changeOrders = new();
        private readonly Dictionary<string, ChangeRequest> _changeRequests = new();
        private readonly List<ChangeAuditEntry> _auditTrail = new();
        private readonly Dictionary<string, ApprovalWorkflow> _workflows = new();

        public event EventHandler<ChangeEventArgs> ChangeRequested;
        public event EventHandler<ChangeEventArgs> ChangeApproved;
        public event EventHandler<ChangeEventArgs> ChangeRejected;
        public event EventHandler<ChangeEventArgs> ImpactAnalysisComplete;

        private ChangeManagementSystem()
        {
            InitializeWorkflows();
        }

        #region Initialization

        private void InitializeWorkflows()
        {
            // Standard approval workflows
            _workflows["minor_change"] = new ApprovalWorkflow
            {
                WorkflowId = "minor_change",
                Name = "Minor Change Approval",
                Description = "For changes under $10,000 with no schedule impact",
                Steps = new List<ApprovalStep>
                {
                    new() { StepOrder = 1, Role = "Project Manager", RequiredLevel = ApprovalLevel.Approve }
                },
                EstimatedDuration = TimeSpan.FromDays(1),
                ThresholdAmount = 10000m
            };

            _workflows["standard_change"] = new ApprovalWorkflow
            {
                WorkflowId = "standard_change",
                Name = "Standard Change Approval",
                Description = "For changes $10,000-$100,000 or with schedule impact",
                Steps = new List<ApprovalStep>
                {
                    new() { StepOrder = 1, Role = "Lead Discipline", RequiredLevel = ApprovalLevel.Review },
                    new() { StepOrder = 2, Role = "Project Manager", RequiredLevel = ApprovalLevel.Approve },
                    new() { StepOrder = 3, Role = "Cost Manager", RequiredLevel = ApprovalLevel.Review }
                },
                EstimatedDuration = TimeSpan.FromDays(5),
                ThresholdAmount = 100000m
            };

            _workflows["major_change"] = new ApprovalWorkflow
            {
                WorkflowId = "major_change",
                Name = "Major Change Approval",
                Description = "For changes over $100,000 or significant scope changes",
                Steps = new List<ApprovalStep>
                {
                    new() { StepOrder = 1, Role = "Lead Discipline", RequiredLevel = ApprovalLevel.Review },
                    new() { StepOrder = 2, Role = "Project Manager", RequiredLevel = ApprovalLevel.Review },
                    new() { StepOrder = 3, Role = "Cost Manager", RequiredLevel = ApprovalLevel.Approve },
                    new() { StepOrder = 4, Role = "Client Representative", RequiredLevel = ApprovalLevel.Approve },
                    new() { StepOrder = 5, Role = "Project Director", RequiredLevel = ApprovalLevel.Approve }
                },
                EstimatedDuration = TimeSpan.FromDays(14),
                ThresholdAmount = decimal.MaxValue
            };

            _workflows["emergency_change"] = new ApprovalWorkflow
            {
                WorkflowId = "emergency_change",
                Name = "Emergency Change",
                Description = "For urgent safety or critical issues",
                Steps = new List<ApprovalStep>
                {
                    new() { StepOrder = 1, Role = "Project Manager", RequiredLevel = ApprovalLevel.EmergencyApprove }
                },
                EstimatedDuration = TimeSpan.FromHours(4),
                ThresholdAmount = 50000m,
                RequiresRetroactiveApproval = true
            };
        }

        #endregion

        #region Change Requests

        /// <summary>
        /// Create a new change request
        /// </summary>
        public ChangeRequest CreateChangeRequest(ChangeRequestInput input)
        {
            var request = new ChangeRequest
            {
                RequestId = $"CR-{DateTime.UtcNow:yyyyMMdd}-{_changeRequests.Count + 1:D4}",
                Title = input.Title,
                Description = input.Description,
                RequestedBy = input.RequestedBy,
                RequestDate = DateTime.UtcNow,
                Category = input.Category,
                Priority = input.Priority,
                AffectedDisciplines = input.AffectedDisciplines,
                AffectedElements = input.AffectedElements,
                Status = ChangeRequestStatus.Draft,
                Attachments = input.Attachments ?? new List<string>()
            };

            lock (_lock)
            {
                _changeRequests[request.RequestId] = request;
                AddAuditEntry(request.RequestId, "Created", input.RequestedBy, "Change request created");
            }

            ChangeRequested?.Invoke(this, new ChangeEventArgs
            {
                Type = ChangeEventType.RequestCreated,
                RequestId = request.RequestId,
                Message = $"Change request {request.RequestId} created"
            });

            return request;
        }

        /// <summary>
        /// Analyze the impact of a proposed change
        /// </summary>
        public async Task<ChangeImpactAnalysis> AnalyzeImpactAsync(string requestId, ModelElements currentModel)
        {
            if (!_changeRequests.TryGetValue(requestId, out var request))
                throw new KeyNotFoundException($"Change request {requestId} not found");

            return await Task.Run(() =>
            {
                var analysis = new ChangeImpactAnalysis
                {
                    RequestId = requestId,
                    AnalyzedAt = DateTime.UtcNow,
                    CostImpact = new CostImpact(),
                    ScheduleImpact = new ScheduleImpact(),
                    ScopeImpact = new ScopeImpact(),
                    AffectedElements = new List<AffectedElement>(),
                    RiskAssessment = new ChangeRiskAssessment()
                };

                // Analyze cost impact
                analysis.CostImpact = AnalyzeCostImpact(request, currentModel);

                // Analyze schedule impact
                analysis.ScheduleImpact = AnalyzeScheduleImpact(request);

                // Analyze scope impact
                analysis.ScopeImpact = AnalyzeScopeImpact(request, currentModel);

                // Identify affected elements
                analysis.AffectedElements = IdentifyAffectedElements(request, currentModel);

                // Risk assessment
                analysis.RiskAssessment = AssessRisk(request, analysis);

                // Update request with analysis
                lock (_lock)
                {
                    request.ImpactAnalysis = analysis;
                    request.Status = ChangeRequestStatus.UnderReview;
                    AddAuditEntry(requestId, "ImpactAnalyzed", "System", "Impact analysis completed");
                }

                ImpactAnalysisComplete?.Invoke(this, new ChangeEventArgs
                {
                    Type = ChangeEventType.ImpactAnalyzed,
                    RequestId = requestId,
                    Message = $"Impact analysis complete. Cost: {analysis.CostImpact.TotalCost:C}, Schedule: {analysis.ScheduleImpact.DaysImpact} days"
                });

                return analysis;
            });
        }

        private CostImpact AnalyzeCostImpact(ChangeRequest request, ModelElements model)
        {
            var impact = new CostImpact();

            // Base cost calculation by category
            var baseCostMultiplier = request.Category switch
            {
                ChangeCategory.Design => 1.5m,
                ChangeCategory.Scope => 2.0m,
                ChangeCategory.Material => 1.2m,
                ChangeCategory.Method => 1.3m,
                ChangeCategory.Schedule => 0.5m,
                ChangeCategory.Coordination => 0.8m,
                _ => 1.0m
            };

            // Calculate direct costs based on affected elements
            var affectedCount = request.AffectedElements?.Count ?? 0;
            impact.DirectCosts = affectedCount * 500m * baseCostMultiplier;

            // Indirect costs (coordination, documentation, etc.)
            impact.IndirectCosts = impact.DirectCosts * 0.15m;

            // Contingency based on risk
            var contingencyRate = request.Priority switch
            {
                ChangePriority.Critical => 0.20m,
                ChangePriority.High => 0.15m,
                ChangePriority.Medium => 0.10m,
                ChangePriority.Low => 0.05m,
                _ => 0.10m
            };
            impact.Contingency = (impact.DirectCosts + impact.IndirectCosts) * contingencyRate;

            impact.TotalCost = impact.DirectCosts + impact.IndirectCosts + impact.Contingency;

            // Cost breakdown by discipline
            impact.ByDiscipline = new Dictionary<string, decimal>();
            foreach (var discipline in request.AffectedDisciplines ?? new List<string>())
            {
                impact.ByDiscipline[discipline] = impact.TotalCost / (request.AffectedDisciplines?.Count ?? 1);
            }

            return impact;
        }

        private ScheduleImpact AnalyzeScheduleImpact(ChangeRequest request)
        {
            var impact = new ScheduleImpact();

            // Base days impact by category
            var baseDays = request.Category switch
            {
                ChangeCategory.Design => 10,
                ChangeCategory.Scope => 15,
                ChangeCategory.Material => 5,
                ChangeCategory.Method => 7,
                ChangeCategory.Schedule => 3,
                ChangeCategory.Coordination => 5,
                _ => 7
            };

            // Adjust by priority
            var priorityMultiplier = request.Priority switch
            {
                ChangePriority.Critical => 0.5, // Expedited handling
                ChangePriority.High => 0.75,
                ChangePriority.Medium => 1.0,
                ChangePriority.Low => 1.25,
                _ => 1.0
            };

            impact.DaysImpact = (int)(baseDays * priorityMultiplier);
            impact.CriticalPathAffected = request.Priority == ChangePriority.Critical ||
                                          request.Category == ChangeCategory.Scope;
            impact.MilestoneImpact = DetermineMilestoneImpact(request);
            impact.ResourceImpact = DetermineResourceImpact(request);

            return impact;
        }

        private string DetermineMilestoneImpact(ChangeRequest request)
        {
            if (request.Category == ChangeCategory.Scope)
                return "Potential impact on major project milestones";
            if (request.Priority == ChangePriority.Critical)
                return "May affect upcoming milestone deadlines";
            return "No significant milestone impact expected";
        }

        private string DetermineResourceImpact(ChangeRequest request)
        {
            var disciplines = request.AffectedDisciplines?.Count ?? 0;
            if (disciplines >= 3)
                return "Significant resource reallocation required across multiple disciplines";
            if (disciplines >= 1)
                return "Moderate resource adjustment needed";
            return "Minimal resource impact";
        }

        private ScopeImpact AnalyzeScopeImpact(ChangeRequest request, ModelElements model)
        {
            var impact = new ScopeImpact();

            impact.ElementsAdded = request.Category == ChangeCategory.Scope ?
                (request.AffectedElements?.Count ?? 0) / 2 : 0;
            impact.ElementsModified = request.AffectedElements?.Count ?? 0;
            impact.ElementsRemoved = request.Category == ChangeCategory.Scope ?
                (request.AffectedElements?.Count ?? 0) / 4 : 0;

            impact.QuantityChanges = CalculateQuantityChanges(request);
            impact.SpecificationChanges = request.Category == ChangeCategory.Material ?
                new List<string> { "Material specifications updated" } :
                new List<string>();

            impact.ScopeChangePercentage = model.Elements?.Count > 0 ?
                (double)impact.ElementsModified / model.Elements.Count * 100 : 0;

            return impact;
        }

        private Dictionary<string, double> CalculateQuantityChanges(ChangeRequest request)
        {
            var changes = new Dictionary<string, double>();

            if (request.Category == ChangeCategory.Scope)
            {
                changes["Concrete (m³)"] = 25.0;
                changes["Steel (kg)"] = 1500.0;
                changes["Drywall (m²)"] = 200.0;
            }
            else if (request.Category == ChangeCategory.Material)
            {
                changes["Substituted Materials"] = request.AffectedElements?.Count ?? 10;
            }

            return changes;
        }

        private List<AffectedElement> IdentifyAffectedElements(ChangeRequest request, ModelElements model)
        {
            var affected = new List<AffectedElement>();

            foreach (var elementId in request.AffectedElements ?? new List<string>())
            {
                var element = model.Elements?.FirstOrDefault(e => e.ElementId == elementId);
                if (element != null)
                {
                    affected.Add(new AffectedElement
                    {
                        ElementId = elementId,
                        ElementType = element.ElementType,
                        Discipline = element.Discipline,
                        ImpactType = DetermineImpactType(request),
                        RequiresRedesign = request.Category == ChangeCategory.Design
                    });
                }
                else
                {
                    // Element reference for new elements
                    affected.Add(new AffectedElement
                    {
                        ElementId = elementId,
                        ImpactType = "New/Modified",
                        RequiresRedesign = true
                    });
                }
            }

            // Add cascade-affected elements
            var cascadeCount = Math.Min(affected.Count * 2, 20);
            for (int i = 0; i < cascadeCount; i++)
            {
                affected.Add(new AffectedElement
                {
                    ElementId = $"CASCADE-{i + 1}",
                    ImpactType = "Cascade Impact",
                    RequiresRedesign = false
                });
            }

            return affected;
        }

        private string DetermineImpactType(ChangeRequest request)
        {
            return request.Category switch
            {
                ChangeCategory.Design => "Redesign Required",
                ChangeCategory.Scope => "Scope Modification",
                ChangeCategory.Material => "Material Change",
                ChangeCategory.Method => "Method Change",
                _ => "Coordination Update"
            };
        }

        private ChangeRiskAssessment AssessRisk(ChangeRequest request, ChangeImpactAnalysis analysis)
        {
            var assessment = new ChangeRiskAssessment
            {
                Risks = new List<IdentifiedRisk>()
            };

            // Cost overrun risk
            if (analysis.CostImpact.TotalCost > 50000)
            {
                assessment.Risks.Add(new IdentifiedRisk
                {
                    RiskId = "COST-01",
                    Category = "Cost",
                    Description = "Significant cost increase may exceed contingency",
                    Likelihood = RiskLikelihood.Medium,
                    Impact = RiskImpact.High,
                    Mitigation = "Conduct value engineering review"
                });
            }

            // Schedule risk
            if (analysis.ScheduleImpact.CriticalPathAffected)
            {
                assessment.Risks.Add(new IdentifiedRisk
                {
                    RiskId = "SCHED-01",
                    Category = "Schedule",
                    Description = "Critical path affected, potential project delay",
                    Likelihood = RiskLikelihood.High,
                    Impact = RiskImpact.High,
                    Mitigation = "Develop acceleration plan"
                });
            }

            // Quality risk
            if (request.Priority == ChangePriority.Critical)
            {
                assessment.Risks.Add(new IdentifiedRisk
                {
                    RiskId = "QUAL-01",
                    Category = "Quality",
                    Description = "Rushed implementation may affect quality",
                    Likelihood = RiskLikelihood.Medium,
                    Impact = RiskImpact.Medium,
                    Mitigation = "Implement additional QA checkpoints"
                });
            }

            // Coordination risk
            if ((request.AffectedDisciplines?.Count ?? 0) >= 3)
            {
                assessment.Risks.Add(new IdentifiedRisk
                {
                    RiskId = "COORD-01",
                    Category = "Coordination",
                    Description = "Multiple disciplines affected, coordination complexity",
                    Likelihood = RiskLikelihood.High,
                    Impact = RiskImpact.Medium,
                    Mitigation = "Schedule dedicated coordination sessions"
                });
            }

            // Calculate overall risk level
            var riskScores = assessment.Risks.Select(r =>
                ((int)r.Likelihood + 1) * ((int)r.Impact + 1)).ToList();
            var avgScore = riskScores.Any() ? riskScores.Average() : 0;

            assessment.OverallRiskLevel = avgScore switch
            {
                > 12 => OverallRisk.Critical,
                > 8 => OverallRisk.High,
                > 4 => OverallRisk.Medium,
                _ => OverallRisk.Low
            };

            return assessment;
        }

        #endregion

        #region Change Orders

        /// <summary>
        /// Convert approved change request to change order
        /// </summary>
        public ChangeOrder CreateChangeOrder(string requestId, string createdBy)
        {
            if (!_changeRequests.TryGetValue(requestId, out var request))
                throw new KeyNotFoundException($"Change request {requestId} not found");

            if (request.Status != ChangeRequestStatus.Approved)
                throw new InvalidOperationException("Only approved requests can become change orders");

            var order = new ChangeOrder
            {
                ChangeOrderId = $"CO-{DateTime.UtcNow:yyyyMMdd}-{_changeOrders.Count + 1:D4}",
                SourceRequestId = requestId,
                Title = request.Title,
                Description = request.Description,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                Status = ChangeOrderStatus.Pending,
                ApprovedCost = request.ImpactAnalysis?.CostImpact.TotalCost ?? 0,
                ApprovedDays = request.ImpactAnalysis?.ScheduleImpact.DaysImpact ?? 0,
                AffectedDisciplines = request.AffectedDisciplines,
                WorkItems = GenerateWorkItems(request)
            };

            lock (_lock)
            {
                _changeOrders[order.ChangeOrderId] = order;
                request.Status = ChangeRequestStatus.Converted;
                request.ChangeOrderId = order.ChangeOrderId;
                AddAuditEntry(order.ChangeOrderId, "Created", createdBy,
                    $"Change order created from request {requestId}");
            }

            return order;
        }

        private List<WorkItem> GenerateWorkItems(ChangeRequest request)
        {
            var items = new List<WorkItem>();
            int order = 1;

            // Design work items
            if (request.Category == ChangeCategory.Design || request.Category == ChangeCategory.Scope)
            {
                items.Add(new WorkItem
                {
                    ItemId = $"WI-{order++}",
                    Description = "Update design documentation",
                    Discipline = request.AffectedDisciplines?.FirstOrDefault() ?? "General",
                    EstimatedHours = 16,
                    Status = WorkItemStatus.NotStarted
                });
            }

            // Model update
            items.Add(new WorkItem
            {
                ItemId = $"WI-{order++}",
                Description = "Update BIM model",
                Discipline = "BIM",
                EstimatedHours = 8,
                Status = WorkItemStatus.NotStarted
            });

            // Coordination
            if ((request.AffectedDisciplines?.Count ?? 0) > 1)
            {
                items.Add(new WorkItem
                {
                    ItemId = $"WI-{order++}",
                    Description = "Cross-discipline coordination",
                    Discipline = "All",
                    EstimatedHours = 4,
                    Status = WorkItemStatus.NotStarted
                });
            }

            // Clash detection
            items.Add(new WorkItem
            {
                ItemId = $"WI-{order++}",
                Description = "Re-run clash detection",
                Discipline = "BIM",
                EstimatedHours = 2,
                Status = WorkItemStatus.NotStarted
            });

            // QA
            items.Add(new WorkItem
            {
                ItemId = $"WI-{order++}",
                Description = "Quality assurance review",
                Discipline = "QA",
                EstimatedHours = 4,
                Status = WorkItemStatus.NotStarted
            });

            return items;
        }

        #endregion

        #region Approval Workflow

        /// <summary>
        /// Submit change request for approval
        /// </summary>
        public void SubmitForApproval(string requestId, string submittedBy)
        {
            lock (_lock)
            {
                if (!_changeRequests.TryGetValue(requestId, out var request))
                    throw new KeyNotFoundException($"Change request {requestId} not found");

                // Determine workflow
                var workflow = DetermineWorkflow(request);
                request.ApprovalWorkflow = workflow;
                request.Status = ChangeRequestStatus.PendingApproval;
                request.CurrentApprovalStep = 1;

                AddAuditEntry(requestId, "SubmittedForApproval", submittedBy,
                    $"Submitted for approval via {workflow.Name}");
            }
        }

        private ApprovalWorkflow DetermineWorkflow(ChangeRequest request)
        {
            var estimatedCost = request.ImpactAnalysis?.CostImpact.TotalCost ?? 0;

            if (request.Priority == ChangePriority.Critical)
                return _workflows["emergency_change"];

            if (estimatedCost > 100000 || request.Category == ChangeCategory.Scope)
                return _workflows["major_change"];

            if (estimatedCost > 10000 || request.ImpactAnalysis?.ScheduleImpact.CriticalPathAffected == true)
                return _workflows["standard_change"];

            return _workflows["minor_change"];
        }

        /// <summary>
        /// Record approval decision
        /// </summary>
        public void RecordApproval(string requestId, string approverId, bool approved, string comments)
        {
            lock (_lock)
            {
                if (!_changeRequests.TryGetValue(requestId, out var request))
                    throw new KeyNotFoundException($"Change request {requestId} not found");

                if (request.Approvals == null)
                    request.Approvals = new List<ApprovalRecord>();

                request.Approvals.Add(new ApprovalRecord
                {
                    ApproverId = approverId,
                    Approved = approved,
                    Comments = comments,
                    Timestamp = DateTime.UtcNow,
                    StepNumber = request.CurrentApprovalStep
                });

                if (approved)
                {
                    var workflow = request.ApprovalWorkflow;
                    if (request.CurrentApprovalStep >= workflow.Steps.Count)
                    {
                        request.Status = ChangeRequestStatus.Approved;
                        ChangeApproved?.Invoke(this, new ChangeEventArgs
                        {
                            Type = ChangeEventType.Approved,
                            RequestId = requestId,
                            Message = $"Change request {requestId} approved"
                        });
                    }
                    else
                    {
                        request.CurrentApprovalStep++;
                    }
                }
                else
                {
                    request.Status = ChangeRequestStatus.Rejected;
                    ChangeRejected?.Invoke(this, new ChangeEventArgs
                    {
                        Type = ChangeEventType.Rejected,
                        RequestId = requestId,
                        Message = $"Change request {requestId} rejected: {comments}"
                    });
                }

                AddAuditEntry(requestId, approved ? "Approved" : "Rejected",
                    approverId, comments);
            }
        }

        #endregion

        #region Audit & Reporting

        private void AddAuditEntry(string id, string action, string user, string description)
        {
            _auditTrail.Add(new ChangeAuditEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                ReferenceId = id,
                Action = action,
                User = user,
                Timestamp = DateTime.UtcNow,
                Description = description
            });
        }

        /// <summary>
        /// Generate change management report
        /// </summary>
        public ChangeManagementReport GenerateReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (_lock)
            {
                var requests = _changeRequests.Values.AsEnumerable();
                if (fromDate.HasValue)
                    requests = requests.Where(r => r.RequestDate >= fromDate.Value);
                if (toDate.HasValue)
                    requests = requests.Where(r => r.RequestDate <= toDate.Value);

                var requestList = requests.ToList();

                return new ChangeManagementReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    TotalRequests = requestList.Count,
                    ByStatus = requestList.GroupBy(r => r.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByCategory = requestList.GroupBy(r => r.Category)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TotalApprovedCost = requestList
                        .Where(r => r.Status == ChangeRequestStatus.Approved)
                        .Sum(r => r.ImpactAnalysis?.CostImpact.TotalCost ?? 0),
                    TotalScheduleImpact = requestList
                        .Where(r => r.Status == ChangeRequestStatus.Approved)
                        .Sum(r => r.ImpactAnalysis?.ScheduleImpact.DaysImpact ?? 0),
                    ApprovalRate = requestList.Count > 0 ?
                        (double)requestList.Count(r => r.Status == ChangeRequestStatus.Approved) / requestList.Count * 100 : 0,
                    AverageProcessingDays = CalculateAverageProcessingDays(requestList),
                    TopAffectedDisciplines = GetTopAffectedDisciplines(requestList)
                };
            }
        }

        private double CalculateAverageProcessingDays(List<ChangeRequest> requests)
        {
            var completed = requests.Where(r =>
                r.Status == ChangeRequestStatus.Approved ||
                r.Status == ChangeRequestStatus.Rejected).ToList();

            if (!completed.Any()) return 0;

            var totalDays = completed.Sum(r =>
            {
                var lastApproval = r.Approvals?.LastOrDefault();
                return lastApproval != null ? (lastApproval.Timestamp - r.RequestDate).TotalDays : 0;
            });

            return totalDays / completed.Count;
        }

        private Dictionary<string, int> GetTopAffectedDisciplines(List<ChangeRequest> requests)
        {
            return requests
                .SelectMany(r => r.AffectedDisciplines ?? new List<string>())
                .GroupBy(d => d)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        #endregion
    }

    #region Data Models

    public class ModelElements
    {
        public List<ModelElement> Elements { get; set; } = new();
    }

    public class ModelElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string Discipline { get; set; }
    }

    public class ChangeRequest
    {
        public string RequestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestDate { get; set; }
        public ChangeCategory Category { get; set; }
        public ChangePriority Priority { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> AffectedElements { get; set; }
        public ChangeRequestStatus Status { get; set; }
        public List<string> Attachments { get; set; }
        public ChangeImpactAnalysis ImpactAnalysis { get; set; }
        public ApprovalWorkflow ApprovalWorkflow { get; set; }
        public int CurrentApprovalStep { get; set; }
        public List<ApprovalRecord> Approvals { get; set; }
        public string ChangeOrderId { get; set; }
    }

    public class ChangeRequestInput
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string RequestedBy { get; set; }
        public ChangeCategory Category { get; set; }
        public ChangePriority Priority { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> AffectedElements { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class ChangeImpactAnalysis
    {
        public string RequestId { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public CostImpact CostImpact { get; set; }
        public ScheduleImpact ScheduleImpact { get; set; }
        public ScopeImpact ScopeImpact { get; set; }
        public List<AffectedElement> AffectedElements { get; set; }
        public ChangeRiskAssessment RiskAssessment { get; set; }
    }

    public class CostImpact
    {
        public decimal DirectCosts { get; set; }
        public decimal IndirectCosts { get; set; }
        public decimal Contingency { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<string, decimal> ByDiscipline { get; set; }
    }

    public class ScheduleImpact
    {
        public int DaysImpact { get; set; }
        public bool CriticalPathAffected { get; set; }
        public string MilestoneImpact { get; set; }
        public string ResourceImpact { get; set; }
    }

    public class ScopeImpact
    {
        public int ElementsAdded { get; set; }
        public int ElementsModified { get; set; }
        public int ElementsRemoved { get; set; }
        public Dictionary<string, double> QuantityChanges { get; set; }
        public List<string> SpecificationChanges { get; set; }
        public double ScopeChangePercentage { get; set; }
    }

    public class AffectedElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string Discipline { get; set; }
        public string ImpactType { get; set; }
        public bool RequiresRedesign { get; set; }
    }

    public class ChangeRiskAssessment
    {
        public List<IdentifiedRisk> Risks { get; set; }
        public OverallRisk OverallRiskLevel { get; set; }
    }

    public class IdentifiedRisk
    {
        public string RiskId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public RiskLikelihood Likelihood { get; set; }
        public RiskImpact Impact { get; set; }
        public string Mitigation { get; set; }
    }

    public class ChangeOrder
    {
        public string ChangeOrderId { get; set; }
        public string SourceRequestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public ChangeOrderStatus Status { get; set; }
        public decimal ApprovedCost { get; set; }
        public int ApprovedDays { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<WorkItem> WorkItems { get; set; }
    }

    public class WorkItem
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public string Discipline { get; set; }
        public double EstimatedHours { get; set; }
        public double ActualHours { get; set; }
        public WorkItemStatus Status { get; set; }
        public string AssignedTo { get; set; }
    }

    public class ApprovalWorkflow
    {
        public string WorkflowId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ApprovalStep> Steps { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public decimal ThresholdAmount { get; set; }
        public bool RequiresRetroactiveApproval { get; set; }
    }

    public class ApprovalStep
    {
        public int StepOrder { get; set; }
        public string Role { get; set; }
        public ApprovalLevel RequiredLevel { get; set; }
    }

    public class ApprovalRecord
    {
        public string ApproverId { get; set; }
        public bool Approved { get; set; }
        public string Comments { get; set; }
        public DateTime Timestamp { get; set; }
        public int StepNumber { get; set; }
    }

    public class ChangeAuditEntry
    {
        public string EntryId { get; set; }
        public string ReferenceId { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
    }

    public class ChangeManagementReport
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public int TotalRequests { get; set; }
        public Dictionary<ChangeRequestStatus, int> ByStatus { get; set; }
        public Dictionary<ChangeCategory, int> ByCategory { get; set; }
        public decimal TotalApprovedCost { get; set; }
        public int TotalScheduleImpact { get; set; }
        public double ApprovalRate { get; set; }
        public double AverageProcessingDays { get; set; }
        public Dictionary<string, int> TopAffectedDisciplines { get; set; }
    }

    public class ChangeEventArgs : EventArgs
    {
        public ChangeEventType Type { get; set; }
        public string RequestId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum ChangeCategory
    {
        Design,
        Scope,
        Material,
        Method,
        Schedule,
        Coordination,
        RFI,
        ValueEngineering
    }

    public enum ChangePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ChangeRequestStatus
    {
        Draft,
        UnderReview,
        PendingApproval,
        Approved,
        Rejected,
        Converted,
        Closed
    }

    public enum ChangeOrderStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }

    public enum WorkItemStatus
    {
        NotStarted,
        InProgress,
        OnHold,
        Completed,
        Cancelled
    }

    public enum ApprovalLevel
    {
        Review,
        Approve,
        EmergencyApprove
    }

    public enum RiskLikelihood
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    public enum RiskImpact
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum OverallRisk
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ChangeEventType
    {
        RequestCreated,
        ImpactAnalyzed,
        SubmittedForApproval,
        Approved,
        Rejected,
        OrderCreated
    }

    #endregion
}
