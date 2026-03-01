// StingBIM.AI.UI.Services.ConversationExporter
// Exports chat conversations to various formats (HTML, PDF, Text)
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Exports chat conversations to various formats for documentation.
    /// </summary>
    public class ConversationExporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Exports a conversation to a file.
        /// </summary>
        public async Task<bool> ExportAsync(ConversationExportData data, ExportFormat format)
        {
            var filter = format switch
            {
                ExportFormat.Html => "HTML Files (*.html)|*.html",
                ExportFormat.Text => "Text Files (*.txt)|*.txt",
                ExportFormat.Markdown => "Markdown Files (*.md)|*.md",
                ExportFormat.Json => "JSON Files (*.json)|*.json",
                _ => "All Files (*.*)|*.*"
            };

            var extension = format switch
            {
                ExportFormat.Html => ".html",
                ExportFormat.Text => ".txt",
                ExportFormat.Markdown => ".md",
                ExportFormat.Json => ".json",
                _ => ".txt"
            };

            var dialog = new SaveFileDialog
            {
                Title = "Export Conversation",
                Filter = filter,
                FileName = $"StingBIM_Conversation_{DateTime.Now:yyyyMMdd_HHmmss}{extension}",
                DefaultExt = extension
            };

            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            try
            {
                var content = format switch
                {
                    ExportFormat.Html => GenerateHtml(data),
                    ExportFormat.Text => GenerateText(data),
                    ExportFormat.Markdown => GenerateMarkdown(data),
                    ExportFormat.Json => GenerateJson(data),
                    _ => GenerateText(data)
                };

                await File.WriteAllTextAsync(dialog.FileName, content, Encoding.UTF8);

                NotificationService.Instance.ShowSuccess("Exported", $"Conversation saved to {Path.GetFileName(dialog.FileName)}");
                Logger.Info($"Conversation exported to {dialog.FileName}");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export conversation");
                NotificationService.Instance.ShowError("Export Failed", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Exports directly to a specified path without dialog.
        /// </summary>
        public async Task<bool> ExportToPathAsync(ConversationExportData data, ExportFormat format, string path)
        {
            try
            {
                var content = format switch
                {
                    ExportFormat.Html => GenerateHtml(data),
                    ExportFormat.Text => GenerateText(data),
                    ExportFormat.Markdown => GenerateMarkdown(data),
                    ExportFormat.Json => GenerateJson(data),
                    _ => GenerateText(data)
                };

                await File.WriteAllTextAsync(path, content, Encoding.UTF8);
                Logger.Info($"Conversation exported to {path}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to export conversation to {path}");
                return false;
            }
        }

        #region HTML Export

        private static string GenerateHtml(ConversationExportData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>StingBIM Conversation - {data.Title}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetHtmlStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <header>");
            sb.AppendLine($"            <h1>{EscapeHtml(data.Title)}</h1>");
            sb.AppendLine($"            <p class=\"meta\">Exported on {DateTime.Now:MMMM d, yyyy 'at' h:mm tt}</p>");
            if (!string.IsNullOrEmpty(data.ProjectName))
            {
                sb.AppendLine($"            <p class=\"meta\">Project: {EscapeHtml(data.ProjectName)}</p>");
            }
            sb.AppendLine("        </header>");

            // Messages
            sb.AppendLine("        <div class=\"messages\">");
            foreach (var message in data.Messages)
            {
                var bubbleClass = message.IsUser ? "user" : (message.IsError ? "error" : "assistant");
                sb.AppendLine($"            <div class=\"message {bubbleClass}\">");
                sb.AppendLine($"                <div class=\"sender\">{(message.IsUser ? "You" : "StingBIM AI")}</div>");
                sb.AppendLine($"                <div class=\"content\">{EscapeHtml(message.Text)}</div>");
                sb.AppendLine($"                <div class=\"timestamp\">{message.Timestamp:HH:mm}</div>");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");

            // Footer
            sb.AppendLine("        <footer>");
            sb.AppendLine($"            <p>Generated by StingBIM AI Assistant</p>");
            sb.AppendLine($"            <p>{data.Messages.Count} messages</p>");
            sb.AppendLine("        </footer>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static string GetHtmlStyles()
        {
            return @"
        :root {
            --bg: #1a1a2e;
            --surface: #16213e;
            --card: #1f2b47;
            --accent: #0f3460;
            --text: #e4e4e4;
            --text-secondary: #a0a0a0;
            --user-bg: #3b82f6;
            --assistant-bg: #2d3748;
            --error-bg: #ef4444;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background-color: var(--bg);
            color: var(--text);
            line-height: 1.6;
        }
        .container {
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
        }
        header {
            text-align: center;
            padding: 30px 0;
            border-bottom: 1px solid var(--card);
            margin-bottom: 30px;
        }
        header h1 {
            font-size: 24px;
            margin-bottom: 10px;
        }
        .meta {
            color: var(--text-secondary);
            font-size: 14px;
        }
        .messages {
            display: flex;
            flex-direction: column;
            gap: 16px;
        }
        .message {
            max-width: 80%;
            padding: 12px 16px;
            border-radius: 16px;
        }
        .message.user {
            background-color: var(--user-bg);
            align-self: flex-end;
            border-bottom-right-radius: 4px;
        }
        .message.assistant {
            background-color: var(--assistant-bg);
            align-self: flex-start;
            border-bottom-left-radius: 4px;
        }
        .message.error {
            background-color: var(--error-bg);
            align-self: flex-start;
            border-bottom-left-radius: 4px;
        }
        .sender {
            font-size: 11px;
            font-weight: 600;
            opacity: 0.7;
            margin-bottom: 4px;
        }
        .content {
            white-space: pre-wrap;
            word-wrap: break-word;
        }
        .timestamp {
            font-size: 10px;
            opacity: 0.5;
            text-align: right;
            margin-top: 6px;
        }
        footer {
            text-align: center;
            padding: 30px 0;
            margin-top: 30px;
            border-top: 1px solid var(--card);
            color: var(--text-secondary);
            font-size: 12px;
        }
        @media print {
            body { background: white; color: black; }
            .message.user { background: #e3f2fd; }
            .message.assistant { background: #f5f5f5; }
            .message.error { background: #ffebee; }
        }";
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;")
                .Replace("\n", "<br>");
        }

        #endregion

        #region Text Export

        private static string GenerateText(ConversationExportData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  StingBIM AI Conversation: {data.Title}");
            sb.AppendLine($"  Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrEmpty(data.ProjectName))
            {
                sb.AppendLine($"  Project: {data.ProjectName}");
            }
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var message in data.Messages)
            {
                var sender = message.IsUser ? "YOU" : (message.IsError ? "ERROR" : "AI");
                sb.AppendLine($"[{message.Timestamp:HH:mm}] {sender}:");
                sb.AppendLine(message.Text);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  Total Messages: {data.Messages.Count}");
            sb.AppendLine("  Generated by StingBIM AI Assistant");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        #endregion

        #region Markdown Export

        private static string GenerateMarkdown(ConversationExportData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# {data.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Exported:** {DateTime.Now:MMMM d, yyyy 'at' h:mm tt}");
            if (!string.IsNullOrEmpty(data.ProjectName))
            {
                sb.AppendLine($"**Project:** {data.ProjectName}");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var message in data.Messages)
            {
                var sender = message.IsUser ? "**You**" : (message.IsError ? "**Error**" : "**StingBIM AI**");
                sb.AppendLine($"### {sender} *{message.Timestamp:HH:mm}*");
                sb.AppendLine();
                sb.AppendLine(message.Text);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*{data.Messages.Count} messages • Generated by StingBIM AI Assistant*");

            return sb.ToString();
        }

        #endregion

        #region JSON Export

        private static string GenerateJson(ConversationExportData data)
        {
            var exportObject = new
            {
                title = data.Title,
                projectName = data.ProjectName,
                exportedAt = DateTime.Now,
                messageCount = data.Messages.Count,
                messages = data.Messages.Select(m => new
                {
                    timestamp = m.Timestamp,
                    isUser = m.IsUser,
                    isError = m.IsError,
                    text = m.Text
                }).ToList()
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(exportObject, Newtonsoft.Json.Formatting.Indented);
        }

        #endregion
    }

    /// <summary>
    /// Data for exporting a conversation.
    /// </summary>
    public class ConversationExportData
    {
        public string Title { get; set; } = "StingBIM Conversation";
        public string ProjectName { get; set; }
        public List<ExportMessage> Messages { get; set; } = new List<ExportMessage>();
    }

    /// <summary>
    /// A message for export.
    /// </summary>
    public class ExportMessage
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; }
        public bool IsUser { get; set; }
        public bool IsError { get; set; }
    }

    /// <summary>
    /// Export format options.
    /// </summary>
    public enum ExportFormat
    {
        Html,
        Text,
        Markdown,
        Json
    }
}
