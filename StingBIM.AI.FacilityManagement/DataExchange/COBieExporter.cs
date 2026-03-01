// ============================================================================
// StingBIM AI - COBie Data Exchange
// Construction Operations Building information exchange (COBie)
// Exports asset data to industry-standard COBie format for FM handover
// ============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.WorkOrders;
using StingBIM.AI.FacilityManagement.SpaceManagement;

namespace StingBIM.AI.FacilityManagement.DataExchange
{
    /// <summary>
    /// COBie Exporter - Generates COBie compliant data for FM system handover
    /// Supports COBie 2.4 and COBie UK 2012 formats
    /// </summary>
    public class COBieExporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly AssetRegistry _assetRegistry;
        private readonly SpaceRegistry? _spaceRegistry;
        private readonly COBieConfiguration _config;

        public COBieExporter(
            AssetRegistry assetRegistry,
            SpaceRegistry? spaceRegistry = null,
            COBieConfiguration? config = null)
        {
            _assetRegistry = assetRegistry;
            _spaceRegistry = spaceRegistry;
            _config = config ?? new COBieConfiguration();
        }

        #region Main Export Methods

        /// <summary>
        /// Export all COBie worksheets to DataSet
        /// </summary>
        public async Task<DataSet> ExportAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting COBie export...");

            var dataSet = new DataSet("COBie");

            // Create all COBie worksheets
            dataSet.Tables.Add(CreateContactSheet());
            dataSet.Tables.Add(CreateFacilitySheet());
            dataSet.Tables.Add(CreateFloorSheet());
            dataSet.Tables.Add(CreateSpaceSheet());
            dataSet.Tables.Add(CreateZoneSheet());
            dataSet.Tables.Add(CreateTypeSheet());
            dataSet.Tables.Add(await CreateComponentSheetAsync(cancellationToken));
            dataSet.Tables.Add(CreateSystemSheet());
            dataSet.Tables.Add(CreateAttributeSheet());
            dataSet.Tables.Add(CreateDocumentSheet());
            dataSet.Tables.Add(CreateJobSheet());
            dataSet.Tables.Add(CreateResourceSheet());

            Logger.Info($"COBie export complete: {dataSet.Tables.Count} worksheets");

