// ============================================================================
// StingBIM AI - Risk Management Engine
// Comprehensive project risk identification, assessment, and mitigation tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RiskManagement
{
    /// <summary>
    /// Complete risk management engine for BIM projects.
    /// Provides risk identification, assessment, mitigation planning, and monitoring.
    /// </summary>
    public sealed class RiskManagementEngine
    {
        private static readonly Lazy<RiskManagementEngine> _instance =
            new Lazy<RiskManagementEngine>(() => new RiskManagementEngine());
        public static RiskManagementEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ProjectRisk> _risks = new();
        private readonly Dictionary<string, RiskCategory> _riskCategories = new();
        private readonly List<RiskAssessmentRecord> _assessmentHistory = new();
        private readonly Dictionary<string, MitigationAction> _mitigations = new();

        public event EventHandler<RiskEventArgs> RiskIdentified;
        public event EventHandler<RiskEventArgs> RiskLevelChanged;
        public event EventHandler<RiskEventArgs> MitigationRequired;

        private RiskManagementEngine()
        {
            InitializeRiskCategories();
            InitializeStandardRisks();
        }

        #region Initialization

        private void InitializeRiskCategories()
        {
            _riskCategories["TECH"] = new RiskCategory
            {
                CategoryId = "TECH",
                Name = "Technical",
                Description = "Design, engineering, and technology-related risks",
                DefaultOwner = "Technical Lead"
            };

            _riskCategories["SCHED"] = new RiskCategory
            {
                CategoryId = "SCHED",
                Name = "Schedule",
                Description = "Timeline and milestone-related risks",
                DefaultOwner = "Project Manager"
            };

            _riskCategories["COST"] = new RiskCategory
            {
                CategoryId = "COST",
                Name = "Cost",
                Description = "Budget and financial risks",
                DefaultOwner = "Cost Manager"
            };

            _riskCategories["QUAL"] = new RiskCategory
            {
                CategoryId = "QUAL",
                Name = "Quality",
                Description = "Quality and compliance risks",
                DefaultOwner = "QA Manager"
            };

            _riskCategories["COORD"] = new RiskCategory
            {
                CategoryId = "COORD",
                Name = "Coordination",
                Description = "Multi-discipline coordination risks",
                DefaultOwner = "BIM Manager"
            };

            _riskCategories["SAFETY"] = new RiskCategory
            {
                CategoryId = "SAFETY",
                Name = "Safety",
                Description = "Health and safety risks",
                DefaultOwner = "Safety Officer"
            };

            _riskCategories["EXT"] = new RiskCategory
            {
                CategoryId = "EXT",
                Name = "External",
                Description = "External factors (weather, permits, supply chain)",
                DefaultOwner = "Project Manager"
            };

            _riskCategories["SCOPE"] = new RiskCategory
            {
                CategoryId = "SCOPE",
                Name = "Scope",
                Description = "Scope creep and requirement changes",
                DefaultOwner = "Project Manager"
            };
        }

        private void InitializeStandardRisks()
        {
            // Pre-defined common BIM project risks
            RegisterStandardRisk("TECH-001", "TECH", "Complex Design Integration",
                "Multiple complex systems require careful integration",
                RiskLikelihood.Medium, RiskImpact.High);

            RegisterStandardRisk("TECH-002", "TECH", "Technology Incompatibility",
                "Software/hardware compatibility issues between project stakeholders",
                RiskLikelihood.Medium, RiskImpact.Medium);

            RegisterStandardRisk("SCHED-001", "SCHED", "Design Delay",
                "Design deliverables behind schedule affecting downstream activities",
                RiskLikelihood.High, RiskImpact.High);

            RegisterStandardRisk("SCHED-002", "SCHED", "Permit Delays",
                "Regulatory approval taking longer than planned",
                RiskLikelihood.Medium, RiskImpact.High);

            RegisterStandardRisk("COST-001", "COST", "Material Price Escalation",
                "Construction material costs increasing beyond budget",
                RiskLikelihood.High, RiskImpact.Medium);

            RegisterStandardRisk("COST-002", "COST", "Scope Creep",
                "Uncontrolled scope additions increasing project cost",
                RiskLikelihood.High, RiskImpact.High);

            RegisterStandardRisk("QUAL-001", "QUAL", "Model Quality Issues",
                "BIM model quality affecting downstream deliverables",
                RiskLikelihood.Medium, RiskImpact.Medium);

            RegisterStandardRisk("COORD-001", "COORD", "Inter-discipline Clashes",
                "Unresolved clashes between building systems",
                RiskLikelihood.High, RiskImpact.Medium);

            RegisterStandardRisk("COORD-002", "COORD", "Information Exchange Failures",
                "Critical information not reaching appropriate stakeholders",
                RiskLikelihood.Medium, RiskImpact.High);

            RegisterStandardRisk("EXT-001", "EXT", "Supply Chain Disruption",
                "Material or equipment supply delays",
                RiskLikelihood.Medium, RiskImpact.High);

            RegisterStandardRisk("EXT-002", "EXT", "Weather Impact",
                "Adverse weather affecting construction schedule",
                RiskLikelihood.Medium, RiskImpact.Medium);

            RegisterStandardRisk("SAFETY-001", "SAFETY", "Construction Site Safety",
                "Potential safety incidents during construction",
                RiskLikelihood.Low, RiskImpact.Critical);
        }

        private void RegisterStandardRisk(string riskId, string category, string title,
            string description, RiskLikelihood likelihood, RiskImpact impact)
        {
            _risks[riskId] = new ProjectRisk
            {
                RiskId = riskId,
                Title = title,
                Description = description,
                Category = category,
                InitialLikelihood = likelihood,
                CurrentLikelihood = likelihood,
                InitialImpact = impact,
                CurrentImpact = impact,
                Status = RiskStatus.Identified,
                IdentifiedDate = DateTime.UtcNow,
                IsStandardRisk = true,
                RiskScore = CalculateRiskScore(likelihood, impact)
            };
        }

        #endregion

        #region Risk Identification

        /// <summary>
        /// Identify a new project risk
        /// </summary>
        public ProjectRisk IdentifyRisk(RiskIdentificationInput input)
        {
            var riskId = $"{input.Category}-{DateTime.UtcNow:yyyyMMdd}-{_risks.Count + 1:D3}";

            var risk = new ProjectRisk
            {
                RiskId = riskId,
                Title = input.Title,
                Description = input.Description,
                Category = input.Category,
                InitialLikelihood = input.Likelihood,
                CurrentLikelihood = input.Likelihood,
                InitialImpact = input.Impact,
                CurrentImpact = input.Impact,
                Status = RiskStatus.Identified,
                IdentifiedBy = input.IdentifiedBy,
                IdentifiedDate = DateTime.UtcNow,
                Owner = input.Owner ?? _riskCategories[input.Category]?.DefaultOwner,
                AffectedPhases = input.AffectedPhases,
                AffectedDisciplines = input.AffectedDisciplines,
                Triggers = input.Triggers,
                RiskScore = CalculateRiskScore(input.Likelihood, input.Impact),
                IsStandardRisk = false
            };

            lock (_lock)
            {
                _risks[riskId] = risk;
            }

            RiskIdentified?.Invoke(this, new RiskEventArgs
            {
                Type = RiskEventType.Identified,
                RiskId = riskId,
                Message = $"New risk identified: {input.Title} (Score: {risk.RiskScore})"
            });

            // Check if immediate mitigation required
            if (risk.RiskScore >= 12)
            {
                MitigationRequired?.Invoke(this, new RiskEventArgs
                {
                    Type = RiskEventType.MitigationRequired,
                    RiskId = riskId,
                    Message = $"High-priority risk requires immediate mitigation: {input.Title}"
                });
            }

            return risk;
        }

        /// <summary>
        /// Auto-identify risks from project data
        /// </summary>
        public async Task<List<ProjectRisk>> AutoIdentifyRisksAsync(ProjectRiskContext context)
        {
            return await Task.Run(() =>
            {
                var identifiedRisks = new List<ProjectRisk>();

                // Analyze project metrics for risk indicators
                if (context.ClashCount > 100)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "COORD",
                        Title = "High Clash Volume",
                        Description = $"Project has {context.ClashCount} unresolved clashes requiring coordination",
                        Likelihood = RiskLikelihood.High,
                        Impact = context.ClashCount > 200 ? RiskImpact.High : RiskImpact.Medium,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Clash count threshold exceeded" }
                    });
                    identifiedRisks.Add(risk);
                }

                if (context.ScheduleVariance > 10)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "SCHED",
                        Title = "Schedule Slippage",
                        Description = $"Project is {context.ScheduleVariance:F1}% behind schedule",
                        Likelihood = RiskLikelihood.High,
                        Impact = context.ScheduleVariance > 20 ? RiskImpact.High : RiskImpact.Medium,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Schedule variance threshold exceeded" }
                    });
                    identifiedRisks.Add(risk);
                }

                if (context.CostVariance > 10)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "COST",
                        Title = "Budget Overrun Risk",
                        Description = $"Project is {context.CostVariance:F1}% over budget",
                        Likelihood = RiskLikelihood.High,
                        Impact = context.CostVariance > 20 ? RiskImpact.Critical : RiskImpact.High,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Cost variance threshold exceeded" }
                    });
                    identifiedRisks.Add(risk);
                }

                if (context.ModelHealthScore < 70)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "QUAL",
                        Title = "Model Quality Concern",
                        Description = $"Model health score is {context.ModelHealthScore:F0}%, below acceptable threshold",
                        Likelihood = RiskLikelihood.Medium,
                        Impact = RiskImpact.Medium,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Model health below threshold" }
                    });
                    identifiedRisks.Add(risk);
                }

                if (context.ChangeRequestCount > 20)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "SCOPE",
                        Title = "Change Volume Risk",
                        Description = $"High volume of change requests ({context.ChangeRequestCount}) indicating scope instability",
                        Likelihood = RiskLikelihood.High,
                        Impact = RiskImpact.Medium,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Change request volume threshold exceeded" }
                    });
                    identifiedRisks.Add(risk);
                }

                if (context.DemolitionScope && !context.HasDemolitionPlan)
                {
                    var risk = IdentifyRisk(new RiskIdentificationInput
                    {
                        Category = "TECH",
                        Title = "Demolition Planning Gap",
                        Description = "Project has demolition scope but no formal demolition plan",
                        Likelihood = RiskLikelihood.Medium,
                        Impact = RiskImpact.High,
                        IdentifiedBy = "Auto-Detection",
                        Triggers = new List<string> { "Demolition scope without plan detected" }
                    });
                    identifiedRisks.Add(risk);
                }

                return identifiedRisks;
            });
        }

        private int CalculateRiskScore(RiskLikelihood likelihood, RiskImpact impact)
        {
            // 5x5 risk matrix scoring
            var likelihoodScore = (int)likelihood + 1;
            var impactScore = (int)impact + 1;
            return likelihoodScore * impactScore;
        }

        #endregion

        #region Risk Assessment

        /// <summary>
        /// Perform risk assessment and update risk levels
        /// </summary>
        public RiskAssessmentResult AssessRisk(string riskId, RiskAssessmentInput input)
        {
            lock (_lock)
            {
                if (!_risks.TryGetValue(riskId, out var risk))
                    throw new KeyNotFoundException($"Risk {riskId} not found");

                var previousScore = risk.RiskScore;

                // Update risk assessment
                risk.CurrentLikelihood = input.Likelihood;
                risk.CurrentImpact = input.Impact;
                risk.RiskScore = CalculateRiskScore(input.Likelihood, input.Impact);
                risk.LastAssessedDate = DateTime.UtcNow;
                risk.LastAssessedBy = input.AssessedBy;
                risk.AssessmentNotes = input.Notes;
                risk.Status = DetermineRiskStatus(risk);

                // Record assessment history
                var record = new RiskAssessmentRecord
                {
                    RiskId = riskId,
                    AssessedDate = DateTime.UtcNow,
                    AssessedBy = input.AssessedBy,
                    PreviousScore = previousScore,
                    NewScore = risk.RiskScore,
                    PreviousLikelihood = input.PreviousLikelihood,
                    NewLikelihood = input.Likelihood,
                    PreviousImpact = input.PreviousImpact,
                    NewImpact = input.Impact,
                    Notes = input.Notes
                };
                _assessmentHistory.Add(record);

                // Fire event if risk level changed significantly
                if (Math.Abs(risk.RiskScore - previousScore) >= 3)
                {
                    RiskLevelChanged?.Invoke(this, new RiskEventArgs
                    {
                        Type = RiskEventType.LevelChanged,
                        RiskId = riskId,
                        Message = $"Risk level changed from {previousScore} to {risk.RiskScore}"
                    });
                }

                return new RiskAssessmentResult
                {
                    RiskId = riskId,
                    PreviousScore = previousScore,
                    NewScore = risk.RiskScore,
                    ScoreChange = risk.RiskScore - previousScore,
                    Status = risk.Status,
                    RequiresEscalation = risk.RiskScore >= 16,
                    Recommendations = GenerateAssessmentRecommendations(risk)
                };
            }
        }

        private RiskStatus DetermineRiskStatus(ProjectRisk risk)
        {
            if (risk.RiskScore <= 4)
                return RiskStatus.Accepted;
            if (risk.RiskScore <= 9)
                return RiskStatus.Monitoring;
            if (risk.Mitigations?.Any(m => m.Status == MitigationStatus.InProgress) == true)
                return RiskStatus.Mitigating;
            return RiskStatus.Active;
        }

        private List<string> GenerateAssessmentRecommendations(ProjectRisk risk)
        {
            var recommendations = new List<string>();

            if (risk.RiskScore >= 16)
            {
                recommendations.Add("CRITICAL: Escalate to senior management immediately");
                recommendations.Add("Develop contingency plan");
                recommendations.Add("Schedule emergency risk review meeting");
            }
            else if (risk.RiskScore >= 12)
            {
                recommendations.Add("Implement active mitigation measures");
                recommendations.Add("Increase monitoring frequency");
                recommendations.Add("Identify risk owner and assign responsibilities");
            }
            else if (risk.RiskScore >= 6)
            {
                recommendations.Add("Monitor risk status weekly");
                recommendations.Add("Prepare mitigation options for quick deployment");
            }
            else
            {
                recommendations.Add("Continue routine monitoring");
                recommendations.Add("Review at next risk assessment cycle");
            }

            return recommendations;
        }

        #endregion

        #region Mitigation

        /// <summary>
        /// Add mitigation action to a risk
        /// </summary>
        public MitigationAction AddMitigation(string riskId, MitigationInput input)
        {
            lock (_lock)
            {
                if (!_risks.TryGetValue(riskId, out var risk))
                    throw new KeyNotFoundException($"Risk {riskId} not found");

                var mitigation = new MitigationAction
                {
                    MitigationId = $"MIT-{riskId}-{(risk.Mitigations?.Count ?? 0) + 1:D2}",
                    RiskId = riskId,
                    Type = input.Type,
                    Description = input.Description,
                    Owner = input.Owner,
                    PlannedStartDate = input.PlannedStartDate,
                    PlannedEndDate = input.PlannedEndDate,
                    EstimatedCost = input.EstimatedCost,
                    Status = MitigationStatus.Planned,
                    SuccessCriteria = input.SuccessCriteria,
                    Tasks = input.Tasks ?? new List<MitigationTask>()
                };

                if (risk.Mitigations == null)
                    risk.Mitigations = new List<MitigationAction>();

                risk.Mitigations.Add(mitigation);
                _mitigations[mitigation.MitigationId] = mitigation;

                risk.Status = RiskStatus.Mitigating;

                return mitigation;
            }
        }

        /// <summary>
        /// Update mitigation status
        /// </summary>
        public void UpdateMitigationStatus(string mitigationId, MitigationStatus status, string notes = null)
        {
            lock (_lock)
            {
                if (!_mitigations.TryGetValue(mitigationId, out var mitigation))
                    throw new KeyNotFoundException($"Mitigation {mitigationId} not found");

                mitigation.Status = status;
                mitigation.StatusNotes = notes;
                mitigation.LastUpdated = DateTime.UtcNow;

                if (status == MitigationStatus.Completed)
                {
                    mitigation.ActualEndDate = DateTime.UtcNow;

                    // Re-assess the risk
                    if (_risks.TryGetValue(mitigation.RiskId, out var risk))
                    {
                        // Check if all mitigations complete
                        if (risk.Mitigations?.All(m => m.Status == MitigationStatus.Completed) == true)
                        {
                            risk.Status = RiskStatus.Mitigated;
                        }
                    }
                }
                else if (status == MitigationStatus.InProgress)
                {
                    mitigation.ActualStartDate = DateTime.UtcNow;
                }
            }
        }

        #endregion

        #region Analysis & Reporting

        /// <summary>
        /// Generate risk register report
        /// </summary>
        public RiskRegisterReport GenerateRiskRegister(bool includeClosedRisks = false)
        {
            lock (_lock)
            {
                var risks = _risks.Values.AsEnumerable();
                if (!includeClosedRisks)
                    risks = risks.Where(r => r.Status != RiskStatus.Closed && r.Status != RiskStatus.Retired);

                var riskList = risks.OrderByDescending(r => r.RiskScore).ToList();

                return new RiskRegisterReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalRisks = riskList.Count,
                    CriticalRisks = riskList.Count(r => r.RiskScore >= 16),
                    HighRisks = riskList.Count(r => r.RiskScore >= 12 && r.RiskScore < 16),
                    MediumRisks = riskList.Count(r => r.RiskScore >= 6 && r.RiskScore < 12),
                    LowRisks = riskList.Count(r => r.RiskScore < 6),
                    ByCategory = riskList.GroupBy(r => r.Category)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByStatus = riskList.GroupBy(r => r.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TopRisks = riskList.Take(10).ToList(),
                    MitigationSummary = new MitigationSummary
                    {
                        TotalMitigations = _mitigations.Count,
                        Planned = _mitigations.Values.Count(m => m.Status == MitigationStatus.Planned),
                        InProgress = _mitigations.Values.Count(m => m.Status == MitigationStatus.InProgress),
                        Completed = _mitigations.Values.Count(m => m.Status == MitigationStatus.Completed),
                        TotalEstimatedCost = _mitigations.Values.Sum(m => m.EstimatedCost),
                        TotalActualCost = _mitigations.Values.Sum(m => m.ActualCost)
                    },
                    RiskExposure = CalculateRiskExposure(riskList)
                };
            }
        }

        private decimal CalculateRiskExposure(List<ProjectRisk> risks)
        {
            // Expected Monetary Value calculation
            decimal totalExposure = 0;
            foreach (var risk in risks)
            {
                var probability = (double)((int)risk.CurrentLikelihood + 1) / 5.0;
                var impact = risk.EstimatedCostImpact ?? (risk.RiskScore * 10000m); // Default estimate
                totalExposure += (decimal)probability * impact;
            }
            return totalExposure;
        }

        /// <summary>
        /// Generate risk trend analysis
        /// </summary>
        public RiskTrendAnalysis AnalyzeTrends(int periodDays = 30)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddDays(-periodDays);
                var recentAssessments = _assessmentHistory
                    .Where(a => a.AssessedDate >= cutoff)
                    .ToList();

                return new RiskTrendAnalysis
                {
                    PeriodStart = cutoff,
                    PeriodEnd = DateTime.UtcNow,
                    NewRisksIdentified = _risks.Values
                        .Count(r => r.IdentifiedDate >= cutoff),
                    RisksClosed = _risks.Values
                        .Count(r => r.Status == RiskStatus.Closed && r.ClosedDate >= cutoff),
                    AverageScoreChange = recentAssessments.Any()
                        ? recentAssessments.Average(a => a.NewScore - a.PreviousScore) : 0,
                    EscalatedRisks = recentAssessments
                        .Count(a => a.NewScore >= 16 && a.PreviousScore < 16),
                    DeescalatedRisks = recentAssessments
                        .Count(a => a.NewScore < 12 && a.PreviousScore >= 12),
                    TrendDirection = DetermineTrendDirection(recentAssessments),
                    KeyInsights = GenerateTrendInsights(recentAssessments)
                };
            }
        }

        private TrendDirection DetermineTrendDirection(List<RiskAssessmentRecord> assessments)
        {
            if (!assessments.Any()) return TrendDirection.Stable;

            var avgChange = assessments.Average(a => a.NewScore - a.PreviousScore);
            if (avgChange > 1) return TrendDirection.Increasing;
            if (avgChange < -1) return TrendDirection.Decreasing;
            return TrendDirection.Stable;
        }

        private List<string> GenerateTrendInsights(List<RiskAssessmentRecord> assessments)
        {
            var insights = new List<string>();

            var escalated = assessments.Where(a => a.NewScore > a.PreviousScore).ToList();
            var deescalated = assessments.Where(a => a.NewScore < a.PreviousScore).ToList();

            if (escalated.Count > deescalated.Count)
            {
                insights.Add($"Risk profile is worsening: {escalated.Count} risks escalated vs {deescalated.Count} improved");
            }
            else if (deescalated.Count > escalated.Count)
            {
                insights.Add($"Risk profile is improving: {deescalated.Count} risks improved vs {escalated.Count} escalated");
            }

            var criticalRisks = _risks.Values.Where(r => r.RiskScore >= 16).ToList();
            if (criticalRisks.Any())
            {
                insights.Add($"ATTENTION: {criticalRisks.Count} critical risks require immediate attention");
            }

            var unmtigated = _risks.Values.Where(r => r.RiskScore >= 12 && (r.Mitigations == null || !r.Mitigations.Any())).ToList();
            if (unmtigated.Any())
            {
                insights.Add($"{unmtigated.Count} high-priority risks have no mitigation plans");
            }

            return insights;
        }

        /// <summary>
        /// Get risk matrix data for visualization
        /// </summary>
        public RiskMatrix GetRiskMatrix()
        {
            lock (_lock)
            {
                var matrix = new RiskMatrix
                {
                    GeneratedAt = DateTime.UtcNow,
                    Cells = new List<RiskMatrixCell>()
                };

                // Build 5x5 matrix
                for (int l = 0; l < 5; l++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var likelihood = (RiskLikelihood)l;
                        var impact = (RiskImpact)i;

                        var risksInCell = _risks.Values
                            .Where(r => r.CurrentLikelihood == likelihood && r.CurrentImpact == impact)
                            .Where(r => r.Status != RiskStatus.Closed && r.Status != RiskStatus.Retired)
                            .ToList();

                        matrix.Cells.Add(new RiskMatrixCell
                        {
                            Likelihood = likelihood,
                            Impact = impact,
                            RiskCount = risksInCell.Count,
                            RiskIds = risksInCell.Select(r => r.RiskId).ToList(),
                            CellScore = (l + 1) * (i + 1),
                            CellColor = GetCellColor((l + 1) * (i + 1))
                        });
                    }
                }

                return matrix;
            }
        }

        private string GetCellColor(int score)
        {
            return score switch
            {
                >= 16 => "#FF0000", // Red - Critical
                >= 12 => "#FF6600", // Orange - High
                >= 6 => "#FFCC00",  // Yellow - Medium
                _ => "#00CC00"      // Green - Low
            };
        }

        #endregion

        #region Search & Query

        /// <summary>
        /// Search risks by criteria
        /// </summary>
        public List<ProjectRisk> SearchRisks(RiskSearchCriteria criteria)
        {
            lock (_lock)
            {
                var query = _risks.Values.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(criteria.Category))
                    query = query.Where(r => r.Category == criteria.Category);

                if (criteria.Status.HasValue)
                    query = query.Where(r => r.Status == criteria.Status.Value);

                if (criteria.MinScore.HasValue)
                    query = query.Where(r => r.RiskScore >= criteria.MinScore.Value);

                if (criteria.MaxScore.HasValue)
                    query = query.Where(r => r.RiskScore <= criteria.MaxScore.Value);

                if (!string.IsNullOrWhiteSpace(criteria.Owner))
                    query = query.Where(r => r.Owner?.Contains(criteria.Owner, StringComparison.OrdinalIgnoreCase) == true);

                if (!string.IsNullOrWhiteSpace(criteria.SearchText))
                {
                    var search = criteria.SearchText.ToLower();
                    query = query.Where(r =>
                        r.Title.ToLower().Contains(search) ||
                        r.Description.ToLower().Contains(search));
                }

                return query.OrderByDescending(r => r.RiskScore).ToList();
            }
        }

        /// <summary>
        /// Get a specific risk by ID
        /// </summary>
        public ProjectRisk GetRisk(string riskId)
        {
            lock (_lock)
            {
                return _risks.TryGetValue(riskId, out var risk) ? risk : null;
            }
        }

        #endregion
    }

    #region Data Models

    public class ProjectRisk
    {
        public string RiskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public RiskLikelihood InitialLikelihood { get; set; }
        public RiskLikelihood CurrentLikelihood { get; set; }
        public RiskImpact InitialImpact { get; set; }
        public RiskImpact CurrentImpact { get; set; }
        public int RiskScore { get; set; }
        public RiskStatus Status { get; set; }
        public string IdentifiedBy { get; set; }
        public DateTime IdentifiedDate { get; set; }
        public string Owner { get; set; }
        public DateTime? LastAssessedDate { get; set; }
        public string LastAssessedBy { get; set; }
        public string AssessmentNotes { get; set; }
        public List<string> AffectedPhases { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> Triggers { get; set; }
        public List<MitigationAction> Mitigations { get; set; }
        public decimal? EstimatedCostImpact { get; set; }
        public int? EstimatedScheduleImpact { get; set; }
        public DateTime? ClosedDate { get; set; }
        public bool IsStandardRisk { get; set; }
    }

    public class RiskCategory
    {
        public string CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DefaultOwner { get; set; }
    }

    public class RiskIdentificationInput
    {
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public RiskLikelihood Likelihood { get; set; }
        public RiskImpact Impact { get; set; }
        public string IdentifiedBy { get; set; }
        public string Owner { get; set; }
        public List<string> AffectedPhases { get; set; }
        public List<string> AffectedDisciplines { get; set; }
        public List<string> Triggers { get; set; }
    }

    public class RiskAssessmentInput
    {
        public RiskLikelihood Likelihood { get; set; }
        public RiskImpact Impact { get; set; }
        public RiskLikelihood PreviousLikelihood { get; set; }
        public RiskImpact PreviousImpact { get; set; }
        public string AssessedBy { get; set; }
        public string Notes { get; set; }
    }

    public class RiskAssessmentResult
    {
        public string RiskId { get; set; }
        public int PreviousScore { get; set; }
        public int NewScore { get; set; }
        public int ScoreChange { get; set; }
        public RiskStatus Status { get; set; }
        public bool RequiresEscalation { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class RiskAssessmentRecord
    {
        public string RiskId { get; set; }
        public DateTime AssessedDate { get; set; }
        public string AssessedBy { get; set; }
        public int PreviousScore { get; set; }
        public int NewScore { get; set; }
        public RiskLikelihood PreviousLikelihood { get; set; }
        public RiskLikelihood NewLikelihood { get; set; }
        public RiskImpact PreviousImpact { get; set; }
        public RiskImpact NewImpact { get; set; }
        public string Notes { get; set; }
    }

    public class MitigationAction
    {
        public string MitigationId { get; set; }
        public string RiskId { get; set; }
        public MitigationType Type { get; set; }
        public string Description { get; set; }
        public string Owner { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal ActualCost { get; set; }
        public MitigationStatus Status { get; set; }
        public string StatusNotes { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string SuccessCriteria { get; set; }
        public List<MitigationTask> Tasks { get; set; }
    }

    public class MitigationInput
    {
        public MitigationType Type { get; set; }
        public string Description { get; set; }
        public string Owner { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public string SuccessCriteria { get; set; }
        public List<MitigationTask> Tasks { get; set; }
    }

    public class MitigationTask
    {
        public string TaskId { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public RiskTaskStatus Status { get; set; }
    }

    public class ProjectRiskContext
    {
        public int ClashCount { get; set; }
        public double ScheduleVariance { get; set; }
        public double CostVariance { get; set; }
        public double ModelHealthScore { get; set; }
        public int ChangeRequestCount { get; set; }
        public bool DemolitionScope { get; set; }
        public bool HasDemolitionPlan { get; set; }
    }

    public class RiskRegisterReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalRisks { get; set; }
        public int CriticalRisks { get; set; }
        public int HighRisks { get; set; }
        public int MediumRisks { get; set; }
        public int LowRisks { get; set; }
        public Dictionary<string, int> ByCategory { get; set; }
        public Dictionary<RiskStatus, int> ByStatus { get; set; }
        public List<ProjectRisk> TopRisks { get; set; }
        public MitigationSummary MitigationSummary { get; set; }
        public decimal RiskExposure { get; set; }
    }

    public class MitigationSummary
    {
        public int TotalMitigations { get; set; }
        public int Planned { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalActualCost { get; set; }
    }

    public class RiskTrendAnalysis
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int NewRisksIdentified { get; set; }
        public int RisksClosed { get; set; }
        public double AverageScoreChange { get; set; }
        public int EscalatedRisks { get; set; }
        public int DeescalatedRisks { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public List<string> KeyInsights { get; set; }
    }

    public class RiskMatrix
    {
        public DateTime GeneratedAt { get; set; }
        public List<RiskMatrixCell> Cells { get; set; }
    }

    public class RiskMatrixCell
    {
        public RiskLikelihood Likelihood { get; set; }
        public RiskImpact Impact { get; set; }
        public int RiskCount { get; set; }
        public List<string> RiskIds { get; set; }
        public int CellScore { get; set; }
        public string CellColor { get; set; }
    }

    public class RiskSearchCriteria
    {
        public string Category { get; set; }
        public RiskStatus? Status { get; set; }
        public int? MinScore { get; set; }
        public int? MaxScore { get; set; }
        public string Owner { get; set; }
        public string SearchText { get; set; }
    }

    public class RiskEventArgs : EventArgs
    {
        public RiskEventType Type { get; set; }
        public string RiskId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum RiskLikelihood
    {
        VeryLow,    // < 10%
        Low,        // 10-30%
        Medium,     // 30-50%
        High,       // 50-70%
        VeryHigh    // > 70%
    }

    public enum RiskImpact
    {
        Negligible,  // Minimal impact
        Low = Negligible,
        Minor,       // Some impact, manageable
        Moderate,    // Noticeable impact
        Medium = Moderate,
        Major,       // Significant impact
        High = Major,
        Critical     // Severe/project-threatening
    }

    public enum RiskStatus
    {
        Identified,
        Active,
        Monitoring,
        Mitigating,
        Mitigated,
        Accepted,
        Closed,
        Retired
    }

    public enum MitigationType
    {
        Avoid,      // Eliminate the threat
        Transfer,   // Shift risk to third party
        Mitigate,   // Reduce probability/impact
        Accept      // Acknowledge and monitor
    }

    public enum MitigationStatus
    {
        Planned,
        InProgress,
        Completed,
        OnHold,
        Cancelled
    }

    public enum RiskTaskStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Blocked
    }

    public enum TrendDirection
    {
        Increasing,
        Stable,
        Decreasing
    }

    public enum RiskEventType
    {
        Identified,
        LevelChanged,
        MitigationRequired,
        Mitigated,
        Closed
    }

    #endregion
}
