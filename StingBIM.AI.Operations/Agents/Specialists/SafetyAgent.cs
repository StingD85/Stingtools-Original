// StingBIM.AI.Agents.Specialists.SafetyAgent
// Specialist agent for fire safety, accessibility, and egress evaluation
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (SAFETY Agent)

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
    /// Safety and compliance specialist agent.
    /// Evaluates fire safety, accessibility (ADA/DDA), egress routes, and life safety codes.
    /// References IBC, NFPA, ADA, and regional fire codes.
    /// </summary>
    public class SafetyAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // Fire Safety Standards (IBC/NFPA)
        private const double MaxTravelDistance = 60.0; // meters (unsprinklered)
        private const double MaxTravelDistanceSprinklered = 75.0; // meters
        private const double MaxDeadEndCorridor = 15.0; // meters
        private const double MinCorridorWidth = 1.12; // meters (44 inches)
        private const double MinStairWidth = 1.12; // meters
        private const double MinExitDoorWidth = 0.813; // meters (32 inches clear)
        private const int OccupantLoadFactorOffice = 10; // m² per person
        private const int OccupantLoadFactorRetail = 6;
        private const int OccupantLoadFactorAssembly = 1;
        private const int OccupantLoadFactorResidential = 20;

        // Accessibility Standards (ADA/EN)
        private const double MinAccessibleDoorWidth = 0.815; // meters (32 inches clear)
        private const double MinAccessibleCorridorWidth = 0.915; // meters (36 inches)
        private const double MaxDoorThreshold = 0.013; // meters (1/2 inch)
        private const double MinTurningRadius = 1.5; // meters (60 inches)
        private const double MaxRampSlope = 0.083; // 1:12
        private const double MaxCrossSlope = 0.02; // 1:50

        // Emergency Standards
        private const int MinExitsRequired = 2; // for occupant loads > 49
        private const double MaxOccupantLoadPerExit = 250;
        private const double FireExtinguisherMaxDistance = 23.0; // meters (75 feet)

        public string AgentId => "SAFETY-001";
        public string Specialty => "Safety & Compliance";
        public float ExpertiseLevel => 0.95f;
        public bool IsActive => true;

        public SafetyAgent()
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
                Logger.Debug($"Safety evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Evaluate fire safety
                EvaluateFireSafety(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate accessibility
                EvaluateAccessibility(proposal, issues, strengths, opinion.AspectScores);

                // Evaluate egress routes
                EvaluateEgress(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate general safety
                EvaluateGeneralSafety(proposal, issues, strengths, opinion.AspectScores);

                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                if (_receivedFeedback.Any())
                {
                    AdjustForOtherDisciplines(opinion);
                }

                opinion.Summary = GenerateSummary(opinion);
                Logger.Debug($"Safety score: {opinion.Score:F2} ({issues.Count} issues)");

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

                // Door-related suggestions
                if (context.CurrentTask?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Accessible door width",
                        Description = $"Doors should provide minimum {MinAccessibleDoorWidth * 1000:F0}mm clear width for accessibility",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.9f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["MinClearWidth"] = MinAccessibleDoorWidth,
                            ["Standard"] = "ADA/EN 17210"
                        }
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Exit door swing direction",
                        Description = "Exit doors must swing in the direction of egress travel",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.98f,
                        Impact = 0.8f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["Standard"] = "IBC 1010.1.2"
                        }
                    });
                }

                // Corridor suggestions
                if (context.CurrentTask?.Contains("corridor", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("hallway", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Corridor width requirements",
                        Description = $"Corridors must be minimum {MinCorridorWidth}m wide. For accessibility, {MinAccessibleCorridorWidth}m recommended.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.85f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Dead-end corridor limit",
                        Description = $"Dead-end corridors must not exceed {MaxDeadEndCorridor}m in length",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.92f,
                        Impact = 0.8f
                    });
                }

                // Room suggestions
                if (context.CurrentTask?.Contains("room", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Wheelchair turning space",
                        Description = $"Provide {MinTurningRadius}m turning radius in accessible rooms",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.9f,
                        Impact = 0.7f
                    });
                }

                // Stair suggestions
                if (context.CurrentTask?.Contains("stair", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Stair dimensions",
                        Description = "Stairs: min 1.12m width, max 180mm riser, min 280mm tread. Handrails required both sides.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.9f
                    });
                }

                // General safety suggestions
                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "Fire extinguisher placement",
                    Description = $"Fire extinguishers should be within {FireExtinguisherMaxDistance}m travel distance from any point",
                    Type = SuggestionType.CodeCompliance,
                    Confidence = 0.9f,
                    Impact = 0.6f
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
                case "createdoor":
                    return ValidateDoorSafety(action);
                case "createcorridor":
                    return ValidateCorridorSafety(action);
                case "createstair":
                    return ValidateStairSafety(action);
                case "deletewall":
                case "deletedoor":
                    return ValidateEgressImpact(action);
                default:
                    return ValidationResult.Valid();
            }
        }

        private void EvaluateFireSafety(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            var hasSprinklers = proposal.Parameters.GetValueOrDefault("HasSprinklers") as bool? ?? false;
            var maxTravel = hasSprinklers ? MaxTravelDistanceSprinklered : MaxTravelDistance;

            // Calculate occupant load
            var totalArea = proposal.Elements
                .Where(e => e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase))
                .Sum(e => (e.Geometry?.Width ?? 4) * (e.Geometry?.Length ?? 4));

            var projectType = context?.ProjectType ?? "office";
            var occupantLoadFactor = projectType.ToLowerInvariant() switch
            {
                "retail" => OccupantLoadFactorRetail,
                "assembly" => OccupantLoadFactorAssembly,
                "residential" => OccupantLoadFactorResidential,
                _ => OccupantLoadFactorOffice
            };

            var occupantLoad = (int)Math.Ceiling(totalArea / occupantLoadFactor);

            // Check number of exits required
            var exitsProvided = proposal.Elements.Count(e =>
                e.Parameters.GetValueOrDefault("IsExit") as bool? ?? false);

            var exitsRequired = occupantLoad > 49 ? MinExitsRequired : 1;
            exitsRequired = Math.Max(exitsRequired, (int)Math.Ceiling(occupantLoad / MaxOccupantLoadPerExit));

            if (exitsProvided < exitsRequired && totalArea > 100)
            {
                issues.Add(new DesignIssue
                {
                    Code = "SAFETY-FIRE-001",
                    Description = $"Insufficient exits: {exitsProvided} provided, {exitsRequired} required for occupant load {occupantLoad}",
                    Severity = IssueSeverity.Critical,
                    Standard = "IBC Chapter 10 - Means of Egress",
                    SuggestedFix = $"Provide at least {exitsRequired} exits",
                    Details = new Dictionary<string, object>
                    {
                        ["OccupantLoad"] = occupantLoad,
                        ["ExitsRequired"] = exitsRequired,
                        ["ExitsProvided"] = exitsProvided
                    }
                });
                scores["FireSafety_Exits"] = 0.3f;
            }
            else
            {
                scores["FireSafety_Exits"] = 1.0f;
                strengths.Add("Exit quantity meets requirements");
            }

            // Check exit separation (exits should be remote from each other)
            if (exitsProvided >= 2)
            {
                strengths.Add("Multiple exits provided for redundancy");
                scores["FireSafety_ExitSeparation"] = 0.9f;
            }

            // Sprinkler recommendation
            if (!hasSprinklers && totalArea > 500)
            {
                issues.Add(new DesignIssue
                {
                    Code = "SAFETY-FIRE-010",
                    Description = "Sprinkler system recommended for buildings over 500m²",
                    Severity = IssueSeverity.Info,
                    Standard = "NFPA 13",
                    SuggestedFix = "Consider installing automatic sprinkler system for increased travel distances and life safety"
                });
            }
            else if (hasSprinklers)
            {
                strengths.Add("Sprinkler system allows extended travel distances");
            }

            scores["FireSafety"] = scores.Where(kvp => kvp.Key.StartsWith("FireSafety")).Select(kvp => kvp.Value).DefaultIfEmpty(0.9f).Average();
        }

        private void EvaluateAccessibility(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check door widths
            var doors = proposal.Elements.Where(e =>
                e.ElementType.Equals("door", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var door in doors)
            {
                var width = door.Geometry?.Width ?? 0.9;
                var clearWidth = width - 0.05; // Approximate clear width

                if (clearWidth < MinAccessibleDoorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-ACCESS-001",
                        Description = $"Door clear width ({clearWidth:F2}m) below accessibility minimum ({MinAccessibleDoorWidth}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "ADA/EN 17210 - Accessibility",
                        SuggestedFix = $"Increase door width to at least {MinAccessibleDoorWidth + 0.05:F2}m"
                    });
                    scores["Accessibility_Doors"] = 0.5f;
                }
            }

            if (!scores.ContainsKey("Accessibility_Doors"))
            {
                scores["Accessibility_Doors"] = 1.0f;
                if (doors.Count > 0)
                    strengths.Add("Door widths meet accessibility requirements");
            }

            // Check corridors
            var corridors = proposal.Elements.Where(e =>
                e.ElementType.Equals("corridor", StringComparison.OrdinalIgnoreCase) ||
                e.Parameters.GetValueOrDefault("RoomType")?.ToString()?.Contains("corridor", StringComparison.OrdinalIgnoreCase) == true).ToList();

            foreach (var corridor in corridors)
            {
                var width = Math.Min(corridor.Geometry?.Width ?? 1.2, corridor.Geometry?.Length ?? 1.2);
                if (width < MinAccessibleCorridorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-ACCESS-010",
                        Description = $"Corridor width ({width:F2}m) below accessibility minimum ({MinAccessibleCorridorWidth}m)",
                        Severity = IssueSeverity.Warning,
                        Standard = "ADA/EN 17210",
                        SuggestedFix = $"Increase corridor width to at least {MinAccessibleCorridorWidth}m"
                    });
                }
            }

            // Check for level changes
            var hasStairs = proposal.Elements.Any(e =>
                e.ElementType.Equals("stair", StringComparison.OrdinalIgnoreCase));
            var hasRamp = proposal.Elements.Any(e =>
                e.ElementType.Equals("ramp", StringComparison.OrdinalIgnoreCase));
            var hasElevator = proposal.Elements.Any(e =>
                e.ElementType.Equals("elevator", StringComparison.OrdinalIgnoreCase));

            if (hasStairs && !hasRamp && !hasElevator)
            {
                issues.Add(new DesignIssue
                {
                    Code = "SAFETY-ACCESS-020",
                    Description = "Stairs present without accessible alternative (ramp or elevator)",
                    Severity = IssueSeverity.Error,
                    Standard = "ADA - Accessible Route",
                    SuggestedFix = "Provide ramp or elevator as accessible route alternative"
                });
                scores["Accessibility_Route"] = 0.4f;
            }
            else
            {
                scores["Accessibility_Route"] = 1.0f;
            }

            scores["Accessibility"] = scores.Where(kvp => kvp.Key.StartsWith("Accessibility")).Select(kvp => kvp.Value).DefaultIfEmpty(0.9f).Average();
        }

        private void EvaluateEgress(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            // Check corridor widths for egress
            var corridors = proposal.Elements.Where(e =>
                e.ElementType.Equals("corridor", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var corridor in corridors)
            {
                var width = Math.Min(corridor.Geometry?.Width ?? 1.2, corridor.Geometry?.Length ?? 1.2);
                if (width < MinCorridorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-EGRESS-001",
                        Description = $"Corridor width ({width:F2}m) below egress minimum ({MinCorridorWidth}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1005 - Egress Width",
                        SuggestedFix = $"Increase corridor width to at least {MinCorridorWidth}m"
                    });
                    scores["Egress_CorridorWidth"] = 0.4f;
                }
            }

            // Check dead-end corridors
            var deadEnds = proposal.Elements.Where(e =>
                e.Parameters.GetValueOrDefault("IsDeadEnd") as bool? ?? false).ToList();

            foreach (var deadEnd in deadEnds)
            {
                var length = Math.Max(deadEnd.Geometry?.Width ?? 0, deadEnd.Geometry?.Length ?? 0);
                if (length > MaxDeadEndCorridor)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-EGRESS-010",
                        Description = $"Dead-end corridor ({length:F1}m) exceeds maximum ({MaxDeadEndCorridor}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1020.4 - Dead Ends",
                        SuggestedFix = "Reduce dead-end length or provide additional exit"
                    });
                }
            }

            // Check exit door widths
            var exitDoors = proposal.Elements.Where(e =>
                e.ElementType.Equals("door", StringComparison.OrdinalIgnoreCase) &&
                (e.Parameters.GetValueOrDefault("IsExit") as bool? ?? false)).ToList();

            foreach (var door in exitDoors)
            {
                var width = door.Geometry?.Width ?? 0.9;
                if (width < MinExitDoorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-EGRESS-020",
                        Description = $"Exit door width ({width:F2}m) below minimum ({MinExitDoorWidth}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1010.1.1",
                        SuggestedFix = $"Increase exit door width to at least {MinExitDoorWidth}m"
                    });
                }
            }

            if (!issues.Any(i => i.Code.StartsWith("SAFETY-EGRESS")))
            {
                scores["Egress"] = 1.0f;
                strengths.Add("Egress routes meet code requirements");
            }
            else
            {
                scores["Egress"] = 0.6f;
            }
        }

        private void EvaluateGeneralSafety(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check stairs
            var stairs = proposal.Elements.Where(e =>
                e.ElementType.Equals("stair", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var stair in stairs)
            {
                var width = stair.Geometry?.Width ?? 1.0;
                if (width < MinStairWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-STAIR-001",
                        Description = $"Stair width ({width:F2}m) below minimum ({MinStairWidth}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1011 - Stairways",
                        SuggestedFix = $"Increase stair width to at least {MinStairWidth}m"
                    });
                }

                var hasHandrails = stair.Parameters.GetValueOrDefault("HasHandrails") as bool? ?? true;
                if (!hasHandrails)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-STAIR-002",
                        Description = "Stairs require handrails on both sides",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1014 - Handrails",
                        SuggestedFix = "Add handrails to both sides of stairway"
                    });
                }
            }

            scores["GeneralSafety"] = issues.Any(i => i.Code.StartsWith("SAFETY-STAIR")) ? 0.7f : 1.0f;
        }

        private void AdjustForOtherDisciplines(AgentOpinion opinion)
        {
            var archFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Architectural Design");

            if (archFeedback != null)
            {
                // Safety requirements may conflict with aesthetic preferences
                if (archFeedback.Issues.Any(i => i.Description.Contains("proportion", StringComparison.OrdinalIgnoreCase)))
                {
                    opinion.Issues.Add(new DesignIssue
                    {
                        Code = "SAFETY-NOTE",
                        Description = "Note: Safety requirements take precedence over aesthetic preferences",
                        Severity = IssueSeverity.Info
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
                        baseScore -= 0.4f; // Safety critical issues are extremely serious
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
                return "CRITICAL SAFETY VIOLATION: Must be resolved before proceeding";
            if (opinion.Score >= 0.9f)
                return "Design meets safety and accessibility requirements";
            if (opinion.Score >= 0.7f)
                return "Generally compliant with safety recommendations noted";
            if (opinion.Score >= 0.5f)
                return "Safety concerns require attention";
            return "Significant safety code violations identified";
        }

        private ValidationResult ValidateDoorSafety(DesignAction action)
        {
            var width = action.Parameters.GetValueOrDefault("Width") as double? ?? 0.9;
            var isExit = action.Parameters.GetValueOrDefault("IsExit") as bool? ?? false;

            if (isExit && width < MinExitDoorWidth)
            {
                return ValidationResult.Invalid(
                    $"Exit door width must be at least {MinExitDoorWidth}m",
                    IssueSeverity.Error);
            }

            if (width < MinAccessibleDoorWidth)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Warnings = new List<string>
                    {
                        $"Door width {width:F2}m may not meet accessibility requirements (min {MinAccessibleDoorWidth}m)"
                    }
                };
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateCorridorSafety(DesignAction action)
        {
            var width = action.Parameters.GetValueOrDefault("Width") as double? ?? 1.2;

            if (width < MinCorridorWidth)
            {
                return ValidationResult.Invalid(
                    $"Corridor width must be at least {MinCorridorWidth}m for egress",
                    IssueSeverity.Error);
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateStairSafety(DesignAction action)
        {
            var width = action.Parameters.GetValueOrDefault("Width") as double? ?? 1.0;

            if (width < MinStairWidth)
            {
                return ValidationResult.Invalid(
                    $"Stair width must be at least {MinStairWidth}m",
                    IssueSeverity.Error);
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateEgressImpact(DesignAction action)
        {
            var isPartOfEgress = action.Parameters.GetValueOrDefault("IsEgressPath") as bool? ?? false;

            if (isPartOfEgress)
            {
                return ValidationResult.Invalid(
                    "Cannot remove element that is part of required egress path",
                    IssueSeverity.Critical);
            }

            return ValidationResult.Valid();
        }
    }
}
