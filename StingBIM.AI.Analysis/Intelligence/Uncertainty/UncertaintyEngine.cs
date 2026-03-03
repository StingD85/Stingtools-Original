// StingBIM.AI.Intelligence.Uncertainty.UncertaintyEngine
// Handles uncertainty and confidence scoring in design decisions
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Enhancement

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Intelligence.Uncertainty
{
    #region Uncertainty Engine

    /// <summary>
    /// Manages uncertainty in design analysis and provides confidence scores.
    /// Enables the AI to express "probably" rather than just "yes/no".
    /// </summary>
    public class UncertaintyEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, UncertaintyModel> _models;
        private readonly EvidenceAccumulator _evidenceAccumulator;
        private readonly ConfidenceCalibrator _calibrator;

        public UncertaintyEngine()
        {
            _models = new Dictionary<string, UncertaintyModel>();
            _evidenceAccumulator = new EvidenceAccumulator();
            _calibrator = new ConfidenceCalibrator();

            InitializeModels();
        }

        /// <summary>
        /// Assesses uncertainty in a property or classification.
        /// </summary>
        public UncertaintyAssessment Assess(string propertyType, object value, AssessmentContext context)
        {
            var assessment = new UncertaintyAssessment
            {
                PropertyType = propertyType,
                AssessedValue = value,
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // Get appropriate model
            if (!_models.TryGetValue(propertyType, out var model))
            {
                model = _models["Default"];
            }

            // Calculate base confidence from model
            var baseConfidence = model.CalculateConfidence(value, context);
            assessment.BaseConfidence = baseConfidence;

            // Gather evidence
            var evidence = _evidenceAccumulator.GatherEvidence(propertyType, value, context);
            assessment.Evidence = evidence;

            // Adjust confidence based on evidence
            var adjustedConfidence = AdjustConfidenceWithEvidence(baseConfidence, evidence);
            assessment.AdjustedConfidence = adjustedConfidence;

            // Generate probability distribution
            assessment.Distribution = GenerateDistribution(propertyType, value, adjustedConfidence, context);

            // Generate uncertainty explanation
            assessment.Explanation = GenerateExplanation(assessment);

            // Calibrate final confidence
            assessment.CalibratedConfidence = _calibrator.Calibrate(adjustedConfidence, propertyType);

            Logger.Trace($"Assessed {propertyType}: confidence {assessment.CalibratedConfidence:P0}");

            return assessment;
        }

        /// <summary>
        /// Combines multiple uncertain assessments.
        /// </summary>
        public CombinedAssessment CombineAssessments(List<UncertaintyAssessment> assessments, CombinationMethod method)
        {
            var combined = new CombinedAssessment
            {
                SourceAssessments = assessments,
                Method = method
            };

            switch (method)
            {
                case CombinationMethod.WeightedAverage:
                    combined.CombinedConfidence = assessments.Average(a => a.CalibratedConfidence);
                    break;

                case CombinationMethod.Minimum:
                    combined.CombinedConfidence = assessments.Min(a => a.CalibratedConfidence);
                    break;

                case CombinationMethod.Maximum:
                    combined.CombinedConfidence = assessments.Max(a => a.CalibratedConfidence);
                    break;

                case CombinationMethod.Bayesian:
                    combined.CombinedConfidence = CombineBayesian(assessments);
                    break;

                case CombinationMethod.DempsterShafer:
                    combined.CombinedConfidence = CombineDempsterShafer(assessments);
                    break;
            }

            combined.OverallUncertainty = 1 - combined.CombinedConfidence;
            combined.Explanation = $"Combined {assessments.Count} assessments using {method}";

            return combined;
        }

        /// <summary>
        /// Propagates uncertainty through a calculation.
        /// </summary>
        public UncertaintyPropagation PropagateUncertainty(
            Func<double[], double> calculation,
            List<UncertainValue> inputs)
        {
            var propagation = new UncertaintyPropagation
            {
                Inputs = inputs,
                Calculation = calculation.Method.Name
            };

            // Monte Carlo simulation for uncertainty propagation
            var samples = 1000;
            var results = new List<double>();
            var random = new Random(42);

            for (int i = 0; i < samples; i++)
            {
                var sampledInputs = inputs.Select(inp => SampleFromDistribution(inp, random)).ToArray();
                var result = calculation(sampledInputs);
                results.Add(result);
            }

            // Calculate output statistics
            propagation.MeanResult = results.Average();
            propagation.StandardDeviation = Math.Sqrt(results.Average(r => Math.Pow(r - propagation.MeanResult, 2)));
            propagation.Confidence95Low = results.OrderBy(r => r).ElementAt((int)(samples * 0.025));
            propagation.Confidence95High = results.OrderBy(r => r).ElementAt((int)(samples * 0.975));

            // Calculate output uncertainty
            propagation.OutputUncertainty = propagation.StandardDeviation / Math.Abs(propagation.MeanResult);

            return propagation;
        }

        /// <summary>
        /// Assesses structural role of an element with uncertainty.
        /// </summary>
        public StructuralAssessment AssessStructuralRole(ElementInfo element)
        {
            var assessment = new StructuralAssessment
            {
                ElementId = element.ElementId,
                ElementCategory = element.Category
            };

            var evidencePoints = new List<EvidencePoint>();

            // Evidence: Wall thickness
            if (element.Parameters.TryGetValue("Thickness", out var thickness))
            {
                var t = Convert.ToDouble(thickness);
                if (t >= 200) // 200mm+ suggests load-bearing
                {
                    evidencePoints.Add(new EvidencePoint
                    {
                        Factor = "Thickness",
                        Value = t,
                        SupportsLoadBearing = true,
                        Strength = Math.Min((t - 100) / 200, 1.0)
                    });
                }
                else
                {
                    evidencePoints.Add(new EvidencePoint
                    {
                        Factor = "Thickness",
                        Value = t,
                        SupportsLoadBearing = false,
                        Strength = (200 - t) / 200
                    });
                }
            }

            // Evidence: Material
            if (element.Parameters.TryGetValue("Material", out var material))
            {
                var mat = material?.ToString() ?? "";
                var loadBearingMaterials = new[] { "Concrete", "Masonry", "Steel", "CMU" };
                var isLoadBearing = loadBearingMaterials.Any(m => mat.Contains(m));

                evidencePoints.Add(new EvidencePoint
                {
                    Factor = "Material",
                    Value = mat,
                    SupportsLoadBearing = isLoadBearing,
                    Strength = isLoadBearing ? 0.8 : 0.3
                });
            }

            // Evidence: Location (exterior walls more likely load-bearing)
            if (element.Parameters.TryGetValue("IsExterior", out var isExterior))
            {
                var exterior = Convert.ToBoolean(isExterior);
                evidencePoints.Add(new EvidencePoint
                {
                    Factor = "Location",
                    Value = exterior ? "Exterior" : "Interior",
                    SupportsLoadBearing = exterior,
                    Strength = exterior ? 0.7 : 0.4
                });
            }

            // Evidence: Continuity (does it continue through multiple floors?)
            if (element.Parameters.TryGetValue("BaseLevel", out var baseLevel) &&
                element.Parameters.TryGetValue("TopLevel", out var topLevel))
            {
                var multiFloor = baseLevel?.ToString() != topLevel?.ToString();
                evidencePoints.Add(new EvidencePoint
                {
                    Factor = "Continuity",
                    Value = multiFloor ? "Multi-floor" : "Single floor",
                    SupportsLoadBearing = multiFloor,
                    Strength = multiFloor ? 0.6 : 0.3
                });
            }

            // Calculate probabilities
            var loadBearingEvidence = evidencePoints.Where(e => e.SupportsLoadBearing).Sum(e => e.Strength);
            var partitionEvidence = evidencePoints.Where(e => !e.SupportsLoadBearing).Sum(e => e.Strength);
            var totalEvidence = loadBearingEvidence + partitionEvidence;

            assessment.LoadBearingProbability = totalEvidence > 0
                ? (float)(loadBearingEvidence / totalEvidence)
                : 0.5f;
            assessment.PartitionProbability = 1 - assessment.LoadBearingProbability;
            assessment.Evidence = evidencePoints;

            // Determine classification with uncertainty
            if (assessment.LoadBearingProbability > 0.7f)
            {
                assessment.Classification = "Load-Bearing";
                assessment.Confidence = assessment.LoadBearingProbability;
            }
            else if (assessment.LoadBearingProbability < 0.3f)
            {
                assessment.Classification = "Partition";
                assessment.Confidence = assessment.PartitionProbability;
            }
            else
            {
                assessment.Classification = "Uncertain";
                assessment.Confidence = 0.5f;
                assessment.RequiresVerification = true;
            }

            assessment.Explanation = GenerateStructuralExplanation(assessment);

            return assessment;
        }

        /// <summary>
        /// Updates confidence based on feedback.
        /// </summary>
        public void RecordFeedback(string propertyType, float predictedConfidence, bool wasCorrect)
        {
            _calibrator.RecordOutcome(propertyType, predictedConfidence, wasCorrect);
        }

        private float AdjustConfidenceWithEvidence(float baseConfidence, List<Evidence> evidence)
        {
            if (!evidence.Any())
                return baseConfidence;

            // Bayesian update with evidence
            var adjustment = evidence.Sum(e => e.Strength * (e.Supports ? 0.1f : -0.1f));
            return Math.Max(0.1f, Math.Min(0.99f, baseConfidence + adjustment));
        }

        private ProbabilityDistribution GenerateDistribution(
            string propertyType,
            object value,
            float confidence,
            AssessmentContext context)
        {
            // Generate a simple distribution based on confidence
            var distribution = new ProbabilityDistribution
            {
                Type = DistributionType.Normal
            };

            if (value is double numericValue)
            {
                distribution.Mean = numericValue;
                distribution.StandardDeviation = numericValue * (1 - confidence) * 0.2;
                distribution.Confidence95Range = (
                    numericValue - 1.96 * distribution.StandardDeviation,
                    numericValue + 1.96 * distribution.StandardDeviation
                );
            }

            return distribution;
        }

        private string GenerateExplanation(UncertaintyAssessment assessment)
        {
            var confidence = assessment.CalibratedConfidence;
            var qualifier = confidence switch
            {
                >= 0.9f => "highly confident",
                >= 0.7f => "reasonably confident",
                >= 0.5f => "moderately confident",
                >= 0.3f => "somewhat uncertain",
                _ => "uncertain"
            };

            var evidenceSummary = assessment.Evidence.Any()
                ? $" Based on {assessment.Evidence.Count} evidence points."
                : "";

            return $"I am {qualifier} ({confidence:P0}) in this assessment.{evidenceSummary}";
        }

        private float CombineBayesian(List<UncertaintyAssessment> assessments)
        {
            // Simple Bayesian combination assuming independence
            var logOdds = assessments.Sum(a =>
            {
                var p = Math.Max(0.01, Math.Min(0.99, a.CalibratedConfidence));
                return Math.Log(p / (1 - p));
            });

            return (float)(1 / (1 + Math.Exp(-logOdds)));
        }

        private float CombineDempsterShafer(List<UncertaintyAssessment> assessments)
        {
            // Simplified Dempster-Shafer combination
            var belief = assessments.Aggregate(0.5,
                (current, a) => current * a.CalibratedConfidence /
                    (current * a.CalibratedConfidence + (1 - current) * (1 - a.CalibratedConfidence)));

            return (float)belief;
        }

        private double SampleFromDistribution(UncertainValue input, Random random)
        {
            // Sample from normal distribution using Box-Muller transform
            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var stdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            return input.Value + stdNormal * input.Uncertainty;
        }

        private string GenerateStructuralExplanation(StructuralAssessment assessment)
        {
            var parts = new List<string>();

            parts.Add($"Classification: {assessment.Classification} ({assessment.Confidence:P0} confidence)");

            var strongEvidence = assessment.Evidence
                .Where(e => e.Strength > 0.5)
                .OrderByDescending(e => e.Strength);

            foreach (var evidence in strongEvidence.Take(2))
            {
                var direction = evidence.SupportsLoadBearing ? "suggests load-bearing" : "suggests partition";
                parts.Add($"{evidence.Factor} ({evidence.Value}) {direction}");
            }

            if (assessment.RequiresVerification)
            {
                parts.Add("Recommend structural engineer verification");
            }

            return string.Join(". ", parts);
        }

        private void InitializeModels()
        {
            _models["Default"] = new DefaultUncertaintyModel();
            _models["StructuralRole"] = new StructuralRoleModel();
            _models["MaterialProperty"] = new MaterialPropertyModel();
            _models["SpatialRelationship"] = new SpatialRelationshipModel();
        }
    }

    #endregion

    #region Evidence Accumulator

    /// <summary>
    /// Gathers evidence to support or refute assessments.
    /// </summary>
    public class EvidenceAccumulator
    {
        public List<Evidence> GatherEvidence(string propertyType, object value, AssessmentContext context)
        {
            var evidence = new List<Evidence>();

            // Context-based evidence
            if (context?.SimilarElements?.Any() == true)
            {
                var similarValues = context.SimilarElements
                    .Select(e => e.Parameters?.GetValueOrDefault(propertyType))
                    .Where(v => v != null)
                    .ToList();

                if (similarValues.Any())
                {
                    var consistency = CalculateConsistency(value, similarValues);
                    evidence.Add(new Evidence
                    {
                        Source = "Similar Elements",
                        Description = $"Compared with {similarValues.Count} similar elements",
                        Supports = consistency > 0.7,
                        Strength = (float)consistency
                    });
                }
            }

            // Historical evidence
            if (context?.HistoricalData?.Any() == true)
            {
                evidence.Add(new Evidence
                {
                    Source = "Historical Data",
                    Description = "Based on historical project data",
                    Supports = true,
                    Strength = 0.5f
                });
            }

            return evidence;
        }

        private double CalculateConsistency(object value, List<object> comparisons)
        {
            if (value is double numericValue)
            {
                var numericComparisons = comparisons
                    .Where(c => c is double)
                    .Cast<double>()
                    .ToList();

                if (!numericComparisons.Any()) return 0.5;

                var mean = numericComparisons.Average();
                var stdDev = Math.Sqrt(numericComparisons.Average(v => Math.Pow(v - mean, 2)));

                if (stdDev < 0.001) return numericValue == mean ? 1.0 : 0.0;

                var zScore = Math.Abs((numericValue - mean) / stdDev);
                return Math.Max(0, 1 - zScore / 3); // 3 sigma rule
            }

            return comparisons.Contains(value) ? 0.8 : 0.3;
        }
    }

    #endregion

    #region Confidence Calibrator

    /// <summary>
    /// Calibrates confidence scores based on historical accuracy.
    /// </summary>
    public class ConfidenceCalibrator
    {
        private readonly Dictionary<string, List<CalibrationPoint>> _history;

        public ConfidenceCalibrator()
        {
            _history = new Dictionary<string, List<CalibrationPoint>>();
        }

        public float Calibrate(float rawConfidence, string propertyType)
        {
            if (!_history.TryGetValue(propertyType, out var points) || points.Count < 10)
            {
                return rawConfidence; // Not enough data to calibrate
            }

            // Find historical accuracy at this confidence level
            var bucket = (int)(rawConfidence * 10) / 10.0f;
            var relevantPoints = points.Where(p => Math.Abs(p.PredictedConfidence - bucket) < 0.1).ToList();

            if (relevantPoints.Count < 5)
            {
                return rawConfidence;
            }

            var actualAccuracy = relevantPoints.Count(p => p.WasCorrect) / (float)relevantPoints.Count;

            // Blend raw confidence with historical accuracy
            return rawConfidence * 0.7f + actualAccuracy * 0.3f;
        }

        public void RecordOutcome(string propertyType, float predictedConfidence, bool wasCorrect)
        {
            if (!_history.ContainsKey(propertyType))
            {
                _history[propertyType] = new List<CalibrationPoint>();
            }

            _history[propertyType].Add(new CalibrationPoint
            {
                PredictedConfidence = predictedConfidence,
                WasCorrect = wasCorrect,
                Timestamp = DateTime.UtcNow
            });

            // Keep only recent history
            if (_history[propertyType].Count > 1000)
            {
                _history[propertyType] = _history[propertyType].TakeLast(1000).ToList();
            }
        }

        private class CalibrationPoint
        {
            public float PredictedConfidence { get; set; }
            public bool WasCorrect { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }

    #endregion

    #region Uncertainty Models

    public abstract class UncertaintyModel
    {
        public abstract float CalculateConfidence(object value, AssessmentContext context);
    }

    public class DefaultUncertaintyModel : UncertaintyModel
    {
        public override float CalculateConfidence(object value, AssessmentContext context)
        {
            var confidence = 0.5f;

            // Value presence and type contribute to confidence
            if (value != null)
            {
                confidence += 0.1f;

                // Numeric values from calculations are more reliable
                if (value is double or float or int or decimal)
                    confidence += 0.05f;
            }

            // Context richness boosts confidence
            if (context?.SimilarElements?.Any() == true)
            {
                var count = context.SimilarElements.Count;
                confidence += Math.Min(0.2f, count * 0.04f);
            }

            if (context?.HistoricalData?.Any() == true)
                confidence += 0.1f;

            if (context?.FromDatabase == true)
                confidence += 0.15f;

            // Properties map provides extra evidence
            if (context?.Properties?.Any() == true)
                confidence += Math.Min(0.1f, context.Properties.Count * 0.02f);

            return Math.Max(0.1f, Math.Min(0.95f, confidence));
        }
    }

    public class StructuralRoleModel : UncertaintyModel
    {
        public override float CalculateConfidence(object value, AssessmentContext context)
        {
            var confidence = 0.4f;

            // Check how many key structural indicators are available
            var indicatorCount = 0;
            var properties = context?.Properties;

            if (properties != null)
            {
                if (properties.ContainsKey("Thickness")) indicatorCount++;
                if (properties.ContainsKey("Material")) indicatorCount++;
                if (properties.ContainsKey("IsExterior")) indicatorCount++;
                if (properties.ContainsKey("BaseLevel") && properties.ContainsKey("TopLevel")) indicatorCount++;
                if (properties.ContainsKey("StructuralUsage")) indicatorCount++;

                // More indicators = higher confidence in assessment
                confidence += indicatorCount * 0.08f;
            }

            // Similar elements provide comparative evidence
            if (context?.SimilarElements?.Any() == true)
            {
                var count = context.SimilarElements.Count;
                confidence += Math.Min(0.15f, count * 0.03f);
            }

            if (context?.HistoricalData?.Any() == true)
                confidence += 0.1f;

            return Math.Max(0.2f, Math.Min(0.95f, confidence));
        }
    }

    public class MaterialPropertyModel : UncertaintyModel
    {
        public override float CalculateConfidence(object value, AssessmentContext context)
        {
            var confidence = 0.5f;

            // Database-sourced values have high confidence
            if (context?.FromDatabase == true)
                confidence = 0.9f;

            // Value type affects confidence: measured numeric > categorical > null
            if (value is double d && !double.IsNaN(d))
                confidence += 0.05f;
            else if (value is string s && !string.IsNullOrEmpty(s))
                confidence += 0.02f;
            else if (value == null)
                confidence -= 0.1f;

            // Consistency with similar elements boosts confidence
            if (context?.SimilarElements != null)
            {
                var similarCount = context.SimilarElements.Count;
                if (similarCount >= 5)
                    confidence += 0.1f;
                else if (similarCount >= 2)
                    confidence += 0.05f;
            }

            // Known properties context
            if (context?.Properties != null)
            {
                // More measured properties = better characterized material
                confidence += Math.Min(0.1f, context.Properties.Count * 0.015f);
            }

            return Math.Max(0.15f, Math.Min(0.98f, confidence));
        }
    }

    public class SpatialRelationshipModel : UncertaintyModel
    {
        public override float CalculateConfidence(object value, AssessmentContext context)
        {
            var confidence = 0.7f;

            // Spatial calculations from precise coordinates are highly reliable
            if (value is double d && !double.IsNaN(d) && !double.IsInfinity(d))
                confidence += 0.1f;

            // If we have the actual geometric data, confidence is high
            if (context?.Properties != null)
            {
                var hasCoordinates = context.Properties.ContainsKey("X") ||
                                     context.Properties.ContainsKey("Location");
                var hasDimensions = context.Properties.ContainsKey("Width") ||
                                    context.Properties.ContainsKey("Length");

                if (hasCoordinates) confidence += 0.1f;
                if (hasDimensions) confidence += 0.05f;
            }

            // Inferred spatial relationships (no direct measurement) are less certain
            if (context?.FromDatabase == false && context?.SimilarElements == null)
                confidence -= 0.15f;

            return Math.Max(0.3f, Math.Min(0.98f, confidence));
        }
    }

    #endregion

    #region Types

    public class UncertaintyAssessment
    {
        public string PropertyType { get; set; }
        public object AssessedValue { get; set; }
        public AssessmentContext Context { get; set; }
        public DateTime Timestamp { get; set; }

        public float BaseConfidence { get; set; }
        public float AdjustedConfidence { get; set; }
        public float CalibratedConfidence { get; set; }

        public List<Evidence> Evidence { get; set; }
        public ProbabilityDistribution Distribution { get; set; }
        public string Explanation { get; set; }
    }

    public class AssessmentContext
    {
        public List<ElementInfo> SimilarElements { get; set; }
        public List<object> HistoricalData { get; set; }
        public bool FromDatabase { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class ElementInfo
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class Evidence
    {
        public string Source { get; set; }
        public string Description { get; set; }
        public bool Supports { get; set; }
        public float Strength { get; set; }
    }

    public class ProbabilityDistribution
    {
        public DistributionType Type { get; set; }
        public double Mean { get; set; }
        public double StandardDeviation { get; set; }
        public (double Low, double High) Confidence95Range { get; set; }
    }

    public enum DistributionType
    {
        Normal,
        Uniform,
        Triangular,
        Beta
    }

    public class CombinedAssessment
    {
        public List<UncertaintyAssessment> SourceAssessments { get; set; }
        public CombinationMethod Method { get; set; }
        public float CombinedConfidence { get; set; }
        public float OverallUncertainty { get; set; }
        public string Explanation { get; set; }
    }

    public enum CombinationMethod
    {
        WeightedAverage,
        Minimum,
        Maximum,
        Bayesian,
        DempsterShafer
    }

    public class UncertainValue
    {
        public double Value { get; set; }
        public double Uncertainty { get; set; }
    }

    public class UncertaintyPropagation
    {
        public List<UncertainValue> Inputs { get; set; }
        public string Calculation { get; set; }
        public double MeanResult { get; set; }
        public double StandardDeviation { get; set; }
        public double Confidence95Low { get; set; }
        public double Confidence95High { get; set; }
        public double OutputUncertainty { get; set; }
    }

    public class StructuralAssessment
    {
        public string ElementId { get; set; }
        public string ElementCategory { get; set; }
        public string Classification { get; set; }
        public float Confidence { get; set; }
        public float LoadBearingProbability { get; set; }
        public float PartitionProbability { get; set; }
        public List<EvidencePoint> Evidence { get; set; }
        public bool RequiresVerification { get; set; }
        public string Explanation { get; set; }
    }

    public class EvidencePoint
    {
        public string Factor { get; set; }
        public object Value { get; set; }
        public bool SupportsLoadBearing { get; set; }
        public double Strength { get; set; }
    }

    #endregion
}
