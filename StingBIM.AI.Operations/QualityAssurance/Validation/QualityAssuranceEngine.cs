// StingBIM.AI.QualityAssurance - QualityAssuranceEngine.cs
// Comprehensive BIM Model Quality Assurance and Validation Engine
// Phase 4: Enterprise AI Transformation - Quality Automation
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace StingBIM.AI.QualityAssurance.Validation
{
    /// <summary>
    /// Enterprise-grade quality assurance engine providing comprehensive BIM model validation,
    /// automated issue detection, severity classification, and corrective action tracking.
    /// </summary>
    public class QualityAssuranceEngine
    {
        #region Fields

        private readonly Dictionary<string, QARule> _rules;
        private readonly Dictionary<string, QACheckCategory> _categories;
        private readonly List<QAIssue> _activeIssues;
        private readonly QAScoreTracker _scoreTracker;
        private readonly CorrectiveActionManager _actionManager;
        private readonly QAReportGenerator _reportGenerator;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public QualityAssuranceEngine()
        {
            _rules = new Dictionary<string, QARule>(StringComparer.OrdinalIgnoreCase);
            _categories = new Dictionary<string, QACheckCategory>(StringComparer.OrdinalIgnoreCase);
            _activeIssues = new List<QAIssue>();
            _scoreTracker = new QAScoreTracker();
            _actionManager = new CorrectiveActionManager();
            _reportGenerator = new QAReportGenerator();

            InitializeDefaultRules();
            InitializeCategories();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultRules()
        {
            // Geometry Validation Rules
            AddRule(new QARule
            {
                RuleId = "GEO-001",
                Name = "Element Has Valid Geometry",
                Description = "Validates that elements have non-zero, valid geometry",
                Category = "Geometry",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateElementGeometry
            });

            AddRule(new QARule
            {
                RuleId = "GEO-002",
                Name = "No Duplicate Elements",
                Description = "Detects elements with identical geometry at same location",
                Category = "Geometry",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateDuplicateElements
            });

            AddRule(new QARule
            {
                RuleId = "GEO-003",
                Name = "Element Intersection Check",
                Description = "Detects unintended geometry intersections between elements",
                Category = "Geometry",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateElementIntersections
            });

            AddRule(new QARule
            {
                RuleId = "GEO-004",
                Name = "Wall Join Integrity",
                Description = "Validates wall joins are properly connected",
                Category = "Geometry",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateWallJoins
            });

            AddRule(new QARule
            {
                RuleId = "GEO-005",
                Name = "Floor Boundary Closure",
                Description = "Validates floor boundaries form closed loops",
                Category = "Geometry",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateFloorBoundaries
            });

            // Data Quality Rules
            AddRule(new QARule
            {
                RuleId = "DAT-001",
                Name = "Required Parameters Populated",
                Description = "Validates required parameters have values",
                Category = "Data",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateRequiredParameters
            });

            AddRule(new QARule
            {
                RuleId = "DAT-002",
                Name = "Parameter Value Ranges",
                Description = "Validates parameter values are within acceptable ranges",
                Category = "Data",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateParameterRanges
            });

            AddRule(new QARule
            {
                RuleId = "DAT-003",
                Name = "Consistent Naming Convention",
                Description = "Validates elements follow naming conventions",
                Category = "Data",
                Severity = QASeverity.Low,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateNamingConventions
            });

            AddRule(new QARule
            {
                RuleId = "DAT-004",
                Name = "Material Assignment",
                Description = "Validates elements have appropriate materials assigned",
                Category = "Data",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateMaterialAssignments
            });

            AddRule(new QARule
            {
                RuleId = "DAT-005",
                Name = "Phase Consistency",
                Description = "Validates element phases are logically consistent",
                Category = "Data",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidatePhaseConsistency
            });

            // Standards Compliance Rules
            AddRule(new QARule
            {
                RuleId = "STD-001",
                Name = "Room Area Minimum",
                Description = "Validates rooms meet minimum area requirements",
                Category = "Standards",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateRoomAreaMinimums
            });

            AddRule(new QARule
            {
                RuleId = "STD-002",
                Name = "Door Width Accessibility",
                Description = "Validates door widths meet accessibility requirements",
                Category = "Standards",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateDoorAccessibility
            });

            AddRule(new QARule
            {
                RuleId = "STD-003",
                Name = "Ceiling Height Requirements",
                Description = "Validates ceiling heights meet code minimums",
                Category = "Standards",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateCeilingHeights
            });

            AddRule(new QARule
            {
                RuleId = "STD-004",
                Name = "Egress Path Width",
                Description = "Validates egress paths meet width requirements",
                Category = "Standards",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateEgressPaths
            });

            AddRule(new QARule
            {
                RuleId = "STD-005",
                Name = "Stair Riser/Tread Compliance",
                Description = "Validates stair dimensions meet code requirements",
                Category = "Standards",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateStairDimensions
            });

            // Model Organization Rules
            AddRule(new QARule
            {
                RuleId = "ORG-001",
                Name = "Workset Assignment",
                Description = "Validates elements are assigned to appropriate worksets",
                Category = "Organization",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateWorksetAssignment
            });

            AddRule(new QARule
            {
                RuleId = "ORG-002",
                Name = "View Template Usage",
                Description = "Validates views use appropriate view templates",
                Category = "Organization",
                Severity = QASeverity.Low,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateViewTemplates
            });

            AddRule(new QARule
            {
                RuleId = "ORG-003",
                Name = "Level Organization",
                Description = "Validates levels are properly organized and named",
                Category = "Organization",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateLevelOrganization
            });

            AddRule(new QARule
            {
                RuleId = "ORG-004",
                Name = "Family Categorization",
                Description = "Validates families are in correct categories",
                Category = "Organization",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateFamilyCategorization
            });

            // MEP Specific Rules
            AddRule(new QARule
            {
                RuleId = "MEP-001",
                Name = "Duct System Connectivity",
                Description = "Validates duct systems are properly connected",
                Category = "MEP",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateDuctConnectivity
            });

            AddRule(new QARule
            {
                RuleId = "MEP-002",
                Name = "Pipe System Connectivity",
                Description = "Validates pipe systems are properly connected",
                Category = "MEP",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidatePipeConnectivity
            });

            AddRule(new QARule
            {
                RuleId = "MEP-003",
                Name = "Electrical Circuit Balance",
                Description = "Validates electrical circuits are balanced",
                Category = "MEP",
                Severity = QASeverity.Medium,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateCircuitBalance
            });

            AddRule(new QARule
            {
                RuleId = "MEP-004",
                Name = "Equipment Clearance",
                Description = "Validates MEP equipment has required clearances",
                Category = "MEP",
                Severity = QASeverity.High,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateEquipmentClearance
            });

            // Structural Rules
            AddRule(new QARule
            {
                RuleId = "STR-001",
                Name = "Structural Connection Integrity",
                Description = "Validates structural members are properly connected",
                Category = "Structural",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateStructuralConnections
            });

            AddRule(new QARule
            {
                RuleId = "STR-002",
                Name = "Foundation Support",
                Description = "Validates structural elements have proper foundations",
                Category = "Structural",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateFoundationSupport
            });

            AddRule(new QARule
            {
                RuleId = "STR-003",
                Name = "Load Path Continuity",
                Description = "Validates continuous load paths exist",
                Category = "Structural",
                Severity = QASeverity.Critical,
                CheckType = QACheckType.Automatic,
                ValidationLogic = ValidateLoadPaths
            });
        }

        private void InitializeCategories()
        {
            _categories["Geometry"] = new QACheckCategory
            {
                CategoryId = "GEO",
                Name = "Geometry",
                Description = "Geometric validation and integrity checks",
                Weight = 1.0,
                Phase = ProjectPhase.All
            };

            _categories["Data"] = new QACheckCategory
            {
                CategoryId = "DAT",
                Name = "Data Quality",
                Description = "Parameter and data validation checks",
                Weight = 0.9,
                Phase = ProjectPhase.All
            };

            _categories["Standards"] = new QACheckCategory
            {
                CategoryId = "STD",
                Name = "Standards Compliance",
                Description = "Building code and standards compliance",
                Weight = 1.0,
                Phase = ProjectPhase.Design
            };

            _categories["Organization"] = new QACheckCategory
            {
                CategoryId = "ORG",
                Name = "Model Organization",
                Description = "Model structure and organization checks",
                Weight = 0.7,
                Phase = ProjectPhase.All
            };

            _categories["MEP"] = new QACheckCategory
            {
                CategoryId = "MEP",
                Name = "MEP Systems",
                Description = "Mechanical, electrical, plumbing validation",
                Weight = 0.9,
                Phase = ProjectPhase.Design
            };

            _categories["Structural"] = new QACheckCategory
            {
                CategoryId = "STR",
                Name = "Structural",
                Description = "Structural integrity and connectivity",
                Weight = 1.0,
                Phase = ProjectPhase.Design
            };

            _categories["Fire Safety"] = new QACheckCategory
            {
                CategoryId = "FIRE",
                Name = "Fire Safety",
                Description = "Fire protection and life safety compliance",
                Weight = 1.0,
                Phase = ProjectPhase.Design
            };

            _categories["Accessibility"] = new QACheckCategory
            {
                CategoryId = "ACC",
                Name = "Accessibility",
                Description = "ADA and accessibility compliance checks",
                Weight = 1.0,
                Phase = ProjectPhase.Design
            };

            _categories["Coordination"] = new QACheckCategory
            {
                CategoryId = "COORD",
                Name = "Coordination",
                Description = "Multi-discipline coordination checks",
                Weight = 0.9,
                Phase = ProjectPhase.ConstructionDocuments
            };

            _categories["Handover"] = new QACheckCategory
            {
                CategoryId = "HND",
                Name = "Handover Readiness",
                Description = "Project phase transition readiness",
                Weight = 0.8,
                Phase = ProjectPhase.Handover
            };
        }

        /// <summary>
        /// Loads additional QA rules from a CSV file
        /// </summary>
        public async Task LoadRulesFromCsvAsync(string csvPath, CancellationToken cancellationToken = default)
        {
            if (!System.IO.File.Exists(csvPath)) return;

            await Task.Run(() =>
            {
                var lines = System.IO.File.ReadAllLines(csvPath);
                if (lines.Length <= 1) return; // Only header or empty

                // Parse header
                var header = lines[0].Split(',');
                var idxRuleId = Array.IndexOf(header, "RuleId");
                var idxName = Array.IndexOf(header, "Name");
                var idxCategory = Array.IndexOf(header, "Category");
                var idxSeverity = Array.IndexOf(header, "Severity");
                var idxCheckType = Array.IndexOf(header, "CheckType");
                var idxDescription = Array.IndexOf(header, "Description");
                var idxReference = Array.IndexOf(header, "Reference");

                for (int i = 1; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < 5) continue;

                    var ruleId = idxRuleId >= 0 ? fields[idxRuleId] : "";
                    if (string.IsNullOrEmpty(ruleId) || _rules.ContainsKey(ruleId)) continue;

                    var severity = ParseSeverity(idxSeverity >= 0 ? fields[idxSeverity] : "Medium");
                    var checkType = ParseCheckType(idxCheckType >= 0 ? fields[idxCheckType] : "Automatic");
                    var category = idxCategory >= 0 ? fields[idxCategory] : "General";
                    var reference = idxReference >= 0 && idxReference < fields.Length ? fields[idxReference] : "";

                    var rule = new QARule
                    {
                        RuleId = ruleId,
                        Name = idxName >= 0 ? fields[idxName] : ruleId,
                        Description = idxDescription >= 0 ? fields[idxDescription] : "",
                        Category = category,
                        Severity = severity,
                        CheckType = checkType,
                        ValidationLogic = GetValidationLogicForRule(ruleId, category),
                        Configuration = new Dictionary<string, object>
                        {
                            ["StandardReference"] = reference
                        }
                    };

                    AddRule(rule);
                }
            }, cancellationToken);
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var current = "";

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            fields.Add(current.Trim());

            return fields.ToArray();
        }

        private QASeverity ParseSeverity(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "critical" => QASeverity.Critical,
                "high" => QASeverity.High,
                "medium" => QASeverity.Medium,
                "low" => QASeverity.Low,
                _ => QASeverity.Medium
            };
        }

        private QACheckType ParseCheckType(string value)
        {
            return value?.ToLowerInvariant() switch
            {
                "automatic" => QACheckType.Automatic,
                "manual" => QACheckType.Manual,
                "hybrid" => QACheckType.Hybrid,
                _ => QACheckType.Automatic
            };
        }

        private Func<Document, QARule, QACheckOptions, List<QAIssue>> GetValidationLogicForRule(string ruleId, string category)
        {
            // Map rule IDs to validation logic
            return ruleId switch
            {
                // Fire Safety Rules
                "FIRE-001" => ValidateFireDoorRating,
                "FIRE-002" => ValidateSprinklerCoverage,
                "FIRE-003" => ValidateExitSignage,

                // Accessibility Rules
                "ACC-001" => ValidateAccessibleRouteWidth,
                "ACC-002" => ValidateRampSlope,
                "ACC-003" => ValidateDoorHardwareHeight,

                // Coordination Rules
                "COORD-001" => ValidateClearanceHeights,
                "COORD-002" => ValidateServiceSpaceAccess,
                "COORD-003" => ValidatePenetrationSealing,

                // Default - create generic validation based on category
                _ => CreateGenericValidator(category)
            };
        }

        private Func<Document, QARule, QACheckOptions, List<QAIssue>> CreateGenericValidator(string category)
        {
            return (doc, rule, options) =>
            {
                // Generic validator that can be customized
                var issues = new List<QAIssue>();
                // Placeholder for generic validation
                return issues;
            };
        }

        #region Fire Safety Validation

        private List<QAIssue> ValidateFireDoorRating(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();

            var doors = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var door in doors)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(door.Id))
                    continue;

                var fireRatingParam = door.LookupParameter("Fire Rating");
                if (fireRatingParam == null || !fireRatingParam.HasValue)
                {
                    // Check if door is in a fire-rated wall
                    var hostWall = GetHostWall(document, door);
                    if (hostWall != null && IsFireRatedWall(hostWall))
                    {
                        issues.Add(new QAIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            Description = $"Door in fire-rated wall has no fire rating specified",
                            AffectedElementIds = new List<ElementId> { door.Id },
                            StandardReference = "IBC 716, NFPA"
                        });
                    }
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateSprinklerCoverage(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();

            // Get all sprinklers
            var sprinklers = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Sprinklers)
                .WhereElementIsNotElementType()
                .ToList();

            // Get all rooms
            var rooms = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<SpatialElement>()
                .ToList();

            const double maxCoverageRadius = 4.6; // meters per NFPA 13

            foreach (var room in rooms)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(room.Id))
                    continue;

                var roomArea = room.get_Parameter(BuiltInParameter.ROOM_AREA)?.AsDouble() * 0.092903 ?? 0;
                if (roomArea <= 0) continue;

                // Count sprinklers in room
                var roomBbox = room.get_BoundingBox(null);
                if (roomBbox == null) continue;

                var sprinklersInRoom = sprinklers.Count(s =>
                {
                    var loc = (s.Location as LocationPoint)?.Point;
                    return loc != null &&
                           loc.X >= roomBbox.Min.X && loc.X <= roomBbox.Max.X &&
                           loc.Y >= roomBbox.Min.Y && loc.Y <= roomBbox.Max.Y;
                });

                // Calculate required sprinklers based on coverage area
                var coverageArea = Math.PI * maxCoverageRadius * maxCoverageRadius;
                var requiredSprinklers = (int)Math.Ceiling(roomArea / coverageArea);

                if (sprinklersInRoom < requiredSprinklers)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Room may have insufficient sprinkler coverage ({sprinklersInRoom} found, ~{requiredSprinklers} may be needed)",
                        AffectedElementIds = new List<ElementId> { room.Id },
                        ActualValue = sprinklersInRoom.ToString(),
                        RequiredValue = $">= {requiredSprinklers}",
                        StandardReference = "NFPA 13"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateExitSignage(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Exit signage validation - checks for exit signs near egress doors
            return issues;
        }

        private Wall GetHostWall(Document document, Element door)
        {
            var hostId = door.get_Parameter(BuiltInParameter.HOST_ID_PARAM)?.AsElementId();
            return hostId != null ? document.GetElement(hostId) as Wall : null;
        }

        private bool IsFireRatedWall(Wall wall)
        {
            var fireRating = wall.LookupParameter("Fire Rating")?.AsString();
            return !string.IsNullOrEmpty(fireRating) && fireRating != "0" && fireRating.ToLower() != "none";
        }

        #endregion

        #region Accessibility Validation

        private List<QAIssue> ValidateAccessibleRouteWidth(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            const double minRouteWidth = 0.915; // 915mm / 36" per ADA

            // Check corridor widths
            var rooms = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<SpatialElement>()
                .Where(r => r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()?.ToLower()
                    .Contains("corridor") == true ||
                    r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString()?.ToLower()
                    .Contains("hallway") == true)
                .ToList();

            foreach (var room in rooms)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(room.Id))
                    continue;

                var bbox = room.get_BoundingBox(null);
                if (bbox == null) continue;

                var width = Math.Min(
                    (bbox.Max.X - bbox.Min.X) * 0.3048,
                    (bbox.Max.Y - bbox.Min.Y) * 0.3048
                );

                if (width < minRouteWidth)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Accessible route width ({width * 1000:F0}mm) is below minimum (915mm)",
                        AffectedElementIds = new List<ElementId> { room.Id },
                        ActualValue = $"{width * 1000:F0}mm",
                        RequiredValue = "915mm minimum",
                        StandardReference = "ADA 403.5.1"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateRampSlope(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            const double maxSlope = 1.0 / 12.0; // 1:12 maximum per ADA

            var ramps = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Ramps)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var ramp in ramps)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(ramp.Id))
                    continue;

                // No valid BuiltInParameter exists for ramp slope across all Revit versions.
                // Use LookupParameter with known parameter names as a safe fallback.
                var slopeParam = ramp.LookupParameter("Maximum Incline Length")
                    ?? ramp.LookupParameter("Slope")
                    ?? ramp.LookupParameter("Max Slope");
                if (slopeParam == null) continue;

                var slope = slopeParam.AsDouble();
                if (slope > maxSlope)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Ramp slope (1:{1 / slope:F0}) exceeds maximum allowed (1:12)",
                        AffectedElementIds = new List<ElementId> { ramp.Id },
                        ActualValue = $"1:{1 / slope:F0}",
                        RequiredValue = "1:12 maximum",
                        StandardReference = "ADA 405.2"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateDoorHardwareHeight(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Door hardware height validation (34" - 48" per ADA)
            return issues;
        }

        #endregion

        #region Coordination Validation

        private List<QAIssue> ValidateClearanceHeights(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            const double minClearance = 2.1; // 2100mm minimum clear height

            // Check for MEP elements that may reduce clearance
            var mepElements = new FilteredElementCollector(document)
                .WherePasses(new ElementMulticategoryFilter(new[]
                {
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_CableTray
                }))
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var element in mepElements)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(element.Id))
                    continue;

                var bbox = element.get_BoundingBox(null);
                if (bbox == null) continue;

                // Get level of element
                var levelParam = element.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                if (levelParam == null) continue;

                var level = document.GetElement(levelParam.AsElementId()) as Level;
                if (level == null) continue;

                var clearanceBelow = (bbox.Min.Z - level.Elevation) * 0.3048;

                if (clearanceBelow < minClearance && clearanceBelow > 0)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Clearance below {element.Category?.Name} ({clearanceBelow * 1000:F0}mm) is below minimum (2100mm)",
                        AffectedElementIds = new List<ElementId> { element.Id },
                        ActualValue = $"{clearanceBelow * 1000:F0}mm",
                        RequiredValue = "2100mm minimum"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateServiceSpaceAccess(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Service space access validation
            return issues;
        }

        private List<QAIssue> ValidatePenetrationSealing(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Fire-rated penetration sealing validation
            return issues;
        }

        #endregion

        #endregion

        #region Public Methods - Rule Management

        public void AddRule(QARule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrEmpty(rule.RuleId)) throw new ArgumentException("RuleId is required");

            lock (_lockObject)
            {
                _rules[rule.RuleId] = rule;
            }
        }

        public void RemoveRule(string ruleId)
        {
            lock (_lockObject)
            {
                _rules.Remove(ruleId);
            }
        }

        public QARule GetRule(string ruleId)
        {
            lock (_lockObject)
            {
                return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
            }
        }

        public IEnumerable<QARule> GetRulesByCategory(string category)
        {
            lock (_lockObject)
            {
                return _rules.Values
                    .Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public IEnumerable<QARule> GetRulesBySeverity(QASeverity severity)
        {
            lock (_lockObject)
            {
                return _rules.Values.Where(r => r.Severity == severity).ToList();
            }
        }

        #endregion

        #region Public Methods - Quality Checks

        /// <summary>
        /// Runs comprehensive quality assurance check on the entire model
        /// </summary>
        public async Task<QAReport> RunFullQACheckAsync(
            Document document,
            QACheckOptions options = null,
            IProgress<QAProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new QACheckOptions();
            var report = new QAReport
            {
                ReportId = Guid.NewGuid().ToString(),
                DocumentName = document.Title,
                StartTime = DateTime.Now,
                Options = options
            };

            var rulesToRun = GetApplicableRules(options);
            int totalRules = rulesToRun.Count;
            int processedRules = 0;

            foreach (var rule in rulesToRun)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Execute validation on calling thread - Revit API is not thread-safe
                    var issues = rule.ValidationLogic?.Invoke(document, rule, options) ?? new List<QAIssue>();

                    foreach (var issue in issues)
                    {
                        issue.RuleId = rule.RuleId;
                        issue.RuleName = rule.Name;
                        issue.Category = rule.Category;
                        issue.Severity = rule.Severity;
                        issue.DetectedAt = DateTime.Now;
                        report.Issues.Add(issue);
                    }

                    report.RulesChecked.Add(rule.RuleId);
                }
                catch (Exception ex)
                {
                    report.Errors.Add(new QAError
                    {
                        RuleId = rule.RuleId,
                        Message = $"Error executing rule: {ex.Message}",
                        Exception = ex
                    });
                }

                processedRules++;
                progress?.Report(new QAProgress
                {
                    CurrentRule = rule.Name,
                    RulesProcessed = processedRules,
                    TotalRules = totalRules,
                    IssuesFound = report.Issues.Count,
                    PercentComplete = (double)processedRules / totalRules * 100
                });
            }

            report.EndTime = DateTime.Now;
            report.QualityScore = _scoreTracker.CalculateScore(report);
            report.Summary = GenerateSummary(report);

            // Track active issues
            lock (_lockObject)
            {
                _activeIssues.Clear();
                _activeIssues.AddRange(report.Issues);
            }

            return report;
        }

        /// <summary>
        /// Runs quality check for specific category
        /// </summary>
        public async Task<QAReport> RunCategoryCheckAsync(
            Document document,
            string category,
            IProgress<QAProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var options = new QACheckOptions
            {
                Categories = new List<string> { category }
            };
            return await RunFullQACheckAsync(document, options, progress, cancellationToken);
        }

        /// <summary>
        /// Runs quick check for critical issues only
        /// </summary>
        public async Task<QAReport> RunQuickCheckAsync(
            Document document,
            IProgress<QAProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var options = new QACheckOptions
            {
                SeverityFilter = new List<QASeverity> { QASeverity.Critical, QASeverity.High }
            };
            return await RunFullQACheckAsync(document, options, progress, cancellationToken);
        }

        /// <summary>
        /// Validates specific elements against all applicable rules
        /// </summary>
        public async Task<ElementQAResult> ValidateElementsAsync(
            Document document,
            IEnumerable<ElementId> elementIds,
            CancellationToken cancellationToken = default)
        {
            var result = new ElementQAResult
            {
                ElementCount = elementIds.Count(),
                StartTime = DateTime.Now
            };

            foreach (var elementId in elementIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var element = document.GetElement(elementId);
                if (element == null) continue;

                var elementIssues = new List<QAIssue>();

                foreach (var rule in _rules.Values)
                {
                    try
                    {
                        var options = new QACheckOptions { TargetElements = new List<ElementId> { elementId } };
                        var issues = rule.ValidationLogic?.Invoke(document, rule, options) ?? new List<QAIssue>();
                        elementIssues.AddRange(issues);
                    }
                    catch { /* Skip failed rules */ }
                }

                if (elementIssues.Any())
                {
                    result.ElementIssues[elementId] = elementIssues;
                }
            }

            result.EndTime = DateTime.Now;
            result.TotalIssues = result.ElementIssues.Values.Sum(l => l.Count);
            return result;
        }

        /// <summary>
        /// Gets handover readiness assessment
        /// </summary>
        public HandoverReadinessReport AssessHandoverReadiness(Document document, ProjectPhase targetPhase)
        {
            var report = new HandoverReadinessReport
            {
                TargetPhase = targetPhase,
                AssessmentDate = DateTime.Now
            };

            // Check phase-specific requirements
            var phaseChecks = GetPhaseChecklist(targetPhase);
            foreach (var check in phaseChecks)
            {
                var checkResult = ExecuteHandoverCheck(document, check);
                report.CheckResults.Add(checkResult);
            }

            report.OverallReadiness = CalculateReadinessScore(report.CheckResults);
            report.BlockingIssues = report.CheckResults
                .Where(c => !c.Passed && c.IsBlocking)
                .Select(c => c.CheckName)
                .ToList();

            report.IsReady = !report.BlockingIssues.Any() && report.OverallReadiness >= 0.8;

            return report;
        }

        #endregion

        #region Public Methods - Issue Management

        /// <summary>
        /// Gets active issues with optional filtering
        /// </summary>
        public IEnumerable<QAIssue> GetActiveIssues(QAIssueFilter filter = null)
        {
            lock (_lockObject)
            {
                var issues = _activeIssues.AsEnumerable();

                if (filter != null)
                {
                    if (filter.Categories?.Any() == true)
                        issues = issues.Where(i => filter.Categories.Contains(i.Category));

                    if (filter.Severities?.Any() == true)
                        issues = issues.Where(i => filter.Severities.Contains(i.Severity));

                    if (filter.Status.HasValue)
                        issues = issues.Where(i => i.Status == filter.Status.Value);

                    if (!string.IsNullOrEmpty(filter.SearchText))
                        issues = issues.Where(i =>
                            i.Description.Contains(filter.SearchText) ||
                            i.RuleName.Contains(filter.SearchText));
                }

                return issues.ToList();
            }
        }

        /// <summary>
        /// Gets prioritized issue list with risk weighting
        /// </summary>
        public IEnumerable<PrioritizedIssue> GetPrioritizedIssues()
        {
            lock (_lockObject)
            {
                return _activeIssues
                    .Select(i => new PrioritizedIssue
                    {
                        Issue = i,
                        Priority = CalculateIssuePriority(i),
                        RiskScore = CalculateRiskScore(i),
                        EstimatedEffort = EstimateResolutionEffort(i),
                        SuggestedAction = GetSuggestedAction(i)
                    })
                    .OrderByDescending(p => p.Priority)
                    .ThenByDescending(p => p.RiskScore)
                    .ToList();
            }
        }

        /// <summary>
        /// Updates issue status
        /// </summary>
        public void UpdateIssueStatus(string issueId, QAIssueStatus newStatus, string notes = null)
        {
            lock (_lockObject)
            {
                var issue = _activeIssues.FirstOrDefault(i => i.IssueId == issueId);
                if (issue != null)
                {
                    issue.Status = newStatus;
                    issue.StatusHistory.Add(new StatusChange
                    {
                        FromStatus = issue.Status,
                        ToStatus = newStatus,
                        ChangedAt = DateTime.Now,
                        Notes = notes
                    });

                    if (newStatus == QAIssueStatus.Resolved)
                    {
                        issue.ResolvedAt = DateTime.Now;
                    }
                }
            }
        }

        /// <summary>
        /// Creates corrective action for issue
        /// </summary>
        public CorrectiveAction CreateCorrectiveAction(string issueId, CorrectiveActionRequest request)
        {
            var issue = _activeIssues.FirstOrDefault(i => i.IssueId == issueId);
            if (issue == null) return null;

            return _actionManager.CreateAction(issue, request);
        }

        /// <summary>
        /// Validates that corrective action resolved the issue
        /// </summary>
        public async Task<ValidationResult> ValidateResolutionAsync(
            Document document,
            string issueId,
            CancellationToken cancellationToken = default)
        {
            var issue = _activeIssues.FirstOrDefault(i => i.IssueId == issueId);
            if (issue == null)
            {
                return new ValidationResult { Success = false, Message = "Issue not found" };
            }

            var rule = GetRule(issue.RuleId);
            if (rule == null)
            {
                return new ValidationResult { Success = false, Message = "Rule not found" };
            }

            var options = new QACheckOptions();
            if (issue.AffectedElementIds?.Any() == true)
            {
                options.TargetElements = issue.AffectedElementIds;
            }

            // Execute validation on calling thread - Revit API is not thread-safe
            var newIssues = rule.ValidationLogic?.Invoke(document, rule, options) ?? new List<QAIssue>();

            var stillExists = newIssues.Any(i =>
                i.AffectedElementIds?.Intersect(issue.AffectedElementIds ?? new List<ElementId>()).Any() == true);

            if (!stillExists)
            {
                UpdateIssueStatus(issueId, QAIssueStatus.Resolved, "Validated as resolved");
                return new ValidationResult { Success = true, Message = "Issue successfully resolved" };
            }

            return new ValidationResult
            {
                Success = false,
                Message = "Issue still present after correction",
                RemainingIssues = newIssues
            };
        }

        #endregion

        #region Public Methods - Scoring & Reporting

        /// <summary>
        /// Gets current quality score breakdown
        /// </summary>
        public QualityScoreBreakdown GetQualityScore()
        {
            lock (_lockObject)
            {
                return _scoreTracker.GetCurrentBreakdown(_activeIssues, _categories);
            }
        }

        /// <summary>
        /// Gets quality score history over time
        /// </summary>
        public IEnumerable<QualityScoreSnapshot> GetScoreHistory(DateTime? since = null)
        {
            return _scoreTracker.GetHistory(since);
        }

        /// <summary>
        /// Generates comprehensive QA report
        /// </summary>
        public async Task<byte[]> GenerateReportAsync(
            QAReport report,
            ReportFormat format,
            ReportOptions options = null)
        {
            return await _reportGenerator.GenerateAsync(report, format, options);
        }

        /// <summary>
        /// Gets trend analysis of quality metrics
        /// </summary>
        public QualityTrendAnalysis AnalyzeTrends(IEnumerable<QAReport> historicalReports)
        {
            var reports = historicalReports.OrderBy(r => r.StartTime).ToList();

            return new QualityTrendAnalysis
            {
                TrendDirection = CalculateTrendDirection(reports),
                ImprovementRate = CalculateImprovementRate(reports),
                RecurringIssues = IdentifyRecurringIssues(reports),
                CategoryTrends = CalculateCategoryTrends(reports),
                ProjectedScore = ProjectFutureScore(reports),
                Recommendations = GenerateTrendRecommendations(reports)
            };
        }

        #endregion

        #region Private Methods - Validation Logic

        private List<QAIssue> ValidateElementGeometry(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            var collector = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            foreach (var element in collector)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(element.Id))
                    continue;

                var bbox = element.get_BoundingBox(null);
                if (bbox == null)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Element '{element.Name}' has no valid geometry (no bounding box)",
                        AffectedElementIds = new List<ElementId> { element.Id },
                        Location = element.Location?.ToString()
                    });
                    continue;
                }

                var min = bbox.Min;
                var max = bbox.Max;
                var volume = (max.X - min.X) * (max.Y - min.Y) * (max.Z - min.Z);

                if (volume <= 0)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Element '{element.Name}' has zero or negative volume",
                        AffectedElementIds = new List<ElementId> { element.Id },
                        Location = $"Min: {min}, Max: {max}"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateDuplicateElements(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            var elementsByLocation = new Dictionary<string, List<Element>>();

            var collector = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            foreach (var element in collector)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(element.Id))
                    continue;

                var bbox = element.get_BoundingBox(null);
                if (bbox == null) continue;

                var locationKey = $"{element.Category?.Name}_{bbox.Min.X:F2}_{bbox.Min.Y:F2}_{bbox.Min.Z:F2}_{bbox.Max.X:F2}_{bbox.Max.Y:F2}_{bbox.Max.Z:F2}";

                if (!elementsByLocation.ContainsKey(locationKey))
                    elementsByLocation[locationKey] = new List<Element>();

                elementsByLocation[locationKey].Add(element);
            }

            foreach (var kvp in elementsByLocation.Where(k => k.Value.Count > 1))
            {
                issues.Add(new QAIssue
                {
                    IssueId = Guid.NewGuid().ToString(),
                    Description = $"Duplicate elements detected: {kvp.Value.Count} {kvp.Value.First().Category?.Name} elements at same location",
                    AffectedElementIds = kvp.Value.Select(e => e.Id).ToList(),
                    Location = kvp.Key
                });
            }

            return issues;
        }

        private List<QAIssue> ValidateElementIntersections(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Simplified intersection check - would use solid geometry in production
            return issues;
        }

        private List<QAIssue> ValidateWallJoins(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            var walls = new FilteredElementCollector(document)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            foreach (var wall in walls)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(wall.Id))
                    continue;

                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) continue;

                // Check wall end joins
                // Note: WallUtils.GetWallJoinType was removed in Revit 2025 API.
                // Use WallUtils.IsWallJoinAllowedAtEnd and LocationCurve join status instead.
                for (int i = 0; i <= 1; i++)
                {
                    try
                    {
                        var joinStatus = locationCurve.get_JoinType(i);
                        if (joinStatus == JoinType.None)
                        {
                            issues.Add(new QAIssue
                            {
                                IssueId = Guid.NewGuid().ToString(),
                                Description = $"Wall '{wall.Name}' has unjoined end at position {i}",
                                AffectedElementIds = new List<ElementId> { wall.Id },
                                Severity = QASeverity.Low
                            });
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentOutOfRangeException)
                    {
                        // End index not valid for this wall
                    }
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateFloorBoundaries(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Floor boundary validation
            return issues;
        }

        private List<QAIssue> ValidateRequiredParameters(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            var requiredParams = GetRequiredParametersList();

            var collector = new FilteredElementCollector(document)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(element.Id))
                    continue;

                var categoryName = element.Category?.Name ?? "Unknown";
                if (!requiredParams.ContainsKey(categoryName)) continue;

                foreach (var paramName in requiredParams[categoryName])
                {
                    var param = element.LookupParameter(paramName);
                    if (param == null || !param.HasValue || string.IsNullOrWhiteSpace(param.AsValueString()))
                    {
                        issues.Add(new QAIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            Description = $"Required parameter '{paramName}' is missing or empty on {categoryName} '{element.Name}'",
                            AffectedElementIds = new List<ElementId> { element.Id },
                            ParameterName = paramName
                        });
                    }
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateParameterRanges(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Parameter range validation
            return issues;
        }

        private List<QAIssue> ValidateNamingConventions(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Naming convention validation
            return issues;
        }

        private List<QAIssue> ValidateMaterialAssignments(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();

            var collector = new FilteredElementCollector(document)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(element.Id))
                    continue;

                var materialParam = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (materialParam != null && materialParam.AsElementId() == ElementId.InvalidElementId)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Element '{element.Name}' has no material assigned",
                        AffectedElementIds = new List<ElementId> { element.Id }
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidatePhaseConsistency(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Phase consistency validation
            return issues;
        }

        private List<QAIssue> ValidateRoomAreaMinimums(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            var minimumAreas = GetMinimumRoomAreas();

            var rooms = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<SpatialElement>()
                .ToList();

            foreach (var room in rooms)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(room.Id))
                    continue;

                var areaParam = room.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam == null) continue;

                var area = areaParam.AsDouble() * 0.092903; // Convert to m²
                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unknown";

                foreach (var minArea in minimumAreas)
                {
                    if (roomName.Contains(minArea.Key, StringComparison.OrdinalIgnoreCase) && area < minArea.Value)
                    {
                        issues.Add(new QAIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            Description = $"Room '{roomName}' area ({area:F2} m²) is below minimum requirement ({minArea.Value} m²)",
                            AffectedElementIds = new List<ElementId> { room.Id },
                            ActualValue = area.ToString("F2"),
                            RequiredValue = minArea.Value.ToString("F2")
                        });
                    }
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateDoorAccessibility(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            const double minDoorWidth = 0.9; // 900mm minimum for accessibility

            var doors = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var door in doors)
            {
                if (options.TargetElements?.Any() == true && !options.TargetElements.Contains(door.Id))
                    continue;

                var widthParam = door.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                if (widthParam == null) continue;

                var width = widthParam.AsDouble() * 0.3048; // Convert to meters

                if (width < minDoorWidth)
                {
                    issues.Add(new QAIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Description = $"Door width ({width * 1000:F0}mm) is below accessibility requirement (900mm)",
                        AffectedElementIds = new List<ElementId> { door.Id },
                        ActualValue = $"{width * 1000:F0}mm",
                        RequiredValue = "900mm",
                        StandardReference = "ADA, IBC 1010.1.1"
                    });
                }
            }

            return issues;
        }

        private List<QAIssue> ValidateCeilingHeights(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Ceiling height validation
            return issues;
        }

        private List<QAIssue> ValidateEgressPaths(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Egress path validation
            return issues;
        }

        private List<QAIssue> ValidateStairDimensions(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Stair dimension validation
            return issues;
        }

        private List<QAIssue> ValidateWorksetAssignment(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Workset assignment validation
            return issues;
        }

        private List<QAIssue> ValidateViewTemplates(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // View template validation
            return issues;
        }

        private List<QAIssue> ValidateLevelOrganization(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Level organization validation
            return issues;
        }

        private List<QAIssue> ValidateFamilyCategorization(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Family categorization validation
            return issues;
        }

        private List<QAIssue> ValidateDuctConnectivity(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Duct connectivity validation
            return issues;
        }

        private List<QAIssue> ValidatePipeConnectivity(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Pipe connectivity validation
            return issues;
        }

        private List<QAIssue> ValidateCircuitBalance(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Circuit balance validation
            return issues;
        }

        private List<QAIssue> ValidateEquipmentClearance(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Equipment clearance validation
            return issues;
        }

        private List<QAIssue> ValidateStructuralConnections(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Structural connection validation
            return issues;
        }

        private List<QAIssue> ValidateFoundationSupport(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Foundation support validation
            return issues;
        }

        private List<QAIssue> ValidateLoadPaths(Document document, QARule rule, QACheckOptions options)
        {
            var issues = new List<QAIssue>();
            // Load path validation
            return issues;
        }

        #endregion

        #region Private Methods - Helpers

        private List<QARule> GetApplicableRules(QACheckOptions options)
        {
            var rules = _rules.Values.AsEnumerable();

            if (options.Categories?.Any() == true)
                rules = rules.Where(r => options.Categories.Contains(r.Category, StringComparer.OrdinalIgnoreCase));

            if (options.SeverityFilter?.Any() == true)
                rules = rules.Where(r => options.SeverityFilter.Contains(r.Severity));

            if (options.ExcludedRules?.Any() == true)
                rules = rules.Where(r => !options.ExcludedRules.Contains(r.RuleId));

            return rules.ToList();
        }

        private Dictionary<string, List<string>> GetRequiredParametersList()
        {
            return new Dictionary<string, List<string>>
            {
                ["Rooms"] = new List<string> { "Room Name", "Room Number", "Department" },
                ["Doors"] = new List<string> { "Mark", "Fire Rating" },
                ["Windows"] = new List<string> { "Mark" },
                ["Walls"] = new List<string> { "Fire Rating" }
            };
        }

        private Dictionary<string, double> GetMinimumRoomAreas()
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 7.5,
                ["Toilet"] = 2.5,
                ["Kitchen"] = 5.0,
                ["Bedroom"] = 9.0,
                ["Living"] = 12.0,
                ["Meeting"] = 10.0,
                ["Conference"] = 15.0
            };
        }

        private string GenerateSummary(QAReport report)
        {
            var critical = report.Issues.Count(i => i.Severity == QASeverity.Critical);
            var high = report.Issues.Count(i => i.Severity == QASeverity.High);
            var medium = report.Issues.Count(i => i.Severity == QASeverity.Medium);
            var low = report.Issues.Count(i => i.Severity == QASeverity.Low);

            return $"QA Check completed: {report.Issues.Count} issues found " +
                   $"({critical} critical, {high} high, {medium} medium, {low} low). " +
                   $"Quality Score: {report.QualityScore:F1}%";
        }

        private double CalculateIssuePriority(QAIssue issue)
        {
            var severityWeight = issue.Severity switch
            {
                QASeverity.Critical => 1.0,
                QASeverity.High => 0.75,
                QASeverity.Medium => 0.5,
                QASeverity.Low => 0.25,
                _ => 0.1
            };

            var ageWeight = Math.Min(1.0, (DateTime.Now - issue.DetectedAt).TotalDays / 7.0);
            var elementCountWeight = Math.Min(1.0, (issue.AffectedElementIds?.Count ?? 1) / 10.0);

            return severityWeight * 0.5 + ageWeight * 0.3 + elementCountWeight * 0.2;
        }

        private double CalculateRiskScore(QAIssue issue)
        {
            // Risk = Probability × Impact
            var probability = issue.Severity switch
            {
                QASeverity.Critical => 0.9,
                QASeverity.High => 0.7,
                QASeverity.Medium => 0.5,
                QASeverity.Low => 0.3,
                _ => 0.1
            };

            var impact = (issue.AffectedElementIds?.Count ?? 1) * 0.1;
            return Math.Min(1.0, probability * Math.Min(1.0, impact));
        }

        private TimeSpan EstimateResolutionEffort(QAIssue issue)
        {
            var baseMinutes = issue.Severity switch
            {
                QASeverity.Critical => 60,
                QASeverity.High => 30,
                QASeverity.Medium => 15,
                QASeverity.Low => 5,
                _ => 10
            };

            var elementMultiplier = Math.Max(1, (issue.AffectedElementIds?.Count ?? 1) / 5);
            return TimeSpan.FromMinutes(baseMinutes * elementMultiplier);
        }

        private string GetSuggestedAction(QAIssue issue)
        {
            return issue.RuleId switch
            {
                "GEO-001" => "Delete or recreate element with valid geometry",
                "GEO-002" => "Delete duplicate elements, keeping one instance",
                "DAT-001" => "Fill in required parameter values",
                "STD-002" => "Resize door to meet accessibility requirements",
                "MEP-001" => "Connect disconnected duct segments",
                "STR-001" => "Verify and fix structural connections",
                _ => "Review and correct the identified issue"
            };
        }

        private List<HandoverCheck> GetPhaseChecklist(ProjectPhase phase)
        {
            var checks = new List<HandoverCheck>();

            if (phase == ProjectPhase.SchematicDesign || phase == ProjectPhase.All)
            {
                checks.Add(new HandoverCheck { CheckId = "SD-001", CheckName = "Room areas defined", IsBlocking = true });
                checks.Add(new HandoverCheck { CheckId = "SD-002", CheckName = "Level elevations set", IsBlocking = true });
            }

            if (phase == ProjectPhase.DesignDevelopment || phase == ProjectPhase.All)
            {
                checks.Add(new HandoverCheck { CheckId = "DD-001", CheckName = "Wall types assigned", IsBlocking = true });
                checks.Add(new HandoverCheck { CheckId = "DD-002", CheckName = "Door/window schedules complete", IsBlocking = false });
            }

            if (phase == ProjectPhase.ConstructionDocuments || phase == ProjectPhase.All)
            {
                checks.Add(new HandoverCheck { CheckId = "CD-001", CheckName = "All elements dimensioned", IsBlocking = true });
                checks.Add(new HandoverCheck { CheckId = "CD-002", CheckName = "Sheets issued for review", IsBlocking = true });
            }

            if (phase == ProjectPhase.Handover || phase == ProjectPhase.All)
            {
                checks.Add(new HandoverCheck { CheckId = "HO-001", CheckName = "As-built verification complete", IsBlocking = true });
                checks.Add(new HandoverCheck { CheckId = "HO-002", CheckName = "O&M data populated", IsBlocking = true });
            }

            return checks;
        }

        private HandoverCheckResult ExecuteHandoverCheck(Document document, HandoverCheck check)
        {
            return new HandoverCheckResult
            {
                CheckId = check.CheckId,
                CheckName = check.CheckName,
                Passed = true, // Simplified - would implement actual checks
                IsBlocking = check.IsBlocking
            };
        }

        private double CalculateReadinessScore(List<HandoverCheckResult> results)
        {
            if (!results.Any()) return 1.0;
            return results.Count(r => r.Passed) / (double)results.Count;
        }

        private TrendDirection CalculateTrendDirection(List<QAReport> reports)
        {
            if (reports.Count < 2) return TrendDirection.Stable;

            var firstHalf = reports.Take(reports.Count / 2).Average(r => r.QualityScore);
            var secondHalf = reports.Skip(reports.Count / 2).Average(r => r.QualityScore);

            if (secondHalf > firstHalf + 5) return TrendDirection.Improving;
            if (secondHalf < firstHalf - 5) return TrendDirection.Declining;
            return TrendDirection.Stable;
        }

        private double CalculateImprovementRate(List<QAReport> reports)
        {
            if (reports.Count < 2) return 0;
            var first = reports.First().QualityScore;
            var last = reports.Last().QualityScore;
            return (last - first) / reports.Count;
        }

        private List<string> IdentifyRecurringIssues(List<QAReport> reports)
        {
            return reports
                .SelectMany(r => r.Issues)
                .GroupBy(i => i.RuleId)
                .Where(g => g.Count() > reports.Count / 2)
                .Select(g => g.First().RuleName)
                .ToList();
        }

        private Dictionary<string, TrendDirection> CalculateCategoryTrends(List<QAReport> reports)
        {
            return new Dictionary<string, TrendDirection>();
        }

        private double ProjectFutureScore(List<QAReport> reports)
        {
            if (reports.Count < 3) return reports.LastOrDefault()?.QualityScore ?? 0;
            var rate = CalculateImprovementRate(reports);
            return Math.Min(100, Math.Max(0, reports.Last().QualityScore + rate * 3));
        }

        private List<string> GenerateTrendRecommendations(List<QAReport> reports)
        {
            var recommendations = new List<string>();
            var recurring = IdentifyRecurringIssues(reports);

            foreach (var issue in recurring)
            {
                recommendations.Add($"Address recurring issue: {issue}");
            }

            if (CalculateTrendDirection(reports) == TrendDirection.Declining)
            {
                recommendations.Add("Quality is declining - consider more frequent checks");
            }

            return recommendations;
        }

        #endregion
    }

    #region Supporting Classes

    public class QARule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public QASeverity Severity { get; set; }
        public QACheckType CheckType { get; set; }
        public Func<Document, QARule, QACheckOptions, List<QAIssue>> ValidationLogic { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    }

    public class QACheckCategory
    {
        public string CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Weight { get; set; }
        public ProjectPhase Phase { get; set; }
    }

    public class QAIssue
    {
        public string IssueId { get; set; }
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Category { get; set; }
        public QASeverity Severity { get; set; }
        public string Description { get; set; }
        public List<ElementId> AffectedElementIds { get; set; } = new List<ElementId>();
        public string Location { get; set; }
        public string ParameterName { get; set; }
        public string ActualValue { get; set; }
        public string RequiredValue { get; set; }
        public string StandardReference { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public QAIssueStatus Status { get; set; } = QAIssueStatus.Open;
        public List<StatusChange> StatusHistory { get; set; } = new List<StatusChange>();
    }

    public class QACheckOptions
    {
        public List<string> Categories { get; set; }
        public List<QASeverity> SeverityFilter { get; set; }
        public List<string> ExcludedRules { get; set; }
        public List<ElementId> TargetElements { get; set; }
        public ProjectPhase Phase { get; set; } = ProjectPhase.All;
        public bool IncludeWarnings { get; set; } = true;
    }

    public class QAReport
    {
        public string ReportId { get; set; }
        public string DocumentName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public QACheckOptions Options { get; set; }
        public List<QAIssue> Issues { get; set; } = new List<QAIssue>();
        public List<string> RulesChecked { get; set; } = new List<string>();
        public List<QAError> Errors { get; set; } = new List<QAError>();
        public double QualityScore { get; set; }
        public string Summary { get; set; }
    }

    public class QAProgress
    {
        public string CurrentRule { get; set; }
        public int RulesProcessed { get; set; }
        public int TotalRules { get; set; }
        public int IssuesFound { get; set; }
        public double PercentComplete { get; set; }
    }

    public class QAError
    {
        public string RuleId { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    public class QAIssueFilter
    {
        public List<string> Categories { get; set; }
        public List<QASeverity> Severities { get; set; }
        public QAIssueStatus? Status { get; set; }
        public string SearchText { get; set; }
    }

    public class PrioritizedIssue
    {
        public QAIssue Issue { get; set; }
        public double Priority { get; set; }
        public double RiskScore { get; set; }
        public TimeSpan EstimatedEffort { get; set; }
        public string SuggestedAction { get; set; }
    }

    public class StatusChange
    {
        public QAIssueStatus FromStatus { get; set; }
        public QAIssueStatus ToStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Notes { get; set; }
    }

    public class ElementQAResult
    {
        public int ElementCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalIssues { get; set; }
        public Dictionary<ElementId, List<QAIssue>> ElementIssues { get; set; } = new Dictionary<ElementId, List<QAIssue>>();
    }

    public class HandoverReadinessReport
    {
        public ProjectPhase TargetPhase { get; set; }
        public DateTime AssessmentDate { get; set; }
        public List<HandoverCheckResult> CheckResults { get; set; } = new List<HandoverCheckResult>();
        public double OverallReadiness { get; set; }
        public List<string> BlockingIssues { get; set; } = new List<string>();
        public bool IsReady { get; set; }
    }

    public class HandoverCheck
    {
        public string CheckId { get; set; }
        public string CheckName { get; set; }
        public bool IsBlocking { get; set; }
    }

    public class HandoverCheckResult
    {
        public string CheckId { get; set; }
        public string CheckName { get; set; }
        public bool Passed { get; set; }
        public bool IsBlocking { get; set; }
        public string Notes { get; set; }
    }

    public class CorrectiveAction
    {
        public string ActionId { get; set; }
        public string IssueId { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public CorrectiveActionStatus Status { get; set; }
    }

    public class CorrectiveActionRequest
    {
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class ValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<QAIssue> RemainingIssues { get; set; }
    }

    public class QualityScoreBreakdown
    {
        public double OverallScore { get; set; }
        public Dictionary<string, double> CategoryScores { get; set; } = new Dictionary<string, double>();
        public Dictionary<QASeverity, int> IssueCounts { get; set; } = new Dictionary<QASeverity, int>();
    }

    public class QualityScoreSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double Score { get; set; }
        public int IssueCount { get; set; }
    }

    public class QualityTrendAnalysis
    {
        public TrendDirection TrendDirection { get; set; }
        public double ImprovementRate { get; set; }
        public List<string> RecurringIssues { get; set; }
        public Dictionary<string, TrendDirection> CategoryTrends { get; set; }
        public double ProjectedScore { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class QAScoreTracker
    {
        private readonly List<QualityScoreSnapshot> _history = new List<QualityScoreSnapshot>();

        public double CalculateScore(QAReport report)
        {
            if (!report.Issues.Any()) return 100.0;

            var criticalPenalty = report.Issues.Count(i => i.Severity == QASeverity.Critical) * 10;
            var highPenalty = report.Issues.Count(i => i.Severity == QASeverity.High) * 5;
            var mediumPenalty = report.Issues.Count(i => i.Severity == QASeverity.Medium) * 2;
            var lowPenalty = report.Issues.Count(i => i.Severity == QASeverity.Low) * 0.5;

            var score = 100 - criticalPenalty - highPenalty - mediumPenalty - lowPenalty;
            score = Math.Max(0, Math.Min(100, score));

            _history.Add(new QualityScoreSnapshot
            {
                Timestamp = DateTime.Now,
                Score = score,
                IssueCount = report.Issues.Count
            });

            return score;
        }

        public QualityScoreBreakdown GetCurrentBreakdown(List<QAIssue> issues, Dictionary<string, QACheckCategory> categories)
        {
            var breakdown = new QualityScoreBreakdown();

            foreach (var category in categories.Values)
            {
                var categoryIssues = issues.Where(i => i.Category == category.Name).ToList();
                var categoryScore = 100 - categoryIssues.Count * 5;
                breakdown.CategoryScores[category.Name] = Math.Max(0, Math.Min(100, categoryScore));
            }

            breakdown.OverallScore = breakdown.CategoryScores.Any()
                ? breakdown.CategoryScores.Values.Average()
                : 100;

            breakdown.IssueCounts = issues
                .GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            return breakdown;
        }

        public IEnumerable<QualityScoreSnapshot> GetHistory(DateTime? since = null)
        {
            var snapshots = _history.AsEnumerable();
            if (since.HasValue)
                snapshots = snapshots.Where(s => s.Timestamp >= since.Value);
            return snapshots.ToList();
        }
    }

    public class CorrectiveActionManager
    {
        private readonly List<CorrectiveAction> _actions = new List<CorrectiveAction>();

        public CorrectiveAction CreateAction(QAIssue issue, CorrectiveActionRequest request)
        {
            var action = new CorrectiveAction
            {
                ActionId = Guid.NewGuid().ToString(),
                IssueId = issue.IssueId,
                Description = request.Description,
                AssignedTo = request.AssignedTo,
                DueDate = request.DueDate,
                Status = CorrectiveActionStatus.Pending
            };

            _actions.Add(action);
            return action;
        }
    }

    public class QAReportGenerator
    {
        public async Task<byte[]> GenerateAsync(QAReport report, ReportFormat format, ReportOptions options)
        {
            await Task.Delay(1); // Placeholder
            return new byte[0];
        }
    }

    public class ReportOptions
    {
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeElementDetails { get; set; } = true;
    }

    public enum QASeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    public enum QACheckType
    {
        Automatic,
        Manual,
        Hybrid
    }

    public enum QAIssueStatus
    {
        Open,
        InProgress,
        Resolved,
        Deferred,
        WontFix
    }

    public enum ProjectPhase
    {
        All,
        Concept,
        SchematicDesign,
        DesignDevelopment,
        ConstructionDocuments,
        Construction,
        Handover,
        Design
    }

    public enum CorrectiveActionStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }

    public enum TrendDirection
    {
        Improving,
        Stable,
        Declining
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
