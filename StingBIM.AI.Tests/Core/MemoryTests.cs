// StingBIM.AI.Tests.Core.MemoryTests
// Comprehensive tests for WorkingMemory, SemanticMemory, and EpisodicMemory
// Tests cover construction, storage, retrieval, capacity, thread safety, and edge cases

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Core.Memory;

namespace StingBIM.AI.Tests.Core
{
    // WorkingMemoryTests moved to dedicated WorkingMemoryTests.cs

    #region SemanticMemoryTests

    [TestFixture]
    public class SemanticMemoryTests
    {
        private SemanticMemory _semanticMemory;
        private string _tempStoragePath;

        [SetUp]
        public void SetUp()
        {
            _tempStoragePath = Path.Combine(Path.GetTempPath(), "StingBIM_Tests", $"semantic_{Guid.NewGuid()}.json");
            _semanticMemory = new SemanticMemory(_tempStoragePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempStoragePath))
            {
                File.Delete(_tempStoragePath);
            }

            var dir = Path.GetDirectoryName(_tempStoragePath);
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }

        [Test]
        public void Constructor_ShouldCreateEmptyStorage()
        {
            // Assert
            _semanticMemory.Count.Should().Be(0);
        }

        [Test]
        public void StoreFact_ShouldStoreAndRetrieveById()
        {
            // Arrange
            var fact = new SemanticFact
            {
                Id = "fact-001",
                Subject = "Kitchen",
                Predicate = "requires",
                Object = "2m² counter space",
                Category = "DesignRules",
                Confidence = 0.9f
            };

            // Act
            _semanticMemory.StoreFact(fact);
            var retrieved = _semanticMemory.GetFact("fact-001");

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be("fact-001");
            retrieved.Subject.Should().Be("Kitchen");
            retrieved.Predicate.Should().Be("requires");
            retrieved.Object.Should().Be("2m² counter space");
            retrieved.Category.Should().Be("DesignRules");
            retrieved.Confidence.Should().Be(0.9f);
        }

        [Test]
        public void StoreFact_WithEmptyId_ShouldAutoAssignId()
        {
            // Arrange
            var fact = new SemanticFact
            {
                Subject = "Bathroom",
                Predicate = "minimum_area",
                Object = "3.5m²"
            };

            // Act
            _semanticMemory.StoreFact(fact);

            // Assert
            fact.Id.Should().NotBeNullOrEmpty();
            _semanticMemory.Count.Should().Be(1);
            var retrieved = _semanticMemory.GetFact(fact.Id);
            retrieved.Should().NotBeNull();
            retrieved.Subject.Should().Be("Bathroom");
        }

        [Test]
        public void StoreFact_WithNullId_ShouldAutoAssignId()
        {
            // Arrange
            var fact = new SemanticFact
            {
                Id = null,
                Subject = "Corridor",
                Predicate = "minimum_width",
                Object = "1200mm"
            };

            // Act
            _semanticMemory.StoreFact(fact);

            // Assert
            fact.Id.Should().NotBeNullOrEmpty();
            _semanticMemory.GetFact(fact.Id).Should().NotBeNull();
        }

        [Test]
        public void StoreFact_ShouldSetLastUpdatedTimestamp()
        {
            // Arrange
            var beforeStore = DateTime.Now;
            var fact = new SemanticFact
            {
                Id = "fact-time",
                Subject = "Door",
                Predicate = "standard_height",
                Object = "2100mm"
            };

            // Act
            _semanticMemory.StoreFact(fact);

            // Assert
            fact.LastUpdated.Should().BeOnOrAfter(beforeStore);
            fact.LastUpdated.Should().BeOnOrBefore(DateTime.Now);
        }

