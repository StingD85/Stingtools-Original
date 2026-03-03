using System;
using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Intelligence.Physics;
using StingBIM.AI.Intelligence.Uncertainty;

namespace StingBIM.AI.Tests.Unit.Analysis
{
    [TestFixture]
    public class ReasoningEngineTests
    {
        #region AcousticModel Tests

        [TestFixture]
        public class AcousticModelTests
        {
            private AcousticModel _model;

            [SetUp]
            public void SetUp()
            {
                _model = new AcousticModel();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _model.Should().NotBeNull();
            }

            [Test]
            public void CalculateRT60_SabineFormula_ReturnsCorrectValue()
            {
                // Sabine formula: RT60 = 0.161 × V / A
                double volume = 200.0; // m³
                var absorption = new Dictionary<int, double>
                {
                    { 125, 15.0 },
                    { 250, 18.0 },
                    { 500, 22.0 },
                    { 1000, 25.0 },
                    { 2000, 28.0 },
                    { 4000, 30.0 }
                };

                var rt60 = _model.CalculateRT60(volume, absorption);

                rt60.Should().NotBeEmpty();
                // RT60 at 500Hz: 0.161 × 200 / 22 ≈ 1.46s
                rt60[500].Should().BeApproximately(0.161 * 200 / 22, 0.1);
            }

            [Test]
            public void CalculateRT60_LargerVolume_LongerReverberation()
            {
                var absorption = new Dictionary<int, double>
                {
                    { 500, 20.0 },
                    { 1000, 25.0 }
                };

                var rt60Small = _model.CalculateRT60(100.0, absorption);
                var rt60Large = _model.CalculateRT60(300.0, absorption);

                rt60Large[500].Should().BeGreaterThan(rt60Small[500],
                    "larger volume should produce longer reverberation");
            }

            [Test]
            public void CalculateRT60_MoreAbsorption_ShorterReverberation()
            {
                double volume = 200.0;
                var lowAbsorption = new Dictionary<int, double> { { 500, 10.0 } };
                var highAbsorption = new Dictionary<int, double> { { 500, 40.0 } };

                var rt60Low = _model.CalculateRT60(volume, lowAbsorption);
                var rt60High = _model.CalculateRT60(volume, highAbsorption);

                rt60High[500].Should().BeLessThan(rt60Low[500],
                    "more absorption should produce shorter reverberation");
            }

            [Test]
            public void CalculateRT60Eyring_HighAbsorption_LessThanSabine()
            {
                double volume = 200.0;
                double surfaceArea = 250.0;
                double averageAlpha = 0.5; // high absorption

                var eyringRT60 = _model.CalculateRT60Eyring(volume, surfaceArea, averageAlpha);

                // Eyring gives lower RT60 than Sabine for high absorption
                double sabineRT60 = 0.161 * volume / (surfaceArea * averageAlpha);
                eyringRT60.Should().BeLessThan(sabineRT60,
                    "Eyring formula should give shorter RT60 than Sabine for high absorption");
            }

            [Test]
            public void CalculateRT60Eyring_LowAbsorption_CloseToSabine()
            {
                double volume = 200.0;
                double surfaceArea = 250.0;
                double averageAlpha = 0.05; // low absorption

                var eyringRT60 = _model.CalculateRT60Eyring(volume, surfaceArea, averageAlpha);

                double sabineRT60 = 0.161 * volume / (surfaceArea * averageAlpha);
                var percentDiff = Math.Abs(eyringRT60 - sabineRT60) / sabineRT60;
                percentDiff.Should().BeLessThan(0.1,
                    "Eyring and Sabine should converge for low absorption");
            }

            [Test]
            public void CalculateSTI_ShortRT60_HighIntelligibility()
            {
                double rt60 = 0.5; // short reverberation
                double volume = 100.0;

                var sti = _model.CalculateSTI(rt60, volume);

                sti.Should().BeInRange(0.0, 1.0);
                sti.Should().BeGreaterThan(0.5,
                    "short reverberation time should yield good speech intelligibility");
            }

            [Test]
            public void CalculateSTI_LongRT60_LowIntelligibility()
            {
                double rt60 = 4.0; // very long reverberation
                double volume = 1000.0;

                var sti = _model.CalculateSTI(rt60, volume);

                sti.Should().BeInRange(0.0, 1.0);
                sti.Should().BeLessThan(0.5,
                    "long reverberation time should yield poor speech intelligibility");
            }

            [Test]
            public void CalculateSTI_AlwaysInZeroToOneRange()
            {
                // Test edge cases
                var sti1 = _model.CalculateSTI(0.1, 10.0);
                var sti2 = _model.CalculateSTI(10.0, 10000.0);

                sti1.Should().BeInRange(0.0, 1.0);
                sti2.Should().BeInRange(0.0, 1.0);
            }
        }

        #endregion

        #region DaylightingModel Tests

        [TestFixture]
        public class DaylightingModelTests
        {
            private DaylightingModel _model;

            [SetUp]
            public void SetUp()
            {
                _model = new DaylightingModel();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _model.Should().NotBeNull();
            }
        }

        #endregion

        #region UncertaintyEngine Tests

        [TestFixture]
        public class UncertaintyEngineTests
        {
            private UncertaintyEngine _engine;

            [SetUp]
            public void SetUp()
            {
                _engine = new UncertaintyEngine();
            }

