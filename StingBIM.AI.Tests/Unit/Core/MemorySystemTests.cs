using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Core.Memory;

namespace StingBIM.AI.Tests.Unit.Core
{
    /// <summary>
    /// Unit tests for the three-tier memory system:
    /// WorkingMemory (short-term), SemanticMemory (facts), EpisodicMemory (events).
    /// </summary>
    [TestFixture]
    public class MemorySystemTests
    {
        #region WorkingMemory Tests

        [TestFixture]
        public class WorkingMemoryTests
        {
            private WorkingMemory _memory;

            [SetUp]
            public void SetUp()
            {
                _memory = new WorkingMemory(maxCapacity: 7);
            }

            [Test]
            public void Constructor_DefaultCapacity_Creates7SlotMemory()
            {
                var memory = new WorkingMemory();

                memory.Items.Should().BeEmpty();
                memory.CurrentCommand.Should().BeNull();
            }

            [Test]
            public void SetCurrentCommand_SetsCommandAndAddsItem()
            {
                _memory.SetCurrentCommand("create a wall");

                _memory.CurrentCommand.Should().Be("create a wall");
                _memory.Items.Should().Contain(i => i.Type == MemoryItemType.Command);
            }

            [Test]
            public void SetSelectedElements_SetsReadOnlyList()
            {
                _memory.SetSelectedElements(new[] { 100, 200, 300 });

                _memory.SelectedElementIds.Should().HaveCount(3);
                _memory.SelectedElementIds.Should().Contain(200);
            }

            [Test]
            public void AddItem_WithinCapacity_AddsToFront()
            {
                _memory.AddItem(new MemoryItem
                {
                    Type = MemoryItemType.Response,
                    Content = "First"
                });
                _memory.AddItem(new MemoryItem
                {
                    Type = MemoryItemType.Response,
                    Content = "Second"
                });

                _memory.Items.First().Content.Should().Be("Second");
            }

            [Test]
            public void AddItem_ExceedsCapacity_RemovesOldest()
            {
                var memory = new WorkingMemory(maxCapacity: 3);

                for (int i = 0; i < 5; i++)
                {
                    memory.AddItem(new MemoryItem
                    {
                        Type = MemoryItemType.Element,
                        Content = $"Item {i}"
                    });
                }

                memory.Items.Count().Should().Be(3);
                // Most recent items should survive
                memory.Items.First().Content.Should().Be("Item 4");
            }

            [Test]
            public void GetMostRecent_ByType_ReturnsCorrectItem()
            {
                _memory.AddItem(new MemoryItem { Type = MemoryItemType.Error, Content = "old error" });
                _memory.AddItem(new MemoryItem { Type = MemoryItemType.Response, Content = "a response" });
                _memory.AddItem(new MemoryItem { Type = MemoryItemType.Error, Content = "new error" });

                var recent = _memory.GetMostRecent(MemoryItemType.Error);

                recent.Should().NotBeNull();
                recent.Content.Should().Be("new error");
            }

            [Test]
            public void GetMostRecent_NoMatch_ReturnsNull()
            {
                _memory.AddItem(new MemoryItem { Type = MemoryItemType.Command, Content = "cmd" });

                var recent = _memory.GetMostRecent(MemoryItemType.Clarification);

                recent.Should().BeNull();
            }

            [Test]
            public void UpdateContext_SetsContext()
            {
                var ctx = new ConversationContext
                {
                    Topic = "wall creation",
                    TurnCount = 3
                };

                _memory.UpdateContext(ctx);

                _memory.Context.Should().NotBeNull();
                _memory.Context.Topic.Should().Be("wall creation");
            }

            [Test]
            public void Clear_ResetsEverything()
            {
                _memory.SetCurrentCommand("test");
                _memory.SetSelectedElements(new[] { 1, 2 });
                _memory.UpdateContext(new ConversationContext { Topic = "test" });
                _memory.AddItem(new MemoryItem { Type = MemoryItemType.Command, Content = "test" });

                _memory.Clear();

                _memory.CurrentCommand.Should().BeNull();
                _memory.SelectedElementIds.Should().BeEmpty();
                _memory.Context.Should().BeNull();
                _memory.Items.Should().BeEmpty();
            }

            [Test]
            public void TimeSinceLastAccess_RecentAccess_IsSmall()
            {
                _memory.SetCurrentCommand("test");

                _memory.TimeSinceLastAccess.Should().BeLessThan(TimeSpan.FromSeconds(2));
            }

            [Test]
            public void MemoryItem_Defaults_AreCorrect()
            {
                var item = new MemoryItem();

                item.Importance.Should().Be(0.5f);
                item.Metadata.Should().NotBeNull();
                item.Metadata.Should().BeEmpty();
            }
        }

