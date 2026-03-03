// ============================================================================
// StingBIM AI - Predictive Maintenance Models
// Domain models for asset condition monitoring, failure prediction,
// work order management, spare parts tracking, and maintenance KPIs.
// Aligned with ISO 55000 (Asset Management) and ASTM E2018 (PCA).
// ============================================================================

using System;
using System.Collections.Generic;

namespace StingBIM.AI.Maintenance.Models
{
    #region Enums

    /// <summary>
    /// Asset condition grade aligned with ASTM E2018 Property Condition Assessment.
    /// </summary>
    public enum ConditionGrade
    {
        /// <summary>No visible defects; asset is new or like-new.</summary>
        A = 0,

        /// <summary>Minor cosmetic defects; fully functional with no remediation needed.</summary>
        B = 1,

        /// <summary>Moderate deterioration; functional but requires planned maintenance.</summary>
        C = 2,

        /// <summary>Significant deterioration; functional but approaching end of service life.</summary>
        D = 3,

        /// <summary>Severe deterioration; limited remaining function, immediate action required.</summary>
        E = 4,

        /// <summary>Failed or non-functional; replacement or major overhaul required.</summary>
        F = 5
    }

    /// <summary>
    /// Maintenance strategy type per ISO 55000 classification.
    /// </summary>
    public enum MaintenanceType
    {
        /// <summary>Time-based or usage-based scheduled maintenance.</summary>
        Preventive = 0,

        /// <summary>Condition-based maintenance driven by analytics and predictions.</summary>
        Predictive = 1,

        /// <summary>Reactive maintenance performed after failure occurs.</summary>
        Corrective = 2,

        /// <summary>Triggered by real-time condition monitoring thresholds.</summary>
        ConditionBased = 3,

        /// <summary>Planned replacement or refurbishment at end of service life.</summary>
        CapitalRenewal = 4,

        /// <summary>Regulatory or code-mandated inspection and servicing.</summary>
        Statutory = 5
    }

    /// <summary>
    /// Work order lifecycle status with defined state transitions.
    /// </summary>
    public enum WorkOrderStatus
    {
        /// <summary>Work order created but not yet approved or scheduled.</summary>
        Open = 0,

        /// <summary>Work order approved and assigned to a technician.</summary>
        Assigned = 1,

        /// <summary>Work is actively being performed.</summary>
        InProgress = 2,

        /// <summary>Work paused due to parts, access, or other constraints.</summary>
        OnHold = 3,

        /// <summary>All work completed and verified.</summary>
        Completed = 4,

        /// <summary>Work order cancelled before completion.</summary>
        Cancelled = 5,

        /// <summary>Work order closed after review of completed work.</summary>
        Closed = 6
    }

    /// <summary>
    /// Equipment failure mode classification for root cause analysis.
    /// </summary>
    public enum FailureMode
    {
        /// <summary>Gradual material loss from surface friction or abrasion.</summary>
        Wear = 0,

        /// <summary>Electrochemical degradation of metallic components.</summary>
        Corrosion = 1,

        /// <summary>Crack propagation under cyclic loading below yield strength.</summary>
        Fatigue = 2,

        /// <summary>Sudden failure from load exceeding material capacity.</summary>
        Overload = 3,

        /// <summary>Insulation breakdown, short circuit, or component burnout.</summary>
        Electrical = 4,

        /// <summary>Foreign material ingress degrading performance or causing blockage.</summary>
        Contamination = 5,

        /// <summary>Shaft or coupling angular/parallel offset causing vibration.</summary>
        Misalignment = 6,

        /// <summary>Rotating mass asymmetry causing excessive vibration.</summary>
        Imbalance = 7,

        /// <summary>Prolonged exposure to temperatures outside design envelope.</summary>
        ThermalDegradation = 8,

        /// <summary>Seal, gasket, or joint failure allowing fluid escape.</summary>
        Leakage = 9,

        /// <summary>Failure mode not yet determined during investigation.</summary>
        Unknown = 99
    }

    /// <summary>
    /// Work order priority level determining response times and resource allocation.
    /// </summary>
    public enum WorkOrderPriority
    {
        /// <summary>Life safety or critical system failure; immediate response required.</summary>
        Emergency = 0,

        /// <summary>Major operational impact; respond within 4 hours.</summary>
        Urgent = 1,

        /// <summary>Significant but non-critical; respond within 24 hours.</summary>
        High = 2,

        /// <summary>Standard maintenance; respond within 1 week.</summary>
        Medium = 3,

        /// <summary>Minor or cosmetic; schedule at convenience.</summary>
        Low = 4
    }

