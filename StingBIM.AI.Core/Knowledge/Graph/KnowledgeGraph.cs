// StingBIM.AI.Knowledge.Graph.KnowledgeGraph
// Building design knowledge graph for AI reasoning
// Master Proposal Reference: Part 2.2 Strategy 2 - Knowledge Graph Explosion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Knowledge.Graph
{
    /// <summary>
    /// Knowledge graph for building design concepts and relationships.
    /// Stores nodes (concepts) and edges (relationships) for AI reasoning.
    /// Each node exponentially increases reasoning paths (Part 2.2).
    /// </summary>
    public class KnowledgeGraph
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, KnowledgeNode> _nodes;
        private readonly List<KnowledgeEdge> _edges;
        private readonly Dictionary<string, List<string>> _nodesByType;
        private readonly Dictionary<string, List<KnowledgeEdge>> _edgesBySource;
        private readonly Dictionary<string, List<KnowledgeEdge>> _edgesByTarget;
        private readonly object _lock = new object();

        /// <summary>
        /// Total number of nodes in the graph.
        /// </summary>
        public int NodeCount
        {
            get { lock (_lock) { return _nodes.Count; } }
        }

        /// <summary>
        /// Total number of edges in the graph.
        /// </summary>
        public int EdgeCount
        {
            get { lock (_lock) { return _edges.Count; } }
        }

        public KnowledgeGraph()
        {
            _nodes = new Dictionary<string, KnowledgeNode>(StringComparer.OrdinalIgnoreCase);
            _edges = new List<KnowledgeEdge>();
            _nodesByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _edgesBySource = new Dictionary<string, List<KnowledgeEdge>>(StringComparer.OrdinalIgnoreCase);
            _edgesByTarget = new Dictionary<string, List<KnowledgeEdge>>(StringComparer.OrdinalIgnoreCase);
        }

        #region Node Operations

        /// <summary>
        /// Adds a node to the graph.
        /// </summary>
        public void AddNode(KnowledgeNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (string.IsNullOrWhiteSpace(node.Id)) throw new ArgumentException("Node must have an ID.", nameof(node));

            lock (_lock)
            {
                if (_nodes.ContainsKey(node.Id))
                {
                    Logger.Warn($"Node {node.Id} already exists, updating");
                    _nodes[node.Id] = node;
                }
                else
                {
                    _nodes[node.Id] = node;

                    // Index by type
                    if (!_nodesByType.ContainsKey(node.NodeType))
                    {
                        _nodesByType[node.NodeType] = new List<string>();
                    }
                    _nodesByType[node.NodeType].Add(node.Id);
                }
            }

            Logger.Debug($"Added node: {node.Id} ({node.NodeType})");
        }

        /// <summary>
        /// Gets a node by ID.
        /// </summary>
        public KnowledgeNode GetNode(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));

            lock (_lock)
            {
                return _nodes.GetValueOrDefault(id);
            }
        }

        /// <summary>
        /// Gets all nodes of a specific type.
        /// </summary>
        public IEnumerable<KnowledgeNode> GetNodesByType(string nodeType)
        {
            lock (_lock)
            {
                if (_nodesByType.TryGetValue(nodeType, out var nodeIds))
                {
                    return nodeIds.Select(id => _nodes[id]).ToList();
                }
                return Enumerable.Empty<KnowledgeNode>();
            }
        }

        /// <summary>
        /// Searches nodes by name or property.
        /// </summary>
        public IEnumerable<KnowledgeNode> SearchNodes(string query, int maxResults = 10)
        {
            query = query.ToLowerInvariant();

            lock (_lock)
            {
                return _nodes.Values
                    .Where(n => n.Name.ToLowerInvariant().Contains(query) ||
                                n.Description?.ToLowerInvariant().Contains(query) == true ||
                                n.Properties.Values.Any(v => v?.ToString().ToLowerInvariant().Contains(query) == true))
                    .OrderByDescending(n => CalculateRelevance(n, query))
                    .Take(maxResults)
                    .ToList();
            }
        }

        #endregion

        #region Edge Operations

        /// <summary>
        /// Adds an edge (relationship) between nodes.
        /// </summary>
        public void AddEdge(KnowledgeEdge edge)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));
            if (string.IsNullOrWhiteSpace(edge.SourceId)) throw new ArgumentException("Edge must have a source ID.", nameof(edge));
            if (string.IsNullOrWhiteSpace(edge.TargetId)) throw new ArgumentException("Edge must have a target ID.", nameof(edge));

            lock (_lock)
            {
                _edges.Add(edge);

                // Index by source
                if (!_edgesBySource.ContainsKey(edge.SourceId))
                {
                    _edgesBySource[edge.SourceId] = new List<KnowledgeEdge>();
                }
                _edgesBySource[edge.SourceId].Add(edge);

                // Index by target
                if (!_edgesByTarget.ContainsKey(edge.TargetId))
                {
                    _edgesByTarget[edge.TargetId] = new List<KnowledgeEdge>();
                }
                _edgesByTarget[edge.TargetId].Add(edge);
            }

            Logger.Debug($"Added edge: {edge.SourceId} --[{edge.RelationType}]--> {edge.TargetId}");
        }

        /// <summary>
        /// Adds a relationship between two nodes.
        /// </summary>
        public void AddRelationship(string sourceId, string targetId, string relationType, float strength = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Source ID cannot be null or empty.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID cannot be null or empty.", nameof(targetId));
            if (string.IsNullOrWhiteSpace(relationType)) throw new ArgumentException("Relation type cannot be null or empty.", nameof(relationType));

            AddEdge(new KnowledgeEdge
            {
                SourceId = sourceId,
                TargetId = targetId,
                RelationType = relationType,
                Strength = strength
            });
        }

        /// <summary>
        /// Gets all edges from a source node.
        /// </summary>
        public IEnumerable<KnowledgeEdge> GetOutgoingEdges(string nodeId)
        {
            lock (_lock)
            {
                return _edgesBySource.GetValueOrDefault(nodeId) ?? Enumerable.Empty<KnowledgeEdge>();
            }
        }

        /// <summary>
        /// Gets all edges originating from a source node.
        /// Alias for GetOutgoingEdges for API compatibility.
        /// </summary>
        public IEnumerable<KnowledgeEdge> GetEdgesFrom(string nodeId)
        {
            return GetOutgoingEdges(nodeId);
        }

        /// <summary>
        /// Gets all edges to a target node.
        /// </summary>
        public IEnumerable<KnowledgeEdge> GetIncomingEdges(string nodeId)
        {
            lock (_lock)
            {
                return _edgesByTarget.GetValueOrDefault(nodeId) ?? Enumerable.Empty<KnowledgeEdge>();
            }
        }

        /// <summary>
        /// Gets related nodes of a specific relationship type.
        /// </summary>
        public IEnumerable<KnowledgeNode> GetRelatedNodes(string nodeId, string relationType = null)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("Node ID cannot be null or empty.", nameof(nodeId));

            lock (_lock)
            {
                var outgoing = _edgesBySource.GetValueOrDefault(nodeId) ?? Enumerable.Empty<KnowledgeEdge>();

                if (!string.IsNullOrEmpty(relationType))
                {
                    outgoing = outgoing.Where(e => e.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase));
                }

                return outgoing
                    .Select(e => _nodes.GetValueOrDefault(e.TargetId))
                    .Where(n => n != null)
                    .ToList();
            }
        }

        #endregion

        #region Graph Queries

        /// <summary>
        /// Finds the shortest path between two nodes.
        /// </summary>
        public IEnumerable<KnowledgeNode> FindPath(string startId, string endId, int maxDepth = 10)
        {
            if (string.IsNullOrWhiteSpace(startId)) throw new ArgumentException("Start node ID cannot be null or empty.", nameof(startId));
            if (string.IsNullOrWhiteSpace(endId)) throw new ArgumentException("End node ID cannot be null or empty.", nameof(endId));

            lock (_lock)
            {
                if (!_nodes.ContainsKey(startId) || !_nodes.ContainsKey(endId))
                    return Enumerable.Empty<KnowledgeNode>();

                // BFS for shortest path
                var visited = new HashSet<string>();
                var queue = new Queue<(string NodeId, List<string> Path)>();
                queue.Enqueue((startId, new List<string> { startId }));

                while (queue.Count > 0)
                {
                    var (currentId, path) = queue.Dequeue();

                    if (currentId == endId)
                    {
                        return path.Select(id => _nodes[id]).ToList();
                    }

                    if (path.Count >= maxDepth)
                        continue;

                    if (visited.Contains(currentId))
                        continue;

                    visited.Add(currentId);

                    var edges = _edgesBySource.GetValueOrDefault(currentId) ?? Enumerable.Empty<KnowledgeEdge>();
                    foreach (var edge in edges)
                    {
                        if (!visited.Contains(edge.TargetId))
                        {
                            var newPath = new List<string>(path) { edge.TargetId };
                            queue.Enqueue((edge.TargetId, newPath));
                        }
                    }
                }

                return Enumerable.Empty<KnowledgeNode>();
            }
        }

        /// <summary>
        /// Gets all nodes within a certain distance from a starting node.
        /// </summary>
        public IEnumerable<KnowledgeNode> GetNeighborhood(string nodeId, int depth = 2)
        {
            lock (_lock)
            {
                var result = new HashSet<string>();
                var toVisit = new Queue<(string Id, int Depth)>();
                toVisit.Enqueue((nodeId, 0));

                while (toVisit.Count > 0)
                {
                    var (currentId, currentDepth) = toVisit.Dequeue();

                    if (result.Contains(currentId) || currentDepth > depth)
                        continue;

                    result.Add(currentId);

                    if (currentDepth < depth)
                    {
                        var edges = _edgesBySource.GetValueOrDefault(currentId) ?? Enumerable.Empty<KnowledgeEdge>();
                        foreach (var edge in edges)
                        {
                            toVisit.Enqueue((edge.TargetId, currentDepth + 1));
                        }
                    }
                }

                return result.Select(id => _nodes[id]).ToList();
            }
        }

        /// <summary>
        /// Queries the graph using a pattern (subject, predicate, object).
        /// </summary>
        public IEnumerable<(KnowledgeNode Subject, KnowledgeEdge Predicate, KnowledgeNode Object)> QueryTriple(
            string subjectType = null,
            string relationType = null,
            string objectType = null)
        {
            lock (_lock)
            {
                var results = new List<(KnowledgeNode, KnowledgeEdge, KnowledgeNode)>();

                foreach (var edge in _edges)
                {
                    if (!string.IsNullOrEmpty(relationType) &&
                        !edge.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var source = _nodes.GetValueOrDefault(edge.SourceId);
                    var target = _nodes.GetValueOrDefault(edge.TargetId);

                    if (source == null || target == null)
                        continue;

                    if (!string.IsNullOrEmpty(subjectType) &&
                        !source.NodeType.Equals(subjectType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrEmpty(objectType) &&
                        !target.NodeType.Equals(objectType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    results.Add((source, edge, target));
                }

                return results;
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Saves the graph to a JSON file.
        /// </summary>
        public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                GraphData data;
                lock (_lock)
                {
                    data = new GraphData
                    {
                        Nodes = _nodes.Values.ToList(),
                        Edges = _edges.ToList()
                    };
                }

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await Task.Run(() => File.WriteAllText(filePath, json), cancellationToken);
                Logger.Info($"Graph saved: {NodeCount} nodes, {EdgeCount} edges");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save knowledge graph");
                throw;
            }
        }

        /// <summary>
        /// Loads the graph from a JSON file.
        /// </summary>
        public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Graph file not found: {filePath}");
                    return;
                }

                var json = await Task.Run(() => File.ReadAllText(filePath), cancellationToken);
                var data = JsonConvert.DeserializeObject<GraphData>(json);

                lock (_lock)
                {
                    _nodes.Clear();
                    _edges.Clear();
                    _nodesByType.Clear();
                    _edgesBySource.Clear();
                    _edgesByTarget.Clear();

                    foreach (var node in data.Nodes)
                    {
                        AddNode(node);
                    }

                    foreach (var edge in data.Edges)
                    {
                        AddEdge(edge);
                    }
                }

                Logger.Info($"Graph loaded: {NodeCount} nodes, {EdgeCount} edges");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load knowledge graph");
                throw;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the graph with building design knowledge.
        /// </summary>
        public void InitializeWithBuildingKnowledge()
        {
            Logger.Info("Initializing knowledge graph with building design concepts");

            // Add building concept hierarchy
            AddNode(new KnowledgeNode { Id = "building", Name = "Building", NodeType = "Concept" });
            AddNode(new KnowledgeNode { Id = "spaces", Name = "Spaces", NodeType = "Category" });
            AddNode(new KnowledgeNode { Id = "systems", Name = "Systems", NodeType = "Category" });
            AddNode(new KnowledgeNode { Id = "envelope", Name = "Envelope", NodeType = "Category" });

            AddRelationship("building", "spaces", "contains");
            AddRelationship("building", "systems", "contains");
            AddRelationship("building", "envelope", "contains");

            // Room types
            var roomTypes = new[] { "bedroom", "bathroom", "kitchen", "living_room", "dining_room", "office", "hallway", "closet" };
            foreach (var room in roomTypes)
            {
                AddNode(new KnowledgeNode
                {
                    Id = room,
                    Name = room.Replace("_", " ").ToTitleCase(),
                    NodeType = "RoomType"
                });
                AddRelationship("spaces", room, "includes");
            }

            // Add spatial relationships
            AddRelationship("kitchen", "dining_room", "adjacent_preferred", 0.95f);
            AddRelationship("bedroom", "bathroom", "adjacent_preferred", 0.85f);
            AddRelationship("living_room", "dining_room", "adjacent_preferred", 0.85f);
            AddRelationship("kitchen", "bedroom", "avoid_adjacent", 0.8f);

            // Add element types
            AddNode(new KnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            AddNode(new KnowledgeNode { Id = "floor", Name = "Floor", NodeType = "ElementType" });
            AddNode(new KnowledgeNode { Id = "roof", Name = "Roof", NodeType = "ElementType" });
            AddNode(new KnowledgeNode { Id = "door", Name = "Door", NodeType = "ElementType" });
            AddNode(new KnowledgeNode { Id = "window", Name = "Window", NodeType = "ElementType" });

            AddRelationship("envelope", "wall", "includes");
            AddRelationship("envelope", "floor", "includes");
            AddRelationship("envelope", "roof", "includes");
            AddRelationship("wall", "door", "can_contain");
            AddRelationship("wall", "window", "can_contain");

            // Add materials
            AddNode(new KnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            AddNode(new KnowledgeNode { Id = "steel", Name = "Steel", NodeType = "Material" });
            AddNode(new KnowledgeNode { Id = "timber", Name = "Timber", NodeType = "Material" });
            AddNode(new KnowledgeNode { Id = "glass", Name = "Glass", NodeType = "Material" });

            AddRelationship("wall", "concrete", "made_of");
            AddRelationship("wall", "timber", "made_of");
            AddRelationship("floor", "concrete", "made_of");
            AddRelationship("window", "glass", "made_of");

            Logger.Info($"Initialized with {NodeCount} nodes and {EdgeCount} edges");
        }

        #endregion

        private float CalculateRelevance(KnowledgeNode node, string query)
        {
            var score = 0f;
            if (node.Name.ToLowerInvariant().Contains(query))
                score += 1f;
            if (node.Description?.ToLowerInvariant().Contains(query) == true)
                score += 0.5f;
            return score;
        }
    }

    #region Supporting Classes

    public class KnowledgeNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public float[] Embedding { get; set; }
    }

    public class KnowledgeEdge
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string RelationType { get; set; }
        public float Strength { get; set; } = 1.0f;
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    internal class GraphData
    {
        public List<KnowledgeNode> Nodes { get; set; }
        public List<KnowledgeEdge> Edges { get; set; }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
        }
    }

    #endregion
}
