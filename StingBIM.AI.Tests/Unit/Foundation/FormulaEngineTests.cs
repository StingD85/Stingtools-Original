using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Formulas;

namespace StingBIM.AI.Tests.Unit.Foundation
{
    [TestFixture]
    public class FormulaEngineTests
    {
        #region FormulaParser Tests

        [TestFixture]
        public class FormulaParserTests
        {
            private FormulaParser _parser;

            [SetUp]
            public void SetUp()
            {
                _parser = new FormulaParser();
            }

            // --- Parse: Basic Tokens ---

            [Test]
            public void Parse_SimpleNumber_ReturnsSingleToken()
            {
                var tokens = _parser.Parse("42");

                tokens.Should().HaveCount(1);
                tokens[0].Type.Should().Be(TokenType.Number);
                tokens[0].Value.Should().Be("42");
            }

            [Test]
            public void Parse_DecimalNumber_ParsesCorrectly()
            {
                var tokens = _parser.Parse("3.14");

                tokens.Should().HaveCount(1);
                tokens[0].Type.Should().Be(TokenType.Number);
                tokens[0].Value.Should().Be("3.14");
            }

            [Test]
            public void Parse_Addition_ReturnsThreeTokens()
            {
                var tokens = _parser.Parse("2 + 3");

                tokens.Should().HaveCount(3);
                tokens[0].Type.Should().Be(TokenType.Number);
                tokens[1].Type.Should().Be(TokenType.Operator);
                tokens[1].Value.Should().Be("+");
                tokens[2].Type.Should().Be(TokenType.Number);
            }

            [TestCase("+")]
            [TestCase("-")]
            [TestCase("*")]
            [TestCase("/")]
            [TestCase("^")]
            public void Parse_ArithmeticOperators_RecognizedCorrectly(string op)
            {
                var tokens = _parser.Parse($"1 {op} 2");

                tokens.Should().Contain(t => t.Type == TokenType.Operator && t.Value == op);
            }

            [Test]
            public void Parse_Parentheses_RecognizedCorrectly()
            {
                var tokens = _parser.Parse("(2 + 3)");

                tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
                tokens.Should().Contain(t => t.Type == TokenType.RightParen);
            }

            [Test]
            public void Parse_Function_RecognizedCorrectly()
            {
                var tokens = _parser.Parse("sqrt(16)");

                tokens.Should().Contain(t => t.Type == TokenType.Function && t.Value.Equals("sqrt", StringComparison.OrdinalIgnoreCase));
            }

            [TestCase("abs")]
            [TestCase("sin")]
            [TestCase("cos")]
            [TestCase("tan")]
            [TestCase("sqrt")]
            [TestCase("round")]
            [TestCase("max")]
            [TestCase("min")]
            public void Parse_BuiltInFunctions_RecognizedAsFunction(string func)
            {
                var tokens = _parser.Parse($"{func}(1)");

                tokens.Should().Contain(t => t.Type == TokenType.Function);
            }

            [Test]
            public void Parse_NullFormula_ThrowsArgumentNullException()
            {
                Action act = () => _parser.Parse(null);

                act.Should().Throw<ArgumentNullException>();
            }

            // --- IsValidSyntax ---

            [TestCase("2 + 3", true)]
            [TestCase("sqrt(16)", true)]
            [TestCase("(2 + 3) * 4", true)]
            public void IsValidSyntax_ValidFormulas_ReturnsTrue(string formula, bool expected)
            {
                var result = _parser.IsValidSyntax(formula);

                result.Should().Be(expected);
            }

            // --- ExtractParameterReferences ---

            [Test]
            public void ExtractParameterReferences_NoParameters_ReturnsEmpty()
            {
                var refs = _parser.ExtractParameterReferences("2 + 3");

                refs.Should().BeEmpty();
            }

            [Test]
            public void ExtractParameterReferences_NullFormula_ReturnsEmpty()
            {
                var refs = _parser.ExtractParameterReferences(null);

                refs.Should().BeEmpty();
            }

