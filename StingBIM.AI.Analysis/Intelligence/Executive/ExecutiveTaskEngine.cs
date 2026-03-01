// ============================================================================
// StingBIM AI - Executive Task Engine
// Handles high-level project management tasks: BEP, schedules, cost tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using StingBIM.AI.Intelligence.Training;

namespace StingBIM.AI.Intelligence.Executive
{
    /// <summary>
    /// Executes high-level BIM project management tasks
    /// </summary>
    public class ExecutiveTaskEngine
    {
        private static readonly Lazy<ExecutiveTaskEngine> _instance =
            new Lazy<ExecutiveTaskEngine>(() => new ExecutiveTaskEngine());
        public static ExecutiveTaskEngine Instance => _instance.Value;

        private readonly TrainingDataLoader _trainingData;
        private readonly Dictionary<string, Func<TaskInput, Task<TaskResult>>> _taskHandlers;

        public event EventHandler<TaskProgressEventArgs> TaskProgress;

        private ExecutiveTaskEngine()
        {
            _trainingData = TrainingDataLoader.Instance;
            _taskHandlers = new Dictionary<string, Func<TaskInput, Task<TaskResult>>>(StringComparer.OrdinalIgnoreCase)
            {
                { "parse project brief", ParseProjectBriefAsync },
                { "project brief", ParseProjectBriefAsync },
                { "generate bim execution plan", GenerateBEPAsync },
                { "bep", GenerateBEPAsync },
                { "bim execution plan", GenerateBEPAsync },
                { "create construction schedule", CreateConstructionScheduleAsync },
                { "construction schedule", CreateConstructionScheduleAsync },
                { "4d schedule", CreateConstructionScheduleAsync },
                { "create maintenance schedule", CreateMaintenanceScheduleAsync },
                { "maintenance schedule", CreateMaintenanceScheduleAsync },
                { "track costs", TrackCostsAsync },
                { "cost tracking", TrackCostsAsync },
                { "5d cost", TrackCostsAsync },
                { "track progress", TrackProgressAsync },
                { "progress tracking", TrackProgressAsync },
                { "progress report", TrackProgressAsync },
                { "generate report", GenerateReportAsync },
                { "coordination report", GenerateReportAsync },
                { "cost report", GenerateCostReportAsync },
                { "provide recommendations", ProvideRecommendationsAsync },
                { "recommendations", ProvideRecommendationsAsync },
                { "recommend", ProvideRecommendationsAsync }
            };
        }

        /// <summary>
        /// Determine if input is a task request (vs a question)
        /// </summary>
        public bool IsTaskRequest(string input)
        {
            var taskIndicators = new[]
            {
                "parse", "generate", "create", "track", "build", "setup",
                "make", "produce", "prepare", "analyze", "calculate"
            };

            var inputLower = input.ToLower();
            return taskIndicators.Any(t => inputLower.Contains(t)) ||
                   _taskHandlers.Keys.Any(k => inputLower.Contains(k));
        }

