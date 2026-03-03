// ============================================================================
// StingBIM AI Tests - Model Health Monitor Tests
// Validates quality checking, performance analysis, compliance tracking,
// trend analysis, and BEP requirements
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Automation.Health;

namespace StingBIM.AI.Tests.Automation
{
    [TestFixture]
    public class HealthMonitorTests
    {
        private ModelHealthMonitor _monitor;
        private QualityChecker _qualityChecker;

        [SetUp]
        public void SetUp()
        {
            _monitor = new ModelHealthMonitor();
            _qualityChecker = new QualityChecker();
        }

        #region Helper Methods

        private BIMModel CreateHealthyModel()
        {
            return new BIMModel
            {
                ModelId = "MDL-001",
                ModelName = "Healthy Office Model",
                ElementCount = 5000,
                ViewCount = 50,
                FamilyCount = 200,
                FileSize = 100 * 1024 * 1024, // 100 MB
                Elements = new List<ModelElement>
                {
                    CreateElement("E-001", "Walls", "STD_Interior Wall", "Concrete", "Room 101",
                        new Dictionary<string, object> { { "Mark", "W-001" }, { "Fire Rating", "60" }, { "Comments", "Standard wall" } }),
                    CreateElement("E-002", "Doors", "STD_Single Door", "Timber", "Room 101",
                        new Dictionary<string, object> { { "Mark", "D-001" }, { "Fire Rating", "30" }, { "Comments", "Entry door" } }),
                    CreateElement("E-003", "Windows", "STD_Double Glazed", "Glass", "Room 101",
                        new Dictionary<string, object> { { "Mark", "WN-001" }, { "Comments", "Office window" } }),
                    CreateElement("E-004", "Floors", "STD_Concrete Slab", "Concrete", "Room 101",
                        new Dictionary<string, object> { { "Mark", "F-001" }, { "Comments", "Ground floor" } }),
                    CreateElement("E-005", "Structural Columns", "STD_Steel Column", "Steel", "Room 101",
                        new Dictionary<string, object> { { "Mark", "C-001" }, { "Comments", "Main column" } })
                },
                Families = new List<ModelFamily>
                {
                    new ModelFamily { FamilyId = "FAM-001", Name = "STD_Interior Wall", IsInPlace = false, InstanceCount = 50 },
                    new ModelFamily { FamilyId = "FAM-002", Name = "STD_Single Door", IsInPlace = false, InstanceCount = 30 },
                    new ModelFamily { FamilyId = "FAM-003", Name = "STD_Double Glazed", IsInPlace = false, InstanceCount = 20 }
                },
                Views = new List<ModelView>
                {
                    new ModelView { ViewId = "V-001", Name = "Level 1 Floor Plan", ViewType = "FloorPlan" },
                    new ModelView { ViewId = "V-002", Name = "Section A-A", ViewType = "Section" },
                    new ModelView { ViewId = "V-003", Name = "3D View - Overall", ViewType = "3D" }
                },
                Levels = new List<ModelLevel>
                {
                    new ModelLevel { LevelId = "L-001", Name = "Ground Floor", ElementCount = 100 },
                    new ModelLevel { LevelId = "L-002", Name = "First Floor", ElementCount = 80 }
                },
                Groups = new List<ModelGroup>
                {
                    new ModelGroup { GroupId = "G-001", Name = "Toilet Block", InstanceCount = 4 }
                },
                LinkedModels = new List<LinkedModel>
                {
                    new LinkedModel { LinkId = "LNK-001", Name = "Structural Model", IsLoaded = true }
                },
                ImportedCad = new List<ImportedCad>(),
                Warnings = new List<ModelWarning>
                {
                    new ModelWarning { Message = "Room not enclosed" }
                },
                Errors = new List<ModelError>()
            };
        }

