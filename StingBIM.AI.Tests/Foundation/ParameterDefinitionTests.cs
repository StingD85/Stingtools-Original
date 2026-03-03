// StingBIM.AI.Tests - ParameterDefinitionTests.cs
// Unit tests for ParameterDefinition and ParameterDefinitionCollection
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Parameters;

namespace StingBIM.AI.Tests.Foundation
{
    /// <summary>
    /// Unit tests for ParameterDefinition parsing, validation, and collection operations.
    /// </summary>
    [TestFixture]
    public class ParameterDefinitionTests
    {
        #region ParameterDefinition Construction Tests

        [Test]
        public void Constructor_DefaultValues_AreCorrect()
        {
            // Act
            var param = new ParameterDefinition();

            // Assert
            param.IsVisible.Should().BeTrue();
            param.IsUserModifiable.Should().BeTrue();
            param.HideWhenNoValue.Should().BeFalse();
            param.BoundCategories.Should().NotBeNull();
            param.BoundCategories.Should().BeEmpty();
        }

        [Test]
        public void FromSharedParameterLine_ValidLine_ParsesCorrectly()
        {
            // Arrange
            var line = "PARAM\t12345678-1234-1234-1234-123456789012\tBLE_WALL_HEIGHT_MM\tLENGTH\t\t1\t1\tWall height in millimeters\t1\t0";
            var groups = new Dictionary<int, string>
            {
                { 1, "BLE_ELES" }
            };

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Name.Should().Be("BLE_WALL_HEIGHT_MM");
            param.DataType.Should().Be("LENGTH");
            param.GroupId.Should().Be(1);
            param.GroupName.Should().Be("BLE_ELES");
            param.IsVisible.Should().BeTrue();
            param.Description.Should().Be("Wall height in millimeters");
            param.IsUserModifiable.Should().BeTrue();
            param.HideWhenNoValue.Should().BeFalse();
        }

        [Test]
        public void FromSharedParameterLine_InvalidPrefix_ThrowsArgumentException()
        {
            // Arrange
            var line = "INVALID\t12345678-1234-1234-1234-123456789012\tTest\tTEXT\t\t1\t1\tDesc\t1\t0";
            var groups = new Dictionary<int, string>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                ParameterDefinition.FromSharedParameterLine(line, groups));
        }