    /// <summary>
    /// Degradation pattern type detected from sensor or inspection history.
    /// </summary>
    public enum DegradationPattern
    {
        /// <summary>Constant rate of deterioration over time (y = ax + b).</summary>
        Linear = 0,

        /// <summary>Accelerating deterioration as condition worsens (y = ae^bx).</summary>
        Exponential = 1,

        /// <summary>Sudden condition drop at discrete points (step function).</summary>
        Step = 2,

        /// <summary>Rapid initial change that gradually levels off (y = a * ln(x) + b).</summary>
        Logarithmic = 3,

        /// <summary>No clear pattern; condition fluctuates randomly.</summary>
        Random = 4
    }

    #endregion

    #region Core Asset Models

    /// <summary>
    /// Represents the current condition state of a building or MEP asset.
    /// Combines sensor data, inspection results, and analytical assessments.
    /// </summary>
    public class AssetCondition
    {
        /// <summary>Unique identifier of the asset being assessed.</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>Human-readable asset name or tag.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Asset category (e.g., HVAC, Electrical, Plumbing, Structural).</summary>
        public string AssetCategory { get; set; } = string.Empty;

        /// <summary>Composite health score from 0 (failed) to 100 (perfect).</summary>
        public double HealthScore { get; set; }

        /// <summary>ASTM E2018-aligned condition grade derived from health score.</summary>
        public ConditionGrade ConditionGrade { get; set; }

        /// <summary>Timestamp of the most recent condition assessment.</summary>
        public DateTime LastAssessment { get; set; }

        /// <summary>Rate of health score decline per month (points/month).</summary>
        public double DegradationRate { get; set; }

        /// <summary>Detected deterioration pattern from historical data.</summary>
        public DegradationPattern DegradationPattern { get; set; }

        /// <summary>Estimated remaining useful life in months.</summary>
        public double RemainingUsefulLifeMonths { get; set; }

        /// <summary>Current replacement value of the asset in USD.</summary>
        public decimal CurrentReplacementValue { get; set; }

        /// <summary>Estimated cost to restore asset to grade A condition.</summary>
        public decimal DeferredMaintenanceCost { get; set; }

        /// <summary>Age of the asset in years since installation.</summary>
        public double AgeYears { get; set; }

        /// <summary>Manufacturer-specified expected service life in years.</summary>
        public double ExpectedServiceLifeYears { get; set; }

        /// <summary>Individual factor scores contributing to the composite health score.</summary>
        public Dictionary<string, double> FactorScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Most recent sensor readings keyed by sensor type.</summary>
        public Dictionary<string, double> SensorReadings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Maintenance Planning Models

    /// <summary>
    /// A scheduled maintenance plan for an asset, defining the type, interval,
    /// estimated cost, and next due date for maintenance activities.
    /// </summary>
    public class MaintenancePlan
    {
        /// <summary>Unique plan identifier.</summary>
        public string PlanId { get; set; } = string.Empty;

        /// <summary>Asset to which this plan applies.</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>Human-readable plan name (e.g., "Quarterly HVAC Filter Replacement").</summary>
        public string PlanName { get; set; } = string.Empty;

        /// <summary>Maintenance strategy type.</summary>
        public MaintenanceType Type { get; set; }

        /// <summary>Interval between maintenance activities in days.</summary>
        public int IntervalDays { get; set; }

        /// <summary>Next scheduled execution date.</summary>
        public DateTime NextDueDate { get; set; }

        /// <summary>Date of last completed execution.</summary>
        public DateTime? LastCompletedDate { get; set; }

        /// <summary>Estimated cost per execution in USD.</summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>Estimated labor hours per execution.</summary>
        public double EstimatedLaborHours { get; set; }

        /// <summary>Priority level for scheduling conflicts.</summary>
        public WorkOrderPriority Priority { get; set; }

        /// <summary>Whether the plan is currently active.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Required spare parts for each execution.</summary>
        public List<SparePartRequirement> RequiredParts { get; set; } = new();

        /// <summary>Step-by-step maintenance procedure.</summary>
        public List<string> ProcedureSteps { get; set; } = new();

        /// <summary>Required technician skill tags.</summary>
        public List<string> RequiredSkills { get; set; } = new();

        /// <summary>Safety precautions and lockout/tagout requirements.</summary>
        public List<string> SafetyPrecautions { get; set; } = new();

        /// <summary>Compliance standard this plan satisfies (e.g., "ASHRAE 180").</summary>
        public string ComplianceStandard { get; set; } = string.Empty;
    }

