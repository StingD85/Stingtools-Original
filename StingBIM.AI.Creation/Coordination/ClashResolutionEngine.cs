// ============================================================================
// StingBIM AI - Clash Resolution Engine
// Intelligent clash detection and auto-resolution system for BIM coordination
// Supports hard/soft/workflow clashes with machine learning-based resolution
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Coordination
{
    /// <summary>
    /// Intelligent engine for detecting, categorizing, prioritizing, and auto-resolving
    /// clashes in BIM models. Supports learning from user resolutions to improve
    /// auto-resolution suggestions over time.
    /// </summary>
    public class ClashResolutionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Resolution learning data
        private readonly Dictionary<string, ResolutionPattern> _learnedPatterns;
        private readonly List<ResolutionHistory> _resolutionHistory;
        private readonly Dictionary<string, ElementPriority> _elementPriorities;
        private readonly Dictionary<string, ToleranceSettings> _toleranceSettings;
        private readonly Dictionary<string, ResolutionRule> _resolutionRules;

        // Thread safety
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _resolutionSemaphore = new SemaphoreSlim(1, 1);

        // Configuration
        private ClashResolutionConfig _config;

        public ClashResolutionEngine()
        {
            _learnedPatterns = new Dictionary<string, ResolutionPattern>(StringComparer.OrdinalIgnoreCase);
            _resolutionHistory = new List<ResolutionHistory>();
            _elementPriorities = InitializeElementPriorities();
            _toleranceSettings = InitializeToleranceSettings();
            _resolutionRules = InitializeResolutionRules();
            _config = ClashResolutionConfig.Default;

            Logger.Info("ClashResolutionEngine initialized with {0} element priorities, {1} tolerance settings, {2} resolution rules",
                _elementPriorities.Count, _toleranceSettings.Count, _resolutionRules.Count);
        }

        #region Initialization

        private Dictionary<string, ElementPriority> InitializeElementPriorities()
        {
            return new Dictionary<string, ElementPriority>(StringComparer.OrdinalIgnoreCase)
            {
                // Structural elements - highest priority (never moved automatically)
                ["Column"] = new ElementPriority { ElementType = "Column", Discipline = ClashDiscipline.Structural, Priority = 1, CanAutoMove = false, CanAutoResize = false },
                ["Beam"] = new ElementPriority { ElementType = "Beam", Discipline = ClashDiscipline.Structural, Priority = 2, CanAutoMove = false, CanAutoResize = false },
                ["Slab"] = new ElementPriority { ElementType = "Slab", Discipline = ClashDiscipline.Structural, Priority = 3, CanAutoMove = false, CanAutoResize = false },
                ["Foundation"] = new ElementPriority { ElementType = "Foundation", Discipline = ClashDiscipline.Structural, Priority = 1, CanAutoMove = false, CanAutoResize = false },
                ["StructuralWall"] = new ElementPriority { ElementType = "StructuralWall", Discipline = ClashDiscipline.Structural, Priority = 2, CanAutoMove = false, CanAutoResize = false },

                // Architectural elements - high priority
                ["Wall"] = new ElementPriority { ElementType = "Wall", Discipline = ClashDiscipline.Architectural, Priority = 10, CanAutoMove = false, CanAutoResize = false },
                ["Floor"] = new ElementPriority { ElementType = "Floor", Discipline = ClashDiscipline.Architectural, Priority = 11, CanAutoMove = false, CanAutoResize = false },
                ["Ceiling"] = new ElementPriority { ElementType = "Ceiling", Discipline = ClashDiscipline.Architectural, Priority = 15, CanAutoMove = true, CanAutoResize = false },
                ["Door"] = new ElementPriority { ElementType = "Door", Discipline = ClashDiscipline.Architectural, Priority = 12, CanAutoMove = false, CanAutoResize = false },
                ["Window"] = new ElementPriority { ElementType = "Window", Discipline = ClashDiscipline.Architectural, Priority = 13, CanAutoMove = false, CanAutoResize = false },

                // MEP - HVAC (medium priority, large elements)
                ["Duct"] = new ElementPriority { ElementType = "Duct", Discipline = ClashDiscipline.HVAC, Priority = 20, CanAutoMove = true, CanAutoResize = true },
                ["DuctFitting"] = new ElementPriority { ElementType = "DuctFitting", Discipline = ClashDiscipline.HVAC, Priority = 21, CanAutoMove = true, CanAutoResize = false },
                ["AirTerminal"] = new ElementPriority { ElementType = "AirTerminal", Discipline = ClashDiscipline.HVAC, Priority = 22, CanAutoMove = true, CanAutoResize = false },
                ["MechanicalEquipment"] = new ElementPriority { ElementType = "MechanicalEquipment", Discipline = ClashDiscipline.HVAC, Priority = 18, CanAutoMove = false, CanAutoResize = false },

                // MEP - Plumbing (medium-high priority, gravity-dependent)
                ["Pipe"] = new ElementPriority { ElementType = "Pipe", Discipline = ClashDiscipline.Plumbing, Priority = 25, CanAutoMove = true, CanAutoResize = true },
                ["PipeFitting"] = new ElementPriority { ElementType = "PipeFitting", Discipline = ClashDiscipline.Plumbing, Priority = 26, CanAutoMove = true, CanAutoResize = false },
                ["PlumbingFixture"] = new ElementPriority { ElementType = "PlumbingFixture", Discipline = ClashDiscipline.Plumbing, Priority = 24, CanAutoMove = false, CanAutoResize = false },

                // MEP - Electrical (lower priority, most flexible)
                ["CableTray"] = new ElementPriority { ElementType = "CableTray", Discipline = ClashDiscipline.Electrical, Priority = 30, CanAutoMove = true, CanAutoResize = true },
                ["Conduit"] = new ElementPriority { ElementType = "Conduit", Discipline = ClashDiscipline.Electrical, Priority = 31, CanAutoMove = true, CanAutoResize = true },
                ["ElectricalPanel"] = new ElementPriority { ElementType = "ElectricalPanel", Discipline = ClashDiscipline.Electrical, Priority = 28, CanAutoMove = false, CanAutoResize = false },
                ["LightingFixture"] = new ElementPriority { ElementType = "LightingFixture", Discipline = ClashDiscipline.Electrical, Priority = 32, CanAutoMove = true, CanAutoResize = false },

                // Fire Protection
                ["SprinklerPipe"] = new ElementPriority { ElementType = "SprinklerPipe", Discipline = ClashDiscipline.FireProtection, Priority = 23, CanAutoMove = true, CanAutoResize = false },
                ["SprinklerHead"] = new ElementPriority { ElementType = "SprinklerHead", Discipline = ClashDiscipline.FireProtection, Priority = 19, CanAutoMove = false, CanAutoResize = false }
            };
        }

        private Dictionary<string, ToleranceSettings> InitializeToleranceSettings()
        {
            return new Dictionary<string, ToleranceSettings>(StringComparer.OrdinalIgnoreCase)
            {
                // Structural tolerances
                ["Column"] = new ToleranceSettings { ElementType = "Column", HardClashTolerance = 0, SoftClashTolerance = 50, InsulationAllowance = 0 },
                ["Beam"] = new ToleranceSettings { ElementType = "Beam", HardClashTolerance = 0, SoftClashTolerance = 50, InsulationAllowance = 0 },
                ["Slab"] = new ToleranceSettings { ElementType = "Slab", HardClashTolerance = 0, SoftClashTolerance = 25, InsulationAllowance = 0 },

                // HVAC tolerances
                ["Duct"] = new ToleranceSettings { ElementType = "Duct", HardClashTolerance = 0, SoftClashTolerance = 150, InsulationAllowance = 50 },
                ["DuctFitting"] = new ToleranceSettings { ElementType = "DuctFitting", HardClashTolerance = 0, SoftClashTolerance = 100, InsulationAllowance = 50 },

                // Plumbing tolerances
                ["Pipe"] = new ToleranceSettings { ElementType = "Pipe", HardClashTolerance = 0, SoftClashTolerance = 75, InsulationAllowance = 40 },
                ["PipeFitting"] = new ToleranceSettings { ElementType = "PipeFitting", HardClashTolerance = 0, SoftClashTolerance = 50, InsulationAllowance = 40 },

                // Electrical tolerances
                ["CableTray"] = new ToleranceSettings { ElementType = "CableTray", HardClashTolerance = 0, SoftClashTolerance = 200, InsulationAllowance = 0 },
                ["Conduit"] = new ToleranceSettings { ElementType = "Conduit", HardClashTolerance = 0, SoftClashTolerance = 50, InsulationAllowance = 0 },

                // Default
                ["Default"] = new ToleranceSettings { ElementType = "Default", HardClashTolerance = 0, SoftClashTolerance = 25, InsulationAllowance = 0 }
            };
        }

        private Dictionary<string, ResolutionRule> InitializeResolutionRules()
        {
            return new Dictionary<string, ResolutionRule>(StringComparer.OrdinalIgnoreCase)
            {
                // Duct vs Beam - route duct under/around beam
                ["Duct_Beam"] = new ResolutionRule
                {
                    RuleId = "RR001",
                    Element1Type = "Duct",
                    Element2Type = "Beam",
                    PreferredResolution = ResolutionStrategy.RouteUnder,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteAround, ResolutionStrategy.ReduceSize },
                    MovingElement = "Duct",
                    MaxVerticalOffset = 500,
                    MaxHorizontalOffset = 1000,
                    RequiresEngineerApproval = false
                },

                // Pipe vs Duct - smaller element yields
                ["Pipe_Duct"] = new ResolutionRule
                {
                    RuleId = "RR002",
                    Element1Type = "Pipe",
                    Element2Type = "Duct",
                    PreferredResolution = ResolutionStrategy.RouteUnder,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteAround },
                    MovingElement = "SmallerElement",
                    MaxVerticalOffset = 300,
                    MaxHorizontalOffset = 500,
                    RequiresEngineerApproval = false
                },

                // CableTray vs Duct
                ["CableTray_Duct"] = new ResolutionRule
                {
                    RuleId = "RR003",
                    Element1Type = "CableTray",
                    Element2Type = "Duct",
                    PreferredResolution = ResolutionStrategy.RouteUnder,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteAround, ResolutionStrategy.RouteAbove },
                    MovingElement = "CableTray",
                    MaxVerticalOffset = 400,
                    MaxHorizontalOffset = 600,
                    RequiresEngineerApproval = false
                },

                // Pipe vs Beam - requires sleeve or reroute
                ["Pipe_Beam"] = new ResolutionRule
                {
                    RuleId = "RR004",
                    Element1Type = "Pipe",
                    Element2Type = "Beam",
                    PreferredResolution = ResolutionStrategy.RouteUnder,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteAround, ResolutionStrategy.CreateSleeve },
                    MovingElement = "Pipe",
                    MaxVerticalOffset = 400,
                    MaxHorizontalOffset = 800,
                    RequiresEngineerApproval = true // Sleeves require structural approval
                },

                // Conduit vs Pipe
                ["Conduit_Pipe"] = new ResolutionRule
                {
                    RuleId = "RR005",
                    Element1Type = "Conduit",
                    Element2Type = "Pipe",
                    PreferredResolution = ResolutionStrategy.RouteAround,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteUnder, ResolutionStrategy.RouteAbove },
                    MovingElement = "Conduit",
                    MaxVerticalOffset = 200,
                    MaxHorizontalOffset = 300,
                    RequiresEngineerApproval = false
                },

                // SprinklerPipe vs Duct
                ["SprinklerPipe_Duct"] = new ResolutionRule
                {
                    RuleId = "RR006",
                    Element1Type = "SprinklerPipe",
                    Element2Type = "Duct",
                    PreferredResolution = ResolutionStrategy.RouteAbove,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteAround },
                    MovingElement = "Duct",
                    MaxVerticalOffset = 300,
                    MaxHorizontalOffset = 500,
                    RequiresEngineerApproval = false
                },

                // Generic MEP vs MEP
                ["MEP_MEP"] = new ResolutionRule
                {
                    RuleId = "RR007",
                    Element1Type = "MEP",
                    Element2Type = "MEP",
                    PreferredResolution = ResolutionStrategy.RouteAround,
                    AlternativeResolutions = new[] { ResolutionStrategy.RouteUnder, ResolutionStrategy.RouteAbove },
                    MovingElement = "LowerPriority",
                    MaxVerticalOffset = 300,
                    MaxHorizontalOffset = 500,
                    RequiresEngineerApproval = false
                }
            };
        }

        #endregion

        #region Clash Detection

        /// <summary>
        /// Performs comprehensive clash detection on the model elements.
        /// </summary>
        public async Task<ClashDetectionReport> DetectClashesAsync(
            ClashDetectionInput input,
            IProgress<ClashDetectionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting clash detection for model: {0} with {1} elements",
                input.ModelName, input.Elements.Count);

            var report = new ClashDetectionReport
            {
                ModelName = input.ModelName,
                DetectionStartTime = DateTime.UtcNow,
                ResolvableClashes = new List<ResolvableClash>()
            };

            await Task.Run(() =>
            {
                var elements = input.Elements;
                int totalComparisons = (elements.Count * (elements.Count - 1)) / 2;
                int currentComparison = 0;

                // Build spatial index for efficient detection
                var spatialIndex = BuildSpatialIndex(elements);

                // Detect clashes by category pairs
                for (int i = 0; i < elements.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var element1 = elements[i];
                    var candidates = GetCandidateElements(element1, spatialIndex, elements);

                    foreach (var element2 in candidates)
                    {
                        if (element1.ElementId.CompareTo(element2.ElementId) >= 0)
                            continue;

                        currentComparison++;

                        // Report progress periodically
                        if (currentComparison % 100 == 0)
                        {
                            progress?.Report(new ClashDetectionProgress
                            {
                                TotalComparisons = totalComparisons,
                                CompletedComparisons = currentComparison,
                                CurrentElement = element1.Name,
                                ClashesFound = report.ResolvableClashes.Count
                            });
                        }

                        // Check for clashes
                        var clash = DetectClashBetweenElements(element1, element2, input.Options);
                        if (clash != null)
                        {
                            clash.ClashId = GenerateClashId();
                            CategorizeClash(clash);
                            PrioritizeClash(clash);
                            report.ResolvableClashes.Add(clash);
                        }
                    }
                }

                // Calculate statistics
                CalculateClashStatistics(report);

            }, cancellationToken);

            report.DetectionEndTime = DateTime.UtcNow;

            Logger.Info("Clash detection complete: {0} total clashes ({1} hard, {2} soft, {3} workflow)",
                report.Statistics.TotalClashes,
                report.Statistics.HardClashes,
                report.Statistics.SoftClashes,
                report.Statistics.WorkflowClashes);

            return report;
        }

        private Dictionary<string, List<ClashElement>> BuildSpatialIndex(List<ClashElement> elements)
        {
            // Simple spatial hashing by grid cells
            var index = new Dictionary<string, List<ClashElement>>();
            double cellSize = 1000; // 1m grid cells

            foreach (var element in elements)
            {
                if (element.BoundingBox == null) continue;

                // Get all cells this element occupies
                int minCellX = (int)(element.BoundingBox.Min.X / cellSize);
                int maxCellX = (int)(element.BoundingBox.Max.X / cellSize);
                int minCellY = (int)(element.BoundingBox.Min.Y / cellSize);
                int maxCellY = (int)(element.BoundingBox.Max.Y / cellSize);
                int minCellZ = (int)(element.BoundingBox.Min.Z / cellSize);
                int maxCellZ = (int)(element.BoundingBox.Max.Z / cellSize);

                for (int x = minCellX; x <= maxCellX; x++)
                {
                    for (int y = minCellY; y <= maxCellY; y++)
                    {
                        for (int z = minCellZ; z <= maxCellZ; z++)
                        {
                            var key = $"{x}_{y}_{z}";
                            if (!index.ContainsKey(key))
                                index[key] = new List<ClashElement>();
                            index[key].Add(element);
                        }
                    }
                }
            }

            return index;
        }

        private List<ClashElement> GetCandidateElements(
            ClashElement element,
            Dictionary<string, List<ClashElement>> spatialIndex,
            List<ClashElement> allElements)
        {
            if (element.BoundingBox == null)
                return new List<ClashElement>();

            var candidates = new HashSet<ClashElement>();
            double cellSize = 1000;

            int minCellX = (int)(element.BoundingBox.Min.X / cellSize);
            int maxCellX = (int)(element.BoundingBox.Max.X / cellSize);
            int minCellY = (int)(element.BoundingBox.Min.Y / cellSize);
            int maxCellY = (int)(element.BoundingBox.Max.Y / cellSize);
            int minCellZ = (int)(element.BoundingBox.Min.Z / cellSize);
            int maxCellZ = (int)(element.BoundingBox.Max.Z / cellSize);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    for (int z = minCellZ; z <= maxCellZ; z++)
                    {
                        var key = $"{x}_{y}_{z}";
                        if (spatialIndex.TryGetValue(key, out var cellElements))
                        {
                            foreach (var e in cellElements)
                            {
                                if (e.ElementId != element.ElementId)
                                    candidates.Add(e);
                            }
                        }
                    }
                }
            }

            return candidates.ToList();
        }

        private ResolvableClash DetectClashBetweenElements(
            ClashElement element1,
            ClashElement element2,
            ClashDetectionOptions options)
        {
            if (element1.BoundingBox == null || element2.BoundingBox == null)
                return null;

            // Get tolerances
            var tol1 = GetToleranceForElement(element1.ElementType);
            var tol2 = GetToleranceForElement(element2.ElementType);

            // Check for hard clash (physical intersection)
            var hardClash = CheckHardClash(element1, element2);
            if (hardClash.HasIntersection)
            {
                return new ResolvableClash
                {
                    Element1 = element1,
                    Element2 = element2,
                    ClashType = ClashCategory.Hard,
                    IntersectionPoint = hardClash.IntersectionPoint,
                    PenetrationDistance = hardClash.PenetrationDistance,
                    ClashVolume = hardClash.IntersectionVolume,
                    Status = ClashResolutionStatus.New
                };
            }

            // Check for soft clash (clearance violation)
            if (options.DetectSoftClashes)
            {
                var softClash = CheckSoftClash(element1, element2, tol1, tol2);
                if (softClash.HasViolation)
                {
                    return new ResolvableClash
                    {
                        Element1 = element1,
                        Element2 = element2,
                        ClashType = ClashCategory.Soft,
                        IntersectionPoint = softClash.ClosestPoint,
                        PenetrationDistance = -softClash.ClearanceDeficit, // Negative for clearance
                        RequiredClearance = softClash.RequiredClearance,
                        ActualClearance = softClash.ActualClearance,
                        Status = ClashResolutionStatus.New
                    };
                }
            }

            // Check for workflow clash (sequencing issues)
            if (options.DetectWorkflowClashes)
            {
                var workflowClash = CheckWorkflowClash(element1, element2);
                if (workflowClash.HasIssue)
                {
                    return new ResolvableClash
                    {
                        Element1 = element1,
                        Element2 = element2,
                        ClashType = ClashCategory.Workflow,
                        WorkflowIssue = workflowClash.IssueDescription,
                        Status = ClashResolutionStatus.New
                    };
                }
            }

            return null;
        }

        private (bool HasIntersection, ClashPoint3D IntersectionPoint, double PenetrationDistance, double IntersectionVolume)
            CheckHardClash(ClashElement element1, ClashElement element2)
        {
            var box1 = element1.BoundingBox;
            var box2 = element2.BoundingBox;

            // AABB intersection test
            bool overlapsX = box1.Max.X >= box2.Min.X && box2.Max.X >= box1.Min.X;
            bool overlapsY = box1.Max.Y >= box2.Min.Y && box2.Max.Y >= box1.Min.Y;
            bool overlapsZ = box1.Max.Z >= box2.Min.Z && box2.Max.Z >= box1.Min.Z;

            if (overlapsX && overlapsY && overlapsZ)
            {
                // Calculate intersection details
                double overlapX = Math.Min(box1.Max.X, box2.Max.X) - Math.Max(box1.Min.X, box2.Min.X);
                double overlapY = Math.Min(box1.Max.Y, box2.Max.Y) - Math.Max(box1.Min.Y, box2.Min.Y);
                double overlapZ = Math.Min(box1.Max.Z, box2.Max.Z) - Math.Max(box1.Min.Z, box2.Min.Z);

                var intersectionPoint = new ClashPoint3D
                {
                    X = (Math.Max(box1.Min.X, box2.Min.X) + Math.Min(box1.Max.X, box2.Max.X)) / 2,
                    Y = (Math.Max(box1.Min.Y, box2.Min.Y) + Math.Min(box1.Max.Y, box2.Max.Y)) / 2,
                    Z = (Math.Max(box1.Min.Z, box2.Min.Z) + Math.Min(box1.Max.Z, box2.Max.Z)) / 2
                };

                double penetration = Math.Min(Math.Min(overlapX, overlapY), overlapZ);
                double volume = overlapX * overlapY * overlapZ;

                return (true, intersectionPoint, penetration, volume);
            }

            return (false, null, 0, 0);
        }

        private (bool HasViolation, ClashPoint3D ClosestPoint, double RequiredClearance, double ActualClearance, double ClearanceDeficit)
            CheckSoftClash(ClashElement element1, ClashElement element2, ToleranceSettings tol1, ToleranceSettings tol2)
        {
            var box1 = element1.BoundingBox;
            var box2 = element2.BoundingBox;

            // Calculate minimum distance between bounding boxes
            double distX = Math.Max(0, Math.Max(box1.Min.X - box2.Max.X, box2.Min.X - box1.Max.X));
            double distY = Math.Max(0, Math.Max(box1.Min.Y - box2.Max.Y, box2.Min.Y - box1.Max.Y));
            double distZ = Math.Max(0, Math.Max(box1.Min.Z - box2.Max.Z, box2.Min.Z - box1.Max.Z));
            double actualClearance = Math.Sqrt(distX * distX + distY * distY + distZ * distZ);

            // Required clearance is max of both elements' soft clash tolerances plus insulation
            double requiredClearance = Math.Max(
                tol1.SoftClashTolerance + tol1.InsulationAllowance,
                tol2.SoftClashTolerance + tol2.InsulationAllowance);

            if (actualClearance < requiredClearance)
            {
                var closestPoint = new ClashPoint3D
                {
                    X = (box1.Max.X + box2.Min.X) / 2,
                    Y = (box1.Max.Y + box2.Min.Y) / 2,
                    Z = (box1.Max.Z + box2.Min.Z) / 2
                };

                return (true, closestPoint, requiredClearance, actualClearance, requiredClearance - actualClearance);
            }

            return (false, null, requiredClearance, actualClearance, 0);
        }

        private (bool HasIssue, string IssueDescription) CheckWorkflowClash(ClashElement element1, ClashElement element2)
        {
            // Check fire rating penetrations
            if ((element1.IsFireRated || element2.IsFireRated) &&
                (element1.Discipline == ClashDiscipline.HVAC || element1.Discipline == ClashDiscipline.Plumbing ||
                 element2.Discipline == ClashDiscipline.HVAC || element2.Discipline == ClashDiscipline.Plumbing))
            {
                var mepElement = element1.Discipline == ClashDiscipline.HVAC || element1.Discipline == ClashDiscipline.Plumbing
                    ? element1 : element2;
                var fireElement = element1.IsFireRated ? element1 : element2;

                // Check if MEP passes through fire-rated element
                if (ElementsIntersectInPlan(element1, element2))
                {
                    if (!mepElement.HasFireDamper && !mepElement.HasFireSleeve)
                    {
                        return (true, $"{mepElement.ElementType} penetrates fire-rated {fireElement.ElementType} without fire protection");
                    }
                }
            }

            // Check construction sequence issues
            if (element1.ConstructionPhase != element2.ConstructionPhase)
            {
                if (ElementsAreDependent(element1, element2))
                {
                    if (element1.ConstructionPhase > element2.ConstructionPhase &&
                        GetElementPriority(element1.ElementType) < GetElementPriority(element2.ElementType))
                    {
                        return (true, $"{element1.ElementType} scheduled after {element2.ElementType} but has higher priority");
                    }
                }
            }

            return (false, null);
        }

        private bool ElementsIntersectInPlan(ClashElement element1, ClashElement element2)
        {
            if (element1.BoundingBox == null || element2.BoundingBox == null) return false;

            return element1.BoundingBox.Max.X >= element2.BoundingBox.Min.X &&
                   element2.BoundingBox.Max.X >= element1.BoundingBox.Min.X &&
                   element1.BoundingBox.Max.Y >= element2.BoundingBox.Min.Y &&
                   element2.BoundingBox.Max.Y >= element1.BoundingBox.Min.Y;
        }

        private bool ElementsAreDependent(ClashElement element1, ClashElement element2)
        {
            // Structural elements are dependencies for everything
            if (element1.Discipline == ClashDiscipline.Structural ||
                element2.Discipline == ClashDiscipline.Structural)
                return true;

            // Check if elements share the same space
            return ElementsIntersectInPlan(element1, element2);
        }

        private void CategorizeClash(ResolvableClash clash)
        {
            // Determine discipline combination
            var disc1 = clash.Element1.Discipline;
            var disc2 = clash.Element2.Discipline;

            if (disc1 == ClashDiscipline.Structural || disc2 == ClashDiscipline.Structural)
            {
                if (disc1 == ClashDiscipline.Architectural || disc2 == ClashDiscipline.Architectural)
                    clash.DisciplineCategory = "Arch-Struct";
                else
                    clash.DisciplineCategory = "MEP-Struct";
            }
            else if ((disc1 == ClashDiscipline.HVAC || disc1 == ClashDiscipline.Plumbing || disc1 == ClashDiscipline.Electrical) &&
                     (disc2 == ClashDiscipline.HVAC || disc2 == ClashDiscipline.Plumbing || disc2 == ClashDiscipline.Electrical))
            {
                clash.DisciplineCategory = "MEP-MEP";
            }
            else
            {
                clash.DisciplineCategory = $"{disc1}-{disc2}";
            }
        }

        private void PrioritizeClash(ResolvableClash clash)
        {
            int severityScore = 0;

            // Factor 1: Clash type
            switch (clash.ClashType)
            {
                case ClashCategory.Hard:
                    severityScore += 100;
                    break;
                case ClashCategory.Soft:
                    severityScore += 50;
                    break;
                case ClashCategory.Workflow:
                    severityScore += 75;
                    break;
            }

            // Factor 2: Elements involved
            var prio1 = GetElementPriority(clash.Element1.ElementType);
            var prio2 = GetElementPriority(clash.Element2.ElementType);
            severityScore += (20 - Math.Min(prio1, prio2)) * 5; // Higher priority elements = higher severity

            // Factor 3: Penetration depth for hard clashes
            if (clash.ClashType == ClashCategory.Hard && clash.PenetrationDistance > 0)
            {
                if (clash.PenetrationDistance > 100) severityScore += 50;
                else if (clash.PenetrationDistance > 50) severityScore += 25;
                else severityScore += 10;
            }

            // Factor 4: Fire-rated elements
            if (clash.Element1.IsFireRated || clash.Element2.IsFireRated)
                severityScore += 30;

            // Assign severity
            if (severityScore >= 150)
                clash.Severity = ClashSeverityLevel.Critical;
            else if (severityScore >= 100)
                clash.Severity = ClashSeverityLevel.Major;
            else if (severityScore >= 50)
                clash.Severity = ClashSeverityLevel.Minor;
            else
                clash.Severity = ClashSeverityLevel.Warning;

            clash.SeverityScore = severityScore;
        }

        private void CalculateClashStatistics(ClashDetectionReport report)
        {
            report.Statistics = new ClashStatistics
            {
                TotalClashes = report.ResolvableClashes.Count,
                HardClashes = report.ResolvableClashes.Count(c => c.ClashType == ClashCategory.Hard),
                SoftClashes = report.ResolvableClashes.Count(c => c.ClashType == ClashCategory.Soft),
                WorkflowClashes = report.ResolvableClashes.Count(c => c.ClashType == ClashCategory.Workflow),
                CriticalClashes = report.ResolvableClashes.Count(c => c.Severity == ClashSeverityLevel.Critical),
                MajorClashes = report.ResolvableClashes.Count(c => c.Severity == ClashSeverityLevel.Major),
                MinorClashes = report.ResolvableClashes.Count(c => c.Severity == ClashSeverityLevel.Minor),
                WarningClashes = report.ResolvableClashes.Count(c => c.Severity == ClashSeverityLevel.Warning),
                ByDisciplineCategory = report.ResolvableClashes
                    .GroupBy(c => c.DisciplineCategory)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByLevel = report.ResolvableClashes
                    .GroupBy(c => c.Element1.LevelName ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        #endregion

        #region Auto-Resolution

        /// <summary>
        /// Automatically resolves clashes where possible based on rules and learned patterns.
        /// </summary>
        public async Task<ClashResolutionReport> AutoResolveClashesAsync(
            List<ResolvableClash> clashes,
            AutoResolutionOptions options,
            IProgress<ResolutionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting auto-resolution for {0} clashes", clashes.Count);

            var report = new ClashResolutionReport
            {
                StartTime = DateTime.UtcNow,
                TotalClashes = clashes.Count,
                Resolutions = new List<ClashResolution>()
            };

            await _resolutionSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Sort clashes by severity (resolve critical first)
                var sortedClashes = clashes
                    .OrderByDescending(c => c.SeverityScore)
                    .ToList();

                int processed = 0;

                foreach (var clash in sortedClashes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processed++;
                    progress?.Report(new ResolutionProgress
                    {
                        TotalClashes = clashes.Count,
                        ProcessedClashes = processed,
                        ResolvedClashes = report.Resolutions.Count(r => r.Success),
                        CurrentClash = clash.ClashId
                    });

                    // Check if auto-resolution is allowed for this clash
                    if (!CanAutoResolve(clash, options))
                    {
                        report.Resolutions.Add(new ClashResolution
                        {
                            ClashId = clash.ClashId,
                            Success = false,
                            Reason = "Auto-resolution not permitted for this clash type",
                            RequiresManualReview = true
                        });
                        continue;
                    }

                    // Attempt resolution
                    var resolution = await AttemptResolutionAsync(clash, options, cancellationToken);
                    report.Resolutions.Add(resolution);

                    // Learn from resolution
                    if (resolution.Success && options.EnableLearning)
                    {
                        LearnFromResolution(clash, resolution);
                    }
                }

                // Calculate statistics
                report.EndTime = DateTime.UtcNow;
                report.ResolvedCount = report.Resolutions.Count(r => r.Success);
                report.FailedCount = report.Resolutions.Count(r => !r.Success);
                report.ManualReviewCount = report.Resolutions.Count(r => r.RequiresManualReview);
                report.SuccessRate = report.TotalClashes > 0
                    ? (double)report.ResolvedCount / report.TotalClashes * 100
                    : 0;
            }
            finally
            {
                _resolutionSemaphore.Release();
            }

            Logger.Info("Auto-resolution complete: {0}/{1} resolved ({2:F1}% success rate)",
                report.ResolvedCount, report.TotalClashes, report.SuccessRate);

            return report;
        }

        private bool CanAutoResolve(ResolvableClash clash, AutoResolutionOptions options)
        {
            // Don't auto-resolve critical structural clashes
            if (clash.Severity == ClashSeverityLevel.Critical &&
                (clash.Element1.Discipline == ClashDiscipline.Structural ||
                 clash.Element2.Discipline == ClashDiscipline.Structural))
            {
                return false;
            }

            // Check penetration limits
            if (clash.PenetrationDistance > options.MaxAutoResolvePenetration)
            {
                return false;
            }

            // Check if both elements can be moved/resized
            var prio1 = _elementPriorities.GetValueOrDefault(clash.Element1.ElementType);
            var prio2 = _elementPriorities.GetValueOrDefault(clash.Element2.ElementType);

            if ((prio1?.CanAutoMove ?? false) || (prio2?.CanAutoMove ?? false))
            {
                return true;
            }

            return false;
        }

        private async Task<ClashResolution> AttemptResolutionAsync(
            ResolvableClash clash,
            AutoResolutionOptions options,
            CancellationToken cancellationToken)
        {
            var resolution = new ClashResolution
            {
                ClashId = clash.ClashId,
                AttemptedStrategies = new List<string>()
            };

            // Find applicable rule
            var rule = FindApplicableRule(clash);
            if (rule == null)
            {
                rule = _resolutionRules.GetValueOrDefault("MEP_MEP"); // Default rule
            }

            if (rule == null)
            {
                resolution.Success = false;
                resolution.Reason = "No applicable resolution rule found";
                resolution.RequiresManualReview = true;
                return resolution;
            }

            // Determine which element to move
            var movingElement = DetermineMovingElement(clash, rule);
            if (movingElement == null)
            {
                resolution.Success = false;
                resolution.Reason = "No element can be moved automatically";
                resolution.RequiresManualReview = true;
                return resolution;
            }

            // Try preferred resolution strategy first
            var strategies = new List<ResolutionStrategy> { rule.PreferredResolution };
            strategies.AddRange(rule.AlternativeResolutions);

            foreach (var strategy in strategies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                resolution.AttemptedStrategies.Add(strategy.ToString());

                var result = await TryResolutionStrategyAsync(clash, movingElement, strategy, rule, options);
                if (result.Success)
                {
                    resolution.Success = true;
                    resolution.AppliedStrategy = strategy;
                    resolution.ModifiedElement = movingElement.ElementId;
                    resolution.Modification = result.Modification;
                    resolution.EstimatedCost = result.EstimatedCost;
                    resolution.EstimatedImpact = result.EstimatedImpact;
                    resolution.RequiresEngineerApproval = rule.RequiresEngineerApproval;
                    return resolution;
                }
            }

            // All strategies failed
            resolution.Success = false;
            resolution.Reason = "All resolution strategies failed";
            resolution.RequiresManualReview = true;
            resolution.SuggestedOptions = GenerateManualResolutionOptions(clash);

            return resolution;
        }

        private ResolutionRule FindApplicableRule(ResolvableClash clash)
        {
            // Try specific rule first
            var key1 = $"{clash.Element1.ElementType}_{clash.Element2.ElementType}";
            var key2 = $"{clash.Element2.ElementType}_{clash.Element1.ElementType}";

            if (_resolutionRules.TryGetValue(key1, out var rule))
                return rule;
            if (_resolutionRules.TryGetValue(key2, out rule))
                return rule;

            // Try general category rules
            if (clash.DisciplineCategory == "MEP-MEP")
                return _resolutionRules.GetValueOrDefault("MEP_MEP");

            return null;
        }

        private ClashElement DetermineMovingElement(ResolvableClash clash, ResolutionRule rule)
        {
            ClashElement element1 = clash.Element1;
            ClashElement element2 = clash.Element2;

            if (rule.MovingElement == "SmallerElement")
            {
                // Move the smaller element
                var size1 = GetElementSize(element1);
                var size2 = GetElementSize(element2);
                return size1 < size2 ? element1 : element2;
            }
            else if (rule.MovingElement == "LowerPriority")
            {
                // Move the lower priority element
                var prio1 = GetElementPriority(element1.ElementType);
                var prio2 = GetElementPriority(element2.ElementType);
                return prio1 > prio2 ? element1 : element2;
            }
            else
            {
                // Move specified element type
                if (element1.ElementType.Equals(rule.MovingElement, StringComparison.OrdinalIgnoreCase))
                    return element1;
                if (element2.ElementType.Equals(rule.MovingElement, StringComparison.OrdinalIgnoreCase))
                    return element2;
            }

            // Fallback: move lower priority
            var p1 = GetElementPriority(element1.ElementType);
            var p2 = GetElementPriority(element2.ElementType);
            var movingElement = p1 > p2 ? element1 : element2;

            // Check if it can actually move
            var priority = _elementPriorities.GetValueOrDefault(movingElement.ElementType);
            return priority?.CanAutoMove == true ? movingElement : null;
        }

        private async Task<(bool Success, ElementModification Modification, decimal EstimatedCost, string EstimatedImpact)>
            TryResolutionStrategyAsync(
                ResolvableClash clash,
                ClashElement movingElement,
                ResolutionStrategy strategy,
                ResolutionRule rule,
                AutoResolutionOptions options)
        {
            await Task.Delay(10); // Simulate async operation

            var modification = new ElementModification
            {
                ElementId = movingElement.ElementId,
                Strategy = strategy
            };

            switch (strategy)
            {
                case ResolutionStrategy.RouteUnder:
                    double requiredOffset = clash.PenetrationDistance + 50; // 50mm additional clearance
                    if (requiredOffset <= rule.MaxVerticalOffset)
                    {
                        modification.VerticalOffset = -requiredOffset;
                        modification.Description = $"Move {movingElement.ElementType} down by {requiredOffset:F0}mm";
                        return (true, modification, EstimateCost(strategy, requiredOffset), "Minor impact - vertical adjustment only");
                    }
                    break;

                case ResolutionStrategy.RouteAbove:
                    requiredOffset = clash.PenetrationDistance + 50;
                    if (requiredOffset <= rule.MaxVerticalOffset)
                    {
                        modification.VerticalOffset = requiredOffset;
                        modification.Description = $"Move {movingElement.ElementType} up by {requiredOffset:F0}mm";
                        return (true, modification, EstimateCost(strategy, requiredOffset), "Minor impact - vertical adjustment only");
                    }
                    break;

                case ResolutionStrategy.RouteAround:
                    double horizontalOffset = clash.PenetrationDistance + 100;
                    if (horizontalOffset <= rule.MaxHorizontalOffset)
                    {
                        modification.HorizontalOffset = horizontalOffset;
                        modification.Description = $"Reroute {movingElement.ElementType} horizontally by {horizontalOffset:F0}mm";
                        return (true, modification, EstimateCost(strategy, horizontalOffset), "Moderate impact - routing change required");
                    }
                    break;

                case ResolutionStrategy.ReduceSize:
                    var priority = _elementPriorities.GetValueOrDefault(movingElement.ElementType);
                    if (priority?.CanAutoResize == true)
                    {
                        double reductionPercent = Math.Min(20, (clash.PenetrationDistance / movingElement.Size) * 100 + 10);
                        modification.SizeReductionPercent = reductionPercent;
                        modification.Description = $"Reduce {movingElement.ElementType} size by {reductionPercent:F0}%";
                        return (true, modification, EstimateCost(strategy, reductionPercent), "Requires engineering verification of capacity");
                    }
                    break;

                case ResolutionStrategy.CreateSleeve:
                    modification.RequiresSleeve = true;
                    modification.SleeveSize = movingElement.Size + 100; // Element size + 100mm
                    modification.Description = $"Create {modification.SleeveSize:F0}mm sleeve for {movingElement.ElementType}";
                    return (true, modification, EstimateCost(strategy, modification.SleeveSize), "Requires structural engineer approval");
            }

            return (false, null, 0, null);
        }

        private decimal EstimateCost(ResolutionStrategy strategy, double magnitude)
        {
            // Base costs in currency units (e.g., USD)
            return strategy switch
            {
                ResolutionStrategy.RouteUnder => 50 + (decimal)(magnitude * 0.5),
                ResolutionStrategy.RouteAbove => 50 + (decimal)(magnitude * 0.5),
                ResolutionStrategy.RouteAround => 100 + (decimal)(magnitude * 0.8),
                ResolutionStrategy.ReduceSize => 200 + (decimal)(magnitude * 2),
                ResolutionStrategy.CreateSleeve => 300 + (decimal)(magnitude * 1.5),
                _ => 100
            };
        }

        private List<ResolutionOption> GenerateManualResolutionOptions(ResolvableClash clash)
        {
            var options = new List<ResolutionOption>();

            // Always suggest coordination meeting
            options.Add(new ResolutionOption
            {
                OptionId = "MAN001",
                Description = "Schedule coordination meeting to resolve conflict",
                Disciplines = new[] { clash.Element1.Discipline.ToString(), clash.Element2.Discipline.ToString() },
                EstimatedEffort = TimeSpan.FromHours(2)
            });

            // Suggest structural review for structural clashes
            if (clash.DisciplineCategory.Contains("Struct"))
            {
                options.Add(new ResolutionOption
                {
                    OptionId = "MAN002",
                    Description = "Request structural engineer review for penetration/modification",
                    Disciplines = new[] { "Structural" },
                    EstimatedEffort = TimeSpan.FromHours(4)
                });
            }

            // Suggest design change
            options.Add(new ResolutionOption
            {
                OptionId = "MAN003",
                Description = "Consider design modification to eliminate conflict",
                Disciplines = new[] { clash.Element1.Discipline.ToString(), clash.Element2.Discipline.ToString() },
                EstimatedEffort = TimeSpan.FromHours(8)
            });

            return options;
        }

        #endregion

        #region Batch Resolution

        /// <summary>
        /// Resolves multiple similar clashes in batch for efficiency.
        /// </summary>
        public async Task<BatchResolutionReport> BatchResolveClashesAsync(
            List<ResolvableClash> clashes,
            BatchResolutionOptions options,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting batch resolution for {0} clashes", clashes.Count);

            var report = new BatchResolutionReport
            {
                StartTime = DateTime.UtcNow,
                TotalClashes = clashes.Count,
                Groups = new List<ClashGroup>()
            };

            // Group similar clashes
            var groups = GroupSimilarClashes(clashes, options.GroupingCriteria);

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var clashGroup = new ClashGroup
                {
                    GroupId = GenerateGroupId(),
                    GroupingReason = group.Key,
                    Clashes = group.Value,
                    ClashCount = group.Value.Count
                };

                // Find common resolution for the group
                var commonResolution = await FindCommonResolutionAsync(group.Value, options);
                if (commonResolution != null)
                {
                    clashGroup.CommonResolution = commonResolution;
                    clashGroup.CanBatchResolve = true;
                    clashGroup.EstimatedTotalCost = commonResolution.EstimatedCost * group.Value.Count;
                }
                else
                {
                    clashGroup.CanBatchResolve = false;
                    clashGroup.FailureReason = "No common resolution pattern found";
                }

                report.Groups.Add(clashGroup);
            }

            report.EndTime = DateTime.UtcNow;
            report.BatchableGroupCount = report.Groups.Count(g => g.CanBatchResolve);
            report.TotalBatchableClashes = report.Groups.Where(g => g.CanBatchResolve).Sum(g => g.ClashCount);

            Logger.Info("Batch analysis complete: {0} groups, {1} clashes batchable",
                report.Groups.Count, report.TotalBatchableClashes);

            return report;
        }

        private Dictionary<string, List<ResolvableClash>> GroupSimilarClashes(
            List<ResolvableClash> clashes,
            BatchGroupingCriteria criteria)
        {
            return criteria switch
            {
                BatchGroupingCriteria.ElementTypes => clashes
                    .GroupBy(c => $"{c.Element1.ElementType}_{c.Element2.ElementType}")
                    .ToDictionary(g => g.Key, g => g.ToList()),

                BatchGroupingCriteria.DisciplinePair => clashes
                    .GroupBy(c => c.DisciplineCategory)
                    .ToDictionary(g => g.Key, g => g.ToList()),

                BatchGroupingCriteria.Level => clashes
                    .GroupBy(c => c.Element1.LevelName ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.ToList()),

                BatchGroupingCriteria.SeverityAndType => clashes
                    .GroupBy(c => $"{c.Severity}_{c.ClashType}")
                    .ToDictionary(g => g.Key, g => g.ToList()),

                _ => clashes
                    .GroupBy(c => c.DisciplineCategory)
                    .ToDictionary(g => g.Key, g => g.ToList())
            };
        }

        private async Task<ClashResolution> FindCommonResolutionAsync(
            List<ResolvableClash> clashes,
            BatchResolutionOptions options)
        {
            await Task.Delay(10); // Simulate async

            if (clashes.Count == 0) return null;

            // Check if all clashes can use the same resolution
            var firstClash = clashes.First();
            var rule = FindApplicableRule(firstClash);
            if (rule == null) return null;

            // Check if resolution would work for all clashes in group
            var maxPenetration = clashes.Max(c => c.PenetrationDistance);
            var requiredOffset = maxPenetration + 50;

            if (requiredOffset <= rule.MaxVerticalOffset)
            {
                return new ClashResolution
                {
                    Success = true,
                    AppliedStrategy = rule.PreferredResolution,
                    Modification = new ElementModification
                    {
                        Strategy = rule.PreferredResolution,
                        VerticalOffset = rule.PreferredResolution == ResolutionStrategy.RouteUnder ? -requiredOffset : requiredOffset,
                        Description = $"Batch resolution: Adjust vertical position by {requiredOffset:F0}mm"
                    },
                    EstimatedCost = EstimateCost(rule.PreferredResolution, requiredOffset)
                };
            }

            return null;
        }

        /// <summary>
        /// Applies batch resolution to a group of clashes.
        /// </summary>
        public async Task<BatchApplicationResult> ApplyBatchResolutionAsync(
            ClashGroup group,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Applying batch resolution to group {0} with {1} clashes",
                group.GroupId, group.ClashCount);

            var result = new BatchApplicationResult
            {
                GroupId = group.GroupId,
                StartTime = DateTime.UtcNow,
                AppliedCount = 0,
                FailedCount = 0,
                Results = new List<ClashResolution>()
            };

            if (!group.CanBatchResolve || group.CommonResolution == null)
            {
                result.Success = false;
                result.FailureReason = "Group is not eligible for batch resolution";
                return result;
            }

            foreach (var clash in group.Clashes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resolution = new ClashResolution
                {
                    ClashId = clash.ClashId,
                    AppliedStrategy = group.CommonResolution.AppliedStrategy,
                    Modification = group.CommonResolution.Modification,
                    EstimatedCost = group.CommonResolution.EstimatedCost
                };

                // In real implementation, would apply modification via Revit API
                resolution.Success = true;
                clash.Status = ClashResolutionStatus.Resolved;
                clash.Resolution = resolution;

                result.Results.Add(resolution);
                result.AppliedCount++;
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = result.FailedCount == 0;
            result.TotalCost = result.Results.Where(r => r.Success).Sum(r => r.EstimatedCost);

            Logger.Info("Batch resolution applied: {0}/{1} successful",
                result.AppliedCount, group.ClashCount);

            return result;
        }

        #endregion

        #region Learning

        /// <summary>
        /// Records a user resolution for learning purposes.
        /// </summary>
        public void RecordUserResolution(ResolvableClash clash, UserResolution userResolution)
        {
            lock (_lock)
            {
                var history = new ResolutionHistory
                {
                    HistoryId = GenerateHistoryId(),
                    ClashId = clash.ClashId,
                    Element1Type = clash.Element1.ElementType,
                    Element2Type = clash.Element2.ElementType,
                    Discipline1 = clash.Element1.Discipline,
                    Discipline2 = clash.Element2.Discipline,
                    ClashType = clash.ClashType,
                    PenetrationDistance = clash.PenetrationDistance,
                    AppliedResolution = userResolution,
                    ResolvedAt = DateTime.UtcNow,
                    ResolvedBy = userResolution.ResolvedBy
                };

                _resolutionHistory.Add(history);

                // Update learned patterns
                UpdateLearnedPatterns(history);

                Logger.Info("User resolution recorded for clash {0}: {1}",
                    clash.ClashId, userResolution.Strategy);
            }
        }

        private void UpdateLearnedPatterns(ResolutionHistory history)
        {
            var patternKey = $"{history.Element1Type}_{history.Element2Type}";

            if (!_learnedPatterns.TryGetValue(patternKey, out var pattern))
            {
                pattern = new ResolutionPattern
                {
                    PatternId = GeneratePatternId(),
                    Element1Type = history.Element1Type,
                    Element2Type = history.Element2Type,
                    StrategyFrequency = new Dictionary<ResolutionStrategy, int>(),
                    TotalResolutions = 0
                };
                _learnedPatterns[patternKey] = pattern;
            }

            // Update frequency
            if (!pattern.StrategyFrequency.ContainsKey(history.AppliedResolution.Strategy))
                pattern.StrategyFrequency[history.AppliedResolution.Strategy] = 0;

            pattern.StrategyFrequency[history.AppliedResolution.Strategy]++;
            pattern.TotalResolutions++;

            // Update preferred strategy based on frequency
            pattern.PreferredStrategy = pattern.StrategyFrequency
                .OrderByDescending(kv => kv.Value)
                .First().Key;

            pattern.Confidence = (double)pattern.StrategyFrequency[pattern.PreferredStrategy] / pattern.TotalResolutions;
            pattern.LastUpdated = DateTime.UtcNow;
        }

        private void LearnFromResolution(ResolvableClash clash, ClashResolution resolution)
        {
            if (!resolution.Success || resolution.AppliedStrategy == null)
                return;

            var userResolution = new UserResolution
            {
                Strategy = resolution.AppliedStrategy.Value,
                Modification = resolution.Modification,
                ResolvedBy = "AutoResolver"
            };

            RecordUserResolution(clash, userResolution);
        }

        /// <summary>
        /// Gets learned resolution pattern for a clash type.
        /// </summary>
        public ResolutionPattern GetLearnedPattern(string element1Type, string element2Type)
        {
            lock (_lock)
            {
                var key1 = $"{element1Type}_{element2Type}";
                var key2 = $"{element2Type}_{element1Type}";

                if (_learnedPatterns.TryGetValue(key1, out var pattern))
                    return pattern;
                if (_learnedPatterns.TryGetValue(key2, out pattern))
                    return pattern;

                return null;
            }
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generates a comprehensive clash report with visualizations.
        /// </summary>
        public async Task<ClashVisualizationReport> GenerateVisualizationReportAsync(
            ClashDetectionReport detectionReport,
            ReportOptions options,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating visualization report for {0} clashes", detectionReport.Statistics.TotalClashes);

            var report = new ClashVisualizationReport
            {
                ReportId = GenerateReportId(),
                GeneratedAt = DateTime.UtcNow,
                ModelName = detectionReport.ModelName,
                Summary = new ReportSummary()
            };

            await Task.Run(() =>
            {
                // Generate summary
                report.Summary = GenerateSummary(detectionReport);

                // Generate heat map data
                if (options.IncludeHeatMap)
                {
                    report.HeatMapData = GenerateHeatMapData(detectionReport.ResolvableClashes);
                }

                // Generate discipline matrix
                if (options.IncludeDisciplineMatrix)
                {
                    report.DisciplineMatrix = GenerateDisciplineMatrix(detectionReport.ResolvableClashes);
                }

                // Generate severity timeline
                if (options.IncludeTrends)
                {
                    report.SeverityDistribution = GenerateSeverityDistribution(detectionReport.ResolvableClashes);
                }

                // Generate detailed clash list
                report.ClashDetails = detectionReport.ResolvableClashes
                    .OrderByDescending(c => c.SeverityScore)
                    .Take(options.MaxDetailedClashes)
                    .Select(c => new ClashDetail
                    {
                        ClashId = c.ClashId,
                        Element1 = $"{c.Element1.Name} ({c.Element1.ElementId})",
                        Element2 = $"{c.Element2.Name} ({c.Element2.ElementId})",
                        Location = FormatLocation(c.IntersectionPoint),
                        Level = c.Element1.LevelName,
                        ClashType = c.ClashType.ToString(),
                        Severity = c.Severity.ToString(),
                        PenetrationDistance = c.PenetrationDistance,
                        Status = c.Status.ToString(),
                        SuggestedResolution = GenerateResolutionSuggestion(c)
                    })
                    .ToList();

                // Generate recommendations
                report.Recommendations = GenerateRecommendations(detectionReport);

            }, cancellationToken);

            Logger.Info("Visualization report generated: {0}", report.ReportId);

            return report;
        }

        private ReportSummary GenerateSummary(ClashDetectionReport report)
        {
            return new ReportSummary
            {
                TotalClashes = report.Statistics.TotalClashes,
                CriticalClashes = report.Statistics.CriticalClashes,
                MajorClashes = report.Statistics.MajorClashes,
                MinorClashes = report.Statistics.MinorClashes,
                WarningClashes = report.Statistics.WarningClashes,
                MostAffectedDiscipline = report.Statistics.ByDisciplineCategory
                    .OrderByDescending(kv => kv.Value)
                    .FirstOrDefault().Key ?? "None",
                MostAffectedLevel = report.Statistics.ByLevel
                    .OrderByDescending(kv => kv.Value)
                    .FirstOrDefault().Key ?? "None",
                EstimatedResolutionHours = report.Statistics.TotalClashes * 0.5 +
                    report.Statistics.CriticalClashes * 2 +
                    report.Statistics.MajorClashes * 1
            };
        }

        private List<HeatMapCell> GenerateHeatMapData(List<ResolvableClash> clashes)
        {
            var cells = new List<HeatMapCell>();
            double cellSize = 5000; // 5m grid

            var clashGroups = clashes
                .Where(c => c.IntersectionPoint != null)
                .GroupBy(c => new
                {
                    CellX = (int)(c.IntersectionPoint.X / cellSize),
                    CellY = (int)(c.IntersectionPoint.Y / cellSize),
                    Level = c.Element1.LevelName
                });

            foreach (var group in clashGroups)
            {
                cells.Add(new HeatMapCell
                {
                    X = group.Key.CellX * cellSize,
                    Y = group.Key.CellY * cellSize,
                    Level = group.Key.Level,
                    ClashCount = group.Count(),
                    MaxSeverity = group.Max(c => c.SeverityScore),
                    Intensity = Math.Min(1.0, group.Count() / 10.0)
                });
            }

            return cells;
        }

        private List<DisciplineMatrixCell> GenerateDisciplineMatrix(List<ResolvableClash> clashes)
        {
            var matrix = new List<DisciplineMatrixCell>();
            var disciplines = Enum.GetValues(typeof(ClashDiscipline)).Cast<ClashDiscipline>();

            foreach (var disc1 in disciplines)
            {
                foreach (var disc2 in disciplines)
                {
                    var count = clashes.Count(c =>
                        (c.Element1.Discipline == disc1 && c.Element2.Discipline == disc2) ||
                        (c.Element1.Discipline == disc2 && c.Element2.Discipline == disc1));

                    if (count > 0)
                    {
                        matrix.Add(new DisciplineMatrixCell
                        {
                            Discipline1 = disc1.ToString(),
                            Discipline2 = disc2.ToString(),
                            ClashCount = count,
                            CriticalCount = clashes.Count(c =>
                                c.Severity == ClashSeverityLevel.Critical &&
                                ((c.Element1.Discipline == disc1 && c.Element2.Discipline == disc2) ||
                                 (c.Element1.Discipline == disc2 && c.Element2.Discipline == disc1)))
                        });
                    }
                }
            }

            return matrix;
        }

        private Dictionary<ClashSeverityLevel, int> GenerateSeverityDistribution(List<ResolvableClash> clashes)
        {
            return clashes
                .GroupBy(c => c.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private string FormatLocation(ClashPoint3D point)
        {
            if (point == null) return "Unknown";
            return $"({point.X:F0}, {point.Y:F0}, {point.Z:F0})";
        }

        private string GenerateResolutionSuggestion(ResolvableClash clash)
        {
            var rule = FindApplicableRule(clash);
            if (rule != null)
            {
                return $"{rule.PreferredResolution}: {rule.MovingElement} element should yield";
            }

            // Check learned patterns
            var pattern = GetLearnedPattern(clash.Element1.ElementType, clash.Element2.ElementType);
            if (pattern != null && pattern.Confidence > 0.7)
            {
                return $"{pattern.PreferredStrategy} (learned, {pattern.Confidence:P0} confidence)";
            }

            return "Manual coordination required";
        }

        private List<string> GenerateRecommendations(ClashDetectionReport report)
        {
            var recommendations = new List<string>();

            if (report.Statistics.CriticalClashes > 0)
            {
                recommendations.Add($"URGENT: Address {report.Statistics.CriticalClashes} critical clashes before construction");
            }

            var topDiscipline = report.Statistics.ByDisciplineCategory
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();

            if (topDiscipline.Value > 10)
            {
                recommendations.Add($"Schedule {topDiscipline.Key} coordination meeting - {topDiscipline.Value} clashes identified");
            }

            var topLevel = report.Statistics.ByLevel
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();

            if (topLevel.Value > 15)
            {
                recommendations.Add($"Focus coordination effort on {topLevel.Key} - highest clash concentration");
            }

            if (report.Statistics.SoftClashes > report.Statistics.HardClashes)
            {
                recommendations.Add("Review clearance requirements - many soft clashes indicate tight service zones");
            }

            if (report.Statistics.WorkflowClashes > 5)
            {
                recommendations.Add("Review construction sequence and fire protection requirements");
            }

            return recommendations;
        }

        #endregion

        #region Utility Methods

        private ToleranceSettings GetToleranceForElement(string elementType)
        {
            if (_toleranceSettings.TryGetValue(elementType, out var settings))
                return settings;
            return _toleranceSettings.GetValueOrDefault("Default") ?? new ToleranceSettings();
        }

        private int GetElementPriority(string elementType)
        {
            if (_elementPriorities.TryGetValue(elementType, out var priority))
                return priority.Priority;
            return 50; // Default mid-priority
        }

        private double GetElementSize(ClashElement element)
        {
            if (element.BoundingBox == null) return 0;

            var box = element.BoundingBox;
            return Math.Max(
                box.Max.X - box.Min.X,
                Math.Max(box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z));
        }

        private string GenerateClashId()
        {
            return $"CLH_{Guid.NewGuid():N}".Substring(0, 15);
        }

        private string GenerateGroupId()
        {
            return $"GRP_{Guid.NewGuid():N}".Substring(0, 15);
        }

        private string GenerateHistoryId()
        {
            return $"HIS_{Guid.NewGuid():N}".Substring(0, 15);
        }

        private string GeneratePatternId()
        {
            return $"PAT_{Guid.NewGuid():N}".Substring(0, 15);
        }

        private string GenerateReportId()
        {
            return $"RPT_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}".Substring(0, 25);
        }

        /// <summary>
        /// Updates configuration settings.
        /// </summary>
        public void UpdateConfiguration(ClashResolutionConfig config)
        {
            _config = config ?? ClashResolutionConfig.Default;
            Logger.Info("Configuration updated");
        }

        /// <summary>
        /// Gets current resolution statistics.
        /// </summary>
        public ResolutionStatistics GetResolutionStatistics()
        {
            lock (_lock)
            {
                return new ResolutionStatistics
                {
                    TotalResolutions = _resolutionHistory.Count,
                    AutoResolvedCount = _resolutionHistory.Count(h => h.ResolvedBy == "AutoResolver"),
                    ManualResolutionCount = _resolutionHistory.Count(h => h.ResolvedBy != "AutoResolver"),
                    LearnedPatternCount = _learnedPatterns.Count,
                    MostCommonStrategy = _resolutionHistory
                        .GroupBy(h => h.AppliedResolution.Strategy)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? ResolutionStrategy.RouteAround,
                    AverageConfidence = _learnedPatterns.Values
                        .Where(p => p.TotalResolutions > 0)
                        .Select(p => p.Confidence)
                        .DefaultIfEmpty(0)
                        .Average()
                };
            }
        }

        #endregion
    }

    #region Data Models

    public enum ClashDiscipline
    {
        Structural,
        Architectural,
        HVAC,
        Plumbing,
        Electrical,
        FireProtection
    }

    public enum ClashCategory
    {
        Hard,
        Soft,
        Workflow
    }

    public enum ClashSeverityLevel
    {
        Warning,
        Minor,
        Major,
        Critical
    }

    public enum ClashResolutionStatus
    {
        New,
        Active,
        InProgress,
        Resolved,
        Approved,
        Ignored
    }

    public enum ResolutionStrategy
    {
        RouteUnder,
        RouteAbove,
        RouteAround,
        ReduceSize,
        CreateSleeve,
        DesignChange,
        Accept
    }

    public enum BatchGroupingCriteria
    {
        ElementTypes,
        DisciplinePair,
        Level,
        SeverityAndType
    }

    public class ClashResolutionConfig
    {
        public double DefaultHardClashTolerance { get; set; } = 0;
        public double DefaultSoftClashTolerance { get; set; } = 50;
        public bool EnableLearning { get; set; } = true;
        public int MaxAutoResolveBatchSize { get; set; } = 100;
        public double LearningConfidenceThreshold { get; set; } = 0.7;

        public static ClashResolutionConfig Default => new ClashResolutionConfig();
    }

    public class ElementPriority
    {
        public string ElementType { get; set; }
        public ClashDiscipline Discipline { get; set; }
        public int Priority { get; set; }
        public bool CanAutoMove { get; set; }
        public bool CanAutoResize { get; set; }
    }

    public class ToleranceSettings
    {
        public string ElementType { get; set; }
        public double HardClashTolerance { get; set; }
        public double SoftClashTolerance { get; set; }
        public double InsulationAllowance { get; set; }
    }

    public class ResolutionRule
    {
        public string RuleId { get; set; }
        public string Element1Type { get; set; }
        public string Element2Type { get; set; }
        public ResolutionStrategy PreferredResolution { get; set; }
        public ResolutionStrategy[] AlternativeResolutions { get; set; }
        public string MovingElement { get; set; }
        public double MaxVerticalOffset { get; set; }
        public double MaxHorizontalOffset { get; set; }
        public bool RequiresEngineerApproval { get; set; }
    }

    public class ClashDetectionInput
    {
        public string ModelName { get; set; }
        public List<ClashElement> Elements { get; set; } = new List<ClashElement>();
        public ClashDetectionOptions Options { get; set; } = new ClashDetectionOptions();
    }

    public class ClashDetectionOptions
    {
        public bool DetectHardClashes { get; set; } = true;
        public bool DetectSoftClashes { get; set; } = true;
        public bool DetectWorkflowClashes { get; set; } = true;
        public double CustomTolerance { get; set; } = 0;
        public List<ClashDiscipline> IncludedDisciplines { get; set; }
        public List<string> ExcludedElementTypes { get; set; }
    }

    public class ClashElement
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string ElementType { get; set; }
        public ClashDiscipline Discipline { get; set; }
        public string LevelName { get; set; }
        public ClashBoundingBox BoundingBox { get; set; }
        public double Size { get; set; }
        public bool IsFireRated { get; set; }
        public bool HasFireDamper { get; set; }
        public bool HasFireSleeve { get; set; }
        public int ConstructionPhase { get; set; }
    }

    public class ClashBoundingBox
    {
        public ClashPoint3D Min { get; set; }
        public ClashPoint3D Max { get; set; }
    }

    public class ClashPoint3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class ResolvableClash
    {
        public string ClashId { get; set; }
        public ClashElement Element1 { get; set; }
        public ClashElement Element2 { get; set; }
        public ClashCategory ClashType { get; set; }
        public ClashSeverityLevel Severity { get; set; }
        public int SeverityScore { get; set; }
        public string DisciplineCategory { get; set; }
        public ClashPoint3D IntersectionPoint { get; set; }
        public double PenetrationDistance { get; set; }
        public double ClashVolume { get; set; }
        public double RequiredClearance { get; set; }
        public double ActualClearance { get; set; }
        public string WorkflowIssue { get; set; }
        public ClashResolutionStatus Status { get; set; }
        public ClashResolution Resolution { get; set; }
    }

    public class ClashDetectionProgress
    {
        public int TotalComparisons { get; set; }
        public int CompletedComparisons { get; set; }
        public string CurrentElement { get; set; }
        public int ClashesFound { get; set; }
        public double PercentComplete => TotalComparisons > 0
            ? (double)CompletedComparisons / TotalComparisons * 100
            : 0;
    }

    public class ClashDetectionReport
    {
        public string ModelName { get; set; }
        public DateTime DetectionStartTime { get; set; }
        public DateTime DetectionEndTime { get; set; }
        public List<ResolvableClash> ResolvableClashes { get; set; }
        public ClashStatistics Statistics { get; set; }
        public TimeSpan Duration => DetectionEndTime - DetectionStartTime;
    }

    public class ClashStatistics
    {
        public int TotalClashes { get; set; }
        public int HardClashes { get; set; }
        public int SoftClashes { get; set; }
        public int WorkflowClashes { get; set; }
        public int CriticalClashes { get; set; }
        public int MajorClashes { get; set; }
        public int MinorClashes { get; set; }
        public int WarningClashes { get; set; }
        public Dictionary<string, int> ByDisciplineCategory { get; set; }
        public Dictionary<string, int> ByLevel { get; set; }
    }

    public class AutoResolutionOptions
    {
        public double MaxAutoResolvePenetration { get; set; } = 100;
        public bool EnableLearning { get; set; } = true;
        public bool RequireApproval { get; set; } = false;
        public List<ResolutionStrategy> AllowedStrategies { get; set; }
    }

    public class ResolutionProgress
    {
        public int TotalClashes { get; set; }
        public int ProcessedClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public string CurrentClash { get; set; }
        public double PercentComplete => TotalClashes > 0
            ? (double)ProcessedClashes / TotalClashes * 100
            : 0;
    }

    public class ClashResolution
    {
        public string ClashId { get; set; }
        public bool Success { get; set; }
        public string Reason { get; set; }
        public ResolutionStrategy? AppliedStrategy { get; set; }
        public List<string> AttemptedStrategies { get; set; }
        public string ModifiedElement { get; set; }
        public ElementModification Modification { get; set; }
        public decimal EstimatedCost { get; set; }
        public string EstimatedImpact { get; set; }
        public bool RequiresManualReview { get; set; }
        public bool RequiresEngineerApproval { get; set; }
        public List<ResolutionOption> SuggestedOptions { get; set; }
    }

    public class ElementModification
    {
        public string ElementId { get; set; }
        public ResolutionStrategy Strategy { get; set; }
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }
        public double SizeReductionPercent { get; set; }
        public bool RequiresSleeve { get; set; }
        public double SleeveSize { get; set; }
        public string Description { get; set; }
    }

    public class ResolutionOption
    {
        public string OptionId { get; set; }
        public string Description { get; set; }
        public string[] Disciplines { get; set; }
        public TimeSpan EstimatedEffort { get; set; }
    }

    public class ClashResolutionReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalClashes { get; set; }
        public int ResolvedCount { get; set; }
        public int FailedCount { get; set; }
        public int ManualReviewCount { get; set; }
        public double SuccessRate { get; set; }
        public List<ClashResolution> Resolutions { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }

    public class BatchResolutionOptions
    {
        public BatchGroupingCriteria GroupingCriteria { get; set; } = BatchGroupingCriteria.DisciplinePair;
        public int MinimumGroupSize { get; set; } = 3;
        public bool RequireApproval { get; set; } = true;
    }

    public class BatchResolutionReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalClashes { get; set; }
        public int BatchableGroupCount { get; set; }
        public int TotalBatchableClashes { get; set; }
        public List<ClashGroup> Groups { get; set; }
    }

    public class ClashGroup
    {
        public string GroupId { get; set; }
        public string GroupingReason { get; set; }
        public List<ResolvableClash> Clashes { get; set; }
        public int ClashCount { get; set; }
        public bool CanBatchResolve { get; set; }
        public string FailureReason { get; set; }
        public ClashResolution CommonResolution { get; set; }
        public decimal EstimatedTotalCost { get; set; }
    }

    public class BatchApplicationResult
    {
        public string GroupId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public int AppliedCount { get; set; }
        public int FailedCount { get; set; }
        public decimal TotalCost { get; set; }
        public List<ClashResolution> Results { get; set; }
    }

    public class UserResolution
    {
        public ResolutionStrategy Strategy { get; set; }
        public ElementModification Modification { get; set; }
        public string ResolvedBy { get; set; }
        public string Notes { get; set; }
    }

    public class ResolutionHistory
    {
        public string HistoryId { get; set; }
        public string ClashId { get; set; }
        public string Element1Type { get; set; }
        public string Element2Type { get; set; }
        public ClashDiscipline Discipline1 { get; set; }
        public ClashDiscipline Discipline2 { get; set; }
        public ClashCategory ClashType { get; set; }
        public double PenetrationDistance { get; set; }
        public UserResolution AppliedResolution { get; set; }
        public DateTime ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
    }

    public class ResolutionPattern
    {
        public string PatternId { get; set; }
        public string Element1Type { get; set; }
        public string Element2Type { get; set; }
        public Dictionary<ResolutionStrategy, int> StrategyFrequency { get; set; }
        public int TotalResolutions { get; set; }
        public ResolutionStrategy PreferredStrategy { get; set; }
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ResolutionStatistics
    {
        public int TotalResolutions { get; set; }
        public int AutoResolvedCount { get; set; }
        public int ManualResolutionCount { get; set; }
        public int LearnedPatternCount { get; set; }
        public ResolutionStrategy MostCommonStrategy { get; set; }
        public double AverageConfidence { get; set; }
    }

    public class ReportOptions
    {
        public bool IncludeHeatMap { get; set; } = true;
        public bool IncludeDisciplineMatrix { get; set; } = true;
        public bool IncludeTrends { get; set; } = true;
        public int MaxDetailedClashes { get; set; } = 100;
        public string OutputFormat { get; set; } = "HTML";
    }

    public class ClashVisualizationReport
    {
        public string ReportId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string ModelName { get; set; }
        public ReportSummary Summary { get; set; }
        public List<HeatMapCell> HeatMapData { get; set; }
        public List<DisciplineMatrixCell> DisciplineMatrix { get; set; }
        public Dictionary<ClashSeverityLevel, int> SeverityDistribution { get; set; }
        public List<ClashDetail> ClashDetails { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ReportSummary
    {
        public int TotalClashes { get; set; }
        public int CriticalClashes { get; set; }
        public int MajorClashes { get; set; }
        public int MinorClashes { get; set; }
        public int WarningClashes { get; set; }
        public string MostAffectedDiscipline { get; set; }
        public string MostAffectedLevel { get; set; }
        public double EstimatedResolutionHours { get; set; }
    }

    public class HeatMapCell
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Level { get; set; }
        public int ClashCount { get; set; }
        public int MaxSeverity { get; set; }
        public double Intensity { get; set; }
    }

    public class DisciplineMatrixCell
    {
        public string Discipline1 { get; set; }
        public string Discipline2 { get; set; }
        public int ClashCount { get; set; }
        public int CriticalCount { get; set; }
    }

    public class ClashDetail
    {
        public string ClashId { get; set; }
        public string Element1 { get; set; }
        public string Element2 { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string ClashType { get; set; }
        public string Severity { get; set; }
        public double PenetrationDistance { get; set; }
        public string Status { get; set; }
        public string SuggestedResolution { get; set; }
    }

    #endregion
}