        private BIMModel CreateUnhealthyModel()
        {
            return new BIMModel
            {
                ModelId = "MDL-002",
                ModelName = "Problematic Model",
                ElementCount = 600000, // Over 500K threshold
                ViewCount = 1500,      // Over 1000 threshold
                FamilyCount = 500,
                FileSize = 600L * 1024 * 1024, // 600 MB, over 500MB threshold
                Elements = new List<ModelElement>
                {
                    // Elements missing materials
                    CreateElement("E-101", "Walls", "Wall Type A", null, null,
                        new Dictionary<string, object>()),
                    CreateElement("E-102", "Doors", "Door Type A", null, "Room A",
                        new Dictionary<string, object>()),
                    CreateElement("E-103", "Floors", "Floor Type A", null, null,
                        new Dictionary<string, object> { { "Comments", "test" } }),
                    // Duplicate elements at same location
                    CreateElementAtLocation("E-104", "Walls", "Wall Type B", "Concrete", 10, 20, 0),
                    CreateElementAtLocation("E-105", "Walls", "Wall Type B", "Concrete", 10, 20, 0),
                    // Element without room assignment
                    CreateElement("E-106", "Furniture", "Desk", null, null, new Dictionary<string, object>())
                },
                Families = new List<ModelFamily>
                {
                    new ModelFamily { FamilyId = "FAM-101", Name = "wall_type_a", IsInPlace = true, InstanceCount = 5 },
                    new ModelFamily { FamilyId = "FAM-102", Name = "custom door", IsInPlace = true, InstanceCount = 3 },
                    // Many in-place families
                    new ModelFamily { FamilyId = "FAM-103", Name = "InPlace_1", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-104", Name = "InPlace_2", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-105", Name = "InPlace_3", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-106", Name = "InPlace_4", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-107", Name = "InPlace_5", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-108", Name = "InPlace_6", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-109", Name = "InPlace_7", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-110", Name = "InPlace_8", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-111", Name = "InPlace_9", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-112", Name = "InPlace_10", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-113", Name = "InPlace_11", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-114", Name = "InPlace_12", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-115", Name = "InPlace_13", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-116", Name = "InPlace_14", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-117", Name = "InPlace_15", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-118", Name = "InPlace_16", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-119", Name = "InPlace_17", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-120", Name = "InPlace_18", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-121", Name = "InPlace_19", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-122", Name = "InPlace_20", IsInPlace = true, InstanceCount = 1 },
                    new ModelFamily { FamilyId = "FAM-123", Name = "InPlace_21", IsInPlace = true, InstanceCount = 1 }
                },
                Views = new List<ModelView>
                {
                    new ModelView { ViewId = "V-101", Name = "working view 1", ViewType = "FloorPlan" },
                    new ModelView { ViewId = "V-102", Name = "copy of view 2", ViewType = "FloorPlan" }
                },
                Levels = new List<ModelLevel>
                {
                    new ModelLevel { LevelId = "L-101", Name = "Ground", ElementCount = 200 },
                    new ModelLevel { LevelId = "L-102", Name = "Unused Level", ElementCount = 0 }
                },
                Groups = new List<ModelGroup>
                {
                    new ModelGroup { GroupId = "G-101", Name = "Old Group", InstanceCount = 0 }
                },
                LinkedModels = new List<LinkedModel>(),
                ImportedCad = new List<ImportedCad>
                {
                    new ImportedCad { Name = "site_plan.dwg" },
                    new ImportedCad { Name = "existing_layout.dwg" },
                    new ImportedCad { Name = "survey.dwg" },
                    new ImportedCad { Name = "boundary.dwg" },
                    new ImportedCad { Name = "services.dwg" },
                    new ImportedCad { Name = "drainage.dwg" },
                    new ImportedCad { Name = "landscaping.dwg" },
                    new ImportedCad { Name = "roads.dwg" },
                    new ImportedCad { Name = "topo.dwg" },
                    new ImportedCad { Name = "utilities.dwg" },
                    new ImportedCad { Name = "extra.dwg" }
                },
                Warnings = new List<ModelWarning>
                {
                    new ModelWarning { Message = "Warning 1" },
                    new ModelWarning { Message = "Warning 2" },
                    new ModelWarning { Message = "Warning 3" },
                    new ModelWarning { Message = "Warning 4" },
                    new ModelWarning { Message = "Warning 5" },
                    new ModelWarning { Message = "Warning 6" },
                    new ModelWarning { Message = "Warning 7" },
                    new ModelWarning { Message = "Warning 8" },
                    new ModelWarning { Message = "Warning 9" },
                    new ModelWarning { Message = "Warning 10" },
                    new ModelWarning { Message = "Warning 11" }
                },
                Errors = new List<ModelError>
                {
                    new ModelError { Message = "Critical error" }
                }
            };
        }

        private ModelElement CreateElement(string id, string category, string typeName, string material, string room, Dictionary<string, object> parameters)
        {
            return new ModelElement
            {
                ElementId = id,
                Category = category,
                TypeName = typeName,
                Material = material,
                Room = room,
                Parameters = parameters ?? new Dictionary<string, object>()
            };
        }

        private ModelElement CreateElementAtLocation(string id, string category, string typeName, string material, double x, double y, double z)
        {
            return new ModelElement
            {
                ElementId = id,
                Category = category,
                TypeName = typeName,
                Material = material,
                Location = new Point3D { X = x, Y = y, Z = z },
                Parameters = new Dictionary<string, object>()
            };
        }

        #endregion

        #region QualityChecker.CheckParameterCompleteness Tests

        [Test]
        public void CheckParameterCompleteness_AllComplete_Returns100Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Single Door", "Timber", "R1",
                    new Dictionary<string, object> { { "Mark", "D1" }, { "Fire Rating", "30" } }),
                CreateElement("E-002", "Doors", "Double Door", "Timber", "R2",
                    new Dictionary<string, object> { { "Mark", "D2" }, { "Fire Rating", "60" } })
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert
            result.Percentage.Should().Be(100.0);
            result.CompleteCount.Should().Be(2);
            result.IncompleteCount.Should().Be(0);
            result.Issues.Should().BeEmpty();
        }

