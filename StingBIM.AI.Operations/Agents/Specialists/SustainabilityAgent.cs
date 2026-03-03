// StingBIM.AI.Agents.Specialists.SustainabilityAgent
// Specialist agent for sustainability, energy, and environmental evaluation
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (SUSTAIN Agent)

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
    /// Sustainability and environmental specialist agent.
    /// Evaluates energy efficiency, material sustainability, daylighting, and green building criteria.
    /// References LEED, BREEAM, EDGE, ASHRAE 90.1, and regional green codes.
    /// </summary>
    public class SustainabilityAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // Energy Standards (ASHRAE 90.1 / IECC)
        private const double MaxWindowToWallRatio = 0.40; // 40% WWR baseline
        private const double MinWallRValue = 3.5; // m²·K/W (R-20)
        private const double MinRoofRValue = 5.3; // m²·K/W (R-30)
        private const double MaxWindowUValue = 2.0; // W/m²·K
        private const double MinLightingEfficacy = 80; // lumens/watt

        // Daylighting Standards
        private const double MinDaylightFactor = 0.02; // 2% daylight factor
        private const double MinWindowToFloorRatio = 0.15; // 15% for daylight
        private const double MaxWindowToFloorRatio = 0.25; // 25% to limit heat gain

        // Material Sustainability
        private const double MinRecycledContent = 0.10; // 10% recycled content target
        private const double MinLocalMaterial = 0.20; // 20% local sourcing target
        private const double MaxEmbodiedCarbon = 500; // kg CO2e/m² target

        // Water Efficiency
        private const double MaxWaterFlowLavatory = 5.7; // L/min (1.5 gpm)
        private const double MaxWaterFlowShower = 7.6; // L/min (2.0 gpm)
        private const double MaxToiletFlush = 4.8; // L/flush (1.28 gpf)

        // Embodied carbon estimates (kg CO2e per unit)
        private static readonly Dictionary<string, double> EmbodiedCarbon = new()
        {
            ["concrete"] = 150,    // per m³
            ["steel"] = 1.85,      // per kg
            ["timber"] = -1.0,     // per kg (carbon negative)
            ["brick"] = 0.23,      // per kg
            ["glass"] = 1.5,       // per kg
            ["aluminum"] = 12.5,   // per kg
            ["insulation"] = 2.5   // per m²
        };

        public string AgentId => "SUSTAIN-001";
        public string Specialty => "Sustainability";
        public float ExpertiseLevel => 0.88f;
        public bool IsActive => true;

        public SustainabilityAgent()
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
                Logger.Debug($"Sustainability evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Evaluate energy efficiency
                EvaluateEnergyEfficiency(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate daylighting
                EvaluateDaylighting(proposal, issues, strengths, opinion.AspectScores);

                // Evaluate material sustainability
                EvaluateMaterialSustainability(proposal, issues, strengths, opinion.AspectScores);

                // Evaluate passive design strategies
                EvaluatePassiveDesign(proposal, issues, strengths, opinion.AspectScores, context);

                // Evaluate water efficiency
                EvaluateWaterEfficiency(proposal, issues, strengths, opinion.AspectScores);

                // Calculate embodied carbon
                var embodiedCarbon = CalculateEmbodiedCarbon(proposal);
                opinion.AspectScores["EmbodiedCarbon"] = Math.Min(1.0f, (float)(MaxEmbodiedCarbon / Math.Max(embodiedCarbon, 1)));

                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                if (_receivedFeedback.Any())
                {
                    AdjustForOtherDisciplines(opinion);
                }

                opinion.Summary = GenerateSummary(opinion, embodiedCarbon);
                Logger.Debug($"Sustainability score: {opinion.Score:F2}");

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

                // Window-related suggestions
                if (context.CurrentTask?.Contains("window", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Optimize glazing for efficiency",
                        Description = $"Target {MaxWindowToWallRatio:P0} window-to-wall ratio. Use low-e glass (U-value < {MaxWindowUValue} W/m²K)",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.9f,
                        Impact = 0.75f,
                        Parameters = new Dictionary<string, object>
                        {
                            ["TargetWWR"] = MaxWindowToWallRatio,
                            ["MaxUValue"] = MaxWindowUValue
                        }
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Consider solar orientation",
                        Description = "Maximize south-facing glazing (northern hemisphere) with appropriate shading for passive solar gain",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.88f,
                        Impact = 0.7f
                    });
                }

                // Wall suggestions
                if (context.CurrentTask?.Contains("wall", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Thermal mass placement",
                        Description = "Place thermal mass (concrete/masonry) on interior for temperature stabilization",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.85f,
                        Impact = 0.6f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Low-carbon wall materials",
                        Description = "Consider timber framing (carbon-negative) or recycled-content blocks over traditional concrete",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.82f,
                        Impact = 0.65f
                    });
                }

                // Room layout suggestions
                if (context.CurrentTask?.Contains("room", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("layout", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Daylight optimization",
                        Description = $"Target {MinWindowToFloorRatio:P0} to {MaxWindowToFloorRatio:P0} window-to-floor ratio for balanced daylighting",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.9f,
                        Impact = 0.7f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Natural ventilation potential",
                        Description = "Orient rooms for cross-ventilation. Place openings on opposite walls.",
                        Type = SuggestionType.Sustainability,
                        Confidence = 0.85f,
                        Impact = 0.65f
                    });
                }

                // Material suggestions
                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "Specify recycled content",
                    Description = $"Target {MinRecycledContent:P0}+ recycled content for structural steel, concrete aggregates, and insulation",
                    Type = SuggestionType.Sustainability,
                    Confidence = 0.87f,
                    Impact = 0.5f
                });

                suggestions.Add(new AgentSuggestion
                {
                    AgentId = AgentId,
                    Title = "Local material sourcing",
                    Description = $"Source {MinLocalMaterial:P0}+ of materials within 800km to reduce transportation emissions",
                    Type = SuggestionType.Sustainability,
                    Confidence = 0.8f,
                    Impact = 0.45f
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
            var result = new ValidationResult { IsValid = true, Warnings = new List<string>() };

            switch (action.ActionType.ToLowerInvariant())
            {
                case "createwindow":
                    var windowArea = action.Parameters.GetValueOrDefault("Area") as double? ?? 0;
                    var floorArea = action.Parameters.GetValueOrDefault("RoomFloorArea") as double? ?? 20;
                    var ratio = windowArea / floorArea;

                    if (ratio > MaxWindowToFloorRatio)
                    {
                        result.Warnings.Add(
                            $"Window-to-floor ratio ({ratio:P0}) exceeds {MaxWindowToFloorRatio:P0} - may increase cooling loads");
                    }
                    else if (ratio < MinWindowToFloorRatio)
                    {
                        result.Warnings.Add(
                            $"Window-to-floor ratio ({ratio:P0}) below {MinWindowToFloorRatio:P0} - may require more artificial lighting");
                    }
                    break;

                case "createwall":
                    var material = action.Parameters.GetValueOrDefault("Material")?.ToString() ?? "";
                    if (material.Contains("concrete", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add(
                            "Concrete has high embodied carbon (~150 kg CO2e/m³). Consider low-carbon concrete or alternatives.");
                    }
                    break;
            }

            return result;
        }

        private void EvaluateEnergyEfficiency(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            // Calculate window-to-wall ratio
            var windows = proposal.Elements.Where(e =>
                e.ElementType.Equals("window", StringComparison.OrdinalIgnoreCase) ||
                e.ElementType.Equals("curtain_wall", StringComparison.OrdinalIgnoreCase)).ToList();

            var exteriorWalls = proposal.Elements.Where(e =>
                e.ElementType.Equals("wall", StringComparison.OrdinalIgnoreCase) &&
                (e.Parameters.GetValueOrDefault("IsExterior") as bool? ?? false)).ToList();

            var windowArea = windows.Sum(w => (w.Geometry?.Width ?? 1.2) * (w.Geometry?.Height ?? 1.5));
            var wallArea = exteriorWalls.Sum(w => (w.Geometry?.Length ?? 3) * (w.Geometry?.Height ?? 2.7));

            if (wallArea > 0)
            {
                var wwr = windowArea / wallArea;

                if (wwr > MaxWindowToWallRatio)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SUSTAIN-ENERGY-001",
                        Description = $"Window-to-wall ratio ({wwr:P0}) exceeds {MaxWindowToWallRatio:P0} baseline",
                        Severity = IssueSeverity.Warning,
                        Standard = "ASHRAE 90.1 / IECC",
                        SuggestedFix = "Reduce glazing area or compensate with high-performance glazing and shading",
                        Details = new Dictionary<string, object>
                        {
                            ["WWR"] = wwr,
                            ["Target"] = MaxWindowToWallRatio
                        }
                    });
                    scores["Energy_WWR"] = (float)(MaxWindowToWallRatio / wwr);
                }
                else
                {
                    scores["Energy_WWR"] = 1.0f;
                    strengths.Add($"Good window-to-wall ratio: {wwr:P0}");
                }
            }

            // Check for thermal envelope info
            var hasInsulation = proposal.Parameters.GetValueOrDefault("HasInsulation") as bool? ?? true;
            if (hasInsulation)
            {
                scores["Energy_Insulation"] = 0.9f;
            }
            else
            {
                issues.Add(new DesignIssue
                {
                    Code = "SUSTAIN-ENERGY-010",
                    Description = "Thermal insulation specification not found",
                    Severity = IssueSeverity.Warning,
                    Standard = "ASHRAE 90.1",
                    SuggestedFix = $"Specify wall R-value ≥ {MinWallRValue} m²·K/W, roof R-value ≥ {MinRoofRValue} m²·K/W"
                });
                scores["Energy_Insulation"] = 0.5f;
            }

            scores["Energy"] = scores.Where(kvp => kvp.Key.StartsWith("Energy")).Select(kvp => kvp.Value).Average();
        }

        private void EvaluateDaylighting(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            var rooms = proposal.Elements.Where(e =>
                e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var room in rooms)
            {
                var roomArea = (room.Geometry?.Width ?? 4) * (room.Geometry?.Length ?? 4);
                var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";

                // Check if room type requires daylight
                var requiresDaylight = !roomType.Contains("storage", StringComparison.OrdinalIgnoreCase) &&
                                       !roomType.Contains("closet", StringComparison.OrdinalIgnoreCase) &&
                                       !roomType.Contains("mechanical", StringComparison.OrdinalIgnoreCase);

                if (requiresDaylight)
                {
                    var windowArea = room.Parameters.GetValueOrDefault("WindowArea") as double? ?? roomArea * 0.15;
                    var wfr = windowArea / roomArea;

                    if (wfr < MinWindowToFloorRatio)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "SUSTAIN-DAYLIGHT-001",
                            Description = $"Room '{roomType}' has low window-to-floor ratio ({wfr:P0} < {MinWindowToFloorRatio:P0})",
                            Severity = IssueSeverity.Info,
                            Standard = "LEED v4 - Daylight",
                            SuggestedFix = "Increase window area or consider skylights for daylight"
                        });
                    }
                }
            }

            scores["Daylighting"] = issues.Any(i => i.Code.StartsWith("SUSTAIN-DAYLIGHT")) ? 0.7f : 1.0f;

            if (!issues.Any(i => i.Code.StartsWith("SUSTAIN-DAYLIGHT")))
            {
                strengths.Add("Daylighting provisions adequate");
            }
        }

        private void EvaluateMaterialSustainability(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check for sustainable material specifications
            var hasRecycledContent = proposal.Parameters.GetValueOrDefault("RecycledContentPercent") as double? ?? 0;
            var hasLocalMaterials = proposal.Parameters.GetValueOrDefault("LocalMaterialPercent") as double? ?? 0;

            if (hasRecycledContent < MinRecycledContent)
            {
                issues.Add(new DesignIssue
                {
                    Code = "SUSTAIN-MAT-001",
                    Description = $"Recycled content ({hasRecycledContent:P0}) below target ({MinRecycledContent:P0})",
                    Severity = IssueSeverity.Info,
                    Standard = "LEED v4 - Materials",
                    SuggestedFix = "Specify materials with recycled content (steel, concrete, insulation)"
                });
                scores["Material_Recycled"] = (float)(hasRecycledContent / MinRecycledContent);
            }
            else
            {
                scores["Material_Recycled"] = 1.0f;
                strengths.Add($"Good recycled content: {hasRecycledContent:P0}");
            }

            if (hasLocalMaterials < MinLocalMaterial)
            {
                issues.Add(new DesignIssue
                {
                    Code = "SUSTAIN-MAT-010",
                    Description = $"Local material sourcing ({hasLocalMaterials:P0}) below target ({MinLocalMaterial:P0})",
                    Severity = IssueSeverity.Info,
                    Standard = "LEED v4 - Regional Materials",
                    SuggestedFix = "Source materials within 800km radius where possible"
                });
                scores["Material_Local"] = (float)(hasLocalMaterials / MinLocalMaterial);
            }
            else
            {
                scores["Material_Local"] = 1.0f;
            }

            // Check for timber (carbon-negative)
            var hasTimber = proposal.Elements.Any(e =>
                e.Parameters.GetValueOrDefault("Material")?.ToString()?.Contains("timber", StringComparison.OrdinalIgnoreCase) == true ||
                e.Parameters.GetValueOrDefault("Material")?.ToString()?.Contains("wood", StringComparison.OrdinalIgnoreCase) == true);

            if (hasTimber)
            {
                strengths.Add("Timber elements contribute to carbon sequestration");
                scores["Material_Carbon"] = 1.0f;
            }
            else
            {
                scores["Material_Carbon"] = 0.8f;
            }

            scores["Materials"] = scores.Where(kvp => kvp.Key.StartsWith("Material")).Select(kvp => kvp.Value).Average();
        }

        private void EvaluatePassiveDesign(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores, EvaluationContext context)
        {
            var climateZone = context?.ClimateZone ?? "temperate";

            // Check for natural ventilation potential
            var hasOppositeOpenings = proposal.Parameters.GetValueOrDefault("HasCrossVentilation") as bool? ?? false;
            if (hasOppositeOpenings)
            {
                strengths.Add("Cross-ventilation potential identified");
                scores["Passive_Ventilation"] = 1.0f;
            }
            else
            {
                scores["Passive_Ventilation"] = 0.7f;
            }

            // Check thermal mass for appropriate climates
            var hasThermalMass = proposal.Elements.Any(e =>
                e.Parameters.GetValueOrDefault("Material")?.ToString()?.Contains("concrete", StringComparison.OrdinalIgnoreCase) == true ||
                e.Parameters.GetValueOrDefault("Material")?.ToString()?.Contains("masonry", StringComparison.OrdinalIgnoreCase) == true);

            if (climateZone.Contains("tropical", StringComparison.OrdinalIgnoreCase) ||
                climateZone.Contains("hot", StringComparison.OrdinalIgnoreCase))
            {
                if (!hasThermalMass)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "SUSTAIN-PASSIVE-001",
                        Description = "Hot climate - consider thermal mass for temperature stabilization",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Add thermal mass elements (concrete floors, masonry walls) to moderate temperatures"
                    });
                }
            }

            // Check for shading
            var hasShading = proposal.Parameters.GetValueOrDefault("HasExternalShading") as bool? ?? false;
            if (hasShading)
            {
                strengths.Add("External shading devices reduce cooling loads");
                scores["Passive_Shading"] = 1.0f;
            }
            else
            {
                issues.Add(new DesignIssue
                {
                    Code = "SUSTAIN-PASSIVE-010",
                    Description = "Consider external shading for south/west facades",
                    Severity = IssueSeverity.Info,
                    SuggestedFix = "Add overhangs, louvers, or vegetation for solar control"
                });
                scores["Passive_Shading"] = 0.7f;
            }

            scores["PassiveDesign"] = scores.Where(kvp => kvp.Key.StartsWith("Passive")).Select(kvp => kvp.Value).Average();
        }

        private void EvaluateWaterEfficiency(DesignProposal proposal, List<DesignIssue> issues,
            List<string> strengths, Dictionary<string, float> scores)
        {
            // Check for water-efficient fixtures specification
            var hasWaterEfficient = proposal.Parameters.GetValueOrDefault("WaterEfficientFixtures") as bool? ?? false;

            if (hasWaterEfficient)
            {
                strengths.Add("Water-efficient fixtures specified");
                scores["Water"] = 1.0f;
            }
            else
            {
                issues.Add(new DesignIssue
                {
                    Code = "SUSTAIN-WATER-001",
                    Description = "Water efficiency not specified",
                    Severity = IssueSeverity.Info,
                    Standard = "LEED v4 - Water Efficiency",
                    SuggestedFix = $"Specify low-flow fixtures: lavatories <{MaxWaterFlowLavatory} L/min, toilets <{MaxToiletFlush} L/flush"
                });
                scores["Water"] = 0.7f;
            }

            // Check for rainwater harvesting
            var hasRainwater = proposal.Parameters.GetValueOrDefault("HasRainwaterHarvesting") as bool? ?? false;
            if (hasRainwater)
            {
                strengths.Add("Rainwater harvesting system included");
            }
        }

        private double CalculateEmbodiedCarbon(DesignProposal proposal)
        {
            var totalCarbon = 0.0;
            var totalArea = 0.0;

            foreach (var element in proposal.Elements)
            {
                var material = element.Parameters.GetValueOrDefault("Material")?.ToString()?.ToLowerInvariant() ?? "concrete";
                var volume = CalculateElementVolume(element);
                var density = GetMaterialDensity(material);

                if (EmbodiedCarbon.TryGetValue(material, out var carbonFactor))
                {
                    totalCarbon += volume * density * carbonFactor;
                }
                else
                {
                    // Default to concrete
                    totalCarbon += volume * EmbodiedCarbon["concrete"];
                }

                if (element.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase) ||
                    element.ElementType.Equals("floor", StringComparison.OrdinalIgnoreCase))
                {
                    totalArea += (element.Geometry?.Width ?? 4) * (element.Geometry?.Length ?? 4);
                }
            }

            // Return kg CO2e per m²
            return totalArea > 0 ? totalCarbon / totalArea : totalCarbon;
        }

        private double CalculateElementVolume(ProposedElement element)
        {
            if (element.Geometry == null) return 1.0;

            return element.ElementType.ToLowerInvariant() switch
            {
                "wall" => (element.Geometry.Length) * (element.Geometry.Height) * (element.Geometry.Width),
                "floor" or "slab" => (element.Geometry.Width) * (element.Geometry.Length) * (element.Geometry.Height),
                "column" => (element.Geometry.Width) * (element.Geometry.Width) * (element.Geometry.Height),
                _ => 1.0
            };
        }

        private double GetMaterialDensity(string material)
        {
            return material switch
            {
                "concrete" => 2400,  // kg/m³
                "steel" => 7850,
                "timber" or "wood" => 500,
                "brick" or "masonry" => 1800,
                "glass" => 2500,
                "aluminum" => 2700,
                _ => 2000
            };
        }

        private void AdjustForOtherDisciplines(AgentOpinion opinion)
        {
            var costFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Cost Engineering");

            if (costFeedback != null)
            {
                // Note cost implications of sustainability measures
                if (costFeedback.Score < 0.7f)
                {
                    opinion.Issues.Add(new DesignIssue
                    {
                        Code = "SUSTAIN-COST-NOTE",
                        Description = "Sustainability measures may have cost implications - lifecycle cost analysis recommended",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Consider lifecycle costs - sustainable features often have lower operating costs"
                    });
                }
            }

            opinion.IsRevised = true;
            _receivedFeedback.Clear();
        }

        private float CalculateOverallScore(List<DesignIssue> issues, Dictionary<string, float> aspectScores)
        {
            var mainScores = new[] { "Energy", "Daylighting", "Materials", "PassiveDesign", "Water", "EmbodiedCarbon" };
            var relevantScores = aspectScores.Where(kvp => mainScores.Contains(kvp.Key)).ToList();

            var baseScore = relevantScores.Count > 0 ? relevantScores.Select(kvp => kvp.Value).Average() : 0.8f;

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

        private string GenerateSummary(AgentOpinion opinion, double embodiedCarbon)
        {
            var carbonLevel = embodiedCarbon switch
            {
                < 300 => "Excellent",
                < 500 => "Good",
                < 700 => "Average",
                _ => "High"
            };

            var greenRating = opinion.Score switch
            {
                >= 0.9f => "Excellent sustainability performance",
                >= 0.75f => "Good sustainability with improvement opportunities",
                >= 0.6f => "Moderate sustainability - enhancements recommended",
                _ => "Sustainability concerns require attention"
            };

            return $"{greenRating}. Embodied carbon: {carbonLevel} ({embodiedCarbon:F0} kg CO2e/m²)";
        }
    }
}
