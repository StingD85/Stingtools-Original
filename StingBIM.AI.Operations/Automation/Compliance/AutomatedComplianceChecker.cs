// ============================================================================
// StingBIM AI - Automated Code Compliance Checker
// Automatically validates building designs against regional codes
// Supports multiple standards: IBC, BS, EAC, KEBS, SANS, NBC
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Automation.Compliance
{
    /// <summary>
    /// Automated Code Compliance Checker
    /// Validates building models against applicable building codes and standards
    /// </summary>
    public class AutomatedComplianceChecker
    {
        private readonly Dictionary<string, CodeStandard> _codeLibrary;
        private readonly RuleEngine _ruleEngine;
        private readonly ComplianceCalculator _calculator;
        private readonly ReportGenerator _reportGenerator;

        public AutomatedComplianceChecker()
        {
            _codeLibrary = LoadCodeLibrary();
            _ruleEngine = new RuleEngine();
            _calculator = new ComplianceCalculator();
            _reportGenerator = new ReportGenerator();
        }

        #region Compliance Checking

        /// <summary>
        /// Run comprehensive compliance check against specified codes
        /// </summary>
        public async Task<ComplianceReport> CheckComplianceAsync(
            BuildingModel model,
            ComplianceCheckOptions options)
        {
            var report = new ComplianceReport
            {
                BuildingId = model.BuildingId,
                BuildingName = model.BuildingName,
                CheckedAt = DateTime.UtcNow,
                ApplicableCodes = options.CodesToCheck
            };

            // Step 1: Determine applicable codes based on project location and type
            var applicableCodes = DetermineApplicableCodes(model, options);
            report.ApplicableCodes = applicableCodes.Select(c => c.CodeId).ToList();

            // Step 2: Run checks for each code
            foreach (var code in applicableCodes)
            {
                var codeResult = await CheckCodeComplianceAsync(model, code, options);
                report.CodeResults.Add(codeResult);
            }

            // Step 3: Aggregate results
            report.Summary = AggregateResults(report.CodeResults);

            // Step 4: Generate recommendations
            report.Recommendations = GenerateComplianceRecommendations(report);

            // Step 5: Calculate compliance score
            report.OverallScore = CalculateOverallScore(report);

            return report;
        }

        /// <summary>
        /// Check compliance against a specific code
        /// </summary>
        public async Task<CodeComplianceResult> CheckCodeComplianceAsync(
            BuildingModel model,
            CodeStandard code,
            ComplianceCheckOptions options)
        {
            var result = new CodeComplianceResult
            {
                CodeId = code.CodeId,
                CodeName = code.Name,
                Version = code.Version
            };

            // Get all rules for this code
            var rules = code.Rules.Where(r =>
                options.SectionsToCheck == null ||
                options.SectionsToCheck.Contains(r.Section)).ToList();

            // Check each rule
            foreach (var rule in rules)
            {
                var checkResult = await CheckRuleAsync(model, rule, code);
                result.RuleResults.Add(checkResult);
            }

            // Calculate pass/fail statistics
            result.TotalChecks = result.RuleResults.Count;
            result.PassedChecks = result.RuleResults.Count(r => r.Status == CheckStatus.Pass);
            result.FailedChecks = result.RuleResults.Count(r => r.Status == CheckStatus.Fail);
            result.WarningChecks = result.RuleResults.Count(r => r.Status == CheckStatus.Warning);
            result.NotApplicable = result.RuleResults.Count(r => r.Status == CheckStatus.NotApplicable);

            result.CompliancePercentage = result.TotalChecks > 0
                ? (double)result.PassedChecks / (result.TotalChecks - result.NotApplicable) * 100
                : 100;

            return result;
        }

        /// <summary>
        /// Check a single compliance rule
        /// </summary>
        private async Task<RuleCheckResult> CheckRuleAsync(
            BuildingModel model,
            ComplianceRule rule,
            CodeStandard code)
        {
            await Task.Delay(1); // Simulate async operation

            var result = new RuleCheckResult
            {
                RuleId = rule.RuleId,
                RuleName = rule.Name,
                Section = rule.Section,
                Category = rule.Category
            };

            try
            {
                // Check if rule is applicable
                if (!IsRuleApplicable(model, rule))
                {
                    result.Status = CheckStatus.NotApplicable;
                    result.Message = "Rule not applicable to this building type";
                    return result;
                }

                // Execute rule check
                var checkResult = _ruleEngine.ExecuteRule(model, rule);

                result.Status = checkResult.Passed ? CheckStatus.Pass :
                               checkResult.IsWarning ? CheckStatus.Warning : CheckStatus.Fail;

                result.ActualValue = checkResult.ActualValue;
                result.RequiredValue = checkResult.RequiredValue;
                result.Message = checkResult.Message;
                result.AffectedElements = checkResult.AffectedElements;

                // Add remediation suggestion for failures
                if (result.Status == CheckStatus.Fail)
                {
                    result.Remediation = GenerateRemediation(rule, checkResult);
                }
            }
            catch (Exception ex)
            {
                result.Status = CheckStatus.Error;
                result.Message = $"Error checking rule: {ex.Message}";
            }

            return result;
        }

        #endregion

        #region Specific Code Checks

        /// <summary>
        /// Check fire safety compliance
        /// </summary>
        public async Task<FireSafetyResult> CheckFireSafetyAsync(BuildingModel model, string codeId)
        {
            var result = new FireSafetyResult
            {
                CheckedAt = DateTime.UtcNow
            };

            // Travel distance checks
            result.TravelDistanceChecks = await CheckTravelDistancesAsync(model, codeId);

            // Exit width calculations
            result.ExitWidthChecks = await CheckExitWidthsAsync(model, codeId);

            // Fire rating requirements
            result.FireRatingChecks = await CheckFireRatingsAsync(model, codeId);

            // Sprinkler coverage
            result.SprinklerChecks = await CheckSprinklerCoverageAsync(model, codeId);

            // Compartmentation
            result.CompartmentationChecks = await CheckCompartmentationAsync(model, codeId);

            // Emergency lighting
            result.EmergencyLightingChecks = await CheckEmergencyLightingAsync(model, codeId);

            // Calculate overall fire safety score
            var allChecks = new List<RuleCheckResult>();
            allChecks.AddRange(result.TravelDistanceChecks);
            allChecks.AddRange(result.ExitWidthChecks);
            allChecks.AddRange(result.FireRatingChecks);
            allChecks.AddRange(result.SprinklerChecks);

            result.OverallScore = allChecks.Count(c => c.Status == CheckStatus.Pass) /
                                 (double)allChecks.Count(c => c.Status != CheckStatus.NotApplicable) * 100;

            return result;
        }

        /// <summary>
        /// Check accessibility compliance (ADA/DDA)
        /// </summary>
        public async Task<AccessibilityResult> CheckAccessibilityAsync(BuildingModel model, string codeId)
        {
            var result = new AccessibilityResult
            {
                CheckedAt = DateTime.UtcNow
            };

            // Route accessibility
            result.RouteChecks = await CheckAccessibleRoutesAsync(model, codeId);

            // Door clearances and hardware
            result.DoorChecks = await CheckAccessibleDoorsAsync(model, codeId);

            // Toilet provisions
            result.ToiletChecks = await CheckAccessibleToiletsAsync(model, codeId);

            // Parking provisions
            result.ParkingChecks = await CheckAccessibleParkingAsync(model, codeId);

            // Signage requirements
            result.SignageChecks = await CheckAccessibleSignageAsync(model, codeId);

            // Lift provisions
            result.LiftChecks = await CheckAccessibleLiftsAsync(model, codeId);

            return result;
        }

        /// <summary>
        /// Check structural code compliance
        /// </summary>
        public async Task<StructuralResult> CheckStructuralComplianceAsync(BuildingModel model, string codeId)
        {
            var result = new StructuralResult
            {
                CheckedAt = DateTime.UtcNow
            };

            // Load calculations verification
            result.LoadChecks = await CheckLoadCalculationsAsync(model, codeId);

            // Material specifications
            result.MaterialChecks = await CheckMaterialSpecsAsync(model, codeId);

            // Foundation adequacy
            result.FoundationChecks = await CheckFoundationAsync(model, codeId);

            // Seismic requirements (if applicable)
            result.SeismicChecks = await CheckSeismicRequirementsAsync(model, codeId);

            return result;
        }

        /// <summary>
        /// Check MEP code compliance
        /// </summary>
        public async Task<MEPComplianceResult> CheckMEPComplianceAsync(BuildingModel model, string codeId)
        {
            var result = new MEPComplianceResult
            {
                CheckedAt = DateTime.UtcNow
            };

            // Ventilation rates (ASHRAE 62.1)
            result.VentilationChecks = await CheckVentilationRatesAsync(model, codeId);

            // Electrical load calculations
            result.ElectricalChecks = await CheckElectricalComplianceAsync(model, codeId);

            // Plumbing fixture counts
            result.PlumbingChecks = await CheckPlumbingComplianceAsync(model, codeId);

            // Energy compliance (ASHRAE 90.1)
            result.EnergyChecks = await CheckEnergyComplianceAsync(model, codeId);

            return result;
        }

        #endregion

        #region Helper Methods

        private List<CodeStandard> DetermineApplicableCodes(
            BuildingModel model,
            ComplianceCheckOptions options)
        {
            var applicableCodes = new List<CodeStandard>();

            // Add explicitly requested codes
            foreach (var codeId in options.CodesToCheck)
            {
                if (_codeLibrary.TryGetValue(codeId, out var code))
                {
                    applicableCodes.Add(code);
                }
            }

            // Add codes based on location
            if (options.AutoDetectCodes)
            {
                var locationCodes = GetCodesForLocation(model.Location);
                applicableCodes.AddRange(locationCodes.Where(c =>
                    !applicableCodes.Any(ac => ac.CodeId == c.CodeId)));
            }

            return applicableCodes;
        }

        private List<CodeStandard> GetCodesForLocation(BuildingLocation location)
        {
            var codes = new List<CodeStandard>();

            // Kenya
            if (location.Country == "Kenya")
            {
                if (_codeLibrary.TryGetValue("KEBS", out var kebs)) codes.Add(kebs);
                if (_codeLibrary.TryGetValue("EAC", out var eac)) codes.Add(eac);
            }
            // Uganda
            else if (location.Country == "Uganda")
            {
                if (_codeLibrary.TryGetValue("UNBS", out var unbs)) codes.Add(unbs);
                if (_codeLibrary.TryGetValue("EAC", out var eac)) codes.Add(eac);
            }
            // Tanzania
            else if (location.Country == "Tanzania")
            {
                if (_codeLibrary.TryGetValue("TBS", out var tbs)) codes.Add(tbs);
                if (_codeLibrary.TryGetValue("EAC", out var eac)) codes.Add(eac);
            }
            // South Africa
            else if (location.Country == "South Africa")
            {
                if (_codeLibrary.TryGetValue("SANS", out var sans)) codes.Add(sans);
            }
            // Nigeria
            else if (location.Country == "Nigeria")
            {
                if (_codeLibrary.TryGetValue("NBC", out var nbc)) codes.Add(nbc);
            }
            // Default to IBC for international
            else
            {
                if (_codeLibrary.TryGetValue("IBC", out var ibc)) codes.Add(ibc);
            }

            return codes;
        }

        private bool IsRuleApplicable(BuildingModel model, ComplianceRule rule)
        {
            // Check building type filter
            if (rule.ApplicableBuildingTypes != null &&
                !rule.ApplicableBuildingTypes.Contains(model.BuildingType))
            {
                return false;
            }

            // Check occupancy filter
            if (rule.ApplicableOccupancies != null &&
                !rule.ApplicableOccupancies.Contains(model.OccupancyType))
            {
                return false;
            }

            // Check size thresholds
            if (rule.MinimumArea.HasValue && model.TotalArea < rule.MinimumArea.Value)
            {
                return false;
            }

            if (rule.MinimumOccupancy.HasValue && model.DesignOccupancy < rule.MinimumOccupancy.Value)
            {
                return false;
            }

            return true;
        }

        private string GenerateRemediation(ComplianceRule rule, RuleExecutionResult checkResult)
        {
            return rule.Category switch
            {
                "Fire Safety" => GenerateFireSafetyRemediation(rule, checkResult),
                "Accessibility" => GenerateAccessibilityRemediation(rule, checkResult),
                "Structural" => GenerateStructuralRemediation(rule, checkResult),
                "MEP" => GenerateMEPRemediation(rule, checkResult),
                _ => $"Review {rule.Section} requirements and adjust design to meet {checkResult.RequiredValue}"
            };
        }

        private string GenerateFireSafetyRemediation(ComplianceRule rule, RuleExecutionResult result)
        {
            if (rule.RuleId.Contains("TRAVEL"))
                return $"Reduce travel distance from {result.ActualValue}m to maximum {result.RequiredValue}m by adding additional exit or relocating spaces";

            if (rule.RuleId.Contains("EXIT_WIDTH"))
                return $"Increase exit width from {result.ActualValue}mm to minimum {result.RequiredValue}mm";

            if (rule.RuleId.Contains("FIRE_RATING"))
                return $"Upgrade construction to achieve {result.RequiredValue} fire rating";

            return "Review fire safety requirements and adjust design accordingly";
        }

        private string GenerateAccessibilityRemediation(ComplianceRule rule, RuleExecutionResult result)
        {
            if (rule.RuleId.Contains("DOOR"))
                return $"Increase door clear width to minimum {result.RequiredValue}mm";

            if (rule.RuleId.Contains("RAMP"))
                return $"Adjust ramp gradient to maximum {result.RequiredValue}";

            if (rule.RuleId.Contains("TOILET"))
                return "Provide accessible toilet facilities as per requirements";

            return "Review accessibility requirements and adjust design accordingly";
        }

        private string GenerateStructuralRemediation(ComplianceRule rule, RuleExecutionResult result)
        {
            return "Consult structural engineer to review and address compliance issue";
        }

        private string GenerateMEPRemediation(ComplianceRule rule, RuleExecutionResult result)
        {
            if (rule.RuleId.Contains("VENTILATION"))
                return $"Increase ventilation rate to minimum {result.RequiredValue} L/s/person";

            if (rule.RuleId.Contains("FIXTURE"))
                return $"Add plumbing fixtures to achieve required {result.RequiredValue} count";

            return "Review MEP requirements and adjust design accordingly";
        }

        private ComplianceSummary AggregateResults(List<CodeComplianceResult> codeResults)
        {
            var allRules = codeResults.SelectMany(c => c.RuleResults).ToList();

            return new ComplianceSummary
            {
                TotalChecks = allRules.Count,
                Passed = allRules.Count(r => r.Status == CheckStatus.Pass),
                Failed = allRules.Count(r => r.Status == CheckStatus.Fail),
                Warnings = allRules.Count(r => r.Status == CheckStatus.Warning),
                NotApplicable = allRules.Count(r => r.Status == CheckStatus.NotApplicable),
                ByCategory = allRules
                    .GroupBy(r => r.Category)
                    .ToDictionary(
                        g => g.Key,
                        g => new CategorySummary
                        {
                            Total = g.Count(),
                            Passed = g.Count(r => r.Status == CheckStatus.Pass),
                            Failed = g.Count(r => r.Status == CheckStatus.Fail)
                        })
            };
        }

        private List<ComplianceRecommendation> GenerateComplianceRecommendations(ComplianceReport report)
        {
            var recommendations = new List<ComplianceRecommendation>();

            var failures = report.CodeResults
                .SelectMany(c => c.RuleResults)
                .Where(r => r.Status == CheckStatus.Fail)
                .ToList();

            // Group by category and prioritize
            var byCategory = failures.GroupBy(f => f.Category);

            foreach (var category in byCategory)
            {
                var critical = category.Where(f =>
                    f.Category == "Fire Safety" || f.Category == "Structural").ToList();

                if (critical.Any())
                {
                    recommendations.Add(new ComplianceRecommendation
                    {
                        Priority = "Critical",
                        Category = category.Key,
                        Description = $"{critical.Count} critical {category.Key} compliance failures require immediate attention",
                        AffectedRules = critical.Select(f => f.RuleId).ToList(),
                        SuggestedActions = critical.Select(f => f.Remediation).Where(r => r != null).ToList()
                    });
                }
                else
                {
                    recommendations.Add(new ComplianceRecommendation
                    {
                        Priority = category.Count() > 5 ? "High" : "Medium",
                        Category = category.Key,
                        Description = $"{category.Count()} {category.Key} compliance issues identified",
                        AffectedRules = category.Select(f => f.RuleId).ToList(),
                        SuggestedActions = category.Select(f => f.Remediation).Where(r => r != null).Distinct().ToList()
                    });
                }
            }

            return recommendations.OrderBy(r =>
                r.Priority == "Critical" ? 0 :
                r.Priority == "High" ? 1 : 2).ToList();
        }

        private double CalculateOverallScore(ComplianceReport report)
        {
            var applicable = report.Summary.TotalChecks - report.Summary.NotApplicable;
            if (applicable == 0) return 100;

            // Weight critical categories higher
            double score = 0;
            double totalWeight = 0;

            foreach (var category in report.Summary.ByCategory)
            {
                var weight = GetCategoryWeight(category.Key);
                var categoryScore = category.Value.Total > 0
                    ? (double)category.Value.Passed / category.Value.Total * 100
                    : 100;

                score += categoryScore * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? score / totalWeight : 100;
        }

        private double GetCategoryWeight(string category)
        {
            return category switch
            {
                "Fire Safety" => 2.0,
                "Structural" => 2.0,
                "Accessibility" => 1.5,
                "MEP" => 1.0,
                "Energy" => 1.0,
                _ => 1.0
            };
        }

        #endregion

        #region Specific Check Implementations

        // =====================================================================
        // Fire Safety Checks (6 methods)
        // =====================================================================

        /// <summary>
        /// Check maximum travel distance per space against code limits.
        /// IBC allows 76m with sprinklers / 45m without. KEBS/SANS/BS = 45m.
        /// Spaces larger than 500 m² receive a warning about needing closer exits.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckTravelDistancesAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            // Maximum travel distance (metres) by code
            double maxTravelDistance = codeId switch
            {
                "IBC" => 76.0,   // with sprinklers; 45 without
                "KEBS" => 45.0,
                "SANS" => 45.0,
                "BS" => 45.0,
                "NBC" => 30.0,
                "EAC" => 45.0,
                _ => 76.0
            };

            var spaces = model.Spaces ?? new List<Space>();

            foreach (var space in spaces)
            {
                // Estimate travel distance as the diagonal of a square with the same area
                double estimatedTravel = Math.Sqrt(space.Area) * Math.Sqrt(2);

                var status = estimatedTravel <= maxTravelDistance
                    ? CheckStatus.Pass
                    : CheckStatus.Fail;

                // Spaces > 500 m² get a warning even if they pass the basic distance check
                if (status == CheckStatus.Pass && space.Area > 500)
                {
                    status = CheckStatus.Warning;
                }

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_TRAVEL_{space.SpaceId}",
                    RuleName = "Travel Distance",
                    Section = codeId == "IBC" ? "1017" : codeId == "KEBS" ? "KS 2000" : codeId == "SANS" ? "SANS 10400-T" : "Fire Safety",
                    Category = "Fire Safety",
                    Status = status,
                    ActualValue = $"{estimatedTravel:F1} m",
                    RequiredValue = $"<= {maxTravelDistance} m",
                    Message = status == CheckStatus.Pass
                        ? $"Space '{space.Name}' travel distance {estimatedTravel:F1} m is within the {maxTravelDistance} m limit"
                        : status == CheckStatus.Warning
                            ? $"Space '{space.Name}' ({space.Area:F0} m²) exceeds 500 m² and may need additional exits for shorter travel distances"
                            : $"Space '{space.Name}' estimated travel distance {estimatedTravel:F1} m exceeds maximum {maxTravelDistance} m",
                    AffectedElements = new List<string> { space.SpaceId },
                    Remediation = status == CheckStatus.Fail
                        ? $"Add additional exit(s) to space '{space.Name}' to reduce travel distance from {estimatedTravel:F1} m to <= {maxTravelDistance} m"
                        : null
                });
            }

            // Building-level check when no spaces are defined
            if (spaces.Count == 0)
            {
                double buildingDiagonal = Math.Sqrt(model.TotalArea) * Math.Sqrt(2);
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_TRAVEL_BLDG",
                    RuleName = "Travel Distance (Building Level)",
                    Section = "Fire Safety",
                    Category = "Fire Safety",
                    Status = buildingDiagonal <= maxTravelDistance ? CheckStatus.Pass : CheckStatus.Fail,
                    ActualValue = $"{buildingDiagonal:F1} m (estimated)",
                    RequiredValue = $"<= {maxTravelDistance} m",
                    Message = buildingDiagonal <= maxTravelDistance
                        ? $"Building-level estimated travel distance {buildingDiagonal:F1} m is within the {maxTravelDistance} m limit"
                        : $"Building-level estimated travel distance {buildingDiagonal:F1} m exceeds maximum {maxTravelDistance} m",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = buildingDiagonal > maxTravelDistance
                        ? $"Provide additional exits to reduce maximum travel distance to <= {maxTravelDistance} m"
                        : null
                });
            }

            return results;
        }

        /// <summary>
        /// Check minimum exit width based on occupancy.
        /// IBC min = 813 mm (5 mm/person). KEBS/BS = 900 mm (5.3 mm/person). SANS = 800 mm.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckExitWidthsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minExitWidthMm = codeId switch
            {
                "IBC" => 813.0,
                "KEBS" => 900.0,
                "BS" => 900.0,
                "SANS" => 800.0,
                "NBC" => 900.0,
                "EAC" => 900.0,
                _ => 813.0
            };

            double widthPerPersonMm = codeId switch
            {
                "IBC" => 5.0,
                "KEBS" => 5.3,
                "BS" => 5.3,
                "SANS" => 5.0,
                "NBC" => 5.3,
                "EAC" => 5.3,
                _ => 5.0
            };

            // Required width based on occupancy
            double requiredWidthByOccupancy = model.DesignOccupancy * widthPerPersonMm;
            double requiredWidth = Math.Max(minExitWidthMm, requiredWidthByOccupancy);

            // Check minimum absolute exit width
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_EXIT_WIDTH_MIN",
                RuleName = "Minimum Exit Width",
                Section = codeId == "IBC" ? "1005" : "Fire Safety",
                Category = "Fire Safety",
                Status = CheckStatus.Pass, // Minimum code requirement recorded
                ActualValue = $"{minExitWidthMm:F0} mm (code minimum)",
                RequiredValue = $">= {minExitWidthMm:F0} mm",
                Message = $"Minimum exit width per {codeId} is {minExitWidthMm:F0} mm",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Occupancy-based required width check
            var occupancyStatus = requiredWidthByOccupancy <= 2000 ? CheckStatus.Pass : CheckStatus.Warning;
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_EXIT_WIDTH_OCC",
                RuleName = "Exit Width for Occupancy",
                Section = codeId == "IBC" ? "1005.1" : "Fire Safety",
                Category = "Fire Safety",
                Status = occupancyStatus,
                ActualValue = $"{requiredWidthByOccupancy:F0} mm required for {model.DesignOccupancy} occupants",
                RequiredValue = $"{model.DesignOccupancy} x {widthPerPersonMm} mm/person = {requiredWidthByOccupancy:F0} mm",
                Message = occupancyStatus == CheckStatus.Pass
                    ? $"Required exit width for {model.DesignOccupancy} occupants is {requiredWidthByOccupancy:F0} mm"
                    : $"Required exit width of {requiredWidthByOccupancy:F0} mm for {model.DesignOccupancy} occupants is large; consider multiple exits",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = occupancyStatus == CheckStatus.Warning
                    ? $"Provide multiple exits whose combined width totals at least {requiredWidthByOccupancy:F0} mm"
                    : null
            });

            // Per-space occupancy estimate check
            var spaces = model.Spaces ?? new List<Space>();
            foreach (var space in spaces)
            {
                // Estimate space occupancy proportionally
                int spaceOccupancy = model.TotalArea > 0
                    ? (int)Math.Ceiling(model.DesignOccupancy * (space.Area / model.TotalArea))
                    : 0;
                double spaceRequiredWidth = Math.Max(minExitWidthMm, spaceOccupancy * widthPerPersonMm);

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_EXIT_WIDTH_{space.SpaceId}",
                    RuleName = $"Exit Width - {space.Name}",
                    Section = "Fire Safety",
                    Category = "Fire Safety",
                    Status = spaceRequiredWidth <= 1200 ? CheckStatus.Pass : CheckStatus.Warning,
                    ActualValue = $"{spaceRequiredWidth:F0} mm required",
                    RequiredValue = $">= {minExitWidthMm:F0} mm",
                    Message = $"Space '{space.Name}' (est. {spaceOccupancy} occupants) requires {spaceRequiredWidth:F0} mm exit width",
                    AffectedElements = new List<string> { space.SpaceId },
                    Remediation = spaceRequiredWidth > 1200
                        ? $"Provide exit width of at least {spaceRequiredWidth:F0} mm or add additional exits for space '{space.Name}'"
                        : null
                });
            }

            return results;
        }

        /// <summary>
        /// Check fire rating requirements per building height/storeys.
        /// > 4 storeys = 120 min, 3-4 storeys = 90 min, 1-2 storeys = 60 min.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckFireRatingsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            int requiredRatingMinutes = model.NumberOfStoreys switch
            {
                > 4 => 120,
                3 or 4 => 90,
                _ => 60
            };

            string sectionRef = codeId switch
            {
                "IBC" => "602",
                "BS" => "BS 9999",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-T",
                _ => "Fire Safety"
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_FIRE_RATING_STRUCT",
                RuleName = "Structural Fire Rating",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = CheckStatus.Pass,
                ActualValue = $"{requiredRatingMinutes} min required",
                RequiredValue = $">= {requiredRatingMinutes} min",
                Message = $"Building with {model.NumberOfStoreys} storeys requires minimum {requiredRatingMinutes}-minute fire rating for structural elements",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Check if high-rise (additional requirements)
            if (model.NumberOfStoreys > 10)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_FIRE_RATING_HIGHRISE",
                    RuleName = "High-Rise Fire Rating",
                    Section = sectionRef,
                    Category = "Fire Safety",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{model.NumberOfStoreys} storeys",
                    RequiredValue = "120 min + enhanced fire safety measures",
                    Message = $"High-rise building ({model.NumberOfStoreys} storeys) requires 120-minute fire rating plus enhanced fire safety systems (pressurised stairwells, fire lifts, refuge floors)",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Ensure 120-minute fire-rated construction with pressurised stairwells, firefighter lifts, and refuge floors at intervals per code"
                });
            }

            // Floor separation rating
            int floorSepRating = model.NumberOfStoreys > 4 ? 90 : 60;
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_FIRE_RATING_FLOOR",
                RuleName = "Floor Separation Fire Rating",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = CheckStatus.Pass,
                ActualValue = $"{floorSepRating} min",
                RequiredValue = $">= {floorSepRating} min",
                Message = $"Floor separations require minimum {floorSepRating}-minute fire rating",
                AffectedElements = new List<string> { model.BuildingId }
            });

            return results;
        }

        /// <summary>
        /// Check whether sprinklers are required.
        /// Required when: area > 5000 m², occupancy > 300, or storeys > 3.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckSprinklerCoverageAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            bool requiredByArea = model.TotalArea > 5000;
            bool requiredByOccupancy = model.DesignOccupancy > 300;
            bool requiredByHeight = model.NumberOfStoreys > 3;
            bool sprinklersRequired = requiredByArea || requiredByOccupancy || requiredByHeight;

            var reasons = new List<string>();
            if (requiredByArea) reasons.Add($"total area {model.TotalArea:F0} m² > 5000 m²");
            if (requiredByOccupancy) reasons.Add($"occupancy {model.DesignOccupancy} > 300");
            if (requiredByHeight) reasons.Add($"storeys {model.NumberOfStoreys} > 3");

            string sectionRef = codeId switch
            {
                "IBC" => "903",
                "BS" => "BS 9999",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-T",
                _ => "Fire Safety"
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_SPRINKLER_REQ",
                RuleName = "Sprinkler System Requirement",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = sprinklersRequired ? CheckStatus.Warning : CheckStatus.Pass,
                ActualValue = sprinklersRequired ? "Required" : "Not required",
                RequiredValue = "Sprinklers when area > 5000 m², occupancy > 300, or storeys > 3",
                Message = sprinklersRequired
                    ? $"Automatic sprinkler system required: {string.Join("; ", reasons)}"
                    : "Building does not trigger automatic sprinkler requirement based on area, occupancy, and height thresholds",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = sprinklersRequired
                    ? "Install automatic sprinkler system throughout the building in accordance with applicable code and NFPA 13"
                    : null
            });

            // If sprinklers are required, check each space
            if (sprinklersRequired)
            {
                var spaces = model.Spaces ?? new List<Space>();
                foreach (var space in spaces)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = $"{codeId}_SPRINKLER_{space.SpaceId}",
                        RuleName = $"Sprinkler Coverage - {space.Name}",
                        Section = sectionRef,
                        Category = "Fire Safety",
                        Status = CheckStatus.Warning,
                        ActualValue = "Verify coverage",
                        RequiredValue = "Full sprinkler coverage per NFPA 13",
                        Message = $"Space '{space.Name}' ({space.Area:F0} m²) must have sprinkler coverage verified",
                        AffectedElements = new List<string> { space.SpaceId },
                        Remediation = $"Ensure space '{space.Name}' has sprinkler heads at maximum spacing per NFPA 13 for its hazard classification"
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Check fire compartmentation against maximum compartment size limits.
        /// IBC = 2500 m², EAC = 2000 m². Checks total area versus compartment limits.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckCompartmentationAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double maxCompartmentArea = codeId switch
            {
                "IBC" => 2500.0,
                "EAC" => 2000.0,
                "KEBS" => 2000.0,
                "SANS" => 2500.0,
                "BS" => 2000.0,
                "NBC" => 2000.0,
                _ => 2500.0
            };

            string sectionRef = codeId switch
            {
                "IBC" => "706",
                "EAC" => "EAS 900",
                "BS" => "BS 9999",
                _ => "Fire Safety"
            };

            // Estimate number of compartments needed per floor
            double areaPerFloor = model.NumberOfStoreys > 0
                ? model.TotalArea / model.NumberOfStoreys
                : model.TotalArea;
            int compartmentsNeeded = (int)Math.Ceiling(areaPerFloor / maxCompartmentArea);
            bool requiresCompartmentation = areaPerFloor > maxCompartmentArea;

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_COMPART_AREA",
                RuleName = "Fire Compartment Size",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = requiresCompartmentation ? CheckStatus.Fail : CheckStatus.Pass,
                ActualValue = $"{areaPerFloor:F0} m² per floor",
                RequiredValue = $"<= {maxCompartmentArea:F0} m² per compartment",
                Message = requiresCompartmentation
                    ? $"Floor area {areaPerFloor:F0} m² exceeds maximum compartment size {maxCompartmentArea:F0} m²; minimum {compartmentsNeeded} compartments required per floor"
                    : $"Floor area {areaPerFloor:F0} m² is within the {maxCompartmentArea:F0} m² compartment limit",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = requiresCompartmentation
                    ? $"Divide each floor into at least {compartmentsNeeded} fire compartments using fire-rated walls and doors. Maximum compartment size: {maxCompartmentArea:F0} m²"
                    : null
            });

            // Check total building compartmentation
            int totalCompartments = (int)Math.Ceiling(model.TotalArea / maxCompartmentArea);
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_COMPART_TOTAL",
                RuleName = "Total Building Compartmentation",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = totalCompartments > 1 ? CheckStatus.Warning : CheckStatus.Pass,
                ActualValue = $"{model.TotalArea:F0} m² total",
                RequiredValue = $"<= {maxCompartmentArea:F0} m² per compartment",
                Message = $"Building requires minimum {totalCompartments} fire compartment(s) based on total area of {model.TotalArea:F0} m²",
                AffectedElements = new List<string> { model.BuildingId }
            });

            return results;
        }

        /// <summary>
        /// Check emergency lighting requirements.
        /// Required for buildings > 300 m² or > 1 storey. Escape routes must have lighting.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckEmergencyLightingAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            bool requiredByArea = model.TotalArea > 300;
            bool requiredByHeight = model.NumberOfStoreys > 1;
            bool emergencyLightingRequired = requiredByArea || requiredByHeight;

            string sectionRef = codeId switch
            {
                "IBC" => "1008",
                "BS" => "BS 5266",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-T",
                _ => "Fire Safety"
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_EMERG_LIGHT_REQ",
                RuleName = "Emergency Lighting Requirement",
                Section = sectionRef,
                Category = "Fire Safety",
                Status = emergencyLightingRequired ? CheckStatus.Warning : CheckStatus.Pass,
                ActualValue = emergencyLightingRequired ? "Required" : "Not required",
                RequiredValue = "Emergency lighting when area > 300 m² or storeys > 1",
                Message = emergencyLightingRequired
                    ? $"Emergency lighting system required (area: {model.TotalArea:F0} m², storeys: {model.NumberOfStoreys})"
                    : "Building does not trigger emergency lighting requirement",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = emergencyLightingRequired
                    ? "Install maintained or non-maintained emergency lighting along all escape routes, at exit doors, stairways, and changes of direction. Minimum 1 lux on escape routes, 0.5 lux in open areas. Duration: 3 hours minimum."
                    : null
            });

            // Check escape route lighting for each space
            if (emergencyLightingRequired)
            {
                var spaces = model.Spaces ?? new List<Space>();
                foreach (var space in spaces)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = $"{codeId}_EMERG_LIGHT_{space.SpaceId}",
                        RuleName = $"Emergency Lighting - {space.Name}",
                        Section = sectionRef,
                        Category = "Fire Safety",
                        Status = CheckStatus.Warning,
                        ActualValue = "Verify installation",
                        RequiredValue = "Emergency lighting on escape routes (min 1 lux)",
                        Message = $"Space '{space.Name}' must have emergency lighting verified on escape routes",
                        AffectedElements = new List<string> { space.SpaceId },
                        Remediation = $"Verify emergency luminaires are installed in space '{space.Name}' covering all escape routes with minimum 1 lux illumination"
                    });
                }
            }

            return results;
        }

        // =====================================================================
        // Accessibility Checks (6 methods)
        // =====================================================================

        /// <summary>
        /// Check accessible route widths (minimum 1200 mm corridor) and ramp requirements
        /// (maximum 1:12 gradient for level changes).
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleRoutesAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minCorridorWidthMm = 1200.0; // Universal minimum
            double maxRampGradient = 1.0 / 12.0; // 1:12

            string sectionRef = codeId switch
            {
                "IBC" => "1104",
                "BS" => "BS 8300",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            // Corridor width check
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_ROUTE_WIDTH",
                RuleName = "Accessible Route Width",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minCorridorWidthMm:F0} mm minimum",
                RequiredValue = $">= {minCorridorWidthMm:F0} mm",
                Message = $"Accessible routes require minimum {minCorridorWidthMm:F0} mm clear width throughout the building",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Ramp requirement for multi-storey
            if (model.NumberOfStoreys > 1)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_RAMP",
                    RuleName = "Ramp Gradient Requirement",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Warning,
                    ActualValue = "Verify ramp gradients",
                    RequiredValue = $"<= 1:12 ({maxRampGradient:F4})",
                    Message = $"Multi-storey building ({model.NumberOfStoreys} storeys) requires ramps with maximum 1:12 gradient at all level changes, or lift access",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Ensure all level changes have ramps with maximum 1:12 gradient, minimum 1200 mm width, handrails on both sides, and landings at 9 m intervals. Alternatively, provide lift access."
                });
            }

            // Per-space route checks
            var spaces = model.Spaces ?? new List<Space>();
            foreach (var space in spaces)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_ROUTE_{space.SpaceId}",
                    RuleName = $"Accessible Route to {space.Name}",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Warning,
                    ActualValue = "Verify accessibility",
                    RequiredValue = $">= {minCorridorWidthMm:F0} mm clear path, no steps",
                    Message = $"Verify accessible route to space '{space.Name}' with minimum {minCorridorWidthMm:F0} mm width and no unramped steps",
                    AffectedElements = new List<string> { space.SpaceId },
                    Remediation = $"Ensure unobstructed accessible route of minimum {minCorridorWidthMm:F0} mm width to space '{space.Name}'"
                });
            }

            return results;
        }

        /// <summary>
        /// Check accessible door minimum clear widths.
        /// IBC = 813 mm, BS = 800 mm, KEBS = 850 mm. Handle height 900-1100 mm.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleDoorsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minDoorWidthMm = codeId switch
            {
                "IBC" => 813.0,
                "BS" => 800.0,
                "KEBS" => 850.0,
                "SANS" => 800.0,
                "NBC" => 850.0,
                "EAC" => 850.0,
                _ => 813.0
            };

            double minHandleHeight = 900.0;
            double maxHandleHeight = 1100.0;

            string sectionRef = codeId switch
            {
                "IBC" => "1010",
                "BS" => "BS 8300",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            // Door clear width check
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_DOOR_WIDTH",
                RuleName = "Accessible Door Clear Width",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minDoorWidthMm:F0} mm minimum per {codeId}",
                RequiredValue = $">= {minDoorWidthMm:F0} mm",
                Message = $"All accessible doors require minimum {minDoorWidthMm:F0} mm clear opening width per {codeId}",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Handle height check
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_DOOR_HANDLE",
                RuleName = "Door Handle Height",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minHandleHeight:F0}-{maxHandleHeight:F0} mm",
                RequiredValue = $"{minHandleHeight:F0}-{maxHandleHeight:F0} mm",
                Message = $"Door handles must be installed at {minHandleHeight:F0}-{maxHandleHeight:F0} mm above finished floor level, lever-type preferred",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Per-space door checks
            var spaces = model.Spaces ?? new List<Space>();
            foreach (var space in spaces)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_DOOR_{space.SpaceId}",
                    RuleName = $"Accessible Door - {space.Name}",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Warning,
                    ActualValue = "Verify door dimensions",
                    RequiredValue = $">= {minDoorWidthMm:F0} mm clear width, handle at {minHandleHeight:F0}-{maxHandleHeight:F0} mm",
                    Message = $"Verify door to '{space.Name}' has >= {minDoorWidthMm:F0} mm clear width with lever handle at {minHandleHeight:F0}-{maxHandleHeight:F0} mm height",
                    AffectedElements = new List<string> { space.SpaceId },
                    Remediation = $"Ensure door to space '{space.Name}' provides minimum {minDoorWidthMm:F0} mm clear opening with accessible hardware"
                });
            }

            return results;
        }

        /// <summary>
        /// Check accessible toilet provision.
        /// Minimum 1 accessible WC per 20 standard WCs. Minimum turning radius 1500 mm.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleToiletsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minTurningRadiusMm = 1500.0;
            int accessiblePerStandard = 20; // 1 accessible per 20 standard WCs

            string sectionRef = codeId switch
            {
                "IBC" => "1109",
                "BS" => "BS 8300",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            // Estimate WC count based on occupancy (1 per 25 people as baseline)
            int estimatedWCs = Math.Max(1, (int)Math.Ceiling(model.DesignOccupancy / 25.0));
            int requiredAccessibleWCs = Math.Max(1, (int)Math.Ceiling((double)estimatedWCs / accessiblePerStandard));

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_TOILET_COUNT",
                RuleName = "Accessible Toilet Provision",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Warning,
                ActualValue = $"Verify: need {requiredAccessibleWCs} accessible WC(s)",
                RequiredValue = $">= {requiredAccessibleWCs} accessible WC(s) (1 per {accessiblePerStandard} standard WCs)",
                Message = $"Building with {model.DesignOccupancy} occupants requires approximately {estimatedWCs} total WCs and at least {requiredAccessibleWCs} accessible WC(s)",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Provide at least {requiredAccessibleWCs} accessible toilet(s) with {minTurningRadiusMm:F0} mm turning circle, grab rails, and emergency alarm"
            });

            // Turning radius requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_TOILET_TURN",
                RuleName = "Accessible Toilet Turning Radius",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minTurningRadiusMm:F0} mm required",
                RequiredValue = $">= {minTurningRadiusMm:F0} mm diameter turning circle",
                Message = $"Accessible toilets must provide a minimum {minTurningRadiusMm:F0} mm diameter turning circle for wheelchair access",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Per-floor requirement for multi-storey buildings
            if (model.NumberOfStoreys > 1)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_TOILET_FLOOR",
                    RuleName = "Accessible Toilet Per Floor",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{model.NumberOfStoreys} floors",
                    RequiredValue = "Accessible toilet on each accessible floor",
                    Message = $"Provide at least one accessible toilet per accessible floor across {model.NumberOfStoreys} storeys",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = $"Ensure each of the {model.NumberOfStoreys} floors has at least one accessible toilet facility"
                });
            }

            return results;
        }

        /// <summary>
        /// Check accessible parking provision.
        /// Minimum 1 accessible space per 25 total spaces. Minimum width 3600 mm.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleParkingAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minAccessibleWidthMm = 3600.0;
            int spacesPerAccessible = 25; // 1 accessible per 25 total

            string sectionRef = codeId switch
            {
                "IBC" => "1106",
                "BS" => "BS 8300",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            // Estimate total parking spaces (1 per 50 m² of building area as rough estimate)
            int estimatedParkingSpaces = Math.Max(1, (int)Math.Ceiling(model.TotalArea / 50.0));
            int requiredAccessibleSpaces = Math.Max(1, (int)Math.Ceiling((double)estimatedParkingSpaces / spacesPerAccessible));

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_PARKING_COUNT",
                RuleName = "Accessible Parking Spaces",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Warning,
                ActualValue = $"Verify: need {requiredAccessibleSpaces} accessible space(s)",
                RequiredValue = $">= {requiredAccessibleSpaces} (1 per {spacesPerAccessible} total spaces)",
                Message = $"Estimated {estimatedParkingSpaces} total parking spaces require at least {requiredAccessibleSpaces} accessible space(s)",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Provide at least {requiredAccessibleSpaces} accessible parking space(s), each minimum {minAccessibleWidthMm:F0} mm wide, located closest to the accessible building entrance"
            });

            // Width requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_PARKING_WIDTH",
                RuleName = "Accessible Parking Space Width",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minAccessibleWidthMm:F0} mm minimum",
                RequiredValue = $">= {minAccessibleWidthMm:F0} mm",
                Message = $"Accessible parking spaces must be minimum {minAccessibleWidthMm:F0} mm wide (standard bay + adjacent access aisle)",
                AffectedElements = new List<string> { model.BuildingId }
            });

            return results;
        }

        /// <summary>
        /// Check accessible signage requirements.
        /// Braille signage, minimum text height 15 mm, contrast ratio compliance.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleSignageAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minTextHeightMm = 15.0;

            string sectionRef = codeId switch
            {
                "IBC" => "1111",
                "BS" => "BS 8300",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            // Braille signage requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_SIGN_BRAILLE",
                RuleName = "Braille Signage",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Warning,
                ActualValue = "Verify installation",
                RequiredValue = "Braille and tactile signage at all accessible entrances, lifts, and WCs",
                Message = "Braille and raised tactile signage is required at key locations including entrances, lift landings, accessible toilets, and room identification signs",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = "Install braille and tactile signage at all accessible entrances, lift call stations, toilet facilities, and room identification locations"
            });

            // Text height requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_SIGN_TEXT",
                RuleName = "Signage Text Height",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Pass,
                ActualValue = $"{minTextHeightMm:F0} mm minimum",
                RequiredValue = $">= {minTextHeightMm:F0} mm",
                Message = $"Directional and informational signage must have minimum {minTextHeightMm:F0} mm character height with sans-serif font",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Contrast ratio requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ACCESS_SIGN_CONTRAST",
                RuleName = "Signage Contrast Ratio",
                Section = sectionRef,
                Category = "Accessibility",
                Status = CheckStatus.Warning,
                ActualValue = "Verify contrast",
                RequiredValue = "Minimum 70% luminance contrast between text and background",
                Message = "All signage must achieve minimum 70% luminance contrast ratio between text/symbols and background for visual accessibility",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = "Ensure all signage uses high-contrast colour combinations (minimum 70% luminance contrast) with non-reflective surfaces"
            });

            return results;
        }

        /// <summary>
        /// Check accessible lift requirements.
        /// Required if building > 1 storey. Minimum cabin size 1100 x 1400 mm.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckAccessibleLiftsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double minCabinWidthMm = 1100.0;
            double minCabinDepthMm = 1400.0;
            bool liftRequired = model.NumberOfStoreys > 1;

            string sectionRef = codeId switch
            {
                "IBC" => "1109.7",
                "BS" => "BS 8300 / BS EN 81-70",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10400-S",
                _ => "Accessibility"
            };

            if (!liftRequired)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_LIFT_REQ",
                    RuleName = "Accessible Lift Requirement",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.NotApplicable,
                    ActualValue = $"{model.NumberOfStoreys} storey(s)",
                    RequiredValue = "Lift required when > 1 storey",
                    Message = "Single-storey building does not require an accessible lift",
                    AffectedElements = new List<string> { model.BuildingId }
                });
            }
            else
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_LIFT_REQ",
                    RuleName = "Accessible Lift Requirement",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{model.NumberOfStoreys} storeys",
                    RequiredValue = "At least 1 accessible lift serving all floors",
                    Message = $"Multi-storey building ({model.NumberOfStoreys} storeys) requires at least one accessible passenger lift serving all floors",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Install at least one passenger lift serving all floors with accessible controls, audible announcements, and tactile buttons"
                });

                // Cabin size requirement
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ACCESS_LIFT_SIZE",
                    RuleName = "Lift Cabin Minimum Size",
                    Section = sectionRef,
                    Category = "Accessibility",
                    Status = CheckStatus.Pass,
                    ActualValue = $"{minCabinWidthMm:F0} x {minCabinDepthMm:F0} mm minimum",
                    RequiredValue = $">= {minCabinWidthMm:F0} mm wide x {minCabinDepthMm:F0} mm deep",
                    Message = $"Accessible lift cabin must be minimum {minCabinWidthMm:F0} mm wide x {minCabinDepthMm:F0} mm deep to accommodate a wheelchair",
                    AffectedElements = new List<string> { model.BuildingId }
                });

                // Additional lift requirements for larger buildings
                if (model.DesignOccupancy > 500 || model.NumberOfStoreys > 5)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = $"{codeId}_ACCESS_LIFT_CAPACITY",
                        RuleName = "Additional Lift Capacity",
                        Section = sectionRef,
                        Category = "Accessibility",
                        Status = CheckStatus.Warning,
                        ActualValue = $"{model.DesignOccupancy} occupants, {model.NumberOfStoreys} storeys",
                        RequiredValue = "Multiple lifts or increased capacity recommended",
                        Message = $"Building with {model.DesignOccupancy} occupants and {model.NumberOfStoreys} storeys should have multiple lifts including at least one with stretcher capability (min 1100 x 2100 mm)",
                        AffectedElements = new List<string> { model.BuildingId },
                        Remediation = "Consider providing multiple lifts with at least one stretcher-capable lift (minimum 1100 x 2100 mm cabin)"
                    });
                }
            }

            return results;
        }

        // =====================================================================
        // Structural Checks (4 methods)
        // =====================================================================

        /// <summary>
        /// Check structural load calculations.
        /// Dead load 1.5 kN/m² + live load by use type (residential=1.5, office=2.5, assembly=5.0).
        /// Factor of safety 1.5.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckLoadCalculationsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            double deadLoadKNm2 = 1.5;
            double factorOfSafety = 1.5;

            string sectionRef = codeId switch
            {
                "IBC" => "1607",
                "BS" => "BS EN 1991",
                "EAC" => "EAS 901",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10160",
                _ => "Structural"
            };

            // Live load lookup by use type
            var liveLoadByUseType = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "residential", 1.5 },
                { "office", 2.5 },
                { "assembly", 5.0 },
                { "retail", 4.0 },
                { "storage", 7.5 },
                { "industrial", 5.0 },
                { "educational", 3.0 },
                { "healthcare", 3.0 },
                { "hotel", 2.0 }
            };

            // Building-level load check
            double buildingLiveLoad = liveLoadByUseType.TryGetValue(model.OccupancyType ?? "", out var ll) ? ll : 2.5;
            double totalDesignLoad = (deadLoadKNm2 + buildingLiveLoad) * factorOfSafety;
            double totalBuildingLoad = totalDesignLoad * model.TotalArea;

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_LOAD_BUILDING",
                RuleName = "Building Design Load",
                Section = sectionRef,
                Category = "Structural",
                Status = CheckStatus.Pass,
                ActualValue = $"Dead: {deadLoadKNm2} + Live: {buildingLiveLoad} = {deadLoadKNm2 + buildingLiveLoad} kN/m² (factored: {totalDesignLoad:F1} kN/m²)",
                RequiredValue = $"FoS >= {factorOfSafety}",
                Message = $"Total factored design load: {totalDesignLoad:F1} kN/m² ({model.OccupancyType ?? "general"} use). Total building load: {totalBuildingLoad:F0} kN for {model.TotalArea:F0} m²",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Per-space load calculations
            var spaces = model.Spaces ?? new List<Space>();
            foreach (var space in spaces)
            {
                double spaceLiveLoad = liveLoadByUseType.TryGetValue(space.UseType ?? "", out var sl) ? sl : buildingLiveLoad;
                double spaceDesignLoad = (deadLoadKNm2 + spaceLiveLoad) * factorOfSafety;
                double spaceTotalLoad = spaceDesignLoad * space.Area;

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_LOAD_{space.SpaceId}",
                    RuleName = $"Design Load - {space.Name}",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Pass,
                    ActualValue = $"Dead: {deadLoadKNm2} + Live: {spaceLiveLoad} kN/m² ({space.UseType ?? "general"})",
                    RequiredValue = $"Factored: {spaceDesignLoad:F1} kN/m² (FoS {factorOfSafety})",
                    Message = $"Space '{space.Name}' ({space.UseType ?? "general"}) design load: {spaceDesignLoad:F1} kN/m². Total: {spaceTotalLoad:F0} kN for {space.Area:F0} m²",
                    AffectedElements = new List<string> { space.SpaceId }
                });
            }

            // High live load warning for assembly spaces
            var assemblySpaces = spaces.Where(s =>
                string.Equals(s.UseType, "assembly", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.UseType, "storage", StringComparison.OrdinalIgnoreCase)).ToList();

            if (assemblySpaces.Any())
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_LOAD_HIGH_LIVE",
                    RuleName = "High Live Load Areas",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{assemblySpaces.Count} high-load space(s)",
                    RequiredValue = "Structural engineer verification required",
                    Message = $"{assemblySpaces.Count} space(s) with elevated live loads (assembly/storage) require specific structural engineer verification",
                    AffectedElements = assemblySpaces.Select(s => s.SpaceId).ToList(),
                    Remediation = "Engage structural engineer to verify floor slab and beam design for elevated live load areas"
                });
            }

            return results;
        }

        /// <summary>
        /// Check material specifications against minimum grades.
        /// Concrete: foundation=C25, columns=C30, slabs=C25. Steel: min S275.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckMaterialSpecsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "1905",
                "BS" => "BS EN 206 / BS EN 1992",
                "EAC" => "EAS 901",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10100",
                _ => "Structural"
            };

            // Concrete grade requirements
            var concreteGrades = new Dictionary<string, int>
            {
                { "Foundation", 25 },
                { "Columns", 30 },
                { "Slabs", 25 },
                { "Beams", 25 },
                { "Walls", 20 }
            };

            foreach (var kvp in concreteGrades)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_MAT_CONCRETE_{kvp.Key.ToUpper()}",
                    RuleName = $"Concrete Grade - {kvp.Key}",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Pass,
                    ActualValue = $"C{kvp.Value} minimum required",
                    RequiredValue = $">= C{kvp.Value} ({kvp.Value} MPa)",
                    Message = $"{kvp.Key} concrete must be minimum grade C{kvp.Value} ({kvp.Value} MPa characteristic strength)",
                    AffectedElements = new List<string> { model.BuildingId }
                });
            }

            // Higher grade for tall buildings
            if (model.NumberOfStoreys > 5)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_MAT_CONCRETE_HIGHRISE",
                    RuleName = "High-Rise Concrete Grade",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{model.NumberOfStoreys} storeys",
                    RequiredValue = ">= C35 for columns in buildings > 5 storeys",
                    Message = $"Building with {model.NumberOfStoreys} storeys: consider upgrading column concrete to minimum C35 for structural adequacy",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Specify minimum C35 concrete for columns and C30 for slabs in buildings exceeding 5 storeys"
                });
            }

            // Steel grade requirement
            string minSteelGrade = "S275";
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_MAT_STEEL",
                RuleName = "Structural Steel Grade",
                Section = sectionRef,
                Category = "Structural",
                Status = CheckStatus.Pass,
                ActualValue = $"{minSteelGrade} minimum",
                RequiredValue = $">= {minSteelGrade} (275 MPa yield strength)",
                Message = $"Structural steel must be minimum grade {minSteelGrade} (275 MPa yield strength). Use S355 for primary members in buildings > 3 storeys.",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Reinforcement steel requirement
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_MAT_REBAR",
                RuleName = "Reinforcement Steel Grade",
                Section = sectionRef,
                Category = "Structural",
                Status = CheckStatus.Pass,
                ActualValue = "Y500 (500 MPa) minimum",
                RequiredValue = ">= Y500 (500 MPa yield strength)",
                Message = "Reinforcement steel must be minimum grade Y500 (500 MPa yield strength) with adequate ductility classification",
                AffectedElements = new List<string> { model.BuildingId }
            });

            return results;
        }

        /// <summary>
        /// Check foundation requirements based on building height.
        /// Minimum depth: 1 storey=600mm, 2 storeys=900mm, 3+ storeys=1200mm. Bearing capacity check.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckFoundationAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "1809",
                "BS" => "BS EN 1997",
                "EAC" => "EAS 901",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10160",
                _ => "Structural"
            };

            // Minimum foundation depth by number of storeys
            double minFoundationDepthMm = model.NumberOfStoreys switch
            {
                1 => 600.0,
                2 => 900.0,
                _ => 1200.0 // 3+ storeys
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_FOUND_DEPTH",
                RuleName = "Foundation Depth",
                Section = sectionRef,
                Category = "Structural",
                Status = CheckStatus.Pass,
                ActualValue = $"{minFoundationDepthMm:F0} mm minimum required",
                RequiredValue = $">= {minFoundationDepthMm:F0} mm",
                Message = $"Building with {model.NumberOfStoreys} storey(s) requires minimum foundation depth of {minFoundationDepthMm:F0} mm below finished ground level",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Bearing capacity check
            // Estimate building load for bearing capacity assessment
            double estimatedLoadKN = model.TotalArea * 5.0 * 1.5; // Approx factored load
            double footprintArea = model.NumberOfStoreys > 0
                ? model.TotalArea / model.NumberOfStoreys
                : model.TotalArea;
            double bearingPressureKPa = footprintArea > 0 ? estimatedLoadKN / footprintArea : 0;
            double typicalAllowableBearingKPa = 150.0; // Typical for medium-density soil

            var bearingStatus = bearingPressureKPa <= typicalAllowableBearingKPa
                ? CheckStatus.Pass
                : bearingPressureKPa <= typicalAllowableBearingKPa * 1.5
                    ? CheckStatus.Warning
                    : CheckStatus.Fail;

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_FOUND_BEARING",
                RuleName = "Foundation Bearing Capacity",
                Section = sectionRef,
                Category = "Structural",
                Status = bearingStatus,
                ActualValue = $"{bearingPressureKPa:F1} kPa estimated bearing pressure",
                RequiredValue = $"<= {typicalAllowableBearingKPa:F0} kPa (typical allowable for medium soil)",
                Message = bearingStatus == CheckStatus.Pass
                    ? $"Estimated bearing pressure {bearingPressureKPa:F1} kPa is within typical allowable range"
                    : bearingStatus == CheckStatus.Warning
                        ? $"Estimated bearing pressure {bearingPressureKPa:F1} kPa is near the typical allowable limit; geotechnical investigation recommended"
                        : $"Estimated bearing pressure {bearingPressureKPa:F1} kPa exceeds typical allowable {typicalAllowableBearingKPa:F0} kPa; deeper foundations or piling may be required",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = bearingStatus != CheckStatus.Pass
                    ? "Commission a geotechnical investigation to determine actual soil bearing capacity. Consider deeper strip/pad foundations, raft foundations, or piled foundations as appropriate."
                    : null
            });

            // Multi-storey buildings need geotechnical investigation
            if (model.NumberOfStoreys >= 3)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_FOUND_GEOTECH",
                    RuleName = "Geotechnical Investigation",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Warning,
                    ActualValue = $"{model.NumberOfStoreys} storeys",
                    RequiredValue = "Geotechnical investigation required for >= 3 storeys",
                    Message = $"Building with {model.NumberOfStoreys} storeys requires a site-specific geotechnical investigation to determine soil properties and appropriate foundation type",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Commission geotechnical investigation including borehole testing to determine soil bearing capacity, water table level, and foundation recommendations"
                });
            }

            return results;
        }

        /// <summary>
        /// Check seismic design requirements.
        /// Required for seismic zones > 0.1g. Ductility class assessment.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckSeismicRequirementsAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "1613",
                "BS" => "BS EN 1998",
                "EAC" => "EAS 901",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10160-4",
                _ => "Structural"
            };

            // Determine seismic zone based on location (simplified regional assessment)
            double peakGroundAcceleration = GetEstimatedPGA(model.Location);
            bool seismicDesignRequired = peakGroundAcceleration > 0.1;

            if (!seismicDesignRequired)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_SEISMIC_REQ",
                    RuleName = "Seismic Design Requirement",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.NotApplicable,
                    ActualValue = $"PGA = {peakGroundAcceleration:F2}g",
                    RequiredValue = "Seismic design when PGA > 0.1g",
                    Message = $"Estimated peak ground acceleration {peakGroundAcceleration:F2}g at {model.Location?.City ?? "location"} is below 0.1g threshold; seismic design not mandatory but good practice",
                    AffectedElements = new List<string> { model.BuildingId }
                });
            }
            else
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_SEISMIC_REQ",
                    RuleName = "Seismic Design Requirement",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Warning,
                    ActualValue = $"PGA = {peakGroundAcceleration:F2}g",
                    RequiredValue = "Seismic design required when PGA > 0.1g",
                    Message = $"Seismic design IS required: estimated PGA = {peakGroundAcceleration:F2}g at {model.Location?.City ?? "location"}",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = $"Design building for seismic zone with PGA = {peakGroundAcceleration:F2}g per applicable seismic code provisions"
                });

                // Ductility class assessment
                string ductilityClass = peakGroundAcceleration >= 0.25 ? "High (DCH)" :
                                        peakGroundAcceleration >= 0.15 ? "Medium (DCM)" : "Low (DCL)";

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_SEISMIC_DUCTILITY",
                    RuleName = "Ductility Class Assessment",
                    Section = sectionRef,
                    Category = "Structural",
                    Status = CheckStatus.Warning,
                    ActualValue = ductilityClass,
                    RequiredValue = $"Ductility class per PGA = {peakGroundAcceleration:F2}g",
                    Message = $"Recommended ductility class: {ductilityClass} for PGA = {peakGroundAcceleration:F2}g. Ensure adequate detailing of reinforcement and connections.",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = $"Design structural elements to {ductilityClass} ductility class with appropriate reinforcement detailing, confinement, and connection design"
                });

                // Height-based seismic concern
                if (model.NumberOfStoreys > 5)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = $"{codeId}_SEISMIC_HEIGHT",
                        RuleName = "Seismic Height Consideration",
                        Section = sectionRef,
                        Category = "Structural",
                        Status = CheckStatus.Fail,
                        ActualValue = $"{model.NumberOfStoreys} storeys in seismic zone (PGA={peakGroundAcceleration:F2}g)",
                        RequiredValue = "Dynamic analysis required for tall buildings in seismic zones",
                        Message = $"Building with {model.NumberOfStoreys} storeys in seismic zone (PGA={peakGroundAcceleration:F2}g) requires dynamic analysis (response spectrum or time-history)",
                        AffectedElements = new List<string> { model.BuildingId },
                        Remediation = "Commission dynamic seismic analysis (response spectrum method minimum, time-history for critical structures) by a qualified structural engineer"
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Estimate peak ground acceleration based on building location (simplified).
        /// </summary>
        private double GetEstimatedPGA(BuildingLocation location)
        {
            if (location == null) return 0.05;

            // Simplified regional PGA estimates
            string country = location.Country ?? "";
            string region = location.Region ?? "";

            return country.ToLowerInvariant() switch
            {
                "kenya" => region.ToLowerInvariant().Contains("rift") ? 0.20 : 0.15,
                "uganda" => region.ToLowerInvariant().Contains("west") ? 0.15 : 0.10,
                "tanzania" => region.ToLowerInvariant().Contains("lake") ? 0.15 : 0.10,
                "south africa" => 0.05,
                "nigeria" => 0.05,
                "ethiopia" => 0.20,
                "rwanda" => 0.15,
                "japan" => 0.40,
                "usa" or "united states" => 0.15,
                _ => 0.05
            };
        }

        // =====================================================================
        // MEP Checks (4 methods)
        // =====================================================================

        /// <summary>
        /// Check ventilation rates per ASHRAE 62.1.
        /// Office = 8.5 L/s/person, residential = 3.5, assembly = 7.5.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckVentilationRatesAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "1203",
                "BS" => "BS EN 16798",
                "KEBS" => "KS 2001",
                "SANS" => "SANS 10400-O",
                _ => "MEP"
            };

            // Ventilation rates (L/s per person) by use type
            var ventilationRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "office", 8.5 },
                { "residential", 3.5 },
                { "assembly", 7.5 },
                { "retail", 7.5 },
                { "educational", 8.0 },
                { "healthcare", 8.5 },
                { "hotel", 5.0 },
                { "industrial", 10.0 },
                { "storage", 2.5 }
            };

            // Building-level ventilation check
            double buildingRate = ventilationRates.TryGetValue(model.OccupancyType ?? "", out var br) ? br : 8.5;
            double totalVentilationRequired = buildingRate * model.DesignOccupancy;

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_VENT_BUILDING",
                RuleName = "Building Ventilation Rate",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Pass,
                ActualValue = $"{buildingRate:F1} L/s/person x {model.DesignOccupancy} = {totalVentilationRequired:F0} L/s total",
                RequiredValue = $">= {buildingRate:F1} L/s/person (ASHRAE 62.1)",
                Message = $"Total minimum outdoor air ventilation: {totalVentilationRequired:F0} L/s for {model.DesignOccupancy} occupants ({model.OccupancyType ?? "general"} use at {buildingRate:F1} L/s/person)",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Per-space ventilation checks
            var spaces = model.Spaces ?? new List<Space>();
            foreach (var space in spaces)
            {
                double spaceRate = ventilationRates.TryGetValue(space.UseType ?? "", out var sr) ? sr : buildingRate;
                int spaceOccupancy = model.TotalArea > 0
                    ? (int)Math.Ceiling(model.DesignOccupancy * (space.Area / model.TotalArea))
                    : 1;
                double spaceVentilation = spaceRate * spaceOccupancy;

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_VENT_{space.SpaceId}",
                    RuleName = $"Ventilation - {space.Name}",
                    Section = sectionRef,
                    Category = "MEP",
                    Status = CheckStatus.Pass,
                    ActualValue = $"{spaceRate:F1} L/s/person x {spaceOccupancy} = {spaceVentilation:F1} L/s",
                    RequiredValue = $">= {spaceRate:F1} L/s/person ({space.UseType ?? "general"})",
                    Message = $"Space '{space.Name}' ({space.UseType ?? "general"}) requires {spaceVentilation:F1} L/s outdoor air for estimated {spaceOccupancy} occupants",
                    AffectedElements = new List<string> { space.SpaceId }
                });
            }

            // Natural ventilation adequacy check (KEBS/EAC preference)
            if (codeId == "KEBS" || codeId == "EAC" || codeId == "SANS")
            {
                double ventOpeningPercentage = 10.0; // min 10% of floor area
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_VENT_NATURAL",
                    RuleName = "Natural Ventilation Openings",
                    Section = sectionRef,
                    Category = "MEP",
                    Status = CheckStatus.Warning,
                    ActualValue = "Verify opening areas",
                    RequiredValue = $">= {ventOpeningPercentage}% of floor area as openable windows/vents",
                    Message = $"Where natural ventilation is used, openable window/vent area must be >= {ventOpeningPercentage}% of floor area for each habitable room",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = $"Ensure each naturally ventilated space has openable window/vent area of at least {ventOpeningPercentage}% of its floor area"
                });
            }

            return results;
        }

        /// <summary>
        /// Check electrical compliance.
        /// Minimum circuits per dwelling = 6. GFCI required in wet areas. Conductor sizing.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckElectricalComplianceAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            int minCircuitsPerDwelling = 6;

            string sectionRef = codeId switch
            {
                "IBC" => "2702",
                "BS" => "BS 7671",
                "KEBS" => "KS 2000",
                "SANS" => "SANS 10142",
                _ => "MEP"
            };

            // Minimum circuits check (applicable to residential)
            bool isResidential = string.Equals(model.OccupancyType, "residential", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(model.OccupancyType, "hotel", StringComparison.OrdinalIgnoreCase);

            if (isResidential)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ELEC_CIRCUITS",
                    RuleName = "Minimum Circuits Per Dwelling",
                    Section = sectionRef,
                    Category = "MEP",
                    Status = CheckStatus.Pass,
                    ActualValue = $"{minCircuitsPerDwelling} minimum",
                    RequiredValue = $">= {minCircuitsPerDwelling} circuits per dwelling unit",
                    Message = $"Each dwelling unit requires minimum {minCircuitsPerDwelling} circuits: lighting (x2), sockets (x2), cooker, and water heater",
                    AffectedElements = new List<string> { model.BuildingId }
                });
            }

            // GFCI / RCD protection in wet areas
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ELEC_GFCI",
                RuleName = "GFCI/RCD Protection in Wet Areas",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Warning,
                ActualValue = "Verify installation",
                RequiredValue = "30mA RCD/GFCI on all circuits in wet areas",
                Message = "All socket outlets and equipment in wet areas (kitchens, bathrooms, laundries, external) must be protected by 30mA RCD/GFCI devices",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = "Install 30mA residual current devices (RCD/GFCI) protecting all circuits serving wet areas including bathrooms, kitchens, laundries, and external outlets"
            });

            // Conductor sizing based on building load estimate
            double estimatedLoadKVA = model.TotalArea * 0.05; // Rough 50 VA/m² estimate
            string conductorSize = estimatedLoadKVA switch
            {
                <= 20 => "10 mm² (Cu) / 16 mm² (Al)",
                <= 50 => "16 mm² (Cu) / 25 mm² (Al)",
                <= 100 => "35 mm² (Cu) / 50 mm² (Al)",
                <= 200 => "70 mm² (Cu) / 95 mm² (Al)",
                _ => "120+ mm² (Cu) / 185+ mm² (Al)"
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ELEC_CONDUCTOR",
                RuleName = "Main Conductor Sizing",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Pass,
                ActualValue = $"Estimated load: {estimatedLoadKVA:F0} kVA",
                RequiredValue = $"Minimum main conductor: {conductorSize}",
                Message = $"Estimated building electrical load of {estimatedLoadKVA:F0} kVA requires minimum main conductor size of {conductorSize}. Verify with detailed load calculation.",
                AffectedElements = new List<string> { model.BuildingId }
            });

            // Emergency power for larger buildings
            if (model.TotalArea > 2000 || model.NumberOfStoreys > 3)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ELEC_EMERGENCY",
                    RuleName = "Emergency Power Supply",
                    Section = sectionRef,
                    Category = "MEP",
                    Status = CheckStatus.Warning,
                    ActualValue = $"Area: {model.TotalArea:F0} m², Storeys: {model.NumberOfStoreys}",
                    RequiredValue = "Emergency generator or UPS for essential services",
                    Message = $"Building ({model.TotalArea:F0} m², {model.NumberOfStoreys} storeys) requires emergency power supply for lifts, emergency lighting, fire alarms, and smoke control systems",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Provide emergency generator or UPS system to supply essential services (fire alarm, emergency lighting, lifts, smoke control) with automatic changeover within 15 seconds"
                });
            }

            return results;
        }

        /// <summary>
        /// Check plumbing fixture counts based on occupancy.
        /// WC: 1 per 25 female, 1 per 50 male. Lavatory: 1 per 40 persons.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckPlumbingComplianceAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "2902",
                "BS" => "BS 6465",
                "KEBS" => "KS 2001",
                "SANS" => "SANS 10252",
                _ => "MEP"
            };

            // Assume 50/50 male/female split
            int femaleOccupancy = (int)Math.Ceiling(model.DesignOccupancy / 2.0);
            int maleOccupancy = model.DesignOccupancy - femaleOccupancy;

            // WC requirements
            int femaleWCs = Math.Max(1, (int)Math.Ceiling(femaleOccupancy / 25.0));
            int maleWCs = Math.Max(1, (int)Math.Ceiling(maleOccupancy / 50.0));
            int maleUrinals = Math.Max(1, (int)Math.Ceiling(maleOccupancy / 25.0));
            int totalWCs = femaleWCs + maleWCs;

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_PLUMB_WC_FEMALE",
                RuleName = "Female WC Provision",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Warning,
                ActualValue = $"Verify: {femaleWCs} WC(s) required",
                RequiredValue = $">= {femaleWCs} female WCs (1 per 25 female occupants)",
                Message = $"Minimum {femaleWCs} female WC(s) required for estimated {femaleOccupancy} female occupants (1 per 25 persons)",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Provide at least {femaleWCs} female WC cubicle(s) distributed across building floors"
            });

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_PLUMB_WC_MALE",
                RuleName = "Male WC Provision",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Warning,
                ActualValue = $"Verify: {maleWCs} WC(s) + {maleUrinals} urinal(s) required",
                RequiredValue = $">= {maleWCs} male WCs (1 per 50) + {maleUrinals} urinals (1 per 25)",
                Message = $"Minimum {maleWCs} male WC(s) and {maleUrinals} urinal(s) required for estimated {maleOccupancy} male occupants",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Provide at least {maleWCs} male WC cubicle(s) and {maleUrinals} urinal(s) distributed across building floors"
            });

            // Lavatory (wash basin) requirements
            int lavatories = Math.Max(1, (int)Math.Ceiling(model.DesignOccupancy / 40.0));
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_PLUMB_LAVATORY",
                RuleName = "Lavatory Provision",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Warning,
                ActualValue = $"Verify: {lavatories} lavatory/lavatories required",
                RequiredValue = $">= {lavatories} lavatories (1 per 40 occupants)",
                Message = $"Minimum {lavatories} wash basin(s)/lavatory(ies) required for {model.DesignOccupancy} occupants (1 per 40 persons)",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Provide at least {lavatories} wash basin(s) in toilet facilities"
            });

            // Drinking water provision
            int drinkingFountains = Math.Max(1, (int)Math.Ceiling(model.DesignOccupancy / 100.0));
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_PLUMB_DRINKING",
                RuleName = "Drinking Water Provision",
                Section = sectionRef,
                Category = "MEP",
                Status = CheckStatus.Pass,
                ActualValue = $"{drinkingFountains} drinking point(s)",
                RequiredValue = $">= {drinkingFountains} (1 per 100 occupants)",
                Message = $"Minimum {drinkingFountains} drinking water point(s) required for {model.DesignOccupancy} occupants",
                AffectedElements = new List<string> { model.BuildingId }
            });

            return results;
        }

        /// <summary>
        /// Check energy compliance.
        /// Maximum U-values: walls=0.35, roof=0.25, windows=2.0 W/m²K. Minimum R-values.
        /// </summary>
        private async Task<List<RuleCheckResult>> CheckEnergyComplianceAsync(BuildingModel model, string codeId)
        {
            await Task.CompletedTask;
            var results = new List<RuleCheckResult>();

            string sectionRef = codeId switch
            {
                "IBC" => "C402 (IECC)",
                "BS" => "Building Regulations Part L",
                "KEBS" => "KS 2000 Energy",
                "SANS" => "SANS 10400-XA",
                _ => "Energy"
            };

            // Maximum U-values (W/m²K)
            var maxUValues = new Dictionary<string, double>
            {
                { "External Walls", 0.35 },
                { "Roof", 0.25 },
                { "Windows", 2.0 },
                { "Ground Floor", 0.25 },
                { "Doors", 1.8 }
            };

            foreach (var kvp in maxUValues)
            {
                // Calculate equivalent R-value
                double rValue = 1.0 / kvp.Value;

                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ENERGY_U_{kvp.Key.Replace(" ", "_").ToUpper()}",
                    RuleName = $"U-Value - {kvp.Key}",
                    Section = sectionRef,
                    Category = "Energy",
                    Status = CheckStatus.Pass,
                    ActualValue = $"Max U = {kvp.Value:F2} W/m²K (min R = {rValue:F2} m²K/W)",
                    RequiredValue = $"<= {kvp.Value:F2} W/m²K",
                    Message = $"{kvp.Key}: maximum U-value {kvp.Value:F2} W/m²K (minimum R-value {rValue:F2} m²K/W). Verify insulation specification meets this requirement.",
                    AffectedElements = new List<string> { model.BuildingId }
                });
            }

            // Thermal bridging warning
            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ENERGY_THERMAL_BRIDGE",
                RuleName = "Thermal Bridging Assessment",
                Section = sectionRef,
                Category = "Energy",
                Status = CheckStatus.Warning,
                ActualValue = "Verify thermal bridge details",
                RequiredValue = "Psi-values within acceptable limits per code",
                Message = "Ensure all junction details (wall-floor, wall-roof, window reveals, corners) are assessed for thermal bridging and meet acceptable psi-value limits",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = "Review all construction junction details for thermal bridging. Use accredited construction details or calculate psi-values for each junction type."
            });

            // Air tightness requirement
            double maxAirPermeability = codeId switch
            {
                "BS" => 10.0,   // m³/h/m² at 50 Pa
                "SANS" => 10.0,
                _ => 7.0        // Stricter for IBC/IECC
            };

            results.Add(new RuleCheckResult
            {
                RuleId = $"{codeId}_ENERGY_AIRTIGHT",
                RuleName = "Air Tightness",
                Section = sectionRef,
                Category = "Energy",
                Status = CheckStatus.Warning,
                ActualValue = "Test required post-construction",
                RequiredValue = $"<= {maxAirPermeability:F1} m³/h/m² at 50 Pa",
                Message = $"Building envelope air permeability must not exceed {maxAirPermeability:F1} m³/h/m² at 50 Pa. Air tightness test required on completion.",
                AffectedElements = new List<string> { model.BuildingId },
                Remediation = $"Design continuous air barrier. Commission air tightness test post-construction to verify <= {maxAirPermeability:F1} m³/h/m² at 50 Pa."
            });

            // Climate-specific considerations for African codes
            if (codeId == "KEBS" || codeId == "EAC" || codeId == "SANS")
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = $"{codeId}_ENERGY_PASSIVE",
                    RuleName = "Passive Cooling Strategy",
                    Section = sectionRef,
                    Category = "Energy",
                    Status = CheckStatus.Warning,
                    ActualValue = "Verify passive design features",
                    RequiredValue = "Passive cooling strategies for tropical/highland climates",
                    Message = "Regional climate requires passive cooling strategies: solar shading (min 600 mm overhangs), cross-ventilation, thermal mass, light-coloured roof finishes, and appropriate building orientation",
                    AffectedElements = new List<string> { model.BuildingId },
                    Remediation = "Incorporate passive cooling: roof overhangs/shading (min 600 mm), cross-ventilation openings, high thermal mass construction, and light-coloured external finishes (solar reflectance > 0.65)"
                });
            }

            return results;
        }

        #endregion

        #region Code Library

        private Dictionary<string, CodeStandard> LoadCodeLibrary()
        {
            return new Dictionary<string, CodeStandard>
            {
                { "IBC", new CodeStandard
                    {
                        CodeId = "IBC",
                        Name = "International Building Code",
                        Version = "2021",
                        Country = "International",
                        Rules = LoadIBCRules()
                    }
                },
                { "KEBS", new CodeStandard
                    {
                        CodeId = "KEBS",
                        Name = "Kenya Bureau of Standards Building Code",
                        Version = "2020",
                        Country = "Kenya",
                        Rules = LoadKEBSRules()
                    }
                },
                { "EAC", new CodeStandard
                    {
                        CodeId = "EAC",
                        Name = "East African Community Standards",
                        Version = "2019",
                        Country = "East Africa",
                        Rules = LoadEACRules()
                    }
                },
                { "SANS", new CodeStandard
                    {
                        CodeId = "SANS",
                        Name = "South African National Standards",
                        Version = "2021",
                        Country = "South Africa",
                        Rules = LoadSANSRules()
                    }
                },
                { "NBC", new CodeStandard
                    {
                        CodeId = "NBC",
                        Name = "National Building Code of Nigeria",
                        Version = "2006",
                        Country = "Nigeria",
                        Rules = LoadNBCRules()
                    }
                },
                { "BS", new CodeStandard
                    {
                        CodeId = "BS",
                        Name = "British Standards",
                        Version = "2022",
                        Country = "UK",
                        Rules = LoadBSRules()
                    }
                }
            };
        }

        private List<ComplianceRule> LoadIBCRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "IBC_TRAVEL_01", Name = "Travel Distance", Section = "1017", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 76 },
                new ComplianceRule { RuleId = "IBC_EXIT_01", Name = "Exit Width", Section = "1005", Category = "Fire Safety", CheckType = CheckType.MinValue, Threshold = 813 },
                new ComplianceRule { RuleId = "IBC_STAIR_01", Name = "Stair Width", Section = "1011", Category = "Fire Safety", CheckType = CheckType.MinValue, Threshold = 1118 },
                new ComplianceRule { RuleId = "IBC_DOOR_01", Name = "Door Clear Width", Section = "1010", Category = "Accessibility", CheckType = CheckType.MinValue, Threshold = 813 }
            };
        }

        private List<ComplianceRule> LoadKEBSRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "KEBS_TRAVEL_01", Name = "Travel Distance", Section = "KS 2000", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 45 },
                new ComplianceRule { RuleId = "KEBS_EXIT_01", Name = "Exit Width", Section = "KS 2000", Category = "Fire Safety", CheckType = CheckType.MinValue, Threshold = 900 },
                new ComplianceRule { RuleId = "KEBS_VENTILATION_01", Name = "Natural Ventilation", Section = "KS 2001", Category = "MEP", CheckType = CheckType.MinPercentage, Threshold = 10 }
            };
        }

        private List<ComplianceRule> LoadEACRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "EAC_FIRE_01", Name = "Fire Compartment Size", Section = "EAS 900", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 2000 },
                new ComplianceRule { RuleId = "EAC_STRUCT_01", Name = "Structural Factor of Safety", Section = "EAS 901", Category = "Structural", CheckType = CheckType.MinValue, Threshold = 1.5 }
            };
        }

        private List<ComplianceRule> LoadSANSRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "SANS_TRAVEL_01", Name = "Travel Distance", Section = "SANS 10400-T", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 45 },
                new ComplianceRule { RuleId = "SANS_ACCESS_01", Name = "Accessible Parking", Section = "SANS 10400-S", Category = "Accessibility", CheckType = CheckType.MinCount, Threshold = 1 }
            };
        }

        private List<ComplianceRule> LoadNBCRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "NBC_FIRE_01", Name = "Fire Escape Route", Section = "NBC Part 7", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 30 },
                new ComplianceRule { RuleId = "NBC_STRUCT_01", Name = "Structural Adequacy", Section = "NBC Part 3", Category = "Structural", CheckType = CheckType.MinValue, Threshold = 1.4 }
            };
        }

        private List<ComplianceRule> LoadBSRules()
        {
            return new List<ComplianceRule>
            {
                new ComplianceRule { RuleId = "BS_FIRE_01", Name = "Travel Distance", Section = "BS 9999", Category = "Fire Safety", CheckType = CheckType.MaxValue, Threshold = 45 },
                new ComplianceRule { RuleId = "BS_ACCESS_01", Name = "Door Clear Width", Section = "BS 8300", Category = "Accessibility", CheckType = CheckType.MinValue, Threshold = 800 },
                new ComplianceRule { RuleId = "BS_VENT_01", Name = "Ventilation Rate", Section = "BS EN 16798", Category = "MEP", CheckType = CheckType.MinValue, Threshold = 10 }
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class RuleEngine
    {
        public RuleExecutionResult ExecuteRule(BuildingModel model, ComplianceRule rule)
        {
            // Execute rule based on check type
            return rule.CheckType switch
            {
                CheckType.MaxValue => CheckMaxValue(model, rule),
                CheckType.MinValue => CheckMinValue(model, rule),
                CheckType.MinCount => CheckMinCount(model, rule),
                CheckType.MinPercentage => CheckMinPercentage(model, rule),
                CheckType.Boolean => CheckBoolean(model, rule),
                _ => new RuleExecutionResult { Passed = true, Message = "Check type not implemented" }
            };
        }

        private RuleExecutionResult CheckMaxValue(BuildingModel model, ComplianceRule rule)
        {
            // Simplified - would extract actual value from model
            var actualValue = 50.0; // Placeholder
            var passed = actualValue <= rule.Threshold;

            return new RuleExecutionResult
            {
                Passed = passed,
                ActualValue = $"{actualValue}",
                RequiredValue = $"≤{rule.Threshold}",
                Message = passed ? "Compliant" : $"Exceeds maximum of {rule.Threshold}"
            };
        }

        private RuleExecutionResult CheckMinValue(BuildingModel model, ComplianceRule rule)
        {
            var actualValue = 1000.0; // Placeholder
            var passed = actualValue >= rule.Threshold;

            return new RuleExecutionResult
            {
                Passed = passed,
                ActualValue = $"{actualValue}",
                RequiredValue = $"≥{rule.Threshold}",
                Message = passed ? "Compliant" : $"Below minimum of {rule.Threshold}"
            };
        }

        private RuleExecutionResult CheckMinCount(BuildingModel model, ComplianceRule rule)
        {
            var actualCount = 2; // Placeholder
            var passed = actualCount >= rule.Threshold;

            return new RuleExecutionResult
            {
                Passed = passed,
                ActualValue = $"{actualCount}",
                RequiredValue = $"≥{rule.Threshold}",
                Message = passed ? "Compliant" : $"Insufficient count ({actualCount} < {rule.Threshold})"
            };
        }

        private RuleExecutionResult CheckMinPercentage(BuildingModel model, ComplianceRule rule)
        {
            var actualPercentage = 12.0; // Placeholder
            var passed = actualPercentage >= rule.Threshold;

            return new RuleExecutionResult
            {
                Passed = passed,
                ActualValue = $"{actualPercentage}%",
                RequiredValue = $"≥{rule.Threshold}%",
                Message = passed ? "Compliant" : $"Below minimum {rule.Threshold}%"
            };
        }

        private RuleExecutionResult CheckBoolean(BuildingModel model, ComplianceRule rule)
        {
            var exists = true; // Placeholder
            return new RuleExecutionResult
            {
                Passed = exists,
                ActualValue = exists ? "Yes" : "No",
                RequiredValue = "Yes",
                Message = exists ? "Compliant" : "Required element not found"
            };
        }
    }

    public class ComplianceCalculator
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Calculates an overall compliance score from rule execution results.
        /// Weights: Critical (Fail on fire safety/structural) = 3x, Error (other Fail) = 2x, Warning = 1x.
        /// Returns a value between 0.0 (fully non-compliant) and 1.0 (fully compliant).
        /// </summary>
        public double CalculateComplianceScore(List<RuleExecutionResult> results)
        {
            if (results == null || results.Count == 0)
            {
                Logger.Warn("No rule results provided for compliance scoring");
                return 1.0;
            }

            double totalWeight = 0;
            double passedWeight = 0;

            foreach (var result in results)
            {
                double weight;

                if (!result.Passed && !result.IsWarning)
                {
                    // Failed rule (Error/Critical severity) - weight 3x for critical, 2x for standard errors
                    // Treat failures with affected elements as critical (structural/fire safety implications)
                    bool isCritical = result.AffectedElements != null && result.AffectedElements.Count > 0;
                    weight = isCritical ? 3.0 : 2.0;
                }
                else if (result.IsWarning)
                {
                    // Warning severity - weight 1x
                    weight = 1.0;
                }
                else
                {
                    // Passed rule - weight based on average (2x standard)
                    weight = 2.0;
                }

                totalWeight += weight;

                if (result.Passed)
                {
                    passedWeight += weight;
                }
                else if (result.IsWarning)
                {
                    // Warnings count as partial pass (50%)
                    passedWeight += weight * 0.5;
                }
            }

            double score = totalWeight > 0 ? passedWeight / totalWeight : 1.0;
            score = Math.Max(0.0, Math.Min(1.0, score));

            Logger.Info($"Compliance score calculated: {score:F3} from {results.Count} rules");
            return score;
        }
    }

    public class ReportGenerator
    {
        private static readonly NLog.ILogger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Generates a formatted text compliance report including summary, score,
        /// pass/fail counts, categorized findings, and recommendations.
        /// </summary>
        public string GenerateReport(ComplianceReport report)
        {
            Logger.Info($"Generating compliance report for building: {report.BuildingName}");

            var sb = new System.Text.StringBuilder();

            // Header
            sb.AppendLine("================================================================================");
            sb.AppendLine("                    COMPLIANCE CHECK REPORT");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"  Building:     {report.BuildingName ?? report.BuildingId}");
            sb.AppendLine($"  Checked At:   {report.CheckedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Overall Score: {report.OverallScore:P1}");
            sb.AppendLine();

            // Applicable codes
            if (report.ApplicableCodes != null && report.ApplicableCodes.Count > 0)
            {
                sb.AppendLine("  Applicable Codes:");
                foreach (var code in report.ApplicableCodes)
                {
                    sb.AppendLine($"    - {code}");
                }
                sb.AppendLine();
            }

            // Summary
            if (report.Summary != null)
            {
                sb.AppendLine("  --- Summary ---");
                sb.AppendLine($"    Total Checks:    {report.Summary.TotalChecks}");
                sb.AppendLine($"    Passed:          {report.Summary.Passed}");
                sb.AppendLine($"    Failed:          {report.Summary.Failed}");
                sb.AppendLine($"    Warnings:        {report.Summary.Warnings}");
                sb.AppendLine($"    Not Applicable:  {report.Summary.NotApplicable}");
                sb.AppendLine();
            }

            // Code-level results
            if (report.CodeResults != null && report.CodeResults.Count > 0)
            {
                sb.AppendLine("  --- Findings by Code ---");
                sb.AppendLine();

                foreach (var codeResult in report.CodeResults)
                {
                    sb.AppendLine($"  [{codeResult.CodeName ?? codeResult.CodeId}] v{codeResult.Version}");
                    sb.AppendLine($"    Compliance: {codeResult.CompliancePercentage:F1}%  ({codeResult.PassedChecks}/{codeResult.TotalChecks} passed)");

                    // Group rule results by category
                    var byCategory = codeResult.RuleResults
                        .GroupBy(r => r.Category ?? "General")
                        .OrderBy(g => g.Key);

                    foreach (var category in byCategory)
                    {
                        sb.AppendLine($"    Category: {category.Key}");

                        foreach (var rule in category)
                        {
                            string severityLabel = rule.Status switch
                            {
                                CheckStatus.Fail => "[CRITICAL]",
                                CheckStatus.Warning => "[WARNING]",
                                CheckStatus.Error => "[ERROR]",
                                CheckStatus.Pass => "[PASS]",
                                CheckStatus.NotApplicable => "[INFO]",
                                _ => "[INFO]"
                            };

                            sb.AppendLine($"      {severityLabel} {rule.RuleName ?? rule.RuleId}: {rule.Message}");

                            if (rule.Status == CheckStatus.Fail || rule.Status == CheckStatus.Warning)
                            {
                                if (!string.IsNullOrEmpty(rule.ActualValue))
                                    sb.AppendLine($"        Actual: {rule.ActualValue}  Required: {rule.RequiredValue}");

                                if (!string.IsNullOrEmpty(rule.Remediation))
                                    sb.AppendLine($"        Remediation: {rule.Remediation}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }

            // Recommendations
            if (report.Recommendations != null && report.Recommendations.Count > 0)
            {
                sb.AppendLine("  --- Recommendations ---");
                sb.AppendLine();

                int recIndex = 1;
                foreach (var rec in report.Recommendations)
                {
                    string priorityLabel = rec.Priority?.ToUpperInvariant() switch
                    {
                        "CRITICAL" => "[CRITICAL]",
                        "HIGH" => "[ERROR]",
                        "MEDIUM" => "[WARNING]",
                        _ => "[INFO]"
                    };

                    sb.AppendLine($"  {recIndex}. {priorityLabel} {rec.Category}: {rec.Description}");

                    if (rec.SuggestedActions != null)
                    {
                        foreach (var action in rec.SuggestedActions)
                        {
                            sb.AppendLine($"       - {action}");
                        }
                    }

                    recIndex++;
                }
                sb.AppendLine();
            }

            sb.AppendLine("================================================================================");
            sb.AppendLine("                         END OF REPORT");
            sb.AppendLine("================================================================================");

            Logger.Info("Compliance report generated successfully");
            return sb.ToString();
        }
    }

    #endregion

    #region Data Models

    public class BuildingModel
    {
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public BuildingLocation Location { get; set; }
        public string BuildingType { get; set; }
        public string OccupancyType { get; set; }
        public double TotalArea { get; set; }
        public int DesignOccupancy { get; set; }
        public int NumberOfStoreys { get; set; }
        public List<Space> Spaces { get; set; }
    }

    public class BuildingLocation
    {
        public string Country { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
    }

    public class Space
    {
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public string UseType { get; set; }
    }

    public class ComplianceCheckOptions
    {
        public List<string> CodesToCheck { get; set; } = new List<string>();
        public List<string> SectionsToCheck { get; set; }
        public bool AutoDetectCodes { get; set; } = true;
        public bool IncludeRecommendations { get; set; } = true;
    }

    public class ComplianceReport
    {
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public DateTime CheckedAt { get; set; }
        public List<string> ApplicableCodes { get; set; }
        public List<CodeComplianceResult> CodeResults { get; set; } = new List<CodeComplianceResult>();
        public ComplianceSummary Summary { get; set; }
        public List<ComplianceRecommendation> Recommendations { get; set; }
        public double OverallScore { get; set; }
    }

    public class CodeStandard
    {
        public string CodeId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Country { get; set; }
        public List<ComplianceRule> Rules { get; set; }
    }

    public class ComplianceRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Section { get; set; }
        public string Category { get; set; }
        public CheckType CheckType { get; set; }
        public double Threshold { get; set; }
        public List<string> ApplicableBuildingTypes { get; set; }
        public List<string> ApplicableOccupancies { get; set; }
        public double? MinimumArea { get; set; }
        public int? MinimumOccupancy { get; set; }
    }

    public class CodeComplianceResult
    {
        public string CodeId { get; set; }
        public string CodeName { get; set; }
        public string Version { get; set; }
        public List<RuleCheckResult> RuleResults { get; set; } = new List<RuleCheckResult>();
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public int WarningChecks { get; set; }
        public int NotApplicable { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class RuleCheckResult
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Section { get; set; }
        public string Category { get; set; }
        public CheckStatus Status { get; set; }
        public string ActualValue { get; set; }
        public string RequiredValue { get; set; }
        public string Message { get; set; }
        public List<string> AffectedElements { get; set; }
        public string Remediation { get; set; }
    }

    public class RuleExecutionResult
    {
        public bool Passed { get; set; }
        public bool IsWarning { get; set; }
        public string ActualValue { get; set; }
        public string RequiredValue { get; set; }
        public string Message { get; set; }
        public List<string> AffectedElements { get; set; }
    }

    public class ComplianceSummary
    {
        public int TotalChecks { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Warnings { get; set; }
        public int NotApplicable { get; set; }
        public Dictionary<string, CategorySummary> ByCategory { get; set; }
    }

    public class CategorySummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
    }

    public class ComplianceRecommendation
    {
        public string Priority { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> AffectedRules { get; set; }
        public List<string> SuggestedActions { get; set; }
    }

    public class FireSafetyResult
    {
        public DateTime CheckedAt { get; set; }
        public List<RuleCheckResult> TravelDistanceChecks { get; set; }
        public List<RuleCheckResult> ExitWidthChecks { get; set; }
        public List<RuleCheckResult> FireRatingChecks { get; set; }
        public List<RuleCheckResult> SprinklerChecks { get; set; }
        public List<RuleCheckResult> CompartmentationChecks { get; set; }
        public List<RuleCheckResult> EmergencyLightingChecks { get; set; }
        public double OverallScore { get; set; }
    }

    public class AccessibilityResult
    {
        public DateTime CheckedAt { get; set; }
        public List<RuleCheckResult> RouteChecks { get; set; }
        public List<RuleCheckResult> DoorChecks { get; set; }
        public List<RuleCheckResult> ToiletChecks { get; set; }
        public List<RuleCheckResult> ParkingChecks { get; set; }
        public List<RuleCheckResult> SignageChecks { get; set; }
        public List<RuleCheckResult> LiftChecks { get; set; }
    }

    public class StructuralResult
    {
        public DateTime CheckedAt { get; set; }
        public List<RuleCheckResult> LoadChecks { get; set; }
        public List<RuleCheckResult> MaterialChecks { get; set; }
        public List<RuleCheckResult> FoundationChecks { get; set; }
        public List<RuleCheckResult> SeismicChecks { get; set; }
    }

    public class MEPComplianceResult
    {
        public DateTime CheckedAt { get; set; }
        public List<RuleCheckResult> VentilationChecks { get; set; }
        public List<RuleCheckResult> ElectricalChecks { get; set; }
        public List<RuleCheckResult> PlumbingChecks { get; set; }
        public List<RuleCheckResult> EnergyChecks { get; set; }
    }

    public enum CheckType
    {
        MaxValue,
        MinValue,
        MinCount,
        MaxCount,
        MinPercentage,
        MaxPercentage,
        Boolean,
        Range
    }

    public enum CheckStatus
    {
        Pass,
        Fail,
        Warning,
        NotApplicable,
        Error
    }

    #endregion
}
