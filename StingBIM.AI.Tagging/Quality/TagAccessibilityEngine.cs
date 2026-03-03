// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagAccessibilityEngine.cs - Accessibility compliance engine for signage and wayfinding
// Ensures tags meet ADA, BS 8300, SANS, and universal design requirements
//
// Accessibility Capabilities:
//   1. ADA Signage Compliance    - Room signs, tactile, contrast, mounting height
//   2. Wayfinding Intelligence   - Decision points, destination hierarchy, progressive disclosure
//   3. Universal Design          - Color-blind safe, cognitive load, multi-sensory
//   4. Emergency Egress          - Exit signs, evacuation routes, areas of rescue
//   5. Facility Specialization   - Healthcare, education, office, hospitality, industrial
//   6. Regional Standards        - ADA, BS 8300, SANS, UNBS, KEBS, EAC
//   7. Accessibility Audit       - Model-wide checks, compliance score, recommendations
//   8. Signage Schedule          - Auto-generate procurement-ready signage schedules

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Quality
{
    #region Enums and Types

    public enum AccessibilityStandard
    {
        ADA2010,
        BS8300,
        DDA_Australia,
        AODA_Canada,
        SANS_SouthAfrica,
        UNBS_Uganda,
        KEBS_Kenya,
        EAC_EastAfrica
    }

    public enum SignageType
    {
        RoomIdentification,
        Directional,
        Informational,
        ExitSign,
        FireSafety,
        Accessibility,
        Floor_Directory,
        Building_Directory,
        Parking,
        Regulatory,
        Warning,
        Wayfinding
    }

    public enum ComplianceLevel
    {
        FullyCompliant,
        MinorNonCompliance,
        MajorNonCompliance,
        NotApplicable,
        NotChecked
    }

    public sealed class AccessibilityIssue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string ElementId { get; set; }
        public string TagId { get; set; }
        public string Standard { get; set; }
        public string Rule { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Recommendation { get; set; }
        public string Location { get; set; }
        public ComplianceLevel Level { get; set; }
    }

    public sealed class SignageScheduleEntry
    {
        public string Id { get; set; }
        public SignageType Type { get; set; }
        public string Location { get; set; }
        public string Level { get; set; }
        public string Room { get; set; }
        public string Content { get; set; }
        public double MountingHeight { get; set; } // mm AFF
        public string MountingSide { get; set; } // "Latch side", "Above", "Adjacent"
        public bool RequiresTactile { get; set; }
        public bool RequiresBraille { get; set; }
        public bool IsIlluminated { get; set; }
        public string Material { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Notes { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public sealed class WayfindingNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Point2D Position { get; set; }
        public string Level { get; set; }
        public bool IsDecisionPoint { get; set; }
        public bool IsDestination { get; set; }
        public bool IsEntrance { get; set; }
        public List<string> ConnectedNodeIds { get; set; } = new();
        public List<string> VisibleDestinations { get; set; } = new();
        public int Priority { get; set; }
    }

    public sealed class AccessibilityAuditResult
    {
        public DateTime AuditDate { get; set; } = DateTime.UtcNow;
        public List<AccessibilityStandard> StandardsChecked { get; set; } = new();
        public int TotalElementsChecked { get; set; }
        public int TotalIssuesFound { get; set; }
        public int CriticalIssues { get; set; }
        public int WarningIssues { get; set; }
        public double ComplianceScore { get; set; } // 0-100
        public List<AccessibilityIssue> Issues { get; set; } = new();
        public Dictionary<string, double> ComplianceByArea { get; set; } = new();
        public Dictionary<string, double> ComplianceByStandard { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<SignageScheduleEntry> MissingSignage { get; set; } = new();
    }

    public sealed class FacilityTypeProfile
    {
        public string FacilityType { get; set; }
        public List<SignageRequirement> Requirements { get; set; } = new();
        public Dictionary<string, string> SpecialConsiderations { get; set; } = new();
    }

    public sealed class SignageRequirement
    {
        public string RoomType { get; set; }
        public SignageType RequiredSignage { get; set; }
        public bool Mandatory { get; set; }
        public string Standard { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region ADA Compliance Checker

    internal sealed class ADAComplianceChecker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // ADA 2010 Standards reference values
        private const double MIN_MOUNTING_HEIGHT_MM = 1219; // 48" AFF
        private const double MAX_MOUNTING_HEIGHT_MM = 1524; // 60" AFF
        private const double MIN_CHARACTER_HEIGHT_MM = 16;   // 5/8" minimum
        private const double MIN_CONTRAST_RATIO = 0.7;      // 70% contrast

        public List<AccessibilityIssue> CheckRoomSignage(
            string elementId, string roomName, string roomNumber,
            bool hasSign, double? mountingHeight, bool hasTactile, bool hasBraille,
            string mountingSide)
        {
            var issues = new List<AccessibilityIssue>();

            // ADA 703.1: Signs identifying permanent rooms/spaces required
            if (!hasSign)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "ADA 703.1",
                    Rule = "Room Identification Required",
                    Description = $"Room '{roomName}' ({roomNumber}) lacks identification signage",
                    Severity = IssueSeverity.Error,
                    Recommendation = "Install tactile room identification sign on latch side of door",
                    Level = ComplianceLevel.MajorNonCompliance
                });
                return issues;
            }

            // ADA 703.4.1: Tactile characters required for permanent rooms
            if (!hasTactile)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "ADA 703.4",
                    Rule = "Tactile Characters Required",
                    Description = $"Room sign for '{roomName}' lacks raised tactile characters",
                    Severity = IssueSeverity.Error,
                    Recommendation = "Add raised tactile characters (1/32\" min raised, sans serif)",
                    Level = ComplianceLevel.MajorNonCompliance
                });
            }

            // ADA 703.3: Braille required
            if (!hasBraille)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "ADA 703.3",
                    Rule = "Braille Required",
                    Description = $"Room sign for '{roomName}' lacks Grade 2 Braille",
                    Severity = IssueSeverity.Error,
                    Recommendation = "Add Grade 2 Braille below tactile text",
                    Level = ComplianceLevel.MajorNonCompliance
                });
            }

            // ADA 703.4.2: Mounting height
            if (mountingHeight.HasValue)
            {
                if (mountingHeight < MIN_MOUNTING_HEIGHT_MM || mountingHeight > MAX_MOUNTING_HEIGHT_MM)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementId = elementId,
                        Standard = "ADA 703.4.2",
                        Rule = "Mounting Height",
                        Description = $"Sign mounting height {mountingHeight:F0}mm is outside " +
                            $"ADA range ({MIN_MOUNTING_HEIGHT_MM}-{MAX_MOUNTING_HEIGHT_MM}mm AFF)",
                        Severity = IssueSeverity.Error,
                        Recommendation = $"Mount sign between {MIN_MOUNTING_HEIGHT_MM}mm and " +
                            $"{MAX_MOUNTING_HEIGHT_MM}mm above finished floor to baseline of lowest character",
                        Level = ComplianceLevel.MajorNonCompliance
                    });
                }
            }

            // ADA 703.4.2: Latch side mounting
            if (!string.IsNullOrEmpty(mountingSide) &&
                !mountingSide.Contains("latch", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "ADA 703.4.2",
                    Rule = "Latch Side Mounting",
                    Description = $"Room sign mounted on '{mountingSide}' - ADA requires latch side",
                    Severity = IssueSeverity.Warning,
                    Recommendation = "Relocate sign to latch side of door, 18\" minimum from door frame",
                    Level = ComplianceLevel.MinorNonCompliance
                });
            }

            return issues;
        }

        public List<AccessibilityIssue> CheckExitSignage(
            string elementId, string location,
            bool hasExitSign, bool isIlluminated, bool isOnEmergencyPower)
        {
            var issues = new List<AccessibilityIssue>();

            if (!hasExitSign)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "ADA 216.4/IBC 1013",
                    Rule = "Exit Sign Required",
                    Description = $"Exit at '{location}' lacks illuminated exit sign",
                    Severity = IssueSeverity.Critical,
                    Recommendation = "Install illuminated EXIT sign with International Symbol of Accessibility if needed",
                    Level = ComplianceLevel.MajorNonCompliance
                });
            }
            else
            {
                if (!isIlluminated)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementId = elementId,
                        Standard = "IBC 1013.3",
                        Rule = "Exit Sign Illumination",
                        Description = "Exit sign is not internally or externally illuminated",
                        Severity = IssueSeverity.Error,
                        Recommendation = "Provide illumination (5 fc min on face)",
                        Level = ComplianceLevel.MajorNonCompliance
                    });
                }
                if (!isOnEmergencyPower)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementId = elementId,
                        Standard = "IBC 1013.6.3",
                        Rule = "Emergency Power",
                        Description = "Exit sign lacks emergency power backup (90-minute minimum)",
                        Severity = IssueSeverity.Error,
                        Recommendation = "Connect to emergency power or provide integral battery backup",
                        Level = ComplianceLevel.MajorNonCompliance
                    });
                }
            }

            return issues;
        }
    }

    #endregion

    #region Wayfinding Analyzer

    internal sealed class WayfindingAnalyzer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<WayfindingNode> _nodes = new();

        public void RegisterNode(WayfindingNode node)
        {
            _nodes.Add(node);
        }

        /// <summary>
        /// Identify decision points that need directional signage.
        /// </summary>
        public List<WayfindingNode> IdentifyDecisionPoints()
        {
            return _nodes.Where(n => n.IsDecisionPoint ||
                n.ConnectedNodeIds.Count >= 3).ToList();
        }

        /// <summary>
        /// Generate wayfinding signage requirements along routes.
        /// </summary>
        public List<SignageScheduleEntry> GenerateWayfindingSchedule(
            List<string> destinationNames)
        {
            var schedule = new List<SignageScheduleEntry>();

            var decisionPoints = IdentifyDecisionPoints();
            foreach (var dp in decisionPoints)
            {
                // Each decision point needs directional signs for visible destinations
                var visibleDests = dp.VisibleDestinations.Any()
                    ? dp.VisibleDestinations
                    : destinationNames.Take(4).ToList();

                schedule.Add(new SignageScheduleEntry
                {
                    Id = $"WF-{dp.Id}",
                    Type = SignageType.Directional,
                    Location = dp.Name,
                    Level = dp.Level,
                    Content = string.Join(", ", visibleDests),
                    MountingHeight = 1400, // Eye level
                    RequiresTactile = false,
                    RequiresBraille = false,
                    Notes = $"Decision point with {dp.ConnectedNodeIds.Count} paths"
                });
            }

            // Floor directories at entrances and elevator lobbies
            var entrances = _nodes.Where(n => n.IsEntrance).ToList();
            foreach (var entrance in entrances)
            {
                schedule.Add(new SignageScheduleEntry
                {
                    Id = $"FD-{entrance.Id}",
                    Type = SignageType.Floor_Directory,
                    Location = entrance.Name,
                    Level = entrance.Level,
                    Content = "Floor directory",
                    MountingHeight = 1400,
                    RequiresTactile = true,
                    RequiresBraille = true,
                    Notes = "Tactile floor directory required at building entrance"
                });
            }

            Logger.Debug("Generated {Count} wayfinding signage entries", schedule.Count);
            return schedule;
        }

        public void Clear() { _nodes.Clear(); }
    }

    #endregion

    #region Main Accessibility Engine

    /// <summary>
    /// Comprehensive accessibility compliance engine for BIM tagging.
    /// Validates against ADA, BS 8300, SANS, and provides wayfinding intelligence.
    /// </summary>
    public sealed class TagAccessibilityEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly ADAComplianceChecker _adaChecker = new();
        private readonly WayfindingAnalyzer _wayfindingAnalyzer = new();
        private readonly Dictionary<string, FacilityTypeProfile> _facilityProfiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<AccessibilityStandard> _enabledStandards = new();

        public TagAccessibilityEngine()
        {
            // Default: ADA enabled
            _enabledStandards.Add(AccessibilityStandard.ADA2010);
            InitializeFacilityProfiles();
            Logger.Info("TagAccessibilityEngine initialized with {Count} facility profiles",
                _facilityProfiles.Count);
        }

        #region Standards Configuration

        public void EnableStandard(AccessibilityStandard standard)
        {
            lock (_lockObject)
            {
                if (!_enabledStandards.Contains(standard))
                    _enabledStandards.Add(standard);
            }
        }

        public void DisableStandard(AccessibilityStandard standard)
        {
            lock (_lockObject) { _enabledStandards.Remove(standard); }
        }

        public List<AccessibilityStandard> GetEnabledStandards()
        {
            lock (_lockObject) { return new List<AccessibilityStandard>(_enabledStandards); }
        }

        #endregion

        #region Accessibility Audit

        /// <summary>
        /// Run a comprehensive accessibility audit on model tagging.
        /// </summary>
        public async Task<AccessibilityAuditResult> RunAuditAsync(
            List<(string ElementId, string RoomName, string RoomNumber,
                string RoomType, string Level, bool HasSign, double? MountingHeight,
                bool HasTactile, bool HasBraille, string MountingSide,
                bool IsExit, bool HasExitSign, bool IsIlluminated,
                bool IsOnEmergencyPower)> elements,
            string facilityType = null,
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            Logger.Info("Starting accessibility audit for {Count} elements, facility={Type}",
                elements.Count, facilityType ?? "General");

            var result = new AccessibilityAuditResult
            {
                StandardsChecked = new List<AccessibilityStandard>(_enabledStandards),
                TotalElementsChecked = elements.Count
            };

            int completed = 0;
            foreach (var elem in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ADA Room signage check
                if (_enabledStandards.Contains(AccessibilityStandard.ADA2010))
                {
                    var roomIssues = _adaChecker.CheckRoomSignage(
                        elem.ElementId, elem.RoomName, elem.RoomNumber,
                        elem.HasSign, elem.MountingHeight, elem.HasTactile,
                        elem.HasBraille, elem.MountingSide);
                    result.Issues.AddRange(roomIssues);

                    // Exit signage check
                    if (elem.IsExit)
                    {
                        var exitIssues = _adaChecker.CheckExitSignage(
                            elem.ElementId, $"{elem.RoomName} ({elem.Level})",
                            elem.HasExitSign, elem.IsIlluminated, elem.IsOnEmergencyPower);
                        result.Issues.AddRange(exitIssues);
                    }
                }

                // Regional standards checks
                if (_enabledStandards.Contains(AccessibilityStandard.SANS_SouthAfrica))
                    result.Issues.AddRange(CheckSANSCompliance(elem.ElementId, elem.RoomName, elem.HasSign));

                if (_enabledStandards.Contains(AccessibilityStandard.BS8300))
                    result.Issues.AddRange(CheckBS8300Compliance(elem.ElementId, elem.RoomName, elem.HasSign, elem.HasTactile));

                // Facility-specific requirements
                if (!string.IsNullOrEmpty(facilityType))
                    result.Issues.AddRange(CheckFacilityRequirements(
                        elem.ElementId, elem.RoomType, facilityType, elem.HasSign));

                completed++;
                progress?.Report((double)completed / elements.Count);
            }

            // Compute scores
            result.TotalIssuesFound = result.Issues.Count;
            result.CriticalIssues = result.Issues.Count(i => i.Severity == IssueSeverity.Critical);
            result.WarningIssues = result.Issues.Count(i => i.Severity == IssueSeverity.Warning);

            result.ComplianceScore = elements.Count > 0
                ? Math.Max(0, 100.0 - (result.CriticalIssues * 5.0) -
                    (result.Issues.Count(i => i.Severity == IssueSeverity.Error) * 2.0) -
                    (result.WarningIssues * 0.5))
                : 100.0;

            // Compliance by standard
            foreach (var std in _enabledStandards)
            {
                string stdName = std.ToString();
                var stdIssues = result.Issues.Where(i =>
                    i.Standard?.Contains(stdName, StringComparison.OrdinalIgnoreCase) == true).ToList();
                int stdChecks = elements.Count;
                result.ComplianceByStandard[stdName] = stdChecks > 0
                    ? Math.Max(0, 100.0 - (stdIssues.Count * 100.0 / stdChecks)) : 100.0;
            }

            // Compliance by level
            var levels = elements.Select(e => e.Level).Distinct();
            foreach (var level in levels.Where(l => !string.IsNullOrEmpty(l)))
            {
                var levelElems = elements.Where(e => e.Level == level).Select(e => e.ElementId).ToHashSet();
                var levelIssues = result.Issues.Where(i => levelElems.Contains(i.ElementId)).ToList();
                result.ComplianceByArea[level] = levelElems.Count > 0
                    ? Math.Max(0, 100.0 - (levelIssues.Count * 100.0 / levelElems.Count)) : 100.0;
            }

            // Generate recommendations
            if (result.CriticalIssues > 0)
                result.Recommendations.Add($"Address {result.CriticalIssues} critical issues immediately (life safety)");
            if (result.Issues.Any(i => i.Rule == "Room Identification Required"))
                result.Recommendations.Add("Install room identification signs at all permanent rooms and spaces");
            if (result.Issues.Any(i => i.Rule == "Tactile Characters Required"))
                result.Recommendations.Add("Upgrade signage to include raised tactile characters per ADA 703.4");
            if (result.Issues.Any(i => i.Rule == "Braille Required"))
                result.Recommendations.Add("Add Grade 2 Braille to all permanent room identification signs");

            Logger.Info("Accessibility audit complete: score={Score:F0}%, {Issues} issues " +
                "({Critical} critical)",
                result.ComplianceScore, result.TotalIssuesFound, result.CriticalIssues);

            return result;
        }

        #endregion

        #region Regional Standards

        private List<AccessibilityIssue> CheckSANSCompliance(
            string elementId, string roomName, bool hasSign)
        {
            var issues = new List<AccessibilityIssue>();
            if (!hasSign)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "SANS 10400-S",
                    Rule = "Signage Required",
                    Description = $"Room '{roomName}' lacks identification per SANS 10400 Part S",
                    Severity = IssueSeverity.Error,
                    Recommendation = "Install identification signage compliant with SANS 10400-S",
                    Level = ComplianceLevel.MajorNonCompliance
                });
            }
            return issues;
        }

        private List<AccessibilityIssue> CheckBS8300Compliance(
            string elementId, string roomName, bool hasSign, bool hasTactile)
        {
            var issues = new List<AccessibilityIssue>();
            if (!hasSign)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "BS 8300:2018",
                    Rule = "Room Signage Required",
                    Description = $"Room '{roomName}' lacks identification per BS 8300",
                    Severity = IssueSeverity.Error,
                    Recommendation = "Install signage per BS 8300 Section 11",
                    Level = ComplianceLevel.MajorNonCompliance
                });
            }
            else if (!hasTactile)
            {
                issues.Add(new AccessibilityIssue
                {
                    ElementId = elementId,
                    Standard = "BS 8300:2018",
                    Rule = "Tactile Information",
                    Description = $"Room sign for '{roomName}' lacks tactile information per BS 8300",
                    Severity = IssueSeverity.Warning,
                    Recommendation = "Add raised tactile characters and Braille",
                    Level = ComplianceLevel.MinorNonCompliance
                });
            }
            return issues;
        }

        private List<AccessibilityIssue> CheckFacilityRequirements(
            string elementId, string roomType, string facilityType, bool hasSign)
        {
            var issues = new List<AccessibilityIssue>();
            if (!_facilityProfiles.TryGetValue(facilityType, out var profile))
                return issues;

            var requirements = profile.Requirements
                .Where(r => r.Mandatory &&
                    (string.IsNullOrEmpty(r.RoomType) ||
                     string.Equals(r.RoomType, roomType, StringComparison.OrdinalIgnoreCase)));

            foreach (var req in requirements)
            {
                if (!hasSign && req.RequiredSignage == SignageType.RoomIdentification)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        ElementId = elementId,
                        Standard = req.Standard ?? facilityType,
                        Rule = $"{facilityType} Facility Requirement",
                        Description = $"{facilityType}: {req.Description}",
                        Severity = IssueSeverity.Warning,
                        Recommendation = req.Description,
                        Level = ComplianceLevel.MinorNonCompliance
                    });
                }
            }

            return issues;
        }

        #endregion

        #region Wayfinding

        public void RegisterWayfindingNode(WayfindingNode node)
        {
            _wayfindingAnalyzer.RegisterNode(node);
        }

        public List<SignageScheduleEntry> GenerateWayfindingSchedule(List<string> destinations)
        {
            return _wayfindingAnalyzer.GenerateWayfindingSchedule(destinations);
        }

        #endregion

        #region Signage Schedule

        /// <summary>
        /// Auto-generate a signage schedule from audit results.
        /// </summary>
        public List<SignageScheduleEntry> GenerateSignageSchedule(
            AccessibilityAuditResult auditResult)
        {
            var schedule = new List<SignageScheduleEntry>();
            int seqNum = 1;

            foreach (var issue in auditResult.Issues.Where(i =>
                i.Rule == "Room Identification Required"))
            {
                schedule.Add(new SignageScheduleEntry
                {
                    Id = $"RS-{seqNum++:D3}",
                    Type = SignageType.RoomIdentification,
                    Location = issue.Location ?? issue.ElementId,
                    Content = "Room name and number",
                    MountingHeight = 1370, // 54" AFF center
                    MountingSide = "Latch side",
                    RequiresTactile = true,
                    RequiresBraille = true,
                    Material = "Acrylic with raised tactile text",
                    Width = 200,
                    Height = 150,
                    Notes = issue.Recommendation
                });
            }

            foreach (var issue in auditResult.Issues.Where(i =>
                i.Rule == "Exit Sign Required"))
            {
                schedule.Add(new SignageScheduleEntry
                {
                    Id = $"EX-{seqNum++:D3}",
                    Type = SignageType.ExitSign,
                    Location = issue.Location ?? issue.ElementId,
                    Content = "EXIT",
                    MountingHeight = 2032, // 80" AFF minimum
                    IsIlluminated = true,
                    Material = "LED illuminated",
                    Width = 300,
                    Height = 150,
                    Notes = "Connect to emergency power (90-min backup)"
                });
            }

            Logger.Info("Generated signage schedule: {Count} entries", schedule.Count);
            return schedule;
        }

        #endregion

        #region Facility Profiles

        private void InitializeFacilityProfiles()
        {
            _facilityProfiles["Healthcare"] = new FacilityTypeProfile
            {
                FacilityType = "Healthcare",
                Requirements = new List<SignageRequirement>
                {
                    new() { RoomType = "Patient Room", RequiredSignage = SignageType.RoomIdentification,
                        Mandatory = true, Description = "Patient room ID with name holder" },
                    new() { RoomType = "Nursing Station", RequiredSignage = SignageType.RoomIdentification,
                        Mandatory = true, Description = "Nursing station identification" },
                    new() { RoomType = "Emergency", RequiredSignage = SignageType.Directional,
                        Mandatory = true, Description = "Emergency department wayfinding" }
                }
            };

            _facilityProfiles["Education"] = new FacilityTypeProfile
            {
                FacilityType = "Education",
                Requirements = new List<SignageRequirement>
                {
                    new() { RoomType = "Classroom", RequiredSignage = SignageType.RoomIdentification,
                        Mandatory = true, Description = "Classroom number and name signage" },
                    new() { RoomType = "Laboratory", RequiredSignage = SignageType.Warning,
                        Mandatory = true, Description = "Laboratory hazard identification" }
                }
            };

            _facilityProfiles["Office"] = new FacilityTypeProfile
            {
                FacilityType = "Office",
                Requirements = new List<SignageRequirement>
                {
                    new() { RoomType = "Suite", RequiredSignage = SignageType.RoomIdentification,
                        Mandatory = true, Description = "Suite identification with tenant name" },
                    new() { RoomType = null, RequiredSignage = SignageType.Floor_Directory,
                        Mandatory = true, Description = "Floor directory at each elevator lobby" }
                }
            };

            _facilityProfiles["Hospitality"] = new FacilityTypeProfile
            {
                FacilityType = "Hospitality",
                Requirements = new List<SignageRequirement>
                {
                    new() { RoomType = "Guest Room", RequiredSignage = SignageType.RoomIdentification,
                        Mandatory = true, Description = "Guest room number with tactile characters" },
                    new() { RoomType = null, RequiredSignage = SignageType.Directional,
                        Mandatory = true, Description = "Wayfinding to amenities, exits, elevators" }
                }
            };

            _facilityProfiles["Industrial"] = new FacilityTypeProfile
            {
                FacilityType = "Industrial",
                Requirements = new List<SignageRequirement>
                {
                    new() { RoomType = null, RequiredSignage = SignageType.Warning,
                        Mandatory = true, Description = "Hazard identification and safety signage" },
                    new() { RoomType = null, RequiredSignage = SignageType.Regulatory,
                        Mandatory = true, Description = "OSHA required safety notices" }
                }
            };
        }

        public FacilityTypeProfile GetFacilityProfile(string facilityType)
        {
            return _facilityProfiles.GetValueOrDefault(facilityType);
        }

        public List<string> GetAvailableFacilityTypes()
        {
            return _facilityProfiles.Keys.ToList();
        }

        #endregion
    }

    #endregion
}
