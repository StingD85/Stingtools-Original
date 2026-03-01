// ===================================================================
// StingBIM Prefabrication Intelligence Engine
// Modular design, prefab optimization, manufacturing tracking
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.PrefabricationIntelligence
{
    #region Enums

    public enum PrefabType { Structural, MEP, Facade, Bathroom, Kitchen, Modular }
    public enum ManufacturingStatus { Design, Approved, InProduction, QualityCheck, Ready, Shipped, Installed }
    public enum TransportMode { Flatbed, Specialized, Modular, Crane }
    public enum FeasibilityResult { HighlyFeasible, Feasible, Marginal, NotRecommended }

    #endregion

    #region Data Models

    public class PrefabProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public List<PrefabModule> Modules { get; set; } = new();
        public List<ManufacturingOrder> Orders { get; set; } = new();
        public PrefabMetrics Metrics { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PrefabModule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModuleCode { get; set; }
        public string Name { get; set; }
        public PrefabType Type { get; set; }
        public ModuleDimensions Dimensions { get; set; }
        public double Weight { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public List<string> Components { get; set; } = new();
        public List<Connection> Connections { get; set; } = new();
        public ManufacturingStatus Status { get; set; } = ManufacturingStatus.Design;
        public FeasibilityAssessment Feasibility { get; set; }
    }

    public class ModuleDimensions
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Volume => Length * Width * Height;
    }

    public class Connection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConnectedModuleId { get; set; }
        public string ConnectionType { get; set; }
        public string Location { get; set; }
        public List<string> Requirements { get; set; } = new();
    }

    public class FeasibilityAssessment
    {
        public FeasibilityResult Result { get; set; }
        public double OverallScore { get; set; }
        public double TransportabilityScore { get; set; }
        public double RepetitionScore { get; set; }
        public double ComplexityScore { get; set; }
        public double CostBenefitScore { get; set; }
        public List<string> Advantages { get; set; } = new();
        public List<string> Challenges { get; set; } = new();
        public decimal EstimatedSavings { get; set; }
        public int ScheduleReduction { get; set; }
    }

    public class ManufacturingOrder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrderNumber { get; set; }
        public string ModuleId { get; set; }
        public int Quantity { get; set; }
        public ManufacturingStatus Status { get; set; } = ManufacturingStatus.Design;
        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Manufacturer { get; set; }
        public List<QualityCheck> QualityChecks { get; set; } = new();
        public TransportPlan Transport { get; set; }
    }

    public class QualityCheck
    {
        public DateTime Date { get; set; }
        public string Inspector { get; set; }
        public string CheckType { get; set; }
        public bool Passed { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public class TransportPlan
    {
        public TransportMode Mode { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public double Distance { get; set; }
        public List<RouteConstraint> Constraints { get; set; } = new();
        public DateTime ScheduledDate { get; set; }
        public decimal Cost { get; set; }
        public List<string> Permits { get; set; } = new();
    }

    public class RouteConstraint
    {
        public string Type { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string Mitigation { get; set; }
    }

    public class AssemblySequence
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<AssemblyStep> Steps { get; set; } = new();
        public int TotalDuration { get; set; }
        public List<string> RequiredEquipment { get; set; } = new();
        public List<string> SafetyRequirements { get; set; } = new();
    }

    public class AssemblyStep
    {
        public int Sequence { get; set; }
        public string ModuleId { get; set; }
        public string Description { get; set; }
        public int DurationMinutes { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public string Equipment { get; set; }
        public int CrewSize { get; set; }
    }

    public class PrefabMetrics
    {
        public int TotalModules { get; set; }
        public int ModulesProduced { get; set; }
        public int ModulesInstalled { get; set; }
        public double ProductionRate { get; set; }
        public double QualityPassRate { get; set; }
        public decimal TotalCost { get; set; }
        public decimal EstimatedSavings { get; set; }
        public int ScheduleDaysReduced { get; set; }
    }

    public class DesignOptimization
    {
        public string ModuleId { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public ModuleDimensions OptimizedDimensions { get; set; }
        public decimal CostReduction { get; set; }
        public double WeightReduction { get; set; }
    }

    #endregion

    public sealed class PrefabricationIntelligenceEngine
    {
        private static readonly Lazy<PrefabricationIntelligenceEngine> _instance =
            new Lazy<PrefabricationIntelligenceEngine>(() => new PrefabricationIntelligenceEngine());
        public static PrefabricationIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, PrefabProject> _projects = new();
        private readonly object _lock = new object();

        private readonly Dictionary<TransportMode, TransportConstraints> _transportLimits = new()
        {
            [TransportMode.Flatbed] = new TransportConstraints { MaxLength = 16, MaxWidth = 2.6, MaxHeight = 4.3, MaxWeight = 25000 },
            [TransportMode.Specialized] = new TransportConstraints { MaxLength = 20, MaxWidth = 3.5, MaxHeight = 4.5, MaxWeight = 40000 },
            [TransportMode.Modular] = new TransportConstraints { MaxLength = 22, MaxWidth = 4.5, MaxHeight = 5.0, MaxWeight = 50000 }
        };

        private PrefabricationIntelligenceEngine() { }

        public PrefabProject CreatePrefabProject(string projectId, string name)
        {
            var project = new PrefabProject { ProjectId = projectId, Name = name };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public PrefabModule CreateModule(string projectId, string name, PrefabType type,
            double length, double width, double height, double weight, int quantity)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var module = new PrefabModule
                {
                    ModuleCode = $"PFB-{project.Modules.Count + 1:D3}",
                    Name = name,
                    Type = type,
                    Dimensions = new ModuleDimensions { Length = length, Width = width, Height = height },
                    Weight = weight,
                    Quantity = quantity
                };

                project.Modules.Add(module);
                return module;
            }
        }

        public async Task<FeasibilityAssessment> AnalyzePrefabFeasibility(string projectId, string moduleId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var module = project.Modules.FirstOrDefault(m => m.Id == moduleId);
                    if (module == null) return null;

                    var assessment = new FeasibilityAssessment();

                    // Transport score
                    var bestMode = _transportLimits.FirstOrDefault(t =>
                        module.Dimensions.Length <= t.Value.MaxLength &&
                        module.Dimensions.Width <= t.Value.MaxWidth &&
                        module.Dimensions.Height <= t.Value.MaxHeight &&
                        module.Weight <= t.Value.MaxWeight);

                    assessment.TransportabilityScore = bestMode.Key != 0 ? 85 : 50;

                    // Repetition score
                    assessment.RepetitionScore = module.Quantity switch
                    {
                        >= 20 => 95,
                        >= 10 => 85,
                        >= 5 => 70,
                        >= 3 => 55,
                        _ => 40
                    };

                    // Complexity score (simpler = higher score)
                    assessment.ComplexityScore = module.Type switch
                    {
                        PrefabType.Bathroom => 90,
                        PrefabType.Structural => 85,
                        PrefabType.MEP => 75,
                        PrefabType.Facade => 70,
                        PrefabType.Modular => 65,
                        _ => 60
                    };

                    // Cost benefit
                    var laborSavings = module.Quantity * 500m;
                    var scheduleSavings = module.Quantity * 200m;
                    assessment.CostBenefitScore = laborSavings > 5000 ? 90 : laborSavings > 2000 ? 75 : 60;
                    assessment.EstimatedSavings = laborSavings + scheduleSavings;
                    assessment.ScheduleReduction = module.Quantity * 2;

                    // Overall score
                    assessment.OverallScore = (assessment.TransportabilityScore +
                                               assessment.RepetitionScore +
                                               assessment.ComplexityScore +
                                               assessment.CostBenefitScore) / 4;

                    assessment.Result = assessment.OverallScore switch
                    {
                        >= 80 => FeasibilityResult.HighlyFeasible,
                        >= 65 => FeasibilityResult.Feasible,
                        >= 50 => FeasibilityResult.Marginal,
                        _ => FeasibilityResult.NotRecommended
                    };

                    // Generate advantages and challenges
                    if (assessment.RepetitionScore >= 70)
                        assessment.Advantages.Add("Good repetition for manufacturing efficiency");
                    if (assessment.TransportabilityScore >= 80)
                        assessment.Advantages.Add("Standard transport possible");
                    if (assessment.ScheduleReduction > 10)
                        assessment.Advantages.Add($"Potential {assessment.ScheduleReduction} day schedule reduction");

                    if (assessment.TransportabilityScore < 70)
                        assessment.Challenges.Add("Oversized transport required");
                    if (module.Weight > 30000)
                        assessment.Challenges.Add("Heavy lift equipment needed");

                    module.Feasibility = assessment;
                    return assessment;
                }
            });
        }

        public async Task<DesignOptimization> OptimizeModularDesign(string projectId, string moduleId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var module = project.Modules.FirstOrDefault(m => m.Id == moduleId);
                    if (module == null) return null;

                    var optimization = new DesignOptimization { ModuleId = moduleId };

                    // Find optimal transport mode
                    var bestTransport = _transportLimits
                        .Where(t => module.Dimensions.Length <= t.Value.MaxLength * 1.1)
                        .OrderBy(t => t.Value.MaxLength)
                        .FirstOrDefault();

                    if (module.Dimensions.Length > bestTransport.Value.MaxLength)
                    {
                        optimization.Recommendations.Add($"Reduce length to {bestTransport.Value.MaxLength}m for standard transport");
                        optimization.OptimizedDimensions = new ModuleDimensions
                        {
                            Length = bestTransport.Value.MaxLength,
                            Width = module.Dimensions.Width,
                            Height = module.Dimensions.Height
                        };
                    }

                    if (module.Dimensions.Width > 3.0)
                    {
                        optimization.Recommendations.Add("Consider splitting into 3m wide sections for easier transport");
                    }

                    optimization.Recommendations.Add("Standardize connection details across modules");
                    optimization.Recommendations.Add("Pre-install MEP rough-ins where possible");
                    optimization.CostReduction = module.UnitCost * 0.05m * module.Quantity;

                    return optimization;
                }
            });
        }

        public TransportPlan PlanTransport(string projectId, string moduleId, string origin, string destination, double distance)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var module = project.Modules.FirstOrDefault(m => m.Id == moduleId);
                if (module == null) return null;

                var mode = DetermineTransportMode(module);

                var plan = new TransportPlan
                {
                    Mode = mode,
                    Origin = origin,
                    Destination = destination,
                    Distance = distance,
                    ScheduledDate = DateTime.UtcNow.AddDays(14),
                    Cost = CalculateTransportCost(distance, mode, module.Weight)
                };

                if (mode == TransportMode.Specialized || mode == TransportMode.Modular)
                {
                    plan.Permits.Add("Oversized Load Permit");
                    plan.Permits.Add("Route Survey Required");
                    plan.Constraints.Add(new RouteConstraint
                    {
                        Type = "Bridge Clearance",
                        Description = "Verify bridge heights along route",
                        Mitigation = "Alternative route or escort vehicle"
                    });
                }

                return plan;
            }
        }

        private TransportMode DetermineTransportMode(PrefabModule module)
        {
            foreach (var (mode, constraints) in _transportLimits.OrderBy(t => t.Value.MaxLength))
            {
                if (module.Dimensions.Length <= constraints.MaxLength &&
                    module.Dimensions.Width <= constraints.MaxWidth &&
                    module.Dimensions.Height <= constraints.MaxHeight &&
                    module.Weight <= constraints.MaxWeight)
                {
                    return mode;
                }
            }
            return TransportMode.Specialized;
        }

        private decimal CalculateTransportCost(double distance, TransportMode mode, double weight)
        {
            var baseRate = mode switch
            {
                TransportMode.Flatbed => 2.5m,
                TransportMode.Specialized => 5.0m,
                TransportMode.Modular => 8.0m,
                _ => 3.0m
            };

            return baseRate * (decimal)distance + (decimal)(weight / 1000) * 50m;
        }

        public AssemblySequence SequenceAssembly(string projectId, List<string> moduleIds)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var sequence = new AssemblySequence
                {
                    RequiredEquipment = new List<string> { "Tower Crane", "Mobile Crane", "Forklift" },
                    SafetyRequirements = new List<string>
                    {
                        "Fall protection required above 6 feet",
                        "Exclusion zone during lifting operations",
                        "Certified riggers for all lifts"
                    }
                };

                int stepNumber = 1;
                foreach (var moduleId in moduleIds)
                {
                    var module = project.Modules.FirstOrDefault(m => m.Id == moduleId);
                    if (module == null) continue;

                    sequence.Steps.Add(new AssemblyStep
                    {
                        Sequence = stepNumber++,
                        ModuleId = moduleId,
                        Description = $"Install {module.Name}",
                        DurationMinutes = CalculateInstallDuration(module),
                        Equipment = module.Weight > 10000 ? "Tower Crane" : "Mobile Crane",
                        CrewSize = module.Weight > 20000 ? 6 : 4
                    });
                }

                sequence.TotalDuration = sequence.Steps.Sum(s => s.DurationMinutes);
                return sequence;
            }
        }

        private int CalculateInstallDuration(PrefabModule module)
        {
            var baseDuration = module.Type switch
            {
                PrefabType.Bathroom => 45,
                PrefabType.Structural => 60,
                PrefabType.MEP => 90,
                PrefabType.Facade => 30,
                PrefabType.Modular => 120,
                _ => 60
            };

            if (module.Weight > 20000) baseDuration += 30;
            return baseDuration;
        }

        public ManufacturingOrder TrackManufacturing(string projectId, string moduleId, int quantity, string manufacturer, DateTime requiredDate)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var order = new ManufacturingOrder
                {
                    OrderNumber = $"MFG-{project.Orders.Count + 1:D3}",
                    ModuleId = moduleId,
                    Quantity = quantity,
                    Manufacturer = manufacturer,
                    OrderDate = DateTime.UtcNow,
                    RequiredDate = requiredDate
                };

                project.Orders.Add(order);
                UpdateMetrics(project);
                return order;
            }
        }

        public ManufacturingOrder UpdateManufacturingStatus(string projectId, string orderId, ManufacturingStatus status)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var order = project.Orders.FirstOrDefault(o => o.Id == orderId);
                if (order == null) return null;

                order.Status = status;
                if (status == ManufacturingStatus.Ready || status == ManufacturingStatus.Installed)
                    order.CompletedDate = DateTime.UtcNow;

                UpdateMetrics(project);
                return order;
            }
        }

        private void UpdateMetrics(PrefabProject project)
        {
            project.Metrics = new PrefabMetrics
            {
                TotalModules = project.Modules.Sum(m => m.Quantity),
                ModulesProduced = project.Orders.Where(o => o.Status >= ManufacturingStatus.Ready).Sum(o => o.Quantity),
                ModulesInstalled = project.Orders.Where(o => o.Status == ManufacturingStatus.Installed).Sum(o => o.Quantity),
                QualityPassRate = project.Orders.SelectMany(o => o.QualityChecks).Any() ?
                    project.Orders.SelectMany(o => o.QualityChecks).Count(q => q.Passed) * 100.0 /
                    project.Orders.SelectMany(o => o.QualityChecks).Count() : 100,
                TotalCost = project.Modules.Sum(m => m.UnitCost * m.Quantity),
                EstimatedSavings = project.Modules.Where(m => m.Feasibility != null).Sum(m => m.Feasibility.EstimatedSavings),
                ScheduleDaysReduced = project.Modules.Where(m => m.Feasibility != null).Sum(m => m.Feasibility.ScheduleReduction)
            };
        }

        private class TransportConstraints
        {
            public double MaxLength { get; set; }
            public double MaxWidth { get; set; }
            public double MaxHeight { get; set; }
            public double MaxWeight { get; set; }
        }
    }
}
