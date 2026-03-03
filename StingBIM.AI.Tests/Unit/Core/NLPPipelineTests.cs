using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.NLP.Pipeline;
using StingBIM.AI.NLP.Semantic;

namespace StingBIM.AI.Tests.Unit.Core
{
    /// <summary>
    /// Unit tests for NLP pipeline: EntityExtractor (built-in patterns),
    /// and SemanticUnderstanding (frames, synonyms, disambiguation, concept graph).
    /// All tests run without ONNX models.
    /// </summary>
    [TestFixture]
    public class NLPPipelineTests
    {
        #region EntityExtractor Tests

        [TestFixture]
        public class EntityExtractorTests
        {
            private EntityExtractor _extractor;

            [SetUp]
            public void SetUp()
            {
                _extractor = new EntityExtractor();
            }

            // --- Dimension extraction ---

            [Test]
            public void Extract_DimensionInMeters_ExtractsDimension()
            {
                var entities = _extractor.Extract("create a wall 4m long");

                entities.Should().Contain(e => e.Type == EntityType.DIMENSION);
                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.Value.Should().Contain("4m");
                dim.NormalizedValue.Should().Be("4000mm");
                dim.Confidence.Should().BeGreaterOrEqualTo(0.9f);
            }

            [Test]
            public void Extract_DimensionInMillimeters_NormalizesToMm()
            {
                var entities = _extractor.Extract("wall is 200mm thick");

                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.NormalizedValue.Should().Be("200mm");
            }

            [Test]
            public void Extract_DimensionInFeet_ConvertsToMm()
            {
                var entities = _extractor.Extract("the room is 10 feet wide");

                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.NormalizedValue.Should().Be("3048mm");
            }

            [Test]
            public void Extract_DimensionInCentimeters_ConvertsToMm()
            {
                var entities = _extractor.Extract("add 50cm border");

                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.NormalizedValue.Should().Be("500mm");
            }

            [Test]
            public void Extract_DecimalDimension_HandlesDecimals()
            {
                var entities = _extractor.Extract("height is 3.5 meters");

                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.NormalizedValue.Should().Be("3500mm");
            }

            // --- Number extraction ---

            [Test]
            public void Extract_NumericDigits_ExtractsNumber()
            {
                var entities = _extractor.Extract("create 5 walls");

                entities.Should().Contain(e => e.Type == EntityType.NUMBER);
            }

            [Test]
            public void Extract_WordNumber_NormalizesToDigit()
            {
                var entities = _extractor.Extract("add three doors");

                var num = entities.First(e => e.Type == EntityType.NUMBER);
                num.NormalizedValue.Should().Be("3");
            }

            [TestCase("twelve", "12")]
            [TestCase("one", "1")]
            [TestCase("ten", "10")]
            public void Extract_WordNumbers_NormalizesCorrectly(string word, string expected)
            {
                var entities = _extractor.Extract($"create {word} elements");

                var num = entities.First(e => e.Type == EntityType.NUMBER);
                num.NormalizedValue.Should().Be(expected);
            }

            // --- Direction extraction ---

            [TestCase("move wall left", "left")]
            [TestCase("shift north", "north")]
            [TestCase("the wall above", "above")]
            [TestCase("place door beside", "beside")]
            public void Extract_Direction_ExtractsDirections(string input, string expected)
            {
                var entities = _extractor.Extract(input);

                entities.Should().Contain(e =>
                    e.Type == EntityType.DIRECTION &&
                    e.Value.Equals(expected, StringComparison.OrdinalIgnoreCase));
            }

            // --- Position extraction ---

            [TestCase("place wall here", "here")]
            [TestCase("put a door there", "there")]
            [TestCase("place at the cursor", "at the cursor")]
            public void Extract_Position_ExtractsPositions(string input, string expected)
            {
                var entities = _extractor.Extract(input);

                entities.Should().Contain(e =>
                    e.Type == EntityType.POSITION &&
                    e.Value.Equals(expected, StringComparison.OrdinalIgnoreCase));
            }

            // --- Color extraction ---

            [TestCase("make it red", "red")]
            [TestCase("set color to blue", "blue")]
            [TestCase("apply #FF0000", "#FF0000")]
            public void Extract_Color_ExtractsColors(string input, string expected)
            {
                var entities = _extractor.Extract(input);

                entities.Should().Contain(e =>
                    e.Type == EntityType.COLOR &&
                    e.Value.Equals(expected, StringComparison.OrdinalIgnoreCase));
            }

            // --- Multiple entity extraction ---

            [Test]
            public void Extract_MultipleEntities_ExtractsAll()
            {
                var entities = _extractor.Extract("create a wall 4m long and 3m high on the left");

                entities.Should().Contain(e => e.Type == EntityType.DIMENSION);
                entities.Should().Contain(e => e.Type == EntityType.DIRECTION);
            }

