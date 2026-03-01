// StingBIM.AI.Collaboration - Issue Tracking System
// Combines BIM 360 Issues + Revizto Issue Tracking with AI enhancements

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Collaboration.Issues
{
    /// <summary>
    /// Comprehensive issue tracking system combining BIM 360 and Revizto features
    /// with AI-powered analysis, prediction, and resolution suggestions
    /// </summary>
    public class IssueTrackingSystem : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, Issue> _issues = new();
        private readonly ConcurrentDictionary<string, IssueComment> _comments = new();
        private readonly ConcurrentDictionary<string, List<IssueHistory>> _history = new();
        private readonly ConcurrentDictionary<string, IssueTemplate> _templates = new();
        private readonly ConcurrentDictionary<string, IssueWorkflow> _workflows = new();
        private readonly IssueAI _issueAI;
        private readonly object _lockObject = new();
        private int _issueCounter;

        public event EventHandler<IssueCreatedEventArgs>? IssueCreated;
        public event EventHandler<IssueUpdatedEventArgs>? IssueUpdated;
        public event EventHandler<IssueAssignedEventArgs>? IssueAssigned;
        public event EventHandler<IssueResolvedEventArgs>? IssueResolved;
        public event EventHandler<IssueCommentAddedEventArgs>? CommentAdded;
        public event EventHandler<IssueDueSoonEventArgs>? IssueDueSoon;

        public IssueTrackingSystem()
        {
            _issueAI = new IssueAI(this);
            InitializeDefaultWorkflows();
            InitializeDefaultTemplates();
        }

        private void InitializeDefaultWorkflows()
        {
            // BIM 360-style workflow
            _workflows["default"] = new IssueWorkflow
            {
                Id = "default",
                Name = "Default Issue Workflow",
                States = new List<WorkflowState>
                {
                    new() { Id = "open", Name = "Open", IsInitial = true, Color = "#FF6B6B" },
                    new() { Id = "in_progress", Name = "In Progress", Color = "#4ECDC4" },
                    new() { Id = "ready_for_review", Name = "Ready for Review", Color = "#FFE66D" },
                    new() { Id = "closed", Name = "Closed", IsFinal = true, Color = "#95E1A3" },
                    new() { Id = "void", Name = "Void", IsFinal = true, Color = "#CCCCCC" }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { From = "open", To = "in_progress", Name = "Start Work" },
                    new() { From = "open", To = "void", Name = "Void Issue" },
                    new() { From = "in_progress", To = "ready_for_review", Name = "Submit for Review" },
                    new() { From = "in_progress", To = "open", Name = "Return to Open" },
                    new() { From = "ready_for_review", To = "closed", Name = "Close Issue" },
                    new() { From = "ready_for_review", To = "in_progress", Name = "Reopen" },
                    new() { From = "closed", To = "open", Name = "Reopen" }
                }
            };

            // Clash-specific workflow
            _workflows["clash"] = new IssueWorkflow
            {
                Id = "clash",
                Name = "Clash Resolution Workflow",
                States = new List<WorkflowState>
                {
                    new() { Id = "new", Name = "New", IsInitial = true, Color = "#FF6B6B" },
                    new() { Id = "active", Name = "Active", Color = "#FFE66D" },
                    new() { Id = "resolved", Name = "Resolved", Color = "#4ECDC4" },
                    new() { Id = "approved", Name = "Approved", IsFinal = true, Color = "#95E1A3" },
                    new() { Id = "ignored", Name = "Ignored", IsFinal = true, Color = "#CCCCCC" }
                },
                Transitions = new List<WorkflowTransition>
                {
                    new() { From = "new", To = "active", Name = "Acknowledge" },
                    new() { From = "new", To = "ignored", Name = "Ignore" },
                    new() { From = "active", To = "resolved", Name = "Mark Resolved" },
                    new() { From = "resolved", To = "approved", Name = "Approve Resolution" },
                    new() { From = "resolved", To = "active", Name = "Reject Resolution" }
                }
            };
        }

        private void InitializeDefaultTemplates()
        {
            _templates["clash"] = new IssueTemplate
            {
                Id = "clash",
                Name = "Clash Issue",
                IssueType = IssueType.Clash,
                DefaultWorkflowId = "clash",
                RequiredFields = new[] { "ClashGroup", "Element1Id", "Element2Id" },
                CustomFields = new List<CustomFieldDefinition>
                {
                    new() { Name = "ClashGroup", Type = FieldType.Text, Required = true },
                    new() { Name = "Element1Id", Type = FieldType.Text, Required = true },
                    new() { Name = "Element2Id", Type = FieldType.Text, Required = true },
                    new() { Name = "ClashPoint", Type = FieldType.Point3D },
                    new() { Name = "ClashDistance", Type = FieldType.Number }
                }
            };

            _templates["design"] = new IssueTemplate
            {
                Id = "design",
                Name = "Design Issue",
                IssueType = IssueType.Design,
                DefaultWorkflowId = "default",
                CustomFields = new List<CustomFieldDefinition>
                {
                    new() { Name = "Discipline", Type = FieldType.Select, Options = new[] { "Architectural", "Structural", "MEP", "Civil" } },
                    new() { Name = "DesignPhase", Type = FieldType.Select, Options = new[] { "SD", "DD", "CD" } }
                }
            };

            _templates["field"] = new IssueTemplate
            {
                Id = "field",
                Name = "Field Issue",
                IssueType = IssueType.Field,
                DefaultWorkflowId = "default",
                CustomFields = new List<CustomFieldDefinition>
                {
                    new() { Name = "Location", Type = FieldType.Text, Required = true },
                    new() { Name = "SafetyConcern", Type = FieldType.Boolean },
                    new() { Name = "PhotoRequired", Type = FieldType.Boolean }
                }
            };

            _templates["rfi"] = new IssueTemplate
            {
                Id = "rfi",
                Name = "RFI",
                IssueType = IssueType.RFI,
                DefaultWorkflowId = "default",
                CustomFields = new List<CustomFieldDefinition>
                {
                    new() { Name = "RFINumber", Type = FieldType.Text, Required = true },
                    new() { Name = "QuestionType", Type = FieldType.Select, Options = new[] { "Clarification", "Change", "Substitution" } }
                }
            };
        }

        #region Issue CRUD Operations

        /// <summary>
        /// Create a new issue with AI analysis
        /// </summary>
        public async Task<Issue> CreateIssueAsync(
            CreateIssueRequest request,
            CancellationToken ct = default)
        {
            var issueId = GenerateIssueId();
            var template = _templates.GetValueOrDefault(request.TemplateId ?? "design");
            var workflow = _workflows.GetValueOrDefault(template?.DefaultWorkflowId ?? "default")!;

            var issue = new Issue
            {
                Id = issueId,
                Number = Interlocked.Increment(ref _issueCounter),
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
                Priority = request.Priority,
                Status = workflow.States.First(s => s.IsInitial).Id,
                WorkflowId = workflow.Id,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate,
                Location = request.Location,
                Viewpoint = request.Viewpoint,
                LinkedElements = request.LinkedElements ?? new List<LinkedElement>(),
                Attachments = request.Attachments ?? new List<IssueAttachment>(),
                Tags = request.Tags ?? new List<string>(),
                CustomFields = request.CustomFields ?? new Dictionary<string, object>()
            };

            // AI Analysis
            var aiAnalysis = await _issueAI.AnalyzeIssueAsync(issue, ct);
            issue.AIAnalysis = aiAnalysis;

            // Auto-assign based on AI if not specified
            if (string.IsNullOrEmpty(issue.AssignedTo) && !string.IsNullOrEmpty(aiAnalysis.SuggestedAssignee))
            {
                issue.AssignedTo = aiAnalysis.SuggestedAssignee;
            }

            // Auto-set priority based on AI if not specified
            if (request.Priority == IssuePriority.None && aiAnalysis.SuggestedPriority != IssuePriority.None)
            {
                issue.Priority = aiAnalysis.SuggestedPriority;
            }

            _issues[issueId] = issue;
            _history[issueId] = new List<IssueHistory>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..12],
                    IssueId = issueId,
                    Action = HistoryAction.Created,
                    PerformedBy = request.CreatedBy,
                    PerformedAt = DateTime.UtcNow,
                    Details = "Issue created"
                }
            };

            IssueCreated?.Invoke(this, new IssueCreatedEventArgs(issue));

            if (!string.IsNullOrEmpty(issue.AssignedTo))
            {
                IssueAssigned?.Invoke(this, new IssueAssignedEventArgs(issue, issue.AssignedTo));
            }

            return issue;
        }

        /// <summary>
        /// Update an existing issue
        /// </summary>
        public async Task<Issue> UpdateIssueAsync(
            string issueId,
            UpdateIssueRequest request,
            string updatedBy,
            CancellationToken ct = default)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            var changes = new List<string>();

            if (request.Title != null && request.Title != issue.Title)
            {
                changes.Add($"Title changed from '{issue.Title}' to '{request.Title}'");
                issue.Title = request.Title;
            }

            if (request.Description != null && request.Description != issue.Description)
            {
                changes.Add("Description updated");
                issue.Description = request.Description;
            }

            if (request.Priority.HasValue && request.Priority != issue.Priority)
            {
                changes.Add($"Priority changed from {issue.Priority} to {request.Priority}");
                issue.Priority = request.Priority.Value;
            }

            if (request.DueDate.HasValue && request.DueDate != issue.DueDate)
            {
                changes.Add($"Due date changed to {request.DueDate:d}");
                issue.DueDate = request.DueDate;
            }

            if (request.AssignedTo != null && request.AssignedTo != issue.AssignedTo)
            {
                var previousAssignee = issue.AssignedTo;
                changes.Add($"Reassigned from {previousAssignee ?? "unassigned"} to {request.AssignedTo}");
                issue.AssignedTo = request.AssignedTo;
                IssueAssigned?.Invoke(this, new IssueAssignedEventArgs(issue, request.AssignedTo, previousAssignee));
            }

            if (request.Tags != null)
            {
                issue.Tags = request.Tags;
                changes.Add("Tags updated");
            }

            if (request.CustomFields != null)
            {
                foreach (var kvp in request.CustomFields)
                {
                    issue.CustomFields[kvp.Key] = kvp.Value;
                }
                changes.Add("Custom fields updated");
            }

            issue.UpdatedAt = DateTime.UtcNow;
            issue.UpdatedBy = updatedBy;

            // Re-analyze with AI if significant changes
            if (changes.Any())
            {
                issue.AIAnalysis = await _issueAI.AnalyzeIssueAsync(issue, ct);

                AddHistory(issueId, new IssueHistory
                {
                    Id = Guid.NewGuid().ToString("N")[..12],
                    IssueId = issueId,
                    Action = HistoryAction.Updated,
                    PerformedBy = updatedBy,
                    PerformedAt = DateTime.UtcNow,
                    Details = string.Join("; ", changes)
                });
            }

            IssueUpdated?.Invoke(this, new IssueUpdatedEventArgs(issue, changes));

            return issue;
        }

        /// <summary>
        /// Transition issue to a new status
        /// </summary>
        public Issue TransitionIssue(
            string issueId,
            string toStatus,
            string performedBy,
            string? comment = null)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            var workflow = _workflows.GetValueOrDefault(issue.WorkflowId)
                ?? throw new WorkflowNotFoundException(issue.WorkflowId);

            var transition = workflow.Transitions
                .FirstOrDefault(t => t.From == issue.Status && t.To == toStatus)
                ?? throw new InvalidTransitionException(issue.Status, toStatus);

            var fromStatus = issue.Status;
            issue.Status = toStatus;
            issue.UpdatedAt = DateTime.UtcNow;
            issue.UpdatedBy = performedBy;

            var targetState = workflow.States.FirstOrDefault(s => s.Id == toStatus);
            if (targetState?.IsFinal == true)
            {
                issue.ClosedAt = DateTime.UtcNow;
                issue.ClosedBy = performedBy;
                IssueResolved?.Invoke(this, new IssueResolvedEventArgs(issue, performedBy));
            }

            AddHistory(issueId, new IssueHistory
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                IssueId = issueId,
                Action = HistoryAction.StatusChanged,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Details = $"Status changed from '{fromStatus}' to '{toStatus}'",
                PreviousValue = fromStatus,
                NewValue = toStatus
            });

            if (!string.IsNullOrEmpty(comment))
            {
                AddComment(issueId, performedBy, comment);
            }

            IssueUpdated?.Invoke(this, new IssueUpdatedEventArgs(issue, new[] { "Status changed" }.ToList()));

            return issue;
        }

        /// <summary>
        /// Add comment to issue
        /// </summary>
        public IssueComment AddComment(
            string issueId,
            string author,
            string content,
            List<IssueAttachment>? attachments = null,
            CommentType type = CommentType.Comment)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            var comment = new IssueComment
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                IssueId = issueId,
                Author = author,
                Content = content,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                Attachments = attachments ?? new List<IssueAttachment>()
            };

            _comments[comment.Id] = comment;
            issue.CommentCount++;

            AddHistory(issueId, new IssueHistory
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                IssueId = issueId,
                Action = HistoryAction.CommentAdded,
                PerformedBy = author,
                PerformedAt = DateTime.UtcNow,
                Details = $"Comment added: {content[..Math.Min(100, content.Length)]}..."
            });

            CommentAdded?.Invoke(this, new IssueCommentAddedEventArgs(issueId, comment));

            return comment;
        }

        /// <summary>
        /// Get issue by ID
        /// </summary>
        public Issue? GetIssue(string issueId)
            => _issues.TryGetValue(issueId, out var issue) ? issue : null;

        /// <summary>
        /// Get issue by number
        /// </summary>
        public Issue? GetIssueByNumber(int number)
            => _issues.Values.FirstOrDefault(i => i.Number == number);

        /// <summary>
        /// Get issue comments
        /// </summary>
        public List<IssueComment> GetComments(string issueId)
            => _comments.Values.Where(c => c.IssueId == issueId).OrderBy(c => c.CreatedAt).ToList();

        /// <summary>
        /// Get issue history
        /// </summary>
        public List<IssueHistory> GetHistory(string issueId)
            => _history.TryGetValue(issueId, out var history)
                ? history.OrderByDescending(h => h.PerformedAt).ToList()
                : new List<IssueHistory>();

        #endregion

        #region Query & Search

        /// <summary>
        /// Query issues with filtering
        /// </summary>
        public IssueQueryResult QueryIssues(IssueQuery query)
        {
            var issues = _issues.Values.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(query.ProjectId))
                issues = issues.Where(i => i.ProjectId == query.ProjectId);

            if (query.Types?.Any() == true)
                issues = issues.Where(i => query.Types.Contains(i.Type));

            if (query.Statuses?.Any() == true)
                issues = issues.Where(i => query.Statuses.Contains(i.Status));

            if (query.Priorities?.Any() == true)
                issues = issues.Where(i => query.Priorities.Contains(i.Priority));

            if (!string.IsNullOrEmpty(query.AssignedTo))
                issues = issues.Where(i => i.AssignedTo == query.AssignedTo);

            if (!string.IsNullOrEmpty(query.CreatedBy))
                issues = issues.Where(i => i.CreatedBy == query.CreatedBy);

            if (query.CreatedAfter.HasValue)
                issues = issues.Where(i => i.CreatedAt >= query.CreatedAfter);

            if (query.CreatedBefore.HasValue)
                issues = issues.Where(i => i.CreatedAt <= query.CreatedBefore);

            if (query.DueBefore.HasValue)
                issues = issues.Where(i => i.DueDate <= query.DueBefore);

            if (query.Tags?.Any() == true)
                issues = issues.Where(i => i.Tags.Any(t => query.Tags.Contains(t)));

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var search = query.SearchText.ToLowerInvariant();
                issues = issues.Where(i =>
                    i.Title.ToLowerInvariant().Contains(search) ||
                    i.Description?.ToLowerInvariant().Contains(search) == true ||
                    i.Number.ToString().Contains(search));
            }

            // Get total count before pagination
            var totalCount = issues.Count();

            // Apply sorting
            issues = query.SortBy switch
            {
                IssueSortField.Created => query.SortDescending
                    ? issues.OrderByDescending(i => i.CreatedAt)
                    : issues.OrderBy(i => i.CreatedAt),
                IssueSortField.Updated => query.SortDescending
                    ? issues.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                    : issues.OrderBy(i => i.UpdatedAt ?? i.CreatedAt),
                IssueSortField.DueDate => query.SortDescending
                    ? issues.OrderByDescending(i => i.DueDate)
                    : issues.OrderBy(i => i.DueDate),
                IssueSortField.Priority => query.SortDescending
                    ? issues.OrderByDescending(i => i.Priority)
                    : issues.OrderBy(i => i.Priority),
                IssueSortField.Number => query.SortDescending
                    ? issues.OrderByDescending(i => i.Number)
                    : issues.OrderBy(i => i.Number),
                _ => issues.OrderByDescending(i => i.CreatedAt)
            };

            // Apply pagination
            var paged = issues.Skip(query.Skip).Take(query.Take).ToList();

            return new IssueQueryResult
            {
                Issues = paged,
                TotalCount = totalCount,
                Skip = query.Skip,
                Take = query.Take
            };
        }

        /// <summary>
        /// Get issues due soon
        /// </summary>
        public List<Issue> GetIssuesDueSoon(int days = 3)
        {
            var cutoff = DateTime.UtcNow.AddDays(days);
            return _issues.Values
                .Where(i => i.DueDate.HasValue &&
                           i.DueDate <= cutoff &&
                           !IsClosedStatus(i.Status))
                .OrderBy(i => i.DueDate)
                .ToList();
        }

        /// <summary>
        /// Get overdue issues
        /// </summary>
        public List<Issue> GetOverdueIssues()
        {
            return _issues.Values
                .Where(i => i.DueDate.HasValue &&
                           i.DueDate < DateTime.UtcNow &&
                           !IsClosedStatus(i.Status))
                .OrderBy(i => i.DueDate)
                .ToList();
        }

        /// <summary>
        /// Get my issues (assigned to user)
        /// </summary>
        public List<Issue> GetMyIssues(string userId, bool includeCreated = true)
        {
            return _issues.Values
                .Where(i => i.AssignedTo == userId ||
                           (includeCreated && i.CreatedBy == userId))
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .ToList();
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk update multiple issues
        /// </summary>
        public async Task<BulkUpdateResult> BulkUpdateAsync(
            List<string> issueIds,
            BulkUpdateRequest request,
            string performedBy,
            CancellationToken ct = default)
        {
            var results = new BulkUpdateResult();

            foreach (var issueId in issueIds)
            {
                try
                {
                    if (!_issues.TryGetValue(issueId, out var issue))
                    {
                        results.Failed.Add(new BulkUpdateFailure { IssueId = issueId, Error = "Not found" });
                        continue;
                    }

                    if (request.Status != null)
                    {
                        TransitionIssue(issueId, request.Status, performedBy);
                    }

                    if (request.Priority.HasValue)
                    {
                        issue.Priority = request.Priority.Value;
                    }

                    if (request.AssignedTo != null)
                    {
                        issue.AssignedTo = request.AssignedTo;
                    }

                    if (request.DueDate.HasValue)
                    {
                        issue.DueDate = request.DueDate;
                    }

                    if (request.AddTags?.Any() == true)
                    {
                        issue.Tags.AddRange(request.AddTags.Where(t => !issue.Tags.Contains(t)));
                    }

                    if (request.RemoveTags?.Any() == true)
                    {
                        issue.Tags.RemoveAll(t => request.RemoveTags.Contains(t));
                    }

                    issue.UpdatedAt = DateTime.UtcNow;
                    issue.UpdatedBy = performedBy;

                    results.Succeeded.Add(issueId);
                }
                catch (Exception ex)
                {
                    results.Failed.Add(new BulkUpdateFailure { IssueId = issueId, Error = ex.Message });
                }
            }

            return results;
        }

        /// <summary>
        /// Import issues from external system
        /// </summary>
        public async Task<ImportResult> ImportIssuesAsync(
            List<ImportedIssue> issues,
            string importedBy,
            ImportSettings settings,
            CancellationToken ct = default)
        {
            var result = new ImportResult();

            foreach (var imported in issues)
            {
                try
                {
                    var request = new CreateIssueRequest
                    {
                        Title = imported.Title,
                        Description = imported.Description,
                        Type = MapIssueType(imported.ExternalType),
                        Priority = MapPriority(imported.ExternalPriority),
                        CreatedBy = importedBy,
                        DueDate = imported.DueDate,
                        Tags = imported.Tags
                    };

                    var issue = await CreateIssueAsync(request, ct);
                    issue.ExternalId = imported.ExternalId;
                    issue.ExternalSystem = imported.ExternalSystem;

                    result.Imported.Add(issue.Id);
                }
                catch (Exception ex)
                {
                    result.Failed.Add(new ImportFailure
                    {
                        ExternalId = imported.ExternalId,
                        Error = ex.Message
                    });
                }
            }

            return result;
        }

        #endregion

        #region Analytics & Reporting

        /// <summary>
        /// Get issue statistics
        /// </summary>
        public IssueStatistics GetStatistics(string? projectId = null)
        {
            var issues = projectId != null
                ? _issues.Values.Where(i => i.ProjectId == projectId)
                : _issues.Values;

            var issueList = issues.ToList();

            return new IssueStatistics
            {
                TotalCount = issueList.Count,
                OpenCount = issueList.Count(i => !IsClosedStatus(i.Status)),
                ClosedCount = issueList.Count(i => IsClosedStatus(i.Status)),
                OverdueCount = issueList.Count(i => i.DueDate < DateTime.UtcNow && !IsClosedStatus(i.Status)),

                ByType = issueList.GroupBy(i => i.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),

                ByPriority = issueList.GroupBy(i => i.Priority)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),

                ByStatus = issueList.GroupBy(i => i.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),

                ByAssignee = issueList.Where(i => !string.IsNullOrEmpty(i.AssignedTo))
                    .GroupBy(i => i.AssignedTo!)
                    .ToDictionary(g => g.Key, g => g.Count()),

                AverageResolutionTime = CalculateAverageResolutionTime(issueList),
                IssuesCreatedLast7Days = issueList.Count(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-7)),
                IssuesClosedLast7Days = issueList.Count(i => i.ClosedAt >= DateTime.UtcNow.AddDays(-7))
            };
        }

        /// <summary>
        /// Get trending issues (AI-powered)
        /// </summary>
        public async Task<List<IssueTrend>> GetTrendsAsync(
            string? projectId = null,
            CancellationToken ct = default)
        {
            var issues = projectId != null
                ? _issues.Values.Where(i => i.ProjectId == projectId)
                : _issues.Values;

            return await _issueAI.AnalyzeTrendsAsync(issues.ToList(), ct);
        }

        /// <summary>
        /// Get issue insights (AI-powered)
        /// </summary>
        public async Task<IssueInsights> GetInsightsAsync(
            string? projectId = null,
            CancellationToken ct = default)
        {
            var issues = projectId != null
                ? _issues.Values.Where(i => i.ProjectId == projectId)
                : _issues.Values;

            return await _issueAI.GenerateInsightsAsync(issues.ToList(), ct);
        }

        private TimeSpan CalculateAverageResolutionTime(List<Issue> issues)
        {
            var closedIssues = issues.Where(i => i.ClosedAt.HasValue).ToList();
            if (!closedIssues.Any()) return TimeSpan.Zero;

            var totalTicks = closedIssues.Sum(i => (i.ClosedAt!.Value - i.CreatedAt).Ticks);
            return TimeSpan.FromTicks(totalTicks / closedIssues.Count);
        }

        #endregion

        #region Clash Management (Revizto-style)

        /// <summary>
        /// Import clashes from clash detection
        /// </summary>
        public async Task<List<Issue>> ImportClashesAsync(
            List<ClashResult> clashes,
            string importedBy,
            ClashImportSettings settings,
            CancellationToken ct = default)
        {
            var issues = new List<Issue>();

            // Group clashes if requested
            var clashGroups = settings.GroupByElement
                ? clashes.GroupBy(c => $"{c.Element1.CategoryId}_{c.Element2.CategoryId}")
                : clashes.Select(c => new[] { c }.AsEnumerable()).Select(g => g);

            foreach (var group in clashGroups)
            {
                var representative = group.First();
                var clashCount = group.Count();

                var request = new CreateIssueRequest
                {
                    Title = settings.GroupByElement && clashCount > 1
                        ? $"Clash Group: {representative.Element1.Category} vs {representative.Element2.Category} ({clashCount} clashes)"
                        : $"Clash: {representative.Element1.Name} vs {representative.Element2.Name}",
                    Description = GenerateClashDescription(representative, group.ToList()),
                    Type = IssueType.Clash,
                    Priority = MapClashSeverity(representative.Severity),
                    CreatedBy = importedBy,
                    TemplateId = "clash",
                    Location = new IssueLocation
                    {
                        Point = representative.ClashPoint,
                        ViewId = representative.ViewId
                    },
                    LinkedElements = new List<LinkedElement>
                    {
                        new() { ElementId = representative.Element1.Id, ElementName = representative.Element1.Name },
                        new() { ElementId = representative.Element2.Id, ElementName = representative.Element2.Name }
                    },
                    CustomFields = new Dictionary<string, object>
                    {
                        ["ClashGroup"] = representative.GroupName ?? "Ungrouped",
                        ["Element1Id"] = representative.Element1.Id,
                        ["Element2Id"] = representative.Element2.Id,
                        ["ClashDistance"] = representative.Distance
                    }
                };

                var issue = await CreateIssueAsync(request, ct);
                issues.Add(issue);
            }

            return issues;
        }

        private static string GenerateClashDescription(ClashResult clash, List<ClashResult> group)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**Element 1:** {clash.Element1.Name} ({clash.Element1.Category})");
            sb.AppendLine($"**Element 2:** {clash.Element2.Name} ({clash.Element2.Category})");
            sb.AppendLine($"**Clash Point:** {clash.ClashPoint}");
            sb.AppendLine($"**Distance:** {clash.Distance:F2} mm");

            if (group.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine($"This issue represents a group of {group.Count} related clashes.");
            }

            return sb.ToString();
        }

        private static IssuePriority MapClashSeverity(ClashSeverity severity) => severity switch
        {
            ClashSeverity.Critical => IssuePriority.Critical,
            ClashSeverity.Major => IssuePriority.High,
            ClashSeverity.Minor => IssuePriority.Medium,
            _ => IssuePriority.Low
        };

        #endregion

        #region Viewpoints (Revizto-style)

        /// <summary>
        /// Save viewpoint for an issue
        /// </summary>
        public void SaveViewpoint(string issueId, IssueViewpoint viewpoint)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            issue.Viewpoint = viewpoint;
            issue.UpdatedAt = DateTime.UtcNow;

            AddHistory(issueId, new IssueHistory
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                IssueId = issueId,
                Action = HistoryAction.ViewpointSaved,
                PerformedBy = viewpoint.CreatedBy ?? "system",
                PerformedAt = DateTime.UtcNow,
                Details = "Viewpoint saved"
            });
        }

        /// <summary>
        /// Link markup to issue (Revizto-style)
        /// </summary>
        public void LinkMarkup(string issueId, IssueMarkup markup)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            issue.Markups ??= new List<IssueMarkup>();
            issue.Markups.Add(markup);
            issue.UpdatedAt = DateTime.UtcNow;
        }

        #endregion

        #region Push Pins (BIM 360-style)

        /// <summary>
        /// Create push pin at 3D location
        /// </summary>
        public PushPin CreatePushPin(
            string issueId,
            Point3D location,
            string createdBy,
            PushPinType type = PushPinType.Issue)
        {
            if (!_issues.TryGetValue(issueId, out var issue))
                throw new IssueNotFoundException(issueId);

            var pushPin = new PushPin
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                IssueId = issueId,
                Location = location,
                Type = type,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            issue.PushPins ??= new List<PushPin>();
            issue.PushPins.Add(pushPin);

            return pushPin;
        }

        /// <summary>
        /// Get push pins by area
        /// </summary>
        public List<PushPin> GetPushPinsInArea(BoundingBox area)
        {
            return _issues.Values
                .Where(i => i.PushPins != null)
                .SelectMany(i => i.PushPins!)
                .Where(p => area.Contains(p.Location))
                .ToList();
        }

        #endregion

        #region Helper Methods

        private string GenerateIssueId() => Guid.NewGuid().ToString("N")[..16];

        private void AddHistory(string issueId, IssueHistory history)
        {
            lock (_lockObject)
            {
                if (!_history.ContainsKey(issueId))
                    _history[issueId] = new List<IssueHistory>();
                _history[issueId].Add(history);
            }
        }

        private bool IsClosedStatus(string status)
        {
            foreach (var workflow in _workflows.Values)
            {
                var state = workflow.States.FirstOrDefault(s => s.Id == status);
                if (state?.IsFinal == true) return true;
            }
            return false;
        }

        private static IssueType MapIssueType(string? externalType) => externalType?.ToLowerInvariant() switch
        {
            "clash" => IssueType.Clash,
            "rfi" => IssueType.RFI,
            "field" => IssueType.Field,
            "safety" => IssueType.Safety,
            _ => IssueType.Design
        };

        private static IssuePriority MapPriority(string? priority) => priority?.ToLowerInvariant() switch
        {
            "critical" or "urgent" => IssuePriority.Critical,
            "high" => IssuePriority.High,
            "medium" or "normal" => IssuePriority.Medium,
            "low" => IssuePriority.Low,
            _ => IssuePriority.Medium
        };

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        #endregion
    }

    #region Core Models

    public class Issue
    {
        public string Id { get; set; } = "";
        public int Number { get; set; }
        public string? ProjectId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public IssueType Type { get; set; }
        public IssuePriority Priority { get; set; }
        public string Status { get; set; } = "";
        public string WorkflowId { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ClosedBy { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public IssueLocation? Location { get; set; }
        public IssueViewpoint? Viewpoint { get; set; }
        public List<LinkedElement> LinkedElements { get; set; } = new();
        public List<IssueAttachment> Attachments { get; set; } = new();
        public List<IssueMarkup>? Markups { get; set; }
        public List<PushPin>? PushPins { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public IssueAIAnalysis? AIAnalysis { get; set; }
        public string? ExternalId { get; set; }
        public string? ExternalSystem { get; set; }
        public int CommentCount { get; set; }
    }

    public enum IssueType { Design, Clash, Field, RFI, Safety, Quality, Coordination, Other }
    public enum IssuePriority { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }

    public class IssueLocation
    {
        public Point3D? Point { get; set; }
        public string? ViewId { get; set; }
        public string? ViewName { get; set; }
        public string? Level { get; set; }
        public string? Zone { get; set; }
        public string? Room { get; set; }
        public BoundingBox? Area { get; set; }
    }

    public class BoundingBox
    {
        public Point3D Min { get; set; } = new();
        public Point3D Max { get; set; } = new();
        public bool Contains(Point3D point) =>
            point.X >= Min.X && point.X <= Max.X &&
            point.Y >= Min.Y && point.Y <= Max.Y &&
            point.Z >= Min.Z && point.Z <= Max.Z;
    }

    public class IssueViewpoint
    {
        public string Id { get; set; } = "";
        public string? ViewId { get; set; }
        public CameraPosition Camera { get; set; } = new();
        public List<string>? VisibleElements { get; set; }
        public List<string>? HiddenElements { get; set; }
        public List<string>? HighlightedElements { get; set; }
        public string? SectionBox { get; set; }
        public byte[]? Thumbnail { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CameraPosition
    {
        public Point3D Eye { get; set; } = new();
        public Point3D Target { get; set; } = new();
        public Point3D Up { get; set; } = new() { Z = 1 };
        public double FieldOfView { get; set; } = 45;
        public bool IsPerspective { get; set; } = true;
    }

    public class LinkedElement
    {
        public string ElementId { get; set; } = "";
        public string? ElementName { get; set; }
        public string? Category { get; set; }
        public string? ModelId { get; set; }
        public LinkType LinkType { get; set; } = LinkType.Related;
    }

    public enum LinkType { Related, Cause, Affected, Reference }

    public class IssueAttachment
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long FileSize { get; set; }
        public string? StoragePath { get; set; }
        public string? Url { get; set; }
        public string UploadedBy { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public AttachmentType Type { get; set; }
    }

    public enum AttachmentType { File, Image, Screenshot, Viewpoint, Markup }

    public class IssueMarkup
    {
        public string Id { get; set; } = "";
        public MarkupShapeType ShapeType { get; set; }
        public List<Point3D> Points { get; set; } = new();
        public string? Color { get; set; }
        public double? LineWidth { get; set; }
        public string? Text { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public enum MarkupShapeType { Arrow, Cloud, Rectangle, Circle, Line, Text, Freehand }

    public class PushPin
    {
        public string Id { get; set; } = "";
        public string IssueId { get; set; } = "";
        public Point3D Location { get; set; } = new();
        public PushPinType Type { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public enum PushPinType { Issue, Info, Question, Approval }

    #endregion

    #region Workflow Models

    public class IssueWorkflow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<WorkflowState> States { get; set; } = new();
        public List<WorkflowTransition> Transitions { get; set; } = new();
    }

    public class WorkflowState
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Color { get; set; }
        public bool IsInitial { get; set; }
        public bool IsFinal { get; set; }
    }

    public class WorkflowTransition
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string>? AllowedRoles { get; set; }
    }

    #endregion

    #region Template Models

    public class IssueTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public IssueType IssueType { get; set; }
        public string DefaultWorkflowId { get; set; } = "";
        public string[]? RequiredFields { get; set; }
        public List<CustomFieldDefinition> CustomFields { get; set; } = new();
    }

    public class CustomFieldDefinition
    {
        public string Name { get; set; } = "";
        public FieldType Type { get; set; }
        public bool Required { get; set; }
        public string[]? Options { get; set; }
        public object? DefaultValue { get; set; }
    }

    public enum FieldType { Text, Number, Boolean, Date, Select, MultiSelect, User, Point3D }

    #endregion

    #region Request/Response Models

    public class CreateIssueRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public IssueType Type { get; set; } = IssueType.Design;
        public IssuePriority Priority { get; set; } = IssuePriority.None;
        public string CreatedBy { get; set; } = "";
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public IssueLocation? Location { get; set; }
        public IssueViewpoint? Viewpoint { get; set; }
        public List<LinkedElement>? LinkedElements { get; set; }
        public List<IssueAttachment>? Attachments { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, object>? CustomFields { get; set; }
        public string? TemplateId { get; set; }
    }

    public class UpdateIssueRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public IssuePriority? Priority { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, object>? CustomFields { get; set; }
    }

    public class IssueQuery
    {
        public string? ProjectId { get; set; }
        public List<IssueType>? Types { get; set; }
        public List<string>? Statuses { get; set; }
        public List<IssuePriority>? Priorities { get; set; }
        public string? AssignedTo { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public DateTime? DueBefore { get; set; }
        public List<string>? Tags { get; set; }
        public string? SearchText { get; set; }
        public IssueSortField SortBy { get; set; } = IssueSortField.Created;
        public bool SortDescending { get; set; } = true;
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public enum IssueSortField { Created, Updated, DueDate, Priority, Number }

    public class IssueQueryResult
    {
        public List<Issue> Issues { get; set; } = new();
        public int TotalCount { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    public class BulkUpdateRequest
    {
        public string? Status { get; set; }
        public IssuePriority? Priority { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string>? AddTags { get; set; }
        public List<string>? RemoveTags { get; set; }
    }

    public class BulkUpdateResult
    {
        public List<string> Succeeded { get; set; } = new();
        public List<BulkUpdateFailure> Failed { get; set; } = new();
    }

    public class BulkUpdateFailure
    {
        public string IssueId { get; set; } = "";
        public string Error { get; set; } = "";
    }

    #endregion

    #region Comment & History Models

    public class IssueComment
    {
        public string Id { get; set; } = "";
        public string IssueId { get; set; } = "";
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public CommentType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public List<IssueAttachment> Attachments { get; set; } = new();
        public List<string>? Mentions { get; set; }
    }

    public enum CommentType { Comment, SystemMessage, Resolution }

    public class IssueHistory
    {
        public string Id { get; set; } = "";
        public string IssueId { get; set; } = "";
        public HistoryAction Action { get; set; }
        public string PerformedBy { get; set; } = "";
        public DateTime PerformedAt { get; set; }
        public string Details { get; set; } = "";
        public string? PreviousValue { get; set; }
        public string? NewValue { get; set; }
    }

    public enum HistoryAction { Created, Updated, StatusChanged, Assigned, CommentAdded, AttachmentAdded, ViewpointSaved, Linked }

    #endregion

    #region Import/Export Models

    public class ImportedIssue
    {
        public string? ExternalId { get; set; }
        public string? ExternalSystem { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? ExternalType { get; set; }
        public string? ExternalPriority { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class ImportSettings
    {
        public bool MapUsers { get; set; }
        public bool PreserveStatus { get; set; }
        public Dictionary<string, string>? UserMapping { get; set; }
    }

    public class ImportResult
    {
        public List<string> Imported { get; set; } = new();
        public List<ImportFailure> Failed { get; set; } = new();
    }

    public class ImportFailure
    {
        public string? ExternalId { get; set; }
        public string Error { get; set; } = "";
    }

    #endregion

    #region Clash Models

    public class ClashResult
    {
        public string Id { get; set; } = "";
        public ClashElement Element1 { get; set; } = new();
        public ClashElement Element2 { get; set; } = new();
        public Point3D ClashPoint { get; set; } = new();
        public double Distance { get; set; }
        public ClashSeverity Severity { get; set; }
        public string? GroupName { get; set; }
        public string? ViewId { get; set; }
    }

    public class ClashElement
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string? CategoryId { get; set; }
        public string? ModelId { get; set; }
    }

    public enum ClashSeverity { Info, Minor, Major, Critical }

    public class ClashImportSettings
    {
        public bool GroupByElement { get; set; } = true;
        public bool IncludeIgnored { get; set; }
        public double MinDistance { get; set; }
    }

    #endregion

    #region Statistics Models

    public class IssueStatistics
    {
        public int TotalCount { get; set; }
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }
        public int OverdueCount { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByPriority { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public Dictionary<string, int> ByAssignee { get; set; } = new();
        public TimeSpan AverageResolutionTime { get; set; }
        public int IssuesCreatedLast7Days { get; set; }
        public int IssuesClosedLast7Days { get; set; }
    }

    public class IssueTrend
    {
        public string TrendType { get; set; } = "";
        public string Description { get; set; } = "";
        public double Confidence { get; set; }
        public List<string> AffectedIssues { get; set; } = new();
        public TrendDirection Direction { get; set; }
        public string? Recommendation { get; set; }
    }

    public enum TrendDirection { Improving, Worsening, Stable }

    public class IssueInsights
    {
        public List<string> KeyFindings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<RiskArea> RiskAreas { get; set; } = new();
        public List<PerformanceMetric> PerformanceMetrics { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class RiskArea
    {
        public string Area { get; set; } = "";
        public RiskLevel Level { get; set; }
        public string Description { get; set; } = "";
        public List<string> RelatedIssues { get; set; } = new();
    }

    public enum RiskLevel { Low, Medium, High, Critical }

    public class PerformanceMetric
    {
        public string Name { get; set; } = "";
        public double Value { get; set; }
        public double? Target { get; set; }
        public string? Unit { get; set; }
        public MetricTrend Trend { get; set; }
    }

    public enum MetricTrend { Up, Down, Stable }

    #endregion

    #region AI Models

    public class IssueAIAnalysis
    {
        public string? SuggestedAssignee { get; set; }
        public IssuePriority SuggestedPriority { get; set; }
        public List<string> SuggestedTags { get; set; } = new();
        public List<string> SimilarIssues { get; set; } = new();
        public List<string> PotentialCauses { get; set; } = new();
        public List<string> SuggestedResolutions { get; set; } = new();
        public double Confidence { get; set; }
        public TimeSpan? EstimatedResolutionTime { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class IssueAI
    {
        private readonly IssueTrackingSystem _system;

        public IssueAI(IssueTrackingSystem system)
        {
            _system = system;
        }

        public Task<IssueAIAnalysis> AnalyzeIssueAsync(Issue issue, CancellationToken ct)
        {
            var analysis = new IssueAIAnalysis
            {
                SuggestedPriority = InferPriority(issue),
                SuggestedTags = InferTags(issue),
                PotentialCauses = InferCauses(issue),
                SuggestedResolutions = InferResolutions(issue),
                EstimatedResolutionTime = EstimateResolutionTime(issue),
                RiskLevel = AssessRisk(issue),
                Confidence = 0.75
            };

            return Task.FromResult(analysis);
        }

        public Task<List<IssueTrend>> AnalyzeTrendsAsync(List<Issue> issues, CancellationToken ct)
        {
            var trends = new List<IssueTrend>();

            // Analyze clash trends
            var clashIssues = issues.Where(i => i.Type == IssueType.Clash).ToList();
            if (clashIssues.Count > 10)
            {
                var recentClashes = clashIssues.Count(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-7));
                var previousClashes = clashIssues.Count(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-14) && i.CreatedAt < DateTime.UtcNow.AddDays(-7));

                if (recentClashes > previousClashes * 1.2)
                {
                    trends.Add(new IssueTrend
                    {
                        TrendType = "Increasing Clashes",
                        Description = "Clash issues are increasing compared to last week",
                        Direction = TrendDirection.Worsening,
                        Confidence = 0.8,
                        Recommendation = "Review coordination process and model update frequency"
                    });
                }
            }

            // Analyze resolution trends
            var avgResolution = issues
                .Where(i => i.ClosedAt.HasValue)
                .Select(i => (i.ClosedAt!.Value - i.CreatedAt).TotalDays)
                .DefaultIfEmpty(0)
                .Average();

            if (avgResolution > 7)
            {
                trends.Add(new IssueTrend
                {
                    TrendType = "Slow Resolution",
                    Description = $"Average resolution time is {avgResolution:F1} days",
                    Direction = TrendDirection.Worsening,
                    Confidence = 0.85,
                    Recommendation = "Consider increasing team capacity or prioritizing critical issues"
                });
            }

            return Task.FromResult(trends);
        }

        public Task<IssueInsights> GenerateInsightsAsync(List<Issue> issues, CancellationToken ct)
        {
            var openIssues = issues.Where(i => !i.ClosedAt.HasValue).ToList();
            var overdue = issues.Where(i => i.DueDate < DateTime.UtcNow && !i.ClosedAt.HasValue).ToList();

            var insights = new IssueInsights
            {
                GeneratedAt = DateTime.UtcNow,
                KeyFindings = new List<string>
                {
                    $"{openIssues.Count} issues currently open",
                    $"{overdue.Count} issues are overdue",
                    $"Most common issue type: {issues.GroupBy(i => i.Type).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key}"
                },
                Recommendations = new List<string>(),
                RiskAreas = new List<RiskArea>(),
                PerformanceMetrics = new List<PerformanceMetric>
                {
                    new() { Name = "Open Issues", Value = openIssues.Count, Target = 50, Trend = openIssues.Count > 50 ? MetricTrend.Up : MetricTrend.Stable },
                    new() { Name = "Overdue Rate", Value = issues.Count > 0 ? (double)overdue.Count / issues.Count * 100 : 0, Unit = "%", Target = 10 }
                }
            };

            if (overdue.Count > 10)
            {
                insights.Recommendations.Add("Schedule a triage meeting to address overdue issues");
                insights.RiskAreas.Add(new RiskArea
                {
                    Area = "Schedule",
                    Level = RiskLevel.High,
                    Description = "High number of overdue issues may impact project timeline"
                });
            }

            return Task.FromResult(insights);
        }

        private IssuePriority InferPriority(Issue issue)
        {
            var title = issue.Title.ToLowerInvariant();
            var desc = issue.Description?.ToLowerInvariant() ?? "";

            if (title.Contains("critical") || title.Contains("urgent") || title.Contains("safety"))
                return IssuePriority.Critical;
            if (issue.Type == IssueType.Clash || issue.Type == IssueType.Safety)
                return IssuePriority.High;
            if (title.Contains("minor") || title.Contains("cosmetic"))
                return IssuePriority.Low;

            return IssuePriority.Medium;
        }

        private List<string> InferTags(Issue issue)
        {
            var tags = new List<string>();
            var text = $"{issue.Title} {issue.Description}".ToLowerInvariant();

            if (text.Contains("arch")) tags.Add("architectural");
            if (text.Contains("struct")) tags.Add("structural");
            if (text.Contains("mep") || text.Contains("mechanical") || text.Contains("electrical") || text.Contains("plumbing"))
                tags.Add("mep");
            if (text.Contains("coordinate") || text.Contains("clash")) tags.Add("coordination");
            if (text.Contains("rfi") || text.Contains("question")) tags.Add("rfi");

            return tags;
        }

        private List<string> InferCauses(Issue issue)
        {
            var causes = new List<string>();

            if (issue.Type == IssueType.Clash)
            {
                causes.Add("Model coordination timing");
                causes.Add("Design changes not communicated");
            }
            else if (issue.Type == IssueType.Design)
            {
                causes.Add("Incomplete design information");
                causes.Add("Standard not followed");
            }

            return causes;
        }

        private List<string> InferResolutions(Issue issue)
        {
            var resolutions = new List<string>();

            if (issue.Type == IssueType.Clash)
            {
                resolutions.Add("Modify element routing");
                resolutions.Add("Adjust element elevation");
                resolutions.Add("Resize element");
            }
            else
            {
                resolutions.Add("Update design documentation");
                resolutions.Add("Request clarification from designer");
            }

            return resolutions;
        }

        private TimeSpan? EstimateResolutionTime(Issue issue)
        {
            return issue.Priority switch
            {
                IssuePriority.Critical => TimeSpan.FromDays(1),
                IssuePriority.High => TimeSpan.FromDays(3),
                IssuePriority.Medium => TimeSpan.FromDays(7),
                _ => TimeSpan.FromDays(14)
            };
        }

        private RiskLevel AssessRisk(Issue issue)
        {
            if (issue.Type == IssueType.Safety || issue.Priority == IssuePriority.Critical)
                return RiskLevel.Critical;
            if (issue.Priority == IssuePriority.High || issue.DueDate < DateTime.UtcNow.AddDays(2))
                return RiskLevel.High;
            if (issue.Priority == IssuePriority.Medium)
                return RiskLevel.Medium;
            return RiskLevel.Low;
        }
    }

    #endregion

    #region Event Args

    public class IssueCreatedEventArgs : EventArgs
    {
        public Issue Issue { get; }
        public IssueCreatedEventArgs(Issue issue) => Issue = issue;
    }

    public class IssueUpdatedEventArgs : EventArgs
    {
        public Issue Issue { get; }
        public List<string> Changes { get; }
        public IssueUpdatedEventArgs(Issue issue, List<string> changes)
        {
            Issue = issue;
            Changes = changes;
        }
    }

    public class IssueAssignedEventArgs : EventArgs
    {
        public Issue Issue { get; }
        public string AssignedTo { get; }
        public string? PreviousAssignee { get; }
        public IssueAssignedEventArgs(Issue issue, string assignedTo, string? previousAssignee = null)
        {
            Issue = issue;
            AssignedTo = assignedTo;
            PreviousAssignee = previousAssignee;
        }
    }

    public class IssueResolvedEventArgs : EventArgs
    {
        public Issue Issue { get; }
        public string ResolvedBy { get; }
        public IssueResolvedEventArgs(Issue issue, string resolvedBy)
        {
            Issue = issue;
            ResolvedBy = resolvedBy;
        }
    }

    public class IssueCommentAddedEventArgs : EventArgs
    {
        public string IssueId { get; }
        public IssueComment Comment { get; }
        public IssueCommentAddedEventArgs(string issueId, IssueComment comment)
        {
            IssueId = issueId;
            Comment = comment;
        }
    }

    public class IssueDueSoonEventArgs : EventArgs
    {
        public Issue Issue { get; }
        public TimeSpan TimeRemaining { get; }
        public IssueDueSoonEventArgs(Issue issue, TimeSpan timeRemaining)
        {
            Issue = issue;
            TimeRemaining = timeRemaining;
        }
    }

    #endregion

    #region Exceptions

    public class IssueNotFoundException : Exception
    {
        public string IssueId { get; }
        public IssueNotFoundException(string issueId) : base($"Issue not found: {issueId}")
            => IssueId = issueId;
    }

    public class WorkflowNotFoundException : Exception
    {
        public string WorkflowId { get; }
        public WorkflowNotFoundException(string workflowId) : base($"Workflow not found: {workflowId}")
            => WorkflowId = workflowId;
    }

    public class InvalidTransitionException : Exception
    {
        public string FromStatus { get; }
        public string ToStatus { get; }
        public InvalidTransitionException(string from, string to)
            : base($"Invalid transition from '{from}' to '{to}'")
        {
            FromStatus = from;
            ToStatus = to;
        }
    }

    #endregion
}
