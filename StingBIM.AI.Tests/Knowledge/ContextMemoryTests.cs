using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.Knowledge.Memory;

namespace StingBIM.AI.Tests.Knowledge
{
    /// <summary>
    /// Unit tests for ContextMemory class.
    /// Tests user profiles, project context, session tracking, and learning.
    /// </summary>
    [TestFixture]
    public class ContextMemoryTests
    {
        private ContextMemory _memory;

        [SetUp]
        public void Setup()
        {
            _memory = new ContextMemory();
        }

        #region User Profile Tests

        [Test]
        public void GetUserProfile_NewUser_ShouldCreateDefaultProfile()
        {
            // Act
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.Should().NotBeNull();
            profile.UserId.Should().Be("user-1");
            profile.InteractionCount.Should().Be(0);
            profile.SkillLevels.Should().NotBeEmpty();
            profile.Preferences.Should().NotBeEmpty();
        }

        [Test]
        public void GetUserProfile_ExistingUser_ShouldReturnSameProfile()
        {
            // Arrange
            var profile1 = _memory.GetUserProfile("user-1");
            profile1.InteractionCount = 5;

            // Act
            var profile2 = _memory.GetUserProfile("user-1");

            // Assert
            profile2.InteractionCount.Should().Be(5);
        }

        [Test]
        public void GetUserProfile_DefaultSkillLevels_ShouldBeIntermediate()
        {
            // Act
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.SkillLevels["Revit"].Should().Be(SkillLevel.Intermediate);
            profile.SkillLevels["BIM"].Should().Be(SkillLevel.Intermediate);
        }

        [Test]
        public void GetUserProfile_DefaultPreferences_ShouldHaveAllClusters()
        {
            // Act
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.Preferences.Should().ContainKey("DesignStyle");
            profile.Preferences.Should().ContainKey("Workflow");
            profile.Preferences.Should().ContainKey("Technical");
            profile.Preferences.Should().ContainKey("Communication");
        }

        [Test]
        public void UpdateUserProfile_WithSkillIndicator_ShouldUpdateSkill()
        {
            // Arrange
            var profile = _memory.GetUserProfile("user-1");
            var observation = new BehaviorObservation
            {
                SkillIndicators = new Dictionary<string, double>
                {
                    ["Revit"] = 0.9 // High skill indicator
                }
            };

            // Act - Multiple updates to shift skill level
            for (int i = 0; i < 20; i++)
            {
                _memory.UpdateUserProfile("user-1", observation);
            }

            // Assert - Should have moved toward Advanced/Expert
            profile.SkillLevels["Revit"].Should().BeOneOf(SkillLevel.Advanced, SkillLevel.Expert);
        }

        [Test]
        public void UpdateUserProfile_WithPreferenceSignal_ShouldUpdatePreference()
        {
            // Arrange
            var observation = new BehaviorObservation
            {
                PreferenceSignals = new List<PreferenceSignal>
                {
                    new PreferenceSignal
                    {
                        ClusterId = "Communication",
                        Dimension = "Verbose-Concise",
                        Value = 0.9 // Prefer concise
                    }
                }
            };

            // Act
            for (int i = 0; i < 10; i++)
            {
                _memory.UpdateUserProfile("user-1", observation);
            }
            var profile = _memory.GetUserProfile("user-1");

            // Assert - Should shift toward concise
            profile.Preferences["Communication"]["Verbose-Concise"].Should().BeGreaterThan(0.5);
        }

        [Test]
        public void UpdateUserProfile_WithFeatureUsed_ShouldTrackUsage()
        {
            // Arrange
            var observation = new BehaviorObservation { FeatureUsed = "CreateWall" };

            // Act
            _memory.UpdateUserProfile("user-1", observation);
            _memory.UpdateUserProfile("user-1", observation);
            _memory.UpdateUserProfile("user-1", observation);
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.FeatureUsageCount["CreateWall"].Should().Be(3);
        }

        [Test]
        public void UpdateUserProfile_ShouldIncrementInteractionCount()
        {
            // Arrange
            var observation = new BehaviorObservation();

            // Act
            _memory.UpdateUserProfile("user-1", observation);
            _memory.UpdateUserProfile("user-1", observation);
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.InteractionCount.Should().Be(2);
        }

