// ===================================================================
// StingBIM Risk Intelligence Engine
// Comprehensive project risk identification, assessment, and mitigation
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RiskIntelligence
{
    /// <summary>
    /// Advanced risk intelligence engine for construction project risk management,
    /// safety analysis, insurance requirements, and mitigation planning
    /// </summary>
    public sealed class RiskIntelligenceEngine
    {
        private static readonly Lazy<RiskIntelligenceEngine> _instance =
            new Lazy<RiskIntelligenceEngine>(() => new RiskIntelligenceEngine());
        public static RiskIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, RiskProject> _projects;
        private readonly ConcurrentDictionary<string, RiskRegister> _registers;
        private readonly ConcurrentDictionary<string, RiskCategory> _categories;
        private readonly object _lockObject = new object();

        public event EventHandler<RiskAlertEventArgs> RiskAlertRaised;
        public event EventHandler<RiskEventArgs> HighRiskIdentified;

        private RiskIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, RiskProject>();
            _registers = new ConcurrentDictionary<string, RiskRegister>();
            _categories = new ConcurrentDictionary<string, RiskCategory>();

            InitializeRiskCategories();
            InitializeRiskTemplates();
        }

        #region Risk Categories Initialization

        private void InitializeRiskCategories()
        {
            var categories = new List<RiskCategory>
            {
                new RiskCategory
                {
                    Id = "DESIGN",
                    Name = "Design Risks",
                    Description = "Risks related to design errors, omissions, and coordination",
                    Subcategories = new List<string> { "Coordination", "Completeness", "Constructability", "Code Compliance", "Scope Creep" }
                },
                new RiskCategory
                {
                    Id = "CONSTRUCTION",
                    Name = "Construction Risks",
                    Description = "Risks during construction phase",
                    Subcategories = new List<string> { "Safety", "Quality", "Productivity", "Site Conditions", "Weather" }
                },
                new RiskCategory
                {
                    Id = "COMMERCIAL",
                    Name = "Commercial Risks",
                    Description = "Financial and contractual risks",
                    Subcategories = new List<string> { "Cost Overrun", "Payment", "Claims", "Change Orders", "Insurance" }
                },
                new RiskCategory
                {
                    Id = "SCHEDULE",
                    Name = "Schedule Risks",
                    Description = "Risks affecting project timeline",
                    Subcategories = new List<string> { "Delays", "Critical Path", "Milestones", "Permits", "Procurement" }
                },
                new RiskCategory
                {
                    Id = "TECHNICAL",
                    Name = "Technical Risks",
                    Description = "Technical and engineering risks",
                    Subcategories = new List<string> { "Complexity", "Technology", "Integration", "Performance", "Testing" }
                },
                new RiskCategory
                {
                    Id = "EXTERNAL",
                    Name = "External Risks",
                    Description = "Risks from external factors",
                    Subcategories = new List<string> { "Market", "Regulatory", "Political", "Environmental", "Force Majeure" }
                },
                new RiskCategory
                {
                    Id = "ORGANIZATIONAL",
                    Name = "Organizational Risks",
                    Description = "Risks related to project organization",
                    Subcategories = new List<string> { "Resources", "Communication", "Experience", "Turnover", "Stakeholders" }
                },
                new RiskCategory
                {
                    Id = "SAFETY",
                    Name = "Safety Risks",
                    Description = "Health and safety risks",
                    Subcategories = new List<string> { "Fall Hazards", "Electrical", "Confined Spaces", "Hazardous Materials", "Equipment" }
                }
            };

            foreach (var category in categories)
            {
                _categories.TryAdd(category.Id, category);
            }
        }

        private readonly List<RiskTemplate> _riskTemplates = new List<RiskTemplate>();

        private void InitializeRiskTemplates()
        {
            _riskTemplates.AddRange(new List<RiskTemplate>
            {
                // Design Risks
                new RiskTemplate { Category = "DESIGN", Title = "Design Coordination Gaps", Description = "Lack of coordination between architectural, structural, and MEP designs leading to conflicts", DefaultProbability = 0.60m, DefaultImpact = 3, DefaultCost = 50000 },
                new RiskTemplate { Category = "DESIGN", Title = "Incomplete Design Documents", Description = "Design documents issued for construction with missing information", DefaultProbability = 0.50m, DefaultImpact = 3, DefaultCost = 75000 },
                new RiskTemplate { Category = "DESIGN", Title = "Code Compliance Issues", Description = "Design does not fully comply with applicable building codes", DefaultProbability = 0.30m, DefaultImpact = 4, DefaultCost = 100000 },
                new RiskTemplate { Category = "DESIGN", Title = "Constructability Issues", Description = "Design is difficult or impossible to construct as detailed", DefaultProbability = 0.40m, DefaultImpact = 3, DefaultCost = 60000 },
                new RiskTemplate { Category = "DESIGN", Title = "Design Scope Creep", Description = "Continuous changes to design scope during construction", DefaultProbability = 0.55m, DefaultImpact = 3, DefaultCost = 80000 },

                // Construction Risks
                new RiskTemplate { Category = "CONSTRUCTION", Title = "Unforeseen Site Conditions", Description = "Discovery of unexpected subsurface or existing conditions", DefaultProbability = 0.45m, DefaultImpact = 4, DefaultCost = 150000 },
                new RiskTemplate { Category = "CONSTRUCTION", Title = "Subcontractor Default", Description = "Key subcontractor fails to perform or goes bankrupt", DefaultProbability = 0.15m, DefaultImpact = 5, DefaultCost = 250000 },
                new RiskTemplate { Category = "CONSTRUCTION", Title = "Quality Defects", Description = "Work not meeting specified quality standards", DefaultProbability = 0.35m, DefaultImpact = 3, DefaultCost = 75000 },
                new RiskTemplate { Category = "CONSTRUCTION", Title = "Labor Shortage", Description = "Insufficient skilled labor available in the market", DefaultProbability = 0.40m, DefaultImpact = 3, DefaultCost = 100000 },
                new RiskTemplate { Category = "CONSTRUCTION", Title = "Equipment Failure", Description = "Critical construction equipment breakdown", DefaultProbability = 0.25m, DefaultImpact = 3, DefaultCost = 50000 },

                // Commercial Risks
                new RiskTemplate { Category = "COMMERCIAL", Title = "Cost Overrun", Description = "Project costs exceed approved budget", DefaultProbability = 0.55m, DefaultImpact = 4, DefaultCost = 200000 },
                new RiskTemplate { Category = "COMMERCIAL", Title = "Payment Delays", Description = "Owner delays in processing payments", DefaultProbability = 0.40m, DefaultImpact = 3, DefaultCost = 50000 },
                new RiskTemplate { Category = "COMMERCIAL", Title = "Material Price Escalation", Description = "Significant increase in material costs", DefaultProbability = 0.50m, DefaultImpact = 3, DefaultCost = 125000 },
                new RiskTemplate { Category = "COMMERCIAL", Title = "Change Order Disputes", Description = "Disagreements over scope and pricing of changes", DefaultProbability = 0.45m, DefaultImpact = 3, DefaultCost = 75000 },
                new RiskTemplate { Category = "COMMERCIAL", Title = "Insurance Coverage Gaps", Description = "Project risks not adequately covered by insurance", DefaultProbability = 0.20m, DefaultImpact = 5, DefaultCost = 500000 },

                // Schedule Risks
                new RiskTemplate { Category = "SCHEDULE", Title = "Permit Delays", Description = "Delays in obtaining required permits and approvals", DefaultProbability = 0.50m, DefaultImpact = 4, DefaultCost = 100000 },
                new RiskTemplate { Category = "SCHEDULE", Title = "Long Lead Equipment Delays", Description = "Delays in delivery of critical long-lead equipment", DefaultProbability = 0.40m, DefaultImpact = 4, DefaultCost = 150000 },
                new RiskTemplate { Category = "SCHEDULE", Title = "Weather Delays", Description = "Adverse weather conditions impacting work", DefaultProbability = 0.60m, DefaultImpact = 2, DefaultCost = 50000 },
                new RiskTemplate { Category = "SCHEDULE", Title = "Critical Path Compression", Description = "Multiple activities on critical path with no float", DefaultProbability = 0.45m, DefaultImpact = 4, DefaultCost = 175000 },
                new RiskTemplate { Category = "SCHEDULE", Title = "Utility Connection Delays", Description = "Delays in utility company connections", DefaultProbability = 0.35m, DefaultImpact = 3, DefaultCost = 75000 },

                // Technical Risks
                new RiskTemplate { Category = "TECHNICAL", Title = "Technology Integration Failure", Description = "Building systems fail to integrate properly", DefaultProbability = 0.30m, DefaultImpact = 4, DefaultCost = 125000 },
                new RiskTemplate { Category = "TECHNICAL", Title = "Performance Not Achieved", Description = "Building systems don't meet specified performance", DefaultProbability = 0.25m, DefaultImpact = 4, DefaultCost = 150000 },
                new RiskTemplate { Category = "TECHNICAL", Title = "Commissioning Issues", Description = "Significant issues discovered during commissioning", DefaultProbability = 0.40m, DefaultImpact = 3, DefaultCost = 75000 },

                // External Risks
                new RiskTemplate { Category = "EXTERNAL", Title = "Regulatory Changes", Description = "Changes to building codes or regulations during project", DefaultProbability = 0.20m, DefaultImpact = 4, DefaultCost = 100000 },
                new RiskTemplate { Category = "EXTERNAL", Title = "Community Opposition", Description = "Local community opposition to the project", DefaultProbability = 0.25m, DefaultImpact = 3, DefaultCost = 75000 },
                new RiskTemplate { Category = "EXTERNAL", Title = "Force Majeure Event", Description = "Natural disaster or other force majeure event", DefaultProbability = 0.10m, DefaultImpact = 5, DefaultCost = 500000 },

                // Safety Risks
                new RiskTemplate { Category = "SAFETY", Title = "Fall From Height", Description = "Workers falling from elevated work areas", DefaultProbability = 0.15m, DefaultImpact = 5, DefaultCost = 250000 },
                new RiskTemplate { Category = "SAFETY", Title = "Struck By Object", Description = "Workers struck by falling or moving objects", DefaultProbability = 0.20m, DefaultImpact = 4, DefaultCost = 150000 },
                new RiskTemplate { Category = "SAFETY", Title = "Electrical Hazards", Description = "Exposure to electrical hazards", DefaultProbability = 0.15m, DefaultImpact = 5, DefaultCost = 200000 },
                new RiskTemplate { Category = "SAFETY", Title = "Trench Collapse", Description = "Excavation or trench collapse", DefaultProbability = 0.10m, DefaultImpact = 5, DefaultCost = 300000 }
            });
        }

        #endregion

        #region Risk Project Management

        public RiskProject CreateProject(RiskProjectRequest request)
        {
            var project = new RiskProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                ProjectType = request.ProjectType,
                ContractValue = request.ContractValue,
                Duration = request.Duration,
                Location = request.Location,
                CreatedDate = DateTime.UtcNow,
                RiskTolerance = request.RiskTolerance ?? RiskTolerance.Medium,
                Registers = new List<string>()
            };

            _projects.TryAdd(project.Id, project);

            // Auto-create initial risk register
            var register = CreateRiskRegister(new RiskRegisterRequest
            {
                ProjectId = project.Id,
                Name = "Primary Risk Register",
                AutoPopulate = request.AutoPopulateRisks
            });

            project.Registers.Add(register.Id);

            return project;
        }

        public RiskRegister CreateRiskRegister(RiskRegisterRequest request)
        {
            var register = new RiskRegister
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                CreatedDate = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Risks = new List<Risk>()
            };

            if (request.AutoPopulate)
            {
                PopulateDefaultRisks(register);
            }

            _registers.TryAdd(register.Id, register);
            return register;
        }

        private void PopulateDefaultRisks(RiskRegister register)
        {
            int sequence = 1;
            foreach (var template in _riskTemplates)
            {
                var risk = new Risk
                {
                    Id = Guid.NewGuid().ToString(),
                    RiskNumber = $"R-{sequence:D3}",
                    Category = template.Category,
                    Title = template.Title,
                    Description = template.Description,
                    Probability = template.DefaultProbability,
                    Impact = template.DefaultImpact,
                    EstimatedCost = template.DefaultCost,
                    Status = RiskStatus.Open,
                    IdentifiedDate = DateTime.UtcNow,
                    Owner = null,
                    MitigationStrategies = new List<MitigationStrategy>(),
                    History = new List<RiskHistoryEntry>()
                };

                risk.RiskScore = CalculateRiskScore(risk.Probability, risk.Impact);
                risk.RiskLevel = DetermineRiskLevel(risk.RiskScore);
                risk.ExpectedValue = risk.Probability * risk.EstimatedCost;

                // Add default mitigation based on category
                AddDefaultMitigation(risk);

                register.Risks.Add(risk);
                sequence++;
            }

            // Calculate register summaries
            UpdateRegisterSummary(register);
        }

        private void AddDefaultMitigation(Risk risk)
        {
            var mitigations = new Dictionary<string, List<MitigationStrategy>>
            {
                { "DESIGN", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Implement BIM coordination and clash detection", EffectivenessPercent = 40, Cost = 25000, Responsibility = "Design Team" },
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Conduct constructability reviews", EffectivenessPercent = 30, Cost = 15000, Responsibility = "Construction Manager" }
                    }
                },
                { "CONSTRUCTION", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Perform thorough site investigation", EffectivenessPercent = 35, Cost = 30000, Responsibility = "Project Manager" },
                        new MitigationStrategy { Type = MitigationType.Transfer, Description = "Include appropriate contract provisions", EffectivenessPercent = 25, Cost = 5000, Responsibility = "Legal/Contracts" }
                    }
                },
                { "COMMERCIAL", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Implement robust cost control procedures", EffectivenessPercent = 30, Cost = 10000, Responsibility = "Cost Manager" },
                        new MitigationStrategy { Type = MitigationType.Accept, Description = "Include adequate contingency in budget", EffectivenessPercent = 20, Cost = 0, Responsibility = "Project Manager" }
                    }
                },
                { "SCHEDULE", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Early engagement with permitting authorities", EffectivenessPercent = 35, Cost = 10000, Responsibility = "Project Manager" },
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Build schedule float into critical activities", EffectivenessPercent = 25, Cost = 0, Responsibility = "Scheduler" }
                    }
                },
                { "TECHNICAL", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Engage specialist consultants early", EffectivenessPercent = 40, Cost = 35000, Responsibility = "Design Team" },
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Develop detailed commissioning plan", EffectivenessPercent = 30, Cost = 20000, Responsibility = "Commissioning Agent" }
                    }
                },
                { "EXTERNAL", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Transfer, Description = "Ensure comprehensive insurance coverage", EffectivenessPercent = 50, Cost = 25000, Responsibility = "Risk Manager" },
                        new MitigationStrategy { Type = MitigationType.Accept, Description = "Include force majeure provisions in contracts", EffectivenessPercent = 20, Cost = 0, Responsibility = "Legal/Contracts" }
                    }
                },
                { "SAFETY", new List<MitigationStrategy>
                    {
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Implement comprehensive safety program", EffectivenessPercent = 50, Cost = 40000, Responsibility = "Safety Manager" },
                        new MitigationStrategy { Type = MitigationType.Reduce, Description = "Conduct regular safety training and toolbox talks", EffectivenessPercent = 30, Cost = 15000, Responsibility = "Safety Manager" }
                    }
                }
            };

            if (mitigations.TryGetValue(risk.Category, out var strategies))
            {
                risk.MitigationStrategies.AddRange(strategies.Select(s => new MitigationStrategy
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = s.Type,
                    Description = s.Description,
                    EffectivenessPercent = s.EffectivenessPercent,
                    Cost = s.Cost,
                    Responsibility = s.Responsibility,
                    Status = MitigationStatus.Planned,
                    DueDate = DateTime.UtcNow.AddDays(30)
                }));
            }
        }

        #endregion

        #region Risk Assessment

        public async Task<RiskAssessment> AssessProjectRisksAsync(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var assessment = new RiskAssessment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                AssessmentDate = DateTime.UtcNow,
                CategorySummaries = new List<CategoryRiskSummary>(),
                TopRisks = new List<Risk>(),
                Recommendations = new List<string>()
            };

            var allRisks = new List<Risk>();
            foreach (var registerId in project.Registers)
            {
                if (_registers.TryGetValue(registerId, out var register))
                {
                    allRisks.AddRange(register.Risks);
                }
            }

            await Task.Run(() =>
            {
                // Calculate overall metrics
                assessment.TotalRisks = allRisks.Count;
                assessment.OpenRisks = allRisks.Count(r => r.Status == RiskStatus.Open || r.Status == RiskStatus.Mitigating);
                assessment.TotalExposure = allRisks.Where(r => r.Status != RiskStatus.Closed).Sum(r => r.ExpectedValue);
                assessment.MitigatedExposure = CalculateMitigatedExposure(allRisks);

                // Category summaries
                foreach (var category in _categories.Values)
                {
                    var categoryRisks = allRisks.Where(r => r.Category == category.Id).ToList();
                    if (categoryRisks.Any())
                    {
                        assessment.CategorySummaries.Add(new CategoryRiskSummary
                        {
                            Category = category.Name,
                            TotalRisks = categoryRisks.Count,
                            HighRisks = categoryRisks.Count(r => r.RiskLevel == RiskLevel.High || r.RiskLevel == RiskLevel.Critical),
                            TotalExposure = categoryRisks.Sum(r => r.ExpectedValue),
                            AverageScore = categoryRisks.Average(r => r.RiskScore)
                        });
                    }
                }

                // Top 10 risks by score
                assessment.TopRisks = allRisks
                    .Where(r => r.Status != RiskStatus.Closed)
                    .OrderByDescending(r => r.RiskScore)
                    .Take(10)
                    .ToList();

                // Risk distribution
                assessment.CriticalCount = allRisks.Count(r => r.RiskLevel == RiskLevel.Critical);
                assessment.HighCount = allRisks.Count(r => r.RiskLevel == RiskLevel.High);
                assessment.MediumCount = allRisks.Count(r => r.RiskLevel == RiskLevel.Medium);
                assessment.LowCount = allRisks.Count(r => r.RiskLevel == RiskLevel.Low);

                // Overall risk rating
                assessment.OverallRiskRating = DetermineOverallRating(assessment);

                // Generate recommendations
                GenerateAssessmentRecommendations(assessment, allRisks, project);
            });

            // Raise alerts for high risks
            if (assessment.CriticalCount > 0)
            {
                RiskAlertRaised?.Invoke(this, new RiskAlertEventArgs
                {
                    ProjectId = projectId,
                    AlertType = "Critical Risks",
                    Message = $"{assessment.CriticalCount} critical risks identified",
                    Severity = AlertSeverity.Critical
                });
            }

            return assessment;
        }

        private decimal CalculateMitigatedExposure(List<Risk> risks)
        {
            decimal mitigatedExposure = 0;
            foreach (var risk in risks.Where(r => r.Status != RiskStatus.Closed))
            {
                var effectiveMitigation = risk.MitigationStrategies
                    .Where(m => m.Status == MitigationStatus.Implemented)
                    .Sum(m => m.EffectivenessPercent) / 100.0m;

                var mitigatedValue = risk.ExpectedValue * (1 - Math.Min(effectiveMitigation, 0.90m));
                mitigatedExposure += mitigatedValue;
            }
            return mitigatedExposure;
        }

        private string DetermineOverallRating(RiskAssessment assessment)
        {
            if (assessment.CriticalCount >= 3 || assessment.HighCount >= 10)
                return "Critical - Immediate action required";
            if (assessment.CriticalCount >= 1 || assessment.HighCount >= 5)
                return "High - Significant risks requiring attention";
            if (assessment.HighCount >= 2 || assessment.MediumCount >= 10)
                return "Medium - Risks require active management";
            return "Low - Risks are well managed";
        }

        private void GenerateAssessmentRecommendations(RiskAssessment assessment, List<Risk> risks, RiskProject project)
        {
            // Critical risk recommendations
            if (assessment.CriticalCount > 0)
            {
                assessment.Recommendations.Add("URGENT: Address critical risks immediately - escalate to senior management");
            }

            // Unmitigated high risks
            var unmitigatedHigh = risks.Count(r => r.RiskLevel == RiskLevel.High &&
                !r.MitigationStrategies.Any(m => m.Status == MitigationStatus.Implemented));
            if (unmitigatedHigh > 0)
            {
                assessment.Recommendations.Add($"Develop and implement mitigation strategies for {unmitigatedHigh} unmitigated high risks");
            }

            // Risk without owners
            var unownedRisks = risks.Count(r => string.IsNullOrEmpty(r.Owner) && r.Status != RiskStatus.Closed);
            if (unownedRisks > 3)
            {
                assessment.Recommendations.Add($"Assign owners to {unownedRisks} risks without designated responsibility");
            }

            // Exposure vs budget
            if (assessment.TotalExposure > project.ContractValue * 0.10m)
            {
                assessment.Recommendations.Add("Total risk exposure exceeds 10% of contract value - review contingency allocation");
            }

            // Safety recommendations
            var safetyRisks = risks.Count(r => r.Category == "SAFETY" && r.RiskLevel >= RiskLevel.Medium);
            if (safetyRisks > 0)
            {
                assessment.Recommendations.Add($"Review and enhance safety measures for {safetyRisks} medium+ safety risks");
            }

            // Insurance review
            var insurableRisks = risks.Where(r => r.EstimatedCost > 100000 && r.Status != RiskStatus.Closed).ToList();
            if (insurableRisks.Any())
            {
                assessment.Recommendations.Add("Review insurance coverage for high-value risks with potential losses exceeding $100,000");
            }

            // Schedule-related recommendations
            var scheduleRisks = risks.Count(r => r.Category == "SCHEDULE" && r.RiskLevel >= RiskLevel.High);
            if (scheduleRisks >= 3)
            {
                assessment.Recommendations.Add("Multiple high schedule risks - consider schedule acceleration options and float management");
            }
        }

        private decimal CalculateRiskScore(decimal probability, int impact)
        {
            // Risk score on 1-25 scale (probability 0-1, impact 1-5)
            return probability * impact * 5;
        }

        private RiskLevel DetermineRiskLevel(decimal score)
        {
            if (score >= 20) return RiskLevel.Critical;
            if (score >= 12) return RiskLevel.High;
            if (score >= 6) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        #endregion

        #region Risk Operations

        public Risk AddRisk(string registerId, RiskRequest request)
        {
            if (!_registers.TryGetValue(registerId, out var register))
                return null;

            var risk = new Risk
            {
                Id = Guid.NewGuid().ToString(),
                RiskNumber = $"R-{register.Risks.Count + 1:D3}",
                Category = request.Category,
                Title = request.Title,
                Description = request.Description,
                Probability = request.Probability,
                Impact = request.Impact,
                EstimatedCost = request.EstimatedCost,
                Status = RiskStatus.Open,
                IdentifiedDate = DateTime.UtcNow,
                Owner = request.Owner,
                MitigationStrategies = new List<MitigationStrategy>(),
                History = new List<RiskHistoryEntry>()
            };

            risk.RiskScore = CalculateRiskScore(risk.Probability, risk.Impact);
            risk.RiskLevel = DetermineRiskLevel(risk.RiskScore);
            risk.ExpectedValue = risk.Probability * risk.EstimatedCost;

            risk.History.Add(new RiskHistoryEntry
            {
                Date = DateTime.UtcNow,
                Action = "Risk Created",
                OldValue = null,
                NewValue = risk.RiskScore.ToString("F1"),
                User = request.CreatedBy
            });

            lock (_lockObject)
            {
                register.Risks.Add(risk);
                register.LastUpdated = DateTime.UtcNow;
            }

            // Alert for high/critical risks
            if (risk.RiskLevel >= RiskLevel.High)
            {
                HighRiskIdentified?.Invoke(this, new RiskEventArgs { Risk = risk });
            }

            UpdateRegisterSummary(register);
            return risk;
        }

        public Risk UpdateRisk(string registerId, string riskId, RiskUpdateRequest request)
        {
            if (!_registers.TryGetValue(registerId, out var register))
                return null;

            var risk = register.Risks.FirstOrDefault(r => r.Id == riskId);
            if (risk == null) return null;

            var oldScore = risk.RiskScore;

            // Update fields
            if (request.Probability.HasValue)
            {
                risk.Probability = request.Probability.Value;
            }
            if (request.Impact.HasValue)
            {
                risk.Impact = request.Impact.Value;
            }
            if (request.EstimatedCost.HasValue)
            {
                risk.EstimatedCost = request.EstimatedCost.Value;
            }
            if (request.Status.HasValue)
            {
                risk.Status = request.Status.Value;
            }
            if (!string.IsNullOrEmpty(request.Owner))
            {
                risk.Owner = request.Owner;
            }

            // Recalculate
            risk.RiskScore = CalculateRiskScore(risk.Probability, risk.Impact);
            risk.RiskLevel = DetermineRiskLevel(risk.RiskScore);
            risk.ExpectedValue = risk.Probability * risk.EstimatedCost;

            // Add history entry
            if (Math.Abs(oldScore - risk.RiskScore) > 0.1m)
            {
                risk.History.Add(new RiskHistoryEntry
                {
                    Date = DateTime.UtcNow,
                    Action = "Risk Score Updated",
                    OldValue = oldScore.ToString("F1"),
                    NewValue = risk.RiskScore.ToString("F1"),
                    User = request.UpdatedBy
                });
            }

            register.LastUpdated = DateTime.UtcNow;
            UpdateRegisterSummary(register);

            return risk;
        }

        public MitigationStrategy AddMitigation(string registerId, string riskId, MitigationRequest request)
        {
            if (!_registers.TryGetValue(registerId, out var register))
                return null;

            var risk = register.Risks.FirstOrDefault(r => r.Id == riskId);
            if (risk == null) return null;

            var mitigation = new MitigationStrategy
            {
                Id = Guid.NewGuid().ToString(),
                Type = request.Type,
                Description = request.Description,
                EffectivenessPercent = request.EffectivenessPercent,
                Cost = request.Cost,
                Responsibility = request.Responsibility,
                Status = MitigationStatus.Planned,
                DueDate = request.DueDate
            };

            lock (_lockObject)
            {
                risk.MitigationStrategies.Add(mitigation);
                risk.History.Add(new RiskHistoryEntry
                {
                    Date = DateTime.UtcNow,
                    Action = "Mitigation Added",
                    NewValue = mitigation.Description,
                    User = request.CreatedBy
                });
            }

            return mitigation;
        }

        public MitigationStrategy UpdateMitigationStatus(string registerId, string riskId, string mitigationId, MitigationStatus newStatus, string updatedBy)
        {
            if (!_registers.TryGetValue(registerId, out var register))
                return null;

            var risk = register.Risks.FirstOrDefault(r => r.Id == riskId);
            var mitigation = risk?.MitigationStrategies.FirstOrDefault(m => m.Id == mitigationId);
            if (mitigation == null) return null;

            var oldStatus = mitigation.Status;
            mitigation.Status = newStatus;

            if (newStatus == MitigationStatus.Implemented)
            {
                mitigation.CompletedDate = DateTime.UtcNow;
            }

            risk.History.Add(new RiskHistoryEntry
            {
                Date = DateTime.UtcNow,
                Action = "Mitigation Status Changed",
                OldValue = oldStatus.ToString(),
                NewValue = newStatus.ToString(),
                User = updatedBy
            });

            // Update risk status if all mitigations implemented
            if (risk.MitigationStrategies.All(m => m.Status == MitigationStatus.Implemented))
            {
                risk.Status = RiskStatus.Mitigated;
            }

            UpdateRegisterSummary(register);
            return mitigation;
        }

        private void UpdateRegisterSummary(RiskRegister register)
        {
            register.TotalRisks = register.Risks.Count;
            register.OpenRisks = register.Risks.Count(r => r.Status == RiskStatus.Open);
            register.TotalExposure = register.Risks.Where(r => r.Status != RiskStatus.Closed).Sum(r => r.ExpectedValue);
            register.HighestRiskScore = register.Risks.Any() ? register.Risks.Max(r => r.RiskScore) : 0;
        }

        #endregion

        #region Monte Carlo Simulation

        public MonteCarloResult RunMonteCarloSimulation(string projectId, int iterations = 10000)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var allRisks = new List<Risk>();
            foreach (var registerId in project.Registers)
            {
                if (_registers.TryGetValue(registerId, out var register))
                {
                    allRisks.AddRange(register.Risks.Where(r => r.Status != RiskStatus.Closed));
                }
            }

            var results = new List<decimal>();
            var random = new Random();

            for (int i = 0; i < iterations; i++)
            {
                decimal totalImpact = 0;
                foreach (var risk in allRisks)
                {
                    // Simulate whether risk occurs
                    if ((decimal)random.NextDouble() < risk.Probability)
                    {
                        // Apply triangular distribution for cost (Low, Most Likely, High)
                        var low = risk.EstimatedCost * 0.7m;
                        var high = risk.EstimatedCost * 1.5m;
                        var mode = risk.EstimatedCost;

                        var u = (decimal)random.NextDouble();
                        var fc = (mode - low) / (high - low);
                        decimal cost;

                        if (u < fc)
                        {
                            cost = low + (decimal)Math.Sqrt((double)(u * (high - low) * (mode - low)));
                        }
                        else
                        {
                            cost = high - (decimal)Math.Sqrt((double)((1 - u) * (high - low) * (high - mode)));
                        }

                        // Apply mitigation effectiveness
                        var mitigationFactor = 1 - risk.MitigationStrategies
                            .Where(m => m.Status == MitigationStatus.Implemented)
                            .Sum(m => m.EffectivenessPercent) / 100.0m;

                        totalImpact += cost * Math.Max(mitigationFactor, 0.10m);
                    }
                }
                results.Add(totalImpact);
            }

            results.Sort();

            return new MonteCarloResult
            {
                ProjectId = projectId,
                Iterations = iterations,
                Mean = results.Average(),
                Median = results[results.Count / 2],
                StandardDeviation = CalculateStandardDeviation(results),
                P10 = results[(int)(iterations * 0.10)],
                P50 = results[(int)(iterations * 0.50)],
                P80 = results[(int)(iterations * 0.80)],
                P90 = results[(int)(iterations * 0.90)],
                P95 = results[(int)(iterations * 0.95)],
                Minimum = results.Min(),
                Maximum = results.Max(),
                RecommendedContingency = results[(int)(iterations * 0.80)] // P80 as recommended contingency
            };
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
        }

        #endregion

        #region Insurance Analysis

        public InsuranceAnalysis AnalyzeInsuranceRequirements(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var analysis = new InsuranceAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                AnalysisDate = DateTime.UtcNow,
                RequiredCoverages = new List<InsuranceCoverage>(),
                Recommendations = new List<string>()
            };

            // Standard construction insurance requirements
            analysis.RequiredCoverages.Add(new InsuranceCoverage
            {
                Type = "Commercial General Liability",
                MinimumLimit = 2000000,
                RecommendedLimit = Math.Max(2000000, project.ContractValue * 0.05m),
                Purpose = "Covers third-party bodily injury and property damage claims",
                Required = true
            });

            analysis.RequiredCoverages.Add(new InsuranceCoverage
            {
                Type = "Professional Liability (E&O)",
                MinimumLimit = 1000000,
                RecommendedLimit = Math.Max(1000000, project.ContractValue * 0.02m),
                Purpose = "Covers design errors and omissions",
                Required = true
            });

            analysis.RequiredCoverages.Add(new InsuranceCoverage
            {
                Type = "Workers' Compensation",
                MinimumLimit = 1000000,
                RecommendedLimit = 1000000,
                Purpose = "Covers employee injuries and occupational diseases",
                Required = true
            });

            analysis.RequiredCoverages.Add(new InsuranceCoverage
            {
                Type = "Builder's Risk",
                MinimumLimit = project.ContractValue,
                RecommendedLimit = project.ContractValue * 1.1m,
                Purpose = "Covers damage to work in progress",
                Required = true
            });

            analysis.RequiredCoverages.Add(new InsuranceCoverage
            {
                Type = "Umbrella/Excess Liability",
                MinimumLimit = 5000000,
                RecommendedLimit = Math.Max(10000000, project.ContractValue * 0.1m),
                Purpose = "Provides additional limits above primary policies",
                Required = project.ContractValue > 10000000
            });

            // Project-specific coverages based on risks
            var allRisks = GetAllProjectRisks(projectId);

            if (allRisks.Any(r => r.Category == "EXTERNAL" && r.Title.Contains("Force Majeure")))
            {
                analysis.RequiredCoverages.Add(new InsuranceCoverage
                {
                    Type = "Delay in Start-Up (DSU)",
                    MinimumLimit = 0,
                    RecommendedLimit = project.ContractValue * 0.15m,
                    Purpose = "Covers financial losses from delayed project completion",
                    Required = false
                });
            }

            if (allRisks.Any(r => r.Category == "CONSTRUCTION" && r.Title.Contains("Subcontractor")))
            {
                analysis.RequiredCoverages.Add(new InsuranceCoverage
                {
                    Type = "Subcontractor Default Insurance",
                    MinimumLimit = 0,
                    RecommendedLimit = project.ContractValue * 0.20m,
                    Purpose = "Covers losses from subcontractor default",
                    Required = false
                });
            }

            // Calculate total recommended coverage
            analysis.TotalRecommendedCoverage = analysis.RequiredCoverages.Sum(c => c.RecommendedLimit);
            analysis.EstimatedPremium = analysis.TotalRecommendedCoverage * 0.005m; // Rough estimate 0.5%

            // Recommendations
            analysis.Recommendations.Add("Ensure all subcontractors provide certificates of insurance meeting minimum requirements");
            analysis.Recommendations.Add("Review policy exclusions carefully, particularly for pollution and professional services");
            analysis.Recommendations.Add("Consider project-specific policy vs. annual policy based on project duration and value");

            if (project.ContractValue > 25000000)
            {
                analysis.Recommendations.Add("Consider Owner Controlled Insurance Program (OCIP) for cost efficiency");
            }

            return analysis;
        }

        private List<Risk> GetAllProjectRisks(string projectId)
        {
            var risks = new List<Risk>();
            if (_projects.TryGetValue(projectId, out var project))
            {
                foreach (var registerId in project.Registers)
                {
                    if (_registers.TryGetValue(registerId, out var register))
                    {
                        risks.AddRange(register.Risks);
                    }
                }
            }
            return risks;
        }

        #endregion

        #region Safety Risk Analysis

        public SafetyRiskAssessment AssessSafetyRisks(string projectId, SafetyAssessmentRequest request)
        {
            var assessment = new SafetyRiskAssessment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                AssessmentDate = DateTime.UtcNow,
                HazardAnalysis = new List<HazardAnalysis>(),
                RequiredPrograms = new List<string>(),
                TrainingRequirements = new List<string>()
            };

            // Analyze work activities for hazards
            foreach (var activity in request.PlannedActivities)
            {
                var hazards = IdentifyHazards(activity);
                assessment.HazardAnalysis.AddRange(hazards);
            }

            // Determine required safety programs
            assessment.RequiredPrograms.AddRange(DetermineRequiredPrograms(assessment.HazardAnalysis));

            // Training requirements
            assessment.TrainingRequirements.AddRange(DetermineTrainingRequirements(assessment.HazardAnalysis));

            // Calculate overall safety risk score
            assessment.OverallSafetyScore = assessment.HazardAnalysis.Any()
                ? (decimal)assessment.HazardAnalysis.Average(h => h.ResidualRisk)
                : 0;

            assessment.SafetyRating = assessment.OverallSafetyScore switch
            {
                <= 3 => "Low Risk - Standard safety measures sufficient",
                <= 6 => "Medium Risk - Enhanced safety measures required",
                <= 9 => "High Risk - Comprehensive safety program needed",
                _ => "Critical Risk - Specialized safety expertise required"
            };

            return assessment;
        }

        private List<HazardAnalysis> IdentifyHazards(string activity)
        {
            var hazards = new List<HazardAnalysis>();

            var hazardMapping = new Dictionary<string, List<(string Hazard, int Severity, int Likelihood, string Control)>>
            {
                { "excavation", new List<(string, int, int, string)>
                    {
                        ("Trench collapse", 5, 3, "Shoring/sloping per OSHA requirements"),
                        ("Underground utilities", 4, 3, "Utility locates before digging"),
                        ("Cave-in", 5, 2, "Trench boxes and daily inspections")
                    }
                },
                { "steel erection", new List<(string, int, int, string)>
                    {
                        ("Falls from height", 5, 3, "Fall protection systems"),
                        ("Struck by falling objects", 4, 3, "Exclusion zones and hard hats"),
                        ("Crane accidents", 5, 2, "Certified operators and lift plans")
                    }
                },
                { "concrete", new List<(string, int, int, string)>
                    {
                        ("Formwork collapse", 5, 2, "Engineered formwork with inspections"),
                        ("Chemical burns", 3, 3, "PPE and wash stations"),
                        ("Silica exposure", 4, 3, "Wet cutting and respiratory protection")
                    }
                },
                { "roofing", new List<(string, int, int, string)>
                    {
                        ("Falls from roof edge", 5, 4, "Guardrails and safety nets"),
                        ("Heat illness", 3, 4, "Work/rest cycles and hydration"),
                        ("Torch burns", 4, 3, "Fire watch and proper training")
                    }
                },
                { "electrical", new List<(string, int, int, string)>
                    {
                        ("Electrocution", 5, 3, "Lockout/tagout procedures"),
                        ("Arc flash", 5, 2, "Arc flash PPE and boundaries"),
                        ("Electrical fires", 4, 2, "Proper installation and inspection")
                    }
                },
                { "demolition", new List<(string, int, int, string)>
                    {
                        ("Structural collapse", 5, 3, "Engineering survey and sequence"),
                        ("Hazardous materials", 4, 4, "Surveys and abatement"),
                        ("Falling debris", 4, 4, "Exclusion zones and netting")
                    }
                }
            };

            var activityLower = activity.ToLower();
            foreach (var mapping in hazardMapping)
            {
                if (activityLower.Contains(mapping.Key))
                {
                    foreach (var (hazard, severity, likelihood, control) in mapping.Value)
                    {
                        var analysis = new HazardAnalysis
                        {
                            Activity = activity,
                            Hazard = hazard,
                            Severity = severity,
                            Likelihood = likelihood,
                            InherentRisk = severity * likelihood,
                            ControlMeasure = control,
                            ResidualRisk = (int)Math.Ceiling(severity * likelihood * 0.3) // Assume 70% reduction with controls
                        };
                        hazards.Add(analysis);
                    }
                }
            }

            return hazards;
        }

        private List<string> DetermineRequiredPrograms(List<HazardAnalysis> hazards)
        {
            var programs = new List<string> { "Site-Specific Safety Plan", "Emergency Action Plan" };

            if (hazards.Any(h => h.Hazard.Contains("fall", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Fall Protection Program");
            if (hazards.Any(h => h.Hazard.Contains("confined", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Confined Space Entry Program");
            if (hazards.Any(h => h.Hazard.Contains("excavation", StringComparison.OrdinalIgnoreCase) || h.Hazard.Contains("trench", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Excavation Safety Program");
            if (hazards.Any(h => h.Hazard.Contains("crane", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Crane and Rigging Program");
            if (hazards.Any(h => h.Hazard.Contains("hazardous", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Hazard Communication Program");
            if (hazards.Any(h => h.Hazard.Contains("silica", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Silica Exposure Control Plan");
            if (hazards.Any(h => h.Hazard.Contains("electric", StringComparison.OrdinalIgnoreCase)))
                programs.Add("Electrical Safety Program");

            return programs.Distinct().ToList();
        }

        private List<string> DetermineTrainingRequirements(List<HazardAnalysis> hazards)
        {
            var training = new List<string> { "OSHA 10-Hour Construction", "Site Orientation" };

            if (hazards.Any(h => h.Hazard.Contains("fall", StringComparison.OrdinalIgnoreCase)))
                training.Add("Fall Protection Training");
            if (hazards.Any(h => h.InherentRisk >= 12))
                training.Add("OSHA 30-Hour Construction (Supervisors)");
            if (hazards.Any(h => h.Hazard.Contains("confined", StringComparison.OrdinalIgnoreCase)))
                training.Add("Confined Space Entry Training");
            if (hazards.Any(h => h.Hazard.Contains("crane", StringComparison.OrdinalIgnoreCase)))
                training.Add("Rigging and Signal Person Training");
            if (hazards.Any(h => h.Hazard.Contains("excavation", StringComparison.OrdinalIgnoreCase)))
                training.Add("Competent Person - Excavation");
            if (hazards.Any(h => h.Hazard.Contains("scaffold", StringComparison.OrdinalIgnoreCase)))
                training.Add("Scaffold User and Competent Person Training");
            if (hazards.Any(h => h.Hazard.Contains("electric", StringComparison.OrdinalIgnoreCase)))
                training.Add("Electrical Safety - Qualified Person");

            return training.Distinct().ToList();
        }

        #endregion

        #region Helper Methods

        public RiskProject GetProject(string projectId)
        {
            _projects.TryGetValue(projectId, out var project);
            return project;
        }

        public RiskRegister GetRegister(string registerId)
        {
            _registers.TryGetValue(registerId, out var register);
            return register;
        }

        public List<RiskCategory> GetRiskCategories()
        {
            return _categories.Values.ToList();
        }

        public List<RiskTemplate> GetRiskTemplates(string categoryId = null)
        {
            if (string.IsNullOrEmpty(categoryId))
                return _riskTemplates;

            return _riskTemplates.Where(t => t.Category == categoryId).ToList();
        }

        #endregion
    }

    #region Data Models

    public class RiskProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public decimal ContractValue { get; set; }
        public int Duration { get; set; }
        public string Location { get; set; }
        public DateTime CreatedDate { get; set; }
        public RiskTolerance RiskTolerance { get; set; }
        public List<string> Registers { get; set; }
    }

    public class RiskProjectRequest
    {
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public decimal ContractValue { get; set; }
        public int Duration { get; set; }
        public string Location { get; set; }
        public RiskTolerance? RiskTolerance { get; set; }
        public bool AutoPopulateRisks { get; set; } = true;
    }

    public class RiskRegister
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<Risk> Risks { get; set; }
        public int TotalRisks { get; set; }
        public int OpenRisks { get; set; }
        public decimal TotalExposure { get; set; }
        public decimal HighestRiskScore { get; set; }
    }

    public class RiskRegisterRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public bool AutoPopulate { get; set; }
    }

    public class RiskCategory
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Subcategories { get; set; }
    }

    public class RiskTemplate
    {
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal DefaultProbability { get; set; }
        public int DefaultImpact { get; set; }
        public decimal DefaultCost { get; set; }
    }

    public class Risk
    {
        public string Id { get; set; }
        public string RiskNumber { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Probability { get; set; }
        public int Impact { get; set; }
        public decimal RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal ExpectedValue { get; set; }
        public RiskStatus Status { get; set; }
        public DateTime IdentifiedDate { get; set; }
        public string Owner { get; set; }
        public List<MitigationStrategy> MitigationStrategies { get; set; }
        public List<RiskHistoryEntry> History { get; set; }
    }

    public class RiskRequest
    {
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Probability { get; set; }
        public int Impact { get; set; }
        public decimal EstimatedCost { get; set; }
        public string Owner { get; set; }
        public string CreatedBy { get; set; }
    }

    public class RiskUpdateRequest
    {
        public decimal? Probability { get; set; }
        public int? Impact { get; set; }
        public decimal? EstimatedCost { get; set; }
        public RiskStatus? Status { get; set; }
        public string Owner { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class MitigationStrategy
    {
        public string Id { get; set; }
        public MitigationType Type { get; set; }
        public string Description { get; set; }
        public int EffectivenessPercent { get; set; }
        public decimal Cost { get; set; }
        public string Responsibility { get; set; }
        public MitigationStatus Status { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    public class MitigationRequest
    {
        public MitigationType Type { get; set; }
        public string Description { get; set; }
        public int EffectivenessPercent { get; set; }
        public decimal Cost { get; set; }
        public string Responsibility { get; set; }
        public DateTime DueDate { get; set; }
        public string CreatedBy { get; set; }
    }

    public class RiskHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string User { get; set; }
    }

    public class RiskAssessment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public int TotalRisks { get; set; }
        public int OpenRisks { get; set; }
        public decimal TotalExposure { get; set; }
        public decimal MitigatedExposure { get; set; }
        public List<CategoryRiskSummary> CategorySummaries { get; set; }
        public List<Risk> TopRisks { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }
        public string OverallRiskRating { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class CategoryRiskSummary
    {
        public string Category { get; set; }
        public int TotalRisks { get; set; }
        public int HighRisks { get; set; }
        public decimal TotalExposure { get; set; }
        public decimal AverageScore { get; set; }
    }

    public class MonteCarloResult
    {
        public string ProjectId { get; set; }
        public int Iterations { get; set; }
        public decimal Mean { get; set; }
        public decimal Median { get; set; }
        public decimal StandardDeviation { get; set; }
        public decimal P10 { get; set; }
        public decimal P50 { get; set; }
        public decimal P80 { get; set; }
        public decimal P90 { get; set; }
        public decimal P95 { get; set; }
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public decimal RecommendedContingency { get; set; }
    }

    public class InsuranceAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<InsuranceCoverage> RequiredCoverages { get; set; }
        public decimal TotalRecommendedCoverage { get; set; }
        public decimal EstimatedPremium { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class InsuranceCoverage
    {
        public string Type { get; set; }
        public decimal MinimumLimit { get; set; }
        public decimal RecommendedLimit { get; set; }
        public string Purpose { get; set; }
        public bool Required { get; set; }
    }

    public class SafetyAssessmentRequest
    {
        public List<string> PlannedActivities { get; set; }
    }

    public class SafetyRiskAssessment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public List<HazardAnalysis> HazardAnalysis { get; set; }
        public List<string> RequiredPrograms { get; set; }
        public List<string> TrainingRequirements { get; set; }
        public decimal OverallSafetyScore { get; set; }
        public string SafetyRating { get; set; }
    }

    public class HazardAnalysis
    {
        public string Activity { get; set; }
        public string Hazard { get; set; }
        public int Severity { get; set; }
        public int Likelihood { get; set; }
        public int InherentRisk { get; set; }
        public string ControlMeasure { get; set; }
        public int ResidualRisk { get; set; }
    }

    public class RiskAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    public class RiskEventArgs : EventArgs
    {
        public Risk Risk { get; set; }
    }

    public enum RiskTolerance { Low, Medium, High }
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum RiskStatus { Open, Mitigating, Mitigated, Closed, Accepted }
    public enum MitigationType { Avoid, Transfer, Reduce, Accept }
    public enum MitigationStatus { Planned, InProgress, Implemented, Cancelled }
    public enum AlertSeverity { Low, Medium, High, Critical }

    #endregion
}
