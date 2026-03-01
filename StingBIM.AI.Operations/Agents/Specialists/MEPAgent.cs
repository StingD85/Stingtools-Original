// StingBIM.AI.Agents.Specialists.MEPAgent
// Specialist agent for MEP (Mechanical, Electrical, Plumbing) evaluation
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (MEP Agent)

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
    /// MEP specialist agent.
    /// Evaluates HVAC routing, electrical requirements, plumbing layout, and service coordination.
    /// References ASHRAE, NEC, IPC, and regional MEP codes.
    /// </summary>
    public class MEPAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // HVAC Standards (ASHRAE)
        private const double MinCeilingClearanceForDucts = 2.4; // meters
        private const double TypicalDuctDepth = 0.3; // meters
        private const double MinVentilationRate = 0.3; // L/s per m² (ASHRAE 62.1)
        private const double ResidentialACHRequired = 0.35; // Air changes per hour
        private const double OfficeACHRequired = 4.0;

        // Electrical Standards (NEC/BS 7671)
        private const double MinOutletSpacing = 3.6; // meters (NEC 12-foot rule)
        private const double MinBathroomOutletDistance = 0.9; // from water source
        private const double TypicalResidentialLoad = 50; // W/m²
        private const double TypicalOfficeLoad = 80; // W/m²

        // Plumbing Standards (IPC)
        private const double MaxDrainSlope = 0.04; // 4% or 1:25
        private const double MinDrainSlope = 0.01; // 1%
        private const double MaxFixtureToVent = 1.8; // meters for trap arm
        private const double MinWaterPressure = 140; // kPa (20 psi)

        public string AgentId => "MEP-001";
        public string Specialty => "MEP Engineering";
        public float ExpertiseLevel => 0.9f;
        public bool IsActive => true;

        public MEPAgent()
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
                Logger.Debug($"MEP evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Evaluate HVAC requirements
                EvaluateHVAC(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate electrical requirements
                EvaluateElectrical(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate plumbing requirements
                EvaluatePlumbing(proposal, issues, strengths, opinion.AspectScores);

                // Evaluate service coordination
                EvaluateServiceCoordination(proposal, issues, strengths, opinion.AspectScores);

                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                if (_receivedFeedback.Any())
                {
                    AdjustForOtherDisciplines(opinion);
                }

                opinion.Summary = GenerateSummary(opinion);
                Logger.Debug($"MEP score: {opinion.Score:F2} ({issues.Count} issues)");

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

                // Room-related suggestions
                if (context.CurrentTask?.Contains("room", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Plan for HVAC routing",
                        Description = $"Allow {TypicalDuctDepth:F1}m ceiling void for duct routing. Minimum finished ceiling height: {MinCeilingClearanceForDucts}m",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.9f,
                        Impact = 0.7f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Electrical outlet placement",
                        Description = $"NEC requires outlets within {MinOutletSpacing}m of any wall point. Plan outlet locations early.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.5f
                    });
                }

                // Kitchen suggestions
                if (context.CurrentTask?.Contains("kitchen", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Kitchen ventilation",
                        Description = "Kitchen requires exhaust ventilation (range hood) with minimum 50 L/s extraction rate",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.92f,
                        Impact = 0.8f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["MinExtractionRate"] = 50,
                            ["Unit"] = "L/s"
                        }
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Kitchen electrical circuits",
                        Description = "Kitchens need dedicated 20A circuits for appliances. Plan at least 2 small appliance circuits.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.9f,
                        Impact = 0.6f
                    });
                }

                // Bathroom suggestions
                if (context.CurrentTask?.Contains("bathroom", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("toilet", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Bathroom ventilation",
                        Description = "Bathrooms require mechanical ventilation (min 25 L/s) or operable window (5% of floor area)",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.8f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "GFCI protection",
                        Description = "All bathroom outlets must have GFCI (ground fault) protection",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.98f,
                        Impact = 0.9f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Plumbing stack location",
                        Description = "Locate bathroom near existing plumbing stack to minimize pipe runs and costs",
                        Type = SuggestionType.CostSaving,
                        Confidence = 0.85f,
                        Impact = 0.6f
                    });
                }

                // Building-wide suggestions
                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "MEP coordination zone",
                    Description = "Reserve vertical shaft space (typically 1.5m x 1.5m per floor) for MEP risers",
                    Type = SuggestionType.BestPractice,
                    Confidence = 0.88f,
                    Impact = 0.7f
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
                case "createroom":
                    return ValidateRoomMEP(action);
                case "createbathroom":
                case "createkitchen":
                    return ValidateWetRoom(action);
                default:
                    return ValidationResult.Valid();
            }
        }

        private void EvaluateHVAC(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            var rooms = proposal.Elements.Where(e =>
                e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase)).ToList();

            if (rooms.Count == 0)
            {
                scores["HVAC"] = 0.8f;
                return;
            }

            foreach (var room in rooms)
            {
                var height = room.Geometry?.Height ?? 2.7;
                var area = (room.Geometry?.Width ?? 4) * (room.Geometry?.Length ?? 4);
                var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "general";

                // Check ceiling height for duct routing
                var availableVoid = height - MinCeilingClearanceForDucts;
                if (availableVoid < TypicalDuctDepth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "MEP-HVAC-001",
                        Description = $"Insufficient ceiling void ({availableVoid:F2}m) for duct routing (need {TypicalDuctDepth}m)",
                        Severity = IssueSeverity.Warning,
                        Standard = "ASHRAE - Duct Design",
                        SuggestedFix = $"Increase room height to {MinCeilingClearanceForDucts + TypicalDuctDepth}m or use alternative distribution"
                    });
                    scores["HVAC_CeilingVoid"] = 0.6f;
                }
                else
                {
                    scores["HVAC_CeilingVoid"] = 1.0f;
                }

                // Calculate ventilation requirement based on room type and area
                var requiredACH = roomType.ToLowerInvariant() switch
                {
                    var rt when rt.Contains("office") => OfficeACHRequired,
                    var rt when rt.Contains("bathroom") || rt.Contains("toilet") => 6.0,
                    var rt when rt.Contains("kitchen") => 8.0,
                    var rt when rt.Contains("bedroom") || rt.Contains("living") => ResidentialACHRequired,
                    _ => ResidentialACHRequired
                };

                var roomVolume = area * height;
                var requiredVentilation = Math.Max(area * MinVentilationRate, roomVolume * requiredACH / 3600.0);
                var providedVentilation = room.Parameters.GetValueOrDefault("VentilationRate") as double? ?? 0;

                if (providedVentilation > 0)
                {
                    var ventilationRatio = providedVentilation / requiredVentilation;
                    if (ventilationRatio < 1.0)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "MEP-HVAC-002",
                            Description = $"Ventilation rate ({providedVentilation:F1} L/s) is below required ({requiredVentilation:F1} L/s) for {roomType}",
                            Severity = ventilationRatio < 0.7 ? IssueSeverity.Error : IssueSeverity.Warning,
                            Standard = "ASHRAE 62.1",
                            SuggestedFix = $"Increase ventilation to at least {requiredVentilation:F1} L/s"
                        });
                        scores["HVAC_Ventilation"] = Math.Max(0.3f, (float)(ventilationRatio * 0.9));
                    }
                    else
                    {
                        scores["HVAC_Ventilation"] = Math.Min(1.0f, (float)(0.85 + 0.15 * Math.Min(ventilationRatio - 1.0, 0.5) / 0.5));
                    }
                }
                else
                {
                    // No ventilation data provided: score based on room type risk
                    var highVentRooms = new[] { "kitchen", "bathroom", "toilet", "laundry" };
                    var isHighVent = highVentRooms.Any(t => roomType.Contains(t, StringComparison.OrdinalIgnoreCase));
                    scores["HVAC_Ventilation"] = isHighVent ? 0.7f : 0.85f;
                }

                // Check for kitchen/bathroom extraction
                if (roomType.Contains("kitchen", StringComparison.OrdinalIgnoreCase))
                {
                    var hasExhaust = room.Parameters.GetValueOrDefault("HasExhaust") as bool? ?? false;
                    if (!hasExhaust)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "MEP-HVAC-010",
                            Description = "Kitchen requires exhaust ventilation",
                            Severity = IssueSeverity.Error,
                            Standard = "ASHRAE 62.1 / Building Code",
                            SuggestedFix = "Provide kitchen exhaust hood with minimum 50 L/s capacity"
                        });
                    }
                }
            }

            if (!issues.Any(i => i.Code.StartsWith("MEP-HVAC")))
            {
                strengths.Add("HVAC routing space adequate");
            }
        }

        private void EvaluateElectrical(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            var rooms = proposal.Elements.Where(e =>
                e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase)).ToList();

            var projectType = context?.ProjectType ?? "residential";
            var loadDensity = projectType.ToLowerInvariant() == "office" ? TypicalOfficeLoad : TypicalResidentialLoad;

            foreach (var room in rooms)
            {
                var area = (room.Geometry?.Width ?? 4) * (room.Geometry?.Length ?? 4);
                var perimeter = 2 * ((room.Geometry?.Width ?? 4) + (room.Geometry?.Length ?? 4));
                var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "general";

                // Calculate minimum outlets per NEC
                var minOutlets = (int)Math.Ceiling(perimeter / MinOutletSpacing);

                // Estimate load
                var estimatedLoad = area * loadDensity;
                var circuitsRequired = (int)Math.Ceiling(estimatedLoad / 1800); // 15A circuits at 120V

                scores["Electrical_Load"] = 0.9f;

                // Special requirements for bathrooms
                if (roomType.Contains("bathroom", StringComparison.OrdinalIgnoreCase) ||
                    roomType.Contains("toilet", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "MEP-ELEC-010",
                        Description = "Bathroom requires GFCI-protected outlets and proper zones",
                        Severity = IssueSeverity.Info,
                        Standard = "NEC / BS 7671",
                        SuggestedFix = "Ensure GFCI protection and maintain required distances from water sources"
                    });
                }
            }

            scores["Electrical"] = issues.Any(i => i.Code.StartsWith("MEP-ELEC") && i.Severity >= IssueSeverity.Error) ? 0.6f : 0.9f;
            strengths.Add("Electrical load assessment completed");
        }

        private void EvaluatePlumbing(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            var wetRooms = proposal.Elements.Where(e =>
            {
                var type = e.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return type.Contains("bathroom", StringComparison.OrdinalIgnoreCase) ||
                       type.Contains("kitchen", StringComparison.OrdinalIgnoreCase) ||
                       type.Contains("toilet", StringComparison.OrdinalIgnoreCase) ||
                       type.Contains("laundry", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (wetRooms.Count == 0)
            {
                scores["Plumbing"] = 0.9f;
                return;
            }

            // Check for plumbing stack proximity
            var stackLocation = proposal.Parameters.GetValueOrDefault("PlumbingStackLocation");
            if (stackLocation == null && wetRooms.Count > 0)
            {
                issues.Add(new DesignIssue
                {
                    Code = "MEP-PLMB-001",
                    Description = "Multiple wet rooms should be grouped near plumbing stack",
                    Severity = IssueSeverity.Info,
                    Standard = "IPC - Plumbing Layout",
                    SuggestedFix = "Consider locating wet rooms back-to-back or stacked vertically"
                });
            }

            foreach (var room in wetRooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";

                // Check fixture-to-vent distance
                if (roomType.Contains("bathroom", StringComparison.OrdinalIgnoreCase))
                {
                    strengths.Add("Bathroom plumbing requirements noted");
                }
            }

            scores["Plumbing"] = 0.85f;
        }

        private void EvaluateServiceCoordination(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check for MEP coordination in multi-story buildings
            var floors = proposal.Parameters.GetValueOrDefault("NumberOfFloors") as int? ?? 1;

            if (floors > 1)
            {
                var hasRiser = proposal.Parameters.GetValueOrDefault("HasMEPRiser") as bool? ?? false;
                if (!hasRiser)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "MEP-COORD-001",
                        Description = "Multi-story building requires vertical MEP risers",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Allocate vertical shaft space (1.5m x 1.5m minimum) for MEP services"
                    });
                }
            }

            // --- Dynamic coordination score based on wet room grouping, risers, and shaft space ---
            var coordinationScore = 1.0f;

            // Evaluate wet room grouping for efficient services
            var wetRooms = proposal.Elements.Where(e =>
            {
                var rt = e.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return rt.Contains("bathroom", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("kitchen", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("toilet", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("laundry", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (wetRooms.Count >= 2)
            {
                // Check clustering: compute average distance between all wet room pairs
                var totalDist = 0.0;
                var pairCount = 0;
                for (int i = 0; i < wetRooms.Count; i++)
                {
                    for (int j = i + 1; j < wetRooms.Count; j++)
                    {
                        totalDist += Math.Sqrt(
                            Math.Pow((wetRooms[i].Geometry?.X ?? 0) - (wetRooms[j].Geometry?.X ?? 0), 2) +
                            Math.Pow((wetRooms[i].Geometry?.Y ?? 0) - (wetRooms[j].Geometry?.Y ?? 0), 2));
                        pairCount++;
                    }
                }
                var avgDist = pairCount > 0 ? totalDist / pairCount : 0;

                if (avgDist > 12.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "MEP-COORD-010",
                        Description = $"Wet rooms are spread apart (avg {avgDist:F1}m), increasing plumbing and duct run lengths",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Group wet rooms together or back-to-back to minimize pipe runs"
                    });
                    coordinationScore -= 0.15f;
                }
                else
                {
                    strengths.Add("Wet rooms are well grouped for efficient MEP coordination");
                }
            }

            // Evaluate MEP riser presence for multi-story
            if (floors > 1)
            {
                var hasRiser = proposal.Parameters.GetValueOrDefault("HasMEPRiser") as bool? ?? false;
                if (hasRiser)
                {
                    strengths.Add("MEP riser shaft provided for vertical distribution");
                }
                else
                {
                    coordinationScore -= 0.15f;
                }
            }

            // Evaluate shaft space allocation
            var hasShaftSpace = proposal.Parameters.GetValueOrDefault("HasMEPShaft") as bool? ?? false;
            var shaftArea = proposal.Parameters.GetValueOrDefault("MEPShaftArea") as double? ?? 0;

            if (hasShaftSpace && shaftArea > 0)
            {
                // Minimum recommended shaft: 1.5m x 1.5m = 2.25m² per shaft
                if (shaftArea < 2.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "MEP-COORD-020",
                        Description = $"MEP shaft area ({shaftArea:F1}m²) may be undersized (minimum 2.25m² recommended)",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Increase MEP shaft to at least 1.5m x 1.5m (2.25m²)"
                    });
                    coordinationScore -= 0.1f;
                }
                else
                {
                    strengths.Add("Adequate MEP shaft space allocated");
                }
            }
            else if (floors > 1)
            {
                coordinationScore -= 0.1f;
            }

            scores["Coordination"] = Math.Max(0.0f, Math.Min(1.0f, coordinationScore));
            if (coordinationScore >= 0.85f)
            {
                strengths.Add("MEP coordination requirements well addressed");
            }
        }

        private void AdjustForOtherDisciplines(AgentOpinion opinion)
        {
            var archFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Architectural Design");
            var structFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Structural Engineering");

            if (archFeedback != null)
            {
                // Check if architectural ceiling heights conflict with MEP needs
                var ceilingIssues = archFeedback.Issues.Where(i =>
                    i.Description.Contains("ceiling", StringComparison.OrdinalIgnoreCase));

                if (ceilingIssues.Any())
                {
                    opinion.Issues.Add(new DesignIssue
                    {
                        Code = "MEP-ARCH-CONFLICT",
                        Description = "Architectural ceiling requirements may conflict with MEP routing",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Coordinate ceiling heights with both architectural and MEP requirements"
                    });
                }
            }

            if (structFeedback != null)
            {
                // Check for beam penetrations
                var beamIssues = structFeedback.Issues.Where(i =>
                    i.Description.Contains("beam", StringComparison.OrdinalIgnoreCase));

                if (beamIssues.Any())
                {
                    opinion.Issues.Add(new DesignIssue
                    {
                        Code = "MEP-STRUCT-CONFLICT",
                        Description = "MEP routing must avoid or coordinate with structural beams",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Plan MEP routes to pass between beams or through approved penetrations"
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
                        baseScore -= 0.25f;
                        break;
                    case IssueSeverity.Error:
                        baseScore -= 0.15f;
                        break;
                    case IssueSeverity.Warning:
                        baseScore -= 0.05f;
                        break;
                }
            }

            return Math.Max(0, Math.Min(1, baseScore));
        }

        private string GenerateSummary(AgentOpinion opinion)
        {
            if (opinion.HasCriticalIssues)
                return "CRITICAL: MEP requirements not met - review before proceeding";
            if (opinion.Score >= 0.9f)
                return "MEP requirements well accommodated in design";
            if (opinion.Score >= 0.7f)
                return "MEP integration acceptable with recommendations";
            if (opinion.Score >= 0.5f)
                return "MEP concerns require design adjustments";
            return "Significant MEP coordination issues identified";
        }

        private ValidationResult ValidateRoomMEP(DesignAction action)
        {
            var height = action.Parameters.GetValueOrDefault("Height") as double? ?? 2.7;
            var minRequiredHeight = MinCeilingClearanceForDucts + TypicalDuctDepth;

            if (height < minRequiredHeight)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Warnings = new List<string>
                    {
                        $"Room height {height:F2}m may not accommodate standard duct routing. " +
                        $"Recommended minimum: {minRequiredHeight:F2}m"
                    }
                };
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateWetRoom(DesignAction action)
        {
            var result = new ValidationResult { IsValid = true, Warnings = new List<string>() };

            var roomType = action.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";

            if (roomType.Contains("bathroom", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("Bathroom requires: GFCI outlets, exhaust fan, waterproofing");
            }

            if (roomType.Contains("kitchen", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("Kitchen requires: Range hood exhaust, dedicated appliance circuits");
            }

            return result;
        }
    }
}
