using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Design.Accessibility
{
    /// <summary>
    /// Accessibility compliance validation engine supporting multiple international standards.
    /// Validates building designs against ADA, BS 8300, ISO 21542, and African accessibility codes.
    /// </summary>
    public class AccessibilityComplianceValidator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, AccessibilityStandard> _standards;
        private readonly Dictionary<string, List<AccessibilityRequirement>> _requirements;
        private readonly List<ValidationResult> _validationHistory;
        private readonly object _lock = new object();

        public AccessibilityComplianceValidator()
        {
            _standards = InitializeStandards();
            _requirements = InitializeRequirements();
            _validationHistory = new List<ValidationResult>();

            Logger.Info("AccessibilityComplianceValidator initialized with {0} standards", _standards.Count);
        }

        #region Standards Database

        private Dictionary<string, AccessibilityStandard> InitializeStandards()
        {
            return new Dictionary<string, AccessibilityStandard>(StringComparer.OrdinalIgnoreCase)
            {
                ["ADA"] = new AccessibilityStandard
                {
                    Code = "ADA",
                    Name = "Americans with Disabilities Act",
                    Version = "2010 Standards",
                    Region = "USA",
                    Description = "US federal accessibility standards for public accommodations and commercial facilities",
                    Categories = new List<string> { "Circulation", "Doors", "Ramps", "Stairs", "Elevators", "Restrooms", "Signage", "Parking" }
                },
                ["BS_8300"] = new AccessibilityStandard
                {
                    Code = "BS_8300",
                    Name = "BS 8300 Design of Buildings",
                    Version = "BS 8300-1:2018, BS 8300-2:2018",
                    Region = "UK",
                    Description = "British Standard for design of accessible and inclusive built environment",
                    Categories = new List<string> { "Approach", "Horizontal_Circulation", "Vertical_Circulation", "Facilities", "Wayfinding" }
                },
                ["ISO_21542"] = new AccessibilityStandard
                {
                    Code = "ISO_21542",
                    Name = "ISO 21542 Accessibility and Usability",
                    Version = "ISO 21542:2011",
                    Region = "International",
                    Description = "International standard for accessibility and usability of the built environment",
                    Categories = new List<string> { "General", "Access_Routes", "Facilities", "Communication" }
                },
                ["UNBS_Accessibility"] = new AccessibilityStandard
                {
                    Code = "UNBS_Accessibility",
                    Name = "Uganda National Bureau of Standards - Accessibility",
                    Version = "US 1561:2020",
                    Region = "Uganda",
                    Description = "Uganda accessibility requirements for public buildings",
                    Categories = new List<string> { "Entrances", "Circulation", "Sanitary", "Emergency" }
                },
                ["KEBS_Accessibility"] = new AccessibilityStandard
                {
                    Code = "KEBS_Accessibility",
                    Name = "Kenya Bureau of Standards - Building Accessibility",
                    Version = "KS 2952:2019",
                    Region = "Kenya",
                    Description = "Kenya accessibility code for buildings and facilities",
                    Categories = new List<string> { "Approach", "Access", "Use", "Egress" }
                },
                ["SANS_10400_S"] = new AccessibilityStandard
                {
                    Code = "SANS_10400_S",
                    Name = "SANS 10400-S Facilities for Persons with Disabilities",
                    Version = "SANS 10400-S:2011",
                    Region = "South Africa",
                    Description = "South African national standard for facilities for persons with disabilities",
                    Categories = new List<string> { "Access", "Ablutions", "Signage", "Parking" }
                },
                ["EAS_Accessibility"] = new AccessibilityStandard
                {
                    Code = "EAS_Accessibility",
                    Name = "East African Standards - Accessibility",
                    Version = "EAS 981:2022",
                    Region = "East Africa",
                    Description = "East African Community harmonized accessibility standards",
                    Categories = new List<string> { "Universal_Design", "Mobility", "Vision", "Hearing" }
                }
            };
        }

        private Dictionary<string, List<AccessibilityRequirement>> InitializeRequirements()
        {
            var requirements = new Dictionary<string, List<AccessibilityRequirement>>(StringComparer.OrdinalIgnoreCase);

            // ADA Requirements
            requirements["ADA"] = new List<AccessibilityRequirement>
            {
                // Accessible Routes
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_AR_001",
                    Category = RequirementCategory.Circulation,
                    Title = "Accessible Route Width",
                    Description = "Accessible routes shall have a minimum clear width of 36 inches (915 mm)",
                    MinimumValue = 915,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 403.5.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_AR_002",
                    Category = RequirementCategory.Circulation,
                    Title = "Passing Space",
                    Description = "Accessible routes with clear width less than 60 inches shall provide passing spaces at maximum 200 feet intervals",
                    MinimumValue = 1525,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 403.5.3"
                },

                // Doors
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_DR_001",
                    Category = RequirementCategory.Doors,
                    Title = "Door Clear Width",
                    Description = "Doorways shall have a minimum clear width of 32 inches (815 mm)",
                    MinimumValue = 815,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 404.2.3"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_DR_002",
                    Category = RequirementCategory.Doors,
                    Title = "Door Maneuvering Clearance - Pull Side",
                    Description = "Maneuvering clearance on pull side with front approach: 60 inches (1525 mm) depth",
                    MinimumValue = 1525,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 404.2.4.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_DR_003",
                    Category = RequirementCategory.Doors,
                    Title = "Door Hardware Height",
                    Description = "Operable parts shall be between 34 and 48 inches (865-1220 mm) above floor",
                    MinimumValue = 865,
                    MaximumValue = 1220,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 404.2.7"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_DR_004",
                    Category = RequirementCategory.Doors,
                    Title = "Door Closing Speed",
                    Description = "Door closer shall take minimum 5 seconds to move from 90° to 12° from latch",
                    MinimumValue = 5,
                    MaximumValue = null,
                    Unit = "seconds",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 404.2.8.1"
                },

                // Ramps
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_RM_001",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Maximum Slope",
                    Description = "Ramp runs shall have a maximum slope of 1:12 (8.33%)",
                    MinimumValue = null,
                    MaximumValue = 8.33,
                    Unit = "%",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 405.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_RM_002",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Clear Width",
                    Description = "Ramps shall have a minimum clear width of 36 inches (915 mm)",
                    MinimumValue = 915,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 405.5"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_RM_003",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Rise Limit",
                    Description = "Maximum rise for any ramp run shall be 30 inches (760 mm)",
                    MinimumValue = null,
                    MaximumValue = 760,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 405.6"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_RM_004",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Landing Length",
                    Description = "Landings shall be minimum 60 inches (1525 mm) long",
                    MinimumValue = 1525,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 405.7.3"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_RM_005",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Handrails",
                    Description = "Ramps with rise greater than 6 inches shall have handrails on both sides",
                    MinimumValue = 150,
                    MaximumValue = null,
                    Unit = "mm rise trigger",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 405.8"
                },

                // Stairs
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_ST_001",
                    Category = RequirementCategory.Stairs,
                    Title = "Stair Riser Height",
                    Description = "Risers shall be 4 to 7 inches (100-180 mm) high",
                    MinimumValue = 100,
                    MaximumValue = 180,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 504.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_ST_002",
                    Category = RequirementCategory.Stairs,
                    Title = "Stair Tread Depth",
                    Description = "Treads shall be minimum 11 inches (280 mm) deep",
                    MinimumValue = 280,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 504.3"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_ST_003",
                    Category = RequirementCategory.Stairs,
                    Title = "Stair Nosing",
                    Description = "Nosings shall project no more than 1.5 inches (38 mm) beyond riser",
                    MinimumValue = null,
                    MaximumValue = 38,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Minor,
                    Reference = "ADA 504.5"
                },

                // Elevators
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_EL_001",
                    Category = RequirementCategory.Elevators,
                    Title = "Elevator Car Dimensions",
                    Description = "Elevator car minimum 51 inches (1295 mm) wide by 68 inches (1730 mm) deep",
                    MinimumValue = 1295,
                    MaximumValue = null,
                    Unit = "mm width",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 407.4.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_EL_002",
                    Category = RequirementCategory.Elevators,
                    Title = "Elevator Door Width",
                    Description = "Elevator doors shall have a minimum clear width of 36 inches (915 mm)",
                    MinimumValue = 915,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 407.4.1"
                },

                // Restrooms
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_001",
                    Category = RequirementCategory.Restrooms,
                    Title = "Wheelchair Accessible Stall Width",
                    Description = "Accessible toilet compartment minimum 60 inches (1525 mm) wide",
                    MinimumValue = 1525,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 604.8.1.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_002",
                    Category = RequirementCategory.Restrooms,
                    Title = "Accessible Stall Depth",
                    Description = "Accessible toilet compartment minimum 56 inches (1420 mm) deep (wall-hung) or 59 inches (1500 mm) (floor-mounted)",
                    MinimumValue = 1420,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 604.8.1.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_003",
                    Category = RequirementCategory.Restrooms,
                    Title = "Toilet Seat Height",
                    Description = "Toilet seat height shall be 17 to 19 inches (430-485 mm) above floor",
                    MinimumValue = 430,
                    MaximumValue = 485,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 604.4"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_004",
                    Category = RequirementCategory.Restrooms,
                    Title = "Grab Bar - Side Wall",
                    Description = "Side wall grab bar minimum 42 inches (1065 mm) long",
                    MinimumValue = 1065,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 604.5.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_005",
                    Category = RequirementCategory.Restrooms,
                    Title = "Lavatory Clear Floor Space",
                    Description = "Clear floor space for forward approach minimum 30 x 48 inches (760 x 1220 mm)",
                    MinimumValue = 760,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 606.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_WC_006",
                    Category = RequirementCategory.Restrooms,
                    Title = "Lavatory Height",
                    Description = "Lavatory rim maximum 34 inches (865 mm) above floor",
                    MinimumValue = null,
                    MaximumValue = 865,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 606.3"
                },

                // Parking
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_PK_001",
                    Category = RequirementCategory.Parking,
                    Title = "Accessible Parking Space Width",
                    Description = "Car accessible parking spaces minimum 96 inches (2440 mm) wide",
                    MinimumValue = 2440,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 502.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_PK_002",
                    Category = RequirementCategory.Parking,
                    Title = "Van Accessible Space Width",
                    Description = "Van accessible parking spaces minimum 132 inches (3350 mm) wide",
                    MinimumValue = 3350,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 502.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_PK_003",
                    Category = RequirementCategory.Parking,
                    Title = "Access Aisle Width",
                    Description = "Access aisle minimum 60 inches (1525 mm) wide",
                    MinimumValue = 1525,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "ADA 502.3.1"
                },

                // Signage
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_SG_001",
                    Category = RequirementCategory.Signage,
                    Title = "Tactile Characters",
                    Description = "Characters shall be raised 1/32 inch (0.8 mm) minimum above background",
                    MinimumValue = 0.8,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 703.2.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "ADA_SG_002",
                    Category = RequirementCategory.Signage,
                    Title = "Sign Mounting Height",
                    Description = "Tactile signs shall be mounted 48 to 60 inches (1220-1525 mm) above floor",
                    MinimumValue = 1220,
                    MaximumValue = 1525,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Major,
                    Reference = "ADA 703.4.1"
                }
            };

            // BS 8300 Requirements (UK)
            requirements["BS_8300"] = new List<AccessibilityRequirement>
            {
                new AccessibilityRequirement
                {
                    RequirementId = "BS_AR_001",
                    Category = RequirementCategory.Circulation,
                    Title = "Corridor Minimum Width",
                    Description = "Corridors should have minimum unobstructed width of 1200mm",
                    MinimumValue = 1200,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "BS 8300-2:2018 8.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "BS_AR_002",
                    Category = RequirementCategory.Circulation,
                    Title = "Corridor Preferred Width",
                    Description = "Preferred corridor width for two-way traffic is 1800mm",
                    MinimumValue = 1800,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Minor,
                    Reference = "BS 8300-2:2018 8.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "BS_DR_001",
                    Category = RequirementCategory.Doors,
                    Title = "Door Clear Opening Width",
                    Description = "Minimum effective clear width of 800mm when door open at 90°",
                    MinimumValue = 800,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "BS 8300-2:2018 9.2.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "BS_RM_001",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Gradient",
                    Description = "Maximum gradient 1:12 for ramps used by wheelchair users",
                    MinimumValue = null,
                    MaximumValue = 8.33,
                    Unit = "%",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "BS 8300-1:2018 9.3"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "BS_RM_002",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Width",
                    Description = "Minimum clear width of 1500mm between handrails",
                    MinimumValue = 1500,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "BS 8300-1:2018 9.3"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "BS_WC_001",
                    Category = RequirementCategory.Restrooms,
                    Title = "Accessible WC Compartment Size",
                    Description = "Minimum 1500mm x 2200mm for corner WC layout",
                    MinimumValue = 1500,
                    MaximumValue = null,
                    Unit = "mm width",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "BS 8300-2:2018 18.3"
                }
            };

            // East African Standards
            requirements["EAS_Accessibility"] = new List<AccessibilityRequirement>
            {
                new AccessibilityRequirement
                {
                    RequirementId = "EAS_AR_001",
                    Category = RequirementCategory.Circulation,
                    Title = "Accessible Path Width",
                    Description = "Accessible paths minimum 1200mm clear width",
                    MinimumValue = 1200,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "EAS 981:2022 6.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "EAS_DR_001",
                    Category = RequirementCategory.Doors,
                    Title = "Door Clear Width",
                    Description = "Minimum door clear width of 850mm",
                    MinimumValue = 850,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "EAS 981:2022 7.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "EAS_RM_001",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Slope",
                    Description = "Maximum ramp slope 1:12 (8.33%)",
                    MinimumValue = null,
                    MaximumValue = 8.33,
                    Unit = "%",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "EAS 981:2022 8.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "EAS_RM_002",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Width",
                    Description = "Minimum ramp width 1200mm",
                    MinimumValue = 1200,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "EAS 981:2022 8.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "EAS_WC_001",
                    Category = RequirementCategory.Restrooms,
                    Title = "Accessible Toilet Size",
                    Description = "Minimum 1500mm x 1800mm accessible toilet compartment",
                    MinimumValue = 1500,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "EAS 981:2022 10.1"
                }
            };

            // Uganda Standards
            requirements["UNBS_Accessibility"] = new List<AccessibilityRequirement>
            {
                new AccessibilityRequirement
                {
                    RequirementId = "UNBS_AR_001",
                    Category = RequirementCategory.Circulation,
                    Title = "Access Route Width",
                    Description = "Minimum 1100mm clear width for accessible routes",
                    MinimumValue = 1100,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "US 1561:2020 5.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "UNBS_RM_001",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Gradient",
                    Description = "Maximum gradient 1:12 for ramps",
                    MinimumValue = null,
                    MaximumValue = 8.33,
                    Unit = "%",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "US 1561:2020 6.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "UNBS_WC_001",
                    Category = RequirementCategory.Restrooms,
                    Title = "Accessible WC Dimensions",
                    Description = "Minimum 1500mm x 1800mm for wheelchair accessible toilet",
                    MinimumValue = 1500,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "US 1561:2020 8.1"
                }
            };

            // South Africa SANS
            requirements["SANS_10400_S"] = new List<AccessibilityRequirement>
            {
                new AccessibilityRequirement
                {
                    RequirementId = "SANS_AR_001",
                    Category = RequirementCategory.Circulation,
                    Title = "Accessible Route Width",
                    Description = "Minimum 1100mm clear width, 1500mm preferred",
                    MinimumValue = 1100,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "SANS 10400-S 4.3.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "SANS_DR_001",
                    Category = RequirementCategory.Doors,
                    Title = "Door Width",
                    Description = "Minimum clear opening width 750mm, recommended 850mm",
                    MinimumValue = 750,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "SANS 10400-S 4.4.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "SANS_RM_001",
                    Category = RequirementCategory.Ramps,
                    Title = "Ramp Gradient",
                    Description = "Maximum 1:12 gradient for ramps exceeding 500mm rise",
                    MinimumValue = null,
                    MaximumValue = 8.33,
                    Unit = "%",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "SANS 10400-S 4.5.2"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "SANS_WC_001",
                    Category = RequirementCategory.Restrooms,
                    Title = "Accessible Toilet",
                    Description = "Minimum 2000mm x 1750mm accessible toilet facility",
                    MinimumValue = 1750,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "SANS 10400-S 4.7.1"
                },
                new AccessibilityRequirement
                {
                    RequirementId = "SANS_PK_001",
                    Category = RequirementCategory.Parking,
                    Title = "Accessible Parking Bay",
                    Description = "Minimum 3500mm wide with 2400mm access aisle",
                    MinimumValue = 3500,
                    MaximumValue = null,
                    Unit = "mm",
                    Severity = ComplianceSeverity.Critical,
                    Reference = "SANS 10400-S 4.2.2"
                }
            };

            return requirements;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validates a building design against specified accessibility standards.
        /// </summary>
        public async Task<ValidationResult> ValidateDesignAsync(
            AccessibilityValidationInput input,
            string standardCode = "ADA")
        {
            Logger.Info("Validating design '{0}' against {1} standard", input.ProjectName, standardCode);

            if (!_standards.ContainsKey(standardCode))
            {
                throw new ArgumentException($"Unknown standard code: {standardCode}");
            }

            var result = new ValidationResult
            {
                ProjectName = input.ProjectName,
                StandardCode = standardCode,
                StandardName = _standards[standardCode].Name,
                ValidationDate = DateTime.UtcNow,
                Issues = new List<ValidationIssue>(),
                PassedRequirements = new List<string>()
            };

            await Task.Run(() =>
            {
                if (!_requirements.TryGetValue(standardCode, out var standardRequirements))
                {
                    Logger.Warn("No requirements defined for standard: {0}", standardCode);
                    return;
                }

                foreach (var requirement in standardRequirements)
                {
                    var issue = ValidateRequirement(requirement, input);
                    if (issue != null)
                    {
                        result.Issues.Add(issue);
                    }
                    else
                    {
                        result.PassedRequirements.Add(requirement.RequirementId);
                    }
                }
            });

            // Calculate compliance score
            int totalRequirements = result.Issues.Count + result.PassedRequirements.Count;
            result.ComplianceScore = totalRequirements > 0
                ? (double)result.PassedRequirements.Count / totalRequirements
                : 1.0;

            result.IsCompliant = !result.Issues.Any(i => i.Severity == ComplianceSeverity.Critical);
            result.CriticalIssueCount = result.Issues.Count(i => i.Severity == ComplianceSeverity.Critical);
            result.MajorIssueCount = result.Issues.Count(i => i.Severity == ComplianceSeverity.Major);
            result.MinorIssueCount = result.Issues.Count(i => i.Severity == ComplianceSeverity.Minor);

            // Generate recommendations
            result.Recommendations = GenerateRecommendations(result.Issues);

            // Store in history
            lock (_lock)
            {
                _validationHistory.Add(result);
            }

            Logger.Info("Validation complete: {0:P0} compliant, {1} critical, {2} major, {3} minor issues",
                result.ComplianceScore, result.CriticalIssueCount, result.MajorIssueCount, result.MinorIssueCount);

            return result;
        }

        private ValidationIssue ValidateRequirement(AccessibilityRequirement requirement, AccessibilityValidationInput input)
        {
            double? actualValue = GetActualValue(requirement, input);

            if (!actualValue.HasValue)
            {
                // Data not provided - cannot validate
                return null;
            }

            bool isCompliant = true;

            if (requirement.MinimumValue.HasValue && actualValue.Value < requirement.MinimumValue.Value)
            {
                isCompliant = false;
            }

            if (requirement.MaximumValue.HasValue && actualValue.Value > requirement.MaximumValue.Value)
            {
                isCompliant = false;
            }

            if (!isCompliant)
            {
                return new ValidationIssue
                {
                    RequirementId = requirement.RequirementId,
                    Category = requirement.Category,
                    Title = requirement.Title,
                    Description = requirement.Description,
                    Severity = requirement.Severity,
                    Reference = requirement.Reference,
                    ActualValue = actualValue.Value,
                    RequiredMinimum = requirement.MinimumValue,
                    RequiredMaximum = requirement.MaximumValue,
                    Unit = requirement.Unit,
                    Location = GetIssueLocation(requirement, input),
                    SuggestedFix = GenerateSuggestedFix(requirement, actualValue.Value)
                };
            }

            return null;
        }

        private double? GetActualValue(AccessibilityRequirement requirement, AccessibilityValidationInput input)
        {
            // Map requirements to input values
            return requirement.RequirementId switch
            {
                // Circulation
                "ADA_AR_001" or "BS_AR_001" or "EAS_AR_001" or "UNBS_AR_001" or "SANS_AR_001" => input.CorridorWidth,
                "ADA_AR_002" => input.PassingSpaceWidth,
                "BS_AR_002" => input.CorridorWidth,

                // Doors
                "ADA_DR_001" or "BS_DR_001" or "EAS_DR_001" or "SANS_DR_001" => input.DoorClearWidth,
                "ADA_DR_002" => input.DoorManeuveringDepth,
                "ADA_DR_003" => input.DoorHardwareHeight,
                "ADA_DR_004" => input.DoorClosingTime,

                // Ramps
                "ADA_RM_001" or "BS_RM_001" or "EAS_RM_001" or "UNBS_RM_001" or "SANS_RM_001" => input.RampSlope,
                "ADA_RM_002" or "BS_RM_002" or "EAS_RM_002" => input.RampWidth,
                "ADA_RM_003" => input.RampRise,
                "ADA_RM_004" => input.RampLandingLength,
                "ADA_RM_005" => input.RampRise, // Trigger check

                // Stairs
                "ADA_ST_001" => input.StairRiserHeight,
                "ADA_ST_002" => input.StairTreadDepth,
                "ADA_ST_003" => input.StairNosing,

                // Elevators
                "ADA_EL_001" => input.ElevatorWidth,
                "ADA_EL_002" => input.ElevatorDoorWidth,

                // Restrooms
                "ADA_WC_001" or "BS_WC_001" or "EAS_WC_001" or "UNBS_WC_001" or "SANS_WC_001" => input.AccessibleToiletWidth,
                "ADA_WC_002" => input.AccessibleToiletDepth,
                "ADA_WC_003" => input.ToiletSeatHeight,
                "ADA_WC_004" => input.GrabBarLength,
                "ADA_WC_005" => input.LavatoryClearance,
                "ADA_WC_006" => input.LavatoryHeight,

                // Parking
                "ADA_PK_001" or "SANS_PK_001" => input.AccessibleParkingWidth,
                "ADA_PK_002" => input.VanAccessibleParkingWidth,
                "ADA_PK_003" => input.AccessAisleWidth,

                // Signage
                "ADA_SG_001" => input.TactileCharacterHeight,
                "ADA_SG_002" => input.SignMountingHeight,

                _ => null
            };
        }

        private string GetIssueLocation(AccessibilityRequirement requirement, AccessibilityValidationInput input)
        {
            return requirement.Category switch
            {
                RequirementCategory.Circulation => input.CirculationLocation ?? "Accessible Route",
                RequirementCategory.Doors => input.DoorLocation ?? "Doors",
                RequirementCategory.Ramps => input.RampLocation ?? "Ramps",
                RequirementCategory.Stairs => input.StairLocation ?? "Stairs",
                RequirementCategory.Elevators => input.ElevatorLocation ?? "Elevators",
                RequirementCategory.Restrooms => input.RestroomLocation ?? "Restrooms",
                RequirementCategory.Parking => input.ParkingLocation ?? "Parking",
                RequirementCategory.Signage => input.SignageLocation ?? "Signage",
                _ => "Building"
            };
        }

        private string GenerateSuggestedFix(AccessibilityRequirement requirement, double actualValue)
        {
            if (requirement.MinimumValue.HasValue && actualValue < requirement.MinimumValue.Value)
            {
                double difference = requirement.MinimumValue.Value - actualValue;
                return $"Increase {requirement.Title.ToLower()} by at least {difference:N0} {requirement.Unit} to meet minimum of {requirement.MinimumValue:N0} {requirement.Unit}";
            }

            if (requirement.MaximumValue.HasValue && actualValue > requirement.MaximumValue.Value)
            {
                double difference = actualValue - requirement.MaximumValue.Value;
                return $"Reduce {requirement.Title.ToLower()} by at least {difference:N0} {requirement.Unit} to meet maximum of {requirement.MaximumValue:N0} {requirement.Unit}";
            }

            return "Review design to meet requirement";
        }

        private List<AccessibilityRecommendation> GenerateRecommendations(List<ValidationIssue> issues)
        {
            var recommendations = new List<AccessibilityRecommendation>();

            // Group issues by category
            var issuesByCategory = issues.GroupBy(i => i.Category);

            foreach (var categoryGroup in issuesByCategory)
            {
                var criticalCount = categoryGroup.Count(i => i.Severity == ComplianceSeverity.Critical);
                var totalCount = categoryGroup.Count();

                recommendations.Add(new AccessibilityRecommendation
                {
                    Category = categoryGroup.Key,
                    Title = $"Address {categoryGroup.Key} accessibility issues",
                    Description = $"{totalCount} issue(s) identified in {categoryGroup.Key} category, including {criticalCount} critical",
                    Priority = criticalCount > 0 ? RecommendationPriority.Critical :
                              totalCount > 2 ? RecommendationPriority.High :
                              RecommendationPriority.Medium,
                    EstimatedImpact = criticalCount > 0 ? "Required for compliance" : "Improves accessibility",
                    AffectedElements = categoryGroup.Select(i => i.Location).Distinct().ToList()
                });
            }

            // Add general recommendations based on patterns
            if (issues.Any(i => i.Category == RequirementCategory.Ramps && i.Severity == ComplianceSeverity.Critical))
            {
                recommendations.Add(new AccessibilityRecommendation
                {
                    Category = RequirementCategory.Ramps,
                    Title = "Consider platform lift alternative",
                    Description = "Where ramp slope requirements cannot be met due to space constraints, consider platform lift as alternative",
                    Priority = RecommendationPriority.Medium,
                    EstimatedImpact = "Alternative compliance path",
                    AffectedElements = new List<string> { "Level changes" }
                });
            }

            if (!issues.Any(i => i.Category == RequirementCategory.Signage))
            {
                recommendations.Add(new AccessibilityRecommendation
                {
                    Category = RequirementCategory.Signage,
                    Title = "Verify wayfinding signage",
                    Description = "Ensure tactile and visual signage is provided at key decision points and room identifiers",
                    Priority = RecommendationPriority.Low,
                    EstimatedImpact = "Enhanced user experience",
                    AffectedElements = new List<string> { "All floors" }
                });
            }

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        #endregion

        #region Multi-Standard Validation

        /// <summary>
        /// Validates against multiple standards simultaneously.
        /// </summary>
        public async Task<MultiStandardValidationResult> ValidateAgainstMultipleStandardsAsync(
            AccessibilityValidationInput input,
            IEnumerable<string> standardCodes)
        {
            Logger.Info("Validating design against {0} standards", standardCodes.Count());

            var result = new MultiStandardValidationResult
            {
                ProjectName = input.ProjectName,
                ValidationDate = DateTime.UtcNow,
                StandardResults = new Dictionary<string, ValidationResult>()
            };

            var tasks = standardCodes.Select(code => ValidateDesignAsync(input, code));
            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < standardCodes.Count(); i++)
            {
                result.StandardResults[standardCodes.ElementAt(i)] = results[i];
            }

            // Find common issues across standards
            result.CommonIssues = FindCommonIssues(results);

            // Calculate overall compliance
            result.OverallCompliance = results.All(r => r.IsCompliant);
            result.AverageComplianceScore = results.Average(r => r.ComplianceScore);

            return result;
        }

        private List<CommonIssue> FindCommonIssues(ValidationResult[] results)
        {
            var commonIssues = new List<CommonIssue>();

            // Group all issues by category and find overlaps
            var allIssues = results.SelectMany(r => r.Issues).ToList();
            var categoryGroups = allIssues.GroupBy(i => i.Category);

            foreach (var group in categoryGroups)
            {
                if (group.Count() >= results.Length / 2) // Issue appears in at least half of standards
                {
                    commonIssues.Add(new CommonIssue
                    {
                        Category = group.Key,
                        Title = group.First().Title,
                        AffectedStandards = group.Select(i =>
                            results.First(r => r.Issues.Contains(i)).StandardCode).Distinct().ToList(),
                        MaxSeverity = group.Max(i => i.Severity)
                    });
                }
            }

            return commonIssues;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets available accessibility standards.
        /// </summary>
        public IEnumerable<AccessibilityStandard> GetAvailableStandards()
        {
            return _standards.Values;
        }

        /// <summary>
        /// Gets requirements for a specific standard.
        /// </summary>
        public IEnumerable<AccessibilityRequirement> GetRequirements(string standardCode)
        {
            return _requirements.TryGetValue(standardCode, out var reqs) ? reqs : Enumerable.Empty<AccessibilityRequirement>();
        }

        /// <summary>
        /// Gets requirements by category for a standard.
        /// </summary>
        public IEnumerable<AccessibilityRequirement> GetRequirementsByCategory(string standardCode, RequirementCategory category)
        {
            if (_requirements.TryGetValue(standardCode, out var reqs))
            {
                return reqs.Where(r => r.Category == category);
            }
            return Enumerable.Empty<AccessibilityRequirement>();
        }

        /// <summary>
        /// Gets validation history.
        /// </summary>
        public IEnumerable<ValidationResult> GetValidationHistory()
        {
            lock (_lock)
            {
                return _validationHistory.ToList();
            }
        }

        /// <summary>
        /// Gets recommended standards for a region.
        /// </summary>
        public List<string> GetRecommendedStandards(string region)
        {
            var recommendations = new List<string>();

            switch (region?.ToLower())
            {
                case "uganda":
                    recommendations.AddRange(new[] { "UNBS_Accessibility", "EAS_Accessibility", "BS_8300" });
                    break;
                case "kenya":
                    recommendations.AddRange(new[] { "KEBS_Accessibility", "EAS_Accessibility", "BS_8300" });
                    break;
                case "south africa":
                    recommendations.AddRange(new[] { "SANS_10400_S", "ISO_21542" });
                    break;
                case "usa":
                case "united states":
                    recommendations.AddRange(new[] { "ADA" });
                    break;
                case "uk":
                case "united kingdom":
                    recommendations.AddRange(new[] { "BS_8300", "ISO_21542" });
                    break;
                default:
                    recommendations.AddRange(new[] { "ISO_21542", "ADA" });
                    break;
            }

            return recommendations;
        }

        #endregion
    }

    #region Data Models

    public class AccessibilityStandard
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Region { get; set; }
        public string Description { get; set; }
        public List<string> Categories { get; set; }
    }

    public class AccessibilityRequirement
    {
        public string RequirementId { get; set; }
        public RequirementCategory Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double? MinimumValue { get; set; }
        public double? MaximumValue { get; set; }
        public string Unit { get; set; }
        public ComplianceSeverity Severity { get; set; }
        public string Reference { get; set; }
    }

    public enum RequirementCategory
    {
        Circulation,
        Doors,
        Ramps,
        Stairs,
        Elevators,
        Restrooms,
        Parking,
        Signage,
        Emergency,
        Furniture
    }

    public enum ComplianceSeverity
    {
        Minor,
        Major,
        Critical
    }

    public class AccessibilityValidationInput
    {
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public string Region { get; set; }

        // Circulation
        public double? CorridorWidth { get; set; }
        public double? PassingSpaceWidth { get; set; }
        public string CirculationLocation { get; set; }

        // Doors
        public double? DoorClearWidth { get; set; }
        public double? DoorManeuveringDepth { get; set; }
        public double? DoorHardwareHeight { get; set; }
        public double? DoorClosingTime { get; set; }
        public string DoorLocation { get; set; }

        // Ramps
        public double? RampSlope { get; set; }
        public double? RampWidth { get; set; }
        public double? RampRise { get; set; }
        public double? RampLandingLength { get; set; }
        public string RampLocation { get; set; }

        // Stairs
        public double? StairRiserHeight { get; set; }
        public double? StairTreadDepth { get; set; }
        public double? StairNosing { get; set; }
        public string StairLocation { get; set; }

        // Elevators
        public double? ElevatorWidth { get; set; }
        public double? ElevatorDepth { get; set; }
        public double? ElevatorDoorWidth { get; set; }
        public string ElevatorLocation { get; set; }

        // Restrooms
        public double? AccessibleToiletWidth { get; set; }
        public double? AccessibleToiletDepth { get; set; }
        public double? ToiletSeatHeight { get; set; }
        public double? GrabBarLength { get; set; }
        public double? LavatoryClearance { get; set; }
        public double? LavatoryHeight { get; set; }
        public string RestroomLocation { get; set; }

        // Parking
        public double? AccessibleParkingWidth { get; set; }
        public double? VanAccessibleParkingWidth { get; set; }
        public double? AccessAisleWidth { get; set; }
        public string ParkingLocation { get; set; }

        // Signage
        public double? TactileCharacterHeight { get; set; }
        public double? SignMountingHeight { get; set; }
        public string SignageLocation { get; set; }
    }

    public class ValidationResult
    {
        public string ProjectName { get; set; }
        public string StandardCode { get; set; }
        public string StandardName { get; set; }
        public DateTime ValidationDate { get; set; }
        public bool IsCompliant { get; set; }
        public double ComplianceScore { get; set; }
        public int CriticalIssueCount { get; set; }
        public int MajorIssueCount { get; set; }
        public int MinorIssueCount { get; set; }
        public List<ValidationIssue> Issues { get; set; }
        public List<string> PassedRequirements { get; set; }
        public List<AccessibilityRecommendation> Recommendations { get; set; }
    }

    public class ValidationIssue
    {
        public string RequirementId { get; set; }
        public RequirementCategory Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ComplianceSeverity Severity { get; set; }
        public string Reference { get; set; }
        public double ActualValue { get; set; }
        public double? RequiredMinimum { get; set; }
        public double? RequiredMaximum { get; set; }
        public string Unit { get; set; }
        public string Location { get; set; }
        public string SuggestedFix { get; set; }
    }

    public class AccessibilityRecommendation
    {
        public RequirementCategory Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string EstimatedImpact { get; set; }
        public List<string> AffectedElements { get; set; }
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class MultiStandardValidationResult
    {
        public string ProjectName { get; set; }
        public DateTime ValidationDate { get; set; }
        public Dictionary<string, ValidationResult> StandardResults { get; set; }
        public List<CommonIssue> CommonIssues { get; set; }
        public bool OverallCompliance { get; set; }
        public double AverageComplianceScore { get; set; }
    }

    public class CommonIssue
    {
        public RequirementCategory Category { get; set; }
        public string Title { get; set; }
        public List<string> AffectedStandards { get; set; }
        public ComplianceSeverity MaxSeverity { get; set; }
    }

    #endregion
}
