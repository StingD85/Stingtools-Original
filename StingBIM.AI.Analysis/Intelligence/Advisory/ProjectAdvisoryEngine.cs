// ============================================================================
// StingBIM AI - Project Advisory Engine
// Intelligent advisor for BIM model analysis, project strategies, and decisions
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.Advisory
{
    /// <summary>
    /// AI-powered advisory engine that provides intelligent recommendations
    /// for model analysis strategies, project decisions, and optimization routes.
    /// </summary>
    public sealed class ProjectAdvisoryEngine
    {
        private static readonly Lazy<ProjectAdvisoryEngine> _instance =
            new Lazy<ProjectAdvisoryEngine>(() => new ProjectAdvisoryEngine());
        public static ProjectAdvisoryEngine Instance => _instance.Value;

        private readonly Dictionary<string, AnalysisStrategy> _analysisStrategies = new();
        private readonly Dictionary<string, ProjectDecisionRule> _decisionRules = new();
        private readonly List<ProjectContext> _projectHistory = new();
        private readonly object _lock = new object();

        public event EventHandler<AdvisoryEventArgs> RecommendationGenerated;
        public event EventHandler<AdvisoryEventArgs> WarningRaised;

        private ProjectAdvisoryEngine()
        {
            InitializeStrategies();
            InitializeDecisionRules();
        }

        #region Initialization

        private void InitializeStrategies()
        {
            // Model Analysis Strategies
            _analysisStrategies["model_health"] = new AnalysisStrategy
            {
                StrategyId = "model_health",
                Name = "Model Health Analysis",
                Description = "Comprehensive assessment of BIM model quality and integrity",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "element_audit", Description = "Audit all model elements for completeness" },
                    new() { Order = 2, Action = "parameter_check", Description = "Verify all required parameters are populated" },
                    new() { Order = 3, Action = "warning_analysis", Description = "Analyze Revit warnings and their severity" },
                    new() { Order = 4, Action = "family_audit", Description = "Check for unused and oversized families" },
                    new() { Order = 5, Action = "workset_review", Description = "Review workset organization" },
                    new() { Order = 6, Action = "link_check", Description = "Verify linked model integrity" }
                },
                ExpectedDuration = TimeSpan.FromMinutes(30),
                Priority = AnalysisPriority.High
            };

            _analysisStrategies["clash_resolution"] = new AnalysisStrategy
            {
                StrategyId = "clash_resolution",
                Name = "Clash Detection and Resolution",
                Description = "Systematic clash detection and automated resolution recommendations",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "inter_discipline_clash", Description = "Run cross-discipline clash detection" },
                    new() { Order = 2, Action = "categorize_clashes", Description = "Categorize by severity and type" },
                    new() { Order = 3, Action = "identify_patterns", Description = "Identify systematic clash patterns" },
                    new() { Order = 4, Action = "generate_solutions", Description = "Generate automated resolution options" },
                    new() { Order = 5, Action = "cost_impact", Description = "Assess cost impact of each solution" },
                    new() { Order = 6, Action = "priority_ranking", Description = "Rank clashes by resolution priority" }
                },
                ExpectedDuration = TimeSpan.FromHours(2),
                Priority = AnalysisPriority.Critical
            };

            _analysisStrategies["cost_optimization"] = new AnalysisStrategy
            {
                StrategyId = "cost_optimization",
                Name = "Cost Optimization Analysis",
                Description = "Analyze model for cost reduction opportunities",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "quantity_extraction", Description = "Extract accurate quantities from model" },
                    new() { Order = 2, Action = "material_analysis", Description = "Analyze material usage and waste" },
                    new() { Order = 3, Action = "alternative_materials", Description = "Identify cost-effective alternatives" },
                    new() { Order = 4, Action = "design_efficiency", Description = "Evaluate design efficiency ratios" },
                    new() { Order = 5, Action = "value_engineering", Description = "Suggest value engineering options" },
                    new() { Order = 6, Action = "roi_calculation", Description = "Calculate ROI for each suggestion" }
                },
                ExpectedDuration = TimeSpan.FromHours(4),
                Priority = AnalysisPriority.High
            };

            _analysisStrategies["demolition_planning"] = new AnalysisStrategy
            {
                StrategyId = "demolition_planning",
                Name = "Demolition Planning Analysis",
                Description = "Comprehensive demolition cost and logistics planning",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "demolition_scope", Description = "Identify all elements marked for demolition" },
                    new() { Order = 2, Action = "hazmat_assessment", Description = "Check for hazardous materials" },
                    new() { Order = 3, Action = "structural_sequence", Description = "Plan safe demolition sequence" },
                    new() { Order = 4, Action = "waste_quantification", Description = "Quantify demolition waste by type" },
                    new() { Order = 5, Action = "disposal_costing", Description = "Calculate disposal costs" },
                    new() { Order = 6, Action = "salvage_potential", Description = "Identify salvageable materials" },
                    new() { Order = 7, Action = "timeline_estimation", Description = "Estimate demolition timeline" }
                },
                ExpectedDuration = TimeSpan.FromHours(3),
                Priority = AnalysisPriority.High
            };

            _analysisStrategies["4d_schedule"] = new AnalysisStrategy
            {
                StrategyId = "4d_schedule",
                Name = "4D Schedule Integration",
                Description = "Link model elements to construction schedule",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "element_grouping", Description = "Group elements by construction phase" },
                    new() { Order = 2, Action = "dependency_mapping", Description = "Map element dependencies" },
                    new() { Order = 3, Action = "critical_path", Description = "Identify critical path elements" },
                    new() { Order = 4, Action = "resource_leveling", Description = "Optimize resource allocation" },
                    new() { Order = 5, Action = "milestone_definition", Description = "Define key milestones" },
                    new() { Order = 6, Action = "progress_metrics", Description = "Setup progress tracking" }
                },
                ExpectedDuration = TimeSpan.FromHours(6),
                Priority = AnalysisPriority.High
            };

            _analysisStrategies["5d_costing"] = new AnalysisStrategy
            {
                StrategyId = "5d_costing",
                Name = "5D Cost Management",
                Description = "Real-time cost tracking from model quantities",
                Steps = new List<AnalysisStep>
                {
                    new() { Order = 1, Action = "cost_database_link", Description = "Link to cost database" },
                    new() { Order = 2, Action = "quantity_takeoff", Description = "Automated quantity takeoff" },
                    new() { Order = 3, Action = "cost_calculation", Description = "Calculate element costs" },
                    new() { Order = 4, Action = "variance_tracking", Description = "Track cost variances" },
                    new() { Order = 5, Action = "forecast_update", Description = "Update cost forecasts" },
                    new() { Order = 6, Action = "cash_flow", Description = "Generate cash flow projections" }
                },
                ExpectedDuration = TimeSpan.FromHours(4),
                Priority = AnalysisPriority.Critical
            };
        }

        private void InitializeDecisionRules()
        {
            // Decision rules for various project scenarios
            _decisionRules["high_clash_count"] = new ProjectDecisionRule
            {
                RuleId = "high_clash_count",
                Condition = "clash_count > 100",
                Recommendation = "Focus on systematic clash resolution before proceeding with detailed design",
                Actions = new List<string>
                {
                    "Run pattern analysis to identify root causes",
                    "Schedule coordination meeting with affected disciplines",
                    "Prioritize clashes by trade and severity",
                    "Consider design freeze for high-conflict zones"
                },
                Priority = DecisionPriority.Critical
            };

            _decisionRules["cost_overrun"] = new ProjectDecisionRule
            {
                RuleId = "cost_overrun",
                Condition = "current_cost > budget * 1.1",
                Recommendation = "Initiate value engineering review",
                Actions = new List<string>
                {
                    "Identify top cost drivers from model",
                    "Analyze alternative materials and systems",
                    "Review scope for potential reductions",
                    "Evaluate phased construction approach"
                },
                Priority = DecisionPriority.High
            };

            _decisionRules["schedule_delay"] = new ProjectDecisionRule
            {
                RuleId = "schedule_delay",
                Condition = "schedule_variance > 10%",
                Recommendation = "Implement schedule recovery plan",
                Actions = new List<string>
                {
                    "Identify critical path activities at risk",
                    "Evaluate fast-track opportunities",
                    "Consider additional resources for critical activities",
                    "Review prefabrication opportunities"
                },
                Priority = DecisionPriority.High
            };

            _decisionRules["model_quality_low"] = new ProjectDecisionRule
            {
                RuleId = "model_quality_low",
                Condition = "model_health_score < 70",
                Recommendation = "Pause design development and focus on model cleanup",
                Actions = new List<string>
                {
                    "Address all critical Revit warnings",
                    "Complete missing parameter data",
                    "Purge unused elements and families",
                    "Verify model standards compliance"
                },
                Priority = DecisionPriority.High
            };

            _decisionRules["design_change"] = new ProjectDecisionRule
            {
                RuleId = "design_change",
                Condition = "design_change_requested",
                Recommendation = "Evaluate change impact before implementation",
                Actions = new List<string>
                {
                    "Run cost impact analysis",
                    "Check schedule implications",
                    "Identify affected disciplines and elements",
                    "Generate comparison visualization"
                },
                Priority = DecisionPriority.High
            };
        }

        #endregion

        #region Advisory Methods

        /// <summary>
        /// Get the recommended analysis strategy based on project context
        /// </summary>
        public AnalysisRecommendation GetAnalysisRecommendation(ProjectAnalysisContext context)
        {
            var recommendations = new List<StrategyRecommendation>();

            // Evaluate each strategy's applicability
            foreach (var strategy in _analysisStrategies.Values)
            {
                var score = EvaluateStrategyApplicability(strategy, context);
                if (score > 0)
                {
                    recommendations.Add(new StrategyRecommendation
                    {
                        Strategy = strategy,
                        Score = score,
                        Reasoning = GetStrategyReasoning(strategy, context)
                    });
                }
            }

            // Sort by score and priority
            recommendations = recommendations
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => (int)r.Strategy.Priority)
                .ToList();

            var result = new AnalysisRecommendation
            {
                GeneratedAt = DateTime.UtcNow,
                Context = context,
                PrimaryRecommendation = recommendations.FirstOrDefault()?.Strategy,
                AlternativeStrategies = recommendations.Skip(1).Take(3).Select(r => r.Strategy).ToList(),
                Reasoning = recommendations.FirstOrDefault()?.Reasoning ?? "No specific recommendation at this time.",
                EstimatedEffort = recommendations.FirstOrDefault()?.Strategy.ExpectedDuration ?? TimeSpan.Zero,
                AllRecommendations = recommendations
            };

            RecommendationGenerated?.Invoke(this, new AdvisoryEventArgs
            {
                Type = AdvisoryType.AnalysisStrategy,
                Message = $"Recommended: {result.PrimaryRecommendation?.Name ?? "General review"}"
            });

            return result;
        }

        private double EvaluateStrategyApplicability(AnalysisStrategy strategy, ProjectAnalysisContext context)
        {
            double score = 0;

            switch (strategy.StrategyId)
            {
                case "model_health":
                    if (context.LastHealthCheckDays > 7) score += 30;
                    if (context.WarningCount > 50) score += 40;
                    if (context.MissingParameterPercentage > 10) score += 30;
                    break;

                case "clash_resolution":
                    if (context.ClashCount > 0) score += context.ClashCount > 100 ? 100 : context.ClashCount;
                    if (context.ProjectPhase == "Construction Documents") score += 50;
                    break;

                case "cost_optimization":
                    if (context.CostVariancePercentage > 5) score += 50;
                    if (context.ProjectPhase == "Design Development") score += 40;
                    if (context.ValueEngineeringRequested) score += 60;
                    break;

                case "demolition_planning":
                    if (context.HasDemolitionScope) score += 100;
                    if (context.DemolitionElementCount > 0) score += Math.Min(50, context.DemolitionElementCount / 10);
                    break;

                case "4d_schedule":
                    if (!context.Has4DSchedule) score += 50;
                    if (context.ScheduleVariancePercentage > 0) score += 40;
                    break;

                case "5d_costing":
                    if (context.CostVariancePercentage > 10) score += 60;
                    if (context.RequiresCostTracking) score += 50;
                    break;
            }

            return score;
        }

        private string GetStrategyReasoning(AnalysisStrategy strategy, ProjectAnalysisContext context)
        {
            return strategy.StrategyId switch
            {
                "model_health" when context.WarningCount > 50 =>
                    $"Model has {context.WarningCount} warnings that should be addressed. Last health check was {context.LastHealthCheckDays} days ago.",

                "clash_resolution" when context.ClashCount > 100 =>
                    $"Critical: {context.ClashCount} clashes detected. Systematic resolution needed before construction.",

                "cost_optimization" when context.CostVariancePercentage > 5 =>
                    $"Project is {context.CostVariancePercentage:F1}% over budget. Value engineering opportunities should be explored.",

                "demolition_planning" when context.HasDemolitionScope =>
                    $"Project includes {context.DemolitionElementCount} elements for demolition. Detailed planning required.",

                "4d_schedule" when !context.Has4DSchedule =>
                    "4D schedule not yet linked. Connecting model to schedule will improve project tracking.",

                "5d_costing" when context.RequiresCostTracking =>
                    "Real-time cost tracking from model will help control project budget.",

                _ => $"This analysis strategy is applicable based on current project conditions."
            };
        }

        /// <summary>
        /// Get decision recommendations based on project metrics
        /// </summary>
        public List<DecisionRecommendation> GetDecisionRecommendations(ProjectMetrics metrics)
        {
            var recommendations = new List<DecisionRecommendation>();

            // Evaluate each decision rule
            if (metrics.ClashCount > 100)
            {
                recommendations.Add(CreateDecisionRecommendation(
                    _decisionRules["high_clash_count"], metrics));
            }

            if (metrics.CurrentCost > metrics.Budget * 1.1m)
            {
                recommendations.Add(CreateDecisionRecommendation(
                    _decisionRules["cost_overrun"], metrics));
            }

            if (metrics.ScheduleVariance > 10)
            {
                recommendations.Add(CreateDecisionRecommendation(
                    _decisionRules["schedule_delay"], metrics));
            }

            if (metrics.ModelHealthScore < 70)
            {
                recommendations.Add(CreateDecisionRecommendation(
                    _decisionRules["model_quality_low"], metrics));
            }

            // Sort by priority
            return recommendations
                .OrderByDescending(r => (int)r.Priority)
                .ToList();
        }

        private DecisionRecommendation CreateDecisionRecommendation(
            ProjectDecisionRule rule, ProjectMetrics metrics)
        {
            return new DecisionRecommendation
            {
                RuleId = rule.RuleId,
                Recommendation = rule.Recommendation,
                Priority = rule.Priority,
                Actions = rule.Actions,
                Context = new Dictionary<string, object>
                {
                    { "clash_count", metrics.ClashCount },
                    { "current_cost", metrics.CurrentCost },
                    { "budget", metrics.Budget },
                    { "schedule_variance", metrics.ScheduleVariance },
                    { "model_health", metrics.ModelHealthScore }
                }
            };
        }

        /// <summary>
        /// Analyze the best route for a given project goal
        /// </summary>
        public RouteAnalysis AnalyzeBestRoute(ProjectGoal goal)
        {
            var analysis = new RouteAnalysis
            {
                Goal = goal,
                AnalyzedAt = DateTime.UtcNow,
                Routes = new List<ProjectRoute>()
            };

            switch (goal.GoalType)
            {
                case GoalType.CostReduction:
                    analysis.Routes = GetCostReductionRoutes(goal);
                    break;

                case GoalType.ScheduleAcceleration:
                    analysis.Routes = GetScheduleAccelerationRoutes(goal);
                    break;

                case GoalType.QualityImprovement:
                    analysis.Routes = GetQualityImprovementRoutes(goal);
                    break;

                case GoalType.ClashResolution:
                    analysis.Routes = GetClashResolutionRoutes(goal);
                    break;

                case GoalType.ComplianceAchievement:
                    analysis.Routes = GetComplianceRoutes(goal);
                    break;
            }

            // Rank routes
            analysis.RecommendedRoute = analysis.Routes
                .OrderByDescending(r => r.SuccessProbability)
                .ThenBy(r => r.EstimatedEffort.TotalHours)
                .FirstOrDefault();

            return analysis;
        }

        private List<ProjectRoute> GetCostReductionRoutes(ProjectGoal goal)
        {
            return new List<ProjectRoute>
            {
                new()
                {
                    RouteId = "material_substitution",
                    Name = "Material Substitution Analysis",
                    Description = "Identify equivalent but lower-cost materials",
                    Steps = new List<string>
                    {
                        "Extract current material specifications from model",
                        "Query cost database for alternatives",
                        "Verify compliance with project requirements",
                        "Calculate potential savings",
                        "Generate substitution report"
                    },
                    EstimatedEffort = TimeSpan.FromHours(8),
                    PotentialSavings = goal.TargetValue * 0.05m, // Typically 5%
                    SuccessProbability = 0.85,
                    RiskLevel = RiskLevel.Low
                },
                new()
                {
                    RouteId = "design_optimization",
                    Name = "Design Optimization",
                    Description = "Optimize structural and MEP sizing",
                    Steps = new List<string>
                    {
                        "Analyze current design margins",
                        "Run optimization algorithms",
                        "Verify code compliance",
                        "Update model with optimized elements",
                        "Recalculate costs"
                    },
                    EstimatedEffort = TimeSpan.FromHours(24),
                    PotentialSavings = goal.TargetValue * 0.10m, // Typically 10%
                    SuccessProbability = 0.70,
                    RiskLevel = RiskLevel.Medium
                },
                new()
                {
                    RouteId = "prefabrication_analysis",
                    Name = "Prefabrication Opportunity Analysis",
                    Description = "Identify elements suitable for prefabrication",
                    Steps = new List<string>
                    {
                        "Identify repetitive elements in model",
                        "Evaluate prefab feasibility",
                        "Calculate cost/schedule benefits",
                        "Assess logistics requirements",
                        "Generate prefab packages"
                    },
                    EstimatedEffort = TimeSpan.FromHours(16),
                    PotentialSavings = goal.TargetValue * 0.08m, // Typically 8%
                    SuccessProbability = 0.75,
                    RiskLevel = RiskLevel.Medium
                }
            };
        }

        private List<ProjectRoute> GetScheduleAccelerationRoutes(ProjectGoal goal)
        {
            return new List<ProjectRoute>
            {
                new()
                {
                    RouteId = "critical_path_optimization",
                    Name = "Critical Path Optimization",
                    Description = "Focus resources on critical path activities",
                    Steps = new List<string>
                    {
                        "Identify current critical path",
                        "Analyze resource loading",
                        "Identify acceleration opportunities",
                        "Model resource reallocation",
                        "Update schedule forecast"
                    },
                    EstimatedEffort = TimeSpan.FromHours(12),
                    PotentialSavings = 0, // Schedule focus
                    SuccessProbability = 0.80,
                    RiskLevel = RiskLevel.Low
                },
                new()
                {
                    RouteId = "fast_track_opportunities",
                    Name = "Fast-Track Analysis",
                    Description = "Identify activities that can be overlapped",
                    Steps = new List<string>
                    {
                        "Analyze activity dependencies",
                        "Identify safe overlap opportunities",
                        "Assess risk of concurrent activities",
                        "Model accelerated schedule",
                        "Calculate cost impact"
                    },
                    EstimatedEffort = TimeSpan.FromHours(16),
                    PotentialSavings = 0,
                    SuccessProbability = 0.65,
                    RiskLevel = RiskLevel.High
                }
            };
        }

        private List<ProjectRoute> GetQualityImprovementRoutes(ProjectGoal goal)
        {
            return new List<ProjectRoute>
            {
                new()
                {
                    RouteId = "model_cleanup",
                    Name = "Comprehensive Model Cleanup",
                    Description = "Address all model quality issues",
                    Steps = new List<string>
                    {
                        "Run full model audit",
                        "Resolve all critical warnings",
                        "Complete missing parameters",
                        "Verify naming standards",
                        "Validate element classification"
                    },
                    EstimatedEffort = TimeSpan.FromHours(24),
                    PotentialSavings = 0,
                    SuccessProbability = 0.95,
                    RiskLevel = RiskLevel.Low
                }
            };
        }

        private List<ProjectRoute> GetClashResolutionRoutes(ProjectGoal goal)
        {
            return new List<ProjectRoute>
            {
                new()
                {
                    RouteId = "automated_resolution",
                    Name = "AI-Assisted Clash Resolution",
                    Description = "Use automated resolution for standard clashes",
                    Steps = new List<string>
                    {
                        "Categorize clashes by type",
                        "Apply automated resolution rules",
                        "Review AI suggestions",
                        "Implement approved resolutions",
                        "Re-run clash detection to verify"
                    },
                    EstimatedEffort = TimeSpan.FromHours(8),
                    PotentialSavings = 0,
                    SuccessProbability = 0.80,
                    RiskLevel = RiskLevel.Low
                },
                new()
                {
                    RouteId = "coordination_meeting",
                    Name = "Structured Coordination Sessions",
                    Description = "Resolve complex clashes through coordination",
                    Steps = new List<string>
                    {
                        "Group clashes by trade/area",
                        "Schedule focused coordination sessions",
                        "Use model for real-time resolution",
                        "Document decisions",
                        "Track resolution implementation"
                    },
                    EstimatedEffort = TimeSpan.FromHours(16),
                    PotentialSavings = 0,
                    SuccessProbability = 0.90,
                    RiskLevel = RiskLevel.Low
                }
            };
        }

        private List<ProjectRoute> GetComplianceRoutes(ProjectGoal goal)
        {
            return new List<ProjectRoute>
            {
                new()
                {
                    RouteId = "compliance_audit",
                    Name = "Automated Compliance Audit",
                    Description = "Check model against all applicable codes",
                    Steps = new List<string>
                    {
                        "Identify applicable codes and standards",
                        "Run automated compliance checks",
                        "Generate compliance report",
                        "Prioritize non-conformances",
                        "Track remediation"
                    },
                    EstimatedEffort = TimeSpan.FromHours(4),
                    PotentialSavings = 0,
                    SuccessProbability = 0.95,
                    RiskLevel = RiskLevel.Low
                }
            };
        }

        #endregion

        #region Real-Time Analysis

        /// <summary>
        /// Perform quick model analysis and return immediate recommendations
        /// </summary>
        public async Task<QuickAnalysisResult> QuickAnalyzeAsync(ModelSnapshot snapshot)
        {
            return await Task.Run(() =>
            {
                var result = new QuickAnalysisResult
                {
                    AnalyzedAt = DateTime.UtcNow,
                    Warnings = new List<AnalysisWarning>(),
                    Recommendations = new List<string>(),
                    Metrics = new Dictionary<string, double>()
                };

                // Analyze element counts
                result.Metrics["total_elements"] = snapshot.TotalElements;
                result.Metrics["warning_count"] = snapshot.WarningCount;
                result.Metrics["clash_count"] = snapshot.ClashCount;

                // Generate warnings
                if (snapshot.WarningCount > 100)
                {
                    result.Warnings.Add(new AnalysisWarning
                    {
                        Severity = WarningSeverity.High,
                        Message = $"High warning count: {snapshot.WarningCount} warnings detected",
                        Recommendation = "Run model health analysis immediately"
                    });
                }

                if (snapshot.ClashCount > 50)
                {
                    result.Warnings.Add(new AnalysisWarning
                    {
                        Severity = WarningSeverity.Critical,
                        Message = $"Significant clashes: {snapshot.ClashCount} clashes require attention",
                        Recommendation = "Initiate clash resolution workflow"
                    });
                }

                // Calculate health score
                var healthScore = 100.0;
                healthScore -= Math.Min(30, snapshot.WarningCount / 10.0);
                healthScore -= Math.Min(40, snapshot.ClashCount / 5.0);
                healthScore -= Math.Min(20, snapshot.MissingParameterPercentage);
                result.Metrics["health_score"] = Math.Max(0, healthScore);

                // Generate recommendations
                if (healthScore < 70)
                    result.Recommendations.Add("Model health is below acceptable threshold. Prioritize cleanup.");

                if (snapshot.DemolitionElements > 0)
                    result.Recommendations.Add($"Demolition scope detected: {snapshot.DemolitionElements} elements. Run demolition cost analysis.");

                return result;
            });
        }

        #endregion
    }

    #region Data Models

    public class AnalysisStrategy
    {
        public string StrategyId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<AnalysisStep> Steps { get; set; }
        public TimeSpan ExpectedDuration { get; set; }
        public AnalysisPriority Priority { get; set; }
    }

    public class AnalysisStep
    {
        public int Order { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
    }

    public class ProjectDecisionRule
    {
        public string RuleId { get; set; }
        public string Condition { get; set; }
        public string Recommendation { get; set; }
        public List<string> Actions { get; set; }
        public DecisionPriority Priority { get; set; }
    }

    public class ProjectAnalysisContext
    {
        public string ProjectPhase { get; set; }
        public int LastHealthCheckDays { get; set; }
        public int WarningCount { get; set; }
        public double MissingParameterPercentage { get; set; }
        public int ClashCount { get; set; }
        public double CostVariancePercentage { get; set; }
        public double ScheduleVariancePercentage { get; set; }
        public bool HasDemolitionScope { get; set; }
        public int DemolitionElementCount { get; set; }
        public bool Has4DSchedule { get; set; }
        public bool RequiresCostTracking { get; set; }
        public bool ValueEngineeringRequested { get; set; }
    }

    public class ProjectMetrics
    {
        public int ClashCount { get; set; }
        public decimal CurrentCost { get; set; }
        public decimal Budget { get; set; }
        public double ScheduleVariance { get; set; }
        public double ModelHealthScore { get; set; }
    }

    public class ProjectContext
    {
        public string ProjectId { get; set; }
        public DateTime Timestamp { get; set; }
        public ProjectMetrics Metrics { get; set; }
        public List<string> Actions { get; set; }
    }

    public class AnalysisRecommendation
    {
        public DateTime GeneratedAt { get; set; }
        public ProjectAnalysisContext Context { get; set; }
        public AnalysisStrategy PrimaryRecommendation { get; set; }
        public List<AnalysisStrategy> AlternativeStrategies { get; set; }
        public string Reasoning { get; set; }
        public TimeSpan EstimatedEffort { get; set; }
        public List<StrategyRecommendation> AllRecommendations { get; set; }
    }

    public class StrategyRecommendation
    {
        public AnalysisStrategy Strategy { get; set; }
        public double Score { get; set; }
        public string Reasoning { get; set; }
    }

    public class DecisionRecommendation
    {
        public string RuleId { get; set; }
        public string Recommendation { get; set; }
        public DecisionPriority Priority { get; set; }
        public List<string> Actions { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class ProjectGoal
    {
        public GoalType GoalType { get; set; }
        public decimal TargetValue { get; set; }
        public string Description { get; set; }
        public DateTime TargetDate { get; set; }
    }

    public class RouteAnalysis
    {
        public ProjectGoal Goal { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public List<ProjectRoute> Routes { get; set; }
        public ProjectRoute RecommendedRoute { get; set; }
    }

    public class ProjectRoute
    {
        public string RouteId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Steps { get; set; }
        public TimeSpan EstimatedEffort { get; set; }
        public decimal PotentialSavings { get; set; }
        public double SuccessProbability { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class ModelSnapshot
    {
        public int TotalElements { get; set; }
        public int WarningCount { get; set; }
        public int ClashCount { get; set; }
        public double MissingParameterPercentage { get; set; }
        public int DemolitionElements { get; set; }
    }

    public class QuickAnalysisResult
    {
        public DateTime AnalyzedAt { get; set; }
        public List<AnalysisWarning> Warnings { get; set; }
        public List<string> Recommendations { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
    }

    public class AnalysisWarning
    {
        public WarningSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Recommendation { get; set; }
    }

    public class AdvisoryEventArgs : EventArgs
    {
        public AdvisoryType Type { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum AnalysisPriority { Low, Medium, High, Critical }
    public enum DecisionPriority { Low, Medium, High, Critical }
    public enum GoalType { CostReduction, ScheduleAcceleration, QualityImprovement, ClashResolution, ComplianceAchievement }
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum WarningSeverity { Info, Low, Medium, High, Critical }
    public enum AdvisoryType { AnalysisStrategy, DecisionSupport, Warning, Recommendation }

    #endregion
}
