// StingBIM.AI.UI.Controls.MarkdownTextBlock
// TextBlock that renders basic markdown formatting
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// A TextBlock-like control that renders basic markdown formatting.
    /// Supports: **bold**, *italic*, `code`, [links](url), - lists, # headers
    /// </summary>
    public class MarkdownTextBlock : TextBlock
    {
        #region Dependency Properties

        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.Register(
                nameof(MarkdownText),
                typeof(string),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public static readonly DependencyProperty CodeBackgroundProperty =
            DependencyProperty.Register(
                nameof(CodeBackground),
                typeof(Brush),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0))));

        public static readonly DependencyProperty CodeForegroundProperty =
            DependencyProperty.Register(
                nameof(CodeForeground),
                typeof(Brush),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(null));

        public static readonly DependencyProperty LinkForegroundProperty =
            DependencyProperty.Register(
                nameof(LinkForeground),
                typeof(Brush),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(59, 130, 246))));

        #endregion

        #region Properties

        /// <summary>
        /// The markdown text to render.
        /// </summary>
        public string MarkdownText
        {
            get => (string)GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        /// <summary>
        /// Background color for inline code.
        /// </summary>
        public Brush CodeBackground
        {
            get => (Brush)GetValue(CodeBackgroundProperty);
            set => SetValue(CodeBackgroundProperty, value);
        }

        /// <summary>
        /// Foreground color for inline code.
        /// </summary>
        public Brush CodeForeground
        {
            get => (Brush)GetValue(CodeForegroundProperty);
            set => SetValue(CodeForegroundProperty, value);
        }

        /// <summary>
        /// Foreground color for links.
        /// </summary>
        public Brush LinkForeground
        {
            get => (Brush)GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        #endregion

        /// <summary>
        /// Event fired when a link is clicked.
        /// </summary>
        public event EventHandler<string> LinkClicked;

        public MarkdownTextBlock()
        {
            TextWrapping = TextWrapping.Wrap;
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock control)
            {
                control.RenderMarkdown();
            }
        }

        private void RenderMarkdown()
        {
            Inlines.Clear();

            if (string.IsNullOrEmpty(MarkdownText))
                return;

            var lines = MarkdownText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Handle headers
                if (line.StartsWith("### "))
                {
                    AddHeader(line.Substring(4), 3);
                }
                else if (line.StartsWith("## "))
                {
                    AddHeader(line.Substring(3), 2);
                }
                else if (line.StartsWith("# "))
                {
                    AddHeader(line.Substring(2), 1);
                }
                // Handle bullet points
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var content = line.TrimStart().Substring(2);
                    AddBulletPoint(content, indent);
                }
                // Handle numbered lists
                else if (Regex.IsMatch(line.TrimStart(), @"^\d+\. "))
                {
                    var match = Regex.Match(line.TrimStart(), @"^(\d+)\. (.*)$");
                    if (match.Success)
                    {
                        AddNumberedItem(match.Groups[1].Value, match.Groups[2].Value);
                    }
                }
                // Handle code blocks (simple single-line)
                else if (line.StartsWith("```") || line.StartsWith("    "))
                {
                    AddCodeLine(line.StartsWith("```") ? "" : line.Substring(4));
                }
                // Regular paragraph
                else
                {
                    ParseInlineFormatting(line);
                }

                // Add line break between lines (except last)
                if (i < lines.Length - 1)
                {
                    Inlines.Add(new LineBreak());
                }
            }
        }

        private void AddHeader(string text, int level)
        {
            var run = new Run(text)
            {
                FontWeight = FontWeights.Bold,
                FontSize = level switch
                {
                    1 => FontSize + 6,
                    2 => FontSize + 4,
                    3 => FontSize + 2,
                    _ => FontSize
                }
            };
            Inlines.Add(run);
        }

        private void AddBulletPoint(string content, int indent)
        {
            // Add indent
            if (indent > 0)
            {
                Inlines.Add(new Run(new string(' ', indent)));
            }

            Inlines.Add(new Run("• "));
            ParseInlineFormatting(content);
        }

        private void AddNumberedItem(string number, string content)
        {
            Inlines.Add(new Run($"{number}. "));
            ParseInlineFormatting(content);
        }

        private void AddCodeLine(string code)
        {
            var run = new Run(code)
            {
                FontFamily = new FontFamily("Consolas"),
                Background = CodeBackground,
                Foreground = CodeForeground ?? Foreground
            };
            Inlines.Add(run);
        }

        private void ParseInlineFormatting(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Pattern matching for inline formatting
            // Order matters: check bold before italic
            var patterns = new List<(string pattern, Func<string, Inline> formatter)>
            {
                // Bold **text**
                (@"\*\*(.+?)\*\*", m => new Bold(new Run(m))),
                // Italic *text* (but not ** which is bold)
                (@"(?<!\*)\*([^\*]+?)\*(?!\*)", m => new Italic(new Run(m))),
                // Inline code `code`
                (@"`(.+?)`", m => CreateCodeInline(m)),
                // Links [text](url)
                (@"\[(.+?)\]\((.+?)\)", m => CreateLinkInline(m)),
            };

            var remaining = text;
            var position = 0;

            while (position < remaining.Length)
            {
                Match earliestMatch = null;
                int earliestIndex = int.MaxValue;
                Func<string, Inline> matchedFormatter = null;
                string matchedPattern = null;

                // Find the earliest match among all patterns
                foreach (var (pattern, formatter) in patterns)
                {
                    var match = Regex.Match(remaining.Substring(position), pattern);
                    if (match.Success && match.Index < earliestIndex)
                    {
                        earliestMatch = match;
                        earliestIndex = match.Index;
                        matchedFormatter = formatter;
                        matchedPattern = pattern;
                    }
                }

                if (earliestMatch != null && matchedFormatter != null)
                {
                    // Add text before the match
                    if (earliestIndex > 0)
                    {
                        Inlines.Add(new Run(remaining.Substring(position, earliestIndex)));
                    }

                    // Handle link pattern specially (has 2 groups)
                    if (matchedPattern.Contains("\\]\\("))
                    {
                        var linkMatch = Regex.Match(remaining.Substring(position), matchedPattern);
                        if (linkMatch.Success && linkMatch.Groups.Count >= 3)
                        {
                            Inlines.Add(CreateLinkInline(linkMatch.Groups[1].Value, linkMatch.Groups[2].Value));
                        }
                    }
                    else
                    {
                        // Add formatted content
                        Inlines.Add(matchedFormatter(earliestMatch.Groups[1].Value));
                    }

                    position += earliestIndex + earliestMatch.Length;
                }
                else
                {
                    // No more matches, add remaining text
                    Inlines.Add(new Run(remaining.Substring(position)));
                    break;
                }
            }
        }

        private Inline CreateCodeInline(string code)
        {
            var border = new Border
            {
                Background = CodeBackground,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Child = new TextBlock
                {
                    Text = code,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = CodeForeground ?? Foreground,
                    FontSize = FontSize - 1
                }
            };

            return new InlineUIContainer(border);
        }

        private Inline CreateLinkInline(string combined)
        {
            // For simple text formatting, just show the text
            return new Run(combined)
            {
                Foreground = LinkForeground,
                TextDecorations = System.Windows.TextDecorations.Underline
            };
        }

        private Inline CreateLinkInline(string text, string url)
        {
            var hyperlink = new Hyperlink(new Run(text))
            {
                Foreground = LinkForeground,
                TextDecorations = null
            };

            hyperlink.Click += (s, e) =>
            {
                LinkClicked?.Invoke(this, url);

                // Optionally open URL
                try
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = uri.AbsoluteUri,
                            UseShellExecute = true
                        });
                    }
                }
                catch
                {
                    // Ignore URL open failures
                }
            };

            return hyperlink;
        }
    }
}
