using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Data.Materials;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Unit tests for MaterialDatabase class.
    /// Tests material lookup, filtering, search, and statistics functionality.
    /// </summary>
    [TestFixture]
    public class MaterialDatabaseTests
    {
        private MaterialDatabase _database;

        [SetUp]
        public void Setup()
        {
            _database = new MaterialDatabase();
            PopulateTestMaterials();
        }

        /// <summary>
        /// Populates the database with test materials using reflection to access private AddMaterial.
        /// </summary>
        private void PopulateTestMaterials()
        {
            // Use reflection to access private AddMaterial method for testing
            var addMethod = typeof(MaterialDatabase).GetMethod("AddMaterial",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var testMaterials = CreateTestMaterials();
            foreach (var material in testMaterials)
            {
                addMethod.Invoke(_database, new object[] { material });
            }
        }

        private List<MaterialDefinition> CreateTestMaterials()
        {
            return new List<MaterialDefinition>
            {
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "CONC-01",
                    Name = "Concrete C30",
                    Category = "Concrete",
                    Discipline = "Structural",
                    Description = "30 MPa concrete",
                    Manufacturer = "Local Mix",
                    Standard = "BS EN 206",
                    ThermalResistance = 0.5,
                    ThermalConductivity = 1.6,
                    Density = 2400,
                    Cost = 120,
                    CostUnit = "m³"
                },
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "CONC-02",
                    Name = "Concrete C40",
                    Category = "Concrete",
                    Discipline = "Structural",
                    Description = "40 MPa concrete",
                    ThermalResistance = 0.5,
                    Cost = 140
                },
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "STEEL-A36",
                    Name = "Steel A36",
                    Category = "Steel",
                    Discipline = "Structural",
                    Description = "Structural steel",
                    Manufacturer = "Steel Corp",
                    Standard = "ASTM A36",
                    ThermalConductivity = 50,
                    Density = 7850,
                    Cost = 800
                },
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "INS-001",
                    Name = "Mineral Wool",
                    Category = "Insulation",
                    Discipline = "Architecture",
                    Description = "Thermal insulation",
                    ThermalResistance = 3.5,
                    ThermalConductivity = 0.035,
                    Density = 30,
                    Cost = 15
                },
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "INS-002",
                    Name = "XPS Board",
                    Category = "Insulation",
                    Discipline = "Architecture",
                    Description = "Extruded polystyrene",
                    ThermalResistance = 5.0,
                    ThermalConductivity = 0.028,
                    Density = 35,
                    Cost = 25
                },
                new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "PIPE-CU",
                    Name = "Copper Pipe",
                    Category = "Pipes",
                    Discipline = "MEP",
                    Description = "Copper piping",
                    Manufacturer = "CopperFlow",
                    ThermalConductivity = 400,
                    Density = 8960,
                    Cost = 50
                }
            };
        }

        #region Basic Property Tests

        [Test]
        public void Count_WithTestData_ShouldReturnCorrectCount()
        {
            // Assert
            _database.Count.Should().Be(6);
        }

        [Test]
        public void Categories_ShouldReturnAllUniqueCategories()
        {
            // Act
            var categories = _database.Categories.ToList();

            // Assert
            categories.Should().HaveCount(4);
            categories.Should().Contain("Concrete");
            categories.Should().Contain("Steel");
            categories.Should().Contain("Insulation");
            categories.Should().Contain("Pipes");
        }

        [Test]
        public void Disciplines_ShouldReturnAllUniqueDisciplines()
        {
            // Act
            var disciplines = _database.Disciplines.ToList();

            // Assert
            disciplines.Should().HaveCount(3);
            disciplines.Should().Contain("Structural");
            disciplines.Should().Contain("Architecture");
            disciplines.Should().Contain("MEP");
        }

        [Test]
        public void AllMaterials_ShouldReturnReadOnlyList()
        {
            // Act
            var materials = _database.AllMaterials;

            // Assert
            materials.Should().BeAssignableTo<IReadOnlyList<MaterialDefinition>>();
            materials.Should().HaveCount(6);
        }

        #endregion

        #region Lookup Tests

        [Test]
        public void GetByCode_ExistingCode_ShouldReturnMaterial()
        {
            // Act
            var material = _database.GetByCode("CONC-01");

            // Assert
            material.Should().NotBeNull();
            material.Name.Should().Be("Concrete C30");
        }

        [Test]
        public void GetByCode_CaseInsensitive_ShouldReturnMaterial()
        {
            // Act
            var material = _database.GetByCode("conc-01");

            // Assert
            material.Should().NotBeNull();
            material.Name.Should().Be("Concrete C30");
        }

        [Test]
        public void GetByCode_NonExistingCode_ShouldReturnNull()
        {
            // Act
            var material = _database.GetByCode("NONEXISTENT");

            // Assert
            material.Should().BeNull();
        }

        [Test]
        public void GetByCode_NullCode_ShouldReturnNull()
        {
            // Act
            var material = _database.GetByCode(null);

            // Assert
            material.Should().BeNull();
        }

        [Test]
        public void GetByCode_EmptyCode_ShouldReturnNull()
        {
            // Act
            var material = _database.GetByCode("");

            // Assert
            material.Should().BeNull();
        }

        [Test]
        public void GetByName_ExistingName_ShouldReturnMaterial()
        {
            // Act
            var material = _database.GetByName("Steel A36");

            // Assert
            material.Should().NotBeNull();
            material.Code.Should().Be("STEEL-A36");
        }

        [Test]
        public void GetByName_CaseInsensitive_ShouldReturnMaterial()
        {
            // Act
            var material = _database.GetByName("steel a36");

            // Assert
            material.Should().NotBeNull();
        }

        [Test]
        public void GetByName_NonExistingName_ShouldReturnNull()
        {
            // Act
            var material = _database.GetByName("Nonexistent Material");

            // Assert
            material.Should().BeNull();
        }

        [Test]
        public void GetByGuid_ExistingGuid_ShouldReturnMaterial()
        {
            // Arrange
            var existingMaterial = _database.AllMaterials.First();

            // Act
            var material = _database.GetByGuid(existingMaterial.Guid);

            // Assert
            material.Should().NotBeNull();
            material.Should().BeSameAs(existingMaterial);
        }

        [Test]
        public void Exists_ExistingCode_ShouldReturnTrue()
        {
            // Act
            var exists = _database.Exists("CONC-01");

            // Assert
            exists.Should().BeTrue();
        }

        [Test]
        public void Exists_NonExistingCode_ShouldReturnFalse()
        {
            // Act
            var exists = _database.Exists("NONEXISTENT");

            // Assert
            exists.Should().BeFalse();
        }

        #endregion

        #region Filter Tests

        [Test]
        public void GetByCategory_ExistingCategory_ShouldReturnMaterials()
        {
            // Act
            var materials = _database.GetByCategory("Concrete");

            // Assert
            materials.Should().HaveCount(2);
            materials.Should().OnlyContain(m => m.Category == "Concrete");
        }

        [Test]
        public void GetByCategory_CaseInsensitive_ShouldReturnMaterials()
        {
            // Act
            var materials = _database.GetByCategory("concrete");

            // Assert
            materials.Should().HaveCount(2);
        }

        [Test]
        public void GetByCategory_NonExistingCategory_ShouldReturnEmptyList()
        {
            // Act
            var materials = _database.GetByCategory("Nonexistent");

            // Assert
            materials.Should().BeEmpty();
        }

        [Test]
        public void GetByCategory_NullCategory_ShouldReturnEmptyList()
        {
            // Act
            var materials = _database.GetByCategory(null);

            // Assert
            materials.Should().BeEmpty();
        }

        [Test]
        public void GetByDiscipline_ExistingDiscipline_ShouldReturnMaterials()
        {
            // Act
            var materials = _database.GetByDiscipline("Structural");

            // Assert
            materials.Should().HaveCount(3);
            materials.Should().OnlyContain(m => m.Discipline == "Structural");
        }

        [Test]
        public void Filter_WithPredicate_ShouldReturnMatchingMaterials()
        {
            // Act
            var materials = _database.Filter(m => m.Cost > 100);

            // Assert
            materials.Should().HaveCount(3); // CONC-01 (120), CONC-02 (140), STEEL-A36 (800)
        }

        [Test]
        public void Filter_NullPredicate_ShouldThrow()
        {
            // Act
            Action act = () => _database.Filter(null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Filter_ByThermalResistance_ShouldWork()
        {
            // Act - Find high R-value insulation
            var materials = _database.Filter(m => m.ThermalResistance > 3.0);

            // Assert
            materials.Should().HaveCount(2); // Mineral Wool (3.5) and XPS (5.0)
        }

        #endregion

        #region Search Tests

        [Test]
        public void Search_ByName_ShouldReturnMatches()
        {
            // Act
            var materials = _database.Search("Concrete");

            // Assert
            materials.Should().HaveCount(2);
        }

        [Test]
        public void Search_ByCode_ShouldReturnMatches()
        {
            // Act
            var materials = _database.Search("INS");

            // Assert
            materials.Should().HaveCount(2); // INS-001 and INS-002
        }

        [Test]
        public void Search_ByDescription_ShouldReturnMatches()
        {
            // Act
            var materials = _database.Search("insulation");

            // Assert
            materials.Should().HaveCount(1); // Mineral Wool
        }

        [Test]
        public void Search_CaseInsensitive_ShouldReturnMatches()
        {
            // Act
            var materials = _database.Search("STEEL");

            // Assert
            materials.Should().HaveCount(1);
        }

        [Test]
        public void Search_EmptyQuery_ShouldReturnEmptyList()
        {
            // Act
            var materials = _database.Search("");

            // Assert
            materials.Should().BeEmpty();
        }

        [Test]
        public void Search_NullQuery_ShouldReturnEmptyList()
        {
            // Act
            var materials = _database.Search(null);

            // Assert
            materials.Should().BeEmpty();
        }

        [Test]
        public void Search_NoMatches_ShouldReturnEmptyList()
        {
            // Act
            var materials = _database.Search("xyz123nonexistent");

            // Assert
            materials.Should().BeEmpty();
        }

        #endregion

        #region Advanced Search Tests

        [Test]
        public void SearchAdvanced_NullCriteria_ShouldThrow()
        {
            // Act
            Action act = () => _database.SearchAdvanced(null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void SearchAdvanced_ByCategory_ShouldFilter()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria { Category = "Insulation" };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(2);
        }

        [Test]
        public void SearchAdvanced_ByDiscipline_ShouldFilter()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria { Discipline = "MEP" };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(1);
            materials[0].Name.Should().Be("Copper Pipe");
        }

        [Test]
        public void SearchAdvanced_ByManufacturer_ShouldFilter()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria { Manufacturer = "Steel" };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(1);
        }

        [Test]
        public void SearchAdvanced_ByStandard_ShouldFilter()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria { Standard = "ASTM" };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(1);
            materials[0].Code.Should().Be("STEEL-A36");
        }

        [Test]
        public void SearchAdvanced_ByThermalResistanceRange_ShouldFilter()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria
            {
                MinThermalResistance = 3.0,
                MaxThermalResistance = 4.0
            };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(1);
            materials[0].Name.Should().Be("Mineral Wool");
        }

        [Test]
        public void SearchAdvanced_CombinedCriteria_ShouldFilterAll()
        {
            // Arrange
            var criteria = new MaterialSearchCriteria
            {
                Category = "Insulation",
                MinThermalResistance = 4.0
            };

            // Act
            var materials = _database.SearchAdvanced(criteria);

            // Assert
            materials.Should().HaveCount(1);
            materials[0].Name.Should().Be("XPS Board");
        }

        #endregion

        #region Statistics Tests

        [Test]
        public void GetStatistics_ShouldReturnCorrectCounts()
        {
            // Act
            var stats = _database.GetStatistics();

            // Assert
            stats.TotalMaterials.Should().Be(6);
            stats.CategoryCount.Should().Be(4);
            stats.DisciplineCount.Should().Be(3);
        }

        [Test]
        public void GetStatistics_ShouldIncludeCategoryBreakdown()
        {
            // Act
            var stats = _database.GetStatistics();

            // Assert
            stats.Categories.Should().ContainKey("Concrete");
            stats.Categories["Concrete"].Should().Be(2);
            stats.Categories["Insulation"].Should().Be(2);
        }

        [Test]
        public void GetStatistics_ShouldIncludeDisciplineBreakdown()
        {
            // Act
            var stats = _database.GetStatistics();

            // Assert
            stats.Disciplines["Structural"].Should().Be(3);
            stats.Disciplines["Architecture"].Should().Be(2);
            stats.Disciplines["MEP"].Should().Be(1);
        }

        [Test]
        public void GetTopCategories_ShouldReturnSortedByCount()
        {
            // Act
            var topCategories = _database.GetTopCategories(3);

            // Assert
            topCategories.Should().HaveCount(3);
            // Concrete and Insulation tied with 2 each, then Steel/Pipes with 1
            topCategories[0].Value.Should().BeGreaterOrEqualTo(topCategories[1].Value);
            topCategories[1].Value.Should().BeGreaterOrEqualTo(topCategories[2].Value);
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_ShouldRemoveAllMaterials()
        {
            // Act
            _database.Clear();

            // Assert
            _database.Count.Should().Be(0);
            _database.IsLoaded.Should().BeFalse();
            _database.Categories.Should().BeEmpty();
            _database.Disciplines.Should().BeEmpty();
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void Lookup_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new System.Threading.Tasks.Task[100];
            var results = new MaterialDefinition[100];

            // Act
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    results[index] = _database.GetByCode("CONC-01");
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert - All should get the same material
            results.Should().OnlyContain(m => m != null && m.Code == "CONC-01");
        }

        [Test]
        public void Search_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new System.Threading.Tasks.Task[50];
            var searchTerms = new[] { "Concrete", "Steel", "Insulation", "Copper", "A36" };

            // Act
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    var term = searchTerms[index % searchTerms.Length];
                    var results = _database.Search(term);
                    results.Should().NotBeNull();
                });
            }

            // Assert
            Action act = () => System.Threading.Tasks.Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        #endregion
    }

    /// <summary>
    /// Unit tests for MaterialSearchCriteria class.
    /// </summary>
    [TestFixture]
    public class MaterialSearchCriteriaTests
    {
        [Test]
        public void Constructor_ShouldInitializeWithNulls()
        {
            // Act
            var criteria = new MaterialSearchCriteria();

            // Assert
            criteria.Query.Should().BeNull();
            criteria.Category.Should().BeNull();
            criteria.Discipline.Should().BeNull();
            criteria.Manufacturer.Should().BeNull();
            criteria.Standard.Should().BeNull();
            criteria.MinThermalResistance.Should().BeNull();
            criteria.MaxThermalResistance.Should().BeNull();
            criteria.CaseSensitive.Should().BeFalse();
        }
    }

    /// <summary>
    /// Unit tests for MaterialDefinition class.
    /// </summary>
    [TestFixture]
    public class MaterialDefinitionTests
    {
        [Test]
        public void Constructor_ShouldGenerateGuid()
        {
            // Act
            var material = new MaterialDefinition();

            // Assert
            material.Guid.Should().NotBe(Guid.Empty);
        }

        [Test]
        public void Constructor_ShouldInitializeCustomProperties()
        {
            // Act
            var material = new MaterialDefinition();

            // Assert
            material.CustomProperties.Should().NotBeNull();
            material.CustomProperties.Should().BeEmpty();
        }

        [Test]
        public void ToString_ShouldIncludeCodeNameCategory()
        {
            // Arrange
            var material = new MaterialDefinition
            {
                Code = "TEST-01",
                Name = "Test Material",
                Category = "Test Category"
            };

            // Act
            var result = material.ToString();

            // Assert
            result.Should().Contain("TEST-01");
            result.Should().Contain("Test Material");
            result.Should().Contain("Test Category");
        }
    }
}
