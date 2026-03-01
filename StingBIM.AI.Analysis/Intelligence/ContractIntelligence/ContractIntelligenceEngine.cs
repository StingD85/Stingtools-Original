// ===================================================================
// StingBIM Contract Intelligence Engine
// Contract clause analysis, risk allocation, obligation tracking
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ContractIntelligence
{
    #region Enums

    public enum ContractType { AIA, EJCDC, ConsensusDocs, FIDIC, NEC, Custom }
    public enum ClauseCategory { Payment, Time, Changes, Claims, Termination, Insurance, Indemnity, Warranty, Dispute }
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum ObligationStatus { Pending, InProgress, Completed, Overdue, Waived }
    public enum PartyType { Owner, Contractor, Subcontractor, Architect, Engineer, Consultant }

    #endregion

    #region Data Models

    public class ContractProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<Contract> Contracts { get; set; } = new();
        public List<ContractObligation> Obligations { get; set; } = new();
        public List<RiskItem> Risks { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Contract
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ContractNumber { get; set; }
        public string Title { get; set; }
        public ContractType Type { get; set; }
        public string Version { get; set; }
        public PartyType Party1Type { get; set; }
        public string Party1Name { get; set; }
        public PartyType Party2Type { get; set; }
        public string Party2Name { get; set; }
        public decimal ContractSum { get; set; }
        public DateTime ExecutionDate { get; set; }
        public DateTime CommencementDate { get; set; }
        public DateTime SubstantialCompletionDate { get; set; }
        public int LiquidatedDamagesPerDay { get; set; }
        public List<ContractClause> Clauses { get; set; } = new();
        public List<Amendment> Amendments { get; set; } = new();
        public ContractAnalysis Analysis { get; set; }
    }

    public class ContractClause
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ArticleNumber { get; set; }
        public string Title { get; set; }
        public ClauseCategory Category { get; set; }
        public string Text { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public List<string> KeyTerms { get; set; } = new();
        public List<string> Obligations { get; set; } = new();
        public string RiskAnalysis { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class Amendment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int AmendmentNumber { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public decimal SumChange { get; set; }
        public int TimeChange { get; set; }
        public List<string> AffectedClauses { get; set; } = new();
    }

    public class ContractObligation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ContractId { get; set; }
        public string ClauseReference { get; set; }
        public string Description { get; set; }
        public PartyType ResponsibleParty { get; set; }
        public DateTime DueDate { get; set; }
        public ObligationStatus Status { get; set; } = ObligationStatus.Pending;
        public int DaysNoticeRequired { get; set; }
        public bool IsRecurring { get; set; }
        public int RecurrenceIntervalDays { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public decimal PenaltyAmount { get; set; }
    }

    public class RiskItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ContractId { get; set; }
        public string ClauseReference { get; set; }
        public string Description { get; set; }
        public RiskLevel Level { get; set; }
        public PartyType RiskBearer { get; set; }
        public decimal PotentialExposure { get; set; }
        public double Probability { get; set; }
        public decimal ExpectedValue => PotentialExposure * (decimal)Probability;
        public List<string> Mitigations { get; set; } = new();
        public bool IsInsurable { get; set; }
    }

    public class ContractAnalysis
    {
        public string ContractId { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public int TotalClauses { get; set; }
        public int HighRiskClauses { get; set; }
        public decimal TotalRiskExposure { get; set; }
        public List<string> KeyConcerns { get; set; } = new();
        public List<string> NegotiationPriorities { get; set; } = new();
        public Dictionary<ClauseCategory, RiskLevel> CategoryRisks { get; set; } = new();
        public double OverallRiskScore { get; set; }
    }

    public class ComparisonResult
    {
        public string BaseContractType { get; set; }
        public string ComparedContractType { get; set; }
        public List<ClauseDifference> Differences { get; set; } = new();
        public List<string> MissingClauses { get; set; } = new();
        public List<string> AdditionalClauses { get; set; } = new();
    }

    public class ClauseDifference
    {
        public string ClauseTitle { get; set; }
        public string BaseVersion { get; set; }
        public string ComparedVersion { get; set; }
        public string Impact { get; set; }
        public RiskLevel RiskChange { get; set; }
    }

    public class NoticeRequirement
    {
        public string TriggerEvent { get; set; }
        public int DaysRequired { get; set; }
        public string Method { get; set; }
        public string Recipient { get; set; }
        public string ConsequenceOfFailure { get; set; }
    }

    #endregion

    public sealed class ContractIntelligenceEngine
    {
        private static readonly Lazy<ContractIntelligenceEngine> _instance =
            new Lazy<ContractIntelligenceEngine>(() => new ContractIntelligenceEngine());
        public static ContractIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ContractProject> _projects = new();
        private readonly Dictionary<ContractType, List<string>> _standardClauses = new();
        private readonly object _lock = new object();

        private ContractIntelligenceEngine()
        {
            InitializeStandardClauses();
        }

        private void InitializeStandardClauses()
        {
            _standardClauses[ContractType.AIA] = new List<string>
            {
                "A101 - Owner-Contractor Agreement",
                "A201 - General Conditions",
                "Article 3 - Contractor",
                "Article 4 - Architect",
                "Article 5 - Subcontractors",
                "Article 7 - Changes in the Work",
                "Article 8 - Time",
                "Article 9 - Payments and Completion",
                "Article 11 - Insurance and Bonds",
                "Article 15 - Claims and Disputes"
            };

            _standardClauses[ContractType.ConsensusDocs] = new List<string>
            {
                "Article 3 - General Provisions",
                "Article 4 - Constructor's Responsibilities",
                "Article 5 - Subcontracts",
                "Article 6 - Time",
                "Article 7 - Contract Price",
                "Article 8 - Changes",
                "Article 9 - Payment",
                "Article 10 - Indemnity and Insurance",
                "Article 11 - Claims Mitigation",
                "Article 12 - Dispute Resolution"
            };
        }

        public ContractProject CreateContractProject(string projectId, string projectName)
        {
            var project = new ContractProject { ProjectId = projectId, ProjectName = projectName };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public Contract CreateContract(string projectId, string title, ContractType type,
            PartyType party1Type, string party1Name, PartyType party2Type, string party2Name,
            decimal contractSum, DateTime commencementDate, DateTime completionDate)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var contract = new Contract
                {
                    ContractNumber = $"CTR-{project.Contracts.Count + 1:D3}",
                    Title = title,
                    Type = type,
                    Party1Type = party1Type,
                    Party1Name = party1Name,
                    Party2Type = party2Type,
                    Party2Name = party2Name,
                    ContractSum = contractSum,
                    CommencementDate = commencementDate,
                    SubstantialCompletionDate = completionDate,
                    Clauses = GenerateStandardClauses(type)
                };

                project.Contracts.Add(contract);
                return contract;
            }
        }

        private List<ContractClause> GenerateStandardClauses(ContractType type)
        {
            var clauses = new List<ContractClause>
            {
                new ContractClause
                {
                    ArticleNumber = "7.1",
                    Title = "Changes in the Work",
                    Category = ClauseCategory.Changes,
                    RiskLevel = RiskLevel.High,
                    KeyTerms = new List<string> { "change order", "written authorization", "adjustment" },
                    Obligations = new List<string> { "Submit change order requests within 21 days", "Document cost impact" }
                },
                new ContractClause
                {
                    ArticleNumber = "8.3",
                    Title = "Delays and Extensions of Time",
                    Category = ClauseCategory.Time,
                    RiskLevel = RiskLevel.High,
                    KeyTerms = new List<string> { "excusable delay", "force majeure", "notice requirement" },
                    Obligations = new List<string> { "Provide written notice within 21 days of delay" }
                },
                new ContractClause
                {
                    ArticleNumber = "9.3",
                    Title = "Applications for Payment",
                    Category = ClauseCategory.Payment,
                    RiskLevel = RiskLevel.Medium,
                    KeyTerms = new List<string> { "progress payment", "retainage", "schedule of values" },
                    Obligations = new List<string> { "Submit monthly payment applications", "Include lien waivers" }
                },
                new ContractClause
                {
                    ArticleNumber = "11.1",
                    Title = "Indemnification",
                    Category = ClauseCategory.Indemnity,
                    RiskLevel = RiskLevel.Critical,
                    KeyTerms = new List<string> { "hold harmless", "defend", "negligence" },
                    RiskAnalysis = "Broad indemnification may exceed insurance coverage"
                },
                new ContractClause
                {
                    ArticleNumber = "15.1",
                    Title = "Claims and Disputes",
                    Category = ClauseCategory.Dispute,
                    RiskLevel = RiskLevel.Medium,
                    KeyTerms = new List<string> { "mediation", "arbitration", "litigation" },
                    Obligations = new List<string> { "Submit claim within 21 days", "Continue performance" }
                }
            };

            return clauses;
        }

        public async Task<ContractAnalysis> AnalyzeContract(string projectId, string contractId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var contract = project.Contracts.FirstOrDefault(c => c.Id == contractId);
                    if (contract == null) return null;

                    var analysis = new ContractAnalysis
                    {
                        ContractId = contractId,
                        TotalClauses = contract.Clauses.Count,
                        HighRiskClauses = contract.Clauses.Count(c => c.RiskLevel == RiskLevel.High || c.RiskLevel == RiskLevel.Critical)
                    };

                    // Analyze by category
                    foreach (var category in Enum.GetValues<ClauseCategory>())
                    {
                        var categoryClauses = contract.Clauses.Where(c => c.Category == category).ToList();
                        if (categoryClauses.Any())
                        {
                            var maxRisk = categoryClauses.Max(c => c.RiskLevel);
                            analysis.CategoryRisks[category] = maxRisk;
                        }
                    }

                    // Identify key concerns
                    var criticalClauses = contract.Clauses.Where(c => c.RiskLevel == RiskLevel.Critical).ToList();
                    foreach (var clause in criticalClauses)
                    {
                        analysis.KeyConcerns.Add($"{clause.ArticleNumber} {clause.Title}: {clause.RiskAnalysis ?? "Requires review"}");
                    }

                    // Calculate risk score
                    analysis.OverallRiskScore = contract.Clauses.Average(c => (int)c.RiskLevel) / 3.0 * 100;

                    // Generate negotiation priorities
                    if (analysis.CategoryRisks.GetValueOrDefault(ClauseCategory.Indemnity) >= RiskLevel.High)
                        analysis.NegotiationPriorities.Add("Negotiate mutual indemnification with carve-outs");
                    if (analysis.CategoryRisks.GetValueOrDefault(ClauseCategory.Time) >= RiskLevel.High)
                        analysis.NegotiationPriorities.Add("Clarify excusable delay provisions");

                    contract.Analysis = analysis;
                    return analysis;
                }
            });
        }

        public ContractObligation TrackObligation(string projectId, string contractId, string clauseReference,
            string description, PartyType responsibleParty, DateTime dueDate, int noticeRequired = 0)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var obligation = new ContractObligation
                {
                    ContractId = contractId,
                    ClauseReference = clauseReference,
                    Description = description,
                    ResponsibleParty = responsibleParty,
                    DueDate = dueDate,
                    DaysNoticeRequired = noticeRequired
                };

                project.Obligations.Add(obligation);
                return obligation;
            }
        }

        public List<ContractObligation> GetUpcomingObligations(string projectId, int daysAhead = 30)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<ContractObligation>();

                var cutoff = DateTime.UtcNow.AddDays(daysAhead);
                return project.Obligations
                    .Where(o => o.DueDate <= cutoff && o.Status == ObligationStatus.Pending)
                    .OrderBy(o => o.DueDate)
                    .ToList();
            }
        }

        public RiskItem IdentifyRisk(string projectId, string contractId, string clauseReference,
            string description, RiskLevel level, PartyType riskBearer, decimal potentialExposure, double probability)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var risk = new RiskItem
                {
                    ContractId = contractId,
                    ClauseReference = clauseReference,
                    Description = description,
                    Level = level,
                    RiskBearer = riskBearer,
                    PotentialExposure = potentialExposure,
                    Probability = probability
                };

                project.Risks.Add(risk);
                return risk;
            }
        }

        public List<NoticeRequirement> ExtractNoticeRequirements(string projectId, string contractId)
        {
            var notices = new List<NoticeRequirement>
            {
                new NoticeRequirement
                {
                    TriggerEvent = "Delay Event",
                    DaysRequired = 21,
                    Method = "Written notice",
                    Recipient = "Owner/Architect",
                    ConsequenceOfFailure = "Waiver of time extension claim"
                },
                new NoticeRequirement
                {
                    TriggerEvent = "Changed Conditions",
                    DaysRequired = 14,
                    Method = "Written notice before disturbing",
                    Recipient = "Owner",
                    ConsequenceOfFailure = "Waiver of additional compensation"
                },
                new NoticeRequirement
                {
                    TriggerEvent = "Claim for Additional Cost",
                    DaysRequired = 21,
                    Method = "Written notice with documentation",
                    Recipient = "Architect",
                    ConsequenceOfFailure = "Claim may be barred"
                },
                new NoticeRequirement
                {
                    TriggerEvent = "Intent to Terminate",
                    DaysRequired = 7,
                    Method = "Written notice",
                    Recipient = "Other Party",
                    ConsequenceOfFailure = "Termination may be wrongful"
                }
            };

            return notices;
        }

        public decimal CalculateTotalRiskExposure(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return 0;

                return project.Risks.Sum(r => r.ExpectedValue);
            }
        }
    }
}
