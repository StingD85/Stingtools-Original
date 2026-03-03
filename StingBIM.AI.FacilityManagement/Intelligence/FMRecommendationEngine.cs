// ============================================================================
// StingBIM AI - Facility Management Recommendation Engine
// Provides intelligent recommendations for maintenance, resource allocation,
// energy optimization, and capital planning
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.Knowledge;
using StingBIM.AI.FacilityManagement.WorkOrders;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Recommendation Models

    /// <summary>
    /// AI-generated recommendation
    /// </summary>
    public class FMRecommendation
    {
        public string RecommendationId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public RecommendationType Type { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;

        // Context
        public string Category { get; set; } = string.Empty;
        public List<string> RelatedAssets { get; set; } = new();
        public List<string> RelatedSystems { get; set; } = new();
        public string Location { get; set; } = string.Empty;

        // Evidence
        public double ConfidenceScore { get; set; }
        public List<RecommendationEvidence> SupportingEvidence { get; set; } = new();
        public List<string> DataSources { get; set; } = new();

        // Impact
        public decimal EstimatedCostSavings { get; set; }
        public decimal EstimatedImplementationCost { get; set; }
        public double EstimatedROIMonths { get; set; }
        public string ImpactDescription { get; set; } = string.Empty;
        public List<string> Benefits { get; set; } = new();
        public List<string> Risks { get; set; } = new();

        // Implementation
        public List<string> ActionSteps { get; set; } = new();
        public string TimeframeRecommendation { get; set; } = string.Empty;
        public List<string> RequiredResources { get; set; } = new();
        public List<string> Prerequisites { get; set; } = new();

        // Tracking
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public RecommendationStatus Status { get; set; } = RecommendationStatus.New;
        public string AcceptedBy { get; set; } = string.Empty;
        public DateTime? AcceptedAt { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
    }

    public enum RecommendationType
    {
        MaintenanceSchedule,     // Optimize maintenance timing
        ResourceAllocation,      // Staffing and resource optimization
        EnergyOptimization,      // Energy saving opportunities
        CapitalPlanning,         // Equipment replacement/upgrade
        CostReduction,           // General cost savings
        RiskMitigation,          // Address identified risks
        ProcessImprovement,      // Workflow/process improvements
        VendorManagement,        // Contractor/supplier recommendations
        SpaceOptimization,       // Space utilization improvements
        ComplianceAction,        // Regulatory/compliance requirements
        TechnologyUpgrade,       // System/technology improvements
        TrainingNeed             // Staff training requirements
    }

    public enum RecommendationPriority
    {
        Critical,    // Immediate action required
        High,        // Should be addressed within 1 week
        Medium,      // Should be addressed within 1 month
        Low,         // Address when convenient
        FYI          // Informational only
    }

    public enum RecommendationStatus
    {
        New,
        UnderReview,
        Accepted,
        InProgress,
        Completed,
        Rejected,
        Deferred
    }

    /// <summary>
    /// Evidence supporting a recommendation
    /// </summary>
    public class RecommendationEvidence
    {
        public string EvidenceType { get; set; } = string.Empty; // Data, Pattern, Prediction, Best Practice
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double Weight { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Resource allocation recommendation
    /// </summary>
    public class ResourceRecommendation
    {
        public string ResourceType { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public string RecommendedState { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public decimal CostImpact { get; set; }
    }

    /// <summary>
    /// Maintenance schedule recommendation
    /// </summary>
    public class ScheduleRecommendation
    {
        public string AssetId { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public DateTime CurrentSchedule { get; set; }
        public DateTime RecommendedSchedule { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
    }

    #endregion

    #region Recommendation Engine

    /// <summary>
    /// FM Recommendation Engine
    /// Generates intelligent recommendations based on analytics and knowledge
    /// </summary>
    public class FMRecommendationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly FMKnowledgeBase _knowledgeBase;
        private readonly FMPredictiveAnalytics _predictiveAnalytics;
        private readonly FMPatternRecognition _patternRecognition;
        private readonly FMAnomalyDetection _anomalyDetection;
        private readonly AssetRegistry _assetRegistry;

        // Generated recommendations
        private readonly List<FMRecommendation> _recommendations = new();

        public FMRecommendationEngine(
            FMKnowledgeBase knowledgeBase,
            FMPredictiveAnalytics predictiveAnalytics = null,
            FMPatternRecognition patternRecognition = null,
            FMAnomalyDetection anomalyDetection = null,
            AssetRegistry assetRegistry = null)
        {
            _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
            _predictiveAnalytics = predictiveAnalytics;
            _patternRecognition = patternRecognition;
            _anomalyDetection = anomalyDetection;
            _assetRegistry = assetRegistry;

            Logger.Info("FM Recommendation Engine initialized");
        }

        #region Recommendation Generation

        /// <summary>
        /// Generate all recommendations based on current state
        /// </summary>
        public List<FMRecommendation> GenerateRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Generate different types of recommendations
            recommendations.AddRange(GenerateMaintenanceRecommendations());
            recommendations.AddRange(GenerateEnergyRecommendations());
            recommendations.AddRange(GenerateCapitalPlanningRecommendations());
            recommendations.AddRange(GenerateResourceRecommendations());
            recommendations.AddRange(GenerateRiskMitigationRecommendations());
            recommendations.AddRange(GenerateProcessImprovementRecommendations());

            // Prioritize and deduplicate
            recommendations = PrioritizeRecommendations(recommendations);

            _recommendations.Clear();
            _recommendations.AddRange(recommendations);

            Logger.Info($"Generated {recommendations.Count} recommendations");

            return recommendations;
        }

        /// <summary>
        /// Generate maintenance-related recommendations
        /// </summary>
        public List<FMRecommendation> GenerateMaintenanceRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Based on failure predictions
            if (_predictiveAnalytics != null)
            {
                var predictions = _predictiveAnalytics.PredictFailures(90);
                var highRiskAssets = predictions.Where(p => p.FailureProbability > 0.6).ToList();

                foreach (var prediction in highRiskAssets)
                {
                    recommendations.Add(new FMRecommendation
                    {
                        Type = RecommendationType.MaintenanceSchedule,
                        Priority = prediction.FailureProbability > 0.8 ? RecommendationPriority.Critical : RecommendationPriority.High,
                        Title = $"Preventive Maintenance for {prediction.AssetName}",
                        Description = $"Schedule preventive maintenance to address predicted {prediction.PredictedFailureMode}",
                        Rationale = $"AI prediction indicates {prediction.FailureProbability:P0} probability of failure within {prediction.DaysUntilPredictedFailure} days",
                        Category = "Predictive Maintenance",
                        RelatedAssets = new() { prediction.AssetId },
                        ConfidenceScore = prediction.FailureProbability,
                        SupportingEvidence = new()
                        {
                            new RecommendationEvidence
                            {
                                EvidenceType = "Prediction",
                                Description = $"Failure probability: {prediction.FailureProbability:P0}",
                                Weight = 0.8
                            }
                        },
                        EstimatedCostSavings = prediction.CostAvoidancePotential,
                        EstimatedImplementationCost = prediction.PreventiveMaintenanceCost,
                        EstimatedROIMonths = prediction.PreventiveMaintenanceCost > 0 ?
                            (double)(prediction.PreventiveMaintenanceCost / (prediction.CostAvoidancePotential / 12)) : 0,
                        Benefits = new()
                        {
                            "Avoid unplanned downtime",
                            $"Save potential repair cost of {prediction.EstimatedFailureCost:N0} UGX",
                            "Extend equipment life"
                        },
                        ActionSteps = new()
                        {
                            "Create preventive maintenance work order",
                            prediction.RecommendedAction,
                            "Document findings and update maintenance history"
                        },
                        TimeframeRecommendation = $"Complete by {prediction.RecommendedActionDate:yyyy-MM-dd}"
                    });
                }
            }

            // Based on patterns
            if (_patternRecognition != null)
            {
                var actionablePatterns = _patternRecognition.GetActionablePatterns();

                foreach (var pattern in actionablePatterns.Where(p => p.Type == PatternType.Cyclic))
                {
                    recommendations.Add(new FMRecommendation
                    {
                        Type = RecommendationType.MaintenanceSchedule,
                        Priority = RecommendationPriority.Medium,
                        Title = $"Optimize Maintenance Schedule: {pattern.PatternName}",
                        Description = pattern.Description,
                        Rationale = $"Pattern analysis reveals consistent {pattern.TimingDescription} cycle",
                        Category = "Schedule Optimization",
                        RelatedSystems = pattern.AffectedSystems,
                        ConfidenceScore = pattern.Confidence,
                        SupportingEvidence = new()
                        {
                            new RecommendationEvidence
                            {
                                EvidenceType = "Pattern",
                                Description = $"Observed {pattern.OccurrenceCount} times since {pattern.FirstObserved:yyyy-MM-dd}",
                                Weight = pattern.Confidence
                            }
                        },
                        Benefits = new()
                        {
                            "Reduce reactive maintenance",
                            "Improve maintenance efficiency",
                            "Better resource planning"
                        },
                        ActionSteps = new()
                        {
                            pattern.RecommendedAction,
                            "Update PM schedule in CMMS",
                            "Monitor effectiveness"
                        },
                        TimeframeRecommendation = "Implement within 30 days"
                    });
                }
            }

            // Best practice recommendations
            var bestPractices = _knowledgeBase.GetAllBestPractices();
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.MaintenanceSchedule,
                Priority = RecommendationPriority.Medium,
                Title = "Implement Condition-Based Maintenance",
                Description = "Transition from time-based to condition-based maintenance for critical equipment",
                Rationale = "Industry best practice shows 20-25% reduction in maintenance costs",
                Category = "Strategy",
                ConfidenceScore = 0.85,
                SupportingEvidence = new()
                {
                    new RecommendationEvidence
                    {
                        EvidenceType = "Best Practice",
                        Description = "ISO 55000 Asset Management Standard",
                        Source = "ISO 55000",
                        Weight = 0.9
                    }
                },
                EstimatedCostSavings = 50000000m, // UGX annually
                EstimatedImplementationCost = 30000000m,
                EstimatedROIMonths = 7.2,
                Benefits = new()
                {
                    "20-25% reduction in maintenance costs",
                    "Increased equipment uptime",
                    "Reduced spare parts inventory"
                },
                ActionSteps = new()
                {
                    "Identify critical equipment for CBM program",
                    "Install/configure necessary sensors",
                    "Establish baseline parameters",
                    "Define maintenance trigger thresholds",
                    "Train maintenance staff"
                },
                TimeframeRecommendation = "6-month implementation program"
            });

            return recommendations;
        }

        /// <summary>
        /// Generate energy optimization recommendations
        /// </summary>
        public List<FMRecommendation> GenerateEnergyRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // HVAC Optimization
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.EnergyOptimization,
                Priority = RecommendationPriority.Medium,
                Title = "HVAC Schedule Optimization",
                Description = "Implement occupancy-based HVAC scheduling to reduce energy consumption during unoccupied periods",
                Rationale = "Building analysis shows potential for 15-20% HVAC energy reduction through schedule optimization",
                Category = "Energy",
                RelatedSystems = new() { "HVAC" },
                ConfidenceScore = 0.80,
                EstimatedCostSavings = 36000000m, // UGX annually (assuming 300M energy bill, 40% HVAC, 15% savings)
                EstimatedImplementationCost = 5000000m,
                EstimatedROIMonths = 1.7,
                Benefits = new()
                {
                    "15-20% HVAC energy reduction",
                    "Reduced equipment wear",
                    "Lower carbon footprint"
                },
                ActionSteps = new()
                {
                    "Audit current HVAC operating schedules",
                    "Map occupancy patterns by zone",
                    "Configure BMS for occupancy-based operation",
                    "Implement night setback and weekend schedules",
                    "Monitor and adjust based on feedback"
                },
                TimeframeRecommendation = "Implement within 60 days"
            });

            // LED Lighting Retrofit
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.EnergyOptimization,
                Priority = RecommendationPriority.Low,
                Title = "LED Lighting Retrofit",
                Description = "Replace remaining fluorescent fixtures with LED for common areas and parking",
                Rationale = "LED lighting uses 50-70% less energy than fluorescent with longer lifespan",
                Category = "Energy",
                RelatedSystems = new() { "Electrical" },
                ConfidenceScore = 0.95,
                EstimatedCostSavings = 18000000m, // UGX annually
                EstimatedImplementationCost = 40000000m,
                EstimatedROIMonths = 26.7,
                Benefits = new()
                {
                    "50-70% lighting energy reduction",
                    "Reduced maintenance (longer lamp life)",
                    "Better light quality"
                },
                ActionSteps = new()
                {
                    "Conduct lighting audit",
                    "Prioritize high-usage areas",
                    "Obtain LED retrofit quotes",
                    "Implement in phases",
                    "Dispose of old fixtures properly"
                },
                TimeframeRecommendation = "12-month phased implementation"
            });

            // Peak Demand Management
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.EnergyOptimization,
                Priority = RecommendationPriority.High,
                Title = "Peak Demand Management Program",
                Description = "Implement load shedding and staggered startup to reduce peak demand charges",
                Rationale = "Peak demand often represents 30-40% of electricity cost",
                Category = "Energy",
                RelatedSystems = new() { "Electrical", "HVAC" },
                ConfidenceScore = 0.85,
                EstimatedCostSavings = 24000000m, // UGX annually
                EstimatedImplementationCost = 10000000m,
                EstimatedROIMonths = 5.0,
                Benefits = new()
                {
                    "Reduce peak demand charges",
                    "Avoid demand-based penalties",
                    "Better grid citizenship"
                },
                ActionSteps = new()
                {
                    "Install demand monitoring",
                    "Identify deferrable loads",
                    "Configure BMS for load rotation",
                    "Implement staggered equipment startup",
                    "Set up demand alerts"
                },
                TimeframeRecommendation = "Implement within 90 days"
            });

            return recommendations;
        }

        /// <summary>
        /// Generate capital planning recommendations
        /// </summary>
        public List<FMRecommendation> GenerateCapitalPlanningRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Equipment nearing end of life
            if (_assetRegistry != null)
            {
                var assets = _assetRegistry.GetAllAssets();
                var agingAssets = assets
                    .Where(a =>
                    {
                        var knowledge = _knowledgeBase.GetEquipmentKnowledge(a.AssetType);
                        if (knowledge == null || a.InstallationDate == default) return false;

                        var ageYears = (DateTime.UtcNow - a.InstallationDate).TotalDays / 365.25;
                        return ageYears > knowledge.TypicalLifespanYears * 0.8;
                    })
                    .ToList();

                if (agingAssets.Any())
                {
                    var groupedByType = agingAssets.GroupBy(a => a.AssetType).ToList();

                    foreach (var group in groupedByType)
                    {
                        var knowledge = _knowledgeBase.GetEquipmentKnowledge(group.Key);
                        var avgAge = group.Average(a => (DateTime.UtcNow - a.InstallationDate).TotalDays / 365.25);

                        recommendations.Add(new FMRecommendation
                        {
                            Type = RecommendationType.CapitalPlanning,
                            Priority = avgAge > (knowledge?.TypicalLifespanYears ?? 15) ?
                                RecommendationPriority.High : RecommendationPriority.Medium,
                            Title = $"Capital Planning: {group.Key} Replacement",
                            Description = $"{group.Count()} {group.Key} units approaching end of expected life",
                            Rationale = $"Average age {avgAge:F1} years vs expected life of {knowledge?.TypicalLifespanYears ?? 15} years",
                            Category = "Capital",
                            RelatedAssets = group.Select(a => a.AssetId).ToList(),
                            ConfidenceScore = 0.90,
                            EstimatedImplementationCost = group.Count() * (knowledge?.TypicalMaintenanceCostPerYear ?? 5000000m) * 10,
                            Benefits = new()
                            {
                                "Avoid unexpected failures",
                                "Improved efficiency with new equipment",
                                "Reduced maintenance costs"
                            },
                            ActionSteps = new()
                            {
                                "Conduct condition assessment",
                                "Develop replacement timeline",
                                "Budget for capital expenditure",
                                "Evaluate modern alternatives",
                                "Plan installation to minimize disruption"
                            },
                            TimeframeRecommendation = "Plan within next budget cycle"
                        });
                    }
                }
            }

            // Technology upgrade recommendations
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.TechnologyUpgrade,
                Priority = RecommendationPriority.Medium,
                Title = "BMS Upgrade for Enhanced Analytics",
                Description = "Upgrade Building Management System to support advanced analytics and predictive maintenance",
                Rationale = "Modern BMS platforms provide 10-20% operational efficiency improvement",
                Category = "Technology",
                RelatedSystems = new() { "BMS", "HVAC", "Electrical" },
                ConfidenceScore = 0.75,
                EstimatedCostSavings = 30000000m, // UGX annually
                EstimatedImplementationCost = 100000000m,
                EstimatedROIMonths = 40,
                Benefits = new()
                {
                    "Enhanced monitoring and control",
                    "Integration with predictive analytics",
                    "Remote access and management",
                    "Automated fault detection"
                },
                ActionSteps = new()
                {
                    "Assess current BMS capabilities",
                    "Define requirements for upgrade",
                    "Evaluate vendor options",
                    "Develop implementation plan",
                    "Train operations staff"
                },
                TimeframeRecommendation = "Include in 3-year capital plan"
            });

            return recommendations;
        }

        /// <summary>
        /// Generate resource allocation recommendations
        /// </summary>
        public List<FMRecommendation> GenerateResourceRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Seasonal staffing
            if (_patternRecognition != null)
            {
                var hvacTrend = _patternRecognition.GetSeasonalTrend("HVAC");
                if (hvacTrend != null)
                {
                    var currentIndex = _patternRecognition.GetCurrentMonthActivityIndex("HVAC");

                    if (currentIndex > 1.2)
                    {
                        recommendations.Add(new FMRecommendation
                        {
                            Type = RecommendationType.ResourceAllocation,
                            Priority = RecommendationPriority.High,
                            Title = "Increase HVAC Technician Coverage",
                            Description = "Add temporary HVAC technician support for current high-demand period",
                            Rationale = $"Current month shows {currentIndex:P0} of average HVAC workload",
                            Category = "Staffing",
                            ConfidenceScore = 0.80,
                            Benefits = new()
                            {
                                "Maintain service levels",
                                "Reduce response times",
                                "Prevent backlog buildup"
                            },
                            ActionSteps = new()
                            {
                                "Assess current workload vs capacity",
                                "Engage contractor for temporary support",
                                "Prioritize critical maintenance tasks"
                            },
                            TimeframeRecommendation = "Implement immediately for current period"
                        });
                    }
                }
            }

            // Skill gap analysis
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.TrainingNeed,
                Priority = RecommendationPriority.Medium,
                Title = "Predictive Maintenance Training Program",
                Description = "Train maintenance staff on condition monitoring and predictive maintenance techniques",
                Rationale = "Skilled workforce is essential for CBM program success",
                Category = "Training",
                ConfidenceScore = 0.85,
                EstimatedImplementationCost = 5000000m,
                Benefits = new()
                {
                    "Improved diagnostic capabilities",
                    "Better maintenance decision making",
                    "Staff development and retention"
                },
                ActionSteps = new()
                {
                    "Assess current skill levels",
                    "Identify training needs by role",
                    "Source appropriate training programs",
                    "Schedule training sessions",
                    "Evaluate training effectiveness"
                },
                TimeframeRecommendation = "Complete within 6 months"
            });

            // Parts inventory optimization
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.ResourceAllocation,
                Priority = RecommendationPriority.Low,
                Title = "Critical Spare Parts Inventory Review",
                Description = "Review and optimize critical spare parts inventory based on failure predictions",
                Rationale = "Right-sized inventory reduces stockout risk and carrying costs",
                Category = "Inventory",
                ConfidenceScore = 0.75,
                Benefits = new()
                {
                    "Reduced stockout risk",
                    "Lower inventory carrying costs",
                    "Faster repair completion"
                },
                ActionSteps = new()
                {
                    "Identify critical components by equipment",
                    "Review historical consumption",
                    "Consider lead times and criticality",
                    "Set reorder points and quantities",
                    "Implement inventory tracking"
                },
                TimeframeRecommendation = "Complete within 90 days"
            });

            return recommendations;
        }

        /// <summary>
        /// Generate risk mitigation recommendations
        /// </summary>
        public List<FMRecommendation> GenerateRiskMitigationRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Based on anomalies
            if (_anomalyDetection != null)
            {
                var criticalAnomalies = _anomalyDetection.GetActiveAnomalies(AnomalySeverity.High);

                foreach (var anomaly in criticalAnomalies.Take(5))
                {
                    recommendations.Add(new FMRecommendation
                    {
                        Type = RecommendationType.RiskMitigation,
                        Priority = anomaly.Severity == AnomalySeverity.Critical ?
                            RecommendationPriority.Critical : RecommendationPriority.High,
                        Title = $"Address Anomaly: {anomaly.Title}",
                        Description = anomaly.Description,
                        Rationale = $"Detected anomaly with {anomaly.ConfidenceScore:P0} confidence",
                        Category = "Risk",
                        RelatedAssets = string.IsNullOrEmpty(anomaly.AssetId) ? new() : new() { anomaly.AssetId },
                        ConfidenceScore = anomaly.ConfidenceScore,
                        EstimatedCostSavings = anomaly.EstimatedCostImpact,
                        Benefits = new()
                        {
                            "Prevent potential failure",
                            "Avoid unexpected costs",
                            "Maintain operations"
                        },
                        ActionSteps = new()
                        {
                            anomaly.RecommendedAction,
                            "Investigate root cause",
                            "Document findings"
                        },
                        TimeframeRecommendation = anomaly.RequiresImmediateAction ?
                            "Immediate action required" : "Address within 48 hours"
                    });
                }
            }

            // Compliance risks
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.ComplianceAction,
                Priority = RecommendationPriority.High,
                Title = "Annual Fire System Inspection Due",
                Description = "Schedule comprehensive fire protection system inspection per NFPA requirements",
                Rationale = "Regulatory compliance requirement and life safety",
                Category = "Compliance",
                RelatedSystems = new() { "Fire Protection" },
                ConfidenceScore = 1.0,
                Benefits = new()
                {
                    "Regulatory compliance",
                    "Life safety assurance",
                    "Insurance compliance"
                },
                ActionSteps = new()
                {
                    "Contact certified fire system inspector",
                    "Schedule inspection",
                    "Prepare system documentation",
                    "Address any deficiencies promptly"
                },
                TimeframeRecommendation = "Schedule within 30 days"
            });

            // Single point of failure risks
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.RiskMitigation,
                Priority = RecommendationPriority.Medium,
                Title = "Address Single Point of Failure: Main Chiller",
                Description = "Develop contingency plan for single-chiller building during equipment failure",
                Rationale = "Single chiller represents critical risk during cooling season",
                Category = "Risk",
                RelatedSystems = new() { "HVAC" },
                ConfidenceScore = 0.85,
                Benefits = new()
                {
                    "Reduced downtime risk",
                    "Improved business continuity",
                    "Better tenant satisfaction"
                },
                ActionSteps = new()
                {
                    "Document emergency procedures",
                    "Identify rental chiller options",
                    "Pre-qualify emergency service contractors",
                    "Consider redundancy in future capital planning"
                },
                TimeframeRecommendation = "Complete contingency plan within 60 days"
            });

            return recommendations;
        }

        /// <summary>
        /// Generate process improvement recommendations
        /// </summary>
        public List<FMRecommendation> GenerateProcessImprovementRecommendations()
        {
            var recommendations = new List<FMRecommendation>();

            // Work order management
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.ProcessImprovement,
                Priority = RecommendationPriority.Medium,
                Title = "Implement Mobile Work Order Management",
                Description = "Deploy mobile app for technicians to receive and complete work orders in the field",
                Rationale = "Mobile access improves response times and documentation quality",
                Category = "Process",
                ConfidenceScore = 0.85,
                EstimatedImplementationCost = 8000000m,
                Benefits = new()
                {
                    "30% improvement in work order cycle time",
                    "Better real-time visibility",
                    "Improved documentation with photos",
                    "Reduced paperwork"
                },
                ActionSteps = new()
                {
                    "Evaluate CMMS mobile capabilities",
                    "Configure mobile app for workflows",
                    "Train technicians",
                    "Roll out in phases"
                },
                TimeframeRecommendation = "Implement within 90 days"
            });

            // Vendor management
            recommendations.Add(new FMRecommendation
            {
                Type = RecommendationType.VendorManagement,
                Priority = RecommendationPriority.Low,
                Title = "Consolidate Service Contracts",
                Description = "Review and consolidate service contracts to improve coverage and reduce costs",
                Rationale = "Consolidated contracts often provide better rates and service",
                Category = "Procurement",
                ConfidenceScore = 0.70,
                EstimatedCostSavings = 10000000m, // UGX annually
                Benefits = new()
                {
                    "5-10% cost reduction",
                    "Simplified vendor management",
                    "Clearer accountability"
                },
                ActionSteps = new()
                {
                    "Inventory current contracts",
                    "Identify consolidation opportunities",
                    "Negotiate bundled agreements",
                    "Track performance metrics"
                },
                TimeframeRecommendation = "Complete during next contract renewal cycle"
            });

            return recommendations;
        }

        #endregion

        #region Recommendation Management

        /// <summary>
        /// Prioritize and deduplicate recommendations
        /// </summary>
        private List<FMRecommendation> PrioritizeRecommendations(List<FMRecommendation> recommendations)
        {
            // Remove duplicates (similar title and category)
            var deduplicated = recommendations
                .GroupBy(r => $"{r.Category}|{r.Title.ToLowerInvariant()}")
                .Select(g => g.OrderByDescending(r => r.ConfidenceScore).First())
                .ToList();

            // Sort by priority and confidence
            return deduplicated
                .OrderBy(r => r.Priority)
                .ThenByDescending(r => r.ConfidenceScore)
                .ThenByDescending(r => r.EstimatedCostSavings)
                .ToList();
        }

        /// <summary>
        /// Get recommendations by type
        /// </summary>
        public List<FMRecommendation> GetRecommendations(RecommendationType? type = null, RecommendationPriority? minPriority = null)
        {
            var query = _recommendations.AsEnumerable();

            if (type.HasValue)
                query = query.Where(r => r.Type == type.Value);

            if (minPriority.HasValue)
                query = query.Where(r => r.Priority <= minPriority.Value);

            return query.ToList();
        }

        /// <summary>
        /// Get top recommendations
        /// </summary>
        public List<FMRecommendation> GetTopRecommendations(int count = 10)
        {
            return _recommendations
                .Where(r => r.Status == RecommendationStatus.New || r.Status == RecommendationStatus.UnderReview)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Accept a recommendation
        /// </summary>
        public void AcceptRecommendation(string recommendationId, string acceptedBy)
        {
            var recommendation = _recommendations.FirstOrDefault(r => r.RecommendationId == recommendationId);
            if (recommendation == null) return;

            recommendation.Status = RecommendationStatus.Accepted;
            recommendation.AcceptedBy = acceptedBy;
            recommendation.AcceptedAt = DateTime.UtcNow;

            Logger.Info($"Recommendation {recommendationId} accepted by {acceptedBy}");
        }

        /// <summary>
        /// Reject a recommendation
        /// </summary>
        public void RejectRecommendation(string recommendationId, string reason)
        {
            var recommendation = _recommendations.FirstOrDefault(r => r.RecommendationId == recommendationId);
            if (recommendation == null) return;

            recommendation.Status = RecommendationStatus.Rejected;
            recommendation.RejectionReason = reason;

            Logger.Info($"Recommendation {recommendationId} rejected: {reason}");
        }

        /// <summary>
        /// Get recommendation summary
        /// </summary>
        public RecommendationSummary GetSummary()
        {
            return new RecommendationSummary
            {
                TotalRecommendations = _recommendations.Count,
                ByPriority = _recommendations.GroupBy(r => r.Priority)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ByType = _recommendations.GroupBy(r => r.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ByStatus = _recommendations.GroupBy(r => r.Status)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                TotalEstimatedSavings = _recommendations.Sum(r => r.EstimatedCostSavings),
                TotalImplementationCost = _recommendations.Sum(r => r.EstimatedImplementationCost),
                AverageConfidence = _recommendations.Average(r => r.ConfidenceScore),
                CriticalCount = _recommendations.Count(r => r.Priority == RecommendationPriority.Critical),
                HighCount = _recommendations.Count(r => r.Priority == RecommendationPriority.High)
            };
        }

        #endregion
    }

    /// <summary>
    /// Summary of recommendations
    /// </summary>
    public class RecommendationSummary
    {
        public int TotalRecommendations { get; set; }
        public Dictionary<string, int> ByPriority { get; set; } = new();
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
        public decimal TotalEstimatedSavings { get; set; }
        public decimal TotalImplementationCost { get; set; }
        public double AverageConfidence { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
    }

    #endregion
}
