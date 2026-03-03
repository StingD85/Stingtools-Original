using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Data.Parameters;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Unit tests for ParameterDefinitionCollection class.
    /// Tests indexing, lookup, and filtering functionality.
    /// </summary>
    [TestFixture]
    public class ParameterDefinitionCollectionTests
    {
        private ParameterDefinitionCollection _collection;
        private List<ParameterDefinition> _testParameters;

        [SetUp]
        public void Setup()
        {
            _collection = new ParameterDefinitionCollection();
            _testParameters = CreateTestParameters();

            foreach (var param in _testParameters)
            {
                _collection.Add(param);
            }
        }

        private List<ParameterDefinition> CreateTestParameters()
        {
            return new List<ParameterDefinition>
            {
                new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "BLE_WALL_HEIGHT_MM",
                    DataType = "LENGTH",
                    Discipline = "Building Elements",
                    System = "Walls",
                    GroupId = 1
                },
                new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "BLE_WALL_WIDTH_MM",
                    DataType = "LENGTH",
                    Discipline = "Building Elements",
                    System = "Walls",
                    GroupId = 1
                },
                new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "ELC_CABLE_SIZE_MM",
                    DataType = "NUMBER",
                    Discipline = "Electrical",
                    System = "Power",
                    GroupId = 2
                },
                new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "HVC_DUCT_SIZE_MM",
                    DataType = "NUMBER",
                    Discipline = "HVAC",
                    System = "Ducts",
                    GroupId = 3
                },
                new ParameterDefinition
                {
                    Guid = Guid.NewGuid(),
                    Name = "PLM_PIPE_DIAMETER_MM",
                    DataType = "LENGTH",
                    Discipline = "Plumbing",
                    System = "Pipes",
                    GroupId = 4
                }
            };
        }

        #region Count Tests

        [Test]
        public void Count_WithTestData_ShouldReturnCorrectCount()
        {
            // Assert
            _collection.Count.Should().Be(5);
        }

        [Test]
        public void Count_EmptyCollection_ShouldReturnZero()
        {
            // Arrange
            var emptyCollection = new ParameterDefinitionCollection();

            // Assert
            emptyCollection.Count.Should().Be(0);
        }

        #endregion

        #region Add Tests

        [Test]
        public void Add_NullParameter_ShouldThrow()
        {
            // Act
            Action act = () => _collection.Add(null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Add_ValidParameter_ShouldIncrementCount()
        {
            // Arrange
            var initialCount = _collection.Count;
            var newParam = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "NEW_PARAM",
                DataType = "TEXT",
                Discipline = "General"
            };

            // Act
            _collection.Add(newParam);

            // Assert
            _collection.Count.Should().Be(initialCount + 1);
        }

        [Test]
        public void Add_DuplicateGuid_ShouldOverwrite()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var param1 = new ParameterDefinition { Guid = guid, Name = "First" };
            var param2 = new ParameterDefinition { Guid = guid, Name = "Second" };
            var collection = new ParameterDefinitionCollection();

            // Act
            collection.Add(param1);
            collection.Add(param2);

            // Assert
            var retrieved = collection.GetByGuid(guid);
            retrieved.Name.Should().Be("Second");
        }

        #endregion

        #region GetByGuid Tests

        [Test]
        public void GetByGuid_ExistingGuid_ShouldReturnParameter()
        {
            // Arrange
            var expectedParam = _testParameters[0];

            // Act
            var result = _collection.GetByGuid(expectedParam.Guid);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedParam);
        }

        [Test]
        public void GetByGuid_NonExistingGuid_ShouldReturnNull()
        {
            // Act
            var result = _collection.GetByGuid(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GetByGuid_EmptyGuid_ShouldReturnNull()
        {
            // Act
            var result = _collection.GetByGuid(Guid.Empty);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetByName Tests

        [Test]
        public void GetByName_ExistingName_ShouldReturnParameter()
        {
            // Act
            var result = _collection.GetByName("BLE_WALL_HEIGHT_MM");

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("BLE_WALL_HEIGHT_MM");
        }

        [Test]
        public void GetByName_CaseInsensitive_ShouldReturnParameter()
        {
            // Act
            var result = _collection.GetByName("ble_wall_height_mm");

            // Assert
            result.Should().NotBeNull();
        }

        [Test]
        public void GetByName_NonExistingName_ShouldReturnNull()
        {
            // Act
            var result = _collection.GetByName("NONEXISTENT_PARAM");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GetByName_NullName_ShouldReturnNull()
        {
            // Act
            var result = _collection.GetByName(null);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetByDiscipline Tests

        [Test]
        public void GetByDiscipline_ExistingDiscipline_ShouldReturnParameters()
        {
            // Act
            var result = _collection.GetByDiscipline("Building Elements");

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.Discipline == "Building Elements");
        }

        [Test]
        public void GetByDiscipline_CaseInsensitive_ShouldReturnParameters()
        {
            // Act
            var result = _collection.GetByDiscipline("building elements");

            // Assert
            result.Should().HaveCount(2);
        }

        [Test]
        public void GetByDiscipline_NonExistingDiscipline_ShouldReturnEmptyList()
        {
            // Act
            var result = _collection.GetByDiscipline("Nonexistent");

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void GetByDiscipline_NullDiscipline_ShouldReturnEmptyList()
        {
            // Act
            var result = _collection.GetByDiscipline(null);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region GetBySystem Tests

        [Test]
        public void GetBySystem_ExistingSystem_ShouldReturnParameters()
        {
            // Act
            var result = _collection.GetBySystem("Walls");

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.System == "Walls");
        }

        [Test]
        public void GetBySystem_CaseInsensitive_ShouldReturnParameters()
        {
            // Act
            var result = _collection.GetBySystem("walls");

            // Assert
            result.Should().HaveCount(2);
        }

        [Test]
        public void GetBySystem_NonExistingSystem_ShouldReturnEmptyList()
        {
            // Act
            var result = _collection.GetBySystem("Nonexistent");

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region GetAll Tests

        [Test]
        public void GetAll_ShouldReturnAllParameters()
        {
            // Act
            var result = _collection.GetAll();

            // Assert
            result.Should().HaveCount(5);
        }

        [Test]
        public void GetAll_ShouldReturnNewList()
        {
            // Act
            var result1 = _collection.GetAll();
            var result2 = _collection.GetAll();

            // Assert - Should be different list instances
            result1.Should().NotBeSameAs(result2);
        }

        #endregion

        #region GetDisciplines Tests

        [Test]
        public void GetDisciplines_ShouldReturnAllUniqueDisciplines()
        {
            // Act
            var result = _collection.GetDisciplines();

            // Assert
            result.Should().HaveCount(4);
            result.Should().Contain("Building Elements");
            result.Should().Contain("Electrical");
            result.Should().Contain("HVAC");
            result.Should().Contain("Plumbing");
        }

        #endregion

        #region GetSystems Tests

        [Test]
        public void GetSystems_ShouldReturnAllUniqueSystems()
        {
            // Act
            var result = _collection.GetSystems();

            // Assert
            result.Should().HaveCount(4);
            result.Should().Contain("Walls");
            result.Should().Contain("Power");
            result.Should().Contain("Ducts");
            result.Should().Contain("Pipes");
        }

        #endregion

        #region Multi-Index Tests

        [Test]
        public void Add_ParameterWithDiscipline_ShouldBeIndexed()
        {
            // Arrange
            var collection = new ParameterDefinitionCollection();
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TEST_PARAM",
                DataType = "TEXT",
                Discipline = "TestDiscipline"
            };

            // Act
            collection.Add(param);

            // Assert
            collection.GetByDiscipline("TestDiscipline").Should().Contain(param);
            collection.GetByGuid(param.Guid).Should().Be(param);
            collection.GetByName("TEST_PARAM").Should().Be(param);
        }

        [Test]
        public void Add_ParameterWithSystem_ShouldBeIndexed()
        {
            // Arrange
            var collection = new ParameterDefinitionCollection();
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TEST_PARAM",
                DataType = "TEXT",
                System = "TestSystem"
            };

            // Act
            collection.Add(param);

            // Assert
            collection.GetBySystem("TestSystem").Should().Contain(param);
        }

        [Test]
        public void Add_ParameterWithoutDiscipline_ShouldNotBeInDisciplineIndex()
        {
            // Arrange
            var collection = new ParameterDefinitionCollection();
            var param = new ParameterDefinition
            {
                Guid = Guid.NewGuid(),
                Name = "TEST_PARAM",
                DataType = "TEXT",
                Discipline = null
            };

            // Act
            collection.Add(param);

            // Assert
            collection.GetDisciplines().Should().BeEmpty();
        }

        #endregion

        #region Real-World Scenario Tests

        [Test]
        public void Collection_ShouldSupportBIMParameterWorkflow()
        {
            // Scenario: Find all wall-related parameters for a schedule

            // Act
            var wallParams = _collection.GetBySystem("Walls");
            var lengthParams = wallParams.FindAll(p => p.DataType == "LENGTH");

            // Assert
            lengthParams.Should().HaveCount(2);
            lengthParams.Should().Contain(p => p.Name == "BLE_WALL_HEIGHT_MM");
            lengthParams.Should().Contain(p => p.Name == "BLE_WALL_WIDTH_MM");
        }

        [Test]
        public void Collection_ShouldSupportMEPParameterGrouping()
        {
            // Scenario: Group parameters by MEP discipline

            // Act
            var electricalParams = _collection.GetByDiscipline("Electrical");
            var hvacParams = _collection.GetByDiscipline("HVAC");
            var plumbingParams = _collection.GetByDiscipline("Plumbing");

            // Assert
            electricalParams.Should().HaveCount(1);
            hvacParams.Should().HaveCount(1);
            plumbingParams.Should().HaveCount(1);
        }

        #endregion
    }
}
