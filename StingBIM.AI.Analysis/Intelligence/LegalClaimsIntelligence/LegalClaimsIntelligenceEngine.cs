// ============================================================================
// StingBIM Legal Claims Intelligence Engine
// Delay claims, forensic scheduling, dispute documentation, EOT analysis
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LegalClaimsIntelligence
{
    #region Enums

    public enum ClaimType { Delay, Disruption, Acceleration, ChangedConditions, ExtraWork, DefectiveSpecifications, Differing, Suspension, Termination, OwnerCausedDelay, ThirdPartyDelay, ForceMAjeure }
    public enum ClaimStatus { Draft, PendingReview, Submitted, UnderNegotiation, Mediation, Arbitration, Litigation, Settled, Withdrawn, Denied }
    public enum DelayType { ExcusableCompensable, ExcusableNonCompensable, NonExcusable, Concurrent, Pacing }
    public enum EOTStatus { Requested, UnderReview, Approved, PartiallyApproved, Denied, Appealed }
    public enum DisputeStatus { Identified, Documented, NoticeGiven, Negotiating, Escalated, Resolved, Closed }
    public enum ForensicMethod { AsPlanned, AsBuilt, ImpactedAsPlanned, CollapsedAsBuilt, WindowsAnalysis, TimeImpactAnalysis }
    public enum DocumentCategory { Notice, Correspondence, DailyReport, Photo, Video, Drawing, Specification, ChangeOrder, RFI, Meeting, Schedule, Financial, Expert, Legal }
    public enum LiabilityParty { Owner, Contractor, Subcontractor, Designer, ThirdParty, Shared, Unknown }
    public enum DamageType { DirectCosts, IndirectCosts, ExtendedOverhead, LostProductivity, Escalation, LostProfits, LiquidatedDamages, Interest }

    #endregion

    #region Data Models

    public class Claim
    {
        public string ClaimId { get; set; } = Guid.NewGuid().ToString();
        public string ClaimNumber { get; set; }
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ClaimType Type { get; set; }
        public ClaimStatus Status { get; set; }
        public ClaimDetails Details { get; set; }
        public ClaimQuantification Quantification { get; set; }
        public List<ClaimEvent> Events { get; set; } = new List<ClaimEvent>();
        public List<ClaimDocument> Documents { get; set; } = new List<ClaimDocument>();
        public List<ClaimCorrespondence> Correspondence { get; set; } = new List<ClaimCorrespondence>();
        public ClaimTimeline Timeline { get; set; }
        public ClaimSettlement Settlement { get; set; }
        public LiabilityAnalysis LiabilityAnalysis { get; set; }
        public string PreparedBy { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    }

    public class ClaimDetails { public string ContractReference { get; set; } public string ClauseBasis { get; set; } public string CausalNarrative { get; set; } public string ImpactDescription { get; set; } public LiabilityParty ResponsibleParty { get; set; } public List<string> AffectedActivities { get; set; } = new List<string>(); public List<string> ContractualEntitlements { get; set; } = new List<string>(); }
    public class ClaimTimeline { public DateTime EventStartDate { get; set; } public DateTime EventEndDate { get; set; } public DateTime NoticeDate { get; set; } public DateTime SubmissionDeadline { get; set; } public DateTime? ResponseDeadline { get; set; } public int DaysToRespond { get; set; } public List<ClaimMilestone> Milestones { get; set; } = new List<ClaimMilestone>(); }
    public class ClaimMilestone { public string MilestoneId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public DateTime DueDate { get; set; } public DateTime? CompletedDate { get; set; } public string Status { get; set; } }

    public class ClaimQuantification
    {
        public int DaysClaimed { get; set; }
        public int DaysAwarded { get; set; }
        public decimal CostsClaimed { get; set; }
        public decimal CostsAwarded { get; set; }
        public List<DamageItem> Damages { get; set; } = new List<DamageItem>();
        public ExtendedOverheadCalculation ExtendedOverhead { get; set; }
        public ProductivityLossCalculation ProductivityLoss { get; set; }
        public decimal TotalClaimValue { get; set; }
        public decimal TotalSettledValue { get; set; }
    }

    public class DamageItem { public string ItemId { get; set; } = Guid.NewGuid().ToString(); public DamageType Type { get; set; } public string Description { get; set; } public decimal Amount { get; set; } public string Methodology { get; set; } public List<string> SupportingDocuments { get; set; } = new List<string>(); }
    public class ExtendedOverheadCalculation { public string Method { get; set; } public decimal DailyRate { get; set; } public int ExtendedDays { get; set; } public decimal TotalAmount { get; set; } public string EichlayFormula { get; set; } public decimal ContractBillings { get; set; } public decimal OverheadPercent { get; set; } public int OriginalContractDays { get; set; } }
    public class ProductivityLossCalculation { public string Method { get; set; } public decimal BaselineProductivity { get; set; } public decimal ImpactedProductivity { get; set; } public decimal LossPercent { get; set; } public decimal LaborHoursAffected { get; set; } public decimal LaborRate { get; set; } public decimal TotalLoss { get; set; } }

    public class ClaimEvent { public string EventId { get; set; } = Guid.NewGuid().ToString(); public string Description { get; set; } public DateTime EventDate { get; set; } public string EventType { get; set; } public DelayType? DelayType { get; set; } public int? ImpactDays { get; set; } public decimal? ImpactCost { get; set; } public string Responsibility { get; set; } public List<string> EvidenceIds { get; set; } = new List<string>(); }
    public class ClaimDocument { public string DocumentId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public DocumentCategory Category { get; set; } public string Description { get; set; } public DateTime DocumentDate { get; set; } public string FilePath { get; set; } public string Author { get; set; } public List<string> RelatedEventIds { get; set; } = new List<string>(); public DateTime UploadDate { get; set; } = DateTime.UtcNow; }
    public class ClaimCorrespondence { public string CorrespondenceId { get; set; } = Guid.NewGuid().ToString(); public string Subject { get; set; } public string From { get; set; } public string To { get; set; } public DateTime Date { get; set; } public string Type { get; set; } public string Content { get; set; } public bool RequiresResponse { get; set; } public DateTime? ResponseDeadline { get; set; } }
    public class ClaimSettlement { public string SettlementId { get; set; } = Guid.NewGuid().ToString(); public DateTime SettlementDate { get; set; } public int DaysGranted { get; set; } public decimal AmountSettled { get; set; } public string Terms { get; set; } public string SettlementType { get; set; } public List<string> Conditions { get; set; } = new List<string>(); }
    public class LiabilityAnalysis { public LiabilityParty PrimaryLiability { get; set; } public Dictionary<LiabilityParty, decimal> LiabilityAllocation { get; set; } = new Dictionary<LiabilityParty, decimal>(); public List<string> ContributingFactors { get; set; } = new List<string>(); public string AnalysisNarrative { get; set; } public double SuccessProbability { get; set; } }

    public class DelayAnalysis
    {
        public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ClaimId { get; set; }
        public ForensicMethod Method { get; set; }
        public DateTime AnalysisCutoffDate { get; set; }
        public ScheduleData AsPlannedSchedule { get; set; }
        public ScheduleData AsBuiltSchedule { get; set; }
        public List<DelayEvent> DelayEvents { get; set; } = new List<DelayEvent>();
        public CriticalPathAnalysis CriticalPathAnalysis { get; set; }
        public DelayAnalysisSummary Summary { get; set; }
        public List<string> Assumptions { get; set; } = new List<string>();
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    }

    public class ScheduleData { public string ScheduleId { get; set; } public string Name { get; set; } public DateTime DataDate { get; set; } public DateTime ProjectStart { get; set; } public DateTime ProjectFinish { get; set; } public int TotalActivities { get; set; } public int CriticalActivities { get; set; } public double TotalFloat { get; set; } }
    public class DelayEvent { public string EventId { get; set; } = Guid.NewGuid().ToString(); public string Description { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public int DurationDays { get; set; } public DelayType Type { get; set; } public LiabilityParty Responsibility { get; set; } public bool IsCritical { get; set; } public int CriticalImpactDays { get; set; } public List<string> AffectedActivities { get; set; } = new List<string>(); public string CausalLink { get; set; } }
    public class CriticalPathAnalysis { public List<string> CriticalActivities { get; set; } = new List<string>(); public int OriginalDuration { get; set; } public int ExtendedDuration { get; set; } public int TotalDelay { get; set; } public Dictionary<LiabilityParty, int> DelayByResponsibility { get; set; } = new Dictionary<LiabilityParty, int>(); }
    public class DelayAnalysisSummary { public int TotalDelayDays { get; set; } public int ExcusableCompensableDays { get; set; } public int ExcusableNonCompensableDays { get; set; } public int NonExcusableDays { get; set; } public int ConcurrentDays { get; set; } public int NetEntitlementDays { get; set; } public Dictionary<DelayType, int> DelayBreakdown { get; set; } = new Dictionary<DelayType, int>(); public string Conclusion { get; set; } }

    public class EOTRequest
    {
        public string EOTId { get; set; } = Guid.NewGuid().ToString();
        public string EOTNumber { get; set; }
        public string ProjectId { get; set; }
        public string ClaimId { get; set; }
        public EOTStatus Status { get; set; }
        public int DaysRequested { get; set; }
        public int DaysApproved { get; set; }
        public DateTime OriginalCompletionDate { get; set; }
        public DateTime RequestedCompletionDate { get; set; }
        public DateTime? ApprovedCompletionDate { get; set; }
        public EOTJustification Justification { get; set; }
        public List<EOTEvent> SupportingEvents { get; set; } = new List<EOTEvent>();
        public EOTResponse Response { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class EOTJustification { public string ContractClause { get; set; } public string Basis { get; set; } public string Narrative { get; set; } public List<string> AffectedMilestones { get; set; } = new List<string>(); public List<string> SupportingDocuments { get; set; } = new List<string>(); }
    public class EOTEvent { public string EventId { get; set; } public string Description { get; set; } public DateTime EventDate { get; set; } public int ImpactDays { get; set; } public string CriticalPathImpact { get; set; } }
    public class EOTResponse { public string ResponseId { get; set; } = Guid.NewGuid().ToString(); public DateTime ResponseDate { get; set; } public string Decision { get; set; } public int DaysGranted { get; set; } public string Rationale { get; set; } public List<string> Conditions { get; set; } = new List<string>(); public string RespondedBy { get; set; } }

    public class Dispute
    {
        public string DisputeId { get; set; } = Guid.NewGuid().ToString();
        public string DisputeNumber { get; set; }
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DisputeStatus Status { get; set; }
        public string DisputeCategory { get; set; }
        public decimal DisputedAmount { get; set; }
        public int DisputedDays { get; set; }
        public List<DisputeParty> Parties { get; set; } = new List<DisputeParty>();
        public List<DisputeIssue> Issues { get; set; } = new List<DisputeIssue>();
        public DisputeResolution Resolution { get; set; }
        public List<string> RelatedClaimIds { get; set; } = new List<string>();
        public DateTime IdentifiedDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class DisputeParty { public string PartyId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public string Role { get; set; } public string ContactName { get; set; } public string ContactEmail { get; set; } public string LegalCounsel { get; set; } }
    public class DisputeIssue { public string IssueId { get; set; } = Guid.NewGuid().ToString(); public string Description { get; set; } public decimal Amount { get; set; } public string ContractReference { get; set; } public string Position { get; set; } public string CounterPosition { get; set; } }
    public class DisputeResolution { public string ResolutionId { get; set; } = Guid.NewGuid().ToString(); public string Method { get; set; } public DateTime ResolutionDate { get; set; } public string Outcome { get; set; } public decimal SettledAmount { get; set; } public int SettledDays { get; set; } public string Terms { get; set; } public List<string> SettlementConditions { get; set; } = new List<string>(); }

    public class ClaimReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public string ClaimId { get; set; }
        public string Title { get; set; }
        public string ReportType { get; set; }
        public ClaimReportSummary Summary { get; set; }
        public List<ClaimReportSection> Sections { get; set; } = new List<ClaimReportSection>();
        public List<ClaimReportExhibit> Exhibits { get; set; } = new List<ClaimReportExhibit>();
        public string PreparedBy { get; set; }
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    }

    public class ClaimReportSummary { public string ExecutiveSummary { get; set; } public int TotalDaysClaimed { get; set; } public decimal TotalCostsClaimed { get; set; } public string RecommendedAction { get; set; } public double SuccessProbability { get; set; } }
    public class ClaimReportSection { public string SectionId { get; set; } = Guid.NewGuid().ToString(); public string Title { get; set; } public int Order { get; set; } public string Content { get; set; } public List<string> Exhibits { get; set; } = new List<string>(); }
    public class ClaimReportExhibit { public string ExhibitId { get; set; } = Guid.NewGuid().ToString(); public string ExhibitNumber { get; set; } public string Title { get; set; } public string Description { get; set; } public string FilePath { get; set; } }

    public class LegalClaimsResult { public bool Success { get; set; } public string Message { get; set; } public string ResultId { get; set; } public object Data { get; set; } public List<string> Warnings { get; set; } = new List<string>(); public DateTime Timestamp { get; set; } = DateTime.UtcNow; }

    #endregion

    #region Engine

    public sealed class LegalClaimsIntelligenceEngine
    {
        private static readonly Lazy<LegalClaimsIntelligenceEngine> _instance = new Lazy<LegalClaimsIntelligenceEngine>(() => new LegalClaimsIntelligenceEngine());
        public static LegalClaimsIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, Claim> _claims;
        private readonly ConcurrentDictionary<string, DelayAnalysis> _delayAnalyses;
        private readonly ConcurrentDictionary<string, EOTRequest> _eotRequests;
        private readonly ConcurrentDictionary<string, Dispute> _disputes;
        private readonly ConcurrentDictionary<string, ClaimReport> _reports;
        private readonly object _syncLock = new object();

        private LegalClaimsIntelligenceEngine()
        {
            _claims = new ConcurrentDictionary<string, Claim>();
            _delayAnalyses = new ConcurrentDictionary<string, DelayAnalysis>();
            _eotRequests = new ConcurrentDictionary<string, EOTRequest>();
            _disputes = new ConcurrentDictionary<string, Dispute>();
            _reports = new ConcurrentDictionary<string, ClaimReport>();
        }

        public LegalClaimsResult CreateClaim(Claim claim)
        {
            if (claim == null) return new LegalClaimsResult { Success = false, Message = "Claim cannot be null" };
            if (string.IsNullOrEmpty(claim.ClaimId)) claim.ClaimId = Guid.NewGuid().ToString();
            claim.CreatedDate = DateTime.UtcNow; claim.LastModifiedDate = DateTime.UtcNow; claim.Status = ClaimStatus.Draft;
            claim.ClaimNumber = $"CLM-{DateTime.UtcNow:yyyyMMdd}-{_claims.Count + 1:D4}";
            claim.Details ??= new ClaimDetails(); claim.Quantification ??= new ClaimQuantification(); claim.Timeline ??= new ClaimTimeline();
            if (_claims.TryAdd(claim.ClaimId, claim)) return new LegalClaimsResult { Success = true, Message = "Claim created successfully", ResultId = claim.ClaimId, Data = claim };
            return new LegalClaimsResult { Success = false, Message = "Failed to create claim" };
        }

        public Claim GetClaim(string claimId) { _claims.TryGetValue(claimId, out var claim); return claim; }
        public List<Claim> GetClaimsByProject(string projectId) => _claims.Values.Where(c => c.ProjectId == projectId).OrderByDescending(c => c.CreatedDate).ToList();
        public List<Claim> GetClaimsByStatus(ClaimStatus status) => _claims.Values.Where(c => c.Status == status).ToList();

        public LegalClaimsResult DocumentDelay(string claimId, ClaimEvent delayEvent)
        {
            if (!_claims.TryGetValue(claimId, out var claim)) return new LegalClaimsResult { Success = false, Message = "Claim not found" };
            if (delayEvent == null) return new LegalClaimsResult { Success = false, Message = "Event cannot be null" };
            if (string.IsNullOrEmpty(delayEvent.EventId)) delayEvent.EventId = Guid.NewGuid().ToString();
            claim.Events.Add(delayEvent); claim.LastModifiedDate = DateTime.UtcNow;
            if (delayEvent.ImpactDays.HasValue) claim.Quantification.DaysClaimed += delayEvent.ImpactDays.Value;
            if (delayEvent.ImpactCost.HasValue) claim.Quantification.CostsClaimed += delayEvent.ImpactCost.Value;
            UpdateClaimQuantification(claim);
            return new LegalClaimsResult { Success = true, Message = "Delay event documented", ResultId = delayEvent.EventId, Data = delayEvent };
        }

        public LegalClaimsResult AddClaimDocument(string claimId, ClaimDocument document)
        {
            if (!_claims.TryGetValue(claimId, out var claim)) return new LegalClaimsResult { Success = false, Message = "Claim not found" };
            if (string.IsNullOrEmpty(document.DocumentId)) document.DocumentId = Guid.NewGuid().ToString();
            document.UploadDate = DateTime.UtcNow;
            claim.Documents.Add(document); claim.LastModifiedDate = DateTime.UtcNow;
            return new LegalClaimsResult { Success = true, Message = "Document added to claim", ResultId = document.DocumentId, Data = document };
        }

        private void UpdateClaimQuantification(Claim claim)
        {
            claim.Quantification.DaysClaimed = claim.Events.Where(e => e.ImpactDays.HasValue).Sum(e => e.ImpactDays.Value);
            claim.Quantification.CostsClaimed = claim.Events.Where(e => e.ImpactCost.HasValue).Sum(e => e.ImpactCost.Value);
            claim.Quantification.TotalClaimValue = claim.Quantification.CostsClaimed + (claim.Quantification.ExtendedOverhead?.TotalAmount ?? 0) + (claim.Quantification.ProductivityLoss?.TotalLoss ?? 0) + claim.Quantification.Damages.Sum(d => d.Amount);
        }

        public EOTRequest AnalyzeEOT(string projectId, string claimId, int daysRequested, DateTime originalCompletion, EOTJustification justification)
        {
            var eot = new EOTRequest { ProjectId = projectId, ClaimId = claimId, Status = EOTStatus.Requested, DaysRequested = daysRequested, OriginalCompletionDate = originalCompletion, RequestedCompletionDate = originalCompletion.AddDays(daysRequested), Justification = justification, EOTNumber = $"EOT-{DateTime.UtcNow:yyyyMMdd}-{_eotRequests.Count + 1:D3}", SubmissionDate = DateTime.UtcNow };
            if (!string.IsNullOrEmpty(claimId) && _claims.TryGetValue(claimId, out var claim))
            {
                eot.SupportingEvents = claim.Events.Where(e => e.ImpactDays.HasValue && e.ImpactDays.Value > 0).Select(e => new EOTEvent { EventId = e.EventId, Description = e.Description, EventDate = e.EventDate, ImpactDays = e.ImpactDays ?? 0, CriticalPathImpact = e.DelayType == DelayType.ExcusableCompensable ? "Critical" : "Non-Critical" }).ToList();
            }
            _eotRequests.TryAdd(eot.EOTId, eot);
            return eot;
        }

        public LegalClaimsResult RespondToEOT(string eotId, int daysGranted, string rationale, string respondedBy)
        {
            if (!_eotRequests.TryGetValue(eotId, out var eot)) return new LegalClaimsResult { Success = false, Message = "EOT request not found" };
            eot.Response = new EOTResponse { ResponseDate = DateTime.UtcNow, DaysGranted = daysGranted, Rationale = rationale, RespondedBy = respondedBy, Decision = daysGranted >= eot.DaysRequested ? "Approved" : daysGranted > 0 ? "Partially Approved" : "Denied" };
            eot.DaysApproved = daysGranted; eot.ApprovedCompletionDate = eot.OriginalCompletionDate.AddDays(daysGranted);
            eot.Status = daysGranted >= eot.DaysRequested ? EOTStatus.Approved : daysGranted > 0 ? EOTStatus.PartiallyApproved : EOTStatus.Denied;
            return new LegalClaimsResult { Success = true, Message = $"EOT response recorded: {eot.Response.Decision}", ResultId = eot.EOTId, Data = eot };
        }

        public Dispute TrackDispute(Dispute dispute)
        {
            if (dispute == null) return null;
            if (string.IsNullOrEmpty(dispute.DisputeId)) dispute.DisputeId = Guid.NewGuid().ToString();
            dispute.CreatedDate = DateTime.UtcNow; dispute.Status = DisputeStatus.Identified;
            dispute.DisputeNumber = $"DSP-{DateTime.UtcNow:yyyyMMdd}-{_disputes.Count + 1:D4}";
            _disputes.TryAdd(dispute.DisputeId, dispute);
            return dispute;
        }

        public LegalClaimsResult ResolveDispute(string disputeId, DisputeResolution resolution)
        {
            if (!_disputes.TryGetValue(disputeId, out var dispute)) return new LegalClaimsResult { Success = false, Message = "Dispute not found" };
            if (resolution == null) return new LegalClaimsResult { Success = false, Message = "Resolution cannot be null" };
            if (string.IsNullOrEmpty(resolution.ResolutionId)) resolution.ResolutionId = Guid.NewGuid().ToString();
            dispute.Resolution = resolution; dispute.Status = DisputeStatus.Resolved;
            return new LegalClaimsResult { Success = true, Message = "Dispute resolved", ResultId = resolution.ResolutionId, Data = dispute };
        }

        public DelayAnalysis PerformForensicAnalysis(string projectId, string claimId, ForensicMethod method, ScheduleData asPlanned, ScheduleData asBuilt)
        {
            var analysis = new DelayAnalysis { ProjectId = projectId, ClaimId = claimId, Method = method, AsPlannedSchedule = asPlanned, AsBuiltSchedule = asBuilt, AnalysisCutoffDate = DateTime.UtcNow };
            if (!string.IsNullOrEmpty(claimId) && _claims.TryGetValue(claimId, out var claim))
            {
                analysis.DelayEvents = claim.Events.Where(e => e.DelayType.HasValue).Select(e => new DelayEvent { EventId = e.EventId, Description = e.Description, StartDate = e.EventDate, EndDate = e.EventDate.AddDays(e.ImpactDays ?? 0), DurationDays = e.ImpactDays ?? 0, Type = e.DelayType.Value, IsCritical = e.DelayType == DelayType.ExcusableCompensable, CriticalImpactDays = e.DelayType == DelayType.ExcusableCompensable ? (e.ImpactDays ?? 0) : 0 }).ToList();
            }
            analysis.CriticalPathAnalysis = AnalyzeCriticalPath(analysis);
            analysis.Summary = GenerateDelayAnalysisSummary(analysis);
            _delayAnalyses.TryAdd(analysis.AnalysisId, analysis);
            return analysis;
        }

        private CriticalPathAnalysis AnalyzeCriticalPath(DelayAnalysis analysis)
        {
            var cpa = new CriticalPathAnalysis { OriginalDuration = analysis.AsPlannedSchedule != null ? (int)(analysis.AsPlannedSchedule.ProjectFinish - analysis.AsPlannedSchedule.ProjectStart).TotalDays : 0, ExtendedDuration = analysis.AsBuiltSchedule != null ? (int)(analysis.AsBuiltSchedule.ProjectFinish - analysis.AsBuiltSchedule.ProjectStart).TotalDays : 0 };
            cpa.TotalDelay = cpa.ExtendedDuration - cpa.OriginalDuration;
            foreach (var party in Enum.GetValues<LiabilityParty>()) cpa.DelayByResponsibility[party] = analysis.DelayEvents.Where(e => e.Responsibility == party && e.IsCritical).Sum(e => e.CriticalImpactDays);
            return cpa;
        }

        private DelayAnalysisSummary GenerateDelayAnalysisSummary(DelayAnalysis analysis)
        {
            var summary = new DelayAnalysisSummary { TotalDelayDays = analysis.DelayEvents.Sum(e => e.DurationDays), ExcusableCompensableDays = analysis.DelayEvents.Where(e => e.Type == DelayType.ExcusableCompensable).Sum(e => e.DurationDays), ExcusableNonCompensableDays = analysis.DelayEvents.Where(e => e.Type == DelayType.ExcusableNonCompensable).Sum(e => e.DurationDays), NonExcusableDays = analysis.DelayEvents.Where(e => e.Type == DelayType.NonExcusable).Sum(e => e.DurationDays), ConcurrentDays = analysis.DelayEvents.Where(e => e.Type == DelayType.Concurrent).Sum(e => e.DurationDays) };
            summary.NetEntitlementDays = summary.ExcusableCompensableDays + summary.ExcusableNonCompensableDays;
            foreach (var delayType in Enum.GetValues<DelayType>()) summary.DelayBreakdown[delayType] = analysis.DelayEvents.Where(e => e.Type == delayType).Sum(e => e.DurationDays);
            summary.Conclusion = $"Based on {analysis.Method} analysis, the contractor is entitled to {summary.NetEntitlementDays} days extension of time.";
            return summary;
        }

        public ClaimReport GenerateClaimReport(string claimId, string reportType = "Full")
        {
            if (!_claims.TryGetValue(claimId, out var claim)) return null;
            var report = new ClaimReport { ClaimId = claimId, Title = $"Claim Report: {claim.Title}", ReportType = reportType };
            report.Summary = new ClaimReportSummary { ExecutiveSummary = GenerateExecutiveSummary(claim), TotalDaysClaimed = claim.Quantification.DaysClaimed, TotalCostsClaimed = claim.Quantification.CostsClaimed, SuccessProbability = claim.LiabilityAnalysis?.SuccessProbability ?? 0.5, RecommendedAction = DetermineRecommendedAction(claim) };
            report.Sections = GenerateReportSections(claim);
            report.Exhibits = GenerateReportExhibits(claim);
            _reports.TryAdd(report.ReportId, report);
            return report;
        }

        private string GenerateExecutiveSummary(Claim claim) => $"This claim ({claim.ClaimNumber}) seeks {claim.Quantification.DaysClaimed} days extension and ${claim.Quantification.TotalClaimValue:N2} in compensation for {claim.Type} impacts. The claim is based on {claim.Details?.ClauseBasis ?? "contractual entitlement"}.";

        private string DetermineRecommendedAction(Claim claim)
        {
            if (claim.LiabilityAnalysis == null) return "Complete liability analysis before proceeding";
            if (claim.LiabilityAnalysis.SuccessProbability >= 0.7) return "Proceed with formal submission - strong entitlement basis";
            if (claim.LiabilityAnalysis.SuccessProbability >= 0.5) return "Consider negotiation - moderate success probability";
            return "Reassess claim basis - low success probability";
        }

        private List<ClaimReportSection> GenerateReportSections(Claim claim)
        {
            return new List<ClaimReportSection>
            {
                new ClaimReportSection { Title = "Introduction and Background", Order = 1, Content = $"This report presents the analysis and quantification of Claim {claim.ClaimNumber}." },
                new ClaimReportSection { Title = "Factual Background", Order = 2, Content = claim.Details?.CausalNarrative ?? "Detailed factual narrative to be provided." },
                new ClaimReportSection { Title = "Contractual Entitlement", Order = 3, Content = $"The claim is based on {claim.Details?.ClauseBasis ?? "the contract provisions"}." },
                new ClaimReportSection { Title = "Delay Analysis", Order = 4, Content = $"A total of {claim.Quantification.DaysClaimed} days delay has been identified and documented." },
                new ClaimReportSection { Title = "Quantum Analysis", Order = 5, Content = $"Total costs claimed: ${claim.Quantification.TotalClaimValue:N2}" },
                new ClaimReportSection { Title = "Conclusion and Recommendation", Order = 6, Content = DetermineRecommendedAction(claim) }
            };
        }

        private List<ClaimReportExhibit> GenerateReportExhibits(Claim claim)
        {
            var exhibits = new List<ClaimReportExhibit>();
            int exhibitNum = 1;
            foreach (var doc in claim.Documents.OrderBy(d => d.DocumentDate)) exhibits.Add(new ClaimReportExhibit { ExhibitNumber = $"A-{exhibitNum++}", Title = doc.Name, Description = doc.Description, FilePath = doc.FilePath });
            return exhibits;
        }

        public void ClearAllData() { lock (_syncLock) { _claims.Clear(); _delayAnalyses.Clear(); _eotRequests.Clear(); _disputes.Clear(); _reports.Clear(); } }
    }

    #endregion
}
