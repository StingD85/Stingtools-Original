// ============================================================================
// StingBIM Financial Intelligence Engine
// Cash flow forecasting, payment applications, retention tracking, financing
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.FinancialIntelligence
{
    #region Enums

    public enum PaymentApplicationStatus { Draft, Submitted, UnderReview, Approved, Rejected, PartiallyApproved, Paid, Disputed }
    public enum InvoiceStatus { Draft, Sent, Received, Approved, Disputed, PartiallyPaid, Paid, Overdue, Voided }
    public enum RetentionType { Standard, Milestone, Performance, Final }
    public enum RetentionStatus { Held, PartiallyReleased, Released, Forfeited, Disputed }
    public enum CashFlowCategory { ContractRevenue, ChangeOrders, Retainage, MaterialPurchases, LaborCosts, EquipmentCosts, SubcontractorPayments, Overhead, Insurance, Bonds, Taxes, Financing, Other }
    public enum CashFlowDirection { Inflow, Outflow }
    public enum FinancingType { LineOfCredit, ConstructionLoan, TermLoan, EquipmentLoan, BondFinancing, OwnerFinancing, JointVenture, Equity }
    public enum FinancingStatus { Proposed, Approved, Active, Suspended, Closed, Defaulted }
    public enum ForecastConfidence { High, Medium, Low, Uncertain }
    public enum BillingMethod { PercentComplete, UnitPrice, CostPlus, LumpSum, TimeAndMaterial }

    #endregion

    #region Data Models

    public class CashFlowForecast
    {
        public string ForecastId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public DateTime ForecastDate { get; set; } = DateTime.UtcNow;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<CashFlowPeriod> Periods { get; set; } = new List<CashFlowPeriod>();
        public CashFlowSummary Summary { get; set; }
        public List<CashFlowScenario> Scenarios { get; set; } = new List<CashFlowScenario>();
        public ForecastConfidence Confidence { get; set; }
        public List<string> Assumptions { get; set; } = new List<string>();
        public List<CashFlowRisk> Risks { get; set; } = new List<CashFlowRisk>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class CashFlowPeriod
    {
        public string PeriodId { get; set; } = Guid.NewGuid().ToString();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string PeriodLabel { get; set; }
        public List<CashFlowItem> Inflows { get; set; } = new List<CashFlowItem>();
        public List<CashFlowItem> Outflows { get; set; } = new List<CashFlowItem>();
        public decimal TotalInflows { get; set; }
        public decimal TotalOutflows { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal CumulativeCashFlow { get; set; }
    }

    public class CashFlowItem { public string ItemId { get; set; } = Guid.NewGuid().ToString(); public CashFlowCategory Category { get; set; } public CashFlowDirection Direction { get; set; } public string Description { get; set; } public decimal Amount { get; set; } public decimal Probability { get; set; } = 1.0m; public decimal ExpectedAmount => Amount * Probability; public DateTime ExpectedDate { get; set; } }
    public class CashFlowSummary { public decimal TotalProjectedInflows { get; set; } public decimal TotalProjectedOutflows { get; set; } public decimal NetProjectedCashFlow { get; set; } public decimal PeakCashRequirement { get; set; } public DateTime PeakCashDate { get; set; } public decimal AverageMonthlyCashFlow { get; set; } public decimal MinimumBalance { get; set; } public decimal MaximumBalance { get; set; } public int NegativeBalancePeriods { get; set; } public decimal FinancingRequired { get; set; } }
    public class CashFlowScenario { public string ScenarioId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public string Description { get; set; } public decimal InflowAdjustment { get; set; } public decimal OutflowAdjustment { get; set; } public CashFlowSummary ProjectedSummary { get; set; } }
    public class CashFlowRisk { public string RiskId { get; set; } = Guid.NewGuid().ToString(); public string Description { get; set; } public string Category { get; set; } public decimal PotentialImpact { get; set; } public decimal Probability { get; set; } public string Mitigation { get; set; } }

    public class PaymentApplication
    {
        public string ApplicationId { get; set; } = Guid.NewGuid().ToString();
        public string ApplicationNumber { get; set; }
        public string ProjectId { get; set; }
        public string ContractId { get; set; }
        public int PeriodNumber { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;
        public PaymentApplicationStatus Status { get; set; }
        public BillingMethod BillingMethod { get; set; }
        public G702Summary G702 { get; set; }
        public List<G703LineItem> G703Lines { get; set; } = new List<G703LineItem>();
        public PaymentApplicationApproval Approval { get; set; }
        public string PreparedBy { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class G702Summary { public decimal OriginalContractSum { get; set; } public decimal NetChangeByChangeOrders { get; set; } public decimal ContractSumToDate { get; set; } public decimal TotalCompletedAndStoredToDate { get; set; } public decimal RetainagePercent { get; set; } public decimal TotalRetainage { get; set; } public decimal TotalEarnedLessRetainage { get; set; } public decimal LessPreviousCertificatesForPayment { get; set; } public decimal CurrentPaymentDue { get; set; } public decimal BalanceToFinishIncludingRetainage { get; set; } public decimal PercentComplete { get; set; } }
    public class G703LineItem { public string LineItemId { get; set; } = Guid.NewGuid().ToString(); public string ItemNumber { get; set; } public string Description { get; set; } public decimal ScheduledValue { get; set; } public decimal PreviousApplications { get; set; } public decimal ThisPeriodWork { get; set; } public decimal MaterialsStoredToDate { get; set; } public decimal TotalCompletedAndStoredToDate { get; set; } public decimal PercentComplete { get; set; } public decimal BalanceToFinish { get; set; } public decimal Retainage { get; set; } }
    public class PaymentApplicationApproval { public string ApprovalId { get; set; } = Guid.NewGuid().ToString(); public string ApprovedBy { get; set; } public DateTime ApprovalDate { get; set; } public decimal ApprovedAmount { get; set; } public decimal AdjustedAmount { get; set; } public string Comments { get; set; } }

    public class Retention
    {
        public string RetentionId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ContractId { get; set; }
        public RetentionType Type { get; set; }
        public RetentionStatus Status { get; set; }
        public decimal RetainagePercent { get; set; }
        public decimal TotalRetained { get; set; }
        public decimal ReleasedAmount { get; set; }
        public decimal RemainingRetention { get; set; }
        public List<RetentionRelease> Releases { get; set; } = new List<RetentionRelease>();
        public RetentionTerms Terms { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class RetentionRelease { public string ReleaseId { get; set; } = Guid.NewGuid().ToString(); public decimal Amount { get; set; } public DateTime ReleaseDate { get; set; } public string Reason { get; set; } public string ApprovedBy { get; set; } public string ReferenceNumber { get; set; } }
    public class RetentionTerms { public decimal StandardRetainagePercent { get; set; } public decimal SubstantialCompletionRelease { get; set; } public decimal FinalCompletionRelease { get; set; } public int WarrantyPeriodDays { get; set; } public List<RetentionMilestone> Milestones { get; set; } = new List<RetentionMilestone>(); }
    public class RetentionMilestone { public string MilestoneId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public decimal ReleasePercent { get; set; } public string Condition { get; set; } public DateTime? TargetDate { get; set; } public bool IsComplete { get; set; } }
    public class RetentionSummary { public string ProjectId { get; set; } public decimal TotalRetained { get; set; } public decimal TotalReleased { get; set; } public decimal CurrentRetention { get; set; } public decimal PendingRelease { get; set; } public List<Retention> Retentions { get; set; } = new List<Retention>(); }

    public class Financing { public string FinancingId { get; set; } = Guid.NewGuid().ToString(); public string ProjectId { get; set; } public string Name { get; set; } public FinancingType Type { get; set; } public FinancingStatus Status { get; set; } public string LenderName { get; set; } public FinancingTerms Terms { get; set; } public FinancingBalance Balance { get; set; } public List<FinancingDraw> Draws { get; set; } = new List<FinancingDraw>(); public DateTime CreatedDate { get; set; } = DateTime.UtcNow; }
    public class FinancingTerms { public decimal CommitmentAmount { get; set; } public decimal InterestRate { get; set; } public string InterestType { get; set; } public decimal OriginationFee { get; set; } public DateTime MaturityDate { get; set; } public int TermMonths { get; set; } }
    public class FinancingBalance { public decimal CommitmentAmount { get; set; } public decimal DrawnAmount { get; set; } public decimal AvailableAmount { get; set; } public decimal OutstandingPrincipal { get; set; } public decimal AccruedInterest { get; set; } public decimal TotalOwed { get; set; } }
    public class FinancingDraw { public string DrawId { get; set; } = Guid.NewGuid().ToString(); public int DrawNumber { get; set; } public decimal Amount { get; set; } public DateTime RequestDate { get; set; } public DateTime? FundedDate { get; set; } public string Status { get; set; } public string Purpose { get; set; } }

    public class FinancingAnalysis { public string AnalysisId { get; set; } = Guid.NewGuid().ToString(); public string ProjectId { get; set; } public DateTime AnalysisDate { get; set; } = DateTime.UtcNow; public decimal TotalProjectCost { get; set; } public decimal EquityRequired { get; set; } public decimal DebtRequired { get; set; } public decimal WeightedAverageCostOfCapital { get; set; } public List<FinancingOption> Options { get; set; } = new List<FinancingOption>(); public FinancingOption RecommendedOption { get; set; } public List<string> Considerations { get; set; } = new List<string>(); }
    public class FinancingOption { public string OptionId { get; set; } = Guid.NewGuid().ToString(); public string Name { get; set; } public FinancingType Type { get; set; } public decimal Amount { get; set; } public decimal InterestRate { get; set; } public int TermMonths { get; set; } public decimal TotalInterestCost { get; set; } public decimal TotalCost { get; set; } public decimal MonthlyPayment { get; set; } public List<string> Pros { get; set; } = new List<string>(); public List<string> Cons { get; set; } = new List<string>(); public double Score { get; set; } }

    public class Invoice { public string InvoiceId { get; set; } = Guid.NewGuid().ToString(); public string InvoiceNumber { get; set; } public string ProjectId { get; set; } public string VendorId { get; set; } public string VendorName { get; set; } public InvoiceStatus Status { get; set; } public DateTime InvoiceDate { get; set; } public DateTime DueDate { get; set; } public List<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>(); public InvoiceTotals Totals { get; set; } public InvoicePaymentInfo PaymentInfo { get; set; } public DateTime CreatedDate { get; set; } = DateTime.UtcNow; }
    public class InvoiceLineItem { public string LineItemId { get; set; } = Guid.NewGuid().ToString(); public int LineNumber { get; set; } public string Description { get; set; } public decimal Quantity { get; set; } public string Unit { get; set; } public decimal UnitPrice { get; set; } public decimal Amount { get; set; } public string CostCode { get; set; } }
    public class InvoiceTotals { public decimal Subtotal { get; set; } public decimal TaxAmount { get; set; } public decimal RetainageAmount { get; set; } public decimal DiscountAmount { get; set; } public decimal TotalAmount { get; set; } public decimal AmountPaid { get; set; } public decimal AmountDue { get; set; } }
    public class InvoicePaymentInfo { public DateTime? PaymentDate { get; set; } public decimal AmountPaid { get; set; } public string PaymentMethod { get; set; } public string TransactionReference { get; set; } }

    public class FinancialResult { public bool Success { get; set; } public string Message { get; set; } public string ResultId { get; set; } public object Data { get; set; } public List<string> Warnings { get; set; } = new List<string>(); public DateTime Timestamp { get; set; } = DateTime.UtcNow; }

    #endregion

    #region Engine

    public sealed class FinancialIntelligenceEngine
    {
        private static readonly Lazy<FinancialIntelligenceEngine> _instance = new Lazy<FinancialIntelligenceEngine>(() => new FinancialIntelligenceEngine());
        public static FinancialIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, CashFlowForecast> _cashFlowForecasts;
        private readonly ConcurrentDictionary<string, PaymentApplication> _paymentApplications;
        private readonly ConcurrentDictionary<string, Retention> _retentions;
        private readonly ConcurrentDictionary<string, Financing> _financings;
        private readonly ConcurrentDictionary<string, Invoice> _invoices;
        private readonly object _syncLock = new object();

        private FinancialIntelligenceEngine()
        {
            _cashFlowForecasts = new ConcurrentDictionary<string, CashFlowForecast>();
            _paymentApplications = new ConcurrentDictionary<string, PaymentApplication>();
            _retentions = new ConcurrentDictionary<string, Retention>();
            _financings = new ConcurrentDictionary<string, Financing>();
            _invoices = new ConcurrentDictionary<string, Invoice>();
        }

        public CashFlowForecast ForecastCashFlow(string projectId, DateTime startDate, int monthsAhead, List<CashFlowItem> knownItems = null)
        {
            var forecast = new CashFlowForecast { ProjectId = projectId, Name = $"Cash Flow Forecast - {projectId}", PeriodStart = startDate, PeriodEnd = startDate.AddMonths(monthsAhead) };
            var currentDate = startDate; decimal runningBalance = 0; decimal cumulativeCashFlow = 0;
            for (int i = 0; i < monthsAhead; i++)
            {
                var periodEnd = currentDate.AddMonths(1).AddDays(-1);
                var period = new CashFlowPeriod { PeriodStart = currentDate, PeriodEnd = periodEnd, PeriodLabel = currentDate.ToString("MMM yyyy"), OpeningBalance = runningBalance };
                var periodItems = knownItems?.Where(item => item.ExpectedDate >= currentDate && item.ExpectedDate <= periodEnd).ToList() ?? GenerateDefaultCashFlowItems(currentDate, periodEnd);
                period.Inflows = periodItems.Where(i => i.Direction == CashFlowDirection.Inflow).ToList();
                period.Outflows = periodItems.Where(i => i.Direction == CashFlowDirection.Outflow).ToList();
                period.TotalInflows = period.Inflows.Sum(i => i.ExpectedAmount); period.TotalOutflows = period.Outflows.Sum(i => i.ExpectedAmount);
                period.NetCashFlow = period.TotalInflows - period.TotalOutflows; runningBalance += period.NetCashFlow;
                period.ClosingBalance = runningBalance; cumulativeCashFlow += period.NetCashFlow; period.CumulativeCashFlow = cumulativeCashFlow;
                forecast.Periods.Add(period); currentDate = currentDate.AddMonths(1);
            }
            forecast.Summary = CalculateCashFlowSummary(forecast.Periods);
            forecast.Confidence = knownItems?.Count > 10 ? ForecastConfidence.High : monthsAhead > 12 ? ForecastConfidence.Uncertain : ForecastConfidence.Medium;
            forecast.Scenarios = GenerateScenarios(forecast);
            forecast.Risks = IdentifyCashFlowRisks(forecast);
            _cashFlowForecasts.TryAdd(forecast.ForecastId, forecast);
            return forecast;
        }

        private List<CashFlowItem> GenerateDefaultCashFlowItems(DateTime periodStart, DateTime periodEnd)
        {
            return new List<CashFlowItem>
            {
                new CashFlowItem { Category = CashFlowCategory.ContractRevenue, Direction = CashFlowDirection.Inflow, Description = "Progress payment", Amount = 100000, Probability = 0.9m, ExpectedDate = periodEnd.AddDays(-15) },
                new CashFlowItem { Category = CashFlowCategory.LaborCosts, Direction = CashFlowDirection.Outflow, Description = "Labor costs", Amount = 40000, Probability = 1.0m, ExpectedDate = periodEnd },
                new CashFlowItem { Category = CashFlowCategory.MaterialPurchases, Direction = CashFlowDirection.Outflow, Description = "Material purchases", Amount = 30000, Probability = 0.95m, ExpectedDate = periodStart.AddDays(15) },
                new CashFlowItem { Category = CashFlowCategory.SubcontractorPayments, Direction = CashFlowDirection.Outflow, Description = "Subcontractor payments", Amount = 20000, Probability = 0.9m, ExpectedDate = periodEnd.AddDays(-10) }
            };
        }

        private CashFlowSummary CalculateCashFlowSummary(List<CashFlowPeriod> periods)
        {
            var summary = new CashFlowSummary { TotalProjectedInflows = periods.Sum(p => p.TotalInflows), TotalProjectedOutflows = periods.Sum(p => p.TotalOutflows), NetProjectedCashFlow = periods.Sum(p => p.NetCashFlow), AverageMonthlyCashFlow = periods.Average(p => p.NetCashFlow), MinimumBalance = periods.Min(p => p.ClosingBalance), MaximumBalance = periods.Max(p => p.ClosingBalance), NegativeBalancePeriods = periods.Count(p => p.ClosingBalance < 0) };
            var peakPeriod = periods.OrderBy(p => p.ClosingBalance).First();
            summary.PeakCashRequirement = Math.Abs(Math.Min(0, peakPeriod.ClosingBalance)); summary.PeakCashDate = peakPeriod.PeriodEnd;
            summary.FinancingRequired = summary.MinimumBalance < 0 ? Math.Abs(summary.MinimumBalance) * 1.1m : 0;
            return summary;
        }

        private List<CashFlowScenario> GenerateScenarios(CashFlowForecast forecast)
        {
            return new List<CashFlowScenario>
            {
                new CashFlowScenario { Name = "Best Case", Description = "Faster collections, reduced costs", InflowAdjustment = 1.1m, OutflowAdjustment = 0.95m, ProjectedSummary = CalculateScenarioSummary(forecast.Periods, 1.1m, 0.95m) },
                new CashFlowScenario { Name = "Worst Case", Description = "Delayed payments, cost overruns", InflowAdjustment = 0.9m, OutflowAdjustment = 1.15m, ProjectedSummary = CalculateScenarioSummary(forecast.Periods, 0.9m, 1.15m) },
                new CashFlowScenario { Name = "Most Likely", Description = "Expected scenario with minor variations", InflowAdjustment = 0.98m, OutflowAdjustment = 1.02m, ProjectedSummary = CalculateScenarioSummary(forecast.Periods, 0.98m, 1.02m) }
            };
        }

        private CashFlowSummary CalculateScenarioSummary(List<CashFlowPeriod> basePeriods, decimal inflowAdj, decimal outflowAdj) =>
            CalculateCashFlowSummary(basePeriods.Select(p => new CashFlowPeriod { TotalInflows = p.TotalInflows * inflowAdj, TotalOutflows = p.TotalOutflows * outflowAdj, NetCashFlow = (p.TotalInflows * inflowAdj) - (p.TotalOutflows * outflowAdj), ClosingBalance = p.OpeningBalance + ((p.TotalInflows * inflowAdj) - (p.TotalOutflows * outflowAdj)) }).ToList());

        private List<CashFlowRisk> IdentifyCashFlowRisks(CashFlowForecast forecast)
        {
            var risks = new List<CashFlowRisk>();
            if (forecast.Summary.NegativeBalancePeriods > 0) risks.Add(new CashFlowRisk { Description = $"Negative cash balance projected for {forecast.Summary.NegativeBalancePeriods} period(s)", Category = "Liquidity", PotentialImpact = forecast.Summary.PeakCashRequirement, Probability = 0.7m, Mitigation = "Secure line of credit or adjust payment timing" });
            if (forecast.Summary.FinancingRequired > 0) risks.Add(new CashFlowRisk { Description = "External financing will be required", Category = "Financing", PotentialImpact = forecast.Summary.FinancingRequired * 0.08m, Probability = 0.9m, Mitigation = "Arrange financing early to secure favorable terms" });
            return risks;
        }

        public PaymentApplication CreatePaymentApplication(string projectId, string contractId, int periodNumber, DateTime periodFrom, DateTime periodTo)
        {
            var application = new PaymentApplication { ProjectId = projectId, ContractId = contractId, PeriodNumber = periodNumber, PeriodFrom = periodFrom, PeriodTo = periodTo, Status = PaymentApplicationStatus.Draft, ApplicationNumber = $"PAY-{projectId.Substring(0, Math.Min(4, projectId.Length)).ToUpper()}-{periodNumber:D3}", G702 = new G702Summary(), G703Lines = new List<G703LineItem>() };
            _paymentApplications.TryAdd(application.ApplicationId, application);
            return application;
        }

        public FinancialResult AddG703LineItem(string applicationId, G703LineItem lineItem)
        {
            if (!_paymentApplications.TryGetValue(applicationId, out var application)) return new FinancialResult { Success = false, Message = "Application not found" };
            if (string.IsNullOrEmpty(lineItem.LineItemId)) lineItem.LineItemId = Guid.NewGuid().ToString();
            lineItem.TotalCompletedAndStoredToDate = lineItem.PreviousApplications + lineItem.ThisPeriodWork + lineItem.MaterialsStoredToDate;
            lineItem.PercentComplete = lineItem.ScheduledValue > 0 ? (lineItem.TotalCompletedAndStoredToDate / lineItem.ScheduledValue) * 100 : 0;
            lineItem.BalanceToFinish = lineItem.ScheduledValue - lineItem.TotalCompletedAndStoredToDate;
            application.G703Lines.Add(lineItem); UpdateG702Summary(application);
            return new FinancialResult { Success = true, Message = "Line item added", ResultId = lineItem.LineItemId, Data = lineItem };
        }

        private void UpdateG702Summary(PaymentApplication application)
        {
            var g702 = application.G702; var lines = application.G703Lines;
            g702.ContractSumToDate = g702.OriginalContractSum + g702.NetChangeByChangeOrders;
            g702.TotalCompletedAndStoredToDate = lines.Sum(l => l.TotalCompletedAndStoredToDate);
            g702.TotalRetainage = g702.TotalCompletedAndStoredToDate * (g702.RetainagePercent / 100);
            g702.TotalEarnedLessRetainage = g702.TotalCompletedAndStoredToDate - g702.TotalRetainage;
            g702.CurrentPaymentDue = g702.TotalEarnedLessRetainage - g702.LessPreviousCertificatesForPayment;
            g702.BalanceToFinishIncludingRetainage = g702.ContractSumToDate - g702.TotalCompletedAndStoredToDate;
            g702.PercentComplete = g702.ContractSumToDate > 0 ? (g702.TotalCompletedAndStoredToDate / g702.ContractSumToDate) * 100 : 0;
        }

        public FinancialResult SubmitPaymentApplication(string applicationId)
        {
            if (!_paymentApplications.TryGetValue(applicationId, out var application)) return new FinancialResult { Success = false, Message = "Application not found" };
            if (application.Status != PaymentApplicationStatus.Draft) return new FinancialResult { Success = false, Message = "Application is not in draft status" };
            application.Status = PaymentApplicationStatus.Submitted; application.ApplicationDate = DateTime.UtcNow;
            return new FinancialResult { Success = true, Message = $"Payment application {application.ApplicationNumber} submitted", ResultId = applicationId, Data = application };
        }

        public FinancialResult ApprovePaymentApplication(string applicationId, string approverName, decimal? adjustedAmount = null, string comments = null)
        {
            if (!_paymentApplications.TryGetValue(applicationId, out var application)) return new FinancialResult { Success = false, Message = "Application not found" };
            application.Status = PaymentApplicationStatus.Approved; application.ApprovedBy = approverName; application.ApprovalDate = DateTime.UtcNow;
            application.Approval = new PaymentApplicationApproval { ApprovedBy = approverName, ApprovalDate = DateTime.UtcNow, ApprovedAmount = adjustedAmount ?? application.G702.CurrentPaymentDue, AdjustedAmount = adjustedAmount ?? application.G702.CurrentPaymentDue, Comments = comments };
            return new FinancialResult { Success = true, Message = $"Payment application approved for ${application.Approval.ApprovedAmount:N2}", ResultId = applicationId, Data = application };
        }

        public Retention CreateRetention(string projectId, string contractId, decimal retainagePercent, RetentionTerms terms)
        {
            var retention = new Retention { ProjectId = projectId, ContractId = contractId, Type = RetentionType.Standard, Status = RetentionStatus.Held, RetainagePercent = retainagePercent, Terms = terms ?? new RetentionTerms { StandardRetainagePercent = retainagePercent } };
            _retentions.TryAdd(retention.RetentionId, retention);
            return retention;
        }

        public FinancialResult TrackRetention(string retentionId, decimal billedAmount)
        {
            if (!_retentions.TryGetValue(retentionId, out var retention)) return new FinancialResult { Success = false, Message = "Retention not found" };
            var retainedAmount = billedAmount * (retention.RetainagePercent / 100);
            retention.TotalRetained += retainedAmount; retention.RemainingRetention = retention.TotalRetained - retention.ReleasedAmount;
            return new FinancialResult { Success = true, Message = $"Retention updated: ${retainedAmount:N2} added", ResultId = retentionId, Data = retention };
        }

        public FinancialResult ReleaseRetention(string retentionId, decimal amount, string reason, string approvedBy)
        {
            if (!_retentions.TryGetValue(retentionId, out var retention)) return new FinancialResult { Success = false, Message = "Retention not found" };
            if (amount > retention.RemainingRetention) return new FinancialResult { Success = false, Message = "Release amount exceeds remaining retention" };
            var release = new RetentionRelease { Amount = amount, ReleaseDate = DateTime.UtcNow, Reason = reason, ApprovedBy = approvedBy, ReferenceNumber = $"RET-REL-{DateTime.UtcNow:yyyyMMdd}-{retention.Releases.Count + 1:D3}" };
            retention.Releases.Add(release); retention.ReleasedAmount += amount; retention.RemainingRetention = retention.TotalRetained - retention.ReleasedAmount;
            if (retention.RemainingRetention <= 0) retention.Status = RetentionStatus.Released; else if (retention.ReleasedAmount > 0) retention.Status = RetentionStatus.PartiallyReleased;
            return new FinancialResult { Success = true, Message = $"Retention of ${amount:N2} released", ResultId = release.ReleaseId, Data = release };
        }

        public RetentionSummary GetRetentionSummary(string projectId)
        {
            var retentions = _retentions.Values.Where(r => r.ProjectId == projectId).ToList();
            return new RetentionSummary { ProjectId = projectId, TotalRetained = retentions.Sum(r => r.TotalRetained), TotalReleased = retentions.Sum(r => r.ReleasedAmount), CurrentRetention = retentions.Sum(r => r.RemainingRetention), PendingRelease = retentions.Where(r => r.Status == RetentionStatus.Held || r.Status == RetentionStatus.PartiallyReleased).Sum(r => r.RemainingRetention), Retentions = retentions };
        }

        public FinancingAnalysis AnalyzeFinancing(string projectId, decimal totalProjectCost, decimal equityAvailable)
        {
            var analysis = new FinancingAnalysis { ProjectId = projectId, TotalProjectCost = totalProjectCost, EquityRequired = equityAvailable, DebtRequired = totalProjectCost - equityAvailable };
            analysis.Options = GenerateFinancingOptions(analysis.DebtRequired);
            analysis.RecommendedOption = analysis.Options.OrderByDescending(o => o.Score).FirstOrDefault();
            analysis.WeightedAverageCostOfCapital = CalculateWACC(analysis);
            analysis.Considerations = GenerateFinancingConsiderations(analysis);
            return analysis;
        }

        private List<FinancingOption> GenerateFinancingOptions(decimal debtRequired)
        {
            return new List<FinancingOption>
            {
                new FinancingOption { Name = "Construction Loan", Type = FinancingType.ConstructionLoan, Amount = debtRequired, InterestRate = 7.5m, TermMonths = 24, TotalInterestCost = debtRequired * 0.075m * 2, MonthlyPayment = 0, Pros = new List<string> { "Interest-only during construction", "Draw schedule flexibility" }, Cons = new List<string> { "Requires takeout financing", "Higher rates" }, Score = 85 },
                new FinancingOption { Name = "Line of Credit", Type = FinancingType.LineOfCredit, Amount = debtRequired, InterestRate = 8.0m, TermMonths = 12, TotalInterestCost = debtRequired * 0.08m, MonthlyPayment = 0, Pros = new List<string> { "Flexible draws", "Pay interest only on used amount" }, Cons = new List<string> { "Annual renewal required", "Variable rate risk" }, Score = 75 },
                new FinancingOption { Name = "Term Loan", Type = FinancingType.TermLoan, Amount = debtRequired, InterestRate = 6.5m, TermMonths = 60, TotalInterestCost = debtRequired * 0.065m * 5 * 0.5m, MonthlyPayment = CalculateMonthlyPayment(debtRequired, 6.5m, 60), Pros = new List<string> { "Fixed rate stability", "Longer term" }, Cons = new List<string> { "Less flexibility", "Immediate principal payments" }, Score = 70 }
            };
        }

        private decimal CalculateMonthlyPayment(decimal principal, decimal annualRate, int months) { var monthlyRate = annualRate / 100 / 12; if (monthlyRate == 0) return principal / months; return principal * (monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), months)) / ((decimal)Math.Pow((double)(1 + monthlyRate), months) - 1); }
        private decimal CalculateWACC(FinancingAnalysis analysis) { var equityWeight = analysis.EquityRequired / analysis.TotalProjectCost; var debtWeight = analysis.DebtRequired / analysis.TotalProjectCost; var equityCost = 0.15m; var debtCost = analysis.RecommendedOption?.InterestRate / 100 ?? 0.07m; return (equityWeight * equityCost) + (debtWeight * debtCost * 0.79m); }
        private List<string> GenerateFinancingConsiderations(FinancingAnalysis analysis) { var considerations = new List<string>(); if (analysis.DebtRequired > analysis.TotalProjectCost * 0.8m) considerations.Add("High leverage ratio - consider additional equity"); considerations.Add($"WACC: {analysis.WeightedAverageCostOfCapital:P2}"); return considerations; }

        public FinancialResult CreateInvoice(Invoice invoice) { if (invoice == null) return new FinancialResult { Success = false, Message = "Invoice cannot be null" }; if (string.IsNullOrEmpty(invoice.InvoiceId)) invoice.InvoiceId = Guid.NewGuid().ToString(); invoice.CreatedDate = DateTime.UtcNow; invoice.Status = InvoiceStatus.Draft; invoice.Totals ??= new InvoiceTotals(); CalculateInvoiceTotals(invoice); if (_invoices.TryAdd(invoice.InvoiceId, invoice)) return new FinancialResult { Success = true, Message = "Invoice created", ResultId = invoice.InvoiceId, Data = invoice }; return new FinancialResult { Success = false, Message = "Failed to create invoice" }; }
        public FinancialResult ManageInvoices(string invoiceId, string action, object actionData = null) { if (!_invoices.TryGetValue(invoiceId, out var invoice)) return new FinancialResult { Success = false, Message = "Invoice not found" }; switch (action.ToLower()) { case "approve": invoice.Status = InvoiceStatus.Approved; break; case "pay": if (actionData is InvoicePaymentInfo payment) { invoice.PaymentInfo = payment; invoice.Totals.AmountPaid += payment.AmountPaid; invoice.Totals.AmountDue = invoice.Totals.TotalAmount - invoice.Totals.AmountPaid; invoice.Status = invoice.Totals.AmountDue <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid; } break; case "void": invoice.Status = InvoiceStatus.Voided; break; } return new FinancialResult { Success = true, Message = $"Invoice {action} completed", ResultId = invoiceId, Data = invoice }; }
        private void CalculateInvoiceTotals(Invoice invoice) { invoice.Totals.Subtotal = invoice.LineItems.Sum(l => l.Amount); invoice.Totals.TotalAmount = invoice.Totals.Subtotal + invoice.Totals.TaxAmount - invoice.Totals.DiscountAmount - invoice.Totals.RetainageAmount; invoice.Totals.AmountDue = invoice.Totals.TotalAmount - invoice.Totals.AmountPaid; }
        public List<Invoice> GetOverdueInvoices(string projectId) => _invoices.Values.Where(i => i.ProjectId == projectId && i.DueDate < DateTime.UtcNow && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Voided).OrderBy(i => i.DueDate).ToList();

        public void ClearAllData() { lock (_syncLock) { _cashFlowForecasts.Clear(); _paymentApplications.Clear(); _retentions.Clear(); _financings.Clear(); _invoices.Clear(); } }
    }

    #endregion
}
