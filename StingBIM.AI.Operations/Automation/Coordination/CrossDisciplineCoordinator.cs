// ============================================================================
// StingBIM AI - Cross-Discipline Coordination
// Automatically detects and resolves conflicts between building disciplines
// Manages coordination workflows and clash resolution
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Automation.Coordination
{
    /// <summary>
    /// Cross-Discipline Coordination AI
    /// Automates detection and resolution of interdisciplinary conflicts
    /// </summary>
    public class CrossDisciplineCoordinator
    {
        private readonly ClashDetectionEngine _clashDetection;
        private readonly ConflictResolver _conflictResolver;
        private readonly CoordinationRules _rules;
        private readonly PriorityManager _priorityManager;
        private readonly ClearanceChecker _clearanceChecker;
        private readonly Dictionary<string, DisciplineProfile> _disciplineProfiles;

        public CrossDisciplineCoordinator()
        {
            _clashDetection = new ClashDetectionEngine();
            _conflictResolver = new ConflictResolver();
            _rules = new CoordinationRules();
            _priorityManager = new PriorityManager();
            _clearanceChecker = new ClearanceChecker();
            _disciplineProfiles = LoadDisciplineProfiles();
        }

        #region Coordination Analysis

        /// <summary>
        /// Run comprehensive coordination analysis across all disciplines
        /// </summary>
        public async Task<CoordinationReport> AnalyzeCoordinationAsync(
            MultiDisciplineModel model,
            CoordinationOptions options = null)
        {
            options ??= CoordinationOptions.Default;

            var report = new CoordinationReport
            {
                ModelId = model.ModelId,
                AnalyzedAt = DateTime.UtcNow
            };

            // Step 1: Detect all clashes
            report.ClashResults = await _clashDetection.DetectAllClashesAsync(model, options);

            // Step 2: Categorize clashes by severity and type
            report.ClashSummary = CategorizeClashes(report.ClashResults);

            // Step 3: Check coordination rules
            report.RuleViolations = await _rules.CheckAllRulesAsync(model);

            // Step 4: Check clearance requirements
            report.ClearanceIssues = await _clearanceChecker.CheckClearancesAsync(model);

            // Step 5: Identify coordination hotspots
            report.Hotspots = IdentifyHotspots(report.ClashResults, model);

            // Step 6: Generate resolution suggestions
            report.ResolutionPlan = await GenerateResolutionPlanAsync(
                report.ClashResults, report.RuleViolations, model, options);

            // Step 7: Estimate coordination effort
            report.EffortEstimate = EstimateResolutionEffort(report);

            // Step 8: Generate discipline-specific reports
            report.DisciplineReports = GenerateDisciplineReports(report, model);

            return report;
        }

        /// <summary>
        /// Automatically resolve clashes where possible
        /// </summary>
        public async Task<AutoResolutionResult> AutoResolveClashesAsync(
            MultiDisciplineModel model,
            List<Clash> clashes,
            AutoResolutionOptions options = null)
        {
            options ??= AutoResolutionOptions.Default;

            var result = new AutoResolutionResult
            {
                StartTime = DateTime.UtcNow,
                TotalClashes = clashes.Count
            };

            foreach (var clash in clashes)
            {
                if (CanAutoResolve(clash, options))
                {
                    var resolution = await _conflictResolver.ResolveAsync(clash, model, options);

                    if (resolution.Success)
                    {
                        result.ResolvedClashes.Add(new ResolvedClash
                        {
                            OriginalClash = clash,
                            Resolution = resolution,
                            ResolvedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        result.FailedResolutions.Add(new FailedResolution
                        {
                            Clash = clash,
                            Reason = resolution.FailureReason,
                            RequiresManualReview = true
                        });
                    }
                }
                else
                {
                    result.ManualReviewRequired.Add(clash);
                }
            }

            result.EndTime = DateTime.UtcNow;
            result.SuccessRate = (double)result.ResolvedClashes.Count / result.TotalClashes * 100;

            return result;
        }

        /// <summary>
        /// Monitor model for new coordination issues
        /// </summary>
        public async Task<CoordinationDelta> CheckForChangesAsync(
            MultiDisciplineModel currentModel,
            CoordinationReport previousReport)
        {
            var delta = new CoordinationDelta
            {
                CheckedAt = DateTime.UtcNow,
                PreviousReportDate = previousReport.AnalyzedAt
            };

            // Detect new clashes
            var currentClashes = await _clashDetection.DetectAllClashesAsync(
                currentModel, CoordinationOptions.Default);

            // Compare with previous
            var previousClashIds = previousReport.ClashResults
                .Select(c => c.ClashId).ToHashSet();

            delta.NewClashes = currentClashes
                .Where(c => !previousClashIds.Contains(c.ClashId))
                .ToList();

            delta.ResolvedClashes = previousReport.ClashResults
                .Where(c => !currentClashes.Any(cc => cc.ClashId == c.ClashId))
                .ToList();

            delta.RemainingClashes = currentClashes
                .Where(c => previousClashIds.Contains(c.ClashId))
                .ToList();

            // Check for clash severity changes
            foreach (var remaining in delta.RemainingClashes)
            {
                var previous = previousReport.ClashResults
                    .First(c => c.ClashId == remaining.ClashId);

                if (remaining.Severity != previous.Severity)
                {
                    delta.SeverityChanges.Add(new SeverityChange
                    {
                        ClashId = remaining.ClashId,
                        PreviousSeverity = previous.Severity,
                        CurrentSeverity = remaining.Severity
                    });
                }
            }

            delta.NetChange = delta.NewClashes.Count - delta.ResolvedClashes.Count;

            return delta;
        }

        #endregion

        #region Clash Resolution

        private bool CanAutoResolve(Clash clash, AutoResolutionOptions options)
        {
            // Don't auto-resolve critical structural clashes
            if (clash.Severity == ClashSeverity.Critical &&
                clash.InvolvesDiscipline(Discipline.Structural))
            {
                return false;
            }

            // Don't auto-resolve if tolerance exceeded
            if (clash.PenetrationDepth > options.MaxAutoResolvePenetration)
            {
                return false;
            }

            // Check if resolution type is allowed
            var possibleResolutions = _conflictResolver.GetPossibleResolutions(clash);
            return possibleResolutions.Any(r => options.AllowedResolutionTypes.Contains(r.Type));
        }

        private async Task<ResolutionPlan> GenerateResolutionPlanAsync(
            List<Clash> clashes,
            List<RuleViolation> violations,
            MultiDisciplineModel model,
            CoordinationOptions options)
        {
            var plan = new ResolutionPlan
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Group clashes by location for efficient resolution
            var clashGroups = GroupClashesByLocation(clashes);

            foreach (var group in clashGroups)
            {
                var groupPlan = new ResolutionGroup
                {
                    Location = group.Key,
                    Clashes = group.Value
                };

                // Determine optimal resolution order
                groupPlan.ResolutionOrder = DetermineResolutionOrder(group.Value);

                // Generate resolution suggestions for each clash
                foreach (var clash in groupPlan.ResolutionOrder)
                {
                    var suggestions = await GenerateResolutionSuggestionsAsync(clash, model);
                    groupPlan.Suggestions[clash.ClashId] = suggestions;
                }

                plan.Groups.Add(groupPlan);
            }

            // Add rule violation resolutions
            foreach (var violation in violations)
            {
                plan.RuleResolutions.Add(new RuleResolution
                {
                    Violation = violation,
                    SuggestedFix = GenerateRuleFix(violation),
                    AffectedElements = violation.AffectedElements
                });
            }

            // Calculate priority order
            plan.PriorityOrder = _priorityManager.CalculatePriorities(plan);

            return plan;
        }

        private Dictionary<string, List<Clash>> GroupClashesByLocation(List<Clash> clashes)
        {
            return clashes
                .GroupBy(c => GetLocationKey(c))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private string GetLocationKey(Clash clash)
        {
            // Group by grid intersection and level
            return $"{clash.Level}-{clash.GridIntersection ?? "General"}";
        }

        private List<Clash> DetermineResolutionOrder(List<Clash> clashes)
        {
            // Resolve in order: Structure -> Architecture -> MEP
            // Within MEP: Duct -> Pipe -> Conduit
            return clashes
                .OrderBy(c => GetDisciplinePriority(c.PrimaryDiscipline))
                .ThenByDescending(c => c.Severity)
                .ThenByDescending(c => c.PenetrationDepth)
                .ToList();
        }

        private int GetDisciplinePriority(Discipline discipline)
        {
            return discipline switch
            {
                Discipline.Structural => 1,
                Discipline.Architectural => 2,
                Discipline.Mechanical => 3,
                Discipline.Plumbing => 4,
                Discipline.Electrical => 5,
                Discipline.FireProtection => 6,
                _ => 10
            };
        }

        private async Task<List<ResolutionSuggestion>> GenerateResolutionSuggestionsAsync(
            Clash clash,
            MultiDisciplineModel model)
        {
            await Task.Delay(10); // Simulate async operation

            var suggestions = new List<ResolutionSuggestion>();

            // Determine which element should move based on discipline priority
            var movingDiscipline = GetLowerPriorityDiscipline(
                clash.PrimaryDiscipline, clash.SecondaryDiscipline);

            // Route modification suggestion
            if (clash.Type == ClashType.HardClash)
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Type = ResolutionType.RouteModification,
                    Description = $"Reroute {movingDiscipline} element to avoid {GetHigherPriorityDiscipline(clash.PrimaryDiscipline, clash.SecondaryDiscipline)} element",
                    AffectedElement = movingDiscipline == clash.PrimaryDiscipline
                        ? clash.Element1Id : clash.Element2Id,
                    Confidence = 0.8,
                    EstimatedEffort = TimeSpan.FromMinutes(30)
                });
            }

            // Elevation change suggestion
            if (HasVerticalClearance(clash, model))
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Type = ResolutionType.ElevationChange,
                    Description = $"Adjust elevation of {movingDiscipline} element by {clash.SuggestedOffset:F0}mm",
                    AffectedElement = movingDiscipline == clash.PrimaryDiscipline
                        ? clash.Element1Id : clash.Element2Id,
                    Confidence = 0.9,
                    EstimatedEffort = TimeSpan.FromMinutes(15)
                });
            }

            // Size reduction if possible
            if (clash.CanReduceSize)
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Type = ResolutionType.SizeReduction,
                    Description = "Consider smaller duct/pipe size if hydraulically acceptable",
                    Confidence = 0.6,
                    EstimatedEffort = TimeSpan.FromMinutes(45),
                    RequiresCalculationCheck = true
                });
            }

            // Structural penetration if approved
            if (clash.InvolvesDiscipline(Discipline.Structural) &&
                clash.StructuralPenetrationAllowed)
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Type = ResolutionType.StructuralPenetration,
                    Description = "Create sleeve/penetration through structural element",
                    AffectedElement = clash.Element1Id,
                    Confidence = 0.7,
                    EstimatedEffort = TimeSpan.FromMinutes(60),
                    RequiresEngineerApproval = true
                });
            }

            return suggestions.OrderByDescending(s => s.Confidence).ToList();
        }

        private Discipline GetLowerPriorityDiscipline(Discipline d1, Discipline d2)
        {
            return GetDisciplinePriority(d1) > GetDisciplinePriority(d2) ? d1 : d2;
        }

        private Discipline GetHigherPriorityDiscipline(Discipline d1, Discipline d2)
        {
            return GetDisciplinePriority(d1) < GetDisciplinePriority(d2) ? d1 : d2;
        }

        private bool HasVerticalClearance(Clash clash, MultiDisciplineModel model)
        {
            // Check if there's room above or below
            return clash.AvailableClearanceAbove > clash.PenetrationDepth ||
                   clash.AvailableClearanceBelow > clash.PenetrationDepth;
        }

        private string GenerateRuleFix(RuleViolation violation)
        {
            return violation.RuleType switch
            {
                "Clearance" => $"Increase clearance to minimum {violation.RequiredValue}mm",
                "Slope" => $"Adjust slope to minimum {violation.RequiredValue}%",
                "Support" => "Add support within maximum span requirement",
                "Access" => "Ensure maintenance access panel or door provided",
                _ => "Review and correct per code requirements"
            };
        }

        #endregion

        #region Analysis Helpers

        private ClashSummary CategorizeClashes(List<Clash> clashes)
        {
            return new ClashSummary
            {
                TotalClashes = clashes.Count,
                BySeverity = clashes.GroupBy(c => c.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByType = clashes.GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByDisciplinePair = clashes.GroupBy(c => GetDisciplinePairKey(c))
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByLevel = clashes.GroupBy(c => c.Level)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        private string GetDisciplinePairKey(Clash clash)
        {
            var disciplines = new[] { clash.PrimaryDiscipline, clash.SecondaryDiscipline }
                .OrderBy(d => d.ToString())
                .ToList();
            return $"{disciplines[0]} vs {disciplines[1]}";
        }

        private List<CoordinationHotspot> IdentifyHotspots(List<Clash> clashes, MultiDisciplineModel model)
        {
            var hotspots = new List<CoordinationHotspot>();

            // Group by spatial region
            var spatialGroups = clashes
                .GroupBy(c => GetSpatialRegion(c))
                .Where(g => g.Count() >= 3) // Minimum 3 clashes for hotspot
                .OrderByDescending(g => g.Count());

            foreach (var group in spatialGroups.Take(10))
            {
                hotspots.Add(new CoordinationHotspot
                {
                    Region = group.Key,
                    ClashCount = group.Count(),
                    Clashes = group.ToList(),
                    Severity = group.Max(c => c.Severity),
                    PrimaryIssue = IdentifyPrimaryIssue(group.ToList()),
                    RecommendedAction = GenerateHotspotRecommendation(group.ToList())
                });
            }

            return hotspots;
        }

        private string GetSpatialRegion(Clash clash)
        {
            return $"{clash.Level} / {clash.GridIntersection ?? "Zone " + clash.ZoneId}";
        }

        private string IdentifyPrimaryIssue(List<Clash> clashes)
        {
            var mostCommonPair = clashes
                .GroupBy(c => GetDisciplinePairKey(c))
                .OrderByDescending(g => g.Count())
                .First();

            return $"{mostCommonPair.Key} coordination ({mostCommonPair.Count()} clashes)";
        }

        private string GenerateHotspotRecommendation(List<Clash> clashes)
        {
            // Analyze pattern and recommend
            if (clashes.All(c => c.InvolvesDiscipline(Discipline.Mechanical) &&
                               c.InvolvesDiscipline(Discipline.Electrical)))
            {
                return "Schedule MEP coordination meeting to resolve duct/cable tray conflicts";
            }

            if (clashes.All(c => c.InvolvesDiscipline(Discipline.Structural)))
            {
                return "Request structural engineer review for penetration locations";
            }

            if (clashes.Any(c => c.Type == ClashType.ClearanceViolation))
            {
                return "Review ceiling height and service zone allocation";
            }

            return "Conduct focused coordination session for this area";
        }

        private EffortEstimate EstimateResolutionEffort(CoordinationReport report)
        {
            var estimate = new EffortEstimate();

            // Estimate hours based on clash types and counts
            foreach (var clash in report.ClashResults)
            {
                var hours = clash.Severity switch
                {
                    ClashSeverity.Critical => 2.0,
                    ClashSeverity.Major => 1.0,
                    ClashSeverity.Minor => 0.25,
                    ClashSeverity.Warning => 0.1,
                    _ => 0.5
                };

                estimate.TotalHours += hours;

                // Add to discipline-specific effort
                estimate.ByDiscipline[clash.PrimaryDiscipline] =
                    estimate.ByDiscipline.GetValueOrDefault(clash.PrimaryDiscipline, 0) + hours / 2;
                estimate.ByDiscipline[clash.SecondaryDiscipline] =
                    estimate.ByDiscipline.GetValueOrDefault(clash.SecondaryDiscipline, 0) + hours / 2;
            }

            // Add rule violation effort
            estimate.TotalHours += report.RuleViolations.Count * 0.5;

            // Add clearance issue effort
            estimate.TotalHours += report.ClearanceIssues.Count * 0.25;

            // Estimate cost (assume $100/hour average)
            estimate.EstimatedCost = (decimal)estimate.TotalHours * 100;

            return estimate;
        }

        private List<DisciplineReport> GenerateDisciplineReports(
            CoordinationReport report,
            MultiDisciplineModel model)
        {
            var reports = new List<DisciplineReport>();

            var disciplines = report.ClashResults
                .SelectMany(c => new[] { c.PrimaryDiscipline, c.SecondaryDiscipline })
                .Distinct();

            foreach (var discipline in disciplines)
            {
                var clashes = report.ClashResults
                    .Where(c => c.InvolvesDiscipline(discipline))
                    .ToList();

                reports.Add(new DisciplineReport
                {
                    Discipline = discipline,
                    TotalClashes = clashes.Count,
                    ClashesByPriority = clashes.GroupBy(c => c.Severity)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TopIssues = clashes
                        .GroupBy(c => GetOtherDiscipline(c, discipline))
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => $"{g.Count()} clashes with {g.Key}")
                        .ToList(),
                    ActionItems = GenerateDisciplineActionItems(clashes, discipline)
                });
            }

            return reports;
        }

        private Discipline GetOtherDiscipline(Clash clash, Discipline thisDiscipline)
        {
            return clash.PrimaryDiscipline == thisDiscipline
                ? clash.SecondaryDiscipline
                : clash.PrimaryDiscipline;
        }

        private List<string> GenerateDisciplineActionItems(List<Clash> clashes, Discipline discipline)
        {
            var actions = new List<string>();

            // Critical clashes
            var criticalCount = clashes.Count(c => c.Severity == ClashSeverity.Critical);
            if (criticalCount > 0)
            {
                actions.Add($"URGENT: Resolve {criticalCount} critical clashes");
            }

            // Structural clashes need engineer review
            if (discipline != Discipline.Structural &&
                clashes.Any(c => c.InvolvesDiscipline(Discipline.Structural)))
            {
                actions.Add("Coordinate penetration locations with structural engineer");
            }

            // Multiple clashes in same area
            var hotspotClashes = clashes
                .GroupBy(c => c.Level)
                .Where(g => g.Count() > 5)
                .ToList();

            foreach (var hotspot in hotspotClashes)
            {
                actions.Add($"Review routing on {hotspot.Key} - {hotspot.Count()} clashes");
            }

            return actions;
        }

        private Dictionary<string, DisciplineProfile> LoadDisciplineProfiles()
        {
            return new Dictionary<string, DisciplineProfile>
            {
                { "Structural", new DisciplineProfile
                    {
                        Discipline = Discipline.Structural,
                        Priority = 1,
                        TypicalElements = new[] { "Columns", "Beams", "Slabs", "Foundations", "Walls (Structural)" },
                        CoordinationNotes = "Structural elements have highest priority - others must route around"
                    }
                },
                { "Architectural", new DisciplineProfile
                    {
                        Discipline = Discipline.Architectural,
                        Priority = 2,
                        TypicalElements = new[] { "Walls", "Doors", "Windows", "Ceilings", "Floors" },
                        CoordinationNotes = "Architectural defines spaces - MEP routes through service zones"
                    }
                },
                { "Mechanical", new DisciplineProfile
                    {
                        Discipline = Discipline.Mechanical,
                        Priority = 3,
                        TypicalElements = new[] { "Ducts", "AHUs", "FCUs", "Chillers", "Diffusers" },
                        CoordinationNotes = "Largest MEP elements - coordinate early for ceiling zones"
                    }
                },
                { "Plumbing", new DisciplineProfile
                    {
                        Discipline = Discipline.Plumbing,
                        Priority = 4,
                        TypicalElements = new[] { "Pipes", "Fixtures", "Equipment", "Tanks" },
                        CoordinationNotes = "Gravity drainage is inflexible - coordinate slopes early"
                    }
                },
                { "Electrical", new DisciplineProfile
                    {
                        Discipline = Discipline.Electrical,
                        Priority = 5,
                        TypicalElements = new[] { "Cable Trays", "Conduits", "Panels", "Lights" },
                        CoordinationNotes = "Most flexible routing - typically routes around others"
                    }
                },
                { "FireProtection", new DisciplineProfile
                    {
                        Discipline = Discipline.FireProtection,
                        Priority = 3,
                        TypicalElements = new[] { "Sprinkler Pipes", "Sprinkler Heads", "Standpipes" },
                        CoordinationNotes = "Sprinkler heads have coverage requirements - coordinate ceiling layout"
                    }
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class ClashDetectionEngine
    {
        public async Task<List<Clash>> DetectAllClashesAsync(
            MultiDisciplineModel model,
            CoordinationOptions options)
        {
            await Task.Delay(10);

            var clashes = new List<Clash>();

            // In real implementation, would use spatial indexing and BVH trees
            // For demonstration, return empty list

            return clashes;
        }
    }

    public class ConflictResolver
    {
        public List<ResolutionOption> GetPossibleResolutions(Clash clash)
        {
            var options = new List<ResolutionOption>
            {
                new ResolutionOption { Type = ResolutionType.RouteModification },
                new ResolutionOption { Type = ResolutionType.ElevationChange }
            };

            if (clash.CanReduceSize)
                options.Add(new ResolutionOption { Type = ResolutionType.SizeReduction });

            return options;
        }

        public async Task<Resolution> ResolveAsync(
            Clash clash,
            MultiDisciplineModel model,
            AutoResolutionOptions options)
        {
            await Task.Delay(10);

            return new Resolution
            {
                Success = true,
                Type = ResolutionType.ElevationChange,
                Description = "Adjusted elevation to clear conflict"
            };
        }
    }

    public class CoordinationRules
    {
        public async Task<List<RuleViolation>> CheckAllRulesAsync(MultiDisciplineModel model)
        {
            await Task.Delay(10);
            return new List<RuleViolation>();
        }
    }

    public class PriorityManager
    {
        public List<string> CalculatePriorities(ResolutionPlan plan)
        {
            return plan.Groups
                .SelectMany(g => g.Clashes)
                .OrderByDescending(c => c.Severity)
                .Select(c => c.ClashId)
                .ToList();
        }
    }

    public class ClearanceChecker
    {
        public async Task<List<ClearanceIssue>> CheckClearancesAsync(MultiDisciplineModel model)
        {
            await Task.Delay(10);
            return new List<ClearanceIssue>();
        }
    }

    #endregion

    #region Data Models

    public class MultiDisciplineModel
    {
        public string ModelId { get; set; }
        public Dictionary<Discipline, List<Element>> ElementsByDiscipline { get; set; }
        public List<Level> Levels { get; set; }
        public List<Grid> Grids { get; set; }
    }

    public class Element
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public Discipline Discipline { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public string Level { get; set; }
    }

    public class BoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }
    }

    public class Level
    {
        public string LevelId { get; set; }
        public string Name { get; set; }
        public double Elevation { get; set; }
    }

    public class Grid
    {
        public string GridId { get; set; }
        public string Name { get; set; }
    }

    public class CoordinationOptions
    {
        public double ClashTolerance { get; set; } = 25; // mm
        public bool IncludeClearanceChecks { get; set; } = true;
        public bool IncludeSoftClashes { get; set; } = true;
        public List<Discipline> DisciplinesToCheck { get; set; }

        public static CoordinationOptions Default => new CoordinationOptions
        {
            DisciplinesToCheck = Enum.GetValues(typeof(Discipline)).Cast<Discipline>().ToList()
        };
    }

    public class AutoResolutionOptions
    {
        public double MaxAutoResolvePenetration { get; set; } = 100; // mm
        public List<ResolutionType> AllowedResolutionTypes { get; set; }
        public bool RequireApproval { get; set; } = false;

        public static AutoResolutionOptions Default => new AutoResolutionOptions
        {
            AllowedResolutionTypes = new List<ResolutionType>
            {
                ResolutionType.ElevationChange,
                ResolutionType.RouteModification
            }
        };
    }

    public class CoordinationReport
    {
        public string ModelId { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public List<Clash> ClashResults { get; set; } = new List<Clash>();
        public ClashSummary ClashSummary { get; set; }
        public List<RuleViolation> RuleViolations { get; set; } = new List<RuleViolation>();
        public List<ClearanceIssue> ClearanceIssues { get; set; } = new List<ClearanceIssue>();
        public List<CoordinationHotspot> Hotspots { get; set; } = new List<CoordinationHotspot>();
        public ResolutionPlan ResolutionPlan { get; set; }
        public EffortEstimate EffortEstimate { get; set; }
        public List<DisciplineReport> DisciplineReports { get; set; } = new List<DisciplineReport>();
    }

    public class Clash
    {
        public string ClashId { get; set; }
        public string Element1Id { get; set; }
        public string Element2Id { get; set; }
        public Discipline PrimaryDiscipline { get; set; }
        public Discipline SecondaryDiscipline { get; set; }
        public ClashType Type { get; set; }
        public ClashSeverity Severity { get; set; }
        public double PenetrationDepth { get; set; }
        public Point3D Location { get; set; }
        public string Level { get; set; }
        public string GridIntersection { get; set; }
        public string ZoneId { get; set; }
        public double AvailableClearanceAbove { get; set; }
        public double AvailableClearanceBelow { get; set; }
        public double SuggestedOffset { get; set; }
        public bool CanReduceSize { get; set; }
        public bool StructuralPenetrationAllowed { get; set; }

        public bool InvolvesDiscipline(Discipline discipline) =>
            PrimaryDiscipline == discipline || SecondaryDiscipline == discipline;
    }

    public class ClashSummary
    {
        public int TotalClashes { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
        public Dictionary<ClashType, int> ByType { get; set; }
        public Dictionary<string, int> ByDisciplinePair { get; set; }
        public Dictionary<string, int> ByLevel { get; set; }
    }

    public class RuleViolation
    {
        public string RuleId { get; set; }
        public string RuleType { get; set; }
        public string Description { get; set; }
        public string RequiredValue { get; set; }
        public string ActualValue { get; set; }
        public List<string> AffectedElements { get; set; }
    }

    public class ClearanceIssue
    {
        public string ElementId { get; set; }
        public string RequiredClearance { get; set; }
        public string ActualClearance { get; set; }
        public string Direction { get; set; }
    }

    public class CoordinationHotspot
    {
        public string Region { get; set; }
        public int ClashCount { get; set; }
        public List<Clash> Clashes { get; set; }
        public ClashSeverity Severity { get; set; }
        public string PrimaryIssue { get; set; }
        public string RecommendedAction { get; set; }
    }

    public class ResolutionPlan
    {
        public DateTime GeneratedAt { get; set; }
        public List<ResolutionGroup> Groups { get; set; } = new List<ResolutionGroup>();
        public List<RuleResolution> RuleResolutions { get; set; } = new List<RuleResolution>();
        public List<string> PriorityOrder { get; set; } = new List<string>();
    }

    public class ResolutionGroup
    {
        public string Location { get; set; }
        public List<Clash> Clashes { get; set; }
        public List<Clash> ResolutionOrder { get; set; }
        public Dictionary<string, List<ResolutionSuggestion>> Suggestions { get; set; } =
            new Dictionary<string, List<ResolutionSuggestion>>();
    }

    public class ResolutionSuggestion
    {
        public ResolutionType Type { get; set; }
        public string Description { get; set; }
        public string AffectedElement { get; set; }
        public double Confidence { get; set; }
        public TimeSpan EstimatedEffort { get; set; }
        public bool RequiresCalculationCheck { get; set; }
        public bool RequiresEngineerApproval { get; set; }
    }

    public class RuleResolution
    {
        public RuleViolation Violation { get; set; }
        public string SuggestedFix { get; set; }
        public List<string> AffectedElements { get; set; }
    }

    public class ResolutionOption
    {
        public ResolutionType Type { get; set; }
    }

    public class Resolution
    {
        public bool Success { get; set; }
        public ResolutionType Type { get; set; }
        public string Description { get; set; }
        public string FailureReason { get; set; }
    }

    public class AutoResolutionResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalClashes { get; set; }
        public List<ResolvedClash> ResolvedClashes { get; set; } = new List<ResolvedClash>();
        public List<FailedResolution> FailedResolutions { get; set; } = new List<FailedResolution>();
        public List<Clash> ManualReviewRequired { get; set; } = new List<Clash>();
        public double SuccessRate { get; set; }
    }

    public class ResolvedClash
    {
        public Clash OriginalClash { get; set; }
        public Resolution Resolution { get; set; }
        public DateTime ResolvedAt { get; set; }
    }

    public class FailedResolution
    {
        public Clash Clash { get; set; }
        public string Reason { get; set; }
        public bool RequiresManualReview { get; set; }
    }

    public class CoordinationDelta
    {
        public DateTime CheckedAt { get; set; }
        public DateTime PreviousReportDate { get; set; }
        public List<Clash> NewClashes { get; set; } = new List<Clash>();
        public List<Clash> ResolvedClashes { get; set; } = new List<Clash>();
        public List<Clash> RemainingClashes { get; set; } = new List<Clash>();
        public List<SeverityChange> SeverityChanges { get; set; } = new List<SeverityChange>();
        public int NetChange { get; set; }
    }

    public class SeverityChange
    {
        public string ClashId { get; set; }
        public ClashSeverity PreviousSeverity { get; set; }
        public ClashSeverity CurrentSeverity { get; set; }
    }

    public class EffortEstimate
    {
        public double TotalHours { get; set; }
        public Dictionary<Discipline, double> ByDiscipline { get; set; } = new Dictionary<Discipline, double>();
        public decimal EstimatedCost { get; set; }
    }

    public class DisciplineReport
    {
        public Discipline Discipline { get; set; }
        public int TotalClashes { get; set; }
        public Dictionary<ClashSeverity, int> ClashesByPriority { get; set; }
        public List<string> TopIssues { get; set; }
        public List<string> ActionItems { get; set; }
    }

    public class DisciplineProfile
    {
        public Discipline Discipline { get; set; }
        public int Priority { get; set; }
        public string[] TypicalElements { get; set; }
        public string CoordinationNotes { get; set; }
    }

    public enum Discipline
    {
        Architectural,
        Structural,
        Mechanical,
        Electrical,
        Plumbing,
        FireProtection,
        Civil
    }

    public enum ClashType
    {
        HardClash,
        SoftClash,
        ClearanceViolation,
        Workflow
    }

    public enum ClashSeverity
    {
        Warning,
        Minor,
        Major,
        Critical
    }

    public enum ResolutionType
    {
        RouteModification,
        ElevationChange,
        SizeReduction,
        StructuralPenetration,
        DesignChange,
        Accepted
    }

    #endregion
}