        [Test]
        public void UpdateUserProfile_ShouldUpdateLastInteraction()
        {
            // Arrange
            var beforeUpdate = DateTime.UtcNow;
            var observation = new BehaviorObservation();

            // Act
            _memory.UpdateUserProfile("user-1", observation);
            var profile = _memory.GetUserProfile("user-1");

            // Assert
            profile.LastInteraction.Should().BeOnOrAfter(beforeUpdate);
        }

        #endregion

        #region Project Context Tests

        [Test]
        public void GetProjectContext_NewProject_ShouldCreateContext()
        {
            // Act
            var context = _memory.GetProjectContext("project-1");

            // Assert
            context.Should().NotBeNull();
            context.ProjectId.Should().Be("project-1");
            context.DesignDecisions.Should().BeEmpty();
            context.ActiveConstraints.Should().BeEmpty();
        }

        [Test]
        public void GetProjectContext_ExistingProject_ShouldReturnSameContext()
        {
            // Arrange
            var context1 = _memory.GetProjectContext("project-1");
            context1.DesignDecisions.Add(new DesignDecision { DecisionId = "d1" });

            // Act
            var context2 = _memory.GetProjectContext("project-1");

            // Assert
            context2.DesignDecisions.Should().HaveCount(1);
        }

        [Test]
        public void RecordDesignDecision_ShouldAddToProject()
        {
            // Arrange
            var decision = new DesignDecision
            {
                DecisionId = "d1",
                Description = "Use open floor plan",
                Topics = new List<string> { "layout", "space planning" }
            };

            // Act
            _memory.RecordDesignDecision("project-1", decision);
            var context = _memory.GetProjectContext("project-1");

            // Assert
            context.DesignDecisions.Should().HaveCount(1);
            context.DesignDecisions[0].Description.Should().Be("Use open floor plan");
            context.DesignDecisions[0].Timestamp.Should().NotBe(default);
        }

        [Test]
        public void RecordDesignDecision_WithConstraints_ShouldAddConstraints()
        {
            // Arrange
            var decision = new DesignDecision
            {
                DecisionId = "d1",
                Description = "Building height limit",
                ImpliedConstraints = new List<DesignConstraint>
                {
                    new DesignConstraint { ConstraintId = "c1", Description = "Max 4 floors" }
                }
            };

            // Act
            _memory.RecordDesignDecision("project-1", decision);
            var context = _memory.GetProjectContext("project-1");

            // Assert
            context.ActiveConstraints.Should().HaveCount(1);
            context.ActiveConstraints[0].Description.Should().Be("Max 4 floors");
        }

        [Test]
        public void GetRelevantDecisions_ShouldFilterByContext()
        {
            // Arrange
            _memory.RecordDesignDecision("project-1", new DesignDecision
            {
                DecisionId = "d1",
                Description = "Wall material selection",
                Topics = new List<string> { "wall", "material" },
                Importance = 0.8
            });
            _memory.RecordDesignDecision("project-1", new DesignDecision
            {
                DecisionId = "d2",
                Description = "HVAC system type",
                Topics = new List<string> { "hvac", "mechanical" },
                Importance = 0.7
            });

            // Act
            var relevant = _memory.GetRelevantDecisions("project-1", "wall construction");

            // Assert
            relevant.Should().HaveCount(1);
            relevant[0].DecisionId.Should().Be("d1");
        }

        [Test]
        public void TrackElement_ShouldAddToRecentElements()
        {
            // Arrange
            var element = new ElementReference
            {
                ElementId = "elem-1",
                ElementType = "Wall",
                Location = new Point3D { X = 0, Y = 0, Z = 0 }
            };

            // Act
            _memory.TrackElement("project-1", element);
            var context = _memory.GetProjectContext("project-1");

            // Assert
            context.RecentElements.Should().HaveCount(1);
            context.RecentElements.Last().ElementType.Should().Be("Wall");
        }

        [Test]
        public void TrackElement_ShouldUpdateSpatialContext()
        {
            // Arrange
            var element = new ElementReference
            {
                ElementId = "elem-1",
                ElementType = "Wall",
                Location = new Point3D { X = 10, Y = 20, Z = 0 }
            };

            // Act
            _memory.TrackElement("project-1", element);
            var context = _memory.GetProjectContext("project-1");

            // Assert
            context.SpatialContext.LastEditLocation.Should().NotBeNull();
            context.SpatialContext.LastEditLocation.X.Should().Be(10);
            context.SpatialContext.LastEditLocation.Y.Should().Be(20);
        }

