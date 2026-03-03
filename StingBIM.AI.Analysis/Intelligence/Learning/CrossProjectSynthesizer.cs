// StingBIM.AI.Intelligence.Learning.CrossProjectSynthesizer
// Transfers learned knowledge across projects by maintaining per-project learning profiles,
// synthesizing abstract patterns, and providing recommendations for new projects.
// Implements confidence scoring, domain clustering, conflict resolution, and anti-overfitting.
// Master Proposal Reference: Part 2.2 Strategy 6 - Intelligence Amplification Phase 1

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Learning;
using StingBIM.AI.Core.Memory;
using StingBIM.AI.Knowledge.Graph;

namespace StingBIM.AI.Intelligence.Learning
{
    #region Cross-Project Synthesizer

    /// <summary>
    /// Transfers learned knowledge across projects by maintaining per-project learning profiles,
    /// synthesizing abstract patterns from multiple projects, and providing recommendations
    /// for new projects based on similar past work. Implements confidence scoring based on
    /// project count, domain clustering, conflict resolution, and anti-overfitting guards.
    /// </summary>
    public class CrossProjectSynthesizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly SemanticMemory _semanticMemory;
        private readonly EpisodicMemory _episodicMemory;
        private readonly KnowledgeGraph _knowledgeGraph;

        // Per-project learning profiles
        private readonly ConcurrentDictionary<string, ProjectLearningProfile> _projectProfiles;

        // Synthesized cross-project knowledge
        private readonly ConcurrentDictionary<string, SynthesizedPattern> _synthesizedPatterns;

        // Domain cluster cache
        private readonly ConcurrentDictionary<string, ProjectDomainCluster> _domainClusters;

        // Persistence
        private readonly string _persistenceDirectory;

        // Configuration
        private const int MinProjectsForTransfer = 3;
        private const float MinConfidenceForTransfer = 0.4f;
        private const float RecencyDecayFactor = 0.95f; // Per month
        private const int MaxPatternsPerProject = 500;
        private const int MaxSynthesizedPatterns = 2000;

