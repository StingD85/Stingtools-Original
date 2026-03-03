// StingBIM.AI.Tests - KnowledgeGraphTests.cs
// Unit tests for Knowledge Graph system
// Validates node/edge operations, graph queries, and initialization
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Knowledge
{
    /// <summary>
    /// Unit tests for Knowledge Graph with nodes, edges, and graph queries.
    /// </summary>
    [TestFixture]
    public class KnowledgeGraphTests
    {
        private TestKnowledgeGraph _graph;

        [SetUp]
        public void Setup()
        {
            _graph = new TestKnowledgeGraph();
        }

        #region AddNode / GetNode Tests

        [Test]
        public void AddNode_ValidNode_IncreasesNodeCount()
        {
            // Arrange
            var initialCount = _graph.NodeCount;
            var node = new TestKnowledgeNode
            {
                Id = "test-node-1",
                Name = "Test Node",
                NodeType = "TestType"
            };

            // Act
            _graph.AddNode(node);

            // Assert
            _graph.NodeCount.Should().Be(initialCount + 1);
        }

        [Test]
        public void AddNode_StoresNodeRetrievableByGetNode()
        {
            // Arrange
            var node = new TestKnowledgeNode
            {
                Id = "wall_01",
                Name = "Exterior Wall",
                NodeType = "ElementType"
            };

            // Act
            _graph.AddNode(node);
            var retrieved = _graph.GetNode("wall_01");

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be("wall_01");
            retrieved.Name.Should().Be("Exterior Wall");
            retrieved.NodeType.Should().Be("ElementType");
        }

        [Test]
        public void AddNode_NullNode_ThrowsArgumentException()
        {
            // Act
            Action act = () => _graph.AddNode(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddNode_NodeWithoutId_ThrowsArgumentException()
        {
            // Arrange
            var node = new TestKnowledgeNode { Name = "No ID Node" };

            // Act
            Action act = () => _graph.AddNode(node);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddNode_WithNullId_ThrowsArgumentException()
        {
            // Arrange
            var node = new TestKnowledgeNode
            {
                Id = null,
                Name = "No ID Node",
                NodeType = "Test"
            };

            // Act
            Action act = () => _graph.AddNode(node);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddNode_WithEmptyId_ThrowsArgumentException()
        {
            // Arrange
            var node = new TestKnowledgeNode
            {
                Id = "",
                Name = "Empty ID Node",
                NodeType = "Test"
            };

            // Act
            Action act = () => _graph.AddNode(node);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddNode_DuplicateId_UpdatesExistingNode()
        {
            // Arrange
            var node1 = new TestKnowledgeNode
            {
                Id = "duplicate-id",
                Name = "Original Name",
                NodeType = "Type1"
            };
            var node2 = new TestKnowledgeNode
            {
                Id = "duplicate-id",
                Name = "Updated Name",
                NodeType = "Type1"
            };

            // Act
            _graph.AddNode(node1);
            var countAfterFirst = _graph.NodeCount;
            _graph.AddNode(node2);

            // Assert
            _graph.NodeCount.Should().Be(countAfterFirst); // Count should not increase
            _graph.GetNode("duplicate-id").Name.Should().Be("Updated Name");
        }

        [Test]
        public void AddNode_UpdatesExistingNodeWithSameId()
        {
            // Arrange
            var original = new TestKnowledgeNode
            {
                Id = "wall_01",
                Name = "Original Wall",
                NodeType = "ElementType"
            };
            var updated = new TestKnowledgeNode
            {
                Id = "wall_01",
                Name = "Updated Wall",
                NodeType = "ElementType"
            };

            // Act
            _graph.AddNode(original);
            _graph.AddNode(updated);
            var retrieved = _graph.GetNode("wall_01");

            // Assert
            retrieved.Name.Should().Be("Updated Wall");
            _graph.NodeCount.Should().Be(1, "updating a node should not increase the count");
        }

        [Test]
        public void GetNode_ExistingNode_ReturnsNode()
        {
            // Arrange
            var node = new TestKnowledgeNode
            {
                Id = "retrievable-node",
                Name = "Test Node",
                NodeType = "TestType"
            };
            _graph.AddNode(node);

            // Act
            var retrieved = _graph.GetNode("retrievable-node");

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Name.Should().Be("Test Node");
        }

        [Test]
        public void GetNode_NonExistentNode_ReturnsNull()
        {
            // Act
            var result = _graph.GetNode("non-existent-id");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GetNode_CaseInsensitive_FindsNode()
        {
            // Arrange
            var node = new TestKnowledgeNode
            {
                Id = "CamelCaseId",
                Name = "Test",
                NodeType = "Type"
            };
            _graph.AddNode(node);

            // Act
            var result = _graph.GetNode("camelcaseid");

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region NodeCount Tests

        [Test]
        public void NodeCount_TracksCorrectly()
        {
            // Act & Assert - starts at 0
            _graph.NodeCount.Should().Be(0);

            // Add first node
            _graph.AddNode(new TestKnowledgeNode { Id = "n1", Name = "Node 1", NodeType = "Type1" });
            _graph.NodeCount.Should().Be(1);

            // Add second node
            _graph.AddNode(new TestKnowledgeNode { Id = "n2", Name = "Node 2", NodeType = "Type1" });
            _graph.NodeCount.Should().Be(2);

            // Add third node of different type
            _graph.AddNode(new TestKnowledgeNode { Id = "n3", Name = "Node 3", NodeType = "Type2" });
            _graph.NodeCount.Should().Be(3);
        }

        #endregion

        #region GetNodesByType Tests

        [Test]
        public void GetNodesByType_ReturnsNodesOfMatchingType()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall_01", Name = "Wall 1", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "wall_02", Name = "Wall 2", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });

            // Act
            var elements = _graph.GetNodesByType("ElementType").ToList();
            var materials = _graph.GetNodesByType("Material").ToList();

            // Assert
            elements.Should().HaveCount(2);
            elements.Should().OnlyContain(n => n.NodeType == "ElementType");
            materials.Should().HaveCount(1);
            materials.First().Name.Should().Be("Concrete");
        }

        [Test]
        public void GetNodesByType_ExistingType_ReturnsNodes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall-1", Name = "Wall 1", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "wall-2", Name = "Wall 2", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "room-1", Name = "Room 1", NodeType = "Space" });

            // Act
            var elements = _graph.GetNodesByType("Element");

            // Assert
            elements.Should().HaveCount(2);
            elements.All(e => e.NodeType == "Element").Should().BeTrue();
        }

        [Test]
        public void GetNodesByType_NonExistentType_ReturnsEmpty()
        {
            // Act
            var result = _graph.GetNodesByType("NonExistentType");

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region SearchNodes Tests

        [Test]
        public void SearchNodes_FindsByName()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "ext_wall", Name = "Exterior Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "int_wall", Name = "Interior Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "floor_01", Name = "Concrete Floor", NodeType = "ElementType" });

            // Act
            var results = _graph.SearchNodes("wall").ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(n => n.Id == "ext_wall");
            results.Should().Contain(n => n.Id == "int_wall");
        }

        [Test]
        public void SearchNodes_ByName_ReturnsMatchingNodes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "1", Name = "Brick Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "2", Name = "Concrete Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "3", Name = "Door", NodeType = "Element" });

            // Act
            var results = _graph.SearchNodes("wall");

            // Assert
            results.Should().HaveCount(2);
        }

        [Test]
        public void SearchNodes_ByDescription_ReturnsMatchingNodes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode
            {
                Id = "1",
                Name = "Wall Type A",
                Description = "Exterior insulated wall",
                NodeType = "Element"
            });

            // Act
            var results = _graph.SearchNodes("insulated");

            // Assert
            results.Should().HaveCount(1);
        }

        [Test]
        public void SearchNodes_RespectMaxResults()
        {
            // Arrange
            for (int i = 0; i < 20; i++)
            {
                _graph.AddNode(new TestKnowledgeNode
                {
                    Id = $"wall-{i}",
                    Name = $"Wall {i}",
                    NodeType = "Element"
                });
            }

            // Act
            var results = _graph.SearchNodes("wall", maxResults: 5);

            // Assert
            results.Should().HaveCount(5);
        }

        #endregion

        #region Edge Operations Tests

        [Test]
        public void AddEdge_ValidEdge_IncreasesEdgeCount()
        {
            // Arrange
            var initialCount = _graph.EdgeCount;
            var edge = new TestKnowledgeEdge
            {
                SourceId = "node-a",
                TargetId = "node-b",
                RelationType = "connects"
            };

            // Act
            _graph.AddEdge(edge);

            // Assert
            _graph.EdgeCount.Should().Be(initialCount + 1);
        }

        [Test]
        public void AddEdge_StoresEdgeAndIndexesBySourceAndTarget()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });

            var edge = new TestKnowledgeEdge
            {
                SourceId = "wall",
                TargetId = "concrete",
                RelationType = "made_of",
                Strength = 0.9f
            };

            // Act
            _graph.AddEdge(edge);

            // Assert
            _graph.EdgeCount.Should().Be(1);

            var outgoing = _graph.GetOutgoingEdges("wall").ToList();
            outgoing.Should().HaveCount(1);
            outgoing.First().TargetId.Should().Be("concrete");
            outgoing.First().RelationType.Should().Be("made_of");

            var incoming = _graph.GetIncomingEdges("concrete").ToList();
            incoming.Should().HaveCount(1);
            incoming.First().SourceId.Should().Be("wall");
        }

        [Test]
        public void AddEdge_NullEdge_ThrowsArgumentException()
        {
            // Act
            Action act = () => _graph.AddEdge(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddEdge_MissingSourceId_ThrowsArgumentException()
        {
            // Arrange
            var edge = new TestKnowledgeEdge
            {
                TargetId = "node-b",
                RelationType = "connects"
            };

            // Act
            Action act = () => _graph.AddEdge(edge);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AddEdge_WithNullTarget_ThrowsArgumentException()
        {
            // Arrange
            var edge = new TestKnowledgeEdge
            {
                SourceId = "wall",
                TargetId = null,
                RelationType = "made_of"
            };

            // Act
            Action act = () => _graph.AddEdge(edge);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region EdgeCount Tests

        [Test]
        public void EdgeCount_TracksCorrectly()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "c", Name = "C", NodeType = "Type" });

            // Act & Assert - starts at 0
            _graph.EdgeCount.Should().Be(0);

            _graph.AddEdge(new TestKnowledgeEdge { SourceId = "a", TargetId = "b", RelationType = "rel1" });
            _graph.EdgeCount.Should().Be(1);

            _graph.AddEdge(new TestKnowledgeEdge { SourceId = "b", TargetId = "c", RelationType = "rel2" });
            _graph.EdgeCount.Should().Be(2);

            _graph.AddEdge(new TestKnowledgeEdge { SourceId = "a", TargetId = "c", RelationType = "rel3" });
            _graph.EdgeCount.Should().Be(3);
        }

        #endregion

        #region AddRelationship Tests

        [Test]
        public void AddRelationship_CreatesEdge()
        {
            // Arrange
            var initialCount = _graph.EdgeCount;

            // Act
            _graph.AddRelationship("node-a", "node-b", "contains");

            // Assert
            _graph.EdgeCount.Should().Be(initialCount + 1);
        }

        [Test]
        public void AddRelationship_CreatesEdgeWithSpecifiedStrength()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "kitchen", Name = "Kitchen", NodeType = "RoomType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "dining", Name = "Dining Room", NodeType = "RoomType" });

            // Act
            _graph.AddRelationship("kitchen", "dining", "adjacent_preferred", 0.95f);

            // Assert
            _graph.EdgeCount.Should().Be(1);
            var edges = _graph.GetOutgoingEdges("kitchen").ToList();
            edges.Should().HaveCount(1);
            edges.First().TargetId.Should().Be("dining");
            edges.First().RelationType.Should().Be("adjacent_preferred");
            edges.First().Strength.Should().Be(0.95f);
        }

        [Test]
        public void AddRelationship_WithStrength_SetsStrength()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "source", Name = "Source", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "target", Name = "Target", NodeType = "Test" });

            // Act
            _graph.AddRelationship("source", "target", "related", 0.75f);

            // Assert
            var edges = _graph.GetOutgoingEdges("source").ToList();
            edges.Should().ContainSingle();
            edges.First().Strength.Should().Be(0.75f);
        }

        [Test]
        public void GetOutgoingEdges_ReturnsEdgesFromSource()
        {
            // Arrange
            _graph.AddRelationship("center", "a", "connects");
            _graph.AddRelationship("center", "b", "connects");
            _graph.AddRelationship("other", "center", "connects");

            // Act
            var edges = _graph.GetOutgoingEdges("center");

            // Assert
            edges.Should().HaveCount(2);
            edges.All(e => e.SourceId == "center").Should().BeTrue();
        }

        [Test]
        public void GetIncomingEdges_ReturnsEdgesToTarget()
        {
            // Arrange
            _graph.AddRelationship("a", "center", "connects");
            _graph.AddRelationship("b", "center", "connects");
            _graph.AddRelationship("center", "other", "connects");

            // Act
            var edges = _graph.GetIncomingEdges("center");

            // Assert
            edges.Should().HaveCount(2);
            edges.All(e => e.TargetId == "center").Should().BeTrue();
        }

        #endregion

        #region GetRelatedNodes Tests

        [Test]
        public void GetRelatedNodes_ReturnsConnectedNodes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "window", Name = "Window", NodeType = "Element" });
            _graph.AddRelationship("wall", "door", "contains");
            _graph.AddRelationship("wall", "window", "contains");

            // Act
            var related = _graph.GetRelatedNodes("wall");

            // Assert
            related.Should().HaveCount(2);
        }

        [Test]
        public void GetRelatedNodes_FiltersByRelationshipType()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddNode(new TestKnowledgeNode { Id = "timber", Name = "Timber", NodeType = "Material" });
            _graph.AddRelationship("wall", "concrete", "made_of");
            _graph.AddRelationship("wall", "timber", "made_of");

            // Act
            var related = _graph.GetRelatedNodes("wall", "made_of").ToList();

            // Assert
            related.Should().HaveCount(2);
            related.Should().Contain(n => n.Id == "concrete");
            related.Should().Contain(n => n.Id == "timber");
        }

        [Test]
        public void GetRelatedNodes_FilterByRelationType()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "Element" });
            _graph.AddRelationship("wall", "concrete", "made_of");
            _graph.AddRelationship("wall", "door", "contains");

            // Act
            var related = _graph.GetRelatedNodes("wall", "made_of");

            // Assert
            related.Should().HaveCount(1);
            related.First().Id.Should().Be("concrete");
        }

        [Test]
        public void GetRelatedNodes_FiltersByMultipleRelationTypes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "ElementType" });
            _graph.AddRelationship("wall", "concrete", "made_of");
            _graph.AddRelationship("wall", "door", "can_contain");

            // Act
            var madeOfRelated = _graph.GetRelatedNodes("wall", "made_of").ToList();
            var containRelated = _graph.GetRelatedNodes("wall", "can_contain").ToList();

            // Assert
            madeOfRelated.Should().HaveCount(1);
            madeOfRelated.First().Id.Should().Be("concrete");

            containRelated.Should().HaveCount(1);
            containRelated.First().Id.Should().Be("door");
        }

        #endregion

        #region FindPath / Graph Query Tests

        [Test]
        public void FindPath_DirectConnection_ReturnsTwoNodes()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Test" });
            _graph.AddRelationship("a", "b", "connects");

            // Act
            var path = _graph.FindPath("a", "b");

            // Assert
            path.Should().HaveCount(2);
            path.First().Id.Should().Be("a");
            path.Last().Id.Should().Be("b");
        }

        [Test]
        public void FindPath_IndirectConnection_ReturnsPath()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "c", Name = "C", NodeType = "Test" });
            _graph.AddRelationship("a", "b", "connects");
            _graph.AddRelationship("b", "c", "connects");

            // Act
            var path = _graph.FindPath("a", "c");

            // Assert
            path.Should().HaveCount(3);
        }

        [Test]
        public void FindPath_ReturnsShortestPathBFS()
        {
            // Arrange - create a graph: A -> B -> C -> D, and A -> D (shortcut)
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "c", Name = "C", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "d", Name = "D", NodeType = "Type" });

            _graph.AddRelationship("a", "b", "connects");
            _graph.AddRelationship("b", "c", "connects");
            _graph.AddRelationship("c", "d", "connects");
            _graph.AddRelationship("a", "d", "shortcut");

            // Act
            var path = _graph.FindPath("a", "d").ToList();

            // Assert - should find direct path A -> D (length 2)
            path.Should().HaveCount(2);
            path.First().Id.Should().Be("a");
            path.Last().Id.Should().Be("d");
        }

        [Test]
        public void FindPath_NoConnection_ReturnsEmpty()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "isolated-a", Name = "A", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "isolated-b", Name = "B", NodeType = "Test" });

            // Act
            var path = _graph.FindPath("isolated-a", "isolated-b");

            // Assert
            path.Should().BeEmpty();
        }

        [Test]
        public void FindPath_ReturnsEmptyForUnreachableNodes()
        {
            // Arrange - create disconnected nodes
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "c", Name = "C", NodeType = "Type" });

            _graph.AddRelationship("a", "b", "connects");
            // No path from a or b to c

            // Act
            var path = _graph.FindPath("a", "c").ToList();

            // Assert
            path.Should().BeEmpty();
        }

        [Test]
        public void FindPath_SameStartAndEnd_ReturnsSingleNode()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "self", Name = "Self", NodeType = "Test" });

            // Act
            var path = _graph.FindPath("self", "self");

            // Assert
            path.Should().HaveCount(1);
        }

        #endregion

        #region GetNeighborhood Tests

        [Test]
        public void GetNeighborhood_Depth1_ReturnsDirectConnections()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "center", Name = "Center", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "n1", Name = "N1", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "n2", Name = "N2", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "far", Name = "Far", NodeType = "Test" });
            _graph.AddRelationship("center", "n1", "connects");
            _graph.AddRelationship("center", "n2", "connects");
            _graph.AddRelationship("n1", "far", "connects");

            // Act
            var neighborhood = _graph.GetNeighborhood("center", depth: 1);

            // Assert
            neighborhood.Should().HaveCount(3); // center + n1 + n2
            neighborhood.Select(n => n.Id).Should().Contain("center");
            neighborhood.Select(n => n.Id).Should().Contain("n1");
            neighborhood.Select(n => n.Id).Should().Contain("n2");
        }

        [Test]
        public void GetNeighborhood_Depth2_IncludesSecondLevelConnections()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "center", Name = "Center", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "n1", Name = "N1", NodeType = "Test" });
            _graph.AddNode(new TestKnowledgeNode { Id = "far", Name = "Far", NodeType = "Test" });
            _graph.AddRelationship("center", "n1", "connects");
            _graph.AddRelationship("n1", "far", "connects");

            // Act
            var neighborhood = _graph.GetNeighborhood("center", depth: 2);

            // Assert
            neighborhood.Should().HaveCount(3);
            neighborhood.Select(n => n.Id).Should().Contain("far");
        }

        [Test]
        public void GetNeighborhood_ReturnsNodesWithinDepth()
        {
            // Arrange - create: A -> B -> C -> D
            _graph.AddNode(new TestKnowledgeNode { Id = "a", Name = "A", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "b", Name = "B", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "c", Name = "C", NodeType = "Type" });
            _graph.AddNode(new TestKnowledgeNode { Id = "d", Name = "D", NodeType = "Type" });

            _graph.AddRelationship("a", "b", "connects");
            _graph.AddRelationship("b", "c", "connects");
            _graph.AddRelationship("c", "d", "connects");

            // Act - depth 1 should get A and B
            var depth1 = _graph.GetNeighborhood("a", 1).ToList();
            // depth 2 should get A, B, and C
            var depth2 = _graph.GetNeighborhood("a", 2).ToList();

            // Assert
            depth1.Should().HaveCount(2);
            depth1.Should().Contain(n => n.Id == "a");
            depth1.Should().Contain(n => n.Id == "b");

            depth2.Should().HaveCount(3);
            depth2.Should().Contain(n => n.Id == "a");
            depth2.Should().Contain(n => n.Id == "b");
            depth2.Should().Contain(n => n.Id == "c");
        }

        #endregion

        #region QueryTriple Tests

        [Test]
        public void QueryTriple_ByRelationType_ReturnsMatchingTriples()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "Element" });
            _graph.AddRelationship("wall", "concrete", "made_of");
            _graph.AddRelationship("wall", "door", "contains");

            // Act
            var triples = _graph.QueryTriple(relationType: "made_of");

            // Assert
            triples.Should().HaveCount(1);
            var triple = triples.First();
            triple.Subject.Id.Should().Be("wall");
            triple.Object.Id.Should().Be("concrete");
        }

        [Test]
        public void QueryTriple_BySubjectType_ReturnsMatchingTriples()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "Element" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddRelationship("wall", "concrete", "made_of");

            // Act
            var triples = _graph.QueryTriple(subjectType: "Element");

            // Assert
            triples.Should().HaveCount(1);
        }

        [Test]
        public void QueryTriple_MatchesBySubjectTypeRelationTypeObjectType()
        {
            // Arrange
            _graph.AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            _graph.AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "ElementType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "kitchen", Name = "Kitchen", NodeType = "RoomType" });
            _graph.AddNode(new TestKnowledgeNode { Id = "dining", Name = "Dining Room", NodeType = "RoomType" });

            _graph.AddRelationship("wall", "concrete", "made_of");
            _graph.AddRelationship("wall", "door", "can_contain");
            _graph.AddRelationship("kitchen", "dining", "adjacent_preferred");

            // Act - query for ElementType --made_of--> Material
            var results = _graph.QueryTriple(
                subjectType: "ElementType",
                relationType: "made_of",
                objectType: "Material").ToList();

            // Assert
            results.Should().HaveCount(1);
            results.First().Subject.Id.Should().Be("wall");
            results.First().Predicate.RelationType.Should().Be("made_of");
            results.First().Object.Id.Should().Be("concrete");
        }

        #endregion

        #region Initialization Tests

        [Test]
        public void InitializeWithBuildingKnowledge_AddsBasicConcepts()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert
            _graph.NodeCount.Should().BeGreaterThan(0);
            _graph.EdgeCount.Should().BeGreaterThan(0);
        }

        [Test]
        public void InitializeWithBuildingKnowledge_CreatesRoomsElementsMaterials()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert - verify rooms exist
            var rooms = _graph.GetNodesByType("RoomType").ToList();
            rooms.Should().NotBeEmpty();
            rooms.Should().Contain(n => n.Id == "bedroom");
            rooms.Should().Contain(n => n.Id == "kitchen");
            rooms.Should().Contain(n => n.Id == "bathroom");
            rooms.Should().Contain(n => n.Id == "living_room");

            // Assert - verify elements exist
            var elements = _graph.GetNodesByType("ElementType").ToList();
            elements.Should().NotBeEmpty();
            elements.Should().Contain(n => n.Id == "wall");
            elements.Should().Contain(n => n.Id == "floor");
            elements.Should().Contain(n => n.Id == "door");
            elements.Should().Contain(n => n.Id == "window");

            // Assert - verify materials exist
            var materials = _graph.GetNodesByType("Material").ToList();
            materials.Should().NotBeEmpty();
            materials.Should().Contain(n => n.Id == "concrete");
            materials.Should().Contain(n => n.Id == "steel");
            materials.Should().Contain(n => n.Id == "timber");
            materials.Should().Contain(n => n.Id == "glass");

            // Assert - verify overall counts are reasonable
            _graph.NodeCount.Should().BeGreaterThan(15);
            _graph.EdgeCount.Should().BeGreaterThan(10);
        }

        [Test]
        public void InitializeWithBuildingKnowledge_ContainsElementTypes()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert
            var wall = _graph.GetNode("wall");
            wall.Should().NotBeNull();
            wall.NodeType.Should().Be("ElementType");
        }

        [Test]
        public void InitializeWithBuildingKnowledge_ContainsMaterials()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert
            var concrete = _graph.GetNode("concrete");
            concrete.Should().NotBeNull();
            concrete.NodeType.Should().Be("Material");
        }

        [Test]
        public void InitializeWithBuildingKnowledge_ContainsSpatialRelationships()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert
            var kitchenEdges = _graph.GetOutgoingEdges("kitchen");
            kitchenEdges.Any(e => e.RelationType == "adjacent_preferred").Should().BeTrue();
        }

        [Test]
        public void InitializeWithBuildingKnowledge_CreatesAdjacencyRelationships()
        {
            // Act
            _graph.InitializeWithBuildingKnowledge();

            // Assert - kitchen should be adjacent_preferred to dining_room
            var kitchenRelated = _graph.GetRelatedNodes("kitchen", "adjacent_preferred").ToList();
            kitchenRelated.Should().Contain(n => n.Id == "dining_room");

            // Assert - bedroom should be adjacent_preferred to bathroom
            var bedroomRelated = _graph.GetRelatedNodes("bedroom", "adjacent_preferred").ToList();
            bedroomRelated.Should().Contain(n => n.Id == "bathroom");

            // Assert - kitchen should have avoid_adjacent to bedroom
            var kitchenAvoid = _graph.GetRelatedNodes("kitchen", "avoid_adjacent").ToList();
            kitchenAvoid.Should().Contain(n => n.Id == "bedroom");

            // Assert - verify adjacency relationships via QueryTriple
            var adjacencyTriples = _graph.QueryTriple(
                subjectType: "RoomType",
                relationType: "adjacent_preferred",
                objectType: "RoomType").ToList();
            adjacencyTriples.Should().NotBeEmpty();
            adjacencyTriples.Should().HaveCountGreaterOrEqualTo(3);
        }

        #endregion

        #region Persistence Tests

        [Test]
        public async Task SaveAndLoad_PreservesGraphStructure()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                _graph.AddNode(new TestKnowledgeNode { Id = "test-1", Name = "Test 1", NodeType = "Test" });
                _graph.AddNode(new TestKnowledgeNode { Id = "test-2", Name = "Test 2", NodeType = "Test" });
                _graph.AddRelationship("test-1", "test-2", "connects");

                var originalNodeCount = _graph.NodeCount;
                var originalEdgeCount = _graph.EdgeCount;

                // Act
                await _graph.SaveAsync(tempFile);

                var newGraph = new TestKnowledgeGraph();
                await newGraph.LoadAsync(tempFile);

                // Assert
                newGraph.NodeCount.Should().Be(originalNodeCount);
                newGraph.EdgeCount.Should().Be(originalEdgeCount);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Test]
        public async Task LoadAsync_NonExistentFile_DoesNotThrow()
        {
            // Act
            var act = async () => await _graph.LoadAsync("/nonexistent/path/file.json");

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void AddNode_ConcurrentAccess_MaintainsIntegrity()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var nodeId = $"concurrent-{i}";
                tasks.Add(Task.Run(() =>
                {
                    _graph.AddNode(new TestKnowledgeNode
                    {
                        Id = nodeId,
                        Name = $"Node {nodeId}",
                        NodeType = "Test"
                    });
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            _graph.NodeCount.Should().Be(100);
        }

        #endregion
    }

    #region Test Helper Classes

    public class TestKnowledgeGraph
    {
        private readonly Dictionary<string, TestKnowledgeNode> _nodes;
        private readonly List<TestKnowledgeEdge> _edges;
        private readonly Dictionary<string, List<string>> _nodesByType;
        private readonly Dictionary<string, List<TestKnowledgeEdge>> _edgesBySource;
        private readonly Dictionary<string, List<TestKnowledgeEdge>> _edgesByTarget;
        private readonly object _lock = new object();

        public int NodeCount
        {
            get { lock (_lock) { return _nodes.Count; } }
        }

        public int EdgeCount
        {
            get { lock (_lock) { return _edges.Count; } }
        }

        public TestKnowledgeGraph()
        {
            _nodes = new Dictionary<string, TestKnowledgeNode>(StringComparer.OrdinalIgnoreCase);
            _edges = new List<TestKnowledgeEdge>();
            _nodesByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _edgesBySource = new Dictionary<string, List<TestKnowledgeEdge>>(StringComparer.OrdinalIgnoreCase);
            _edgesByTarget = new Dictionary<string, List<TestKnowledgeEdge>>(StringComparer.OrdinalIgnoreCase);
        }

        public void AddNode(TestKnowledgeNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
                throw new ArgumentException("Node must have an ID");

            lock (_lock)
            {
                if (_nodes.ContainsKey(node.Id))
                {
                    _nodes[node.Id] = node;
                }
                else
                {
                    _nodes[node.Id] = node;
                    if (!_nodesByType.ContainsKey(node.NodeType))
                        _nodesByType[node.NodeType] = new List<string>();
                    _nodesByType[node.NodeType].Add(node.Id);
                }
            }
        }

        public TestKnowledgeNode GetNode(string id)
        {
            lock (_lock)
            {
                return _nodes.GetValueOrDefault(id);
            }
        }

        public IEnumerable<TestKnowledgeNode> GetNodesByType(string nodeType)
        {
            lock (_lock)
            {
                if (_nodesByType.TryGetValue(nodeType, out var nodeIds))
                    return nodeIds.Select(id => _nodes[id]).ToList();
                return Enumerable.Empty<TestKnowledgeNode>();
            }
        }

        public IEnumerable<TestKnowledgeNode> SearchNodes(string query, int maxResults = 10)
        {
            query = query.ToLowerInvariant();
            lock (_lock)
            {
                return _nodes.Values
                    .Where(n => n.Name.ToLowerInvariant().Contains(query) ||
                                n.Description?.ToLowerInvariant().Contains(query) == true)
                    .Take(maxResults)
                    .ToList();
            }
        }

        public void AddEdge(TestKnowledgeEdge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.SourceId) || string.IsNullOrEmpty(edge.TargetId))
                throw new ArgumentException("Edge must have source and target IDs");

            lock (_lock)
            {
                _edges.Add(edge);

                if (!_edgesBySource.ContainsKey(edge.SourceId))
                    _edgesBySource[edge.SourceId] = new List<TestKnowledgeEdge>();
                _edgesBySource[edge.SourceId].Add(edge);

                if (!_edgesByTarget.ContainsKey(edge.TargetId))
                    _edgesByTarget[edge.TargetId] = new List<TestKnowledgeEdge>();
                _edgesByTarget[edge.TargetId].Add(edge);
            }
        }

        public void AddRelationship(string sourceId, string targetId, string relationType, float strength = 1.0f)
        {
            AddEdge(new TestKnowledgeEdge
            {
                SourceId = sourceId,
                TargetId = targetId,
                RelationType = relationType,
                Strength = strength
            });
        }

        public IEnumerable<TestKnowledgeEdge> GetOutgoingEdges(string nodeId)
        {
            lock (_lock)
            {
                return _edgesBySource.GetValueOrDefault(nodeId) ?? Enumerable.Empty<TestKnowledgeEdge>();
            }
        }

        public IEnumerable<TestKnowledgeEdge> GetIncomingEdges(string nodeId)
        {
            lock (_lock)
            {
                return _edgesByTarget.GetValueOrDefault(nodeId) ?? Enumerable.Empty<TestKnowledgeEdge>();
            }
        }

        public IEnumerable<TestKnowledgeNode> GetRelatedNodes(string nodeId, string relationType = null)
        {
            lock (_lock)
            {
                var outgoing = _edgesBySource.GetValueOrDefault(nodeId) ?? Enumerable.Empty<TestKnowledgeEdge>();

                if (!string.IsNullOrEmpty(relationType))
                    outgoing = outgoing.Where(e => e.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase));

                return outgoing
                    .Select(e => _nodes.GetValueOrDefault(e.TargetId))
                    .Where(n => n != null)
                    .ToList();
            }
        }

        public IEnumerable<TestKnowledgeNode> FindPath(string startId, string endId, int maxDepth = 10)
        {
            lock (_lock)
            {
                if (!_nodes.ContainsKey(startId) || !_nodes.ContainsKey(endId))
                    return Enumerable.Empty<TestKnowledgeNode>();

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<(string NodeId, List<string> Path)>();
                queue.Enqueue((startId, new List<string> { startId }));

                while (queue.Count > 0)
                {
                    var (currentId, path) = queue.Dequeue();

                    if (currentId.Equals(endId, StringComparison.OrdinalIgnoreCase))
                        return path.Select(id => _nodes[id]).ToList();

                    if (path.Count >= maxDepth || visited.Contains(currentId))
                        continue;

                    visited.Add(currentId);

                    var edges = _edgesBySource.GetValueOrDefault(currentId) ?? Enumerable.Empty<TestKnowledgeEdge>();
                    foreach (var edge in edges)
                    {
                        if (!visited.Contains(edge.TargetId))
                        {
                            var newPath = new List<string>(path) { edge.TargetId };
                            queue.Enqueue((edge.TargetId, newPath));
                        }
                    }
                }

                return Enumerable.Empty<TestKnowledgeNode>();
            }
        }

        public IEnumerable<TestKnowledgeNode> GetNeighborhood(string nodeId, int depth = 2)
        {
            lock (_lock)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        var edges = _edgesBySource.GetValueOrDefault(currentId) ?? Enumerable.Empty<TestKnowledgeEdge>();
                        foreach (var edge in edges)
                            toVisit.Enqueue((edge.TargetId, currentDepth + 1));
                    }
                }

                return result
                    .Where(id => _nodes.ContainsKey(id))
                    .Select(id => _nodes[id])
                    .ToList();
            }
        }

        public IEnumerable<(TestKnowledgeNode Subject, TestKnowledgeEdge Predicate, TestKnowledgeNode Object)> QueryTriple(
            string subjectType = null,
            string relationType = null,
            string objectType = null)
        {
            lock (_lock)
            {
                var results = new List<(TestKnowledgeNode, TestKnowledgeEdge, TestKnowledgeNode)>();

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

        public void InitializeWithBuildingKnowledge()
        {
            // Add building hierarchy
            AddNode(new TestKnowledgeNode { Id = "building", Name = "Building", NodeType = "Concept" });
            AddNode(new TestKnowledgeNode { Id = "spaces", Name = "Spaces", NodeType = "Category" });

            AddRelationship("building", "spaces", "contains");

            // Room types
            var roomTypes = new[] { "bedroom", "bathroom", "kitchen", "living_room", "dining_room" };
            foreach (var room in roomTypes)
            {
                AddNode(new TestKnowledgeNode { Id = room, Name = room.Replace("_", " "), NodeType = "RoomType" });
                AddRelationship("spaces", room, "includes");
            }

            // Spatial relationships
            AddRelationship("kitchen", "dining_room", "adjacent_preferred", 0.95f);
            AddRelationship("bedroom", "bathroom", "adjacent_preferred", 0.85f);
            AddRelationship("living_room", "dining_room", "adjacent_preferred", 0.90f);
            AddRelationship("kitchen", "bedroom", "avoid_adjacent", 0.80f);

            // Element types
            AddNode(new TestKnowledgeNode { Id = "wall", Name = "Wall", NodeType = "ElementType" });
            AddNode(new TestKnowledgeNode { Id = "floor", Name = "Floor", NodeType = "ElementType" });
            AddNode(new TestKnowledgeNode { Id = "door", Name = "Door", NodeType = "ElementType" });
            AddNode(new TestKnowledgeNode { Id = "window", Name = "Window", NodeType = "ElementType" });

            // Materials
            AddNode(new TestKnowledgeNode { Id = "concrete", Name = "Concrete", NodeType = "Material" });
            AddNode(new TestKnowledgeNode { Id = "steel", Name = "Steel", NodeType = "Material" });
            AddNode(new TestKnowledgeNode { Id = "timber", Name = "Timber", NodeType = "Material" });
            AddNode(new TestKnowledgeNode { Id = "glass", Name = "Glass", NodeType = "Material" });

            AddRelationship("wall", "concrete", "made_of");
            AddRelationship("wall", "door", "can_contain");
            AddRelationship("wall", "window", "can_contain");
        }

        public async Task SaveAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            var data = new TestGraphData
            {
                Nodes = _nodes.Values.ToList(),
                Edges = _edges.ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return;

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = System.Text.Json.JsonSerializer.Deserialize<TestGraphData>(json);

            lock (_lock)
            {
                _nodes.Clear();
                _edges.Clear();
                _nodesByType.Clear();
                _edgesBySource.Clear();
                _edgesByTarget.Clear();

                foreach (var node in data.Nodes)
                    AddNode(node);

                foreach (var edge in data.Edges)
                    AddEdge(edge);
            }
        }
    }

    public class TestKnowledgeNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class TestKnowledgeEdge
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string RelationType { get; set; }
        public float Strength { get; set; } = 1.0f;
    }

    public class TestGraphData
    {
        public List<TestKnowledgeNode> Nodes { get; set; }
        public List<TestKnowledgeEdge> Edges { get; set; }
    }

    #endregion
}