            // --- ToRPN (Shunting-Yard) ---

            [Test]
            public void ToRPN_SimpleAddition_CorrectPostfixOrder()
            {
                var tokens = _parser.Parse("2 + 3");
                var rpn = _parser.ToRPN(tokens);

                // In RPN: 2, 3, +
                var values = rpn.Select(t => t.Value).ToList();
                values.Should().EndWith("+");
            }

            [Test]
            public void ToRPN_PrecedenceMultiplyOverAdd_CorrectOrder()
            {
                var tokens = _parser.Parse("2 + 3 * 4");
                var rpn = _parser.ToRPN(tokens);

                // In RPN: 2, 3, 4, *, +
                var values = rpn.Select(t => t.Value).ToList();
                var multIdx = values.IndexOf("*");
                var addIdx = values.IndexOf("+");
                multIdx.Should().BeLessThan(addIdx, "multiply should come before add in RPN");
            }

            [Test]
            public void ToRPN_ParenthesesOverridePrecedence_CorrectOrder()
            {
                var tokens = _parser.Parse("(2 + 3) * 4");
                var rpn = _parser.ToRPN(tokens);

                // In RPN: 2, 3, +, 4, *
                var values = rpn.Select(t => t.Value).ToList();
                var addIdx = values.IndexOf("+");
                var multIdx = values.IndexOf("*");
                addIdx.Should().BeLessThan(multIdx, "parenthesized addition should come before multiply");
            }

            [Test]
            public void ToRPN_NoParenthesesInOutput()
            {
                var tokens = _parser.Parse("(2 + 3) * (4 - 1)");
                var rpn = _parser.ToRPN(tokens);

                rpn.Should().NotContain(t => t.Type == TokenType.LeftParen);
                rpn.Should().NotContain(t => t.Type == TokenType.RightParen);
            }
        }

        #endregion

        #region FormulaEvaluator Tests

        [TestFixture]
        public class FormulaEvaluatorTests
        {
            private FormulaEvaluator _evaluator;

            [SetUp]
            public void SetUp()
            {
                _evaluator = new FormulaEvaluator();
            }

            // --- Basic Arithmetic ---

            [TestCase("2 + 3", 5.0)]
            [TestCase("10 - 4", 6.0)]
            [TestCase("6 * 7", 42.0)]
            [TestCase("20 / 4", 5.0)]
            [TestCase("2 ^ 3", 8.0)]
            public void Evaluate_BasicArithmetic_ReturnsCorrectResult(string formula, double expected)
            {
                var result = _evaluator.Evaluate(formula, new Dictionary<string, double>());

                result.Should().BeApproximately(expected, 0.001);
            }

            [Test]
            public void Evaluate_OperatorPrecedence_MultiplyBeforeAdd()
            {
                var result = _evaluator.Evaluate("2 + 3 * 4", new Dictionary<string, double>());

                // 2 + (3 * 4) = 14, NOT (2 + 3) * 4 = 20
                result.Should().BeApproximately(14.0, 0.001);
            }

            [Test]
            public void Evaluate_Parentheses_OverridePrecedence()
            {
                var result = _evaluator.Evaluate("(2 + 3) * 4", new Dictionary<string, double>());

                result.Should().BeApproximately(20.0, 0.001);
            }

            [Test]
            public void Evaluate_NestedParentheses_CorrectResult()
            {
                var result = _evaluator.Evaluate("((2 + 3) * (4 - 1))", new Dictionary<string, double>());

                // (5) * (3) = 15
                result.Should().BeApproximately(15.0, 0.001);
            }

            // --- Built-in Functions ---

            [Test]
            public void Evaluate_Sqrt_ReturnsSquareRoot()
            {
                var result = _evaluator.Evaluate("sqrt(16)", new Dictionary<string, double>());

                result.Should().BeApproximately(4.0, 0.001);
            }

