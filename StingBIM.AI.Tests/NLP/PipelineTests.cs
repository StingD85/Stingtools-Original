// StingBIM.AI.Tests.NLP.PipelineTests
// Tests for EntityExtractor and IntentPattern classes
// Validates entity extraction, normalization, and intent pattern matching

using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.NLP.Pipeline;
using System;
using System.Linq;

namespace StingBIM.AI.Tests.NLP
{
    [TestFixture]
    public class EntityExtractorTests
    {
        private EntityExtractor _extractor;

        [SetUp]
        public void SetUp()
        {
            _extractor = new EntityExtractor();
        }

        #region DIMENSION Entity Tests

        [TestCase("4m", "4000mm")]
        [TestCase("3.5 meters", "3500mm")]
        [TestCase("4000mm", "4000mm")]
        [TestCase("10 feet", "3048mm")]
        public void Extract_FindsDimensionEntities(string input, string expectedNormalized)
        {
            // Act
            var entities = _extractor.Extract(input);

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.DIMENSION);
            var dimension = entities.First(e => e.Type == EntityType.DIMENSION);
            dimension.Value.Should().NotBeNullOrEmpty();
            dimension.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        [TestCase("4m", "4000mm")]
        [TestCase("3.5 meters", "3500mm")]
        [TestCase("4000mm", "4000mm")]
        [TestCase("10 feet", "3048mm")]
        public void Extract_NormalizesDimensionsToMillimeters(string input, string expectedNormalized)
        {
            // Act
            var entities = _extractor.Extract(input);

            // Assert
            var dimension = entities.First(e => e.Type == EntityType.DIMENSION);
            dimension.NormalizedValue.Should().Be(expectedNormalized);
        }

        #endregion

        #region NUMBER Entity Tests

        [TestCase("3")]
        [TestCase("4.5")]
        public void Extract_FindsNumberEntities(string input)
        {
            // Act
            var entities = _extractor.Extract(input);

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.NUMBER);
            var number = entities.First(e => e.Type == EntityType.NUMBER);
            number.Value.Should().Be(input);
            number.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region DIRECTION Entity Tests

