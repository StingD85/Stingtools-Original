// ============================================================================
// StingBIM AI - BIM Consultant Engine
// Full BIM consulting intelligence for project planning and advisory
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.BIMConsultant
{
    /// <summary>
    /// Core BIM Consultant Engine providing project-level advisory,
    /// BEP generation, LOD management, and strategic BIM guidance.
    /// </summary>
    public sealed class BIMConsultantEngine
    {
        private static readonly Lazy<BIMConsultantEngine> _instance =
            new Lazy<BIMConsultantEngine>(() => new BIMConsultantEngine());
        public static BIMConsultantEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, BIMProject> _projects = new();
        private readonly Dictionary<string, BIMExecutionPlan> _beps = new();
        private readonly Dictionary<string, LODSpecification> _lodSpecs = new();
        private readonly List<BIMStandard> _standards = new();
        private readonly List<BestPractice> _bestPractices = new();
        private readonly Dictionary<string, ProjectRole> _roles = new();

        public event EventHandler<BIMAdvisoryEventArgs> AdvisoryIssued;
        public event EventHandler<BIMAdvisoryEventArgs> ComplianceAlert;

        private BIMConsultantEngine()
        {
            InitializeStandards();
            InitializeBestPractices();
            InitializeRoles();
        }

        #region Initialization

        private void InitializeStandards()
        {
            _standards.AddRange(new[]
            {
                new BIMStandard
                {
                    Code = "ISO19650-1",
                    Name = "ISO 19650-1:2018",
                    Description = "Organization and digitization of information about buildings and civil engineering works",
                    Category = StandardCategory.InformationManagement,
                    Requirements = new List<string>
                    {
                        "Common Data Environment (CDE) implementation",
                        "Information container naming convention",
                        "Status codes for work in progress",
                        "Information exchange requirements"
                    }
                },
                new BIMStandard
                {
                    Code = "ISO19650-2",
                    Name = "ISO 19650-2:2018",
                    Description = "Delivery phase of the assets",
                    Category = StandardCategory.InformationManagement,
                    Requirements = new List<string>
                    {
                        "Appointment and mobilization",
                        "Collaborative production of information",
                        "Information model delivery",
                        "Project closeout"
                    }
                },
                new BIMStandard
                {
                    Code = "ISO19650-3",
                    Name = "ISO 19650-3:2020",
                    Description = "Operational phase of the assets",
                    Category = StandardCategory.AssetManagement,
                    Requirements = new List<string>
                    {
                        "Asset information requirements",
                        "Trigger events for information updates",
                        "Information model maintenance"
                    }
                },
                new BIMStandard
                {
                    Code = "ISO19650-5",
                    Name = "ISO 19650-5:2020",
                    Description = "Security-minded approach to information management",
                    Category = StandardCategory.Security,
                    Requirements = new List<string>
                    {
                        "Security triage process",
                        "Security risk assessment",
                        "Security breach protocol"
                    }
                },
                new BIMStandard
                {
                    Code = "BSUKSBS",
                    Name = "UK BIM Framework",
                    Description = "UK implementation of ISO 19650",
                    Category = StandardCategory.Regional,
                    Requirements = new List<string>
                    {
                        "Plain language questions",
                        "Exchange information requirements",
                        "Asset information requirements"
                    }
                },
                new BIMStandard
                {
                    Code = "NBIMS-US",
                    Name = "National BIM Standard - United States",
                    Description = "US BIM implementation standard",
                    Category = StandardCategory.Regional,
                    Requirements = new List<string>
                    {
                        "Information exchange standards",
                        "Minimum BIM requirements",
                        "Practice documents"
                    }
                },
                new BIMStandard
                {
                    Code = "COBie",
                    Name = "COBie - Construction Operations Building Information Exchange",
                    Description = "Facility handover data format",
                    Category = StandardCategory.DataExchange,
                    Requirements = new List<string>
                    {
                        "Contact information",
                        "Facility data",
                        "Space data",
                        "Equipment data",
                        "Document links"
                    }
                },
                new BIMStandard
                {
                    Code = "IFC4",
                    Name = "IFC 4 - Industry Foundation Classes",
                    Description = "Open BIM data exchange standard",
                    Category = StandardCategory.DataExchange,
                    Requirements = new List<string>
                    {
                        "Geometric representation",
                        "Property sets",
                        "Relationships",
                        "Classification references"
                    }
                }
            });
        }

        private void InitializeBestPractices()
        {
            _bestPractices.AddRange(new[]
            {
                new BestPractice
                {
                    Id = "BP001",
                    Title = "Early BIM Planning",
                    Category = BestPracticeCategory.ProjectSetup,
                    Description = "Establish BIM requirements and execution plan before project kickoff",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Define BIM goals aligned with project objectives",
                        "Establish information requirements early",
                        "Select appropriate LOD for each phase",
                        "Identify key stakeholders and their needs"
                    }
                },
                new BestPractice
                {
                    Id = "BP002",
                    Title = "Model Organization",
                    Category = BestPracticeCategory.Modeling,
                    Description = "Maintain consistent model organization and structure",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Use consistent workset strategy",
                        "Implement file naming conventions",
                        "Establish view organization standards",
                        "Define parameter naming protocols"
                    }
                },
                new BestPractice
                {
                    Id = "BP003",
                    Title = "Regular Coordination",
                    Category = BestPracticeCategory.Coordination,
                    Description = "Schedule regular coordination meetings and model reviews",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Weekly clash detection runs",
                        "Bi-weekly coordination meetings",
                        "Monthly design reviews",
                        "Document all coordination decisions"
                    }
                },
                new BestPractice
                {
                    Id = "BP004",
                    Title = "Version Control",
                    Category = BestPracticeCategory.DataManagement,
                    Description = "Implement robust version control and backup procedures",
                    Impact = ImpactLevel.Critical,
                    Recommendations = new List<string>
                    {
                        "Daily model backups",
                        "Clear version numbering",
                        "Change documentation",
                        "Rollback procedures"
                    }
                },
                new BestPractice
                {
                    Id = "BP005",
                    Title = "LOD Compliance",
                    Category = BestPracticeCategory.Modeling,
                    Description = "Ensure model elements meet required LOD specifications",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Define LOD requirements by element type",
                        "Regular LOD audits",
                        "Progressive LOD development",
                        "LOD verification before milestones"
                    }
                },
                new BestPractice
                {
                    Id = "BP006",
                    Title = "CDE Workflow",
                    Category = BestPracticeCategory.DataManagement,
                    Description = "Use Common Data Environment with proper status workflows",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Work In Progress (WIP) area for development",
                        "Shared area for team review",
                        "Published area for approved documents",
                        "Archive for superseded versions"
                    }
                },
                new BestPractice
                {
                    Id = "BP007",
                    Title = "Quality Gates",
                    Category = BestPracticeCategory.Quality,
                    Description = "Implement quality checkpoints before information sharing",
                    Impact = ImpactLevel.High,
                    Recommendations = new List<string>
                    {
                        "Model audit before sharing",
                        "Clash resolution verification",
                        "Data completeness check",
                        "Standards compliance verification"
                    }
                },
                new BestPractice
                {
                    Id = "BP008",
                    Title = "Training & Support",
                    Category = BestPracticeCategory.People,
                    Description = "Provide adequate BIM training and ongoing support",
                    Impact = ImpactLevel.Medium,
                    Recommendations = new List<string>
                    {
                        "Role-specific training programs",
                        "Software proficiency requirements",
                        "Ongoing skill development",
                        "Knowledge sharing sessions"
                    }
                }
            });
        }

        private void InitializeRoles()
        {
            var roles = new[]
            {
                new ProjectRole
                {
                    RoleId = "BIM_MGR",
                    Title = "BIM Manager",
                    Description = "Overall BIM strategy and implementation leadership",
                    Responsibilities = new List<string>
                    {
                        "Develop and maintain BIM Execution Plan",
                        "Coordinate BIM activities across disciplines",
                        "Establish BIM standards and protocols",
                        "Manage CDE and information workflows",
                        "Report on BIM metrics and KPIs"
                    },
                    RequiredSkills = new List<string>
                    {
                        "Advanced BIM software proficiency",
                        "Project management",
                        "Team leadership",
                        "Standards knowledge (ISO 19650)"
                    }
                },
                new ProjectRole
                {
                    RoleId = "BIM_COORD",
                    Title = "BIM Coordinator",
                    Description = "Day-to-day BIM coordination and model management",
                    Responsibilities = new List<string>
                    {
                        "Run clash detection analyses",
                        "Coordinate model exchanges",
                        "Maintain federated models",
                        "Support discipline teams",
                        "Track issue resolution"
                    },
                    RequiredSkills = new List<string>
                    {
                        "Clash detection software",
                        "Coordination workflows",
                        "Model federation",
                        "Issue tracking systems"
                    }
                },
                new ProjectRole
                {
                    RoleId = "MODEL_MGR",
                    Title = "Model Manager",
                    Description = "Technical model management and quality control",
                    Responsibilities = new List<string>
                    {
                        "Maintain model health and performance",
                        "Enforce modeling standards",
                        "Manage worksets and links",
                        "Perform model audits",
                        "Optimize model structure"
                    },
                    RequiredSkills = new List<string>
                    {
                        "Advanced Revit proficiency",
                        "Model optimization techniques",
                        "Quality assurance processes",
                        "Troubleshooting expertise"
                    }
                },
                new ProjectRole
                {
                    RoleId = "INFO_MGR",
                    Title = "Information Manager",
                    Description = "Information requirements and data management",
                    Responsibilities = new List<string>
                    {
                        "Define information requirements",
                        "Manage data exchange processes",
                        "Ensure data quality and completeness",
                        "Coordinate handover information",
                        "Maintain document control"
                    },
                    RequiredSkills = new List<string>
                    {
                        "Information management",
                        "Data standards (COBie, IFC)",
                        "Document control systems",
                        "Database management"
                    }
                },
                new ProjectRole
                {
                    RoleId = "DISC_LEAD",
                    Title = "Discipline Lead",
                    Description = "BIM leadership within a specific discipline",
                    Responsibilities = new List<string>
                    {
                        "Lead discipline modeling efforts",
                        "Ensure discipline standards compliance",
                        "Coordinate with other disciplines",
                        "Review and approve discipline deliverables",
                        "Train discipline team members"
                    },
                    RequiredSkills = new List<string>
                    {
                        "Discipline-specific expertise",
                        "BIM software proficiency",
                        "Coordination skills",
                        "Quality review capabilities"
                    }
                }
            };

            foreach (var role in roles)
            {
                _roles[role.RoleId] = role;
            }
        }

        #endregion

        #region Project Management

        /// <summary>
        /// Create a new BIM project
        /// </summary>
        public BIMProject CreateProject(BIMProjectRequest request)
        {
            var project = new BIMProject
            {
                ProjectId = $"PROJ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                ProjectName = request.ProjectName,
                ProjectNumber = request.ProjectNumber,
                ClientName = request.ClientName,
                ProjectType = request.ProjectType,
                Location = request.Location,
                GrossArea = request.GrossArea,
                NumberOfFloors = request.NumberOfFloors,
                EstimatedBudget = request.EstimatedBudget,
                StartDate = request.StartDate,
                PlannedEndDate = request.PlannedEndDate,
                BIMLevel = request.BIMLevel,
                ApplicableStandards = request.ApplicableStandards ?? new List<string> { "ISO19650-1", "ISO19650-2" },
                Disciplines = request.Disciplines ?? new List<string> { "Architecture", "Structure", "MEP" },
                CreatedDate = DateTime.UtcNow,
                Status = ProjectStatus.Planning,
                TeamMembers = new List<TeamMember>(),
                Milestones = new List<ProjectMilestone>(),
                DeliverablePlans = new List<DeliverablePlan>()
            };

            // Generate standard milestones based on project type
            project.Milestones = GenerateStandardMilestones(project);

            lock (_lock)
            {
                _projects[project.ProjectId] = project;
            }

            return project;
        }

        private List<ProjectMilestone> GenerateStandardMilestones(BIMProject project)
        {
            var duration = (project.PlannedEndDate - project.StartDate).TotalDays;
            var milestones = new List<ProjectMilestone>();

            // Standard BIM milestones
            var phases = new[]
            {
                ("Concept Design", 0.10, "LOD 100"),
                ("Schematic Design", 0.25, "LOD 200"),
                ("Design Development", 0.50, "LOD 300"),
                ("Construction Documents", 0.75, "LOD 350"),
                ("Construction", 0.95, "LOD 400"),
                ("As-Built/Handover", 1.00, "LOD 500")
            };

            foreach (var (name, progress, lod) in phases)
            {
                milestones.Add(new ProjectMilestone
                {
                    MilestoneId = Guid.NewGuid().ToString(),
                    Name = name,
                    PlannedDate = project.StartDate.AddDays(duration * progress),
                    RequiredLOD = lod,
                    Status = MilestoneStatus.Pending,
                    Deliverables = GetMilestoneDeliverables(name)
                });
            }

            return milestones;
        }

        private List<string> GetMilestoneDeliverables(string phase)
        {
            return phase switch
            {
                "Concept Design" => new List<string>
                {
                    "BIM Execution Plan Draft",
                    "Concept Model",
                    "Massing Studies",
                    "Site Analysis"
                },
                "Schematic Design" => new List<string>
                {
                    "BIM Execution Plan Final",
                    "Schematic Models (All Disciplines)",
                    "Initial Clash Report",
                    "Space Planning Verification"
                },
                "Design Development" => new List<string>
                {
                    "Coordinated Design Models",
                    "Clash Resolution Report",
                    "4D Schedule Integration",
                    "Specification Links"
                },
                "Construction Documents" => new List<string>
                {
                    "Construction Models",
                    "Drawing Sets",
                    "5D Cost Model",
                    "Shop Drawing Coordination"
                },
                "Construction" => new List<string>
                {
                    "As-Built Updates",
                    "Field Coordination Models",
                    "Progress Tracking",
                    "Change Order Integration"
                },
                "As-Built/Handover" => new List<string>
                {
                    "As-Built Model",
                    "COBie Data Export",
                    "O&M Documentation",
                    "FM System Integration"
                },
                _ => new List<string>()
            };
        }

        #endregion

        #region BIM Execution Plan

        /// <summary>
        /// Generate a comprehensive BIM Execution Plan
        /// </summary>
        public async Task<BIMExecutionPlan> GenerateBEPAsync(string projectId, BEPRequest request)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                throw new KeyNotFoundException($"Project {projectId} not found");

            var bep = new BIMExecutionPlan
            {
                BEPId = $"BEP-{project.ProjectNumber}-{DateTime.UtcNow:yyyyMMdd}",
                ProjectId = projectId,
                Version = "1.0",
                CreatedDate = DateTime.UtcNow,
                Status = BEPStatus.Draft,

                // Project Information
                ProjectInfo = new BEPProjectInfo
                {
                    ProjectName = project.ProjectName,
                    ProjectNumber = project.ProjectNumber,
                    ClientName = project.ClientName,
                    Location = project.Location,
                    ProjectType = project.ProjectType,
                    GrossArea = project.GrossArea,
                    BIMLevel = project.BIMLevel,
                    ApplicableStandards = project.ApplicableStandards
                },

                // BIM Goals
                Goals = GenerateBIMGoals(project, request),

                // Organizational Roles
                OrganizationalRoles = GenerateOrganizationalRoles(request),

                // BIM Process Design
                ProcessDesign = GenerateProcessDesign(project),

                // Technology Plan
                TechnologyPlan = GenerateTechnologyPlan(request),

                // LOD Matrix
                LODMatrix = GenerateLODMatrix(project),

                // Deliverable Schedule
                DeliverableSchedule = GenerateDeliverableSchedule(project),

                // Quality Plan
                QualityPlan = GenerateQualityPlan(),

                // Collaboration Procedures
                CollaborationProcedures = GenerateCollaborationProcedures(),

                // Model Standards
                ModelStandards = GenerateModelStandards()
            };

            lock (_lock)
            {
                _beps[bep.BEPId] = bep;
            }

            // Issue advisory about BEP creation
            AdvisoryIssued?.Invoke(this, new BIMAdvisoryEventArgs
            {
                Type = AdvisoryType.Information,
                Message = $"BIM Execution Plan {bep.BEPId} generated for project {project.ProjectName}",
                ProjectId = projectId,
                Recommendations = new List<string>
                {
                    "Review and customize BEP for project-specific needs",
                    "Circulate to all stakeholders for input",
                    "Schedule kickoff meeting to review BEP"
                }
            });

            return bep;
        }

        private List<BIMGoal> GenerateBIMGoals(BIMProject project, BEPRequest request)
        {
            var goals = new List<BIMGoal>
            {
                new BIMGoal
                {
                    GoalId = "G001",
                    Category = GoalCategory.DesignCoordination,
                    Description = "Achieve clash-free coordinated design",
                    Metrics = new List<string>
                    {
                        "Zero critical clashes at CD phase",
                        "Weekly clash resolution rate > 90%",
                        "All disciplines coordinated by DD phase"
                    },
                    Priority = Priority.High
                },
                new BIMGoal
                {
                    GoalId = "G002",
                    Category = GoalCategory.CostControl,
                    Description = "Enable accurate quantity takeoffs and cost estimation",
                    Metrics = new List<string>
                    {
                        "Model-based QTO accuracy within 5%",
                        "Monthly cost reports from model",
                        "Change impact analysis within 24 hours"
                    },
                    Priority = Priority.High
                },
                new BIMGoal
                {
                    GoalId = "G003",
                    Category = GoalCategory.ScheduleManagement,
                    Description = "Support 4D construction sequencing",
                    Metrics = new List<string>
                    {
                        "4D model updated weekly during construction",
                        "Sequence conflicts identified pre-construction",
                        "Progress tracking linked to model"
                    },
                    Priority = Priority.Medium
                },
                new BIMGoal
                {
                    GoalId = "G004",
                    Category = GoalCategory.FacilityManagement,
                    Description = "Deliver comprehensive asset information for FM",
                    Metrics = new List<string>
                    {
                        "COBie data 100% complete at handover",
                        "All equipment with maintenance data",
                        "Spatial data linked to FM system"
                    },
                    Priority = Priority.High
                },
                new BIMGoal
                {
                    GoalId = "G005",
                    Category = GoalCategory.Sustainability,
                    Description = "Support sustainability analysis and certification",
                    Metrics = new List<string>
                    {
                        "Energy model derived from BIM",
                        "Material quantities for LCA",
                        "Daylight analysis from model"
                    },
                    Priority = Priority.Medium
                }
            };

            // Add custom goals from request
            if (request.CustomGoals != null)
            {
                goals.AddRange(request.CustomGoals);
            }

            return goals;
        }

        private List<BEPRole> GenerateOrganizationalRoles(BEPRequest request)
        {
            var roles = new List<BEPRole>();

            foreach (var role in _roles.Values)
            {
                roles.Add(new BEPRole
                {
                    RoleId = role.RoleId,
                    Title = role.Title,
                    Description = role.Description,
                    Responsibilities = role.Responsibilities,
                    AssignedTo = request.RoleAssignments?.GetValueOrDefault(role.RoleId),
                    Organization = request.RoleOrganizations?.GetValueOrDefault(role.RoleId)
                });
            }

            return roles;
        }

        private BEPProcessDesign GenerateProcessDesign(BIMProject project)
        {
            return new BEPProcessDesign
            {
                Phases = project.Milestones.Select(m => new ProcessPhase
                {
                    Name = m.Name,
                    StartDate = m.PlannedDate.AddMonths(-2),
                    EndDate = m.PlannedDate,
                    RequiredLOD = m.RequiredLOD,
                    KeyActivities = m.Deliverables
                }).ToList(),

                CoordinationMeetings = new List<MeetingSchedule>
                {
                    new MeetingSchedule { Name = "Weekly BIM Coordination", Frequency = "Weekly", Attendees = "All Discipline Leads" },
                    new MeetingSchedule { Name = "Clash Review", Frequency = "Weekly", Attendees = "BIM Coordinator, Affected Disciplines" },
                    new MeetingSchedule { Name = "Design Review", Frequency = "Bi-weekly", Attendees = "Project Team, Client" },
                    new MeetingSchedule { Name = "BIM Steering Committee", Frequency = "Monthly", Attendees = "Project Managers, BIM Manager" }
                },

                ModelExchangeProtocol = new ModelExchangeProtocol
                {
                    ExchangeFrequency = "Weekly",
                    ExchangeFormat = "RVT native + IFC export",
                    CDEPlatform = "ProjectWise / ACC",
                    StatusCodes = new Dictionary<string, string>
                    {
                        { "S0", "Work in Progress" },
                        { "S1", "For Coordination" },
                        { "S2", "For Information" },
                        { "S3", "For Review and Comment" },
                        { "S4", "For Stage Approval" },
                        { "S5", "For Construction" },
                        { "S6", "As-Built" }
                    }
                }
            };
        }

        private BEPTechnologyPlan GenerateTechnologyPlan(BEPRequest request)
        {
            return new BEPTechnologyPlan
            {
                AuthoringSoftware = new List<SoftwareRequirement>
                {
                    new SoftwareRequirement { Name = "Autodesk Revit", Version = "2024/2025", Purpose = "BIM Authoring", Disciplines = new List<string> { "Architecture", "Structure", "MEP" } },
                    new SoftwareRequirement { Name = "AutoCAD", Version = "2024/2025", Purpose = "2D Documentation", Disciplines = new List<string> { "All" } },
                    new SoftwareRequirement { Name = "Civil 3D", Version = "2024/2025", Purpose = "Site/Civil", Disciplines = new List<string> { "Civil" } }
                },

                CoordinationSoftware = new List<SoftwareRequirement>
                {
                    new SoftwareRequirement { Name = "Autodesk Navisworks", Version = "2024/2025", Purpose = "Clash Detection, 4D" },
                    new SoftwareRequirement { Name = "BIM 360 / ACC", Version = "Latest", Purpose = "Cloud Collaboration" },
                    new SoftwareRequirement { Name = "Solibri", Version = "Latest", Purpose = "Model Checking" }
                },

                AnalysisSoftware = new List<SoftwareRequirement>
                {
                    new SoftwareRequirement { Name = "Insight", Version = "Latest", Purpose = "Energy Analysis" },
                    new SoftwareRequirement { Name = "Robot Structural", Version = "2024", Purpose = "Structural Analysis" },
                    new SoftwareRequirement { Name = "CFD", Version = "2024", Purpose = "HVAC Analysis" }
                },

                CDEPlatform = request.CDEPlatform ?? "Autodesk Construction Cloud",
                FileNamingConvention = GenerateFileNamingConvention(),
                HardwareRequirements = GenerateHardwareRequirements()
            };
        }

        private FileNamingConvention GenerateFileNamingConvention()
        {
            return new FileNamingConvention
            {
                Pattern = "{Project}-{Originator}-{Zone}-{Level}-{Type}-{Discipline}-{Number}",
                Examples = new List<string>
                {
                    "PROJ01-ARC-ZA-L01-M3-AR-0001.rvt",
                    "PROJ01-STR-ZA-L01-M3-ST-0001.rvt",
                    "PROJ01-MEP-ZA-L01-M3-ME-0001.rvt"
                },
                FieldDefinitions = new Dictionary<string, string>
                {
                    { "Project", "Project code (6 chars max)" },
                    { "Originator", "Company/team code (3 chars)" },
                    { "Zone", "Building zone (ZA, ZB, etc.)" },
                    { "Level", "Level (L01, L02, RF, etc.)" },
                    { "Type", "File type (M3=3D Model, DR=Drawing)" },
                    { "Discipline", "Discipline code (AR, ST, ME, EL, PL)" },
                    { "Number", "Sequential number (4 digits)" }
                }
            };
        }

        private HardwareRequirements GenerateHardwareRequirements()
        {
            return new HardwareRequirements
            {
                MinimumSpec = new HardwareSpec
                {
                    CPU = "Intel i7 / AMD Ryzen 7",
                    RAM = "32 GB",
                    GPU = "NVIDIA RTX 3060 / AMD equivalent",
                    Storage = "512 GB NVMe SSD",
                    Display = "1920x1080"
                },
                RecommendedSpec = new HardwareSpec
                {
                    CPU = "Intel i9 / AMD Ryzen 9",
                    RAM = "64 GB",
                    GPU = "NVIDIA RTX 4070 / AMD equivalent",
                    Storage = "1 TB NVMe SSD",
                    Display = "2560x1440 or 4K"
                }
            };
        }

        private List<LODMatrixEntry> GenerateLODMatrix(BIMProject project)
        {
            var matrix = new List<LODMatrixEntry>();

            var elementTypes = new[]
            {
                ("Walls - Exterior", "Architecture"),
                ("Walls - Interior", "Architecture"),
                ("Floors", "Architecture"),
                ("Roofs", "Architecture"),
                ("Ceilings", "Architecture"),
                ("Doors", "Architecture"),
                ("Windows", "Architecture"),
                ("Stairs", "Architecture"),
                ("Columns - Structural", "Structure"),
                ("Beams", "Structure"),
                ("Foundations", "Structure"),
                ("Slabs - Structural", "Structure"),
                ("HVAC Ducts", "Mechanical"),
                ("HVAC Equipment", "Mechanical"),
                ("Piping", "Plumbing"),
                ("Plumbing Fixtures", "Plumbing"),
                ("Electrical Panels", "Electrical"),
                ("Lighting Fixtures", "Electrical"),
                ("Cable Trays", "Electrical"),
                ("Fire Protection", "Fire Protection")
            };

            foreach (var (element, discipline) in elementTypes)
            {
                matrix.Add(new LODMatrixEntry
                {
                    ElementType = element,
                    Discipline = discipline,
                    ConceptLOD = "100",
                    SchematicLOD = "200",
                    DDevelLOD = "300",
                    ConstructionLOD = "350",
                    AsBuiltLOD = "500",
                    LODSpecifications = GetLODSpecifications(element)
                });
            }

            return matrix;
        }

        private Dictionary<string, string> GetLODSpecifications(string elementType)
        {
            return new Dictionary<string, string>
            {
                { "LOD 100", "Overall massing, approximate location" },
                { "LOD 200", "Generic placeholder, approximate size and shape" },
                { "LOD 300", "Specific type, accurate size, location, orientation" },
                { "LOD 350", "Connection details, supports, clearances" },
                { "LOD 400", "Fabrication level detail" },
                { "LOD 500", "As-built verified, field measurements" }
            };
        }

        private List<DeliverableScheduleEntry> GenerateDeliverableSchedule(BIMProject project)
        {
            var schedule = new List<DeliverableScheduleEntry>();

            foreach (var milestone in project.Milestones)
            {
                foreach (var deliverable in milestone.Deliverables)
                {
                    schedule.Add(new DeliverableScheduleEntry
                    {
                        Deliverable = deliverable,
                        Phase = milestone.Name,
                        DueDate = milestone.PlannedDate,
                        ResponsibleParty = GetResponsibleParty(deliverable),
                        Format = GetDeliverableFormat(deliverable),
                        ReviewRequired = true
                    });
                }
            }

            return schedule;
        }

        private string GetResponsibleParty(string deliverable)
        {
            if (deliverable.Contains("BIM Execution Plan")) return "BIM Manager";
            if (deliverable.Contains("Model")) return "Discipline Leads";
            if (deliverable.Contains("Clash")) return "BIM Coordinator";
            if (deliverable.Contains("COBie")) return "Information Manager";
            return "Project Team";
        }

        private string GetDeliverableFormat(string deliverable)
        {
            if (deliverable.Contains("Model")) return "RVT, IFC, NWC";
            if (deliverable.Contains("COBie")) return "XLSX, XML";
            if (deliverable.Contains("Drawing")) return "PDF, DWG";
            if (deliverable.Contains("Report")) return "PDF, DOCX";
            return "PDF";
        }

        private BEPQualityPlan GenerateQualityPlan()
        {
            return new BEPQualityPlan
            {
                QualityCheckpoints = new List<QualityCheckpoint>
                {
                    new QualityCheckpoint
                    {
                        Name = "Pre-Share Check",
                        Trigger = "Before any model share",
                        Checks = new List<string>
                        {
                            "File naming convention compliance",
                            "No error warnings in model",
                            "Correct shared coordinates",
                            "Required parameters populated"
                        }
                    },
                    new QualityCheckpoint
                    {
                        Name = "Coordination Check",
                        Trigger = "Weekly coordination cycle",
                        Checks = new List<string>
                        {
                            "Clash detection run complete",
                            "Critical clashes addressed",
                            "Model elements at correct LOD",
                            "Worksets properly organized"
                        }
                    },
                    new QualityCheckpoint
                    {
                        Name = "Milestone Check",
                        Trigger = "Before each project milestone",
                        Checks = new List<string>
                        {
                            "LOD requirements met",
                            "All deliverables complete",
                            "Data completeness verified",
                            "Standards compliance verified"
                        }
                    }
                },

                AuditFrequency = "Monthly",
                AuditCriteria = new List<string>
                {
                    "Model health metrics",
                    "Element naming compliance",
                    "Parameter data quality",
                    "Coordination status",
                    "Documentation completeness"
                }
            };
        }

        private BEPCollaborationProcedures GenerateCollaborationProcedures()
        {
            return new BEPCollaborationProcedures
            {
                ModelSharingProtocol = new ModelSharingProtocol
                {
                    ShareFrequency = "Weekly (minimum)",
                    ShareDay = "Monday",
                    ShareLocation = "CDE Shared Area",
                    NotificationRequired = true,
                    PreShareChecklist = new List<string>
                    {
                        "Detach from central",
                        "Purge unused elements",
                        "Audit and compact",
                        "Verify shared coordinates",
                        "Update revision"
                    }
                },

                ClashResolutionProcess = new ClashResolutionProcess
                {
                    DetectionFrequency = "Weekly",
                    ReviewMeeting = "Weekly clash review meeting",
                    ResolutionSLA = new Dictionary<string, int>
                    {
                        { "Critical", 2 },  // days
                        { "Major", 5 },
                        { "Minor", 10 }
                    },
                    EscalationPath = new List<string>
                    {
                        "BIM Coordinator",
                        "Discipline Leads",
                        "BIM Manager",
                        "Project Manager"
                    }
                },

                CommunicationPlan = new CommunicationPlan
                {
                    PrimaryChannel = "CDE Comments/Issues",
                    MeetingNotes = "Stored in CDE",
                    IssueTracking = "CDE Issue Module",
                    ContactDirectory = "Maintained by BIM Manager"
                }
            };
        }

        private BEPModelStandards GenerateModelStandards()
        {
            return new BEPModelStandards
            {
                Units = new ModelUnits
                {
                    Length = "Millimeters",
                    Area = "Square Meters",
                    Volume = "Cubic Meters",
                    Angle = "Decimal Degrees"
                },

                Coordinates = new CoordinateSystem
                {
                    ProjectBasePoint = "Set to site survey origin",
                    SharedCoordinates = "All models use shared coordinates",
                    TrueNorth = "Set per survey data"
                },

                WorksetStrategy = new WorksetStrategy
                {
                    StandardWorksets = new List<WorksetDefinition>
                    {
                        new WorksetDefinition { Name = "00_Links", Purpose = "External references" },
                        new WorksetDefinition { Name = "01_Site", Purpose = "Site elements" },
                        new WorksetDefinition { Name = "02_Shell", Purpose = "Building shell" },
                        new WorksetDefinition { Name = "03_Interior", Purpose = "Interior elements" },
                        new WorksetDefinition { Name = "04_FF&E", Purpose = "Furniture and equipment" },
                        new WorksetDefinition { Name = "99_Working", Purpose = "Temporary work" }
                    },
                    NamingConvention = "NN_Description format"
                },

                ViewOrganization = new ViewOrganization
                {
                    BrowserStructure = "Discipline > Type > Phase > Level",
                    ViewNaming = "{Discipline}-{Type}-{Level}-{Description}",
                    ViewTemplates = "Mandatory for all project views"
                },

                ParameterStandards = new ParameterStandards
                {
                    SharedParameterFile = "Use project shared parameter file",
                    NamingPrefix = "Project code prefix for custom parameters",
                    RequiredParameters = new List<string>
                    {
                        "Classification code",
                        "Phase created/demolished",
                        "Cost code",
                        "Specification reference"
                    }
                }
            };
        }

        #endregion

        #region LOD Management

        /// <summary>
        /// Create LOD specification for a project
        /// </summary>
        public LODSpecification CreateLODSpecification(string projectId, LODSpecificationRequest request)
        {
            var spec = new LODSpecification
            {
                SpecId = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                CreatedDate = DateTime.UtcNow,
                Elements = request.Elements ?? GenerateDefaultLODElements(),
                PhaseRequirements = request.PhaseRequirements ?? GenerateDefaultPhaseRequirements()
            };

            lock (_lock)
            {
                _lodSpecs[spec.SpecId] = spec;
            }

            return spec;
        }

        private List<LODElement> GenerateDefaultLODElements()
        {
            return new List<LODElement>
            {
                new LODElement
                {
                    Category = "Walls",
                    ElementType = "Exterior Walls",
                    LOD100 = "Overall building envelope massing",
                    LOD200 = "Generic wall type, approximate thickness",
                    LOD300 = "Specific wall assembly, accurate dimensions, materials",
                    LOD350 = "Connection details, embedments, supports",
                    LOD400 = "Shop drawing level, fabrication details",
                    LOD500 = "As-built verified dimensions and materials"
                },
                new LODElement
                {
                    Category = "Structural",
                    ElementType = "Steel Columns",
                    LOD100 = "Approximate column locations",
                    LOD200 = "Generic column shapes and sizes",
                    LOD300 = "Specific member sizes, splice locations",
                    LOD350 = "Connection details, fireproofing",
                    LOD400 = "Fabrication details, bolt patterns",
                    LOD500 = "As-built verified, field modifications"
                },
                new LODElement
                {
                    Category = "HVAC",
                    ElementType = "Ductwork",
                    LOD100 = "Major duct runs indicated schematically",
                    LOD200 = "Generic duct sizes and routing",
                    LOD300 = "Specific duct sizes, fittings, routing",
                    LOD350 = "Supports, access doors, connections",
                    LOD400 = "Fabrication spool drawings",
                    LOD500 = "As-built verified routing and sizes"
                }
            };
        }

        private Dictionary<string, string> GenerateDefaultPhaseRequirements()
        {
            return new Dictionary<string, string>
            {
                { "Concept", "LOD 100" },
                { "Schematic Design", "LOD 200" },
                { "Design Development", "LOD 300" },
                { "Construction Documents", "LOD 350" },
                { "Construction", "LOD 400" },
                { "As-Built", "LOD 500" }
            };
        }

        /// <summary>
        /// Verify LOD compliance for a project milestone
        /// </summary>
        public LODComplianceReport VerifyLODCompliance(string projectId, string milestone, List<ElementLODStatus> elements)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                throw new KeyNotFoundException($"Project {projectId} not found");

            var report = new LODComplianceReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                Milestone = milestone,
                GeneratedDate = DateTime.UtcNow,
                ElementStatuses = new List<ElementComplianceStatus>(),
                OverallCompliance = 0,
                Issues = new List<LODComplianceIssue>()
            };

            var requiredLOD = project.Milestones.FirstOrDefault(m => m.Name == milestone)?.RequiredLOD ?? "LOD 300";
            var requiredLevel = int.Parse(requiredLOD.Replace("LOD ", ""));

            int compliant = 0;
            foreach (var element in elements)
            {
                var elementLevel = int.Parse(element.CurrentLOD.Replace("LOD ", ""));
                var isCompliant = elementLevel >= requiredLevel;

                report.ElementStatuses.Add(new ElementComplianceStatus
                {
                    ElementId = element.ElementId,
                    ElementType = element.ElementType,
                    CurrentLOD = element.CurrentLOD,
                    RequiredLOD = requiredLOD,
                    IsCompliant = isCompliant
                });

                if (isCompliant)
                    compliant++;
                else
                {
                    report.Issues.Add(new LODComplianceIssue
                    {
                        ElementId = element.ElementId,
                        Issue = $"Element at {element.CurrentLOD}, requires {requiredLOD}",
                        Severity = requiredLevel - elementLevel > 100 ? "Critical" : "Warning"
                    });
                }
            }

            report.OverallCompliance = elements.Count > 0 ? (double)compliant / elements.Count * 100 : 0;

            return report;
        }

        #endregion

        #region Advisory Services

        /// <summary>
        /// Get BIM recommendations for a project
        /// </summary>
        public List<BIMRecommendation> GetRecommendations(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                throw new KeyNotFoundException($"Project {projectId} not found");

            var recommendations = new List<BIMRecommendation>();

            // Phase-based recommendations
            foreach (var milestone in project.Milestones.Where(m => m.Status == MilestoneStatus.Pending))
            {
                recommendations.Add(new BIMRecommendation
                {
                    Category = RecommendationCategory.Planning,
                    Title = $"Prepare for {milestone.Name}",
                    Description = $"Upcoming milestone requires {milestone.RequiredLOD} completion",
                    Priority = Priority.High,
                    Actions = milestone.Deliverables.Select(d => $"Complete: {d}").ToList(),
                    DueDate = milestone.PlannedDate.AddDays(-14) // 2 weeks before
                });
            }

            // Team-based recommendations
            if (project.TeamMembers.Count == 0)
            {
                recommendations.Add(new BIMRecommendation
                {
                    Category = RecommendationCategory.Organization,
                    Title = "Assign BIM Team Members",
                    Description = "No team members assigned to project",
                    Priority = Priority.Critical,
                    Actions = _roles.Values.Select(r => $"Assign {r.Title}").ToList()
                });
            }

            // Standard best practices
            foreach (var bp in _bestPractices.Where(b => b.Impact >= ImpactLevel.High))
            {
                recommendations.Add(new BIMRecommendation
                {
                    Category = RecommendationCategory.BestPractice,
                    Title = bp.Title,
                    Description = bp.Description,
                    Priority = bp.Impact == ImpactLevel.Critical ? Priority.Critical : Priority.High,
                    Actions = bp.Recommendations
                });
            }

            return recommendations;
        }

        /// <summary>
        /// Analyze project and provide strategic advice
        /// </summary>
        public ProjectAnalysis AnalyzeProject(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                throw new KeyNotFoundException($"Project {projectId} not found");

            var analysis = new ProjectAnalysis
            {
                ProjectId = projectId,
                AnalysisDate = DateTime.UtcNow,
                Strengths = new List<string>(),
                Weaknesses = new List<string>(),
                Opportunities = new List<string>(),
                Threats = new List<string>(),
                Recommendations = new List<string>()
            };

            // Analyze project setup
            if (project.ApplicableStandards.Contains("ISO19650-1"))
                analysis.Strengths.Add("ISO 19650 compliance framework in place");
            else
                analysis.Weaknesses.Add("Not following ISO 19650 framework");

            if (project.BIMLevel >= 2)
                analysis.Strengths.Add($"BIM Level {project.BIMLevel} enables good collaboration");
            else
                analysis.Recommendations.Add("Consider advancing to BIM Level 2 for better collaboration");

            // Analyze team
            if (project.TeamMembers.Any(t => t.Role == "BIM Manager"))
                analysis.Strengths.Add("Dedicated BIM Manager assigned");
            else
                analysis.Weaknesses.Add("No dedicated BIM Manager");

            // Analyze timeline
            var duration = (project.PlannedEndDate - project.StartDate).TotalDays / 30.0;
            if (duration < 12 && project.GrossArea > 10000)
                analysis.Threats.Add("Aggressive timeline for project size");

            // Opportunities
            analysis.Opportunities.Add("4D sequencing can improve construction coordination");
            analysis.Opportunities.Add("5D integration can enable real-time cost tracking");
            analysis.Opportunities.Add("Digital twin capability for facilities management");

            return analysis;
        }

        /// <summary>
        /// Get applicable best practices
        /// </summary>
        public List<BestPractice> GetBestPractices(BestPracticeCategory? category = null)
        {
            if (category.HasValue)
                return _bestPractices.Where(bp => bp.Category == category.Value).ToList();

            return _bestPractices.ToList();
        }

        /// <summary>
        /// Get applicable standards
        /// </summary>
        public List<BIMStandard> GetStandards(StandardCategory? category = null)
        {
            if (category.HasValue)
                return _standards.Where(s => s.Category == category.Value).ToList();

            return _standards.ToList();
        }

        #endregion

        #region Queries

        public BIMProject GetProject(string projectId)
        {
            lock (_lock)
            {
                return _projects.TryGetValue(projectId, out var project) ? project : null;
            }
        }

        public BIMExecutionPlan GetBEP(string bepId)
        {
            lock (_lock)
            {
                return _beps.TryGetValue(bepId, out var bep) ? bep : null;
            }
        }

        public List<BIMProject> GetAllProjects()
        {
            lock (_lock)
            {
                return _projects.Values.ToList();
            }
        }

        public List<ProjectRole> GetRoles()
        {
            return _roles.Values.ToList();
        }

        #endregion
    }

    #region Data Models

    public class BIMProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ClientName { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public double GrossArea { get; set; }
        public int NumberOfFloors { get; set; }
        public decimal EstimatedBudget { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public int BIMLevel { get; set; }
        public List<string> ApplicableStandards { get; set; }
        public List<string> Disciplines { get; set; }
        public DateTime CreatedDate { get; set; }
        public ProjectStatus Status { get; set; }
        public List<TeamMember> TeamMembers { get; set; }
        public List<ProjectMilestone> Milestones { get; set; }
        public List<DeliverablePlan> DeliverablePlans { get; set; }
    }

    public class BIMProjectRequest
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ClientName { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public double GrossArea { get; set; }
        public int NumberOfFloors { get; set; }
        public decimal EstimatedBudget { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public int BIMLevel { get; set; } = 2;
        public List<string> ApplicableStandards { get; set; }
        public List<string> Disciplines { get; set; }
    }

    public class TeamMember
    {
        public string MemberId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Organization { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public List<string> Disciplines { get; set; }
    }

    public class ProjectMilestone
    {
        public string MilestoneId { get; set; }
        public string Name { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public string RequiredLOD { get; set; }
        public MilestoneStatus Status { get; set; }
        public List<string> Deliverables { get; set; }
    }

    public class DeliverablePlan
    {
        public string DeliverableId { get; set; }
        public string Name { get; set; }
        public string Phase { get; set; }
        public DateTime DueDate { get; set; }
        public string ResponsibleParty { get; set; }
        public string Format { get; set; }
        public DeliverableStatus Status { get; set; }
    }

    public class ProjectRole
    {
        public string RoleId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Responsibilities { get; set; }
        public List<string> RequiredSkills { get; set; }
    }

    public class BIMStandard
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StandardCategory Category { get; set; }
        public List<string> Requirements { get; set; }
    }

    public class BestPractice
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public BestPracticeCategory Category { get; set; }
        public string Description { get; set; }
        public ImpactLevel Impact { get; set; }
        public List<string> Recommendations { get; set; }
    }

    #region BEP Models

    public class BIMExecutionPlan
    {
        public string BEPId { get; set; }
        public string ProjectId { get; set; }
        public string Version { get; set; }
        public DateTime CreatedDate { get; set; }
        public BEPStatus Status { get; set; }
        public BEPProjectInfo ProjectInfo { get; set; }
        public List<BIMGoal> Goals { get; set; }
        public List<BEPRole> OrganizationalRoles { get; set; }
        public BEPProcessDesign ProcessDesign { get; set; }
        public BEPTechnologyPlan TechnologyPlan { get; set; }
        public List<LODMatrixEntry> LODMatrix { get; set; }
        public List<DeliverableScheduleEntry> DeliverableSchedule { get; set; }
        public BEPQualityPlan QualityPlan { get; set; }
        public BEPCollaborationProcedures CollaborationProcedures { get; set; }
        public BEPModelStandards ModelStandards { get; set; }
    }

    public class BEPRequest
    {
        public Dictionary<string, string> RoleAssignments { get; set; }
        public Dictionary<string, string> RoleOrganizations { get; set; }
        public string CDEPlatform { get; set; }
        public List<BIMGoal> CustomGoals { get; set; }
    }

    public class BEPProjectInfo
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ClientName { get; set; }
        public string Location { get; set; }
        public string ProjectType { get; set; }
        public double GrossArea { get; set; }
        public int BIMLevel { get; set; }
        public List<string> ApplicableStandards { get; set; }
    }

    public class BIMGoal
    {
        public string GoalId { get; set; }
        public GoalCategory Category { get; set; }
        public string Description { get; set; }
        public List<string> Metrics { get; set; }
        public Priority Priority { get; set; }
    }

    public class BEPRole
    {
        public string RoleId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Responsibilities { get; set; }
        public string AssignedTo { get; set; }
        public string Organization { get; set; }
    }

    public class BEPProcessDesign
    {
        public List<ProcessPhase> Phases { get; set; }
        public List<MeetingSchedule> CoordinationMeetings { get; set; }
        public ModelExchangeProtocol ModelExchangeProtocol { get; set; }
    }

    public class ProcessPhase
    {
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string RequiredLOD { get; set; }
        public List<string> KeyActivities { get; set; }
    }

    public class MeetingSchedule
    {
        public string Name { get; set; }
        public string Frequency { get; set; }
        public string Attendees { get; set; }
    }

    public class ModelExchangeProtocol
    {
        public string ExchangeFrequency { get; set; }
        public string ExchangeFormat { get; set; }
        public string CDEPlatform { get; set; }
        public Dictionary<string, string> StatusCodes { get; set; }
    }

    public class BEPTechnologyPlan
    {
        public List<SoftwareRequirement> AuthoringSoftware { get; set; }
        public List<SoftwareRequirement> CoordinationSoftware { get; set; }
        public List<SoftwareRequirement> AnalysisSoftware { get; set; }
        public string CDEPlatform { get; set; }
        public FileNamingConvention FileNamingConvention { get; set; }
        public HardwareRequirements HardwareRequirements { get; set; }
    }

    public class SoftwareRequirement
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Purpose { get; set; }
        public List<string> Disciplines { get; set; }
    }

    public class FileNamingConvention
    {
        public string Pattern { get; set; }
        public List<string> Examples { get; set; }
        public Dictionary<string, string> FieldDefinitions { get; set; }
    }

    public class HardwareRequirements
    {
        public HardwareSpec MinimumSpec { get; set; }
        public HardwareSpec RecommendedSpec { get; set; }
    }

    public class HardwareSpec
    {
        public string CPU { get; set; }
        public string RAM { get; set; }
        public string GPU { get; set; }
        public string Storage { get; set; }
        public string Display { get; set; }
    }

    public class LODMatrixEntry
    {
        public string ElementType { get; set; }
        public string Discipline { get; set; }
        public string ConceptLOD { get; set; }
        public string SchematicLOD { get; set; }
        public string DDevelLOD { get; set; }
        public string ConstructionLOD { get; set; }
        public string AsBuiltLOD { get; set; }
        public Dictionary<string, string> LODSpecifications { get; set; }
    }

    public class DeliverableScheduleEntry
    {
        public string Deliverable { get; set; }
        public string Phase { get; set; }
        public DateTime DueDate { get; set; }
        public string ResponsibleParty { get; set; }
        public string Format { get; set; }
        public bool ReviewRequired { get; set; }
    }

    public class BEPQualityPlan
    {
        public List<QualityCheckpoint> QualityCheckpoints { get; set; }
        public string AuditFrequency { get; set; }
        public List<string> AuditCriteria { get; set; }
    }

    public class QualityCheckpoint
    {
        public string Name { get; set; }
        public string Trigger { get; set; }
        public List<string> Checks { get; set; }
    }

    public class BEPCollaborationProcedures
    {
        public ModelSharingProtocol ModelSharingProtocol { get; set; }
        public ClashResolutionProcess ClashResolutionProcess { get; set; }
        public CommunicationPlan CommunicationPlan { get; set; }
    }

    public class ModelSharingProtocol
    {
        public string ShareFrequency { get; set; }
        public string ShareDay { get; set; }
        public string ShareLocation { get; set; }
        public bool NotificationRequired { get; set; }
        public List<string> PreShareChecklist { get; set; }
    }

    public class ClashResolutionProcess
    {
        public string DetectionFrequency { get; set; }
        public string ReviewMeeting { get; set; }
        public Dictionary<string, int> ResolutionSLA { get; set; }
        public List<string> EscalationPath { get; set; }
    }

    public class CommunicationPlan
    {
        public string PrimaryChannel { get; set; }
        public string MeetingNotes { get; set; }
        public string IssueTracking { get; set; }
        public string ContactDirectory { get; set; }
    }

    public class BEPModelStandards
    {
        public ModelUnits Units { get; set; }
        public CoordinateSystem Coordinates { get; set; }
        public WorksetStrategy WorksetStrategy { get; set; }
        public ViewOrganization ViewOrganization { get; set; }
        public ParameterStandards ParameterStandards { get; set; }
    }

    public class ModelUnits
    {
        public string Length { get; set; }
        public string Area { get; set; }
        public string Volume { get; set; }
        public string Angle { get; set; }
    }

    public class CoordinateSystem
    {
        public string ProjectBasePoint { get; set; }
        public string SharedCoordinates { get; set; }
        public string TrueNorth { get; set; }
    }

    public class WorksetStrategy
    {
        public List<WorksetDefinition> StandardWorksets { get; set; }
        public string NamingConvention { get; set; }
    }

    public class WorksetDefinition
    {
        public string Name { get; set; }
        public string Purpose { get; set; }
    }

    public class ViewOrganization
    {
        public string BrowserStructure { get; set; }
        public string ViewNaming { get; set; }
        public string ViewTemplates { get; set; }
    }

    public class ParameterStandards
    {
        public string SharedParameterFile { get; set; }
        public string NamingPrefix { get; set; }
        public List<string> RequiredParameters { get; set; }
    }

    #endregion

    #region LOD Models

    public class LODSpecification
    {
        public string SpecId { get; set; }
        public string ProjectId { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<LODElement> Elements { get; set; }
        public Dictionary<string, string> PhaseRequirements { get; set; }
    }

    public class LODSpecificationRequest
    {
        public List<LODElement> Elements { get; set; }
        public Dictionary<string, string> PhaseRequirements { get; set; }
    }

    public class LODElement
    {
        public string Category { get; set; }
        public string ElementType { get; set; }
        public string LOD100 { get; set; }
        public string LOD200 { get; set; }
        public string LOD300 { get; set; }
        public string LOD350 { get; set; }
        public string LOD400 { get; set; }
        public string LOD500 { get; set; }
    }

    public class ElementLODStatus
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string CurrentLOD { get; set; }
    }

    public class LODComplianceReport
    {
        public string ReportId { get; set; }
        public string ProjectId { get; set; }
        public string Milestone { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<ElementComplianceStatus> ElementStatuses { get; set; }
        public double OverallCompliance { get; set; }
        public List<LODComplianceIssue> Issues { get; set; }
    }

    public class ElementComplianceStatus
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string CurrentLOD { get; set; }
        public string RequiredLOD { get; set; }
        public bool IsCompliant { get; set; }
    }

    public class LODComplianceIssue
    {
        public string ElementId { get; set; }
        public string Issue { get; set; }
        public string Severity { get; set; }
    }

    #endregion

    #region Advisory Models

    public class BIMRecommendation
    {
        public RecommendationCategory Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Priority Priority { get; set; }
        public List<string> Actions { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class ProjectAnalysis
    {
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<string> Strengths { get; set; }
        public List<string> Weaknesses { get; set; }
        public List<string> Opportunities { get; set; }
        public List<string> Threats { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class BIMAdvisoryEventArgs : EventArgs
    {
        public AdvisoryType Type { get; set; }
        public string Message { get; set; }
        public string ProjectId { get; set; }
        public List<string> Recommendations { get; set; }
    }

    #endregion

    #region Enums

    public enum ProjectStatus { Planning, Active, OnHold, Completed, Cancelled }
    public enum MilestoneStatus { Pending, InProgress, Completed, Delayed }
    public enum DeliverableStatus { NotStarted, InProgress, UnderReview, Approved, Superseded }
    public enum BEPStatus { Draft, UnderReview, Approved, Superseded }
    public enum StandardCategory { InformationManagement, AssetManagement, Security, Regional, DataExchange }
    public enum BestPracticeCategory { ProjectSetup, Modeling, Coordination, DataManagement, Quality, People }
    public enum ImpactLevel { Low, Medium, High, Critical }
    public enum GoalCategory { DesignCoordination, CostControl, ScheduleManagement, FacilityManagement, Sustainability, Quality }
    public enum Priority { Low, Medium, High, Critical }
    public enum RecommendationCategory { Planning, Organization, Technical, BestPractice, Compliance }
    public enum AdvisoryType { Information, Warning, Critical, Success }

    #endregion

    #endregion
}
