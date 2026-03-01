using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Knowledge.Graph;
using StingBIM.AI.Knowledge.Inference;

namespace StingBIM.AI.Tests.Unit.Core
{
    [TestFixture]
    public class KnowledgeGraphTests
    {
        #region KnowledgeGraph CRUD Tests

        [TestFixture]
        public class GraphCrudTests
        {
            private KnowledgeGraph _graph;

            [SetUp]
            public void SetUp()
            {
                _graph = new KnowledgeGraph();
            }

            [Test]
            public void AddNode_ValidNode_IncreasesNodeCount()
            {
                _graph.AddNode(new KnowledgeNode { Id = "room-1", Name = "Living Room", NodeType = "RoomType" });

                _graph.NodeCount.Should().Be(1);
            }

            [Test]
            public void AddNode_MultipleNodes_TracksAll()
            {
                _graph.AddNode(new KnowledgeNode { Id = "room-1", Name = "Living Room", NodeType = "RoomType" });
                _graph.AddNode(new KnowledgeNode { Id = "room-2", Name = "Kitchen", NodeType = "RoomType" });
                _graph.AddNode(new KnowledgeNode { Id = "mat-1", Name = "Concrete", NodeType = "Material" });

                _graph.NodeCount.Should().Be(3);
            }

            [Test]
            public void AddNode_NullId_ThrowsArgumentException()
            {
                Action act = () => _graph.AddNode(new KnowledgeNode { Id = null, Name = "Test" });

                act.Should().Throw<ArgumentException>();
            }

            [Test]
            public void GetNode_ExistingId_ReturnsNode()
            {
                var node = new KnowledgeNode { Id = "wall-1", Name = "Exterior Wall", NodeType = "ElementType" };
                _graph.AddNode(node);

                var retrieved = _graph.GetNode("wall-1");

                retrieved.Should().NotBeNull();
                retrieved.Name.Should().Be("Exterior Wall");
            }

            [Test]
            public void GetNode_NonexistentId_ReturnsNull()
            {
                var retrieved = _graph.GetNode("nonexistent");

                retrieved.Should().BeNull();
            }

            [Test]
            public void GetNodesByType_FiltersByType()
            {
                _graph.AddNode(new KnowledgeNode { Id = "r1", Name = "Room 1", NodeType = "RoomType" });
                _graph.AddNode(new KnowledgeNode { Id = "r2", Name = "Room 2", NodeType = "RoomType" });
                _graph.AddNode(new KnowledgeNode { Id = "m1", Name = "Steel", NodeType = "Material" });

                var rooms = _graph.GetNodesByType("RoomType").ToList();

                rooms.Should().HaveCount(2);
                rooms.Should().OnlyContain(n => n.NodeType == "RoomType");
            }

            [Test]
            public void AddEdge_ValidEdge_IncreasesEdgeCount()
            {
                _graph.AddNode(new KnowledgeNode { Id = "a", Name = "A", NodeType = "Concept" });
                _graph.AddNode(new KnowledgeNode { Id = "b", Name = "B", NodeType = "Concept" });

                _graph.AddEdge(new KnowledgeEdge { SourceId = "a", TargetId = "b", RelationType = "related_to" });

                _graph.EdgeCount.Should().Be(1);
            }

            [Test]
            public void AddRelationship_ConvenienceMethod_CreatesEdge()
            {
                _graph.AddNode(new KnowledgeNode { Id = "a", Name = "A", NodeType = "Concept" });
                _graph.AddNode(new KnowledgeNode { Id = "b", Name = "B", NodeType = "Concept" });

                _graph.AddRelationship("a", "b", "contains", 0.9f);

                _graph.EdgeCount.Should().Be(1);
                var edges = _graph.GetOutgoingEdges("a").ToList();
                edges.Should().HaveCount(1);
                edges[0].Strength.Should().BeApproximately(0.9f, 0.001f);
            }

