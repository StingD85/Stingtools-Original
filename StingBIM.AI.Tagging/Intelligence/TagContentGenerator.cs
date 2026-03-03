// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagContentGenerator.cs - Smart tag text generation from parameter expressions
// Surpasses Naviate's "parameter updaters" and "Combine Parameters" with full expression
// language including conditionals, arithmetic, formatting, abbreviation, and unique ID generation.
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Inner Types

    /// <summary>
    /// Classification of a token produced by the expression tokenizer.
    /// </summary>
    public enum TokenType
    {
        /// <summary>Literal text that appears verbatim in the output.</summary>
        Literal,

        /// <summary>Reference to a parameter value, e.g., {Door_Number}.</summary>
        ParameterRef,

        /// <summary>Conditional expression: {IF condition THEN value ELSE value}.</summary>
        Conditional,

        /// <summary>Arithmetic expression involving parameter values, e.g., {Width * Height}.</summary>
        Arithmetic,

        /// <summary>Format specifier attached to a parameter or arithmetic token, e.g., :F2.</summary>
        Format,

        /// <summary>Unique ID generator token, e.g., {UNIQUE_ID:D-###}.</summary>
        UniqueId,

        /// <summary>Cluster count meta-variable, e.g., {CLUSTER_COUNT}.</summary>
        ClusterCount
    }

    /// <summary>
    /// A single token extracted from a content expression string.
    /// </summary>
    public class ExpressionToken
    {
        /// <summary>The type of this token.</summary>
        public TokenType Type { get; set; }

        /// <summary>Raw text content of the token.</summary>
        public string Value { get; set; }

        /// <summary>Optional .NET format specifier (e.g., "F2", "N1").</summary>
        public string FormatSpecifier { get; set; }

        /// <summary>For conditional tokens: the condition expression.</summary>
        public string Condition { get; set; }

        /// <summary>For conditional tokens: the THEN branch expression.</summary>
        public string ThenBranch { get; set; }

        /// <summary>For conditional tokens: the ELSE branch expression.</summary>
        public string ElseBranch { get; set; }

        /// <summary>For arithmetic tokens: the left operand parameter name.</summary>
        public string LeftOperand { get; set; }

        /// <summary>For arithmetic tokens: the right operand parameter name or literal.</summary>
        public string RightOperand { get; set; }

        /// <summary>For arithmetic tokens: the operator (+, -, *, /).</summary>
        public char Operator { get; set; }

        /// <summary>For unique ID tokens: the prefix before the sequential number.</summary>
        public string UniqueIdPrefix { get; set; }

        /// <summary>For unique ID tokens: the number of digits (determined by # count).</summary>
        public int UniqueIdDigits { get; set; }

        /// <summary>Returns a debug-friendly representation of this token.</summary>
        public override string ToString() => $"[{Type}] {Value}";
    }

    /// <summary>
    /// Provides parameter name-to-value mappings and metadata for expression evaluation.
    /// All parameter lookups are case-insensitive.
    /// </summary>
    public class ContentEvaluationContext
    {
        /// <summary>Parameter name to value mappings. Keys are case-insensitive.</summary>
        public Dictionary<string, object> Parameters { get; set; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The element's facing direction in degrees (0 = North/Up, 90 = East/Right).
        /// Used for orientation-aware text generation.</summary>
        public double FacingDirectionDegrees { get; set; }

        /// <summary>Number of elements in a cluster (for CLUSTER_COUNT token).</summary>
        public int ClusterCount { get; set; } = 1;

        /// <summary>Unique sequential index for this element within a batch (for UNIQUE_ID).</summary>
        public int SequentialIndex { get; set; }

        /// <summary>Optional element identifier for diagnostics.</summary>
        public string ElementId { get; set; }
    }

    /// <summary>
    /// Result of evaluating a content expression against a context.
    /// </summary>
    public class ContentResult
    {
        /// <summary>The fully evaluated text content ready for display.</summary>
        public string Text { get; set; }

        /// <summary>Whether evaluation completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Warning or error messages produced during evaluation.</summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>Names of parameters that were referenced but not found in the context.</summary>
        public List<string> MissingParameters { get; set; } = new List<string>();

        /// <summary>Abbreviated version of the text, if abbreviation was requested.</summary>
        public string AbbreviatedText { get; set; }
    }

    #endregion

    /// <summary>
    /// Generates smart tag text from parameter expressions. Supports simple parameter references,
    /// formatted values, conditional content, compound expressions, basic arithmetic, unique ID
    /// generation, and cluster count injection. Provides orientation-aware text adjustment,
    /// abbreviation support, and efficient batch evaluation.
    /// <para>
    /// Surpasses Naviate's "parameter updaters" and "Combine Parameters" by unifying all
    /// content generation capabilities into a single expression language with rich formatting,
    /// conditional logic, arithmetic, and intelligent abbreviation.
    /// </para>
    /// </summary>
    public class TagContentGenerator
    {
        #region Private Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Pattern that matches brace-delimited tokens in an expression string.
        /// Captures the content between { and }, handling nested quotes.
        /// </summary>
        private static readonly Regex BracePattern = new Regex(
            @"\{([^{}]+)\}",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern that identifies a conditional expression:
        /// IF &lt;condition&gt; THEN &lt;value&gt; ELSE &lt;value&gt;
        /// </summary>
        private static readonly Regex ConditionalPattern = new Regex(
            @"^IF\s+(.+?)\s+THEN\s+(.+?)\s+ELSE\s+(.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern that identifies a UNIQUE_ID token: UNIQUE_ID:&lt;prefix&gt;&lt;###&gt;
        /// </summary>
        private static readonly Regex UniqueIdPattern = new Regex(
            @"^UNIQUE_ID:(.+?)(#{2,})$",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern that identifies an arithmetic expression: &lt;operand&gt; &lt;op&gt; &lt;operand&gt;
        /// </summary>
        private static readonly Regex ArithmeticPattern = new Regex(
            @"^(.+?)\s*([+\-*/])\s*(.+?)(?::([A-Za-z]\d+))?$",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern that identifies a simple parameter reference with optional format:
        /// ParameterName or ParameterName:Format
        /// </summary>
        private static readonly Regex ParameterRefPattern = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_ ]*?)(?::([A-Za-z]\d+))?$",
            RegexOptions.Compiled);

        /// <summary>
        /// Standard abbreviation dictionary for common BIM/AEC terms.
        /// Maps full word (lowercase) to abbreviated form.
        /// </summary>
        private static readonly Dictionary<string, string> StandardAbbreviations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Room", "Rm" },
                { "Level", "Lvl" },
                { "Floor", "Flr" },
                { "Ceiling", "Clg" },
                { "Corridor", "Corr" },
                { "Mechanical", "Mech" },
                { "Electrical", "Elec" },
                { "Plumbing", "Plmb" },
                { "Conference", "Conf" },
                { "Department", "Dept" },
                { "Building", "Bldg" },
                { "Basement", "Bsmt" },
                { "Bathroom", "Bath" },
                { "Kitchen", "Kit" },
                { "Storage", "Stor" },
                { "Office", "Ofc" },
                { "Laboratory", "Lab" },
                { "Entrance", "Ent" },
                { "Emergency", "Emrg" },
                { "Assembly", "Assy" },
                { "Number", "No" },
                { "Diameter", "Dia" },
                { "Temperature", "Temp" },
                { "Maximum", "Max" },
                { "Minimum", "Min" },
                { "Approximate", "Approx" },
                { "Reference", "Ref" },
                { "Specification", "Spec" },
                { "Drawing", "Dwg" },
                { "Foundation", "Fdn" },
                { "Structural", "Str" },
                { "Residential", "Res" },
                { "Commercial", "Comm" },
                { "Industrial", "Ind" },
                { "Ventilation", "Vent" },
                { "Insulation", "Ins" },
                { "Concrete", "Conc" },
                { "Reinforced", "Reinf" },
                { "Stainless", "SS" },
                { "Galvanized", "Galv" },
                { "Aluminum", "Alum" },
                { "Dimensions", "Dims" },
                { "Elevation", "Elev" },
                { "Section", "Sect" },
                { "Typical", "Typ" },
                { "Existing", "Exist" },
                { "Required", "Req" },
                { "Partition", "Part" },
                { "Interior", "Int" },
                { "Exterior", "Ext" },
                { "Stairway", "Stair" },
                { "Sprinkler", "Sprk" },
                { "Recessed", "Rec" },
                { "Suspended", "Susp" },
                { "Continuous", "Cont" },
                { "Miscellaneous", "Misc" },
                { "Equipment", "Equip" },
                { "Condition", "Cond" },
                { "Pressure", "Press" },
                { "Accessory", "Access" },
                { "Schedule", "Sched" },
                { "Installation", "Install" },
                { "Connection", "Conn" }
            };

        /// <summary>
        /// Thread-safe counter for unique ID generation within a session.
        /// Keyed by prefix pattern so different ID patterns maintain independent sequences.
        /// </summary>
        private readonly Dictionary<string, int> _uniqueIdCounters =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly object _counterLock = new object();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagContentGenerator"/> class.
        /// </summary>
        public TagContentGenerator()
        {
            Logger.Debug("TagContentGenerator initialized");
        }

        #endregion

        #region Public Methods - Expression Evaluation

        /// <summary>
        /// Evaluates a content expression against the given parameter context to produce
        /// display text for a tag.
        /// </summary>
        /// <param name="expression">The content expression to evaluate. Supports parameter
        /// references ({Param}), formatting ({Param:F2}), conditionals
        /// ({IF cond THEN val ELSE val}), arithmetic ({W * H:F2}), unique IDs
        /// ({UNIQUE_ID:D-###}), cluster count ({CLUSTER_COUNT}), and literal text.</param>
        /// <param name="context">Evaluation context containing parameter values and metadata.</param>
        /// <returns>A <see cref="ContentResult"/> containing the evaluated text, success status,
        /// and any diagnostic messages.</returns>
        public ContentResult Evaluate(string expression, ContentEvaluationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var result = new ContentResult();

            if (string.IsNullOrWhiteSpace(expression))
            {
                result.Text = string.Empty;
                result.Success = true;
                return result;
            }

            try
            {
                var tokens = Tokenize(expression);
                var builder = new StringBuilder();

                foreach (var token in tokens)
                {
                    string evaluated = EvaluateToken(token, context, result);
                    builder.Append(evaluated);
                }

                result.Text = builder.ToString();
                result.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to evaluate expression: {0}", expression);
                result.Text = expression;
                result.Success = false;
                result.Messages.Add($"Evaluation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Evaluates a content expression and additionally generates an abbreviated version
        /// of the result text constrained to the specified maximum character count.
        /// </summary>
        /// <param name="expression">The content expression to evaluate.</param>
        /// <param name="context">Evaluation context containing parameter values.</param>
        /// <param name="maxCharacters">Maximum character count for the abbreviated text.</param>
        /// <returns>A <see cref="ContentResult"/> with both full and abbreviated text.</returns>
        public ContentResult EvaluateWithAbbreviation(
            string expression,
            ContentEvaluationContext context,
            int maxCharacters)
        {
            var result = Evaluate(expression, context);

            if (result.Success && result.Text != null)
            {
                result.AbbreviatedText = Abbreviate(result.Text, maxCharacters);
            }

            return result;
        }

        /// <summary>
        /// Evaluates the same content expression against multiple element contexts efficiently.
        /// Tokenization is performed once and reused for every context.
        /// </summary>
        /// <param name="expression">The content expression to evaluate.</param>
        /// <param name="contexts">Collection of evaluation contexts, one per element.</param>
        /// <returns>A list of <see cref="ContentResult"/> in the same order as the input contexts.</returns>
        public List<ContentResult> EvaluateBatch(
            string expression,
            IReadOnlyList<ContentEvaluationContext> contexts)
        {
            if (contexts == null)
                throw new ArgumentNullException(nameof(contexts));

            if (string.IsNullOrWhiteSpace(expression))
            {
                return contexts.Select(_ => new ContentResult
                {
                    Text = string.Empty,
                    Success = true
                }).ToList();
            }

            List<ExpressionToken> tokens;
            try
            {
                tokens = Tokenize(expression);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to tokenize expression for batch: {0}", expression);
                return contexts.Select(_ => new ContentResult
                {
                    Text = expression,
                    Success = false,
                    Messages = { $"Tokenization error: {ex.Message}" }
                }).ToList();
            }

            var results = new List<ContentResult>(contexts.Count);

            for (int i = 0; i < contexts.Count; i++)
            {
                var ctx = contexts[i];
                var result = new ContentResult();

                try
                {
                    var builder = new StringBuilder();
                    foreach (var token in tokens)
                    {
                        builder.Append(EvaluateToken(token, ctx, result));
                    }
                    result.Text = builder.ToString();
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Batch evaluation failed for element index {0}", i);
                    result.Text = expression;
                    result.Success = false;
                    result.Messages.Add($"Evaluation error at index {i}: {ex.Message}");
                }

                results.Add(result);
            }

            Logger.Debug("Batch evaluation complete: {0} elements, expression: {1}",
                contexts.Count, expression);
            return results;
        }

        #endregion

        #region Public Methods - Orientation-Aware Text

        /// <summary>
        /// Adjusts text content based on the element's facing direction. Useful for door swing
        /// labels, window orientation markers, and directional descriptors.
        /// </summary>
        /// <param name="baseText">The base text before orientation adjustment.</param>
        /// <param name="facingDirectionDegrees">Facing direction in degrees
        /// (0 = North, 90 = East, 180 = South, 270 = West).</param>
        /// <param name="orientationMap">Dictionary mapping direction ranges to text replacements.
        /// If null, a default door-swing map is used.</param>
        /// <returns>The orientation-adjusted text.</returns>
        public string AdjustForOrientation(
            string baseText,
            double facingDirectionDegrees,
            Dictionary<string, string> orientationMap = null)
        {
            if (string.IsNullOrEmpty(baseText))
                return baseText;

            if (orientationMap == null)
            {
                orientationMap = BuildDefaultOrientationMap(facingDirectionDegrees);
            }

            string adjusted = baseText;
            foreach (var kvp in orientationMap)
            {
                if (adjusted.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    adjusted = Regex.Replace(
                        adjusted,
                        Regex.Escape(kvp.Key),
                        kvp.Value,
                        RegexOptions.IgnoreCase);
                }
            }

            return adjusted;
        }

        /// <summary>
        /// Determines the cardinal direction label from a facing angle.
        /// </summary>
        /// <param name="facingDirectionDegrees">Facing direction in degrees (0=N, 90=E).</param>
        /// <returns>Cardinal direction string: "North", "East", "South", or "West".</returns>
        public string GetCardinalDirection(double facingDirectionDegrees)
        {
            double normalized = ((facingDirectionDegrees % 360) + 360) % 360;

            if (normalized >= 315 || normalized < 45)
                return "North";
            if (normalized >= 45 && normalized < 135)
                return "East";
            if (normalized >= 135 && normalized < 225)
                return "South";
            return "West";
        }

        /// <summary>
        /// Determines the swing direction label based on element facing.
        /// </summary>
        /// <param name="facingDirectionDegrees">Facing direction in degrees.</param>
        /// <returns>"Left Swing" or "Right Swing" based on the facing quadrant.</returns>
        public string GetSwingDirection(double facingDirectionDegrees)
        {
            double normalized = ((facingDirectionDegrees % 360) + 360) % 360;

            // Convention: facing 0-180 = Left Swing, 180-360 = Right Swing
            return normalized >= 0 && normalized < 180 ? "Left Swing" : "Right Swing";
        }

        #endregion

        #region Public Methods - Abbreviation

        /// <summary>
        /// Generates an abbreviated version of the given text constrained to a maximum
        /// character count. Applies standard BIM/AEC abbreviations first, then truncates
        /// remaining long words if still over the limit.
        /// </summary>
        /// <param name="fullText">The full text to abbreviate.</param>
        /// <param name="maxCharacters">Maximum allowed character count. Must be at least 3.</param>
        /// <returns>The abbreviated text, guaranteed to be at most <paramref name="maxCharacters"/>
        /// characters long.</returns>
        public string Abbreviate(string fullText, int maxCharacters)
        {
            if (string.IsNullOrEmpty(fullText))
                return fullText;

            if (maxCharacters < 3)
                maxCharacters = 3;

            // Already fits
            if (fullText.Length <= maxCharacters)
                return fullText;

            // Phase 1: Apply standard abbreviations
            string abbreviated = ApplyStandardAbbreviations(fullText);
            if (abbreviated.Length <= maxCharacters)
                return abbreviated;

            // Phase 2: Remove vowels from long words (4+ chars), keeping first letter
            abbreviated = RemoveInternalVowels(abbreviated);
            if (abbreviated.Length <= maxCharacters)
                return abbreviated;

            // Phase 3: Truncate words from right to left
            abbreviated = TruncateWords(abbreviated, maxCharacters);
            if (abbreviated.Length <= maxCharacters)
                return abbreviated;

            // Phase 4: Hard truncate with ellipsis
            return abbreviated.Substring(0, maxCharacters - 1) + ".";
        }

        #endregion

        #region Public Methods - Unique ID Management

        /// <summary>
        /// Resets the unique ID counter for the specified prefix pattern.
        /// Call before a new batch tagging session to restart numbering.
        /// </summary>
        /// <param name="prefix">The ID prefix pattern to reset. If null, resets all counters.</param>
        public void ResetUniqueIdCounter(string prefix = null)
        {
            lock (_counterLock)
            {
                if (prefix == null)
                {
                    _uniqueIdCounters.Clear();
                    Logger.Debug("All unique ID counters reset");
                }
                else
                {
                    _uniqueIdCounters.Remove(prefix);
                    Logger.Debug("Unique ID counter reset for prefix: {0}", prefix);
                }
            }
        }

        /// <summary>
        /// Sets the starting value for a unique ID counter with the specified prefix.
        /// </summary>
        /// <param name="prefix">The ID prefix pattern.</param>
        /// <param name="startValue">The starting value (the next generated ID will use this value).</param>
        public void SetUniqueIdStart(string prefix, int startValue)
        {
            lock (_counterLock)
            {
                _uniqueIdCounters[prefix] = startValue - 1;
                Logger.Debug("Unique ID counter for '{0}' set to start at {1}", prefix, startValue);
            }
        }

        #endregion

        #region Private Methods - Tokenizer

        /// <summary>
        /// Breaks a content expression string into a sequence of typed tokens.
        /// Handles literal text, brace-delimited parameter references, conditionals,
        /// arithmetic, unique IDs, and cluster count.
        /// </summary>
        private List<ExpressionToken> Tokenize(string expression)
        {
            var tokens = new List<ExpressionToken>();
            int lastEnd = 0;

            var matches = BracePattern.Matches(expression);

            foreach (Match match in matches)
            {
                // Capture any literal text before this brace token
                if (match.Index > lastEnd)
                {
                    string literalText = expression.Substring(lastEnd, match.Index - lastEnd);
                    tokens.Add(new ExpressionToken
                    {
                        Type = TokenType.Literal,
                        Value = literalText
                    });
                }

                string innerContent = match.Groups[1].Value.Trim();
                tokens.Add(ClassifyToken(innerContent));

                lastEnd = match.Index + match.Length;
            }

            // Capture trailing literal text
            if (lastEnd < expression.Length)
            {
                tokens.Add(new ExpressionToken
                {
                    Type = TokenType.Literal,
                    Value = expression.Substring(lastEnd)
                });
            }

            return tokens;
        }

        /// <summary>
        /// Classifies the inner content of a brace-delimited token into the correct token type.
        /// </summary>
        private ExpressionToken ClassifyToken(string content)
        {
            // Check for CLUSTER_COUNT
            if (string.Equals(content, "CLUSTER_COUNT", StringComparison.OrdinalIgnoreCase))
            {
                return new ExpressionToken
                {
                    Type = TokenType.ClusterCount,
                    Value = content
                };
            }

            // Check for UNIQUE_ID pattern: UNIQUE_ID:prefix###
            var uniqueIdMatch = UniqueIdPattern.Match(content);
            if (uniqueIdMatch.Success)
            {
                return new ExpressionToken
                {
                    Type = TokenType.UniqueId,
                    Value = content,
                    UniqueIdPrefix = uniqueIdMatch.Groups[1].Value,
                    UniqueIdDigits = uniqueIdMatch.Groups[2].Value.Length
                };
            }

            // Check for conditional: IF ... THEN ... ELSE ...
            var conditionalMatch = ConditionalPattern.Match(content);
            if (conditionalMatch.Success)
            {
                return new ExpressionToken
                {
                    Type = TokenType.Conditional,
                    Value = content,
                    Condition = conditionalMatch.Groups[1].Value.Trim(),
                    ThenBranch = conditionalMatch.Groups[2].Value.Trim(),
                    ElseBranch = conditionalMatch.Groups[3].Value.Trim()
                };
            }

            // Check for arithmetic: operand op operand[:format]
            var arithmeticMatch = ArithmeticPattern.Match(content);
            if (arithmeticMatch.Success)
            {
                string leftRaw = arithmeticMatch.Groups[1].Value.Trim();
                string op = arithmeticMatch.Groups[2].Value;
                string rightRaw = arithmeticMatch.Groups[3].Value.Trim();
                string fmt = arithmeticMatch.Groups[4].Success
                    ? arithmeticMatch.Groups[4].Value
                    : null;

                // Verify at least one side looks like a parameter name (not purely numeric)
                bool leftIsParam = !double.TryParse(leftRaw, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out _);
                bool rightIsParam = !double.TryParse(rightRaw, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out _);

                if (leftIsParam || rightIsParam)
                {
                    return new ExpressionToken
                    {
                        Type = TokenType.Arithmetic,
                        Value = content,
                        LeftOperand = leftRaw,
                        Operator = op[0],
                        RightOperand = rightRaw,
                        FormatSpecifier = fmt
                    };
                }
            }

            // Check for simple parameter reference with optional format: ParamName[:Format]
            var paramMatch = ParameterRefPattern.Match(content);
            if (paramMatch.Success)
            {
                return new ExpressionToken
                {
                    Type = TokenType.ParameterRef,
                    Value = paramMatch.Groups[1].Value.Trim(),
                    FormatSpecifier = paramMatch.Groups[2].Success
                        ? paramMatch.Groups[2].Value
                        : null
                };
            }

            // Fallback: treat as literal
            Logger.Warn("Unrecognized expression token, treating as literal: {0}", content);
            return new ExpressionToken
            {
                Type = TokenType.Literal,
                Value = "{" + content + "}"
            };
        }

        #endregion

        #region Private Methods - Token Evaluation

        /// <summary>
        /// Evaluates a single token against the provided context and appends diagnostic
        /// information to the result.
        /// </summary>
        private string EvaluateToken(
            ExpressionToken token,
            ContentEvaluationContext context,
            ContentResult result)
        {
            switch (token.Type)
            {
                case TokenType.Literal:
                    return token.Value;

                case TokenType.ParameterRef:
                    return EvaluateParameterRef(token, context, result);

                case TokenType.Conditional:
                    return EvaluateConditional(token, context, result);

                case TokenType.Arithmetic:
                    return EvaluateArithmetic(token, context, result);

                case TokenType.UniqueId:
                    return EvaluateUniqueId(token, context);

                case TokenType.ClusterCount:
                    return context.ClusterCount.ToString(CultureInfo.InvariantCulture);

                default:
                    result.Messages.Add($"Unknown token type: {token.Type}");
                    return token.Value;
            }
        }

        /// <summary>
        /// Evaluates a parameter reference token by looking up the parameter value in the context.
        /// </summary>
        private string EvaluateParameterRef(
            ExpressionToken token,
            ContentEvaluationContext context,
            ContentResult result)
        {
            if (!context.Parameters.TryGetValue(token.Value, out object value) || value == null)
            {
                if (!result.MissingParameters.Contains(token.Value))
                    result.MissingParameters.Add(token.Value);
                return string.Empty;
            }

            return FormatValue(value, token.FormatSpecifier);
        }

        /// <summary>
        /// Evaluates a conditional token: resolves the condition, then returns the THEN or ELSE branch.
        /// </summary>
        private string EvaluateConditional(
            ExpressionToken token,
            ContentEvaluationContext context,
            ContentResult result)
        {
            bool conditionResult = EvaluateCondition(token.Condition, context);

            string branch = conditionResult ? token.ThenBranch : token.ElseBranch;
            return EvaluateBranchExpression(branch, context, result);
        }

        /// <summary>
        /// Evaluates an arithmetic expression (two operands with a single operator) and formats
        /// the numeric result.
        /// </summary>
        private string EvaluateArithmetic(
            ExpressionToken token,
            ContentEvaluationContext context,
            ContentResult result)
        {
            double leftValue = ResolveNumericOperand(token.LeftOperand, context, result);
            double rightValue = ResolveNumericOperand(token.RightOperand, context, result);

            double computed;
            switch (token.Operator)
            {
                case '+':
                    computed = leftValue + rightValue;
                    break;
                case '-':
                    computed = leftValue - rightValue;
                    break;
                case '*':
                    computed = leftValue * rightValue;
                    break;
                case '/':
                    if (Math.Abs(rightValue) < 1e-15)
                    {
                        result.Messages.Add(
                            $"Division by zero in expression: {token.LeftOperand} / {token.RightOperand}");
                        return "ERR";
                    }
                    computed = leftValue / rightValue;
                    break;
                default:
                    result.Messages.Add($"Unsupported operator: {token.Operator}");
                    return "ERR";
            }

            return FormatValue(computed, token.FormatSpecifier);
        }

        /// <summary>
        /// Generates a unique sequential ID based on the prefix and digit pattern.
        /// </summary>
        private string EvaluateUniqueId(ExpressionToken token, ContentEvaluationContext context)
        {
            int nextValue;
            lock (_counterLock)
            {
                if (!_uniqueIdCounters.TryGetValue(token.UniqueIdPrefix, out int current))
                {
                    current = 0;
                }

                // Use the context's sequential index if it is greater than the current counter,
                // allowing external sequencing to take precedence.
                if (context.SequentialIndex > current)
                {
                    current = context.SequentialIndex;
                }
                else
                {
                    current++;
                }

                _uniqueIdCounters[token.UniqueIdPrefix] = current;
                nextValue = current;
            }

            string numberPart = nextValue.ToString(CultureInfo.InvariantCulture)
                .PadLeft(token.UniqueIdDigits, '0');

            return token.UniqueIdPrefix + numberPart;
        }

        #endregion

        #region Private Methods - Condition Evaluation

        /// <summary>
        /// Evaluates a simple condition expression. Supports:
        /// - ParamName != null / ParamName == null
        /// - ParamName == "value" / ParamName != "value"
        /// - ParamName > number / ParamName &lt; number / ParamName >= number / ParamName &lt;= number
        /// </summary>
        private bool EvaluateCondition(string condition, ContentEvaluationContext context)
        {
            condition = condition.Trim();

            // Pattern: param != null
            var nullCheckNot = Regex.Match(condition, @"^(\S+)\s*!=\s*null$", RegexOptions.IgnoreCase);
            if (nullCheckNot.Success)
            {
                string paramName = nullCheckNot.Groups[1].Value;
                return context.Parameters.TryGetValue(paramName, out object val)
                       && val != null
                       && !string.IsNullOrEmpty(val.ToString());
            }

            // Pattern: param == null
            var nullCheckEq = Regex.Match(condition, @"^(\S+)\s*==\s*null$", RegexOptions.IgnoreCase);
            if (nullCheckEq.Success)
            {
                string paramName = nullCheckEq.Groups[1].Value;
                return !context.Parameters.TryGetValue(paramName, out object val)
                       || val == null
                       || string.IsNullOrEmpty(val.ToString());
            }

            // Pattern: param == "value"
            var eqString = Regex.Match(condition, @"^(\S+)\s*==\s*""(.+?)""$");
            if (eqString.Success)
            {
                string paramName = eqString.Groups[1].Value;
                string expected = eqString.Groups[2].Value;
                if (context.Parameters.TryGetValue(paramName, out object val) && val != null)
                {
                    return string.Equals(val.ToString(), expected, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }

            // Pattern: param != "value"
            var neqString = Regex.Match(condition, @"^(\S+)\s*!=\s*""(.+?)""$");
            if (neqString.Success)
            {
                string paramName = neqString.Groups[1].Value;
                string expected = neqString.Groups[2].Value;
                if (context.Parameters.TryGetValue(paramName, out object val) && val != null)
                {
                    return !string.Equals(val.ToString(), expected, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }

            // Pattern: param > number, param < number, param >= number, param <= number
            var numericComp = Regex.Match(condition, @"^(\S+)\s*(>=|<=|>|<)\s*(\S+)$");
            if (numericComp.Success)
            {
                string paramName = numericComp.Groups[1].Value;
                string op = numericComp.Groups[2].Value;
                string rhsRaw = numericComp.Groups[3].Value;

                if (context.Parameters.TryGetValue(paramName, out object val) && val != null
                    && double.TryParse(val.ToString(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double lhs)
                    && double.TryParse(rhsRaw, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double rhs))
                {
                    switch (op)
                    {
                        case ">": return lhs > rhs;
                        case "<": return lhs < rhs;
                        case ">=": return lhs >= rhs;
                        case "<=": return lhs <= rhs;
                    }
                }

                return false;
            }

            // Fallback: treat condition as a parameter name, truthy if non-null/non-empty
            if (context.Parameters.TryGetValue(condition, out object fallbackVal)
                && fallbackVal != null
                && !string.IsNullOrEmpty(fallbackVal.ToString()))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - Branch Expression Evaluation

        /// <summary>
        /// Evaluates a branch expression from a conditional. Supports string concatenation
        /// with the + operator, quoted literals, and parameter references.
        /// </summary>
        private string EvaluateBranchExpression(
            string branch,
            ContentEvaluationContext context,
            ContentResult result)
        {
            if (string.IsNullOrEmpty(branch))
                return string.Empty;

            // Check for empty string literal
            if (branch == "\"\"")
                return string.Empty;

            // Check for concatenation with +
            if (branch.Contains("+"))
            {
                var parts = SplitConcatenation(branch);
                var builder = new StringBuilder();
                foreach (string part in parts)
                {
                    builder.Append(EvaluateSingleBranchPart(part.Trim(), context, result));
                }
                return builder.ToString();
            }

            return EvaluateSingleBranchPart(branch, context, result);
        }

        /// <summary>
        /// Evaluates a single part of a branch expression (either a quoted literal or
        /// a parameter reference).
        /// </summary>
        private string EvaluateSingleBranchPart(
            string part,
            ContentEvaluationContext context,
            ContentResult result)
        {
            if (string.IsNullOrEmpty(part))
                return string.Empty;

            // Quoted string literal
            if (part.StartsWith("\"") && part.EndsWith("\"") && part.Length >= 2)
            {
                return part.Substring(1, part.Length - 2);
            }

            // Parameter reference
            if (context.Parameters.TryGetValue(part, out object val) && val != null)
            {
                return val.ToString();
            }

            if (!result.MissingParameters.Contains(part))
                result.MissingParameters.Add(part);
            return string.Empty;
        }

        /// <summary>
        /// Splits a concatenation expression by + while respecting quoted strings.
        /// </summary>
        private List<string> SplitConcatenation(string expression)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == '+' && !inQuotes)
                {
                    string segment = current.ToString().Trim();
                    if (segment.Length > 0)
                        parts.Add(segment);
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            string trailing = current.ToString().Trim();
            if (trailing.Length > 0)
                parts.Add(trailing);

            return parts;
        }

        #endregion

        #region Private Methods - Numeric Resolution

        /// <summary>
        /// Resolves an operand string to a numeric value. If the operand is a valid number
        /// literal it is returned directly; otherwise it is treated as a parameter name and
        /// looked up in the context.
        /// </summary>
        private double ResolveNumericOperand(
            string operand,
            ContentEvaluationContext context,
            ContentResult result)
        {
            // Try as numeric literal first
            if (double.TryParse(operand, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double literal))
            {
                return literal;
            }

            // Try as parameter reference
            if (context.Parameters.TryGetValue(operand, out object val) && val != null)
            {
                if (double.TryParse(val.ToString(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double paramValue))
                {
                    return paramValue;
                }

                result.Messages.Add(
                    $"Parameter '{operand}' value '{val}' is not numeric, defaulting to 0");
                return 0.0;
            }

            if (!result.MissingParameters.Contains(operand))
                result.MissingParameters.Add(operand);
            return 0.0;
        }

        #endregion

        #region Private Methods - Formatting

        /// <summary>
        /// Formats a value using the specified .NET format string, or returns the default
        /// string representation if no format is specified.
        /// </summary>
        private string FormatValue(object value, string formatSpecifier)
        {
            if (value == null)
                return string.Empty;

            if (string.IsNullOrEmpty(formatSpecifier))
                return value.ToString();

            // Attempt numeric formatting
            if (double.TryParse(value.ToString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out double numericValue))
            {
                try
                {
                    return numericValue.ToString(formatSpecifier, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    Logger.Warn("Invalid format specifier '{0}' for value '{1}'",
                        formatSpecifier, value);
                    return value.ToString();
                }
            }

            return value.ToString();
        }

        #endregion

        #region Private Methods - Abbreviation Helpers

        /// <summary>
        /// Applies all matching standard abbreviations to the text.
        /// Words are replaced only at word boundaries to avoid partial matches.
        /// </summary>
        private string ApplyStandardAbbreviations(string text)
        {
            string result = text;

            foreach (var kvp in StandardAbbreviations.OrderByDescending(a => a.Key.Length))
            {
                // Replace at word boundaries, case-insensitive
                string pattern = @"\b" + Regex.Escape(kvp.Key) + @"\b";
                result = Regex.Replace(result, pattern, kvp.Value, RegexOptions.IgnoreCase);
            }

            return result;
        }

        /// <summary>
        /// Removes internal vowels from words that are 4 or more characters long, preserving
        /// the first character of each word for readability.
        /// </summary>
        private string RemoveInternalVowels(string text)
        {
            var words = text.Split(' ');
            var processed = new List<string>();

            foreach (string word in words)
            {
                if (word.Length < 4)
                {
                    processed.Add(word);
                    continue;
                }

                var builder = new StringBuilder();
                builder.Append(word[0]); // Always keep first char

                for (int i = 1; i < word.Length; i++)
                {
                    char c = char.ToLower(word[i]);
                    if (c != 'a' && c != 'e' && c != 'i' && c != 'o' && c != 'u')
                    {
                        builder.Append(word[i]);
                    }
                }

                processed.Add(builder.ToString());
            }

            return string.Join(" ", processed);
        }

        /// <summary>
        /// Progressively truncates words from right to left to fit within the character limit.
        /// Each word is truncated to its first 3 characters before moving to the next.
        /// </summary>
        private string TruncateWords(string text, int maxCharacters)
        {
            var words = text.Split(' ').ToList();
            if (words.Count == 0)
                return text;

            // Work from the last word backwards
            for (int i = words.Count - 1; i >= 0; i--)
            {
                string current = string.Join(" ", words);
                if (current.Length <= maxCharacters)
                    return current;

                if (words[i].Length > 3)
                {
                    words[i] = words[i].Substring(0, 3);
                }
                else if (words.Count > 1)
                {
                    // Remove the word entirely if already very short
                    words.RemoveAt(i);
                }
            }

            return string.Join(" ", words);
        }

        #endregion

        #region Private Methods - Orientation Helpers

        /// <summary>
        /// Builds the default orientation map for door-swing and directional text tokens.
        /// </summary>
        private Dictionary<string, string> BuildDefaultOrientationMap(double facingDegrees)
        {
            string cardinal = GetCardinalDirection(facingDegrees);
            string swing = GetSwingDirection(facingDegrees);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "{DIRECTION}", cardinal },
                { "{SWING}", swing },
                { "{FACING}", cardinal + "-Facing" },
                { "Left Swing", swing },
                { "Right Swing", swing },
                { "Inward", facingDegrees >= 0 && facingDegrees < 180 ? "Inward" : "Outward" },
                { "Outward", facingDegrees >= 0 && facingDegrees < 180 ? "Inward" : "Outward" }
            };
        }

        #endregion

        #region Placement Engine Integration

        /// <summary>
        /// Generates tag content text for an element using the specified template.
        /// </summary>
        /// <param name="elementId">The Revit element ID to generate content for.</param>
        /// <param name="template">The tag template definition containing the content expression.</param>
        /// <returns>The generated content text, or a fallback string if generation fails.</returns>
        public string GenerateContent(int elementId, TagTemplateDefinition template)
        {
            if (template == null)
                return $"<{elementId}>";

            string expression = template.ContentExpression;
            if (string.IsNullOrEmpty(expression))
                return $"<{elementId}>";

            var context = new ContentEvaluationContext
            {
                ElementId = elementId.ToString(),
                Parameters = new Dictionary<string, object>
                {
                    { "ElementId", elementId }
                }
            };

            var result = Evaluate(expression, context);
            return result.Success ? result.Text : $"<{elementId}>";
        }

        /// <summary>
        /// Estimates the bounding box dimensions for a tag given its content text,
        /// template, and view scale.
        /// </summary>
        /// <param name="content">The tag display text.</param>
        /// <param name="template">The tag template definition.</param>
        /// <param name="viewScale">The view scale factor (e.g., 100 for 1:100).</param>
        /// <returns>An estimated bounding box for the tag.</returns>
        public TagBounds2D EstimateTagBounds(string content, TagTemplateDefinition template, double viewScale)
        {
            // Estimate character dimensions in model space
            double charWidthModel = 0.002 * (viewScale / 100.0);
            double charHeightModel = 0.003 * (viewScale / 100.0);

            int textLength = string.IsNullOrEmpty(content) ? 5 : content.Length;

            double width = textLength * charWidthModel;
            double height = charHeightModel * 1.5; // Add padding

            // Minimum tag dimensions
            width = Math.Max(width, 0.01);
            height = Math.Max(height, 0.005);

            return new TagBounds2D(0, 0, width, height);
        }

        #endregion
    }
}
