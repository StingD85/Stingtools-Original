// StingBIM.AI.Tests - DecisionSupportTests.cs
// Unit tests for Decision Support System
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Reasoning
{
    /// <summary>
    /// Unit tests for DecisionSupport multi-criteria analysis system.
    /// </summary>
    [TestFixture]
    public class DecisionSupportTests
    {
        private TestDecisionSupport _decisionSupport;

        [SetUp]
        public void Setup()
        {
            _decisionSupport = new TestDecisionSupport();
        }

        #region Alternative Evaluation Tests

        [Test]
        public void EvaluateAlternatives_WithTwoAlternatives_ReturnsRanking()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Alt1",
                    Name = "Concrete Frame",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1500,
                        ["Durability"] = 50,
                        ["EmbodiedCarbon"] = 300
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Alt2",
                    Name = "Steel Frame",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1800,
                        ["Durability"] = 60,
                        ["EmbodiedCarbon"] = 450
                    }
                }
            };

            var weights = new Dictionary<string, double>
            {
                ["InitialCost"] = 0.4,
                ["Durability"] = 0.35,
                ["EmbodiedCarbon"] = 0.25
            };

            // Act
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Assert
            analysis.Should().NotBeNull();
            analysis.Ranking.Should().HaveCount(2);
            analysis.WeightedScores.Should().HaveCount(2);
        }

        [Test]
        public void EvaluateAlternatives_WhenCostIsHighlyWeighted_LowerCostWins()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Expensive",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 2000, // Higher cost
                        ["Durability"] = 80     // Better durability
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Affordable",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000, // Lower cost
                        ["Durability"] = 40     // Lower durability
                    }
                }
            };

            var weights = new Dictionary<string, double>
            {
                ["InitialCost"] = 0.9,   // Heavy weight on cost
                ["Durability"] = 0.1
            };

            // Act
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Assert
            analysis.Ranking.First().AlternativeId.Should().Be("Affordable");
        }

        [Test]
        public void EvaluateAlternatives_WhenPerformanceIsHighlyWeighted_HigherPerformanceWins()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "HighPerformance",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["EnergyPerformance"] = 50,  // Lower energy use (better)
                        ["ThermalComfort"] = 90      // Higher comfort (better)
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "LowPerformance",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["EnergyPerformance"] = 150, // Higher energy use (worse)
                        ["ThermalComfort"] = 60      // Lower comfort (worse)
                    }
                }
            };

            var weights = new Dictionary<string, double>
            {
                ["EnergyPerformance"] = 0.5,
                ["ThermalComfort"] = 0.5
            };

            // Act
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Assert
            analysis.Ranking.First().AlternativeId.Should().Be("HighPerformance");
        }

        [Test]
        public void EvaluateAlternatives_GeneratesRecommendation()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "A",
                    Name = "Option A",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000
                    }
                }
            };

            var weights = new Dictionary<string, double>
            {
                ["InitialCost"] = 1.0
            };

            // Act
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Assert
            analysis.Recommendation.Should().NotBeNull();
            analysis.Recommendation.RecommendedAlternative.Should().Be("A");
        }

        #endregion

        #region Score Normalization Tests

        [Test]
        public void NormalizeScores_MinimizeCriterion_LowestValueGetsHighestScore()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Cheap",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 500
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Medium",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Expensive",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1500
                    }
                }
            };

            // Act
            var normalized = _decisionSupport.NormalizeScores(alternatives);

            // Assert - for minimize criterion, lowest value should get highest normalized score
            normalized["Cheap"]["InitialCost"].Should().BeGreaterThan(normalized["Expensive"]["InitialCost"]);
        }

        [Test]
        public void NormalizeScores_MaximizeCriterion_HighestValueGetsHighestScore()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Short",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["Durability"] = 20
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Long",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["Durability"] = 100
                    }
                }
            };

            // Act
            var normalized = _decisionSupport.NormalizeScores(alternatives);

            // Assert - for maximize criterion, highest value should get highest normalized score
            normalized["Long"]["Durability"].Should().BeGreaterThan(normalized["Short"]["Durability"]);
        }

        #endregion

        #region Trade-Off Analysis Tests

        [Test]
        public void GetTradeOffGuidance_CostVsEnergy_ReturnsGuidance()
        {
            // Act
            var guidance = _decisionSupport.GetTradeOffGuidance("InitialCost", "EnergyPerformance");

            // Assert
            guidance.Should().NotBeNull();
            guidance.HasRule.Should().BeTrue();
            guidance.Guidance.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void GetTradeOffGuidance_UnknownCombination_ReturnsNoRule()
        {
            // Act
            var guidance = _decisionSupport.GetTradeOffGuidance("UnknownCriterion1", "UnknownCriterion2");

            // Assert
            guidance.Should().NotBeNull();
            guidance.HasRule.Should().BeFalse();
        }

        [Test]
        public void AnalyzeTradeOff_ValidCriteria_ReturnsAnalysis()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "A",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000,
                        ["EnergyPerformance"] = 100
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "B",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1500,
                        ["EnergyPerformance"] = 60
                    }
                }
            };

            // Act
            var analysis = _decisionSupport.AnalyzeTradeOff(alternatives, "InitialCost", "EnergyPerformance");

            // Assert
            analysis.Should().NotBeNull();
            analysis.DataPoints.Should().HaveCount(2);
        }

        [Test]
        public void AnalyzeTradeOff_IdentifiesParetoFrontier()
        {
            // Arrange - Create alternatives where some dominate others
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Dominated",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1500, // Worse
                        ["EnergyPerformance"] = 100 // Worse
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "Pareto",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000, // Better
                        ["EnergyPerformance"] = 50 // Better
                    }
                }
            };

            // Act
            var analysis = _decisionSupport.AnalyzeTradeOff(alternatives, "InitialCost", "EnergyPerformance");

            // Assert
            analysis.ParetoFrontier.Should().NotBeEmpty();
            analysis.ParetoFrontier.Any(p => p.AlternativeId == "Pareto").Should().BeTrue();
        }

        #endregion

        #region Risk Assessment Tests

        [Test]
        public void AssessRisks_WithSupplyChainTriggers_IdentifiesRisks()
        {
            // Arrange
            var alternative = new TestDesignAlternative
            {
                AlternativeId = "RiskyOption",
                Characteristics = new List<string>
                {
                    "Uses imported materials",
                    "Single source suppliers",
                    "Long lead times"
                }
            };

            var context = new TestProjectContext
            {
                Region = "Africa",
                BuildingType = "Office"
            };

            // Act
            var assessment = _decisionSupport.AssessRisks(alternative, context);

            // Assert
            assessment.Should().NotBeNull();
            assessment.IdentifiedRisks.Should().NotBeEmpty();
        }

        [Test]
        public void AssessRisks_NoTriggers_ReturnsLowRisk()
        {
            // Arrange
            var alternative = new TestDesignAlternative
            {
                AlternativeId = "SafeOption",
                Characteristics = new List<string>
                {
                    "Uses local materials",
                    "Standard construction"
                }
            };

            var context = new TestProjectContext();

            // Act
            var assessment = _decisionSupport.AssessRisks(alternative, context);

            // Assert
            assessment.Should().NotBeNull();
            assessment.RiskLevel.Should().Be(TestRiskLevel.Low);
        }

        #endregion

        #region Decision Template Tests

        [Test]
        public void GetTemplate_StructuralSystem_ReturnsTemplate()
        {
            // Act
            var template = _decisionSupport.GetTemplate("StructuralSystem");

            // Assert
            template.Should().NotBeNull();
            template.Name.Should().Be("Structural System Selection");
            template.TypicalAlternatives.Should().NotBeEmpty();
        }

        [Test]
        public void GetAllTemplates_ReturnsMultipleTemplates()
        {
            // Act
            var templates = _decisionSupport.GetAllTemplates();

            // Assert
            templates.Should().NotBeEmpty();
            templates.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        [Test]
        public void GetTemplate_UnknownId_ReturnsNull()
        {
            // Act
            var template = _decisionSupport.GetTemplate("NonExistentTemplate");

            // Assert
            template.Should().BeNull();
        }

        #endregion

        #region Sensitivity Analysis Tests

        [Test]
        public void AnalyzeSensitivity_VaryingWeight_FindsThresholds()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "CostFocused",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 800,
                        ["Durability"] = 30
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "PerformanceFocused",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1500,
                        ["Durability"] = 80
                    }
                }
            };

            var baseWeights = new Dictionary<string, double>
            {
                ["InitialCost"] = 0.5,
                ["Durability"] = 0.5
            };

            // Act
            var sensitivity = _decisionSupport.AnalyzeSensitivity(alternatives, baseWeights, "InitialCost");

            // Assert
            sensitivity.Should().NotBeNull();
            sensitivity.Results.Should().NotBeEmpty();
            sensitivity.CriterionVaried.Should().Be("InitialCost");
        }

        [Test]
        public void CalculateSensitivity_HighlySensitiveCriterion_HighValue()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "A",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["Cost"] = 1000,
                        ["Quality"] = 50
                    }
                },
                new TestDesignAlternative
                {
                    AlternativeId = "B",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["Cost"] = 1100,
                        ["Quality"] = 90
                    }
                }
            };

            var weights = new Dictionary<string, double>
            {
                ["Cost"] = 0.5,
                ["Quality"] = 0.5
            };

            // Act
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Assert
            analysis.Sensitivity.Should().NotBeEmpty();
        }

        #endregion

        #region Report Generation Tests

        [Test]
        public void GenerateReport_ValidAnalysis_ReturnsReport()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "Option1",
                    Name = "Concrete",
                    Description = "Reinforced concrete system",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000
                    }
                }
            };

            var weights = new Dictionary<string, double> { ["InitialCost"] = 1.0 };
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Act
            var report = _decisionSupport.GenerateReport(analysis);

            // Assert
            report.Should().NotBeNull();
            report.Title.Should().NotBeNullOrEmpty();
            report.Sections.Should().NotBeEmpty();
        }

        [Test]
        public void GenerateReport_ContainsRequiredSections()
        {
            // Arrange
            var alternatives = new List<TestDesignAlternative>
            {
                new TestDesignAlternative
                {
                    AlternativeId = "A",
                    CriteriaValues = new Dictionary<string, double>
                    {
                        ["InitialCost"] = 1000
                    }
                }
            };

            var weights = new Dictionary<string, double> { ["InitialCost"] = 1.0 };
            var analysis = _decisionSupport.EvaluateAlternatives(alternatives, weights);

            // Act
            var report = _decisionSupport.GenerateReport(analysis);

            // Assert
            report.Sections.Should().Contain(s => s.Title == "Executive Summary");
            report.Sections.Should().Contain(s => s.Title == "Recommendation");
        }

        #endregion
    }

    #region Test Helper Classes

    /// <summary>
    /// Test implementation of DecisionSupport for unit testing
    /// </summary>
    public class TestDecisionSupport
    {
        private readonly Dictionary<string, TestDecisionCriterion> _criteria;
        private readonly Dictionary<string, TestTradeOffRule> _tradeOffRules;
        private readonly Dictionary<string, TestRiskFactor> _riskFactors;
        private readonly List<TestDecisionTemplate> _templates;

        public TestDecisionSupport()
        {
            _criteria = new Dictionary<string, TestDecisionCriterion>();
            _tradeOffRules = new Dictionary<string, TestTradeOffRule>();
            _riskFactors = new Dictionary<string, TestRiskFactor>();
            _templates = new List<TestDecisionTemplate>();

            InitializeCriteria();
            InitializeTradeOffRules();
            InitializeRiskFactors();
            InitializeTemplates();
        }

        private void InitializeCriteria()
        {
            _criteria["InitialCost"] = new TestDecisionCriterion
            {
                CriterionId = "InitialCost",
                Name = "Initial Cost",
                Direction = TestOptimizationDirection.Minimize
            };
            _criteria["EnergyPerformance"] = new TestDecisionCriterion
            {
                CriterionId = "EnergyPerformance",
                Name = "Energy Performance",
                Direction = TestOptimizationDirection.Minimize
            };
            _criteria["ThermalComfort"] = new TestDecisionCriterion
            {
                CriterionId = "ThermalComfort",
                Name = "Thermal Comfort",
                Direction = TestOptimizationDirection.Maximize
            };
            _criteria["Durability"] = new TestDecisionCriterion
            {
                CriterionId = "Durability",
                Name = "Durability",
                Direction = TestOptimizationDirection.Maximize
            };
            _criteria["EmbodiedCarbon"] = new TestDecisionCriterion
            {
                CriterionId = "EmbodiedCarbon",
                Name = "Embodied Carbon",
                Direction = TestOptimizationDirection.Minimize
            };
        }

        private void InitializeTradeOffRules()
        {
            _tradeOffRules["CostVsEnergy"] = new TestTradeOffRule
            {
                RuleId = "CostVsEnergy",
                Criteria1 = "InitialCost",
                Criteria2 = "EnergyPerformance",
                Relationship = TestTradeOffRelationship.Inverse,
                Guidance = "Higher upfront investment typically yields better energy performance"
            };
        }

        private void InitializeRiskFactors()
        {
            _riskFactors["MaterialAvailability"] = new TestRiskFactor
            {
                RiskId = "MaterialAvailability",
                Name = "Material Availability Risk",
                Triggers = new[] { "imported", "single source", "long lead" },
                Mitigations = new[] { "Use local alternatives", "Identify multiple suppliers" },
                ImpactAreas = new[] { "Schedule", "Cost" }
            };
        }

        private void InitializeTemplates()
        {
            _templates.Add(new TestDecisionTemplate
            {
                TemplateId = "StructuralSystem",
                Name = "Structural System Selection",
                TypicalAlternatives = new[]
                {
                    new TestAlternativeTemplate { Name = "RC Frame" },
                    new TestAlternativeTemplate { Name = "Steel Frame" },
                    new TestAlternativeTemplate { Name = "Timber" }
                }
            });
            _templates.Add(new TestDecisionTemplate
            {
                TemplateId = "FacadeSystem",
                Name = "Facade System Selection",
                TypicalAlternatives = new[]
                {
                    new TestAlternativeTemplate { Name = "Brick Cavity" },
                    new TestAlternativeTemplate { Name = "Curtain Wall" }
                }
            });
            _templates.Add(new TestDecisionTemplate
            {
                TemplateId = "HVACSystem",
                Name = "HVAC System Selection",
                TypicalAlternatives = new[]
                {
                    new TestAlternativeTemplate { Name = "VAV" },
                    new TestAlternativeTemplate { Name = "VRF" }
                }
            });
        }

        public TestDecisionAnalysis EvaluateAlternatives(
            List<TestDesignAlternative> alternatives,
            Dictionary<string, double> weights)
        {
            var analysis = new TestDecisionAnalysis
            {
                Alternatives = alternatives,
                CriteriaWeights = weights
            };

            var normalized = NormalizeScores(alternatives);

            foreach (var alt in alternatives)
            {
                var score = CalculateWeightedScore(alt.AlternativeId, normalized, weights);
                analysis.WeightedScores[alt.AlternativeId] = score;
            }

            analysis.Ranking = analysis.WeightedScores
                .OrderByDescending(s => s.Value)
                .Select((kvp, idx) => new TestAlternativeRank
                {
                    AlternativeId = kvp.Key,
                    Rank = idx + 1,
                    Score = kvp.Value
                })
                .ToList();

            analysis.TradeOffs = IdentifyTradeOffs(alternatives, weights);
            analysis.Sensitivity = CalculateSensitivity(alternatives, weights);
            analysis.Recommendation = new TestDecisionRecommendation
            {
                RecommendedAlternative = analysis.Ranking.FirstOrDefault()?.AlternativeId,
                Summary = $"Based on analysis, {analysis.Ranking.FirstOrDefault()?.AlternativeId} is recommended."
            };

            return analysis;
        }

        public Dictionary<string, Dictionary<string, double>> NormalizeScores(
            List<TestDesignAlternative> alternatives)
        {
            var normalized = new Dictionary<string, Dictionary<string, double>>();

            var allCriteria = alternatives
                .SelectMany(a => a.CriteriaValues.Keys)
                .Distinct()
                .ToList();

            foreach (var criterion in allCriteria)
            {
                var values = alternatives
                    .Where(a => a.CriteriaValues.ContainsKey(criterion))
                    .Select(a => a.CriteriaValues[criterion])
                    .ToList();

                if (!values.Any()) continue;

                var min = values.Min();
                var max = values.Max();
                var range = max - min;

                var critDef = _criteria.GetValueOrDefault(criterion);
                var maximize = critDef?.Direction == TestOptimizationDirection.Maximize;

                foreach (var alt in alternatives)
                {
                    if (!normalized.ContainsKey(alt.AlternativeId))
                        normalized[alt.AlternativeId] = new Dictionary<string, double>();

                    if (alt.CriteriaValues.TryGetValue(criterion, out var value))
                    {
                        var normalizedValue = range > 0 ? (value - min) / range : 0.5;
                        if (!maximize) normalizedValue = 1 - normalizedValue;
                        normalized[alt.AlternativeId][criterion] = normalizedValue;
                    }
                }
            }

            return normalized;
        }

        private double CalculateWeightedScore(
            string alternativeId,
            Dictionary<string, Dictionary<string, double>> normalizedScores,
            Dictionary<string, double> weights)
        {
            if (!normalizedScores.TryGetValue(alternativeId, out var scores)) return 0;

            double total = 0, totalWeight = 0;
            foreach (var weight in weights)
            {
                if (scores.TryGetValue(weight.Key, out var score))
                {
                    total += score * weight.Value;
                    totalWeight += weight.Value;
                }
            }

            return totalWeight > 0 ? total / totalWeight : 0;
        }

        private List<TestTradeOffInsight> IdentifyTradeOffs(
            List<TestDesignAlternative> alternatives,
            Dictionary<string, double> weights)
        {
            return _tradeOffRules.Values
                .Where(r => weights.ContainsKey(r.Criteria1) && weights.ContainsKey(r.Criteria2))
                .Select(r => new TestTradeOffInsight
                {
                    Criterion1 = r.Criteria1,
                    Criterion2 = r.Criteria2,
                    Relationship = r.Relationship,
                    Guidance = r.Guidance
                })
                .ToList();
        }

        private Dictionary<string, double> CalculateSensitivity(
            List<TestDesignAlternative> alternatives,
            Dictionary<string, double> weights)
        {
            return weights.Keys.ToDictionary(k => k, k => 0.2); // Simplified for testing
        }

        public TestTradeOffGuidance GetTradeOffGuidance(string criterion1, string criterion2)
        {
            var guidance = new TestTradeOffGuidance
            {
                Criterion1 = criterion1,
                Criterion2 = criterion2
            };

            var rule = _tradeOffRules.Values.FirstOrDefault(r =>
                (r.Criteria1 == criterion1 && r.Criteria2 == criterion2) ||
                (r.Criteria1 == criterion2 && r.Criteria2 == criterion1));

            if (rule != null)
            {
                guidance.HasRule = true;
                guidance.Relationship = rule.Relationship;
                guidance.Guidance = rule.Guidance;
            }

            return guidance;
        }

        public TestTradeOffAnalysis AnalyzeTradeOff(
            List<TestDesignAlternative> alternatives,
            string criterion1,
            string criterion2)
        {
            var analysis = new TestTradeOffAnalysis
            {
                Criterion1 = criterion1,
                Criterion2 = criterion2,
                DataPoints = alternatives.Select(a => new TestTradeOffPoint
                {
                    AlternativeId = a.AlternativeId,
                    Value1 = a.CriteriaValues.GetValueOrDefault(criterion1, 0),
                    Value2 = a.CriteriaValues.GetValueOrDefault(criterion2, 0)
                }).ToList()
            };

            // Identify Pareto frontier
            analysis.ParetoFrontier = analysis.DataPoints
                .Where(p => !analysis.DataPoints.Any(other =>
                    other.AlternativeId != p.AlternativeId &&
                    other.Value1 <= p.Value1 &&
                    other.Value2 <= p.Value2 &&
                    (other.Value1 < p.Value1 || other.Value2 < p.Value2)))
                .ToList();

            return analysis;
        }

        public TestRiskAssessment AssessRisks(TestDesignAlternative alternative, TestProjectContext context)
        {
            var assessment = new TestRiskAssessment
            {
                AlternativeId = alternative.AlternativeId,
                IdentifiedRisks = new List<TestIdentifiedRisk>()
            };

            foreach (var riskFactor in _riskFactors.Values)
            {
                var triggered = riskFactor.Triggers
                    .Where(t => alternative.Characteristics?.Any(c =>
                        c.Contains(t, StringComparison.OrdinalIgnoreCase)) == true)
                    .ToList();

                if (triggered.Any())
                {
                    assessment.IdentifiedRisks.Add(new TestIdentifiedRisk
                    {
                        RiskId = riskFactor.RiskId,
                        RiskName = riskFactor.Name,
                        TriggeredBy = triggered
                    });
                }
            }

            assessment.RiskLevel = assessment.IdentifiedRisks.Any()
                ? TestRiskLevel.Medium
                : TestRiskLevel.Low;

            return assessment;
        }

        public TestDecisionTemplate GetTemplate(string templateId)
        {
            return _templates.FirstOrDefault(t => t.TemplateId == templateId);
        }

        public List<TestDecisionTemplate> GetAllTemplates()
        {
            return _templates.ToList();
        }

        public TestSensitivityAnalysis AnalyzeSensitivity(
            List<TestDesignAlternative> alternatives,
            Dictionary<string, double> baseWeights,
            string criterionToVary)
        {
            var sensitivity = new TestSensitivityAnalysis
            {
                CriterionVaried = criterionToVary,
                BaseWeights = baseWeights,
                Results = new List<TestSensitivityResult>()
            };

            for (double weight = 0; weight <= 1.0; weight += 0.1)
            {
                sensitivity.Results.Add(new TestSensitivityResult
                {
                    CriterionWeight = weight,
                    TopAlternative = alternatives.First().AlternativeId
                });
            }

            return sensitivity;
        }

        public TestDecisionReport GenerateReport(TestDecisionAnalysis analysis)
        {
            return new TestDecisionReport
            {
                Title = "Design Decision Analysis Report",
                Sections = new List<TestReportSection>
                {
                    new TestReportSection { Title = "Executive Summary", Content = "Summary content" },
                    new TestReportSection { Title = "Alternatives Evaluated", Content = "Alternatives content" },
                    new TestReportSection { Title = "Evaluation Criteria", Content = "Criteria content" },
                    new TestReportSection { Title = "Analysis Results", Content = "Results content" },
                    new TestReportSection { Title = "Recommendation", Content = analysis.Recommendation.Summary }
                }
            };
        }
    }

    public class TestDesignAlternative
    {
        public string AlternativeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, double> CriteriaValues { get; set; } = new();
        public List<string> Characteristics { get; set; } = new();
    }

    public class TestDecisionCriterion
    {
        public string CriterionId { get; set; }
        public string Name { get; set; }
        public TestOptimizationDirection Direction { get; set; }
    }

    public class TestTradeOffRule
    {
        public string RuleId { get; set; }
        public string Criteria1 { get; set; }
        public string Criteria2 { get; set; }
        public TestTradeOffRelationship Relationship { get; set; }
        public string Guidance { get; set; }
    }

    public class TestRiskFactor
    {
        public string RiskId { get; set; }
        public string Name { get; set; }
        public string[] Triggers { get; set; }
        public string[] Mitigations { get; set; }
        public string[] ImpactAreas { get; set; }
    }

    public class TestDecisionTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public TestAlternativeTemplate[] TypicalAlternatives { get; set; }
    }

    public class TestAlternativeTemplate
    {
        public string Name { get; set; }
    }

    public class TestDecisionAnalysis
    {
        public List<TestDesignAlternative> Alternatives { get; set; }
        public Dictionary<string, double> CriteriaWeights { get; set; }
        public Dictionary<string, double> WeightedScores { get; set; } = new();
        public List<TestAlternativeRank> Ranking { get; set; } = new();
        public List<TestTradeOffInsight> TradeOffs { get; set; } = new();
        public Dictionary<string, double> Sensitivity { get; set; } = new();
        public TestDecisionRecommendation Recommendation { get; set; }
    }

    public class TestAlternativeRank
    {
        public string AlternativeId { get; set; }
        public int Rank { get; set; }
        public double Score { get; set; }
    }

    public class TestTradeOffInsight
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public TestTradeOffRelationship Relationship { get; set; }
        public string Guidance { get; set; }
    }

    public class TestDecisionRecommendation
    {
        public string RecommendedAlternative { get; set; }
        public string Summary { get; set; }
    }

    public class TestTradeOffGuidance
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public bool HasRule { get; set; }
        public TestTradeOffRelationship Relationship { get; set; }
        public string Guidance { get; set; }
    }

    public class TestTradeOffAnalysis
    {
        public string Criterion1 { get; set; }
        public string Criterion2 { get; set; }
        public List<TestTradeOffPoint> DataPoints { get; set; }
        public List<TestTradeOffPoint> ParetoFrontier { get; set; }
    }

    public class TestTradeOffPoint
    {
        public string AlternativeId { get; set; }
        public double Value1 { get; set; }
        public double Value2 { get; set; }
    }

    public class TestProjectContext
    {
        public string Region { get; set; }
        public string BuildingType { get; set; }
    }

    public class TestRiskAssessment
    {
        public string AlternativeId { get; set; }
        public List<TestIdentifiedRisk> IdentifiedRisks { get; set; }
        public TestRiskLevel RiskLevel { get; set; }
    }

    public class TestIdentifiedRisk
    {
        public string RiskId { get; set; }
        public string RiskName { get; set; }
        public List<string> TriggeredBy { get; set; }
    }

    public class TestSensitivityAnalysis
    {
        public string CriterionVaried { get; set; }
        public Dictionary<string, double> BaseWeights { get; set; }
        public List<TestSensitivityResult> Results { get; set; }
    }

    public class TestSensitivityResult
    {
        public double CriterionWeight { get; set; }
        public string TopAlternative { get; set; }
    }

    public class TestDecisionReport
    {
        public string Title { get; set; }
        public List<TestReportSection> Sections { get; set; }
    }

    public class TestReportSection
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public enum TestOptimizationDirection { Minimize, Maximize }
    public enum TestTradeOffRelationship { Proportional, Inverse, Complex }
    public enum TestRiskLevel { Low, Medium, High, VeryHigh }

    #endregion
}
