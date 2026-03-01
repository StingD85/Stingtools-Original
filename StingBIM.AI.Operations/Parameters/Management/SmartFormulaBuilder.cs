// SmartFormulaBuilder.cs
// StingBIM AI - Intelligent Formula Creation with Dependency Management
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Parameters.Management
{
    /// <summary>
    /// Smart Formula Builder - Creates and manages formulas in Revit families
    /// with intelligent dependency tracking, validation, and optimization.
    ///
    /// Key Features:
    /// - Natural language formula input ("Area = Width times Height")
    /// - Dependency graph with circular reference detection
    /// - Unit-aware formula validation
    /// - Formula optimization suggestions
    /// - Batch formula application across families
    /// - Formula templates library
    /// - Conditional formula support
    /// - Array/lookup formulas
    /// </summary>
    public class SmartFormulaBuilder
    {
        #region Private Fields

        private readonly Dictionary<string, FormulaDefinition> _formulas;
        private readonly Dictionary<string, FormulaTemplate> _templates;
        private readonly Dictionary<string, List<string>> _dependencyGraph;
        private readonly Dictionary<string, UnitType> _parameterUnits;
        private readonly FormulaParser _parser;
        private readonly FormulaOptimizer _optimizer;
        private readonly NaturalLanguageFormulaInterpreter _nlInterpreter;

        #endregion

        #region Constructor

        public SmartFormulaBuilder()
        {
            _formulas = new Dictionary<string, FormulaDefinition>(StringComparer.OrdinalIgnoreCase);
            _templates = new Dictionary<string, FormulaTemplate>(StringComparer.OrdinalIgnoreCase);
            _dependencyGraph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _parameterUnits = new Dictionary<string, UnitType>(StringComparer.OrdinalIgnoreCase);
            _parser = new FormulaParser();
            _optimizer = new FormulaOptimizer();
            _nlInterpreter = new NaturalLanguageFormulaInterpreter();

            InitializeFormulaTemplates();
            InitializeUnitMappings();
        }

        #endregion

        #region Public Methods - Formula Creation

        /// <summary>
        /// Create a formula from natural language description.
        /// </summary>
        public FormulaCreationResult CreateFromNaturalLanguage(string description, FormulaContext context)
        {
            var result = new FormulaCreationResult();

            try
            {
                // Interpret natural language
                var interpretation = _nlInterpreter.Interpret(description, context);

                if (!interpretation.Success)
                {
                    result.Success = false;
                    result.Errors.AddRange(interpretation.Errors);
                    return result;
                }

                // Build formula
                result = CreateFormula(new FormulaSpecification
                {
                    ResultParameter = interpretation.ResultParameter,
                    Expression = interpretation.Formula,
                    Description = description,
                    Context = context
                });

                result.InterpretedAs = interpretation.Formula;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Failed to interpret: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Create a formula from specification.
        /// </summary>
        public FormulaCreationResult CreateFormula(FormulaSpecification spec)
        {
            var result = new FormulaCreationResult
            {
                ResultParameter = spec.ResultParameter,
                OriginalExpression = spec.Expression
            };

            try
            {
                // 1. Parse the formula
                var parseResult = _parser.Parse(spec.Expression);
                if (!parseResult.Success)
                {
                    result.Success = false;
                    result.Errors.AddRange(parseResult.Errors);
                    return result;
                }

                // 2. Validate parameter references
                var validationResult = ValidateParameterReferences(parseResult.ReferencedParameters, spec.Context);
                if (!validationResult.IsValid)
                {
                    result.Warnings.AddRange(validationResult.Warnings);
                    if (validationResult.Errors.Any())
                    {
                        result.Success = false;
                        result.Errors.AddRange(validationResult.Errors);
                        return result;
                    }
                }

                // 3. Check for circular dependencies
                var circularCheck = CheckCircularDependencies(spec.ResultParameter, parseResult.ReferencedParameters);
                if (circularCheck.HasCircular)
                {
                    result.Success = false;
                    result.Errors.Add($"Circular dependency detected: {string.Join(" -> ", circularCheck.Path)}");
                    return result;
                }

                // 4. Validate units compatibility
                var unitValidation = ValidateUnitCompatibility(spec.ResultParameter, parseResult, spec.Context);
                if (!unitValidation.IsValid)
                {
                    result.Warnings.AddRange(unitValidation.Warnings);
                }

                // 5. Optimize formula if possible
                var optimized = _optimizer.Optimize(parseResult);
                result.OptimizedExpression = optimized.Expression;
                result.OptimizationApplied = optimized.WasOptimized;

                // 6. Generate Revit-compatible formula
                result.RevitFormula = GenerateRevitFormula(optimized);

                // 7. Create formula definition
                var definition = new FormulaDefinition
                {
                    Id = Guid.NewGuid().ToString(),
                    ResultParameter = spec.ResultParameter,
                    Expression = result.OptimizedExpression,
                    RevitFormula = result.RevitFormula,
                    Dependencies = parseResult.ReferencedParameters,
                    Description = spec.Description,
                    CreatedAt = DateTime.Now
                };

                _formulas[spec.ResultParameter] = definition;
                UpdateDependencyGraph(spec.ResultParameter, parseResult.ReferencedParameters);

                result.Success = true;
                result.Formula = definition;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Formula creation failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Create formulas from a template.
        /// </summary>
        public BatchFormulaResult CreateFromTemplate(
            string templateId,
            Dictionary<string, string> parameterMapping,
            FormulaContext context)
        {
            var result = new BatchFormulaResult();

            if (!_templates.TryGetValue(templateId, out var template))
            {
                result.Errors.Add($"Template not found: {templateId}");
                return result;
            }

            foreach (var formulaSpec in template.Formulas)
            {
                // Replace template parameters with actual parameters
                var mappedExpression = MapTemplateParameters(formulaSpec.Expression, parameterMapping);
                var mappedResult = MapTemplateParameter(formulaSpec.ResultParameter, parameterMapping);

                var createResult = CreateFormula(new FormulaSpecification
                {
                    ResultParameter = mappedResult,
                    Expression = mappedExpression,
                    Description = formulaSpec.Description,
                    Context = context
                });

                if (createResult.Success)
                {
                    result.CreatedFormulas.Add(createResult.Formula);
                }
                else
                {
                    result.Errors.AddRange(createResult.Errors.Select(e => $"{formulaSpec.ResultParameter}: {e}"));
                }
            }

            return result;
        }

        /// <summary>
        /// Create conditional formula with if/then/else logic.
        /// </summary>
        public FormulaCreationResult CreateConditionalFormula(ConditionalFormulaSpec spec)
        {
            var result = new FormulaCreationResult
            {
                ResultParameter = spec.ResultParameter
            };

            try
            {
                // Build conditional expression
                var conditionalExpr = BuildConditionalExpression(spec.Conditions, spec.DefaultValue);

                // Validate all branches
                foreach (var condition in spec.Conditions)
                {
                    var branchParse = _parser.Parse(condition.ThenValue);
                    if (!branchParse.Success)
                    {
                        result.Errors.Add($"Invalid expression in condition: {condition.ThenValue}");
                    }
                }

                if (result.Errors.Any())
                {
                    result.Success = false;
                    return result;
                }

                // Create the formula
                return CreateFormula(new FormulaSpecification
                {
                    ResultParameter = spec.ResultParameter,
                    Expression = conditionalExpr,
                    Description = spec.Description,
                    Context = spec.Context
                });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Conditional formula failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Create lookup/array formula for selecting values based on input.
        /// </summary>
        public FormulaCreationResult CreateLookupFormula(LookupFormulaSpec spec)
        {
            var result = new FormulaCreationResult
            {
                ResultParameter = spec.ResultParameter
            };

            try
            {
                // Build nested if statements for lookup
                var lookupExpr = BuildLookupExpression(spec.LookupParameter, spec.LookupTable, spec.DefaultValue);

                return CreateFormula(new FormulaSpecification
                {
                    ResultParameter = spec.ResultParameter,
                    Expression = lookupExpr,
                    Description = $"Lookup based on {spec.LookupParameter}",
                    Context = spec.Context
                });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Lookup formula failed: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Public Methods - Dependency Analysis

        /// <summary>
        /// Analyze formula dependencies for a family.
        /// </summary>
        public DependencyAnalysisResult AnalyzeDependencies(IEnumerable<string> formulaParameters)
        {
            var result = new DependencyAnalysisResult();

            foreach (var param in formulaParameters)
            {
                if (_formulas.TryGetValue(param, out var formula))
                {
                    result.FormulaCount++;

                    foreach (var dep in formula.Dependencies)
                    {
                        if (!result.AllDependencies.Contains(dep))
                        {
                            result.AllDependencies.Add(dep);
                        }

                        // Check if dependency is also a formula
                        if (_formulas.ContainsKey(dep))
                        {
                            result.ChainedFormulas.Add((param, dep));
                        }
                    }
                }
            }

            // Build dependency order (topological sort)
            result.EvaluationOrder = GetTopologicalOrder(formulaParameters.ToList());

            // Identify independent formulas (can be calculated in parallel)
            result.IndependentFormulas = FindIndependentFormulas(formulaParameters.ToList());

            return result;
        }

        /// <summary>
        /// Get all parameters that depend on a given parameter.
        /// </summary>
        public List<string> GetDependentParameters(string parameterName)
        {
            var dependents = new List<string>();

            foreach (var kvp in _dependencyGraph)
            {
                if (kvp.Value.Contains(parameterName))
                {
                    dependents.Add(kvp.Key);
                }
            }

            return dependents;
        }

        /// <summary>
        /// Get the full dependency tree for a parameter.
        /// </summary>
        public DependencyTree GetDependencyTree(string parameterName)
        {
            var tree = new DependencyTree
            {
                ParameterName = parameterName
            };

            if (_formulas.TryGetValue(parameterName, out var formula))
            {
                tree.Formula = formula.Expression;

                foreach (var dep in formula.Dependencies)
                {
                    tree.Dependencies.Add(GetDependencyTree(dep));
                }
            }

            return tree;
        }

        /// <summary>
        /// Visualize dependency graph as text.
        /// </summary>
        public string VisualizeDependencies(string parameterName, int maxDepth = 5)
        {
            var sb = new System.Text.StringBuilder();
            VisualizeDependenciesRecursive(parameterName, sb, "", true, maxDepth, 0);
            return sb.ToString();
        }

        #endregion

        #region Public Methods - Formula Suggestions

        /// <summary>
        /// Suggest formulas based on existing parameters.
        /// </summary>
        public List<FormulaSuggestion> SuggestFormulas(
            List<string> existingParameters,
            string familyCategory)
        {
            var suggestions = new List<FormulaSuggestion>();

            // Area calculations
            if (existingParameters.Contains("Width") && existingParameters.Contains("Height") &&
                !existingParameters.Contains("Area"))
            {
                suggestions.Add(new FormulaSuggestion
                {
                    ResultParameter = "Area",
                    Formula = "Width * Height",
                    Description = "Calculate area from dimensions",
                    Confidence = 0.95,
                    Category = "Dimensional"
                });
            }

            if (existingParameters.Contains("Width") && existingParameters.Contains("Depth") &&
                !existingParameters.Contains("Footprint_Area"))
            {
                suggestions.Add(new FormulaSuggestion
                {
                    ResultParameter = "Footprint_Area",
                    Formula = "Width * Depth",
                    Description = "Calculate footprint area",
                    Confidence = 0.9,
                    Category = "Dimensional"
                });
            }

            // Volume calculations
            if (existingParameters.Contains("Width") && existingParameters.Contains("Height") &&
                existingParameters.Contains("Depth") && !existingParameters.Contains("Volume"))
            {
                suggestions.Add(new FormulaSuggestion
                {
                    ResultParameter = "Volume",
                    Formula = "Width * Height * Depth",
                    Description = "Calculate volume from dimensions",
                    Confidence = 0.95,
                    Category = "Dimensional"
                });
            }

            // Perimeter calculations
            if (existingParameters.Contains("Width") && existingParameters.Contains("Depth") &&
                !existingParameters.Contains("Perimeter"))
            {
                suggestions.Add(new FormulaSuggestion
                {
                    ResultParameter = "Perimeter",
                    Formula = "2 * (Width + Depth)",
                    Description = "Calculate perimeter",
                    Confidence = 0.85,
                    Category = "Dimensional"
                });
            }

            // Category-specific suggestions
            switch (familyCategory)
            {
                case "Lighting Fixtures":
                    if (existingParameters.Contains("Lumens") && existingParameters.Contains("Wattage") &&
                        !existingParameters.Contains("Efficacy"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Efficacy",
                            Formula = "Lumens / Wattage",
                            Description = "Calculate luminous efficacy (lm/W)",
                            Confidence = 0.98,
                            Category = "Performance"
                        });
                    }
                    break;

                case "Mechanical Equipment":
                    if (existingParameters.Contains("Cooling_Capacity") && existingParameters.Contains("Electrical_Load") &&
                        !existingParameters.Contains("EER"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "EER",
                            Formula = "Cooling_Capacity * 3.412 / Electrical_Load",
                            Description = "Calculate Energy Efficiency Ratio",
                            Confidence = 0.95,
                            Category = "Performance"
                        });
                    }

                    if (existingParameters.Contains("Cooling_Capacity") && existingParameters.Contains("Electrical_Load") &&
                        !existingParameters.Contains("COP"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "COP",
                            Formula = "Cooling_Capacity / Electrical_Load",
                            Description = "Calculate Coefficient of Performance",
                            Confidence = 0.95,
                            Category = "Performance"
                        });
                    }
                    break;

                case "Doors":
                case "Windows":
                    if (existingParameters.Contains("Width") && existingParameters.Contains("Frame_Width") &&
                        !existingParameters.Contains("Clear_Width"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Clear_Width",
                            Formula = "Width - 2 * Frame_Width",
                            Description = "Calculate clear opening width",
                            Confidence = 0.9,
                            Category = "Dimensional"
                        });
                    }

                    if (existingParameters.Contains("Sill_Height") && existingParameters.Contains("Height") &&
                        !existingParameters.Contains("Head_Height"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Head_Height",
                            Formula = "Sill_Height + Height",
                            Description = "Calculate head height from floor",
                            Confidence = 0.95,
                            Category = "Dimensional"
                        });
                    }
                    break;

                case "Structural Columns":
                case "Structural Framing":
                    if (existingParameters.Contains("Volume") && !existingParameters.Contains("Weight"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Weight",
                            Formula = "Volume * 2400",
                            Description = "Calculate self-weight (assuming concrete 2400 kg/m³)",
                            Confidence = 0.8,
                            Category = "Structural"
                        });
                    }
                    break;

                case "Electrical Equipment":
                    if (existingParameters.Contains("Voltage") && existingParameters.Contains("Amperage") &&
                        !existingParameters.Contains("Power_Rating"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Power_Rating",
                            Formula = "Voltage * Amperage / 1000",
                            Description = "Calculate power rating in kW",
                            Confidence = 0.95,
                            Category = "Electrical"
                        });
                    }
                    break;

                case "Plumbing Fixtures":
                    if (existingParameters.Contains("Flow_Rate") && !existingParameters.Contains("Daily_Usage"))
                    {
                        suggestions.Add(new FormulaSuggestion
                        {
                            ResultParameter = "Daily_Usage",
                            Formula = "Flow_Rate * 60 * 10",
                            Description = "Estimate daily water usage (assuming 10 min/day)",
                            Confidence = 0.6,
                            Category = "Performance"
                        });
                    }
                    break;
            }

            // Cost calculations
            if (existingParameters.Contains("Cost") && existingParameters.Contains("Width") &&
                existingParameters.Contains("Height") && !existingParameters.Contains("Cost_Per_Area"))
            {
                suggestions.Add(new FormulaSuggestion
                {
                    ResultParameter = "Cost_Per_Area",
                    Formula = "Cost / (Width * Height)",
                    Description = "Calculate cost per unit area",
                    Confidence = 0.7,
                    Category = "Cost"
                });
            }

            return suggestions.OrderByDescending(s => s.Confidence).ToList();
        }

        #endregion

        #region Public Methods - Batch Operations

        /// <summary>
        /// Apply formulas to multiple families in batch.
        /// </summary>
        public async Task<BatchFormulaApplicationResult> ApplyFormulasToFamiliesAsync(
            IEnumerable<string> familyPaths,
            IEnumerable<FormulaDefinition> formulas,
            BatchFormulaOptions options = null,
            IProgress<BatchOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new BatchFormulaOptions();
            var result = new BatchFormulaApplicationResult { StartTime = DateTime.Now };

            var paths = familyPaths.ToList();
            var formulaList = formulas.ToList();
            var processed = 0;

            foreach (var familyPath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var familyResult = new FamilyFormulaResult
                {
                    FamilyPath = familyPath,
                    FamilyName = Path.GetFileNameWithoutExtension(familyPath)
                };

                try
                {
                    // Verify family has required parameters
                    var familyParams = await GetFamilyParametersAsync(familyPath, cancellationToken);
                    var missingDependencies = new List<string>();

                    foreach (var formula in formulaList)
                    {
                        var missing = formula.Dependencies.Where(d => !familyParams.Contains(d)).ToList();
                        if (missing.Any())
                        {
                            if (options.CreateMissingParameters)
                            {
                                // Create missing parameters first
                                foreach (var param in missing)
                                {
                                    await CreateParameterInFamilyAsync(familyPath, param, cancellationToken);
                                    familyResult.ParametersCreated.Add(param);
                                }
                            }
                            else
                            {
                                familyResult.SkippedFormulas.Add(formula.ResultParameter);
                                familyResult.Warnings.Add($"Missing dependencies for {formula.ResultParameter}: {string.Join(", ", missing)}");
                                continue;
                            }
                        }

                        // Apply the formula
                        try
                        {
                            await ApplyFormulaToFamilyAsync(familyPath, formula, cancellationToken);
                            familyResult.AppliedFormulas.Add(formula.ResultParameter);
                        }
                        catch (Exception ex)
                        {
                            familyResult.FailedFormulas.Add(formula.ResultParameter);
                            familyResult.Errors.Add($"Failed to apply {formula.ResultParameter}: {ex.Message}");
                        }
                    }

                    familyResult.Success = !familyResult.FailedFormulas.Any();
                }
                catch (Exception ex)
                {
                    familyResult.Success = false;
                    familyResult.Errors.Add($"Failed to process family: {ex.Message}");
                }

                result.FamilyResults.Add(familyResult);
                processed++;

                progress?.Report(new BatchOperationProgress
                {
                    Current = processed,
                    Total = paths.Count,
                    CurrentItem = familyResult.FamilyName,
                    PercentComplete = (double)processed / paths.Count * 100,
                    Message = $"Processed: {familyResult.FamilyName}"
                });
            }

            result.EndTime = DateTime.Now;
            result.TotalFamilies = paths.Count;
            result.SuccessfulFamilies = result.FamilyResults.Count(r => r.Success);

            return result;
        }

        /// <summary>
        /// Validate formulas across multiple families.
        /// </summary>
        public async Task<FormulaValidationReport> ValidateFormulasInFamiliesAsync(
            IEnumerable<string> familyPaths,
            CancellationToken cancellationToken = default)
        {
            var report = new FormulaValidationReport();

            foreach (var path in familyPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var familyFormulas = await GetFamilyFormulasAsync(path, cancellationToken);
                var familyParams = await GetFamilyParametersAsync(path, cancellationToken);

                foreach (var formula in familyFormulas)
                {
                    var validation = new FormulaValidationEntry
                    {
                        FamilyPath = path,
                        FamilyName = Path.GetFileNameWithoutExtension(path),
                        ParameterName = formula.Key,
                        Formula = formula.Value
                    };

                    // Parse and validate
                    var parseResult = _parser.Parse(formula.Value);
                    validation.IsValid = parseResult.Success;

                    if (!parseResult.Success)
                    {
                        validation.Errors.AddRange(parseResult.Errors);
                    }

                    // Check dependencies
                    foreach (var dep in parseResult.ReferencedParameters)
                    {
                        if (!familyParams.Contains(dep))
                        {
                            validation.IsValid = false;
                            validation.Errors.Add($"Missing dependency: {dep}");
                        }
                    }

                    report.Entries.Add(validation);
                }
            }

            report.TotalFormulas = report.Entries.Count;
            report.ValidFormulas = report.Entries.Count(e => e.IsValid);
            report.InvalidFormulas = report.Entries.Count(e => !e.IsValid);

            return report;
        }

        #endregion

        #region Private Methods - Initialization

        private void InitializeFormulaTemplates()
        {
            // Dimensional calculations template
            _templates["DIMENSIONAL"] = new FormulaTemplate
            {
                Id = "DIMENSIONAL",
                Name = "Dimensional Calculations",
                Description = "Common dimensional formulas",
                Formulas = new List<FormulaTemplateEntry>
                {
                    new FormulaTemplateEntry { ResultParameter = "Area", Expression = "Width * Height", Description = "Calculate area" },
                    new FormulaTemplateEntry { ResultParameter = "Volume", Expression = "Width * Height * Depth", Description = "Calculate volume" },
                    new FormulaTemplateEntry { ResultParameter = "Perimeter", Expression = "2 * (Width + Depth)", Description = "Calculate perimeter" },
                    new FormulaTemplateEntry { ResultParameter = "Diagonal", Expression = "sqrt(Width^2 + Height^2)", Description = "Calculate diagonal" }
                }
            };

            // MEP equipment template
            _templates["MEP_PERFORMANCE"] = new FormulaTemplate
            {
                Id = "MEP_PERFORMANCE",
                Name = "MEP Performance Calculations",
                Description = "Performance formulas for MEP equipment",
                Formulas = new List<FormulaTemplateEntry>
                {
                    new FormulaTemplateEntry { ResultParameter = "EER", Expression = "Cooling_Capacity * 3.412 / Electrical_Load", Description = "Energy Efficiency Ratio" },
                    new FormulaTemplateEntry { ResultParameter = "COP", Expression = "Cooling_Capacity / Electrical_Load", Description = "Coefficient of Performance" },
                    new FormulaTemplateEntry { ResultParameter = "Efficacy", Expression = "Lumens / Wattage", Description = "Luminous efficacy" },
                    new FormulaTemplateEntry { ResultParameter = "Power_kW", Expression = "Voltage * Amperage / 1000", Description = "Power in kilowatts" }
                }
            };

            // Structural template
            _templates["STRUCTURAL"] = new FormulaTemplate
            {
                Id = "STRUCTURAL",
                Name = "Structural Calculations",
                Description = "Formulas for structural families",
                Formulas = new List<FormulaTemplateEntry>
                {
                    new FormulaTemplateEntry { ResultParameter = "Self_Weight", Expression = "Volume * Material_Density", Description = "Calculate self-weight" },
                    new FormulaTemplateEntry { ResultParameter = "Section_Area", Expression = "Width * Depth", Description = "Cross-section area" },
                    new FormulaTemplateEntry { ResultParameter = "Moment_of_Inertia", Expression = "Width * Depth^3 / 12", Description = "Moment of inertia (rectangular)" }
                }
            };

            // Door/Window template
            _templates["OPENINGS"] = new FormulaTemplate
            {
                Id = "OPENINGS",
                Name = "Opening Calculations",
                Description = "Formulas for doors and windows",
                Formulas = new List<FormulaTemplateEntry>
                {
                    new FormulaTemplateEntry { ResultParameter = "Clear_Width", Expression = "Width - 2 * Frame_Width", Description = "Clear opening width" },
                    new FormulaTemplateEntry { ResultParameter = "Clear_Height", Expression = "Height - Frame_Height", Description = "Clear opening height" },
                    new FormulaTemplateEntry { ResultParameter = "Glass_Area", Expression = "(Width - 2 * Frame_Width) * (Height - Frame_Height)", Description = "Glass area" },
                    new FormulaTemplateEntry { ResultParameter = "Head_Height", Expression = "Sill_Height + Height", Description = "Head height from floor" }
                }
            };

            // Costing template
            _templates["COSTING"] = new FormulaTemplate
            {
                Id = "COSTING",
                Name = "Cost Calculations",
                Description = "Cost-related formulas",
                Formulas = new List<FormulaTemplateEntry>
                {
                    new FormulaTemplateEntry { ResultParameter = "Total_Cost", Expression = "Unit_Cost * Quantity", Description = "Total cost calculation" },
                    new FormulaTemplateEntry { ResultParameter = "Cost_Per_Area", Expression = "Cost / Area", Description = "Cost per unit area" },
                    new FormulaTemplateEntry { ResultParameter = "Cost_Per_Volume", Expression = "Cost / Volume", Description = "Cost per unit volume" }
                }
            };
        }

        private void InitializeUnitMappings()
        {
            _parameterUnits["Width"] = UnitType.Length;
            _parameterUnits["Height"] = UnitType.Length;
            _parameterUnits["Depth"] = UnitType.Length;
            _parameterUnits["Length"] = UnitType.Length;
            _parameterUnits["Area"] = UnitType.Area;
            _parameterUnits["Volume"] = UnitType.Volume;
            _parameterUnits["Angle"] = UnitType.Angle;
            _parameterUnits["Cost"] = UnitType.Currency;
            _parameterUnits["Weight"] = UnitType.Mass;
            _parameterUnits["Wattage"] = UnitType.Power;
            _parameterUnits["Voltage"] = UnitType.ElectricalPotential;
            _parameterUnits["Amperage"] = UnitType.ElectricalCurrent;
        }

        #endregion

        #region Private Methods - Formula Processing

        private ParameterValidationResult ValidateParameterReferences(
            List<string> parameters,
            FormulaContext context)
        {
            var result = new ParameterValidationResult { IsValid = true };

            foreach (var param in parameters)
            {
                if (!context.AvailableParameters.Contains(param))
                {
                    if (context.AllowMissingParameters)
                    {
                        result.Warnings.Add($"Parameter '{param}' not found - will need to be created");
                    }
                    else
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Parameter '{param}' not found in family");
                    }
                }
            }

            return result;
        }

        private CircularDependencyResult CheckCircularDependencies(string resultParam, List<string> dependencies)
        {
            var result = new CircularDependencyResult();
            var visited = new HashSet<string>();
            var path = new List<string> { resultParam };

            foreach (var dep in dependencies)
            {
                if (CheckCircularRecursive(dep, resultParam, visited, path))
                {
                    result.HasCircular = true;
                    result.Path = new List<string>(path);
                    return result;
                }
            }

            return result;
        }

        private bool CheckCircularRecursive(string current, string target, HashSet<string> visited, List<string> path)
        {
            if (current == target)
            {
                path.Add(current);
                return true;
            }

            if (visited.Contains(current))
                return false;

            visited.Add(current);
            path.Add(current);

            if (_dependencyGraph.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (CheckCircularRecursive(dep, target, visited, path))
                        return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private UnitValidationResult ValidateUnitCompatibility(
            string resultParam,
            FormulaParseResult parseResult,
            FormulaContext context)
        {
            var result = new UnitValidationResult { IsValid = true };

            // Basic unit inference - in production would be more sophisticated
            if (_parameterUnits.TryGetValue(resultParam, out var expectedUnit))
            {
                // Check if formula result matches expected unit
                var inferredUnit = InferResultUnit(parseResult);
                if (inferredUnit != UnitType.Unknown && inferredUnit != expectedUnit)
                {
                    result.Warnings.Add($"Unit mismatch: expected {expectedUnit}, formula produces {inferredUnit}");
                }
            }

            return result;
        }

        private UnitType InferResultUnit(FormulaParseResult parseResult)
        {
            // Simple unit inference based on operations
            var units = parseResult.ReferencedParameters
                .Where(p => _parameterUnits.ContainsKey(p))
                .Select(p => _parameterUnits[p])
                .ToList();

            if (!units.Any())
                return UnitType.Unknown;

            // If all same unit and multiplication, derive area/volume
            if (units.All(u => u == UnitType.Length))
            {
                if (units.Count == 2)
                    return UnitType.Area;
                if (units.Count == 3)
                    return UnitType.Volume;
                return UnitType.Length;
            }

            return UnitType.Unknown;
        }

        private void UpdateDependencyGraph(string resultParam, List<string> dependencies)
        {
            _dependencyGraph[resultParam] = new List<string>(dependencies);
        }

        private string GenerateRevitFormula(FormulaOptimizationResult optimized)
        {
            // Convert to Revit formula syntax
            var formula = optimized.Expression;

            // Replace operators if needed (Revit uses standard math operators)
            // Replace function names to Revit equivalents
            formula = Regex.Replace(formula, @"\bsqrt\b", "sqrt", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\babs\b", "abs", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\bround\b", "round", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\bif\b", "if", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\band\b", "and", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\bor\b", "or", RegexOptions.IgnoreCase);
            formula = Regex.Replace(formula, @"\bnot\b", "not", RegexOptions.IgnoreCase);

            return formula;
        }

        private string BuildConditionalExpression(List<FormulaCondition> conditions, string defaultValue)
        {
            if (!conditions.Any())
                return defaultValue;

            var expr = new System.Text.StringBuilder();

            for (int i = 0; i < conditions.Count; i++)
            {
                var cond = conditions[i];
                expr.Append($"if({cond.Condition}, {cond.ThenValue}, ");
            }

            expr.Append(defaultValue);

            // Close all ifs
            for (int i = 0; i < conditions.Count; i++)
            {
                expr.Append(")");
            }

            return expr.ToString();
        }

        private string BuildLookupExpression(string lookupParam, Dictionary<string, string> table, string defaultValue)
        {
            if (!table.Any())
                return defaultValue;

            var expr = new System.Text.StringBuilder();
            var entries = table.ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                expr.Append($"if({lookupParam} = \"{entry.Key}\", {entry.Value}, ");
            }

            expr.Append(defaultValue);

            for (int i = 0; i < entries.Count; i++)
            {
                expr.Append(")");
            }

            return expr.ToString();
        }

        private string MapTemplateParameters(string expression, Dictionary<string, string> mapping)
        {
            var result = expression;
            foreach (var kvp in mapping)
            {
                result = Regex.Replace(result, $@"\b{Regex.Escape(kvp.Key)}\b", kvp.Value);
            }
            return result;
        }

        private string MapTemplateParameter(string param, Dictionary<string, string> mapping)
        {
            return mapping.TryGetValue(param, out var mapped) ? mapped : param;
        }

        private List<string> GetTopologicalOrder(List<string> parameters)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();

            void Visit(string param)
            {
                if (visited.Contains(param))
                    return;

                visited.Add(param);

                if (_dependencyGraph.TryGetValue(param, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        Visit(dep);
                    }
                }

                result.Add(param);
            }

            foreach (var param in parameters)
            {
                Visit(param);
            }

            return result;
        }

        private List<string> FindIndependentFormulas(List<string> parameters)
        {
            // Formulas that don't depend on other formulas
            return parameters.Where(p =>
            {
                if (!_formulas.TryGetValue(p, out var formula))
                    return false;

                return !formula.Dependencies.Any(d => _formulas.ContainsKey(d));
            }).ToList();
        }

        private void VisualizeDependenciesRecursive(string param, System.Text.StringBuilder sb, string indent, bool isLast, int maxDepth, int currentDepth)
        {
            if (currentDepth > maxDepth)
            {
                sb.AppendLine($"{indent}└── ...");
                return;
            }

            var prefix = isLast ? "└── " : "├── ";
            var hasFormula = _formulas.TryGetValue(param, out var formula);

            sb.Append($"{indent}{prefix}{param}");
            if (hasFormula)
            {
                sb.Append($" = {formula.Expression}");
            }
            sb.AppendLine();

            if (hasFormula && formula.Dependencies.Any())
            {
                var childIndent = indent + (isLast ? "    " : "│   ");
                for (int i = 0; i < formula.Dependencies.Count; i++)
                {
                    var dep = formula.Dependencies[i];
                    var isLastChild = i == formula.Dependencies.Count - 1;
                    VisualizeDependenciesRecursive(dep, sb, childIndent, isLastChild, maxDepth, currentDepth + 1);
                }
            }
        }

        #endregion

        #region Private Methods - Revit API Stubs

        private Task<List<string>> GetFamilyParametersAsync(string familyPath, CancellationToken cancellationToken)
        {
            // Would use Revit API to get family parameters
            return Task.FromResult(new List<string>());
        }

        private Task<Dictionary<string, string>> GetFamilyFormulasAsync(string familyPath, CancellationToken cancellationToken)
        {
            // Would use Revit API to get formulas
            return Task.FromResult(new Dictionary<string, string>());
        }

        private Task CreateParameterInFamilyAsync(string familyPath, string paramName, CancellationToken cancellationToken)
        {
            // Would use Revit API to create parameter
            return Task.CompletedTask;
        }

        private Task ApplyFormulaToFamilyAsync(string familyPath, FormulaDefinition formula, CancellationToken cancellationToken)
        {
            // Would use Revit API to set parameter formula
            return Task.CompletedTask;
        }

        #endregion
    }

    #region Supporting Classes - Formula Parser

    public class FormulaParser
    {
        private static readonly Regex ParameterPattern = new Regex(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
        private static readonly string[] Functions = { "sqrt", "abs", "round", "if", "and", "or", "not", "sin", "cos", "tan", "asin", "acos", "atan", "log", "exp", "pi" };
        private static readonly string[] Operators = { "+", "-", "*", "/", "^", "=", "<>", "<", ">", "<=", ">=" };

        public FormulaParseResult Parse(string expression)
        {
            var result = new FormulaParseResult { OriginalExpression = expression };

            try
            {
                // Extract all identifiers
                var matches = ParameterPattern.Matches(expression);
                var identifiers = matches.Cast<Match>().Select(m => m.Value).Distinct().ToList();

                // Filter out functions and keywords
                result.ReferencedParameters = identifiers
                    .Where(id => !Functions.Contains(id.ToLower()))
                    .Where(id => !IsKeyword(id))
                    .ToList();

                // Basic syntax validation
                if (!ValidateSyntax(expression, out var syntaxError))
                {
                    result.Success = false;
                    result.Errors.Add(syntaxError);
                    return result;
                }

                result.Success = true;
                result.ParsedExpression = expression;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Parse error: {ex.Message}");
            }

            return result;
        }

        private bool IsKeyword(string identifier)
        {
            var keywords = new[] { "if", "then", "else", "and", "or", "not", "true", "false" };
            return keywords.Contains(identifier.ToLower());
        }

        private bool ValidateSyntax(string expression, out string error)
        {
            error = null;

            // Check balanced parentheses
            int depth = 0;
            foreach (var c in expression)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;

                if (depth < 0)
                {
                    error = "Unmatched closing parenthesis";
                    return false;
                }
            }

            if (depth > 0)
            {
                error = "Unmatched opening parenthesis";
                return false;
            }

            return true;
        }
    }

    public class FormulaOptimizer
    {
        public FormulaOptimizationResult Optimize(FormulaParseResult parseResult)
        {
            var result = new FormulaOptimizationResult
            {
                Expression = parseResult.ParsedExpression,
                WasOptimized = false
            };

            // Simple optimizations
            var expr = parseResult.ParsedExpression;

            // Remove redundant parentheses around single terms
            // Simplify constant expressions
            // etc.

            result.Expression = expr;
            return result;
        }
    }

    public class NaturalLanguageFormulaInterpreter
    {
        private readonly Dictionary<string, string> _operatorMappings;
        private readonly Dictionary<string, string> _functionMappings;

        public NaturalLanguageFormulaInterpreter()
        {
            _operatorMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "plus", "+" }, { "added to", "+" }, { "add", "+" },
                { "minus", "-" }, { "subtract", "-" }, { "subtracted from", "-" },
                { "times", "*" }, { "multiplied by", "*" }, { "multiply", "*" },
                { "divided by", "/" }, { "divide", "/" }, { "over", "/" },
                { "squared", "^2" }, { "cubed", "^3" },
                { "to the power of", "^" }, { "power", "^" },
                { "equals", "=" }, { "is equal to", "=" },
                { "greater than", ">" }, { "less than", "<" },
                { "at least", ">=" }, { "at most", "<=" }
            };

            _functionMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "square root of", "sqrt" }, { "sqrt of", "sqrt" },
                { "absolute value of", "abs" }, { "abs of", "abs" },
                { "rounded", "round" }, { "round", "round" }
            };
        }

        public NLInterpretationResult Interpret(string description, FormulaContext context)
        {
            var result = new NLInterpretationResult();

            try
            {
                // Extract result parameter (usually before "=" or "is")
                var parts = Regex.Split(description, @"\s+(=|is|equals)\s+", RegexOptions.IgnoreCase);
                if (parts.Length >= 2)
                {
                    result.ResultParameter = NormalizeParameterName(parts[0].Trim());
                    var expression = parts[parts.Length - 1].Trim();

                    // Convert natural language to formula
                    expression = ConvertToFormula(expression, context);

                    result.Formula = expression;
                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.Errors.Add("Could not identify result parameter and expression");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Interpretation failed: {ex.Message}");
            }

            return result;
        }

        private string ConvertToFormula(string expression, FormulaContext context)
        {
            var result = expression;

            // Replace function words
            foreach (var mapping in _functionMappings)
            {
                result = Regex.Replace(result, Regex.Escape(mapping.Key), mapping.Value + "(", RegexOptions.IgnoreCase);
            }

            // Replace operators
            foreach (var mapping in _operatorMappings)
            {
                result = Regex.Replace(result, @"\b" + Regex.Escape(mapping.Key) + @"\b", mapping.Value, RegexOptions.IgnoreCase);
            }

            // Normalize parameter names
            var words = result.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                var normalized = NormalizeParameterName(words[i]);
                if (context.AvailableParameters.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    words[i] = normalized;
                }
            }

            return string.Join(" ", words).Trim();
        }

        private string NormalizeParameterName(string name)
        {
            // Convert "the width" to "Width", "door height" to "Door_Height", etc.
            name = Regex.Replace(name, @"^(the|a|an)\s+", "", RegexOptions.IgnoreCase);
            name = name.Trim();
            name = Regex.Replace(name, @"\s+", "_");
            name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
            return name;
        }
    }

    #endregion

    #region Supporting Classes - Data Types

    public class FormulaDefinition
    {
        public string Id { get; set; }
        public string ResultParameter { get; set; }
        public string Expression { get; set; }
        public string RevitFormula { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FormulaTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<FormulaTemplateEntry> Formulas { get; set; } = new List<FormulaTemplateEntry>();
    }

    public class FormulaTemplateEntry
    {
        public string ResultParameter { get; set; }
        public string Expression { get; set; }
        public string Description { get; set; }
    }

    public class FormulaSpecification
    {
        public string ResultParameter { get; set; }
        public string Expression { get; set; }
        public string Description { get; set; }
        public FormulaContext Context { get; set; }
    }

    public class FormulaContext
    {
        public List<string> AvailableParameters { get; set; } = new List<string>();
        public string FamilyCategory { get; set; }
        public bool AllowMissingParameters { get; set; }
    }

    public class FormulaCreationResult
    {
        public bool Success { get; set; }
        public string ResultParameter { get; set; }
        public string OriginalExpression { get; set; }
        public string OptimizedExpression { get; set; }
        public string RevitFormula { get; set; }
        public string InterpretedAs { get; set; }
        public bool OptimizationApplied { get; set; }
        public FormulaDefinition Formula { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class BatchFormulaResult
    {
        public List<FormulaDefinition> CreatedFormulas { get; set; } = new List<FormulaDefinition>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class ConditionalFormulaSpec
    {
        public string ResultParameter { get; set; }
        public List<FormulaCondition> Conditions { get; set; } = new List<FormulaCondition>();
        public string DefaultValue { get; set; }
        public string Description { get; set; }
        public FormulaContext Context { get; set; }
    }

    public class FormulaCondition
    {
        public string Condition { get; set; }
        public string ThenValue { get; set; }
    }

    public class LookupFormulaSpec
    {
        public string ResultParameter { get; set; }
        public string LookupParameter { get; set; }
        public Dictionary<string, string> LookupTable { get; set; } = new Dictionary<string, string>();
        public string DefaultValue { get; set; }
        public FormulaContext Context { get; set; }
    }

    public class FormulaParseResult
    {
        public bool Success { get; set; }
        public string OriginalExpression { get; set; }
        public string ParsedExpression { get; set; }
        public List<string> ReferencedParameters { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class FormulaOptimizationResult
    {
        public string Expression { get; set; }
        public bool WasOptimized { get; set; }
    }

    public class NLInterpretationResult
    {
        public bool Success { get; set; }
        public string ResultParameter { get; set; }
        public string Formula { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class DependencyAnalysisResult
    {
        public int FormulaCount { get; set; }
        public List<string> AllDependencies { get; set; } = new List<string>();
        public List<(string Formula, string DependsOn)> ChainedFormulas { get; set; } = new List<(string, string)>();
        public List<string> EvaluationOrder { get; set; } = new List<string>();
        public List<string> IndependentFormulas { get; set; } = new List<string>();
    }

    public class DependencyTree
    {
        public string ParameterName { get; set; }
        public string Formula { get; set; }
        public List<DependencyTree> Dependencies { get; set; } = new List<DependencyTree>();
    }

    public class CircularDependencyResult
    {
        public bool HasCircular { get; set; }
        public List<string> Path { get; set; } = new List<string>();
    }

    public class ParameterValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class UnitValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public enum UnitType
    {
        Unknown,
        Length,
        Area,
        Volume,
        Angle,
        Mass,
        Currency,
        Power,
        ElectricalPotential,
        ElectricalCurrent,
        Temperature,
        Time,
        Dimensionless
    }

    public class BatchFormulaApplicationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalFamilies { get; set; }
        public int SuccessfulFamilies { get; set; }
        public List<FamilyFormulaResult> FamilyResults { get; set; } = new List<FamilyFormulaResult>();
    }

    public class FamilyFormulaResult
    {
        public string FamilyPath { get; set; }
        public string FamilyName { get; set; }
        public bool Success { get; set; }
        public List<string> AppliedFormulas { get; set; } = new List<string>();
        public List<string> SkippedFormulas { get; set; } = new List<string>();
        public List<string> FailedFormulas { get; set; } = new List<string>();
        public List<string> ParametersCreated { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BatchFormulaOptions
    {
        public bool CreateMissingParameters { get; set; } = false;
        public bool OverwriteExisting { get; set; } = false;
    }

    public class FormulaValidationReport
    {
        public int TotalFormulas { get; set; }
        public int ValidFormulas { get; set; }
        public int InvalidFormulas { get; set; }
        public List<FormulaValidationEntry> Entries { get; set; } = new List<FormulaValidationEntry>();
    }

    public class FormulaValidationEntry
    {
        public string FamilyPath { get; set; }
        public string FamilyName { get; set; }
        public string ParameterName { get; set; }
        public string Formula { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class FormulaSuggestion
    {
        public string ResultParameter { get; set; }
        public string Formula { get; set; }
        public string Description { get; set; }
        public double Confidence { get; set; }
        public string Category { get; set; }
    }

    #endregion
}
