// StingBIM.AI.Tests.Creation.NLPElementCreatorTests
// Unit tests for NLPElementCreator - Natural Language to BIM Element Creation
// Tests intent recognition, entity extraction, and element creation from text

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Creation
{
    [TestFixture]
    public class NLPElementCreatorTests
    {
        #region Intent Recognition Tests

        [Test]
        public void RecognizeIntent_CreateWallCommand_ReturnsCreateIntent()
        {
            // Arrange
            var recognizer = new IntentRecognizer();
            var input = "Create a wall 3 meters long";

            // Act
            var intent = recognizer.RecognizeIntent(input);

            // Assert
            intent.IntentType.Should().Be(IntentType.Create);
            intent.Confidence.Should().BeGreaterThan(0.7);
        }

        [Test]
        public void RecognizeIntent_ModifyCommand_ReturnsModifyIntent()
        {
            // Arrange
            var recognizer = new IntentRecognizer();
            var input = "Change the wall height to 4 meters";

            // Act
            var intent = recognizer.RecognizeIntent(input);

            // Assert
            intent.IntentType.Should().Be(IntentType.Modify);
        }

        [Test]
        public void RecognizeIntent_DeleteCommand_ReturnsDeleteIntent()
        {
            // Arrange
            var recognizer = new IntentRecognizer();
            var input = "Remove the selected door";

            // Act
            var intent = recognizer.RecognizeIntent(input);

            // Assert
            intent.IntentType.Should().Be(IntentType.Delete);
        }

        [Test]
        public void RecognizeIntent_QueryCommand_ReturnsQueryIntent()
        {
            // Arrange
            var recognizer = new IntentRecognizer();
            var input = "What is the area of this room?";

            // Act
            var intent = recognizer.RecognizeIntent(input);

            // Assert
            intent.IntentType.Should().Be(IntentType.Query);
        }

        [Test]
        public void RecognizeIntent_AmbiguousCommand_ReturnsLowConfidence()
        {
            // Arrange
            var recognizer = new IntentRecognizer();
            var input = "wall door window";

            // Act
            var intent = recognizer.RecognizeIntent(input);

            // Assert
            intent.Confidence.Should().BeLessThan(0.5);
        }

        #endregion

        #region Entity Extraction Tests

        [Test]
        public void ExtractEntities_WallWithDimensions_ExtractsAllEntities()
        {
            // Arrange
            var extractor = new BIMEntityExtractor();
            var input = "Create a concrete wall 3 meters long and 2.5 meters high";

            // Act
            var entities = extractor.ExtractEntities(input);

            // Assert
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "wall");
            entities.Should().Contain(e => e.EntityType == EntityType.Material && e.Value == "concrete");
            entities.Should().Contain(e => e.EntityType == EntityType.Dimension && e.Label == "length");
            entities.Should().Contain(e => e.EntityType == EntityType.Dimension && e.Label == "height");
        }

        [Test]
        public void ExtractEntities_DoorWithWidth_ExtractsDoorEntity()
        {
            // Arrange
            var extractor = new BIMEntityExtractor();
            var input = "Add a 900mm wide door";

            // Act
            var entities = extractor.ExtractEntities(input);

            // Assert
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "door");
            var widthEntity = entities.FirstOrDefault(e => e.Label == "width");
            widthEntity.Should().NotBeNull();
            widthEntity.NumericValue.Should().Be(900);
            widthEntity.Unit.Should().Be("mm");
        }

        [Test]
        public void ExtractEntities_RoomWithArea_ExtractsAreaDimension()
        {
            // Arrange
            var extractor = new BIMEntityExtractor();
            var input = "Create a 20 square meter office";

            // Act
            var entities = extractor.ExtractEntities(input);

            // Assert
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "room");
            entities.Should().Contain(e => e.EntityType == EntityType.RoomType && e.Value == "office");
            var areaEntity = entities.FirstOrDefault(e => e.Label == "area");
            areaEntity.Should().NotBeNull();
            areaEntity.NumericValue.Should().Be(20);
        }

        [Test]
        public void ExtractEntities_WindowOnNorthWall_ExtractsLocationEntity()
        {
            // Arrange
            var extractor = new BIMEntityExtractor();
            var input = "Place a window on the north wall";

            // Act
            var entities = extractor.ExtractEntities(input);

            // Assert
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "window");
            entities.Should().Contain(e => e.EntityType == EntityType.Location && e.Value == "north wall");
        }

        [Test]
        public void ExtractEntities_MultipleElements_ExtractsAll()
        {
            // Arrange
            var extractor = new BIMEntityExtractor();
            var input = "Add a door and two windows to the bedroom";

            // Act
            var entities = extractor.ExtractEntities(input);

            // Assert
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "door");
            entities.Should().Contain(e => e.EntityType == EntityType.ElementType && e.Value == "window");
            var windowEntity = entities.FirstOrDefault(e => e.Value == "window");
            windowEntity?.Quantity.Should().Be(2);
        }

        #endregion

        #region Unit Conversion Tests

        [Test]
        public void ConvertUnits_MetersToMillimeters_ConvertsCorrectly()
        {
            // Arrange
            var converter = new UnitConverter();

            // Act
            var result = converter.Convert(3.0, "m", "mm");

            // Assert
            result.Should().Be(3000.0);
        }

        [Test]
        public void ConvertUnits_FeetToMeters_ConvertsCorrectly()
        {
            // Arrange
            var converter = new UnitConverter();

            // Act
            var result = converter.Convert(10.0, "ft", "m");

            // Assert
            result.Should().BeApproximately(3.048, 0.001);
        }

        [Test]
        public void ConvertUnits_InchesToMillimeters_ConvertsCorrectly()
        {
            // Arrange
            var converter = new UnitConverter();

            // Act
            var result = converter.Convert(12.0, "in", "mm");

            // Assert
            result.Should().BeApproximately(304.8, 0.1);
        }

        [Test]
        public void ConvertUnits_SquareMetersToSquareFeet_ConvertsCorrectly()
        {
            // Arrange
            var converter = new UnitConverter();

            // Act
            var result = converter.Convert(10.0, "m2", "ft2");

            // Assert
            result.Should().BeApproximately(107.639, 0.01);
        }

        #endregion

        #region Element Creation Request Tests

        [Test]
        public void ParseCreationRequest_SimpleWall_CreatesValidRequest()
        {
            // Arrange
            var parser = new CreationRequestParser();
            var input = "Create a wall 5 meters long";

            // Act
            var request = parser.Parse(input);

            // Assert
            request.ElementType.Should().Be("wall");
            request.Parameters.Should().ContainKey("length");
            request.Parameters["length"].Should().Be(5000.0); // Converted to mm
        }

        [Test]
        public void ParseCreationRequest_DoorWithType_IncludesTypeInfo()
        {
            // Arrange
            var parser = new CreationRequestParser();
            var input = "Add a single swing door 800mm wide";

            // Act
            var request = parser.Parse(input);

            // Assert
            request.ElementType.Should().Be("door");
            request.SubType.Should().Be("single swing");
            request.Parameters["width"].Should().Be(800.0);
        }

        [Test]
        public void ParseCreationRequest_RoomWithFunction_IncludesRoomType()
        {
            // Arrange
            var parser = new CreationRequestParser();
            var input = "Create a conference room 30 square meters";

            // Act
            var request = parser.Parse(input);

            // Assert
            request.ElementType.Should().Be("room");
            request.RoomType.Should().Be("conference room");
            request.Parameters["area"].Should().Be(30.0);
        }

        #endregion

        #region Contextual Understanding Tests

        [Test]
        public void ResolveReference_ItReference_ResolvesToLastElement()
        {
            // Arrange
            var context = new ConversationContext();
            context.AddElement("wall", "WALL-001");
            var resolver = new ReferenceResolver(context);

            // Act
            var resolved = resolver.Resolve("Make it 4 meters high");

            // Assert
            resolved.ReferencedElementId.Should().Be("WALL-001");
            resolved.ReferencedElementType.Should().Be("wall");
        }

        [Test]
        public void ResolveReference_TheWallReference_ResolvesToCorrectElement()
        {
            // Arrange
            var context = new ConversationContext();
            context.AddElement("wall", "WALL-001");
            context.AddElement("door", "DOOR-001");
            var resolver = new ReferenceResolver(context);

            // Act
            var resolved = resolver.Resolve("Make the wall thicker");

            // Assert
            resolved.ReferencedElementId.Should().Be("WALL-001");
        }

        [Test]
        public void ResolveReference_ThatDoorReference_ResolvesToLastDoor()
        {
            // Arrange
            var context = new ConversationContext();
            context.AddElement("door", "DOOR-001");
            context.AddElement("wall", "WALL-001");
            context.AddElement("door", "DOOR-002");
            var resolver = new ReferenceResolver(context);

            // Act
            var resolved = resolver.Resolve("Remove that door");

            // Assert
            resolved.ReferencedElementId.Should().Be("DOOR-002");
        }

        [Test]
        public void ResolveReference_NoContext_ReturnsNull()
        {
            // Arrange
            var context = new ConversationContext();
            var resolver = new ReferenceResolver(context);

            // Act
            var resolved = resolver.Resolve("Delete it");

            // Assert
            resolved.Should().BeNull();
        }

        #endregion

        #region Validation Tests

        [Test]
        public void ValidateRequest_WallWithValidDimensions_ReturnsValid()
        {
            // Arrange
            var validator = new CreationRequestValidator();
            var request = new ElementCreationRequest
            {
                ElementType = "wall",
                Parameters = new Dictionary<string, double>
                {
                    { "length", 3000 },
                    { "height", 2700 }
                }
            };

            // Act
            var result = validator.Validate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateRequest_WallWithNegativeLength_ReturnsInvalid()
        {
            // Arrange
            var validator = new CreationRequestValidator();
            var request = new ElementCreationRequest
            {
                ElementType = "wall",
                Parameters = new Dictionary<string, double>
                {
                    { "length", -1000 },
                    { "height", 2700 }
                }
            };

            // Act
            var result = validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("negative"));
        }

        [Test]
        public void ValidateRequest_DoorTooWide_ReturnsWarning()
        {
            // Arrange
            var validator = new CreationRequestValidator();
            var request = new ElementCreationRequest
            {
                ElementType = "door",
                Parameters = new Dictionary<string, double>
                {
                    { "width", 2500 }
                }
            };

            // Act
            var result = validator.Validate(request);

            // Assert
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w => w.Contains("unusual") || w.Contains("wide"));
        }

        [Test]
        public void ValidateRequest_MissingRequiredParameter_ReturnsInvalid()
        {
            // Arrange
            var validator = new CreationRequestValidator();
            var request = new ElementCreationRequest
            {
                ElementType = "wall",
                Parameters = new Dictionary<string, double>() // Empty
            };

            // Act
            var result = validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("required") || e.Contains("length"));
        }

        #endregion

        #region Spelling Correction Tests

        [Test]
        public void CorrectSpelling_CommonMisspelling_CorrectsTerm()
        {
            // Arrange
            var corrector = new BIMSpellingCorrector();

            // Act & Assert
            corrector.Correct("wll").Should().Be("wall");
            corrector.Correct("widnow").Should().Be("window");
            corrector.Correct("dor").Should().Be("door");
            corrector.Correct("flor").Should().Be("floor");
        }

        [Test]
        public void CorrectSpelling_CorrectTerm_ReturnsUnchanged()
        {
            // Arrange
            var corrector = new BIMSpellingCorrector();

            // Act
            var result = corrector.Correct("wall");

            // Assert
            result.Should().Be("wall");
        }

        [Test]
        public void CorrectSpelling_UnknownTerm_ReturnsOriginal()
        {
            // Arrange
            var corrector = new BIMSpellingCorrector();

            // Act
            var result = corrector.Correct("xyz123");

            // Assert
            result.Should().Be("xyz123");
        }

        #endregion

        #region Response Generation Tests

        [Test]
        public void GenerateResponse_SuccessfulCreation_ReturnsConfirmation()
        {
            // Arrange
            var generator = new ResponseGenerator();
            var result = new CreationResult
            {
                Success = true,
                ElementId = "WALL-001",
                ElementType = "wall"
            };

            // Act
            var response = generator.GenerateCreationResponse(result);

            // Assert
            response.Should().Contain("wall");
            response.Should().Contain("created");
        }

        [Test]
        public void GenerateResponse_FailedCreation_ReturnsErrorMessage()
        {
            // Arrange
            var generator = new ResponseGenerator();
            var result = new CreationResult
            {
                Success = false,
                ErrorMessage = "Invalid wall placement"
            };

            // Act
            var response = generator.GenerateCreationResponse(result);

            // Assert
            response.Should().Contain("could not");
            response.Should().Contain("Invalid wall placement");
        }

        [Test]
        public void GenerateResponse_WithSuggestion_IncludesSuggestion()
        {
            // Arrange
            var generator = new ResponseGenerator();
            var result = new CreationResult
            {
                Success = true,
                ElementId = "DOOR-001",
                ElementType = "door",
                Suggestions = new List<string> { "Consider adding a door swing direction" }
            };

            // Act
            var response = generator.GenerateCreationResponse(result);

            // Assert
            response.Should().Contain("swing direction");
        }

        #endregion
    }

    #region Test Helper Classes

    internal enum IntentType
    {
        Create,
        Modify,
        Delete,
        Query,
        Navigate,
        Unknown
    }

    internal class RecognizedIntent
    {
        public IntentType IntentType { get; set; }
        public double Confidence { get; set; }
    }

    internal class IntentRecognizer
    {
        private readonly Dictionary<string, IntentType> _intentKeywords = new()
        {
            { "create", IntentType.Create },
            { "add", IntentType.Create },
            { "make", IntentType.Create },
            { "place", IntentType.Create },
            { "change", IntentType.Modify },
            { "modify", IntentType.Modify },
            { "update", IntentType.Modify },
            { "set", IntentType.Modify },
            { "delete", IntentType.Delete },
            { "remove", IntentType.Delete },
            { "what", IntentType.Query },
            { "how", IntentType.Query },
            { "show", IntentType.Query }
        };

        public RecognizedIntent RecognizeIntent(string input)
        {
            var lowerInput = input.ToLowerInvariant();
            var words = lowerInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (_intentKeywords.TryGetValue(word, out var intentType))
                {
                    return new RecognizedIntent
                    {
                        IntentType = intentType,
                        Confidence = 0.85
                    };
                }
            }

            // Check for question patterns
            if (lowerInput.Contains("?"))
            {
                return new RecognizedIntent { IntentType = IntentType.Query, Confidence = 0.7 };
            }

            return new RecognizedIntent { IntentType = IntentType.Unknown, Confidence = 0.3 };
        }
    }

    internal enum EntityType
    {
        ElementType,
        Material,
        Dimension,
        Location,
        RoomType,
        Quantity
    }

    internal class ExtractedEntity
    {
        public EntityType EntityType { get; set; }
        public string Value { get; set; }
        public string Label { get; set; }
        public double NumericValue { get; set; }
        public string Unit { get; set; }
        public int Quantity { get; set; } = 1;
    }

    internal class BIMEntityExtractor
    {
        private readonly HashSet<string> _elementTypes = new() { "wall", "door", "window", "floor", "ceiling", "room", "column", "beam" };
        private readonly HashSet<string> _materials = new() { "concrete", "brick", "steel", "wood", "glass", "aluminum" };
        private readonly HashSet<string> _roomTypes = new() { "office", "bedroom", "bathroom", "kitchen", "living room", "conference room" };

        public List<ExtractedEntity> ExtractEntities(string input)
        {
            var entities = new List<ExtractedEntity>();
            var lowerInput = input.ToLowerInvariant();

            // Extract element types
            foreach (var elementType in _elementTypes)
            {
                if (lowerInput.Contains(elementType))
                {
                    var entity = new ExtractedEntity { EntityType = EntityType.ElementType, Value = elementType };

                    // Check for quantity (e.g., "two windows")
                    var quantityMatch = System.Text.RegularExpressions.Regex.Match(lowerInput, $@"(\d+|two|three|four|five)\s+{elementType}s?");
                    if (quantityMatch.Success)
                    {
                        entity.Quantity = ParseQuantity(quantityMatch.Groups[1].Value);
                    }

                    entities.Add(entity);
                }
            }

            // Extract materials
            foreach (var material in _materials)
            {
                if (lowerInput.Contains(material))
                {
                    entities.Add(new ExtractedEntity { EntityType = EntityType.Material, Value = material });
                }
            }

            // Extract room types
            foreach (var roomType in _roomTypes)
            {
                if (lowerInput.Contains(roomType))
                {
                    entities.Add(new ExtractedEntity { EntityType = EntityType.RoomType, Value = roomType });
                    // Also add room as element type if not already
                    if (!entities.Any(e => e.EntityType == EntityType.ElementType && e.Value == "room"))
                    {
                        entities.Add(new ExtractedEntity { EntityType = EntityType.ElementType, Value = "room" });
                    }
                }
            }

            // Extract dimensions
            var dimensionPatterns = new Dictionary<string, string>
            {
                { @"(\d+(?:\.\d+)?)\s*(m|meters?|mm|millimeters?|ft|feet|in|inches?)\s+(?:long|length)", "length" },
                { @"(\d+(?:\.\d+)?)\s*(m|meters?|mm|millimeters?|ft|feet|in|inches?)\s+(?:high|height|tall)", "height" },
                { @"(\d+(?:\.\d+)?)\s*(m|meters?|mm|millimeters?|ft|feet|in|inches?)\s+(?:wide|width)", "width" },
                { @"(\d+(?:\.\d+)?)\s*(?:square\s*)?(m|meters?|m2|sqm)\s+", "area" }
            };

            foreach (var pattern in dimensionPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerInput, pattern.Key);
                if (match.Success)
                {
                    entities.Add(new ExtractedEntity
                    {
                        EntityType = EntityType.Dimension,
                        Label = pattern.Value,
                        NumericValue = double.Parse(match.Groups[1].Value),
                        Unit = NormalizeUnit(match.Groups[2].Value)
                    });
                }
            }

            // Extract location
            var locationPatterns = new[] { "north wall", "south wall", "east wall", "west wall", "front", "back", "left", "right" };
            foreach (var location in locationPatterns)
            {
                if (lowerInput.Contains(location))
                {
                    entities.Add(new ExtractedEntity { EntityType = EntityType.Location, Value = location });
                    break;
                }
            }

            return entities;
        }

        private int ParseQuantity(string value)
        {
            var wordToNumber = new Dictionary<string, int>
            {
                { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 }
            };

            if (int.TryParse(value, out var num)) return num;
            return wordToNumber.TryGetValue(value.ToLower(), out var wordNum) ? wordNum : 1;
        }

        private string NormalizeUnit(string unit)
        {
            return unit.ToLower() switch
            {
                "m" or "meters" or "meter" => "m",
                "mm" or "millimeters" or "millimeter" => "mm",
                "ft" or "feet" or "foot" => "ft",
                "in" or "inches" or "inch" => "in",
                "m2" or "sqm" => "m2",
                _ => unit
            };
        }
    }

    internal class UnitConverter
    {
        private readonly Dictionary<(string, string), double> _conversionFactors = new()
        {
            { ("m", "mm"), 1000.0 },
            { ("mm", "m"), 0.001 },
            { ("ft", "m"), 0.3048 },
            { ("m", "ft"), 3.28084 },
            { ("in", "mm"), 25.4 },
            { ("mm", "in"), 0.0393701 },
            { ("m2", "ft2"), 10.7639 },
            { ("ft2", "m2"), 0.092903 }
        };

        public double Convert(double value, string fromUnit, string toUnit)
        {
            if (fromUnit == toUnit) return value;

            if (_conversionFactors.TryGetValue((fromUnit, toUnit), out var factor))
            {
                return value * factor;
            }

            throw new ArgumentException($"Conversion from {fromUnit} to {toUnit} not supported");
        }
    }

    internal class ElementCreationRequest
    {
        public string ElementType { get; set; }
        public string SubType { get; set; }
        public string RoomType { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new();
    }

    internal class CreationRequestParser
    {
        private readonly BIMEntityExtractor _extractor = new();
        private readonly UnitConverter _converter = new();

        public ElementCreationRequest Parse(string input)
        {
            var entities = _extractor.ExtractEntities(input);
            var request = new ElementCreationRequest();

            // Get element type
            var elementEntity = entities.FirstOrDefault(e => e.EntityType == EntityType.ElementType);
            request.ElementType = elementEntity?.Value ?? "unknown";

            // Get room type if applicable
            var roomTypeEntity = entities.FirstOrDefault(e => e.EntityType == EntityType.RoomType);
            request.RoomType = roomTypeEntity?.Value;

            // Extract sub-type from input
            var lowerInput = input.ToLowerInvariant();
            if (lowerInput.Contains("single swing")) request.SubType = "single swing";
            else if (lowerInput.Contains("double swing")) request.SubType = "double swing";
            else if (lowerInput.Contains("sliding")) request.SubType = "sliding";

            // Get dimensions and convert to standard units (mm for length, m2 for area)
            foreach (var dim in entities.Where(e => e.EntityType == EntityType.Dimension))
            {
                double value = dim.NumericValue;

                if (dim.Label != "area")
                {
                    // Convert to mm for length dimensions
                    if (dim.Unit == "m") value = _converter.Convert(value, "m", "mm");
                    else if (dim.Unit == "ft") value = _converter.Convert(value, "ft", "m") * 1000;
                    else if (dim.Unit == "in") value = _converter.Convert(value, "in", "mm");
                }

                request.Parameters[dim.Label] = value;
            }

            return request;
        }
    }

    internal class ConversationContext
    {
        private readonly List<(string Type, string Id)> _elements = new();

        public void AddElement(string type, string id)
        {
            _elements.Add((type, id));
        }

        public (string Type, string Id)? GetLastElement()
        {
            return _elements.Count > 0 ? _elements[^1] : null;
        }

        public (string Type, string Id)? GetLastElementOfType(string type)
        {
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                if (_elements[i].Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                    return _elements[i];
            }
            return null;
        }
    }

    internal class ResolvedReference
    {
        public string ReferencedElementId { get; set; }
        public string ReferencedElementType { get; set; }
    }

    internal class ReferenceResolver
    {
        private readonly ConversationContext _context;

        public ReferenceResolver(ConversationContext context)
        {
            _context = context;
        }

        public ResolvedReference Resolve(string input)
        {
            var lowerInput = input.ToLowerInvariant();

            // Check for "it" reference
            if (lowerInput.Contains(" it ") || lowerInput.EndsWith(" it"))
            {
                var last = _context.GetLastElement();
                if (last == null) return null;
                return new ResolvedReference { ReferencedElementId = last.Value.Id, ReferencedElementType = last.Value.Type };
            }

            // Check for "the/that [element]" reference
            var elementTypes = new[] { "wall", "door", "window", "floor", "room", "column" };
            foreach (var elementType in elementTypes)
            {
                if (lowerInput.Contains($"the {elementType}") || lowerInput.Contains($"that {elementType}"))
                {
                    var element = _context.GetLastElementOfType(elementType);
                    if (element != null)
                    {
                        return new ResolvedReference { ReferencedElementId = element.Value.Id, ReferencedElementType = element.Value.Type };
                    }
                }
            }

            return null;
        }
    }

    internal class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    internal class CreationRequestValidator
    {
        private readonly Dictionary<string, string[]> _requiredParameters = new()
        {
            { "wall", new[] { "length" } },
            { "door", new[] { "width" } },
            { "window", new[] { "width" } }
        };

        private readonly Dictionary<string, (double Min, double Max)> _parameterRanges = new()
        {
            { "wall.length", (100, 50000) },
            { "wall.height", (1000, 10000) },
            { "door.width", (600, 2000) },
            { "door.height", (1800, 3000) },
            { "window.width", (300, 5000) }
        };

        public ValidationResult Validate(ElementCreationRequest request)
        {
            var result = new ValidationResult { IsValid = true };

            // Check for required parameters
            if (_requiredParameters.TryGetValue(request.ElementType, out var required))
            {
                foreach (var param in required)
                {
                    if (!request.Parameters.ContainsKey(param))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Missing required parameter: {param}");
                    }
                }
            }

            // Check for negative values
            foreach (var param in request.Parameters)
            {
                if (param.Value < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Parameter {param.Key} cannot be negative");
                }
            }

            // Check parameter ranges
            foreach (var param in request.Parameters)
            {
                var key = $"{request.ElementType}.{param.Key}";
                if (_parameterRanges.TryGetValue(key, out var range))
                {
                    if (param.Value < range.Min || param.Value > range.Max)
                    {
                        result.Warnings.Add($"Parameter {param.Key} value {param.Value} is unusual (typical range: {range.Min}-{range.Max})");
                    }
                }
            }

            return result;
        }
    }

    internal class BIMSpellingCorrector
    {
        private readonly Dictionary<string, string> _corrections = new()
        {
            { "wll", "wall" },
            { "wal", "wall" },
            { "wlal", "wall" },
            { "widnow", "window" },
            { "windwo", "window" },
            { "winodw", "window" },
            { "dor", "door" },
            { "dorr", "door" },
            { "doar", "door" },
            { "flor", "floor" },
            { "floro", "floor" },
            { "floorr", "floor" },
            { "ceilling", "ceiling" },
            { "celing", "ceiling" },
            { "colum", "column" },
            { "colmun", "column" }
        };

        public string Correct(string term)
        {
            return _corrections.TryGetValue(term.ToLower(), out var correction) ? correction : term;
        }
    }

    internal class CreationResult
    {
        public bool Success { get; set; }
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    internal class ResponseGenerator
    {
        public string GenerateCreationResponse(CreationResult result)
        {
            if (result.Success)
            {
                var response = $"I've successfully created the {result.ElementType}";
                if (!string.IsNullOrEmpty(result.ElementId))
                {
                    response += $" (ID: {result.ElementId})";
                }
                response += ".";

                if (result.Suggestions.Count > 0)
                {
                    response += $" Tip: {result.Suggestions[0]}";
                }

                return response;
            }
            else
            {
                return $"I could not create the element. {result.ErrorMessage}";
            }
        }
    }

    #endregion
}