        #endregion

        #region SemanticMemory Tests

        [TestFixture]
        public class SemanticMemoryTests
        {
            private SemanticMemory _memory;
            private string _testPath;

            [SetUp]
            public void SetUp()
            {
                _testPath = Path.Combine(Path.GetTempPath(), "stingbim_test", $"semantic_{Guid.NewGuid()}.json");
                _memory = new SemanticMemory(_testPath);
            }

            [TearDown]
            public void TearDown()
            {
                if (File.Exists(_testPath))
                    File.Delete(_testPath);
            }

            [Test]
            public void StoreFact_AutoGeneratesId()
            {
                var fact = new SemanticFact
                {
                    Subject = "Kitchen",
                    Predicate = "requires",
                    Object = "2m² counter space"
                };

                _memory.StoreFact(fact);

                fact.Id.Should().NotBeNullOrEmpty();
                _memory.Count.Should().Be(1);
            }

            [Test]
            public void StoreFact_PreservesExistingId()
            {
                var fact = new SemanticFact
                {
                    Id = "custom-id",
                    Subject = "Room",
                    Predicate = "has",
                    Object = "minimum area"
                };

                _memory.StoreFact(fact);

                _memory.GetFact("custom-id").Should().NotBeNull();
            }

            [Test]
            public void GetFact_ExistingId_ReturnsFact()
            {
                var fact = new SemanticFact
                {
                    Subject = "Wall",
                    Predicate = "has_type",
                    Object = "Partition"
                };
                _memory.StoreFact(fact);

                var retrieved = _memory.GetFact(fact.Id);

                retrieved.Should().NotBeNull();
                retrieved.Subject.Should().Be("Wall");
            }

            [Test]
            public void GetFact_NonExistentId_ReturnsNull()
            {
                var result = _memory.GetFact("nonexistent");

                result.Should().BeNull();
            }

            [Test]
            public void QueryBySubject_MatchingFacts_ReturnsAll()
            {
                _memory.StoreFact(new SemanticFact { Subject = "Kitchen", Predicate = "area", Object = "12m²" });
                _memory.StoreFact(new SemanticFact { Subject = "Kitchen", Predicate = "height", Object = "2.7m" });
                _memory.StoreFact(new SemanticFact { Subject = "Bathroom", Predicate = "area", Object = "6m²" });

                var results = _memory.QueryBySubject("Kitchen").ToList();

                results.Should().HaveCount(2);
                results.Should().AllSatisfy(f => f.Subject.Should().Be("Kitchen"));
            }

            [Test]
            public void QueryBySubject_CaseInsensitive()
            {
                _memory.StoreFact(new SemanticFact { Subject = "Wall", Predicate = "is", Object = "structural" });

                var results = _memory.QueryBySubject("wall").ToList();

                results.Should().HaveCount(1);
            }

