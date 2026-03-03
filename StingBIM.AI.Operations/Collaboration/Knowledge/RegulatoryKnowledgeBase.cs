// ================================================================================================
// STINGBIM AI COLLABORATION - REGULATORY KNOWLEDGE BASE
// Comprehensive repository of building codes, standards, regulations, and compliance requirements
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

    public enum RegulatoryDomain
    {
        Building,
        Fire,
        Electrical,
        Plumbing,
        Mechanical,
        Energy,
        Accessibility,
        Structural,
        Environmental,
        Occupational,
        Zoning,
        Health,
        Transportation
    }

    public enum JurisdictionLevel
    {
        International,
        Federal,
        State,
        County,
        Municipal,
        Special
    }

    public enum StandardOrganization
    {
        ICC,           // International Code Council
        NFPA,          // National Fire Protection Association
        ASHRAE,        // American Society of Heating, Refrigerating and Air-Conditioning Engineers
        ASTM,          // American Society for Testing and Materials
        ANSI,          // American National Standards Institute
        ACI,           // American Concrete Institute
        AISC,          // American Institute of Steel Construction
        AWS,           // American Welding Society
        IEEE,          // Institute of Electrical and Electronics Engineers
        UL,            // Underwriters Laboratories
        OSHA,          // Occupational Safety and Health Administration
        EPA,           // Environmental Protection Agency
        ADA,           // Americans with Disabilities Act
        DOT,           // Department of Transportation
        ISO,           // International Organization for Standardization
        EN,            // European Standards
        BS,            // British Standards
        SANS,          // South African National Standards
        KEBS,          // Kenya Bureau of Standards
        UNBS,          // Uganda National Bureau of Standards
        Other
    }

    public enum RequirementType
    {
        Prescriptive,
        Performance,
        Mandatory,
        Advisory,
        Alternative,
        Exception
    }

    public enum ComplianceLevel
    {
        FullyCompliant,
        PartiallyCompliant,
        NonCompliant,
        NotApplicable,
        RequiresReview,
        ExceptionGranted
    }

    #endregion

    #region Data Models

    public class RegulatoryCode
    {
        public string CodeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int Year { get; set; }
        public StandardOrganization Organization { get; set; }
        public RegulatoryDomain Domain { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public List<CodeChapter> Chapters { get; set; } = new();
        public List<string> AdoptedJurisdictions { get; set; } = new();
        public DateTime? EffectiveDate { get; set; }
        public string? SupersededBy { get; set; }
        public List<string> ReferencedStandards { get; set; } = new();
        public string? DownloadUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CodeChapter
    {
        public string ChapterId { get; set; } = Guid.NewGuid().ToString();
        public int ChapterNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<CodeSection> Sections { get; set; } = new();
    }

    public class CodeSection
    {
        public string SectionId { get; set; } = Guid.NewGuid().ToString();
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public RequirementType Type { get; set; }
        public List<CodeRequirement> Requirements { get; set; } = new();
        public List<string> Exceptions { get; set; } = new();
        public List<string> References { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
    }

    public class CodeRequirement
    {
        public string RequirementId { get; set; } = Guid.NewGuid().ToString();
        public string CodeReference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RequirementType Type { get; set; }
        public RegulatoryDomain Domain { get; set; }
        public string? Value { get; set; }
        public string? Unit { get; set; }
        public string? Condition { get; set; }
        public List<string> ApplicableOccupancies { get; set; } = new();
        public List<string> ApplicableConstructionTypes { get; set; } = new();
        public List<string> Exceptions { get; set; } = new();
        public string? VerificationMethod { get; set; }
        public List<string> RelatedRequirements { get; set; } = new();
    }

    public class Standard
    {
        public string StandardId { get; set; } = Guid.NewGuid().ToString();
        public string Designation { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public StandardOrganization Organization { get; set; }
        public string Version { get; set; } = string.Empty;
        public int Year { get; set; }
        public RegulatoryDomain Domain { get; set; }
        public string Scope { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public List<string> Applications { get; set; } = new();
        public List<string> ReferencingCodes { get; set; } = new();
        public string? SupersededBy { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Jurisdiction
    {
        public string JurisdictionId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public JurisdictionLevel Level { get; set; }
        public string? ParentJurisdiction { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public List<AdoptedCode> AdoptedCodes { get; set; } = new();
        public List<LocalAmendment> LocalAmendments { get; set; } = new();
        public List<string> PermitRequirements { get; set; } = new();
        public List<ContactInfo> Contacts { get; set; } = new();
    }

    public class AdoptedCode
    {
        public string CodeId { get; set; } = string.Empty;
        public string CodeName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime AdoptionDate { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public bool HasLocalAmendments { get; set; }
    }

    public class LocalAmendment
    {
        public string AmendmentId { get; set; } = Guid.NewGuid().ToString();
        public string CodeReference { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;
        public string AmendedText { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
    }

    public class ContactInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Address { get; set; }
    }

    public class OccupancyClassification
    {
        public string ClassificationId { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Examples { get; set; } = new();
        public List<OccupancyRequirement> Requirements { get; set; } = new();
        public List<string> SpecialProvisions { get; set; } = new();
        public int? MaxOccupantLoad { get; set; }
        public List<string> AllowedConstructionTypes { get; set; } = new();
    }

    public class OccupancyRequirement
    {
        public string Category { get; set; } = string.Empty;
        public string Requirement { get; set; } = string.Empty;
        public string? CodeReference { get; set; }
        public string? Condition { get; set; }
    }

    public class ConstructionType
    {
        public string TypeId { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FireResistanceRatings FireRatings { get; set; } = new();
        public List<string> AllowedMaterials { get; set; } = new();
        public HeightAreaLimits HeightAreaLimits { get; set; } = new();
    }

    public class FireResistanceRatings
    {
        public double StructuralFrame { get; set; }
        public double BearingWallsExterior { get; set; }
        public double BearingWallsInterior { get; set; }
        public double NonbearingWallsExterior { get; set; }
        public double FloorConstruction { get; set; }
        public double RoofConstruction { get; set; }
        public string Unit { get; set; } = "hours";
    }

    public class HeightAreaLimits
    {
        public int MaxStories { get; set; }
        public int MaxHeightFeet { get; set; }
        public int MaxAreaPerFloorSF { get; set; }
        public bool SprinkleredIncrease { get; set; }
        public double? SprinkleredHeightIncrease { get; set; }
        public double? SprinkleredAreaIncrease { get; set; }
    }

    public class PermitType
    {
        public string PermitTypeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RegulatoryDomain Domain { get; set; }
        public List<string> RequiredDocuments { get; set; } = new();
        public List<string> ReviewDepartments { get; set; } = new();
        public string? TypicalReviewTime { get; set; }
        public List<string> Fees { get; set; } = new();
        public List<string> RequiredInspections { get; set; } = new();
    }

    public class InspectionType
    {
        public string InspectionTypeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RegulatoryDomain Domain { get; set; }
        public string Phase { get; set; } = string.Empty;
        public List<string> Checkpoints { get; set; } = new();
        public List<string> RequiredDocumentation { get; set; } = new();
        public List<string> CommonDeficiencies { get; set; } = new();
        public string? CodeReference { get; set; }
    }

    public class ComplianceCheck
    {
        public string CheckId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string RequirementId { get; set; } = string.Empty;
        public string CodeReference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplianceLevel Status { get; set; }
        public string? Finding { get; set; }
        public string? Resolution { get; set; }
        public string? Evidence { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public string? CheckedBy { get; set; }
    }

    public class RegulatoryQuery
    {
        public string QueryId { get; set; } = Guid.NewGuid().ToString();
        public string SearchText { get; set; } = string.Empty;
        public List<RegulatoryDomain>? Domains { get; set; }
        public List<string>? Codes { get; set; }
        public string? Jurisdiction { get; set; }
        public string? OccupancyType { get; set; }
        public string? ConstructionType { get; set; }
        public int MaxResults { get; set; } = 20;
    }

    public class RegulatorySearchResult
    {
        public string ResultId { get; set; } = Guid.NewGuid().ToString();
        public string QueryId { get; set; } = string.Empty;
        public List<RegulatorySearchItem> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public Dictionary<string, int> DomainCounts { get; set; } = new();
        public double SearchTimeMs { get; set; }
    }

    public class RegulatorySearchItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public RegulatoryDomain Domain { get; set; }
        public double RelevanceScore { get; set; }
    }

    #endregion

    /// <summary>
    /// Regulatory Knowledge Base providing comprehensive access to building codes,
    /// standards, regulations, and compliance requirements
    /// </summary>
    public class RegulatoryKnowledgeBase : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, RegulatoryCode> _codes = new();
        private readonly ConcurrentDictionary<string, Standard> _standards = new();
        private readonly ConcurrentDictionary<string, Jurisdiction> _jurisdictions = new();
        private readonly ConcurrentDictionary<string, OccupancyClassification> _occupancies = new();
        private readonly ConcurrentDictionary<string, ConstructionType> _constructionTypes = new();
        private readonly ConcurrentDictionary<string, PermitType> _permitTypes = new();
        private readonly ConcurrentDictionary<string, InspectionType> _inspectionTypes = new();
        private readonly ConcurrentDictionary<string, CodeRequirement> _requirements = new();
        private readonly SemaphoreSlim _searchSemaphore = new(10);
        private bool _disposed;

        public RegulatoryKnowledgeBase()
        {
            InitializeKnowledgeBase();
        }

        #region Initialization

        private void InitializeKnowledgeBase()
        {
            InitializeCodes();
            InitializeStandards();
            InitializeOccupancies();
            InitializeConstructionTypes();
            InitializePermitTypes();
            InitializeInspectionTypes();
            InitializeRequirements();
        }

        private void InitializeCodes()
        {
            var codes = new[]
            {
                new RegulatoryCode
                {
                    Name = "International Building Code",
                    Abbreviation = "IBC",
                    Version = "2021",
                    Year = 2021,
                    Organization = StandardOrganization.ICC,
                    Domain = RegulatoryDomain.Building,
                    Description = "Model building code providing minimum requirements for building systems",
                    Scope = "New and existing buildings, structures, and systems",
                    Chapters = new()
                    {
                        new() { ChapterNumber = 1, Title = "Scope and Administration" },
                        new() { ChapterNumber = 3, Title = "Use and Occupancy Classification" },
                        new() { ChapterNumber = 5, Title = "General Building Heights and Areas" },
                        new() { ChapterNumber = 6, Title = "Types of Construction" },
                        new() { ChapterNumber = 7, Title = "Fire and Smoke Protection Features" },
                        new() { ChapterNumber = 9, Title = "Fire Protection Systems" },
                        new() { ChapterNumber = 10, Title = "Means of Egress" },
                        new() { ChapterNumber = 11, Title = "Accessibility" }
                    }
                },
                new RegulatoryCode
                {
                    Name = "International Fire Code",
                    Abbreviation = "IFC",
                    Version = "2021",
                    Year = 2021,
                    Organization = StandardOrganization.ICC,
                    Domain = RegulatoryDomain.Fire,
                    Description = "Model fire code for fire prevention and life safety",
                    Scope = "Fire and life safety requirements for new and existing buildings"
                },
                new RegulatoryCode
                {
                    Name = "National Electrical Code",
                    Abbreviation = "NEC",
                    Version = "2023",
                    Year = 2023,
                    Organization = StandardOrganization.NFPA,
                    Domain = RegulatoryDomain.Electrical,
                    Description = "NFPA 70 - Standard for electrical installations",
                    Scope = "Electrical conductors, equipment, and raceways"
                },
                new RegulatoryCode
                {
                    Name = "International Plumbing Code",
                    Abbreviation = "IPC",
                    Version = "2021",
                    Year = 2021,
                    Organization = StandardOrganization.ICC,
                    Domain = RegulatoryDomain.Plumbing,
                    Description = "Model plumbing code for plumbing systems",
                    Scope = "Plumbing fixtures, water distribution, and drainage"
                },
                new RegulatoryCode
                {
                    Name = "International Mechanical Code",
                    Abbreviation = "IMC",
                    Version = "2021",
                    Year = 2021,
                    Organization = StandardOrganization.ICC,
                    Domain = RegulatoryDomain.Mechanical,
                    Description = "Model mechanical code for HVAC systems",
                    Scope = "Mechanical systems including HVAC, exhaust, and duct systems"
                },
                new RegulatoryCode
                {
                    Name = "International Energy Conservation Code",
                    Abbreviation = "IECC",
                    Version = "2021",
                    Year = 2021,
                    Organization = StandardOrganization.ICC,
                    Domain = RegulatoryDomain.Energy,
                    Description = "Model energy code for building efficiency",
                    Scope = "Energy efficiency requirements for building envelope and systems"
                },
                new RegulatoryCode
                {
                    Name = "NFPA 72 - Fire Alarm Code",
                    Abbreviation = "NFPA 72",
                    Version = "2022",
                    Year = 2022,
                    Organization = StandardOrganization.NFPA,
                    Domain = RegulatoryDomain.Fire,
                    Description = "National Fire Alarm and Signaling Code",
                    Scope = "Fire alarm systems, emergency communications"
                },
                new RegulatoryCode
                {
                    Name = "NFPA 13 - Sprinkler Systems",
                    Abbreviation = "NFPA 13",
                    Version = "2022",
                    Year = 2022,
                    Organization = StandardOrganization.NFPA,
                    Domain = RegulatoryDomain.Fire,
                    Description = "Standard for Installation of Sprinkler Systems",
                    Scope = "Automatic sprinkler system design and installation"
                },
                new RegulatoryCode
                {
                    Name = "ADA Accessibility Guidelines",
                    Abbreviation = "ADAAG",
                    Version = "2010",
                    Year = 2010,
                    Organization = StandardOrganization.ADA,
                    Domain = RegulatoryDomain.Accessibility,
                    Description = "Accessibility requirements for buildings and facilities",
                    Scope = "Accessibility for people with disabilities"
                },
                new RegulatoryCode
                {
                    Name = "OSHA Construction Standards",
                    Abbreviation = "29 CFR 1926",
                    Version = "2024",
                    Year = 2024,
                    Organization = StandardOrganization.OSHA,
                    Domain = RegulatoryDomain.Occupational,
                    Description = "Safety and Health Regulations for Construction",
                    Scope = "Worker safety in construction operations"
                }
            };

            foreach (var code in codes)
            {
                _codes[code.CodeId] = code;
            }
        }

        private void InitializeStandards()
        {
            var standards = new[]
            {
                new Standard { Designation = "ASTM A615", Title = "Standard Specification for Deformed and Plain Carbon-Steel Bars for Concrete Reinforcement", Organization = StandardOrganization.ASTM, Domain = RegulatoryDomain.Structural, Keywords = new() { "rebar", "reinforcement", "concrete" } },
                new Standard { Designation = "ASTM C94", Title = "Standard Specification for Ready-Mixed Concrete", Organization = StandardOrganization.ASTM, Domain = RegulatoryDomain.Structural, Keywords = new() { "concrete", "ready-mix" } },
                new Standard { Designation = "ASTM C150", Title = "Standard Specification for Portland Cement", Organization = StandardOrganization.ASTM, Domain = RegulatoryDomain.Structural, Keywords = new() { "cement", "portland" } },
                new Standard { Designation = "ACI 318", Title = "Building Code Requirements for Structural Concrete", Organization = StandardOrganization.ACI, Domain = RegulatoryDomain.Structural, Keywords = new() { "concrete", "structural", "design" } },
                new Standard { Designation = "AISC 360", Title = "Specification for Structural Steel Buildings", Organization = StandardOrganization.AISC, Domain = RegulatoryDomain.Structural, Keywords = new() { "steel", "structural" } },
                new Standard { Designation = "AWS D1.1", Title = "Structural Welding Code - Steel", Organization = StandardOrganization.AWS, Domain = RegulatoryDomain.Structural, Keywords = new() { "welding", "steel" } },
                new Standard { Designation = "ASHRAE 90.1", Title = "Energy Standard for Buildings Except Low-Rise Residential", Organization = StandardOrganization.ASHRAE, Domain = RegulatoryDomain.Energy, Keywords = new() { "energy", "efficiency", "HVAC" } },
                new Standard { Designation = "ASHRAE 62.1", Title = "Ventilation for Acceptable Indoor Air Quality", Organization = StandardOrganization.ASHRAE, Domain = RegulatoryDomain.Mechanical, Keywords = new() { "ventilation", "IAQ", "air quality" } },
                new Standard { Designation = "UL 263", Title = "Fire Tests of Building Construction and Materials", Organization = StandardOrganization.UL, Domain = RegulatoryDomain.Fire, Keywords = new() { "fire", "testing", "rating" } },
                new Standard { Designation = "IEEE C2", Title = "National Electrical Safety Code", Organization = StandardOrganization.IEEE, Domain = RegulatoryDomain.Electrical, Keywords = new() { "electrical", "safety" } }
            };

            foreach (var std in standards)
            {
                _standards[std.StandardId] = std;
            }
        }

        private void InitializeOccupancies()
        {
            var occupancies = new[]
            {
                new OccupancyClassification
                {
                    Code = "A-1",
                    Name = "Assembly, Fixed Seating",
                    Description = "Assembly uses with fixed seating for viewing performing arts or motion pictures",
                    Examples = new() { "Theaters", "Concert halls", "TV studios with live audience" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Automatic sprinkler system required", CodeReference = "IBC 903.2.1.1" },
                        new() { Category = "Egress", Requirement = "Multiple exits required", CodeReference = "IBC 1006" },
                        new() { Category = "Fire Alarm", Requirement = "Manual and automatic detection required", CodeReference = "IBC 907.2.1" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "B",
                    Name = "Business",
                    Description = "Office, professional, or service-type transactions",
                    Examples = new() { "Offices", "Banks", "Clinics", "Laboratories" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Sprinklers required based on height and area", CodeReference = "IBC 903.2.3" },
                        new() { Category = "Occupant Load", Requirement = "100 gross SF per occupant", CodeReference = "IBC Table 1004.5" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "E",
                    Name = "Educational",
                    Description = "Educational purposes through 12th grade",
                    Examples = new() { "Schools", "Day care (> 5 children)", "Training facilities" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Automatic sprinkler system required", CodeReference = "IBC 903.2.4" },
                        new() { Category = "Fire Alarm", Requirement = "Manual fire alarm required", CodeReference = "IBC 907.2.3" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "F-1",
                    Name = "Factory Industrial, Moderate Hazard",
                    Description = "Manufacturing with moderate-hazard materials",
                    Examples = new() { "Aircraft", "Appliances", "Automobiles", "Electronics" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Based on height and area", CodeReference = "IBC 903.2.5" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "H",
                    Name = "High Hazard",
                    Description = "Manufacturing or storage of hazardous materials",
                    Examples = new() { "Chemical plants", "Refineries", "Explosives manufacturing" },
                    Requirements = new()
                    {
                        new() { Category = "Special", Requirement = "Specific requirements based on hazard type", CodeReference = "IBC Chapter 4" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "I-2",
                    Name = "Institutional, Medical",
                    Description = "Medical, surgical, psychiatric, nursing, or custodial care",
                    Examples = new() { "Hospitals", "Nursing homes", "Mental health facilities" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Automatic sprinkler system required", CodeReference = "IBC 903.2.6" },
                        new() { Category = "Construction", Requirement = "Type I or II construction generally required", CodeReference = "IBC 407" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "M",
                    Name = "Mercantile",
                    Description = "Display and sale of merchandise",
                    Examples = new() { "Department stores", "Retail stores", "Markets" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Based on area", CodeReference = "IBC 903.2.7" },
                        new() { Category = "Occupant Load", Requirement = "60 gross SF per occupant (basement)", CodeReference = "IBC Table 1004.5" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "R-2",
                    Name = "Residential, Transient",
                    Description = "Sleeping units not meeting R-1 or R-3",
                    Examples = new() { "Apartments", "Condos", "Dormitories", "Boarding houses" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "NFPA 13R for 4+ stories, NFPA 13D for 3 or less", CodeReference = "IBC 903.2.8" },
                        new() { Category = "Fire Alarm", Requirement = "Manual and automatic detection", CodeReference = "IBC 907.2.9" }
                    }
                },
                new OccupancyClassification
                {
                    Code = "S-1",
                    Name = "Storage, Moderate Hazard",
                    Description = "Storage of moderate-hazard materials",
                    Examples = new() { "Furniture", "Lumber", "Paper products", "Tires" },
                    Requirements = new()
                    {
                        new() { Category = "Fire Suppression", Requirement = "Based on height and area", CodeReference = "IBC 903.2.9" }
                    }
                }
            };

            foreach (var occ in occupancies)
            {
                _occupancies[occ.ClassificationId] = occ;
            }
        }

        private void InitializeConstructionTypes()
        {
            var types = new[]
            {
                new ConstructionType
                {
                    Code = "IA",
                    Name = "Type IA - Fire Resistive",
                    Description = "Non-combustible construction with highest fire ratings",
                    FireRatings = new()
                    {
                        StructuralFrame = 3,
                        BearingWallsExterior = 3,
                        BearingWallsInterior = 3,
                        FloorConstruction = 2,
                        RoofConstruction = 1.5
                    },
                    AllowedMaterials = new() { "Concrete", "Steel", "Masonry" },
                    HeightAreaLimits = new() { MaxStories = 160, MaxHeightFeet = 160, MaxAreaPerFloorSF = -1 } // Unlimited for most occupancies
                },
                new ConstructionType
                {
                    Code = "IB",
                    Name = "Type IB - Fire Resistive",
                    Description = "Non-combustible construction with reduced fire ratings",
                    FireRatings = new()
                    {
                        StructuralFrame = 2,
                        BearingWallsExterior = 2,
                        BearingWallsInterior = 2,
                        FloorConstruction = 2,
                        RoofConstruction = 1
                    },
                    AllowedMaterials = new() { "Concrete", "Steel", "Masonry" }
                },
                new ConstructionType
                {
                    Code = "IIA",
                    Name = "Type IIA - Non-combustible",
                    Description = "Non-combustible with 1-hour fire ratings",
                    FireRatings = new()
                    {
                        StructuralFrame = 1,
                        BearingWallsExterior = 1,
                        BearingWallsInterior = 1,
                        FloorConstruction = 1,
                        RoofConstruction = 1
                    },
                    AllowedMaterials = new() { "Concrete", "Steel", "Masonry" }
                },
                new ConstructionType
                {
                    Code = "IIB",
                    Name = "Type IIB - Non-combustible",
                    Description = "Non-combustible with no fire rating required",
                    FireRatings = new()
                    {
                        StructuralFrame = 0,
                        BearingWallsExterior = 0,
                        BearingWallsInterior = 0,
                        FloorConstruction = 0,
                        RoofConstruction = 0
                    },
                    AllowedMaterials = new() { "Steel", "Concrete", "Masonry" }
                },
                new ConstructionType
                {
                    Code = "IIIA",
                    Name = "Type IIIA - Ordinary",
                    Description = "Exterior walls non-combustible, interior can be combustible with 1-hour rating",
                    FireRatings = new()
                    {
                        StructuralFrame = 1,
                        BearingWallsExterior = 2,
                        BearingWallsInterior = 1,
                        FloorConstruction = 1,
                        RoofConstruction = 1
                    },
                    AllowedMaterials = new() { "Masonry exterior", "Wood interior" }
                },
                new ConstructionType
                {
                    Code = "VA",
                    Name = "Type VA - Wood Frame",
                    Description = "Combustible framing with 1-hour fire ratings",
                    FireRatings = new()
                    {
                        StructuralFrame = 1,
                        BearingWallsExterior = 1,
                        BearingWallsInterior = 1,
                        FloorConstruction = 1,
                        RoofConstruction = 1
                    },
                    AllowedMaterials = new() { "Wood", "Light-gauge steel" }
                },
                new ConstructionType
                {
                    Code = "VB",
                    Name = "Type VB - Wood Frame",
                    Description = "Combustible framing with no fire rating required",
                    FireRatings = new()
                    {
                        StructuralFrame = 0,
                        BearingWallsExterior = 0,
                        BearingWallsInterior = 0,
                        FloorConstruction = 0,
                        RoofConstruction = 0
                    },
                    AllowedMaterials = new() { "Wood", "Light-gauge steel" }
                }
            };

            foreach (var type in types)
            {
                _constructionTypes[type.TypeId] = type;
            }
        }

        private void InitializePermitTypes()
        {
            var permits = new[]
            {
                new PermitType
                {
                    Name = "Building Permit",
                    Description = "Permit for new construction, additions, and alterations",
                    Domain = RegulatoryDomain.Building,
                    RequiredDocuments = new() { "Construction documents", "Site plan", "Structural calculations", "Energy compliance forms" },
                    ReviewDepartments = new() { "Building", "Planning", "Fire", "Engineering" },
                    TypicalReviewTime = "2-4 weeks",
                    RequiredInspections = new() { "Foundation", "Framing", "MEP rough", "Insulation", "Final" }
                },
                new PermitType
                {
                    Name = "Electrical Permit",
                    Description = "Permit for electrical installations and modifications",
                    Domain = RegulatoryDomain.Electrical,
                    RequiredDocuments = new() { "Electrical plans", "Panel schedules", "Load calculations" },
                    ReviewDepartments = new() { "Electrical" },
                    TypicalReviewTime = "1-2 weeks"
                },
                new PermitType
                {
                    Name = "Plumbing Permit",
                    Description = "Permit for plumbing installations",
                    Domain = RegulatoryDomain.Plumbing,
                    RequiredDocuments = new() { "Plumbing plans", "Isometrics", "Fixture schedule" },
                    ReviewDepartments = new() { "Plumbing" },
                    TypicalReviewTime = "1-2 weeks"
                },
                new PermitType
                {
                    Name = "Mechanical Permit",
                    Description = "Permit for HVAC installations",
                    Domain = RegulatoryDomain.Mechanical,
                    RequiredDocuments = new() { "HVAC plans", "Equipment schedules", "Duct sizing calculations" },
                    ReviewDepartments = new() { "Mechanical" },
                    TypicalReviewTime = "1-2 weeks"
                },
                new PermitType
                {
                    Name = "Fire Alarm Permit",
                    Description = "Permit for fire alarm system installation",
                    Domain = RegulatoryDomain.Fire,
                    RequiredDocuments = new() { "Fire alarm plans", "Device schedules", "Sequence of operation" },
                    ReviewDepartments = new() { "Fire Marshal" },
                    TypicalReviewTime = "2-3 weeks"
                },
                new PermitType
                {
                    Name = "Demolition Permit",
                    Description = "Permit for building demolition",
                    Domain = RegulatoryDomain.Building,
                    RequiredDocuments = new() { "Demolition plan", "Asbestos survey", "Utility disconnect confirmation" },
                    ReviewDepartments = new() { "Building", "Environmental" },
                    TypicalReviewTime = "1-2 weeks"
                }
            };

            foreach (var permit in permits)
            {
                _permitTypes[permit.PermitTypeId] = permit;
            }
        }

        private void InitializeInspectionTypes()
        {
            var inspections = new[]
            {
                new InspectionType { Name = "Foundation", Description = "Verify foundation per approved plans", Domain = RegulatoryDomain.Building, Phase = "Foundation", Checkpoints = new() { "Footing dimensions", "Rebar placement", "Anchor bolt locations", "Soil conditions" } },
                new InspectionType { Name = "Framing", Description = "Verify structural framing", Domain = RegulatoryDomain.Building, Phase = "Structure", Checkpoints = new() { "Member sizes", "Connections", "Shear walls", "Hold-downs" } },
                new InspectionType { Name = "Electrical Rough", Description = "Verify electrical rough-in before cover", Domain = RegulatoryDomain.Electrical, Phase = "Rough-In", Checkpoints = new() { "Wire sizing", "Box fill", "Grounding", "Code compliance" } },
                new InspectionType { Name = "Plumbing Rough", Description = "Verify plumbing rough-in before cover", Domain = RegulatoryDomain.Plumbing, Phase = "Rough-In", Checkpoints = new() { "Pipe sizing", "Venting", "Drainage slope", "Testing" } },
                new InspectionType { Name = "Mechanical Rough", Description = "Verify HVAC rough-in before cover", Domain = RegulatoryDomain.Mechanical, Phase = "Rough-In", Checkpoints = new() { "Duct sizing", "Equipment access", "Clearances", "Support" } },
                new InspectionType { Name = "Insulation", Description = "Verify insulation installation", Domain = RegulatoryDomain.Energy, Phase = "Pre-Drywall", Checkpoints = new() { "R-values", "Vapor barrier", "Air sealing", "Coverage" } },
                new InspectionType { Name = "Fire Sprinkler", Description = "Verify fire sprinkler installation", Domain = RegulatoryDomain.Fire, Phase = "Rough-In", Checkpoints = new() { "Coverage", "Spacing", "Obstruction rules", "Head type" } },
                new InspectionType { Name = "Final Building", Description = "Final building inspection", Domain = RegulatoryDomain.Building, Phase = "Final", Checkpoints = new() { "Egress", "Accessibility", "Life safety", "Finishes" } },
                new InspectionType { Name = "Fire Final", Description = "Fire marshal final inspection", Domain = RegulatoryDomain.Fire, Phase = "Final", Checkpoints = new() { "Alarm testing", "Sprinkler testing", "Egress", "Fire stopping" } }
            };

            foreach (var insp in inspections)
            {
                _inspectionTypes[insp.InspectionTypeId] = insp;
            }
        }

        private void InitializeRequirements()
        {
            var requirements = new[]
            {
                new CodeRequirement { CodeReference = "IBC 1006.2.1", Description = "Minimum of two exits required when occupant load exceeds 49", Domain = RegulatoryDomain.Building, Type = RequirementType.Prescriptive },
                new CodeRequirement { CodeReference = "IBC 1005.1", Description = "Egress width: 0.3 inch per occupant for stairs, 0.2 inch for other", Domain = RegulatoryDomain.Building, Type = RequirementType.Prescriptive },
                new CodeRequirement { CodeReference = "IBC 1020.1", Description = "Corridors serving occupant load > 30 shall be minimum 44 inches wide", Domain = RegulatoryDomain.Building, Type = RequirementType.Prescriptive, Value = "44", Unit = "inches" },
                new CodeRequirement { CodeReference = "IBC 1011.5.2", Description = "Stair riser height: max 7 inches, tread depth: min 11 inches", Domain = RegulatoryDomain.Building, Type = RequirementType.Prescriptive },
                new CodeRequirement { CodeReference = "IBC 903.2", Description = "Automatic sprinkler systems required based on occupancy and area", Domain = RegulatoryDomain.Fire, Type = RequirementType.Mandatory },
                new CodeRequirement { CodeReference = "IBC 1107.6.1", Description = "Accessible parking: 1 space per 25 total spaces (or portion)", Domain = RegulatoryDomain.Accessibility, Type = RequirementType.Prescriptive },
                new CodeRequirement { CodeReference = "IBC 1109.2", Description = "Accessible toilet rooms required on each floor with toilet facilities", Domain = RegulatoryDomain.Accessibility, Type = RequirementType.Mandatory },
                new CodeRequirement { CodeReference = "ASHRAE 62.1", Description = "Minimum outdoor air ventilation rates per occupant and area", Domain = RegulatoryDomain.Mechanical, Type = RequirementType.Performance },
                new CodeRequirement { CodeReference = "NEC 210.12", Description = "AFCI protection required in dwelling unit bedrooms", Domain = RegulatoryDomain.Electrical, Type = RequirementType.Mandatory },
                new CodeRequirement { CodeReference = "NEC 210.8", Description = "GFCI protection required for outlets near water sources", Domain = RegulatoryDomain.Electrical, Type = RequirementType.Mandatory }
            };

            foreach (var req in requirements)
            {
                _requirements[req.RequirementId] = req;
            }
        }

        #endregion

        #region Search and Query

        /// <summary>
        /// Search the regulatory knowledge base
        /// </summary>
        public async Task<RegulatorySearchResult> SearchAsync(
            RegulatoryQuery query,
            CancellationToken ct = default)
        {
            await _searchSemaphore.WaitAsync(ct);
            try
            {
                var startTime = DateTime.UtcNow;
                var results = new List<RegulatorySearchItem>();

                // Search requirements
                foreach (var req in _requirements.Values)
                {
                    if (MatchesQuery(req.Description, req.CodeReference, query))
                    {
                        results.Add(new RegulatorySearchItem
                        {
                            ItemId = req.RequirementId,
                            ItemType = "Requirement",
                            Code = req.CodeReference.Split(' ')[0],
                            Reference = req.CodeReference,
                            Title = req.CodeReference,
                            Summary = req.Description,
                            Domain = req.Domain,
                            RelevanceScore = CalculateRelevance(req.Description, req.CodeReference, query.SearchText)
                        });
                    }
                }

                // Search codes
                foreach (var code in _codes.Values)
                {
                    if (MatchesQuery(code.Name, code.Description, query))
                    {
                        results.Add(new RegulatorySearchItem
                        {
                            ItemId = code.CodeId,
                            ItemType = "Code",
                            Code = code.Abbreviation,
                            Reference = $"{code.Abbreviation} {code.Version}",
                            Title = code.Name,
                            Summary = code.Description,
                            Domain = code.Domain,
                            RelevanceScore = CalculateRelevance(code.Name, code.Description, query.SearchText)
                        });
                    }
                }

                // Search standards
                foreach (var std in _standards.Values)
                {
                    if (MatchesQuery(std.Title, std.Designation, query))
                    {
                        results.Add(new RegulatorySearchItem
                        {
                            ItemId = std.StandardId,
                            ItemType = "Standard",
                            Code = std.Designation,
                            Reference = std.Designation,
                            Title = std.Title,
                            Summary = std.Scope,
                            Domain = std.Domain,
                            RelevanceScore = CalculateRelevance(std.Title, std.Designation, query.SearchText)
                        });
                    }
                }

                // Filter and sort
                results = results
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(query.MaxResults)
                    .ToList();

                return new RegulatorySearchResult
                {
                    QueryId = query.QueryId,
                    Results = results,
                    TotalCount = results.Count,
                    SearchTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    DomainCounts = results.GroupBy(r => r.Domain.ToString())
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
            finally
            {
                _searchSemaphore.Release();
            }
        }

        private bool MatchesQuery(string title, string reference, RegulatoryQuery query)
        {
            if (string.IsNullOrEmpty(query.SearchText))
                return true;

            var searchTerms = query.SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var content = $"{title} {reference}".ToLower();

            return searchTerms.Any(term => content.Contains(term));
        }

        private double CalculateRelevance(string title, string reference, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return 0.5;

            var searchTerms = searchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titleLower = title.ToLower();
            var refLower = reference.ToLower();

            double score = 0;
            foreach (var term in searchTerms)
            {
                if (titleLower.Contains(term))
                    score += 0.5;
                if (refLower.Contains(term))
                    score += 0.3;
            }

            return Math.Min(1.0, score);
        }

        #endregion

        #region Getters

        /// <summary>
        /// Get code by abbreviation
        /// </summary>
        public RegulatoryCode? GetCode(string abbreviation)
        {
            return _codes.Values.FirstOrDefault(c =>
                c.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all codes for a domain
        /// </summary>
        public List<RegulatoryCode> GetCodesByDomain(RegulatoryDomain domain)
        {
            return _codes.Values.Where(c => c.Domain == domain).ToList();
        }

        /// <summary>
        /// Get standard by designation
        /// </summary>
        public Standard? GetStandard(string designation)
        {
            return _standards.Values.FirstOrDefault(s =>
                s.Designation.Equals(designation, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get occupancy classification by code
        /// </summary>
        public OccupancyClassification? GetOccupancy(string code)
        {
            return _occupancies.Values.FirstOrDefault(o =>
                o.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all occupancy classifications
        /// </summary>
        public List<OccupancyClassification> GetAllOccupancies()
        {
            return _occupancies.Values.ToList();
        }

        /// <summary>
        /// Get construction type by code
        /// </summary>
        public ConstructionType? GetConstructionType(string code)
        {
            return _constructionTypes.Values.FirstOrDefault(t =>
                t.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all construction types
        /// </summary>
        public List<ConstructionType> GetAllConstructionTypes()
        {
            return _constructionTypes.Values.ToList();
        }

        /// <summary>
        /// Get permit types by domain
        /// </summary>
        public List<PermitType> GetPermitTypes(RegulatoryDomain? domain = null)
        {
            return domain.HasValue
                ? _permitTypes.Values.Where(p => p.Domain == domain.Value).ToList()
                : _permitTypes.Values.ToList();
        }

        /// <summary>
        /// Get inspection types by domain
        /// </summary>
        public List<InspectionType> GetInspectionTypes(RegulatoryDomain? domain = null)
        {
            return domain.HasValue
                ? _inspectionTypes.Values.Where(i => i.Domain == domain.Value).ToList()
                : _inspectionTypes.Values.ToList();
        }

        /// <summary>
        /// Get requirements by domain
        /// </summary>
        public List<CodeRequirement> GetRequirements(RegulatoryDomain? domain = null)
        {
            return domain.HasValue
                ? _requirements.Values.Where(r => r.Domain == domain.Value).ToList()
                : _requirements.Values.ToList();
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
                ["Codes"] = _codes.Count,
                ["Standards"] = _standards.Count,
                ["Jurisdictions"] = _jurisdictions.Count,
                ["Occupancies"] = _occupancies.Count,
                ["ConstructionTypes"] = _constructionTypes.Count,
                ["PermitTypes"] = _permitTypes.Count,
                ["InspectionTypes"] = _inspectionTypes.Count,
                ["Requirements"] = _requirements.Count
            };
        }

        #endregion

        #region Disposal

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _searchSemaphore.Dispose();
            _codes.Clear();
            _standards.Clear();
            _jurisdictions.Clear();
            _occupancies.Clear();
            _constructionTypes.Clear();
            _permitTypes.Clear();
            _inspectionTypes.Clear();
            _requirements.Clear();

            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