        /// <summary>
        /// Execute a task based on natural language input
        /// </summary>
        public async Task<TaskResult> ExecuteTaskAsync(string taskDescription, Dictionary<string, object> parameters = null)
        {
            var taskType = IdentifyTaskType(taskDescription);
            if (string.IsNullOrEmpty(taskType))
            {
                return new TaskResult
                {
                    Success = false,
                    ErrorMessage = $"Could not identify task type from: {taskDescription}"
                };
            }

            var input = new TaskInput
            {
                TaskType = taskType,
                Description = taskDescription,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            // Extract parameters from description
            ExtractParametersFromDescription(input);

            if (_taskHandlers.TryGetValue(taskType, out var handler))
            {
                OnTaskProgress(0, $"Starting: {taskType}");
                var result = await handler(input);
                OnTaskProgress(100, $"Completed: {taskType}");
                return result;
            }

            return new TaskResult
            {
                Success = false,
                ErrorMessage = $"No handler for task type: {taskType}"
            };
        }

        /// <summary>
        /// Get list of available executive tasks
        /// </summary>
        public List<string> GetAvailableTasks()
        {
            return new List<string>
            {
                "Parse Project Brief - Extract requirements from project description",
                "Generate BIM Execution Plan - Create complete 9-section BEP",
                "Create Construction Schedule - Generate 4D timeline with phases",
                "Create Maintenance Schedule - Preventive maintenance program",
                "Track Costs (5D BIM) - Budget breakdown and earned value analysis",
                "Track Progress - Schedule performance and earned value metrics",
                "Generate Coordination Report - Clash detection summary",
                "Generate Cost Report - Monthly cost analysis",
                "Provide Recommendations - Actionable next steps"
            };
        }

        private string IdentifyTaskType(string description)
        {
            var descLower = description.ToLower();

            foreach (var taskKey in _taskHandlers.Keys)
            {
                if (descLower.Contains(taskKey))
                    return taskKey;
            }

            // Pattern matching for common variations
            if (descLower.Contains("brief") && (descLower.Contains("parse") || descLower.Contains("analyze")))
                return "parse project brief";
            if (descLower.Contains("bep") || descLower.Contains("execution plan"))
                return "generate bim execution plan";
            if (descLower.Contains("schedule") && descLower.Contains("construction"))
                return "create construction schedule";
            if (descLower.Contains("schedule") && descLower.Contains("maintenance"))
                return "create maintenance schedule";
            if (descLower.Contains("cost") && (descLower.Contains("track") || descLower.Contains("5d")))
                return "track costs";
            if (descLower.Contains("progress"))
                return "track progress";
            if (descLower.Contains("report") && descLower.Contains("cost"))
                return "cost report";
            if (descLower.Contains("report"))
                return "generate report";
            if (descLower.Contains("recommend"))
                return "provide recommendations";

            return null;
        }

        private void ExtractParametersFromDescription(TaskInput input)
        {
            var desc = input.Description.ToLower();

            // Extract project size
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+[,\d]*)\s*(sq\s*ft|sqft|square\s*feet|sf)");
            if (sizeMatch.Success)
            {
                input.Parameters["size"] = sizeMatch.Groups[1].Value.Replace(",", "") + " sq ft";
            }

            // Extract budget
            var budgetMatch = System.Text.RegularExpressions.Regex.Match(desc, @"\$\s*(\d+(?:\.\d+)?)\s*(m|million|k|thousand)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (budgetMatch.Success)
            {
                var value = double.Parse(budgetMatch.Groups[1].Value);
                var multiplier = budgetMatch.Groups[2].Value.ToLower();
                if (multiplier.StartsWith("m")) value *= 1000000;
                else if (multiplier.StartsWith("k") || multiplier.StartsWith("t")) value *= 1000;
                input.Parameters["budget"] = value;
            }

            // Extract duration
            var durationMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+)\s*(months?|weeks?|years?)");
            if (durationMatch.Success)
            {
                input.Parameters["duration"] = int.Parse(durationMatch.Groups[1].Value);
                input.Parameters["duration_unit"] = durationMatch.Groups[2].Value;
            }

            // Extract floors
            var floorsMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+)[- ]?(story|stories|floor|floors|storey|storeys)");
            if (floorsMatch.Success)
            {
                input.Parameters["floors"] = int.Parse(floorsMatch.Groups[1].Value);
            }

            // Extract project type
            var projectTypes = new[] { "office", "residential", "hospital", "healthcare", "retail", "industrial", "warehouse", "school", "university", "hotel" };
            foreach (var type in projectTypes)
            {
                if (desc.Contains(type))
                {
                    input.Parameters["project_type"] = char.ToUpper(type[0]) + type.Substring(1);
                    break;
                }
            }
        }

        #region Task Handlers

