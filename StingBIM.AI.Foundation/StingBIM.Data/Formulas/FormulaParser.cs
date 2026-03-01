using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace StingBIM.Data.Formulas
{
    /// <summary>
    /// Parses Revit formula syntax into tokens and validates syntax correctness.
    /// Handles all Revit formula operators, functions, and parameter references.
    /// </summary>
    /// <remarks>
    /// Supports:
    /// - Arithmetic operators: +, -, *, /, ^
    /// - Comparison operators: =, <>, <, >, <=, >=
    /// - Logical operators: and, or, not
    /// - Functions: if, abs, sin, cos, tan, asin, acos, atan, sqrt, exp, ln, log, round, etc.
    /// - Parameter references: enclosed in quotes or square brackets
    /// - Constants: numeric values with units
    /// - Parentheses for grouping
    /// </remarks>
    public class FormulaParser
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Revit formula operators by precedence (highest to lowest)
        private static readonly Dictionary<string, int> OperatorPrecedence = new Dictionary<string, int>
        {
            { "^", 5 },      // Power (highest)
            { "*", 4 },      // Multiplication
            { "/", 4 },      // Division
            { "+", 3 },      // Addition
            { "-", 3 },      // Subtraction
            { "=", 2 },      // Equal
            { "<>", 2 },     // Not equal
            { "<", 2 },      // Less than
            { ">", 2 },      // Greater than
            { "<=", 2 },     // Less than or equal
            { ">=", 2 },     // Greater than or equal
            { "and", 1 },    // Logical AND
            { "or", 1 },     // Logical OR
            { "not", 1 }     // Logical NOT (lowest)
        };

        // Revit built-in functions
        private static readonly HashSet<string> BuiltInFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "if", "abs", "acos", "asin", "atan", "cos", "exp", "ln", "log",
            "round", "rounddown", "roundup", "sin", "sqrt", "tan", "pi",
            "max", "min", "ceiling", "floor"
        };

        // Valid Revit units
        private static readonly HashSet<string> ValidUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mm", "cm", "m", "ft", "in",               // Length
            "sq m", "sq ft", "sq in",                  // Area
            "cu m", "cu ft", "cu in",                  // Volume
            "kg", "lb",                                // Mass
            "kN", "N", "lbf",                          // Force
            "kPa", "MPa", "Pa", "psi",                 // Pressure
            "C", "F", "K",                             // Temperature
            "W", "kW", "BTU",                          // Power
            "L", "gal",                                // Capacity
            "m/s", "ft/s",                             // Velocity
            "A", "V", "Ohm",                           // Electrical
            "deg", "rad"                               // Angle
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Parses a Revit formula string into tokens.
        /// </summary>
        /// <param name="formula">The formula string to parse</param>
        /// <returns>List of tokens representing the parsed formula</returns>
        /// <exception cref="ArgumentNullException">Thrown if formula is null</exception>
        /// <exception cref="FormulaParseException">Thrown if formula has syntax errors</exception>
        public List<FormulaToken> Parse(string formula)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            try
            {
                Logger.Debug($"Parsing formula: {formula}");

                // Tokenize the formula
                var tokens = Tokenize(formula);

                // Validate syntax
                ValidateSyntax(tokens);

                Logger.Debug($"Successfully parsed formula into {tokens.Count} tokens");
                return tokens;
            }
            catch (FormulaParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error parsing formula: {formula}");
                throw new FormulaParseException($"Failed to parse formula: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates formula syntax without full parsing.
        /// </summary>
        /// <param name="formula">The formula string to validate</param>
        /// <returns>True if syntax is valid; otherwise, false</returns>
        public bool IsValidSyntax(string formula)
        {
            try
            {
                Parse(formula);
                return true;
            }
            catch (FormulaParseException)
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts all parameter references from a formula.
        /// </summary>
        /// <param name="formula">The formula string</param>
        /// <returns>List of parameter names referenced in the formula</returns>
        public List<string> ExtractParameterReferences(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return new List<string>();
            }

            var parameters = new List<string>();
            var tokens = Tokenize(formula);

            foreach (var token in tokens.Where(t => t.Type == TokenType.Parameter))
            {
                if (!parameters.Contains(token.Value, StringComparer.OrdinalIgnoreCase))
                {
                    parameters.Add(token.Value);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Converts tokens to Reverse Polish Notation (RPN) for easier evaluation.
        /// Uses the Shunting Yard algorithm.
        /// </summary>
        /// <param name="tokens">Infix notation tokens</param>
        /// <returns>RPN tokens</returns>
        public List<FormulaToken> ToRPN(List<FormulaToken> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return new List<FormulaToken>();
            }

            var output = new List<FormulaToken>();
            var operatorStack = new Stack<FormulaToken>();

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.Parameter:
                        output.Add(token);
                        break;

                    case TokenType.Function:
                        operatorStack.Push(token);
                        break;

                    case TokenType.Operator:
                        while (operatorStack.Count > 0 &&
                               operatorStack.Peek().Type == TokenType.Operator &&
                               GetPrecedence(operatorStack.Peek().Value) >= GetPrecedence(token.Value))
                        {
                            output.Add(operatorStack.Pop());
                        }
                        operatorStack.Push(token);
                        break;

                    case TokenType.LeftParen:
                        operatorStack.Push(token);
                        break;

                    case TokenType.RightParen:
                        while (operatorStack.Count > 0 && operatorStack.Peek().Type != TokenType.LeftParen)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        if (operatorStack.Count == 0)
                        {
                            throw new FormulaParseException("Mismatched parentheses");
                        }
                        operatorStack.Pop(); // Remove left paren

                        // If there's a function on the stack, pop it to output
                        if (operatorStack.Count > 0 && operatorStack.Peek().Type == TokenType.Function)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        break;

                    case TokenType.Comma:
                        while (operatorStack.Count > 0 && operatorStack.Peek().Type != TokenType.LeftParen)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        break;
                }
            }

            while (operatorStack.Count > 0)
            {
                var token = operatorStack.Pop();
                if (token.Type == TokenType.LeftParen || token.Type == TokenType.RightParen)
                {
                    throw new FormulaParseException("Mismatched parentheses");
                }
                output.Add(token);
            }

            return output;
        }

        #endregion

        #region Private Methods - Tokenization

        /// <summary>
        /// Tokenizes a formula string into individual tokens.
        /// </summary>
        private List<FormulaToken> Tokenize(string formula)
        {
            var tokens = new List<FormulaToken>();
            var position = 0;

            while (position < formula.Length)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(formula[position]))
                {
                    position++;
                    continue;
                }

                // Try to read each token type
                if (TryReadNumber(formula, ref position, out var numberToken))
                {
                    tokens.Add(numberToken);
                }
                else if (TryReadParameter(formula, ref position, out var paramToken))
                {
                    tokens.Add(paramToken);
                }
                else if (TryReadFunction(formula, ref position, out var funcToken))
                {
                    tokens.Add(funcToken);
                }
                else if (TryReadOperator(formula, ref position, out var opToken))
                {
                    tokens.Add(opToken);
                }
                else if (TryReadParenthesis(formula, ref position, out var parenToken))
                {
                    tokens.Add(parenToken);
                }
                else if (TryReadComma(formula, ref position, out var commaToken))
                {
                    tokens.Add(commaToken);
                }
                else
                {
                    throw new FormulaParseException($"Unexpected character at position {position}: '{formula[position]}'");
                }
            }

            return tokens;
        }

        /// <summary>
        /// Tries to read a numeric value with optional unit.
        /// </summary>
        private bool TryReadNumber(string formula, ref int position, out FormulaToken token)
        {
            token = null;
            var start = position;

            // Check if starts with digit or decimal point
            if (!char.IsDigit(formula[position]) && formula[position] != '.')
            {
                return false;
            }

            // Read number (integer or decimal)
            var sb = new StringBuilder();
            while (position < formula.Length &&
                   (char.IsDigit(formula[position]) || formula[position] == '.'))
            {
                sb.Append(formula[position]);
                position++;
            }

            // Skip whitespace before unit
            while (position < formula.Length && char.IsWhiteSpace(formula[position]))
            {
                position++;
            }

            // Try to read unit
            string unit = null;
            if (position < formula.Length && char.IsLetter(formula[position]))
            {
                var unitStart = position;
                while (position < formula.Length &&
                       (char.IsLetter(formula[position]) || char.IsWhiteSpace(formula[position])))
                {
                    position++;
                }

                unit = formula.Substring(unitStart, position - unitStart).Trim();

                // Validate unit
                if (!ValidUnits.Contains(unit))
                {
                    // Not a valid unit, backtrack
                    position = unitStart;
                    unit = null;
                }
            }

            token = new FormulaToken
            {
                Type = TokenType.Number,
                Value = sb.ToString(),
                Unit = unit,
                Position = start
            };

            return true;
        }

        /// <summary>
        /// Tries to read a parameter reference (quoted or bracketed).
        /// </summary>
        private bool TryReadParameter(string formula, ref int position, out FormulaToken token)
        {
            token = null;

            // Parameters can be enclosed in quotes or square brackets
            char? closingChar = null;
            if (formula[position] == '"')
            {
                closingChar = '"';
            }
            else if (formula[position] == '[')
            {
                closingChar = ']';
            }
            else
            {
                return false;
            }

            var start = position;
            position++; // Skip opening quote/bracket

            var sb = new StringBuilder();
            while (position < formula.Length && formula[position] != closingChar)
            {
                sb.Append(formula[position]);
                position++;
            }

            if (position >= formula.Length)
            {
                throw new FormulaParseException($"Unclosed parameter reference at position {start}");
            }

            position++; // Skip closing quote/bracket

            token = new FormulaToken
            {
                Type = TokenType.Parameter,
                Value = sb.ToString(),
                Position = start
            };

            return true;
        }

        /// <summary>
        /// Tries to read a function name.
        /// </summary>
        private bool TryReadFunction(string formula, ref int position, out FormulaToken token)
        {
            token = null;

            if (!char.IsLetter(formula[position]))
            {
                return false;
            }

            var start = position;
            var sb = new StringBuilder();

            while (position < formula.Length && char.IsLetter(formula[position]))
            {
                sb.Append(formula[position]);
                position++;
            }

            var functionName = sb.ToString();

            // Skip whitespace
            while (position < formula.Length && char.IsWhiteSpace(formula[position]))
            {
                position++;
            }

            // Must be followed by '(' to be a function
            if (position < formula.Length && formula[position] == '(')
            {
                if (BuiltInFunctions.Contains(functionName))
                {
                    token = new FormulaToken
                    {
                        Type = TokenType.Function,
                        Value = functionName.ToLower(),
                        Position = start
                    };
                    return true;
                }
                else
                {
                    throw new FormulaParseException($"Unknown function '{functionName}' at position {start}");
                }
            }

            // Not a function, backtrack
            position = start;
            return false;
        }

        /// <summary>
        /// Tries to read an operator.
        /// </summary>
        private bool TryReadOperator(string formula, ref int position, out FormulaToken token)
        {
            token = null;
            var start = position;

            // Try two-character operators first
            if (position + 1 < formula.Length)
            {
                var twoChar = formula.Substring(position, 2);
                if (OperatorPrecedence.ContainsKey(twoChar))
                {
                    token = new FormulaToken
                    {
                        Type = TokenType.Operator,
                        Value = twoChar,
                        Position = start
                    };
                    position += 2;
                    return true;
                }
            }

            // Try single-character operators
            var oneChar = formula[position].ToString();
            if (OperatorPrecedence.ContainsKey(oneChar))
            {
                token = new FormulaToken
                {
                    Type = TokenType.Operator,
                    Value = oneChar,
                    Position = start
                };
                position++;
                return true;
            }

            // Try word operators (and, or, not)
            if (char.IsLetter(formula[position]))
            {
                var sb = new StringBuilder();
                while (position < formula.Length && char.IsLetter(formula[position]))
                {
                    sb.Append(formula[position]);
                    position++;
                }

                var word = sb.ToString().ToLower();
                if (OperatorPrecedence.ContainsKey(word))
                {
                    token = new FormulaToken
                    {
                        Type = TokenType.Operator,
                        Value = word,
                        Position = start
                    };
                    return true;
                }

                // Not an operator, backtrack
                position = start;
            }

            return false;
        }

        /// <summary>
        /// Tries to read a parenthesis.
        /// </summary>
        private bool TryReadParenthesis(string formula, ref int position, out FormulaToken token)
        {
            token = null;
            var start = position;

            if (formula[position] == '(')
            {
                token = new FormulaToken
                {
                    Type = TokenType.LeftParen,
                    Value = "(",
                    Position = start
                };
                position++;
                return true;
            }
            else if (formula[position] == ')')
            {
                token = new FormulaToken
                {
                    Type = TokenType.RightParen,
                    Value = ")",
                    Position = start
                };
                position++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to read a comma (function argument separator).
        /// </summary>
        private bool TryReadComma(string formula, ref int position, out FormulaToken token)
        {
            token = null;
            var start = position;

            if (formula[position] == ',')
            {
                token = new FormulaToken
                {
                    Type = TokenType.Comma,
                    Value = ",",
                    Position = start
                };
                position++;
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - Validation

        /// <summary>
        /// Validates formula syntax.
        /// </summary>
        private void ValidateSyntax(List<FormulaToken> tokens)
        {
            if (tokens.Count == 0)
            {
                throw new FormulaParseException("Formula is empty");
            }

            // Check parentheses balance
            ValidateParentheses(tokens);

            // Check operator/operand sequence
            ValidateSequence(tokens);

            // Check function argument counts
            ValidateFunctions(tokens);
        }

        /// <summary>
        /// Validates that parentheses are balanced.
        /// </summary>
        private void ValidateParentheses(List<FormulaToken> tokens)
        {
            var depth = 0;

            foreach (var token in tokens)
            {
                if (token.Type == TokenType.LeftParen)
                {
                    depth++;
                }
                else if (token.Type == TokenType.RightParen)
                {
                    depth--;
                    if (depth < 0)
                    {
                        throw new FormulaParseException($"Unmatched closing parenthesis at position {token.Position}");
                    }
                }
            }

            if (depth != 0)
            {
                throw new FormulaParseException("Unclosed parenthesis");
            }
        }

        /// <summary>
        /// Validates operator/operand sequence.
        /// </summary>
        private void ValidateSequence(List<FormulaToken> tokens)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                // Operators (except 'not') need operands on both sides
                if (token.Type == TokenType.Operator && token.Value.ToLower() != "not")
                {
                    // Check left side
                    if (i == 0)
                    {
                        throw new FormulaParseException($"Operator '{token.Value}' at position {token.Position} is missing left operand");
                    }

                    var leftToken = tokens[i - 1];
                    if (leftToken.Type != TokenType.Number &&
                        leftToken.Type != TokenType.Parameter &&
                        leftToken.Type != TokenType.RightParen)
                    {
                        throw new FormulaParseException($"Invalid left operand for operator '{token.Value}' at position {token.Position}");
                    }

                    // Check right side
                    if (i == tokens.Count - 1)
                    {
                        throw new FormulaParseException($"Operator '{token.Value}' at position {token.Position} is missing right operand");
                    }
                }
            }
        }

        /// <summary>
        /// Validates function argument counts.
        /// </summary>
        private void ValidateFunctions(List<FormulaToken> tokens)
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type != TokenType.Function)
                {
                    continue;
                }

                var functionName = tokens[i].Value.ToLower();

                // Find the matching closing parenthesis
                if (i + 1 >= tokens.Count || tokens[i + 1].Type != TokenType.LeftParen)
                {
                    throw new FormulaParseException($"Function '{functionName}' at position {tokens[i].Position} is missing opening parenthesis");
                }

                // Count arguments
                var argCount = CountFunctionArguments(tokens, i + 1);

                // Validate argument count
                ValidateFunctionArgumentCount(functionName, argCount, tokens[i].Position);
            }
        }

        /// <summary>
        /// Counts the number of arguments in a function call.
        /// </summary>
        private int CountFunctionArguments(List<FormulaToken> tokens, int openParenIndex)
        {
            var depth = 1;
            var argCount = 0;
            var hasContent = false;

            for (var i = openParenIndex + 1; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Type == TokenType.LeftParen)
                {
                    depth++;
                    hasContent = true;
                }
                else if (token.Type == TokenType.RightParen)
                {
                    depth--;
                    if (depth == 0)
                    {
                        // End of function arguments
                        if (hasContent)
                        {
                            argCount++;
                        }
                        break;
                    }
                    hasContent = true;
                }
                else if (token.Type == TokenType.Comma && depth == 1)
                {
                    argCount++;
                    hasContent = false;
                }
                else
                {
                    hasContent = true;
                }
            }

            return argCount;
        }

        /// <summary>
        /// Validates function argument count.
        /// </summary>
        private void ValidateFunctionArgumentCount(string functionName, int argCount, int position)
        {
            // Define expected argument counts for each function
            var expectedArgs = new Dictionary<string, (int min, int max)>
            {
                { "if", (3, 3) },
                { "abs", (1, 1) },
                { "acos", (1, 1) },
                { "asin", (1, 1) },
                { "atan", (1, 1) },
                { "cos", (1, 1) },
                { "exp", (1, 1) },
                { "ln", (1, 1) },
                { "log", (1, 1) },
                { "round", (1, 1) },
                { "rounddown", (1, 1) },
                { "roundup", (1, 1) },
                { "sin", (1, 1) },
                { "sqrt", (1, 1) },
                { "tan", (1, 1) },
                { "pi", (0, 0) },
                { "max", (2, int.MaxValue) },
                { "min", (2, int.MaxValue) },
                { "ceiling", (1, 1) },
                { "floor", (1, 1) }
            };

            if (expectedArgs.TryGetValue(functionName, out var range))
            {
                if (argCount < range.min || argCount > range.max)
                {
                    if (range.min == range.max)
                    {
                        throw new FormulaParseException($"Function '{functionName}' at position {position} expects {range.min} argument(s), but got {argCount}");
                    }
                    else
                    {
                        throw new FormulaParseException($"Function '{functionName}' at position {position} expects {range.min}-{range.max} arguments, but got {argCount}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets operator precedence.
        /// </summary>
        private int GetPrecedence(string op)
        {
            return OperatorPrecedence.TryGetValue(op, out var precedence) ? precedence : 0;
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Represents a token in a formula.
    /// </summary>
    public class FormulaToken
    {
        /// <summary>
        /// Token type
        /// </summary>
        public TokenType Type { get; set; }

        /// <summary>
        /// Token value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Unit (for numeric tokens)
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Position in original formula
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Returns string representation of token
        /// </summary>
        public override string ToString()
        {
            var result = $"{Type}: {Value}";
            if (!string.IsNullOrEmpty(Unit))
            {
                result += $" ({Unit})";
            }
            return result;
        }
    }

    /// <summary>
    /// Token types
    /// </summary>
    public enum TokenType
    {
        Number,
        Parameter,
        Operator,
        Function,
        LeftParen,
        RightParen,
        Comma
    }

    /// <summary>
    /// Exception thrown when formula parsing fails.
    /// </summary>
    public class FormulaParseException : Exception
    {
        public FormulaParseException(string message) : base(message) { }
        public FormulaParseException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion
}
