// ============================================================================
// StingBIM AI Tests - Working Memory Tests
// Unit tests for thread-safe working memory implementation
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace StingBIM.AI.Tests.Core
{
    [TestFixture]
    public class WorkingMemoryTests
    {
        private TestableWorkingMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _memory = new TestableWorkingMemory(maxCapacity: 7);
        }

        [TearDown]
        public void TearDown()
        {
            _memory.Clear();
        }

        #region Basic Operations Tests

        [Test]
        public void AddItem_SingleItem_ShouldBeRetrievable()
        {
            // Arrange
            var item = CreateMemoryItem("test-1", "Test content");

            // Act
            _memory.AddItem(item);

            // Assert
            _memory.Count.Should().Be(1);
            _memory.GetItem("test-1").Should().NotBeNull();
            _memory.GetItem("test-1").Content.Should().Be("Test content");
        }

        [Test]
        public void AddItem_ExceedsCapacity_ShouldRemoveOldest()
        {
            // Arrange - Add 8 items (capacity is 7)
            for (int i = 0; i < 8; i++)
            {
                _memory.AddItem(CreateMemoryItem($"item-{i}", $"Content {i}"));
            }

            // Assert
            _memory.Count.Should().Be(7);
            _memory.GetItem("item-0").Should().BeNull(); // First item removed
            _memory.GetItem("item-7").Should().NotBeNull(); // Last item exists
        }

        [Test]
        public void GetItem_NonExistent_ShouldReturnNull()
        {
            // Act
            var item = _memory.GetItem("non-existent");

            // Assert
            item.Should().BeNull();
        }

        [Test]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                _memory.AddItem(CreateMemoryItem($"item-{i}", $"Content {i}"));
            }

            // Act
            _memory.Clear();

            // Assert
            _memory.Count.Should().Be(0);
        }

        #endregion

        #region Relevance and Decay Tests

        [Test]
        public void GetRelevantItems_ShouldReturnMatchingItems()
        {
            // Arrange
            _memory.AddItem(CreateMemoryItem("wall-1", "Create wall", "Wall"));
            _memory.AddItem(CreateMemoryItem("door-1", "Create door", "Door"));
            _memory.AddItem(CreateMemoryItem("wall-2", "Modify wall", "Wall"));

            // Act
            var wallItems = _memory.GetItemsByCategory("Wall");

            // Assert
            wallItems.Should().HaveCount(2);
            wallItems.All(i => i.Category == "Wall").Should().BeTrue();
        }

        [Test]
        public void Item_RelevanceDecays_OverTime()
        {
            // Arrange
            var item = CreateMemoryItem("test-1", "Content", initialRelevance: 1.0f);
            _memory.AddItem(item);

            // Act - Simulate time passing
            _memory.ApplyDecay(decayFactor: 0.9f);

            // Assert
            var retrievedItem = _memory.GetItem("test-1");
            retrievedItem.Relevance.Should().BeLessThan(1.0f);
        }

        [Test]
        public void GetMostRelevant_ShouldReturnHighestRelevance()
        {
            // Arrange
            _memory.AddItem(CreateMemoryItem("low", "Low", initialRelevance: 0.3f));
            _memory.AddItem(CreateMemoryItem("high", "High", initialRelevance: 0.9f));
            _memory.AddItem(CreateMemoryItem("medium", "Medium", initialRelevance: 0.6f));

            // Act
            var mostRelevant = _memory.GetMostRelevant(2);

            // Assert
            mostRelevant.Should().HaveCount(2);
            mostRelevant.First().Id.Should().Be("high");
            mostRelevant.Last().Id.Should().Be("medium");
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public async Task AddItem_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var addedCount = 0;

            // Act - Add items from multiple threads
            for (int i = 0; i < 100; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    _memory.AddItem(CreateMemoryItem($"item-{index}", $"Content {index}"));
                    Interlocked.Increment(ref addedCount);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should not throw and count should be <= capacity
            addedCount.Should().Be(100);
            _memory.Count.Should().BeLessOrEqualTo(7); // Max capacity
        }

        [Test]
        public async Task GetItem_ConcurrentReadWrite_ShouldBeThreadSafe()
        {
            // Arrange
            var readTasks = new List<Task<MemoryItem>>();
            var writeTasks = new List<Task>();

            // Pre-populate
            for (int i = 0; i < 5; i++)
            {
                _memory.AddItem(CreateMemoryItem($"item-{i}", $"Content {i}"));
            }

            // Act - Concurrent reads and writes
            for (int i = 0; i < 50; i++)
            {
                var index = i;
                writeTasks.Add(Task.Run(() =>
                {
                    _memory.AddItem(CreateMemoryItem($"new-{index}", $"New {index}"));
                }));

                readTasks.Add(Task.Run(() =>
                {
                    return _memory.GetItem($"item-{index % 5}");
                }));
            }

            await Task.WhenAll(writeTasks);
            var results = await Task.WhenAll(readTasks);

            // Assert - Should not throw
            results.Should().NotBeNull();
        }

        [Test]
        public async Task Clear_DuringConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        if (index % 10 == 0)
                        {
                            _memory.Clear();
                        }
                        else
                        {
                            _memory.AddItem(CreateMemoryItem($"item-{index}", $"Content {index}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            exceptions.Should().BeEmpty();
        }

        #endregion

        #region Helper Methods and Classes

        private MemoryItem CreateMemoryItem(
            string id,
            string content,
            string category = null,
            float initialRelevance = 1.0f)
        {
            return new MemoryItem
            {
                Id = id,
                Content = content,
                Category = category,
                Relevance = initialRelevance,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Testable working memory implementation for unit tests.
        /// </summary>
        private class TestableWorkingMemory
        {
            private readonly LinkedList<MemoryItem> _items;
            private readonly Dictionary<string, LinkedListNode<MemoryItem>> _index;
            private readonly object _lock = new object();
            private readonly int _maxCapacity;

            public int Count
            {
                get { lock (_lock) { return _items.Count; } }
            }

            public TestableWorkingMemory(int maxCapacity = 7)
            {
                _maxCapacity = maxCapacity;
                _items = new LinkedList<MemoryItem>();
                _index = new Dictionary<string, LinkedListNode<MemoryItem>>();
            }

            public void AddItem(MemoryItem item)
            {
                lock (_lock)
                {
                    // Remove existing if present
                    if (_index.TryGetValue(item.Id, out var existing))
                    {
                        _items.Remove(existing);
                        _index.Remove(item.Id);
                    }

                    // Add to front
                    var node = _items.AddFirst(item);
                    _index[item.Id] = node;

                    // Trim if over capacity
                    while (_items.Count > _maxCapacity)
                    {
                        var last = _items.Last;
                        _index.Remove(last.Value.Id);
                        _items.RemoveLast();
                    }
                }
            }

            public MemoryItem GetItem(string id)
            {
                lock (_lock)
                {
                    return _index.TryGetValue(id, out var node) ? node.Value : null;
                }
            }

            public IEnumerable<MemoryItem> GetItemsByCategory(string category)
            {
                lock (_lock)
                {
                    return _items.Where(i => i.Category == category).ToList();
                }
            }

            public IEnumerable<MemoryItem> GetMostRelevant(int count)
            {
                lock (_lock)
                {
                    return _items.OrderByDescending(i => i.Relevance).Take(count).ToList();
                }
            }

            public void ApplyDecay(float decayFactor)
            {
                lock (_lock)
                {
                    foreach (var item in _items)
                    {
                        item.Relevance *= decayFactor;
                    }
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    _items.Clear();
                    _index.Clear();
                }
            }
        }

        private class MemoryItem
        {
            public string Id { get; set; }
            public string Content { get; set; }
            public string Category { get; set; }
            public float Relevance { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion
    }
}
