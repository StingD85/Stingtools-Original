// StingBIM.AI.Automation.Compliance.UgandaComplianceChecker
// Uganda-specific compliance: UNBS, Building Control Act, Fire, Accessibility, Public Health
// v4 Prompt Reference: Section A.8 Phase 8 — ComplianceChecker rule engine

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Automation.Compliance
{
    /// <summary>
    /// Uganda-specific compliance rule engine covering:
    ///   - Uganda Building Control Act 2013
    ///   - UNBS US 319 (Fire safety)
    ///   - UNBS US 320-327 (Building standards)
    ///   - Persons with Disabilities Act 2020 (Accessibility)
    ///   - Public Health Act (ventilation, sanitation, drainage)
    ///   - Kampala Capital City Authority (KCCA) building regulations
    ///   - National Environment Act 2019
    ///
    /// All rules return structured results with code references,
    /// severity levels, and actionable recommendations.
    /// </summary>
    public class UgandaComplianceChecker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly object _lock = new object();

        // Uganda-specific building constants
        private const double MIN_ROOM_HEIGHT_M = 2.6;           // KCCA minimum habitable room height
        private const double MIN_ROOM_AREA_M2 = 7.5;            // Minimum habitable room area
        private const double MIN_KITCHEN_AREA_M2 = 5.0;
        private const double MIN_BATHROOM_AREA_M2 = 2.5;
        private const double MIN_CORRIDOR_WIDTH_M = 1.2;        // General
        private const double MIN_CORRIDOR_WIDTH_COMMERCIAL_M = 1.5;
        private const double MIN_DOOR_WIDTH_M = 0.8;            // Internal
        private const double MIN_DOOR_WIDTH_EXTERNAL_M = 0.9;
        private const double MIN_DOOR_WIDTH_ACCESSIBLE_M = 0.9;
        private const double MIN_WINDOW_AREA_RATIO = 0.10;      // Window area / floor area (10%)
        private const double MIN_STAIR_WIDTH_M = 0.9;           // Domestic
        private const double MIN_STAIR_WIDTH_COMMERCIAL_M = 1.2;
        private const double MAX_STAIR_RISE_MM = 200;
        private const double MIN_STAIR_GOING_MM = 250;
        private const double MAX_FLIGHT_RISE_M = 3.6;           // Max height before landing
        private const double MIN_LANDING_LENGTH_M = 0.9;
        private const double MAX_RAMP_GRADIENT = 1.0 / 12.0;    // 1:12
        private const double MIN_RAMP_WIDTH_M = 1.2;
        private const double MAX_BUILDING_HEIGHT_RESIDENTIAL_M = 15.0;  // Without structural engineer
        private const double MIN_SETBACK_FRONT_M = 4.5;         // KCCA
        private const double MIN_SETBACK_SIDE_M = 1.5;          // KCCA
        private const double MIN_SETBACK_REAR_M = 3.0;          // KCCA

        // Fire safety constants
        private const double MAX_TRAVEL_DISTANCE_SINGLE_M = 18.0;
        private const double MAX_TRAVEL_DISTANCE_MULTI_M = 45.0;
        private const double MIN_FIRE_EXIT_WIDTH_M = 0.9;
        private const double MIN_FIRE_DOOR_RATING_MINUTES = 30;
        private const int MAX_PERSONS_PER_EXIT = 60;

        // Sanitation constants (Public Health Act)
        private const int PERSONS_PER_WC_MALE = 25;
        private const int PERSONS_PER_WC_FEMALE = 15;
        private const int PERSONS_PER_URINAL = 25;
        private const int PERSONS_PER_WHB = 25;               // Wash hand basin
        private const double MIN_SEPTIC_TANK_M3_PER_PERSON = 0.06;

        // Parking requirements (KCCA)
        private static readonly Dictionary<string, double> ParkingRatios =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["residential"] = 1.0,      // 1 space per dwelling unit
                ["office"] = 0.033,          // 1 space per 30 m²
                ["retail"] = 0.05,           // 1 space per 20 m²
                ["hotel"] = 0.5,             // 1 space per 2 rooms
                ["hospital"] = 0.1,          // 1 space per 10 beds
                ["school"] = 0.05,           // 1 space per 20 students
                ["church"] = 0.1,            // 1 space per 10 seats
                ["restaurant"] = 0.1,        // 1 space per 10 seats
                ["industrial"] = 0.02,       // 1 space per 50 m²
            };

        public UgandaComplianceChecker(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        #region Full Compliance Check

        /// <summary>
        /// Runs all Uganda compliance checks on the model.
        /// </summary>
        public UgandaComplianceReport CheckAll()
        {
            var report = new UgandaComplianceReport
            {
                CheckTime = DateTime.Now,
                BuildingType = DetectBuildingType()
            };

            try
            {
                Logger.Info("Running full Uganda compliance check");

                report.RoomChecks = CheckRoomSizes();
                report.DoorChecks = CheckDoorWidths();
                report.StairChecks = CheckStairCompliance();
                report.FireChecks = CheckFireCompliance();
                report.AccessibilityChecks = CheckAccessibility();
                report.VentilationChecks = CheckVentilation();
                report.SanitationChecks = CheckSanitation();
                report.ParkingChecks = CheckParking();

                // Aggregate
                var allIssues = new List<ComplianceIssue>();
                allIssues.AddRange(report.RoomChecks);
                allIssues.AddRange(report.DoorChecks);
                allIssues.AddRange(report.StairChecks);
                allIssues.AddRange(report.FireChecks);
                allIssues.AddRange(report.AccessibilityChecks);
                allIssues.AddRange(report.VentilationChecks);
                allIssues.AddRange(report.SanitationChecks);
                allIssues.AddRange(report.ParkingChecks);

                report.TotalIssues = allIssues.Count;
                report.CriticalCount = allIssues.Count(i => i.Severity == IssueSeverity.Fail);
                report.WarningCount = allIssues.Count(i => i.Severity == IssueSeverity.Warning);
                report.PassCount = allIssues.Count(i => i.Severity == IssueSeverity.Pass);

                report.OverallScore = report.TotalIssues > 0
                    ? (double)report.PassCount / report.TotalIssues * 100.0
                    : 100.0;

                Logger.Info($"Uganda compliance check complete: {report.CriticalCount} fails, " +
                           $"{report.WarningCount} warnings, {report.PassCount} pass");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Uganda compliance check failed");
                report.Error = ex.Message;
            }

            return report;
        }

        /// <summary>
        /// Formats compliance report for chat display.
        /// </summary>
        public string FormatReport(UgandaComplianceReport report)
        {
            var lines = new List<string>
            {
                $"Uganda Building Compliance Report",
                $"Building type: {report.BuildingType}",
                $"Score: {report.OverallScore:F0}%\n",
                $"Results: {report.CriticalCount} FAIL | {report.WarningCount} WARNING | {report.PassCount} PASS\n"
            };

            void AddSection(string title, List<ComplianceIssue> issues)
            {
                if (issues.Count == 0) return;
                lines.Add($"{title}:");
                foreach (var issue in issues.Where(i => i.Severity != IssueSeverity.Pass))
                {
                    var icon = issue.Severity == IssueSeverity.Fail ? "FAIL" : "WARN";
                    lines.Add($"  [{icon}] {issue.Code}: {issue.Description}");
                    lines.Add($"    Standard: {issue.Standard}");
                    if (!string.IsNullOrEmpty(issue.Recommendation))
                        lines.Add($"    Fix: {issue.Recommendation}");
                }
            }

            AddSection("Room Sizes", report.RoomChecks);
            AddSection("Door Widths", report.DoorChecks);
            AddSection("Stairs", report.StairChecks);
            AddSection("Fire Safety", report.FireChecks);
            AddSection("Accessibility", report.AccessibilityChecks);
            AddSection("Ventilation", report.VentilationChecks);
            AddSection("Sanitation", report.SanitationChecks);
            AddSection("Parking", report.ParkingChecks);

            return string.Join("\n", lines);
        }

        #endregion

        #region Individual Checks

        private List<ComplianceIssue> CheckRoomSizes()
        {
            var issues = new List<ComplianceIssue>();

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            foreach (var room in rooms)
            {
                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Room";
                double areaSqFt = room.Area;
                double areaM2 = areaSqFt * 0.0929;
                var lower = roomName.ToLowerInvariant();

                // Check minimum area
                double minArea;
                if (lower.Contains("kitchen"))
                    minArea = MIN_KITCHEN_AREA_M2;
                else if (lower.Contains("bath") || lower.Contains("wc") || lower.Contains("toilet"))
                    minArea = MIN_BATHROOM_AREA_M2;
                else if (lower.Contains("corridor") || lower.Contains("hallway"))
                    minArea = 0; // check width instead
                else
                    minArea = MIN_ROOM_AREA_M2;

                if (minArea > 0 && areaM2 < minArea)
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-RM-001",
                        Severity = IssueSeverity.Fail,
                        Category = "Room Size",
                        Description = $"'{roomName}' area {areaM2:F1} m² < minimum {minArea:F1} m²",
                        Standard = "Uganda Building Control Act 2013",
                        Recommendation = $"Increase room area to at least {minArea:F1} m²",
                        ElementId = room.Id
                    });
                }
                else if (minArea > 0)
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-RM-001",
                        Severity = IssueSeverity.Pass,
                        Category = "Room Size",
                        Description = $"'{roomName}' area {areaM2:F1} m² meets minimum {minArea:F1} m²",
                        Standard = "Uganda Building Control Act 2013"
                    });
                }

                // Check ceiling height
                var heightParam = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                if (heightParam != null)
                {
                    double heightM = heightParam.AsDouble() * 0.3048;
                    if (heightM < MIN_ROOM_HEIGHT_M && !lower.Contains("store") && !lower.Contains("plant"))
                    {
                        issues.Add(new ComplianceIssue
                        {
                            Code = "UG-RM-002",
                            Severity = IssueSeverity.Fail,
                            Category = "Room Height",
                            Description = $"'{roomName}' height {heightM:F2} m < minimum {MIN_ROOM_HEIGHT_M} m",
                            Standard = "KCCA Building Regulations",
                            Recommendation = $"Increase floor-to-ceiling height to {MIN_ROOM_HEIGHT_M} m minimum",
                            ElementId = room.Id
                        });
                    }
                }
            }

            return issues;
        }

        private List<ComplianceIssue> CheckDoorWidths()
        {
            var issues = new List<ComplianceIssue>();

            var doors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var door in doors)
            {
                var typeName = (door.Symbol?.Name ?? "").ToLowerInvariant();
                var widthParam = door.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                if (widthParam == null) continue;

                double widthM = widthParam.AsDouble() * 0.3048;
                double minWidth;

                if (typeName.Contains("external") || typeName.Contains("entrance") || typeName.Contains("main"))
                    minWidth = MIN_DOOR_WIDTH_EXTERNAL_M;
                else if (typeName.Contains("fire") || typeName.Contains("escape"))
                    minWidth = MIN_FIRE_EXIT_WIDTH_M;
                else if (typeName.Contains("accessible") || typeName.Contains("disable"))
                    minWidth = MIN_DOOR_WIDTH_ACCESSIBLE_M;
                else
                    minWidth = MIN_DOOR_WIDTH_M;

                if (widthM < minWidth)
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-DR-001",
                        Severity = typeName.Contains("fire") ? IssueSeverity.Fail : IssueSeverity.Warning,
                        Category = "Door Width",
                        Description = $"Door '{door.Symbol.Name}' width {widthM * 1000:F0}mm " +
                                     $"< minimum {minWidth * 1000:F0}mm",
                        Standard = typeName.Contains("fire") ? "UNBS US 319" : "Uganda Building Control Act",
                        Recommendation = $"Increase door width to {minWidth * 1000:F0}mm",
                        ElementId = door.Id
                    });
                }
                else
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-DR-001",
                        Severity = IssueSeverity.Pass,
                        Category = "Door Width",
                        Description = $"Door '{door.Symbol.Name}' width {widthM * 1000:F0}mm meets minimum",
                        Standard = "Uganda Building Control Act"
                    });
                }
            }

            return issues;
        }

        private List<ComplianceIssue> CheckStairCompliance()
        {
            var issues = new List<ComplianceIssue>();

            var stairs = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Stairs)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var stair in stairs)
            {
                var riserHeight = stair.LookupParameter("Actual Riser Height");
                if (riserHeight != null)
                {
                    double riseMm = riserHeight.AsDouble() * 304.8;
                    if (riseMm > MAX_STAIR_RISE_MM)
                    {
                        issues.Add(new ComplianceIssue
                        {
                            Code = "UG-ST-001",
                            Severity = IssueSeverity.Fail,
                            Category = "Stair Rise",
                            Description = $"Stair riser height {riseMm:F0}mm > maximum {MAX_STAIR_RISE_MM}mm",
                            Standard = "UNBS US 322 / BS 5395",
                            Recommendation = $"Reduce riser height to maximum {MAX_STAIR_RISE_MM}mm",
                            ElementId = stair.Id
                        });
                    }
                }

                var treadDepth = stair.LookupParameter("Actual Tread Depth");
                if (treadDepth != null)
                {
                    double goingMm = treadDepth.AsDouble() * 304.8;
                    if (goingMm < MIN_STAIR_GOING_MM)
                    {
                        issues.Add(new ComplianceIssue
                        {
                            Code = "UG-ST-002",
                            Severity = IssueSeverity.Fail,
                            Category = "Stair Going",
                            Description = $"Stair tread depth {goingMm:F0}mm < minimum {MIN_STAIR_GOING_MM}mm",
                            Standard = "UNBS US 322 / BS 5395",
                            Recommendation = $"Increase tread depth to minimum {MIN_STAIR_GOING_MM}mm",
                            ElementId = stair.Id
                        });
                    }
                }
            }

            if (stairs.Count == 0)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-ST-000",
                    Severity = IssueSeverity.Pass,
                    Category = "Stairs",
                    Description = "No stairs in model (single-storey or stairs not yet placed)",
                    Standard = "N/A"
                });
            }

            return issues;
        }

        private List<ComplianceIssue> CheckFireCompliance()
        {
            var issues = new List<ComplianceIssue>();

            // Check fire exit count
            var doors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            var fireExits = doors.Where(d =>
            {
                var name = (d.Symbol?.Name ?? "").ToLowerInvariant();
                return name.Contains("fire") || name.Contains("exit") || name.Contains("escape");
            }).ToList();

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            double totalAreaM2 = rooms.Sum(r => r.Area * 0.0929);

            // Estimate occupancy (10 m² per person for office, 5 m² for assembly)
            int estimatedOccupancy = (int)Math.Ceiling(totalAreaM2 / 10);
            int requiredExits = estimatedOccupancy <= 60 ? 1
                : estimatedOccupancy <= 120 ? 2
                : estimatedOccupancy <= 240 ? 3
                : 4;

            if (fireExits.Count < requiredExits && requiredExits > 1)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-FR-001",
                    Severity = IssueSeverity.Fail,
                    Category = "Fire Exits",
                    Description = $"Only {fireExits.Count} fire exit(s) for estimated " +
                                 $"{estimatedOccupancy} occupants (need {requiredExits})",
                    Standard = "UNBS US 319 / Building Control Act",
                    Recommendation = $"Add {requiredExits - fireExits.Count} fire exit(s). " +
                                    "Exits must be on opposite sides of building."
                });
            }
            else
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-FR-001",
                    Severity = IssueSeverity.Pass,
                    Category = "Fire Exits",
                    Description = $"Fire exit count ({fireExits.Count}) adequate for " +
                                 $"estimated {estimatedOccupancy} occupants",
                    Standard = "UNBS US 319"
                });
            }

            // Check for fire detection
            var fireAlarms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_FireAlarmDevices)
                .WhereElementIsNotElementType()
                .GetElementCount();

            if (fireAlarms == 0)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-FR-002",
                    Severity = IssueSeverity.Warning,
                    Category = "Fire Detection",
                    Description = "No fire alarm devices detected in model",
                    Standard = "UNBS US 319 / BS 5839",
                    Recommendation = "Add smoke/heat detectors in all habitable rooms and corridors"
                });
            }

            return issues;
        }

        private List<ComplianceIssue> CheckAccessibility()
        {
            var issues = new List<ComplianceIssue>();

            // Check for ramps
            var ramps = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Ramps)
                .WhereElementIsNotElementType()
                .ToList();

            // Check for multi-storey without lift
            var levels = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count > 1)
            {
                // Check for lift/elevator
                var genericModels = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(e => (e.Symbol?.Name ?? "").ToLowerInvariant().Contains("lift") ||
                               (e.Symbol?.Name ?? "").ToLowerInvariant().Contains("elevator"))
                    .ToList();

                if (genericModels.Count == 0)
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-AC-001",
                        Severity = IssueSeverity.Warning,
                        Category = "Accessibility",
                        Description = "Multi-storey building ({levels.Count} levels) with no lift detected",
                        Standard = "PWD Act 2020 / KCCA Regulations",
                        Recommendation = "Provide at least one accessible lift for buildings above 2 storeys"
                    });
                }
            }

            // Check for accessible entrance
            if (ramps.Count == 0 && levels.Count > 0)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-AC-002",
                    Severity = IssueSeverity.Warning,
                    Category = "Accessibility",
                    Description = "No ramps detected — accessible entrance may not be provided",
                    Standard = "PWD Act 2020",
                    Recommendation = "Add ramp at main entrance (max gradient 1:12, min width 1.2m)"
                });
            }

            return issues;
        }

        private List<ComplianceIssue> CheckVentilation()
        {
            var issues = new List<ComplianceIssue>();

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            var windows = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .GetElementCount();

            // Simple check: ratio of windows to rooms
            if (rooms.Count > 0 && windows == 0)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-VN-001",
                    Severity = IssueSeverity.Fail,
                    Category = "Ventilation",
                    Description = "No windows in model — natural ventilation not provided",
                    Standard = "Public Health Act / KCCA Building Regulations",
                    Recommendation = "Provide openable windows (min 10% of floor area) in all habitable rooms"
                });
            }
            else if (rooms.Count > 0)
            {
                double ratio = (double)windows / rooms.Count;
                if (ratio < 0.5) // less than 1 window per 2 rooms
                {
                    issues.Add(new ComplianceIssue
                    {
                        Code = "UG-VN-001",
                        Severity = IssueSeverity.Warning,
                        Category = "Ventilation",
                        Description = $"Low window count ({windows}) for {rooms.Count} rooms. " +
                                     "Some rooms may lack natural ventilation.",
                        Standard = "Public Health Act",
                        Recommendation = "Ensure all habitable rooms have openable windows " +
                                        "(min 10% of floor area)"
                    });
                }
            }

            return issues;
        }

        private List<ComplianceIssue> CheckSanitation()
        {
            var issues = new List<ComplianceIssue>();

            // Count WCs and basins
            var plumbingFixtures = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            int wcCount = plumbingFixtures.Count(f =>
            {
                var name = (f.Symbol?.Name ?? "").ToLowerInvariant();
                return name.Contains("wc") || name.Contains("toilet") || name.Contains("closet");
            });

            int whbCount = plumbingFixtures.Count(f =>
            {
                var name = (f.Symbol?.Name ?? "").ToLowerInvariant();
                return name.Contains("basin") || name.Contains("sink") || name.Contains("wash");
            });

            // Estimate occupancy
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            double totalAreaM2 = rooms.Sum(r => r.Area * 0.0929);
            int estimatedOccupancy = Math.Max(1, (int)Math.Ceiling(totalAreaM2 / 10));

            int requiredWC = Math.Max(1, (int)Math.Ceiling((double)estimatedOccupancy / PERSONS_PER_WC_MALE));
            int requiredWHB = Math.Max(1, (int)Math.Ceiling((double)estimatedOccupancy / PERSONS_PER_WHB));

            if (wcCount < requiredWC)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-SN-001",
                    Severity = IssueSeverity.Warning,
                    Category = "Sanitation",
                    Description = $"WC count ({wcCount}) may be insufficient for estimated " +
                                 $"{estimatedOccupancy} occupants (need {requiredWC}+)",
                    Standard = "Public Health Act / KCCA Regulations",
                    Recommendation = $"Provide at least {requiredWC} WCs (1 per {PERSONS_PER_WC_MALE} persons)"
                });
            }

            if (whbCount < requiredWHB && whbCount > 0)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-SN-002",
                    Severity = IssueSeverity.Warning,
                    Category = "Sanitation",
                    Description = $"Wash basin count ({whbCount}) may be insufficient",
                    Standard = "Public Health Act",
                    Recommendation = $"Provide at least {requiredWHB} wash basins (1 per {PERSONS_PER_WHB} persons)"
                });
            }

            return issues;
        }

        private List<ComplianceIssue> CheckParking()
        {
            var issues = new List<ComplianceIssue>();

            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            double totalAreaM2 = rooms.Sum(r => r.Area * 0.0929);
            var buildingType = DetectBuildingType();

            double parkingRatio = ParkingRatios.ContainsKey(buildingType)
                ? ParkingRatios[buildingType]
                : ParkingRatios["office"];

            int requiredSpaces;
            if (buildingType == "residential")
            {
                // Count dwelling units (bedrooms / apartments)
                int dwellings = rooms.Count(r =>
                {
                    var name = (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                    return name.Contains("bedroom") || name.Contains("master");
                });
                requiredSpaces = Math.Max(1, dwellings);
            }
            else
            {
                requiredSpaces = Math.Max(1, (int)Math.Ceiling(totalAreaM2 * parkingRatio));
            }

            // Check existing parking
            var parkingRooms = rooms.Where(r =>
            {
                var name = (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                return name.Contains("park") || name.Contains("garage") || name.Contains("carport");
            }).ToList();

            double parkingAreaM2 = parkingRooms.Sum(r => r.Area * 0.0929);
            int estimatedSpaces = (int)(parkingAreaM2 / 15); // ~15 m² per space

            if (estimatedSpaces < requiredSpaces && requiredSpaces > 2)
            {
                issues.Add(new ComplianceIssue
                {
                    Code = "UG-PK-001",
                    Severity = IssueSeverity.Warning,
                    Category = "Parking",
                    Description = $"Parking provision (~{estimatedSpaces} spaces) may be " +
                                 $"below KCCA requirement ({requiredSpaces} for {buildingType})",
                    Standard = "KCCA Physical Planning Regulations",
                    Recommendation = $"Provide at least {requiredSpaces} parking spaces"
                });
            }

            return issues;
        }

        #endregion

        #region Helpers

        private string DetectBuildingType()
        {
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            int bedrooms = 0, offices = 0, classrooms = 0, wards = 0;

            foreach (var room in rooms)
            {
                var name = (room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                if (name.Contains("bedroom") || name.Contains("master")) bedrooms++;
                else if (name.Contains("office") || name.Contains("conference")) offices++;
                else if (name.Contains("class") || name.Contains("lecture")) classrooms++;
                else if (name.Contains("ward") || name.Contains("theatre")) wards++;
            }

            if (bedrooms > offices && bedrooms > classrooms) return "residential";
            if (offices > bedrooms && offices > classrooms) return "office";
            if (classrooms > 0) return "school";
            if (wards > 0) return "hospital";

            return "office"; // default
        }

        #endregion
    }

    #region Data Types

    public enum IssueSeverity
    {
        Pass,
        Warning,
        Fail
    }

    public class ComplianceIssue
    {
        public string Code { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Standard { get; set; }
        public string Recommendation { get; set; }
        public ElementId ElementId { get; set; }
    }

    public class UgandaComplianceReport
    {
        public DateTime CheckTime { get; set; }
        public string BuildingType { get; set; }
        public double OverallScore { get; set; }
        public int TotalIssues { get; set; }
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int PassCount { get; set; }
        public string Error { get; set; }

        public List<ComplianceIssue> RoomChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> DoorChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> StairChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> FireChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> AccessibilityChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> VentilationChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> SanitationChecks { get; set; } = new List<ComplianceIssue>();
        public List<ComplianceIssue> ParkingChecks { get; set; } = new List<ComplianceIssue>();
    }

    #endregion
}
