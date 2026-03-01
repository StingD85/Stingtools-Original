// ===================================================================
// StingBIM Portfolio Intelligence Engine
// Multi-project analytics, resource optimization, risk aggregation
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.PortfolioIntelligence
{
    #region Enums

    public enum ProjectStatus { Planning, Preconstruction, Active, OnHold, Closeout, Complete }
    public enum HealthStatus { Green, Yellow, Red, Critical }
    public enum ResourceType { Labor, Equipment, Material, Subcontractor }
    public enum RiskCategory { Schedule, Cost, Quality, Safety, External }

    #endregion

    #region Data Models

    public class PortfolioIntelligenceProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public List<PortfolioProject> Projects { get; set; } = new();
        public PortfolioMetrics Metrics { get; set; }
        public ResourcePool Resources { get; set; }
        public AggregatedRisk RiskProfile { get; set; }
        public List<PortfolioAlert> Alerts { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class PortfolioProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Client { get; set; }
        public string ProjectManager { get; set; }
        public ProjectStatus Status { get; set; }
        public HealthStatus Health { get; set; }
        public double ContractValue { get; set; }
        public double BilledToDate { get; set; }
        public double CostToDate { get; set; }
        public double EstimatedFinalCost { get; set; }
        public double PercentComplete { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedCompletion { get; set; }
        public DateTime? ForecastCompletion { get; set; }
        public ProjectPerformance Performance { get; set; }
        public List<ProjectRisk> Risks { get; set; } = new();
        public List<ResourceAllocation> ResourceAllocations { get; set; } = new();
    }

    public class ProjectPerformance
    {
        public double CPI { get; set; }
        public double SPI { get; set; }
        public double GrossMargin { get; set; }
        public double ProjectedMargin { get; set; }
        public double EarnedValue { get; set; }
        public double PlannedValue { get; set; }
        public double ActualCost { get; set; }
        public double CostVariance { get; set; }
        public double ScheduleVariance { get; set; }
        public double EstimateAtCompletion { get; set; }
    }

    public class ProjectRisk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; }
        public RiskCategory Category { get; set; }
        public double Probability { get; set; }
        public double Impact { get; set; }
        public double RiskScore => Probability * Impact;
        public string MitigationPlan { get; set; }
        public string Owner { get; set; }
    }

    public class ResourceAllocation
    {
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public ResourceType Type { get; set; }
        public double AllocationPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double HoursPerWeek { get; set; }
    }

    public class PortfolioMetrics
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public double TotalContractValue { get; set; }
        public double TotalBilledToDate { get; set; }
        public double TotalCostToDate { get; set; }
        public double OverallCPI { get; set; }
        public double OverallSPI { get; set; }
        public double AverageGrossMargin { get; set; }
        public double ProjectedAnnualRevenue { get; set; }
        public int ProjectsOnSchedule { get; set; }
        public int ProjectsOnBudget { get; set; }
        public int ProjectsAtRisk { get; set; }
        public double BacklogValue { get; set; }
        public double WeightedAverageCompletion { get; set; }
    }

    public class ResourcePool
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<Resource> Resources { get; set; } = new();
        public double TotalCapacity { get; set; }
        public double CurrentUtilization { get; set; }
        public List<ResourceConflict> Conflicts { get; set; } = new();
        public List<ResourceGap> Gaps { get; set; } = new();
    }

    public class Resource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ResourceType Type { get; set; }
        public double Capacity { get; set; }
        public double CurrentUtilization { get; set; }
        public List<string> Skills { get; set; } = new();
        public List<string> AssignedProjects { get; set; } = new();
        public double HourlyRate { get; set; }
    }

    public class ResourceConflict
    {
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public List<string> ConflictingProjects { get; set; } = new();
        public DateTime ConflictStart { get; set; }
        public DateTime ConflictEnd { get; set; }
        public double OverallocationPercentage { get; set; }
    }

    public class ResourceGap
    {
        public ResourceType Type { get; set; }
        public string SkillRequired { get; set; }
        public string ProjectId { get; set; }
        public DateTime NeededFrom { get; set; }
        public DateTime NeededTo { get; set; }
        public double HoursNeeded { get; set; }
    }

    public class AggregatedRisk
    {
        public double OverallRiskScore { get; set; }
        public int TotalRisks { get; set; }
        public int HighRisks { get; set; }
        public int MediumRisks { get; set; }
        public int LowRisks { get; set; }
        public Dictionary<RiskCategory, double> RiskByCategory { get; set; } = new();
        public double TotalRiskExposure { get; set; }
        public List<ProjectRisk> TopRisks { get; set; } = new();
    }

    public class PortfolioAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public HealthStatus Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsAcknowledged { get; set; }
    }

    public class PortfolioForecast
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public double ProjectedRevenue { get; set; }
        public double ProjectedCost { get; set; }
        public double ProjectedMargin { get; set; }
        public int ActiveProjectCount { get; set; }
        public double Backlog { get; set; }
    }

    public class ProjectPrioritization
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public double StrategicScore { get; set; }
        public double FinancialScore { get; set; }
        public double RiskScore { get; set; }
        public double OverallPriority { get; set; }
        public int Rank { get; set; }
    }

    #endregion

    public sealed class PortfolioIntelligenceEngine
    {
        private static readonly Lazy<PortfolioIntelligenceEngine> _instance =
            new Lazy<PortfolioIntelligenceEngine>(() => new PortfolioIntelligenceEngine());
        public static PortfolioIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, PortfolioIntelligenceProject> _portfolios = new();
        private readonly object _lock = new object();

        private PortfolioIntelligenceEngine() { }

        public PortfolioIntelligenceProject CreatePortfolio(string name)
        {
            var portfolio = new PortfolioIntelligenceProject
            {
                Name = name,
                Resources = new ResourcePool(),
                RiskProfile = new AggregatedRisk(),
                Metrics = new PortfolioMetrics()
            };

            lock (_lock) { _portfolios[portfolio.Id] = portfolio; }
            return portfolio;
        }

        public PortfolioProject AddProject(string portfolioId, string name, string client,
            double contractValue, DateTime start, DateTime plannedEnd)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return null;

                var project = new PortfolioProject
                {
                    Name = name,
                    Client = client,
                    ContractValue = contractValue,
                    StartDate = start,
                    PlannedCompletion = plannedEnd,
                    Status = ProjectStatus.Planning,
                    Health = HealthStatus.Green,
                    Performance = new ProjectPerformance { CPI = 1.0, SPI = 1.0, GrossMargin = 0.08 }
                };

                portfolio.Projects.Add(project);
                return project;
            }
        }

        public void UpdateProjectPerformance(string portfolioId, string projectId,
            double percentComplete, double costToDate, double billedToDate)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return;

                var project = portfolio.Projects.FirstOrDefault(p => p.Id == projectId);
                if (project == null) return;

                project.PercentComplete = percentComplete;
                project.CostToDate = costToDate;
                project.BilledToDate = billedToDate;

                // Calculate EVM metrics
                project.Performance.EarnedValue = project.ContractValue * percentComplete;
                project.Performance.PlannedValue = project.ContractValue *
                    Math.Min(1, (DateTime.UtcNow - project.StartDate).TotalDays /
                    (project.PlannedCompletion - project.StartDate).TotalDays);
                project.Performance.ActualCost = costToDate;

                project.Performance.CPI = costToDate > 0 ? project.Performance.EarnedValue / costToDate : 1;
                project.Performance.SPI = project.Performance.PlannedValue > 0 ?
                    project.Performance.EarnedValue / project.Performance.PlannedValue : 1;

                project.Performance.CostVariance = project.Performance.EarnedValue - costToDate;
                project.Performance.ScheduleVariance = project.Performance.EarnedValue - project.Performance.PlannedValue;

                project.Performance.EstimateAtCompletion = project.Performance.CPI > 0 ?
                    project.ContractValue / project.Performance.CPI : project.ContractValue;

                project.Performance.GrossMargin = billedToDate > 0 ? (billedToDate - costToDate) / billedToDate : 0;
                project.Performance.ProjectedMargin = project.ContractValue > 0 ?
                    (project.ContractValue - project.Performance.EstimateAtCompletion) / project.ContractValue : 0;

                // Update health status
                project.Health = (project.Performance.CPI, project.Performance.SPI) switch
                {
                    ( >= 0.95, >= 0.95) => HealthStatus.Green,
                    ( >= 0.9, >= 0.9) => HealthStatus.Yellow,
                    ( >= 0.8, >= 0.8) => HealthStatus.Red,
                    _ => HealthStatus.Critical
                };

                // Generate alerts
                if (project.Health >= HealthStatus.Red)
                {
                    portfolio.Alerts.Add(new PortfolioAlert
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        AlertType = "Performance",
                        Message = $"Project performance is {project.Health}. CPI: {project.Performance.CPI:F2}, SPI: {project.Performance.SPI:F2}",
                        Severity = project.Health,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        public async Task<PortfolioMetrics> CalculatePortfolioMetrics(string portfolioId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                        return null;

                    var metrics = new PortfolioMetrics
                    {
                        TotalProjects = portfolio.Projects.Count,
                        ActiveProjects = portfolio.Projects.Count(p => p.Status == ProjectStatus.Active),
                        TotalContractValue = portfolio.Projects.Sum(p => p.ContractValue),
                        TotalBilledToDate = portfolio.Projects.Sum(p => p.BilledToDate),
                        TotalCostToDate = portfolio.Projects.Sum(p => p.CostToDate)
                    };

                    var activeProjects = portfolio.Projects.Where(p => p.Status == ProjectStatus.Active).ToList();
                    if (activeProjects.Any())
                    {
                        metrics.OverallCPI = activeProjects.Sum(p => p.Performance?.EarnedValue ?? 0) /
                            Math.Max(1, activeProjects.Sum(p => p.Performance?.ActualCost ?? 1));
                        metrics.OverallSPI = activeProjects.Sum(p => p.Performance?.EarnedValue ?? 0) /
                            Math.Max(1, activeProjects.Sum(p => p.Performance?.PlannedValue ?? 1));
                        metrics.AverageGrossMargin = activeProjects.Average(p => p.Performance?.GrossMargin ?? 0);
                        metrics.WeightedAverageCompletion = activeProjects.Sum(p => p.PercentComplete * p.ContractValue) /
                            Math.Max(1, activeProjects.Sum(p => p.ContractValue));
                    }

                    metrics.ProjectsOnSchedule = portfolio.Projects.Count(p => (p.Performance?.SPI ?? 1) >= 0.95);
                    metrics.ProjectsOnBudget = portfolio.Projects.Count(p => (p.Performance?.CPI ?? 1) >= 0.95);
                    metrics.ProjectsAtRisk = portfolio.Projects.Count(p => p.Health >= HealthStatus.Red);

                    metrics.BacklogValue = portfolio.Projects
                        .Where(p => p.Status == ProjectStatus.Planning || p.Status == ProjectStatus.Preconstruction)
                        .Sum(p => p.ContractValue);

                    portfolio.Metrics = metrics;
                    return metrics;
                }
            });
        }

        public async Task<AggregatedRisk> AggregateRisks(string portfolioId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                        return null;

                    var allRisks = portfolio.Projects.SelectMany(p => p.Risks).ToList();

                    var riskProfile = new AggregatedRisk
                    {
                        TotalRisks = allRisks.Count,
                        HighRisks = allRisks.Count(r => r.RiskScore >= 0.6),
                        MediumRisks = allRisks.Count(r => r.RiskScore >= 0.3 && r.RiskScore < 0.6),
                        LowRisks = allRisks.Count(r => r.RiskScore < 0.3),
                        OverallRiskScore = allRisks.Any() ? allRisks.Average(r => r.RiskScore) : 0,
                        TotalRiskExposure = allRisks.Sum(r => r.Impact * r.Probability),
                        TopRisks = allRisks.OrderByDescending(r => r.RiskScore).Take(10).ToList()
                    };

                    foreach (RiskCategory category in Enum.GetValues<RiskCategory>())
                    {
                        var categoryRisks = allRisks.Where(r => r.Category == category);
                        riskProfile.RiskByCategory[category] = categoryRisks.Any() ?
                            categoryRisks.Average(r => r.RiskScore) : 0;
                    }

                    portfolio.RiskProfile = riskProfile;
                    return riskProfile;
                }
            });
        }

        public Resource AddResource(string portfolioId, string name, ResourceType type,
            double capacity, double hourlyRate, List<string> skills)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return null;

                var resource = new Resource
                {
                    Name = name,
                    Type = type,
                    Capacity = capacity,
                    HourlyRate = hourlyRate,
                    Skills = skills ?? new List<string>()
                };

                portfolio.Resources.Resources.Add(resource);
                return resource;
            }
        }

        public List<ResourceConflict> IdentifyResourceConflicts(string portfolioId)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return new List<ResourceConflict>();

                var conflicts = new List<ResourceConflict>();

                foreach (var resource in portfolio.Resources.Resources)
                {
                    var allocations = portfolio.Projects
                        .SelectMany(p => p.ResourceAllocations.Where(a => a.ResourceId == resource.Id))
                        .ToList();

                    double totalAllocation = allocations.Sum(a => a.AllocationPercentage);

                    if (totalAllocation > 100)
                    {
                        conflicts.Add(new ResourceConflict
                        {
                            ResourceId = resource.Id,
                            ResourceName = resource.Name,
                            ConflictingProjects = allocations.Select(a => a.ResourceName).Distinct().ToList(),
                            OverallocationPercentage = totalAllocation
                        });
                    }
                }

                portfolio.Resources.Conflicts = conflicts;
                return conflicts;
            }
        }

        public List<PortfolioForecast> GenerateForecast(string portfolioId, int months)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return new List<PortfolioForecast>();

                var forecasts = new List<PortfolioForecast>();
                var startDate = DateTime.UtcNow;

                for (int i = 0; i < months; i++)
                {
                    var forecastDate = startDate.AddMonths(i);
                    var activeProjects = portfolio.Projects
                        .Where(p => p.StartDate <= forecastDate && (p.ForecastCompletion ?? p.PlannedCompletion) >= forecastDate)
                        .ToList();

                    forecasts.Add(new PortfolioForecast
                    {
                        Month = forecastDate.Month,
                        Year = forecastDate.Year,
                        ActiveProjectCount = activeProjects.Count,
                        ProjectedRevenue = activeProjects.Sum(p => p.ContractValue / 12),
                        ProjectedCost = activeProjects.Sum(p => (p.Performance?.EstimateAtCompletion ?? p.ContractValue * 0.92) / 12),
                        ProjectedMargin = 0.08,
                        Backlog = portfolio.Projects.Where(p => p.StartDate > forecastDate).Sum(p => p.ContractValue)
                    });
                }

                return forecasts;
            }
        }

        public List<ProjectPrioritization> PrioritizeProjects(string portfolioId)
        {
            lock (_lock)
            {
                if (!_portfolios.TryGetValue(portfolioId, out var portfolio))
                    return new List<ProjectPrioritization>();

                var random = new Random();
                var prioritizations = portfolio.Projects.Select(p => new ProjectPrioritization
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    StrategicScore = 0.5 + random.NextDouble() * 0.5,
                    FinancialScore = p.Performance?.ProjectedMargin ?? 0.08,
                    RiskScore = p.Risks.Any() ? 1 - p.Risks.Average(r => r.RiskScore) : 0.8
                }).ToList();

                foreach (var p in prioritizations)
                {
                    p.OverallPriority = p.StrategicScore * 0.4 + p.FinancialScore * 10 * 0.35 + p.RiskScore * 0.25;
                }

                var ranked = prioritizations.OrderByDescending(p => p.OverallPriority).ToList();
                for (int i = 0; i < ranked.Count; i++)
                    ranked[i].Rank = i + 1;

                return ranked;
            }
        }
    }
}