        [Test]
        public void TrackElement_ShouldLimitRecentElements()
        {
            // Arrange & Act - Add more than limit
            for (int i = 0; i < 25; i++)
            {
                _memory.TrackElement("project-1", new ElementReference
                {
                    ElementId = $"elem-{i}",
                    ElementType = "Wall"
                });
            }
            var context = _memory.GetProjectContext("project-1");

            // Assert - Should be limited to 20
            context.RecentElements.Count.Should().BeLessOrEqualTo(20);
        }

        #endregion

        #region Session Context Tests

        [Test]
        public void RecordInteraction_ShouldAddToSession()
        {
            // Arrange
            var interaction = new InteractionRecord
            {
                Type = InteractionType.Command,
                Content = "Create a wall",
                CommandType = "CreateWall"
            };

            // Act
            _memory.RecordInteraction(interaction);
            var shortTerm = _memory.GetShortTermContext();

            // Assert
            shortTerm.RecentCommands.Should().Contain("Create a wall");
        }

        [Test]
        public void RecordInteraction_ShouldSetTimestamp()
        {
            // Arrange
            var beforeRecord = DateTime.UtcNow;
            var interaction = new InteractionRecord
            {
                Type = InteractionType.Command,
                CommandType = "CreateWall"
            };

            // Act
            _memory.RecordInteraction(interaction);

            // Assert
            interaction.Timestamp.Should().BeOnOrAfter(beforeRecord);
        }

