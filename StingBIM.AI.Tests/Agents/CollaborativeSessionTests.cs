// StingBIM.AI.Tests.Agents.CollaborativeSessionTests
// Tests for the collaborative design session with iterative refinement
// Covers: Iteration, convergence, shared state, session management

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
    public class CollaborativeSessionTests
    {
        private AgentCoordinator _coordinator;
        private MessageBus _messageBus;
        private DesignProposal _initialProposal;

        [SetUp]
        public void SetUp()
        {
            _coordinator = new AgentCoordinator();
            _messageBus = _coordinator.MessageBus;
            _initialProposal = CreateTestProposal();
        }

        #region Session Creation Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateSession()
        {
            // Act
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Assert
            session.SessionId.Should().NotBeNullOrEmpty();
            session.SessionId.Should().HaveLength(8);
            session.IterationCount.Should().Be(0);
            session.CurrentProposal.Should().BeSameAs(_initialProposal);
            session.Status.Should().Be(SessionStatus.Active);
        }

        [Test]
        public void Constructor_ShouldGenerateUniqueSessionIds()
        {
            // Act
            var session1 = new CollaborativeSession(_coordinator, _messageBus, CreateTestProposal());
            var session2 = new CollaborativeSession(_coordinator, _messageBus, CreateTestProposal());
            var session3 = new CollaborativeSession(_coordinator, _messageBus, CreateTestProposal());

            // Assert
            var ids = new[] { session1.SessionId, session2.SessionId, session3.SessionId };
            ids.Should().OnlyHaveUniqueItems();
        }

        #endregion

        #region Shared State Tests

        [Test]
        public void SetSharedState_WithValue_ShouldStoreValue()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            session.SetSharedState("key1", "value1");
            session.SetSharedState("key2", 42);

            // Assert
            session.GetSharedState<string>("key1").Should().Be("value1");
            session.GetSharedState<int>("key2").Should().Be(42);
        }

        [Test]
        public void GetSharedState_NonExistentKey_ShouldReturnDefault()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var stringResult = session.GetSharedState<string>("nonexistent");
            var intResult = session.GetSharedState<int>("nonexistent");
            var objectResult = session.GetSharedState<object>("nonexistent");

            // Assert
            stringResult.Should().BeNull();
            intResult.Should().Be(0);
            objectResult.Should().BeNull();
        }

        [Test]
        public void SetSharedState_OverwriteExisting_ShouldUpdateValue()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            session.SetSharedState("key", "original");

            // Act
            session.SetSharedState("key", "updated");

            // Assert
            session.GetSharedState<string>("key").Should().Be("updated");
        }

        [Test]
        public void GetSharedState_WrongType_ShouldReturnDefault()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            session.SetSharedState("key", "string value");

            // Act
            var result = session.GetSharedState<int>("key");

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void SharedState_ComplexObjects_ShouldWorkCorrectly()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            var complexObject = new Dictionary<string, List<int>>
            {
                ["list1"] = new List<int> { 1, 2, 3 },
                ["list2"] = new List<int> { 4, 5, 6 }
            };

            // Act
            session.SetSharedState("complex", complexObject);
            var retrieved = session.GetSharedState<Dictionary<string, List<int>>>("complex");

            // Assert
            retrieved.Should().BeEquivalentTo(complexObject);
        }

        #endregion

        #region IterateAsync Tests

        [Test]
        public async Task IterateAsync_WithApprovedConsensus_ShouldConverge()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "approver", 0.9f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var iteration = await session.IterateAsync();

            // Assert
            iteration.IterationNumber.Should().Be(1);
            iteration.ConsensusResult.Should().NotBeNull();
            session.Status.Should().Be(SessionStatus.Converged);
        }

        [Test]
        public async Task IterateAsync_WithRejectedConsensus_ShouldRemainActive()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "rejector", 0.3f); // Low score = not approved
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var iteration = await session.IterateAsync();

            // Assert
            session.Status.Should().Be(SessionStatus.Active);
        }

        [Test]
        public async Task IterateAsync_ShouldIncrementIterationNumber()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.5f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var iter1 = await session.IterateAsync();
            var iter2 = await session.IterateAsync();
            var iter3 = await session.IterateAsync();

            // Assert
            iter1.IterationNumber.Should().Be(1);
            iter2.IterationNumber.Should().Be(2);
            iter3.IterationNumber.Should().Be(3);
            session.IterationCount.Should().Be(3);
        }

        [Test]
        public async Task IterateAsync_ShouldRecordTiming()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.8f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var startTime = DateTime.UtcNow;
            var iteration = await session.IterateAsync();
            var endTime = DateTime.UtcNow;

            // Assert
            iteration.StartTime.Should().BeOnOrAfter(startTime);
            iteration.EndTime.Should().BeOnOrBefore(endTime);
            iteration.Duration.Should().BePositive();
        }

        [Test]
        public async Task IterateAsync_MaxIterations_ShouldSetMaxIterationsStatus()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.5f); // Never approves
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act - Run 10 iterations (max default)
            for (int i = 0; i < 10; i++)
            {
                await session.IterateAsync();
            }

            // Assert
            session.Status.Should().Be(SessionStatus.MaxIterations);
        }

        [Test]
        public async Task IterateAsync_ShouldBroadcastIterationComplete()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.8f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            DesignIteration receivedIteration = null;
            _messageBus.Subscribe("test-receiver", "session.iteration.complete", async msg =>
            {
                receivedIteration = msg.GetPayload<DesignIteration>();
                await Task.CompletedTask;
            });

            // Act
            await session.IterateAsync();
            await Task.Delay(100); // Allow message delivery

            // Assert
            receivedIteration.Should().NotBeNull();
            receivedIteration.IterationNumber.Should().Be(1);
        }

        [Test]
        public async Task IterateAsync_WithCancellation_ShouldThrow()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.8f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                session.IterateAsync(cancellationToken: cts.Token));
        }

        #endregion

        #region RunToCompletionAsync Tests

        [Test]
        public async Task RunToCompletionAsync_QuickConvergence_ShouldReturnConvergedResult()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "approver", 0.9f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var result = await session.RunToCompletionAsync();

            // Assert
            result.Status.Should().Be(SessionStatus.Converged);
            result.IterationCount.Should().Be(1);
            result.FinalProposal.Should().NotBeNull();
            result.SessionId.Should().Be(session.SessionId);
        }

        [Test]
        public async Task RunToCompletionAsync_NoConvergence_ShouldReachMaxIterations()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "rejector", 0.3f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var result = await session.RunToCompletionAsync(maxIterations: 5);

            // Assert
            result.Status.Should().Be(SessionStatus.MaxIterations);
            result.IterationCount.Should().Be(5);
        }

        [Test]
        public async Task RunToCompletionAsync_ShouldContainAllIterations()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.5f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var result = await session.RunToCompletionAsync(maxIterations: 3);

            // Assert
            result.Iterations.Should().HaveCount(3);
            result.Iterations.Select(i => i.IterationNumber).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        [Test]
        public async Task RunToCompletionAsync_ShouldReturnFinalConsensus()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.85f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var result = await session.RunToCompletionAsync();

            // Assert
            result.FinalConsensus.Should().NotBeNull();
        }

        [Test]
        public async Task RunToCompletionAsync_WithCancellation_ShouldStopEarly()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.5f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            var cts = new CancellationTokenSource();

            // Cancel after short delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                cts.Cancel();
            });

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                session.RunToCompletionAsync(maxIterations: 100, cancellationToken: cts.Token));
        }

        #endregion

        #region Session Properties Tests

        [Test]
        public void SessionId_ShouldBeImmutable()
        {
            // Arrange
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);
            var originalId = session.SessionId;

            // Assert
            session.SessionId.Should().Be(originalId);
        }

        [Test]
        public async Task CurrentProposal_AfterIteration_ShouldBeAvailable()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.8f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            await session.IterateAsync();

            // Assert
            session.CurrentProposal.Should().NotBeNull();
            session.CurrentProposal.ProposalId.Should().Be(_initialProposal.ProposalId);
        }

        #endregion

        #region DesignIteration Tests

        [Test]
        public async Task DesignIteration_ShouldContainInputAndOutputProposals()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.8f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var iteration = await session.IterateAsync();

            // Assert
            iteration.InputProposal.Should().NotBeNull();
            iteration.OutputProposal.Should().NotBeNull();
        }

        [Test]
        public async Task DesignIteration_ShouldCollectSuggestions()
        {
            // Arrange
            var mockAgent = new Mock<IDesignAgent>();
            mockAgent.Setup(a => a.AgentId).Returns("suggester");
            mockAgent.Setup(a => a.Specialty).Returns("Structural");
            mockAgent.Setup(a => a.IsActive).Returns(true);
            mockAgent.Setup(a => a.ExpertiseLevel).Returns(0.8f);
            mockAgent.Setup(a => a.EvaluateAsync(
                    It.IsAny<DesignProposal>(),
                    It.IsAny<EvaluationContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentOpinion
                {
                    AgentId = "suggester",
                    Score = 0.6f // Not approved
                });
            mockAgent.Setup(a => a.SuggestAsync(
                    It.IsAny<DesignContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AgentSuggestion>
                {
                    new AgentSuggestion
                    {
                        AgentId = "suggester",
                        Title = "Test suggestion",
                        Confidence = 0.9f,
                        Impact = 0.8f
                    }
                });

            _coordinator.RegisterAgent(mockAgent.Object);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var iteration = await session.IterateAsync();

            // Assert
            iteration.Suggestions.Should().NotBeNull();
        }

        #endregion

        #region SessionResult Tests

        [Test]
        public async Task SessionResult_ShouldContainCorrectMetadata()
        {
            // Arrange
            RegisterAgentWithApproval(_coordinator, "agent", 0.9f);
            var session = new CollaborativeSession(_coordinator, _messageBus, _initialProposal);

            // Act
            var result = await session.RunToCompletionAsync();

            // Assert
            result.SessionId.Should().Be(session.SessionId);
            result.FinalProposal.Should().NotBeNull();
            result.IterationCount.Should().BeGreaterThan(0);
            result.Iterations.Should().NotBeEmpty();
        }

        #endregion

        #region Helper Methods

        private void RegisterAgentWithApproval(AgentCoordinator coordinator, string agentId, float score)
        {
            var mockAgent = new Mock<IDesignAgent>();
            mockAgent.Setup(a => a.AgentId).Returns(agentId);
            mockAgent.Setup(a => a.Specialty).Returns("General");
            mockAgent.Setup(a => a.IsActive).Returns(true);
            mockAgent.Setup(a => a.ExpertiseLevel).Returns(0.8f);
            mockAgent.Setup(a => a.EvaluateAsync(
                    It.IsAny<DesignProposal>(),
                    It.IsAny<EvaluationContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentOpinion
                {
                    AgentId = agentId,
                    Score = score
                });
            mockAgent.Setup(a => a.SuggestAsync(
                    It.IsAny<DesignContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AgentSuggestion>());

            coordinator.RegisterAgent(mockAgent.Object);
        }

        private DesignProposal CreateTestProposal()
        {
            return new DesignProposal
            {
                ProposalId = Guid.NewGuid().ToString("N").Substring(0, 8),
                Description = "Test proposal for collaborative session",
                Elements = new List<ProposedElement>
                {
                    new ProposedElement
                    {
                        ElementType = "Wall",
                        FamilyName = "Basic Wall",
                        Geometry = new GeometryInfo
                        {
                            X = 0, Y = 0, Z = 0,
                            Width = 0.2, Height = 3.0, Length = 5.0
                        }
                    }
                }
            };
        }

        #endregion
    }
}
