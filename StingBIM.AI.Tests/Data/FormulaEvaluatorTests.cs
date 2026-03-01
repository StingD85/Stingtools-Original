using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Data.Formulas;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Unit tests for FormulaEvaluator class.
    /// Tests formula parsing, RPN evaluation, and built-in functions.
    /// </summary>
    [TestFixture]
    public class FormulaEvaluatorTests
    {
        private FormulaEvaluator _evaluator;

        [SetUp]
        public void Setup()
        {
            _evaluator = new FormulaEvaluator();
        }

        #region Basic Arithmetic Tests

        [Test]
        public void Evaluate_Addition_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "5 + 3";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(8);
        }

        [Test]
        public void Evaluate_Subtraction_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "10 - 4";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(6);
        }

        [Test]
        public void Evaluate_Multiplication_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "6 * 7";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(42);
        }

        [Test]
        public void Evaluate_Division_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "20 / 4";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(5);
        }

        [Test]
        public void Evaluate_DivisionByZero_ShouldThrow()
        {
            // Arrange
            var formula = "10 / 0";
            var parameters = new Dictionary<string, double>();

            // Act
            Action act = () => _evaluator.Evaluate(formula, parameters);

            // Assert
            act.Should().Throw<FormulaEvaluationException>()
                .WithMessage("*zero*");
        }

        [Test]
        public void Evaluate_Power_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "2 ^ 3";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(8);
        }

        [Test]
        public void Evaluate_ComplexExpression_ShouldRespectOperatorPrecedence()
        {
            // Arrange
            var formula = "2 + 3 * 4";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(14); // Not 20
        }

        [Test]
        public void Evaluate_Parentheses_ShouldOverridePrecedence()
        {
            // Arrange
            var formula = "(2 + 3) * 4";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(20);
        }

        [Test]
        public void Evaluate_NestedParentheses_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "((2 + 3) * (4 - 1))";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(15); // (5 * 3)
        }

        #endregion

        #region Comparison Operator Tests

        [Test]
        public void Evaluate_Equality_True_ShouldReturnOne()
        {
            // Arrange
            var formula = "5 = 5";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_Equality_False_ShouldReturnZero()
        {
            // Arrange
            var formula = "5 = 6";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void Evaluate_NotEqual_True_ShouldReturnOne()
        {
            // Arrange
            var formula = "5 <> 6";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_LessThan_True_ShouldReturnOne()
        {
            // Arrange
            var formula = "3 < 5";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_GreaterThan_True_ShouldReturnOne()
        {
            // Arrange
            var formula = "7 > 5";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_LessOrEqual_Equal_ShouldReturnOne()
        {
            // Arrange
            var formula = "5 <= 5";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_GreaterOrEqual_Greater_ShouldReturnOne()
        {
            // Arrange
            var formula = "6 >= 5";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        #endregion

        #region Logical Operator Tests

        [Test]
        public void Evaluate_And_BothTrue_ShouldReturnOne()
        {
            // Arrange
            var formula = "1 and 1";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_And_OneFalse_ShouldReturnZero()
        {
            // Arrange
            var formula = "1 and 0";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void Evaluate_Or_OneTrue_ShouldReturnOne()
        {
            // Arrange
            var formula = "1 or 0";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_Or_BothFalse_ShouldReturnZero()
        {
            // Arrange
            var formula = "0 or 0";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void Evaluate_Not_True_ShouldReturnZero()
        {
            // Arrange
            var formula = "not 1";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void Evaluate_Not_False_ShouldReturnOne()
        {
            // Arrange
            var formula = "not 0";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        #endregion

        #region Function Tests

        [Test]
        public void Evaluate_Abs_Negative_ShouldReturnPositive()
        {
            // Arrange
            var formula = "abs(-5)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(5);
        }

        [Test]
        public void Evaluate_Sqrt_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "sqrt(16)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_Sqrt_Negative_ShouldThrow()
        {
            // Arrange
            var formula = "sqrt(-1)";
            var parameters = new Dictionary<string, double>();

            // Act
            Action act = () => _evaluator.Evaluate(formula, parameters);

            // Assert
            act.Should().Throw<FormulaEvaluationException>()
                .WithMessage("*negative*");
        }

        [Test]
        public void Evaluate_Round_ShouldRoundCorrectly()
        {
            // Arrange
            var formula = "round(3.7)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_RoundDown_ShouldFloor()
        {
            // Arrange
            var formula = "rounddown(3.9)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(3);
        }

        [Test]
        public void Evaluate_RoundUp_ShouldCeiling()
        {
            // Arrange
            var formula = "roundup(3.1)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_Floor_ShouldFloorCorrectly()
        {
            // Arrange
            var formula = "floor(3.9)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(3);
        }

        [Test]
        public void Evaluate_Ceiling_ShouldCeilingCorrectly()
        {
            // Arrange
            var formula = "ceiling(3.1)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_Max_ShouldReturnLarger()
        {
            // Arrange
            var formula = "max(3, 7)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(7);
        }

        [Test]
        public void Evaluate_Min_ShouldReturnSmaller()
        {
            // Arrange
            var formula = "min(3, 7)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(3);
        }

        [Test]
        public void Evaluate_Pi_ShouldReturnPi()
        {
            // Arrange
            var formula = "pi()";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(Math.PI, 0.0001);
        }

        [Test]
        public void Evaluate_Sin_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "sin(0)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(0, 0.0001);
        }

        [Test]
        public void Evaluate_Cos_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "cos(0)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(1, 0.0001);
        }

        [Test]
        public void Evaluate_Tan_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "tan(0)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(0, 0.0001);
        }

        [Test]
        public void Evaluate_Exp_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "exp(1)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(Math.E, 0.0001);
        }

        [Test]
        public void Evaluate_Ln_ShouldCalculateCorrectly()
        {
            // Arrange
            var formula = "ln(2.718281828)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(1, 0.001);
        }

        [Test]
        public void Evaluate_Log_ShouldCalculateLog10()
        {
            // Arrange
            var formula = "log(100)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(2, 0.0001);
        }

        #endregion

        #region IF Function Tests

        [Test]
        public void Evaluate_If_ConditionTrue_ShouldReturnTrueValue()
        {
            // Arrange
            var formula = "if(1, 10, 20)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(10);
        }

        [Test]
        public void Evaluate_If_ConditionFalse_ShouldReturnFalseValue()
        {
            // Arrange
            var formula = "if(0, 10, 20)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(20);
        }

        [Test]
        public void Evaluate_If_WithComparison_ShouldWork()
        {
            // Arrange
            var formula = "if(5 > 3, 100, 200)";
            var parameters = new Dictionary<string, double>();

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(100);
        }

        #endregion

        #region Parameter Tests

        [Test]
        public void Evaluate_WithParameter_ShouldSubstituteValue()
        {
            // Arrange
            var formula = "Width * Height";
            var parameters = new Dictionary<string, double>
            {
                { "Width", 10 },
                { "Height", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(50);
        }

        [Test]
        public void Evaluate_MissingParameter_ShouldThrow()
        {
            // Arrange
            var formula = "Width * Height";
            var parameters = new Dictionary<string, double>
            {
                { "Width", 10 }
                // Height is missing
            };

            // Act
            Action act = () => _evaluator.Evaluate(formula, parameters);

            // Assert
            act.Should().Throw<FormulaEvaluationException>()
                .WithMessage("*Height*not found*");
        }

        [Test]
        public void Evaluate_ComplexFormulaWithParameters_ShouldWork()
        {
            // Arrange - Area calculation for pipe cross-section
            var formula = "pi() * (Diameter / 2) ^ 2";
            var parameters = new Dictionary<string, double>
            {
                { "Diameter", 2 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(Math.PI, 0.0001);
        }

        [Test]
        public void Evaluate_EngineeringFormula_HeatTransfer()
        {
            // Arrange - Q = U * A * DeltaT (heat transfer)
            var formula = "U * A * DeltaT";
            var parameters = new Dictionary<string, double>
            {
                { "U", 5.0 },      // W/m²K
                { "A", 10.0 },     // m²
                { "DeltaT", 20.0 } // K
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1000); // 1000 W
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void Evaluate_NullFormula_ShouldThrow()
        {
            // Arrange
            var parameters = new Dictionary<string, double>();

            // Act
            Action act = () => _evaluator.Evaluate(null, parameters);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Evaluate_NullParameters_ShouldThrow()
        {
            // Arrange
            var formula = "5 + 3";

            // Act
            Action act = () => _evaluator.Evaluate(formula, (Dictionary<string, double>)null);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Evaluate_UnknownFunction_ShouldThrow()
        {
            // Arrange
            var formula = "unknownfunc(5)";
            var parameters = new Dictionary<string, double>();

            // Act
            Action act = () => _evaluator.Evaluate(formula, parameters);

            // Assert
            act.Should().Throw<FormulaEvaluationException>()
                .WithMessage("*Unknown function*");
        }

        #endregion

        #region TryEvaluate Tests

        [Test]
        public void TryEvaluate_ValidFormula_ShouldReturnTrue()
        {
            // Arrange
            var formula = "5 + 3";
            var parameters = new Dictionary<string, double>();

            // Act
            var success = _evaluator.TryEvaluate(formula, parameters, out var result);

            // Assert
            success.Should().BeTrue();
            result.Should().Be(8);
        }

        [Test]
        public void TryEvaluate_InvalidFormula_ShouldReturnFalse()
        {
            // Arrange
            var formula = "5 / 0";
            var parameters = new Dictionary<string, double>();

            // Act
            var success = _evaluator.TryEvaluate(formula, parameters, out var result);

            // Assert
            success.Should().BeFalse();
            result.Should().Be(0);
        }

        [Test]
        public void TryEvaluate_MissingParameter_ShouldReturnFalse()
        {
            // Arrange
            var formula = "Width * Height";
            var parameters = new Dictionary<string, double>();

            // Act
            var success = _evaluator.TryEvaluate(formula, parameters, out var result);

            // Assert
            success.Should().BeFalse();
        }

        #endregion

        #region Real-World BIM Formula Tests

        [Test]
        public void Evaluate_WallArea_ShouldCalculateCorrectly()
        {
            // Arrange - Wall area = Length * Height - (Window area + Door area)
            var formula = "Length * Height - WindowArea - DoorArea";
            var parameters = new Dictionary<string, double>
            {
                { "Length", 5000 },     // mm
                { "Height", 3000 },     // mm
                { "WindowArea", 1500000 }, // mm²
                { "DoorArea", 2100000 }    // mm²
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(11400000); // mm² (15000000 - 1500000 - 2100000)
        }

        [Test]
        public void Evaluate_HVACDuctSizing_ShouldCalculateCorrectly()
        {
            // Arrange - Duct area = CFM / Velocity
            var formula = "CFM / Velocity * 144"; // Convert to sq inches
            var parameters = new Dictionary<string, double>
            {
                { "CFM", 1000 },      // Cubic feet per minute
                { "Velocity", 800 }  // FPM
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(180); // sq inches
        }

        [Test]
        public void Evaluate_ElectricalLoadCalc_ShouldCalculateCorrectly()
        {
            // Arrange - Power = Voltage * Current * PowerFactor
            var formula = "Voltage * Current * PowerFactor";
            var parameters = new Dictionary<string, double>
            {
                { "Voltage", 230 },
                { "Current", 10 },
                { "PowerFactor", 0.85 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(1955, 1); // Watts
        }

        [Test]
        public void Evaluate_ConditionalMaterialSelection_ShouldWork()
        {
            // Arrange - Use different insulation R-value based on climate zone
            var formula = "if(ClimateZone >= 5, 30, if(ClimateZone >= 3, 20, 13))";
            var parameters = new Dictionary<string, double>
            {
                { "ClimateZone", 4 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(20); // R-20 for zone 3-4
        }

        #endregion
    }
}
