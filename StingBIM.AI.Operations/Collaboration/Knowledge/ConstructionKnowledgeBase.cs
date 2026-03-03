// ================================================================================================
// STINGBIM AI COLLABORATION - CONSTRUCTION KNOWLEDGE BASE
// Comprehensive knowledge repository for construction best practices, lessons learned,
// standard procedures, and industry expertise
// ================================================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Collaboration.Knowledge
{
    #region Enums

    public enum KnowledgeCategory
    {
        BestPractice,
        LessonLearned,
        StandardProcedure,
        SafetyGuideline,
        QualityStandard,
        MaterialProperty,
        ConstructionMethod,
        EquipmentSpec,
        CostData,
        ProductivityRate,
        WeatherGuideline,
        ToolUsage,
        TradeSpecific,
        ProjectManagement,
        Coordination,
        Sequencing,
        Commissioning,
        Closeout
    }

    public enum ConstructionDiscipline
    {
        General,
        Sitework,
        Concrete,
        Masonry,
        Structural,
        Carpentry,
        Roofing,
        Waterproofing,
        Doors,
        Windows,
        Finishes,
        Specialties,
        Equipment,
        Furnishings,
        Conveying,
        Mechanical,
        Plumbing,
        HVAC,
        FireProtection,
        Electrical,
        Communications,
        Security,
        Earthwork,
        Utilities,
        Landscaping
    }

    public enum ExpertiseLevel
    {
        Beginner,
        Intermediate,
        Advanced,
        Expert
    }

    public enum KnowledgeReliability
    {
        Verified,
        Validated,
        Contributed,
        Unverified
    }

    #endregion

    #region Data Models

    public class KnowledgeArticle
    {
        public string ArticleId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public KnowledgeCategory Category { get; set; }
        public ConstructionDiscipline Discipline { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public ExpertiseLevel TargetLevel { get; set; }
        public KnowledgeReliability Reliability { get; set; }
        public string? SourceReference { get; set; }
        public string? AuthorId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }
        public int UseCount { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public List<string> RelatedArticleIds { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class BestPractice
    {
        public string PracticeId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public List<string> ApplicablePhases { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public List<string> Benefits { get; set; } = new();
        public List<string> Pitfalls { get; set; } = new();
        public List<string> RequiredResources { get; set; } = new();
        public string? CostImpact { get; set; }
        public string? TimeImpact { get; set; }
        public string? QualityImpact { get; set; }
        public string? SafetyImpact { get; set; }
        public List<string> RelatedStandards { get; set; } = new();
        public List<string> ExampleProjects { get; set; } = new();
        public double EffectivenessScore { get; set; }
        public int AdoptionCount { get; set; }
    }

    public class LessonLearned
    {
        public string LessonId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string? ProjectName { get; set; }
        public ConstructionDiscipline Discipline { get; set; }
        public string Situation { get; set; } = string.Empty;
        public string Problem { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        public LessonImpact Impact { get; set; } = new();
        public List<string> PreventionMeasures { get; set; } = new();
        public string? RootCause { get; set; }
        public DateTime OccurredAt { get; set; }
        public string? SubmittedBy { get; set; }
        public bool IsValidated { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class LessonImpact
    {
        public decimal? CostImpact { get; set; }
        public int? ScheduleImpactDays { get; set; }
        public string? SafetyImpact { get; set; }
        public string? QualityImpact { get; set; }
        public string? ReputationImpact { get; set; }
    }

    public class StandardProcedure
    {
        public string ProcedureId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public string? Scope { get; set; }
        public List<ProcedureStep> Steps { get; set; } = new();
        public List<string> RequiredMaterials { get; set; } = new();
        public List<string> RequiredEquipment { get; set; } = new();
        public List<string> RequiredPPE { get; set; } = new();
        public List<string> SafetyPrecautions { get; set; } = new();
        public List<string> QualityCheckpoints { get; set; } = new();
        public TimeSpan? EstimatedDuration { get; set; }
        public int? RequiredCrew { get; set; }
        public List<string> RelatedStandards { get; set; } = new();
        public string? Version { get; set; }
        public DateTime? EffectiveDate { get; set; }
    }

    public class ProcedureStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public List<string> SubSteps { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Tips { get; set; } = new();
        public TimeSpan? EstimatedTime { get; set; }
        public string? ResponsibleParty { get; set; }
        public bool RequiresInspection { get; set; }
    }

    public class MaterialKnowledge
    {
        public string MaterialId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public MaterialProperties Properties { get; set; } = new();
        public List<string> Applications { get; set; } = new();
        public List<string> Advantages { get; set; } = new();
        public List<string> Disadvantages { get; set; } = new();
        public List<string> InstallationRequirements { get; set; } = new();
        public List<string> StorageRequirements { get; set; } = new();
        public List<string> CompatibleMaterials { get; set; } = new();
        public List<string> IncompatibleMaterials { get; set; } = new();
        public List<string> ApplicableStandards { get; set; } = new();
        public List<string> CommonManufacturers { get; set; } = new();
        public CostRange? CostRange { get; set; }
        public string? EnvironmentalImpact { get; set; }
        public int? TypicalLifespanYears { get; set; }
        public List<string> MaintenanceRequirements { get; set; } = new();
    }

    public class MaterialProperties
    {
        public double? Density { get; set; }
        public string? DensityUnit { get; set; }
        public double? CompressiveStrength { get; set; }
        public string? CompressiveStrengthUnit { get; set; }
        public double? TensileStrength { get; set; }
        public string? TensileStrengthUnit { get; set; }
        public double? ThermalConductivity { get; set; }
        public double? RValue { get; set; }
        public double? FireRating { get; set; }
        public string? FireClass { get; set; }
        public double? WaterAbsorption { get; set; }
        public bool? IsCombustible { get; set; }
        public bool? IsRecyclable { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    public class CostRange
    {
        public decimal MinCost { get; set; }
        public decimal MaxCost { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
        public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
        public string? Region { get; set; }
    }

    public class ProductivityRate
    {
        public string RateId { get; set; } = Guid.NewGuid().ToString();
        public string TaskDescription { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public double OutputQuantity { get; set; }
        public string OutputUnit { get; set; } = string.Empty;
        public double LaborHours { get; set; }
        public int CrewSize { get; set; }
        public double ProductivityPerHour => LaborHours > 0 ? OutputQuantity / LaborHours : 0;
        public ProductivityConditions Conditions { get; set; } = new();
        public List<ProductivityFactor> Factors { get; set; } = new();
        public string? Source { get; set; }
        public DateTime? EffectiveDate { get; set; }
    }

    public class ProductivityConditions
    {
        public string? Weather { get; set; }
        public string? SiteAccess { get; set; }
        public string? WorkHeight { get; set; }
        public string? Complexity { get; set; }
        public bool? IsOvertime { get; set; }
        public string? Experience { get; set; }
    }

    public class ProductivityFactor
    {
        public string Name { get; set; } = string.Empty;
        public double Multiplier { get; set; } = 1.0;
        public string Description { get; set; } = string.Empty;
    }

    public class EquipmentKnowledge
    {
        public string EquipmentId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public List<string> Applications { get; set; } = new();
        public EquipmentSpecs Specifications { get; set; } = new();
        public List<string> OperatorRequirements { get; set; } = new();
        public List<string> SafetyRequirements { get; set; } = new();
        public List<string> MaintenanceSchedule { get; set; } = new();
        public List<string> CommonIssues { get; set; } = new();
        public CostRange? RentalCostRange { get; set; }
        public CostRange? PurchaseCostRange { get; set; }
        public List<string> Manufacturers { get; set; } = new();
    }

    public class EquipmentSpecs
    {
        public double? Capacity { get; set; }
        public string? CapacityUnit { get; set; }
        public double? Reach { get; set; }
        public string? ReachUnit { get; set; }
        public double? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? PowerSource { get; set; }
        public double? FuelConsumption { get; set; }
        public string? Dimensions { get; set; }
        public Dictionary<string, object> CustomSpecs { get; set; } = new();
    }

    public class ConstructionSequence
    {
        public string SequenceId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public List<SequencePhase> Phases { get; set; } = new();
        public List<SequenceDependency> Dependencies { get; set; } = new();
        public List<string> CriticalPath { get; set; } = new();
        public List<string> CommonVariations { get; set; } = new();
        public List<string> Considerations { get; set; } = new();
    }

    public class SequencePhase
    {
        public string PhaseId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Activities { get; set; } = new();
        public List<string> Milestones { get; set; } = new();
        public string? TypicalDuration { get; set; }
        public List<string> Trades { get; set; } = new();
    }

    public class SequenceDependency
    {
        public string FromPhase { get; set; } = string.Empty;
        public string ToPhase { get; set; } = string.Empty;
        public string DependencyType { get; set; } = string.Empty; // FS, SS, FF, SF
        public int? LagDays { get; set; }
        public string? Reason { get; set; }
    }

    public class WeatherGuideline
    {
        public string GuidelineId { get; set; } = Guid.NewGuid().ToString();
        public string Activity { get; set; } = string.Empty;
        public ConstructionDiscipline Discipline { get; set; }
        public TemperatureRange Temperature { get; set; } = new();
        public HumidityRange? Humidity { get; set; }
        public WindLimit? Wind { get; set; }
        public PrecipitationLimit? Precipitation { get; set; }
        public List<string> Precautions { get; set; } = new();
        public List<string> Prohibitions { get; set; } = new();
        public string? StandardReference { get; set; }
    }

    public class TemperatureRange
    {
        public double? MinTemperature { get; set; }
        public double? MaxTemperature { get; set; }
        public string Unit { get; set; } = "°F";
        public double? IdealMin { get; set; }
        public double? IdealMax { get; set; }
    }

    public class HumidityRange
    {
        public double? MinHumidity { get; set; }
        public double? MaxHumidity { get; set; }
    }

    public class WindLimit
    {
        public double MaxSpeed { get; set; }
        public string Unit { get; set; } = "mph";
        public string? Activity { get; set; }
    }

    public class PrecipitationLimit
    {
        public bool AllowRain { get; set; }
        public bool AllowSnow { get; set; }
        public double? MaxAccumulation { get; set; }
        public string? WaitTimeAfter { get; set; }
    }

    public class KnowledgeQuery
    {
        public string QueryId { get; set; } = Guid.NewGuid().ToString();
        public string SearchText { get; set; } = string.Empty;
        public List<KnowledgeCategory>? Categories { get; set; }
        public List<ConstructionDiscipline>? Disciplines { get; set; }
        public List<string>? Tags { get; set; }
        public ExpertiseLevel? TargetLevel { get; set; }
        public int MaxResults { get; set; } = 20;
        public bool IncludeRelated { get; set; } = true;
    }

    public class KnowledgeSearchResult
    {
        public string ResultId { get; set; } = Guid.NewGuid().ToString();
        public string QueryId { get; set; } = string.Empty;
        public List<SearchResultItem> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public List<string> SuggestedQueries { get; set; } = new();
        public Dictionary<string, int> CategoryCounts { get; set; } = new();
        public double SearchTimeMs { get; set; }
    }

    public class SearchResultItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public double RelevanceScore { get; set; }
        public KnowledgeCategory Category { get; set; }
        public ConstructionDiscipline Discipline { get; set; }
        public List<string> MatchedKeywords { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    #endregion

    /// <summary>
    /// Construction Knowledge Base providing comprehensive access to construction
    /// best practices, lessons learned, standard procedures, and industry expertise
    /// </summary>
    public class ConstructionKnowledgeBase : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, KnowledgeArticle> _articles = new();
        private readonly ConcurrentDictionary<string, BestPractice> _bestPractices = new();
        private readonly ConcurrentDictionary<string, LessonLearned> _lessonsLearned = new();
        private readonly ConcurrentDictionary<string, StandardProcedure> _procedures = new();
        private readonly ConcurrentDictionary<string, MaterialKnowledge> _materials = new();
        private readonly ConcurrentDictionary<string, ProductivityRate> _productivityRates = new();
        private readonly ConcurrentDictionary<string, EquipmentKnowledge> _equipment = new();
        private readonly ConcurrentDictionary<string, ConstructionSequence> _sequences = new();
        private readonly ConcurrentDictionary<string, WeatherGuideline> _weatherGuidelines = new();
        private readonly SemaphoreSlim _searchSemaphore = new(10);
        private bool _disposed;

        public ConstructionKnowledgeBase()
        {
            InitializeKnowledgeBase();
        }

        #region Initialization

        private void InitializeKnowledgeBase()
        {
            // Initialize Best Practices
            InitializeBestPractices();

            // Initialize Standard Procedures
            InitializeStandardProcedures();

            // Initialize Material Knowledge
            InitializeMaterialKnowledge();

            // Initialize Productivity Rates
            InitializeProductivityRates();

            // Initialize Weather Guidelines
            InitializeWeatherGuidelines();

            // Initialize Construction Sequences
            InitializeConstructionSequences();
        }

        private void InitializeBestPractices()
        {
            var practices = new[]
            {
                new BestPractice
                {
                    Title = "Pre-Construction Site Survey",
                    Description = "Comprehensive site assessment before mobilization",
                    Discipline = ConstructionDiscipline.General,
                    ApplicablePhases = new() { "Pre-Construction", "Mobilization" },
                    Steps = new()
                    {
                        "Review all available site documentation",
                        "Conduct physical site walkthrough",
                        "Document existing conditions with photos and video",
                        "Identify underground utilities and mark locations",
                        "Assess access routes and staging areas",
                        "Note environmental conditions and constraints"
                    },
                    Benefits = new()
                    {
                        "Reduces unforeseen conditions",
                        "Improves project planning accuracy",
                        "Minimizes change orders",
                        "Enhances safety planning"
                    },
                    Pitfalls = new()
                    {
                        "Skipping documentation of existing conditions",
                        "Not verifying utility locations",
                        "Inadequate stakeholder communication"
                    },
                    EffectivenessScore = 0.95
                },
                new BestPractice
                {
                    Title = "Daily Safety Briefings",
                    Description = "Morning safety meetings to review hazards and safe work practices",
                    Discipline = ConstructionDiscipline.General,
                    ApplicablePhases = new() { "Construction" },
                    Steps = new()
                    {
                        "Gather all workers before shift start",
                        "Review day's work activities and associated hazards",
                        "Discuss weather conditions and their impact",
                        "Review any incidents or near-misses from previous day",
                        "Confirm all workers have required PPE",
                        "Address worker questions and concerns",
                        "Document attendance and topics covered"
                    },
                    Benefits = new()
                    {
                        "Reduces workplace incidents",
                        "Improves safety awareness",
                        "Builds safety culture",
                        "Facilitates communication"
                    },
                    SafetyImpact = "Significant reduction in incident rates",
                    EffectivenessScore = 0.92
                },
                new BestPractice
                {
                    Title = "BIM Coordination Workflow",
                    Description = "Systematic approach to BIM-based clash detection and resolution",
                    Discipline = ConstructionDiscipline.General,
                    ApplicablePhases = new() { "Pre-Construction", "Construction" },
                    Steps = new()
                    {
                        "Establish model standards and protocols",
                        "Define clash detection matrix by discipline",
                        "Run weekly clash detection tests",
                        "Prioritize clashes by severity and trade impact",
                        "Conduct coordination meetings with responsible parties",
                        "Document resolutions and update models",
                        "Verify resolutions through re-run of clash detection"
                    },
                    Benefits = new()
                    {
                        "Reduces field conflicts by 80%+",
                        "Improves coordination between trades",
                        "Saves rework costs",
                        "Accelerates installation"
                    },
                    CostImpact = "10-20% reduction in MEP installation costs",
                    TimeImpact = "Reduced RFI response times",
                    EffectivenessScore = 0.94
                },
                new BestPractice
                {
                    Title = "Look-Ahead Schedule Review",
                    Description = "Weekly schedule review and constraint analysis",
                    Discipline = ConstructionDiscipline.General,
                    ApplicablePhases = new() { "Construction" },
                    Steps = new()
                    {
                        "Update current schedule progress",
                        "Review 3-week look-ahead activities",
                        "Identify and document constraints",
                        "Assign constraint removal responsibilities",
                        "Verify resource availability",
                        "Confirm material deliveries",
                        "Coordinate access and sequencing with trades"
                    },
                    Benefits = new()
                    {
                        "Improves schedule reliability",
                        "Proactive constraint management",
                        "Better resource utilization",
                        "Enhanced trade coordination"
                    },
                    EffectivenessScore = 0.91
                },
                new BestPractice
                {
                    Title = "Quality First-Time Installation",
                    Description = "Systematic approach to achieving quality work the first time",
                    Discipline = ConstructionDiscipline.General,
                    ApplicablePhases = new() { "Construction" },
                    Steps = new()
                    {
                        "Review specifications and drawings before work",
                        "Confirm materials meet specifications",
                        "Verify prerequisites are complete",
                        "Use approved installation methods",
                        "Perform self-inspection during and after work",
                        "Document completed work with photos",
                        "Request formal inspection when ready"
                    },
                    Benefits = new()
                    {
                        "Eliminates rework",
                        "Reduces inspection failures",
                        "Improves schedule predictability",
                        "Enhances reputation"
                    },
                    QualityImpact = "90%+ first-time pass rate on inspections",
                    EffectivenessScore = 0.93
                }
            };

            foreach (var practice in practices)
            {
                _bestPractices[practice.PracticeId] = practice;
            }
        }

        private void InitializeStandardProcedures()
        {
            var procedures = new[]
            {
                new StandardProcedure
                {
                    Title = "Concrete Placement Procedure",
                    Description = "Standard procedure for placing structural concrete",
                    Discipline = ConstructionDiscipline.Concrete,
                    Scope = "All structural concrete placements",
                    Steps = new()
                    {
                        new() { StepNumber = 1, Description = "Verify formwork and reinforcement ready", Detail = "Confirm forms are properly aligned, secured, and rebar placement matches drawings" },
                        new() { StepNumber = 2, Description = "Confirm concrete mix design approved", Detail = "Verify mix ticket matches specified design" },
                        new() { StepNumber = 3, Description = "Prepare placement equipment", Detail = "Position pump, vibrators, and finishing tools" },
                        new() { StepNumber = 4, Description = "Conduct pre-placement meeting", Detail = "Review placement sequence with crew" },
                        new() { StepNumber = 5, Description = "Begin concrete placement", Detail = "Place in layers not exceeding 18 inches", Warnings = new() { "Do not allow concrete to free-fall more than 5 feet" } },
                        new() { StepNumber = 6, Description = "Consolidate concrete", Detail = "Use vibrators to remove air pockets" },
                        new() { StepNumber = 7, Description = "Finish surface as specified", Detail = "Apply appropriate finish per specifications" },
                        new() { StepNumber = 8, Description = "Implement curing measures", Detail = "Apply curing compound or wet cure as specified" },
                        new() { StepNumber = 9, Description = "Collect test cylinders", Detail = "Per testing requirements in specifications" }
                    },
                    RequiredMaterials = new() { "Concrete (per mix design)", "Curing compound", "Release agent" },
                    RequiredEquipment = new() { "Concrete pump or crane/bucket", "Vibrators", "Finishing tools", "Curing equipment" },
                    RequiredPPE = new() { "Hard hat", "Safety glasses", "Rubber boots", "Gloves", "High-vis vest" },
                    SafetyPrecautions = new()
                    {
                        "Protect skin from wet concrete",
                        "Ensure stable working platforms",
                        "Watch for pinch points with pump hoses",
                        "Stay clear of crane swing radius"
                    },
                    QualityCheckpoints = new()
                    {
                        "Verify slump test results",
                        "Check concrete temperature",
                        "Confirm proper consolidation",
                        "Document placement conditions"
                    },
                    RelatedStandards = new() { "ACI 301", "ACI 318", "ASTM C94" }
                },
                new StandardProcedure
                {
                    Title = "Steel Erection Procedure",
                    Description = "Standard procedure for structural steel erection",
                    Discipline = ConstructionDiscipline.Structural,
                    Steps = new()
                    {
                        new() { StepNumber = 1, Description = "Verify anchor bolt locations", Detail = "Check layout against approved drawings" },
                        new() { StepNumber = 2, Description = "Establish erection sequence", Detail = "Follow approved erection drawings" },
                        new() { StepNumber = 3, Description = "Set first columns", Detail = "Plumb and secure with guy wires" },
                        new() { StepNumber = 4, Description = "Install beams and connections", Detail = "Use drift pins for alignment, then install bolts" },
                        new() { StepNumber = 5, Description = "Verify alignment and plumbness", Detail = "Use survey instruments to confirm" },
                        new() { StepNumber = 6, Description = "Complete bolted connections", Detail = "Install all bolts and tighten per specification" },
                        new() { StepNumber = 7, Description = "Request inspection", Detail = "Before release of temporary bracing" }
                    },
                    RequiredPPE = new() { "Hard hat", "Safety glasses", "Fall protection", "Steel-toe boots", "High-vis vest" },
                    SafetyPrecautions = new()
                    {
                        "100% fall protection required above 15 feet",
                        "No work below suspended loads",
                        "Tag lines required for all picks",
                        "Monitor wind conditions"
                    },
                    RelatedStandards = new() { "AISC 360", "OSHA 1926.756", "AWS D1.1" }
                }
            };

            foreach (var proc in procedures)
            {
                _procedures[proc.ProcedureId] = proc;
            }
        }

        private void InitializeMaterialKnowledge()
        {
            var materials = new[]
            {
                new MaterialKnowledge
                {
                    Name = "Ready-Mix Concrete",
                    Description = "Pre-mixed concrete delivered to site",
                    Category = "Concrete",
                    Discipline = ConstructionDiscipline.Concrete,
                    Properties = new()
                    {
                        CompressiveStrength = 4000,
                        CompressiveStrengthUnit = "psi",
                        Density = 150,
                        DensityUnit = "pcf"
                    },
                    Applications = new() { "Foundations", "Slabs", "Columns", "Beams", "Walls" },
                    Advantages = new() { "Consistent quality", "Labor savings", "Time efficiency", "Quality control" },
                    Disadvantages = new() { "Limited working time", "Delivery coordination required", "Weather sensitive" },
                    InstallationRequirements = new()
                    {
                        "Place within 90 minutes of batching",
                        "Maintain proper slump",
                        "Adequate consolidation",
                        "Proper curing"
                    },
                    ApplicableStandards = new() { "ASTM C94", "ACI 301", "ACI 318" },
                    CostRange = new() { MinCost = 100, MaxCost = 200, Unit = "CY" },
                    TypicalLifespanYears = 100
                },
                new MaterialKnowledge
                {
                    Name = "Structural Steel",
                    Description = "Hot-rolled structural steel shapes",
                    Category = "Metals",
                    Discipline = ConstructionDiscipline.Structural,
                    Properties = new()
                    {
                        TensileStrength = 58000,
                        TensileStrengthUnit = "psi",
                        Density = 490,
                        DensityUnit = "pcf"
                    },
                    Applications = new() { "Columns", "Beams", "Bracing", "Trusses" },
                    Advantages = new() { "High strength-to-weight ratio", "Ductile behavior", "Fast erection", "Recyclable" },
                    Disadvantages = new() { "Requires fire protection", "Corrosion protection needed", "Connection design critical" },
                    ApplicableStandards = new() { "ASTM A992", "AISC 360", "AWS D1.1" },
                    CostRange = new() { MinCost = 2000, MaxCost = 4000, Unit = "ton" },
                    TypicalLifespanYears = 100
                },
                new MaterialKnowledge
                {
                    Name = "Gypsum Board",
                    Description = "Drywall/sheetrock for interior partitions",
                    Category = "Finishes",
                    Discipline = ConstructionDiscipline.Finishes,
                    Properties = new()
                    {
                        FireRating = 1,
                        FireClass = "Type X",
                        Density = 50,
                        DensityUnit = "pcf"
                    },
                    Applications = new() { "Interior walls", "Ceilings", "Shaft walls", "Fire barriers" },
                    Advantages = new() { "Fire resistant", "Easy to install", "Cost effective", "Accepts various finishes" },
                    Disadvantages = new() { "Not moisture resistant (standard)", "Impact damage susceptible" },
                    StorageRequirements = new() { "Store flat", "Keep dry", "Support full length" },
                    ApplicableStandards = new() { "ASTM C36", "ASTM C1396", "GA-216" },
                    CostRange = new() { MinCost = 8, MaxCost = 20, Unit = "sheet" }
                }
            };

            foreach (var mat in materials)
            {
                _materials[mat.MaterialId] = mat;
            }
        }

        private void InitializeProductivityRates()
        {
            var rates = new[]
            {
                new ProductivityRate
                {
                    TaskDescription = "Form and pour concrete slab on grade",
                    Discipline = ConstructionDiscipline.Concrete,
                    OutputQuantity = 100,
                    OutputUnit = "SF",
                    LaborHours = 8,
                    CrewSize = 4,
                    Conditions = new() { Complexity = "Normal", Weather = "Good", Experience = "Experienced" }
                },
                new ProductivityRate
                {
                    TaskDescription = "Install structural steel beams",
                    Discipline = ConstructionDiscipline.Structural,
                    OutputQuantity = 10,
                    OutputUnit = "tons",
                    LaborHours = 8,
                    CrewSize = 4,
                    Conditions = new() { Complexity = "Normal", Weather = "Good" }
                },
                new ProductivityRate
                {
                    TaskDescription = "Install gypsum board on metal studs",
                    Discipline = ConstructionDiscipline.Finishes,
                    OutputQuantity = 400,
                    OutputUnit = "SF",
                    LaborHours = 8,
                    CrewSize = 2,
                    Conditions = new() { Complexity = "Normal" }
                },
                new ProductivityRate
                {
                    TaskDescription = "Install copper pipe",
                    Discipline = ConstructionDiscipline.Plumbing,
                    OutputQuantity = 80,
                    OutputUnit = "LF",
                    LaborHours = 8,
                    CrewSize = 2,
                    Conditions = new() { Complexity = "Normal" }
                },
                new ProductivityRate
                {
                    TaskDescription = "Install rectangular ductwork",
                    Discipline = ConstructionDiscipline.HVAC,
                    OutputQuantity = 150,
                    OutputUnit = "LF",
                    LaborHours = 8,
                    CrewSize = 2,
                    Conditions = new() { Complexity = "Normal", WorkHeight = "Ground level" }
                }
            };

            foreach (var rate in rates)
            {
                _productivityRates[rate.RateId] = rate;
            }
        }

        private void InitializeWeatherGuidelines()
        {
            var guidelines = new[]
            {
                new WeatherGuideline
                {
                    Activity = "Concrete Placement",
                    Discipline = ConstructionDiscipline.Concrete,
                    Temperature = new()
                    {
                        MinTemperature = 40,
                        MaxTemperature = 90,
                        IdealMin = 50,
                        IdealMax = 75,
                        Unit = "°F"
                    },
                    Precipitation = new() { AllowRain = false, AllowSnow = false, WaitTimeAfter = "Until surface is dry" },
                    Precautions = new()
                    {
                        "Below 50°F: Use heated enclosures and/or blankets",
                        "Above 80°F: Use retarder, sunshades, fog spray",
                        "Wind over 15 mph: Use wind breaks"
                    },
                    Prohibitions = new()
                    {
                        "Do not place if temperature expected to fall below 25°F within 24 hours",
                        "Do not place on frozen subgrade"
                    },
                    StandardReference = "ACI 306R, ACI 305R"
                },
                new WeatherGuideline
                {
                    Activity = "Crane Operations",
                    Discipline = ConstructionDiscipline.General,
                    Wind = new() { MaxSpeed = 30, Unit = "mph", Activity = "General lifting" },
                    Precautions = new()
                    {
                        "Monitor wind speed continuously",
                        "Reduce capacity for sustained wind",
                        "Use tag lines on all loads"
                    },
                    Prohibitions = new()
                    {
                        "No blind lifts in wind over 20 mph",
                        "No personnel platforms in wind over 20 mph"
                    }
                },
                new WeatherGuideline
                {
                    Activity = "Roofing - Hot Applied",
                    Discipline = ConstructionDiscipline.Roofing,
                    Temperature = new()
                    {
                        MinTemperature = 40,
                        MaxTemperature = 95,
                        Unit = "°F"
                    },
                    Precipitation = new() { AllowRain = false, WaitTimeAfter = "Deck must be dry" },
                    Precautions = new()
                    {
                        "Cold weather: Warm materials before application",
                        "Hot weather: Schedule work for cooler hours"
                    }
                },
                new WeatherGuideline
                {
                    Activity = "Painting - Exterior",
                    Discipline = ConstructionDiscipline.Finishes,
                    Temperature = new()
                    {
                        MinTemperature = 50,
                        MaxTemperature = 90,
                        Unit = "°F"
                    },
                    Humidity = new() { MinHumidity = 30, MaxHumidity = 85 },
                    Precipitation = new() { AllowRain = false, WaitTimeAfter = "Surface must be dry" },
                    Precautions = new()
                    {
                        "Check substrate temperature",
                        "Ensure no dew formation expected",
                        "Allow adequate drying time"
                    }
                }
            };

            foreach (var guideline in guidelines)
            {
                _weatherGuidelines[guideline.GuidelineId] = guideline;
            }
        }

        private void InitializeConstructionSequences()
        {
            var sequence = new ConstructionSequence
            {
                Name = "Commercial Building - Steel Frame",
                Description = "Typical construction sequence for steel-framed commercial building",
                BuildingType = "Commercial Office",
                Phases = new()
                {
                    new() { Name = "Site Preparation", Order = 1, Activities = new() { "Clear and grub", "Erosion control", "Rough grade", "Utilities rough-in" }, Trades = new() { "Sitework", "Utilities" } },
                    new() { Name = "Foundation", Order = 2, Activities = new() { "Excavation", "Form footings", "Place concrete", "Waterproofing", "Backfill" }, Trades = new() { "Concrete", "Waterproofing" } },
                    new() { Name = "Structure", Order = 3, Activities = new() { "Erect steel", "Install metal deck", "Place concrete on deck", "Fireproofing" }, Trades = new() { "Steel", "Concrete" } },
                    new() { Name = "Building Enclosure", Order = 4, Activities = new() { "Curtainwall/facades", "Roofing", "Windows", "Doors" }, Trades = new() { "Glazing", "Roofing" } },
                    new() { Name = "MEP Rough-In", Order = 5, Activities = new() { "HVAC ductwork", "Plumbing", "Electrical", "Fire protection" }, Trades = new() { "HVAC", "Plumbing", "Electrical" } },
                    new() { Name = "Interior Finishes", Order = 6, Activities = new() { "Framing", "Drywall", "Ceilings", "Flooring", "Paint" }, Trades = new() { "Carpentry", "Drywall", "Flooring" } },
                    new() { Name = "MEP Trim", Order = 7, Activities = new() { "Fixtures", "Devices", "Controls", "Testing" }, Trades = new() { "HVAC", "Plumbing", "Electrical" } },
                    new() { Name = "Commissioning", Order = 8, Activities = new() { "Systems testing", "Balancing", "Training", "Documentation" }, Trades = new() { "Commissioning" } }
                },
                CriticalPath = new() { "Foundation", "Structure", "Building Enclosure", "MEP Rough-In" }
            };

            _sequences[sequence.SequenceId] = sequence;
        }

        #endregion

        #region Search and Query

        /// <summary>
        /// Search the knowledge base
        /// </summary>
        public async Task<KnowledgeSearchResult> SearchAsync(
            KnowledgeQuery query,
            CancellationToken ct = default)
        {
            await _searchSemaphore.WaitAsync(ct);
            try
            {
                var startTime = DateTime.UtcNow;
                var results = new List<SearchResultItem>();

                // Search best practices
                foreach (var practice in _bestPractices.Values)
                {
                    if (MatchesQuery(practice.Title, practice.Description, query))
                    {
                        results.Add(new SearchResultItem
                        {
                            ItemId = practice.PracticeId,
                            ItemType = "BestPractice",
                            Title = practice.Title,
                            Summary = practice.Description,
                            Category = KnowledgeCategory.BestPractice,
                            Discipline = practice.Discipline,
                            RelevanceScore = CalculateRelevance(practice.Title, practice.Description, query.SearchText)
                        });
                    }
                }

                // Search procedures
                foreach (var proc in _procedures.Values)
                {
                    if (MatchesQuery(proc.Title, proc.Description, query))
                    {
                        results.Add(new SearchResultItem
                        {
                            ItemId = proc.ProcedureId,
                            ItemType = "StandardProcedure",
                            Title = proc.Title,
                            Summary = proc.Description,
                            Category = KnowledgeCategory.StandardProcedure,
                            Discipline = proc.Discipline,
                            RelevanceScore = CalculateRelevance(proc.Title, proc.Description, query.SearchText)
                        });
                    }
                }

                // Search materials
                foreach (var mat in _materials.Values)
                {
                    if (MatchesQuery(mat.Name, mat.Description, query))
                    {
                        results.Add(new SearchResultItem
                        {
                            ItemId = mat.MaterialId,
                            ItemType = "MaterialKnowledge",
                            Title = mat.Name,
                            Summary = mat.Description,
                            Category = KnowledgeCategory.MaterialProperty,
                            Discipline = mat.Discipline,
                            RelevanceScore = CalculateRelevance(mat.Name, mat.Description, query.SearchText)
                        });
                    }
                }

                // Search productivity rates
                foreach (var rate in _productivityRates.Values)
                {
                    if (MatchesQuery(rate.TaskDescription, "", query))
                    {
                        results.Add(new SearchResultItem
                        {
                            ItemId = rate.RateId,
                            ItemType = "ProductivityRate",
                            Title = rate.TaskDescription,
                            Summary = $"{rate.ProductivityPerHour:F2} {rate.OutputUnit}/hour",
                            Category = KnowledgeCategory.ProductivityRate,
                            Discipline = rate.Discipline,
                            RelevanceScore = CalculateRelevance(rate.TaskDescription, "", query.SearchText)
                        });
                    }
                }

                // Sort by relevance and limit
                results = results
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(query.MaxResults)
                    .ToList();

                return new KnowledgeSearchResult
                {
                    QueryId = query.QueryId,
                    Results = results,
                    TotalCount = results.Count,
                    SearchTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    CategoryCounts = results.GroupBy(r => r.Category.ToString())
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
            finally
            {
                _searchSemaphore.Release();
            }
        }

        private bool MatchesQuery(string title, string description, KnowledgeQuery query)
        {
            if (string.IsNullOrEmpty(query.SearchText))
                return true;

            var searchTerms = query.SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var content = $"{title} {description}".ToLower();

            return searchTerms.Any(term => content.Contains(term));
        }

        private double CalculateRelevance(string title, string description, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return 0.5;

            var searchTerms = searchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titleLower = title.ToLower();
            var descLower = description.ToLower();

            double score = 0;
            foreach (var term in searchTerms)
            {
                if (titleLower.Contains(term))
                    score += 0.5;
                if (descLower.Contains(term))
                    score += 0.25;
            }

            return Math.Min(1.0, score);
        }

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Get a best practice by ID
        /// </summary>
        public BestPractice? GetBestPractice(string practiceId)
        {
            return _bestPractices.TryGetValue(practiceId, out var practice) ? practice : null;
        }

        /// <summary>
        /// Get all best practices for a discipline
        /// </summary>
        public List<BestPractice> GetBestPracticesByDiscipline(ConstructionDiscipline discipline)
        {
            return _bestPractices.Values
                .Where(p => p.Discipline == discipline || p.Discipline == ConstructionDiscipline.General)
                .ToList();
        }

        /// <summary>
        /// Add a lesson learned
        /// </summary>
        public LessonLearned AddLessonLearned(LessonLearned lesson)
        {
            _lessonsLearned[lesson.LessonId] = lesson;
            return lesson;
        }

        /// <summary>
        /// Get lessons learned for a discipline
        /// </summary>
        public List<LessonLearned> GetLessonsLearned(ConstructionDiscipline discipline)
        {
            return _lessonsLearned.Values
                .Where(l => l.Discipline == discipline)
                .OrderByDescending(l => l.OccurredAt)
                .ToList();
        }

        /// <summary>
        /// Get a procedure by ID
        /// </summary>
        public StandardProcedure? GetProcedure(string procedureId)
        {
            return _procedures.TryGetValue(procedureId, out var proc) ? proc : null;
        }

        /// <summary>
        /// Get procedures for a discipline
        /// </summary>
        public List<StandardProcedure> GetProceduresByDiscipline(ConstructionDiscipline discipline)
        {
            return _procedures.Values
                .Where(p => p.Discipline == discipline)
                .ToList();
        }

        /// <summary>
        /// Get material knowledge
        /// </summary>
        public MaterialKnowledge? GetMaterial(string materialId)
        {
            return _materials.TryGetValue(materialId, out var mat) ? mat : null;
        }

        /// <summary>
        /// Get productivity rates for a discipline
        /// </summary>
        public List<ProductivityRate> GetProductivityRates(ConstructionDiscipline discipline)
        {
            return _productivityRates.Values
                .Where(r => r.Discipline == discipline)
                .ToList();
        }

        /// <summary>
        /// Get weather guidelines for an activity
        /// </summary>
        public WeatherGuideline? GetWeatherGuideline(string activity)
        {
            return _weatherGuidelines.Values
                .FirstOrDefault(g => g.Activity.ToLower().Contains(activity.ToLower()));
        }

        /// <summary>
        /// Get construction sequence by building type
        /// </summary>
        public ConstructionSequence? GetConstructionSequence(string buildingType)
        {
            return _sequences.Values
                .FirstOrDefault(s => s.BuildingType.ToLower().Contains(buildingType.ToLower()));
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get knowledge base statistics
        /// </summary>
        public Dictionary<string, int> GetStatistics()
        {
            return new Dictionary<string, int>
            {
                ["BestPractices"] = _bestPractices.Count,
                ["LessonsLearned"] = _lessonsLearned.Count,
                ["StandardProcedures"] = _procedures.Count,
                ["Materials"] = _materials.Count,
                ["ProductivityRates"] = _productivityRates.Count,
                ["WeatherGuidelines"] = _weatherGuidelines.Count,
                ["Sequences"] = _sequences.Count,
                ["Articles"] = _articles.Count
            };
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _searchSemaphore.Dispose();
            _articles.Clear();
            _bestPractices.Clear();
            _lessonsLearned.Clear();
            _procedures.Clear();
            _materials.Clear();
            _productivityRates.Clear();
            _equipment.Clear();
            _sequences.Clear();
            _weatherGuidelines.Clear();

            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