            [Test]
            public void QueryByCategory_MatchingFacts_ReturnsAll()
            {
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Concrete", Predicate = "strength", Object = "30 MPa",
                    Category = "Materials"
                });
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Steel", Predicate = "grade", Object = "S275",
                    Category = "Materials"
                });
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Room", Predicate = "area", Object = "20m²",
                    Category = "Spaces"
                });

                var results = _memory.QueryByCategory("Materials").ToList();

                results.Should().HaveCount(2);
            }

            [Test]
            public void QueryTriple_BySubjectAndPredicate_FiltersCorrectly()
            {
                _memory.StoreFact(new SemanticFact { Subject = "Wall", Predicate = "material", Object = "Concrete" });
                _memory.StoreFact(new SemanticFact { Subject = "Wall", Predicate = "height", Object = "3m" });
                _memory.StoreFact(new SemanticFact { Subject = "Floor", Predicate = "material", Object = "Concrete" });

                var results = _memory.QueryTriple(subject: "Wall", predicate: "material").ToList();

                results.Should().HaveCount(1);
                results[0].Object.Should().Be("Concrete");
            }

            [Test]
            public void QueryTriple_ByObjectOnly_FindsAllWithThatObject()
            {
                _memory.StoreFact(new SemanticFact { Subject = "Wall", Predicate = "made_of", Object = "Concrete" });
                _memory.StoreFact(new SemanticFact { Subject = "Column", Predicate = "made_of", Object = "Concrete" });
                _memory.StoreFact(new SemanticFact { Subject = "Beam", Predicate = "made_of", Object = "Steel" });

                var results = _memory.QueryTriple(obj: "Concrete").ToList();

                results.Should().HaveCount(2);
            }

            [Test]
            public void Search_RelevantTerms_ReturnsRankedResults()
            {
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Kitchen", Predicate = "requires", Object = "ventilation",
                    Description = "Kitchen areas need mechanical ventilation", Confidence = 0.9f
                });
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Office", Predicate = "has", Object = "HVAC system",
                    Description = "Office spaces use central HVAC", Confidence = 0.8f
                });

                var results = _memory.Search("kitchen ventilation").ToList();

                results.Should().NotBeEmpty();
                results.First().Subject.Should().Be("Kitchen");
            }

            [Test]
            public void Search_NoMatch_ReturnsEmpty()
            {
                _memory.StoreFact(new SemanticFact { Subject = "A", Predicate = "B", Object = "C" });

                var results = _memory.Search("xyz123nonexistent").ToList();

                results.Should().BeEmpty();
            }

            [Test]
            public void ReinforceFact_IncreasesConfidence()
            {
                var fact = new SemanticFact
                {
                    Subject = "Test", Predicate = "is", Object = "true",
                    Confidence = 0.5f
                };
                _memory.StoreFact(fact);

                _memory.ReinforceFact(fact.Id, 0.2f);

                var updated = _memory.GetFact(fact.Id);
                updated.Confidence.Should().BeApproximately(0.7f, 0.01f);
                updated.ReinforcementCount.Should().Be(1);
            }

            [Test]
            public void ReinforceFact_ClampsToBounds()
            {
                var fact = new SemanticFact { Subject = "A", Predicate = "B", Object = "C", Confidence = 0.9f };
                _memory.StoreFact(fact);

                _memory.ReinforceFact(fact.Id, 0.5f);

                _memory.GetFact(fact.Id).Confidence.Should().Be(1.0f);
            }

            [Test]
            public async Task SaveAndLoad_RoundTrip_PreservesData()
            {
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Wall", Predicate = "type", Object = "Partition",
                    Category = "Elements", Confidence = 0.85f
                });
                _memory.StoreFact(new SemanticFact
                {
                    Subject = "Door", Predicate = "width", Object = "0.9m",
                    Category = "Elements", Confidence = 0.9f
                });

                await _memory.SaveAsync();

                // Load into new instance
                var loaded = new SemanticMemory(_testPath);
                await loaded.LoadAsync();

                loaded.Count.Should().Be(2);
                loaded.QueryBySubject("Wall").Should().NotBeEmpty();
                loaded.QueryBySubject("Door").Should().NotBeEmpty();
            }

            [Test]
            public async Task LoadAsync_NonExistentFile_StartsEmpty()
            {
                var memory = new SemanticMemory("/tmp/nonexistent_file.json");

                await memory.LoadAsync();

                memory.Count.Should().Be(0);
            }
        }

        #endregion

        #region EpisodicMemory Tests

        [TestFixture]
        public class EpisodicMemoryTests
        {
            private EpisodicMemory _memory;
            private string _testPath;

            [SetUp]
            public void SetUp()
            {
                _testPath = Path.Combine(Path.GetTempPath(), "stingbim_test", $"episodic_{Guid.NewGuid()}.json");
                _memory = new EpisodicMemory(_testPath, maxEpisodesInMemory: 100);
            }

            [TearDown]
            public void TearDown()
            {
                if (File.Exists(_testPath))
                    File.Delete(_testPath);
            }

            [Test]
            public void RecordEpisode_AssignsIdAndTimestamp()
            {
                var episode = new Episode
                {
                    Action = "create wall",
                    Context = "floor plan",
                    Outcome = EpisodeOutcome.Accepted
                };

                _memory.RecordEpisode(episode);

                episode.Id.Should().NotBeNullOrEmpty();
                episode.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
                _memory.Count.Should().Be(1);
            }

            [Test]
            public void RecordEpisode_ExceedsCapacity_TrimsToLimit()
            {
                var memory = new EpisodicMemory(_testPath, maxEpisodesInMemory: 5);

                for (int i = 0; i < 10; i++)
                {
                    memory.RecordEpisode(new Episode
                    {
                        Action = $"action {i}",
                        Outcome = EpisodeOutcome.Accepted,
                        Importance = i * 0.1f
                    });
                }

                memory.Count.Should().Be(5);
            }

            [Test]
            public void FindSimilarEpisodes_MatchingAction_ReturnsResults()
            {
                _memory.RecordEpisode(new Episode { Action = "create wall", Context = "floor plan", Outcome = EpisodeOutcome.Accepted });
                _memory.RecordEpisode(new Episode { Action = "create wall", Context = "elevation", Outcome = EpisodeOutcome.Corrected });
                _memory.RecordEpisode(new Episode { Action = "create door", Context = "floor plan", Outcome = EpisodeOutcome.Accepted });

                var similar = _memory.FindSimilarEpisodes("create wall").ToList();

                similar.Should().HaveCount(2);
            }

            [Test]
            public void FindSimilarEpisodes_WithContext_FiltersCorrectly()
            {
                _memory.RecordEpisode(new Episode { Action = "create wall", Context = "floor plan", Outcome = EpisodeOutcome.Accepted });
                _memory.RecordEpisode(new Episode { Action = "create wall", Context = "elevation", Outcome = EpisodeOutcome.Accepted });

                var similar = _memory.FindSimilarEpisodes("create wall", "floor plan").ToList();

                similar.Should().HaveCount(1);
                similar.First().Context.Should().Contain("floor plan");
            }

            [Test]
            public void FindSimilarEpisodes_NoMatch_ReturnsEmpty()
            {
                _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Accepted });

                var similar = _memory.FindSimilarEpisodes("delete room").ToList();

                similar.Should().BeEmpty();
            }

            [Test]
            public void GetCorrectionEpisodes_ReturnsCorrectedAndUndone()
            {
                _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Accepted });
                _memory.RecordEpisode(new Episode { Action = "move door", Outcome = EpisodeOutcome.Corrected, UserCorrection = "moved to left" });
                _memory.RecordEpisode(new Episode { Action = "delete window", Outcome = EpisodeOutcome.Undone });
                _memory.RecordEpisode(new Episode { Action = "resize room", Outcome = EpisodeOutcome.Failed });

                var corrections = _memory.GetCorrectionEpisodes().ToList();

                corrections.Should().HaveCount(2);
                corrections.Should().OnlyContain(e =>
                    e.Outcome == EpisodeOutcome.Corrected || e.Outcome == EpisodeOutcome.Undone);
            }

            [Test]
            public void GetRepeatedPatterns_WithRepeats_DetectsPatterns()
            {
                // Record the same action 5 times
                for (int i = 0; i < 5; i++)
                {
                    _memory.RecordEpisode(new Episode
                    {
                        Action = "move door",
                        Outcome = i < 4 ? EpisodeOutcome.Accepted : EpisodeOutcome.Corrected
                    });
                }
                _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Accepted });

                var patterns = _memory.GetRepeatedPatterns(TimeSpan.FromHours(1), minOccurrences: 3).ToList();

                patterns.Should().HaveCount(1);
                patterns.First().Action.Should().Be("move door");
                patterns.First().Occurrences.Should().Be(5);
                patterns.First().SuccessRate.Should().BeApproximately(0.8f, 0.01f);
            }

            [Test]
            public void GetRepeatedPatterns_NoRepeats_ReturnsEmpty()
            {
                _memory.RecordEpisode(new Episode { Action = "action A", Outcome = EpisodeOutcome.Accepted });
                _memory.RecordEpisode(new Episode { Action = "action B", Outcome = EpisodeOutcome.Accepted });

                var patterns = _memory.GetRepeatedPatterns(TimeSpan.FromHours(1), minOccurrences: 3).ToList();

                patterns.Should().BeEmpty();
            }

            [Test]
            public void GetActionSuccessRate_AllAccepted_Returns1()
            {
                for (int i = 0; i < 5; i++)
                {
                    _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Accepted });
                }

                var rate = _memory.GetActionSuccessRate("create wall");

                rate.Should().Be(1.0f);
            }

            [Test]
            public void GetActionSuccessRate_Mixed_ReturnsCorrectRate()
            {
                _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Accepted });
                _memory.RecordEpisode(new Episode { Action = "create wall", Outcome = EpisodeOutcome.Failed });

                var rate = _memory.GetActionSuccessRate("create wall");

                rate.Should().BeApproximately(0.5f, 0.01f);
            }

            [Test]
            public void GetActionSuccessRate_NoData_ReturnsNeutral()
            {
                var rate = _memory.GetActionSuccessRate("unknown action");

                rate.Should().Be(0.5f);
            }

            [Test]
            public async Task SaveAndLoad_RoundTrip_PreservesEpisodes()
            {
                _memory.RecordEpisode(new Episode
                {
                    Action = "create wall",
                    Context = "floor plan",
                    Outcome = EpisodeOutcome.Accepted,
                    Importance = 0.8f
                });

                await _memory.SaveAsync();

                var loaded = new EpisodicMemory(_testPath);
                await loaded.LoadAsync();

                loaded.Count.Should().Be(1);
                loaded.FindSimilarEpisodes("create wall").Should().NotBeEmpty();
            }

            [Test]
            public async Task LoadAsync_NonExistentFile_StartsEmpty()
            {
                var memory = new EpisodicMemory("/tmp/nonexistent_episodes.json");

                await memory.LoadAsync();

                memory.Count.Should().Be(0);
            }
        }

        #endregion
    }
}
