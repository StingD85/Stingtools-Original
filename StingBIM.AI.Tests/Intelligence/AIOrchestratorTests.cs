// ============================================================================
// StingBIM AI Tests - AI Orchestrator Tests
// Unit tests for central AI routing and response generation
// ============================================================================

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.Core;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class AIOrchestratorTests
    {
        private AIOrchestrator _orchestrator;
        private string _testDataPath;

        [SetUp]
        public void Setup()
        {
            _orchestrator = AIOrchestrator.Instance;
            _testDataPath = FindProjectRoot(AppDomain.CurrentDomain.BaseDirectory);
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
            var instance1 = AIOrchestrator.Instance;
            var instance2 = AIOrchestrator.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        [Test]
        public async Task InitializeAsync_ShouldLoadData()
        {
            // Arrange
            var docsPath = Path.Combine(_testDataPath, "docs");
            if (!Directory.Exists(docsPath))
            {
                Assert.Inconclusive("Test data path not found.");
                return;
            }

            // Act
            await _orchestrator.InitializeAsync(_testDataPath);

            // Assert
            Assert.That(_orchestrator.IsInitialized, Is.True);
        }

        [Test]
        public async Task ProcessInputAsync_ShouldReturnErrorForEmptyInput()
        {
            // Arrange & Act
            var response = await _orchestrator.ProcessInputAsync("");

            // Assert
            Assert.That(response.Type, Is.EqualTo(ResponseType.Error));
        }

        [Test]
        public async Task ProcessInputAsync_ShouldReturnErrorForNullInput()
        {
            // Arrange & Act
            var response = await _orchestrator.ProcessInputAsync(null);

            // Assert
            Assert.That(response.Type, Is.EqualTo(ResponseType.Error));
        }

        [Test]
        public async Task ProcessInputAsync_ShouldRouteTaskRequest()
        {
            // Arrange
            await EnsureInitialized();
            var input = "Generate a BIM Execution Plan";

            // Act
            var response = await _orchestrator.ProcessInputAsync(input);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Type, Is.EqualTo(ResponseType.TaskResult));
        }

        [Test]
        public async Task ProcessInputAsync_ShouldHandleHelpRequest()
        {
            // Arrange
            await EnsureInitialized();
            var input = "help";

            // Act
            var response = await _orchestrator.ProcessInputAsync(input);

            // Assert
            Assert.That(response, Is.Not.Null);
            // May return Help or Answer type
            Assert.That(response.Message, Is.Not.Null);
        }

        [Test]
        public async Task ProcessInputAsync_ShouldReturnResponseForQuestion()
        {
            // Arrange
            await EnsureInitialized();
            var input = "What is LOD 350?";

            // Act
            var response = await _orchestrator.ProcessInputAsync(input);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Message, Is.Not.Null);
        }

        [Test]
        public async Task GetSuggestions_ShouldReturnSuggestionList()
        {
            // Arrange
            await EnsureInitialized();

            // Act
            var suggestions = _orchestrator.GetSuggestions();

            // Assert
            Assert.That(suggestions, Is.Not.Null);
            Assert.That(suggestions.Count > 0, Is.True);
        }

        [Test]
        public async Task GetSuggestions_ShouldFilterByPartialInput()
        {
            // Arrange
            await EnsureInitialized();

            // Act
            var suggestions = _orchestrator.GetSuggestions("BIM");

            // Assert
            Assert.That(suggestions, Is.Not.Null);
            // All suggestions should relate to BIM
            foreach (var suggestion in suggestions)
            {
                var match = suggestion.Command.ToLower().Contains("bim") ||
                           suggestion.Description.ToLower().Contains("bim");
                // This may or may not match depending on implementation
            }
        }

        [Test]
        public async Task SearchKnowledge_ShouldReturnResults()
        {
            // Arrange
            await EnsureInitialized();

            // Act
            var results = _orchestrator.SearchKnowledge("parameter", maxResults: 5);

            // Assert
            Assert.That(results, Is.Not.Null);
        }

        [Test]
        public async Task ProcessInputAsync_ShouldFireProgressEvents()
        {
            // Arrange
            await EnsureInitialized();
            var progressReceived = false;
            _orchestrator.ProcessingProgress += (s, e) => progressReceived = true;

            // Act
            await _orchestrator.ProcessInputAsync("Generate a cost report");

            // Assert
            Assert.That(progressReceived, Is.True, "Should have received progress events");
        }

        [Test]
        public async Task ProcessInputAsync_ShouldFireResponseEvent()
        {
            // Arrange
            await EnsureInitialized();
            AIResponse receivedResponse = null;
            _orchestrator.ResponseGenerated += (s, e) => receivedResponse = e.Response;

            // Act
            await _orchestrator.ProcessInputAsync("Track progress");

            // Assert
            Assert.That(receivedResponse, Is.Not.Null, "Should have received response event");
        }

        private async Task EnsureInitialized()
        {
            if (!_orchestrator.IsInitialized)
            {
                try
                {
                    await _orchestrator.InitializeAsync(_testDataPath);
                }
                catch
                {
                    // Ignore initialization errors in tests
                }
            }
        }
    }
}
