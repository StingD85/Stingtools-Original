// ============================================================================
// StingBIM AI Tests - Intent Classifier Tests
// Unit tests for NLP intent classification
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace StingBIM.AI.Tests.NLP
{
    [TestFixture]
    public class IntentClassifierTests
    {
        private TestableIntentClassifier _classifier;

        [SetUp]
        public void SetUp()
        {
            _classifier = new TestableIntentClassifier();
            _classifier.LoadIntents(GetTestIntents());
        }

        #region Intent Classification Tests

        [Test]
        [TestCase("create a wall", "CreateWall")]
        [TestCase("add an exterior wall", "CreateWall")]
        [TestCase("place a 200mm concrete wall", "CreateWall")]
        [TestCase("draw a wall along the grid", "CreateWall")]
        public void Classify_WallCreationPhrases_ShouldReturnCreateWall(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
            result.Confidence.Should().BeGreaterThan(0.5f);
        }

        [Test]
        [TestCase("add a door to this wall", "CreateDoor")]
        [TestCase("place a double door", "CreateDoor")]
        [TestCase("insert fire door", "CreateDoor")]
        public void Classify_DoorCreationPhrases_ShouldReturnCreateDoor(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
        }

        [Test]
        [TestCase("delete this wall", "DeleteElement")]
        [TestCase("remove the door", "DeleteElement")]
        [TestCase("delete selected elements", "DeleteElement")]
        public void Classify_DeletionPhrases_ShouldReturnDeleteElement(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
        }

        [Test]
        [TestCase("check fire code compliance", "CheckCompliance")]
        [TestCase("verify ADA requirements", "CheckCompliance")]
        [TestCase("run compliance check", "CheckCompliance")]
        public void Classify_CompliancePhrases_ShouldReturnCheckCompliance(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
        }

        [Test]
        public void Classify_UnknownPhrase_ShouldReturnLowConfidence()
        {
            // Act
            var result = _classifier.Classify("something completely unrelated xyz123");

            // Assert
            result.Confidence.Should().BeLessThan(0.5f);
        }

        [Test]
        public void Classify_EmptyInput_ShouldReturnNull()
        {
            // Act
            var result = _classifier.Classify("");

            // Assert
            result.Intent.Should().BeNull();
        }

        #endregion

        #region Confidence Score Tests

        [Test]
        public void Classify_ExactMatch_ShouldHaveHighConfidence()
        {
            // Arrange - Use exact example phrase
            var input = "create a wall from here to there";

            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Confidence.Should().BeGreaterThan(0.8f);
        }

        [Test]
        public void Classify_PartialMatch_ShouldHaveMediumConfidence()
        {
            // Arrange
            var input = "I want to create some kind of wall";

            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Confidence.Should().BeInRange(0.5f, 0.8f);
        }

        [Test]
        public void Classify_MultipleIntentsPossible_ShouldReturnBestMatch()
        {
            // Arrange
            var input = "create a door for the wall"; // Could match CreateDoor or CreateWall

            // Act
            var result = _classifier.Classify(input);

            // Assert
            // Should match CreateDoor as "door" is more specific
            result.Intent.Should().Be("CreateDoor");
        }

        #endregion

        #region Entity Extraction Tests

        [Test]
        public void ExtractEntities_WithDimensions_ShouldExtractDimension()
        {
            // Arrange
            var input = "create a 200mm concrete wall";

            // Act
            var result = _classifier.ClassifyWithEntities(input);

            // Assert
            result.Entities.Should().ContainKey("Dimension");
            result.Entities["Dimension"].Should().Be("200mm");
        }

        [Test]
        public void ExtractEntities_WithMaterial_ShouldExtractMaterial()
        {
            // Arrange
            var input = "add a concrete wall";

            // Act
            var result = _classifier.ClassifyWithEntities(input);

            // Assert
            result.Entities.Should().ContainKey("Material");
            result.Entities["Material"].Should().Be("concrete");
        }

        [Test]
        public void ExtractEntities_MultipleEntities_ShouldExtractAll()
        {
            // Arrange
            var input = "create a 150mm steel beam on level 2";

            // Act
            var result = _classifier.ClassifyWithEntities(input);

            // Assert
            result.Entities.Should().HaveCountGreaterOrEqualTo(2);
        }

        #endregion

        #region Synonym Handling Tests

        [Test]
        [TestCase("add a wall", "CreateWall")]
        [TestCase("place a wall", "CreateWall")]
        [TestCase("insert a wall", "CreateWall")]
        [TestCase("make a wall", "CreateWall")]
        [TestCase("build a wall", "CreateWall")]
        public void Classify_CreateSynonyms_ShouldAllMatchCreateIntent(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
        }

        [Test]
        [TestCase("remove the wall", "DeleteElement")]
        [TestCase("erase the wall", "DeleteElement")]
        [TestCase("clear the wall", "DeleteElement")]
        public void Classify_DeleteSynonyms_ShouldAllMatchDeleteIntent(string input, string expectedIntent)
        {
            // Act
            var result = _classifier.Classify(input);

            // Assert
            result.Intent.Should().Be(expectedIntent);
        }

        #endregion

        #region Helper Methods and Classes

        private List<IntentDefinition> GetTestIntents()
        {
            return new List<IntentDefinition>
            {
                new IntentDefinition
                {
                    Name = "CreateWall",
                    Examples = new List<string>
                    {
                        "create a wall from here to there",
                        "add an exterior wall",
                        "place a 200mm concrete wall",
                        "draw a wall along the grid"
                    },
                    Keywords = new List<string> { "wall", "partition", "barrier" }
                },
                new IntentDefinition
                {
                    Name = "CreateDoor",
                    Examples = new List<string>
                    {
                        "add a door to this wall",
                        "place a double door",
                        "insert fire door"
                    },
                    Keywords = new List<string> { "door", "entrance", "entry" }
                },
                new IntentDefinition
                {
                    Name = "DeleteElement",
                    Examples = new List<string>
                    {
                        "delete this wall",
                        "remove the door",
                        "delete selected elements"
                    },
                    Keywords = new List<string> { "delete", "remove", "erase", "clear" }
                },
                new IntentDefinition
                {
                    Name = "CheckCompliance",
                    Examples = new List<string>
                    {
                        "check fire code compliance",
                        "verify ADA requirements",
                        "run compliance check"
                    },
                    Keywords = new List<string> { "check", "verify", "compliance", "code" }
                }
            };
        }

        /// <summary>
        /// Testable intent classifier for unit tests.
        /// </summary>
        private class TestableIntentClassifier
        {
            private readonly List<IntentDefinition> _intents = new List<IntentDefinition>();
            private readonly Dictionary<string, List<string>> _synonyms;

            public TestableIntentClassifier()
            {
                _synonyms = new Dictionary<string, List<string>>
                {
                    ["create"] = new List<string> { "add", "place", "insert", "make", "draw", "build" },
                    ["delete"] = new List<string> { "remove", "erase", "clear" }
                };
            }

            public void LoadIntents(List<IntentDefinition> intents)
            {
                _intents.Clear();
                _intents.AddRange(intents);
            }

            public ClassificationResult Classify(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return new ClassificationResult { Intent = null, Confidence = 0 };
                }

                var normalizedInput = NormalizeInput(input);
                var bestMatch = (Intent: (string)null, Score: 0f);

                foreach (var intent in _intents)
                {
                    var score = CalculateMatchScore(normalizedInput, intent);
                    if (score > bestMatch.Score)
                    {
                        bestMatch = (intent.Name, score);
                    }
                }

                return new ClassificationResult
                {
                    Intent = bestMatch.Score > 0.3f ? bestMatch.Intent : null,
                    Confidence = bestMatch.Score
                };
            }

            public ClassificationResultWithEntities ClassifyWithEntities(string input)
            {
                var result = Classify(input);
                var entities = ExtractEntities(input);

                return new ClassificationResultWithEntities
                {
                    Intent = result.Intent,
                    Confidence = result.Confidence,
                    Entities = entities
                };
            }

            private string NormalizeInput(string input)
            {
                var normalized = input.ToLowerInvariant();

                // Replace synonyms
                foreach (var kvp in _synonyms)
                {
                    foreach (var synonym in kvp.Value)
                    {
                        normalized = normalized.Replace(synonym, kvp.Key);
                    }
                }

                return normalized;
            }

            private float CalculateMatchScore(string input, IntentDefinition intent)
            {
                float score = 0;

                // Exact example match
                if (intent.Examples.Any(e => input.Contains(e.ToLowerInvariant())))
                {
                    score = 0.9f;
                }
                // Keyword matching
                else
                {
                    var keywordMatches = intent.Keywords.Count(k => input.Contains(k.ToLowerInvariant()));
                    score = keywordMatches > 0 ? 0.5f + (keywordMatches * 0.1f) : 0;
                }

                return Math.Min(score, 1.0f);
            }

            private Dictionary<string, string> ExtractEntities(string input)
            {
                var entities = new Dictionary<string, string>();

                // Extract dimension (e.g., "200mm", "150mm")
                var dimMatch = System.Text.RegularExpressions.Regex.Match(
                    input, @"(\d+)\s*(mm|cm|m|ft|in)");
                if (dimMatch.Success)
                {
                    entities["Dimension"] = dimMatch.Value;
                }

                // Extract material
                var materials = new[] { "concrete", "steel", "wood", "glass", "brick", "aluminum" };
                foreach (var material in materials)
                {
                    if (input.ToLowerInvariant().Contains(material))
                    {
                        entities["Material"] = material;
                        break;
                    }
                }

                // Extract level
                var levelMatch = System.Text.RegularExpressions.Regex.Match(
                    input, @"level\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (levelMatch.Success)
                {
                    entities["Level"] = levelMatch.Groups[1].Value;
                }

                return entities;
            }
        }

        private class IntentDefinition
        {
            public string Name { get; set; }
            public List<string> Examples { get; set; } = new List<string>();
            public List<string> Keywords { get; set; } = new List<string>();
        }

        private class ClassificationResult
        {
            public string Intent { get; set; }
            public float Confidence { get; set; }
        }

        private class ClassificationResultWithEntities : ClassificationResult
        {
            public Dictionary<string, string> Entities { get; set; } = new Dictionary<string, string>();
        }

        #endregion
    }
}
