// StingBIM.AI.Intelligence.Uncertainty.KnowledgeGapAnalyzer
// Identifies and tracks what the system doesn't know - knowledge gaps, low-confidence areas,
// stale knowledge, and inference failures. Generates targeted questions to fill gaps.
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

namespace StingBIM.AI.Intelligence.Uncertainty
{
    #region Knowledge Gap Analyzer

    /// <summary>
    /// Identifies and tracks what the system doesn't know. Analyzes knowledge coverage
    /// across domains and depth levels, detects gaps, generates targeted questions to
    /// fill those gaps, and prioritizes knowledge acquisition efforts.
    /// </summary>
    public class KnowledgeGapAnalyzer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Domain and depth coverage tracking
        private readonly ConcurrentDictionary<string, DomainCoverage> _domainCoverage;
        private readonly ConcurrentDictionary<string, GapRecord> _identifiedGaps;
        private readonly ConcurrentDictionary<string, LowConfidenceDecision> _lowConfidenceDecisions;
        private readonly ConcurrentDictionary<string, InferenceFailureRecord> _inferenceFailures;
        private readonly ConcurrentDictionary<string, UserQuestionRecord> _unansweredQuestions;
        private readonly ConcurrentDictionary<string, KnowledgeAcquisitionPriority> _acquisitionPriorities;

        // Configuration
        private readonly KnowledgeGapConfiguration _configuration;
        private readonly ConcurrentQueue<GapAnalysisEvent> _eventQueue;
        private readonly Timer _periodicAnalysisTimer;
        private DateTime _lastFullAnalysis;

        // Known domains and depth levels
        private static readonly string[] KnownDomains = new[]
        {
            "Structural", "MEP", "HVAC", "Electrical", "Plumbing", "Fire",
            "Accessibility", "Architectural", "Envelope", "Sustainability",
            "Cost", "Schedule", "Safety", "Acoustics", "Lighting",
            "ThermalComfort", "IndoorAirQuality", "WaterEfficiency",
            "EnergyPerformance", "Materials", "Geotechnical",
            "SitePlanning", "VerticalTransportation", "Landscaping",
            "SecuritySystems", "Telecommunications", "SpecialSystems"
        };

        private static readonly string[] DepthLevels = new[]
        {
            "Conceptual", "Schematic", "DesignDevelopment", "ConstructionDocument",
            "Specification", "Detailing", "Installation", "Commissioning"
        };

        public KnowledgeGapAnalyzer()
            : this(new KnowledgeGapConfiguration())
        {
        }

        public KnowledgeGapAnalyzer(KnowledgeGapConfiguration configuration)
        {
            _configuration = configuration ?? new KnowledgeGapConfiguration();
            _domainCoverage = new ConcurrentDictionary<string, DomainCoverage>(StringComparer.OrdinalIgnoreCase);
            _identifiedGaps = new ConcurrentDictionary<string, GapRecord>(StringComparer.OrdinalIgnoreCase);
            _lowConfidenceDecisions = new ConcurrentDictionary<string, LowConfidenceDecision>(StringComparer.OrdinalIgnoreCase);
            _inferenceFailures = new ConcurrentDictionary<string, InferenceFailureRecord>(StringComparer.OrdinalIgnoreCase);
            _unansweredQuestions = new ConcurrentDictionary<string, UserQuestionRecord>(StringComparer.OrdinalIgnoreCase);
            _acquisitionPriorities = new ConcurrentDictionary<string, KnowledgeAcquisitionPriority>(StringComparer.OrdinalIgnoreCase);
            _eventQueue = new ConcurrentQueue<GapAnalysisEvent>();
            _lastFullAnalysis = DateTime.MinValue;

            InitializeDomainCoverage();

            // Periodic re-analysis every configured interval
            _periodicAnalysisTimer = new Timer(
                _ => EnqueuePeriodicAnalysis(),
                null,
                TimeSpan.FromMinutes(_configuration.PeriodicAnalysisIntervalMinutes),
                TimeSpan.FromMinutes(_configuration.PeriodicAnalysisIntervalMinutes));

            Logger.Info("KnowledgeGapAnalyzer initialized with {0} domains and {1} depth levels",
                KnownDomains.Length, DepthLevels.Length);
        }

        #region Public Analysis Methods

        /// <summary>
        /// Performs a full gap analysis across all domains and depth levels.
        /// Examines connectivity, domain coverage, confidence, temporal freshness,
        /// inference failures, and unanswered user questions.
        /// </summary>
        public async Task<KnowledgeGapReport> AnalyzeKnowledgeGapsAsync(
            KnowledgeGraphSnapshot graphSnapshot,
            SemanticMemorySnapshot memorySnapshot,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info("Starting full knowledge gap analysis");
            progress?.Report("Initializing knowledge gap analysis...");

            var report = new KnowledgeGapReport
            {
                AnalysisTimestamp = DateTime.UtcNow,
                AnalysisId = Guid.NewGuid().ToString("N")
            };

            try
            {
                // Step 1: Analyze connectivity gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing connectivity gaps...");
                var connectivityGaps = await AnalyzeConnectivityGapsAsync(graphSnapshot, cancellationToken);
                report.ConnectivityGaps = connectivityGaps;
                Logger.Debug("Found {0} connectivity gaps", connectivityGaps.Count);

                // Step 2: Analyze domain coverage gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing domain coverage gaps...");
                var coverageGaps = await AnalyzeDomainCoverageGapsAsync(graphSnapshot, memorySnapshot, cancellationToken);
                report.DomainCoverageGaps = coverageGaps;
                Logger.Debug("Found {0} domain coverage gaps", coverageGaps.Count);

                // Step 3: Analyze confidence gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing confidence gaps...");
                var confidenceGaps = await AnalyzeConfidenceGapsAsync(memorySnapshot, cancellationToken);
                report.ConfidenceGaps = confidenceGaps;
                Logger.Debug("Found {0} confidence gaps", confidenceGaps.Count);

                // Step 4: Analyze temporal gaps (stale knowledge)
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing temporal gaps...");
                var temporalGaps = await AnalyzeTemporalGapsAsync(memorySnapshot, cancellationToken);
                report.TemporalGaps = temporalGaps;
                Logger.Debug("Found {0} temporal gaps", temporalGaps.Count);

                // Step 5: Analyze inference failure gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing inference failure gaps...");
                var inferenceGaps = AnalyzeInferenceFailureGaps();
                report.InferenceFailureGaps = inferenceGaps;
                Logger.Debug("Found {0} inference failure gaps", inferenceGaps.Count);

                // Step 6: Analyze user question gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing user question gaps...");
                var questionGaps = AnalyzeUserQuestionGaps();
                report.UserQuestionGaps = questionGaps;
                Logger.Debug("Found {0} user question gaps", questionGaps.Count);

                // Step 7: Consolidate and aggregate all gaps
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Consolidating gap analysis...");
                report.AllGaps = ConsolidateGaps(report);
                report.TotalGapCount = report.AllGaps.Count;
                report.CriticalGapCount = report.AllGaps.Count(g => g.Severity == GapSeverity.Critical);
                report.OverallCoverageScore = CalculateOverallCoverageScore(report);

                // Step 8: Generate coverage matrix
                report.CoverageMatrix = BuildCoverageMatrix(graphSnapshot, memorySnapshot);

                // Update internal state
                lock (_lockObject)
                {
                    foreach (var gap in report.AllGaps)
                    {
                        _identifiedGaps[gap.GapId] = gap;
                    }
                    _lastFullAnalysis = DateTime.UtcNow;
                }

                // Recompute acquisition priorities
                await RecomputeAcquisitionPrioritiesAsync(report, cancellationToken);

                progress?.Report($"Gap analysis complete: {report.TotalGapCount} gaps found, " +
                                 $"{report.CriticalGapCount} critical. Coverage: {report.OverallCoverageScore:P1}");

                Logger.Info("Knowledge gap analysis complete: {0} gaps, coverage {1:P1}",
                    report.TotalGapCount, report.OverallCoverageScore);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Knowledge gap analysis was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during knowledge gap analysis");
                report.AnalysisErrors.Add(ex.Message);
            }

            return report;
        }

        /// <summary>
        /// Returns a ranked list of what the system should learn next,
        /// ordered by impact and urgency.
        /// </summary>
        public async Task<List<KnowledgeAcquisitionPriority>> GetGapPrioritiesAsync(
            int maxResults = 20,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Debug("Retrieving gap priorities, max {0}", maxResults);
            progress?.Report("Computing knowledge acquisition priorities...");

            var priorities = _acquisitionPriorities.Values
                .OrderByDescending(p => p.CompositeScore)
                .Take(maxResults)
                .ToList();

            if (priorities.Count == 0)
            {
                progress?.Report("No priorities computed yet. Running quick analysis...");
                // If no priorities yet, do a lightweight recomputation
                await RecomputeAcquisitionPrioritiesFromGapsAsync(cancellationToken);
                priorities = _acquisitionPriorities.Values
                    .OrderByDescending(p => p.CompositeScore)
                    .Take(maxResults)
                    .ToList();
            }

            progress?.Report($"Returning {priorities.Count} acquisition priorities");
            return priorities;
        }

