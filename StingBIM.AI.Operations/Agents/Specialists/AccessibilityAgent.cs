// ============================================================================
// StingBIM AI Agents - Accessibility Agent
// Specialist agent for ADA/accessibility compliance evaluation
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
    /// Specialist agent for accessibility compliance evaluation.
    /// Evaluates ADA, Fair Housing, and universal design requirements.
    /// </summary>
    public class AccessibilityAgent : IDesignAgent
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public string AgentId => "AccessibilityAgent";
        public string Specialty => "Accessibility";
        public float ExpertiseLevel => 0.9f;
        public bool IsActive { get; set; } = true;

        // ADA and accessibility requirements
        private readonly Dictionary<string, AccessibilityRequirement> _requirements;
        private AgentOpinion _lastReceivedFeedback;

        public AccessibilityAgent()
        {
            _requirements = InitializeRequirements();
        }

        public async Task<AgentOpinion> EvaluateAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Accessibility evaluation for proposal: {proposal.ProposalId}");

            var opinion = new AgentOpinion
            {
                AgentId = AgentId,
                AgentSpecialty = Specialty,
                Confidence = ExpertiseLevel
            };

            var issues = new List<DesignIssue>();
            var strengths = new List<string>();
            var score = 1.0f;

            // Evaluate accessible routes
            var routeIssues = EvaluateAccessibleRoutes(proposal);
            issues.AddRange(routeIssues);
            score -= routeIssues.Count(i => i.Severity == IssueSeverity.Critical) * 0.2f;
            score -= routeIssues.Count(i => i.Severity == IssueSeverity.Major) * 0.1f;

            // Evaluate door accessibility
            var doorIssues = EvaluateDoorAccessibility(proposal);
            issues.AddRange(doorIssues);
            score -= doorIssues.Count(i => i.Severity == IssueSeverity.Critical) * 0.2f;

            // Evaluate restroom accessibility
            var restroomIssues = EvaluateRestroomAccessibility(proposal);
            issues.AddRange(restroomIssues);
            score -= restroomIssues.Count(i => i.Severity == IssueSeverity.Critical) * 0.2f;

            // Evaluate vertical circulation
            var verticalIssues = EvaluateVerticalCirculation(proposal);
            issues.AddRange(verticalIssues);
            score -= verticalIssues.Count * 0.15f;

            // Evaluate signage and wayfinding
            var signageIssues = EvaluateSignageAndWayfinding(proposal);
            issues.AddRange(signageIssues);
            score -= signageIssues.Count * 0.05f;

            // Check for accessibility strengths
            if (HasUniversalDesignFeatures(proposal))
            {
                strengths.Add("Universal design features exceed minimum requirements");
            }

            if (HasAccessibleParkingCompliance(proposal))
            {
                strengths.Add("Accessible parking meets ADA requirements");
            }

            if (HasProperWayfinding(proposal))
            {
                strengths.Add("Tactile and visual wayfinding provided");
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

            // Validate door width and clearances
            if (action.ActionType == "CreateDoor" || action.ActionType == "ModifyDoor")
            {
                ValidateDoorAction(action, result);
            }

            // Validate ramp specifications
            if (action.ActionType == "CreateRamp" || action.ActionType == "ModifyRamp")
            {
                ValidateRampAction(action, result);
            }

            // Validate corridor width
            if (action.ActionType == "CreateCorridor" || action.ActionType == "ModifyCorridor")
            {
                ValidateCorridorAction(action, result);
            }

            // Validate restroom layout
            if (action.ActionType == "CreateRestroom")
            {
                ValidateRestroomAction(action, result);
            }

            // Validate stair handrails
            if (action.ActionType == "CreateStair" || action.ActionType == "ModifyStair")
            {
                ValidateStairAction(action, result);
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
                // Suggest accessible route improvements
                var routeGaps = FindAccessibleRouteGaps(context);
                foreach (var gap in routeGaps)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = "Complete accessible route",
                        Description = $"Add accessible route between {gap.From} and {gap.To}",
                        Priority = SuggestionPriority.Critical,
                        Confidence = 0.95f,
                        Impact = 0.9f,
                        Category = "Accessible Routes"
                    });
                }

                // Suggest door hardware upgrades
                var nonCompliantDoors = FindNonCompliantDoors(context);
                foreach (var door in nonCompliantDoors)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Upgrade door hardware at {door.Location}",
                        Description = "Replace round door knob with lever handle for accessibility",
                        Priority = SuggestionPriority.High,
                        Confidence = 0.9f,
                        Impact = 0.6f,
                        Category = "Door Accessibility",
                        Modifications = new List<ProposalModification>
                        {
                            new ProposalModification
                            {
                                Type = ModificationType.Modify,
                                ElementId = door.DoorId,
                                Parameters = new Dictionary<string, object>
                                {
                                    ["HardwareType"] = "Lever",
                                    ["IsAccessible"] = true
                                }
                            }
                        }
                    });
                }

                // Suggest signage improvements
                var areasNeedingSignage = FindAreasNeedingSignage(context);
                foreach (var area in areasNeedingSignage)
                {
                    suggestions.Add(new AgentSuggestion
                    {
                        AgentId = AgentId,
                        Title = $"Add accessible signage at {area}",
                        Description = "Install tactile signage with Braille and raised characters",
                        Priority = SuggestionPriority.Medium,
                        Confidence = 0.85f,
                        Impact = 0.4f,
                        Category = "Signage"
                    });
                }
            }

            await Task.CompletedTask;
            return suggestions;
        }

        public void ReceiveFeedback(AgentOpinion otherOpinion)
        {
            _lastReceivedFeedback = otherOpinion;

            // Safety agent concerns take priority
            if (otherOpinion.AgentSpecialty == "Safety" && otherOpinion.HasCriticalIssues)
            {
                Logger.Info("Safety concerns may affect accessible egress - coordinating requirements");
            }
        }

        #region Private Validation Methods

        private void ValidateDoorAction(DesignAction action, ValidationResult result)
        {
            // ADA 404.2.3 - Clear Width
            var clearWidth = action.Parameters.GetValueOrDefault("ClearWidth") as double? ?? 0;
            if (clearWidth < 0.815) // 32 inches minimum
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-404.2.3",
                    Description = $"Door clear width ({clearWidth:F3}m) below minimum 0.815m (32\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 404.2.4 - Maneuvering Clearances
            var maneuveringDepth = action.Parameters.GetValueOrDefault("ManeuveringDepth") as double? ?? 0;
            var doorSwing = action.Parameters.GetValueOrDefault("DoorSwing") as string ?? "Push";
            var requiredDepth = doorSwing == "Pull" ? 1.525 : 1.22; // 60" for pull, 48" for push

            if (maneuveringDepth < requiredDepth)
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-404.2.4",
                    Description = $"Maneuvering clearance ({maneuveringDepth:F2}m) insufficient for {doorSwing} door. Required: {requiredDepth}m",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 404.2.7 - Door Hardware
            var hardwareType = action.Parameters.GetValueOrDefault("HardwareType") as string;
            if (hardwareType == "Knob")
            {
                result.Warnings.Add("Round door knobs not accessible - use lever handles or push/pull hardware");
            }

            // ADA 404.2.5 - Threshold
            var thresholdHeight = action.Parameters.GetValueOrDefault("ThresholdHeight") as double? ?? 0;
            if (thresholdHeight > 0.0127) // 1/2 inch
            {
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-404.2.5",
                    Description = $"Door threshold ({thresholdHeight:F3}m) exceeds maximum 0.013m (1/2\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Major
                });
            }

            // ADA 404.2.9 - Door Opening Force
            var openingForce = action.Parameters.GetValueOrDefault("OpeningForce") as double? ?? 0;
            if (openingForce > 22.2) // 5 lbf for interior doors
            {
                result.Warnings.Add($"Door opening force ({openingForce}N) may exceed 22.2N (5 lbf) limit");
            }
        }

        private void ValidateRampAction(DesignAction action, ValidationResult result)
        {
            // ADA 405.2 - Slope
            var slope = action.Parameters.GetValueOrDefault("Slope") as double? ?? 0;
            if (slope > 0.0833) // 1:12 maximum
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-405.2",
                    Description = $"Ramp slope ({slope * 100:F1}%) exceeds maximum 8.33% (1:12)",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 405.5 - Clear Width
            var clearWidth = action.Parameters.GetValueOrDefault("ClearWidth") as double? ?? 0;
            if (clearWidth < 0.915) // 36 inches
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-405.5",
                    Description = $"Ramp width ({clearWidth:F2}m) below minimum 0.915m (36\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 405.6 - Rise
            var rise = action.Parameters.GetValueOrDefault("Rise") as double? ?? 0;
            if (rise > 0.76) // 30 inches maximum rise before landing
            {
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-405.6",
                    Description = $"Ramp rise ({rise:F2}m) exceeds maximum 0.76m (30\") before landing required",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Major
                });
            }

            // ADA 405.8 - Handrails
            var hasHandrails = action.Parameters.GetValueOrDefault("HasHandrails") as bool? ?? false;
            if (rise > 0.15 && !hasHandrails) // Handrails required if rise > 6"
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-405.8",
                    Description = "Handrails required for ramps with rise greater than 0.15m (6\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 405.9 - Edge Protection
            var hasEdgeProtection = action.Parameters.GetValueOrDefault("HasEdgeProtection") as bool? ?? false;
            if (!hasEdgeProtection)
            {
                result.Warnings.Add("Edge protection (curb or barrier) recommended for ramps");
            }
        }

        private void ValidateCorridorAction(DesignAction action, ValidationResult result)
        {
            // ADA 403.5.1 - Clear Width
            var clearWidth = action.Parameters.GetValueOrDefault("ClearWidth") as double? ?? 0;
            if (clearWidth < 0.915) // 36 inches minimum
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-403.5.1",
                    Description = $"Corridor width ({clearWidth:F2}m) below minimum 0.915m (36\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // Check for passing space every 60m
            var length = action.Parameters.GetValueOrDefault("Length") as double? ?? 0;
            var hasPassingSpaces = action.Parameters.GetValueOrDefault("HasPassingSpaces") as bool? ?? false;
            if (length > 60 && !hasPassingSpaces && clearWidth < 1.525) // Need passing if not 60" wide
            {
                result.Warnings.Add("Passing spaces (1.5m x 1.5m) required every 60m if corridor < 1.525m wide");
            }

            // ADA 403.5.3 - Protruding Objects
            var hasProtrudingObjects = action.Parameters.GetValueOrDefault("HasProtrudingObjects") as bool? ?? false;
            if (hasProtrudingObjects)
            {
                result.Warnings.Add("Verify protruding objects comply with ADA 307 (max 100mm projection above 685mm)");
            }
        }

        private void ValidateRestroomAction(DesignAction action, ValidationResult result)
        {
            // ADA 604 - Water Closets
            var wcClearFloor = action.Parameters.GetValueOrDefault("WCClearFloorWidth") as double? ?? 0;
            if (wcClearFloor < 1.525) // 60 inches
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-604.3",
                    Description = $"Water closet clear floor space ({wcClearFloor:F2}m) below minimum 1.525m (60\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 604.5 - Grab Bars
            var hasSideGrabBar = action.Parameters.GetValueOrDefault("HasSideGrabBar") as bool? ?? false;
            var hasRearGrabBar = action.Parameters.GetValueOrDefault("HasRearGrabBar") as bool? ?? false;
            if (!hasSideGrabBar || !hasRearGrabBar)
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-604.5",
                    Description = "Accessible water closet requires side and rear grab bars",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }

            // ADA 606 - Lavatories
            var lavKneeSpace = action.Parameters.GetValueOrDefault("LavKneeSpace") as double? ?? 0;
            if (lavKneeSpace < 0.685) // 27 inches
            {
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-606.2",
                    Description = $"Lavatory knee clearance ({lavKneeSpace:F2}m) below minimum 0.685m (27\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Major
                });
            }

            // Turning space
            var turningDiameter = action.Parameters.GetValueOrDefault("TurningDiameter") as double? ?? 0;
            if (turningDiameter < 1.525) // 60 inches
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-603.2.1",
                    Description = $"Turning space ({turningDiameter:F2}m) below minimum 1.525m (60\") diameter",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }
        }

        private void ValidateStairAction(DesignAction action, ValidationResult result)
        {
            // ADA 504.2 - Treads and Risers
            var riserHeight = action.Parameters.GetValueOrDefault("RiserHeight") as double? ?? 0;
            if (riserHeight < 0.1 || riserHeight > 0.178) // 4" to 7"
            {
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-504.2",
                    Description = $"Riser height ({riserHeight:F3}m) outside range 0.1m-0.178m (4\"-7\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Major
                });
            }

            var treadDepth = action.Parameters.GetValueOrDefault("TreadDepth") as double? ?? 0;
            if (treadDepth < 0.28) // 11 inches minimum
            {
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-504.2",
                    Description = $"Tread depth ({treadDepth:F3}m) below minimum 0.28m (11\")",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Major
                });
            }

            // ADA 504.6 - Handrails
            var hasHandrails = action.Parameters.GetValueOrDefault("HasHandrailsBothSides") as bool? ?? false;
            if (!hasHandrails)
            {
                result.IsValid = false;
                result.Issues.Add(new DesignIssue
                {
                    Code = "ADA-504.6",
                    Description = "Handrails required on both sides of stairs",
                    Domain = "Accessibility",
                    Severity = IssueSeverity.Critical
                });
            }
        }

        #endregion

        #region Private Evaluation Methods

        private List<DesignIssue> EvaluateAccessibleRoutes(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            // Check for continuous accessible route from entrance to all spaces
            var publicSpaces = proposal.Elements?.Where(e =>
                e.Type == "Room" && IsPublicSpace(e)) ?? Enumerable.Empty<ProposalElement>();

            foreach (var space in publicSpaces)
            {
                var hasAccessibleRoute = space.Parameters.GetValueOrDefault("HasAccessibleRoute") as bool? ?? true;
                if (!hasAccessibleRoute)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ADA-206",
                        Description = $"No accessible route to {space.Parameters.GetValueOrDefault("RoomType")}",
                        Domain = "Accessible Routes",
                        Severity = IssueSeverity.Critical,
                        ElementId = space.ElementId
                    });
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateDoorAccessibility(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var doors = proposal.Elements?.Where(e => e.Type == "Door") ?? Enumerable.Empty<ProposalElement>();

            foreach (var door in doors)
            {
                var clearWidth = door.Parameters.GetValueOrDefault("ClearWidth") as double? ?? 0;
                var isOnAccessibleRoute = door.Parameters.GetValueOrDefault("IsOnAccessibleRoute") as bool? ?? true;

                if (isOnAccessibleRoute && clearWidth < 0.815)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ADA-404.2.3",
                        Description = $"Door clear width ({clearWidth:F2}m) below ADA minimum 0.815m",
                        Domain = "Door Accessibility",
                        Severity = IssueSeverity.Critical,
                        ElementId = door.ElementId
                    });
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateRestroomAccessibility(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var restrooms = proposal.Elements?.Where(e =>
                e.Type == "Room" &&
                (e.Parameters.GetValueOrDefault("RoomType") as string)?.Contains("Restroom") == true)
                ?? Enumerable.Empty<ProposalElement>();

            foreach (var restroom in restrooms)
            {
                var hasAccessibleFixtures = restroom.Parameters.GetValueOrDefault("HasAccessibleFixtures") as bool? ?? false;

                if (!hasAccessibleFixtures)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ADA-213",
                        Description = "Restroom requires accessible toilet compartment",
                        Domain = "Restroom Accessibility",
                        Severity = IssueSeverity.Critical,
                        ElementId = restroom.ElementId
                    });
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateVerticalCirculation(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var hasMultipleFloors = proposal.Elements?.Any(e =>
                e.Parameters.GetValueOrDefault("Level") as int? > 1) ?? false;

            if (hasMultipleFloors)
            {
                var hasElevator = proposal.Elements?.Any(e => e.Type == "Elevator") ?? false;
                var hasAccessibleRamp = proposal.Elements?.Any(e =>
                    e.Type == "Ramp" &&
                    (e.Parameters.GetValueOrDefault("Slope") as double? ?? 1) <= 0.0833) ?? false;

                if (!hasElevator && !hasAccessibleRamp)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ADA-206.2.3",
                        Description = "Multi-story building requires elevator or accessible ramp",
                        Domain = "Vertical Circulation",
                        Severity = IssueSeverity.Critical
                    });
                }
            }

            return issues;
        }

        private List<DesignIssue> EvaluateSignageAndWayfinding(DesignProposal proposal)
        {
            var issues = new List<DesignIssue>();

            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();

            foreach (var room in rooms)
            {
                var roomType = room.Parameters.GetValueOrDefault("RoomType") as string;
                var hasTactileSignage = room.Parameters.GetValueOrDefault("HasTactileSignage") as bool? ?? false;

                // Permanent rooms require tactile signage
                if (IsPermanentRoom(roomType) && !hasTactileSignage)
                {
                    issues.Add(new DesignIssue
                    {
                        Code = "ADA-216.2",
                        Description = $"Tactile signage required for {roomType}",
                        Domain = "Signage",
                        Severity = IssueSeverity.Warning,
                        ElementId = room.ElementId
                    });
                }
            }

            return issues;
        }

        private bool IsPublicSpace(ProposalElement element)
        {
            var roomType = element.Parameters.GetValueOrDefault("RoomType") as string ?? "";
            var publicTypes = new[] { "Lobby", "Corridor", "Conference", "Restroom", "Office", "Retail" };
            return publicTypes.Any(t => roomType.Contains(t));
        }

        private bool IsPermanentRoom(string roomType)
        {
            var permanentTypes = new[] { "Office", "Conference", "Restroom", "Mechanical", "Electrical" };
            return permanentTypes.Any(t => roomType?.Contains(t) == true);
        }

        private bool HasUniversalDesignFeatures(DesignProposal proposal)
        {
            var doors = proposal.Elements?.Where(e => e.Type == "Door") ?? Enumerable.Empty<ProposalElement>();
            var wideDoors = doors.Count(d => (d.Parameters.GetValueOrDefault("ClearWidth") as double? ?? 0) >= 0.915);
            return wideDoors > doors.Count() * 0.8;
        }

        private bool HasAccessibleParkingCompliance(DesignProposal proposal)
        {
            var parking = proposal.Elements?.Where(e => e.Type == "ParkingSpace") ?? Enumerable.Empty<ProposalElement>();
            var accessibleSpaces = parking.Count(p => p.Parameters.GetValueOrDefault("IsAccessible") as bool? ?? false);
            var totalSpaces = parking.Count();

            if (totalSpaces == 0) return true;

            // ADA requires different ratios based on total spaces
            var requiredAccessible = totalSpaces switch
            {
                <= 25 => 1,
                <= 50 => 2,
                <= 75 => 3,
                <= 100 => 4,
                _ => 4 + (totalSpaces - 100) / 50
            };

            return accessibleSpaces >= requiredAccessible;
        }

        private bool HasProperWayfinding(DesignProposal proposal)
        {
            var rooms = proposal.Elements?.Where(e => e.Type == "Room") ?? Enumerable.Empty<ProposalElement>();
            var roomsWithSignage = rooms.Count(r => r.Parameters.GetValueOrDefault("HasTactileSignage") as bool? ?? false);
            return roomsWithSignage > rooms.Count() * 0.7;
        }

        private IEnumerable<RouteGap> FindAccessibleRouteGaps(DesignContext context)
        {
            return Enumerable.Empty<RouteGap>();
        }

        private IEnumerable<NonCompliantDoor> FindNonCompliantDoors(DesignContext context)
        {
            return Enumerable.Empty<NonCompliantDoor>();
        }

        private IEnumerable<string> FindAreasNeedingSignage(DesignContext context)
        {
            return Enumerable.Empty<string>();
        }

        #endregion

        #region Initialization

        private Dictionary<string, AccessibilityRequirement> InitializeRequirements()
        {
            return new Dictionary<string, AccessibilityRequirement>(StringComparer.OrdinalIgnoreCase)
            {
                ["Door"] = new AccessibilityRequirement { MinClearWidth = 0.815, MaxThreshold = 0.013 },
                ["Corridor"] = new AccessibilityRequirement { MinClearWidth = 0.915 },
                ["Ramp"] = new AccessibilityRequirement { MaxSlope = 0.0833, MinClearWidth = 0.915 },
                ["Elevator"] = new AccessibilityRequirement { MinCabWidth = 1.73, MinCabDepth = 1.37 }
            };
        }

        #endregion

        #region Supporting Types

        private class AccessibilityRequirement
        {
            public double MinClearWidth { get; set; }
            public double MaxThreshold { get; set; }
            public double MaxSlope { get; set; }
            public double MinCabWidth { get; set; }
            public double MinCabDepth { get; set; }
        }

        private class RouteGap
        {
            public string From { get; set; }
            public string To { get; set; }
        }

        private class NonCompliantDoor
        {
            public string DoorId { get; set; }
            public string Location { get; set; }
        }

        #endregion
    }
}
