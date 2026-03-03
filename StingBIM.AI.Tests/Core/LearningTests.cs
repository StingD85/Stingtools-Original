// StingBIM.AI.Tests.Core.LearningTests
// Unit tests for FeedbackCollector and PatternLearner
// Tests learning pipeline components from StingBIM.AI.Core.Learning

using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Core.Learning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Tests.Core
{
    [TestFixture]
    public class FeedbackCollectorTests
    {
        private FeedbackCollector _collector;
        private string _tempStoragePath;

        [SetUp]
        public void SetUp()
        {
            _tempStoragePath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "StingBIM_Tests",
                $"feedback_{Guid.NewGuid()}.json");
            _collector = new FeedbackCollector(_tempStoragePath);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (System.IO.File.Exists(_tempStoragePath))
                    System.IO.File.Delete(_tempStoragePath);

                var dir = System.IO.Path.GetDirectoryName(_tempStoragePath);
                if (System.IO.Directory.Exists(dir) &&
                    !System.IO.Directory.EnumerateFileSystemEntries(dir).Any())
                    System.IO.Directory.Delete(dir);
            }
            catch
            {
                // Cleanup is best-effort
            }
        }

        [Test]
        public void RecordAcceptance_CreatesEntryWithAcceptedReaction()
        {
            // Arrange
            var actionId = "action-001";
            var action = "CreateWall";

            // Act
            _collector.RecordAcceptance(actionId, action);

            // Assert
            var pending = _collector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(1);

            var entry = pending.First();
            entry.ActionId.Should().Be(actionId);
            entry.Action.Should().Be(action);
            entry.Reaction.Should().Be(UserReaction.Accepted);
            entry.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
            entry.Id.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void RecordModification_StoresOriginalAndModifiedAction()
        {
            // Arrange
            var actionId = "action-002";
            var originalAction = "PlaceDoor";
            var modifiedAction = "PlaceDoor_900mm";
            var context = new Dictionary<string, object>
            {
                { "Width", 900 },
                { "Room", "Office" }
            };

            // Act
            _collector.RecordModification(actionId, originalAction, modifiedAction, context);

            // Assert
            var pending = _collector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(1);

            var entry = pending.First();
            entry.ActionId.Should().Be(actionId);
            entry.Action.Should().Be(originalAction);
            entry.ModifiedAction.Should().Be(modifiedAction);
            entry.Reaction.Should().Be(UserReaction.Modified);
            entry.Context.Should().ContainKey("Width");
            entry.Context["Width"].Should().Be(900);
        }

        [Test]
        public void RecordUndo_CreatesEntryWithUndoneReaction()
        {
            // Arrange
            var actionId = "action-003";
            var action = "DeleteElement";

            // Act
            _collector.RecordUndo(actionId, action);

            // Assert
            var pending = _collector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(1);

            var entry = pending.First();
            entry.ActionId.Should().Be(actionId);
            entry.Action.Should().Be(action);
            entry.Reaction.Should().Be(UserReaction.Undone);
        }

        [Test]
        public void RecordRating_StoresRatingValue()
        {
            // Arrange
            var actionId = "action-004";
            var action = "GenerateSchedule";
            var rating = 4;
            var comment = "Good but could include more columns";

            // Act
            _collector.RecordRating(actionId, action, rating, comment);

            // Assert
            var pending = _collector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(1);

            var entry = pending.First();
            entry.ActionId.Should().Be(actionId);
            entry.Action.Should().Be(action);
            entry.Reaction.Should().Be(UserReaction.Rated);
            entry.Rating.Should().Be(rating);
            entry.Comment.Should().Be(comment);
        }

        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RecordRating_StoresRatingValues1Through5(int rating)
        {
            // Act
            _collector.RecordRating("action-rating", "SomeAction", rating);

            // Assert
            var entry = _collector.GetPendingFeedback().First();
            entry.Rating.Should().Be(rating);
        }

        [Test]
        public void RecordClarificationRequest_StoresQuestionAsComment()
        {
            // Arrange
            var actionId = "action-005";
            var action = "ApplyMaterial";
            var question = "Which material type should be applied to exterior walls?";

            // Act
            _collector.RecordClarificationRequest(actionId, action, question);

            // Assert
            var pending = _collector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(1);

            var entry = pending.First();
            entry.ActionId.Should().Be(actionId);
            entry.Action.Should().Be(action);
            entry.Reaction.Should().Be(UserReaction.Confused);
            entry.Comment.Should().Be(question);
        }

        [Test]
        public void GetPendingFeedback_ReturnsAllQueuedFeedback()
        {
            // Arrange
            _collector.RecordAcceptance("a1", "Action1");
            _collector.RecordUndo("a2", "Action2");
            _collector.RecordRating("a3", "Action3", 5);
            _collector.RecordModification("a4", "Action4", "Action4_Modified");
            _collector.RecordClarificationRequest("a5", "Action5", "Why?");

            // Act
            var pending = _collector.GetPendingFeedback().ToList();

            // Assert
            pending.Should().HaveCount(5);
            pending.Select(p => p.Reaction).Should().Contain(UserReaction.Accepted);
            pending.Select(p => p.Reaction).Should().Contain(UserReaction.Undone);
            pending.Select(p => p.Reaction).Should().Contain(UserReaction.Rated);
            pending.Select(p => p.Reaction).Should().Contain(UserReaction.Modified);
            pending.Select(p => p.Reaction).Should().Contain(UserReaction.Confused);
        }

        [Test]
        public void FeedbackReceived_EventFiresOnEachRecord()
        {
            // Arrange
            var receivedEntries = new List<FeedbackEntry>();
            _collector.FeedbackReceived += (sender, entry) => receivedEntries.Add(entry);

            // Act
            _collector.RecordAcceptance("a1", "Action1");
            _collector.RecordUndo("a2", "Action2");
            _collector.RecordRating("a3", "Action3", 3);

            // Assert
            receivedEntries.Should().HaveCount(3);
            receivedEntries[0].Reaction.Should().Be(UserReaction.Accepted);
            receivedEntries[1].Reaction.Should().Be(UserReaction.Undone);
            receivedEntries[2].Reaction.Should().Be(UserReaction.Rated);
        }

        [Test]
        public void FeedbackReceived_EventProvidesSenderReference()
        {
            // Arrange
            object capturedSender = null;
            _collector.FeedbackReceived += (sender, entry) => capturedSender = sender;

            // Act
            _collector.RecordAcceptance("a1", "Action1");

            // Assert
            capturedSender.Should().BeSameAs(_collector);
        }

        [Test]
        public void Constructor_SetsBatchSize()
        {
            // Arrange & Act
            var customBatchCollector = new FeedbackCollector(_tempStoragePath, batchSize: 50);

            // Record 49 entries - should not trigger auto-flush (all remain pending)
            for (int i = 0; i < 49; i++)
            {
                customBatchCollector.RecordAcceptance($"action-{i}", $"Action{i}");
            }

            // Assert - all 49 should still be in pending queue (below batch size of 50)
            var pending = customBatchCollector.GetPendingFeedback().ToList();
            pending.Should().HaveCount(49);
        }

        [Test]
        public void RecordAcceptance_WithNullContext_DefaultsToEmptyDictionary()
        {
            // Act
            _collector.RecordAcceptance("a1", "Action1", context: null);

            // Assert
            var entry = _collector.GetPendingFeedback().First();
            entry.Context.Should().NotBeNull();
            entry.Context.Should().BeEmpty();
        }

        [Test]
        public void RecordAcceptance_WithContext_StoresContext()
        {
            // Arrange
            var context = new Dictionary<string, object>
            {
                { "ProjectPhase", "Schematic Design" },
                { "Floor", 2 }
            };

            // Act
            _collector.RecordAcceptance("a1", "CreateWall", context);

            // Assert
            var entry = _collector.GetPendingFeedback().First();
            entry.Context.Should().ContainKey("ProjectPhase");
            entry.Context["ProjectPhase"].Should().Be("Schematic Design");
        }

        [Test]
        public void GetPendingFeedback_WhenEmpty_ReturnsEmptyCollection()
        {
            // Act
            var pending = _collector.GetPendingFeedback().ToList();

            // Assert
            pending.Should().BeEmpty();
        }

        [Test]
        public void EachFeedbackEntry_HasUniqueId()
        {
            // Act
            _collector.RecordAcceptance("a1", "Action1");
            _collector.RecordAcceptance("a2", "Action2");
            _collector.RecordAcceptance("a3", "Action3");

            // Assert
            var ids = _collector.GetPendingFeedback().Select(f => f.Id).ToList();
            ids.Should().OnlyHaveUniqueItems();
        }
    }

    [TestFixture]
    public class PatternLearnerTests
    {
        private PatternLearner _learner;

        [SetUp]
        public void SetUp()
        {
            _learner = new PatternLearner();
        }

        private ProjectSession CreateTestSession()
        {
            return new ProjectSession
            {
                SessionId = "test-session",
                UserId = "user1",
                Actions = new List<UserAction>
                {
                    new UserAction
                    {
                        ActionId = "1",
                        ActionType = "CreateWall",
                        Timestamp = DateTime.Now,
                        Parameters = new Dictionary<string, object> { { "Height", 3000 } }
                    },
                    new UserAction
                    {
                        ActionId = "2",
                        ActionType = "PlaceDoor",
                        Timestamp = DateTime.Now.AddSeconds(5),
                        Parameters = new Dictionary<string, object> { { "Width", 900 } }
                    },
                    new UserAction
                    {
                        ActionId = "3",
                        ActionType = "CreateWall",
                        Timestamp = DateTime.Now.AddSeconds(10),
                        Parameters = new Dictionary<string, object> { { "Height", 3000 } }
                    },
                }
            };
        }

        [Test]
        public void AnalyzeSession_ExtractsSequentialPatterns()
        {
            // Arrange
            var session = CreateTestSession();

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert
            var sequentialPatterns = patterns
                .Where(p => p.PatternType == PatternType.Sequential)
                .ToList();

            sequentialPatterns.Should().NotBeEmpty();

            // CreateWall -> PlaceDoor should be detected
            sequentialPatterns.Should().Contain(p =>
                p.Key.Contains("CreateWall") &&
                p.Key.Contains("PlaceDoor") &&
                p.Key.StartsWith("SEQ:"));

            // PlaceDoor -> CreateWall should be detected
            sequentialPatterns.Should().Contain(p =>
                p.Context.GetValueOrDefault("TriggerAction")?.ToString() == "PlaceDoor" &&
                p.Context.GetValueOrDefault("FollowingAction")?.ToString() == "CreateWall");
        }

        [Test]
        public void AnalyzeSession_ExtractsPreferencePatterns()
        {
            // Arrange - need at least 2 occurrences of same param value for same action type
            var session = CreateTestSession();
            // The session has 2 CreateWall actions with Height=3000

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert
            var preferencePatterns = patterns
                .Where(p => p.PatternType == PatternType.Preference)
                .ToList();

            preferencePatterns.Should().NotBeEmpty();

            // Should detect Height=3000 preference for CreateWall
            preferencePatterns.Should().Contain(p =>
                p.Key.Contains("CreateWall") &&
                p.Key.Contains("Height") &&
                p.Key.Contains("3000"));
        }

        [Test]
        public void AnalyzeSession_ExtractsCorrectionPatterns()
        {
            // Arrange - session with an Undo action
            var session = new ProjectSession
            {
                SessionId = "correction-session",
                UserId = "user1",
                Actions = new List<UserAction>
                {
                    new UserAction
                    {
                        ActionId = "1",
                        ActionType = "PlaceWindow",
                        Timestamp = DateTime.Now,
                        Parameters = new Dictionary<string, object> { { "Width", 1200 } }
                    },
                    new UserAction
                    {
                        ActionId = "2",
                        ActionType = "UndoAction",
                        Timestamp = DateTime.Now.AddSeconds(3),
                        Parameters = new Dictionary<string, object>()
                    }
                }
            };

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert
            var correctionPatterns = patterns
                .Where(p => p.PatternType == PatternType.Correction)
                .ToList();

            correctionPatterns.Should().NotBeEmpty();
            correctionPatterns.Should().Contain(p =>
                p.Key.Contains("PlaceWindow") &&
                p.Context.GetValueOrDefault("ProblematicAction")?.ToString() == "PlaceWindow");
        }

        [Test]
        public void GetPatternsForAction_ReturnsMatchingPatterns_CaseInsensitive()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act - search with different casing
            var patterns = _learner.GetPatternsForAction("createwall").ToList();

            // Assert
            patterns.Should().NotBeEmpty();
            patterns.Should().OnlyContain(p =>
                p.Key.Contains("CreateWall", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetPatternsForAction_ReturnsOrderedByConfidence()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act
            var patterns = _learner.GetPatternsForAction("CreateWall").ToList();

            // Assert
            patterns.Should().NotBeEmpty();
            patterns.Should().BeInDescendingOrder(p => p.Confidence);
        }

        [Test]
        public void GetAllPatterns_ReturnsAllStoredPatterns()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act
            var allPatterns = _learner.GetAllPatterns().ToList();

            // Assert
            allPatterns.Should().NotBeEmpty();

            // Should contain both sequential and preference pattern types
            allPatterns.Select(p => p.PatternType).Should().Contain(PatternType.Sequential);
            allPatterns.Select(p => p.PatternType).Should().Contain(PatternType.Preference);
        }

        [Test]
        public void GetAllPatterns_WhenNoSessionsAnalyzed_ReturnsEmpty()
        {
            // Act
            var allPatterns = _learner.GetAllPatterns().ToList();

            // Assert
            allPatterns.Should().BeEmpty();
        }

        [Test]
        public void PredictNextAction_ReturnsMostLikelyNextAction()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act - CreateWall is followed by PlaceDoor in the session
            var prediction = _learner.PredictNextAction("CreateWall");

            // Assert
            prediction.Should().NotBeNull();
            prediction.Should().Be("PlaceDoor");
        }

        [Test]
        public void PredictNextAction_ReturnsNullWhenNoPatternsMatch()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act - "PlaceRoof" never appears in any session
            var prediction = _learner.PredictNextAction("PlaceRoof");

            // Assert
            prediction.Should().BeNull();
        }

        [Test]
        public void PredictNextAction_ReturnsNullWhenNoSessionsAnalyzed()
        {
            // Act
            var prediction = _learner.PredictNextAction("CreateWall");

            // Assert
            prediction.Should().BeNull();
        }

        [Test]
        public void RepeatedAnalyzeSession_IncreasesPatternConfidence()
        {
            // Arrange
            var session = CreateTestSession();

            // Act - analyze same session pattern multiple times
            _learner.AnalyzeSession(session);
            var patternsAfterFirst = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .ToList();
            var firstConfidence = patternsAfterFirst.First().Confidence;

            _learner.AnalyzeSession(session);
            var patternsAfterSecond = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .ToList();
            var secondConfidence = patternsAfterSecond.First().Confidence;

            // Assert
            secondConfidence.Should().BeGreaterThan(firstConfidence);
        }

        [Test]
        public void RepeatedAnalyzeSession_ConfidenceDoesNotExceedMaximum()
        {
            // Arrange
            var session = CreateTestSession();

            // Act - analyze many times to push confidence toward maximum
            for (int i = 0; i < 20; i++)
            {
                _learner.AnalyzeSession(session);
            }

            // Assert
            var allPatterns = _learner.GetAllPatterns().ToList();
            allPatterns.Should().OnlyContain(p => p.Confidence <= 0.95f);
        }

        [Test]
        public void Patterns_StoreFirstSeenAndLastSeenTimestamps()
        {
            // Arrange
            var session = CreateTestSession();

            // Act
            _learner.AnalyzeSession(session);

            // Assert
            var patterns = _learner.GetAllPatterns().ToList();
            patterns.Should().NotBeEmpty();

            foreach (var pattern in patterns)
            {
                pattern.FirstSeen.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
                pattern.LastSeen.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
                pattern.LastSeen.Should().BeOnOrAfter(pattern.FirstSeen);
            }
        }

        [Test]
        public void RepeatedAnalyzeSession_UpdatesLastSeenTimestamp()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            var patternAfterFirst = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .First();
            var firstSeenOriginal = patternAfterFirst.FirstSeen;
            var lastSeenOriginal = patternAfterFirst.LastSeen;

            // Act - analyze again
            System.Threading.Thread.Sleep(50); // Small delay to ensure timestamp difference
            _learner.AnalyzeSession(session);

            // Assert
            var patternAfterSecond = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .First();

            patternAfterSecond.FirstSeen.Should().Be(firstSeenOriginal);
            patternAfterSecond.LastSeen.Should().BeOnOrAfter(lastSeenOriginal);
        }

        [Test]
        public void RepeatedAnalyzeSession_IncreasesOccurrenceCount()
        {
            // Arrange
            var session = CreateTestSession();

            // Act
            _learner.AnalyzeSession(session);
            var firstOccurrences = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .First().Occurrences;

            _learner.AnalyzeSession(session);
            var secondOccurrences = _learner.GetAllPatterns()
                .Where(p => p.PatternType == PatternType.Sequential)
                .First().Occurrences;

            // Assert
            secondOccurrences.Should().BeGreaterThan(firstOccurrences);
        }

        [Test]
        public void SequentialPattern_StoresTimeDelta()
        {
            // Arrange
            var session = CreateTestSession();

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert
            var sequentialPattern = patterns
                .First(p => p.PatternType == PatternType.Sequential);

            sequentialPattern.Context.Should().ContainKey("TimeDelta");
            var timeDelta = Convert.ToDouble(sequentialPattern.Context["TimeDelta"]);
            timeDelta.Should().BeGreaterOrEqualTo(0);
        }

        [Test]
        public void PreferencePattern_RequiresAtLeastTwoOccurrences()
        {
            // Arrange - session with only one occurrence of each action type
            var session = new ProjectSession
            {
                SessionId = "single-session",
                UserId = "user1",
                Actions = new List<UserAction>
                {
                    new UserAction
                    {
                        ActionId = "1",
                        ActionType = "CreateWall",
                        Timestamp = DateTime.Now,
                        Parameters = new Dictionary<string, object> { { "Height", 3000 } }
                    },
                    new UserAction
                    {
                        ActionId = "2",
                        ActionType = "PlaceDoor",
                        Timestamp = DateTime.Now.AddSeconds(5),
                        Parameters = new Dictionary<string, object> { { "Width", 900 } }
                    }
                }
            };

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert - no preference patterns since each action type only appears once
            var preferencePatterns = patterns
                .Where(p => p.PatternType == PatternType.Preference)
                .ToList();

            preferencePatterns.Should().BeEmpty();
        }

        [Test]
        public void CorrectionPattern_DetectsUndoInActionType()
        {
            // Arrange - "Undo" substring in ActionType triggers correction detection
            var session = new ProjectSession
            {
                SessionId = "undo-session",
                UserId = "user1",
                Actions = new List<UserAction>
                {
                    new UserAction
                    {
                        ActionId = "1",
                        ActionType = "CreateFloor",
                        Timestamp = DateTime.Now,
                        Parameters = new Dictionary<string, object> { { "Thickness", 200 } }
                    },
                    new UserAction
                    {
                        ActionId = "2",
                        ActionType = "Undo",
                        Timestamp = DateTime.Now.AddSeconds(2),
                        Parameters = new Dictionary<string, object>()
                    },
                    new UserAction
                    {
                        ActionId = "3",
                        ActionType = "CreateFloor",
                        Timestamp = DateTime.Now.AddSeconds(4),
                        Parameters = new Dictionary<string, object> { { "Thickness", 250 } }
                    }
                }
            };

            // Act
            var patterns = _learner.AnalyzeSession(session).ToList();

            // Assert
            var correctionPatterns = patterns
                .Where(p => p.PatternType == PatternType.Correction)
                .ToList();

            correctionPatterns.Should().NotBeEmpty();
            correctionPatterns.Should().Contain(p =>
                p.Context.GetValueOrDefault("ProblematicAction")?.ToString() == "CreateFloor");
        }

        [Test]
        public void GetPatternsForAction_ReturnsEmptyForUnknownAction()
        {
            // Arrange
            var session = CreateTestSession();
            _learner.AnalyzeSession(session);

            // Act
            var patterns = _learner.GetPatternsForAction("NonExistentAction").ToList();

            // Assert
            patterns.Should().BeEmpty();
        }

        [Test]
        public void PredictNextAction_ChoosesHighestConfidenceTimesOccurrences()
        {
            // Arrange - create two different sessions where CreateWall is followed by different actions
            // PlaceDoor appears more often, so it should win
            var session1 = CreateTestSession(); // CreateWall -> PlaceDoor
            _learner.AnalyzeSession(session1);
            _learner.AnalyzeSession(session1); // Reinforce CreateWall -> PlaceDoor

            var session2 = new ProjectSession
            {
                SessionId = "alt-session",
                UserId = "user1",
                Actions = new List<UserAction>
                {
                    new UserAction
                    {
                        ActionId = "1",
                        ActionType = "CreateWall",
                        Timestamp = DateTime.Now,
                        Parameters = new Dictionary<string, object> { { "Height", 3000 } }
                    },
                    new UserAction
                    {
                        ActionId = "2",
                        ActionType = "PlaceWindow",
                        Timestamp = DateTime.Now.AddSeconds(5),
                        Parameters = new Dictionary<string, object> { { "Width", 1200 } }
                    }
                }
            };
            _learner.AnalyzeSession(session2);

            // Act
            var prediction = _learner.PredictNextAction("CreateWall");

            // Assert - PlaceDoor should win due to higher confidence * occurrences
            prediction.Should().Be("PlaceDoor");
        }
    }
}
