// StingBIM.AI.Agents.Specialists.StructuralAgent
// Specialist agent for structural engineering evaluation
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (STRUCT Agent)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Agents.Framework;

namespace StingBIM.AI.Agents.Specialists
{
    /// <summary>
    /// Structural engineering specialist agent.
    /// Evaluates load paths, structural integrity, material strength, and code compliance.
    /// References ASCE 7, Eurocodes, and regional structural codes.
    /// </summary>
    public class StructuralAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // Structural standards and limits (SI units)
        private const double MaxWallSpanWithoutSupport = 6.0; // meters
        private const double MinLoadBearingWallThickness = 0.2; // meters
        private const double MaxFloorSpan = 7.5; // meters without intermediate support
        private const double MinColumnSpacing = 3.0; // meters
        private const double MaxColumnSpacing = 9.0; // meters
        private const double MinFoundationDepth = 0.6; // meters
        private const double SafetyFactor = 1.5;

        // Load values (kN/m²)
        private const double ResidentialFloorLoad = 2.0;
        private const double OfficeFloorLoad = 2.5;
        private const double RetailFloorLoad = 4.0;
        private const double StorageFloorLoad = 7.5;

        public string AgentId => "STRUCT-001";
        public string Specialty => "Structural Engineering";
        public float ExpertiseLevel => 0.95f;
        public bool IsActive => true;

        public StructuralAgent()
        {
            _receivedFeedback = new List<AgentOpinion>();
        }