        [Test]
        public void FromSharedParameterLine_EmptyLine_ThrowsArgumentException()
        {
            // Arrange
            var groups = new Dictionary<int, string>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                ParameterDefinition.FromSharedParameterLine("", groups));
        }

        [Test]
        public void FromSharedParameterLine_InsufficientParts_ThrowsArgumentException()
        {
            // Arrange
            var line = "PARAM\t12345678-1234-1234-1234-123456789012\tTest";
            var groups = new Dictionary<int, string>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                ParameterDefinition.FromSharedParameterLine(line, groups));
        }

        #endregion

        #region Discipline Extraction Tests

        [Test]
        [TestCase("BLE_WALL_HEIGHT_MM", "Building Elements")]
        [TestCase("ELC_CBL_SZ_MM", "Electrical")]
        [TestCase("HVC_DUCT_SZ_MM", "HVAC")]
        [TestCase("PLM_PIPE_SZ_MM", "Plumbing")]
        [TestCase("STR_BEAM_DEPTH", "Structural")]
        [TestCase("ASS_SERIAL_NO", "Asset Management")]
        [TestCase("CST_UNIT_PRICE", "Cost/Procurement")]
        [TestCase("UNKNOWN_PARAM", "General")]
        public void ExtractDiscipline_VariousPrefixes_ReturnsCorrectDiscipline(string paramName, string expectedDiscipline)
        {
            // Arrange
            var line = $"PARAM\t12345678-1234-1234-1234-123456789012\t{paramName}\tTEXT\t\t1\t1\tDesc\t1\t0";
            var groups = new Dictionary<int, string> { { 1, "TestGroup" } };

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be(expectedDiscipline);
        }

        #endregion

        #region DataType Conversion Tests

        [Test]
        [TestCase("TEXT", "Text")]
        [TestCase("LENGTH", "Length")]
        [TestCase("AREA", "Area")]
        [TestCase("VOLUME", "Volume")]
        [TestCase("NUMBER", "Number")]
        [TestCase("INTEGER", "Integer")]
        [TestCase("YESNO", "YesNo")]
        [TestCase("ANGLE", "Angle")]
        [TestCase("CURRENCY", "Currency")]
        public void DataTypeConversion_VariousTypes_ConvertsCorrectly(string dataType, string expectedTypeName)
        {
            // Arrange
            var line = $"PARAM\t12345678-1234-1234-1234-123456789012\tTestParam\t{dataType}\t\t1\t1\tDesc\t1\t0";
            var groups = new Dictionary<int, string>();

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.RevitParameterType.ToString().Should().Be(expectedTypeName);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void IsValid_ValidParameter_ReturnsTrue()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TestParameter",
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var isValid = param.IsValid();

            // Assert
            isValid.Should().BeTrue();
        }

        [Test]
        public void IsValid_EmptyGuid_ReturnsFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.Empty,
                Name = "TestParameter",
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var isValid = param.IsValid();

            // Assert
            isValid.Should().BeFalse();
        }

        [Test]
        public void IsValid_EmptyName_ReturnsFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "",
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var isValid = param.IsValid();

            // Assert
            isValid.Should().BeFalse();
        }

        [Test]
        public void IsValid_NegativeGroupId_ReturnsFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TestParameter",
                DataType = "TEXT",
                GroupId = -1
            };

            // Act
            var isValid = param.IsValid();

            // Assert
            isValid.Should().BeFalse();
        }

        [Test]
        public void GetValidationErrors_MultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.Empty,
                Name = "",
                DataType = null,
                GroupId = -1
            };

            // Act
            var errors = param.GetValidationErrors();

            // Assert
            errors.Should().HaveCount(4);
            errors.Should().Contain("GUID cannot be empty");
            errors.Should().Contain("Name cannot be null or empty");
            errors.Should().Contain("DataType cannot be null or empty");
            errors.Should().Contain("GroupId must be non-negative");
        }

        #endregion

        #region Utility Method Tests

        [Test]
        public void GetDisplayName_WithUnderscores_ReplacesWithSpaces()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Name = "BLE_WALL_HEIGHT_MM"
            };

            // Act
            var displayName = param.GetDisplayName();

            // Assert
            displayName.Should().Be("BLE WALL HEIGHT MM");
        }

        [Test]
        public void GetDisplayName_EmptyName_ReturnsUnknownParameter()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Name = ""
            };

            // Act
            var displayName = param.GetDisplayName();

            // Assert
            displayName.Should().Be("Unknown Parameter");
        }

        [Test]
        public void BelongsToDiscipline_MatchingDiscipline_ReturnsTrue()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Discipline = "Electrical"
            };

            // Act
            var belongs = param.BelongsToDiscipline("electrical");

            // Assert
            belongs.Should().BeTrue();
        }

        [Test]
        public void BelongsToDiscipline_NonMatchingDiscipline_ReturnsFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Discipline = "Electrical"
            };

            // Act
            var belongs = param.BelongsToDiscipline("HVAC");

            // Assert
            belongs.Should().BeFalse();
        }

        [Test]
        public void Clone_CreatesIndependentCopy()
        {
            // Arrange
            var original = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "Original",
                DataType = "TEXT",
                Description = "Original description"
            };

            // Act
            var clone = original.Clone();
            clone.Name = "Modified";
            clone.Description = "Modified description";

            // Assert
            original.Name.Should().Be("Original");
            original.Description.Should().Be("Original description");
            clone.Name.Should().Be("Modified");
        }

        #endregion

        #region Equality Tests

        [Test]
        public void Equals_SameGuid_ReturnsTrue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var param1 = new ParameterDefinition { Guid = guid, Name = "Param1" };
            var param2 = new ParameterDefinition { Guid = guid, Name = "Param2" };

            // Act & Assert
            param1.Equals(param2).Should().BeTrue();
        }

        [Test]
        public void Equals_DifferentGuid_ReturnsFalse()
        {
            // Arrange
            var param1 = new ParameterDefinition { Guid = Guid.NewGuid(), Name = "Same" };
            var param2 = new ParameterDefinition { Guid = Guid.NewGuid(), Name = "Same" };

            // Act & Assert
            param1.Equals(param2).Should().BeFalse();
        }

        [Test]
        public void GetHashCode_SameGuid_ReturnsSameHash()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var param1 = new ParameterDefinition { Guid = guid };
            var param2 = new ParameterDefinition { Guid = guid };

            // Act & Assert
            param1.GetHashCode().Should().Be(param2.GetHashCode());
        }

        #endregion
    }

    /// <summary>
    /// Unit tests for ParameterDefinitionCollection operations.
    /// </summary>
    [TestFixture]
    public class ParameterDefinitionCollectionTests
    {
        private ParameterDefinitionCollection _collection;

        [SetUp]
        public void SetUp()
        {
            _collection = new ParameterDefinitionCollection();
        }

        [Test]
        public void Add_ValidParameter_IncreasesCount()
        {
            // Arrange
            var param = CreateTestParameter("Test", "Electrical", "ELC_Systems");

            // Act
            _collection.Add(param);

            // Assert
            _collection.Count.Should().Be(1);
        }

        [Test]
        public void Add_NullParameter_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _collection.Add(null));
        }

        [Test]
        public void GetByGuid_ExistingParameter_ReturnsParameter()
        {
            // Arrange
            var param = CreateTestParameter("Test", "Electrical", "ELC_Systems");
            _collection.Add(param);

            // Act
            var retrieved = _collection.GetByGuid(param.Guid);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Name.Should().Be("Test");
        }

        [Test]
        public void GetByGuid_NonExistingParameter_ReturnsNull()
        {
            // Act
            var retrieved = _collection.GetByGuid(Guid.NewGuid());

            // Assert
            retrieved.Should().BeNull();
        }

        [Test]
        public void GetByName_ExistingParameter_ReturnsParameter()
        {
            // Arrange
            var param = CreateTestParameter("BLE_WALL_HEIGHT", "Building Elements", "BLE_ELES");
            _collection.Add(param);

            // Act
            var retrieved = _collection.GetByName("BLE_WALL_HEIGHT");

            // Assert
            retrieved.Should().NotBeNull();
        }

        [Test]
        public void GetByName_CaseInsensitive_ReturnsParameter()
        {
            // Arrange
            var param = CreateTestParameter("BLE_WALL_HEIGHT", "Building Elements", "BLE_ELES");
            _collection.Add(param);

            // Act
            var retrieved = _collection.GetByName("ble_wall_height");

            // Assert
            retrieved.Should().NotBeNull();
        }

        [Test]
        public void GetByDiscipline_MultipleParameters_ReturnsFilteredList()
        {
            // Arrange
            _collection.Add(CreateTestParameter("ELC_CABLE_SIZE", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("ELC_VOLTAGE", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("HVC_DUCT_SIZE", "HVAC", "HVC_Systems"));

            // Act
            var electricalParams = _collection.GetByDiscipline("Electrical");

            // Assert
            electricalParams.Should().HaveCount(2);
            electricalParams.Should().OnlyContain(p => p.Discipline == "Electrical");
        }

        [Test]
        public void GetBySystem_ReturnsFilteredList()
        {
            // Arrange
            _collection.Add(CreateTestParameter("Param1", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("Param2", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("Param3", "Electrical", "ELC_Power"));

            // Act
            var systemParams = _collection.GetBySystem("ELC_Systems");

            // Assert
            systemParams.Should().HaveCount(2);
        }

        [Test]
        public void GetAll_ReturnsAllParameters()
        {
            // Arrange
            _collection.Add(CreateTestParameter("Param1", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("Param2", "HVAC", "HVC_Systems"));
            _collection.Add(CreateTestParameter("Param3", "Plumbing", "PLM_Systems"));

            // Act
            var allParams = _collection.GetAll();

            // Assert
            allParams.Should().HaveCount(3);
        }

        [Test]
        public void GetDisciplines_ReturnsUniqueList()
        {
            // Arrange
            _collection.Add(CreateTestParameter("Param1", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("Param2", "Electrical", "ELC_Power"));
            _collection.Add(CreateTestParameter("Param3", "HVAC", "HVC_Systems"));

            // Act
            var disciplines = _collection.GetDisciplines();

            // Assert
            disciplines.Should().HaveCount(2);
            disciplines.Should().Contain("Electrical");
            disciplines.Should().Contain("HVAC");
        }

        [Test]
        public void GetSystems_ReturnsUniqueList()
        {
            // Arrange
            _collection.Add(CreateTestParameter("Param1", "Electrical", "ELC_Systems"));
            _collection.Add(CreateTestParameter("Param2", "Electrical", "ELC_Power"));
            _collection.Add(CreateTestParameter("Param3", "HVAC", "HVC_Systems"));

            // Act
            var systems = _collection.GetSystems();

            // Assert
            systems.Should().HaveCount(3);
        }

        private ParameterDefinition CreateTestParameter(string name, string discipline, string system)
        {
            return new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = name,
                DataType = "TEXT",
                Discipline = discipline,
                System = system,
                GroupId = 1
            };
        }
    }
}
