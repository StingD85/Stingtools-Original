// StingBIM.AI.Reasoning.Compliance.ComplianceChecker
// Building code compliance verification engine
// Master Proposal Reference: Part 2.1 Pillar 4 - Domain Intelligence (Compliance)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Reasoning.Compliance
{
    /// <summary>
    /// Compliance checking engine that validates designs against building codes and standards.
    /// Supports IBC, NFPA, ADA, ASHRAE, and regional codes.
    /// </summary>
    public class ComplianceChecker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<ComplianceRule> _rules;
        private readonly Dictionary<string, BuildingCodeProfile> _codeProfiles;
        private BuildingCodeProfile _activeProfile;

        public ComplianceChecker()
        {
            _rules = new List<ComplianceRule>();
            _codeProfiles = new Dictionary<string, BuildingCodeProfile>(StringComparer.OrdinalIgnoreCase);

            InitializeStandardRules();
            InitializeCodeProfiles();
        }

        #region Public API

        /// <summary>
        /// Sets the active building code profile.
        /// </summary>
        public void SetCodeProfile(string profileName)
        {
            if (_codeProfiles.TryGetValue(profileName, out var profile))
            {
                _activeProfile = profile;
                Logger.Info($"Set active code profile: {profileName}");
            }
            else
            {
                throw new ArgumentException($"Unknown code profile: {profileName}");
            }
        }

        /// <summary>
        /// Gets available code profiles.
        /// </summary>
        public IEnumerable<string> GetAvailableProfiles() => _codeProfiles.Keys;

        /// <summary>
        /// Checks a design element against all applicable rules.
        /// </summary>
        public async Task<ComplianceResult> CheckAsync(DesignElement element)
        {
            return await Task.Run(() =>
            {
                Logger.Debug($"Checking compliance for element: {element.Id} ({element.Type})");

                var result = new ComplianceResult
                {
                    ElementId = element.Id,
                    ElementType = element.Type,
                    CheckedAt = DateTime.Now,
                    Profile = _activeProfile?.Name ?? "Default"
                };

                var applicableRules = GetApplicableRules(element);

                foreach (var rule in applicableRules)
                {
                    try
                    {
                        var ruleResult = EvaluateRule(rule, element);
                        result.RuleResults.Add(ruleResult);

                        if (!ruleResult.Passed)
                        {
                            result.Violations.Add(new ComplianceViolation
                            {
                                RuleId = rule.Id,
                                RuleName = rule.Name,
                                Severity = rule.Severity,
                                Description = ruleResult.Message,
                                Standard = rule.Standard,
                                Clause = rule.Clause,
                                Remediation = rule.Remediation
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Rule {rule.Id} failed to evaluate");
                    }
                }

                // Calculate overall compliance score
                result.IsCompliant = !result.Violations.Any(v => v.Severity >= ViolationSeverity.Error);
                result.Score = CalculateComplianceScore(result);

                Logger.Debug($"Compliance check complete: Score={result.Score:P0}, Violations={result.Violations.Count}");

                return result;
            });
        }

        /// <summary>
        /// Checks multiple elements in batch.
        /// </summary>
        public async Task<BatchComplianceResult> CheckBatchAsync(IEnumerable<DesignElement> elements)
        {
            var result = new BatchComplianceResult
            {
                CheckedAt = DateTime.Now,
                Profile = _activeProfile?.Name ?? "Default"
            };

            foreach (var element in elements)
            {
                var elementResult = await CheckAsync(element);
                result.ElementResults.Add(elementResult);
            }

            // Aggregate results
            result.TotalElements = result.ElementResults.Count;
            result.CompliantElements = result.ElementResults.Count(r => r.IsCompliant);
            result.TotalViolations = result.ElementResults.Sum(r => r.Violations.Count);
            result.CriticalViolations = result.ElementResults
                .Sum(r => r.Violations.Count(v => v.Severity == ViolationSeverity.Critical));
            result.OverallScore = result.ElementResults.Count > 0
                ? result.ElementResults.Average(r => r.Score)
                : 1.0;

            return result;
        }

        /// <summary>
        /// Gets a compliance report for the entire design.
        /// </summary>
        public ComplianceReport GenerateReport(BatchComplianceResult batchResult)
        {
            var report = new ComplianceReport
            {
                GeneratedAt = DateTime.Now,
                Profile = batchResult.Profile,
                Summary = new ComplianceSummary
                {
                    TotalElements = batchResult.TotalElements,
                    CompliantElements = batchResult.CompliantElements,
                    NonCompliantElements = batchResult.TotalElements - batchResult.CompliantElements,
                    TotalViolations = batchResult.TotalViolations,
                    CriticalViolations = batchResult.CriticalViolations,
                    OverallScore = batchResult.OverallScore
                }
            };

            // Group violations by standard
            report.ViolationsByStandard = batchResult.ElementResults
                .SelectMany(r => r.Violations)
                .GroupBy(v => v.Standard)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            // Group violations by category
            report.ViolationsByCategory = batchResult.ElementResults
                .SelectMany(r => r.Violations)
                .GroupBy(v => GetViolationCategory(v))
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(batchResult);

            return report;
        }

        #endregion

        #region Rule Initialization

        private void InitializeStandardRules()
        {
            // ===== EGRESS RULES (IBC Chapter 10) =====
            _rules.Add(new ComplianceRule
            {
                Id = "IBC-1005.1",
                Name = "Minimum Egress Width",
                Category = ComplianceCategory.Egress,
                Standard = "IBC 2021",
                Clause = "1005.1",
                Description = "Egress width shall be minimum 0.3 inches per occupant",
                AppliesTo = new[] { "Corridor", "Stair", "Exit" },
                Severity = ViolationSeverity.Critical,
                Evaluate = (element, profile) =>
                {
                    var width = element.GetProperty<double>("Width");
                    var minWidth = profile?.MinCorridorWidth ?? 1.12; // 44 inches default

                    if (width < minWidth)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Width ({width:F2}m) is below minimum ({minWidth}m)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase corridor/stair width to meet minimum requirements"
            });

            _rules.Add(new ComplianceRule
            {
                Id = "IBC-1020.4",
                Name = "Dead-End Corridor Limit",
                Category = ComplianceCategory.Egress,
                Standard = "IBC 2021",
                Clause = "1020.4",
                Description = "Dead-end corridors shall not exceed 20 feet (6m) or 50 feet (15m) with sprinklers",
                AppliesTo = new[] { "Corridor" },
                Severity = ViolationSeverity.Error,
                Evaluate = (element, profile) =>
                {
                    var isDeadEnd = element.GetProperty<bool>("IsDeadEnd");
                    if (!isDeadEnd) return RuleResult.Pass();

                    var length = element.GetProperty<double>("Length");
                    var maxLength = profile?.MaxDeadEndLength ?? 6.0;

                    if (length > maxLength)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Dead-end length ({length:F1}m) exceeds maximum ({maxLength}m)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Reduce dead-end length or provide additional exit"
            });

            // ===== ACCESSIBILITY RULES (ADA/EN) =====
            _rules.Add(new ComplianceRule
            {
                Id = "ADA-404.2.3",
                Name = "Door Clear Width",
                Category = ComplianceCategory.Accessibility,
                Standard = "ADA 2010",
                Clause = "404.2.3",
                Description = "Doorways shall provide minimum 32 inch (815mm) clear width",
                AppliesTo = new[] { "Door" },
                Severity = ViolationSeverity.Error,
                Evaluate = (element, profile) =>
                {
                    var clearWidth = element.GetProperty<double>("ClearWidth");
                    var minWidth = profile?.MinDoorClearWidth ?? 0.815;

                    if (clearWidth < minWidth)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Clear width ({clearWidth:F3}m) below minimum ({minWidth}m)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase door width to provide minimum 815mm clear opening"
            });

            _rules.Add(new ComplianceRule
            {
                Id = "ADA-304.3",
                Name = "Turning Space",
                Category = ComplianceCategory.Accessibility,
                Standard = "ADA 2010",
                Clause = "304.3",
                Description = "Turning space shall be 60 inch (1525mm) diameter minimum",
                AppliesTo = new[] { "Room", "Bathroom", "Kitchen" },
                Severity = ViolationSeverity.Warning,
                Evaluate = (element, profile) =>
                {
                    var width = element.GetProperty<double>("Width");
                    var length = element.GetProperty<double>("Length");
                    var minDimension = Math.Min(width, length);
                    var minTurning = profile?.MinTurningRadius ?? 1.525;

                    if (minDimension < minTurning)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Room dimension ({minDimension:F2}m) may not allow wheelchair turning"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Ensure minimum 1.5m clear floor space for wheelchair turning"
            });

            // ===== FIRE SAFETY RULES (NFPA) =====
            _rules.Add(new ComplianceRule
            {
                Id = "NFPA-101-7.2.4",
                Name = "Exit Door Swing Direction",
                Category = ComplianceCategory.FireSafety,
                Standard = "NFPA 101",
                Clause = "7.2.4.3",
                Description = "Exit doors shall swing in direction of egress travel",
                AppliesTo = new[] { "Door" },
                Severity = ViolationSeverity.Error,
                Evaluate = (element, profile) =>
                {
                    var isExit = element.GetProperty<bool>("IsExit");
                    if (!isExit) return RuleResult.Pass();

                    var swingsInEgressDirection = element.GetProperty<bool>("SwingsInEgressDirection");

                    if (!swingsInEgressDirection)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = "Exit door does not swing in direction of egress travel"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Reverse door swing to open in direction of exit travel"
            });

            _rules.Add(new ComplianceRule
            {
                Id = "NFPA-101-7.3.1",
                Name = "Exit Signage",
                Category = ComplianceCategory.FireSafety,
                Standard = "NFPA 101",
                Clause = "7.3.1",
                Description = "Exits shall be marked with approved exit signs",
                AppliesTo = new[] { "Exit", "ExitDoor" },
                Severity = ViolationSeverity.Warning,
                Evaluate = (element, profile) =>
                {
                    var hasExitSign = element.GetProperty<bool>("HasExitSign");

                    if (!hasExitSign)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = "Exit requires illuminated exit signage"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Add illuminated exit sign above exit door"
            });

            // ===== VENTILATION RULES (ASHRAE) =====
            _rules.Add(new ComplianceRule
            {
                Id = "ASHRAE-62.1-6.2",
                Name = "Minimum Ventilation Rate",
                Category = ComplianceCategory.Ventilation,
                Standard = "ASHRAE 62.1",
                Clause = "6.2",
                Description = "Outdoor air ventilation shall meet minimum requirements",
                AppliesTo = new[] { "Room" },
                Severity = ViolationSeverity.Warning,
                Evaluate = (element, profile) =>
                {
                    var area = element.GetProperty<double>("Area");
                    var occupants = element.GetProperty<int>("Occupants");
                    var ventilationRate = element.GetProperty<double>("VentilationRate");

                    // Minimum: 0.06 CFM/ft² + 5 CFM/person = ~0.3 L/s/m² + 2.5 L/s/person
                    var minRate = (area * 0.3) + (occupants * 2.5);

                    if (ventilationRate < minRate)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Ventilation rate ({ventilationRate:F1} L/s) below minimum ({minRate:F1} L/s)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase ventilation capacity or reduce occupant load"
            });

            // ===== NATURAL LIGHT RULES =====
            _rules.Add(new ComplianceRule
            {
                Id = "IBC-1205.1",
                Name = "Natural Light",
                Category = ComplianceCategory.Lighting,
                Standard = "IBC 2021",
                Clause = "1205.1",
                Description = "Habitable rooms shall have glazing area not less than 8% of floor area",
                AppliesTo = new[] { "Bedroom", "Living", "Office", "Kitchen" },
                Severity = ViolationSeverity.Warning,
                Evaluate = (element, profile) =>
                {
                    var floorArea = element.GetProperty<double>("Area");
                    var glazingArea = element.GetProperty<double>("GlazingArea");
                    var minRatio = profile?.MinGlazingRatio ?? 0.08;

                    var actualRatio = floorArea > 0 ? glazingArea / floorArea : 0;

                    if (actualRatio < minRatio)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Glazing ratio ({actualRatio:P1}) below minimum ({minRatio:P0})"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase window area or add skylights"
            });

            // ===== STRUCTURAL RULES =====
            _rules.Add(new ComplianceRule
            {
                Id = "STRUCT-001",
                Name = "Minimum Wall Thickness",
                Category = ComplianceCategory.Structural,
                Standard = "General Structural",
                Description = "Load-bearing walls shall have minimum thickness",
                AppliesTo = new[] { "Wall" },
                Severity = ViolationSeverity.Critical,
                Evaluate = (element, profile) =>
                {
                    var isLoadBearing = element.GetProperty<bool>("IsLoadBearing");
                    if (!isLoadBearing) return RuleResult.Pass();

                    var thickness = element.GetProperty<double>("Thickness");
                    var minThickness = profile?.MinLoadBearingWallThickness ?? 0.2;

                    if (thickness < minThickness)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Load-bearing wall thickness ({thickness:F3}m) below minimum ({minThickness}m)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase wall thickness to structural minimum"
            });

            // ===== ROOM SIZE RULES =====
            _rules.Add(new ComplianceRule
            {
                Id = "IBC-1208.1",
                Name = "Minimum Room Area",
                Category = ComplianceCategory.General,
                Standard = "IBC 2021",
                Clause = "1208.1",
                Description = "Habitable spaces shall be minimum 70 sq ft (6.5 m²)",
                AppliesTo = new[] { "Room", "Bedroom", "Living", "Office" },
                Severity = ViolationSeverity.Error,
                Evaluate = (element, profile) =>
                {
                    var area = element.GetProperty<double>("Area");
                    var roomType = element.GetProperty<string>("RoomType");
                    var minArea = GetMinimumRoomArea(roomType, profile);

                    if (area < minArea)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Room area ({area:F1}m²) below minimum for {roomType} ({minArea}m²)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase room dimensions to meet minimum area requirements"
            });

            _rules.Add(new ComplianceRule
            {
                Id = "IBC-1208.2",
                Name = "Minimum Ceiling Height",
                Category = ComplianceCategory.General,
                Standard = "IBC 2021",
                Clause = "1208.2",
                Description = "Habitable rooms shall have ceiling height minimum 7 feet (2.13m)",
                AppliesTo = new[] { "Room", "Bedroom", "Living", "Office", "Kitchen" },
                Severity = ViolationSeverity.Error,
                Evaluate = (element, profile) =>
                {
                    var height = element.GetProperty<double>("Height");
                    var minHeight = profile?.MinCeilingHeight ?? 2.13;

                    if (height < minHeight)
                    {
                        return new RuleResult
                        {
                            Passed = false,
                            Message = $"Ceiling height ({height:F2}m) below minimum ({minHeight}m)"
                        };
                    }
                    return RuleResult.Pass();
                },
                Remediation = "Increase ceiling height to meet minimum requirements"
            });

            Logger.Info($"Initialized {_rules.Count} compliance rules");
        }

        private void InitializeCodeProfiles()
        {
            // IBC 2021 (US)
            _codeProfiles["IBC2021"] = new BuildingCodeProfile
            {
                Name = "IBC 2021",
                Description = "International Building Code 2021",
                Region = "United States",
                MinCorridorWidth = 1.12,
                MinDoorClearWidth = 0.815,
                MinCeilingHeight = 2.13,
                MinGlazingRatio = 0.08,
                MinLoadBearingWallThickness = 0.2,
                MinTurningRadius = 1.525,
                MaxDeadEndLength = 6.0
            };

            // UK Building Regulations
            _codeProfiles["UKBR"] = new BuildingCodeProfile
            {
                Name = "UK Building Regulations",
                Description = "UK Building Regulations Part B/K/M",
                Region = "United Kingdom",
                MinCorridorWidth = 1.2,
                MinDoorClearWidth = 0.775,
                MinCeilingHeight = 2.3,
                MinGlazingRatio = 0.1,
                MinLoadBearingWallThickness = 0.2,
                MinTurningRadius = 1.5,
                MaxDeadEndLength = 7.5
            };

            // East Africa Community
            _codeProfiles["EAC"] = new BuildingCodeProfile
            {
                Name = "East African Community",
                Description = "EAC Building Codes (Kenya, Uganda, Tanzania)",
                Region = "East Africa",
                MinCorridorWidth = 1.0,
                MinDoorClearWidth = 0.8,
                MinCeilingHeight = 2.4,
                MinGlazingRatio = 0.15, // Higher for natural ventilation
                MinLoadBearingWallThickness = 0.2,
                MinTurningRadius = 1.5,
                MaxDeadEndLength = 6.0
            };

            // Set default profile
            _activeProfile = _codeProfiles["IBC2021"];
        }

        #endregion

        #region Private Methods

        private IEnumerable<ComplianceRule> GetApplicableRules(DesignElement element)
        {
            return _rules.Where(r =>
                r.AppliesTo == null ||
                r.AppliesTo.Length == 0 ||
                r.AppliesTo.Any(t => t.Equals(element.Type, StringComparison.OrdinalIgnoreCase)));
        }

        private RuleResult EvaluateRule(ComplianceRule rule, DesignElement element)
        {
            return rule.Evaluate(element, _activeProfile);
        }

        private double CalculateComplianceScore(ComplianceResult result)
        {
            if (result.RuleResults.Count == 0) return 1.0;

            var passed = result.RuleResults.Count(r => r.Passed);
            var total = result.RuleResults.Count;

            // Weight critical violations more heavily
            var criticalPenalty = result.Violations.Count(v => v.Severity == ViolationSeverity.Critical) * 0.2;
            var errorPenalty = result.Violations.Count(v => v.Severity == ViolationSeverity.Error) * 0.1;

            var baseScore = (double)passed / total;
            return Math.Max(0, baseScore - criticalPenalty - errorPenalty);
        }

        private static double GetMinimumRoomArea(string roomType, BuildingCodeProfile profile)
        {
            return roomType?.ToLowerInvariant() switch
            {
                "bedroom" => 9.0,
                "living" or "living room" => 12.0,
                "kitchen" => 6.0,
                "bathroom" => 2.5,
                "office" => 6.0,
                _ => 6.5
            };
        }

        private string GetViolationCategory(ComplianceViolation violation)
        {
            return violation.Standard?.Split(' ').FirstOrDefault() ?? "General";
        }

        private List<string> GenerateRecommendations(BatchComplianceResult batchResult)
        {
            var recommendations = new List<string>();

            var criticalCount = batchResult.CriticalViolations;
            if (criticalCount > 0)
            {
                recommendations.Add($"URGENT: Address {criticalCount} critical violation(s) before proceeding");
            }

            // Group by category
            var byCategory = batchResult.ElementResults
                .SelectMany(r => r.Violations)
                .GroupBy(v => v.Severity)
                .OrderByDescending(g => (int)g.Key);

            foreach (var group in byCategory)
            {
                if (group.Key == ViolationSeverity.Critical)
                {
                    var distinctIssues = group.Select(v => v.RuleName).Distinct();
                    foreach (var issue in distinctIssues)
                    {
                        recommendations.Add($"Fix: {issue}");
                    }
                }
            }

            if (batchResult.OverallScore >= 0.9)
            {
                recommendations.Add("Design shows good compliance - minor improvements recommended");
            }
            else if (batchResult.OverallScore >= 0.7)
            {
                recommendations.Add("Design needs attention to several code requirements");
            }
            else
            {
                recommendations.Add("Design requires significant revisions for compliance");
            }

            return recommendations;
        }

        #endregion
    }

    #region Supporting Types

    public class DesignElement
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();

        public T GetProperty<T>(string name)
        {
            if (Properties.TryGetValue(name, out var value))
            {
                if (value is T typedValue)
                    return typedValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }
    }

    public class ComplianceRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ComplianceCategory Category { get; set; }
        public string Standard { get; set; }
        public string Clause { get; set; }
        public string Description { get; set; }
        public string[] AppliesTo { get; set; }
        public ViolationSeverity Severity { get; set; }
        public Func<DesignElement, BuildingCodeProfile, RuleResult> Evaluate { get; set; }
        public string Remediation { get; set; }
    }

    public class RuleResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; }

        public static RuleResult Pass() => new RuleResult { Passed = true };
    }

    public class ComplianceResult
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public DateTime CheckedAt { get; set; }
        public string Profile { get; set; }
        public bool IsCompliant { get; set; }
        public double Score { get; set; }
        public List<RuleResult> RuleResults { get; set; } = new();
        public List<ComplianceViolation> Violations { get; set; } = new();
    }

    public class ComplianceViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string Description { get; set; }
        public string Standard { get; set; }
        public string Clause { get; set; }
        public string Remediation { get; set; }
    }

    public class BatchComplianceResult
    {
        public DateTime CheckedAt { get; set; }
        public string Profile { get; set; }
        public int TotalElements { get; set; }
        public int CompliantElements { get; set; }
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public double OverallScore { get; set; }
        public List<ComplianceResult> ElementResults { get; set; } = new();
    }

    public class ComplianceReport
    {
        public DateTime GeneratedAt { get; set; }
        public string Profile { get; set; }
        public ComplianceSummary Summary { get; set; }
        public Dictionary<string, List<ComplianceViolation>> ViolationsByStandard { get; set; }
        public Dictionary<string, List<ComplianceViolation>> ViolationsByCategory { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ComplianceSummary
    {
        public int TotalElements { get; set; }
        public int CompliantElements { get; set; }
        public int NonCompliantElements { get; set; }
        public int TotalViolations { get; set; }
        public int CriticalViolations { get; set; }
        public double OverallScore { get; set; }
    }

    public class BuildingCodeProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Region { get; set; }
        public double MinCorridorWidth { get; set; }
        public double MinDoorClearWidth { get; set; }
        public double MinCeilingHeight { get; set; }
        public double MinGlazingRatio { get; set; }
        public double MinLoadBearingWallThickness { get; set; }
        public double MinTurningRadius { get; set; }
        public double MaxDeadEndLength { get; set; }
    }

    public enum ComplianceCategory
    {
        General,
        Egress,
        Accessibility,
        FireSafety,
        Structural,
        Ventilation,
        Lighting,
        Plumbing,
        Electrical
    }

    public enum ViolationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    #endregion
}
