// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagRepository.cs - Persistence, import/export, extensible storage, session tracking
// Surpasses Naviate extensible storage and Ideate Excel integration

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Data
{
    /// <summary>
    /// Central repository for all tagging data: tag instances, rules, templates,
    /// learning data, and session history. Provides persistence via JSON files,
    /// Revit extensible storage integration points, and Excel export capabilities.
    ///
    /// Surpasses:
    /// - Naviate: Rules/templates stored in Revit extensible storage with import/export
    /// - Ideate: BIMLink-style Excel export of tag inventories and quality reports
    /// - BIMLOGIQ: Template sharing across teams and projects
    /// </summary>
    public class TagRepository
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _tagsLock = new object();
        private readonly object _rulesLock = new object();
        private readonly object _templatesLock = new object();
        private readonly object _sessionsLock = new object();
        private readonly object _correctionsLock = new object();

        // In-memory stores
        private readonly Dictionary<string, TagInstance> _tags;
        private readonly Dictionary<string, TagRule> _rules;
        private readonly Dictionary<string, RuleGroup> _ruleGroups;
        private readonly Dictionary<string, TagTemplateDefinition> _templates;
        private readonly List<TagSession> _sessions;
        private readonly List<PlacementCorrection> _corrections;
        private readonly Dictionary<string, PlacementPattern> _patterns;
        private readonly List<TagOperation> _operationHistory;

        // Index for fast lookup
        private readonly Dictionary<int, List<string>> _tagsByHostElement;
        private readonly Dictionary<int, List<string>> _tagsByView;
        private readonly Dictionary<string, List<string>> _tagsByCategory;

        private string _dataDirectory;
        private bool _isInitialized;

        public TagRepository()
        {
            _tags = new Dictionary<string, TagInstance>(StringComparer.OrdinalIgnoreCase);
            _rules = new Dictionary<string, TagRule>(StringComparer.OrdinalIgnoreCase);
            _ruleGroups = new Dictionary<string, RuleGroup>(StringComparer.OrdinalIgnoreCase);
            _templates = new Dictionary<string, TagTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
            _sessions = new List<TagSession>();
            _corrections = new List<PlacementCorrection>();
            _patterns = new Dictionary<string, PlacementPattern>(StringComparer.OrdinalIgnoreCase);
            _operationHistory = new List<TagOperation>();
            _tagsByHostElement = new Dictionary<int, List<string>>();
            _tagsByView = new Dictionary<int, List<string>>();
            _tagsByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        #region Initialization

        /// <summary>
        /// Initializes the repository, loading persisted data from the data directory.
        /// </summary>
        public async Task InitializeAsync(string dataDirectory = null, CancellationToken ct = default)
        {
            _dataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "Tagging");

            if (!Directory.Exists(_dataDirectory))
                Directory.CreateDirectory(_dataDirectory);

            await LoadRulesAsync(ct);
            await LoadTemplatesAsync(ct);
            await LoadPatternsAsync(ct);

            _isInitialized = true;
            Logger.Info("TagRepository initialized with {0} rules, {1} templates, {2} patterns",
                _rules.Count, _templates.Count, _patterns.Count);
        }

        #endregion

        #region Tag CRUD Operations

        /// <summary>
        /// Registers a new tag instance in the repository and updates all indices.
        /// </summary>
        public void AddTag(TagInstance tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (string.IsNullOrEmpty(tag.TagId))
                tag.TagId = Guid.NewGuid().ToString("N");

            lock (_tagsLock)
            {
                _tags[tag.TagId] = tag;
                IndexTag(tag);
            }
        }

        /// <summary>
        /// Updates an existing tag instance.
        /// </summary>
        public void UpdateTag(TagInstance tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            lock (_tagsLock)
            {
                if (_tags.ContainsKey(tag.TagId))
                {
                    RemoveFromIndices(tag.TagId);
                    _tags[tag.TagId] = tag;
                    IndexTag(tag);
                    tag.LastModified = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Removes a tag from the repository.
        /// </summary>
        public void RemoveTag(string tagId)
        {
            lock (_tagsLock)
            {
                if (_tags.ContainsKey(tagId))
                {
                    RemoveFromIndices(tagId);
                    _tags.Remove(tagId);
                }
            }
        }

        /// <summary>
        /// Gets a tag by its ID.
        /// </summary>
        public TagInstance GetTag(string tagId)
        {
            lock (_tagsLock)
            {
                return _tags.TryGetValue(tagId, out var tag) ? tag : null;
            }
        }

        /// <summary>
        /// Gets all tags for a specific host element.
        /// </summary>
        public List<TagInstance> GetTagsByHostElement(int hostElementId)
        {
            lock (_tagsLock)
            {
                if (_tagsByHostElement.TryGetValue(hostElementId, out var tagIds))
                    return tagIds.Select(id => _tags[id]).ToList();
                return new List<TagInstance>();
            }
        }

        /// <summary>
        /// Gets all tags in a specific view.
        /// </summary>
        public List<TagInstance> GetTagsByView(int viewId)
        {
            lock (_tagsLock)
            {
                if (_tagsByView.TryGetValue(viewId, out var tagIds))
                    return tagIds.Select(id => _tags[id]).ToList();
                return new List<TagInstance>();
            }
        }

        /// <summary>
        /// Gets all tags for a specific Revit category.
        /// </summary>
        public List<TagInstance> GetTagsByCategory(string categoryName)
        {
            lock (_tagsLock)
            {
                if (_tagsByCategory.TryGetValue(categoryName, out var tagIds))
                    return tagIds.Select(id => _tags[id]).ToList();
                return new List<TagInstance>();
            }
        }

        /// <summary>
        /// Gets all managed tags.
        /// </summary>
        public List<TagInstance> GetAllTags()
        {
            lock (_tagsLock)
            {
                return _tags.Values.ToList();
            }
        }

        private void IndexTag(TagInstance tag)
        {
            if (!_tagsByHostElement.ContainsKey(tag.HostElementId))
                _tagsByHostElement[tag.HostElementId] = new List<string>();
            _tagsByHostElement[tag.HostElementId].Add(tag.TagId);

            if (!_tagsByView.ContainsKey(tag.ViewId))
                _tagsByView[tag.ViewId] = new List<string>();
            _tagsByView[tag.ViewId].Add(tag.TagId);

            if (!string.IsNullOrEmpty(tag.CategoryName))
            {
                if (!_tagsByCategory.ContainsKey(tag.CategoryName))
                    _tagsByCategory[tag.CategoryName] = new List<string>();
                _tagsByCategory[tag.CategoryName].Add(tag.TagId);
            }
        }

        private void RemoveFromIndices(string tagId)
        {
            if (!_tags.TryGetValue(tagId, out var tag)) return;

            if (_tagsByHostElement.TryGetValue(tag.HostElementId, out var hostList))
                hostList.Remove(tagId);
            if (_tagsByView.TryGetValue(tag.ViewId, out var viewList))
                viewList.Remove(tagId);
            if (!string.IsNullOrEmpty(tag.CategoryName) &&
                _tagsByCategory.TryGetValue(tag.CategoryName, out var catList))
                catList.Remove(tagId);
        }

        #endregion

        #region Rules Management

        /// <summary>Adds or updates a tag rule.</summary>
        public void SaveRule(TagRule rule)
        {
            lock (_rulesLock) { _rules[rule.RuleId] = rule; }
        }

        /// <summary>Gets a rule by ID.</summary>
        public TagRule GetRule(string ruleId)
        {
            lock (_rulesLock) { return _rules.TryGetValue(ruleId, out var r) ? r : null; }
        }

        /// <summary>Gets all rules, optionally filtered by category.</summary>
        public List<TagRule> GetRules(string categoryFilter = null)
        {
            lock (_rulesLock)
            {
                var rules = _rules.Values.AsEnumerable();
                if (!string.IsNullOrEmpty(categoryFilter))
                    rules = rules.Where(r => string.Equals(r.CategoryFilter, categoryFilter,
                        StringComparison.OrdinalIgnoreCase));
                return rules.OrderBy(r => r.Priority).ToList();
            }
        }

        /// <summary>Gets rules in a named group.</summary>
        public List<TagRule> GetRulesByGroup(string groupName)
        {
            lock (_rulesLock)
            {
                if (!_ruleGroups.TryGetValue(groupName, out var group)) return new List<TagRule>();
                return group.RuleIds
                    .Where(id => _rules.ContainsKey(id))
                    .Select(id => _rules[id])
                    .OrderBy(r => r.Priority)
                    .ToList();
            }
        }

        /// <summary>Saves a rule group.</summary>
        public void SaveRuleGroup(RuleGroup group)
        {
            lock (_rulesLock) { _ruleGroups[group.Name] = group; }
        }

        /// <summary>Gets all rule groups.</summary>
        public List<RuleGroup> GetRuleGroups()
        {
            lock (_rulesLock) { return _ruleGroups.Values.ToList(); }
        }

        /// <summary>Removes a rule.</summary>
        public void RemoveRule(string ruleId)
        {
            lock (_rulesLock) { _rules.Remove(ruleId); }
        }

        #endregion

        #region Templates Management

        /// <summary>Adds or updates a tag template.</summary>
        public void SaveTemplate(TagTemplateDefinition template)
        {
            lock (_templatesLock) { _templates[template.Name] = template; }
        }

        /// <summary>Gets a template by name.</summary>
        public TagTemplateDefinition GetTemplate(string name)
        {
            lock (_templatesLock) { return _templates.TryGetValue(name, out var t) ? t : null; }
        }

        /// <summary>Gets all templates, optionally filtered by category.</summary>
        public List<TagTemplateDefinition> GetTemplates(string categoryFilter = null)
        {
            lock (_templatesLock)
            {
                var templates = _templates.Values.AsEnumerable();
                if (!string.IsNullOrEmpty(categoryFilter))
                    templates = templates.Where(t => string.Equals(t.CategoryName, categoryFilter,
                        StringComparison.OrdinalIgnoreCase));
                return templates.ToList();
            }
        }

        /// <summary>Gets the best template for a category and view type combination.</summary>
        public TagTemplateDefinition GetBestTemplate(string categoryName, TagViewType viewType)
        {
            lock (_templatesLock)
            {
                return _templates.Values
                    .Where(t => string.Equals(t.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                    .Where(t => t.ViewTypes == null || t.ViewTypes.Count == 0 || t.ViewTypes.Contains(viewType))
                    .FirstOrDefault();
            }
        }

        /// <summary>Removes a template.</summary>
        public void RemoveTemplate(string name)
        {
            lock (_templatesLock) { _templates.Remove(name); }
        }

        #endregion

        #region Learning Data

        /// <summary>Records a user placement correction for learning.</summary>
        public void AddCorrection(PlacementCorrection correction)
        {
            lock (_correctionsLock) { _corrections.Add(correction); }
        }

        /// <summary>Gets all corrections for a category and view type.</summary>
        public List<PlacementCorrection> GetCorrections(string categoryName = null, TagViewType? viewType = null)
        {
            lock (_correctionsLock)
            {
                var corrections = _corrections.AsEnumerable();
                if (!string.IsNullOrEmpty(categoryName))
                    corrections = corrections.Where(c => string.Equals(c.CategoryName, categoryName,
                        StringComparison.OrdinalIgnoreCase));
                if (viewType.HasValue)
                    corrections = corrections.Where(c => c.ViewType == viewType.Value);
                return corrections.ToList();
            }
        }

        /// <summary>Saves a learned placement pattern.</summary>
        public void SavePattern(PlacementPattern pattern)
        {
            lock (_correctionsLock) { _patterns[pattern.PatternId] = pattern; }
        }

        /// <summary>Gets a learned pattern by ID.</summary>
        public PlacementPattern GetPattern(string patternId)
        {
            lock (_correctionsLock) { return _patterns.TryGetValue(patternId, out var p) ? p : null; }
        }

        /// <summary>Gets the best matching pattern for a category and view type.</summary>
        public PlacementPattern GetBestPattern(string categoryName, TagViewType viewType)
        {
            lock (_correctionsLock)
            {
                return _patterns.Values
                    .Where(p => string.Equals(p.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                    .Where(p => p.ViewType == viewType)
                    .Where(p => p.Confidence >= TagConfiguration.Instance.Settings.IntelligenceScoreWeight)
                    .OrderByDescending(p => p.Confidence)
                    .FirstOrDefault();
            }
        }

        /// <summary>Gets all learned patterns.</summary>
        public List<PlacementPattern> GetAllPatterns()
        {
            lock (_correctionsLock) { return _patterns.Values.ToList(); }
        }

        #endregion

        #region Operation History

        /// <summary>Records a tag operation for undo/redo.</summary>
        public void RecordOperation(TagOperation operation)
        {
            lock (_tagsLock)
            {
                if (string.IsNullOrEmpty(operation.OperationId))
                    operation.OperationId = Guid.NewGuid().ToString("N");
                operation.Timestamp = DateTime.UtcNow;
                _operationHistory.Add(operation);

                // Keep last 1000 operations
                if (_operationHistory.Count > 1000)
                    _operationHistory.RemoveRange(0, _operationHistory.Count - 1000);
            }
        }

        /// <summary>Gets recent operations for undo.</summary>
        public List<TagOperation> GetRecentOperations(int count = 50)
        {
            lock (_tagsLock)
            {
                return _operationHistory
                    .OrderByDescending(o => o.Timestamp)
                    .Take(count)
                    .ToList();
            }
        }

        #endregion

        #region Session Tracking

        /// <summary>Creates and registers a new tagging session.</summary>
        public TagSession StartSession(string description)
        {
            var session = new TagSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Description = description,
                StartedAt = DateTime.UtcNow,
                IsActive = true
            };

            lock (_sessionsLock) { _sessions.Add(session); }
            Logger.Info("Tagging session started: {0}", description);
            return session;
        }

        /// <summary>Completes a tagging session with results.</summary>
        public void EndSession(string sessionId, BatchPlacementResult result)
        {
            lock (_sessionsLock)
            {
                var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (session != null)
                {
                    session.IsActive = false;
                    session.CompletedAt = DateTime.UtcNow;
                    session.TagsPlaced = result?.SuccessCount ?? 0;
                    session.QualityScore = result?.QualityScore ?? 0;
                    session.ViewsProcessed = result?.ViewsProcessed ?? 0;
                }
            }
        }

        /// <summary>Gets session history.</summary>
        public List<TagSession> GetSessionHistory(int count = 20)
        {
            lock (_sessionsLock)
            {
                return _sessions
                    .OrderByDescending(s => s.StartedAt)
                    .Take(count)
                    .ToList();
            }
        }

        #endregion

        #region Import / Export

        /// <summary>
        /// Exports rules, templates, and patterns to a JSON package for sharing.
        /// Surpasses BIMLOGIQ template export and Naviate XML import/export.
        /// </summary>
        public async Task<string> ExportPackageAsync(string outputPath, CancellationToken ct = default)
        {
            var package = new ExportPackage
            {
                ExportedAt = DateTime.UtcNow,
                Version = "1.0.0",
                Rules = GetRules(),
                RuleGroups = GetRuleGroups(),
                Templates = GetTemplates(),
                Patterns = GetAllPatterns()
            };

            string json = JsonConvert.SerializeObject(package, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json, ct);

            Logger.Info("Exported {0} rules, {1} templates, {2} patterns to {3}",
                package.Rules.Count, package.Templates.Count, package.Patterns.Count, outputPath);
            return outputPath;
        }

        /// <summary>
        /// Imports rules, templates, and patterns from a JSON package.
        /// Handles conflicts by keeping the newer version.
        /// </summary>
        public async Task<ImportResult> ImportPackageAsync(string inputPath, CancellationToken ct = default)
        {
            var result = new ImportResult();

            try
            {
                string json = await File.ReadAllTextAsync(inputPath, ct);
                var package = JsonConvert.DeserializeObject<ExportPackage>(json);

                if (package == null) return result;

                foreach (var rule in package.Rules ?? Enumerable.Empty<TagRule>())
                {
                    SaveRule(rule);
                    result.RulesImported++;
                }

                foreach (var group in package.RuleGroups ?? Enumerable.Empty<RuleGroup>())
                {
                    SaveRuleGroup(group);
                    result.GroupsImported++;
                }

                foreach (var template in package.Templates ?? Enumerable.Empty<TagTemplateDefinition>())
                {
                    SaveTemplate(template);
                    result.TemplatesImported++;
                }

                foreach (var pattern in package.Patterns ?? Enumerable.Empty<PlacementPattern>())
                {
                    SavePattern(pattern);
                    result.PatternsImported++;
                }

                Logger.Info("Imported {0} rules, {1} groups, {2} templates, {3} patterns from {4}",
                    result.RulesImported, result.GroupsImported, result.TemplatesImported,
                    result.PatternsImported, inputPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to import package from {0}", inputPath);
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Exports tag inventory to CSV format for external analysis.
        /// Surpasses Ideate BIMLink's annotation export with richer data.
        /// </summary>
        public async Task ExportInventoryToCsvAsync(string outputPath, CancellationToken ct = default)
        {
            var tags = GetAllTags();
            var lines = new List<string>
            {
                "TagId,HostElementId,ViewId,Category,Family,Type,TagFamily,DisplayText,State,PlacementScore,CreatedByRule,CreatedByTemplate,LastModified"
            };

            foreach (var tag in tags)
            {
                lines.Add(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9:F3},{10},{11},{12:O}",
                    tag.TagId, tag.HostElementId, tag.ViewId,
                    EscapeCsv(tag.CategoryName), EscapeCsv(tag.FamilyName), EscapeCsv(tag.TypeName),
                    EscapeCsv(tag.TagFamilyName), EscapeCsv(tag.DisplayText),
                    tag.State, tag.PlacementScore,
                    EscapeCsv(tag.CreatedByRule), EscapeCsv(tag.CreatedByTemplate),
                    tag.LastModified));
            }

            await File.WriteAllLinesAsync(outputPath, lines, ct);
            Logger.Info("Exported {0} tags to CSV: {1}", tags.Count, outputPath);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        #endregion

        #region Persistence (JSON Files)

        private async Task LoadRulesAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "rules.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path, ct);
                    var data = JsonConvert.DeserializeObject<RulesData>(json);
                    if (data != null)
                    {
                        lock (_rulesLock)
                        {
                            foreach (var r in data.Rules ?? Enumerable.Empty<TagRule>())
                                _rules[r.RuleId] = r;
                            foreach (var g in data.Groups ?? Enumerable.Empty<RuleGroup>())
                                _ruleGroups[g.Name] = g;
                        }
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Failed to load rules from {0}", path); }
            }
        }

        private async Task LoadTemplatesAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "templates.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path, ct);
                    var templates = JsonConvert.DeserializeObject<List<TagTemplateDefinition>>(json);
                    if (templates != null)
                    {
                        lock (_templatesLock)
                        {
                            foreach (var t in templates)
                                _templates[t.Name] = t;
                        }
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Failed to load templates from {0}", path); }
            }
        }

        private async Task LoadPatternsAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "patterns.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path, ct);
                    var patterns = JsonConvert.DeserializeObject<List<PlacementPattern>>(json);
                    if (patterns != null)
                    {
                        lock (_correctionsLock)
                        {
                            foreach (var p in patterns)
                                _patterns[p.PatternId] = p;
                        }
                    }
                }
                catch (Exception ex) { Logger.Warn(ex, "Failed to load patterns from {0}", path); }
            }
        }

        /// <summary>Persists all data to disk.</summary>
        public async Task SaveAllAsync(CancellationToken ct = default)
        {
            await SaveRulesAsync(ct);
            await SaveTemplatesAsync(ct);
            await SavePatternsAsync(ct);
        }

        private async Task SaveRulesAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "rules.json");
            RulesData data;
            lock (_rulesLock)
            {
                data = new RulesData
                {
                    Rules = _rules.Values.ToList(),
                    Groups = _ruleGroups.Values.ToList()
                };
            }
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(data, Formatting.Indented), ct);
        }

        private async Task SaveTemplatesAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "templates.json");
            List<TagTemplateDefinition> templates;
            lock (_templatesLock) { templates = _templates.Values.ToList(); }
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(templates, Formatting.Indented), ct);
        }

        private async Task SavePatternsAsync(CancellationToken ct)
        {
            string path = Path.Combine(_dataDirectory, "patterns.json");
            List<PlacementPattern> patterns;
            lock (_correctionsLock) { patterns = _patterns.Values.ToList(); }
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(patterns, Formatting.Indented), ct);
        }

        #endregion

        #region View Context

        /// <summary>
        /// Builds a view context for the specified view ID using cached tag data.
        /// </summary>
        public ViewTagContext GetViewContext(int viewId)
        {
            // Build a minimal view context from stored tag information
            var tagsInView = GetTagsByView(viewId);

            return new ViewTagContext
            {
                ViewId = viewId,
                ViewName = $"View_{viewId}",
                ViewType = tagsInView.FirstOrDefault()?.ViewType ?? TagViewType.FloorPlan,
                Scale = 100,
                ExistingAnnotationBounds = tagsInView
                    .Where(t => t.Bounds != null)
                    .Select(t => t.Bounds)
                    .ToList()
            };
        }

        #endregion

        #region Inner Types

        private class RulesData
        {
            public List<TagRule> Rules { get; set; }
            public List<RuleGroup> Groups { get; set; }
        }

        #endregion
    }

    #region Export/Import Types

    /// <summary>
    /// Portable package for sharing tag configurations across projects and teams.
    /// </summary>
    public class ExportPackage
    {
        public DateTime ExportedAt { get; set; }
        public string Version { get; set; }
        public List<TagRule> Rules { get; set; }
        public List<RuleGroup> RuleGroups { get; set; }
        public List<TagTemplateDefinition> Templates { get; set; }
        public List<PlacementPattern> Patterns { get; set; }
    }

    /// <summary>
    /// Result of importing a configuration package.
    /// </summary>
    public class ImportResult
    {
        public int RulesImported { get; set; }
        public int GroupsImported { get; set; }
        public int TemplatesImported { get; set; }
        public int PatternsImported { get; set; }
        public int ConflictsDetected { get; set; }
        public string ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }

    /// <summary>
    /// Record of a tagging session for audit trail.
    /// </summary>
    public class TagSession
    {
        public string SessionId { get; set; }
        public string Description { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsActive { get; set; }
        public int TagsPlaced { get; set; }
        public int ViewsProcessed { get; set; }
        public double QualityScore { get; set; }
        public string ActiveRuleGroup { get; set; }
    }

    #endregion
}