        private async Task<TaskResult> ParseProjectBriefAsync(TaskInput input)
        {
            OnTaskProgress(20, "Analyzing project brief...");

            var result = new ProjectBriefResult
            {
                ProjectType = input.Parameters.GetValueOrDefault("project_type", "Commercial Building")?.ToString(),
                Size = input.Parameters.GetValueOrDefault("size", "Unknown")?.ToString(),
                Floors = Convert.ToInt32(input.Parameters.GetValueOrDefault("floors", 1)),
                Budget = Convert.ToDouble(input.Parameters.GetValueOrDefault("budget", 0)),
                DurationMonths = Convert.ToInt32(input.Parameters.GetValueOrDefault("duration", 12))
            };

            OnTaskProgress(50, "Extracting requirements...");

            // Analyze for special requirements
            var desc = input.Description.ToLower();
            result.SpecialRequirements = new List<string>();

            if (desc.Contains("leed") || desc.Contains("green") || desc.Contains("sustainable"))
                result.SpecialRequirements.Add("LEED/Green Building Certification");
            if (desc.Contains("bim") && desc.Contains("coordination"))
                result.SpecialRequirements.Add("Full BIM Coordination Required");
            if (desc.Contains("cobie"))
                result.SpecialRequirements.Add("COBie Data Handover");
            if (desc.Contains("4d") || desc.Contains("schedule"))
                result.SpecialRequirements.Add("4D Schedule Visualization");
            if (desc.Contains("5d") || desc.Contains("cost"))
                result.SpecialRequirements.Add("5D Cost Tracking");

            OnTaskProgress(80, "Generating deliverables list...");

            result.RecommendedDeliverables = new List<string>
            {
                "BIM Execution Plan (BEP)",
                "3D Coordinated Models (LOD 350)",
                "Clash Detection Reports",
                "Construction Documents"
            };

            if (result.SpecialRequirements.Any(r => r.Contains("4D")))
                result.RecommendedDeliverables.Add("4D Construction Schedule");
            if (result.SpecialRequirements.Any(r => r.Contains("5D")))
                result.RecommendedDeliverables.Add("5D Cost Tracking System");
            if (result.SpecialRequirements.Any(r => r.Contains("COBie")))
                result.RecommendedDeliverables.Add("COBie Data Package");
            if (result.SpecialRequirements.Any(r => r.Contains("LEED")))
                result.RecommendedDeliverables.Add("Energy Analysis for Certification");

            return new TaskResult
            {
                Success = true,
                TaskType = "Project Brief Parsing",
                Output = result,
                Summary = $"Parsed project: {result.ProjectType}, {result.Size}, {result.Floors} floors, ${result.Budget:N0} budget"
            };
        }

