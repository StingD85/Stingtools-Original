// ===================================================================
// StingBIM Market Intelligence Engine
// Construction market analysis, trends, forecasting, opportunities
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.MarketIntelligence
{
    #region Enums

    public enum MarketSector { Residential, Commercial, Industrial, Infrastructure, Institutional, MixedUse }
    public enum ProjectStage { Planning, Design, Bidding, Construction, Completed }
    public enum TrendDirection { Rising, Stable, Declining, Volatile }
    public enum MarketIndicator { BuildingPermits, ConstructionSpending, MaterialCosts, LaborCosts, InterestRates }
    public enum RiskLevel { Low, Moderate, High, VeryHigh }
    public enum RegionType { Metro, Suburban, Rural, National, International }

    #endregion

    #region Data Models

    public class MarketIntelligenceProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public List<MarketRegion> Regions { get; set; } = new();
        public List<MarketTrend> Trends { get; set; } = new();
        public List<ProjectOpportunity> Opportunities { get; set; } = new();
        public MarketForecast Forecast { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class MarketRegion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public RegionType Type { get; set; }
        public double Population { get; set; }
        public double GDPGrowth { get; set; }
        public double ConstructionVolume { get; set; }
        public Dictionary<MarketSector, SectorMetrics> SectorData { get; set; } = new();
        public List<MarketIndicatorValue> Indicators { get; set; } = new();
        public double MarketScore { get; set; }
    }

    public class SectorMetrics
    {
        public MarketSector Sector { get; set; }
        public double MarketSize { get; set; }
        public double GrowthRate { get; set; }
        public TrendDirection Trend { get; set; }
        public int ActiveProjects { get; set; }
        public double AverageProjectSize { get; set; }
        public double AvgSquareFootCost { get; set; }
        public List<string> KeyDrivers { get; set; } = new();
    }

    public class MarketIndicatorValue
    {
        public MarketIndicator Indicator { get; set; }
        public double Value { get; set; }
        public double YoYChange { get; set; }
        public TrendDirection Trend { get; set; }
        public DateTime AsOfDate { get; set; }
    }

    public class MarketTrend
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public TrendDirection Direction { get; set; }
        public double Impact { get; set; }
        public double Probability { get; set; }
        public string TimeHorizon { get; set; }
        public List<string> AffectedSectors { get; set; } = new();
        public List<string> Implications { get; set; } = new();
    }

    public class ProjectOpportunity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public MarketSector Sector { get; set; }
        public ProjectStage Stage { get; set; }
        public double EstimatedValue { get; set; }
        public double SquareFootage { get; set; }
        public DateTime EstimatedStart { get; set; }
        public string Owner { get; set; }
        public string Architect { get; set; }
        public double WinProbability { get; set; }
        public RiskLevel Risk { get; set; }
        public List<string> RequiredCapabilities { get; set; } = new();
        public string Source { get; set; }
    }

    public class MarketForecast
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Region { get; set; }
        public int ForecastYear { get; set; }
        public Dictionary<MarketSector, double> SectorGrowth { get; set; } = new();
        public double OverallGrowth { get; set; }
        public double ConfidenceInterval { get; set; }
        public List<ForecastScenario> Scenarios { get; set; } = new();
        public List<string> KeyAssumptions { get; set; } = new();
    }

    public class ForecastScenario
    {
        public string Name { get; set; }
        public double Probability { get; set; }
        public double GrowthRate { get; set; }
        public string Description { get; set; }
    }

    public class MaterialPriceIndex
    {
        public string Material { get; set; }
        public double CurrentPrice { get; set; }
        public double PriceIndex { get; set; }
        public double YoYChange { get; set; }
        public double MoMChange { get; set; }
        public TrendDirection Trend { get; set; }
        public double ForecastPrice { get; set; }
        public string Unit { get; set; }
    }

    public class LaborMarketData
    {
        public string Trade { get; set; }
        public double HourlyRate { get; set; }
        public double AvailabilityIndex { get; set; }
        public double DemandIndex { get; set; }
        public TrendDirection WageTrend { get; set; }
        public double UnionizationRate { get; set; }
        public double ProjectedShortage { get; set; }
    }

    public class CompetitiveLandscape
    {
        public string Region { get; set; }
        public MarketSector Sector { get; set; }
        public int TotalCompetitors { get; set; }
        public double MarketConcentration { get; set; }
        public List<MarketPlayer> TopPlayers { get; set; } = new();
        public double AverageMargin { get; set; }
        public double BidCompetitiveness { get; set; }
    }

    public class MarketPlayer
    {
        public string Name { get; set; }
        public double MarketShare { get; set; }
        public double Revenue { get; set; }
        public int ActiveProjects { get; set; }
        public double WinRate { get; set; }
        public List<string> Strengths { get; set; } = new();
    }

    #endregion

    public sealed class MarketIntelligenceEngine
    {
        private static readonly Lazy<MarketIntelligenceEngine> _instance =
            new Lazy<MarketIntelligenceEngine>(() => new MarketIntelligenceEngine());
        public static MarketIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, MarketIntelligenceProject> _projects = new();
        private readonly object _lock = new object();

        private MarketIntelligenceEngine() { }

        public MarketIntelligenceProject CreateProject(string name)
        {
            var project = new MarketIntelligenceProject { Name = name };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public MarketRegion AddRegion(string projectId, string name, RegionType type,
            double population, double constructionVolume)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var region = new MarketRegion
                {
                    Name = name,
                    Type = type,
                    Population = population,
                    ConstructionVolume = constructionVolume
                };

                // Initialize sector data
                foreach (MarketSector sector in Enum.GetValues<MarketSector>())
                {
                    region.SectorData[sector] = new SectorMetrics
                    {
                        Sector = sector,
                        MarketSize = constructionVolume * GetSectorShare(sector),
                        GrowthRate = 0.02 + new Random().NextDouble() * 0.06,
                        Trend = TrendDirection.Stable
                    };
                }

                region.MarketScore = CalculateMarketScore(region);
                project.Regions.Add(region);
                return region;
            }
        }

        private double GetSectorShare(MarketSector sector)
        {
            return sector switch
            {
                MarketSector.Residential => 0.35,
                MarketSector.Commercial => 0.25,
                MarketSector.Industrial => 0.15,
                MarketSector.Infrastructure => 0.12,
                MarketSector.Institutional => 0.08,
                MarketSector.MixedUse => 0.05,
                _ => 0.1
            };
        }

        private double CalculateMarketScore(MarketRegion region)
        {
            double score = 0;
            score += Math.Min(region.Population / 1000000, 30);
            score += Math.Min(region.ConstructionVolume / 1000000000, 30);
            score += region.GDPGrowth * 10;
            score += region.SectorData.Values.Average(s => s.GrowthRate) * 100;
            return Math.Min(100, score);
        }

        public ProjectOpportunity AddOpportunity(string projectId, string name, string location,
            MarketSector sector, double value, double sqft, DateTime estStart)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var opportunity = new ProjectOpportunity
                {
                    Name = name,
                    Location = location,
                    Sector = sector,
                    EstimatedValue = value,
                    SquareFootage = sqft,
                    EstimatedStart = estStart,
                    Stage = ProjectStage.Planning,
                    WinProbability = 0.15 + new Random().NextDouble() * 0.35,
                    Risk = value > 50000000 ? RiskLevel.High : RiskLevel.Moderate
                };

                project.Opportunities.Add(opportunity);
                return opportunity;
            }
        }

        public async Task<MarketForecast> GenerateForecast(string projectId, string regionName, int year)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var region = project.Regions.FirstOrDefault(r => r.Name == regionName);
                    var random = new Random();

                    var forecast = new MarketForecast
                    {
                        Region = regionName,
                        ForecastYear = year,
                        OverallGrowth = 0.02 + random.NextDouble() * 0.04,
                        ConfidenceInterval = 0.15
                    };

                    foreach (MarketSector sector in Enum.GetValues<MarketSector>())
                    {
                        forecast.SectorGrowth[sector] = -0.02 + random.NextDouble() * 0.08;
                    }

                    forecast.Scenarios = new List<ForecastScenario>
                    {
                        new() { Name = "Base Case", Probability = 0.60, GrowthRate = forecast.OverallGrowth, Description = "Continued moderate growth" },
                        new() { Name = "Upside", Probability = 0.25, GrowthRate = forecast.OverallGrowth * 1.5, Description = "Strong economic expansion" },
                        new() { Name = "Downside", Probability = 0.15, GrowthRate = forecast.OverallGrowth * 0.3, Description = "Economic slowdown" }
                    };

                    forecast.KeyAssumptions = new List<string>
                    {
                        "Interest rates remain stable",
                        "No major policy changes",
                        "Labor availability improves moderately",
                        "Material costs increase 3-5%"
                    };

                    project.Forecast = forecast;
                    return forecast;
                }
            });
        }

        public List<MaterialPriceIndex> GetMaterialPrices()
        {
            var random = new Random();
            var materials = new[] { "Concrete", "Steel Rebar", "Structural Steel", "Lumber", "Copper", "Aluminum", "Drywall", "Roofing" };

            return materials.Select(m => new MaterialPriceIndex
            {
                Material = m,
                CurrentPrice = 100 + random.NextDouble() * 500,
                PriceIndex = 100 + random.NextDouble() * 50,
                YoYChange = -0.1 + random.NextDouble() * 0.3,
                MoMChange = -0.05 + random.NextDouble() * 0.1,
                Trend = (TrendDirection)random.Next(4),
                Unit = m == "Concrete" ? "$/CY" : "$/ton"
            }).ToList();
        }

        public List<LaborMarketData> GetLaborMarketData()
        {
            var random = new Random();
            var trades = new[] { "Electrician", "Plumber", "Carpenter", "HVAC Tech", "Ironworker", "Mason", "Roofer", "Laborer" };

            return trades.Select(t => new LaborMarketData
            {
                Trade = t,
                HourlyRate = 25 + random.NextDouble() * 40,
                AvailabilityIndex = 0.5 + random.NextDouble() * 0.4,
                DemandIndex = 0.6 + random.NextDouble() * 0.35,
                WageTrend = TrendDirection.Rising,
                UnionizationRate = 0.1 + random.NextDouble() * 0.5,
                ProjectedShortage = random.NextDouble() * 0.2
            }).ToList();
        }

        public MarketTrend AddTrend(string projectId, string name, string description,
            TrendDirection direction, double impact, List<string> sectors)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var trend = new MarketTrend
                {
                    Name = name,
                    Description = description,
                    Direction = direction,
                    Impact = impact,
                    Probability = 0.5 + new Random().NextDouble() * 0.4,
                    AffectedSectors = sectors ?? new List<string>()
                };

                project.Trends.Add(trend);
                return trend;
            }
        }

        public CompetitiveLandscape AnalyzeCompetitiveLandscape(string region, MarketSector sector)
        {
            var random = new Random();

            var landscape = new CompetitiveLandscape
            {
                Region = region,
                Sector = sector,
                TotalCompetitors = 20 + random.Next(80),
                MarketConcentration = 0.3 + random.NextDouble() * 0.4,
                AverageMargin = 0.03 + random.NextDouble() * 0.07,
                BidCompetitiveness = 3 + random.Next(8)
            };

            for (int i = 0; i < 5; i++)
            {
                landscape.TopPlayers.Add(new MarketPlayer
                {
                    Name = $"Competitor {i + 1}",
                    MarketShare = 0.05 + random.NextDouble() * 0.15,
                    Revenue = 100000000 + random.NextDouble() * 900000000,
                    ActiveProjects = 5 + random.Next(20),
                    WinRate = 0.15 + random.NextDouble() * 0.25
                });
            }

            return landscape;
        }

        public List<ProjectOpportunity> GetTopOpportunities(string projectId, int count = 10)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<ProjectOpportunity>();

                return project.Opportunities
                    .OrderByDescending(o => o.EstimatedValue * o.WinProbability)
                    .Take(count)
                    .ToList();
            }
        }
    }
}
