// ============================================================================
// StingBIM AI Agents - Lighting Agent
// Specialist agent for daylighting and artificial lighting evaluation
// ============================================================================

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
    /// Specialist agent for lighting design evaluation.
    /// Evaluates daylighting, artificial lighting, and energy compliance.
    /// </summary>
    public class LightingAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public string AgentId => "LightingAgent";
        public string Specialty => "Lighting";
        public float ExpertiseLevel => 0.85f;
        public bool IsActive { get; set; } = true;

        // Lighting standards and requirements
        private readonly Dictionary<string, LightingRequirements> _roomRequirements;
        private readonly Dictionary<string, double> _lightingPowerDensity;
        private AgentOpinion _lastReceivedFeedback;

        public LightingAgent()
        {
            _roomRequirements = InitializeRoomRequirements();
            _lightingPowerDensity = InitializeLPDLimits();
        }

        public async Task<AgentOpinion> EvaluateAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Lighting evaluation for proposal: {proposal.ProposalId}");

            var opinion = new AgentOpinion
            {
                AgentId = AgentId,
                AgentSpecialty = Specialty,
                Confidence = ExpertiseLevel
            };

            var issues = new List<DesignIssue>();
            var strengths = new List<string>();
            var score = 1.0f;

            // Evaluate daylighting
            var daylightIssues = EvaluateDaylighting(proposal);
            issues.AddRange(daylightIssues);
            score -= daylightIssues.Count * 0.1f;

            // Evaluate artificial lighting levels
            var lightingIssues = EvaluateArtificialLighting(proposal);
            issues.AddRange(lightingIssues);
            score -= lightingIssues.Count * 0.1f;

            // Evaluate energy compliance (ASHRAE 90.1 LPD)
            var energyIssues = EvaluateLightingPowerDensity(proposal);
            issues.AddRange(energyIssues);
            score -= energyIssues.Count * 0.15f;

            // Evaluate glare control
            var glareIssues = EvaluateGlareControl(proposal);
            issues.AddRange(glareIssues);
            score -= glareIssues.Count * 0.05f;

            // Check for lighting strengths
            if (HasGoodDaylightIntegration(proposal))
            {
                strengths.Add("Good daylight integration with automatic controls");
            }

            if (MeetsEnergyCodeRequirements(proposal))
            {
                strengths.Add("Lighting power density meets ASHRAE 90.1 requirements");
            }

            if (HasProperTaskLighting(proposal))
            {
                strengths.Add("Appropriate task lighting provided for workspaces");
            }

            opinion.Score = Math.Max(0, score);
            opinion.Issues = issues;
            opinion.Strengths = strengths;
            opinion.HasCriticalIssues = issues.Any(i => i.Severity == IssueSeverity.Critical);
            opinion.IsPositive = opinion.Score >= 0.7f;

            await Task.CompletedTask;
            return opinion;
        }

        public ValidationResult ValidateAction(DesignAction action)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate window placement for daylighting
            if (action.ActionType == "CreateWindow")
            {
                var windowArea = action.Parameters.GetValueOrDefault("Area") as double? ?? 0;
                var roomArea = action.Parameters.GetValueOrDefault("RoomArea") as double? ?? 100;
                var orientation = action.Parameters.GetValueOrDefault("Orientation") as string;

                // Check window-to-floor ratio
                var wfr = windowArea / roomArea;
                if (wfr < 0.08) // Less than 8%
                {
                    result.Warnings.Add($"Window-to-floor ratio ({wfr:P0}) may provide insufficient daylight");
                }
                else if (wfr > 0.25) // More than 25%
                {
                    result.Warnings.Add($"Window-to-floor ratio ({wfr:P0}) may cause glare or thermal issues");
                }

                // Check orientation for heat gain
                if (orientation == "West" && windowArea > 5)
                {
                    result.Warnings.Add("Large west-facing windows may cause afternoon glare and heat gain");
                }
            }

            // Validate light fixture placement
            if (action.ActionType == "CreateLightingFixture")
            {
                var fixtureType = action.Parameters.GetValueOrDefault("FixtureType") as string;
                var roomType = action.Parameters.GetValueOrDefault("RoomType") as string;
                var lumens = action.Parameters.GetValueOrDefault("Lumens") as double? ?? 0;

                if (roomType != null && _roomRequirements.TryGetValue(roomType, out var req))
                {
                    // Check color temperature appropriateness
                    var colorTemp = action.Parameters.GetValueOrDefault("ColorTemperature") as int? ?? 4000;
                    if (roomType == "Bedroom" && colorTemp > 3000)
                    {
                        result.Warnings.Add("Warm color temperature (≤3000K) recommended for bedrooms");
                    }
                    if ((roomType == "Office" || roomType == "Classroom") && colorTemp < 3500)
                    {
                        result.Warnings.Add("Neutral to cool color temperature (3500-5000K) recommended for work areas");
                    }
                }
            }

            return result;
        }

        public async Task<IEnumerable<AgentSuggestion>> SuggestAsync(
            DesignContext context,
            CancellationToken cancellationToken = default)
        {
            var suggestions = new List<AgentSuggestion>();

            if (context.CurrentProposal != null)
            {
                // Suggest daylight improvements
                var darkRooms = FindUnderlitRooms(context);
                foreach (var room in darkRooms)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Improve daylighting in {room.Name}",
                        Description = $"Consider adding skylights or light shelves to increase daylight autonomy from {room.CurrentDA:P0} to target {room.TargetDA:P0}",
                        Priority = SuggestionPriority.Medium,
                        Confidence = 0.8f,
                        Impact = 0.7f,
                        Category = "Lighting"
                    });
                }

                // Suggest lighting controls
                var roomsNeedingControls = FindRoomsNeedingControls(context);
                foreach (var room in roomsNeedingControls)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Add occupancy sensors to {room}",
                        Description = "Install occupancy sensors to reduce energy consumption and meet code requirements",
                        Priority = SuggestionPriority.Low,
                        Confidence = 0.9f,
                        Impact = 0.4f,
                        Category = "Lighting Controls"
                    });
                }

                // Suggest glare mitigation
                var glareRisks = FindGlareRisks(context);
                foreach (var risk in glareRisks)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Add glare control to {risk.WindowId}",
                        Description = $"Install {risk.RecommendedSolution} to reduce glare from {risk.Direction}-facing window",
                        Priority = SuggestionPriority.Medium,
                        Confidence = 0.85f,
                        Impact = 0.5f,
                        Category = "Glare Control"
                    });
                }
            }

            await Task.CompletedTask;
            return suggestions;
        }

        public void ReceiveFeedback(AgentOpinion otherOpinion)
        {
            _lastReceivedFeedback = otherOpinion;

            // Consider sustainability agent feedback for energy-efficient lighting
            if (otherOpinion.AgentSpecialty == "Sustainability")
            {
                Logger.Debug("Incorporating sustainability concerns into lighting recommendations");
            }
        }

        #region Private Evaluation Methods

        private List<DesignIssue> EvaluateDaylighting(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();

            foreach (var room in rooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType") as string ?? "General";
                var windowArea = room.Parameters.GetValueOrDefault("WindowArea") as double? ?? 0;
                var floorArea = room.Parameters.GetValueOrDefault("FloorArea") as double? ?? 1;
                var daylightAutonomy = room.Parameters.GetValueOrDefault("DaylightAutonomy") as double? ?? 0;

                if (_roomRequirements.TryGetValue(roomType, out var req))
                {
                    // Check daylight factor
                    var wfr = windowArea / floorArea;
                    if (wfr < req.MinWindowFloorRatio)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "LTG-001",
                            Description = $"Window-to-floor ratio ({wfr:P1}) below minimum for {roomType} ({req.MinWindowFloorRatio:P1})",
                            Domain = "Daylighting",
                            Severity = IssueSeverity.Major,
                            ElementId = room.ElementId,
                            Recommendation = "Increase window area or add skylights"
                        });
                    }

                    // Check daylight autonomy for LEED compliance
                    if (daylightAutonomy < req.MinDaylightAutonomy)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "LTG-002",
                            Description = $"Daylight autonomy ({daylightAutonomy:P0}) below target for {roomType} ({req.MinDaylightAutonomy:P0})",
                            Domain = "Daylighting",
                            Severity = IssueSeverity.Warning,
                            ElementId = room.ElementId
                        });
                    }
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateArtificialLighting(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();

            foreach (var room in rooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType") as string ?? "General";
                var illuminanceLevel = room.Parameters.GetValueOrDefault("DesignIlluminance") as double? ?? 0;

                if (_roomRequirements.TryGetValue(roomType, out var req))
                {
                    if (illuminanceLevel < req.MinIlluminance)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "LTG-003",
                            Description = $"Design illuminance ({illuminanceLevel} lux) below minimum for {roomType} ({req.MinIlluminance} lux)",
                            Domain = "Artificial Lighting",
                            Severity = IssueSeverity.Major,
                            ElementId = room.ElementId,
                            Recommendation = "Add additional light fixtures or use higher output lamps"
                        });
                    }
                    else if (illuminanceLevel > req.MaxIlluminance)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "LTG-004",
                            Description = $"Design illuminance ({illuminanceLevel} lux) exceeds maximum for {roomType} ({req.MaxIlluminance} lux) - potential energy waste",
                            Domain = "Artificial Lighting",
                            Severity = IssueSeverity.Warning,
                            ElementId = room.ElementId
                        });
                    }
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateLightingPowerDensity(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();

            foreach (var room in rooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType") as string ?? "General";
                var totalWattage = room.Parameters.GetValueOrDefault("LightingWattage") as double? ?? 0;
                var floorArea = room.Parameters.GetValueOrDefault("FloorArea") as double? ?? 1;

                var actualLPD = totalWattage / floorArea;

                if (_lightingPowerDensity.TryGetValue(roomType, out var maxLPD))
                {
                    if (actualLPD > maxLPD)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "LTG-005",
                            Description = $"Lighting power density ({actualLPD:F1} W/m²) exceeds ASHRAE 90.1 limit ({maxLPD} W/m²) for {roomType}",
                            Domain = "Energy Compliance",
                            Severity = IssueSeverity.Critical,
                            ElementId = room.ElementId,
                            Recommendation = "Use more efficient LED fixtures to reduce LPD"
                        });
                    }
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateGlareControl(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var windows = proposal.Elements?.Where(e => e.Type == "Window") ?? Enumerable.Empty<ProposalElement>();

            foreach (var window in windows)
            {
                var orientation = window.Parameters.GetValueOrDefault("Orientation") as string;
                var hasShading = window.Parameters.GetValueOrDefault("HasShading") as bool? ?? false;
                var windowArea = window.Parameters.GetValueOrDefault("Area") as double? ?? 0;

                // West and south windows need glare control
                if ((orientation == "West" || orientation == "South") && !hasShading && windowArea > 2)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "LTG-006",
                        Description = $"Large {orientation}-facing window lacks glare control",
                        Domain = "Glare",
                        Severity = IssueSeverity.Warning,
                        ElementId = window.ElementId,
                        Recommendation = orientation == "West"
                            ? "Install vertical fins or automated blinds"
                            : "Install horizontal shading or light shelves"
                    });
                }
            }

            return issues;
        }

        private bool HasGoodDaylightIntegration(DesignProposal proposal)
        {
            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();
            var roomsWithDaylightControls = rooms.Count(r =>
                r.Parameters.GetValueOrDefault("HasDaylightSensor") as bool? ?? false);
            return roomsWithDaylightControls > rooms.Count() * 0.5;
        }

        private bool MeetsEnergyCodeRequirements(DesignProposal proposal)
        {
            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();
            return rooms.All(r =>
            {
                var roomType = r.Parameters.GetValueOrDefault("RoomType") as string ?? "General";
                var totalWattage = r.Parameters.GetValueOrDefault("LightingWattage") as double? ?? 0;
                var floorArea = r.Parameters.GetValueOrDefault("FloorArea") as double? ?? 1;
                var actualLPD = totalWattage / floorArea;
                return !_lightingPowerDensity.TryGetValue(roomType, out var maxLPD) || actualLPD <= maxLPD;
            });
        }

        private bool HasProperTaskLighting(DesignProposal proposal)
        {
            var workspaces = proposal.Elements?.Where(e =>
                e.Type == "Room" &&
                (e.Parameters.GetValueOrDefault("RoomType") as string == "Office" ||
                 e.Parameters.GetValueOrDefault("RoomType") as string == "Classroom")) ?? Enumerable.Empty<ProposalElement>();

            return workspaces.All(w =>
            {
                var illuminance = w.Parameters.GetValueOrDefault("DesignIlluminance") as double? ?? 0;
                return illuminance >= 300; // Minimum for task work
            });
        }

        private IEnumerable<RoomDaylightInfo> FindUnderlitRooms(DesignContext context)
        {
            return Enumerable.Empty<RoomDaylightInfo>();
        }

        private IEnumerable<string> FindRoomsNeedingControls(DesignContext context)
        {
            return Enumerable.Empty<string>();
        }

        private IEnumerable<GlareRisk> FindGlareRisks(DesignContext context)
        {
            return Enumerable.Empty<GlareRisk>();
        }

        #endregion

        #region Initialization

        private Dictionary<string, LightingRequirements> InitializeRoomRequirements()
        {
            return new Dictionary<string, LightingRequirements>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = new LightingRequirements { MinIlluminance = 300, MaxIlluminance = 500, MinWindowFloorRatio = 0.1, MinDaylightAutonomy = 0.55 },
                ["Open Office"] = new LightingRequirements { MinIlluminance = 300, MaxIlluminance = 500, MinWindowFloorRatio = 0.08, MinDaylightAutonomy = 0.55 },
                ["Classroom"] = new LightingRequirements { MinIlluminance = 300, MaxIlluminance = 500, MinWindowFloorRatio = 0.12, MinDaylightAutonomy = 0.55 },
                ["Conference Room"] = new LightingRequirements { MinIlluminance = 300, MaxIlluminance = 500, MinWindowFloorRatio = 0.08, MinDaylightAutonomy = 0.50 },
                ["Corridor"] = new LightingRequirements { MinIlluminance = 100, MaxIlluminance = 200, MinWindowFloorRatio = 0, MinDaylightAutonomy = 0 },
                ["Lobby"] = new LightingRequirements { MinIlluminance = 200, MaxIlluminance = 300, MinWindowFloorRatio = 0.1, MinDaylightAutonomy = 0.50 },
                ["Bedroom"] = new LightingRequirements { MinIlluminance = 100, MaxIlluminance = 300, MinWindowFloorRatio = 0.1, MinDaylightAutonomy = 0.40 },
                ["Bathroom"] = new LightingRequirements { MinIlluminance = 200, MaxIlluminance = 400, MinWindowFloorRatio = 0.05, MinDaylightAutonomy = 0 },
                ["Kitchen"] = new LightingRequirements { MinIlluminance = 300, MaxIlluminance = 500, MinWindowFloorRatio = 0.1, MinDaylightAutonomy = 0.30 },
                ["Laboratory"] = new LightingRequirements { MinIlluminance = 500, MaxIlluminance = 750, MinWindowFloorRatio = 0.1, MinDaylightAutonomy = 0.40 },
                ["Storage"] = new LightingRequirements { MinIlluminance = 100, MaxIlluminance = 200, MinWindowFloorRatio = 0, MinDaylightAutonomy = 0 }
            };
        }

        private Dictionary<string, double> InitializeLPDLimits()
        {
            // ASHRAE 90.1-2019 Space-by-Space Method (W/m²)
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office"] = 10.76,         // 1.0 W/ft²
                ["Open Office"] = 9.69,      // 0.9 W/ft²
                ["Conference Room"] = 12.92, // 1.2 W/ft²
                ["Classroom"] = 12.92,       // 1.2 W/ft²
                ["Corridor"] = 7.53,         // 0.7 W/ft²
                ["Lobby"] = 12.92,           // 1.2 W/ft²
                ["Restroom"] = 10.76,        // 1.0 W/ft²
                ["Storage"] = 6.46,          // 0.6 W/ft²
                ["Laboratory"] = 16.15,      // 1.5 W/ft²
                ["Retail"] = 16.15           // 1.5 W/ft²
            };
        }

        #endregion

        #region Supporting Types

        private class LightingRequirements
        {
            public double MinIlluminance { get; set; }
            public double MaxIlluminance { get; set; }
            public double MinWindowFloorRatio { get; set; }
            public double MinDaylightAutonomy { get; set; }
        }

        private class RoomDaylightInfo
        {
            public string Name { get; set; }
            public double CurrentDA { get; set; }
            public double TargetDA { get; set; }
        }

        private class GlareRisk
        {
            public string WindowId { get; set; }
            public string Direction { get; set; }
            public string RecommendedSolution { get; set; }
        }

        #endregion
    }
}
