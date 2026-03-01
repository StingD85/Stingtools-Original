// StingBIM.AI.Knowledge.Inference.InferenceEngine
// Reasoning engine for deriving new knowledge from existing facts
// Master Proposal Reference: Part 2.2 Strategy 2 - Knowledge Graph Explosion

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.Knowledge.Graph;

namespace StingBIM.AI.Knowledge.Inference
{
    /// <summary>
    /// Inference engine that derives new knowledge from existing facts.
    /// Implements forward and backward chaining, analogy-based reasoning,
    /// and probabilistic inference for building design.
    /// </summary>
    public class InferenceEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly KnowledgeGraph _graph;
        private readonly List<InferenceRule> _rules;
        private readonly Dictionary<string, List<DerivedFact>> _derivedFacts;
        private readonly InferenceConfig _config;

        public InferenceEngine(KnowledgeGraph graph, InferenceConfig config = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _config = config ?? new InferenceConfig();
            _rules = new List<InferenceRule>();
            _derivedFacts = new Dictionary<string, List<DerivedFact>>();

            InitializeBuiltInRules();
        }

        #region Public API

        /// <summary>
        /// Runs forward chaining inference to derive new facts.
        /// </summary>
        public InferenceResult RunForwardChaining(int maxIterations = 10)
        {
            Logger.Debug($"Starting forward chaining (max {maxIterations} iterations)");

            var result = new InferenceResult
            {
                StartTime = DateTime.Now,
                Method = InferenceMethod.ForwardChaining
            };

            var newFactsTotal = 0;
            var iteration = 0;

            while (iteration < maxIterations)
            {
                var newFacts = RunSingleForwardPass();
                if (newFacts == 0) break;

                newFactsTotal += newFacts;
                iteration++;

                Logger.Debug($"Iteration {iteration}: derived {newFacts} new facts");
            }

            result.EndTime = DateTime.Now;
            result.DerivedFacts = newFactsTotal;
            result.Iterations = iteration;
            result.Success = true;

            Logger.Info($"Forward chaining complete: {newFactsTotal} new facts in {iteration} iterations");
            return result;
        }

        /// <summary>
        /// Runs backward chaining to prove a goal.
        /// </summary>
        public InferenceResult RunBackwardChaining(InferenceGoal goal)
        {
            Logger.Debug($"Starting backward chaining for: {goal.Predicate}({goal.Subject}, {goal.Object})");

            var result = new InferenceResult
            {
                StartTime = DateTime.Now,
                Method = InferenceMethod.BackwardChaining,
                Goal = goal
            };

            var proofPath = TryProveGoal(goal, new HashSet<string>(), 0);

            result.EndTime = DateTime.Now;
            result.Success = proofPath != null;
            result.ProofPath = proofPath;

            if (result.Success)
            {
                Logger.Info($"Goal proven with {proofPath.Count} steps");
            }
            else
            {
                Logger.Info("Goal could not be proven");
            }

            return result;
        }

        /// <summary>
        /// Answers a query using inference.
        /// </summary>
        public QueryAnswer AnswerQuery(InferenceQuery query)
        {
            var answer = new QueryAnswer
            {
                Query = query,
                Answers = new List<QueryResult>()
            };

            switch (query.Type)
            {
                case QueryType.IsRelated:
                    answer.Answers.AddRange(QueryIsRelated(query));
                    break;

                case QueryType.FindRelated:
                    answer.Answers.AddRange(QueryFindRelated(query));
                    break;

                case QueryType.WhyRelated:
                    answer.Answers.AddRange(QueryWhyRelated(query));
                    break;

                case QueryType.WhatIf:
                    answer.Answers.AddRange(QueryWhatIf(query));
                    break;

                case QueryType.Recommend:
                    answer.Answers.AddRange(QueryRecommend(query));
                    break;
            }

            answer.Confidence = answer.Answers.Any()
                ? answer.Answers.Max(a => a.Confidence)
                : 0;

            return answer;
        }