    /// <summary>
    /// Spare part quantity requirement for a maintenance plan.
    /// </summary>
    public class SparePartRequirement
    {
        public string PartId { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    #endregion

    #region Work Order Models

    /// <summary>
    /// A maintenance work order representing a unit of maintenance work,
    /// from creation through execution to completion and cost capture.
    /// </summary>
    public class WorkOrder
    {
        /// <summary>Unique work order identifier (auto-generated).</summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>Parent work order if this is a child task.</summary>
        public string ParentOrderId { get; set; } = string.Empty;

        /// <summary>Asset being serviced.</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>Human-readable asset name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Maintenance type classification.</summary>
        public MaintenanceType Type { get; set; }

        /// <summary>Brief description of the work to be performed.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Detailed maintenance procedure.</summary>
        public string Procedure { get; set; } = string.Empty;

        /// <summary>Technician or team assigned to perform the work.</summary>
        public string AssignedTo { get; set; } = string.Empty;

        /// <summary>Current lifecycle status.</summary>
        public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;

        /// <summary>Priority level.</summary>
        public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

        /// <summary>Date and time the work order was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Scheduled start date.</summary>
        public DateTime? ScheduledDate { get; set; }

        /// <summary>Target completion date based on SLA.</summary>
        public DateTime? TargetCompletionDate { get; set; }

        /// <summary>Actual start timestamp.</summary>
        public DateTime? StartedDate { get; set; }

        /// <summary>Actual completion timestamp.</summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>Actual labor hours expended.</summary>
        public double LaborHours { get; set; }

        /// <summary>Actual parts cost in USD.</summary>
        public decimal PartsCost { get; set; }

        /// <summary>Actual labor cost in USD.</summary>
        public decimal LaborCost { get; set; }

        /// <summary>Total actual cost (labor + parts + overhead).</summary>
        public decimal TotalCost { get; set; }

        /// <summary>Estimated cost at time of creation.</summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>Originating maintenance plan, if any.</summary>
        public string MaintenancePlanId { get; set; } = string.Empty;

        /// <summary>Failure mode identified during execution.</summary>
        public FailureMode? IdentifiedFailureMode { get; set; }

        /// <summary>Root cause analysis notes.</summary>
        public string RootCauseNotes { get; set; } = string.Empty;

        /// <summary>Warranty claim reference if applicable.</summary>
        public string WarrantyReference { get; set; } = string.Empty;

        /// <summary>Whether the work is covered under warranty.</summary>
        public bool IsWarrantyCovered { get; set; }

        /// <summary>Completion notes and technician comments.</summary>
        public List<string> Notes { get; set; } = new();

        /// <summary>Parts consumed during execution.</summary>
        public List<SparePartUsage> PartsUsed { get; set; } = new();

        /// <summary>Status change audit trail.</summary>
        public List<StatusChange> StatusHistory { get; set; } = new();
    }

    /// <summary>
    /// Records a spare part consumed during work order execution.
    /// </summary>
    public class SparePartUsage
    {
        public string PartId { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int QuantityUsed { get; set; }
        public decimal UnitCost { get; set; }
    }

    /// <summary>
    /// Audit record of a work order status transition.
    /// </summary>
    public class StatusChange
    {
        public WorkOrderStatus FromStatus { get; set; }
        public WorkOrderStatus ToStatus { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ChangedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    #endregion

    #region Failure Prediction Models

    /// <summary>
    /// Result of a predictive failure analysis for an asset, including
    /// predicted failure date, remaining useful life, and risk scoring.
    /// </summary>
    public class FailurePrediction
    {
        /// <summary>Asset being analyzed.</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>Human-readable asset name.</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>Predicted date of next failure event.</summary>
        public DateTime PredictedFailureDate { get; set; }

        /// <summary>Confidence level of the prediction (0.0 to 1.0).</summary>
        public double Confidence { get; set; }

        /// <summary>Estimated remaining useful life in days.</summary>
        public double RemainingUsefulLifeDays { get; set; }

        /// <summary>Most likely failure mode.</summary>
        public FailureMode FailureMode { get; set; }

        /// <summary>Composite risk score (0-100) combining probability and consequence.</summary>
        public double RiskScore { get; set; }

        /// <summary>Failure probability within the prediction window (0.0 to 1.0).</summary>
        public double FailureProbability { get; set; }

        /// <summary>Consequence severity score (0-100).</summary>
        public double ConsequenceScore { get; set; }

        /// <summary>Estimated cost of unplanned failure in USD.</summary>
        public decimal EstimatedFailureCost { get; set; }

        /// <summary>Cost of preventive action to avoid failure in USD.</summary>
        public decimal PreventiveActionCost { get; set; }

        /// <summary>Weibull shape parameter (beta) used in the analysis.</summary>
        public double WeibullShape { get; set; }

        /// <summary>Weibull scale parameter (eta) used in the analysis.</summary>
        public double WeibullScale { get; set; }

        /// <summary>Contributing degradation factors and their weights.</summary>
        public List<DegradationFactor> DegradationFactors { get; set; } = new();

        /// <summary>Recommended maintenance actions.</summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>Timestamp when this prediction was generated.</summary>
        public DateTime PredictionTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Model version that produced this prediction.</summary>
        public string ModelVersion { get; set; } = "7.0.0";
    }

    /// <summary>
    /// A single factor contributing to asset degradation, with weight and current state.
    /// </summary>
    public class DegradationFactor
    {
        public string FactorName { get; set; } = string.Empty;
        public double Weight { get; set; }
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public double NormalizedScore { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    #endregion

    #region Spare Parts Models

    /// <summary>
    /// Spare part inventory record with reorder management.
    /// </summary>
    public class SparePart
    {
        /// <summary>Unique part identifier or SKU.</summary>
        public string PartId { get; set; } = string.Empty;

        /// <summary>Part name / description.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Part category (e.g., Filters, Belts, Bearings, Valves).</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Manufacturer part number.</summary>
        public string ManufacturerPartNumber { get; set; } = string.Empty;

        /// <summary>List of asset IDs this part is compatible with.</summary>
        public List<string> CompatibleAssets { get; set; } = new();

        /// <summary>Supplier lead time in days.</summary>
        public int LeadTimeDays { get; set; }

        /// <summary>Current quantity in stock.</summary>
        public int CurrentStock { get; set; }

        /// <summary>Minimum stock level that triggers reorder.</summary>
        public int ReorderPoint { get; set; }

        /// <summary>Economic order quantity for reorder.</summary>
        public int ReorderQuantity { get; set; }

        /// <summary>Unit cost in USD.</summary>
        public decimal UnitCost { get; set; }

        /// <summary>Whether the part is currently on order.</summary>
        public bool OnOrder { get; set; }

        /// <summary>Expected delivery date if on order.</summary>
        public DateTime? ExpectedDeliveryDate { get; set; }

        /// <summary>Storage location or bin reference.</summary>
        public string StorageLocation { get; set; } = string.Empty;

        /// <summary>Average monthly consumption rate.</summary>
        public double AverageMonthlyUsage { get; set; }
    }

    /// <summary>
    /// Forecasted spare parts demand for a future period.
    /// </summary>
    public class SparePartForecast
    {
        public string PartId { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int ForecastMonths { get; set; }
        public int PredictedDemand { get; set; }
        public int CurrentStock { get; set; }
        public int ShortfallQuantity { get; set; }
        public decimal EstimatedCost { get; set; }
        public DateTime RecommendedOrderDate { get; set; }
    }

    #endregion

    #region KPI Models

    /// <summary>
    /// Key Performance Indicators for maintenance operations,
    /// aligned with ISO 55000 and EN 15341 maintenance KPIs.
    /// </summary>
    public class MaintenanceKPI
    {
        /// <summary>Period start date for the KPI calculation.</summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>Period end date for the KPI calculation.</summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>Mean Time Between Failures in hours.</summary>
        public double MTBF { get; set; }

        /// <summary>Mean Time To Repair in hours.</summary>
        public double MTTR { get; set; }

        /// <summary>Asset availability percentage (0-100). A = MTBF / (MTBF + MTTR).</summary>
        public double Availability { get; set; }

        /// <summary>Overall Equipment Effectiveness (0-100). OEE = Availability x Performance x Quality.</summary>
        public double OEE { get; set; }

        /// <summary>Total backlog of open maintenance work in labor-hours.</summary>
        public double BacklogHours { get; set; }

        /// <summary>Ratio of planned to unplanned maintenance (higher is better).</summary>
        public double PlannedVsUnplannedRatio { get; set; }

        /// <summary>Percentage of work orders completed on time per SLA.</summary>
        public double SLACompliancePercent { get; set; }

        /// <summary>Percentage of PM work orders completed on schedule.</summary>
        public double PMCompliancePercent { get; set; }

        /// <summary>Total maintenance cost for the period in USD.</summary>
        public decimal TotalMaintenanceCost { get; set; }

        /// <summary>Maintenance cost per unit area (USD/sqft).</summary>
        public decimal CostPerSquareFoot { get; set; }

        /// <summary>Number of work orders completed in the period.</summary>
        public int WorkOrdersCompleted { get; set; }

        /// <summary>Number of work orders currently open.</summary>
        public int WorkOrdersOpen { get; set; }

        /// <summary>Number of emergency/corrective work orders in the period.</summary>
        public int EmergencyWorkOrders { get; set; }

        /// <summary>Average technician utilization percentage (0-100).</summary>
        public double LaborUtilizationPercent { get; set; }

        /// <summary>Facility Condition Index: deferred maintenance / replacement value (0.0-1.0).</summary>
        public double FacilityConditionIndex { get; set; }
    }

    #endregion

    #region Inspection Models

    /// <summary>
    /// Results of a physical asset condition inspection, aligned with ASTM E2018.
    /// </summary>
    public class InspectionResult
    {
        /// <summary>Unique inspection identifier.</summary>
        public string InspectionId { get; set; } = string.Empty;

        /// <summary>Asset inspected.</summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>Name or ID of the inspector.</summary>
        public string Inspector { get; set; } = string.Empty;

        /// <summary>Date and time the inspection was performed.</summary>
        public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

        /// <summary>Inspection type (e.g., Routine, Annual, Pre-Purchase, Special).</summary>
        public string InspectionType { get; set; } = string.Empty;

        /// <summary>General findings and observations narrative.</summary>
        public string Findings { get; set; } = string.Empty;

        /// <summary>Photo references or file paths documenting findings.</summary>
        public List<string> Photos { get; set; } = new();

        /// <summary>Overall condition grade assigned by the inspector.</summary>
        public ConditionGrade ConditionGrade { get; set; }

        /// <summary>Individual defects identified during inspection.</summary>
        public List<DefectRecord> Defects { get; set; } = new();

        /// <summary>Whether the asset passed minimum condition requirements.</summary>
        public bool PassedMinimumStandard { get; set; }

        /// <summary>Next recommended inspection date.</summary>
        public DateTime? NextInspectionDate { get; set; }
    }

    /// <summary>
    /// A specific defect identified during an asset inspection.
    /// </summary>
    public class DefectRecord
    {
        /// <summary>Unique defect identifier.</summary>
        public string DefectId { get; set; } = string.Empty;

        /// <summary>Description of the defect observed.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity from 1 (minor cosmetic) to 5 (critical safety).</summary>
        public int Severity { get; set; }

        /// <summary>Physical location on the asset where the defect was found.</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Photo reference documenting this specific defect.</summary>
        public string PhotoRef { get; set; } = string.Empty;

        /// <summary>Recommended remediation action.</summary>
        public string RecommendedAction { get; set; } = string.Empty;

        /// <summary>Estimated cost to remediate the defect in USD.</summary>
        public decimal EstimatedRemediationCost { get; set; }

        /// <summary>Whether the defect poses a safety hazard.</summary>
        public bool IsSafetyHazard { get; set; }

        /// <summary>Current status (Open, InReview, Scheduled, Remediated, Accepted).</summary>
        public string Status { get; set; } = "Open";
    }

    #endregion

    #region Risk Assessment Models

    /// <summary>
    /// Risk assessment result from a failure probability x consequence analysis.
    /// </summary>
    public class RiskAssessment
    {
        public string AssetId { get; set; } = string.Empty;
        public double FailureProbability { get; set; }
        public double ConsequenceSeverity { get; set; }
        public double RiskScore { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
        public List<string> MitigationActions { get; set; } = new();
        public decimal MitigationCost { get; set; }
        public decimal PotentialLoss { get; set; }
        public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Repair vs. replace cost-benefit analysis result.
    /// </summary>
    public class RepairReplaceAnalysis
    {
        public string AssetId { get; set; } = string.Empty;
        public decimal RepairCost { get; set; }
        public decimal ReplacementCost { get; set; }
        public double RepairExtendedLifeYears { get; set; }
        public double ReplacementLifeYears { get; set; }
        public decimal RepairAnnualizedCost { get; set; }
        public decimal ReplacementAnnualizedCost { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public double BreakEvenYears { get; set; }
    }

    /// <summary>
    /// Technician profile for skill-based work order assignment.
    /// </summary>
    public class TechnicianProfile
    {
        public string TechnicianId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new();
        public List<string> Certifications { get; set; } = new();
        public int ActiveWorkOrders { get; set; }
        public int MaxConcurrentWorkOrders { get; set; } = 5;
        public double UtilizationPercent { get; set; }
        public bool IsAvailable { get; set; } = true;
        public double AverageCompletionRatePerDay { get; set; }
    }

    /// <summary>
    /// Sensor data reading for condition-based monitoring.
    /// </summary>
    public class SensorReading
    {
        public string SensorId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public string SensorType { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsAnomalous { get; set; }
    }

    #endregion
}
