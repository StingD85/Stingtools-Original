// ===================================================================
// StingBIM Disaster Recovery Intelligence Engine
// Business continuity, emergency response, recovery planning
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DisasterRecoveryIntelligence
{
    #region Enums

    public enum DisasterType { Fire, Flood, Earthquake, Hurricane, Tornado, Terrorism, Pandemic, CyberAttack, PowerOutage, EquipmentFailure }
    public enum ImpactSeverity { Negligible, Minor, Moderate, Major, Catastrophic }
    public enum RecoveryPriority { Critical, High, Medium, Low }
    public enum BCPStatus { Draft, Approved, Active, UnderReview, Expired }
    public enum ExerciseType { Tabletop, Walkthrough, Functional, FullScale }
    public enum ResourceType { Personnel, Equipment, Technology, Facility, Financial, Supplier }

    #endregion

    #region Data Models

    public class BusinessContinuityPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public string Version { get; set; }
        public BCPStatus Status { get; set; }
        public List<BusinessFunction> Functions { get; set; } = new();
        public List<DisasterScenario> Scenarios { get; set; } = new();
        public List<RecoveryStrategy> Strategies { get; set; } = new();
        public List<CriticalResource> Resources { get; set; } = new();
        public EmergencyContactList Contacts { get; set; }
        public List<BCPExercise> Exercises { get; set; } = new();
        public DateTime LastReview { get; set; }
        public DateTime NextReview { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BusinessFunction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string Department { get; set; }
        public RecoveryPriority Priority { get; set; }
        public int MaxTolerableDowntime { get; set; } // Hours
        public int RecoveryTimeObjective { get; set; } // Hours
        public int RecoveryPointObjective { get; set; } // Hours (data loss tolerance)
        public List<string> Dependencies { get; set; } = new();
        public List<string> DependentFunctions { get; set; } = new();
        public double RevenueImpactPerHour { get; set; }
        public List<string> CriticalApplications { get; set; } = new();
        public int MinimumStaff { get; set; }
    }

    public class DisasterScenario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public DisasterType Type { get; set; }
        public string Description { get; set; }
        public double Probability { get; set; }
        public ImpactSeverity Severity { get; set; }
        public List<string> AffectedFunctions { get; set; } = new();
        public List<string> AffectedFacilities { get; set; } = new();
        public double EstimatedDowntime { get; set; } // Hours
        public double FinancialImpact { get; set; }
        public List<string> WarningIndicators { get; set; } = new();
        public List<ImmediateAction> ImmediateActions { get; set; } = new();
    }

    public class ImmediateAction
    {
        public int Sequence { get; set; }
        public string Action { get; set; }
        public string Responsible { get; set; }
        public int TimeframeMinutes { get; set; }
        public List<string> Resources { get; set; } = new();
    }

    public class RecoveryStrategy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FunctionId { get; set; }
        public string StrategyName { get; set; }
        public string Description { get; set; }
        public RecoveryPriority Priority { get; set; }
        public int TargetRecoveryTime { get; set; } // Hours
        public List<RecoveryTask> Tasks { get; set; } = new();
        public List<string> RequiredResources { get; set; } = new();
        public double ImplementationCost { get; set; }
        public string AlternateSite { get; set; }
        public List<string> Workarounds { get; set; } = new();
    }

    public class RecoveryTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Sequence { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public string Responsible { get; set; }
        public int Duration { get; set; } // Minutes
        public List<string> Prerequisites { get; set; } = new();
        public List<string> Resources { get; set; } = new();
        public string SuccessCriteria { get; set; }
    }

    public class CriticalResource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ResourceType Type { get; set; }
        public RecoveryPriority Priority { get; set; }
        public string Location { get; set; }
        public string PrimaryContact { get; set; }
        public string BackupContact { get; set; }
        public List<string> SupportedFunctions { get; set; } = new();
        public string BackupResource { get; set; }
        public int RecoveryTime { get; set; } // Hours
        public double ReplacementCost { get; set; }
    }

    public class EmergencyContactList
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<EmergencyContact> Contacts { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string EmergencyHotline { get; set; }
        public string CommandCenterLocation { get; set; }
        public string BackupCommandCenter { get; set; }
    }

    public class EmergencyContact
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Team { get; set; }
        public string PrimaryPhone { get; set; }
        public string SecondaryPhone { get; set; }
        public string Email { get; set; }
        public bool IsOnCall { get; set; }
        public List<string> Responsibilities { get; set; } = new();
    }

    public class BCPExercise
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ExerciseType Type { get; set; }
        public string Scenario { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public List<string> Participants { get; set; } = new();
        public List<string> Objectives { get; set; } = new();
        public ExerciseResults Results { get; set; }
        public string Status { get; set; }
    }

    public class ExerciseResults
    {
        public double OverallScore { get; set; }
        public int ObjectivesMet { get; set; }
        public int TotalObjectives { get; set; }
        public double ActualRecoveryTime { get; set; } // Hours
        public double TargetRecoveryTime { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public List<string> ImprovementActions { get; set; } = new();
        public List<string> LessonsLearned { get; set; } = new();
    }

    public class BusinessImpactAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; }
        public List<BusinessFunction> Functions { get; set; } = new();
        public List<ImpactTimeline> Timeline { get; set; } = new();
        public double TotalDailyImpact { get; set; }
        public double TotalWeeklyImpact { get; set; }
        public List<string> CriticalDependencies { get; set; } = new();
        public DateTime AnalysisDate { get; set; }
    }

    public class ImpactTimeline
    {
        public int HoursAfterDisaster { get; set; }
        public double CumulativeFinancialImpact { get; set; }
        public double OperationalCapacity { get; set; }
        public int FunctionsRecovered { get; set; }
        public int TotalFunctions { get; set; }
        public List<string> MilestoneActions { get; set; } = new();
    }

    public class IncidentResponse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DisasterType IncidentType { get; set; }
        public DateTime OccurredAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public ImpactSeverity Severity { get; set; }
        public string Description { get; set; }
        public List<string> AffectedAreas { get; set; } = new();
        public List<string> ActionsToken { get; set; } = new();
        public string IncidentCommander { get; set; }
        public string Status { get; set; }
        public double ActualDowntime { get; set; }
        public double FinancialImpact { get; set; }
        public List<string> LessonsLearned { get; set; } = new();
    }

    public class RecoveryReadiness
    {
        public string OrganizationId { get; set; }
        public double OverallScore { get; set; }
        public double PlanCurrentness { get; set; }
        public double ResourceReadiness { get; set; }
        public double TeamPreparedness { get; set; }
        public double ExerciseFrequency { get; set; }
        public double BackupSystemStatus { get; set; }
        public List<string> Gaps { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public DateTime AssessmentDate { get; set; }
    }

    #endregion

    public sealed class DisasterRecoveryIntelligenceEngine
    {
        private static readonly Lazy<DisasterRecoveryIntelligenceEngine> _instance =
            new Lazy<DisasterRecoveryIntelligenceEngine>(() => new DisasterRecoveryIntelligenceEngine());
        public static DisasterRecoveryIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, BusinessContinuityPlan> _plans = new();
        private readonly Dictionary<string, IncidentResponse> _incidents = new();
        private readonly object _lock = new object();

        // Standard RTO by function type
        private readonly Dictionary<string, int> _standardRTO = new()
        {
            ["Life Safety"] = 0,
            ["Critical Operations"] = 4,
            ["Essential Services"] = 24,
            ["Important Functions"] = 72,
            ["Normal Operations"] = 168
        };

        private DisasterRecoveryIntelligenceEngine() { }

        public BusinessContinuityPlan CreatePlan(string organizationId, string organizationName)
        {
            var plan = new BusinessContinuityPlan
            {
                OrganizationId = organizationId,
                OrganizationName = organizationName,
                Version = "1.0",
                Status = BCPStatus.Draft,
                LastReview = DateTime.UtcNow,
                NextReview = DateTime.UtcNow.AddMonths(12),
                Contacts = new EmergencyContactList
                {
                    LastUpdated = DateTime.UtcNow,
                    EmergencyHotline = "1-800-EMERGENCY"
                }
            };

            lock (_lock) { _plans[plan.Id] = plan; }
            return plan;
        }

        public BusinessFunction AddBusinessFunction(string planId, string name, string department,
            RecoveryPriority priority, int maxDowntime, double revenueImpact)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var function = new BusinessFunction
                {
                    Name = name,
                    Department = department,
                    Priority = priority,
                    MaxTolerableDowntime = maxDowntime,
                    RecoveryTimeObjective = CalculateRTO(priority, maxDowntime),
                    RecoveryPointObjective = CalculateRPO(priority),
                    RevenueImpactPerHour = revenueImpact
                };

                plan.Functions.Add(function);
                return function;
            }
        }

        private int CalculateRTO(RecoveryPriority priority, int maxDowntime)
        {
            double factor = priority switch
            {
                RecoveryPriority.Critical => 0.25,
                RecoveryPriority.High => 0.5,
                RecoveryPriority.Medium => 0.75,
                _ => 0.9
            };
            return (int)(maxDowntime * factor);
        }

        private int CalculateRPO(RecoveryPriority priority)
        {
            return priority switch
            {
                RecoveryPriority.Critical => 1,
                RecoveryPriority.High => 4,
                RecoveryPriority.Medium => 24,
                _ => 72
            };
        }

        public DisasterScenario AddScenario(string planId, string name, DisasterType type,
            string description, double probability, ImpactSeverity severity)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var scenario = new DisasterScenario
                {
                    Name = name,
                    Type = type,
                    Description = description,
                    Probability = probability,
                    Severity = severity,
                    AffectedFunctions = DetermineAffectedFunctions(plan, type),
                    EstimatedDowntime = EstimateDowntime(type, severity),
                    FinancialImpact = EstimateFinancialImpact(plan, type, severity)
                };

                // Add warning indicators
                scenario.WarningIndicators = GetWarningIndicators(type);

                // Add immediate actions
                scenario.ImmediateActions = GenerateImmediateActions(type);

                plan.Scenarios.Add(scenario);
                return scenario;
            }
        }

        private List<string> DetermineAffectedFunctions(BusinessContinuityPlan plan, DisasterType type)
        {
            // Different disasters affect different functions
            return type switch
            {
                DisasterType.Fire => plan.Functions.Select(f => f.Id).ToList(),
                DisasterType.Flood => plan.Functions.Where(f => f.Department != "Remote").Select(f => f.Id).ToList(),
                DisasterType.CyberAttack => plan.Functions.Where(f => f.CriticalApplications.Any()).Select(f => f.Id).ToList(),
                DisasterType.PowerOutage => plan.Functions.Select(f => f.Id).ToList(),
                DisasterType.Pandemic => plan.Functions.Where(f => f.MinimumStaff > 0).Select(f => f.Id).ToList(),
                _ => plan.Functions.Select(f => f.Id).ToList()
            };
        }

        private double EstimateDowntime(DisasterType type, ImpactSeverity severity)
        {
            double baseDowntime = type switch
            {
                DisasterType.Fire => 168,
                DisasterType.Flood => 72,
                DisasterType.Earthquake => 336,
                DisasterType.Hurricane => 120,
                DisasterType.CyberAttack => 48,
                DisasterType.PowerOutage => 8,
                DisasterType.Pandemic => 720,
                _ => 24
            };

            double severityMultiplier = severity switch
            {
                ImpactSeverity.Negligible => 0.1,
                ImpactSeverity.Minor => 0.3,
                ImpactSeverity.Moderate => 0.6,
                ImpactSeverity.Major => 1.0,
                ImpactSeverity.Catastrophic => 2.0,
                _ => 1.0
            };

            return baseDowntime * severityMultiplier;
        }

        private double EstimateFinancialImpact(BusinessContinuityPlan plan, DisasterType type, ImpactSeverity severity)
        {
            double hourlyImpact = plan.Functions.Sum(f => f.RevenueImpactPerHour);
            double downtime = EstimateDowntime(type, severity);
            double directDamage = type switch
            {
                DisasterType.Fire => 500000,
                DisasterType.Flood => 300000,
                DisasterType.Earthquake => 1000000,
                DisasterType.CyberAttack => 200000,
                _ => 100000
            };

            double severityMultiplier = (int)severity * 0.5;
            return (hourlyImpact * downtime) + (directDamage * severityMultiplier);
        }

        private List<string> GetWarningIndicators(DisasterType type)
        {
            return type switch
            {
                DisasterType.Fire => new List<string> { "Smoke detected", "Fire alarm", "Electrical issues", "Unusual odors" },
                DisasterType.Flood => new List<string> { "Heavy rainfall forecast", "Rising water levels", "Flash flood warning", "Dam concerns" },
                DisasterType.Hurricane => new List<string> { "Hurricane watch", "Storm surge warning", "Evacuation orders", "Tropical storm upgrade" },
                DisasterType.CyberAttack => new List<string> { "Unusual network activity", "Failed login attempts", "Ransomware detection", "Data exfiltration alerts" },
                DisasterType.PowerOutage => new List<string> { "Grid instability", "Substation issues", "Weather warnings", "Scheduled maintenance" },
                DisasterType.Pandemic => new List<string> { "Disease outbreak reports", "WHO alerts", "Rising infection rates", "Supply chain issues" },
                _ => new List<string> { "Unusual activity", "System alerts", "Environmental changes" }
            };
        }

        private List<ImmediateAction> GenerateImmediateActions(DisasterType type)
        {
            var actions = new List<ImmediateAction>
            {
                new ImmediateAction { Sequence = 1, Action = "Ensure life safety - evacuate if necessary", Responsible = "All Staff", TimeframeMinutes = 5 },
                new ImmediateAction { Sequence = 2, Action = "Account for all personnel", Responsible = "Department Heads", TimeframeMinutes = 15 },
                new ImmediateAction { Sequence = 3, Action = "Notify emergency response team", Responsible = "Security", TimeframeMinutes = 5 }
            };

            switch (type)
            {
                case DisasterType.Fire:
                    actions.Add(new ImmediateAction { Sequence = 4, Action = "Activate fire suppression", Responsible = "Facilities", TimeframeMinutes = 2 });
                    actions.Add(new ImmediateAction { Sequence = 5, Action = "Contact fire department", Responsible = "Security", TimeframeMinutes = 3 });
                    break;
                case DisasterType.CyberAttack:
                    actions.Add(new ImmediateAction { Sequence = 4, Action = "Isolate affected systems", Responsible = "IT Security", TimeframeMinutes = 10 });
                    actions.Add(new ImmediateAction { Sequence = 5, Action = "Activate incident response", Responsible = "CISO", TimeframeMinutes = 15 });
                    break;
                case DisasterType.Flood:
                    actions.Add(new ImmediateAction { Sequence = 4, Action = "Move critical equipment to higher ground", Responsible = "Facilities", TimeframeMinutes = 30 });
                    actions.Add(new ImmediateAction { Sequence = 5, Action = "Shut down electrical systems", Responsible = "Facilities", TimeframeMinutes = 10 });
                    break;
            }

            actions.Add(new ImmediateAction { Sequence = 6, Action = "Establish command center", Responsible = "Incident Commander", TimeframeMinutes = 30 });
            actions.Add(new ImmediateAction { Sequence = 7, Action = "Begin damage assessment", Responsible = "Assessment Team", TimeframeMinutes = 60 });

            return actions;
        }

        public RecoveryStrategy CreateRecoveryStrategy(string planId, string functionId,
            string strategyName, List<RecoveryTask> tasks)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var function = plan.Functions.FirstOrDefault(f => f.Id == functionId);
                if (function == null) return null;

                var strategy = new RecoveryStrategy
                {
                    FunctionId = functionId,
                    StrategyName = strategyName,
                    Priority = function.Priority,
                    TargetRecoveryTime = function.RecoveryTimeObjective,
                    Tasks = tasks,
                    Workarounds = GenerateWorkarounds(function)
                };

                plan.Strategies.Add(strategy);
                return strategy;
            }
        }

        private List<string> GenerateWorkarounds(BusinessFunction function)
        {
            var workarounds = new List<string>
            {
                "Manual processing of critical transactions",
                "Use of backup communication channels",
                "Temporary relocation to alternate site"
            };

            if (function.CriticalApplications.Any())
            {
                workarounds.Add("Switch to cloud-based backup systems");
                workarounds.Add("Use paper-based forms temporarily");
            }

            return workarounds;
        }

        public async Task<BusinessImpactAnalysis> ConductBIA(string planId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_plans.TryGetValue(planId, out var plan))
                        return null;

                    var bia = new BusinessImpactAnalysis
                    {
                        OrganizationId = plan.OrganizationId,
                        Functions = plan.Functions.OrderBy(f => f.Priority).ThenBy(f => f.MaxTolerableDowntime).ToList(),
                        AnalysisDate = DateTime.UtcNow
                    };

                    // Calculate impacts
                    bia.TotalDailyImpact = plan.Functions.Sum(f => f.RevenueImpactPerHour * 24);
                    bia.TotalWeeklyImpact = bia.TotalDailyImpact * 7;

                    // Generate timeline
                    int[] hours = { 1, 4, 8, 24, 48, 72, 168 };
                    double cumulative = 0;
                    foreach (int hour in hours)
                    {
                        var affectedFunctions = plan.Functions.Where(f => f.RecoveryTimeObjective > hour).ToList();
                        cumulative += affectedFunctions.Sum(f => f.RevenueImpactPerHour) * (hour == 1 ? 1 : (hour - hours[Array.IndexOf(hours, hour) - 1]));
                        int recovered = plan.Functions.Count(f => f.RecoveryTimeObjective <= hour);

                        bia.Timeline.Add(new ImpactTimeline
                        {
                            HoursAfterDisaster = hour,
                            CumulativeFinancialImpact = cumulative,
                            OperationalCapacity = (double)recovered / plan.Functions.Count * 100,
                            FunctionsRecovered = recovered,
                            TotalFunctions = plan.Functions.Count,
                            MilestoneActions = plan.Functions.Where(f => f.RecoveryTimeObjective == hour).Select(f => $"Recover {f.Name}").ToList()
                        });
                    }

                    // Identify dependencies
                    bia.CriticalDependencies = plan.Functions
                        .SelectMany(f => f.Dependencies)
                        .Distinct()
                        .ToList();

                    return bia;
                }
            });
        }

        public BCPExercise ScheduleExercise(string planId, string name, ExerciseType type,
            string scenario, DateTime scheduledDate, List<string> objectives)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var exercise = new BCPExercise
                {
                    Name = name,
                    Type = type,
                    Scenario = scenario,
                    ScheduledDate = scheduledDate,
                    Objectives = objectives,
                    Status = "Scheduled"
                };

                plan.Exercises.Add(exercise);
                return exercise;
            }
        }

        public ExerciseResults RecordExerciseResults(string planId, string exerciseId,
            double recoveryTime, List<string> strengths, List<string> weaknesses, List<string> lessons)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var exercise = plan.Exercises.FirstOrDefault(e => e.Id == exerciseId);
                if (exercise == null) return null;

                var results = new ExerciseResults
                {
                    ActualRecoveryTime = recoveryTime,
                    TargetRecoveryTime = plan.Functions.Where(f => f.Priority == RecoveryPriority.Critical).Min(f => f.RecoveryTimeObjective),
                    Strengths = strengths,
                    Weaknesses = weaknesses,
                    LessonsLearned = lessons,
                    ObjectivesMet = exercise.Objectives.Count - weaknesses.Count,
                    TotalObjectives = exercise.Objectives.Count
                };

                results.OverallScore = (double)results.ObjectivesMet / results.TotalObjectives * 100;

                // Generate improvement actions
                results.ImprovementActions = weaknesses.Select(w => $"Address: {w}").ToList();
                if (results.ActualRecoveryTime > results.TargetRecoveryTime)
                {
                    results.ImprovementActions.Add("Review and optimize recovery procedures");
                }

                exercise.Results = results;
                exercise.CompletedDate = DateTime.UtcNow;
                exercise.Status = "Completed";

                return results;
            }
        }

        public IncidentResponse RecordIncident(DisasterType type, ImpactSeverity severity,
            string description, List<string> affectedAreas)
        {
            var incident = new IncidentResponse
            {
                IncidentType = type,
                Severity = severity,
                Description = description,
                AffectedAreas = affectedAreas,
                OccurredAt = DateTime.UtcNow,
                Status = "Active"
            };

            lock (_lock) { _incidents[incident.Id] = incident; }
            return incident;
        }

        public void ResolveIncident(string incidentId, List<string> actionsTaken,
            double actualDowntime, double financialImpact, List<string> lessons)
        {
            lock (_lock)
            {
                if (_incidents.TryGetValue(incidentId, out var incident))
                {
                    incident.ResolvedAt = DateTime.UtcNow;
                    incident.ActionsToken = actionsTaken;
                    incident.ActualDowntime = actualDowntime;
                    incident.FinancialImpact = financialImpact;
                    incident.LessonsLearned = lessons;
                    incident.Status = "Resolved";
                }
            }
        }

        public RecoveryReadiness AssessReadiness(string planId)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return null;

                var readiness = new RecoveryReadiness
                {
                    OrganizationId = plan.OrganizationId,
                    AssessmentDate = DateTime.UtcNow,
                    Gaps = new List<string>(),
                    Recommendations = new List<string>()
                };

                // Plan currentness
                int daysSinceReview = (DateTime.UtcNow - plan.LastReview).Days;
                readiness.PlanCurrentness = daysSinceReview <= 365 ? (365 - daysSinceReview) / 365.0 * 100 : 0;
                if (daysSinceReview > 365)
                {
                    readiness.Gaps.Add("BCP not reviewed in over 12 months");
                    readiness.Recommendations.Add("Conduct immediate BCP review");
                }

                // Resource readiness
                int totalResources = plan.Resources.Count;
                int readyResources = plan.Resources.Count(r => !string.IsNullOrEmpty(r.BackupResource));
                readiness.ResourceReadiness = totalResources > 0 ? (double)readyResources / totalResources * 100 : 50;
                if (readiness.ResourceReadiness < 80)
                {
                    readiness.Gaps.Add("Insufficient backup resources identified");
                    readiness.Recommendations.Add("Identify backup resources for all critical resources");
                }

                // Team preparedness (based on exercises)
                var recentExercises = plan.Exercises.Where(e => e.CompletedDate > DateTime.UtcNow.AddMonths(-12)).ToList();
                readiness.TeamPreparedness = recentExercises.Any() ? recentExercises.Average(e => e.Results?.OverallScore ?? 0) : 0;
                if (!recentExercises.Any())
                {
                    readiness.Gaps.Add("No exercises conducted in past 12 months");
                    readiness.Recommendations.Add("Schedule tabletop exercise within 30 days");
                }

                // Exercise frequency
                int exerciseCount = plan.Exercises.Count(e => e.CompletedDate > DateTime.UtcNow.AddYears(-1));
                readiness.ExerciseFrequency = Math.Min(100, exerciseCount * 25); // 4 exercises/year = 100%

                // Backup system status
                int functionsWithStrategy = plan.Strategies.Select(s => s.FunctionId).Distinct().Count();
                readiness.BackupSystemStatus = plan.Functions.Count > 0 ?
                    (double)functionsWithStrategy / plan.Functions.Count * 100 : 0;
                if (readiness.BackupSystemStatus < 100)
                {
                    readiness.Gaps.Add("Not all functions have recovery strategies");
                    readiness.Recommendations.Add("Develop recovery strategies for all business functions");
                }

                // Calculate overall score
                readiness.OverallScore = (readiness.PlanCurrentness + readiness.ResourceReadiness +
                    readiness.TeamPreparedness + readiness.ExerciseFrequency + readiness.BackupSystemStatus) / 5;

                return readiness;
            }
        }

        public double CalculateRiskExposure(string planId)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return 0;

                double totalExposure = 0;
                foreach (var scenario in plan.Scenarios)
                {
                    // Annual Loss Expectancy = probability * impact
                    totalExposure += scenario.Probability * scenario.FinancialImpact;
                }

                return totalExposure;
            }
        }

        public List<(string Function, int Gap)> IdentifyRTOGaps(string planId)
        {
            lock (_lock)
            {
                if (!_plans.TryGetValue(planId, out var plan))
                    return new List<(string, int)>();

                var gaps = new List<(string Function, int Gap)>();
                foreach (var function in plan.Functions)
                {
                    var strategy = plan.Strategies.FirstOrDefault(s => s.FunctionId == function.Id);
                    if (strategy != null && strategy.TargetRecoveryTime > function.RecoveryTimeObjective)
                    {
                        gaps.Add((function.Name, strategy.TargetRecoveryTime - function.RecoveryTimeObjective));
                    }
                    else if (strategy == null)
                    {
                        gaps.Add((function.Name, function.MaxTolerableDowntime));
                    }
                }

                return gaps.OrderByDescending(g => g.Gap).ToList();
            }
        }
    }
}
