// =============================================================================
// StingBIM.AI.Collaboration - AI Design Assistant
// Natural language BIM design assistant that understands design intent,
// suggests solutions, and can autonomously create/modify elements
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.DesignAssistant
{
    /// <summary>
    /// AI-powered design assistant that understands natural language requests
    /// and translates them into BIM actions. Learns from user patterns and
    /// project context to provide intelligent suggestions.
    /// </summary>
    public class AIDesignAssistant
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Knowledge bases
        private readonly DesignKnowledgeBase _knowledge = new();
        private readonly ProjectContext _projectContext = new();
        private readonly UserPreferences _userPreferences = new();

        // Intent processing
        private readonly IntentClassifier _intentClassifier = new();
        private readonly EntityExtractor _entityExtractor = new();
        private readonly ActionPlanner _actionPlanner = new();

        // Conversation state
        private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();
        private readonly List<DesignSuggestion> _pendingSuggestions = new();

        // Learning
        private readonly DesignPatternLearner _patternLearner = new();
        private readonly FeedbackProcessor _feedbackProcessor = new();

        // Events
        public event EventHandler<DesignActionEventArgs>? DesignActionProposed;
        public event EventHandler<SuggestionEventArgs>? SuggestionGenerated;
        public event EventHandler<ClarificationEventArgs>? ClarificationNeeded;
        public event EventHandler<DesignInsightEventArgs>? InsightDiscovered;

        #region Natural Language Processing

        /// <summary>
        /// Process a natural language design request
        /// </summary>
        public async Task<AssistantResponse> ProcessRequestAsync(string userId, string request, RequestContext? context = null)
        {
            Logger.Info($"Processing request from {userId}: {request}");

            try
            {
                // Get or create conversation state
                var conversation = _conversations.GetOrAdd(userId, _ => new ConversationState { UserId = userId });
                conversation.AddMessage(new ConversationMessage { Role = "user", Content = request });

                // Classify intent
                var intent = _intentClassifier.Classify(request, conversation);
                Logger.Debug($"Classified intent: {intent.Type} (confidence: {intent.Confidence:P})");

                // Extract entities
                var entities = _entityExtractor.Extract(request, intent, _projectContext);
                Logger.Debug($"Extracted entities: {string.Join(", ", entities.Select(e => $"{e.Type}:{e.Value}"))}");

                // Check if we need clarification
                if (intent.Confidence < 0.7 || entities.Any(e => e.NeedsClarification))
                {
                    var clarification = GenerateClarification(intent, entities, conversation);
                    conversation.AddMessage(new ConversationMessage { Role = "assistant", Content = clarification.Question });
                    ClarificationNeeded?.Invoke(this, new ClarificationEventArgs(clarification));

                    return new AssistantResponse
                    {
                        ResponseType = ResponseType.Clarification,
                        Message = clarification.Question,
                        Suggestions = clarification.Options
                    };
                }

                // Plan actions
                var actions = await _actionPlanner.PlanActionsAsync(intent, entities, _projectContext, _knowledge);

                // Generate response
                var response = await GenerateResponseAsync(intent, entities, actions, conversation);
                conversation.AddMessage(new ConversationMessage { Role = "assistant", Content = response.Message });

                // Propose design actions
                if (actions.Any())
                {
                    foreach (var action in actions)
                    {
                        DesignActionProposed?.Invoke(this, new DesignActionEventArgs(action));
                    }
                }

                // Check for proactive suggestions
                var suggestions = await GenerateProactiveSuggestionsAsync(intent, entities, actions);
                if (suggestions.Any())
                {
                    response.ProactiveSuggestions = suggestions;
                    foreach (var suggestion in suggestions)
                    {
                        SuggestionGenerated?.Invoke(this, new SuggestionEventArgs(suggestion));
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing request");
                return new AssistantResponse
                {
                    ResponseType = ResponseType.Error,
                    Message = "I encountered an error processing your request. Could you try rephrasing it?"
                };
            }
        }

        /// <summary>
        /// Provide feedback on a design action
        /// </summary>
        public void ProvideFeedback(string actionId, FeedbackType feedback, string? comment = null)
        {
            _feedbackProcessor.ProcessFeedback(actionId, feedback, comment);

            // Learn from feedback
            if (feedback == FeedbackType.Positive)
            {
                _patternLearner.ReinforcePatterntFromAction(actionId);
            }
            else if (feedback == FeedbackType.Negative)
            {
                _patternLearner.DiscouragePatterntFromAction(actionId);
            }
        }

        #endregion

        #region Intent Handling

        private Clarification GenerateClarification(DesignIntent intent, List<ExtractedEntity> entities, ConversationState conversation)
        {
            var clarification = new Clarification();

            // Check for ambiguous intent
            if (intent.Confidence < 0.7)
            {
                clarification.Question = "I want to make sure I understand. Are you trying to:";
                clarification.Options = intent.AlternativeIntents
                    .Take(3)
                    .Select(i => new ClarificationOption
                    {
                        Text = DescribeIntent(i),
                        Value = i.Type.ToString()
                    })
                    .ToList();
                return clarification;
            }

            // Check for missing entities
            var missing = entities.Where(e => e.NeedsClarification).ToList();
            if (missing.Any())
            {
                var entity = missing.First();
                clarification.Question = GenerateEntityQuestion(entity, intent);
                clarification.Options = GenerateEntityOptions(entity);
            }

            return clarification;
        }

        private string DescribeIntent(DesignIntent intent)
        {
            return intent.Type switch
            {
                IntentType.CreateElement => $"Create a new {intent.TargetCategory ?? "element"}",
                IntentType.ModifyElement => $"Modify existing {intent.TargetCategory ?? "element"}",
                IntentType.DeleteElement => $"Delete {intent.TargetCategory ?? "element"}",
                IntentType.QueryElement => $"Get information about {intent.TargetCategory ?? "element"}",
                IntentType.LayoutDesign => "Design a layout or arrangement",
                IntentType.OptimizeDesign => "Optimize the design",
                IntentType.ComplianceCheck => "Check compliance",
                IntentType.CostEstimate => "Estimate costs",
                _ => intent.Type.ToString()
            };
        }

        private string GenerateEntityQuestion(ExtractedEntity entity, DesignIntent intent)
        {
            return entity.Type switch
            {
                EntityType.Category => "What type of element should I work with?",
                EntityType.Dimension => $"What {entity.DimensionType} would you like?",
                EntityType.Material => "Which material should I use?",
                EntityType.Location => "Where should this be placed?",
                EntityType.Level => "On which level?",
                EntityType.Quantity => "How many?",
                _ => $"Could you specify the {entity.Type.ToString().ToLower()}?"
            };
        }

        private List<ClarificationOption> GenerateEntityOptions(ExtractedEntity entity)
        {
            return entity.Type switch
            {
                EntityType.Category => _projectContext.AvailableCategories
                    .Take(5)
                    .Select(c => new ClarificationOption { Text = c, Value = c })
                    .ToList(),

                EntityType.Material => _knowledge.GetCommonMaterials(entity.Context)
                    .Take(5)
                    .Select(m => new ClarificationOption { Text = m.Name, Value = m.Id })
                    .ToList(),

                EntityType.Level => _projectContext.Levels
                    .Select(l => new ClarificationOption { Text = l.Name, Value = l.Id })
                    .ToList(),

                _ => new List<ClarificationOption>()
            };
        }

        private async Task<AssistantResponse> GenerateResponseAsync(
            DesignIntent intent,
            List<ExtractedEntity> entities,
            List<DesignAction> actions,
            ConversationState conversation)
        {
            var response = new AssistantResponse
            {
                ResponseType = ResponseType.Success,
                Intent = intent,
                Actions = actions
            };

            // Generate natural language response
            if (actions.Any())
            {
                if (actions.Count == 1)
                {
                    response.Message = DescribeAction(actions.First());
                }
                else
                {
                    response.Message = $"I'll perform {actions.Count} actions:\n" +
                        string.Join("\n", actions.Select((a, i) => $"{i + 1}. {DescribeAction(a)}"));
                }

                // Add confirmation prompt for destructive actions
                if (actions.Any(a => a.IsDestructive))
                {
                    response.RequiresConfirmation = true;
                    response.Message += "\n\nThis will make changes to your model. Should I proceed?";
                }
            }
            else
            {
                response.Message = GenerateInformationalResponse(intent, entities);
            }

            return response;
        }

        private string DescribeAction(DesignAction action)
        {
            return action.Type switch
            {
                DesignActionType.Create =>
                    $"Create a {action.ElementType}{(action.Location != null ? $" at {action.Location}" : "")}",

                DesignActionType.Modify =>
                    $"Modify {action.ElementDescription}: {string.Join(", ", action.Modifications.Select(m => $"{m.Property} to {m.NewValue}"))}",

                DesignActionType.Delete =>
                    $"Delete {action.ElementDescription}",

                DesignActionType.Copy =>
                    $"Copy {action.ElementDescription} to {action.TargetLocation}",

                DesignActionType.Move =>
                    $"Move {action.ElementDescription} to {action.TargetLocation}",

                DesignActionType.Align =>
                    $"Align {action.ElementDescription} with {action.ReferenceElement}",

                _ => action.Type.ToString()
            };
        }

        private string GenerateInformationalResponse(DesignIntent intent, List<ExtractedEntity> entities)
        {
            return intent.Type switch
            {
                IntentType.QueryElement => GenerateElementQueryResponse(entities),
                IntentType.ComplianceCheck => "Running compliance check...",
                IntentType.CostEstimate => "Calculating cost estimate...",
                _ => "I understand your request. How would you like me to proceed?"
            };
        }

        private string GenerateElementQueryResponse(List<ExtractedEntity> entities)
        {
            var category = entities.FirstOrDefault(e => e.Type == EntityType.Category);
            if (category != null)
            {
                var count = _projectContext.GetElementCount(category.Value);
                return $"There are {count} {category.Value} in the current view.";
            }
            return "What would you like to know about?";
        }

        #endregion

        #region Proactive Suggestions

        private async Task<List<DesignSuggestion>> GenerateProactiveSuggestionsAsync(
            DesignIntent intent,
            List<ExtractedEntity> entities,
            List<DesignAction> actions)
        {
            var suggestions = new List<DesignSuggestion>();

            // Pattern-based suggestions
            var patternSuggestions = _patternLearner.GetSuggestionsForContext(intent, entities, _projectContext);
            suggestions.AddRange(patternSuggestions);

            // Compliance suggestions
            if (intent.Type == IntentType.CreateElement || intent.Type == IntentType.ModifyElement)
            {
                var complianceSuggestions = await CheckComplianceProactivelyAsync(actions);
                suggestions.AddRange(complianceSuggestions);
            }

            // Optimization suggestions
            var optimizationSuggestions = await GenerateOptimizationSuggestionsAsync(intent, entities);
            suggestions.AddRange(optimizationSuggestions);

            // Collaboration suggestions
            var collabSuggestions = GenerateCollaborationSuggestions(entities);
            suggestions.AddRange(collabSuggestions);

            return suggestions.Take(3).ToList(); // Limit to top 3
        }

        private async Task<List<DesignSuggestion>> CheckComplianceProactivelyAsync(List<DesignAction> actions)
        {
            var suggestions = new List<DesignSuggestion>();

            foreach (var action in actions.Where(a => a.Type == DesignActionType.Create))
            {
                // Check fire safety
                if (action.ElementType?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new DesignSuggestion
                    {
                        Type = SuggestionType.Compliance,
                        Title = "Fire Rating Check",
                        Description = "Doors in this location may require fire rating. Would you like me to check the requirements?",
                        ConfidenceScore = 0.85,
                        AutoApplicable = false
                    });
                }

                // Check accessibility
                if (action.ElementType?.Contains("corridor", StringComparison.OrdinalIgnoreCase) == true ||
                    action.ElementType?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new DesignSuggestion
                    {
                        Type = SuggestionType.Compliance,
                        Title = "Accessibility Check",
                        Description = "This element affects accessibility paths. Current minimum width requirements apply.",
                        ConfidenceScore = 0.9,
                        AutoApplicable = false
                    });
                }
            }

            return suggestions;
        }

        private async Task<List<DesignSuggestion>> GenerateOptimizationSuggestionsAsync(
            DesignIntent intent,
            List<ExtractedEntity> entities)
        {
            var suggestions = new List<DesignSuggestion>();

            // Material optimization
            var material = entities.FirstOrDefault(e => e.Type == EntityType.Material);
            if (material != null)
            {
                var alternatives = _knowledge.GetSustainableAlternatives(material.Value);
                if (alternatives.Any())
                {
                    suggestions.Add(new DesignSuggestion
                    {
                        Type = SuggestionType.Optimization,
                        Title = "Sustainable Alternative",
                        Description = $"Consider using {alternatives.First().Name} - it has 30% lower embodied carbon.",
                        Data = new { material = alternatives.First() },
                        ConfidenceScore = 0.75,
                        AutoApplicable = true
                    });
                }
            }

            // Cost optimization
            if (intent.Type == IntentType.CreateElement)
            {
                var category = entities.FirstOrDefault(e => e.Type == EntityType.Category);
                if (category != null)
                {
                    var costOptimizations = _knowledge.GetCostOptimizations(category.Value);
                    suggestions.AddRange(costOptimizations.Take(2));
                }
            }

            return suggestions;
        }

        private List<DesignSuggestion> GenerateCollaborationSuggestions(List<ExtractedEntity> entities)
        {
            var suggestions = new List<DesignSuggestion>();

            // Check if working near other users' areas
            var location = entities.FirstOrDefault(e => e.Type == EntityType.Location);
            if (location != null)
            {
                var nearbyUsers = _projectContext.GetUsersNearLocation(location.Value);
                if (nearbyUsers.Any())
                {
                    suggestions.Add(new DesignSuggestion
                    {
                        Type = SuggestionType.Collaboration,
                        Title = "Coordination Notice",
                        Description = $"{nearbyUsers.First()} is working in this area. Consider coordinating your changes.",
                        ConfidenceScore = 0.8,
                        AutoApplicable = false
                    });
                }
            }

            return suggestions;
        }

        #endregion

        #region Design Intelligence

        /// <summary>
        /// Get design recommendations for current context
        /// </summary>
        public async Task<List<DesignRecommendation>> GetRecommendationsAsync(DesignContext context)
        {
            var recommendations = new List<DesignRecommendation>();

            // Spatial analysis recommendations
            if (context.SelectedElements.Any())
            {
                var spatialRecs = await AnalyzeSpatialRelationshipsAsync(context.SelectedElements);
                recommendations.AddRange(spatialRecs);
            }

            // Code compliance recommendations
            var complianceRecs = await CheckCodeComplianceAsync(context);
            recommendations.AddRange(complianceRecs);

            // Performance recommendations
            var perfRecs = await AnalyzePerformanceAsync(context);
            recommendations.AddRange(perfRecs);

            // Cost recommendations
            var costRecs = await AnalyzeCostAsync(context);
            recommendations.AddRange(costRecs);

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        /// <summary>
        /// Analyze what-if scenarios
        /// </summary>
        public async Task<WhatIfAnalysis> AnalyzeWhatIfAsync(string scenario, DesignContext context)
        {
            var analysis = new WhatIfAnalysis { Scenario = scenario };

            // Parse scenario
            var parsed = ParseWhatIfScenario(scenario);

            // Run analysis
            analysis.CostImpact = await CalculateCostImpactAsync(parsed, context);
            analysis.ScheduleImpact = await CalculateScheduleImpactAsync(parsed, context);
            analysis.ComplianceImpact = await CheckComplianceImpactAsync(parsed, context);
            analysis.SustainabilityImpact = await CalculateSustainabilityImpactAsync(parsed, context);

            // Generate recommendation
            analysis.Recommendation = GenerateWhatIfRecommendation(analysis);

            return analysis;
        }

        /// <summary>
        /// Generate design options
        /// </summary>
        public async Task<List<DesignOption>> GenerateDesignOptionsAsync(DesignRequirements requirements)
        {
            var options = new List<DesignOption>();

            // Generate baseline option
            var baseline = await GenerateBaselineDesignAsync(requirements);
            baseline.Name = "Standard Design";
            options.Add(baseline);

            // Generate optimized options
            var costOptimized = await GenerateCostOptimizedDesignAsync(requirements);
            costOptimized.Name = "Cost Optimized";
            options.Add(costOptimized);

            var sustainableOption = await GenerateSustainableDesignAsync(requirements);
            sustainableOption.Name = "Sustainable Design";
            options.Add(sustainableOption);

            var performanceOption = await GeneratePerformanceOptimizedDesignAsync(requirements);
            performanceOption.Name = "Performance Optimized";
            options.Add(performanceOption);

            return options;
        }

        private async Task<List<DesignRecommendation>> AnalyzeSpatialRelationshipsAsync(List<string> elementIds)
        {
            // Analyze spatial relationships and suggest improvements
            return new List<DesignRecommendation>();
        }

        private async Task<List<DesignRecommendation>> CheckCodeComplianceAsync(DesignContext context)
        {
            return new List<DesignRecommendation>();
        }

        private async Task<List<DesignRecommendation>> AnalyzePerformanceAsync(DesignContext context)
        {
            return new List<DesignRecommendation>();
        }

        private async Task<List<DesignRecommendation>> AnalyzeCostAsync(DesignContext context)
        {
            return new List<DesignRecommendation>();
        }

        private WhatIfScenario ParseWhatIfScenario(string scenario)
        {
            return new WhatIfScenario();
        }

        private async Task<CostImpact> CalculateCostImpactAsync(WhatIfScenario scenario, DesignContext context)
        {
            return new CostImpact();
        }

        private async Task<ScheduleImpact> CalculateScheduleImpactAsync(WhatIfScenario scenario, DesignContext context)
        {
            return new ScheduleImpact();
        }

        private async Task<ComplianceImpact> CheckComplianceImpactAsync(WhatIfScenario scenario, DesignContext context)
        {
            return new ComplianceImpact();
        }

        private async Task<SustainabilityImpact> CalculateSustainabilityImpactAsync(WhatIfScenario scenario, DesignContext context)
        {
            return new SustainabilityImpact();
        }

        private string GenerateWhatIfRecommendation(WhatIfAnalysis analysis)
        {
            return "Based on the analysis, this change would be beneficial.";
        }

        private async Task<DesignOption> GenerateBaselineDesignAsync(DesignRequirements requirements)
        {
            return new DesignOption();
        }

        private async Task<DesignOption> GenerateCostOptimizedDesignAsync(DesignRequirements requirements)
        {
            return new DesignOption();
        }

        private async Task<DesignOption> GenerateSustainableDesignAsync(DesignRequirements requirements)
        {
            return new DesignOption();
        }

        private async Task<DesignOption> GeneratePerformanceOptimizedDesignAsync(DesignRequirements requirements)
        {
            return new DesignOption();
        }

        #endregion

        #region Context Management

        /// <summary>
        /// Update project context
        /// </summary>
        public void UpdateProjectContext(ProjectContextUpdate update)
        {
            if (update.CurrentView != null)
                _projectContext.CurrentView = update.CurrentView;

            if (update.SelectedElements != null)
                _projectContext.SelectedElements = update.SelectedElements;

            if (update.ActiveLevel != null)
                _projectContext.ActiveLevel = update.ActiveLevel;

            if (update.ActiveWorkset != null)
                _projectContext.ActiveWorkset = update.ActiveWorkset;

            // Trigger proactive insights
            _ = Task.Run(async () =>
            {
                var insights = await GenerateContextualInsightsAsync();
                foreach (var insight in insights)
                {
                    InsightDiscovered?.Invoke(this, new DesignInsightEventArgs(insight));
                }
            });
        }

        private async Task<List<DesignInsight>> GenerateContextualInsightsAsync()
        {
            var insights = new List<DesignInsight>();

            // Check for potential issues in current view
            // Check for optimization opportunities
            // Check for collaboration opportunities

            return insights;
        }

        #endregion
    }

    #region Supporting Classes

    public class IntentClassifier
    {
        public DesignIntent Classify(string text, ConversationState conversation)
        {
            var lower = text.ToLower();

            // Simple pattern matching - in production would use ML model
            if (Regex.IsMatch(lower, @"(create|add|place|insert|put|make)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.CreateElement,
                    Confidence = 0.9,
                    TargetCategory = ExtractCategory(lower)
                };
            }

            if (Regex.IsMatch(lower, @"(change|modify|edit|update|set|adjust)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.ModifyElement,
                    Confidence = 0.85
                };
            }

            if (Regex.IsMatch(lower, @"(delete|remove|get rid of)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.DeleteElement,
                    Confidence = 0.9
                };
            }

            if (Regex.IsMatch(lower, @"(how many|what is|show me|find|count|list)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.QueryElement,
                    Confidence = 0.85
                };
            }

            if (Regex.IsMatch(lower, @"(layout|arrange|organize|design)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.LayoutDesign,
                    Confidence = 0.8
                };
            }

            if (Regex.IsMatch(lower, @"(optimize|improve|better|efficient)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.OptimizeDesign,
                    Confidence = 0.8
                };
            }

            if (Regex.IsMatch(lower, @"(check|comply|code|regulation|standard)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.ComplianceCheck,
                    Confidence = 0.85
                };
            }

            if (Regex.IsMatch(lower, @"(cost|price|budget|estimate|how much)"))
            {
                return new DesignIntent
                {
                    Type = IntentType.CostEstimate,
                    Confidence = 0.85
                };
            }

            return new DesignIntent
            {
                Type = IntentType.Unknown,
                Confidence = 0.3
            };
        }

        private string? ExtractCategory(string text)
        {
            var categories = new[] { "wall", "door", "window", "floor", "ceiling", "roof",
                "column", "beam", "pipe", "duct", "room", "stair", "furniture" };

            foreach (var cat in categories)
            {
                if (text.Contains(cat)) return cat;
            }
            return null;
        }
    }

    public class EntityExtractor
    {
        public List<ExtractedEntity> Extract(string text, DesignIntent intent, ProjectContext context)
        {
            var entities = new List<ExtractedEntity>();
            var lower = text.ToLower();

            // Extract dimensions
            var dimMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*(m|meter|cm|mm|ft|feet|inch|in)");
            if (dimMatch.Success)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = EntityType.Dimension,
                    Value = dimMatch.Groups[1].Value,
                    Unit = dimMatch.Groups[2].Value
                });
            }

            // Extract quantities
            var qtyMatch = Regex.Match(text, @"(\d+)\s*(walls?|doors?|windows?|columns?|beams?)");
            if (qtyMatch.Success)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = EntityType.Quantity,
                    Value = qtyMatch.Groups[1].Value
                });
            }

            // Extract levels
            var levelMatch = Regex.Match(lower, @"level\s*(\d+|ground|basement|roof)");
            if (levelMatch.Success)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = EntityType.Level,
                    Value = levelMatch.Groups[1].Value
                });
            }

            // Extract materials
            var materials = new[] { "concrete", "steel", "wood", "glass", "brick", "gypsum", "aluminum" };
            foreach (var mat in materials)
            {
                if (lower.Contains(mat))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = EntityType.Material,
                        Value = mat
                    });
                    break;
                }
            }

            // Extract direction/location
            var directions = new[] { "left", "right", "north", "south", "east", "west", "above", "below", "next to" };
            foreach (var dir in directions)
            {
                if (lower.Contains(dir))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Type = EntityType.Location,
                        Value = dir
                    });
                    break;
                }
            }

            return entities;
        }
    }

    public class ActionPlanner
    {
        public async Task<List<DesignAction>> PlanActionsAsync(
            DesignIntent intent,
            List<ExtractedEntity> entities,
            ProjectContext context,
            DesignKnowledgeBase knowledge)
        {
            var actions = new List<DesignAction>();

            switch (intent.Type)
            {
                case IntentType.CreateElement:
                    actions.Add(new DesignAction
                    {
                        ActionId = Guid.NewGuid().ToString(),
                        Type = DesignActionType.Create,
                        ElementType = intent.TargetCategory,
                        Parameters = entities.ToDictionary(e => e.Type.ToString(), e => (object)e.Value),
                        Level = entities.FirstOrDefault(e => e.Type == EntityType.Level)?.Value ?? context.ActiveLevel
                    });
                    break;

                case IntentType.ModifyElement:
                    actions.Add(new DesignAction
                    {
                        ActionId = Guid.NewGuid().ToString(),
                        Type = DesignActionType.Modify,
                        ElementIds = context.SelectedElements,
                        Modifications = ExtractModifications(entities)
                    });
                    break;

                case IntentType.DeleteElement:
                    actions.Add(new DesignAction
                    {
                        ActionId = Guid.NewGuid().ToString(),
                        Type = DesignActionType.Delete,
                        ElementIds = context.SelectedElements,
                        IsDestructive = true
                    });
                    break;
            }

            return actions;
        }

        private List<PropertyModification> ExtractModifications(List<ExtractedEntity> entities)
        {
            var mods = new List<PropertyModification>();

            var dim = entities.FirstOrDefault(e => e.Type == EntityType.Dimension);
            if (dim != null)
            {
                mods.Add(new PropertyModification
                {
                    Property = dim.DimensionType ?? "Width",
                    NewValue = dim.Value
                });
            }

            var mat = entities.FirstOrDefault(e => e.Type == EntityType.Material);
            if (mat != null)
            {
                mods.Add(new PropertyModification
                {
                    Property = "Material",
                    NewValue = mat.Value
                });
            }

            return mods;
        }
    }

    public class DesignKnowledgeBase
    {
        public IEnumerable<MaterialInfo> GetCommonMaterials(string? context)
        {
            return new List<MaterialInfo>
            {
                new MaterialInfo { Id = "concrete", Name = "Concrete" },
                new MaterialInfo { Id = "steel", Name = "Steel" },
                new MaterialInfo { Id = "wood", Name = "Wood" },
                new MaterialInfo { Id = "glass", Name = "Glass" }
            };
        }

        public IEnumerable<MaterialInfo> GetSustainableAlternatives(string material)
        {
            return material.ToLower() switch
            {
                "concrete" => new[] { new MaterialInfo { Id = "recycled_concrete", Name = "Recycled Concrete" } },
                "steel" => new[] { new MaterialInfo { Id = "recycled_steel", Name = "Recycled Steel" } },
                _ => Enumerable.Empty<MaterialInfo>()
            };
        }

        public IEnumerable<DesignSuggestion> GetCostOptimizations(string category)
        {
            return new List<DesignSuggestion>();
        }
    }

    public class ProjectContext
    {
        public string? CurrentView { get; set; }
        public string? ActiveLevel { get; set; }
        public string? ActiveWorkset { get; set; }
        public List<string> SelectedElements { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new() { "Walls", "Doors", "Windows", "Floors", "Ceilings" };
        public List<LevelInfo> Levels { get; set; } = new();

        public int GetElementCount(string category) => 0;
        public List<string> GetUsersNearLocation(string location) => new();
    }

    public class UserPreferences
    {
        public Dictionary<string, int> PreferredMaterials { get; set; } = new();
        public Dictionary<string, double> CategoryWeights { get; set; } = new();
    }

    public class DesignPatternLearner
    {
        public List<DesignSuggestion> GetSuggestionsForContext(DesignIntent intent, List<ExtractedEntity> entities, ProjectContext context)
        {
            return new List<DesignSuggestion>();
        }

        public void ReinforcePatterntFromAction(string actionId) { }
        public void DiscouragePatterntFromAction(string actionId) { }
    }

    public class FeedbackProcessor
    {
        public void ProcessFeedback(string actionId, FeedbackType feedback, string? comment) { }
    }

    #endregion

    #region Data Models

    public class DesignIntent
    {
        public IntentType Type { get; set; }
        public double Confidence { get; set; }
        public string? TargetCategory { get; set; }
        public List<DesignIntent> AlternativeIntents { get; set; } = new();
    }

    public enum IntentType
    {
        Unknown,
        CreateElement,
        ModifyElement,
        DeleteElement,
        QueryElement,
        LayoutDesign,
        OptimizeDesign,
        ComplianceCheck,
        CostEstimate
    }

    public class ExtractedEntity
    {
        public EntityType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public string? DimensionType { get; set; }
        public string? Context { get; set; }
        public bool NeedsClarification { get; set; }
    }

    public enum EntityType
    {
        Category,
        Dimension,
        Material,
        Location,
        Level,
        Quantity,
        ElementReference
    }

    public class ConversationState
    {
        public string UserId { get; set; } = string.Empty;
        public List<ConversationMessage> Messages { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();

        public void AddMessage(ConversationMessage message)
        {
            Messages.Add(message);
            if (Messages.Count > 20) Messages.RemoveAt(0);
        }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AssistantResponse
    {
        public ResponseType ResponseType { get; set; }
        public string Message { get; set; } = string.Empty;
        public DesignIntent? Intent { get; set; }
        public List<DesignAction> Actions { get; set; } = new();
        public List<string>? Suggestions { get; set; }
        public List<DesignSuggestion>? ProactiveSuggestions { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public enum ResponseType
    {
        Success,
        Clarification,
        Error,
        Information
    }

    public class DesignAction
    {
        public string ActionId { get; set; } = string.Empty;
        public DesignActionType Type { get; set; }
        public string? ElementType { get; set; }
        public string? ElementDescription { get; set; }
        public List<string>? ElementIds { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public List<PropertyModification> Modifications { get; set; } = new();
        public string? Location { get; set; }
        public string? TargetLocation { get; set; }
        public string? ReferenceElement { get; set; }
        public string? Level { get; set; }
        public bool IsDestructive { get; set; }
    }

    public enum DesignActionType
    {
        Create,
        Modify,
        Delete,
        Copy,
        Move,
        Align,
        Array,
        Mirror
    }

    public class PropertyModification
    {
        public string Property { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    public class Clarification
    {
        public string Question { get; set; } = string.Empty;
        public List<ClarificationOption> Options { get; set; } = new();
    }

    public class ClarificationOption
    {
        public string Text { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class DesignSuggestion
    {
        public string SuggestionId { get; set; } = Guid.NewGuid().ToString();
        public SuggestionType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object? Data { get; set; }
        public double ConfidenceScore { get; set; }
        public bool AutoApplicable { get; set; }
    }

    public enum SuggestionType
    {
        Compliance,
        Optimization,
        Collaboration,
        Pattern,
        Cost,
        Sustainability
    }

    public class RequestContext
    {
        public string? ViewId { get; set; }
        public List<string>? SelectedElements { get; set; }
        public object? CursorPosition { get; set; }
    }

    public class MaterialInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class LevelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Elevation { get; set; }
    }

    public class ProjectContextUpdate
    {
        public string? CurrentView { get; set; }
        public List<string>? SelectedElements { get; set; }
        public string? ActiveLevel { get; set; }
        public string? ActiveWorkset { get; set; }
    }

    public class DesignContext
    {
        public List<string> SelectedElements { get; set; } = new();
    }

    public class DesignRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    public class WhatIfAnalysis
    {
        public string Scenario { get; set; } = string.Empty;
        public CostImpact? CostImpact { get; set; }
        public ScheduleImpact? ScheduleImpact { get; set; }
        public ComplianceImpact? ComplianceImpact { get; set; }
        public SustainabilityImpact? SustainabilityImpact { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class WhatIfScenario { }
    public class CostImpact { public decimal Delta { get; set; } }
    public class ScheduleImpact { public int DaysDelta { get; set; } }
    public class ComplianceImpact { public bool PassesCompliance { get; set; } }
    public class SustainabilityImpact { public double CarbonDelta { get; set; } }

    public class DesignRequirements { }

    public class DesignOption
    {
        public string Name { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public int EstimatedDuration { get; set; }
        public double SustainabilityScore { get; set; }
    }

    public class DesignInsight
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InsightType Type { get; set; }
    }

    public enum InsightType
    {
        Opportunity,
        Warning,
        Information
    }

    public enum FeedbackType
    {
        Positive,
        Negative,
        Neutral
    }

    #endregion

    #region Event Args

    public class DesignActionEventArgs : EventArgs
    {
        public DesignAction Action { get; }
        public DesignActionEventArgs(DesignAction action) => Action = action;
    }

    public class SuggestionEventArgs : EventArgs
    {
        public DesignSuggestion Suggestion { get; }
        public SuggestionEventArgs(DesignSuggestion suggestion) => Suggestion = suggestion;
    }

    public class ClarificationEventArgs : EventArgs
    {
        public Clarification Clarification { get; }
        public ClarificationEventArgs(Clarification clarification) => Clarification = clarification;
    }

    public class DesignInsightEventArgs : EventArgs
    {
        public DesignInsight Insight { get; }
        public DesignInsightEventArgs(DesignInsight insight) => Insight = insight;
    }

    #endregion
}
