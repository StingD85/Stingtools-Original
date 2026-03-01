using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Parameters;
using StingBIM.Data.Schedules;

namespace StingBIM.AI.Tests.Integration.RevitMocked
{
    /// <summary>
    /// Tier 2 — Integration tests for ParameterValidator, ScheduleTemplate,
    /// and other Foundation classes that work without a live Revit Document.
    /// TransactionManager itself requires Revit (sealed Document) and is tested in Tier 3.
    /// </summary>
    [TestFixture]
    public class FoundationIntegrationTests
    {
        #region ParameterValidator Tests

        [TestFixture]
        public class ParameterValidatorTests
        {
            private ParameterValidator _validator;

            [SetUp]
            public void SetUp()
            {
                _validator = ParameterValidator.CreateStandalone();
            }

            // --- ValidateDefinition ---

            [Test]
            public void ValidateDefinition_ValidParameter_ReturnsSuccess()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "Wall_Length",
                    DataType = "LENGTH",
                    GroupName = "Dimensions_Group",
                    GroupId = 1,
                    Description = "ISO 19650 compliant wall length parameter"
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ValidateDefinition_NullParameter_ReturnsFailure()
            {
                var result = _validator.ValidateDefinition(null);

                result.IsValid.Should().BeFalse();
                result.Errors.Should().NotBeEmpty();
            }

            [Test]
            public void ValidateDefinition_EmptyGuid_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.Empty,
                    Name = "Test_Param",
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = 1,
                    Description = "Test"
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("GUID");
            }

            [Test]
            public void ValidateDefinition_EmptyName_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "",
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = 1
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("name");
            }

