// ============================================================================
// StingBIM AI - Asset Handover System
// COBie data generation, O&M manual compilation, and FM integration
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.AssetHandover
{
    /// <summary>
    /// Asset Handover System providing COBie data generation, O&M documentation,
    /// as-built validation, and facilities management integration.
    /// </summary>
    public sealed class AssetHandoverSystem
    {
        private static readonly Lazy<AssetHandoverSystem> _instance =
            new Lazy<AssetHandoverSystem>(() => new AssetHandoverSystem());
        public static AssetHandoverSystem Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, Asset> _assets = new();
        private readonly Dictionary<string, AssetType> _assetTypes = new();
        private readonly Dictionary<string, Space> _spaces = new();
        private readonly Dictionary<string, Floor> _floors = new();
        private readonly Dictionary<string, Zone> _zones = new();
        private readonly Dictionary<string, System_> _systems = new();
        private readonly Dictionary<string, Document> _documents = new();
        private readonly Dictionary<string, Spare> _spares = new();
        private readonly Dictionary<string, Job> _jobs = new();
        private readonly List<Contact> _contacts = new();

        public event EventHandler<HandoverEventArgs> COBieGenerated;
        public event EventHandler<HandoverEventArgs> ValidationComplete;

        private AssetHandoverSystem() { }

        #region Asset Registration

        /// <summary>
        /// Register an asset for handover
        /// </summary>
        public Asset RegisterAsset(AssetRegistration registration)
        {
            var asset = new Asset
            {
                AssetId = registration.AssetId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Description = registration.Description,
                AssetTypeId = registration.AssetTypeId,
                SpaceId = registration.SpaceId,
                SerialNumber = registration.SerialNumber,
                TagNumber = registration.TagNumber,
                BarCode = registration.BarCode,
                InstallationDate = registration.InstallationDate,
                WarrantyStartDate = registration.WarrantyStartDate,
                WarrantyEndDate = registration.WarrantyEndDate,
                ExpectedLife = registration.ExpectedLife,
                Manufacturer = registration.Manufacturer,
                ModelNumber = registration.ModelNumber,
                ReplacementCost = registration.ReplacementCost,
                Attributes = registration.Attributes ?? new Dictionary<string, string>(),
                Documents = new List<string>(),
                Spares = new List<string>(),
                Jobs = new List<string>(),
                CreatedDate = DateTime.UtcNow
            };

            lock (_lock)
            {
                _assets[asset.AssetId] = asset;
            }

            return asset;
        }

        /// <summary>
        /// Register an asset type
        /// </summary>
        public AssetType RegisterAssetType(AssetTypeRegistration registration)
        {
            var assetType = new AssetType
            {
                TypeId = registration.TypeId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Category = registration.Category,
                Description = registration.Description,
                Manufacturer = registration.Manufacturer,
                ModelNumber = registration.ModelNumber,
                WarrantyDuration = registration.WarrantyDuration,
                ExpectedLife = registration.ExpectedLife,
                ReplacementCost = registration.ReplacementCost,
                NominalCapacity = registration.NominalCapacity,
                NominalEfficiency = registration.NominalEfficiency,
                Attributes = registration.Attributes ?? new Dictionary<string, string>(),
                Documents = new List<string>(),
                Spares = new List<string>(),
                Jobs = new List<string>()
            };

            lock (_lock)
            {
                _assetTypes[assetType.TypeId] = assetType;
            }

            return assetType;
        }

        /// <summary>
        /// Register a space
        /// </summary>
        public Space RegisterSpace(SpaceRegistration registration)
        {
            var space = new Space
            {
                SpaceId = registration.SpaceId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                FloorId = registration.FloorId,
                ZoneIds = registration.ZoneIds ?? new List<string>(),
                Description = registration.Description,
                Category = registration.Category,
                RoomTag = registration.RoomTag,
                UsableHeight = registration.UsableHeight,
                GrossArea = registration.GrossArea,
                NetArea = registration.NetArea
            };

            lock (_lock)
            {
                _spaces[space.SpaceId] = space;
            }

            return space;
        }

        /// <summary>
        /// Register a floor
        /// </summary>
        public Floor RegisterFloor(FloorRegistration registration)
        {
            var floor = new Floor
            {
                FloorId = registration.FloorId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Category = registration.Category,
                Elevation = registration.Elevation,
                Height = registration.Height
            };

            lock (_lock)
            {
                _floors[floor.FloorId] = floor;
            }

            return floor;
        }

        /// <summary>
        /// Register a system
        /// </summary>
        public System_ RegisterSystem(SystemRegistration registration)
        {
            var system = new System_
            {
                SystemId = registration.SystemId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Category = registration.Category,
                Description = registration.Description,
                ComponentIds = registration.ComponentIds ?? new List<string>()
            };

            lock (_lock)
            {
                _systems[system.SystemId] = system;
            }

            return system;
        }

        #endregion

        #region Document Management

        /// <summary>
        /// Register a document
        /// </summary>
        public Document RegisterDocument(DocumentRegistration registration)
        {
            var document = new Document
            {
                DocumentId = registration.DocumentId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Category = registration.Category,
                Description = registration.Description,
                Directory = registration.Directory,
                FileName = registration.FileName,
                Stage = registration.Stage,
                Reference = registration.Reference,
                ApprovalDate = registration.ApprovalDate,
                ApprovalBy = registration.ApprovalBy
            };

            lock (_lock)
            {
                _documents[document.DocumentId] = document;
            }

            return document;
        }

        /// <summary>
        /// Link document to asset or asset type
        /// </summary>
        public void LinkDocumentToAsset(string documentId, string assetId)
        {
            lock (_lock)
            {
                if (_assets.TryGetValue(assetId, out var asset) && !asset.Documents.Contains(documentId))
                    asset.Documents.Add(documentId);
            }
        }

        public void LinkDocumentToAssetType(string documentId, string typeId)
        {
            lock (_lock)
            {
                if (_assetTypes.TryGetValue(typeId, out var type) && !type.Documents.Contains(documentId))
                    type.Documents.Add(documentId);
            }
        }

        #endregion

        #region Spare Parts

        /// <summary>
        /// Register a spare part
        /// </summary>
        public Spare RegisterSpare(SpareRegistration registration)
        {
            var spare = new Spare
            {
                SpareId = registration.SpareId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Description = registration.Description,
                TypeId = registration.TypeId,
                Suppliers = registration.Suppliers ?? new List<string>(),
                PartNumber = registration.PartNumber,
                SetNumber = registration.SetNumber
            };

            lock (_lock)
            {
                _spares[spare.SpareId] = spare;

                // Link to asset type
                if (!string.IsNullOrEmpty(registration.TypeId) &&
                    _assetTypes.TryGetValue(registration.TypeId, out var assetType))
                {
                    if (!assetType.Spares.Contains(spare.SpareId))
                        assetType.Spares.Add(spare.SpareId);
                }
            }

            return spare;
        }

        #endregion

        #region Maintenance Jobs

        /// <summary>
        /// Register a maintenance job/task
        /// </summary>
        public Job RegisterJob(JobRegistration registration)
        {
            var job = new Job
            {
                JobId = registration.JobId ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Description = registration.Description,
                TypeId = registration.TypeId,
                Duration = registration.Duration,
                DurationUnit = registration.DurationUnit,
                Frequency = registration.Frequency,
                FrequencyUnit = registration.FrequencyUnit,
                PriorityRating = registration.PriorityRating,
                Resources = registration.Resources ?? new List<JobResource>()
            };

            lock (_lock)
            {
                _jobs[job.JobId] = job;

                // Link to asset type
                if (!string.IsNullOrEmpty(registration.TypeId) &&
                    _assetTypes.TryGetValue(registration.TypeId, out var assetType))
                {
                    if (!assetType.Jobs.Contains(job.JobId))
                        assetType.Jobs.Add(job.JobId);
                }
            }

            return job;
        }

        #endregion

        #region COBie Generation

        /// <summary>
        /// Generate COBie spreadsheet data
        /// </summary>
        public async Task<COBieData> GenerateCOBieAsync(COBieGenerationRequest request)
        {
            return await Task.Run(() =>
            {
                var cobie = new COBieData
                {
                    GeneratedAt = DateTime.UtcNow,
                    ProjectName = request.ProjectName,
                    SiteName = request.SiteName,
                    Facility = new COBieFacility
                    {
                        Name = request.FacilityName,
                        Category = request.FacilityCategory,
                        ProjectName = request.ProjectName,
                        SiteName = request.SiteName,
                        Phase = request.Phase,
                        Description = request.Description
                    }
                };

                // Generate Contact sheet
                cobie.Contacts = _contacts.Select(c => new COBieContact
                {
                    Email = c.Email,
                    CreatedBy = c.CreatedBy,
                    CreatedOn = c.CreatedOn,
                    Category = c.Category,
                    Company = c.Company,
                    Phone = c.Phone,
                    Department = c.Department,
                    Street = c.Street,
                    PostalCode = c.PostalCode,
                    Town = c.Town,
                    Country = c.Country
                }).ToList();

                // Generate Floor sheet
                cobie.Floors = _floors.Values.Select(f => new COBieFloor
                {
                    Name = f.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = f.Category,
                    Elevation = f.Elevation,
                    Height = f.Height
                }).ToList();

                // Generate Space sheet
                cobie.Spaces = _spaces.Values.Select(s => new COBieSpace
                {
                    Name = s.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = s.Category,
                    FloorName = _floors.TryGetValue(s.FloorId ?? "", out var floor) ? floor.Name : "",
                    Description = s.Description,
                    RoomTag = s.RoomTag,
                    UsableHeight = s.UsableHeight,
                    GrossArea = s.GrossArea,
                    NetArea = s.NetArea
                }).ToList();

                // Generate Zone sheet
                cobie.Zones = _zones.Values.Select(z => new COBieZone
                {
                    Name = z.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = z.Category,
                    SpaceNames = string.Join(",", z.SpaceIds.Select(id =>
                        _spaces.TryGetValue(id, out var space) ? space.Name : ""))
                }).ToList();

                // Generate Type sheet
                cobie.Types = _assetTypes.Values.Select(t => new COBieType
                {
                    Name = t.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = t.Category,
                    Description = t.Description,
                    Manufacturer = t.Manufacturer,
                    ModelNumber = t.ModelNumber,
                    WarrantyDurationParts = t.WarrantyDuration,
                    WarrantyDurationLabor = t.WarrantyDuration,
                    ExpectedLife = t.ExpectedLife,
                    ReplacementCost = t.ReplacementCost,
                    NominalLength = t.NominalCapacity,
                    NominalWidth = null,
                    NominalHeight = null
                }).ToList();

                // Generate Component sheet
                cobie.Components = _assets.Values.Select(a => new COBieComponent
                {
                    Name = a.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    TypeName = _assetTypes.TryGetValue(a.AssetTypeId ?? "", out var type) ? type.Name : "",
                    SpaceName = _spaces.TryGetValue(a.SpaceId ?? "", out var space) ? space.Name : "",
                    Description = a.Description,
                    SerialNumber = a.SerialNumber,
                    InstallationDate = a.InstallationDate,
                    WarrantyStartDate = a.WarrantyStartDate,
                    TagNumber = a.TagNumber,
                    BarCode = a.BarCode
                }).ToList();

                // Generate System sheet
                cobie.Systems = _systems.Values.Select(s => new COBieSystem
                {
                    Name = s.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = s.Category,
                    ComponentNames = string.Join(",", s.ComponentIds.Select(id =>
                        _assets.TryGetValue(id, out var asset) ? asset.Name : ""))
                }).ToList();

                // Generate Document sheet
                cobie.Documents = _documents.Values.Select(d => new COBieDocument
                {
                    Name = d.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    Category = d.Category,
                    Directory = d.Directory,
                    File = d.FileName,
                    Stage = d.Stage,
                    Reference = d.Reference
                }).ToList();

                // Generate Spare sheet
                cobie.Spares = _spares.Values.Select(s => new COBieSpare
                {
                    Name = s.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    TypeName = _assetTypes.TryGetValue(s.TypeId ?? "", out var t) ? t.Name : "",
                    Description = s.Description,
                    Suppliers = string.Join(",", s.Suppliers),
                    SetNumber = s.SetNumber,
                    PartNumber = s.PartNumber
                }).ToList();

                // Generate Job sheet
                cobie.Jobs = _jobs.Values.Select(j => new COBieJob
                {
                    Name = j.Name,
                    CreatedBy = request.CreatedBy,
                    CreatedOn = DateTime.UtcNow,
                    TypeName = _assetTypes.TryGetValue(j.TypeId ?? "", out var t) ? t.Name : "",
                    Description = j.Description,
                    Duration = j.Duration,
                    DurationUnit = j.DurationUnit,
                    Frequency = j.Frequency,
                    FrequencyUnit = j.FrequencyUnit
                }).ToList();

                // Calculate statistics
                cobie.Statistics = new COBieStatistics
                {
                    TotalAssets = cobie.Components.Count,
                    TotalTypes = cobie.Types.Count,
                    TotalSpaces = cobie.Spaces.Count,
                    TotalFloors = cobie.Floors.Count,
                    TotalDocuments = cobie.Documents.Count,
                    TotalSystems = cobie.Systems.Count,
                    TotalJobs = cobie.Jobs.Count,
                    TotalSpares = cobie.Spares.Count,
                    CompletenessScore = CalculateCompletenessScore(cobie)
                };

                COBieGenerated?.Invoke(this, new HandoverEventArgs
                {
                    Type = HandoverEventType.COBieGenerated,
                    Message = $"COBie generated with {cobie.Components.Count} components"
                });

                return cobie;
            });
        }

        private double CalculateCompletenessScore(COBieData cobie)
        {
            double score = 100;

            // Check for required fields
            if (!cobie.Components.Any()) score -= 20;
            if (!cobie.Types.Any()) score -= 15;
            if (!cobie.Spaces.Any()) score -= 10;
            if (!cobie.Documents.Any()) score -= 10;

            // Check for warranty info
            var componentsWithWarranty = cobie.Components.Count(c => c.WarrantyStartDate.HasValue);
            if (cobie.Components.Any())
                score -= (1 - (double)componentsWithWarranty / cobie.Components.Count) * 10;

            // Check for serial numbers
            var componentsWithSerial = cobie.Components.Count(c => !string.IsNullOrEmpty(c.SerialNumber));
            if (cobie.Components.Any())
                score -= (1 - (double)componentsWithSerial / cobie.Components.Count) * 10;

            return Math.Max(0, score);
        }

        /// <summary>
        /// Export COBie to CSV format
        /// </summary>
        public Dictionary<string, string> ExportCOBieToCSV(COBieData cobie)
        {
            var exports = new Dictionary<string, string>();

            // Contact sheet
            exports["Contact"] = GenerateCSV(cobie.Contacts, new[]
            {
                "Email", "CreatedBy", "CreatedOn", "Category", "Company", "Phone", "Department"
            });

            // Floor sheet
            exports["Floor"] = GenerateCSV(cobie.Floors, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "Category", "Elevation", "Height"
            });

            // Space sheet
            exports["Space"] = GenerateCSV(cobie.Spaces, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "Category", "FloorName", "Description", "RoomTag", "GrossArea", "NetArea"
            });

            // Type sheet
            exports["Type"] = GenerateCSV(cobie.Types, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "Category", "Description", "Manufacturer", "ModelNumber",
                "WarrantyDurationParts", "ExpectedLife", "ReplacementCost"
            });

            // Component sheet
            exports["Component"] = GenerateCSV(cobie.Components, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "TypeName", "SpaceName", "Description",
                "SerialNumber", "InstallationDate", "WarrantyStartDate", "TagNumber", "BarCode"
            });

            // System sheet
            exports["System"] = GenerateCSV(cobie.Systems, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "Category", "ComponentNames"
            });

            // Document sheet
            exports["Document"] = GenerateCSV(cobie.Documents, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "Category", "Directory", "File", "Stage", "Reference"
            });

            // Spare sheet
            exports["Spare"] = GenerateCSV(cobie.Spares, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "TypeName", "Description", "Suppliers", "PartNumber"
            });

            // Job sheet
            exports["Job"] = GenerateCSV(cobie.Jobs, new[]
            {
                "Name", "CreatedBy", "CreatedOn", "TypeName", "Description", "Duration", "Frequency"
            });

            return exports;
        }

        private string GenerateCSV<T>(List<T> items, string[] columns)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", columns));

            // Data rows
            foreach (var item in items)
            {
                var values = new List<string>();
                var type = typeof(T);

                foreach (var column in columns)
                {
                    var prop = type.GetProperty(column);
                    var value = prop?.GetValue(item)?.ToString() ?? "";
                    // Escape CSV values
                    if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                        value = $"\"{value.Replace("\"", "\"\"")}\"";
                    values.Add(value);
                }

                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate asset data for handover
        /// </summary>
        public async Task<ValidationResult> ValidateHandoverDataAsync()
        {
            return await Task.Run(() =>
            {
                var result = new ValidationResult
                {
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<ValidationError>(),
                    Warnings = new List<ValidationWarning>()
                };

                // Validate assets
                foreach (var asset in _assets.Values)
                {
                    if (string.IsNullOrEmpty(asset.Name))
                        result.Errors.Add(new ValidationError
                        {
                            EntityType = "Component",
                            EntityId = asset.AssetId,
                            Field = "Name",
                            Message = "Component name is required"
                        });

                    if (string.IsNullOrEmpty(asset.SerialNumber))
                        result.Warnings.Add(new ValidationWarning
                        {
                            EntityType = "Component",
                            EntityId = asset.AssetId,
                            Field = "SerialNumber",
                            Message = "Serial number not provided"
                        });

                    if (!asset.WarrantyStartDate.HasValue)
                        result.Warnings.Add(new ValidationWarning
                        {
                            EntityType = "Component",
                            EntityId = asset.AssetId,
                            Field = "WarrantyStartDate",
                            Message = "Warranty start date not set"
                        });

                    if (string.IsNullOrEmpty(asset.SpaceId) ||
                        !_spaces.ContainsKey(asset.SpaceId))
                        result.Warnings.Add(new ValidationWarning
                        {
                            EntityType = "Component",
                            EntityId = asset.AssetId,
                            Field = "Space",
                            Message = "Component not assigned to a valid space"
                        });
                }

                // Validate asset types
                foreach (var assetType in _assetTypes.Values)
                {
                    if (string.IsNullOrEmpty(assetType.Manufacturer))
                        result.Warnings.Add(new ValidationWarning
                        {
                            EntityType = "Type",
                            EntityId = assetType.TypeId,
                            Field = "Manufacturer",
                            Message = "Manufacturer not specified"
                        });

                    if (!assetType.Documents.Any())
                        result.Warnings.Add(new ValidationWarning
                        {
                            EntityType = "Type",
                            EntityId = assetType.TypeId,
                            Field = "Documents",
                            Message = "No documents linked to asset type"
                        });
                }

                // Validate spaces
                foreach (var space in _spaces.Values)
                {
                    if (string.IsNullOrEmpty(space.FloorId) ||
                        !_floors.ContainsKey(space.FloorId))
                        result.Errors.Add(new ValidationError
                        {
                            EntityType = "Space",
                            EntityId = space.SpaceId,
                            Field = "Floor",
                            Message = "Space not assigned to a valid floor"
                        });
                }

                // Calculate overall score
                result.IsValid = !result.Errors.Any();
                result.Score = 100.0 -
                    (result.Errors.Count * 5) -
                    (result.Warnings.Count * 1);
                result.Score = Math.Max(0, result.Score);

                ValidationComplete?.Invoke(this, new HandoverEventArgs
                {
                    Type = HandoverEventType.ValidationComplete,
                    Message = $"Validation complete. Score: {result.Score:F1}%"
                });

                return result;
            });
        }

        #endregion

        #region O&M Manual Generation

        /// <summary>
        /// Generate O&M manual structure
        /// </summary>
        public async Task<OMManual> GenerateOMManualAsync(string facilityName)
        {
            return await Task.Run(() =>
            {
                var manual = new OMManual
                {
                    GeneratedAt = DateTime.UtcNow,
                    FacilityName = facilityName,
                    Sections = new List<OMSection>()
                };

                // Section 1: General Information
                manual.Sections.Add(new OMSection
                {
                    SectionNumber = "1",
                    Title = "General Information",
                    SubSections = new List<OMSubSection>
                    {
                        new() { Number = "1.1", Title = "Building Overview", Content = GenerateBuildingOverview() },
                        new() { Number = "1.2", Title = "Contact Directory", Content = GenerateContactDirectory() },
                        new() { Number = "1.3", Title = "Emergency Procedures", Content = "See emergency plan documentation" }
                    }
                });

                // Section 2: Systems by category
                var systemsByCategory = _systems.Values.GroupBy(s => s.Category ?? "General");
                int sectionNum = 2;

                foreach (var category in systemsByCategory)
                {
                    var section = new OMSection
                    {
                        SectionNumber = sectionNum.ToString(),
                        Title = $"{category.Key} Systems",
                        SubSections = new List<OMSubSection>()
                    };

                    int subNum = 1;
                    foreach (var system in category)
                    {
                        section.SubSections.Add(new OMSubSection
                        {
                            Number = $"{sectionNum}.{subNum}",
                            Title = system.Name,
                            Content = GenerateSystemContent(system)
                        });
                        subNum++;
                    }

                    manual.Sections.Add(section);
                    sectionNum++;
                }

                // Section: Maintenance Schedules
                manual.Sections.Add(new OMSection
                {
                    SectionNumber = sectionNum.ToString(),
                    Title = "Maintenance Schedules",
                    SubSections = new List<OMSubSection>
                    {
                        new() { Number = $"{sectionNum}.1", Title = "Preventive Maintenance", Content = GenerateMaintenanceSchedule() },
                        new() { Number = $"{sectionNum}.2", Title = "Spare Parts Inventory", Content = GenerateSparePartsInventory() }
                    }
                });

                // Section: Warranty Information
                sectionNum++;
                manual.Sections.Add(new OMSection
                {
                    SectionNumber = sectionNum.ToString(),
                    Title = "Warranty Information",
                    SubSections = new List<OMSubSection>
                    {
                        new() { Number = $"{sectionNum}.1", Title = "Warranty Summary", Content = GenerateWarrantySummary() },
                        new() { Number = $"{sectionNum}.2", Title = "Warranty Contacts", Content = GenerateWarrantyContacts() }
                    }
                });

                // Calculate page count estimate
                manual.EstimatedPages = manual.Sections.Sum(s => s.SubSections.Count) * 5;

                return manual;
            });
        }

        private string GenerateBuildingOverview()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total Floors: {_floors.Count}");
            sb.AppendLine($"Total Spaces: {_spaces.Count}");
            sb.AppendLine($"Total Assets: {_assets.Count}");
            sb.AppendLine($"Total Systems: {_systems.Count}");
            return sb.ToString();
        }

        private string GenerateContactDirectory()
        {
            var sb = new StringBuilder();
            foreach (var contact in _contacts)
            {
                sb.AppendLine($"{contact.Company} - {contact.Category}");
                sb.AppendLine($"  Phone: {contact.Phone}");
                sb.AppendLine($"  Email: {contact.Email}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GenerateSystemContent(System_ system)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Description: {system.Description}");
            sb.AppendLine();
            sb.AppendLine("Components:");
            foreach (var componentId in system.ComponentIds)
            {
                if (_assets.TryGetValue(componentId, out var asset))
                {
                    sb.AppendLine($"  - {asset.Name}");
                    sb.AppendLine($"    Serial: {asset.SerialNumber}");
                    sb.AppendLine($"    Model: {asset.ModelNumber}");
                }
            }
            return sb.ToString();
        }

        private string GenerateMaintenanceSchedule()
        {
            var sb = new StringBuilder();
            foreach (var job in _jobs.Values)
            {
                sb.AppendLine($"Task: {job.Name}");
                sb.AppendLine($"  Frequency: Every {job.Frequency} {job.FrequencyUnit}");
                sb.AppendLine($"  Duration: {job.Duration} {job.DurationUnit}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GenerateSparePartsInventory()
        {
            var sb = new StringBuilder();
            foreach (var spare in _spares.Values)
            {
                sb.AppendLine($"Part: {spare.Name}");
                sb.AppendLine($"  Part Number: {spare.PartNumber}");
                sb.AppendLine($"  Suppliers: {string.Join(", ", spare.Suppliers)}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GenerateWarrantySummary()
        {
            var sb = new StringBuilder();
            var assetsWithWarranty = _assets.Values
                .Where(a => a.WarrantyEndDate.HasValue)
                .OrderBy(a => a.WarrantyEndDate);

            foreach (var asset in assetsWithWarranty)
            {
                sb.AppendLine($"{asset.Name}");
                sb.AppendLine($"  Warranty Ends: {asset.WarrantyEndDate:d}");
                sb.AppendLine($"  Manufacturer: {asset.Manufacturer}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GenerateWarrantyContacts()
        {
            var manufacturers = _assets.Values
                .Select(a => a.Manufacturer)
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct();

            var sb = new StringBuilder();
            foreach (var manufacturer in manufacturers)
            {
                var contact = _contacts.FirstOrDefault(c => c.Company == manufacturer);
                if (contact != null)
                {
                    sb.AppendLine($"{manufacturer}");
                    sb.AppendLine($"  Phone: {contact.Phone}");
                    sb.AppendLine($"  Email: {contact.Email}");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get asset handover statistics
        /// </summary>
        public HandoverStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new HandoverStatistics
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalAssets = _assets.Count,
                    TotalAssetTypes = _assetTypes.Count,
                    TotalSpaces = _spaces.Count,
                    TotalFloors = _floors.Count,
                    TotalSystems = _systems.Count,
                    TotalDocuments = _documents.Count,
                    TotalSpares = _spares.Count,
                    TotalJobs = _jobs.Count,
                    AssetsWithWarranty = _assets.Values.Count(a => a.WarrantyEndDate.HasValue),
                    AssetsWithSerialNumber = _assets.Values.Count(a => !string.IsNullOrEmpty(a.SerialNumber)),
                    DocumentsPerAsset = _assets.Count > 0 ?
                        (double)_documents.Count / _assets.Count : 0,
                    AssetsByCategory = _assetTypes.Values.GroupBy(t => t.Category ?? "Uncategorized")
                        .ToDictionary(g => g.Key, g =>
                            _assets.Values.Count(a => g.Select(t => t.TypeId).Contains(a.AssetTypeId)))
                };
            }
        }

        #endregion
    }

    #region Data Models

    public class Asset
    {
        public string AssetId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetTypeId { get; set; }
        public string SpaceId { get; set; }
        public string SerialNumber { get; set; }
        public string TagNumber { get; set; }
        public string BarCode { get; set; }
        public DateTime? InstallationDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int? ExpectedLife { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public decimal? ReplacementCost { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public List<string> Documents { get; set; }
        public List<string> Spares { get; set; }
        public List<string> Jobs { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AssetRegistration
    {
        public string AssetId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetTypeId { get; set; }
        public string SpaceId { get; set; }
        public string SerialNumber { get; set; }
        public string TagNumber { get; set; }
        public string BarCode { get; set; }
        public DateTime? InstallationDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int? ExpectedLife { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public decimal? ReplacementCost { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }

    public class AssetType
    {
        public string TypeId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public int? WarrantyDuration { get; set; }
        public int? ExpectedLife { get; set; }
        public decimal? ReplacementCost { get; set; }
        public double? NominalCapacity { get; set; }
        public double? NominalEfficiency { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public List<string> Documents { get; set; }
        public List<string> Spares { get; set; }
        public List<string> Jobs { get; set; }
    }

    public class AssetTypeRegistration
    {
        public string TypeId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public int? WarrantyDuration { get; set; }
        public int? ExpectedLife { get; set; }
        public decimal? ReplacementCost { get; set; }
        public double? NominalCapacity { get; set; }
        public double? NominalEfficiency { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }

    public class Space
    {
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public string FloorId { get; set; }
        public List<string> ZoneIds { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string RoomTag { get; set; }
        public double? UsableHeight { get; set; }
        public double? GrossArea { get; set; }
        public double? NetArea { get; set; }
    }

    public class SpaceRegistration
    {
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public string FloorId { get; set; }
        public List<string> ZoneIds { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string RoomTag { get; set; }
        public double? UsableHeight { get; set; }
        public double? GrossArea { get; set; }
        public double? NetArea { get; set; }
    }

    public class Floor
    {
        public string FloorId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public double? Elevation { get; set; }
        public double? Height { get; set; }
    }

    public class FloorRegistration
    {
        public string FloorId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public double? Elevation { get; set; }
        public double? Height { get; set; }
    }

    public class Zone
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<string> SpaceIds { get; set; }
    }

    public class System_
    {
        public string SystemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> ComponentIds { get; set; }
    }

    public class SystemRegistration
    {
        public string SystemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> ComponentIds { get; set; }
    }

    public class Document
    {
        public string DocumentId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Directory { get; set; }
        public string FileName { get; set; }
        public string Stage { get; set; }
        public string Reference { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string ApprovalBy { get; set; }
    }

    public class DocumentRegistration
    {
        public string DocumentId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Directory { get; set; }
        public string FileName { get; set; }
        public string Stage { get; set; }
        public string Reference { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string ApprovalBy { get; set; }
    }

    public class Spare
    {
        public string SpareId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeId { get; set; }
        public List<string> Suppliers { get; set; }
        public string PartNumber { get; set; }
        public string SetNumber { get; set; }
    }

    public class SpareRegistration
    {
        public string SpareId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeId { get; set; }
        public List<string> Suppliers { get; set; }
        public string PartNumber { get; set; }
        public string SetNumber { get; set; }
    }

    public class Job
    {
        public string JobId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeId { get; set; }
        public double? Duration { get; set; }
        public string DurationUnit { get; set; }
        public int? Frequency { get; set; }
        public string FrequencyUnit { get; set; }
        public int? PriorityRating { get; set; }
        public List<JobResource> Resources { get; set; }
    }

    public class JobRegistration
    {
        public string JobId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TypeId { get; set; }
        public double? Duration { get; set; }
        public string DurationUnit { get; set; }
        public int? Frequency { get; set; }
        public string FrequencyUnit { get; set; }
        public int? PriorityRating { get; set; }
        public List<JobResource> Resources { get; set; }
    }

    public class JobResource
    {
        public string ResourceType { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class Contact
    {
        public string Email { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string Company { get; set; }
        public string Phone { get; set; }
        public string Department { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string Town { get; set; }
        public string Country { get; set; }
    }

    // COBie Data structures
    public class COBieData
    {
        public DateTime GeneratedAt { get; set; }
        public string ProjectName { get; set; }
        public string SiteName { get; set; }
        public COBieFacility Facility { get; set; }
        public List<COBieContact> Contacts { get; set; }
        public List<COBieFloor> Floors { get; set; }
        public List<COBieSpace> Spaces { get; set; }
        public List<COBieZone> Zones { get; set; }
        public List<COBieType> Types { get; set; }
        public List<COBieComponent> Components { get; set; }
        public List<COBieSystem> Systems { get; set; }
        public List<COBieDocument> Documents { get; set; }
        public List<COBieSpare> Spares { get; set; }
        public List<COBieJob> Jobs { get; set; }
        public COBieStatistics Statistics { get; set; }
    }

    public class COBieFacility
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string ProjectName { get; set; }
        public string SiteName { get; set; }
        public string Phase { get; set; }
        public string Description { get; set; }
    }

    public class COBieContact
    {
        public string Email { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string Company { get; set; }
        public string Phone { get; set; }
        public string Department { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string Town { get; set; }
        public string Country { get; set; }
    }

    public class COBieFloor
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public double? Elevation { get; set; }
        public double? Height { get; set; }
    }

    public class COBieSpace
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string FloorName { get; set; }
        public string Description { get; set; }
        public string RoomTag { get; set; }
        public double? UsableHeight { get; set; }
        public double? GrossArea { get; set; }
        public double? NetArea { get; set; }
    }

    public class COBieZone
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string SpaceNames { get; set; }
    }

    public class COBieType
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public int? WarrantyDurationParts { get; set; }
        public int? WarrantyDurationLabor { get; set; }
        public int? ExpectedLife { get; set; }
        public decimal? ReplacementCost { get; set; }
        public double? NominalLength { get; set; }
        public double? NominalWidth { get; set; }
        public double? NominalHeight { get; set; }
    }

    public class COBieComponent
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string TypeName { get; set; }
        public string SpaceName { get; set; }
        public string Description { get; set; }
        public string SerialNumber { get; set; }
        public DateTime? InstallationDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public string TagNumber { get; set; }
        public string BarCode { get; set; }
    }

    public class COBieSystem
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string ComponentNames { get; set; }
    }

    public class COBieDocument
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Category { get; set; }
        public string Directory { get; set; }
        public string File { get; set; }
        public string Stage { get; set; }
        public string Reference { get; set; }
    }

    public class COBieSpare
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public string Suppliers { get; set; }
        public string SetNumber { get; set; }
        public string PartNumber { get; set; }
    }

    public class COBieJob
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public double? Duration { get; set; }
        public string DurationUnit { get; set; }
        public int? Frequency { get; set; }
        public string FrequencyUnit { get; set; }
    }

    public class COBieStatistics
    {
        public int TotalAssets { get; set; }
        public int TotalTypes { get; set; }
        public int TotalSpaces { get; set; }
        public int TotalFloors { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalSystems { get; set; }
        public int TotalJobs { get; set; }
        public int TotalSpares { get; set; }
        public double CompletenessScore { get; set; }
    }

    public class COBieGenerationRequest
    {
        public string ProjectName { get; set; }
        public string SiteName { get; set; }
        public string FacilityName { get; set; }
        public string FacilityCategory { get; set; }
        public string Phase { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
    }

    public class ValidationResult
    {
        public DateTime ValidatedAt { get; set; }
        public bool IsValid { get; set; }
        public double Score { get; set; }
        public List<ValidationError> Errors { get; set; }
        public List<ValidationWarning> Warnings { get; set; }
    }

    public class ValidationError
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Field { get; set; }
        public string Message { get; set; }
    }

    public class ValidationWarning
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Field { get; set; }
        public string Message { get; set; }
    }

    public class OMManual
    {
        public DateTime GeneratedAt { get; set; }
        public string FacilityName { get; set; }
        public List<OMSection> Sections { get; set; }
        public int EstimatedPages { get; set; }
    }

    public class OMSection
    {
        public string SectionNumber { get; set; }
        public string Title { get; set; }
        public List<OMSubSection> SubSections { get; set; }
    }

    public class OMSubSection
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class HandoverStatistics
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalAssets { get; set; }
        public int TotalAssetTypes { get; set; }
        public int TotalSpaces { get; set; }
        public int TotalFloors { get; set; }
        public int TotalSystems { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalSpares { get; set; }
        public int TotalJobs { get; set; }
        public int AssetsWithWarranty { get; set; }
        public int AssetsWithSerialNumber { get; set; }
        public double DocumentsPerAsset { get; set; }
        public Dictionary<string, int> AssetsByCategory { get; set; }
    }

    public class HandoverEventArgs : EventArgs
    {
        public HandoverEventType Type { get; set; }
        public string Message { get; set; }
    }

    public enum HandoverEventType
    {
        COBieGenerated,
        ValidationComplete,
        OMManualGenerated
    }

    #endregion
}
