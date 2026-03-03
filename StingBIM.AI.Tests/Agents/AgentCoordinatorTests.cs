// ============================================================================
// StingBIM AI Tests - Agent Coordinator Tests
// Unit tests for the multi-agent consensus system
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using StingBIM.AI.Agents.Framework;

namespace StingBIM.AI.Tests.Agents
{
    [TestFixture]
    public class AgentCoordinatorTests
    {
        private AgentCoordinator _coordinator;
        private Mock<IDesignAgent> _mockArchAgent;
        private Mock<IDesignAgent> _mockSafetyAgent;
        private Mock<IDesignAgent> _mockStructuralAgent;

        [SetUp]
        public void SetUp()
        {
            _coordinator = new AgentCoordinator();

            // Create mock agents
            _mockArchAgent = CreateMockAgent("ArchAgent", "Architectural", 0.8f);
            _mockSafetyAgent = CreateMockAgent("SafetyAgent", "Safety", 0.9f);
            _mockStructuralAgent = CreateMockAgent("StructuralAgent", "Structural", 0.85f);
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator = null;
        }

        #region Agent Registration Tests

        [Test]
        public void RegisterAgent_ShouldAddAgentToList()
        {
            // Act
            _coordinator.RegisterAgent(_mockArchAgent.Object);

            // Assert
            _coordinator.Agents.Should().Contain(_mockArchAgent.Object);
            _coordinator.Agents.Count.Should().Be(1);
        }

        [Test]
        public void RegisterAgent_DuplicateAgent_ShouldNotAddTwice()
        {
            // Arrange
            _coordinator.RegisterAgent(_mockArchAgent.Object);

            // Act
            _coordinator.RegisterAgent(_mockArchAgent.Object);

            // Assert
            _coordinator.Agents.Count.Should().Be(1);
        }

        [Test]
        public void UnregisterAgent_ShouldRemoveAgent()
        {
            // Arrange
            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);

            // Act
            _coordinator.UnregisterAgent("ArchAgent");

            // Assert
            _coordinator.Agents.Count.Should().Be(1);
            _coordinator.Agents.Should().NotContain(a => a.AgentId == "ArchAgent");
        }

        #endregion

        #region Consensus Tests

        [Test]
        public async Task GetConsensusAsync_WithNoAgents_ShouldReturnNoAgentsStatus()
        {
            // Arrange
            var proposal = CreateTestProposal();

            // Act
            var result = await _coordinator.GetConsensusAsync(proposal);

            // Assert
            result.Status.Should().Be(ConsensusStatus.NoAgents);
            result.Message.Should().Contain("No active agents");
        }

        [Test]
        public async Task GetConsensusAsync_AllAgentsAgree_ShouldReturnConsensus()
        {
            // Arrange
            SetupAgentResponse(_mockArchAgent, score: 0.85f, hasCritical: false);
            SetupAgentResponse(_mockSafetyAgent, score: 0.85f, hasCritical: false);
            SetupAgentResponse(_mockStructuralAgent, score: 0.85f, hasCritical: false);

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);
            _coordinator.RegisterAgent(_mockStructuralAgent.Object);

            var proposal = CreateTestProposal();

            // Act
            var result = await _coordinator.GetConsensusAsync(proposal);

