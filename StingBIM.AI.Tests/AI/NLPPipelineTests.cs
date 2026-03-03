// StingBIM.AI.Tests - NLPPipelineTests.cs
// Unit tests for NLP pipeline components
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.AI
{
    /// <summary>
    /// Unit tests for NLP pipeline components including tokenization,
    /// intent classification, and entity extraction.
    /// </summary>
    [TestFixture]
    public class NLPPipelineTests
    {
        #region Intent Classification Tests

        [Test]
        [TestCase("Create a wall on level 1", "CreateElement")]
        [TestCase("Add a door to the wall", "CreateElement")]
        [TestCase("What is the area of this room?", "Query")]
        [TestCase("Show me all walls on level 2", "Query")]
        [TestCase("Delete the selected element", "DeleteElement")]
        [TestCase("Remove this window", "DeleteElement")]
        [TestCase("Change the wall height to 3 meters", "ModifyParameter")]
        [TestCase("Set the door width to 900mm", "ModifyParameter")]
        [TestCase("Check compliance with building codes", "ComplianceCheck")]
        [TestCase("Is this design code compliant?", "ComplianceCheck")]
        public void ClassifyIntent_VariousCommands_ReturnsCorrectIntent(string command, string expectedIntent)
        {
            // Arrange
            var classifier = new SimpleIntentClassifier();

            // Act
            var intent = classifier.Classify(command);

            // Assert
            intent.Should().Be(expectedIntent);
        }

        [Test]
        public void ClassifyIntent_UnknownCommand_ReturnsGeneral()
        {
            // Arrange
            var classifier = new SimpleIntentClassifier();

            // Act
            var intent = classifier.Classify("Hello, how are you?");

            // Assert
            intent.Should().Be("General");
        }

        [Test]
        public void ClassifyIntent_EmptyString_ReturnsGeneral()
        {
            // Arrange
            var classifier = new SimpleIntentClassifier();

            // Act
            var intent = classifier.Classify("");

            // Assert
            intent.Should().Be("General");
        }

        #endregion

        #region Entity Extraction Tests

        [Test]
        public void ExtractEntities_WallCommand_ExtractsElementType()
        {
            // Arrange
            var extractor = new SimpleEntityExtractor();
            var command = "Create a brick wall on level 1";

            // Act
            var entities = extractor.Extract(command);

            // Assert
            entities.Should().ContainKey("ElementType");
            entities["ElementType"].Should().Be("wall");
        }

        [Test]
        public void ExtractEntities_DimensionCommand_ExtractsNumericValue()
        {
            // Arrange
            var extractor = new SimpleEntityExtractor();
            var command = "Set the wall height to 3000mm";

            // Act
            var entities = extractor.Extract(command);

            // Assert
            entities.Should().ContainKey("NumericValue");
            entities["NumericValue"].Should().Be("3000");
            entities.Should().ContainKey("Unit");
            entities["Unit"].Should().Be("mm");
        }

        [Test]
        public void ExtractEntities_LevelReference_ExtractsLevel()
        {
            // Arrange
            var extractor = new SimpleEntityExtractor();
            var command = "Create a floor on Level 2";

            // Act
            var entities = extractor.Extract(command);

            // Assert
            entities.Should().ContainKey("Level");
            entities["Level"].Should().Be("Level 2");
        }

        [Test]
        public void ExtractEntities_MultipleEntities_ExtractsAll()
        {
            // Arrange
            var extractor = new SimpleEntityExtractor();
            var command = "Create a 200mm thick wall that is 4000mm long on Level 1";

            // Act
            var entities = extractor.Extract(command);

            // Assert
            entities.Should().ContainKey("ElementType");
            entities.Should().ContainKey("Level");
            entities.Keys.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public void ExtractEntities_MaterialReference_ExtractsMaterial()
        {
            // Arrange
            var extractor = new SimpleEntityExtractor();
            var command = "Create a concrete wall with reinforcement";

            // Act
            var entities = extractor.Extract(command);

            // Assert
            entities.Should().ContainKey("Material");
            entities["Material"].Should().Be("concrete");
        }

        #endregion

        #region Tokenization Tests

        [Test]
        public void Tokenize_SimpleCommand_ReturnsTokens()
        {
            // Arrange
            var tokenizer = new SimpleTokenizer();
            var command = "Create a wall";

            // Act
            var tokens = tokenizer.Tokenize(command);

            // Assert
            tokens.Should().HaveCount(3);
            tokens.Should().Contain("create");
            tokens.Should().Contain("a");
            tokens.Should().Contain("wall");
        }

        [Test]
        public void Tokenize_WithPunctuation_RemovesPunctuation()
        {
            // Arrange
            var tokenizer = new SimpleTokenizer();
            var command = "Create a wall, please!";

            // Act
            var tokens = tokenizer.Tokenize(command);

            // Assert
            tokens.Should().NotContain(",");
            tokens.Should().NotContain("!");
        }

        [Test]
        public void Tokenize_CaseSensitivity_ReturnsLowercase()
        {
            // Arrange
            var tokenizer = new SimpleTokenizer();
            var command = "CREATE A WALL";

            // Act
            var tokens = tokenizer.Tokenize(command);

            // Assert
            tokens.Should().OnlyContain(t => t == t.ToLower());
        }

        [Test]
        public void Tokenize_WithNumbers_PreservesNumbers()
        {
            // Arrange
            var tokenizer = new SimpleTokenizer();
            var command = "Set height to 3000mm";

            // Act
            var tokens = tokenizer.Tokenize(command);

            // Assert
            tokens.Should().Contain("3000mm");
        }

        #endregion

        #region BIM Domain Vocabulary Tests

        [Test]
        [TestCase("wall", true)]
        [TestCase("door", true)]
        [TestCase("window", true)]
        [TestCase("floor", true)]
        [TestCase("ceiling", true)]
        [TestCase("column", true)]
        [TestCase("beam", true)]
        [TestCase("stair", true)]
        [TestCase("railing", true)]
        [TestCase("pizza", false)]
        [TestCase("car", false)]
        public void IsBIMElement_VariousWords_ReturnsCorrectResult(string word, bool expected)
        {
            // Arrange
            var vocabulary = new BIMVocabulary();

            // Act
            var result = vocabulary.IsBIMElement(word);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [TestCase("height", true)]
        [TestCase("width", true)]
        [TestCase("length", true)]
        [TestCase("area", true)]
        [TestCase("volume", true)]
        [TestCase("thickness", true)]
        [TestCase("color", false)]
        public void IsBIMParameter_VariousWords_ReturnsCorrectResult(string word, bool expected)
        {
            // Arrange
            var vocabulary = new BIMVocabulary();

            // Act
            var result = vocabulary.IsBIMParameter(word);

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        [TestCase("mm", true)]
        [TestCase("m", true)]
        [TestCase("ft", true)]
        [TestCase("in", true)]
        [TestCase("sqm", true)]
        [TestCase("xyz", false)]
        public void IsUnit_VariousWords_ReturnsCorrectResult(string word, bool expected)
        {
            // Arrange
            var vocabulary = new BIMVocabulary();

            // Act
            var result = vocabulary.IsUnit(word);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region Context Understanding Tests

        [Test]
        public void ResolveReference_ThisElement_ReturnsSelectedElement()
        {
            // Arrange
            var contextTracker = new SimpleContextTracker();
            contextTracker.SetSelectedElement("Wall-001");

            // Act
            var resolved = contextTracker.ResolveReference("this");

            // Assert
            resolved.Should().Be("Wall-001");
        }

        [Test]
        public void ResolveReference_TheWall_ReturnsLastMentionedWall()
        {
            // Arrange
            var contextTracker = new SimpleContextTracker();
            contextTracker.MentionElement("Wall", "Wall-001");

            // Act
            var resolved = contextTracker.ResolveReference("the wall");

            // Assert
            resolved.Should().Be("Wall-001");
        }

        [Test]
        public void TrackConversation_MultiTurn_MaintainsContext()
        {
            // Arrange
            var contextTracker = new SimpleContextTracker();

            // Act
            contextTracker.AddTurn("Create a wall on level 1", "Wall-001 created");
            contextTracker.AddTurn("Make it 3 meters tall", "Height set to 3000mm");

            // Assert
            contextTracker.GetCurrentContext().Should().ContainKey("LastCreatedElement");
            contextTracker.GetCurrentContext()["LastCreatedElement"].Should().Be("Wall-001");
        }

        #endregion
    }

    #region Test Helper Classes

    /// <summary>
    /// Simple intent classifier for testing
    /// </summary>
    public class SimpleIntentClassifier
    {
        private readonly Dictionary<string, List<string>> _intentKeywords = new()
        {
            ["CreateElement"] = new List<string> { "create", "add", "place", "insert", "draw" },
            ["DeleteElement"] = new List<string> { "delete", "remove", "erase" },
            ["ModifyParameter"] = new List<string> { "change", "set", "modify", "update", "adjust" },
            ["Query"] = new List<string> { "what", "show", "list", "find", "get", "how" },
            ["ComplianceCheck"] = new List<string> { "check", "compliance", "code", "compliant", "validate" }
        };

        public string Classify(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "General";

            var lowerCommand = command.ToLower();

            foreach (var intent in _intentKeywords)
            {
                if (intent.Value.Any(keyword => lowerCommand.Contains(keyword)))
                {
                    return intent.Key;
                }
            }

            return "General";
        }
    }

    /// <summary>
    /// Simple entity extractor for testing
    /// </summary>
    public class SimpleEntityExtractor
    {
        private readonly HashSet<string> _elementTypes = new()
        {
            "wall", "door", "window", "floor", "ceiling", "column", "beam", "stair", "roof"
        };

        private readonly HashSet<string> _materials = new()
        {
            "brick", "concrete", "steel", "wood", "glass", "aluminum"
        };

        public Dictionary<string, string> Extract(string command)
        {
            var entities = new Dictionary<string, string>();
            var lowerCommand = command.ToLower();

            // Extract element type
            foreach (var elementType in _elementTypes)
            {
                if (lowerCommand.Contains(elementType))
                {
                    entities["ElementType"] = elementType;
                    break;
                }
            }

            // Extract material
            foreach (var material in _materials)
            {
                if (lowerCommand.Contains(material))
                {
                    entities["Material"] = material;
                    break;
                }
            }

            // Extract numeric values with units
            var numericPattern = new System.Text.RegularExpressions.Regex(@"(\d+)(mm|m|ft|in)?");
            var match = numericPattern.Match(command);
            if (match.Success)
            {
                entities["NumericValue"] = match.Groups[1].Value;
                if (match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    entities["Unit"] = match.Groups[2].Value;
                }
            }

            // Extract level reference
            var levelPattern = new System.Text.RegularExpressions.Regex(@"(Level\s*\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var levelMatch = levelPattern.Match(command);
            if (levelMatch.Success)
            {
                entities["Level"] = levelMatch.Groups[1].Value;
            }

            return entities;
        }
    }

    /// <summary>
    /// Simple tokenizer for testing
    /// </summary>
    public class SimpleTokenizer
    {
        public List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Remove punctuation except for units attached to numbers
            var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"[,!?;:]", " ");

            // Split by whitespace and convert to lowercase
            return cleaned
                .Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .ToList();
        }
    }

    /// <summary>
    /// BIM vocabulary helper for testing
    /// </summary>
    public class BIMVocabulary
    {
        private readonly HashSet<string> _elements = new()
        {
            "wall", "door", "window", "floor", "ceiling", "column", "beam",
            "stair", "railing", "roof", "curtainwall", "room", "space"
        };

        private readonly HashSet<string> _parameters = new()
        {
            "height", "width", "length", "area", "volume", "thickness",
            "depth", "offset", "angle", "slope"
        };

        private readonly HashSet<string> _units = new()
        {
            "mm", "m", "cm", "ft", "in", "sqm", "sqft", "cbm", "cbft"
        };

        public bool IsBIMElement(string word) => _elements.Contains(word.ToLower());
        public bool IsBIMParameter(string word) => _parameters.Contains(word.ToLower());
        public bool IsUnit(string word) => _units.Contains(word.ToLower());
    }

    /// <summary>
    /// Simple context tracker for testing
    /// </summary>
    public class SimpleContextTracker
    {
        private readonly Dictionary<string, string> _context = new();
        private readonly Dictionary<string, string> _mentionedElements = new();
        private string _selectedElement;

        public void SetSelectedElement(string elementId)
        {
            _selectedElement = elementId;
        }

        public void MentionElement(string elementType, string elementId)
        {
            _mentionedElements[elementType.ToLower()] = elementId;
        }

        public string ResolveReference(string reference)
        {
            if (reference.ToLower() == "this" || reference.ToLower() == "it")
            {
                return _selectedElement;
            }

            var match = System.Text.RegularExpressions.Regex.Match(reference, @"the\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var elementType = match.Groups[1].Value.ToLower();
                if (_mentionedElements.TryGetValue(elementType, out var elementId))
                {
                    return elementId;
                }
            }

            return null;
        }

        public void AddTurn(string userMessage, string systemResponse)
        {
            // Extract created element from response
            var createdMatch = System.Text.RegularExpressions.Regex.Match(systemResponse, @"(\w+-\d+)\s+created");
            if (createdMatch.Success)
            {
                _context["LastCreatedElement"] = createdMatch.Groups[1].Value;
            }
        }

        public Dictionary<string, string> GetCurrentContext()
        {
            return new Dictionary<string, string>(_context);
        }
    }

    #endregion
}
