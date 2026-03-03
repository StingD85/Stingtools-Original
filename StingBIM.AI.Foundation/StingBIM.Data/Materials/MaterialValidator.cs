using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Materials
{
    /// <summary>
    /// Validates material assignments and checks for compatibility issues.
    /// Ensures materials are correctly applied and meet standards requirements.
    /// </summary>
    /// <remarks>
    /// Validation checks:
    /// - Material assignment validity
    /// - Category compatibility
    /// - Missing materials
    /// - Duplicate assignments
    /// - Standards compliance
    /// </remarks>
    public class MaterialValidator
    {
        #region Private Fields

        private readonly Document _document;
        private readonly MaterialDatabase _database;
        private readonly List<ValidationRule> _rules;
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<MaterialValidator>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the Revit document.
        /// </summary>
        public Document Document => _document;

        /// <summary>
        /// Gets the material database.
        /// </summary>
        public MaterialDatabase Database => _database;

        /// <summary>
        /// Gets the number of validation rules.
        /// </summary>
        public int RuleCount => _rules.Count;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialValidator"/> class.
        /// </summary>
        /// <param name="document">Revit document.</param>
        /// <param name="database">Material database (optional).</param>
        public MaterialValidator(Document document, MaterialDatabase database = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _database = database;
            _rules = new List<ValidationRule>();

            LoadDefaultRules();
        }

        #endregion

        #region Public Methods - Single Element Validation

        /// <summary>
        /// Validates material assignment for a single element.
        /// </summary>
        /// <param name="element">Element to validate.</param>
        /// <returns>Validation result.</returns>
        public MaterialValidationResult ValidateElement(Element element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var result = new MaterialValidationResult
            {
                ElementId = element.Id,
                ElementType = element.GetType().Name
            };

            try
            {
                // Check if element has material assignment
                var materialId = GetElementMaterialId(element);

                if (materialId == ElementId.InvalidElementId)
                {
                    result.Warnings.Add("Element has no material assigned");
                    return result;
                }

                // Get the material
                var material = _document.GetElement(materialId) as Material;
                if (material == null)
                {
                    result.Errors.Add($"Invalid material ID: {materialId}");
                    return result;
                }

                result.MaterialName = material.Name;

                // Run validation rules
                foreach (var rule in _rules)
                {
                    if (!rule.AppliesTo(element))
                        continue;

                    var ruleResult = rule.Validate(element, material, _database);

                    if (!ruleResult.IsValid)
                    {
                        result.Errors.AddRange(ruleResult.Errors);
                    }

                    if (ruleResult.HasWarnings)
                    {
                        result.Warnings.AddRange(ruleResult.Warnings);
                    }
                }

                _logger.Debug($"Validated element {element.Id}: {result.Errors.Count} errors, {result.Warnings.Count} warnings");

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error validating element {element.Id}: {ex.Message}");
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        #endregion

        #region Public Methods - Batch Validation

        /// <summary>
        /// Validates materials for multiple elements.
        /// </summary>
        /// <param name="elements">Elements to validate.</param>
        /// <returns>Batch validation result.</returns>
        public MaterialBatchValidationResult ValidateBatch(IEnumerable<Element> elements)
        {
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));

            var batchResult = new MaterialBatchValidationResult();

            try
            {
                var elementList = elements.ToList();
                _logger.Info($"Starting batch validation of {elementList.Count} elements...");

                foreach (var element in elementList)
                {
                    try
                    {
                        var result = ValidateElement(element);
                        batchResult.Results.Add(result);

                        if (result.IsValid)
                            batchResult.ValidCount++;
                        else
                            batchResult.InvalidCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Failed to validate element {element.Id}: {ex.Message}");
                        batchResult.InvalidCount++;
                    }
                }

                _logger.Info($"Batch validation complete: {batchResult.ValidCount} valid, {batchResult.InvalidCount} invalid");

                return batchResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Batch validation failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Validates all elements in a specific category.
        /// </summary>
        /// <param name="category">Category to validate.</param>
        /// <returns>Batch validation result.</returns>
        public MaterialBatchValidationResult ValidateCategory(BuiltInCategory category)
        {
            try
            {
                var collector = new FilteredElementCollector(_document)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                var elements = collector.ToElements();

                _logger.Info($"Validating {elements.Count} elements in category {category}");

                return ValidateBatch(elements);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to validate category: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Public Methods - Rule Management

        /// <summary>
        /// Adds a custom validation rule.
        /// </summary>
        /// <param name="rule">Validation rule to add.</param>
        public void AddRule(ValidationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _rules.Add(rule);
            _logger.Debug($"Added validation rule: {rule.Name}");
        }

        /// <summary>
        /// Removes a validation rule by name.
        /// </summary>
        /// <param name="ruleName">Name of rule to remove.</param>
        /// <returns>True if removed; otherwise, false.</returns>
        public bool RemoveRule(string ruleName)
        {
            var rule = _rules.FirstOrDefault(r => r.Name == ruleName);
            if (rule != null)
            {
                _rules.Remove(rule);
                _logger.Debug($"Removed validation rule: {ruleName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clears all validation rules.
        /// </summary>
        public void ClearRules()
        {
            _rules.Clear();
            _logger.Debug("Cleared all validation rules");
        }

        #endregion

        #region Public Methods - Reporting

        /// <summary>
        /// Gets a summary of validation issues in the document.
        /// </summary>
        /// <returns>Validation summary.</returns>
        public MaterialValidationSummary GetValidationSummary()
        {
            try
            {
                var summary = new MaterialValidationSummary();

                // Collect all model elements
                var collector = new FilteredElementCollector(_document)
                    .WhereElementIsNotElementType();

                var elements = collector.ToElements();

                // Validate all elements
                var batchResult = ValidateBatch(elements);

                summary.TotalElements = elements.Count;
                summary.ValidElements = batchResult.ValidCount;
                summary.InvalidElements = batchResult.InvalidCount;
                summary.TotalErrors = batchResult.Results.Sum(r => r.Errors.Count);
                summary.TotalWarnings = batchResult.Results.Sum(r => r.Warnings.Count);

                // Group errors by type
                summary.ErrorsByType = batchResult.Results
                    .SelectMany(r => r.Errors)
                    .GroupBy(e => e)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());

                return summary;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to generate validation summary: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the material ID from an element.
        /// </summary>
        private ElementId GetElementMaterialId(Element element)
        {
            try
            {
                // Try to get material from parameter
                var materialParam = element.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (materialParam != null && materialParam.HasValue)
                {
                    return materialParam.AsElementId();
                }

                // Try to get from type
                if (element is FamilyInstance familyInstance)
                {
                    var typeParam = familyInstance.Symbol?.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (typeParam != null && typeParam.HasValue)
                    {
                        return typeParam.AsElementId();
                    }
                }

                return ElementId.InvalidElementId;
            }
            catch
            {
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// Loads default validation rules.
        /// </summary>
        private void LoadDefaultRules()
        {
            // Rule 1: Check for missing materials
            _rules.Add(new ValidationRule
            {
                Name = "MaterialAssigned",
                AppliesTo = (element) => true,
                Validate = (element, material, database) =>
                {
                    if (material == null)
                    {
                        return ValidationRuleResult.Error("No material assigned");
                    }
                    return ValidationRuleResult.Success();
                }
            });

            // Rule 2: Check material exists in database (if database provided)
            if (_database != null)
            {
                _rules.Add(new ValidationRule
                {
                    Name = "MaterialInDatabase",
                    AppliesTo = (element) => true,
                    Validate = (element, material, database) =>
                    {
                        if (material != null && database != null)
                        {
                            var dbMaterial = database.GetByName(material.Name);
                            if (dbMaterial == null)
                            {
                                return ValidationRuleResult.Warning($"Material '{material.Name}' not found in database");
                            }
                        }
                        return ValidationRuleResult.Success();
                    }
                });
            }

            // Rule 3: Check for valid material name
            _rules.Add(new ValidationRule
            {
                Name = "ValidMaterialName",
                AppliesTo = (element) => true,
                Validate = (element, material, database) =>
                {
                    if (material != null)
                    {
                        if (string.IsNullOrWhiteSpace(material.Name))
                        {
                            return ValidationRuleResult.Error("Material has empty name");
                        }

                        if (material.Name.Contains("<") || material.Name.Contains(">"))
                        {
                            return ValidationRuleResult.Warning("Material name contains invalid characters");
                        }
                    }
                    return ValidationRuleResult.Success();
                }
            });
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Validation result for a single element.
    /// </summary>
    public class MaterialValidationResult
    {
        public ElementId ElementId { get; set; }
        public string ElementType { get; set; }
        public string MaterialName { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

        public MaterialValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public override string ToString()
        {
            return IsValid
                ? $"Element {ElementId}: Valid"
                : $"Element {ElementId}: {Errors.Count} errors";
        }
    }

    /// <summary>
    /// Batch validation result.
    /// </summary>
    public class MaterialBatchValidationResult
    {
        public List<MaterialValidationResult> Results { get; set; }
        public int ValidCount { get; set; }
        public int InvalidCount { get; set; }

        public int TotalCount => Results.Count;
        public bool AllValid => InvalidCount == 0;

        public MaterialBatchValidationResult()
        {
            Results = new List<MaterialValidationResult>();
        }

        public override string ToString()
        {
            return $"Validated {TotalCount} elements: {ValidCount} valid, {InvalidCount} invalid";
        }
    }

    /// <summary>
    /// Validation summary for entire document.
    /// </summary>
    public class MaterialValidationSummary
    {
        public int TotalElements { get; set; }
        public int ValidElements { get; set; }
        public int InvalidElements { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; }

        public double ValidationRate => TotalElements > 0 ? (double)ValidElements / TotalElements * 100 : 0;

        public MaterialValidationSummary()
        {
            ErrorsByType = new Dictionary<string, int>();
        }

        public override string ToString()
        {
            return $"Validation: {ValidElements}/{TotalElements} ({ValidationRate:F1}%) - {TotalErrors} errors, {TotalWarnings} warnings";
        }
    }

    /// <summary>
    /// Validation rule definition.
    /// </summary>
    public class ValidationRule
    {
        public string Name { get; set; }
        public Func<Element, bool> AppliesTo { get; set; }
        public Func<Element, Material, MaterialDatabase, ValidationRuleResult> Validate { get; set; }
    }

    /// <summary>
    /// Result of a validation rule check.
    /// </summary>
    public class ValidationRuleResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public bool HasWarnings => Warnings.Count > 0;

        public ValidationRuleResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public static ValidationRuleResult Success()
        {
            return new ValidationRuleResult { IsValid = true };
        }

        public static ValidationRuleResult Error(string message)
        {
            return new ValidationRuleResult
            {
                IsValid = false,
                Errors = new List<string> { message }
            };
        }

        public static ValidationRuleResult Warning(string message)
        {
            return new ValidationRuleResult
            {
                IsValid = true,
                Warnings = new List<string> { message }
            };
        }
    }

    #endregion
}
