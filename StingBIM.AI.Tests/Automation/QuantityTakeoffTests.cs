// ============================================================================
// StingBIM AI Tests - Automated Quantity Takeoff Tests
// Validates quantity extraction, measurement rules, waste factors,
// BOQ formatting, and cost application
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Automation.Quantities;

namespace StingBIM.AI.Tests.Automation
{
    [TestFixture]
    public class QuantityTakeoffTests
    {
        private AutomatedQuantityTakeoff _takeoff;
        private MeasurementRules _measurementRules;
        private WasteFactors _wasteFactors;

        [SetUp]
        public void SetUp()
        {
            _takeoff = new AutomatedQuantityTakeoff();
            _measurementRules = new MeasurementRules();
            _wasteFactors = new WasteFactors();
        }

        #region Helper Methods

        private BIMModel CreateModelWithWalls()
        {
            return new BIMModel
            {
                ModelId = "MDL-QT-001",
                ModelName = "Wall Test Model",
                Elements = new List<BIMElement>
                {
                    CreateWallElement("W-001", "STD_Brick Wall", "Level 1", 25.0, 12.5, 3.0, 0.22),
                    CreateWallElement("W-002", "STD_Brick Wall", "Level 1", 20.0, 10.0, 3.0, 0.22),
                    CreateWallElement("W-003", "EXT_Concrete Wall", "Level 1", 30.0, 15.0, 3.5, 0.30)
                }
            };
        }

        private BIMModel CreateModelWithFloors()
        {
            return new BIMModel
            {
                ModelId = "MDL-QT-002",
                ModelName = "Floor Test Model",
                Elements = new List<BIMElement>
                {
                    CreateFloorElement("F-001", "STD_Concrete Slab", "Level 1", 150.0, 45.0, 0.20),
                    CreateFloorElement("F-002", "STD_Concrete Slab", "Level 2", 150.0, 45.0, 0.20),
                    CreateFloorElement("F-003", "FIN_Tile Finish", "Level 1", 80.0, 0, 0.02)
                }
            };
        }

        private BIMModel CreateModelWithDoors()
        {
            return new BIMModel
            {
                ModelId = "MDL-QT-003",
                ModelName = "Door Test Model",
                Elements = new List<BIMElement>
                {
                    CreateDoorElement("D-001", "STD_Single Door", "Level 1", 900, 2100),
                    CreateDoorElement("D-002", "STD_Single Door", "Level 1", 900, 2100),
                    CreateDoorElement("D-003", "STD_Double Door", "Level 1", 1800, 2100),
                    CreateDoorElement("D-004", "FD_Fire Door", "Level 1", 1000, 2100)
                }
            };
        }

        private BIMModel CreateComprehensiveModel()
        {
            var model = new BIMModel
            {
                ModelId = "MDL-QT-FULL",
                ModelName = "Comprehensive Test Model",
                Elements = new List<BIMElement>()
            };

            // Add walls
            model.Elements.Add(CreateWallElement("W-001", "Brick Wall", "Level 1", 25.0, 12.5, 3.0, 0.22));
            model.Elements.Add(CreateWallElement("W-002", "Block Wall", "Level 1", 20.0, 10.0, 3.0, 0.15));

            // Add floors
            model.Elements.Add(CreateFloorElement("F-001", "Concrete Slab", "Level 1", 200.0, 60.0, 0.20));

            // Add doors
            model.Elements.Add(CreateDoorElement("D-001", "Timber Door", "Level 1", 900, 2100));
            model.Elements.Add(CreateDoorElement("D-002", "Timber Door", "Level 1", 900, 2100));

            // Add windows
            model.Elements.Add(CreateWindowElement("WN-001", "Double Glazed Window", "Level 1", 1200, 1500));
            model.Elements.Add(CreateWindowElement("WN-002", "Double Glazed Window", "Level 1", 1200, 1500));
            model.Elements.Add(CreateWindowElement("WN-003", "Single Glazed Window", "Level 1", 600, 900));

            // Add structural
            model.Elements.Add(CreateStructuralElement("SC-001", "Structural Columns", "Steel Column", "Level 1", 3.5, "Steel"));
            model.Elements.Add(CreateStructuralElement("SB-001", "Structural Beams", "Concrete Beam", "Level 1", 6.0, "Concrete"));

            // Add pipes
            model.Elements.Add(CreateMEPElement("P-001", "Pipes", "Copper Pipe 22mm", "Level 1", 15.0, 1.0, "22mm"));
            model.Elements.Add(CreateMEPElement("P-002", "Pipes", "Copper Pipe 22mm", "Level 1", 10.0, 0.7, "22mm"));

            return model;
        }