        public async Task<AgentOpinion> EvaluateAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Debug($"Structural evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Evaluate structural elements
                foreach (var element in proposal.Elements)
                {
                    EvaluateStructuralElement(element, issues, strengths, opinion.AspectScores, context);
                }

                // Evaluate modifications for structural impact
                foreach (var modification in proposal.Modifications)
                {
                    EvaluateModificationImpact(modification, issues, strengths);
                }

                // Evaluate overall structural system
                EvaluateStructuralSystem(proposal, issues, strengths, opinion.AspectScores);

                // Check load paths
                EvaluateLoadPaths(proposal, issues, strengths, opinion.AspectScores);

                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                if (_receivedFeedback.Any())
                {
                    AdjustForArchitecturalNeeds(opinion);
                }

                opinion.Summary = GenerateSummary(opinion);
                Logger.Debug($"Structural score: {opinion.Score:F2} ({issues.Count} issues)");

                return opinion;
            }, cancellationToken);
        }

        public async Task<IEnumerable<AgentSuggestion>> SuggestAsync(
            DesignContext context,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var suggestions = new List<AgentSuggestion>();

                // Wall-related suggestions
                if (context.CurrentTask?.Contains("wall", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Consider load-bearing requirements",
                        Description = "Walls longer than 6m should be assessed for load-bearing capacity or require intermediate supports",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.9f,
                        Impact = 0.8f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["MaxSpanWithoutSupport"] = MaxWallSpanWithoutSupport,
                            ["MinThickness"] = MinLoadBearingWallThickness
                        }
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Wall-to-floor connection",
                        Description = "Ensure proper connection details between walls and floor slabs for lateral load transfer",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.85f,
                        Impact = 0.7f
                    });
                }

                // Floor/slab suggestions
                if (context.CurrentTask?.Contains("floor", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("slab", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Floor span optimization",
                        Description = $"Floor spans exceeding {MaxFloorSpan}m require deeper sections or intermediate support",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.92f,
                        Impact = 0.85f
                    });
                }

                // Column suggestions
                if (context.CurrentTask?.Contains("column", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Column grid spacing",
                        Description = $"Recommended column spacing: {MinColumnSpacing}m to {MaxColumnSpacing}m for economical design",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.88f,
                        Impact = 0.6f
                    });
                }

                // General structural suggestions
                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "Regular structural grid",
                    Description = "A regular structural grid simplifies construction and reduces costs",
                    Type = SuggestionType.BestPractice,
                    Confidence = 0.8f,
                    Impact = 0.5f
                });

                return suggestions;
            }, cancellationToken);
        }

        public void ReceiveFeedback(AgentOpinion otherOpinion)
        {
            _receivedFeedback.Add(otherOpinion);
        }

        public ValidationResult ValidateAction(DesignAction action)
        {
            switch (action.ActionType.ToLowerInvariant())
            {
                case "createwall":
                    return ValidateWallStructure(action);
                case "deletewall":
                    return ValidateWallRemoval(action);
                case "createfloor":
                    return ValidateFloorStructure(action);
                case "createcolumn":
                    return ValidateColumnPlacement(action);
                case "createopening":
                    return ValidateOpeningInStructure(action);
                default:
                    return ValidationResult.Valid();
            }
        }

        private void EvaluateStructuralElement(ProposedElement element, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            switch (element.ElementType.ToLowerInvariant())
            {
                case "wall":
                    EvaluateWall(element, issues, strengths, scores);
                    break;
                case "floor":
                case "slab":
                    EvaluateFloor(element, issues, strengths, scores, context);
                    break;
                case "column":
                    EvaluateColumn(element, issues, strengths, scores);
                    break;
                case "beam":
                    EvaluateBeam(element, issues, strengths, scores);
                    break;
            }
        }

        private void EvaluateWall(ProposedElement wall, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            var isLoadBearing = wall.Parameters.GetValueOrDefault("IsLoadBearing") as bool? ?? false;
            var thickness = wall.Geometry?.Width ?? 0.15;
            var length = wall.Geometry?.Length ?? 0;
            var height = wall.Geometry?.Height ?? 2.7;

            // Check thickness for load-bearing walls
            if (isLoadBearing && thickness < MinLoadBearingWallThickness)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-001",
                    Description = $"Load-bearing wall thickness ({thickness:F2}m) below minimum ({MinLoadBearingWallThickness}m)",
                    Severity = IssueSeverity.Critical,
                    Standard = "Structural Code - Masonry Walls",
                    SuggestedFix = $"Increase wall thickness to at least {MinLoadBearingWallThickness}m"
                });
                scores["WallThickness"] = 0.3f;
            }
            else
            {
                scores["WallThickness"] = 1.0f;
            }

            // Check slenderness ratio
            var slendernessRatio = height / thickness;
            if (slendernessRatio > 20 && isLoadBearing)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-002",
                    Description = $"Wall slenderness ratio ({slendernessRatio:F1}) exceeds recommended limit (20)",
                    Severity = IssueSeverity.Warning,
                    Standard = "Structural Code - Wall Stability",
                    SuggestedFix = "Increase wall thickness or reduce unsupported height"
                });
                scores["WallSlenderness"] = 0.6f;
            }
            else
            {
                scores["WallSlenderness"] = 1.0f;
            }

            // Check span
            if (length > MaxWallSpanWithoutSupport && isLoadBearing)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-003",
                    Description = $"Wall span ({length:F1}m) exceeds maximum ({MaxWallSpanWithoutSupport}m) without lateral support",
                    Severity = IssueSeverity.Warning,
                    Standard = "Structural Code - Wall Bracing",
                    SuggestedFix = "Add perpendicular walls or piers for lateral support"
                });
                scores["WallSpan"] = 0.7f;
            }
            else
            {
                scores["WallSpan"] = 1.0f;
                if (isLoadBearing)
                    strengths.Add("Wall span within acceptable limits");
            }
        }

        private void EvaluateFloor(ProposedElement floor, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            var span = Math.Max(floor.Geometry?.Width ?? 0, floor.Geometry?.Length ?? 0);
            var thickness = floor.Geometry?.Height ?? 0.15;
            var projectType = context?.ProjectType ?? "residential";

            // Determine design load
            var designLoad = projectType.ToLowerInvariant() switch
            {
                "office" => OfficeFloorLoad,
                "retail" => RetailFloorLoad,
                "storage" => StorageFloorLoad,
                _ => ResidentialFloorLoad
            };

            // Check span-to-depth ratio
            var spanDepthRatio = span / thickness;
            var maxSpanDepthRatio = 30; // typical for concrete slabs

            if (spanDepthRatio > maxSpanDepthRatio)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-010",
                    Description = $"Floor span-to-depth ratio ({spanDepthRatio:F1}) exceeds limit ({maxSpanDepthRatio})",
                    Severity = IssueSeverity.Error,
                    Standard = "Structural Code - Slab Design",
                    SuggestedFix = "Increase slab thickness or reduce span",
                    Details = new Dictionary<string, object>
                    {
                        ["Span"] = span,
                        ["Thickness"] = thickness,
                        ["SuggestedThickness"] = span / maxSpanDepthRatio
                    }
                });
                scores["FloorSpan"] = 0.5f;
            }
            else
            {
                scores["FloorSpan"] = 1.0f - (float)(spanDepthRatio / (maxSpanDepthRatio * 1.5));
            }

            // Check maximum span
            if (span > MaxFloorSpan)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-011",
                    Description = $"Floor span ({span:F1}m) exceeds typical maximum ({MaxFloorSpan}m)",
                    Severity = IssueSeverity.Warning,
                    SuggestedFix = "Consider adding beams or intermediate supports"
                });
            }
            else
            {
                strengths.Add($"Floor span within structural limits");
            }

            // Store design load info
            scores["FloorLoad"] = 0.9f;
        }

        private void EvaluateColumn(ProposedElement column, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            var width = column.Geometry?.Width ?? 0.3;
            var height = column.Geometry?.Height ?? 3.0;
            var slenderness = height / width;

            // Check slenderness
            if (slenderness > 15)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-020",
                    Description = $"Column slenderness ({slenderness:F1}) may require buckling analysis",
                    Severity = slenderness > 25 ? IssueSeverity.Error : IssueSeverity.Warning,
                    Standard = "Structural Code - Column Design",
                    SuggestedFix = "Increase column size or add lateral bracing"
                });
                scores["ColumnSlenderness"] = slenderness > 25 ? 0.4f : 0.7f;
            }
            else
            {
                scores["ColumnSlenderness"] = 1.0f;
                strengths.Add("Column proportions adequate for stability");
            }
        }

        private void EvaluateBeam(ProposedElement beam, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            var span = beam.Geometry?.Length ?? 0;
            var depth = beam.Geometry?.Height ?? 0.3;
            var spanDepthRatio = span / depth;

            // Check span-to-depth ratio (typical: span/12 to span/18)
            if (spanDepthRatio > 20)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-030",
                    Description = $"Beam span-to-depth ratio ({spanDepthRatio:F1}) may cause excessive deflection",
                    Severity = IssueSeverity.Warning,
                    SuggestedFix = $"Recommended beam depth: {span / 15:F2}m to {span / 12:F2}m"
                });
                scores["BeamDesign"] = 0.7f;
            }
            else
            {
                scores["BeamDesign"] = 1.0f;
                strengths.Add("Beam proportions adequate for span");
            }
        }

        private void EvaluateModificationImpact(ProposedModification modification,
            List<DesignIssue> issues, List<string> strengths)
        {
            // Check if modification affects load-bearing elements
            if (modification.ModificationType.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                var isStructural = modification.OldValues.GetValueOrDefault("IsLoadBearing") as bool? ?? false;
                if (isStructural)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "STRUCT-100",
                        Description = "Removing load-bearing element requires structural analysis",
                        Severity = IssueSeverity.Critical,
                        Location = modification.ElementId,
                        SuggestedFix = "Provide alternative load path before removal"
                    });
                }
            }
        }

        private void EvaluateStructuralSystem(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Evaluate overall structural regularity
            var walls = proposal.Elements.Where(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase)).ToList();
            var columns = proposal.Elements.Where(e =>
                e.ElementType.Equals("column", StringComparison.OrdinalIgnoreCase)).ToList();
            var beams = proposal.Elements.Where(e =>
                e.ElementType.Equals("beam", StringComparison.OrdinalIgnoreCase)).ToList();

            if (walls.Count + columns.Count == 0)
            {
                scores["StructuralSystem"] = 0.5f;
                return;
            }

            var systemScore = 1.0f;

            // --- Check structural grid regularity (columns should have consistent spacing) ---
            if (columns.Count >= 2)
            {
                var xPositions = columns.Select(c => c.Geometry?.X ?? 0).OrderBy(x => x).ToList();
                var yPositions = columns.Select(c => c.Geometry?.Y ?? 0).OrderBy(y => y).ToList();

                // Compute spacings in X direction
                var xSpacings = new List<double>();
                for (int i = 1; i < xPositions.Count; i++)
                {
                    var spacing = xPositions[i] - xPositions[i - 1];
                    if (spacing > 0.5) // ignore nearly coincident columns
                        xSpacings.Add(spacing);
                }

                // Check regularity: coefficient of variation of spacings
                if (xSpacings.Count >= 2)
                {
                    var avgSpacing = xSpacings.Average();
                    var stdDev = Math.Sqrt(xSpacings.Sum(s => Math.Pow(s - avgSpacing, 2)) / xSpacings.Count);
                    var coeffOfVariation = avgSpacing > 0 ? stdDev / avgSpacing : 0;

                    if (coeffOfVariation > 0.3)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "STRUCT-SYS-001",
                            Description = $"Irregular column grid spacing (variation coefficient: {coeffOfVariation:F2}). Regular grids are more economical.",
                            Severity = IssueSeverity.Warning,
                            Standard = "Structural Engineering Best Practice",
                            SuggestedFix = "Regularize column spacing to a consistent grid module"
                        });
                        systemScore -= 0.15f;
                    }
                    else
                    {
                        strengths.Add($"Regular structural column grid (avg spacing: {avgSpacing:F1}m)");
                    }

                    // Check if any spacing exceeds maximum
                    if (xSpacings.Any(s => s > MaxColumnSpacing))
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "STRUCT-SYS-002",
                            Description = $"Column spacing exceeds {MaxColumnSpacing}m in some bays; longer spans increase cost",
                            Severity = IssueSeverity.Info,
                            SuggestedFix = $"Keep column spacing within {MinColumnSpacing}m to {MaxColumnSpacing}m for economy"
                        });
                        systemScore -= 0.05f;
                    }
                }
            }

            // --- Evaluate mix of walls and columns (hybrid system assessment) ---
            var loadBearingWalls = walls.Where(w =>
                w.Parameters.GetValueOrDefault("IsLoadBearing") as bool? ?? false).ToList();

            if (loadBearingWalls.Count > 0 && columns.Count > 0)
            {
                // Mixed system: can work but needs careful coordination
                var totalStructural = loadBearingWalls.Count + columns.Count;
                var wallRatio = (double)loadBearingWalls.Count / totalStructural;

                if (wallRatio > 0.2 && wallRatio < 0.8)
                {
                    strengths.Add("Hybrid wall-column system provides redundancy");
                }
                else
                {
                    strengths.Add(wallRatio >= 0.8
                        ? "Predominantly wall-based structural system"
                        : "Predominantly column-based structural system");
                }
            }
            else if (loadBearingWalls.Count > 0)
            {
                strengths.Add("Load-bearing wall structural system identified");
            }
            else if (columns.Count > 0)
            {
                strengths.Add("Column-frame structural system identified");
            }

            // --- Check for potential transfer structures (columns not aligned vertically) ---
            // Use Z position to detect multi-story; group columns by approximate X-Y to see if they stack
            if (columns.Count >= 4)
            {
                var zLevels = columns.Select(c => c.Geometry?.Z ?? 0).Distinct().OrderBy(z => z).ToList();
                if (zLevels.Count > 1)
                {
                    // Multi-level: check if each upper column has a matching lower column within tolerance
                    var lowerColumns = columns.Where(c => Math.Abs((c.Geometry?.Z ?? 0) - zLevels[0]) < 0.1).ToList();
                    var upperColumns = columns.Where(c => (c.Geometry?.Z ?? 0) > zLevels[0] + 0.5).ToList();

                    var misalignedCount = 0;
                    foreach (var upper in upperColumns)
                    {
                        var hasAligned = lowerColumns.Any(lower =>
                            Math.Abs((upper.Geometry?.X ?? 0) - (lower.Geometry?.X ?? 0)) < 0.5 &&
                            Math.Abs((upper.Geometry?.Y ?? 0) - (lower.Geometry?.Y ?? 0)) < 0.5);

                        if (!hasAligned)
                            misalignedCount++;
                    }

                    if (misalignedCount > 0)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "STRUCT-SYS-010",
                            Description = $"{misalignedCount} upper column(s) do not align with columns below, requiring transfer structures",
                            Severity = IssueSeverity.Warning,
                            Standard = "Structural Engineering - Load Path Continuity",
                            SuggestedFix = "Align columns vertically where possible to avoid costly transfer beams/slabs"
                        });
                        systemScore -= 0.1f * Math.Min(1.0f, misalignedCount / (float)upperColumns.Count);
                    }
                    else
                    {
                        strengths.Add("Columns are vertically aligned across levels");
                    }
                }
            }

            // --- Beam-to-column connectivity ---
            if (beams.Count > 0 && columns.Count == 0 && loadBearingWalls.Count == 0)
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-SYS-020",
                    Description = "Beams present without columns or load-bearing walls for support",
                    Severity = IssueSeverity.Error,
                    SuggestedFix = "Add columns or load-bearing walls to support beam ends"
                });
                systemScore -= 0.2f;
            }

            scores["StructuralSystem"] = Math.Max(0.0f, Math.Min(1.0f, (float)systemScore));
        }

        private void EvaluateLoadPaths(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Simplified load path check
            var hasVerticalElements = proposal.Elements.Any(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase) ||
                e.ElementType.Equals("column", StringComparison.OrdinalIgnoreCase));

            if (!hasVerticalElements && proposal.Elements.Any(e =>
                e.ElementType.Equals("floor", StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new DesignIssue
                {
                    Code = "STRUCT-200",
                    Description = "Floor elements require vertical support (walls or columns)",
                    Severity = IssueSeverity.Error,
                    SuggestedFix = "Add walls or columns to support floor loads"
                });
                scores["LoadPath"] = 0.3f;
            }
            else
            {
                scores["LoadPath"] = 1.0f;
            }
        }

        private void AdjustForArchitecturalNeeds(AgentOpinion opinion)
        {
            var archFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Architectural Design");
            if (archFeedback != null)
            {
                // Consider architectural concerns but maintain structural safety
                if (archFeedback.Issues.Any(i => i.Description.Contains("ceiling height")))
                {
                    opinion.Issues.Add(new DesignIssue
                    {
                        Code = "STRUCT-ARCH",
                        Description = "Structural depth may affect architectural ceiling height requirements",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Consider flat slab or post-tensioned options for reduced depth"
                    });
                }
            }

            opinion.IsRevised = true;
            _receivedFeedback.Clear();
        }

        private float CalculateOverallScore(List<DesignIssue> issues, Dictionary<string, float> aspectScores)
        {
            var baseScore = aspectScores.Count > 0 ? aspectScores.Values.Average() : 1.0f;

            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Critical:
                        baseScore -= 0.35f; // Structural critical issues are very serious
                        break;
                    case IssueSeverity.Error:
                        baseScore -= 0.2f;
                        break;
                    case IssueSeverity.Warning:
                        baseScore -= 0.08f;
                        break;
                }
            }

            return Math.Max(0, Math.Min(1, baseScore));
        }

        private string GenerateSummary(AgentOpinion opinion)
        {
            if (opinion.HasCriticalIssues)
                return "CRITICAL: Structural safety concerns must be addressed before proceeding";
            if (opinion.Score >= 0.9f)
                return "Structurally sound design with adequate load paths";
            if (opinion.Score >= 0.7f)
                return "Structure acceptable with recommended improvements";
            if (opinion.Score >= 0.5f)
                return "Structural concerns require attention";
            return "Significant structural issues need engineering review";
        }

        private ValidationResult ValidateWallStructure(DesignAction action)
        {
            var isLoadBearing = action.Parameters.GetValueOrDefault("IsLoadBearing") as bool? ?? false;
            var thickness = action.Parameters.GetValueOrDefault("Thickness") as double? ?? 0.15;

            if (isLoadBearing && thickness < MinLoadBearingWallThickness)
            {
                return ValidationResult.Invalid(
                    $"Load-bearing wall thickness must be at least {MinLoadBearingWallThickness}m",
                    IssueSeverity.Critical);
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateWallRemoval(DesignAction action)
        {
            var isLoadBearing = action.Parameters.GetValueOrDefault("IsLoadBearing") as bool? ?? false;
            if (isLoadBearing)
            {
                return ValidationResult.Invalid(
                    "Cannot remove load-bearing wall without providing alternative support",
                    IssueSeverity.Critical);
            }
            return ValidationResult.Valid();
        }

        private ValidationResult ValidateFloorStructure(DesignAction action)
        {
            var span = action.Parameters.GetValueOrDefault("Span") as double? ?? 0;
            if (span > MaxFloorSpan * 1.5)
            {
                return ValidationResult.Invalid(
                    $"Floor span ({span:F1}m) exceeds safe limits. Add intermediate support.");
            }
            return ValidationResult.Valid();
        }

        private ValidationResult ValidateColumnPlacement(DesignAction action)
        {
            return ValidationResult.Valid();
        }

        private ValidationResult ValidateOpeningInStructure(DesignAction action)
        {
            var wallIsLoadBearing = action.Parameters.GetValueOrDefault("WallIsLoadBearing") as bool? ?? false;
            var openingWidth = action.Parameters.GetValueOrDefault("Width") as double? ?? 0;

            if (wallIsLoadBearing && openingWidth > 1.2)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Warnings = new List<string>
                    {
                        "Opening in load-bearing wall requires lintel. Verify lintel design."
                    }
                };
            }

            return ValidationResult.Valid();
        }
    }
}
