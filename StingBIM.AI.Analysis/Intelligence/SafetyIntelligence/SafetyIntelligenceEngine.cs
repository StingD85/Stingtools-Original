// ===================================================================
// StingBIM Safety Intelligence Engine - JHA & Incident Management
// Job hazard analysis, safety planning, toolbox talks, incident tracking
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.SafetyIntelligence
{
    /// <summary>
    /// Comprehensive construction safety management with job hazard analysis,
    /// incident tracking, safety planning, and compliance monitoring
    /// </summary>
    public sealed class SafetyIntelligenceEngine
    {
        private static readonly Lazy<SafetyIntelligenceEngine> _instance =
            new Lazy<SafetyIntelligenceEngine>(() => new SafetyIntelligenceEngine());
        public static SafetyIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, SafetyProject> _projects;
        private readonly Dictionary<string, JobHazardAnalysis> _jhas;
        private readonly Dictionary<string, SafetyIncident> _incidents;
        private readonly Dictionary<string, SafetyInspection> _inspections;
        private readonly Dictionary<string, ToolboxTalk> _toolboxTalks;
        private readonly List<HazardTemplate> _hazardTemplates;
        private readonly List<PPERequirement> _ppeRequirements;
        private readonly object _lockObject = new object();

        public event EventHandler<SafetyAlertEventArgs> SafetyAlert;
        public event EventHandler<IncidentReportedEventArgs> IncidentReported;

        private SafetyIntelligenceEngine()
        {
            _projects = new Dictionary<string, SafetyProject>();
            _jhas = new Dictionary<string, JobHazardAnalysis>();
            _incidents = new Dictionary<string, SafetyIncident>();
            _inspections = new Dictionary<string, SafetyInspection>();
            _toolboxTalks = new Dictionary<string, ToolboxTalk>();
            _hazardTemplates = new List<HazardTemplate>();
            _ppeRequirements = new List<PPERequirement>();
            InitializeHazardTemplates();
            InitializePPERequirements();
        }

        #region Project Management

        public SafetyProject CreateProject(string projectId, string projectName)
        {
            var project = new SafetyProject
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                CreatedDate = DateTime.Now,
                Status = ProjectStatus.Active,
                SafetyPlan = new SafetyPlan(),
                JHAs = new List<string>(),
                Incidents = new List<string>(),
                Inspections = new List<string>(),
                ToolboxTalks = new List<string>(),
                EmergencyContacts = new List<EmergencyContact>(),
                Statistics = new SafetyStatistics()
            };

            lock (_lockObject)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        #endregion

        #region Job Hazard Analysis

        public JobHazardAnalysis CreateJHA(JHAInput input)
        {
            var jha = new JobHazardAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateJHANumber(input.ProjectId),
                TaskName = input.TaskName,
                TaskDescription = input.TaskDescription,
                WorkArea = input.WorkArea,
                TradeContractor = input.TradeContractor,
                PreparedBy = input.PreparedBy,
                PreparedDate = DateTime.Now,
                ReviewedBy = input.ReviewedBy,
                Status = JHAStatus.Draft,
                Steps = new List<JHAStep>(),
                RequiredPPE = new List<string>(),
                RequiredTraining = new List<string>(),
                RequiredPermits = new List<string>(),
                EmergencyProcedures = new List<string>()
            };

            // Auto-populate based on task type
            PopulateJHAFromTemplates(jha, input.TaskType);

            lock (_lockObject)
            {
                _jhas[jha.Id] = jha;
                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.JHAs.Add(jha.Id);
                }
            }

            return jha;
        }

        public void AddJHAStep(string jhaId, JHAStepInput stepInput)
        {
            lock (_lockObject)
            {
                if (_jhas.TryGetValue(jhaId, out var jha))
                {
                    var step = new JHAStep
                    {
                        StepNumber = jha.Steps.Count + 1,
                        Description = stepInput.Description,
                        Hazards = stepInput.Hazards ?? new List<Hazard>(),
                        Controls = stepInput.Controls ?? new List<HazardControl>()
                    };

                    jha.Steps.Add(step);
                }
            }
        }

        public async Task<JHARiskAssessment> AssessJHARisksAsync(string jhaId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_jhas.TryGetValue(jhaId, out var jha))
                        return null;

                    var assessment = new JHARiskAssessment
                    {
                        JHAId = jhaId,
                        AssessmentDate = DateTime.Now,
                        OverallRiskLevel = RiskLevel.Low,
                        StepAssessments = new List<StepRiskAssessment>(),
                        Recommendations = new List<string>()
                    };

                    var maxRisk = RiskLevel.Low;

                    foreach (var step in jha.Steps)
                    {
                        var stepAssessment = new StepRiskAssessment
                        {
                            StepNumber = step.StepNumber,
                            HazardRatings = new List<HazardRating>()
                        };

                        foreach (var hazard in step.Hazards)
                        {
                            var rating = new HazardRating
                            {
                                HazardId = hazard.Id,
                                HazardDescription = hazard.Description,
                                InherentRisk = CalculateRiskLevel(hazard.Severity, hazard.Likelihood),
                                ControlEffectiveness = EvaluateControls(hazard, step.Controls),
                                ResidualRisk = RiskLevel.Low
                            };

                            // Calculate residual risk
                            rating.ResidualRisk = CalculateResidualRisk(rating.InherentRisk, rating.ControlEffectiveness);

                            if (rating.ResidualRisk > maxRisk)
                                maxRisk = rating.ResidualRisk;

                            stepAssessment.HazardRatings.Add(rating);
                        }

                        assessment.StepAssessments.Add(stepAssessment);
                    }

                    assessment.OverallRiskLevel = maxRisk;

                    // Generate recommendations
                    if (maxRisk >= RiskLevel.High)
                    {
                        assessment.Recommendations.Add("Additional engineering controls recommended");
                        assessment.Recommendations.Add("Supervisor presence required during task");
                    }

                    if (maxRisk == RiskLevel.Critical)
                    {
                        assessment.Recommendations.Add("Task requires management approval before proceeding");
                        assessment.Recommendations.Add("Consider alternative methods to reduce risk");
                    }

                    return assessment;
                }
            });
        }

        private RiskLevel CalculateRiskLevel(SeverityLevel severity, LikelihoodLevel likelihood)
        {
            var score = (int)severity * (int)likelihood;
            if (score >= 16) return RiskLevel.Critical;
            if (score >= 9) return RiskLevel.High;
            if (score >= 4) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        private double EvaluateControls(Hazard hazard, List<HazardControl> controls)
        {
            var relevantControls = controls.Where(c => c.HazardId == hazard.Id).ToList();
            if (!relevantControls.Any()) return 0;

            // Higher effectiveness for engineering controls, lower for PPE
            var effectiveness = relevantControls.Sum(c => c.Type switch
            {
                ControlType.Elimination => 1.0,
                ControlType.Substitution => 0.9,
                ControlType.Engineering => 0.7,
                ControlType.Administrative => 0.5,
                ControlType.PPE => 0.3,
                _ => 0.2
            });

            return Math.Min(effectiveness, 1.0);
        }

        private RiskLevel CalculateResidualRisk(RiskLevel inherentRisk, double controlEffectiveness)
        {
            var reduction = (int)inherentRisk * controlEffectiveness;
            var residualScore = (int)inherentRisk - (int)reduction;
            if (residualScore >= 3) return RiskLevel.High;
            if (residualScore >= 2) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        private void PopulateJHAFromTemplates(JobHazardAnalysis jha, TaskType taskType)
        {
            var templates = _hazardTemplates.Where(t => t.ApplicableTasks.Contains(taskType)).ToList();

            foreach (var template in templates)
            {
                jha.RequiredPPE.AddRange(template.RequiredPPE);
                jha.RequiredTraining.AddRange(template.RequiredTraining);
            }

            jha.RequiredPPE = jha.RequiredPPE.Distinct().ToList();
            jha.RequiredTraining = jha.RequiredTraining.Distinct().ToList();
        }

        private string GenerateJHANumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _jhas.Values.Count(j => j.ProjectId == projectId) + 1;
                return $"JHA-{count:D4}";
            }
        }

        #endregion

        #region Incident Management

        public SafetyIncident ReportIncident(IncidentReport report)
        {
            var incident = new SafetyIncident
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = report.ProjectId,
                Number = GenerateIncidentNumber(report.ProjectId),
                Type = report.Type,
                Severity = report.Severity,
                DateTime = report.DateTime,
                Location = report.Location,
                Description = report.Description,
                InjuredPersons = report.InjuredPersons ?? new List<InjuredPerson>(),
                Witnesses = report.Witnesses ?? new List<Witness>(),
                ReportedBy = report.ReportedBy,
                ReportedDate = DateTime.Now,
                Status = IncidentStatus.Reported,
                ImmediateActions = report.ImmediateActions ?? new List<string>(),
                Photos = report.Photos ?? new List<string>(),
                Investigation = null,
                CorrectiveActions = new List<CorrectiveAction>()
            };

            lock (_lockObject)
            {
                _incidents[incident.Id] = incident;
                if (_projects.TryGetValue(report.ProjectId, out var project))
                {
                    project.Incidents.Add(incident.Id);
                    UpdateSafetyStatistics(project, incident);
                }
            }

            // Raise alert for serious incidents
            if (incident.Severity >= IncidentSeverity.Serious)
            {
                OnIncidentReported(new IncidentReportedEventArgs
                {
                    IncidentId = incident.Id,
                    Type = incident.Type,
                    Severity = incident.Severity,
                    Description = incident.Description
                });
            }

            return incident;
        }

        public void InvestigateIncident(string incidentId, IncidentInvestigation investigation)
        {
            lock (_lockObject)
            {
                if (_incidents.TryGetValue(incidentId, out var incident))
                {
                    investigation.StartDate = DateTime.Now;
                    incident.Investigation = investigation;
                    incident.Status = IncidentStatus.UnderInvestigation;
                }
            }
        }

        public void CompleteInvestigation(string incidentId, string rootCause, List<CorrectiveAction> actions)
        {
            lock (_lockObject)
            {
                if (_incidents.TryGetValue(incidentId, out var incident))
                {
                    if (incident.Investigation != null)
                    {
                        incident.Investigation.CompletedDate = DateTime.Now;
                        incident.Investigation.RootCause = rootCause;
                    }

                    incident.CorrectiveActions = actions;
                    incident.Status = IncidentStatus.Investigated;
                }
            }
        }

        public void CloseIncident(string incidentId, string closedBy, string closureNotes)
        {
            lock (_lockObject)
            {
                if (_incidents.TryGetValue(incidentId, out var incident))
                {
                    // Verify all corrective actions are complete
                    var allComplete = incident.CorrectiveActions.All(a => a.Status == ActionStatus.Completed);
                    if (!allComplete)
                    {
                        throw new InvalidOperationException("Cannot close incident with incomplete corrective actions");
                    }

                    incident.Status = IncidentStatus.Closed;
                    incident.ClosedDate = DateTime.Now;
                    incident.ClosedBy = closedBy;
                }
            }
        }

        private void UpdateSafetyStatistics(SafetyProject project, SafetyIncident incident)
        {
            project.Statistics.TotalIncidents++;

            switch (incident.Type)
            {
                case IncidentType.Injury:
                    project.Statistics.TotalInjuries++;
                    if (incident.InjuredPersons.Any(p => p.MedicalTreatment))
                        project.Statistics.RecordableInjuries++;
                    if (incident.InjuredPersons.Any(p => p.LostTimeDays > 0))
                        project.Statistics.LostTimeInjuries++;
                    break;
                case IncidentType.NearMiss:
                    project.Statistics.NearMisses++;
                    break;
                case IncidentType.PropertyDamage:
                    project.Statistics.PropertyDamageIncidents++;
                    break;
            }
        }

        private string GenerateIncidentNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _incidents.Values.Count(i => i.ProjectId == projectId) + 1;
                return $"INC-{count:D4}";
            }
        }

        #endregion

        #region Safety Inspections

        public SafetyInspection CreateInspection(InspectionInput input)
        {
            var inspection = new SafetyInspection
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Number = GenerateInspectionNumber(input.ProjectId),
                Type = input.Type,
                Area = input.Area,
                Inspector = input.Inspector,
                InspectionDate = input.InspectionDate ?? DateTime.Now,
                Status = InspectionStatus.InProgress,
                Checklist = GenerateChecklist(input.Type),
                Findings = new List<InspectionFinding>(),
                OverallScore = 0
            };

            lock (_lockObject)
            {
                _inspections[inspection.Id] = inspection;
                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.Inspections.Add(inspection.Id);
                }
            }

            return inspection;
        }

        public void RecordFinding(string inspectionId, InspectionFinding finding)
        {
            lock (_lockObject)
            {
                if (_inspections.TryGetValue(inspectionId, out var inspection))
                {
                    finding.Id = Guid.NewGuid().ToString();
                    finding.FoundDate = DateTime.Now;
                    inspection.Findings.Add(finding);
                }
            }
        }

        public void CompleteInspection(string inspectionId, string completedBy)
        {
            lock (_lockObject)
            {
                if (_inspections.TryGetValue(inspectionId, out var inspection))
                {
                    // Calculate overall score
                    var totalItems = inspection.Checklist.Count;
                    var compliantItems = inspection.Checklist.Count(c => c.Status == ChecklistStatus.Compliant);
                    inspection.OverallScore = totalItems > 0 ? (compliantItems * 100.0 / totalItems) : 100;

                    inspection.Status = InspectionStatus.Completed;
                    inspection.CompletedDate = DateTime.Now;
                    inspection.CompletedBy = completedBy;

                    // Raise alert for low scores
                    if (inspection.OverallScore < 70)
                    {
                        OnSafetyAlert(new SafetyAlertEventArgs
                        {
                            ProjectId = inspection.ProjectId,
                            AlertType = SafetyAlertType.LowInspectionScore,
                            Message = $"Safety inspection {inspection.Number} scored {inspection.OverallScore:F0}%"
                        });
                    }
                }
            }
        }

        private List<ChecklistItem> GenerateChecklist(InspectionType type)
        {
            var checklist = new List<ChecklistItem>();

            switch (type)
            {
                case InspectionType.Weekly:
                    checklist.AddRange(new[]
                    {
                        new ChecklistItem { Category = "PPE", Question = "Workers wearing required PPE", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Housekeeping", Question = "Work areas clean and organized", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Housekeeping", Question = "Walkways clear of obstructions", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Fall Protection", Question = "Guardrails in place where required", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Fall Protection", Question = "Floor openings covered/protected", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Electrical", Question = "Extension cords in good condition", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Electrical", Question = "GFCIs in use for temporary power", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Fire Safety", Question = "Fire extinguishers accessible", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Fire Safety", Question = "Flammable materials properly stored", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Ladders", Question = "Ladders in good condition", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Scaffolding", Question = "Scaffolding properly erected", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Excavation", Question = "Excavations properly sloped/shored", Status = ChecklistStatus.NotChecked }
                    });
                    break;

                case InspectionType.Daily:
                    checklist.AddRange(new[]
                    {
                        new ChecklistItem { Category = "General", Question = "Site secured", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "PPE", Question = "PPE available and worn", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Housekeeping", Question = "Work area clean", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "Equipment", Question = "Equipment in safe condition", Status = ChecklistStatus.NotChecked }
                    });
                    break;

                case InspectionType.OSHA:
                    checklist.AddRange(new[]
                    {
                        new ChecklistItem { Category = "OSHA 1926.20", Question = "Safety program implemented", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.21", Question = "Safety training provided", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.50", Question = "First aid facilities available", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.51", Question = "Sanitation facilities adequate", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.100", Question = "Head protection worn", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.102", Question = "Eye protection provided", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.451", Question = "Scaffolding compliant", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.501", Question = "Fall protection in place", Status = ChecklistStatus.NotChecked },
                        new ChecklistItem { Category = "OSHA 1926.651", Question = "Excavation requirements met", Status = ChecklistStatus.NotChecked }
                    });
                    break;
            }

            return checklist;
        }

        private string GenerateInspectionNumber(string projectId)
        {
            lock (_lockObject)
            {
                var count = _inspections.Values.Count(i => i.ProjectId == projectId) + 1;
                return $"INS-{count:D4}";
            }
        }

        #endregion

        #region Toolbox Talks

        public ToolboxTalk CreateToolboxTalk(ToolboxTalkInput input)
        {
            var talk = new ToolboxTalk
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = input.ProjectId,
                Topic = input.Topic,
                Content = input.Content,
                ScheduledDate = input.ScheduledDate,
                Presenter = input.Presenter,
                Status = ToolboxTalkStatus.Scheduled,
                Attendees = new List<ToolboxAttendee>(),
                Duration = input.Duration,
                RelatedHazards = input.RelatedHazards ?? new List<string>()
            };

            // Auto-generate content from library if not provided
            if (string.IsNullOrEmpty(talk.Content))
            {
                talk.Content = GetToolboxTalkContent(talk.Topic);
            }

            lock (_lockObject)
            {
                _toolboxTalks[talk.Id] = talk;
                if (_projects.TryGetValue(input.ProjectId, out var project))
                {
                    project.ToolboxTalks.Add(talk.Id);
                }
            }

            return talk;
        }

        public void ConductToolboxTalk(string talkId, List<string> attendeeNames, string conductedBy)
        {
            lock (_lockObject)
            {
                if (_toolboxTalks.TryGetValue(talkId, out var talk))
                {
                    talk.Status = ToolboxTalkStatus.Completed;
                    talk.ConductedDate = DateTime.Now;
                    talk.ConductedBy = conductedBy;
                    talk.Attendees = attendeeNames.Select(name => new ToolboxAttendee
                    {
                        Name = name,
                        Signed = true,
                        SignedDate = DateTime.Now
                    }).ToList();
                }
            }
        }

        private string GetToolboxTalkContent(ToolboxTopic topic)
        {
            return topic switch
            {
                ToolboxTopic.FallProtection => "Fall protection is required when working at heights of 6 feet or more...",
                ToolboxTopic.LadderSafety => "Always maintain three points of contact when climbing ladders...",
                ToolboxTopic.PPE => "Personal Protective Equipment is your last line of defense against hazards...",
                ToolboxTopic.Excavation => "Never enter an excavation that has not been properly sloped or shored...",
                ToolboxTopic.Electrical => "Treat all electrical equipment as if it is energized...",
                ToolboxTopic.HeatIllness => "Stay hydrated and take breaks in shaded areas during hot weather...",
                ToolboxTopic.Housekeeping => "A clean worksite is a safe worksite...",
                ToolboxTopic.FirePrevention => "Know the location of fire extinguishers and emergency exits...",
                _ => "Safety topic content not available"
            };
        }

        #endregion

        #region Safety Analytics

        public SafetyMetrics GetSafetyMetrics(string projectId, int manhours)
        {
            lock (_lockObject)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var metrics = new SafetyMetrics
                {
                    ProjectId = projectId,
                    CalculatedDate = DateTime.Now,
                    TotalManhours = manhours,
                    Statistics = project.Statistics,

                    // Calculate rates (per 200,000 manhours - OSHA standard)
                    TRIR = manhours > 0 ? (project.Statistics.RecordableInjuries * 200000.0 / manhours) : 0,
                    DART = manhours > 0 ? (project.Statistics.LostTimeInjuries * 200000.0 / manhours) : 0,
                    LostTimeRate = manhours > 0 ? (project.Statistics.TotalLostDays * 200000.0 / manhours) : 0,

                    // Days since last incident
                    DaysSinceLastIncident = CalculateDaysSinceLastIncident(projectId),

                    // Trend data
                    MonthlyTrend = GenerateMonthlyTrend(projectId)
                };

                // Benchmark comparison
                metrics.IndustryBenchmark = new IndustryBenchmark
                {
                    IndustryTRIR = 3.0,
                    IndustryDART = 1.5,
                    PerformanceRating = metrics.TRIR < 3.0 ? "Above Average" :
                                       metrics.TRIR < 5.0 ? "Average" : "Below Average"
                };

                return metrics;
            }
        }

        private int CalculateDaysSinceLastIncident(string projectId)
        {
            var lastIncident = _incidents.Values
                .Where(i => i.ProjectId == projectId && i.Type == IncidentType.Injury)
                .OrderByDescending(i => i.DateTime)
                .FirstOrDefault();

            if (lastIncident == null)
                return 999; // No incidents

            return (DateTime.Now - lastIncident.DateTime).Days;
        }

        private List<MonthlyTrend> GenerateMonthlyTrend(string projectId)
        {
            var trend = new List<MonthlyTrend>();
            var incidents = _incidents.Values.Where(i => i.ProjectId == projectId).ToList();

            var monthGroups = incidents
                .GroupBy(i => new { i.DateTime.Year, i.DateTime.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

            foreach (var group in monthGroups)
            {
                trend.Add(new MonthlyTrend
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    IncidentCount = group.Count(),
                    InjuryCount = group.Count(i => i.Type == IncidentType.Injury),
                    NearMissCount = group.Count(i => i.Type == IncidentType.NearMiss)
                });
            }

            return trend;
        }

        #endregion

        #region Helper Methods

        private void InitializeHazardTemplates()
        {
            _hazardTemplates.AddRange(new[]
            {
                new HazardTemplate
                {
                    Id = "fall-height",
                    Name = "Fall from Height",
                    Description = "Risk of falling from elevated work areas",
                    ApplicableTasks = new[] { TaskType.RoofWork, TaskType.Scaffolding, TaskType.SteelErection, TaskType.LadderWork },
                    RequiredPPE = new[] { "Hard Hat", "Safety Harness", "Safety Glasses" },
                    RequiredTraining = new[] { "Fall Protection", "Ladder Safety" }
                },
                new HazardTemplate
                {
                    Id = "electrical",
                    Name = "Electrical Hazard",
                    Description = "Risk of electrical shock or electrocution",
                    ApplicableTasks = new[] { TaskType.ElectricalWork, TaskType.Demolition },
                    RequiredPPE = new[] { "Insulated Gloves", "Safety Glasses", "Hard Hat" },
                    RequiredTraining = new[] { "Electrical Safety", "Lockout/Tagout" }
                },
                new HazardTemplate
                {
                    Id = "excavation",
                    Name = "Excavation Hazard",
                    Description = "Risk of cave-in or engulfment",
                    ApplicableTasks = new[] { TaskType.Excavation, TaskType.Trenching },
                    RequiredPPE = new[] { "Hard Hat", "Safety Vest", "Steel-Toe Boots" },
                    RequiredTraining = new[] { "Excavation Safety", "Competent Person" }
                },
                new HazardTemplate
                {
                    Id = "confined-space",
                    Name = "Confined Space",
                    Description = "Risk of asphyxiation or toxic exposure",
                    ApplicableTasks = new[] { TaskType.ConfinedSpace },
                    RequiredPPE = new[] { "Hard Hat", "Respirator", "Gas Monitor" },
                    RequiredTraining = new[] { "Confined Space Entry", "Rescue Procedures" }
                },
                new HazardTemplate
                {
                    Id = "struck-by",
                    Name = "Struck By Object",
                    Description = "Risk of being struck by falling or moving objects",
                    ApplicableTasks = new[] { TaskType.CraneOperations, TaskType.SteelErection, TaskType.Demolition },
                    RequiredPPE = new[] { "Hard Hat", "Safety Glasses", "Safety Vest" },
                    RequiredTraining = new[] { "Rigging", "Signal Person" }
                }
            });
        }

        private void InitializePPERequirements()
        {
            _ppeRequirements.AddRange(new[]
            {
                new PPERequirement { Item = "Hard Hat", Standard = "ANSI Z89.1", Required = true },
                new PPERequirement { Item = "Safety Glasses", Standard = "ANSI Z87.1", Required = true },
                new PPERequirement { Item = "Safety Vest", Standard = "ANSI 107", Required = true },
                new PPERequirement { Item = "Steel-Toe Boots", Standard = "ASTM F2413", Required = true },
                new PPERequirement { Item = "Hearing Protection", Standard = "ANSI S3.19", Required = false },
                new PPERequirement { Item = "Safety Harness", Standard = "ANSI Z359.1", Required = false },
                new PPERequirement { Item = "Respirator", Standard = "NIOSH", Required = false },
                new PPERequirement { Item = "Work Gloves", Standard = "ANSI 105", Required = false }
            });
        }

        #endregion

        #region Events

        private void OnSafetyAlert(SafetyAlertEventArgs e) => SafetyAlert?.Invoke(this, e);
        private void OnIncidentReported(IncidentReportedEventArgs e) => IncidentReported?.Invoke(this, e);

        #endregion
    }

    #region Data Models

    public class SafetyProject
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public SafetyPlan SafetyPlan { get; set; }
        public List<string> JHAs { get; set; }
        public List<string> Incidents { get; set; }
        public List<string> Inspections { get; set; }
        public List<string> ToolboxTalks { get; set; }
        public List<EmergencyContact> EmergencyContacts { get; set; }
        public SafetyStatistics Statistics { get; set; }
    }

    public class SafetyPlan
    {
        public string Id { get; set; }
        public string EmergencyProcedures { get; set; }
        public List<string> SiteRules { get; set; } = new List<string>();
        public List<string> RequiredTraining { get; set; } = new List<string>();
    }

    public class EmergencyContact
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Phone { get; set; }
    }

    public class SafetyStatistics
    {
        public int TotalIncidents { get; set; }
        public int TotalInjuries { get; set; }
        public int RecordableInjuries { get; set; }
        public int LostTimeInjuries { get; set; }
        public int TotalLostDays { get; set; }
        public int NearMisses { get; set; }
        public int PropertyDamageIncidents { get; set; }
    }

    public class JobHazardAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public string TaskName { get; set; }
        public string TaskDescription { get; set; }
        public string WorkArea { get; set; }
        public string TradeContractor { get; set; }
        public string PreparedBy { get; set; }
        public DateTime PreparedDate { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime ReviewedDate { get; set; }
        public JHAStatus Status { get; set; }
        public List<JHAStep> Steps { get; set; }
        public List<string> RequiredPPE { get; set; }
        public List<string> RequiredTraining { get; set; }
        public List<string> RequiredPermits { get; set; }
        public List<string> EmergencyProcedures { get; set; }
    }

    public class JHAInput
    {
        public string ProjectId { get; set; }
        public string TaskName { get; set; }
        public string TaskDescription { get; set; }
        public TaskType TaskType { get; set; }
        public string WorkArea { get; set; }
        public string TradeContractor { get; set; }
        public string PreparedBy { get; set; }
        public string ReviewedBy { get; set; }
    }

    public class JHAStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; }
        public List<Hazard> Hazards { get; set; }
        public List<HazardControl> Controls { get; set; }
    }

    public class JHAStepInput
    {
        public string Description { get; set; }
        public List<Hazard> Hazards { get; set; }
        public List<HazardControl> Controls { get; set; }
    }

    public class Hazard
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public HazardCategory Category { get; set; }
        public SeverityLevel Severity { get; set; }
        public LikelihoodLevel Likelihood { get; set; }
    }

    public class HazardControl
    {
        public string Id { get; set; }
        public string HazardId { get; set; }
        public ControlType Type { get; set; }
        public string Description { get; set; }
    }

    public class JHARiskAssessment
    {
        public string JHAId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public RiskLevel OverallRiskLevel { get; set; }
        public List<StepRiskAssessment> StepAssessments { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class StepRiskAssessment
    {
        public int StepNumber { get; set; }
        public List<HazardRating> HazardRatings { get; set; }
    }

    public class HazardRating
    {
        public string HazardId { get; set; }
        public string HazardDescription { get; set; }
        public RiskLevel InherentRisk { get; set; }
        public double ControlEffectiveness { get; set; }
        public RiskLevel ResidualRisk { get; set; }
    }

    public class HazardTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskType[] ApplicableTasks { get; set; }
        public string[] RequiredPPE { get; set; }
        public string[] RequiredTraining { get; set; }
    }

    public class PPERequirement
    {
        public string Item { get; set; }
        public string Standard { get; set; }
        public bool Required { get; set; }
    }

    public class SafetyIncident
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public DateTime DateTime { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public List<InjuredPerson> InjuredPersons { get; set; }
        public List<Witness> Witnesses { get; set; }
        public string ReportedBy { get; set; }
        public DateTime ReportedDate { get; set; }
        public IncidentStatus Status { get; set; }
        public List<string> ImmediateActions { get; set; }
        public List<string> Photos { get; set; }
        public IncidentInvestigation Investigation { get; set; }
        public List<CorrectiveAction> CorrectiveActions { get; set; }
        public DateTime ClosedDate { get; set; }
        public string ClosedBy { get; set; }
    }

    public class IncidentReport
    {
        public string ProjectId { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public DateTime DateTime { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public List<InjuredPerson> InjuredPersons { get; set; }
        public List<Witness> Witnesses { get; set; }
        public string ReportedBy { get; set; }
        public List<string> ImmediateActions { get; set; }
        public List<string> Photos { get; set; }
    }

    public class InjuredPerson
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public string InjuryType { get; set; }
        public string BodyPart { get; set; }
        public bool MedicalTreatment { get; set; }
        public int LostTimeDays { get; set; }
    }

    public class Witness
    {
        public string Name { get; set; }
        public string Company { get; set; }
        public string Phone { get; set; }
        public string Statement { get; set; }
    }

    public class IncidentInvestigation
    {
        public string InvestigatorId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime CompletedDate { get; set; }
        public string RootCause { get; set; }
        public List<string> ContributingFactors { get; set; }
    }

    public class CorrectiveAction
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public ActionStatus Status { get; set; }
        public DateTime CompletedDate { get; set; }
    }

    public class SafetyInspection
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Number { get; set; }
        public InspectionType Type { get; set; }
        public string Area { get; set; }
        public string Inspector { get; set; }
        public DateTime InspectionDate { get; set; }
        public InspectionStatus Status { get; set; }
        public List<ChecklistItem> Checklist { get; set; }
        public List<InspectionFinding> Findings { get; set; }
        public double OverallScore { get; set; }
        public DateTime CompletedDate { get; set; }
        public string CompletedBy { get; set; }
    }

    public class InspectionInput
    {
        public string ProjectId { get; set; }
        public InspectionType Type { get; set; }
        public string Area { get; set; }
        public string Inspector { get; set; }
        public DateTime? InspectionDate { get; set; }
    }

    public class ChecklistItem
    {
        public string Category { get; set; }
        public string Question { get; set; }
        public ChecklistStatus Status { get; set; }
        public string Comments { get; set; }
    }

    public class InspectionFinding
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public FindingSeverity Severity { get; set; }
        public string Location { get; set; }
        public string Photo { get; set; }
        public DateTime FoundDate { get; set; }
        public string CorrectedBy { get; set; }
        public DateTime CorrectedDate { get; set; }
    }

    public class ToolboxTalk
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public ToolboxTopic Topic { get; set; }
        public string Content { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Presenter { get; set; }
        public ToolboxTalkStatus Status { get; set; }
        public DateTime ConductedDate { get; set; }
        public string ConductedBy { get; set; }
        public List<ToolboxAttendee> Attendees { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> RelatedHazards { get; set; }
    }

    public class ToolboxTalkInput
    {
        public string ProjectId { get; set; }
        public ToolboxTopic Topic { get; set; }
        public string Content { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Presenter { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> RelatedHazards { get; set; }
    }

    public class ToolboxAttendee
    {
        public string Name { get; set; }
        public bool Signed { get; set; }
        public DateTime SignedDate { get; set; }
    }

    public class SafetyMetrics
    {
        public string ProjectId { get; set; }
        public DateTime CalculatedDate { get; set; }
        public int TotalManhours { get; set; }
        public SafetyStatistics Statistics { get; set; }
        public double TRIR { get; set; }
        public double DART { get; set; }
        public double LostTimeRate { get; set; }
        public int DaysSinceLastIncident { get; set; }
        public List<MonthlyTrend> MonthlyTrend { get; set; }
        public IndustryBenchmark IndustryBenchmark { get; set; }
    }

    public class MonthlyTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int IncidentCount { get; set; }
        public int InjuryCount { get; set; }
        public int NearMissCount { get; set; }
    }

    public class IndustryBenchmark
    {
        public double IndustryTRIR { get; set; }
        public double IndustryDART { get; set; }
        public string PerformanceRating { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectStatus { Active, OnHold, Completed }
    public enum JHAStatus { Draft, UnderReview, Approved, Expired }
    public enum TaskType { RoofWork, Scaffolding, SteelErection, LadderWork, ElectricalWork, Demolition, Excavation, Trenching, ConfinedSpace, CraneOperations, WeldingCutting, Concrete, General }
    public enum HazardCategory { Fall, StruckBy, CaughtBetween, Electrical, Chemical, Biological, Ergonomic, Environmental }
    public enum SeverityLevel { Minor = 1, Moderate = 2, Serious = 3, Severe = 4, Catastrophic = 5 }
    public enum LikelihoodLevel { Rare = 1, Unlikely = 2, Possible = 3, Likely = 4, AlmostCertain = 5 }
    public enum ControlType { Elimination, Substitution, Engineering, Administrative, PPE }
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum IncidentType { Injury, NearMiss, PropertyDamage, Environmental, Fire, Theft }
    public enum IncidentSeverity { Minor, Moderate, Serious, Severe, Fatal }
    public enum IncidentStatus { Reported, UnderInvestigation, Investigated, Closed }
    public enum ActionStatus { Open, InProgress, Completed, Cancelled }
    public enum InspectionType { Daily, Weekly, Monthly, OSHA, PreTask }
    public enum InspectionStatus { Scheduled, InProgress, Completed, Failed }
    public enum ChecklistStatus { NotChecked, Compliant, NonCompliant, NA }
    public enum FindingSeverity { Low, Medium, High, Critical }
    public enum ToolboxTopic { FallProtection, LadderSafety, PPE, Excavation, Electrical, HeatIllness, Housekeeping, FirePrevention, Other }
    public enum ToolboxTalkStatus { Scheduled, Completed, Cancelled }
    public enum SafetyAlertType { NewIncident, HighRiskJHA, LowInspectionScore, OverdueAction }

    #endregion

    #region Event Args

    public class SafetyAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public SafetyAlertType AlertType { get; set; }
        public string Message { get; set; }
    }

    public class IncidentReportedEventArgs : EventArgs
    {
        public string IncidentId { get; set; }
        public IncidentType Type { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; }
    }

    #endregion
}