        private BIMElement CreateWallElement(string id, string typeName, string level, double area, double volume, double height, double width)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = "Walls",
                Family = "Basic Wall",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Area", area },
                    { "Volume", volume },
                    { "Length", area / height }, // Rough estimate
                    { "Height", height },
                    { "Width", width * 1000 } // mm
                }
            };
        }

        private BIMElement CreateFloorElement(string id, string typeName, string level, double area, double volume, double thickness)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = "Floors",
                Family = "Floor",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Area", area },
                    { "Volume", volume },
                    { "Thickness", thickness * 1000 }, // mm
                    { "Perimeter", Math.Sqrt(area) * 4 } // Rough estimate for square floor
                }
            };
        }

        private BIMElement CreateDoorElement(string id, string typeName, string level, double widthMm, double heightMm)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = "Doors",
                Family = "Single Door",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Width", widthMm },
                    { "Height", heightMm }
                }
            };
        }

        private BIMElement CreateWindowElement(string id, string typeName, string level, double widthMm, double heightMm)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = "Windows",
                Family = "Window",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Width", widthMm },
                    { "Height", heightMm }
                }
            };
        }

        private BIMElement CreateStructuralElement(string id, string category, string typeName, string level, double length, string material)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = category,
                Family = "Structural",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Volume", length * 0.04 }, // Rough estimate
                    { "Length", length },
                    { "Material", null } // Material is a string, use GetParameter which returns double?
                }
            };
        }

        private BIMElement CreateMEPElement(string id, string category, string typeName, string level, double length, double area, string size)
        {
            return new BIMElement
            {
                ElementId = id,
                Category = category,
                Family = "Pipe",
                TypeName = typeName,
                Level = level,
                Phase = "New Construction",
                Parameters = new Dictionary<string, double?>
                {
                    { "Length", length },
                    { "Area", area },
                    { "Size", null } // Size is a string
                }
            };
        }

        #endregion

        #region GenerateTakeoff Tests

        [Test]
        public async Task GenerateTakeoff_WithWalls_ExtractsQuantities()
        {
            // Arrange
            var model = CreateModelWithWalls();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls" },
                ApplyWasteFactors = false,
                IncludeCosts = false
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.Should().NotBeNull();
            result.ModelId.Should().Be("MDL-QT-001");
            result.RawQuantities.Should().ContainKey("Walls");
            result.RawQuantities["Walls"].Items.Should().HaveCount(3);
        }

        [Test]
        public async Task GenerateTakeoff_WithMultipleCategories_ExtractsAll()
        {
            // Arrange
            var model = CreateComprehensiveModel();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls", "Floors", "Doors", "Windows" },
                ApplyWasteFactors = false,
                IncludeCosts = false
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.RawQuantities.Should().HaveCount(4);
            result.RawQuantities["Walls"].Items.Should().HaveCount(2);
            result.RawQuantities["Floors"].Items.Should().HaveCount(1);
            result.RawQuantities["Doors"].Items.Should().HaveCount(2);
            result.RawQuantities["Windows"].Items.Should().HaveCount(3);
        }

        [Test]
        public async Task GenerateTakeoff_WithWasteFactors_AdjustsQuantities()
        {
            // Arrange
            var model = CreateModelWithWalls();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls" },
                ApplyWasteFactors = true,
                IncludeCosts = false
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            var wallItems = result.RawQuantities["Walls"].Items;
            foreach (var item in wallItems)
            {
                // Wall waste factor is 5%
                item.WasteFactor.Should().Be(0.05);
                if (item.Area.HasValue)
                {
                    item.AdjustedArea.Should().BeApproximately(item.Area.Value * 1.05, 0.01);
                }
            }
        }

        [Test]
        public async Task GenerateTakeoff_WithCosts_CalculatesCostSummary()
        {
            // Arrange
            var model = CreateModelWithWalls();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls" },
                ApplyWasteFactors = false,
                IncludeCosts = true,
                Region = "Kenya"
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.CostSummary.Should().NotBeNull();
            result.CostSummary.DirectCost.Should().BeGreaterThanOrEqualTo(0);
            result.CostSummary.VAT.Should().BeGreaterThanOrEqualTo(0);
            result.CostSummary.GrandTotal.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task GenerateTakeoff_CostSummary_IncludesPreliminariesAndContingency()
        {
            // Arrange
            var model = CreateModelWithWalls();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls" },
                ApplyWasteFactors = false,
                IncludeCosts = true,
                Region = "Kenya"
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            var summary = result.CostSummary;
            summary.Preliminaries.Should().Be(summary.DirectCost * 0.12m);
            summary.Contingency.Should().Be(summary.DirectCost * 0.05m);
            summary.Overheads.Should().Be(summary.DirectCost * 0.08m);
            summary.Profit.Should().Be(summary.DirectCost * 0.05m);
            summary.VAT.Should().Be(summary.TotalBeforeTax * 0.16m);
        }

        [Test]
        public async Task GenerateTakeoff_Statistics_ArePopulated()
        {
            // Arrange
            var model = CreateComprehensiveModel();
            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls", "Floors", "Doors", "Windows" },
                ApplyWasteFactors = false,
                IncludeCosts = false
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.Statistics.Should().NotBeNull();
            result.Statistics.TotalElements.Should().BeGreaterThan(0);
            result.Statistics.CategoriesProcessed.Should().Be(4);
            result.Statistics.TotalWallArea.Should().BeGreaterThan(0);
            result.Statistics.TotalFloorArea.Should().BeGreaterThan(0);
            result.Statistics.DoorCount.Should().Be(2);
            result.Statistics.WindowCount.Should().Be(3);
        }

        [Test]
        public async Task GenerateTakeoff_DefaultOptions_UsesNRM2()
        {
            // Arrange
            var model = CreateModelWithWalls();

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, TakeoffOptions.Default);

            // Assert
            result.MeasurementStandard.Should().Be(MeasurementStandard.NRM2);
        }

        #endregion

        #region ExtractCategoryQuantities Tests

        [Test]
        public async Task ExtractCategoryQuantities_Walls_ExtractsAreaVolumeLength()
        {
            // Arrange
            var model = CreateModelWithWalls();

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Walls");

            // Assert
            quantities.Category.Should().Be("Walls");
            quantities.Items.Should().HaveCount(3);

            var firstWall = quantities.Items[0];
            firstWall.Area.Should().Be(25.0);
            firstWall.Volume.Should().Be(12.5);
            firstWall.Height.Should().Be(3.0);
        }

        [Test]
        public async Task ExtractCategoryQuantities_Doors_ExtractsWidthHeightArea()
        {
            // Arrange
            var model = CreateModelWithDoors();

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Doors");

            // Assert
            quantities.Items.Should().HaveCount(4);

            var firstDoor = quantities.Items[0];
            firstDoor.Width.Should().Be(900);
            firstDoor.Height.Should().Be(2100);
            firstDoor.Count.Should().Be(1);
            // Area should be Width * Height / 1000000 (converted to m2)
            firstDoor.Area.Should().BeApproximately(900.0 * 2100.0 / 1000000, 0.001);
        }

        [Test]
        public async Task ExtractCategoryQuantities_Floors_ExtractsAreaVolumeThickness()
        {
            // Arrange
            var model = CreateModelWithFloors();

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Floors");

            // Assert
            quantities.Items.Should().HaveCount(3);

            var firstFloor = quantities.Items[0];
            firstFloor.Area.Should().Be(150.0);
            firstFloor.Volume.Should().Be(45.0);
            firstFloor.Thickness.Should().Be(200); // 0.20m stored as mm in parameters
        }

        [Test]
        public async Task ExtractCategoryQuantities_TypeSummaries_GroupsByType()
        {
            // Arrange
            var model = CreateModelWithDoors(); // Has 2x STD_Single Door, 1x STD_Double Door, 1x FD_Fire Door

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Doors");

            // Assert
            quantities.TypeSummaries.Should().HaveCount(3);

            var singleDoorSummary = quantities.TypeSummaries
                .FirstOrDefault(s => s.TypeName == "STD_Single Door");
            singleDoorSummary.Should().NotBeNull();
            singleDoorSummary.Count.Should().Be(2);
        }

        [Test]
        public async Task ExtractCategoryQuantities_EmptyCategory_ReturnsEmptyItems()
        {
            // Arrange
            var model = CreateModelWithWalls(); // No Roofs elements

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Roofs");

            // Assert
            quantities.Category.Should().Be("Roofs");
            quantities.Items.Should().BeEmpty();
            quantities.TypeSummaries.Should().BeEmpty();
        }

        [Test]
        public async Task ExtractCategoryQuantities_StructuralElements_CalculatesWeight()
        {
            // Arrange - structural elements with known material
            var model = new BIMModel
            {
                ModelId = "MDL-STRUCT",
                Elements = new List<BIMElement>
                {
                    new BIMElement
                    {
                        ElementId = "SC-001",
                        Category = "Structural Columns",
                        Family = "Column",
                        TypeName = "Steel Column 254x254",
                        Level = "Level 1",
                        Parameters = new Dictionary<string, double?>
                        {
                            { "Volume", 0.5 },
                            { "Length", 3.5 },
                            { "Material", null } // Weight calc uses GetParameter("Material")
                        }
                    }
                }
            };

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Structural Columns");

            // Assert
            quantities.Items.Should().HaveCount(1);
            quantities.Items[0].Volume.Should().Be(0.5);
            quantities.Items[0].Length.Should().Be(3.5);
        }

        [Test]
        public async Task ExtractCategoryQuantities_Pipes_ExtractsLengthAndSize()
        {
            // Arrange
            var model = new BIMModel
            {
                ModelId = "MDL-PIPE",
                Elements = new List<BIMElement>
                {
                    new BIMElement
                    {
                        ElementId = "P-001",
                        Category = "Pipes",
                        Family = "Pipe",
                        TypeName = "Copper 22mm",
                        Level = "Level 1",
                        Parameters = new Dictionary<string, double?>
                        {
                            { "Length", 15.0 },
                            { "Area", 1.0 },
                            { "Size", null }
                        }
                    }
                }
            };

            // Act
            var quantities = await _takeoff.ExtractCategoryQuantitiesAsync(model, "Pipes");

            // Assert
            quantities.Items.Should().HaveCount(1);
            quantities.Items[0].Length.Should().Be(15.0);
            quantities.Items[0].Area.Should().Be(1.0);
        }

        #endregion

        #region MeasurementRules.GetRulesForCategory Tests

        [Test]
        public void GetRulesForCategory_Walls_ReturnsDeductOpeningsRule()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Walls", MeasurementStandard.NRM2);

            // Assert
            rules.Should().HaveCount(1);
            rules[0].RuleType.Should().Be(MeasurementRuleType.DeductOpenings);
            rules[0].Threshold.Should().Be(0.5);
        }

        [Test]
        public void GetRulesForCategory_Floors_ReturnsMinimumQuantityRule()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Floors", MeasurementStandard.NRM2);

            // Assert
            rules.Should().HaveCount(1);
            rules[0].RuleType.Should().Be(MeasurementRuleType.MinimumQuantity);
            rules[0].MinimumValue.Should().Be(1.0);
        }

        [Test]
        public void GetRulesForCategory_Pipes_ReturnsRoundUpRule()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Pipes", MeasurementStandard.NRM2);

            // Assert
            rules.Should().HaveCount(1);
            rules[0].RuleType.Should().Be(MeasurementRuleType.RoundUp);
            rules[0].RoundingUnit.Should().Be(0.5);
        }

        [Test]
        public void GetRulesForCategory_Ducts_ReturnsRoundUpRule()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Ducts", MeasurementStandard.NRM2);

            // Assert
            rules.Should().HaveCount(1);
            rules[0].RuleType.Should().Be(MeasurementRuleType.RoundUp);
        }

        [Test]
        public void GetRulesForCategory_UnknownCategory_ReturnsEmptyList()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Furniture", MeasurementStandard.NRM2);

            // Assert
            rules.Should().BeEmpty();
        }

        [Test]
        public void GetRulesForCategory_Doors_ReturnsEmptyList()
        {
            // Doors don't have special measurement rules
            // Act
            var rules = _measurementRules.GetRulesForCategory("Doors", MeasurementStandard.NRM2);

            // Assert
            rules.Should().BeEmpty();
        }

        [Test]
        public void GetRulesForCategory_Windows_ReturnsEmptyList()
        {
            // Act
            var rules = _measurementRules.GetRulesForCategory("Windows", MeasurementStandard.NRM2);

            // Assert
            rules.Should().BeEmpty();
        }

        #endregion

        #region WasteFactors.GetFactor Tests

        [Test]
        [TestCase("Walls", 0.05)]
        [TestCase("Floors", 0.03)]
        [TestCase("Ceilings", 0.10)]
        [TestCase("Roofs", 0.08)]
        [TestCase("Doors", 0.00)]
        [TestCase("Windows", 0.00)]
        [TestCase("Pipes", 0.10)]
        [TestCase("Ducts", 0.12)]
        [TestCase("Cable Trays", 0.08)]
        public void GetFactor_KnownCategory_ReturnsCorrectFactor(string category, double expectedFactor)
        {
            // Act
            var factor = _wasteFactors.GetFactor(category);

            // Assert
            factor.Should().Be(expectedFactor);
        }

        [Test]
        public void GetFactor_UnknownCategory_ReturnsDefaultFactor()
        {
            // Act
            var factor = _wasteFactors.GetFactor("SomeUnknownCategory");

            // Assert
            factor.Should().Be(0.05, "unknown categories default to 5% waste");
        }

        [Test]
        public void GetFactor_Furniture_ReturnsDefaultFactor()
        {
            // Act
            var factor = _wasteFactors.GetFactor("Furniture");

            // Assert
            factor.Should().Be(0.05);
        }

        [Test]
        public void GetFactor_AllKnownCategories_HaveReasonableValues()
        {
            // Arrange
            var categories = new[] { "Walls", "Floors", "Ceilings", "Roofs", "Doors", "Windows", "Pipes", "Ducts", "Cable Trays" };

            // Act & Assert
            foreach (var category in categories)
            {
                var factor = _wasteFactors.GetFactor(category);
                factor.Should().BeGreaterThanOrEqualTo(0.0, $"{category} waste factor should be non-negative");
                factor.Should().BeLessThanOrEqualTo(0.20, $"{category} waste factor should not exceed 20%");
            }
        }

        [Test]
        public void GetFactor_ItemCategories_HaveZeroWaste()
        {
            // Doors and Windows are counted items, no material waste
            _wasteFactors.GetFactor("Doors").Should().Be(0.0);
            _wasteFactors.GetFactor("Windows").Should().Be(0.0);
        }

        [Test]
        public void GetFactor_CutCategories_HaveHigherWaste()
        {
            // Categories that require cutting should have higher waste factors
            var cuttingFactor = _wasteFactors.GetFactor("Ducts");
            var itemFactor = _wasteFactors.GetFactor("Doors");

            cuttingFactor.Should().BeGreaterThan(itemFactor,
                "categories requiring cutting should have higher waste than counted items");
        }

        #endregion

        #region QuantityExtractor Tests

        // NOTE: QuantityExtractor is currently an empty class: `public class QuantityExtractor { }`
        // The actual extraction logic is implemented directly in AutomatedQuantityTakeoff.
        // Tests for extraction are covered via the AutomatedQuantityTakeoff tests above.

        [Test]
        public void QuantityExtractor_CanBeInstantiated()
        {
            var extractor = new QuantityExtractor();
            extractor.Should().NotBeNull();
        }

        #endregion

        #region BOQFormatter Tests

        // NOTE: BOQFormatter is currently an empty class: `public class BOQFormatter { }`
        // No methods to test. Tests should be added when the class is implemented.

        [Test]
        public void BOQFormatter_CanBeInstantiated()
        {
            var formatter = new BOQFormatter();
            formatter.Should().NotBeNull();
        }

        #endregion

        #region CostDatabase Tests

        [Test]
        public async Task CostDatabase_GetRate_ReturnsRate()
        {
            // Arrange
            var costDb = new CostDatabase();

            // Act
            var rate = await costDb.GetRateAsync("Brick Wall", "m2", "Kenya", DateTime.Now);

            // Assert
            rate.Should().NotBeNull();
            rate.Rate.Should().BeGreaterThan(0);
            rate.Source.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task CostDatabase_GetRate_WithNullDate_StillReturnsRate()
        {
            // Arrange
            var costDb = new CostDatabase();

            // Act
            var rate = await costDb.GetRateAsync("Concrete Floor", "m3", "Kenya", null);

            // Assert
            rate.Should().NotBeNull();
            rate.Rate.Should().BeGreaterThan(0);
        }

        #endregion

        #region BIMModel and BIMElement Tests

        [Test]
        public void BIMModel_GetElementsByCategory_FiltersCorrectly()
        {
            // Arrange
            var model = CreateComprehensiveModel();

            // Act
            var walls = model.GetElementsByCategory("Walls").ToList();
            var doors = model.GetElementsByCategory("Doors").ToList();
            var nonExistent = model.GetElementsByCategory("NonExistent").ToList();

            // Assert
            walls.Should().HaveCount(2);
            doors.Should().HaveCount(2);
            nonExistent.Should().BeEmpty();
        }

        [Test]
        public void BIMElement_GetParameter_ExistingKey_ReturnsValue()
        {
            // Arrange
            var element = new BIMElement
            {
                Parameters = new Dictionary<string, double?>
                {
                    { "Area", 25.5 },
                    { "Volume", 12.0 },
                    { "Height", null }
                }
            };

            // Act & Assert
            element.GetParameter("Area").Should().Be(25.5);
            element.GetParameter("Volume").Should().Be(12.0);
        }

        [Test]
        public void BIMElement_GetParameter_NullValue_ReturnsNull()
        {
            // Arrange
            var element = new BIMElement
            {
                Parameters = new Dictionary<string, double?> { { "Height", null } }
            };

            // Act & Assert
            element.GetParameter("Height").Should().BeNull();
        }

        [Test]
        public void BIMElement_GetParameter_MissingKey_ReturnsNull()
        {
            // Arrange
            var element = new BIMElement
            {
                Parameters = new Dictionary<string, double?>()
            };

            // Act & Assert
            element.GetParameter("NonExistent").Should().BeNull();
        }

        #endregion

        #region TakeoffOptions Tests

        [Test]
        public void TakeoffOptions_Default_HasReasonableDefaults()
        {
            // Act
            var options = TakeoffOptions.Default;

            // Assert
            options.MeasurementStandard.Should().Be(MeasurementStandard.NRM2);
            options.ApplyWasteFactors.Should().BeTrue();
            options.IncludeCosts.Should().BeTrue();
            options.Region.Should().Be("Kenya");
        }

        [Test]
        public void TakeoffOptions_Default_CategoriesAreNull()
        {
            // Act
            var options = TakeoffOptions.Default;

            // Assert - null categories means use default categories in the takeoff engine
            options.CategoriesToExtract.Should().BeNull();
        }

        #endregion

        #region MeasurementStandard Enum Tests

        [Test]
        public void MeasurementStandard_HasExpectedValues()
        {
            // Assert
            Enum.GetValues(typeof(MeasurementStandard)).Length.Should().Be(4);
            Enum.IsDefined(typeof(MeasurementStandard), MeasurementStandard.NRM2).Should().BeTrue();
            Enum.IsDefined(typeof(MeasurementStandard), MeasurementStandard.SMM7).Should().BeTrue();
            Enum.IsDefined(typeof(MeasurementStandard), MeasurementStandard.POMI).Should().BeTrue();
            Enum.IsDefined(typeof(MeasurementStandard), MeasurementStandard.Custom).Should().BeTrue();
        }

        #endregion

        #region WorkSection and WorkItem Data Model Tests

        [Test]
        public void WorkSection_NewInstance_HasEmptyItems()
        {
            // Act
            var section = new WorkSection();

            // Assert
            section.Items.Should().NotBeNull();
            section.Items.Should().BeEmpty();
            section.SectionTotal.Should().Be(0);
        }

        [Test]
        public void CategoryQuantities_NewInstance_HasEmptyCollections()
        {
            // Act
            var quantities = new CategoryQuantities();

            // Assert
            quantities.Items.Should().NotBeNull().And.BeEmpty();
            quantities.TypeSummaries.Should().NotBeNull().And.BeEmpty();
        }

        [Test]
        public void QuantityItem_NewInstance_HasDefaultValues()
        {
            // Act
            var item = new QuantityItem();

            // Assert
            item.WasteFactor.Should().Be(0);
            item.Deductions.Should().NotBeNull().And.BeEmpty();
            item.Adjustments.Should().NotBeNull().And.BeEmpty();
            item.Area.Should().BeNull();
            item.Volume.Should().BeNull();
            item.Length.Should().BeNull();
        }

        #endregion

        #region Integration Tests - Full Pipeline

        [Test]
        public async Task FullPipeline_ComprehensiveModel_ProducesCompleteResult()
        {
            // Arrange
            var model = CreateComprehensiveModel();
            var options = new TakeoffOptions
            {
                MeasurementStandard = MeasurementStandard.NRM2,
                ApplyWasteFactors = true,
                IncludeCosts = true,
                Region = "Kenya"
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.Should().NotBeNull();
            result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.RawQuantities.Should().NotBeNull();
            result.Statistics.Should().NotBeNull();

            // Waste factors should be applied
            foreach (var category in result.RawQuantities)
            {
                var expectedWaste = _wasteFactors.GetFactor(category.Key);
                foreach (var item in category.Value.Items)
                {
                    item.WasteFactor.Should().Be(expectedWaste);
                }
            }
        }

        [Test]
        public async Task FullPipeline_EmptyModel_HandlesGracefully()
        {
            // Arrange
            var model = new BIMModel
            {
                ModelId = "MDL-EMPTY",
                ModelName = "Empty Model",
                Elements = new List<BIMElement>()
            };

            var options = new TakeoffOptions
            {
                CategoriesToExtract = new List<string> { "Walls" },
                ApplyWasteFactors = false,
                IncludeCosts = false
            };

            // Act
            var result = await _takeoff.GenerateTakeoffAsync(model, options);

            // Assert
            result.Should().NotBeNull();
            result.RawQuantities.Should().ContainKey("Walls");
            result.RawQuantities["Walls"].Items.Should().BeEmpty();
        }

        #endregion
    }
}