        // Domain type taxonomy
        private static readonly Dictionary<string, string[]> DomainTypeKeywords =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Residential"] = new[] { "residential", "house", "apartment", "dwelling", "home", "villa", "condo", "flat" },
                ["Commercial"] = new[] { "commercial", "office", "retail", "shop", "mall", "store", "workspace" },
                ["Healthcare"] = new[] { "hospital", "clinic", "healthcare", "medical", "pharmacy", "lab" },
                ["Educational"] = new[] { "school", "university", "college", "classroom", "campus", "education" },
                ["Industrial"] = new[] { "factory", "warehouse", "industrial", "manufacturing", "plant" },
                ["Hospitality"] = new[] { "hotel", "resort", "restaurant", "hospitality", "lodge" },
                ["Institutional"] = new[] { "government", "library", "museum", "church", "religious", "civic" },
                ["MixedUse"] = new[] { "mixed-use", "mixed use", "multi-use" },
                ["Infrastructure"] = new[] { "infrastructure", "bridge", "road", "utility", "tunnel" }
            };

        /// <summary>
        /// Number of project profiles currently tracked.
        /// </summary>
        public int ProjectCount => _projectProfiles.Count;

        /// <summary>
        /// Number of synthesized cross-project patterns.
        /// </summary>
        public int SynthesizedPatternCount => _synthesizedPatterns.Count;

        /// <summary>
        /// Initializes the cross-project synthesizer.
        /// </summary>
        public CrossProjectSynthesizer(
            SemanticMemory semanticMemory,
            EpisodicMemory episodicMemory,
            KnowledgeGraph knowledgeGraph,
            string persistenceDirectory = null)
        {
            _semanticMemory = semanticMemory ?? throw new ArgumentNullException(nameof(semanticMemory));
            _episodicMemory = episodicMemory ?? throw new ArgumentNullException(nameof(episodicMemory));
            _knowledgeGraph = knowledgeGraph ?? throw new ArgumentNullException(nameof(knowledgeGraph));

            _projectProfiles = new ConcurrentDictionary<string, ProjectLearningProfile>(StringComparer.OrdinalIgnoreCase);
            _synthesizedPatterns = new ConcurrentDictionary<string, SynthesizedPattern>(StringComparer.OrdinalIgnoreCase);
            _domainClusters = new ConcurrentDictionary<string, ProjectDomainCluster>(StringComparer.OrdinalIgnoreCase);

            _persistenceDirectory = persistenceDirectory ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StingBIM", "CrossProject");
        }

        #region Project Profile Management

        /// <summary>
        /// Registers a new project or retrieves an existing project's learning profile.
        /// </summary>
        public ProjectLearningProfile GetOrCreateProfile(
            string projectId,
            string projectName = null,
            string projectType = null)
        {
            return _projectProfiles.GetOrAdd(projectId, id => new ProjectLearningProfile
            {
                ProjectId = id,
                ProjectName = projectName ?? id,
                ProjectType = projectType ?? ClassifyProjectType(projectName ?? id),
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Patterns = new List<ProjectPattern>(),
                Preferences = new Dictionary<string, ProjectPreference>(StringComparer.OrdinalIgnoreCase),
                Corrections = new List<ProjectCorrection>(),
                Statistics = new ProjectStatistics()
            });
        }

        /// <summary>
        /// Records a learned pattern for a specific project.
        /// </summary>
        public void RecordProjectPattern(
            string projectId,
            LearnedPattern pattern)
        {
            if (string.IsNullOrEmpty(projectId) || pattern == null) return;

            var profile = GetOrCreateProfile(projectId);

            lock (profile.LockObject)
            {
                // Check for duplicate or similar existing pattern
                var existing = profile.Patterns.FirstOrDefault(p =>
                    string.Equals(p.Key, pattern.Key, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Reinforce existing pattern
                    existing.Confidence = Math.Min(1.0f,
                        existing.Confidence * 0.7f + pattern.Confidence * 0.3f);
                    existing.Occurrences += pattern.Occurrences;
                    existing.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    // Add new pattern (respecting max limit)
                    if (profile.Patterns.Count >= MaxPatternsPerProject)
                    {
                        // Remove lowest confidence pattern
                        var weakest = profile.Patterns
                            .OrderBy(p => p.Confidence)
                            .First();
                        profile.Patterns.Remove(weakest);
                    }

                    profile.Patterns.Add(new ProjectPattern
                    {
                        Key = pattern.Key,
                        Description = pattern.Description,
                        PatternType = pattern.PatternType.ToString(),
                        Confidence = pattern.Confidence,
                        Occurrences = pattern.Occurrences,
                        FirstSeen = pattern.FirstSeen,
                        LastSeen = pattern.LastSeen,
                        Context = pattern.Context != null
                            ? new Dictionary<string, object>(pattern.Context, StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    });
                }

                profile.LastUpdatedAt = DateTime.UtcNow;
                profile.Statistics.TotalPatternsRecorded++;
            }
        }

        /// <summary>
        /// Records a user preference for a specific project.
        /// </summary>
        public void RecordProjectPreference(
            string projectId,
            string preferenceKey,
            string preferredValue,
            float confidence = 0.8f)
        {
            if (string.IsNullOrEmpty(projectId)) return;

            var profile = GetOrCreateProfile(projectId);

            lock (profile.LockObject)
            {
                if (profile.Preferences.TryGetValue(preferenceKey, out var existing))
                {
                    if (existing.Value == preferredValue)
                    {
                        existing.Confidence = Math.Min(1.0f, existing.Confidence + 0.05f);
                        existing.Occurrences++;
                    }
                    else
                    {
                        // Preference changed - update with new value
                        existing.Value = preferredValue;
                        existing.Confidence = confidence;
                        existing.Occurrences = 1;
                    }
                    existing.LastUpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    profile.Preferences[preferenceKey] = new ProjectPreference
                    {
                        Key = preferenceKey,
                        Value = preferredValue,
                        Confidence = confidence,
                        Occurrences = 1,
                        FirstRecordedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                }

                profile.LastUpdatedAt = DateTime.UtcNow;
                profile.Statistics.TotalPreferencesRecorded++;
            }
        }

        /// <summary>
        /// Records a user correction in a project for learning what NOT to do.
        /// </summary>
        public void RecordProjectCorrection(
            string projectId,
            string originalAction,
            string correctedAction,
            string context = null)
        {
            if (string.IsNullOrEmpty(projectId)) return;

            var profile = GetOrCreateProfile(projectId);

            lock (profile.LockObject)
            {
                profile.Corrections.Add(new ProjectCorrection
                {
                    OriginalAction = originalAction,
                    CorrectedAction = correctedAction,
                    Context = context,
                    Timestamp = DateTime.UtcNow
                });

                profile.LastUpdatedAt = DateTime.UtcNow;
                profile.Statistics.TotalCorrectionsRecorded++;
            }
        }

        /// <summary>
        /// Gets a project's full learning profile.
        /// </summary>
        public ProjectLearningProfile GetProjectProfile(string projectId)
        {
            _projectProfiles.TryGetValue(projectId, out var profile);
            return profile;
        }

        /// <summary>
        /// Lists all tracked project IDs.
        /// </summary>
        public IReadOnlyList<string> GetAllProjectIds()
        {
            return _projectProfiles.Keys.ToList().AsReadOnly();
        }

        #endregion

        #region Pattern Synthesis

        /// <summary>
        /// Runs full synthesis across all projects. Finds patterns that appear in multiple
        /// projects, generalizes them, scores confidence, and resolves conflicts.
        /// </summary>
        public SynthesisReport SynthesizeFromAllProjects(
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Starting cross-project pattern synthesis...");
            progress?.Report("Synthesizing patterns across all projects...");

            var report = new SynthesisReport { StartedAt = DateTime.UtcNow };

            try
            {
                var profiles = _projectProfiles.Values.ToList();
                report.ProjectsAnalyzed = profiles.Count;

                if (profiles.Count < MinProjectsForTransfer)
                {
                    Logger.Info($"Only {profiles.Count} projects (minimum {MinProjectsForTransfer}). " +
                                "Skipping synthesis.");
                    report.Skipped = true;
                    report.SkipReason = $"Minimum {MinProjectsForTransfer} projects required, only {profiles.Count} available";
                    report.CompletedAt = DateTime.UtcNow;
                    return report;
                }

                // Step 1: Cluster projects by domain type
                progress?.Report("Clustering projects by domain...");
                var clusters = ClusterProjectsByDomain(profiles);
                report.ClustersFound = clusters.Count;

                // Step 2: Find patterns that appear across multiple projects
                progress?.Report("Finding cross-project patterns...");
                var crossPatterns = FindCrossProjectPatterns(profiles);
                report.CrossProjectPatternsFound = crossPatterns.Count;

                // Step 3: Generalize patterns (strip project-specific details)
                progress?.Report("Generalizing patterns...");
                var generalized = GeneralizePatterns(crossPatterns);
                report.GeneralizedPatterns = generalized.Count;

                // Step 4: Score confidence based on project count and recency
                progress?.Report("Scoring confidence...");
                ScorePatternConfidence(generalized, profiles.Count);

                // Step 5: Resolve conflicts
                progress?.Report("Resolving conflicts...");
                int conflictsResolved = ResolveConflicts(generalized);
                report.ConflictsResolved = conflictsResolved;

                // Step 6: Store synthesized patterns
                progress?.Report("Storing synthesized knowledge...");
                int stored = StoreSynthesizedPatterns(generalized);
                report.PatternsStored = stored;

                // Step 7: Find cross-project preferences
                var preferences = SynthesizePreferences(profiles);
                report.PreferencesSynthesized = preferences.Count;

                // Step 8: Find cross-project corrections (anti-patterns)
                var antiPatterns = SynthesizeAntiPatterns(profiles);
                report.AntiPatternsFound = antiPatterns.Count;

                report.CompletedAt = DateTime.UtcNow;
                report.Success = true;

                Logger.Info($"Synthesis complete: {report.CrossProjectPatternsFound} cross-project patterns, " +
                            $"{report.GeneralizedPatterns} generalized, {report.PatternsStored} stored, " +
                            $"{report.ConflictsResolved} conflicts resolved ({report.Duration.TotalSeconds:F1}s)");

                progress?.Report($"Synthesis complete: {report.PatternsStored} transferable patterns from " +
                                 $"{report.ProjectsAnalyzed} projects");

                return report;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in cross-project synthesis");
                report.CompletedAt = DateTime.UtcNow;
                report.Success = false;
                report.ErrorMessage = ex.Message;
                return report;
            }
        }

        /// <summary>
        /// Clusters projects by their domain type (residential, commercial, etc.).
        /// </summary>
        private Dictionary<string, ProjectDomainCluster> ClusterProjectsByDomain(
            List<ProjectLearningProfile> profiles)
        {
            _domainClusters.Clear();

            foreach (var profile in profiles)
            {
                var domain = profile.ProjectType ?? ClassifyProjectType(profile.ProjectName);
                profile.ProjectType = domain;

                var cluster = _domainClusters.GetOrAdd(domain, d => new ProjectDomainCluster
                {
                    DomainType = d,
                    ProjectIds = new List<string>(),
                    CommonPatterns = new List<SynthesizedPattern>()
                });

                lock (cluster)
                {
                    if (!cluster.ProjectIds.Contains(profile.ProjectId))
                    {
                        cluster.ProjectIds.Add(profile.ProjectId);
                    }
                }
            }

            Logger.Debug($"Clustered {profiles.Count} projects into {_domainClusters.Count} domains: " +
                          $"{string.Join(", ", _domainClusters.Select(c => $"{c.Key}({c.Value.ProjectIds.Count})"))}");

            return _domainClusters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds patterns that appear across multiple projects.
        /// Only patterns appearing in >= MinProjectsForTransfer projects qualify.
        /// </summary>
        private List<CrossProjectPatternMatch> FindCrossProjectPatterns(
            List<ProjectLearningProfile> profiles)
        {
            // Group all patterns by their normalized key
            var patternIndex = new Dictionary<string, List<PatternOccurrence>>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in profiles)
            {
                List<ProjectPattern> patterns;
                lock (profile.LockObject)
                {
                    patterns = profile.Patterns.ToList();
                }

                foreach (var pattern in patterns)
                {
                    var normalizedKey = NormalizePatternKey(pattern.Key, pattern.Description);

                    if (!patternIndex.TryGetValue(normalizedKey, out var occurrences))
                    {
                        occurrences = new List<PatternOccurrence>();
                        patternIndex[normalizedKey] = occurrences;
                    }

                    occurrences.Add(new PatternOccurrence
                    {
                        ProjectId = profile.ProjectId,
                        ProjectType = profile.ProjectType,
                        Pattern = pattern,
                        ProjectLastUpdated = profile.LastUpdatedAt
                    });
                }
            }

            // Filter to patterns that appear in enough projects (anti-overfitting)
            var crossPatterns = patternIndex
                .Where(kvp => kvp.Value.Select(o => o.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                              >= MinProjectsForTransfer)
                .Select(kvp => new CrossProjectPatternMatch
                {
                    NormalizedKey = kvp.Key,
                    Occurrences = kvp.Value,
                    ProjectCount = kvp.Value.Select(o => o.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    DomainTypes = kvp.Value.Select(o => o.ProjectType).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                })
                .OrderByDescending(cp => cp.ProjectCount)
                .ToList();

            Logger.Debug($"Found {crossPatterns.Count} cross-project patterns " +
                          $"(from {patternIndex.Count} unique pattern keys)");

            return crossPatterns;
        }

        /// <summary>
        /// Generalizes patterns by stripping project-specific details.
        /// Keeps universal principles and domain-relevant characteristics.
        /// </summary>
        private List<SynthesizedPattern> GeneralizePatterns(
            List<CrossProjectPatternMatch> crossPatterns)
        {
            var generalized = new List<SynthesizedPattern>();

            foreach (var match in crossPatterns)
            {
                // Merge occurrences to create a generalized pattern
                var bestOccurrence = match.Occurrences
                    .OrderByDescending(o => o.Pattern.Confidence)
                    .First();

                var synthesized = new SynthesizedPattern
                {
                    Id = $"SYNTH-{Guid.NewGuid():N}".Substring(0, 24),
                    NormalizedKey = match.NormalizedKey,
                    Description = GeneralizeDescription(bestOccurrence.Pattern.Description, match.DomainTypes),
                    PatternType = bestOccurrence.Pattern.PatternType,
                    ProjectCount = match.ProjectCount,
                    DomainTypes = match.DomainTypes,
                    IsUniversal = match.DomainTypes.Count >= 2,
                    SourceProjectIds = match.Occurrences
                        .Select(o => o.ProjectId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    MergedConfidence = match.Occurrences.Average(o => o.Pattern.Confidence),
                    TotalOccurrences = match.Occurrences.Sum(o => o.Pattern.Occurrences),
                    FirstSeen = match.Occurrences.Min(o => o.Pattern.FirstSeen),
                    LastSeen = match.Occurrences.Max(o => o.Pattern.LastSeen),
                    SynthesizedAt = DateTime.UtcNow,
                    GeneralizedContext = MergeContexts(match.Occurrences)
                };

                generalized.Add(synthesized);
            }

            return generalized;
        }

        /// <summary>
        /// Generalizes a pattern description by removing project-specific references.
        /// </summary>
        private string GeneralizeDescription(string description, List<string> domainTypes)
        {
            if (string.IsNullOrEmpty(description)) return "Cross-project learned pattern";

            // Strip project-specific identifiers (project names, IDs, specific rooms)
            var generalized = description;

            // Add domain context if pattern is domain-specific
            if (domainTypes.Count == 1)
            {
                generalized = $"[{domainTypes[0]}] {generalized}";
            }
            else if (domainTypes.Count > 1)
            {
                generalized = $"[Universal] {generalized}";
            }

            return generalized;
        }

        /// <summary>
        /// Merges context dictionaries from multiple pattern occurrences.
        /// Keeps values that are consistent across projects.
        /// </summary>
        private Dictionary<string, object> MergeContexts(List<PatternOccurrence> occurrences)
        {
            var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var valueCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var occ in occurrences)
            {
                if (occ.Pattern.Context == null) continue;

                foreach (var kvp in occ.Pattern.Context)
                {
                    var valueStr = kvp.Value?.ToString() ?? "null";

                    if (!valueCounts.TryGetValue(kvp.Key, out var counts))
                    {
                        counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        valueCounts[kvp.Key] = counts;
                    }

                    if (!counts.ContainsKey(valueStr)) counts[valueStr] = 0;
                    counts[valueStr]++;
                }
            }

            // Only keep context values that are consistent (majority agreement)
            foreach (var kvp in valueCounts)
            {
                var topValue = kvp.Value
                    .OrderByDescending(v => v.Value)
                    .First();

                double agreement = (double)topValue.Value / occurrences.Count;
                if (agreement >= 0.5) // At least 50% agreement
                {
                    merged[kvp.Key] = topValue.Key;
                }
            }

            return merged;
        }

        /// <summary>
        /// Scores confidence for synthesized patterns based on project count and recency.
        /// More projects = higher confidence. More recent = higher weight.
        /// </summary>
        private void ScorePatternConfidence(
            List<SynthesizedPattern> patterns,
            int totalProjectCount)
        {
            foreach (var pattern in patterns)
            {
                // Base: frequency across projects (0.0 - 1.0)
                double frequencyScore = Math.Min(1.0, (double)pattern.ProjectCount / totalProjectCount);

                // Recency: months since last seen, with decay
                double monthsSinceLastSeen = (DateTime.UtcNow - pattern.LastSeen).TotalDays / 30.0;
                double recencyScore = Math.Pow(RecencyDecayFactor, monthsSinceLastSeen);

                // Consistency: merged confidence from individual projects
                double consistencyScore = pattern.MergedConfidence;

                // Universality bonus: patterns spanning multiple domains are more reliable
                double universalityBonus = pattern.IsUniversal ? 0.1 : 0.0;

                // Occurrence volume bonus
                double volumeBonus = Math.Min(0.1, pattern.TotalOccurrences / 100.0);

                // Final confidence: weighted combination
                pattern.TransferConfidence = (float)Math.Min(1.0,
                    frequencyScore * 0.35 +
                    recencyScore * 0.20 +
                    consistencyScore * 0.30 +
                    universalityBonus +
                    volumeBonus +
                    0.05); // Base floor

                // Anti-overfitting: penalize if only from minimum project count
                if (pattern.ProjectCount == MinProjectsForTransfer)
                {
                    pattern.TransferConfidence *= 0.8f;
                }
            }
        }

        /// <summary>
        /// Resolves conflicts when different projects have contradictory patterns.
        /// Uses frequency + recency weighting.
        /// </summary>
        private int ResolveConflicts(List<SynthesizedPattern> patterns)
        {
            int resolved = 0;

            // Group patterns by normalized key to find contradictions
            var groups = patterns
                .GroupBy(p => p.NormalizedKey, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var conflicting = group.OrderByDescending(p => p.TransferConfidence).ToList();

                // Keep the highest confidence version, mark others as conflicts
                var winner = conflicting.First();
                winner.ConflictsResolved = conflicting.Count - 1;

                for (int i = 1; i < conflicting.Count; i++)
                {
                    patterns.Remove(conflicting[i]);
                    resolved++;
                }
            }

            return resolved;
        }

        /// <summary>
        /// Stores synthesized patterns in the persistent dictionary and semantic memory.
        /// </summary>
        private int StoreSynthesizedPatterns(List<SynthesizedPattern> patterns)
        {
            int stored = 0;

            foreach (var pattern in patterns.Where(p => p.TransferConfidence >= MinConfidenceForTransfer))
            {
                // Respect max limit
                if (_synthesizedPatterns.Count >= MaxSynthesizedPatterns)
                {
                    // Evict the weakest pattern
                    var weakest = _synthesizedPatterns.Values
                        .OrderBy(p => p.TransferConfidence)
                        .FirstOrDefault();

                    if (weakest != null && weakest.TransferConfidence < pattern.TransferConfidence)
                    {
                        _synthesizedPatterns.TryRemove(weakest.Id, out _);
                    }
                    else
                    {
                        continue; // All existing patterns are stronger
                    }
                }

                _synthesizedPatterns[pattern.Id] = pattern;

                // Also store as semantic fact for retrieval
                _semanticMemory.StoreFact(new SemanticFact
                {
                    Id = $"CROSSPROJ-{pattern.Id}",
                    Subject = pattern.NormalizedKey,
                    Predicate = "isCrossProjectPattern",
                    Object = string.Join(",", pattern.DomainTypes),
                    Description = $"Cross-project pattern (from {pattern.ProjectCount} projects): {pattern.Description}",
                    Category = "CrossProjectKnowledge",
                    Source = "CrossProjectSynthesizer",
                    Confidence = pattern.TransferConfidence,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ProjectCount"] = pattern.ProjectCount,
                        ["IsUniversal"] = pattern.IsUniversal,
                        ["DomainTypes"] = string.Join(",", pattern.DomainTypes),
                        ["TotalOccurrences"] = pattern.TotalOccurrences
                    }
                });

                stored++;
            }

            return stored;
        }

        /// <summary>
        /// Synthesizes common preferences across projects.
        /// </summary>
        private List<SynthesizedPreference> SynthesizePreferences(
            List<ProjectLearningProfile> profiles)
        {
            var prefIndex = new Dictionary<string, List<(string Value, float Confidence, string ProjectId)>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var profile in profiles)
            {
                Dictionary<string, ProjectPreference> prefs;
                lock (profile.LockObject)
                {
                    prefs = new Dictionary<string, ProjectPreference>(profile.Preferences, StringComparer.OrdinalIgnoreCase);
                }

                foreach (var pref in prefs)
                {
                    if (!prefIndex.TryGetValue(pref.Key, out var values))
                    {
                        values = new List<(string, float, string)>();
                        prefIndex[pref.Key] = values;
                    }
                    values.Add((pref.Value.Value, pref.Value.Confidence, profile.ProjectId));
                }
            }

            var synthesized = new List<SynthesizedPreference>();

            foreach (var kvp in prefIndex)
            {
                var projectCount = kvp.Value.Select(v => v.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (projectCount < MinProjectsForTransfer) continue;

                // Find the most common value
                var valueCounts = kvp.Value
                    .GroupBy(v => v.Value, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .First();

                synthesized.Add(new SynthesizedPreference
                {
                    Key = kvp.Key,
                    Value = valueCounts.Key,
                    ProjectCount = projectCount,
                    Confidence = valueCounts.Average(v => v.Confidence),
                    Agreement = (double)valueCounts.Count() / kvp.Value.Count
                });
            }

            return synthesized;
        }

        /// <summary>
        /// Finds anti-patterns: actions that are frequently corrected across projects.
        /// </summary>
        private List<SynthesizedAntiPattern> SynthesizeAntiPatterns(
            List<ProjectLearningProfile> profiles)
        {
            var correctionIndex = new Dictionary<string, List<ProjectCorrection>>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in profiles)
            {
                List<ProjectCorrection> corrections;
                lock (profile.LockObject)
                {
                    corrections = profile.Corrections.ToList();
                }

                foreach (var correction in corrections)
                {
                    var key = NormalizePatternKey(correction.OriginalAction, correction.OriginalAction);

                    if (!correctionIndex.TryGetValue(key, out var list))
                    {
                        list = new List<ProjectCorrection>();
                        correctionIndex[key] = list;
                    }
                    list.Add(correction);
                }
            }

            var antiPatterns = correctionIndex
                .Where(kvp => kvp.Value.Count >= MinProjectsForTransfer)
                .Select(kvp => new SynthesizedAntiPattern
                {
                    OriginalAction = kvp.Key,
                    CorrectedActions = kvp.Value
                        .Select(c => c.CorrectedAction)
                        .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .ToList(),
                    OccurrenceCount = kvp.Value.Count,
                    Confidence = Math.Min(1.0f, kvp.Value.Count / 10.0f)
                })
                .ToList();

            return antiPatterns;
        }

        #endregion

        #region Transfer Learning for New Projects

        /// <summary>
        /// Returns pre-loaded patterns and recommendations for a new project based on
        /// its type and similarity to past projects. This is the main API for transfer learning.
        /// </summary>
        public TransferRecommendations GetRecommendationsForNewProject(
            string projectType,
            string projectName = null,
            Dictionary<string, object> projectContext = null)
        {
            Logger.Info($"Getting transfer recommendations for new {projectType} project");

            var recommendations = new TransferRecommendations
            {
                ProjectType = projectType,
                GeneratedAt = DateTime.UtcNow
            };

            // 1. Find domain-specific patterns
            var domainPatterns = _synthesizedPatterns.Values
                .Where(p => p.DomainTypes.Any(d =>
                    string.Equals(d, projectType, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(p => p.TransferConfidence)
                .ToList();

            recommendations.DomainSpecificPatterns = domainPatterns
                .Select(p => new TransferablePattern
                {
                    Key = p.NormalizedKey,
                    Description = p.Description,
                    Confidence = p.TransferConfidence,
                    Source = $"From {p.ProjectCount} {projectType} projects",
                    PatternType = p.PatternType,
                    Context = p.GeneralizedContext
                })
                .ToList();

            // 2. Find universal patterns (applicable to all project types)
            var universalPatterns = _synthesizedPatterns.Values
                .Where(p => p.IsUniversal)
                .OrderByDescending(p => p.TransferConfidence)
                .ToList();

            recommendations.UniversalPatterns = universalPatterns
                .Select(p => new TransferablePattern
                {
                    Key = p.NormalizedKey,
                    Description = p.Description,
                    Confidence = p.TransferConfidence,
                    Source = $"Universal pattern from {p.ProjectCount} projects",
                    PatternType = p.PatternType,
                    Context = p.GeneralizedContext
                })
                .ToList();

            // 3. Find similar past projects
            recommendations.SimilarProjects = FindSimilarProjects(projectType, projectContext);

            // 4. Get domain-specific preferences
            var domainProfiles = _projectProfiles.Values
                .Where(p => string.Equals(p.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (domainProfiles.Count >= MinProjectsForTransfer)
            {
                recommendations.SuggestedPreferences = SynthesizePreferences(domainProfiles)
                    .Select(p => new SuggestedPreference
                    {
                        Key = p.Key,
                        Value = p.Value,
                        Confidence = (float)p.Confidence,
                        Agreement = p.Agreement,
                        ProjectCount = p.ProjectCount
                    })
                    .ToList();
            }

            // 5. Get relevant anti-patterns to avoid
            recommendations.AntiPatterns = GetAntiPatternsForDomain(projectType);

            // 6. Compute overall readiness score
            recommendations.TransferReadinessScore = ComputeTransferReadiness(recommendations);

            Logger.Info($"Transfer recommendations: {recommendations.DomainSpecificPatterns.Count} domain patterns, " +
                        $"{recommendations.UniversalPatterns.Count} universal, " +
                        $"{recommendations.SimilarProjects.Count} similar projects, " +
                        $"readiness: {recommendations.TransferReadinessScore:P0}");

            return recommendations;
        }

        /// <summary>
        /// Finds projects most similar to the target project type.
        /// </summary>
        private List<SimilarProjectSummary> FindSimilarProjects(
            string projectType,
            Dictionary<string, object> context = null)
        {
            return _projectProfiles.Values
                .Where(p => string.Equals(p.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.LastUpdatedAt)
                .Take(5)
                .Select(p => new SimilarProjectSummary
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    ProjectType = p.ProjectType,
                    PatternCount = p.Patterns.Count,
                    PreferenceCount = p.Preferences.Count,
                    LastUpdated = p.LastUpdatedAt,
                    SimilarityScore = ComputeSimilarityScore(p, projectType, context)
                })
                .ToList();
        }

        /// <summary>
        /// Computes similarity score between a project profile and target criteria.
        /// </summary>
        private float ComputeSimilarityScore(
            ProjectLearningProfile profile,
            string targetType,
            Dictionary<string, object> context)
        {
            float score = 0;

            // Type match: 0.5 points
            if (string.Equals(profile.ProjectType, targetType, StringComparison.OrdinalIgnoreCase))
                score += 0.5f;

            // Recency: 0.3 points (more recent = higher)
            double monthsAgo = (DateTime.UtcNow - profile.LastUpdatedAt).TotalDays / 30.0;
            score += (float)(0.3 * Math.Pow(RecencyDecayFactor, monthsAgo));

            // Data richness: 0.2 points (more patterns = more useful)
            int totalData = profile.Patterns.Count + profile.Preferences.Count;
            score += (float)Math.Min(0.2, totalData / 200.0);

            return Math.Min(1.0f, score);
        }

        /// <summary>
        /// Gets anti-patterns relevant to a domain.
        /// </summary>
        private List<AntiPatternWarning> GetAntiPatternsForDomain(string projectType)
        {
            // Find corrections common in projects of this type
            var domainProfiles = _projectProfiles.Values
                .Where(p => string.Equals(p.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (domainProfiles.Count < MinProjectsForTransfer)
                return new List<AntiPatternWarning>();

            var correctionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var correctedTo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var profile in domainProfiles)
            {
                List<ProjectCorrection> corrections;
                lock (profile.LockObject)
                {
                    corrections = profile.Corrections.ToList();
                }

                foreach (var c in corrections)
                {
                    var key = c.OriginalAction;
                    if (!correctionCounts.ContainsKey(key)) correctionCounts[key] = 0;
                    correctionCounts[key]++;

                    if (!string.IsNullOrEmpty(c.CorrectedAction))
                    {
                        correctedTo[key] = c.CorrectedAction;
                    }
                }
            }

            return correctionCounts
                .Where(kvp => kvp.Value >= 2)
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new AntiPatternWarning
                {
                    Action = kvp.Key,
                    TimesCorreced = kvp.Value,
                    SuggestedAlternative = correctedTo.TryGetValue(kvp.Key, out var alt) ? alt : null,
                    Severity = kvp.Value >= 5 ? "High" : kvp.Value >= 3 ? "Medium" : "Low"
                })
                .ToList();
        }

        /// <summary>
        /// Computes an overall readiness score for transfer learning.
        /// </summary>
        private float ComputeTransferReadiness(TransferRecommendations recommendations)
        {
            float score = 0;

            // Domain patterns available: 0-40 points
            if (recommendations.DomainSpecificPatterns.Count > 0)
                score += Math.Min(0.4f, recommendations.DomainSpecificPatterns.Count / 50.0f);

            // Universal patterns: 0-20 points
            if (recommendations.UniversalPatterns.Count > 0)
                score += Math.Min(0.2f, recommendations.UniversalPatterns.Count / 30.0f);

            // Similar projects: 0-20 points
            if (recommendations.SimilarProjects.Count > 0)
                score += Math.Min(0.2f, recommendations.SimilarProjects.Count / 5.0f);

            // Preferences: 0-10 points
            if (recommendations.SuggestedPreferences?.Count > 0)
                score += Math.Min(0.1f, recommendations.SuggestedPreferences.Count / 20.0f);

            // Anti-patterns: 0-10 points (having warnings is helpful)
            if (recommendations.AntiPatterns?.Count > 0)
                score += Math.Min(0.1f, recommendations.AntiPatterns.Count / 10.0f);

            return Math.Min(1.0f, score);
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Saves all synthesized knowledge and project profiles to JSON files.
        /// </summary>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(_persistenceDirectory))
                {
                    Directory.CreateDirectory(_persistenceDirectory);
                }

                // Save project profiles
                var profilesJson = JsonConvert.SerializeObject(
                    _projectProfiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Formatting.Indented,
                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                var profilesPath = Path.Combine(_persistenceDirectory, "project_profiles.json");
                await Task.Run(() => File.WriteAllText(profilesPath, profilesJson), cancellationToken);

                // Save synthesized patterns
                var patternsJson = JsonConvert.SerializeObject(
                    _synthesizedPatterns.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Formatting.Indented);
                var patternsPath = Path.Combine(_persistenceDirectory, "synthesized_patterns.json");
                await Task.Run(() => File.WriteAllText(patternsPath, patternsJson), cancellationToken);

                Logger.Info($"Saved {_projectProfiles.Count} project profiles and " +
                            $"{_synthesizedPatterns.Count} synthesized patterns");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error saving cross-project data");
            }
        }

        /// <summary>
        /// Loads previously saved project profiles and synthesized patterns.
        /// </summary>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Load project profiles
                var profilesPath = Path.Combine(_persistenceDirectory, "project_profiles.json");
                if (File.Exists(profilesPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(profilesPath), cancellationToken);
                    var profiles = JsonConvert.DeserializeObject<
                        Dictionary<string, ProjectLearningProfile>>(json);

                    if (profiles != null)
                    {
                        foreach (var kvp in profiles)
                        {
                            _projectProfiles[kvp.Key] = kvp.Value;
                        }
                    }

                    Logger.Info($"Loaded {_projectProfiles.Count} project profiles");
                }

                // Load synthesized patterns
                var patternsPath = Path.Combine(_persistenceDirectory, "synthesized_patterns.json");
                if (File.Exists(patternsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(patternsPath), cancellationToken);
                    var patterns = JsonConvert.DeserializeObject<
                        Dictionary<string, SynthesizedPattern>>(json);

                    if (patterns != null)
                    {
                        foreach (var kvp in patterns)
                        {
                            _synthesizedPatterns[kvp.Key] = kvp.Value;
                        }
                    }

                    Logger.Info($"Loaded {_synthesizedPatterns.Count} synthesized patterns");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error loading cross-project data");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Classifies a project into a domain type based on its name.
        /// </summary>
        private string ClassifyProjectType(string projectName)
        {
            if (string.IsNullOrEmpty(projectName)) return "General";

            var lower = projectName.ToLowerInvariant();

            foreach (var kvp in DomainTypeKeywords)
            {
                if (kvp.Value.Any(keyword => lower.Contains(keyword)))
                {
                    return kvp.Key;
                }
            }

            return "General";
        }

        /// <summary>
        /// Normalizes a pattern key for cross-project comparison.
        /// Strips project-specific prefixes and normalizes casing.
        /// </summary>
        private string NormalizePatternKey(string key, string description = null)
        {
            var normalized = (key ?? description ?? "unknown").ToLowerInvariant().Trim();

            // Remove common project-specific prefixes
            var prefixesToRemove = new[] { "proj-", "prj-", "session-", "ses-" };
            foreach (var prefix in prefixesToRemove)
            {
                if (normalized.StartsWith(prefix))
                {
                    normalized = normalized.Substring(prefix.Length);
                }
            }

            // Remove GUIDs and numeric IDs
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized, @"[0-9a-f]{8,}", "{id}");
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized, @"\d{4,}", "{num}");

            return normalized;
        }

        #endregion
    }

    #endregion

    #region Project Profile Types

    /// <summary>
    /// Learning profile for a single project.
    /// </summary>
    public class ProjectLearningProfile
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public List<ProjectPattern> Patterns { get; set; } = new List<ProjectPattern>();
        public Dictionary<string, ProjectPreference> Preferences { get; set; } =
            new Dictionary<string, ProjectPreference>(StringComparer.OrdinalIgnoreCase);
        public List<ProjectCorrection> Corrections { get; set; } = new List<ProjectCorrection>();
        public ProjectStatistics Statistics { get; set; } = new ProjectStatistics();

        [JsonIgnore]
        public readonly object LockObject = new object();
    }

    public class ProjectPattern
    {
        public string Key { get; set; }
        public string Description { get; set; }
        public string PatternType { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public Dictionary<string, object> Context { get; set; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public class ProjectPreference
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
        public DateTime FirstRecordedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class ProjectCorrection
    {
        public string OriginalAction { get; set; }
        public string CorrectedAction { get; set; }
        public string Context { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ProjectStatistics
    {
        public int TotalPatternsRecorded { get; set; }
        public int TotalPreferencesRecorded { get; set; }
        public int TotalCorrectionsRecorded { get; set; }
    }

    #endregion

    #region Synthesis Types

    public class SynthesizedPattern
    {
        public string Id { get; set; }
        public string NormalizedKey { get; set; }
        public string Description { get; set; }
        public string PatternType { get; set; }
        public int ProjectCount { get; set; }
        public List<string> DomainTypes { get; set; } = new List<string>();
        public bool IsUniversal { get; set; }
        public List<string> SourceProjectIds { get; set; } = new List<string>();
        public float MergedConfidence { get; set; }
        public float TransferConfidence { get; set; }
        public int TotalOccurrences { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime SynthesizedAt { get; set; }
        public int ConflictsResolved { get; set; }
        public Dictionary<string, object> GeneralizedContext { get; set; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public class ProjectDomainCluster
    {
        public string DomainType { get; set; }
        public List<string> ProjectIds { get; set; } = new List<string>();
        public List<SynthesizedPattern> CommonPatterns { get; set; } = new List<SynthesizedPattern>();
    }

    internal class CrossProjectPatternMatch
    {
        public string NormalizedKey { get; set; }
        public List<PatternOccurrence> Occurrences { get; set; }
        public int ProjectCount { get; set; }
        public List<string> DomainTypes { get; set; }
    }

    internal class PatternOccurrence
    {
        public string ProjectId { get; set; }
        public string ProjectType { get; set; }
        public ProjectPattern Pattern { get; set; }
        public DateTime ProjectLastUpdated { get; set; }
    }

    internal class SynthesizedPreference
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public int ProjectCount { get; set; }
        public float Confidence { get; set; }
        public double Agreement { get; set; }
    }

    internal class SynthesizedAntiPattern
    {
        public string OriginalAction { get; set; }
        public List<string> CorrectedActions { get; set; }
        public int OccurrenceCount { get; set; }
        public float Confidence { get; set; }
    }

    #endregion

    #region Transfer Recommendation Types

    /// <summary>
    /// Comprehensive transfer learning recommendations for a new project.
    /// </summary>
    public class TransferRecommendations
    {
        public string ProjectType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public float TransferReadinessScore { get; set; }

        public List<TransferablePattern> DomainSpecificPatterns { get; set; } = new List<TransferablePattern>();
        public List<TransferablePattern> UniversalPatterns { get; set; } = new List<TransferablePattern>();
        public List<SimilarProjectSummary> SimilarProjects { get; set; } = new List<SimilarProjectSummary>();
        public List<SuggestedPreference> SuggestedPreferences { get; set; } = new List<SuggestedPreference>();
        public List<AntiPatternWarning> AntiPatterns { get; set; } = new List<AntiPatternWarning>();

        public int TotalRecommendations =>
            DomainSpecificPatterns.Count + UniversalPatterns.Count +
            (SuggestedPreferences?.Count ?? 0) + (AntiPatterns?.Count ?? 0);
    }

    public class TransferablePattern
    {
        public string Key { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public string Source { get; set; }
        public string PatternType { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class SimilarProjectSummary
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectType { get; set; }
        public int PatternCount { get; set; }
        public int PreferenceCount { get; set; }
        public DateTime LastUpdated { get; set; }
        public float SimilarityScore { get; set; }
    }

    public class SuggestedPreference
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public float Confidence { get; set; }
        public double Agreement { get; set; }
        public int ProjectCount { get; set; }
    }

    public class AntiPatternWarning
    {
        public string Action { get; set; }
        public int TimesCorreced { get; set; }
        public string SuggestedAlternative { get; set; }
        public string Severity { get; set; }
    }

    #endregion

    #region Synthesis Report

    /// <summary>
    /// Report from a full cross-project synthesis run.
    /// </summary>
    public class SynthesisReport
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public string SkipReason { get; set; }
        public string ErrorMessage { get; set; }

        public int ProjectsAnalyzed { get; set; }
        public int ClustersFound { get; set; }
        public int CrossProjectPatternsFound { get; set; }
        public int GeneralizedPatterns { get; set; }
        public int ConflictsResolved { get; set; }
        public int PatternsStored { get; set; }
        public int PreferencesSynthesized { get; set; }
        public int AntiPatternsFound { get; set; }

        public override string ToString()
        {
            if (Skipped) return $"Synthesis skipped: {SkipReason}";
            if (!Success) return $"Synthesis failed: {ErrorMessage}";

            return $"Cross-Project Synthesis: {ProjectsAnalyzed} projects, " +
                   $"{ClustersFound} clusters, {CrossProjectPatternsFound} cross-project patterns, " +
                   $"{GeneralizedPatterns} generalized, {PatternsStored} stored, " +
                   $"{ConflictsResolved} conflicts resolved, " +
                   $"{PreferencesSynthesized} preferences, {AntiPatternsFound} anti-patterns " +
                   $"({Duration.TotalSeconds:F1}s)";
        }
    }

    #endregion
}
