// ===================================================================
// StingBIM Business Development Intelligence Engine
// Lead tracking, pipeline management, proposal optimization
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.BusinessDevelopmentIntelligence
{
    #region Enums

    public enum LeadSource { Referral, RFP, Website, NetworkEvent, ColdOutreach, Repeat, Partnership }
    public enum LeadStage { Prospect, Qualification, Proposal, Negotiation, Closed_Won, Closed_Lost }
    public enum ProposalStatus { Draft, Review, Submitted, Shortlisted, Awarded, NotAwarded }
    public enum ClientType { Government, Private, Developer, Owner_Occupied, Institutional }
    public enum PursuitDecision { Pursue, NoPursue, Conditional, Pending }

    #endregion

    #region Data Models

    public class BusinessDevelopmentProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CompanyName { get; set; }
        public List<Lead> Leads { get; set; } = new();
        public List<Client> Clients { get; set; } = new();
        public Pipeline Pipeline { get; set; }
        public List<Proposal> Proposals { get; set; } = new();
        public BDMetrics Metrics { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class Lead
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public LeadSource Source { get; set; }
        public LeadStage Stage { get; set; }
        public double EstimatedValue { get; set; }
        public double WinProbability { get; set; }
        public double WeightedValue => EstimatedValue * WinProbability;
        public DateTime IdentifiedDate { get; set; }
        public DateTime? ProposalDueDate { get; set; }
        public DateTime? DecisionDate { get; set; }
        public PursuitDecision GoNoGo { get; set; }
        public GoNoGoAssessment Assessment { get; set; }
        public List<Activity> Activities { get; set; } = new();
        public string AssignedBDManager { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class GoNoGoAssessment
    {
        public double StrategicFit { get; set; }
        public double ClientRelationship { get; set; }
        public double TechnicalCapability { get; set; }
        public double CompetitivePosition { get; set; }
        public double ResourceAvailability { get; set; }
        public double ProfitPotential { get; set; }
        public double RiskLevel { get; set; }
        public double OverallScore { get; set; }
        public string Recommendation { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Concerns { get; set; } = new();
    }

    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string PerformedBy { get; set; }
        public string Outcome { get; set; }
        public string NextStep { get; set; }
    }

    public class Client
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ClientType Type { get; set; }
        public string Industry { get; set; }
        public double AnnualConstructionSpend { get; set; }
        public List<string> PrimaryContacts { get; set; } = new();
        public double LifetimeValue { get; set; }
        public int ProjectsCompleted { get; set; }
        public int ActiveProjects { get; set; }
        public double SatisfactionScore { get; set; }
        public double RelationshipStrength { get; set; }
        public List<string> PreferredProjectTypes { get; set; } = new();
        public DateTime LastProjectDate { get; set; }
        public string AccountManager { get; set; }
    }

    public class Pipeline
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double TotalValue { get; set; }
        public double WeightedValue { get; set; }
        public Dictionary<LeadStage, StageMetrics> StageBreakdown { get; set; } = new();
        public double ConversionRate { get; set; }
        public double AverageDealSize { get; set; }
        public int AverageSalesCycle { get; set; }
        public List<PipelineHealthIndicator> HealthIndicators { get; set; } = new();
    }

    public class StageMetrics
    {
        public LeadStage Stage { get; set; }
        public int Count { get; set; }
        public double TotalValue { get; set; }
        public double WeightedValue { get; set; }
        public double AverageAge { get; set; }
        public double ConversionToNext { get; set; }
    }

    public class PipelineHealthIndicator
    {
        public string Indicator { get; set; }
        public string Status { get; set; }
        public double Value { get; set; }
        public double Target { get; set; }
        public string Action { get; set; }
    }

    public class Proposal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LeadId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public ProposalStatus Status { get; set; }
        public double ProposedValue { get; set; }
        public double ProposedMargin { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public List<ProposalSection> Sections { get; set; } = new();
        public List<string> TeamMembers { get; set; } = new();
        public ProposalScore Score { get; set; }
        public List<string> Differentiators { get; set; } = new();
    }

    public class ProposalSection
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Owner { get; set; }
        public DateTime DueDate { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class ProposalScore
    {
        public double TechnicalScore { get; set; }
        public double ManagementScore { get; set; }
        public double PriceScore { get; set; }
        public double OverallScore { get; set; }
        public List<string> StrengthAreas { get; set; } = new();
        public List<string> ImprovementAreas { get; set; } = new();
    }

    public class BDMetrics
    {
        public int TotalLeads { get; set; }
        public int ActiveLeads { get; set; }
        public double WinRate { get; set; }
        public double PipelineValue { get; set; }
        public double WeightedPipeline { get; set; }
        public double AverageProposalValue { get; set; }
        public int ProposalsSubmitted { get; set; }
        public int ProposalsWon { get; set; }
        public double ProposalWinRate { get; set; }
        public double CaptureRatio { get; set; }
        public int NewClientsYTD { get; set; }
        public double RepeatClientPercentage { get; set; }
        public double AverageSalesCycleDays { get; set; }
    }

    public class TargetClient
    {
        public string ClientName { get; set; }
        public ClientType Type { get; set; }
        public double EstimatedAnnualSpend { get; set; }
        public double FitScore { get; set; }
        public double AccessibilityScore { get; set; }
        public string CurrentContractor { get; set; }
        public string EntryStrategy { get; set; }
        public List<string> KeyContacts { get; set; } = new();
    }

    #endregion

    public sealed class BusinessDevelopmentIntelligenceEngine
    {
        private static readonly Lazy<BusinessDevelopmentIntelligenceEngine> _instance =
            new Lazy<BusinessDevelopmentIntelligenceEngine>(() => new BusinessDevelopmentIntelligenceEngine());
        public static BusinessDevelopmentIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, BusinessDevelopmentProject> _projects = new();
        private readonly object _lock = new object();

        private BusinessDevelopmentIntelligenceEngine() { }

        public BusinessDevelopmentProject CreateProject(string companyName)
        {
            var project = new BusinessDevelopmentProject
            {
                CompanyName = companyName,
                Pipeline = new Pipeline(),
                Metrics = new BDMetrics()
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public Lead AddLead(string projectId, string projectName, string clientName,
            LeadSource source, double estimatedValue, DateTime? proposalDue = null)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var lead = new Lead
                {
                    ProjectName = projectName,
                    ClientName = clientName,
                    Source = source,
                    EstimatedValue = estimatedValue,
                    Stage = LeadStage.Prospect,
                    IdentifiedDate = DateTime.UtcNow,
                    ProposalDueDate = proposalDue,
                    WinProbability = 0.1,
                    GoNoGo = PursuitDecision.Pending
                };

                project.Leads.Add(lead);
                return lead;
            }
        }

        public GoNoGoAssessment AssessGoNoGo(string projectId, string leadId,
            double strategicFit, double relationship, double capability,
            double competitive, double resources, double profit, double risk)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var lead = project.Leads.FirstOrDefault(l => l.Id == leadId);
                if (lead == null) return null;

                var assessment = new GoNoGoAssessment
                {
                    StrategicFit = strategicFit,
                    ClientRelationship = relationship,
                    TechnicalCapability = capability,
                    CompetitivePosition = competitive,
                    ResourceAvailability = resources,
                    ProfitPotential = profit,
                    RiskLevel = risk
                };

                assessment.OverallScore = (strategicFit * 0.15 + relationship * 0.2 +
                    capability * 0.2 + competitive * 0.15 + resources * 0.1 +
                    profit * 0.15 + (1 - risk) * 0.05);

                if (strategicFit >= 0.7) assessment.Strengths.Add("Strong strategic fit");
                if (relationship >= 0.7) assessment.Strengths.Add("Existing client relationship");
                if (capability >= 0.8) assessment.Strengths.Add("Strong technical capability");

                if (competitive < 0.5) assessment.Concerns.Add("Weak competitive position");
                if (resources < 0.5) assessment.Concerns.Add("Resource constraints");
                if (risk > 0.6) assessment.Concerns.Add("High risk profile");

                assessment.Recommendation = assessment.OverallScore >= 0.7 ? "Pursue" :
                    assessment.OverallScore >= 0.5 ? "Pursue with conditions" : "No-Go";

                lead.Assessment = assessment;
                lead.GoNoGo = assessment.OverallScore >= 0.7 ? PursuitDecision.Pursue :
                    assessment.OverallScore >= 0.5 ? PursuitDecision.Conditional : PursuitDecision.NoPursue;

                return assessment;
            }
        }

        public void AdvanceLeadStage(string projectId, string leadId, LeadStage newStage)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var lead = project.Leads.FirstOrDefault(l => l.Id == leadId);
                if (lead == null) return;

                lead.Stage = newStage;

                // Update win probability based on stage
                lead.WinProbability = newStage switch
                {
                    LeadStage.Prospect => 0.1,
                    LeadStage.Qualification => 0.2,
                    LeadStage.Proposal => 0.35,
                    LeadStage.Negotiation => 0.6,
                    LeadStage.Closed_Won => 1.0,
                    LeadStage.Closed_Lost => 0,
                    _ => lead.WinProbability
                };
            }
        }

        public Proposal CreateProposal(string projectId, string leadId, double proposedValue, double proposedMargin)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var lead = project.Leads.FirstOrDefault(l => l.Id == leadId);
                if (lead == null) return null;

                var proposal = new Proposal
                {
                    LeadId = leadId,
                    ProjectName = lead.ProjectName,
                    ClientName = lead.ClientName,
                    ProposedValue = proposedValue,
                    ProposedMargin = proposedMargin,
                    Status = ProposalStatus.Draft,
                    DueDate = lead.ProposalDueDate ?? DateTime.UtcNow.AddDays(14)
                };

                // Initialize standard sections
                proposal.Sections = new List<ProposalSection>
                {
                    new() { Name = "Executive Summary", Status = "Not Started", CompletionPercentage = 0 },
                    new() { Name = "Technical Approach", Status = "Not Started", CompletionPercentage = 0 },
                    new() { Name = "Project Team", Status = "Not Started", CompletionPercentage = 0 },
                    new() { Name = "Schedule", Status = "Not Started", CompletionPercentage = 0 },
                    new() { Name = "Pricing", Status = "Not Started", CompletionPercentage = 0 },
                    new() { Name = "References", Status = "Not Started", CompletionPercentage = 0 }
                };

                project.Proposals.Add(proposal);
                lead.Stage = LeadStage.Proposal;
                return proposal;
            }
        }

        public async Task<Pipeline> AnalyzePipeline(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var activeLeads = project.Leads.Where(l =>
                        l.Stage != LeadStage.Closed_Won && l.Stage != LeadStage.Closed_Lost).ToList();

                    var pipeline = new Pipeline
                    {
                        TotalValue = activeLeads.Sum(l => l.EstimatedValue),
                        WeightedValue = activeLeads.Sum(l => l.WeightedValue)
                    };

                    foreach (LeadStage stage in Enum.GetValues<LeadStage>())
                    {
                        var stageLeads = activeLeads.Where(l => l.Stage == stage).ToList();
                        pipeline.StageBreakdown[stage] = new StageMetrics
                        {
                            Stage = stage,
                            Count = stageLeads.Count,
                            TotalValue = stageLeads.Sum(l => l.EstimatedValue),
                            WeightedValue = stageLeads.Sum(l => l.WeightedValue),
                            AverageAge = stageLeads.Any() ?
                                stageLeads.Average(l => (DateTime.UtcNow - l.IdentifiedDate).TotalDays) : 0
                        };
                    }

                    var closedWon = project.Leads.Count(l => l.Stage == LeadStage.Closed_Won);
                    var closedLost = project.Leads.Count(l => l.Stage == LeadStage.Closed_Lost);
                    pipeline.ConversionRate = closedWon + closedLost > 0 ?
                        (double)closedWon / (closedWon + closedLost) : 0;

                    pipeline.AverageDealSize = closedWon > 0 ?
                        project.Leads.Where(l => l.Stage == LeadStage.Closed_Won).Average(l => l.EstimatedValue) : 0;

                    // Health indicators
                    pipeline.HealthIndicators = new List<PipelineHealthIndicator>
                    {
                        new() { Indicator = "Pipeline Coverage", Value = pipeline.WeightedValue, Target = 1000000, Status = pipeline.WeightedValue >= 1000000 ? "Healthy" : "Low" },
                        new() { Indicator = "Conversion Rate", Value = pipeline.ConversionRate, Target = 0.25, Status = pipeline.ConversionRate >= 0.25 ? "Healthy" : "Below Target" },
                        new() { Indicator = "Early Stage Leads", Value = pipeline.StageBreakdown[LeadStage.Prospect].Count, Target = 10, Status = pipeline.StageBreakdown[LeadStage.Prospect].Count >= 10 ? "Healthy" : "Need More" }
                    };

                    project.Pipeline = pipeline;
                    return pipeline;
                }
            });
        }

        public async Task<BDMetrics> CalculateMetrics(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var metrics = new BDMetrics
                    {
                        TotalLeads = project.Leads.Count,
                        ActiveLeads = project.Leads.Count(l => l.Stage != LeadStage.Closed_Won && l.Stage != LeadStage.Closed_Lost),
                        ProposalsSubmitted = project.Proposals.Count(p => p.Status >= ProposalStatus.Submitted),
                        ProposalsWon = project.Proposals.Count(p => p.Status == ProposalStatus.Awarded)
                    };

                    var closedWon = project.Leads.Where(l => l.Stage == LeadStage.Closed_Won).ToList();
                    var closedLost = project.Leads.Where(l => l.Stage == LeadStage.Closed_Lost).ToList();

                    metrics.WinRate = closedWon.Count + closedLost.Count > 0 ?
                        (double)closedWon.Count / (closedWon.Count + closedLost.Count) : 0;

                    metrics.ProposalWinRate = metrics.ProposalsSubmitted > 0 ?
                        (double)metrics.ProposalsWon / metrics.ProposalsSubmitted : 0;

                    metrics.PipelineValue = project.Pipeline?.TotalValue ?? 0;
                    metrics.WeightedPipeline = project.Pipeline?.WeightedValue ?? 0;

                    metrics.AverageProposalValue = project.Proposals.Any() ?
                        project.Proposals.Average(p => p.ProposedValue) : 0;

                    var repeatClients = project.Leads
                        .Where(l => l.Stage == LeadStage.Closed_Won)
                        .GroupBy(l => l.ClientName)
                        .Count(g => g.Count() > 1);

                    metrics.RepeatClientPercentage = closedWon.Any() ?
                        (double)repeatClients / closedWon.Select(l => l.ClientName).Distinct().Count() : 0;

                    project.Metrics = metrics;
                    return metrics;
                }
            });
        }

        public Client AddClient(string projectId, string name, ClientType type,
            double annualSpend, string accountManager)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var client = new Client
                {
                    Name = name,
                    Type = type,
                    AnnualConstructionSpend = annualSpend,
                    AccountManager = accountManager,
                    RelationshipStrength = 0.5,
                    SatisfactionScore = 0.8
                };

                project.Clients.Add(client);
                return client;
            }
        }

        public List<TargetClient> IdentifyTargetClients(string projectId, int count = 10)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<TargetClient>();

                var random = new Random();
                var targets = new List<TargetClient>();

                for (int i = 0; i < count; i++)
                {
                    targets.Add(new TargetClient
                    {
                        ClientName = $"Target Client {i + 1}",
                        Type = (ClientType)random.Next(5),
                        EstimatedAnnualSpend = 5000000 + random.NextDouble() * 45000000,
                        FitScore = 0.5 + random.NextDouble() * 0.5,
                        AccessibilityScore = 0.3 + random.NextDouble() * 0.6,
                        EntryStrategy = random.NextDouble() > 0.5 ? "Direct outreach" : "Referral introduction"
                    });
                }

                return targets.OrderByDescending(t => t.FitScore * t.AccessibilityScore).ToList();
            }
        }
    }
}