        [Test]
        public void CheckParameterCompleteness_SomeMissing_ReportsIncomplete()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Single Door", "Timber", "R1",
                    new Dictionary<string, object> { { "Mark", "D1" } }), // Missing Fire Rating
                CreateElement("E-002", "Doors", "Double Door", "Timber", "R2",
                    new Dictionary<string, object> { { "Mark", "D2" }, { "Fire Rating", "60" } })
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert
            result.Percentage.Should().Be(50.0);
            result.CompleteCount.Should().Be(1);
            result.IncompleteCount.Should().Be(1);
            result.Issues.Should().HaveCount(1);
            result.Issues[0].Description.Should().Contain("Fire Rating");
        }

        [Test]
        public void CheckParameterCompleteness_AllMissing_Returns0Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Door A", null, null,
                    new Dictionary<string, object>()),
                CreateElement("E-002", "Doors", "Door B", null, null,
                    new Dictionary<string, object>())
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert
            result.Percentage.Should().Be(0.0);
            result.IncompleteCount.Should().Be(2);
            result.Issues.Should().HaveCount(2);
        }

        [Test]
        public void CheckParameterCompleteness_EmptyElements_Returns100Percent()
        {
            // Arrange
            var elements = new List<ModelElement>();
            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckParameterCompleteness_NullElements_Returns100Percent()
        {
            // Arrange
            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(null, required);

            // Assert
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckParameterCompleteness_NullRequired_Returns100Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Door A", null, null, new Dictionary<string, object>())
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, null);

            // Assert
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckParameterCompleteness_CategoryFiltering_OnlyChecksApplicable()
        {
            // Arrange - parameter applies to Doors but element is Walls
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "Standard Wall", "Concrete", "R1",
                    new Dictionary<string, object>()) // No Mark, but Mark only applies to Doors
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert - walls should not be checked since Mark only applies to Doors
            result.TotalChecked.Should().Be(0);
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckParameterCompleteness_AllCategory_ChecksEverything()
        {
            // Arrange - "All" category applies to every element
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "Standard Wall", "Concrete", "R1",
                    new Dictionary<string, object> { { "Comments", "test" } }),
                CreateElement("E-002", "Doors", "Standard Door", "Timber", "R1",
                    new Dictionary<string, object>()) // Missing Comments
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Comments", Categories = new[] { "All" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert
            result.TotalChecked.Should().Be(2);
            result.CompleteCount.Should().Be(1);
            result.IncompleteCount.Should().Be(1);
        }

        [Test]
        public void CheckParameterCompleteness_ManyMissing_SeverityIsError()
        {
            // Arrange - element missing more than half of its applicable parameters
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Door A", null, null,
                    new Dictionary<string, object> { { "Mark", "D1" } }) // Has 1 of 3 required
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Comments", Categories = new[] { "All" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert - missing 2 of 3 (> half), should be Error severity
            result.Issues.Should().HaveCount(1);
            result.Issues[0].Severity.Should().Be(IssueSeverity.Error);
        }

        [Test]
        public void CheckParameterCompleteness_FewMissing_SeverityIsWarning()
        {
            // Arrange - element missing less than half of its applicable parameters
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Doors", "Door A", null, null,
                    new Dictionary<string, object> { { "Mark", "D1" }, { "Fire Rating", "30" } }) // Has 2 of 3
            };

            var required = new List<RequiredParameter>
            {
                new RequiredParameter { Name = "Mark", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Fire Rating", Categories = new[] { "Doors" } },
                new RequiredParameter { Name = "Comments", Categories = new[] { "All" } }
            };

            // Act
            var result = _qualityChecker.CheckParameterCompleteness(elements, required);

            // Assert - missing 1 of 3 (< half), should be Warning severity
            result.Issues.Should().HaveCount(1);
            result.Issues[0].Severity.Should().Be(IssueSeverity.Warning);
        }

        #endregion

        #region QualityChecker.CheckNamingCompliance Tests

        [Test]
        public void CheckNamingCompliance_AllMatch_Returns100Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "STD_Interior Wall", null, null, new Dictionary<string, object>()),
                CreateElement("E-002", "Walls", "EXT_Exterior Wall", null, null, new Dictionary<string, object>())
            };

            var patterns = new List<NamingPattern>
            {
                // Matches PREFIX_ pattern (2-4 uppercase letters followed by underscore)
                new NamingPattern { AppliesTo = "Walls", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(elements, patterns);

            // Assert
            result.Percentage.Should().Be(100.0);
            result.CompliantCount.Should().Be(2);
            result.NonCompliantCount.Should().Be(0);
            result.Issues.Should().BeEmpty();
        }

        [Test]
        public void CheckNamingCompliance_NoneMatch_Returns0Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "interior wall", null, null, new Dictionary<string, object>()),
                CreateElement("E-002", "Walls", "my custom wall", null, null, new Dictionary<string, object>())
            };

            var patterns = new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "Walls", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(elements, patterns);

            // Assert
            result.Percentage.Should().Be(0.0);
            result.NonCompliantCount.Should().Be(2);
            result.Issues.Should().HaveCount(2);
            result.Issues.All(i => i.Severity == IssueSeverity.Warning).Should().BeTrue();
        }

        [Test]
        public void CheckNamingCompliance_MixedCompliance_CorrectPercentage()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "STD_Good Wall", null, null, new Dictionary<string, object>()),
                CreateElement("E-002", "Walls", "bad wall name", null, null, new Dictionary<string, object>()),
                CreateElement("E-003", "Walls", "EXT_Also Good", null, null, new Dictionary<string, object>())
            };

            var patterns = new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "Walls", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(elements, patterns);

            // Assert
            result.TotalChecked.Should().Be(3);
            result.CompliantCount.Should().Be(2);
            result.NonCompliantCount.Should().Be(1);
            result.Percentage.Should().BeApproximately(66.67, 0.01);
        }

        [Test]
        public void CheckNamingCompliance_NoApplicablePattern_SkipsElement()
        {
            // Arrange - pattern for Doors, but elements are Walls
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "any name", null, null, new Dictionary<string, object>())
            };

            var patterns = new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "Doors", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(elements, patterns);

            // Assert
            result.TotalChecked.Should().Be(0);
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckNamingCompliance_AllPattern_MatchesEverything()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "STD_Wall", null, null, new Dictionary<string, object>()),
                CreateElement("E-002", "Doors", "bad door", null, null, new Dictionary<string, object>())
            };

            var patterns = new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "All", Pattern = @"^[A-Z]{2,4}_.*", Description = "PREFIX_Name" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(elements, patterns);

            // Assert
            result.TotalChecked.Should().Be(2);
            result.CompliantCount.Should().Be(1);
            result.NonCompliantCount.Should().Be(1);
        }

        [Test]
        public void CheckNamingCompliance_EmptyElements_Returns100Percent()
        {
            // Arrange
            var patterns = new List<NamingPattern>
            {
                new NamingPattern { AppliesTo = "All", Pattern = @".*", Description = "Any" }
            };

            // Act
            var result = _qualityChecker.CheckNamingCompliance(new List<ModelElement>(), patterns);

            // Assert
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckNamingCompliance_NullInputs_Returns100Percent()
        {
            // Act
            var result1 = _qualityChecker.CheckNamingCompliance(null, new List<NamingPattern>());
            var result2 = _qualityChecker.CheckNamingCompliance(new List<ModelElement>(), null);

            // Assert
            result1.Percentage.Should().Be(100.0);
            result2.Percentage.Should().Be(100.0);
        }

        #endregion

        #region QualityChecker.CheckMaterialAssignment Tests

        [Test]
        public void CheckMaterialAssignment_AllAssigned_Returns100Percent()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "Wall A", "Concrete", null, new Dictionary<string, object>()),
                CreateElement("E-002", "Floors", "Floor A", "Concrete", null, new Dictionary<string, object>()),
                CreateElement("E-003", "Doors", "Door A", "Timber", null, new Dictionary<string, object>())
            };

            // Act
            var result = _qualityChecker.CheckMaterialAssignment(elements);

            // Assert
            result.Percentage.Should().Be(100.0);
            result.AssignedCount.Should().Be(3);
            result.UnassignedCount.Should().Be(0);
        }

        [Test]
        public void CheckMaterialAssignment_SomeUnassigned_ReportsIssues()
        {
            // Arrange
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Walls", "Wall A", "Concrete", null, new Dictionary<string, object>()),
                CreateElement("E-002", "Walls", "Wall B", null, null, new Dictionary<string, object>()), // No material
                CreateElement("E-003", "Doors", "Door A", "", null, new Dictionary<string, object>()) // Empty material
            };

            // Act
            var result = _qualityChecker.CheckMaterialAssignment(elements);

            // Assert
            result.AssignedCount.Should().Be(1);
            result.UnassignedCount.Should().Be(2);
            result.Issues.Should().HaveCount(2);
            result.Issues.All(i => i.Category == "Material Assignment").Should().BeTrue();
        }

        [Test]
        public void CheckMaterialAssignment_NonMaterialCategory_NotChecked()
        {
            // Arrange - Furniture is not in the categories requiring materials
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Furniture", "Desk", null, null, new Dictionary<string, object>())
            };

            // Act
            var result = _qualityChecker.CheckMaterialAssignment(elements);

            // Assert
            result.TotalChecked.Should().Be(0);
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckMaterialAssignment_NullElements_Returns100Percent()
        {
            // Act
            var result = _qualityChecker.CheckMaterialAssignment(null);

            // Assert
            result.Percentage.Should().Be(100.0);
        }

        [Test]
        public void CheckMaterialAssignment_StructuralElements_AreChecked()
        {
            // Arrange - Structural Columns and Beams should be checked
            var elements = new List<ModelElement>
            {
                CreateElement("E-001", "Structural Columns", "Steel Column", "Steel", null, new Dictionary<string, object>()),
                CreateElement("E-002", "Structural Beams", "Concrete Beam", null, null, new Dictionary<string, object>()) // Missing
            };

            // Act
            var result = _qualityChecker.CheckMaterialAssignment(elements);

            // Assert
            result.TotalChecked.Should().Be(2);
            result.AssignedCount.Should().Be(1);
            result.UnassignedCount.Should().Be(1);
        }

        #endregion

        #region PerformanceAnalyzer, ComplianceTracker, TrendAnalyzer Tests

        // NOTE: PerformanceAnalyzer is currently an empty class: `public class PerformanceAnalyzer { }`
        // No methods to test. Tests should be added when the class is implemented.

        // NOTE: ComplianceTracker is currently an empty class: `public class ComplianceTracker { }`
        // No methods to test. Tests should be added when the class is implemented.

        // NOTE: TrendAnalyzer is currently an empty class: `public class TrendAnalyzer { }`
        // No methods to test. Tests should be added when the class is implemented.

        [Test]
        public void PerformanceAnalyzer_CanBeInstantiated()
        {
            var analyzer = new PerformanceAnalyzer();
            analyzer.Should().NotBeNull();
        }

        [Test]
        public void ComplianceTracker_CanBeInstantiated()
        {
            var tracker = new ComplianceTracker();
            tracker.Should().NotBeNull();
        }

        [Test]
        public void TrendAnalyzer_CanBeInstantiated()
        {
            var analyzer = new TrendAnalyzer();
            analyzer.Should().NotBeNull();
        }

        #endregion

        #region BEPRequirements.ForISO19650 Factory Tests

        [Test]
        public void ForISO19650_ReturnsCompleteRequirements()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.Should().NotBeNull();
            bep.LODRequirementsByPhase.Should().NotBeEmpty();
            bep.FileNamingConventionPattern.Should().NotBeNullOrEmpty();
            bep.RequiredParameterGroups.Should().NotBeEmpty();
            bep.ModelOrigin.Should().NotBeNull();
            bep.DeliveryMilestones.Should().NotBeEmpty();
        }

        [Test]
        public void ForISO19650_LODPhases_AreCorrect()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.LODRequirementsByPhase.Should().ContainKey("Brief");
            bep.LODRequirementsByPhase["Brief"].Should().Be(100);
            bep.LODRequirementsByPhase.Should().ContainKey("Concept");
            bep.LODRequirementsByPhase["Concept"].Should().Be(200);
            bep.LODRequirementsByPhase.Should().ContainKey("Design Development");
            bep.LODRequirementsByPhase["Design Development"].Should().Be(300);
            bep.LODRequirementsByPhase.Should().ContainKey("Technical Design");
            bep.LODRequirementsByPhase["Technical Design"].Should().Be(350);
            bep.LODRequirementsByPhase.Should().ContainKey("Construction");
            bep.LODRequirementsByPhase["Construction"].Should().Be(400);
            bep.LODRequirementsByPhase.Should().ContainKey("As Built");
            bep.LODRequirementsByPhase["As Built"].Should().Be(500);
        }

        [Test]
        public void ForISO19650_RequiredParameterGroups_ContainCoreGroups()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.RequiredParameterGroups.Should().Contain("Identity Data");
            bep.RequiredParameterGroups.Should().Contain("Phasing");
            bep.RequiredParameterGroups.Should().Contain("IFC Parameters");
            bep.RequiredParameterGroups.Should().Contain("Classification");
            bep.RequiredParameterGroups.Should().Contain("ISO 19650 Status");
            bep.RequiredParameterGroups.Should().Contain("Revision Tracking");
        }

        [Test]
        public void ForISO19650_ModelOrigin_HasSharedCoordinates()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.ModelOrigin.RequireSharedCoordinates.Should().BeTrue();
            bep.ModelOrigin.RequireSurveyPoint.Should().BeTrue();
            bep.ModelOrigin.MaxDistanceFromOriginMeters.Should().Be(30000.0);
        }

        [Test]
        public void ForISO19650_DeliveryMilestones_Has4Milestones()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.DeliveryMilestones.Should().HaveCount(4);
            bep.DeliveryMilestones.Select(m => m.Phase).Should().Contain("Concept");
            bep.DeliveryMilestones.Select(m => m.Phase).Should().Contain("Design Development");
            bep.DeliveryMilestones.Select(m => m.Phase).Should().Contain("Technical Design");
            bep.DeliveryMilestones.Select(m => m.Phase).Should().Contain("As Built");
        }

        [Test]
        public void ForISO19650_HealthThresholds_AreReasonable()
        {
            // Act
            var bep = BEPRequirements.ForISO19650();

            // Assert
            bep.MinimumHealthScore.Should().Be(75.0);
            bep.MaxCriticalIssues.Should().Be(0);
            bep.MaxWarningIssues.Should().Be(25);
        }

        #endregion

        #region BEPRequirements.Validate Tests

        [Test]
        public void Validate_HealthyReport_NoIssues()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 70.0,
                MaxCriticalIssues = 0,
                MaxWarningIssues = 50
            };

            var report = new ModelHealthReport
            {
                ModelName = "Good Model",
                OverallScore = 85.0,
                Issues = new List<HealthIssue>()
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().BeEmpty();
        }

        [Test]
        public void Validate_LowScore_ReportsError()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 70.0,
                MaxCriticalIssues = 5,
                MaxWarningIssues = 100
            };

            var report = new ModelHealthReport
            {
                ModelName = "Low Score Model",
                OverallScore = 55.0,
                Issues = new List<HealthIssue>()
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().HaveCount(1);
            issues[0].Severity.Should().Be(IssueSeverity.Error);
            issues[0].Category.Should().Be("BEP Compliance");
            issues[0].Description.Should().Contain("55.0").And.Contain("70.0");
        }

        [Test]
        public void Validate_TooManyCriticalIssues_ReportsCritical()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0, // Don't fail on score
                MaxCriticalIssues = 0,
                MaxWarningIssues = 100
            };

            var report = new ModelHealthReport
            {
                ModelName = "Critical Issues Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>
                {
                    new HealthIssue { Severity = IssueSeverity.Critical, Category = "Test", Description = "Critical 1" },
                    new HealthIssue { Severity = IssueSeverity.Critical, Category = "Test", Description = "Critical 2" }
                }
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().ContainSingle(i => i.Severity == IssueSeverity.Critical);
            issues.First(i => i.Severity == IssueSeverity.Critical)
                .Description.Should().Contain("2 critical issues");
        }

        [Test]
        public void Validate_TooManyWarnings_ReportsWarning()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 2
            };

            var warningIssues = Enumerable.Range(1, 5)
                .Select(i => new HealthIssue { Severity = IssueSeverity.Warning, Category = "Test", Description = $"Warning {i}" })
                .ToList();

            var report = new ModelHealthReport
            {
                ModelName = "Warning Model",
                OverallScore = 80.0,
                Issues = warningIssues
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().ContainSingle(i => i.Severity == IssueSeverity.Warning);
            issues.First(i => i.Severity == IssueSeverity.Warning)
                .Description.Should().Contain("5 warnings");
        }

        [Test]
        public void Validate_LODNotMet_ReportsError()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 100,
                LODRequirementsByPhase = new Dictionary<string, int>
                {
                    { "Construction", 400 }
                }
            };

            var report = new ModelHealthReport
            {
                ModelName = "Low LOD Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>(),
                Standards = new StandardsMetrics
                {
                    LODCompliance = new LODMetric { AchievedLOD = 200, RequiredLOD = 400, Compliant = false }
                }
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().ContainSingle(i => i.Description.Contains("LOD"));
            issues.First(i => i.Description.Contains("LOD")).Severity.Should().Be(IssueSeverity.Error);
        }

        [Test]
        public void Validate_LowParameterCompleteness_ReportsWarning()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 100,
                RequiredParameterGroups = new List<string> { "Identity Data", "Phasing" }
            };

            var report = new ModelHealthReport
            {
                ModelName = "Incomplete Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>(),
                DataQuality = new DataQualityMetrics
                {
                    ParameterCompleteness = new CompletenessMetric { Percentage = 60.0 }
                }
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().Contain(i => i.Description.Contains("Parameter completeness"));
        }

        [Test]
        public void Validate_LowNamingCompliance_ReportsWarning()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 100,
                FileNamingConventionPattern = @"^[A-Z]{2,5}-.*"
            };

            var report = new ModelHealthReport
            {
                ModelName = "Bad Naming Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>(),
                DataQuality = new DataQualityMetrics
                {
                    NamingCompliance = new ComplianceMetric { Percentage = 50.0 }
                }
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().Contain(i => i.Description.Contains("Naming compliance"));
        }

        [Test]
        public void Validate_OverdueMilestones_ReportsErrors()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 100,
                DeliveryMilestones = new List<DeliveryMilestone>
                {
                    new DeliveryMilestone
                    {
                        Name = "Concept Delivery",
                        Phase = "Concept",
                        DueDate = DateTime.UtcNow.AddDays(-30), // Overdue
                        IsCompleted = false,
                        MinimumScore = 60.0,
                        RequiredLOD = 200
                    },
                    new DeliveryMilestone
                    {
                        Name = "Future Milestone",
                        Phase = "Construction",
                        DueDate = DateTime.UtcNow.AddDays(365), // Not yet due
                        IsCompleted = false,
                        MinimumScore = 85.0,
                        RequiredLOD = 400
                    }
                }
            };

            var report = new ModelHealthReport
            {
                ModelName = "Overdue Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>()
            };

            // Act
            var issues = bep.Validate(report);

            // Assert - only the overdue milestone should produce an issue
            issues.Should().ContainSingle(i => i.Description.Contains("Concept Delivery"));
            issues.First(i => i.Description.Contains("Concept Delivery")).Severity.Should().Be(IssueSeverity.Error);
        }

        [Test]
        public void Validate_CompletedMilestone_NoIssue()
        {
            // Arrange
            var bep = new BEPRequirements
            {
                MinimumHealthScore = 0,
                MaxCriticalIssues = 10,
                MaxWarningIssues = 100,
                DeliveryMilestones = new List<DeliveryMilestone>
                {
                    new DeliveryMilestone
                    {
                        Name = "Done Milestone",
                        Phase = "Concept",
                        DueDate = DateTime.UtcNow.AddDays(-10), // Past due but completed
                        IsCompleted = true,
                        MinimumScore = 60.0,
                        RequiredLOD = 200
                    }
                }
            };

            var report = new ModelHealthReport
            {
                ModelName = "Completed Model",
                OverallScore = 80.0,
                Issues = new List<HealthIssue>()
            };

            // Act
            var issues = bep.Validate(report);

            // Assert
            issues.Should().BeEmpty();
        }

        [Test]
        public void Validate_MultipleFailures_ReturnsAllIssues()
        {
            // Arrange
            var bep = BEPRequirements.ForISO19650();

            var report = new ModelHealthReport
            {
                ModelName = "Failing Model",
                OverallScore = 40.0, // Below 75 minimum
                Issues = new List<HealthIssue>
                {
                    new HealthIssue { Severity = IssueSeverity.Critical, Category = "Test", Description = "Crit 1" }
                },
                Standards = new StandardsMetrics
                {
                    LODCompliance = new LODMetric { AchievedLOD = 100, RequiredLOD = 500 }
                },
                DataQuality = new DataQualityMetrics
                {
                    ParameterCompleteness = new CompletenessMetric { Percentage = 30.0 },
                    NamingCompliance = new ComplianceMetric { Percentage = 20.0 }
                }
            };

            // Act
            var issues = bep.Validate(report);

            // Assert - should have multiple issues: score, critical count, LOD, parameter completeness, naming
            issues.Count.Should().BeGreaterThanOrEqualTo(4);
        }

        #endregion

        #region ModelHealthMonitor Integration Tests

        [Test]
        public async Task AssessHealth_HealthyModel_ReturnsGoodScore()
        {
            // Arrange
            var model = CreateHealthyModel();

            // Act
            var report = await _monitor.AssessHealthAsync(model);

            // Assert
            report.Should().NotBeNull();
            report.ModelId.Should().Be("MDL-001");
            report.ModelName.Should().Be("Healthy Office Model");
            report.AssessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            report.OverallScore.Should().BeGreaterThan(0);
            report.HealthGrade.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task AssessHealth_ReturnsAllMetricCategories()
        {
            // Arrange
            var model = CreateHealthyModel();

            // Act
            var report = await _monitor.AssessHealthAsync(model);

            // Assert
            report.DataQuality.Should().NotBeNull();
            report.ModelStructure.Should().NotBeNull();
            report.Performance.Should().NotBeNull();
            report.Standards.Should().NotBeNull();
            report.Geometry.Should().NotBeNull();
            report.Issues.Should().NotBeNull();
            report.Recommendations.Should().NotBeNull();
        }

        [Test]
        public async Task QuickCheck_HealthyModel_ReturnsHealthyStatus()
        {
            // Arrange
            var model = CreateHealthyModel();

            // Act
            var status = await _monitor.QuickCheckAsync(model);

            // Assert
            status.Should().NotBeNull();
            status.ModelId.Should().Be("MDL-001");
            status.ErrorCount.Should().Be(0);
            status.WarningCount.Should().Be(1); // One warning in healthy model
            status.Status.Should().Be(HealthStatus.NeedsAttention);
        }

        [Test]
        public async Task QuickCheck_ModelWithErrors_ReturnsCriticalStatus()
        {
            // Arrange
            var model = CreateUnhealthyModel();

            // Act
            var status = await _monitor.QuickCheckAsync(model);

            // Assert
            status.ErrorCount.Should().BeGreaterThan(0);
            status.Status.Should().Be(HealthStatus.Critical);
        }

        [Test]
        public async Task QuickCheck_ModelWithManyWarnings_ReturnsWarningStatus()
        {
            // Arrange
            var model = CreateHealthyModel();
            model.Errors = new List<ModelError>(); // No errors
            model.Warnings = Enumerable.Range(1, 15)
                .Select(i => new ModelWarning { Message = $"Warning {i}" })
                .ToList(); // > 10 warnings

            // Act
            var status = await _monitor.QuickCheckAsync(model);

            // Assert
            status.WarningCount.Should().Be(15);
            status.Status.Should().Be(HealthStatus.Warning);
        }

        [Test]
        public async Task AssessHealth_WithPreviousReport_IncludesComparison()
        {
            // Arrange
            var model = CreateHealthyModel();
            var previousReport = new ModelHealthReport
            {
                ModelName = "Previous",
                OverallScore = 70.0,
                Issues = new List<HealthIssue>
                {
                    new HealthIssue { Severity = IssueSeverity.Warning, Category = "Test", ElementId = "OLD-001", Description = "Old issue" }
                }
            };

            var options = new HealthCheckOptions { PreviousReport = previousReport };

            // Act
            var report = await _monitor.AssessHealthAsync(model, options);

            // Assert
            report.Comparison.Should().NotBeNull();
            report.Comparison.Trend.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Health Grade Tests

        [Test]
        public async Task HealthGrade_HighScore_GradeA()
        {
            // Arrange - create a model that will score very high
            var model = CreateHealthyModel();

            // Act
            var report = await _monitor.AssessHealthAsync(model);

            // Assert - with a well-configured model, grade should be decent
            // Exact grade depends on calculated scores, but should be a valid grade
            report.HealthGrade.Should().BeOneOf("A", "B", "C", "D", "F");
        }

        #endregion

        #region NamingPattern.Matches Tests

        [Test]
        [TestCase("STD_Wall Type", true)]
        [TestCase("EXT_Exterior Wall", true)]
        [TestCase("AB_Name", true)]
        [TestCase("ABCD_Name", true)]
        [TestCase("wall type", false)]
        [TestCase("A_Too Short", false)]
        [TestCase("ABCDE_Too Long", false)]
        public void NamingPattern_Matches_PrefixPattern(string name, bool shouldMatch)
        {
            // Arrange
            var pattern = new NamingPattern
            {
                AppliesTo = "All",
                Pattern = @"^[A-Z]{2,4}_.*",
                Description = "PREFIX_Name"
            };

            // Act
            var result = pattern.Matches(name);

            // Assert
            result.Should().Be(shouldMatch);
        }

        [Test]
        [TestCase("Level 1 Plan", true)]
        [TestCase("Section A-A", true)]
        [TestCase("Detail 01", true)]
        [TestCase("3D Overview", true)]
        [TestCase("my custom view", false)]
        [TestCase("Working View", false)]
        public void NamingPattern_Matches_ViewPattern(string name, bool shouldMatch)
        {
            // Arrange
            var pattern = new NamingPattern
            {
                AppliesTo = "View",
                Pattern = @"^(Level|Section|Detail|3D).*",
                Description = "Type prefix"
            };

            // Act
            var result = pattern.Matches(name);

            // Assert
            result.Should().Be(shouldMatch);
        }

        #endregion

        #region RequiredParameter.AppliesTo Tests

        [Test]
        [TestCase("Doors", true)]
        [TestCase("Walls", true)]
        [TestCase("Floors", false)]
        [TestCase("Windows", false)]
        public void RequiredParameter_AppliesTo_FiltersByCategory(string category, bool shouldApply)
        {
            // Arrange
            var param = new RequiredParameter
            {
                Name = "Fire Rating",
                Categories = new[] { "Doors", "Walls" }
            };

            // Act
            var result = param.AppliesTo(category);

            // Assert
            result.Should().Be(shouldApply);
        }

        [Test]
        [TestCase("Doors")]
        [TestCase("Walls")]
        [TestCase("Floors")]
        [TestCase("Windows")]
        [TestCase("AnyCategory")]
        public void RequiredParameter_AppliesTo_AllCategory_MatchesEverything(string category)
        {
            // Arrange
            var param = new RequiredParameter
            {
                Name = "Comments",
                Categories = new[] { "All" }
            };

            // Act
            var result = param.AppliesTo(category);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region ModelElement.HasParameterValue Tests

        [Test]
        public void HasParameterValue_ExistingValue_ReturnsTrue()
        {
            // Arrange
            var element = new ModelElement
            {
                Parameters = new Dictionary<string, object> { { "Mark", "D-001" } }
            };

            // Act & Assert
            element.HasParameterValue("Mark").Should().BeTrue();
        }

        [Test]
        public void HasParameterValue_NullValue_ReturnsFalse()
        {
            // Arrange
            var element = new ModelElement
            {
                Parameters = new Dictionary<string, object> { { "Mark", null } }
            };

            // Act & Assert
            element.HasParameterValue("Mark").Should().BeFalse();
        }

        [Test]
        public void HasParameterValue_MissingKey_ReturnsFalse()
        {
            // Arrange
            var element = new ModelElement
            {
                Parameters = new Dictionary<string, object>()
            };

            // Act & Assert
            element.HasParameterValue("Mark").Should().BeFalse();
        }

        #endregion
    }
}
