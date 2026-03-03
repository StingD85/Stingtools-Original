// StingBIM.AI.Agents.Specialists.CostAgent
// Specialist agent for cost estimation and value engineering
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (COST Agent)

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
    /// Cost estimation and value engineering specialist agent.
    /// Evaluates construction costs, material efficiency, and value engineering opportunities.
    /// </summary>
    public class CostAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // Unit costs (USD per unit, 2025 estimates - adjustable per region)
        private static readonly Dictionary<string, CostRate> MaterialCosts = new()
        {
            // Structural elements
            ["concrete_wall"] = new CostRate { UnitCost = 180, Unit = "m²", Description = "Concrete wall with formwork" },
            ["masonry_wall"] = new CostRate { UnitCost = 120, Unit = "m²", Description = "CMU/brick masonry wall" },
            ["drywall"] = new CostRate { UnitCost = 45, Unit = "m²", Description = "Metal stud with drywall" },
            ["concrete_slab"] = new CostRate { UnitCost = 95, Unit = "m²", Description = "Reinforced concrete slab" },
            ["steel_beam"] = new CostRate { UnitCost = 350, Unit = "m", Description = "Steel beam installed" },
            ["concrete_column"] = new CostRate { UnitCost = 800, Unit = "each", Description = "Concrete column" },

            // Openings
            ["standard_door"] = new CostRate { UnitCost = 450, Unit = "each", Description = "Standard interior door" },
            ["exterior_door"] = new CostRate { UnitCost = 1200, Unit = "each", Description = "Exterior door with frame" },
            ["window"] = new CostRate { UnitCost = 350, Unit = "m²", Description = "Standard window" },
            ["curtain_wall"] = new CostRate { UnitCost = 800, Unit = "m²", Description = "Curtain wall system" },

            // Finishes
            ["floor_tile"] = new CostRate { UnitCost = 65, Unit = "m²", Description = "Ceramic floor tile" },
            ["carpet"] = new CostRate { UnitCost = 35, Unit = "m²", Description = "Commercial carpet" },
            ["paint"] = new CostRate { UnitCost = 12, Unit = "m²", Description = "Interior paint (2 coats)" },
            ["ceiling_tile"] = new CostRate { UnitCost = 40, Unit = "m²", Description = "Suspended ceiling" },

            // MEP
            ["hvac_duct"] = new CostRate { UnitCost = 85, Unit = "m", Description = "HVAC ductwork" },
            ["electrical_outlet"] = new CostRate { UnitCost = 75, Unit = "each", Description = "Electrical outlet" },
            ["plumbing_fixture"] = new CostRate { UnitCost = 600, Unit = "each", Description = "Standard plumbing fixture" }
        };

        // Regional cost multipliers
        private static readonly Dictionary<string, float> RegionalMultipliers = new()
        {
            ["us_northeast"] = 1.2f,
            ["us_southeast"] = 0.9f,
            ["us_midwest"] = 0.85f,
            ["us_west"] = 1.15f,
            ["uk"] = 1.1f,
            ["europe_west"] = 1.25f,
            ["east_africa"] = 0.6f,
            ["west_africa"] = 0.65f,
            ["southern_africa"] = 0.7f,
            ["middle_east"] = 1.0f,
            ["asia_pacific"] = 0.75f
        };

        // Design efficiency thresholds
        private const double TargetNetToGrossRatio = 0.85; // 85% efficiency
        private const double AcceptableNetToGrossRatio = 0.75;
        private const double MaxWallToFloorRatio = 0.5; // m² wall per m² floor
        private const double EconomicColumnSpacing = 7.5; // meters

        public string AgentId => "COST-001";
        public string Specialty => "Cost Engineering";
        public float ExpertiseLevel => 0.85f;
        public bool IsActive => true;

        public CostAgent()
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
                Logger.Debug($"Cost evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Estimate costs
                var costEstimate = EstimateCosts(proposal, context);
                opinion.AspectScores["CostEstimate"] = 0.9f;

                // Evaluate design efficiency
                EvaluateDesignEfficiency(proposal, issues, strengths, opinion.AspectScores);

                // Identify value engineering opportunities
                EvaluateValueEngineering(proposal, issues, strengths, opinion.AspectScores);

                // Check material optimization
                EvaluateMaterialOptimization(proposal, issues, strengths, opinion.AspectScores);

                // Store cost estimate in details
                opinion.AspectScores["EstimatedCost"] = costEstimate.TotalCost;

                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                if (_receivedFeedback.Any())
                {
                    AdjustForOtherDisciplines(opinion);
                }

                opinion.Summary = GenerateCostSummary(opinion, costEstimate);
                Logger.Debug($"Cost score: {opinion.Score:F2} (Est: ${costEstimate.TotalCost:N0})");

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

                // Wall-related cost suggestions
                if (context.CurrentTask?.Contains("wall", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Consider wall type for cost",
                        Description = "Drywall ($45/m²) vs masonry ($120/m²) vs concrete ($180/m²). Choose based on structural and acoustic needs.",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.9f,
                        Impact = 0.7f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["DrywallCost"] = MaterialCosts["drywall"].UnitCost,
                            ["MasonryCost"] = MaterialCosts["masonry_wall"].UnitCost,
                            ["ConcreteCost"] = MaterialCosts["concrete_wall"].UnitCost
                        }
                    });
                }

                // Layout efficiency suggestions
                if (context.CurrentTask?.Contains("room", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("layout", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Optimize room proportions",
                        Description = "Square or near-square rooms minimize wall perimeter per floor area, reducing costs",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.85f,
                        Impact = 0.5f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Stack wet areas vertically",
                        Description = "Aligning bathrooms/kitchens vertically reduces plumbing costs by 15-25%",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.88f,
                        Impact = 0.65f
                    });
                }

                // Structural grid suggestions
                if (context.CurrentTask?.Contains("column", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("grid", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Economic column spacing",
                        Description = $"Column spacing of {EconomicColumnSpacing}m typically optimizes structural costs",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.82f,
                        Impact = 0.6f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Regular structural grid",
                        Description = "A regular grid reduces formwork reuse costs and simplifies construction",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.9f,
                        Impact = 0.55f
                    });
                }

                // General cost suggestions
                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "Standard dimensions reduce costs",
                    Description = "Using standard material sizes (1.2m drywall, 600mm tiles) minimizes waste",
                    Type = SuggestionType.CostSaving,
                    Confidence = 0.85f,
                    Impact = 0.4f
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
            // Cost agent doesn't typically invalidate actions, but provides warnings
            var result = new ValidationResult { IsValid = true, Warnings = new List<string>() };

            switch (action.ActionType.ToLowerInvariant())
            {
                case "createwall":
                    var wallType = action.Parameters.GetValueOrDefault("WallType")?.ToString() ?? "drywall";
                    var area = action.Parameters.GetValueOrDefault("Area") as double? ?? 0;
                    if (MaterialCosts.TryGetValue(wallType, out var rate))
                    {
                        result.Warnings.Add($"Estimated wall cost: ${rate.UnitCost * area:N0}");
                    }
                    break;

                case "createroom":
                    var roomArea = action.Parameters.GetValueOrDefault("Area") as double? ?? 0;
                    var estimatedRoomCost = roomArea * 150; // rough estimate per m²
                    result.Warnings.Add($"Estimated room finishing cost: ${estimatedRoomCost:N0}");
                    break;
            }

            return result;
        }

        private CostEstimate EstimateCosts(DesignProposal proposal, EvaluationContext context)
        {
            var estimate = new CostEstimate();
            var region = context?.ProjectParameters?.GetValueOrDefault("Region")?.ToString() ?? "us_midwest";
            var multiplier = RegionalMultipliers.GetValueOrDefault(region, 1.0f);

            foreach (var element in proposal.Elements)
            {
                var elementCost = EstimateElementCost(element);
                estimate.ElementCosts[element.ElementType] =
                    estimate.ElementCosts.GetValueOrDefault(element.ElementType, 0) + elementCost;
                estimate.TotalCost += elementCost;
            }

            // Apply regional multiplier
            estimate.TotalCost *= multiplier;
            estimate.RegionalMultiplier = multiplier;

            // Add contingency (typically 10-15%)
            estimate.Contingency = estimate.TotalCost * 0.1f;
            estimate.TotalCost += estimate.Contingency;

            return estimate;
        }

        private float EstimateElementCost(ProposedElement element)
        {
            var cost = 0f;

            switch (element.ElementType.ToLowerInvariant())
            {
                case "wall":
                    var wallArea = (element.Geometry?.Length ?? 3) * (element.Geometry?.Height ?? 2.7);
                    var wallType = element.Parameters.GetValueOrDefault("WallType")?.ToString() ?? "drywall";
                    cost = (float)(wallArea * MaterialCosts.GetValueOrDefault(wallType, MaterialCosts["drywall"]).UnitCost);
                    break;

                case "floor":
                case "slab":
                    var floorArea = (element.Geometry?.Width ?? 4) * (element.Geometry?.Length ?? 4);
                    cost = (float)(floorArea * MaterialCosts["concrete_slab"].UnitCost);
                    break;

                case "room":
                    var roomArea = (element.Geometry?.Width ?? 4) * (element.Geometry?.Length ?? 4);
                    // Room cost includes flooring, ceiling, paint
                    cost = (float)(roomArea * (MaterialCosts["floor_tile"].UnitCost +
                                               MaterialCosts["ceiling_tile"].UnitCost +
                                               MaterialCosts["paint"].UnitCost * 2)); // walls
                    break;

                case "door":
                    var isExterior = element.Parameters.GetValueOrDefault("IsExterior") as bool? ?? false;
                    cost = isExterior ? MaterialCosts["exterior_door"].UnitCost : MaterialCosts["standard_door"].UnitCost;
                    break;

                case "window":
                    var windowArea = (element.Geometry?.Width ?? 1.2) * (element.Geometry?.Height ?? 1.5);
                    cost = (float)(windowArea * MaterialCosts["window"].UnitCost);
                    break;

                case "column":
                    cost = MaterialCosts["concrete_column"].UnitCost;
                    break;

                case "beam":
                    var beamLength = element.Geometry?.Length ?? 6;
                    cost = (float)(beamLength * MaterialCosts["steel_beam"].UnitCost);
                    break;
            }

            return cost;
        }

        private void EvaluateDesignEfficiency(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Calculate net-to-gross ratio
            var rooms = proposal.Elements.Where(e =>
                e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase)).ToList();

            var netArea = rooms.Sum(r => (r.Geometry?.Width ?? 4) * (r.Geometry?.Length ?? 4));
            var grossArea = proposal.Parameters.GetValueOrDefault("GrossFloorArea") as double? ?? netArea * 1.2;
            var netToGross = netArea / grossArea;

            if (netToGross < AcceptableNetToGrossRatio)
            {
                issues.Add(new DesignIssue
                {
                    Code = "COST-EFF-001",
                    Description = $"Low space efficiency: {netToGross:P0} net-to-gross (target: {TargetNetToGrossRatio:P0})",
                    Severity = IssueSeverity.Warning,
                    SuggestedFix = "Reduce circulation area or consolidate service spaces",
                    Details = new Dictionary<string, object>
                    {
                        ["NetArea"] = netArea,
                        ["GrossArea"] = grossArea,
                        ["NetToGross"] = netToGross
                    }
                });
                scores["Efficiency"] = (float)(netToGross / TargetNetToGrossRatio);
            }
            else if (netToGross >= TargetNetToGrossRatio)
            {
                strengths.Add($"Excellent space efficiency: {netToGross:P0}");
                scores["Efficiency"] = 1.0f;
            }
            else
            {
                scores["Efficiency"] = (float)(netToGross / TargetNetToGrossRatio);
            }

            // Calculate wall-to-floor ratio
            var walls = proposal.Elements.Where(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase)).ToList();
            var totalWallArea = walls.Sum(w => (w.Geometry?.Length ?? 3) * (w.Geometry?.Height ?? 2.7));
            var wallToFloor = totalWallArea / Math.Max(netArea, 1);

            if (wallToFloor > MaxWallToFloorRatio)
            {
                issues.Add(new DesignIssue
                {
                    Code = "COST-EFF-010",
                    Description = $"High wall-to-floor ratio: {wallToFloor:F2} m²/m² (target: <{MaxWallToFloorRatio})",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Consider larger rooms or open-plan layouts to reduce wall area"
                });
            }
            else
            {
                strengths.Add("Efficient wall-to-floor ratio");
            }
        }

        private void EvaluateValueEngineering(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check for value engineering opportunities
            var opportunities = new List<string>();

            // Check for expensive wall types where cheaper would suffice
            var expensiveWalls = proposal.Elements.Where(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase) &&
                e.Parameters.GetValueOrDefault("WallType")?.ToString() == "concrete_wall" &&
                e.Parameters.GetValueOrDefault("IsLoadBearing") as bool? == false).ToList();

            if (expensiveWalls.Any())
            {
                issues.Add(new DesignIssue
                {
                    Code = "COST-VE-001",
                    Description = $"{expensiveWalls.Count} non-load-bearing walls using concrete - consider drywall",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Use drywall for non-structural partitions (saves ~$135/m²)",
                    Details = new Dictionary<string, object>
                    {
                        ["PotentialSavings"] = expensiveWalls.Sum(w =>
                            (w.Geometry?.Length ?? 3) * (w.Geometry?.Height ?? 2.7) * 135)
                    }
                });
            }

            // Check window-to-wall ratio (high ratios = high cost)
            var windows = proposal.Elements.Where(e =>
                e.ElementType.Equals("window", StringComparison.OrdinalIgnoreCase) ||
                e.ElementType.Equals("curtain_wall", StringComparison.OrdinalIgnoreCase)).ToList();

            var walls = proposal.Elements.Where(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase)).ToList();

            var windowArea = windows.Sum(w => (w.Geometry?.Width ?? 1.2) * (w.Geometry?.Height ?? 1.5));
            var facadeArea = walls.Sum(w => (w.Geometry?.Length ?? 3) * (w.Geometry?.Height ?? 2.7));
            var windowToWall = facadeArea > 0 ? windowArea / facadeArea : 0;

            if (windowToWall > 0.6)
            {
                issues.Add(new DesignIssue
                {
                    Code = "COST-VE-010",
                    Description = $"High window-to-wall ratio ({windowToWall:P0}) increases facade cost",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Consider reducing glazing area to 40-50% for cost optimization"
                });
            }

            scores["ValueEngineering"] = 1.0f - (issues.Count(i => i.Code.StartsWith("COST-VE")) * 0.1f);
        }

        private void EvaluateMaterialOptimization(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check for standard dimensions
            var nonStandardDimensions = 0;

            foreach (var element in proposal.Elements)
            {
                if (element.Geometry != null)
                {
                    // Check if dimensions align with standard module (typically 300mm or 600mm)
                    var moduleSize = 0.6; // 600mm
                    var widthRemainder = element.Geometry.Width % moduleSize;
                    var lengthRemainder = element.Geometry.Length % moduleSize;

                    if (widthRemainder > 0.05 && widthRemainder < moduleSize - 0.05)
                        nonStandardDimensions++;
                    if (lengthRemainder > 0.05 && lengthRemainder < moduleSize - 0.05)
                        nonStandardDimensions++;
                }
            }

            if (nonStandardDimensions > proposal.Elements.Count * 0.3)
            {
                issues.Add(new DesignIssue
                {
                    Code = "COST-MAT-001",
                    Description = "Many non-modular dimensions may increase material waste",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Use dimensions that align with 600mm module for standard material sizes"
                });
                scores["MaterialOptimization"] = 0.8f;
            }
            else
            {
                strengths.Add("Dimensions align well with standard material sizes");
                scores["MaterialOptimization"] = 1.0f;
            }
        }

        private void AdjustForOtherDisciplines(AgentOpinion opinion)
        {
            var structFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Structural Engineering");

            if (structFeedback != null && structFeedback.HasCriticalIssues)
            {
                opinion.Issues.Add(new DesignIssue
                {
                    Code = "COST-STRUCT-NOTE",
                    Description = "Structural requirements may increase construction costs",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Consider alternative structural systems for cost optimization"
                });
            }

            opinion.IsRevised = true;
            _receivedFeedback.Clear();
        }

        private float CalculateOverallScore(List<DesignIssue> issues, Dictionary<string, float> aspectScores)
        {
            var relevantScores = aspectScores.Where(kvp =>
                kvp.Key != "EstimatedCost" && kvp.Key != "CostEstimate").ToList();

            var baseScore = relevantScores.Count > 0 ? relevantScores.Select(kvp => kvp.Value).Average() : 1.0f;

            // Cost agent has softer penalties since it's advisory
            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Error:
                        baseScore -= 0.1f;
                        break;
                    case IssueSeverity.Warning:
                        baseScore -= 0.05f;
                        break;
                }
            }

            return Math.Max(0, Math.Min(1, baseScore));
        }

        private string GenerateCostSummary(AgentOpinion opinion, CostEstimate estimate)
        {
            var costLevel = estimate.TotalCost switch
            {
                > 1000000 => "Major project",
                > 100000 => "Significant project",
                > 10000 => "Medium project",
                _ => "Small project"
            };

            return $"{costLevel} - Est. ${estimate.TotalCost:N0} (incl. {estimate.Contingency / estimate.TotalCost:P0} contingency). " +
                   $"Score: {opinion.Score:P0} - {(opinion.Score >= 0.8f ? "Good value" : "Optimization opportunities exist")}";
        }
    }

    internal class CostRate
    {
        public float UnitCost { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
    }

    internal class CostEstimate
    {
        public float TotalCost { get; set; }
        public float Contingency { get; set; }
        public float RegionalMultiplier { get; set; }
        public Dictionary<string, float> ElementCosts { get; set; } = new();
    }
}
