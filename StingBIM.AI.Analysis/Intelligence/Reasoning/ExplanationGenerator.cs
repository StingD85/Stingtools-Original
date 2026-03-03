// StingBIM.AI.Intelligence.Reasoning.ExplanationGenerator
// Generates human-readable explanations for all AI recommendations and decisions
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Amplification (Deepen Reasoning)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Explanation Generator Engine

    /// <summary>
    /// Generates human-readable explanations for all AI recommendations.
    /// Traces reasoning path from input through knowledge lookup, inference, and recommendation.
    /// Supports multiple explanation depths and NLP-friendly output format.
    /// </summary>
    public class ExplanationGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly ConcurrentDictionary<string, RecommendationTrace> _recommendationTraces;
        private readonly ConcurrentDictionary<string, DecisionRecord> _decisionRecords;
        private readonly ConcurrentDictionary<string, ActionAuditTrail> _auditTrails;
        private readonly ConcurrentDictionary<string, EvidenceSource> _evidenceSources;
        private readonly List<ExplanationTemplate> _explanationTemplates;
        private readonly ExplanationConfiguration _configuration;

        private int _totalExplanationsGenerated;
        private int _totalContradictionsFlagged;

        /// <summary>
        /// Initializes the explanation generator with default configuration.
        /// </summary>
        public ExplanationGenerator()
            : this(new ExplanationConfiguration())
        {
        }

        /// <summary>
        /// Initializes the explanation generator with custom configuration.
        /// </summary>
        public ExplanationGenerator(ExplanationConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _recommendationTraces = new ConcurrentDictionary<string, RecommendationTrace>(StringComparer.OrdinalIgnoreCase);
            _decisionRecords = new ConcurrentDictionary<string, DecisionRecord>(StringComparer.OrdinalIgnoreCase);
            _auditTrails = new ConcurrentDictionary<string, ActionAuditTrail>(StringComparer.OrdinalIgnoreCase);
            _evidenceSources = new ConcurrentDictionary<string, EvidenceSource>(StringComparer.OrdinalIgnoreCase);
            _explanationTemplates = new List<ExplanationTemplate>();

            _totalExplanationsGenerated = 0;
            _totalContradictionsFlagged = 0;

            InitializeExplanationTemplates();

            Logger.Info("ExplanationGenerator initialized with {0} templates", _explanationTemplates.Count);
        }

        #region Public Methods - Recording

        /// <summary>
        /// Records a recommendation trace for later explanation.
        /// </summary>
        public void RecordRecommendation(RecommendationTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (string.IsNullOrWhiteSpace(trace.RecommendationId))
            {
                trace.RecommendationId = $"REC_{Guid.NewGuid():N}".Substring(0, 24);
            }

            trace.RecordedAt = DateTime.UtcNow;
            _recommendationTraces.AddOrUpdate(trace.RecommendationId, trace, (_, __) => trace);

            Logger.Debug("Recorded recommendation trace: {0} ({1})", trace.RecommendationId, trace.RecommendationType);
        }

        /// <summary>
        /// Records a decision for later explanation.
        /// </summary>
        public void RecordDecision(DecisionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.DecisionId))
            {
                record.DecisionId = $"DEC_{Guid.NewGuid():N}".Substring(0, 24);
            }

            record.RecordedAt = DateTime.UtcNow;
            _decisionRecords.AddOrUpdate(record.DecisionId, record, (_, __) => record);

            Logger.Debug("Recorded decision: {0} ({1})", record.DecisionId, record.DecisionType);
        }

        /// <summary>
        /// Records an action in the audit trail.
        /// </summary>
        public void RecordAction(string actionId, string actionType, string description,
            Dictionary<string, object> context = null)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return;

            var trail = _auditTrails.GetOrAdd(actionId, _ => new ActionAuditTrail
            {
                ActionId = actionId,
                ActionType = actionType,
                Description = description,
                StartedAt = DateTime.UtcNow,
                Steps = new List<AuditStep>()
            });

            trail.Context = context ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a step to an action's audit trail.
        /// </summary>
        public void AddAuditStep(string actionId, string stepName, string description,
            Dictionary<string, object> data = null)
        {
            if (_auditTrails.TryGetValue(actionId, out var trail))
            {
                lock (_lockObject)
                {
                    trail.Steps.Add(new AuditStep
                    {
                        StepNumber = trail.Steps.Count + 1,
                        StepName = stepName,
                        Description = description,
                        Timestamp = DateTime.UtcNow,
                        Data = data ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    });
                }
            }
        }

        /// <summary>
        /// Registers an evidence source (CSV data, building code, learned pattern, user preference).
        /// </summary>
        public void RegisterEvidenceSource(EvidenceSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.SourceId)) return;
            _evidenceSources.AddOrUpdate(source.SourceId, source, (_, __) => source);
        }

        #endregion

        #region Public Methods - Explanation Generation

        /// <summary>
        /// Generates a full explanation for a recommendation at the specified depth.
        /// </summary>
        public async Task<Explanation> ExplainRecommendationAsync(
            string recommendationId,
            ExplanationDepth depth = ExplanationDepth.Standard,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (string.IsNullOrWhiteSpace(recommendationId))
                throw new ArgumentNullException(nameof(recommendationId));

            progress?.Report($"Generating {depth} explanation for recommendation {recommendationId}...");

            if (!_recommendationTraces.TryGetValue(recommendationId, out var trace))
            {
                Logger.Warn("Recommendation trace not found: {0}", recommendationId);
                return new Explanation
                {
                    TargetId = recommendationId,
                    Type = ExplanationType.WhyRecommended,
                    Depth = depth,
                    Summary = $"No explanation trace available for recommendation {recommendationId}",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var explanation = new Explanation
            {
                TargetId = recommendationId,
                Type = ExplanationType.WhyRecommended,
                Depth = depth,
                GeneratedAt = DateTime.UtcNow,
                EvidenceChain = new List<EvidenceLink>(),
                Contradictions = new List<Contradiction>()
            };

            try
            {
                // Generate explanation at requested depth
                switch (depth)
                {
                    case ExplanationDepth.Brief:
                        explanation.Summary = GenerateBriefExplanation(trace);
                        break;

                    case ExplanationDepth.Standard:
                        explanation.Summary = GenerateStandardExplanation(trace);
                        explanation.DetailedReasoning = GenerateReasoningParagraph(trace);
                        explanation.EvidenceChain = BuildEvidenceChain(trace);
                        break;

                    case ExplanationDepth.Detailed:
                        explanation.Summary = GenerateStandardExplanation(trace);
                        explanation.DetailedReasoning = GenerateDetailedExplanation(trace);
                        explanation.EvidenceChain = BuildEvidenceChain(trace);
                        explanation.ReasoningSteps = BuildReasoningSteps(trace);
                        explanation.ConfidenceBreakdown = BuildConfidenceBreakdown(trace);
                        explanation.AlternativesConsidered = BuildAlternativesList(trace);
                        break;
                }

                // Check for contradictions
                cancellationToken.ThrowIfCancellationRequested();
                var contradictions = DetectContradictions(trace);
                explanation.Contradictions = contradictions;
                if (contradictions.Any())
                {
                    explanation.HasContradictions = true;
                    _totalContradictionsFlagged += contradictions.Count;
                }

                // Add confidence report
                explanation.OverallConfidence = trace.OverallConfidence;
                explanation.ConfidenceFactors = GenerateConfidenceFactors(trace);

                // Generate NLP-friendly output
                explanation.NlpFriendlyText = FormatForNlp(explanation);
                explanation.VoiceFriendlyText = FormatForVoice(explanation);

                _totalExplanationsGenerated++;

                Logger.Debug("Generated {0} explanation for {1}: {2}",
                    depth, recommendationId, explanation.Summary?.Substring(0, Math.Min(100, explanation.Summary?.Length ?? 0)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating explanation for {0}", recommendationId);
                explanation.Summary = $"Error generating explanation: {ex.Message}";
            }

            return explanation;
        }

        /// <summary>
        /// Synchronous wrapper for ExplainRecommendation.
        /// </summary>
        public Explanation ExplainRecommendation(string recommendationId,
            ExplanationDepth depth = ExplanationDepth.Standard)
        {
            return ExplainRecommendationAsync(recommendationId, depth).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Explains why a specific decision was made.
        /// </summary>
        public async Task<Explanation> ExplainDecisionAsync(
            string decisionId,
            ExplanationDepth depth = ExplanationDepth.Standard,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
                throw new ArgumentNullException(nameof(decisionId));

            progress?.Report($"Generating explanation for decision {decisionId}...");

            if (!_decisionRecords.TryGetValue(decisionId, out var record))
            {
                return new Explanation
                {
                    TargetId = decisionId,
                    Type = ExplanationType.HowDerived,
                    Depth = depth,
                    Summary = $"No decision record found for {decisionId}",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var explanation = new Explanation
            {
                TargetId = decisionId,
                Type = DetermineExplanationType(record),
                Depth = depth,
                GeneratedAt = DateTime.UtcNow,
                EvidenceChain = new List<EvidenceLink>(),
                Contradictions = new List<Contradiction>()
            };

            try
            {
                // Build explanation for the decision
                explanation.Summary = GenerateDecisionSummary(record);

                if (depth >= ExplanationDepth.Standard)
                {
                    explanation.DetailedReasoning = GenerateDecisionReasoning(record);
                    explanation.EvidenceChain = BuildDecisionEvidence(record);
                }

                if (depth == ExplanationDepth.Detailed)
                {
                    explanation.ReasoningSteps = BuildDecisionReasoningSteps(record);
                    explanation.ConfidenceBreakdown = BuildDecisionConfidenceBreakdown(record);
                    explanation.AlternativesConsidered = record.AlternativesConsidered;
                }

                // Generate what-if analysis for rejected alternatives
                if (record.RejectedAlternatives != null && record.RejectedAlternatives.Any())
                {
                    explanation.RejectionExplanations = record.RejectedAlternatives
                        .Select(alt => new RejectionExplanation
                        {
                            AlternativeId = alt.AlternativeId,
                            AlternativeName = alt.Name,
                            RejectionReason = alt.RejectionReason,
                            WhatIfDescription = alt.WhatIfConsequence
                        })
                        .ToList();
                }

                explanation.OverallConfidence = record.Confidence;
                explanation.NlpFriendlyText = FormatForNlp(explanation);
                explanation.VoiceFriendlyText = FormatForVoice(explanation);

                _totalExplanationsGenerated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating decision explanation for {0}", decisionId);
                explanation.Summary = $"Error generating explanation: {ex.Message}";
            }

            return explanation;
        }

        /// <summary>
        /// Synchronous wrapper for ExplainDecision.
        /// </summary>
        public Explanation ExplainDecision(string decisionId,
            ExplanationDepth depth = ExplanationDepth.Standard)
        {
            return ExplainDecisionAsync(decisionId, depth).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Generates a complete audit trail report for an action.
        /// </summary>
        public async Task<TraceReport> GenerateTraceReportAsync(
            string actionId,
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentNullException(nameof(actionId));

            progress?.Report($"Generating trace report for action {actionId}...");

            var report = new TraceReport
            {
                ActionId = actionId,
                GeneratedAt = DateTime.UtcNow,
                Sections = new List<TraceSection>()
            };

            if (!_auditTrails.TryGetValue(actionId, out var trail))
            {
                report.Summary = $"No audit trail found for action {actionId}";
                return report;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                report.ActionType = trail.ActionType;
                report.ActionDescription = trail.Description;
                report.StartTime = trail.StartedAt;
                report.EndTime = trail.CompletedAt ?? DateTime.UtcNow;

                // Section 1: Input summary
                report.Sections.Add(new TraceSection
                {
                    SectionName = "Input",
                    Description = "Initial inputs and parameters",
                    Content = FormatInputSection(trail),
                    Order = 1
                });

                cancellationToken.ThrowIfCancellationRequested();

                // Section 2: Knowledge lookups
                var knowledgeSteps = trail.Steps
                    .Where(s => s.StepName.Contains("Knowledge") || s.StepName.Contains("Lookup") ||
                               s.StepName.Contains("Query"))
                    .ToList();

                if (knowledgeSteps.Any())
                {
                    report.Sections.Add(new TraceSection
                    {
                        SectionName = "Knowledge Lookup",
                        Description = "Knowledge base queries and results",
                        Content = FormatKnowledgeSection(knowledgeSteps),
                        Order = 2
                    });
                }

                // Section 3: Inference steps
                var inferenceSteps = trail.Steps
                    .Where(s => s.StepName.Contains("Inference") || s.StepName.Contains("Rule") ||
                               s.StepName.Contains("Reasoning"))
                    .ToList();

                if (inferenceSteps.Any())
                {
                    report.Sections.Add(new TraceSection
                    {
                        SectionName = "Inference",
                        Description = "Rules applied and conclusions drawn",
                        Content = FormatInferenceSection(inferenceSteps),
                        Order = 3
                    });
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Section 4: Decision/output
                var decisionSteps = trail.Steps
                    .Where(s => s.StepName.Contains("Decision") || s.StepName.Contains("Output") ||
                               s.StepName.Contains("Result"))
                    .ToList();

                if (decisionSteps.Any())
                {
                    report.Sections.Add(new TraceSection
                    {
                        SectionName = "Decision",
                        Description = "Final decision and rationale",
                        Content = FormatDecisionSection(decisionSteps),
                        Order = 4
                    });
                }

                // Section 5: Complete step-by-step trace
                report.Sections.Add(new TraceSection
                {
                    SectionName = "Complete Trace",
                    Description = "Full step-by-step execution log",
                    Content = FormatCompleteTrace(trail),
                    Order = 5
                });

                report.TotalSteps = trail.Steps.Count;
                report.Summary = $"Action '{trail.Description}' completed in {trail.Steps.Count} steps " +
                    $"from {trail.StartedAt:HH:mm:ss} to {trail.CompletedAt?.ToString("HH:mm:ss") ?? "ongoing"}";

                _totalExplanationsGenerated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating trace report for {0}", actionId);
                report.Summary = $"Error generating report: {ex.Message}";
            }

            return report;
        }

        /// <summary>
        /// Synchronous wrapper for GenerateTraceReport.
        /// </summary>
        public TraceReport GenerateTraceReport(string actionId)
        {
            return GenerateTraceReportAsync(actionId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Generates a code-justification explanation referencing specific building code sections.
        /// </summary>
        public Explanation GenerateCodeJustification(string requirementId, string codeReference,
            string section, string requirement)
        {
            var explanation = new Explanation
            {
                TargetId = requirementId,
                Type = ExplanationType.CodeJustification,
                Depth = ExplanationDepth.Standard,
                GeneratedAt = DateTime.UtcNow,
                EvidenceChain = new List<EvidenceLink>()
            };

            explanation.Summary = $"Building code {codeReference} section {section} requires: {requirement}";
            explanation.DetailedReasoning = $"This requirement is mandated by {codeReference}, " +
                $"specifically in section {section}. The code states: \"{requirement}\". " +
                $"Compliance with this provision is mandatory for the applicable jurisdiction.";

            explanation.EvidenceChain.Add(new EvidenceLink
            {
                SourceType = EvidenceSourceType.BuildingCode,
                SourceName = codeReference,
                SourceReference = section,
                Claim = requirement,
                Confidence = 1.0f,
                IsAuthoritative = true
            });

            explanation.OverallConfidence = 1.0f;
            explanation.NlpFriendlyText = explanation.Summary;
            explanation.VoiceFriendlyText = $"According to {codeReference}, section {section}, {requirement}";

            return explanation;
        }

        /// <summary>
        /// Generates a precedent-based explanation from similar past projects.
        /// </summary>
        public Explanation GeneratePrecedentExplanation(string recommendationId,
            List<ProjectPrecedent> precedents)
        {
            if (precedents == null || !precedents.Any())
            {
                return new Explanation
                {
                    TargetId = recommendationId,
                    Type = ExplanationType.PrecedentBased,
                    Depth = ExplanationDepth.Standard,
                    Summary = "No project precedents available for this recommendation.",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            var explanation = new Explanation
            {
                TargetId = recommendationId,
                Type = ExplanationType.PrecedentBased,
                Depth = ExplanationDepth.Standard,
                GeneratedAt = DateTime.UtcNow,
                EvidenceChain = new List<EvidenceLink>()
            };

            var precedentCount = precedents.Count;
            var avgSuccess = precedents.Average(p => p.SuccessRate);

            explanation.Summary = $"In {precedentCount} similar project{(precedentCount > 1 ? "s" : "")}, " +
                $"this approach was used with an average success rate of {avgSuccess:P0}.";

            var sb = new StringBuilder();
            sb.AppendLine($"This recommendation is supported by {precedentCount} project precedent{(precedentCount > 1 ? "s" : "")}:");
            sb.AppendLine();

            foreach (var precedent in precedents.OrderByDescending(p => p.Similarity))
            {
                sb.AppendLine($"  - {precedent.ProjectName} ({precedent.BuildingType}, {precedent.Year}):");
                sb.AppendLine($"    Similarity: {precedent.Similarity:P0}, Success: {precedent.SuccessRate:P0}");
                if (!string.IsNullOrEmpty(precedent.Outcome))
                {
                    sb.AppendLine($"    Outcome: {precedent.Outcome}");
                }
                sb.AppendLine();

                explanation.EvidenceChain.Add(new EvidenceLink
                {
                    SourceType = EvidenceSourceType.ProjectPrecedent,
                    SourceName = precedent.ProjectName,
                    SourceReference = $"{precedent.BuildingType} ({precedent.Year})",
                    Claim = precedent.Outcome ?? "Applied same approach",
                    Confidence = precedent.SuccessRate,
                    IsAuthoritative = false
                });
            }

            explanation.DetailedReasoning = sb.ToString();
            explanation.OverallConfidence = Math.Min(0.95f, avgSuccess * (1f + precedentCount * 0.05f));
            explanation.NlpFriendlyText = explanation.Summary;
            explanation.VoiceFriendlyText = explanation.Summary;

            return explanation;
        }

        /// <summary>
        /// Gets the total number of explanations generated.
        /// </summary>
        public int TotalExplanationsGenerated => _totalExplanationsGenerated;

        /// <summary>
        /// Gets the total number of contradictions flagged.
        /// </summary>
        public int TotalContradictionsFlagged => _totalContradictionsFlagged;

        #endregion

        #region Private Methods - Brief Explanation

        private string GenerateBriefExplanation(RecommendationTrace trace)
        {
            // Single sentence explanation
            var template = FindBestTemplate(trace.RecommendationType);
            if (template != null)
            {
                return FormatTemplate(template.BriefFormat, trace);
            }

            return $"{trace.RecommendationDescription} was recommended based on " +
                $"{trace.InputFactors?.Count ?? 0} input factors with {trace.OverallConfidence:P0} confidence.";
        }

        #endregion

        #region Private Methods - Standard Explanation

        private string GenerateStandardExplanation(RecommendationTrace trace)
        {
            var template = FindBestTemplate(trace.RecommendationType);
            if (template != null)
            {
                return FormatTemplate(template.StandardFormat, trace);
            }

            var sb = new StringBuilder();
            sb.Append($"{trace.RecommendationDescription} was recommended because ");

            if (trace.PrimaryReasons != null && trace.PrimaryReasons.Any())
            {
                sb.Append(string.Join(", and ", trace.PrimaryReasons.Take(3)));
                sb.Append(".");
            }
            else
            {
                sb.Append($"it best satisfies the identified requirements with {trace.OverallConfidence:P0} confidence.");
            }

            return sb.ToString();
        }

        private string GenerateReasoningParagraph(RecommendationTrace trace)
        {
            var sb = new StringBuilder();

            // Describe input analysis
            if (trace.InputFactors != null && trace.InputFactors.Any())
            {
                sb.AppendLine("Input Analysis:");
                foreach (var factor in trace.InputFactors.Take(5))
                {
                    sb.AppendLine($"  - {factor.Key}: {factor.Value}");
                }
                sb.AppendLine();
            }

            // Describe knowledge consulted
            if (trace.KnowledgeSources != null && trace.KnowledgeSources.Any())
            {
                sb.AppendLine("Knowledge Sources Consulted:");
                foreach (var source in trace.KnowledgeSources)
                {
                    sb.AppendLine($"  - {source.SourceName} ({source.SourceType}): {source.FindingSummary}");
                }
                sb.AppendLine();
            }

            // Describe inference
            if (trace.InferencesApplied != null && trace.InferencesApplied.Any())
            {
                sb.AppendLine("Inferences Applied:");
                foreach (var inference in trace.InferencesApplied)
                {
                    sb.AppendLine($"  - {inference.RuleName}: {inference.Conclusion} (confidence: {inference.Confidence:P0})");
                }
                sb.AppendLine();
            }

            // Conclusion
            sb.AppendLine($"Conclusion: {trace.RecommendationDescription}");
            sb.AppendLine($"Overall Confidence: {trace.OverallConfidence:P0}");

            return sb.ToString();
        }

        #endregion

        #region Private Methods - Detailed Explanation

        private string GenerateDetailedExplanation(RecommendationTrace trace)
        {
            var sb = new StringBuilder();

            // Full reasoning paragraph
            sb.AppendLine(GenerateReasoningParagraph(trace));
            sb.AppendLine();

            // Confidence analysis
            sb.AppendLine("Confidence Analysis:");
            sb.AppendLine($"  Overall: {trace.OverallConfidence:P0}");

            if (trace.ConfidenceContributors != null)
            {
                foreach (var contributor in trace.ConfidenceContributors)
                {
                    sb.AppendLine($"  - {contributor.Factor}: {contributor.Contribution:P0} " +
                        $"({contributor.Direction})");
                }
            }
            sb.AppendLine();

            // What would increase confidence
            sb.AppendLine("To increase confidence:");
            if (trace.OverallConfidence < 0.9f)
            {
                if (trace.MissingInformation != null && trace.MissingInformation.Any())
                {
                    foreach (var missing in trace.MissingInformation)
                    {
                        sb.AppendLine($"  - Provide: {missing}");
                    }
                }
                else
                {
                    sb.AppendLine("  - Additional project data or building code confirmations would help");
                }
            }
            else
            {
                sb.AppendLine("  - Confidence is already high. No additional information needed.");
            }
            sb.AppendLine();

            // Alternatives considered
            if (trace.AlternativesConsidered != null && trace.AlternativesConsidered.Any())
            {
                sb.AppendLine("Alternatives Considered:");
                foreach (var alt in trace.AlternativesConsidered)
                {
                    sb.AppendLine($"  - {alt.Name}: Score {alt.Score:F2}" +
                        (alt.RejectionReason != null ? $" (Rejected: {alt.RejectionReason})" : ""));
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Private Methods - Evidence Chain

        private List<EvidenceLink> BuildEvidenceChain(RecommendationTrace trace)
        {
            var chain = new List<EvidenceLink>();

            // Add knowledge source evidence
            if (trace.KnowledgeSources != null)
            {
                foreach (var source in trace.KnowledgeSources)
                {
                    chain.Add(new EvidenceLink
                    {
                        SourceType = ClassifySourceType(source.SourceType),
                        SourceName = source.SourceName,
                        SourceReference = source.Reference,
                        Claim = source.FindingSummary,
                        Confidence = source.Confidence,
                        IsAuthoritative = source.SourceType == "BuildingCode" || source.SourceType == "Standard"
                    });
                }
            }

            // Add inference evidence
            if (trace.InferencesApplied != null)
            {
                foreach (var inference in trace.InferencesApplied)
                {
                    chain.Add(new EvidenceLink
                    {
                        SourceType = EvidenceSourceType.InferenceRule,
                        SourceName = inference.RuleName,
                        SourceReference = inference.RuleId,
                        Claim = inference.Conclusion,
                        Confidence = inference.Confidence,
                        IsAuthoritative = false
                    });
                }
            }

            // Add learned pattern evidence
            if (trace.LearnedPatternEvidence != null)
            {
                foreach (var pattern in trace.LearnedPatternEvidence)
                {
                    chain.Add(new EvidenceLink
                    {
                        SourceType = EvidenceSourceType.LearnedPattern,
                        SourceName = pattern.PatternName,
                        SourceReference = $"Observed {pattern.Occurrences} times",
                        Claim = pattern.Description,
                        Confidence = pattern.Confidence,
                        IsAuthoritative = false
                    });
                }
            }

            return chain.OrderByDescending(e => e.Confidence).ToList();
        }

        private List<ReasoningStep> BuildReasoningSteps(RecommendationTrace trace)
        {
            var steps = new List<ReasoningStep>();
            int stepNum = 1;

            // Step 1: Input analysis
            steps.Add(new ReasoningStep
            {
                StepNumber = stepNum++,
                StepType = "Input Analysis",
                Description = $"Analyzed {trace.InputFactors?.Count ?? 0} input factors",
                Input = trace.InputFactors != null
                    ? string.Join("; ", trace.InputFactors.Select(f => $"{f.Key}={f.Value}").Take(5))
                    : "No inputs",
                Output = "Requirements identified",
                Confidence = 1.0f
            });

            // Step 2: Knowledge lookup
            if (trace.KnowledgeSources != null && trace.KnowledgeSources.Any())
            {
                steps.Add(new ReasoningStep
                {
                    StepNumber = stepNum++,
                    StepType = "Knowledge Lookup",
                    Description = $"Consulted {trace.KnowledgeSources.Count} knowledge sources",
                    Input = "Requirements from Step 1",
                    Output = string.Join("; ", trace.KnowledgeSources
                        .Select(s => s.FindingSummary).Take(3)),
                    Confidence = trace.KnowledgeSources.Average(s => s.Confidence)
                });
            }

            // Step 3: Inference
            if (trace.InferencesApplied != null && trace.InferencesApplied.Any())
            {
                steps.Add(new ReasoningStep
                {
                    StepNumber = stepNum++,
                    StepType = "Inference",
                    Description = $"Applied {trace.InferencesApplied.Count} inference rules",
                    Input = "Knowledge from Step 2",
                    Output = string.Join("; ", trace.InferencesApplied
                        .Select(i => i.Conclusion).Take(3)),
                    Confidence = trace.InferencesApplied.Average(i => i.Confidence)
                });
            }

            // Step 4: Recommendation
            steps.Add(new ReasoningStep
            {
                StepNumber = stepNum++,
                StepType = "Recommendation",
                Description = "Generated final recommendation",
                Input = "Inferences and evidence",
                Output = trace.RecommendationDescription,
                Confidence = trace.OverallConfidence
            });

            return steps;
        }

        private List<ConfidenceContributor> BuildConfidenceBreakdown(RecommendationTrace trace)
        {
            return trace.ConfidenceContributors ?? new List<ConfidenceContributor>();
        }

        private List<ConsideredAlternative> BuildAlternativesList(RecommendationTrace trace)
        {
            return trace.AlternativesConsidered ?? new List<ConsideredAlternative>();
        }

        #endregion

        #region Private Methods - Decision Explanation

        private string GenerateDecisionSummary(DecisionRecord record)
        {
            var sb = new StringBuilder();

            switch (record.DecisionType)
            {
                case "Selection":
                    sb.Append($"Selected '{record.ChosenOption}' because {record.PrimaryReason}.");
                    break;
                case "Rejection":
                    sb.Append($"Rejected '{record.RejectedOption}' because {record.PrimaryReason}.");
                    break;
                case "Calculation":
                    sb.Append($"Calculated value of {record.CalculatedValue} based on {record.PrimaryReason}.");
                    break;
                default:
                    sb.Append($"Decision: {record.Description}. Reason: {record.PrimaryReason}.");
                    break;
            }

            return sb.ToString();
        }

        private string GenerateDecisionReasoning(DecisionRecord record)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Decision Type: {record.DecisionType}");
            sb.AppendLine($"Context: {record.Description}");
            sb.AppendLine();

            if (record.Factors != null && record.Factors.Any())
            {
                sb.AppendLine("Decision Factors:");
                foreach (var factor in record.Factors)
                {
                    sb.AppendLine($"  - {factor.Name}: Weight {factor.Weight:F2}, Score {factor.Score:F2}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Primary Reason: {record.PrimaryReason}");
            sb.AppendLine($"Confidence: {record.Confidence:P0}");

            return sb.ToString();
        }

        private List<EvidenceLink> BuildDecisionEvidence(DecisionRecord record)
        {
            var evidence = new List<EvidenceLink>();

            if (record.SupportingEvidence != null)
            {
                foreach (var item in record.SupportingEvidence)
                {
                    evidence.Add(new EvidenceLink
                    {
                        SourceType = ClassifySourceType(item.SourceType),
                        SourceName = item.SourceName,
                        SourceReference = item.Reference,
                        Claim = item.Claim,
                        Confidence = item.Confidence,
                        IsAuthoritative = item.IsAuthoritative
                    });
                }
            }

            return evidence;
        }

        private List<ReasoningStep> BuildDecisionReasoningSteps(DecisionRecord record)
        {
            var steps = new List<ReasoningStep>();
            int stepNum = 1;

            steps.Add(new ReasoningStep
            {
                StepNumber = stepNum++,
                StepType = "Problem Definition",
                Description = record.Description,
                Input = "User request or system trigger",
                Output = "Decision required",
                Confidence = 1.0f
            });

            if (record.Factors != null)
            {
                steps.Add(new ReasoningStep
                {
                    StepNumber = stepNum++,
                    StepType = "Factor Analysis",
                    Description = $"Evaluated {record.Factors.Count} decision factors",
                    Input = "Decision context",
                    Output = string.Join("; ", record.Factors.Select(f => $"{f.Name}={f.Score:F2}")),
                    Confidence = record.Confidence
                });
            }

            steps.Add(new ReasoningStep
            {
                StepNumber = stepNum++,
                StepType = "Decision",
                Description = record.PrimaryReason,
                Input = "Factor scores and evidence",
                Output = record.ChosenOption ?? record.CalculatedValue ?? record.Description,
                Confidence = record.Confidence
            });

            return steps;
        }

        private List<ConfidenceContributor> BuildDecisionConfidenceBreakdown(DecisionRecord record)
        {
            var contributors = new List<ConfidenceContributor>();

            if (record.Factors != null)
            {
                foreach (var factor in record.Factors)
                {
                    contributors.Add(new ConfidenceContributor
                    {
                        Factor = factor.Name,
                        Contribution = factor.Score * factor.Weight,
                        Direction = factor.Score > 0.5f ? "Positive" : "Negative"
                    });
                }
            }

            return contributors;
        }

        #endregion

        #region Private Methods - Contradiction Detection

        private List<Contradiction> DetectContradictions(RecommendationTrace trace)
        {
            var contradictions = new List<Contradiction>();

            if (trace.KnowledgeSources == null || trace.KnowledgeSources.Count < 2)
                return contradictions;

            // Check for contradictions between knowledge sources
            for (int i = 0; i < trace.KnowledgeSources.Count; i++)
            {
                for (int j = i + 1; j < trace.KnowledgeSources.Count; j++)
                {
                    var sourceA = trace.KnowledgeSources[i];
                    var sourceB = trace.KnowledgeSources[j];

                    if (AreContradictory(sourceA, sourceB))
                    {
                        contradictions.Add(new Contradiction
                        {
                            SourceA = sourceA.SourceName,
                            SourceB = sourceB.SourceName,
                            ClaimA = sourceA.FindingSummary,
                            ClaimB = sourceB.FindingSummary,
                            Severity = DetermineContradictionSeverity(sourceA, sourceB),
                            Resolution = SuggestResolution(sourceA, sourceB)
                        });
                    }
                }
            }

            // Check inferences against knowledge
            if (trace.InferencesApplied != null)
            {
                foreach (var inference in trace.InferencesApplied)
                {
                    foreach (var source in trace.KnowledgeSources)
                    {
                        if (InferenceContradictsSource(inference, source))
                        {
                            contradictions.Add(new Contradiction
                            {
                                SourceA = $"Inference: {inference.RuleName}",
                                SourceB = source.SourceName,
                                ClaimA = inference.Conclusion,
                                ClaimB = source.FindingSummary,
                                Severity = ContradictionSeverity.Medium,
                                Resolution = "Review inference rule against source data"
                            });
                        }
                    }
                }
            }

            return contradictions;
        }

        private bool AreContradictory(KnowledgeSourceTrace sourceA, KnowledgeSourceTrace sourceB)
        {
            if (sourceA.FindingSummary == null || sourceB.FindingSummary == null)
                return false;

            // Simple contradiction detection: opposing keywords
            var oppositions = new[]
            {
                ("increase", "decrease"), ("higher", "lower"), ("more", "less"),
                ("required", "prohibited"), ("must", "must not"), ("shall", "shall not"),
                ("allow", "prohibit"), ("minimum", "maximum"), ("above", "below")
            };

            var summaryA = sourceA.FindingSummary.ToLowerInvariant();
            var summaryB = sourceB.FindingSummary.ToLowerInvariant();

            // Check if they address the same topic but with opposing conclusions
            foreach (var (posA, posB) in oppositions)
            {
                if ((summaryA.Contains(posA) && summaryB.Contains(posB)) ||
                    (summaryA.Contains(posB) && summaryB.Contains(posA)))
                {
                    // Both need to be about the same topic
                    var wordsA = summaryA.Split(' ').Where(w => w.Length > 4).ToHashSet();
                    var wordsB = summaryB.Split(' ').Where(w => w.Length > 4).ToHashSet();
                    var commonWords = wordsA.Intersect(wordsB).Count();

                    if (commonWords >= 2)
                        return true;
                }
            }

            return false;
        }

        private bool InferenceContradictsSource(InferenceTrace inference, KnowledgeSourceTrace source)
        {
            if (inference.Conclusion == null || source.FindingSummary == null)
                return false;

            var conclusionLower = inference.Conclusion.ToLowerInvariant();
            var findingLower = source.FindingSummary.ToLowerInvariant();

            // Check for direct negation patterns
            if (conclusionLower.Contains("not required") && findingLower.Contains("required"))
                return true;
            if (conclusionLower.Contains("exceeds") && findingLower.Contains("within limits"))
                return true;

            return false;
        }

        private ContradictionSeverity DetermineContradictionSeverity(
            KnowledgeSourceTrace sourceA, KnowledgeSourceTrace sourceB)
        {
            // Code vs code contradictions are high severity
            if (sourceA.SourceType == "BuildingCode" && sourceB.SourceType == "BuildingCode")
                return ContradictionSeverity.High;

            // Code vs preference is medium
            if (sourceA.SourceType == "BuildingCode" || sourceB.SourceType == "BuildingCode")
                return ContradictionSeverity.Medium;

            return ContradictionSeverity.Low;
        }

        private string SuggestResolution(KnowledgeSourceTrace sourceA, KnowledgeSourceTrace sourceB)
        {
            // Higher authority source should take precedence
            var authorityA = GetSourceAuthority(sourceA.SourceType);
            var authorityB = GetSourceAuthority(sourceB.SourceType);

            if (authorityA > authorityB)
                return $"Defer to {sourceA.SourceName} (higher authority source)";
            if (authorityB > authorityA)
                return $"Defer to {sourceB.SourceName} (higher authority source)";

            // Same authority: defer to higher confidence
            if (sourceA.Confidence > sourceB.Confidence + 0.1f)
                return $"Defer to {sourceA.SourceName} (higher confidence: {sourceA.Confidence:P0})";
            if (sourceB.Confidence > sourceA.Confidence + 0.1f)
                return $"Defer to {sourceB.SourceName} (higher confidence: {sourceB.Confidence:P0})";

            return "Manual review required - sources have equal authority and confidence";
        }

        private int GetSourceAuthority(string sourceType)
        {
            var hierarchy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["InternationalCode"] = 7,
                ["NationalCode"] = 6,
                ["BuildingCode"] = 6,
                ["LocalCode"] = 5,
                ["Standard"] = 4,
                ["IndustryStandard"] = 4,
                ["BestPractice"] = 3,
                ["CsvData"] = 2,
                ["UserPreference"] = 2,
                ["LearnedPattern"] = 1
            };

            return hierarchy.TryGetValue(sourceType, out var authority) ? authority : 0;
        }

        #endregion

        #region Private Methods - Formatting

        private ExplanationType DetermineExplanationType(DecisionRecord record)
        {
            switch (record.DecisionType)
            {
                case "Selection": return ExplanationType.WhyRecommended;
                case "Rejection": return ExplanationType.WhyRejected;
                case "Calculation": return ExplanationType.HowDerived;
                case "WhatIf": return ExplanationType.WhatIfChanged;
                default: return ExplanationType.HowDerived;
            }
        }

        private EvidenceSourceType ClassifySourceType(string sourceType)
        {
            var mapping = new Dictionary<string, EvidenceSourceType>(StringComparer.OrdinalIgnoreCase)
            {
                ["BuildingCode"] = EvidenceSourceType.BuildingCode,
                ["Standard"] = EvidenceSourceType.BuildingCode,
                ["CsvData"] = EvidenceSourceType.CsvData,
                ["LearnedPattern"] = EvidenceSourceType.LearnedPattern,
                ["UserPreference"] = EvidenceSourceType.UserPreference,
                ["InferenceRule"] = EvidenceSourceType.InferenceRule,
                ["ProjectPrecedent"] = EvidenceSourceType.ProjectPrecedent
            };

            return mapping.TryGetValue(sourceType, out var type) ? type : EvidenceSourceType.Other;
        }

        private List<string> GenerateConfidenceFactors(RecommendationTrace trace)
        {
            var factors = new List<string>();

            if (trace.OverallConfidence >= 0.9f)
                factors.Add("High confidence: Strong evidence from authoritative sources");
            else if (trace.OverallConfidence >= 0.7f)
                factors.Add("Good confidence: Adequate evidence, some assumptions made");
            else if (trace.OverallConfidence >= 0.5f)
                factors.Add("Moderate confidence: Limited evidence, consider alternatives");
            else
                factors.Add("Low confidence: Insufficient evidence, manual review recommended");

            if (trace.KnowledgeSources != null)
            {
                var codeCount = trace.KnowledgeSources.Count(s => s.SourceType == "BuildingCode");
                if (codeCount > 0)
                    factors.Add($"Supported by {codeCount} building code reference{(codeCount > 1 ? "s" : "")}");
            }

            if (trace.LearnedPatternEvidence != null && trace.LearnedPatternEvidence.Any())
                factors.Add($"Consistent with {trace.LearnedPatternEvidence.Count} observed pattern(s)");

            if (trace.MissingInformation != null && trace.MissingInformation.Any())
                factors.Add($"Note: {trace.MissingInformation.Count} information gap(s) may affect accuracy");

            return factors;
        }

        private string FormatForNlp(Explanation explanation)
        {
            var sb = new StringBuilder();
            sb.Append(explanation.Summary);

            if (explanation.HasContradictions)
            {
                sb.Append($" Note: {explanation.Contradictions.Count} conflicting requirement(s) detected.");
            }

            return sb.ToString();
        }

        private string FormatForVoice(Explanation explanation)
        {
            // Simplify for voice output: shorter sentences, no special characters
            var text = explanation.Summary ?? "";
            text = text.Replace("%", " percent");
            text = text.Replace(">=", " at least ");
            text = text.Replace("<=", " at most ");

            // Truncate long voice output
            if (text.Length > 200)
            {
                var cutoff = text.LastIndexOf('.', 200);
                if (cutoff > 50) text = text.Substring(0, cutoff + 1);
            }

            return text;
        }

        private string FormatTemplate(string format, RecommendationTrace trace)
        {
            if (string.IsNullOrEmpty(format)) return null;

            return format
                .Replace("{RecommendationDescription}", trace.RecommendationDescription ?? "This option")
                .Replace("{PrimaryReason}", trace.PrimaryReasons?.FirstOrDefault() ?? "the analysis")
                .Replace("{Confidence}", $"{trace.OverallConfidence:P0}")
                .Replace("{SourceCount}", $"{trace.KnowledgeSources?.Count ?? 0}")
                .Replace("{InferenceCount}", $"{trace.InferencesApplied?.Count ?? 0}");
        }

        private ExplanationTemplate FindBestTemplate(string recommendationType)
        {
            return _explanationTemplates
                .FirstOrDefault(t => string.Equals(t.RecommendationType, recommendationType,
                    StringComparison.OrdinalIgnoreCase));
        }

        private string FormatInputSection(ActionAuditTrail trail)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Action: {trail.ActionType}");
            sb.AppendLine($"Description: {trail.Description}");
            if (trail.Context != null)
            {
                foreach (var kvp in trail.Context)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            return sb.ToString();
        }

        private string FormatKnowledgeSection(List<AuditStep> steps)
        {
            var sb = new StringBuilder();
            foreach (var step in steps)
            {
                sb.AppendLine($"[{step.Timestamp:HH:mm:ss}] {step.StepName}: {step.Description}");
            }
            return sb.ToString();
        }

        private string FormatInferenceSection(List<AuditStep> steps)
        {
            var sb = new StringBuilder();
            foreach (var step in steps)
            {
                sb.AppendLine($"[{step.Timestamp:HH:mm:ss}] {step.StepName}: {step.Description}");
            }
            return sb.ToString();
        }

        private string FormatDecisionSection(List<AuditStep> steps)
        {
            var sb = new StringBuilder();
            foreach (var step in steps)
            {
                sb.AppendLine($"[{step.Timestamp:HH:mm:ss}] {step.StepName}: {step.Description}");
            }
            return sb.ToString();
        }

        private string FormatCompleteTrace(ActionAuditTrail trail)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Audit Trail: {trail.ActionId} ===");
            sb.AppendLine($"Started: {trail.StartedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var step in trail.Steps)
            {
                sb.AppendLine($"Step {step.StepNumber}: {step.StepName}");
                sb.AppendLine($"  Time: {step.Timestamp:HH:mm:ss}");
                sb.AppendLine($"  Description: {step.Description}");
                if (step.Data != null && step.Data.Any())
                {
                    foreach (var kvp in step.Data)
                    {
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                sb.AppendLine();
            }

            if (trail.CompletedAt.HasValue)
            {
                sb.AppendLine($"Completed: {trail.CompletedAt:yyyy-MM-dd HH:mm:ss}");
            }

            return sb.ToString();
        }

        #endregion

        #region Initialization

        private void InitializeExplanationTemplates()
        {
            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "WallType",
                BriefFormat = "{RecommendationDescription} was recommended because {PrimaryReason}.",
                StandardFormat = "{RecommendationDescription} was selected based on analysis of {SourceCount} sources " +
                    "including building codes and material data, with {Confidence} confidence. The primary reason: {PrimaryReason}."
            });

            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "MEPSizing",
                BriefFormat = "{RecommendationDescription} was calculated based on {PrimaryReason}.",
                StandardFormat = "The MEP sizing recommendation of {RecommendationDescription} was derived from " +
                    "{InferenceCount} engineering calculations referencing {SourceCount} standards, " +
                    "achieving {Confidence} confidence. Key factor: {PrimaryReason}."
            });

            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "MaterialSelection",
                BriefFormat = "{RecommendationDescription} was chosen because {PrimaryReason}.",
                StandardFormat = "{RecommendationDescription} was selected after evaluating alternatives " +
                    "against {SourceCount} criteria sources. Confidence: {Confidence}. " +
                    "Primary factor: {PrimaryReason}."
            });

            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "SpatialLayout",
                BriefFormat = "This layout was recommended because {PrimaryReason}.",
                StandardFormat = "The spatial arrangement was determined by analyzing adjacency requirements, " +
                    "circulation patterns, and {SourceCount} design criteria with {Confidence} confidence. {PrimaryReason}."
            });

            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "CodeCompliance",
                BriefFormat = "This is required by building code: {PrimaryReason}.",
                StandardFormat = "This requirement is mandated by applicable building codes. " +
                    "Based on {SourceCount} code references with {Confidence} confidence. {PrimaryReason}."
            });

            _explanationTemplates.Add(new ExplanationTemplate
            {
                RecommendationType = "EnergyOptimization",
                BriefFormat = "{RecommendationDescription} optimizes energy performance because {PrimaryReason}.",
                StandardFormat = "Energy optimization analysis using {SourceCount} data sources " +
                    "and {InferenceCount} thermal calculations yields {RecommendationDescription} " +
                    "with {Confidence} confidence. {PrimaryReason}."
            });

            Logger.Debug("Initialized {0} explanation templates", _explanationTemplates.Count);
        }

        #endregion
    }

    #endregion

    #region Explanation Types

    /// <summary>
    /// Depth of explanation detail.
    /// </summary>
    public enum ExplanationDepth
    {
        /// <summary>Single sentence explanation.</summary>
        Brief = 0,
        /// <summary>Paragraph-level explanation with evidence.</summary>
        Standard = 1,
        /// <summary>Full trace including alternatives, confidence breakdown, and reasoning steps.</summary>
        Detailed = 2
    }

    /// <summary>
    /// Type of explanation being generated.
    /// </summary>
    public enum ExplanationType
    {
        WhyRecommended,
        WhyRejected,
        WhatIfChanged,
        HowDerived,
        CodeJustification,
        PrecedentBased
    }

    /// <summary>
    /// Type of evidence source.
    /// </summary>
    public enum EvidenceSourceType
    {
        BuildingCode,
        CsvData,
        LearnedPattern,
        UserPreference,
        InferenceRule,
        ProjectPrecedent,
        Other
    }

    /// <summary>
    /// Severity of a detected contradiction.
    /// </summary>
    public enum ContradictionSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// A complete explanation for a recommendation or decision.
    /// </summary>
    public class Explanation
    {
        public string TargetId { get; set; }
        public ExplanationType Type { get; set; }
        public ExplanationDepth Depth { get; set; }
        public string Summary { get; set; }
        public string DetailedReasoning { get; set; }
        public List<EvidenceLink> EvidenceChain { get; set; }
        public List<ReasoningStep> ReasoningSteps { get; set; }
        public List<ConfidenceContributor> ConfidenceBreakdown { get; set; }
        public List<ConsideredAlternative> AlternativesConsidered { get; set; }
        public List<RejectionExplanation> RejectionExplanations { get; set; }
        public List<Contradiction> Contradictions { get; set; }
        public bool HasContradictions { get; set; }
        public float OverallConfidence { get; set; }
        public List<string> ConfidenceFactors { get; set; }
        public string NlpFriendlyText { get; set; }
        public string VoiceFriendlyText { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// A link in the evidence chain connecting a claim to its source.
    /// </summary>
    public class EvidenceLink
    {
        public EvidenceSourceType SourceType { get; set; }
        public string SourceName { get; set; }
        public string SourceReference { get; set; }
        public string Claim { get; set; }
        public float Confidence { get; set; }
        public bool IsAuthoritative { get; set; }
    }

    /// <summary>
    /// A step in the reasoning process.
    /// </summary>
    public class ReasoningStep
    {
        public int StepNumber { get; set; }
        public string StepType { get; set; }
        public string Description { get; set; }
        public string Input { get; set; }
        public string Output { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// A factor contributing to overall confidence.
    /// </summary>
    public class ConfidenceContributor
    {
        public string Factor { get; set; }
        public float Contribution { get; set; }
        public string Direction { get; set; }
    }

    /// <summary>
    /// An alternative that was considered during decision-making.
    /// </summary>
    public class ConsideredAlternative
    {
        public string Name { get; set; }
        public float Score { get; set; }
        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// Explanation for why an alternative was rejected.
    /// </summary>
    public class RejectionExplanation
    {
        public string AlternativeId { get; set; }
        public string AlternativeName { get; set; }
        public string RejectionReason { get; set; }
        public string WhatIfDescription { get; set; }
    }

    /// <summary>
    /// A detected contradiction between knowledge sources or rules.
    /// </summary>
    public class Contradiction
    {
        public string SourceA { get; set; }
        public string SourceB { get; set; }
        public string ClaimA { get; set; }
        public string ClaimB { get; set; }
        public ContradictionSeverity Severity { get; set; }
        public string Resolution { get; set; }
    }

    /// <summary>
    /// Trace of a recommendation for explanation generation.
    /// </summary>
    public class RecommendationTrace
    {
        public string RecommendationId { get; set; }
        public string RecommendationType { get; set; }
        public string RecommendationDescription { get; set; }
        public List<string> PrimaryReasons { get; set; }
        public Dictionary<string, object> InputFactors { get; set; }
        public List<KnowledgeSourceTrace> KnowledgeSources { get; set; }
        public List<InferenceTrace> InferencesApplied { get; set; }
        public List<LearnedPatternTrace> LearnedPatternEvidence { get; set; }
        public List<ConfidenceContributor> ConfidenceContributors { get; set; }
        public List<ConsideredAlternative> AlternativesConsidered { get; set; }
        public List<string> MissingInformation { get; set; }
        public float OverallConfidence { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// Trace of a knowledge source consulted during reasoning.
    /// </summary>
    public class KnowledgeSourceTrace
    {
        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public string Reference { get; set; }
        public string FindingSummary { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Trace of an inference rule applied during reasoning.
    /// </summary>
    public class InferenceTrace
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Conclusion { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Trace of a learned pattern used as evidence.
    /// </summary>
    public class LearnedPatternTrace
    {
        public string PatternName { get; set; }
        public string Description { get; set; }
        public int Occurrences { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Record of a decision for explanation.
    /// </summary>
    public class DecisionRecord
    {
        public string DecisionId { get; set; }
        public string DecisionType { get; set; }
        public string Description { get; set; }
        public string PrimaryReason { get; set; }
        public string ChosenOption { get; set; }
        public string RejectedOption { get; set; }
        public string CalculatedValue { get; set; }
        public float Confidence { get; set; }
        public List<DecisionFactor> Factors { get; set; }
        public List<SupportingEvidenceItem> SupportingEvidence { get; set; }
        public List<ConsideredAlternative> AlternativesConsidered { get; set; }
        public List<RejectedAlternative> RejectedAlternatives { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// A factor considered in a decision.
    /// </summary>
    public class DecisionFactor
    {
        public string Name { get; set; }
        public float Weight { get; set; }
        public float Score { get; set; }
    }

    /// <summary>
    /// An item of supporting evidence.
    /// </summary>
    public class SupportingEvidenceItem
    {
        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public string Reference { get; set; }
        public string Claim { get; set; }
        public float Confidence { get; set; }
        public bool IsAuthoritative { get; set; }
    }

    /// <summary>
    /// An alternative that was rejected.
    /// </summary>
    public class RejectedAlternative
    {
        public string AlternativeId { get; set; }
        public string Name { get; set; }
        public string RejectionReason { get; set; }
        public string WhatIfConsequence { get; set; }
    }

    /// <summary>
    /// An evidence source registered with the explanation system.
    /// </summary>
    public class EvidenceSource
    {
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public EvidenceSourceType SourceType { get; set; }
        public string Description { get; set; }
        public float AuthorityLevel { get; set; }
    }

    /// <summary>
    /// A project precedent for precedent-based explanation.
    /// </summary>
    public class ProjectPrecedent
    {
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public int Year { get; set; }
        public float Similarity { get; set; }
        public float SuccessRate { get; set; }
        public string Outcome { get; set; }
    }

    /// <summary>
    /// An action's audit trail for trace reports.
    /// </summary>
    public class ActionAuditTrail
    {
        public string ActionId { get; set; }
        public string ActionType { get; set; }
        public string Description { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public List<AuditStep> Steps { get; set; }
    }

    /// <summary>
    /// A single step in an audit trail.
    /// </summary>
    public class AuditStep
    {
        public int StepNumber { get; set; }
        public string StepName { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    /// <summary>
    /// A trace report containing complete audit information.
    /// </summary>
    public class TraceReport
    {
        public string ActionId { get; set; }
        public string ActionType { get; set; }
        public string ActionDescription { get; set; }
        public string Summary { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalSteps { get; set; }
        public List<TraceSection> Sections { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// A section of a trace report.
    /// </summary>
    public class TraceSection
    {
        public string SectionName { get; set; }
        public string Description { get; set; }
        public string Content { get; set; }
        public int Order { get; set; }
    }

    /// <summary>
    /// Template for generating explanations of a specific recommendation type.
    /// </summary>
    public class ExplanationTemplate
    {
        public string RecommendationType { get; set; }
        public string BriefFormat { get; set; }
        public string StandardFormat { get; set; }
    }

    /// <summary>
    /// Configuration for the explanation generator.
    /// </summary>
    public class ExplanationConfiguration
    {
        public ExplanationDepth DefaultDepth { get; set; } = ExplanationDepth.Standard;
        public int MaxEvidenceChainLength { get; set; } = 20;
        public int MaxReasoningSteps { get; set; } = 10;
        public bool EnableContradictionDetection { get; set; } = true;
        public int MaxVoiceOutputLength { get; set; } = 200;
    }

    #endregion
}
