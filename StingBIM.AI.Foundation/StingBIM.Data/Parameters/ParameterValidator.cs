using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Validates parameter operations and ensures data integrity
    /// Provides comprehensive validation for:
    /// - Parameter definitions
    /// - Category bindings
    /// - Value assignments
    /// - Data type compatibility
    /// - Standards compliance
    /// </summary>
    public class ParameterValidator
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<ParameterValidator>();
        private readonly Document _document;
        private readonly List<ValidationRule> _rules;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new ParameterValidator
        /// </summary>
        /// <param name="document">Revit document for context-aware validation</param>
        public ParameterValidator(Document document = null)
        {
            _document = document;
            _rules = new List<ValidationRule>();
            
            // Initialize default validation rules
            InitializeDefaultRules();
            
            _logger.Info("ParameterValidator initialized");
        }
        
        #endregion

        #region Validation Methods
        
        /// <summary>
        /// Validates a parameter definition
        /// </summary>
        /// <param name="parameter">Parameter to validate</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateDefinition(ParameterDefinition parameter)
        {
            if (parameter == null)
            {
                return ValidationResult.Failure("Parameter cannot be null");
            }
            
            var errors = new List<string>();
            var warnings = new List<string>();
            
            // Validate GUID
            if (parameter.Guid == Guid.Empty)
            {
                errors.Add("Parameter GUID cannot be empty");
            }
            
            // Validate name
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                errors.Add("Parameter name cannot be null or empty");
            }
            else
            {
                // Check name length
                if (parameter.Name.Length > 255)
                {
                    errors.Add($"Parameter name exceeds maximum length (255): {parameter.Name.Length}");
                }
                
                // Check for invalid characters
                var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
                if (parameter.Name.IndexOfAny(invalidChars) >= 0)
                {
                    errors.Add($"Parameter name contains invalid characters: {parameter.Name}");
                }
                
                // Check naming conventions
                if (!char.IsLetter(parameter.Name[0]))
                {
                    warnings.Add($"Parameter name should start with a letter: {parameter.Name}");
                }
            }
            
            // Validate data type
            if (string.IsNullOrWhiteSpace(parameter.DataType))
            {
                errors.Add("Parameter data type cannot be null or empty");
            }
            else
            {
                if (!IsValidDataType(parameter.DataType))
                {
                    warnings.Add($"Unknown data type: {parameter.DataType}");
                }
            }
            
            // Validate group
            if (string.IsNullOrWhiteSpace(parameter.GroupName))
            {
                errors.Add("Parameter group name cannot be null or empty");
            }
            
            if (parameter.GroupId < 0)
            {
                errors.Add($"Invalid group ID: {parameter.GroupId}");
            }
            
            // Validate description
            if (string.IsNullOrWhiteSpace(parameter.Description))
            {
                warnings.Add($"Parameter {parameter.Name} has no description");
            }
            
            // Apply custom rules
            foreach (var rule in _rules)
            {
                if (rule.AppliesTo(parameter))
                {
                    var ruleResult = rule.Validate(parameter);
                    errors.AddRange(ruleResult.Errors);
                    warnings.AddRange(ruleResult.Warnings);
                }
            }
            
            return errors.Count > 0
                ? ValidationResult.Failure(errors, warnings)
                : warnings.Count > 0
                    ? ValidationResult.SuccessWithWarnings(warnings)
                    : ValidationResult.Success();
        }
        
        /// <summary>
        /// Validates multiple parameter definitions
        /// </summary>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Combined validation result</returns>
        public ValidationResult ValidateDefinitions(IEnumerable<ParameterDefinition> parameters)
        {
            if (parameters == null)
            {
                return ValidationResult.Failure("Parameters collection cannot be null");
            }
            
            var allErrors = new List<string>();
            var allWarnings = new List<string>();
            var parameterList = parameters.ToList();
            
            _logger.Debug($"Validating {parameterList.Count} parameter definitions");
            
            // Validate individual parameters
            foreach (var param in parameterList)
            {
                var result = ValidateDefinition(param);
                allErrors.AddRange(result.Errors);
                allWarnings.AddRange(result.Warnings);
            }
            
            // Check for duplicate GUIDs
            var duplicateGuids = parameterList
                .GroupBy(p => p.Guid)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            
            foreach (var guid in duplicateGuids)
            {
                var paramNames = parameterList.Where(p => p.Guid == guid).Select(p => p.Name);
                allErrors.Add($"Duplicate GUID {guid} found in parameters: {string.Join(", ", paramNames)}");
            }
            
            // Check for duplicate names
            var duplicateNames = parameterList
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            
            foreach (var name in duplicateNames)
            {
                allWarnings.Add($"Duplicate parameter name (case-insensitive): {name}");
            }
            
            _logger.Info($"Validation complete: {allErrors.Count} errors, {allWarnings.Count} warnings");
            
            return allErrors.Count > 0
                ? ValidationResult.Failure(allErrors, allWarnings)
                : allWarnings.Count > 0
                    ? ValidationResult.SuccessWithWarnings(allWarnings)
                    : ValidationResult.Success();
        }
        
        /// <summary>
        /// Validates a category binding
        /// </summary>
        /// <param name="parameter">Parameter being bound</param>
        /// <param name="categories">Categories to bind to</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateBinding(ParameterDefinition parameter, IEnumerable<Category> categories)
        {
            if (parameter == null)
            {
                return ValidationResult.Failure("Parameter cannot be null");
            }
            
            if (categories == null || !categories.Any())
            {
                return ValidationResult.Failure("At least one category must be specified for binding");
            }
            
            var errors = new List<string>();
            var warnings = new List<string>();
            var categoryList = categories.ToList();
            
            // Validate parameter definition first
            var paramResult = ValidateDefinition(parameter);
            if (!paramResult.IsValid)
            {
                return paramResult;
            }
            
            // Validate categories
            foreach (var category in categoryList)
            {
                if (category == null)
                {
                    errors.Add("Category cannot be null");
                    continue;
                }
                
                // Check if category allows parameters
                if (!category.AllowsBoundParameters)
                {
                    errors.Add($"Category {category.Name} does not allow bound parameters");
                }
                
                // Check if category is valid for this document
                if (_document != null)
                {
                    try
                    {
                        var docCategory = Category.GetCategory(_document, category.Id);
                        if (docCategory == null)
                        {
                            errors.Add($"Category {category.Name} not found in document");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to validate category: {category.Name}");
                        errors.Add($"Failed to validate category {category.Name}: {ex.Message}");
                    }
                }
            }
            
            // Check for duplicate categories
            var duplicates = categoryList
                .GroupBy(c => c.Id.Value)
                .Where(g => g.Count() > 1)
                .Select(g => g.First().Name);
            
            foreach (var duplicate in duplicates)
            {
                warnings.Add($"Duplicate category in binding: {duplicate}");
            }
            
            return errors.Count > 0
                ? ValidationResult.Failure(errors, warnings)
                : warnings.Count > 0
                    ? ValidationResult.SuccessWithWarnings(warnings)
                    : ValidationResult.Success();
        }
        
        /// <summary>
        /// Validates a parameter value assignment
        /// </summary>
        /// <param name="parameter">Parameter definition</param>
        /// <param name="value">Value to assign</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateValue(ParameterDefinition parameter, object value)
        {
            if (parameter == null)
            {
                return ValidationResult.Failure("Parameter cannot be null");
            }
            
            var errors = new List<string>();
            var warnings = new List<string>();
            
            // Null value handling
            if (value == null)
            {
                if (!parameter.HideWhenNoValue)
                {
                    warnings.Add($"Null value for parameter {parameter.Name}");
                }
                return ValidationResult.SuccessWithWarnings(warnings);
            }
            
            // Validate value type compatibility
            switch (parameter.DataType.ToUpperInvariant())
            {
                case "TEXT":
                case "URL":
                    if (!(value is string))
                    {
                        errors.Add($"Expected string value for {parameter.Name}, got {value.GetType().Name}");
                    }
                    else
                    {
                        var strValue = (string)value;
                        if (strValue.Length > 1024)
                        {
                            warnings.Add($"String value for {parameter.Name} exceeds recommended length (1024)");
                        }
                    }
                    break;
                
                case "INTEGER":
                    if (!(value is int || value is long))
                    {
                        errors.Add($"Expected integer value for {parameter.Name}, got {value.GetType().Name}");
                    }
                    break;
                
                case "NUMBER":
                    if (!(value is double || value is float || value is int))
                    {
                        errors.Add($"Expected numeric value for {parameter.Name}, got {value.GetType().Name}");
                    }
                    break;
                
                case "LENGTH":
                case "AREA":
                case "VOLUME":
                case "ANGLE":
                    if (!(value is double || value is float))
                    {
                        errors.Add($"Expected numeric value for {parameter.DataType} parameter {parameter.Name}");
                    }
                    else
                    {
                        var numValue = Convert.ToDouble(value);
                        if (numValue < 0 && parameter.DataType != "ANGLE")
                        {
                            warnings.Add($"Negative value for {parameter.DataType} parameter {parameter.Name}");
                        }
                    }
                    break;
                
                case "YESNO":
                    if (!(value is bool || value is int))
                    {
                        errors.Add($"Expected boolean value for {parameter.Name}, got {value.GetType().Name}");
                    }
                    break;
                
                case "CURRENCY":
                    if (!(value is double || value is decimal || value is float))
                    {
                        errors.Add($"Expected numeric value for currency parameter {parameter.Name}");
                    }
                    break;
                
                case "ELECTRICAL_CURRENT":
                case "ELECTRICAL_POTENTIAL":
                case "ELECTRICAL_POWER":
                    if (!(value is double || value is float))
                    {
                        errors.Add($"Expected numeric value for {parameter.DataType} parameter {parameter.Name}");
                    }
                    else
                    {
                        var numValue = Convert.ToDouble(value);
                        if (numValue < 0)
                        {
                            errors.Add($"Negative value not allowed for {parameter.DataType} parameter {parameter.Name}");
                        }
                    }
                    break;
                
                default:
                    warnings.Add($"Unknown data type {parameter.DataType}, cannot validate value");
                    break;
            }
            
            return errors.Count > 0
                ? ValidationResult.Failure(errors, warnings)
                : warnings.Count > 0
                    ? ValidationResult.SuccessWithWarnings(warnings)
                    : ValidationResult.Success();
        }
        
        /// <summary>
        /// Validates parameter exists in document
        /// </summary>
        /// <param name="parameterGuid">Parameter GUID to check</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateExistsInDocument(Guid parameterGuid)
        {
            if (_document == null)
            {
                return ValidationResult.Failure("No document context for validation");
            }
            
            try
            {
                var bindingMap = _document.ParameterBindings;
                var iterator = bindingMap.ForwardIterator();
                
                while (iterator.MoveNext())
                {
                    var definition = iterator.Key as ExternalDefinition;
                    if (definition != null && definition.GUID == parameterGuid)
                    {
                        return ValidationResult.Success();
                    }
                }
                
                return ValidationResult.Failure($"Parameter with GUID {parameterGuid} not found in document");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to validate parameter existence: {parameterGuid}");
                return ValidationResult.Failure($"Validation failed: {ex.Message}");
            }
        }
        
        #endregion

        #region Validation Rules
        
        /// <summary>
        /// Adds a custom validation rule
        /// </summary>
        /// <param name="rule">Validation rule to add</param>
        public void AddRule(ValidationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            
            _rules.Add(rule);
            _logger.Debug($"Added validation rule: {rule.Name}");
        }
        
        /// <summary>
        /// Removes a validation rule
        /// </summary>
        /// <param name="ruleName">Name of rule to remove</param>
        /// <returns>True if rule was removed</returns>
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
        /// Clears all custom validation rules
        /// </summary>
        public void ClearRules()
        {
            _rules.Clear();
            InitializeDefaultRules();
            _logger.Debug("Cleared all custom validation rules, restored defaults");
        }
        
        /// <summary>
        /// Gets all active validation rules
        /// </summary>
        /// <returns>List of validation rules</returns>
        public List<ValidationRule> GetRules()
        {
            return new List<ValidationRule>(_rules);
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Checks if a data type string is valid
        /// </summary>
        private bool IsValidDataType(string dataType)
        {
            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TEXT", "INTEGER", "NUMBER", "LENGTH", "AREA", "VOLUME",
                "ANGLE", "URL", "YESNO", "CURRENCY",
                "ELECTRICAL_CURRENT", "ELECTRICAL_POTENTIAL", "ELECTRICAL_POWER"
            };
            
            return validTypes.Contains(dataType);
        }
        
        /// <summary>
        /// Initializes default validation rules
        /// </summary>
        private void InitializeDefaultRules()
        {
            // ISO 19650 compliance rule
            _rules.Add(new ValidationRule(
                "ISO19650Compliance",
                p => !string.IsNullOrEmpty(p.Description) && p.Description.Contains("ISO 19650"),
                p =>
                {
                    if (string.IsNullOrEmpty(p.Description) || !p.Description.Contains("ISO 19650"))
                    {
                        return ValidationResult.Warning($"Parameter {p.Name} may not be ISO 19650 compliant");
                    }
                    return ValidationResult.Success();
                }));
            
            // Group naming convention rule
            _rules.Add(new ValidationRule(
                "GroupNamingConvention",
                p => true,
                p =>
                {
                    if (!p.GroupName.Contains("_"))
                    {
                        return ValidationResult.Warning($"Group name {p.GroupName} doesn't follow naming convention (should contain underscore)");
                    }
                    return ValidationResult.Success();
                }));
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a ParameterValidator for the specified document
        /// </summary>
        public static ParameterValidator For(Document document)
        {
            return new ParameterValidator(document);
        }
        
        /// <summary>
        /// Creates a ParameterValidator without document context
        /// </summary>
        public static ParameterValidator CreateStandalone()
        {
            return new ParameterValidator(null);
        }
        
        #endregion
    }
    
    #region Support Classes
    
    /// <summary>
    /// Represents a validation result
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether validation passed (no errors)
        /// </summary>
        public bool IsValid { get; }
        
        /// <summary>
        /// Gets whether validation has warnings
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;
        
        /// <summary>
        /// Gets validation errors
        /// </summary>
        public IReadOnlyList<string> Errors { get; }
        
        /// <summary>
        /// Gets validation warnings
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }
        
        /// <summary>
        /// Gets combined error message
        /// </summary>
        public string ErrorMessage => string.Join("; ", Errors);
        
        /// <summary>
        /// Gets combined warning message
        /// </summary>
        public string WarningMessage => string.Join("; ", Warnings);
        
        private ValidationResult(bool isValid, List<string> errors, List<string> warnings)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }
        
        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static ValidationResult Success()
        {
            return new ValidationResult(true, new List<string>(), new List<string>());
        }
        
        /// <summary>
        /// Creates a successful result with warnings
        /// </summary>
        public static ValidationResult SuccessWithWarnings(List<string> warnings)
        {
            return new ValidationResult(true, new List<string>(), warnings);
        }
        
        /// <summary>
        /// Creates a successful result with single warning
        /// </summary>
        public static ValidationResult Warning(string warning)
        {
            return new ValidationResult(true, new List<string>(), new List<string> { warning });
        }
        
        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        public static ValidationResult Failure(string error)
        {
            return new ValidationResult(false, new List<string> { error }, new List<string>());
        }
        
        /// <summary>
        /// Creates a failed validation result with multiple errors
        /// </summary>
        public static ValidationResult Failure(List<string> errors, List<string> warnings = null)
        {
            return new ValidationResult(false, errors, warnings ?? new List<string>());
        }
        
        /// <summary>
        /// Returns string representation of result
        /// </summary>
        public override string ToString()
        {
            if (IsValid && !HasWarnings)
                return "Validation passed";
            
            var parts = new List<string>();
            if (!IsValid)
                parts.Add($"Errors: {ErrorMessage}");
            if (HasWarnings)
                parts.Add($"Warnings: {WarningMessage}");
            
            return string.Join(" | ", parts);
        }
    }
    
    /// <summary>
    /// Represents a custom validation rule
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// Gets the rule name
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the predicate to determine if rule applies
        /// </summary>
        public Func<ParameterDefinition, bool> AppliesTo { get; }
        
        /// <summary>
        /// Gets the validation function
        /// </summary>
        public Func<ParameterDefinition, ValidationResult> Validate { get; }
        
        /// <summary>
        /// Creates a new validation rule
        /// </summary>
        public ValidationRule(
            string name,
            Func<ParameterDefinition, bool> appliesTo,
            Func<ParameterDefinition, ValidationResult> validate)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            AppliesTo = appliesTo ?? throw new ArgumentNullException(nameof(appliesTo));
            Validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }
    }
    
    #endregion
}
