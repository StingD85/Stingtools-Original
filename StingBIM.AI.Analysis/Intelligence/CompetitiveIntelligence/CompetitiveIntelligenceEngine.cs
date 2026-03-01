// ===================================================================
// StingBIM Competitive Intelligence Engine
// Competitor analysis, win/loss tracking, positioning strategy
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CompetitiveIntelligence
{
    #region Enums

    public enum CompetitorTier { Major, Regional, Local, Emerging }
    public enum CompetitiveStrength { Strong, Moderate, Weak }
    public enum BidOutcome { Won, Lost, NoDecision, Withdrawn }
    public enum PositioningStrategy { CostLeader, Differentiator, Niche, Hybrid }
    public enum ThreatLevel { Low, Moderate, High, Critical }

    #endregion

    #region Data Models

    public class CompetitiveIntelligenceProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CompanyName { get; set; }
        public List<Competitor> Competitors { get; set; } = new();
        public List<BidRecord> BidHistory { get; set; } = new();
        public CompetitivePosition OwnPosition { get; set; }
        public List<StrategicRecommendation> Recommendations { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class Competitor
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public CompetitorTier Tier { get; set; }
        public double AnnualRevenue { get; set; }
        public int EmployeeCount { get; set; }
        public List<string> PrimaryMarkets { get; set; } = new();
        public List<string> ServiceLines { get; set; } = new();
        public CompetitorProfile Profile { get; set; }
        public List<CompetitorStrengthWeakness> SWOT { get; set; } = new();
        public ThreatLevel ThreatLevel { get; set; }
        public double MarketShare { get; set; }
        public List<RecentWin> RecentWins { get; set; } = new();
    }

    public class CompetitorProfile
    {
        public string OwnershipType { get; set; }
        public int YearsInBusiness { get; set; }
        public string Headquarters { get; set; }
        public List<string> GeographicPresence { get; set; } = new();
        public double BondingCapacity { get; set; }
        public List<string> Certifications { get; set; } = new();
        public PositioningStrategy Strategy { get; set; }
        public double PricingTendency { get; set; }
        public string ReputationScore { get; set; }
    }

    public class CompetitorStrengthWeakness
    {
        public string Category { get; set; }
        public bool IsStrength { get; set; }
        public string Description { get; set; }
        public CompetitiveStrength Significance { get; set; }
    }

    public class RecentWin
    {
        public string ProjectName { get; set; }
        public string Client { get; set; }
        public double ContractValue { get; set; }
        public DateTime AwardDate { get; set; }
        public string ProjectType { get; set; }
    }

    public class BidRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectName { get; set; }
        public string Client { get; set; }
        public double BidAmount { get; set; }
        public double WinningBid { get; set; }
        public BidOutcome Outcome { get; set; }
        public string WinningCompetitor { get; set; }
        public DateTime BidDate { get; set; }
        public List<string> KnownCompetitors { get; set; } = new();
        public BidAnalysis Analysis { get; set; }
        public List<string> LessonsLearned { get; set; } = new();
    }

    public class BidAnalysis
    {
        public double PriceVariance { get; set; }
        public double MarketPriceIndex { get; set; }
        public int CompetitorCount { get; set; }
        public string OutcomeReason { get; set; }
        public List<string> StrengthsLeveraged { get; set; } = new();
        public List<string> WeaknessesExposed { get; set; } = new();
    }

    public class CompetitivePosition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double MarketShare { get; set; }
        public int MarketRank { get; set; }
        public PositioningStrategy CurrentStrategy { get; set; }
        public Dictionary<string, CompetitiveStrength> CapabilityScores { get; set; } = new();
        public double WinRate { get; set; }
        public double AverageBidVariance { get; set; }
        public List<string> CompetitiveAdvantages { get; set; } = new();
        public List<string> CompetitiveDisadvantages { get; set; } = new();
    }

    public class StrategicRecommendation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public string Rationale { get; set; }
        public string Priority { get; set; }
        public string TimeFrame { get; set; }
        public double ExpectedImpact { get; set; }
        public List<string> RequiredActions { get; set; } = new();
        public List<string> TargetCompetitors { get; set; } = new();
    }

    public class WinLossAnalysis
    {
        public int TotalBids { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate { get; set; }
        public double TotalBidValue { get; set; }
        public double TotalWonValue { get; set; }
        public Dictionary<string, double> WinRateByClient { get; set; } = new();
        public Dictionary<string, double> WinRateByProjectType { get; set; } = new();
        public Dictionary<string, int> LossReasons { get; set; } = new();
        public List<string> TopCompetitorsBeaten { get; set; } = new();
        public List<string> TopCompetitorsLostTo { get; set; } = new();
    }

    public class BenchmarkComparison
    {
        public string Metric { get; set; }
        public double OwnValue { get; set; }
        public double IndustryAverage { get; set; }
        public double BestInClass { get; set; }
        public string Gap { get; set; }
        public CompetitiveStrength Position { get; set; }
    }

    #endregion

    public sealed class CompetitiveIntelligenceEngine
    {
        private static readonly Lazy<CompetitiveIntelligenceEngine> _instance =
            new Lazy<CompetitiveIntelligenceEngine>(() => new CompetitiveIntelligenceEngine());
        public static CompetitiveIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, CompetitiveIntelligenceProject> _projects = new();
        private readonly object _lock = new object();

        private CompetitiveIntelligenceEngine() { }

        public CompetitiveIntelligenceProject CreateProject(string companyName)
        {
            var project = new CompetitiveIntelligenceProject
            {
                CompanyName = companyName,
                OwnPosition = new CompetitivePosition()
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public Competitor AddCompetitor(string projectId, string name, CompetitorTier tier,
            double revenue, int employees, List<string> markets)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var random = new Random();
                var competitor = new Competitor
                {
                    Name = name,
                    Tier = tier,
                    AnnualRevenue = revenue,
                    EmployeeCount = employees,
                    PrimaryMarkets = markets ?? new List<string>(),
                    MarketShare = 0.02 + random.NextDouble() * 0.15,
                    ThreatLevel = tier == CompetitorTier.Major ? ThreatLevel.High : ThreatLevel.Moderate,
                    Profile = new CompetitorProfile
                    {
                        YearsInBusiness = 10 + random.Next(50),
                        OwnershipType = random.NextDouble() > 0.5 ? "Private" : "Public",
                        Strategy = (PositioningStrategy)random.Next(4),
                        PricingTendency = 0.9 + random.NextDouble() * 0.2,
                        BondingCapacity = revenue * (1 + random.NextDouble())
                    }
                };

                // Generate SWOT items
                competitor.SWOT.AddRange(new[]
                {
                    new CompetitorStrengthWeakness { Category = "Price", IsStrength = random.NextDouble() > 0.5, Description = "Pricing competitiveness", Significance = CompetitiveStrength.Moderate },
                    new CompetitorStrengthWeakness { Category = "Quality", IsStrength = random.NextDouble() > 0.4, Description = "Quality reputation", Significance = CompetitiveStrength.Strong },
                    new CompetitorStrengthWeakness { Category = "Relationships", IsStrength = random.NextDouble() > 0.5, Description = "Client relationships", Significance = CompetitiveStrength.Moderate }
                });

                project.Competitors.Add(competitor);
                return competitor;
            }
        }

        public BidRecord RecordBid(string projectId, string projectName, string client,
            double bidAmount, BidOutcome outcome, string winner = null, double? winningBid = null)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var record = new BidRecord
                {
                    ProjectName = projectName,
                    Client = client,
                    BidAmount = bidAmount,
                    Outcome = outcome,
                    WinningCompetitor = winner,
                    WinningBid = winningBid ?? bidAmount,
                    BidDate = DateTime.UtcNow
                };

                record.Analysis = new BidAnalysis
                {
                    PriceVariance = winningBid.HasValue ? (bidAmount - winningBid.Value) / winningBid.Value : 0,
                    CompetitorCount = 3 + new Random().Next(5),
                    OutcomeReason = outcome == BidOutcome.Won ? "Competitive pricing and strong proposal" :
                        outcome == BidOutcome.Lost ? "Price higher than competition" : "Pending decision"
                };

                project.BidHistory.Add(record);
                return record;
            }
        }

        public async Task<WinLossAnalysis> AnalyzeWinLoss(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var analysis = new WinLossAnalysis
                    {
                        TotalBids = project.BidHistory.Count,
                        Wins = project.BidHistory.Count(b => b.Outcome == BidOutcome.Won),
                        Losses = project.BidHistory.Count(b => b.Outcome == BidOutcome.Lost),
                        TotalBidValue = project.BidHistory.Sum(b => b.BidAmount),
                        TotalWonValue = project.BidHistory.Where(b => b.Outcome == BidOutcome.Won).Sum(b => b.BidAmount)
                    };

                    analysis.WinRate = analysis.TotalBids > 0 ? (double)analysis.Wins / analysis.TotalBids : 0;

                    // Win rate by client
                    var clientGroups = project.BidHistory.GroupBy(b => b.Client);
                    foreach (var group in clientGroups)
                    {
                        int clientWins = group.Count(b => b.Outcome == BidOutcome.Won);
                        analysis.WinRateByClient[group.Key] = group.Count() > 0 ? (double)clientWins / group.Count() : 0;
                    }

                    // Loss reasons
                    var lostBids = project.BidHistory.Where(b => b.Outcome == BidOutcome.Lost);
                    analysis.LossReasons["Price"] = lostBids.Count(b => b.Analysis?.PriceVariance > 0.05);
                    analysis.LossReasons["Relationship"] = lostBids.Count() - analysis.LossReasons["Price"];

                    // Top competitors
                    analysis.TopCompetitorsLostTo = project.BidHistory
                        .Where(b => b.Outcome == BidOutcome.Lost && !string.IsNullOrEmpty(b.WinningCompetitor))
                        .GroupBy(b => b.WinningCompetitor)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => g.Key)
                        .ToList();

                    return analysis;
                }
            });
        }

        public async Task<List<BenchmarkComparison>> BenchmarkPerformance(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return new List<BenchmarkComparison>();

                    var random = new Random();
                    var metrics = new[] { "Win Rate", "Average Margin", "Client Retention", "Safety Record", "Schedule Performance", "Quality Score" };

                    return metrics.Select(m =>
                    {
                        double own = 0.5 + random.NextDouble() * 0.4;
                        double avg = 0.5 + random.NextDouble() * 0.3;
                        double best = 0.8 + random.NextDouble() * 0.19;

                        return new BenchmarkComparison
                        {
                            Metric = m,
                            OwnValue = own,
                            IndustryAverage = avg,
                            BestInClass = best,
                            Gap = own >= best ? "Leading" : $"{(best - own) * 100:F0}% below best",
                            Position = own >= avg * 1.1 ? CompetitiveStrength.Strong :
                                own >= avg * 0.9 ? CompetitiveStrength.Moderate : CompetitiveStrength.Weak
                        };
                    }).ToList();
                }
            });
        }

        public List<StrategicRecommendation> GenerateRecommendations(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<StrategicRecommendation>();

                var recommendations = new List<StrategicRecommendation>();

                // Analyze win rate
                double winRate = project.BidHistory.Count > 0 ?
                    (double)project.BidHistory.Count(b => b.Outcome == BidOutcome.Won) / project.BidHistory.Count : 0;

                if (winRate < 0.25)
                {
                    recommendations.Add(new StrategicRecommendation
                    {
                        Title = "Improve Bid Selectivity",
                        Description = "Focus on opportunities with higher win probability",
                        Rationale = $"Current win rate of {winRate:P0} is below industry average",
                        Priority = "High",
                        TimeFrame = "Immediate",
                        ExpectedImpact = 0.3,
                        RequiredActions = new List<string>
                        {
                            "Implement go/no-go criteria",
                            "Increase pre-bid client engagement",
                            "Focus on repeat clients"
                        }
                    });
                }

                // Analyze competitive threats
                var highThreatCompetitors = project.Competitors.Where(c => c.ThreatLevel >= ThreatLevel.High).ToList();
                if (highThreatCompetitors.Any())
                {
                    recommendations.Add(new StrategicRecommendation
                    {
                        Title = "Counter High-Threat Competitors",
                        Description = "Develop strategies to compete against major competitors",
                        Rationale = $"{highThreatCompetitors.Count} competitors identified as high threat",
                        Priority = "High",
                        TimeFrame = "3-6 months",
                        ExpectedImpact = 0.25,
                        TargetCompetitors = highThreatCompetitors.Select(c => c.Name).ToList(),
                        RequiredActions = new List<string>
                        {
                            "Identify competitor weaknesses",
                            "Develop differentiation strategy",
                            "Build relationships in their key accounts"
                        }
                    });
                }

                project.Recommendations = recommendations;
                return recommendations;
            }
        }

        public CompetitivePosition UpdateOwnPosition(string projectId, double marketShare,
            int rank, double winRate, List<string> advantages)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                project.OwnPosition.MarketShare = marketShare;
                project.OwnPosition.MarketRank = rank;
                project.OwnPosition.WinRate = winRate;
                project.OwnPosition.CompetitiveAdvantages = advantages ?? new List<string>();

                return project.OwnPosition;
            }
        }
    }
}
