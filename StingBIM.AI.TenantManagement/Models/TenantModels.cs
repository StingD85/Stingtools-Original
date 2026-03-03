// ============================================================================
// StingBIM AI - Tenant & Space Management Models
// Domain models for tenant management, lease tracking, space allocation,
// rent roll generation, service charges, and stacking plan visualization.
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StingBIM.AI.TenantManagement.Models
{
    #region Enumerations

    /// <summary>
    /// Classification of leasable space types within a building.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SpaceType
    {
        Office,
        Retail,
        Industrial,
        Parking,
        Storage,
        Common,
        MeetingRoom,
        ServerRoom,
        Reception,
        FoodCourt,
        Medical,
        Gym,
        Residential,
        Rooftop,
        Basement
    }

    /// <summary>
    /// Current lifecycle status of a lease agreement.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LeaseStatus
    {
        Active,
        Expired,
        Pending,
        Terminated,
        InNegotiation,
        Holdover,
        Surrendered
    }

    /// <summary>
    /// Rent escalation methodology applied to a lease.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EscalationType
    {
        Fixed,
        CPI,
        Market,
        SteppedFixed,
        OpenMarketReview,
        IndexLinked,
        Hybrid
    }

    /// <summary>
    /// Current occupancy status of a space unit.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OccupancyStatus
    {
        Occupied,
        Vacant,
        UnderFitOut,
        UnderRefurbishment,
        Reserved,
        Sublet,
        Unavailable
    }

    /// <summary>
    /// Types of events that occur during a lease lifecycle.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LeaseEventType
    {
        Renewal,
        Expiry,
        BreakNotice,
        RentReview,
        Termination,
        Commencement,
        RentFreeEnd,
        EscalationDate,
        InsuranceRenewal,
        DepositReview
    }

    /// <summary>
    /// Status of a tenant move request.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MoveRequestStatus
    {
        Requested,
        UnderReview,
        Approved,
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Rejected
    }

    /// <summary>
    /// Tenant organization size classification.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TenantSize
    {
        Small,
        Medium,
        Large,
        Enterprise
    }

    /// <summary>
    /// Tenant credit rating for risk assessment.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CreditRating
    {
        AAA,
        AA,
        A,
        BBB,
        BB,
        B,
        CCC,
        Unrated
    }

    /// <summary>
    /// Cost apportionment method for service charges.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ApportionmentMethod
    {
        FloorArea,
        WeightedArea,
        EqualShare,
        Metered,
        Fixed,
        Headcount,
        Custom
    }

    /// <summary>
    /// Supported currencies for multi-currency lease operations.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Currency
    {
        UGX,
        KES,
        ZAR,
        USD,
        GBP,
        EUR,
        TZS,
        RWF,
        NGN
    }

    #endregion

    #region Core Models

    /// <summary>
    /// Represents a tenant (lessee) occupying one or more spaces in a managed building.
    /// </summary>
    public class Tenant
    {
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TradingName { get; set; } = string.Empty;
        public ContactInfo ContactInfo { get; set; } = new();
        public List<string> LeaseIds { get; set; } = new();
        public string Industry { get; set; } = string.Empty;
        public string IndustrySector { get; set; } = string.Empty;
        public TenantSize Size { get; set; } = TenantSize.Small;
        public CreditRating CreditRating { get; set; } = CreditRating.Unrated;
        public string TaxId { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public DateTime OnboardingDate { get; set; }
        public bool IsActive { get; set; } = true;
        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Calculates the total monthly rent obligation across all active leases.
        /// </summary>
        public decimal TotalMonthlyRent(IEnumerable<Lease> leases)
        {
            return leases
                .Where(l => LeaseIds.Contains(l.LeaseId) && l.Status == LeaseStatus.Active)
                .Sum(l => l.MonthlyRent);
        }
    }

    /// <summary>
    /// Contact information for tenant correspondence.
    /// </summary>
    public class ContactInfo
    {
        public string PrimaryContact { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string AlternatePhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string EmergencyContact { get; set; } = string.Empty;
        public string EmergencyPhone { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lease agreement binding a tenant to one or more spaces for a defined term.
    /// Supports multi-currency, escalation schedules, break clauses, and renewal options.
    /// </summary>
    public class Lease
    {
        public string LeaseId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public List<string> SpaceIds { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public Currency Currency { get; set; } = Currency.USD;
        public decimal EscalationRate { get; set; }
        public EscalationType EscalationType { get; set; } = EscalationType.Fixed;
        public BreakClause? BreakClause { get; set; }
        public RenewalOption? RenewalOption { get; set; }
        public LeaseStatus Status { get; set; } = LeaseStatus.Pending;
        public decimal SecurityDeposit { get; set; }
        public int RentFreePeriodMonths { get; set; }
        public DateTime? RentFreeEndDate { get; set; }
        public decimal FitOutContribution { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> CustomTerms { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Total annual rent based on current monthly amount.
        /// </summary>
        public decimal AnnualRent => MonthlyRent * 12;

        /// <summary>
        /// Remaining term in months from the current date.
        /// </summary>
        public int RemainingMonths => Math.Max(0, (int)((EndDate - DateTime.UtcNow).TotalDays / 30.44));

        /// <summary>
        /// Total lease term in months.
        /// </summary>
        public int TotalTermMonths => (int)((EndDate - StartDate).TotalDays / 30.44);

        /// <summary>
        /// Whether the lease is currently within its active term.
        /// </summary>
        public bool IsWithinTerm => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
    }

    /// <summary>
    /// Break clause allowing early termination of a lease under specified conditions.
    /// </summary>
    public class BreakClause
    {
        public DateTime BreakDate { get; set; }
        public int NoticePeriodMonths { get; set; } = 6;
        public decimal? PenaltyAmount { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public bool MutualBreak { get; set; }

        /// <summary>
        /// Deadline by which break notice must be served.
        /// </summary>
        public DateTime NoticeDeadline => BreakDate.AddMonths(-NoticePeriodMonths);
    }

    /// <summary>
    /// Renewal option embedded within a lease agreement.
    /// </summary>
    public class RenewalOption
    {
        public int RenewalTermMonths { get; set; } = 12;
        public decimal? ProposedRent { get; set; }
        public EscalationType? ProposedEscalationType { get; set; }
        public int NoticeRequiredMonths { get; set; } = 3;
        public int MaxRenewals { get; set; } = 1;
        public int RenewalsExercised { get; set; }
        public bool IsAutomatic { get; set; }
    }

    /// <summary>
    /// Represents a leasable or occupiable space unit within a building,
    /// linked to Revit room elements for BIM integration.
    /// </summary>
    public class Space
    {
        public string SpaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<int> RoomIds { get; set; } = new();
        public string BuildingId { get; set; } = string.Empty;
        public int FloorLevel { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public double Area_sqm { get; set; }
        public double UsableArea_sqm { get; set; }
        public double CommonAreaFactor { get; set; } = 1.0;
        public SpaceType SpaceType { get; set; } = SpaceType.Office;
        public OccupancyStatus OccupancyStatus { get; set; } = OccupancyStatus.Vacant;
        public string CurrentTenantId { get; set; } = string.Empty;
        public string CurrentLeaseId { get; set; } = string.Empty;
        public int MaxOccupancy { get; set; }
        public bool IsSubdivisible { get; set; }
        public double MinSubdivisionArea_sqm { get; set; }
        public decimal MarketRentPerSqm { get; set; }
        public string Amenities { get; set; } = string.Empty;
        public string Condition { get; set; } = "Good";
        public DateTime? LastRefurbishmentDate { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Rentable area considering common area factor.
        /// </summary>
        public double RentableArea_sqm => UsableArea_sqm * CommonAreaFactor;

        /// <summary>
        /// Current estimated annual market rent for this space.
        /// </summary>
        public decimal EstimatedAnnualRent => MarketRentPerSqm * (decimal)RentableArea_sqm * 12;
    }

    #endregion

    #region Event and Financial Models

    /// <summary>
    /// A scheduled or triggered event within a lease lifecycle.
    /// </summary>
    public class LeaseEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        public string LeaseId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public LeaseEventType Type { get; set; }
        public DateTime EventDate { get; set; }
        public bool Processed { get; set; }
        public string Description { get; set; } = string.Empty;
        public int AlertDaysBefore { get; set; } = 90;
        public DateTime? ProcessedDate { get; set; }
        public string ProcessedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Whether the event is upcoming within its alert window.
        /// </summary>
        public bool IsUpcoming => !Processed && EventDate > DateTime.UtcNow
            && EventDate <= DateTime.UtcNow.AddDays(AlertDaysBefore);

        /// <summary>
        /// Whether the event is overdue (past date and unprocessed).
        /// </summary>
        public bool IsOverdue => !Processed && EventDate < DateTime.UtcNow;
    }

    /// <summary>
    /// Complete rent roll for a given period, summarizing all tenant rental income.
    /// </summary>
    public class RentRoll
    {
        public string Period { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public string BuildingId { get; set; } = string.Empty;
        public List<RentRollEntry> Entries { get; set; } = new();
        public decimal TotalGrossRent { get; set; }
        public decimal TotalNetRent { get; set; }
        public decimal VacancyLoss { get; set; }
        public decimal EffectiveGrossIncome { get; set; }
        public double TotalLeasedArea_sqm { get; set; }
        public double TotalVacantArea_sqm { get; set; }
        public double OccupancyRate { get; set; }
        public Currency Currency { get; set; } = Currency.USD;

        /// <summary>
        /// Vacancy rate as a percentage.
        /// </summary>
        public double VacancyRate => TotalLeasedArea_sqm + TotalVacantArea_sqm > 0
            ? TotalVacantArea_sqm / (TotalLeasedArea_sqm + TotalVacantArea_sqm) * 100.0
            : 100.0;
    }

    /// <summary>
    /// Individual line item in a rent roll report.
    /// </summary>
    public class RentRollEntry
    {
        public string TenantName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public string SpaceDescription { get; set; } = string.Empty;
        public List<string> SpaceIds { get; set; } = new();
        public int FloorLevel { get; set; }
        public double Area_sqm { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal AnnualRent { get; set; }
        public decimal RentPerSqm { get; set; }
        public DateTime LeaseStart { get; set; }
        public DateTime LeaseExpiry { get; set; }
        public int RemainingMonths { get; set; }
        public LeaseStatus LeaseStatus { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalOccupancyCost { get; set; }
    }

    /// <summary>
    /// Space utilization metrics for a specific space over a date range.
    /// </summary>
    public class SpaceUtilization
    {
        public string SpaceId { get; set; } = string.Empty;
        public string SpaceName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double OccupancyHours { get; set; }
        public double AvailableHours { get; set; }
        public int PeakOccupancy { get; set; }
        public double AvgOccupancy { get; set; }
        public double UtilizationPct { get; set; }
        public double DeskUtilizationPct { get; set; }
        public int TotalDesks { get; set; }
        public int OccupiedDesks { get; set; }
        public Dictionary<string, double> HourlyDistribution { get; set; } = new();

        /// <summary>
        /// Effective utilization combining space and desk metrics.
        /// </summary>
        public double EffectiveUtilization =>
            (UtilizationPct * 0.6) + (DeskUtilizationPct * 0.4);
    }

    /// <summary>
    /// Tenant relocation request with status tracking and cost estimation.
    /// </summary>
    public class MoveRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public List<string> FromSpaceIds { get; set; } = new();
        public List<string> ToSpaceIds { get; set; } = new();
        public DateTime RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public MoveRequestStatus Status { get; set; } = MoveRequestStatus.Requested;
        public decimal EstimatedCost { get; set; }
        public decimal ActualCost { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int EstimatedDurationDays { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public List<MovePhase> Phases { get; set; } = new();
        public string ApprovedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// A phase within a multi-step tenant move operation.
    /// </summary>
    public class MovePhase
    {
        public int PhaseNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public bool IsComplete { get; set; }
        public List<string> AffectedSpaceIds { get; set; } = new();
    }

    #endregion

    #region Service Charge Models

    /// <summary>
    /// Service charge assessment for a tenant covering shared building operating costs.
    /// </summary>
    public class ServiceCharge
    {
        public string ChargeId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        public string TenantId { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public Currency Currency { get; set; } = Currency.USD;
        public ApportionmentMethod ApportionmentMethod { get; set; } = ApportionmentMethod.FloorArea;
        public double TenantAreaProportion { get; set; }
        public ServiceChargeBreakdown Breakdown { get; set; } = new();
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsReconciled { get; set; }
        public decimal ReconciliationAdjustment { get; set; }
    }

    /// <summary>
    /// Itemized breakdown of service charge cost categories.
    /// </summary>
    public class ServiceChargeBreakdown
    {
        public decimal ManagementFees { get; set; }
        public decimal Insurance { get; set; }
        public decimal RepairsAndMaintenance { get; set; }
        public decimal Utilities { get; set; }
        public decimal Cleaning { get; set; }
        public decimal Security { get; set; }
        public decimal Landscaping { get; set; }
        public decimal CommonAreaMaintenance { get; set; }
        public decimal WasteManagement { get; set; }
        public decimal FireSafety { get; set; }
        public decimal LiftMaintenance { get; set; }
        public decimal Sinking { get; set; }
        public Dictionary<string, decimal> OtherCosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Total of all itemized cost categories.
        /// </summary>
        public decimal Total => ManagementFees + Insurance + RepairsAndMaintenance
            + Utilities + Cleaning + Security + Landscaping + CommonAreaMaintenance
            + WasteManagement + FireSafety + LiftMaintenance + Sinking
            + OtherCosts.Values.Sum();
    }

    #endregion

    #region Stacking Plan Models

    /// <summary>
    /// Stacking plan providing a floor-by-floor visualization of tenancies.
    /// </summary>
    public class StackingPlan
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public List<StackingFloor> Floors { get; set; } = new();
        public double TotalBuildingArea_sqm { get; set; }
        public double TotalLeasedArea_sqm { get; set; }
        public double TotalVacantArea_sqm { get; set; }
        public double OccupancyRate { get; set; }
        public int TotalTenants { get; set; }
        public decimal TotalAnnualRent { get; set; }
        public decimal AverageRentPerSqm { get; set; }
        public double WALE_Years { get; set; }

        /// <summary>
        /// Vacancy rate as a percentage.
        /// </summary>
        public double VacancyRate => TotalBuildingArea_sqm > 0
            ? TotalVacantArea_sqm / TotalBuildingArea_sqm * 100.0
            : 0;
    }

    /// <summary>
    /// A single floor within a stacking plan.
    /// </summary>
    public class StackingFloor
    {
        public int FloorLevel { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public double TotalArea_sqm { get; set; }
        public double LeasedArea_sqm { get; set; }
        public double VacantArea_sqm { get; set; }
        public List<StackingSpace> Spaces { get; set; } = new();

        /// <summary>
        /// Floor-level occupancy rate.
        /// </summary>
        public double OccupancyRate => TotalArea_sqm > 0
            ? LeasedArea_sqm / TotalArea_sqm * 100.0
            : 0;
    }

    /// <summary>
    /// A space unit within a stacking plan floor.
    /// </summary>
    public class StackingSpace
    {
        public string SpaceId { get; set; } = string.Empty;
        public string SpaceName { get; set; } = string.Empty;
        public SpaceType SpaceType { get; set; }
        public double Area_sqm { get; set; }
        public OccupancyStatus OccupancyStatus { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public DateTime? LeaseExpiry { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal RentPerSqm { get; set; }
    }

    #endregion

    #region Cash Flow and Analysis Models

    /// <summary>
    /// Multi-year cash flow projection for a building or portfolio.
    /// </summary>
    public class CashFlowProjection
    {
        public string BuildingId { get; set; } = string.Empty;
        public int ProjectionYears { get; set; }
        public DateTime ProjectionStartDate { get; set; }
        public Currency Currency { get; set; } = Currency.USD;
        public List<AnnualCashFlow> AnnualFlows { get; set; } = new();
        public decimal TotalProjectedIncome { get; set; }
        public decimal NetPresentValue { get; set; }
        public double DiscountRate { get; set; } = 0.08;
    }

    /// <summary>
    /// Projected cash flow for a single year.
    /// </summary>
    public class AnnualCashFlow
    {
        public int Year { get; set; }
        public decimal GrossRentalIncome { get; set; }
        public decimal VacancyAllowance { get; set; }
        public decimal EffectiveGrossIncome { get; set; }
        public decimal ServiceChargeIncome { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal NetOperatingIncome { get; set; }
        public double ProjectedOccupancyRate { get; set; }
        public int LeaseExpiriesCount { get; set; }
        public decimal EscalationImpact { get; set; }
    }

    /// <summary>
    /// Vacancy analysis result with current and projected vacancy data.
    /// </summary>
    public class VacancyAnalysis
    {
        public string BuildingId { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public double CurrentVacancyRate { get; set; }
        public double TotalVacantArea_sqm { get; set; }
        public int VacantSpaceCount { get; set; }
        public decimal EstimatedVacancyLoss { get; set; }
        public List<VacancyByFloor> ByFloor { get; set; } = new();
        public List<VacancyByType> ByType { get; set; } = new();
        public List<ProjectedVacancy> Projections { get; set; } = new();
    }

    /// <summary>
    /// Vacancy breakdown for a single floor.
    /// </summary>
    public class VacancyByFloor
    {
        public int FloorLevel { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public double VacantArea_sqm { get; set; }
        public double TotalArea_sqm { get; set; }
        public double VacancyRate { get; set; }
        public int VacantSpaceCount { get; set; }
    }

    /// <summary>
    /// Vacancy breakdown by space type.
    /// </summary>
    public class VacancyByType
    {
        public SpaceType SpaceType { get; set; }
        public double VacantArea_sqm { get; set; }
        public double TotalArea_sqm { get; set; }
        public double VacancyRate { get; set; }
        public int VacantSpaceCount { get; set; }
    }

    /// <summary>
    /// Projected vacancy at a future date based on lease expiries.
    /// </summary>
    public class ProjectedVacancy
    {
        public DateTime ProjectionDate { get; set; }
        public double ProjectedVacancyRate { get; set; }
        public double AdditionalVacantArea_sqm { get; set; }
        public int ExpiringLeaseCount { get; set; }
        public double AssumedRenewalRate { get; set; } = 0.65;
    }

    /// <summary>
    /// Space requirements specification for tenant space matching.
    /// </summary>
    public class SpaceRequirements
    {
        public double MinArea_sqm { get; set; }
        public double MaxArea_sqm { get; set; }
        public SpaceType PreferredType { get; set; } = SpaceType.Office;
        public List<int> PreferredFloors { get; set; } = new();
        public int RequiredHeadcount { get; set; }
        public bool NeedsNaturalLight { get; set; }
        public bool NeedsReception { get; set; }
        public List<string> RequiredAmenities { get; set; } = new();
        public decimal MaxBudgetPerSqm { get; set; }
        public List<string> AdjacentTenantPreferences { get; set; } = new();
        public DateTime RequiredByDate { get; set; }
    }

    /// <summary>
    /// Area breakdown following BOMA/RICS measurement standards.
    /// </summary>
    public class AreaBreakdown
    {
        public string BuildingId { get; set; } = string.Empty;
        public string Standard { get; set; } = "BOMA 2017";
        public double GrossInternalArea_sqm { get; set; }
        public double NetInternalArea_sqm { get; set; }
        public double NetLettableArea_sqm { get; set; }
        public double CommonArea_sqm { get; set; }
        public double ServiceArea_sqm { get; set; }
        public double CirculationArea_sqm { get; set; }
        public double ParkingArea_sqm { get; set; }
        public double ExternalArea_sqm { get; set; }
        public double BuildingEfficiency => GrossInternalArea_sqm > 0
            ? NetLettableArea_sqm / GrossInternalArea_sqm * 100.0
            : 0;
        public List<FloorAreaBreakdown> FloorBreakdowns { get; set; } = new();
    }

    /// <summary>
    /// Area breakdown for a specific floor.
    /// </summary>
    public class FloorAreaBreakdown
    {
        public int FloorLevel { get; set; }
        public string FloorName { get; set; } = string.Empty;
        public double GrossArea_sqm { get; set; }
        public double NetArea_sqm { get; set; }
        public double LettableArea_sqm { get; set; }
        public double CommonArea_sqm { get; set; }
        public double CommonAreaFactor { get; set; }
    }

    #endregion
}
