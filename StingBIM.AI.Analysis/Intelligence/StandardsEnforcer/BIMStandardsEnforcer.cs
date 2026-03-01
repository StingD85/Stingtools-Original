// ============================================================================
// StingBIM AI - BIM Standards Enforcer
// Automated compliance checking, naming conventions, and standards validation
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StingBIM.AI.Intelligence.StandardsEnforcer
{
    /// <summary>
    /// BIM Standards Enforcer for automated compliance validation,
    /// naming convention checks, and standards adherence monitoring.
    /// </summary>
    public sealed class BIMStandardsEnforcer
    {
        private static readonly Lazy<BIMStandardsEnforcer> _instance =
            new Lazy<BIMStandardsEnforcer>(() => new BIMStandardsEnforcer());
        public static BIMStandardsEnforcer Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, StandardsProfile> _profiles = new();
        private readonly Dictionary<string, ComplianceReport> _reports = new();
        private readonly List<NamingRule> _namingRules = new();
        private readonly List<ParameterRule> _parameterRules = new();
        private readonly List<ModelRule> _modelRules = new();
        private readonly List<ClassificationSystem> _classificationSystems = new();

        public event EventHandler<ComplianceEventArgs> ComplianceViolation;

        private BIMStandardsEnforcer()
        {
            InitializeNamingRules();
            InitializeParameterRules();
            InitializeModelRules();
            InitializeClassificationSystems();
        }

        #region Initialization

        private void InitializeNamingRules()
        {
            _namingRules.AddRange(new[]
            {
                // File naming rules
                new NamingRule
                {
                    RuleId = "NR-FILE-001",
                    Name = "Model File Naming",
                    Category = NamingCategory.File,
                    Pattern = @"^[A-Z0-9]{2,6}-[A-Z]{2,3}-[A-Z]{2}-[A-Z0-9]{2,4}-[A-Z]{1,2}-[A-Z]{2}-\d{4}$",
                    Description = "Model files must follow ISO 19650 naming convention",
                    Example = "PROJ01-ARC-ZA-L01-M3-AR-0001",
                    Fields = new List<NamingField>
                    {
                        new NamingField { Name = "Project", Position = 0, Required = true },
                        new NamingField { Name = "Originator", Position = 1, Required = true },
                        new NamingField { Name = "Zone", Position = 2, Required = true },
                        new NamingField { Name = "Level", Position = 3, Required = true },
                        new NamingField { Name = "FileType", Position = 4, Required = true },
                        new NamingField { Name = "Discipline", Position = 5, Required = true },
                        new NamingField { Name = "Number", Position = 6, Required = true }
                    }
                },
                new NamingRule
                {
                    RuleId = "NR-FILE-002",
                    Name = "Drawing File Naming",
                    Category = NamingCategory.File,
                    Pattern = @"^[A-Z0-9]{2,6}-[A-Z]{2,3}-[A-Z0-9]{2,4}-[A-Z]{2}-\d{4}$",
                    Description = "Drawing files must follow naming convention",
                    Example = "PROJ01-ARC-L01-AR-0001"
                },

                // Family naming rules
                new NamingRule
                {
                    RuleId = "NR-FAM-001",
                    Name = "Family Naming Convention",
                    Category = NamingCategory.Family,
                    Pattern = @"^[A-Z][A-Za-z0-9]+_[A-Za-z0-9_]+$",
                    Description = "Families should use Category_Description format",
                    Example = "Door_SingleFlush_900x2100"
                },
                new NamingRule
                {
                    RuleId = "NR-FAM-002",
                    Name = "No Spaces in Family Names",
                    Category = NamingCategory.Family,
                    Pattern = @"^[^\s]+$",
                    Description = "Family names should not contain spaces"
                },

                // Type naming rules
                new NamingRule
                {
                    RuleId = "NR-TYPE-001",
                    Name = "Wall Type Naming",
                    Category = NamingCategory.Type,
                    Pattern = @"^(EXT|INT|PTN|CW|FW)-[A-Z0-9]+-\d+$",
                    Description = "Wall types should include prefix and thickness",
                    Example = "EXT-BRK-300"
                },
                new NamingRule
                {
                    RuleId = "NR-TYPE-002",
                    Name = "Door Type Naming",
                    Category = NamingCategory.Type,
                    Pattern = @"^\d{3,4}x\d{4}(-[A-Z]+)?$",
                    Description = "Door types should include dimensions",
                    Example = "900x2100-SGL"
                },

                // View naming rules
                new NamingRule
                {
                    RuleId = "NR-VIEW-001",
                    Name = "View Naming Convention",
                    Category = NamingCategory.View,
                    Pattern = @"^(AR|ST|ME|EL|PL|FP)[-_][A-Z]+[-_].+$",
                    Description = "Views should include discipline prefix",
                    Example = "AR_PLAN_Level 01"
                },
                new NamingRule
                {
                    RuleId = "NR-VIEW-002",
                    Name = "Working View Prefix",
                    Category = NamingCategory.View,
                    Pattern = @"^(WIP|WORK|_).+$",
                    Description = "Working views must have WIP prefix",
                    Example = "WIP_Section Study"
                },

                // Sheet naming rules
                new NamingRule
                {
                    RuleId = "NR-SHEET-001",
                    Name = "Sheet Number Format",
                    Category = NamingCategory.Sheet,
                    Pattern = @"^[A-Z]\d{3}$",
                    Description = "Sheet numbers should be DisciplineNNN format",
                    Example = "A101"
                },

                // Parameter naming rules
                new NamingRule
                {
                    RuleId = "NR-PARAM-001",
                    Name = "Custom Parameter Prefix",
                    Category = NamingCategory.Parameter,
                    Pattern = @"^(PROJ|STG|MR)_[A-Za-z0-9_]+$",
                    Description = "Custom parameters should have project prefix",
                    Example = "PROJ_CostCode"
                },

                // Workset naming rules
                new NamingRule
                {
                    RuleId = "NR-WS-001",
                    Name = "Workset Naming",
                    Category = NamingCategory.Workset,
                    Pattern = @"^\d{2}_[A-Za-z_]+$",
                    Description = "Worksets should have numbered prefix",
                    Example = "02_Shell"
                }
            });
        }

        private void InitializeParameterRules()
        {
            _parameterRules.AddRange(new[]
            {
                new ParameterRule
                {
                    RuleId = "PR-REQ-001",
                    Name = "Classification Code Required",
                    ParameterName = "ClassificationCode",
                    IsRequired = true,
                    Categories = new List<string> { "All" },
                    Description = "All elements must have classification code"
                },
                new ParameterRule
                {
                    RuleId = "PR-REQ-002",
                    Name = "Manufacturer Required for Equipment",
                    ParameterName = "Manufacturer",
                    IsRequired = true,
                    Categories = new List<string> { "Mechanical Equipment", "Electrical Equipment", "Plumbing Fixtures" },
                    Description = "Equipment must have manufacturer specified"
                },
                new ParameterRule
                {
                    RuleId = "PR-REQ-003",
                    Name = "Model Number Required for Equipment",
                    ParameterName = "Model",
                    IsRequired = true,
                    Categories = new List<string> { "Mechanical Equipment", "Electrical Equipment" },
                    Description = "Equipment must have model number"
                },
                new ParameterRule
                {
                    RuleId = "PR-REQ-004",
                    Name = "Fire Rating Required for Fire Assemblies",
                    ParameterName = "FireRating",
                    IsRequired = true,
                    Categories = new List<string> { "Doors", "Walls" },
                    Condition = "IsFireRated = true",
                    Description = "Fire-rated assemblies must have fire rating value"
                },
                new ParameterRule
                {
                    RuleId = "PR-FORMAT-001",
                    Name = "Mark Format",
                    ParameterName = "Mark",
                    Pattern = @"^[A-Z0-9]+-\d+$",
                    Categories = new List<string> { "Doors", "Windows" },
                    Description = "Door/Window marks should be formatted as TYPE-NUMBER"
                },
                new ParameterRule
                {
                    RuleId = "PR-RANGE-001",
                    Name = "Valid Fire Rating Values",
                    ParameterName = "FireRating",
                    AllowedValues = new List<string> { "0", "20", "30", "45", "60", "90", "120", "180", "240" },
                    Categories = new List<string> { "Doors", "Walls" },
                    Description = "Fire rating must be a standard value in minutes"
                },
                new ParameterRule
                {
                    RuleId = "PR-COBie-001",
                    Name = "COBie AssetType Required",
                    ParameterName = "COBie.Type.AssetType",
                    IsRequired = true,
                    Categories = new List<string> { "Mechanical Equipment", "Electrical Equipment" },
                    Description = "Equipment must have COBie asset type for handover"
                }
            });
        }

        private void InitializeModelRules()
        {
            _modelRules.AddRange(new[]
            {
                new ModelRule
                {
                    RuleId = "MR-COORD-001",
                    Name = "Shared Coordinates Required",
                    Category = ModelRuleCategory.Coordinates,
                    Description = "Model must use shared coordinates",
                    Severity = RuleSeverity.Error
                },
                new ModelRule
                {
                    RuleId = "MR-COORD-002",
                    Name = "Project Base Point Set",
                    Category = ModelRuleCategory.Coordinates,
                    Description = "Project base point must be set correctly",
                    Severity = RuleSeverity.Error
                },
                new ModelRule
                {
                    RuleId = "MR-LEVEL-001",
                    Name = "Level Naming Convention",
                    Category = ModelRuleCategory.Levels,
                    Description = "Levels must follow naming convention",
                    Severity = RuleSeverity.Warning
                },
                new ModelRule
                {
                    RuleId = "MR-GRID-001",
                    Name = "Grid Naming Convention",
                    Category = ModelRuleCategory.Grids,
                    Description = "Grids must use numeric/alphabetic naming",
                    Severity = RuleSeverity.Warning
                },
                new ModelRule
                {
                    RuleId = "MR-ROOM-001",
                    Name = "Room Bounding Complete",
                    Category = ModelRuleCategory.Rooms,
                    Description = "All rooms must be properly bounded",
                    Severity = RuleSeverity.Error
                },
                new ModelRule
                {
                    RuleId = "MR-ROOM-002",
                    Name = "Room Numbers Unique",
                    Category = ModelRuleCategory.Rooms,
                    Description = "Room numbers must be unique",
                    Severity = RuleSeverity.Error
                },
                new ModelRule
                {
                    RuleId = "MR-LINK-001",
                    Name = "Links on Correct Workset",
                    Category = ModelRuleCategory.Links,
                    Description = "Linked models must be on Links workset",
                    Severity = RuleSeverity.Warning
                },
                new ModelRule
                {
                    RuleId = "MR-WARN-001",
                    Name = "Warning Count Threshold",
                    Category = ModelRuleCategory.Warnings,
                    Description = "Model should have less than 100 warnings",
                    Severity = RuleSeverity.Warning,
                    Threshold = 100
                },
                new ModelRule
                {
                    RuleId = "MR-PHASE-001",
                    Name = "Phase Assignment",
                    Category = ModelRuleCategory.Phasing,
                    Description = "All elements must have phase assigned",
                    Severity = RuleSeverity.Warning
                }
            });
        }

        private void InitializeClassificationSystems()
        {
            _classificationSystems.AddRange(new[]
            {
                new ClassificationSystem
                {
                    SystemId = "UNICLASS2015",
                    Name = "Uniclass 2015",
                    Description = "UK unified classification system",
                    Tables = new List<ClassificationTable>
                    {
                        new ClassificationTable { Code = "Ac", Name = "Activities" },
                        new ClassificationTable { Code = "Co", Name = "Complexes" },
                        new ClassificationTable { Code = "En", Name = "Entities" },
                        new ClassificationTable { Code = "SL", Name = "Spaces/Locations" },
                        new ClassificationTable { Code = "EF", Name = "Elements/Functions" },
                        new ClassificationTable { Code = "Ss", Name = "Systems" },
                        new ClassificationTable { Code = "Pr", Name = "Products" }
                    },
                    Pattern = @"^(Ac|Co|En|SL|EF|Ss|Pr)_\d{2}(_\d{2}){0,4}$"
                },
                new ClassificationSystem
                {
                    SystemId = "OMNICLASS",
                    Name = "OmniClass",
                    Description = "North American classification system",
                    Tables = new List<ClassificationTable>
                    {
                        new ClassificationTable { Code = "11", Name = "Construction Entities by Function" },
                        new ClassificationTable { Code = "12", Name = "Construction Entities by Form" },
                        new ClassificationTable { Code = "13", Name = "Spaces by Function" },
                        new ClassificationTable { Code = "21", Name = "Elements" },
                        new ClassificationTable { Code = "22", Name = "Work Results" },
                        new ClassificationTable { Code = "23", Name = "Products" }
                    },
                    Pattern = @"^\d{2}[-\s]\d{2}(\s\d{2}){0,4}$"
                },
                new ClassificationSystem
                {
                    SystemId = "MASTERFORMAT",
                    Name = "MasterFormat 2020",
                    Description = "CSI specification numbering",
                    Tables = new List<ClassificationTable>
                    {
                        new ClassificationTable { Code = "03", Name = "Concrete" },
                        new ClassificationTable { Code = "04", Name = "Masonry" },
                        new ClassificationTable { Code = "05", Name = "Metals" },
                        new ClassificationTable { Code = "06", Name = "Wood, Plastics, Composites" },
                        new ClassificationTable { Code = "07", Name = "Thermal and Moisture Protection" },
                        new ClassificationTable { Code = "08", Name = "Openings" },
                        new ClassificationTable { Code = "09", Name = "Finishes" },
                        new ClassificationTable { Code = "21", Name = "Fire Suppression" },
                        new ClassificationTable { Code = "22", Name = "Plumbing" },
                        new ClassificationTable { Code = "23", Name = "HVAC" },
                        new ClassificationTable { Code = "26", Name = "Electrical" }
                    },
                    Pattern = @"^\d{2}\s\d{2}\s\d{2}(\.\d{2})?$"
                },
                new ClassificationSystem
                {
                    SystemId = "NRMS",
                    Name = "NRM - New Rules of Measurement",
                    Description = "UK cost measurement rules",
                    Tables = new List<ClassificationTable>
                    {
                        new ClassificationTable { Code = "1", Name = "Substructure" },
                        new ClassificationTable { Code = "2", Name = "Superstructure" },
                        new ClassificationTable { Code = "3", Name = "Internal Finishes" },
                        new ClassificationTable { Code = "4", Name = "Fittings and Furnishings" },
                        new ClassificationTable { Code = "5", Name = "Services" },
                        new ClassificationTable { Code = "6", Name = "Prefabricated Buildings" },
                        new ClassificationTable { Code = "7", Name = "Work to Existing Buildings" },
                        new ClassificationTable { Code = "8", Name = "External Works" }
                    },
                    Pattern = @"^\d(\.\d+)*$"
                }
            });
        }

        #endregion

        #region Standards Profile Management

        /// <summary>
        /// Create a standards profile for a project
        /// </summary>
        public StandardsProfile CreateProfile(StandardsProfileRequest request)
        {
            var profile = new StandardsProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ProfileName = request.ProfileName,
                CreatedDate = DateTime.UtcNow,
                ActiveNamingRules = request.ActiveNamingRules ?? _namingRules.Select(r => r.RuleId).ToList(),
                ActiveParameterRules = request.ActiveParameterRules ?? _parameterRules.Select(r => r.RuleId).ToList(),
                ActiveModelRules = request.ActiveModelRules ?? _modelRules.Select(r => r.RuleId).ToList(),
                ClassificationSystem = request.ClassificationSystem ?? "UNICLASS2015",
                CustomRules = request.CustomRules ?? new List<CustomRule>(),
                Exceptions = request.Exceptions ?? new List<RuleException>()
            };

            lock (_lock)
            {
                _profiles[profile.ProfileId] = profile;
            }

            return profile;
        }

        /// <summary>
        /// Add custom rule to profile
        /// </summary>
        public void AddCustomRule(string profileId, CustomRule rule)
        {
            lock (_lock)
            {
                if (_profiles.TryGetValue(profileId, out var profile))
                {
                    rule.RuleId = $"CUSTOM-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                    profile.CustomRules.Add(rule);
                }
            }
        }

        /// <summary>
        /// Add exception to profile
        /// </summary>
        public void AddException(string profileId, RuleException exception)
        {
            lock (_lock)
            {
                if (_profiles.TryGetValue(profileId, out var profile))
                {
                    exception.ExceptionId = Guid.NewGuid().ToString();
                    exception.CreatedDate = DateTime.UtcNow;
                    profile.Exceptions.Add(exception);
                }
            }
        }

        #endregion

        #region Compliance Checking

        /// <summary>
        /// Run comprehensive compliance check
        /// </summary>
        public ComplianceReport RunComplianceCheck(ComplianceCheckRequest request)
        {
            if (!_profiles.TryGetValue(request.ProfileId, out var profile))
                throw new KeyNotFoundException($"Standards profile {request.ProfileId} not found");

            var report = new ComplianceReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ProfileId = request.ProfileId,
                ModelPath = request.ModelPath,
                CheckDate = DateTime.UtcNow,
                NamingViolations = new List<NamingViolation>(),
                ParameterViolations = new List<ParameterViolation>(),
                ModelViolations = new List<ModelViolation>(),
                ClassificationViolations = new List<ClassificationViolation>()
            };

            // Check naming conventions
            if (request.CheckNaming && request.Items != null)
            {
                foreach (var item in request.Items)
                {
                    var violations = CheckNaming(item, profile);
                    report.NamingViolations.AddRange(violations);
                }
            }

            // Check parameters
            if (request.CheckParameters && request.Elements != null)
            {
                foreach (var element in request.Elements)
                {
                    var violations = CheckParameters(element, profile);
                    report.ParameterViolations.AddRange(violations);
                }
            }

            // Check model rules
            if (request.CheckModel && request.ModelData != null)
            {
                var violations = CheckModelRules(request.ModelData, profile);
                report.ModelViolations.AddRange(violations);
            }

            // Check classification
            if (request.CheckClassification && request.Elements != null)
            {
                foreach (var element in request.Elements)
                {
                    var violation = CheckClassification(element, profile);
                    if (violation != null)
                        report.ClassificationViolations.Add(violation);
                }
            }

            // Calculate summary
            report.Summary = new ComplianceSummary
            {
                TotalViolations = report.NamingViolations.Count + report.ParameterViolations.Count +
                    report.ModelViolations.Count + report.ClassificationViolations.Count,
                Errors = report.NamingViolations.Count(v => v.Severity == RuleSeverity.Error) +
                    report.ParameterViolations.Count(v => v.Severity == RuleSeverity.Error) +
                    report.ModelViolations.Count(v => v.Severity == RuleSeverity.Error),
                Warnings = report.NamingViolations.Count(v => v.Severity == RuleSeverity.Warning) +
                    report.ParameterViolations.Count(v => v.Severity == RuleSeverity.Warning) +
                    report.ModelViolations.Count(v => v.Severity == RuleSeverity.Warning),
                ComplianceScore = CalculateComplianceScore(report)
            };

            lock (_lock)
            {
                _reports[report.ReportId] = report;
            }

            // Fire events for violations
            if (report.Summary.Errors > 0)
            {
                ComplianceViolation?.Invoke(this, new ComplianceEventArgs
                {
                    ReportId = report.ReportId,
                    ViolationType = "Error",
                    Message = $"Found {report.Summary.Errors} compliance errors"
                });
            }

            return report;
        }

        private List<NamingViolation> CheckNaming(NamedItem item, StandardsProfile profile)
        {
            var violations = new List<NamingViolation>();

            var applicableRules = _namingRules
                .Where(r => profile.ActiveNamingRules.Contains(r.RuleId))
                .Where(r => r.Category == item.Category);

            foreach (var rule in applicableRules)
            {
                // Check for exception
                if (profile.Exceptions.Any(e => e.RuleId == rule.RuleId &&
                    (e.ItemId == item.ItemId || e.ItemName == item.Name)))
                    continue;

                if (!Regex.IsMatch(item.Name, rule.Pattern))
                {
                    violations.Add(new NamingViolation
                    {
                        RuleId = rule.RuleId,
                        RuleName = rule.Name,
                        ItemId = item.ItemId,
                        ItemName = item.Name,
                        ItemType = item.Category.ToString(),
                        ExpectedPattern = rule.Pattern,
                        Example = rule.Example,
                        Description = rule.Description,
                        Severity = RuleSeverity.Warning
                    });
                }
            }

            return violations;
        }

        private List<ParameterViolation> CheckParameters(ElementData element, StandardsProfile profile)
        {
            var violations = new List<ParameterViolation>();

            var applicableRules = _parameterRules
                .Where(r => profile.ActiveParameterRules.Contains(r.RuleId))
                .Where(r => r.Categories.Contains("All") || r.Categories.Contains(element.Category));

            foreach (var rule in applicableRules)
            {
                // Check for exception
                if (profile.Exceptions.Any(e => e.RuleId == rule.RuleId && e.ItemId == element.ElementId))
                    continue;

                var paramValue = element.Parameters?.GetValueOrDefault(rule.ParameterName);

                // Check required
                if (rule.IsRequired && string.IsNullOrEmpty(paramValue))
                {
                    violations.Add(new ParameterViolation
                    {
                        RuleId = rule.RuleId,
                        RuleName = rule.Name,
                        ElementId = element.ElementId,
                        Category = element.Category,
                        ParameterName = rule.ParameterName,
                        ViolationType = "Missing Required Parameter",
                        Description = rule.Description,
                        Severity = RuleSeverity.Error
                    });
                    continue;
                }

                // Check pattern
                if (!string.IsNullOrEmpty(rule.Pattern) && !string.IsNullOrEmpty(paramValue))
                {
                    if (!Regex.IsMatch(paramValue, rule.Pattern))
                    {
                        violations.Add(new ParameterViolation
                        {
                            RuleId = rule.RuleId,
                            RuleName = rule.Name,
                            ElementId = element.ElementId,
                            Category = element.Category,
                            ParameterName = rule.ParameterName,
                            CurrentValue = paramValue,
                            ViolationType = "Invalid Format",
                            Description = rule.Description,
                            Severity = RuleSeverity.Warning
                        });
                    }
                }

                // Check allowed values
                if (rule.AllowedValues != null && rule.AllowedValues.Count > 0 && !string.IsNullOrEmpty(paramValue))
                {
                    if (!rule.AllowedValues.Contains(paramValue))
                    {
                        violations.Add(new ParameterViolation
                        {
                            RuleId = rule.RuleId,
                            RuleName = rule.Name,
                            ElementId = element.ElementId,
                            Category = element.Category,
                            ParameterName = rule.ParameterName,
                            CurrentValue = paramValue,
                            ViolationType = "Invalid Value",
                            AllowedValues = rule.AllowedValues,
                            Description = rule.Description,
                            Severity = RuleSeverity.Warning
                        });
                    }
                }
            }

            return violations;
        }

        private List<ModelViolation> CheckModelRules(ModelCheckData modelData, StandardsProfile profile)
        {
            var violations = new List<ModelViolation>();

            foreach (var rule in _modelRules.Where(r => profile.ActiveModelRules.Contains(r.RuleId)))
            {
                bool violated = false;
                string details = null;

                switch (rule.RuleId)
                {
                    case "MR-COORD-001":
                        violated = !modelData.HasSharedCoordinates;
                        details = "Model is not using shared coordinates";
                        break;

                    case "MR-WARN-001":
                        violated = modelData.WarningCount > rule.Threshold;
                        details = $"Model has {modelData.WarningCount} warnings (threshold: {rule.Threshold})";
                        break;

                    case "MR-ROOM-001":
                        violated = modelData.UnboundedRoomCount > 0;
                        details = $"{modelData.UnboundedRoomCount} rooms are not properly bounded";
                        break;

                    case "MR-ROOM-002":
                        violated = modelData.DuplicateRoomNumbers > 0;
                        details = $"{modelData.DuplicateRoomNumbers} duplicate room numbers found";
                        break;

                    case "MR-LINK-001":
                        violated = modelData.LinksOnWrongWorkset > 0;
                        details = $"{modelData.LinksOnWrongWorkset} links not on Links workset";
                        break;
                }

                if (violated)
                {
                    violations.Add(new ModelViolation
                    {
                        RuleId = rule.RuleId,
                        RuleName = rule.Name,
                        Category = rule.Category.ToString(),
                        Description = rule.Description,
                        Details = details,
                        Severity = rule.Severity
                    });
                }
            }

            return violations;
        }

        private ClassificationViolation CheckClassification(ElementData element, StandardsProfile profile)
        {
            var system = _classificationSystems.FirstOrDefault(s => s.SystemId == profile.ClassificationSystem);
            if (system == null)
                return null;

            var classCode = element.Parameters?.GetValueOrDefault("ClassificationCode");

            if (string.IsNullOrEmpty(classCode))
            {
                return new ClassificationViolation
                {
                    ElementId = element.ElementId,
                    Category = element.Category,
                    SystemId = system.SystemId,
                    ViolationType = "Missing Classification",
                    Description = $"Element missing {system.Name} classification code"
                };
            }

            if (!Regex.IsMatch(classCode, system.Pattern))
            {
                return new ClassificationViolation
                {
                    ElementId = element.ElementId,
                    Category = element.Category,
                    SystemId = system.SystemId,
                    CurrentCode = classCode,
                    ViolationType = "Invalid Classification Format",
                    ExpectedPattern = system.Pattern,
                    Description = $"Classification code does not match {system.Name} format"
                };
            }

            return null;
        }

        private double CalculateComplianceScore(ComplianceReport report)
        {
            var totalChecks = Math.Max(1, report.NamingViolations.Count + report.ParameterViolations.Count +
                report.ModelViolations.Count + report.ClassificationViolations.Count + 100);

            var deductions = report.Summary.Errors * 10 + report.Summary.Warnings * 3;
            var score = Math.Max(0, 100 - (deductions * 100.0 / totalChecks));

            return Math.Round(score, 1);
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validate a single name against rules
        /// </summary>
        public NamingValidationResult ValidateName(string name, NamingCategory category)
        {
            var result = new NamingValidationResult
            {
                Name = name,
                Category = category,
                IsValid = true,
                Violations = new List<string>(),
                Suggestions = new List<string>()
            };

            var rules = _namingRules.Where(r => r.Category == category);

            foreach (var rule in rules)
            {
                if (!Regex.IsMatch(name, rule.Pattern))
                {
                    result.IsValid = false;
                    result.Violations.Add($"Violates rule '{rule.Name}': {rule.Description}");
                    if (!string.IsNullOrEmpty(rule.Example))
                        result.Suggestions.Add($"Example: {rule.Example}");
                }
            }

            return result;
        }

        /// <summary>
        /// Validate classification code
        /// </summary>
        public ClassificationValidationResult ValidateClassificationCode(string code, string systemId)
        {
            var system = _classificationSystems.FirstOrDefault(s => s.SystemId == systemId);
            if (system == null)
            {
                return new ClassificationValidationResult
                {
                    Code = code,
                    IsValid = false,
                    Message = $"Unknown classification system: {systemId}"
                };
            }

            var isValid = Regex.IsMatch(code, system.Pattern);

            return new ClassificationValidationResult
            {
                Code = code,
                SystemId = systemId,
                SystemName = system.Name,
                IsValid = isValid,
                Message = isValid ? "Valid classification code" : $"Code does not match {system.Name} format",
                ExpectedPattern = system.Pattern
            };
        }

        /// <summary>
        /// Get suggested classification code
        /// </summary>
        public List<ClassificationSuggestion> GetClassificationSuggestions(string category, string systemId)
        {
            var system = _classificationSystems.FirstOrDefault(s => s.SystemId == systemId);
            if (system == null)
                return new List<ClassificationSuggestion>();

            // Return table-based suggestions
            return system.Tables.Select(t => new ClassificationSuggestion
            {
                TableCode = t.Code,
                TableName = t.Name,
                SuggestedPrefix = t.Code
            }).ToList();
        }

        #endregion

        #region Queries

        public StandardsProfile GetProfile(string profileId)
        {
            lock (_lock)
            {
                return _profiles.TryGetValue(profileId, out var profile) ? profile : null;
            }
        }

        public ComplianceReport GetReport(string reportId)
        {
            lock (_lock)
            {
                return _reports.TryGetValue(reportId, out var report) ? report : null;
            }
        }

        public List<NamingRule> GetNamingRules(NamingCategory? category = null)
        {
            if (category.HasValue)
                return _namingRules.Where(r => r.Category == category.Value).ToList();
            return _namingRules.ToList();
        }

        public List<ParameterRule> GetParameterRules() => _parameterRules.ToList();

        public List<ModelRule> GetModelRules() => _modelRules.ToList();

        public List<ClassificationSystem> GetClassificationSystems() => _classificationSystems.ToList();

        #endregion
    }

    #region Data Models

    public class StandardsProfile
    {
        public string ProfileId { get; set; }
        public string ProjectId { get; set; }
        public string ProfileName { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> ActiveNamingRules { get; set; }
        public List<string> ActiveParameterRules { get; set; }
        public List<string> ActiveModelRules { get; set; }
        public string ClassificationSystem { get; set; }
        public List<CustomRule> CustomRules { get; set; }
        public List<RuleException> Exceptions { get; set; }
    }

    public class StandardsProfileRequest
    {
        public string ProjectId { get; set; }
        public string ProfileName { get; set; }
        public List<string> ActiveNamingRules { get; set; }
        public List<string> ActiveParameterRules { get; set; }
        public List<string> ActiveModelRules { get; set; }
        public string ClassificationSystem { get; set; }
        public List<CustomRule> CustomRules { get; set; }
        public List<RuleException> Exceptions { get; set; }
    }

    public class NamingRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public NamingCategory Category { get; set; }
        public string Pattern { get; set; }
        public string Description { get; set; }
        public string Example { get; set; }
        public List<NamingField> Fields { get; set; }
    }

    public class NamingField
    {
        public string Name { get; set; }
        public int Position { get; set; }
        public bool Required { get; set; }
    }

    public class ParameterRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string ParameterName { get; set; }
        public bool IsRequired { get; set; }
        public string Pattern { get; set; }
        public List<string> AllowedValues { get; set; }
        public List<string> Categories { get; set; }
        public string Condition { get; set; }
        public string Description { get; set; }
    }

    public class ModelRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public ModelRuleCategory Category { get; set; }
        public string Description { get; set; }
        public RuleSeverity Severity { get; set; }
        public int? Threshold { get; set; }
    }

    public class ClassificationSystem
    {
        public string SystemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ClassificationTable> Tables { get; set; }
        public string Pattern { get; set; }
    }

    public class ClassificationTable
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class CustomRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public string TargetType { get; set; }
        public RuleSeverity Severity { get; set; }
    }

    public class RuleException
    {
        public string ExceptionId { get; set; }
        public string RuleId { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string Reason { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class ComplianceCheckRequest
    {
        public string ProfileId { get; set; }
        public string ModelPath { get; set; }
        public bool CheckNaming { get; set; } = true;
        public bool CheckParameters { get; set; } = true;
        public bool CheckModel { get; set; } = true;
        public bool CheckClassification { get; set; } = true;
        public List<NamedItem> Items { get; set; }
        public List<ElementData> Elements { get; set; }
        public ModelCheckData ModelData { get; set; }
    }

    public class NamedItem
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public NamingCategory Category { get; set; }
    }

    public class ElementData
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class ModelCheckData
    {
        public bool HasSharedCoordinates { get; set; }
        public int WarningCount { get; set; }
        public int UnboundedRoomCount { get; set; }
        public int DuplicateRoomNumbers { get; set; }
        public int LinksOnWrongWorkset { get; set; }
    }

    public class ComplianceReport
    {
        public string ReportId { get; set; }
        public string ProfileId { get; set; }
        public string ModelPath { get; set; }
        public DateTime CheckDate { get; set; }
        public List<NamingViolation> NamingViolations { get; set; }
        public List<ParameterViolation> ParameterViolations { get; set; }
        public List<ModelViolation> ModelViolations { get; set; }
        public List<ClassificationViolation> ClassificationViolations { get; set; }
        public ComplianceSummary Summary { get; set; }
    }

    public class NamingViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; }
        public string ExpectedPattern { get; set; }
        public string Example { get; set; }
        public string Description { get; set; }
        public RuleSeverity Severity { get; set; }
    }

    public class ParameterViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string ParameterName { get; set; }
        public string CurrentValue { get; set; }
        public string ViolationType { get; set; }
        public List<string> AllowedValues { get; set; }
        public string Description { get; set; }
        public RuleSeverity Severity { get; set; }
    }

    public class ModelViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Details { get; set; }
        public RuleSeverity Severity { get; set; }
    }

    public class ClassificationViolation
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string SystemId { get; set; }
        public string CurrentCode { get; set; }
        public string ViolationType { get; set; }
        public string ExpectedPattern { get; set; }
        public string Description { get; set; }
    }

    public class ComplianceSummary
    {
        public int TotalViolations { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public double ComplianceScore { get; set; }
    }

    public class NamingValidationResult
    {
        public string Name { get; set; }
        public NamingCategory Category { get; set; }
        public bool IsValid { get; set; }
        public List<string> Violations { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class ClassificationValidationResult
    {
        public string Code { get; set; }
        public string SystemId { get; set; }
        public string SystemName { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string ExpectedPattern { get; set; }
    }

    public class ClassificationSuggestion
    {
        public string TableCode { get; set; }
        public string TableName { get; set; }
        public string SuggestedPrefix { get; set; }
    }

    public class ComplianceEventArgs : EventArgs
    {
        public string ReportId { get; set; }
        public string ViolationType { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum NamingCategory { File, Family, Type, View, Sheet, Parameter, Workset, Level, Grid }
    public enum ModelRuleCategory { Coordinates, Levels, Grids, Rooms, Links, Warnings, Phasing, General }
    public enum RuleSeverity { Info, Warning, Error, Critical }

    #endregion
}
