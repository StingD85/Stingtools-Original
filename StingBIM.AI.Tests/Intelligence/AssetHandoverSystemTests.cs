// ============================================================================
// StingBIM AI Tests - Asset Handover System Tests
// Unit tests for COBie generation, asset registration, and O&M documentation
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StingBIM.AI.Intelligence.AssetHandover;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class AssetHandoverSystemTests
    {
        private AssetHandoverSystem _system;

        [SetUp]
        public void Setup()
        {
            _system = AssetHandoverSystem.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = AssetHandoverSystem.Instance;
            var instance2 = AssetHandoverSystem.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Project Setup Tests

        [Test]
        public void InitializeProject_ShouldCreateProject()
        {
            // Arrange
            var setup = new ProjectSetup
            {
                ProjectName = "Downtown Office Tower",
                ClientName = "ABC Development Corp",
                SiteName = "123 Main Street",
                FacilityName = "Office Tower A",
                ProjectNumber = "PRJ-2024-001",
                Description = "20-story commercial office building",
                Region = "North America",
                Currency = "USD",
                AreaUnit = "SF",
                LengthUnit = "FT"
            };

            // Act
            var project = _system.InitializeProject(setup);

            // Assert
            Assert.That(project, Is.Not.Null);
            Assert.That(project.ProjectId, Is.Not.Null);
            Assert.That(project.ProjectName, Is.EqualTo("Downtown Office Tower"));
            Assert.That(project.ProjectNumber, Is.EqualTo("PRJ-2024-001"));
        }

        #endregion

        #region Floor and Space Tests

        [Test]
        public void RegisterFloor_ShouldCreateFloor()
        {
            // Arrange
            var floorData = new FloorData
            {
                Name = "Level 5",
                Description = "Fifth Floor - Open Office",
                Elevation = 18.0,
                Height = 3.6,
                GrossArea = 2500.0
            };

            // Act
            var floor = _system.RegisterFloor(floorData);

            // Assert
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor.FloorId, Is.Not.Null);
            Assert.That(floor.Name, Is.EqualTo("Level 5"));
            Assert.That(floor.Elevation, Is.EqualTo(18.0));
        }

        [Test]
        public void RegisterSpace_ShouldCreateSpace()
        {
            // Arrange
            var floor = _system.RegisterFloor(new FloorData { Name = "Level 1" });
            var spaceData = new SpaceData
            {
                Name = "Conference Room 101",
                RoomNumber = "101",
                Category = "Office",
                FloorId = floor.FloorId,
                UsableArea = 45.0,
                GrossArea = 50.0,
                Description = "Large conference room"
            };

            // Act
            var space = _system.RegisterSpace(spaceData);

            // Assert
            Assert.That(space, Is.Not.Null);
            Assert.That(space.SpaceId, Is.Not.Null);
            Assert.That(space.Name, Is.EqualTo("Conference Room 101"));
            Assert.That(space.FloorId, Is.EqualTo(floor.FloorId));
        }

        [Test]
        public void RegisterZone_ShouldCreateZone()
        {
            // Arrange
            var zoneData = new ZoneData
            {
                Name = "HVAC Zone A",
                Category = "HVAC",
                Description = "North wing HVAC zone"
            };

            // Act
            var zone = _system.RegisterZone(zoneData);

            // Assert
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.ZoneId, Is.Not.Null);
            Assert.That(zone.Name, Is.EqualTo("HVAC Zone A"));
        }

        #endregion

        #region Asset Type Tests

        [Test]
        public void RegisterAssetType_ShouldCreateType()
        {
            // Arrange
            var typeData = new AssetTypeData
            {
                Name = "Variable Air Volume Box",
                Category = "HVAC",
                Manufacturer = "Trane",
                ModelNumber = "TMVB-500",
                Description = "VAV box with hot water reheat",
                ExpectedLife = 20,
                WarrantyDuration = 24,
                WarrantyDescription = "Parts and labor"
            };

            // Act
            var assetType = _system.RegisterAssetType(typeData);

            // Assert
            Assert.That(assetType, Is.Not.Null);
            Assert.That(assetType.TypeId, Is.Not.Null);
            Assert.That(assetType.Name, Is.EqualTo("Variable Air Volume Box"));
            Assert.That(assetType.Manufacturer, Is.EqualTo("Trane"));
        }

        #endregion

        #region Asset Tests

        [Test]
        public void RegisterAsset_ShouldCreateAsset()
        {
            // Arrange
            var floor = _system.RegisterFloor(new FloorData { Name = "L3" });
            var space = _system.RegisterSpace(new SpaceData { Name = "Mech Room", FloorId = floor.FloorId });
            var assetType = _system.RegisterAssetType(new AssetTypeData
            {
                Name = "Pump",
                Category = "Mechanical",
                Manufacturer = "Grundfos"
            });

            var assetData = new AssetData
            {
                Name = "CW Pump P-1",
                Description = "Primary chilled water pump",
                TypeId = assetType.TypeId,
                SpaceId = space.SpaceId,
                SerialNumber = "SN-12345",
                InstallationDate = DateTime.Today.AddDays(-30),
                WarrantyStartDate = DateTime.Today.AddDays(-30),
                BarCode = "BC-001",
                TagNumber = "P-CW-001"
            };

            // Act
            var asset = _system.RegisterAsset(assetData);

            // Assert
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.AssetId, Is.Not.Null);
            Assert.That(asset.Name, Is.EqualTo("CW Pump P-1"));
            Assert.That(asset.SerialNumber, Is.EqualTo("SN-12345"));
            Assert.That(asset.TypeId, Is.EqualTo(assetType.TypeId));
        }

        [Test]
        public void RegisterAsset_ShouldFireEvent()
        {
            // Arrange
            var assetType = _system.RegisterAssetType(new AssetTypeData
            {
                Name = "Event Test Type",
                Category = "Test"
            });

            AssetEventArgs eventArgs = null;
            _system.AssetRegistered += (s, e) => eventArgs = e;

            // Act
            var asset = _system.RegisterAsset(new AssetData
            {
                Name = "Event Test Asset",
                TypeId = assetType.TypeId
            });

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Asset.AssetId, Is.EqualTo(asset.AssetId));
        }

        #endregion

        #region System Tests

        [Test]
        public void RegisterSystem_ShouldCreateSystem()
        {
            // Arrange
            var systemData = new SystemData
            {
                Name = "Chilled Water System",
                Category = "HVAC",
                Description = "Primary chilled water distribution system"
            };

            // Act
            var system = _system.RegisterSystem(systemData);

            // Assert
            Assert.That(system, Is.Not.Null);
            Assert.That(system.SystemId, Is.Not.Null);
            Assert.That(system.Name, Is.EqualTo("Chilled Water System"));
        }

        [Test]
        public void AddAssetToSystem_ShouldLinkAsset()
        {
            // Arrange
            var assetType = _system.RegisterAssetType(new AssetTypeData { Name = "Pump", Category = "Mech" });
            var asset = _system.RegisterAsset(new AssetData { Name = "Pump 1", TypeId = assetType.TypeId });
            var system = _system.RegisterSystem(new SystemData { Name = "CHW System", Category = "HVAC" });

            // Act
            _system.AddAssetToSystem(asset.AssetId, system.SystemId);

            // Assert
            Assert.That(asset.SystemId, Is.EqualTo(system.SystemId));
            Assert.That(system.AssetIds.Contains(asset.AssetId), Is.True);
        }

        #endregion

        #region Document Tests

        [Test]
        public void AddDocument_ShouldCreateDocument()
        {
            // Arrange
            var assetType = _system.RegisterAssetType(new AssetTypeData { Name = "AHU", Category = "HVAC" });
            var asset = _system.RegisterAsset(new AssetData { Name = "AHU-1", TypeId = assetType.TypeId });

            var docData = new DocumentData
            {
                Name = "O&M Manual - AHU-1",
                Category = DocumentCategory.OperationManual,
                FilePath = "/docs/ahu1_manual.pdf",
                Description = "Operations and maintenance manual",
                AssetIds = new List<string> { asset.AssetId }
            };

            // Act
            var document = _system.AddDocument(docData);

            // Assert
            Assert.That(document, Is.Not.Null);
            Assert.That(document.DocumentId, Is.Not.Null);
            Assert.That(document.Name, Is.EqualTo("O&M Manual - AHU-1"));
            Assert.That(document.Category, Is.EqualTo(DocumentCategory.OperationManual));
        }

        #endregion

        #region Spare Parts Tests

        [Test]
        public void AddSparePart_ShouldCreatePart()
        {
            // Arrange
            var assetType = _system.RegisterAssetType(new AssetTypeData { Name = "Fan", Category = "HVAC" });

            var partData = new SparePartData
            {
                Name = "Replacement Belt",
                PartNumber = "BELT-V12",
                TypeId = assetType.TypeId,
                Suppliers = new List<string> { "Industrial Supply Co", "Parts Direct" },
                SetQuantity = 2,
                MinimumStock = 5
            };

            // Act
            var part = _system.AddSparePart(partData);

            // Assert
            Assert.That(part, Is.Not.Null);
            Assert.That(part.PartId, Is.Not.Null);
            Assert.That(part.PartNumber, Is.EqualTo("BELT-V12"));
            Assert.That(part.Suppliers.Count, Is.EqualTo(2));
        }

        #endregion

        #region Maintenance Job Tests

        [Test]
        public void AddMaintenanceJob_ShouldCreateJob()
        {
            // Arrange
            var assetType = _system.RegisterAssetType(new AssetTypeData { Name = "Filter", Category = "HVAC" });

            var jobData = new MaintenanceJobData
            {
                Name = "Replace Air Filters",
                Description = "Replace all air handling unit filters",
                TypeId = assetType.TypeId,
                TaskNumber = "PM-001",
                Frequency = MaintenanceFrequency.Quarterly,
                Duration = 2.0,
                Resources = new List<string> { "HVAC Technician", "Ladder", "Filter stock" },
                Procedures = new List<string>
                {
                    "Turn off AHU",
                    "Remove access panel",
                    "Remove old filters",
                    "Install new filters",
                    "Replace access panel",
                    "Turn on AHU and verify operation"
                }
            };

            // Act
            var job = _system.AddMaintenanceJob(jobData);

            // Assert
            Assert.That(job, Is.Not.Null);
            Assert.That(job.JobId, Is.Not.Null);
            Assert.That(job.TaskNumber, Is.EqualTo("PM-001"));
            Assert.That(job.Frequency, Is.EqualTo(MaintenanceFrequency.Quarterly));
            Assert.That(job.Procedures.Count, Is.EqualTo(6));
        }

        #endregion

        #region COBie Export Tests

        [Test]
        public void ExportCOBie_ShouldGenerateAllSheets()
        {
            // Arrange - Create sample data
            var project = _system.InitializeProject(new ProjectSetup
            {
                ProjectName = "Export Test Project",
                ClientName = "Test Client"
            });

            var floor = _system.RegisterFloor(new FloorData { Name = "Level 1" });
            var space = _system.RegisterSpace(new SpaceData { Name = "Room 101", FloorId = floor.FloorId });
            var zone = _system.RegisterZone(new ZoneData { Name = "Zone A", Category = "HVAC" });
            var assetType = _system.RegisterAssetType(new AssetTypeData
            {
                Name = "Test Type",
                Category = "Test",
                Manufacturer = "Test Mfg"
            });
            var asset = _system.RegisterAsset(new AssetData
            {
                Name = "Test Asset",
                TypeId = assetType.TypeId,
                SpaceId = space.SpaceId
            });
            var system = _system.RegisterSystem(new SystemData { Name = "Test System", Category = "Test" });

            // Act
            var cobieData = _system.ExportCOBie();

            // Assert
            Assert.That(cobieData, Is.Not.Null);
            Assert.That(cobieData.ContainsKey("Contact"), Is.True);
            Assert.That(cobieData.ContainsKey("Facility"), Is.True);
            Assert.That(cobieData.ContainsKey("Floor"), Is.True);
            Assert.That(cobieData.ContainsKey("Space"), Is.True);
            Assert.That(cobieData.ContainsKey("Zone"), Is.True);
            Assert.That(cobieData.ContainsKey("Type"), Is.True);
            Assert.That(cobieData.ContainsKey("Component"), Is.True);
            Assert.That(cobieData.ContainsKey("System"), Is.True);
        }

        [Test]
        public void ExportCOBieToCSV_ShouldGenerateCSVContent()
        {
            // Arrange
            _system.InitializeProject(new ProjectSetup { ProjectName = "CSV Test" });
            _system.RegisterFloor(new FloorData { Name = "Floor 1" });

            // Act
            var csvFiles = _system.ExportCOBieToCSV();

            // Assert
            Assert.That(csvFiles, Is.Not.Null);
            Assert.That(csvFiles.Count > 0, Is.True);
            Assert.That(csvFiles.ContainsKey("Floor.csv"), Is.True);
        }

        #endregion

        #region O&M Manual Tests

        [Test]
        public void GenerateOMMManual_ShouldGenerateManual()
        {
            // Arrange
            _system.InitializeProject(new ProjectSetup { ProjectName = "O&M Test Project" });

            var assetType = _system.RegisterAssetType(new AssetTypeData
            {
                Name = "Chiller",
                Category = "HVAC",
                Manufacturer = "Carrier",
                WarrantyDuration = 24
            });

            _system.RegisterAsset(new AssetData
            {
                Name = "Chiller CH-1",
                TypeId = assetType.TypeId
            });

            _system.AddMaintenanceJob(new MaintenanceJobData
            {
                Name = "Chiller Inspection",
                TypeId = assetType.TypeId,
                Frequency = MaintenanceFrequency.Monthly
            });

            // Act
            var manual = _system.GenerateOMMManual();

            // Assert
            Assert.That(manual, Is.Not.Null);
            Assert.That(manual.Sections.Count > 0, Is.True);
            Assert.That(manual.Sections.Find(s => s.Title == "Building Systems"), Is.Not.Null);
            Assert.That(manual.Sections.Find(s => s.Title == "Maintenance Schedules"), Is.Not.Null);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void ValidateHandoverData_ShouldReturnValidationResults()
        {
            // Arrange
            _system.InitializeProject(new ProjectSetup { ProjectName = "Validation Test" });
            _system.RegisterFloor(new FloorData { Name = "Level 1" });

            // Register type but asset without required fields
            var assetType = _system.RegisterAssetType(new AssetTypeData
            {
                Name = "Incomplete Type",
                Category = "Test"
                // Missing manufacturer
            });

            _system.RegisterAsset(new AssetData
            {
                Name = "Incomplete Asset",
                TypeId = assetType.TypeId
                // Missing serial number, installation date
            });

            // Act
            var validation = _system.ValidateHandoverData();

            // Assert
            Assert.That(validation, Is.Not.Null);
            Assert.That(validation.TotalAssets > 0, Is.True);
            // Should have some warnings about missing data
        }

        [Test]
        public void GetHandoverSummary_ShouldReturnSummary()
        {
            // Arrange
            _system.InitializeProject(new ProjectSetup { ProjectName = "Summary Test" });
            var floor = _system.RegisterFloor(new FloorData { Name = "F1" });
            _system.RegisterSpace(new SpaceData { Name = "S1", FloorId = floor.FloorId });
            var type = _system.RegisterAssetType(new AssetTypeData { Name = "T1", Category = "C1" });
            _system.RegisterAsset(new AssetData { Name = "A1", TypeId = type.TypeId });

            // Act
            var summary = _system.GetHandoverSummary();

            // Assert
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.TotalFloors > 0, Is.True);
            Assert.That(summary.TotalAssets > 0, Is.True);
            Assert.That(summary.AssetsByCategory, Is.Not.Null);
        }

        #endregion
    }
}
