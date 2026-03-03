// ===================================================================
// StingBIM.AI.Collaboration - Intelligence Orchestration System
// Coordinates all intelligence layers for unified AI capabilities
// Provides composite insights and automated decision support
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Orchestration Models

    /// <summary>
    /// Intelligence query request
    /// </summary>
    public class IntelligenceQuery
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string QueryText { get; set; } = string.Empty;
        public QueryIntent Intent { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> RequiredLayers { get; set; } = new();
        public int MaxResponseTime { get; set; } = 5000; // ms
        public QueryPriority Priority { get; set; } = QueryPriority.Normal;
    }

    /// <summary>
    /// Query intent types
    /// </summary>
    public enum QueryIntent
    {
        Question,
        Command,
        Analysis,
        Prediction,
        Recommendation,
        Alert,
        Search,
        Comparison
    }

    /// <summary>
    /// Query priority
    /// </summary>
    public enum QueryPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Composite intelligence response
    /// </summary>
    public class IntelligenceResponse
    {
        public string QueryId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<InsightResult> Insights { get; set; } = new();
        public List<RecommendationResult> Recommendations { get; set; } = new();
        public List<PredictionSummary> Predictions { get; set; } = new();
        public List<AlertResult> Alerts { get; set; } = new();
        public Dictionary<string, object> Data { get; set; } = new();
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<string> LayersUsed { get; set; } = new();
        public string? FollowUpSuggestion { get; set; }
    }

    /// <summary>
    /// Individual insight from a layer
    /// </summary>
    public class InsightResult
    {
        public string Source { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double Relevance { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Recommendation result
    /// </summary>
    public class RecommendationResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public double Priority { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; } = string.Empty;
        public decimal? EstimatedImpact { get; set; }
        public List<string> Prerequisites { get; set; } = new();
    }

    /// <summary>
    /// Prediction summary
    /// </summary>
    public class PredictionSummary
    {
        public string Metric { get; set; } = string.Empty;
        public object PredictedValue { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string TimeFrame { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty;
    }

    /// <summary>
    /// Alert result
    /// </summary>
    public class AlertResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public List<string> AffectedItems { get; set; } = new();
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Layer health status
    /// </summary>
    public class LayerHealthStatus
    {
        public string LayerName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public double ResponseTime { get; set; }
        public int RequestsProcessed { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastActivity { get; set; }
    }

    #endregion

    #region Intelligence Orchestrator

    /// <summary>
    /// Main intelligence orchestration system
    /// </summary>
    public class IntelligenceOrchestrator : IAsyncDisposable
    {
        private readonly ILogger? _logger;

        // Intelligence layers
        private readonly ContextualAwarenessLayer _contextLayer;
        private readonly SemanticUnderstandingLayer _semanticLayer;
        private readonly PredictiveAnalyticsLayer _predictiveLayer;
        private readonly SafetyQualityLayer _safetyQualityLayer;
        private readonly KnowledgeGraphLayer _knowledgeGraph;

        // State
        private readonly ConcurrentDictionary<string, IntelligenceQuery> _activeQueries = new();
        private readonly ConcurrentDictionary<string, LayerHealthStatus> _layerHealth = new();
        private readonly ConcurrentQueue<IntelligenceQuery> _queryQueue = new();
        private readonly ConcurrentDictionary<string, List<InsightResult>> _insightCache = new();

        // Configuration
        private readonly int _maxConcurrentQueries = 10;
        private readonly SemaphoreSlim _querySemaphore;

        public IntelligenceOrchestrator(ILogger? logger = null)
        {
            _logger = logger;
            _querySemaphore = new SemaphoreSlim(_maxConcurrentQueries);

            // Initialize layers
            _contextLayer = new ContextualAwarenessLayer(logger);
            _semanticLayer = new SemanticUnderstandingLayer(logger);
            _predictiveLayer = new PredictiveAnalyticsLayer(logger);
            _safetyQualityLayer = new SafetyQualityLayer(logger);
            _knowledgeGraph = new KnowledgeGraphLayer(logger);

            // Initialize health tracking
            InitializeHealthTracking();

            _logger?.LogInformation("IntelligenceOrchestrator initialized with 5 layers");
        }

        #region Query Processing

        /// <summary>
        /// Process an intelligence query
        /// </summary>
        public async Task<IntelligenceResponse> ProcessQueryAsync(
            IntelligenceQuery query,
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new IntelligenceResponse { QueryId = query.Id };

            try
            {
                await _querySemaphore.WaitAsync(ct);
                _activeQueries[query.Id] = query;

                // Analyze query semantics
                var semanticAnalysis = await _semanticLayer.AnalyzeAsync(query.QueryText, ct);
                response.LayersUsed.Add("semantic");

                // Get context
                var userContext = await _contextLayer.GetUserContextAsync(query.UserId, ct);
                var projectContext = await _contextLayer.GetProjectContextAsync(query.ProjectId, ct: ct);
                response.LayersUsed.Add("context");

                // Route to appropriate handlers based on intent
                var intent = DetermineIntent(query, semanticAnalysis);

                switch (intent)
                {
                    case QueryIntent.Question:
                        await HandleQuestionAsync(query, response, semanticAnalysis, ct);
                        break;

                    case QueryIntent.Analysis:
                        await HandleAnalysisAsync(query, response, projectContext, ct);
                        break;

                    case QueryIntent.Prediction:
                        await HandlePredictionAsync(query, response, projectContext, ct);
                        break;

                    case QueryIntent.Recommendation:
                        await HandleRecommendationAsync(query, response, userContext, projectContext, ct);
                        break;

                    case QueryIntent.Alert:
                        await HandleAlertCheckAsync(query, response, projectContext, ct);
                        break;

                    case QueryIntent.Search:
                        await HandleSearchAsync(query, response, semanticAnalysis, ct);
                        break;

                    default:
                        await HandleGeneralQueryAsync(query, response, semanticAnalysis, ct);
                        break;
                }

                // Generate composite insights
                await GenerateCompositeInsightsAsync(response, projectContext, ct);

                // Calculate overall confidence
                response.Confidence = CalculateOverallConfidence(response);

                // Generate summary
                response.Summary = GenerateResponseSummary(response);

                // Suggest follow-up
                response.FollowUpSuggestion = SuggestFollowUp(query, response);

                response.Success = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing query {QueryId}", query.Id);
                response.Success = false;
                response.Summary = $"Error processing query: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                response.ProcessingTime = stopwatch.Elapsed;
                _activeQueries.TryRemove(query.Id, out _);
                _querySemaphore.Release();
            }

            _logger?.LogInformation("Query {QueryId} processed in {Time}ms with {Insights} insights",
                query.Id, stopwatch.ElapsedMilliseconds, response.Insights.Count);

            return response;
        }

        private QueryIntent DetermineIntent(IntelligenceQuery query, SemanticAnalysisResult analysis)
        {
            if (query.Intent != default)
                return query.Intent;

            // Infer from semantic analysis
            var primaryIntent = analysis.Intents.FirstOrDefault();
            if (primaryIntent != null)
            {
                if (primaryIntent.Intent.Contains("query"))
                    return QueryIntent.Question;
                if (primaryIntent.Intent.Contains("analyze"))
                    return QueryIntent.Analysis;
                if (primaryIntent.Intent.Contains("predict"))
                    return QueryIntent.Prediction;
            }

            // Infer from keywords
            var textLower = query.QueryText.ToLower();
            if (textLower.Contains("predict") || textLower.Contains("forecast") || textLower.Contains("will"))
                return QueryIntent.Prediction;
            if (textLower.Contains("recommend") || textLower.Contains("suggest") || textLower.Contains("should"))
                return QueryIntent.Recommendation;
            if (textLower.Contains("alert") || textLower.Contains("warning") || textLower.Contains("risk"))
                return QueryIntent.Alert;
            if (textLower.Contains("find") || textLower.Contains("search") || textLower.Contains("where"))
                return QueryIntent.Search;
            if (textLower.Contains("analyze") || textLower.Contains("compare") || textLower.Contains("evaluate"))
                return QueryIntent.Analysis;

            return QueryIntent.Question;
        }

        #endregion

        #region Query Handlers

        private async Task HandleQuestionAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            SemanticAnalysisResult semantics,
            CancellationToken ct)
        {
            // Search knowledge graph for relevant information
            foreach (var entity in semantics.Entities.Take(5))
            {
                var nodes = _knowledgeGraph.SearchNodes(
                    type: entity.Type,
                    labelContains: entity.Text,
                    limit: 5);

                foreach (var node in nodes)
                {
                    response.Insights.Add(new InsightResult
                    {
                        Source = "knowledge_graph",
                        Type = "entity_info",
                        Title = node.Label,
                        Description = $"Found {node.Type}: {node.Label}",
                        Details = node.Properties,
                        Confidence = 0.9,
                        Relevance = 0.8
                    });
                }
            }

            response.LayersUsed.Add("knowledge_graph");
        }

        private async Task HandleAnalysisAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            ProjectContext context,
            CancellationToken ct)
        {
            // Add project health analysis
            response.Insights.Add(new InsightResult
            {
                Source = "context",
                Type = "health_analysis",
                Title = "Project Health",
                Description = $"Overall project health: {context.Health.OverallScore:F1}/100",
                Details = new Dictionary<string, object>
                {
                    ["schedule"] = context.Health.ScheduleHealth,
                    ["budget"] = context.Health.BudgetHealth,
                    ["quality"] = context.Health.QualityHealth,
                    ["safety"] = context.Health.SafetyHealth,
                    ["team"] = context.Health.TeamHealth
                },
                Confidence = 0.85,
                Relevance = 0.9
            });

            // Add safety analysis
            var safetyTrends = await _safetyQualityLayer.AnalyzeSafetyTrendsAsync(
                context.ProjectId, DateTime.UtcNow.AddDays(-30), ct);

            response.Insights.Add(new InsightResult
            {
                Source = "safety_quality",
                Type = "safety_analysis",
                Title = "Safety Performance",
                Description = $"Incident rate: {safetyTrends.IncidentRate:F2}, Trend: {safetyTrends.Trend}",
                Confidence = 0.8,
                Relevance = 0.85
            });

            // Add quality analysis
            var qualityTrends = await _safetyQualityLayer.AnalyzeQualityTrendsAsync(
                context.ProjectId, DateTime.UtcNow.AddDays(-30), ct);

            response.Insights.Add(new InsightResult
            {
                Source = "safety_quality",
                Type = "quality_analysis",
                Title = "Quality Performance",
                Description = $"First-time quality: {qualityTrends.FirstTimeQualityRate:P0}, Rework rate: {qualityTrends.ReworkRate:P0}",
                Confidence = 0.8,
                Relevance = 0.85
            });

            response.LayersUsed.Add("safety_quality");
        }

        private async Task HandlePredictionAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            ProjectContext context,
            CancellationToken ct)
        {
            // Schedule predictions
            var scheduleActivities = new List<ScheduleActivity>
            {
                new() { Id = "act1", Name = "Foundation", Type = "concrete", PlannedStart = DateTime.UtcNow, PlannedEnd = DateTime.UtcNow.AddDays(14), ResourceUtilization = 0.85 }
            };

            var schedulePredictions = await _predictiveLayer.PredictScheduleAsync(
                context.ProjectId, scheduleActivities, ct);

            foreach (var pred in schedulePredictions.Take(3))
            {
                response.Predictions.Add(new PredictionSummary
                {
                    Metric = $"Activity: {pred.ActivityName}",
                    PredictedValue = $"Delay: {pred.PredictedDelayDays} days",
                    Confidence = 1 - pred.DelayProbability,
                    TimeFrame = pred.PredictedEnd.ToString("yyyy-MM-dd"),
                    Trend = pred.DelayProbability > 0.5 ? "At Risk" : "On Track"
                });
            }

            // Risk predictions
            var risks = await _predictiveLayer.PredictRisksAsync(context.ProjectId, ct);

            foreach (var risk in risks.Take(3))
            {
                response.Predictions.Add(new PredictionSummary
                {
                    Metric = $"Risk: {risk.Category}",
                    PredictedValue = risk.Description,
                    Confidence = 1 - risk.Probability,
                    TimeFrame = risk.PredictedOccurrence.ToString("yyyy-MM-dd"),
                    Trend = risk.RiskScore > 0.5 ? "High" : "Medium"
                });
            }

            response.LayersUsed.Add("predictive");
        }

        private async Task HandleRecommendationAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            UserContext userContext,
            ProjectContext projectContext,
            CancellationToken ct)
        {
            // Get contextual recommendations
            var contextRecs = await _contextLayer.GetContextualRecommendationsAsync(
                userContext.UserId, projectContext.ProjectId, ct);

            foreach (var rec in contextRecs.Take(5))
            {
                response.Recommendations.Add(new RecommendationResult
                {
                    Title = rec.Title,
                    Description = rec.Description,
                    Action = rec.Action,
                    Priority = rec.Priority,
                    Confidence = 0.8,
                    Rationale = $"Based on {rec.Type} analysis"
                });
            }

            // Add predicted action recommendations
            var predictedActions = await _contextLayer.PredictNextActionsAsync(userContext.UserId, ct);

            foreach (var action in predictedActions.Take(3))
            {
                response.Recommendations.Add(new RecommendationResult
                {
                    Title = $"Suggested: {action.Action}",
                    Description = action.Reason,
                    Action = action.Action,
                    Priority = action.Confidence,
                    Confidence = action.Confidence
                });
            }

            // Add health-based recommendations
            foreach (var issue in projectContext.Health.Issues)
            {
                response.Recommendations.Add(new RecommendationResult
                {
                    Title = $"{issue.Category} Improvement",
                    Description = issue.Description,
                    Action = issue.RecommendedAction,
                    Priority = issue.Impact,
                    Confidence = 0.85,
                    Rationale = "Based on project health analysis"
                });
            }
        }

        private async Task HandleAlertCheckAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            ProjectContext context,
            CancellationToken ct)
        {
            // Check for context anomalies
            var anomalies = await _contextLayer.DetectAnomaliesAsync(context.ProjectId, ct);

            foreach (var anomaly in anomalies)
            {
                response.Alerts.Add(new AlertResult
                {
                    Type = anomaly.Type,
                    Severity = anomaly.Severity,
                    Message = anomaly.Description,
                    Source = "context",
                    DetectedAt = anomaly.DetectedAt
                });
            }

            // Check for safety hazards
            var hazards = await _safetyQualityLayer.DetectHazardsFromTextAsync(
                context.ProjectId, query.QueryText, ct: ct);

            foreach (var hazard in hazards)
            {
                response.Alerts.Add(new AlertResult
                {
                    Type = "safety",
                    Severity = hazard.Severity.ToString(),
                    Message = hazard.Description,
                    Source = "safety_quality",
                    DetectedAt = hazard.DetectedAt,
                    RecommendedAction = hazard.RecommendedControls.FirstOrDefault()?.Description
                });
            }

            // Check project health alerts
            if (context.Health.OverallScore < 70)
            {
                response.Alerts.Add(new AlertResult
                {
                    Type = "project_health",
                    Severity = context.Health.OverallScore < 50 ? "Critical" : "Warning",
                    Message = $"Project health score is low: {context.Health.OverallScore:F0}/100",
                    Source = "context"
                });
            }
        }

        private async Task HandleSearchAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            SemanticAnalysisResult semantics,
            CancellationToken ct)
        {
            // Search knowledge graph
            var searchTerms = semantics.Keywords;

            foreach (var term in searchTerms.Take(5))
            {
                var nodes = _knowledgeGraph.SearchNodes(labelContains: term, limit: 10);

                foreach (var node in nodes)
                {
                    // Get connected subgraph
                    var subgraph = _knowledgeGraph.GetConnectedSubgraph(node.Id, 1, 5);

                    response.Insights.Add(new InsightResult
                    {
                        Source = "knowledge_graph",
                        Type = "search_result",
                        Title = node.Label,
                        Description = $"{node.Type} with {subgraph.Edges.Count} connections",
                        Details = new Dictionary<string, object>
                        {
                            ["properties"] = node.Properties,
                            ["connections"] = subgraph.Edges.Count
                        },
                        Confidence = 0.9,
                        Relevance = 0.7
                    });
                }
            }

            response.LayersUsed.Add("knowledge_graph");
        }

        private async Task HandleGeneralQueryAsync(
            IntelligenceQuery query,
            IntelligenceResponse response,
            SemanticAnalysisResult semantics,
            CancellationToken ct)
        {
            // Combine insights from multiple layers
            await HandleQuestionAsync(query, response, semantics, ct);

            // Add sentiment-based insights
            if (semantics.Sentiment != null)
            {
                if (semantics.Sentiment.Urgency > 0.5)
                {
                    response.Insights.Add(new InsightResult
                    {
                        Source = "semantic",
                        Type = "urgency_detected",
                        Title = "Urgent Request Detected",
                        Description = $"This query appears urgent (urgency score: {semantics.Sentiment.Urgency:F2})",
                        Confidence = semantics.Sentiment.Urgency,
                        Relevance = 0.9
                    });
                }
            }
        }

        #endregion

        #region Composite Analysis

        private async Task GenerateCompositeInsightsAsync(
            IntelligenceResponse response,
            ProjectContext context,
            CancellationToken ct)
        {
            // Cross-reference insights
            var safetyInsights = response.Insights.Where(i => i.Source == "safety_quality").ToList();
            var scheduleInsights = response.Predictions.Where(p => p.Metric.Contains("Activity")).ToList();

            // Correlate safety with schedule
            if (safetyInsights.Any() && scheduleInsights.Any())
            {
                response.Insights.Add(new InsightResult
                {
                    Source = "orchestrator",
                    Type = "correlation",
                    Title = "Safety-Schedule Correlation",
                    Description = "Analysis shows relationship between safety performance and schedule adherence",
                    Confidence = 0.75,
                    Relevance = 0.8
                });
            }

            // Add trend insights
            if (context.Health.Trend == HealthTrend.Declining)
            {
                response.Insights.Add(new InsightResult
                {
                    Source = "orchestrator",
                    Type = "trend_alert",
                    Title = "Declining Project Health",
                    Description = "Project health metrics show a declining trend",
                    Confidence = 0.85,
                    Relevance = 0.95
                });
            }
        }

        #endregion

        #region Helper Methods

        private double CalculateOverallConfidence(IntelligenceResponse response)
        {
            var scores = new List<double>();

            if (response.Insights.Any())
                scores.Add(response.Insights.Average(i => i.Confidence));
            if (response.Recommendations.Any())
                scores.Add(response.Recommendations.Average(r => r.Confidence));
            if (response.Predictions.Any())
                scores.Add(response.Predictions.Average(p => p.Confidence));

            return scores.Any() ? scores.Average() : 0.5;
        }

        private string GenerateResponseSummary(IntelligenceResponse response)
        {
            var parts = new List<string>();

            if (response.Insights.Any())
                parts.Add($"{response.Insights.Count} insights");
            if (response.Recommendations.Any())
                parts.Add($"{response.Recommendations.Count} recommendations");
            if (response.Predictions.Any())
                parts.Add($"{response.Predictions.Count} predictions");
            if (response.Alerts.Any())
                parts.Add($"{response.Alerts.Count} alerts");

            if (!parts.Any())
                return "No significant findings.";

            return $"Analysis complete: {string.Join(", ", parts)} (confidence: {response.Confidence:P0})";
        }

        private string? SuggestFollowUp(IntelligenceQuery query, IntelligenceResponse response)
        {
            if (response.Alerts.Any(a => a.Severity == "Critical"))
                return "Would you like me to create action items for the critical alerts?";

            if (response.Recommendations.Count > 3)
                return "Would you like me to prioritize these recommendations?";

            if (response.Predictions.Any(p => p.Trend == "At Risk"))
                return "Would you like a detailed risk analysis for the at-risk items?";

            return null;
        }

        private void InitializeHealthTracking()
        {
            var layers = new[] { "context", "semantic", "predictive", "safety_quality", "knowledge_graph" };
            foreach (var layer in layers)
            {
                _layerHealth[layer] = new LayerHealthStatus
                {
                    LayerName = layer,
                    IsHealthy = true,
                    LastActivity = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region Health Monitoring

        /// <summary>
        /// Get health status of all layers
        /// </summary>
        public List<LayerHealthStatus> GetHealthStatus()
        {
            return _layerHealth.Values.ToList();
        }

        /// <summary>
        /// Check if system is healthy
        /// </summary>
        public bool IsHealthy()
        {
            return _layerHealth.Values.All(h => h.IsHealthy);
        }

        #endregion

        #region Direct Layer Access

        /// <summary>
        /// Get contextual awareness layer
        /// </summary>
        public ContextualAwarenessLayer Context => _contextLayer;

        /// <summary>
        /// Get semantic understanding layer
        /// </summary>
        public SemanticUnderstandingLayer Semantic => _semanticLayer;

        /// <summary>
        /// Get predictive analytics layer
        /// </summary>
        public PredictiveAnalyticsLayer Predictive => _predictiveLayer;

        /// <summary>
        /// Get safety and quality layer
        /// </summary>
        public SafetyQualityLayer SafetyQuality => _safetyQualityLayer;

        /// <summary>
        /// Get knowledge graph layer
        /// </summary>
        public KnowledgeGraphLayer KnowledgeGraph => _knowledgeGraph;

        #endregion

        public async ValueTask DisposeAsync()
        {
            _querySemaphore.Dispose();
            await _contextLayer.DisposeAsync();
            await _semanticLayer.DisposeAsync();
            await _predictiveLayer.DisposeAsync();
            await _safetyQualityLayer.DisposeAsync();
            await _knowledgeGraph.DisposeAsync();
            _logger?.LogInformation("IntelligenceOrchestrator disposed");
        }
    }

    #endregion
}