            // Assert
            result.Status.Should().Be(ConsensusStatus.Consensus);
            result.IsApproved.Should().BeTrue();
        }

        [Test]
        public async Task GetConsensusAsync_AgentsDisagree_ShouldReturnMajority()
        {
            // Arrange
            SetupAgentResponse(_mockArchAgent, score: 0.9f, hasCritical: false);
            SetupAgentResponse(_mockSafetyAgent, score: 0.4f, hasCritical: false);
            SetupAgentResponse(_mockStructuralAgent, score: 0.85f, hasCritical: false);

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);
            _coordinator.RegisterAgent(_mockStructuralAgent.Object);

            var proposal = CreateTestProposal();

            // Act
            var result = await _coordinator.GetConsensusAsync(proposal);

            // Assert
            result.Status.Should().BeOneOf(ConsensusStatus.Majority, ConsensusStatus.Disagreement);
        }

        [Test]
        public async Task GetConsensusAsync_CriticalIssue_ShouldNotApprove()
        {
            // Arrange
            SetupAgentResponse(_mockArchAgent, score: 0.9f, hasCritical: false);
            SetupAgentResponse(_mockSafetyAgent, score: 0.3f, hasCritical: true);
            SetupAgentResponse(_mockStructuralAgent, score: 0.85f, hasCritical: false);

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);
            _coordinator.RegisterAgent(_mockStructuralAgent.Object);

            var proposal = CreateTestProposal();

            // Act
            var result = await _coordinator.GetConsensusAsync(proposal);

            // Assert
            result.IsApproved.Should().BeFalse();
            result.Issues.Should().NotBeEmpty();
        }

        [Test]
        public async Task GetConsensusAsync_WithCancellation_ShouldRespectToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            var proposal = CreateTestProposal();

            // Act & Assert
            await FluentActions
                .Invoking(() => _coordinator.GetConsensusAsync(proposal, null, cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region Validation Tests

        [Test]
        public void ValidateAction_AllAgentsApprove_ShouldReturnValid()
        {
            // Arrange
            var action = new DesignAction { ActionType = "CreateWall" };

            _mockArchAgent.Setup(a => a.ValidateAction(action))
                .Returns(new ValidationResult { IsValid = true });
            _mockSafetyAgent.Setup(a => a.ValidateAction(action))
                .Returns(new ValidationResult { IsValid = true });

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);

            // Act
            var result = _coordinator.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Issues.Should().BeEmpty();
        }

        [Test]
        public void ValidateAction_OneAgentRejects_ShouldReturnInvalid()
        {
            // Arrange
            var action = new DesignAction { ActionType = "CreateWall" };

            _mockArchAgent.Setup(a => a.ValidateAction(action))
                .Returns(new ValidationResult { IsValid = true });
            _mockSafetyAgent.Setup(a => a.ValidateAction(action))
                .Returns(new ValidationResult
                {
                    IsValid = false,
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Description = "Fire rating insufficient" }
                    }
                });

            _coordinator.RegisterAgent(_mockArchAgent.Object);
            _coordinator.RegisterAgent(_mockSafetyAgent.Object);

            // Act
            var result = _coordinator.ValidateAction(action);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Issues.Should().NotBeEmpty();
        }

        #endregion

        #region Helper Methods

        private Mock<IDesignAgent> CreateMockAgent(string agentId, string specialty, float expertiseLevel)
        {
            var mock = new Mock<IDesignAgent>();
            mock.Setup(a => a.AgentId).Returns(agentId);
            mock.Setup(a => a.Specialty).Returns(specialty);
            mock.Setup(a => a.ExpertiseLevel).Returns(expertiseLevel);
            mock.Setup(a => a.IsActive).Returns(true);
            return mock;
        }

        private void SetupAgentResponse(Mock<IDesignAgent> mockAgent, float score, bool hasCritical)
        {
            mockAgent.Setup(a => a.EvaluateAsync(
                    It.IsAny<DesignProposal>(),
                    It.IsAny<EvaluationContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentOpinion
                {
                    AgentId = mockAgent.Object.AgentId,
                    AgentSpecialty = mockAgent.Object.Specialty,
                    Score = score,
                    Confidence = 0.9f,
                    HasCriticalIssues = hasCritical,
                    Issues = hasCritical
                        ? new List<DesignIssue> { new DesignIssue { Description = "Critical issue" } }
                        : new List<DesignIssue>()
                });
        }

        private DesignProposal CreateTestProposal()
        {
            return new DesignProposal
            {
                ProposalId = Guid.NewGuid().ToString(),
                Description = "Test wall creation"
            };
        }

        #endregion
    }
}
