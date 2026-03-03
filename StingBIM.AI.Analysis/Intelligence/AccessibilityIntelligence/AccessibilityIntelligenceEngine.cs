// ===================================================================
// StingBIM Accessibility Intelligence Engine
// ADA/ADAG compliance, accessible route analysis
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.AccessibilityIntelligence
{
    #region Enums

    public enum AccessibilityStandard { ADA2010, ADAAG, FairHousing, ICC_A117, UFAS }
    public enum RouteType { Accessible, Primary, Emergency, Service }
    public enum ComplianceLevel { Compliant, NonCompliant, Exceeds, NotApplicable }
    public enum SpaceType { Entrance, Corridor, Restroom, Parking, Assembly, Dwelling, Workspace }

    #endregion

    #region Data Models

    public class AccessibilityProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public AccessibilityStandard PrimaryStandard { get; set; } = AccessibilityStandard.ADA2010;
        public List<AccessibleRoute> Routes { get; set; } = new();
        public List<AccessibleSpace> Spaces { get; set; } = new();
        public List<AccessibilityCheck> Checks { get; set; } = new();
        public AccessibilitySummary Summary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AccessibleRoute
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public RouteType Type { get; set; }
        public string StartLocation { get; set; }
        public string EndLocation { get; set; }
        public double TotalLength { get; set; }
        public List<RouteSegment> Segments { get; set; } = new();
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public class RouteSegment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Slope { get; set; }
        public double CrossSlope { get; set; }
        public string SurfaceType { get; set; }
        public bool HasObstruction { get; set; }
        public ComplianceLevel Compliance { get; set; }
    }

    public class AccessibleSpace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public SpaceType Type { get; set; }
        public double Area { get; set; }
        public List<AccessibilityFeature> Features { get; set; } = new();
        public List<string> RequiredFeatures { get; set; } = new();
        public ComplianceLevel OverallCompliance { get; set; }
    }

    public class AccessibilityFeature
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public double RequiredValue { get; set; }
        public double ProvidedValue { get; set; }
        public string Unit { get; set; }
        public ComplianceLevel Compliance { get; set; }
        public string CodeReference { get; set; }
    }

    public class AccessibilityCheck
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Location { get; set; }
        public string Element { get; set; }
        public string Requirement { get; set; }
        public string CodeSection { get; set; }
        public double RequiredValue { get; set; }
        public double ActualValue { get; set; }
        public string Unit { get; set; }
        public ComplianceLevel Status { get; set; }
        public string Recommendation { get; set; }
    }

    public class AccessibilitySummary
    {
        public int TotalChecks { get; set; }
        public int CompliantChecks { get; set; }
        public int NonCompliantChecks { get; set; }
        public double ComplianceRate => TotalChecks > 0 ? CompliantChecks * 100.0 / TotalChecks : 0;
        public List<string> CriticalIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ParkingRequirement
    {
        public int TotalSpaces { get; set; }
        public int RequiredAccessibleSpaces { get; set; }
        public int RequiredVanAccessible { get; set; }
        public int ProvidedAccessibleSpaces { get; set; }
        public int ProvidedVanAccessible { get; set; }
        public ComplianceLevel Compliance { get; set; }
    }

    public class RestroomRequirement
    {
        public string RestroomId { get; set; }
        public double ClearFloorSpace { get; set; }
        public double TurningRadius { get; set; }
        public double GrabBarHeight { get; set; }
        public double SeatHeight { get; set; }
        public double LavatoryClearance { get; set; }
        public double MirrorHeight { get; set; }
        public List<AccessibilityCheck> DetailedChecks { get; set; } = new();
    }

    #endregion

    public sealed class AccessibilityIntelligenceEngine
    {
        private static readonly Lazy<AccessibilityIntelligenceEngine> _instance =
            new Lazy<AccessibilityIntelligenceEngine>(() => new AccessibilityIntelligenceEngine());
        public static AccessibilityIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, AccessibilityProject> _projects = new();
        private readonly object _lock = new object();

        // ADA 2010 Standards
        private readonly Dictionary<string, (double min, double max, string unit)> _adaRequirements = new()
        {
            ["Route Width"] = (36, 0, "inches"),
            ["Route Width Passing"] = (60, 0, "inches"),
            ["Door Clear Width"] = (32, 0, "inches"),
            ["Running Slope Max"] = (0, 5, "percent"),
            ["Cross Slope Max"] = (0, 2, "percent"),
            ["Ramp Slope Max"] = (0, 8.33, "percent"),
            ["Turning Space"] = (60, 0, "inches"),
            ["Clear Floor Space Width"] = (30, 0, "inches"),
            ["Clear Floor Space Depth"] = (48, 0, "inches"),
            ["Grab Bar Height"] = (33, 36, "inches"),
            ["Toilet Seat Height"] = (17, 19, "inches"),
            ["Lavatory Knee Clearance"] = (27, 0, "inches"),
            ["Mirror Max Height"] = (0, 40, "inches"),
            ["Reach Range Low"] = (15, 0, "inches"),
            ["Reach Range High"] = (0, 48, "inches")
        };

        private AccessibilityIntelligenceEngine() { }

        public AccessibilityProject CreateAccessibilityProject(string projectId, string projectName)
        {
            var project = new AccessibilityProject { ProjectId = projectId, ProjectName = projectName };
            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public AccessibleRoute AnalyzeRoute(string projectId, string name, string startLocation, string endLocation)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var route = new AccessibleRoute
                {
                    Name = name,
                    Type = RouteType.Accessible,
                    StartLocation = startLocation,
                    EndLocation = endLocation
                };

                project.Routes.Add(route);
                return route;
            }
        }

        public RouteSegment AddRouteSegment(string projectId, string routeId, string type,
            double length, double width, double slope, double crossSlope)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var route = project.Routes.FirstOrDefault(r => r.Id == routeId);
                if (route == null) return null;

                var segment = new RouteSegment
                {
                    Type = type,
                    Length = length,
                    Width = width,
                    Slope = slope,
                    CrossSlope = crossSlope
                };

                // Check compliance
                var issues = new List<string>();

                if (width < 36)
                {
                    segment.Compliance = ComplianceLevel.NonCompliant;
                    issues.Add($"Width {width}\" is less than required 36\"");
                }
                else if (slope > 5)
                {
                    segment.Compliance = ComplianceLevel.NonCompliant;
                    issues.Add($"Running slope {slope}% exceeds 5% max (requires ramp)");
                }
                else if (crossSlope > 2)
                {
                    segment.Compliance = ComplianceLevel.NonCompliant;
                    issues.Add($"Cross slope {crossSlope}% exceeds 2% max");
                }
                else
                {
                    segment.Compliance = ComplianceLevel.Compliant;
                }

                route.Segments.Add(segment);
                route.TotalLength = route.Segments.Sum(s => s.Length);
                route.Issues.AddRange(issues);
                route.IsCompliant = route.Segments.All(s => s.Compliance == ComplianceLevel.Compliant);

                return segment;
            }
        }

        public ParkingRequirement CalculateParkingRequirements(int totalSpaces)
        {
            var requirement = new ParkingRequirement { TotalSpaces = totalSpaces };

            // ADA Table 208.2
            requirement.RequiredAccessibleSpaces = totalSpaces switch
            {
                <= 25 => 1,
                <= 50 => 2,
                <= 75 => 3,
                <= 100 => 4,
                <= 150 => 5,
                <= 200 => 6,
                <= 300 => 7,
                <= 400 => 8,
                <= 500 => 9,
                <= 1000 => (int)Math.Ceiling(totalSpaces * 0.02),
                _ => 20 + (int)Math.Ceiling((totalSpaces - 1000) / 100.0)
            };

            // At least 1 van accessible per 6 accessible spaces
            requirement.RequiredVanAccessible = Math.Max(1, (int)Math.Ceiling(requirement.RequiredAccessibleSpaces / 6.0));

            return requirement;
        }

        public RestroomRequirement AnalyzeRestroom(string projectId, string restroomId)
        {
            var requirement = new RestroomRequirement { RestroomId = restroomId };

            requirement.DetailedChecks = new List<AccessibilityCheck>
            {
                new AccessibilityCheck
                {
                    Element = "Clear Floor Space",
                    Requirement = "30\" x 48\" minimum",
                    CodeSection = "ADA 305",
                    RequiredValue = 30,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Turning Space",
                    Requirement = "60\" diameter turning space",
                    CodeSection = "ADA 304",
                    RequiredValue = 60,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Toilet Seat Height",
                    Requirement = "17\"-19\" AFF",
                    CodeSection = "ADA 604.4",
                    RequiredValue = 17,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Side Grab Bar",
                    Requirement = "42\" minimum length, 33\"-36\" AFF",
                    CodeSection = "ADA 604.5.1",
                    RequiredValue = 42,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Rear Grab Bar",
                    Requirement = "36\" minimum length",
                    CodeSection = "ADA 604.5.2",
                    RequiredValue = 36,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Lavatory Knee Clearance",
                    Requirement = "27\" minimum under lavatory",
                    CodeSection = "ADA 606.2",
                    RequiredValue = 27,
                    Unit = "inches"
                },
                new AccessibilityCheck
                {
                    Element = "Mirror Height",
                    Requirement = "40\" max to bottom of reflective surface",
                    CodeSection = "ADA 603.3",
                    RequiredValue = 40,
                    Unit = "inches"
                }
            };

            return requirement;
        }

        public AccessibilityCheck PerformCheck(string projectId, string location, string element,
            string codeSection, double requiredValue, double actualValue, string unit)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var check = new AccessibilityCheck
                {
                    Location = location,
                    Element = element,
                    CodeSection = codeSection,
                    RequiredValue = requiredValue,
                    ActualValue = actualValue,
                    Unit = unit
                };

                // Determine compliance based on element type
                if (_adaRequirements.TryGetValue(element, out var req))
                {
                    if (req.max > 0 && actualValue > req.max)
                    {
                        check.Status = ComplianceLevel.NonCompliant;
                        check.Recommendation = $"Reduce to maximum {req.max} {req.unit}";
                    }
                    else if (req.min > 0 && actualValue < req.min)
                    {
                        check.Status = ComplianceLevel.NonCompliant;
                        check.Recommendation = $"Increase to minimum {req.min} {req.unit}";
                    }
                    else
                    {
                        check.Status = ComplianceLevel.Compliant;
                    }
                }
                else
                {
                    check.Status = actualValue >= requiredValue ? ComplianceLevel.Compliant : ComplianceLevel.NonCompliant;
                }

                project.Checks.Add(check);
                UpdateSummary(project);
                return check;
            }
        }

        public List<string> GetDwellingUnitRequirements(int totalUnits, bool isCovered)
        {
            var requirements = new List<string>();

            if (isCovered) // Fair Housing Act applies
            {
                requirements.Add($"All {totalUnits} ground floor units require accessible features");
                requirements.Add("Accessible entrance on an accessible route");
                requirements.Add("Accessible public and common areas");
                requirements.Add("Usable doors (32\" clear)");
                requirements.Add("Accessible route into and through dwelling");
                requirements.Add("Light switches, outlets, thermostats at accessible heights");
                requirements.Add("Reinforced bathroom walls for grab bar installation");
                requirements.Add("Usable kitchens and bathrooms");
            }

            // Type A and Type B units per ICC A117.1
            var typeAUnits = (int)Math.Ceiling(totalUnits * 0.02);
            var typeBUnits = (int)Math.Ceiling(totalUnits * 0.05);

            requirements.Add($"Type A units required: {typeAUnits} (2%)");
            requirements.Add($"Type B units required: {typeBUnits} (5%)");

            return requirements;
        }

        public async Task<AccessibilitySummary> GenerateSummary(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    UpdateSummary(project);
                    return project.Summary;
                }
            });
        }

        private void UpdateSummary(AccessibilityProject project)
        {
            project.Summary = new AccessibilitySummary
            {
                TotalChecks = project.Checks.Count,
                CompliantChecks = project.Checks.Count(c => c.Status == ComplianceLevel.Compliant),
                NonCompliantChecks = project.Checks.Count(c => c.Status == ComplianceLevel.NonCompliant)
            };

            foreach (var check in project.Checks.Where(c => c.Status == ComplianceLevel.NonCompliant))
            {
                project.Summary.CriticalIssues.Add($"{check.Location}: {check.Element} - {check.Recommendation}");
            }

            if (project.Summary.ComplianceRate < 100)
            {
                project.Summary.Recommendations.Add("Address non-compliant items before construction");
                project.Summary.Recommendations.Add("Consider accessibility consultant review");
            }
        }
    }
}