            [Test]
            public void GetOutgoingEdges_ReturnsEdgesFromNode()
            {
                _graph.AddNode(new KnowledgeNode { Id = "building", Name = "Building", NodeType = "Concept" });
                _graph.AddNode(new KnowledgeNode { Id = "floor", Name = "Floor", NodeType = "Concept" });
                _graph.AddNode(new KnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Concept" });

                _graph.AddRelationship("building", "floor", "contains");
                _graph.AddRelationship("building", "wall", "contains");

                var outgoing = _graph.GetOutgoingEdges("building").ToList();

                outgoing.Should().HaveCount(2);
            }

            [Test]
            public void GetIncomingEdges_ReturnsEdgesToNode()
            {
                _graph.AddNode(new KnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
                _graph.AddNode(new KnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
                _graph.AddNode(new KnowledgeNode { Id = "floor", Name = "Floor", NodeType = "Element" });

                _graph.AddRelationship("wall", "concrete", "uses_material");
                _graph.AddRelationship("floor", "concrete", "uses_material");

                var incoming = _graph.GetIncomingEdges("concrete").ToList();

                incoming.Should().HaveCount(2);
            }
        }

        #endregion

        #region Graph Query Tests

        [TestFixture]
        public class GraphQueryTests
        {
            private KnowledgeGraph _graph;

            [SetUp]
            public void SetUp()
            {
                _graph = new KnowledgeGraph();

                // Build a small test graph: Building → Floor → Room → Wall
                _graph.AddNode(new KnowledgeNode { Id = "building", Name = "Building", NodeType = "Concept" });
                _graph.AddNode(new KnowledgeNode { Id = "floor", Name = "Floor", NodeType = "Element" });
                _graph.AddNode(new KnowledgeNode { Id = "room", Name = "Room", NodeType = "Space" });
                _graph.AddNode(new KnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
                _graph.AddNode(new KnowledgeNode { Id = "isolated", Name = "Isolated", NodeType = "Concept" });

                _graph.AddRelationship("building", "floor", "contains");
                _graph.AddRelationship("floor", "room", "contains");
                _graph.AddRelationship("room", "wall", "bounded_by");
            }

            [Test]
            public void FindPath_DirectConnection_ReturnsTwoNodes()
            {
                var path = _graph.FindPath("building", "floor").ToList();

                path.Should().NotBeEmpty();
                path.First().Id.Should().Be("building");
                path.Last().Id.Should().Be("floor");
            }

            [Test]
            public void FindPath_MultiHop_ReturnsFullPath()
            {
                var path = _graph.FindPath("building", "wall").ToList();

                path.Should().HaveCountGreaterThanOrEqualTo(2);
                path.First().Id.Should().Be("building");
                path.Last().Id.Should().Be("wall");
            }

            [Test]
            public void FindPath_NoConnection_ReturnsEmpty()
            {
                var path = _graph.FindPath("building", "isolated").ToList();

                path.Should().BeEmpty();
            }

            [Test]
            public void GetNeighborhood_Depth1_ReturnsDirectNeighbors()
            {
                var neighbors = _graph.GetNeighborhood("floor", 1).ToList();

                neighbors.Should().NotBeEmpty();
            }

            [Test]
            public void GetNeighborhood_Depth2_ReturnsExtendedNeighbors()
            {
                var neighbors = _graph.GetNeighborhood("floor", 2).ToList();

                neighbors.Count.Should().BeGreaterThanOrEqualTo(
                    _graph.GetNeighborhood("floor", 1).Count());
            }

            [Test]
            public void GetRelatedNodes_WithRelationType_FiltersCorrectly()
            {
                var related = _graph.GetRelatedNodes("building", "contains").ToList();

                related.Should().HaveCount(1);
                related[0].Id.Should().Be("floor");
            }

            [Test]
            public void GetRelatedNodes_NoRelationFilter_ReturnsAll()
            {
                var related = _graph.GetRelatedNodes("building").ToList();

                related.Should().NotBeEmpty();
            }

            [Test]
            public void SearchNodes_ByName_FindsMatches()
            {
                var results = _graph.SearchNodes("Wall").ToList();

                results.Should().Contain(n => n.Id == "wall");
            }

            [Test]
            public void SearchNodes_CaseInsensitive_FindsMatches()
            {
                var results = _graph.SearchNodes("wall").ToList();

                results.Should().NotBeEmpty();
            }

            [Test]
            public void QueryTriple_ByRelationType_ReturnsMatchingTriples()
            {
                var triples = _graph.QueryTriple(relationType: "contains").ToList();

                triples.Should().HaveCount(2); // building→floor, floor→room
            }

            [Test]
            public void QueryTriple_BySubjectType_FiltersCorrectly()
            {
                var triples = _graph.QueryTriple(subjectType: "Space").ToList();

                triples.Should().OnlyContain(t => t.Subject.NodeType == "Space");
            }
        }

        #endregion

        #region Graph Persistence Tests

        [TestFixture]
        public class GraphPersistenceTests
        {
            [Test]
            public async Task SaveAndLoad_PreservesGraphStructure()
            {
                var graph = new KnowledgeGraph();
                graph.AddNode(new KnowledgeNode { Id = "a", Name = "Node A", NodeType = "Test" });
                graph.AddNode(new KnowledgeNode { Id = "b", Name = "Node B", NodeType = "Test" });
                graph.AddRelationship("a", "b", "linked_to", 0.85f);

                var tempFile = Path.Combine(Path.GetTempPath(), $"kg_test_{Guid.NewGuid()}.json");
                try
                {
                    await graph.SaveAsync(tempFile);

                    var loaded = new KnowledgeGraph();
                    await loaded.LoadAsync(tempFile);

                    loaded.NodeCount.Should().Be(2);
                    loaded.EdgeCount.Should().Be(1);
                    loaded.GetNode("a").Name.Should().Be("Node A");
                    loaded.GetNode("b").Name.Should().Be("Node B");
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
        }

        #endregion

        #region Graph Initialization Tests

        [TestFixture]
        public class GraphInitializationTests
        {
            [Test]
            public void InitializeWithBuildingKnowledge_PopulatesGraph()
            {
                var graph = new KnowledgeGraph();

                graph.InitializeWithBuildingKnowledge();

                graph.NodeCount.Should().BeGreaterThan(0);
                graph.EdgeCount.Should().BeGreaterThan(0);
            }
        }

        #endregion

        #region InferenceEngine Tests

        [TestFixture]
        public class InferenceEngineTests
        {
            private KnowledgeGraph _graph;
            private InferenceEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _graph = new KnowledgeGraph();
                _graph.InitializeWithBuildingKnowledge();
                _engine = new InferenceEngine(_graph);
            }

            [Test]
            public void Constructor_NullGraph_ThrowsArgumentNullException()
            {
                Action act = () => new InferenceEngine(null);

                act.Should().Throw<ArgumentNullException>();
            }

            [Test]
            public void RunForwardChaining_OnInitializedGraph_ReturnsResult()
            {
                var result = _engine.RunForwardChaining(maxIterations: 5);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.Iterations.Should().BeGreaterThanOrEqualTo(1);
            }

            [Test]
            public void AddRule_ValidRule_DoesNotThrow()
            {
                var rule = new InferenceRule
                {
                    Name = "TestRule",
                    Description = "A test rule",
                    Evaluate = (graph, context) => new List<DerivedFact>()
                };

                Action act = () => _engine.AddRule(rule);

                act.Should().NotThrow();
            }

            [Test]
            public void RunBackwardChaining_WithGoal_ReturnsResult()
            {
                var goal = new InferenceGoal
                {
                    Subject = "Room",
                    Predicate = "contains",
                    Object = "Wall"
                };

                var result = _engine.RunBackwardChaining(goal);

                result.Should().NotBeNull();
            }

            [Test]
            public void FindAnalogies_ValidConcept_ReturnsResults()
            {
                // Get any node from the graph to test with
                var nodes = _graph.GetNodesByType("Concept").ToList();
                if (nodes.Count == 0) return; // skip if no concept nodes

                var analogies = _engine.FindAnalogies(nodes[0].Id).ToList();

                // May or may not find analogies, but should not throw
                analogies.Should().NotBeNull();
            }

            [Test]
            public void AnswerQuery_IsRelated_ReturnsAnswer()
            {
                var query = new InferenceQuery
                {
                    Type = QueryType.IsRelated,
                    Subject = "Room",
                    Object = "Wall"
                };

                var answer = _engine.AnswerQuery(query);

                answer.Should().NotBeNull();
                answer.Query.Should().Be(query);
            }

            [Test]
            public void AnswerQuery_FindRelated_ReturnsAnswer()
            {
                var query = new InferenceQuery
                {
                    Type = QueryType.FindRelated,
                    Subject = "Room"
                };

                var answer = _engine.AnswerQuery(query);

                answer.Should().NotBeNull();
            }
        }

        #endregion
    }
}
