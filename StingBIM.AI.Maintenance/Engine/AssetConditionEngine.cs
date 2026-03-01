// ============================================================================
// StingBIM AI - Asset Condition Engine
// Comprehensive asset condition assessment, inspection management, defect
// lifecycle tracking, Facility Condition Index (FCI) calculation, capital
// renewal prioritization, condition reporting, and future condition projection.
// Aligned with ASTM E2018 (Property Condition Assessment) and ISO 55000.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Maintenance.Models;

namespace StingBIM.AI.Maintenance.Engine
{
    /// <summary>
    /// Asset condition assessment engine providing multi-factor condition evaluation,
    /// inspection recording, defect tracking, Facility Condition Index computation,
    /// capital renewal prioritization, condition reporting, and Markov chain-based
    /// future condition projection.
    /// </summary>
    public class AssetConditionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        #region Internal State

        // Asset conditions: assetId -> condition
        private readonly Dictionary<string, AssetCondition> _assetConditions =
            new(StringComparer.OrdinalIgnoreCase);

        // Inspection history: assetId -> list of inspections
        private readonly Dictionary<string, List<InspectionResult>> _inspections =
            new(StringComparer.OrdinalIgnoreCase);

        // Defect records: defectId -> defect
        private readonly Dictionary<string, DefectRecord> _defects =
            new(StringComparer.OrdinalIgnoreCase);

        // Defect-to-asset mapping: assetId -> list of defect IDs
        private readonly Dictionary<string, List<string>> _assetDefects =
            new(StringComparer.OrdinalIgnoreCase);

        // Building asset grouping: buildingId -> list of asset IDs
        private readonly Dictionary<string, List<string>> _buildingAssets =
            new(StringComparer.OrdinalIgnoreCase);

        // Markov transition matrix for condition grade deterioration (6x6 for grades A-F).
        // Row i = current grade, Column j = probability of transitioning to grade j in one year.
        // Default matrix based on industry data for building systems.
        private readonly double[,] _markovTransitionMatrix = new double[6, 6]
        {
            //       A      B      C      D      E      F
            /*A*/ { 0.80,  0.15,  0.04,  0.01,  0.00,  0.00 },
            /*B*/ { 0.02,  0.75,  0.18,  0.04,  0.01,  0.00 },
            /*C*/ { 0.00,  0.03,  0.70,  0.20,  0.06,  0.01 },
            /*D*/ { 0.00,  0.00,  0.02,  0.65,  0.25,  0.08 },
            /*E*/ { 0.00,  0.00,  0.00,  0.03,  0.60,  0.37 },
            /*F*/ { 0.00,  0.00,  0.00,  0.00,  0.05,  0.95 }
        };

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the asset condition engine.
        /// </summary>
        public AssetConditionEngine()
        {
            Logger.Info("AssetConditionEngine initialized.");
        }

        #endregion

        #region Asset Registration

        /// <summary>
        /// Registers an asset and associates it with a building for FCI calculations.
        /// </summary>
        /// <param name="condition">Asset condition data.</param>
        /// <param name="buildingId">Building to which the asset belongs.</param>
        public void RegisterAsset(AssetCondition condition, string buildingId = "DEFAULT")
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            lock (_lockObject)
            {
                _assetConditions[condition.AssetId] = condition;

                if (!_buildingAssets.TryGetValue(buildingId, out var assets))
                {
                    assets = new List<string>();
                    _buildingAssets[buildingId] = assets;
                }

                if (!assets.Contains(condition.AssetId, StringComparer.OrdinalIgnoreCase))
                {
                    assets.Add(condition.AssetId);
                }
            }