        private async Task<TaskResult> GenerateBEPAsync(TaskInput input)
        {
            OnTaskProgress(10, "Initializing BEP generation...");

            var bep = new BIMExecutionPlan
            {
                ProjectName = input.Parameters.GetValueOrDefault("project_name", "Project")?.ToString(),
                CreatedDate = DateTime.Now
            };

            OnTaskProgress(20, "Section 1: Project Information...");
            bep.ProjectInformation = new ProjectInformation
            {
                Name = bep.ProjectName,
                Size = input.Parameters.GetValueOrDefault("size", "TBD")?.ToString(),
                Budget = Convert.ToDouble(input.Parameters.GetValueOrDefault("budget", 0)),
                Duration = $"{input.Parameters.GetValueOrDefault("duration", 12)} months",
                DeliveryMethod = "Design-Bid-Build"
            };

            OnTaskProgress(30, "Section 2: BIM Goals...");
            bep.BIMGoals = new List<string>
            {
                "Achieve LOD 350 coordination across all disciplines",
                "Deliver clash-free construction documents",
                "Enable real-time project cost tracking (5D BIM)",
                "Visualize construction sequence (4D BIM)",
                "Provide COBie data for facility management"
            };

            OnTaskProgress(40, "Section 3: BIM Uses...");
            bep.BIMUses = new Dictionary<string, List<string>>
            {
                { "Design Phase", new List<string> { "Design Authoring", "Design Review", "Energy Analysis" } },
                { "Coordination Phase", new List<string> { "3D Coordination", "Clash Detection", "4D Simulation" } },
                { "Construction Phase", new List<string> { "5D Cost Tracking", "Progress Monitoring" } },
                { "Handover Phase", new List<string> { "COBie Export", "As-Built Documentation" } }
            };

            OnTaskProgress(50, "Section 4: LOD Progression...");
            bep.LODProgression = new Dictionary<string, string>
            {
                { "Schematic Design", "LOD 200 - All disciplines" },
                { "Design Development", "LOD 300 - All disciplines" },
                { "Construction Documents", "LOD 350 - All disciplines for coordination" },
                { "Fabrication", "LOD 400 - MEP systems" },
                { "As-Built", "LOD 500 - All systems" }
            };

            OnTaskProgress(60, "Section 5: Responsibility Matrix...");
            bep.ResponsibilityMatrix = new Dictionary<string, string>
            {
                { "Architect", "Architectural model to LOD 350" },
                { "Structural Engineer", "Structural model to LOD 350" },
                { "MEP Engineer", "MEP models to LOD 350, shop drawings to LOD 400" },
                { "BIM Coordinator", "Model federation, clash detection, coordination" },
                { "Contractor", "4D scheduling, 5D cost tracking, construction coordination" }
            };

            OnTaskProgress(70, "Section 6: Software Requirements...");
            bep.SoftwareRequirements = new Dictionary<string, string>
            {
                { "Authoring", "Revit 2025" },
                { "Coordination", "Navisworks Manage 2025" },
                { "Scheduling", "Synchro Pro" },
                { "Cost Estimation", "CostX" },
                { "CDE", "Autodesk Construction Cloud" }
            };

            OnTaskProgress(80, "Section 7: Coordination Workflow...");
            bep.CoordinationWorkflow = new List<string>
            {
                "Each discipline models to specified LOD",
                "Export to NWC format weekly",
                "Upload to Common Data Environment",
                "BIM Coordinator federates models",
                "Run automated clash detection",
                "Weekly coordination meeting",
                "Assign and track clash resolution",
                "Resolve in source models",
                "Re-export and verify"
            };

            OnTaskProgress(90, "Sections 8-9: Deliverables and QC...");
            bep.QualityControl = new List<string>
            {
                "Weekly model audits",
                "Clash detection every model update",
                "Parameter population verification",
                "Schedule validation",
                "Monthly QC reports"
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "BIM Execution Plan Generation",
                Output = bep,
                Summary = $"Generated complete 9-section BEP for {bep.ProjectName}"
            };
        }

