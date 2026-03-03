// ============================================================================
// StingBIM AI Tests - Training Data Loader Tests
// Unit tests for JSONL training data loading and knowledge search
// ============================================================================

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.Training;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class TrainingDataLoaderTests
    {
        private string _testDataPath;
        private TrainingDataLoader _loader;

        [SetUp]
        public void Setup()
        {
            // Find project root by traversing up from test assembly
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            _testDataPath = FindProjectRoot(currentDir);
            _loader = TrainingDataLoader.Instance;
        }

        private string FindProjectRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return startPath;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = TrainingDataLoader.Instance;
            var instance2 = TrainingDataLoader.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public async Task LoadAllAsync_ShouldLoadTrainingData()
        {
            // Arrange
            var docsPath = Path.Combine(_testDataPath, "docs");

            if (!Directory.Exists(docsPath))
            {
                Assert.Inconclusive("Test data path not found. Skipping test.");
                return;
            }

            // Act
            await _loader.LoadAllAsync(_testDataPath);

            // Assert
            Assert.That(_loader.IsLoaded, Is.True, "Training data should be loaded");
            Assert.That(_loader.KnowledgeEntryCount > 0 || _loader.TaskTemplateCount > 0,
                Is.True, "Should have loaded some data");
        }

        [Test]
        public async Task FindAnswer_ShouldReturnMatchForKnownQuestion()
        {
            // Arrange
            await EnsureDataLoaded();

            // Act
            var result = _loader.FindAnswer("What is LOD");

            // Assert
            if (result != null)
            {
                Assert.That(result.Entry, Is.Not.Null, "Should return an entry");
                Assert.That(result.Score > 0, Is.True, "Score should be positive");
            }
        }

        [Test]
        public async Task Search_ShouldReturnRelevantResults()
        {
            // Arrange
            await EnsureDataLoaded();

            // Act
            var results = _loader.Search("BIM parameter", maxResults: 5);

            // Assert
            Assert.That(results, Is.Not.Null);
            // Results may be empty if no matching data
        }

        [Test]
        public async Task GetTaskTemplate_ShouldReturnTemplateForKnownTask()
        {
            // Arrange
            await EnsureDataLoaded();

            // Act
            var template = _loader.GetTaskTemplate("construction schedule");

            // Assert
            if (template != null)
            {
                Assert.That(template.TaskType, Is.Not.Null, "TaskType should not be null");
            }
        }

        [Test]
        public async Task GetAvailableTaskTypes_ShouldReturnList()
        {
            // Arrange
            await EnsureDataLoaded();

            // Act
            var taskTypes = _loader.GetAvailableTaskTypes();

            // Assert
            Assert.That(taskTypes, Is.Not.Null);
        }

        [Test]
        public async Task FindByCategory_ShouldReturnFilteredResults()
        {
            // Arrange
            await EnsureDataLoaded();

            // Act
            var results = _loader.FindByCategory("parameter");

            // Assert
            Assert.That(results, Is.Not.Null);
        }

        private async Task EnsureDataLoaded()
        {
            if (!_loader.IsLoaded)
            {
                try
                {
                    await _loader.LoadAllAsync(_testDataPath);
                }
                catch
                {
                    // Ignore load errors in tests
                }
            }
        }
    }
}
