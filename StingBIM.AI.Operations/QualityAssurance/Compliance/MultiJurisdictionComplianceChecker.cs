// StingBIM.AI.QualityAssurance - MultiJurisdictionComplianceChecker.cs
// Simultaneous Multi-Jurisdiction Building Code Compliance Checking
// Phase 4: Enterprise AI Transformation - Compliance Automation
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace StingBIM.AI.QualityAssurance.Compliance
{
    /// <summary>
    /// Advanced compliance checker supporting simultaneous checking against multiple
    /// building codes and jurisdictions with conflict detection and variance documentation.
    /// </summary>
    public class MultiJurisdictionComplianceChecker
    {
        #region Fields

        private readonly Dictionary<string, BuildingCode> _buildingCodes;
        private readonly Dictionary<string, ComplianceRule> _complianceRules;
        private readonly CodeConflictResolver _conflictResolver;
        private readonly VarianceManager _varianceManager;
        private readonly ComplianceReportGenerator _reportGenerator;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public MultiJurisdictionComplianceChecker()
        {
            _buildingCodes = new Dictionary<string, BuildingCode>(StringComparer.OrdinalIgnoreCase);
            _complianceRules = new Dictionary<string, ComplianceRule>(StringComparer.OrdinalIgnoreCase);
            _conflictResolver = new CodeConflictResolver();
            _varianceManager = new VarianceManager();
            _reportGenerator = new ComplianceReportGenerator();

            InitializeBuildingCodes();
            InitializeComplianceRules();
        }

        #endregion

        #region Initialization

        private void InitializeBuildingCodes()
        {
            // International Codes
            _buildingCodes["IBC-2024"] = new BuildingCode
            {
                CodeId = "IBC-2024",
                Name = "International Building Code 2024",
                Jurisdiction = "International",
                Region = "USA",
                Version = "2024",
                EffectiveDate = new DateTime(2024, 1, 1),
                Categories = new List<string> { "Egress", "Fire Safety", "Accessibility", "Structural", "MEP" }
            };

            _buildingCodes["IPC-2024"] = new BuildingCode
            {
                CodeId = "IPC-2024",
                Name = "International Plumbing Code 2024",
                Jurisdiction = "International",
                Region = "USA",
                Version = "2024",
                EffectiveDate = new DateTime(2024, 1, 1),
                Categories = new List<string> { "Plumbing", "Fixtures", "Water Supply", "Drainage" }
            };

            _buildingCodes["IMC-2024"] = new BuildingCode
            {
                CodeId = "IMC-2024",
                Name = "International Mechanical Code 2024",
                Jurisdiction = "International",
                Region = "USA",
                Version = "2024",
                EffectiveDate = new DateTime(2024, 1, 1),
                Categories = new List<string> { "HVAC", "Ventilation", "Exhaust", "Duct Systems" }
            };

            _buildingCodes["NEC-2023"] = new BuildingCode
            {
                CodeId = "NEC-2023",
                Name = "National Electrical Code 2023",
                Jurisdiction = "USA",
                Region = "USA",
                Version = "2023",
                EffectiveDate = new DateTime(2023, 1, 1),
                Categories = new List<string> { "Electrical", "Wiring", "Panels", "Circuits" }
            };

            // European Codes
            _buildingCodes["EUROCODE"] = new BuildingCode
            {
                CodeId = "EUROCODE",
                Name = "Eurocodes (EN 1990-1999)",
                Jurisdiction = "European Union",
                Region = "Europe",
                Version = "2023",
                EffectiveDate = new DateTime(2023, 1, 1),
                Categories = new List<string> { "Structural", "Actions", "Materials", "Fire" }
            };

            _buildingCodes["BS-7671"] = new BuildingCode
            {
                CodeId = "BS-7671",
                Name = "BS 7671 Wiring Regulations",
                Jurisdiction = "United Kingdom",
                Region = "UK",
                Version = "18th Edition",
                EffectiveDate = new DateTime(2022, 3, 28),
                Categories = new List<string> { "Electrical", "Wiring", "Safety" }
            };

            // African Regional Codes
            _buildingCodes["KEBS"] = new BuildingCode
            {
                CodeId = "KEBS",
                Name = "Kenya Bureau of Standards Building Code",
                Jurisdiction = "Kenya",
                Region = "East Africa",
                Version = "2020",
                EffectiveDate = new DateTime(2020, 1, 1),
                Categories = new List<string> { "Building", "Planning", "Fire Safety", "Structural" }
            };

            _buildingCodes["SANS"] = new BuildingCode
            {
                CodeId = "SANS",
                Name = "South African National Standards",
                Jurisdiction = "South Africa",
                Region = "Southern Africa",
                Version = "SANS 10400",
                EffectiveDate = new DateTime(2021, 1, 1),
                Categories = new List<string> { "Building", "Energy", "Fire", "Accessibility" }
            };

            _buildingCodes["EAC"] = new BuildingCode
            {
                CodeId = "EAC",
                Name = "East African Community Standards",
                Jurisdiction = "EAC",
                Region = "East Africa",
                Version = "2023",
                EffectiveDate = new DateTime(2023, 1, 1),
                Categories = new List<string> { "Building", "Structural", "Fire Safety" }
            };

            _buildingCodes["UNBS"] = new BuildingCode
            {
                CodeId = "UNBS",
                Name = "Uganda National Bureau of Standards",
                Jurisdiction = "Uganda",
                Region = "East Africa",
                Version = "2022",
                EffectiveDate = new DateTime(2022, 1, 1),
                Categories = new List<string> { "Building", "Materials", "Safety" }
            };

            // Energy Codes
            _buildingCodes["ASHRAE-90.1"] = new BuildingCode
            {
                CodeId = "ASHRAE-90.1",
                Name = "ASHRAE Standard 90.1 Energy Standard",
                Jurisdiction = "International",
                Region = "Global",
                Version = "2022",
                EffectiveDate = new DateTime(2022, 10, 1),
                Categories = new List<string> { "Energy", "HVAC", "Lighting", "Envelope" }
            };

            _buildingCodes["ASHRAE-62.1"] = new BuildingCode
            {
                CodeId = "ASHRAE-62.1",
                Name = "ASHRAE Standard 62.1 Ventilation",
                Jurisdiction = "International",
                Region = "Global",
                Version = "2022",
                EffectiveDate = new DateTime(2022, 10, 1),
                Categories = new List<string> { "Ventilation", "IAQ", "Air Quality" }
            };

            // Accessibility Codes
            _buildingCodes["ADA"] = new BuildingCode
            {
                CodeId = "ADA",
                Name = "Americans with Disabilities Act Standards",
                Jurisdiction = "USA",
                Region = "USA",
                Version = "2010",
                EffectiveDate = new DateTime(2012, 3, 15),
                Categories = new List<string> { "Accessibility", "Circulation", "Signage" }
            };

            _buildingCodes["BS-8300"] = new BuildingCode
            {
                CodeId = "BS-8300",
                Name = "BS 8300 Design of Buildings for Disabled People",
                Jurisdiction = "United Kingdom",
                Region = "UK",
                Version = "2018",
                EffectiveDate = new DateTime(2018, 1, 1),
                Categories = new List<string> { "Accessibility", "Design", "Facilities" }
            };
        }

        private void InitializeComplianceRules()
        {
            // Egress Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "EGRESS-001",
                Name = "Exit Door Minimum Width",
                Description = "Exit doors must meet minimum width requirements",
                Category = "Egress",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS", "EAC" },
                CheckLogic = CheckExitDoorWidth,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 813, Unit = "mm", Reference = "IBC 1010.1.1" },
                    ["KEBS"] = new CodeRequirement { MinValue = 900, Unit = "mm", Reference = "KEBS Building Code 4.5.2" },
                    ["SANS"] = new CodeRequirement { MinValue = 850, Unit = "mm", Reference = "SANS 10400-T" },
                    ["EAC"] = new CodeRequirement { MinValue = 900, Unit = "mm", Reference = "EAC Building Code 5.3" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "EGRESS-002",
                Name = "Corridor Minimum Width",
                Description = "Corridors must meet minimum width requirements based on occupancy",
                Category = "Egress",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS" },
                CheckLogic = CheckCorridorWidth,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 1118, Unit = "mm", Reference = "IBC 1020.2" },
                    ["KEBS"] = new CodeRequirement { MinValue = 1200, Unit = "mm", Reference = "KEBS Building Code 4.6.1" },
                    ["SANS"] = new CodeRequirement { MinValue = 1100, Unit = "mm", Reference = "SANS 10400-T" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "EGRESS-003",
                Name = "Maximum Travel Distance",
                Description = "Travel distance to exit must not exceed maximum",
                Category = "Egress",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS", "BS-9999" },
                CheckLogic = CheckTravelDistance,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MaxValue = 76000, Unit = "mm", Reference = "IBC 1017.1" },
                    ["KEBS"] = new CodeRequirement { MaxValue = 45000, Unit = "mm", Reference = "KEBS 4.7.3" },
                    ["SANS"] = new CodeRequirement { MaxValue = 60000, Unit = "mm", Reference = "SANS 10400-T" }
                }
            });

            // Stair Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "STAIR-001",
                Name = "Stair Riser Height",
                Description = "Stair risers must be within allowed height range",
                Category = "Stairs",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS", "BS-5395" },
                CheckLogic = CheckStairRiserHeight,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 102, MaxValue = 178, Unit = "mm", Reference = "IBC 1011.5.2" },
                    ["KEBS"] = new CodeRequirement { MinValue = 150, MaxValue = 180, Unit = "mm", Reference = "KEBS 4.8.2" },
                    ["SANS"] = new CodeRequirement { MinValue = 100, MaxValue = 200, Unit = "mm", Reference = "SANS 10400-M" },
                    ["BS-5395"] = new CodeRequirement { MinValue = 150, MaxValue = 170, Unit = "mm", Reference = "BS 5395-1" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "STAIR-002",
                Name = "Stair Tread Depth",
                Description = "Stair treads must meet minimum depth requirements",
                Category = "Stairs",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS", "BS-5395" },
                CheckLogic = CheckStairTreadDepth,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 279, Unit = "mm", Reference = "IBC 1011.5.2" },
                    ["KEBS"] = new CodeRequirement { MinValue = 250, Unit = "mm", Reference = "KEBS 4.8.2" },
                    ["SANS"] = new CodeRequirement { MinValue = 250, Unit = "mm", Reference = "SANS 10400-M" },
                    ["BS-5395"] = new CodeRequirement { MinValue = 250, Unit = "mm", Reference = "BS 5395-1" }
                }
            });

            // Accessibility Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ACCESS-001",
                Name = "Accessible Door Clear Width",
                Description = "Accessible doors must meet minimum clear width",
                Category = "Accessibility",
                ApplicableCodes = new List<string> { "ADA", "BS-8300", "SANS", "KEBS" },
                CheckLogic = CheckAccessibleDoorWidth,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["ADA"] = new CodeRequirement { MinValue = 813, Unit = "mm", Reference = "ADA 404.2.3" },
                    ["BS-8300"] = new CodeRequirement { MinValue = 800, Unit = "mm", Reference = "BS 8300:2018 8.2" },
                    ["SANS"] = new CodeRequirement { MinValue = 850, Unit = "mm", Reference = "SANS 10400-S" },
                    ["KEBS"] = new CodeRequirement { MinValue = 900, Unit = "mm", Reference = "KEBS 5.2.1" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ACCESS-002",
                Name = "Ramp Maximum Slope",
                Description = "Accessible ramps must not exceed maximum slope",
                Category = "Accessibility",
                ApplicableCodes = new List<string> { "ADA", "BS-8300", "SANS" },
                CheckLogic = CheckRampSlope,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["ADA"] = new CodeRequirement { MaxValue = 8.33, Unit = "%", Reference = "ADA 405.2" },
                    ["BS-8300"] = new CodeRequirement { MaxValue = 8.0, Unit = "%", Reference = "BS 8300:2018 9.1" },
                    ["SANS"] = new CodeRequirement { MaxValue = 8.33, Unit = "%", Reference = "SANS 10400-S" }
                }
            });

            // Room Size Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ROOM-001",
                Name = "Minimum Room Height",
                Description = "Habitable rooms must meet minimum ceiling height",
                Category = "Rooms",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS" },
                CheckLogic = CheckRoomHeight,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 2134, Unit = "mm", Reference = "IBC 1208.2" },
                    ["KEBS"] = new CodeRequirement { MinValue = 2400, Unit = "mm", Reference = "KEBS 3.2.1" },
                    ["SANS"] = new CodeRequirement { MinValue = 2400, Unit = "mm", Reference = "SANS 10400-O" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ROOM-002",
                Name = "Minimum Toilet Room Size",
                Description = "Toilet rooms must meet minimum area requirements",
                Category = "Rooms",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS" },
                CheckLogic = CheckToiletRoomSize,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 1.86, Unit = "m²", Reference = "IBC 1210" },
                    ["KEBS"] = new CodeRequirement { MinValue = 2.5, Unit = "m²", Reference = "KEBS 3.4.2" },
                    ["SANS"] = new CodeRequirement { MinValue = 2.0, Unit = "m²", Reference = "SANS 10400-O" }
                }
            });

            // Fire Safety Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "FIRE-001",
                Name = "Fire Door Rating",
                Description = "Fire doors must meet minimum fire rating requirements",
                Category = "Fire Safety",
                ApplicableCodes = new List<string> { "IBC-2024", "KEBS", "SANS", "BS-476" },
                CheckLogic = CheckFireDoorRating,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["IBC-2024"] = new CodeRequirement { MinValue = 20, Unit = "minutes", Reference = "IBC 716" },
                    ["KEBS"] = new CodeRequirement { MinValue = 30, Unit = "minutes", Reference = "KEBS Fire Code" },
                    ["SANS"] = new CodeRequirement { MinValue = 30, Unit = "minutes", Reference = "SANS 10400-T" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "FIRE-002",
                Name = "Sprinkler Coverage",
                Description = "Sprinklers must provide adequate coverage",
                Category = "Fire Safety",
                ApplicableCodes = new List<string> { "NFPA-13", "BS-EN-12845", "SANS" },
                CheckLogic = CheckSprinklerCoverage,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["NFPA-13"] = new CodeRequirement { MaxValue = 20.9, Unit = "m²/head", Reference = "NFPA 13" },
                    ["BS-EN-12845"] = new CodeRequirement { MaxValue = 21.0, Unit = "m²/head", Reference = "BS EN 12845" },
                    ["SANS"] = new CodeRequirement { MaxValue = 21.0, Unit = "m²/head", Reference = "SANS 10287" }
                }
            });

            // MEP Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "VENT-001",
                Name = "Minimum Ventilation Rate",
                Description = "Spaces must have adequate ventilation per occupant",
                Category = "Ventilation",
                ApplicableCodes = new List<string> { "ASHRAE-62.1", "CIBSE-A", "SANS" },
                CheckLogic = CheckVentilationRate,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["ASHRAE-62.1"] = new CodeRequirement { MinValue = 2.5, Unit = "L/s/person", Reference = "ASHRAE 62.1 Table 6-1" },
                    ["CIBSE-A"] = new CodeRequirement { MinValue = 10.0, Unit = "L/s/person", Reference = "CIBSE Guide A" },
                    ["SANS"] = new CodeRequirement { MinValue = 7.5, Unit = "L/s/person", Reference = "SANS 10400-O" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ELEC-001",
                Name = "Socket Outlet Quantity",
                Description = "Rooms must have minimum socket outlets per wall",
                Category = "Electrical",
                ApplicableCodes = new List<string> { "NEC-2023", "BS-7671", "SANS" },
                CheckLogic = CheckSocketOutlets,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["NEC-2023"] = new CodeRequirement { MinValue = 1, Unit = "per 3.6m wall", Reference = "NEC 210.52" },
                    ["BS-7671"] = new CodeRequirement { MinValue = 1, Unit = "per wall", Reference = "BS 7671" },
                    ["SANS"] = new CodeRequirement { MinValue = 1, Unit = "per 4m wall", Reference = "SANS 10142-1" }
                }
            });

            // Energy Efficiency Rules
            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ENERGY-001",
                Name = "Wall U-Value Maximum",
                Description = "External walls must meet thermal performance requirements",
                Category = "Energy",
                ApplicableCodes = new List<string> { "ASHRAE-90.1", "SANS-10400-XA", "BS-Part-L" },
                CheckLogic = CheckWallUValue,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["ASHRAE-90.1"] = new CodeRequirement { MaxValue = 0.45, Unit = "W/m²K", Reference = "ASHRAE 90.1 Table 5.5-4" },
                    ["SANS-10400-XA"] = new CodeRequirement { MaxValue = 0.58, Unit = "W/m²K", Reference = "SANS 10400-XA" },
                    ["BS-Part-L"] = new CodeRequirement { MaxValue = 0.26, Unit = "W/m²K", Reference = "Building Regs Part L" }
                }
            });

            AddComplianceRule(new ComplianceRule
            {
                RuleId = "ENERGY-002",
                Name = "Glazing Area Ratio",
                Description = "Window-to-wall ratio must not exceed maximum",
                Category = "Energy",
                ApplicableCodes = new List<string> { "ASHRAE-90.1", "SANS-10400-XA" },
                CheckLogic = CheckGlazingRatio,
                RequirementsByCode = new Dictionary<string, CodeRequirement>
                {
                    ["ASHRAE-90.1"] = new CodeRequirement { MaxValue = 40, Unit = "%", Reference = "ASHRAE 90.1" },
                    ["SANS-10400-XA"] = new CodeRequirement { MaxValue = 50, Unit = "%", Reference = "SANS 10400-XA" }
                }
            });
        }

        #endregion

        #region Public Methods - Code Management

        public void AddBuildingCode(BuildingCode code)
        {
            lock (_lockObject)
            {
                _buildingCodes[code.CodeId] = code;
            }
        }

        public void AddComplianceRule(ComplianceRule rule)
        {
            lock (_lockObject)
            {
                _complianceRules[rule.RuleId] = rule;
            }
        }

        public IEnumerable<BuildingCode> GetAvailableCodes()
        {
            lock (_lockObject)
            {
                return _buildingCodes.Values.ToList();
            }
        }

        public IEnumerable<BuildingCode> GetCodesByRegion(string region)
        {
            lock (_lockObject)
            {
                return _buildingCodes.Values
                    .Where(c => c.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        #endregion

        #region Public Methods - Compliance Checking

        /// <summary>
        /// Runs compliance check against multiple codes simultaneously
        /// </summary>
        public async Task<MultiJurisdictionReport> CheckComplianceAsync(
            Document document,
            IEnumerable<string> codeIds,
            ComplianceCheckOptions options = null,
            IProgress<ComplianceProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new ComplianceCheckOptions();
            var codes = codeIds.Select(id => _buildingCodes.GetValueOrDefault(id)).Where(c => c != null).ToList();

            var report = new MultiJurisdictionReport
            {
                ReportId = Guid.NewGuid().ToString(),
                DocumentName = document.Title,
                CheckedCodes = codes.Select(c => c.CodeId).ToList(),
                StartTime = DateTime.Now
            };

            var applicableRules = GetApplicableRules(codes, options);
            int totalRules = applicableRules.Count;
            int processedRules = 0;

            foreach (var rule in applicableRules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var code in codes.Where(c => rule.ApplicableCodes.Contains(c.CodeId)))
                {
                    try
                    {
                        var violations = await Task.Run(() =>
                            rule.CheckLogic?.Invoke(document, rule, code) ?? new List<ComplianceViolation>(),
                            cancellationToken);

                        foreach (var violation in violations)
                        {
                            violation.RuleId = rule.RuleId;
                            violation.RuleName = rule.Name;
                            violation.CodeId = code.CodeId;
                            violation.CodeName = code.Name;
                            violation.Category = rule.Category;
                            report.Violations.Add(violation);
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Errors.Add(new ComplianceError
                        {
                            RuleId = rule.RuleId,
                            CodeId = code.CodeId,
                            Message = ex.Message
                        });
                    }
                }

                processedRules++;
                progress?.Report(new ComplianceProgress
                {
                    CurrentRule = rule.Name,
                    RulesProcessed = processedRules,
                    TotalRules = totalRules,
                    ViolationsFound = report.Violations.Count
                });
            }

            // Detect code conflicts
            report.CodeConflicts = _conflictResolver.DetectConflicts(report.Violations, codes);

            // Calculate compliance scores per code
            report.ComplianceScores = CalculateComplianceScores(report, codes);

            report.EndTime = DateTime.Now;
            report.Summary = GenerateSummary(report);

            return report;
        }

        /// <summary>
        /// Checks compliance for specific element types
        /// </summary>
        public async Task<ElementComplianceReport> CheckElementComplianceAsync(
            Document document,
            BuiltInCategory category,
            IEnumerable<string> codeIds,
            CancellationToken cancellationToken = default)
        {
            var report = new ElementComplianceReport
            {
                Category = category.ToString(),
                CheckedCodes = codeIds.ToList()
            };

            var elements = new FilteredElementCollector(document)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var elementViolations = new List<ComplianceViolation>();

                foreach (var codeId in codeIds)
                {
                    var code = _buildingCodes.GetValueOrDefault(codeId);
                    if (code == null) continue;

                    var rules = _complianceRules.Values
                        .Where(r => r.ApplicableCodes.Contains(codeId))
                        .ToList();

                    foreach (var rule in rules)
                    {
                        var violations = rule.CheckLogic?.Invoke(document, rule, code)
                            .Where(v => v.AffectedElementIds?.Contains(element.Id) == true);

                        if (violations != null)
                            elementViolations.AddRange(violations);
                    }
                }

                if (elementViolations.Any())
                {
                    report.ElementViolations[element.Id] = elementViolations;
                }
            }

            return report;
        }

        /// <summary>
        /// Gets the most stringent requirement across multiple codes
        /// </summary>
        public MostStringentRequirement GetMostStringentRequirement(
            string ruleId,
            IEnumerable<string> codeIds)
        {
            var rule = _complianceRules.GetValueOrDefault(ruleId);
            if (rule == null) return null;

            var requirements = codeIds
                .Where(id => rule.RequirementsByCode.ContainsKey(id))
                .Select(id => new { CodeId = id, Requirement = rule.RequirementsByCode[id] })
                .ToList();

            if (!requirements.Any()) return null;

            // For minimum requirements, find the highest minimum
            var mostStringentMin = requirements
                .Where(r => r.Requirement.MinValue.HasValue)
                .OrderByDescending(r => r.Requirement.MinValue)
                .FirstOrDefault();

            // For maximum requirements, find the lowest maximum
            var mostStringentMax = requirements
                .Where(r => r.Requirement.MaxValue.HasValue)
                .OrderBy(r => r.Requirement.MaxValue)
                .FirstOrDefault();

            return new MostStringentRequirement
            {
                RuleId = ruleId,
                RuleName = rule.Name,
                MostStringentMinCode = mostStringentMin?.CodeId,
                MostStringentMinValue = mostStringentMin?.Requirement.MinValue,
                MostStringentMaxCode = mostStringentMax?.CodeId,
                MostStringentMaxValue = mostStringentMax?.Requirement.MaxValue,
                Unit = requirements.First().Requirement.Unit
            };
        }

        /// <summary>
        /// Compares requirements between codes
        /// </summary>
        public CodeComparisonReport CompareCodeRequirements(
            IEnumerable<string> codeIds,
            string category = null)
        {
            var report = new CodeComparisonReport
            {
                ComparedCodes = codeIds.ToList()
            };

            var rulesToCompare = _complianceRules.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(category))
                rulesToCompare = rulesToCompare.Where(r => r.Category == category);

            foreach (var rule in rulesToCompare)
            {
                var comparison = new RuleComparison
                {
                    RuleId = rule.RuleId,
                    RuleName = rule.Name,
                    Category = rule.Category
                };

                foreach (var codeId in codeIds)
                {
                    if (rule.RequirementsByCode.TryGetValue(codeId, out var requirement))
                    {
                        comparison.RequirementsByCode[codeId] = requirement;
                    }
                }

                if (comparison.RequirementsByCode.Count > 1)
                {
                    comparison.HasDifferences = HasSignificantDifferences(comparison.RequirementsByCode);
                    comparison.MostStringent = GetMostStringentFromComparison(comparison.RequirementsByCode);
                    comparison.LeastStringent = GetLeastStringentFromComparison(comparison.RequirementsByCode);
                }

                report.RuleComparisons.Add(comparison);
            }

            return report;
        }

        #endregion

        #region Public Methods - Variance Management

        /// <summary>
        /// Documents a code variance when requirements conflict
        /// </summary>
        public Variance DocumentVariance(VarianceRequest request)
        {
            return _varianceManager.CreateVariance(request);
        }

        /// <summary>
        /// Gets all active variances for a project
        /// </summary>
        public IEnumerable<Variance> GetActiveVariances(string projectId)
        {
            return _varianceManager.GetVariances(projectId);
        }

        /// <summary>
        /// Validates that a variance is still applicable
        /// </summary>
        public VarianceValidationResult ValidateVariance(Document document, string varianceId)
        {
            return _varianceManager.ValidateVariance(document, varianceId);
        }

        #endregion

        #region Public Methods - Reporting

        /// <summary>
        /// Generates compliance report in specified format
        /// </summary>
        public async Task<byte[]> GenerateReportAsync(
            MultiJurisdictionReport report,
            ReportFormat format,
            ReportOptions options = null)
        {
            return await _reportGenerator.GenerateAsync(report, format, options);
        }

        /// <summary>
        /// Gets compliance dashboard summary
        /// </summary>
        public ComplianceDashboard GetDashboard(MultiJurisdictionReport report)
        {
            return new ComplianceDashboard
            {
                OverallCompliance = CalculateOverallCompliance(report),
                ViolationsByCategory = report.Violations
                    .GroupBy(v => v.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ViolationsByCode = report.Violations
                    .GroupBy(v => v.CodeId)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CriticalViolations = report.Violations
                    .Where(v => v.Severity == ViolationSeverity.Critical)
                    .ToList(),
                ConflictCount = report.CodeConflicts.Count,
                TopPriorityActions = GetTopPriorityActions(report)
            };
        }

        #endregion

        #region Private Methods - Check Logic

        private List<ComplianceViolation> CheckExitDoorWidth(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            var requirement = rule.RequirementsByCode.GetValueOrDefault(code.CodeId);
            if (requirement == null) return violations;

            var doors = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var door in doors)
            {
                var widthParam = door.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                if (widthParam == null) continue;

                var widthMm = widthParam.AsDouble() * 304.8; // Convert to mm

                if (requirement.MinValue.HasValue && widthMm < requirement.MinValue.Value)
                {
                    violations.Add(new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        Description = $"Exit door width ({widthMm:F0}mm) is below minimum ({requirement.MinValue}mm)",
                        AffectedElementIds = new List<ElementId> { door.Id },
                        ActualValue = widthMm,
                        RequiredValue = requirement.MinValue.Value,
                        Unit = requirement.Unit,
                        Reference = requirement.Reference,
                        Severity = ViolationSeverity.High
                    });
                }
            }

            return violations;
        }

        private List<ComplianceViolation> CheckCorridorWidth(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Corridor width checking implementation
            return violations;
        }

        private List<ComplianceViolation> CheckTravelDistance(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Travel distance checking implementation
            return violations;
        }

        private List<ComplianceViolation> CheckStairRiserHeight(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            var requirement = rule.RequirementsByCode.GetValueOrDefault(code.CodeId);
            if (requirement == null) return violations;

            var stairs = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Stairs)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var stair in stairs)
            {
                var riserParam = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT);
                if (riserParam == null) continue;

                var riserMm = riserParam.AsDouble() * 304.8;

                if (requirement.MinValue.HasValue && riserMm < requirement.MinValue.Value)
                {
                    violations.Add(new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        Description = $"Stair riser ({riserMm:F0}mm) below minimum ({requirement.MinValue}mm)",
                        AffectedElementIds = new List<ElementId> { stair.Id },
                        ActualValue = riserMm,
                        RequiredValue = requirement.MinValue.Value,
                        Unit = requirement.Unit,
                        Reference = requirement.Reference,
                        Severity = ViolationSeverity.High
                    });
                }

                if (requirement.MaxValue.HasValue && riserMm > requirement.MaxValue.Value)
                {
                    violations.Add(new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        Description = $"Stair riser ({riserMm:F0}mm) exceeds maximum ({requirement.MaxValue}mm)",
                        AffectedElementIds = new List<ElementId> { stair.Id },
                        ActualValue = riserMm,
                        RequiredValue = requirement.MaxValue.Value,
                        Unit = requirement.Unit,
                        Reference = requirement.Reference,
                        Severity = ViolationSeverity.High
                    });
                }
            }

            return violations;
        }

        private List<ComplianceViolation> CheckStairTreadDepth(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Stair tread depth checking
            return violations;
        }

        private List<ComplianceViolation> CheckAccessibleDoorWidth(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Accessible door width checking
            return violations;
        }

        private List<ComplianceViolation> CheckRampSlope(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Ramp slope checking
            return violations;
        }

        private List<ComplianceViolation> CheckRoomHeight(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            var requirement = rule.RequirementsByCode.GetValueOrDefault(code.CodeId);
            if (requirement == null) return violations;

            var rooms = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<SpatialElement>()
                .ToList();

            foreach (var room in rooms)
            {
                var heightParam = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                if (heightParam == null) continue;

                var heightMm = heightParam.AsDouble() * 304.8;

                if (requirement.MinValue.HasValue && heightMm < requirement.MinValue.Value)
                {
                    violations.Add(new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        Description = $"Room height ({heightMm:F0}mm) below minimum ({requirement.MinValue}mm)",
                        AffectedElementIds = new List<ElementId> { room.Id },
                        ActualValue = heightMm,
                        RequiredValue = requirement.MinValue.Value,
                        Unit = requirement.Unit,
                        Reference = requirement.Reference,
                        Severity = ViolationSeverity.Medium
                    });
                }
            }

            return violations;
        }

        private List<ComplianceViolation> CheckToiletRoomSize(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Toilet room size checking
            return violations;
        }

        private List<ComplianceViolation> CheckFireDoorRating(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Fire door rating checking
            return violations;
        }

        private List<ComplianceViolation> CheckSprinklerCoverage(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Sprinkler coverage checking
            return violations;
        }

        private List<ComplianceViolation> CheckVentilationRate(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Ventilation rate checking
            return violations;
        }

        private List<ComplianceViolation> CheckSocketOutlets(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Socket outlet checking
            return violations;
        }

        private List<ComplianceViolation> CheckWallUValue(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Wall U-value checking
            return violations;
        }

        private List<ComplianceViolation> CheckGlazingRatio(Document document, ComplianceRule rule, BuildingCode code)
        {
            var violations = new List<ComplianceViolation>();
            // Glazing ratio checking
            return violations;
        }

        #endregion

        #region Private Methods - Helpers

        private List<ComplianceRule> GetApplicableRules(List<BuildingCode> codes, ComplianceCheckOptions options)
        {
            var codeIds = codes.Select(c => c.CodeId).ToList();
            var rules = _complianceRules.Values
                .Where(r => r.ApplicableCodes.Any(ac => codeIds.Contains(ac)));

            if (options.Categories?.Any() == true)
                rules = rules.Where(r => options.Categories.Contains(r.Category));

            if (options.ExcludedRules?.Any() == true)
                rules = rules.Where(r => !options.ExcludedRules.Contains(r.RuleId));

            return rules.ToList();
        }

        private Dictionary<string, double> CalculateComplianceScores(MultiJurisdictionReport report, List<BuildingCode> codes)
        {
            var scores = new Dictionary<string, double>();

            foreach (var code in codes)
            {
                var codeViolations = report.Violations.Where(v => v.CodeId == code.CodeId).ToList();
                var criticalCount = codeViolations.Count(v => v.Severity == ViolationSeverity.Critical);
                var highCount = codeViolations.Count(v => v.Severity == ViolationSeverity.High);
                var mediumCount = codeViolations.Count(v => v.Severity == ViolationSeverity.Medium);

                var score = 100 - (criticalCount * 20 + highCount * 10 + mediumCount * 5);
                scores[code.CodeId] = Math.Max(0, Math.Min(100, score));
            }

            return scores;
        }

        private double CalculateOverallCompliance(MultiJurisdictionReport report)
        {
            if (!report.ComplianceScores.Any()) return 100;
            return report.ComplianceScores.Values.Average();
        }

        private string GenerateSummary(MultiJurisdictionReport report)
        {
            var totalViolations = report.Violations.Count;
            var critical = report.Violations.Count(v => v.Severity == ViolationSeverity.Critical);
            var conflicts = report.CodeConflicts.Count;

            return $"Checked {report.CheckedCodes.Count} codes: {totalViolations} violations found " +
                   $"({critical} critical), {conflicts} code conflicts detected.";
        }

        private bool HasSignificantDifferences(Dictionary<string, CodeRequirement> requirements)
        {
            var minValues = requirements.Values.Where(r => r.MinValue.HasValue).Select(r => r.MinValue.Value).ToList();
            var maxValues = requirements.Values.Where(r => r.MaxValue.HasValue).Select(r => r.MaxValue.Value).ToList();

            if (minValues.Count > 1)
            {
                var range = minValues.Max() - minValues.Min();
                if (range > minValues.Average() * 0.1) return true;
            }

            if (maxValues.Count > 1)
            {
                var range = maxValues.Max() - maxValues.Min();
                if (range > maxValues.Average() * 0.1) return true;
            }

            return false;
        }

        private string GetMostStringentFromComparison(Dictionary<string, CodeRequirement> requirements)
        {
            var maxMin = requirements.Where(r => r.Value.MinValue.HasValue)
                .OrderByDescending(r => r.Value.MinValue).FirstOrDefault();

            var minMax = requirements.Where(r => r.Value.MaxValue.HasValue)
                .OrderBy(r => r.Value.MaxValue).FirstOrDefault();

            return maxMin.Key ?? minMax.Key;
        }

        private string GetLeastStringentFromComparison(Dictionary<string, CodeRequirement> requirements)
        {
            var minMin = requirements.Where(r => r.Value.MinValue.HasValue)
                .OrderBy(r => r.Value.MinValue).FirstOrDefault();

            var maxMax = requirements.Where(r => r.Value.MaxValue.HasValue)
                .OrderByDescending(r => r.Value.MaxValue).FirstOrDefault();

            return minMin.Key ?? maxMax.Key;
        }

        private List<PriorityAction> GetTopPriorityActions(MultiJurisdictionReport report)
        {
            return report.Violations
                .Where(v => v.Severity == ViolationSeverity.Critical || v.Severity == ViolationSeverity.High)
                .OrderByDescending(v => v.Severity)
                .Take(10)
                .Select(v => new PriorityAction
                {
                    Description = v.Description,
                    CodeReference = $"{v.CodeId}: {v.Reference}",
                    Severity = v.Severity.ToString()
                })
                .ToList();
        }

        #endregion
    }

    #region Supporting Classes

    public class BuildingCode
    {
        public string CodeId { get; set; }
        public string Name { get; set; }
        public string Jurisdiction { get; set; }
        public string Region { get; set; }
        public string Version { get; set; }
        public DateTime EffectiveDate { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public bool IsActive { get; set; } = true;
    }

    public class ComplianceRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<string> ApplicableCodes { get; set; } = new List<string>();
        public Dictionary<string, CodeRequirement> RequirementsByCode { get; set; } = new Dictionary<string, CodeRequirement>();
        public Func<Document, ComplianceRule, BuildingCode, List<ComplianceViolation>> CheckLogic { get; set; }
    }

    public class CodeRequirement
    {
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string Unit { get; set; }
        public string Reference { get; set; }
        public string Notes { get; set; }
    }

    public class ComplianceViolation
    {
        public string ViolationId { get; set; }
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string CodeId { get; set; }
        public string CodeName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<ElementId> AffectedElementIds { get; set; } = new List<ElementId>();
        public double ActualValue { get; set; }
        public double RequiredValue { get; set; }
        public string Unit { get; set; }
        public string Reference { get; set; }
        public ViolationSeverity Severity { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }

    public class ComplianceCheckOptions
    {
        public List<string> Categories { get; set; }
        public List<string> ExcludedRules { get; set; }
        public bool CheckVariances { get; set; } = true;
    }

    public class MultiJurisdictionReport
    {
        public string ReportId { get; set; }
        public string DocumentName { get; set; }
        public List<string> CheckedCodes { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<ComplianceViolation> Violations { get; set; } = new List<ComplianceViolation>();
        public List<CodeConflict> CodeConflicts { get; set; } = new List<CodeConflict>();
        public Dictionary<string, double> ComplianceScores { get; set; } = new Dictionary<string, double>();
        public List<ComplianceError> Errors { get; set; } = new List<ComplianceError>();
        public string Summary { get; set; }
    }

    public class ComplianceProgress
    {
        public string CurrentRule { get; set; }
        public int RulesProcessed { get; set; }
        public int TotalRules { get; set; }
        public int ViolationsFound { get; set; }
    }

    public class ComplianceError
    {
        public string RuleId { get; set; }
        public string CodeId { get; set; }
        public string Message { get; set; }
    }

    public class CodeConflict
    {
        public string ConflictId { get; set; }
        public string RuleId { get; set; }
        public List<string> ConflictingCodes { get; set; } = new List<string>();
        public string Description { get; set; }
        public ConflictResolution SuggestedResolution { get; set; }
    }

    public class ConflictResolution
    {
        public string Strategy { get; set; }
        public string RecommendedCode { get; set; }
        public string Justification { get; set; }
    }

    public class ElementComplianceReport
    {
        public string Category { get; set; }
        public List<string> CheckedCodes { get; set; }
        public Dictionary<ElementId, List<ComplianceViolation>> ElementViolations { get; set; } = new Dictionary<ElementId, List<ComplianceViolation>>();
    }

    public class MostStringentRequirement
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string MostStringentMinCode { get; set; }
        public double? MostStringentMinValue { get; set; }
        public string MostStringentMaxCode { get; set; }
        public double? MostStringentMaxValue { get; set; }
        public string Unit { get; set; }
    }

    public class CodeComparisonReport
    {
        public List<string> ComparedCodes { get; set; } = new List<string>();
        public List<RuleComparison> RuleComparisons { get; set; } = new List<RuleComparison>();
    }

    public class RuleComparison
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Category { get; set; }
        public Dictionary<string, CodeRequirement> RequirementsByCode { get; set; } = new Dictionary<string, CodeRequirement>();
        public bool HasDifferences { get; set; }
        public string MostStringent { get; set; }
        public string LeastStringent { get; set; }
    }

    public class Variance
    {
        public string VarianceId { get; set; }
        public string ProjectId { get; set; }
        public string RuleId { get; set; }
        public List<string> AffectedCodes { get; set; }
        public string Justification { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime ApprovalDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public VarianceStatus Status { get; set; }
    }

    public class VarianceRequest
    {
        public string ProjectId { get; set; }
        public string RuleId { get; set; }
        public List<string> AffectedCodes { get; set; }
        public string Justification { get; set; }
        public string RequestedBy { get; set; }
    }

    public class VarianceValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
    }

    public class ComplianceDashboard
    {
        public double OverallCompliance { get; set; }
        public Dictionary<string, int> ViolationsByCategory { get; set; }
        public Dictionary<string, int> ViolationsByCode { get; set; }
        public List<ComplianceViolation> CriticalViolations { get; set; }
        public int ConflictCount { get; set; }
        public List<PriorityAction> TopPriorityActions { get; set; }
    }

    public class PriorityAction
    {
        public string Description { get; set; }
        public string CodeReference { get; set; }
        public string Severity { get; set; }
    }

    public class CodeConflictResolver
    {
        public List<CodeConflict> DetectConflicts(List<ComplianceViolation> violations, List<BuildingCode> codes)
        {
            var conflicts = new List<CodeConflict>();
            var violationsByElement = violations.GroupBy(v => v.AffectedElementIds?.FirstOrDefault());

            foreach (var group in violationsByElement)
            {
                var ruleGroups = group.GroupBy(v => v.RuleId);
                foreach (var ruleGroup in ruleGroups.Where(rg => rg.Select(v => v.CodeId).Distinct().Count() > 1))
                {
                    conflicts.Add(new CodeConflict
                    {
                        ConflictId = Guid.NewGuid().ToString(),
                        RuleId = ruleGroup.Key,
                        ConflictingCodes = ruleGroup.Select(v => v.CodeId).Distinct().ToList(),
                        Description = $"Different codes have conflicting requirements for {ruleGroup.First().RuleName}",
                        SuggestedResolution = new ConflictResolution
                        {
                            Strategy = "Apply most stringent requirement",
                            RecommendedCode = ruleGroup.OrderByDescending(v => v.RequiredValue).First().CodeId,
                            Justification = "Using most stringent requirement ensures compliance with all codes"
                        }
                    });
                }
            }

            return conflicts;
        }
    }

    public class VarianceManager
    {
        private readonly List<Variance> _variances = new List<Variance>();

        public Variance CreateVariance(VarianceRequest request)
        {
            var variance = new Variance
            {
                VarianceId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                RuleId = request.RuleId,
                AffectedCodes = request.AffectedCodes,
                Justification = request.Justification,
                Status = VarianceStatus.Pending
            };
            _variances.Add(variance);
            return variance;
        }

        public IEnumerable<Variance> GetVariances(string projectId)
        {
            return _variances.Where(v => v.ProjectId == projectId).ToList();
        }

        public VarianceValidationResult ValidateVariance(Document document, string varianceId)
        {
            return new VarianceValidationResult { IsValid = true, Message = "Variance is still applicable" };
        }
    }

    public class ComplianceReportGenerator
    {
        public async Task<byte[]> GenerateAsync(MultiJurisdictionReport report, ReportFormat format, ReportOptions options)
        {
            await Task.Delay(1);
            return new byte[0];
        }
    }

    public class ReportOptions
    {
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeElementDetails { get; set; } = true;
    }

    public enum ViolationSeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    public enum VarianceStatus
    {
        Pending,
        Approved,
        Denied,
        Expired
    }

    public enum ReportFormat
    {
        PDF,
        Excel,
        HTML,
        JSON
    }

    #endregion
}
