// ============================================================================
// StingBIM Data - Formula Engine
// Engineering formula evaluation with dependency tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using NLog;

namespace StingBIM.Data.Formulas
{
    /// <summary>
    /// Formula engine for engineering calculations.
    /// Supports dependency tracking and Revit formula syntax.
    /// </summary>
    public class FormulaEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, FormulaDefinition> _formulas;
        private readonly Dictionary<string, List<string>> _dependencyGraph;

        // Regex for parsing formula variables
        private static readonly Regex VariablePattern = new Regex(
            @"\{([A-Za-z_][A-Za-z0-9_]*)\}",
            RegexOptions.Compiled);

        // Regex for parsing function calls
        private static readonly Regex FunctionPattern = new Regex(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)",
            RegexOptions.Compiled);

        public FormulaEngine()
        {
            _formulas = new Dictionary<string, FormulaDefinition>(StringComparer.OrdinalIgnoreCase);
            _dependencyGraph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            RegisterBuiltInFormulas();
        }

        /// <summary>
        /// Loads formulas from a CSV file.
        /// </summary>
        public void LoadFromCsv(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Warn($"Formula file not found: {filePath}");
                return;
            }

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null
                };

                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);

                var records = csv.GetRecords<FormulaCsvRecord>();

                foreach (var record in records)
                {
                    RegisterFormula(new FormulaDefinition
                    {
                        Id = record.FormulaId,
                        Name = record.Name,
                        Expression = record.Expression,
                        Description = record.Description,
                        Category = record.Category,
                        Unit = record.Unit,
                        InputParameters = ParseParameters(record.InputParameters),
                        OutputType = ParseOutputType(record.OutputType)
                    });
                }

                Logger.Info($"Loaded {_formulas.Count} formulas from {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load formulas from {filePath}");
            }
        }

        /// <summary>
        /// Registers a formula definition.
        /// </summary>
        public void RegisterFormula(FormulaDefinition formula)
        {
            _formulas[formula.Id] = formula;

            // Extract dependencies
            var dependencies = ExtractDependencies(formula.Expression);
            _dependencyGraph[formula.Id] = dependencies;

            Logger.Debug($"Registered formula: {formula.Id} with {dependencies.Count} dependencies");
        }

        /// <summary>
        /// Gets a formula by ID.
        /// </summary>
        public FormulaDefinition GetFormula(string formulaId)
        {
            return _formulas.TryGetValue(formulaId, out var formula) ? formula : null;
        }

        /// <summary>
        /// Gets formulas by category.
        /// </summary>
        public IEnumerable<FormulaDefinition> GetFormulasByCategory(string category)
        {
            return _formulas.Values.Where(f =>
                f.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Evaluates a formula with given variable values.
        /// </summary>
        public FormulaResult Evaluate(string formulaId, Dictionary<string, double> variables)
        {
            if (!_formulas.TryGetValue(formulaId, out var formula))
            {
                return new FormulaResult
                {
                    Success = false,
                    ErrorMessage = $"Formula not found: {formulaId}"
                };
            }

            return EvaluateExpression(formula.Expression, variables);
        }

        /// <summary>
        /// Evaluates a raw expression with given variable values.
        /// </summary>
        public FormulaResult EvaluateExpression(string expression, Dictionary<string, double> variables)
        {
            try
            {
                // Replace variables with values
                var processedExpression = SubstituteVariables(expression, variables);

                // Process functions
                processedExpression = ProcessFunctions(processedExpression);

                // Evaluate the expression
                var result = EvaluateMathExpression(processedExpression);

                return new FormulaResult
                {
                    Success = true,
                    Value = result,
                    Expression = expression,
                    ProcessedExpression = processedExpression
                };
            }
            catch (Exception ex)
            {
                return new FormulaResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Expression = expression
                };
            }
        }

        /// <summary>
        /// Converts a natural language description to a Revit formula.
        /// </summary>
        public string NaturalLanguageToFormula(string description)
        {
            var lower = description.ToLowerInvariant();

            // Pattern matching for common formulas
            if (lower.Contains("area") && lower.Contains("width") && lower.Contains("length"))
            {
                return "{Width} * {Length}";
            }
            if (lower.Contains("volume") && (lower.Contains("width") || lower.Contains("area")))
            {
                if (lower.Contains("width") && lower.Contains("length") && lower.Contains("height"))
                {
                    return "{Width} * {Length} * {Height}";
                }
                return "{Area} * {Height}";
            }
            if (lower.Contains("perimeter") && lower.Contains("rectangle"))
            {
                return "2 * ({Width} + {Length})";
            }
            if (lower.Contains("perimeter") && lower.Contains("circle"))
            {
                return "2 * 3.14159 * {Radius}";
            }
            if (lower.Contains("area") && lower.Contains("circle"))
            {
                return "3.14159 * {Radius} * {Radius}";
            }
            if (lower.Contains("total") && lower.Contains("cost"))
            {
                return "{Quantity} * {UnitCost}";
            }

            // Default: return the description as a placeholder
            return $"/* {description} */";
        }

        /// <summary>
        /// Validates a formula expression.
        /// </summary>
        public FormulaValidationResult ValidateExpression(string expression)
        {
            var result = new FormulaValidationResult { IsValid = true };

            // Check for balanced parentheses
            var parenCount = 0;
            foreach (var c in expression)
            {
                if (c == '(') parenCount++;
                if (c == ')') parenCount--;
                if (parenCount < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Unbalanced parentheses");
                    break;
                }
            }
            if (parenCount != 0)
            {
                result.IsValid = false;
                result.Errors.Add("Unbalanced parentheses");
            }

            // Extract and validate variables
            var variables = VariablePattern.Matches(expression)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            result.RequiredVariables = variables;

            // Check for unknown functions
            var functions = FunctionPattern.Matches(expression)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct();

            var knownFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "if", "and", "or", "not", "abs", "round", "roundup", "rounddown",
                "sqrt", "pow", "min", "max", "sin", "cos", "tan", "pi"
            };

            foreach (var func in functions)
            {
                if (!knownFunctions.Contains(func))
                {
                    result.Warnings.Add($"Unknown function: {func}");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the dependency order for evaluating formulas.
        /// </summary>
        public IEnumerable<string> GetEvaluationOrder(IEnumerable<string> formulaIds)
        {
            var visited = new HashSet<string>();
            var result = new List<string>();

            void Visit(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);

                if (_dependencyGraph.TryGetValue(id, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        Visit(dep);
                    }
                }

                result.Add(id);
            }

            foreach (var id in formulaIds)
            {
                Visit(id);
            }

            return result;
        }

        private void RegisterBuiltInFormulas()
        {
            // Area formulas
            RegisterFormula(new FormulaDefinition
            {
                Id = "AREA_RECTANGLE",
                Name = "Rectangle Area",
                Expression = "{Width} * {Length}",
                Category = "Geometry",
                Unit = "m²"
            });

            RegisterFormula(new FormulaDefinition
            {
                Id = "AREA_CIRCLE",
                Name = "Circle Area",
                Expression = "pi() * {Radius} * {Radius}",
                Category = "Geometry",
                Unit = "m²"
            });

            // Volume formulas
            RegisterFormula(new FormulaDefinition
            {
                Id = "VOLUME_BOX",
                Name = "Box Volume",
                Expression = "{Width} * {Length} * {Height}",
                Category = "Geometry",
                Unit = "m³"
            });

            // Thermal formulas
            RegisterFormula(new FormulaDefinition
            {
                Id = "R_VALUE",
                Name = "R-Value Calculation",
                Expression = "{Thickness} / {ThermalConductivity}",
                Category = "Thermal",
                Unit = "m²·K/W"
            });

            RegisterFormula(new FormulaDefinition
            {
                Id = "U_VALUE",
                Name = "U-Value Calculation",
                Expression = "1 / {RValue}",
                Category = "Thermal",
                Unit = "W/(m²·K)"
            });

            // Cost formulas
            RegisterFormula(new FormulaDefinition
            {
                Id = "TOTAL_COST",
                Name = "Total Cost",
                Expression = "{Quantity} * {UnitCost}",
                Category = "Cost",
                Unit = "currency"
            });
        }

        private List<string> ExtractDependencies(string expression)
        {
            return VariablePattern.Matches(expression)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
        }

        private string SubstituteVariables(string expression, Dictionary<string, double> variables)
        {
            return VariablePattern.Replace(expression, match =>
            {
                var varName = match.Groups[1].Value;
                if (variables.TryGetValue(varName, out var value))
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }
                throw new ArgumentException($"Variable not provided: {varName}");
            });
        }

        private string ProcessFunctions(string expression)
        {
            // Process pi()
            expression = Regex.Replace(expression, @"pi\(\)", Math.PI.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);

            // Process sqrt(x)
            expression = Regex.Replace(expression, @"sqrt\(([^)]+)\)", match =>
            {
                var inner = EvaluateMathExpression(match.Groups[1].Value);
                return Math.Sqrt(inner).ToString(CultureInfo.InvariantCulture);
            }, RegexOptions.IgnoreCase);

            // Process abs(x)
            expression = Regex.Replace(expression, @"abs\(([^)]+)\)", match =>
            {
                var inner = EvaluateMathExpression(match.Groups[1].Value);
                return Math.Abs(inner).ToString(CultureInfo.InvariantCulture);
            }, RegexOptions.IgnoreCase);

            return expression;
        }

        private double EvaluateMathExpression(string expression)
        {
            // Simple expression evaluator using DataTable.Compute
            var table = new System.Data.DataTable();
            var result = table.Compute(expression, "");
            return Convert.ToDouble(result);
        }

        private List<ParameterInfo> ParseParameters(string parameterString)
        {
            if (string.IsNullOrWhiteSpace(parameterString))
                return new List<ParameterInfo>();

            return parameterString.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p =>
                {
                    var parts = p.Split(':');
                    return new ParameterInfo
                    {
                        Name = parts[0].Trim(),
                        Type = parts.Length > 1 ? parts[1].Trim() : "Number"
                    };
                })
                .ToList();
        }

        private FormulaOutputType ParseOutputType(string outputType)
        {
            return outputType?.ToLowerInvariant() switch
            {
                "number" => FormulaOutputType.Number,
                "length" => FormulaOutputType.Length,
                "area" => FormulaOutputType.Area,
                "volume" => FormulaOutputType.Volume,
                "angle" => FormulaOutputType.Angle,
                "currency" => FormulaOutputType.Currency,
                "boolean" => FormulaOutputType.Boolean,
                _ => FormulaOutputType.Number
            };
        }
    }

    /// <summary>
    /// Formula definition.
    /// </summary>
    public class FormulaDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Expression { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public List<ParameterInfo> InputParameters { get; set; } = new List<ParameterInfo>();
        public FormulaOutputType OutputType { get; set; }
    }

    /// <summary>
    /// Parameter information for formulas.
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public double? DefaultValue { get; set; }
    }

    /// <summary>
    /// Result of formula evaluation.
    /// </summary>
    public class FormulaResult
    {
        public bool Success { get; set; }
        public double Value { get; set; }
        public string ErrorMessage { get; set; }
        public string Expression { get; set; }
        public string ProcessedExpression { get; set; }
    }

    /// <summary>
    /// Result of formula validation.
    /// </summary>
    public class FormulaValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> RequiredVariables { get; set; } = new List<string>();
    }

    /// <summary>
    /// Formula output types.
    /// </summary>
    public enum FormulaOutputType
    {
        Number,
        Length,
        Area,
        Volume,
        Angle,
        Currency,
        Boolean
    }

    /// <summary>
    /// CSV record for formula import.
    /// </summary>
    internal class FormulaCsvRecord
    {
        public string FormulaId { get; set; }
        public string Name { get; set; }
        public string Expression { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
        public string InputParameters { get; set; }
        public string OutputType { get; set; }
    }
}