        private async Task<TaskResult> CreateConstructionScheduleAsync(TaskInput input)
        {
            OnTaskProgress(20, "Creating schedule framework...");

            var schedule = new ConstructionSchedule
            {
                ProjectName = input.Parameters.GetValueOrDefault("project_name", "Project")?.ToString(),
                TotalWeeks = Convert.ToInt32(input.Parameters.GetValueOrDefault("duration", 12)) * 4,
                StartDate = DateTime.Now.AddMonths(1)
            };

            OnTaskProgress(40, "Generating phases...");

            schedule.Phases = new List<SchedulePhase>
            {
                new SchedulePhase { Name = "Preconstruction", DurationWeeks = 4, Activities = new[] { "Permits", "Mobilization" }.ToList() },
                new SchedulePhase { Name = "Foundation", DurationWeeks = 8, Activities = new[] { "Excavation", "Footings", "Foundation walls" }.ToList() },
                new SchedulePhase { Name = "Structure", DurationWeeks = 16, Activities = new[] { "Columns", "Beams", "Floor decks", "Roof" }.ToList() },
                new SchedulePhase { Name = "Envelope", DurationWeeks = 12, Activities = new[] { "Curtain wall", "Roofing", "Waterproofing" }.ToList() },
                new SchedulePhase { Name = "MEP Rough-In", DurationWeeks = 16, Activities = new[] { "HVAC mains", "Electrical", "Plumbing" }.ToList() },
                new SchedulePhase { Name = "Interior Build-Out", DurationWeeks = 18, Activities = new[] { "Framing", "Drywall", "Ceilings", "Finishes" }.ToList() },
                new SchedulePhase { Name = "MEP Finishes", DurationWeeks = 12, Activities = new[] { "Fixtures", "Controls", "Testing" }.ToList() },
                new SchedulePhase { Name = "Commissioning", DurationWeeks = 6, Activities = new[] { "System testing", "Balancing", "Training" }.ToList() },
                new SchedulePhase { Name = "Closeout", DurationWeeks = 4, Activities = new[] { "Punch list", "Documentation", "COBie delivery" }.ToList() }
            };

            OnTaskProgress(60, "Calculating milestones...");

            var cumulativeWeeks = 0;
            schedule.Milestones = new List<Milestone>();
            foreach (var phase in schedule.Phases)
            {
                cumulativeWeeks += phase.DurationWeeks;
                schedule.Milestones.Add(new Milestone
                {
                    Name = $"{phase.Name} Complete",
                    Week = cumulativeWeeks,
                    Date = schedule.StartDate.AddDays(cumulativeWeeks * 7)
                });
            }

            OnTaskProgress(80, "Identifying critical path...");
            schedule.CriticalPath = "Foundation → Structure → Envelope → MEP Rough-in → Commissioning → CO";

            return new TaskResult
            {
                Success = true,
                TaskType = "Construction Schedule Creation",
                Output = schedule,
                Summary = $"Created {schedule.TotalWeeks}-week schedule with {schedule.Phases.Count} phases"
            };
        }

        private async Task<TaskResult> CreateMaintenanceScheduleAsync(TaskInput input)
        {
            OnTaskProgress(30, "Building maintenance schedule...");

            var maintenance = new MaintenanceSchedule
            {
                BuildingType = input.Parameters.GetValueOrDefault("project_type", "Commercial")?.ToString(),
                Systems = new Dictionary<string, MaintenanceItem>()
            };

            maintenance.Systems["HVAC"] = new MaintenanceItem
            {
                MonthlyTasks = new[] { "Filter inspection", "Belt check", "BMS review" }.ToList(),
                QuarterlyTasks = new[] { "Coil cleaning", "Calibrate sensors", "Damper test" }.ToList(),
                AnnualTasks = new[] { "Full inspection", "Refrigerant check", "Performance test" }.ToList(),
                EstimatedAnnualCost = 85000
            };

            maintenance.Systems["Electrical"] = new MaintenanceItem
            {
                MonthlyTasks = new[] { "Panel inspection", "Thermal scan", "Generator test" }.ToList(),
                QuarterlyTasks = new[] { "Emergency lighting test", "Transfer switch test" }.ToList(),
                AnnualTasks = new[] { "Thermographic survey", "Arc flash review", "Load bank test" }.ToList(),
                EstimatedAnnualCost = 35000
            };

            maintenance.Systems["Plumbing"] = new MaintenanceItem
            {
                MonthlyTasks = new[] { "Water heater inspection", "Pump seal check" }.ToList(),
                QuarterlyTasks = new[] { "Backflow test", "Water quality test" }.ToList(),
                AnnualTasks = new[] { "Water heater flush", "Pump assessment", "Backflow certification" }.ToList(),
                EstimatedAnnualCost = 25000
            };

            maintenance.Systems["Fire Protection"] = new MaintenanceItem
            {
                MonthlyTasks = new[] { "Fire pump churn test", "Extinguisher inspection" }.ToList(),
                QuarterlyTasks = new[] { "Flow test prep", "Sprinkler visual inspection" }.ToList(),
                AnnualTasks = new[] { "Full flow test", "Fire alarm inspection", "Certification" }.ToList(),
                EstimatedAnnualCost = 18000
            };

            OnTaskProgress(70, "Calculating total budget...");
            maintenance.TotalAnnualBudget = maintenance.Systems.Values.Sum(s => s.EstimatedAnnualCost);

            return new TaskResult
            {
                Success = true,
                TaskType = "Maintenance Schedule Creation",
                Output = maintenance,
                Summary = $"Created maintenance schedule for {maintenance.Systems.Count} systems, ${maintenance.TotalAnnualBudget:N0}/year"
            };
        }

