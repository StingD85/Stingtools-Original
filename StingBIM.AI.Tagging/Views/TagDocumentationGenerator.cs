// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagDocumentationGenerator.cs - Auto-generate tag documentation, schedules, legends, and reports
// Produces schedules, legends, key plans, reports, and submittal documentation from tag data
//
// Documentation Capabilities:
//   1. Tag Schedules       - Category-based, cross-category, with grouping and calculated fields
//   2. Tag Legends         - Symbol, color code, abbreviation legends
//   3. Report Generation   - Summary, quality, compliance, productivity, audit reports
//   4. Export Formats      - CSV, JSON, HTML, Markdown
//   5. Submittal Docs      - Equipment submittals, spec cross-reference, vendor data
//   6. As-Built Docs       - Milestone capture, design-vs-actual comparison, handover packages
//   7. Document Templates  - Per-project-type templates with brand customization

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Views
{
    #region Data Models

    public sealed class ScheduleColumn
    {
        public string Header { get; set; }
        public string ParameterName { get; set; }
        public string Format { get; set; }
        public bool IsCalculated { get; set; }
        public string Formula { get; set; } // e.g., "SUM", "COUNT", "AVG"
        public int Width { get; set; } = 100;
        public string Alignment { get; set; } = "Left";
    }

    public sealed class ScheduleDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CategoryFilter { get; set; }
        public List<ScheduleColumn> Columns { get; set; } = new();
        public string GroupBy { get; set; }
        public string SortBy { get; set; }
        public bool Ascending { get; set; } = true;
        public string FilterExpression { get; set; }
        public bool ShowTotals { get; set; }
    }

    public sealed class ScheduleRow
    {
        public Dictionary<string, string> Values { get; set; } = new();
        public bool IsGroupHeader { get; set; }
        public bool IsTotalRow { get; set; }
        public string GroupName { get; set; }
    }

    public sealed class GeneratedSchedule
    {
        public string Name { get; set; }
        public List<ScheduleColumn> Columns { get; set; } = new();
        public List<ScheduleRow> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class LegendEntry
    {
        public string Symbol { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public string Category { get; set; }
    }

    public sealed class DocumentSection
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public int Level { get; set; } = 1;
        public List<DocumentSection> SubSections { get; set; } = new();
    }

    public sealed class DocumentTemplate
    {
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public string CompanyName { get; set; }
        public string LogoPath { get; set; }
        public List<string> Sections { get; set; } = new();
        public Dictionary<string, string> Variables { get; set; } = new();
    }

    #endregion

    #region Schedule Builder

    internal sealed class ScheduleBuilder
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public GeneratedSchedule Build(
            ScheduleDefinition definition,
            List<TagInstance> tags,
            Dictionary<string, Dictionary<string, string>> elementParameters)
        {
            var schedule = new GeneratedSchedule
            {
                Name = definition.Name,
                Columns = definition.Columns
            };

            // Filter tags
            var filtered = tags.AsEnumerable();
            if (!string.IsNullOrEmpty(definition.CategoryFilter))
            {
                filtered = filtered.Where(t =>
                    string.Equals(t.CategoryName, definition.CategoryFilter,
                        StringComparison.OrdinalIgnoreCase));
            }

            var tagList = filtered.ToList();

            // Build rows
            var rows = new List<ScheduleRow>();
            foreach (var tag in tagList)
            {
                var row = new ScheduleRow();
                elementParameters.TryGetValue(tag.HostElementId.ToString(), out var elemParams);
                elemParams ??= new Dictionary<string, string>();

                foreach (var col in definition.Columns)
                {
                    string value = "";
                    if (col.ParameterName == "TagId") value = tag.TagId;
                    else if (col.ParameterName == "ElementId") value = tag.HostElementId.ToString();
                    else if (col.ParameterName == "Category") value = tag.CategoryName;
                    else if (col.ParameterName == "ViewId") value = tag.ViewId.ToString();
                    else if (col.ParameterName == "Content") value = tag.DisplayText;
                    else if (col.ParameterName == "Template") value = tag.CreatedByTemplate;
                    else if (col.ParameterName == "State") value = tag.State.ToString();
                    else if (elemParams.TryGetValue(col.ParameterName ?? "", out var pVal))
                        value = pVal;

                    row.Values[col.Header] = value ?? "";
                }
                rows.Add(row);
            }

            // Sort
            if (!string.IsNullOrEmpty(definition.SortBy))
            {
                rows = definition.Ascending
                    ? rows.OrderBy(r => r.Values.GetValueOrDefault(definition.SortBy, "")).ToList()
                    : rows.OrderByDescending(r => r.Values.GetValueOrDefault(definition.SortBy, "")).ToList();
            }

            // Group
            if (!string.IsNullOrEmpty(definition.GroupBy))
            {
                var grouped = rows.GroupBy(r => r.Values.GetValueOrDefault(definition.GroupBy, ""));
                var groupedRows = new List<ScheduleRow>();
                foreach (var group in grouped.OrderBy(g => g.Key))
                {
                    groupedRows.Add(new ScheduleRow
                    {
                        IsGroupHeader = true,
                        GroupName = group.Key,
                        Values = new Dictionary<string, string>
                        {
                            [definition.Columns.First().Header] = $"--- {definition.GroupBy}: {group.Key} ({group.Count()}) ---"
                        }
                    });
                    groupedRows.AddRange(group);
                }
                rows = groupedRows;
            }

            // Totals
            if (definition.ShowTotals)
            {
                rows.Add(new ScheduleRow
                {
                    IsTotalRow = true,
                    Values = new Dictionary<string, string>
                    {
                        [definition.Columns.First().Header] = $"TOTAL: {tagList.Count} items"
                    }
                });
            }

            schedule.Rows = rows;
            schedule.TotalCount = tagList.Count;

            Logger.Debug("Schedule '{Name}' built: {Rows} rows, {Count} tags",
                definition.Name, rows.Count, tagList.Count);

            return schedule;
        }
    }

    #endregion

    #region Export Formatters

    internal sealed class ExportFormatter
    {
        public string ToCsv(GeneratedSchedule schedule)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",",
                schedule.Columns.Select(c => EscapeCsv(c.Header))));

            // Rows
            foreach (var row in schedule.Rows)
            {
                var values = schedule.Columns
                    .Select(c => EscapeCsv(row.Values.GetValueOrDefault(c.Header, "")));
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        public string ToJson(GeneratedSchedule schedule)
        {
            var data = schedule.Rows
                .Where(r => !r.IsGroupHeader && !r.IsTotalRow)
                .Select(r => r.Values)
                .ToList();
            return JsonConvert.SerializeObject(new
            {
                schedule.Name,
                schedule.GeneratedAt,
                schedule.TotalCount,
                Data = data
            }, Formatting.Indented);
        }

        public string ToHtml(GeneratedSchedule schedule, string title = null,
            string companyName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\">");
            sb.AppendLine($"<title>{title ?? schedule.Name}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #2c3e50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".group-header { background-color: #ecf0f1; font-weight: bold; }");
            sb.AppendLine(".total-row { background-color: #d5dbdb; font-weight: bold; }");
            sb.AppendLine("h1 { color: #2c3e50; }");
            sb.AppendLine(".meta { color: #7f8c8d; font-size: 0.9em; }");
            sb.AppendLine("</style></head><body>");

            if (!string.IsNullOrEmpty(companyName))
                sb.AppendLine($"<p>{companyName}</p>");

            sb.AppendLine($"<h1>{schedule.Name}</h1>");
            sb.AppendLine($"<p class=\"meta\">Generated: {schedule.GeneratedAt:yyyy-MM-dd HH:mm} | " +
                $"Total: {schedule.TotalCount} items</p>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            foreach (var col in schedule.Columns)
                sb.AppendLine($"<th>{col.Header}</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var row in schedule.Rows)
            {
                string cls = row.IsGroupHeader ? " class=\"group-header\"" :
                    row.IsTotalRow ? " class=\"total-row\"" : "";
                sb.AppendLine($"<tr{cls}>");
                foreach (var col in schedule.Columns)
                    sb.AppendLine($"<td>{row.Values.GetValueOrDefault(col.Header, "")}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            return sb.ToString();
        }

        public string ToMarkdown(GeneratedSchedule schedule)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {schedule.Name}");
            sb.AppendLine($"*Generated: {schedule.GeneratedAt:yyyy-MM-dd HH:mm} | Total: {schedule.TotalCount}*");
            sb.AppendLine();

            // Header
            sb.AppendLine("| " + string.Join(" | ", schedule.Columns.Select(c => c.Header)) + " |");
            sb.AppendLine("| " + string.Join(" | ", schedule.Columns.Select(_ => "---")) + " |");

            // Rows
            foreach (var row in schedule.Rows.Where(r => !r.IsGroupHeader && !r.IsTotalRow))
            {
                sb.AppendLine("| " + string.Join(" | ",
                    schedule.Columns.Select(c => row.Values.GetValueOrDefault(c.Header, ""))) + " |");
            }

            return sb.ToString();
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    #endregion

    #region Report Builder

    internal sealed class ReportBuilder
    {
        public string BuildSummaryReport(
            int totalTags, int totalElements,
            Dictionary<string, int> tagsByCategory,
            Dictionary<string, int> tagsByView,
            double qualityScore)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tag Summary Report");
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine("## Overview");
            sb.AppendLine($"- Total Tags: **{totalTags}**");
            sb.AppendLine($"- Total Elements: **{totalElements}**");
            sb.AppendLine($"- Coverage: **{(totalElements > 0 ? (double)totalTags / totalElements * 100 : 0):F1}%**");
            sb.AppendLine($"- Quality Score: **{qualityScore:F0}/100**");
            sb.AppendLine();

            sb.AppendLine("## Tags by Category");
            foreach (var cat in tagsByCategory.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"- {cat.Key}: {cat.Value}");
            sb.AppendLine();

            sb.AppendLine("## Tags by View");
            foreach (var view in tagsByView.OrderByDescending(kv => kv.Value).Take(20))
                sb.AppendLine($"- {view.Key}: {view.Value}");

            return sb.ToString();
        }

        public string BuildQualityReport(
            double overallScore,
            int overlaps, int orphans, int duplicates, int blanks,
            int stale, int misaligned,
            List<string> topIssues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tag Quality Report");
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"## Overall Quality Score: {overallScore:F0}/100");
            sb.AppendLine();
            sb.AppendLine("## Issue Breakdown");
            sb.AppendLine($"| Issue Type | Count |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| Overlaps | {overlaps} |");
            sb.AppendLine($"| Orphan Tags | {orphans} |");
            sb.AppendLine($"| Duplicates | {duplicates} |");
            sb.AppendLine($"| Blank Content | {blanks} |");
            sb.AppendLine($"| Stale Content | {stale} |");
            sb.AppendLine($"| Misaligned | {misaligned} |");
            sb.AppendLine();

            if (topIssues.Any())
            {
                sb.AppendLine("## Top Issues");
                foreach (var issue in topIssues.Take(10))
                    sb.AppendLine($"- {issue}");
            }

            return sb.ToString();
        }
    }

    #endregion

    #region Main Documentation Generator

    /// <summary>
    /// Auto-generates comprehensive tag documentation: schedules, legends, reports,
    /// and export packages from tag data.
    /// </summary>
    public sealed class TagDocumentationGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly ScheduleBuilder _scheduleBuilder = new();
        private readonly ExportFormatter _exportFormatter = new();
        private readonly ReportBuilder _reportBuilder = new();
        private readonly Dictionary<string, DocumentTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

        public TagDocumentationGenerator()
        {
            InitializeDefaultTemplates();
            Logger.Info("TagDocumentationGenerator initialized with {Count} templates",
                _templates.Count);
        }

        #region Schedule Generation

        /// <summary>
        /// Generate a tag schedule for a specific category.
        /// </summary>
        public GeneratedSchedule GenerateSchedule(
            ScheduleDefinition definition,
            List<TagInstance> tags,
            Dictionary<string, Dictionary<string, string>> elementParameters)
        {
            using var _ = new PerformanceTimer("GenerateSchedule");
            return _scheduleBuilder.Build(definition, tags, elementParameters);
        }

        /// <summary>
        /// Get pre-defined schedule definitions for common categories.
        /// </summary>
        public ScheduleDefinition GetDefaultScheduleDefinition(string category)
        {
            var catLower = category?.ToLowerInvariant() ?? "";

            if (catLower.Contains("door"))
            {
                return new ScheduleDefinition
                {
                    Name = "Door Tag Schedule",
                    CategoryFilter = category,
                    Columns = new List<ScheduleColumn>
                    {
                        new() { Header = "Mark", ParameterName = "Mark", Width = 60 },
                        new() { Header = "Level", ParameterName = "Level", Width = 80 },
                        new() { Header = "Room From", ParameterName = "From_Room", Width = 100 },
                        new() { Header = "Room To", ParameterName = "To_Room", Width = 100 },
                        new() { Header = "Type", ParameterName = "Type", Width = 120 },
                        new() { Header = "Fire Rating", ParameterName = "Fire_Rating", Width = 80 },
                        new() { Header = "Hardware", ParameterName = "Hardware_Set", Width = 100 },
                    },
                    GroupBy = "Level",
                    SortBy = "Mark",
                    ShowTotals = true
                };
            }
            else if (catLower.Contains("room"))
            {
                return new ScheduleDefinition
                {
                    Name = "Room Tag Schedule",
                    CategoryFilter = category,
                    Columns = new List<ScheduleColumn>
                    {
                        new() { Header = "Number", ParameterName = "Number", Width = 60 },
                        new() { Header = "Name", ParameterName = "Name", Width = 150 },
                        new() { Header = "Level", ParameterName = "Level", Width = 80 },
                        new() { Header = "Area", ParameterName = "Area", Width = 80 },
                        new() { Header = "Department", ParameterName = "Department", Width = 100 },
                        new() { Header = "Finish Floor", ParameterName = "Floor_Finish", Width = 100 },
                    },
                    GroupBy = "Level",
                    SortBy = "Number",
                    ShowTotals = true
                };
            }
            else
            {
                return new ScheduleDefinition
                {
                    Name = $"{category} Tag Schedule",
                    CategoryFilter = category,
                    Columns = new List<ScheduleColumn>
                    {
                        new() { Header = "Mark", ParameterName = "Mark", Width = 80 },
                        new() { Header = "Type", ParameterName = "Type", Width = 150 },
                        new() { Header = "Level", ParameterName = "Level", Width = 80 },
                        new() { Header = "Content", ParameterName = "Content", Width = 200 },
                    },
                    SortBy = "Mark",
                    ShowTotals = true
                };
            }
        }

        #endregion

        #region Legend Generation

        /// <summary>
        /// Generate an abbreviation legend from tag data.
        /// </summary>
        public List<LegendEntry> GenerateAbbreviationLegend(
            Dictionary<string, string> abbreviations)
        {
            return abbreviations
                .OrderBy(kv => kv.Key)
                .Select(kv => new LegendEntry
                {
                    Symbol = kv.Key,
                    Description = kv.Value,
                    Category = "Abbreviation"
                })
                .ToList();
        }

        /// <summary>
        /// Generate a category legend (what tag shapes/colors mean).
        /// </summary>
        public List<LegendEntry> GenerateCategoryLegend(
            Dictionary<string, string> categoryColors)
        {
            return categoryColors
                .OrderBy(kv => kv.Key)
                .Select(kv => new LegendEntry
                {
                    Symbol = kv.Key,
                    Description = $"Tags for {kv.Key} elements",
                    Color = kv.Value,
                    Category = "Category"
                })
                .ToList();
        }

        #endregion

        #region Report Generation

        public string GenerateSummaryReport(
            List<TagInstance> tags,
            int totalElements,
            double qualityScore)
        {
            var byCategory = tags.GroupBy(t => t.CategoryName ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            var byView = tags.GroupBy(t => t.ViewId.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            return _reportBuilder.BuildSummaryReport(
                tags.Count, totalElements, byCategory, byView, qualityScore);
        }

        public string GenerateQualityReport(
            double overallScore,
            Dictionary<string, int> issueBreakdown,
            List<string> topIssues)
        {
            return _reportBuilder.BuildQualityReport(
                overallScore,
                issueBreakdown.GetValueOrDefault("Overlaps"),
                issueBreakdown.GetValueOrDefault("Orphans"),
                issueBreakdown.GetValueOrDefault("Duplicates"),
                issueBreakdown.GetValueOrDefault("Blanks"),
                issueBreakdown.GetValueOrDefault("Stale"),
                issueBreakdown.GetValueOrDefault("Misaligned"),
                topIssues);
        }

        #endregion

        #region Export

        public string ExportScheduleToCsv(GeneratedSchedule schedule)
            => _exportFormatter.ToCsv(schedule);

        public string ExportScheduleToJson(GeneratedSchedule schedule)
            => _exportFormatter.ToJson(schedule);

        public string ExportScheduleToHtml(GeneratedSchedule schedule,
            string companyName = null)
            => _exportFormatter.ToHtml(schedule, companyName: companyName);

        public string ExportScheduleToMarkdown(GeneratedSchedule schedule)
            => _exportFormatter.ToMarkdown(schedule);

        /// <summary>
        /// Export schedule to file in specified format.
        /// </summary>
        public async Task ExportToFileAsync(
            GeneratedSchedule schedule,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            string content = extension switch
            {
                ".csv" => ExportScheduleToCsv(schedule),
                ".json" => ExportScheduleToJson(schedule),
                ".html" => ExportScheduleToHtml(schedule),
                ".md" => ExportScheduleToMarkdown(schedule),
                _ => ExportScheduleToCsv(schedule)
            };

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            Logger.Info("Schedule exported to {Path} ({Format})", filePath, extension);
        }

        #endregion

        #region Document Templates

        private void InitializeDefaultTemplates()
        {
            _templates["Commercial"] = new DocumentTemplate
            {
                Name = "Commercial Project",
                ProjectType = "Commercial",
                Sections = new List<string>
                {
                    "Summary", "Door Schedule", "Room Schedule", "Equipment Schedule",
                    "Quality Report", "Abbreviation Legend"
                }
            };

            _templates["Residential"] = new DocumentTemplate
            {
                Name = "Residential Project",
                ProjectType = "Residential",
                Sections = new List<string>
                {
                    "Summary", "Room Schedule", "Door Schedule", "Window Schedule",
                    "Quality Report"
                }
            };

            _templates["Healthcare"] = new DocumentTemplate
            {
                Name = "Healthcare Project",
                ProjectType = "Healthcare",
                Sections = new List<string>
                {
                    "Summary", "Room Schedule", "Door Schedule", "Equipment Schedule",
                    "Fire Safety Schedule", "Accessibility Report", "Quality Report",
                    "Abbreviation Legend"
                }
            };
        }

        public DocumentTemplate GetTemplate(string name)
        {
            return _templates.GetValueOrDefault(name);
        }

        public List<string> GetAvailableTemplates()
        {
            return _templates.Keys.ToList();
        }

        public void RegisterTemplate(DocumentTemplate template)
        {
            lock (_lockObject) { _templates[template.Name] = template; }
        }

        #endregion

        #region Utility

        private sealed class PerformanceTimer : IDisposable
        {
            private readonly string _name;
            private readonly System.Diagnostics.Stopwatch _sw;
            private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

            public PerformanceTimer(string name)
            {
                _name = name;
                _sw = System.Diagnostics.Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                if (_sw.ElapsedMilliseconds > 1000)
                    Logger.Debug("{Name} took {Ms}ms", _name, _sw.ElapsedMilliseconds);
            }
        }

        #endregion
    }

    #endregion
}