            [Test]
            public void ValidateDefinition_InvalidCharactersInName_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "Wall/Length",
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = 1
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("invalid characters");
            }

            [Test]
            public void ValidateDefinition_NameTooLong_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = new string('A', 256),
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = 1
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("maximum length");
            }

            [Test]
            public void ValidateDefinition_EmptyDataType_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "Test_Param",
                    DataType = "",
                    GroupName = "Test_Group",
                    GroupId = 1
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
            }

            [Test]
            public void ValidateDefinition_NegativeGroupId_ReturnsError()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "Test_Param",
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = -1
                };

                var result = _validator.ValidateDefinition(param);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("group ID");
            }

            [Test]
            public void ValidateDefinition_NoDescription_ReturnsWarning()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "Test_Param",
                    DataType = "TEXT",
                    GroupName = "Test_Group",
                    GroupId = 1,
                    Description = ""
                };

                var result = _validator.ValidateDefinition(param);

                result.HasWarnings.Should().BeTrue();
            }

            [Test]
            public void ValidateDefinition_NameStartsWithNumber_ReturnsWarning()
            {
                var param = new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "1st_Floor_Area",
                    DataType = "AREA",
                    GroupName = "Test_Group",
                    GroupId = 1,
                    Description = "ISO 19650 area"
                };

                var result = _validator.ValidateDefinition(param);

                // Valid, but with a warning about naming
                result.HasWarnings.Should().BeTrue();
            }

            // --- ValidateDefinitions (Batch) ---

            [Test]
            public void ValidateDefinitions_NullCollection_ReturnsFailure()
            {
                var result = _validator.ValidateDefinitions(null);

                result.IsValid.Should().BeFalse();
            }

            [Test]
            public void ValidateDefinitions_DuplicateGuids_ReturnsError()
            {
                var sharedGuid = Guid.NewGuid();
                var parameters = new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Guid = sharedGuid, Name = "Param_A", DataType = "TEXT",
                        GroupName = "Test_Group", GroupId = 1, Description = "ISO 19650 A"
                    },
                    new ParameterDefinition
                    {
                        Guid = sharedGuid, Name = "Param_B", DataType = "TEXT",
                        GroupName = "Test_Group", GroupId = 1, Description = "ISO 19650 B"
                    }
                };

                var result = _validator.ValidateDefinitions(parameters);

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("Duplicate GUID");
            }

            [Test]
            public void ValidateDefinitions_DuplicateNames_ReturnsWarning()
            {
                var parameters = new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Guid = Guid.NewGuid(), Name = "Wall_Length", DataType = "LENGTH",
                        GroupName = "Test_Group", GroupId = 1, Description = "ISO 19650"
                    },
                    new ParameterDefinition
                    {
                        Guid = Guid.NewGuid(), Name = "wall_length", DataType = "LENGTH",
                        GroupName = "Test_Group", GroupId = 1, Description = "ISO 19650"
                    }
                };

                var result = _validator.ValidateDefinitions(parameters);

                result.HasWarnings.Should().BeTrue();
                result.WarningMessage.Should().Contain("Duplicate parameter name");
            }

            // --- ValidateValue ---

            [Test]
            public void ValidateValue_TextParameter_StringValue_Succeeds()
            {
                var param = new ParameterDefinition { Name = "Room_Name", DataType = "TEXT" };

                var result = _validator.ValidateValue(param, "Living Room");

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_TextParameter_NonStringValue_Fails()
            {
                var param = new ParameterDefinition { Name = "Room_Name", DataType = "TEXT" };

                var result = _validator.ValidateValue(param, 42);

                result.IsValid.Should().BeFalse();
            }

            [Test]
            public void ValidateValue_IntegerParameter_IntValue_Succeeds()
            {
                var param = new ParameterDefinition { Name = "Floor_Number", DataType = "INTEGER" };

                var result = _validator.ValidateValue(param, 3);

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_NumberParameter_DoubleValue_Succeeds()
            {
                var param = new ParameterDefinition { Name = "Ratio", DataType = "NUMBER" };

                var result = _validator.ValidateValue(param, 3.14);

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_LengthParameter_NegativeValue_ReturnsWarning()
            {
                var param = new ParameterDefinition { Name = "Wall_Length", DataType = "LENGTH" };

                var result = _validator.ValidateValue(param, -5.0);

                result.HasWarnings.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_YesNoParameter_BoolValue_Succeeds()
            {
                var param = new ParameterDefinition { Name = "Is_Structural", DataType = "YESNO" };

                var result = _validator.ValidateValue(param, true);

                result.IsValid.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_ElectricalParameter_NegativeValue_Fails()
            {
                var param = new ParameterDefinition { Name = "Circuit_Current", DataType = "ELECTRICAL_CURRENT" };

                var result = _validator.ValidateValue(param, -10.0);

                result.IsValid.Should().BeFalse();
            }

            [Test]
            public void ValidateValue_NullValue_ReturnsWarningWhenVisible()
            {
                var param = new ParameterDefinition
                {
                    Name = "Test_Param",
                    DataType = "TEXT",
                    HideWhenNoValue = false
                };

                var result = _validator.ValidateValue(param, null);

                result.IsValid.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
            }

            [Test]
            public void ValidateValue_CurrencyParameter_DecimalValue_Succeeds()
            {
                var param = new ParameterDefinition { Name = "Cost", DataType = "CURRENCY" };

                var result = _validator.ValidateValue(param, 1500.50m);

                result.IsValid.Should().BeTrue();
            }

            // --- Custom Rules ---

            [Test]
            public void AddRule_NullRule_ThrowsArgumentNullException()
            {
                Action act = () => _validator.AddRule(null);

                act.Should().Throw<ArgumentNullException>();
            }

            [Test]
            public void GetRules_AfterInit_HasDefaultRules()
            {
                var rules = _validator.GetRules();

                rules.Should().NotBeEmpty();
                rules.Should().Contain(r => r.Name == "ISO19650Compliance");
                rules.Should().Contain(r => r.Name == "GroupNamingConvention");
            }

            [Test]
            public void RemoveRule_ExistingRule_ReturnsTrue()
            {
                var result = _validator.RemoveRule("ISO19650Compliance");

                result.Should().BeTrue();
                _validator.GetRules().Should().NotContain(r => r.Name == "ISO19650Compliance");
            }

            [Test]
            public void RemoveRule_NonExistentRule_ReturnsFalse()
            {
                var result = _validator.RemoveRule("NonExistentRule");

                result.Should().BeFalse();
            }

            [Test]
            public void ClearRules_RestoresDefaults()
            {
                _validator.AddRule(new ValidationRule(
                    "CustomRule",
                    p => true,
                    p => StingBIM.Data.Parameters.ValidationResult.Success()));

                _validator.ClearRules();

                var rules = _validator.GetRules();
                rules.Should().NotContain(r => r.Name == "CustomRule");
                rules.Should().Contain(r => r.Name == "ISO19650Compliance");
            }

            [Test]
            public void ValidateExistsInDocument_NoDocument_ReturnsFailure()
            {
                var result = _validator.ValidateExistsInDocument(Guid.NewGuid());

                result.IsValid.Should().BeFalse();
                result.ErrorMessage.Should().Contain("No document context");
            }
        }

        #endregion

        #region ScheduleTemplate Tests

        [TestFixture]
        public class ScheduleTemplateTests
        {
            [Test]
            public void Constructor_ValidArgs_CreatesTemplate()
            {
                var fields = new List<ScheduleFieldDefinition>
                {
                    new ScheduleFieldDefinition("Mark", 0),
                    new ScheduleFieldDefinition("Type", 1)
                };

                var template = new ScheduleTemplate("Door Schedule", "Doors", ScheduleType.Standard, fields);

                template.Name.Should().Be("Door Schedule");
                template.CategoryName.Should().Be("Doors");
                template.Type.Should().Be(ScheduleType.Standard);
                template.Fields.Should().HaveCount(2);
            }

            [Test]
            public void Constructor_NullName_ThrowsArgumentException()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Mark", 0) };

                Action act = () => new ScheduleTemplate(null, "Doors", ScheduleType.Standard, fields);

                act.Should().Throw<ArgumentException>();
            }

            [Test]
            public void Constructor_EmptyFields_ThrowsArgumentException()
            {
                Action act = () => new ScheduleTemplate("Test", "Doors", ScheduleType.Standard, new List<ScheduleFieldDefinition>());

                act.Should().Throw<ArgumentException>();
            }

            [Test]
            public void ExtractDiscipline_MEPName_ReturnsMEP()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Size", 0) };
                var template = new ScheduleTemplate("MEP Duct Schedule", "Ducts", ScheduleType.Standard, fields);

                template.Discipline.Should().Be("MEP");
            }

            [Test]
            public void ExtractDiscipline_ArchName_ReturnsArchitecture()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Name", 0) };
                var template = new ScheduleTemplate("Room Schedule", "Rooms", ScheduleType.Standard, fields);

                template.Discipline.Should().Be("Architecture");
            }

            [Test]
            public void ExtractDiscipline_StructName_ReturnsStructural()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Size", 0) };
                var template = new ScheduleTemplate("Structural Framing Schedule", "Framing", ScheduleType.Standard, fields);

                template.Discipline.Should().Be("Structural");
            }

            [Test]
            public void ExtractDiscipline_GenericName_ReturnsGeneral()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Name", 0) };
                var template = new ScheduleTemplate("Asset Register", "Assets", ScheduleType.Standard, fields);

                template.Discipline.Should().Be("General");
            }

            [Test]
            public void GetReferencedParameters_IncludesFieldsFiltersSorting()
            {
                var fields = new List<ScheduleFieldDefinition>
                {
                    new ScheduleFieldDefinition("Mark", 0),
                    new ScheduleFieldDefinition("Type", 1)
                };
                var filters = new List<ScheduleFilterDefinition>
                {
                    new ScheduleFilterDefinition("Level", ScheduleFilterType.Equal, "Level 1")
                };
                var sorting = new List<ScheduleSortDefinition>
                {
                    new ScheduleSortDefinition("Mark", true, 0)
                };

                var template = new ScheduleTemplate("Test", "Doors", ScheduleType.Standard,
                    fields, filters, sorting);

                var refs = template.GetReferencedParameters();

                refs.Should().Contain("Mark");
                refs.Should().Contain("Type");
                refs.Should().Contain("Level");
            }

            [Test]
            public void HasFilters_WithFilters_ReturnsTrue()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Mark", 0) };
                var filters = new List<ScheduleFilterDefinition>
                {
                    new ScheduleFilterDefinition("Level", ScheduleFilterType.Equal, "Level 1")
                };

                var template = new ScheduleTemplate("Test", "Doors", ScheduleType.Standard, fields, filters);

                template.HasFilters.Should().BeTrue();
                template.HasSorting.Should().BeFalse();
                template.HasGrouping.Should().BeFalse();
            }

            [Test]
            public void Equality_SameName_AreEqual()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Mark", 0) };
                var t1 = new ScheduleTemplate("Door Schedule", "Doors", ScheduleType.Standard, fields);
                var t2 = new ScheduleTemplate("door schedule", "Windows", ScheduleType.MaterialTakeoff, fields);

                t1.Should().Be(t2, "equality is based on name (case-insensitive)");
                t1.GetHashCode().Should().Be(t2.GetHashCode());
            }

            [Test]
            public void Equality_DifferentName_AreNotEqual()
            {
                var fields = new List<ScheduleFieldDefinition> { new ScheduleFieldDefinition("Mark", 0) };
                var t1 = new ScheduleTemplate("Door Schedule", "Doors", ScheduleType.Standard, fields);
                var t2 = new ScheduleTemplate("Window Schedule", "Windows", ScheduleType.Standard, fields);

                t1.Should().NotBe(t2);
            }

            [Test]
            public void ToString_ReturnsNameAndFieldCount()
            {
                var fields = new List<ScheduleFieldDefinition>
                {
                    new ScheduleFieldDefinition("Mark", 0),
                    new ScheduleFieldDefinition("Type", 1),
                    new ScheduleFieldDefinition("Width", 2)
                };
                var template = new ScheduleTemplate("Door Schedule", "Doors", ScheduleType.Standard, fields);

                template.ToString().Should().Contain("Door Schedule");
                template.ToString().Should().Contain("3 fields");
            }

            [Test]
            public void FromCsvLine_ValidLine_ParsesCorrectly()
            {
                var line = "Door Schedule,Doors,Mark;Type;Width";

                var template = ScheduleTemplate.FromCsvLine(line);

                template.Name.Should().Be("Door Schedule");
                template.CategoryName.Should().Be("Doors");
                template.Fields.Should().HaveCount(3);
                template.Fields[0].ParameterName.Should().Be("Mark");
                template.Fields[1].ParameterName.Should().Be("Type");
                template.Fields[2].ParameterName.Should().Be("Width");
            }

            [Test]
            public void FromCsvLine_WithFilters_ParsesFilters()
            {
                var line = "Door Schedule,Doors,Mark;Type,Level=Level 1";

                var template = ScheduleTemplate.FromCsvLine(line);

                template.HasFilters.Should().BeTrue();
                template.Filters[0].ParameterName.Should().Be("Level");
                template.Filters[0].FilterType.Should().Be(ScheduleFilterType.Equal);
                template.Filters[0].Value.Should().Be("Level 1");
            }

            [Test]
            public void FromCsvLine_WithSorting_ParsesSorting()
            {
                var line = "Door Schedule,Doors,Mark;Type,,Mark ASC;Type DESC";

                var template = ScheduleTemplate.FromCsvLine(line);

                template.HasSorting.Should().BeTrue();
                template.Sorting[0].ParameterName.Should().Be("Mark");
                template.Sorting[0].Ascending.Should().BeTrue();
                template.Sorting[1].ParameterName.Should().Be("Type");
                template.Sorting[1].Ascending.Should().BeFalse();
            }

            [Test]
            public void FromCsvLine_TooFewColumns_ThrowsArgumentException()
            {
                Action act = () => ScheduleTemplate.FromCsvLine("OnlyOneColumn");

                act.Should().Throw<ArgumentException>();
            }
        }

        #endregion

        #region ScheduleTemplateBuilder Tests

        [TestFixture]
        public class ScheduleTemplateBuilderTests
        {
            [Test]
            public void Builder_FluentAPI_CreatesTemplate()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Door Schedule")
                    .ForCategory("Doors")
                    .WithType(ScheduleType.Standard)
                    .AddField("Mark")
                    .AddField("Type")
                    .AddField("Width")
                    .Build();

                template.Name.Should().Be("Door Schedule");
                template.CategoryName.Should().Be("Doors");
                template.Fields.Should().HaveCount(3);
            }

            [Test]
            public void Builder_WithFilterAndSorting_CreatesTemplate()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Room Schedule")
                    .ForCategory("Rooms")
                    .AddField("Name")
                    .AddField("Area")
                    .AddFilter("Level", ScheduleFilterType.Equal, "Level 1")
                    .AddSorting("Name")
                    .Build();

                template.HasFilters.Should().BeTrue();
                template.HasSorting.Should().BeTrue();
            }

            [Test]
            public void Builder_WithGrouping_CreatesTemplate()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Wall Schedule")
                    .ForCategory("Walls")
                    .AddField("Type")
                    .AddField("Length")
                    .AddGrouping("Type")
                    .Build();

                template.HasGrouping.Should().BeTrue();
            }

            [Test]
            public void Builder_WithDiscipline_OverridesAutoDetection()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Custom Schedule")
                    .ForCategory("Custom")
                    .AddField("Field1")
                    .WithDiscipline("MEP")
                    .Build();

                template.Discipline.Should().Be("MEP");
            }

            [Test]
            public void Builder_WithMetadata_SetsMetadata()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Test Schedule")
                    .ForCategory("Walls")
                    .AddField("Type")
                    .AddMetadata("Author", "StingBIM")
                    .AddMetadata("Version", "7.0")
                    .Build();

                template.Metadata.Should().ContainKey("Author");
                template.Metadata["Author"].Should().Be("StingBIM");
            }

            [Test]
            public void Builder_WithFormatting_ConfiguresFormatting()
            {
                var template = ScheduleTemplate.Builder()
                    .WithName("Formatted Schedule")
                    .ForCategory("Doors")
                    .AddField("Mark")
                    .WithFormatting(f =>
                    {
                        f.BoldHeaders = true;
                        f.TextSize = 4.0;
                        f.FontName = "Calibri";
                    })
                    .Build();

                template.Formatting.BoldHeaders.Should().BeTrue();
                template.Formatting.TextSize.Should().Be(4.0);
                template.Formatting.FontName.Should().Be("Calibri");
            }
        }

        #endregion

        #region ScheduleFieldDefinition Tests

        [TestFixture]
        public class ScheduleFieldDefinitionTests
        {
            [Test]
            public void Constructor_SetsDefaults()
            {
                var field = new ScheduleFieldDefinition("Mark", 0);

                field.ParameterName.Should().Be("Mark");
                field.DisplayOrder.Should().Be(0);
                field.ShowHeader.Should().BeTrue();
                field.Width.Should().Be(-1, "default is auto width");
                field.IsCalculatedValue.Should().BeFalse();
            }

            [Test]
            public void Constructor_NullParameterName_ThrowsArgumentNullException()
            {
                Action act = () => new ScheduleFieldDefinition(null, 0);

                act.Should().Throw<ArgumentNullException>();
            }
        }

        #endregion

        #region ScheduleFilterDefinition Tests

        [TestFixture]
        public class ScheduleFilterDefinitionTests
        {
            [Test]
            public void Constructor_SetsProperties()
            {
                var filter = new ScheduleFilterDefinition("Level", ScheduleFilterType.Equal, "Level 1");

                filter.ParameterName.Should().Be("Level");
                filter.FilterType.Should().Be(ScheduleFilterType.Equal);
                filter.Value.Should().Be("Level 1");
            }

            [Test]
            public void Constructor_NullValue_DefaultsToEmpty()
            {
                var filter = new ScheduleFilterDefinition("Level", ScheduleFilterType.Equal, null);

                filter.Value.Should().BeEmpty();
            }
        }

        #endregion

        #region ScheduleGroupDefinition Tests

        [TestFixture]
        public class ScheduleGroupDefinitionTests
        {
            [Test]
            public void Constructor_DefaultValues_SetsCorrectly()
            {
                var group = new ScheduleGroupDefinition("Type");

                group.ParameterName.Should().Be("Type");
                group.ShowHeader.Should().BeTrue();
                group.ShowFooter.Should().BeFalse();
                group.ShowBlankLine.Should().BeTrue();
            }

            [Test]
            public void Constructor_CustomValues_SetsCorrectly()
            {
                var group = new ScheduleGroupDefinition("Level", showHeader: false, showFooter: true, showBlankLine: false);

                group.ShowHeader.Should().BeFalse();
                group.ShowFooter.Should().BeTrue();
                group.ShowBlankLine.Should().BeFalse();
            }
        }

        #endregion

        #region ScheduleFormatting Tests

        [TestFixture]
        public class ScheduleFormattingTests
        {
            [Test]
            public void Constructor_SetsDefaults()
            {
                var formatting = new ScheduleFormatting();

                formatting.BoldHeaders.Should().BeTrue();
                formatting.TextSize.Should().Be(3.0);
                formatting.FontName.Should().Be("Arial");
                formatting.HeaderColor.Should().BeNull();
                formatting.TextColor.Should().BeNull();
                formatting.AlternatingRowColor.Should().BeNull();
            }
        }

        #endregion
    }
}