            [Test]
            public void Extract_EmptyString_ReturnsEmpty()
            {
                var entities = _extractor.Extract("");

                entities.Should().BeEmpty();
            }

            [Test]
            public void Extract_NoEntities_ReturnsEmpty()
            {
                var entities = _extractor.Extract("hello world");

                // May have number matches for some patterns, but no dimensions/directions/positions
                entities.Where(e => e.Type == EntityType.DIMENSION).Should().BeEmpty();
                entities.Where(e => e.Type == EntityType.DIRECTION).Should().BeEmpty();
                entities.Where(e => e.Type == EntityType.POSITION).Should().BeEmpty();
            }

            // --- Type-filtered extraction ---

            [Test]
            public void Extract_FilteredByType_ReturnsOnlyThatType()
            {
                var entities = _extractor.Extract("create a wall 4m long to the left", EntityType.DIRECTION);

                entities.Should().AllSatisfy(e => e.Type.Should().Be(EntityType.DIRECTION));
            }

            // --- Synonym resolution ---

            [Test]
            public void ResolveSynonym_NoSynonym_ReturnsSameWord()
            {
                var result = _extractor.ResolveSynonym("uniqueword");

                result.Should().Be("uniqueword");
            }

            // --- Entity structure ---

            [Test]
            public void Extract_ReturnsEntityWithPositionInfo()
            {
                var entities = _extractor.Extract("wall is 5m");

                var dim = entities.First(e => e.Type == EntityType.DIMENSION);
                dim.StartIndex.Should().BeGreaterOrEqualTo(0);
                dim.EndIndex.Should().BeGreaterThan(dim.StartIndex);
            }

            // --- Overlap resolution ---

            [Test]
            public void Extract_OverlappingEntities_HigherConfidenceWins()
            {
                // Built-in patterns have 0.95 confidence
                var entities = _extractor.Extract("move 5m north");

                // Should not have duplicate overlapping entities at same position
                var positions = entities.Select(e => e.StartIndex).ToList();
                positions.Should().OnlyHaveUniqueItems();
            }
        }

        #endregion

        #region SemanticUnderstanding Tests

        [TestFixture]
        public class SemanticUnderstandingTests
        {
            private SemanticUnderstanding _semantic;

            [SetUp]
            public void SetUp()
            {
                _semantic = new SemanticUnderstanding();
            }

            // --- Frame identification ---

            [TestCase("create a wall", "Creation")]
            [TestCase("add a door here", "Creation")]
            [TestCase("place a window", "Creation")]
            [TestCase("build a room", "Creation")]
            public void Analyze_CreationInput_IdentifiesCreationFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            [TestCase("change the height", "Modification")]
            [TestCase("modify the wall", "Modification")]
            [TestCase("make it taller", "Modification")]
            [TestCase("increase the width", "Modification")]
            public void Analyze_ModificationInput_IdentifiesModificationFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            [TestCase("what is the area", "Query")]
            [TestCase("how many walls", "Query")]
            [TestCase("show me the rooms", "Query")]
            [TestCase("find all doors", "Query")]
            public void Analyze_QueryInput_IdentifiesQueryFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            [TestCase("check compliance", "Analysis")]
            [TestCase("analyze the structure", "Analysis")]
            [TestCase("validate the model", "Analysis")]
            public void Analyze_AnalysisInput_IdentifiesAnalysisFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            [TestCase("go to level 1", "Navigation")]
            [TestCase("show the plan view", "Navigation")]
            [TestCase("zoom to the entrance", "Navigation")]
            public void Analyze_NavigationInput_IdentifiesNavigationFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            [TestCase("create a schedule", "Scheduling")]
            [TestCase("generate a door schedule", "Scheduling")]
            public void Analyze_SchedulingInput_IdentifiesSchedulingFrame(string input, string expectedFrame)
            {
                var analysis = _semantic.Analyze(input);

                analysis.Frame.Should().NotBeNull();
                analysis.Frame.FrameId.Should().Be(expectedFrame);
            }

            // --- Entity extraction ---

            [Test]
            public void Analyze_WithDimensions_ExtractsEntities()
            {
                var analysis = _semantic.Analyze("create a wall 4m long");

                analysis.Entities.Should().NotBeEmpty();
            }

            [Test]
            public void Analyze_ReturnsConfidence()
            {
                var analysis = _semantic.Analyze("create a wall");

                analysis.Confidence.Should().BeGreaterThan(0);
                analysis.Confidence.Should().BeLessOrEqualTo(1.0);
            }

            [Test]
            public void Analyze_ReturnsInterpretation()
            {
                var analysis = _semantic.Analyze("add a door to the room");

                analysis.Interpretation.Should().NotBeNullOrEmpty();
            }

            // --- Synonym resolution ---

            [Test]
            public void Synonyms_WallSynonyms_AreRecognized()
            {
                // "partition" is a synonym of "wall"
                var related = _semantic.GetRelatedConcepts("wall");

                related.Should().Contain(r => r.Relation == "synonym");
                related.Where(r => r.Relation == "synonym")
                    .Select(r => r.Concept)
                    .Should().Contain("partition");
            }

