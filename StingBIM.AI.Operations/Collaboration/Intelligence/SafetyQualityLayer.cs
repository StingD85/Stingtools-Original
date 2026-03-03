// ===================================================================
// StingBIM.AI.Collaboration - Safety & Quality Intelligence Layer
// Provides hazard detection, quality prediction, compliance checking,
// and proactive safety/quality management
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Intelligence
{
    #region Safety Models

    /// <summary>
    /// Detected safety hazard
    /// </summary>
    public class SafetyHazardDetection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HazardType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public HazardSeverity Severity { get; set; }
        public double Confidence { get; set; }
        public HazardLocation? Location { get; set; }
        public List<string> AffectedElements { get; set; } = new();
        public List<string> AffectedTrades { get; set; } = new();
        public List<string> RegulatoryReferences { get; set; } = new();
        public List<SafetyControl> RecommendedControls { get; set; } = new();
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string? Source { get; set; }
        public bool RequiresImmediateAction { get; set; }
    }

    /// <summary>
    /// Hazard severity levels
    /// </summary>
    public enum HazardSeverity
    {
        Low,
        Medium,
        High,
        Critical,
        Imminent
    }

    /// <summary>
    /// Hazard location
    /// </summary>
    public class HazardLocation
    {
        public string Area { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double Radius { get; set; } = 5.0; // meters
    }

    /// <summary>
    /// Safety control measure
    /// </summary>
    public class SafetyControl
    {
        public string ControlType { get; set; } = string.Empty; // Elimination, Substitution, Engineering, Administrative, PPE
        public string Description { get; set; } = string.Empty;
        public double Effectiveness { get; set; }
        public decimal EstimatedCost { get; set; }
        public int ImplementationDays { get; set; }
        public string Priority { get; set; } = "Normal";
    }

    /// <summary>
    /// Safety inspection result
    /// </summary>
    public class SafetyInspectionResult
    {
        public string InspectionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
        public string InspectedBy { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public List<SafetyHazardDetection> HazardsFound { get; set; } = new();
        public List<SafetyObservation> Observations { get; set; } = new();
        public List<SafetyCompliance> ComplianceItems { get; set; } = new();
        public string RecommendedActions { get; set; } = string.Empty;
    }

    /// <summary>
    /// Safety observation
    /// </summary>
    public class SafetyObservation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty; // Safe, At-Risk, Hazard
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
    }

    /// <summary>
    /// Safety compliance item
    /// </summary>
    public class SafetyCompliance
    {
        public string Requirement { get; set; } = string.Empty;
        public string Standard { get; set; } = string.Empty;
        public bool IsCompliant { get; set; }
        public string? NonComplianceDetails { get; set; }
        public string CorrectiveAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Safety trend analysis
    /// </summary>
    public class SafetyTrendAnalysis
    {
        public string ProjectId { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public int TotalIncidents { get; set; }
        public int NearMisses { get; set; }
        public int DaysWithoutIncident { get; set; }
        public double IncidentRate { get; set; }
        public string Trend { get; set; } = string.Empty;
        public List<SafetyMetric> Metrics { get; set; } = new();
        public List<string> HighRiskAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Safety metric
    /// </summary>
    public class SafetyMetric
    {
        public string Name { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double PreviousValue { get; set; }
        public string Trend { get; set; } = string.Empty;
    }

    #endregion

    #region Quality Models

    /// <summary>
    /// Quality issue prediction
    /// </summary>
    public class QualityPrediction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Probability { get; set; }
        public double Impact { get; set; }
        public double RiskScore => Probability * Impact;
        public string Element { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public List<string> ContributingFactors { get; set; } = new();
        public List<QualityMitigation> Mitigations { get; set; } = new();
    }

    /// <summary>
    /// Quality mitigation
    /// </summary>
    public class QualityMitigation
    {
        public string Action { get; set; } = string.Empty;
        public double Effectiveness { get; set; }
        public decimal Cost { get; set; }
        public string Implementation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Quality inspection result
    /// </summary>
    public class QualityInspectionResult
    {
        public string InspectionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
        public string InspectedBy { get; set; } = string.Empty;
        public string WorkItem { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public QualityStatus Status { get; set; }
        public double Score { get; set; }
        public List<QualityDefect> Defects { get; set; } = new();
        public List<QualityChecklistItem> ChecklistResults { get; set; } = new();
        public bool RequiresRework { get; set; }
        public string? ReworkDescription { get; set; }
    }

    /// <summary>
    /// Quality status
    /// </summary>
    public enum QualityStatus
    {
        Passed,
        PassedWithReservations,
        Failed,
        RequiresReinspection
    }

    /// <summary>
    /// Quality defect
    /// </summary>
    public class QualityDefect
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DefectSeverity Severity { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string Responsibility { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string Status { get; set; } = "Open";
    }

    /// <summary>
    /// Defect severity
    /// </summary>
    public enum DefectSeverity
    {
        Minor,
        Moderate,
        Major,
        Critical
    }

    /// <summary>
    /// Quality checklist item
    /// </summary>
    public class QualityChecklistItem
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool? Passed { get; set; }
        public string? Notes { get; set; }
        public string? PhotoUrl { get; set; }
    }

    /// <summary>
    /// Quality trend analysis
    /// </summary>
    public class QualityTrendAnalysis
    {
        public string ProjectId { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public double OverallQualityScore { get; set; }
        public double FirstTimeQualityRate { get; set; }
        public int TotalDefects { get; set; }
        public int OpenDefects { get; set; }
        public double ReworkRate { get; set; }
        public Dictionary<string, double> ScoreByTrade { get; set; } = new();
        public Dictionary<string, int> DefectsByType { get; set; } = new();
        public List<QualityMetric> Metrics { get; set; } = new();
        public List<string> HighRiskAreas { get; set; } = new();
    }

    /// <summary>
    /// Quality metric
    /// </summary>
    public class QualityMetric
    {
        public string Name { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public string Trend { get; set; } = string.Empty;
    }

    #endregion

    #region Safety & Quality Intelligence Layer

    /// <summary>
    /// Safety and Quality intelligence layer
    /// </summary>
    public class SafetyQualityLayer : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, List<SafetyHazardDetection>> _projectHazards = new();
        private readonly ConcurrentDictionary<string, List<QualityDefect>> _projectDefects = new();
        private readonly ConcurrentDictionary<string, SafetyTrendAnalysis> _safetyTrends = new();
        private readonly ConcurrentDictionary<string, QualityTrendAnalysis> _qualityTrends = new();
        private readonly List<HazardPattern> _hazardPatterns = new();
        private readonly List<DefectPattern> _defectPatterns = new();

        public event EventHandler<SafetyHazardDetection>? HazardDetected;
        public event EventHandler<QualityPrediction>? QualityRiskDetected;

        public SafetyQualityLayer(ILogger? logger = null)
        {
            _logger = logger;
            InitializePatterns();
            _logger?.LogInformation("SafetyQualityLayer initialized");
        }

        #region Pattern Initialization

        private void InitializePatterns()
        {
            // Safety hazard patterns
            _hazardPatterns.AddRange(new[]
            {
                new HazardPattern("fall_protection", "Fall Hazard",
                    new[] { "elevated work", "open edge", "unprotected", "ladder", "scaffold", "roof" },
                    HazardSeverity.High,
                    new[] { "OSHA 1926.501", "OSHA 1926.502" }),

                new HazardPattern("electrical", "Electrical Hazard",
                    new[] { "exposed wire", "energized", "electrical panel", "live circuit", "no lockout" },
                    HazardSeverity.Critical,
                    new[] { "OSHA 1926.405", "NFPA 70E" }),

                new HazardPattern("struck_by", "Struck-By Hazard",
                    new[] { "overhead work", "crane", "material storage", "falling object", "unsecured load" },
                    HazardSeverity.High,
                    new[] { "OSHA 1926.251" }),

                new HazardPattern("caught_in", "Caught-In/Between Hazard",
                    new[] { "excavation", "trench", "confined space", "rotating equipment", "pinch point" },
                    HazardSeverity.Critical,
                    new[] { "OSHA 1926.651", "OSHA 1926.652" }),

                new HazardPattern("fire", "Fire Hazard",
                    new[] { "hot work", "welding", "combustible", "flammable", "fire watch" },
                    HazardSeverity.High,
                    new[] { "OSHA 1926.352", "NFPA 51B" }),

                new HazardPattern("hazmat", "Hazardous Materials",
                    new[] { "asbestos", "lead", "silica", "chemical", "hazardous waste" },
                    HazardSeverity.High,
                    new[] { "OSHA 1926.1101", "OSHA 1926.62" }),

                new HazardPattern("housekeeping", "Housekeeping Hazard",
                    new[] { "debris", "cluttered", "trip hazard", "blocked exit", "poor lighting" },
                    HazardSeverity.Medium,
                    new[] { "OSHA 1926.25" }),
            });

            // Quality defect patterns
            _defectPatterns.AddRange(new[]
            {
                new DefectPattern("concrete_crack", "Concrete Cracking",
                    new[] { "crack", "spalling", "honeycomb", "cold joint", "segregation" },
                    new[] { "improper curing", "poor mix design", "premature loading" }),

                new DefectPattern("waterproofing", "Waterproofing Failure",
                    new[] { "leak", "water intrusion", "moisture", "ponding", "efflorescence" },
                    new[] { "improper application", "membrane damage", "poor drainage" }),

                new DefectPattern("alignment", "Alignment Issues",
                    new[] { "out of plumb", "not level", "offset", "misaligned", "tolerance" },
                    new[] { "layout error", "settling", "poor workmanship" }),

                new DefectPattern("finish", "Finish Defects",
                    new[] { "paint", "scratch", "dent", "stain", "uneven", "visible joint" },
                    new[] { "poor surface prep", "improper application", "damage" }),

                new DefectPattern("mep_installation", "MEP Installation Defect",
                    new[] { "improper slope", "no access", "not insulated", "wrong size", "missing support" },
                    new[] { "coordination failure", "incorrect specification", "workmanship" }),
            });
        }

        #endregion

        #region Safety Hazard Detection

        /// <summary>
        /// Detect hazards from photo analysis
        /// </summary>
        public async Task<List<SafetyHazardDetection>> DetectHazardsFromPhotoAsync(
            string projectId,
            byte[] photoData,
            HazardLocation? location = null,
            CancellationToken ct = default)
        {
            var hazards = new List<SafetyHazardDetection>();

            // Simulated image analysis (would use ML model)
            // In production, this would use computer vision APIs

            // Simulate detection based on random factors for demo
            var random = new Random();
            if (random.NextDouble() > 0.7)
            {
                hazards.Add(new SafetyHazardDetection
                {
                    HazardType = "fall_protection",
                    Category = "Fall Hazard",
                    Description = "Worker observed at elevation without fall protection",
                    Severity = HazardSeverity.High,
                    Confidence = 0.85,
                    Location = location,
                    Source = "photo_analysis",
                    RequiresImmediateAction = true,
                    RegulatoryReferences = new List<string> { "OSHA 1926.501" },
                    RecommendedControls = new List<SafetyControl>
                    {
                        new() { ControlType = "Engineering", Description = "Install guardrails", Effectiveness = 0.95, EstimatedCost = 500m, ImplementationDays = 1 },
                        new() { ControlType = "PPE", Description = "Require personal fall arrest system", Effectiveness = 0.85, EstimatedCost = 200m, ImplementationDays = 0 }
                    }
                });
            }

            // Store and notify
            foreach (var hazard in hazards)
            {
                StoreHazard(projectId, hazard);
                HazardDetected?.Invoke(this, hazard);
            }

            return hazards;
        }

        /// <summary>
        /// Detect hazards from text description
        /// </summary>
        public async Task<List<SafetyHazardDetection>> DetectHazardsFromTextAsync(
            string projectId,
            string text,
            HazardLocation? location = null,
            CancellationToken ct = default)
        {
            var hazards = new List<SafetyHazardDetection>();
            var textLower = text.ToLower();

            foreach (var pattern in _hazardPatterns)
            {
                var matchCount = pattern.Keywords.Count(k => textLower.Contains(k));
                if (matchCount >= 2)
                {
                    var confidence = Math.Min(0.95, 0.5 + matchCount * 0.15);

                    var hazard = new SafetyHazardDetection
                    {
                        HazardType = pattern.Type,
                        Category = pattern.Category,
                        Description = $"Potential {pattern.Category.ToLower()} detected: {text}",
                        Severity = pattern.DefaultSeverity,
                        Confidence = confidence,
                        Location = location,
                        Source = "text_analysis",
                        RegulatoryReferences = pattern.RegulatoryReferences.ToList(),
                        RequiresImmediateAction = pattern.DefaultSeverity >= HazardSeverity.High,
                        RecommendedControls = GenerateSafetyControls(pattern.Type)
                    };

                    hazards.Add(hazard);
                    StoreHazard(projectId, hazard);
                    HazardDetected?.Invoke(this, hazard);
                }
            }

            return hazards;
        }

        /// <summary>
        /// Detect hazards from model analysis
        /// </summary>
        public async Task<List<SafetyHazardDetection>> DetectHazardsFromModelAsync(
            string projectId,
            List<ModelElement> elements,
            CancellationToken ct = default)
        {
            var hazards = new List<SafetyHazardDetection>();

            // Check for fall hazards (openings, edges)
            var openings = elements.Where(e =>
                e.Category == "Floor" && e.HasOpening ||
                e.Category == "Shaft" ||
                (e.Category == "Stairs" && !e.HasRailing)).ToList();

            foreach (var opening in openings)
            {
                hazards.Add(new SafetyHazardDetection
                {
                    HazardType = "fall_protection",
                    Category = "Fall Hazard",
                    Description = $"Unprotected opening: {opening.Name}",
                    Severity = HazardSeverity.High,
                    Confidence = 0.9,
                    AffectedElements = new List<string> { opening.Id },
                    Location = new HazardLocation
                    {
                        Level = opening.Level,
                        X = opening.X,
                        Y = opening.Y,
                        Z = opening.Z
                    },
                    Source = "model_analysis"
                });
            }

            // Check for MEP hazards (clearance, access)
            var mepElements = elements.Where(e =>
                e.Category == "Electrical" ||
                e.Category == "Mechanical" ||
                e.Category == "Plumbing").ToList();

            foreach (var mep in mepElements.Where(e => e.Clearance < e.RequiredClearance))
            {
                hazards.Add(new SafetyHazardDetection
                {
                    HazardType = "access",
                    Category = "Inadequate Access",
                    Description = $"Insufficient clearance for maintenance: {mep.Name}",
                    Severity = HazardSeverity.Medium,
                    Confidence = 0.85,
                    AffectedElements = new List<string> { mep.Id },
                    Source = "model_analysis"
                });
            }

            // Store hazards
            foreach (var hazard in hazards)
            {
                StoreHazard(projectId, hazard);
            }

            return hazards;
        }

        private List<SafetyControl> GenerateSafetyControls(string hazardType)
        {
            return hazardType switch
            {
                "fall_protection" => new List<SafetyControl>
                {
                    new() { ControlType = "Engineering", Description = "Install permanent guardrails", Effectiveness = 0.95, Priority = "High" },
                    new() { ControlType = "Engineering", Description = "Install safety nets", Effectiveness = 0.9, Priority = "High" },
                    new() { ControlType = "Administrative", Description = "Restrict access to area", Effectiveness = 0.7, Priority = "Medium" },
                    new() { ControlType = "PPE", Description = "Personal fall arrest system", Effectiveness = 0.85, Priority = "High" }
                },
                "electrical" => new List<SafetyControl>
                {
                    new() { ControlType = "Elimination", Description = "De-energize and lockout/tagout", Effectiveness = 0.99, Priority = "High" },
                    new() { ControlType = "Engineering", Description = "Install barriers around electrical hazard", Effectiveness = 0.85, Priority = "High" },
                    new() { ControlType = "PPE", Description = "Arc flash PPE", Effectiveness = 0.8, Priority = "High" }
                },
                "struck_by" => new List<SafetyControl>
                {
                    new() { ControlType = "Engineering", Description = "Install toe boards and netting", Effectiveness = 0.85, Priority = "High" },
                    new() { ControlType = "Administrative", Description = "Establish exclusion zones", Effectiveness = 0.75, Priority = "Medium" },
                    new() { ControlType = "PPE", Description = "Hard hats required", Effectiveness = 0.6, Priority = "High" }
                },
                _ => new List<SafetyControl>()
            };
        }

        private void StoreHazard(string projectId, SafetyHazardDetection hazard)
        {
            var hazards = _projectHazards.GetOrAdd(projectId, _ => new List<SafetyHazardDetection>());
            lock (hazards)
            {
                hazards.Add(hazard);
            }

            if (hazard.RequiresImmediateAction)
            {
                _logger?.LogWarning("Immediate action required: {Category} hazard in project {ProjectId}",
                    hazard.Category, projectId);
            }
        }

        #endregion

        #region Quality Prediction

        /// <summary>
        /// Predict quality risks
        /// </summary>
        public async Task<List<QualityPrediction>> PredictQualityRisksAsync(
            string projectId,
            List<WorkItem> workItems,
            CancellationToken ct = default)
        {
            var predictions = new List<QualityPrediction>();

            foreach (var item in workItems)
            {
                var prediction = await PredictWorkItemQualityAsync(projectId, item, ct);
                if (prediction.RiskScore > 0.3)
                {
                    predictions.Add(prediction);
                    QualityRiskDetected?.Invoke(this, prediction);
                }
            }

            return predictions.OrderByDescending(p => p.RiskScore).ToList();
        }

        private async Task<QualityPrediction> PredictWorkItemQualityAsync(
            string projectId,
            WorkItem item,
            CancellationToken ct)
        {
            var prediction = new QualityPrediction
            {
                Category = item.Category,
                Description = $"Quality risk for {item.Name}",
                Element = item.ElementId,
                Trade = item.Trade
            };

            // Factors affecting quality
            var factors = new Dictionary<string, double>();

            // Crew experience factor
            if (item.CrewExperienceYears < 2)
            {
                factors["inexperienced_crew"] = 0.3;
                prediction.ContributingFactors.Add("Crew experience less than 2 years");
            }

            // Schedule pressure factor
            if (item.SchedulePressure > 0.7)
            {
                factors["schedule_pressure"] = 0.25;
                prediction.ContributingFactors.Add("High schedule pressure");
            }

            // Complexity factor
            if (item.Complexity > 0.7)
            {
                factors["high_complexity"] = 0.2;
                prediction.ContributingFactors.Add("Complex work item");
            }

            // Weather factor for exterior work
            if (item.IsExterior && item.WeatherRisk > 0.5)
            {
                factors["weather_risk"] = 0.15;
                prediction.ContributingFactors.Add("Weather-sensitive exterior work");
            }

            // Historical performance
            var historicalDefectRate = await GetHistoricalDefectRateAsync(item.Trade, ct);
            if (historicalDefectRate > 0.1)
            {
                factors["historical_issues"] = historicalDefectRate;
                prediction.ContributingFactors.Add($"Historical defect rate: {historicalDefectRate:P0}");
            }

            // Calculate probability
            prediction.Probability = Math.Min(0.95, factors.Values.Sum());

            // Impact based on category
            prediction.Impact = item.Category switch
            {
                "Structure" => 0.9,
                "Waterproofing" => 0.85,
                "MEP" => 0.7,
                "Finishes" => 0.4,
                _ => 0.5
            };

            // Generate mitigations
            if (prediction.RiskScore > 0.4)
            {
                prediction.Mitigations = GenerateQualityMitigations(prediction);
            }

            return prediction;
        }

        private async Task<double> GetHistoricalDefectRateAsync(string trade, CancellationToken ct)
        {
            // Would query historical data
            return trade switch
            {
                "Concrete" => 0.12,
                "Drywall" => 0.15,
                "MEP" => 0.08,
                "Finishes" => 0.18,
                _ => 0.1
            };
        }

        private List<QualityMitigation> GenerateQualityMitigations(QualityPrediction prediction)
        {
            var mitigations = new List<QualityMitigation>();

            if (prediction.ContributingFactors.Any(f => f.Contains("experience")))
            {
                mitigations.Add(new QualityMitigation
                {
                    Action = "Assign experienced supervisor for oversight",
                    Effectiveness = 0.6,
                    Cost = 500m,
                    Implementation = "Direct supervision of work"
                });
            }

            if (prediction.ContributingFactors.Any(f => f.Contains("pressure")))
            {
                mitigations.Add(new QualityMitigation
                {
                    Action = "Implement progressive inspections",
                    Effectiveness = 0.5,
                    Cost = 200m,
                    Implementation = "Inspect at each major milestone"
                });
            }

            if (prediction.ContributingFactors.Any(f => f.Contains("Complex")))
            {
                mitigations.Add(new QualityMitigation
                {
                    Action = "Pre-work meeting and mockup",
                    Effectiveness = 0.7,
                    Cost = 1000m,
                    Implementation = "Build mockup for approval before production"
                });
            }

            return mitigations;
        }

        #endregion

        #region Quality Inspection Analysis

        /// <summary>
        /// Analyze inspection results
        /// </summary>
        public async Task<QualityInspectionResult> AnalyzeInspectionAsync(
            string projectId,
            QualityInspectionResult inspection,
            CancellationToken ct = default)
        {
            // Calculate score
            if (inspection.ChecklistResults.Any())
            {
                var passedItems = inspection.ChecklistResults.Count(c => c.Passed == true);
                var totalItems = inspection.ChecklistResults.Count(c => c.Passed.HasValue);
                inspection.Score = totalItems > 0 ? (double)passedItems / totalItems * 100 : 0;
            }

            // Determine status
            inspection.Status = inspection.Score switch
            {
                >= 95 => QualityStatus.Passed,
                >= 80 => QualityStatus.PassedWithReservations,
                >= 60 => QualityStatus.RequiresReinspection,
                _ => QualityStatus.Failed
            };

            // Identify if rework needed
            inspection.RequiresRework = inspection.Defects.Any(d =>
                d.Severity >= DefectSeverity.Major);

            if (inspection.RequiresRework)
            {
                inspection.ReworkDescription = string.Join("; ",
                    inspection.Defects
                        .Where(d => d.Severity >= DefectSeverity.Major)
                        .Select(d => d.Description));
            }

            // Store defects
            foreach (var defect in inspection.Defects)
            {
                StoreDefect(projectId, defect);
            }

            return inspection;
        }

        private void StoreDefect(string projectId, QualityDefect defect)
        {
            var defects = _projectDefects.GetOrAdd(projectId, _ => new List<QualityDefect>());
            lock (defects)
            {
                defects.Add(defect);
            }
        }

        #endregion

        #region Trend Analysis

        /// <summary>
        /// Analyze safety trends
        /// </summary>
        public async Task<SafetyTrendAnalysis> AnalyzeSafetyTrendsAsync(
            string projectId,
            DateTime fromDate,
            CancellationToken ct = default)
        {
            var analysis = new SafetyTrendAnalysis { ProjectId = projectId };

            var hazards = _projectHazards.TryGetValue(projectId, out var h)
                ? h.Where(x => x.DetectedAt >= fromDate).ToList()
                : new List<SafetyHazardDetection>();

            analysis.TotalIncidents = hazards.Count(h => h.Severity >= HazardSeverity.High);
            analysis.NearMisses = hazards.Count(h => h.Severity == HazardSeverity.Medium);

            // Calculate metrics
            var totalWorkerHours = 10000; // Would be calculated
            analysis.IncidentRate = analysis.TotalIncidents / (totalWorkerHours / 200000.0);

            analysis.Metrics = new List<SafetyMetric>
            {
                new() { Name = "TRIR", CurrentValue = analysis.IncidentRate, TargetValue = 2.0, Trend = "stable" },
                new() { Name = "Near Miss Reports", CurrentValue = analysis.NearMisses, TargetValue = 10, Trend = "up" },
                new() { Name = "Hazards Resolved", CurrentValue = hazards.Count * 0.8, TargetValue = hazards.Count, Trend = "up" }
            };

            // Identify high-risk areas
            analysis.HighRiskAreas = hazards
                .GroupBy(h => h.Location?.Area ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            // Generate recommendations
            analysis.Recommendations = GenerateSafetyRecommendations(hazards);

            // Determine trend
            analysis.Trend = analysis.IncidentRate < 2.0 ? "Improving" : "Needs Attention";

            _safetyTrends[projectId] = analysis;
            return analysis;
        }

        private List<string> GenerateSafetyRecommendations(List<SafetyHazardDetection> hazards)
        {
            var recommendations = new List<string>();

            var topHazardType = hazards
                .GroupBy(h => h.HazardType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            if (topHazardType == "fall_protection")
            {
                recommendations.Add("Increase fall protection training frequency");
                recommendations.Add("Conduct daily fall hazard inspections");
            }

            if (hazards.Any(h => h.RequiresImmediateAction))
            {
                recommendations.Add("Establish rapid response protocol for critical hazards");
            }

            return recommendations;
        }

        /// <summary>
        /// Analyze quality trends
        /// </summary>
        public async Task<QualityTrendAnalysis> AnalyzeQualityTrendsAsync(
            string projectId,
            DateTime fromDate,
            CancellationToken ct = default)
        {
            var analysis = new QualityTrendAnalysis { ProjectId = projectId };

            var defects = _projectDefects.TryGetValue(projectId, out var d)
                ? d.ToList()
                : new List<QualityDefect>();

            analysis.TotalDefects = defects.Count;
            analysis.OpenDefects = defects.Count(d => d.Status == "Open");

            // Calculate first-time quality rate
            // Would query inspection data
            analysis.FirstTimeQualityRate = 0.85;
            analysis.ReworkRate = 0.12;
            analysis.OverallQualityScore = 88.5;

            // Score by trade
            analysis.ScoreByTrade = new Dictionary<string, double>
            {
                ["Concrete"] = 92.0,
                ["Structural Steel"] = 95.0,
                ["MEP"] = 85.0,
                ["Drywall"] = 78.0,
                ["Finishes"] = 82.0
            };

            // Defects by type
            analysis.DefectsByType = defects
                .GroupBy(d => d.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            // Metrics
            analysis.Metrics = new List<QualityMetric>
            {
                new() { Name = "First-Time Quality", CurrentValue = analysis.FirstTimeQualityRate * 100, TargetValue = 90, Trend = "stable" },
                new() { Name = "Rework Rate", CurrentValue = analysis.ReworkRate * 100, TargetValue = 5, Trend = "down" },
                new() { Name = "Open Defects", CurrentValue = analysis.OpenDefects, TargetValue = 0, Trend = "up" }
            };

            // High-risk areas
            analysis.HighRiskAreas = analysis.ScoreByTrade
                .Where(kvp => kvp.Value < 85)
                .Select(kvp => kvp.Key)
                .ToList();

            _qualityTrends[projectId] = analysis;
            return analysis;
        }

        #endregion

        #region Compliance Checking

        /// <summary>
        /// Check safety compliance
        /// </summary>
        public async Task<List<SafetyCompliance>> CheckSafetyComplianceAsync(
            string projectId,
            string area,
            CancellationToken ct = default)
        {
            var results = new List<SafetyCompliance>();

            // Would check against applicable standards
            var requirements = new[]
            {
                ("OSHA 1926.501", "Fall protection required at 6 feet", true),
                ("OSHA 1926.405", "Electrical panels accessible and labeled", true),
                ("OSHA 1926.651", "Excavation inspected daily", true),
                ("OSHA 1926.25", "Housekeeping maintained", false),
            };

            foreach (var (standard, requirement, isCompliant) in requirements)
            {
                results.Add(new SafetyCompliance
                {
                    Standard = standard,
                    Requirement = requirement,
                    IsCompliant = isCompliant,
                    NonComplianceDetails = isCompliant ? null : "Violation observed",
                    CorrectiveAction = isCompliant ? "" : "Immediate correction required"
                });
            }

            return results;
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            _logger?.LogInformation("SafetyQualityLayer disposed");
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Support Classes

    internal class HazardPattern
    {
        public string Type { get; }
        public string Category { get; }
        public string[] Keywords { get; }
        public HazardSeverity DefaultSeverity { get; }
        public string[] RegulatoryReferences { get; }

        public HazardPattern(string type, string category, string[] keywords,
            HazardSeverity severity, string[] references)
        {
            Type = type;
            Category = category;
            Keywords = keywords;
            DefaultSeverity = severity;
            RegulatoryReferences = references;
        }
    }

    internal class DefectPattern
    {
        public string Type { get; }
        public string Category { get; }
        public string[] Keywords { get; }
        public string[] CommonCauses { get; }

        public DefectPattern(string type, string category, string[] keywords, string[] causes)
        {
            Type = type;
            Category = category;
            Keywords = keywords;
            CommonCauses = causes;
        }
    }

    public class ModelElement
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public bool HasOpening { get; set; }
        public bool HasRailing { get; set; }
        public double Clearance { get; set; }
        public double RequiredClearance { get; set; }
    }

    public class WorkItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Trade { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
        public int CrewExperienceYears { get; set; }
        public double SchedulePressure { get; set; }
        public double Complexity { get; set; }
        public bool IsExterior { get; set; }
        public double WeatherRisk { get; set; }
    }

    #endregion
}
