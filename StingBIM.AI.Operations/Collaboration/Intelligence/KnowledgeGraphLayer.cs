// ===================================================================
// StingBIM.AI.Collaboration - Knowledge Graph Intelligence Layer
// Provides entity-relationship modeling, knowledge inference,
// and intelligent querying across BIM data
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Knowledge Graph Models

    /// <summary>
    /// Knowledge graph node (entity)
    /// </summary>
    public class KnowledgeNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        public double Importance { get; set; } = 0.5;
        public List<string> Tags { get; set; } = new();
        public string? SourceId { get; set; }
        public string? SourceType { get; set; }
    }

    /// <summary>
    /// Knowledge graph edge (relationship)
    /// </summary>
    public class KnowledgeEdge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public double Weight { get; set; } = 1.0;
        public double Confidence { get; set; } = 1.0;
        public bool IsBidirectional { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Graph query result
    /// </summary>
    public class GraphQueryResult
    {
        public List<KnowledgeNode> Nodes { get; set; } = new();
        public List<KnowledgeEdge> Edges { get; set; } = new();
        public Dictionary<string, object> Aggregations { get; set; } = new();
        public int TotalMatches { get; set; }
        public TimeSpan QueryTime { get; set; }
    }

    /// <summary>
    /// Path between nodes
    /// </summary>
    public class GraphPath
    {
        public List<KnowledgeNode> Nodes { get; set; } = new();
        public List<KnowledgeEdge> Edges { get; set; } = new();
        public double TotalWeight { get; set; }
        public int Length => Edges.Count;
    }

    /// <summary>
    /// Inference rule
    /// </summary>
    public class InferenceRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RuleCondition Condition { get; set; } = new();
        public RuleAction Action { get; set; } = new();
        public double Confidence { get; set; } = 0.8;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Rule condition
    /// </summary>
    public class RuleCondition
    {
        public string Pattern { get; set; } = string.Empty;
        public Dictionary<string, object> Constraints { get; set; } = new();
    }

    /// <summary>
    /// Rule action
    /// </summary>
    public class RuleAction
    {
        public string ActionType { get; set; } = string.Empty; // CreateEdge, UpdateNode, Alert
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Inferred knowledge
    /// </summary>
    public class InferredKnowledge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RuleId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> SourceNodeIds { get; set; } = new();
        public double Confidence { get; set; }
        public DateTime InferredAt { get; set; } = DateTime.UtcNow;
        public object? Value { get; set; }
    }

    /// <summary>
    /// Similarity result
    /// </summary>
    public class SimilarityResult
    {
        public KnowledgeNode Node { get; set; } = new();
        public double SimilarityScore { get; set; }
        public List<string> MatchingProperties { get; set; } = new();
    }

    /// <summary>
    /// Cluster of related nodes
    /// </summary>
    public class NodeCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = string.Empty;
        public List<KnowledgeNode> Members { get; set; } = new();
        public KnowledgeNode? Centroid { get; set; }
        public double Cohesion { get; set; }
    }

    #endregion

    #region Knowledge Graph Layer

    /// <summary>
    /// Knowledge graph intelligence layer
    /// </summary>
    public class KnowledgeGraphLayer : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, KnowledgeNode> _nodes = new();
        private readonly ConcurrentDictionary<string, KnowledgeEdge> _edges = new();
        private readonly ConcurrentDictionary<string, List<string>> _nodeEdgeIndex = new();
        private readonly ConcurrentDictionary<string, List<string>> _typeIndex = new();
        private readonly List<InferenceRule> _inferenceRules = new();
        private readonly ConcurrentDictionary<string, InferredKnowledge> _inferences = new();

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;

        public KnowledgeGraphLayer(ILogger? logger = null)
        {
            _logger = logger;
            InitializeInferenceRules();
            _logger?.LogInformation("KnowledgeGraphLayer initialized");
        }

        #region Node Operations

        /// <summary>
        /// Add or update a node
        /// </summary>
        public KnowledgeNode UpsertNode(KnowledgeNode node)
        {
            node.ModifiedAt = DateTime.UtcNow;
            _nodes[node.Id] = node;

            // Update type index
            var typeNodes = _typeIndex.GetOrAdd(node.Type, _ => new List<string>());
            lock (typeNodes)
            {
                if (!typeNodes.Contains(node.Id))
                    typeNodes.Add(node.Id);
            }

            return node;
        }

        /// <summary>
        /// Get node by ID
        /// </summary>
        public KnowledgeNode? GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        /// <summary>
        /// Get nodes by type
        /// </summary>
        public List<KnowledgeNode> GetNodesByType(string type)
        {
            if (!_typeIndex.TryGetValue(type, out var nodeIds))
                return new List<KnowledgeNode>();

            return nodeIds
                .Select(id => GetNode(id))
                .Where(n => n != null)
                .Cast<KnowledgeNode>()
                .ToList();
        }

        /// <summary>
        /// Search nodes by properties
        /// </summary>
        public List<KnowledgeNode> SearchNodes(
            string? type = null,
            Dictionary<string, object>? propertyFilters = null,
            string? labelContains = null,
            int limit = 100)
        {
            var query = _nodes.Values.AsEnumerable();

            if (type != null)
                query = query.Where(n => n.Type == type);

            if (labelContains != null)
                query = query.Where(n => n.Label.Contains(labelContains, StringComparison.OrdinalIgnoreCase));

            if (propertyFilters != null)
            {
                foreach (var filter in propertyFilters)
                {
                    query = query.Where(n =>
                        n.Properties.TryGetValue(filter.Key, out var value) &&
                        value.Equals(filter.Value));
                }
            }

            return query.Take(limit).ToList();
        }

        /// <summary>
        /// Delete node and its edges
        /// </summary>
        public bool DeleteNode(string nodeId)
        {
            if (!_nodes.TryRemove(nodeId, out var node))
                return false;

            // Remove from type index
            if (_typeIndex.TryGetValue(node.Type, out var typeNodes))
            {
                lock (typeNodes)
                {
                    typeNodes.Remove(nodeId);
                }
            }

            // Remove connected edges
            if (_nodeEdgeIndex.TryRemove(nodeId, out var edgeIds))
            {
                foreach (var edgeId in edgeIds)
                {
                    _edges.TryRemove(edgeId, out _);
                }
            }

            return true;
        }

        #endregion

        #region Edge Operations

        /// <summary>
        /// Create edge between nodes
        /// </summary>
        public KnowledgeEdge CreateEdge(
            string sourceId,
            string targetId,
            string relationType,
            Dictionary<string, object>? properties = null,
            double weight = 1.0,
            bool bidirectional = false)
        {
            var edge = new KnowledgeEdge
            {
                SourceId = sourceId,
                TargetId = targetId,
                RelationType = relationType,
                Properties = properties ?? new Dictionary<string, object>(),
                Weight = weight,
                IsBidirectional = bidirectional
            };

            _edges[edge.Id] = edge;

            // Update edge index
            UpdateEdgeIndex(sourceId, edge.Id);
            UpdateEdgeIndex(targetId, edge.Id);

            return edge;
        }

        private void UpdateEdgeIndex(string nodeId, string edgeId)
        {
            var edges = _nodeEdgeIndex.GetOrAdd(nodeId, _ => new List<string>());
            lock (edges)
            {
                if (!edges.Contains(edgeId))
                    edges.Add(edgeId);
            }
        }

        /// <summary>
        /// Get edges for a node
        /// </summary>
        public List<KnowledgeEdge> GetNodeEdges(string nodeId, string? relationType = null)
        {
            if (!_nodeEdgeIndex.TryGetValue(nodeId, out var edgeIds))
                return new List<KnowledgeEdge>();

            var edges = edgeIds
                .Select(id => _edges.TryGetValue(id, out var e) ? e : null)
                .Where(e => e != null)
                .Cast<KnowledgeEdge>();

            if (relationType != null)
                edges = edges.Where(e => e.RelationType == relationType);

            return edges.ToList();
        }

        /// <summary>
        /// Get neighbors of a node
        /// </summary>
        public List<KnowledgeNode> GetNeighbors(
            string nodeId,
            string? relationType = null,
            bool outgoing = true,
            bool incoming = true)
        {
            var edges = GetNodeEdges(nodeId, relationType);
            var neighborIds = new HashSet<string>();

            foreach (var edge in edges)
            {
                if (outgoing && edge.SourceId == nodeId)
                    neighborIds.Add(edge.TargetId);
                if (incoming && edge.TargetId == nodeId)
                    neighborIds.Add(edge.SourceId);
                if (edge.IsBidirectional)
                {
                    neighborIds.Add(edge.SourceId);
                    neighborIds.Add(edge.TargetId);
                }
            }

            neighborIds.Remove(nodeId);

            return neighborIds
                .Select(id => GetNode(id))
                .Where(n => n != null)
                .Cast<KnowledgeNode>()
                .ToList();
        }

        #endregion

        #region Graph Traversal

        /// <summary>
        /// Find shortest path between nodes
        /// </summary>
        public async Task<GraphPath?> FindShortestPathAsync(
            string sourceId,
            string targetId,
            int maxDepth = 10,
            CancellationToken ct = default)
        {
            if (sourceId == targetId)
                return new GraphPath { Nodes = new List<KnowledgeNode> { GetNode(sourceId)! } };

            var visited = new HashSet<string>();
            var queue = new Queue<(string nodeId, GraphPath path)>();

            var startNode = GetNode(sourceId);
            if (startNode == null) return null;

            queue.Enqueue((sourceId, new GraphPath { Nodes = new List<KnowledgeNode> { startNode } }));

            while (queue.Count > 0 && !ct.IsCancellationRequested)
            {
                var (currentId, currentPath) = queue.Dequeue();

                if (currentPath.Length >= maxDepth)
                    continue;

                if (visited.Contains(currentId))
                    continue;

                visited.Add(currentId);

                var edges = GetNodeEdges(currentId);

                foreach (var edge in edges)
                {
                    var nextId = edge.SourceId == currentId ? edge.TargetId : edge.SourceId;

                    if (visited.Contains(nextId))
                        continue;

                    var nextNode = GetNode(nextId);
                    if (nextNode == null)
                        continue;

                    var newPath = new GraphPath
                    {
                        Nodes = new List<KnowledgeNode>(currentPath.Nodes) { nextNode },
                        Edges = new List<KnowledgeEdge>(currentPath.Edges) { edge },
                        TotalWeight = currentPath.TotalWeight + edge.Weight
                    };

                    if (nextId == targetId)
                        return newPath;

                    queue.Enqueue((nextId, newPath));
                }
            }

            return null;
        }

        /// <summary>
        /// Find all paths between nodes
        /// </summary>
        public async Task<List<GraphPath>> FindAllPathsAsync(
            string sourceId,
            string targetId,
            int maxDepth = 5,
            int maxPaths = 10,
            CancellationToken ct = default)
        {
            var paths = new List<GraphPath>();
            var startNode = GetNode(sourceId);
            if (startNode == null) return paths;

            void DFS(string currentId, GraphPath currentPath, HashSet<string> visited)
            {
                if (paths.Count >= maxPaths || ct.IsCancellationRequested)
                    return;

                if (currentPath.Length >= maxDepth)
                    return;

                if (currentId == targetId)
                {
                    paths.Add(new GraphPath
                    {
                        Nodes = new List<KnowledgeNode>(currentPath.Nodes),
                        Edges = new List<KnowledgeEdge>(currentPath.Edges),
                        TotalWeight = currentPath.TotalWeight
                    });
                    return;
                }

                foreach (var edge in GetNodeEdges(currentId))
                {
                    var nextId = edge.SourceId == currentId ? edge.TargetId : edge.SourceId;

                    if (visited.Contains(nextId))
                        continue;

                    var nextNode = GetNode(nextId);
                    if (nextNode == null)
                        continue;

                    visited.Add(nextId);
                    currentPath.Nodes.Add(nextNode);
                    currentPath.Edges.Add(edge);
                    currentPath.TotalWeight += edge.Weight;

                    DFS(nextId, currentPath, visited);

                    currentPath.Nodes.RemoveAt(currentPath.Nodes.Count - 1);
                    currentPath.Edges.RemoveAt(currentPath.Edges.Count - 1);
                    currentPath.TotalWeight -= edge.Weight;
                    visited.Remove(nextId);
                }
            }

            var initialPath = new GraphPath { Nodes = new List<KnowledgeNode> { startNode } };
            var initialVisited = new HashSet<string> { sourceId };
            DFS(sourceId, initialPath, initialVisited);

            return paths.OrderBy(p => p.TotalWeight).ToList();
        }

        /// <summary>
        /// Find connected subgraph
        /// </summary>
        public GraphQueryResult GetConnectedSubgraph(
            string startNodeId,
            int maxDepth = 3,
            int maxNodes = 100)
        {
            var result = new GraphQueryResult();
            var visited = new HashSet<string>();
            var queue = new Queue<(string nodeId, int depth)>();

            queue.Enqueue((startNodeId, 0));

            while (queue.Count > 0 && result.Nodes.Count < maxNodes)
            {
                var (currentId, depth) = queue.Dequeue();

                if (visited.Contains(currentId) || depth > maxDepth)
                    continue;

                visited.Add(currentId);

                var node = GetNode(currentId);
                if (node == null)
                    continue;

                result.Nodes.Add(node);

                var edges = GetNodeEdges(currentId);
                foreach (var edge in edges)
                {
                    if (!result.Edges.Any(e => e.Id == edge.Id))
                        result.Edges.Add(edge);

                    var nextId = edge.SourceId == currentId ? edge.TargetId : edge.SourceId;
                    if (!visited.Contains(nextId))
                        queue.Enqueue((nextId, depth + 1));
                }
            }

            return result;
        }

        #endregion

        #region Inference

        private void InitializeInferenceRules()
        {
            // Transitivity rule: if A contains B and B contains C, then A contains C
            _inferenceRules.Add(new InferenceRule
            {
                Name = "Transitivity_Contains",
                Description = "If A contains B and B contains C, infer A contains C",
                Condition = new RuleCondition
                {
                    Pattern = "(A)-[contains]->(B)-[contains]->(C)",
                    Constraints = new Dictionary<string, object>()
                },
                Action = new RuleAction
                {
                    ActionType = "CreateEdge",
                    Parameters = new Dictionary<string, object>
                    {
                        ["relationType"] = "contains",
                        ["source"] = "A",
                        ["target"] = "C"
                    }
                },
                Confidence = 0.9
            });

            // Similarity rule: elements with same type and similar properties are related
            _inferenceRules.Add(new InferenceRule
            {
                Name = "SimilarElements",
                Description = "Elements with same type and similar properties are related",
                Condition = new RuleCondition
                {
                    Pattern = "SameType(A, B) AND SimilarProperties(A, B, 0.8)",
                    Constraints = new Dictionary<string, object>()
                },
                Action = new RuleAction
                {
                    ActionType = "CreateEdge",
                    Parameters = new Dictionary<string, object>
                    {
                        ["relationType"] = "similar_to",
                        ["source"] = "A",
                        ["target"] = "B"
                    }
                },
                Confidence = 0.75
            });

            // Impact rule: issues linked to elements affect their related elements
            _inferenceRules.Add(new InferenceRule
            {
                Name = "IssueImpact",
                Description = "Issues affecting an element may impact connected elements",
                Condition = new RuleCondition
                {
                    Pattern = "(Issue)-[affects]->(Element)-[connected_to]->(OtherElement)",
                    Constraints = new Dictionary<string, object> { ["Issue.severity"] = "high" }
                },
                Action = new RuleAction
                {
                    ActionType = "Alert",
                    Parameters = new Dictionary<string, object>
                    {
                        ["message"] = "High severity issue may impact connected elements"
                    }
                },
                Confidence = 0.7
            });
        }

        /// <summary>
        /// Run inference engine
        /// </summary>
        public async Task<List<InferredKnowledge>> RunInferenceAsync(
            CancellationToken ct = default)
        {
            var inferences = new List<InferredKnowledge>();

            foreach (var rule in _inferenceRules.Where(r => r.IsActive))
            {
                if (ct.IsCancellationRequested) break;

                var ruleInferences = await ApplyRuleAsync(rule, ct);
                inferences.AddRange(ruleInferences);
            }

            // Store inferences
            foreach (var inference in inferences)
            {
                _inferences[inference.Id] = inference;
            }

            _logger?.LogInformation("Inference engine produced {Count} new inferences",
                inferences.Count);

            return inferences;
        }

        private async Task<List<InferredKnowledge>> ApplyRuleAsync(
            InferenceRule rule,
            CancellationToken ct)
        {
            var inferences = new List<InferredKnowledge>();

            // Simplified rule application - would use pattern matching
            if (rule.Name == "SimilarElements")
            {
                var nodesByType = _nodes.Values.GroupBy(n => n.Type);

                foreach (var group in nodesByType)
                {
                    var nodes = group.ToList();
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        for (int j = i + 1; j < nodes.Count; j++)
                        {
                            var similarity = CalculateSimilarity(nodes[i], nodes[j]);
                            if (similarity > 0.8)
                            {
                                // Check if edge already exists
                                var existingEdges = GetNodeEdges(nodes[i].Id, "similar_to");
                                if (!existingEdges.Any(e => e.TargetId == nodes[j].Id))
                                {
                                    CreateEdge(nodes[i].Id, nodes[j].Id, "similar_to",
                                        new Dictionary<string, object> { ["similarity"] = similarity },
                                        similarity, true);

                                    inferences.Add(new InferredKnowledge
                                    {
                                        RuleId = rule.Id,
                                        Type = "relationship",
                                        Description = $"Inferred similarity between {nodes[i].Label} and {nodes[j].Label}",
                                        SourceNodeIds = new List<string> { nodes[i].Id, nodes[j].Id },
                                        Confidence = rule.Confidence * similarity
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return inferences;
        }

        #endregion

        #region Similarity & Clustering

        /// <summary>
        /// Calculate similarity between nodes
        /// </summary>
        public double CalculateSimilarity(KnowledgeNode a, KnowledgeNode b)
        {
            if (a.Type != b.Type) return 0;

            var commonProps = a.Properties.Keys.Intersect(b.Properties.Keys);
            if (!commonProps.Any()) return 0.3; // Same type, no common properties

            var matchingProps = commonProps.Count(k =>
                a.Properties[k].Equals(b.Properties[k]));

            return 0.3 + 0.7 * (matchingProps / (double)commonProps.Count());
        }

        /// <summary>
        /// Find similar nodes
        /// </summary>
        public List<SimilarityResult> FindSimilarNodes(
            string nodeId,
            double minSimilarity = 0.7,
            int limit = 10)
        {
            var node = GetNode(nodeId);
            if (node == null) return new List<SimilarityResult>();

            var candidates = GetNodesByType(node.Type)
                .Where(n => n.Id != nodeId);

            return candidates
                .Select(n => new SimilarityResult
                {
                    Node = n,
                    SimilarityScore = CalculateSimilarity(node, n),
                    MatchingProperties = n.Properties.Keys
                        .Intersect(node.Properties.Keys)
                        .Where(k => n.Properties[k].Equals(node.Properties[k]))
                        .ToList()
                })
                .Where(r => r.SimilarityScore >= minSimilarity)
                .OrderByDescending(r => r.SimilarityScore)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Cluster nodes by similarity
        /// </summary>
        public List<NodeCluster> ClusterNodes(
            string nodeType,
            int maxClusters = 10)
        {
            var nodes = GetNodesByType(nodeType);
            if (nodes.Count == 0) return new List<NodeCluster>();

            var clusters = new List<NodeCluster>();
            var assigned = new HashSet<string>();

            foreach (var node in nodes.OrderByDescending(n => n.Importance))
            {
                if (assigned.Contains(node.Id)) continue;

                var cluster = new NodeCluster
                {
                    Label = node.Label,
                    Centroid = node,
                    Members = new List<KnowledgeNode> { node }
                };

                assigned.Add(node.Id);

                // Find similar nodes for cluster
                var similar = FindSimilarNodes(node.Id, 0.6, 20);
                foreach (var sim in similar.Where(s => !assigned.Contains(s.Node.Id)))
                {
                    cluster.Members.Add(sim.Node);
                    assigned.Add(sim.Node.Id);
                }

                cluster.Cohesion = cluster.Members.Count > 1
                    ? cluster.Members.Average(m => CalculateSimilarity(node, m))
                    : 1.0;

                clusters.Add(cluster);

                if (clusters.Count >= maxClusters) break;
            }

            return clusters;
        }

        #endregion

        #region Impact Analysis

        /// <summary>
        /// Analyze impact of a change
        /// </summary>
        public async Task<ImpactAnalysisResult> AnalyzeImpactAsync(
            string nodeId,
            string changeType,
            int maxDepth = 3,
            CancellationToken ct = default)
        {
            var result = new ImpactAnalysisResult { SourceNodeId = nodeId, ChangeType = changeType };

            var subgraph = GetConnectedSubgraph(nodeId, maxDepth, 50);

            foreach (var affectedNode in subgraph.Nodes.Where(n => n.Id != nodeId))
            {
                var path = await FindShortestPathAsync(nodeId, affectedNode.Id, maxDepth, ct);
                if (path == null) continue;

                var impactScore = CalculateImpactScore(path, changeType);

                result.AffectedNodes.Add(new ImpactedNode
                {
                    Node = affectedNode,
                    ImpactScore = impactScore,
                    PathLength = path.Length,
                    RelationshipChain = path.Edges.Select(e => e.RelationType).ToList()
                });
            }

            result.AffectedNodes = result.AffectedNodes
                .OrderByDescending(n => n.ImpactScore)
                .ToList();

            result.TotalImpactScore = result.AffectedNodes.Sum(n => n.ImpactScore);

            return result;
        }

        private double CalculateImpactScore(GraphPath path, string changeType)
        {
            var baseScore = changeType switch
            {
                "delete" => 1.0,
                "modify" => 0.7,
                "add" => 0.3,
                _ => 0.5
            };

            // Decay with distance
            var distanceFactor = 1.0 / (1 + path.Length * 0.5);

            // Edge weight factor
            var weightFactor = path.Edges.Any()
                ? path.Edges.Average(e => e.Weight)
                : 1.0;

            return baseScore * distanceFactor * weightFactor;
        }

        #endregion

        #region Export/Import

        /// <summary>
        /// Export graph to serializable format
        /// </summary>
        public GraphExport Export()
        {
            return new GraphExport
            {
                Nodes = _nodes.Values.ToList(),
                Edges = _edges.Values.ToList(),
                Inferences = _inferences.Values.ToList(),
                ExportedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Import graph from export
        /// </summary>
        public void Import(GraphExport data, bool merge = false)
        {
            if (!merge)
            {
                _nodes.Clear();
                _edges.Clear();
                _nodeEdgeIndex.Clear();
                _typeIndex.Clear();
            }

            foreach (var node in data.Nodes)
            {
                UpsertNode(node);
            }

            foreach (var edge in data.Edges)
            {
                _edges[edge.Id] = edge;
                UpdateEdgeIndex(edge.SourceId, edge.Id);
                UpdateEdgeIndex(edge.TargetId, edge.Id);
            }

            _logger?.LogInformation("Imported {Nodes} nodes and {Edges} edges",
                data.Nodes.Count, data.Edges.Count);
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _logger?.LogInformation("KnowledgeGraphLayer disposed with {Nodes} nodes, {Edges} edges",
                _nodes.Count, _edges.Count);
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Support Models

    public class ImpactAnalysisResult
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public List<ImpactedNode> AffectedNodes { get; set; } = new();
        public double TotalImpactScore { get; set; }
    }

    public class ImpactedNode
    {
        public KnowledgeNode Node { get; set; } = new();
        public double ImpactScore { get; set; }
        public int PathLength { get; set; }
        public List<string> RelationshipChain { get; set; } = new();
    }

    public class GraphExport
    {
        public List<KnowledgeNode> Nodes { get; set; } = new();
        public List<KnowledgeEdge> Edges { get; set; } = new();
        public List<InferredKnowledge> Inferences { get; set; } = new();
        public DateTime ExportedAt { get; set; }
    }

    #endregion
}
