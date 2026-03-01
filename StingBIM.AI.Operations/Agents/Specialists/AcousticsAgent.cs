// ============================================================================
// StingBIM AI Agents - Acoustics Agent
// Specialist agent for acoustic design evaluation and recommendations
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
    /// Specialist agent for acoustic design evaluation.
    /// Evaluates sound transmission, reverberation, and noise control.
    /// </summary>
    public class AcousticsAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public string AgentId => "AcousticsAgent";
        public string Specialty => "Acoustics";
        public float ExpertiseLevel => 0.85f;
        public bool IsActive { get; set; } = true;

        // Acoustic standards and thresholds
        private readonly Dictionary<string, AcousticRequirements> _roomRequirements;
        private readonly Dictionary<string, double> _stcRatings;
        private AgentOpinion _lastReceivedFeedback;

        public AcousticsAgent()
        {
            _roomRequirements = InitializeRoomRequirements();
            _stcRatings = InitializeSTCRatings();
        }

        public async Task<AgentOpinion> EvaluateAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Acoustics evaluation for proposal: {proposal.ProposalId}");

            var opinion = new AgentOpinion
            {
                AgentId = AgentId,
                AgentSpecialty = Specialty,
                Confidence = ExpertiseLevel
            };

            var issues = new List<DesignIssue>();
            var strengths = new List<string>();
            var score = 1.0f;

            // Evaluate wall STC ratings
            var wallIssues = EvaluateWallAcoustics(proposal);
            issues.AddRange(wallIssues);
            score -= wallIssues.Count * 0.1f;

            // Evaluate room acoustics
            var roomIssues = EvaluateRoomAcoustics(proposal);
            issues.AddRange(roomIssues);
            score -= roomIssues.Count * 0.1f;

            // Evaluate HVAC noise
            var hvacIssues = EvaluateHVACNoise(proposal);
            issues.AddRange(hvacIssues);
            score -= hvacIssues.Count * 0.1f;

            // Check for acoustic strengths
            if (HasAdequateSoundIsolation(proposal))
            {
                strengths.Add("Adequate sound isolation between spaces");
            }

            if (HasProperReverberationControl(proposal))
            {
                strengths.Add("Good reverberation control measures");
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

            // Validate wall modifications for acoustic impact
            if (action.ActionType == "ModifyWall" || action.ActionType == "CreateWall")
            {
                var stcRating = action.Parameters.GetValueOrDefault("STCRating") as double? ?? 0;
                var adjacentRooms = action.Parameters.GetValueOrDefault("AdjacentRooms") as List<string>;

                if (adjacentRooms != null)
                {
                    foreach (var room in adjacentRooms)
                    {
                        if (_roomRequirements.TryGetValue(room, out var req))
                        {
                            if (stcRating < req.MinimumSTC)
                            {
                                result.IsValid = false;
                                result.Issues.Add(new DesignIssue
                                {
                                    Code = "ACO-001",
                                    Description = $"Wall STC rating ({stcRating}) insufficient for {room}. Minimum: {req.MinimumSTC}",
                                    Domain = "Acoustics",
                                    Severity = IssueSeverity.Major
                                });
                            }
                        }
                    }
                }
            }

            // Validate door specifications for acoustic sealing
            if (action.ActionType == "CreateDoor")
            {
                var hasAcousticSeal = action.Parameters.GetValueOrDefault("HasAcousticSeal") as bool? ?? false;
                var roomType = action.Parameters.GetValueOrDefault("RoomType") as string;

                if (roomType != null && _roomRequirements.TryGetValue(roomType, out var req))
                {
                    if (req.RequiresAcousticDoor && !hasAcousticSeal)
                    {
                        result.Warnings.Add($"Acoustic door seal recommended for {roomType}");
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

            // Suggest acoustic improvements based on room types
            if (context.CurrentProposal != null)
            {
                // Check for rooms needing acoustic treatment
                var sensitiveRooms = FindAcousticallySensitiveRooms(context);
                foreach (var room in sensitiveRooms)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Add acoustic treatment to {room}",
                        Description = $"Consider adding acoustic panels or ceiling treatment to improve speech intelligibility in {room}",
                        Priority = SuggestionPriority.Medium,
                        Confidence = 0.8f,
                        Impact = 0.6f,
                        Category = "Acoustics"
                    });
                }

                // Suggest wall upgrades for noise-sensitive adjacencies
                var problematicWalls = FindProblematicAdjacencies(context);
                foreach (var wall in problematicWalls)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Upgrade wall acoustic rating",
                        Description = $"Wall between {wall.Room1} and {wall.Room2} should be upgraded to STC {wall.RecommendedSTC}",
                        Priority = SuggestionPriority.High,
                        Confidence = 0.85f,
                        Impact = 0.7f,
                        Category = "Acoustics",
                        Modifications = new List<ProposalModification>
                        {
                            new ProposalModification
                            {
                                Type = ModificationType.Modify,
                                ElementId = wall.WallId,
                                Parameters = new Dictionary<string, object>
                                {
                                    ["STCRating"] = wall.RecommendedSTC
                                }
                            }
                        }
                    });
                }
            }

            await Task.CompletedTask;
            return suggestions;
        }

        public void ReceiveFeedback(AgentOpinion otherOpinion)
        {
            _lastReceivedFeedback = otherOpinion;

            // Adjust recommendations based on other agent feedback
            if (otherOpinion.AgentSpecialty == "Cost" && otherOpinion.Score < 0.5f)
            {
                Logger.Debug("Received cost concern - will prioritize cost-effective acoustic solutions");
            }
        }

        #region Private Evaluation Methods

        private List<DesignIssue> EvaluateWallAcoustics(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            // Check wall assemblies for STC ratings
            var walls = proposal.Elements?.Where(e => e.Type == "Wall") ?? Enumerable.Empty<ProposalElement>();

            foreach (var wall in walls)
            {
                var stcRating = wall.Parameters.GetValueOrDefault("STCRating") as double? ?? 35; // Default STC
                var wallLocation = wall.Parameters.GetValueOrDefault("Location") as string;

                // Party walls need higher STC
                if (wallLocation?.Contains("Party") == true && stcRating < 50)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ACO-002",
                        Description = $"Party wall STC rating ({stcRating}) below minimum requirement (50)",
                        Domain = "Acoustics",
                        Severity = IssueSeverity.Critical,
                        ElementId = wall.ElementId,
                        Recommendation = "Upgrade wall assembly to achieve STC 50 or higher"
                    });
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateRoomAcoustics(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();

            foreach (var room in rooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType") as string ?? "General";
                var volume = room.Parameters.GetValueOrDefault("Volume") as double? ?? 0;
                var absorptionArea = room.Parameters.GetValueOrDefault("AbsorptionArea") as double? ?? 0;

                if (_roomRequirements.TryGetValue(roomType, out var requirements))
                {
                    // Calculate reverberation time (simplified Sabine formula)
                    if (volume > 0 && absorptionArea > 0)
                    {
                        var rt60 = 0.161 * volume / absorptionArea;

                        if (rt60 > requirements.MaxRT60)
                        {
                            issues.Add(new DesignIssue
                            {
                                Code = "ACO-003",
                                Description = $"Reverberation time ({rt60:F2}s) exceeds maximum for {roomType} ({requirements.MaxRT60}s)",
                                Domain = "Acoustics",
                                Severity = IssueSeverity.Major,
                                ElementId = room.ElementId,
                                Recommendation = "Add acoustic absorption materials to reduce reverberation"
                            });
                        }
                    }

                    // Check background noise level
                    var ncRating = room.Parameters.GetValueOrDefault("NCRating") as int? ?? 40;
                    if (ncRating > requirements.MaxNC)
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "ACO-004",
                            Description = $"Background noise (NC-{ncRating}) exceeds maximum for {roomType} (NC-{requirements.MaxNC})",
                            Domain = "Acoustics",
                            Severity = IssueSeverity.Major,
                            ElementId = room.ElementId
                        });
                    }
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateHVACNoise(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            // Check for HVAC equipment near noise-sensitive spaces
            var hvacEquipment = proposal.Elements?.Where(e =>
                e.Type == "AirTerminal" || e.Type == "MechanicalEquipment") ?? Enumerable.Empty<ProposalElement>();

            foreach (var equip in hvacEquipment)
            {
                var soundPowerLevel = equip.Parameters.GetValueOrDefault("SoundPowerLevel") as double? ?? 0;
                var adjacentRoom = equip.Parameters.GetValueOrDefault("ServesRoom") as string;

                if (adjacentRoom != null && _roomRequirements.TryGetValue(adjacentRoom, out var req))
                {
                    // Simplified noise transmission check
                    if (soundPowerLevel > req.MaxNC + 20) // Rule of thumb
                    {
                        issues.Add(new DesignIssue
                        {
                            Code = "ACO-005",
                            Description = $"HVAC equipment noise may exceed NC requirements for {adjacentRoom}",
                            Domain = "Acoustics",
                            Severity = IssueSeverity.Warning,
                            ElementId = equip.ElementId,
                            Recommendation = "Consider adding silencer or relocating equipment"
                        });
                    }
                }
            }

            return issues;
        }

        private bool HasAdequateSoundIsolation(DesignProposal proposal)
        {
            var walls = proposal.Elements?.Where(e => e.Type == "Wall") ?? Enumerable.Empty<ProposalElement>();
            var adequateCount = walls.Count(w =>
            {
                var stc = w.Parameters.GetValueOrDefault("STCRating") as double? ?? 0;
                return stc >= 45;
            });
            return adequateCount > walls.Count() * 0.7;
        }

        private bool HasProperReverberationControl(DesignProposal proposal)
        {
            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();
            return rooms.Any(r => r.Parameters.ContainsKey("AcousticTreatment"));
        }

        private IEnumerable<string> FindAcousticallySensitiveRooms(DesignContext context)
        {
            var sensitiveTypes = new[] { "Conference Room", "Theater", "Studio", "Library", "Classroom" };
            return context.CurrentProposal?.Elements?
                .Where(e => e.Type == "Room" &&
                    sensitiveTypes.Contains(e.Parameters.GetValueOrDefault("RoomType") as string))
                .Select(e => e.Parameters.GetValueOrDefault("RoomType") as string)
                .Distinct() ?? Enumerable.Empty<string>();
        }

        private IEnumerable<WallAdjacency> FindProblematicAdjacencies(DesignContext context)
        {
            // Simplified - in real implementation would analyze room adjacencies
            return Enumerable.Empty<WallAdjacency>();
        }

        #endregion

        #region Initialization

        private Dictionary<string, AcousticRequirements> InitializeRoomRequirements()
        {
            return new Dictionary<string, AcousticRequirements>(StringComparer.OrdinalIgnoreCase)
            {
                ["Conference Room"] = new AcousticRequirements { MinimumSTC = 50, MaxRT60 = 0.8, MaxNC = 30, RequiresAcousticDoor = true },
                ["Private Office"] = new AcousticRequirements { MinimumSTC = 45, MaxRT60 = 0.6, MaxNC = 35, RequiresAcousticDoor = false },
                ["Open Office"] = new AcousticRequirements { MinimumSTC = 35, MaxRT60 = 0.8, MaxNC = 40, RequiresAcousticDoor = false },
                ["Classroom"] = new AcousticRequirements { MinimumSTC = 50, MaxRT60 = 0.6, MaxNC = 25, RequiresAcousticDoor = true },
                ["Library"] = new AcousticRequirements { MinimumSTC = 50, MaxRT60 = 0.8, MaxNC = 30, RequiresAcousticDoor = true },
                ["Theater"] = new AcousticRequirements { MinimumSTC = 60, MaxRT60 = 1.5, MaxNC = 20, RequiresAcousticDoor = true },
                ["Studio"] = new AcousticRequirements { MinimumSTC = 65, MaxRT60 = 0.4, MaxNC = 15, RequiresAcousticDoor = true },
                ["Hospital Room"] = new AcousticRequirements { MinimumSTC = 50, MaxRT60 = 0.6, MaxNC = 30, RequiresAcousticDoor = true },
                ["Bedroom"] = new AcousticRequirements { MinimumSTC = 50, MaxRT60 = 0.5, MaxNC = 30, RequiresAcousticDoor = false }
            };
        }

        private Dictionary<string, double> InitializeSTCRatings()
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Single Layer Drywall"] = 33,
                ["Double Layer Drywall"] = 40,
                ["Staggered Stud Wall"] = 50,
                ["Double Stud Wall"] = 55,
                ["Concrete Block 8in"] = 50,
                ["Concrete 6in"] = 55,
                ["Concrete 8in"] = 58
            };
        }

        #endregion

        #region Supporting Types

        private class AcousticRequirements
        {
            public int MinimumSTC { get; set; }
            public double MaxRT60 { get; set; }
            public int MaxNC { get; set; }
            public bool RequiresAcousticDoor { get; set; }
        }

        private class WallAdjacency
        {
            public string WallId { get; set; }
            public string Room1 { get; set; }
            public string Room2 { get; set; }
            public int RecommendedSTC { get; set; }
        }

        #endregion
    }
}