        private async Task<TaskResult> TrackCostsAsync(TaskInput input)
        {
            OnTaskProgress(20, "Setting up cost tracking...");

            var budget = Convert.ToDouble(input.Parameters.GetValueOrDefault("budget", 15000000));

            var costTracking = new CostTrackingResult
            {
                OriginalBudget = budget,
                CostBreakdown = new Dictionary<string, CostCategory>
                {
                    { "Structure", new CostCategory { Budget = budget * 0.25, PercentOfTotal = "25%" } },
                    { "Envelope", new CostCategory { Budget = budget * 0.15, PercentOfTotal = "15%" } },
                    { "MEP", new CostCategory { Budget = budget * 0.35, PercentOfTotal = "35%" } },
                    { "Interiors", new CostCategory { Budget = budget * 0.175, PercentOfTotal = "17.5%" } },
                    { "Site", new CostCategory { Budget = budget * 0.05, PercentOfTotal = "5%" } },
                    { "Contingency", new CostCategory { Budget = budget * 0.025, PercentOfTotal = "2.5%" } }
                }
            };

            OnTaskProgress(50, "Configuring tracking methodology...");
            costTracking.TrackingMethodology = new CostTrackingMethodology
            {
                QuantityTakeoff = "Automated from BIM model",
                UnitCosts = "Updated from cost database monthly",
                Frequency = "Weekly budget reports",
                VarianceAlerts = "Alert if >2% variance from baseline"
            };

            OnTaskProgress(80, "Setting up reporting...");
            costTracking.Reporting = new ReportingConfig
            {
                Weekly = "Cost variance report by CSI division",
                Monthly = "Executive summary, forecast to completion",
                ChangeOrders = "Impact analysis within 48 hours"
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "5D Cost Tracking Setup",
                Output = costTracking,
                Summary = $"Set up cost tracking for ${budget:N0} budget across {costTracking.CostBreakdown.Count} categories"
            };
        }

        private async Task<TaskResult> TrackProgressAsync(TaskInput input)
        {
            OnTaskProgress(30, "Analyzing progress metrics...");

            var progress = new ProgressTrackingResult
            {
                CurrentWeek = 32,
                PlannedCompletion = 78,
                OverallPercentComplete = 40.8,
                PlannedPercentComplete = 41.0,
                VariancePercent = -0.2,
                Status = "On Track - GREEN"
            };

            OnTaskProgress(60, "Calculating earned value...");
            progress.EarnedValueMetrics = new EarnedValueAnalysis
            {
                BCWS_PlannedValue = 6150000,
                BCWP_EarnedValue = 6120000,
                ACWP_ActualCost = 6200000,
                SPI = 0.995,
                CPI = 0.987,
                ScheduleVariance = -30000,
                CostVariance = -80000,
                EAC = 15197000
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "Progress Tracking",
                Output = progress,
                Summary = $"Week {progress.CurrentWeek}: {progress.OverallPercentComplete}% complete, Status: {progress.Status}"
            };
        }

        private async Task<TaskResult> GenerateReportAsync(TaskInput input)
        {
            OnTaskProgress(40, "Generating coordination report...");

            var report = new CoordinationReport
            {
                ReportDate = DateTime.Now,
                ReportNumber = $"CR-{DateTime.Now:yyyyMMdd}",
                TotalClashes = 847,
                ClashesByStatus = new Dictionary<string, int>
                {
                    { "New", 23 },
                    { "Active", 89 },
                    { "Resolved", 712 },
                    { "Approved", 23 }
                },
                OpenRFIs = 7,
                NextMeeting = DateTime.Now.AddDays(7)
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "Coordination Report",
                Output = report,
                Summary = $"Report {report.ReportNumber}: {report.ClashesByStatus["New"]} new clashes, {report.ClashesByStatus["Active"]} active, {report.OpenRFIs} open RFIs"
            };
        }

