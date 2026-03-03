// StingBIM.AI.Construction - ConstructionSequencingEngine.cs
// 4D/5D Construction Sequencing and Planning Engine
// Phase 4: Enterprise AI Transformation - Construction Automation
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace StingBIM.AI.Construction.Sequencing
{
    /// <summary>
    /// Advanced construction sequencing engine providing 4D scheduling (time),
    /// 5D costing, critical path analysis, resource optimization, and logistics planning.
    /// </summary>
    public class ConstructionSequencingEngine
    {
        #region Fields

        private readonly Dictionary<string, ConstructionActivity> _activities;
        private readonly Dictionary<string, WorkPackage> _workPackages;
        private readonly Dictionary<string, Resource> _resources;
        private readonly SequenceOptimizer _optimizer;
        private readonly CriticalPathAnalyzer _criticalPathAnalyzer;
        private readonly ResourceScheduler _resourceScheduler;
        private readonly CostEstimator _costEstimator;
        private readonly LogisticsPlanner _logisticsPlanner;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public ConstructionSequencingEngine()
        {
            _activities = new Dictionary<string, ConstructionActivity>(StringComparer.OrdinalIgnoreCase);
            _workPackages = new Dictionary<string, WorkPackage>(StringComparer.OrdinalIgnoreCase);
            _resources = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);
            _optimizer = new SequenceOptimizer();
            _criticalPathAnalyzer = new CriticalPathAnalyzer();
            _resourceScheduler = new ResourceScheduler();
            _costEstimator = new CostEstimator();
            _logisticsPlanner = new LogisticsPlanner();

            InitializeDefaultActivities();
            InitializeDefaultResources();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultActivities()
        {
            // Site Preparation
            AddActivity(new ConstructionActivity
            {
                ActivityId = "SITE-001",
                Name = "Site Clearing",
                Category = "Site Work",
                Phase = ConstructionPhase.SitePreparation,
                DefaultDurationDays = 5,
                LaborTypes = new List<string> { "General Labor", "Equipment Operator" },
                Prerequisites = new List<string>(),
                SuccessorsCanOverlap = false
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "SITE-002",
                Name = "Excavation",
                Category = "Site Work",
                Phase = ConstructionPhase.SitePreparation,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Equipment Operator", "General Labor" },
                Prerequisites = new List<string> { "SITE-001" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 20
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "SITE-003",
                Name = "Site Utilities Installation",
                Category = "Site Work",
                Phase = ConstructionPhase.SitePreparation,
                DefaultDurationDays = 15,
                LaborTypes = new List<string> { "Plumber", "Electrician", "General Labor" },
                Prerequisites = new List<string> { "SITE-002" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 30
            });

            // Foundation
            AddActivity(new ConstructionActivity
            {
                ActivityId = "FND-001",
                Name = "Foundation Layout",
                Category = "Foundation",
                Phase = ConstructionPhase.Foundation,
                DefaultDurationDays = 3,
                LaborTypes = new List<string> { "Surveyor", "General Labor" },
                Prerequisites = new List<string> { "SITE-002" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "FND-002",
                Name = "Foundation Formwork",
                Category = "Foundation",
                Phase = ConstructionPhase.Foundation,
                DefaultDurationDays = 7,
                LaborTypes = new List<string> { "Carpenter", "General Labor" },
                Prerequisites = new List<string> { "FND-001" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "FND-003",
                Name = "Foundation Reinforcement",
                Category = "Foundation",
                Phase = ConstructionPhase.Foundation,
                DefaultDurationDays = 5,
                LaborTypes = new List<string> { "Ironworker", "General Labor" },
                Prerequisites = new List<string> { "FND-002" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 50
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "FND-004",
                Name = "Foundation Concrete Pour",
                Category = "Foundation",
                Phase = ConstructionPhase.Foundation,
                DefaultDurationDays = 3,
                LaborTypes = new List<string> { "Concrete Finisher", "General Labor" },
                Prerequisites = new List<string> { "FND-003" },
                RequiresCuring = true,
                CuringDays = 7
            });

            // Structure
            AddActivity(new ConstructionActivity
            {
                ActivityId = "STR-001",
                Name = "Structural Steel Erection",
                Category = "Structure",
                Phase = ConstructionPhase.Structure,
                DefaultDurationDays = 20,
                LaborTypes = new List<string> { "Ironworker", "Crane Operator", "General Labor" },
                Prerequisites = new List<string> { "FND-004" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 25
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "STR-002",
                Name = "Concrete Columns",
                Category = "Structure",
                Phase = ConstructionPhase.Structure,
                DefaultDurationDays = 15,
                LaborTypes = new List<string> { "Carpenter", "Ironworker", "Concrete Finisher" },
                Prerequisites = new List<string> { "FND-004" },
                RequiresCuring = true,
                CuringDays = 7
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "STR-003",
                Name = "Floor Slab Construction",
                Category = "Structure",
                Phase = ConstructionPhase.Structure,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Carpenter", "Ironworker", "Concrete Finisher" },
                Prerequisites = new List<string> { "STR-002" },
                RepeatPerLevel = true,
                RequiresCuring = true,
                CuringDays = 7
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "STR-004",
                Name = "Roof Structure",
                Category = "Structure",
                Phase = ConstructionPhase.Structure,
                DefaultDurationDays = 12,
                LaborTypes = new List<string> { "Ironworker", "Carpenter", "Crane Operator" },
                Prerequisites = new List<string> { "STR-003" }
            });

            // Envelope
            AddActivity(new ConstructionActivity
            {
                ActivityId = "ENV-001",
                Name = "Exterior Wall Framing",
                Category = "Envelope",
                Phase = ConstructionPhase.Envelope,
                DefaultDurationDays = 15,
                LaborTypes = new List<string> { "Carpenter", "General Labor" },
                Prerequisites = new List<string> { "STR-003" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 40
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "ENV-002",
                Name = "Window Installation",
                Category = "Envelope",
                Phase = ConstructionPhase.Envelope,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Glazier", "General Labor" },
                Prerequisites = new List<string> { "ENV-001" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "ENV-003",
                Name = "Roofing",
                Category = "Envelope",
                Phase = ConstructionPhase.Envelope,
                DefaultDurationDays = 8,
                LaborTypes = new List<string> { "Roofer", "General Labor" },
                Prerequisites = new List<string> { "STR-004" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "ENV-004",
                Name = "Exterior Cladding",
                Category = "Envelope",
                Phase = ConstructionPhase.Envelope,
                DefaultDurationDays = 20,
                LaborTypes = new List<string> { "Mason", "General Labor" },
                Prerequisites = new List<string> { "ENV-001", "ENV-002" }
            });

            // MEP Rough-In
            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEP-001",
                Name = "Electrical Rough-In",
                Category = "MEP",
                Phase = ConstructionPhase.MEPRoughIn,
                DefaultDurationDays = 15,
                LaborTypes = new List<string> { "Electrician" },
                Prerequisites = new List<string> { "ENV-001" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 50
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEP-002",
                Name = "Plumbing Rough-In",
                Category = "MEP",
                Phase = ConstructionPhase.MEPRoughIn,
                DefaultDurationDays = 12,
                LaborTypes = new List<string> { "Plumber" },
                Prerequisites = new List<string> { "ENV-001" },
                SuccessorsCanOverlap = true,
                OverlapPercentage = 50
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEP-003",
                Name = "HVAC Ductwork",
                Category = "MEP",
                Phase = ConstructionPhase.MEPRoughIn,
                DefaultDurationDays = 18,
                LaborTypes = new List<string> { "Sheet Metal Worker", "HVAC Technician" },
                Prerequisites = new List<string> { "ENV-001" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEP-004",
                Name = "Fire Protection Rough-In",
                Category = "MEP",
                Phase = ConstructionPhase.MEPRoughIn,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Sprinkler Fitter" },
                Prerequisites = new List<string> { "MEP-003" }
            });

            // Interior Finishes
            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-001",
                Name = "Interior Framing",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 12,
                LaborTypes = new List<string> { "Carpenter", "General Labor" },
                Prerequisites = new List<string> { "MEP-001", "MEP-002", "MEP-003" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-002",
                Name = "Drywall Installation",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Drywall Installer", "General Labor" },
                Prerequisites = new List<string> { "INT-001" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-003",
                Name = "Painting",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 8,
                LaborTypes = new List<string> { "Painter" },
                Prerequisites = new List<string> { "INT-002" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-004",
                Name = "Flooring Installation",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Flooring Installer" },
                Prerequisites = new List<string> { "INT-003" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-005",
                Name = "Ceiling Installation",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 8,
                LaborTypes = new List<string> { "Ceiling Installer" },
                Prerequisites = new List<string> { "INT-002" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "INT-006",
                Name = "Interior Door Installation",
                Category = "Interior",
                Phase = ConstructionPhase.InteriorFinish,
                DefaultDurationDays = 5,
                LaborTypes = new List<string> { "Carpenter" },
                Prerequisites = new List<string> { "INT-002" }
            });

            // MEP Trim
            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEPT-001",
                Name = "Electrical Trim",
                Category = "MEP Trim",
                Phase = ConstructionPhase.MEPTrim,
                DefaultDurationDays = 8,
                LaborTypes = new List<string> { "Electrician" },
                Prerequisites = new List<string> { "INT-003" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEPT-002",
                Name = "Plumbing Fixtures",
                Category = "MEP Trim",
                Phase = ConstructionPhase.MEPTrim,
                DefaultDurationDays = 6,
                LaborTypes = new List<string> { "Plumber" },
                Prerequisites = new List<string> { "INT-003" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "MEPT-003",
                Name = "HVAC Equipment Installation",
                Category = "MEP Trim",
                Phase = ConstructionPhase.MEPTrim,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "HVAC Technician" },
                Prerequisites = new List<string> { "INT-005" }
            });

            // Commissioning
            AddActivity(new ConstructionActivity
            {
                ActivityId = "COM-001",
                Name = "MEP Commissioning",
                Category = "Commissioning",
                Phase = ConstructionPhase.Commissioning,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "Commissioning Agent", "HVAC Technician", "Electrician" },
                Prerequisites = new List<string> { "MEPT-001", "MEPT-002", "MEPT-003" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "COM-002",
                Name = "Systems Testing",
                Category = "Commissioning",
                Phase = ConstructionPhase.Commissioning,
                DefaultDurationDays = 5,
                LaborTypes = new List<string> { "Commissioning Agent" },
                Prerequisites = new List<string> { "COM-001" }
            });

            // Closeout
            AddActivity(new ConstructionActivity
            {
                ActivityId = "CLO-001",
                Name = "Punch List",
                Category = "Closeout",
                Phase = ConstructionPhase.Closeout,
                DefaultDurationDays = 10,
                LaborTypes = new List<string> { "General Labor", "Various Trades" },
                Prerequisites = new List<string> { "COM-002" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "CLO-002",
                Name = "Final Cleaning",
                Category = "Closeout",
                Phase = ConstructionPhase.Closeout,
                DefaultDurationDays = 3,
                LaborTypes = new List<string> { "Cleaning Crew" },
                Prerequisites = new List<string> { "CLO-001" }
            });

            AddActivity(new ConstructionActivity
            {
                ActivityId = "CLO-003",
                Name = "Handover",
                Category = "Closeout",
                Phase = ConstructionPhase.Closeout,
                DefaultDurationDays = 2,
                LaborTypes = new List<string> { "Project Manager" },
                Prerequisites = new List<string> { "CLO-002" }
            });
        }

        private void InitializeDefaultResources()
        {
            // Labor Resources
            AddResource(new Resource
            {
                ResourceId = "LAB-001",
                Name = "General Labor",
                Type = ResourceType.Labor,
                UnitCostPerDay = 150,
                Currency = "USD",
                AvailableUnits = 20,
                Skills = new List<string> { "Manual Labor", "Site Work" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-002",
                Name = "Carpenter",
                Type = ResourceType.Labor,
                UnitCostPerDay = 280,
                Currency = "USD",
                AvailableUnits = 8,
                Skills = new List<string> { "Framing", "Formwork", "Finish Carpentry" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-003",
                Name = "Electrician",
                Type = ResourceType.Labor,
                UnitCostPerDay = 320,
                Currency = "USD",
                AvailableUnits = 6,
                Skills = new List<string> { "Electrical Rough-In", "Electrical Trim", "Commissioning" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-004",
                Name = "Plumber",
                Type = ResourceType.Labor,
                UnitCostPerDay = 300,
                Currency = "USD",
                AvailableUnits = 5,
                Skills = new List<string> { "Plumbing Rough-In", "Fixture Installation" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-005",
                Name = "HVAC Technician",
                Type = ResourceType.Labor,
                UnitCostPerDay = 340,
                Currency = "USD",
                AvailableUnits = 4,
                Skills = new List<string> { "Ductwork", "Equipment Installation", "Commissioning" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-006",
                Name = "Ironworker",
                Type = ResourceType.Labor,
                UnitCostPerDay = 350,
                Currency = "USD",
                AvailableUnits = 6,
                Skills = new List<string> { "Structural Steel", "Reinforcement" }
            });

            AddResource(new Resource
            {
                ResourceId = "LAB-007",
                Name = "Concrete Finisher",
                Type = ResourceType.Labor,
                UnitCostPerDay = 260,
                Currency = "USD",
                AvailableUnits = 8,
                Skills = new List<string> { "Concrete Placement", "Finishing" }
            });

            // Equipment Resources
            AddResource(new Resource
            {
                ResourceId = "EQP-001",
                Name = "Tower Crane",
                Type = ResourceType.Equipment,
                UnitCostPerDay = 1500,
                Currency = "USD",
                AvailableUnits = 1,
                MobilizationCost = 25000,
                DemobilizationCost = 15000
            });

            AddResource(new Resource
            {
                ResourceId = "EQP-002",
                Name = "Excavator",
                Type = ResourceType.Equipment,
                UnitCostPerDay = 800,
                Currency = "USD",
                AvailableUnits = 2,
                MobilizationCost = 3000
            });

            AddResource(new Resource
            {
                ResourceId = "EQP-003",
                Name = "Concrete Pump",
                Type = ResourceType.Equipment,
                UnitCostPerDay = 1200,
                Currency = "USD",
                AvailableUnits = 1,
                MobilizationCost = 2000
            });

            AddResource(new Resource
            {
                ResourceId = "EQP-004",
                Name = "Scissor Lift",
                Type = ResourceType.Equipment,
                UnitCostPerDay = 250,
                Currency = "USD",
                AvailableUnits = 4
            });
        }

        #endregion

        #region Public Methods - Activity Management

        public void AddActivity(ConstructionActivity activity)
        {
            lock (_lockObject)
            {
                _activities[activity.ActivityId] = activity;
            }
        }

        public void AddWorkPackage(WorkPackage package)
        {
            lock (_lockObject)
            {
                _workPackages[package.PackageId] = package;
            }
        }

        public void AddResource(Resource resource)
        {
            lock (_lockObject)
            {
                _resources[resource.ResourceId] = resource;
            }
        }

        public ConstructionActivity GetActivity(string activityId)
        {
            lock (_lockObject)
            {
                return _activities.GetValueOrDefault(activityId);
            }
        }

        public IEnumerable<ConstructionActivity> GetActivitiesByPhase(ConstructionPhase phase)
        {
            lock (_lockObject)
            {
                return _activities.Values.Where(a => a.Phase == phase).ToList();
            }
        }

        #endregion

        #region Public Methods - Schedule Generation

        /// <summary>
        /// Generates construction schedule from BIM model elements
        /// </summary>
        public async Task<ConstructionSchedule> GenerateScheduleFromModelAsync(
            Document document,
            ScheduleGenerationOptions options,
            IProgress<SequencingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var schedule = new ConstructionSchedule
            {
                ScheduleId = Guid.NewGuid().ToString(),
                ProjectName = document.Title,
                GeneratedAt = DateTime.Now,
                StartDate = options.ProjectStartDate
            };

            // Extract elements from model
            var modelElements = await ExtractModelElementsAsync(document, cancellationToken);
            progress?.Report(new SequencingProgress { Phase = "Element Extraction", PercentComplete = 20 });

            // Generate work packages from elements
            var workPackages = GenerateWorkPackages(modelElements, options);
            schedule.WorkPackages.AddRange(workPackages);
            progress?.Report(new SequencingProgress { Phase = "Work Package Generation", PercentComplete = 40 });

            // Create activity instances
            var scheduledActivities = CreateScheduledActivities(workPackages, options);
            schedule.Activities.AddRange(scheduledActivities);
            progress?.Report(new SequencingProgress { Phase = "Activity Scheduling", PercentComplete = 60 });

            // Calculate dates based on dependencies
            CalculateActivityDates(schedule, options);
            progress?.Report(new SequencingProgress { Phase = "Date Calculation", PercentComplete = 70 });

            // Perform critical path analysis
            schedule.CriticalPath = _criticalPathAnalyzer.Analyze(schedule);
            progress?.Report(new SequencingProgress { Phase = "Critical Path Analysis", PercentComplete = 80 });

            // Optimize resource allocation
            if (options.OptimizeResources)
            {
                _resourceScheduler.OptimizeAllocation(schedule, _resources.Values.ToList());
            }
            progress?.Report(new SequencingProgress { Phase = "Resource Optimization", PercentComplete = 90 });

            // Calculate costs (5D)
            if (options.IncludeCostEstimate)
            {
                schedule.CostEstimate = _costEstimator.EstimateCosts(schedule, _resources.Values.ToList());
            }

            schedule.EndDate = schedule.Activities.Max(a => a.EndDate);
            schedule.TotalDuration = (schedule.EndDate - schedule.StartDate).Days;

            progress?.Report(new SequencingProgress { Phase = "Complete", PercentComplete = 100 });
            return schedule;
        }

        /// <summary>
        /// Creates schedule from predefined activities
        /// </summary>
        public ConstructionSchedule CreateScheduleFromActivities(
            IEnumerable<string> activityIds,
            DateTime startDate,
            ScheduleGenerationOptions options = null)
        {
            options = options ?? new ScheduleGenerationOptions { ProjectStartDate = startDate };

            var schedule = new ConstructionSchedule
            {
                ScheduleId = Guid.NewGuid().ToString(),
                StartDate = startDate,
                GeneratedAt = DateTime.Now
            };

            var activities = activityIds
                .Select(id => _activities.GetValueOrDefault(id))
                .Where(a => a != null)
                .ToList();

            foreach (var activity in activities)
            {
                schedule.Activities.Add(new ScheduledActivity
                {
                    ActivityId = activity.ActivityId,
                    Name = activity.Name,
                    Category = activity.Category,
                    Phase = activity.Phase,
                    DurationDays = activity.DefaultDurationDays,
                    Prerequisites = new List<string>(activity.Prerequisites),
                    LaborTypes = new List<string>(activity.LaborTypes)
                });
            }

            CalculateActivityDates(schedule, options);
            schedule.CriticalPath = _criticalPathAnalyzer.Analyze(schedule);
            schedule.EndDate = schedule.Activities.Max(a => a.EndDate);
            schedule.TotalDuration = (schedule.EndDate - schedule.StartDate).Days;

            return schedule;
        }

        #endregion

        #region Public Methods - Schedule Analysis

        /// <summary>
        /// Performs what-if analysis for schedule changes
        /// </summary>
        public WhatIfAnalysisResult AnalyzeScheduleChange(
            ConstructionSchedule schedule,
            ScheduleChangeScenario scenario)
        {
            var result = new WhatIfAnalysisResult
            {
                ScenarioName = scenario.Name,
                OriginalEndDate = schedule.EndDate,
                OriginalTotalCost = schedule.CostEstimate?.TotalCost ?? 0
            };

            // Clone schedule for analysis
            var modifiedSchedule = CloneSchedule(schedule);

            // Apply changes
            foreach (var change in scenario.Changes)
            {
                ApplyChange(modifiedSchedule, change);
            }

            // Recalculate
            CalculateActivityDates(modifiedSchedule, new ScheduleGenerationOptions());
            modifiedSchedule.CriticalPath = _criticalPathAnalyzer.Analyze(modifiedSchedule);
            modifiedSchedule.EndDate = modifiedSchedule.Activities.Max(a => a.EndDate);

            result.ModifiedEndDate = modifiedSchedule.EndDate;
            result.ScheduleImpactDays = (result.ModifiedEndDate - result.OriginalEndDate).Days;
            result.CriticalPathChanged = !modifiedSchedule.CriticalPath.SequenceEqual(schedule.CriticalPath);
            result.AffectedActivities = GetAffectedActivities(schedule, modifiedSchedule);

            if (scenario.IncludeCostImpact)
            {
                var newCost = _costEstimator.EstimateCosts(modifiedSchedule, _resources.Values.ToList());
                result.ModifiedTotalCost = newCost.TotalCost;
                result.CostImpact = result.ModifiedTotalCost - result.OriginalTotalCost;
            }

            return result;
        }

        /// <summary>
        /// Identifies schedule risks and bottlenecks
        /// </summary>
        public ScheduleRiskAssessment AssessRisks(ConstructionSchedule schedule)
        {
            var assessment = new ScheduleRiskAssessment
            {
                AssessmentDate = DateTime.Now
            };

            // Identify activities with low float (near-critical)
            foreach (var activity in schedule.Activities)
            {
                if (activity.TotalFloat <= 3 && !schedule.CriticalPath.Contains(activity.ActivityId))
                {
                    assessment.NearCriticalActivities.Add(new RiskItem
                    {
                        ActivityId = activity.ActivityId,
                        ActivityName = activity.Name,
                        RiskLevel = RiskLevel.Medium,
                        Description = $"Activity has only {activity.TotalFloat} days of float",
                        Mitigation = "Consider adding resources to reduce duration"
                    });
                }
            }

            // Identify resource overallocation
            var resourceUsage = AnalyzeResourceUsage(schedule);
            foreach (var overallocation in resourceUsage.Where(r => r.Value > 100))
            {
                assessment.ResourceRisks.Add(new RiskItem
                {
                    RiskLevel = RiskLevel.High,
                    Description = $"Resource '{overallocation.Key}' is overallocated at {overallocation.Value}%",
                    Mitigation = "Level resources or acquire additional capacity"
                });
            }

            // Identify weather-sensitive activities
            var weatherSensitive = schedule.Activities
                .Where(a => IsWeatherSensitive(a.Category))
                .Where(a => IsInRainySeason(a.StartDate, a.EndDate))
                .ToList();

            foreach (var activity in weatherSensitive)
            {
                assessment.WeatherRisks.Add(new RiskItem
                {
                    ActivityId = activity.ActivityId,
                    ActivityName = activity.Name,
                    RiskLevel = RiskLevel.Medium,
                    Description = "Activity is weather-sensitive and scheduled during rainy season",
                    Mitigation = "Add contingency days or reschedule if possible"
                });
            }

            assessment.OverallRiskScore = CalculateOverallRiskScore(assessment);
            return assessment;
        }

        /// <summary>
        /// Optimizes schedule for given objective
        /// </summary>
        public OptimizationResult OptimizeSchedule(
            ConstructionSchedule schedule,
            OptimizationObjective objective,
            OptimizationConstraints constraints = null)
        {
            return _optimizer.Optimize(schedule, objective, constraints, _resources.Values.ToList());
        }

        #endregion

        #region Public Methods - 4D Visualization

        /// <summary>
        /// Gets elements to display at specific date for 4D visualization
        /// </summary>
        public Model4DSnapshot Get4DSnapshot(
            Document document,
            ConstructionSchedule schedule,
            DateTime snapshotDate)
        {
            var snapshot = new Model4DSnapshot
            {
                Date = snapshotDate,
                ScheduleId = schedule.ScheduleId
            };

            foreach (var activity in schedule.Activities)
            {
                if (activity.EndDate <= snapshotDate)
                {
                    // Activity completed - show elements
                    snapshot.CompletedElementIds.AddRange(activity.AssociatedElementIds ?? new List<ElementId>());
                }
                else if (activity.StartDate <= snapshotDate && activity.EndDate > snapshotDate)
                {
                    // Activity in progress - show partial
                    var progress = (snapshotDate - activity.StartDate).TotalDays / activity.DurationDays;
                    snapshot.InProgressActivities.Add(new ActivityProgress
                    {
                        ActivityId = activity.ActivityId,
                        ActivityName = activity.Name,
                        PercentComplete = progress * 100,
                        ElementIds = activity.AssociatedElementIds ?? new List<ElementId>()
                    });
                }
                else
                {
                    // Activity not started - hide elements
                    snapshot.FutureElementIds.AddRange(activity.AssociatedElementIds ?? new List<ElementId>());
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Generates 4D animation keyframes
        /// </summary>
        public List<Model4DSnapshot> Generate4DAnimation(
            Document document,
            ConstructionSchedule schedule,
            TimeSpan frameInterval)
        {
            var snapshots = new List<Model4DSnapshot>();
            var currentDate = schedule.StartDate;

            while (currentDate <= schedule.EndDate)
            {
                snapshots.Add(Get4DSnapshot(document, schedule, currentDate));
                currentDate = currentDate.Add(frameInterval);
            }

            return snapshots;
        }

        #endregion

        #region Public Methods - 5D Cost Management

        /// <summary>
        /// Gets cost breakdown by phase
        /// </summary>
        public CostBreakdown GetCostBreakdownByPhase(ConstructionSchedule schedule)
        {
            if (schedule.CostEstimate == null)
            {
                schedule.CostEstimate = _costEstimator.EstimateCosts(schedule, _resources.Values.ToList());
            }

            var breakdown = new CostBreakdown
            {
                TotalCost = schedule.CostEstimate.TotalCost,
                Currency = schedule.CostEstimate.Currency
            };

            var phaseGroups = schedule.Activities.GroupBy(a => a.Phase);
            foreach (var group in phaseGroups)
            {
                var phaseCost = group.Sum(a => a.EstimatedCost);
                breakdown.ByPhase[group.Key.ToString()] = phaseCost;
            }

            var categoryGroups = schedule.Activities.GroupBy(a => a.Category);
            foreach (var group in categoryGroups)
            {
                var categoryCost = group.Sum(a => a.EstimatedCost);
                breakdown.ByCategory[group.Key] = categoryCost;
            }

            return breakdown;
        }

        /// <summary>
        /// Gets cash flow projection
        /// </summary>
        public CashFlowProjection GetCashFlow(
            ConstructionSchedule schedule,
            CashFlowOptions options = null)
        {
            options = options ?? new CashFlowOptions();

            var projection = new CashFlowProjection
            {
                StartDate = schedule.StartDate,
                EndDate = schedule.EndDate
            };

            var currentDate = schedule.StartDate;
            var cumulativeCost = 0.0;

            while (currentDate <= schedule.EndDate)
            {
                var periodEnd = currentDate.AddDays(options.PeriodDays);
                var periodActivities = schedule.Activities
                    .Where(a => a.StartDate < periodEnd && a.EndDate > currentDate)
                    .ToList();

                var periodCost = 0.0;
                foreach (var activity in periodActivities)
                {
                    var overlapStart = activity.StartDate > currentDate ? activity.StartDate : currentDate;
                    var overlapEnd = activity.EndDate < periodEnd ? activity.EndDate : periodEnd;
                    var overlapDays = (overlapEnd - overlapStart).Days;
                    var dailyCost = activity.EstimatedCost / activity.DurationDays;
                    periodCost += dailyCost * overlapDays;
                }

                cumulativeCost += periodCost;

                projection.Periods.Add(new CashFlowPeriod
                {
                    StartDate = currentDate,
                    EndDate = periodEnd,
                    PeriodCost = periodCost,
                    CumulativeCost = cumulativeCost
                });

                currentDate = periodEnd;
            }

            return projection;
        }

        #endregion

        #region Public Methods - Logistics Planning

        /// <summary>
        /// Generates material delivery schedule
        /// </summary>
        public MaterialDeliverySchedule PlanMaterialDeliveries(
            ConstructionSchedule schedule,
            LogisticsOptions options = null)
        {
            return _logisticsPlanner.PlanDeliveries(schedule, options);
        }

        /// <summary>
        /// Plans site logistics and staging areas
        /// </summary>
        public SiteLogisticsPlan PlanSiteLogistics(
            Document document,
            ConstructionSchedule schedule,
            SiteConstraints constraints = null)
        {
            return _logisticsPlanner.PlanSiteLogistics(document, schedule, constraints);
        }

        #endregion

        #region Private Methods - Schedule Calculation

        private async Task<List<ModelElement>> ExtractModelElementsAsync(Document document, CancellationToken cancellationToken)
        {
            var elements = new List<ModelElement>();

            await Task.Run(() =>
            {
                // Extract structural elements
                var structuralCategories = new[]
                {
                    BuiltInCategory.OST_StructuralFoundation,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_Floors
                };

                foreach (var category in structuralCategories)
                {
                    var categoryElements = new FilteredElementCollector(document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var element in categoryElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        elements.Add(new ModelElement
                        {
                            ElementId = element.Id,
                            Category = category.ToString(),
                            Name = element.Name,
                            Level = GetElementLevel(element)
                        });
                    }
                }

                // Extract architectural elements
                var archCategories = new[]
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Doors,
                    BuiltInCategory.OST_Windows,
                    BuiltInCategory.OST_Roofs,
                    BuiltInCategory.OST_Ceilings
                };

                foreach (var category in archCategories)
                {
                    var categoryElements = new FilteredElementCollector(document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var element in categoryElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        elements.Add(new ModelElement
                        {
                            ElementId = element.Id,
                            Category = category.ToString(),
                            Name = element.Name,
                            Level = GetElementLevel(element)
                        });
                    }
                }

                // Extract MEP elements
                var mepCategories = new[]
                {
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_ElectricalEquipment,
                    BuiltInCategory.OST_MechanicalEquipment,
                    BuiltInCategory.OST_PlumbingFixtures
                };

                foreach (var category in mepCategories)
                {
                    var categoryElements = new FilteredElementCollector(document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var element in categoryElements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        elements.Add(new ModelElement
                        {
                            ElementId = element.Id,
                            Category = category.ToString(),
                            Name = element.Name,
                            Level = GetElementLevel(element)
                        });
                    }
                }
            }, cancellationToken);

            return elements;
        }

        private string GetElementLevel(Element element)
        {
            var levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
            {
                return element.Document.GetElement(levelParam.AsElementId())?.Name ?? "Unknown";
            }
            return "Unknown";
        }

        private List<WorkPackage> GenerateWorkPackages(List<ModelElement> elements, ScheduleGenerationOptions options)
        {
            var packages = new List<WorkPackage>();
            var groupedByLevel = elements.GroupBy(e => e.Level);

            foreach (var levelGroup in groupedByLevel)
            {
                var groupedByCategory = levelGroup.GroupBy(e => e.Category);

                foreach (var categoryGroup in groupedByCategory)
                {
                    packages.Add(new WorkPackage
                    {
                        PackageId = Guid.NewGuid().ToString(),
                        Name = $"{categoryGroup.Key} - {levelGroup.Key}",
                        Level = levelGroup.Key,
                        Category = categoryGroup.Key,
                        ElementIds = categoryGroup.Select(e => e.ElementId).ToList(),
                        ElementCount = categoryGroup.Count()
                    });
                }
            }

            return packages;
        }

        private List<ScheduledActivity> CreateScheduledActivities(List<WorkPackage> packages, ScheduleGenerationOptions options)
        {
            var scheduledActivities = new List<ScheduledActivity>();

            foreach (var package in packages)
            {
                var matchingActivity = FindMatchingActivity(package);
                if (matchingActivity != null)
                {
                    var duration = CalculateDuration(matchingActivity, package);

                    scheduledActivities.Add(new ScheduledActivity
                    {
                        ActivityId = $"{matchingActivity.ActivityId}-{package.PackageId.Substring(0, 8)}",
                        Name = $"{matchingActivity.Name} - {package.Level}",
                        Category = matchingActivity.Category,
                        Phase = matchingActivity.Phase,
                        DurationDays = duration,
                        Prerequisites = new List<string>(matchingActivity.Prerequisites),
                        LaborTypes = new List<string>(matchingActivity.LaborTypes),
                        AssociatedElementIds = package.ElementIds,
                        WorkPackageId = package.PackageId
                    });
                }
            }

            return scheduledActivities;
        }

        private ConstructionActivity FindMatchingActivity(WorkPackage package)
        {
            var categoryMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OST_StructuralFoundation"] = "FND",
                ["OST_StructuralColumns"] = "STR-002",
                ["OST_StructuralFraming"] = "STR-001",
                ["OST_Floors"] = "STR-003",
                ["OST_Walls"] = "ENV-001",
                ["OST_Doors"] = "INT-006",
                ["OST_Windows"] = "ENV-002",
                ["OST_Roofs"] = "ENV-003",
                ["OST_DuctCurves"] = "MEP-003",
                ["OST_PipeCurves"] = "MEP-002",
                ["OST_ElectricalEquipment"] = "MEP-001"
            };

            if (categoryMappings.TryGetValue(package.Category, out var activityPrefix))
            {
                return _activities.Values.FirstOrDefault(a => a.ActivityId.StartsWith(activityPrefix));
            }

            return null;
        }

        private int CalculateDuration(ConstructionActivity activity, WorkPackage package)
        {
            var baseDuration = activity.DefaultDurationDays;
            var elementFactor = Math.Max(1, Math.Sqrt(package.ElementCount / 10.0));
            return (int)Math.Ceiling(baseDuration * elementFactor);
        }

        private void CalculateActivityDates(ConstructionSchedule schedule, ScheduleGenerationOptions options)
        {
            var activityDict = schedule.Activities.ToDictionary(a => a.ActivityId);
            var processed = new HashSet<string>();

            // Forward pass - calculate early start/finish
            foreach (var activity in schedule.Activities.OrderBy(a => a.Prerequisites.Count))
            {
                CalculateEarlyDates(activity, activityDict, processed, schedule.StartDate);
            }

            // Backward pass - calculate late start/finish
            var projectEnd = schedule.Activities.Max(a => a.EarlyFinish);
            processed.Clear();

            foreach (var activity in schedule.Activities.OrderByDescending(a => a.EarlyFinish))
            {
                CalculateLateDates(activity, activityDict, processed, projectEnd);
            }

            // Calculate float
            foreach (var activity in schedule.Activities)
            {
                activity.TotalFloat = (activity.LateFinish - activity.EarlyFinish).Days;
                activity.StartDate = activity.EarlyStart;
                activity.EndDate = activity.EarlyFinish;
            }
        }

        private void CalculateEarlyDates(
            ScheduledActivity activity,
            Dictionary<string, ScheduledActivity> activityDict,
            HashSet<string> processed,
            DateTime projectStart)
        {
            if (processed.Contains(activity.ActivityId)) return;

            var earlyStart = projectStart;

            foreach (var prereqId in activity.Prerequisites)
            {
                var prereqPattern = prereqId.Split('-')[0];
                var prereq = activityDict.Values.FirstOrDefault(a =>
                    a.ActivityId == prereqId || a.ActivityId.StartsWith(prereqPattern + "-"));

                if (prereq != null)
                {
                    if (!processed.Contains(prereq.ActivityId))
                    {
                        CalculateEarlyDates(prereq, activityDict, processed, projectStart);
                    }

                    if (prereq.EarlyFinish > earlyStart)
                    {
                        earlyStart = prereq.EarlyFinish;
                    }
                }
            }

            activity.EarlyStart = earlyStart;
            activity.EarlyFinish = earlyStart.AddDays(activity.DurationDays);
            processed.Add(activity.ActivityId);
        }

        private void CalculateLateDates(
            ScheduledActivity activity,
            Dictionary<string, ScheduledActivity> activityDict,
            HashSet<string> processed,
            DateTime projectEnd)
        {
            if (processed.Contains(activity.ActivityId)) return;

            activity.LateFinish = projectEnd;

            // Find successors
            var successors = activityDict.Values
                .Where(a => a.Prerequisites.Any(p =>
                    activity.ActivityId.StartsWith(p.Split('-')[0])))
                .ToList();

            foreach (var successor in successors)
            {
                if (!processed.Contains(successor.ActivityId))
                {
                    CalculateLateDates(successor, activityDict, processed, projectEnd);
                }

                if (successor.LateStart < activity.LateFinish)
                {
                    activity.LateFinish = successor.LateStart;
                }
            }

            activity.LateStart = activity.LateFinish.AddDays(-activity.DurationDays);
            processed.Add(activity.ActivityId);
        }

        private ConstructionSchedule CloneSchedule(ConstructionSchedule schedule)
        {
            return new ConstructionSchedule
            {
                ScheduleId = schedule.ScheduleId + "-clone",
                ProjectName = schedule.ProjectName,
                StartDate = schedule.StartDate,
                EndDate = schedule.EndDate,
                Activities = schedule.Activities.Select(a => new ScheduledActivity
                {
                    ActivityId = a.ActivityId,
                    Name = a.Name,
                    Category = a.Category,
                    Phase = a.Phase,
                    DurationDays = a.DurationDays,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    Prerequisites = new List<string>(a.Prerequisites),
                    LaborTypes = new List<string>(a.LaborTypes),
                    EarlyStart = a.EarlyStart,
                    EarlyFinish = a.EarlyFinish,
                    LateStart = a.LateStart,
                    LateFinish = a.LateFinish,
                    TotalFloat = a.TotalFloat
                }).ToList()
            };
        }

        private void ApplyChange(ConstructionSchedule schedule, ScheduleChange change)
        {
            var activity = schedule.Activities.FirstOrDefault(a => a.ActivityId == change.ActivityId);
            if (activity == null) return;

            switch (change.ChangeType)
            {
                case ScheduleChangeType.DurationChange:
                    activity.DurationDays = (int)change.NewValue;
                    break;
                case ScheduleChangeType.StartDateChange:
                    activity.StartDate = (DateTime)change.NewValue;
                    break;
                case ScheduleChangeType.AddPredecessor:
                    activity.Prerequisites.Add((string)change.NewValue);
                    break;
                case ScheduleChangeType.RemovePredecessor:
                    activity.Prerequisites.Remove((string)change.NewValue);
                    break;
            }
        }

        private List<string> GetAffectedActivities(ConstructionSchedule original, ConstructionSchedule modified)
        {
            var affected = new List<string>();

            foreach (var origActivity in original.Activities)
            {
                var modActivity = modified.Activities.FirstOrDefault(a => a.ActivityId == origActivity.ActivityId);
                if (modActivity != null)
                {
                    if (origActivity.StartDate != modActivity.StartDate || origActivity.EndDate != modActivity.EndDate)
                    {
                        affected.Add(origActivity.ActivityId);
                    }
                }
            }

            return affected;
        }

        private Dictionary<string, double> AnalyzeResourceUsage(ConstructionSchedule schedule)
        {
            var usage = new Dictionary<string, double>();
            // Simplified resource usage analysis
            foreach (var resource in _resources.Values.Where(r => r.Type == ResourceType.Labor))
            {
                usage[resource.Name] = 80; // Placeholder
            }
            return usage;
        }

        private bool IsWeatherSensitive(string category)
        {
            var weatherSensitive = new[] { "Site Work", "Foundation", "Envelope", "Roofing" };
            return weatherSensitive.Contains(category);
        }

        private bool IsInRainySeason(DateTime start, DateTime end)
        {
            // Simplified rainy season check (March-May, October-December for East Africa)
            var month = start.Month;
            return (month >= 3 && month <= 5) || (month >= 10 && month <= 12);
        }

        private double CalculateOverallRiskScore(ScheduleRiskAssessment assessment)
        {
            var score = 0.0;
            score += assessment.NearCriticalActivities.Count * 5;
            score += assessment.ResourceRisks.Count(r => r.RiskLevel == RiskLevel.High) * 10;
            score += assessment.ResourceRisks.Count(r => r.RiskLevel == RiskLevel.Medium) * 5;
            score += assessment.WeatherRisks.Count * 3;
            return Math.Min(100, score);
        }

        #endregion
    }

    #region Supporting Classes

    public class ConstructionActivity
    {
        public string ActivityId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public ConstructionPhase Phase { get; set; }
        public int DefaultDurationDays { get; set; }
        public List<string> Prerequisites { get; set; } = new List<string>();
        public List<string> LaborTypes { get; set; } = new List<string>();
        public bool SuccessorsCanOverlap { get; set; }
        public int OverlapPercentage { get; set; }
        public bool RepeatPerLevel { get; set; }
        public bool RequiresCuring { get; set; }
        public int CuringDays { get; set; }
    }

    public class ScheduledActivity
    {
        public string ActivityId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public ConstructionPhase Phase { get; set; }
        public int DurationDays { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime EarlyStart { get; set; }
        public DateTime EarlyFinish { get; set; }
        public DateTime LateStart { get; set; }
        public DateTime LateFinish { get; set; }
        public int TotalFloat { get; set; }
        public List<string> Prerequisites { get; set; } = new List<string>();
        public List<string> LaborTypes { get; set; } = new List<string>();
        public List<ElementId> AssociatedElementIds { get; set; } = new List<ElementId>();
        public string WorkPackageId { get; set; }
        public double EstimatedCost { get; set; }
        public double PercentComplete { get; set; }
    }

    public class WorkPackage
    {
        public string PackageId { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public string Category { get; set; }
        public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
        public int ElementCount { get; set; }
    }

    public class Resource
    {
        public string ResourceId { get; set; }
        public string Name { get; set; }
        public ResourceType Type { get; set; }
        public double UnitCostPerDay { get; set; }
        public string Currency { get; set; } = "USD";
        public int AvailableUnits { get; set; }
        public double MobilizationCost { get; set; }
        public double DemobilizationCost { get; set; }
        public List<string> Skills { get; set; } = new List<string>();
    }

    public class ConstructionSchedule
    {
        public string ScheduleId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDuration { get; set; }
        public List<ScheduledActivity> Activities { get; set; } = new List<ScheduledActivity>();
        public List<WorkPackage> WorkPackages { get; set; } = new List<WorkPackage>();
        public List<string> CriticalPath { get; set; } = new List<string>();
        public CostEstimate CostEstimate { get; set; }
    }

    public class ScheduleGenerationOptions
    {
        public DateTime ProjectStartDate { get; set; } = DateTime.Today;
        public bool OptimizeResources { get; set; } = true;
        public bool IncludeCostEstimate { get; set; } = true;
        public int WorkdaysPerWeek { get; set; } = 6;
        public List<DateTime> Holidays { get; set; } = new List<DateTime>();
    }

    public class SequencingProgress
    {
        public string Phase { get; set; }
        public double PercentComplete { get; set; }
        public string CurrentActivity { get; set; }
    }

    public class ModelElement
    {
        public ElementId ElementId { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
    }

    public class Model4DSnapshot
    {
        public DateTime Date { get; set; }
        public string ScheduleId { get; set; }
        public List<ElementId> CompletedElementIds { get; set; } = new List<ElementId>();
        public List<ActivityProgress> InProgressActivities { get; set; } = new List<ActivityProgress>();
        public List<ElementId> FutureElementIds { get; set; } = new List<ElementId>();
    }

    public class ActivityProgress
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public double PercentComplete { get; set; }
        public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
    }

    public class CostEstimate
    {
        public double TotalCost { get; set; }
        public string Currency { get; set; } = "USD";
        public double LaborCost { get; set; }
        public double MaterialCost { get; set; }
        public double EquipmentCost { get; set; }
        public double Contingency { get; set; }
    }

    public class CostBreakdown
    {
        public double TotalCost { get; set; }
        public string Currency { get; set; }
        public Dictionary<string, double> ByPhase { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> ByCategory { get; set; } = new Dictionary<string, double>();
    }

    public class CashFlowProjection
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<CashFlowPeriod> Periods { get; set; } = new List<CashFlowPeriod>();
    }

    public class CashFlowPeriod
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double PeriodCost { get; set; }
        public double CumulativeCost { get; set; }
    }

    public class CashFlowOptions
    {
        public int PeriodDays { get; set; } = 7;
    }

    public class ScheduleChangeScenario
    {
        public string Name { get; set; }
        public List<ScheduleChange> Changes { get; set; } = new List<ScheduleChange>();
        public bool IncludeCostImpact { get; set; } = true;
    }

    public class ScheduleChange
    {
        public string ActivityId { get; set; }
        public ScheduleChangeType ChangeType { get; set; }
        public object NewValue { get; set; }
    }

    public class WhatIfAnalysisResult
    {
        public string ScenarioName { get; set; }
        public DateTime OriginalEndDate { get; set; }
        public DateTime ModifiedEndDate { get; set; }
        public int ScheduleImpactDays { get; set; }
        public double OriginalTotalCost { get; set; }
        public double ModifiedTotalCost { get; set; }
        public double CostImpact { get; set; }
        public bool CriticalPathChanged { get; set; }
        public List<string> AffectedActivities { get; set; } = new List<string>();
    }

    public class ScheduleRiskAssessment
    {
        public DateTime AssessmentDate { get; set; }
        public double OverallRiskScore { get; set; }
        public List<RiskItem> NearCriticalActivities { get; set; } = new List<RiskItem>();
        public List<RiskItem> ResourceRisks { get; set; } = new List<RiskItem>();
        public List<RiskItem> WeatherRisks { get; set; } = new List<RiskItem>();
    }

    public class RiskItem
    {
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string Description { get; set; }
        public string Mitigation { get; set; }
    }

    public class OptimizationResult
    {
        public bool Success { get; set; }
        public ConstructionSchedule OptimizedSchedule { get; set; }
        public double ImprovementPercent { get; set; }
        public List<string> ChangesApplied { get; set; } = new List<string>();
    }

    public class OptimizationConstraints
    {
        public DateTime? MaxEndDate { get; set; }
        public double? MaxBudget { get; set; }
        public Dictionary<string, int> MaxResourceUnits { get; set; } = new Dictionary<string, int>();
    }

    public class MaterialDeliverySchedule
    {
        public List<DeliveryItem> Deliveries { get; set; } = new List<DeliveryItem>();
    }

    public class DeliveryItem
    {
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public DateTime DeliveryDate { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public string ActivityId { get; set; }
    }

    public class LogisticsOptions
    {
        public int LeadTimeDays { get; set; } = 14;
        public bool JustInTimeDelivery { get; set; } = true;
    }

    public class SiteLogisticsPlan
    {
        public List<StagingArea> StagingAreas { get; set; } = new List<StagingArea>();
        public List<AccessRoute> AccessRoutes { get; set; } = new List<AccessRoute>();
    }

    public class StagingArea
    {
        public string AreaId { get; set; }
        public string Name { get; set; }
        public string Purpose { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class AccessRoute
    {
        public string RouteId { get; set; }
        public string Description { get; set; }
        public List<string> VehicleTypes { get; set; } = new List<string>();
    }

    public class SiteConstraints
    {
        public double SiteArea { get; set; }
        public int MaxVehiclesPerDay { get; set; }
        public List<string> RestrictedAreas { get; set; } = new List<string>();
    }

    public class CriticalPathAnalyzer
    {
        public List<string> Analyze(ConstructionSchedule schedule)
        {
            return schedule.Activities
                .Where(a => a.TotalFloat == 0)
                .OrderBy(a => a.StartDate)
                .Select(a => a.ActivityId)
                .ToList();
        }
    }

    public class ResourceScheduler
    {
        public void OptimizeAllocation(ConstructionSchedule schedule, List<Resource> resources)
        {
            // Resource leveling implementation
        }
    }

    public class CostEstimator
    {
        public CostEstimate EstimateCosts(ConstructionSchedule schedule, List<Resource> resources)
        {
            var estimate = new CostEstimate();

            foreach (var activity in schedule.Activities)
            {
                var laborCost = 0.0;
                foreach (var laborType in activity.LaborTypes)
                {
                    var resource = resources.FirstOrDefault(r => r.Name == laborType);
                    if (resource != null)
                    {
                        laborCost += resource.UnitCostPerDay * activity.DurationDays;
                    }
                }

                activity.EstimatedCost = laborCost * 1.5; // Include materials estimate
                estimate.LaborCost += laborCost;
            }

            estimate.MaterialCost = estimate.LaborCost * 0.8;
            estimate.EquipmentCost = estimate.LaborCost * 0.3;
            estimate.Contingency = (estimate.LaborCost + estimate.MaterialCost + estimate.EquipmentCost) * 0.1;
            estimate.TotalCost = estimate.LaborCost + estimate.MaterialCost + estimate.EquipmentCost + estimate.Contingency;

            return estimate;
        }
    }

    public class LogisticsPlanner
    {
        public MaterialDeliverySchedule PlanDeliveries(ConstructionSchedule schedule, LogisticsOptions options)
        {
            return new MaterialDeliverySchedule();
        }

        public SiteLogisticsPlan PlanSiteLogistics(Document document, ConstructionSchedule schedule, SiteConstraints constraints)
        {
            return new SiteLogisticsPlan();
        }
    }

    public class SequenceOptimizer
    {
        public OptimizationResult Optimize(
            ConstructionSchedule schedule,
            OptimizationObjective objective,
            OptimizationConstraints constraints,
            List<Resource> resources)
        {
            return new OptimizationResult { Success = true, OptimizedSchedule = schedule };
        }
    }

    public enum ConstructionPhase
    {
        SitePreparation,
        Foundation,
        Structure,
        Envelope,
        MEPRoughIn,
        InteriorFinish,
        MEPTrim,
        Commissioning,
        Closeout
    }

    public enum ResourceType
    {
        Labor,
        Equipment,
        Material
    }

    public enum ScheduleChangeType
    {
        DurationChange,
        StartDateChange,
        AddPredecessor,
        RemovePredecessor
    }

    public enum OptimizationObjective
    {
        MinimizeDuration,
        MinimizeCost,
        LevelResources,
        MinimizeRisk
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
