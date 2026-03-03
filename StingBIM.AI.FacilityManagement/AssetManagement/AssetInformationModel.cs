// ============================================================================
// StingBIM AI - Asset Information Model (AIM)
// ISO 19650-3 compliant asset information management
// Links BIM elements to FM asset records via Revit GUIDs
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.FacilityManagement.AssetManagement
{
    #region Core Models

    /// <summary>
    /// Asset Information Model - ISO 19650-3 compliant asset record
    /// Links BIM element data to facility management operations
    /// </summary>
    public class Asset
    {
        // Identification
        public string AssetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AssetName { get => Name; set => Name = value; } // Alias for Name
        public string Description { get; set; } = string.Empty;
        public string BarCode { get; set; } = string.Empty;

        // BIM Linking - Critical for BIM-FM integration
        public Guid? RevitElementGuid { get; set; }
        public string? RevitFamilyName { get; set; }
        public string? RevitTypeName { get; set; }
        public string? COBieReference { get; set; }
        public string? IFCGuid { get; set; }

        // Classification
        public string AssetType { get; set; } = string.Empty;
        public string AssetCategory { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public string SubSystem { get; set; } = string.Empty;
        public string UniclassCode { get; set; } = string.Empty;
        public string OmniClassCode { get; set; } = string.Empty;

        // Location - Linked to space management
        public string LocationId { get; set; } = string.Empty;
        public string FloorId { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public double? XCoordinate { get; set; }
        public double? YCoordinate { get; set; }
        public double? ZCoordinate { get; set; }

        // Manufacturer Information
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;

        // Lifecycle Dates
        public DateTime? ManufactureDate { get; set; }
        public DateTime InstallDate { get; set; }
        public DateTime InstallationDate { get => InstallDate; set => InstallDate = value; } // Alias for InstallDate
        public DateTime? CommissionDate { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public int ExpectedLifeYears { get; set; }
        public DateTime? ExpectedReplacementDate =>
            InstallDate.AddYears(ExpectedLifeYears);

        // Condition and Status
        public AssetStatus Status { get; set; } = AssetStatus.Operational;
        public AssetCondition Condition { get; set; } = AssetCondition.Good;
        public double ConditionScore { get; set; } = 100;
        public AssetCriticality Criticality { get; set; } = AssetCriticality.Standard;

        // Financial
        public decimal PurchaseCost { get; set; }
        public decimal ReplacementCost { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal AnnualMaintenanceCost { get; set; }
        public string CostCenter { get; set; } = string.Empty;

        // Performance
        public double? DesignCapacity { get; set; }
        public string? CapacityUnit { get; set; }
        public double? CurrentEfficiency { get; set; }
        public double? DesignEfficiency { get; set; }
        public double? PowerRating_kW { get; set; }

        // Maintenance
        public DateTime? LastMaintenanceDate { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
        public int MaintenanceIntervalDays { get; set; }
        public string MaintenanceContractId { get; set; } = string.Empty;
        public string PreferredVendorId { get; set; } = string.Empty;

        // Documentation
        public List<AssetDocument> Documents { get; set; } = new();
        public List<AssetPhoto> Photos { get; set; } = new();

        // Relationships
        public string? ParentAssetId { get; set; }
        public List<string> ChildAssetIds { get; set; } = new();
        public List<string> ConnectedAssetIds { get; set; } = new();

        // Attributes - Extensible key-value pairs
        public Dictionary<string, object> Attributes { get; set; } = new();

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }

        // Calculated Properties
        public double AgeYears => (DateTime.Now - InstallDate).TotalDays / 365.25;
        public double RemainingLifeYears => Math.Max(0, ExpectedLifeYears - AgeYears);
        public double LifeUsedPercent => Math.Min(100, (AgeYears / ExpectedLifeYears) * 100);
        public bool IsUnderWarranty => WarrantyExpiry.HasValue && WarrantyExpiry > DateTime.Now;
        public bool IsMaintenanceDue => NextMaintenanceDate.HasValue && NextMaintenanceDate <= DateTime.Now;
        public bool IsNearEndOfLife => RemainingLifeYears < 2;
    }

    public class AssetDocument
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }

    public class AssetPhoto
    {
        public string PhotoId { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime TakenDate { get; set; }
        public string TakenBy { get; set; } = string.Empty;
        public PhotoType Type { get; set; }
    }

    public enum AssetStatus
    {
        Operational,
        Degraded,
        Failed,
        UnderRepair,
        Standby,
        Decommissioned,
        Disposed,
        Pending
    }

    public enum AssetCondition
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Critical
    }

    public enum AssetCriticality
    {
        Critical,      // Failure causes immediate safety or major business impact
        High,          // Failure causes significant operational disruption
        Medium,        // Moderate operational impact
        Standard,      // Normal operational equipment
        Low            // Non-essential, easily replaced
    }

    public enum DocumentType
    {
        OMManual,
        Datasheet,
        Drawing,
        Certificate,
        Warranty,
        ServiceReport,
        Specification,
        Photo,
        Other
    }

    public enum PhotoType
    {
        Nameplate,
        Installation,
        Condition,
        Defect,
        BeforeRepair,
        AfterRepair,
        General
    }

    #endregion

    #region Asset Registry

    /// <summary>
    /// Central asset registry with BIM integration
    /// Manages all facility assets and their relationships
    /// </summary>
    public class AssetRegistry
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, Asset> _assetsById;
        private readonly Dictionary<Guid, Asset> _assetsByRevitGuid;
        private readonly Dictionary<string, List<Asset>> _assetsByLocation;
        private readonly Dictionary<string, List<Asset>> _assetsBySystem;
        private readonly object _lock = new();

        public AssetRegistry()
        {
            _assetsById = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
            _assetsByRevitGuid = new Dictionary<Guid, Asset>();
            _assetsByLocation = new Dictionary<string, List<Asset>>(StringComparer.OrdinalIgnoreCase);
            _assetsBySystem = new Dictionary<string, List<Asset>>(StringComparer.OrdinalIgnoreCase);
        }

        #region Loading

        /// <summary>
        /// Load assets from CSV file
        /// </summary>
        public async Task LoadFromCsvAsync(string csvPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(csvPath))
            {
                Logger.Warn($"Asset file not found: {csvPath}");
                return;
            }

            Logger.Info($"Loading assets from {csvPath}");

            var lines = await File.ReadAllLinesAsync(csvPath, cancellationToken);
            var headers = lines[0].Split(',');

            foreach (var line in lines.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var values = ParseCsvLine(line);
                    if (values.Length < headers.Length) continue;

                    var asset = new Asset
                    {
                        AssetId = GetValue(values, headers, "AssetId"),
                        Name = GetValue(values, headers, "Name"),
                        AssetType = GetValue(values, headers, "AssetType"),
                        AssetCategory = GetValue(values, headers, "AssetCategory"),
                        System = GetValue(values, headers, "System"),
                        SubSystem = GetValue(values, headers, "SubSystem"),
                        LocationId = GetValue(values, headers, "LocationId"),
                        FloorId = GetValue(values, headers, "FloorId"),
                        RoomNumber = GetValue(values, headers, "RoomNumber"),
                        Manufacturer = GetValue(values, headers, "Manufacturer"),
                        Model = GetValue(values, headers, "Model"),
                        SerialNumber = GetValue(values, headers, "SerialNumber"),
                        InstallDate = ParseDate(GetValue(values, headers, "InstallDate")),
                        CommissionDate = ParseNullableDate(GetValue(values, headers, "CommissionDate")),
                        WarrantyExpiry = ParseNullableDate(GetValue(values, headers, "WarrantyExpiry")),
                        ExpectedLifeYears = ParseInt(GetValue(values, headers, "ExpectedLifeYears"), 15),
                        ReplacementCost = ParseDecimal(GetValue(values, headers, "ReplacementCost")),
                        Criticality = ParseCriticality(GetValue(values, headers, "Criticality")),
                        Status = ParseStatus(GetValue(values, headers, "Status")),
                        COBieReference = GetValue(values, headers, "COBieReference"),
                        BarCode = GetValue(values, headers, "BarCode"),
                        Description = GetValue(values, headers, "Description")
                    };

                    // Parse Revit GUID if present
                    var guidStr = GetValue(values, headers, "RevitElementGuid");
                    if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var guid))
                    {
                        asset.RevitElementGuid = guid;
                    }

                    RegisterAsset(asset);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error parsing asset line: {line}");
                }
            }

            Logger.Info($"Loaded {_assetsById.Count} assets");
        }

        #endregion

        #region Registration

        /// <summary>
        /// Register a new asset in the registry
        /// </summary>
        public void RegisterAsset(Asset asset)
        {
            lock (_lock)
            {
                _assetsById[asset.AssetId] = asset;

                if (asset.RevitElementGuid.HasValue)
                {
                    _assetsByRevitGuid[asset.RevitElementGuid.Value] = asset;
                }

                // Index by location
                if (!string.IsNullOrEmpty(asset.LocationId))
                {
                    if (!_assetsByLocation.ContainsKey(asset.LocationId))
                        _assetsByLocation[asset.LocationId] = new List<Asset>();
                    _assetsByLocation[asset.LocationId].Add(asset);
                }

                // Index by system
                if (!string.IsNullOrEmpty(asset.System))
                {
                    if (!_assetsBySystem.ContainsKey(asset.System))
                        _assetsBySystem[asset.System] = new List<Asset>();
                    _assetsBySystem[asset.System].Add(asset);
                }
            }
        }

        /// <summary>
        /// Link a Revit element to an existing asset
        /// </summary>
        public bool LinkRevitElement(string assetId, Guid revitGuid)
        {
            lock (_lock)
            {
                if (_assetsById.TryGetValue(assetId, out var asset))
                {
                    // Remove old GUID link if exists
                    if (asset.RevitElementGuid.HasValue)
                    {
                        _assetsByRevitGuid.Remove(asset.RevitElementGuid.Value);
                    }

                    asset.RevitElementGuid = revitGuid;
                    _assetsByRevitGuid[revitGuid] = asset;
                    asset.ModifiedDate = DateTime.UtcNow;

                    Logger.Info($"Linked asset {assetId} to Revit element {revitGuid}");
                    return true;
                }
                return false;
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Get asset by ID
        /// </summary>
        public Asset? GetById(string assetId)
        {
            lock (_lock)
            {
                return _assetsById.TryGetValue(assetId, out var asset) ? asset : null;
            }
        }

        /// <summary>
        /// Get asset by Revit element GUID - Key for BIM-FM integration
        /// </summary>
        public Asset? GetByRevitGuid(Guid revitGuid)
        {
            lock (_lock)
            {
                return _assetsByRevitGuid.TryGetValue(revitGuid, out var asset) ? asset : null;
            }
        }

        /// <summary>
        /// Get assets by location
        /// </summary>
        public IReadOnlyList<Asset> GetByLocation(string locationId)
        {
            lock (_lock)
            {
                return _assetsByLocation.TryGetValue(locationId, out var assets)
                    ? assets.ToList()
                    : new List<Asset>();
            }
        }

        /// <summary>
        /// Get assets by system
        /// </summary>
        public IReadOnlyList<Asset> GetBySystem(string system)
        {
            lock (_lock)
            {
                return _assetsBySystem.TryGetValue(system, out var assets)
                    ? assets.ToList()
                    : new List<Asset>();
            }
        }

        /// <summary>
        /// Get all critical assets
        /// </summary>
        public IReadOnlyList<Asset> GetCriticalAssets()
        {
            lock (_lock)
            {
                return _assetsById.Values
                    .Where(a => a.Criticality == AssetCriticality.Critical)
                    .ToList();
            }
        }

        /// <summary>
        /// Get assets requiring attention (maintenance due, poor condition, near EOL)
        /// </summary>
        public IReadOnlyList<Asset> GetAssetsRequiringAttention()
        {
            lock (_lock)
            {
                return _assetsById.Values
                    .Where(a => a.IsMaintenanceDue ||
                               a.Condition == AssetCondition.Poor ||
                               a.Condition == AssetCondition.Critical ||
                               a.IsNearEndOfLife ||
                               a.Status == AssetStatus.Degraded ||
                               a.Status == AssetStatus.Failed)
                    .OrderByDescending(a => a.Criticality)
                    .ThenByDescending(a => a.ConditionScore)
                    .ToList();
            }
        }

        /// <summary>
        /// Get assets under warranty
        /// </summary>
        public IReadOnlyList<Asset> GetAssetsUnderWarranty()
        {
            lock (_lock)
            {
                return _assetsById.Values
                    .Where(a => a.IsUnderWarranty)
                    .OrderBy(a => a.WarrantyExpiry)
                    .ToList();
            }
        }

        /// <summary>
        /// Get assets approaching end of life
        /// </summary>
        public IReadOnlyList<Asset> GetAssetsNearEndOfLife(int withinYears = 3)
        {
            lock (_lock)
            {
                return _assetsById.Values
                    .Where(a => a.RemainingLifeYears <= withinYears)
                    .OrderBy(a => a.RemainingLifeYears)
                    .ToList();
            }
        }

        /// <summary>
        /// Search assets by keyword
        /// </summary>
        public IReadOnlyList<Asset> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<Asset>();

            keyword = keyword.ToLower();

            lock (_lock)
            {
                return _assetsById.Values
                    .Where(a => a.Name.ToLower().Contains(keyword) ||
                               a.AssetId.ToLower().Contains(keyword) ||
                               a.AssetType.ToLower().Contains(keyword) ||
                               a.SerialNumber.ToLower().Contains(keyword) ||
                               a.BarCode.ToLower().Contains(keyword) ||
                               a.LocationId.ToLower().Contains(keyword))
                    .ToList();
            }
        }

        /// <summary>
        /// Get all assets
        /// </summary>
        public IReadOnlyList<Asset> GetAll()
        {
            lock (_lock)
            {
                return _assetsById.Values.ToList();
            }
        }

        /// <summary>
        /// Get all assets (alias for GetAll)
        /// </summary>
        public IReadOnlyList<Asset> GetAllAssets() => GetAll();

        /// <summary>
        /// Get asset count
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _assetsById.Count;
                }
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get asset statistics summary
        /// </summary>
        public AssetStatistics GetStatistics()
        {
            lock (_lock)
            {
                var assets = _assetsById.Values.ToList();

                return new AssetStatistics
                {
                    TotalAssets = assets.Count,
                    ByStatus = assets.GroupBy(a => a.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByCondition = assets.GroupBy(a => a.Condition)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByCriticality = assets.GroupBy(a => a.Criticality)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    BySystem = assets.GroupBy(a => a.System)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TotalReplacementValue = assets.Sum(a => a.ReplacementCost),
                    AverageAge = assets.Any() ? assets.Average(a => a.AgeYears) : 0,
                    AssetsUnderWarranty = assets.Count(a => a.IsUnderWarranty),
                    AssetsNearEOL = assets.Count(a => a.IsNearEndOfLife),
                    MaintenanceDue = assets.Count(a => a.IsMaintenanceDue),
                    LinkedToBIM = assets.Count(a => a.RevitElementGuid.HasValue)
                };
            }
        }

        #endregion

        #region Private Helpers

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var current = string.Empty;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = string.Empty;
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);

            return result.ToArray();
        }

        private string GetValue(string[] values, string[] headers, string columnName)
        {
            var index = Array.IndexOf(headers, columnName);
            return index >= 0 && index < values.Length ? values[index].Trim() : string.Empty;
        }

        private DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, out var date) ? date : DateTime.Now;
        }

        private DateTime? ParseNullableDate(string value)
        {
            return DateTime.TryParse(value, out var date) ? date : null;
        }

        private int ParseInt(string value, int defaultValue = 0)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        private decimal ParseDecimal(string value)
        {
            return decimal.TryParse(value, out var result) ? result : 0;
        }

        private AssetCriticality ParseCriticality(string value)
        {
            return value?.ToLower() switch
            {
                "critical" => AssetCriticality.Critical,
                "high" => AssetCriticality.High,
                "low" => AssetCriticality.Low,
                _ => AssetCriticality.Standard
            };
        }

        private AssetStatus ParseStatus(string value)
        {
            return value?.ToLower() switch
            {
                "operational" => AssetStatus.Operational,
                "degraded" => AssetStatus.Degraded,
                "failed" => AssetStatus.Failed,
                "standby" => AssetStatus.Standby,
                "decommissioned" => AssetStatus.Decommissioned,
                _ => AssetStatus.Operational
            };
        }

        #endregion
    }

    /// <summary>
    /// Asset statistics summary
    /// </summary>
    public class AssetStatistics
    {
        public int TotalAssets { get; set; }
        public Dictionary<AssetStatus, int> ByStatus { get; set; } = new();
        public Dictionary<AssetCondition, int> ByCondition { get; set; } = new();
        public Dictionary<AssetCriticality, int> ByCriticality { get; set; } = new();
        public Dictionary<string, int> BySystem { get; set; } = new();
        public decimal TotalReplacementValue { get; set; }
        public double AverageAge { get; set; }
        public int AssetsUnderWarranty { get; set; }
        public int AssetsNearEOL { get; set; }
        public int MaintenanceDue { get; set; }
        public int LinkedToBIM { get; set; }
    }

    #endregion
}
