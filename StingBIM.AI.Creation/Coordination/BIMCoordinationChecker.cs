using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Coordination
{
    /// <summary>
    /// BIM coordination checker for detecting clashes, validating spatial relationships,
    /// and ensuring element coordination across disciplines.
    /// </summary>
    public class BIMCoordinationChecker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, ClashRule> _clashRules;
        private readonly Dictionary<string, ClearanceRequirement> _clearanceRequirements;
        private readonly List<CoordinationIssue> _issueHistory;
        private readonly object _lock = new object();

        public BIMCoordinationChecker()
        {
            _clashRules = InitializeClashRules();
            _clearanceRequirements = InitializeClearanceRequirements();
            _issueHistory = new List<CoordinationIssue>();

            Logger.Info("BIMCoordinationChecker initialized with {0} clash rules, {1} clearance requirements",
                _clashRules.Count, _clearanceRequirements.Count);
        }

        #region Rules and Requirements

        private Dictionary<string, ClashRule> InitializeClashRules()
        {
            return new Dictionary<string, ClashRule>(StringComparer.OrdinalIgnoreCase)
            {
                // Hard Clashes (physical intersections)
                ["Structure_MEP"] = new ClashRule
                {
                    RuleId = "CLH001",
                    Name = "Structure vs MEP Hard Clash",
                    Category1 = ElementCategory.Structure,
                    Category2 = ElementCategory.MEP,
                    ClashType = ClashType.Hard,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "Structural elements intersecting with MEP systems",
                    Resolution = "Relocate MEP routing or provide structural penetration with sleeve"
                },
                ["Structure_Architecture"] = new ClashRule
                {
                    RuleId = "CLH002",
                    Name = "Structure vs Architecture Hard Clash",
                    Category1 = ElementCategory.Structure,
                    Category2 = ElementCategory.Architecture,
                    ClashType = ClashType.Hard,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "Structural elements clashing with architectural elements",
                    Resolution = "Coordinate column/beam locations with architectural layout"
                },
                ["MEP_MEP"] = new ClashRule
                {
                    RuleId = "CLH003",
                    Name = "MEP vs MEP Hard Clash",
                    Category1 = ElementCategory.MEP,
                    Category2 = ElementCategory.MEP,
                    ClashType = ClashType.Hard,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "MEP systems intersecting with each other",
                    Resolution = "Reroute conflicting services, check crossing priorities"
                },
                ["Duct_Pipe"] = new ClashRule
                {
                    RuleId = "CLH004",
                    Name = "Ductwork vs Piping Hard Clash",
                    Category1 = ElementCategory.HVAC,
                    Category2 = ElementCategory.Plumbing,
                    ClashType = ClashType.Hard,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "HVAC ductwork intersecting with plumbing",
                    Resolution = "Adjust vertical offsets, piping typically yields to ductwork"
                },
                ["Electrical_Plumbing"] = new ClashRule
                {
                    RuleId = "CLH005",
                    Name = "Electrical vs Plumbing Hard Clash",
                    Category1 = ElementCategory.Electrical,
                    Category2 = ElementCategory.Plumbing,
                    ClashType = ClashType.Hard,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "Electrical routing intersecting with plumbing",
                    Resolution = "Maintain electrical above plumbing, ensure proper separation"
                },

                // Soft/Clearance Clashes
                ["Duct_Clearance"] = new ClashRule
                {
                    RuleId = "CLS001",
                    Name = "Ductwork Maintenance Clearance",
                    Category1 = ElementCategory.HVAC,
                    Category2 = ElementCategory.Structure,
                    ClashType = ClashType.Soft,
                    Severity = ClashSeverity.Major,
                    Tolerance = 150, // 150mm clearance required
                    Description = "Insufficient clearance around ductwork for maintenance access",
                    Resolution = "Provide minimum 150mm clearance for access panels and maintenance"
                },
                ["Pipe_Clearance"] = new ClashRule
                {
                    RuleId = "CLS002",
                    Name = "Piping Insulation Clearance",
                    Category1 = ElementCategory.Plumbing,
                    Category2 = ElementCategory.Structure,
                    ClashType = ClashType.Soft,
                    Severity = ClashSeverity.Major,
                    Tolerance = 50, // 50mm clearance for insulation
                    Description = "Insufficient clearance for pipe insulation",
                    Resolution = "Allow clearance for insulation thickness plus 25mm"
                },
                ["Cable_Tray_Clearance"] = new ClashRule
                {
                    RuleId = "CLS003",
                    Name = "Cable Tray Access Clearance",
                    Category1 = ElementCategory.Electrical,
                    Category2 = ElementCategory.Structure,
                    ClashType = ClashType.Soft,
                    Severity = ClashSeverity.Major,
                    Tolerance = 200, // 200mm clearance for cable pulling
                    Description = "Insufficient clearance above cable trays for cable installation",
                    Resolution = "Provide minimum 200mm vertical clearance above cable trays"
                },
                ["Door_Swing"] = new ClashRule
                {
                    RuleId = "CLS004",
                    Name = "Door Swing Clearance",
                    Category1 = ElementCategory.Door,
                    Category2 = ElementCategory.Furniture,
                    ClashType = ClashType.Soft,
                    Severity = ClashSeverity.Minor,
                    Tolerance = 0,
                    Description = "Door swing obstructed by furniture or equipment",
                    Resolution = "Relocate furniture or consider alternative door swing direction"
                },
                ["Ceiling_Services"] = new ClashRule
                {
                    RuleId = "CLS005",
                    Name = "Ceiling vs Services Zone",
                    Category1 = ElementCategory.Ceiling,
                    Category2 = ElementCategory.MEP,
                    ClashType = ClashType.Soft,
                    Severity = ClashSeverity.Major,
                    Tolerance = 50,
                    Description = "Services penetrating ceiling zone without coordination",
                    Resolution = "Coordinate ceiling grid with service locations, provide access panels"
                },

                // Workflow Clashes
                ["Fire_Rating"] = new ClashRule
                {
                    RuleId = "CLW001",
                    Name = "Fire Compartment Penetration",
                    Category1 = ElementCategory.FireProtection,
                    Category2 = ElementCategory.MEP,
                    ClashType = ClashType.Workflow,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "MEP penetrating fire-rated assembly without protection",
                    Resolution = "Provide fire-rated collar, damper, or sealant per fire engineer specifications"
                },
                ["Accessibility_Route"] = new ClashRule
                {
                    RuleId = "CLW002",
                    Name = "Accessible Route Obstruction",
                    Category1 = ElementCategory.Accessibility,
                    Category2 = ElementCategory.Architecture,
                    ClashType = ClashType.Workflow,
                    Severity = ClashSeverity.Critical,
                    Tolerance = 0,
                    Description = "Accessible route obstructed or dimensions not met",
                    Resolution = "Maintain minimum clearances per accessibility requirements"
                }
            };
        }

        private Dictionary<string, ClearanceRequirement> InitializeClearanceRequirements()
        {
            return new Dictionary<string, ClearanceRequirement>(StringComparer.OrdinalIgnoreCase)
            {
                // MEP Service Clearances
                ["Duct_Vertical"] = new ClearanceRequirement
                {
                    RequirementId = "CLR001",
                    ElementType = "Ductwork",
                    Direction = ClearanceDirection.Vertical,
                    MinimumClearance = 150,
                    PreferredClearance = 300,
                    Unit = "mm",
                    Reason = "Maintenance access and air sealing"
                },
                ["Duct_Horizontal"] = new ClearanceRequirement
                {
                    RequirementId = "CLR002",
                    ElementType = "Ductwork",
                    Direction = ClearanceDirection.Horizontal,
                    MinimumClearance = 25,
                    PreferredClearance = 50,
                    Unit = "mm",
                    Reason = "Insulation and thermal expansion"
                },
                ["Pipe_Vertical"] = new ClearanceRequirement
                {
                    RequirementId = "CLR003",
                    ElementType = "Piping",
                    Direction = ClearanceDirection.Vertical,
                    MinimumClearance = 50,
                    PreferredClearance = 100,
                    Unit = "mm",
                    Reason = "Insulation and condensation prevention"
                },
                ["Pipe_Horizontal"] = new ClearanceRequirement
                {
                    RequirementId = "CLR004",
                    ElementType = "Piping",
                    Direction = ClearanceDirection.Horizontal,
                    MinimumClearance = 25,
                    PreferredClearance = 75,
                    Unit = "mm",
                    Reason = "Insulation thickness and valve access"
                },
                ["Cable_Tray"] = new ClearanceRequirement
                {
                    RequirementId = "CLR005",
                    ElementType = "Cable Tray",
                    Direction = ClearanceDirection.Vertical,
                    MinimumClearance = 200,
                    PreferredClearance = 300,
                    Unit = "mm",
                    Reason = "Cable pulling and future additions"
                },
                ["Electrical_Panel"] = new ClearanceRequirement
                {
                    RequirementId = "CLR006",
                    ElementType = "Electrical Panel",
                    Direction = ClearanceDirection.Frontal,
                    MinimumClearance = 900,
                    PreferredClearance = 1200,
                    Unit = "mm",
                    Reason = "NEC working clearance requirements"
                },
                ["AHU_Access"] = new ClearanceRequirement
                {
                    RequirementId = "CLR007",
                    ElementType = "Air Handling Unit",
                    Direction = ClearanceDirection.All,
                    MinimumClearance = 600,
                    PreferredClearance = 900,
                    Unit = "mm",
                    Reason = "Filter access and maintenance"
                },

                // Accessibility Clearances
                ["Wheelchair_Corridor"] = new ClearanceRequirement
                {
                    RequirementId = "CLR010",
                    ElementType = "Wheelchair Accessible Route",
                    Direction = ClearanceDirection.Horizontal,
                    MinimumClearance = 915,
                    PreferredClearance = 1525,
                    Unit = "mm",
                    Reason = "ADA accessible route requirements"
                },
                ["Door_Maneuvering"] = new ClearanceRequirement
                {
                    RequirementId = "CLR011",
                    ElementType = "Door Approach",
                    Direction = ClearanceDirection.Frontal,
                    MinimumClearance = 1525,
                    PreferredClearance = 1800,
                    Unit = "mm",
                    Reason = "Wheelchair maneuvering space"
                },

                // Structural Clearances
                ["Column_Wall"] = new ClearanceRequirement
                {
                    RequirementId = "CLR020",
                    ElementType = "Column to Wall",
                    Direction = ClearanceDirection.Horizontal,
                    MinimumClearance = 0, // Columns can be embedded
                    PreferredClearance = 50,
                    Unit = "mm",
                    Reason = "Construction tolerance and finish coordination"
                },
                ["Beam_Ceiling"] = new ClearanceRequirement
                {
                    RequirementId = "CLR021",
                    ElementType = "Beam to Ceiling",
                    Direction = ClearanceDirection.Vertical,
                    MinimumClearance = 200,
                    PreferredClearance = 400,
                    Unit = "mm",
                    Reason = "Service zone and fire protection"
                }
            };
        }

        #endregion

        #region Clash Detection

        /// <summary>
        /// Performs comprehensive clash detection on building model elements.
        /// </summary>
        public async Task<ClashDetectionResult> DetectClashesAsync(BuildingModelInput model)
        {
            Logger.Info("Starting clash detection for model: {0}", model.ModelName);

            var result = new ClashDetectionResult
            {
                ModelName = model.ModelName,
                DetectionDate = DateTime.UtcNow,
                Clashes = new List<CoordinationClash>(),
                Statistics = new CoordinationClashStatistics()
            };

            await Task.Run(() =>
            {
                // Group elements by category for efficient comparison
                var elementsByCategory = model.Elements.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Check each clash rule
                foreach (var rule in _clashRules.Values)
                {
                    var clashes = CheckClashRule(rule, elementsByCategory);
                    result.Clashes.AddRange(clashes);
                }

                // Calculate statistics
                result.Statistics.TotalClashesDetected = result.Clashes.Count;
                result.Statistics.CriticalClashes = result.Clashes.Count(c => c.Severity == ClashSeverity.Critical);
                result.Statistics.MajorClashes = result.Clashes.Count(c => c.Severity == ClashSeverity.Major);
                result.Statistics.MinorClashes = result.Clashes.Count(c => c.Severity == ClashSeverity.Minor);
                result.Statistics.HardClashes = result.Clashes.Count(c => c.ClashType == ClashType.Hard);
                result.Statistics.SoftClashes = result.Clashes.Count(c => c.ClashType == ClashType.Soft);
                result.Statistics.WorkflowClashes = result.Clashes.Count(c => c.ClashType == ClashType.Workflow);

                // Group by discipline
                result.Statistics.ClashesByDiscipline = result.Clashes
                    .GroupBy(c => $"{c.Element1.Discipline}-{c.Element2.Discipline}")
                    .ToDictionary(g => g.Key, g => g.Count());
            });

            // Store history
            lock (_lock)
            {
                _issueHistory.AddRange(result.Clashes.Select(c => new CoordinationIssue
                {
                    IssueId = c.ClashId,
                    Type = CoordinationIssueType.Clash,
                    Severity = c.Severity.ToString(),
                    Description = c.Description,
                    DetectedDate = DateTime.UtcNow
                }));
            }

            Logger.Info("Clash detection complete: {0} clashes ({1} critical, {2} major, {3} minor)",
                result.Statistics.TotalClashesDetected,
                result.Statistics.CriticalClashes,
                result.Statistics.MajorClashes,
                result.Statistics.MinorClashes);

            return result;
        }

        private List<CoordinationClash> CheckClashRule(ClashRule rule, Dictionary<ElementCategory, List<ModelElement>> elementsByCategory)
        {
            var clashes = new List<CoordinationClash>();

            if (!elementsByCategory.TryGetValue(rule.Category1, out var elements1))
                return clashes;

            if (!elementsByCategory.TryGetValue(rule.Category2, out var elements2))
                return clashes;

            // Pairwise comparison
            foreach (var element1 in elements1)
            {
                foreach (var element2 in elements2)
                {
                    // Skip self-comparison for same-category rules
                    if (rule.Category1 == rule.Category2 && element1.ElementId == element2.ElementId)
                        continue;

                    // Check for geometric clash
                    var clashResult = CheckGeometricClash(element1, element2, rule.Tolerance);

                    if (clashResult.HasClash)
                    {
                        clashes.Add(new CoordinationClash
                        {
                            ClashId = $"CLH_{Guid.NewGuid():N}".Substring(0, 15),
                            RuleId = rule.RuleId,
                            RuleName = rule.Name,
                            ClashType = rule.ClashType,
                            Severity = rule.Severity,
                            Element1 = element1,
                            Element2 = element2,
                            ClashPoint = clashResult.IntersectionPoint,
                            PenetrationDistance = clashResult.PenetrationDistance,
                            Description = $"{element1.Name} ({element1.ElementId}) clashes with {element2.Name} ({element2.ElementId})",
                            SuggestedResolution = rule.Resolution,
                            Status = ClashStatus.New
                        });
                    }
                }
            }

            return clashes;
        }

        private (bool HasClash, Point3D IntersectionPoint, double PenetrationDistance) CheckGeometricClash(
            ModelElement element1, ModelElement element2, double tolerance)
        {
            // Simplified AABB (Axis-Aligned Bounding Box) clash detection
            var box1 = element1.BoundingBox;
            var box2 = element2.BoundingBox;

            if (box1 == null || box2 == null)
                return (false, null, 0);

            // Check for overlap with tolerance
            bool overlapsX = (box1.Max.X + tolerance >= box2.Min.X - tolerance) &&
                            (box2.Max.X + tolerance >= box1.Min.X - tolerance);
            bool overlapsY = (box1.Max.Y + tolerance >= box2.Min.Y - tolerance) &&
                            (box2.Max.Y + tolerance >= box1.Min.Y - tolerance);
            bool overlapsZ = (box1.Max.Z + tolerance >= box2.Min.Z - tolerance) &&
                            (box2.Max.Z + tolerance >= box1.Min.Z - tolerance);

            if (overlapsX && overlapsY && overlapsZ)
            {
                // Calculate intersection point (centroid of overlap region)
                var intersectionPoint = new Point3D
                {
                    X = (Math.Max(box1.Min.X, box2.Min.X) + Math.Min(box1.Max.X, box2.Max.X)) / 2,
                    Y = (Math.Max(box1.Min.Y, box2.Min.Y) + Math.Min(box1.Max.Y, box2.Max.Y)) / 2,
                    Z = (Math.Max(box1.Min.Z, box2.Min.Z) + Math.Min(box1.Max.Z, box2.Max.Z)) / 2
                };

                // Calculate penetration distance
                double overlapX = Math.Min(box1.Max.X, box2.Max.X) - Math.Max(box1.Min.X, box2.Min.X);
                double overlapY = Math.Min(box1.Max.Y, box2.Max.Y) - Math.Max(box1.Min.Y, box2.Min.Y);
                double overlapZ = Math.Min(box1.Max.Z, box2.Max.Z) - Math.Max(box1.Min.Z, box2.Min.Z);
                double penetration = Math.Min(Math.Min(overlapX, overlapY), overlapZ);

                return (true, intersectionPoint, Math.Max(0, penetration));
            }

            return (false, null, 0);
        }

        #endregion

        #region Clearance Validation

        /// <summary>
        /// Validates clearance requirements for model elements.
        /// </summary>
        public async Task<ClearanceValidationResult> ValidateClearancesAsync(BuildingModelInput model)
        {
            Logger.Info("Starting clearance validation for model: {0}", model.ModelName);

            var result = new ClearanceValidationResult
            {
                ModelName = model.ModelName,
                ValidationDate = DateTime.UtcNow,
                Violations = new List<ClearanceViolation>()
            };

            await Task.Run(() =>
            {
                foreach (var element in model.Elements)
                {
                    // Find applicable clearance requirements
                    var applicableRequirements = GetApplicableClearanceRequirements(element);

                    foreach (var requirement in applicableRequirements)
                    {
                        // Find nearby elements that might violate clearance
                        var nearbyElements = FindNearbyElements(element, model.Elements, requirement.MinimumClearance * 2);

                        foreach (var nearbyElement in nearbyElements)
                        {
                            var violation = CheckClearanceViolation(element, nearbyElement, requirement);
                            if (violation != null)
                            {
                                result.Violations.Add(violation);
                            }
                        }
                    }
                }
            });

            // Calculate summary
            result.TotalViolations = result.Violations.Count;
            result.CriticalViolations = result.Violations.Count(v => v.ActualClearance < 0); // Hard clash
            result.ViolationsByType = result.Violations.GroupBy(v => v.ElementType)
                .ToDictionary(g => g.Key, g => g.Count());

            Logger.Info("Clearance validation complete: {0} violations found", result.TotalViolations);

            return result;
        }

        private IEnumerable<ClearanceRequirement> GetApplicableClearanceRequirements(ModelElement element)
        {
            var requirements = new List<ClearanceRequirement>();

            // Match requirements based on element type/category
            foreach (var req in _clearanceRequirements.Values)
            {
                if (MatchesElementType(element, req.ElementType))
                {
                    requirements.Add(req);
                }
            }

            return requirements;
        }

        private bool MatchesElementType(ModelElement element, string elementType)
        {
            return element.Category switch
            {
                ElementCategory.HVAC when elementType.Contains("Duct") => true,
                ElementCategory.Plumbing when elementType.Contains("Pip") => true,
                ElementCategory.Electrical when elementType.Contains("Cable") || elementType.Contains("Electrical") => true,
                ElementCategory.Door when elementType.Contains("Door") => true,
                ElementCategory.Structure when elementType.Contains("Column") || elementType.Contains("Beam") => true,
                _ => false
            };
        }

        private IEnumerable<ModelElement> FindNearbyElements(ModelElement element, List<ModelElement> allElements, double searchRadius)
        {
            if (element.BoundingBox == null)
                return Enumerable.Empty<ModelElement>();

            var center = new Point3D
            {
                X = (element.BoundingBox.Min.X + element.BoundingBox.Max.X) / 2,
                Y = (element.BoundingBox.Min.Y + element.BoundingBox.Max.Y) / 2,
                Z = (element.BoundingBox.Min.Z + element.BoundingBox.Max.Z) / 2
            };

            return allElements.Where(e =>
            {
                if (e.ElementId == element.ElementId || e.BoundingBox == null)
                    return false;

                var otherCenter = new Point3D
                {
                    X = (e.BoundingBox.Min.X + e.BoundingBox.Max.X) / 2,
                    Y = (e.BoundingBox.Min.Y + e.BoundingBox.Max.Y) / 2,
                    Z = (e.BoundingBox.Min.Z + e.BoundingBox.Max.Z) / 2
                };

                double distance = Math.Sqrt(
                    Math.Pow(center.X - otherCenter.X, 2) +
                    Math.Pow(center.Y - otherCenter.Y, 2) +
                    Math.Pow(center.Z - otherCenter.Z, 2));

                return distance <= searchRadius;
            });
        }

        private ClearanceViolation CheckClearanceViolation(ModelElement element, ModelElement nearbyElement, ClearanceRequirement requirement)
        {
            if (element.BoundingBox == null || nearbyElement.BoundingBox == null)
                return null;

            double actualClearance = CalculateClearance(element.BoundingBox, nearbyElement.BoundingBox, requirement.Direction);

            if (actualClearance < requirement.MinimumClearance)
            {
                return new ClearanceViolation
                {
                    ViolationId = $"CLR_{Guid.NewGuid():N}".Substring(0, 15),
                    RequirementId = requirement.RequirementId,
                    ElementType = requirement.ElementType,
                    Element = element,
                    ObstructingElement = nearbyElement,
                    Direction = requirement.Direction,
                    RequiredClearance = requirement.MinimumClearance,
                    PreferredClearance = requirement.PreferredClearance,
                    ActualClearance = actualClearance,
                    Deficit = requirement.MinimumClearance - actualClearance,
                    Reason = requirement.Reason,
                    SuggestedFix = $"Increase clearance by at least {requirement.MinimumClearance - actualClearance:N0}{requirement.Unit}"
                };
            }

            return null;
        }

        private double CalculateClearance(BoundingBox box1, BoundingBox box2, ClearanceDirection direction)
        {
            switch (direction)
            {
                case ClearanceDirection.Horizontal:
                    double gapX = Math.Max(0, Math.Max(box2.Min.X - box1.Max.X, box1.Min.X - box2.Max.X));
                    double gapY = Math.Max(0, Math.Max(box2.Min.Y - box1.Max.Y, box1.Min.Y - box2.Max.Y));
                    return Math.Max(gapX, gapY);

                case ClearanceDirection.Vertical:
                    return Math.Max(0, Math.Max(box2.Min.Z - box1.Max.Z, box1.Min.Z - box2.Max.Z));

                case ClearanceDirection.Frontal:
                    return Math.Max(0, Math.Max(box2.Min.Y - box1.Max.Y, box1.Min.Y - box2.Max.Y));

                case ClearanceDirection.All:
                    double minGap = double.MaxValue;
                    minGap = Math.Min(minGap, Math.Max(0, box2.Min.X - box1.Max.X));
                    minGap = Math.Min(minGap, Math.Max(0, box1.Min.X - box2.Max.X));
                    minGap = Math.Min(minGap, Math.Max(0, box2.Min.Y - box1.Max.Y));
                    minGap = Math.Min(minGap, Math.Max(0, box1.Min.Y - box2.Max.Y));
                    minGap = Math.Min(minGap, Math.Max(0, box2.Min.Z - box1.Max.Z));
                    minGap = Math.Min(minGap, Math.Max(0, box1.Min.Z - box2.Max.Z));
                    return minGap;

                default:
                    return 0;
            }
        }

        #endregion

        #region Spatial Validation

        /// <summary>
        /// Validates spatial relationships and coordination requirements.
        /// </summary>
        public async Task<SpatialValidationResult> ValidateSpatialRelationshipsAsync(BuildingModelInput model)
        {
            Logger.Info("Starting spatial relationship validation for model: {0}", model.ModelName);

            var result = new SpatialValidationResult
            {
                ModelName = model.ModelName,
                ValidationDate = DateTime.UtcNow,
                Issues = new List<SpatialIssue>()
            };

            await Task.Run(() =>
            {
                // Check service zones
                result.Issues.AddRange(ValidateServiceZones(model));

                // Check ceiling heights
                result.Issues.AddRange(ValidateCeilingHeights(model));

                // Check floor-to-floor coordination
                result.Issues.AddRange(ValidateFloorCoordination(model));

                // Check MEP routing zones
                result.Issues.AddRange(ValidateMEPRoutingZones(model));

                // Check accessibility paths
                result.Issues.AddRange(ValidateAccessibilityPaths(model));
            });

            result.TotalIssues = result.Issues.Count;
            result.IssuesByCategory = result.Issues.GroupBy(i => i.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            Logger.Info("Spatial validation complete: {0} issues found", result.TotalIssues);

            return result;
        }

        private List<SpatialIssue> ValidateServiceZones(BuildingModelInput model)
        {
            var issues = new List<SpatialIssue>();

            // Find MEP elements in each floor
            var mepByFloor = model.Elements
                .Where(e => e.Category == ElementCategory.MEP || e.Category == ElementCategory.HVAC ||
                           e.Category == ElementCategory.Plumbing || e.Category == ElementCategory.Electrical)
                .GroupBy(e => e.Level);

            foreach (var floorGroup in mepByFloor)
            {
                // Check if services fit within defined ceiling void
                var maxServiceHeight = floorGroup.Max(e => e.BoundingBox?.Max.Z ?? 0);
                var minCeilingHeight = floorGroup.Min(e => e.BoundingBox?.Min.Z ?? double.MaxValue);

                if (model.CeilingVoidDepth > 0 && (maxServiceHeight - minCeilingHeight) > model.CeilingVoidDepth)
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "Service Zone",
                        Title = $"Services exceed ceiling void depth on Level {floorGroup.Key}",
                        Description = $"MEP services require {maxServiceHeight - minCeilingHeight:N0}mm but ceiling void is only {model.CeilingVoidDepth:N0}mm",
                        Severity = SpatialIssueSeverity.Major,
                        Location = $"Level {floorGroup.Key}",
                        SuggestedResolution = "Increase ceiling void depth or reorganize service routing"
                    });
                }
            }

            return issues;
        }

        private List<SpatialIssue> ValidateCeilingHeights(BuildingModelInput model)
        {
            var issues = new List<SpatialIssue>();

            foreach (var room in model.Rooms)
            {
                double effectiveCeilingHeight = room.FloorToFloorHeight - model.CeilingVoidDepth - model.FloorBuildupDepth;

                // Check minimum ceiling height (typically 2400-2700mm)
                if (effectiveCeilingHeight < 2400)
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "Ceiling Height",
                        Title = $"Insufficient ceiling height in {room.RoomName}",
                        Description = $"Effective ceiling height {effectiveCeilingHeight:N0}mm is below minimum 2400mm",
                        Severity = SpatialIssueSeverity.Critical,
                        Location = room.RoomName,
                        SuggestedResolution = "Reduce ceiling void depth, floor buildup, or increase floor-to-floor height"
                    });
                }
                else if (effectiveCeilingHeight < 2700 && room.RoomType == "Office")
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "Ceiling Height",
                        Title = $"Low ceiling height in {room.RoomName}",
                        Description = $"Office ceiling height {effectiveCeilingHeight:N0}mm is below recommended 2700mm",
                        Severity = SpatialIssueSeverity.Minor,
                        Location = room.RoomName,
                        SuggestedResolution = "Consider optimizing service routing to increase ceiling height"
                    });
                }
            }

            return issues;
        }

        private List<SpatialIssue> ValidateFloorCoordination(BuildingModelInput model)
        {
            var issues = new List<SpatialIssue>();

            // Check structural elements align across floors
            var columnsByLevel = model.Elements
                .Where(e => e.Type == "Column")
                .GroupBy(e => e.Level);

            var levelList = columnsByLevel.OrderBy(g => g.Key).ToList();

            for (int i = 0; i < levelList.Count - 1; i++)
            {
                var lowerColumns = levelList[i].ToList();
                var upperColumns = levelList[i + 1].ToList();

                foreach (var lowerCol in lowerColumns)
                {
                    var alignedUpper = upperColumns.FirstOrDefault(uc =>
                        Math.Abs((uc.BoundingBox?.Min.X ?? 0) - (lowerCol.BoundingBox?.Min.X ?? 0)) < 50 &&
                        Math.Abs((uc.BoundingBox?.Min.Y ?? 0) - (lowerCol.BoundingBox?.Min.Y ?? 0)) < 50);

                    if (alignedUpper == null)
                    {
                        issues.Add(new SpatialIssue
                        {
                            IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                            Category = "Structural Coordination",
                            Title = $"Column discontinuity at Level {levelList[i].Key}",
                            Description = $"Column {lowerCol.ElementId} does not have aligned column above",
                            Severity = SpatialIssueSeverity.Critical,
                            Location = $"Level {levelList[i].Key}",
                            SuggestedResolution = "Verify structural design intent or add transfer structure"
                        });
                    }
                }
            }

            return issues;
        }

        private List<SpatialIssue> ValidateMEPRoutingZones(BuildingModelInput model)
        {
            var issues = new List<SpatialIssue>();

            // Check main duct and pipe routing stays within designated zones
            var mainDucts = model.Elements.Where(e => e.Category == ElementCategory.HVAC && e.Width > 400);
            var mainPipes = model.Elements.Where(e => e.Category == ElementCategory.Plumbing && e.Diameter > 100);

            foreach (var duct in mainDucts)
            {
                // Check if duct routing respects column grid
                if (IntersectsColumnGrid(duct, model))
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "MEP Routing",
                        Title = $"Main duct crosses column line",
                        Description = $"Duct {duct.ElementId} routing crosses structural column grid",
                        Severity = SpatialIssueSeverity.Major,
                        Location = $"Level {duct.Level}",
                        SuggestedResolution = "Route main ducts parallel to column grid in designated service corridors"
                    });
                }
            }

            return issues;
        }

        private bool IntersectsColumnGrid(ModelElement element, BuildingModelInput model)
        {
            // Simplified check - in real implementation would check actual grid positions
            var columns = model.Elements.Where(e => e.Type == "Column" && e.Level == element.Level);

            foreach (var column in columns)
            {
                if (column.BoundingBox == null || element.BoundingBox == null)
                    continue;

                // Check if element crosses column position
                bool crossesX = element.BoundingBox.Min.X < column.BoundingBox.Max.X &&
                               element.BoundingBox.Max.X > column.BoundingBox.Min.X;
                bool crossesY = element.BoundingBox.Min.Y < column.BoundingBox.Max.Y &&
                               element.BoundingBox.Max.Y > column.BoundingBox.Min.Y;

                if (crossesX && crossesY)
                    return true;
            }

            return false;
        }

        private List<SpatialIssue> ValidateAccessibilityPaths(BuildingModelInput model)
        {
            var issues = new List<SpatialIssue>();

            // Check accessible route continuity
            var accessibleRoutes = model.Elements.Where(e => e.IsAccessibleRoute);

            foreach (var route in accessibleRoutes)
            {
                // Check width
                if (route.Width < 915) // ADA minimum
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "Accessibility",
                        Title = "Accessible route width insufficient",
                        Description = $"Route width {route.Width:N0}mm is below minimum 915mm",
                        Severity = SpatialIssueSeverity.Critical,
                        Location = $"Level {route.Level}",
                        SuggestedResolution = "Increase route width to meet accessibility requirements"
                    });
                }

                // Check for obstructions
                var obstructions = FindObstructions(route, model.Elements);
                foreach (var obstruction in obstructions)
                {
                    issues.Add(new SpatialIssue
                    {
                        IssueId = $"SPA_{Guid.NewGuid():N}".Substring(0, 15),
                        Category = "Accessibility",
                        Title = "Accessible route obstructed",
                        Description = $"Route obstructed by {obstruction.Type} ({obstruction.ElementId})",
                        Severity = SpatialIssueSeverity.Critical,
                        Location = $"Level {route.Level}",
                        SuggestedResolution = $"Relocate {obstruction.Type} or modify route"
                    });
                }
            }

            return issues;
        }

        private IEnumerable<ModelElement> FindObstructions(ModelElement route, List<ModelElement> allElements)
        {
            if (route.BoundingBox == null)
                return Enumerable.Empty<ModelElement>();

            return allElements.Where(e =>
            {
                if (e.ElementId == route.ElementId || e.BoundingBox == null)
                    return false;

                // Check if element is within route bounds
                bool overlapsX = route.BoundingBox.Max.X > e.BoundingBox.Min.X &&
                                route.BoundingBox.Min.X < e.BoundingBox.Max.X;
                bool overlapsY = route.BoundingBox.Max.Y > e.BoundingBox.Min.Y &&
                                route.BoundingBox.Min.Y < e.BoundingBox.Max.Y;

                // Element protrudes into route at head height (below 2100mm from floor)
                bool atHeadHeight = e.BoundingBox.Min.Z < 2100;

                return overlapsX && overlapsY && atHeadHeight;
            });
        }

        #endregion

        #region Coordination Report

        /// <summary>
        /// Generates a comprehensive coordination report.
        /// </summary>
        public async Task<CoordinationReport> GenerateCoordinationReportAsync(BuildingModelInput model)
        {
            Logger.Info("Generating comprehensive coordination report for: {0}", model.ModelName);

            // Run all checks in parallel
            var clashTask = DetectClashesAsync(model);
            var clearanceTask = ValidateClearancesAsync(model);
            var spatialTask = ValidateSpatialRelationshipsAsync(model);

            await Task.WhenAll(clashTask, clearanceTask, spatialTask);

            var report = new CoordinationReport
            {
                ModelName = model.ModelName,
                ReportDate = DateTime.UtcNow,
                ClashResults = clashTask.Result,
                ClearanceResults = clearanceTask.Result,
                SpatialResults = spatialTask.Result,
                OverallStatus = DetermineOverallStatus(clashTask.Result, clearanceTask.Result, spatialTask.Result),
                ExecutiveSummary = GenerateExecutiveSummary(clashTask.Result, clearanceTask.Result, spatialTask.Result),
                Recommendations = GenerateCoordinationRecommendations(clashTask.Result, clearanceTask.Result, spatialTask.Result)
            };

            Logger.Info("Coordination report generated: Status = {0}", report.OverallStatus);

            return report;
        }

        private CoordinationStatus DetermineOverallStatus(
            ClashDetectionResult clashes,
            ClearanceValidationResult clearances,
            SpatialValidationResult spatial)
        {
            int criticalCount = clashes.Statistics.CriticalClashes +
                               clearances.CriticalViolations +
                               spatial.Issues.Count(i => i.Severity == SpatialIssueSeverity.Critical);

            int majorCount = clashes.Statistics.MajorClashes +
                            clearances.Violations.Count(v => v.Deficit > 50) +
                            spatial.Issues.Count(i => i.Severity == SpatialIssueSeverity.Major);

            if (criticalCount > 0)
                return CoordinationStatus.Critical;
            if (majorCount > 5)
                return CoordinationStatus.NeedsAttention;
            if (majorCount > 0 || clashes.Statistics.MinorClashes > 10)
                return CoordinationStatus.MinorIssues;

            return CoordinationStatus.Coordinated;
        }

        private string GenerateExecutiveSummary(
            ClashDetectionResult clashes,
            ClearanceValidationResult clearances,
            SpatialValidationResult spatial)
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine($"## Coordination Analysis Summary");
            summary.AppendLine();
            summary.AppendLine($"**Total Issues Found:** {clashes.Statistics.TotalClashesDetected + clearances.TotalViolations + spatial.TotalIssues}");
            summary.AppendLine();
            summary.AppendLine("### Clash Detection");
            summary.AppendLine($"- Critical: {clashes.Statistics.CriticalClashes}");
            summary.AppendLine($"- Major: {clashes.Statistics.MajorClashes}");
            summary.AppendLine($"- Minor: {clashes.Statistics.MinorClashes}");
            summary.AppendLine();
            summary.AppendLine("### Clearance Violations");
            summary.AppendLine($"- Total: {clearances.TotalViolations}");
            summary.AppendLine($"- Critical (Hard Clash): {clearances.CriticalViolations}");
            summary.AppendLine();
            summary.AppendLine("### Spatial Issues");
            summary.AppendLine($"- Total: {spatial.TotalIssues}");
            foreach (var category in spatial.IssuesByCategory)
            {
                summary.AppendLine($"- {category.Key}: {category.Value}");
            }

            return summary.ToString();
        }

        private List<CoordinationRecommendation> GenerateCoordinationRecommendations(
            ClashDetectionResult clashes,
            ClearanceValidationResult clearances,
            SpatialValidationResult spatial)
        {
            var recommendations = new List<CoordinationRecommendation>();

            // Priority 1: Critical structural/MEP clashes
            if (clashes.Statistics.CriticalClashes > 0)
            {
                recommendations.Add(new CoordinationRecommendation
                {
                    Priority = 1,
                    Title = "Resolve Critical Clashes",
                    Description = $"Address {clashes.Statistics.CriticalClashes} critical clashes before construction",
                    ImpactedDisciplines = new List<string> { "Structure", "MEP" },
                    ActionItems = clashes.Clashes
                        .Where(c => c.Severity == ClashSeverity.Critical)
                        .Take(5)
                        .Select(c => c.SuggestedResolution)
                        .ToList()
                });
            }

            // Priority 2: Service zone coordination
            if (spatial.Issues.Any(i => i.Category == "Service Zone"))
            {
                recommendations.Add(new CoordinationRecommendation
                {
                    Priority = 2,
                    Title = "Coordinate Service Zones",
                    Description = "MEP services exceed allocated ceiling void in some areas",
                    ImpactedDisciplines = new List<string> { "MEP", "Architecture" },
                    ActionItems = new List<string>
                    {
                        "Review ceiling void depths with architect",
                        "Optimize MEP routing priorities",
                        "Consider raised floor for electrical distribution"
                    }
                });
            }

            // Priority 3: Accessibility compliance
            if (spatial.Issues.Any(i => i.Category == "Accessibility"))
            {
                recommendations.Add(new CoordinationRecommendation
                {
                    Priority = 3,
                    Title = "Ensure Accessibility Compliance",
                    Description = "Accessible routes have coordination issues",
                    ImpactedDisciplines = new List<string> { "Architecture", "MEP" },
                    ActionItems = spatial.Issues
                        .Where(i => i.Category == "Accessibility")
                        .Select(i => i.SuggestedResolution)
                        .ToList()
                });
            }

            return recommendations.OrderBy(r => r.Priority).ToList();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets available clash rules.
        /// </summary>
        public IEnumerable<ClashRule> GetClashRules()
        {
            return _clashRules.Values;
        }

        /// <summary>
        /// Gets clearance requirements.
        /// </summary>
        public IEnumerable<ClearanceRequirement> GetClearanceRequirements()
        {
            return _clearanceRequirements.Values;
        }

        /// <summary>
        /// Gets coordination issue history.
        /// </summary>
        public IEnumerable<CoordinationIssue> GetIssueHistory()
        {
            lock (_lock)
            {
                return _issueHistory.ToList();
            }
        }

        #endregion
    }

    #region Data Models

    public enum ElementCategory
    {
        Structure,
        Architecture,
        MEP,
        HVAC,
        Plumbing,
        Electrical,
        FireProtection,
        Door,
        Window,
        Ceiling,
        Furniture,
        Accessibility
    }

    public enum ClashType
    {
        Hard,
        Soft,
        Workflow
    }

    public enum ClashSeverity
    {
        Minor,
        Major,
        Critical
    }

    public enum ClashStatus
    {
        New,
        Active,
        Reviewed,
        Resolved,
        Approved
    }

    public enum ClearanceDirection
    {
        Horizontal,
        Vertical,
        Frontal,
        All
    }

    public enum SpatialIssueSeverity
    {
        Minor,
        Major,
        Critical
    }

    public enum CoordinationStatus
    {
        Coordinated,
        MinorIssues,
        NeedsAttention,
        Critical
    }

    public enum CoordinationIssueType
    {
        Clash,
        Clearance,
        Spatial
    }

    public class ClashRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public ElementCategory Category1 { get; set; }
        public ElementCategory Category2 { get; set; }
        public ClashType ClashType { get; set; }
        public ClashSeverity Severity { get; set; }
        public double Tolerance { get; set; }
        public string Description { get; set; }
        public string Resolution { get; set; }
    }

    public class ClearanceRequirement
    {
        public string RequirementId { get; set; }
        public string ElementType { get; set; }
        public ClearanceDirection Direction { get; set; }
        public double MinimumClearance { get; set; }
        public double PreferredClearance { get; set; }
        public string Unit { get; set; }
        public string Reason { get; set; }
    }

    public class BuildingModelInput
    {
        public string ModelName { get; set; }
        public List<ModelElement> Elements { get; set; } = new List<ModelElement>();
        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();
        public double CeilingVoidDepth { get; set; }
        public double FloorBuildupDepth { get; set; }
    }

    public class ModelElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public ElementCategory Category { get; set; }
        public string Discipline { get; set; }
        public int Level { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }
        public bool IsAccessibleRoute { get; set; }
    }

    public class BoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }
    }

    public class RoomInfo
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public int Level { get; set; }
        public double FloorToFloorHeight { get; set; }
    }

    public class ClashDetectionResult
    {
        public string ModelName { get; set; }
        public DateTime DetectionDate { get; set; }
        public List<CoordinationClash> Clashes { get; set; }
        public CoordinationClashStatistics Statistics { get; set; }
    }

    public class CoordinationClash
    {
        public string ClashId { get; set; }
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public ClashType ClashType { get; set; }
        public ClashSeverity Severity { get; set; }
        public ModelElement Element1 { get; set; }
        public ModelElement Element2 { get; set; }
        public Point3D ClashPoint { get; set; }
        public double PenetrationDistance { get; set; }
        public string Description { get; set; }
        public string SuggestedResolution { get; set; }
        public ClashStatus Status { get; set; }
    }

    public class CoordinationClashStatistics
    {
        public int TotalClashesDetected { get; set; }
        public int CriticalClashes { get; set; }
        public int MajorClashes { get; set; }
        public int MinorClashes { get; set; }
        public int HardClashes { get; set; }
        public int SoftClashes { get; set; }
        public int WorkflowClashes { get; set; }
        public Dictionary<string, int> ClashesByDiscipline { get; set; }
    }

    public class ClearanceValidationResult
    {
        public string ModelName { get; set; }
        public DateTime ValidationDate { get; set; }
        public List<ClearanceViolation> Violations { get; set; }
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public Dictionary<string, int> ViolationsByType { get; set; }
    }

    public class ClearanceViolation
    {
        public string ViolationId { get; set; }
        public string RequirementId { get; set; }
        public string ElementType { get; set; }
        public ModelElement Element { get; set; }
        public ModelElement ObstructingElement { get; set; }
        public ClearanceDirection Direction { get; set; }
        public double RequiredClearance { get; set; }
        public double PreferredClearance { get; set; }
        public double ActualClearance { get; set; }
        public double Deficit { get; set; }
        public string Reason { get; set; }
        public string SuggestedFix { get; set; }
    }

    public class SpatialValidationResult
    {
        public string ModelName { get; set; }
        public DateTime ValidationDate { get; set; }
        public List<SpatialIssue> Issues { get; set; }
        public int TotalIssues { get; set; }
        public Dictionary<string, int> IssuesByCategory { get; set; }
    }

    public class SpatialIssue
    {
        public string IssueId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public SpatialIssueSeverity Severity { get; set; }
        public string Location { get; set; }
        public string SuggestedResolution { get; set; }
    }

    public class CoordinationReport
    {
        public string ModelName { get; set; }
        public DateTime ReportDate { get; set; }
        public ClashDetectionResult ClashResults { get; set; }
        public ClearanceValidationResult ClearanceResults { get; set; }
        public SpatialValidationResult SpatialResults { get; set; }
        public CoordinationStatus OverallStatus { get; set; }
        public string ExecutiveSummary { get; set; }
        public List<CoordinationRecommendation> Recommendations { get; set; }
    }

    public class CoordinationRecommendation
    {
        public int Priority { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> ImpactedDisciplines { get; set; }
        public List<string> ActionItems { get; set; }
    }

    public class CoordinationIssue
    {
        public string IssueId { get; set; }
        public CoordinationIssueType Type { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
        public DateTime DetectedDate { get; set; }
    }

    #endregion
}