            [Test]
            public void Evaluate_Abs_ReturnsAbsoluteValue()
            {
                var result = _evaluator.Evaluate("abs(-5)", new Dictionary<string, double>());

                result.Should().BeApproximately(5.0, 0.001);
            }

            // --- Parameters ---

            [Test]
            public void Evaluate_WithParameters_SubstitutesValues()
            {
                var parameters = new Dictionary<string, double>
                {
                    { "Length", 10.0 },
                    { "Width", 5.0 }
                };

                var result = _evaluator.Evaluate("Length * Width", parameters);

                result.Should().BeApproximately(50.0, 0.001);
            }

            [Test]
            public void Evaluate_ComplexFormula_WithParameters()
            {
                var parameters = new Dictionary<string, double>
                {
                    { "Height", 3.0 },
                    { "Width", 4.0 }
                };

                var result = _evaluator.Evaluate("sqrt(Height ^ 2 + Width ^ 2)", parameters);

                // sqrt(9 + 16) = sqrt(25) = 5 (Pythagorean theorem)
                result.Should().BeApproximately(5.0, 0.001);
            }

            // --- TryEvaluate ---

            [Test]
            public void TryEvaluate_ValidFormula_ReturnsTrueWithResult()
            {
                var success = _evaluator.TryEvaluate("2 + 3", new Dictionary<string, double>(), out double result);

                success.Should().BeTrue();
                result.Should().BeApproximately(5.0, 0.001);
            }

            [Test]
            public void TryEvaluate_InvalidFormula_ReturnsFalse()
            {
                var success = _evaluator.TryEvaluate("2 +", new Dictionary<string, double>(), out double result);

                success.Should().BeFalse();
            }

            // --- Edge Cases ---

            [Test]
            public void Evaluate_NullFormula_ThrowsArgumentNullException()
            {
                Action act = () => _evaluator.Evaluate(null, new Dictionary<string, double>());

                act.Should().Throw<ArgumentNullException>();
            }

            [Test]
            public void Evaluate_NullParameters_ThrowsArgumentNullException()
            {
                Action act = () => _evaluator.Evaluate("1 + 1", (Dictionary<string, double>)null);

                act.Should().Throw<ArgumentNullException>();
            }
        }

        #endregion

        #region DependencyGraph Tests

        [TestFixture]
        public class DependencyGraphTests
        {
            [Test]
            public void DependencyGraph_AddNode_IncreasesCount()
            {
                var graph = new DependencyGraph();

                graph.AddNode("Area");
                graph.AddNode("Volume");

                graph.NodeCount.Should().Be(2);
            }

            [Test]
            public void DependencyGraph_AddDependency_TrackedCorrectly()
            {
                var graph = new DependencyGraph();
                graph.AddNode("Area");
                graph.AddNode("Length");
                graph.AddNode("Width");

                graph.AddDependency("Area", "Length");
                graph.AddDependency("Area", "Width");

                graph.GetDependencies("Area").Should().Contain("Length");
                graph.GetDependencies("Area").Should().Contain("Width");
                graph.DependencyCount.Should().Be(2);
            }

            [Test]
            public void DependencyGraph_GetDependents_ReturnsReverseDependencies()
            {
                var graph = new DependencyGraph();
                graph.AddNode("Area");
                graph.AddNode("Volume");
                graph.AddNode("Height");

                graph.AddDependency("Area", "Height");
                graph.AddDependency("Volume", "Height");

                graph.GetDependents("Height").Should().Contain("Area");
                graph.GetDependents("Height").Should().Contain("Volume");
            }

            [Test]
            public void DependencyGraph_ContainsNode_CaseInsensitive()
            {
                var graph = new DependencyGraph();
                graph.AddNode("Area");

                graph.ContainsNode("area").Should().BeTrue();
                graph.ContainsNode("AREA").Should().BeTrue();
            }
        }

        #endregion
    }
}
