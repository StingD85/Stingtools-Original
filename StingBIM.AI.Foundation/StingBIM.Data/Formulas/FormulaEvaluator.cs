using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.Data.Formulas
{
    /// <summary>
    /// Evaluates parsed formulas and calculates results.
    /// Handles all Revit formula operations including arithmetic, comparisons, functions, and parameter lookups.
    /// </summary>
    public class FormulaEvaluator
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly FormulaParser _parser;
        private Dictionary<string, Func<double, double>> _unaryFunctions;
        private Dictionary<string, Func<double, double, double>> _binaryFunctions;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FormulaEvaluator"/> class.
        /// </summary>
        public FormulaEvaluator()
        {
            _parser = new FormulaParser();
            InitializeFunctions();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Evaluates a formula for a specific element.
        /// </summary>
        /// <param name="formula">The formula string to evaluate</param>
        /// <param name="element">The Revit element providing parameter values</param>
        /// <returns>The calculated result</returns>
        /// <exception cref="ArgumentNullException">Thrown if formula or element is null</exception>
        /// <exception cref="FormulaEvaluationException">Thrown if evaluation fails</exception>
        public double Evaluate(string formula, Element element)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            try
            {
                Logger.Debug($"Evaluating formula for element {element.Id}: {formula}");

                // Parse formula
                var tokens = _parser.Parse(formula);

                // Convert to RPN
                var rpn = _parser.ToRPN(tokens);

                // Evaluate RPN expression
                var result = EvaluateRPN(rpn, element);

                Logger.Debug($"Formula result: {result}");
                return result;
            }
            catch (FormulaParseException ex)
            {
                Logger.Error(ex, $"Parse error in formula: {formula}");
                throw new FormulaEvaluationException($"Failed to parse formula: {ex.Message}", ex);
            }
            catch (FormulaEvaluationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error evaluating formula: {formula}");
                throw new FormulaEvaluationException($"Failed to evaluate formula: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Evaluates a formula with provided parameter values.
        /// </summary>
        /// <param name="formula">The formula string to evaluate</param>
        /// <param name="parameterValues">Dictionary of parameter names to values</param>
        /// <returns>The calculated result</returns>
        public double Evaluate(string formula, Dictionary<string, double> parameterValues)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (parameterValues == null)
            {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            try
            {
                Logger.Debug($"Evaluating formula with {parameterValues.Count} parameter values: {formula}");

                // Parse formula
                var tokens = _parser.Parse(formula);

                // Convert to RPN
                var rpn = _parser.ToRPN(tokens);

                // Evaluate RPN expression
                var result = EvaluateRPN(rpn, parameterValues);

                Logger.Debug($"Formula result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error evaluating formula: {formula}");
                throw new FormulaEvaluationException($"Failed to evaluate formula: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Batch evaluates a formula for multiple elements.
        /// </summary>
        /// <param name="formula">The formula string to evaluate</param>
        /// <param name="elements">Collection of elements to evaluate</param>
        /// <returns>Dictionary mapping element IDs to calculated results</returns>
        public Dictionary<ElementId, double> EvaluateBatch(string formula, IEnumerable<Element> elements)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            var results = new Dictionary<ElementId, double>();
            var elementList = elements.ToList();

            Logger.Info($"Batch evaluating formula for {elementList.Count} elements");

            // Parse once, evaluate many times
            var tokens = _parser.Parse(formula);
            var rpn = _parser.ToRPN(tokens);

            foreach (var element in elementList)
            {
                try
                {
                    var result = EvaluateRPN(rpn, element);
                    results[element.Id] = result;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to evaluate formula for element {element.Id}");
                    // Continue with other elements
                }
            }

            Logger.Info($"Successfully evaluated {results.Count} of {elementList.Count} elements");
            return results;
        }

        /// <summary>
        /// Tests if a formula can be evaluated without errors.
        /// </summary>
        /// <param name="formula">The formula string to test</param>
        /// <param name="parameterValues">Dictionary of parameter names to test values</param>
        /// <param name="result">The calculated result if successful</param>
        /// <returns>True if evaluation succeeds; otherwise, false</returns>
        public bool TryEvaluate(string formula, Dictionary<string, double> parameterValues, out double result)
        {
            result = 0;

            try
            {
                result = Evaluate(formula, parameterValues);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods - RPN Evaluation

        /// <summary>
        /// Evaluates an RPN expression using element parameters.
        /// </summary>
        private double EvaluateRPN(List<FormulaToken> rpn, Element element)
        {
            var stack = new Stack<double>();

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                        stack.Push(ParseNumber(token));
                        break;

                    case TokenType.Parameter:
                        var value = GetParameterValue(element, token.Value);
                        stack.Push(value);
                        break;

                    case TokenType.Operator:
                        EvaluateOperator(stack, token.Value);
                        break;

                    case TokenType.Function:
                        EvaluateFunction(stack, token.Value);
                        break;

                    default:
                        throw new FormulaEvaluationException($"Unexpected token type in RPN: {token.Type}");
                }
            }

            if (stack.Count != 1)
            {
                throw new FormulaEvaluationException($"Invalid RPN expression: stack has {stack.Count} values (expected 1)");
            }

            return stack.Pop();
        }

        /// <summary>
        /// Evaluates an RPN expression using parameter dictionary.
        /// </summary>
        private double EvaluateRPN(List<FormulaToken> rpn, Dictionary<string, double> parameterValues)
        {
            var stack = new Stack<double>();

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                        stack.Push(ParseNumber(token));
                        break;

                    case TokenType.Parameter:
                        if (!parameterValues.TryGetValue(token.Value, out var value))
                        {
                            throw new FormulaEvaluationException($"Parameter '{token.Value}' not found in provided values");
                        }
                        stack.Push(value);
                        break;

                    case TokenType.Operator:
                        EvaluateOperator(stack, token.Value);
                        break;

                    case TokenType.Function:
                        EvaluateFunction(stack, token.Value);
                        break;

                    default:
                        throw new FormulaEvaluationException($"Unexpected token type in RPN: {token.Type}");
                }
            }

            if (stack.Count != 1)
            {
                throw new FormulaEvaluationException($"Invalid RPN expression: stack has {stack.Count} values (expected 1)");
            }

            return stack.Pop();
        }

        /// <summary>
        /// Evaluates an operator on the stack.
        /// </summary>
        private void EvaluateOperator(Stack<double> stack, string op)
        {
            if (op.ToLower() == "not")
            {
                // Unary operator
                if (stack.Count < 1)
                {
                    throw new FormulaEvaluationException($"Insufficient operands for operator '{op}'");
                }

                var operand = stack.Pop();
                var result = operand == 0 ? 1 : 0; // NOT operation (0 = false, non-zero = true)
                stack.Push(result);
            }
            else
            {
                // Binary operator
                if (stack.Count < 2)
                {
                    throw new FormulaEvaluationException($"Insufficient operands for operator '{op}'");
                }

                var right = stack.Pop();
                var left = stack.Pop();
                var result = ApplyOperator(left, right, op);
                stack.Push(result);
            }
        }

        /// <summary>
        /// Applies a binary operator.
        /// </summary>
        private double ApplyOperator(double left, double right, string op)
        {
            switch (op)
            {
                case "+":
                    return left + right;

                case "-":
                    return left - right;

                case "*":
                    return left * right;

                case "/":
                    if (Math.Abs(right) < 1e-10)
                    {
                        throw new FormulaEvaluationException("Division by zero");
                    }
                    return left / right;

                case "^":
                    return Math.Pow(left, right);

                case "=":
                    return Math.Abs(left - right) < 1e-10 ? 1 : 0;

                case "<>":
                    return Math.Abs(left - right) >= 1e-10 ? 1 : 0;

                case "<":
                    return left < right ? 1 : 0;

                case ">":
                    return left > right ? 1 : 0;

                case "<=":
                    return left <= right ? 1 : 0;

                case ">=":
                    return left >= right ? 1 : 0;

                case "and":
                    return (left != 0 && right != 0) ? 1 : 0;

                case "or":
                    return (left != 0 || right != 0) ? 1 : 0;

                default:
                    throw new FormulaEvaluationException($"Unknown operator: {op}");
            }
        }

        /// <summary>
        /// Evaluates a function on the stack.
        /// </summary>
        private void EvaluateFunction(Stack<double> stack, string functionName)
        {
            var func = functionName.ToLower();

            switch (func)
            {
                case "if":
                    EvaluateIfFunction(stack);
                    break;

                case "max":
                case "min":
                    EvaluateMinMaxFunction(stack, func);
                    break;

                case "pi":
                    stack.Push(Math.PI);
                    break;

                default:
                    // Unary functions
                    if (_unaryFunctions.TryGetValue(func, out var unaryFunc))
                    {
                        if (stack.Count < 1)
                        {
                            throw new FormulaEvaluationException($"Insufficient arguments for function '{functionName}'");
                        }
                        var arg = stack.Pop();
                        var result = unaryFunc(arg);
                        stack.Push(result);
                    }
                    else
                    {
                        throw new FormulaEvaluationException($"Unknown function: {functionName}");
                    }
                    break;
            }
        }

        /// <summary>
        /// Evaluates the IF function.
        /// </summary>
        private void EvaluateIfFunction(Stack<double> stack)
        {
            if (stack.Count < 3)
            {
                throw new FormulaEvaluationException("IF function requires 3 arguments");
            }

            var falseValue = stack.Pop();
            var trueValue = stack.Pop();
            var condition = stack.Pop();

            var result = condition != 0 ? trueValue : falseValue;
            stack.Push(result);
        }

        /// <summary>
        /// Evaluates MIN/MAX functions (variable arguments).
        /// </summary>
        private void EvaluateMinMaxFunction(Stack<double> stack, string functionName)
        {
            if (stack.Count < 2)
            {
                throw new FormulaEvaluationException($"{functionName.ToUpper()} function requires at least 2 arguments");
            }

            // Collect all arguments (for simplicity, assume 2 args in this implementation)
            var arg2 = stack.Pop();
            var arg1 = stack.Pop();

            var result = functionName == "max" ? Math.Max(arg1, arg2) : Math.Min(arg1, arg2);
            stack.Push(result);
        }

        #endregion

        #region Private Methods - Helper Functions

        /// <summary>
        /// Initializes built-in functions.
        /// </summary>
        private void InitializeFunctions()
        {
            _unaryFunctions = new Dictionary<string, Func<double, double>>
            {
                { "abs", Math.Abs },
                { "acos", Math.Acos },
                { "asin", Math.Asin },
                { "atan", Math.Atan },
                { "cos", Math.Cos },
                { "exp", Math.Exp },
                { "ln", Math.Log },
                { "log", Math.Log10 },
                { "round", x => Math.Round(x) },
                { "rounddown", x => Math.Floor(x) },
                { "roundup", x => Math.Ceiling(x) },
                { "sin", Math.Sin },
                { "sqrt", x =>
                    {
                        if (x < 0)
                        {
                            throw new FormulaEvaluationException("Cannot take square root of negative number");
                        }
                        return Math.Sqrt(x);
                    }
                },
                { "tan", Math.Tan },
                { "ceiling", Math.Ceiling },
                { "floor", Math.Floor }
            };

            _binaryFunctions = new Dictionary<string, Func<double, double, double>>
            {
                { "max", Math.Max },
                { "min", Math.Min }
            };
        }

        /// <summary>
        /// Parses a number token to double value.
        /// </summary>
        private double ParseNumber(FormulaToken token)
        {
            if (!double.TryParse(token.Value, out var value))
            {
                throw new FormulaEvaluationException($"Invalid number: {token.Value}");
            }

            // Unit conversion would go here if needed
            // For now, assume all values are in internal Revit units

            return value;
        }

        /// <summary>
        /// Gets parameter value from element.
        /// </summary>
        private double GetParameterValue(Element element, string parameterName)
        {
            // Try to get parameter by name
            var parameter = element.LookupParameter(parameterName);

            if (parameter == null)
            {
                throw new FormulaEvaluationException($"Parameter '{parameterName}' not found on element {element.Id}");
            }

            // Get value based on storage type
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    return parameter.AsDouble();

                case StorageType.Integer:
                    return parameter.AsInteger();

                case StorageType.String:
                    // Try to parse string as number
                    var stringValue = parameter.AsString();
                    if (double.TryParse(stringValue, out var parsedValue))
                    {
                        return parsedValue;
                    }
                    throw new FormulaEvaluationException($"Parameter '{parameterName}' contains non-numeric value: {stringValue}");

                case StorageType.ElementId:
                    // Return element ID as integer
                    return parameter.AsElementId().Value;

                default:
                    throw new FormulaEvaluationException($"Parameter '{parameterName}' has unsupported storage type: {parameter.StorageType}");
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Exception thrown when formula evaluation fails.
    /// </summary>
    public class FormulaEvaluationException : Exception
    {
        public FormulaEvaluationException(string message) : base(message) { }
        public FormulaEvaluationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Result of formula evaluation.
    /// </summary>
    public class FormulaEvaluationResult
    {
        /// <summary>
        /// Indicates if evaluation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The calculated result (if successful)
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Error message (if failed)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Exception that occurred (if failed)
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static FormulaEvaluationResult CreateSuccess(double value)
        {
            return new FormulaEvaluationResult
            {
                Success = true,
                Value = value
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static FormulaEvaluationResult CreateFailure(string errorMessage, Exception exception = null)
        {
            return new FormulaEvaluationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Batch evaluation results.
    /// </summary>
    public class BatchEvaluationResult
    {
        /// <summary>
        /// Successfully evaluated results (ElementId -> Value)
        /// </summary>
        public Dictionary<ElementId, double> Results { get; set; }

        /// <summary>
        /// Failed evaluations (ElementId -> Error)
        /// </summary>
        public Dictionary<ElementId, string> Errors { get; set; }

        /// <summary>
        /// Total number of elements processed
        /// </summary>
        public int TotalCount => (Results?.Count ?? 0) + (Errors?.Count ?? 0);

        /// <summary>
        /// Number of successful evaluations
        /// </summary>
        public int SuccessCount => Results?.Count ?? 0;

        /// <summary>
        /// Number of failed evaluations
        /// </summary>
        public int FailureCount => Errors?.Count ?? 0;

        /// <summary>
        /// Success rate (0-1)
        /// </summary>
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

        /// <summary>
        /// Constructor
        /// </summary>
        public BatchEvaluationResult()
        {
            Results = new Dictionary<ElementId, double>();
            Errors = new Dictionary<ElementId, string>();
        }

        /// <summary>
        /// Adds a successful result.
        /// </summary>
        public void AddSuccess(ElementId elementId, double value)
        {
            Results[elementId] = value;
        }

        /// <summary>
        /// Adds a failed result.
        /// </summary>
        public void AddFailure(ElementId elementId, string error)
        {
            Errors[elementId] = error;
        }

        /// <summary>
        /// Returns summary string.
        /// </summary>
        public override string ToString()
        {
            return $"Evaluated {TotalCount} elements: {SuccessCount} succeeded, {FailureCount} failed ({SuccessRate:P1} success rate)";
        }
    }

    #endregion
}
