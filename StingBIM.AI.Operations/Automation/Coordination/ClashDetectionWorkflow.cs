// ============================================================================
// StingBIM AI - Clash Detection Workflow
// Automated workflow for running clash detection, generating reports,
// and tracking clash resolution progress over time
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Automation.Coordination
{
    /// <summary>
    /// Automated workflow for clash detection and resolution tracking.
    /// Integrates with CrossDisciplineCoordinator and provides:
    /// - Scheduled clash detection runs
    /// - Progress tracking over time
    /// - Report generation and export
    /// - Integration with issue tracking systems
    /// </summary>
    public class ClashDetectionWorkflow
    {
        private readonly CrossDisciplineCoordinator _coordinator;
        private readonly ClashHistoryManager _historyManager;
        private readonly ClashReportGenerator _reportGenerator;
        private readonly ClashNotificationService _notificationService;
        private readonly WorkflowConfiguration _config;

        public ClashDetectionWorkflow(WorkflowConfiguration config = null)
        {
            _config = config ?? WorkflowConfiguration.Default;
            _coordinator = new CrossDisciplineCoordinator();
            _historyManager = new ClashHistoryManager(_config.HistoryStoragePath);
            _reportGenerator = new ClashReportGenerator();
            _notificationService = new ClashNotificationService(_config);
        }

        #region Workflow Execution

        /// <summary>
        /// Run a complete clash detection workflow
        /// </summary>
        public async Task<WorkflowResult> RunWorkflowAsync(
            MultiDisciplineModel model,
            WorkflowOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= WorkflowOptions.Default;
            var result = new WorkflowResult
            {
                WorkflowId = Guid.NewGuid().ToString(),
                StartTime = DateTime.UtcNow,
                Status = WorkflowStatus.Running
            };

            try
            {
                // Step 1: Run coordination analysis
                result.AddStep("Running clash detection...");
                var coordinationReport = await _coordinator.AnalyzeCoordinationAsync(
                    model,
                    options.CoordinationOptions);

                result.CoordinationReport = coordinationReport;
                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Compare with previous results
                result.AddStep("Comparing with previous analysis...");
                CoordinationDelta delta = null;
                var previousReport = await _historyManager.GetLatestReportAsync(model.ModelId);

                if (previousReport != null)
                {
                    delta = await _coordinator.CheckForChangesAsync(model, previousReport);
                    result.Delta = delta;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Step 3: Auto-resolve if enabled
                if (options.EnableAutoResolution && coordinationReport.ClashResults.Any())
                {
                    result.AddStep("Attempting automatic resolution...");
                    var autoResult = await _coordinator.AutoResolveClashesAsync(
                        model,
                        coordinationReport.ClashResults,
                        options.AutoResolutionOptions);

                    result.AutoResolutionResult = autoResult;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Generate reports
                result.AddStep("Generating reports...");
                var reports = await GenerateReportsAsync(coordinationReport, delta, options);
                result.GeneratedReports = reports;

                // Step 5: Save to history
                result.AddStep("Saving to history...");
                await _historyManager.SaveReportAsync(model.ModelId, coordinationReport);

                // Step 6: Send notifications
                if (options.EnableNotifications)
                {
                    result.AddStep("Sending notifications...");
                    await SendNotificationsAsync(coordinationReport, delta, options);
                }

                // Step 7: Generate action items
                result.AddStep("Generating action items...");
                result.ActionItems = GenerateActionItems(coordinationReport, delta);

                result.Status = WorkflowStatus.Completed;
                result.EndTime = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                result.Status = WorkflowStatus.Cancelled;
                result.EndTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                result.Status = WorkflowStatus.Failed;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// Run clash detection for specific discipline pairs
        /// </summary>
        public async Task<WorkflowResult> RunTargetedClashDetectionAsync(
            MultiDisciplineModel model,
            List<DisciplinePair> disciplinePairs,
            CancellationToken cancellationToken = default)
        {
            var options = WorkflowOptions.Default;
            options.CoordinationOptions.DisciplinesToCheck = disciplinePairs
                .SelectMany(p => new[] { p.Discipline1, p.Discipline2 })
                .Distinct()
                .ToList();

            var result = await RunWorkflowAsync(model, options, cancellationToken);

            // Filter to only requested pairs
            if (result.CoordinationReport != null)
            {
                result.CoordinationReport.ClashResults = result.CoordinationReport.ClashResults
                    .Where(c => disciplinePairs.Any(p =>
                        (c.PrimaryDiscipline == p.Discipline1 && c.SecondaryDiscipline == p.Discipline2) ||
                        (c.PrimaryDiscipline == p.Discipline2 && c.SecondaryDiscipline == p.Discipline1)))
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Run incremental clash detection (only new/modified elements)
        /// </summary>
        public async Task<WorkflowResult> RunIncrementalDetectionAsync(
            MultiDisciplineModel model,
            List<string> modifiedElementIds,
            CancellationToken cancellationToken = default)
        {
            var options = WorkflowOptions.Default;
            options.CoordinationOptions = new CoordinationOptions
            {
                ClashTolerance = 25,
                IncludeClearanceChecks = true,
                // In real implementation, would filter to only check modified elements
            };

            return await RunWorkflowAsync(model, options, cancellationToken);
        }

        #endregion

        #region Report Generation

        private async Task<List<GeneratedReport>> GenerateReportsAsync(
            CoordinationReport report,
            CoordinationDelta delta,
            WorkflowOptions options)
        {
            var reports = new List<GeneratedReport>();

            foreach (var format in options.ReportFormats)
            {
                var generatedReport = await GenerateReportAsync(report, delta, format);
                if (generatedReport != null)
                {
                    reports.Add(generatedReport);
                }
            }

            return reports;
        }

        private async Task<GeneratedReport> GenerateReportAsync(
            CoordinationReport report,
            CoordinationDelta delta,
            ReportFormat format)
        {
            await Task.Delay(10); // Simulate async generation

            var content = format switch
            {
                ReportFormat.HTML => _reportGenerator.GenerateHtmlReport(report, delta),
                ReportFormat.PDF => _reportGenerator.GeneratePdfReport(report, delta),
                ReportFormat.Excel => _reportGenerator.GenerateExcelReport(report, delta),
                ReportFormat.BCF => _reportGenerator.GenerateBcfReport(report),
                ReportFormat.JSON => _reportGenerator.GenerateJsonReport(report, delta),
                _ => null
            };

            if (content == null) return null;

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"ClashReport_{report.ModelId}_{timestamp}.{GetFileExtension(format)}";

            return new GeneratedReport
            {
                Format = format,
                FileName = fileName,
                Content = content,
                GeneratedAt = DateTime.UtcNow,
                FileSize = content.Length
            };
        }

        private string GetFileExtension(ReportFormat format)
        {
            return format switch
            {
                ReportFormat.HTML => "html",
                ReportFormat.PDF => "pdf",
                ReportFormat.Excel => "xlsx",
                ReportFormat.BCF => "bcf",
                ReportFormat.JSON => "json",
                _ => "txt"
            };
        }

        #endregion

        #region Notifications

        private async Task SendNotificationsAsync(
            CoordinationReport report,
            CoordinationDelta delta,
            WorkflowOptions options)
        {
            // Notify if critical clashes found
            var criticalClashes = report.ClashResults
                .Where(c => c.Severity == ClashSeverity.Critical)
                .ToList();

            if (criticalClashes.Any())
            {
                await _notificationService.SendCriticalClashAlertAsync(
                    criticalClashes,
                    options.NotificationRecipients);
            }

            // Notify of new clashes if delta available
            if (delta != null && delta.NewClashes.Any())
            {
                await _notificationService.SendNewClashesNotificationAsync(
                    delta.NewClashes,
                    options.NotificationRecipients);
            }

            // Send summary to project team
            if (options.SendDailySummary)
            {
                await _notificationService.SendSummaryAsync(
                    report,
                    delta,
                    options.NotificationRecipients);
            }
        }

        #endregion

        #region Action Items

        private List<ActionItem> GenerateActionItems(
            CoordinationReport report,
            CoordinationDelta delta)
        {
            var items = new List<ActionItem>();

            // Critical clashes need immediate attention
            foreach (var clash in report.ClashResults.Where(c => c.Severity == ClashSeverity.Critical))
            {
                items.Add(new ActionItem
                {
                    Priority = ActionPriority.Critical,
                    Title = $"Resolve critical clash: {clash.PrimaryDiscipline} vs {clash.SecondaryDiscipline}",
                    Description = $"Critical clash at {clash.Level}, {clash.GridIntersection}. " +
                                  $"Penetration: {clash.PenetrationDepth:F0}mm",
                    AssignedDiscipline = GetResponsibleDiscipline(clash),
                    DueDate = DateTime.UtcNow.AddDays(1),
                    RelatedClashIds = new List<string> { clash.ClashId }
                });
            }

            // Hotspots need coordination meetings
            foreach (var hotspot in report.Hotspots.Where(h => h.ClashCount > 5))
            {
                items.Add(new ActionItem
                {
                    Priority = ActionPriority.High,
                    Title = $"Schedule coordination meeting for {hotspot.Region}",
                    Description = $"{hotspot.ClashCount} clashes detected. {hotspot.PrimaryIssue}. " +
                                  $"Recommendation: {hotspot.RecommendedAction}",
                    AssignedDiscipline = Discipline.Architectural, // Typically arch coordinates
                    DueDate = DateTime.UtcNow.AddDays(3),
                    RelatedClashIds = hotspot.Clashes.Select(c => c.ClashId).ToList()
                });
            }

            // Rule violations need design review
            foreach (var violation in report.RuleViolations)
            {
                items.Add(new ActionItem
                {
                    Priority = ActionPriority.Medium,
                    Title = $"Address {violation.RuleType} violation",
                    Description = violation.Description,
                    AssignedDiscipline = Discipline.Architectural,
                    DueDate = DateTime.UtcNow.AddDays(5)
                });
            }

            // Track regression (previously resolved clashes reappearing)
            if (delta != null)
            {
                var regressions = delta.NewClashes
                    .Where(c => delta.ResolvedClashes.Any(r =>
                        r.Element1Id == c.Element1Id && r.Element2Id == c.Element2Id))
                    .ToList();

                foreach (var regression in regressions)
                {
                    items.Add(new ActionItem
                    {
                        Priority = ActionPriority.High,
                        Title = "Clash regression detected",
                        Description = $"Previously resolved clash has reappeared at {regression.Level}",
                        AssignedDiscipline = GetResponsibleDiscipline(regression),
                        DueDate = DateTime.UtcNow.AddDays(2),
                        RelatedClashIds = new List<string> { regression.ClashId }
                    });
                }
            }

            return items.OrderBy(i => i.Priority).ThenBy(i => i.DueDate).ToList();
        }

        private Discipline GetResponsibleDiscipline(Clash clash)
        {
            // Lower priority discipline (higher number) typically moves
            var priority1 = GetDisciplinePriority(clash.PrimaryDiscipline);
            var priority2 = GetDisciplinePriority(clash.SecondaryDiscipline);

            return priority1 > priority2 ? clash.PrimaryDiscipline : clash.SecondaryDiscipline;
        }

        private int GetDisciplinePriority(Discipline discipline)
        {
            return discipline switch
            {
                Discipline.Structural => 1,
                Discipline.Architectural => 2,
                Discipline.Mechanical => 3,
                Discipline.Plumbing => 4,
                Discipline.Electrical => 5,
                Discipline.FireProtection => 6,
                _ => 10
            };
        }

        #endregion

        #region History and Trends

        /// <summary>
        /// Get clash trend over time
        /// </summary>
        public async Task<ClashTrendAnalysis> GetClashTrendAsync(
            string modelId,
            DateTime startDate,
            DateTime endDate)
        {
            var history = await _historyManager.GetReportHistoryAsync(modelId, startDate, endDate);

            var trend = new ClashTrendAnalysis
            {
                ModelId = modelId,
                StartDate = startDate,
                EndDate = endDate,
                DataPoints = new List<TrendDataPoint>()
            };

            foreach (var report in history.OrderBy(r => r.AnalyzedAt))
            {
                trend.DataPoints.Add(new TrendDataPoint
                {
                    Date = report.AnalyzedAt,
                    TotalClashes = report.ClashResults.Count,
                    CriticalClashes = report.ClashResults.Count(c => c.Severity == ClashSeverity.Critical),
                    MajorClashes = report.ClashResults.Count(c => c.Severity == ClashSeverity.Major),
                    MinorClashes = report.ClashResults.Count(c => c.Severity == ClashSeverity.Minor),
                    WarningClashes = report.ClashResults.Count(c => c.Severity == ClashSeverity.Warning)
                });
            }

            // Calculate trend direction
            if (trend.DataPoints.Count >= 2)
            {
                var recent = trend.DataPoints.TakeLast(3).Average(d => d.TotalClashes);
                var earlier = trend.DataPoints.Take(3).Average(d => d.TotalClashes);

                trend.TrendDirection = recent < earlier ? TrendDirection.Improving :
                                       recent > earlier ? TrendDirection.Worsening :
                                       TrendDirection.Stable;

                trend.ChangePercentage = earlier > 0 ? ((recent - earlier) / earlier) * 100 : 0;
            }

            return trend;
        }

        /// <summary>
        /// Get clash resolution statistics
        /// </summary>
        public async Task<ResolutionStatistics> GetResolutionStatisticsAsync(
            string modelId,
            DateTime startDate,
            DateTime endDate)
        {
            var history = await _historyManager.GetReportHistoryAsync(modelId, startDate, endDate);

            var stats = new ResolutionStatistics
            {
                ModelId = modelId,
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}"
            };

            // Track unique clashes and their resolution
            var allClashes = new Dictionary<string, ClashLifecycle>();

            foreach (var report in history.OrderBy(r => r.AnalyzedAt))
            {
                foreach (var clash in report.ClashResults)
                {
                    if (!allClashes.ContainsKey(clash.ClashId))
                    {
                        allClashes[clash.ClashId] = new ClashLifecycle
                        {
                            ClashId = clash.ClashId,
                            FirstDetected = report.AnalyzedAt,
                            Severity = clash.Severity
                        };
                    }

                    allClashes[clash.ClashId].LastSeen = report.AnalyzedAt;
                }

                // Mark clashes not in this report as resolved
                var currentIds = report.ClashResults.Select(c => c.ClashId).ToHashSet();
                foreach (var kvp in allClashes.Where(a => a.Value.ResolvedAt == null &&
                                                          !currentIds.Contains(a.Key)))
                {
                    kvp.Value.ResolvedAt = report.AnalyzedAt;
                }
            }

            stats.TotalClashesDetected = allClashes.Count;
            stats.ClashesResolved = allClashes.Count(c => c.Value.ResolvedAt != null);
            stats.ClashesRemaining = allClashes.Count(c => c.Value.ResolvedAt == null);
            stats.ResolutionRate = stats.TotalClashesDetected > 0
                ? (double)stats.ClashesResolved / stats.TotalClashesDetected * 100
                : 0;

            // Calculate average resolution time
            var resolvedClashes = allClashes.Values
                .Where(c => c.ResolvedAt != null)
                .ToList();

            if (resolvedClashes.Any())
            {
                stats.AverageResolutionTime = TimeSpan.FromTicks(
                    (long)resolvedClashes.Average(c =>
                        (c.ResolvedAt.Value - c.FirstDetected).Ticks));
            }

            // Breakdown by severity
            stats.ResolutionBySeverity = allClashes.Values
                .Where(c => c.ResolvedAt != null)
                .GroupBy(c => c.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            return stats;
        }

        #endregion
    }

    #region Supporting Classes

    public class ClashHistoryManager
    {
        private readonly string _storagePath;
        private readonly Dictionary<string, List<CoordinationReport>> _cache;

        public ClashHistoryManager(string storagePath)
        {
            _storagePath = storagePath;
            _cache = new Dictionary<string, List<CoordinationReport>>();
        }

        public async Task<CoordinationReport> GetLatestReportAsync(string modelId)
        {
            await Task.Delay(10);

            if (_cache.TryGetValue(modelId, out var reports) && reports.Any())
            {
                return reports.OrderByDescending(r => r.AnalyzedAt).First();
            }

            return null;
        }

        public async Task SaveReportAsync(string modelId, CoordinationReport report)
        {
            await Task.Delay(10);

            if (!_cache.ContainsKey(modelId))
            {
                _cache[modelId] = new List<CoordinationReport>();
            }

            _cache[modelId].Add(report);
        }

        public async Task<List<CoordinationReport>> GetReportHistoryAsync(
            string modelId,
            DateTime startDate,
            DateTime endDate)
        {
            await Task.Delay(10);

            if (_cache.TryGetValue(modelId, out var reports))
            {
                return reports
                    .Where(r => r.AnalyzedAt >= startDate && r.AnalyzedAt <= endDate)
                    .OrderBy(r => r.AnalyzedAt)
                    .ToList();
            }

            return new List<CoordinationReport>();
        }
    }

    public class ClashReportGenerator
    {
        public byte[] GenerateHtmlReport(CoordinationReport report, CoordinationDelta delta)
        {
            var html = GenerateHtmlContent(report, delta);
            return System.Text.Encoding.UTF8.GetBytes(html);
        }

        public byte[] GeneratePdfReport(CoordinationReport report, CoordinationDelta delta)
        {
            // In real implementation, would use PDF library
            return System.Text.Encoding.UTF8.GetBytes("PDF content placeholder");
        }

        public byte[] GenerateExcelReport(CoordinationReport report, CoordinationDelta delta)
        {
            // In real implementation, would use Excel library
            return System.Text.Encoding.UTF8.GetBytes("Excel content placeholder");
        }

        public byte[] GenerateBcfReport(CoordinationReport report)
        {
            // BCF (BIM Collaboration Format) for clash issues
            // In real implementation, would generate proper BCF XML
            return System.Text.Encoding.UTF8.GetBytes("BCF content placeholder");
        }

        public byte[] GenerateJsonReport(CoordinationReport report, CoordinationDelta delta)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                report.ModelId,
                report.AnalyzedAt,
                ClashCount = report.ClashResults.Count,
                Summary = report.ClashSummary,
                Hotspots = report.Hotspots,
                Delta = delta != null ? new
                {
                    delta.NewClashes.Count,
                    ResolvedCount = delta.ResolvedClashes.Count,
                    delta.NetChange
                } : null
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private string GenerateHtmlContent(CoordinationReport report, CoordinationDelta delta)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<title>Clash Detection Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #4CAF50; color: white; }");
            sb.AppendLine(".critical { color: red; font-weight: bold; }");
            sb.AppendLine(".major { color: orange; }");
            sb.AppendLine(".minor { color: #999; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine($"<h1>Clash Detection Report</h1>");
            sb.AppendLine($"<p><strong>Model:</strong> {report.ModelId}</p>");
            sb.AppendLine($"<p><strong>Analysis Date:</strong> {report.AnalyzedAt:yyyy-MM-dd HH:mm}</p>");

            // Summary
            sb.AppendLine("<h2>Summary</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Total Clashes</th><th>Critical</th><th>Major</th><th>Minor</th><th>Warning</th></tr>");
            sb.AppendLine($"<tr>");
            sb.AppendLine($"<td>{report.ClashSummary?.TotalClashes ?? 0}</td>");
            sb.AppendLine($"<td class='critical'>{report.ClashSummary?.BySeverity.GetValueOrDefault(ClashSeverity.Critical, 0)}</td>");
            sb.AppendLine($"<td class='major'>{report.ClashSummary?.BySeverity.GetValueOrDefault(ClashSeverity.Major, 0)}</td>");
            sb.AppendLine($"<td class='minor'>{report.ClashSummary?.BySeverity.GetValueOrDefault(ClashSeverity.Minor, 0)}</td>");
            sb.AppendLine($"<td>{report.ClashSummary?.BySeverity.GetValueOrDefault(ClashSeverity.Warning, 0)}</td>");
            sb.AppendLine("</tr></table>");

            // Delta if available
            if (delta != null)
            {
                sb.AppendLine("<h2>Changes Since Last Report</h2>");
                sb.AppendLine($"<p>New clashes: {delta.NewClashes.Count}</p>");
                sb.AppendLine($"<p>Resolved clashes: {delta.ResolvedClashes.Count}</p>");
                sb.AppendLine($"<p>Net change: {(delta.NetChange >= 0 ? "+" : "")}{delta.NetChange}</p>");
            }

            // Hotspots
            if (report.Hotspots?.Any() == true)
            {
                sb.AppendLine("<h2>Coordination Hotspots</h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Region</th><th>Clashes</th><th>Severity</th><th>Recommendation</th></tr>");
                foreach (var hotspot in report.Hotspots.Take(10))
                {
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"<td>{hotspot.Region}</td>");
                    sb.AppendLine($"<td>{hotspot.ClashCount}</td>");
                    sb.AppendLine($"<td>{hotspot.Severity}</td>");
                    sb.AppendLine($"<td>{hotspot.RecommendedAction}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }

    public class ClashNotificationService
    {
        private readonly WorkflowConfiguration _config;

        public ClashNotificationService(WorkflowConfiguration config)
        {
            _config = config;
        }

        public async Task SendCriticalClashAlertAsync(
            List<Clash> criticalClashes,
            List<string> recipients)
        {
            await Task.Delay(10);
            // In real implementation, would send email/teams notification
        }

        public async Task SendNewClashesNotificationAsync(
            List<Clash> newClashes,
            List<string> recipients)
        {
            await Task.Delay(10);
        }

        public async Task SendSummaryAsync(
            CoordinationReport report,
            CoordinationDelta delta,
            List<string> recipients)
        {
            await Task.Delay(10);
        }
    }

    #endregion

    #region Data Models

    public class WorkflowConfiguration
    {
        public string HistoryStoragePath { get; set; } = "./clash_history";
        public string SmtpServer { get; set; }
        public string TeamsWebhookUrl { get; set; }
        public bool EnableEmailNotifications { get; set; }
        public bool EnableTeamsNotifications { get; set; }

        public static WorkflowConfiguration Default => new WorkflowConfiguration();
    }

    public class WorkflowOptions
    {
        public CoordinationOptions CoordinationOptions { get; set; } = CoordinationOptions.Default;
        public AutoResolutionOptions AutoResolutionOptions { get; set; } = AutoResolutionOptions.Default;
        public bool EnableAutoResolution { get; set; } = false;
        public bool EnableNotifications { get; set; } = true;
        public bool SendDailySummary { get; set; } = false;
        public List<string> NotificationRecipients { get; set; } = new List<string>();
        public List<ReportFormat> ReportFormats { get; set; } = new List<ReportFormat>
        {
            ReportFormat.HTML,
            ReportFormat.JSON
        };

        public static WorkflowOptions Default => new WorkflowOptions();
    }

    public class WorkflowResult
    {
        public string WorkflowId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public WorkflowStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Steps { get; set; } = new List<string>();

        public CoordinationReport CoordinationReport { get; set; }
        public CoordinationDelta Delta { get; set; }
        public AutoResolutionResult AutoResolutionResult { get; set; }
        public List<GeneratedReport> GeneratedReports { get; set; } = new List<GeneratedReport>();
        public List<ActionItem> ActionItems { get; set; } = new List<ActionItem>();

        public TimeSpan Duration => EndTime - StartTime;

        public void AddStep(string step)
        {
            Steps.Add($"[{DateTime.UtcNow:HH:mm:ss}] {step}");
        }
    }

    public class DisciplinePair
    {
        public Discipline Discipline1 { get; set; }
        public Discipline Discipline2 { get; set; }

        public DisciplinePair(Discipline d1, Discipline d2)
        {
            Discipline1 = d1;
            Discipline2 = d2;
        }
    }

    public class GeneratedReport
    {
        public ReportFormat Format { get; set; }
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public DateTime GeneratedAt { get; set; }
        public long FileSize { get; set; }
    }

    public class ActionItem
    {
        public ActionPriority Priority { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Discipline AssignedDiscipline { get; set; }
        public DateTime DueDate { get; set; }
        public List<string> RelatedClashIds { get; set; } = new List<string>();
        public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;
    }

    public class ClashTrendAnalysis
    {
        public string ModelId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public double ChangePercentage { get; set; }
    }

    public class TrendDataPoint
    {
        public DateTime Date { get; set; }
        public int TotalClashes { get; set; }
        public int CriticalClashes { get; set; }
        public int MajorClashes { get; set; }
        public int MinorClashes { get; set; }
        public int WarningClashes { get; set; }
    }

    public class ResolutionStatistics
    {
        public string ModelId { get; set; }
        public string Period { get; set; }
        public int TotalClashesDetected { get; set; }
        public int ClashesResolved { get; set; }
        public int ClashesRemaining { get; set; }
        public double ResolutionRate { get; set; }
        public TimeSpan AverageResolutionTime { get; set; }
        public Dictionary<ClashSeverity, int> ResolutionBySeverity { get; set; }
    }

    public class ClashLifecycle
    {
        public string ClashId { get; set; }
        public DateTime FirstDetected { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public ClashSeverity Severity { get; set; }
    }

    public enum WorkflowStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public enum ReportFormat
    {
        HTML,
        PDF,
        Excel,
        BCF,
        JSON
    }

    public enum ActionPriority
    {
        Critical = 1,
        High = 2,
        Medium = 3,
        Low = 4
    }

    public enum ActionItemStatus
    {
        Open,
        InProgress,
        Resolved,
        Deferred
    }

    public enum TrendDirection
    {
        Improving,
        Stable,
        Worsening
    }

    #endregion
}
