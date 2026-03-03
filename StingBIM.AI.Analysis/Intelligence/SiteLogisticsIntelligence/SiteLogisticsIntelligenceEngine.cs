// ============================================================================
// StingBIM Site Logistics Intelligence Engine
// Material tracking, equipment scheduling, laydown management, traffic planning
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.SiteLogisticsIntelligence
{
    #region Enums

    public enum MaterialStatus
    {
        Ordered,
        InTransit,
        Received,
        InStorage,
        Deployed,
        Installed,
        Returned,
        Damaged,
        Lost
    }

    public enum EquipmentStatus
    {
        Available,
        InUse,
        Maintenance,
        Breakdown,
        Reserved,
        InTransit,
        Decommissioned
    }

    public enum EquipmentType
    {
        Crane,
        Excavator,
        Loader,
        Forklift,
        Scaffolding,
        ConcreteEquipment,
        WeldingEquipment,
        GeneratingSet,
        Compressor,
        Pump,
        Hoist,
        Lift,
        Vehicle,
        HandTools,
        SafetyEquipment,
        SurveyingEquipment,
        Other
    }

    public enum LaydownAreaType
    {
        MaterialStorage,
        EquipmentParking,
        Fabrication,
        Assembly,
        Staging,
        Waste,
        Office,
        Welfare,
        Laydown,
        Temporary
    }

    public enum LaydownAreaStatus
    {
        Available,
        PartiallyOccupied,
        FullyOccupied,
        Reserved,
        UnderPreparation,
        Decommissioned
    }

    public enum TrafficRouteType
    {
        VehicleAccess,
        PedestrianAccess,
        EmergencyAccess,
        MaterialDelivery,
        EquipmentAccess,
        HeavyHaul,
        Restricted
    }

    public enum TrafficPriority
    {
        Emergency,
        High,
        Normal,
        Low
    }

    public enum WeatherCondition
    {
        Clear,
        Cloudy,
        Rain,
        HeavyRain,
        Snow,
        HeavySnow,
        Wind,
        HighWind,
        Storm,
        Fog,
        Extreme
    }

    public enum WeatherImpactSeverity
    {
        None,
        Minor,
        Moderate,
        Significant,
        Severe,
        WorkStoppage
    }

    public enum DeliveryPriority
    {
        Critical,
        High,
        Normal,
        Low,
        Flexible
    }

    public enum DeliveryStatus
    {
        Scheduled,
        Confirmed,
        InTransit,
        Arrived,
        Unloading,
        Completed,
        Delayed,
        Cancelled,
        Rescheduled
    }

    #endregion

    #region Data Models

    public class SiteLogisticsPlan
    {
        public string PlanId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string SiteName { get; set; }
        public string Description { get; set; }
        public SiteInfo SiteInfo { get; set; }
        public List<string> LaydownAreaIds { get; set; } = new List<string>();
        public List<string> TrafficRouteIds { get; set; } = new List<string>();
        public List<string> DeliveryScheduleIds { get; set; } = new List<string>();
        public PlanConfiguration Configuration { get; set; }
        public PlanMetrics Metrics { get; set; }
        public DateTime PlanStartDate { get; set; }
        public DateTime PlanEndDate { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    }

    public class SiteInfo
    {
        public string Address { get; set; }
        public double TotalArea { get; set; }
        public double UsableArea { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string TimeZone { get; set; }
        public List<string> AccessPoints { get; set; } = new List<string>();
        public List<string> Constraints { get; set; } = new List<string>();
        public WorkingHours WorkingHours { get; set; }
    }

    public class WorkingHours
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<int> WorkingDays { get; set; } = new List<int> { 1, 2, 3, 4, 5 };
        public List<DateTime> Holidays { get; set; } = new List<DateTime>();
        public bool AllowOvertimeDeliveries { get; set; }
        public bool AllowWeekendDeliveries { get; set; }
    }

    public class PlanConfiguration
    {
        public int MaxSimultaneousDeliveries { get; set; } = 3;
        public int DeliveryBufferMinutes { get; set; } = 30;
        public double SafetyZoneMeters { get; set; } = 5;
        public bool RequirePreBooking { get; set; } = true;
        public int PreBookingLeadHours { get; set; } = 24;
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
    }

    public class PlanMetrics
    {
        public int TotalDeliveries { get; set; }
        public int OnTimeDeliveries { get; set; }
        public int DelayedDeliveries { get; set; }
        public double AverageUnloadingTime { get; set; }
        public double StorageUtilization { get; set; }
        public double EquipmentUtilization { get; set; }
        public int SafetyIncidents { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
    }

    public class Material
    {
        public string MaterialId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string MaterialCode { get; set; }
        public string Category { get; set; }
        public MaterialSpecification Specification { get; set; }
        public MaterialStatus Status { get; set; }
        public MaterialLocation CurrentLocation { get; set; }
        public string SupplierId { get; set; }
        public string PurchaseOrderId { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double UnitCost { get; set; }
        public double TotalCost { get; set; }
        public List<MaterialMovement> MovementHistory { get; set; } = new List<MaterialMovement>();
        public MaterialRequirements Requirements { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public DateTime? ActualDeliveryDate { get; set; }
        public DateTime RequiredDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class MaterialSpecification
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Weight { get; set; }
        public string DimensionUnit { get; set; }
        public string WeightUnit { get; set; }
        public bool IsHazardous { get; set; }
        public bool RequiresSpecialHandling { get; set; }
        public string StorageRequirements { get; set; }
        public Dictionary<string, string> CustomSpecs { get; set; } = new Dictionary<string, string>();
    }

    public class MaterialLocation
    {
        public string LocationId { get; set; }
        public string LocationType { get; set; }
        public string AreaId { get; set; }
        public string ZoneId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    public class MaterialMovement
    {
        public string MovementId { get; set; } = Guid.NewGuid().ToString();
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public double Quantity { get; set; }
        public string MovedBy { get; set; }
        public string Reason { get; set; }
        public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    }

    public class MaterialRequirements
    {
        public bool RequiresCoveredStorage { get; set; }
        public bool RequiresClimateControl { get; set; }
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public double MaxHumidity { get; set; }
        public bool RequiresFlatSurface { get; set; }
        public double MinClearance { get; set; }
        public List<string> IncompatibleMaterials { get; set; } = new List<string>();
    }

    public class Equipment
    {
        public string EquipmentId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string AssetTag { get; set; }
        public EquipmentType Type { get; set; }
        public EquipmentStatus Status { get; set; }
        public EquipmentSpecification Specification { get; set; }
        public string OwnerId { get; set; }
        public string OperatorId { get; set; }
        public string CurrentLocationId { get; set; }
        public List<EquipmentScheduleEntry> Schedule { get; set; } = new List<EquipmentScheduleEntry>();
        public List<EquipmentMaintenance> MaintenanceHistory { get; set; } = new List<EquipmentMaintenance>();
        public double HourlyRate { get; set; }
        public double DailyRate { get; set; }
        public DateTime LastInspectionDate { get; set; }
        public DateTime NextInspectionDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class EquipmentSpecification
    {
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public int YearManufactured { get; set; }
        public double Capacity { get; set; }
        public string CapacityUnit { get; set; }
        public double MaxReach { get; set; }
        public double MaxHeight { get; set; }
        public double Weight { get; set; }
        public double FuelCapacity { get; set; }
        public string FuelType { get; set; }
        public List<string> RequiredCertifications { get; set; } = new List<string>();
    }

    public class EquipmentScheduleEntry
    {
        public string EntryId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskDescription { get; set; }
        public string AssignedTo { get; set; }
        public string LocationId { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
    }

    public class EquipmentMaintenance
    {
        public string MaintenanceId { get; set; } = Guid.NewGuid().ToString();
        public string MaintenanceType { get; set; }
        public string Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string PerformedBy { get; set; }
        public double Cost { get; set; }
        public string Notes { get; set; }
    }

    public class LaydownArea
    {
        public string AreaId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string PlanId { get; set; }
        public LaydownAreaType Type { get; set; }
        public LaydownAreaStatus Status { get; set; }
        public AreaDimensions Dimensions { get; set; }
        public AreaCapacity Capacity { get; set; }
        public AreaLocation Location { get; set; }
        public List<string> AssignedMaterialIds { get; set; } = new List<string>();
        public List<string> AssignedEquipmentIds { get; set; } = new List<string>();
        public AreaFeatures Features { get; set; }
        public List<AreaReservation> Reservations { get; set; } = new List<AreaReservation>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class AreaDimensions
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Area { get; set; }
        public double UsableArea { get; set; }
        public double ClearHeight { get; set; }
        public string Unit { get; set; }
    }

    public class AreaCapacity
    {
        public double MaxWeight { get; set; }
        public double CurrentWeight { get; set; }
        public int MaxItems { get; set; }
        public int CurrentItems { get; set; }
        public double UtilizationPercent { get; set; }
    }

    public class AreaLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public List<Coordinate> Boundary { get; set; } = new List<Coordinate>();
        public string Zone { get; set; }
        public string AccessRoute { get; set; }
    }

    public class Coordinate
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class AreaFeatures
    {
        public bool HasCover { get; set; }
        public bool HasLighting { get; set; }
        public bool HasPower { get; set; }
        public bool HasWater { get; set; }
        public bool HasSecurity { get; set; }
        public bool IsPaved { get; set; }
        public bool IsLevelGround { get; set; }
        public double LoadBearingCapacity { get; set; }
        public List<string> AccessibleBy { get; set; } = new List<string>();
    }

    public class AreaReservation
    {
        public string ReservationId { get; set; } = Guid.NewGuid().ToString();
        public string ReservedBy { get; set; }
        public string Purpose { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double ReservedArea { get; set; }
        public string Status { get; set; }
    }

    public class TrafficRoute
    {
        public string RouteId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string PlanId { get; set; }
        public TrafficRouteType Type { get; set; }
        public TrafficPriority Priority { get; set; }
        public RouteSpecification Specification { get; set; }
        public List<Coordinate> Path { get; set; } = new List<Coordinate>();
        public List<string> ConnectedAreas { get; set; } = new List<string>();
        public List<TrafficRestriction> Restrictions { get; set; } = new List<TrafficRestriction>();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class RouteSpecification
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public double MaxVehicleWeight { get; set; }
        public double MaxVehicleHeight { get; set; }
        public double MaxVehicleWidth { get; set; }
        public double SpeedLimit { get; set; }
        public string SurfaceType { get; set; }
        public bool IsTwoWay { get; set; }
    }

    public class TrafficRestriction
    {
        public string RestrictionId { get; set; } = Guid.NewGuid().ToString();
        public string RestrictionType { get; set; }
        public string Description { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public List<int> ApplicableDays { get; set; } = new List<int>();
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public class Delivery
    {
        public string DeliveryId { get; set; } = Guid.NewGuid().ToString();
        public string DeliveryNumber { get; set; }
        public string PlanId { get; set; }
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public DeliveryPriority Priority { get; set; }
        public DeliveryStatus Status { get; set; }
        public DeliverySchedule Schedule { get; set; }
        public VehicleInfo Vehicle { get; set; }
        public List<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
        public string DestinationAreaId { get; set; }
        public string AssignedRouteId { get; set; }
        public string UnloadingEquipmentId { get; set; }
        public List<DeliveryNote> Notes { get; set; } = new List<DeliveryNote>();
        public DeliveryCheckIn CheckIn { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class DeliverySchedule
    {
        public DateTime ScheduledArrival { get; set; }
        public DateTime? ActualArrival { get; set; }
        public int EstimatedUnloadingMinutes { get; set; }
        public int ActualUnloadingMinutes { get; set; }
        public DateTime? CompletedTime { get; set; }
        public string TimeSlot { get; set; }
    }

    public class VehicleInfo
    {
        public string VehicleType { get; set; }
        public string RegistrationNumber { get; set; }
        public string DriverName { get; set; }
        public string DriverContact { get; set; }
        public double VehicleLength { get; set; }
        public double VehicleWidth { get; set; }
        public double VehicleHeight { get; set; }
        public double GrossWeight { get; set; }
    }

    public class DeliveryItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string MaterialId { get; set; }
        public string Description { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double Weight { get; set; }
        public bool RequiresInspection { get; set; }
        public string InspectionStatus { get; set; }
    }

    public class DeliveryNote
    {
        public string NoteId { get; set; } = Guid.NewGuid().ToString();
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class DeliveryCheckIn
    {
        public DateTime CheckInTime { get; set; }
        public string CheckedInBy { get; set; }
        public string GateUsed { get; set; }
        public bool DocumentsVerified { get; set; }
        public bool VehicleInspected { get; set; }
        public string Notes { get; set; }
    }

    public class WeatherForecast
    {
        public string ForecastId { get; set; } = Guid.NewGuid().ToString();
        public string PlanId { get; set; }
        public DateTime ForecastDate { get; set; }
        public WeatherCondition Condition { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double WindSpeed { get; set; }
        public string WindDirection { get; set; }
        public double PrecipitationProbability { get; set; }
        public double PrecipitationAmount { get; set; }
        public double Visibility { get; set; }
        public DateTime SunriseTime { get; set; }
        public DateTime SunsetTime { get; set; }
        public DateTime RetrievedDate { get; set; } = DateTime.UtcNow;
    }

    public class WeatherImpactAssessment
    {
        public string AssessmentId { get; set; } = Guid.NewGuid().ToString();
        public string PlanId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public WeatherForecast Forecast { get; set; }
        public WeatherImpactSeverity OverallSeverity { get; set; }
        public List<ActivityImpact> ActivityImpacts { get; set; } = new List<ActivityImpact>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<string> RequiredPrecautions { get; set; } = new List<string>();
        public bool DeliveriesAffected { get; set; }
        public bool CraneOperationsAffected { get; set; }
        public bool ConcreteWorkAffected { get; set; }
        public bool ExteriorWorkAffected { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }

    public class ActivityImpact
    {
        public string ActivityType { get; set; }
        public WeatherImpactSeverity Severity { get; set; }
        public string Impact { get; set; }
        public string Mitigation { get; set; }
        public bool CanProceed { get; set; }
    }

    public class SiteLogisticsResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ResultId { get; set; }
        public object Data { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class TrafficPlanResult
    {
        public string PlanId { get; set; }
        public List<TrafficRoute> Routes { get; set; } = new List<TrafficRoute>();
        public List<TrafficConflict> Conflicts { get; set; } = new List<TrafficConflict>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public double SafetyScore { get; set; }
    }

    public class TrafficConflict
    {
        public string ConflictId { get; set; } = Guid.NewGuid().ToString();
        public string Route1Id { get; set; }
        public string Route2Id { get; set; }
        public string ConflictType { get; set; }
        public string Location { get; set; }
        public string Severity { get; set; }
        public string Resolution { get; set; }
    }

    public class LaydownManagementResult
    {
        public string PlanId { get; set; }
        public double TotalArea { get; set; }
        public double UsedArea { get; set; }
        public double AvailableArea { get; set; }
        public double UtilizationPercent { get; set; }
        public Dictionary<LaydownAreaType, double> AreaByType { get; set; } = new Dictionary<LaydownAreaType, double>();
        public List<LaydownArea> Areas { get; set; } = new List<LaydownArea>();
        public List<string> Alerts { get; set; } = new List<string>();
    }

    #endregion

    #region Engine

    public sealed class SiteLogisticsIntelligenceEngine
    {
        private static readonly Lazy<SiteLogisticsIntelligenceEngine> _instance =
            new Lazy<SiteLogisticsIntelligenceEngine>(() => new SiteLogisticsIntelligenceEngine());

        public static SiteLogisticsIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, SiteLogisticsPlan> _plans;
        private readonly ConcurrentDictionary<string, Material> _materials;
        private readonly ConcurrentDictionary<string, Equipment> _equipment;
        private readonly ConcurrentDictionary<string, LaydownArea> _laydownAreas;
        private readonly ConcurrentDictionary<string, TrafficRoute> _trafficRoutes;
        private readonly ConcurrentDictionary<string, Delivery> _deliveries;
        private readonly ConcurrentDictionary<string, WeatherForecast> _weatherForecasts;
        private readonly object _syncLock = new object();

        private SiteLogisticsIntelligenceEngine()
        {
            _plans = new ConcurrentDictionary<string, SiteLogisticsPlan>();
            _materials = new ConcurrentDictionary<string, Material>();
            _equipment = new ConcurrentDictionary<string, Equipment>();
            _laydownAreas = new ConcurrentDictionary<string, LaydownArea>();
            _trafficRoutes = new ConcurrentDictionary<string, TrafficRoute>();
            _deliveries = new ConcurrentDictionary<string, Delivery>();
            _weatherForecasts = new ConcurrentDictionary<string, WeatherForecast>();
        }

        #region Site Logistics Plan Management

        public SiteLogisticsResult CreateSiteLogisticsPlan(SiteLogisticsPlan plan)
        {
            if (plan == null)
                return new SiteLogisticsResult { Success = false, Message = "Plan cannot be null" };

            if (string.IsNullOrEmpty(plan.PlanId))
                plan.PlanId = Guid.NewGuid().ToString();

            plan.CreatedDate = DateTime.UtcNow;
            plan.LastModifiedDate = DateTime.UtcNow;
            plan.Status = "Active";
            plan.SiteInfo ??= new SiteInfo();
            plan.Configuration ??= new PlanConfiguration();
            plan.Metrics ??= new PlanMetrics();

            if (_plans.TryAdd(plan.PlanId, plan))
            {
                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = "Site logistics plan created successfully",
                    ResultId = plan.PlanId,
                    Data = plan
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to create plan" };
        }

        public SiteLogisticsPlan GetPlan(string planId)
        {
            _plans.TryGetValue(planId, out var plan);
            return plan;
        }

        public List<SiteLogisticsPlan> GetAllPlans()
        {
            return _plans.Values.ToList();
        }

        public SiteLogisticsResult UpdatePlanMetrics(string planId)
        {
            var plan = GetPlan(planId);
            if (plan == null)
                return new SiteLogisticsResult { Success = false, Message = "Plan not found" };

            var deliveries = _deliveries.Values.Where(d => d.PlanId == planId).ToList();
            var completedDeliveries = deliveries.Where(d => d.Status == DeliveryStatus.Completed).ToList();

            plan.Metrics.TotalDeliveries = deliveries.Count;
            plan.Metrics.OnTimeDeliveries = completedDeliveries.Count(d =>
                d.Schedule?.ActualArrival <= d.Schedule?.ScheduledArrival.AddMinutes(15));
            plan.Metrics.DelayedDeliveries = completedDeliveries.Count(d =>
                d.Schedule?.ActualArrival > d.Schedule?.ScheduledArrival.AddMinutes(15));

            if (completedDeliveries.Any())
            {
                plan.Metrics.AverageUnloadingTime = completedDeliveries
                    .Average(d => d.Schedule?.ActualUnloadingMinutes ?? 0);
            }

            var areas = _laydownAreas.Values.Where(a => a.PlanId == planId).ToList();
            if (areas.Any())
            {
                plan.Metrics.StorageUtilization = areas.Average(a => a.Capacity?.UtilizationPercent ?? 0);
            }

            var equipmentList = _equipment.Values.ToList();
            if (equipmentList.Any())
            {
                var inUseCount = equipmentList.Count(e => e.Status == EquipmentStatus.InUse);
                plan.Metrics.EquipmentUtilization = (double)inUseCount / equipmentList.Count * 100;
            }

            plan.LastModifiedDate = DateTime.UtcNow;

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Plan metrics updated",
                ResultId = planId,
                Data = plan.Metrics
            };
        }

        #endregion

        #region Material Tracking

        public SiteLogisticsResult TrackMaterial(Material material)
        {
            if (material == null)
                return new SiteLogisticsResult { Success = false, Message = "Material cannot be null" };

            if (string.IsNullOrEmpty(material.MaterialId))
                material.MaterialId = Guid.NewGuid().ToString();

            material.CreatedDate = DateTime.UtcNow;
            material.Status = MaterialStatus.Ordered;
            material.Specification ??= new MaterialSpecification();
            material.Requirements ??= new MaterialRequirements();
            material.CurrentLocation ??= new MaterialLocation();
            material.TotalCost = material.Quantity * material.UnitCost;

            if (_materials.TryAdd(material.MaterialId, material))
            {
                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = "Material tracking initiated",
                    ResultId = material.MaterialId,
                    Data = material
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to track material" };
        }

        public Material GetMaterial(string materialId)
        {
            _materials.TryGetValue(materialId, out var material);
            return material;
        }

        public List<Material> GetMaterialsByStatus(MaterialStatus status)
        {
            return _materials.Values.Where(m => m.Status == status).ToList();
        }

        public SiteLogisticsResult UpdateMaterialStatus(string materialId, MaterialStatus newStatus, string location = null)
        {
            if (!_materials.TryGetValue(materialId, out var material))
                return new SiteLogisticsResult { Success = false, Message = "Material not found" };

            var previousStatus = material.Status;
            material.Status = newStatus;

            if (!string.IsNullOrEmpty(location))
            {
                var movement = new MaterialMovement
                {
                    FromLocation = material.CurrentLocation?.LocationId,
                    ToLocation = location,
                    Quantity = material.Quantity,
                    MovementDate = DateTime.UtcNow
                };
                material.MovementHistory.Add(movement);
                material.CurrentLocation = new MaterialLocation
                {
                    LocationId = location,
                    UpdatedDate = DateTime.UtcNow
                };
            }

            if (newStatus == MaterialStatus.Received && !material.ActualDeliveryDate.HasValue)
            {
                material.ActualDeliveryDate = DateTime.UtcNow;
            }

            return new SiteLogisticsResult
            {
                Success = true,
                Message = $"Material status updated from {previousStatus} to {newStatus}",
                ResultId = materialId,
                Data = material
            };
        }

        public SiteLogisticsResult MoveMaterial(string materialId, string toLocationId, double quantity, string movedBy)
        {
            if (!_materials.TryGetValue(materialId, out var material))
                return new SiteLogisticsResult { Success = false, Message = "Material not found" };

            if (quantity > material.Quantity)
                return new SiteLogisticsResult { Success = false, Message = "Insufficient quantity" };

            var movement = new MaterialMovement
            {
                FromLocation = material.CurrentLocation?.LocationId,
                ToLocation = toLocationId,
                Quantity = quantity,
                MovedBy = movedBy,
                MovementDate = DateTime.UtcNow
            };

            material.MovementHistory.Add(movement);
            material.CurrentLocation = new MaterialLocation
            {
                LocationId = toLocationId,
                UpdatedDate = DateTime.UtcNow
            };

            return new SiteLogisticsResult
            {
                Success = true,
                Message = $"Material moved to {toLocationId}",
                ResultId = movement.MovementId,
                Data = movement
            };
        }

        public List<Material> GetMaterialsRequiringDelivery(int daysAhead = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
            return _materials.Values
                .Where(m => m.Status == MaterialStatus.Ordered &&
                           m.ExpectedDeliveryDate <= cutoffDate)
                .OrderBy(m => m.ExpectedDeliveryDate)
                .ToList();
        }

        #endregion

        #region Equipment Scheduling

        public SiteLogisticsResult RegisterEquipment(Equipment equipment)
        {
            if (equipment == null)
                return new SiteLogisticsResult { Success = false, Message = "Equipment cannot be null" };

            if (string.IsNullOrEmpty(equipment.EquipmentId))
                equipment.EquipmentId = Guid.NewGuid().ToString();

            equipment.CreatedDate = DateTime.UtcNow;
            equipment.Status = EquipmentStatus.Available;
            equipment.Specification ??= new EquipmentSpecification();

            if (_equipment.TryAdd(equipment.EquipmentId, equipment))
            {
                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = "Equipment registered successfully",
                    ResultId = equipment.EquipmentId,
                    Data = equipment
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to register equipment" };
        }

        public SiteLogisticsResult ScheduleEquipment(string equipmentId, EquipmentScheduleEntry scheduleEntry)
        {
            if (!_equipment.TryGetValue(equipmentId, out var equipment))
                return new SiteLogisticsResult { Success = false, Message = "Equipment not found" };

            if (scheduleEntry == null)
                return new SiteLogisticsResult { Success = false, Message = "Schedule entry cannot be null" };

            if (string.IsNullOrEmpty(scheduleEntry.EntryId))
                scheduleEntry.EntryId = Guid.NewGuid().ToString();

            var conflicts = equipment.Schedule.Where(s =>
                s.StartTime < scheduleEntry.EndTime &&
                s.EndTime > scheduleEntry.StartTime &&
                s.Status != "Cancelled"
            ).ToList();

            if (conflicts.Any())
            {
                return new SiteLogisticsResult
                {
                    Success = false,
                    Message = "Schedule conflict detected",
                    Warnings = conflicts.Select(c => $"Conflict with {c.TaskDescription} ({c.StartTime} - {c.EndTime})").ToList()
                };
            }

            scheduleEntry.Status = "Scheduled";
            equipment.Schedule.Add(scheduleEntry);

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Equipment scheduled successfully",
                ResultId = scheduleEntry.EntryId,
                Data = scheduleEntry
            };
        }

        public List<Equipment> GetAvailableEquipment(EquipmentType? type, DateTime startTime, DateTime endTime)
        {
            return _equipment.Values.Where(e =>
            {
                if (type.HasValue && e.Type != type.Value)
                    return false;

                if (e.Status != EquipmentStatus.Available && e.Status != EquipmentStatus.Reserved)
                    return false;

                var hasConflict = e.Schedule.Any(s =>
                    s.StartTime < endTime &&
                    s.EndTime > startTime &&
                    s.Status != "Cancelled");

                return !hasConflict;
            }).ToList();
        }

        public Equipment GetEquipment(string equipmentId)
        {
            _equipment.TryGetValue(equipmentId, out var equipment);
            return equipment;
        }

        public SiteLogisticsResult UpdateEquipmentStatus(string equipmentId, EquipmentStatus status)
        {
            if (!_equipment.TryGetValue(equipmentId, out var equipment))
                return new SiteLogisticsResult { Success = false, Message = "Equipment not found" };

            var previousStatus = equipment.Status;
            equipment.Status = status;

            return new SiteLogisticsResult
            {
                Success = true,
                Message = $"Equipment status updated from {previousStatus} to {status}",
                ResultId = equipmentId,
                Data = equipment
            };
        }

        #endregion

        #region Laydown Area Management

        public SiteLogisticsResult CreateLaydownArea(LaydownArea area)
        {
            if (area == null)
                return new SiteLogisticsResult { Success = false, Message = "Area cannot be null" };

            if (string.IsNullOrEmpty(area.AreaId))
                area.AreaId = Guid.NewGuid().ToString();

            area.CreatedDate = DateTime.UtcNow;
            area.Status = LaydownAreaStatus.Available;
            area.Dimensions ??= new AreaDimensions();
            area.Capacity ??= new AreaCapacity();
            area.Location ??= new AreaLocation();
            area.Features ??= new AreaFeatures();

            if (area.Dimensions.Length > 0 && area.Dimensions.Width > 0)
            {
                area.Dimensions.Area = area.Dimensions.Length * area.Dimensions.Width;
                area.Dimensions.UsableArea = area.Dimensions.Area * 0.85;
            }

            if (_laydownAreas.TryAdd(area.AreaId, area))
            {
                var plan = GetPlan(area.PlanId);
                if (plan != null && !plan.LaydownAreaIds.Contains(area.AreaId))
                {
                    plan.LaydownAreaIds.Add(area.AreaId);
                }

                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = "Laydown area created successfully",
                    ResultId = area.AreaId,
                    Data = area
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to create laydown area" };
        }

        public LaydownManagementResult ManageLaydownAreas(string planId)
        {
            var plan = GetPlan(planId);
            if (plan == null) return null;

            var areas = _laydownAreas.Values.Where(a => a.PlanId == planId).ToList();

            var result = new LaydownManagementResult
            {
                PlanId = planId,
                Areas = areas,
                TotalArea = areas.Sum(a => a.Dimensions?.Area ?? 0),
                UsedArea = areas.Where(a => a.Status != LaydownAreaStatus.Available)
                    .Sum(a => a.Dimensions?.UsableArea ?? 0),
            };

            result.AvailableArea = result.TotalArea - result.UsedArea;
            result.UtilizationPercent = result.TotalArea > 0
                ? result.UsedArea / result.TotalArea * 100
                : 0;

            foreach (var areaType in Enum.GetValues<LaydownAreaType>())
            {
                result.AreaByType[areaType] = areas
                    .Where(a => a.Type == areaType)
                    .Sum(a => a.Dimensions?.Area ?? 0);
            }

            if (result.UtilizationPercent > 85)
            {
                result.Alerts.Add("Storage utilization exceeds 85% - consider additional laydown areas");
            }

            var fullAreas = areas.Where(a => a.Status == LaydownAreaStatus.FullyOccupied).ToList();
            if (fullAreas.Any())
            {
                result.Alerts.Add($"{fullAreas.Count} laydown area(s) at full capacity");
            }

            return result;
        }

        public SiteLogisticsResult AssignMaterialToArea(string materialId, string areaId)
        {
            if (!_materials.TryGetValue(materialId, out var material))
                return new SiteLogisticsResult { Success = false, Message = "Material not found" };

            if (!_laydownAreas.TryGetValue(areaId, out var area))
                return new SiteLogisticsResult { Success = false, Message = "Laydown area not found" };

            if (!area.AssignedMaterialIds.Contains(materialId))
            {
                area.AssignedMaterialIds.Add(materialId);
            }

            material.CurrentLocation = new MaterialLocation
            {
                LocationId = areaId,
                AreaId = areaId,
                LocationType = "LaydownArea",
                UpdatedDate = DateTime.UtcNow
            };

            material.Status = MaterialStatus.InStorage;

            UpdateAreaCapacity(area);

            return new SiteLogisticsResult
            {
                Success = true,
                Message = $"Material assigned to {area.Name}",
                ResultId = areaId,
                Data = new { Material = material, Area = area }
            };
        }

        public SiteLogisticsResult ReserveLaydownArea(string areaId, AreaReservation reservation)
        {
            if (!_laydownAreas.TryGetValue(areaId, out var area))
                return new SiteLogisticsResult { Success = false, Message = "Area not found" };

            if (reservation == null)
                return new SiteLogisticsResult { Success = false, Message = "Reservation cannot be null" };

            if (string.IsNullOrEmpty(reservation.ReservationId))
                reservation.ReservationId = Guid.NewGuid().ToString();

            reservation.Status = "Active";
            area.Reservations.Add(reservation);

            if (area.Status == LaydownAreaStatus.Available)
            {
                area.Status = LaydownAreaStatus.Reserved;
            }

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Area reserved successfully",
                ResultId = reservation.ReservationId,
                Data = reservation
            };
        }

        private void UpdateAreaCapacity(LaydownArea area)
        {
            area.Capacity.CurrentItems = area.AssignedMaterialIds.Count + area.AssignedEquipmentIds.Count;

            double totalWeight = 0;
            foreach (var materialId in area.AssignedMaterialIds)
            {
                if (_materials.TryGetValue(materialId, out var material))
                {
                    totalWeight += material.Specification?.Weight ?? 0;
                }
            }
            area.Capacity.CurrentWeight = totalWeight;

            if (area.Capacity.MaxItems > 0)
            {
                area.Capacity.UtilizationPercent = (double)area.Capacity.CurrentItems / area.Capacity.MaxItems * 100;
            }

            if (area.Capacity.UtilizationPercent >= 100)
            {
                area.Status = LaydownAreaStatus.FullyOccupied;
            }
            else if (area.Capacity.UtilizationPercent > 0)
            {
                area.Status = LaydownAreaStatus.PartiallyOccupied;
            }
            else
            {
                area.Status = LaydownAreaStatus.Available;
            }
        }

        #endregion

        #region Traffic Planning

        public SiteLogisticsResult CreateTrafficRoute(TrafficRoute route)
        {
            if (route == null)
                return new SiteLogisticsResult { Success = false, Message = "Route cannot be null" };

            if (string.IsNullOrEmpty(route.RouteId))
                route.RouteId = Guid.NewGuid().ToString();

            route.CreatedDate = DateTime.UtcNow;
            route.IsActive = true;
            route.Specification ??= new RouteSpecification();

            if (_trafficRoutes.TryAdd(route.RouteId, route))
            {
                var plan = GetPlan(route.PlanId);
                if (plan != null && !plan.TrafficRouteIds.Contains(route.RouteId))
                {
                    plan.TrafficRouteIds.Add(route.RouteId);
                }

                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = "Traffic route created successfully",
                    ResultId = route.RouteId,
                    Data = route
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to create route" };
        }

        public TrafficPlanResult PlanTraffic(string planId)
        {
            var plan = GetPlan(planId);
            if (plan == null) return null;

            var routes = _trafficRoutes.Values.Where(r => r.PlanId == planId && r.IsActive).ToList();

            var result = new TrafficPlanResult
            {
                PlanId = planId,
                Routes = routes
            };

            for (int i = 0; i < routes.Count; i++)
            {
                for (int j = i + 1; j < routes.Count; j++)
                {
                    var conflict = DetectRouteConflict(routes[i], routes[j]);
                    if (conflict != null)
                    {
                        result.Conflicts.Add(conflict);
                    }
                }
            }

            if (!routes.Any(r => r.Type == TrafficRouteType.EmergencyAccess))
            {
                result.Recommendations.Add("Consider adding dedicated emergency access route");
            }

            if (!routes.Any(r => r.Type == TrafficRouteType.PedestrianAccess))
            {
                result.Recommendations.Add("Pedestrian routes should be clearly defined and separated from vehicle traffic");
            }

            var heavyHaulRoutes = routes.Where(r => r.Type == TrafficRouteType.HeavyHaul).ToList();
            if (heavyHaulRoutes.Any())
            {
                result.Recommendations.Add("Ensure heavy haul routes have adequate ground bearing capacity");
            }

            result.SafetyScore = CalculateTrafficSafetyScore(routes, result.Conflicts);

            return result;
        }

        private TrafficConflict DetectRouteConflict(TrafficRoute route1, TrafficRoute route2)
        {
            if (route1.Path.Count < 2 || route2.Path.Count < 2)
                return null;

            for (int i = 0; i < route1.Path.Count - 1; i++)
            {
                for (int j = 0; j < route2.Path.Count - 1; j++)
                {
                    if (DoSegmentsIntersect(route1.Path[i], route1.Path[i + 1], route2.Path[j], route2.Path[j + 1]))
                    {
                        return new TrafficConflict
                        {
                            Route1Id = route1.RouteId,
                            Route2Id = route2.RouteId,
                            ConflictType = "Intersection",
                            Location = $"Near ({route1.Path[i].X:F1}, {route1.Path[i].Y:F1})",
                            Severity = DetermineConflictSeverity(route1, route2),
                            Resolution = GenerateConflictResolution(route1, route2)
                        };
                    }
                }
            }

            return null;
        }

        private bool DoSegmentsIntersect(Coordinate p1, Coordinate q1, Coordinate p2, Coordinate q2)
        {
            double d1 = Direction(p2, q2, p1);
            double d2 = Direction(p2, q2, q1);
            double d3 = Direction(p1, q1, p2);
            double d4 = Direction(p1, q1, q2);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            return false;
        }

        private double Direction(Coordinate pi, Coordinate pj, Coordinate pk)
        {
            return (pk.X - pi.X) * (pj.Y - pi.Y) - (pj.X - pi.X) * (pk.Y - pi.Y);
        }

        private string DetermineConflictSeverity(TrafficRoute route1, TrafficRoute route2)
        {
            if (route1.Type == TrafficRouteType.EmergencyAccess || route2.Type == TrafficRouteType.EmergencyAccess)
                return "Critical";

            if (route1.Type == TrafficRouteType.HeavyHaul || route2.Type == TrafficRouteType.HeavyHaul)
                return "High";

            if (route1.Type == TrafficRouteType.PedestrianAccess || route2.Type == TrafficRouteType.PedestrianAccess)
                return "High";

            return "Medium";
        }

        private string GenerateConflictResolution(TrafficRoute route1, TrafficRoute route2)
        {
            if (route1.Type == TrafficRouteType.PedestrianAccess || route2.Type == TrafficRouteType.PedestrianAccess)
                return "Install pedestrian crossing with warning signs and signals";

            if (route1.Priority != route2.Priority)
                return $"Implement priority signage for {(route1.Priority < route2.Priority ? route1.Name : route2.Name)}";

            return "Consider traffic signals, roundabout, or time-based scheduling";
        }

        private double CalculateTrafficSafetyScore(List<TrafficRoute> routes, List<TrafficConflict> conflicts)
        {
            if (!routes.Any()) return 0;

            double score = 100;

            foreach (var conflict in conflicts)
            {
                switch (conflict.Severity)
                {
                    case "Critical":
                        score -= 20;
                        break;
                    case "High":
                        score -= 10;
                        break;
                    case "Medium":
                        score -= 5;
                        break;
                    default:
                        score -= 2;
                        break;
                }
            }

            if (routes.Any(r => r.Type == TrafficRouteType.EmergencyAccess))
                score += 5;
            if (routes.Any(r => r.Type == TrafficRouteType.PedestrianAccess))
                score += 5;

            return Math.Max(0, Math.Min(100, score));
        }

        #endregion

        #region Weather Impact Assessment

        public SiteLogisticsResult AddWeatherForecast(WeatherForecast forecast)
        {
            if (forecast == null)
                return new SiteLogisticsResult { Success = false, Message = "Forecast cannot be null" };

            if (string.IsNullOrEmpty(forecast.ForecastId))
                forecast.ForecastId = Guid.NewGuid().ToString();

            forecast.RetrievedDate = DateTime.UtcNow;

            _weatherForecasts.AddOrUpdate(forecast.ForecastId, forecast, (k, v) => forecast);

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Weather forecast added",
                ResultId = forecast.ForecastId,
                Data = forecast
            };
        }

        public WeatherImpactAssessment AssessWeatherImpact(string planId, WeatherForecast forecast)
        {
            var plan = GetPlan(planId);
            if (plan == null || forecast == null) return null;

            var assessment = new WeatherImpactAssessment
            {
                PlanId = planId,
                AssessmentDate = forecast.ForecastDate,
                Forecast = forecast
            };

            assessment.ActivityImpacts = new List<ActivityImpact>
            {
                AssessActivityImpact("Crane Operations", forecast),
                AssessActivityImpact("Concrete Work", forecast),
                AssessActivityImpact("Exterior Work", forecast),
                AssessActivityImpact("Deliveries", forecast),
                AssessActivityImpact("Excavation", forecast),
                AssessActivityImpact("Roofing", forecast),
                AssessActivityImpact("Steel Erection", forecast),
                AssessActivityImpact("Painting", forecast)
            };

            assessment.OverallSeverity = assessment.ActivityImpacts.Max(a => a.Severity);
            assessment.DeliveriesAffected = assessment.ActivityImpacts.Any(a => a.ActivityType == "Deliveries" && !a.CanProceed);
            assessment.CraneOperationsAffected = assessment.ActivityImpacts.Any(a => a.ActivityType == "Crane Operations" && !a.CanProceed);
            assessment.ConcreteWorkAffected = assessment.ActivityImpacts.Any(a => a.ActivityType == "Concrete Work" && !a.CanProceed);
            assessment.ExteriorWorkAffected = assessment.ActivityImpacts.Any(a => a.ActivityType == "Exterior Work" && !a.CanProceed);

            assessment.Recommendations = GenerateWeatherRecommendations(forecast, assessment);
            assessment.RequiredPrecautions = GenerateWeatherPrecautions(forecast);

            return assessment;
        }

        private ActivityImpact AssessActivityImpact(string activityType, WeatherForecast forecast)
        {
            var impact = new ActivityImpact
            {
                ActivityType = activityType,
                CanProceed = true,
                Severity = WeatherImpactSeverity.None
            };

            switch (activityType)
            {
                case "Crane Operations":
                    if (forecast.WindSpeed > 35)
                    {
                        impact.Severity = WeatherImpactSeverity.WorkStoppage;
                        impact.CanProceed = false;
                        impact.Impact = "Wind speed exceeds safe crane operation limits";
                        impact.Mitigation = "Suspend crane operations until wind subsides";
                    }
                    else if (forecast.WindSpeed > 25)
                    {
                        impact.Severity = WeatherImpactSeverity.Significant;
                        impact.Impact = "High wind may affect crane stability";
                        impact.Mitigation = "Reduce load capacity and implement additional safety measures";
                    }
                    break;

                case "Concrete Work":
                    if (forecast.Condition == WeatherCondition.Rain || forecast.Condition == WeatherCondition.HeavyRain)
                    {
                        impact.Severity = WeatherImpactSeverity.WorkStoppage;
                        impact.CanProceed = false;
                        impact.Impact = "Rain will affect concrete curing and finish quality";
                        impact.Mitigation = "Postpone concrete pours or provide adequate cover";
                    }
                    else if (forecast.Temperature < 5 || forecast.Temperature > 35)
                    {
                        impact.Severity = WeatherImpactSeverity.Significant;
                        impact.Impact = "Extreme temperature affects concrete setting";
                        impact.Mitigation = "Use temperature-adjusted mix design and curing methods";
                    }
                    break;

                case "Exterior Work":
                    if (forecast.Condition == WeatherCondition.Storm || forecast.Condition == WeatherCondition.HighWind)
                    {
                        impact.Severity = WeatherImpactSeverity.WorkStoppage;
                        impact.CanProceed = false;
                        impact.Impact = "Unsafe conditions for exterior work";
                        impact.Mitigation = "Suspend exterior activities and secure materials";
                    }
                    else if (forecast.Condition == WeatherCondition.Rain)
                    {
                        impact.Severity = WeatherImpactSeverity.Moderate;
                        impact.Impact = "Rain affects exterior work productivity";
                        impact.Mitigation = "Provide weather protection or reschedule";
                    }
                    break;

                case "Deliveries":
                    if (forecast.Visibility < 100)
                    {
                        impact.Severity = WeatherImpactSeverity.Significant;
                        impact.Impact = "Low visibility affects safe delivery operations";
                        impact.Mitigation = "Delay deliveries until visibility improves";
                    }
                    else if (forecast.Condition == WeatherCondition.Snow || forecast.Condition == WeatherCondition.HeavySnow)
                    {
                        impact.Severity = WeatherImpactSeverity.Moderate;
                        impact.Impact = "Snow may affect road conditions and unloading";
                        impact.Mitigation = "Clear access routes and unloading areas";
                    }
                    break;

                case "Excavation":
                    if (forecast.PrecipitationAmount > 25)
                    {
                        impact.Severity = WeatherImpactSeverity.Significant;
                        impact.CanProceed = false;
                        impact.Impact = "Heavy rain causes groundwater and stability issues";
                        impact.Mitigation = "Install dewatering and shore excavations";
                    }
                    break;

                case "Roofing":
                    if (forecast.Condition == WeatherCondition.Rain || forecast.WindSpeed > 25)
                    {
                        impact.Severity = WeatherImpactSeverity.WorkStoppage;
                        impact.CanProceed = false;
                        impact.Impact = "Rain or high wind prevents safe roofing work";
                        impact.Mitigation = "Secure partially completed areas and reschedule";
                    }
                    break;

                case "Steel Erection":
                    if (forecast.WindSpeed > 30)
                    {
                        impact.Severity = WeatherImpactSeverity.WorkStoppage;
                        impact.CanProceed = false;
                        impact.Impact = "High wind prevents safe steel erection";
                        impact.Mitigation = "Secure all materials and suspend operations";
                    }
                    break;

                case "Painting":
                    if (forecast.Humidity > 85 || forecast.Temperature < 10 || forecast.Temperature > 35)
                    {
                        impact.Severity = WeatherImpactSeverity.Significant;
                        impact.Impact = "Humidity or temperature outside optimal range";
                        impact.Mitigation = "Use appropriate paint formulations or reschedule";
                    }
                    break;
            }

            return impact;
        }

        private List<string> GenerateWeatherRecommendations(WeatherForecast forecast, WeatherImpactAssessment assessment)
        {
            var recommendations = new List<string>();

            if (assessment.OverallSeverity >= WeatherImpactSeverity.Significant)
            {
                recommendations.Add("Review and update daily work plan based on weather conditions");
                recommendations.Add("Conduct pre-shift safety briefing addressing weather hazards");
            }

            if (forecast.WindSpeed > 20)
            {
                recommendations.Add("Secure all loose materials and temporary structures");
            }

            if (forecast.PrecipitationProbability > 50)
            {
                recommendations.Add("Prepare weather protection for sensitive materials and work areas");
                recommendations.Add("Check site drainage and dewatering systems");
            }

            if (forecast.Temperature > 30)
            {
                recommendations.Add("Implement heat stress prevention measures");
                recommendations.Add("Ensure adequate hydration stations are available");
            }

            if (forecast.Temperature < 5)
            {
                recommendations.Add("Implement cold weather protection for workers and materials");
            }

            if (assessment.DeliveriesAffected)
            {
                recommendations.Add("Contact suppliers to reschedule affected deliveries");
            }

            return recommendations;
        }

        private List<string> GenerateWeatherPrecautions(WeatherForecast forecast)
        {
            var precautions = new List<string>();

            if (forecast.WindSpeed > 15)
            {
                precautions.Add("Secure scaffolding and temporary structures");
                precautions.Add("Remove or secure loose debris and materials");
            }

            if (forecast.Condition == WeatherCondition.Rain || forecast.Condition == WeatherCondition.HeavyRain)
            {
                precautions.Add("Cover exposed steel and electrical work");
                precautions.Add("Ensure slip-resistant surfaces on walkways");
            }

            if (forecast.Condition == WeatherCondition.Storm)
            {
                precautions.Add("Establish emergency shelter locations");
                precautions.Add("Review emergency evacuation procedures");
            }

            if (forecast.Visibility < 200)
            {
                precautions.Add("Enhance lighting and visibility markers");
                precautions.Add("Reduce vehicle speeds on site");
            }

            return precautions;
        }

        #endregion

        #region Delivery Management

        public SiteLogisticsResult ScheduleDelivery(Delivery delivery)
        {
            if (delivery == null)
                return new SiteLogisticsResult { Success = false, Message = "Delivery cannot be null" };

            if (string.IsNullOrEmpty(delivery.DeliveryId))
                delivery.DeliveryId = Guid.NewGuid().ToString();

            delivery.CreatedDate = DateTime.UtcNow;
            delivery.Status = DeliveryStatus.Scheduled;
            delivery.Schedule ??= new DeliverySchedule();
            delivery.Vehicle ??= new VehicleInfo();
            delivery.DeliveryNumber = $"DEL-{DateTime.UtcNow:yyyyMMdd}-{_deliveries.Count + 1:D4}";

            var plan = GetPlan(delivery.PlanId);
            if (plan != null)
            {
                var existingDeliveries = _deliveries.Values
                    .Where(d => d.PlanId == delivery.PlanId &&
                               d.Status != DeliveryStatus.Completed &&
                               d.Status != DeliveryStatus.Cancelled)
                    .ToList();

                var sameTimeDeliveries = existingDeliveries.Where(d =>
                    Math.Abs((d.Schedule?.ScheduledArrival - delivery.Schedule?.ScheduledArrival)?.TotalMinutes ?? 0) < plan.Configuration.DeliveryBufferMinutes
                ).ToList();

                if (sameTimeDeliveries.Count >= plan.Configuration.MaxSimultaneousDeliveries)
                {
                    return new SiteLogisticsResult
                    {
                        Success = false,
                        Message = "Maximum simultaneous deliveries exceeded for this time slot",
                        Warnings = new List<string> { $"Consider scheduling after {sameTimeDeliveries.Max(d => d.Schedule?.ScheduledArrival.AddMinutes(plan.Configuration.DeliveryBufferMinutes))}" }
                    };
                }
            }

            if (_deliveries.TryAdd(delivery.DeliveryId, delivery))
            {
                return new SiteLogisticsResult
                {
                    Success = true,
                    Message = $"Delivery {delivery.DeliveryNumber} scheduled successfully",
                    ResultId = delivery.DeliveryId,
                    Data = delivery
                };
            }

            return new SiteLogisticsResult { Success = false, Message = "Failed to schedule delivery" };
        }

        public SiteLogisticsResult CheckInDelivery(string deliveryId, DeliveryCheckIn checkIn)
        {
            if (!_deliveries.TryGetValue(deliveryId, out var delivery))
                return new SiteLogisticsResult { Success = false, Message = "Delivery not found" };

            delivery.CheckIn = checkIn;
            delivery.CheckIn.CheckInTime = DateTime.UtcNow;
            delivery.Status = DeliveryStatus.Arrived;
            delivery.Schedule.ActualArrival = DateTime.UtcNow;

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Delivery checked in",
                ResultId = deliveryId,
                Data = delivery
            };
        }

        public SiteLogisticsResult CompleteDelivery(string deliveryId, int actualUnloadingMinutes)
        {
            if (!_deliveries.TryGetValue(deliveryId, out var delivery))
                return new SiteLogisticsResult { Success = false, Message = "Delivery not found" };

            delivery.Status = DeliveryStatus.Completed;
            delivery.Schedule.ActualUnloadingMinutes = actualUnloadingMinutes;
            delivery.Schedule.CompletedTime = DateTime.UtcNow;

            foreach (var item in delivery.Items)
            {
                if (!string.IsNullOrEmpty(item.MaterialId))
                {
                    UpdateMaterialStatus(item.MaterialId, MaterialStatus.Received, delivery.DestinationAreaId);
                }
            }

            return new SiteLogisticsResult
            {
                Success = true,
                Message = "Delivery completed",
                ResultId = deliveryId,
                Data = delivery
            };
        }

        public List<Delivery> GetUpcomingDeliveries(string planId, int hoursAhead = 24)
        {
            var cutoff = DateTime.UtcNow.AddHours(hoursAhead);
            return _deliveries.Values
                .Where(d => d.PlanId == planId &&
                           d.Status == DeliveryStatus.Scheduled &&
                           d.Schedule?.ScheduledArrival >= DateTime.UtcNow &&
                           d.Schedule?.ScheduledArrival <= cutoff)
                .OrderBy(d => d.Schedule?.ScheduledArrival)
                .ToList();
        }

        #endregion

        #region Utility Methods

        public void ClearAllData()
        {
            lock (_syncLock)
            {
                _plans.Clear();
                _materials.Clear();
                _equipment.Clear();
                _laydownAreas.Clear();
                _trafficRoutes.Clear();
                _deliveries.Clear();
                _weatherForecasts.Clear();
            }
        }

        #endregion
    }

    #endregion
}
