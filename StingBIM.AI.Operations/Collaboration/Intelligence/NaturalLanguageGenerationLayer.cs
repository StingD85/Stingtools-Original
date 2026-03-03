// ================================================================================================
// STINGBIM AI COLLABORATION - NATURAL LANGUAGE GENERATION LAYER
// Advanced NLG system for generating reports, summaries, notifications, and documentation
// ================================================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Enums

    public enum DocumentType
    {
        DailyReport,
        WeeklyReport,
        MonthlyReport,
        ProgressReport,
        SafetyReport,
        QualityReport,
        ClashReport,
        RFIResponse,
        SubmittalCover,
        MeetingMinutes,
        ChangeOrderNarrative,
        PunchListSummary,
        CloseoutDocument,
        ExecutiveSummary,
        TechnicalNarrative,
        IncidentReport,
        InspectionReport,
        ComplianceReport,
        CostReport,
        ScheduleNarrative
    }

    public enum ToneStyle
    {
        Formal,
        Professional,
        Technical,
        Conversational,
        Executive,
        Legal,
        Marketing,
        Educational
    }

    public enum AudienceLevel
    {
        Executive,
        ProjectManager,
        FieldSupervisor,
        Technician,
        Client,
        Regulator,
        Public,
        Internal
    }

    public enum ContentLength
    {
        Brief,          // 1-2 sentences
        Short,          // 1 paragraph
        Medium,         // 2-3 paragraphs
        Long,           // Full page
        Comprehensive   // Multi-page
    }

    public enum NotificationType
    {
        Alert,
        Warning,
        Information,
        ActionRequired,
        Reminder,
        Escalation,
        Approval,
        Completion,
        Delay,
        Issue,
        Milestone,
        Update
    }

    #endregion

    #region Data Models

    public class GenerationRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public DocumentType DocumentType { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public ToneStyle Tone { get; set; } = ToneStyle.Professional;
        public AudienceLevel Audience { get; set; } = AudienceLevel.ProjectManager;
        public ContentLength Length { get; set; } = ContentLength.Medium;
        public Dictionary<string, object> Context { get; set; } = new();
        public List<string> IncludeSections { get; set; } = new();
        public List<string> ExcludeSections { get; set; } = new();
        public string? CustomInstructions { get; set; }
        public string? TemplateId { get; set; }
        public string Language { get; set; } = "en-US";
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class GeneratedDocument
    {
        public string DocumentId { get; set; } = Guid.NewGuid().ToString();
        public string RequestId { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<DocumentSection> Sections { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public double ReadabilityScore { get; set; }
        public string ReadabilityGrade { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan GenerationTime { get; set; }
    }

    public class TableData
    {
        public string TableId { get; set; } = Guid.NewGuid().ToString();
        public string Caption { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    public class NotificationContent
    {
        public string NotificationId { get; set; } = Guid.NewGuid().ToString();
        public NotificationType Type { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string ShortMessage { get; set; } = string.Empty;
        public string FullMessage { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public Priority Priority { get; set; }
        public List<string> Recipients { get; set; } = new();
        public Dictionary<string, string> Placeholders { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public enum Priority
    {
        Low,
        Normal,
        High,
        Urgent,
        Critical
    }

    public class SummaryRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string SourceContent { get; set; } = string.Empty;
        public ContentLength TargetLength { get; set; } = ContentLength.Short;
        public AudienceLevel Audience { get; set; } = AudienceLevel.Executive;
        public List<string> FocusAreas { get; set; } = new();
        public bool IncludeKeyMetrics { get; set; } = true;
        public bool IncludeRecommendations { get; set; } = true;
    }

    public class GeneratedSummary
    {
        public string SummaryId { get; set; } = Guid.NewGuid().ToString();
        public string RequestId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
        public List<KeyMetric> Metrics { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> ActionItems { get; set; } = new();
        public double CompressionRatio { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class KeyMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty; // up, down, stable
        public string Context { get; set; } = string.Empty;
    }

    public class DocumentTemplate
    {
        public string TemplateId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<TemplateSection> Sections { get; set; } = new();
        public Dictionary<string, string> DefaultValues { get; set; } = new();
        public string HeaderTemplate { get; set; } = string.Empty;
        public string FooterTemplate { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TemplateSection
    {
        public string SectionId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string ContentTemplate { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool IsConditional { get; set; }
        public string? Condition { get; set; }
        public List<string> Placeholders { get; set; } = new();
    }

    public class NarrativeContext
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
        public string PreparedBy { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public WeatherConditions? Weather { get; set; }
        public ScheduleStatus? Schedule { get; set; }
        public BudgetStatus? Budget { get; set; }
        public SafetyStatus? Safety { get; set; }
        public QualityStatus? Quality { get; set; }
        public List<WorkActivity> Activities { get; set; } = new();
        public List<IssueItem> Issues { get; set; } = new();
        public List<string> Accomplishments { get; set; } = new();
        public List<string> PlannedWork { get; set; } = new();
        public Dictionary<string, object> CustomData { get; set; } = new();
    }

    public class WeatherConditions
    {
        public string Description { get; set; } = string.Empty;
        public double TemperatureHigh { get; set; }
        public double TemperatureLow { get; set; }
        public string TemperatureUnit { get; set; } = "°F";
        public int HumidityPercent { get; set; }
        public string WindSpeed { get; set; } = string.Empty;
        public string Precipitation { get; set; } = string.Empty;
        public bool WorkImpacted { get; set; }
        public string ImpactDescription { get; set; } = string.Empty;
    }

    public class ScheduleStatus
    {
        public double PercentComplete { get; set; }
        public int DaysAheadBehind { get; set; } // Negative = behind
        public DateTime PlannedCompletion { get; set; }
        public DateTime ForecastCompletion { get; set; }
        public List<string> CriticalPathActivities { get; set; } = new();
        public List<string> Milestones { get; set; } = new();
    }

    public class BudgetStatus
    {
        public decimal OriginalBudget { get; set; }
        public decimal CurrentBudget { get; set; }
        public decimal SpentToDate { get; set; }
        public decimal CommittedCosts { get; set; }
        public decimal ForecastAtCompletion { get; set; }
        public decimal Variance { get; set; }
        public double PercentSpent { get; set; }
    }

    public class SafetyStatus
    {
        public int DaysWithoutIncident { get; set; }
        public int IncidentsThisPeriod { get; set; }
        public int NearMissesThisPeriod { get; set; }
        public int SafetyObservations { get; set; }
        public double SafetyScore { get; set; }
        public List<string> SafetyTopics { get; set; } = new();
    }


    public class WorkActivity
    {
        public string ActivityId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double PercentComplete { get; set; }
        public int WorkersOnSite { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class IssueItem
    {
        public string IssueId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string Resolution { get; set; } = string.Empty;
    }

    #endregion

    #region Phrase Libraries

    public static class PhraseLibrary
    {
        public static readonly Dictionary<string, List<string>> Transitions = new()
        {
            ["addition"] = new() { "Furthermore", "Additionally", "Moreover", "In addition", "Also" },
            ["contrast"] = new() { "However", "Nevertheless", "On the other hand", "Conversely", "Despite this" },
            ["cause"] = new() { "As a result", "Consequently", "Therefore", "Thus", "Accordingly" },
            ["time"] = new() { "Subsequently", "Meanwhile", "Previously", "Following this", "Currently" },
            ["emphasis"] = new() { "Notably", "Significantly", "Importantly", "In particular", "Especially" },
            ["conclusion"] = new() { "In conclusion", "To summarize", "Overall", "In summary", "Finally" }
        };

        public static readonly Dictionary<string, List<string>> StatusDescriptions = new()
        {
            ["on_track"] = new()
            {
                "The project is progressing on schedule",
                "Work continues to meet planned milestones",
                "Progress remains aligned with the baseline schedule",
                "Activities are advancing as planned"
            },
            ["ahead"] = new()
            {
                "The project is ahead of schedule",
                "Work is progressing faster than anticipated",
                "The team has achieved early completion of several activities",
                "Schedule performance is exceeding expectations"
            },
            ["behind"] = new()
            {
                "The project is experiencing schedule delays",
                "Progress has fallen behind the baseline schedule",
                "Recovery efforts are underway to address schedule variance",
                "Additional resources may be required to recover lost time"
            },
            ["under_budget"] = new()
            {
                "The project is currently under budget",
                "Cost performance is favorable",
                "Expenditures remain below projections",
                "The team has achieved cost savings"
            },
            ["over_budget"] = new()
            {
                "The project is experiencing cost overruns",
                "Expenditures have exceeded the approved budget",
                "Cost containment measures are being implemented",
                "A change order may be required to address budget variance"
            }
        };

        public static readonly Dictionary<string, List<string>> SafetyPhrases = new()
        {
            ["positive"] = new()
            {
                "Safety performance remains exemplary",
                "The team continues to demonstrate a strong commitment to safety",
                "No safety incidents were reported during this period",
                "Safety protocols are being followed diligently"
            },
            ["concern"] = new()
            {
                "Safety observations require immediate attention",
                "Enhanced safety measures have been implemented",
                "Additional safety training has been scheduled",
                "Close monitoring of safety compliance continues"
            },
            ["incident"] = new()
            {
                "A safety incident occurred and has been thoroughly investigated",
                "Corrective actions have been implemented following the incident",
                "Root cause analysis has been completed",
                "Lessons learned have been shared with the team"
            }
        };

        public static readonly Dictionary<string, List<string>> QualityPhrases = new()
        {
            ["excellent"] = new()
            {
                "Quality standards are being exceeded",
                "Workmanship quality is exemplary",
                "All inspections have been passed without deficiencies",
                "The quality program is performing exceptionally"
            },
            ["acceptable"] = new()
            {
                "Quality requirements are being met",
                "Work is meeting specification requirements",
                "Minor deficiencies are being addressed promptly",
                "Quality control processes are functioning effectively"
            },
            ["concern"] = new()
            {
                "Quality issues have been identified and are being addressed",
                "Corrective work is required in several areas",
                "Enhanced quality oversight has been implemented",
                "Rework is being performed to meet specifications"
            }
        };

        public static readonly Dictionary<ToneStyle, Dictionary<string, string>> ToneModifiers = new()
        {
            [ToneStyle.Formal] = new()
            {
                ["good"] = "satisfactory",
                ["bad"] = "unsatisfactory",
                ["problem"] = "issue requiring attention",
                ["fixed"] = "remediated",
                ["done"] = "completed",
                ["start"] = "commence",
                ["end"] = "conclude",
                ["check"] = "verify",
                ["help"] = "assist",
                ["need"] = "require"
            },
            [ToneStyle.Technical] = new()
            {
                ["good"] = "within specifications",
                ["bad"] = "non-conforming",
                ["problem"] = "deficiency",
                ["fixed"] = "corrected",
                ["done"] = "executed",
                ["start"] = "initiate",
                ["end"] = "terminate",
                ["check"] = "inspect",
                ["help"] = "support",
                ["need"] = "necessitate"
            },
            [ToneStyle.Executive] = new()
            {
                ["good"] = "positive",
                ["bad"] = "concerning",
                ["problem"] = "challenge",
                ["fixed"] = "resolved",
                ["done"] = "achieved",
                ["start"] = "launch",
                ["end"] = "finalize",
                ["check"] = "review",
                ["help"] = "facilitate",
                ["need"] = "demand"
            }
        };

        public static string GetRandomPhrase(string category, string key)
        {
            if (Transitions.TryGetValue(key, out var transitions))
            {
                return transitions[new Random().Next(transitions.Count)];
            }

            return category switch
            {
                "status" => StatusDescriptions.TryGetValue(key, out var status)
                    ? status[new Random().Next(status.Count)] : key,
                "safety" => SafetyPhrases.TryGetValue(key, out var safety)
                    ? safety[new Random().Next(safety.Count)] : key,
                "quality" => QualityPhrases.TryGetValue(key, out var quality)
                    ? quality[new Random().Next(quality.Count)] : key,
                _ => key
            };
        }
    }

    #endregion

    /// <summary>
    /// Natural Language Generation Layer for creating human-readable documents,
    /// reports, summaries, and notifications from structured data
    /// </summary>
    public class NaturalLanguageGenerationLayer : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, DocumentTemplate> _templates = new();
        private readonly ConcurrentDictionary<string, GeneratedDocument> _documentCache = new();
        private readonly ConcurrentDictionary<string, int> _usageStats = new();
        private readonly SemaphoreSlim _generationSemaphore = new(10);
        private readonly Random _random = new();
        private bool _disposed;

        public NaturalLanguageGenerationLayer()
        {
            InitializeDefaultTemplates();
        }

        #region Initialization

        private void InitializeDefaultTemplates()
        {
            // Daily Report Template
            _templates["daily_report"] = new DocumentTemplate
            {
                TemplateId = "daily_report",
                Name = "Daily Construction Report",
                Type = DocumentType.DailyReport,
                Description = "Standard daily progress report for construction projects",
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "header", Title = "Report Header", Order = 1, IsRequired = true },
                    new() { SectionId = "weather", Title = "Weather Conditions", Order = 2, IsRequired = true },
                    new() { SectionId = "manpower", Title = "Manpower", Order = 3, IsRequired = true },
                    new() { SectionId = "work_performed", Title = "Work Performed", Order = 4, IsRequired = true },
                    new() { SectionId = "issues", Title = "Issues and Delays", Order = 5, IsConditional = true },
                    new() { SectionId = "safety", Title = "Safety", Order = 6, IsRequired = true },
                    new() { SectionId = "materials", Title = "Materials Received", Order = 7, IsConditional = true },
                    new() { SectionId = "visitors", Title = "Visitors", Order = 8, IsConditional = true },
                    new() { SectionId = "photos", Title = "Photo Documentation", Order = 9, IsConditional = true },
                    new() { SectionId = "planned_work", Title = "Planned Work for Tomorrow", Order = 10, IsRequired = true }
                }
            };

            // Weekly Report Template
            _templates["weekly_report"] = new DocumentTemplate
            {
                TemplateId = "weekly_report",
                Name = "Weekly Progress Report",
                Type = DocumentType.WeeklyReport,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "executive_summary", Title = "Executive Summary", Order = 1, IsRequired = true },
                    new() { SectionId = "schedule_status", Title = "Schedule Status", Order = 2, IsRequired = true },
                    new() { SectionId = "budget_status", Title = "Budget Status", Order = 3, IsRequired = true },
                    new() { SectionId = "accomplishments", Title = "Key Accomplishments", Order = 4, IsRequired = true },
                    new() { SectionId = "issues", Title = "Issues and Risks", Order = 5, IsRequired = true },
                    new() { SectionId = "lookahead", Title = "Two-Week Lookahead", Order = 6, IsRequired = true },
                    new() { SectionId = "safety", Title = "Safety Performance", Order = 7, IsRequired = true },
                    new() { SectionId = "quality", Title = "Quality Status", Order = 8, IsRequired = true }
                }
            };

            // Executive Summary Template
            _templates["executive_summary"] = new DocumentTemplate
            {
                TemplateId = "executive_summary",
                Name = "Executive Summary",
                Type = DocumentType.ExecutiveSummary,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "overview", Title = "Project Overview", Order = 1, IsRequired = true },
                    new() { SectionId = "key_metrics", Title = "Key Performance Indicators", Order = 2, IsRequired = true },
                    new() { SectionId = "highlights", Title = "Period Highlights", Order = 3, IsRequired = true },
                    new() { SectionId = "concerns", Title = "Areas of Concern", Order = 4, IsConditional = true },
                    new() { SectionId = "recommendations", Title = "Recommendations", Order = 5, IsRequired = true }
                }
            };

            // Clash Report Template
            _templates["clash_report"] = new DocumentTemplate
            {
                TemplateId = "clash_report",
                Name = "Clash Detection Report",
                Type = DocumentType.ClashReport,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "summary", Title = "Summary", Order = 1, IsRequired = true },
                    new() { SectionId = "statistics", Title = "Clash Statistics", Order = 2, IsRequired = true },
                    new() { SectionId = "critical", Title = "Critical Clashes", Order = 3, IsRequired = true },
                    new() { SectionId = "by_discipline", Title = "Clashes by Discipline", Order = 4, IsRequired = true },
                    new() { SectionId = "resolved", Title = "Resolved Clashes", Order = 5, IsConditional = true },
                    new() { SectionId = "recommendations", Title = "Recommended Actions", Order = 6, IsRequired = true }
                }
            };

            // RFI Response Template
            _templates["rfi_response"] = new DocumentTemplate
            {
                TemplateId = "rfi_response",
                Name = "RFI Response",
                Type = DocumentType.RFIResponse,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "question", Title = "Question", Order = 1, IsRequired = true },
                    new() { SectionId = "response", Title = "Response", Order = 2, IsRequired = true },
                    new() { SectionId = "impact", Title = "Cost/Schedule Impact", Order = 3, IsConditional = true },
                    new() { SectionId = "attachments", Title = "Attachments", Order = 4, IsConditional = true }
                }
            };

            // Safety Report Template
            _templates["safety_report"] = new DocumentTemplate
            {
                TemplateId = "safety_report",
                Name = "Safety Report",
                Type = DocumentType.SafetyReport,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "summary", Title = "Safety Summary", Order = 1, IsRequired = true },
                    new() { SectionId = "statistics", Title = "Safety Statistics", Order = 2, IsRequired = true },
                    new() { SectionId = "incidents", Title = "Incidents", Order = 3, IsConditional = true },
                    new() { SectionId = "observations", Title = "Safety Observations", Order = 4, IsRequired = true },
                    new() { SectionId = "training", Title = "Training Conducted", Order = 5, IsRequired = true },
                    new() { SectionId = "actions", Title = "Corrective Actions", Order = 6, IsConditional = true }
                }
            };

            // Incident Report Template
            _templates["incident_report"] = new DocumentTemplate
            {
                TemplateId = "incident_report",
                Name = "Incident Report",
                Type = DocumentType.IncidentReport,
                Sections = new List<TemplateSection>
                {
                    new() { SectionId = "summary", Title = "Incident Summary", Order = 1, IsRequired = true },
                    new() { SectionId = "details", Title = "Incident Details", Order = 2, IsRequired = true },
                    new() { SectionId = "involved", Title = "Persons Involved", Order = 3, IsRequired = true },
                    new() { SectionId = "witnesses", Title = "Witnesses", Order = 4, IsConditional = true },
                    new() { SectionId = "immediate_actions", Title = "Immediate Actions Taken", Order = 5, IsRequired = true },
                    new() { SectionId = "root_cause", Title = "Root Cause Analysis", Order = 6, IsRequired = true },
                    new() { SectionId = "corrective_actions", Title = "Corrective Actions", Order = 7, IsRequired = true },
                    new() { SectionId = "prevention", Title = "Prevention Measures", Order = 8, IsRequired = true }
                }
            };
        }

        #endregion

        #region Document Generation

        /// <summary>
        /// Generate a document based on the request parameters
        /// </summary>
        public async Task<GeneratedDocument> GenerateDocumentAsync(
            GenerationRequest request,
            NarrativeContext context,
            CancellationToken ct = default)
        {
            await _generationSemaphore.WaitAsync(ct);
            try
            {
                var startTime = DateTime.UtcNow;

                var document = request.DocumentType switch
                {
                    DocumentType.DailyReport => GenerateDailyReport(request, context),
                    DocumentType.WeeklyReport => GenerateWeeklyReport(request, context),
                    DocumentType.MonthlyReport => GenerateMonthlyReport(request, context),
                    DocumentType.ExecutiveSummary => GenerateExecutiveSummary(request, context),
                    DocumentType.SafetyReport => GenerateSafetyReport(request, context),
                    DocumentType.QualityReport => GenerateQualityReport(request, context),
                    DocumentType.ClashReport => GenerateClashReport(request, context),
                    DocumentType.ProgressReport => GenerateProgressReport(request, context),
                    DocumentType.CostReport => GenerateCostReport(request, context),
                    DocumentType.ScheduleNarrative => GenerateScheduleNarrative(request, context),
                    DocumentType.IncidentReport => GenerateIncidentReport(request, context),
                    _ => GenerateGenericDocument(request, context)
                };

                document.RequestId = request.RequestId;
                document.GenerationTime = DateTime.UtcNow - startTime;
                document.WordCount = CountWords(document.Content);
                document.CharacterCount = document.Content.Length;
                CalculateReadability(document);

                _documentCache[document.DocumentId] = document;
                IncrementUsage(request.DocumentType.ToString());

                return document;
            }
            finally
            {
                _generationSemaphore.Release();
            }
        }

        private GeneratedDocument GenerateDailyReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.DailyReport,
                Title = $"Daily Construction Report - {context.ProjectName}",
                Metadata = new Dictionary<string, string>
                {
                    ["project_number"] = context.ProjectNumber,
                    ["report_date"] = context.ReportDate.ToString("MMMM d, yyyy"),
                    ["prepared_by"] = context.PreparedBy
                }
            };

            var content = new StringBuilder();
            var sections = new List<DocumentSection>();

            // Header Section
            content.AppendLine($"# Daily Construction Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Project Number:** {context.ProjectNumber}");
            content.AppendLine($"**Date:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine($"**Prepared By:** {context.PreparedBy}");
            content.AppendLine();

            // Weather Section
            if (context.Weather != null)
            {
                var weatherSection = new DocumentSection
                {
                    Title = "Weather Conditions",
                    Order = 1,
                    Content = GenerateWeatherNarrative(context.Weather, request.Tone)
                };
                sections.Add(weatherSection);
                content.AppendLine("## Weather Conditions");
                content.AppendLine(weatherSection.Content);
                content.AppendLine();
            }

            // Work Performed Section
            if (context.Activities.Any())
            {
                var workSection = new DocumentSection
                {
                    Title = "Work Performed",
                    Order = 2,
                    Content = GenerateWorkPerformedNarrative(context.Activities, request.Tone)
                };
                sections.Add(workSection);
                content.AppendLine("## Work Performed");
                content.AppendLine(workSection.Content);
                content.AppendLine();
            }

            // Manpower Section
            var totalWorkers = context.Activities.Sum(a => a.WorkersOnSite);
            if (totalWorkers > 0)
            {
                var manpowerSection = new DocumentSection
                {
                    Title = "Manpower",
                    Order = 3,
                    Content = GenerateManpowerNarrative(context.Activities, request.Tone)
                };
                sections.Add(manpowerSection);
                content.AppendLine("## Manpower");
                content.AppendLine(manpowerSection.Content);
                content.AppendLine();
            }

            // Issues Section
            if (context.Issues.Any())
            {
                var issuesSection = new DocumentSection
                {
                    Title = "Issues and Delays",
                    Order = 4,
                    Content = GenerateIssuesNarrative(context.Issues, request.Tone)
                };
                sections.Add(issuesSection);
                content.AppendLine("## Issues and Delays");
                content.AppendLine(issuesSection.Content);
                content.AppendLine();
            }

            // Safety Section
            if (context.Safety != null)
            {
                var safetySection = new DocumentSection
                {
                    Title = "Safety",
                    Order = 5,
                    Content = GenerateSafetyNarrative(context.Safety, request.Tone)
                };
                sections.Add(safetySection);
                content.AppendLine("## Safety");
                content.AppendLine(safetySection.Content);
                content.AppendLine();
            }

            // Planned Work Section
            if (context.PlannedWork.Any())
            {
                var plannedSection = new DocumentSection
                {
                    Title = "Planned Work for Tomorrow",
                    Order = 6,
                    BulletPoints = context.PlannedWork
                };
                sections.Add(plannedSection);
                content.AppendLine("## Planned Work for Tomorrow");
                foreach (var item in context.PlannedWork)
                {
                    content.AppendLine($"- {item}");
                }
                content.AppendLine();
            }

            doc.Sections = sections;
            doc.Content = content.ToString();
            doc.Summary = GenerateDailySummary(context, request.Tone);

            return doc;
        }

        private GeneratedDocument GenerateWeeklyReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.WeeklyReport,
                Title = $"Weekly Progress Report - {context.ProjectName}",
                Metadata = new Dictionary<string, string>
                {
                    ["project_number"] = context.ProjectNumber,
                    ["week_ending"] = context.ReportDate.ToString("MMMM d, yyyy"),
                    ["prepared_by"] = context.PreparedBy
                }
            };

            var content = new StringBuilder();
            var sections = new List<DocumentSection>();

            content.AppendLine($"# Weekly Progress Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Week Ending:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine($"**Client:** {context.ClientName}");
            content.AppendLine();

            // Executive Summary
            var execSummary = GenerateExecutiveSummaryNarrative(context, request.Tone, request.Audience);
            var execSection = new DocumentSection
            {
                Title = "Executive Summary",
                Order = 1,
                Content = execSummary
            };
            sections.Add(execSection);
            content.AppendLine("## Executive Summary");
            content.AppendLine(execSummary);
            content.AppendLine();

            // Schedule Status
            if (context.Schedule != null)
            {
                var scheduleNarrative = GenerateScheduleStatusNarrative(context.Schedule, request.Tone);
                var scheduleSection = new DocumentSection
                {
                    Title = "Schedule Status",
                    Order = 2,
                    Content = scheduleNarrative
                };
                sections.Add(scheduleSection);
                content.AppendLine("## Schedule Status");
                content.AppendLine(scheduleNarrative);
                content.AppendLine();
            }

            // Budget Status
            if (context.Budget != null)
            {
                var budgetNarrative = GenerateBudgetStatusNarrative(context.Budget, request.Tone);
                var budgetSection = new DocumentSection
                {
                    Title = "Budget Status",
                    Order = 3,
                    Content = budgetNarrative
                };
                sections.Add(budgetSection);
                content.AppendLine("## Budget Status");
                content.AppendLine(budgetNarrative);
                content.AppendLine();
            }

            // Key Accomplishments
            if (context.Accomplishments.Any())
            {
                var accomplishSection = new DocumentSection
                {
                    Title = "Key Accomplishments",
                    Order = 4,
                    BulletPoints = context.Accomplishments
                };
                sections.Add(accomplishSection);
                content.AppendLine("## Key Accomplishments");
                foreach (var item in context.Accomplishments)
                {
                    content.AppendLine($"- {item}");
                }
                content.AppendLine();
            }

            // Issues and Risks
            if (context.Issues.Any())
            {
                var issuesNarrative = GenerateIssuesNarrative(context.Issues, request.Tone);
                var issuesSection = new DocumentSection
                {
                    Title = "Issues and Risks",
                    Order = 5,
                    Content = issuesNarrative
                };
                sections.Add(issuesSection);
                content.AppendLine("## Issues and Risks");
                content.AppendLine(issuesNarrative);
                content.AppendLine();
            }

            // Safety Performance
            if (context.Safety != null)
            {
                var safetyNarrative = GenerateSafetyNarrative(context.Safety, request.Tone);
                var safetySection = new DocumentSection
                {
                    Title = "Safety Performance",
                    Order = 6,
                    Content = safetyNarrative
                };
                sections.Add(safetySection);
                content.AppendLine("## Safety Performance");
                content.AppendLine(safetyNarrative);
                content.AppendLine();
            }

            // Quality Status
            if (context.Quality != null)
            {
                var qualityNarrative = GenerateQualityNarrative(context.Quality, request.Tone);
                var qualitySection = new DocumentSection
                {
                    Title = "Quality Status",
                    Order = 7,
                    Content = qualityNarrative
                };
                sections.Add(qualitySection);
                content.AppendLine("## Quality Status");
                content.AppendLine(qualityNarrative);
                content.AppendLine();
            }

            // Lookahead
            if (context.PlannedWork.Any())
            {
                var lookaheadSection = new DocumentSection
                {
                    Title = "Two-Week Lookahead",
                    Order = 8,
                    BulletPoints = context.PlannedWork
                };
                sections.Add(lookaheadSection);
                content.AppendLine("## Two-Week Lookahead");
                foreach (var item in context.PlannedWork)
                {
                    content.AppendLine($"- {item}");
                }
                content.AppendLine();
            }

            doc.Sections = sections;
            doc.Content = content.ToString();
            doc.Summary = execSummary;

            return doc;
        }

        private GeneratedDocument GenerateMonthlyReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.MonthlyReport,
                Title = $"Monthly Progress Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Monthly Progress Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Month:** {context.ReportDate:MMMM yyyy}");
            content.AppendLine();
            content.AppendLine("## Executive Overview");
            content.AppendLine(GenerateExecutiveSummaryNarrative(context, request.Tone, request.Audience));
            content.AppendLine();

            if (context.Schedule != null)
            {
                content.AppendLine("## Schedule Performance");
                content.AppendLine(GenerateScheduleStatusNarrative(context.Schedule, request.Tone));
                content.AppendLine();
            }

            if (context.Budget != null)
            {
                content.AppendLine("## Cost Performance");
                content.AppendLine(GenerateBudgetStatusNarrative(context.Budget, request.Tone));
                content.AppendLine();
            }

            content.AppendLine("## Key Milestones");
            content.AppendLine(GenerateMilestonesNarrative(context, request.Tone));
            content.AppendLine();

            content.AppendLine("## Risk Assessment");
            content.AppendLine(GenerateRiskAssessmentNarrative(context, request.Tone));
            content.AppendLine();

            doc.Content = content.ToString();
            doc.Summary = GenerateMonthlyExecutiveSummary(context, request.Tone);

            return doc;
        }

        private GeneratedDocument GenerateExecutiveSummary(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.ExecutiveSummary,
                Title = $"Executive Summary - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Executive Summary");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Date:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine();

            content.AppendLine("## Project Status at a Glance");
            content.AppendLine();

            // Generate status indicators
            if (context.Schedule != null)
            {
                var scheduleStatus = context.Schedule.DaysAheadBehind >= 0 ? "On Track" : "Behind Schedule";
                var scheduleIcon = context.Schedule.DaysAheadBehind >= 0 ? "✓" : "⚠";
                content.AppendLine($"- **Schedule:** {scheduleIcon} {scheduleStatus} ({Math.Abs(context.Schedule.DaysAheadBehind)} days {(context.Schedule.DaysAheadBehind >= 0 ? "ahead" : "behind")})");
            }

            if (context.Budget != null)
            {
                var budgetStatus = context.Budget.Variance >= 0 ? "Under Budget" : "Over Budget";
                var budgetIcon = context.Budget.Variance >= 0 ? "✓" : "⚠";
                content.AppendLine($"- **Budget:** {budgetIcon} {budgetStatus} ({Math.Abs(context.Budget.Variance):C0} variance)");
            }

            if (context.Safety != null)
            {
                var safetyIcon = context.Safety.IncidentsThisPeriod == 0 ? "✓" : "⚠";
                content.AppendLine($"- **Safety:** {safetyIcon} {context.Safety.DaysWithoutIncident} days without incident");
            }

            if (context.Quality != null)
            {
                var passRate = context.Quality.InspectionsPassed + context.Quality.InspectionsFailed > 0
                    ? (double)context.Quality.InspectionsPassed / (context.Quality.InspectionsPassed + context.Quality.InspectionsFailed) * 100
                    : 100;
                var qualityIcon = passRate >= 90 ? "✓" : "⚠";
                content.AppendLine($"- **Quality:** {qualityIcon} {passRate:F0}% inspection pass rate");
            }

            content.AppendLine();
            content.AppendLine("## Key Highlights");
            content.AppendLine();
            content.AppendLine(GenerateExecutiveSummaryNarrative(context, request.Tone, AudienceLevel.Executive));

            if (context.Issues.Any(i => i.Severity == "Critical" || i.Severity == "High"))
            {
                content.AppendLine();
                content.AppendLine("## Critical Attention Items");
                foreach (var issue in context.Issues.Where(i => i.Severity == "Critical" || i.Severity == "High"))
                {
                    content.AppendLine($"- **{issue.Title}**: {issue.Description}");
                }
            }

            doc.Content = content.ToString();
            doc.Summary = GenerateOneLinerSummary(context);

            return doc;
        }

        private GeneratedDocument GenerateSafetyReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.SafetyReport,
                Title = $"Safety Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Safety Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Period:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine();

            if (context.Safety != null)
            {
                content.AppendLine("## Safety Statistics");
                content.AppendLine();
                content.AppendLine($"| Metric | Value |");
                content.AppendLine($"|--------|-------|");
                content.AppendLine($"| Days Without Incident | {context.Safety.DaysWithoutIncident} |");
                content.AppendLine($"| Incidents This Period | {context.Safety.IncidentsThisPeriod} |");
                content.AppendLine($"| Near Misses | {context.Safety.NearMissesThisPeriod} |");
                content.AppendLine($"| Safety Observations | {context.Safety.SafetyObservations} |");
                content.AppendLine($"| Safety Score | {context.Safety.SafetyScore:F1}/100 |");
                content.AppendLine();

                content.AppendLine("## Safety Performance Analysis");
                content.AppendLine();
                content.AppendLine(GenerateSafetyNarrative(context.Safety, request.Tone));
                content.AppendLine();

                if (context.Safety.SafetyTopics.Any())
                {
                    content.AppendLine("## Safety Training Topics Covered");
                    foreach (var topic in context.Safety.SafetyTopics)
                    {
                        content.AppendLine($"- {topic}");
                    }
                    content.AppendLine();
                }
            }

            doc.Content = content.ToString();
            doc.Summary = context.Safety != null
                ? $"Safety performance: {context.Safety.DaysWithoutIncident} days without incident, {context.Safety.SafetyScore:F0}/100 safety score"
                : "Safety data not available";

            return doc;
        }

        private GeneratedDocument GenerateQualityReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.QualityReport,
                Title = $"Quality Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Quality Report");
            content.AppendLine();

            if (context.Quality != null)
            {
                content.AppendLine("## Quality Metrics");
                content.AppendLine();
                var totalInspections = context.Quality.InspectionsPassed + context.Quality.InspectionsFailed;
                var passRate = totalInspections > 0
                    ? (double)context.Quality.InspectionsPassed / totalInspections * 100
                    : 100;

                content.AppendLine($"| Metric | Value |");
                content.AppendLine($"|--------|-------|");
                content.AppendLine($"| Inspections Passed | {context.Quality.InspectionsPassed} |");
                content.AppendLine($"| Inspections Failed | {context.Quality.InspectionsFailed} |");
                content.AppendLine($"| Pass Rate | {passRate:F1}% |");
                content.AppendLine($"| Open Deficiencies | {context.Quality.OpenDeficiencies} |");
                content.AppendLine($"| Closed Deficiencies | {context.Quality.ClosedDeficiencies} |");
                content.AppendLine($"| Quality Score | {context.Quality.QualityScore:F1}/100 |");
                content.AppendLine();

                content.AppendLine("## Quality Analysis");
                content.AppendLine(GenerateQualityNarrative(context.Quality, request.Tone));
            }

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateClashReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.ClashReport,
                Title = $"Clash Detection Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Clash Detection Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Date:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine();

            // Get clash data from custom data if available
            if (context.CustomData.TryGetValue("clashes", out var clashData) && clashData is List<object> clashes)
            {
                content.AppendLine("## Summary");
                content.AppendLine();
                content.AppendLine($"This report documents the results of the clash detection analysis performed on the coordinated BIM model. ");
                content.AppendLine($"A total of {clashes.Count} clashes were identified and categorized by severity and discipline.");
                content.AppendLine();
            }
            else
            {
                content.AppendLine("## Summary");
                content.AppendLine();
                content.AppendLine("This report documents the results of the clash detection analysis. ");
                content.AppendLine("Detailed clash information should be provided in the context data.");
            }

            content.AppendLine("## Recommended Actions");
            content.AppendLine();
            content.AppendLine("1. Review all critical clashes with discipline leads");
            content.AppendLine("2. Schedule coordination meeting to resolve high-priority conflicts");
            content.AppendLine("3. Update models with approved resolutions");
            content.AppendLine("4. Re-run clash detection to verify resolutions");

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateProgressReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.ProgressReport,
                Title = $"Progress Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Progress Report");
            content.AppendLine();
            content.AppendLine(GenerateExecutiveSummaryNarrative(context, request.Tone, request.Audience));

            if (context.Schedule != null)
            {
                content.AppendLine();
                content.AppendLine("## Schedule Progress");
                content.AppendLine(GenerateScheduleStatusNarrative(context.Schedule, request.Tone));
            }

            if (context.Activities.Any())
            {
                content.AppendLine();
                content.AppendLine("## Work Progress");
                content.AppendLine(GenerateWorkPerformedNarrative(context.Activities, request.Tone));
            }

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateCostReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.CostReport,
                Title = $"Cost Report - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Cost Report");
            content.AppendLine();

            if (context.Budget != null)
            {
                content.AppendLine("## Budget Summary");
                content.AppendLine();
                content.AppendLine($"| Category | Amount |");
                content.AppendLine($"|----------|--------|");
                content.AppendLine($"| Original Budget | {context.Budget.OriginalBudget:C0} |");
                content.AppendLine($"| Current Budget | {context.Budget.CurrentBudget:C0} |");
                content.AppendLine($"| Spent to Date | {context.Budget.SpentToDate:C0} |");
                content.AppendLine($"| Committed Costs | {context.Budget.CommittedCosts:C0} |");
                content.AppendLine($"| Forecast at Completion | {context.Budget.ForecastAtCompletion:C0} |");
                content.AppendLine($"| Variance | {context.Budget.Variance:C0} |");
                content.AppendLine();

                content.AppendLine("## Analysis");
                content.AppendLine(GenerateBudgetStatusNarrative(context.Budget, request.Tone));
            }

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateScheduleNarrative(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.ScheduleNarrative,
                Title = $"Schedule Narrative - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine("# Schedule Narrative");
            content.AppendLine();

            if (context.Schedule != null)
            {
                content.AppendLine(GenerateScheduleStatusNarrative(context.Schedule, request.Tone));

                if (context.Schedule.CriticalPathActivities.Any())
                {
                    content.AppendLine();
                    content.AppendLine("## Critical Path Activities");
                    foreach (var activity in context.Schedule.CriticalPathActivities)
                    {
                        content.AppendLine($"- {activity}");
                    }
                }

                if (context.Schedule.Milestones.Any())
                {
                    content.AppendLine();
                    content.AppendLine("## Upcoming Milestones");
                    foreach (var milestone in context.Schedule.Milestones)
                    {
                        content.AppendLine($"- {milestone}");
                    }
                }
            }

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateIncidentReport(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = DocumentType.IncidentReport,
                Title = "Incident Report"
            };

            var content = new StringBuilder();
            content.AppendLine("# Incident Report");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Date:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine($"**Location:** {context.Location}");
            content.AppendLine();

            content.AppendLine("## Incident Summary");
            content.AppendLine();
            content.AppendLine("[Incident details to be provided]");
            content.AppendLine();

            content.AppendLine("## Immediate Actions Taken");
            content.AppendLine();
            content.AppendLine("1. Area secured");
            content.AppendLine("2. First aid administered if required");
            content.AppendLine("3. Supervisor notified");
            content.AppendLine("4. Incident documented");
            content.AppendLine();

            content.AppendLine("## Root Cause Analysis");
            content.AppendLine();
            content.AppendLine("[Root cause analysis to be completed]");
            content.AppendLine();

            content.AppendLine("## Corrective Actions");
            content.AppendLine();
            content.AppendLine("[Corrective actions to be determined]");

            doc.Content = content.ToString();
            return doc;
        }

        private GeneratedDocument GenerateGenericDocument(GenerationRequest request, NarrativeContext context)
        {
            var doc = new GeneratedDocument
            {
                Type = request.DocumentType,
                Title = $"{request.DocumentType} - {context.ProjectName}"
            };

            var content = new StringBuilder();
            content.AppendLine($"# {request.DocumentType}");
            content.AppendLine();
            content.AppendLine($"**Project:** {context.ProjectName}");
            content.AppendLine($"**Date:** {context.ReportDate:MMMM d, yyyy}");
            content.AppendLine();
            content.AppendLine("[Document content to be generated based on provided context]");

            doc.Content = content.ToString();
            return doc;
        }

        #endregion

        #region Narrative Generation Helpers

        private string GenerateWeatherNarrative(WeatherConditions weather, ToneStyle tone)
        {
            var sb = new StringBuilder();

            sb.Append($"Weather conditions on site were {weather.Description.ToLower()}");
            sb.Append($" with temperatures ranging from {weather.TemperatureLow}{weather.TemperatureUnit} to {weather.TemperatureHigh}{weather.TemperatureUnit}");

            if (weather.HumidityPercent > 0)
            {
                sb.Append($" and {weather.HumidityPercent}% humidity");
            }

            sb.Append(". ");

            if (!string.IsNullOrEmpty(weather.WindSpeed))
            {
                sb.Append($"Wind conditions were {weather.WindSpeed.ToLower()}. ");
            }

            if (!string.IsNullOrEmpty(weather.Precipitation) && weather.Precipitation.ToLower() != "none")
            {
                sb.Append($"Precipitation: {weather.Precipitation}. ");
            }

            if (weather.WorkImpacted)
            {
                sb.Append(PhraseLibrary.GetRandomPhrase("transition", "contrast"));
                sb.Append($", weather conditions impacted work activities. {weather.ImpactDescription}");
            }
            else
            {
                sb.Append("Weather conditions were favorable for construction activities.");
            }

            return sb.ToString();
        }

        private string GenerateWorkPerformedNarrative(List<WorkActivity> activities, ToneStyle tone)
        {
            if (!activities.Any())
                return "No work activities recorded for this period.";

            var sb = new StringBuilder();
            var groupedByTrade = activities.GroupBy(a => a.Trade).ToList();

            foreach (var tradeGroup in groupedByTrade)
            {
                var trade = string.IsNullOrEmpty(tradeGroup.Key) ? "General" : tradeGroup.Key;
                sb.AppendLine($"**{trade}:**");

                foreach (var activity in tradeGroup)
                {
                    var statusText = activity.PercentComplete >= 100 ? "Completed" :
                                    activity.PercentComplete > 0 ? $"{activity.PercentComplete:F0}% complete" :
                                    "In progress";

                    sb.AppendLine($"- {activity.Description}");
                    if (!string.IsNullOrEmpty(activity.Location))
                    {
                        sb.AppendLine($"  - Location: {activity.Location}");
                    }
                    sb.AppendLine($"  - Status: {statusText}");
                    if (activity.WorkersOnSite > 0)
                    {
                        sb.AppendLine($"  - Workers: {activity.WorkersOnSite}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateManpowerNarrative(List<WorkActivity> activities, ToneStyle tone)
        {
            var totalWorkers = activities.Sum(a => a.WorkersOnSite);
            var byTrade = activities.GroupBy(a => a.Trade)
                .Select(g => new { Trade = g.Key, Count = g.Sum(a => a.WorkersOnSite) })
                .Where(t => t.Count > 0)
                .OrderByDescending(t => t.Count)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Total manpower on site: **{totalWorkers} workers**");
            sb.AppendLine();

            if (byTrade.Any())
            {
                sb.AppendLine("| Trade | Workers |");
                sb.AppendLine("|-------|---------|");
                foreach (var trade in byTrade)
                {
                    var tradeName = string.IsNullOrEmpty(trade.Trade) ? "General Labor" : trade.Trade;
                    sb.AppendLine($"| {tradeName} | {trade.Count} |");
                }
            }

            return sb.ToString();
        }

        private string GenerateIssuesNarrative(List<IssueItem> issues, ToneStyle tone)
        {
            if (!issues.Any())
                return "No significant issues were identified during this period.";

            var sb = new StringBuilder();
            var criticalIssues = issues.Where(i => i.Severity == "Critical").ToList();
            var highIssues = issues.Where(i => i.Severity == "High").ToList();
            var otherIssues = issues.Where(i => i.Severity != "Critical" && i.Severity != "High").ToList();

            if (criticalIssues.Any())
            {
                sb.AppendLine("### Critical Issues");
                foreach (var issue in criticalIssues)
                {
                    sb.AppendLine($"- **{issue.Title}**: {issue.Description}");
                    if (!string.IsNullOrEmpty(issue.AssignedTo))
                        sb.AppendLine($"  - Assigned to: {issue.AssignedTo}");
                    if (issue.DueDate.HasValue)
                        sb.AppendLine($"  - Due: {issue.DueDate:MMM d, yyyy}");
                }
                sb.AppendLine();
            }

            if (highIssues.Any())
            {
                sb.AppendLine("### High Priority Issues");
                foreach (var issue in highIssues)
                {
                    sb.AppendLine($"- **{issue.Title}**: {issue.Description}");
                }
                sb.AppendLine();
            }

            if (otherIssues.Any())
            {
                sb.AppendLine("### Other Issues");
                foreach (var issue in otherIssues)
                {
                    sb.AppendLine($"- {issue.Title}: {issue.Description}");
                }
            }

            return sb.ToString();
        }

        private string GenerateSafetyNarrative(SafetyStatus safety, ToneStyle tone)
        {
            var sb = new StringBuilder();

            if (safety.IncidentsThisPeriod == 0)
            {
                sb.AppendLine(PhraseLibrary.GetRandomPhrase("safety", "positive"));
                sb.AppendLine($" The project has achieved {safety.DaysWithoutIncident} consecutive days without a recordable incident.");
            }
            else
            {
                sb.AppendLine(PhraseLibrary.GetRandomPhrase("safety", "incident"));
                sb.AppendLine($" {safety.IncidentsThisPeriod} incident(s) occurred during this reporting period.");
            }

            if (safety.NearMissesThisPeriod > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{safety.NearMissesThisPeriod} near-miss event(s) were reported and investigated. ");
                sb.AppendLine("Proactive reporting of near-misses demonstrates the team's commitment to identifying and addressing potential hazards.");
            }

            if (safety.SafetyObservations > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{safety.SafetyObservations} safety observations were documented. ");
            }

            sb.AppendLine();
            sb.AppendLine($"Overall safety score: **{safety.SafetyScore:F1}/100**");

            return sb.ToString();
        }

        private string GenerateQualityNarrative(QualityStatus quality, ToneStyle tone)
        {
            var sb = new StringBuilder();
            var totalInspections = quality.InspectionsPassed + quality.InspectionsFailed;
            var passRate = totalInspections > 0
                ? (double)quality.InspectionsPassed / totalInspections * 100
                : 100;

            if (passRate >= 95)
            {
                sb.AppendLine(PhraseLibrary.GetRandomPhrase("quality", "excellent"));
            }
            else if (passRate >= 80)
            {
                sb.AppendLine(PhraseLibrary.GetRandomPhrase("quality", "acceptable"));
            }
            else
            {
                sb.AppendLine(PhraseLibrary.GetRandomPhrase("quality", "concern"));
            }

            sb.AppendLine();
            sb.AppendLine($"Inspection pass rate: **{passRate:F1}%** ({quality.InspectionsPassed} passed, {quality.InspectionsFailed} failed)");

            if (quality.OpenDeficiencies > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**{quality.OpenDeficiencies}** open deficiencies require attention. ");
                sb.AppendLine($"{quality.ClosedDeficiencies} deficiencies have been resolved to date.");
            }

            if (quality.QualityIssues.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Quality issues requiring attention:");
                foreach (var issue in quality.QualityIssues)
                {
                    sb.AppendLine($"- {issue}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Overall quality score: **{quality.QualityScore:F1}/100**");

            return sb.ToString();
        }

        private string GenerateScheduleStatusNarrative(ScheduleStatus schedule, ToneStyle tone)
        {
            var sb = new StringBuilder();

            var statusKey = schedule.DaysAheadBehind > 0 ? "ahead" :
                           schedule.DaysAheadBehind < 0 ? "behind" : "on_track";

            sb.AppendLine(PhraseLibrary.GetRandomPhrase("status", statusKey));
            sb.AppendLine();

            sb.AppendLine($"- **Overall Progress:** {schedule.PercentComplete:F1}% complete");
            sb.AppendLine($"- **Schedule Variance:** {Math.Abs(schedule.DaysAheadBehind)} days {(schedule.DaysAheadBehind >= 0 ? "ahead" : "behind")}");
            sb.AppendLine($"- **Planned Completion:** {schedule.PlannedCompletion:MMMM d, yyyy}");
            sb.AppendLine($"- **Forecast Completion:** {schedule.ForecastCompletion:MMMM d, yyyy}");

            if (schedule.CriticalPathActivities.Any())
            {
                sb.AppendLine();
                sb.AppendLine("**Critical Path Activities:**");
                foreach (var activity in schedule.CriticalPathActivities.Take(5))
                {
                    sb.AppendLine($"- {activity}");
                }
            }

            return sb.ToString();
        }

        private string GenerateBudgetStatusNarrative(BudgetStatus budget, ToneStyle tone)
        {
            var sb = new StringBuilder();

            var statusKey = budget.Variance >= 0 ? "under_budget" : "over_budget";
            sb.AppendLine(PhraseLibrary.GetRandomPhrase("status", statusKey));
            sb.AppendLine();

            sb.AppendLine($"- **Current Budget:** {budget.CurrentBudget:C0}");
            sb.AppendLine($"- **Spent to Date:** {budget.SpentToDate:C0} ({budget.PercentSpent:F1}%)");
            sb.AppendLine($"- **Committed:** {budget.CommittedCosts:C0}");
            sb.AppendLine($"- **Forecast at Completion:** {budget.ForecastAtCompletion:C0}");
            sb.AppendLine($"- **Variance:** {budget.Variance:C0}");

            if (budget.Variance < 0)
            {
                sb.AppendLine();
                sb.AppendLine("Cost containment measures are being reviewed to bring the project back within budget. ");
                sb.AppendLine("Potential change orders may be required to address scope changes impacting costs.");
            }

            return sb.ToString();
        }

        private string GenerateExecutiveSummaryNarrative(NarrativeContext context, ToneStyle tone, AudienceLevel audience)
        {
            var sb = new StringBuilder();

            // Opening statement
            sb.Append($"The {context.ProjectName} project ");

            if (context.Schedule != null)
            {
                if (context.Schedule.DaysAheadBehind >= 0)
                {
                    sb.Append("is progressing on schedule ");
                }
                else
                {
                    sb.Append($"is experiencing schedule challenges ");
                }

                sb.Append($"at {context.Schedule.PercentComplete:F0}% complete");
            }

            if (context.Budget != null)
            {
                if (context.Budget.Variance >= 0)
                {
                    sb.Append(" and remains within budget");
                }
                else
                {
                    sb.Append(" with cost variances requiring attention");
                }
            }

            sb.AppendLine(".");
            sb.AppendLine();

            // Key highlights based on audience
            if (audience == AudienceLevel.Executive)
            {
                if (context.Accomplishments.Any())
                {
                    sb.AppendLine("**Key Accomplishments:**");
                    foreach (var accomplishment in context.Accomplishments.Take(3))
                    {
                        sb.AppendLine($"- {accomplishment}");
                    }
                    sb.AppendLine();
                }

                var criticalIssues = context.Issues.Where(i => i.Severity == "Critical" || i.Severity == "High").ToList();
                if (criticalIssues.Any())
                {
                    sb.AppendLine("**Attention Required:**");
                    foreach (var issue in criticalIssues.Take(3))
                    {
                        sb.AppendLine($"- {issue.Title}");
                    }
                }
            }

            return sb.ToString();
        }

        private string GenerateMilestonesNarrative(NarrativeContext context, ToneStyle tone)
        {
            var sb = new StringBuilder();

            if (context.Schedule?.Milestones.Any() == true)
            {
                sb.AppendLine("The following milestones are tracked for this period:");
                sb.AppendLine();
                foreach (var milestone in context.Schedule.Milestones)
                {
                    sb.AppendLine($"- {milestone}");
                }
            }
            else
            {
                sb.AppendLine("Milestone tracking data not available for this period.");
            }

            return sb.ToString();
        }

        private string GenerateRiskAssessmentNarrative(NarrativeContext context, ToneStyle tone)
        {
            var sb = new StringBuilder();

            var criticalIssues = context.Issues.Where(i => i.Severity == "Critical").Count();
            var highIssues = context.Issues.Where(i => i.Severity == "High").Count();

            if (criticalIssues > 0 || highIssues > 0)
            {
                sb.AppendLine($"The project currently has {criticalIssues} critical and {highIssues} high-priority risks identified. ");
                sb.AppendLine("Active monitoring and mitigation strategies are in place.");
            }
            else
            {
                sb.AppendLine("No critical risks are currently identified. Standard project risks are being monitored through the risk register.");
            }

            return sb.ToString();
        }

        private string GenerateMonthlyExecutiveSummary(NarrativeContext context, ToneStyle tone)
        {
            return $"Monthly status: {context.Schedule?.PercentComplete:F0}% complete, " +
                   $"{(context.Budget?.Variance >= 0 ? "under" : "over")} budget by {Math.Abs(context.Budget?.Variance ?? 0):C0}";
        }

        private string GenerateDailySummary(NarrativeContext context, ToneStyle tone)
        {
            var workers = context.Activities.Sum(a => a.WorkersOnSite);
            var activities = context.Activities.Count;
            return $"Daily summary: {workers} workers on site, {activities} activities in progress, " +
                   $"{(context.Issues.Any() ? $"{context.Issues.Count} issues noted" : "no issues")}";
        }

        private string GenerateOneLinerSummary(NarrativeContext context)
        {
            var scheduleStatus = context.Schedule?.DaysAheadBehind >= 0 ? "on track" : "behind schedule";
            var budgetStatus = context.Budget?.Variance >= 0 ? "under budget" : "over budget";
            return $"Project is {scheduleStatus} and {budgetStatus} at {context.Schedule?.PercentComplete:F0}% complete.";
        }

        #endregion

        #region Notification Generation

        /// <summary>
        /// Generate notification content based on event type and context
        /// </summary>
        public async Task<NotificationContent> GenerateNotificationAsync(
            NotificationType type,
            Dictionary<string, object> context,
            ToneStyle tone = ToneStyle.Professional,
            CancellationToken ct = default)
        {
            await Task.CompletedTask; // Placeholder for async operations

            var notification = new NotificationContent
            {
                Type = type,
                Priority = DetermineNotificationPriority(type, context)
            };

            switch (type)
            {
                case NotificationType.Alert:
                    GenerateAlertNotification(notification, context, tone);
                    break;
                case NotificationType.Warning:
                    GenerateWarningNotification(notification, context, tone);
                    break;
                case NotificationType.ActionRequired:
                    GenerateActionRequiredNotification(notification, context, tone);
                    break;
                case NotificationType.Reminder:
                    GenerateReminderNotification(notification, context, tone);
                    break;
                case NotificationType.Milestone:
                    GenerateMilestoneNotification(notification, context, tone);
                    break;
                case NotificationType.Issue:
                    GenerateIssueNotification(notification, context, tone);
                    break;
                case NotificationType.Delay:
                    GenerateDelayNotification(notification, context, tone);
                    break;
                case NotificationType.Completion:
                    GenerateCompletionNotification(notification, context, tone);
                    break;
                default:
                    GenerateInformationNotification(notification, context, tone);
                    break;
            }

            IncrementUsage($"notification_{type}");
            return notification;
        }

        private Priority DetermineNotificationPriority(NotificationType type, Dictionary<string, object> context)
        {
            return type switch
            {
                NotificationType.Alert => Priority.High,
                NotificationType.Warning => Priority.High,
                NotificationType.ActionRequired => Priority.High,
                NotificationType.Escalation => Priority.Urgent,
                NotificationType.Delay when context.ContainsKey("critical_path") => Priority.Critical,
                NotificationType.Issue when context.TryGetValue("severity", out var sev) && sev?.ToString() == "Critical" => Priority.Critical,
                NotificationType.Milestone => Priority.Normal,
                NotificationType.Completion => Priority.Normal,
                NotificationType.Reminder => Priority.Low,
                NotificationType.Update => Priority.Low,
                _ => Priority.Normal
            };
        }

        private void GenerateAlertNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var title = context.TryGetValue("title", out var t) ? t.ToString() : "Alert";
            var message = context.TryGetValue("message", out var m) ? m.ToString() : "An alert has been raised.";

            notification.Subject = $"[ALERT] {title}";
            notification.ShortMessage = message ?? string.Empty;
            notification.FullMessage = $"Alert: {title}\n\n{message}\n\nPlease review and take appropriate action.";
            notification.ActionText = "View Details";
        }

        private void GenerateWarningNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var title = context.TryGetValue("title", out var t) ? t.ToString() : "Warning";
            var message = context.TryGetValue("message", out var m) ? m.ToString() : "A warning has been issued.";

            notification.Subject = $"[WARNING] {title}";
            notification.ShortMessage = message ?? string.Empty;
            notification.FullMessage = $"Warning: {title}\n\n{message}\n\nPlease be aware and monitor the situation.";
            notification.ActionText = "Acknowledge";
        }

        private void GenerateActionRequiredNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var action = context.TryGetValue("action", out var a) ? a.ToString() : "review this item";
            var deadline = context.TryGetValue("deadline", out var d) ? d.ToString() : "as soon as possible";

            notification.Subject = $"Action Required: {action}";
            notification.ShortMessage = $"Your action is required to {action}. Please respond by {deadline}.";
            notification.FullMessage = $"Action Required\n\nYou are requested to {action}.\n\nDeadline: {deadline}\n\nPlease complete this action to avoid delays.";
            notification.ActionText = "Take Action";
        }

        private void GenerateReminderNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var item = context.TryGetValue("item", out var i) ? i.ToString() : "scheduled task";
            var dueDate = context.TryGetValue("due_date", out var d) ? d.ToString() : "soon";

            notification.Subject = $"Reminder: {item}";
            notification.ShortMessage = $"Reminder: {item} is due {dueDate}.";
            notification.FullMessage = $"This is a reminder that {item} is due {dueDate}.\n\nPlease ensure this is completed on time.";
            notification.ActionText = "View Item";
        }

        private void GenerateMilestoneNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var milestone = context.TryGetValue("milestone", out var m) ? m.ToString() : "Project Milestone";
            var status = context.TryGetValue("status", out var s) ? s.ToString() : "achieved";

            notification.Subject = $"Milestone {status}: {milestone}";
            notification.ShortMessage = $"The milestone '{milestone}' has been {status}.";
            notification.FullMessage = $"Milestone Update\n\nMilestone: {milestone}\nStatus: {status}\n\nCongratulations to the team on this achievement!";
            notification.ActionText = "View Milestone";
        }

        private void GenerateIssueNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var issue = context.TryGetValue("issue", out var i) ? i.ToString() : "An issue";
            var severity = context.TryGetValue("severity", out var s) ? s.ToString() : "Medium";

            notification.Subject = $"[{severity?.ToUpper()}] Issue: {issue}";
            notification.ShortMessage = $"A {severity?.ToLower()} priority issue has been identified: {issue}";
            notification.FullMessage = $"Issue Report\n\nSeverity: {severity}\nDescription: {issue}\n\nPlease review and assign for resolution.";
            notification.ActionText = "View Issue";
        }

        private void GenerateDelayNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var activity = context.TryGetValue("activity", out var a) ? a.ToString() : "Activity";
            var days = context.TryGetValue("days", out var d) ? d.ToString() : "unknown";
            var impact = context.TryGetValue("impact", out var i) ? i.ToString() : "under assessment";

            notification.Subject = $"Schedule Delay: {activity}";
            notification.ShortMessage = $"{activity} is delayed by {days} days. Impact: {impact}";
            notification.FullMessage = $"Schedule Delay Notification\n\nActivity: {activity}\nDelay: {days} days\nImpact: {impact}\n\nRecovery measures are being evaluated.";
            notification.ActionText = "View Schedule";
        }

        private void GenerateCompletionNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var item = context.TryGetValue("item", out var i) ? i.ToString() : "Task";

            notification.Subject = $"Completed: {item}";
            notification.ShortMessage = $"{item} has been completed successfully.";
            notification.FullMessage = $"Completion Notice\n\n{item} has been marked as complete.\n\nThank you for your contribution to the project.";
            notification.ActionText = "View Details";
        }

        private void GenerateInformationNotification(NotificationContent notification, Dictionary<string, object> context, ToneStyle tone)
        {
            var title = context.TryGetValue("title", out var t) ? t.ToString() : "Information";
            var message = context.TryGetValue("message", out var m) ? m.ToString() : "No additional details.";

            notification.Subject = title ?? "Information";
            notification.ShortMessage = message ?? string.Empty;
            notification.FullMessage = $"{title}\n\n{message}";
            notification.ActionText = "View";
        }

        #endregion

        #region Summary Generation

        /// <summary>
        /// Generate a summary of provided content
        /// </summary>
        public async Task<GeneratedSummary> GenerateSummaryAsync(
            SummaryRequest request,
            CancellationToken ct = default)
        {
            await Task.CompletedTask;

            var summary = new GeneratedSummary
            {
                RequestId = request.RequestId
            };

            // Extract key points from content
            var sentences = SplitIntoSentences(request.SourceContent);
            var keyPoints = ExtractKeyPoints(sentences, request.FocusAreas);

            summary.KeyPoints = keyPoints.Take(GetPointCount(request.TargetLength)).ToList();
            summary.Summary = GenerateSummaryText(keyPoints, request.TargetLength, request.Audience);

            if (request.IncludeKeyMetrics)
            {
                summary.Metrics = ExtractMetrics(request.SourceContent);
            }

            if (request.IncludeRecommendations)
            {
                summary.Recommendations = GenerateRecommendations(keyPoints);
            }

            summary.CompressionRatio = request.SourceContent.Length > 0
                ? (double)summary.Summary.Length / request.SourceContent.Length
                : 0;

            IncrementUsage("summary");
            return summary;
        }

        private List<string> SplitIntoSentences(string content)
        {
            var delimiters = new[] { ". ", "! ", "? ", "\n" };
            var sentences = new List<string>();
            var remaining = content;

            while (!string.IsNullOrEmpty(remaining))
            {
                var nextDelimiter = -1;
                var delimiterLength = 0;

                foreach (var delimiter in delimiters)
                {
                    var index = remaining.IndexOf(delimiter, StringComparison.Ordinal);
                    if (index >= 0 && (nextDelimiter < 0 || index < nextDelimiter))
                    {
                        nextDelimiter = index;
                        delimiterLength = delimiter.Length;
                    }
                }

                if (nextDelimiter >= 0)
                {
                    var sentence = remaining.Substring(0, nextDelimiter + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    remaining = remaining.Substring(nextDelimiter + delimiterLength);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(remaining))
                    {
                        sentences.Add(remaining.Trim());
                    }
                    break;
                }
            }

            return sentences;
        }

        private List<string> ExtractKeyPoints(List<string> sentences, List<string> focusAreas)
        {
            var scored = sentences.Select(s => new
            {
                Sentence = s,
                Score = CalculateSentenceImportance(s, focusAreas)
            })
            .OrderByDescending(s => s.Score)
            .Select(s => s.Sentence)
            .ToList();

            return scored;
        }

        private double CalculateSentenceImportance(string sentence, List<string> focusAreas)
        {
            var score = 0.0;
            var lowerSentence = sentence.ToLower();

            // Important keywords boost
            var importantKeywords = new[] { "critical", "important", "key", "significant", "major", "primary", "essential" };
            foreach (var keyword in importantKeywords)
            {
                if (lowerSentence.Contains(keyword))
                    score += 2.0;
            }

            // Focus areas boost
            foreach (var focus in focusAreas)
            {
                if (lowerSentence.Contains(focus.ToLower()))
                    score += 3.0;
            }

            // Numbers/metrics boost
            if (System.Text.RegularExpressions.Regex.IsMatch(sentence, @"\d+%|\$[\d,]+|\d+\s*(days|weeks|months)"))
                score += 1.5;

            // Length penalty for very short or very long sentences
            if (sentence.Length < 20)
                score -= 1.0;
            if (sentence.Length > 200)
                score -= 0.5;

            return score;
        }

        private int GetPointCount(ContentLength length)
        {
            return length switch
            {
                ContentLength.Brief => 1,
                ContentLength.Short => 3,
                ContentLength.Medium => 5,
                ContentLength.Long => 8,
                ContentLength.Comprehensive => 12,
                _ => 5
            };
        }

        private string GenerateSummaryText(List<string> keyPoints, ContentLength length, AudienceLevel audience)
        {
            var sb = new StringBuilder();
            var pointCount = GetPointCount(length);

            foreach (var point in keyPoints.Take(pointCount))
            {
                sb.AppendLine(point);
            }

            return sb.ToString().Trim();
        }

        private List<KeyMetric> ExtractMetrics(string content)
        {
            var metrics = new List<KeyMetric>();

            // Extract percentages
            var percentMatches = System.Text.RegularExpressions.Regex.Matches(content, @"(\d+(?:\.\d+)?)\s*%");
            foreach (System.Text.RegularExpressions.Match match in percentMatches)
            {
                metrics.Add(new KeyMetric
                {
                    Value = match.Groups[1].Value,
                    Unit = "%"
                });
            }

            // Extract currency
            var currencyMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\$[\d,]+(?:\.\d{2})?");
            foreach (System.Text.RegularExpressions.Match match in currencyMatches)
            {
                metrics.Add(new KeyMetric
                {
                    Value = match.Value,
                    Unit = "USD"
                });
            }

            // Extract time durations
            var timeMatches = System.Text.RegularExpressions.Regex.Matches(content, @"(\d+)\s*(days?|weeks?|months?|years?)");
            foreach (System.Text.RegularExpressions.Match match in timeMatches)
            {
                metrics.Add(new KeyMetric
                {
                    Value = match.Groups[1].Value,
                    Unit = match.Groups[2].Value
                });
            }

            return metrics.Take(10).ToList();
        }

        private List<string> GenerateRecommendations(List<string> keyPoints)
        {
            var recommendations = new List<string>();

            // Generate generic recommendations based on key points content
            foreach (var point in keyPoints.Take(3))
            {
                var lowerPoint = point.ToLower();

                if (lowerPoint.Contains("delay") || lowerPoint.Contains("behind"))
                {
                    recommendations.Add("Review schedule recovery options and consider resource reallocation");
                }
                else if (lowerPoint.Contains("cost") || lowerPoint.Contains("budget") || lowerPoint.Contains("over"))
                {
                    recommendations.Add("Implement cost containment measures and review change order management");
                }
                else if (lowerPoint.Contains("safety") || lowerPoint.Contains("incident"))
                {
                    recommendations.Add("Reinforce safety protocols and conduct additional training sessions");
                }
                else if (lowerPoint.Contains("quality") || lowerPoint.Contains("defect"))
                {
                    recommendations.Add("Enhance quality control inspections and address root causes");
                }
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Continue monitoring project performance indicators");
                recommendations.Add("Maintain regular communication with stakeholders");
            }

            return recommendations.Distinct().ToList();
        }

        #endregion

        #region Template Management

        /// <summary>
        /// Get a document template by ID
        /// </summary>
        public DocumentTemplate? GetTemplate(string templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        /// <summary>
        /// Get all available templates
        /// </summary>
        public List<DocumentTemplate> GetAllTemplates()
        {
            return _templates.Values.Where(t => t.IsActive).ToList();
        }

        /// <summary>
        /// Register a custom template
        /// </summary>
        public void RegisterTemplate(DocumentTemplate template)
        {
            _templates[template.TemplateId] = template;
        }

        #endregion

        #region Utility Methods

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private void CalculateReadability(GeneratedDocument document)
        {
            var words = CountWords(document.Content);
            var sentences = document.Content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var syllables = EstimateSyllables(document.Content);

            if (words > 0 && sentences > 0)
            {
                // Flesch-Kincaid Grade Level
                var grade = 0.39 * ((double)words / sentences) + 11.8 * ((double)syllables / words) - 15.59;
                document.ReadabilityScore = Math.Max(0, Math.Min(100, 100 - grade * 5));
                document.ReadabilityGrade = GetGradeLevel(grade);
            }
            else
            {
                document.ReadabilityScore = 50;
                document.ReadabilityGrade = "N/A";
            }
        }

        private int EstimateSyllables(string text)
        {
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var total = 0;

            foreach (var word in words)
            {
                var cleaned = new string(word.Where(char.IsLetter).ToArray()).ToLower();
                if (cleaned.Length == 0) continue;

                var count = 0;
                var wasVowel = false;

                foreach (var c in cleaned)
                {
                    var isVowel = "aeiou".Contains(c);
                    if (isVowel && !wasVowel)
                        count++;
                    wasVowel = isVowel;
                }

                // Adjust for silent e
                if (cleaned.EndsWith("e") && count > 1)
                    count--;

                total += Math.Max(1, count);
            }

            return total;
        }

        private string GetGradeLevel(double grade)
        {
            return grade switch
            {
                < 6 => "Elementary",
                < 9 => "Middle School",
                < 12 => "High School",
                < 16 => "College",
                _ => "Graduate"
            };
        }

        private void IncrementUsage(string category)
        {
            _usageStats.AddOrUpdate(category, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Get usage statistics
        /// </summary>
        public Dictionary<string, int> GetUsageStats()
        {
            return new Dictionary<string, int>(_usageStats);
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _generationSemaphore.Dispose();
            _templates.Clear();
            _documentCache.Clear();

            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
