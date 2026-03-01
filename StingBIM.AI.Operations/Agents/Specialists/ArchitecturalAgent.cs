// StingBIM.AI.Agents.Specialists.ArchitecturalAgent
// Specialist agent for architectural design evaluation
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (ARCH Agent)

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
    /// Architectural specialist agent.
    /// Evaluates spatial quality, proportions, flow, natural light, and user comfort.
    /// </summary>
    public class ArchitecturalAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<AgentOpinion> _receivedFeedback;

        // Architectural standards and guidelines
        private const double MinCeilingHeight = 2.4; // meters
        private const double IdealCeilingHeight = 2.7;
        private const double MinDoorWidth = 0.8;
        private const double MinCorridorWidth = 1.2;
        private const double MinRoomArea = 6.0; // m²

        // Daylighting constants
        private const double MinWindowToFloorRatio = 0.10; // 10% minimum for habitable rooms
        private const double GoodWindowToFloorRatio = 0.20; // 20% for good daylighting
        private const double MinDaylightFactor = 2.0; // percent - minimum for habitable rooms
        private const double GoodDaylightFactor = 5.0; // percent - well daylit spaces

        // Circulation constants
        private const double MaxDeadEndCorridorLength = 6.0; // meters per IBC 1020.4
        private const double MaxTravelDistance = 60.0; // meters to exit (sprinklered building)
        private const double MinEmergencyExitWidth = 1.0; // meters

        // Zone definitions for adjacency analysis
        private static readonly Dictionary<string, string> RoomZoneMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bedroom"] = "Private", ["bathroom"] = "Private", ["dressing"] = "Private",
            ["living"] = "Public", ["dining"] = "Public", ["kitchen"] = "Public",
            ["lobby"] = "Public", ["reception"] = "Public",
            ["corridor"] = "Circulation", ["stairwell"] = "Circulation",
            ["hallway"] = "Circulation", ["entrance"] = "Circulation",
            ["garage"] = "Service", ["storage"] = "Service", ["laundry"] = "Service",
            ["mechanical_room"] = "Service", ["electrical_room"] = "Service",
            ["utility"] = "Service", ["janitor"] = "Service",
        };

        // Preferred zone adjacency matrix (zone → preferred neighbors)
        private static readonly Dictionary<string, string[]> ZoneAdjacencyPreferences = new()
        {
            ["Private"] = new[] { "Private", "Circulation" },
            ["Public"] = new[] { "Public", "Circulation" },
            ["Circulation"] = new[] { "Private", "Public", "Circulation", "Service" },
            ["Service"] = new[] { "Service", "Circulation" },
        };

        public string AgentId => "ARCH-001";
        public string Specialty => "Architectural Design";
        public float ExpertiseLevel => 0.9f;
        public bool IsActive => true;

        public ArchitecturalAgent()
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
                Logger.Debug($"Architectural evaluation of proposal: {proposal.ProposalId}");

                var opinion = new AgentOpinion
                {
                    AgentId = AgentId,
                    Specialty = Specialty,
                    Score = 1.0f,
                    AspectScores = new Dictionary<string, float>()
                };

                var issues = new List<DesignIssue>();
                var strengths = new List<string>();

                // Evaluate each proposed element
                foreach (var element in proposal.Elements)
                {
                    EvaluateElement(element, issues, strengths, opinion.AspectScores);
                }

                // Evaluate modifications
                foreach (var modification in proposal.Modifications)
                {
                    EvaluateModification(modification, issues, strengths);
                }

                // Evaluate spatial relationships
                EvaluateSpatialRelationships(proposal, issues, strengths, opinion.AspectScores);

                // Calculate overall score
                opinion.Issues = issues;
                opinion.Strengths = strengths;
                opinion.Score = CalculateOverallScore(issues, opinion.AspectScores);

                // Consider feedback from other agents
                if (_receivedFeedback.Any())
                {
                    AdjustForFeedback(opinion);
                }

                opinion.Summary = GenerateSummary(opinion);
                Logger.Debug($"Architectural score: {opinion.Score:F2} ({issues.Count} issues)");

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

                // Suggest based on current task
                if (context.CurrentTask?.Contains("room", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Consider natural lighting",
                        Description = "Position the room to maximize natural light from south-facing windows",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.85f,
                        Impact = 0.7f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Maintain proportions",
                        Description = "Room aspect ratio should be between 1:1 and 1:2 for comfortable feel",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.8f,
                        Impact = 0.5f
                    });
                }

                if (context.CurrentTask?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Door swing direction",
                        Description = "Doors should swing into the room they open to, not into corridors",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.9f,
                        Impact = 0.4f
                    });
                }

                if (context.CurrentTask?.Contains("window", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Window-to-floor ratio",
                        Description = "Aim for at least 20% window-to-floor ratio for good daylighting. Minimum is 10% per building code.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.9f,
                        Impact = 0.8f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Glare control for west-facing windows",
                        Description = "West-facing glazing causes afternoon glare. Consider external shading or low-e coatings.",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.85f,
                        Impact = 0.6f
                    });
                }

                if (context.CurrentTask?.Contains("corridor", StringComparison.OrdinalIgnoreCase) == true ||
                    context.CurrentTask?.Contains("circulation", StringComparison.OrdinalIgnoreCase) == true)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Dead-end corridor limit",
                        Description = "Dead-end corridors must not exceed 6m per IBC 1020.4. Ensure two-way egress where possible.",
                        Type = SuggestionType.CodeCompliance,
                        Confidence = 0.95f,
                        Impact = 0.9f
                    });

                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Zone separation",
                        Description = "Separate private zones (bedrooms) from service zones (laundry, utilities) with circulation buffers.",
                        Type = SuggestionType.BestPractice,
                        Confidence = 0.8f,
                        Impact = 0.5f
                    });
                }

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
                    return ValidateWallCreation(action);
                case "createroom":
                    return ValidateRoomCreation(action);
                case "createdoor":
                case "createwindow":
                    return ValidateOpeningCreation(action);
                default:
                    return ValidationResult.Valid();
            }
        }

        private void EvaluateElement(ProposedElement element, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            switch (element.ElementType.ToLowerInvariant())
            {
                case "room":
                    EvaluateRoom(element, issues, strengths, scores);
                    break;
                case "wall":
                    EvaluateWall(element, issues, strengths, scores);
                    break;
                case "door":
                case "window":
                    EvaluateOpening(element, issues, strengths, scores);
                    break;
            }
        }

        private void EvaluateRoom(ProposedElement room, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            // Check ceiling height
            if (room.Geometry != null && room.Geometry.Height < MinCeilingHeight)
            {
                issues.Add(new DesignIssue
                {
                    Code = "ARCH-001",
                    Description = $"Ceiling height ({room.Geometry.Height:F1}m) is below minimum ({MinCeilingHeight}m)",
                    Severity = IssueSeverity.Error,
                    SuggestedFix = $"Increase ceiling height to at least {MinCeilingHeight}m"
                });
            }
            else if (room.Geometry != null && room.Geometry.Height >= IdealCeilingHeight)
            {
                strengths.Add("Good ceiling height for comfortable space");
                scores["CeilingHeight"] = 1.0f;
            }
            else
            {
                scores["CeilingHeight"] = 0.8f;
            }

            // Check room area
            if (room.Geometry != null)
            {
                var area = room.Geometry.Width * room.Geometry.Length;
                if (area < MinRoomArea)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-002",
                        Description = $"Room area ({area:F1}m²) is below minimum ({MinRoomArea}m²)",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Consider increasing room dimensions"
                    });
                    scores["RoomSize"] = 0.5f;
                }
                else
                {
                    scores["RoomSize"] = Math.Min(1.0f, (float)(area / 20.0));
                }

                // Check proportions
                var aspectRatio = Math.Max(room.Geometry.Width, room.Geometry.Length) /
                                  Math.Min(room.Geometry.Width, room.Geometry.Length);
                if (aspectRatio > 2.5)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-003",
                        Description = $"Room proportions are elongated (aspect ratio {aspectRatio:F1}:1)",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Consider a more balanced aspect ratio (1:1 to 1:2)"
                    });
                    scores["Proportions"] = 0.6f;
                }
                else
                {
                    scores["Proportions"] = 1.0f - (float)((aspectRatio - 1.0) / 3.0);
                }
            }

            // Check for windows (habitable rooms need windows)
            var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString();
            var hasWindow = room.Parameters.GetValueOrDefault("HasWindow") as bool? ?? false;

            if (IsHabitableRoom(roomType) && !hasWindow)
            {
                issues.Add(new DesignIssue
                {
                    Code = "ARCH-004",
                    Description = "Habitable room requires natural light (window)",
                    Severity = IssueSeverity.Error,
                    Standard = "Building Code - Natural Lighting Requirements",
                    SuggestedFix = "Add a window to provide natural light"
                });
            }

            // --- Daylighting analysis ---
            EvaluateDaylighting(room, issues, strengths, scores);
        }

        private void EvaluateDaylighting(ProposedElement room, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            var roomType = room.Parameters.GetValueOrDefault("RoomType")?.ToString();
            if (!IsHabitableRoom(roomType)) return;

            var roomArea = (room.Geometry?.Width ?? 0) * (room.Geometry?.Length ?? 0);
            if (roomArea <= 0) return;

            // Window-to-floor ratio analysis
            var windowArea = room.Parameters.GetValueOrDefault("WindowArea") as double? ?? 0;
            if (windowArea > 0)
            {
                var wfr = windowArea / roomArea;

                if (wfr < MinWindowToFloorRatio)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-DAY-001",
                        Description = $"Window-to-floor ratio ({wfr:P0}) is below minimum ({MinWindowToFloorRatio:P0})",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1204.1 / ASHRAE 90.1",
                        SuggestedFix = $"Increase glazed area to at least {roomArea * MinWindowToFloorRatio:F1}m²"
                    });
                    scores["Daylighting"] = 0.3f;
                }
                else if (wfr >= GoodWindowToFloorRatio)
                {
                    strengths.Add($"Excellent window-to-floor ratio ({wfr:P0}) for natural daylighting");
                    scores["Daylighting"] = 1.0f;
                }
                else
                {
                    scores["Daylighting"] = 0.5f + (float)((wfr - MinWindowToFloorRatio) / (GoodWindowToFloorRatio - MinWindowToFloorRatio)) * 0.5f;
                }
            }

            // Daylight factor estimation (simplified BRS method)
            var roomDepth = Math.Max(room.Geometry?.Length ?? 0, room.Geometry?.Width ?? 0);
            var windowHeight = room.Parameters.GetValueOrDefault("WindowHeadHeight") as double? ?? 2.1;
            var windowSillHeight = room.Parameters.GetValueOrDefault("WindowSillHeight") as double? ?? 0.9;
            var effectiveWindowHeight = windowHeight - windowSillHeight;

            if (effectiveWindowHeight > 0 && roomDepth > 0)
            {
                // Rule of thumb: no-sky-line depth ≈ 2× window head height
                var noSkyLineDepth = 2.0 * windowHeight;
                if (roomDepth > noSkyLineDepth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-DAY-002",
                        Description = $"Room depth ({roomDepth:F1}m) exceeds no-sky-line depth ({noSkyLineDepth:F1}m); rear of room will be poorly daylit",
                        Severity = IssueSeverity.Warning,
                        Standard = "BRS Split-Flux Method / CIBSE LG10",
                        SuggestedFix = "Add clerestory windows, lightshelves, or reduce room depth"
                    });
                }
            }

            // Orientation-based glare risk assessment
            var orientation = room.Parameters.GetValueOrDefault("Orientation")?.ToString()?.ToLowerInvariant() ?? "";
            if (orientation.Contains("west"))
            {
                issues.Add(new DesignIssue
                {
                    Code = "ARCH-DAY-003",
                    Description = "West-facing glazing is prone to afternoon glare and overheating",
                    Severity = IssueSeverity.Info,
                    Standard = "CIBSE TM37 / ASHRAE Fundamentals",
                    SuggestedFix = "Provide external shading devices, low-e glass, or internal blinds for west-facing windows"
                });
            }
            else if (orientation.Contains("south") || orientation.Contains("north"))
            {
                strengths.Add($"Good window orientation ({orientation}) for balanced daylighting");
            }
        }

        private void EvaluateWall(ProposedElement wall, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            var wallScore = 1.0f;
            var thickness = wall.Geometry?.Width ?? 0.15;
            var height = wall.Geometry?.Height ?? 2.7;
            var length = wall.Geometry?.Length ?? 0;

            // --- Wall thickness checks ---
            // Minimum 0.1m for any wall (partition or otherwise)
            if (thickness < 0.1)
            {
                issues.Add(new DesignIssue
                {
                    Code = "ARCH-WALL-001",
                    Description = $"Wall thickness ({thickness:F2}m) is too thin for practical construction",
                    Severity = IssueSeverity.Warning,
                    SuggestedFix = "Increase wall thickness to at least 0.1m for partitions or 0.15m for standard walls"
                });
                wallScore -= 0.2f;
            }

            // --- Acoustic performance (party/separating walls need STC rating) ---
            var isPartyWall = wall.Parameters.GetValueOrDefault("IsPartyWall") as bool? ?? false;
            var wallFunction = wall.Parameters.GetValueOrDefault("WallFunction")?.ToString() ?? "";
            var isSeparating = isPartyWall ||
                               wallFunction.Contains("party", StringComparison.OrdinalIgnoreCase) ||
                               wallFunction.Contains("separating", StringComparison.OrdinalIgnoreCase);

            if (isSeparating)
            {
                // Party walls typically need STC 50+ (requires ~0.2m+ construction)
                if (thickness < 0.2)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-WALL-010",
                        Description = $"Party/separating wall thickness ({thickness:F2}m) is likely insufficient for acoustic isolation (STC 50+ required)",
                        Severity = IssueSeverity.Error,
                        Standard = "Building Code - Sound Transmission Class",
                        SuggestedFix = "Increase party wall thickness to 0.2m minimum, or use acoustic insulation and double-leaf construction"
                    });
                    wallScore -= 0.2f;
                }
                else
                {
                    strengths.Add("Party wall thickness adequate for acoustic separation");
                }
            }

            // --- Aesthetic proportions (wall height-to-length ratio) ---
            if (length > 0)
            {
                var wallRatio = length / height;
                // Very long unbroken walls (ratio > 8:1) can look monotonous
                if (wallRatio > 8.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-WALL-020",
                        Description = $"Long unbroken wall ({length:F1}m length, {wallRatio:F1}:1 ratio) may appear monotonous",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Consider articulating the wall with openings, recesses, or material changes"
                    });
                    wallScore -= 0.05f;
                }
            }

            scores["WallDesign"] = Math.Max(0.0f, Math.Min(1.0f, wallScore));
        }

        private void EvaluateOpening(ProposedElement opening, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            // Check door width
            if (opening.ElementType.Equals("door", StringComparison.OrdinalIgnoreCase))
            {
                var width = opening.Geometry?.Width ?? 0;
                if (width < MinDoorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-010",
                        Description = $"Door width ({width:F2}m) is below minimum ({MinDoorWidth}m)",
                        Severity = IssueSeverity.Error,
                        Standard = "Accessibility Requirements",
                        SuggestedFix = $"Increase door width to at least {MinDoorWidth}m"
                    });
                }
            }
        }

        private void EvaluateModification(ProposedModification modification, List<DesignIssue> issues, List<string> strengths)
        {
            var modType = modification.ModificationType?.ToLowerInvariant() ?? "";

            // Deletion: warn if it might affect room flow/circulation
            if (modType == "delete")
            {
                var elementType = modification.OldValues.GetValueOrDefault("ElementType")?.ToString() ?? "";

                if (elementType.Equals("door", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-MOD-001",
                        Description = "Removing a door may disrupt room circulation and flow",
                        Severity = IssueSeverity.Warning,
                        Location = modification.ElementId,
                        SuggestedFix = "Verify alternative circulation paths exist before removing door"
                    });
                }
                else if (elementType.Equals("wall", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-MOD-002",
                        Description = "Removing a wall may affect spatial separation and room flow",
                        Severity = IssueSeverity.Warning,
                        Location = modification.ElementId,
                        SuggestedFix = "Check that removing this wall does not create poorly defined spaces"
                    });
                }
                else if (elementType.Equals("window", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-MOD-003",
                        Description = "Removing a window reduces natural light and may violate habitable room requirements",
                        Severity = IssueSeverity.Warning,
                        Location = modification.ElementId,
                        SuggestedFix = "Ensure remaining windows provide adequate natural light"
                    });
                }
            }
            // Resize: check proportions remain acceptable
            else if (modType == "resize")
            {
                var newWidth = modification.NewValues.GetValueOrDefault("Width") as double? ?? 0;
                var newLength = modification.NewValues.GetValueOrDefault("Length") as double? ?? 0;

                if (newWidth > 0 && newLength > 0)
                {
                    var aspectRatio = Math.Max(newWidth, newLength) / Math.Min(newWidth, newLength);
                    if (aspectRatio > 3.0)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "ARCH-MOD-010",
                            Description = $"Resized element has extreme aspect ratio ({aspectRatio:F1}:1), may feel disproportionate",
                            Severity = IssueSeverity.Warning,
                            Location = modification.ElementId,
                            SuggestedFix = "Aim for aspect ratios below 2.5:1 for comfortable proportions"
                        });
                    }
                    else
                    {
                        strengths.Add("Modified element maintains acceptable proportions");
                    }
                }
            }
            // Material change: check aesthetic consistency
            else if (modType == "materialchange" || modType == "material_change")
            {
                var oldMaterial = modification.OldValues.GetValueOrDefault("Material")?.ToString() ?? "";
                var newMaterial = modification.NewValues.GetValueOrDefault("Material")?.ToString() ?? "";

                if (!string.IsNullOrEmpty(oldMaterial) && !string.IsNullOrEmpty(newMaterial))
                {
                    // Check if mixing incompatible material families (e.g., traditional brick with ultra-modern glass)
                    var traditionalMaterials = new[] { "brick", "stone", "timber", "wood", "plaster" };
                    var modernMaterials = new[] { "glass", "steel", "aluminium", "aluminum", "composite" };

                    var oldIsTraditional = traditionalMaterials.Any(m => oldMaterial.Contains(m, StringComparison.OrdinalIgnoreCase));
                    var newIsModern = modernMaterials.Any(m => newMaterial.Contains(m, StringComparison.OrdinalIgnoreCase));
                    var oldIsModern = modernMaterials.Any(m => oldMaterial.Contains(m, StringComparison.OrdinalIgnoreCase));
                    var newIsTraditional = traditionalMaterials.Any(m => newMaterial.Contains(m, StringComparison.OrdinalIgnoreCase));

                    if ((oldIsTraditional && newIsModern) || (oldIsModern && newIsTraditional))
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "ARCH-MOD-020",
                            Description = $"Material change from '{oldMaterial}' to '{newMaterial}' may create aesthetic inconsistency",
                            Severity = IssueSeverity.Info,
                            Location = modification.ElementId,
                            SuggestedFix = "Ensure the new material is consistent with the overall design language"
                        });
                    }
                    else
                    {
                        strengths.Add("Material change maintains aesthetic consistency");
                    }
                }
            }
        }

        private void EvaluateSpatialRelationships(DesignProposal proposal, List<DesignIssue> issues, List<string> strengths, Dictionary<string, float> scores)
        {
            var rooms = proposal.Elements.Where(e =>
                e.ElementType.Equals("room", StringComparison.OrdinalIgnoreCase)).Cast<ProposedElement>().ToList();

            if (rooms.Count == 0)
            {
                scores["SpatialOrganization"] = 0.8f;
                return;
            }

            var spatialScore = 1.0f;

            // --- Check functional clustering (wet rooms together, living areas together) ---
            var wetRoomTypes = new[] { "bathroom", "toilet", "kitchen", "laundry", "utility" };
            var livingTypes = new[] { "living", "dining", "family", "lounge" };

            var wetRooms = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return wetRoomTypes.Any(t => rt.Contains(t, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            var livingRooms = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return livingTypes.Any(t => rt.Contains(t, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            // Check if wet rooms are clustered (close X/Y positions)
            if (wetRooms.Count >= 2)
            {
                var avgX = wetRooms.Average(r => r.Geometry?.X ?? 0);
                var avgY = wetRooms.Average(r => r.Geometry?.Y ?? 0);
                var maxDeviation = wetRooms.Max(r =>
                    Math.Sqrt(Math.Pow((r.Geometry?.X ?? 0) - avgX, 2) +
                              Math.Pow((r.Geometry?.Y ?? 0) - avgY, 2)));

                if (maxDeviation > 10.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-SPATIAL-001",
                        Description = "Wet rooms are scattered across the plan; grouping them reduces plumbing costs",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Locate bathrooms, kitchens, and laundry rooms near each other"
                    });
                    spatialScore -= 0.1f;
                }
                else
                {
                    strengths.Add("Wet rooms are well clustered for efficient plumbing");
                }
            }

            // --- Check circulation efficiency (corridors not too long) ---
            var corridors = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return rt.Contains("corridor", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("hallway", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("passage", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var corridor in corridors)
            {
                var length = Math.Max(corridor.Geometry?.Length ?? 0, corridor.Geometry?.Width ?? 0);
                var width = Math.Min(corridor.Geometry?.Length ?? 0, corridor.Geometry?.Width ?? 0);

                if (length > 15.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-SPATIAL-010",
                        Description = $"Corridor length ({length:F1}m) is excessive, reducing circulation efficiency",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Consider reorganizing rooms to shorten corridor length below 15m"
                    });
                    spatialScore -= 0.1f;
                }

                if (width > 0 && width < MinCorridorWidth)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-SPATIAL-011",
                        Description = $"Corridor width ({width:F2}m) is below minimum ({MinCorridorWidth}m)",
                        Severity = IssueSeverity.Error,
                        SuggestedFix = $"Widen corridor to at least {MinCorridorWidth}m"
                    });
                    spatialScore -= 0.15f;
                }
            }

            // --- Evaluate adjacency relationships ---
            // Kitchen should be near dining
            var kitchens = rooms.Where(r =>
                (r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "")
                    .Contains("kitchen", StringComparison.OrdinalIgnoreCase)).ToList();
            var diningRooms = rooms.Where(r =>
                (r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "")
                    .Contains("dining", StringComparison.OrdinalIgnoreCase)).ToList();

            if (kitchens.Any() && diningRooms.Any())
            {
                var minDistance = (from k in kitchens
                                  from d in diningRooms
                                  let dist = Math.Sqrt(
                                      Math.Pow((k.Geometry?.X ?? 0) - (d.Geometry?.X ?? 0), 2) +
                                      Math.Pow((k.Geometry?.Y ?? 0) - (d.Geometry?.Y ?? 0), 2))
                                  select dist).Min();

                if (minDistance > 8.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-SPATIAL-020",
                        Description = $"Kitchen and dining room are far apart ({minDistance:F1}m); adjacency is preferred",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Position kitchen adjacent to or near the dining room"
                    });
                    spatialScore -= 0.05f;
                }
                else
                {
                    strengths.Add("Kitchen is well positioned near dining area");
                }
            }

            // Bathroom should be near bedrooms
            var bathrooms = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "";
                return rt.Contains("bathroom", StringComparison.OrdinalIgnoreCase) ||
                       rt.Contains("toilet", StringComparison.OrdinalIgnoreCase);
            }).ToList();
            var bedrooms = rooms.Where(r =>
                (r.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "")
                    .Contains("bedroom", StringComparison.OrdinalIgnoreCase)).ToList();

            if (bathrooms.Any() && bedrooms.Any())
            {
                var minDistance = (from b in bathrooms
                                  from br in bedrooms
                                  let dist = Math.Sqrt(
                                      Math.Pow((b.Geometry?.X ?? 0) - (br.Geometry?.X ?? 0), 2) +
                                      Math.Pow((b.Geometry?.Y ?? 0) - (br.Geometry?.Y ?? 0), 2))
                                  select dist).Min();

                if (minDistance > 10.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-SPATIAL-021",
                        Description = $"Nearest bathroom is {minDistance:F1}m from bedrooms; closer proximity is recommended",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = "Position at least one bathroom near the bedroom zone"
                    });
                    spatialScore -= 0.05f;
                }
                else
                {
                    strengths.Add("Bathroom is conveniently located near bedrooms");
                }
            }

            // --- Zone-based adjacency analysis ---
            EvaluateZoneOrganization(rooms, issues, strengths, ref spatialScore);

            // --- Multi-step circulation path analysis ---
            EvaluateCirculationPaths(rooms, issues, strengths, ref spatialScore);

            scores["SpatialOrganization"] = Math.Max(0.0f, Math.Min(1.0f, spatialScore));
        }

        private void EvaluateZoneOrganization(List<ProposedElement> rooms, List<DesignIssue> issues, List<string> strengths, ref float spatialScore)
        {
            if (rooms.Count < 3) return;

            // Classify rooms into zones
            var roomsByZone = new Dictionary<string, List<ProposedElement>>();
            foreach (var room in rooms)
            {
                var rt = room.Parameters.GetValueOrDefault("RoomType")?.ToString()?.ToLowerInvariant() ?? "";
                var zone = RoomZoneMap.GetValueOrDefault(rt, "Public");

                if (!roomsByZone.ContainsKey(zone))
                    roomsByZone[zone] = new List<ProposedElement>();
                roomsByZone[zone].Add(room);
            }

            // Check zone separation (private rooms should not be adjacent to service rooms)
            foreach (var room in rooms)
            {
                var rt = room.Parameters.GetValueOrDefault("RoomType")?.ToString()?.ToLowerInvariant() ?? "";
                var zone = RoomZoneMap.GetValueOrDefault(rt, "Public");

                if (!ZoneAdjacencyPreferences.TryGetValue(zone, out var preferredNeighborZones))
                    continue;

                // Find nearest room and check if zone pairing is appropriate
                foreach (var other in rooms)
                {
                    if (other == room) continue;
                    var otherRt = other.Parameters.GetValueOrDefault("RoomType")?.ToString()?.ToLowerInvariant() ?? "";
                    var otherZone = RoomZoneMap.GetValueOrDefault(otherRt, "Public");

                    var distance = Math.Sqrt(
                        Math.Pow((room.Geometry?.X ?? 0) - (other.Geometry?.X ?? 0), 2) +
                        Math.Pow((room.Geometry?.Y ?? 0) - (other.Geometry?.Y ?? 0), 2));

                    // Check direct adjacency (within 2m indicates sharing a wall)
                    if (distance < 2.0 && !preferredNeighborZones.Contains(otherZone))
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "ARCH-ZONE-001",
                            Description = $"{rt} ({zone} zone) is directly adjacent to {otherRt} ({otherZone} zone); zone separation recommended",
                            Severity = IssueSeverity.Warning,
                            SuggestedFix = $"Separate {zone} and {otherZone} zones with a circulation buffer (corridor or lobby)"
                        });
                        spatialScore -= 0.05f;
                        break; // One issue per room
                    }
                }
            }

            // Check zone clustering (rooms in same zone should be grouped)
            foreach (var (zone, zoneRooms) in roomsByZone)
            {
                if (zoneRooms.Count < 2) continue;

                var avgX = zoneRooms.Average(r => r.Geometry?.X ?? 0);
                var avgY = zoneRooms.Average(r => r.Geometry?.Y ?? 0);
                var maxSpread = zoneRooms.Max(r =>
                    Math.Sqrt(Math.Pow((r.Geometry?.X ?? 0) - avgX, 2) +
                              Math.Pow((r.Geometry?.Y ?? 0) - avgY, 2)));

                // If zone rooms are spread over more than 15m, they're fragmented
                if (maxSpread > 15.0)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-ZONE-002",
                        Description = $"{zone} zone is fragmented (spread: {maxSpread:F1}m); grouping improves efficiency",
                        Severity = IssueSeverity.Info,
                        SuggestedFix = $"Consolidate {zone} zone rooms closer together for better organization"
                    });
                    spatialScore -= 0.03f;
                }
                else if (zoneRooms.Count >= 3)
                {
                    strengths.Add($"{zone} zone is well clustered ({zoneRooms.Count} rooms within {maxSpread:F1}m)");
                }
            }
        }

        private void EvaluateCirculationPaths(List<ProposedElement> rooms, List<DesignIssue> issues, List<string> strengths, ref float spatialScore)
        {
            var corridors = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString()?.ToLowerInvariant() ?? "";
                return rt.Contains("corridor") || rt.Contains("hallway") || rt.Contains("passage");
            }).ToList();

            var otherRooms = rooms.Where(r => !corridors.Contains(r)).ToList();

            if (corridors.Count == 0 && otherRooms.Count > 3)
            {
                issues.Add(new DesignIssue
                {
                    Code = "ARCH-CIRC-001",
                    Description = "No corridors defined despite multiple rooms; circulation paths may be unclear",
                    Severity = IssueSeverity.Warning,
                    SuggestedFix = "Add corridors to define clear circulation paths between rooms"
                });
                spatialScore -= 0.1f;
                return;
            }

            // Check dead-end corridors (corridor with only one connection point)
            foreach (var corridor in corridors)
            {
                var length = Math.Max(corridor.Geometry?.Length ?? 0, corridor.Geometry?.Width ?? 0);
                var connectedRooms = otherRooms.Count(r =>
                {
                    var dist = Math.Sqrt(
                        Math.Pow((corridor.Geometry?.X ?? 0) - (r.Geometry?.X ?? 0), 2) +
                        Math.Pow((corridor.Geometry?.Y ?? 0) - (r.Geometry?.Y ?? 0), 2));
                    return dist < length; // Room is roughly along the corridor
                });

                // A dead-end corridor connects rooms on only one end
                if (connectedRooms <= 1 && length > MaxDeadEndCorridorLength)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ARCH-CIRC-002",
                        Description = $"Dead-end corridor ({length:F1}m) exceeds maximum ({MaxDeadEndCorridorLength}m); fire egress concern",
                        Severity = IssueSeverity.Error,
                        Standard = "IBC 1020.4 — Dead End Corridors",
                        SuggestedFix = "Add a second exit or shorten the dead-end corridor to under 6m"
                    });
                    spatialScore -= 0.15f;
                }
            }

            // Check maximum travel distance to an exit
            var exits = rooms.Where(r =>
            {
                var rt = r.Parameters.GetValueOrDefault("RoomType")?.ToString()?.ToLowerInvariant() ?? "";
                return rt.Contains("entrance") || rt.Contains("lobby") || rt.Contains("stair");
            }).ToList();

            if (exits.Any())
            {
                foreach (var room in otherRooms)
                {
                    var minDistToExit = exits.Min(e => Math.Sqrt(
                        Math.Pow((room.Geometry?.X ?? 0) - (e.Geometry?.X ?? 0), 2) +
                        Math.Pow((room.Geometry?.Y ?? 0) - (e.Geometry?.Y ?? 0), 2)));

                    if (minDistToExit > MaxTravelDistance)
                    {
                        var rt = room.Parameters.GetValueOrDefault("RoomType")?.ToString() ?? "room";
                        issues.Add(new DesignIssue
                        {
                            Code = "ARCH-CIRC-003",
                            Description = $"{rt} is {minDistToExit:F1}m from nearest exit (max {MaxTravelDistance}m)",
                            Severity = IssueSeverity.Error,
                            Standard = "IBC 1017.1 — Exit Access Travel Distance",
                            SuggestedFix = "Add an additional exit closer to this room or reorganize layout"
                        });
                        spatialScore -= 0.2f;
                    }
                }
            }

            if (corridors.Count > 0 && !issues.Any(i => i.Code.StartsWith("ARCH-CIRC")))
            {
                strengths.Add("Circulation paths appear well organized with acceptable travel distances");
            }
        }

        private bool IsHabitableRoom(string roomType)
        {
            var habitableTypes = new[] { "bedroom", "living", "office", "study", "kitchen", "dining" };
            return habitableTypes.Any(t => roomType?.Contains(t, StringComparison.OrdinalIgnoreCase) == true);
        }

        private float CalculateOverallScore(List<DesignIssue> issues, Dictionary<string, float> aspectScores)
        {
            var baseScore = aspectScores.Count > 0 ? aspectScores.Values.Average() : 1.0f;

            // Deduct for issues
            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Critical:
                        baseScore -= 0.3f;
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

        private void AdjustForFeedback(AgentOpinion opinion)
        {
            // Consider structural agent feedback about load-bearing walls
            var structuralFeedback = _receivedFeedback.FirstOrDefault(f => f.Specialty == "Structural Engineering");
            if (structuralFeedback != null && structuralFeedback.HasCriticalIssues)
            {
                // Add note about structural constraints
                opinion.Issues.Add(new DesignIssue
                {
                    Code = "ARCH-STRUCT",
                    Description = "Note: Structural constraints may affect architectural options",
                    Severity = IssueSeverity.Info
                });
            }

            opinion.IsRevised = true;
            _receivedFeedback.Clear();
        }

        private string GenerateSummary(AgentOpinion opinion)
        {
            if (opinion.Score >= 0.9f)
                return "Excellent architectural design with good spatial quality";
            if (opinion.Score >= 0.7f)
                return "Good design with minor areas for improvement";
            if (opinion.Score >= 0.5f)
                return "Design needs attention to architectural concerns";
            return "Significant architectural issues need to be addressed";
        }

        private ValidationResult ValidateWallCreation(DesignAction action)
        {
            // Walls are generally OK from architectural perspective
            return ValidationResult.Valid();
        }

        private ValidationResult ValidateRoomCreation(DesignAction action)
        {
            var height = action.Parameters.GetValueOrDefault("Height") as double? ?? MinCeilingHeight;
            if (height < MinCeilingHeight)
            {
                return ValidationResult.Invalid($"Room height must be at least {MinCeilingHeight}m");
            }
            return ValidationResult.Valid();
        }

        private ValidationResult ValidateOpeningCreation(DesignAction action)
        {
            return ValidationResult.Valid();
        }
    }
}
