// StingBIM.AI.NLP.Consulting.BIMExecutionPlanGenerator
// Generates ISO 19650-2 compliant BIM Execution Plans (BEP) from natural language prompts.
// Covers Pre-Appointment BEP and Post-Appointment BEP with 17 sections, ~189 elements.
// Integrates with KnowledgeGraph for standards lookup and InferenceEngine for role recommendations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.NLP.Consulting
{
    /// <summary>
    /// Generates comprehensive BIM Execution Plans (BEP) per ISO 19650-2.
    /// Supports both Pre-Appointment and Post-Appointment BEPs.
    /// Extracts project parameters from natural language prompts and populates
    /// all 17 BEP sections with intelligent defaults and standards-referenced content.
    /// </summary>
    public class BIMExecutionPlanGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, ProjectTypeProfile> _projectProfiles;
        private readonly Dictionary<string, LODSpecification> _lodSpecifications;
        private readonly Dictionary<string, List<string>> _softwareRecommendations;
        private readonly Dictionary<string, NamingConvention> _namingConventions;

        public BIMExecutionPlanGenerator()
        {
            _projectProfiles = new Dictionary<string, ProjectTypeProfile>(StringComparer.OrdinalIgnoreCase);
            _lodSpecifications = new Dictionary<string, LODSpecification>(StringComparer.OrdinalIgnoreCase);
            _softwareRecommendations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _namingConventions = new Dictionary<string, NamingConvention>(StringComparer.OrdinalIgnoreCase);

            InitializeProjectProfiles();
            InitializeLODSpecifications();
            InitializeSoftwareRecommendations();
            InitializeNamingConventions();
        }

        #region Initialization

        private void InitializeProjectProfiles()
        {
            _projectProfiles["residential"] = new ProjectTypeProfile
            {
                TypeName = "Residential",
                TypicalPhases = new List<string> { "Concept", "Developed Design", "Technical Design", "Construction", "Handover" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Quantity Takeoff", "Energy Analysis", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "MEP", "Landscape", "Interior Design" },
                TypicalLOD = "LOD 300",
                RegulatoryBodies = new List<string> { "Local Planning Authority", "Building Control" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "MEP Model", "Site Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1", "ADA", "NEC 2023", "Local Building Code" }
            };

            _projectProfiles["commercial"] = new ProjectTypeProfile
            {
                TypeName = "Commercial",
                TypicalPhases = new List<string> { "Brief", "Concept", "Developed Design", "Technical Design", "Construction", "Handover", "In Use" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "4D Scheduling", "5D Cost Estimation", "Energy Analysis", "Facility Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Plumbing", "Fire Protection", "Facade", "Landscape", "Interior Design" },
                TypicalLOD = "LOD 350",
                RegulatoryBodies = new List<string> { "Local Planning Authority", "Building Control", "Fire Authority", "Environmental Agency" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "Mechanical Model", "Electrical Model", "Plumbing Model", "Fire Protection Model", "Facade Model", "Site Model", "Federated Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1", "ASHRAE 62.1", "ADA", "NFPA 13", "NEC 2023", "ASCE 7" }
            };

            _projectProfiles["healthcare"] = new ProjectTypeProfile
            {
                TypeName = "Healthcare",
                TypicalPhases = new List<string> { "Strategic Brief", "Concept", "Developed Design", "Technical Design", "Specialist Design", "Construction", "Commissioning", "Handover", "In Use" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "4D Scheduling", "5D Cost", "Energy Analysis", "Infection Control Analysis", "Patient Flow Simulation", "Facility Management", "Asset Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Plumbing", "Medical Gas", "Fire Protection", "Specialist Equipment", "Interior Design", "Landscape" },
                TypicalLOD = "LOD 400",
                RegulatoryBodies = new List<string> { "Health Authority", "Fire Authority", "Building Control", "Environmental Agency", "Infection Control Board" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "Mechanical Model", "Electrical Model", "Plumbing Model", "Medical Gas Model", "Fire Protection Model", "Equipment Model", "Federated Model", "COBie Data", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 170", "ASHRAE 90.1", "ADA", "NFPA 101", "NFPA 99", "FGI Guidelines", "NEC 2023", "ASCE 7" }
            };

            _projectProfiles["educational"] = new ProjectTypeProfile
            {
                TypeName = "Educational",
                TypicalPhases = new List<string> { "Brief", "Concept", "Developed Design", "Technical Design", "Construction", "Handover", "In Use" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "Energy Analysis", "Daylighting Analysis", "Acoustic Analysis", "Facility Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Plumbing", "Fire Protection", "Landscape", "Interior Design", "Acoustics" },
                TypicalLOD = "LOD 350",
                RegulatoryBodies = new List<string> { "Education Authority", "Building Control", "Fire Authority" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "MEP Model", "Site Model", "Federated Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1", "ASHRAE 62.1", "ADA", "ANSI S12.60 Acoustics", "NEC 2023" }
            };

            _projectProfiles["industrial"] = new ProjectTypeProfile
            {
                TypeName = "Industrial",
                TypicalPhases = new List<string> { "Feasibility", "Concept", "Detailed Design", "Procurement", "Construction", "Commissioning", "Handover" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "4D Scheduling", "5D Cost", "Process Integration", "Safety Analysis", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Process", "Fire Protection", "Civil" },
                TypicalLOD = "LOD 350",
                RegulatoryBodies = new List<string> { "HSE", "Building Control", "Fire Authority", "Environmental Agency" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "MEP Model", "Process Model", "Site/Civil Model", "Federated Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "OSHA", "NFPA 30", "NEC 2023", "ASCE 7" }
            };

            _projectProfiles["infrastructure"] = new ProjectTypeProfile
            {
                TypeName = "Infrastructure",
                TypicalPhases = new List<string> { "Strategic Definition", "Preparation & Brief", "Concept", "Spatial Coordination", "Technical Design", "Manufacturing & Construction", "Handover", "Operations" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "GIS Integration", "4D Scheduling", "5D Cost", "Environmental Impact", "Quantity Takeoff", "Asset Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Civil", "Structural", "Geotechnical", "Environmental", "Traffic", "Utilities", "Landscape" },
                TypicalLOD = "LOD 300",
                RegulatoryBodies = new List<string> { "Transport Authority", "Environmental Agency", "Planning Authority", "Utility Providers" },
                TypicalDeliverables = new List<string> { "Alignment Model", "Structural Model", "Utility Model", "Terrain Model", "GIS Dataset", "Federated Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "AASHTO", "Local Highway Standards", "Environmental Regs" }
            };

            _projectProfiles["hospitality"] = new ProjectTypeProfile
            {
                TypeName = "Hospitality",
                TypicalPhases = new List<string> { "Brief", "Concept", "Developed Design", "Technical Design", "FF&E Design", "Construction", "Fit-Out", "Handover" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "Interior Visualization", "Energy Analysis", "Facility Management", "Asset Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Plumbing", "Fire Protection", "Interior Design", "Kitchen Design", "Landscape" },
                TypicalLOD = "LOD 350",
                RegulatoryBodies = new List<string> { "Tourism Authority", "Building Control", "Fire Authority", "Health & Safety" },
                TypicalDeliverables = new List<string> { "Architectural Model", "Structural Model", "MEP Model", "Interior Model", "Kitchen Model", "Landscape Model", "Federated Model", "FF&E Schedule", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1", "ADA", "NFPA 101", "NEC 2023", "Local Fire Code" }
            };

            _projectProfiles["mixed-use"] = new ProjectTypeProfile
            {
                TypeName = "Mixed-Use",
                TypicalPhases = new List<string> { "Brief", "Concept", "Developed Design", "Technical Design", "Construction", "Handover", "In Use" },
                KeyBIMUses = new List<string> { "Design Authoring", "3D Coordination", "Clash Detection", "Quantity Takeoff", "4D Scheduling", "5D Cost", "Energy Analysis", "Vertical Transportation", "Facility Management", "Record Model" },
                TypicalDisciplines = new List<string> { "Architecture", "Structural", "Mechanical", "Electrical", "Plumbing", "Fire Protection", "Facade", "Landscape", "Interior Design", "Vertical Transportation" },
                TypicalLOD = "LOD 350",
                RegulatoryBodies = new List<string> { "Planning Authority", "Building Control", "Fire Authority", "Environmental Agency" },
                TypicalDeliverables = new List<string> { "Architectural Model (per use)", "Structural Model", "MEP Model", "Facade Model", "Podium Model", "Tower Model", "Federated Model", "As-Built Model" },
                StandardsRequired = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1", "ASHRAE 62.1", "ADA", "NFPA 13", "NEC 2023", "ASCE 7" }
            };
        }

        private void InitializeLODSpecifications()
        {
            _lodSpecifications["LOD 100"] = new LODSpecification
            {
                Level = "LOD 100",
                Name = "Conceptual",
                Description = "Overall building massing, area, height, volume, location, orientation",
                GeometricDetail = "Symbolic or generic representation",
                DataRequirements = "Approximate area, volume, cost/m²",
                TypicalPhase = "Concept Design",
                ModelUses = new List<string> { "Conceptual Analysis", "Cost per m²", "Phase Planning" }
            };

            _lodSpecifications["LOD 200"] = new LODSpecification
            {
                Level = "LOD 200",
                Name = "Schematic",
                Description = "Generalized elements with approximate quantities, size, shape, location, orientation",
                GeometricDetail = "Generic placeholders with approximate geometry",
                DataRequirements = "Approximate dimensions, system type, performance criteria",
                TypicalPhase = "Schematic Design",
                ModelUses = new List<string> { "Design Analysis", "Approximate Cost", "Code Review", "Coordination" }
            };

            _lodSpecifications["LOD 300"] = new LODSpecification
            {
                Level = "LOD 300",
                Name = "Detailed Design",
                Description = "Specific elements with precise quantity, size, shape, location, orientation",
                GeometricDetail = "Accurate geometry modeled as specific assemblies",
                DataRequirements = "Specific dimensions, materials, finishes, manufacturer generic",
                TypicalPhase = "Detailed / Technical Design",
                ModelUses = new List<string> { "3D Coordination", "Clash Detection", "Quantity Takeoff", "Code Compliance", "Energy Analysis" }
            };

            _lodSpecifications["LOD 350"] = new LODSpecification
            {
                Level = "LOD 350",
                Name = "Construction Documentation",
                Description = "Elements with detailing, connections, supports, and interface conditions",
                GeometricDetail = "Accurate geometry with connections, supports, and necessary clearances",
                DataRequirements = "Specific products, detailed specs, connection details, installation data",
                TypicalPhase = "Construction Documentation",
                ModelUses = new List<string> { "3D Coordination", "Clash Detection", "Fabrication", "4D Scheduling", "5D Cost", "Shop Drawings" }
            };

            _lodSpecifications["LOD 400"] = new LODSpecification
            {
                Level = "LOD 400",
                Name = "Fabrication / Assembly",
                Description = "Elements modeled with sufficient detail for fabrication and assembly",
                GeometricDetail = "Fabrication-ready geometry with exact tolerances",
                DataRequirements = "Fabrication data, assembly sequences, exact products, serial numbers",
                TypicalPhase = "Fabrication & Construction",
                ModelUses = new List<string> { "Fabrication", "Assembly", "Installation Sequencing", "QA/QC Verification" }
            };

            _lodSpecifications["LOD 500"] = new LODSpecification
            {
                Level = "LOD 500",
                Name = "As-Built / Record",
                Description = "Field-verified elements representing as-constructed conditions",
                GeometricDetail = "As-built geometry verified by survey or field measurement",
                DataRequirements = "Warranty data, O&M manuals, commissioning data, asset tags, replacement schedules",
                TypicalPhase = "Handover & Operations",
                ModelUses = new List<string> { "Facility Management", "Asset Management", "Space Management", "Maintenance Planning" }
            };
        }

        private void InitializeSoftwareRecommendations()
        {
            _softwareRecommendations["BIM Authoring"] = new List<string>
            {
                "Autodesk Revit 2025 (Architecture, Structure, MEP)",
                "Tekla Structures (Steel/Precast detailing)",
                "Archicad (Architecture alternative)",
                "Bentley OpenBuildings (Infrastructure)"
            };

            _softwareRecommendations["Coordination"] = new List<string>
            {
                "Autodesk Navisworks Manage (Clash Detection & 4D)",
                "Solibri Model Checker (Rule-based checking)",
                "BIMcollab (BCF Issue Management)",
                "Trimble Connect (Cloud coordination)"
            };

            _softwareRecommendations["CDE Platform"] = new List<string>
            {
                "Autodesk Construction Cloud (ACC)",
                "Bentley ProjectWise",
                "Aconex (Oracle)",
                "Trimble Connect",
                "SharePoint + BIM 360 (Hybrid)"
            };

            _softwareRecommendations["Analysis"] = new List<string>
            {
                "Autodesk Insight (Energy)",
                "IES VE (Energy & Daylighting)",
                "ETABS / SAP2000 (Structural)",
                "Trane TRACE 3D Plus (HVAC Load)",
                "Dialux (Lighting)"
            };

            _softwareRecommendations["Visualization"] = new List<string>
            {
                "Enscape (Real-time rendering)",
                "Twinmotion (Visualization)",
                "Lumion (Rendering & Animation)",
                "Unity Reflect (VR/AR)"
            };

            _softwareRecommendations["Cost & Scheduling"] = new List<string>
            {
                "CostX (5D QTO)",
                "Cubicost (QTO)",
                "Primavera P6 (Scheduling)",
                "Microsoft Project (Scheduling)",
                "Synchro (4D Simulation)"
            };

            _softwareRecommendations["FM / Asset Management"] = new List<string>
            {
                "Archibus (IWMS)",
                "FM:Systems",
                "Maximo (IBM)",
                "Planon",
                "COBie Export from Revit"
            };
        }

        private void InitializeNamingConventions()
        {
            _namingConventions["ISO 19650"] = new NamingConvention
            {
                Standard = "ISO 19650",
                FileFormat = "[Project]-[Originator]-[Volume/System]-[Level/Location]-[Type]-[Role]-[Classification]-[Number]",
                Example = "PRJ-ARC-ZZ-01-M3-A-0001",
                Fields = new List<NamingField>
                {
                    new NamingField { Name = "Project", Description = "Project code (2-6 chars)", Example = "PRJ" },
                    new NamingField { Name = "Originator", Description = "Organization code (3 chars)", Example = "ARC" },
                    new NamingField { Name = "Volume/System", Description = "Functional breakdown (2 chars)", Example = "ZZ" },
                    new NamingField { Name = "Level/Location", Description = "Spatial breakdown (2 chars)", Example = "01" },
                    new NamingField { Name = "Type", Description = "Information container type", Example = "M3 (3D Model)" },
                    new NamingField { Name = "Role", Description = "Discipline role code", Example = "A (Architectural)" },
                    new NamingField { Name = "Classification", Description = "Uniclass/OmniClass code", Example = "0001" },
                    new NamingField { Name = "Number", Description = "Sequential number", Example = "0001" }
                },
                StatusCodes = new Dictionary<string, string>
                {
                    ["S0"] = "Work In Progress",
                    ["S1"] = "Suitable for Coordination",
                    ["S2"] = "Suitable for Information",
                    ["S3"] = "Suitable for Review & Comment",
                    ["S4"] = "Suitable for Stage Approval",
                    ["S5"] = "Suitable for Costing",
                    ["S6"] = "Suitable for Construction",
                    ["S7"] = "Suitable for Manufacture",
                    ["A"] = "Approved (with or without comments)",
                    ["B"] = "Approved with comments - resubmit"
                },
                RevisionScheme = "P01, P02... (preliminary) → C01, C02... (contractual)"
            };

            _namingConventions["BS 1192"] = new NamingConvention
            {
                Standard = "BS 1192:2007+A2:2016",
                FileFormat = "[Project]-[Originator]-[Zone]-[Level]-[Type]-[Role]-[Number]",
                Example = "PRJ-ARC-ZZ-01-DR-A-0001",
                Fields = new List<NamingField>
                {
                    new NamingField { Name = "Project", Description = "Project code", Example = "PRJ" },
                    new NamingField { Name = "Originator", Description = "Company code", Example = "ARC" },
                    new NamingField { Name = "Zone", Description = "Spatial zone", Example = "ZZ (all zones)" },
                    new NamingField { Name = "Level", Description = "Floor level", Example = "01" },
                    new NamingField { Name = "Type", Description = "Document type (DR, M3, VS)", Example = "DR (Drawing)" },
                    new NamingField { Name = "Role", Description = "Discipline", Example = "A" },
                    new NamingField { Name = "Number", Description = "Sequential number", Example = "0001" }
                },
                StatusCodes = new Dictionary<string, string>
                {
                    ["S0"] = "Work In Progress",
                    ["S1"] = "Shared (non-contractual)",
                    ["S2"] = "Shared (contractual)",
                    ["S3"] = "Published (contractual)",
                    ["S4"] = "Archived"
                },
                RevisionScheme = "P01, P02... → C01, C02..."
            };
        }

        #endregion

        #region BEP Generation

        /// <summary>
        /// Generates a complete BIM Execution Plan from a natural language prompt.
        /// Extracts project parameters and populates all 17 ISO 19650-2 sections.
        /// </summary>
        public async Task<BIMExecutionPlan> GenerateBEPAsync(
            BEPRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating BEP for: {request.ProjectDescription}");

            var bep = new BIMExecutionPlan
            {
                GeneratedAt = DateTime.Now,
                BEPType = request.BEPType,
                Version = "1.0"
            };

            // Extract project parameters from prompt
            var projectParams = ExtractProjectParameters(request);

            // Get project profile
            var profile = GetProjectProfile(projectParams.ProjectType);

            // Generate all 17 sections
            bep.ProjectInformation = GenerateProjectInformation(request, projectParams);
            bep.BIMGoalsAndUses = GenerateBIMGoalsAndUses(request, projectParams, profile);
            bep.RolesAndResponsibilities = GenerateRolesAndResponsibilities(request, projectParams, profile);
            bep.LODRequirements = GenerateLODRequirements(request, projectParams, profile);
            bep.EIRAlignment = GenerateEIRAlignment(request, projectParams, profile);
            bep.CDEWorkflow = GenerateCDEWorkflow(request, projectParams);
            bep.InformationDeliveryPlan = GenerateInformationDeliveryPlan(request, projectParams, profile);
            bep.NamingConventions = GenerateNamingConventionsSection(request, projectParams);
            bep.CoordinationProcedures = GenerateCoordinationProcedures(request, projectParams, profile);
            bep.ClashDetectionStrategy = GenerateClashDetectionStrategy(request, projectParams, profile);
            bep.QualityAssurance = GenerateQualityAssurance(request, projectParams);
            bep.SoftwareAndTechnology = GenerateSoftwareAndTechnology(request, projectParams, profile);
            bep.ModelStructure = GenerateModelStructure(request, projectParams, profile);
            bep.InformationSecurity = GenerateInformationSecurity(request, projectParams);
            bep.RiskManagement = GenerateRiskManagement(request, projectParams);
            bep.StandardsCompliance = GenerateStandardsCompliance(request, projectParams, profile);
            bep.DeliverySchedule = GenerateDeliverySchedule(request, projectParams, profile);

            Logger.Info($"BEP generated: {bep.ProjectInformation.ProjectName} - {bep.BEPType}");
            return bep;
        }

        /// <summary>
        /// Formats a BEP as a readable text document.
        /// </summary>
        public string FormatBEPAsText(BIMExecutionPlan bep)
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  BIM EXECUTION PLAN ({bep.BEPType})");
            sb.AppendLine($"  {bep.ProjectInformation.ProjectName}");
            sb.AppendLine($"  Generated: {bep.GeneratedAt:yyyy-MM-dd}  |  Version: {bep.Version}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // Section 1: Project Information
            sb.AppendLine("1. PROJECT INFORMATION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Project Name:     {bep.ProjectInformation.ProjectName}");
            sb.AppendLine($"  Project Number:   {bep.ProjectInformation.ProjectNumber}");
            sb.AppendLine($"  Project Type:     {bep.ProjectInformation.ProjectType}");
            sb.AppendLine($"  Location:         {bep.ProjectInformation.Location}");
            sb.AppendLine($"  Client:           {bep.ProjectInformation.ClientName}");
            sb.AppendLine($"  Gross Floor Area: {bep.ProjectInformation.GrossFloorArea}");
            sb.AppendLine($"  Stories:          {bep.ProjectInformation.NumberOfStories}");
            sb.AppendLine($"  Start Date:       {bep.ProjectInformation.StartDate}");
            sb.AppendLine($"  Completion:       {bep.ProjectInformation.CompletionDate}");
            sb.AppendLine($"  Budget Range:     {bep.ProjectInformation.BudgetRange}");
            sb.AppendLine($"  Description:      {bep.ProjectInformation.Description}");
            sb.AppendLine();

            // Section 2: BIM Goals & Uses
            sb.AppendLine("2. BIM GOALS & USES");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Strategic Goals:");
            foreach (var goal in bep.BIMGoalsAndUses.StrategicGoals)
                sb.AppendLine($"    • {goal}");
            sb.AppendLine();
            sb.AppendLine("  BIM Uses by Phase:");
            foreach (var phase in bep.BIMGoalsAndUses.BIMUsesByPhase)
            {
                sb.AppendLine($"    [{phase.Key}]");
                foreach (var use in phase.Value)
                    sb.AppendLine($"      - {use}");
            }
            sb.AppendLine();
            sb.AppendLine("  Key Performance Indicators:");
            foreach (var kpi in bep.BIMGoalsAndUses.KPIs)
                sb.AppendLine($"    • {kpi}");
            sb.AppendLine();

            // Section 3: Roles & Responsibilities
            sb.AppendLine("3. ROLES & RESPONSIBILITIES (RACI Matrix)");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var role in bep.RolesAndResponsibilities.Roles)
            {
                sb.AppendLine($"  {role.RoleName} ({role.Organization})");
                sb.AppendLine($"    Responsibilities:");
                foreach (var resp in role.Responsibilities)
                    sb.AppendLine($"      - {resp}");
                sb.AppendLine();
            }
            sb.AppendLine("  RACI Matrix (R=Responsible, A=Accountable, C=Consulted, I=Informed):");
            sb.AppendLine($"  {"Activity",-40} {"BIM Mgr",-8} {"Lead",-8} {"Coord",-8} {"Author",-8}");
            sb.AppendLine($"  {"────────────────────────────────────────",-40} {"───────",-8} {"───────",-8} {"───────",-8} {"───────",-8}");
            foreach (var entry in bep.RolesAndResponsibilities.RACIMatrix)
                sb.AppendLine($"  {entry.Activity,-40} {entry.BIMManager,-8} {entry.LeadDesigner,-8} {entry.Coordinator,-8} {entry.ModelAuthor,-8}");
            sb.AppendLine();

            // Section 4: LOD Requirements
            sb.AppendLine("4. LEVEL OF DEVELOPMENT (LOD) REQUIREMENTS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var lod in bep.LODRequirements.LODByPhase)
            {
                sb.AppendLine($"  Phase: {lod.Phase}");
                sb.AppendLine($"    Target LOD: {lod.TargetLOD}");
                sb.AppendLine($"    Elements:");
                foreach (var elem in lod.ElementRequirements)
                    sb.AppendLine($"      - {elem.Key}: {elem.Value}");
                sb.AppendLine();
            }
            sb.AppendLine("  LOD Matrix (Element Categories):");
            foreach (var cat in bep.LODRequirements.LODMatrix)
                sb.AppendLine($"    {cat.Key,-25}: {cat.Value}");
            sb.AppendLine();

            // Section 5: EIR Alignment
            sb.AppendLine("5. EMPLOYER'S INFORMATION REQUIREMENTS (EIR) ALIGNMENT");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Technical Requirements:");
            foreach (var req in bep.EIRAlignment.TechnicalRequirements)
                sb.AppendLine($"    • {req}");
            sb.AppendLine("  Management Requirements:");
            foreach (var req in bep.EIRAlignment.ManagementRequirements)
                sb.AppendLine($"    • {req}");
            sb.AppendLine("  Commercial Requirements:");
            foreach (var req in bep.EIRAlignment.CommercialRequirements)
                sb.AppendLine($"    • {req}");
            sb.AppendLine();

            // Section 6: CDE Workflow
            sb.AppendLine("6. COMMON DATA ENVIRONMENT (CDE) WORKFLOW");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Platform: {bep.CDEWorkflow.Platform}");
            sb.AppendLine($"  CDE States: {string.Join(" → ", bep.CDEWorkflow.States)}");
            sb.AppendLine("  Access Matrix:");
            foreach (var access in bep.CDEWorkflow.AccessMatrix)
                sb.AppendLine($"    {access.Role,-25}: {access.Permissions}");
            sb.AppendLine("  Review/Approval Process:");
            foreach (var step in bep.CDEWorkflow.ApprovalProcess)
                sb.AppendLine($"    {step.StepNumber}. {step.Description} (Owner: {step.Owner}, SLA: {step.SLA})");
            sb.AppendLine();

            // Section 7: Information Delivery Plan
            sb.AppendLine("7. MASTER INFORMATION DELIVERY PLAN (MIDP)");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var milestone in bep.InformationDeliveryPlan.Milestones)
            {
                sb.AppendLine($"  Milestone: {milestone.Name} ({milestone.Date})");
                sb.AppendLine($"    Deliverables:");
                foreach (var del in milestone.Deliverables)
                    sb.AppendLine($"      - {del}");
                sb.AppendLine();
            }

            // Section 8: Naming Conventions
            sb.AppendLine("8. NAMING CONVENTIONS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Standard: {bep.NamingConventions.Standard}");
            sb.AppendLine($"  File Format: {bep.NamingConventions.FileFormat}");
            sb.AppendLine($"  Example: {bep.NamingConventions.Example}");
            sb.AppendLine("  Status Codes:");
            foreach (var code in bep.NamingConventions.StatusCodes)
                sb.AppendLine($"    {code.Key} = {code.Value}");
            sb.AppendLine($"  Revision Scheme: {bep.NamingConventions.RevisionScheme}");
            sb.AppendLine();

            // Section 9: Coordination Procedures
            sb.AppendLine("9. COORDINATION PROCEDURES");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Meeting Frequency: {bep.CoordinationProcedures.MeetingFrequency}");
            sb.AppendLine("  Coordination Workflow:");
            foreach (var step in bep.CoordinationProcedures.WorkflowSteps)
                sb.AppendLine($"    {step.StepNumber}. {step.Description}");
            sb.AppendLine("  Model Federation Strategy:");
            foreach (var rule in bep.CoordinationProcedures.FederationRules)
                sb.AppendLine($"    • {rule}");
            sb.AppendLine();

            // Section 10: Clash Detection Strategy
            sb.AppendLine("10. CLASH DETECTION STRATEGY");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Clash Detection Matrix:");
            foreach (var test in bep.ClashDetectionStrategy.ClashTests)
            {
                sb.AppendLine($"    Test: {test.TestName}");
                sb.AppendLine($"      Discipline A: {test.DisciplineA} vs Discipline B: {test.DisciplineB}");
                sb.AppendLine($"      Tolerance: {test.Tolerance} | Priority: {test.Priority}");
                sb.AppendLine($"      Frequency: {test.Frequency}");
            }
            sb.AppendLine($"  Resolution Workflow: {bep.ClashDetectionStrategy.ResolutionWorkflow}");
            sb.AppendLine($"  Reporting Tool: {bep.ClashDetectionStrategy.ReportingTool}");
            sb.AppendLine();

            // Section 11: Quality Assurance
            sb.AppendLine("11. QUALITY ASSURANCE & QUALITY CONTROL");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  QA Checks:");
            foreach (var check in bep.QualityAssurance.QAChecks)
                sb.AppendLine($"    • {check.CheckName}: {check.Description} (Frequency: {check.Frequency})");
            sb.AppendLine("  Model Audit Criteria:");
            foreach (var criterion in bep.QualityAssurance.AuditCriteria)
                sb.AppendLine($"    • {criterion}");
            sb.AppendLine();

            // Section 12: Software & Technology
            sb.AppendLine("12. SOFTWARE & TECHNOLOGY STACK");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var cat in bep.SoftwareAndTechnology.SoftwareByCategory)
            {
                sb.AppendLine($"  {cat.Key}:");
                foreach (var sw in cat.Value)
                    sb.AppendLine($"    - {sw}");
            }
            sb.AppendLine($"  Exchange Formats: {string.Join(", ", bep.SoftwareAndTechnology.ExchangeFormats)}");
            sb.AppendLine($"  Interoperability Protocol: {bep.SoftwareAndTechnology.InteroperabilityProtocol}");
            sb.AppendLine();

            // Section 13: Model Structure
            sb.AppendLine("13. MODEL STRUCTURE & BREAKDOWN");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Model Breakdown:");
            foreach (var model in bep.ModelStructure.Models)
                sb.AppendLine($"    • {model.ModelName} ({model.Discipline}) - {model.Description}");
            sb.AppendLine("  Shared Coordinates Strategy:");
            sb.AppendLine($"    Origin: {bep.ModelStructure.SharedCoordinatesOrigin}");
            sb.AppendLine($"    Survey Point: {bep.ModelStructure.SurveyPointStrategy}");
            sb.AppendLine("  Workset Organization:");
            foreach (var ws in bep.ModelStructure.WorksetStrategy)
                sb.AppendLine($"    - {ws}");
            sb.AppendLine();

            // Section 14: Information Security
            sb.AppendLine("14. INFORMATION SECURITY");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Classification: {bep.InformationSecurity.Classification}");
            sb.AppendLine("  Access Controls:");
            foreach (var ctrl in bep.InformationSecurity.AccessControls)
                sb.AppendLine($"    • {ctrl}");
            sb.AppendLine("  Data Protection:");
            foreach (var measure in bep.InformationSecurity.DataProtectionMeasures)
                sb.AppendLine($"    • {measure}");
            sb.AppendLine();

            // Section 15: Risk Management
            sb.AppendLine("15. BIM RISK MANAGEMENT");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var risk in bep.RiskManagement.Risks)
            {
                sb.AppendLine($"  Risk: {risk.Description}");
                sb.AppendLine($"    Likelihood: {risk.Likelihood} | Impact: {risk.Impact} | Rating: {risk.Rating}");
                sb.AppendLine($"    Mitigation: {risk.Mitigation}");
                sb.AppendLine();
            }

            // Section 16: Standards Compliance
            sb.AppendLine("16. STANDARDS COMPLIANCE");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Applicable Standards:");
            foreach (var std in bep.StandardsCompliance.ApplicableStandards)
                sb.AppendLine($"    • {std}");
            sb.AppendLine("  Classification System:");
            sb.AppendLine($"    {bep.StandardsCompliance.ClassificationSystem}");
            sb.AppendLine("  Regional Requirements:");
            foreach (var req in bep.StandardsCompliance.RegionalRequirements)
                sb.AppendLine($"    • {req}");
            sb.AppendLine();

            // Section 17: Delivery Schedule
            sb.AppendLine("17. DELIVERY SCHEDULE");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var phase in bep.DeliverySchedule.Phases)
            {
                sb.AppendLine($"  {phase.PhaseName}:");
                sb.AppendLine($"    Duration: {phase.Duration}");
                sb.AppendLine($"    Key Deliverables: {string.Join(", ", phase.KeyDeliverables)}");
                sb.AppendLine($"    LOD Target: {phase.LODTarget}");
            }
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  Document generated by StingBIM AI BIM Consultant Engine");
            sb.AppendLine($"  ISO 19650-2 compliant | {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        #endregion

        #region Parameter Extraction

        private ProjectParameters ExtractProjectParameters(BEPRequest request)
        {
            var desc = request.ProjectDescription?.ToLowerInvariant() ?? "";
            var parms = new ProjectParameters();

            // Extract project type
            if (desc.Contains("hospital") || desc.Contains("clinic") || desc.Contains("healthcare") || desc.Contains("medical"))
                parms.ProjectType = "healthcare";
            else if (desc.Contains("school") || desc.Contains("university") || desc.Contains("education") || desc.Contains("campus"))
                parms.ProjectType = "educational";
            else if (desc.Contains("hotel") || desc.Contains("resort") || desc.Contains("hospitality"))
                parms.ProjectType = "hospitality";
            else if (desc.Contains("warehouse") || desc.Contains("factory") || desc.Contains("industrial") || desc.Contains("manufacturing"))
                parms.ProjectType = "industrial";
            else if (desc.Contains("bridge") || desc.Contains("road") || desc.Contains("highway") || desc.Contains("infrastructure"))
                parms.ProjectType = "infrastructure";
            else if (desc.Contains("mixed") || desc.Contains("mixed-use") || desc.Contains("multi-use"))
                parms.ProjectType = "mixed-use";
            else if (desc.Contains("office") || desc.Contains("commercial") || desc.Contains("retail") || desc.Contains("shopping"))
                parms.ProjectType = "commercial";
            else if (desc.Contains("house") || desc.Contains("residential") || desc.Contains("apartment") || desc.Contains("housing") || desc.Contains("flat") || desc.Contains("villa") || desc.Contains("condo"))
                parms.ProjectType = "residential";
            else
                parms.ProjectType = "commercial"; // default

            // Override with explicit request if provided
            if (!string.IsNullOrEmpty(request.ProjectType))
                parms.ProjectType = request.ProjectType.ToLowerInvariant();

            // Extract scale
            if (desc.Contains("large") || desc.Contains("tower") || desc.Contains("high-rise") || desc.Contains("highrise"))
                parms.Scale = "Large";
            else if (desc.Contains("small") || desc.Contains("single") || desc.Contains("cottage"))
                parms.Scale = "Small";
            else if (desc.Contains("medium") || desc.Contains("mid-rise"))
                parms.Scale = "Medium";
            else
                parms.Scale = "Medium";

            // Extract story count
            var storyMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+)\s*(?:stor(?:y|ies|ey)|floor|level)");
            if (storyMatch.Success)
                parms.Stories = int.Parse(storyMatch.Groups[1].Value);
            else
                parms.Stories = parms.Scale == "Large" ? 20 : parms.Scale == "Medium" ? 5 : 2;

            // Extract area
            var areaMatch = System.Text.RegularExpressions.Regex.Match(desc, @"(\d[\d,]*)\s*(?:m2|m²|sqm|square met|sq\.?\s*m)");
            if (areaMatch.Success)
                parms.AreaM2 = double.Parse(areaMatch.Groups[1].Value.Replace(",", ""));
            else
                parms.AreaM2 = parms.Scale == "Large" ? 50000 : parms.Scale == "Medium" ? 10000 : 2000;

            // Extract location / region
            if (desc.Contains("kenya") || desc.Contains("nairobi"))
                parms.Region = "Kenya";
            else if (desc.Contains("uganda") || desc.Contains("kampala"))
                parms.Region = "Uganda";
            else if (desc.Contains("tanzania") || desc.Contains("dar es salaam"))
                parms.Region = "Tanzania";
            else if (desc.Contains("rwanda") || desc.Contains("kigali"))
                parms.Region = "Rwanda";
            else if (desc.Contains("south africa") || desc.Contains("johannesburg") || desc.Contains("cape town"))
                parms.Region = "South Africa";
            else if (desc.Contains("nigeria") || desc.Contains("lagos"))
                parms.Region = "Nigeria";
            else if (desc.Contains("uk") || desc.Contains("london") || desc.Contains("britain"))
                parms.Region = "United Kingdom";
            else if (desc.Contains("africa"))
                parms.Region = "East Africa";
            else
                parms.Region = request.Region ?? "International";

            // Extract LOD level if specified
            var lodMatch = System.Text.RegularExpressions.Regex.Match(desc, @"lod\s*(\d{3})");
            if (lodMatch.Success)
                parms.RequestedLOD = $"LOD {lodMatch.Groups[1].Value}";

            // Extract project name
            parms.ProjectName = request.ProjectName ?? ExtractProjectName(desc, parms.ProjectType);

            // Extract team size
            if (desc.Contains("small team") || desc.Contains("few"))
                parms.TeamSize = "Small (3-5)";
            else if (desc.Contains("large team") || desc.Contains("many discipline"))
                parms.TeamSize = "Large (15+)";
            else
                parms.TeamSize = parms.Scale == "Large" ? "Large (15+)" : "Medium (6-14)";

            return parms;
        }

        private string ExtractProjectName(string desc, string projectType)
        {
            var words = desc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 3)
            {
                var name = string.Join(" ", words.Take(Math.Min(5, words.Length)));
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
            }
            return $"New {System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(projectType)} Project";
        }

        private ProjectTypeProfile GetProjectProfile(string projectType)
        {
            if (_projectProfiles.TryGetValue(projectType, out var profile))
                return profile;
            return _projectProfiles["commercial"];
        }

        #endregion

        #region Section Generators

        private BEPProjectInformation GenerateProjectInformation(BEPRequest request, ProjectParameters parms)
        {
            return new BEPProjectInformation
            {
                ProjectName = parms.ProjectName,
                ProjectNumber = request.ProjectNumber ?? $"PRJ-{DateTime.Now:yyyyMM}-001",
                ProjectType = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(parms.ProjectType),
                Location = parms.Region,
                ClientName = request.ClientName ?? "[Client Name]",
                GrossFloorArea = $"{parms.AreaM2:N0} m²",
                NumberOfStories = parms.Stories.ToString(),
                StartDate = request.StartDate ?? DateTime.Now.AddMonths(1).ToString("yyyy-MM"),
                CompletionDate = request.CompletionDate ?? DateTime.Now.AddYears(parms.Scale == "Large" ? 3 : 2).ToString("yyyy-MM"),
                BudgetRange = EstimateBudget(parms),
                Description = request.ProjectDescription ?? $"A {parms.Scale.ToLower()}-scale {parms.ProjectType} development"
            };
        }

        private string EstimateBudget(ProjectParameters parms)
        {
            double costPerM2 = parms.ProjectType switch
            {
                "healthcare" => 3500,
                "educational" => 2200,
                "commercial" => 2800,
                "residential" => 1800,
                "hospitality" => 3200,
                "industrial" => 1500,
                "infrastructure" => 2000,
                "mixed-use" => 2600,
                _ => 2500
            };

            if (parms.Region == "East Africa" || parms.Region == "Kenya" || parms.Region == "Uganda" ||
                parms.Region == "Tanzania" || parms.Region == "Rwanda")
                costPerM2 *= 0.4; // Africa regional adjustment

            if (parms.Region == "South Africa" || parms.Region == "Nigeria")
                costPerM2 *= 0.55;

            var totalLow = parms.AreaM2 * costPerM2 * 0.85;
            var totalHigh = parms.AreaM2 * costPerM2 * 1.15;

            return $"${totalLow / 1_000_000:F1}M - ${totalHigh / 1_000_000:F1}M (estimated)";
        }

        private BEPGoalsAndUses GenerateBIMGoalsAndUses(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPGoalsAndUses
            {
                StrategicGoals = new List<string>
                {
                    "Improve design coordination and reduce RFIs by 40% through clash detection",
                    "Enable accurate quantity takeoff and cost estimation within 5% of actual",
                    "Deliver ISO 19650-compliant information management throughout project lifecycle",
                    "Facilitate handover of asset information for facility management",
                    $"Achieve code compliance with {string.Join(", ", profile.StandardsRequired.Take(3))}"
                },
                KPIs = new List<string>
                {
                    "< 50 unresolved clashes at each design stage gate",
                    "> 95% model elements at target LOD for each phase",
                    "100% compliance with naming convention and CDE workflow",
                    "< 5% variance between BIM quantity takeoff and final quantities",
                    "All models federated and coordinated before each design review"
                }
            };

            // BIM uses by phase
            result.BIMUsesByPhase = new Dictionary<string, List<string>>();

            foreach (var phase in profile.TypicalPhases)
            {
                var uses = new List<string>();
                if (phase.Contains("Concept") || phase.Contains("Brief"))
                {
                    uses.AddRange(new[] { "Design Authoring (LOD 100-200)", "Massing Studies", "Site Analysis", "Preliminary Cost Estimation" });
                }
                else if (phase.Contains("Developed") || phase.Contains("Schematic"))
                {
                    uses.AddRange(new[] { "Design Authoring (LOD 200-300)", "3D Coordination", "Preliminary Clash Detection", "Energy Analysis", "Code Review" });
                }
                else if (phase.Contains("Technical") || phase.Contains("Detailed"))
                {
                    uses.AddRange(new[] { "Design Authoring (LOD 300-350)", "Full Clash Detection", "Quantity Takeoff", "4D Scheduling", "Compliance Checking", "Shop Drawing Generation" });
                }
                else if (phase.Contains("Construction") || phase.Contains("Manufacturing"))
                {
                    uses.AddRange(new[] { "Construction Sequencing (4D)", "Cost Tracking (5D)", "RFI Management", "Field BIM", "Progress Monitoring", "As-Built Updates" });
                }
                else if (phase.Contains("Handover") || phase.Contains("Commissioning"))
                {
                    uses.AddRange(new[] { "As-Built Verification (LOD 500)", "COBie Data Export", "O&M Manual Linking", "Asset Registration", "Record Model Delivery" });
                }
                else if (phase.Contains("In Use") || phase.Contains("Operations"))
                {
                    uses.AddRange(new[] { "Facility Management", "Space Management", "Maintenance Planning", "Asset Tracking", "Energy Monitoring" });
                }
                result.BIMUsesByPhase[phase] = uses;
            }

            return result;
        }

        private BEPRolesAndResponsibilities GenerateRolesAndResponsibilities(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPRolesAndResponsibilities();

            result.Roles = new List<BEPRole>
            {
                new BEPRole
                {
                    RoleName = "BIM Manager (Appointing Party)",
                    Organization = request.ClientName ?? "[Client/PM Organization]",
                    Responsibilities = new List<string>
                    {
                        "Define and maintain EIR (Employer's Information Requirements)",
                        "Approve BEP and MIDP submissions",
                        "Chair BIM coordination meetings",
                        "Oversee CDE compliance and information exchange",
                        "Review clash detection reports and sign off on resolutions",
                        "Validate LOD compliance at stage gates"
                    }
                },
                new BEPRole
                {
                    RoleName = "Lead Appointed Party (Lead Designer)",
                    Organization = "[Lead Design Organization]",
                    Responsibilities = new List<string>
                    {
                        "Prepare and maintain the BEP",
                        "Coordinate Task Information Delivery Plans (TIDP) from all parties",
                        "Compile Master Information Delivery Plan (MIDP)",
                        "Manage model federation and clash detection processes",
                        "Ensure information is delivered per CDE workflow",
                        "Coordinate design across all disciplines"
                    }
                },
                new BEPRole
                {
                    RoleName = "BIM Coordinator",
                    Organization = "[Each Discipline Organization]",
                    Responsibilities = new List<string>
                    {
                        "Maintain discipline-specific BIM standards",
                        "Perform internal model audits and QA checks",
                        "Resolve discipline-level clashes",
                        "Prepare TIDP for their discipline",
                        "Ensure model content meets LOD requirements",
                        "Manage worksets, links, and shared coordinates"
                    }
                },
                new BEPRole
                {
                    RoleName = "Model Author",
                    Organization = "[Discipline Teams]",
                    Responsibilities = new List<string>
                    {
                        "Create and maintain model content per BEP standards",
                        "Follow naming conventions and model structure requirements",
                        "Perform self-checks before sharing models",
                        "Update models per coordination meeting actions",
                        "Ensure correct LOD for the current project phase"
                    }
                },
                new BEPRole
                {
                    RoleName = "Information Manager",
                    Organization = "[PM / Client Representative]",
                    Responsibilities = new List<string>
                    {
                        "Manage CDE access permissions and user accounts",
                        "Monitor CDE status and workflow compliance",
                        "Archive superseded information",
                        "Generate compliance reports",
                        "Manage information security protocols"
                    }
                }
            };

            result.RACIMatrix = new List<RACIEntry>
            {
                new RACIEntry { Activity = "BEP Preparation & Updates", BIMManager = "A", LeadDesigner = "R", Coordinator = "C", ModelAuthor = "I" },
                new RACIEntry { Activity = "EIR Definition", BIMManager = "R", LeadDesigner = "C", Coordinator = "I", ModelAuthor = "I" },
                new RACIEntry { Activity = "MIDP Compilation", BIMManager = "A", LeadDesigner = "R", Coordinator = "C", ModelAuthor = "I" },
                new RACIEntry { Activity = "TIDP Preparation", BIMManager = "I", LeadDesigner = "C", Coordinator = "R", ModelAuthor = "C" },
                new RACIEntry { Activity = "Model Authoring", BIMManager = "I", LeadDesigner = "I", Coordinator = "C", ModelAuthor = "R" },
                new RACIEntry { Activity = "Internal Model Audit", BIMManager = "I", LeadDesigner = "I", Coordinator = "R", ModelAuthor = "C" },
                new RACIEntry { Activity = "Clash Detection", BIMManager = "A", LeadDesigner = "R", Coordinator = "C", ModelAuthor = "I" },
                new RACIEntry { Activity = "Clash Resolution", BIMManager = "I", LeadDesigner = "C", Coordinator = "R", ModelAuthor = "R" },
                new RACIEntry { Activity = "Model Federation", BIMManager = "A", LeadDesigner = "R", Coordinator = "C", ModelAuthor = "I" },
                new RACIEntry { Activity = "Design Review Gate", BIMManager = "A", LeadDesigner = "R", Coordinator = "C", ModelAuthor = "I" },
                new RACIEntry { Activity = "CDE Management", BIMManager = "A", LeadDesigner = "C", Coordinator = "I", ModelAuthor = "I" },
                new RACIEntry { Activity = "COBie / Handover Data", BIMManager = "A", LeadDesigner = "C", Coordinator = "R", ModelAuthor = "R" },
                new RACIEntry { Activity = "As-Built Model Delivery", BIMManager = "A", LeadDesigner = "R", Coordinator = "R", ModelAuthor = "R" },
            };

            return result;
        }

        private BEPLODRequirements GenerateLODRequirements(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPLODRequirements();
            var targetLOD = parms.RequestedLOD ?? profile.TypicalLOD;

            result.LODByPhase = new List<LODPhaseRequirement>();
            foreach (var phase in profile.TypicalPhases)
            {
                var lodLevel = phase switch
                {
                    var p when p.Contains("Brief") || p.Contains("Strategic") => "LOD 100",
                    var p when p.Contains("Concept") => "LOD 200",
                    var p when p.Contains("Developed") || p.Contains("Schematic") => "LOD 300",
                    var p when p.Contains("Technical") || p.Contains("Detailed") => "LOD 350",
                    var p when p.Contains("Construction") || p.Contains("Manufacturing") => "LOD 400",
                    var p when p.Contains("Handover") || p.Contains("In Use") || p.Contains("Operations") => "LOD 500",
                    _ => "LOD 300"
                };

                result.LODByPhase.Add(new LODPhaseRequirement
                {
                    Phase = phase,
                    TargetLOD = lodLevel,
                    ElementRequirements = GetElementLODRequirements(lodLevel)
                });
            }

            // LOD matrix by element category
            result.LODMatrix = new Dictionary<string, string>
            {
                ["Walls (Exterior)"] = $"LOD 200 → LOD 300 → {targetLOD} → LOD 500",
                ["Walls (Interior)"] = $"LOD 100 → LOD 200 → {targetLOD} → LOD 500",
                ["Floors / Slabs"] = $"LOD 200 → LOD 300 → {targetLOD} → LOD 500",
                ["Roofs"] = $"LOD 200 → LOD 300 → {targetLOD} → LOD 500",
                ["Structural Columns"] = $"LOD 200 → LOD 300 → LOD 400 → LOD 500",
                ["Structural Beams"] = $"LOD 200 → LOD 300 → LOD 400 → LOD 500",
                ["Foundations"] = $"LOD 100 → LOD 200 → LOD 400 → LOD 500",
                ["Doors"] = $"LOD 200 → LOD 300 → {targetLOD} → LOD 500",
                ["Windows / Curtain Walls"] = $"LOD 200 → LOD 300 → {targetLOD} → LOD 500",
                ["HVAC Ductwork"] = $"LOD 100 → LOD 200 → LOD 350 → LOD 500",
                ["Piping Systems"] = $"LOD 100 → LOD 200 → LOD 350 → LOD 500",
                ["Electrical Systems"] = $"LOD 100 → LOD 200 → LOD 350 → LOD 500",
                ["Fire Protection"] = $"LOD 100 → LOD 200 → LOD 350 → LOD 500",
                ["Furniture / Equipment"] = $"LOD 100 → LOD 200 → LOD 300 → LOD 500",
                ["Site / Landscape"] = $"LOD 100 → LOD 200 → LOD 300 → LOD 500"
            };

            return result;
        }

        private Dictionary<string, string> GetElementLODRequirements(string lodLevel)
        {
            if (_lodSpecifications.TryGetValue(lodLevel, out var spec))
            {
                return new Dictionary<string, string>
                {
                    ["Geometric Detail"] = spec.GeometricDetail,
                    ["Data Requirements"] = spec.DataRequirements,
                    ["Model Uses"] = string.Join(", ", spec.ModelUses)
                };
            }
            return new Dictionary<string, string>();
        }

        private BEPEIRAlignment GenerateEIRAlignment(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            return new BEPEIRAlignment
            {
                TechnicalRequirements = new List<string>
                {
                    $"BIM authoring software: Autodesk Revit 2025 (primary)",
                    $"Target LOD: {parms.RequestedLOD ?? profile.TypicalLOD} at Technical Design stage",
                    $"File exchange formats: IFC 4.0 (openBIM), Native Revit (.rvt), PDF/DWG (2D)",
                    "Shared parameters: ISO 19650 compliant, project-specific shared parameter file",
                    "Coordinate system: Project Base Point aligned to survey datum",
                    "Units: Metric (mm for dimensions, m² for areas)",
                    "Model checking: Solibri or Navisworks validation before sharing"
                },
                ManagementRequirements = new List<string>
                {
                    "CDE platform per ISO 19650 with WIP/Shared/Published/Archive states",
                    "BIM coordination meetings: Weekly during design, bi-weekly during construction",
                    "Clash detection: Bi-weekly during detailed design, weekly pre-construction",
                    "Model audit: Monthly LOD and standards compliance checks",
                    "Information delivery: Per MIDP milestones and stage gates",
                    $"Naming convention: ISO 19650 file naming protocol",
                    "Change management: All model changes tracked via CDE revision history"
                },
                CommercialRequirements = new List<string>
                {
                    "BIM deliverables included in professional fee scope",
                    "LOD progression milestones linked to payment stages",
                    "As-built model (LOD 500) required before final payment release",
                    "COBie data drops at Developed Design, Construction, and Handover",
                    "Intellectual property: Models remain property of appointing party",
                    "License requirements: All software licenses provided by appointed parties"
                }
            };
        }

        private BEPCDEWorkflow GenerateCDEWorkflow(BEPRequest request, ProjectParameters parms)
        {
            return new BEPCDEWorkflow
            {
                Platform = "Autodesk Construction Cloud (ACC) or equivalent ISO 19650 CDE",
                States = new List<string>
                {
                    "Work In Progress (WIP)",
                    "Shared",
                    "Published",
                    "Archive"
                },
                AccessMatrix = new List<CDEAccessEntry>
                {
                    new CDEAccessEntry { Role = "BIM Manager", Permissions = "Full access to all states, user management, workflow configuration" },
                    new CDEAccessEntry { Role = "Lead Designer", Permissions = "Read/Write all WIP/Shared, Publish own discipline, Read Published" },
                    new CDEAccessEntry { Role = "BIM Coordinator", Permissions = "Read/Write own discipline WIP/Shared, Read other disciplines Shared/Published" },
                    new CDEAccessEntry { Role = "Model Author", Permissions = "Read/Write own discipline WIP, Read Shared/Published" },
                    new CDEAccessEntry { Role = "Client / Reviewer", Permissions = "Read Shared/Published, Comment capability" },
                    new CDEAccessEntry { Role = "Contractor", Permissions = "Read Published, Upload construction RFIs" }
                },
                ApprovalProcess = new List<CDEApprovalStep>
                {
                    new CDEApprovalStep { StepNumber = 1, Description = "Author completes model content and performs self-check", Owner = "Model Author", SLA = "Before sharing deadline" },
                    new CDEApprovalStep { StepNumber = 2, Description = "BIM Coordinator performs internal QA audit", Owner = "BIM Coordinator", SLA = "2 working days" },
                    new CDEApprovalStep { StepNumber = 3, Description = "Model moved from WIP to Shared (S1 status)", Owner = "BIM Coordinator", SLA = "Upon QA pass" },
                    new CDEApprovalStep { StepNumber = 4, Description = "Lead Designer reviews and coordinates cross-discipline", Owner = "Lead Designer", SLA = "3 working days" },
                    new CDEApprovalStep { StepNumber = 5, Description = "BIM Manager reviews for EIR compliance", Owner = "BIM Manager", SLA = "5 working days" },
                    new CDEApprovalStep { StepNumber = 6, Description = "Approved content moved to Published (S4+ status)", Owner = "BIM Manager", SLA = "Upon approval" },
                    new CDEApprovalStep { StepNumber = 7, Description = "Superseded content archived with full audit trail", Owner = "Information Manager", SLA = "Automatic" }
                }
            };
        }

        private BEPInformationDeliveryPlan GenerateInformationDeliveryPlan(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPInformationDeliveryPlan();
            result.Milestones = new List<BEPMilestone>();

            var startDate = DateTime.Now.AddMonths(1);
            var phaseIndex = 0;

            foreach (var phase in profile.TypicalPhases)
            {
                var monthsPerPhase = phase switch
                {
                    var p when p.Contains("Brief") || p.Contains("Strategic") => 1,
                    var p when p.Contains("Concept") => 2,
                    var p when p.Contains("Developed") || p.Contains("Schematic") => 3,
                    var p when p.Contains("Technical") || p.Contains("Detailed") || p.Contains("Specialist") => 4,
                    var p when p.Contains("Construction") || p.Contains("Manufacturing") || p.Contains("Procurement") => parms.Scale == "Large" ? 18 : 12,
                    var p when p.Contains("Commissioning") => 2,
                    var p when p.Contains("Handover") => 1,
                    var p when p.Contains("Fit-Out") || p.Contains("FF&E") => 3,
                    _ => 2
                };

                var milestoneDate = startDate.AddMonths(phaseIndex);
                result.Milestones.Add(new BEPMilestone
                {
                    Name = $"{phase} Stage Gate",
                    Date = milestoneDate.ToString("yyyy-MM"),
                    Deliverables = GetPhaseDeliverables(phase, profile)
                });

                phaseIndex += monthsPerPhase;
            }

            return result;
        }

        private List<string> GetPhaseDeliverables(string phase, ProjectTypeProfile profile)
        {
            if (phase.Contains("Concept") || phase.Contains("Brief"))
            {
                return new List<string>
                {
                    "Massing model (LOD 100)",
                    "Site analysis model",
                    "Area schedule",
                    "Preliminary cost estimate (cost/m²)",
                    "BEP (Pre-Appointment)"
                };
            }
            if (phase.Contains("Developed") || phase.Contains("Schematic"))
            {
                return new List<string>
                {
                    "Discipline models (LOD 200-300)",
                    "Federated model",
                    "Preliminary clash report",
                    "Design freeze confirmation",
                    "Quantity takeoff (preliminary)",
                    "Energy model (baseline)",
                    "COBie Data Drop 1"
                };
            }
            if (phase.Contains("Technical") || phase.Contains("Detailed"))
            {
                return new List<string>
                {
                    "Discipline models (LOD 300-350)",
                    "Full clash detection report (< 50 open clashes)",
                    "4D construction sequence model",
                    "5D cost model",
                    "Coordinated 2D drawing set (from BIM)",
                    "Specification linkage",
                    "Code compliance report",
                    "COBie Data Drop 2"
                };
            }
            if (phase.Contains("Construction") || phase.Contains("Manufacturing"))
            {
                return new List<string>
                {
                    "Fabrication models (LOD 400 where required)",
                    "Shop drawing coordination",
                    "Construction issue tracking",
                    "As-built model updates (ongoing)",
                    "Monthly progress model updates"
                };
            }
            if (phase.Contains("Handover") || phase.Contains("Commissioning"))
            {
                return new List<string>
                {
                    "As-built model (LOD 500)",
                    "Final COBie data export",
                    "O&M manuals linked to model elements",
                    "Asset register",
                    "Commissioning data attached to systems",
                    "Record model delivery"
                };
            }
            return new List<string> { $"Deliverables per {phase} requirements" };
        }

        private BEPNamingConventionsSection GenerateNamingConventionsSection(BEPRequest request, ProjectParameters parms)
        {
            var convention = _namingConventions["ISO 19650"];
            return new BEPNamingConventionsSection
            {
                Standard = convention.Standard,
                FileFormat = convention.FileFormat,
                Example = convention.Example,
                StatusCodes = convention.StatusCodes,
                RevisionScheme = convention.RevisionScheme,
                FieldDefinitions = convention.Fields.Select(f => $"{f.Name}: {f.Description} (e.g., {f.Example})").ToList()
            };
        }

        private BEPCoordinationProcedures GenerateCoordinationProcedures(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            return new BEPCoordinationProcedures
            {
                MeetingFrequency = "Weekly BIM coordination meetings during design, bi-weekly during construction",
                WorkflowSteps = new List<CoordinationStep>
                {
                    new CoordinationStep { StepNumber = 1, Description = "Each discipline updates their model and performs internal QA" },
                    new CoordinationStep { StepNumber = 2, Description = "Models shared to CDE Shared state by coordination deadline" },
                    new CoordinationStep { StepNumber = 3, Description = "Lead Designer federates all discipline models" },
                    new CoordinationStep { StepNumber = 4, Description = "Automated clash detection run on federated model" },
                    new CoordinationStep { StepNumber = 5, Description = "Clash report distributed via BCF format to responsible parties" },
                    new CoordinationStep { StepNumber = 6, Description = "BIM coordination meeting to review clashes and assign actions" },
                    new CoordinationStep { StepNumber = 7, Description = "Disciplines resolve assigned clashes and update models" },
                    new CoordinationStep { StepNumber = 8, Description = "Verification run confirms clash resolution" },
                    new CoordinationStep { StepNumber = 9, Description = "Resolved models promoted to Published state" }
                },
                FederationRules = new List<string>
                {
                    "All models use shared coordinates established from survey datum",
                    "Model federation frequency: Weekly during design phases",
                    "Each discipline provides a single consolidated model per building/zone",
                    "Link management: Use Revit linked models with shared coordinates",
                    "Model origin point: Consistent across all discipline models",
                    "Levels and grids: Established by architectural model, referenced by all disciplines"
                }
            };
        }

        private BEPClashDetectionStrategy GenerateClashDetectionStrategy(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPClashDetectionStrategy
            {
                ResolutionWorkflow = "BCF (BIM Collaboration Format) workflow: Detect → Report → Assign → Resolve → Verify → Close",
                ReportingTool = "Navisworks Manage / BIMcollab",
                ClashTests = new List<ClashTest>
                {
                    new ClashTest { TestName = "ARCH vs STRUCT", DisciplineA = "Architectural", DisciplineB = "Structural", Tolerance = "25mm", Priority = "High", Frequency = "Bi-weekly" },
                    new ClashTest { TestName = "ARCH vs MEP", DisciplineA = "Architectural", DisciplineB = "Mechanical", Tolerance = "25mm", Priority = "High", Frequency = "Bi-weekly" },
                    new ClashTest { TestName = "STRUCT vs MEP", DisciplineA = "Structural", DisciplineB = "Mechanical", Tolerance = "10mm", Priority = "Critical", Frequency = "Weekly" },
                    new ClashTest { TestName = "MECH vs ELEC", DisciplineA = "Mechanical", DisciplineB = "Electrical", Tolerance = "25mm", Priority = "High", Frequency = "Bi-weekly" },
                    new ClashTest { TestName = "MECH vs PLUMB", DisciplineA = "Mechanical", DisciplineB = "Plumbing", Tolerance = "25mm", Priority = "High", Frequency = "Bi-weekly" },
                    new ClashTest { TestName = "ELEC vs PLUMB", DisciplineA = "Electrical", DisciplineB = "Plumbing", Tolerance = "25mm", Priority = "Medium", Frequency = "Bi-weekly" },
                    new ClashTest { TestName = "MEP vs FIRE", DisciplineA = "All MEP", DisciplineB = "Fire Protection", Tolerance = "10mm", Priority = "Critical", Frequency = "Weekly" },
                    new ClashTest { TestName = "ALL vs Clearance", DisciplineA = "All Disciplines", DisciplineB = "Clearance Zones", Tolerance = "0mm", Priority = "Critical", Frequency = "Weekly" }
                }
            };

            // Add discipline-specific tests for healthcare
            if (parms.ProjectType == "healthcare")
            {
                result.ClashTests.Add(new ClashTest { TestName = "MEDGAS vs ALL", DisciplineA = "Medical Gas", DisciplineB = "All Systems", Tolerance = "10mm", Priority = "Critical", Frequency = "Weekly" });
                result.ClashTests.Add(new ClashTest { TestName = "EQUIP vs ALL", DisciplineA = "Medical Equipment", DisciplineB = "All Disciplines", Tolerance = "50mm", Priority = "High", Frequency = "Bi-weekly" });
            }

            return result;
        }

        private BEPQualityAssurance GenerateQualityAssurance(BEPRequest request, ProjectParameters parms)
        {
            return new BEPQualityAssurance
            {
                QAChecks = new List<QACheck>
                {
                    new QACheck { CheckName = "Model Standards Check", Description = "Verify naming conventions, shared parameters, and project standards compliance", Frequency = "Before each CDE share" },
                    new QACheck { CheckName = "LOD Compliance", Description = "Verify all elements meet the target LOD for current project phase", Frequency = "Monthly" },
                    new QACheck { CheckName = "Clash Detection", Description = "Run full inter-discipline clash detection on federated model", Frequency = "Bi-weekly (design), Weekly (pre-construction)" },
                    new QACheck { CheckName = "Code Compliance", Description = "Check model against applicable building codes and regulations", Frequency = "At each stage gate" },
                    new QACheck { CheckName = "Data Completeness", Description = "Verify required parameter data is populated for all elements", Frequency = "Before each data drop" },
                    new QACheck { CheckName = "Coordinate Alignment", Description = "Verify shared coordinates are consistent across all discipline models", Frequency = "After each model share" },
                    new QACheck { CheckName = "Model Performance", Description = "Check file sizes, warning counts, and model health metrics", Frequency = "Monthly" },
                    new QACheck { CheckName = "Visual Inspection", Description = "3D walkthrough review for anomalies and missing elements", Frequency = "Bi-weekly" }
                },
                AuditCriteria = new List<string>
                {
                    "All elements follow project naming convention",
                    "No unresolved Revit warnings > 100",
                    "File size within acceptable limits (< 300MB per discipline model)",
                    "All linked models current and properly referenced",
                    "All room/space elements properly bounded and tagged",
                    "Shared parameters populated per LOD requirements",
                    "No duplicate elements or overlapping geometry",
                    "Proper phase and workset assignment for all elements",
                    "Design option sets properly managed",
                    "View templates applied consistently"
                }
            };
        }

        private BEPSoftwareAndTechnology GenerateSoftwareAndTechnology(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPSoftwareAndTechnology
            {
                SoftwareByCategory = new Dictionary<string, List<string>>(),
                ExchangeFormats = new List<string> { "IFC 4.0 (ISO 16739-1)", "Native Revit (.rvt/.rfa)", "NWC/NWD (Navisworks)", "BCF (BIM Collaboration Format)", "COBie (spreadsheet/IFC)", "PDF (2D documentation)", "DWG/DXF (2D legacy)" },
                InteroperabilityProtocol = "IFC 4.0 as primary exchange format. Native formats for same-software disciplines. BCF for issue tracking. COBie for facility handover data."
            };

            foreach (var category in _softwareRecommendations)
            {
                result.SoftwareByCategory[category.Key] = new List<string>(category.Value);
            }

            return result;
        }

        private BEPModelStructure GenerateModelStructure(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var models = new List<BEPModel>();
            foreach (var discipline in profile.TypicalDisciplines)
            {
                models.Add(new BEPModel
                {
                    ModelName = $"{parms.ProjectName.Replace(" ", "")}-{GetDisciplineCode(discipline)}-ZZ-XX-M3",
                    Discipline = discipline,
                    Description = $"{discipline} design model"
                });
            }

            // Add federated model
            models.Add(new BEPModel
            {
                ModelName = $"{parms.ProjectName.Replace(" ", "")}-FED-ZZ-XX-M3",
                Discipline = "Federated",
                Description = "Federated coordination model (Navisworks)"
            });

            return new BEPModelStructure
            {
                Models = models,
                SharedCoordinatesOrigin = "Project Base Point at site survey datum (coordinates to be confirmed by surveyor)",
                SurveyPointStrategy = "True North aligned to site survey. Survey Point placed at known survey marker.",
                WorksetStrategy = new List<string>
                {
                    "Workset per major element category (Walls, Floors, Roofs, Structure, MEP, etc.)",
                    "Linked models on dedicated 'Link-[Discipline]' worksets",
                    "Shared levels and grids on 'Shared Levels and Grids' workset",
                    "Site elements on 'Site' workset",
                    "Interior elements separated from exterior where practical",
                    "Default workset set to discipline-specific primary workset"
                }
            };
        }

        private string GetDisciplineCode(string discipline)
        {
            return discipline.ToUpperInvariant() switch
            {
                "ARCHITECTURE" => "ARC",
                "STRUCTURAL" => "STR",
                "MECHANICAL" => "MEC",
                "ELECTRICAL" => "ELE",
                "PLUMBING" => "PLB",
                "FIRE PROTECTION" => "FPR",
                "LANDSCAPE" => "LND",
                "INTERIOR DESIGN" => "INT",
                "CIVIL" => "CIV",
                "FACADE" => "FAC",
                "GEOTECHNICAL" => "GEO",
                "MEDICAL GAS" => "MGS",
                "SPECIALIST EQUIPMENT" => "SPE",
                "ACOUSTICS" => "ACO",
                "KITCHEN DESIGN" => "KIT",
                "VERTICAL TRANSPORTATION" => "VTR",
                "PROCESS" => "PRC",
                "MEP" => "MEP",
                _ => discipline[..Math.Min(3, discipline.Length)].ToUpperInvariant()
            };
        }

        private BEPInformationSecurity GenerateInformationSecurity(BEPRequest request, ProjectParameters parms)
        {
            return new BEPInformationSecurity
            {
                Classification = "Confidential - Project Team Only",
                AccessControls = new List<string>
                {
                    "Role-based access control (RBAC) on CDE platform",
                    "Multi-factor authentication required for CDE access",
                    "IP-restricted access for sensitive project areas",
                    "Named user accounts only - no shared credentials",
                    "Access reviewed quarterly and upon team member departure",
                    "External party access via time-limited guest accounts"
                },
                DataProtectionMeasures = new List<string>
                {
                    "Encryption in transit (TLS 1.2+) and at rest (AES-256)",
                    "Automatic backup and versioning on CDE platform",
                    "No local storage of Published models outside CDE",
                    "USB/removable media restricted for model files",
                    "Screen sharing of models requires meeting host approval",
                    "Model data retention: 7 years post-project completion per ISO 19650",
                    "GDPR/data protection compliance for personal data in models"
                }
            };
        }

        private BEPRiskManagement GenerateRiskManagement(BEPRequest request, ProjectParameters parms)
        {
            return new BEPRiskManagement
            {
                Risks = new List<BEPRisk>
                {
                    new BEPRisk
                    {
                        Description = "Team members lack BIM skills or ISO 19650 knowledge",
                        Likelihood = "Medium", Impact = "High", Rating = "High",
                        Mitigation = "Mandatory BIM induction training, project-specific standards training, BIM support desk"
                    },
                    new BEPRisk
                    {
                        Description = "Software version incompatibility between disciplines",
                        Likelihood = "Medium", Impact = "Medium", Rating = "Medium",
                        Mitigation = "Standardize on agreed software versions per BEP, test interoperability at project start"
                    },
                    new BEPRisk
                    {
                        Description = "Model corruption or data loss",
                        Likelihood = "Low", Impact = "Critical", Rating = "High",
                        Mitigation = "CDE-based cloud storage with automatic versioning, daily backups, local backup copies weekly"
                    },
                    new BEPRisk
                    {
                        Description = "Unresolved clashes delaying construction",
                        Likelihood = "High", Impact = "High", Rating = "Critical",
                        Mitigation = "Bi-weekly clash detection, mandatory clash resolution before stage gates, escalation protocol"
                    },
                    new BEPRisk
                    {
                        Description = "Inadequate LOD at stage gates delaying approvals",
                        Likelihood = "Medium", Impact = "Medium", Rating = "Medium",
                        Mitigation = "Monthly LOD audits, early warning system, LOD compliance in MIDP milestones"
                    },
                    new BEPRisk
                    {
                        Description = "CDE downtime or access issues",
                        Likelihood = "Low", Impact = "High", Rating = "Medium",
                        Mitigation = "SLA with CDE provider (99.9% uptime), offline working protocol, contingency file sharing"
                    },
                    new BEPRisk
                    {
                        Description = "Information security breach - unauthorized model access",
                        Likelihood = "Low", Impact = "Critical", Rating = "High",
                        Mitigation = "MFA enforcement, role-based access, quarterly access review, incident response plan"
                    },
                    new BEPRisk
                    {
                        Description = "Handover data (COBie) incomplete or inaccurate",
                        Likelihood = "Medium", Impact = "High", Rating = "High",
                        Mitigation = "Incremental COBie data drops at design stages, automated validation tools, early FM team involvement"
                    }
                }
            };
        }

        private BEPStandardsCompliance GenerateStandardsCompliance(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPStandardsCompliance
            {
                ApplicableStandards = new List<string>(profile.StandardsRequired),
                ClassificationSystem = "Uniclass 2015 (primary) / OmniClass (secondary)",
                RegionalRequirements = new List<string>()
            };

            // Add regional standards
            switch (parms.Region)
            {
                case "Kenya":
                    result.RegionalRequirements.AddRange(new[] { "KEBS standards", "NCA regulations", "NEMA environmental requirements", "County building bylaws" });
                    result.ApplicableStandards.Add("EAS (East African Standards)");
                    break;
                case "Uganda":
                    result.RegionalRequirements.AddRange(new[] { "UNBS standards", "KCCA building regulations", "NEMA-UG environmental requirements" });
                    result.ApplicableStandards.Add("UNBS / EAS");
                    break;
                case "Tanzania":
                    result.RegionalRequirements.AddRange(new[] { "TBS standards", "NHC building regulations", "NEMC environmental requirements" });
                    result.ApplicableStandards.Add("TBS / EAS");
                    break;
                case "Rwanda":
                    result.RegionalRequirements.AddRange(new[] { "RSB standards", "Rwanda building code", "REMA environmental requirements" });
                    result.ApplicableStandards.Add("RSB / EAS");
                    break;
                case "South Africa":
                    result.RegionalRequirements.AddRange(new[] { "SANS 10400 (National Building Regulations)", "CIDB requirements", "SACAP regulations" });
                    result.ApplicableStandards.Add("SANS");
                    break;
                case "Nigeria":
                    result.RegionalRequirements.AddRange(new[] { "NIS standards", "National Building Code of Nigeria", "NESREA environmental requirements" });
                    result.ApplicableStandards.Add("ECOWAS / NIS");
                    break;
                case "United Kingdom":
                    result.RegionalRequirements.AddRange(new[] { "UK BIM Framework (ISO 19650 UK National Annex)", "Building Regulations Part L/M", "CDM Regulations" });
                    result.ApplicableStandards.Add("BS EN ISO 19650");
                    break;
                default:
                    result.RegionalRequirements.Add("Local building code and regulatory requirements to be confirmed");
                    break;
            }

            return result;
        }

        private BEPDeliverySchedule GenerateDeliverySchedule(BEPRequest request, ProjectParameters parms, ProjectTypeProfile profile)
        {
            var result = new BEPDeliverySchedule();
            result.Phases = new List<BEPPhase>();

            foreach (var phase in profile.TypicalPhases)
            {
                var duration = phase switch
                {
                    var p when p.Contains("Brief") || p.Contains("Strategic") => "4-6 weeks",
                    var p when p.Contains("Concept") => "6-8 weeks",
                    var p when p.Contains("Developed") || p.Contains("Schematic") => "8-12 weeks",
                    var p when p.Contains("Technical") || p.Contains("Detailed") => "12-16 weeks",
                    var p when p.Contains("Specialist") => "8-12 weeks",
                    var p when p.Contains("Construction") || p.Contains("Manufacturing") => parms.Scale == "Large" ? "18-24 months" : "12-18 months",
                    var p when p.Contains("Commissioning") => "4-8 weeks",
                    var p when p.Contains("Handover") => "4-6 weeks",
                    var p when p.Contains("Fit-Out") || p.Contains("FF&E") => "8-12 weeks",
                    var p when p.Contains("Procurement") => "8-12 weeks",
                    _ => "4-8 weeks"
                };

                var lodTarget = phase switch
                {
                    var p when p.Contains("Brief") || p.Contains("Strategic") => "LOD 100",
                    var p when p.Contains("Concept") => "LOD 200",
                    var p when p.Contains("Developed") || p.Contains("Schematic") => "LOD 300",
                    var p when p.Contains("Technical") || p.Contains("Detailed") || p.Contains("Specialist") => "LOD 350",
                    var p when p.Contains("Construction") || p.Contains("Manufacturing") || p.Contains("Fabrication") => "LOD 400",
                    var p when p.Contains("Handover") || p.Contains("In Use") => "LOD 500",
                    _ => "LOD 300"
                };

                result.Phases.Add(new BEPPhase
                {
                    PhaseName = phase,
                    Duration = duration,
                    LODTarget = lodTarget,
                    KeyDeliverables = GetPhaseDeliverables(phase, profile).Take(4).ToList()
                });
            }

            return result;
        }

        #endregion
    }

    #region Data Models

    public class BEPRequest
    {
        public string ProjectDescription { get; set; }
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ProjectType { get; set; }
        public string ClientName { get; set; }
        public string Region { get; set; }
        public string StartDate { get; set; }
        public string CompletionDate { get; set; }
        public string BEPType { get; set; } = "Pre-Appointment BEP";
    }

    public class BIMExecutionPlan
    {
        public DateTime GeneratedAt { get; set; }
        public string BEPType { get; set; }
        public string Version { get; set; }

        public BEPProjectInformation ProjectInformation { get; set; }
        public BEPGoalsAndUses BIMGoalsAndUses { get; set; }
        public BEPRolesAndResponsibilities RolesAndResponsibilities { get; set; }
        public BEPLODRequirements LODRequirements { get; set; }
        public BEPEIRAlignment EIRAlignment { get; set; }
        public BEPCDEWorkflow CDEWorkflow { get; set; }
        public BEPInformationDeliveryPlan InformationDeliveryPlan { get; set; }
        public BEPNamingConventionsSection NamingConventions { get; set; }
        public BEPCoordinationProcedures CoordinationProcedures { get; set; }
        public BEPClashDetectionStrategy ClashDetectionStrategy { get; set; }
        public BEPQualityAssurance QualityAssurance { get; set; }
        public BEPSoftwareAndTechnology SoftwareAndTechnology { get; set; }
        public BEPModelStructure ModelStructure { get; set; }
        public BEPInformationSecurity InformationSecurity { get; set; }
        public BEPRiskManagement RiskManagement { get; set; }
        public BEPStandardsCompliance StandardsCompliance { get; set; }
        public BEPDeliverySchedule DeliverySchedule { get; set; }
    }

    public class BEPProjectInformation
    {
        public string ProjectName { get; set; }
        public string ProjectNumber { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public string ClientName { get; set; }
        public string GrossFloorArea { get; set; }
        public string NumberOfStories { get; set; }
        public string StartDate { get; set; }
        public string CompletionDate { get; set; }
        public string BudgetRange { get; set; }
        public string Description { get; set; }
    }

    public class BEPGoalsAndUses
    {
        public List<string> StrategicGoals { get; set; } = new();
        public Dictionary<string, List<string>> BIMUsesByPhase { get; set; } = new();
        public List<string> KPIs { get; set; } = new();
    }

    public class BEPRolesAndResponsibilities
    {
        public List<BEPRole> Roles { get; set; } = new();
        public List<RACIEntry> RACIMatrix { get; set; } = new();
    }

    public class BEPRole
    {
        public string RoleName { get; set; }
        public string Organization { get; set; }
        public List<string> Responsibilities { get; set; } = new();
    }

    public class RACIEntry
    {
        public string Activity { get; set; }
        public string BIMManager { get; set; }
        public string LeadDesigner { get; set; }
        public string Coordinator { get; set; }
        public string ModelAuthor { get; set; }
    }

    public class BEPLODRequirements
    {
        public List<LODPhaseRequirement> LODByPhase { get; set; } = new();
        public Dictionary<string, string> LODMatrix { get; set; } = new();
    }

    public class LODPhaseRequirement
    {
        public string Phase { get; set; }
        public string TargetLOD { get; set; }
        public Dictionary<string, string> ElementRequirements { get; set; } = new();
    }

    public class BEPEIRAlignment
    {
        public List<string> TechnicalRequirements { get; set; } = new();
        public List<string> ManagementRequirements { get; set; } = new();
        public List<string> CommercialRequirements { get; set; } = new();
    }

    public class BEPCDEWorkflow
    {
        public string Platform { get; set; }
        public List<string> States { get; set; } = new();
        public List<CDEAccessEntry> AccessMatrix { get; set; } = new();
        public List<CDEApprovalStep> ApprovalProcess { get; set; } = new();
    }

    public class CDEAccessEntry
    {
        public string Role { get; set; }
        public string Permissions { get; set; }
    }

    public class CDEApprovalStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; }
        public string Owner { get; set; }
        public string SLA { get; set; }
    }

    public class BEPInformationDeliveryPlan
    {
        public List<BEPMilestone> Milestones { get; set; } = new();
    }

    public class BEPMilestone
    {
        public string Name { get; set; }
        public string Date { get; set; }
        public List<string> Deliverables { get; set; } = new();
    }

    public class BEPNamingConventionsSection
    {
        public string Standard { get; set; }
        public string FileFormat { get; set; }
        public string Example { get; set; }
        public Dictionary<string, string> StatusCodes { get; set; } = new();
        public string RevisionScheme { get; set; }
        public List<string> FieldDefinitions { get; set; } = new();
    }

    public class BEPCoordinationProcedures
    {
        public string MeetingFrequency { get; set; }
        public List<CoordinationStep> WorkflowSteps { get; set; } = new();
        public List<string> FederationRules { get; set; } = new();
    }

    public class CoordinationStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; }
    }

    public class BEPClashDetectionStrategy
    {
        public List<ClashTest> ClashTests { get; set; } = new();
        public string ResolutionWorkflow { get; set; }
        public string ReportingTool { get; set; }
    }

    public class ClashTest
    {
        public string TestName { get; set; }
        public string DisciplineA { get; set; }
        public string DisciplineB { get; set; }
        public string Tolerance { get; set; }
        public string Priority { get; set; }
        public string Frequency { get; set; }
    }

    public class BEPQualityAssurance
    {
        public List<QACheck> QAChecks { get; set; } = new();
        public List<string> AuditCriteria { get; set; } = new();
    }

    public class QACheck
    {
        public string CheckName { get; set; }
        public string Description { get; set; }
        public string Frequency { get; set; }
    }

    public class BEPSoftwareAndTechnology
    {
        public Dictionary<string, List<string>> SoftwareByCategory { get; set; } = new();
        public List<string> ExchangeFormats { get; set; } = new();
        public string InteroperabilityProtocol { get; set; }
    }

    public class BEPModelStructure
    {
        public List<BEPModel> Models { get; set; } = new();
        public string SharedCoordinatesOrigin { get; set; }
        public string SurveyPointStrategy { get; set; }
        public List<string> WorksetStrategy { get; set; } = new();
    }

    public class BEPModel
    {
        public string ModelName { get; set; }
        public string Discipline { get; set; }
        public string Description { get; set; }
    }

    public class BEPInformationSecurity
    {
        public string Classification { get; set; }
        public List<string> AccessControls { get; set; } = new();
        public List<string> DataProtectionMeasures { get; set; } = new();
    }

    public class BEPRiskManagement
    {
        public List<BEPRisk> Risks { get; set; } = new();
    }

    public class BEPRisk
    {
        public string Description { get; set; }
        public string Likelihood { get; set; }
        public string Impact { get; set; }
        public string Rating { get; set; }
        public string Mitigation { get; set; }
    }

    public class BEPStandardsCompliance
    {
        public List<string> ApplicableStandards { get; set; } = new();
        public string ClassificationSystem { get; set; }
        public List<string> RegionalRequirements { get; set; } = new();
    }

    public class BEPDeliverySchedule
    {
        public List<BEPPhase> Phases { get; set; } = new();
    }

    public class BEPPhase
    {
        public string PhaseName { get; set; }
        public string Duration { get; set; }
        public string LODTarget { get; set; }
        public List<string> KeyDeliverables { get; set; } = new();
    }

    // Internal helper models
    internal class ProjectParameters
    {
        public string ProjectType { get; set; }
        public string ProjectName { get; set; }
        public string Scale { get; set; }
        public int Stories { get; set; }
        public double AreaM2 { get; set; }
        public string Region { get; set; }
        public string RequestedLOD { get; set; }
        public string TeamSize { get; set; }
    }

    internal class ProjectTypeProfile
    {
        public string TypeName { get; set; }
        public List<string> TypicalPhases { get; set; } = new();
        public List<string> KeyBIMUses { get; set; } = new();
        public List<string> TypicalDisciplines { get; set; } = new();
        public string TypicalLOD { get; set; }
        public List<string> RegulatoryBodies { get; set; } = new();
        public List<string> TypicalDeliverables { get; set; } = new();
        public List<string> StandardsRequired { get; set; } = new();
    }

    internal class LODSpecification
    {
        public string Level { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string GeometricDetail { get; set; }
        public string DataRequirements { get; set; }
        public string TypicalPhase { get; set; }
        public List<string> ModelUses { get; set; } = new();
    }

    internal class NamingConvention
    {
        public string Standard { get; set; }
        public string FileFormat { get; set; }
        public string Example { get; set; }
        public List<NamingField> Fields { get; set; } = new();
        public Dictionary<string, string> StatusCodes { get; set; } = new();
        public string RevisionScheme { get; set; }
    }

    internal class NamingField
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Example { get; set; }
    }

    #endregion
}