            return dataSet;
        }

        /// <summary>
        /// Export COBie data to CSV files
        /// </summary>
        public async Task ExportToCsvAsync(string outputDirectory, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDirectory);

            var dataSet = await ExportAsync(cancellationToken);

            foreach (DataTable table in dataSet.Tables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = Path.Combine(outputDirectory, $"COBie_{table.TableName}.csv");
                await ExportTableToCsvAsync(table, filePath);
                Logger.Info($"Exported {table.TableName}: {table.Rows.Count} rows");
            }
        }

        #endregion

        #region COBie Worksheet Creation

        /// <summary>
        /// Contact - People and organizations involved with the facility
        /// </summary>
        private DataTable CreateContactSheet()
        {
            var table = new DataTable("Contact");

            // COBie Contact columns
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("Company", typeof(string));
            table.Columns.Add("Phone", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Department", typeof(string));
            table.Columns.Add("OrganizationCode", typeof(string));
            table.Columns.Add("GivenName", typeof(string));
            table.Columns.Add("FamilyName", typeof(string));
            table.Columns.Add("Street", typeof(string));
            table.Columns.Add("PostalBox", typeof(string));
            table.Columns.Add("Town", typeof(string));
            table.Columns.Add("StateRegion", typeof(string));
            table.Columns.Add("PostalCode", typeof(string));
            table.Columns.Add("Country", typeof(string));

            // Add facility owner/operator
            table.Rows.Add(
                _config.OwnerEmail,
                _config.CreatedBy,
                DateTime.Now,
                "Owner",
                _config.OwnerCompany,
                _config.OwnerPhone,
                "StingBIM",
                "Contact",
                "CON-001",
                "Facilities",
                "",
                _config.OwnerFirstName,
                _config.OwnerLastName,
                "",
                "",
                _config.City,
                _config.Region,
                "",
                _config.Country
            );

            return table;
        }

        /// <summary>
        /// Facility - The building or facility being documented
        /// </summary>
        private DataTable CreateFacilitySheet()
        {
            var table = new DataTable("Facility");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("ProjectName", typeof(string));
            table.Columns.Add("SiteName", typeof(string));
            table.Columns.Add("LinearUnits", typeof(string));
            table.Columns.Add("AreaUnits", typeof(string));
            table.Columns.Add("VolumeUnits", typeof(string));
            table.Columns.Add("CurrencyUnit", typeof(string));
            table.Columns.Add("AreaMeasurement", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalProjectObject", typeof(string));
            table.Columns.Add("ExternalProjectIdentifier", typeof(string));
            table.Columns.Add("ExternalSiteObject", typeof(string));
            table.Columns.Add("ExternalSiteIdentifier", typeof(string));
            table.Columns.Add("ExternalFacilityObject", typeof(string));
            table.Columns.Add("ExternalFacilityIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("ProjectDescription", typeof(string));
            table.Columns.Add("SiteDescription", typeof(string));
            table.Columns.Add("Phase", typeof(string));

            table.Rows.Add(
                _config.FacilityName,
                _config.CreatedBy,
                DateTime.Now,
                _config.FacilityCategory,
                _config.ProjectName,
                _config.SiteName,
                "millimeters",
                "square meters",
                "cubic meters",
                _config.CurrencyUnit,
                "Gross Floor Area",
                "StingBIM",
                "Project",
                _config.ProjectId,
                "Site",
                _config.SiteId,
                "Facility",
                _config.FacilityId,
                _config.FacilityDescription,
                _config.ProjectDescription,
                _config.SiteDescription,
                "Operations"
            );

            return table;
        }

        /// <summary>
        /// Floor - Building levels/stories
        /// </summary>
        private DataTable CreateFloorSheet()
        {
            var table = new DataTable("Floor");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("Elevation", typeof(double));
            table.Columns.Add("Height", typeof(double));

            // Get unique floors from assets
            var floors = _assetRegistry.GetAll()
                .Select(a => a.FloorId)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .OrderBy(f => f);

            foreach (var floor in floors)
            {
                table.Rows.Add(
                    floor,
                    _config.CreatedBy,
                    DateTime.Now,
                    "Floor",
                    "StingBIM",
                    "Floor",
                    floor,
                    $"Level {floor}",
                    0.0,
                    3500.0
                );
            }

            return table;
        }

        /// <summary>
        /// Space - Rooms and spaces within the facility
        /// </summary>
        private DataTable CreateSpaceSheet()
        {
            var table = new DataTable("Space");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("FloorName", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("RoomTag", typeof(string));
            table.Columns.Add("UsableHeight", typeof(double));
            table.Columns.Add("GrossArea", typeof(double));
            table.Columns.Add("NetArea", typeof(double));

            // Get unique locations from assets
            var locations = _assetRegistry.GetAll()
                .Select(a => new { a.LocationId, a.FloorId, a.RoomNumber })
                .Where(l => !string.IsNullOrEmpty(l.LocationId))
                .Distinct()
                .OrderBy(l => l.FloorId)
                .ThenBy(l => l.LocationId);

            foreach (var loc in locations)
            {
                table.Rows.Add(
                    loc.LocationId,
                    _config.CreatedBy,
                    DateTime.Now,
                    "Space",
                    loc.FloorId,
                    $"Room {loc.RoomNumber}",
                    "StingBIM",
                    "Space",
                    loc.LocationId,
                    loc.RoomNumber,
                    2700.0,
                    0.0,
                    0.0
                );
            }

            return table;
        }

        /// <summary>
        /// Zone - Functional groupings of spaces
        /// </summary>
        private DataTable CreateZoneSheet()
        {
            var table = new DataTable("Zone");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("SpaceNames", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));

            // Get unique systems as zones
            var systems = _assetRegistry.GetAll()
                .Select(a => a.System)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s);

            foreach (var system in systems)
            {
                var spaces = _assetRegistry.GetBySystem(system)
                    .Select(a => a.LocationId)
                    .Distinct()
                    .Take(10);

                table.Rows.Add(
                    $"{system} Zone",
                    _config.CreatedBy,
                    DateTime.Now,
                    "System Zone",
                    string.Join(",", spaces),
                    "StingBIM",
                    "Zone",
                    $"ZN-{system}",
                    $"{system} serving zone"
                );
            }

            return table;
        }

        /// <summary>
        /// Type - Equipment types/product data templates
        /// </summary>
        private DataTable CreateTypeSheet()
        {
            var table = new DataTable("Type");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("AssetType", typeof(string));
            table.Columns.Add("Manufacturer", typeof(string));
            table.Columns.Add("ModelNumber", typeof(string));
            table.Columns.Add("WarrantyGuarantorParts", typeof(string));
            table.Columns.Add("WarrantyDurationParts", typeof(double));
            table.Columns.Add("WarrantyGuarantorLabor", typeof(string));
            table.Columns.Add("WarrantyDurationLabor", typeof(double));
            table.Columns.Add("WarrantyDurationUnit", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("ReplacementCost", typeof(decimal));
            table.Columns.Add("ExpectedLife", typeof(double));
            table.Columns.Add("DurationUnit", typeof(string));
            table.Columns.Add("NominalLength", typeof(double));
            table.Columns.Add("NominalWidth", typeof(double));
            table.Columns.Add("NominalHeight", typeof(double));
            table.Columns.Add("ModelReference", typeof(string));
            table.Columns.Add("Shape", typeof(string));
            table.Columns.Add("Size", typeof(string));
            table.Columns.Add("Color", typeof(string));
            table.Columns.Add("Finish", typeof(string));
            table.Columns.Add("Grade", typeof(string));
            table.Columns.Add("Material", typeof(string));
            table.Columns.Add("Constituents", typeof(string));
            table.Columns.Add("Features", typeof(string));
            table.Columns.Add("AccessibilityPerformance", typeof(string));
            table.Columns.Add("CodePerformance", typeof(string));
            table.Columns.Add("SustainabilityPerformance", typeof(string));

            // Group assets by type
            var types = _assetRegistry.GetAll()
                .GroupBy(a => new { a.AssetType, a.Manufacturer, a.Model })
                .Select(g => g.First());

            foreach (var type in types)
            {
                table.Rows.Add(
                    $"{type.Manufacturer} {type.Model}",
                    _config.CreatedBy,
                    DateTime.Now,
                    type.AssetCategory,
                    type.Description,
                    type.AssetType,
                    type.Manufacturer,
                    type.Model,
                    type.Manufacturer,
                    12.0,
                    type.Manufacturer,
                    12.0,
                    "month",
                    "StingBIM",
                    "Type",
                    $"TYP-{type.AssetType}",
                    type.ReplacementCost,
                    type.ExpectedLifeYears,
                    "year",
                    0.0, 0.0, 0.0,
                    type.Model,
                    "", "", "", "", "", "", "", "",
                    "", "", ""
                );
            }

            return table;
        }

        /// <summary>
        /// Component - Individual asset instances
        /// </summary>
        private async Task<DataTable> CreateComponentSheetAsync(CancellationToken cancellationToken)
        {
            var table = new DataTable("Component");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("TypeName", typeof(string));
            table.Columns.Add("Space", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("SerialNumber", typeof(string));
            table.Columns.Add("InstallationDate", typeof(DateTime));
            table.Columns.Add("WarrantyStartDate", typeof(DateTime));
            table.Columns.Add("TagNumber", typeof(string));
            table.Columns.Add("BarCode", typeof(string));
            table.Columns.Add("AssetIdentifier", typeof(string));

            var assets = _assetRegistry.GetAll();

            foreach (var asset in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                table.Rows.Add(
                    asset.Name,
                    _config.CreatedBy,
                    DateTime.Now,
                    $"{asset.Manufacturer} {asset.Model}",
                    asset.LocationId,
                    asset.Description,
                    "StingBIM",
                    "Component",
                    asset.RevitElementGuid?.ToString() ?? asset.AssetId,
                    asset.SerialNumber,
                    asset.InstallDate,
                    asset.WarrantyStartDate ?? asset.InstallDate,
                    asset.AssetId,
                    asset.BarCode,
                    asset.AssetId
                );
            }

            return table;
        }

        /// <summary>
        /// System - Building systems (HVAC, Electrical, etc.)
        /// </summary>
        private DataTable CreateSystemSheet()
        {
            var table = new DataTable("System");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("ComponentNames", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));

            var systems = _assetRegistry.GetAll()
                .GroupBy(a => a.System)
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var system in systems)
            {
                var components = system.Select(a => a.Name).Take(20);

                table.Rows.Add(
                    system.Key,
                    _config.CreatedBy,
                    DateTime.Now,
                    system.First().AssetCategory,
                    string.Join(",", components),
                    "StingBIM",
                    "System",
                    $"SYS-{system.Key}",
                    $"{system.Key} building system"
                );
            }

            return table;
        }

        /// <summary>
        /// Attribute - Extended properties
        /// </summary>
        private DataTable CreateAttributeSheet()
        {
            var table = new DataTable("Attribute");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("SheetName", typeof(string));
            table.Columns.Add("RowName", typeof(string));
            table.Columns.Add("Value", typeof(string));
            table.Columns.Add("Unit", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("AllowedValues", typeof(string));

            // Add key attributes for each asset
            foreach (var asset in _assetRegistry.GetAll())
            {
                // Criticality
                table.Rows.Add(
                    "Criticality", _config.CreatedBy, DateTime.Now, "Equipment",
                    "Component", asset.Name, asset.Criticality.ToString(), "",
                    "StingBIM", "Attribute", $"ATTR-{asset.AssetId}-CRIT",
                    "Asset criticality level", "Critical,High,Standard,Low"
                );

                // Power Rating
                if (asset.PowerRating_kW.HasValue)
                {
                    table.Rows.Add(
                        "PowerRating", _config.CreatedBy, DateTime.Now, "Performance",
                        "Component", asset.Name, asset.PowerRating_kW.ToString(), "kW",
                        "StingBIM", "Attribute", $"ATTR-{asset.AssetId}-PWR",
                        "Electrical power rating", ""
                    );
                }
            }

            return table;
        }

        /// <summary>
        /// Document - O&M manuals and documentation
        /// </summary>
        private DataTable CreateDocumentSheet()
        {
            var table = new DataTable("Document");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("ApprovalBy", typeof(string));
            table.Columns.Add("Stage", typeof(string));
            table.Columns.Add("SheetName", typeof(string));
            table.Columns.Add("RowName", typeof(string));
            table.Columns.Add("Directory", typeof(string));
            table.Columns.Add("File", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("Reference", typeof(string));

            // Add documents for assets that have them
            foreach (var asset in _assetRegistry.GetAll())
            {
                foreach (var doc in asset.Documents)
                {
                    table.Rows.Add(
                        doc.Name,
                        _config.CreatedBy,
                        DateTime.Now,
                        doc.Type.ToString(),
                        _config.CreatedBy,
                        "Operations",
                        "Component",
                        asset.Name,
                        Path.GetDirectoryName(doc.FilePath),
                        Path.GetFileName(doc.FilePath),
                        "StingBIM",
                        "Document",
                        doc.DocumentId,
                        $"O&M documentation for {asset.Name}",
                        doc.Url
                    );
                }
            }

            return table;
        }

        /// <summary>
        /// Job - Maintenance tasks and procedures
        /// </summary>
        private DataTable CreateJobSheet()
        {
            var table = new DataTable("Job");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("TypeName", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("Duration", typeof(double));
            table.Columns.Add("DurationUnit", typeof(string));
            table.Columns.Add("Start", typeof(double));
            table.Columns.Add("TaskStartUnit", typeof(string));
            table.Columns.Add("Frequency", typeof(double));
            table.Columns.Add("FrequencyUnit", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Priors", typeof(string));
            table.Columns.Add("ResourceNames", typeof(string));

            // Add standard maintenance jobs based on asset types
            var assetTypes = _assetRegistry.GetAll()
                .Select(a => a.AssetType)
                .Distinct();

            int jobNum = 1;
            foreach (var assetType in assetTypes)
            {
                // Preventive maintenance job
                table.Rows.Add(
                    $"PM-{assetType}",
                    _config.CreatedBy,
                    DateTime.Now,
                    "Preventive Maintenance",
                    "Active",
                    assetType,
                    $"Scheduled preventive maintenance for {assetType}",
                    2.0,
                    "hour",
                    0.0,
                    "day",
                    3.0,
                    "month",
                    "StingBIM",
                    "Job",
                    $"JOB-{jobNum++:D3}",
                    "",
                    "Maintenance Technician"
                );

                // Inspection job
                table.Rows.Add(
                    $"INSP-{assetType}",
                    _config.CreatedBy,
                    DateTime.Now,
                    "Inspection",
                    "Active",
                    assetType,
                    $"Routine inspection for {assetType}",
                    1.0,
                    "hour",
                    0.0,
                    "day",
                    1.0,
                    "month",
                    "StingBIM",
                    "Job",
                    $"JOB-{jobNum++:D3}",
                    "",
                    "Inspector"
                );
            }

            return table;
        }

        /// <summary>
        /// Resource - Labor and materials required for jobs
        /// </summary>
        private DataTable CreateResourceSheet()
        {
            var table = new DataTable("Resource");

            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("CreatedOn", typeof(DateTime));
            table.Columns.Add("Category", typeof(string));
            table.Columns.Add("ExternalSystem", typeof(string));
            table.Columns.Add("ExternalObject", typeof(string));
            table.Columns.Add("ExternalIdentifier", typeof(string));
            table.Columns.Add("Description", typeof(string));

            // Standard labor resources
            var laborTypes = new[]
            {
                ("HVAC Technician", "HVAC maintenance and repair"),
                ("Electrician", "Electrical maintenance and repair"),
                ("Plumber", "Plumbing maintenance and repair"),
                ("General Maintenance", "General building maintenance"),
                ("Fire Technician", "Fire system maintenance"),
                ("Lift Engineer", "Elevator maintenance"),
                ("BMS Technician", "Building automation systems"),
                ("Inspector", "Equipment inspection")
            };

            int resNum = 1;
            foreach (var (name, desc) in laborTypes)
            {
                table.Rows.Add(
                    name,
                    _config.CreatedBy,
                    DateTime.Now,
                    "Labor",
                    "StingBIM",
                    "Resource",
                    $"RES-{resNum++:D3}",
                    desc
                );
            }

            return table;
        }

        #endregion

        #region Helper Methods

        private async Task ExportTableToCsvAsync(DataTable table, string filePath)
        {
            using var writer = new StreamWriter(filePath);

            // Write headers
            var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            await writer.WriteLineAsync(string.Join(",", headers));

            // Write data rows
            foreach (DataRow row in table.Rows)
            {
                var values = row.ItemArray.Select(v => EscapeCsvValue(v?.ToString() ?? ""));
                await writer.WriteLineAsync(string.Join(",", values));
            }
        }

        private string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        #endregion
    }

    /// <summary>
    /// COBie export configuration
    /// </summary>
    public class COBieConfiguration
    {
        // Facility Information
        public string FacilityId { get; set; } = "FAC-001";
        public string FacilityName { get; set; } = "Main Building";
        public string FacilityDescription { get; set; } = "Commercial office building";
        public string FacilityCategory { get; set; } = "Office";

        // Project Information
        public string ProjectId { get; set; } = "PRJ-001";
        public string ProjectName { get; set; } = "FM Handover";
        public string ProjectDescription { get; set; } = "Facility management handover project";

        // Site Information
        public string SiteId { get; set; } = "SITE-001";
        public string SiteName { get; set; } = "Main Site";
        public string SiteDescription { get; set; } = "Main facility site";

        // Owner/Contact Information
        public string OwnerEmail { get; set; } = "facilities@building.com";
        public string OwnerCompany { get; set; } = "Building Management Ltd";
        public string OwnerPhone { get; set; } = "+256-700-000001";
        public string OwnerFirstName { get; set; } = "Facilities";
        public string OwnerLastName { get; set; } = "Manager";

        // Location
        public string City { get; set; } = "Kampala";
        public string Region { get; set; } = "Central";
        public string Country { get; set; } = "Uganda";

        // Metadata
        public string CreatedBy { get; set; } = "StingBIM";
        public string CurrencyUnit { get; set; } = "UGX";

        // Export Options
        public bool IncludeDocuments { get; set; } = true;
        public bool IncludePhotos { get; set; } = true;
        public bool IncludeMaintenanceJobs { get; set; } = true;
    }
}