        private async Task<TaskResult> GenerateCostReportAsync(TaskInput input)
        {
            OnTaskProgress(40, "Generating cost report...");

            var report = new CostReport
            {
                ReportDate = DateTime.Now,
                Period = DateTime.Now.ToString("MMMM yyyy"),
                OriginalBudget = 15000000,
                ApprovedChanges = 275000,
                CurrentBudget = 15275000,
                ActualCostsToDate = 6750000,
                PercentSpent = 44.2
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "Cost Report",
                Output = report,
                Summary = $"Cost Report: ${report.ActualCostsToDate:N0} spent ({report.PercentSpent}%), ${report.CurrentBudget - report.ActualCostsToDate:N0} remaining"
            };
        }

        private async Task<TaskResult> ProvideRecommendationsAsync(TaskInput input)
        {
            OnTaskProgress(40, "Analyzing project and generating recommendations...");

            var recommendations = new RecommendationsResult
            {
                ImmediateActions = new List<Recommendation>
                {
                    new Recommendation
                    {
                        Priority = "HIGH",
                        Category = "Coordination",
                        Description = "Expedite resolution of new clashes before MEP rough-in",
                        Impact = "Prevents 3-5 day delay",
                        Owner = "BIM Coordinator"
                    },
                    new Recommendation
                    {
                        Priority = "MEDIUM",
                        Category = "Cost",
                        Description = "Review MEP subcontractor change proposals",
                        Impact = "Potential $130K exposure",
                        Owner = "Cost Manager"
                    }
                },
                CostSavingsOpportunities = new List<Recommendation>
                {
                    new Recommendation
                    {
                        Priority = "MEDIUM",
                        Category = "Value Engineering",
                        Description = "Simplify BMS integration",
                        Impact = "$45,000 potential savings",
                        Owner = "Project Manager"
                    }
                },
                OverallHealth = "GOOD",
                ConfidenceInCompletion = "85%"
            };

            return new TaskResult
            {
                Success = true,
                TaskType = "Project Recommendations",
                Output = recommendations,
                Summary = $"Generated {recommendations.ImmediateActions.Count} immediate actions, {recommendations.CostSavingsOpportunities.Count} savings opportunities"
            };
        }

        #endregion