            [Test]
            public void Constructor_CreatesInstance()
            {
                _engine.Should().NotBeNull();
            }

            [Test]
            public void CombineAssessments_WeightedAverage_ReturnsBlendedConfidence()
            {
                var assessments = new List<UncertaintyAssessment>
                {
                    new UncertaintyAssessment
                    {
                        PropertyType = "Thickness",
                        CalibratedConfidence = 0.9f,
                        AdjustedConfidence = 0.9f,
                        BaseConfidence = 0.9f
                    },
                    new UncertaintyAssessment
                    {
                        PropertyType = "Material",
                        CalibratedConfidence = 0.6f,
                        AdjustedConfidence = 0.6f,
                        BaseConfidence = 0.6f
                    }
                };

                var combined = _engine.CombineAssessments(assessments, CombinationMethod.WeightedAverage);

                combined.Should().NotBeNull();
                combined.CombinedConfidence.Should().BeInRange(0.0f, 1.0f);
                // Weighted average of 0.9 and 0.6 should be between them
                combined.CombinedConfidence.Should().BeGreaterThanOrEqualTo(0.6f);
                combined.CombinedConfidence.Should().BeLessThanOrEqualTo(0.9f);
            }

            [Test]
            public void CombineAssessments_Minimum_ReturnsLowestConfidence()
            {
                var assessments = new List<UncertaintyAssessment>
                {
                    new UncertaintyAssessment { CalibratedConfidence = 0.9f, AdjustedConfidence = 0.9f, BaseConfidence = 0.9f },
                    new UncertaintyAssessment { CalibratedConfidence = 0.3f, AdjustedConfidence = 0.3f, BaseConfidence = 0.3f },
                    new UncertaintyAssessment { CalibratedConfidence = 0.7f, AdjustedConfidence = 0.7f, BaseConfidence = 0.7f }
                };

                var combined = _engine.CombineAssessments(assessments, CombinationMethod.Minimum);

                combined.Should().NotBeNull();
                combined.CombinedConfidence.Should().BeApproximately(0.3f, 0.05f,
                    "minimum combination should return lowest confidence");
            }

            [Test]
            public void CombineAssessments_Maximum_ReturnsHighestConfidence()
            {
                var assessments = new List<UncertaintyAssessment>
                {
                    new UncertaintyAssessment { CalibratedConfidence = 0.2f, AdjustedConfidence = 0.2f, BaseConfidence = 0.2f },
                    new UncertaintyAssessment { CalibratedConfidence = 0.8f, AdjustedConfidence = 0.8f, BaseConfidence = 0.8f }
                };

                var combined = _engine.CombineAssessments(assessments, CombinationMethod.Maximum);

                combined.Should().NotBeNull();
                combined.CombinedConfidence.Should().BeApproximately(0.8f, 0.05f,
                    "maximum combination should return highest confidence");
            }

            [Test]
            public void PropagateUncertainty_LinearFunction_PropagatesCorrectly()
            {
                // f(x) = 2x, with x having uncertainty
                Func<double[], double> linearCalc = inputs => inputs[0] * 2;
                var uncertainInputs = new List<UncertainValue>
                {
                    new UncertainValue { Value = 10.0, Uncertainty = 1.0 }
                };

                var result = _engine.PropagateUncertainty(linearCalc, uncertainInputs);

                result.Should().NotBeNull();
                result.MeanResult.Should().BeApproximately(20.0, 2.0,
                    "mean of f(x)=2x with x=10 should be ~20");
                result.Confidence95High.Should().BeGreaterThan(result.MeanResult);
                result.Confidence95Low.Should().BeLessThan(result.MeanResult);
            }

            [Test]
            public void PropagateUncertainty_MultipleInputs_ProducesUncertaintyBounds()
            {
                // f(x,y) = x + y
                Func<double[], double> addCalc = inputs => inputs[0] + inputs[1];
                var uncertainInputs = new List<UncertainValue>
                {
                    new UncertainValue { Value = 5.0, Uncertainty = 0.5 },
                    new UncertainValue { Value = 3.0, Uncertainty = 0.3 }
                };

                var result = _engine.PropagateUncertainty(addCalc, uncertainInputs);

                result.Should().NotBeNull();
                result.MeanResult.Should().BeApproximately(8.0, 1.0);
                result.OutputUncertainty.Should().BeGreaterThan(0,
                    "combined uncertainty should be positive");
            }

            [Test]
            public void PropagateUncertainty_ZeroUncertainty_NarrowBounds()
            {
                Func<double[], double> calc = inputs => inputs[0] * 3;
                var inputs = new List<UncertainValue>
                {
                    new UncertainValue { Value = 10.0, Uncertainty = 0.0001 }
                };

                var result = _engine.PropagateUncertainty(calc, inputs);

                result.Should().NotBeNull();
                var spread = result.Confidence95High - result.Confidence95Low;
                spread.Should().BeLessThan(1.0,
                    "near-zero input uncertainty should produce narrow output bounds");
            }

            [Test]
            public void Assess_ValidProperty_ReturnsAssessment()
            {
                var context = new AssessmentContext();

                var assessment = _engine.Assess("Thickness", 0.3, context);

                assessment.Should().NotBeNull();
                assessment.PropertyType.Should().Be("Thickness");
                assessment.CalibratedConfidence.Should().BeInRange(0.0f, 1.0f);
            }
        }

        #endregion
    }
}