            Logger.Debug("Registered asset '{AssetId}' in building '{BuildingId}', " +
                         "health={Health:F1}, grade={Grade}",
                condition.AssetId, buildingId, condition.HealthScore, condition.ConditionGrade);
        }

        #endregion

        #region Condition Assessment

        /// <summary>
        /// Performs a comprehensive multi-factor condition assessment of an asset,
        /// incorporating inspection history, defect severity, age, maintenance
        /// records, and sensor data to produce an updated health score and grade.
        /// </summary>
        /// <param name="assetId">The asset to assess.</param>
        /// <returns>Updated asset condition with computed health score and grade.</returns>
        public AssetCondition AssessCondition(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Info("Assessing condition for asset '{AssetId}'...", assetId);

            AssetCondition condition;
            lock (_lockObject)
            {
                if (!_assetConditions.TryGetValue(assetId, out condition))
                {
                    condition = new AssetCondition
                    {
                        AssetId = assetId,
                        AssetName = assetId,
                        HealthScore = 100.0,
                        ConditionGrade = ConditionGrade.A,
                        LastAssessment = DateTime.UtcNow
                    };
                    _assetConditions[assetId] = condition;
                }
            }

            // Factor 1: Inspection-based score (0-100)
            double inspectionScore = CalculateInspectionScore(assetId);

            // Factor 2: Defect severity score (0-100)
            double defectScore = CalculateDefectScore(assetId);

            // Factor 3: Age-based deterioration score (0-100)
            double ageScore = CalculateAgeScore(condition);

            // Factor 4: Remaining service life ratio score (0-100)
            double serviceLifeScore = CalculateServiceLifeScore(condition);

            // Factor 5: Deferred maintenance burden score (0-100)
            double deferredMaintenanceScore = CalculateDeferredMaintenanceScore(condition);

            // Weighted composite
            const double wInspection = 0.30;
            const double wDefect = 0.25;
            const double wAge = 0.20;
            const double wServiceLife = 0.15;
            const double wDeferred = 0.10;

            double healthScore = (inspectionScore * wInspection) +
                                 (defectScore * wDefect) +
                                 (ageScore * wAge) +
                                 (serviceLifeScore * wServiceLife) +
                                 (deferredMaintenanceScore * wDeferred);

            healthScore = Math.Clamp(healthScore, 0.0, 100.0);

            // Determine condition grade from health score
            ConditionGrade grade = MapHealthToGrade(healthScore);

            // Calculate degradation rate from inspection history
            double degradationRate = CalculateDegradationRate(assetId, condition);

            // Estimate remaining useful life
            double remainingMonths = 0;
            if (degradationRate > 0)
            {
                double remainingHealth = healthScore - 20.0; // Threshold for grade F
                remainingMonths = Math.Max(0, remainingHealth / degradationRate);
            }
            else if (condition.ExpectedServiceLifeYears > 0)
            {
                double remainingYears = condition.ExpectedServiceLifeYears - condition.AgeYears;
                remainingMonths = Math.Max(0, remainingYears * 12.0);
            }

            // Update condition record
            lock (_lockObject)
            {
                condition.HealthScore = healthScore;
                condition.ConditionGrade = grade;
                condition.LastAssessment = DateTime.UtcNow;
                condition.DegradationRate = degradationRate;
                condition.RemainingUsefulLifeMonths = remainingMonths;
                condition.FactorScores["Inspection"] = inspectionScore;
                condition.FactorScores["Defect"] = defectScore;
                condition.FactorScores["Age"] = ageScore;
                condition.FactorScores["ServiceLife"] = serviceLifeScore;
                condition.FactorScores["DeferredMaintenance"] = deferredMaintenanceScore;
            }

            Logger.Info("Condition for '{AssetId}': health={Health:F1}, grade={Grade}, " +
                        "degradation={Rate:F2}/month, RUL={RUL:F0} months",
                assetId, healthScore, grade, degradationRate, remainingMonths);

            return condition;
        }

        #endregion

        #region Inspection Management

        /// <summary>
        /// Records an inspection result for an asset, updating the condition
        /// assessment and registering any new defects found.
        /// </summary>
        /// <param name="inspection">The inspection result to record.</param>
        public void RecordInspection(InspectionResult inspection)
        {
            if (inspection == null) throw new ArgumentNullException(nameof(inspection));
            if (string.IsNullOrEmpty(inspection.AssetId))
                throw new ArgumentException("Asset ID is required in inspection result.");

            Logger.Info("Recording inspection '{InspectionId}' for asset '{AssetId}', " +
                        "grade={Grade}, defects={DefectCount}",
                inspection.InspectionId, inspection.AssetId,
                inspection.ConditionGrade, inspection.Defects?.Count ?? 0);

            // Generate inspection ID if not provided
            if (string.IsNullOrEmpty(inspection.InspectionId))
            {
                inspection.InspectionId = $"INSP-{Guid.NewGuid():N}".Substring(0, 20);
            }

            lock (_lockObject)
            {
                // Store inspection
                if (!_inspections.TryGetValue(inspection.AssetId, out var history))
                {
                    history = new List<InspectionResult>();
                    _inspections[inspection.AssetId] = history;
                }
                history.Add(inspection);

                // Register defects
                if (inspection.Defects != null)
                {
                    foreach (var defect in inspection.Defects)
                    {
                        if (string.IsNullOrEmpty(defect.DefectId))
                        {
                            defect.DefectId = $"DEF-{Guid.NewGuid():N}".Substring(0, 20);
                        }

                        _defects[defect.DefectId] = defect;

                        if (!_assetDefects.TryGetValue(inspection.AssetId, out var defectIds))
                        {
                            defectIds = new List<string>();
                            _assetDefects[inspection.AssetId] = defectIds;
                        }
                        defectIds.Add(defect.DefectId);
                    }
                }

                // Update asset condition grade from latest inspection
                if (_assetConditions.TryGetValue(inspection.AssetId, out var condition))
                {
                    condition.ConditionGrade = inspection.ConditionGrade;
                    condition.LastAssessment = inspection.InspectionDate;
                }
            }

            Logger.Info("Inspection recorded: {InspectionId}, {DefectCount} defects registered.",
                inspection.InspectionId, inspection.Defects?.Count ?? 0);
        }

        /// <summary>
        /// Retrieves the inspection history for an asset, ordered most recent first.
        /// </summary>
        public List<InspectionResult> GetInspectionHistory(string assetId)
        {
            lock (_lockObject)
            {
                if (_inspections.TryGetValue(assetId, out var history))
                {
                    return history.OrderByDescending(i => i.InspectionDate).ToList();
                }
            }
            return new List<InspectionResult>();
        }

        #endregion

        #region Defect Tracking

        /// <summary>
        /// Retrieves all defects for an asset with lifecycle tracking information,
        /// sorted by severity (highest first) then by status.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>List of defect records for the asset.</returns>
        public List<DefectRecord> TrackDefects(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            Logger.Debug("Tracking defects for asset '{AssetId}'...", assetId);

            lock (_lockObject)
            {
                if (!_assetDefects.TryGetValue(assetId, out var defectIds))
                    return new List<DefectRecord>();

                var defects = defectIds
                    .Where(id => _defects.ContainsKey(id))
                    .Select(id => _defects[id])
                    .OrderByDescending(d => d.Severity)
                    .ThenBy(d => d.Status)
                    .ToList();

                Logger.Debug("Asset '{AssetId}' has {Count} tracked defects ({Open} open).",
                    assetId, defects.Count, defects.Count(d => d.Status == "Open"));

                return defects;
            }
        }

        /// <summary>
        /// Updates the status of a defect (Open, InReview, Scheduled, Remediated, Accepted).
        /// </summary>
        public bool UpdateDefectStatus(string defectId, string newStatus, string notes = "")
        {
            if (string.IsNullOrEmpty(defectId))
                throw new ArgumentException("Defect ID is required.", nameof(defectId));

            var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Open", "InReview", "Scheduled", "Remediated", "Accepted" };

            if (!validStatuses.Contains(newStatus))
            {
                Logger.Warn("Invalid defect status '{Status}'. Valid: {Valid}",
                    newStatus, string.Join(", ", validStatuses));
                return false;
            }

            lock (_lockObject)
            {
                if (!_defects.TryGetValue(defectId, out var defect))
                {
                    Logger.Warn("Defect '{DefectId}' not found.", defectId);
                    return false;
                }

                string previousStatus = defect.Status;
                defect.Status = newStatus;

                Logger.Info("Defect '{DefectId}' status: {From} -> {To}. {Notes}",
                    defectId, previousStatus, newStatus, notes);
            }

            return true;
        }

        #endregion

        #region Facility Condition Index

        /// <summary>
        /// Calculates the Facility Condition Index (FCI) for a building.
        /// FCI = Total Deferred Maintenance / Total Current Replacement Value
        /// Lower FCI indicates better condition. Industry benchmarks:
        ///   Good: FCI less than 0.05 (5%)
        ///   Fair: FCI 0.05-0.10 (5-10%)
        ///   Poor: FCI greater than 0.10 (above 10%)
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <returns>
        /// Tuple of (FCI value, total deferred maintenance cost, total replacement value, rating).
        /// </returns>
        public (double FCI, decimal DeferredMaintenance, decimal ReplacementValue, string Rating)
            CalculateFacilityConditionIndex(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
                throw new ArgumentException("Building ID is required.", nameof(buildingId));

            Logger.Info("Calculating FCI for building '{BuildingId}'...", buildingId);

            decimal totalDeferred = 0m;
            decimal totalReplacement = 0m;

            lock (_lockObject)
            {
                if (!_buildingAssets.TryGetValue(buildingId, out var assetIds))
                {
                    Logger.Warn("No assets registered for building '{BuildingId}'.", buildingId);
                    return (0, 0, 0, "Unknown");
                }

                foreach (var assetId in assetIds)
                {
                    if (_assetConditions.TryGetValue(assetId, out var condition))
                    {
                        totalDeferred += condition.DeferredMaintenanceCost;
                        totalReplacement += condition.CurrentReplacementValue;
                    }
                }
            }

            double fci = totalReplacement > 0
                ? (double)(totalDeferred / totalReplacement)
                : 0;

            string rating = fci switch
            {
                <= 0.05 => "Good",
                <= 0.10 => "Fair",
                <= 0.20 => "Poor",
                <= 0.50 => "Critical",
                _ => "Failing"
            };

            Logger.Info("FCI for building '{BuildingId}': {FCI:P2} ({Rating}), " +
                        "deferred={Deferred:C}, replacement value={RV:C}",
                buildingId, fci, rating, totalDeferred, totalReplacement);

            return (fci, totalDeferred, totalReplacement, rating);
        }

        #endregion

        #region Capital Renewal Prioritization

        /// <summary>
        /// Prioritizes capital renewal spending within a budget constraint using a
        /// multi-criteria scoring model: condition severity, risk, cost-effectiveness,
        /// and remaining service life. Returns an ordered list of assets to renew.
        /// </summary>
        /// <param name="buildingId">Building to prioritize.</param>
        /// <param name="budget">Available budget for renewal in USD.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered list of renewal recommendations fitting within the budget.</returns>
        public async Task<List<(string AssetId, string AssetName, ConditionGrade Grade,
            decimal RenewalCost, double PriorityScore, string Recommendation)>>
            PrioritizeCapitalRenewal(
                string buildingId,
                decimal budget,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(buildingId))
                throw new ArgumentException("Building ID is required.", nameof(buildingId));

            Logger.Info("Prioritizing capital renewal for building '{BuildingId}', budget={Budget:C}",
                buildingId, budget);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidates = new List<(string AssetId, string AssetName, ConditionGrade Grade,
                    decimal RenewalCost, double PriorityScore, string Recommendation)>();

                lock (_lockObject)
                {
                    if (!_buildingAssets.TryGetValue(buildingId, out var assetIds))
                        return candidates;

                    foreach (var assetId in assetIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!_assetConditions.TryGetValue(assetId, out var condition))
                            continue;

                        // Only consider assets in grades C, D, E, or F for renewal
                        if (condition.ConditionGrade < ConditionGrade.C)
                            continue;

                        // Criterion 1: Condition severity (0-40 points)
                        double conditionScore = condition.ConditionGrade switch
                        {
                            ConditionGrade.F => 40,
                            ConditionGrade.E => 32,
                            ConditionGrade.D => 24,
                            ConditionGrade.C => 16,
                            _ => 0
                        };

                        // Criterion 2: Risk impact (0-25 points)
                        double riskScore = 0;
                        string category = condition.AssetCategory?.ToUpperInvariant() ?? "";
                        riskScore = category switch
                        {
                            "FIRE PROTECTION" => 25,
                            "ELECTRICAL" => 22,
                            "STRUCTURAL" => 25,
                            "ELEVATOR" => 22,
                            "HVAC" => 18,
                            "PLUMBING" => 15,
                            "ROOFING" => 12,
                            _ => 10
                        };

                        // Criterion 3: Cost-effectiveness (0-20 points)
                        // Higher score for lower cost relative to replacement value
                        double costEffectiveness = 0;
                        if (condition.CurrentReplacementValue > 0)
                        {
                            double costRatio = (double)(condition.DeferredMaintenanceCost
                                / condition.CurrentReplacementValue);
                            // Better score when deferred maintenance is low relative to replacement
                            costEffectiveness = Math.Max(0, 20.0 * (1.0 - costRatio));
                        }

                        // Criterion 4: Remaining service life (0-15 points)
                        double serviceLifeScore = 0;
                        if (condition.RemainingUsefulLifeMonths < 12)
                            serviceLifeScore = 15;
                        else if (condition.RemainingUsefulLifeMonths < 36)
                            serviceLifeScore = 10;
                        else if (condition.RemainingUsefulLifeMonths < 60)
                            serviceLifeScore = 5;

                        double totalScore = conditionScore + riskScore + costEffectiveness + serviceLifeScore;

                        // Determine renewal cost and recommendation
                        decimal renewalCost = condition.DeferredMaintenanceCost;
                        if (renewalCost <= 0)
                        {
                            renewalCost = condition.CurrentReplacementValue * 0.15m;
                        }

                        string recommendation;
                        if (condition.ConditionGrade >= ConditionGrade.E)
                        {
                            recommendation = "Replace: Asset at or near end of useful life.";
                            renewalCost = condition.CurrentReplacementValue;
                        }
                        else if (condition.ConditionGrade == ConditionGrade.D)
                        {
                            recommendation = "Major renovation: Significant restoration required.";
                            renewalCost = condition.CurrentReplacementValue * 0.40m;
                        }
                        else
                        {
                            recommendation = "Repair/refurbish: Targeted maintenance to restore condition.";
                        }

                        candidates.Add((
                            assetId,
                            condition.AssetName,
                            condition.ConditionGrade,
                            renewalCost,
                            totalScore,
                            recommendation
                        ));
                    }
                }

                // Sort by priority score descending
                candidates.Sort((a, b) => b.PriorityScore.CompareTo(a.PriorityScore));

                // Select within budget using a greedy knapsack approach
                var selected = new List<(string, string, ConditionGrade, decimal, double, string)>();
                decimal remainingBudget = budget;

                foreach (var candidate in candidates)
                {
                    if (candidate.RenewalCost <= remainingBudget)
                    {
                        selected.Add(candidate);
                        remainingBudget -= candidate.RenewalCost;
                    }
                }

                Logger.Info("Capital renewal prioritization: {Selected}/{Total} assets selected, " +
                            "total cost={Cost:C}, remaining budget={Remaining:C}",
                    selected.Count, candidates.Count,
                    budget - remainingBudget, remainingBudget);

                return selected;
            }, cancellationToken);
        }

        #endregion

        #region Condition Reporting

        /// <summary>
        /// Generates a comprehensive building condition survey report including
        /// overall FCI, asset-by-asset condition summary, critical defects,
        /// and recommended actions.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <returns>Dictionary of report sections with their content.</returns>
        public Dictionary<string, object> GenerateConditionReport(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
                throw new ArgumentException("Building ID is required.", nameof(buildingId));

            Logger.Info("Generating condition report for building '{BuildingId}'...", buildingId);

            var report = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Section 1: Executive Summary
            var (fci, deferredMaint, replacementValue, fciRating) =
                CalculateFacilityConditionIndex(buildingId);

            report["ExecutiveSummary"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["BuildingId"] = buildingId,
                ["ReportDate"] = DateTime.UtcNow,
                ["FCI"] = fci,
                ["FCIRating"] = fciRating,
                ["TotalDeferredMaintenance"] = deferredMaint,
                ["TotalReplacementValue"] = replacementValue
            };

            // Section 2: Asset Condition Summary
            var assetSummaries = new List<Dictionary<string, object>>();

            lock (_lockObject)
            {
                if (_buildingAssets.TryGetValue(buildingId, out var assetIds))
                {
                    foreach (var assetId in assetIds)
                    {
                        if (_assetConditions.TryGetValue(assetId, out var condition))
                        {
                            var summary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["AssetId"] = condition.AssetId,
                                ["AssetName"] = condition.AssetName,
                                ["Category"] = condition.AssetCategory,
                                ["HealthScore"] = condition.HealthScore,
                                ["ConditionGrade"] = condition.ConditionGrade.ToString(),
                                ["AgeYears"] = condition.AgeYears,
                                ["ExpectedLifeYears"] = condition.ExpectedServiceLifeYears,
                                ["RemainingLifeMonths"] = condition.RemainingUsefulLifeMonths,
                                ["DeferredMaintenanceCost"] = condition.DeferredMaintenanceCost,
                                ["ReplacementValue"] = condition.CurrentReplacementValue,
                                ["LastAssessment"] = condition.LastAssessment
                            };
                            assetSummaries.Add(summary);
                        }
                    }
                }
            }

            report["AssetConditions"] = assetSummaries
                .OrderBy(a => a["HealthScore"])
                .ToList();

            // Section 3: Condition Grade Distribution
            var gradeDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ConditionGrade grade in Enum.GetValues<ConditionGrade>())
            {
                gradeDistribution[grade.ToString()] = assetSummaries
                    .Count(a => a["ConditionGrade"].ToString() == grade.ToString());
            }
            report["GradeDistribution"] = gradeDistribution;

            // Section 4: Critical Defects
            var criticalDefects = new List<Dictionary<string, object>>();
            lock (_lockObject)
            {
                if (_buildingAssets.TryGetValue(buildingId, out var buildingAssetIds))
                {
                    foreach (var assetId in buildingAssetIds)
                    {
                        if (_assetDefects.TryGetValue(assetId, out var defectIds))
                        {
                            foreach (var defectId in defectIds)
                            {
                                if (_defects.TryGetValue(defectId, out var defect) &&
                                    (defect.Severity >= 4 || defect.IsSafetyHazard) &&
                                    defect.Status != "Remediated" &&
                                    defect.Status != "Accepted")
                                {
                                    criticalDefects.Add(new Dictionary<string, object>(
                                        StringComparer.OrdinalIgnoreCase)
                                    {
                                        ["DefectId"] = defect.DefectId,
                                        ["AssetId"] = assetId,
                                        ["Description"] = defect.Description,
                                        ["Severity"] = defect.Severity,
                                        ["IsSafetyHazard"] = defect.IsSafetyHazard,
                                        ["Location"] = defect.Location,
                                        ["RecommendedAction"] = defect.RecommendedAction,
                                        ["EstimatedCost"] = defect.EstimatedRemediationCost
                                    });
                                }
                            }
                        }
                    }
                }
            }

            report["CriticalDefects"] = criticalDefects
                .OrderByDescending(d => (int)d["Severity"])
                .ToList();

            // Section 5: Recommended Actions (ranked by urgency)
            var actions = new List<string>();
            if (fci > 0.20)
            {
                actions.Add("URGENT: FCI exceeds 20%. Develop comprehensive capital renewal plan.");
            }
            if (criticalDefects.Any(d => (bool)d["IsSafetyHazard"]))
            {
                actions.Add("IMMEDIATE: Safety hazard defects identified. Remediate before occupancy.");
            }
            if (gradeDistribution.GetValueOrDefault("F", 0) > 0)
            {
                int failedCount = gradeDistribution.GetValueOrDefault("F", 0);
                actions.Add($"HIGH: {failedCount} asset(s) in Failed condition (Grade F). " +
                            "Schedule replacement or major overhaul.");
            }
            if (gradeDistribution.GetValueOrDefault("E", 0) > 0)
            {
                int severeCount = gradeDistribution.GetValueOrDefault("E", 0);
                actions.Add($"HIGH: {severeCount} asset(s) in Severe condition (Grade E). " +
                            "Plan corrective maintenance within 30 days.");
            }
            actions.Add("ROUTINE: Continue scheduled preventive maintenance for all assets.");
            actions.Add("MONITORING: Update condition assessments per ASTM E2018 schedule.");

            report["RecommendedActions"] = actions;

            Logger.Info("Condition report generated for '{BuildingId}': FCI={FCI:P2}, " +
                        "{AssetCount} assets, {DefectCount} critical defects",
                buildingId, fci, assetSummaries.Count, criticalDefects.Count);

            return report;
        }

        #endregion

        #region Future Condition Projection

        /// <summary>
        /// Projects the future condition of an asset over a specified number of years
        /// using a Markov chain deterioration model. The model predicts the probability
        /// distribution across condition grades at each future year and computes the
        /// expected health score trajectory.
        /// </summary>
        /// <param name="assetId">The asset to project.</param>
        /// <param name="years">Number of years to project (1-20).</param>
        /// <returns>
        /// List of yearly projections containing year, expected health score,
        /// most likely grade, and full probability distribution across grades.
        /// </returns>
        public List<(int Year, double ExpectedHealthScore, ConditionGrade MostLikelyGrade,
            Dictionary<ConditionGrade, double> GradeProbabilities)>
            ProjectConditionDecline(string assetId, int years)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (years < 1 || years > 20)
                throw new ArgumentOutOfRangeException(nameof(years), "Years must be between 1 and 20.");

            Logger.Info("Projecting condition for '{AssetId}' over {Years} years...", assetId, years);

            // Get current condition grade
            ConditionGrade currentGrade;
            lock (_lockObject)
            {
                if (_assetConditions.TryGetValue(assetId, out var condition))
                {
                    currentGrade = condition.ConditionGrade;
                }
                else
                {
                    currentGrade = ConditionGrade.B; // Default assumption
                }
            }

            // Initialize state probability vector (one-hot for current grade)
            double[] stateVector = new double[6];
            stateVector[(int)currentGrade] = 1.0;

            var projections = new List<(int, double, ConditionGrade, Dictionary<ConditionGrade, double>)>();

            // Health score midpoints for each grade
            double[] gradeHealthScores = { 92.5, 77.5, 62.5, 47.5, 30.0, 10.0 };

            for (int year = 1; year <= years; year++)
            {
                // Multiply state vector by transition matrix: s' = s * P
                double[] newStateVector = new double[6];
                for (int j = 0; j < 6; j++)
                {
                    double sum = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        sum += stateVector[i] * _markovTransitionMatrix[i, j];
                    }
                    newStateVector[j] = sum;
                }

                // Normalize to handle floating point drift
                double totalProb = newStateVector.Sum();
                if (totalProb > 0)
                {
                    for (int i = 0; i < 6; i++)
                        newStateVector[i] /= totalProb;
                }

                stateVector = newStateVector;

                // Calculate expected health score
                double expectedHealth = 0;
                for (int i = 0; i < 6; i++)
                {
                    expectedHealth += stateVector[i] * gradeHealthScores[i];
                }

                // Find most likely grade
                int maxIndex = 0;
                for (int i = 1; i < 6; i++)
                {
                    if (stateVector[i] > stateVector[maxIndex])
                        maxIndex = i;
                }
                ConditionGrade mostLikely = (ConditionGrade)maxIndex;

                // Build probability distribution
                var probabilities = new Dictionary<ConditionGrade, double>();
                for (int i = 0; i < 6; i++)
                {
                    probabilities[(ConditionGrade)i] = Math.Round(stateVector[i], 4);
                }

                projections.Add((year, Math.Round(expectedHealth, 1), mostLikely, probabilities));
            }

            Logger.Info("Condition projection for '{AssetId}': " +
                        "Year 1 health={H1:F1}, Year {N} health={HN:F1}",
                assetId,
                projections.First().Item2,
                years,
                projections.Last().Item2);

            return projections;
        }

        #endregion

        #region Capital Planning

        /// <summary>
        /// Generates a multi-year capital plan (5, 10, or 20 year horizons) by
        /// projecting condition decline for all building assets and estimating
        /// the renewal costs at each interval.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="horizonYears">Planning horizon: 5, 10, or 20 years.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <returns>
        /// List of yearly capital budget requirements with asset-level breakdowns.
        /// </returns>
        public async Task<List<(int Year, decimal RequiredBudget,
            List<(string AssetId, string Action, decimal Cost)> Actions)>>
            GenerateCapitalPlan(
                string buildingId,
                int horizonYears = 10,
                CancellationToken cancellationToken = default,
                IProgress<double> progress = null)
        {
            if (string.IsNullOrEmpty(buildingId))
                throw new ArgumentException("Building ID is required.", nameof(buildingId));

            if (horizonYears != 5 && horizonYears != 10 && horizonYears != 20)
                throw new ArgumentException("Horizon must be 5, 10, or 20 years.", nameof(horizonYears));

            Logger.Info("Generating {Horizon}-year capital plan for building '{BuildingId}'...",
                horizonYears, buildingId);

            progress?.Report(0.0);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var capitalPlan = new List<(int, decimal, List<(string, string, decimal)>)>();
                List<string> assetIds;

                lock (_lockObject)
                {
                    if (!_buildingAssets.TryGetValue(buildingId, out assetIds) || assetIds.Count == 0)
                    {
                        Logger.Warn("No assets for building '{BuildingId}'.", buildingId);
                        return capitalPlan;
                    }
                    assetIds = new List<string>(assetIds); // Copy for thread safety
                }

                // Project each asset's condition and determine intervention years
                var assetProjections = new Dictionary<string, List<(int Year, double Health, ConditionGrade Grade)>>(
                    StringComparer.OrdinalIgnoreCase);

                int processed = 0;
                foreach (var assetId in assetIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var projection = ProjectConditionDecline(assetId, horizonYears);
                    assetProjections[assetId] = projection
                        .Select(p => (p.Year, p.ExpectedHealthScore, p.MostLikelyGrade))
                        .ToList();

                    processed++;
                    progress?.Report((double)processed / assetIds.Count * 0.8);
                }

                // Build yearly budget requirements
                for (int year = 1; year <= horizonYears; year++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var yearActions = new List<(string, string, decimal)>();
                    decimal yearBudget = 0;

                    foreach (var assetId in assetIds)
                    {
                        if (!assetProjections.TryGetValue(assetId, out var projection))
                            continue;

                        var yearProjection = projection.FirstOrDefault(p => p.Year == year);
                        if (yearProjection == default) continue;

                        AssetCondition condition;
                        lock (_lockObject)
                        {
                            _assetConditions.TryGetValue(assetId, out condition);
                        }
                        if (condition == null) continue;

                        // Determine if intervention is needed this year
                        string action = null;
                        decimal cost = 0;

                        if (yearProjection.Grade == ConditionGrade.F)
                        {
                            action = "Replace";
                            cost = condition.CurrentReplacementValue;
                        }
                        else if (yearProjection.Grade == ConditionGrade.E && year > 1)
                        {
                            // Check if it just reached grade E this year
                            var prevYear = projection.FirstOrDefault(p => p.Year == year - 1);
                            if (prevYear.Grade < ConditionGrade.E)
                            {
                                action = "Major renovation";
                                cost = condition.CurrentReplacementValue * 0.40m;
                            }
                        }
                        else if (yearProjection.Grade == ConditionGrade.D && year % 3 == 0)
                        {
                            // Periodic significant maintenance for grade D assets
                            action = "Significant maintenance";
                            cost = condition.CurrentReplacementValue * 0.15m;
                        }

                        if (action != null && cost > 0)
                        {
                            yearActions.Add((assetId, action, cost));
                            yearBudget += cost;
                        }
                    }

                    capitalPlan.Add((year, yearBudget, yearActions));
                }

                progress?.Report(1.0);

                decimal totalBudget = capitalPlan.Sum(y => y.Item2);
                Logger.Info("{Horizon}-year capital plan for '{BuildingId}': " +
                            "total={Total:C}, avg/year={Avg:C}",
                    horizonYears, buildingId, totalBudget,
                    horizonYears > 0 ? totalBudget / horizonYears : 0);

                return capitalPlan;
            }, cancellationToken);
        }

        #endregion

        #region Remaining Service Life

        /// <summary>
        /// Estimates the remaining service life of an asset using both deterministic
        /// (age-based) and stochastic (Markov model) approaches, returning the
        /// more conservative estimate.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>
        /// Tuple of (estimated remaining years, method used, confidence level 0-1).
        /// </returns>
        public (double RemainingYears, string Method, double Confidence)
            EstimateRemainingServiceLife(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                throw new ArgumentException("Asset ID is required.", nameof(assetId));

            AssetCondition condition;
            lock (_lockObject)
            {
                if (!_assetConditions.TryGetValue(assetId, out condition))
                {
                    Logger.Warn("No condition data for asset '{AssetId}'.", assetId);
                    return (0, "Unknown", 0);
                }
            }

            // Method 1: Deterministic (age-based)
            double deterministicRemainingYears = Math.Max(0,
                condition.ExpectedServiceLifeYears - condition.AgeYears);

            // Method 2: Markov projection (find year when expected health < 20)
            double markovRemainingYears = deterministicRemainingYears;
            try
            {
                var projection = ProjectConditionDecline(assetId, 20);
                var failureYear = projection.FirstOrDefault(p => p.ExpectedHealthScore < 20.0);
                if (failureYear != default)
                {
                    markovRemainingYears = failureYear.Year;
                }
                else
                {
                    markovRemainingYears = 20; // Beyond projection horizon
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Markov projection failed for '{AssetId}', using deterministic.", assetId);
            }

            // Method 3: Degradation rate-based
            double degradationRemainingYears = double.MaxValue;
            if (condition.DegradationRate > 0)
            {
                double monthsRemaining = Math.Max(0,
                    (condition.HealthScore - 20.0) / condition.DegradationRate);
                degradationRemainingYears = monthsRemaining / 12.0;
            }

            // Take the most conservative estimate
            double bestEstimate = Math.Min(
                deterministicRemainingYears,
                Math.Min(markovRemainingYears, degradationRemainingYears));

            bestEstimate = Math.Max(0, bestEstimate);

            // Determine which method dominated
            string method;
            if (bestEstimate == degradationRemainingYears && degradationRemainingYears < double.MaxValue)
                method = "DegradationRate";
            else if (bestEstimate == markovRemainingYears)
                method = "MarkovProjection";
            else
                method = "AgeBased";

            // Confidence based on data availability
            double confidence = 0.3; // Base
            if (condition.DegradationRate > 0) confidence += 0.2;
            if (condition.LastAssessment > DateTime.UtcNow.AddYears(-1)) confidence += 0.2;
            lock (_lockObject)
            {
                if (_inspections.ContainsKey(assetId)) confidence += 0.15;
            }
            confidence = Math.Min(confidence, 0.95);

            Logger.Info("Remaining service life for '{AssetId}': {Years:F1} years " +
                        "(method={Method}, confidence={Conf:F2})",
                assetId, bestEstimate, method, confidence);

            return (bestEstimate, method, confidence);
        }

        #endregion

        #region Private Scoring Methods

        private double CalculateInspectionScore(string assetId)
        {
            lock (_lockObject)
            {
                if (!_inspections.TryGetValue(assetId, out var inspections) || inspections.Count == 0)
                    return 70.0; // Default when no inspections exist

                // Use the most recent inspection grade
                var latest = inspections.OrderByDescending(i => i.InspectionDate).First();

                double gradeScore = latest.ConditionGrade switch
                {
                    ConditionGrade.A => 95,
                    ConditionGrade.B => 80,
                    ConditionGrade.C => 60,
                    ConditionGrade.D => 40,
                    ConditionGrade.E => 20,
                    ConditionGrade.F => 5,
                    _ => 50
                };

                // Penalty for stale inspections (older than 2 years)
                double staleness = (DateTime.UtcNow - latest.InspectionDate).TotalDays;
                if (staleness > 730)
                {
                    double stalePenalty = Math.Min(20, (staleness - 730) / 365 * 10);
                    gradeScore -= stalePenalty;
                }

                return Math.Clamp(gradeScore, 0, 100);
            }
        }

        private double CalculateDefectScore(string assetId)
        {
            lock (_lockObject)
            {
                if (!_assetDefects.TryGetValue(assetId, out var defectIds) || defectIds.Count == 0)
                    return 90.0; // No defects is excellent

                var openDefects = defectIds
                    .Where(id => _defects.ContainsKey(id))
                    .Select(id => _defects[id])
                    .Where(d => d.Status != "Remediated" && d.Status != "Accepted")
                    .ToList();

                if (openDefects.Count == 0)
                    return 85.0; // All defects resolved

                // Calculate weighted severity score
                double maxSeverity = openDefects.Max(d => d.Severity);
                double avgSeverity = openDefects.Average(d => (double)d.Severity);
                int safetyHazards = openDefects.Count(d => d.IsSafetyHazard);

                // Base score reduced by defect severity
                double score = 100.0;
                score -= maxSeverity * 10; // Max severity penalty
                score -= avgSeverity * openDefects.Count * 3; // Cumulative penalty
                score -= safetyHazards * 15; // Safety hazard penalty

                return Math.Clamp(score, 0, 100);
            }
        }

        private static double CalculateAgeScore(AssetCondition condition)
        {
            if (condition.ExpectedServiceLifeYears <= 0)
                return 70.0;

            double ageRatio = condition.AgeYears / condition.ExpectedServiceLifeYears;

            // Sigmoid function centered at 75% of expected life
            double score = 100.0 / (1.0 + Math.Exp(6.0 * (ageRatio - 0.75)));
            return Math.Clamp(score, 0, 100);
        }

        private static double CalculateServiceLifeScore(AssetCondition condition)
        {
            if (condition.RemainingUsefulLifeMonths <= 0)
                return 5.0;

            if (condition.ExpectedServiceLifeYears <= 0)
                return 50.0;

            double remainingRatio = condition.RemainingUsefulLifeMonths /
                (condition.ExpectedServiceLifeYears * 12.0);

            return Math.Clamp(remainingRatio * 100.0, 0, 100);
        }

        private static double CalculateDeferredMaintenanceScore(AssetCondition condition)
        {
            if (condition.CurrentReplacementValue <= 0)
                return 50.0;

            double deferredRatio = (double)(condition.DeferredMaintenanceCost /
                condition.CurrentReplacementValue);

            // Score decreases as deferred maintenance ratio increases
            double score = 100.0 * (1.0 - Math.Min(1.0, deferredRatio * 5.0));
            return Math.Clamp(score, 0, 100);
        }

        private double CalculateDegradationRate(string assetId, AssetCondition condition)
        {
            lock (_lockObject)
            {
                if (!_inspections.TryGetValue(assetId, out var inspections) || inspections.Count < 2)
                {
                    // Estimate from age and current condition
                    if (condition.AgeYears > 0 && condition.HealthScore < 100)
                    {
                        return (100.0 - condition.HealthScore) / (condition.AgeYears * 12.0);
                    }
                    return 0.5; // Default: 0.5 points per month
                }

                // Calculate from inspection history
                var sorted = inspections.OrderBy(i => i.InspectionDate).ToList();
                double[] healthScores = { 92.5, 77.5, 62.5, 47.5, 30.0, 10.0 };

                double firstHealth = healthScores[(int)sorted.First().ConditionGrade];
                double lastHealth = healthScores[(int)sorted.Last().ConditionGrade];
                double monthsSpan = (sorted.Last().InspectionDate - sorted.First().InspectionDate).TotalDays / 30.44;

                if (monthsSpan > 0 && firstHealth > lastHealth)
                {
                    return (firstHealth - lastHealth) / monthsSpan;
                }
            }

            return 0.5; // Default
        }

        private static ConditionGrade MapHealthToGrade(double healthScore)
        {
            return healthScore switch
            {
                >= 85 => ConditionGrade.A,
                >= 70 => ConditionGrade.B,
                >= 55 => ConditionGrade.C,
                >= 40 => ConditionGrade.D,
                >= 20 => ConditionGrade.E,
                _ => ConditionGrade.F
            };
        }

        #endregion
    }
}