        /// <summary>
        /// Finds analogies between concepts.
        /// </summary>
        public IEnumerable<Analogy> FindAnalogies(string conceptId)
        {
            var analogies = new List<Analogy>();
            var sourceNode = _graph.GetNode(conceptId);

            if (sourceNode == null) return analogies;

            // Get relationships for source
            var sourceEdges = _graph.GetOutgoingEdges(conceptId).ToList();
            var sourceRelations = sourceEdges.GroupBy(e => e.RelationType)
                .ToDictionary(g => g.Key, g => g.Select(e => e.TargetId).ToList());

            // Find nodes with similar relationship patterns
            var allNodes = _graph.GetNodesByType(sourceNode.NodeType).Where(n => n.Id != conceptId);

            foreach (var candidate in allNodes)
            {
                var candidateEdges = _graph.GetOutgoingEdges(candidate.Id).ToList();
                var candidateRelations = candidateEdges.GroupBy(e => e.RelationType)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.TargetId).ToList());

                var similarity = CalculateRelationSimilarity(sourceRelations, candidateRelations);

                if (similarity > 0.5)
                {
                    analogies.Add(new Analogy
                    {
                        SourceId = conceptId,
                        TargetId = candidate.Id,
                        Similarity = similarity,
                        SharedRelations = sourceRelations.Keys.Intersect(candidateRelations.Keys).ToList()
                    });
                }
            }

            return analogies.OrderByDescending(a => a.Similarity).Take(5);
        }

        /// <summary>
        /// Adds a custom inference rule.
        /// </summary>
        public void AddRule(InferenceRule rule)
        {
            _rules.Add(rule);
            Logger.Debug($"Added rule: {rule.Name}");
        }

        /// <summary>
        /// Gets all derived facts for a subject.
        /// </summary>
        public IEnumerable<DerivedFact> GetDerivedFacts(string subjectId)
        {
            return _derivedFacts.GetValueOrDefault(subjectId) ?? Enumerable.Empty<DerivedFact>();
        }

        #endregion

        #region Built-in Rules

        private void InitializeBuiltInRules()
        {
            // Transitivity rule: if A is_a B and B is_a C, then A is_a C
            AddRule(new InferenceRule
            {
                Name = "Transitivity",
                Description = "If A is related to B, and B is related to C (same relation), then A may be related to C",
                TransitiveRelations = new[] { "is_a", "part_of", "contains", "includes" },
                RequireAllSubGoals = false, // OR mode: any intermediate path suffices
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    foreach (var relation in new[] { "is_a", "part_of" })
                    {
                        var edges = graph.QueryTriple(relationType: relation).ToList();

                        foreach (var (s1, p1, o1) in edges)
                        {
                            var secondHops = graph.GetOutgoingEdges(o1.Id)
                                .Where(e => e.RelationType == relation);

                            foreach (var e2 in secondHops)
                            {
                                // Check if direct relation already exists
                                var existing = graph.GetOutgoingEdges(s1.Id)
                                    .Any(e => e.RelationType == relation && e.TargetId == e2.TargetId);

                                if (!existing)
                                {
                                    newFacts.Add(new DerivedFact
                                    {
                                        SubjectId = s1.Id,
                                        Predicate = relation,
                                        ObjectId = e2.TargetId,
                                        Confidence = p1.Strength * 0.8f,
                                        DerivationRule = "Transitivity",
                                        Evidence = new[] { $"{s1.Id} {relation} {o1.Id}", $"{o1.Id} {relation} {e2.TargetId}" }
                                    });
                                }
                            }
                        }
                    }

                    return newFacts;
                }
            });

            // Adjacency inheritance: if room type A prefers adjacency to B, specific instances inherit this
            AddRule(new InferenceRule
            {
                Name = "AdjacencyInheritance",
                Description = "Room instances inherit adjacency preferences from their types",
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    // Find adjacency preferences between room types
                    var preferences = graph.QueryTriple(relationType: "adjacent_preferred").ToList();

                    foreach (var (roomType1, pref, roomType2) in preferences)
                    {
                        // Find all instances of these room types
                        var instances1 = graph.QueryTriple(relationType: "instance_of")
                            .Where(t => t.Object.Id == roomType1.Id)
                            .Select(t => t.Subject);

                        var instances2 = graph.QueryTriple(relationType: "instance_of")
                            .Where(t => t.Object.Id == roomType2.Id)
                            .Select(t => t.Subject);

                        foreach (var inst1 in instances1)
                        {
                            foreach (var inst2 in instances2)
                            {
                                newFacts.Add(new DerivedFact
                                {
                                    SubjectId = inst1.Id,
                                    Predicate = "should_be_adjacent_to",
                                    ObjectId = inst2.Id,
                                    Confidence = pref.Strength * 0.9f,
                                    DerivationRule = "AdjacencyInheritance",
                                    Evidence = new[] { $"{roomType1.Name} adjacent_preferred {roomType2.Name}" }
                                });
                            }
                        }
                    }

                    return newFacts;
                }
            });

            // Material suitability: infer material recommendations based on room type
            AddRule(new InferenceRule
            {
                Name = "MaterialSuitability",
                Description = "Infer suitable materials based on room characteristics",
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    // Wet room materials
                    var wetRooms = new[] { "bathroom", "kitchen", "laundry" };
                    var waterResistantMaterials = new[] { "tile", "porcelain", "vinyl", "stainless_steel" };

                    foreach (var roomType in wetRooms)
                    {
                        var room = graph.GetNode(roomType);
                        if (room == null) continue;

                        foreach (var material in waterResistantMaterials)
                        {
                            var matNode = graph.GetNode(material);
                            if (matNode == null) continue;

                            newFacts.Add(new DerivedFact
                            {
                                SubjectId = roomType,
                                Predicate = "suitable_material",
                                ObjectId = material,
                                Confidence = 0.85f,
                                DerivationRule = "MaterialSuitability",
                                Evidence = new[] { $"{roomType} is wet room", $"{material} is water resistant" }
                            });
                        }
                    }

                    return newFacts;
                }
            });

            // Spatial implication: derive spatial requirements from room types
            AddRule(new InferenceRule
            {
                Name = "SpatialImplication",
                Description = "Derive spatial requirements from room function",
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    var spatialRules = new Dictionary<string, Dictionary<string, object>>
                    {
                        ["bedroom"] = new() { ["requires_window"] = true, ["min_area"] = 9.0, ["requires_door"] = true },
                        ["bathroom"] = new() { ["requires_ventilation"] = true, ["requires_plumbing"] = true, ["requires_door"] = true },
                        ["kitchen"] = new() { ["requires_ventilation"] = true, ["requires_plumbing"] = true, ["requires_window"] = true },
                        ["living_room"] = new() { ["requires_window"] = true, ["min_area"] = 15.0 },
                        ["garage"] = new() { ["requires_ventilation"] = true, ["min_area"] = 18.0 }
                    };

                    foreach (var (roomType, requirements) in spatialRules)
                    {
                        var room = graph.GetNode(roomType);
                        if (room == null) continue;

                        foreach (var (req, value) in requirements)
                        {
                            newFacts.Add(new DerivedFact
                            {
                                SubjectId = roomType,
                                Predicate = req,
                                ObjectId = value.ToString(),
                                Confidence = 0.95f,
                                DerivationRule = "SpatialImplication",
                                Evidence = new[] { $"{roomType} function implies {req}" }
                            });
                        }
                    }

                    return newFacts;
                }
            });

            // Inverse relationships
            AddRule(new InferenceRule
            {
                Name = "InverseRelation",
                Description = "Derive inverse relationships automatically",
                InverseRelations = new Dictionary<string, string>
                {
                    ["contains"] = "contained_in",
                    ["part_of"] = "has_part",
                    ["adjacent_to"] = "adjacent_to",
                    ["above"] = "below",
                    ["supports"] = "supported_by"
                },
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();
                    var inverses = new Dictionary<string, string>
                    {
                        ["contains"] = "contained_in",
                        ["part_of"] = "has_part",
                        ["above"] = "below",
                        ["supports"] = "supported_by"
                    };

                    foreach (var (relation, inverse) in inverses)
                    {
                        var edges = graph.QueryTriple(relationType: relation).ToList();

                        foreach (var (subject, pred, obj) in edges)
                        {
                            // Check if inverse already exists
                            var existing = graph.GetOutgoingEdges(obj.Id)
                                .Any(e => e.RelationType == inverse && e.TargetId == subject.Id);

                            if (!existing)
                            {
                                newFacts.Add(new DerivedFact
                                {
                                    SubjectId = obj.Id,
                                    Predicate = inverse,
                                    ObjectId = subject.Id,
                                    Confidence = pred.Strength,
                                    DerivationRule = "InverseRelation",
                                    Evidence = new[] { $"{subject.Id} {relation} {obj.Id}" }
                                });
                            }
                        }
                    }

                    return newFacts;
                }
            });

            // Code compliance: derive compliance requirements from standards linked to rooms/elements
            AddRule(new InferenceRule
            {
                Name = "CodeCompliance",
                Description = "Derive compliance requirements from applicable building standards",
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    // Find all RegulatedBy relationships and derive compliance requirements
                    var regulations = graph.QueryTriple(relationType: "RegulatedBy").ToList();

                    foreach (var (subject, predicate, standard) in regulations)
                    {
                        var section = predicate.Properties?.GetValueOrDefault("Section")?.ToString() ?? "";
                        var requirement = predicate.Properties?.GetValueOrDefault("Requirement")?.ToString() ?? "";

                        newFacts.Add(new DerivedFact
                        {
                            SubjectId = subject.Id,
                            Predicate = "requires_compliance_with",
                            ObjectId = standard.Id,
                            Confidence = 0.95f,
                            DerivationRule = "CodeCompliance",
                            Evidence = new[] { $"{subject.Name} is regulated by {standard.Name} § {section}: {requirement}" }
                        });

                        // Derive specific requirements from standard properties
                        if (!string.IsNullOrEmpty(requirement))
                        {
                            newFacts.Add(new DerivedFact
                            {
                                SubjectId = subject.Id,
                                Predicate = "has_code_requirement",
                                ObjectId = requirement,
                                Confidence = 0.90f,
                                DerivationRule = "CodeCompliance",
                                Evidence = new[] { $"Standard {standard.Name} § {section}" }
                            });
                        }
                    }

                    return newFacts;
                }
            });

            // Causal reasoning: derive impacts from causal relationships
            AddRule(new InferenceRule
            {
                Name = "CausalReasoning",
                Description = "Derive downstream impacts from causal relationships",
                Evaluate = (graph, context) =>
                {
                    var newFacts = new List<DerivedFact>();

                    var causalEdges = graph.QueryTriple(relationType: "CausalRelation").ToList();

                    // Two-hop causal chains: if A causes B and B causes C, then A indirectly affects C
                    foreach (var (source, edge1, intermediate) in causalEdges)
                    {
                        var secondHops = graph.GetOutgoingEdges(intermediate.Id)
                            .Where(e => e.RelationType == "CausalRelation");

                        foreach (var edge2 in secondHops)
                        {
                            var existing = newFacts.Any(f =>
                                f.SubjectId == source.Id && f.ObjectId == edge2.TargetId && f.Predicate == "indirectly_affects");

                            if (!existing)
                            {
                                var combinedStrength = edge1.Strength * edge2.Strength;
                                if (combinedStrength >= 0.5f)
                                {
                                    var cause1 = edge1.Properties?.GetValueOrDefault("Cause")?.ToString() ?? source.Id;
                                    var effect2 = edge2.Properties?.GetValueOrDefault("Effect")?.ToString() ?? edge2.TargetId;

                                    newFacts.Add(new DerivedFact
                                    {
                                        SubjectId = source.Id,
                                        Predicate = "indirectly_affects",
                                        ObjectId = edge2.TargetId,
                                        Confidence = combinedStrength * 0.9f,
                                        DerivationRule = "CausalReasoning",
                                        Evidence = new[] { $"{cause1} → {intermediate.Id} → {effect2}" }
                                    });
                                }
                            }
                        }
                    }

                    return newFacts;
                }
            });

            Logger.Info($"Initialized {_rules.Count} inference rules");
        }

        #endregion

        #region Private Methods

        private int RunSingleForwardPass()
        {
            var newFacts = 0;
            var context = new InferenceContext();

            foreach (var rule in _rules)
            {
                try
                {
                    var derived = rule.Evaluate(_graph, context);

                    foreach (var fact in derived)
                    {
                        if (AddDerivedFact(fact))
                        {
                            newFacts++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Rule {rule.Name} failed");
                }
            }

            return newFacts;
        }

        private bool AddDerivedFact(DerivedFact fact)
        {
            if (!_derivedFacts.ContainsKey(fact.SubjectId))
            {
                _derivedFacts[fact.SubjectId] = new List<DerivedFact>();
            }

            // Check for duplicate
            var existing = _derivedFacts[fact.SubjectId]
                .Any(f => f.Predicate == fact.Predicate && f.ObjectId == fact.ObjectId);

            if (existing) return false;

            _derivedFacts[fact.SubjectId].Add(fact);

            // Optionally add to graph
            if (_config.MaterializeDerivedFacts && fact.Confidence >= _config.MaterializationThreshold)
            {
                _graph.AddRelationship(fact.SubjectId, fact.ObjectId, fact.Predicate, fact.Confidence);
            }

            return true;
        }

        private List<ProofStep> TryProveGoal(InferenceGoal goal, HashSet<string> visited, int depth)
        {
            if (depth > _config.MaxProofDepth) return null;

            var goalKey = $"{goal.Subject}:{goal.Predicate}:{goal.Object}";
            if (visited.Contains(goalKey)) return null;
            visited.Add(goalKey);

            // Direct check in graph
            var direct = _graph.GetOutgoingEdges(goal.Subject)
                .FirstOrDefault(e => e.RelationType == goal.Predicate &&
                                     (string.IsNullOrEmpty(goal.Object) || e.TargetId == goal.Object));

            if (direct != null)
            {
                return new List<ProofStep>
                {
                    new ProofStep
                    {
                        Type = ProofStepType.DirectFact,
                        Description = $"{goal.Subject} {goal.Predicate} {direct.TargetId}",
                        Confidence = direct.Strength
                    }
                };
            }

            // Check derived facts
            var derived = GetDerivedFacts(goal.Subject)
                .FirstOrDefault(f => f.Predicate == goal.Predicate &&
                                     (string.IsNullOrEmpty(goal.Object) || f.ObjectId == goal.Object));

            if (derived != null)
            {
                return new List<ProofStep>
                {
                    new ProofStep
                    {
                        Type = ProofStepType.DerivedFact,
                        Description = $"{goal.Subject} {goal.Predicate} {derived.ObjectId} (via {derived.DerivationRule})",
                        Confidence = derived.Confidence,
                        Evidence = derived.Evidence.ToList()
                    }
                };
            }

            // Try backward chaining through rules
            foreach (var rule in _rules.Where(r => r.CanDerive(goal.Predicate)))
            {
                var subGoals = rule.GetSubGoals(goal, _graph);
                if (subGoals == null) continue;

                if (rule.RequireAllSubGoals)
                {
                    // AND mode: all sub-goals must be proven
                    var allProven = true;
                    var combinedPath = new List<ProofStep>();

                    foreach (var subGoal in subGoals)
                    {
                        var subProof = TryProveGoal(subGoal, visited, depth + 1);
                        if (subProof == null)
                        {
                            allProven = false;
                            break;
                        }
                        combinedPath.AddRange(subProof);
                    }

                    if (allProven && combinedPath.Any())
                    {
                        combinedPath.Add(new ProofStep
                        {
                            Type = ProofStepType.RuleApplication,
                            Description = $"Applied rule: {rule.Name}",
                            Confidence = combinedPath.Min(s => s.Confidence) * 0.9f
                        });
                        return combinedPath;
                    }
                }
                else
                {
                    // OR mode: any one sub-goal proving is sufficient (used by transitivity)
                    foreach (var subGoal in subGoals)
                    {
                        // Use a fresh visited copy so failed alternatives don't block others
                        var subProof = TryProveGoal(subGoal, new HashSet<string>(visited), depth + 1);
                        if (subProof != null)
                        {
                            subProof.Add(new ProofStep
                            {
                                Type = ProofStepType.RuleApplication,
                                Description = $"Applied rule: {rule.Name} (via {subGoal.Subject})",
                                Confidence = subProof.Min(s => s.Confidence) * 0.9f
                            });
                            return subProof;
                        }
                    }
                }
            }

            return null;
        }

        private IEnumerable<QueryResult> QueryIsRelated(InferenceQuery query)
        {
            // Check direct relationship
            var direct = _graph.GetOutgoingEdges(query.Subject)
                .Any(e => e.TargetId == query.Object);

            if (direct)
            {
                yield return new QueryResult
                {
                    Answer = "Yes",
                    Confidence = 1.0f,
                    Explanation = "Direct relationship exists in knowledge graph"
                };
                yield break;
            }

            // Check path
            var path = _graph.FindPath(query.Subject, query.Object);
            if (path.Any())
            {
                yield return new QueryResult
                {
                    Answer = "Yes (indirect)",
                    Confidence = 0.8f,
                    Explanation = $"Connected through {path.Count() - 1} intermediate nodes",
                    Path = path.Select(n => n.Id).ToList()
                };
            }
            else
            {
                yield return new QueryResult
                {
                    Answer = "No known relationship",
                    Confidence = 0.5f
                };
            }
        }

        private IEnumerable<QueryResult> QueryFindRelated(InferenceQuery query)
        {
            // Get direct relations
            var edges = _graph.GetOutgoingEdges(query.Subject);

            if (!string.IsNullOrEmpty(query.Relation))
            {
                edges = edges.Where(e => e.RelationType.Equals(query.Relation, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var edge in edges.Take(10))
            {
                var target = _graph.GetNode(edge.TargetId);
                yield return new QueryResult
                {
                    Answer = target?.Name ?? edge.TargetId,
                    Confidence = edge.Strength,
                    Explanation = $"{query.Subject} {edge.RelationType} {edge.TargetId}",
                    Relation = edge.RelationType
                };
            }

            // Add derived facts
            foreach (var fact in GetDerivedFacts(query.Subject).Take(5))
            {
                if (!string.IsNullOrEmpty(query.Relation) &&
                    !fact.Predicate.Equals(query.Relation, StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return new QueryResult
                {
                    Answer = fact.ObjectId,
                    Confidence = fact.Confidence * 0.9f,
                    Explanation = $"Derived: {fact.SubjectId} {fact.Predicate} {fact.ObjectId}",
                    Relation = fact.Predicate
                };
            }
        }

        private IEnumerable<QueryResult> QueryWhyRelated(InferenceQuery query)
        {
            var goal = new InferenceGoal
            {
                Subject = query.Subject,
                Predicate = query.Relation ?? "related_to",
                Object = query.Object
            };

            var result = RunBackwardChaining(goal);

            if (result.Success)
            {
                yield return new QueryResult
                {
                    Answer = "Related through:",
                    Confidence = result.ProofPath.Min(s => s.Confidence),
                    Explanation = string.Join(" → ", result.ProofPath.Select(s => s.Description)),
                    ProofSteps = result.ProofPath
                };
            }
            else
            {
                yield return new QueryResult
                {
                    Answer = "No known relationship path",
                    Confidence = 0.3f
                };
            }
        }

        private IEnumerable<QueryResult> QueryWhatIf(InferenceQuery query)
        {
            // Simulate adding a fact and see what new facts would be derived
            // This is a hypothetical reasoning capability

            yield return new QueryResult
            {
                Answer = $"If {query.Subject} {query.Relation} {query.Object}:",
                Confidence = 0.7f,
                Explanation = "Hypothetical consequences would be derived"
            };

            // Use transitivity to infer potential consequences
            var targetEdges = _graph.GetOutgoingEdges(query.Object);
            foreach (var edge in targetEdges.Take(3))
            {
                yield return new QueryResult
                {
                    Answer = $"Then {query.Subject} might {edge.RelationType} {edge.TargetId}",
                    Confidence = 0.6f * edge.Strength,
                    Explanation = "By transitivity"
                };
            }
        }

        private IEnumerable<QueryResult> QueryRecommend(InferenceQuery query)
        {
            var node = _graph.GetNode(query.Subject);
            if (node == null) yield break;

            // Get recommendations based on type
            var sameType = _graph.GetNodesByType(node.NodeType)
                .Where(n => n.Id != query.Subject)
                .Take(5);

            foreach (var similar in sameType)
            {
                var edges = _graph.GetOutgoingEdges(similar.Id);
                foreach (var edge in edges.Take(2))
                {
                    yield return new QueryResult
                    {
                        Answer = $"Consider: {edge.TargetId}",
                        Confidence = 0.7f,
                        Explanation = $"Similar to {similar.Name} which {edge.RelationType} {edge.TargetId}",
                        Relation = edge.RelationType
                    };
                }
            }

            // Add derived recommendations
            foreach (var fact in GetDerivedFacts(query.Subject))
            {
                if (fact.Predicate.Contains("suitable") || fact.Predicate.Contains("recommend"))
                {
                    yield return new QueryResult
                    {
                        Answer = fact.ObjectId,
                        Confidence = fact.Confidence,
                        Explanation = $"Derived recommendation: {fact.Predicate}"
                    };
                }
            }
        }

        private double CalculateRelationSimilarity(
            Dictionary<string, List<string>> rel1,
            Dictionary<string, List<string>> rel2)
        {
            var allRelations = rel1.Keys.Union(rel2.Keys);
            if (!allRelations.Any()) return 0;

            var matches = 0;
            var total = 0;

            foreach (var rel in allRelations)
            {
                var has1 = rel1.ContainsKey(rel);
                var has2 = rel2.ContainsKey(rel);

                if (has1 && has2)
                {
                    matches += 2;
                    // Bonus for similar targets
                    var overlap = rel1[rel].Intersect(rel2[rel]).Count();
                    matches += overlap;
                }

                total += 2;
            }

            return (double)matches / Math.Max(total, 1);
        }

        #endregion
    }

    #region Supporting Types

    public class InferenceConfig
    {
        public bool MaterializeDerivedFacts { get; set; } = false;
        public float MaterializationThreshold { get; set; } = 0.9f;
        public int MaxProofDepth { get; set; } = 10;
        public int MaxIterations { get; set; } = 20;
    }

    public class InferenceRule
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] TransitiveRelations { get; set; }
        public Dictionary<string, string> InverseRelations { get; set; }
        public bool RequireAllSubGoals { get; set; } = true;
        public Func<KnowledgeGraph, InferenceContext, IEnumerable<DerivedFact>> Evaluate { get; set; }

        public bool CanDerive(string predicate)
        {
            return TransitiveRelations?.Contains(predicate) == true ||
                   InverseRelations?.ContainsKey(predicate) == true ||
                   InverseRelations?.ContainsValue(predicate) == true;
        }

        /// <summary>
        /// Decomposes a goal into sub-goals for backward chaining.
        /// For transitivity rules, generates OR-alternatives (any intermediate path suffices).
        /// For inverse rules, generates a single sub-goal with swapped subject/object.
        /// </summary>
        public IEnumerable<InferenceGoal> GetSubGoals(InferenceGoal goal, KnowledgeGraph graph)
        {
            if (goal == null) return null;

            // Transitivity: to prove A --rel--> C, find intermediates B where A --rel--> B,
            // then try to prove B --rel--> C for any one intermediate (OR mode)
            if (TransitiveRelations != null && TransitiveRelations.Contains(goal.Predicate) && graph != null)
            {
                var intermediates = graph.GetOutgoingEdges(goal.Subject)
                    .Where(e => e.RelationType == goal.Predicate && e.TargetId != goal.Object)
                    .Select(e => e.TargetId)
                    .Distinct()
                    .ToList();

                if (!intermediates.Any()) return null;

                // Each intermediate is an OR-alternative sub-goal: prove intermediate --rel--> Object
                return intermediates.Select(mid => new InferenceGoal
                {
                    Subject = mid,
                    Predicate = goal.Predicate,
                    Object = goal.Object
                }).ToList();
            }

            // Inverse relations: to prove A --rel--> B, prove B --inverse(rel)--> A
            if (InverseRelations != null)
            {
                // Forward lookup: goal predicate is a key (e.g., "contains" → prove "contained_in")
                if (InverseRelations.TryGetValue(goal.Predicate, out var inversePredicate))
                {
                    return new[]
                    {
                        new InferenceGoal
                        {
                            Subject = goal.Object,
                            Predicate = inversePredicate,
                            Object = goal.Subject
                        }
                    };
                }

                // Reverse lookup: goal predicate is a value (e.g., "contained_in" → prove "contains")
                var forwardEntry = InverseRelations.FirstOrDefault(kv => kv.Value == goal.Predicate);
                if (forwardEntry.Key != null)
                {
                    return new[]
                    {
                        new InferenceGoal
                        {
                            Subject = goal.Object,
                            Predicate = forwardEntry.Key,
                            Object = goal.Subject
                        }
                    };
                }
            }

            return null;
        }
    }

    public class InferenceContext
    {
        public Dictionary<string, object> Variables { get; set; } = new();
        public List<string> Trace { get; set; } = new();
    }

    public class DerivedFact
    {
        public string SubjectId { get; set; }
        public string Predicate { get; set; }
        public string ObjectId { get; set; }
        public float Confidence { get; set; }
        public string DerivationRule { get; set; }
        public string[] Evidence { get; set; }
        public DateTime DerivedAt { get; set; } = DateTime.Now;
    }

    public class InferenceResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public InferenceMethod Method { get; set; }
        public bool Success { get; set; }
        public int DerivedFacts { get; set; }
        public int Iterations { get; set; }
        public InferenceGoal Goal { get; set; }
        public List<ProofStep> ProofPath { get; set; }
    }

    public enum InferenceMethod
    {
        ForwardChaining,
        BackwardChaining,
        Analogy,
        Probabilistic
    }

    public class InferenceGoal
    {
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Object { get; set; }
    }

    public class ProofStep
    {
        public ProofStepType Type { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public List<string> Evidence { get; set; }
    }

    public enum ProofStepType
    {
        DirectFact,
        DerivedFact,
        RuleApplication,
        Assumption
    }

    public class InferenceQuery
    {
        public QueryType Type { get; set; }
        public string Subject { get; set; }
        public string Object { get; set; }
        public string Relation { get; set; }
    }

    public enum QueryType
    {
        IsRelated,
        FindRelated,
        WhyRelated,
        WhatIf,
        Recommend
    }

    public class QueryAnswer
    {
        public InferenceQuery Query { get; set; }
        public List<QueryResult> Answers { get; set; }
        public float Confidence { get; set; }
    }

    public class QueryResult
    {
        public string Answer { get; set; }
        public float Confidence { get; set; }
        public string Explanation { get; set; }
        public string Relation { get; set; }
        public List<string> Path { get; set; }
        public List<ProofStep> ProofSteps { get; set; }
    }

    public class Analogy
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public double Similarity { get; set; }
        public List<string> SharedRelations { get; set; }
    }

    #endregion
}