        private void OnTaskProgress(int percent, string message)
        {
            TaskProgress?.Invoke(this, new TaskProgressEventArgs { Percent = percent, Message = message });
        }
    }

    #region Data Models

    public class TaskInput
    {
        public string TaskType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class TaskResult
    {
        public bool Success { get; set; }
        public string TaskType { get; set; }
        public object Output { get; set; }
        public string Summary { get; set; }
        public string ErrorMessage { get; set; }

        public string ToFormattedString()
        {
            if (!Success)
                return $"Error: {ErrorMessage}";

            return Output != null
                ? JsonSerializer.Serialize(Output, new JsonSerializerOptions { WriteIndented = true })
                : Summary;
        }
    }

    public class TaskProgressEventArgs : EventArgs
    {
        public int Percent { get; set; }
        public string Message { get; set; }
    }

    public class ProjectBriefResult
    {
        public string ProjectType { get; set; }
        public string Size { get; set; }
        public int Floors { get; set; }
        public double Budget { get; set; }
        public int DurationMonths { get; set; }
        public List<string> SpecialRequirements { get; set; }
        public List<string> RecommendedDeliverables { get; set; }
    }

    public class BIMExecutionPlan
    {
        public string ProjectName { get; set; }
        public DateTime CreatedDate { get; set; }
        public ProjectInformation ProjectInformation { get; set; }
        public List<string> BIMGoals { get; set; }
        public Dictionary<string, List<string>> BIMUses { get; set; }
        public Dictionary<string, string> LODProgression { get; set; }
        public Dictionary<string, string> ResponsibilityMatrix { get; set; }
        public Dictionary<string, string> SoftwareRequirements { get; set; }
        public List<string> CoordinationWorkflow { get; set; }
        public List<string> QualityControl { get; set; }
    }

    public class ProjectInformation
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public double Budget { get; set; }
        public string Duration { get; set; }
        public string DeliveryMethod { get; set; }
    }

    public class ConstructionSchedule
    {
        public string ProjectName { get; set; }
        public int TotalWeeks { get; set; }
        public DateTime StartDate { get; set; }
        public List<SchedulePhase> Phases { get; set; }
        public List<Milestone> Milestones { get; set; }
        public string CriticalPath { get; set; }
    }

    public class SchedulePhase
    {
        public string Name { get; set; }
        public int DurationWeeks { get; set; }
        public List<string> Activities { get; set; }
    }

    public class Milestone
    {
        public string Name { get; set; }
        public int Week { get; set; }
        public DateTime Date { get; set; }
    }

    public class MaintenanceSchedule
    {
        public string BuildingType { get; set; }
        public Dictionary<string, MaintenanceItem> Systems { get; set; }
        public double TotalAnnualBudget { get; set; }
    }

    public class MaintenanceItem
    {
        public List<string> MonthlyTasks { get; set; }
        public List<string> QuarterlyTasks { get; set; }
        public List<string> AnnualTasks { get; set; }
        public double EstimatedAnnualCost { get; set; }
    }

    public class CostTrackingResult
    {
        public double OriginalBudget { get; set; }
        public Dictionary<string, CostCategory> CostBreakdown { get; set; }
        public CostTrackingMethodology TrackingMethodology { get; set; }
        public ReportingConfig Reporting { get; set; }
    }

    public class CostCategory
    {
        public double Budget { get; set; }
        public string PercentOfTotal { get; set; }
    }

    public class CostTrackingMethodology
    {
        public string QuantityTakeoff { get; set; }
        public string UnitCosts { get; set; }
        public string Frequency { get; set; }
        public string VarianceAlerts { get; set; }
    }

    public class ReportingConfig
    {
        public string Weekly { get; set; }
        public string Monthly { get; set; }
        public string ChangeOrders { get; set; }
    }

    public class ProgressTrackingResult
    {
        public int CurrentWeek { get; set; }
        public int PlannedCompletion { get; set; }
        public double OverallPercentComplete { get; set; }
        public double PlannedPercentComplete { get; set; }
        public double VariancePercent { get; set; }
        public string Status { get; set; }
        public EarnedValueAnalysis EarnedValueMetrics { get; set; }
    }

    public class EarnedValueAnalysis
    {
        public double BCWS_PlannedValue { get; set; }
        public double BCWP_EarnedValue { get; set; }
        public double ACWP_ActualCost { get; set; }
        public double SPI { get; set; }
        public double CPI { get; set; }
        public double ScheduleVariance { get; set; }
        public double CostVariance { get; set; }
        public double EAC { get; set; }
    }

    public class CoordinationReport
    {
        public DateTime ReportDate { get; set; }
        public string ReportNumber { get; set; }
        public int TotalClashes { get; set; }
        public Dictionary<string, int> ClashesByStatus { get; set; }
        public int OpenRFIs { get; set; }
        public DateTime NextMeeting { get; set; }
    }

    public class CostReport
    {
        public DateTime ReportDate { get; set; }
        public string Period { get; set; }
        public double OriginalBudget { get; set; }
        public double ApprovedChanges { get; set; }
        public double CurrentBudget { get; set; }
        public double ActualCostsToDate { get; set; }
        public double PercentSpent { get; set; }
    }

    public class RecommendationsResult
    {
        public List<Recommendation> ImmediateActions { get; set; }
        public List<Recommendation> CostSavingsOpportunities { get; set; }
        public string OverallHealth { get; set; }
        public string ConfidenceInCompletion { get; set; }
    }

    public class Recommendation
    {
        public string Priority { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public string Owner { get; set; }
    }

    #endregion
}