        /// <summary>
        /// Generates targeted questions to fill identified knowledge gaps in a specific domain.
        /// Questions are formulated to be answerable by domain experts or data sources.
        /// </summary>
        public async Task<List<GeneratedQuestion>> GenerateQuestionsAsync(
            string domain,
            int maxQuestions = 10,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Debug("Generating questions for domain '{0}', max {1}", domain, maxQuestions);
            progress?.Report($"Generating questions for {domain}...");

            var questions = new List<GeneratedQuestion>();

            // Get gaps relevant to this domain
            var domainGaps = _identifiedGaps.Values
                .Where(g => string.Equals(g.Domain, domain, StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrEmpty(domain))
                .OrderByDescending(g => g.Impact)
                .Take(maxQuestions * 2)
                .ToList();

            foreach (var gap in domainGaps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gapQuestions = GenerateQuestionsForGap(gap);
                questions.AddRange(gapQuestions);

                if (questions.Count >= maxQuestions)
                    break;
            }

            // Also generate from inference failures in this domain
            var domainFailures = _inferenceFailures.Values
                .Where(f => string.Equals(f.Domain, domain, StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrEmpty(domain))
                .OrderByDescending(f => f.FailureCount)
                .Take(5)
                .ToList();

            foreach (var failure in domainFailures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var failureQuestion = GenerateQuestionFromInferenceFailure(failure);
                if (failureQuestion != null)
                    questions.Add(failureQuestion);
            }

            // Also generate from unanswered user questions
            var domainUserQuestions = _unansweredQuestions.Values
                .Where(q => string.Equals(q.Domain, domain, StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrEmpty(domain))
                .OrderByDescending(q => q.AskCount)
                .Take(5)
                .ToList();

            foreach (var uq in domainUserQuestions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                questions.Add(new GeneratedQuestion
                {
                    QuestionId = $"UQ_{uq.QuestionHash}",
                    Domain = uq.Domain,
                    Question = uq.OriginalQuestion,
                    Purpose = "Users have asked this question but the system could not answer",
                    Priority = QuestionPriority.High,
                    ExpectedAnswerType = "Factual",
                    TimesAsked = uq.AskCount,
                    SourceGapId = uq.QuestionHash
                });
            }

            var result = questions
                .OrderByDescending(q => q.Priority)
                .ThenByDescending(q => q.TimesAsked)
                .Take(maxQuestions)
                .ToList();

            progress?.Report($"Generated {result.Count} questions for {domain}");
            Logger.Info("Generated {0} questions for domain '{1}'", result.Count, domain);

            return result;
        }

        /// <summary>
        /// Produces a domain-by-depth coverage matrix showing knowledge density and quality.
        /// </summary>
        public async Task<CoverageReport> GetCoverageReportAsync(
            KnowledgeGraphSnapshot graphSnapshot = null,
            SemanticMemorySnapshot memorySnapshot = null,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Debug("Generating coverage report");
            progress?.Report("Building coverage report...");

            var report = new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow,
                Domains = KnownDomains.ToList(),
                DepthLevels = DepthLevels.ToList()
            };

            // Build coverage matrix
            if (graphSnapshot != null || memorySnapshot != null)
            {
                report.Matrix = BuildCoverageMatrix(graphSnapshot, memorySnapshot);
            }
            else
            {
                // Use cached domain coverage
                report.Matrix = BuildCoverageMatrixFromCache();
            }

            // Compute summary statistics
            report.DomainSummaries = new Dictionary<string, DomainCoverageSummary>(StringComparer.OrdinalIgnoreCase);
            foreach (var domain in KnownDomains)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var summary = new DomainCoverageSummary
                {
                    Domain = domain,
                    OverallScore = 0.0f,
                    DepthScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    GapCount = _identifiedGaps.Values.Count(g =>
                        string.Equals(g.Domain, domain, StringComparison.OrdinalIgnoreCase)),
                    NodeCount = 0,
                    FactCount = 0,
                    AverageConfidence = 0.0f,
                    StalenessRatio = 0.0f
                };

                if (_domainCoverage.TryGetValue(domain, out var coverage))
                {
                    summary.NodeCount = coverage.NodeCount;
                    summary.FactCount = coverage.FactCount;
                    summary.AverageConfidence = coverage.AverageConfidence;
                    summary.StalenessRatio = coverage.StalenessRatio;
                    summary.LastUpdated = coverage.LastUpdated;

                    float totalDepthScore = 0f;
                    int depthCount = 0;
                    foreach (var depth in DepthLevels)
                    {
                        float depthScore = 0f;
                        if (coverage.DepthCoverage.TryGetValue(depth, out var dc))
                        {
                            depthScore = dc;
                        }
                        summary.DepthScores[depth] = depthScore;
                        totalDepthScore += depthScore;
                        depthCount++;
                    }
                    summary.OverallScore = depthCount > 0 ? totalDepthScore / depthCount : 0f;
                }

                report.DomainSummaries[domain] = summary;
            }

            // Identify weakest areas
            report.WeakestDomains = report.DomainSummaries.Values
                .OrderBy(s => s.OverallScore)
                .Take(5)
                .Select(s => s.Domain)
                .ToList();

            report.StrongestDomains = report.DomainSummaries.Values
                .OrderByDescending(s => s.OverallScore)
                .Take(5)
                .Select(s => s.Domain)
                .ToList();

            report.OverallCoverage = report.DomainSummaries.Values.Any()
                ? report.DomainSummaries.Values.Average(s => s.OverallScore)
                : 0f;

            progress?.Report($"Coverage report complete: overall {report.OverallCoverage:P1}");
            return report;
        }

        /// <summary>
        /// Tracks a decision made with low confidence for later analysis.
        /// Any decision with confidence below the threshold (default 0.7) is logged.
        /// </summary>
        public void TrackLowConfidenceDecision(
            string decisionDescription,
            float confidence,
            Dictionary<string, object> context)
        {
            if (confidence >= _configuration.LowConfidenceThreshold)
                return;

            var domain = ExtractDomainFromContext(context);
            var decisionId = $"LCD_{Guid.NewGuid():N}";

            var decision = new LowConfidenceDecision
            {
                DecisionId = decisionId,
                Description = decisionDescription,
                Confidence = confidence,
                Domain = domain,
                Context = context ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow,
                ConfidenceDeficit = _configuration.LowConfidenceThreshold - confidence
            };

            _lowConfidenceDecisions[decisionId] = decision;

            // Maintain bounded collection
            PruneLowConfidenceDecisions();

            Logger.Debug("Tracked low-confidence decision: '{0}' at {1:P1} (domain: {2})",
                decisionDescription, confidence, domain);

            // Enqueue event for background processing
            _eventQueue.Enqueue(new GapAnalysisEvent
            {
                EventType = GapEventType.LowConfidenceDecision,
                Domain = domain,
                Data = decision,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Records that the system failed to answer a user question.
        /// Tracks frequency and domain to identify systematic knowledge gaps.
        /// </summary>
        public void TrackUnansweredQuestion(string question, string domain, Dictionary<string, object> context)
        {
            var hash = ComputeQuestionHash(question);
            var key = $"{domain}_{hash}";

            _unansweredQuestions.AddOrUpdate(key,
                new UserQuestionRecord
                {
                    QuestionHash = hash,
                    OriginalQuestion = question,
                    Domain = domain ?? "Unknown",
                    AskCount = 1,
                    FirstAsked = DateTime.UtcNow,
                    LastAsked = DateTime.UtcNow,
                    Context = context ?? new Dictionary<string, object>()
                },
                (_, existing) =>
                {
                    existing.AskCount++;
                    existing.LastAsked = DateTime.UtcNow;
                    return existing;
                });

            Logger.Debug("Tracked unanswered question in domain '{0}': '{1}'",
                domain, TruncateForLog(question, 80));
        }

        /// <summary>
        /// Records an inference failure for gap analysis.
        /// </summary>
        public void TrackInferenceFailure(
            string topic,
            string domain,
            string failureReason,
            Dictionary<string, object> context)
        {
            var key = $"{domain}_{topic}".ToUpperInvariant();

            _inferenceFailures.AddOrUpdate(key,
                new InferenceFailureRecord
                {
                    Topic = topic,
                    Domain = domain ?? "Unknown",
                    FailureReasons = new List<string> { failureReason },
                    FailureCount = 1,
                    FirstOccurrence = DateTime.UtcNow,
                    LastOccurrence = DateTime.UtcNow,
                    Context = context ?? new Dictionary<string, object>()
                },
                (_, existing) =>
                {
                    existing.FailureCount++;
                    existing.LastOccurrence = DateTime.UtcNow;
                    if (!existing.FailureReasons.Contains(failureReason))
                    {
                        existing.FailureReasons.Add(failureReason);
                        // Keep bounded
                        if (existing.FailureReasons.Count > 20)
                            existing.FailureReasons.RemoveAt(0);
                    }
                    return existing;
                });

            Logger.Debug("Tracked inference failure for '{0}' in domain '{1}': {2}",
                topic, domain, failureReason);
        }

        /// <summary>
        /// Updates domain coverage data from an external knowledge graph snapshot.
        /// Call this after loading or updating the knowledge graph.
        /// </summary>
        public void UpdateDomainCoverage(string domain, int nodeCount, int factCount,
            float averageConfidence, Dictionary<string, float> depthCoverage)
        {
            _domainCoverage.AddOrUpdate(domain,
                new DomainCoverage
                {
                    Domain = domain,
                    NodeCount = nodeCount,
                    FactCount = factCount,
                    AverageConfidence = averageConfidence,
                    DepthCoverage = depthCoverage ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    LastUpdated = DateTime.UtcNow,
                    StalenessRatio = 0f
                },
                (_, existing) =>
                {
                    existing.NodeCount = nodeCount;
                    existing.FactCount = factCount;
                    existing.AverageConfidence = averageConfidence;
                    if (depthCoverage != null)
                        existing.DepthCoverage = depthCoverage;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Generates heatmap data for knowledge coverage visualization.
        /// Returns a 2D array [domain][depth] with coverage scores 0.0 to 1.0.
        /// </summary>
        public KnowledgeHeatmapData GetCoverageHeatmap()
        {
            var heatmap = new KnowledgeHeatmapData
            {
                DomainLabels = KnownDomains.ToList(),
                DepthLabels = DepthLevels.ToList(),
                Values = new float[KnownDomains.Length, DepthLevels.Length],
                ConfidenceValues = new float[KnownDomains.Length, DepthLevels.Length],
                GapFlags = new bool[KnownDomains.Length, DepthLevels.Length],
                GeneratedAt = DateTime.UtcNow
            };

            for (int i = 0; i < KnownDomains.Length; i++)
            {
                var domain = KnownDomains[i];
                _domainCoverage.TryGetValue(domain, out var coverage);

                for (int j = 0; j < DepthLevels.Length; j++)
                {
                    var depth = DepthLevels[j];
                    float coverageScore = 0f;
                    float confidenceScore = 0f;

                    if (coverage != null)
                    {
                        coverage.DepthCoverage.TryGetValue(depth, out coverageScore);
                        confidenceScore = coverage.AverageConfidence;
                    }

                    heatmap.Values[i, j] = coverageScore;
                    heatmap.ConfidenceValues[i, j] = confidenceScore;
                    heatmap.GapFlags[i, j] = coverageScore < _configuration.CoverageGapThreshold;
                }
            }

            return heatmap;
        }

        /// <summary>
        /// Suggests CSV data files that would help fill identified knowledge gaps.
        /// Maps gaps to potential data sources for the CsvKnowledgeIngester.
        /// </summary>
        public List<DataFileRecommendation> GetDataFileRecommendations()
        {
            var recommendations = new List<DataFileRecommendation>();

            var gapsByDomain = _identifiedGaps.Values
                .GroupBy(g => g.Domain)
                .OrderByDescending(g => g.Sum(gap => gap.Impact))
                .ToList();

            foreach (var domainGroup in gapsByDomain)
            {
                var domain = domainGroup.Key;
                var gapCount = domainGroup.Count();
                var avgImpact = domainGroup.Average(g => g.Impact);

                var suggestedFiles = MapDomainToDataFiles(domain);
                foreach (var file in suggestedFiles)
                {
                    recommendations.Add(new DataFileRecommendation
                    {
                        Domain = domain,
                        SuggestedFileName = file.FileName,
                        SuggestedFormat = file.Format,
                        SuggestedColumns = file.SuggestedColumns,
                        GapCount = gapCount,
                        EstimatedImpact = avgImpact,
                        Rationale = $"Would address {gapCount} knowledge gaps in {domain} " +
                                    $"with average impact {avgImpact:F2}"
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.EstimatedImpact * r.GapCount).ToList();
        }

        /// <summary>
        /// Gets statistics about the current state of knowledge gaps.
        /// </summary>
        public KnowledgeGapStatistics GetStatistics()
        {
            return new KnowledgeGapStatistics
            {
                TotalGapsIdentified = _identifiedGaps.Count,
                CriticalGaps = _identifiedGaps.Values.Count(g => g.Severity == GapSeverity.Critical),
                HighGaps = _identifiedGaps.Values.Count(g => g.Severity == GapSeverity.High),
                MediumGaps = _identifiedGaps.Values.Count(g => g.Severity == GapSeverity.Medium),
                LowGaps = _identifiedGaps.Values.Count(g => g.Severity == GapSeverity.Low),
                LowConfidenceDecisionCount = _lowConfidenceDecisions.Count,
                InferenceFailureCount = _inferenceFailures.Values.Sum(f => f.FailureCount),
                UnansweredQuestionCount = _unansweredQuestions.Count,
                DomainsWithGaps = _identifiedGaps.Values.Select(g => g.Domain).Distinct().Count(),
                LastFullAnalysis = _lastFullAnalysis,
                AverageGapImpact = _identifiedGaps.Values.Any()
                    ? _identifiedGaps.Values.Average(g => g.Impact) : 0f
            };
        }

        #endregion

        #region Private Analysis Methods

        private async Task<List<GapRecord>> AnalyzeConnectivityGapsAsync(
            KnowledgeGraphSnapshot graph,
            CancellationToken cancellationToken)
        {
            var gaps = new List<GapRecord>();
            if (graph == null) return gaps;

            await Task.Run(() =>
            {
                // Find nodes with very few connections (isolated knowledge)
                foreach (var node in graph.Nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int edgeCount = graph.GetEdgeCountForNode(node.Id);
                    if (edgeCount < _configuration.MinEdgesForConnectivity)
                    {
                        var domain = node.NodeType ?? "Unknown";
                        var gap = new GapRecord
                        {
                            GapId = $"CONN_{node.Id}",
                            GapType = GapType.Connectivity,
                            Domain = domain,
                            Description = $"Node '{node.Name}' has only {edgeCount} connections " +
                                          $"(minimum: {_configuration.MinEdgesForConnectivity})",
                            Severity = edgeCount == 0 ? GapSeverity.High : GapSeverity.Medium,
                            Impact = CalculateConnectivityImpact(node, edgeCount),
                            AffectedNodeId = node.Id,
                            AffectedNodeName = node.Name,
                            DetectedAt = DateTime.UtcNow,
                            SuggestedAction = $"Add relationships connecting '{node.Name}' " +
                                              $"to related concepts in {domain}"
                        };
                        gaps.Add(gap);
                    }
                }

                // Find disconnected clusters
                var clusters = FindDisconnectedClusters(graph);
                foreach (var cluster in clusters.Where(c => c.NodeCount < _configuration.MinClusterSize))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    gaps.Add(new GapRecord
                    {
                        GapId = $"CLUST_{cluster.ClusterId}",
                        GapType = GapType.Connectivity,
                        Domain = cluster.PrimaryDomain,
                        Description = $"Small disconnected cluster of {cluster.NodeCount} nodes " +
                                      $"in {cluster.PrimaryDomain}",
                        Severity = GapSeverity.Medium,
                        Impact = 0.5f * cluster.NodeCount / 10f,
                        DetectedAt = DateTime.UtcNow,
                        SuggestedAction = $"Bridge cluster to main knowledge graph via " +
                                          $"cross-domain relationships"
                    });
                }
            }, cancellationToken);

            return gaps;
        }

        private async Task<List<GapRecord>> AnalyzeDomainCoverageGapsAsync(
            KnowledgeGraphSnapshot graph,
            SemanticMemorySnapshot memory,
            CancellationToken cancellationToken)
        {
            var gaps = new List<GapRecord>();

            await Task.Run(() =>
            {
                foreach (var domain in KnownDomains)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int nodeCount = graph?.GetNodeCountByType(domain) ?? 0;
                    int factCount = memory?.GetFactCountByCategory(domain) ?? 0;
                    int totalKnowledge = nodeCount + factCount;

                    // Update internal coverage
                    UpdateDomainCoverageInternal(domain, nodeCount, factCount, graph, memory);

                    if (totalKnowledge < _configuration.MinNodesPerDomain)
                    {
                        float deficit = 1.0f - ((float)totalKnowledge / _configuration.MinNodesPerDomain);
                        var severity = deficit > 0.8f ? GapSeverity.Critical :
                                       deficit > 0.5f ? GapSeverity.High :
                                       deficit > 0.3f ? GapSeverity.Medium : GapSeverity.Low;

                        gaps.Add(new GapRecord
                        {
                            GapId = $"COV_{domain}",
                            GapType = GapType.DomainCoverage,
                            Domain = domain,
                            Description = $"Domain '{domain}' has only {totalKnowledge} knowledge items " +
                                          $"(minimum: {_configuration.MinNodesPerDomain})",
                            Severity = severity,
                            Impact = deficit,
                            DetectedAt = DateTime.UtcNow,
                            SuggestedAction = $"Add {_configuration.MinNodesPerDomain - totalKnowledge} " +
                                              $"more knowledge items to {domain}",
                            Metadata = new Dictionary<string, object>
                            {
                                ["NodeCount"] = nodeCount,
                                ["FactCount"] = factCount,
                                ["Deficit"] = deficit
                            }
                        });
                    }

                    // Check depth coverage within the domain
                    foreach (var depth in DepthLevels)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        float depthScore = CalculateDepthScore(domain, depth, graph, memory);
                        if (depthScore < _configuration.CoverageGapThreshold)
                        {
                            gaps.Add(new GapRecord
                            {
                                GapId = $"COV_{domain}_{depth}",
                                GapType = GapType.DomainCoverage,
                                Domain = domain,
                                Description = $"Domain '{domain}' has poor coverage at " +
                                              $"'{depth}' depth level ({depthScore:P0})",
                                Severity = depthScore < 0.1f ? GapSeverity.High : GapSeverity.Medium,
                                Impact = 1.0f - depthScore,
                                DetectedAt = DateTime.UtcNow,
                                SuggestedAction = $"Add {depth}-level knowledge for {domain}",
                                Metadata = new Dictionary<string, object>
                                {
                                    ["DepthLevel"] = depth,
                                    ["CoverageScore"] = depthScore
                                }
                            });
                        }
                    }
                }
            }, cancellationToken);

            return gaps;
        }

        private async Task<List<GapRecord>> AnalyzeConfidenceGapsAsync(
            SemanticMemorySnapshot memory,
            CancellationToken cancellationToken)
        {
            var gaps = new List<GapRecord>();
            if (memory == null) return gaps;

            await Task.Run(() =>
            {
                // Group facts by domain/category and check average confidence
                var factsByCategory = memory.Facts
                    .GroupBy(f => f.Category ?? "Unknown")
                    .ToList();

                foreach (var group in factsByCategory)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var avgConfidence = group.Average(f => f.Confidence);
                    var lowConfCount = group.Count(f => f.Confidence < _configuration.LowConfidenceThreshold);
                    var totalCount = group.Count();
                    var lowConfRatio = (float)lowConfCount / totalCount;

                    if (avgConfidence < _configuration.LowConfidenceThreshold ||
                        lowConfRatio > _configuration.LowConfidenceRatioThreshold)
                    {
                        gaps.Add(new GapRecord
                        {
                            GapId = $"CONF_{group.Key}",
                            GapType = GapType.Confidence,
                            Domain = group.Key,
                            Description = $"Domain '{group.Key}' has low average confidence " +
                                          $"({avgConfidence:P0}). {lowConfCount}/{totalCount} facts " +
                                          $"below threshold.",
                            Severity = avgConfidence < 0.3f ? GapSeverity.Critical :
                                       avgConfidence < 0.5f ? GapSeverity.High : GapSeverity.Medium,
                            Impact = 1.0f - avgConfidence,
                            DetectedAt = DateTime.UtcNow,
                            SuggestedAction = $"Verify and reinforce facts in {group.Key}. " +
                                              $"Cross-reference with authoritative sources.",
                            Metadata = new Dictionary<string, object>
                            {
                                ["AverageConfidence"] = avgConfidence,
                                ["LowConfidenceCount"] = lowConfCount,
                                ["TotalFactCount"] = totalCount,
                                ["LowConfidenceRatio"] = lowConfRatio
                            }
                        });
                    }
                }
            }, cancellationToken);

            return gaps;
        }

        private async Task<List<GapRecord>> AnalyzeTemporalGapsAsync(
            SemanticMemorySnapshot memory,
            CancellationToken cancellationToken)
        {
            var gaps = new List<GapRecord>();
            if (memory == null) return gaps;

            var staleThreshold = DateTime.UtcNow.AddDays(-_configuration.StalenessThresholdDays);

            await Task.Run(() =>
            {
                var factsByCategory = memory.Facts
                    .GroupBy(f => f.Category ?? "Unknown")
                    .ToList();

                foreach (var group in factsByCategory)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var staleCount = group.Count(f => f.LastUpdated < staleThreshold);
                    var totalCount = group.Count();
                    var staleRatio = (float)staleCount / totalCount;

                    if (staleRatio > _configuration.StalenessRatioThreshold)
                    {
                        var oldestFact = group.OrderBy(f => f.LastUpdated).First();
                        var daysSinceOldest = (DateTime.UtcNow - oldestFact.LastUpdated).TotalDays;

                        gaps.Add(new GapRecord
                        {
                            GapId = $"TEMP_{group.Key}",
                            GapType = GapType.Temporal,
                            Domain = group.Key,
                            Description = $"Domain '{group.Key}' has {staleCount}/{totalCount} stale facts " +
                                          $"(not updated in {_configuration.StalenessThresholdDays}+ days). " +
                                          $"Oldest: {daysSinceOldest:F0} days ago.",
                            Severity = staleRatio > 0.8f ? GapSeverity.High :
                                       staleRatio > 0.5f ? GapSeverity.Medium : GapSeverity.Low,
                            Impact = staleRatio,
                            DetectedAt = DateTime.UtcNow,
                            SuggestedAction = $"Refresh knowledge in {group.Key}. " +
                                              $"Check for updated standards, codes, or best practices.",
                            Metadata = new Dictionary<string, object>
                            {
                                ["StaleCount"] = staleCount,
                                ["TotalCount"] = totalCount,
                                ["StaleRatio"] = staleRatio,
                                ["OldestDaysAgo"] = daysSinceOldest
                            }
                        });
                    }

                    // Update domain staleness ratio
                    if (_domainCoverage.TryGetValue(group.Key, out var coverage))
                    {
                        coverage.StalenessRatio = staleRatio;
                    }
                }
            }, cancellationToken);

            return gaps;
        }

        private List<GapRecord> AnalyzeInferenceFailureGaps()
        {
            var gaps = new List<GapRecord>();

            var failuresByDomain = _inferenceFailures.Values
                .GroupBy(f => f.Domain)
                .ToList();

            foreach (var group in failuresByDomain)
            {
                var totalFailures = group.Sum(f => f.FailureCount);
                var uniqueTopics = group.Select(f => f.Topic).Distinct().Count();

                if (totalFailures >= _configuration.MinInferenceFailuresForGap)
                {
                    var topFailures = group.OrderByDescending(f => f.FailureCount).Take(3).ToList();
                    var topTopics = string.Join(", ", topFailures.Select(f => f.Topic));

                    gaps.Add(new GapRecord
                    {
                        GapId = $"INF_{group.Key}",
                        GapType = GapType.InferenceFailure,
                        Domain = group.Key,
                        Description = $"Reasoning frequently fails in domain '{group.Key}': " +
                                      $"{totalFailures} failures across {uniqueTopics} topics. " +
                                      $"Top failing topics: {topTopics}",
                        Severity = totalFailures > 20 ? GapSeverity.Critical :
                                   totalFailures > 10 ? GapSeverity.High : GapSeverity.Medium,
                        Impact = Math.Min(1.0f, totalFailures / 30.0f),
                        DetectedAt = DateTime.UtcNow,
                        SuggestedAction = $"Add inference rules and relationships for {group.Key}. " +
                                          $"Focus on: {topTopics}",
                        Metadata = new Dictionary<string, object>
                        {
                            ["TotalFailures"] = totalFailures,
                            ["UniqueTopics"] = uniqueTopics,
                            ["TopFailingTopics"] = topFailures.Select(f => f.Topic).ToList()
                        }
                    });
                }
            }

            return gaps;
        }

        private List<GapRecord> AnalyzeUserQuestionGaps()
        {
            var gaps = new List<GapRecord>();

            var questionsByDomain = _unansweredQuestions.Values
                .GroupBy(q => q.Domain)
                .ToList();

            foreach (var group in questionsByDomain)
            {
                var totalAsks = group.Sum(q => q.AskCount);
                var uniqueQuestions = group.Count();

                if (uniqueQuestions >= _configuration.MinUnansweredQuestionsForGap ||
                    totalAsks >= _configuration.MinUnansweredQuestionsForGap * 2)
                {
                    var topQuestions = group.OrderByDescending(q => q.AskCount).Take(3).ToList();
                    var examples = string.Join("; ",
                        topQuestions.Select(q => TruncateForLog(q.OriginalQuestion, 60)));

                    gaps.Add(new GapRecord
                    {
                        GapId = $"UQ_{group.Key}",
                        GapType = GapType.UserQuestion,
                        Domain = group.Key,
                        Description = $"Users have asked {totalAsks} questions about '{group.Key}' " +
                                      $"that the system could not answer ({uniqueQuestions} unique). " +
                                      $"Examples: {examples}",
                        Severity = totalAsks > 15 ? GapSeverity.High :
                                   totalAsks > 5 ? GapSeverity.Medium : GapSeverity.Low,
                        Impact = Math.Min(1.0f, totalAsks / 20.0f),
                        DetectedAt = DateTime.UtcNow,
                        SuggestedAction = $"Add knowledge to answer user questions about {group.Key}",
                        Metadata = new Dictionary<string, object>
                        {
                            ["TotalAsks"] = totalAsks,
                            ["UniqueQuestions"] = uniqueQuestions,
                            ["TopQuestions"] = topQuestions.Select(q => q.OriginalQuestion).ToList()
                        }
                    });
                }
            }

            return gaps;
        }

        #endregion

        #region Question Generation

        private List<GeneratedQuestion> GenerateQuestionsForGap(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();

            switch (gap.GapType)
            {
                case GapType.DomainCoverage:
                    questions.AddRange(GenerateDomainCoverageQuestions(gap));
                    break;

                case GapType.Connectivity:
                    questions.AddRange(GenerateConnectivityQuestions(gap));
                    break;

                case GapType.Confidence:
                    questions.AddRange(GenerateConfidenceQuestions(gap));
                    break;

                case GapType.Temporal:
                    questions.AddRange(GenerateTemporalQuestions(gap));
                    break;

                case GapType.InferenceFailure:
                    questions.AddRange(GenerateInferenceQuestions(gap));
                    break;

                case GapType.UserQuestion:
                    // User questions are already questions; pass them through
                    break;
            }

            return questions;
        }

        private List<GeneratedQuestion> GenerateDomainCoverageQuestions(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();
            var domain = gap.Domain;

            // Generate domain-specific questions using templates
            var templates = GetQuestionTemplatesForDomain(domain);
            foreach (var template in templates.Take(3))
            {
                questions.Add(new GeneratedQuestion
                {
                    QuestionId = $"DCQ_{gap.GapId}_{questions.Count}",
                    Domain = domain,
                    Question = template,
                    Purpose = $"Fill coverage gap in {domain}",
                    Priority = gap.Severity == GapSeverity.Critical ? QuestionPriority.Critical :
                               gap.Severity == GapSeverity.High ? QuestionPriority.High :
                               QuestionPriority.Medium,
                    ExpectedAnswerType = "Factual",
                    SourceGapId = gap.GapId
                });
            }

            return questions;
        }

        private List<GeneratedQuestion> GenerateConnectivityQuestions(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();
            var nodeName = gap.AffectedNodeName ?? gap.Domain;

            questions.Add(new GeneratedQuestion
            {
                QuestionId = $"CNQ_{gap.GapId}_rel",
                Domain = gap.Domain,
                Question = $"What are the primary relationships between '{nodeName}' and other " +
                           $"{gap.Domain} concepts?",
                Purpose = $"Establish connections for isolated knowledge node '{nodeName}'",
                Priority = QuestionPriority.Medium,
                ExpectedAnswerType = "Relational",
                SourceGapId = gap.GapId
            });

            questions.Add(new GeneratedQuestion
            {
                QuestionId = $"CNQ_{gap.GapId}_dep",
                Domain = gap.Domain,
                Question = $"What depends on '{nodeName}' and what does '{nodeName}' depend on " +
                           $"in a typical BIM project?",
                Purpose = $"Identify dependency chains for '{nodeName}'",
                Priority = QuestionPriority.Medium,
                ExpectedAnswerType = "Relational",
                SourceGapId = gap.GapId
            });

            return questions;
        }

        private List<GeneratedQuestion> GenerateConfidenceQuestions(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();

            questions.Add(new GeneratedQuestion
            {
                QuestionId = $"CFQ_{gap.GapId}_verify",
                Domain = gap.Domain,
                Question = $"What are the authoritative references for {gap.Domain} requirements " +
                           $"in current building codes?",
                Purpose = $"Verify and reinforce low-confidence knowledge in {gap.Domain}",
                Priority = QuestionPriority.High,
                ExpectedAnswerType = "Reference",
                SourceGapId = gap.GapId
            });

            return questions;
        }

        private List<GeneratedQuestion> GenerateTemporalQuestions(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();

            questions.Add(new GeneratedQuestion
            {
                QuestionId = $"TMQ_{gap.GapId}_update",
                Domain = gap.Domain,
                Question = $"What has changed in {gap.Domain} standards or best practices in the last " +
                           $"{_configuration.StalenessThresholdDays} days?",
                Purpose = $"Refresh stale knowledge in {gap.Domain}",
                Priority = QuestionPriority.Medium,
                ExpectedAnswerType = "Update",
                SourceGapId = gap.GapId
            });

            return questions;
        }

        private List<GeneratedQuestion> GenerateInferenceQuestions(GapRecord gap)
        {
            var questions = new List<GeneratedQuestion>();

            if (gap.Metadata != null &&
                gap.Metadata.TryGetValue("TopFailingTopics", out var topicsObj) &&
                topicsObj is List<string> topics)
            {
                foreach (var topic in topics.Take(2))
                {
                    questions.Add(new GeneratedQuestion
                    {
                        QuestionId = $"IFQ_{gap.GapId}_{topic.GetHashCode():X}",
                        Domain = gap.Domain,
                        Question = $"What are the rules and relationships governing '{topic}' in {gap.Domain}?",
                        Purpose = $"Fix inference failures for '{topic}'",
                        Priority = QuestionPriority.High,
                        ExpectedAnswerType = "Rules",
                        SourceGapId = gap.GapId
                    });
                }
            }

            return questions;
        }

        private GeneratedQuestion GenerateQuestionFromInferenceFailure(InferenceFailureRecord failure)
        {
            return new GeneratedQuestion
            {
                QuestionId = $"IFQ_{failure.Topic.GetHashCode():X}",
                Domain = failure.Domain,
                Question = $"What rules govern '{failure.Topic}' in {failure.Domain}? " +
                           $"Common failure: {failure.FailureReasons.FirstOrDefault()}",
                Purpose = $"Address repeated inference failure ({failure.FailureCount} occurrences)",
                Priority = failure.FailureCount > 10 ? QuestionPriority.Critical : QuestionPriority.High,
                ExpectedAnswerType = "Rules",
                TimesAsked = failure.FailureCount
            };
        }

        private List<string> GetQuestionTemplatesForDomain(string domain)
        {
            var templates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Structural"] = new List<string>
                {
                    "What structural load requirements apply to {building_type} buildings per ACI 318?",
                    "What are the minimum foundation requirements for different soil bearing capacities?",
                    "What fire rating is required for structural steel members in {occupancy_type} occupancies?"
                },
                ["MEP"] = new List<string>
                {
                    "What are the standard pipe sizing requirements for different plumbing fixture counts?",
                    "What clearance requirements exist for mechanical equipment access?",
                    "What are the duct sizing criteria for different air volume requirements?"
                },
                ["HVAC"] = new List<string>
                {
                    "What are the minimum ventilation rates per ASHRAE 62.1 for different space types?",
                    "What equipment efficiency requirements apply in different climate zones per ASHRAE 90.1?",
                    "What are the thermal comfort parameters per ASHRAE 55 for office environments?"
                },
                ["Electrical"] = new List<string>
                {
                    "What are the circuit loading requirements per NEC for commercial buildings?",
                    "What emergency lighting requirements apply to different occupancy types?",
                    "What are the grounding and bonding requirements for healthcare facilities?"
                },
                ["Fire"] = new List<string>
                {
                    "What fire rating is required for {wall_type} walls in {occupancy_type} occupancies?",
                    "What are the maximum travel distances to exits per building code?",
                    "What sprinkler system requirements apply to high-rise buildings?"
                },
                ["Accessibility"] = new List<string>
                {
                    "What is the minimum ceiling height for {room_type} in {building_code}?",
                    "What are the ADA requirements for accessible route widths and turning radii?",
                    "What percentage of parking spaces must be accessible per ADA?"
                },
                ["Sustainability"] = new List<string>
                {
                    "What are the LEED v4.1 credit requirements for water efficiency?",
                    "What embodied carbon limits are becoming standard for different building types?",
                    "What on-site renewable energy percentage is required for net-zero buildings?"
                },
                ["Cost"] = new List<string>
                {
                    "What are typical cost per square meter benchmarks for {building_type} in {region}?",
                    "What percentage contingency is standard for different project phases?",
                    "What are typical cost escalation factors for multi-year construction projects?"
                }
            };

            if (templates.TryGetValue(domain, out var domainTemplates))
                return domainTemplates;

            // Generic templates for any domain
            return new List<string>
            {
                $"What are the primary code requirements for {domain} in commercial buildings?",
                $"What are the key design parameters for {domain} systems?",
                $"What best practices apply to {domain} coordination with other disciplines?"
            };
        }

        #endregion

        #region Coverage Matrix and Scoring

        private CoverageMatrix BuildCoverageMatrix(
            KnowledgeGraphSnapshot graph,
            SemanticMemorySnapshot memory)
        {
            var matrix = new CoverageMatrix
            {
                DomainLabels = KnownDomains.ToList(),
                DepthLabels = DepthLevels.ToList(),
                Cells = new CoverageCell[KnownDomains.Length, DepthLevels.Length]
            };

            for (int i = 0; i < KnownDomains.Length; i++)
            {
                for (int j = 0; j < DepthLevels.Length; j++)
                {
                    var domain = KnownDomains[i];
                    var depth = DepthLevels[j];

                    matrix.Cells[i, j] = new CoverageCell
                    {
                        Domain = domain,
                        Depth = depth,
                        CoverageScore = CalculateDepthScore(domain, depth, graph, memory),
                        NodeCount = graph?.GetNodeCountByTypeAndDepth(domain, depth) ?? 0,
                        FactCount = memory?.GetFactCountByCategoryAndDepth(domain, depth) ?? 0,
                        AverageConfidence = memory?.GetAverageConfidenceByCategoryAndDepth(domain, depth) ?? 0f,
                        HasGap = false
                    };
                    matrix.Cells[i, j].HasGap =
                        matrix.Cells[i, j].CoverageScore < _configuration.CoverageGapThreshold;
                }
            }

            return matrix;
        }

        private CoverageMatrix BuildCoverageMatrixFromCache()
        {
            var matrix = new CoverageMatrix
            {
                DomainLabels = KnownDomains.ToList(),
                DepthLabels = DepthLevels.ToList(),
                Cells = new CoverageCell[KnownDomains.Length, DepthLevels.Length]
            };

            for (int i = 0; i < KnownDomains.Length; i++)
            {
                for (int j = 0; j < DepthLevels.Length; j++)
                {
                    var domain = KnownDomains[i];
                    var depth = DepthLevels[j];
                    float score = 0f;

                    if (_domainCoverage.TryGetValue(domain, out var coverage) &&
                        coverage.DepthCoverage.TryGetValue(depth, out var depthScore))
                    {
                        score = depthScore;
                    }

                    matrix.Cells[i, j] = new CoverageCell
                    {
                        Domain = domain,
                        Depth = depth,
                        CoverageScore = score,
                        HasGap = score < _configuration.CoverageGapThreshold
                    };
                }
            }

            return matrix;
        }

        private float CalculateDepthScore(string domain, string depth,
            KnowledgeGraphSnapshot graph, SemanticMemorySnapshot memory)
        {
            int nodeCount = graph?.GetNodeCountByTypeAndDepth(domain, depth) ?? 0;
            int factCount = memory?.GetFactCountByCategoryAndDepth(domain, depth) ?? 0;
            float avgConf = memory?.GetAverageConfidenceByCategoryAndDepth(domain, depth) ?? 0f;

            int totalItems = nodeCount + factCount;
            if (totalItems == 0) return 0f;

            // Score based on quantity and quality
            float quantityScore = Math.Min(1.0f, totalItems / (float)_configuration.MinItemsPerDepthLevel);
            float qualityScore = avgConf;

            return quantityScore * 0.6f + qualityScore * 0.4f;
        }

        private float CalculateOverallCoverageScore(KnowledgeGapReport report)
        {
            if (report.CoverageMatrix == null) return 0f;

            float totalScore = 0f;
            int count = 0;

            for (int i = 0; i < KnownDomains.Length; i++)
            {
                for (int j = 0; j < DepthLevels.Length; j++)
                {
                    if (report.CoverageMatrix.Cells[i, j] != null)
                    {
                        totalScore += report.CoverageMatrix.Cells[i, j].CoverageScore;
                        count++;
                    }
                }
            }

            return count > 0 ? totalScore / count : 0f;
        }

        private float CalculateConnectivityImpact(KnowledgeGraphNodeInfo node, int edgeCount)
        {
            // Nodes with important types have higher impact when isolated
            var highImpactTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Structural", "Fire", "Safety", "Accessibility", "MEP"
            };

            float typeMultiplier = highImpactTypes.Contains(node.NodeType) ? 1.5f : 1.0f;
            float isolationFactor = 1.0f - (edgeCount / (float)_configuration.MinEdgesForConnectivity);

            return Math.Min(1.0f, isolationFactor * typeMultiplier);
        }

        #endregion

        #region Priority Computation

        private async Task RecomputeAcquisitionPrioritiesAsync(
            KnowledgeGapReport report,
            CancellationToken cancellationToken)
        {
            _acquisitionPriorities.Clear();

            await Task.Run(() =>
            {
                foreach (var gap in report.AllGaps)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var priority = ComputePriorityForGap(gap);
                    _acquisitionPriorities[priority.PriorityId] = priority;
                }

                // Also generate priorities from low-confidence decisions
                var decisionsByDomain = _lowConfidenceDecisions.Values
                    .GroupBy(d => d.Domain)
                    .ToList();

                foreach (var group in decisionsByDomain)
                {
                    var avgDeficit = group.Average(d => d.ConfidenceDeficit);
                    var count = group.Count();
                    var priorityId = $"LCD_{group.Key}";

                    _acquisitionPriorities[priorityId] = new KnowledgeAcquisitionPriority
                    {
                        PriorityId = priorityId,
                        Domain = group.Key,
                        Description = $"Low-confidence decisions in {group.Key}: " +
                                      $"{count} decisions averaging {avgDeficit:P0} below threshold",
                        ImpactScore = avgDeficit * (count / 10.0f),
                        UrgencyScore = count > 10 ? 1.0f : count / 10.0f,
                        FrequencyScore = Math.Min(1.0f, count / 20.0f),
                        CompositeScore = 0f, // computed below
                        SuggestedAction = $"Improve knowledge quality in {group.Key} to raise decision confidence",
                        GapType = GapType.Confidence,
                        RelatedGapIds = group.Select(d => d.DecisionId).Take(5).ToList()
                    };
                    _acquisitionPriorities[priorityId].CompositeScore =
                        ComputeCompositeScore(_acquisitionPriorities[priorityId]);
                }
            }, cancellationToken);
        }

        private async Task RecomputeAcquisitionPrioritiesFromGapsAsync(CancellationToken cancellationToken)
        {
            _acquisitionPriorities.Clear();

            await Task.Run(() =>
            {
                foreach (var gap in _identifiedGaps.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var priority = ComputePriorityForGap(gap);
                    _acquisitionPriorities[priority.PriorityId] = priority;
                }
            }, cancellationToken);
        }

        private KnowledgeAcquisitionPriority ComputePriorityForGap(GapRecord gap)
        {
            float impactScore = gap.Impact;
            float urgencyScore = gap.Severity switch
            {
                GapSeverity.Critical => 1.0f,
                GapSeverity.High => 0.75f,
                GapSeverity.Medium => 0.5f,
                GapSeverity.Low => 0.25f,
                _ => 0.1f
            };

            // Frequency: how often this gap causes issues
            float frequencyScore = 0f;
            if (gap.GapType == GapType.InferenceFailure && gap.Metadata != null &&
                gap.Metadata.TryGetValue("TotalFailures", out var failObj))
            {
                int failures = Convert.ToInt32(failObj);
                frequencyScore = Math.Min(1.0f, failures / 20.0f);
            }
            else if (gap.GapType == GapType.UserQuestion && gap.Metadata != null &&
                     gap.Metadata.TryGetValue("TotalAsks", out var asksObj))
            {
                int asks = Convert.ToInt32(asksObj);
                frequencyScore = Math.Min(1.0f, asks / 15.0f);
            }
            else
            {
                frequencyScore = impactScore * 0.5f;
            }

            var priority = new KnowledgeAcquisitionPriority
            {
                PriorityId = $"PRI_{gap.GapId}",
                Domain = gap.Domain,
                Description = gap.Description,
                ImpactScore = impactScore,
                UrgencyScore = urgencyScore,
                FrequencyScore = frequencyScore,
                SuggestedAction = gap.SuggestedAction,
                GapType = gap.GapType,
                RelatedGapIds = new List<string> { gap.GapId }
            };

            priority.CompositeScore = ComputeCompositeScore(priority);
            return priority;
        }

        private float ComputeCompositeScore(KnowledgeAcquisitionPriority priority)
        {
            return priority.ImpactScore * _configuration.ImpactWeight +
                   priority.UrgencyScore * _configuration.UrgencyWeight +
                   priority.FrequencyScore * _configuration.FrequencyWeight;
        }

        #endregion

        #region Helper Methods

        private void InitializeDomainCoverage()
        {
            foreach (var domain in KnownDomains)
            {
                _domainCoverage[domain] = new DomainCoverage
                {
                    Domain = domain,
                    NodeCount = 0,
                    FactCount = 0,
                    AverageConfidence = 0f,
                    DepthCoverage = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                    LastUpdated = DateTime.MinValue,
                    StalenessRatio = 1.0f
                };

                foreach (var depth in DepthLevels)
                {
                    _domainCoverage[domain].DepthCoverage[depth] = 0f;
                }
            }
        }

        private void UpdateDomainCoverageInternal(string domain, int nodeCount, int factCount,
            KnowledgeGraphSnapshot graph, SemanticMemorySnapshot memory)
        {
            if (_domainCoverage.TryGetValue(domain, out var coverage))
            {
                coverage.NodeCount = nodeCount;
                coverage.FactCount = factCount;
                coverage.LastUpdated = DateTime.UtcNow;

                // Compute depth-level coverage
                foreach (var depth in DepthLevels)
                {
                    coverage.DepthCoverage[depth] = CalculateDepthScore(domain, depth, graph, memory);
                }

                // Compute average confidence
                float avgConf = 0f;
                int confCount = 0;
                foreach (var depth in DepthLevels)
                {
                    float depthConf = memory?.GetAverageConfidenceByCategoryAndDepth(domain, depth) ?? 0f;
                    if (depthConf > 0)
                    {
                        avgConf += depthConf;
                        confCount++;
                    }
                }
                coverage.AverageConfidence = confCount > 0 ? avgConf / confCount : 0f;
            }
        }

        private List<GapRecord> ConsolidateGaps(KnowledgeGapReport report)
        {
            var all = new List<GapRecord>();
            all.AddRange(report.ConnectivityGaps ?? new List<GapRecord>());
            all.AddRange(report.DomainCoverageGaps ?? new List<GapRecord>());
            all.AddRange(report.ConfidenceGaps ?? new List<GapRecord>());
            all.AddRange(report.TemporalGaps ?? new List<GapRecord>());
            all.AddRange(report.InferenceFailureGaps ?? new List<GapRecord>());
            all.AddRange(report.UserQuestionGaps ?? new List<GapRecord>());

            // Remove duplicates by GapId
            return all
                .GroupBy(g => g.GapId)
                .Select(g => g.OrderByDescending(x => x.Impact).First())
                .OrderByDescending(g => g.Impact)
                .ToList();
        }

        private List<KnowledgeCluster> FindDisconnectedClusters(KnowledgeGraphSnapshot graph)
        {
            var clusters = new List<KnowledgeCluster>();
            if (graph == null) return clusters;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int clusterId = 0;

            foreach (var node in graph.Nodes)
            {
                if (visited.Contains(node.Id)) continue;

                var clusterNodes = new List<KnowledgeGraphNodeInfo>();
                var queue = new Queue<string>();
                queue.Enqueue(node.Id);

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();
                    if (visited.Contains(currentId)) continue;
                    visited.Add(currentId);

                    var currentNode = graph.GetNode(currentId);
                    if (currentNode != null)
                        clusterNodes.Add(currentNode);

                    foreach (var neighborId in graph.GetNeighborIds(currentId))
                    {
                        if (!visited.Contains(neighborId))
                            queue.Enqueue(neighborId);
                    }
                }

                if (clusterNodes.Any())
                {
                    var primaryDomain = clusterNodes
                        .GroupBy(n => n.NodeType)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    clusters.Add(new KnowledgeCluster
                    {
                        ClusterId = $"C{clusterId++}",
                        NodeCount = clusterNodes.Count,
                        PrimaryDomain = primaryDomain ?? "Unknown",
                        NodeIds = clusterNodes.Select(n => n.Id).ToList()
                    });
                }
            }

            return clusters;
        }

        private List<DataFileSuggestion> MapDomainToDataFiles(string domain)
        {
            var suggestions = new Dictionary<string, List<DataFileSuggestion>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Structural"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "STRUCTURAL_DESIGN_CRITERIA.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "BuildingType", "LoadType", "DesignValue", "Code", "Section", "Notes" }
                    }
                },
                ["Fire"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "FIRE_RATING_REQUIREMENTS.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "AssemblyType", "OccupancyType", "RequiredRating", "Code", "Section" }
                    }
                },
                ["Accessibility"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "ACCESSIBILITY_CLEARANCES.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "SpaceType", "Requirement", "MinDimension", "Code", "Section" }
                    }
                },
                ["HVAC"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "HVAC_DESIGN_CRITERIA.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "SystemType", "SpaceType", "DesignParameter", "Value", "Unit", "Standard" }
                    }
                },
                ["Electrical"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "ELECTRICAL_LOAD_REQUIREMENTS.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "BuildingType", "LoadCategory", "DemandFactor", "Code", "Section" }
                    }
                },
                ["Cost"] = new List<DataFileSuggestion>
                {
                    new DataFileSuggestion
                    {
                        FileName = "COST_BENCHMARKS_BY_TYPE.csv",
                        Format = "CSV",
                        SuggestedColumns = new List<string>
                            { "BuildingType", "Region", "CostPerSqM", "Currency", "Year", "Source" }
                    }
                }
            };

            if (suggestions.TryGetValue(domain, out var domainSuggestions))
                return domainSuggestions;

            return new List<DataFileSuggestion>
            {
                new DataFileSuggestion
                {
                    FileName = $"{domain.ToUpperInvariant()}_KNOWLEDGE_BASE.csv",
                    Format = "CSV",
                    SuggestedColumns = new List<string>
                        { "Topic", "Subtopic", "Fact", "Value", "Unit", "Source", "Confidence" }
                }
            };
        }

        private string ExtractDomainFromContext(Dictionary<string, object> context)
        {
            if (context == null) return "Unknown";

            if (context.TryGetValue("Domain", out var domain))
                return domain?.ToString() ?? "Unknown";
            if (context.TryGetValue("Category", out var category))
                return category?.ToString() ?? "Unknown";
            if (context.TryGetValue("Discipline", out var discipline))
                return discipline?.ToString() ?? "Unknown";

            return "Unknown";
        }

        private string ComputeQuestionHash(string question)
        {
            // Simple hash for deduplication
            var normalized = question.Trim().ToLowerInvariant();
            return normalized.GetHashCode().ToString("X8");
        }

        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private void PruneLowConfidenceDecisions()
        {
            if (_lowConfidenceDecisions.Count > _configuration.MaxLowConfidenceDecisions)
            {
                var oldest = _lowConfidenceDecisions.Values
                    .OrderBy(d => d.Timestamp)
                    .Take(_lowConfidenceDecisions.Count - _configuration.MaxLowConfidenceDecisions)
                    .ToList();

                foreach (var decision in oldest)
                {
                    _lowConfidenceDecisions.TryRemove(decision.DecisionId, out _);
                }
            }
        }

        private void EnqueuePeriodicAnalysis()
        {
            _eventQueue.Enqueue(new GapAnalysisEvent
            {
                EventType = GapEventType.PeriodicReanalysis,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configuration for the KnowledgeGapAnalyzer.
    /// </summary>
    public class KnowledgeGapConfiguration
    {
        public float LowConfidenceThreshold { get; set; } = 0.7f;
        public float LowConfidenceRatioThreshold { get; set; } = 0.4f;
        public float CoverageGapThreshold { get; set; } = 0.3f;
        public int MinNodesPerDomain { get; set; } = 20;
        public int MinItemsPerDepthLevel { get; set; } = 5;
        public int MinEdgesForConnectivity { get; set; } = 3;
        public int MinClusterSize { get; set; } = 5;
        public int StalenessThresholdDays { get; set; } = 90;
        public float StalenessRatioThreshold { get; set; } = 0.5f;
        public int MinInferenceFailuresForGap { get; set; } = 5;
        public int MinUnansweredQuestionsForGap { get; set; } = 3;
        public int MaxLowConfidenceDecisions { get; set; } = 500;
        public int PeriodicAnalysisIntervalMinutes { get; set; } = 60;
        public float ImpactWeight { get; set; } = 0.4f;
        public float UrgencyWeight { get; set; } = 0.35f;
        public float FrequencyWeight { get; set; } = 0.25f;
    }

    #endregion

    #region Report Types

    /// <summary>
    /// Full knowledge gap analysis report.
    /// </summary>
    public class KnowledgeGapReport
    {
        public string AnalysisId { get; set; }
        public DateTime AnalysisTimestamp { get; set; }

        public List<GapRecord> ConnectivityGaps { get; set; } = new List<GapRecord>();
        public List<GapRecord> DomainCoverageGaps { get; set; } = new List<GapRecord>();
        public List<GapRecord> ConfidenceGaps { get; set; } = new List<GapRecord>();
        public List<GapRecord> TemporalGaps { get; set; } = new List<GapRecord>();
        public List<GapRecord> InferenceFailureGaps { get; set; } = new List<GapRecord>();
        public List<GapRecord> UserQuestionGaps { get; set; } = new List<GapRecord>();

        public List<GapRecord> AllGaps { get; set; } = new List<GapRecord>();
        public int TotalGapCount { get; set; }
        public int CriticalGapCount { get; set; }
        public float OverallCoverageScore { get; set; }

        public CoverageMatrix CoverageMatrix { get; set; }
        public List<string> AnalysisErrors { get; set; } = new List<string>();
    }

    /// <summary>
    /// A single identified knowledge gap.
    /// </summary>
    public class GapRecord
    {
        public string GapId { get; set; }
        public GapType GapType { get; set; }
        public string Domain { get; set; }
        public string Description { get; set; }
        public GapSeverity Severity { get; set; }
        public float Impact { get; set; }
        public string AffectedNodeId { get; set; }
        public string AffectedNodeName { get; set; }
        public DateTime DetectedAt { get; set; }
        public string SuggestedAction { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public enum GapType
    {
        Connectivity,
        DomainCoverage,
        Confidence,
        Temporal,
        InferenceFailure,
        UserQuestion
    }

    public enum GapSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Coverage Types

    /// <summary>
    /// Coverage report for domain-by-depth analysis.
    /// </summary>
    public class CoverageReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<string> Domains { get; set; }
        public List<string> DepthLevels { get; set; }
        public CoverageMatrix Matrix { get; set; }
        public Dictionary<string, DomainCoverageSummary> DomainSummaries { get; set; }
        public List<string> WeakestDomains { get; set; }
        public List<string> StrongestDomains { get; set; }
        public float OverallCoverage { get; set; }
    }

    public class CoverageMatrix
    {
        public List<string> DomainLabels { get; set; }
        public List<string> DepthLabels { get; set; }
        public CoverageCell[,] Cells { get; set; }
    }

    public class CoverageCell
    {
        public string Domain { get; set; }
        public string Depth { get; set; }
        public float CoverageScore { get; set; }
        public int NodeCount { get; set; }
        public int FactCount { get; set; }
        public float AverageConfidence { get; set; }
        public bool HasGap { get; set; }
    }

    public class DomainCoverageSummary
    {
        public string Domain { get; set; }
        public float OverallScore { get; set; }
        public Dictionary<string, float> DepthScores { get; set; }
        public int GapCount { get; set; }
        public int NodeCount { get; set; }
        public int FactCount { get; set; }
        public float AverageConfidence { get; set; }
        public float StalenessRatio { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class DomainCoverage
    {
        public string Domain { get; set; }
        public int NodeCount { get; set; }
        public int FactCount { get; set; }
        public float AverageConfidence { get; set; }
        public Dictionary<string, float> DepthCoverage { get; set; }
        public DateTime LastUpdated { get; set; }
        public float StalenessRatio { get; set; }
    }

    #endregion

    #region Heatmap and Visualization

    /// <summary>
    /// Heatmap data for coverage visualization.
    /// </summary>
    public class KnowledgeHeatmapData
    {
        public List<string> DomainLabels { get; set; }
        public List<string> DepthLabels { get; set; }
        public float[,] Values { get; set; }
        public float[,] ConfidenceValues { get; set; }
        public bool[,] GapFlags { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    #endregion

    #region Question Types

    /// <summary>
    /// A question generated to fill a knowledge gap.
    /// </summary>
    public class GeneratedQuestion
    {
        public string QuestionId { get; set; }
        public string Domain { get; set; }
        public string Question { get; set; }
        public string Purpose { get; set; }
        public QuestionPriority Priority { get; set; }
        public string ExpectedAnswerType { get; set; }
        public int TimesAsked { get; set; }
        public string SourceGapId { get; set; }
    }

    public enum QuestionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Priority Types

    /// <summary>
    /// A ranked knowledge acquisition priority.
    /// </summary>
    public class KnowledgeAcquisitionPriority
    {
        public string PriorityId { get; set; }
        public string Domain { get; set; }
        public string Description { get; set; }
        public float ImpactScore { get; set; }
        public float UrgencyScore { get; set; }
        public float FrequencyScore { get; set; }
        public float CompositeScore { get; set; }
        public string SuggestedAction { get; set; }
        public GapType GapType { get; set; }
        public List<string> RelatedGapIds { get; set; } = new List<string>();
    }

    #endregion

    #region Tracking Types

    public class LowConfidenceDecision
    {
        public string DecisionId { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public float ConfidenceDeficit { get; set; }
        public string Domain { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class InferenceFailureRecord
    {
        public string Topic { get; set; }
        public string Domain { get; set; }
        public List<string> FailureReasons { get; set; } = new List<string>();
        public int FailureCount { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class UserQuestionRecord
    {
        public string QuestionHash { get; set; }
        public string OriginalQuestion { get; set; }
        public string Domain { get; set; }
        public int AskCount { get; set; }
        public DateTime FirstAsked { get; set; }
        public DateTime LastAsked { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    #endregion

    #region Data File Recommendations

    public class DataFileRecommendation
    {
        public string Domain { get; set; }
        public string SuggestedFileName { get; set; }
        public string SuggestedFormat { get; set; }
        public List<string> SuggestedColumns { get; set; }
        public int GapCount { get; set; }
        public float EstimatedImpact { get; set; }
        public string Rationale { get; set; }
    }

    public class DataFileSuggestion
    {
        public string FileName { get; set; }
        public string Format { get; set; }
        public List<string> SuggestedColumns { get; set; }
    }

    #endregion

    #region Snapshot and Event Types

    /// <summary>
    /// Lightweight snapshot of a knowledge graph for gap analysis.
    /// </summary>
    public class KnowledgeGraphSnapshot
    {
        public List<KnowledgeGraphNodeInfo> Nodes { get; set; } = new List<KnowledgeGraphNodeInfo>();
        public List<KnowledgeGraphEdgeInfo> Edges { get; set; } = new List<KnowledgeGraphEdgeInfo>();

        private Dictionary<string, KnowledgeGraphNodeInfo> _nodeIndex;
        private Dictionary<string, List<string>> _adjacencyList;

        public void BuildIndex()
        {
            _nodeIndex = Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
            _adjacencyList = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in Edges)
            {
                if (!_adjacencyList.ContainsKey(edge.SourceId))
                    _adjacencyList[edge.SourceId] = new List<string>();
                _adjacencyList[edge.SourceId].Add(edge.TargetId);

                if (!_adjacencyList.ContainsKey(edge.TargetId))
                    _adjacencyList[edge.TargetId] = new List<string>();
                _adjacencyList[edge.TargetId].Add(edge.SourceId);
            }
        }

        public KnowledgeGraphNodeInfo GetNode(string id)
        {
            if (_nodeIndex == null) BuildIndex();
            _nodeIndex.TryGetValue(id, out var node);
            return node;
        }

        public int GetEdgeCountForNode(string nodeId)
        {
            if (_adjacencyList == null) BuildIndex();
            return _adjacencyList.TryGetValue(nodeId, out var neighbors) ? neighbors.Count : 0;
        }

        public IEnumerable<string> GetNeighborIds(string nodeId)
        {
            if (_adjacencyList == null) BuildIndex();
            return _adjacencyList.TryGetValue(nodeId, out var neighbors)
                ? neighbors : Enumerable.Empty<string>();
        }

        public int GetNodeCountByType(string type)
        {
            return Nodes.Count(n => string.Equals(n.NodeType, type, StringComparison.OrdinalIgnoreCase));
        }

        public int GetNodeCountByTypeAndDepth(string type, string depth)
        {
            return Nodes.Count(n =>
                string.Equals(n.NodeType, type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(n.DepthLevel, depth, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class KnowledgeGraphNodeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string DepthLevel { get; set; }
    }

    public class KnowledgeGraphEdgeInfo
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string RelationType { get; set; }
        public float Strength { get; set; }
    }

    /// <summary>
    /// Lightweight snapshot of semantic memory for gap analysis.
    /// </summary>
    public class SemanticMemorySnapshot
    {
        public List<SemanticFactInfo> Facts { get; set; } = new List<SemanticFactInfo>();

        public int GetFactCountByCategory(string category)
        {
            return Facts.Count(f => string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        public int GetFactCountByCategoryAndDepth(string category, string depth)
        {
            return Facts.Count(f =>
                string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(f.DepthLevel, depth, StringComparison.OrdinalIgnoreCase));
        }

        public float GetAverageConfidenceByCategoryAndDepth(string category, string depth)
        {
            var matching = Facts.Where(f =>
                string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(f.DepthLevel, depth, StringComparison.OrdinalIgnoreCase)).ToList();

            return matching.Any() ? matching.Average(f => f.Confidence) : 0f;
        }
    }

    public class SemanticFactInfo
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string DepthLevel { get; set; }
        public float Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class KnowledgeCluster
    {
        public string ClusterId { get; set; }
        public int NodeCount { get; set; }
        public string PrimaryDomain { get; set; }
        public List<string> NodeIds { get; set; } = new List<string>();
    }

    public class KnowledgeGapStatistics
    {
        public int TotalGapsIdentified { get; set; }
        public int CriticalGaps { get; set; }
        public int HighGaps { get; set; }
        public int MediumGaps { get; set; }
        public int LowGaps { get; set; }
        public int LowConfidenceDecisionCount { get; set; }
        public int InferenceFailureCount { get; set; }
        public int UnansweredQuestionCount { get; set; }
        public int DomainsWithGaps { get; set; }
        public DateTime LastFullAnalysis { get; set; }
        public float AverageGapImpact { get; set; }
    }

    public class GapAnalysisEvent
    {
        public GapEventType EventType { get; set; }
        public string Domain { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum GapEventType
    {
        LowConfidenceDecision,
        InferenceFailure,
        UnansweredQuestion,
        PeriodicReanalysis
    }

    #endregion
}