        [Test]
        public void QueryBySubject_ShouldReturnMatchingFacts()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "max_height", Object = "3600mm" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f2", Subject = "Wall", Predicate = "min_thickness", Object = "100mm" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f3", Subject = "Floor", Predicate = "max_span", Object = "6000mm" });

            // Act
            var results = _semanticMemory.QueryBySubject("Wall").ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(f => f.Subject.Should().Be("Wall"));
        }

        [Test]
        public void QueryBySubject_ShouldBeCaseInsensitive()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete" });

            // Act
            var results = _semanticMemory.QueryBySubject("wall").ToList();

            // Assert
            results.Should().HaveCount(1);
        }

        [Test]
        public void QueryBySubject_WhenNoMatch_ShouldReturnEmpty()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete" });

            // Act
            var results = _semanticMemory.QueryBySubject("Roof").ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [Test]
        public void QueryByCategory_ShouldReturnCategorizedFacts()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete", Category = "Structural" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f2", Subject = "Beam", Predicate = "type", Object = "Steel", Category = "Structural" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f3", Subject = "Duct", Predicate = "type", Object = "Galvanized", Category = "MEP" });

            // Act
            var results = _semanticMemory.QueryByCategory("Structural").ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(f => f.Category.Should().Be("Structural"));
        }

        [Test]
        public void QueryByCategory_WhenNoMatch_ShouldReturnEmpty()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete", Category = "Structural" });

            // Act
            var results = _semanticMemory.QueryByCategory("Plumbing").ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [Test]
        public void QueryByCategory_ShouldBeCaseInsensitive()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete", Category = "Structural" });

            // Act
            var results = _semanticMemory.QueryByCategory("structural").ToList();

            // Assert
            results.Should().HaveCount(1);
        }

        [Test]
        public void Search_ShouldFindFactsByTextRelevance()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f1",
                Subject = "Kitchen",
                Predicate = "requires",
                Object = "ventilation",
                Description = "Kitchens require adequate ventilation per ASHRAE",
                Confidence = 0.9f
            });
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f2",
                Subject = "Bathroom",
                Predicate = "requires",
                Object = "waterproofing",
                Description = "Bathrooms need waterproof membranes",
                Confidence = 0.8f
            });

            // Act
            var results = _semanticMemory.Search("kitchen ventilation").ToList();

            // Assert
            results.Should().NotBeEmpty();
            results.First().Subject.Should().Be("Kitchen");
        }

        [Test]
        public void Search_WithNoMatches_ShouldReturnEmpty()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f1",
                Subject = "Wall",
                Predicate = "type",
                Object = "Concrete",
                Confidence = 0.9f
            });

            // Act
            var results = _semanticMemory.Search("plumbing fixtures").ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [Test]
        public void Search_ShouldRespectMaxResults()
        {
            // Arrange
            for (int i = 0; i < 20; i++)
            {
                _semanticMemory.StoreFact(new SemanticFact
                {
                    Id = $"f{i}",
                    Subject = "Wall",
                    Predicate = "property",
                    Object = $"Value {i}",
                    Confidence = 0.9f
                });
            }

            // Act
            var results = _semanticMemory.Search("wall", maxResults: 5).ToList();

            // Assert
            results.Count.Should().BeLessOrEqualTo(5);
        }

        [Test]
        public void QueryTriple_ShouldMatchSubjectPredicateObject()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "has_material", Object = "Concrete" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f2", Subject = "Wall", Predicate = "has_material", Object = "Brick" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f3", Subject = "Floor", Predicate = "has_material", Object = "Concrete" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f4", Subject = "Wall", Predicate = "has_height", Object = "3000mm" });

            // Act
            var bySubjectAndPredicate = _semanticMemory.QueryTriple(subject: "Wall", predicate: "has_material").ToList();
            var bySubjectOnly = _semanticMemory.QueryTriple(subject: "Wall").ToList();
            var byObjectOnly = _semanticMemory.QueryTriple(obj: "Concrete").ToList();
            var byAll = _semanticMemory.QueryTriple(subject: "Wall", predicate: "has_material", obj: "Brick").ToList();

            // Assert
            bySubjectAndPredicate.Should().HaveCount(2);
            bySubjectOnly.Should().HaveCount(3);
            byObjectOnly.Should().HaveCount(2);
            byAll.Should().HaveCount(1);
            byAll.First().Object.Should().Be("Brick");
        }

        [Test]
        public void QueryTriple_WithNoFilters_ShouldReturnAllFacts()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "A", Predicate = "B", Object = "C" });
            _semanticMemory.StoreFact(new SemanticFact { Id = "f2", Subject = "D", Predicate = "E", Object = "F" });

            // Act
            var results = _semanticMemory.QueryTriple().ToList();

            // Assert
            results.Should().HaveCount(2);
        }

        [Test]
        public void ReinforceFact_ShouldAdjustConfidence()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f1",
                Subject = "Wall",
                Predicate = "type",
                Object = "Concrete",
                Confidence = 0.5f
            });

            // Act
            _semanticMemory.ReinforceFact("f1", 0.2f);
            var fact = _semanticMemory.GetFact("f1");

            // Assert
            fact.Confidence.Should().BeApproximately(0.7f, 0.001f);
            fact.ReinforcementCount.Should().Be(1);
        }

        [Test]
        public void ReinforceFact_ShouldClampConfidenceAt1()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f1",
                Subject = "Wall",
                Predicate = "type",
                Object = "Concrete",
                Confidence = 0.9f
            });

            // Act
            _semanticMemory.ReinforceFact("f1", 0.5f);
            var fact = _semanticMemory.GetFact("f1");

            // Assert
            fact.Confidence.Should().Be(1.0f);
        }

        [Test]
        public void ReinforceFact_ShouldClampConfidenceAt0()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact
            {
                Id = "f1",
                Subject = "Wall",
                Predicate = "type",
                Object = "Concrete",
                Confidence = 0.2f
            });

            // Act
            _semanticMemory.ReinforceFact("f1", -0.5f);
            var fact = _semanticMemory.GetFact("f1");

            // Assert
            fact.Confidence.Should().Be(0.0f);
        }

        [Test]
        public void ReinforceFact_WithNonExistentId_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _semanticMemory.ReinforceFact("nonexistent", 0.1f);
            action.Should().NotThrow();
        }

        [Test]
        public void ReinforceFact_ShouldIncrementReinforcementCount()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "A", Predicate = "B", Object = "C" });

            // Act
            _semanticMemory.ReinforceFact("f1", 0.1f);
            _semanticMemory.ReinforceFact("f1", 0.1f);
            _semanticMemory.ReinforceFact("f1", 0.1f);

            // Assert
            _semanticMemory.GetFact("f1").ReinforcementCount.Should().Be(3);
        }

        [Test]
        public void Count_ShouldBeAccurate()
        {
            // Arrange & Act
            _semanticMemory.Count.Should().Be(0);

            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "A", Predicate = "B", Object = "C" });
            _semanticMemory.Count.Should().Be(1);

            _semanticMemory.StoreFact(new SemanticFact { Id = "f2", Subject = "D", Predicate = "E", Object = "F" });
            _semanticMemory.Count.Should().Be(2);

            // Overwrite existing fact
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "X", Predicate = "Y", Object = "Z" });
            _semanticMemory.Count.Should().Be(2);
        }

        [Test]
        public void GetFact_WithNonExistentId_ShouldReturnNull()
        {
            // Act
            var result = _semanticMemory.GetFact("nonexistent");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void StoreFact_ShouldOverwriteExistingFactWithSameId()
        {
            // Arrange
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Concrete" });

            // Act
            _semanticMemory.StoreFact(new SemanticFact { Id = "f1", Subject = "Wall", Predicate = "type", Object = "Brick" });

            // Assert
            var fact = _semanticMemory.GetFact("f1");
            fact.Object.Should().Be("Brick");
            _semanticMemory.Count.Should().Be(1);
        }
    }

    #endregion

    #region EpisodicMemoryTests

    [TestFixture]
    public class EpisodicMemoryTests
    {
        private EpisodicMemory _episodicMemory;
        private string _tempStoragePath;

        [SetUp]
        public void SetUp()
        {
            _tempStoragePath = Path.Combine(Path.GetTempPath(), "StingBIM_Tests", $"episodic_{Guid.NewGuid()}.json");
            _episodicMemory = new EpisodicMemory(_tempStoragePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempStoragePath))
            {
                File.Delete(_tempStoragePath);
            }

            var dir = Path.GetDirectoryName(_tempStoragePath);
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }

        [Test]
        public void Constructor_ShouldCreateEmptyStorage()
        {
            // Assert
            _episodicMemory.Count.Should().Be(0);
        }

        [Test]
        public void RecordEpisode_ShouldStoreEpisodeWithAutoAssignedIdAndTimestamp()
        {
            // Arrange
            var beforeRecord = DateTime.Now;
            var episode = new Episode
            {
                Action = "Create wall",
                Context = "Level 1",
                Outcome = EpisodeOutcome.Accepted,
                UserId = "user1"
            };

            // Act
            _episodicMemory.RecordEpisode(episode);

            // Assert
            _episodicMemory.Count.Should().Be(1);
            episode.Id.Should().NotBeNullOrEmpty();
            episode.Timestamp.Should().BeOnOrAfter(beforeRecord);
            episode.Timestamp.Should().BeOnOrBefore(DateTime.Now);
        }

        [Test]
        public void RecordEpisode_ShouldOverwriteExistingIdWithNewGuid()
        {
            // Arrange
            var episode = new Episode
            {
                Id = "my-custom-id",
                Action = "Create wall",
                Outcome = EpisodeOutcome.Accepted
            };

            // Act
            _episodicMemory.RecordEpisode(episode);

            // Assert - RecordEpisode always assigns a new GUID
            episode.Id.Should().NotBe("my-custom-id");
            episode.Id.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void RecordEpisode_WhenExceedingMaxMemory_ShouldTrimToCapacity()
        {
            // Arrange - create memory with small capacity
            var memory = new EpisodicMemory(_tempStoragePath, maxEpisodesInMemory: 3);

            // Act - add 5 episodes, exceeding limit of 3
            for (int i = 0; i < 5; i++)
            {
                memory.RecordEpisode(new Episode
                {
                    Action = $"Action {i}",
                    Outcome = EpisodeOutcome.Accepted,
                    Importance = 0.5f
                });
            }

            // Assert
            memory.Count.Should().Be(3);
        }

        [Test]
        public void RecordEpisode_WhenTrimming_ShouldKeepMostImportant()
        {
            // Arrange
            var memory = new EpisodicMemory(_tempStoragePath, maxEpisodesInMemory: 2);

            // Act - add 3 episodes with varying importance
            memory.RecordEpisode(new Episode
            {
                Action = "Low importance action",
                Outcome = EpisodeOutcome.Accepted,
                Importance = 0.1f
            });
            memory.RecordEpisode(new Episode
            {
                Action = "High importance action",
                Outcome = EpisodeOutcome.Accepted,
                Importance = 0.9f
            });
            memory.RecordEpisode(new Episode
            {
                Action = "Medium importance action",
                Outcome = EpisodeOutcome.Accepted,
                Importance = 0.5f
            });

            // Assert - should keep highest importance episodes
            memory.Count.Should().Be(2);
            var episodes = memory.FindSimilarEpisodes("action", maxResults: 10).ToList();
            episodes.Should().Contain(e => e.Action == "High importance action");
        }

        [Test]
        public void FindSimilarEpisodes_ShouldFindByActionText()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall on Level 1", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Delete floor element", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall on Level 2", Outcome = EpisodeOutcome.Corrected });

            // Act
            var results = _episodicMemory.FindSimilarEpisodes("Create wall").ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(e => e.Action.Should().Contain("Create wall"));
        }

        [Test]
        public void FindSimilarEpisodes_ShouldBeCaseInsensitive()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create Wall", Outcome = EpisodeOutcome.Accepted });

            // Act
            var results = _episodicMemory.FindSimilarEpisodes("create wall").ToList();

            // Assert
            results.Should().HaveCount(1);
        }

        [Test]
        public void FindSimilarEpisodes_ShouldFilterByContext()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Context = "Level 1", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Context = "Level 2", Outcome = EpisodeOutcome.Corrected });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Context = "Level 1 - North Wing", Outcome = EpisodeOutcome.Accepted });

            // Act
            var results = _episodicMemory.FindSimilarEpisodes("Create wall", context: "Level 1").ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(e => e.Context.Should().Contain("Level 1"));
        }

        [Test]
        public void FindSimilarEpisodes_WithNoMatches_ShouldReturnEmpty()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });

            // Act
            var results = _episodicMemory.FindSimilarEpisodes("Delete roof").ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [Test]
        public void FindSimilarEpisodes_ShouldReturnOrderedByTimestampDescending()
        {
            // Arrange - RecordEpisode sets timestamp to DateTime.Now, so add a small delay
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall first", Outcome = EpisodeOutcome.Accepted });
            Thread.Sleep(10);
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall second", Outcome = EpisodeOutcome.Accepted });

            // Act
            var results = _episodicMemory.FindSimilarEpisodes("Create wall").ToList();

            // Assert
            results.Should().HaveCount(2);
            results[0].Action.Should().Be("Create wall second");
            results[1].Action.Should().Be("Create wall first");
        }

        [Test]
        public void GetCorrectionEpisodes_ShouldReturnCorrectedAndUndoneOutcomes()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Move door", Outcome = EpisodeOutcome.Corrected, UserCorrection = "Moved 200mm left" });
            _episodicMemory.RecordEpisode(new Episode { Action = "Delete window", Outcome = EpisodeOutcome.Undone });
            _episodicMemory.RecordEpisode(new Episode { Action = "Resize room", Outcome = EpisodeOutcome.Failed });
            _episodicMemory.RecordEpisode(new Episode { Action = "Add column", Outcome = EpisodeOutcome.Abandoned });

            // Act
            var corrections = _episodicMemory.GetCorrectionEpisodes().ToList();

            // Assert
            corrections.Should().HaveCount(2);
            corrections.Should().AllSatisfy(e =>
                (e.Outcome == EpisodeOutcome.Corrected || e.Outcome == EpisodeOutcome.Undone).Should().BeTrue()
            );
        }

        [Test]
        public void GetCorrectionEpisodes_WhenNone_ShouldReturnEmpty()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create floor", Outcome = EpisodeOutcome.Accepted });

            // Act
            var corrections = _episodicMemory.GetCorrectionEpisodes().ToList();

            // Assert
            corrections.Should().BeEmpty();
        }

        [Test]
        public void GetRepeatedPatterns_ShouldGroupByActionWithinTimeWindow()
        {
            // Arrange - add repeated actions
            for (int i = 0; i < 5; i++)
            {
                _episodicMemory.RecordEpisode(new Episode
                {
                    Action = "Move door",
                    Outcome = i < 3 ? EpisodeOutcome.Accepted : EpisodeOutcome.Corrected
                });
            }
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });

            // Act - window of 1 hour, minimum 3 occurrences
            var patterns = _episodicMemory.GetRepeatedPatterns(TimeSpan.FromHours(1), minOccurrences: 3).ToList();

            // Assert
            patterns.Should().HaveCount(1);
            patterns.First().Action.Should().Be("Move door");
            patterns.First().Occurrences.Should().Be(5);
            patterns.First().SuccessRate.Should().BeApproximately(3f / 5f, 0.001f);
        }

        [Test]
        public void GetRepeatedPatterns_WhenBelowMinOccurrences_ShouldReturnEmpty()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });

            // Act
            var patterns = _episodicMemory.GetRepeatedPatterns(TimeSpan.FromHours(1), minOccurrences: 3).ToList();

            // Assert
            patterns.Should().BeEmpty();
        }

        [Test]
        public void GetRepeatedPatterns_ShouldOrderByOccurrencesDescending()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                _episodicMemory.RecordEpisode(new Episode { Action = "Move door", Outcome = EpisodeOutcome.Accepted });
            }
            for (int i = 0; i < 3; i++)
            {
                _episodicMemory.RecordEpisode(new Episode { Action = "Resize room", Outcome = EpisodeOutcome.Accepted });
            }

            // Act
            var patterns = _episodicMemory.GetRepeatedPatterns(TimeSpan.FromHours(1), minOccurrences: 3).ToList();

            // Assert
            patterns.Should().HaveCount(2);
            patterns[0].Occurrences.Should().BeGreaterThanOrEqualTo(patterns[1].Occurrences);
        }

        [Test]
        public void GetActionSuccessRate_WhenNoData_ShouldReturnHalf()
        {
            // Act
            var rate = _episodicMemory.GetActionSuccessRate("nonexistent action");

            // Assert
            rate.Should().Be(0.5f);
        }

        [Test]
        public void GetActionSuccessRate_ShouldCalculateCorrectly()
        {
            // Arrange - 3 accepted, 1 corrected, 1 failed out of 5
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall element", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall on level", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall section", Outcome = EpisodeOutcome.Corrected });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall partition", Outcome = EpisodeOutcome.Failed });

            // Act
            var rate = _episodicMemory.GetActionSuccessRate("Create wall");

            // Assert - 3 accepted out of 5 = 0.6
            rate.Should().BeApproximately(3f / 5f, 0.001f);
        }

        [Test]
        public void GetActionSuccessRate_ShouldBeCaseInsensitive()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create Wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "CREATE WALL element", Outcome = EpisodeOutcome.Corrected });

            // Act
            var rate = _episodicMemory.GetActionSuccessRate("create wall");

            // Assert
            rate.Should().BeApproximately(0.5f, 0.001f);
        }

        [Test]
        public void GetActionSuccessRate_AllAccepted_ShouldReturnOne()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Accepted });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall section", Outcome = EpisodeOutcome.Accepted });

            // Act
            var rate = _episodicMemory.GetActionSuccessRate("Create wall");

            // Assert
            rate.Should().Be(1.0f);
        }

        [Test]
        public void GetActionSuccessRate_NoneAccepted_ShouldReturnZero()
        {
            // Arrange
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall", Outcome = EpisodeOutcome.Failed });
            _episodicMemory.RecordEpisode(new Episode { Action = "Create wall section", Outcome = EpisodeOutcome.Undone });

            // Act
            var rate = _episodicMemory.GetActionSuccessRate("Create wall");

            // Assert
            rate.Should().Be(0.0f);
        }
    }

    #endregion
}
