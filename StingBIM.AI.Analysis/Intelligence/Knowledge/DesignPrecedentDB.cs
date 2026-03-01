// StingBIM.AI.Intelligence.Knowledge.DesignPrecedentDB
// Indexed database of design solutions searchable by problem type, building type,
// system type, climate zone, and scale. Sources include CSV case studies,
// learned user patterns, building code solutions, and episodic memory mining.
// Master Proposal Reference: Part 2.3 - Phase 3 Active Intelligence

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Knowledge
{
    #region Design Precedent Database

    /// <summary>
    /// Indexed database of design solutions searchable by problem type.
    /// Stores precedents as Problem -> Context -> Solution -> Outcome -> Lessons.
    /// Supports similarity search, solution adaptation, success tracking,
    /// and principle extraction from multiple similar precedents.
    /// </summary>
    public class DesignPrecedentDB
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Primary storage
        private readonly ConcurrentDictionary<string, DesignPrecedent> _precedents;

        // Indexes for fast lookup
        private readonly ConcurrentDictionary<string, HashSet<string>> _buildingTypeIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _systemTypeIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _problemCategoryIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _climateZoneIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _scaleIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _buildingCodeIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _tagIndex;

        // Extracted principles
        private readonly ConcurrentDictionary<string, DesignPrinciple> _extractedPrinciples;

        // Configuration
        private readonly DesignPrecedentConfiguration _configuration;
        private int _totalSuccessfulApplications;
        private int _totalFailedApplications;

        public int PrecedentCount => _precedents.Count;
        public int PrincipleCount => _extractedPrinciples.Count;

        public DesignPrecedentDB()
            : this(new DesignPrecedentConfiguration())
        {
        }

        public DesignPrecedentDB(DesignPrecedentConfiguration configuration)
        {
            _configuration = configuration ?? new DesignPrecedentConfiguration();
            _precedents = new ConcurrentDictionary<string, DesignPrecedent>(StringComparer.OrdinalIgnoreCase);
            _buildingTypeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _systemTypeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _problemCategoryIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _climateZoneIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _scaleIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _buildingCodeIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _tagIndex = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _extractedPrinciples = new ConcurrentDictionary<string, DesignPrinciple>(StringComparer.OrdinalIgnoreCase);
            _totalSuccessfulApplications = 0;
            _totalFailedApplications = 0;

            Logger.Info("DesignPrecedentDB initialized");
        }

        #region Adding Precedents

        /// <summary>
        /// Stores a new design precedent in the database with full indexing.
        /// </summary>
        public void AddPrecedent(DesignPrecedent precedent)
        {
            if (precedent == null)
                throw new ArgumentNullException(nameof(precedent));

            if (string.IsNullOrEmpty(precedent.PrecedentId))
                precedent.PrecedentId = GeneratePrecedentId();

            precedent.AddedAt = DateTime.UtcNow;
            precedent.LastUsed = DateTime.MinValue;

            _precedents[precedent.PrecedentId] = precedent;

            // Build indexes
            IndexPrecedent(precedent);

            Logger.Debug("Added precedent '{0}': {1} (building: {2}, system: {3})",
                precedent.PrecedentId, TruncateForLog(precedent.Problem.Description, 60),
                precedent.Context.BuildingType, precedent.Context.SystemType);
        }

        /// <summary>
        /// Bulk-loads precedents from a list.
        /// </summary>
        public int AddPrecedents(IEnumerable<DesignPrecedent> precedents)
        {
            int count = 0;
            foreach (var precedent in precedents)
            {
                try
                {
                    AddPrecedent(precedent);
                    count++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to add precedent: {0}", precedent?.PrecedentId);
                }
            }
            Logger.Info("Bulk-loaded {0} precedents", count);
            return count;
        }

        /// <summary>
        /// Loads precedents from CSV case study files.
        /// Parses CASE_STUDIES_PROJECTS.csv and CASE_STUDIES_LESSONS.csv.
        /// </summary>
        public async Task<int> LoadFromCsvAsync(
            string csvDirectory,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Loading precedents from CSV directory: {0}", csvDirectory);
            progress?.Report("Loading design precedents from CSV files...");

            int totalLoaded = 0;

            // Load from CASE_STUDIES_PROJECTS.csv
            var projectsFile = Path.Combine(csvDirectory, "CASE_STUDIES_PROJECTS.csv");
            if (File.Exists(projectsFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading case study projects...");
                var projectPrecedents = await LoadProjectsCsvAsync(projectsFile, cancellationToken);
                totalLoaded += AddPrecedents(projectPrecedents);
                Logger.Info("Loaded {0} precedents from CASE_STUDIES_PROJECTS.csv", projectPrecedents.Count);
            }
            else
            {
                Logger.Warn("CASE_STUDIES_PROJECTS.csv not found at {0}", projectsFile);
            }

            // Load from CASE_STUDIES_LESSONS.csv
            var lessonsFile = Path.Combine(csvDirectory, "CASE_STUDIES_LESSONS.csv");
            if (File.Exists(lessonsFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading case study lessons...");
                var lessonPrecedents = await LoadLessonsCsvAsync(lessonsFile, cancellationToken);
                totalLoaded += AddPrecedents(lessonPrecedents);
                Logger.Info("Loaded {0} precedents from CASE_STUDIES_LESSONS.csv", lessonPrecedents.Count);
            }
            else
            {
                Logger.Warn("CASE_STUDIES_LESSONS.csv not found at {0}", lessonsFile);
            }

            // Load from building code solutions
            var codesFile = Path.Combine(csvDirectory, "BUILDING_CODES_ULTRA_COMPREHENSIVE.csv");
            if (File.Exists(codesFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Loading building code precedents...");
                var codePrecedents = await LoadBuildingCodesCsvAsync(codesFile, cancellationToken);
                totalLoaded += AddPrecedents(codePrecedents);
                Logger.Info("Loaded {0} precedents from building codes", codePrecedents.Count);
            }

            progress?.Report($"Loaded {totalLoaded} design precedents from CSV files");
            return totalLoaded;
        }

        /// <summary>
        /// Adds a precedent learned from a user session (auto-populated).
        /// </summary>
        public void AddLearnedPrecedent(
            string problemDescription,
            string solutionDescription,
            PrecedentContext context,
            PrecedentOutcome outcome)
        {
            var precedent = new DesignPrecedent
            {
                PrecedentId = GeneratePrecedentId(),
                Source = PrecedentSource.LearnedFromUser,
                Problem = new PrecedentProblem
                {
                    Description = problemDescription,
                    Category = context?.ProblemCategory ?? "General",
                    Severity = ProblemSeverity.Medium
                },
                Context = context ?? new PrecedentContext(),
                Solution = new PrecedentSolution
                {
                    Description = solutionDescription,
                    Approach = SolutionApproach.Empirical,
                    ImplementationSteps = new List<string> { solutionDescription }
                },
                Outcome = outcome ?? new PrecedentOutcome { Success = true },
                Lessons = new List<string>(),
                Tags = new List<string> { "learned", "user-session" },
                Confidence = 0.6f
            };

            AddPrecedent(precedent);
        }

        #endregion

        #region Searching and Retrieval

        /// <summary>
        /// Finds precedents most similar to the given problem context.
        /// Uses multi-dimensional similarity scoring across building type,
        /// system type, problem category, climate zone, and scale.
        /// </summary>
        public async Task<List<PrecedentSearchResult>> FindSimilarPrecedentsAsync(
            ProblemContext problemContext,
            int maxResults = 10,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (problemContext == null)
                throw new ArgumentNullException(nameof(problemContext));

            Logger.Debug("Searching for precedents similar to: {0}", problemContext.Description);
            progress?.Report("Searching design precedent database...");

            // Get candidate precedents using indexes
            var candidates = GetCandidatePrecedents(problemContext);

            if (!candidates.Any())
            {
                // Fall back to all precedents if no index matches
                candidates = _precedents.Values.ToList();
            }

            // Score each candidate
            var scoredResults = new List<PrecedentSearchResult>();

            await Task.Run(() =>
            {
                foreach (var precedent in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var similarity = CalculateSimilarity(problemContext, precedent);
                    if (similarity >= _configuration.MinSimilarityThreshold)
                    {
                        scoredResults.Add(new PrecedentSearchResult
                        {
                            Precedent = precedent,
                            SimilarityScore = similarity,
                            MatchDimensions = IdentifyMatchDimensions(problemContext, precedent),
                            AdaptationNotes = GenerateAdaptationNotes(problemContext, precedent)
                        });
                    }
                }
            }, cancellationToken);

            var results = scoredResults
                .OrderByDescending(r => r.SimilarityScore)
                .ThenByDescending(r => r.Precedent.Confidence)
                .ThenByDescending(r => r.Precedent.SuccessCount)
                .Take(maxResults)
                .ToList();

            progress?.Report($"Found {results.Count} matching precedents");
            Logger.Debug("Found {0} precedents matching query (from {1} candidates)",
                results.Count, candidates.Count);

            return results;
        }

        /// <summary>
        /// Gets the best solution for a given problem context.
        /// Returns the top-ranked precedent with an explanation of why it was chosen
        /// and how to adapt it.
        /// </summary>
        public async Task<BestSolutionResult> GetBestSolutionAsync(
            ProblemContext problemContext,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Debug("Finding best solution for: {0}", problemContext?.Description);
            progress?.Report("Searching for best matching design solution...");

            var results = await FindSimilarPrecedentsAsync(problemContext, 5, cancellationToken, progress);

            if (!results.Any())
            {
                Logger.Info("No matching precedents found for: {0}", problemContext?.Description);
                return new BestSolutionResult
                {
                    Found = false,
                    Explanation = "No matching design precedents found in the database. " +
                                  "Consider adding relevant case studies or building code data."
                };
            }

            var best = results.First();
            var alternative = results.Count > 1 ? results[1] : null;

            // Adapt the solution to current context
            var adaptedSolution = AdaptSolution(best.Precedent, problemContext);

            var result = new BestSolutionResult
            {
                Found = true,
                Precedent = best.Precedent,
                SimilarityScore = best.SimilarityScore,
                AdaptedSolution = adaptedSolution,
                Explanation = GenerateSolutionExplanation(best, problemContext),
                AlternativePrecedent = alternative?.Precedent,
                AlternativeSimilarity = alternative?.SimilarityScore ?? 0f,
                MatchDimensions = best.MatchDimensions,
                Confidence = best.SimilarityScore * best.Precedent.Confidence
            };

            // Update usage stats
            best.Precedent.UsageCount++;
            best.Precedent.LastUsed = DateTime.UtcNow;

            progress?.Report($"Found best solution with {best.SimilarityScore:P0} match");
            return result;
        }

        /// <summary>
        /// Gets all precedents matching specific criteria.
        /// </summary>
        public List<DesignPrecedent> GetPrecedentsByFilter(PrecedentFilter filter)
        {
            var query = _precedents.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(filter.BuildingType))
            {
                if (_buildingTypeIndex.TryGetValue(filter.BuildingType, out var ids))
                    query = query.Where(p => ids.Contains(p.PrecedentId));
                else
                    return new List<DesignPrecedent>();
            }

            if (!string.IsNullOrEmpty(filter.SystemType))
            {
                if (_systemTypeIndex.TryGetValue(filter.SystemType, out var ids))
                    query = query.Where(p => ids.Contains(p.PrecedentId));
                else
                    return new List<DesignPrecedent>();
            }

            if (!string.IsNullOrEmpty(filter.ProblemCategory))
            {
                if (_problemCategoryIndex.TryGetValue(filter.ProblemCategory, out var ids))
                    query = query.Where(p => ids.Contains(p.PrecedentId));
                else
                    return new List<DesignPrecedent>();
            }

            if (!string.IsNullOrEmpty(filter.ClimateZone))
            {
                if (_climateZoneIndex.TryGetValue(filter.ClimateZone, out var ids))
                    query = query.Where(p => ids.Contains(p.PrecedentId));
                else
                    return new List<DesignPrecedent>();
            }

            if (!string.IsNullOrEmpty(filter.Scale))
            {
                if (_scaleIndex.TryGetValue(filter.Scale, out var ids))
                    query = query.Where(p => ids.Contains(p.PrecedentId));
                else
                    return new List<DesignPrecedent>();
            }

            if (filter.MinConfidence > 0)
                query = query.Where(p => p.Confidence >= filter.MinConfidence);

            if (filter.SuccessfulOnly)
                query = query.Where(p => p.Outcome?.Success == true);

            if (filter.Source.HasValue)
                query = query.Where(p => p.Source == filter.Source.Value);

            return query
                .OrderByDescending(p => p.Confidence * (p.SuccessCount + 1))
                .Take(filter.MaxResults)
                .ToList();
        }

        #endregion

        #region Feedback and Learning

        /// <summary>
        /// Records the outcome of applying a precedent solution.
        /// Updates confidence, success/failure counts, and lessons learned.
        /// </summary>
        public void LearnFromOutcome(
            string precedentId,
            ApplicationOutcome outcome)
        {
            if (!_precedents.TryGetValue(precedentId, out var precedent))
            {
                Logger.Warn("Precedent '{0}' not found for outcome recording", precedentId);
                return;
            }

            lock (_lockObject)
            {
                if (outcome.Success)
                {
                    precedent.SuccessCount++;
                    Interlocked.Increment(ref _totalSuccessfulApplications);

                    // Boost confidence on success
                    precedent.Confidence = Math.Min(1.0f,
                        precedent.Confidence + _configuration.ConfidenceBoostOnSuccess);
                }
                else
                {
                    precedent.FailureCount++;
                    Interlocked.Increment(ref _totalFailedApplications);

                    // Decrease confidence on failure
                    precedent.Confidence = Math.Max(0.1f,
                        precedent.Confidence - _configuration.ConfidencePenaltyOnFailure);
                }

                // Record lessons
                if (!string.IsNullOrEmpty(outcome.LessonLearned))
                {
                    if (precedent.Lessons == null)
                        precedent.Lessons = new List<string>();
                    precedent.Lessons.Add(outcome.LessonLearned);

                    // Keep lessons bounded
                    while (precedent.Lessons.Count > 20)
                        precedent.Lessons.RemoveAt(0);
                }

                // Record adaptation notes
                if (!string.IsNullOrEmpty(outcome.AdaptationUsed))
                {
                    if (precedent.SuccessfulAdaptations == null)
                        precedent.SuccessfulAdaptations = new List<string>();

                    if (outcome.Success)
                        precedent.SuccessfulAdaptations.Add(outcome.AdaptationUsed);
                }

                // Update outcome metadata
                if (precedent.OutcomeHistory == null)
                    precedent.OutcomeHistory = new List<ApplicationOutcome>();
                precedent.OutcomeHistory.Add(outcome);
                while (precedent.OutcomeHistory.Count > 50)
                    precedent.OutcomeHistory.RemoveAt(0);

                precedent.LastUsed = DateTime.UtcNow;
            }

            Logger.Info("Recorded {0} outcome for precedent '{1}' (confidence now: {2:P0})",
                outcome.Success ? "successful" : "failed",
                precedentId, precedent.Confidence);
        }

        /// <summary>
        /// Extracts generalized design principles from multiple similar precedents
        /// within a problem category. Identifies common patterns, success factors,
        /// and pitfalls across related cases.
        /// </summary>
        public async Task<List<DesignPrinciple>> ExtractPrinciplesAsync(
            string problemCategory,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Extracting principles for category: {0}", problemCategory);
            progress?.Report($"Extracting design principles for {problemCategory}...");

            var principles = new List<DesignPrinciple>();

            // Get all precedents in this category
            HashSet<string> categoryIds = null;
            if (!string.IsNullOrEmpty(problemCategory))
            {
                _problemCategoryIndex.TryGetValue(problemCategory, out categoryIds);
            }

            var relevantPrecedents = categoryIds != null
                ? _precedents.Values.Where(p => categoryIds.Contains(p.PrecedentId)).ToList()
                : _precedents.Values.ToList();

            if (relevantPrecedents.Count < _configuration.MinPrecedentsForPrinciple)
            {
                Logger.Info("Not enough precedents ({0}) to extract principles (min: {1})",
                    relevantPrecedents.Count, _configuration.MinPrecedentsForPrinciple);
                return principles;
            }

            await Task.Run(() =>
            {
                // Extract success-factor principles
                cancellationToken.ThrowIfCancellationRequested();
                var successPrinciples = ExtractSuccessFactors(relevantPrecedents, problemCategory);
                principles.AddRange(successPrinciples);

                // Extract common-solution principles
                cancellationToken.ThrowIfCancellationRequested();
                var solutionPrinciples = ExtractCommonSolutions(relevantPrecedents, problemCategory);
                principles.AddRange(solutionPrinciples);

                // Extract failure-avoidance principles
                cancellationToken.ThrowIfCancellationRequested();
                var failurePrinciples = ExtractFailurePatterns(relevantPrecedents, problemCategory);
                principles.AddRange(failurePrinciples);

                // Extract context-dependent principles
                cancellationToken.ThrowIfCancellationRequested();
                var contextPrinciples = ExtractContextDependencies(relevantPrecedents, problemCategory);
                principles.AddRange(contextPrinciples);

            }, cancellationToken);

            // Store extracted principles
            foreach (var principle in principles)
            {
                _extractedPrinciples[principle.PrincipleId] = principle;
            }

            progress?.Report($"Extracted {principles.Count} design principles from " +
                             $"{relevantPrecedents.Count} precedents");
            Logger.Info("Extracted {0} principles from {1} precedents in category '{2}'",
                principles.Count, relevantPrecedents.Count, problemCategory);

            return principles;
        }

        /// <summary>
        /// Gets previously extracted principles.
        /// </summary>
        public List<DesignPrinciple> GetPrinciples(string problemCategory = null)
        {
            var query = _extractedPrinciples.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(problemCategory))
            {
                query = query.Where(p =>
                    string.Equals(p.Category, problemCategory, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(p => p.Confidence)
                .ThenByDescending(p => p.SupportingPrecedentCount)
                .ToList();
        }

        /// <summary>
        /// Gets database statistics.
        /// </summary>
        public PrecedentDBStatistics GetStatistics()
        {
            return new PrecedentDBStatistics
            {
                TotalPrecedents = _precedents.Count,
                PrecedentsBySource = _precedents.Values
                    .GroupBy(p => p.Source)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                PrecedentsByBuildingType = _buildingTypeIndex
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                PrecedentsBySystemType = _systemTypeIndex
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                PrecedentsByCategory = _problemCategoryIndex
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                ExtractedPrinciples = _extractedPrinciples.Count,
                TotalSuccessfulApplications = _totalSuccessfulApplications,
                TotalFailedApplications = _totalFailedApplications,
                AverageConfidence = _precedents.Values.Any()
                    ? _precedents.Values.Average(p => p.Confidence) : 0f,
                MostUsedPrecedentId = _precedents.Values
                    .OrderByDescending(p => p.UsageCount)
                    .FirstOrDefault()?.PrecedentId
            };
        }

        #endregion

        #region Similarity Calculation

        private float CalculateSimilarity(ProblemContext query, DesignPrecedent precedent)
        {
            float totalScore = 0f;
            float totalWeight = 0f;

            // Building type similarity
            if (!string.IsNullOrEmpty(query.BuildingType))
            {
                float btScore = CalculateBuildingTypeSimilarity(
                    query.BuildingType, precedent.Context.BuildingType);
                totalScore += btScore * _configuration.BuildingTypeWeight;
                totalWeight += _configuration.BuildingTypeWeight;
            }

            // System type similarity
            if (!string.IsNullOrEmpty(query.SystemType))
            {
                float stScore = CalculateSystemTypeSimilarity(
                    query.SystemType, precedent.Context.SystemType);
                totalScore += stScore * _configuration.SystemTypeWeight;
                totalWeight += _configuration.SystemTypeWeight;
            }

            // Problem category similarity
            if (!string.IsNullOrEmpty(query.ProblemCategory))
            {
                float pcScore = string.Equals(query.ProblemCategory,
                    precedent.Problem?.Category, StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.2f;
                totalScore += pcScore * _configuration.ProblemCategoryWeight;
                totalWeight += _configuration.ProblemCategoryWeight;
            }

            // Climate zone similarity
            if (!string.IsNullOrEmpty(query.ClimateZone))
            {
                float czScore = CalculateClimateZoneSimilarity(
                    query.ClimateZone, precedent.Context.ClimateZone);
                totalScore += czScore * _configuration.ClimateZoneWeight;
                totalWeight += _configuration.ClimateZoneWeight;
            }

            // Scale similarity
            if (!string.IsNullOrEmpty(query.Scale))
            {
                float scScore = CalculateScaleSimilarity(
                    query.Scale, precedent.Context.Scale);
                totalScore += scScore * _configuration.ScaleWeight;
                totalWeight += _configuration.ScaleWeight;
            }

            // Text similarity on description
            if (!string.IsNullOrEmpty(query.Description) &&
                !string.IsNullOrEmpty(precedent.Problem?.Description))
            {
                float textScore = CalculateTextSimilarity(
                    query.Description, precedent.Problem.Description);
                totalScore += textScore * _configuration.DescriptionWeight;
                totalWeight += _configuration.DescriptionWeight;
            }

            // Building code similarity
            if (!string.IsNullOrEmpty(query.BuildingCode))
            {
                float codeScore = string.Equals(query.BuildingCode,
                    precedent.Context.BuildingCode, StringComparison.OrdinalIgnoreCase) ? 1.0f :
                    AreSameCodeFamily(query.BuildingCode, precedent.Context.BuildingCode) ? 0.5f : 0.1f;
                totalScore += codeScore * _configuration.BuildingCodeWeight;
                totalWeight += _configuration.BuildingCodeWeight;
            }

            return totalWeight > 0 ? totalScore / totalWeight : 0f;
        }

        private float CalculateBuildingTypeSimilarity(string type1, string type2)
        {
            if (string.IsNullOrEmpty(type1) || string.IsNullOrEmpty(type2)) return 0.3f;
            if (string.Equals(type1, type2, StringComparison.OrdinalIgnoreCase)) return 1.0f;

            // Group similar building types
            var residentialTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Residential", "SingleFamily", "MultiFamily", "Apartment", "Condominium", "Housing" };
            var commercialTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Commercial", "Office", "Retail", "MixedUse", "Business" };
            var healthcareTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Healthcare", "Hospital", "Clinic", "Medical", "Laboratory" };
            var educationalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Educational", "School", "University", "College", "Library" };
            var industrialTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Industrial", "Warehouse", "Factory", "Manufacturing", "DataCenter" };
            var hospitalityTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Hospitality", "Hotel", "Resort", "Restaurant" };

            var groups = new[] { residentialTypes, commercialTypes, healthcareTypes,
                                 educationalTypes, industrialTypes, hospitalityTypes };

            foreach (var group in groups)
            {
                if (group.Contains(type1) && group.Contains(type2))
                    return 0.7f;
            }

            return 0.2f;
        }

        private float CalculateSystemTypeSimilarity(string type1, string type2)
        {
            if (string.IsNullOrEmpty(type1) || string.IsNullOrEmpty(type2)) return 0.3f;
            if (string.Equals(type1, type2, StringComparison.OrdinalIgnoreCase)) return 1.0f;

            var mepTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "HVAC", "Plumbing", "Electrical", "MEP", "FireProtection" };
            var structuralTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Structural", "Foundation", "Steel", "Concrete", "Timber" };
            var envelopeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Envelope", "Facade", "Roofing", "Waterproofing", "Insulation" };

            var groups = new[] { mepTypes, structuralTypes, envelopeTypes };
            foreach (var group in groups)
            {
                if (group.Contains(type1) && group.Contains(type2))
                    return 0.6f;
            }

            return 0.2f;
        }

        private float CalculateClimateZoneSimilarity(string zone1, string zone2)
        {
            if (string.IsNullOrEmpty(zone1) || string.IsNullOrEmpty(zone2)) return 0.3f;
            if (string.Equals(zone1, zone2, StringComparison.OrdinalIgnoreCase)) return 1.0f;

            // ASHRAE climate zones: 1-8, A/B/C
            if (int.TryParse(zone1.Substring(0, 1), out var z1) &&
                int.TryParse(zone2.Substring(0, 1), out var z2))
            {
                var diff = Math.Abs(z1 - z2);
                return Math.Max(0.1f, 1.0f - diff * 0.15f);
            }

            // Named climate zones
            var hotZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Tropical", "1", "1A", "2", "2A", "2B", "Arid" };
            var warmZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "3", "3A", "3B", "3C", "4", "4A", "Temperate" };
            var coldZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "5", "5A", "5B", "6", "6A", "6B", "Cold" };
            var vColdZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "7", "8", "Subarctic", "Arctic" };

            var groups = new[] { hotZones, warmZones, coldZones, vColdZones };
            foreach (var group in groups)
            {
                if (group.Contains(zone1) && group.Contains(zone2))
                    return 0.7f;
            }

            return 0.3f;
        }

        private float CalculateScaleSimilarity(string scale1, string scale2)
        {
            if (string.IsNullOrEmpty(scale1) || string.IsNullOrEmpty(scale2)) return 0.3f;
            if (string.Equals(scale1, scale2, StringComparison.OrdinalIgnoreCase)) return 1.0f;

            var scaleOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["SingleRoom"] = 1, ["Room"] = 1,
                ["Floor"] = 2, ["Level"] = 2,
                ["Building"] = 3,
                ["Campus"] = 4, ["District"] = 4
            };

            if (scaleOrder.TryGetValue(scale1, out var s1) &&
                scaleOrder.TryGetValue(scale2, out var s2))
            {
                var diff = Math.Abs(s1 - s2);
                return diff == 0 ? 1.0f : diff == 1 ? 0.6f : 0.3f;
            }

            return 0.3f;
        }

        private float CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0f;

            // Simple word overlap similarity (Jaccard)
            var words1 = text1.ToLowerInvariant().Split(
                new[] { ' ', ',', '.', ';', ':', '-', '/', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToHashSet();

            var words2 = text2.ToLowerInvariant().Split(
                new[] { ' ', ',', '.', ';', ':', '-', '/', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToHashSet();

            if (!words1.Any() || !words2.Any()) return 0f;

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return union > 0 ? (float)intersection / union : 0f;
        }

        private bool AreSameCodeFamily(string code1, string code2)
        {
            if (string.IsNullOrEmpty(code1) || string.IsNullOrEmpty(code2)) return false;

            var usaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "IBC", "ASHRAE", "ACI", "ASCE", "NEC", "NFPA", "IPC", "IRC" };
            var ukCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "BS", "CIBSE", "BS6399", "BS7671", "BS5950" };
            var euCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Eurocode", "EN", "EC2", "EC3", "EC7", "EC8" };
            var africaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "UNBS", "KEBS", "SANS", "TBS", "EAS", "ECOWAS" };

            var families = new[] { usaCodes, ukCodes, euCodes, africaCodes };
            foreach (var family in families)
            {
                if (family.Any(c => code1.Contains(c)) && family.Any(c => code2.Contains(c)))
                    return true;
            }

            return false;
        }

        #endregion

        #region Solution Adaptation

        private AdaptedSolution AdaptSolution(DesignPrecedent precedent, ProblemContext currentContext)
        {
            var adapted = new AdaptedSolution
            {
                OriginalPrecedentId = precedent.PrecedentId,
                OriginalSolution = precedent.Solution?.Description,
                AdaptedDescription = precedent.Solution?.Description ?? "",
                Adaptations = new List<SolutionAdaptation>(),
                Confidence = precedent.Confidence
            };

            // Adapt for different building type
            if (!string.IsNullOrEmpty(currentContext.BuildingType) &&
                !string.Equals(currentContext.BuildingType, precedent.Context?.BuildingType,
                    StringComparison.OrdinalIgnoreCase))
            {
                adapted.Adaptations.Add(new SolutionAdaptation
                {
                    Dimension = "BuildingType",
                    OriginalValue = precedent.Context?.BuildingType,
                    TargetValue = currentContext.BuildingType,
                    AdaptationNote = $"Adapt from {precedent.Context?.BuildingType} to " +
                                     $"{currentContext.BuildingType}. Verify occupancy " +
                                     $"and use-case specific requirements.",
                    ConfidenceImpact = -0.1f
                });
                adapted.Confidence -= 0.1f;
            }

            // Adapt for different climate zone
            if (!string.IsNullOrEmpty(currentContext.ClimateZone) &&
                !string.Equals(currentContext.ClimateZone, precedent.Context?.ClimateZone,
                    StringComparison.OrdinalIgnoreCase))
            {
                adapted.Adaptations.Add(new SolutionAdaptation
                {
                    Dimension = "ClimateZone",
                    OriginalValue = precedent.Context?.ClimateZone,
                    TargetValue = currentContext.ClimateZone,
                    AdaptationNote = $"Adapt from climate zone {precedent.Context?.ClimateZone} " +
                                     $"to {currentContext.ClimateZone}. Review thermal, " +
                                     $"moisture, and energy implications.",
                    ConfidenceImpact = -0.15f
                });
                adapted.Confidence -= 0.15f;
            }

            // Adapt for different scale
            if (!string.IsNullOrEmpty(currentContext.Scale) &&
                !string.Equals(currentContext.Scale, precedent.Context?.Scale,
                    StringComparison.OrdinalIgnoreCase))
            {
                adapted.Adaptations.Add(new SolutionAdaptation
                {
                    Dimension = "Scale",
                    OriginalValue = precedent.Context?.Scale,
                    TargetValue = currentContext.Scale,
                    AdaptationNote = $"Scale from {precedent.Context?.Scale} to " +
                                     $"{currentContext.Scale}. Verify capacity, " +
                                     $"sizing, and cost implications.",
                    ConfidenceImpact = -0.1f
                });
                adapted.Confidence -= 0.1f;
            }

            // Adapt for different building code
            if (!string.IsNullOrEmpty(currentContext.BuildingCode) &&
                !string.Equals(currentContext.BuildingCode, precedent.Context?.BuildingCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                adapted.Adaptations.Add(new SolutionAdaptation
                {
                    Dimension = "BuildingCode",
                    OriginalValue = precedent.Context?.BuildingCode,
                    TargetValue = currentContext.BuildingCode,
                    AdaptationNote = $"Verify compliance with {currentContext.BuildingCode} " +
                                     $"(original solution was for {precedent.Context?.BuildingCode}). " +
                                     $"Check all code-specific requirements.",
                    ConfidenceImpact = -0.2f
                });
                adapted.Confidence -= 0.2f;
            }

            // Ensure confidence doesn't go below minimum
            adapted.Confidence = Math.Max(0.1f, adapted.Confidence);

            // Build the adapted description
            if (adapted.Adaptations.Any())
            {
                adapted.AdaptedDescription += "\n\nAdaptations needed:\n" +
                    string.Join("\n", adapted.Adaptations.Select(a =>
                        $"- {a.Dimension}: {a.AdaptationNote}"));
            }

            return adapted;
        }

        #endregion

        #region Principle Extraction

        private List<DesignPrinciple> ExtractSuccessFactors(
            List<DesignPrecedent> precedents, string category)
        {
            var principles = new List<DesignPrinciple>();

            var successful = precedents.Where(p => p.Outcome?.Success == true).ToList();
            if (successful.Count < _configuration.MinPrecedentsForPrinciple) return principles;

            // Find common elements in successful solutions
            var approachCounts = successful
                .Where(p => p.Solution != null)
                .GroupBy(p => p.Solution.Approach)
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var group in approachCounts.Where(g => g.Count() >= 2))
            {
                var pctSuccessful = (float)group.Count() / successful.Count;
                if (pctSuccessful >= 0.3f)
                {
                    principles.Add(new DesignPrinciple
                    {
                        PrincipleId = $"SF_{category}_{group.Key}",
                        Category = category,
                        Title = $"Prefer {group.Key} approach for {category} problems",
                        Description = $"In {category} problems, {group.Key} solutions succeed " +
                                      $"{pctSuccessful:P0} of the time ({group.Count()}/{successful.Count} cases)",
                        PrincipleType = PrincipleType.SuccessFactor,
                        Confidence = pctSuccessful,
                        SupportingPrecedentCount = group.Count(),
                        SupportingPrecedentIds = group.Select(p => p.PrecedentId).ToList(),
                        ExtractedAt = DateTime.UtcNow
                    });
                }
            }

            // Find common lessons from successful outcomes
            var allLessons = successful
                .Where(p => p.Lessons != null)
                .SelectMany(p => p.Lessons)
                .GroupBy(l => l.ToLowerInvariant())
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count())
                .Take(3);

            foreach (var lesson in allLessons)
            {
                principles.Add(new DesignPrinciple
                {
                    PrincipleId = $"SL_{category}_{lesson.Key.GetHashCode():X8}",
                    Category = category,
                    Title = TruncateForLog(lesson.First(), 80),
                    Description = $"Recurring lesson from {lesson.Count()} successful cases: {lesson.First()}",
                    PrincipleType = PrincipleType.SuccessFactor,
                    Confidence = Math.Min(1.0f, lesson.Count() / 5.0f),
                    SupportingPrecedentCount = lesson.Count(),
                    ExtractedAt = DateTime.UtcNow
                });
            }

            return principles;
        }

        private List<DesignPrinciple> ExtractCommonSolutions(
            List<DesignPrecedent> precedents, string category)
        {
            var principles = new List<DesignPrinciple>();

            // Group by system type and find common solution approaches
            var bySystem = precedents
                .Where(p => p.Context?.SystemType != null && p.Solution != null)
                .GroupBy(p => p.Context.SystemType)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var systemGroup in bySystem)
            {
                var approaches = systemGroup
                    .GroupBy(p => p.Solution.Approach)
                    .OrderByDescending(g => g.Count())
                    .First();

                if (approaches.Count() >= 2)
                {
                    principles.Add(new DesignPrinciple
                    {
                        PrincipleId = $"CS_{category}_{systemGroup.Key}",
                        Category = category,
                        Title = $"Standard approach for {systemGroup.Key} in {category}",
                        Description = $"For {systemGroup.Key} systems in {category} problems, " +
                                      $"the {approaches.Key} approach is most common " +
                                      $"({approaches.Count()}/{systemGroup.Count()} cases)",
                        PrincipleType = PrincipleType.CommonSolution,
                        Confidence = (float)approaches.Count() / systemGroup.Count(),
                        SupportingPrecedentCount = approaches.Count(),
                        SupportingPrecedentIds = approaches.Select(p => p.PrecedentId).ToList(),
                        ExtractedAt = DateTime.UtcNow
                    });
                }
            }

            return principles;
        }

        private List<DesignPrinciple> ExtractFailurePatterns(
            List<DesignPrecedent> precedents, string category)
        {
            var principles = new List<DesignPrinciple>();

            var failed = precedents.Where(p => p.Outcome?.Success == false).ToList();
            if (failed.Count < 2) return principles;

            // Find common failure factors
            var failureReasons = failed
                .Where(p => p.Outcome?.FailureReason != null)
                .GroupBy(p => p.Outcome.FailureReason.ToLowerInvariant())
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count())
                .Take(3);

            foreach (var reason in failureReasons)
            {
                principles.Add(new DesignPrinciple
                {
                    PrincipleId = $"FP_{category}_{reason.Key.GetHashCode():X8}",
                    Category = category,
                    Title = $"Avoid: {TruncateForLog(reason.Key, 60)}",
                    Description = $"Common failure in {category}: {reason.Key} " +
                                  $"(occurred in {reason.Count()} cases)",
                    PrincipleType = PrincipleType.FailureAvoidance,
                    Confidence = Math.Min(1.0f, reason.Count() / 3.0f),
                    SupportingPrecedentCount = reason.Count(),
                    SupportingPrecedentIds = reason.Select(p => p.PrecedentId).ToList(),
                    ExtractedAt = DateTime.UtcNow
                });
            }

            return principles;
        }

        private List<DesignPrinciple> ExtractContextDependencies(
            List<DesignPrecedent> precedents, string category)
        {
            var principles = new List<DesignPrinciple>();

            // Check if success varies significantly by climate zone
            var byClimate = precedents
                .Where(p => p.Context?.ClimateZone != null && p.Outcome != null)
                .GroupBy(p => p.Context.ClimateZone)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var climateGroup in byClimate)
            {
                var successRate = climateGroup.Count(p => p.Outcome.Success) /
                    (float)climateGroup.Count();

                if (successRate > 0.8f || successRate < 0.3f)
                {
                    principles.Add(new DesignPrinciple
                    {
                        PrincipleId = $"CD_{category}_{climateGroup.Key}",
                        Category = category,
                        Title = successRate > 0.8f
                            ? $"{category} solutions work well in climate zone {climateGroup.Key}"
                            : $"{category} solutions struggle in climate zone {climateGroup.Key}",
                        Description = $"In climate zone {climateGroup.Key}, {category} solutions " +
                                      $"have a {successRate:P0} success rate " +
                                      $"({climateGroup.Count()} cases)",
                        PrincipleType = PrincipleType.ContextDependency,
                        Confidence = Math.Min(1.0f, climateGroup.Count() / 5.0f),
                        SupportingPrecedentCount = climateGroup.Count(),
                        ExtractedAt = DateTime.UtcNow
                    });
                }
            }

            return principles;
        }

        #endregion

        #region Indexing

        private void IndexPrecedent(DesignPrecedent precedent)
        {
            var id = precedent.PrecedentId;

            // Index by building type
            if (!string.IsNullOrEmpty(precedent.Context?.BuildingType))
            {
                AddToIndex(_buildingTypeIndex, precedent.Context.BuildingType, id);
            }

            // Index by system type
            if (!string.IsNullOrEmpty(precedent.Context?.SystemType))
            {
                AddToIndex(_systemTypeIndex, precedent.Context.SystemType, id);
            }

            // Index by problem category
            if (!string.IsNullOrEmpty(precedent.Problem?.Category))
            {
                AddToIndex(_problemCategoryIndex, precedent.Problem.Category, id);
            }

            // Index by climate zone
            if (!string.IsNullOrEmpty(precedent.Context?.ClimateZone))
            {
                AddToIndex(_climateZoneIndex, precedent.Context.ClimateZone, id);
            }

            // Index by scale
            if (!string.IsNullOrEmpty(precedent.Context?.Scale))
            {
                AddToIndex(_scaleIndex, precedent.Context.Scale, id);
            }

            // Index by building code
            if (!string.IsNullOrEmpty(precedent.Context?.BuildingCode))
            {
                AddToIndex(_buildingCodeIndex, precedent.Context.BuildingCode, id);
            }

            // Index by tags
            if (precedent.Tags != null)
            {
                foreach (var tag in precedent.Tags)
                {
                    AddToIndex(_tagIndex, tag, id);
                }
            }
        }

        private void AddToIndex(ConcurrentDictionary<string, HashSet<string>> index,
            string key, string precedentId)
        {
            index.AddOrUpdate(key,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { precedentId },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        existing.Add(precedentId);
                    }
                    return existing;
                });
        }

        private List<DesignPrecedent> GetCandidatePrecedents(ProblemContext query)
        {
            var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Gather candidates from all relevant indexes
            if (!string.IsNullOrEmpty(query.BuildingType) &&
                _buildingTypeIndex.TryGetValue(query.BuildingType, out var btIds))
                candidateIds.UnionWith(btIds);

            if (!string.IsNullOrEmpty(query.SystemType) &&
                _systemTypeIndex.TryGetValue(query.SystemType, out var stIds))
                candidateIds.UnionWith(stIds);

            if (!string.IsNullOrEmpty(query.ProblemCategory) &&
                _problemCategoryIndex.TryGetValue(query.ProblemCategory, out var pcIds))
                candidateIds.UnionWith(pcIds);

            if (!string.IsNullOrEmpty(query.ClimateZone) &&
                _climateZoneIndex.TryGetValue(query.ClimateZone, out var czIds))
                candidateIds.UnionWith(czIds);

            if (!string.IsNullOrEmpty(query.Scale) &&
                _scaleIndex.TryGetValue(query.Scale, out var scIds))
                candidateIds.UnionWith(scIds);

            return candidateIds
                .Select(id => _precedents.TryGetValue(id, out var p) ? p : null)
                .Where(p => p != null)
                .ToList();
        }

        #endregion

        #region CSV Loading Helpers

        private async Task<List<DesignPrecedent>> LoadProjectsCsvAsync(
            string filePath, CancellationToken cancellationToken)
        {
            var precedents = new List<DesignPrecedent>();

            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return precedents;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++)
                    headerIndex[headers[i].Trim()] = i;

                for (int row = 1; row < lines.Length; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[row]);
                        var precedent = MapProjectRowToPrecedent(fields, headerIndex, row);
                        if (precedent != null)
                            precedents.Add(precedent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping CSV row {0}: {1}", row, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load projects CSV: {0}", filePath);
            }

            return precedents;
        }

        private async Task<List<DesignPrecedent>> LoadLessonsCsvAsync(
            string filePath, CancellationToken cancellationToken)
        {
            var precedents = new List<DesignPrecedent>();

            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return precedents;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++)
                    headerIndex[headers[i].Trim()] = i;

                for (int row = 1; row < lines.Length; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[row]);
                        var precedent = MapLessonRowToPrecedent(fields, headerIndex, row);
                        if (precedent != null)
                            precedents.Add(precedent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping lessons CSV row {0}: {1}", row, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load lessons CSV: {0}", filePath);
            }

            return precedents;
        }

        private async Task<List<DesignPrecedent>> LoadBuildingCodesCsvAsync(
            string filePath, CancellationToken cancellationToken)
        {
            var precedents = new List<DesignPrecedent>();

            try
            {
                var lines = await Task.Run(() => File.ReadAllLines(filePath), cancellationToken);
                if (lines.Length < 2) return precedents;

                var headers = ParseCsvLine(lines[0]);
                var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++)
                    headerIndex[headers[i].Trim()] = i;

                for (int row = 1; row < lines.Length; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fields = ParseCsvLine(lines[row]);
                        var precedent = MapCodeRowToPrecedent(fields, headerIndex, row);
                        if (precedent != null)
                            precedents.Add(precedent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Trace("Skipping codes CSV row {0}: {1}", row, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load building codes CSV: {0}", filePath);
            }

            return precedents;
        }

        private DesignPrecedent MapProjectRowToPrecedent(
            string[] fields, Dictionary<string, int> headers, int row)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var description = GetField("Description") ?? GetField("ProjectName") ?? "";
            if (string.IsNullOrWhiteSpace(description)) return null;

            return new DesignPrecedent
            {
                PrecedentId = $"CSV_PRJ_{row}",
                Source = PrecedentSource.CsvCaseStudy,
                Problem = new PrecedentProblem
                {
                    Description = GetField("Challenge") ?? GetField("Problem") ?? description,
                    Category = GetField("Category") ?? "General",
                    Severity = ProblemSeverity.Medium
                },
                Context = new PrecedentContext
                {
                    BuildingType = GetField("BuildingType") ?? GetField("ProjectType"),
                    SystemType = GetField("SystemType") ?? GetField("Discipline"),
                    ClimateZone = GetField("ClimateZone") ?? GetField("Climate"),
                    Scale = GetField("Scale") ?? GetField("Size"),
                    BuildingCode = GetField("BuildingCode") ?? GetField("Code"),
                    Region = GetField("Region") ?? GetField("Country"),
                    ProblemCategory = GetField("Category")
                },
                Solution = new PrecedentSolution
                {
                    Description = GetField("Solution") ?? GetField("Approach") ?? "",
                    Approach = ParseApproach(GetField("Approach")),
                    ImplementationSteps = new List<string>()
                },
                Outcome = new PrecedentOutcome
                {
                    Success = ParseBool(GetField("Success") ?? GetField("Successful") ?? "true"),
                    FailureReason = GetField("FailureReason"),
                    CostImpact = GetField("CostImpact"),
                    ScheduleImpact = GetField("ScheduleImpact")
                },
                Lessons = ParseLessons(GetField("Lessons") ?? GetField("LessonsLearned")),
                Tags = ParseTags(GetField("Tags")),
                Confidence = 0.7f
            };
        }

        private DesignPrecedent MapLessonRowToPrecedent(
            string[] fields, Dictionary<string, int> headers, int row)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var lesson = GetField("Lesson") ?? GetField("Description") ?? "";
            if (string.IsNullOrWhiteSpace(lesson)) return null;

            return new DesignPrecedent
            {
                PrecedentId = $"CSV_LSN_{row}",
                Source = PrecedentSource.CsvCaseStudy,
                Problem = new PrecedentProblem
                {
                    Description = GetField("Problem") ?? GetField("Issue") ?? lesson,
                    Category = GetField("Category") ?? "LessonsLearned",
                    Severity = ProblemSeverity.Medium
                },
                Context = new PrecedentContext
                {
                    BuildingType = GetField("BuildingType"),
                    SystemType = GetField("System") ?? GetField("Discipline"),
                    ProblemCategory = GetField("Category")
                },
                Solution = new PrecedentSolution
                {
                    Description = GetField("Resolution") ?? GetField("Solution") ?? lesson,
                    Approach = SolutionApproach.BestPractice
                },
                Outcome = new PrecedentOutcome
                {
                    Success = ParseBool(GetField("Resolved") ?? "true")
                },
                Lessons = new List<string> { lesson },
                Confidence = 0.65f
            };
        }

        private DesignPrecedent MapCodeRowToPrecedent(
            string[] fields, Dictionary<string, int> headers, int row)
        {
            string GetField(string name) =>
                headers.TryGetValue(name, out var idx) && idx < fields.Length
                    ? fields[idx]?.Trim() : null;

            var requirement = GetField("Requirement") ?? GetField("Description") ?? "";
            if (string.IsNullOrWhiteSpace(requirement)) return null;

            return new DesignPrecedent
            {
                PrecedentId = $"CSV_CODE_{row}",
                Source = PrecedentSource.BuildingCode,
                Problem = new PrecedentProblem
                {
                    Description = $"Code compliance: {requirement}",
                    Category = "CodeCompliance",
                    Severity = ProblemSeverity.High
                },
                Context = new PrecedentContext
                {
                    BuildingCode = GetField("Code") ?? GetField("Standard"),
                    SystemType = GetField("System") ?? GetField("Category"),
                    ProblemCategory = "CodeCompliance"
                },
                Solution = new PrecedentSolution
                {
                    Description = GetField("Compliance") ?? GetField("Solution") ?? requirement,
                    Approach = SolutionApproach.CodeBased
                },
                Outcome = new PrecedentOutcome { Success = true },
                Lessons = new List<string>(),
                Confidence = 0.9f
            };
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        #endregion

        #region Helper Methods

        private string GeneratePrecedentId()
        {
            return $"PRE_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        private Dictionary<string, float> IdentifyMatchDimensions(
            ProblemContext query, DesignPrecedent precedent)
        {
            var dimensions = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(query.BuildingType))
                dimensions["BuildingType"] = CalculateBuildingTypeSimilarity(
                    query.BuildingType, precedent.Context?.BuildingType);

            if (!string.IsNullOrEmpty(query.SystemType))
                dimensions["SystemType"] = CalculateSystemTypeSimilarity(
                    query.SystemType, precedent.Context?.SystemType);

            if (!string.IsNullOrEmpty(query.ProblemCategory))
                dimensions["ProblemCategory"] = string.Equals(query.ProblemCategory,
                    precedent.Problem?.Category, StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.2f;

            if (!string.IsNullOrEmpty(query.ClimateZone))
                dimensions["ClimateZone"] = CalculateClimateZoneSimilarity(
                    query.ClimateZone, precedent.Context?.ClimateZone);

            if (!string.IsNullOrEmpty(query.Scale))
                dimensions["Scale"] = CalculateScaleSimilarity(
                    query.Scale, precedent.Context?.Scale);

            return dimensions;
        }

        private string GenerateAdaptationNotes(ProblemContext query, DesignPrecedent precedent)
        {
            var notes = new List<string>();

            if (!string.IsNullOrEmpty(query.BuildingType) &&
                !string.Equals(query.BuildingType, precedent.Context?.BuildingType,
                    StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Adapt from {precedent.Context?.BuildingType ?? "unknown"} " +
                          $"to {query.BuildingType}");
            }

            if (!string.IsNullOrEmpty(query.ClimateZone) &&
                !string.Equals(query.ClimateZone, precedent.Context?.ClimateZone,
                    StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Climate adaptation needed: {precedent.Context?.ClimateZone ?? "unknown"} " +
                          $"-> {query.ClimateZone}");
            }

            return notes.Any() ? string.Join("; ", notes) : "Direct application possible";
        }

        private string GenerateSolutionExplanation(PrecedentSearchResult result, ProblemContext query)
        {
            var p = result.Precedent;
            var parts = new List<string>();

            parts.Add($"Best match ({result.SimilarityScore:P0} similarity) from " +
                      $"{p.Source} source.");

            if (p.SuccessCount > 0)
                parts.Add($"Successfully applied {p.SuccessCount} time(s).");

            if (result.MatchDimensions.Any())
            {
                var strongMatches = result.MatchDimensions
                    .Where(kv => kv.Value > 0.7f)
                    .Select(kv => kv.Key);
                if (strongMatches.Any())
                    parts.Add($"Strong match on: {string.Join(", ", strongMatches)}.");
            }

            if (p.Lessons?.Any() == true)
                parts.Add($"Key lesson: {TruncateForLog(p.Lessons.First(), 100)}");

            return string.Join(" ", parts);
        }

        private SolutionApproach ParseApproach(string text)
        {
            if (string.IsNullOrEmpty(text)) return SolutionApproach.Empirical;
            var lower = text.ToLowerInvariant();
            if (lower.Contains("code") || lower.Contains("standard")) return SolutionApproach.CodeBased;
            if (lower.Contains("innovat") || lower.Contains("novel")) return SolutionApproach.Innovative;
            if (lower.Contains("best practice") || lower.Contains("proven")) return SolutionApproach.BestPractice;
            if (lower.Contains("hybrid") || lower.Contains("combined")) return SolutionApproach.Hybrid;
            if (lower.Contains("value") || lower.Contains("engineer")) return SolutionApproach.ValueEngineered;
            return SolutionApproach.Empirical;
        }

        private bool ParseBool(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lower = text.ToLowerInvariant().Trim();
            return lower == "true" || lower == "yes" || lower == "1" || lower == "success";
        }

        private List<string> ParseLessons(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return text.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private List<string> ParseTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return text.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        #endregion
    }

    #endregion

    #region Configuration

    public class DesignPrecedentConfiguration
    {
        public float MinSimilarityThreshold { get; set; } = 0.2f;
        public int MinPrecedentsForPrinciple { get; set; } = 3;
        public float ConfidenceBoostOnSuccess { get; set; } = 0.05f;
        public float ConfidencePenaltyOnFailure { get; set; } = 0.08f;
        public float BuildingTypeWeight { get; set; } = 0.2f;
        public float SystemTypeWeight { get; set; } = 0.2f;
        public float ProblemCategoryWeight { get; set; } = 0.25f;
        public float ClimateZoneWeight { get; set; } = 0.1f;
        public float ScaleWeight { get; set; } = 0.1f;
        public float DescriptionWeight { get; set; } = 0.1f;
        public float BuildingCodeWeight { get; set; } = 0.05f;
    }

    #endregion

    #region Core Types

    /// <summary>
    /// A complete design precedent: Problem -> Context -> Solution -> Outcome -> Lessons.
    /// </summary>
    public class DesignPrecedent
    {
        public string PrecedentId { get; set; }
        public PrecedentSource Source { get; set; }
        public PrecedentProblem Problem { get; set; }
        public PrecedentContext Context { get; set; }
        public PrecedentSolution Solution { get; set; }
        public PrecedentOutcome Outcome { get; set; }
        public List<string> Lessons { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public float Confidence { get; set; }
        public int UsageCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> SuccessfulAdaptations { get; set; } = new List<string>();
        public List<ApplicationOutcome> OutcomeHistory { get; set; } = new List<ApplicationOutcome>();
        public DateTime AddedAt { get; set; }
        public DateTime LastUsed { get; set; }
    }

    public enum PrecedentSource
    {
        CsvCaseStudy,
        LearnedFromUser,
        BuildingCode,
        EpisodicMemory,
        ManualEntry
    }

    public class PrecedentProblem
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public ProblemSeverity Severity { get; set; }
        public List<string> Constraints { get; set; } = new List<string>();
    }

    public enum ProblemSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class PrecedentContext
    {
        public string BuildingType { get; set; }
        public string SystemType { get; set; }
        public string ProblemCategory { get; set; }
        public string ClimateZone { get; set; }
        public string Scale { get; set; }
        public string BuildingCode { get; set; }
        public string Region { get; set; }
        public Dictionary<string, object> AdditionalContext { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public class PrecedentSolution
    {
        public string Description { get; set; }
        public SolutionApproach Approach { get; set; }
        public List<string> ImplementationSteps { get; set; } = new List<string>();
        public Dictionary<string, object> Parameters { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string EstimatedCost { get; set; }
        public string EstimatedDuration { get; set; }
    }

    public enum SolutionApproach
    {
        CodeBased,
        BestPractice,
        Innovative,
        ValueEngineered,
        Hybrid,
        Empirical,
        Performance
    }

    public class PrecedentOutcome
    {
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public string CostImpact { get; set; }
        public string ScheduleImpact { get; set; }
        public string QualityImpact { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Search and Result Types

    /// <summary>
    /// Context describing the current problem for similarity search.
    /// </summary>
    public class ProblemContext
    {
        public string Description { get; set; }
        public string BuildingType { get; set; }
        public string SystemType { get; set; }
        public string ProblemCategory { get; set; }
        public string ClimateZone { get; set; }
        public string Scale { get; set; }
        public string BuildingCode { get; set; }
        public Dictionary<string, object> AdditionalContext { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public class PrecedentSearchResult
    {
        public DesignPrecedent Precedent { get; set; }
        public float SimilarityScore { get; set; }
        public Dictionary<string, float> MatchDimensions { get; set; }
        public string AdaptationNotes { get; set; }
    }

    public class BestSolutionResult
    {
        public bool Found { get; set; }
        public DesignPrecedent Precedent { get; set; }
        public float SimilarityScore { get; set; }
        public AdaptedSolution AdaptedSolution { get; set; }
        public string Explanation { get; set; }
        public DesignPrecedent AlternativePrecedent { get; set; }
        public float AlternativeSimilarity { get; set; }
        public Dictionary<string, float> MatchDimensions { get; set; }
        public float Confidence { get; set; }
    }

    public class AdaptedSolution
    {
        public string OriginalPrecedentId { get; set; }
        public string OriginalSolution { get; set; }
        public string AdaptedDescription { get; set; }
        public List<SolutionAdaptation> Adaptations { get; set; } = new List<SolutionAdaptation>();
        public float Confidence { get; set; }
    }

    public class SolutionAdaptation
    {
        public string Dimension { get; set; }
        public string OriginalValue { get; set; }
        public string TargetValue { get; set; }
        public string AdaptationNote { get; set; }
        public float ConfidenceImpact { get; set; }
    }

    public class PrecedentFilter
    {
        public string BuildingType { get; set; }
        public string SystemType { get; set; }
        public string ProblemCategory { get; set; }
        public string ClimateZone { get; set; }
        public string Scale { get; set; }
        public float MinConfidence { get; set; }
        public bool SuccessfulOnly { get; set; }
        public PrecedentSource? Source { get; set; }
        public int MaxResults { get; set; } = 50;
    }

    #endregion

    #region Outcome and Principle Types

    public class ApplicationOutcome
    {
        public bool Success { get; set; }
        public string LessonLearned { get; set; }
        public string AdaptationUsed { get; set; }
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Context { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A generalized design principle extracted from multiple precedents.
    /// </summary>
    public class DesignPrinciple
    {
        public string PrincipleId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public PrincipleType PrincipleType { get; set; }
        public float Confidence { get; set; }
        public int SupportingPrecedentCount { get; set; }
        public List<string> SupportingPrecedentIds { get; set; } = new List<string>();
        public DateTime ExtractedAt { get; set; }
    }

    public enum PrincipleType
    {
        SuccessFactor,
        CommonSolution,
        FailureAvoidance,
        ContextDependency,
        BestPractice
    }

    #endregion

    #region Statistics

    public class PrecedentDBStatistics
    {
        public int TotalPrecedents { get; set; }
        public Dictionary<string, int> PrecedentsBySource { get; set; }
        public Dictionary<string, int> PrecedentsByBuildingType { get; set; }
        public Dictionary<string, int> PrecedentsBySystemType { get; set; }
        public Dictionary<string, int> PrecedentsByCategory { get; set; }
        public int ExtractedPrinciples { get; set; }
        public int TotalSuccessfulApplications { get; set; }
        public int TotalFailedApplications { get; set; }
        public float AverageConfidence { get; set; }
        public string MostUsedPrecedentId { get; set; }
    }

    #endregion
}
