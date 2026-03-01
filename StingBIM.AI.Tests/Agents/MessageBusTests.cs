// StingBIM.AI.Tests.Agents.MessageBusTests
// Tests for the publish-subscribe message bus system
// Covers: Subscribe, Publish, Broadcast, Direct messaging, History

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.Agents.Framework;

namespace StingBIM.AI.Tests.Agents
{
    [TestFixture]
    public class MessageBusTests
    {
        private MessageBus _messageBus;

        [SetUp]
        public void SetUp()
        {
            _messageBus = new MessageBus(maxHistorySize: 100);
        }

        #region Subscribe Tests

        [Test]
        public void Subscribe_ShouldAcceptNewSubscription()
        {
            // Arrange
            var receivedMessages = new List<AgentMessage>();

            // Act
            _messageBus.Subscribe("agent1", "test.topic", async msg =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            });

            // Assert - no exception thrown
            receivedMessages.Should().BeEmpty();
        }

        [Test]
        public void Subscribe_MultipleAgentsSameTopic_ShouldAllowMultipleSubscribers()
        {
            // Arrange
            var agent1Received = new List<AgentMessage>();
            var agent2Received = new List<AgentMessage>();

            // Act
            _messageBus.Subscribe("agent1", "shared.topic", async msg =>
            {
                agent1Received.Add(msg);
                await Task.CompletedTask;
            });
            _messageBus.Subscribe("agent2", "shared.topic", async msg =>
            {
                agent2Received.Add(msg);
                await Task.CompletedTask;
            });

            // Assert - no exception, both subscribed
            agent1Received.Should().BeEmpty();
            agent2Received.Should().BeEmpty();
        }

        [Test]
        public void Subscribe_WildcardTopic_ShouldAcceptWildcard()
        {
            // Arrange
            var receivedMessages = new List<AgentMessage>();

            // Act
            _messageBus.Subscribe("monitor", "*", async msg =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            });

