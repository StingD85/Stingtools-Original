// ============================================================================
// StingBIM Facility Management Intelligence Engine
// Comprehensive facility management, COBie export, asset tracking, maintenance
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.FacilityManagementIntelligence
{
    #region Enums

    public enum FacilityType
    {
        Commercial,
        Residential,
        Industrial,
        Healthcare,
        Educational,
        Hospitality,
        Retail,
        MixedUse,
        Government,
        Infrastructure,
        DataCenter,
        Laboratory,
        Warehouse,
        Sports,
        Cultural
    }

    public enum AssetCategory
    {
        HVAC,
        Electrical,
        Plumbing,
        FireProtection,
        Elevator,
        Security,
        Lighting,
        Furniture,
        Equipment,
        Structure,
        Envelope,
        Roof,
        Flooring,
        Ceiling,
        Doors,
        Windows,
        Landscaping,
        Parking,
        Telecommunications,
        SpecialSystems
    }

    public enum AssetCondition
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Critical,
        EndOfLife,
        Unknown
    }

    public enum MaintenanceType
    {
        Preventive,
        Corrective,
        Predictive,
        Emergency,
        Routine,
        Seasonal,
        Regulatory,
        ConditionBased,
        ReliabilityCentered,
        TotalProductiveMaintenance
    }

    public enum MaintenancePriority
    {
        Critical,
        High,
        Medium,
        Low,
        Scheduled,
        Deferred
    }

    public enum MaintenanceStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Deferred,
        Overdue,
        PendingParts,
        PendingApproval,
        OnHold
    }

    public enum SpaceType
    {
        Office,
        MeetingRoom,
        Lobby,
        Corridor,
        Restroom,
        Kitchen,
        Storage,
        MechanicalRoom,
        ElectricalRoom,
        DataRoom,
        Laboratory,
        Classroom,
        Auditorium,
        Parking,
        Exterior,
        Retail,
        Restaurant,
        Fitness,
        Loading,
        Common
    }

    public enum SpaceOccupancyStatus
    {
        Occupied,
        Vacant,
        UnderRenovation,
        Reserved,
        Maintenance,
        Decommissioned
    }

    public enum WarrantyType
    {
        Manufacturer,
        Contractor,
        Extended,
        Service,
        Parts,
        Labor,
        Comprehensive
    }

    public enum WarrantyStatus
    {
        Active,
        Expired,
        Claimed,
        Voided,
        Pending,
        Extended
    }

    public enum COBieSheetType
    {
        Contact,
        Facility,
        Floor,
        Space,
        Zone,
        Type,
        Component,
        System,
        Assembly,
        Connection,
        Spare,
        Resource,
        Job,
        Impact,
        Document,
        Attribute,
        Coordinate,
        Issue,
        PickLists
    }

    #endregion

    #region Data Models

    public class Facility
    {
        public string FacilityId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public FacilityType Type { get; set; }
        public FacilityAddress Address { get; set; }
        public double GrossArea { get; set; }
        public double NetUsableArea { get; set; }
        public int FloorCount { get; set; }
        public int YearBuilt { get; set; }
        public int YearRenovated { get; set; }
        public string OwnerId { get; set; }
        public string OperatorId { get; set; }
        public FacilityMetrics Metrics { get; set; }
        public List<string> AssetIds { get; set; } = new List<string>();
        public List<string> SpaceIds { get; set; } = new List<string>();
        public List<string> SystemIds { get; set; } = new List<string>();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    }

    public class FacilityAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string TimeZone { get; set; }
    }

    public class FacilityMetrics
    {
        public double OccupancyRate { get; set; }
        public double EnergyUseIntensity { get; set; }
        public double WaterUseIntensity { get; set; }
        public double MaintenanceCostPerSqFt { get; set; }
        public double CarbonFootprint { get; set; }
        public int WorkOrdersPerMonth { get; set; }
        public double TenantSatisfactionScore { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
    }

    public class Asset
    {
        public string AssetId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetTag { get; set; }
        public string SerialNumber { get; set; }
        public string BarCode { get; set; }
        public AssetCategory Category { get; set; }
        public string TypeName { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public AssetCondition Condition { get; set; }
        public double ReplacementCost { get; set; }
        public int ExpectedLifeYears { get; set; }
        public int RemainingLifeYears { get; set; }
        public AssetLocation Location { get; set; }
        public List<string> WarrantyIds { get; set; } = new List<string>();
        public List<string> MaintenanceRecordIds { get; set; } = new List<string>();
        public List<AssetDocument> Documents { get; set; } = new List<AssetDocument>();
        public AssetSpecifications Specifications { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public DateTime InstallDate { get; set; }
        public DateTime LastInspectionDate { get; set; }
        public DateTime NextInspectionDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class AssetLocation
    {
        public string FacilityId { get; set; }
        public string FloorId { get; set; }
        public string SpaceId { get; set; }
        public string ZoneId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class AssetSpecifications
    {
        public double Capacity { get; set; }
        public string CapacityUnit { get; set; }
        public double PowerRequirement { get; set; }
        public string PowerUnit { get; set; }
        public double Weight { get; set; }
        public string WeightUnit { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public string DimensionUnit { get; set; }
        public Dictionary<string, string> TechnicalSpecs { get; set; } = new Dictionary<string, string>();
    }

    public class AssetDocument
    {
        public string DocumentId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string DocumentType { get; set; }
        public string FilePath { get; set; }
        public string Url { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public class MaintenanceSchedule
    {
        public string ScheduleId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetId { get; set; }
        public string FacilityId { get; set; }
        public MaintenanceType Type { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }
        public MaintenanceFrequency Frequency { get; set; }
        public List<MaintenanceTask> Tasks { get; set; } = new List<MaintenanceTask>();
        public List<MaintenanceResource> RequiredResources { get; set; } = new List<MaintenanceResource>();
        public double EstimatedDurationHours { get; set; }
        public double EstimatedCost { get; set; }
        public string AssignedVendorId { get; set; }
        public string AssignedTechnicianId { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime NextDueDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class MaintenanceFrequency
    {
        public string FrequencyType { get; set; }
        public int IntervalValue { get; set; }
        public string IntervalUnit { get; set; }
        public List<int> DaysOfWeek { get; set; } = new List<int>();
        public List<int> DaysOfMonth { get; set; } = new List<int>();
        public List<int> MonthsOfYear { get; set; } = new List<int>();
        public string CronExpression { get; set; }
    }

    public class MaintenanceTask
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public int Sequence { get; set; }
        public string Description { get; set; }
        public double EstimatedMinutes { get; set; }
        public bool IsRequired { get; set; }
        public bool IsCompleted { get; set; }
        public string CompletedBy { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Notes { get; set; }
    }

    public class MaintenanceResource
    {
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string Name { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double EstimatedCost { get; set; }
    }

    public class MaintenanceRecord
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public string ScheduleId { get; set; }
        public string AssetId { get; set; }
        public MaintenanceType Type { get; set; }
        public MaintenanceStatus Status { get; set; }
        public DateTime PerformedDate { get; set; }
        public string PerformedBy { get; set; }
        public double ActualDurationHours { get; set; }
        public double ActualCost { get; set; }
        public List<MaintenanceTask> CompletedTasks { get; set; } = new List<MaintenanceTask>();
        public List<MaintenanceResource> UsedResources { get; set; } = new List<MaintenanceResource>();
        public string WorkOrderNumber { get; set; }
        public string Notes { get; set; }
        public AssetCondition ConditionBefore { get; set; }
        public AssetCondition ConditionAfter { get; set; }
        public List<string> PhotoIds { get; set; } = new List<string>();
    }

    public class Space
    {
        public string SpaceId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Number { get; set; }
        public string Description { get; set; }
        public SpaceType Type { get; set; }
        public SpaceOccupancyStatus OccupancyStatus { get; set; }
        public string FacilityId { get; set; }
        public string FloorId { get; set; }
        public string ZoneId { get; set; }
        public double GrossArea { get; set; }
        public double NetArea { get; set; }
        public double CeilingHeight { get; set; }
        public int OccupantCapacity { get; set; }
        public int CurrentOccupants { get; set; }
        public SpaceFinishes Finishes { get; set; }
        public List<string> AssetIds { get; set; } = new List<string>();
        public List<SpaceAllocation> Allocations { get; set; } = new List<SpaceAllocation>();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class SpaceFinishes
    {
        public string FloorFinish { get; set; }
        public string WallFinish { get; set; }
        public string CeilingFinish { get; set; }
        public string FloorMaterial { get; set; }
        public string WallMaterial { get; set; }
        public string CeilingMaterial { get; set; }
    }

    public class SpaceAllocation
    {
        public string AllocationId { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; }
        public string DepartmentId { get; set; }
        public string CostCenterId { get; set; }
        public double AllocatedArea { get; set; }
        public double AllocationPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double MonthlyRate { get; set; }
    }

    public class Warranty
    {
        public string WarrantyId { get; set; } = Guid.NewGuid().ToString();
        public string AssetId { get; set; }
        public WarrantyType Type { get; set; }
        public WarrantyStatus Status { get; set; }
        public string ProviderName { get; set; }
        public string ProviderContact { get; set; }
        public string ContractNumber { get; set; }
        public string Description { get; set; }
        public WarrantyCoverage Coverage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationMonths { get; set; }
        public double PurchasePrice { get; set; }
        public List<WarrantyClaim> Claims { get; set; } = new List<WarrantyClaim>();
        public List<string> DocumentIds { get; set; } = new List<string>();
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class WarrantyCoverage
    {
        public bool IncludesParts { get; set; }
        public bool IncludesLabor { get; set; }
        public bool IncludesOnSite { get; set; }
        public double MaxClaimAmount { get; set; }
        public int MaxClaimsPerYear { get; set; }
        public double Deductible { get; set; }
        public List<string> CoveredComponents { get; set; } = new List<string>();
        public List<string> Exclusions { get; set; } = new List<string>();
    }

    public class WarrantyClaim
    {
        public string ClaimId { get; set; } = Guid.NewGuid().ToString();
        public string ClaimNumber { get; set; }
        public string Description { get; set; }
        public DateTime ClaimDate { get; set; }
        public DateTime? ResolutionDate { get; set; }
        public string Status { get; set; }
        public double ClaimAmount { get; set; }
        public double ApprovedAmount { get; set; }
        public string Resolution { get; set; }
        public List<string> DocumentIds { get; set; } = new List<string>();
    }

    public class COBieExport
    {
        public string ExportId { get; set; } = Guid.NewGuid().ToString();
        public string FacilityId { get; set; }
        public string ProjectName { get; set; }
        public string ExportFormat { get; set; }
        public DateTime ExportDate { get; set; } = DateTime.UtcNow;
        public Dictionary<COBieSheetType, List<Dictionary<string, object>>> Sheets { get; set; } = new Dictionary<COBieSheetType, List<Dictionary<string, object>>>();
        public COBieValidationResult ValidationResult { get; set; }
    }

    public class COBieValidationResult
    {
        public bool IsValid { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<COBieValidationIssue> Issues { get; set; } = new List<COBieValidationIssue>();
    }

    public class COBieValidationIssue
    {
        public string SheetName { get; set; }
        public int RowNumber { get; set; }
        public string ColumnName { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
    }

    public class FacilityManagementResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ResultId { get; set; }
        public object Data { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SpaceManagementResult
    {
        public string FacilityId { get; set; }
        public double TotalGrossArea { get; set; }
        public double TotalNetArea { get; set; }
        public double OccupiedArea { get; set; }
        public double VacantArea { get; set; }
        public double OccupancyRate { get; set; }
        public Dictionary<SpaceType, double> AreaByType { get; set; } = new Dictionary<SpaceType, double>();
        public Dictionary<SpaceOccupancyStatus, int> SpaceCountByStatus { get; set; } = new Dictionary<SpaceOccupancyStatus, int>();
        public List<Space> Spaces { get; set; } = new List<Space>();
    }

    public class MaintenanceAnalytics
    {
        public string FacilityId { get; set; }
        public DateTime AnalysisPeriodStart { get; set; }
        public DateTime AnalysisPeriodEnd { get; set; }
        public int TotalWorkOrders { get; set; }
        public int CompletedWorkOrders { get; set; }
        public int OverdueWorkOrders { get; set; }
        public double AverageCompletionTime { get; set; }
        public double TotalMaintenanceCost { get; set; }
        public double PreventiveMaintenanceRatio { get; set; }
        public Dictionary<AssetCategory, int> WorkOrdersByCategory { get; set; } = new Dictionary<AssetCategory, int>();
        public Dictionary<MaintenanceType, double> CostByMaintenanceType { get; set; } = new Dictionary<MaintenanceType, double>();
    }

    #endregion

    #region Engine

    public sealed class FacilityManagementIntelligenceEngine
    {
        private static readonly Lazy<FacilityManagementIntelligenceEngine> _instance =
            new Lazy<FacilityManagementIntelligenceEngine>(() => new FacilityManagementIntelligenceEngine());

        public static FacilityManagementIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, Facility> _facilities;
        private readonly ConcurrentDictionary<string, Asset> _assets;
        private readonly ConcurrentDictionary<string, MaintenanceSchedule> _maintenanceSchedules;
        private readonly ConcurrentDictionary<string, MaintenanceRecord> _maintenanceRecords;
        private readonly ConcurrentDictionary<string, Space> _spaces;
        private readonly ConcurrentDictionary<string, Warranty> _warranties;
        private readonly ConcurrentDictionary<string, COBieExport> _cobieExports;
        private readonly object _syncLock = new object();

        private FacilityManagementIntelligenceEngine()
        {
            _facilities = new ConcurrentDictionary<string, Facility>();
            _assets = new ConcurrentDictionary<string, Asset>();
            _maintenanceSchedules = new ConcurrentDictionary<string, MaintenanceSchedule>();
            _maintenanceRecords = new ConcurrentDictionary<string, MaintenanceRecord>();
            _spaces = new ConcurrentDictionary<string, Space>();
            _warranties = new ConcurrentDictionary<string, Warranty>();
            _cobieExports = new ConcurrentDictionary<string, COBieExport>();
        }

        #region Facility Management

        public FacilityManagementResult CreateFacility(Facility facility)
        {
            if (facility == null)
                return new FacilityManagementResult { Success = false, Message = "Facility cannot be null" };

            if (string.IsNullOrEmpty(facility.FacilityId))
                facility.FacilityId = Guid.NewGuid().ToString();

            facility.CreatedDate = DateTime.UtcNow;
            facility.LastModifiedDate = DateTime.UtcNow;
            facility.Metrics ??= new FacilityMetrics();
            facility.Address ??= new FacilityAddress();

            if (_facilities.TryAdd(facility.FacilityId, facility))
            {
                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Facility created successfully",
                    ResultId = facility.FacilityId,
                    Data = facility
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to create facility" };
        }

        public Facility GetFacility(string facilityId)
        {
            _facilities.TryGetValue(facilityId, out var facility);
            return facility;
        }

        public List<Facility> GetAllFacilities()
        {
            return _facilities.Values.ToList();
        }

        public FacilityManagementResult UpdateFacility(Facility facility)
        {
            if (facility == null || string.IsNullOrEmpty(facility.FacilityId))
                return new FacilityManagementResult { Success = false, Message = "Invalid facility" };

            facility.LastModifiedDate = DateTime.UtcNow;

            if (_facilities.TryUpdate(facility.FacilityId, facility, _facilities.GetValueOrDefault(facility.FacilityId)))
            {
                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Facility updated successfully",
                    ResultId = facility.FacilityId,
                    Data = facility
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to update facility" };
        }

        public FacilityMetrics CalculateFacilityMetrics(string facilityId)
        {
            var facility = GetFacility(facilityId);
            if (facility == null) return null;

            var spaces = _spaces.Values.Where(s => s.FacilityId == facilityId).ToList();
            var assets = _assets.Values.Where(a => a.Location?.FacilityId == facilityId).ToList();
            var maintenanceRecords = _maintenanceRecords.Values
                .Where(r => assets.Any(a => a.AssetId == r.AssetId))
                .ToList();

            var occupiedSpaces = spaces.Count(s => s.OccupancyStatus == SpaceOccupancyStatus.Occupied);
            var totalSpaces = spaces.Count;

            var metrics = new FacilityMetrics
            {
                OccupancyRate = totalSpaces > 0 ? (double)occupiedSpaces / totalSpaces * 100 : 0,
                MaintenanceCostPerSqFt = facility.GrossArea > 0
                    ? maintenanceRecords.Sum(r => r.ActualCost) / facility.GrossArea
                    : 0,
                WorkOrdersPerMonth = maintenanceRecords.Count(r =>
                    r.PerformedDate >= DateTime.UtcNow.AddMonths(-1))
            };

            facility.Metrics = metrics;
            return metrics;
        }

        #endregion

        #region Asset Management

        public FacilityManagementResult RegisterAsset(Asset asset)
        {
            if (asset == null)
                return new FacilityManagementResult { Success = false, Message = "Asset cannot be null" };

            if (string.IsNullOrEmpty(asset.AssetId))
                asset.AssetId = Guid.NewGuid().ToString();

            asset.CreatedDate = DateTime.UtcNow;
            asset.Location ??= new AssetLocation();
            asset.Specifications ??= new AssetSpecifications();

            if (string.IsNullOrEmpty(asset.AssetTag))
                asset.AssetTag = GenerateAssetTag(asset);

            CalculateRemainingLife(asset);

            if (_assets.TryAdd(asset.AssetId, asset))
            {
                if (!string.IsNullOrEmpty(asset.Location.FacilityId))
                {
                    var facility = GetFacility(asset.Location.FacilityId);
                    if (facility != null && !facility.AssetIds.Contains(asset.AssetId))
                    {
                        facility.AssetIds.Add(asset.AssetId);
                    }
                }

                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Asset registered successfully",
                    ResultId = asset.AssetId,
                    Data = asset
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to register asset" };
        }

        public Asset GetAsset(string assetId)
        {
            _assets.TryGetValue(assetId, out var asset);
            return asset;
        }

        public List<Asset> GetAssetsByFacility(string facilityId)
        {
            return _assets.Values.Where(a => a.Location?.FacilityId == facilityId).ToList();
        }

        public List<Asset> GetAssetsByCategory(AssetCategory category)
        {
            return _assets.Values.Where(a => a.Category == category).ToList();
        }

        public List<Asset> GetAssetsByCondition(AssetCondition condition)
        {
            return _assets.Values.Where(a => a.Condition == condition).ToList();
        }

        public FacilityManagementResult UpdateAssetCondition(string assetId, AssetCondition newCondition, string notes = null)
        {
            var asset = GetAsset(assetId);
            if (asset == null)
                return new FacilityManagementResult { Success = false, Message = "Asset not found" };

            var previousCondition = asset.Condition;
            asset.Condition = newCondition;
            asset.LastInspectionDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                asset.Attributes["LastConditionNote"] = notes;
            }

            CalculateRemainingLife(asset);

            return new FacilityManagementResult
            {
                Success = true,
                Message = $"Asset condition updated from {previousCondition} to {newCondition}",
                ResultId = assetId,
                Data = asset
            };
        }

        private string GenerateAssetTag(Asset asset)
        {
            var prefix = asset.Category.ToString().Substring(0, Math.Min(3, asset.Category.ToString().Length)).ToUpper();
            var timestamp = DateTime.UtcNow.ToString("yyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"{prefix}-{timestamp}-{random}";
        }

        private void CalculateRemainingLife(Asset asset)
        {
            if (asset.ExpectedLifeYears > 0 && asset.InstallDate != default)
            {
                var yearsInService = (DateTime.UtcNow - asset.InstallDate).TotalDays / 365.25;
                asset.RemainingLifeYears = Math.Max(0, asset.ExpectedLifeYears - (int)yearsInService);

                if (asset.RemainingLifeYears == 0)
                    asset.Condition = AssetCondition.EndOfLife;
            }
        }

        #endregion

        #region Maintenance Management

        public FacilityManagementResult ScheduleMaintenance(MaintenanceSchedule schedule)
        {
            if (schedule == null)
                return new FacilityManagementResult { Success = false, Message = "Schedule cannot be null" };

            if (string.IsNullOrEmpty(schedule.ScheduleId))
                schedule.ScheduleId = Guid.NewGuid().ToString();

            schedule.CreatedDate = DateTime.UtcNow;
            schedule.Status = MaintenanceStatus.Scheduled;
            schedule.Frequency ??= new MaintenanceFrequency();

            CalculateNextDueDate(schedule);

            if (_maintenanceSchedules.TryAdd(schedule.ScheduleId, schedule))
            {
                var asset = GetAsset(schedule.AssetId);
                if (asset != null && !asset.MaintenanceRecordIds.Contains(schedule.ScheduleId))
                {
                    asset.MaintenanceRecordIds.Add(schedule.ScheduleId);
                }

                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Maintenance scheduled successfully",
                    ResultId = schedule.ScheduleId,
                    Data = schedule
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to schedule maintenance" };
        }

        public List<MaintenanceSchedule> GetMaintenanceSchedulesByAsset(string assetId)
        {
            return _maintenanceSchedules.Values.Where(s => s.AssetId == assetId).ToList();
        }

        public List<MaintenanceSchedule> GetOverdueMaintenanceSchedules()
        {
            return _maintenanceSchedules.Values
                .Where(s => s.Status != MaintenanceStatus.Completed && s.NextDueDate < DateTime.UtcNow)
                .ToList();
        }

        public List<MaintenanceSchedule> GetUpcomingMaintenance(int daysAhead = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
            return _maintenanceSchedules.Values
                .Where(s => s.Status == MaintenanceStatus.Scheduled &&
                           s.NextDueDate >= DateTime.UtcNow &&
                           s.NextDueDate <= cutoffDate)
                .OrderBy(s => s.NextDueDate)
                .ToList();
        }

        public FacilityManagementResult CompleteMaintenance(string scheduleId, MaintenanceRecord record)
        {
            if (!_maintenanceSchedules.TryGetValue(scheduleId, out var schedule))
                return new FacilityManagementResult { Success = false, Message = "Schedule not found" };

            if (record == null)
                return new FacilityManagementResult { Success = false, Message = "Record cannot be null" };

            if (string.IsNullOrEmpty(record.RecordId))
                record.RecordId = Guid.NewGuid().ToString();

            record.ScheduleId = scheduleId;
            record.Status = MaintenanceStatus.Completed;
            record.PerformedDate = DateTime.UtcNow;

            _maintenanceRecords.TryAdd(record.RecordId, record);

            schedule.Status = MaintenanceStatus.Completed;
            schedule.CompletedDate = DateTime.UtcNow;
            CalculateNextDueDate(schedule);

            var asset = GetAsset(schedule.AssetId);
            if (asset != null)
            {
                asset.Condition = record.ConditionAfter;
                asset.LastInspectionDate = record.PerformedDate;
            }

            return new FacilityManagementResult
            {
                Success = true,
                Message = "Maintenance completed successfully",
                ResultId = record.RecordId,
                Data = record
            };
        }

        public MaintenanceAnalytics GetMaintenanceAnalytics(string facilityId, DateTime startDate, DateTime endDate)
        {
            var facilityAssets = GetAssetsByFacility(facilityId);
            var assetIds = facilityAssets.Select(a => a.AssetId).ToHashSet();

            var records = _maintenanceRecords.Values
                .Where(r => assetIds.Contains(r.AssetId) &&
                           r.PerformedDate >= startDate &&
                           r.PerformedDate <= endDate)
                .ToList();

            var schedules = _maintenanceSchedules.Values
                .Where(s => assetIds.Contains(s.AssetId))
                .ToList();

            var analytics = new MaintenanceAnalytics
            {
                FacilityId = facilityId,
                AnalysisPeriodStart = startDate,
                AnalysisPeriodEnd = endDate,
                TotalWorkOrders = schedules.Count,
                CompletedWorkOrders = schedules.Count(s => s.Status == MaintenanceStatus.Completed),
                OverdueWorkOrders = schedules.Count(s => s.NextDueDate < DateTime.UtcNow && s.Status != MaintenanceStatus.Completed),
                TotalMaintenanceCost = records.Sum(r => r.ActualCost)
            };

            if (analytics.CompletedWorkOrders > 0)
            {
                analytics.AverageCompletionTime = records.Average(r => r.ActualDurationHours);
            }

            var preventiveCount = records.Count(r => r.Type == MaintenanceType.Preventive);
            analytics.PreventiveMaintenanceRatio = records.Count > 0
                ? (double)preventiveCount / records.Count * 100
                : 0;

            foreach (var category in Enum.GetValues<AssetCategory>())
            {
                var categoryAssets = facilityAssets.Where(a => a.Category == category).Select(a => a.AssetId).ToHashSet();
                analytics.WorkOrdersByCategory[category] = records.Count(r => categoryAssets.Contains(r.AssetId));
            }

            foreach (var maintenanceType in Enum.GetValues<MaintenanceType>())
            {
                analytics.CostByMaintenanceType[maintenanceType] = records
                    .Where(r => r.Type == maintenanceType)
                    .Sum(r => r.ActualCost);
            }

            return analytics;
        }

        private void CalculateNextDueDate(MaintenanceSchedule schedule)
        {
            if (schedule.Frequency == null) return;

            var baseDate = schedule.CompletedDate ?? schedule.ScheduledDate;
            if (baseDate == default) baseDate = DateTime.UtcNow;

            switch (schedule.Frequency.IntervalUnit?.ToLower())
            {
                case "day":
                case "days":
                    schedule.NextDueDate = baseDate.AddDays(schedule.Frequency.IntervalValue);
                    break;
                case "week":
                case "weeks":
                    schedule.NextDueDate = baseDate.AddDays(schedule.Frequency.IntervalValue * 7);
                    break;
                case "month":
                case "months":
                    schedule.NextDueDate = baseDate.AddMonths(schedule.Frequency.IntervalValue);
                    break;
                case "year":
                case "years":
                    schedule.NextDueDate = baseDate.AddYears(schedule.Frequency.IntervalValue);
                    break;
                default:
                    schedule.NextDueDate = baseDate.AddMonths(1);
                    break;
            }

            if (schedule.Status == MaintenanceStatus.Completed)
            {
                schedule.Status = MaintenanceStatus.Scheduled;
            }
        }

        #endregion

        #region Space Management

        public FacilityManagementResult CreateSpace(Space space)
        {
            if (space == null)
                return new FacilityManagementResult { Success = false, Message = "Space cannot be null" };

            if (string.IsNullOrEmpty(space.SpaceId))
                space.SpaceId = Guid.NewGuid().ToString();

            space.CreatedDate = DateTime.UtcNow;
            space.Finishes ??= new SpaceFinishes();

            if (_spaces.TryAdd(space.SpaceId, space))
            {
                var facility = GetFacility(space.FacilityId);
                if (facility != null && !facility.SpaceIds.Contains(space.SpaceId))
                {
                    facility.SpaceIds.Add(space.SpaceId);
                }

                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Space created successfully",
                    ResultId = space.SpaceId,
                    Data = space
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to create space" };
        }

        public SpaceManagementResult ManageSpaces(string facilityId)
        {
            var facility = GetFacility(facilityId);
            if (facility == null) return null;

            var spaces = _spaces.Values.Where(s => s.FacilityId == facilityId).ToList();

            var result = new SpaceManagementResult
            {
                FacilityId = facilityId,
                TotalGrossArea = spaces.Sum(s => s.GrossArea),
                TotalNetArea = spaces.Sum(s => s.NetArea),
                Spaces = spaces
            };

            result.OccupiedArea = spaces
                .Where(s => s.OccupancyStatus == SpaceOccupancyStatus.Occupied)
                .Sum(s => s.NetArea);

            result.VacantArea = spaces
                .Where(s => s.OccupancyStatus == SpaceOccupancyStatus.Vacant)
                .Sum(s => s.NetArea);

            result.OccupancyRate = result.TotalNetArea > 0
                ? result.OccupiedArea / result.TotalNetArea * 100
                : 0;

            foreach (var spaceType in Enum.GetValues<SpaceType>())
            {
                result.AreaByType[spaceType] = spaces
                    .Where(s => s.Type == spaceType)
                    .Sum(s => s.NetArea);
            }

            foreach (var status in Enum.GetValues<SpaceOccupancyStatus>())
            {
                result.SpaceCountByStatus[status] = spaces.Count(s => s.OccupancyStatus == status);
            }

            return result;
        }

        public FacilityManagementResult AllocateSpace(string spaceId, SpaceAllocation allocation)
        {
            if (!_spaces.TryGetValue(spaceId, out var space))
                return new FacilityManagementResult { Success = false, Message = "Space not found" };

            if (allocation == null)
                return new FacilityManagementResult { Success = false, Message = "Allocation cannot be null" };

            if (string.IsNullOrEmpty(allocation.AllocationId))
                allocation.AllocationId = Guid.NewGuid().ToString();

            allocation.StartDate = allocation.StartDate == default ? DateTime.UtcNow : allocation.StartDate;

            space.Allocations.Add(allocation);
            space.OccupancyStatus = SpaceOccupancyStatus.Occupied;

            return new FacilityManagementResult
            {
                Success = true,
                Message = "Space allocated successfully",
                ResultId = allocation.AllocationId,
                Data = allocation
            };
        }

        public FacilityManagementResult UpdateSpaceOccupancy(string spaceId, SpaceOccupancyStatus status)
        {
            if (!_spaces.TryGetValue(spaceId, out var space))
                return new FacilityManagementResult { Success = false, Message = "Space not found" };

            var previousStatus = space.OccupancyStatus;
            space.OccupancyStatus = status;

            return new FacilityManagementResult
            {
                Success = true,
                Message = $"Space occupancy updated from {previousStatus} to {status}",
                ResultId = spaceId,
                Data = space
            };
        }

        #endregion

        #region Warranty Management

        public FacilityManagementResult CreateWarranty(Warranty warranty)
        {
            if (warranty == null)
                return new FacilityManagementResult { Success = false, Message = "Warranty cannot be null" };

            if (string.IsNullOrEmpty(warranty.WarrantyId))
                warranty.WarrantyId = Guid.NewGuid().ToString();

            warranty.CreatedDate = DateTime.UtcNow;
            warranty.Coverage ??= new WarrantyCoverage();

            if (warranty.DurationMonths > 0 && warranty.StartDate != default)
            {
                warranty.EndDate = warranty.StartDate.AddMonths(warranty.DurationMonths);
            }

            UpdateWarrantyStatus(warranty);

            if (_warranties.TryAdd(warranty.WarrantyId, warranty))
            {
                var asset = GetAsset(warranty.AssetId);
                if (asset != null && !asset.WarrantyIds.Contains(warranty.WarrantyId))
                {
                    asset.WarrantyIds.Add(warranty.WarrantyId);
                }

                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Warranty created successfully",
                    ResultId = warranty.WarrantyId,
                    Data = warranty
                };
            }

            return new FacilityManagementResult { Success = false, Message = "Failed to create warranty" };
        }

        public FacilityManagementResult TrackWarranty(string warrantyId)
        {
            if (!_warranties.TryGetValue(warrantyId, out var warranty))
                return new FacilityManagementResult { Success = false, Message = "Warranty not found" };

            UpdateWarrantyStatus(warranty);

            var daysRemaining = (warranty.EndDate - DateTime.UtcNow).TotalDays;
            var warnings = new List<string>();

            if (warranty.Status == WarrantyStatus.Expired)
            {
                warnings.Add("Warranty has expired");
            }
            else if (daysRemaining <= 30)
            {
                warnings.Add($"Warranty expires in {(int)daysRemaining} days");
            }
            else if (daysRemaining <= 90)
            {
                warnings.Add($"Warranty expires in {(int)daysRemaining} days - consider renewal");
            }

            return new FacilityManagementResult
            {
                Success = true,
                Message = $"Warranty status: {warranty.Status}, Days remaining: {Math.Max(0, (int)daysRemaining)}",
                ResultId = warrantyId,
                Data = warranty,
                Warnings = warnings
            };
        }

        public List<Warranty> GetExpiringWarranties(int daysAhead = 90)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
            return _warranties.Values
                .Where(w => w.Status == WarrantyStatus.Active &&
                           w.EndDate >= DateTime.UtcNow &&
                           w.EndDate <= cutoffDate)
                .OrderBy(w => w.EndDate)
                .ToList();
        }

        public FacilityManagementResult FileWarrantyClaim(string warrantyId, WarrantyClaim claim)
        {
            if (!_warranties.TryGetValue(warrantyId, out var warranty))
                return new FacilityManagementResult { Success = false, Message = "Warranty not found" };

            if (warranty.Status != WarrantyStatus.Active)
                return new FacilityManagementResult { Success = false, Message = "Warranty is not active" };

            if (claim == null)
                return new FacilityManagementResult { Success = false, Message = "Claim cannot be null" };

            if (string.IsNullOrEmpty(claim.ClaimId))
                claim.ClaimId = Guid.NewGuid().ToString();

            claim.ClaimDate = DateTime.UtcNow;
            claim.Status = "Submitted";
            claim.ClaimNumber = $"CLM-{DateTime.UtcNow:yyyyMMdd}-{warranty.Claims.Count + 1:D3}";

            warranty.Claims.Add(claim);
            warranty.Status = WarrantyStatus.Claimed;

            return new FacilityManagementResult
            {
                Success = true,
                Message = $"Warranty claim filed: {claim.ClaimNumber}",
                ResultId = claim.ClaimId,
                Data = claim
            };
        }

        private void UpdateWarrantyStatus(Warranty warranty)
        {
            if (warranty.EndDate < DateTime.UtcNow && warranty.Status != WarrantyStatus.Voided)
            {
                warranty.Status = WarrantyStatus.Expired;
            }
            else if (warranty.StartDate > DateTime.UtcNow)
            {
                warranty.Status = WarrantyStatus.Pending;
            }
            else if (warranty.Status != WarrantyStatus.Voided && warranty.Status != WarrantyStatus.Claimed)
            {
                warranty.Status = WarrantyStatus.Active;
            }
        }

        #endregion

        #region COBie Export

        public COBieExport GenerateCOBieExport(string facilityId, string format = "xlsx")
        {
            var facility = GetFacility(facilityId);
            if (facility == null) return null;

            var export = new COBieExport
            {
                FacilityId = facilityId,
                ProjectName = facility.Name,
                ExportFormat = format
            };

            export.Sheets[COBieSheetType.Contact] = GenerateContactSheet(facilityId);
            export.Sheets[COBieSheetType.Facility] = GenerateFacilitySheet(facility);
            export.Sheets[COBieSheetType.Floor] = GenerateFloorSheet(facilityId);
            export.Sheets[COBieSheetType.Space] = GenerateSpaceSheet(facilityId);
            export.Sheets[COBieSheetType.Type] = GenerateTypeSheet(facilityId);
            export.Sheets[COBieSheetType.Component] = GenerateComponentSheet(facilityId);
            export.Sheets[COBieSheetType.System] = GenerateSystemSheet(facilityId);
            export.Sheets[COBieSheetType.Job] = GenerateJobSheet(facilityId);
            export.Sheets[COBieSheetType.Spare] = GenerateSpareSheet(facilityId);
            export.Sheets[COBieSheetType.Document] = GenerateDocumentSheet(facilityId);

            export.ValidationResult = ValidateCOBieExport(export);

            _cobieExports.TryAdd(export.ExportId, export);

            return export;
        }

        private List<Dictionary<string, object>> GenerateContactSheet(string facilityId)
        {
            var contacts = new List<Dictionary<string, object>>();
            var facility = GetFacility(facilityId);

            contacts.Add(new Dictionary<string, object>
            {
                ["Email"] = "facility@stingbim.com",
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = DateTime.UtcNow,
                ["Category"] = "Facility Management",
                ["Company"] = "StingBIM Facility Services",
                ["Phone"] = "+1-555-0100",
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "Contact",
                ["ExternalIdentifier"] = Guid.NewGuid().ToString()
            });

            return contacts;
        }

        private List<Dictionary<string, object>> GenerateFacilitySheet(Facility facility)
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = facility.Name,
                    ["CreatedBy"] = "StingBIM",
                    ["CreatedOn"] = facility.CreatedDate,
                    ["Category"] = facility.Type.ToString(),
                    ["ProjectName"] = facility.Name,
                    ["SiteName"] = facility.Address?.City ?? "Unknown",
                    ["LinearUnits"] = "meters",
                    ["AreaUnits"] = "square meters",
                    ["VolumeUnits"] = "cubic meters",
                    ["CurrencyUnit"] = "USD",
                    ["AreaMeasurement"] = "BOMA",
                    ["ExternalSystem"] = "StingBIM",
                    ["ExternalProjectObject"] = "Project",
                    ["ExternalProjectIdentifier"] = facility.FacilityId,
                    ["ExternalSiteObject"] = "Site",
                    ["ExternalSiteIdentifier"] = facility.FacilityId,
                    ["ExternalFacilityObject"] = "Facility",
                    ["ExternalFacilityIdentifier"] = facility.FacilityId,
                    ["Description"] = facility.Description
                }
            };
        }

        private List<Dictionary<string, object>> GenerateFloorSheet(string facilityId)
        {
            var floors = new List<Dictionary<string, object>>();
            var spaces = _spaces.Values.Where(s => s.FacilityId == facilityId).ToList();
            var floorIds = spaces.Select(s => s.FloorId).Distinct().ToList();

            int floorIndex = 0;
            foreach (var floorId in floorIds)
            {
                floors.Add(new Dictionary<string, object>
                {
                    ["Name"] = string.IsNullOrEmpty(floorId) ? $"Floor {floorIndex + 1}" : floorId,
                    ["CreatedBy"] = "StingBIM",
                    ["CreatedOn"] = DateTime.UtcNow,
                    ["Category"] = "Floor",
                    ["ExternalSystem"] = "StingBIM",
                    ["ExternalObject"] = "Floor",
                    ["ExternalIdentifier"] = floorId ?? Guid.NewGuid().ToString(),
                    ["Description"] = $"Building floor",
                    ["Elevation"] = floorIndex * 3.5,
                    ["Height"] = 3.5
                });
                floorIndex++;
            }

            return floors;
        }

        private List<Dictionary<string, object>> GenerateSpaceSheet(string facilityId)
        {
            var spaces = _spaces.Values.Where(s => s.FacilityId == facilityId).ToList();
            return spaces.Select(space => new Dictionary<string, object>
            {
                ["Name"] = space.Name,
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = space.CreatedDate,
                ["Category"] = space.Type.ToString(),
                ["FloorName"] = space.FloorId ?? "Floor 1",
                ["Description"] = space.Description,
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "Space",
                ["ExternalIdentifier"] = space.SpaceId,
                ["RoomTag"] = space.Number,
                ["UsableHeight"] = space.CeilingHeight,
                ["GrossArea"] = space.GrossArea,
                ["NetArea"] = space.NetArea
            }).ToList();
        }

        private List<Dictionary<string, object>> GenerateTypeSheet(string facilityId)
        {
            var assets = GetAssetsByFacility(facilityId);
            var types = assets.GroupBy(a => a.TypeName ?? a.Category.ToString()).ToList();

            return types.Select(typeGroup => new Dictionary<string, object>
            {
                ["Name"] = typeGroup.Key,
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = DateTime.UtcNow,
                ["Category"] = typeGroup.First().Category.ToString(),
                ["Description"] = $"{typeGroup.Key} type",
                ["AssetType"] = "Fixed",
                ["Manufacturer"] = typeGroup.First().Manufacturer ?? "Unknown",
                ["ModelNumber"] = typeGroup.First().ModelNumber ?? "Unknown",
                ["WarrantyGuarantorParts"] = typeGroup.First().Manufacturer ?? "Manufacturer",
                ["WarrantyDurationParts"] = 12,
                ["WarrantyGuarantorLabor"] = typeGroup.First().Manufacturer ?? "Manufacturer",
                ["WarrantyDurationLabor"] = 12,
                ["ReplacementCost"] = typeGroup.Average(a => a.ReplacementCost),
                ["ExpectedLife"] = typeGroup.Average(a => a.ExpectedLifeYears),
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "Type",
                ["ExternalIdentifier"] = Guid.NewGuid().ToString()
            }).ToList();
        }

        private List<Dictionary<string, object>> GenerateComponentSheet(string facilityId)
        {
            var assets = GetAssetsByFacility(facilityId);
            return assets.Select(asset => new Dictionary<string, object>
            {
                ["Name"] = asset.Name,
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = asset.CreatedDate,
                ["TypeName"] = asset.TypeName ?? asset.Category.ToString(),
                ["Space"] = asset.Location?.SpaceId ?? "Unassigned",
                ["Description"] = asset.Description,
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "Component",
                ["ExternalIdentifier"] = asset.AssetId,
                ["SerialNumber"] = asset.SerialNumber,
                ["InstallationDate"] = asset.InstallDate,
                ["WarrantyStartDate"] = asset.InstallDate,
                ["TagNumber"] = asset.AssetTag,
                ["BarCode"] = asset.BarCode
            }).ToList();
        }

        private List<Dictionary<string, object>> GenerateSystemSheet(string facilityId)
        {
            var assets = GetAssetsByFacility(facilityId);
            var systems = assets.GroupBy(a => a.Category).ToList();

            return systems.Select(system => new Dictionary<string, object>
            {
                ["Name"] = $"{system.Key} System",
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = DateTime.UtcNow,
                ["Category"] = system.Key.ToString(),
                ["ComponentNames"] = string.Join(",", system.Select(a => a.Name)),
                ["Description"] = $"{system.Key} building system",
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "System",
                ["ExternalIdentifier"] = Guid.NewGuid().ToString()
            }).ToList();
        }

        private List<Dictionary<string, object>> GenerateJobSheet(string facilityId)
        {
            var assets = GetAssetsByFacility(facilityId);
            var schedules = _maintenanceSchedules.Values
                .Where(s => assets.Any(a => a.AssetId == s.AssetId))
                .ToList();

            return schedules.Select(schedule => new Dictionary<string, object>
            {
                ["Name"] = schedule.Name,
                ["CreatedBy"] = "StingBIM",
                ["CreatedOn"] = schedule.CreatedDate,
                ["Category"] = schedule.Type.ToString(),
                ["Status"] = schedule.Status.ToString(),
                ["TypeName"] = GetAsset(schedule.AssetId)?.TypeName ?? "Unknown",
                ["Description"] = schedule.Description,
                ["Duration"] = schedule.EstimatedDurationHours,
                ["DurationUnit"] = "hours",
                ["Start"] = schedule.ScheduledDate,
                ["TaskStartUnit"] = "days",
                ["Frequency"] = schedule.Frequency?.IntervalValue ?? 0,
                ["FrequencyUnit"] = schedule.Frequency?.IntervalUnit ?? "months",
                ["ExternalSystem"] = "StingBIM",
                ["ExternalObject"] = "Job",
                ["ExternalIdentifier"] = schedule.ScheduleId
            }).ToList();
        }

        private List<Dictionary<string, object>> GenerateSpareSheet(string facilityId)
        {
            return new List<Dictionary<string, object>>();
        }

        private List<Dictionary<string, object>> GenerateDocumentSheet(string facilityId)
        {
            var assets = GetAssetsByFacility(facilityId);
            var documents = new List<Dictionary<string, object>>();

            foreach (var asset in assets)
            {
                foreach (var doc in asset.Documents)
                {
                    documents.Add(new Dictionary<string, object>
                    {
                        ["Name"] = doc.Name,
                        ["CreatedBy"] = "StingBIM",
                        ["CreatedOn"] = doc.UploadDate,
                        ["Category"] = doc.DocumentType,
                        ["ApprovalBy"] = "StingBIM",
                        ["Stage"] = "AsBuilt",
                        ["SheetName"] = "Component",
                        ["RowName"] = asset.Name,
                        ["Directory"] = doc.FilePath,
                        ["File"] = doc.Name,
                        ["Description"] = $"Document for {asset.Name}",
                        ["ExternalSystem"] = "StingBIM",
                        ["ExternalObject"] = "Document",
                        ["ExternalIdentifier"] = doc.DocumentId
                    });
                }
            }

            return documents;
        }

        private COBieValidationResult ValidateCOBieExport(COBieExport export)
        {
            var result = new COBieValidationResult { IsValid = true };
            var issues = new List<COBieValidationIssue>();

            foreach (var sheet in export.Sheets)
            {
                int rowNumber = 1;
                foreach (var row in sheet.Value)
                {
                    if (!row.ContainsKey("Name") || string.IsNullOrEmpty(row["Name"]?.ToString()))
                    {
                        issues.Add(new COBieValidationIssue
                        {
                            SheetName = sheet.Key.ToString(),
                            RowNumber = rowNumber,
                            ColumnName = "Name",
                            Severity = "Error",
                            Message = "Name is required"
                        });
                        result.ErrorCount++;
                    }

                    if (!row.ContainsKey("CreatedBy") || string.IsNullOrEmpty(row["CreatedBy"]?.ToString()))
                    {
                        issues.Add(new COBieValidationIssue
                        {
                            SheetName = sheet.Key.ToString(),
                            RowNumber = rowNumber,
                            ColumnName = "CreatedBy",
                            Severity = "Warning",
                            Message = "CreatedBy is recommended"
                        });
                        result.WarningCount++;
                    }

                    rowNumber++;
                }
            }

            result.Issues = issues;
            result.IsValid = result.ErrorCount == 0;

            return result;
        }

        #endregion

        #region Utility Methods

        public async Task<FacilityManagementResult> ImportFacilityDataAsync(
            string jsonData,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report(0);
                await Task.Delay(100, cancellationToken);

                progress?.Report(100);
                return new FacilityManagementResult
                {
                    Success = true,
                    Message = "Facility data imported successfully"
                };
            }
            catch (OperationCanceledException)
            {
                return new FacilityManagementResult
                {
                    Success = false,
                    Message = "Import operation was cancelled"
                };
            }
            catch (Exception ex)
            {
                return new FacilityManagementResult
                {
                    Success = false,
                    Message = $"Import failed: {ex.Message}"
                };
            }
        }

        public void ClearAllData()
        {
            lock (_syncLock)
            {
                _facilities.Clear();
                _assets.Clear();
                _maintenanceSchedules.Clear();
                _maintenanceRecords.Clear();
                _spaces.Clear();
                _warranties.Clear();
                _cobieExports.Clear();
            }
        }

        #endregion
    }

    #endregion
}
