// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagNLPCommandProcessor.cs - Natural language command parsing for tagging operations
// Integrates with StingBIM.AI.NLP patterns for intent classification and entity extraction
//
// Parses commands like:
//   "Tag all doors on Level 2"            -> SelectByCategory + SelectSameLevel + PlaceTags
//   "Tag fire-rated doors with fire tag"  -> SelectByParameter(Fire_Rating) + template
//   "Remove duplicate tags in this view"  -> FixDuplicates
//   "Align all door tags horizontally"    -> SelectTagsByCategory + AlignHorizontal
//   "Show untagged electrical equipment"  -> SelectUntagged + filterByCategory
//   "Tag typical air terminals with count"-> ClusterDetector + TagWithCount
//   "Run quality check on all sheets"     -> AnalyzeAsync(AllPlacedViews)
//   "Export tag inventory to Excel"       -> ExportInventoryToCsvAsync
//
// Works fully offline with keyword-weighted intent classification (no ML dependency).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;
using StingBIM.AI.Tagging.Rules;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Enumerations

    /// <summary>Entity types extractable from natural language tagging commands.</summary>
    public enum TagEntityType
    {
        Category, Level, Parameter, ParameterValue, TagType,
        ViewName, Count, Direction, Distance, Standard, RuleGroup
    }

    /// <summary>High-level intent categories for tagging natural language commands.</summary>
    public enum TagCommandIntent
    {
        Tag, Untag, Select, Align, Distribute, Check, Export,
        Configure, Optimize, Find, ChangeTemplate, Fix, Undo, Redo, Help, Unknown
    }

    /// <summary>Scope of the view(s) a command should apply to.</summary>
    public enum CommandViewScope
    {
        ActiveView, NamedView, ActiveSheet, AllSheets, AllViews
    }

    #endregion

    #region Data Transfer Objects

    /// <summary>
    /// An entity extracted from natural language input with its resolved value,
    /// type classification, and extraction confidence.
    /// </summary>
    public class TagExtractedEntity
    {
        /// <summary>Classified type of this entity.</summary>
        public TagEntityType Type { get; set; }

        /// <summary>Raw text as it appeared in the input.</summary>
        public string RawValue { get; set; }

        /// <summary>Normalized/resolved value suitable for API consumption.</summary>
        public string NormalizedValue { get; set; }

        /// <summary>Confidence score (0.0 to 1.0) of the extraction.</summary>
        public float Confidence { get; set; }

        /// <summary>Start character index in the original input string.</summary>
        public int StartIndex { get; set; }

        /// <summary>End character index (exclusive) in the original input string.</summary>
        public int EndIndex { get; set; }

        public override string ToString() =>
            $"[{Type}] \"{RawValue}\" -> \"{NormalizedValue}\" ({Confidence:P0})";
    }

    /// <summary>
    /// Ranked match result from intent classification.
    /// Includes the matched pattern name and individual keyword score contributions.
    /// </summary>
    public class TagIntentMatch
    {
        /// <summary>Classified intent.</summary>
        public TagCommandIntent Intent { get; set; }

        /// <summary>Confidence score (0.0 to 1.0).</summary>
        public float Confidence { get; set; }

        /// <summary>Name of the pattern that matched, if pattern-based.</summary>
        public string MatchedPatternName { get; set; }

        /// <summary>Individual keyword scores contributing to this match.</summary>
        public Dictionary<string, float> KeywordScores { get; set; } = new Dictionary<string, float>();

        public override string ToString() =>
            $"{Intent} ({Confidence:P0}) via {MatchedPatternName ?? "aggregate"}";
    }

    /// <summary>
    /// A fully parsed tagging command ready for execution by the tagging engine.
    /// Contains the action, target selection, template, view scope, and options.
    /// </summary>
    public class TaggingCommand
    {
        /// <summary>Primary action to perform.</summary>
        public TagCommandIntent Action { get; set; }

        /// <summary>Target selection criteria (category, level, parameter filters).</summary>
        public TargetSelection TargetSelection { get; set; }

        /// <summary>Tag template or tag type to use (null for auto-selection).</summary>
        public string TagTemplateName { get; set; }

        /// <summary>View scope for the command.</summary>
        public CommandViewScope ViewScope { get; set; } = CommandViewScope.ActiveView;

        /// <summary>Named view if ViewScope is NamedView.</summary>
        public string ViewName { get; set; }

        /// <summary>Additional options extracted from the command.</summary>
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();

        /// <summary>Overall confidence in the parsed command (0.0 to 1.0).</summary>
        public float Confidence { get; set; }

        /// <summary>Whether the command is complete enough to execute.</summary>
        public bool IsComplete { get; set; }

        /// <summary>If not complete, what additional information is needed.</summary>
        public List<string> MissingInfo { get; set; } = new List<string>();

        /// <summary>Human-readable summary of what this command will do.</summary>
        public string Summary { get; set; }

        /// <summary>The original natural language input that produced this command.</summary>
        public string OriginalInput { get; set; }

        /// <summary>Time taken to parse the command in milliseconds.</summary>
        public double ParseTimeMs { get; set; }
    }

    /// <summary>
    /// Element selection criteria assembled from the parsed command.
    /// Drives the TagSelectionEngine to filter elements for the operation.
    /// </summary>
    public class TargetSelection
    {
        /// <summary>Category name to filter by (null for all).</summary>
        public string CategoryName { get; set; }

        /// <summary>Level name to filter by (null for all).</summary>
        public string LevelName { get; set; }

        /// <summary>Parameter filters to apply.</summary>
        public List<ParameterFilter> ParameterFilters { get; set; } = new List<ParameterFilter>();

        /// <summary>Whether to target untagged elements only.</summary>
        public bool UntaggedOnly { get; set; }

        /// <summary>Whether to target existing tags rather than elements.</summary>
        public bool TargetTags { get; set; }

        /// <summary>Specific element IDs to target (empty for filter-based selection).</summary>
        public List<int> SpecificElementIds { get; set; } = new List<int>();

        /// <summary>Whether to include elements from linked models.</summary>
        public bool IncludeLinks { get; set; }

        /// <summary>Cluster tag strategy if applicable.</summary>
        public ClusterTagStrategy? ClusterStrategy { get; set; }
    }

    /// <summary>
    /// Parameter-based filter condition extracted from a natural language command.
    /// Maps to a <see cref="RuleCondition"/> for rule engine evaluation.
    /// </summary>
    public class ParameterFilter
    {
        /// <summary>Parameter name to evaluate.</summary>
        public string ParameterName { get; set; }

        /// <summary>Comparison operator.</summary>
        public RuleOperator Operator { get; set; }

        /// <summary>Value to compare against (null for IsNull/IsNotNull operators).</summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// Autocomplete / next-action suggestion based on context.
    /// Returned by <see cref="TagNLPCommandProcessor.GetSuggestions"/>.
    /// </summary>
    public class CommandSuggestion
    {
        /// <summary>The suggested command text.</summary>
        public string CommandText { get; set; }

        /// <summary>Human-readable description of what the suggestion does.</summary>
        public string Description { get; set; }

        /// <summary>Relevance score (0.0 to 1.0).</summary>
        public float Relevance { get; set; }

        /// <summary>Category of this suggestion (e.g., "follow-up", "common", "autocomplete").</summary>
        public string Category { get; set; }
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// Weighted keyword pattern for offline intent classification.
    /// Required keywords use AND logic; optional keywords boost score additively.
    /// Negative keywords disqualify the pattern entirely.
    /// </summary>
    internal class IntentKeywordPattern
    {
        /// <summary>Target intent this pattern matches.</summary>
        public TagCommandIntent Intent;

        /// <summary>Pattern name for diagnostics.</summary>
        public string Name;

        /// <summary>Keywords that must all appear (AND logic).</summary>
        public string[] RequiredKeywords;

        /// <summary>Keywords that boost the score if present.</summary>
        public string[] OptionalKeywords;

        /// <summary>Keywords that disqualify this pattern if present.</summary>
        public string[] NegativeKeywords;

        /// <summary>Base weight when all required keywords match.</summary>
        public float BaseWeight;

        /// <summary>Bonus weight per optional keyword found.</summary>
        public float OptionalBonus;
    }

    /// <summary>Record of an executed command for history and undo support.</summary>
    internal class CommandHistoryEntry
    {
        public string CommandText;
        public TaggingCommand ParsedCommand;
        public DateTime ExecutedAt;
        public bool WasSuccessful;
        public string ResultSummary;
    }

    #endregion

    /// <summary>
    /// Parses natural language commands into structured tagging operations.
    /// Uses keyword-weighted intent classification and regex-based entity extraction.
    /// Works fully offline with 40+ intent patterns and comprehensive BIM vocabulary.
    /// </summary>
    public class TagNLPCommandProcessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<IntentKeywordPattern> _intentPatterns;
        private readonly Dictionary<string, string> _categoryVocab;   // colloquial -> canonical
        private readonly Dictionary<string, string> _parameterVocab;
        private readonly Dictionary<string, string> _tagTypeVocab;
        private readonly Dictionary<string, string> _levelVocab;
        private readonly Dictionary<TagEntityType, List<Regex>> _entityPatterns;
        private readonly List<CommandHistoryEntry> _history;
        private readonly Dictionary<TagCommandIntent, List<CommandSuggestion>> _followUps;
        private readonly object _historyLock = new object();
        private const int MaxHistory = 100;
        private const float ConfidenceThreshold = 0.35f;

        public TagNLPCommandProcessor()
        {
            _intentPatterns = new List<IntentKeywordPattern>();
            _categoryVocab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _parameterVocab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _tagTypeVocab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _levelVocab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _entityPatterns = new Dictionary<TagEntityType, List<Regex>>();
            _history = new List<CommandHistoryEntry>();
            _followUps = new Dictionary<TagCommandIntent, List<CommandSuggestion>>();

            InitializeIntentPatterns();
            InitializeBIMVocabulary();
            InitializeEntityPatterns();
            InitializeFollowUpSuggestions();

            Logger.Info($"TagNLPCommandProcessor initialized: {_intentPatterns.Count} patterns, " +
                        $"{_categoryVocab.Count} category terms, {_parameterVocab.Count} param terms");
        }

        // ============================= PUBLIC API =============================

        /// <summary>Parses natural language input into a structured TaggingCommand.</summary>
        public TaggingCommand ParseCommand(string naturalLanguageInput)
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageInput))
                return MakeError("No input provided. Please describe what you would like to do.", "", 0f);

            var start = DateTime.UtcNow;
            var input = naturalLanguageInput.Trim();
            Logger.Debug($"Parsing command: \"{input}\"");

            // Step 1: Shortcuts (undo, redo, help, repeat)
            var shortcut = TryParseShortcut(input);
            if (shortcut != null) { shortcut.ParseTimeMs = Ms(start); return shortcut; }

            // Step 2: Classify intent
            var intents = ClassifyIntent(input);
            var best = intents.FirstOrDefault();
            if (best == null || best.Confidence < ConfidenceThreshold)
                return MakeError(
                    "Could not understand that command. Try: \"Tag all doors on Level 1\", " +
                    "\"Remove duplicate tags\", \"Align room tags horizontally\".",
                    input, best?.Confidence ?? 0f, Ms(start));

            // Step 3: Extract entities
            var entities = ExtractEntities(input);

            // Step 4: Build command
            var cmd = BuildCommand(best, entities, input);
            cmd.ParseTimeMs = Ms(start);
            Logger.Info($"Parsed: {cmd.Action} confidence={cmd.Confidence:P0} complete={cmd.IsComplete} {cmd.ParseTimeMs:F1}ms");
            return cmd;
        }

        /// <summary>Classifies intent using weighted keyword matching across 40+ patterns.</summary>
        public List<TagIntentMatch> ClassifyIntent(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<TagIntentMatch>();
            var norm = input.ToLowerInvariant().Trim();
            var scores = new Dictionary<TagCommandIntent, TagIntentMatch>();

            foreach (var p in _intentPatterns)
            {
                if (p.NegativeKeywords != null && p.NegativeKeywords.Any(nk => norm.Contains(nk, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (p.RequiredKeywords != null && !p.RequiredKeywords.All(rk => norm.Contains(rk, StringComparison.OrdinalIgnoreCase)))
                    continue;

                float score = p.BaseWeight;
                var kwScores = new Dictionary<string, float>();
                if (p.RequiredKeywords != null)
                    foreach (var rk in p.RequiredKeywords)
                        kwScores[rk] = p.BaseWeight / p.RequiredKeywords.Length;
                if (p.OptionalKeywords != null)
                    foreach (var ok in p.OptionalKeywords.Where(ok => norm.Contains(ok, StringComparison.OrdinalIgnoreCase)))
                    { score += p.OptionalBonus; kwScores[ok] = p.OptionalBonus; }

                if (!scores.TryGetValue(p.Intent, out var ex) || ex.Confidence < score)
                    scores[p.Intent] = new TagIntentMatch
                    { Intent = p.Intent, Confidence = Math.Min(score, 1f), MatchedPatternName = p.Name, KeywordScores = kwScores };
            }

            var results = scores.Values.OrderByDescending(m => m.Confidence).ToList();
            if (results.Count > 0 && results[0].Confidence > 1f)
            { float n = 1f / results[0].Confidence; foreach (var r in results) r.Confidence *= n; }
            return results;
        }

        /// <summary>Extracts entities using regex patterns and BIM vocabulary lookup.</summary>
        public List<TagExtractedEntity> ExtractEntities(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<TagExtractedEntity>();
            var entities = new List<TagExtractedEntity>();

            // Regex-based extraction
            foreach (var (type, patterns) in _entityPatterns)
                foreach (var pat in patterns)
                    foreach (Match m in pat.Matches(input))
                        entities.Add(new TagExtractedEntity
                        { Type = type, RawValue = m.Value.Trim(), NormalizedValue = NormalizeEntity(type, m.Value.Trim()),
                          Confidence = 0.85f, StartIndex = m.Index, EndIndex = m.Index + m.Length });

            // Vocabulary-based extraction
            entities.AddRange(VocabScan(input, _categoryVocab, TagEntityType.Category, 0.90f));
            entities.AddRange(VocabScan(input, _parameterVocab, TagEntityType.Parameter, 0.85f));
            entities.AddRange(VocabScan(input, _tagTypeVocab, TagEntityType.TagType, 0.88f));
            entities.AddRange(VocabScan(input, _levelVocab, TagEntityType.Level, 0.92f));

            // Resolve overlaps
            var sorted = entities.OrderBy(e => e.StartIndex).ThenByDescending(e => e.Confidence)
                .ThenByDescending(e => e.EndIndex - e.StartIndex).ToList();
            var result = new List<TagExtractedEntity>();
            foreach (var e in sorted)
                if (!result.Any(x => e.StartIndex < x.EndIndex && e.EndIndex > x.StartIndex))
                    result.Add(e);

            Logger.Debug($"Extracted {result.Count} entities from: \"{input}\"");
            return result;
        }

        /// <summary>Returns suggestions based on partial input and recent command context.</summary>
        public List<CommandSuggestion> GetSuggestions(string partialInput)
        {
            var sugs = new List<CommandSuggestion>();
            if (!string.IsNullOrWhiteSpace(partialInput))
            {
                var lo = partialInput.ToLowerInvariant().Trim();
                foreach (var (cmd, desc) in _suggestionTemplates)
                    if (cmd.Contains(lo, StringComparison.OrdinalIgnoreCase))
                        sugs.Add(new CommandSuggestion { CommandText = cmd, Description = desc,
                            Relevance = cmd.StartsWith(lo, StringComparison.OrdinalIgnoreCase) ? 0.95f : 0.70f, Category = "autocomplete" });
            }

            lock (_historyLock)
            {
                var last = _history.LastOrDefault(h => h.WasSuccessful);
                if (last != null && _followUps.TryGetValue(last.ParsedCommand.Action, out var fu))
                    foreach (var f in fu.Where(f => !sugs.Any(s => s.CommandText == f.CommandText)))
                        sugs.Add(new CommandSuggestion { CommandText = f.CommandText, Description = f.Description,
                            Relevance = f.Relevance * 0.9f, Category = "follow-up" });
            }

            if (sugs.Count == 0)
                sugs.AddRange(new[]
                {
                    new CommandSuggestion { CommandText = "Tag all doors", Description = "Tag every door in the active view", Relevance = 0.80f, Category = "common" },
                    new CommandSuggestion { CommandText = "Tag all rooms", Description = "Tag every room in the active view", Relevance = 0.78f, Category = "common" },
                    new CommandSuggestion { CommandText = "Run quality check", Description = "Analyze tags for quality issues", Relevance = 0.75f, Category = "common" },
                    new CommandSuggestion { CommandText = "Show untagged elements", Description = "Find elements missing tags", Relevance = 0.72f, Category = "common" },
                    new CommandSuggestion { CommandText = "Export tag inventory to Excel", Description = "Export tag inventory report", Relevance = 0.65f, Category = "common" },
                });

            return sugs.OrderByDescending(s => s.Relevance).Take(10).ToList();
        }

        /// <summary>Returns recent command history entries, most recent first.</summary>
        public List<TaggingCommand> GetCommandHistory(int count = 10)
        {
            lock (_historyLock)
                return _history.OrderByDescending(h => h.ExecutedAt).Take(Math.Clamp(count, 1, MaxHistory))
                    .Select(h => h.ParsedCommand).ToList();
        }

        /// <summary>Records a command execution result for history and suggestion context.</summary>
        public void RecordExecution(TaggingCommand command, bool wasSuccessful, string resultSummary = null)
        {
            if (command == null) return;
            lock (_historyLock)
            {
                _history.Add(new CommandHistoryEntry { CommandText = command.OriginalInput, ParsedCommand = command,
                    ExecutedAt = DateTime.UtcNow, WasSuccessful = wasSuccessful,
                    ResultSummary = resultSummary ?? (wasSuccessful ? "Completed" : "Failed") });
                while (_history.Count > MaxHistory) _history.RemoveAt(0);
            }
        }

        // ============================= SHORTCUTS =============================

        private TaggingCommand TryParseShortcut(string input)
        {
            var lo = input.ToLowerInvariant().Trim();
            if (Regex.IsMatch(lo, @"^(undo|undo\s+last|revert)$"))
                return MakeOk(TagCommandIntent.Undo, input, "Undo the last tagging operation");
            if (Regex.IsMatch(lo, @"^(redo|redo\s+last)$"))
                return MakeOk(TagCommandIntent.Redo, input, "Redo the previously undone operation");
            if (Regex.IsMatch(lo, @"^(repeat|again|same\s+again|do\s+it\s+again)$"))
            {
                lock (_historyLock) { var l = _history.LastOrDefault(h => h.WasSuccessful);
                    if (l != null) { l.ParsedCommand.Summary = $"Repeat: {l.ParsedCommand.Summary}"; return l.ParsedCommand; } }
                return MakeError("No previous command to repeat.", input, 0f);
            }
            if (Regex.IsMatch(lo, @"^(help|commands|\?|what\s+can\s+you\s+do)$"))
                return MakeOk(TagCommandIntent.Help, input, "Show available tagging commands");
            return null;
        }

        // ========================= COMMAND BUILDER ===========================

        private TaggingCommand BuildCommand(TagIntentMatch intent, List<TagExtractedEntity> entities, string input)
        {
            var cmd = new TaggingCommand { Action = intent.Intent, Confidence = intent.Confidence,
                OriginalInput = input, TargetSelection = new TargetSelection() };
            var lo = input.ToLowerInvariant();

            // Populate target selection from entities
            var catE = entities.FirstOrDefault(e => e.Type == TagEntityType.Category);
            if (catE != null) cmd.TargetSelection.CategoryName = catE.NormalizedValue;
            var lvlE = entities.FirstOrDefault(e => e.Type == TagEntityType.Level);
            if (lvlE != null) cmd.TargetSelection.LevelName = lvlE.NormalizedValue;
            var tagE = entities.FirstOrDefault(e => e.Type == TagEntityType.TagType);
            if (tagE != null) cmd.TagTemplateName = tagE.NormalizedValue;
            var viewE = entities.FirstOrDefault(e => e.Type == TagEntityType.ViewName);
            if (viewE != null) cmd.ViewName = viewE.NormalizedValue;

            // Parameter filters
            var parms = entities.Where(e => e.Type == TagEntityType.Parameter).ToList();
            var vals = entities.Where(e => e.Type == TagEntityType.ParameterValue).ToList();
            for (int i = 0; i < parms.Count; i++)
                cmd.TargetSelection.ParameterFilters.Add(new ParameterFilter
                { ParameterName = parms[i].NormalizedValue,
                  Operator = i < vals.Count ? RuleOperator.Equals : RuleOperator.IsNotNull,
                  Value = i < vals.Count ? vals[i].NormalizedValue : null });

            // Flags from input text
            if (lo.Contains("untagged") || lo.Contains("not tagged") || lo.Contains("missing tag") || lo.Contains("without tag"))
                cmd.TargetSelection.UntaggedOnly = true;
            if (lo.Contains("linked") || lo.Contains("external"))
                cmd.TargetSelection.IncludeLinks = true;
            if (lo.Contains("typical")) cmd.TargetSelection.ClusterStrategy = ClusterTagStrategy.TagTypical;
            else if (Regex.IsMatch(lo, @"\bwith\s+count\b")) cmd.TargetSelection.ClusterStrategy = ClusterTagStrategy.TagWithCount;
            else if (lo.Contains("group")) cmd.TargetSelection.ClusterStrategy = ClusterTagStrategy.TagGrouped;

            // View scope
            cmd.ViewScope = Regex.IsMatch(lo, @"\ball\s+sheets\b") ? CommandViewScope.AllSheets
                : Regex.IsMatch(lo, @"\ball\s+views\b|\bentire\s+project\b") ? CommandViewScope.AllViews
                : Regex.IsMatch(lo, @"\b(this|current|active)\s+sheet\b") ? CommandViewScope.ActiveSheet
                : viewE != null ? CommandViewScope.NamedView : CommandViewScope.ActiveView;

            // Intent-specific enrichment
            switch (intent.Intent)
            {
                case TagCommandIntent.Tag:
                    if (lo.Contains("fire") && (lo.Contains("rate") || lo.Contains("rating")))
                    { if (!cmd.TargetSelection.ParameterFilters.Any(f => f.ParameterName == "Fire_Rating"))
                          cmd.TargetSelection.ParameterFilters.Add(new ParameterFilter { ParameterName = "Fire_Rating", Operator = RuleOperator.IsNotNull });
                      if (string.IsNullOrEmpty(cmd.TagTemplateName)) cmd.TagTemplateName = "Doors_Fire_Rated"; }
                    if (Regex.IsMatch(lo, @"\btag\s+all\b") && cmd.TargetSelection.CategoryName == null) cmd.Options["tag_all"] = true;
                    if (lo.Contains("replace") || lo.Contains("retag")) cmd.Options["replace_existing"] = true;
                    if (lo.Contains("only untagged") || lo.Contains("skip existing")) cmd.TargetSelection.UntaggedOnly = true;
                    break;

                case TagCommandIntent.Untag:
                    cmd.TargetSelection.TargetTags = true;
                    if (lo.Contains("duplicate")) { cmd.Action = TagCommandIntent.Fix; cmd.Options["fix_type"] = "duplicates"; }
                    else if (lo.Contains("orphan")) { cmd.Action = TagCommandIntent.Fix; cmd.Options["fix_type"] = "orphans"; }
                    else if (lo.Contains("all")) cmd.Options["remove_all"] = true;
                    break;

                case TagCommandIntent.Select: case TagCommandIntent.Find:
                    if (lo.Contains("tag")) cmd.TargetSelection.TargetTags = true;
                    break;

                case TagCommandIntent.Align:
                    cmd.TargetSelection.TargetTags = true;
                    var dirE = entities.FirstOrDefault(e => e.Type == TagEntityType.Direction);
                    cmd.Options["direction"] = dirE?.NormalizedValue
                        ?? (lo.Contains("horizontal") ? "horizontal" : lo.Contains("vertical") ? "vertical"
                        : lo.Contains("left") ? "left" : lo.Contains("right") ? "right"
                        : lo.Contains("top") ? "top" : lo.Contains("bottom") ? "bottom" : "auto");
                    break;

                case TagCommandIntent.Distribute:
                    cmd.TargetSelection.TargetTags = true;
                    cmd.Options["distribution"] = lo.Contains("even") || lo.Contains("equal") ? "equal_spacing" : "auto";
                    if (lo.Contains("horizontal")) cmd.Options["direction"] = "horizontal";
                    else if (lo.Contains("vertical")) cmd.Options["direction"] = "vertical";
                    break;

                case TagCommandIntent.Check:
                    var checks = new List<string>();
                    if (lo.Contains("clash") || lo.Contains("overlap")) checks.Add("clash");
                    if (lo.Contains("orphan")) checks.Add("orphan");
                    if (lo.Contains("duplicate")) checks.Add("duplicate");
                    if (lo.Contains("blank") || lo.Contains("empty")) checks.Add("blank");
                    if (lo.Contains("misalign")) checks.Add("misaligned");
                    if (lo.Contains("stale") || lo.Contains("outdated")) checks.Add("stale");
                    cmd.Options["check_types"] = checks.Count > 0 ? (object)checks : "all";
                    break;

                case TagCommandIntent.Export:
                    cmd.Options["format"] = lo.Contains("excel") || lo.Contains("xlsx") ? "xlsx" : lo.Contains("json") ? "json" : "csv";
                    cmd.Options["export_type"] = lo.Contains("quality") || lo.Contains("report") ? "quality_report"
                        : lo.Contains("rule") ? "rules" : "inventory";
                    break;

                case TagCommandIntent.Configure:
                    cmd.Options["config_target"] = lo.Contains("leader") ? "leader" : lo.Contains("offset") ? "offset"
                        : lo.Contains("alignment") ? "alignment" : lo.Contains("strategy") ? "strategy" : "general";
                    var distE = entities.FirstOrDefault(e => e.Type == TagEntityType.Distance);
                    if (distE != null) cmd.Options["config_value"] = distE.NormalizedValue;
                    break;

                case TagCommandIntent.Optimize:
                    cmd.TargetSelection.TargetTags = true;
                    cmd.Options["optimize_target"] = lo.Contains("leader") ? "leaders"
                        : lo.Contains("readab") ? "readability" : lo.Contains("layout") ? "layout" : "all";
                    break;

                case TagCommandIntent.ChangeTemplate:
                    cmd.TargetSelection.TargetTags = true;
                    var showM = Regex.Match(lo, @"\bshow\s+(\w+(?:\s+\w+)?)\b");
                    if (showM.Success)
                    { cmd.Options["display_parameter"] = showM.Groups[1].Value;
                      cmd.TagTemplateName = $"{cmd.TargetSelection.CategoryName ?? ""}_{showM.Groups[1].Value.Replace(" ", "_")}".Trim('_'); }
                    break;

                case TagCommandIntent.Fix:
                    cmd.Options["fix_type"] = lo.Contains("duplicate") ? "duplicates" : lo.Contains("orphan") ? "orphans"
                        : lo.Contains("clash") || lo.Contains("overlap") ? "clashes" : lo.Contains("blank") ? "blanks"
                        : lo.Contains("misalign") ? "misalignment" : lo.Contains("stale") || lo.Contains("refresh") ? "stale" : "all";
                    break;
            }

            // Validate completeness
            cmd.IsComplete = true;
            cmd.MissingInfo = new List<string>();
            switch (cmd.Action)
            {
                case TagCommandIntent.Tag:
                    if (cmd.TargetSelection.CategoryName == null && !cmd.Options.ContainsKey("tag_all") && cmd.TargetSelection.ParameterFilters.Count == 0)
                    { cmd.IsComplete = false; cmd.MissingInfo.Add("Which elements? Specify a category (e.g. \"doors\") or say \"tag all\"."); }
                    break;
                case TagCommandIntent.Untag:
                    if (cmd.TargetSelection.CategoryName == null && !cmd.Options.ContainsKey("remove_all") && !cmd.Options.ContainsKey("fix_type"))
                    { cmd.IsComplete = false; cmd.MissingInfo.Add("Which tags to remove? Specify a category or \"remove all\"."); }
                    break;
                case TagCommandIntent.Align:
                    if (!cmd.Options.ContainsKey("direction"))
                    { cmd.IsComplete = false; cmd.MissingInfo.Add("Which direction? Say \"horizontally\" or \"vertically\"."); }
                    break;
                case TagCommandIntent.ChangeTemplate:
                    if (string.IsNullOrEmpty(cmd.TagTemplateName) && !cmd.Options.ContainsKey("display_parameter"))
                    { cmd.IsComplete = false; cmd.MissingInfo.Add("What should tags display? E.g. \"show area\", \"show fire rating\"."); }
                    break;
                case TagCommandIntent.Configure:
                    if (!cmd.Options.ContainsKey("config_target") || cmd.Options["config_target"].ToString() == "general")
                    { cmd.IsComplete = false; cmd.MissingInfo.Add("What to configure? Options: leader, offset, alignment, strategy."); }
                    break;
                case TagCommandIntent.Unknown:
                    cmd.IsComplete = false; break;
            }

            cmd.Summary = GenerateSummary(cmd);
            return cmd;
        }

        // ========================= SUMMARY GENERATION ========================

        private string GenerateSummary(TaggingCommand c)
        {
            var p = new List<string>();
            var t = c.TargetSelection;
            switch (c.Action)
            {
                case TagCommandIntent.Tag:
                    p.Add("Tag"); if (t.UntaggedOnly) p.Add("untagged");
                    p.Add(t.CategoryName?.ToLowerInvariant() ?? (c.Options.ContainsKey("tag_all") ? "all elements" : "elements"));
                    if (t.LevelName != null) p.Add($"on {t.LevelName}");
                    if (t.ParameterFilters.Count > 0) p.Add($"where {string.Join(", ", t.ParameterFilters.Select(f => f.Value != null ? $"{f.ParameterName}={f.Value}" : f.ParameterName))}");
                    if (!string.IsNullOrEmpty(c.TagTemplateName)) p.Add($"using \"{c.TagTemplateName}\"");
                    if (t.ClusterStrategy.HasValue) p.Add($"({t.ClusterStrategy.Value})");
                    break;
                case TagCommandIntent.Untag: p.Add("Remove tags from"); p.Add(t.CategoryName?.ToLowerInvariant() ?? "elements"); break;
                case TagCommandIntent.Select: case TagCommandIntent.Find:
                    p.Add("Find"); if (t.UntaggedOnly) p.Add("untagged"); p.Add(t.CategoryName?.ToLowerInvariant() ?? "elements"); break;
                case TagCommandIntent.Align:
                    p.Add("Align"); p.Add(t.CategoryName != null ? $"{t.CategoryName.ToLowerInvariant()} tags" : "tags");
                    if (c.Options.TryGetValue("direction", out var d)) p.Add(d.ToString()); break;
                case TagCommandIntent.Check: p.Add("Run quality check"); break;
                case TagCommandIntent.Export: p.Add("Export"); if (c.Options.TryGetValue("export_type", out var et)) p.Add(et.ToString().Replace("_", " "));
                    if (c.Options.TryGetValue("format", out var fm)) p.Add($"as {fm}"); break;
                case TagCommandIntent.Fix: p.Add("Fix"); p.Add(c.Options.TryGetValue("fix_type", out var ft) ? ft.ToString() : "all issues"); break;
                case TagCommandIntent.Optimize: p.Add("Optimize tag layout"); break;
                case TagCommandIntent.ChangeTemplate: p.Add("Change tags to");
                    if (c.Options.TryGetValue("display_parameter", out var dp)) p.Add($"show {dp}"); break;
                default: p.Add(c.Action.ToString()); break;
            }
            switch (c.ViewScope) {
                case CommandViewScope.AllSheets: p.Add("across all sheets"); break;
                case CommandViewScope.AllViews: p.Add("across all views"); break;
                case CommandViewScope.ActiveSheet: p.Add("on active sheet"); break;
                case CommandViewScope.NamedView when c.ViewName != null: p.Add($"in \"{c.ViewName}\""); break;
                default: p.Add("in active view"); break;
            }
            return string.Join(" ", p);
        }

        // ========================= ENTITY HELPERS ============================

        private List<TagExtractedEntity> VocabScan(string input, Dictionary<string, string> vocab, TagEntityType type, float conf)
        {
            var entities = new List<TagExtractedEntity>();
            var lo = input.ToLowerInvariant();
            foreach (var (term, canonical) in vocab)
            {
                var m = Regex.Match(lo, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase);
                if (m.Success)
                    entities.Add(new TagExtractedEntity { Type = type, RawValue = input.Substring(m.Index, m.Length),
                        NormalizedValue = canonical, Confidence = conf, StartIndex = m.Index, EndIndex = m.Index + m.Length });
            }
            return entities;
        }

        private string NormalizeEntity(TagEntityType type, string raw)
        {
            switch (type)
            {
                case TagEntityType.Direction:
                    return raw.ToLowerInvariant() switch { "left" or "l" => "left", "right" or "r" => "right",
                        "top" or "up" or "above" => "top", "bottom" or "down" or "below" => "bottom",
                        "horizontal" or "horizontally" or "h" => "horizontal",
                        "vertical" or "vertically" or "v" => "vertical", _ => raw.ToLowerInvariant() };
                case TagEntityType.Distance:
                    var dm = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*(mm|cm|m|in(?:ch(?:es)?)?|ft|feet|pt|points?)?", RegexOptions.IgnoreCase);
                    if (!dm.Success) return raw;
                    double n = double.Parse(dm.Groups[1].Value);
                    string u = dm.Groups[2].Success ? dm.Groups[2].Value.ToLowerInvariant() : "mm";
                    double mm = u switch { "m" => n * 1000, "cm" => n * 10, "mm" => n, "in" or "inch" or "inches" => n * 25.4,
                        "ft" or "feet" or "foot" => n * 304.8, "pt" or "point" or "points" => n * 0.3528, _ => n };
                    return $"{mm:F1}mm";
                case TagEntityType.Count:
                    return raw.ToLowerInvariant() switch { "all" or "every" => "-1", "one" or "single" => "1",
                        "two" => "2", "three" => "3", "four" => "4", "five" => "5", _ => raw };
                default:
                    if (_categoryVocab.TryGetValue(raw, out var c)) return c;
                    if (_parameterVocab.TryGetValue(raw, out var p)) return p;
                    return raw;
            }
        }

        // ========================= UTILITY HELPERS ===========================

        private static double Ms(DateTime start) => (DateTime.UtcNow - start).TotalMilliseconds;
        private static TaggingCommand MakeOk(TagCommandIntent action, string input, string summary) =>
            new TaggingCommand { Action = action, IsComplete = true, Confidence = 1f, OriginalInput = input, Summary = summary, TargetSelection = new TargetSelection() };
        private static TaggingCommand MakeError(string msg, string input, float conf, double ms = 0) =>
            new TaggingCommand { Action = TagCommandIntent.Unknown, IsComplete = false, Confidence = conf,
                OriginalInput = input, MissingInfo = new List<string> { msg }, ParseTimeMs = ms, TargetSelection = new TargetSelection() };

        // =================== SUGGESTION TEMPLATE DATA ========================

        private static readonly (string Cmd, string Desc)[] _suggestionTemplates =
        {
            ("tag all doors", "Tag all doors in the active view"),
            ("tag all rooms", "Tag all rooms in the active view"),
            ("tag all windows", "Tag all windows in the active view"),
            ("tag all walls", "Tag all walls in the active view"),
            ("tag fire-rated doors", "Tag doors with Fire_Rating parameter"),
            ("tag typical air terminals", "Tag clusters of identical air terminals"),
            ("tag untagged electrical equipment", "Find and tag missing electrical tags"),
            ("remove duplicate tags", "Remove duplicate tags in the active view"),
            ("remove orphan tags", "Remove tags whose host no longer exists"),
            ("align door tags horizontally", "Align door tags on a horizontal rail"),
            ("align room tags vertically", "Align room tags on a vertical rail"),
            ("distribute tags evenly", "Distribute tags with equal spacing"),
            ("run quality check", "Full quality analysis on the active view"),
            ("run quality check on all sheets", "Quality analysis across all sheets"),
            ("export tag inventory to csv", "Export tag inventory to CSV"),
            ("export tag inventory to excel", "Export tag inventory to Excel"),
            ("show untagged rooms", "Highlight rooms missing tags"),
            ("show untagged doors", "Highlight doors missing tags"),
            ("change room tags to show area", "Switch room tags to display area"),
            ("optimize tag layout", "Minimize overlaps and improve readability"),
            ("fix all issues", "Auto-fix all quality issues"),
            ("fix clashes", "Resolve tag overlaps automatically"),
            ("change door tags to show fire rating", "Switch door tags to fire rating display"),
            ("tag all lighting fixtures", "Tag all lighting fixtures in the active view"),
            ("tag all mechanical equipment", "Tag all mechanical equipment in the active view"),
            ("align tags left", "Left-align selected tags"),
            ("show untagged electrical equipment", "Highlight electrical equipment missing tags"),
            ("tag all on level 1", "Tag all elements on Level 1"),
            ("remove all tags", "Remove all tags from the active view"),
        };

        // =================== INITIALIZATION METHODS ==========================

        private void InitializeIntentPatterns()
        {
            void P(TagCommandIntent i, string name, string[] req, string[] opt, string[] neg, float w, float b)
                => _intentPatterns.Add(new IntentKeywordPattern { Intent = i, Name = name, RequiredKeywords = req,
                    OptionalKeywords = opt, NegativeKeywords = neg, BaseWeight = w, OptionalBonus = b });

            // Tag
            P(TagCommandIntent.Tag, "tag_general", new[]{"tag"}, new[]{"all","place","add","create","annotate","label"}, new[]{"untag","remove tag","delete tag","align tag"}, 0.55f, 0.08f);
            P(TagCommandIntent.Tag, "tag_annotate", new[]{"annotate"}, new[]{"all","every","elements"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Tag, "tag_label", new[]{"label"}, new[]{"all","every","place"}, null, 0.60f, 0.06f);
            P(TagCommandIntent.Tag, "tag_place", new[]{"place","tag"}, new[]{"all","new"}, null, 0.70f, 0.05f);
            P(TagCommandIntent.Tag, "tag_add", new[]{"add","tag"}, new[]{"new","all"}, null, 0.70f, 0.05f);
            P(TagCommandIntent.Tag, "tag_typical", new[]{"typical"}, new[]{"tag","mark","label"}, null, 0.60f, 0.10f);

            // Untag
            P(TagCommandIntent.Untag, "untag_remove", new[]{"remove"}, new[]{"tag","tags","all","annotation"}, new[]{"duplicate"}, 0.50f, 0.10f);
            P(TagCommandIntent.Untag, "untag_delete", new[]{"delete"}, new[]{"tag","tags","all"}, null, 0.50f, 0.10f);
            P(TagCommandIntent.Untag, "untag_clear", new[]{"clear"}, new[]{"tag","tags","all"}, null, 0.50f, 0.10f);
            P(TagCommandIntent.Untag, "untag_explicit", new[]{"untag"}, new[]{"all","every"}, null, 0.85f, 0.05f);

            // Select / Find
            P(TagCommandIntent.Select, "select", new[]{"select"}, new[]{"all","tag","element","untagged"}, null, 0.60f, 0.08f);
            P(TagCommandIntent.Find, "find", new[]{"find"}, new[]{"tag","element","untagged","missing"}, null, 0.60f, 0.08f);
            P(TagCommandIntent.Find, "find_show", new[]{"show"}, new[]{"untagged","missing","without"}, new[]{"show area","show name","show number","show fire"}, 0.45f, 0.12f);
            P(TagCommandIntent.Find, "find_highlight", new[]{"highlight"}, new[]{"untagged","missing","tag"}, null, 0.60f, 0.08f);
            P(TagCommandIntent.Find, "find_locate", new[]{"locate"}, new[]{"untagged","missing","tag"}, null, 0.58f, 0.08f);

            // Align
            P(TagCommandIntent.Align, "align", new[]{"align"}, new[]{"tag","tags","horizontal","vertical","left","right","top","bottom"}, null, 0.70f, 0.06f);
            P(TagCommandIntent.Align, "align_straighten", new[]{"straighten"}, new[]{"tag","tags","horizontal","vertical"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Align, "align_lineup", new[]{"line up"}, new[]{"tag","tags"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Align, "align_snap", new[]{"snap"}, new[]{"tag","grid","line","horizontal","vertical"}, null, 0.55f, 0.08f);

            // Distribute
            P(TagCommandIntent.Distribute, "distribute", new[]{"distribute"}, new[]{"tag","tags","even","equal","evenly"}, null, 0.70f, 0.06f);
            P(TagCommandIntent.Distribute, "space", new[]{"space"}, new[]{"tag","tags","even","equal","out"}, null, 0.50f, 0.08f);
            P(TagCommandIntent.Distribute, "spread", new[]{"spread"}, new[]{"tag","tags","out","even"}, null, 0.55f, 0.08f);

            // Check
            P(TagCommandIntent.Check, "check", new[]{"check"}, new[]{"quality","tag","sheet","view","all"}, null, 0.55f, 0.08f);
            P(TagCommandIntent.Check, "analyze", new[]{"analyze"}, new[]{"tag","quality","sheet","view"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Check, "review", new[]{"review"}, new[]{"tag","quality","annotation"}, null, 0.55f, 0.08f);
            P(TagCommandIntent.Check, "audit", new[]{"audit"}, new[]{"tag","annotation"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Check, "validate", new[]{"validate"}, new[]{"tag","annotation"}, null, 0.65f, 0.06f);

            // Export
            P(TagCommandIntent.Export, "export", new[]{"export"}, new[]{"tag","inventory","excel","csv","json","report"}, null, 0.70f, 0.06f);
            P(TagCommandIntent.Export, "save_as", new[]{"save"}, new[]{"excel","csv","json","file","report","inventory"}, null, 0.45f, 0.10f);

            // Configure
            P(TagCommandIntent.Configure, "configure", new[]{"configure"}, new[]{"leader","offset","alignment","tag","template"}, null, 0.70f, 0.06f);
            P(TagCommandIntent.Configure, "set", new[]{"set"}, new[]{"leader","offset","distance","alignment","strategy","mode"}, null, 0.40f, 0.12f);
            P(TagCommandIntent.Configure, "settings", new[]{"settings"}, new[]{"tag","tagging","placement","open","change"}, null, 0.55f, 0.08f);

            // Optimize
            P(TagCommandIntent.Optimize, "optimize", new[]{"optimize"}, new[]{"tag","layout","position","leader","readability"}, null, 0.75f, 0.05f);
            P(TagCommandIntent.Optimize, "improve", new[]{"improve"}, new[]{"tag","layout","readability","placement"}, null, 0.55f, 0.08f);
            P(TagCommandIntent.Optimize, "cleanup", new[]{"clean"}, new[]{"up","tag","layout","annotation"}, null, 0.50f, 0.08f);
            P(TagCommandIntent.Optimize, "tidy", new[]{"tidy"}, new[]{"up","tag","annotation"}, null, 0.55f, 0.08f);

            // ChangeTemplate
            P(TagCommandIntent.ChangeTemplate, "change", new[]{"change"}, new[]{"tag","template","show","display","to"}, null, 0.45f, 0.10f);
            P(TagCommandIntent.ChangeTemplate, "switch", new[]{"switch"}, new[]{"tag","template","to","display"}, null, 0.50f, 0.08f);
            P(TagCommandIntent.ChangeTemplate, "show_param", new[]{"show"}, new[]{"area","name","number","fire rating","mark","type"}, new[]{"untagged","missing"}, 0.40f, 0.15f);

            // Fix
            P(TagCommandIntent.Fix, "fix", new[]{"fix"}, new[]{"tag","duplicate","orphan","clash","overlap","blank","stale","misaligned","all","issue"}, null, 0.65f, 0.06f);
            P(TagCommandIntent.Fix, "resolve", new[]{"resolve"}, new[]{"clash","overlap","collision","conflict","duplicate"}, null, 0.60f, 0.08f);
            P(TagCommandIntent.Fix, "rm_dups", new[]{"remove","duplicate"}, new[]{"tag","tags"}, null, 0.85f, 0.04f);
            P(TagCommandIntent.Fix, "repair", new[]{"repair"}, new[]{"tag","annotation"}, null, 0.60f, 0.08f);
            P(TagCommandIntent.Fix, "refresh", new[]{"refresh"}, new[]{"tag","all","stale","outdated"}, null, 0.55f, 0.08f);
            P(TagCommandIntent.Fix, "update", new[]{"update"}, new[]{"tag","all","stale","text"}, null, 0.50f, 0.08f);

            Logger.Debug($"Initialized {_intentPatterns.Count} intent patterns");
        }

        private void InitializeBIMVocabulary()
        {
            // Category: colloquial -> canonical Revit BuiltInCategory display name
            var cats = new (string Key, string Val)[] {
                ("doors","Doors"), ("door","Doors"), ("windows","Windows"), ("window","Windows"),
                ("walls","Walls"), ("wall","Walls"), ("rooms","Rooms"), ("room","Rooms"),
                ("floors","Floors"), ("floor","Floors"), ("ceilings","Ceilings"), ("ceiling","Ceilings"),
                ("roofs","Roofs"), ("roof","Roofs"), ("stairs","Stairs"), ("stair","Stairs"),
                ("railings","Railings"), ("railing","Railings"), ("columns","Columns"), ("column","Columns"),
                ("beams","Structural Framing"), ("beam","Structural Framing"), ("furniture","Furniture"),
                ("casework","Casework"), ("cabinets","Casework"), ("generic models","Generic Models"),
                ("structural columns","Structural Columns"), ("foundations","Structural Foundations"),
                ("mechanical equipment","Mechanical Equipment"), ("ahu","Mechanical Equipment"),
                ("air handling unit","Mechanical Equipment"), ("hvac","Mechanical Equipment"),
                ("ducts","Ducts"), ("duct","Ducts"), ("ductwork","Ducts"),
                ("diffusers","Air Terminals"), ("diffuser","Air Terminals"), ("air terminals","Air Terminals"),
                ("grilles","Air Terminals"), ("grille","Air Terminals"), ("vav","Air Terminals"),
                ("duct fittings","Duct Fittings"), ("duct accessories","Duct Accessories"),
                ("flex ducts","Flex Ducts"), ("flex duct","Flex Ducts"),
                ("electrical equipment","Electrical Equipment"), ("panels","Electrical Equipment"),
                ("panel","Electrical Equipment"), ("switchboard","Electrical Equipment"),
                ("transformer","Electrical Equipment"), ("transformers","Electrical Equipment"),
                ("lights","Lighting Fixtures"), ("light","Lighting Fixtures"), ("lighting","Lighting Fixtures"),
                ("lighting fixtures","Lighting Fixtures"), ("luminaires","Lighting Fixtures"),
                ("electrical fixtures","Electrical Fixtures"), ("outlets","Electrical Fixtures"),
                ("outlet","Electrical Fixtures"), ("receptacles","Electrical Fixtures"),
                ("switches","Electrical Fixtures"), ("switch","Electrical Fixtures"),
                ("cable trays","Cable Trays"), ("cable tray","Cable Trays"),
                ("conduits","Conduits"), ("conduit","Conduits"),
                ("pipes","Pipes"), ("pipe","Pipes"), ("piping","Pipes"),
                ("pipe fittings","Pipe Fittings"), ("pipe accessories","Pipe Accessories"),
                ("valves","Pipe Accessories"), ("valve","Pipe Accessories"),
                ("plumbing fixtures","Plumbing Fixtures"), ("plumbing","Plumbing Fixtures"),
                ("sprinklers","Sprinklers"), ("sprinkler","Sprinklers"),
                ("fire alarm devices","Fire Alarm Devices"), ("fire alarm","Fire Alarm Devices"),
                ("smoke detectors","Fire Alarm Devices"),
                ("spaces","Spaces"), ("space","Spaces"), ("areas","Areas"), ("area","Areas"),
                ("curtain walls","Curtain Wall Panels"), ("curtain panels","Curtain Wall Panels"),
                ("parking","Parking"),
                // Specialty categories
                ("entourage","Entourage"), ("planting","Planting"), ("topography","Topography"),
                ("mass","Mass"), ("ramps","Ramps"), ("ramp","Ramps"),
                ("curtain systems","Curtain Systems"), ("curtain grids","Curtain Grids"),
                ("shaft openings","Shaft Openings"), ("shaft opening","Shaft Openings"),
                ("structural connections","Structural Connections"),
                ("structural rebar","Structural Rebar"), ("rebar","Structural Rebar")
            };
            foreach (var (k, v) in cats) _categoryVocab[k] = v;

            // Parameters
            var pars = new (string Key, string Val)[] {
                ("fire rating","Fire_Rating"), ("fire-rating","Fire_Rating"), ("fire rated","Fire_Rating"),
                ("fire-rated","Fire_Rating"), ("fire resistance","Fire_Rating"),
                ("mark","Mark"), ("number","Mark"), ("door number","Mark"),
                ("room number","Number"), ("room name","Name"), ("name","Name"),
                ("area","Area"), ("volume","Volume"), ("perimeter","Perimeter"),
                ("height","Height"), ("width","Width"), ("length","Length"), ("depth","Depth"),
                ("thickness","Thickness"), ("comments","Comments"), ("description","Description"),
                ("type","Type Name"), ("type name","Type Name"), ("family","Family Name"),
                ("family name","Family Name"), ("level","Level"), ("phase","Phase Created"),
                ("department","Department"), ("occupancy","Occupancy"), ("cost","Cost"),
                ("manufacturer","Manufacturer"), ("model","Model"), ("keynote","Keynote"),
                ("assembly code","Assembly Code"), ("u-value","Heat Transfer Coefficient (U)"),
                ("thermal transmittance","Heat Transfer Coefficient (U)"), ("r-value","Thermal Resistance (R)"),
                ("flow","Flow"), ("airflow","Flow"), ("cfm","Flow"), ("pressure drop","Pressure Drop"),
                ("circuit number","Circuit Number"), ("voltage","Voltage"), ("amperage","Apparent Load"),
                ("load","Apparent Load"), ("size","Size"), ("pipe size","Size"), ("duct size","Size"),
                ("system name","System Name"), ("system type","System Type"),
                ("workset","Workset"), ("design option","Design Option"),
                ("scope box","Scope Box"), ("classification","OmniClass Title"),
                ("omniclass","OmniClass Title"), ("uniformat","Assembly Code"),
                ("structural usage","Structural Usage"), ("span","Span Direction"),
                ("slope","Slope"), ("offset from level","Offset from Level")
            };
            foreach (var (k, v) in pars) _parameterVocab[k] = v;

            // Tag types -> template names
            var tags = new (string Key, string Val)[] {
                ("fire rating tag","Doors_Fire_Rated"), ("fire tag","Doors_Fire_Rated"),
                ("door tag","Doors_Standard"), ("room tag","Rooms_Standard"),
                ("room area tag","Rooms_Area"), ("room number tag","Rooms_Number"),
                ("room name tag","Rooms_Name"), ("wall tag","Walls_Standard"),
                ("window tag","Windows_Standard"), ("duct tag","Ducts_Standard"),
                ("pipe tag","Pipes_Standard"), ("equipment tag","Equipment_Standard"),
                ("light tag","Lighting_Standard"), ("lighting tag","Lighting_Standard"),
                ("panel tag","Electrical_Panel"), ("structural tag","Structural_Standard"),
                ("column tag","Columns_Standard"), ("beam tag","Beams_Standard"),
                ("keynote tag","Generic_Keynote"), ("material tag","Generic_Material"),
                ("area tag","Areas_Standard"), ("space tag","Spaces_Standard"),
                ("sprinkler tag","Sprinklers_Standard"), ("valve tag","Valves_Standard"),
                ("air terminal tag","AirTerminals_Standard"), ("diffuser tag","AirTerminals_Standard"),
                ("ceiling tag","Ceilings_Standard"), ("floor tag","Floors_Standard"),
                ("furniture tag","Furniture_Standard")
            };
            foreach (var (k, v) in tags) _tagTypeVocab[k] = v;

            // Levels
            var lvls = new (string Key, string Val)[] {
                ("ground floor","Level 0"), ("ground level","Level 0"), ("ground","Level 0"), ("gf","Level 0"),
                ("basement","Level B1"), ("basement 1","Level B1"), ("basement 2","Level B2"),
                ("sub-basement","Level B2"),
                ("level 0","Level 0"), ("level 1","Level 1"), ("level 2","Level 2"), ("level 3","Level 3"),
                ("level 4","Level 4"), ("level 5","Level 5"), ("level 6","Level 6"), ("level 7","Level 7"),
                ("level 8","Level 8"), ("level 9","Level 9"), ("level 10","Level 10"),
                ("first floor","Level 1"), ("second floor","Level 2"), ("third floor","Level 3"),
                ("fourth floor","Level 4"), ("fifth floor","Level 5"),
                ("1st floor","Level 1"), ("2nd floor","Level 2"), ("3rd floor","Level 3"),
                ("4th floor","Level 4"), ("5th floor","Level 5"),
                ("roof","Roof"), ("roof level","Roof"), ("rooftop","Roof"),
                ("mezzanine","Mezzanine"), ("mezz","Mezzanine"), ("penthouse","Penthouse")
            };
            foreach (var (k, v) in lvls) _levelVocab[k] = v;
        }

        private void InitializeEntityPatterns()
        {
            var o = RegexOptions.IgnoreCase | RegexOptions.Compiled;

            _entityPatterns[TagEntityType.Level] = new List<Regex> {
                new Regex(@"\bLevel\s+(?:B?\d{1,3}|[A-Z])\b", o),
                new Regex(@"\bon\s+L\d{1,3}\b", o),
                new Regex(@"\b\d{1,2}(?:st|nd|rd|th)\s+floor\b", o),
                new Regex(@"\b(?:first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth)\s+floor\b", o),
                new Regex(@"\bground\s+(?:floor|level)\b", o),
                new Regex(@"\b(?:sub-?)?basement\s*\d?\b", o),
                new Regex(@"\b(?:roof(?:\s*level|top)?|mezzanine|mezz|penthouse)\b", o)
            };
            _entityPatterns[TagEntityType.Direction] = new List<Regex> {
                new Regex(@"\b(?:horizontal(?:ly)?|vertical(?:ly)?|left|right|top|bottom|up|down|above|below|center|middle)\b", o)
            };
            _entityPatterns[TagEntityType.Distance] = new List<Regex> {
                new Regex(@"\b\d+(?:\.\d+)?\s*(?:mm|cm|m|in(?:ch(?:es)?)?|ft|feet|foot|pt|points?)\b", o),
                new Regex(@"\b\d+'-\d+""?\b", o)
            };
            _entityPatterns[TagEntityType.Count] = new List<Regex> {
                new Regex(@"\bfirst\s+\d+\b", o), new Regex(@"\btop\s+\d+\b", o),
                new Regex(@"\b(?:all|every)\b", o)
            };
            _entityPatterns[TagEntityType.Standard] = new List<Regex> {
                new Regex(@"\b(?:IBC|IMC|IPC|NEC|NFPA|ASHRAE|ASCE|ACI|BS|EN|ISO|SANS|KEBS|UNBS|CIBSE)\s*\d*(?:\.\d+)?\s*(?:\d{4})?\b", o),
                new Regex(@"\bEurocode\s*\d?\b", o)
            };
            _entityPatterns[TagEntityType.RuleGroup] = new List<Regex> {
                new Regex(@"\b(?:architectural|structural|mep|mechanical|electrical|plumbing|fire\s+protection|civil)\s+(?:rules?|group|set)\b", o)
            };
            _entityPatterns[TagEntityType.ViewName] = new List<Regex> {
                new Regex(@"(?:in|on)\s+(?:view\s+)?['""]([^'""]+)['""]", o),
                new Regex(@"\bSection\s+[A-Z]-[A-Z]\b", o), new Regex(@"\bSection\s+\d+\b", o),
                new Regex(@"\bElevation\s*-?\s*(?:North|South|East|West)\b", o)
            };
            _entityPatterns[TagEntityType.ParameterValue] = new List<Regex> {
                new Regex(@"['""]([^'""]+)['""]", o),
                new Regex(@"\b\d+(?:-|\s+)(?:hour|hr|minute|min)\b", o),
                new Regex(@"\b(?:yes|no|true|false)\b", o)
            };
        }

        private void InitializeFollowUpSuggestions()
        {
            CommandSuggestion S(string cmd, string desc, float rel) =>
                new CommandSuggestion { CommandText = cmd, Description = desc, Relevance = rel, Category = "follow-up" };

            _followUps[TagCommandIntent.Tag] = new List<CommandSuggestion> {
                S("Run quality check", "Check for clashes with newly placed tags", 0.90f),
                S("Align tags horizontally", "Align new tags for clean layout", 0.85f),
                S("Optimize tag layout", "Run layout optimization", 0.80f),
                S("Show untagged elements", "Check if any elements were missed", 0.75f) };
            _followUps[TagCommandIntent.Check] = new List<CommandSuggestion> {
                S("Fix all issues", "Auto-fix all issues found", 0.92f),
                S("Export quality report to Excel", "Export results for documentation", 0.70f) };
            _followUps[TagCommandIntent.Fix] = new List<CommandSuggestion> {
                S("Run quality check", "Verify all issues resolved", 0.90f),
                S("Optimize tag layout", "Optimize layout after fixes", 0.75f) };
            _followUps[TagCommandIntent.Align] = new List<CommandSuggestion> {
                S("Distribute tags evenly", "Equalize spacing between aligned tags", 0.85f),
                S("Run quality check", "Check for remaining clashes", 0.80f) };
            _followUps[TagCommandIntent.Untag] = new List<CommandSuggestion> {
                S("Tag all elements", "Re-tag with fresh placement", 0.80f) };
            _followUps[TagCommandIntent.Optimize] = new List<CommandSuggestion> {
                S("Run quality check", "Verify optimization results", 0.88f),
                S("Export tag inventory to Excel", "Document final tag state", 0.60f) };
            _followUps[TagCommandIntent.Export] = new List<CommandSuggestion> {
                S("Run quality check on all sheets", "Comprehensive quality analysis", 0.70f) };
            _followUps[TagCommandIntent.Distribute] = new List<CommandSuggestion> {
                S("Run quality check", "Verify distribution results", 0.85f),
                S("Align tags horizontally", "Combine with alignment for polished layout", 0.75f) };
            _followUps[TagCommandIntent.ChangeTemplate] = new List<CommandSuggestion> {
                S("Run quality check", "Verify tags display correctly after template change", 0.85f),
                S("Export tag inventory to Excel", "Document the updated tags", 0.60f) };
            _followUps[TagCommandIntent.Select] = new List<CommandSuggestion> {
                S("Tag selected elements", "Tag the elements you just selected", 0.90f),
                S("Align selected tags", "Align the tags you just selected", 0.80f) };
        }

        /// <summary>
        /// Returns a list of all recognized command patterns with descriptions.
        /// Useful for displaying help text or training users.
        /// </summary>
        public List<CommandSuggestion> GetAvailableCommands()
        {
            var commands = new List<CommandSuggestion>();
            foreach (var (cmd, desc) in _suggestionTemplates)
            {
                commands.Add(new CommandSuggestion
                {
                    CommandText = cmd,
                    Description = desc,
                    Relevance = 1.0f,
                    Category = "available"
                });
            }
            return commands;
        }

        /// <summary>
        /// Clears the command history. Useful when switching projects or resetting state.
        /// </summary>
        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _history.Clear();
            }
            Logger.Info("Command history cleared");
        }

        /// <summary>
        /// Returns the number of intent patterns loaded for diagnostics.
        /// </summary>
        public int PatternCount => _intentPatterns.Count;

        /// <summary>
        /// Returns the total number of BIM vocabulary terms loaded across all dictionaries.
        /// </summary>
        public int VocabularySize =>
            _categoryVocab.Count + _parameterVocab.Count + _tagTypeVocab.Count + _levelVocab.Count;
    }
}
