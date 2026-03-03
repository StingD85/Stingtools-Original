// ============================================================================
// StingBIM AI - Predictive Maintenance Scheduler
// Automatically generates and optimizes maintenance schedules
// Uses equipment data, usage patterns, and failure predictions
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Automation.Maintenance
{
    /// <summary>
    /// Predictive Maintenance Scheduler
    /// Generates optimized maintenance schedules based on equipment analysis
    /// </summary>
    public class PredictiveMaintenanceScheduler
    {
        private readonly EquipmentAnalyzer _equipmentAnalyzer;
        private readonly FailurePrediction _failurePrediction;
        private readonly MaintenanceOptimizer _optimizer;
        private readonly CostCalculator _costCalculator;
        private readonly Dictionary<string, MaintenanceProfile> _maintenanceProfiles;
        private readonly Dictionary<string, EquipmentLifecycle> _lifecycleData;

        public PredictiveMaintenanceScheduler()
        {
            _equipmentAnalyzer = new EquipmentAnalyzer();
            _failurePrediction = new FailurePrediction();
            _optimizer = new MaintenanceOptimizer();
            _costCalculator = new CostCalculator();
            _maintenanceProfiles = LoadMaintenanceProfiles();
            _lifecycleData = LoadLifecycleData();
        }

        #region Schedule Generation

        /// <summary>
        /// Generate comprehensive maintenance schedule for building
        /// </summary>
        public async Task<MaintenanceSchedule> GenerateScheduleAsync(
            BuildingModel building,
            MaintenanceScheduleOptions options = null)
        {
            options ??= MaintenanceScheduleOptions.Default;

            var schedule = new MaintenanceSchedule
            {
                BuildingId = building.BuildingId,
                BuildingName = building.BuildingName,
                GeneratedAt = DateTime.UtcNow,
                SchedulePeriod = options.SchedulePeriod,
                StartDate = options.StartDate ?? DateTime.Today
            };

            // Step 1: Analyze all equipment
            var equipmentList = await _equipmentAnalyzer.AnalyzeAllEquipmentAsync(building);
            schedule.TotalEquipment = equipmentList.Count;

            // Step 2: Calculate failure risks for each equipment
            foreach (var equipment in equipmentList)
            {
                equipment.FailureRisk = await _failurePrediction.PredictFailureRiskAsync(equipment);
                equipment.RemainingLife = _failurePrediction.EstimateRemainingLife(equipment);
            }

            // Step 3: Generate maintenance tasks for each equipment
            var allTasks = new List<MaintenanceTask>();
            foreach (var equipment in equipmentList)
            {
                var tasks = GenerateMaintenanceTasks(equipment, options);
                allTasks.AddRange(tasks);
            }

            // Step 4: Optimize schedule (group tasks, balance workload)
            schedule.Tasks = _optimizer.OptimizeSchedule(allTasks, options);

            // Step 5: Calculate costs
            schedule.CostEstimate = _costCalculator.CalculateTotalCost(schedule.Tasks);

            // Step 6: Generate calendar view
            schedule.CalendarView = GenerateCalendarView(schedule.Tasks, schedule.StartDate, options.SchedulePeriod);

            // Step 7: Identify critical equipment requiring attention
            schedule.CriticalEquipment = equipmentList
                .Where(e => e.FailureRisk.RiskLevel == RiskLevel.High ||
                           e.FailureRisk.RiskLevel == RiskLevel.Critical)
                .OrderByDescending(e => e.FailureRisk.RiskScore)
                .ToList();

            // Step 8: Generate recommendations
            schedule.Recommendations = GenerateRecommendations(equipmentList, schedule.Tasks);

            return schedule;
        }

        /// <summary>
        /// Generate maintenance tasks for specific equipment
        /// </summary>
        public List<MaintenanceTask> GenerateMaintenanceTasks(
            Equipment equipment,
            MaintenanceScheduleOptions options)
        {
            var tasks = new List<MaintenanceTask>();

            // Get maintenance profile for equipment type
            if (!_maintenanceProfiles.TryGetValue(equipment.EquipmentType, out var profile))
            {
                profile = _maintenanceProfiles["Generic"];
            }

            var startDate = options.StartDate ?? DateTime.Today;
            var endDate = startDate.AddMonths(options.SchedulePeriod);

            // Generate scheduled maintenance tasks
            foreach (var activity in profile.ScheduledActivities)
            {
                var taskDates = CalculateTaskDates(
                    startDate, endDate, activity.Frequency, equipment.LastMaintenanceDate);

                foreach (var date in taskDates)
                {
                    tasks.Add(new MaintenanceTask
                    {
                        TaskId = Guid.NewGuid().ToString(),
                        EquipmentId = equipment.EquipmentId,
                        EquipmentName = equipment.Name,
                        EquipmentLocation = equipment.Location,
                        TaskType = activity.ActivityType,
                        Description = activity.Description,
                        ScheduledDate = date,
                        EstimatedDuration = activity.Duration,
                        RequiredSkills = activity.RequiredSkills,
                        RequiredParts = activity.RequiredParts,
                        Priority = DeterminePriority(equipment, activity),
                        EstimatedCost = activity.EstimatedCost,
                        SafetyRequirements = activity.SafetyRequirements,
                        AccessRequirements = GetAccessRequirements(equipment),
                        Category = equipment.Category,
                        System = equipment.System
                    });
                }
            }

            // Add condition-based tasks if risk is elevated
            if (equipment.FailureRisk.RiskLevel >= RiskLevel.Medium)
            {
                tasks.AddRange(GenerateConditionBasedTasks(equipment, startDate, profile));
            }

            // Add replacement planning if near end of life
            if (equipment.RemainingLife != null && equipment.RemainingLife.Value.TotalDays < 365)
            {
                tasks.Add(GenerateReplacementPlanningTask(equipment));
            }

            return tasks;
        }

        /// <summary>
        /// Update schedule based on completed work
        /// </summary>
        public MaintenanceSchedule UpdateSchedule(
            MaintenanceSchedule currentSchedule,
            List<CompletedTask> completedTasks,
            List<Equipment> updatedEquipment)
        {
            // Remove completed tasks
            foreach (var completed in completedTasks)
            {
                var task = currentSchedule.Tasks.FirstOrDefault(t => t.TaskId == completed.TaskId);
                if (task != null)
                {
                    task.Status = MaintenanceTaskStatus.Completed;
                    task.ActualCompletionDate = completed.CompletedDate;
                    task.ActualDuration = completed.ActualDuration;
                    task.ActualCost = completed.ActualCost;
                    task.Notes = completed.Notes;
                }
            }

            // Update equipment records and recalculate risks
            foreach (var equipment in updatedEquipment)
            {
                var existing = currentSchedule.CriticalEquipment.FirstOrDefault(
                    e => e.EquipmentId == equipment.EquipmentId);
                if (existing != null)
                {
                    existing.LastMaintenanceDate = equipment.LastMaintenanceDate;
                    existing.Condition = equipment.Condition;
                }
            }

            // Recalculate cost estimates
            currentSchedule.CostEstimate = _costCalculator.CalculateTotalCost(
                currentSchedule.Tasks.Where(t => t.Status != MaintenanceTaskStatus.Completed).ToList());

            return currentSchedule;
        }

        #endregion

        #region Failure Prediction

        /// <summary>
        /// Predict equipment that will likely fail in the next period
        /// </summary>
        public async Task<List<FailurePredictionResult>> PredictUpcomingFailuresAsync(
            BuildingModel building,
            int monthsAhead = 6)
        {
            var predictions = new List<FailurePredictionResult>();
            var equipment = await _equipmentAnalyzer.AnalyzeAllEquipmentAsync(building);

            foreach (var item in equipment)
            {
                var prediction = await _failurePrediction.PredictFailureAsync(item, monthsAhead);
                if (prediction.FailureProbability > 0.3) // 30% threshold
                {
                    predictions.Add(prediction);
                }
            }

            return predictions.OrderByDescending(p => p.FailureProbability).ToList();
        }

        /// <summary>
        /// Analyze failure patterns and root causes
        /// </summary>
        public FailureAnalysis AnalyzeFailurePatterns(
            List<HistoricalFailure> failures,
            List<Equipment> equipment)
        {
            var analysis = new FailureAnalysis
            {
                AnalysisDate = DateTime.UtcNow,
                TotalFailures = failures.Count
            };

            // Group by equipment type
            analysis.FailuresByType = failures
                .GroupBy(f => f.EquipmentType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by system
            analysis.FailuresBySystem = failures
                .GroupBy(f => f.System)
                .ToDictionary(g => g.Key, g => g.Count());

            // Identify common causes
            analysis.CommonCauses = failures
                .GroupBy(f => f.Cause)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new FailureCause
                {
                    Cause = g.Key,
                    Occurrences = g.Count(),
                    Percentage = (double)g.Count() / failures.Count * 100
                })
                .ToList();

            // Calculate mean time between failures
            analysis.MTBFByType = CalculateMTBF(failures, equipment);

            // Seasonal patterns
            analysis.SeasonalPatterns = AnalyzeSeasonalPatterns(failures);

            // Cost impact
            analysis.TotalFailureCost = failures.Sum(f => f.RepairCost + f.DowntimeCost);
            analysis.AverageRepairTime = TimeSpan.FromHours(
                failures.Average(f => f.RepairDuration.TotalHours));

            return analysis;
        }

        #endregion

        #region Cost Optimization

        /// <summary>
        /// Optimize maintenance strategy to minimize total cost
        /// </summary>
        public MaintenanceStrategyRecommendation OptimizeStrategy(
            BuildingModel building,
            MaintenanceSchedule currentSchedule,
            BudgetConstraints budget)
        {
            var recommendation = new MaintenanceStrategyRecommendation
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Analyze current costs
            var currentCosts = _costCalculator.AnalyzeCosts(currentSchedule);
            recommendation.CurrentAnnualCost = currentCosts.TotalAnnual;

            // Calculate optimal maintenance intervals
            var optimalIntervals = CalculateOptimalIntervals(
                currentSchedule.CriticalEquipment,
                currentCosts);

            // Generate alternative strategies
            var strategies = new List<MaintenanceStrategy>
            {
                GeneratePreventiveStrategy(currentSchedule),
                GeneratePredictiveStrategy(currentSchedule),
                GenerateReactiveStrategy(currentSchedule),
                GenerateHybridStrategy(currentSchedule)
            };

            // Evaluate each strategy
            foreach (var strategy in strategies)
            {
                strategy.EstimatedAnnualCost = _costCalculator.EstimateStrategyCost(strategy);
                strategy.RiskScore = CalculateStrategyRisk(strategy);
                strategy.ROI = CalculateROI((double)currentCosts.TotalAnnual, (double)strategy.EstimatedAnnualCost);
            }

            // Recommend best strategy within budget
            recommendation.RecommendedStrategy = strategies
                .Where(s => s.EstimatedAnnualCost <= budget.AnnualBudget)
                .OrderBy(s => s.RiskScore)
                .ThenBy(s => s.EstimatedAnnualCost)
                .FirstOrDefault() ?? strategies.OrderBy(s => s.EstimatedAnnualCost).First();

            recommendation.AlternativeStrategies = strategies
                .Where(s => s != recommendation.RecommendedStrategy)
                .ToList();

            recommendation.PotentialSavings = currentCosts.TotalAnnual -
                recommendation.RecommendedStrategy.EstimatedAnnualCost;

            // Equipment-specific recommendations
            recommendation.EquipmentRecommendations = GenerateEquipmentRecommendations(
                currentSchedule.CriticalEquipment, optimalIntervals);

            return recommendation;
        }

        #endregion

        #region Private Helper Methods

        private List<DateTime> CalculateTaskDates(
            DateTime startDate,
            DateTime endDate,
            MaintenanceFrequency frequency,
            DateTime? lastMaintenance)
        {
            var dates = new List<DateTime>();
            DateTime nextDate;

            if (lastMaintenance.HasValue)
            {
                nextDate = GetNextMaintenanceDate(lastMaintenance.Value, frequency);
                if (nextDate < startDate)
                    nextDate = startDate;
            }
            else
            {
                nextDate = startDate;
            }

            while (nextDate <= endDate)
            {
                dates.Add(nextDate);
                nextDate = GetNextMaintenanceDate(nextDate, frequency);
            }

            return dates;
        }

        private DateTime GetNextMaintenanceDate(DateTime fromDate, MaintenanceFrequency frequency)
        {
            return frequency switch
            {
                MaintenanceFrequency.Daily => fromDate.AddDays(1),
                MaintenanceFrequency.Weekly => fromDate.AddDays(7),
                MaintenanceFrequency.Biweekly => fromDate.AddDays(14),
                MaintenanceFrequency.Monthly => fromDate.AddMonths(1),
                MaintenanceFrequency.Quarterly => fromDate.AddMonths(3),
                MaintenanceFrequency.Biannual => fromDate.AddMonths(6),
                MaintenanceFrequency.Annual => fromDate.AddYears(1),
                MaintenanceFrequency.Biennial => fromDate.AddYears(2),
                MaintenanceFrequency.FiveYearly => fromDate.AddYears(5),
                _ => fromDate.AddMonths(1)
            };
        }

        private TaskPriority DeterminePriority(Equipment equipment, MaintenanceActivity activity)
        {
            // Consider equipment criticality and failure risk
            if (equipment.Criticality == Criticality.Critical ||
                equipment.FailureRisk?.RiskLevel == RiskLevel.Critical)
            {
                return TaskPriority.Critical;
            }

            if (equipment.FailureRisk?.RiskLevel == RiskLevel.High ||
                activity.IsSafetyRelated)
            {
                return TaskPriority.High;
            }

            if (equipment.Criticality == Criticality.Important ||
                equipment.FailureRisk?.RiskLevel == RiskLevel.Medium)
            {
                return TaskPriority.Medium;
            }

            return TaskPriority.Low;
        }

        private string GetAccessRequirements(Equipment equipment)
        {
            var requirements = new List<string>();

            if (equipment.Location.Contains("Roof"))
                requirements.Add("Roof access required");
            if (equipment.Location.Contains("Plant Room"))
                requirements.Add("Plant room key required");
            if (equipment.Location.Contains("Ceiling"))
                requirements.Add("Ladder/scaffold required");
            if (equipment.HeightAboveFloor > 3)
                requirements.Add("Working at height permit");
            if (equipment.RequiresIsolation)
                requirements.Add("Electrical/mechanical isolation required");

            return string.Join("; ", requirements);
        }

        private List<MaintenanceTask> GenerateConditionBasedTasks(
            Equipment equipment,
            DateTime startDate,
            MaintenanceProfile profile)
        {
            var tasks = new List<MaintenanceTask>();

            // Additional inspection based on condition
            tasks.Add(new MaintenanceTask
            {
                TaskId = Guid.NewGuid().ToString(),
                EquipmentId = equipment.EquipmentId,
                EquipmentName = equipment.Name,
                TaskType = MaintenanceTaskType.Inspection,
                Description = $"Condition assessment - elevated failure risk ({equipment.FailureRisk.RiskScore:F0}%)",
                ScheduledDate = startDate.AddDays(7),
                EstimatedDuration = TimeSpan.FromHours(1),
                Priority = TaskPriority.High,
                Category = equipment.Category,
                System = equipment.System
            });

            // Vibration analysis for rotating equipment
            if (equipment.HasRotatingComponents)
            {
                tasks.Add(new MaintenanceTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    EquipmentId = equipment.EquipmentId,
                    EquipmentName = equipment.Name,
                    TaskType = MaintenanceTaskType.PredictiveTesting,
                    Description = "Vibration analysis",
                    ScheduledDate = startDate.AddDays(14),
                    EstimatedDuration = TimeSpan.FromHours(2),
                    Priority = TaskPriority.High,
                    RequiredSkills = new List<string> { "Vibration analyst" },
                    Category = equipment.Category,
                    System = equipment.System
                });
            }

            // Thermographic inspection for electrical
            if (equipment.Category == "Electrical")
            {
                tasks.Add(new MaintenanceTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    EquipmentId = equipment.EquipmentId,
                    EquipmentName = equipment.Name,
                    TaskType = MaintenanceTaskType.PredictiveTesting,
                    Description = "Thermographic inspection",
                    ScheduledDate = startDate.AddDays(21),
                    EstimatedDuration = TimeSpan.FromHours(1),
                    Priority = TaskPriority.Medium,
                    RequiredSkills = new List<string> { "Thermographer" },
                    Category = equipment.Category,
                    System = equipment.System
                });
            }

            return tasks;
        }

        private MaintenanceTask GenerateReplacementPlanningTask(Equipment equipment)
        {
            return new MaintenanceTask
            {
                TaskId = Guid.NewGuid().ToString(),
                EquipmentId = equipment.EquipmentId,
                EquipmentName = equipment.Name,
                TaskType = MaintenanceTaskType.ReplacementPlanning,
                Description = $"Plan replacement - estimated {equipment.RemainingLife?.TotalDays:F0} days remaining life",
                ScheduledDate = DateTime.Today.AddDays(30),
                EstimatedDuration = TimeSpan.FromHours(4),
                Priority = TaskPriority.High,
                Category = equipment.Category,
                System = equipment.System,
                Notes = $"Expected replacement cost: {equipment.ReplacementCost:C}"
            };
        }

        private CalendarView GenerateCalendarView(
            List<MaintenanceTask> tasks,
            DateTime startDate,
            int months)
        {
            var calendar = new CalendarView
            {
                StartDate = startDate,
                EndDate = startDate.AddMonths(months)
            };

            // Group by month
            calendar.MonthlyView = tasks
                .Where(t => t.Status != MaintenanceTaskStatus.Completed)
                .GroupBy(t => new { t.ScheduledDate.Year, t.ScheduledDate.Month })
                .Select(g => new MonthSummary
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TaskCount = g.Count(),
                    CriticalCount = g.Count(t => t.Priority == TaskPriority.Critical),
                    EstimatedHours = g.Sum(t => t.EstimatedDuration.TotalHours),
                    EstimatedCost = g.Sum(t => t.EstimatedCost)
                })
                .ToList();

            // Group by week for near-term view
            calendar.WeeklyView = tasks
                .Where(t => t.ScheduledDate <= startDate.AddMonths(3) &&
                           t.Status != MaintenanceTaskStatus.Completed)
                .GroupBy(t => GetWeekNumber(t.ScheduledDate))
                .Select(g => new WeekSummary
                {
                    WeekNumber = g.Key,
                    Tasks = g.ToList(),
                    TotalHours = g.Sum(t => t.EstimatedDuration.TotalHours)
                })
                .ToList();

            return calendar;
        }

        private int GetWeekNumber(DateTime date)
        {
            return (date.DayOfYear - 1) / 7 + 1;
        }

        private List<MaintenanceRecommendation> GenerateRecommendations(
            List<Equipment> equipment,
            List<MaintenanceTask> tasks)
        {
            var recommendations = new List<MaintenanceRecommendation>();

            // Critical equipment recommendations
            var critical = equipment.Where(e =>
                e.FailureRisk?.RiskLevel == RiskLevel.Critical).ToList();

            if (critical.Any())
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Priority = "Critical",
                    Category = "Equipment Health",
                    Recommendation = $"{critical.Count} equipment items at critical failure risk - immediate attention required",
                    AffectedEquipment = critical.Select(e => e.Name).ToList(),
                    EstimatedCost = critical.Sum(e => e.RepairCost ?? 0)
                });
            }

            // End of life equipment
            var endOfLife = equipment.Where(e =>
                e.RemainingLife.HasValue && e.RemainingLife.Value.TotalDays < 180).ToList();

            if (endOfLife.Any())
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Priority = "High",
                    Category = "Replacement Planning",
                    Recommendation = $"{endOfLife.Count} equipment items approaching end of life - budget for replacement",
                    AffectedEquipment = endOfLife.Select(e => e.Name).ToList(),
                    EstimatedCost = endOfLife.Sum(e => e.ReplacementCost ?? 0)
                });
            }

            // Workload balancing
            var monthlyTasks = tasks
                .GroupBy(t => t.ScheduledDate.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();

            var avgTasks = monthlyTasks.Average(m => m.Count);
            var peakMonths = monthlyTasks.Where(m => m.Count > avgTasks * 1.5).ToList();

            if (peakMonths.Any())
            {
                recommendations.Add(new MaintenanceRecommendation
                {
                    Priority = "Medium",
                    Category = "Resource Planning",
                    Recommendation = $"Peak workload in months: {string.Join(", ", peakMonths.Select(m => m.Month))} - consider additional resources",
                    EstimatedCost = 0
                });
            }

            return recommendations;
        }

        private Dictionary<string, double> CalculateMTBF(
            List<HistoricalFailure> failures,
            List<Equipment> equipment)
        {
            var mtbf = new Dictionary<string, double>();

            var grouped = failures.GroupBy(f => f.EquipmentType);
            foreach (var group in grouped)
            {
                var equipCount = equipment.Count(e => e.EquipmentType == group.Key);
                var totalOperatingTime = equipCount * 365 * 24; // Approximate annual hours
                mtbf[group.Key] = totalOperatingTime / group.Count();
            }

            return mtbf;
        }

        private Dictionary<string, double> AnalyzeSeasonalPatterns(List<HistoricalFailure> failures)
        {
            return failures
                .GroupBy(f => f.FailureDate.Month)
                .ToDictionary(g => GetMonthName(g.Key), g => (double)g.Count());
        }

        private string GetMonthName(int month)
        {
            return new DateTime(2000, month, 1).ToString("MMMM");
        }

        private Dictionary<string, TimeSpan> CalculateOptimalIntervals(
            List<Equipment> equipment,
            CostAnalysis currentCosts)
        {
            var intervals = new Dictionary<string, TimeSpan>();

            foreach (var item in equipment)
            {
                // Economic optimal interval calculation
                // Balance between preventive cost and failure cost
                if (_lifecycleData.TryGetValue(item.EquipmentType, out var lifecycle))
                {
                    var optimalDays = Math.Sqrt(
                        2 * (double)lifecycle.PreventiveMaintenanceCost /
                        (lifecycle.FailureRate * (double)lifecycle.FailureCost));

                    intervals[item.EquipmentId] = TimeSpan.FromDays(optimalDays);
                }
            }

            return intervals;
        }

        private MaintenanceStrategy GeneratePreventiveStrategy(MaintenanceSchedule schedule)
        {
            return new MaintenanceStrategy
            {
                Name = "Preventive Maintenance",
                Description = "Time-based scheduled maintenance for all equipment",
                MaintenanceApproach = MaintenanceApproach.Preventive,
                IntervalReduction = 0
            };
        }

        private MaintenanceStrategy GeneratePredictiveStrategy(MaintenanceSchedule schedule)
        {
            return new MaintenanceStrategy
            {
                Name = "Predictive Maintenance",
                Description = "Condition-based maintenance using monitoring and analytics",
                MaintenanceApproach = MaintenanceApproach.Predictive,
                RequiresMonitoring = true,
                MonitoringInvestment = schedule.TotalEquipment * 500 // $500 per equipment for sensors
            };
        }

        private MaintenanceStrategy GenerateReactiveStrategy(MaintenanceSchedule schedule)
        {
            return new MaintenanceStrategy
            {
                Name = "Reactive Maintenance",
                Description = "Run-to-failure for non-critical equipment",
                MaintenanceApproach = MaintenanceApproach.Reactive
            };
        }

        private MaintenanceStrategy GenerateHybridStrategy(MaintenanceSchedule schedule)
        {
            return new MaintenanceStrategy
            {
                Name = "Hybrid Strategy",
                Description = "Predictive for critical, preventive for important, reactive for low-risk",
                MaintenanceApproach = MaintenanceApproach.Hybrid,
                RequiresMonitoring = true
            };
        }

        private double CalculateStrategyRisk(MaintenanceStrategy strategy)
        {
            return strategy.MaintenanceApproach switch
            {
                MaintenanceApproach.Preventive => 20,
                MaintenanceApproach.Predictive => 15,
                MaintenanceApproach.Reactive => 60,
                MaintenanceApproach.Hybrid => 25,
                _ => 30
            };
        }

        private double CalculateROI(double currentCost, double newCost)
        {
            if (newCost >= currentCost) return 0;
            return (currentCost - newCost) / currentCost * 100;
        }

        private List<EquipmentRecommendation> GenerateEquipmentRecommendations(
            List<Equipment> equipment,
            Dictionary<string, TimeSpan> optimalIntervals)
        {
            return equipment.Select(e => new EquipmentRecommendation
            {
                EquipmentId = e.EquipmentId,
                EquipmentName = e.Name,
                CurrentInterval = e.MaintenanceInterval,
                RecommendedInterval = optimalIntervals.GetValueOrDefault(e.EquipmentId, e.MaintenanceInterval),
                RecommendedAction = DetermineRecommendedAction(e),
                PotentialSavings = CalculatePotentialSavings(e, optimalIntervals)
            }).ToList();
        }

        private string DetermineRecommendedAction(Equipment equipment)
        {
            if (equipment.FailureRisk?.RiskLevel == RiskLevel.Critical)
                return "Immediate inspection and repair";
            if (equipment.RemainingLife?.TotalDays < 180)
                return "Plan replacement";
            if (equipment.FailureRisk?.RiskLevel == RiskLevel.High)
                return "Increase inspection frequency";
            return "Continue current maintenance plan";
        }

        private decimal CalculatePotentialSavings(
            Equipment equipment,
            Dictionary<string, TimeSpan> optimalIntervals)
        {
            // Simplified savings calculation
            return 0;
        }

        #endregion

        #region Database Loading

        private Dictionary<string, MaintenanceProfile> LoadMaintenanceProfiles()
        {
            return new Dictionary<string, MaintenanceProfile>(StringComparer.OrdinalIgnoreCase)
            {
                { "AHU", new MaintenanceProfile
                    {
                        EquipmentType = "AHU",
                        ScheduledActivities = new List<MaintenanceActivity>
                        {
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.FilterChange, Frequency = MaintenanceFrequency.Monthly, Duration = TimeSpan.FromHours(1), Description = "Replace air filters", EstimatedCost = 150 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.BeltInspection, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(2), Description = "Inspect and adjust belt tension", EstimatedCost = 100 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.CoilCleaning, Frequency = MaintenanceFrequency.Annual, Duration = TimeSpan.FromHours(4), Description = "Clean heating and cooling coils", EstimatedCost = 500 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.BearingLubrication, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(1), Description = "Lubricate fan bearings", EstimatedCost = 75 }
                        }
                    }
                },
                { "Chiller", new MaintenanceProfile
                    {
                        EquipmentType = "Chiller",
                        ScheduledActivities = new List<MaintenanceActivity>
                        {
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Inspection, Frequency = MaintenanceFrequency.Monthly, Duration = TimeSpan.FromHours(2), Description = "Operating parameter check", EstimatedCost = 200 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.OilAnalysis, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(1), Description = "Compressor oil analysis", EstimatedCost = 150 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.TubeInspection, Frequency = MaintenanceFrequency.Annual, Duration = TimeSpan.FromHours(8), Description = "Eddy current tube testing", EstimatedCost = 2000 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.RefrigerantCheck, Frequency = MaintenanceFrequency.Biannual, Duration = TimeSpan.FromHours(2), Description = "Refrigerant level and leak check", EstimatedCost = 300, IsSafetyRelated = true }
                        }
                    }
                },
                { "Lift", new MaintenanceProfile
                    {
                        EquipmentType = "Lift",
                        ScheduledActivities = new List<MaintenanceActivity>
                        {
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Inspection, Frequency = MaintenanceFrequency.Monthly, Duration = TimeSpan.FromHours(2), Description = "Monthly safety inspection", EstimatedCost = 250, IsSafetyRelated = true },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Lubrication, Frequency = MaintenanceFrequency.Monthly, Duration = TimeSpan.FromHours(1), Description = "Guide rails and doors lubrication", EstimatedCost = 100 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.LoadTest, Frequency = MaintenanceFrequency.Annual, Duration = TimeSpan.FromHours(4), Description = "Annual load test", EstimatedCost = 800, IsSafetyRelated = true },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.RopeInspection, Frequency = MaintenanceFrequency.Biannual, Duration = TimeSpan.FromHours(3), Description = "Wire rope inspection and measurement", EstimatedCost = 400, IsSafetyRelated = true }
                        }
                    }
                },
                { "Generator", new MaintenanceProfile
                    {
                        EquipmentType = "Generator",
                        ScheduledActivities = new List<MaintenanceActivity>
                        {
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Testing, Frequency = MaintenanceFrequency.Weekly, Duration = TimeSpan.FromHours(0.5), Description = "Weekly run test", EstimatedCost = 50 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.OilChange, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(2), Description = "Oil and filter change", EstimatedCost = 300 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.LoadBankTest, Frequency = MaintenanceFrequency.Annual, Duration = TimeSpan.FromHours(4), Description = "Full load bank test", EstimatedCost = 1000 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.BatteryService, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(1), Description = "Starting battery service", EstimatedCost = 100 }
                        }
                    }
                },
                { "Generic", new MaintenanceProfile
                    {
                        EquipmentType = "Generic",
                        ScheduledActivities = new List<MaintenanceActivity>
                        {
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Inspection, Frequency = MaintenanceFrequency.Quarterly, Duration = TimeSpan.FromHours(1), Description = "General inspection", EstimatedCost = 100 },
                            new MaintenanceActivity { ActivityType = MaintenanceTaskType.Cleaning, Frequency = MaintenanceFrequency.Monthly, Duration = TimeSpan.FromHours(0.5), Description = "Cleaning and housekeeping", EstimatedCost = 50 }
                        }
                    }
                }
            };
        }

        private Dictionary<string, EquipmentLifecycle> LoadLifecycleData()
        {
            return new Dictionary<string, EquipmentLifecycle>(StringComparer.OrdinalIgnoreCase)
            {
                { "AHU", new EquipmentLifecycle { EquipmentType = "AHU", ExpectedLifeYears = 20, FailureRate = 0.05, PreventiveMaintenanceCost = 2000, FailureCost = 15000 } },
                { "Chiller", new EquipmentLifecycle { EquipmentType = "Chiller", ExpectedLifeYears = 25, FailureRate = 0.04, PreventiveMaintenanceCost = 5000, FailureCost = 50000 } },
                { "Boiler", new EquipmentLifecycle { EquipmentType = "Boiler", ExpectedLifeYears = 25, FailureRate = 0.03, PreventiveMaintenanceCost = 3000, FailureCost = 30000 } },
                { "Pump", new EquipmentLifecycle { EquipmentType = "Pump", ExpectedLifeYears = 15, FailureRate = 0.08, PreventiveMaintenanceCost = 500, FailureCost = 5000 } },
                { "Lift", new EquipmentLifecycle { EquipmentType = "Lift", ExpectedLifeYears = 25, FailureRate = 0.02, PreventiveMaintenanceCost = 8000, FailureCost = 25000 } },
                { "Generator", new EquipmentLifecycle { EquipmentType = "Generator", ExpectedLifeYears = 30, FailureRate = 0.03, PreventiveMaintenanceCost = 3000, FailureCost = 20000 } },
                { "Transformer", new EquipmentLifecycle { EquipmentType = "Transformer", ExpectedLifeYears = 35, FailureRate = 0.02, PreventiveMaintenanceCost = 2000, FailureCost = 100000 } }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class EquipmentAnalyzer
    {
        public async Task<List<Equipment>> AnalyzeAllEquipmentAsync(BuildingModel building)
        {
            await Task.Delay(10);
            return new List<Equipment>();
        }
    }

    public class FailurePrediction
    {
        public async Task<FailureRisk> PredictFailureRiskAsync(Equipment equipment)
        {
            await Task.Delay(10);

            // Calculate risk based on age, condition, maintenance history
            double riskScore = 0;

            // Age factor
            if (equipment.Age.TotalDays > equipment.ExpectedLifeDays * 0.8)
                riskScore += 30;
            else if (equipment.Age.TotalDays > equipment.ExpectedLifeDays * 0.6)
                riskScore += 15;

            // Condition factor
            riskScore += equipment.Condition switch
            {
                EquipmentCondition.Poor => 40,
                EquipmentCondition.Fair => 20,
                EquipmentCondition.Good => 5,
                _ => 0
            };

            // Maintenance history factor
            if (equipment.MissedMaintenanceCount > 3)
                riskScore += 20;

            return new FailureRisk
            {
                RiskScore = Math.Min(100, riskScore),
                RiskLevel = riskScore > 70 ? RiskLevel.Critical :
                           riskScore > 50 ? RiskLevel.High :
                           riskScore > 30 ? RiskLevel.Medium : RiskLevel.Low
            };
        }

        public TimeSpan? EstimateRemainingLife(Equipment equipment)
        {
            var remainingDays = equipment.ExpectedLifeDays - equipment.Age.TotalDays;

            // Adjust for condition
            remainingDays *= equipment.Condition switch
            {
                EquipmentCondition.Excellent => 1.2,
                EquipmentCondition.Good => 1.0,
                EquipmentCondition.Fair => 0.8,
                EquipmentCondition.Poor => 0.5,
                _ => 1.0
            };

            return remainingDays > 0 ? TimeSpan.FromDays(remainingDays) : TimeSpan.Zero;
        }

        public async Task<FailurePredictionResult> PredictFailureAsync(Equipment equipment, int monthsAhead)
        {
            await Task.Delay(10);

            var risk = await PredictFailureRiskAsync(equipment);
            var monthlyIncrease = 2.0; // Risk increases 2% per month

            return new FailurePredictionResult
            {
                EquipmentId = equipment.EquipmentId,
                EquipmentName = equipment.Name,
                FailureProbability = Math.Min(1.0, (risk.RiskScore + monthlyIncrease * monthsAhead) / 100),
                PredictedFailureMode = DetermineLikelyFailureMode(equipment),
                RecommendedAction = risk.RiskLevel >= RiskLevel.High ? "Immediate inspection" : "Monitor condition"
            };
        }

        private string DetermineLikelyFailureMode(Equipment equipment)
        {
            return equipment.EquipmentType switch
            {
                "AHU" => "Fan bearing failure or belt wear",
                "Chiller" => "Compressor failure or refrigerant leak",
                "Pump" => "Seal failure or impeller wear",
                "Lift" => "Control system or door mechanism",
                _ => "General wear"
            };
        }
    }

    public class MaintenanceOptimizer
    {
        public List<MaintenanceTask> OptimizeSchedule(
            List<MaintenanceTask> tasks,
            MaintenanceScheduleOptions options)
        {
            // Group tasks by location and date for efficiency
            var optimized = tasks
                .OrderBy(t => t.ScheduledDate)
                .ThenBy(t => t.EquipmentLocation)
                .ThenByDescending(t => t.Priority)
                .ToList();

            // Balance workload across days
            if (options.BalanceWorkload)
            {
                optimized = BalanceWorkload(optimized, options.MaxTasksPerDay);
            }

            return optimized;
        }

        private List<MaintenanceTask> BalanceWorkload(List<MaintenanceTask> tasks, int maxPerDay)
        {
            var balanced = new List<MaintenanceTask>();
            var dailyCount = new Dictionary<DateTime, int>();

            foreach (var task in tasks.OrderByDescending(t => t.Priority))
            {
                var date = task.ScheduledDate.Date;
                var count = dailyCount.GetValueOrDefault(date, 0);

                if (count >= maxPerDay)
                {
                    // Move to next available day
                    while (dailyCount.GetValueOrDefault(date, 0) >= maxPerDay)
                    {
                        date = date.AddDays(1);
                    }
                    task.ScheduledDate = date;
                }

                dailyCount[date] = dailyCount.GetValueOrDefault(date, 0) + 1;
                balanced.Add(task);
            }

            return balanced.OrderBy(t => t.ScheduledDate).ToList();
        }
    }

    public class CostCalculator
    {
        public CostEstimate CalculateTotalCost(List<MaintenanceTask> tasks)
        {
            return new CostEstimate
            {
                LaborCost = (decimal)tasks.Sum(t => t.EstimatedDuration.TotalHours * 75), // $75/hour
                MaterialCost = tasks.Sum(t => t.EstimatedCost * 0.4m),
                ContractorCost = tasks.Where(t => t.RequiresContractor).Sum(t => t.EstimatedCost),
                TotalCost = tasks.Sum(t => t.EstimatedCost)
            };
        }

        public CostAnalysis AnalyzeCosts(MaintenanceSchedule schedule)
        {
            var estimate = CalculateTotalCost(schedule.Tasks);
            return new CostAnalysis
            {
                TotalAnnual = estimate.TotalCost * 12 / schedule.SchedulePeriod,
                ByCategory = schedule.Tasks
                    .GroupBy(t => t.Category)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.EstimatedCost)),
                BySystem = schedule.Tasks
                    .GroupBy(t => t.System)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.EstimatedCost))
            };
        }

        public decimal EstimateStrategyCost(MaintenanceStrategy strategy)
        {
            // Simplified cost estimation
            var baseCost = strategy.MaintenanceApproach switch
            {
                MaintenanceApproach.Preventive => 100000,
                MaintenanceApproach.Predictive => 80000,
                MaintenanceApproach.Reactive => 120000,
                MaintenanceApproach.Hybrid => 90000,
                _ => 100000
            };

            return baseCost + (strategy.MonitoringInvestment / 5); // Amortize over 5 years
        }
    }

    #endregion

    #region Data Models

    public class BuildingModel
    {
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public List<Equipment> Equipment { get; set; }
    }

    public class Equipment
    {
        public string EquipmentId { get; set; }
        public string Name { get; set; }
        public string EquipmentType { get; set; }
        public string Category { get; set; }
        public string System { get; set; }
        public string Location { get; set; }
        public DateTime InstallDate { get; set; }
        public TimeSpan Age => DateTime.Now - InstallDate;
        public double ExpectedLifeDays { get; set; }
        public EquipmentCondition Condition { get; set; }
        public Criticality Criticality { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public TimeSpan MaintenanceInterval { get; set; }
        public int MissedMaintenanceCount { get; set; }
        public FailureRisk FailureRisk { get; set; }
        public TimeSpan? RemainingLife { get; set; }
        public bool HasRotatingComponents { get; set; }
        public bool RequiresIsolation { get; set; }
        public double HeightAboveFloor { get; set; }
        public decimal? RepairCost { get; set; }
        public decimal? ReplacementCost { get; set; }
    }

    public class MaintenanceSchedule
    {
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int SchedulePeriod { get; set; }
        public DateTime StartDate { get; set; }
        public int TotalEquipment { get; set; }
        public List<MaintenanceTask> Tasks { get; set; } = new List<MaintenanceTask>();
        public CostEstimate CostEstimate { get; set; }
        public CalendarView CalendarView { get; set; }
        public List<Equipment> CriticalEquipment { get; set; }
        public List<MaintenanceRecommendation> Recommendations { get; set; }
    }

    public class MaintenanceScheduleOptions
    {
        public DateTime? StartDate { get; set; }
        public int SchedulePeriod { get; set; } = 12; // months
        public bool BalanceWorkload { get; set; } = true;
        public int MaxTasksPerDay { get; set; } = 10;
        public bool IncludePredictive { get; set; } = true;

        public static MaintenanceScheduleOptions Default => new MaintenanceScheduleOptions();
    }

    public class MaintenanceTask
    {
        public string TaskId { get; set; }
        public string EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public string EquipmentLocation { get; set; }
        public MaintenanceTaskType TaskType { get; set; }
        public string Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public List<string> RequiredSkills { get; set; }
        public List<string> RequiredParts { get; set; }
        public TaskPriority Priority { get; set; }
        public decimal EstimatedCost { get; set; }
        public string SafetyRequirements { get; set; }
        public string AccessRequirements { get; set; }
        public string Category { get; set; }
        public string System { get; set; }
        public bool RequiresContractor { get; set; }
        public MaintenanceTaskStatus Status { get; set; } = MaintenanceTaskStatus.Scheduled;
        public DateTime? ActualCompletionDate { get; set; }
        public TimeSpan? ActualDuration { get; set; }
        public decimal? ActualCost { get; set; }
        public string Notes { get; set; }
    }

    public class MaintenanceProfile
    {
        public string EquipmentType { get; set; }
        public List<MaintenanceActivity> ScheduledActivities { get; set; }
    }

    public class MaintenanceActivity
    {
        public MaintenanceTaskType ActivityType { get; set; }
        public MaintenanceFrequency Frequency { get; set; }
        public TimeSpan Duration { get; set; }
        public string Description { get; set; }
        public decimal EstimatedCost { get; set; }
        public List<string> RequiredSkills { get; set; }
        public List<string> RequiredParts { get; set; }
        public string SafetyRequirements { get; set; }
        public bool IsSafetyRelated { get; set; }
    }

    public class EquipmentLifecycle
    {
        public string EquipmentType { get; set; }
        public int ExpectedLifeYears { get; set; }
        public double FailureRate { get; set; }
        public decimal PreventiveMaintenanceCost { get; set; }
        public decimal FailureCost { get; set; }
    }

    public class FailureRisk
    {
        public double RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class FailurePredictionResult
    {
        public string EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public double FailureProbability { get; set; }
        public string PredictedFailureMode { get; set; }
        public string RecommendedAction { get; set; }
    }

    public class FailureAnalysis
    {
        public DateTime AnalysisDate { get; set; }
        public int TotalFailures { get; set; }
        public Dictionary<string, int> FailuresByType { get; set; }
        public Dictionary<string, int> FailuresBySystem { get; set; }
        public List<FailureCause> CommonCauses { get; set; }
        public Dictionary<string, double> MTBFByType { get; set; }
        public Dictionary<string, double> SeasonalPatterns { get; set; }
        public decimal TotalFailureCost { get; set; }
        public TimeSpan AverageRepairTime { get; set; }
    }

    public class FailureCause
    {
        public string Cause { get; set; }
        public int Occurrences { get; set; }
        public double Percentage { get; set; }
    }

    public class HistoricalFailure
    {
        public string EquipmentId { get; set; }
        public string EquipmentType { get; set; }
        public string System { get; set; }
        public DateTime FailureDate { get; set; }
        public string Cause { get; set; }
        public decimal RepairCost { get; set; }
        public decimal DowntimeCost { get; set; }
        public TimeSpan RepairDuration { get; set; }
    }

    public class CompletedTask
    {
        public string TaskId { get; set; }
        public DateTime CompletedDate { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public decimal ActualCost { get; set; }
        public string Notes { get; set; }
    }

    public class CostEstimate
    {
        public decimal LaborCost { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal ContractorCost { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class CostAnalysis
    {
        public decimal TotalAnnual { get; set; }
        public Dictionary<string, decimal> ByCategory { get; set; }
        public Dictionary<string, decimal> BySystem { get; set; }
    }

    public class CalendarView
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<MonthSummary> MonthlyView { get; set; }
        public List<WeekSummary> WeeklyView { get; set; }
    }

    public class MonthSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TaskCount { get; set; }
        public int CriticalCount { get; set; }
        public double EstimatedHours { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class WeekSummary
    {
        public int WeekNumber { get; set; }
        public List<MaintenanceTask> Tasks { get; set; }
        public double TotalHours { get; set; }
    }

    public class MaintenanceRecommendation
    {
        public string Priority { get; set; }
        public string Category { get; set; }
        public string Recommendation { get; set; }
        public List<string> AffectedEquipment { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class MaintenanceStrategyRecommendation
    {
        public DateTime GeneratedAt { get; set; }
        public decimal CurrentAnnualCost { get; set; }
        public MaintenanceStrategy RecommendedStrategy { get; set; }
        public List<MaintenanceStrategy> AlternativeStrategies { get; set; }
        public decimal PotentialSavings { get; set; }
        public List<EquipmentRecommendation> EquipmentRecommendations { get; set; }
    }

    public class MaintenanceStrategy
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public MaintenanceApproach MaintenanceApproach { get; set; }
        public int IntervalReduction { get; set; }
        public bool RequiresMonitoring { get; set; }
        public decimal MonitoringInvestment { get; set; }
        public decimal EstimatedAnnualCost { get; set; }
        public double RiskScore { get; set; }
        public double ROI { get; set; }
    }

    public class BudgetConstraints
    {
        public decimal AnnualBudget { get; set; }
        public decimal QuarterlyBudget { get; set; }
    }

    public class EquipmentRecommendation
    {
        public string EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public TimeSpan CurrentInterval { get; set; }
        public TimeSpan RecommendedInterval { get; set; }
        public string RecommendedAction { get; set; }
        public decimal PotentialSavings { get; set; }
    }

    public enum EquipmentCondition { Excellent, Good, Fair, Poor }
    public enum Criticality { Critical, Important, Standard, Low }
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum TaskPriority { Low, Medium, High, Critical }
    public enum MaintenanceTaskStatus { Scheduled, InProgress, Completed, Overdue, Cancelled }
    public enum MaintenanceFrequency { Daily, Weekly, Biweekly, Monthly, Quarterly, Biannual, Annual, Biennial, FiveYearly }
    public enum MaintenanceTaskType { Inspection, Cleaning, Lubrication, FilterChange, BeltInspection, CoilCleaning, BearingLubrication, OilAnalysis, TubeInspection, RefrigerantCheck, LoadTest, RopeInspection, Testing, OilChange, LoadBankTest, BatteryService, PredictiveTesting, ReplacementPlanning }
    public enum MaintenanceApproach { Preventive, Predictive, Reactive, Hybrid }

    #endregion
}
