// ===================================================================
// StingBIM Quality Intelligence Engine
// QA/QC automation, inspection planning, and defect management
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.QualityIntelligence
{
    /// <summary>
    /// Comprehensive quality intelligence for construction QA/QC,
    /// inspection management, defect tracking, and commissioning support
    /// </summary>
    public sealed class QualityIntelligenceEngine
    {
        private static readonly Lazy<QualityIntelligenceEngine> _instance =
            new Lazy<QualityIntelligenceEngine>(() => new QualityIntelligenceEngine());
        public static QualityIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, QualityProject> _projects;
        private readonly ConcurrentDictionary<string, QualityPlan> _qualityPlans;
        private readonly ConcurrentDictionary<string, InspectionTemplate> _inspectionTemplates;
        private readonly ConcurrentDictionary<string, Defect> _defects;
        private readonly object _lockObject = new object();

        public event EventHandler<QualityAlertEventArgs> QualityAlertRaised;
        public event EventHandler<DefectEventArgs> CriticalDefectFound;

        private QualityIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, QualityProject>();
            _qualityPlans = new ConcurrentDictionary<string, QualityPlan>();
            _inspectionTemplates = new ConcurrentDictionary<string, InspectionTemplate>();
            _defects = new ConcurrentDictionary<string, Defect>();

            InitializeInspectionTemplates();
            InitializeQualityStandards();
        }

        #region Initialization

        private readonly Dictionary<string, QualityStandard> _qualityStandards = new Dictionary<string, QualityStandard>();

        private void InitializeInspectionTemplates()
        {
            var templates = new List<InspectionTemplate>
            {
                new InspectionTemplate
                {
                    Id = "TMPL-CONCRETE",
                    Name = "Concrete Placement Inspection",
                    Category = "Structural",
                    Phase = ConstructionPhase.Structure,
                    ChecklistItems = new List<ChecklistItem>
                    {
                        new ChecklistItem { Id = "C01", Description = "Verify formwork alignment and bracing", Category = "Pre-Pour", Critical = true },
                        new ChecklistItem { Id = "C02", Description = "Check reinforcement placement per drawings", Category = "Pre-Pour", Critical = true },
                        new ChecklistItem { Id = "C03", Description = "Verify rebar cover and spacing", Category = "Pre-Pour", Critical = true },
                        new ChecklistItem { Id = "C04", Description = "Inspect embedded items placement", Category = "Pre-Pour", Critical = false },
                        new ChecklistItem { Id = "C05", Description = "Verify concrete mix design/batch ticket", Category = "During Pour", Critical = true },
                        new ChecklistItem { Id = "C06", Description = "Take slump test samples", Category = "During Pour", Critical = true },
                        new ChecklistItem { Id = "C07", Description = "Take cylinder samples for testing", Category = "During Pour", Critical = true },
                        new ChecklistItem { Id = "C08", Description = "Monitor concrete temperature", Category = "During Pour", Critical = false },
                        new ChecklistItem { Id = "C09", Description = "Verify proper vibration/consolidation", Category = "During Pour", Critical = true },
                        new ChecklistItem { Id = "C10", Description = "Check finish requirements", Category = "Post-Pour", Critical = false },
                        new ChecklistItem { Id = "C11", Description = "Verify curing procedures initiated", Category = "Post-Pour", Critical = true }
                    }
                },
                new InspectionTemplate
                {
                    Id = "TMPL-STEEL",
                    Name = "Structural Steel Inspection",
                    Category = "Structural",
                    Phase = ConstructionPhase.Structure,
                    ChecklistItems = new List<ChecklistItem>
                    {
                        new ChecklistItem { Id = "S01", Description = "Verify mill certificates and material traceability", Category = "Material", Critical = true },
                        new ChecklistItem { Id = "S02", Description = "Check member sizes and grades", Category = "Material", Critical = true },
                        new ChecklistItem { Id = "S03", Description = "Inspect welds per AWS D1.1", Category = "Fabrication", Critical = true },
                        new ChecklistItem { Id = "S04", Description = "Verify bolt hole alignment", Category = "Fabrication", Critical = true },
                        new ChecklistItem { Id = "S05", Description = "Check shop primer application", Category = "Fabrication", Critical = false },
                        new ChecklistItem { Id = "S06", Description = "Verify erection sequence per plan", Category = "Erection", Critical = true },
                        new ChecklistItem { Id = "S07", Description = "Check column plumbness (1/500)", Category = "Erection", Critical = true },
                        new ChecklistItem { Id = "S08", Description = "Inspect bolted connections torque", Category = "Connections", Critical = true },
                        new ChecklistItem { Id = "S09", Description = "Verify field weld procedures", Category = "Connections", Critical = true },
                        new ChecklistItem { Id = "S10", Description = "Check fireproofing application", Category = "Fireproofing", Critical = true }
                    }
                },
                new InspectionTemplate
                {
                    Id = "TMPL-MEP-ROUGH",
                    Name = "MEP Rough-In Inspection",
                    Category = "MEP",
                    Phase = ConstructionPhase.MEPRoughIn,
                    ChecklistItems = new List<ChecklistItem>
                    {
                        new ChecklistItem { Id = "M01", Description = "Verify ductwork sizes per drawings", Category = "HVAC", Critical = true },
                        new ChecklistItem { Id = "M02", Description = "Check duct sealing and insulation", Category = "HVAC", Critical = true },
                        new ChecklistItem { Id = "M03", Description = "Inspect piping supports and hangers", Category = "Plumbing", Critical = true },
                        new ChecklistItem { Id = "M04", Description = "Verify pipe slopes for drainage", Category = "Plumbing", Critical = true },
                        new ChecklistItem { Id = "M05", Description = "Check pressure test results", Category = "Plumbing", Critical = true },
                        new ChecklistItem { Id = "M06", Description = "Inspect conduit routing and fill", Category = "Electrical", Critical = true },
                        new ChecklistItem { Id = "M07", Description = "Verify wire pulling and terminations", Category = "Electrical", Critical = true },
                        new ChecklistItem { Id = "M08", Description = "Check grounding and bonding", Category = "Electrical", Critical = true },
                        new ChecklistItem { Id = "M09", Description = "Inspect fire damper installations", Category = "Fire Protection", Critical = true },
                        new ChecklistItem { Id = "M10", Description = "Verify sprinkler head locations", Category = "Fire Protection", Critical = true }
                    }
                },
                new InspectionTemplate
                {
                    Id = "TMPL-ENVELOPE",
                    Name = "Building Envelope Inspection",
                    Category = "Envelope",
                    Phase = ConstructionPhase.Envelope,
                    ChecklistItems = new List<ChecklistItem>
                    {
                        new ChecklistItem { Id = "E01", Description = "Verify air/vapor barrier continuity", Category = "Waterproofing", Critical = true },
                        new ChecklistItem { Id = "E02", Description = "Check window/curtain wall anchors", Category = "Glazing", Critical = true },
                        new ChecklistItem { Id = "E03", Description = "Inspect sealant joints and backer rod", Category = "Glazing", Critical = true },
                        new ChecklistItem { Id = "E04", Description = "Verify insulation installation", Category = "Insulation", Critical = true },
                        new ChecklistItem { Id = "E05", Description = "Check flashing installation at transitions", Category = "Flashing", Critical = true },
                        new ChecklistItem { Id = "E06", Description = "Inspect roofing membrane installation", Category = "Roofing", Critical = true },
                        new ChecklistItem { Id = "E07", Description = "Verify drainage provisions", Category = "Roofing", Critical = true },
                        new ChecklistItem { Id = "E08", Description = "Water test completed satisfactorily", Category = "Testing", Critical = true }
                    }
                },
                new InspectionTemplate
                {
                    Id = "TMPL-FINISHES",
                    Name = "Interior Finishes Inspection",
                    Category = "Finishes",
                    Phase = ConstructionPhase.Finishes,
                    ChecklistItems = new List<ChecklistItem>
                    {
                        new ChecklistItem { Id = "F01", Description = "Check drywall taping and finishing", Category = "Drywall", Critical = false },
                        new ChecklistItem { Id = "F02", Description = "Verify ceiling grid alignment", Category = "Ceilings", Critical = false },
                        new ChecklistItem { Id = "F03", Description = "Inspect flooring subfloor preparation", Category = "Flooring", Critical = true },
                        new ChecklistItem { Id = "F04", Description = "Check floor levelness tolerances", Category = "Flooring", Critical = true },
                        new ChecklistItem { Id = "F05", Description = "Verify paint colors and finishes", Category = "Painting", Critical = false },
                        new ChecklistItem { Id = "F06", Description = "Inspect millwork installation", Category = "Millwork", Critical = false },
                        new ChecklistItem { Id = "F07", Description = "Check tile alignment and grout", Category = "Tile", Critical = false },
                        new ChecklistItem { Id = "F08", Description = "Verify door hardware operation", Category = "Hardware", Critical = true }
                    }
                }
            };

            foreach (var template in templates)
            {
                _inspectionTemplates.TryAdd(template.Id, template);
            }
        }

        private void InitializeQualityStandards()
        {
            _qualityStandards["ACI-301"] = new QualityStandard { Code = "ACI 301", Name = "Specifications for Structural Concrete", Category = "Concrete" };
            _qualityStandards["ACI-318"] = new QualityStandard { Code = "ACI 318", Name = "Building Code Requirements for Structural Concrete", Category = "Concrete" };
            _qualityStandards["AWS-D1.1"] = new QualityStandard { Code = "AWS D1.1", Name = "Structural Welding Code - Steel", Category = "Steel" };
            _qualityStandards["AISC-303"] = new QualityStandard { Code = "AISC 303", Name = "Code of Standard Practice for Steel Buildings", Category = "Steel" };
            _qualityStandards["ASTM-E1186"] = new QualityStandard { Code = "ASTM E1186", Name = "Air Leakage Site Detection", Category = "Envelope" };
            _qualityStandards["AAMA-501"] = new QualityStandard { Code = "AAMA 501", Name = "Methods of Test for Exterior Walls", Category = "Envelope" };
            _qualityStandards["ASHRAE-90.1"] = new QualityStandard { Code = "ASHRAE 90.1", Name = "Energy Standard for Buildings", Category = "MEP" };
            _qualityStandards["NFPA-13"] = new QualityStandard { Code = "NFPA 13", Name = "Standard for Installation of Sprinkler Systems", Category = "Fire Protection" };
        }

        #endregion

        #region Quality Plan Management

        public QualityProject CreateProject(QualityProjectRequest request)
        {
            var project = new QualityProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                ProjectType = request.ProjectType,
                CreatedDate = DateTime.UtcNow,
                QualityPlans = new List<string>(),
                Inspections = new List<string>(),
                Defects = new List<string>()
            };

            _projects.TryAdd(project.Id, project);
            return project;
        }

        public QualityPlan CreateQualityPlan(QualityPlanRequest request)
        {
            var plan = new QualityPlan
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                Version = "1.0",
                CreatedDate = DateTime.UtcNow,
                Status = PlanStatus.Draft,
                QualityObjectives = new List<QualityObjective>(),
                InspectionPoints = new List<InspectionPoint>(),
                TestingRequirements = new List<TestingRequirement>(),
                Responsibilities = new List<QualityResponsibility>()
            };

            // Generate inspection points based on project type
            GenerateInspectionPoints(plan, request.ProjectType);

            // Generate testing requirements
            GenerateTestingRequirements(plan, request.ProjectType);

            // Add quality objectives
            plan.QualityObjectives.AddRange(new List<QualityObjective>
            {
                new QualityObjective { Objective = "Zero critical defects at substantial completion", Metric = "Critical defects", Target = "0", Priority = 1 },
                new QualityObjective { Objective = "First-time inspection pass rate > 90%", Metric = "Pass rate", Target = "90%", Priority = 2 },
                new QualityObjective { Objective = "Complete commissioning before occupancy", Metric = "Commissioning completion", Target = "100%", Priority = 3 },
                new QualityObjective { Objective = "Defect resolution within 5 business days", Metric = "Resolution time", Target = "5 days", Priority = 4 }
            });

            _qualityPlans.TryAdd(plan.Id, plan);

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.QualityPlans.Add(plan.Id);
            }

            return plan;
        }

        private void GenerateInspectionPoints(QualityPlan plan, string projectType)
        {
            var phases = new List<(ConstructionPhase Phase, string Description, int TypicalDay)>
            {
                (ConstructionPhase.Excavation, "Foundation excavation and subgrade preparation", 30),
                (ConstructionPhase.Foundation, "Foundation concrete and waterproofing", 60),
                (ConstructionPhase.Structure, "Structural frame - concrete/steel", 120),
                (ConstructionPhase.Envelope, "Building envelope - roofing, glazing, cladding", 180),
                (ConstructionPhase.MEPRoughIn, "MEP rough-in before close-up", 210),
                (ConstructionPhase.Finishes, "Interior finishes inspection", 270),
                (ConstructionPhase.MEPFinal, "MEP final connections and testing", 300),
                (ConstructionPhase.Commissioning, "Systems commissioning and testing", 330),
                (ConstructionPhase.Punchlist, "Final punchlist and closeout", 350)
            };

            int sequence = 1;
            foreach (var (phase, description, day) in phases)
            {
                var point = new InspectionPoint
                {
                    Id = Guid.NewGuid().ToString(),
                    Sequence = sequence++,
                    Phase = phase,
                    Description = description,
                    TemplateId = GetTemplateForPhase(phase),
                    RequiredApprovals = GetRequiredApprovals(phase),
                    HoldPoint = IsHoldPoint(phase),
                    PlannedDate = DateTime.UtcNow.AddDays(day),
                    Status = InspectionStatus.Pending
                };

                plan.InspectionPoints.Add(point);
            }
        }

        private string GetTemplateForPhase(ConstructionPhase phase)
        {
            return phase switch
            {
                ConstructionPhase.Structure => "TMPL-CONCRETE",
                ConstructionPhase.MEPRoughIn => "TMPL-MEP-ROUGH",
                ConstructionPhase.Envelope => "TMPL-ENVELOPE",
                ConstructionPhase.Finishes => "TMPL-FINISHES",
                _ => null
            };
        }

        private List<string> GetRequiredApprovals(ConstructionPhase phase)
        {
            var baseApprovals = new List<string> { "Quality Manager", "Superintendent" };

            switch (phase)
            {
                case ConstructionPhase.Foundation:
                case ConstructionPhase.Structure:
                    baseApprovals.Add("Structural Engineer");
                    break;
                case ConstructionPhase.MEPRoughIn:
                case ConstructionPhase.MEPFinal:
                    baseApprovals.Add("MEP Engineer");
                    break;
                case ConstructionPhase.Envelope:
                    baseApprovals.Add("Envelope Consultant");
                    break;
                case ConstructionPhase.Commissioning:
                    baseApprovals.Add("Commissioning Agent");
                    break;
            }

            return baseApprovals;
        }

        private bool IsHoldPoint(ConstructionPhase phase)
        {
            return phase switch
            {
                ConstructionPhase.Foundation => true,
                ConstructionPhase.Structure => true,
                ConstructionPhase.MEPRoughIn => true,
                ConstructionPhase.Commissioning => true,
                _ => false
            };
        }

        private void GenerateTestingRequirements(QualityPlan plan, string projectType)
        {
            plan.TestingRequirements.AddRange(new List<TestingRequirement>
            {
                new TestingRequirement { Test = "Concrete Cylinder Testing", Standard = "ASTM C39", Frequency = "Per ACI 301 - min 1 per 50 CY", ResponsibleParty = "Testing Lab", Phase = ConstructionPhase.Structure },
                new TestingRequirement { Test = "Weld Inspection (Visual/UT)", Standard = "AWS D1.1", Frequency = "Per AISC QC requirements", ResponsibleParty = "Special Inspector", Phase = ConstructionPhase.Structure },
                new TestingRequirement { Test = "High-Strength Bolt Testing", Standard = "RCSC Specification", Frequency = "Daily verification + random", ResponsibleParty = "Special Inspector", Phase = ConstructionPhase.Structure },
                new TestingRequirement { Test = "Fireproofing Thickness", Standard = "UL Listed Assembly", Frequency = "25% of beams, 10% of columns", ResponsibleParty = "Special Inspector", Phase = ConstructionPhase.Structure },
                new TestingRequirement { Test = "Air Barrier Testing", Standard = "ASTM E2357", Frequency = "Per specification sections", ResponsibleParty = "Testing Consultant", Phase = ConstructionPhase.Envelope },
                new TestingRequirement { Test = "Water Infiltration Testing", Standard = "AAMA 501.2", Frequency = "Representative areas + problem areas", ResponsibleParty = "Testing Consultant", Phase = ConstructionPhase.Envelope },
                new TestingRequirement { Test = "Duct Leakage Testing", Standard = "SMACNA", Frequency = "Per duct class specification", ResponsibleParty = "TAB Contractor", Phase = ConstructionPhase.MEPRoughIn },
                new TestingRequirement { Test = "Pipe Pressure Testing", Standard = "Plumbing Code", Frequency = "All systems before concealment", ResponsibleParty = "Plumbing Contractor", Phase = ConstructionPhase.MEPRoughIn },
                new TestingRequirement { Test = "Electrical Testing (Meg/Hi-Pot)", Standard = "NETA ATS", Frequency = "Per NETA tables", ResponsibleParty = "Electrical Contractor", Phase = ConstructionPhase.MEPFinal },
                new TestingRequirement { Test = "Fire Alarm Testing", Standard = "NFPA 72", Frequency = "100% of devices", ResponsibleParty = "Fire Alarm Contractor", Phase = ConstructionPhase.MEPFinal },
                new TestingRequirement { Test = "TAB (Test, Adjust, Balance)", Standard = "AABC/NEBB", Frequency = "All air/water systems", ResponsibleParty = "TAB Contractor", Phase = ConstructionPhase.Commissioning },
                new TestingRequirement { Test = "Functional Performance Testing", Standard = "ASHRAE Guideline 0", Frequency = "All major equipment", ResponsibleParty = "Commissioning Agent", Phase = ConstructionPhase.Commissioning }
            });
        }

        #endregion

        #region Inspection Management

        public Inspection CreateInspection(InspectionRequest request)
        {
            var inspection = new Inspection
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                InspectionPointId = request.InspectionPointId,
                Location = request.Location,
                ScheduledDate = request.ScheduledDate,
                Inspector = request.Inspector,
                Status = InspectionStatus.Scheduled,
                CreatedDate = DateTime.UtcNow,
                ChecklistResults = new List<ChecklistResult>(),
                Attachments = new List<string>(),
                Defects = new List<string>()
            };

            // Load template checklist if available
            if (!string.IsNullOrEmpty(request.TemplateId) && _inspectionTemplates.TryGetValue(request.TemplateId, out var template))
            {
                foreach (var item in template.ChecklistItems)
                {
                    inspection.ChecklistResults.Add(new ChecklistResult
                    {
                        ItemId = item.Id,
                        Description = item.Description,
                        Category = item.Category,
                        Critical = item.Critical,
                        Result = CheckResult.Pending
                    });
                }
            }

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.Inspections.Add(inspection.Id);
            }

            return inspection;
        }

        public Inspection PerformInspection(string inspectionId, InspectionPerformRequest request)
        {
            // Find inspection in projects
            Inspection inspection = null;
            foreach (var project in _projects.Values)
            {
                if (project.Inspections.Contains(inspectionId))
                {
                    // In real implementation, would retrieve from storage
                    break;
                }
            }

            if (inspection == null)
            {
                inspection = new Inspection { Id = inspectionId };
            }

            inspection.ActualDate = DateTime.UtcNow;
            inspection.Inspector = request.Inspector;
            inspection.Status = InspectionStatus.InProgress;

            // Update checklist results
            foreach (var result in request.Results)
            {
                var existing = inspection.ChecklistResults.FirstOrDefault(c => c.ItemId == result.ItemId);
                if (existing != null)
                {
                    existing.Result = result.Result;
                    existing.Notes = result.Notes;
                    existing.PhotoRequired = result.PhotoRequired;
                    existing.PhotoTaken = result.PhotoTaken;
                }
            }

            return inspection;
        }

        public Inspection CompleteInspection(string inspectionId, InspectionCompleteRequest request)
        {
            var inspection = new Inspection
            {
                Id = inspectionId,
                ActualDate = DateTime.UtcNow,
                CompletedDate = DateTime.UtcNow,
                Inspector = request.Inspector,
                OverallComments = request.Comments,
                Status = DetermineInspectionStatus(request)
            };

            // Create defects for failed items
            foreach (var failedItem in request.Results.Where(r => r.Result == CheckResult.Fail))
            {
                var defect = CreateDefectFromInspection(inspection, failedItem, request.ProjectId);
                inspection.Defects.Add(defect.Id);
            }

            // Raise alert for failed critical items
            var criticalFailures = request.Results.Count(r => r.Result == CheckResult.Fail && r.Critical);
            if (criticalFailures > 0)
            {
                QualityAlertRaised?.Invoke(this, new QualityAlertEventArgs
                {
                    ProjectId = request.ProjectId,
                    AlertType = "Critical Inspection Failure",
                    Message = $"{criticalFailures} critical checklist items failed inspection",
                    Severity = AlertSeverity.High
                });
            }

            return inspection;
        }

        private InspectionStatus DetermineInspectionStatus(InspectionCompleteRequest request)
        {
            var results = request.Results;

            if (results.All(r => r.Result == CheckResult.Pass || r.Result == CheckResult.NA))
                return InspectionStatus.Passed;

            if (results.Any(r => r.Result == CheckResult.Fail && r.Critical))
                return InspectionStatus.Failed;

            if (results.Any(r => r.Result == CheckResult.Fail))
                return InspectionStatus.ConditionalPass;

            return InspectionStatus.Passed;
        }

        private Defect CreateDefectFromInspection(Inspection inspection, ChecklistResultInput failedItem, string projectId)
        {
            var defect = new Defect
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                InspectionId = inspection.Id,
                Title = failedItem.Description,
                Description = failedItem.Notes ?? "Failed inspection item - requires correction",
                Location = inspection.Location,
                Severity = failedItem.Critical ? DefectSeverity.Critical : DefectSeverity.Major,
                Status = DefectStatus.Open,
                IdentifiedDate = DateTime.UtcNow,
                IdentifiedBy = inspection.Inspector,
                DueDate = DateTime.UtcNow.AddDays(failedItem.Critical ? 3 : 7),
                History = new List<DefectHistoryEntry>()
            };

            defect.History.Add(new DefectHistoryEntry
            {
                Date = DateTime.UtcNow,
                Action = "Defect Created",
                User = inspection.Inspector,
                Notes = "Created from inspection finding"
            });

            _defects.TryAdd(defect.Id, defect);

            if (_projects.TryGetValue(projectId, out var project))
            {
                project.Defects.Add(defect.Id);
            }

            if (defect.Severity == DefectSeverity.Critical)
            {
                CriticalDefectFound?.Invoke(this, new DefectEventArgs { Defect = defect });
            }

            return defect;
        }

        #endregion

        #region Defect Management

        public Defect CreateDefect(DefectRequest request)
        {
            var defect = new Defect
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Title = request.Title,
                Description = request.Description,
                Location = request.Location,
                Category = request.Category,
                Severity = request.Severity,
                Trade = request.Trade,
                Status = DefectStatus.Open,
                IdentifiedDate = DateTime.UtcNow,
                IdentifiedBy = request.IdentifiedBy,
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(GetDefaultDueDays(request.Severity)),
                Photos = request.Photos ?? new List<string>(),
                History = new List<DefectHistoryEntry>()
            };

            defect.History.Add(new DefectHistoryEntry
            {
                Date = DateTime.UtcNow,
                Action = "Defect Created",
                User = request.IdentifiedBy,
                Notes = request.Description
            });

            _defects.TryAdd(defect.Id, defect);

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.Defects.Add(defect.Id);
            }

            if (defect.Severity == DefectSeverity.Critical)
            {
                CriticalDefectFound?.Invoke(this, new DefectEventArgs { Defect = defect });
            }

            return defect;
        }

        private int GetDefaultDueDays(DefectSeverity severity)
        {
            return severity switch
            {
                DefectSeverity.Critical => 1,
                DefectSeverity.Major => 3,
                DefectSeverity.Minor => 7,
                DefectSeverity.Cosmetic => 14,
                _ => 7
            };
        }

        public Defect UpdateDefectStatus(string defectId, DefectStatusUpdate update)
        {
            if (!_defects.TryGetValue(defectId, out var defect))
                return null;

            var oldStatus = defect.Status;
            defect.Status = update.NewStatus;

            if (update.NewStatus == DefectStatus.InProgress && defect.StartedDate == null)
            {
                defect.StartedDate = DateTime.UtcNow;
            }

            if (update.NewStatus == DefectStatus.ReadyForInspection)
            {
                defect.CompletedDate = DateTime.UtcNow;
            }

            if (update.NewStatus == DefectStatus.Closed)
            {
                defect.ClosedDate = DateTime.UtcNow;
                defect.ClosedBy = update.UpdatedBy;
            }

            defect.History.Add(new DefectHistoryEntry
            {
                Date = DateTime.UtcNow,
                Action = $"Status changed from {oldStatus} to {update.NewStatus}",
                User = update.UpdatedBy,
                Notes = update.Notes
            });

            return defect;
        }

        public DefectAnalytics AnalyzeDefects(string projectId)
        {
            var defects = _defects.Values.Where(d => d.ProjectId == projectId).ToList();

            var analytics = new DefectAnalytics
            {
                ProjectId = projectId,
                AnalysisDate = DateTime.UtcNow,
                TotalDefects = defects.Count,
                OpenDefects = defects.Count(d => d.Status == DefectStatus.Open || d.Status == DefectStatus.InProgress),
                ClosedDefects = defects.Count(d => d.Status == DefectStatus.Closed),
                OverdueDefects = defects.Count(d => d.DueDate < DateTime.UtcNow && d.Status != DefectStatus.Closed),
                CriticalDefects = defects.Count(d => d.Severity == DefectSeverity.Critical),
                DefectsByCategory = defects.GroupBy(d => d.Category ?? "Unclassified")
                    .ToDictionary(g => g.Key, g => g.Count()),
                DefectsByTrade = defects.GroupBy(d => d.Trade ?? "Unassigned")
                    .ToDictionary(g => g.Key, g => g.Count()),
                DefectsBySeverity = defects.GroupBy(d => d.Severity)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                TrendData = new List<DefectTrendPoint>()
            };

            // Calculate average resolution time
            var closedDefects = defects.Where(d => d.ClosedDate.HasValue && d.IdentifiedDate != default).ToList();
            if (closedDefects.Any())
            {
                analytics.AverageResolutionDays = (decimal)closedDefects
                    .Average(d => (d.ClosedDate.Value - d.IdentifiedDate).TotalDays);
            }

            // Generate trend data (last 12 weeks)
            for (int week = 11; week >= 0; week--)
            {
                var weekStart = DateTime.UtcNow.AddDays(-7 * (week + 1));
                var weekEnd = DateTime.UtcNow.AddDays(-7 * week);

                var created = defects.Count(d => d.IdentifiedDate >= weekStart && d.IdentifiedDate < weekEnd);
                var closed = defects.Count(d => d.ClosedDate >= weekStart && d.ClosedDate < weekEnd);

                analytics.TrendData.Add(new DefectTrendPoint
                {
                    WeekEnding = weekEnd,
                    Created = created,
                    Closed = closed,
                    NetChange = created - closed
                });
            }

            // Identify top problem areas
            analytics.TopProblemAreas = defects
                .GroupBy(d => d.Location)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new ProblemArea { Location = g.Key, DefectCount = g.Count() })
                .ToList();

            return analytics;
        }

        #endregion

        #region Punchlist Management

        public Punchlist CreatePunchlist(PunchlistRequest request)
        {
            var punchlist = new Punchlist
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                Area = request.Area,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                Status = PunchlistStatus.Open,
                Items = new List<PunchlistItem>()
            };

            return punchlist;
        }

        public PunchlistItem AddPunchlistItem(string punchlistId, PunchlistItemRequest request)
        {
            var item = new PunchlistItem
            {
                Id = Guid.NewGuid().ToString(),
                Number = request.Number,
                Description = request.Description,
                Location = request.Location,
                Trade = request.Trade,
                Priority = request.Priority,
                Status = PunchlistItemStatus.Open,
                CreatedDate = DateTime.UtcNow,
                DueDate = request.DueDate,
                Photos = request.Photos ?? new List<string>()
            };

            return item;
        }

        public PunchlistSummary GetPunchlistSummary(string projectId)
        {
            // In real implementation, would aggregate from punchlists
            return new PunchlistSummary
            {
                ProjectId = projectId,
                GeneratedDate = DateTime.UtcNow,
                TotalItems = 0,
                CompletedItems = 0,
                OpenItems = 0,
                OverdueItems = 0,
                ItemsByTrade = new Dictionary<string, int>(),
                ItemsByPriority = new Dictionary<string, int>()
            };
        }

        #endregion

        #region Commissioning Support

        public CommissioningPlan CreateCommissioningPlan(CommissioningPlanRequest request)
        {
            var plan = new CommissioningPlan
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                CreatedDate = DateTime.UtcNow,
                Status = CommissioningStatus.Planning,
                Systems = new List<CommissioningSystem>(),
                Schedule = new List<CommissioningMilestone>()
            };

            // Add standard systems for commissioning
            var systems = new List<(string Name, string Category, int Priority)>
            {
                ("Chilled Water System", "HVAC", 1),
                ("Hot Water System", "HVAC", 1),
                ("Air Handling Units", "HVAC", 2),
                ("Variable Air Volume System", "HVAC", 2),
                ("Building Automation System", "Controls", 1),
                ("Fire Alarm System", "Life Safety", 1),
                ("Fire Suppression System", "Life Safety", 1),
                ("Emergency Generator", "Electrical", 1),
                ("Normal Power Distribution", "Electrical", 2),
                ("Lighting Controls", "Electrical", 3),
                ("Domestic Water System", "Plumbing", 2),
                ("Sanitary System", "Plumbing", 3),
                ("Elevator Systems", "Conveying", 2)
            };

            foreach (var (name, category, priority) in systems)
            {
                plan.Systems.Add(new CommissioningSystem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Category = category,
                    Priority = priority,
                    Status = SystemStatus.NotStarted,
                    PreFunctionalComplete = false,
                    FunctionalTestComplete = false,
                    Tests = GenerateSystemTests(name)
                });
            }

            // Add schedule milestones
            plan.Schedule.AddRange(new List<CommissioningMilestone>
            {
                new CommissioningMilestone { Name = "Commissioning Plan Approval", PlannedDate = DateTime.UtcNow.AddDays(30), Required = true },
                new CommissioningMilestone { Name = "Pre-Functional Checklists Complete", PlannedDate = DateTime.UtcNow.AddDays(120), Required = true },
                new CommissioningMilestone { Name = "Functional Performance Testing Start", PlannedDate = DateTime.UtcNow.AddDays(150), Required = true },
                new CommissioningMilestone { Name = "Integrated Systems Testing", PlannedDate = DateTime.UtcNow.AddDays(180), Required = true },
                new CommissioningMilestone { Name = "Seasonal Testing (if required)", PlannedDate = DateTime.UtcNow.AddDays(270), Required = false },
                new CommissioningMilestone { Name = "Commissioning Complete", PlannedDate = DateTime.UtcNow.AddDays(210), Required = true }
            });

            return plan;
        }

        private List<FunctionalTest> GenerateSystemTests(string systemName)
        {
            var tests = new List<FunctionalTest>();

            if (systemName.Contains("Air Handling") || systemName.Contains("VAV"))
            {
                tests.Add(new FunctionalTest { Name = "Fan Operation - All Speeds", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Damper Operation - Full Travel", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Temperature Control - Heating/Cooling", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Economizer Operation", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Safety Interlocks", Status = TestStatus.NotStarted });
            }
            else if (systemName.Contains("Chilled") || systemName.Contains("Hot Water"))
            {
                tests.Add(new FunctionalTest { Name = "Pump Operation - Lead/Lag", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Temperature Setpoint Control", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Pressure Control", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Equipment Staging", Status = TestStatus.NotStarted });
            }
            else if (systemName.Contains("Building Automation"))
            {
                tests.Add(new FunctionalTest { Name = "Point-to-Point Verification", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Trend Data Logging", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Alarm Generation and Routing", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Schedule Programming", Status = TestStatus.NotStarted });
            }
            else if (systemName.Contains("Fire"))
            {
                tests.Add(new FunctionalTest { Name = "Device Testing - 100%", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Notification Appliance Coverage", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Supervisory Signals", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Fire Department Connection", Status = TestStatus.NotStarted });
            }
            else if (systemName.Contains("Generator"))
            {
                tests.Add(new FunctionalTest { Name = "Start/Transfer Time", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Load Bank Testing", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Automatic Transfer Switch Operation", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Fuel System", Status = TestStatus.NotStarted });
            }
            else
            {
                tests.Add(new FunctionalTest { Name = "System Startup Verification", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Operational Testing", Status = TestStatus.NotStarted });
                tests.Add(new FunctionalTest { Name = "Safety Verification", Status = TestStatus.NotStarted });
            }

            return tests;
        }

        #endregion

        #region Quality Metrics

        public QualityDashboard GetQualityDashboard(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var defects = _defects.Values.Where(d => d.ProjectId == projectId).ToList();

            return new QualityDashboard
            {
                ProjectId = projectId,
                GeneratedDate = DateTime.UtcNow,
                TotalInspections = project.Inspections.Count,
                InspectionPassRate = 85, // Would calculate from actual data
                TotalDefects = defects.Count,
                OpenDefects = defects.Count(d => d.Status == DefectStatus.Open || d.Status == DefectStatus.InProgress),
                CriticalDefectsOpen = defects.Count(d => d.Severity == DefectSeverity.Critical && d.Status != DefectStatus.Closed),
                OverdueDefects = defects.Count(d => d.DueDate < DateTime.UtcNow && d.Status != DefectStatus.Closed),
                AverageResolutionTime = 4.5m, // Would calculate from actual data
                QualityScore = CalculateQualityScore(defects),
                RecentActivity = new List<QualityActivity>(),
                UpcomingInspections = new List<UpcomingInspection>()
            };
        }

        private decimal CalculateQualityScore(List<Defect> defects)
        {
            if (!defects.Any()) return 100;

            // Score based on defect severity and resolution
            var baseScore = 100m;

            // Deduct for open defects
            baseScore -= defects.Count(d => d.Status != DefectStatus.Closed && d.Severity == DefectSeverity.Critical) * 10;
            baseScore -= defects.Count(d => d.Status != DefectStatus.Closed && d.Severity == DefectSeverity.Major) * 5;
            baseScore -= defects.Count(d => d.Status != DefectStatus.Closed && d.Severity == DefectSeverity.Minor) * 2;

            // Deduct for overdue
            baseScore -= defects.Count(d => d.DueDate < DateTime.UtcNow && d.Status != DefectStatus.Closed) * 3;

            return Math.Max(baseScore, 0);
        }

        #endregion

        #region Helper Methods

        public List<InspectionTemplate> GetInspectionTemplates()
        {
            return _inspectionTemplates.Values.ToList();
        }

        public InspectionTemplate GetTemplate(string templateId)
        {
            _inspectionTemplates.TryGetValue(templateId, out var template);
            return template;
        }

        public Defect GetDefect(string defectId)
        {
            _defects.TryGetValue(defectId, out var defect);
            return defect;
        }

        public List<Defect> GetProjectDefects(string projectId)
        {
            return _defects.Values.Where(d => d.ProjectId == projectId).ToList();
        }

        #endregion
    }

    #region Data Models

    public class QualityProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> QualityPlans { get; set; }
        public List<string> Inspections { get; set; }
        public List<string> Defects { get; set; }
    }

    public class QualityProjectRequest
    {
        public string Name { get; set; }
        public string ProjectType { get; set; }
    }

    public class QualityPlan
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public DateTime CreatedDate { get; set; }
        public PlanStatus Status { get; set; }
        public List<QualityObjective> QualityObjectives { get; set; }
        public List<InspectionPoint> InspectionPoints { get; set; }
        public List<TestingRequirement> TestingRequirements { get; set; }
        public List<QualityResponsibility> Responsibilities { get; set; }
    }

    public class QualityPlanRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string ProjectType { get; set; }
    }

    public class QualityObjective
    {
        public string Objective { get; set; }
        public string Metric { get; set; }
        public string Target { get; set; }
        public int Priority { get; set; }
    }

    public class InspectionPoint
    {
        public string Id { get; set; }
        public int Sequence { get; set; }
        public ConstructionPhase Phase { get; set; }
        public string Description { get; set; }
        public string TemplateId { get; set; }
        public List<string> RequiredApprovals { get; set; }
        public bool HoldPoint { get; set; }
        public DateTime PlannedDate { get; set; }
        public InspectionStatus Status { get; set; }
    }

    public class TestingRequirement
    {
        public string Test { get; set; }
        public string Standard { get; set; }
        public string Frequency { get; set; }
        public string ResponsibleParty { get; set; }
        public ConstructionPhase Phase { get; set; }
    }

    public class QualityResponsibility
    {
        public string Role { get; set; }
        public string Name { get; set; }
        public List<string> Responsibilities { get; set; }
    }

    public class QualityStandard
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
    }

    public class InspectionTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public ConstructionPhase Phase { get; set; }
        public List<ChecklistItem> ChecklistItems { get; set; }
    }

    public class ChecklistItem
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool Critical { get; set; }
    }

    public class Inspection
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string InspectionPointId { get; set; }
        public string Location { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Inspector { get; set; }
        public InspectionStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ChecklistResult> ChecklistResults { get; set; }
        public string OverallComments { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> Defects { get; set; }
    }

    public class InspectionRequest
    {
        public string ProjectId { get; set; }
        public string InspectionPointId { get; set; }
        public string TemplateId { get; set; }
        public string Location { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Inspector { get; set; }
    }

    public class InspectionPerformRequest
    {
        public string Inspector { get; set; }
        public List<ChecklistResultInput> Results { get; set; }
    }

    public class InspectionCompleteRequest
    {
        public string ProjectId { get; set; }
        public string Inspector { get; set; }
        public string Comments { get; set; }
        public List<ChecklistResultInput> Results { get; set; }
    }

    public class ChecklistResult
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool Critical { get; set; }
        public CheckResult Result { get; set; }
        public string Notes { get; set; }
        public bool PhotoRequired { get; set; }
        public bool PhotoTaken { get; set; }
    }

    public class ChecklistResultInput
    {
        public string ItemId { get; set; }
        public string Description { get; set; }
        public CheckResult Result { get; set; }
        public string Notes { get; set; }
        public bool Critical { get; set; }
        public bool PhotoRequired { get; set; }
        public bool PhotoTaken { get; set; }
    }

    public class Defect
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string InspectionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Category { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Trade { get; set; }
        public DefectStatus Status { get; set; }
        public DateTime IdentifiedDate { get; set; }
        public string IdentifiedBy { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string ClosedBy { get; set; }
        public List<string> Photos { get; set; }
        public List<DefectHistoryEntry> History { get; set; }
    }

    public class DefectRequest
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Category { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Trade { get; set; }
        public string IdentifiedBy { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Photos { get; set; }
    }

    public class DefectStatusUpdate
    {
        public DefectStatus NewStatus { get; set; }
        public string UpdatedBy { get; set; }
        public string Notes { get; set; }
    }

    public class DefectHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public string Notes { get; set; }
    }

    public class DefectAnalytics
    {
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int TotalDefects { get; set; }
        public int OpenDefects { get; set; }
        public int ClosedDefects { get; set; }
        public int OverdueDefects { get; set; }
        public int CriticalDefects { get; set; }
        public decimal AverageResolutionDays { get; set; }
        public Dictionary<string, int> DefectsByCategory { get; set; }
        public Dictionary<string, int> DefectsByTrade { get; set; }
        public Dictionary<string, int> DefectsBySeverity { get; set; }
        public List<DefectTrendPoint> TrendData { get; set; }
        public List<ProblemArea> TopProblemAreas { get; set; }
    }

    public class DefectTrendPoint
    {
        public DateTime WeekEnding { get; set; }
        public int Created { get; set; }
        public int Closed { get; set; }
        public int NetChange { get; set; }
    }

    public class ProblemArea
    {
        public string Location { get; set; }
        public int DefectCount { get; set; }
    }

    public class Punchlist
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Area { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public PunchlistStatus Status { get; set; }
        public List<PunchlistItem> Items { get; set; }
    }

    public class PunchlistRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Area { get; set; }
        public string CreatedBy { get; set; }
    }

    public class PunchlistItem
    {
        public string Id { get; set; }
        public int Number { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Trade { get; set; }
        public PunchlistPriority Priority { get; set; }
        public PunchlistItemStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public List<string> Photos { get; set; }
    }

    public class PunchlistItemRequest
    {
        public int Number { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Trade { get; set; }
        public PunchlistPriority Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Photos { get; set; }
    }

    public class PunchlistSummary
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
        public int OpenItems { get; set; }
        public int OverdueItems { get; set; }
        public Dictionary<string, int> ItemsByTrade { get; set; }
        public Dictionary<string, int> ItemsByPriority { get; set; }
    }

    public class CommissioningPlan
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public CommissioningStatus Status { get; set; }
        public List<CommissioningSystem> Systems { get; set; }
        public List<CommissioningMilestone> Schedule { get; set; }
    }

    public class CommissioningPlanRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
    }

    public class CommissioningSystem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public SystemStatus Status { get; set; }
        public bool PreFunctionalComplete { get; set; }
        public bool FunctionalTestComplete { get; set; }
        public List<FunctionalTest> Tests { get; set; }
    }

    public class FunctionalTest
    {
        public string Name { get; set; }
        public TestStatus Status { get; set; }
        public DateTime? TestDate { get; set; }
        public string TestedBy { get; set; }
        public string Result { get; set; }
    }

    public class CommissioningMilestone
    {
        public string Name { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public bool Required { get; set; }
        public bool Complete { get; set; }
    }

    public class QualityDashboard
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalInspections { get; set; }
        public decimal InspectionPassRate { get; set; }
        public int TotalDefects { get; set; }
        public int OpenDefects { get; set; }
        public int CriticalDefectsOpen { get; set; }
        public int OverdueDefects { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public decimal QualityScore { get; set; }
        public List<QualityActivity> RecentActivity { get; set; }
        public List<UpcomingInspection> UpcomingInspections { get; set; }
    }

    public class QualityActivity
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    public class UpcomingInspection
    {
        public string Name { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Location { get; set; }
    }

    public class QualityAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    public class DefectEventArgs : EventArgs
    {
        public Defect Defect { get; set; }
    }

    public enum ConstructionPhase { Excavation, Foundation, Structure, Envelope, MEPRoughIn, Finishes, MEPFinal, Commissioning, Punchlist }
    public enum PlanStatus { Draft, Active, Complete, Archived }
    public enum InspectionStatus { Pending, Scheduled, InProgress, Passed, ConditionalPass, Failed, Cancelled }
    public enum CheckResult { Pending, Pass, Fail, NA }
    public enum DefectSeverity { Critical, Major, Minor, Cosmetic }
    public enum DefectStatus { Open, InProgress, ReadyForInspection, Reinspection, Closed }
    public enum PunchlistStatus { Open, InProgress, Complete }
    public enum PunchlistPriority { High, Medium, Low }
    public enum PunchlistItemStatus { Open, InProgress, Complete, Verified }
    public enum CommissioningStatus { Planning, PreFunctional, FunctionalTesting, Integrated, Complete }
    public enum SystemStatus { NotStarted, InProgress, Complete, Issues }
    public enum TestStatus { NotStarted, InProgress, Pass, Fail }
    public enum AlertSeverity { Low, Medium, High, Critical }

    #endregion
}
