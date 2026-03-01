// ===================================================================
// StingBIM Change Management Intelligence Engine
// Change request tracking, impact analysis, trend detection, scope management
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ChangeManagementIntelligence
{
    /// <summary>
    /// Comprehensive change management with tracking, impact analysis,
    /// approval workflows, and scope creep detection
    /// </summary>
    public sealed class ChangeManagementIntelligenceEngine
    {
        private static readonly Lazy<ChangeManagementIntelligenceEngine> _instance =
            new Lazy<ChangeManagementIntelligenceEngine>(() => new ChangeManagementIntelligenceEngine());
        public static ChangeManagementIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ChangeProject> _projects;
        private readonly Dictionary<string, ChangeRequest> _changeRequests;
        private readonly Dictionary<string, ChangeOrder> _changeOrders;
        private readonly Dictionary<string, ApprovalWorkflow> _workflows;
        private readonly List<ImpactCategory> _impactCategories;
        private readonly object _lockObject = new object();

        public event EventHandler<ChangeAlertEventArgs> ChangeAlert;
        public event EventHandler<ScopeCreepAlertEventArgs> ScopeCreepDetected;
        public event EventHandler<ApprovalRequiredEventArgs> ApprovalRequired;

        private ChangeManagementIntelligenceEngine()
        {
            _projects = new Dictionary<string, ChangeProject>();
            _changeRequests = new Dictionary<string, ChangeRequest>();
            _changeOrders = new Dictionary<string, ChangeOrder>();
            _workflows = new Dictionary<string, ApprovalWorkflow>();
            _impactCategories = new List<ImpactCategory>();
            InitializeImpactCategories();
            InitializeWorkflowTemplates();
        }

        #region Project & Change Request Management

        public ChangeProject CreateProject(string projectId, string projectName, decimal originalBudget, int originalDuration)
        {
            var project = new ChangeProject
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                OriginalBudget = originalBudget,
                CurrentBudget = originalBudget,
                OriginalDuration = originalDuration,
                CurrentDuration = originalDuration,
                OriginalScope = new ScopeBaseline(),
                CreatedDate = DateTime.Now,
                Status = ProjectStatus.Active,
                ChangeRequests = new List<string>(),
                ChangeOrders = new List<string>()
            };

            lock (_lockObject)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        public ChangeRequest CreateChangeRequest(ChangeRequestInput input)
        {
            var request = new ChangeRequest
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateChangeRequestNumber(input.ProjectId),
                Title = input.Title,
                Description = input.Description,
                Category = input.Category,
                Priority = input.Priority,
                RequestedBy = input.RequestedBy,
                RequestDate = DateTime.Now,
                RequiredDate = input.RequiredDate,
                Status = ChangeRequestStatus.Draft,
                Source = input.Source,
                Justification = input.Justification,
                AffectedAreas = input.AffectedAreas ?? new List<string>(),
                AffectedDisciplines = input.AffectedDisciplines ?? new List<string>(),
                Attachments = input.Attachments ?? new List<string>(),
                History = new List<ChangeHistoryEntry>()
            };

            request.History.Add(new ChangeHistoryEntry
            {
                Date = DateTime.Now,
                Action = "Created",
                User = input.RequestedBy,
                Notes = "Change request created"
            });

            lock (_lockObject)
            {
                _changeRequests[request.Id] = request;

                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.ChangeRequests.Add(request.Id);
                }
            }

            return request;
        }

        public void SubmitChangeRequest(string changeRequestId, string submitter)
        {
            lock (_lockObject)
            {
                if (_changeRequests.TryGetValue(changeRequestId, out var request))
                {
                    request.Status = ChangeRequestStatus.Submitted;
                    request.SubmittedDate = DateTime.Now;
                    request.History.Add(new ChangeHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Submitted",
                        User = submitter,
                        Notes = "Change request submitted for review"
                    });

                    OnApprovalRequired(new ApprovalRequiredEventArgs
                    {
                        ChangeRequestId = changeRequestId,
                        Title = request.Title,
                        Priority = request.Priority
                    });
                }
            }
        }

        private string GenerateChangeRequestNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _changeRequests.Values.Count(cr => cr.ProjectId == projectId) + 1;
                return $"CR-{count:D4}";
            }
        }

        #endregion

        #region Impact Analysis

        public async Task<ImpactAnalysisResult> AnalyzeImpactAsync(string changeRequestId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_changeRequests.TryGetValue(changeRequestId, out var request))
                        return null;

                    var result = new ImpactAnalysisResult
                    {
                        Id = Guid.NewGuid().ToString(),
                        ChangeRequestId = changeRequestId,
                        AnalysisDate = DateTime.Now,
                        CostImpact = new CostImpact(),
                        ScheduleImpact = new ScheduleImpact(),
                        ScopeImpact = new ScopeImpact(),
                        QualityImpact = new QualityImpact(),
                        RiskImpact = new RiskImpact(),
                        DisciplineImpacts = new List<DisciplineImpact>(),
                        Dependencies = new List<ChangeDependency>(),
                        Recommendations = new List<string>()
                    };

                    // Analyze cost impact
                    result.CostImpact = AnalyzeCostImpact(request);

                    // Analyze schedule impact
                    result.ScheduleImpact = AnalyzeScheduleImpact(request);

                    // Analyze scope impact
                    result.ScopeImpact = AnalyzeScopeImpact(request);

                    // Analyze quality impact
                    result.QualityImpact = AnalyzeQualityImpact(request);

                    // Analyze risk impact
                    result.RiskImpact = AnalyzeRiskImpact(request);

                    // Analyze discipline impacts
                    foreach (var discipline in request.AffectedDisciplines)
                    {
                        result.DisciplineImpacts.Add(new DisciplineImpact
                        {
                            Discipline = discipline,
                            EffortHours = EstimateEffortHours(request, discipline),
                            RequiresRedesign = DetermineRedesignRequired(request, discipline),
                            AffectedDocuments = GetAffectedDocuments(request, discipline)
                        });
                    }

                    // Calculate overall impact score
                    result.OverallImpactScore = CalculateOverallImpactScore(result);
                    result.ImpactLevel = DetermineImpactLevel(result.OverallImpactScore);

                    // Generate recommendations
                    GenerateImpactRecommendations(result, request);

                    // Update change request
                    request.ImpactAnalysis = result;
                    request.Status = ChangeRequestStatus.UnderReview;
                    request.History.Add(new ChangeHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Impact Analysis",
                        User = "System",
                        Notes = $"Impact analysis completed. Overall score: {result.OverallImpactScore:F1}"
                    });

                    return result;
                }
            });
        }

        private CostImpact AnalyzeCostImpact(ChangeRequest request)
        {
            var impact = new CostImpact
            {
                DirectCosts = new Dictionary<string, decimal>(),
                IndirectCosts = new Dictionary<string, decimal>()
            };

            // Estimate based on category and affected areas
            var baseCost = request.Category switch
            {
                ChangeCategory.Design => 15000m,
                ChangeCategory.Specification => 8000m,
                ChangeCategory.Scope => 50000m,
                ChangeCategory.Schedule => 5000m,
                ChangeCategory.Quality => 10000m,
                ChangeCategory.Regulatory => 20000m,
                _ => 10000m
            };

            // Adjust for affected areas
            baseCost *= request.AffectedAreas.Count;

            impact.DirectCosts["Labor"] = baseCost * 0.6m;
            impact.DirectCosts["Materials"] = baseCost * 0.3m;
            impact.DirectCosts["Equipment"] = baseCost * 0.1m;

            impact.IndirectCosts["Project Management"] = baseCost * 0.1m;
            impact.IndirectCosts["Overhead"] = baseCost * 0.05m;

            impact.TotalDirectCost = impact.DirectCosts.Values.Sum();
            impact.TotalIndirectCost = impact.IndirectCosts.Values.Sum();
            impact.TotalCost = impact.TotalDirectCost + impact.TotalIndirectCost;
            impact.Contingency = impact.TotalCost * 0.1m;

            return impact;
        }

        private ScheduleImpact AnalyzeScheduleImpact(ChangeRequest request)
        {
            var impact = new ScheduleImpact();

            // Estimate based on category
            var baseDays = request.Category switch
            {
                ChangeCategory.Design => 10,
                ChangeCategory.Specification => 5,
                ChangeCategory.Scope => 20,
                ChangeCategory.Schedule => 0,
                ChangeCategory.Quality => 7,
                ChangeCategory.Regulatory => 15,
                _ => 7
            };

            // Adjust for priority
            baseDays = request.Priority switch
            {
                ChangePriority.Critical => baseDays / 2,
                ChangePriority.High => (int)(baseDays * 0.75),
                ChangePriority.Medium => baseDays,
                ChangePriority.Low => (int)(baseDays * 1.25),
                _ => baseDays
            };

            impact.EstimatedDays = baseDays;
            impact.CriticalPathImpact = request.Priority == ChangePriority.Critical ||
                                       request.Priority == ChangePriority.High;
            impact.AffectedMilestones = new List<string>();

            if (impact.CriticalPathImpact)
            {
                impact.AffectedMilestones.Add("Project Completion");
            }

            impact.FloatConsumed = impact.CriticalPathImpact ? baseDays : baseDays / 2;
            impact.AccelerationPossible = request.Priority == ChangePriority.Critical;
            impact.AccelerationCost = impact.AccelerationPossible ? baseDays * 2000m : 0;

            return impact;
        }

        private ScopeImpact AnalyzeScopeImpact(ChangeRequest request)
        {
            return new ScopeImpact
            {
                ScopeChange = request.Category == ChangeCategory.Scope
                    ? ScopeChangeType.Addition
                    : ScopeChangeType.Modification,
                AffectedWBSElements = request.AffectedAreas,
                NewDeliverables = request.Category == ChangeCategory.Scope
                    ? new List<string> { "New scope element" }
                    : new List<string>(),
                ModifiedDeliverables = request.Category != ChangeCategory.Scope
                    ? request.AffectedAreas
                    : new List<string>(),
                DeletedDeliverables = new List<string>(),
                PercentScopeChange = request.Category == ChangeCategory.Scope ? 2.5 : 0.5
            };
        }

        private QualityImpact AnalyzeQualityImpact(ChangeRequest request)
        {
            return new QualityImpact
            {
                RequiresInspection = request.Category == ChangeCategory.Quality ||
                                    request.Category == ChangeCategory.Specification,
                AffectedSpecifications = request.AffectedDisciplines
                    .Select(d => $"{d} Specification").ToList(),
                TestingRequired = request.Category == ChangeCategory.Quality,
                QualityRiskLevel = request.Priority == ChangePriority.Critical
                    ? QualityRiskLevel.High
                    : QualityRiskLevel.Medium
            };
        }

        private RiskImpact AnalyzeRiskImpact(ChangeRequest request)
        {
            var impact = new RiskImpact
            {
                NewRisks = new List<IdentifiedRisk>(),
                MitigatedRisks = new List<string>(),
                RiskScoreChange = 0
            };

            // Identify new risks based on change
            if (request.Category == ChangeCategory.Scope)
            {
                impact.NewRisks.Add(new IdentifiedRisk
                {
                    Description = "Scope creep risk - change may lead to additional changes",
                    Probability = 0.6,
                    Impact = 0.7,
                    Category = "Scope"
                });
            }

            if (request.Category == ChangeCategory.Schedule)
            {
                impact.NewRisks.Add(new IdentifiedRisk
                {
                    Description = "Schedule compression risk - may impact quality",
                    Probability = 0.5,
                    Impact = 0.6,
                    Category = "Schedule"
                });
            }

            impact.RiskScoreChange = impact.NewRisks.Sum(r => r.Probability * r.Impact);
            impact.OverallRiskLevel = impact.RiskScoreChange > 0.5
                ? RiskLevel.High
                : impact.RiskScoreChange > 0.3
                    ? RiskLevel.Medium
                    : RiskLevel.Low;

            return impact;
        }

        private int EstimateEffortHours(ChangeRequest request, string discipline)
        {
            var baseHours = request.Category switch
            {
                ChangeCategory.Design => 40,
                ChangeCategory.Specification => 20,
                ChangeCategory.Scope => 80,
                _ => 24
            };

            return baseHours;
        }

        private bool DetermineRedesignRequired(ChangeRequest request, string discipline)
        {
            return request.Category == ChangeCategory.Design ||
                   request.Category == ChangeCategory.Scope;
        }

        private List<string> GetAffectedDocuments(ChangeRequest request, string discipline)
        {
            var docs = new List<string>();
            docs.Add($"{discipline} Drawings");
            if (request.Category == ChangeCategory.Specification)
                docs.Add($"{discipline} Specifications");
            if (request.Category == ChangeCategory.Scope)
                docs.Add($"{discipline} Scope Document");
            return docs;
        }

        private double CalculateOverallImpactScore(ImpactAnalysisResult result)
        {
            var costScore = Math.Min(result.CostImpact.TotalCost / 100000m, 1m) * 30;
            var scheduleScore = Math.Min(result.ScheduleImpact.EstimatedDays / 30.0, 1.0) * 30;
            var scopeScore = result.ScopeImpact.PercentScopeChange * 10;
            var riskScore = result.RiskImpact.RiskScoreChange * 30;

            return (double)(costScore + (decimal)scheduleScore + (decimal)scopeScore + (decimal)riskScore);
        }

        private ImpactLevel DetermineImpactLevel(double score)
        {
            if (score >= 70) return ImpactLevel.Critical;
            if (score >= 50) return ImpactLevel.High;
            if (score >= 30) return ImpactLevel.Medium;
            return ImpactLevel.Low;
        }

        private void GenerateImpactRecommendations(ImpactAnalysisResult result, ChangeRequest request)
        {
            if (result.ImpactLevel == ImpactLevel.Critical)
            {
                result.Recommendations.Add("Requires executive approval due to critical impact");
                result.Recommendations.Add("Consider phased implementation to reduce risk");
            }

            if (result.ScheduleImpact.CriticalPathImpact)
            {
                result.Recommendations.Add("Evaluate schedule acceleration options");
                result.Recommendations.Add("Review resource allocation for affected activities");
            }

            if (result.CostImpact.TotalCost > 50000)
            {
                result.Recommendations.Add("Recommend value engineering review");
                result.Recommendations.Add("Consider alternative approaches to reduce cost");
            }

            if (result.RiskImpact.OverallRiskLevel == RiskLevel.High)
            {
                result.Recommendations.Add("Develop detailed risk mitigation plan");
                result.Recommendations.Add("Increase contingency allocation");
            }
        }

        #endregion

        #region Approval Workflow

        public void InitiateApproval(string changeRequestId, string workflowTemplateId)
        {
            lock (_lockObject)
            {
                if (!_changeRequests.TryGetValue(changeRequestId, out var request))
                    return;

                var template = GetWorkflowTemplate(workflowTemplateId);
                if (template == null) return;

                request.ApprovalWorkflow = new ApprovalInstance
                {
                    Id = Guid.NewGuid().ToString(),
                    TemplateId = workflowTemplateId,
                    Status = ApprovalStatus.InProgress,
                    CurrentStep = 0,
                    Steps = template.Steps.Select(s => new ApprovalStepInstance
                    {
                        StepId = s.Id,
                        Name = s.Name,
                        ApproverId = s.ApproverId,
                        Status = ApprovalStepStatus.Pending,
                        RequiredDate = DateTime.Now.AddDays(s.MaxDays)
                    }).ToList(),
                    StartedDate = DateTime.Now
                };

                request.Status = ChangeRequestStatus.PendingApproval;
                request.History.Add(new ChangeHistoryEntry
                {
                    Date = DateTime.Now,
                    Action = "Approval Initiated",
                    User = "System",
                    Notes = $"Approval workflow '{template.Name}' initiated"
                });
            }
        }

        public void ApproveStep(string changeRequestId, string approverId, string comments)
        {
            lock (_lockObject)
            {
                if (!_changeRequests.TryGetValue(changeRequestId, out var request) ||
                    request.ApprovalWorkflow == null)
                    return;

                var currentStep = request.ApprovalWorkflow.Steps[request.ApprovalWorkflow.CurrentStep];
                currentStep.Status = ApprovalStepStatus.Approved;
                currentStep.ApprovedDate = DateTime.Now;
                currentStep.Comments = comments;

                request.History.Add(new ChangeHistoryEntry
                {
                    Date = DateTime.Now,
                    Action = $"Step Approved: {currentStep.Name}",
                    User = approverId,
                    Notes = comments
                });

                // Move to next step or complete
                if (request.ApprovalWorkflow.CurrentStep < request.ApprovalWorkflow.Steps.Count - 1)
                {
                    request.ApprovalWorkflow.CurrentStep++;
                }
                else
                {
                    request.ApprovalWorkflow.Status = ApprovalStatus.Approved;
                    request.ApprovalWorkflow.CompletedDate = DateTime.Now;
                    request.Status = ChangeRequestStatus.Approved;
                    request.ApprovedDate = DateTime.Now;
                    request.ApprovedBy = approverId;
                }
            }
        }

        public void RejectStep(string changeRequestId, string approverId, string reason)
        {
            lock (_lockObject)
            {
                if (!_changeRequests.TryGetValue(changeRequestId, out var request) ||
                    request.ApprovalWorkflow == null)
                    return;

                var currentStep = request.ApprovalWorkflow.Steps[request.ApprovalWorkflow.CurrentStep];
                currentStep.Status = ApprovalStepStatus.Rejected;
                currentStep.ApprovedDate = DateTime.Now;
                currentStep.Comments = reason;

                request.ApprovalWorkflow.Status = ApprovalStatus.Rejected;
                request.ApprovalWorkflow.CompletedDate = DateTime.Now;
                request.Status = ChangeRequestStatus.Rejected;
                request.RejectionReason = reason;

                request.History.Add(new ChangeHistoryEntry
                {
                    Date = DateTime.Now,
                    Action = $"Rejected at: {currentStep.Name}",
                    User = approverId,
                    Notes = reason
                });
            }
        }

        private ApprovalWorkflow GetWorkflowTemplate(string templateId)
        {
            return _workflows.TryGetValue(templateId, out var workflow) ? workflow : null;
        }

        #endregion

        #region Change Order Management

        public ChangeOrder CreateChangeOrder(string changeRequestId)
        {
            lock (_lockObject)
            {
                if (!_changeRequests.TryGetValue(changeRequestId, out var request) ||
                    request.Status != ChangeRequestStatus.Approved)
                    return null;

                var changeOrder = new ChangeOrder
                {
                    Id = Guid.NewGuid().ToString(),
                    ChangeRequestId = changeRequestId,
                    ProjectId = request.ProjectId,
                    Number = GenerateChangeOrderNumber(request.ProjectId),
                    Title = request.Title,
                    Description = request.Description,
                    Status = ChangeOrderStatus.Draft,
                    CostAmount = request.ImpactAnalysis?.CostImpact.TotalCost ?? 0,
                    ScheduleDays = request.ImpactAnalysis?.ScheduleImpact.EstimatedDays ?? 0,
                    CreatedDate = DateTime.Now,
                    LineItems = new List<ChangeOrderLineItem>(),
                    Signatures = new List<ChangeOrderSignature>()
                };

                // Generate line items from impact analysis
                if (request.ImpactAnalysis?.CostImpact != null)
                {
                    foreach (var cost in request.ImpactAnalysis.CostImpact.DirectCosts)
                    {
                        changeOrder.LineItems.Add(new ChangeOrderLineItem
                        {
                            Description = cost.Key,
                            Category = "Direct Cost",
                            Amount = cost.Value
                        });
                    }
                    foreach (var cost in request.ImpactAnalysis.CostImpact.IndirectCosts)
                    {
                        changeOrder.LineItems.Add(new ChangeOrderLineItem
                        {
                            Description = cost.Key,
                            Category = "Indirect Cost",
                            Amount = cost.Value
                        });
                    }
                }

                _changeOrders[changeOrder.Id] = changeOrder;

                if (_projects.TryGetValue(request.ProjectId, out var project))
                {
                    project.ChangeOrders.Add(changeOrder.Id);
                }

                request.ChangeOrderId = changeOrder.Id;
                request.Status = ChangeRequestStatus.ChangeOrderCreated;

                return changeOrder;
            }
        }

        public void ExecuteChangeOrder(string changeOrderId)
        {
            lock (_lockObject)
            {
                if (_changeOrders.TryGetValue(changeOrderId, out var changeOrder))
                {
                    changeOrder.Status = ChangeOrderStatus.Executed;
                    changeOrder.ExecutedDate = DateTime.Now;

                    // Update project totals
                    if (_projects.TryGetValue(changeOrder.ProjectId, out var project))
                    {
                        project.CurrentBudget += changeOrder.CostAmount;
                        project.CurrentDuration += changeOrder.ScheduleDays;
                        project.TotalChangeOrderValue += changeOrder.CostAmount;
                        project.TotalScheduleChange += changeOrder.ScheduleDays;

                        // Check for scope creep
                        var changePercentage = (project.CurrentBudget - project.OriginalBudget) /
                                             project.OriginalBudget * 100;
                        if (changePercentage > 10)
                        {
                            OnScopeCreepDetected(new ScopeCreepAlertEventArgs
                            {
                                ProjectId = project.Id,
                                OriginalBudget = project.OriginalBudget,
                                CurrentBudget = project.CurrentBudget,
                                PercentageChange = changePercentage,
                                Message = $"Project budget has increased by {changePercentage:F1}%"
                            });
                        }
                    }
                }
            }
        }

        private string GenerateChangeOrderNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _changeOrders.Values.Count(co => co.ProjectId == projectId) + 1;
                return $"CO-{count:D3}";
            }
        }

        #endregion

        #region Analytics & Trending

        public ChangeAnalytics GetChangeAnalytics(string projectId)
        {
            lock (_lockObject)
            {
                var requests = _changeRequests.Values.Where(cr => cr.ProjectId == projectId).ToList();
                var orders = _changeOrders.Values.Where(co => co.ProjectId == projectId).ToList();

                var analytics = new ChangeAnalytics
                {
                    ProjectId = projectId,
                    GeneratedDate = DateTime.Now,
                    TotalChangeRequests = requests.Count,
                    PendingRequests = requests.Count(r => r.Status == ChangeRequestStatus.PendingApproval ||
                                                         r.Status == ChangeRequestStatus.UnderReview),
                    ApprovedRequests = requests.Count(r => r.Status == ChangeRequestStatus.Approved ||
                                                          r.Status == ChangeRequestStatus.ChangeOrderCreated ||
                                                          r.Status == ChangeRequestStatus.Implemented),
                    RejectedRequests = requests.Count(r => r.Status == ChangeRequestStatus.Rejected),
                    TotalChangeOrders = orders.Count,
                    ExecutedChangeOrders = orders.Count(o => o.Status == ChangeOrderStatus.Executed),
                    TotalCostChange = orders.Where(o => o.Status == ChangeOrderStatus.Executed)
                                           .Sum(o => o.CostAmount),
                    TotalScheduleChange = orders.Where(o => o.Status == ChangeOrderStatus.Executed)
                                                .Sum(o => o.ScheduleDays),
                    CategoryBreakdown = new Dictionary<ChangeCategory, int>(),
                    SourceBreakdown = new Dictionary<ChangeSource, int>(),
                    MonthlyTrend = new List<MonthlyChangeTrend>(),
                    AverageApprovalDays = 0,
                    TopChangeReasons = new List<ChangeReasonSummary>()
                };

                // Category breakdown
                foreach (ChangeCategory category in Enum.GetValues<ChangeCategory>())
                {
                    analytics.CategoryBreakdown[category] = requests.Count(r => r.Category == category);
                }

                // Source breakdown
                foreach (ChangeSource source in Enum.GetValues<ChangeSource>())
                {
                    analytics.SourceBreakdown[source] = requests.Count(r => r.Source == source);
                }

                // Monthly trend
                var monthlyGroups = requests
                    .GroupBy(r => new { r.RequestDate.Year, r.RequestDate.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var group in monthlyGroups)
                {
                    var monthOrders = orders
                        .Where(o => o.CreatedDate.Year == group.Key.Year &&
                                   o.CreatedDate.Month == group.Key.Month)
                        .ToList();

                    analytics.MonthlyTrend.Add(new MonthlyChangeTrend
                    {
                        Year = group.Key.Year,
                        Month = group.Key.Month,
                        RequestCount = group.Count(),
                        ApprovedCount = group.Count(r => r.Status == ChangeRequestStatus.Approved ||
                                                        r.Status == ChangeRequestStatus.ChangeOrderCreated),
                        TotalCost = monthOrders.Sum(o => o.CostAmount)
                    });
                }

                // Average approval time
                var approvedWithDates = requests
                    .Where(r => r.ApprovedDate != default && r.SubmittedDate != default)
                    .ToList();
                if (approvedWithDates.Any())
                {
                    analytics.AverageApprovalDays = approvedWithDates
                        .Average(r => (r.ApprovedDate - r.SubmittedDate).TotalDays);
                }

                // Top change reasons
                var reasonGroups = requests
                    .GroupBy(r => r.Justification?.Split(' ').FirstOrDefault() ?? "Unknown")
                    .OrderByDescending(g => g.Count())
                    .Take(5);

                foreach (var group in reasonGroups)
                {
                    analytics.TopChangeReasons.Add(new ChangeReasonSummary
                    {
                        Reason = group.Key,
                        Count = group.Count(),
                        TotalCost = group.Where(r => r.ImpactAnalysis != null)
                                        .Sum(r => r.ImpactAnalysis.CostImpact.TotalCost)
                    });
                }

                return analytics;
            }
        }

        public ScopeCreepAnalysis AnalyzeScopeCreep(string projectId)
        {
            lock (_lockObject)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var analysis = new ScopeCreepAnalysis
                {
                    ProjectId = projectId,
                    AnalysisDate = DateTime.Now,
                    OriginalBudget = project.OriginalBudget,
                    CurrentBudget = project.CurrentBudget,
                    OriginalDuration = project.OriginalDuration,
                    CurrentDuration = project.CurrentDuration,
                    BudgetVariance = project.CurrentBudget - project.OriginalBudget,
                    BudgetVariancePercent = (project.CurrentBudget - project.OriginalBudget) /
                                           project.OriginalBudget * 100,
                    ScheduleVariance = project.CurrentDuration - project.OriginalDuration,
                    ScheduleVariancePercent = (project.CurrentDuration - project.OriginalDuration) /
                                             (double)project.OriginalDuration * 100,
                    Contributors = new List<ScopeCreepContributor>(),
                    Recommendations = new List<string>()
                };

                // Identify contributors
                var orders = _changeOrders.Values
                    .Where(co => co.ProjectId == projectId && co.Status == ChangeOrderStatus.Executed)
                    .OrderByDescending(co => co.CostAmount)
                    .ToList();

                foreach (var order in orders.Take(10))
                {
                    var request = _changeRequests.Values.FirstOrDefault(cr => cr.Id == order.ChangeRequestId);
                    analysis.Contributors.Add(new ScopeCreepContributor
                    {
                        ChangeOrderNumber = order.Number,
                        Description = order.Title,
                        CostImpact = order.CostAmount,
                        ScheduleImpact = order.ScheduleDays,
                        Category = request?.Category ?? ChangeCategory.Other,
                        Source = request?.Source ?? ChangeSource.Other
                    });
                }

                // Generate recommendations
                if (analysis.BudgetVariancePercent > 15)
                {
                    analysis.Recommendations.Add("Critical: Budget creep exceeds 15% - implement strict change control");
                    analysis.SeverityLevel = ScopeCreepSeverity.Critical;
                }
                else if (analysis.BudgetVariancePercent > 10)
                {
                    analysis.Recommendations.Add("Warning: Budget creep exceeds 10% - review change approval process");
                    analysis.SeverityLevel = ScopeCreepSeverity.High;
                }
                else if (analysis.BudgetVariancePercent > 5)
                {
                    analysis.Recommendations.Add("Caution: Budget creep at 5-10% - monitor closely");
                    analysis.SeverityLevel = ScopeCreepSeverity.Medium;
                }
                else
                {
                    analysis.SeverityLevel = ScopeCreepSeverity.Low;
                }

                // Source-specific recommendations
                var topSource = analysis.Contributors
                    .GroupBy(c => c.Source)
                    .OrderByDescending(g => g.Sum(c => c.CostImpact))
                    .FirstOrDefault();

                if (topSource != null)
                {
                    analysis.Recommendations.Add($"Top change source: {topSource.Key} - focus mitigation efforts here");
                }

                return analysis;
            }
        }

        #endregion

        #region Helper Methods

        private void InitializeImpactCategories()
        {
            _impactCategories.AddRange(new[]
            {
                new ImpactCategory { Code = "COST", Name = "Cost Impact", Weight = 0.3 },
                new ImpactCategory { Code = "SCHEDULE", Name = "Schedule Impact", Weight = 0.3 },
                new ImpactCategory { Code = "SCOPE", Name = "Scope Impact", Weight = 0.2 },
                new ImpactCategory { Code = "QUALITY", Name = "Quality Impact", Weight = 0.1 },
                new ImpactCategory { Code = "RISK", Name = "Risk Impact", Weight = 0.1 }
            });
        }

        private void InitializeWorkflowTemplates()
        {
            _workflows["standard"] = new ApprovalWorkflow
            {
                Id = "standard",
                Name = "Standard Approval",
                Steps = new List<ApprovalStep>
                {
                    new ApprovalStep { Id = "1", Name = "Technical Review", ApproverId = "technical_lead", Order = 1, MaxDays = 3 },
                    new ApprovalStep { Id = "2", Name = "Project Manager", ApproverId = "project_manager", Order = 2, MaxDays = 2 },
                    new ApprovalStep { Id = "3", Name = "Client Approval", ApproverId = "client_rep", Order = 3, MaxDays = 5 }
                }
            };

            _workflows["expedited"] = new ApprovalWorkflow
            {
                Id = "expedited",
                Name = "Expedited Approval",
                Steps = new List<ApprovalStep>
                {
                    new ApprovalStep { Id = "1", Name = "Project Manager", ApproverId = "project_manager", Order = 1, MaxDays = 1 },
                    new ApprovalStep { Id = "2", Name = "Client Approval", ApproverId = "client_rep", Order = 2, MaxDays = 2 }
                }
            };

            _workflows["major"] = new ApprovalWorkflow
            {
                Id = "major",
                Name = "Major Change Approval",
                Steps = new List<ApprovalStep>
                {
                    new ApprovalStep { Id = "1", Name = "Technical Review", ApproverId = "technical_lead", Order = 1, MaxDays = 5 },
                    new ApprovalStep { Id = "2", Name = "Cost Review", ApproverId = "cost_manager", Order = 2, MaxDays = 3 },
                    new ApprovalStep { Id = "3", Name = "Project Manager", ApproverId = "project_manager", Order = 3, MaxDays = 2 },
                    new ApprovalStep { Id = "4", Name = "Executive Review", ApproverId = "executive", Order = 4, MaxDays = 5 },
                    new ApprovalStep { Id = "5", Name = "Client Approval", ApproverId = "client_rep", Order = 5, MaxDays = 7 }
                }
            };
        }

        #endregion

        #region Events

        private void OnChangeAlert(ChangeAlertEventArgs e)
        {
            ChangeAlert?.Invoke(this, e);
        }

        private void OnScopeCreepDetected(ScopeCreepAlertEventArgs e)
        {
            ScopeCreepDetected?.Invoke(this, e);
        }

        private void OnApprovalRequired(ApprovalRequiredEventArgs e)
        {
            ApprovalRequired?.Invoke(this, e);
        }

        #endregion
    }

    #region Data Models

    public class ChangeProject
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public decimal OriginalBudget { get; set; }
        public decimal CurrentBudget { get; set; }
        public int OriginalDuration { get; set; }
        public int CurrentDuration { get; set; }
        public ScopeBaseline OriginalScope { get; set; }
        public decimal TotalChangeOrderValue { get; set; }
        public int TotalScheduleChange { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> ChangeRequests { get; set; }
        public List<string> ChangeOrders { get; set; }
    }

    public class ScopeBaseline
    {
        public List<string> WBSElements { get; set; } = new List<string>();
        public List<string> Deliverables { get; set; } = new List<string>();
        public DateTime BaselineDate { get; set; }
    }

    public class ChangeRequest
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ChangeCategory Category { get; set; }
        public ChangePriority Priority { get; set; }
        public ChangeSource Source { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime SubmittedDate { get; set; }
        public DateTime ApprovedDate { get; set; }
        public string ApprovedBy { get; set; }
        public ChangeRequestStatus Status { get; set; }
        public string Justification { get; set; }
        public string RejectionReason { get; set; }
        public List<string> AffectedAreas { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> Attachments { get; set; }
        public ImpactAnalysisResult ImpactAnalysis { get; set; }
        public ApprovalInstance ApprovalWorkflow { get; set; }
        public string ChangeOrderId { get; set; }
        public List<ChangeHistoryEntry> History { get; set; }
    }

    public class ChangeRequestInput
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ChangeCategory Category { get; set; }
        public ChangePriority Priority { get; set; }
        public ChangeSource Source { get; set; }
        public string RequestedBy { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string Justification { get; set; }
        public List<string> AffectedAreas { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class ChangeHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public string Notes { get; set; }
    }

    public class ImpactAnalysisResult
    {
        public string Id { get; set; }
        public string ChangeRequestId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public CostImpact CostImpact { get; set; }
        public ScheduleImpact ScheduleImpact { get; set; }
        public ScopeImpact ScopeImpact { get; set; }
        public QualityImpact QualityImpact { get; set; }
        public RiskImpact RiskImpact { get; set; }
        public List<DisciplineImpact> DisciplineImpacts { get; set; }
        public List<ChangeDependency> Dependencies { get; set; }
        public double OverallImpactScore { get; set; }
        public ImpactLevel ImpactLevel { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class CostImpact
    {
        public Dictionary<string, decimal> DirectCosts { get; set; }
        public Dictionary<string, decimal> IndirectCosts { get; set; }
        public decimal TotalDirectCost { get; set; }
        public decimal TotalIndirectCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Contingency { get; set; }
    }

    public class ScheduleImpact
    {
        public int EstimatedDays { get; set; }
        public bool CriticalPathImpact { get; set; }
        public List<string> AffectedMilestones { get; set; }
        public int FloatConsumed { get; set; }
        public bool AccelerationPossible { get; set; }
        public decimal AccelerationCost { get; set; }
    }

    public class ScopeImpact
    {
        public ScopeChangeType ScopeChange { get; set; }
        public List<string> AffectedWBSElements { get; set; }
        public List<string> NewDeliverables { get; set; }
        public List<string> ModifiedDeliverables { get; set; }
        public List<string> DeletedDeliverables { get; set; }
        public double PercentScopeChange { get; set; }
    }

    public class QualityImpact
    {
        public bool RequiresInspection { get; set; }
        public List<string> AffectedSpecifications { get; set; }
        public bool TestingRequired { get; set; }
        public QualityRiskLevel QualityRiskLevel { get; set; }
    }

    public class RiskImpact
    {
        public List<IdentifiedRisk> NewRisks { get; set; }
        public List<string> MitigatedRisks { get; set; }
        public double RiskScoreChange { get; set; }
        public RiskLevel OverallRiskLevel { get; set; }
    }

    public class IdentifiedRisk
    {
        public string Description { get; set; }
        public double Probability { get; set; }
        public double Impact { get; set; }
        public string Category { get; set; }
    }

    public class DisciplineImpact
    {
        public string Discipline { get; set; }
        public int EffortHours { get; set; }
        public bool RequiresRedesign { get; set; }
        public List<string> AffectedDocuments { get; set; }
    }

    public class ChangeDependency
    {
        public string DependentChangeId { get; set; }
        public string Description { get; set; }
        public DependencyType Type { get; set; }
    }

    public class ApprovalWorkflow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<ApprovalStep> Steps { get; set; }
    }

    public class ApprovalStep
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ApproverId { get; set; }
        public int Order { get; set; }
        public int MaxDays { get; set; }
    }

    public class ApprovalInstance
    {
        public string Id { get; set; }
        public string TemplateId { get; set; }
        public ApprovalStatus Status { get; set; }
        public int CurrentStep { get; set; }
        public List<ApprovalStepInstance> Steps { get; set; }
        public DateTime StartedDate { get; set; }
        public DateTime CompletedDate { get; set; }
    }

    public class ApprovalStepInstance
    {
        public string StepId { get; set; }
        public string Name { get; set; }
        public string ApproverId { get; set; }
        public ApprovalStepStatus Status { get; set; }
        public DateTime RequiredDate { get; set; }
        public DateTime ApprovedDate { get; set; }
        public string Comments { get; set; }
    }

    public class ChangeOrder
    {
        public string Id { get; set; }
        public string ChangeRequestId { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ChangeOrderStatus Status { get; set; }
        public decimal CostAmount { get; set; }
        public int ScheduleDays { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ExecutedDate { get; set; }
        public List<ChangeOrderLineItem> LineItems { get; set; }
        public List<ChangeOrderSignature> Signatures { get; set; }
    }

    public class ChangeOrderLineItem
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }

    public class ChangeOrderSignature
    {
        public string SignerId { get; set; }
        public string SignerName { get; set; }
        public string Role { get; set; }
        public DateTime SignedDate { get; set; }
    }

    public class ChangeAnalytics
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalChangeRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int TotalChangeOrders { get; set; }
        public int ExecutedChangeOrders { get; set; }
        public decimal TotalCostChange { get; set; }
        public int TotalScheduleChange { get; set; }
        public Dictionary<ChangeCategory, int> CategoryBreakdown { get; set; }
        public Dictionary<ChangeSource, int> SourceBreakdown { get; set; }
        public List<MonthlyChangeTrend> MonthlyTrend { get; set; }
        public double AverageApprovalDays { get; set; }
        public List<ChangeReasonSummary> TopChangeReasons { get; set; }
    }

    public class MonthlyChangeTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int RequestCount { get; set; }
        public int ApprovedCount { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ChangeReasonSummary
    {
        public string Reason { get; set; }
        public int Count { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ScopeCreepAnalysis
    {
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public decimal OriginalBudget { get; set; }
        public decimal CurrentBudget { get; set; }
        public int OriginalDuration { get; set; }
        public int CurrentDuration { get; set; }
        public decimal BudgetVariance { get; set; }
        public decimal BudgetVariancePercent { get; set; }
        public int ScheduleVariance { get; set; }
        public double ScheduleVariancePercent { get; set; }
        public ScopeCreepSeverity SeverityLevel { get; set; }
        public List<ScopeCreepContributor> Contributors { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ScopeCreepContributor
    {
        public string ChangeOrderNumber { get; set; }
        public string Description { get; set; }
        public decimal CostImpact { get; set; }
        public int ScheduleImpact { get; set; }
        public ChangeCategory Category { get; set; }
        public ChangeSource Source { get; set; }
    }

    public class ImpactCategory
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public double Weight { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectStatus
    {
        Active,
        OnHold,
        Completed,
        Cancelled
    }

    public enum ChangeCategory
    {
        Design,
        Specification,
        Scope,
        Schedule,
        Quality,
        Regulatory,
        Safety,
        Environmental,
        Other
    }

    public enum ChangePriority
    {
        Critical,
        High,
        Medium,
        Low
    }

    public enum ChangeSource
    {
        Owner,
        Designer,
        Contractor,
        Consultant,
        Regulatory,
        SiteCondition,
        ValueEngineering,
        Coordination,
        Other
    }

    public enum ChangeRequestStatus
    {
        Draft,
        Submitted,
        UnderReview,
        PendingApproval,
        Approved,
        Rejected,
        ChangeOrderCreated,
        Implemented,
        Closed
    }

    public enum ImpactLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ScopeChangeType
    {
        Addition,
        Deletion,
        Modification
    }

    public enum QualityRiskLevel
    {
        Low,
        Medium,
        High
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum DependencyType
    {
        Prerequisite,
        Related,
        Blocking
    }

    public enum ApprovalStatus
    {
        NotStarted,
        InProgress,
        Approved,
        Rejected
    }

    public enum ApprovalStepStatus
    {
        Pending,
        Approved,
        Rejected,
        Skipped
    }

    public enum ChangeOrderStatus
    {
        Draft,
        Pending,
        Approved,
        Executed,
        Voided
    }

    public enum ScopeCreepSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Event Args

    public class ChangeAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string ChangeRequestId { get; set; }
        public string Message { get; set; }
    }

    public class ScopeCreepAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public decimal OriginalBudget { get; set; }
        public decimal CurrentBudget { get; set; }
        public decimal PercentageChange { get; set; }
        public string Message { get; set; }
    }

    public class ApprovalRequiredEventArgs : EventArgs
    {
        public string ChangeRequestId { get; set; }
        public string Title { get; set; }
        public ChangePriority Priority { get; set; }
    }

    #endregion
}