        [Test]
        public void GetShortTermContext_ShouldReturnRecentCommands()
        {
            // Arrange
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                Content = "Command 1",
                CommandType = "Type1"
            });
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                Content = "Command 2",
                CommandType = "Type2"
            });

            // Act
            var context = _memory.GetShortTermContext();

            // Assert
            context.RecentCommands.Should().HaveCount(2);
        }

        [Test]
        public void GetShortTermContext_ShouldReturnMentionedEntities()
        {
            // Arrange
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                MentionedEntities = new List<string> { "wall", "door" }
            });
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                MentionedEntities = new List<string> { "wall", "window" }
            });

            // Act
            var context = _memory.GetShortTermContext();

            // Assert
            context.RecentEntities.Should().Contain("wall");
            context.RecentEntities.Should().Contain("door");
            context.RecentEntities.Should().Contain("window");
        }

        [Test]
        public void GetShortTermContext_ShouldTrackSessionDuration()
        {
            // Act
            System.Threading.Thread.Sleep(10); // Brief delay
            var context = _memory.GetShortTermContext();

            // Assert
            context.SessionDuration.TotalMilliseconds.Should().BeGreaterThan(0);
        }

        [Test]
        public void GetShortTermContext_ShouldTrackInteractionCount()
        {
            // Arrange
            _memory.RecordInteraction(new InteractionRecord { Type = InteractionType.Command });
            _memory.RecordInteraction(new InteractionRecord { Type = InteractionType.Query });
            _memory.RecordInteraction(new InteractionRecord { Type = InteractionType.Selection });

            // Act
            var context = _memory.GetShortTermContext();

            // Assert
            context.InteractionCount.Should().Be(3);
        }

        #endregion

        #region Prediction Tests

        [Test]
        public void PredictNextActions_AfterCreateWall_ShouldSuggestRelatedActions()
        {
            // Arrange
            _memory.GetUserProfile("user-1"); // Initialize profile
            _memory.GetProjectContext("project-1"); // Initialize context
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                CommandType = "CreateWall"
            });

            // Act
            var predictions = _memory.PredictNextActions("user-1", "project-1");

            // Assert
            predictions.Should().NotBeEmpty();
            predictions.Should().Contain(p => p.ActionType == "CreateDoor" || p.ActionType == "CreateWindow");
        }

        [Test]
        public void PredictNextActions_ShouldHaveConfidenceScores()
        {
            // Arrange
            _memory.GetUserProfile("user-1");
            _memory.GetProjectContext("project-1");
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                CommandType = "CreateWall"
            });

            // Act
            var predictions = _memory.PredictNextActions("user-1", "project-1");

            // Assert
            predictions.Should().OnlyContain(p => p.Confidence > 0 && p.Confidence <= 1);
        }

        [Test]
        public void PredictNextActions_ShouldBeOrderedByConfidence()
        {
            // Arrange
            _memory.GetUserProfile("user-1");
            _memory.GetProjectContext("project-1");
            _memory.RecordInteraction(new InteractionRecord
            {
                Type = InteractionType.Command,
                CommandType = "CreateWall"
            });

            // Act
            var predictions = _memory.PredictNextActions("user-1", "project-1");

            // Assert
            predictions.Should().BeInDescendingOrder(p => p.Confidence);
        }

        [Test]
        public void PredictNextActions_ShouldConsiderRecentElements()
        {
            // Arrange
            _memory.GetUserProfile("user-1");
            _memory.TrackElement("project-1", new ElementReference
            {
                ElementId = "room-1",
                ElementType = "Room"
            });

            // Act
            var predictions = _memory.PredictNextActions("user-1", "project-1");

            // Assert
            predictions.Should().Contain(p =>
                p.ActionType == "PlaceFurniture" ||
                p.ActionType == "SetFinishes" ||
                p.ActionType == "CalculateArea");
        }

        #endregion

        #region Learning Tests

        [Test]
        public void LearnFromFeedback_Accepted_ShouldRecordSuccess()
        {
            // Arrange
            var feedback = new FeedbackRecord
            {
                SuggestionType = "MaterialRecommendation",
                Accepted = true
            };

            // Act
            _memory.LearnFromFeedback("user-1", feedback);
            var context = _memory.GetShortTermContext();

            // Assert
            context.InteractionCount.Should().BeGreaterThan(0);
        }

        [Test]
        public void LearnFromFeedback_Rejected_ShouldRecordRejection()
        {
            // Arrange
            var feedback = new FeedbackRecord
            {
                SuggestionType = "LayoutSuggestion",
                Accepted = false
            };

            // Act
            _memory.LearnFromFeedback("user-1", feedback);
            var context = _memory.GetShortTermContext();

            // Assert - Should have recorded the interaction
            context.InteractionCount.Should().BeGreaterThan(0);
        }

        [Test]
        public void GetRecommendations_ShouldReturnPersonalizedRecommendations()
        {
            // Arrange
            _memory.GetUserProfile("user-1");
            _memory.GetProjectContext("project-1");

            // Act
            var recommendations = _memory.GetRecommendations("user-1", "project-1", "wall design");

            // Assert
            recommendations.Should().NotBeNull();
            recommendations.UserId.Should().Be("user-1");
            recommendations.CommunicationStyle.Should().NotBeNull();
            recommendations.WorkflowAdaptations.Should().NotBeNull();
        }

        [Test]
        public void GetRecommendations_ShouldIncludeSkillAppropriateFeatures()
        {
            // Arrange
            var profile = _memory.GetUserProfile("user-1");
            profile.SkillLevels["Revit"] = SkillLevel.Expert;
            _memory.GetProjectContext("project-1");

            // Act
            var recommendations = _memory.GetRecommendations("user-1", "project-1", "");

            // Assert
            recommendations.SuggestedFeatures.Should().NotBeEmpty();
            // Expert should get advanced features
            recommendations.SuggestedFeatures.Should().Contain(f =>
                f.Contains("Dynamo") || f.Contains("API") || f.Contains("Complex"));
        }

        [Test]
        public void GetRecommendations_BeginnerUser_ShouldGetBasicFeatures()
        {
            // Arrange
            var profile = _memory.GetUserProfile("user-1");
            profile.SkillLevels["Revit"] = SkillLevel.Beginner;
            _memory.GetProjectContext("project-1");

            // Act
            var recommendations = _memory.GetRecommendations("user-1", "project-1", "");

            // Assert
            recommendations.SuggestedFeatures.Should().Contain(f =>
                f.Contains("Basic") || f.Contains("Simple"));
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void Constructor_WithConfig_ShouldUseConfig()
        {
            // Arrange
            var config = new MemoryConfiguration
            {
                MaxHistorySize = 5000,
                ShortTermMemorySize = 25,
                EnableLearning = false,
                LearningRate = 0.05
            };

            // Act
            var memory = new ContextMemory(config);

            // Assert - Memory should be created successfully
            memory.Should().NotBeNull();
        }

        [Test]
        public void Constructor_NullConfig_ShouldUseDefaults()
        {
            // Act
            var memory = new ContextMemory(null);

            // Assert
            memory.Should().NotBeNull();
        }

        #endregion
    }
}
