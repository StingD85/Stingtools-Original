using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Data.Parameters;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Unit tests for ParameterDefinition class.
    /// Tests parsing, validation, and utility methods.
    /// </summary>
    [TestFixture]
    public class ParameterDefinitionTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_ShouldInitializeDefaults()
        {
            // Act
            var param = new ParameterDefinition();

            // Assert
            param.BoundCategories.Should().NotBeNull();
            param.BoundCategories.Should().BeEmpty();
            param.IsVisible.Should().BeTrue();
            param.IsUserModifiable.Should().BeTrue();
            param.HideWhenNoValue.Should().BeFalse();
        }

        #endregion

        #region Validation Tests

        [Test]
        public void IsValid_ValidParameter_ShouldReturnTrue()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "BLE_WALL_HEIGHT_MM",
                DataType = "LENGTH",
                GroupId = 1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void IsValid_EmptyGuid_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.Empty,
                Name = "TestParam",
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsValid_NullName_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = null,
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsValid_EmptyName_ShouldReturnFalse()
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
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsValid_WhitespaceName_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "   ",
                DataType = "TEXT",
                GroupId = 1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsValid_NullDataType_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TestParam",
                DataType = null,
                GroupId = 1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsValid_NegativeGroupId_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TestParam",
                DataType = "TEXT",
                GroupId = -1
            };

            // Act
            var result = param.IsValid();

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void GetValidationErrors_ValidParameter_ShouldReturnEmpty()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "BLE_WALL_HEIGHT_MM",
                DataType = "LENGTH",
                GroupId = 1
            };

            // Act
            var errors = param.GetValidationErrors();

            // Assert
            errors.Should().BeEmpty();
        }

        [Test]
        public void GetValidationErrors_MultipleErrors_ShouldReturnAll()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Guid = Guid.Empty,
                Name = null,
                DataType = null,
                GroupId = -1
            };

            // Act
            var errors = param.GetValidationErrors();

            // Assert
            errors.Should().HaveCount(4);
            errors.Should().Contain(e => e.Contains("GUID"));
            errors.Should().Contain(e => e.Contains("Name"));
            errors.Should().Contain(e => e.Contains("DataType"));
            errors.Should().Contain(e => e.Contains("GroupId"));
        }

        #endregion

        #region FromSharedParameterLine Tests

        [Test]
        public void FromSharedParameterLine_ValidLine_ShouldParse()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string> { { 1, "BLE_ELES" } };
            var line = $"PARAM\t{guid}\tBLE_WALL_HEIGHT_MM\tLENGTH\t\t1\t1\tWall height in millimeters\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Should().NotBeNull();
            param.Guid.Should().Be(guid);
            param.Name.Should().Be("BLE_WALL_HEIGHT_MM");
            param.DataType.Should().Be("LENGTH");
            param.GroupId.Should().Be(1);
            param.IsVisible.Should().BeTrue();
            param.Description.Should().Be("Wall height in millimeters");
            param.IsUserModifiable.Should().BeTrue();
            param.HideWhenNoValue.Should().BeFalse();
        }

        [Test]
        public void FromSharedParameterLine_NullLine_ShouldThrow()
        {
            // Arrange
            var groups = new Dictionary<int, string>();

            // Act
            Action act = () => ParameterDefinition.FromSharedParameterLine(null, groups);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void FromSharedParameterLine_EmptyLine_ShouldThrow()
        {
            // Arrange
            var groups = new Dictionary<int, string>();

            // Act
            Action act = () => ParameterDefinition.FromSharedParameterLine("", groups);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void FromSharedParameterLine_LineNotStartingWithPARAM_ShouldThrow()
        {
            // Arrange
            var groups = new Dictionary<int, string>();
            var line = "GROUP\t1\tTest Group";

            // Act
            Action act = () => ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*PARAM*");
        }

        [Test]
        public void FromSharedParameterLine_InsufficientParts_ShouldThrow()
        {
            // Arrange
            var groups = new Dictionary<int, string>();
            var line = "PARAM\tguid\tname"; // Only 3 parts

            // Act
            Action act = () => ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*10 parts*");
        }

        [Test]
        public void FromSharedParameterLine_InvalidGuid_ShouldThrow()
        {
            // Arrange
            var groups = new Dictionary<int, string>();
            var line = "PARAM\tinvalid-guid\tTestParam\tTEXT\t\t1\t1\tDescription\t1\t0";

            // Act
            Action act = () => ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void FromSharedParameterLine_BooleanParsing_ZeroIsFalse()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tTestParam\tTEXT\t\t1\t0\tDescription\t0\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.IsVisible.Should().BeFalse();
            param.IsUserModifiable.Should().BeFalse();
            param.HideWhenNoValue.Should().BeFalse();
        }

        [Test]
        public void FromSharedParameterLine_BooleanParsing_OneIsTrue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tTestParam\tTEXT\t\t1\t1\tDescription\t1\t1";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.IsVisible.Should().BeTrue();
            param.IsUserModifiable.Should().BeTrue();
            param.HideWhenNoValue.Should().BeTrue();
        }

        #endregion

        #region Discipline Extraction Tests

        [Test]
        public void FromSharedParameterLine_BLE_Prefix_ShouldExtractBuildingElements()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tBLE_WALL_HEIGHT\tLENGTH\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("Building Elements");
        }

        [Test]
        public void FromSharedParameterLine_ELC_Prefix_ShouldExtractElectrical()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tELC_CABLE_SIZE\tNUMBER\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("Electrical");
        }

        [Test]
        public void FromSharedParameterLine_HVC_Prefix_ShouldExtractHVAC()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tHVC_DUCT_SIZE\tNUMBER\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("HVAC");
        }

        [Test]
        public void FromSharedParameterLine_PLM_Prefix_ShouldExtractPlumbing()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tPLM_PIPE_SIZE\tNUMBER\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("Plumbing");
        }

        [Test]
        public void FromSharedParameterLine_STR_Prefix_ShouldExtractStructural()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tSTR_BEAM_SIZE\tNUMBER\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("Structural");
        }

        [Test]
        public void FromSharedParameterLine_UnknownPrefix_ShouldExtractGeneral()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var groups = new Dictionary<int, string>();
            var line = $"PARAM\t{guid}\tUNKNOWN_PARAM\tTEXT\t\t1\t1\tDescription\t1\t0";

            // Act
            var param = ParameterDefinition.FromSharedParameterLine(line, groups);

            // Assert
            param.Discipline.Should().Be("General");
        }

        #endregion

        #region Utility Method Tests

        [Test]
        public void GetDisplayName_ShouldReplaceUnderscoresWithSpaces()
        {
            // Arrange
            var param = new ParameterDefinition { Name = "BLE_WALL_HEIGHT_MM" };

            // Act
            var displayName = param.GetDisplayName();

            // Assert
            displayName.Should().Be("BLE WALL HEIGHT MM");
        }

        [Test]
        public void GetDisplayName_NullName_ShouldReturnDefault()
        {
            // Arrange
            var param = new ParameterDefinition { Name = null };

            // Act
            var displayName = param.GetDisplayName();

            // Assert
            displayName.Should().Be("Unknown Parameter");
        }

        [Test]
        public void GetDisplayName_EmptyName_ShouldReturnDefault()
        {
            // Arrange
            var param = new ParameterDefinition { Name = "" };

            // Act
            var displayName = param.GetDisplayName();

            // Assert
            displayName.Should().Be("Unknown Parameter");
        }

        [Test]
        public void BelongsToDiscipline_MatchingDiscipline_ShouldReturnTrue()
        {
            // Arrange
            var param = new ParameterDefinition { Discipline = "Electrical" };

            // Act
            var result = param.BelongsToDiscipline("Electrical");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void BelongsToDiscipline_CaseInsensitive_ShouldReturnTrue()
        {
            // Arrange
            var param = new ParameterDefinition { Discipline = "Electrical" };

            // Act
            var result = param.BelongsToDiscipline("electrical");

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void BelongsToDiscipline_NonMatchingDiscipline_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition { Discipline = "Electrical" };

            // Act
            var result = param.BelongsToDiscipline("Plumbing");

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void BelongsToDiscipline_NullDiscipline_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition { Discipline = null };

            // Act
            var result = param.BelongsToDiscipline("Electrical");

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TestParam",
                DataType = "TEXT",
                Discipline = "Electrical",
                GroupId = 1,
                Description = "Test description"
            };

            // Act
            var clone = original.Clone();

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.Guid.Should().Be(original.Guid);
            clone.Name.Should().Be(original.Name);
            clone.DataType.Should().Be(original.DataType);
            clone.Discipline.Should().Be(original.Discipline);
            clone.BoundCategories.Should().NotBeSameAs(original.BoundCategories);
        }

        #endregion

        #region Equality Tests

        [Test]
        public void Equals_SameGuid_ShouldReturnTrue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var param1 = new ParameterDefinition { Guid = guid };
            var param2 = new ParameterDefinition { Guid = guid };

            // Act
            var result = param1.Equals(param2);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void Equals_DifferentGuid_ShouldReturnFalse()
        {
            // Arrange
            var param1 = new ParameterDefinition { Guid = Guid.NewGuid() };
            var param2 = new ParameterDefinition { Guid = Guid.NewGuid() };

            // Act
            var result = param1.Equals(param2);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void Equals_Null_ShouldReturnFalse()
        {
            // Arrange
            var param = new ParameterDefinition { Guid = Guid.NewGuid() };

            // Act
            var result = param.Equals(null);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void GetHashCode_SameGuid_ShouldBeSame()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var param1 = new ParameterDefinition { Guid = guid };
            var param2 = new ParameterDefinition { Guid = guid };

            // Act & Assert
            param1.GetHashCode().Should().Be(param2.GetHashCode());
        }

        [Test]
        public void ToString_ShouldContainNameAndDataType()
        {
            // Arrange
            var param = new ParameterDefinition
            {
                Name = "TestParam",
                DataType = "TEXT",
                Description = "Test description"
            };

            // Act
            var result = param.ToString();

            // Assert
            result.Should().Contain("TestParam");
            result.Should().Contain("TEXT");
        }

        #endregion
    }
}
