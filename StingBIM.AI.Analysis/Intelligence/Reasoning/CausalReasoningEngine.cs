// StingBIM.AI.Intelligence.Reasoning.CausalReasoningEngine
// Causal reasoning for understanding cause-effect chains in BIM design
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Enhancement

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Causal Graph

    /// <summary>
    /// Represents a cause-effect relationship in the causal graph.
    /// </summary>
    public class CausalRelationship
    {
        public string CauseId { get; set; }
        public string Cause { get; set; }
        public string Effect { get; set; }
        public string EffectId { get; set; }
        public float Strength { get; set; }
        public bool Reversible { get; set; }
        public string TimeToManifest { get; set; }
        public string Category { get; set; }
        public string Domain { get; set; }
        public string Description { get; set; }
        public string MitigationStrategy { get; set; }
    }

    /// <summary>
    /// A node in the causal graph representing a state or condition.
    /// </summary>
    public class CausalNode
    {
        public string NodeId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Domain { get; set; }
        public List<CausalEdge> Causes { get; set; } = new List<CausalEdge>();
        public List<CausalEdge> Effects { get; set; } = new List<CausalEdge>();
        public float CurrentProbability { get; set; }
        public bool IsObserved { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// An edge in the causal graph representing a causal link.
    /// </summary>
    public class CausalEdge
    {
        public CausalNode FromNode { get; set; }
        public CausalNode ToNode { get; set; }
        public float Strength { get; set; }
        public bool Reversible { get; set; }
        public string TimeToManifest { get; set; }
        public string MitigationStrategy { get; set; }
    }

    /// <summary>
    /// The causal graph containing all cause-effect relationships.
    /// </summary>
    public class CausalGraph
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, CausalNode> _nodes;
        private readonly List<CausalEdge> _edges;

        public CausalGraph()
        {
            _nodes = new Dictionary<string, CausalNode>();
            _edges = new List<CausalEdge>();
        }

        /// <summary>
        /// Loads causal relationships from CSV data.
        /// </summary>
        public void LoadFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Logger.Warn($"Causal relationships file not found: {csvPath}");
                return;
            }

            var lines = File.ReadAllLines(csvPath).Skip(1); // Skip header
            foreach (var line in lines)
            {
                var parts = ParseCsvLine(line);
                if (parts.Count < 11) continue;

                var relationship = new CausalRelationship
                {
                    CauseId = parts[0],
                    Cause = parts[1],
                    Effect = parts[2],
                    EffectId = parts[3],
                    Strength = float.TryParse(parts[4], out var s) ? s : 0.5f,
                    Reversible = parts[5].ToLower() == "true",
                    TimeToManifest = parts[6],
                    Category = parts[7],
                    Domain = parts[8],
                    Description = parts[9],
                    MitigationStrategy = parts.Count > 10 ? parts[10] : ""
                };

                AddRelationship(relationship);
            }

            Logger.Info($"Loaded {_nodes.Count} causal nodes and {_edges.Count} edges");
        }

        /// <summary>
        /// Parses a CSV line handling quoted fields that may contain commas.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            int fieldStart = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (line[i] == ',' && !inQuotes)
                {
                    fields.Add(ExtractField(line, fieldStart, i));
                    fieldStart = i + 1;
                }
            }

            fields.Add(ExtractField(line, fieldStart, line.Length));
            return fields;
        }

        private static string ExtractField(string line, int start, int end)
        {
            var field = line.Substring(start, end - start).Trim();
            if (field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"')
                field = field.Substring(1, field.Length - 2).Replace("\"\"", "\"");
            return field;
        }

        /// <summary>
        /// Adds a causal relationship to the graph.
        /// </summary>
        public void AddRelationship(CausalRelationship relationship)
        {
            // Get or create cause node
            if (!_nodes.TryGetValue(relationship.Cause, out var causeNode))
            {
                causeNode = new CausalNode
                {
                    NodeId = relationship.CauseId,
                    Name = relationship.Cause,
                    Category = relationship.Category,
                    Domain = relationship.Domain
                };
                _nodes[relationship.Cause] = causeNode;
            }

            // Get or create effect node
            if (!_nodes.TryGetValue(relationship.Effect, out var effectNode))
            {
                effectNode = new CausalNode
                {
                    NodeId = relationship.EffectId,
                    Name = relationship.Effect,
                    Category = relationship.Category,
                    Domain = relationship.Domain
                };
                _nodes[relationship.Effect] = effectNode;
            }

            // Create edge
            var edge = new CausalEdge
            {
                FromNode = causeNode,
                ToNode = effectNode,
                Strength = relationship.Strength,
                Reversible = relationship.Reversible,
                TimeToManifest = relationship.TimeToManifest,
                MitigationStrategy = relationship.MitigationStrategy
            };

            causeNode.Effects.Add(edge);
            effectNode.Causes.Add(edge);
            _edges.Add(edge);
        }

        /// <summary>
        /// Gets a node by name.
        /// </summary>
        public CausalNode GetNode(string name) =>
            _nodes.TryGetValue(name, out var node) ? node : null;

        /// <summary>
        /// Gets all nodes in a category.
        /// </summary>
        public IEnumerable<CausalNode> GetNodesByCategory(string category) =>
            _nodes.Values.Where(n => n.Category == category);

        /// <summary>
        /// Gets all root causes (nodes with no incoming edges).
        /// </summary>
        public IEnumerable<CausalNode> GetRootCauses() =>
            _nodes.Values.Where(n => n.Causes.Count == 0);

        /// <summary>
        /// Gets all terminal effects (nodes with no outgoing edges).
        /// </summary>
        public IEnumerable<CausalNode> GetTerminalEffects() =>
            _nodes.Values.Where(n => n.Effects.Count == 0);

        public IReadOnlyDictionary<string, CausalNode> Nodes => _nodes;
        public IReadOnlyList<CausalEdge> Edges => _edges;
    }

    #endregion

    #region Causal Reasoning Engine

    /// <summary>
    /// Engine for causal reasoning - predicting effects, finding causes, and suggesting mitigations.
    /// </summary>
    public class CausalReasoningEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly CausalGraph _graph;

        // Bayesian inference state (T2-8)
        private readonly Dictionary<string, float> _priors;
        private readonly Dictionary<string, float> _posteriors;
        private readonly Dictionary<string, bool> _evidence;

        public CausalReasoningEngine()
        {
            _graph = new CausalGraph();
            _priors = new Dictionary<string, float>();
            _posteriors = new Dictionary<string, float>();
            _evidence = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Loads the causal knowledge base.
        /// </summary>
        public void LoadKnowledgeBase(string csvPath)
        {
            _graph.LoadFromCsv(csvPath);
        }

        /// <summary>
        /// Predicts all downstream effects of a given condition.
        /// </summary>
        public CausalChain PredictEffects(string condition, float minStrength = 0.5f, int maxDepth = 5)
        {
            var chain = new CausalChain
            {
                RootCause = condition,
                PredictedEffects = new List<PredictedEffect>()
            };

            var startNode = _graph.GetNode(condition);
            if (startNode == null)
            {
                Logger.Debug($"Condition not found in causal graph: {condition}");
                return chain;
            }

            var visited = new HashSet<string>();
            var queue = new Queue<(CausalNode Node, float CumulativeStrength, int Depth, List<string> Path)>();
            queue.Enqueue((startNode, 1.0f, 0, new List<string> { condition }));

            while (queue.Count > 0)
            {
                var (node, strength, depth, path) = queue.Dequeue();

                if (depth >= maxDepth || visited.Contains(node.Name))
                    continue;

                visited.Add(node.Name);

                foreach (var edge in node.Effects)
                {
                    var effectStrength = strength * edge.Strength;
                    if (effectStrength < minStrength)
                        continue;

                    var newPath = new List<string>(path) { edge.ToNode.Name };

                    chain.PredictedEffects.Add(new PredictedEffect
                    {
                        Effect = edge.ToNode.Name,
                        Probability = effectStrength,
                        CausalPath = newPath,
                        TimeToManifest = edge.TimeToManifest,
                        Category = edge.ToNode.Category,
                        Domain = edge.ToNode.Domain,
                        IsReversible = edge.Reversible,
                        MitigationStrategy = edge.MitigationStrategy,
                        Depth = depth + 1
                    });

                    queue.Enqueue((edge.ToNode, effectStrength, depth + 1, newPath));
                }
            }

            // Sort by probability descending
            chain.PredictedEffects = chain.PredictedEffects
                .OrderByDescending(e => e.Probability)
                .ToList();

            chain.TotalEffects = chain.PredictedEffects.Count;
            chain.CriticalEffects = chain.PredictedEffects.Count(e => e.Probability > 0.8f);

            return chain;
        }

        /// <summary>
        /// Traces back to find root causes of an observed effect.
        /// </summary>
        public RootCauseAnalysis FindRootCauses(string observedEffect, int maxDepth = 5)
        {
            var analysis = new RootCauseAnalysis
            {
                ObservedEffect = observedEffect,
                PotentialCauses = new List<PotentialCause>()
            };

            var effectNode = _graph.GetNode(observedEffect);
            if (effectNode == null)
            {
                Logger.Debug($"Effect not found in causal graph: {observedEffect}");
                return analysis;
            }

            var visited = new HashSet<string>();
            var queue = new Queue<(CausalNode Node, float CumulativeStrength, int Depth, List<string> Path)>();
            queue.Enqueue((effectNode, 1.0f, 0, new List<string> { observedEffect }));

            while (queue.Count > 0)
            {
                var (node, strength, depth, path) = queue.Dequeue();

                if (depth >= maxDepth || visited.Contains(node.Name))
                    continue;

                visited.Add(node.Name);

                // If this node has no causes, it's a root cause
                if (node.Causes.Count == 0)
                {
                    analysis.PotentialCauses.Add(new PotentialCause
                    {
                        Cause = node.Name,
                        Likelihood = strength,
                        CausalPath = new List<string>(path),
                        Category = node.Category,
                        Domain = node.Domain,
                        IsRootCause = true,
                        Depth = depth
                    });
                }

                foreach (var edge in node.Causes)
                {
                    var causeStrength = strength * edge.Strength;
                    var newPath = new List<string> { edge.FromNode.Name };
                    newPath.AddRange(path);

                    // Add intermediate causes too
                    analysis.PotentialCauses.Add(new PotentialCause
                    {
                        Cause = edge.FromNode.Name,
                        Likelihood = causeStrength,
                        CausalPath = newPath,
                        Category = edge.FromNode.Category,
                        Domain = edge.FromNode.Domain,
                        IsRootCause = edge.FromNode.Causes.Count == 0,
                        Depth = depth + 1
                    });

                    queue.Enqueue((edge.FromNode, causeStrength, depth + 1, newPath));
                }
            }

            // Sort by likelihood descending
            analysis.PotentialCauses = analysis.PotentialCauses
                .GroupBy(c => c.Cause)
                .Select(g => g.OrderByDescending(c => c.Likelihood).First())
                .OrderByDescending(c => c.Likelihood)
                .ToList();

            analysis.MostLikelyCause = analysis.PotentialCauses.FirstOrDefault()?.Cause;

            return analysis;
        }

        /// <summary>
        /// Analyzes a design decision and predicts its consequences.
        /// </summary>
        public DesignDecisionAnalysis AnalyzeDecision(DesignDecision decision)
        {
            var analysis = new DesignDecisionAnalysis
            {
                Decision = decision,
                PositiveEffects = new List<PredictedEffect>(),
                NegativeEffects = new List<PredictedEffect>(),
                Mitigations = new List<MitigationSuggestion>()
            };

            // Map decision to causal conditions
            var conditions = MapDecisionToConditions(decision);

            foreach (var condition in conditions)
            {
                var chain = PredictEffects(condition.Condition, 0.4f);

                foreach (var effect in chain.PredictedEffects)
                {
                    // Classify as positive or negative based on category
                    if (IsNegativeOutcome(effect))
                    {
                        var adjustedProbability = effect.Probability * condition.Relevance;
                        effect.Probability = adjustedProbability;
                        analysis.NegativeEffects.Add(effect);

                        // Add mitigation if available
                        if (!string.IsNullOrEmpty(effect.MitigationStrategy))
                        {
                            analysis.Mitigations.Add(new MitigationSuggestion
                            {
                                ForEffect = effect.Effect,
                                Strategy = effect.MitigationStrategy,
                                ReducesRiskBy = 0.5f,
                                Effort = EstimateMitigationEffort(effect.MitigationStrategy)
                            });
                        }
                    }
                    else
                    {
                        analysis.PositiveEffects.Add(effect);
                    }
                }
            }

            // Calculate risk score
            analysis.OverallRiskScore = analysis.NegativeEffects.Any()
                ? analysis.NegativeEffects.Max(e => e.Probability)
                : 0f;

            analysis.RecommendedAction = analysis.OverallRiskScore > 0.7f
                ? "Reconsider decision or implement mitigations"
                : analysis.OverallRiskScore > 0.4f
                    ? "Proceed with caution, monitor effects"
                    : "Acceptable risk level";

            return analysis;
        }

        /// <summary>
        /// Performs "what-if" analysis for a proposed change.
        /// </summary>
        public WhatIfAnalysis AnalyzeWhatIf(string proposedChange, Dictionary<string, object> parameters)
        {
            var analysis = new WhatIfAnalysis
            {
                ProposedChange = proposedChange,
                Parameters = parameters,
                Scenarios = new List<WhatIfScenario>()
            };

            // Generate scenarios based on the change
            var baseConditions = MapChangeToConditions(proposedChange, parameters);

            // Best case scenario
            var bestCase = new WhatIfScenario { ScenarioName = "Best Case", Probability = 0.2f };
            foreach (var condition in baseConditions.Where(c => !c.IsNegative))
            {
                var effects = PredictEffects(condition.Condition, 0.6f, 3);
                bestCase.Effects.AddRange(effects.PredictedEffects.Where(e => !IsNegativeOutcome(e)));
            }
            analysis.Scenarios.Add(bestCase);

            // Expected case scenario
            var expectedCase = new WhatIfScenario { ScenarioName = "Expected Case", Probability = 0.6f };
            foreach (var condition in baseConditions)
            {
                var effects = PredictEffects(condition.Condition, 0.5f, 4);
                expectedCase.Effects.AddRange(effects.PredictedEffects);
            }
            analysis.Scenarios.Add(expectedCase);

            // Worst case scenario
            var worstCase = new WhatIfScenario { ScenarioName = "Worst Case", Probability = 0.2f };
            foreach (var condition in baseConditions.Where(c => c.IsNegative))
            {
                var effects = PredictEffects(condition.Condition, 0.3f, 5);
                worstCase.Effects.AddRange(effects.PredictedEffects.Where(e => IsNegativeOutcome(e)));
            }
            analysis.Scenarios.Add(worstCase);

            return analysis;
        }

        #region Bayesian Inference (T2-8)

        /// <summary>
        /// Sets the prior probability for a node in the causal graph.
        /// If not set, defaults to 0.5 (maximum uncertainty).
        /// </summary>
        public void SetPrior(string nodeName, float probability)
        {
            _priors[nodeName] = Math.Max(0.001f, Math.Min(0.999f, probability));
        }

        /// <summary>
        /// Records observed evidence: a node is known to be true or false.
        /// Call ComputePosteriors() after setting all evidence.
        /// </summary>
        public void ObserveEvidence(string nodeName, bool isTrue)
        {
            _evidence[nodeName] = isTrue;
            Logger.Debug($"Evidence observed: {nodeName} = {isTrue}");
        }

        /// <summary>
        /// Clears all observed evidence and computed posteriors.
        /// </summary>
        public void ClearEvidence()
        {
            _evidence.Clear();
            _posteriors.Clear();
        }

        /// <summary>
        /// Computes posterior probabilities for all nodes given observed evidence.
        /// Uses Bayesian belief propagation: P(H|E) = P(E|H) * P(H) / P(E)
        /// where edge strengths serve as conditional probabilities P(effect|cause).
        /// </summary>
        public BayesianAnalysisResult ComputePosteriors()
        {
            _posteriors.Clear();

            // Initialize posteriors from priors
            foreach (var node in _graph.Nodes.Values)
            {
                _posteriors[node.Name] = GetPrior(node.Name);
            }

            // Set observed evidence to certainty
            foreach (var (nodeName, isTrue) in _evidence)
            {
                _posteriors[nodeName] = isTrue ? 0.999f : 0.001f;
            }

            // Forward propagation: update children of observed nodes
            // Iterate until convergence (max 10 iterations to prevent infinite loops)
            for (int iteration = 0; iteration < 10; iteration++)
            {
                float maxChange = 0f;

                foreach (var node in _graph.Nodes.Values)
                {
                    if (_evidence.ContainsKey(node.Name))
                        continue; // Evidence nodes are fixed

                    if (node.Causes.Count == 0)
                        continue; // Root nodes keep their priors

                    // Noisy-OR combination: P(effect) = 1 - ∏(1 - P(cause_i) * strength_i)
                    float probNotCaused = 1.0f;
                    foreach (var causeEdge in node.Causes)
                    {
                        var causeProb = _posteriors.TryGetValue(causeEdge.FromNode.Name, out var cp) ? cp : GetPrior(causeEdge.FromNode.Name);
                        probNotCaused *= (1.0f - causeProb * causeEdge.Strength);
                    }

                    var newPosterior = 1.0f - probNotCaused;

                    // Blend with prior using a damping factor for stability
                    var prior = GetPrior(node.Name);
                    newPosterior = 0.7f * newPosterior + 0.3f * prior;
                    newPosterior = Math.Max(0.001f, Math.Min(0.999f, newPosterior));

                    var change = Math.Abs(newPosterior - _posteriors.GetValueOrDefault(node.Name, prior));
                    maxChange = Math.Max(maxChange, change);
                    _posteriors[node.Name] = newPosterior;
                }

                // Backward propagation: update causes based on observed effects
                foreach (var node in _graph.Nodes.Values)
                {
                    if (_evidence.ContainsKey(node.Name))
                        continue;

                    if (node.Effects.Count == 0)
                        continue;

                    // If any effect is observed true, boost this cause
                    // P(cause|effect) ∝ P(effect|cause) * P(cause)
                    float bayesianBoost = 0f;
                    int observedEffects = 0;

                    foreach (var effectEdge in node.Effects)
                    {
                        if (_evidence.TryGetValue(effectEdge.ToNode.Name, out var observed) && observed)
                        {
                            // Bayes: P(cause|effect) = P(effect|cause) * P(cause) / P(effect)
                            var likelihood = effectEdge.Strength;
                            var priorCause = GetPrior(node.Name);
                            var priorEffect = _posteriors.GetValueOrDefault(effectEdge.ToNode.Name, 0.5f);

                            if (priorEffect > 0.001f)
                            {
                                bayesianBoost += (likelihood * priorCause) / priorEffect;
                            }
                            observedEffects++;
                        }
                    }

                    if (observedEffects > 0)
                    {
                        var avgBoost = bayesianBoost / observedEffects;
                        var currentPosterior = _posteriors[node.Name];
                        var boostedPosterior = Math.Max(currentPosterior, Math.Min(0.999f, avgBoost));
                        var change = Math.Abs(boostedPosterior - currentPosterior);
                        maxChange = Math.Max(maxChange, change);
                        _posteriors[node.Name] = boostedPosterior;
                    }
                }

                if (maxChange < 0.001f)
                {
                    Logger.Debug($"Bayesian inference converged after {iteration + 1} iterations");
                    break;
                }
            }

            // Build result
            var result = new BayesianAnalysisResult
            {
                Evidence = new Dictionary<string, bool>(_evidence),
                Posteriors = new Dictionary<string, float>(_posteriors),
                MostLikelyHypotheses = _posteriors
                    .Where(p => !_evidence.ContainsKey(p.Key))
                    .OrderByDescending(p => p.Value)
                    .Take(10)
                    .Select(p => new BayesianHypothesis
                    {
                        NodeName = p.Key,
                        PriorProbability = GetPrior(p.Key),
                        PosteriorProbability = p.Value,
                        ProbabilityShift = p.Value - GetPrior(p.Key)
                    })
                    .ToList(),
                HighestRiskNodes = _posteriors
                    .Where(p => !_evidence.ContainsKey(p.Key) && p.Value > 0.6f)
                    .OrderByDescending(p => p.Value)
                    .Select(p => p.Key)
                    .ToList()
            };

            Logger.Info($"Bayesian analysis: {_evidence.Count} evidence nodes, {result.HighestRiskNodes.Count} high-risk nodes");
            return result;
        }

        /// <summary>
        /// Predicts effects using Bayesian posterior probabilities instead of raw edge strengths.
        /// Requires ComputePosteriors() to have been called first.
        /// </summary>
        public CausalChain PredictEffectsWithBayesianBeliefs(string condition, float minPosterior = 0.3f, int maxDepth = 5)
        {
            // Ensure posteriors are computed
            if (_posteriors.Count == 0)
            {
                ComputePosteriors();
            }

            var chain = new CausalChain
            {
                RootCause = condition,
                PredictedEffects = new List<PredictedEffect>()
            };

            var startNode = _graph.GetNode(condition);
            if (startNode == null) return chain;

            var visited = new HashSet<string>();
            var queue = new Queue<(CausalNode Node, int Depth, List<string> Path)>();
            queue.Enqueue((startNode, 0, new List<string> { condition }));

            while (queue.Count > 0)
            {
                var (node, depth, path) = queue.Dequeue();
                if (depth >= maxDepth || visited.Contains(node.Name))
                    continue;

                visited.Add(node.Name);

                foreach (var edge in node.Effects)
                {
                    var posterior = _posteriors.GetValueOrDefault(edge.ToNode.Name, GetPrior(edge.ToNode.Name));
                    if (posterior < minPosterior)
                        continue;

                    var newPath = new List<string>(path) { edge.ToNode.Name };
                    chain.PredictedEffects.Add(new PredictedEffect
                    {
                        Effect = edge.ToNode.Name,
                        Probability = posterior,
                        CausalPath = newPath,
                        TimeToManifest = edge.TimeToManifest,
                        Category = edge.ToNode.Category,
                        Domain = edge.ToNode.Domain,
                        IsReversible = edge.Reversible,
                        MitigationStrategy = edge.MitigationStrategy,
                        Depth = depth + 1
                    });

                    queue.Enqueue((edge.ToNode, depth + 1, newPath));
                }
            }

            chain.PredictedEffects = chain.PredictedEffects.OrderByDescending(e => e.Probability).ToList();
            chain.TotalEffects = chain.PredictedEffects.Count;
            chain.CriticalEffects = chain.PredictedEffects.Count(e => e.Probability > 0.8f);

            return chain;
        }

        /// <summary>
        /// Gets the posterior probability of a specific node. Returns prior if not computed.
        /// </summary>
        public float GetPosterior(string nodeName)
        {
            return _posteriors.TryGetValue(nodeName, out var p) ? p : GetPrior(nodeName);
        }

        private float GetPrior(string nodeName)
        {
            return _priors.TryGetValue(nodeName, out var p) ? p : 0.5f;
        }

        #endregion

        /// <summary>
        /// Gets the causal explanation for why something happened.
        /// </summary>
        public string GetCausalExplanation(string effect, string cause)
        {
            var rootAnalysis = FindRootCauses(effect);
            var relevantCause = rootAnalysis.PotentialCauses.FirstOrDefault(c => c.Cause == cause);

            if (relevantCause == null)
                return $"No direct causal link found between '{cause}' and '{effect}'";

            var pathDescription = string.Join(" → ", relevantCause.CausalPath);
            return $"'{cause}' leads to '{effect}' through the chain: {pathDescription} " +
                   $"(likelihood: {relevantCause.Likelihood:P0})";
        }

        private List<ConditionMapping> MapDecisionToConditions(DesignDecision decision)
        {
            var conditions = new List<ConditionMapping>();

            // Map common decision types to causal conditions
            switch (decision.DecisionType.ToLower())
            {
                case "corridor_width":
                    if (decision.Parameters.TryGetValue("width", out var width) && (double)width < 1200)
                        conditions.Add(new ConditionMapping { Condition = "NarrowCorridor", Relevance = 1.0f });
                    break;

                case "ceiling_height":
                    if (decision.Parameters.TryGetValue("height", out var height) && (double)height < 2400)
                        conditions.Add(new ConditionMapping { Condition = "LowCeilingHeight", Relevance = 1.0f });
                    break;

                case "insulation":
                    if (decision.Parameters.TryGetValue("rvalue", out var rvalue) && (double)rvalue < 3.0)
                        conditions.Add(new ConditionMapping { Condition = "InsufficientInsulation", Relevance = 0.8f });
                    break;

                case "glazing_ratio":
                    if (decision.Parameters.TryGetValue("wwr", out var wwr) && (double)wwr > 0.4)
                        conditions.Add(new ConditionMapping { Condition = "ExcessiveGlazing", Relevance = 0.9f });
                    break;

                case "remove_wall":
                    conditions.Add(new ConditionMapping { Condition = "LoadBearingWallRemoval", Relevance = 0.7f });
                    break;

                case "fire_separation":
                    if (decision.Parameters.TryGetValue("rating", out var rating) && (int)rating == 0)
                        conditions.Add(new ConditionMapping { Condition = "NoFireSeparation", Relevance = 1.0f });
                    break;
            }

            return conditions;
        }

        private List<ConditionMapping> MapChangeToConditions(string change, Dictionary<string, object> parameters)
        {
            var conditions = new List<ConditionMapping>();

            // Map changes to potential causal conditions
            if (change.Contains("narrow") || change.Contains("reduce width"))
            {
                conditions.Add(new ConditionMapping { Condition = "NarrowCorridor", Relevance = 0.8f, IsNegative = true });
            }

            if (change.Contains("lower ceiling") || change.Contains("reduce height"))
            {
                conditions.Add(new ConditionMapping { Condition = "LowCeilingHeight", Relevance = 0.8f, IsNegative = true });
            }

            if (change.Contains("more glass") || change.Contains("larger window"))
            {
                conditions.Add(new ConditionMapping { Condition = "ExcessiveGlazing", Relevance = 0.7f, IsNegative = true });
            }

            if (change.Contains("remove wall"))
            {
                conditions.Add(new ConditionMapping { Condition = "LoadBearingWallRemoval", Relevance = 0.6f, IsNegative = true });
            }

            return conditions;
        }

        private bool IsNegativeOutcome(PredictedEffect effect)
        {
            var negativeCategories = new HashSet<string> { "Safety", "Health", "Compliance", "Durability" };
            var negativeTerms = new[] { "Risk", "Violation", "Failure", "Collapse", "Damage", "Complaint", "Problem" };

            if (negativeCategories.Contains(effect.Category))
                return true;

            return negativeTerms.Any(term => effect.Effect.Contains(term));
        }

        private MitigationEffort EstimateMitigationEffort(string strategy)
        {
            if (strategy.Contains("immediate") || strategy.Contains("Upgrade"))
                return MitigationEffort.High;
            if (strategy.Contains("Add") || strategy.Contains("Install"))
                return MitigationEffort.Medium;
            return MitigationEffort.Low;
        }

        private class ConditionMapping
        {
            public string Condition { get; set; }
            public float Relevance { get; set; }
            public bool IsNegative { get; set; }
        }
    }

    #endregion

    #region Analysis Results

    /// <summary>
    /// A chain of predicted effects from a root cause.
    /// </summary>
    public class CausalChain
    {
        public string RootCause { get; set; }
        public List<PredictedEffect> PredictedEffects { get; set; }
        public int TotalEffects { get; set; }
        public int CriticalEffects { get; set; }
    }

    /// <summary>
    /// A predicted effect in a causal chain.
    /// </summary>
    public class PredictedEffect
    {
        public string Effect { get; set; }
        public float Probability { get; set; }
        public List<string> CausalPath { get; set; }
        public string TimeToManifest { get; set; }
        public string Category { get; set; }
        public string Domain { get; set; }
        public bool IsReversible { get; set; }
        public string MitigationStrategy { get; set; }
        public int Depth { get; set; }
    }

    /// <summary>
    /// Root cause analysis result.
    /// </summary>
    public class RootCauseAnalysis
    {
        public string ObservedEffect { get; set; }
        public string MostLikelyCause { get; set; }
        public List<PotentialCause> PotentialCauses { get; set; }
    }

    /// <summary>
    /// A potential root cause.
    /// </summary>
    public class PotentialCause
    {
        public string Cause { get; set; }
        public float Likelihood { get; set; }
        public List<string> CausalPath { get; set; }
        public string Category { get; set; }
        public string Domain { get; set; }
        public bool IsRootCause { get; set; }
        public int Depth { get; set; }
    }

    /// <summary>
    /// Analysis of a design decision.
    /// </summary>
    public class DesignDecisionAnalysis
    {
        public DesignDecision Decision { get; set; }
        public List<PredictedEffect> PositiveEffects { get; set; }
        public List<PredictedEffect> NegativeEffects { get; set; }
        public List<MitigationSuggestion> Mitigations { get; set; }
        public float OverallRiskScore { get; set; }
        public string RecommendedAction { get; set; }
    }

    /// <summary>
    /// A design decision to analyze.
    /// </summary>
    public class DesignDecision
    {
        public string DecisionId { get; set; }
        public string DecisionType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A mitigation suggestion.
    /// </summary>
    public class MitigationSuggestion
    {
        public string ForEffect { get; set; }
        public string Strategy { get; set; }
        public float ReducesRiskBy { get; set; }
        public MitigationEffort Effort { get; set; }
    }

    public enum MitigationEffort
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// What-if analysis result.
    /// </summary>
    public class WhatIfAnalysis
    {
        public string ProposedChange { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<WhatIfScenario> Scenarios { get; set; }
    }

    /// <summary>
    /// A what-if scenario.
    /// </summary>
    public class WhatIfScenario
    {
        public string ScenarioName { get; set; }
        public float Probability { get; set; }
        public List<PredictedEffect> Effects { get; set; } = new List<PredictedEffect>();
    }

    /// <summary>
    /// Result of Bayesian posterior computation.
    /// </summary>
    public class BayesianAnalysisResult
    {
        public Dictionary<string, bool> Evidence { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, float> Posteriors { get; set; } = new Dictionary<string, float>();
        public List<BayesianHypothesis> MostLikelyHypotheses { get; set; } = new List<BayesianHypothesis>();
        public List<string> HighestRiskNodes { get; set; } = new List<string>();
    }

    /// <summary>
    /// A Bayesian hypothesis with prior and posterior probabilities.
    /// </summary>
    public class BayesianHypothesis
    {
        public string NodeName { get; set; }
        public float PriorProbability { get; set; }
        public float PosteriorProbability { get; set; }
        public float ProbabilityShift { get; set; }
    }

    #endregion
}