            // Assert - no exception
            receivedMessages.Should().BeEmpty();
        }

        #endregion

        #region Unsubscribe Tests

        [Test]
        public void Unsubscribe_ExistingSubscription_ShouldRemoveSubscriber()
        {
            // Arrange
            var receivedMessages = new List<AgentMessage>();
            _messageBus.Subscribe("agent1", "test.topic", async msg =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            });

            // Act
            _messageBus.Unsubscribe("agent1", "test.topic");

            // Assert - no exception, subscription removed
            receivedMessages.Should().BeEmpty();
        }

        [Test]
        public void Unsubscribe_NonExistentSubscription_ShouldNotThrow()
        {
            // Act & Assert
            var act = () => _messageBus.Unsubscribe("nonexistent", "test.topic");
            act.Should().NotThrow();
        }

        #endregion

        #region PublishAsync Tests

        [Test]
        public async Task PublishAsync_WithSubscriber_ShouldDeliverMessage()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<AgentMessage>();
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("receiver", "test.topic", async msg =>
            {
                receivedMessages.Add(msg);
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "test.topic",
                Payload = "Hello"
            };

            // Act
            await _messageBus.PublishAsync(message);
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            receivedMessages.Should().HaveCount(1);
            receivedMessages.First().Payload.Should().Be("Hello");
        }

        [Test]
        public async Task PublishAsync_ShouldAssignMessageId()
        {
            // Arrange
            var receivedMessage = (AgentMessage)null;
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("receiver", "test.topic", async msg =>
            {
                receivedMessage = msg;
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "test.topic"
            };

            // Act
            await _messageBus.PublishAsync(message);
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            message.MessageId.Should().BeGreaterThan(0);
            message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task PublishAsync_ShouldNotDeliverToSender()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<AgentMessage>();

            _messageBus.Subscribe("self-sender", "test.topic", async msg =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            });

            var message = new AgentMessage
            {
                SenderId = "self-sender",
                Topic = "test.topic",
                Payload = "Self message"
            };

            // Act
            await _messageBus.PublishAsync(message);
            await Task.Delay(100); // Give time for any delivery

            // Assert - sender should not receive its own message
            receivedMessages.Should().BeEmpty();
        }

        [Test]
        public async Task PublishAsync_WithWildcardSubscriber_ShouldDeliverToWildcard()
        {
            // Arrange
            var wildcardReceived = new ConcurrentBag<AgentMessage>();
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("monitor", "*", async msg =>
            {
                wildcardReceived.Add(msg);
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "any.topic",
                Payload = "Broadcast"
            };

            // Act
            await _messageBus.PublishAsync(message);
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            wildcardReceived.Should().HaveCount(1);
        }

        [Test]
        public async Task PublishAsync_NoSubscribers_ShouldNotThrow()
        {
            // Arrange
            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "orphan.topic",
                Payload = "Nobody listening"
            };

            // Act & Assert
            var act = async () => await _messageBus.PublishAsync(message);
            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task PublishAsync_MultipleSequentialMessages_ShouldIncrementIds()
        {
            // Arrange
            var message1 = new AgentMessage { SenderId = "sender", Topic = "test" };
            var message2 = new AgentMessage { SenderId = "sender", Topic = "test" };
            var message3 = new AgentMessage { SenderId = "sender", Topic = "test" };

            // Act
            await _messageBus.PublishAsync(message1);
            await _messageBus.PublishAsync(message2);
            await _messageBus.PublishAsync(message3);

            // Assert
            message1.MessageId.Should().Be(1);
            message2.MessageId.Should().Be(2);
            message3.MessageId.Should().Be(3);
        }

        #endregion

        #region BroadcastAsync Tests

        [Test]
        public async Task BroadcastAsync_ShouldCreateAndPublishMessage()
        {
            // Arrange
            var receivedMessages = new ConcurrentBag<AgentMessage>();
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("receiver", "broadcast.topic", async msg =>
            {
                receivedMessages.Add(msg);
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.BroadcastAsync("broadcaster", "broadcast.topic", new { Value = 42 });
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            receivedMessages.Should().HaveCount(1);
            var received = receivedMessages.First();
            received.SenderId.Should().Be("broadcaster");
            received.Topic.Should().Be("broadcast.topic");
            received.MessageType.Should().Be(MessageType.Broadcast);
        }

        [Test]
        public async Task BroadcastAsync_WithPriority_ShouldSetPriority()
        {
            // Arrange
            var receivedMessage = (AgentMessage)null;
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("receiver", "critical.topic", async msg =>
            {
                receivedMessage = msg;
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.BroadcastAsync("sender", "critical.topic", "urgent",
                MessagePriority.Critical);
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            receivedMessage.Should().NotBeNull();
            receivedMessage.Priority.Should().Be(MessagePriority.Critical);
        }

        [Test]
        public async Task BroadcastAsync_ToMultipleSubscribers_ShouldDeliverToAll()
        {
            // Arrange
            var agent1Messages = new ConcurrentBag<AgentMessage>();
            var agent2Messages = new ConcurrentBag<AgentMessage>();
            var agent3Messages = new ConcurrentBag<AgentMessage>();
            var countdown = new CountdownEvent(3);

            _messageBus.Subscribe("agent1", "multi.topic", async msg =>
            {
                agent1Messages.Add(msg);
                countdown.Signal();
                await Task.CompletedTask;
            });
            _messageBus.Subscribe("agent2", "multi.topic", async msg =>
            {
                agent2Messages.Add(msg);
                countdown.Signal();
                await Task.CompletedTask;
            });
            _messageBus.Subscribe("agent3", "multi.topic", async msg =>
            {
                agent3Messages.Add(msg);
                countdown.Signal();
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.BroadcastAsync("broadcaster", "multi.topic", "Hello all");
            countdown.Wait(TimeSpan.FromSeconds(2));

            // Assert
            agent1Messages.Should().HaveCount(1);
            agent2Messages.Should().HaveCount(1);
            agent3Messages.Should().HaveCount(1);
        }

        #endregion

        #region SendDirectAsync Tests

        [Test]
        public async Task SendDirectAsync_ToSpecificAgent_ShouldDeliverOnlyToTarget()
        {
            // Arrange
            var agent1Messages = new ConcurrentBag<AgentMessage>();
            var agent2Messages = new ConcurrentBag<AgentMessage>();
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("agent1", "direct.topic", async msg =>
            {
                agent1Messages.Add(msg);
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });
            _messageBus.Subscribe("agent2", "direct.topic", async msg =>
            {
                agent2Messages.Add(msg);
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.SendDirectAsync("sender", "agent1", "direct.topic", "Private message");
            await Task.WhenAny(tcs.Task, Task.Delay(1000));
            await Task.Delay(100); // Extra time to ensure agent2 doesn't receive

            // Assert
            agent1Messages.Should().HaveCount(1);
            agent2Messages.Should().BeEmpty();
        }

        [Test]
        public async Task SendDirectAsync_ShouldSetMessageTypeAsDirect()
        {
            // Arrange
            var receivedMessage = (AgentMessage)null;
            var tcs = new TaskCompletionSource<bool>();

            _messageBus.Subscribe("target", "direct.topic", async msg =>
            {
                receivedMessage = msg;
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.SendDirectAsync("sender", "target", "direct.topic", "Direct");
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            receivedMessage.Should().NotBeNull();
            receivedMessage.MessageType.Should().Be(MessageType.Direct);
            receivedMessage.ReceiverId.Should().Be("target");
        }

        [Test]
        public async Task SendDirectAsync_ToNonExistentAgent_ShouldNotThrow()
        {
            // Act & Assert
            var act = async () => await _messageBus.SendDirectAsync(
                "sender", "nonexistent", "direct.topic", "Lost message");
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region GetHistory Tests

        [Test]
        public async Task GetHistory_AfterPublish_ShouldContainMessage()
        {
            // Arrange
            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "history.topic",
                Payload = "Historical"
            };

            // Act
            await _messageBus.PublishAsync(message);
            var history = _messageBus.GetHistory().ToList();

            // Assert
            history.Should().HaveCount(1);
            history[0].Payload.Should().Be("Historical");
        }

        [Test]
        public async Task GetHistory_WithFilter_ShouldFilterMessages()
        {
            // Arrange
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "agent1",
                Topic = "topic.a"
            });
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "agent2",
                Topic = "topic.b"
            });
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "agent1",
                Topic = "topic.c"
            });

            // Act
            var agent1History = _messageBus.GetHistory(
                filter: msg => msg.SenderId == "agent1").ToList();

            // Assert
            agent1History.Should().HaveCount(2);
            agent1History.Should().OnlyContain(m => m.SenderId == "agent1");
        }

        [Test]
        public async Task GetHistory_WithLimit_ShouldRespectLimit()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                await _messageBus.PublishAsync(new AgentMessage
                {
                    SenderId = "sender",
                    Topic = "test",
                    Payload = $"Message {i}"
                });
            }

            // Act
            var history = _messageBus.GetHistory(limit: 5).ToList();

            // Assert
            history.Should().HaveCount(5);
        }

        [Test]
        public async Task GetHistory_ShouldReturnMostRecentFirst()
        {
            // Arrange
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "sender",
                Topic = "test",
                Payload = "First"
            });
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "sender",
                Topic = "test",
                Payload = "Second"
            });
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "sender",
                Topic = "test",
                Payload = "Third"
            });

            // Act
            var history = _messageBus.GetHistory().ToList();

            // Assert
            history[0].Payload.Should().Be("Third");
            history[1].Payload.Should().Be("Second");
            history[2].Payload.Should().Be("First");
        }

        [Test]
        public async Task GetHistory_ExceedsMaxSize_ShouldDiscardOldest()
        {
            // Arrange - bus with max size of 100
            for (int i = 0; i < 150; i++)
            {
                await _messageBus.PublishAsync(new AgentMessage
                {
                    SenderId = "sender",
                    Topic = "test",
                    Payload = $"Message {i}"
                });
            }

            // Act
            var history = _messageBus.GetHistory(limit: 200).ToList();

            // Assert
            history.Should().HaveCount(100); // Max size
            history[0].Payload.Should().Be("Message 149"); // Most recent
            history[99].Payload.Should().Be("Message 50"); // Oldest remaining
        }

        #endregion

        #region AgentMessage Tests

        [Test]
        public void AgentMessage_GetPayload_WithCorrectType_ShouldReturnValue()
        {
            // Arrange
            var message = new AgentMessage
            {
                Payload = new TestPayload { Value = 42, Name = "Test" }
            };

            // Act
            var payload = message.GetPayload<TestPayload>();

            // Assert
            payload.Should().NotBeNull();
            payload.Value.Should().Be(42);
            payload.Name.Should().Be("Test");
        }

        [Test]
        public void AgentMessage_GetPayload_WithWrongType_ShouldReturnDefault()
        {
            // Arrange
            var message = new AgentMessage
            {
                Payload = "string payload"
            };

            // Act
            var payload = message.GetPayload<TestPayload>();

            // Assert
            payload.Should().BeNull();
        }

        [Test]
        public void AgentMessage_GetPayload_WithNullPayload_ShouldReturnDefault()
        {
            // Arrange
            var message = new AgentMessage { Payload = null };

            // Act
            var payload = message.GetPayload<TestPayload>();

            // Assert
            payload.Should().BeNull();
        }

        #endregion

        #region Concurrent Access Tests

        [Test]
        public async Task PublishAsync_ConcurrentPublishes_ShouldBeThreadSafe()
        {
            // Arrange
            var receivedCount = 0;
            var lockObj = new object();

            _messageBus.Subscribe("receiver", "concurrent.topic", async msg =>
            {
                lock (lockObj) { receivedCount++; }
                await Task.CompletedTask;
            });

            // Act - Publish 100 messages concurrently
            var tasks = Enumerable.Range(0, 100).Select(i =>
                _messageBus.PublishAsync(new AgentMessage
                {
                    SenderId = $"sender{i % 10}",
                    Topic = "concurrent.topic",
                    Payload = i
                }));

            await Task.WhenAll(tasks);
            await Task.Delay(500); // Allow delivery

            // Assert
            receivedCount.Should().Be(100);
        }

        [Test]
        public async Task Subscribe_ConcurrentSubscribes_ShouldBeThreadSafe()
        {
            // Arrange
            var receivedCounts = new ConcurrentDictionary<string, int>();
            var countdown = new CountdownEvent(50);

            // Act - Subscribe 50 agents concurrently
            var subscribeTasks = Enumerable.Range(0, 50).Select(i =>
            {
                var agentId = $"agent{i}";
                receivedCounts[agentId] = 0;
                return Task.Run(() =>
                {
                    _messageBus.Subscribe(agentId, "concurrent.topic", async msg =>
                    {
                        receivedCounts.AddOrUpdate(agentId, 1, (_, v) => v + 1);
                        countdown.Signal();
                        await Task.CompletedTask;
                    });
                });
            });

            await Task.WhenAll(subscribeTasks);

            // Publish one message
            await _messageBus.BroadcastAsync("broadcaster", "concurrent.topic", "Test");
            countdown.Wait(TimeSpan.FromSeconds(5));

            // Assert
            receivedCounts.Values.Sum().Should().Be(50);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public async Task PublishAsync_HandlerThrowsException_ShouldContinueDelivery()
        {
            // Arrange
            var successfulDeliveries = new ConcurrentBag<string>();
            var countdown = new CountdownEvent(2);

            _messageBus.Subscribe("failing-agent", "test.topic", async msg =>
            {
                throw new InvalidOperationException("Handler failed");
            });
            _messageBus.Subscribe("good-agent1", "test.topic", async msg =>
            {
                successfulDeliveries.Add("agent1");
                countdown.Signal();
                await Task.CompletedTask;
            });
            _messageBus.Subscribe("good-agent2", "test.topic", async msg =>
            {
                successfulDeliveries.Add("agent2");
                countdown.Signal();
                await Task.CompletedTask;
            });

            // Act
            await _messageBus.PublishAsync(new AgentMessage
            {
                SenderId = "sender",
                Topic = "test.topic"
            });
            countdown.Wait(TimeSpan.FromSeconds(2));

            // Assert - other agents should still receive the message
            successfulDeliveries.Should().HaveCount(2);
        }

        [Test]
        public async Task PublishAsync_WithCancellation_ShouldStopGracefully()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var message = new AgentMessage
            {
                SenderId = "sender",
                Topic = "test.topic"
            };

            // Act & Assert - should handle cancellation gracefully
            // Note: Current implementation doesn't throw on cancellation during delivery
            var act = async () => await _messageBus.PublishAsync(message, cts.Token);
            await act.Should().NotThrowAsync();
        }

        #endregion

        private class TestPayload
        {
            public int Value { get; set; }
            public string Name { get; set; }
        }
    }
}