            [Test]
            public void Synonyms_DoorSynonyms_AreRecognized()
            {
                var related = _semantic.GetRelatedConcepts("door");

                related.Should().Contain(r =>
                    r.Relation == "synonym" && r.Concept == "entrance");
            }

            [Test]
            public void Synonyms_ActionSynonyms_CreateIncludesAdd()
            {
                var related = _semantic.GetRelatedConcepts("create");

                related.Should().Contain(r =>
                    r.Relation == "synonym" && r.Concept == "add");
            }

            // --- Concept graph ---

            [Test]
            public void GetRelatedConcepts_Wall_ContainsDoorAndWindow()
            {
                var related = _semantic.GetRelatedConcepts("Wall");

                related.Should().Contain(r => r.Concept == "Door" && r.Relation == "contains");
                related.Should().Contain(r => r.Concept == "Window" && r.Relation == "contains");
            }

            [Test]
            public void GetRelatedConcepts_Room_BoundedByWallFloorCeiling()
            {
                var related = _semantic.GetRelatedConcepts("Room");

                related.Should().Contain(r => r.Concept == "Wall" && r.Relation == "bounded_by");
                related.Should().Contain(r => r.Concept == "Floor" && r.Relation == "bounded_by");
                related.Should().Contain(r => r.Concept == "Ceiling" && r.Relation == "bounded_by");
            }

            [Test]
            public void GetRelatedConcepts_Column_SupportsBeamAndFloor()
            {
                var related = _semantic.GetRelatedConcepts("Column");

                related.Should().Contain(r => r.Concept == "Beam" && r.Relation == "supports");
                related.Should().Contain(r => r.Concept == "Floor" && r.Relation == "supports");
            }

            [Test]
            public void GetRelatedConcepts_Door_InverseRelationFromWall()
            {
                var related = _semantic.GetRelatedConcepts("Door");

                // Door is contained by Wall (inverse)
                related.Should().Contain(r =>
                    r.Concept == "Wall" && r.Relation.Contains("inverse"));
            }

            [Test]
            public void GetRelatedConcepts_Unknown_ReturnsEmpty()
            {
                var related = _semantic.GetRelatedConcepts("xyznonexistent");

                related.Should().BeEmpty();
            }

            // --- Slot filling ---

            [Test]
            public void ExtractSlots_Creation_WithElementType_FillsSlots()
            {
                var slots = _semantic.ExtractSlots("create a wall 4m long", "Creation");

                slots.FrameId.Should().Be("Creation");
                slots.FilledSlots.Should().NotBeEmpty();
            }

            [Test]
            public void ExtractSlots_UnknownFrame_ReturnsFailed()
            {
                var slots = _semantic.ExtractSlots("some input", "UnknownFrame");

                slots.Success.Should().BeFalse();
                slots.MissingSlots.Should().NotBeEmpty();
            }

            [Test]
            public void ExtractSlots_MissingCoreRoles_GeneratesClarificationQuestions()
            {
                // "create" without specifying element type — missing core role
                var slots = _semantic.ExtractSlots("create something", "Creation");

                if (!slots.Success)
                {
                    slots.ClarificationQuestions.Should().NotBeEmpty();
                }
            }

            // --- Query expansion ---

            [Test]
            public void ExpandQuery_WallQuery_IncludesSynonyms()
            {
                var expansion = _semantic.ExpandQuery("wall height");

                expansion.ExpandedTerms.Should().NotBeEmpty();
                var wallTerm = expansion.ExpandedTerms.FirstOrDefault(t => t.Original == "wall");
                wallTerm.Should().NotBeNull();
                wallTerm.Synonyms.Should().Contain("partition");
            }

            [Test]
            public void ExpandQuery_CreateDoor_IncludesRelatedTerms()
            {
                var expansion = _semantic.ExpandQuery("create door");

                expansion.OriginalQuery.Should().Be("create door");
                expansion.ExpandedTerms.Should().NotBeEmpty();
            }

            // --- Coreference resolution ---

            [Test]
            public void ResolveCoreferences_WithPronoun_FindsResolution()
            {
                var context = new StingBIM.AI.NLP.Semantic.ConversationContext
                {
                    RecentEntities = new List<string> { "wall", "door" }
                };

                var resolution = _semantic.ResolveCoreferences("make it taller", context);

                resolution.OriginalInput.Should().Be("make it taller");
                resolution.Resolutions.Should().NotBeEmpty();
            }

            [Test]
            public void ResolveCoreferences_NoPronoun_ReturnsEmptyResolutions()
            {
                var context = new StingBIM.AI.NLP.Semantic.ConversationContext();

                var resolution = _semantic.ResolveCoreferences("create a wall", context);

                resolution.Resolutions.Should().BeEmpty();
            }
        }

        #endregion
    }
}
