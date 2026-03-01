// StingBIM.AI.Tests - FormulaEvaluatorTests.cs
// Unit tests for the FormulaEvaluator component
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Formulas;

namespace StingBIM.AI.Tests.Foundation
{
    /// <summary>
    /// Unit tests for FormulaEvaluator - testing RPN evaluation,
    /// arithmetic operations, functions, and edge cases.
    /// </summary>
    [TestFixture]
    public class FormulaEvaluatorTests
    {
        private FormulaEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new FormulaEvaluator();
        }

        #region Basic Arithmetic Tests

        [Test]
        public void Evaluate_Addition_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a + b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(15);
        }

        [Test]
        public void Evaluate_Subtraction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a - b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 3 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(7);
        }

        [Test]
        public void Evaluate_Multiplication_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "length * width";
            var parameters = new Dictionary<string, double>
            {
                { "length", 5 },
                { "width", 4 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(20);
        }

        [Test]
        public void Evaluate_Division_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "total / count";
            var parameters = new Dictionary<string, double>
            {
                { "total", 100 },
                { "count", 4 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(25);
        }

        [Test]
        public void Evaluate_DivisionByZero_ThrowsFormulaEvaluationException()
        {
            // Arrange
            var formula = "a / b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 0 }
            };

            // Act & Assert
            Assert.Throws<FormulaEvaluationException>(() =>
                _evaluator.Evaluate(formula, parameters));
        }

        [Test]
        public void Evaluate_Power_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "base ^ exponent";
            var parameters = new Dictionary<string, double>
            {
                { "base", 2 },
                { "exponent", 3 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(8);
        }

        #endregion

        #region Complex Expression Tests

        [Test]
        public void Evaluate_ComplexExpression_ReturnsCorrectResult()
        {
            // Arrange - Area calculation: length * width + 2 * (length + width)
            var formula = "length * width + 2 * (length + width)";
            var parameters = new Dictionary<string, double>
            {
                { "length", 10 },
                { "width", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(80); // 10*5 + 2*(10+5) = 50 + 30 = 80
        }

        [Test]
        public void Evaluate_NestedParentheses_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "((a + b) * (c - d)) / e";
            var parameters = new Dictionary<string, double>
            {
                { "a", 2 },
                { "b", 3 },
                { "c", 10 },
                { "d", 5 },
                { "e", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(5); // ((2+3)*(10-5))/5 = (5*5)/5 = 5
        }

        [Test]
        public void Evaluate_OperatorPrecedence_ReturnsCorrectResult()
        {
            // Arrange - Tests that * has higher precedence than +
            var formula = "a + b * c";
            var parameters = new Dictionary<string, double>
            {
                { "a", 2 },
                { "b", 3 },
                { "c", 4 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(14); // 2 + (3*4) = 14, not (2+3)*4 = 20
        }

        #endregion

        #region Function Tests

        [Test]
        public void Evaluate_SqrtFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "sqrt(area)";
            var parameters = new Dictionary<string, double>
            {
                { "area", 144 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(12);
        }

        [Test]
        public void Evaluate_SqrtOfNegative_ThrowsException()
        {
            // Arrange
            var formula = "sqrt(value)";
            var parameters = new Dictionary<string, double>
            {
                { "value", -4 }
            };

            // Act & Assert
            Assert.Throws<FormulaEvaluationException>(() =>
                _evaluator.Evaluate(formula, parameters));
        }

        [Test]
        public void Evaluate_AbsFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "abs(value)";
            var parameters = new Dictionary<string, double>
            {
                { "value", -25 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(25);
        }

        [Test]
        public void Evaluate_RoundFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "round(value)";
            var parameters = new Dictionary<string, double>
            {
                { "value", 3.7 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_FloorFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "floor(value)";
            var parameters = new Dictionary<string, double>
            {
                { "value", 3.9 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(3);
        }

        [Test]
        public void Evaluate_CeilingFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "ceiling(value)";
            var parameters = new Dictionary<string, double>
            {
                { "value", 3.1 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(4);
        }

        [Test]
        public void Evaluate_MinFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "min(a, b)";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(5);
        }

        [Test]
        public void Evaluate_MaxFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "max(a, b)";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(10);
        }

        [Test]
        public void Evaluate_SinFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "sin(angle)";
            var parameters = new Dictionary<string, double>
            {
                { "angle", Math.PI / 2 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(1.0, 0.0001);
        }

        [Test]
        public void Evaluate_CosFunction_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "cos(angle)";
            var parameters = new Dictionary<string, double>
            {
                { "angle", 0 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_PiConstant_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "pi() * radius * radius";
            var parameters = new Dictionary<string, double>
            {
                { "radius", 2 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(Math.PI * 4, 0.0001);
        }

        #endregion

        #region IF Function Tests

        [Test]
        public void Evaluate_IfFunction_TrueCondition_ReturnsTrueValue()
        {
            // Arrange
            var formula = "if(condition, trueVal, falseVal)";
            var parameters = new Dictionary<string, double>
            {
                { "condition", 1 },
                { "trueVal", 100 },
                { "falseVal", 50 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(100);
        }

        [Test]
        public void Evaluate_IfFunction_FalseCondition_ReturnsFalseValue()
        {
            // Arrange
            var formula = "if(condition, trueVal, falseVal)";
            var parameters = new Dictionary<string, double>
            {
                { "condition", 0 },
                { "trueVal", 100 },
                { "falseVal", 50 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(50);
        }

        [Test]
        public void Evaluate_IfWithComparison_ReturnsCorrectResult()
        {
            // Arrange - if(area > 100, area * 1.1, area)
            var formula = "if(area > 100, area * 1.1, area)";
            var parameters = new Dictionary<string, double>
            {
                { "area", 150 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(165); // 150 * 1.1 = 165
        }

        #endregion

        #region Comparison Operator Tests

        [Test]
        public void Evaluate_GreaterThan_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a > b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 10 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1); // True = 1
        }

        [Test]
        public void Evaluate_LessThan_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a < b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 3 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1); // True = 1
        }

        [Test]
        public void Evaluate_EqualComparison_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a = b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 5 },
                { "b", 5 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1); // True = 1
        }

        [Test]
        public void Evaluate_NotEqualComparison_ReturnsCorrectResult()
        {
            // Arrange
            var formula = "a <> b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 5 },
                { "b", 10 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1); // True = 1
        }

        #endregion

        #region Logical Operator Tests

        [Test]
        public void Evaluate_AndOperator_BothTrue_ReturnsTrue()
        {
            // Arrange
            var formula = "a and b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 1 },
                { "b", 1 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_AndOperator_OneFalse_ReturnsFalse()
        {
            // Arrange
            var formula = "a and b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 1 },
                { "b", 0 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        [Test]
        public void Evaluate_OrOperator_OneTrue_ReturnsTrue()
        {
            // Arrange
            var formula = "a or b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 0 },
                { "b", 1 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(1);
        }

        [Test]
        public void Evaluate_NotOperator_ReturnsInverse()
        {
            // Arrange
            var formula = "not a";
            var parameters = new Dictionary<string, double>
            {
                { "a", 1 }
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void Evaluate_MissingParameter_ThrowsException()
        {
            // Arrange
            var formula = "a + b + c";
            var parameters = new Dictionary<string, double>
            {
                { "a", 1 },
                { "b", 2 }
                // 'c' is missing
            };

            // Act & Assert
            Assert.Throws<FormulaEvaluationException>(() =>
                _evaluator.Evaluate(formula, parameters));
        }

        [Test]
        public void Evaluate_NullFormula_ThrowsArgumentNullException()
        {
            // Arrange
            var parameters = new Dictionary<string, double>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _evaluator.Evaluate(null, parameters));
        }

        [Test]
        public void Evaluate_NullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var formula = "a + b";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _evaluator.Evaluate(formula, (Dictionary<string, double>)null));
        }

        [Test]
        public void TryEvaluate_ValidFormula_ReturnsTrue()
        {
            // Arrange
            var formula = "a + b";
            var parameters = new Dictionary<string, double>
            {
                { "a", 5 },
                { "b", 3 }
            };

            // Act
            var success = _evaluator.TryEvaluate(formula, parameters, out var result);

            // Assert
            success.Should().BeTrue();
            result.Should().Be(8);
        }

        [Test]
        public void TryEvaluate_InvalidFormula_ReturnsFalse()
        {
            // Arrange
            var formula = "a +";
            var parameters = new Dictionary<string, double>
            {
                { "a", 5 }
            };

            // Act
            var success = _evaluator.TryEvaluate(formula, parameters, out var result);

            // Assert
            success.Should().BeFalse();
        }

        #endregion

        #region BIM-Specific Formula Tests

        [Test]
        public void Evaluate_WallAreaFormula_ReturnsCorrectResult()
        {
            // Arrange - Typical BIM wall area calculation
            var formula = "Length * Height - (DoorCount * DoorArea + WindowCount * WindowArea)";
            var parameters = new Dictionary<string, double>
            {
                { "Length", 10000 },      // 10m in mm
                { "Height", 3000 },       // 3m in mm
                { "DoorCount", 2 },
                { "DoorArea", 2100 * 900 }, // Standard door
                { "WindowCount", 3 },
                { "WindowArea", 1200 * 1500 } // Standard window
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            var expected = 10000 * 3000 - (2 * 2100 * 900 + 3 * 1200 * 1500);
            result.Should().Be(expected);
        }

        [Test]
        public void Evaluate_VolumeFormula_ReturnsCorrectResult()
        {
            // Arrange - Room volume calculation
            var formula = "Area * Height";
            var parameters = new Dictionary<string, double>
            {
                { "Area", 25 },    // 25 sqm
                { "Height", 3.5 }  // 3.5m
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().Be(87.5);
        }

        [Test]
        public void Evaluate_UValueFormula_ReturnsCorrectResult()
        {
            // Arrange - Thermal U-value calculation: 1 / (Rsi + R1 + R2 + Rse)
            var formula = "1 / (Rsi + R1 + R2 + Rse)";
            var parameters = new Dictionary<string, double>
            {
                { "Rsi", 0.13 },   // Internal surface resistance
                { "R1", 0.5 },    // Insulation layer
                { "R2", 0.15 },   // Block layer
                { "Rse", 0.04 }   // External surface resistance
            };

            // Act
            var result = _evaluator.Evaluate(formula, parameters);

            // Assert
            result.Should().BeApproximately(1.22, 0.01);
        }

        #endregion
    }
}
