// ParameterAutoPopulator.cs
// StingBIM AI - Automatic Parameter Population from Revit Native Parameters
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Parameters.AutoPopulation
{
    /// <summary>
    /// Automatically populates shared parameters from corresponding Revit native/built-in parameters.
    /// Enables intelligent parameter value transfer, calculation, and lookup-based population.
    /// </summary>
    public class ParameterAutoPopulator
    {
        #region Private Fields

        private readonly Dictionary<string, SharedParameterDefinition> _sharedParameters;
        private readonly Dictionary<string, ParameterMapping> _parameterMappings;
        private readonly Dictionary<string, List<ParameterMapping>> _categoryMappings;
        private readonly Dictionary<string, Func<object, ElementContext, object>> _transformFunctions;
        private readonly Dictionary<string, Dictionary<string, object>> _lookupTables;
        private readonly PopulationStatistics _statistics;
        private readonly object _lockObject = new object();
        private bool _isInitialized;

        #endregion

        #region Constructor

        public ParameterAutoPopulator()
        {
            _sharedParameters = new Dictionary<string, SharedParameterDefinition>(StringComparer.OrdinalIgnoreCase);
            _parameterMappings = new Dictionary<string, ParameterMapping>(StringComparer.OrdinalIgnoreCase);
            _categoryMappings = new Dictionary<string, List<ParameterMapping>>(StringComparer.OrdinalIgnoreCase);
            _transformFunctions = new Dictionary<string, Func<object, ElementContext, object>>(StringComparer.OrdinalIgnoreCase);
            _lookupTables = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            _statistics = new PopulationStatistics();

            InitializeTransformFunctions();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the auto-populator with parameter definitions and mappings.
        /// </summary>
        public async Task InitializeAsync(string parametersPath, string mappingsPath, CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;

                // Load shared parameter definitions
                if (File.Exists(parametersPath))
                {
                    LoadSharedParameters(parametersPath);
                }

                // Load parameter mappings
                if (File.Exists(mappingsPath))
                {
                    LoadParameterMappings(mappingsPath);
                }

                // Build category index
                BuildCategoryIndex();

                // Initialize lookup tables
                InitializeLookupTables();

                _isInitialized = true;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Auto-populate shared parameters for a single element from its native parameters.
        /// </summary>
        public PopulationResult PopulateElement(ElementData element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var result = new PopulationResult
            {
                ElementId = element.ElementId,
                Category = element.Category,
                StartTime = DateTime.Now
            };

            try
            {
                // Get mappings for this category
                if (!_categoryMappings.TryGetValue(element.Category, out var mappings))
                {
                    // Try "All" category for universal parameters
                    _categoryMappings.TryGetValue("All", out mappings);
                }

                if (mappings == null || mappings.Count == 0)
                {
                    result.Status = PopulationStatus.NoMappings;
                    return result;
                }

                var context = new ElementContext
                {
                    ElementId = element.ElementId,
                    Category = element.Category,
                    TypeName = element.TypeName,
                    Level = element.Level,
                    Room = element.Room,
                    NativeParameters = element.NativeParameters,
                    TypeParameters = element.TypeParameters
                };

                // Process each mapping
                foreach (var mapping in mappings.OrderBy(m => m.Priority))
                {
                    try
                    {
                        var populatedValue = PopulateParameter(mapping, context);

                        if (populatedValue != null)
                        {
                            result.PopulatedParameters[mapping.SharedParameterId] = new PopulatedParameter
                            {
                                ParameterId = mapping.SharedParameterId,
                                ParameterName = GetParameterName(mapping.SharedParameterId),
                                Value = populatedValue,
                                Source = mapping.MappingType.ToString(),
                                SourceParameter = mapping.RevitParameterName
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error populating {mapping.SharedParameterId}: {ex.Message}");
                    }
                }

                result.Status = result.PopulatedParameters.Count > 0
                    ? PopulationStatus.Success
                    : PopulationStatus.NoValues;
            }
            catch (Exception ex)
            {
                result.Status = PopulationStatus.Error;
                result.Errors.Add($"Population failed: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
                UpdateStatistics(result);
            }

            return result;
        }

        /// <summary>
        /// Auto-populate parameters for multiple elements in batch.
        /// </summary>
        public async Task<BatchPopulationResult> PopulateBatchAsync(
            IEnumerable<ElementData> elements,
            IProgress<PopulationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var elementList = elements.ToList();
            var batchResult = new BatchPopulationResult
            {
                TotalElements = elementList.Count,
                StartTime = DateTime.Now
            };

            var processed = 0;

            foreach (var element in elementList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = PopulateElement(element);
                batchResult.Results.Add(result);

                if (result.Status == PopulationStatus.Success)
                    batchResult.SuccessCount++;
                else if (result.Status == PopulationStatus.Error)
                    batchResult.ErrorCount++;

                processed++;

                progress?.Report(new PopulationProgress
                {
                    Current = processed,
                    Total = elementList.Count,
                    CurrentElement = element.ElementId,
                    PercentComplete = (double)processed / elementList.Count * 100
                });
            }

            batchResult.EndTime = DateTime.Now;
            return batchResult;
        }

        /// <summary>
        /// Get suggested parameters for a schedule based on category.
        /// </summary>
        public List<ScheduleParameterSuggestion> GetScheduleParameterSuggestions(
            string category,
            ScheduleType scheduleType = ScheduleType.General)
        {
            var suggestions = new List<ScheduleParameterSuggestion>();

            // Get parameters for this category
            var categoryParams = _sharedParameters.Values
                .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                           p.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.SortOrder)
                .ToList();

            foreach (var param in categoryParams)
            {
                var suggestion = new ScheduleParameterSuggestion
                {
                    ParameterId = param.ParameterId,
                    ParameterName = param.Name,
                    Description = param.Description,
                    DataType = param.DataType,
                    Group = param.Group,
                    IsRequired = param.IsRequired,
                    CanAutoPopulate = HasAutoPopulateMapping(param.ParameterId),
                    AutoPopulateSource = GetAutoPopulateSource(param.ParameterId),
                    RecommendedForSchedule = IsRecommendedForSchedule(param, scheduleType),
                    Priority = CalculateSuggestionPriority(param, scheduleType)
                };

                suggestions.Add(suggestion);
            }

            return suggestions
                .OrderByDescending(s => s.RecommendedForSchedule)
                .ThenByDescending(s => s.Priority)
                .ThenBy(s => s.ParameterName)
                .ToList();
        }

        /// <summary>
        /// Get auto-populate preview for an element (shows what values would be populated).
        /// </summary>
        public PopulationPreview GetPopulationPreview(ElementData element)
        {
            var preview = new PopulationPreview
            {
                ElementId = element.ElementId,
                Category = element.Category
            };

            if (!_categoryMappings.TryGetValue(element.Category, out var mappings))
            {
                _categoryMappings.TryGetValue("All", out mappings);
            }

            if (mappings == null) return preview;

            var context = new ElementContext
            {
                ElementId = element.ElementId,
                Category = element.Category,
                TypeName = element.TypeName,
                Level = element.Level,
                Room = element.Room,
                NativeParameters = element.NativeParameters,
                TypeParameters = element.TypeParameters
            };

            foreach (var mapping in mappings)
            {
                var previewItem = new ParameterPreviewItem
                {
                    SharedParameterId = mapping.SharedParameterId,
                    SharedParameterName = GetParameterName(mapping.SharedParameterId),
                    MappingType = mapping.MappingType.ToString(),
                    SourceParameter = mapping.RevitParameterName
                };

                // Check if native parameter exists
                previewItem.SourceAvailable = HasSourceParameter(mapping, context);

                // Try to get preview value
                if (previewItem.SourceAvailable)
                {
                    try
                    {
                        previewItem.PreviewValue = PopulateParameter(mapping, context);
                        previewItem.WillPopulate = previewItem.PreviewValue != null;
                    }
                    catch
                    {
                        previewItem.WillPopulate = false;
                    }
                }

                preview.Parameters.Add(previewItem);
            }

            return preview;
        }

        /// <summary>
        /// Calculate derived/formula-based parameters for an element.
        /// </summary>
        public Dictionary<string, object> CalculateDerivedParameters(ElementData element)
        {
            var derived = new Dictionary<string, object>();

            var formulaParams = _sharedParameters.Values
                .Where(p => p.AutoPopulateSource == "Calculated" && !string.IsNullOrEmpty(p.Formula))
                .ToList();

            var context = new ElementContext
            {
                ElementId = element.ElementId,
                Category = element.Category,
                NativeParameters = element.NativeParameters,
                TypeParameters = element.TypeParameters
            };

            foreach (var param in formulaParams)
            {
                try
                {
                    var value = EvaluateFormula(param.Formula, context);
                    if (value != null)
                    {
                        derived[param.ParameterId] = value;
                    }
                }
                catch
                {
                    // Skip parameters that can't be calculated
                }
            }

            return derived;
        }

        /// <summary>
        /// Validate parameter values against defined rules.
        /// </summary>
        public ValidationResult ValidateParameterValues(ElementData element, Dictionary<string, object> values)
        {
            var result = new ValidationResult
            {
                ElementId = element.ElementId,
                IsValid = true
            };

            foreach (var kvp in values)
            {
                if (!_sharedParameters.TryGetValue(kvp.Key, out var paramDef))
                    continue;

                var validation = ValidateValue(kvp.Value, paramDef);

                if (!validation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        ParameterId = kvp.Key,
                        ParameterName = paramDef.Name,
                        Value = kvp.Value?.ToString(),
                        Rule = paramDef.ValidationRule,
                        Message = validation.Message
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Get statistics about auto-population performance.
        /// </summary>
        public PopulationStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new PopulationStatistics
                {
                    TotalElementsProcessed = _statistics.TotalElementsProcessed,
                    TotalParametersPopulated = _statistics.TotalParametersPopulated,
                    SuccessRate = _statistics.SuccessRate,
                    AverageProcessingTimeMs = _statistics.AverageProcessingTimeMs,
                    MostPopulatedCategories = new Dictionary<string, int>(_statistics.MostPopulatedCategories),
                    MostUsedMappings = new Dictionary<string, int>(_statistics.MostUsedMappings)
                };
            }
        }

        #endregion

        #region Private Methods - Loading

        private void LoadSharedParameters(string path)
        {
            var lines = File.ReadAllLines(path);
            var isFirstLine = true;

            foreach (var line in lines)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var parts = ParseCsvLine(line);
                    if (parts.Length < 15) continue;

                    var param = new SharedParameterDefinition
                    {
                        ParameterId = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Discipline = parts[2].Trim(),
                        Category = parts[3].Trim(),
                        DataType = parts[4].Trim(),
                        Group = parts[5].Trim(),
                        Unit = parts[6].Trim(),
                        Description = parts[7].Trim(),
                        RevitNativeMapping = parts[8].Trim(),
                        AutoPopulateSource = parts[9].Trim(),
                        Formula = parts[10].Trim(),
                        DefaultValue = parts[11].Trim(),
                        ValidationRule = parts[12].Trim(),
                        IsRequired = parts[13].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase),
                        IsReadOnly = parts[14].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase),
                        SortOrder = parts.Length > 15 && int.TryParse(parts[15], out var sort) ? sort : 999
                    };

                    _sharedParameters[param.ParameterId] = param;
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }

        private void LoadParameterMappings(string path)
        {
            var lines = File.ReadAllLines(path);
            var isFirstLine = true;

            foreach (var line in lines)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var parts = ParseCsvLine(line);
                    if (parts.Length < 8) continue;

                    var mapping = new ParameterMapping
                    {
                        SharedParameterId = parts[0].Trim(),
                        RevitBuiltInParameter = parts[1].Trim(),
                        RevitParameterName = parts[2].Trim(),
                        Category = parts[3].Trim(),
                        MappingType = ParseMappingType(parts[4].Trim()),
                        TransformFunction = parts[5].Trim(),
                        FallbackSource = parts[6].Trim(),
                        Priority = int.TryParse(parts[7].Trim(), out var priority) ? priority : 5,
                        Notes = parts.Length > 8 ? parts[8].Trim() : ""
                    };

                    _parameterMappings[mapping.SharedParameterId] = mapping;
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }

        private void BuildCategoryIndex()
        {
            _categoryMappings.Clear();

            foreach (var mapping in _parameterMappings.Values)
            {
                if (!_categoryMappings.ContainsKey(mapping.Category))
                {
                    _categoryMappings[mapping.Category] = new List<ParameterMapping>();
                }
                _categoryMappings[mapping.Category].Add(mapping);
            }
        }

        private void InitializeLookupTables()
        {
            // Room type to occupancy classification lookup
            _lookupTables["RoomTypeToOccupancy"] = new Dictionary<string, object>
            {
                { "Office", "B" },
                { "Conference", "B" },
                { "Meeting Room", "B" },
                { "Classroom", "E" },
                { "Laboratory", "B" },
                { "Hospital Room", "I-2" },
                { "Hotel Room", "R-1" },
                { "Apartment", "R-2" },
                { "Retail", "M" },
                { "Restaurant", "A-2" },
                { "Assembly", "A-1" },
                { "Storage", "S-1" },
                { "Warehouse", "S-2" },
                { "Parking", "S-2" },
                { "Industrial", "F-1" },
                { "Residential", "R-3" }
            };

            // Room type to occupancy factor (m² per person)
            _lookupTables["OccupancyFactors"] = new Dictionary<string, object>
            {
                { "Office", 10.0 },
                { "Open Office", 7.0 },
                { "Conference", 1.4 },
                { "Meeting Room", 1.9 },
                { "Classroom", 1.9 },
                { "Laboratory", 5.0 },
                { "Corridor", 0.0 },
                { "Lobby", 1.4 },
                { "Reception", 2.8 },
                { "Restaurant", 1.4 },
                { "Retail", 5.6 },
                { "Storage", 28.0 },
                { "Residential", 18.6 }
            };

            // Room type to ventilation rate (L/s per person)
            _lookupTables["VentilationRates"] = new Dictionary<string, object>
            {
                { "Office", 10.0 },
                { "Conference", 10.0 },
                { "Meeting Room", 10.0 },
                { "Classroom", 10.0 },
                { "Laboratory", 15.0 },
                { "Corridor", 0.3 }, // per m²
                { "Lobby", 10.0 },
                { "Restaurant", 10.0 },
                { "Retail", 7.5 },
                { "Gym", 20.0 },
                { "Residential", 8.5 }
            };

            // Room type to lighting level (lux)
            _lookupTables["LightingLevels"] = new Dictionary<string, object>
            {
                { "Office", 500 },
                { "Open Office", 500 },
                { "Conference", 500 },
                { "Meeting Room", 500 },
                { "Classroom", 500 },
                { "Laboratory", 750 },
                { "Corridor", 100 },
                { "Lobby", 200 },
                { "Reception", 300 },
                { "Restaurant", 200 },
                { "Retail", 500 },
                { "Storage", 150 },
                { "Residential", 300 },
                { "Hospital Room", 300 },
                { "Operating Room", 1000 }
            };

            // Acoustic ratings by room type
            _lookupTables["AcousticRatings"] = new Dictionary<string, object>
            {
                { "Office", "STC 45" },
                { "Conference", "STC 50" },
                { "Meeting Room", "STC 50" },
                { "Classroom", "STC 50" },
                { "Hospital Room", "STC 50" },
                { "Hotel Room", "STC 55" },
                { "Recording Studio", "STC 65" },
                { "Auditorium", "STC 60" }
            };

            // Fire rating lookup by wall function
            _lookupTables["FireRatings"] = new Dictionary<string, object>
            {
                { "Shaft Wall", "2 Hour" },
                { "Stair Enclosure", "2 Hour" },
                { "Corridor Wall", "1 Hour" },
                { "Exit Passageway", "1 Hour" },
                { "Tenant Separation", "1 Hour" },
                { "Occupancy Separation", "2 Hour" }
            };
        }

        #endregion

        #region Private Methods - Population

        private object PopulateParameter(ParameterMapping mapping, ElementContext context)
        {
            switch (mapping.MappingType)
            {
                case MappingType.Direct:
                    return GetDirectValue(mapping, context);

                case MappingType.Reference:
                    return GetReferenceValue(mapping, context);

                case MappingType.Type:
                    return GetTypeValue(mapping, context);

                case MappingType.Calculated:
                    return GetCalculatedValue(mapping, context);

                case MappingType.Lookup:
                    return GetLookupValue(mapping, context);

                case MappingType.User:
                    return GetUserValue(mapping, context);

                default:
                    return null;
            }
        }

        private object GetDirectValue(ParameterMapping mapping, ElementContext context)
        {
            // Direct mapping from native instance parameter
            if (context.NativeParameters.TryGetValue(mapping.RevitParameterName, out var value))
            {
                if (!string.IsNullOrEmpty(mapping.TransformFunction))
                {
                    return ApplyTransform(mapping.TransformFunction, value, context);
                }
                return value;
            }

            // Try built-in parameter name
            if (context.NativeParameters.TryGetValue(mapping.RevitBuiltInParameter, out value))
            {
                if (!string.IsNullOrEmpty(mapping.TransformFunction))
                {
                    return ApplyTransform(mapping.TransformFunction, value, context);
                }
                return value;
            }

            return null;
        }

        private object GetReferenceValue(ParameterMapping mapping, ElementContext context)
        {
            // Reference to related element (e.g., Room, Level)
            var paramName = mapping.RevitParameterName;

            // Handle compound references like "From Room:Name"
            if (paramName.Contains(":"))
            {
                var parts = paramName.Split(':');
                var refElement = parts[0];
                var refProperty = parts[1];

                // This would need to resolve the reference in actual Revit implementation
                // For now, return from context if available
                if (refElement.Equals("Room", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(context.Room))
                {
                    return context.Room;
                }
                if (refElement.Equals("Level", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(context.Level))
                {
                    return context.Level;
                }
            }

            // Simple reference
            if (paramName.Equals("Level", StringComparison.OrdinalIgnoreCase))
                return context.Level;
            if (paramName.Equals("Room", StringComparison.OrdinalIgnoreCase))
                return context.Room;

            // Try from native parameters
            return context.NativeParameters.TryGetValue(paramName, out var value) ? value : null;
        }

        private object GetTypeValue(ParameterMapping mapping, ElementContext context)
        {
            // Get value from type parameters
            if (context.TypeParameters.TryGetValue(mapping.RevitParameterName, out var value))
                return value;

            if (context.TypeParameters.TryGetValue(mapping.RevitBuiltInParameter, out value))
                return value;

            return null;
        }

        private object GetCalculatedValue(ParameterMapping mapping, ElementContext context)
        {
            // Get formula from shared parameter definition
            if (_sharedParameters.TryGetValue(mapping.SharedParameterId, out var paramDef) &&
                !string.IsNullOrEmpty(paramDef.Formula))
            {
                return EvaluateFormula(paramDef.Formula, context);
            }

            // Handle transform function as formula
            if (!string.IsNullOrEmpty(mapping.TransformFunction))
            {
                return EvaluateFormula(mapping.TransformFunction, context);
            }

            return null;
        }

        private object GetLookupValue(ParameterMapping mapping, ElementContext context)
        {
            // Get lookup table name from transform function
            var tableName = mapping.TransformFunction;
            if (string.IsNullOrEmpty(tableName)) return null;

            if (!_lookupTables.TryGetValue(tableName, out var lookupTable))
                return null;

            // Determine lookup key based on context
            string lookupKey = null;

            // Try room name for room-based lookups
            if (!string.IsNullOrEmpty(context.Room))
            {
                lookupKey = FindBestLookupMatch(context.Room, lookupTable.Keys);
            }

            // Try type name
            if (lookupKey == null && !string.IsNullOrEmpty(context.TypeName))
            {
                lookupKey = FindBestLookupMatch(context.TypeName, lookupTable.Keys);
            }

            // Try category
            if (lookupKey == null)
            {
                lookupKey = FindBestLookupMatch(context.Category, lookupTable.Keys);
            }

            if (lookupKey != null && lookupTable.TryGetValue(lookupKey, out var value))
            {
                return value;
            }

            return null;
        }

        private object GetUserValue(ParameterMapping mapping, ElementContext context)
        {
            // User-entered values - return default if defined
            if (_sharedParameters.TryGetValue(mapping.SharedParameterId, out var paramDef) &&
                !string.IsNullOrEmpty(paramDef.DefaultValue) &&
                paramDef.DefaultValue != "None")
            {
                return ConvertDefaultValue(paramDef.DefaultValue, paramDef.DataType);
            }

            return null;
        }

        private string FindBestLookupMatch(string input, IEnumerable<string> keys)
        {
            if (string.IsNullOrEmpty(input)) return null;

            var keyList = keys.ToList();

            // Exact match
            var exact = keyList.FirstOrDefault(k =>
                k.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Contains match
            var contains = keyList.FirstOrDefault(k =>
                input.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
            if (contains != null) return contains;

            // Reverse contains
            var reverseContains = keyList.FirstOrDefault(k =>
                k.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
            if (reverseContains != null) return reverseContains;

            return null;
        }

        private object ApplyTransform(string functionName, object value, ElementContext context)
        {
            if (_transformFunctions.TryGetValue(functionName, out var func))
            {
                return func(value, context);
            }

            // Check if it's a formula
            if (functionName.Contains("*") || functionName.Contains("/") ||
                functionName.Contains("+") || functionName.Contains("-"))
            {
                return EvaluateFormula(functionName, context);
            }

            return value;
        }

        private object EvaluateFormula(string formula, ElementContext context)
        {
            if (string.IsNullOrEmpty(formula)) return null;

            try
            {
                // Simple formula evaluation
                var expression = formula;

                // Replace parameter references with values
                var paramPattern = new Regex(@"([A-Za-z_][A-Za-z0-9_]*)");
                var matches = paramPattern.Matches(formula);

                foreach (Match match in matches)
                {
                    var paramName = match.Value;
                    object paramValue = null;

                    // Try native parameters
                    if (context.NativeParameters.TryGetValue(paramName, out paramValue))
                    {
                        expression = expression.Replace(paramName, paramValue.ToString());
                    }
                    // Try type parameters
                    else if (context.TypeParameters.TryGetValue(paramName, out paramValue))
                    {
                        expression = expression.Replace(paramName, paramValue.ToString());
                    }
                    // Try lookup tables
                    else if (_lookupTables.TryGetValue(paramName, out var lookup))
                    {
                        var key = FindBestLookupMatch(context.Room ?? context.Category, lookup.Keys);
                        if (key != null && lookup.TryGetValue(key, out paramValue))
                        {
                            expression = expression.Replace(paramName, paramValue.ToString());
                        }
                    }
                }

                // Evaluate simple arithmetic
                return EvaluateArithmetic(expression);
            }
            catch
            {
                return null;
            }
        }

        private object EvaluateArithmetic(string expression)
        {
            try
            {
                // Simple arithmetic evaluation using DataTable.Compute
                var dt = new System.Data.DataTable();
                var result = dt.Compute(expression, string.Empty);
                return result;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Private Methods - Transform Functions

        private void InitializeTransformFunctions()
        {
            // Unit conversions
            _transformFunctions["ToMeters"] = (value, ctx) =>
            {
                if (value is double d) return d / 1000.0; // mm to m
                if (double.TryParse(value?.ToString(), out var v)) return v / 1000.0;
                return value;
            };

            _transformFunctions["ToMillimeters"] = (value, ctx) =>
            {
                if (value is double d) return d * 1000.0; // m to mm
                if (double.TryParse(value?.ToString(), out var v)) return v * 1000.0;
                return value;
            };

            _transformFunctions["ToSquareMeters"] = (value, ctx) =>
            {
                if (value is double d) return d / 1000000.0; // mm² to m²
                if (double.TryParse(value?.ToString(), out var v)) return v / 1000000.0;
                return value;
            };

            // Revit internal units (feet) to metric
            _transformFunctions["FeetToMeters"] = (value, ctx) =>
            {
                if (value is double d) return d * 0.3048;
                if (double.TryParse(value?.ToString(), out var v)) return v * 0.3048;
                return value;
            };

            _transformFunctions["SquareFeetToSquareMeters"] = (value, ctx) =>
            {
                if (value is double d) return d * 0.092903;
                if (double.TryParse(value?.ToString(), out var v)) return v * 0.092903;
                return value;
            };

            _transformFunctions["CubicFeetToCubicMeters"] = (value, ctx) =>
            {
                if (value is double d) return d * 0.0283168;
                if (double.TryParse(value?.ToString(), out var v)) return v * 0.0283168;
                return value;
            };

            // Geometry calculations
            _transformFunctions["FromGeometry"] = (value, ctx) =>
            {
                // Placeholder - would calculate from element geometry in actual Revit implementation
                return value;
            };

            // Area calculations
            _transformFunctions["Width*Height"] = (value, ctx) =>
            {
                if (ctx.NativeParameters.TryGetValue("Width", out var width) &&
                    ctx.NativeParameters.TryGetValue("Height", out var height))
                {
                    var w = Convert.ToDouble(width);
                    var h = Convert.ToDouble(height);
                    return (w * h) / 1000000.0; // mm² to m²
                }
                return null;
            };

            // Weight calculations
            _transformFunctions["Volume*Density"] = (value, ctx) =>
            {
                if (ctx.NativeParameters.TryGetValue("Volume", out var volume))
                {
                    var v = Convert.ToDouble(volume);
                    // Assume concrete density 2400 kg/m³
                    return v * 2400.0;
                }
                return null;
            };

            // Accessibility check
            _transformFunctions["CheckWidth>=900"] = (value, ctx) =>
            {
                if (ctx.NativeParameters.TryGetValue("Width", out var width))
                {
                    var w = Convert.ToDouble(width);
                    return w >= 900;
                }
                return false;
            };
        }

        #endregion

        #region Private Methods - Validation

        private (bool IsValid, string Message) ValidateValue(object value, SharedParameterDefinition paramDef)
        {
            if (value == null)
            {
                if (paramDef.IsRequired)
                    return (false, "Required parameter is empty");
                return (true, null);
            }

            var rule = paramDef.ValidationRule;
            if (string.IsNullOrEmpty(rule) || rule == "None")
                return (true, null);

            var valueStr = value.ToString();

            // NotEmpty
            if (rule.Equals("NotEmpty", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(valueStr))
                    return (false, "Value cannot be empty");
                return (true, null);
            }

            // GreaterThan:X
            if (rule.StartsWith("GreaterThan:", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(rule.Substring(12), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var threshold))
                {
                    if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var numValue))
                    {
                        if (numValue <= threshold)
                            return (false, $"Value must be greater than {threshold}");
                    }
                }
                return (true, null);
            }

            // Range:X-Y
            if (rule.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
            {
                var rangeParts = rule.Substring(6).Split('-');
                if (rangeParts.Length == 2 &&
                    double.TryParse(rangeParts[0], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var min) &&
                    double.TryParse(rangeParts[1], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var max))
                {
                    if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var numValue))
                    {
                        if (numValue < min || numValue > max)
                            return (false, $"Value must be between {min} and {max}");
                    }
                }
                return (true, null);
            }

            // ValidDate
            if (rule.Equals("ValidDate", StringComparison.OrdinalIgnoreCase))
            {
                if (!DateTime.TryParse(valueStr, out _))
                    return (false, "Invalid date format");
                return (true, null);
            }

            return (true, null);
        }

        #endregion

        #region Private Methods - Helpers

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private MappingType ParseMappingType(string value)
        {
            return Enum.TryParse<MappingType>(value, true, out var result)
                ? result
                : MappingType.Direct;
        }

        private string GetParameterName(string parameterId)
        {
            return _sharedParameters.TryGetValue(parameterId, out var param)
                ? param.Name
                : parameterId;
        }

        private bool HasAutoPopulateMapping(string parameterId)
        {
            return _parameterMappings.ContainsKey(parameterId);
        }

        private string GetAutoPopulateSource(string parameterId)
        {
            if (_parameterMappings.TryGetValue(parameterId, out var mapping))
            {
                return $"{mapping.MappingType}: {mapping.RevitParameterName}";
            }
            if (_sharedParameters.TryGetValue(parameterId, out var param))
            {
                return param.AutoPopulateSource;
            }
            return null;
        }

        private bool HasSourceParameter(ParameterMapping mapping, ElementContext context)
        {
            switch (mapping.MappingType)
            {
                case MappingType.Direct:
                    return context.NativeParameters.ContainsKey(mapping.RevitParameterName) ||
                           context.NativeParameters.ContainsKey(mapping.RevitBuiltInParameter);
                case MappingType.Type:
                    return context.TypeParameters.ContainsKey(mapping.RevitParameterName) ||
                           context.TypeParameters.ContainsKey(mapping.RevitBuiltInParameter);
                case MappingType.Reference:
                    return !string.IsNullOrEmpty(context.Level) || !string.IsNullOrEmpty(context.Room);
                case MappingType.Calculated:
                case MappingType.Lookup:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsRecommendedForSchedule(SharedParameterDefinition param, ScheduleType scheduleType)
        {
            // Core identity parameters are always recommended
            if (param.Group == "Identity") return true;

            // Required parameters are recommended
            if (param.IsRequired) return true;

            // Dimension parameters for quantity schedules
            if (scheduleType == ScheduleType.Quantities && param.Group == "Dimensions")
                return true;

            // Performance parameters for equipment schedules
            if (scheduleType == ScheduleType.Equipment && param.Group == "Performance")
                return true;

            // Material parameters for material schedules
            if (scheduleType == ScheduleType.Materials && param.Group == "Materials")
                return true;

            return false;
        }

        private int CalculateSuggestionPriority(SharedParameterDefinition param, ScheduleType scheduleType)
        {
            var priority = 0;

            if (param.IsRequired) priority += 100;
            if (param.Group == "Identity") priority += 50;
            if (HasAutoPopulateMapping(param.ParameterId)) priority += 30;
            if (param.SortOrder < 10) priority += 20;

            return priority;
        }

        private object ConvertDefaultValue(string defaultValue, string dataType)
        {
            if (string.IsNullOrEmpty(defaultValue) || defaultValue == "None")
                return null;

            switch (dataType.ToLowerInvariant())
            {
                case "integer":
                    return int.TryParse(defaultValue, out var intVal) ? intVal : (object)null;
                case "number":
                case "area":
                case "volume":
                case "length":
                    return double.TryParse(defaultValue, out var dblVal) ? dblVal : (object)null;
                case "yesno":
                    return defaultValue.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                           defaultValue.Equals("True", StringComparison.OrdinalIgnoreCase);
                default:
                    return defaultValue;
            }
        }

        private void UpdateStatistics(PopulationResult result)
        {
            lock (_lockObject)
            {
                _statistics.TotalElementsProcessed++;
                _statistics.TotalParametersPopulated += result.PopulatedParameters.Count;

                if (result.Status == PopulationStatus.Success)
                {
                    _statistics.SuccessCount++;

                    if (!_statistics.MostPopulatedCategories.ContainsKey(result.Category))
                        _statistics.MostPopulatedCategories[result.Category] = 0;
                    _statistics.MostPopulatedCategories[result.Category]++;
                }

                _statistics.SuccessRate = (double)_statistics.SuccessCount / _statistics.TotalElementsProcessed * 100;

                var processingTime = (result.EndTime - result.StartTime).TotalMilliseconds;
                _statistics.TotalProcessingTimeMs += processingTime;
                _statistics.AverageProcessingTimeMs = _statistics.TotalProcessingTimeMs / _statistics.TotalElementsProcessed;
            }
        }

        #endregion
    }

    #region Supporting Types

    public class SharedParameterDefinition
    {
        public string ParameterId { get; set; }
        public string Name { get; set; }
        public string Discipline { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
        public string RevitNativeMapping { get; set; }
        public string AutoPopulateSource { get; set; }
        public string Formula { get; set; }
        public string DefaultValue { get; set; }
        public string ValidationRule { get; set; }
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }
        public int SortOrder { get; set; }

        /// <summary>
        /// Creates a shallow copy of this parameter definition.
        /// </summary>
        public SharedParameterDefinition Clone() => (SharedParameterDefinition)MemberwiseClone();
    }

    public class ParameterMapping
    {
        public string SharedParameterId { get; set; }
        public string RevitBuiltInParameter { get; set; }
        public string RevitParameterName { get; set; }
        public string Category { get; set; }
        public MappingType MappingType { get; set; }
        public string TransformFunction { get; set; }
        public string FallbackSource { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
    }

    public enum MappingType
    {
        Direct,     // Direct mapping from native instance parameter
        Reference,  // Reference to related element (Room, Level, etc.)
        Type,       // Value from type/family parameter
        Calculated, // Calculated from formula
        Lookup,     // Lookup from table based on context
        User        // User-entered value
    }

    public class ElementData
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public string Room { get; set; }
        public Dictionary<string, object> NativeParameters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> TypeParameters { get; set; } = new Dictionary<string, object>();
    }

    public class ElementContext
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public string Room { get; set; }
        public Dictionary<string, object> NativeParameters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> TypeParameters { get; set; } = new Dictionary<string, object>();
    }

    public class PopulationResult
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public PopulationStatus Status { get; set; }
        public Dictionary<string, PopulatedParameter> PopulatedParameters { get; set; } = new Dictionary<string, PopulatedParameter>();
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class PopulatedParameter
    {
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public object Value { get; set; }
        public string Source { get; set; }
        public string SourceParameter { get; set; }
    }

    public enum PopulationStatus
    {
        Success,
        NoMappings,
        NoValues,
        Error
    }

    public class BatchPopulationResult
    {
        public int TotalElements { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<PopulationResult> Results { get; set; } = new List<PopulationResult>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }

    public class PopulationProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentElement { get; set; }
        public double PercentComplete { get; set; }
    }

    public class ScheduleParameterSuggestion
    {
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public string Description { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public bool IsRequired { get; set; }
        public bool CanAutoPopulate { get; set; }
        public string AutoPopulateSource { get; set; }
        public bool RecommendedForSchedule { get; set; }
        public int Priority { get; set; }
    }

    public enum ScheduleType
    {
        General,
        Quantities,
        Equipment,
        Materials,
        Finishes,
        Coordination
    }

    public class PopulationPreview
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public List<ParameterPreviewItem> Parameters { get; set; } = new List<ParameterPreviewItem>();
    }

    public class ParameterPreviewItem
    {
        public string SharedParameterId { get; set; }
        public string SharedParameterName { get; set; }
        public string MappingType { get; set; }
        public string SourceParameter { get; set; }
        public bool SourceAvailable { get; set; }
        public bool WillPopulate { get; set; }
        public object PreviewValue { get; set; }
    }

    public class ValidationResult
    {
        public string ElementId { get; set; }
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
    }

    public class ValidationError
    {
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public string Value { get; set; }
        public string Rule { get; set; }
        public string Message { get; set; }
    }

    public class PopulationStatistics
    {
        public int TotalElementsProcessed { get; set; }
        public int TotalParametersPopulated { get; set; }
        public int SuccessCount { get; set; }
        public double SuccessRate { get; set; }
        public double TotalProcessingTimeMs { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public Dictionary<string, int> MostPopulatedCategories { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> MostUsedMappings { get; set; } = new Dictionary<string, int>();
    }

    #endregion
}