        [TestCase("left")]
        [TestCase("north")]
        public void Extract_FindsDirectionEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"move the wall to the {input}");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.DIRECTION);
            var direction = entities.First(e => e.Type == EntityType.DIRECTION);
            direction.Value.Should().Be(input);
            direction.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region COLOR Entity Tests

        [TestCase("red")]
        [TestCase("blue")]
        public void Extract_FindsColorEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"paint the wall {input}");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.COLOR);
            var color = entities.First(e => e.Type == EntityType.COLOR);
            color.Value.Should().Be(input);
            color.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region PERFORMANCE_SPEC Entity Tests

        [TestCase("fireproof")]
        [TestCase("load-bearing")]
        public void Extract_FindsPerformanceSpecEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"I need a {input} wall");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.PERFORMANCE_SPEC);
            var spec = entities.First(e => e.Type == EntityType.PERFORMANCE_SPEC);
            spec.Value.Should().NotBeNullOrEmpty();
            spec.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region COMPLIANCE_STANDARD Entity Tests

        [TestCase("ADA")]
        [TestCase("IBC")]
        [TestCase("ASHRAE")]
        public void Extract_FindsComplianceStandardEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"check {input} compliance");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.COMPLIANCE_STANDARD);
            var standard = entities.First(e => e.Type == EntityType.COMPLIANCE_STANDARD);
            standard.Value.Should().Be(input);
            standard.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region CLIMATE_ZONE Entity Tests

        [TestCase("tropical")]
        [TestCase("arid")]
        public void Extract_FindsClimateZoneEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"design for {input} climate");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.CLIMATE_ZONE);
            var zone = entities.First(e => e.Type == EntityType.CLIMATE_ZONE);
            zone.Value.Should().Be(input);
            zone.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region PROJECT_TYPE Entity Tests

        [TestCase("residential")]
        [TestCase("commercial")]
        public void Extract_FindsProjectTypeEntities(string input)
        {
            // Act
            var entities = _extractor.Extract($"this is a {input} project");

            // Assert
            entities.Should().Contain(e => e.Type == EntityType.PROJECT_TYPE);
            var projectType = entities.First(e => e.Type == EntityType.PROJECT_TYPE);
            projectType.Value.Should().Be(input);
            projectType.Confidence.Should().BeGreaterOrEqualTo(0.9f);
        }

        #endregion

        #region Empty / No Match Tests

        [Test]
        public void Extract_ReturnsEmptyListForTextWithNoEntities()
        {
            // Act
            var entities = _extractor.Extract("the quick fox jumps over the lazy dog");

            // Assert
            entities.Should().BeEmpty();
        }

        #endregion

        #region Synonym Tests

        [Test]
        public void ResolveSynonym_ReturnsOriginalWordWhenNoSynonymFound()
        {
            // Arrange - no synonyms loaded, so everything returns itself
            var word = "xylophone";

            // Act
            var result = _extractor.ResolveSynonym(word);

            // Assert
            result.Should().Be(word);
        }

        #endregion
    }

    [TestFixture]
    public class IntentPatternTests
    {
        #region PatternType.Exact Tests

        [Test]
        public void Matches_ExactPattern_MatchesExactly()
        {
            // Arrange
            var pattern = new IntentPattern
            {
                IntentName = "TEST_INTENT",
                Pattern = "create wall",
                Type = PatternType.Exact,
                Confidence = 0.9f
            };

            // Act & Assert
            pattern.Matches("create wall").Should().BeTrue();
            pattern.Matches("create wall here").Should().BeFalse();
            pattern.Matches("please create wall").Should().BeFalse();
        }

        #endregion

        #region PatternType.StartsWith Tests

        [Test]
        public void Matches_StartsWithPattern_MatchesPrefix()
        {
            // Arrange
            var pattern = new IntentPattern
            {
                IntentName = "TEST_INTENT",
                Pattern = "create",
                Type = PatternType.StartsWith,
                Confidence = 0.9f
            };

            // Act & Assert
            pattern.Matches("create a wall").Should().BeTrue();
            pattern.Matches("create").Should().BeTrue();
            pattern.Matches("please create").Should().BeFalse();
        }

        #endregion

        #region PatternType.Contains Tests

        [Test]
        public void Matches_ContainsPattern_MatchesSubstring()
        {
            // Arrange
            var pattern = new IntentPattern
            {
                IntentName = "TEST_INTENT",
                Pattern = "wall",
                Type = PatternType.Contains,
                Confidence = 0.9f
            };

            // Act & Assert
            pattern.Matches("create a wall here").Should().BeTrue();
            pattern.Matches("wall").Should().BeTrue();
            pattern.Matches("create a floor").Should().BeFalse();
        }

        #endregion

        #region PatternType.Regex Tests

        [Test]
        public void Matches_RegexPattern_MatchesRegex()
        {
            // Arrange
            var pattern = new IntentPattern
            {
                IntentName = "TEST_INTENT",
                Pattern = @"\b(create|add|place)\s+(wall|floor)\b",
                Type = PatternType.Regex,
                Confidence = 0.9f
            };

            // Act & Assert
            pattern.Matches("create wall").Should().BeTrue();
            pattern.Matches("add floor").Should().BeTrue();
            pattern.Matches("place wall").Should().BeTrue();
            pattern.Matches("delete wall").Should().BeFalse();
        }

        #endregion

        #region Case Insensitivity Tests

        [Test]
        public void Matches_IsCaseInsensitive()
        {
            // Arrange - test Exact
            var exactPattern = new IntentPattern
            {
                IntentName = "TEST_EXACT",
                Pattern = "create wall",
                Type = PatternType.Exact,
                Confidence = 0.9f
            };

            // Act & Assert - Exact is case-insensitive
            exactPattern.Matches("Create Wall").Should().BeTrue();
            exactPattern.Matches("CREATE WALL").Should().BeTrue();

            // Arrange - test Contains
            var containsPattern = new IntentPattern
            {
                IntentName = "TEST_CONTAINS",
                Pattern = "wall",
                Type = PatternType.Contains,
                Confidence = 0.9f
            };

            // Act & Assert - Contains is case-insensitive
            containsPattern.Matches("Create a WALL").Should().BeTrue();

            // Arrange - test StartsWith
            var startsWithPattern = new IntentPattern
            {
                IntentName = "TEST_STARTS",
                Pattern = "create",
                Type = PatternType.StartsWith,
                Confidence = 0.9f
            };

            // Act & Assert - StartsWith is case-insensitive
            startsWithPattern.Matches("CREATE a wall").Should().BeTrue();

            // Arrange - test Regex
            var regexPattern = new IntentPattern
            {
                IntentName = "TEST_REGEX",
                Pattern = @"\bwall\b",
                Type = PatternType.Regex,
                Confidence = 0.9f
            };

            // Act & Assert - Regex is case-insensitive
            regexPattern.Matches("Build a WALL").Should().BeTrue();
        }

        #endregion

        #region RegisterConsultingPatterns Tests

        [Test]
        public void RegisterConsultingPatterns_Registers22Patterns()
        {
            // Arrange
            var classifier = new IntentClassifier(null, null);

            // Act
            classifier.RegisterConsultingPatterns();

            // Assert - use reflection to access the private _patterns field
            var patternsField = typeof(IntentClassifier)
                .GetField("_patterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var patterns = (System.Collections.Generic.List<IntentPattern>)patternsField.GetValue(classifier);
            patterns.Should().HaveCount(22, "12 consulting + 10 management patterns should be registered");
        }

        [TestCase("CONSULT_STRUCTURAL", "beam sizing for the second floor")]
        [TestCase("CONSULT_MEP", "duct sizing for the HVAC system")]
        [TestCase("CONSULT_COMPLIANCE", "is this compliant with building code")]
        [TestCase("CONSULT_MATERIALS", "recommend a material for exterior walls")]
        [TestCase("CONSULT_COST", "estimate the cost of this design")]
        [TestCase("CONSULT_SUSTAINABILITY", "how to achieve leed certification")]
        [TestCase("CONSULT_FIRE_SAFETY", "fire rated wall requirements")]
        [TestCase("CONSULT_ACCESSIBILITY", "ada compliance for the entrance")]
        [TestCase("CONSULT_ENERGY", "energy efficiency improvements")]
        [TestCase("CONSULT_ACOUSTICS", "sound insulation rating needed")]
        [TestCase("CONSULT_DAYLIGHTING", "daylight factor analysis")]
        [TestCase("CONSULT_SITE_PLANNING", "parking requirements for the site")]
        [TestCase("MANAGE_DESIGN_ANALYSIS", "analyse the design layout")]
        [TestCase("MANAGE_OPTIMIZATION", "optimize the layout")]
        [TestCase("MANAGE_DECISION_SUPPORT", "compare these options")]
        [TestCase("MANAGE_VALIDATION", "validate the design model")]
        [TestCase("MANAGE_DESIGN_PATTERNS", "suggest a design pattern")]
        [TestCase("MANAGE_PREDICTIVE", "what should I do next")]
        [TestCase("CONSULT_BEP", "generate a bim execution plan")]
        [TestCase("CONSULT_DWG_TO_BIM", "convert dwg to bim")]
        [TestCase("CONSULT_IMAGE_TO_BIM", "convert image to bim")]
        [TestCase("MANAGE_GENERATIVE_DESIGN", "generative design options")]
        public void RegisterConsultingPatterns_EachPatternMatchesExpectedInput(string expectedIntent, string input)
        {
            // Arrange
            var classifier = new IntentClassifier(null, null);
            classifier.RegisterConsultingPatterns();

            // Access the private _patterns field
            var patternsField = typeof(IntentClassifier)
                .GetField("_patterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var patterns = (System.Collections.Generic.List<IntentPattern>)patternsField.GetValue(classifier);

            // Act - find patterns that match for the expected intent
            var matchingPattern = patterns
                .Where(p => p.IntentName == expectedIntent)
                .FirstOrDefault(p => p.Matches(input));

            // Assert
            matchingPattern.Should().NotBeNull(
                $"pattern for intent '{expectedIntent}' should match input '{input}'");
            matchingPattern.IntentName.Should().Be(expectedIntent);
        }

        #endregion
    }
}
