// ============================================================================
// StingBIM AI Tests - Executive Task Engine Tests
// Unit tests for BEP generation, schedules, cost tracking, and recommendations
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.Executive;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class ExecutiveTaskEngineTests
    {
        private ExecutiveTaskEngine _engine;

        [SetUp]
        public void Setup()
        {
            _engine = ExecutiveTaskEngine.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = ExecutiveTaskEngine.Instance;
            var instance2 = ExecutiveTaskEngine.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public void IsTaskRequest_ShouldIdentifyTaskRequests()
        {
            // Arrange & Act & Assert
            Assert.That(_engine.IsTaskRequest("Generate a BIM Execution Plan"), Is.True);
            Assert.That(_engine.IsTaskRequest("Create a construction schedule"), Is.True);
            Assert.That(_engine.IsTaskRequest("Track project costs"), Is.True);
            Assert.That(_engine.IsTaskRequest("Parse this project brief"), Is.True);
            Assert.That(_engine.IsTaskRequest("Provide recommendations"), Is.True);
        }

        [Test]
        public void IsTaskRequest_ShouldNotMatchQuestions()
        {
            // Arrange & Act & Assert
            Assert.That(_engine.IsTaskRequest("What is LOD 350?"), Is.False);
            Assert.That(_engine.IsTaskRequest("How does this work?"), Is.False);
        }

        [Test]
        public void GetAvailableTasks_ShouldReturnTaskList()
        {
            // Arrange & Act
            var tasks = _engine.GetAvailableTasks();

            // Assert
            Assert.That(tasks, Is.Not.Null);
            Assert.That(tasks.Count > 0, Is.True);
            Assert.That(tasks.Exists(t => t.Contains("BIM Execution Plan")), Is.True);
            Assert.That(tasks.Exists(t => t.Contains("Construction Schedule")), Is.True);
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldGenerateBEP()
        {
            // Arrange
            var taskDescription = "Generate a BIM Execution Plan for Downtown Office Tower";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("BIM Execution Plan Generation"));
            Assert.That(result.Output, Is.Not.Null);
            Assert.That(result.Summary, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldCreateConstructionSchedule()
        {
            // Arrange
            var taskDescription = "Create a construction schedule for an 18 month project";
            var parameters = new Dictionary<string, object>
            {
                { "project_name", "Test Project" },
                { "duration", 18 }
            };

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription, parameters);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Construction Schedule Creation"));
            Assert.That(result.Output, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldCreateMaintenanceSchedule()
        {
            // Arrange
            var taskDescription = "Create a maintenance schedule";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Maintenance Schedule Creation"));
            Assert.That(result.Output, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldTrackCosts()
        {
            // Arrange
            var taskDescription = "Track costs for a $15M budget project";
            var parameters = new Dictionary<string, object>
            {
                { "budget", 15000000 }
            };

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription, parameters);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("5D Cost Tracking Setup"));
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldTrackProgress()
        {
            // Arrange
            var taskDescription = "Track project progress";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Progress Tracking"));
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldGenerateCoordinationReport()
        {
            // Arrange
            var taskDescription = "Generate coordination report";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Coordination Report"));
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldGenerateCostReport()
        {
            // Arrange
            var taskDescription = "Generate a cost report";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Cost Report"));
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldProvideRecommendations()
        {
            // Arrange
            var taskDescription = "Provide project recommendations";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Project Recommendations"));
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldParseProjectBrief()
        {
            // Arrange
            var taskDescription = "Parse project brief: 50,000 sqft office building, 5 floors, $15M budget, 18 months duration, LEED certified";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True, $"Task should succeed: {result.ErrorMessage}");
            Assert.That(result.TaskType, Is.EqualTo("Project Brief Parsing"));
            Assert.That(result.Output, Is.Not.Null);

            // Verify parameter extraction
            var briefResult = result.Output as ProjectBriefResult;
            if (briefResult != null)
            {
                Assert.That(briefResult.SpecialRequirements.Contains("LEED/Green Building Certification"),
                    Is.True, "Should detect LEED requirement");
            }
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldReturnErrorForUnknownTask()
        {
            // Arrange
            var taskDescription = "Do something completely unknown";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldExtractParametersFromDescription()
        {
            // Arrange
            var taskDescription = "Parse brief: 100,000 sq ft hospital, 10 floors, $50M budget";

            // Act
            var result = await _engine.ExecuteTaskAsync(taskDescription);

            // Assert
            Assert.That(result.Success, Is.True);

            var briefResult = result.Output as ProjectBriefResult;
            if (briefResult != null)
            {
                Assert.That(briefResult.ProjectType, Is.EqualTo("Hospital"));
                Assert.That(briefResult.Floors, Is.EqualTo(10));
            }
        }

        [Test]
        public async Task ExecuteTaskAsync_ShouldFireProgressEvents()
        {
            // Arrange
            var progressMessages = new List<string>();
            _engine.TaskProgress += (s, e) => progressMessages.Add(e.Message);

            // Act
            await _engine.ExecuteTaskAsync("Generate a BIM Execution Plan");

            // Assert
            Assert.That(progressMessages.Count > 0, Is.True, "Should have received progress events");
        }
    }
}
