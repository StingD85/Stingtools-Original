// ParameterIntelligenceEngine.cs
// StingBIM AI - Intelligent Parameter Suggestions and Value Prediction
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Parameters.Intelligence
{
    /// <summary>
    /// Provides intelligent parameter suggestions, value predictions, and context-aware
    /// recommendations based on project context, learned patterns, and design standards.
    /// </summary>
    public class ParameterIntelligenceEngine
    {
        #region Private Fields

        private readonly Dictionary<string, ParameterPattern> _learnedPatterns;
        private readonly Dictionary<string, List<ParameterCorrelation>> _correlations;
        private readonly Dictionary<string, ParameterUsageStats> _usageStats;
        private readonly Dictionary<string, RoomParameterProfile> _roomProfiles;
        private readonly Dictionary<string, StandardParameterSet> _standardSets;
        private readonly ParameterValuePredictor _predictor;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public ParameterIntelligenceEngine()
        {
            _learnedPatterns = new Dictionary<string, ParameterPattern>(StringComparer.OrdinalIgnoreCase);
            _correlations = new Dictionary<string, List<ParameterCorrelation>>(StringComparer.OrdinalIgnoreCase);
            _usageStats = new Dictionary<string, ParameterUsageStats>(StringComparer.OrdinalIgnoreCase);
            _roomProfiles = new Dictionary<string, RoomParameterProfile>(StringComparer.OrdinalIgnoreCase);
            _standardSets = new Dictionary<string, StandardParameterSet>(StringComparer.OrdinalIgnoreCase);
            _predictor = new ParameterValuePredictor();

            InitializeRoomProfiles();
            InitializeStandardParameterSets();
            InitializeParameterCorrelations();
        }

        #endregion

        #region Public Methods - Intelligent Suggestions

        /// <summary>
        /// Get intelligent parameter value suggestions based on context.
        /// </summary>
        public List<ParameterValueSuggestion> GetValueSuggestions(
            string parameterId,
            ParameterContext context)
        {
            var suggestions = new List<ParameterValueSuggestion>();

            // 1. Check learned patterns first
            var patternSuggestion = GetPatternBasedSuggestion(parameterId, context);
            if (patternSuggestion != null)
                suggestions.Add(patternSuggestion);

            // 2. Room profile based suggestions
            var roomSuggestion = GetRoomBasedSuggestion(parameterId, context);
            if (roomSuggestion != null)
                suggestions.Add(roomSuggestion);

            // 3. Standard compliance suggestions
            var standardSuggestions = GetStandardBasedSuggestions(parameterId, context);
            suggestions.AddRange(standardSuggestions);

            // 4. Correlation-based predictions
            var correlationSuggestion = GetCorrelationBasedSuggestion(parameterId, context);
            if (correlationSuggestion != null)
                suggestions.Add(correlationSuggestion);

            // 5. Historical usage suggestions
            var usageSuggestion = GetUsageBasedSuggestion(parameterId, context);
            if (usageSuggestion != null)
                suggestions.Add(usageSuggestion);

            // Sort by confidence and remove duplicates
            return suggestions
                .GroupBy(s => s.Value?.ToString())
                .Select(g => g.OrderByDescending(s => s.Confidence).First())
                .OrderByDescending(s => s.Confidence)
                .Take(5)
                .ToList();
        }

        /// <summary>
        /// Predict parameter value based on ML model and context.
        /// </summary>
        public ParameterPrediction PredictValue(string parameterId, ParameterContext context)
        {
            return _predictor.Predict(parameterId, context);
        }

        /// <summary>
        /// Get recommended parameters for a specific element category and context.
        /// </summary>
        public List<ParameterRecommendation> GetRecommendedParameters(
            string category,
            ParameterContext context)
        {
            var recommendations = new List<ParameterRecommendation>();

            // Get room-specific recommendations
            if (!string.IsNullOrEmpty(context.RoomType) &&
                _roomProfiles.TryGetValue(context.RoomType, out var profile))
            {
                foreach (var reqParam in profile.RequiredParameters)
                {
                    recommendations.Add(new ParameterRecommendation
                    {
                        ParameterId = reqParam,
                        Reason = $"Required for {context.RoomType}",
                        Priority = RecommendationPriority.Required,
                        Source = "Room Profile"
                    });
                }

                foreach (var optParam in profile.OptionalParameters)
                {
                    recommendations.Add(new ParameterRecommendation
                    {
                        ParameterId = optParam,
                        Reason = $"Recommended for {context.RoomType}",
                        Priority = RecommendationPriority.Recommended,
                        Source = "Room Profile"
                    });
                }
            }

            // Get standard-based recommendations
            if (!string.IsNullOrEmpty(context.BuildingCode) &&
                _standardSets.TryGetValue(context.BuildingCode, out var standardSet))
            {
                foreach (var param in standardSet.RequiredParameters)
                {
                    if (!recommendations.Any(r => r.ParameterId == param))
                    {
                        recommendations.Add(new ParameterRecommendation
                        {
                            ParameterId = param,
                            Reason = $"Required by {context.BuildingCode}",
                            Priority = RecommendationPriority.Required,
                            Source = "Building Code"
                        });
                    }
                }
            }

            // Get category-specific recommendations
            var categoryParams = GetCategorySpecificParameters(category);
            foreach (var param in categoryParams)
            {
                if (!recommendations.Any(r => r.ParameterId == param.ParameterId))
                {
                    recommendations.Add(param);
                }
            }

            return recommendations
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.ParameterId)
                .ToList();
        }

        /// <summary>
        /// Analyze parameter completeness for an element.
        /// </summary>
        public CompletenessAnalysis AnalyzeCompleteness(
            string category,
            Dictionary<string, object> currentValues,
            ParameterContext context)
        {
            var analysis = new CompletenessAnalysis
            {
                Category = category
            };

            var recommendations = GetRecommendedParameters(category, context);

            foreach (var rec in recommendations)
            {
                var hasValue = currentValues.ContainsKey(rec.ParameterId) &&
                              currentValues[rec.ParameterId] != null &&
                              !string.IsNullOrEmpty(currentValues[rec.ParameterId].ToString());

                if (rec.Priority == RecommendationPriority.Required)
                {
                    analysis.RequiredCount++;
                    if (hasValue) analysis.RequiredFilledCount++;
                    else analysis.MissingRequired.Add(rec.ParameterId);
                }
                else if (rec.Priority == RecommendationPriority.Recommended)
                {
                    analysis.RecommendedCount++;
                    if (hasValue) analysis.RecommendedFilledCount++;
                    else analysis.MissingRecommended.Add(rec.ParameterId);
                }
            }

            analysis.CompletenessScore = analysis.RequiredCount > 0
                ? (double)analysis.RequiredFilledCount / analysis.RequiredCount * 100
                : 100;

            return analysis;
        }

        /// <summary>
        /// Detect parameter value anomalies.
        /// </summary>
        public List<ParameterAnomaly> DetectAnomalies(
            string category,
            Dictionary<string, object> values,
            ParameterContext context)
        {
            var anomalies = new List<ParameterAnomaly>();

            foreach (var kvp in values)
            {
                // Check against learned patterns
                if (_learnedPatterns.TryGetValue(kvp.Key, out var pattern))
                {
                    var anomaly = CheckValueAgainstPattern(kvp.Key, kvp.Value, pattern, context);
                    if (anomaly != null)
                        anomalies.Add(anomaly);
                }

                // Check against room profile expectations
                if (!string.IsNullOrEmpty(context.RoomType) &&
                    _roomProfiles.TryGetValue(context.RoomType, out var profile))
                {
                    var anomaly = CheckValueAgainstRoomProfile(kvp.Key, kvp.Value, profile);
                    if (anomaly != null)
                        anomalies.Add(anomaly);
                }

                // Check correlations
                var correlationAnomaly = CheckCorrelationAnomalies(kvp.Key, kvp.Value, values);
                if (correlationAnomaly != null)
                    anomalies.Add(correlationAnomaly);
            }

            return anomalies;
        }

        #endregion

        #region Public Methods - Learning

        /// <summary>
        /// Learn parameter patterns from existing element data.
        /// </summary>
        public void LearnFromElement(string category, Dictionary<string, object> values, ParameterContext context)
        {
            lock (_lockObject)
            {
                foreach (var kvp in values)
                {
                    if (kvp.Value == null) continue;

                    // Update usage stats
                    if (!_usageStats.ContainsKey(kvp.Key))
                    {
                        _usageStats[kvp.Key] = new ParameterUsageStats { ParameterId = kvp.Key };
                    }

                    _usageStats[kvp.Key].UsageCount++;

                    // Track value distribution
                    var valueStr = kvp.Value.ToString();
                    if (!_usageStats[kvp.Key].ValueDistribution.ContainsKey(valueStr))
                    {
                        _usageStats[kvp.Key].ValueDistribution[valueStr] = 0;
                    }
                    _usageStats[kvp.Key].ValueDistribution[valueStr]++;

                    // Track numeric ranges
                    if (double.TryParse(valueStr, out var numValue))
                    {
                        if (!_usageStats[kvp.Key].MinValue.HasValue ||
                            numValue < _usageStats[kvp.Key].MinValue)
                            _usageStats[kvp.Key].MinValue = numValue;

                        if (!_usageStats[kvp.Key].MaxValue.HasValue ||
                            numValue > _usageStats[kvp.Key].MaxValue)
                            _usageStats[kvp.Key].MaxValue = numValue;

                        _usageStats[kvp.Key].NumericValues.Add(numValue);
                    }

                    // Track context associations
                    if (!string.IsNullOrEmpty(context.RoomType))
                    {
                        var key = $"{kvp.Key}|{context.RoomType}";
                        if (!_usageStats[kvp.Key].ContextValues.ContainsKey(key))
                        {
                            _usageStats[kvp.Key].ContextValues[key] = new List<object>();
                        }
                        _usageStats[kvp.Key].ContextValues[key].Add(kvp.Value);
                    }
                }

                // Learn correlations between parameters
                var paramList = values.Keys.ToList();
                for (int i = 0; i < paramList.Count; i++)
                {
                    for (int j = i + 1; j < paramList.Count; j++)
                    {
                        UpdateCorrelation(paramList[i], paramList[j], values[paramList[i]], values[paramList[j]]);
                    }
                }
            }
        }

        /// <summary>
        /// Learn from a batch of elements.
        /// </summary>
        public async Task LearnFromBatchAsync(
            IEnumerable<(string Category, Dictionary<string, object> Values, ParameterContext Context)> elements,
            CancellationToken cancellationToken = default)
        {
            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LearnFromElement(element.Category, element.Values, element.Context);
            }

            // Update patterns after batch learning
            UpdatePatternsFromStats();

            await Task.CompletedTask;
        }

        #endregion

        #region Private Methods - Initialization

        private void InitializeRoomProfiles()
        {
            // Office rooms
            _roomProfiles["Office"] = new RoomParameterProfile
            {
                RoomType = "Office",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP017", "SP018"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP015", "SP019", "SP020"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 500 },      // 500 lux
                    { "SP018", 10.0 },     // 10 L/s/person ventilation
                    { "SP006", 2.7 }       // 2.7m height
                }
            };

            // Conference/Meeting rooms
            _roomProfiles["Conference"] = new RoomParameterProfile
            {
                RoomType = "Conference",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP015", "SP017", "SP018"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP016", "SP019", "SP020"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 500 },      // 500 lux
                    { "SP018", 10.0 },     // 10 L/s/person
                    { "SP015", "STC 50" }, // Sound rating
                    { "SP006", 3.0 }       // 3.0m height
                }
            };

            // Classroom
            _roomProfiles["Classroom"] = new RoomParameterProfile
            {
                RoomType = "Classroom",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP015", "SP017", "SP018"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP016", "SP019"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 500 },      // 500 lux
                    { "SP018", 10.0 },     // 10 L/s/person
                    { "SP015", "STC 50" }, // Sound rating
                    { "SP006", 3.0 }       // 3.0m height
                }
            };

            // Laboratory
            _roomProfiles["Laboratory"] = new RoomParameterProfile
            {
                RoomType = "Laboratory",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP017", "SP018", "SP019"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP015", "SP016"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 750 },      // 750 lux
                    { "SP018", 15.0 },     // 15 L/s/person
                    { "SP006", 3.2 }       // 3.2m height for services
                }
            };

            // Hospital/Clinical
            _roomProfiles["Hospital Room"] = new RoomParameterProfile
            {
                RoomType = "Hospital Room",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP015", "SP016", "SP017", "SP018"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP019", "SP020"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 300 },
                    { "SP015", "STC 50" },
                    { "SP006", 2.9 }
                }
            };

            // Corridor
            _roomProfiles["Corridor"] = new RoomParameterProfile
            {
                RoomType = "Corridor",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP017"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP016"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 100 },      // 100 lux
                    { "SP006", 2.7 }       // 2.7m height
                }
            };

            // Lobby/Reception
            _roomProfiles["Lobby"] = new RoomParameterProfile
            {
                RoomType = "Lobby",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP017"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013", "SP015"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 200 },      // 200 lux
                    { "SP006", 4.0 }       // 4.0m height
                }
            };

            // Retail
            _roomProfiles["Retail"] = new RoomParameterProfile
            {
                RoomType = "Retail",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP017", "SP019"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP016"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 500 },      // 500 lux
                    { "SP006", 3.5 }       // 3.5m height
                }
            };

            // Restaurant/Dining
            _roomProfiles["Restaurant"] = new RoomParameterProfile
            {
                RoomType = "Restaurant",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP009", "SP017", "SP018"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP015"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 200 },      // 200 lux ambient
                    { "SP018", 10.0 },
                    { "SP006", 3.0 }
                }
            };

            // Storage
            _roomProfiles["Storage"] = new RoomParameterProfile
            {
                RoomType = "Storage",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP017"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP016"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 150 },      // 150 lux
                    { "SP006", 2.7 }
                }
            };

            // Toilet/Restroom
            _roomProfiles["Toilet"] = new RoomParameterProfile
            {
                RoomType = "Toilet",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP017"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP012", "SP013"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 200 },
                    { "SP006", 2.7 }
                }
            };

            // Server Room / Data Center
            _roomProfiles["Server Room"] = new RoomParameterProfile
            {
                RoomType = "Server Room",
                RequiredParameters = new List<string>
                {
                    "SP001", "SP002", "SP003", "SP006", "SP016", "SP017", "SP019"
                },
                OptionalParameters = new List<string>
                {
                    "SP011", "SP015"
                },
                DefaultValues = new Dictionary<string, object>
                {
                    { "SP017", 500 },
                    { "SP016", "2 Hour" },  // Fire rating
                    { "SP006", 3.5 }
                }
            };
        }

        private void InitializeStandardParameterSets()
        {
            // IBC (International Building Code) requirements
            _standardSets["IBC"] = new StandardParameterSet
            {
                Code = "IBC",
                RequiredParameters = new List<string>
                {
                    "SP009",  // Occupancy count
                    "SP010",  // Occupancy type
                    "SP016",  // Fire rating
                    "SP374",  // Fire compartment
                    "SP375",  // Exit distance
                    "SP376",  // Exit count
                    "SP379"   // Accessibility
                },
                ParameterRules = new Dictionary<string, ParameterRule>
                {
                    { "SP375", new ParameterRule { MaxValue = 60.0, Unit = "meters", Description = "Max travel distance" } },
                    { "SP376", new ParameterRule { MinValue = 2.0, Description = "Min 2 exits for assembly" } }
                }
            };

            // ASHRAE requirements
            _standardSets["ASHRAE"] = new StandardParameterSet
            {
                Code = "ASHRAE",
                RequiredParameters = new List<string>
                {
                    "SP018",  // Ventilation rate
                    "SP019",  // Cooling load
                    "SP020",  // Heating load
                    "SP304",  // Space name
                    "SP308",  // Occupancy
                    "SP311",  // Supply airflow
                    "SP315"   // Design temperature
                },
                ParameterRules = new Dictionary<string, ParameterRule>
                {
                    { "SP018", new ParameterRule { MinValue = 7.5, Unit = "L/s/person", Description = "Min ventilation" } }
                }
            };

            // KEBS (Kenya) requirements
            _standardSets["KEBS"] = new StandardParameterSet
            {
                Code = "KEBS",
                RequiredParameters = new List<string>
                {
                    "SP009",  // Occupancy
                    "SP016",  // Fire rating
                    "SP017",  // Lighting level
                    "SP018",  // Ventilation
                    "SP374",  // Fire compartment
                    "SP379"   // Accessibility
                },
                ParameterRules = new Dictionary<string, ParameterRule>
                {
                    { "SP006", new ParameterRule { MinValue = 2.4, Unit = "meters", Description = "Min room height (tropical)" } }
                }
            };

            // SANS (South Africa) requirements
            _standardSets["SANS"] = new StandardParameterSet
            {
                Code = "SANS",
                RequiredParameters = new List<string>
                {
                    "SP009",  // Occupancy
                    "SP010",  // Occupancy type
                    "SP016",  // Fire rating
                    "SP017",  // Lighting level
                    "SP375",  // Exit distance
                    "SP379"   // Accessibility
                }
            };

            // EAC (East African Community) requirements
            _standardSets["EAC"] = new StandardParameterSet
            {
                Code = "EAC",
                RequiredParameters = new List<string>
                {
                    "SP009",  // Occupancy
                    "SP016",  // Fire rating
                    "SP017",  // Lighting level
                    "SP018",  // Ventilation (critical in tropics)
                    "SP374"   // Fire compartment
                }
            };

            // British Standards
            _standardSets["BS"] = new StandardParameterSet
            {
                Code = "BS",
                RequiredParameters = new List<string>
                {
                    "SP009",  // Occupancy
                    "SP016",  // Fire rating
                    "SP017",  // Lighting level
                    "SP018",  // Ventilation
                    "SP015",  // Acoustic rating
                    "SP374"   // Fire compartment
                },
                ParameterRules = new Dictionary<string, ParameterRule>
                {
                    { "SP097", new ParameterRule { MinValue = 1000, Unit = "mm", Description = "Min stair width (BS 9999)" } }
                }
            };
        }

        private void InitializeParameterCorrelations()
        {
            // Room area and occupancy correlation
            AddCorrelation("SP003", "SP009", CorrelationType.Linear, 0.1, "Area determines occupancy");

            // Room area and cooling load
            AddCorrelation("SP003", "SP019", CorrelationType.Linear, 100, "Area to cooling load ~100W/m²");

            // Room area and ventilation
            AddCorrelation("SP003", "SP018", CorrelationType.Indirect, 1, "Area influences ventilation via occupancy");

            // Occupancy and ventilation rate
            AddCorrelation("SP009", "SP018", CorrelationType.Linear, 10, "Ventilation 10 L/s per person");

            // Window width and height correlation
            AddCorrelation("SP037", "SP038", CorrelationType.Ratio, 0.8, "Window W:H ratio typically 0.8");

            // Door width and accessibility
            AddCorrelation("SP022", "SP035", CorrelationType.Threshold, 900, "Width >= 900mm for accessibility");

            // Wall height and area
            AddCorrelation("SP054", "SP055", CorrelationType.Product, 1, "Area = Length * Height");

            // Duct width and airflow
            AddCorrelation("SP125", "SP130", CorrelationType.Quadratic, 0.5, "Airflow proportional to area");

            // Pipe diameter and flow
            AddCorrelation("SP113", "SP118", CorrelationType.Quadratic, 0.25, "Flow proportional to diameter²");
        }

        private void AddCorrelation(string param1, string param2, CorrelationType type, double factor, string description)
        {
            if (!_correlations.ContainsKey(param1))
                _correlations[param1] = new List<ParameterCorrelation>();

            _correlations[param1].Add(new ParameterCorrelation
            {
                SourceParameter = param1,
                TargetParameter = param2,
                Type = type,
                Factor = factor,
                Description = description
            });
        }

        #endregion

        #region Private Methods - Suggestions

        private ParameterValueSuggestion GetPatternBasedSuggestion(string parameterId, ParameterContext context)
        {
            if (!_learnedPatterns.TryGetValue(parameterId, out var pattern))
                return null;

            // Check if we have context-specific pattern
            if (!string.IsNullOrEmpty(context.RoomType))
            {
                var contextKey = $"{parameterId}|{context.RoomType}";
                if (pattern.ContextMostCommon.TryGetValue(contextKey, out var contextValue))
                {
                    return new ParameterValueSuggestion
                    {
                        ParameterId = parameterId,
                        Value = contextValue,
                        Confidence = 0.85,
                        Source = "Learned Pattern (Context)",
                        Reason = $"Most common value for {context.RoomType}"
                    };
                }
            }

            // Use general most common value
            if (pattern.MostCommonValue != null)
            {
                return new ParameterValueSuggestion
                {
                    ParameterId = parameterId,
                    Value = pattern.MostCommonValue,
                    Confidence = 0.7,
                    Source = "Learned Pattern",
                    Reason = "Most frequently used value in project"
                };
            }

            return null;
        }

        private ParameterValueSuggestion GetRoomBasedSuggestion(string parameterId, ParameterContext context)
        {
            if (string.IsNullOrEmpty(context.RoomType))
                return null;

            if (!_roomProfiles.TryGetValue(context.RoomType, out var profile))
            {
                // Try to find partial match
                profile = _roomProfiles.Values.FirstOrDefault(p =>
                    context.RoomType.IndexOf(p.RoomType, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (profile == null) return null;

            if (profile.DefaultValues.TryGetValue(parameterId, out var defaultValue))
            {
                return new ParameterValueSuggestion
                {
                    ParameterId = parameterId,
                    Value = defaultValue,
                    Confidence = 0.9,
                    Source = "Room Profile",
                    Reason = $"Standard value for {profile.RoomType}"
                };
            }

            return null;
        }

        private List<ParameterValueSuggestion> GetStandardBasedSuggestions(string parameterId, ParameterContext context)
        {
            var suggestions = new List<ParameterValueSuggestion>();

            // Check applicable standards
            var codes = new List<string> { context.BuildingCode };
            if (string.IsNullOrEmpty(context.BuildingCode))
            {
                codes = new List<string> { "IBC", "ASHRAE" }; // Default to international
            }

            foreach (var code in codes.Where(c => !string.IsNullOrEmpty(c)))
            {
                if (_standardSets.TryGetValue(code, out var standardSet))
                {
                    if (standardSet.ParameterRules.TryGetValue(parameterId, out var rule))
                    {
                        var suggestion = new ParameterValueSuggestion
                        {
                            ParameterId = parameterId,
                            Source = $"Code Compliance ({code})",
                            Confidence = 0.95
                        };

                        if (rule.MinValue.HasValue && rule.MaxValue.HasValue)
                        {
                            suggestion.Value = (rule.MinValue.Value + rule.MaxValue.Value) / 2;
                            suggestion.Reason = $"{code} requires {rule.MinValue}-{rule.MaxValue} {rule.Unit}";
                        }
                        else if (rule.MinValue.HasValue)
                        {
                            suggestion.Value = rule.MinValue.Value;
                            suggestion.Reason = $"{code} requires minimum {rule.MinValue} {rule.Unit}";
                        }
                        else if (rule.MaxValue.HasValue)
                        {
                            suggestion.Value = rule.MaxValue.Value;
                            suggestion.Reason = $"{code} requires maximum {rule.MaxValue} {rule.Unit}";
                        }

                        if (suggestion.Value != null)
                        {
                            suggestions.Add(suggestion);
                        }
                    }
                }
            }

            return suggestions;
        }

        private ParameterValueSuggestion GetCorrelationBasedSuggestion(string parameterId, ParameterContext context)
        {
            // Find correlations where this parameter is the target
            var relevantCorrelations = _correlations.Values
                .SelectMany(c => c)
                .Where(c => c.TargetParameter == parameterId)
                .ToList();

            foreach (var correlation in relevantCorrelations)
            {
                if (context.ExistingValues.TryGetValue(correlation.SourceParameter, out var sourceValue))
                {
                    var predictedValue = CalculateCorrelatedValue(sourceValue, correlation);
                    if (predictedValue != null)
                    {
                        return new ParameterValueSuggestion
                        {
                            ParameterId = parameterId,
                            Value = predictedValue,
                            Confidence = 0.75,
                            Source = "Correlation",
                            Reason = correlation.Description
                        };
                    }
                }
            }

            return null;
        }

        private ParameterValueSuggestion GetUsageBasedSuggestion(string parameterId, ParameterContext context)
        {
            if (!_usageStats.TryGetValue(parameterId, out var stats))
                return null;

            // For numeric parameters, suggest median or mean
            if (stats.NumericValues.Count > 5)
            {
                var sortedValues = stats.NumericValues.OrderBy(v => v).ToList();
                var median = sortedValues[sortedValues.Count / 2];

                return new ParameterValueSuggestion
                {
                    ParameterId = parameterId,
                    Value = Math.Round(median, 2),
                    Confidence = 0.6,
                    Source = "Usage Statistics",
                    Reason = $"Median value from {stats.UsageCount} usages"
                };
            }

            // For categorical, suggest most common
            if (stats.ValueDistribution.Count > 0)
            {
                var mostCommon = stats.ValueDistribution
                    .OrderByDescending(kvp => kvp.Value)
                    .First();

                return new ParameterValueSuggestion
                {
                    ParameterId = parameterId,
                    Value = mostCommon.Key,
                    Confidence = (double)mostCommon.Value / stats.UsageCount,
                    Source = "Usage Statistics",
                    Reason = $"Used {mostCommon.Value} times ({(double)mostCommon.Value / stats.UsageCount * 100:F0}%)"
                };
            }

            return null;
        }

        private object CalculateCorrelatedValue(object sourceValue, ParameterCorrelation correlation)
        {
            if (!double.TryParse(sourceValue?.ToString(), out var source))
                return null;

            switch (correlation.Type)
            {
                case CorrelationType.Linear:
                    return source * correlation.Factor;

                case CorrelationType.Quadratic:
                    return source * source * correlation.Factor;

                case CorrelationType.Ratio:
                    return source * correlation.Factor;

                case CorrelationType.Product:
                    return source * correlation.Factor;

                case CorrelationType.Threshold:
                    return source >= correlation.Factor;

                default:
                    return null;
            }
        }

        #endregion

        #region Private Methods - Anomaly Detection

        private ParameterAnomaly CheckValueAgainstPattern(string parameterId, object value, ParameterPattern pattern, ParameterContext context)
        {
            if (value == null) return null;

            // Check numeric range
            if (double.TryParse(value.ToString(), out var numValue))
            {
                if (pattern.MinValue.HasValue && numValue < pattern.MinValue.Value * 0.5)
                {
                    return new ParameterAnomaly
                    {
                        ParameterId = parameterId,
                        CurrentValue = value,
                        ExpectedRange = $"{pattern.MinValue:F2} - {pattern.MaxValue:F2}",
                        Severity = AnomalySeverity.Warning,
                        Message = $"Value {numValue:F2} is significantly below typical range"
                    };
                }

                if (pattern.MaxValue.HasValue && numValue > pattern.MaxValue.Value * 2)
                {
                    return new ParameterAnomaly
                    {
                        ParameterId = parameterId,
                        CurrentValue = value,
                        ExpectedRange = $"{pattern.MinValue:F2} - {pattern.MaxValue:F2}",
                        Severity = AnomalySeverity.Warning,
                        Message = $"Value {numValue:F2} is significantly above typical range"
                    };
                }
            }

            return null;
        }

        private ParameterAnomaly CheckValueAgainstRoomProfile(string parameterId, object value, RoomParameterProfile profile)
        {
            if (!profile.DefaultValues.TryGetValue(parameterId, out var expectedValue))
                return null;

            if (double.TryParse(value?.ToString(), out var actualNum) &&
                double.TryParse(expectedValue.ToString(), out var expectedNum))
            {
                var deviation = Math.Abs(actualNum - expectedNum) / expectedNum;
                if (deviation > 0.5) // More than 50% deviation
                {
                    return new ParameterAnomaly
                    {
                        ParameterId = parameterId,
                        CurrentValue = value,
                        ExpectedValue = expectedValue,
                        Severity = deviation > 1.0 ? AnomalySeverity.Error : AnomalySeverity.Warning,
                        Message = $"Value deviates {deviation * 100:F0}% from expected for {profile.RoomType}"
                    };
                }
            }

            return null;
        }

        private ParameterAnomaly CheckCorrelationAnomalies(string parameterId, object value, Dictionary<string, object> allValues)
        {
            if (!_correlations.TryGetValue(parameterId, out var correlations))
                return null;

            foreach (var correlation in correlations)
            {
                if (!allValues.TryGetValue(correlation.TargetParameter, out var targetValue))
                    continue;

                var expected = CalculateCorrelatedValue(value, correlation);
                if (expected == null) continue;

                if (double.TryParse(targetValue?.ToString(), out var actualTarget) &&
                    double.TryParse(expected.ToString(), out var expectedTarget))
                {
                    var deviation = Math.Abs(actualTarget - expectedTarget) / expectedTarget;
                    if (deviation > 0.5)
                    {
                        return new ParameterAnomaly
                        {
                            ParameterId = correlation.TargetParameter,
                            CurrentValue = targetValue,
                            ExpectedValue = expected,
                            Severity = AnomalySeverity.Warning,
                            Message = $"Value inconsistent with {parameterId} - {correlation.Description}"
                        };
                    }
                }
            }

            return null;
        }

        #endregion

        #region Private Methods - Helpers

        private List<ParameterRecommendation> GetCategorySpecificParameters(string category)
        {
            var recommendations = new List<ParameterRecommendation>();

            // Category-specific core parameters
            switch (category.ToLowerInvariant())
            {
                case "rooms":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP001", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP002", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP003", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP006", Priority = RecommendationPriority.Required }
                    });
                    break;

                case "doors":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP021", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP022", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP023", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP027", Priority = RecommendationPriority.Recommended }
                    });
                    break;

                case "windows":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP036", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP037", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP038", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP039", Priority = RecommendationPriority.Required }
                    });
                    break;

                case "walls":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP052", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP055", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP057", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP059", Priority = RecommendationPriority.Recommended }
                    });
                    break;

                case "ducts":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP123", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP124", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP130", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP131", Priority = RecommendationPriority.Recommended }
                    });
                    break;

                case "pipes":
                    recommendations.AddRange(new[]
                    {
                        new ParameterRecommendation { ParameterId = "SP111", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP112", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP113", Priority = RecommendationPriority.Required },
                        new ParameterRecommendation { ParameterId = "SP118", Priority = RecommendationPriority.Recommended }
                    });
                    break;
            }

            return recommendations;
        }

        private void UpdateCorrelation(string param1, string param2, object value1, object value2)
        {
            if (!double.TryParse(value1?.ToString(), out var v1) ||
                !double.TryParse(value2?.ToString(), out var v2))
                return;

            // Update correlation statistics (simplified)
            var key = $"{param1}|{param2}";
            if (!_correlations.ContainsKey(key))
            {
                _correlations[key] = new List<ParameterCorrelation>
                {
                    new ParameterCorrelation
                    {
                        SourceParameter = param1,
                        TargetParameter = param2,
                        Type = CorrelationType.Linear,
                        Factor = v2 / v1
                    }
                };
            }
        }

        private void UpdatePatternsFromStats()
        {
            foreach (var stat in _usageStats.Values)
            {
                var pattern = new ParameterPattern
                {
                    ParameterId = stat.ParameterId
                };

                // Set most common value
                if (stat.ValueDistribution.Count > 0)
                {
                    pattern.MostCommonValue = stat.ValueDistribution
                        .OrderByDescending(kvp => kvp.Value)
                        .First().Key;
                }

                // Set numeric range
                if (stat.NumericValues.Count > 0)
                {
                    pattern.MinValue = stat.NumericValues.Min();
                    pattern.MaxValue = stat.NumericValues.Max();
                    pattern.MedianValue = stat.NumericValues.OrderBy(v => v).ElementAt(stat.NumericValues.Count / 2);
                }

                // Context-specific most common
                foreach (var contextGroup in stat.ContextValues.GroupBy(kvp => kvp.Key.Split('|').Last()))
                {
                    var allValues = contextGroup.SelectMany(g => g.Value).ToList();
                    var mostCommon = allValues.GroupBy(v => v?.ToString())
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (mostCommon != null)
                    {
                        pattern.ContextMostCommon[contextGroup.Key] = mostCommon.Key;
                    }
                }

                _learnedPatterns[stat.ParameterId] = pattern;
            }
        }

        #endregion
    }

    #region Supporting Types

    public class ParameterContext
    {
        public string RoomType { get; set; }
        public string BuildingCode { get; set; }
        public string ProjectType { get; set; }
        public string Region { get; set; }
        public Dictionary<string, object> ExistingValues { get; set; } = new Dictionary<string, object>();
    }

    public class ParameterValueSuggestion
    {
        public string ParameterId { get; set; }
        public object Value { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public string Reason { get; set; }
    }

    public class ParameterPrediction
    {
        public string ParameterId { get; set; }
        public object PredictedValue { get; set; }
        public double Confidence { get; set; }
        public List<string> InputFeatures { get; set; } = new List<string>();
    }

    public class ParameterRecommendation
    {
        public string ParameterId { get; set; }
        public string Reason { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Source { get; set; }
    }

    public enum RecommendationPriority
    {
        Optional = 0,
        Recommended = 1,
        Required = 2
    }

    public class CompletenessAnalysis
    {
        public string Category { get; set; }
        public int RequiredCount { get; set; }
        public int RequiredFilledCount { get; set; }
        public int RecommendedCount { get; set; }
        public int RecommendedFilledCount { get; set; }
        public double CompletenessScore { get; set; }
        public List<string> MissingRequired { get; set; } = new List<string>();
        public List<string> MissingRecommended { get; set; } = new List<string>();
    }

    public class ParameterAnomaly
    {
        public string ParameterId { get; set; }
        public object CurrentValue { get; set; }
        public object ExpectedValue { get; set; }
        public string ExpectedRange { get; set; }
        public AnomalySeverity Severity { get; set; }
        public string Message { get; set; }
    }

    public enum AnomalySeverity
    {
        Info,
        Warning,
        Error
    }

    public class RoomParameterProfile
    {
        public string RoomType { get; set; }
        public List<string> RequiredParameters { get; set; } = new List<string>();
        public List<string> OptionalParameters { get; set; } = new List<string>();
        public Dictionary<string, object> DefaultValues { get; set; } = new Dictionary<string, object>();
    }

    public class StandardParameterSet
    {
        public string Code { get; set; }
        public List<string> RequiredParameters { get; set; } = new List<string>();
        public Dictionary<string, ParameterRule> ParameterRules { get; set; } = new Dictionary<string, ParameterRule>();
    }

    public class ParameterRule
    {
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
    }

    public class ParameterPattern
    {
        public string ParameterId { get; set; }
        public object MostCommonValue { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? MedianValue { get; set; }
        public Dictionary<string, object> ContextMostCommon { get; set; } = new Dictionary<string, object>();
    }

    public class ParameterCorrelation
    {
        public string SourceParameter { get; set; }
        public string TargetParameter { get; set; }
        public CorrelationType Type { get; set; }
        public double Factor { get; set; }
        public string Description { get; set; }
    }

    public enum CorrelationType
    {
        Linear,
        Quadratic,
        Ratio,
        Product,
        Threshold,
        Indirect
    }

    public class ParameterUsageStats
    {
        public string ParameterId { get; set; }
        public int UsageCount { get; set; }
        public Dictionary<string, int> ValueDistribution { get; set; } = new Dictionary<string, int>();
        public List<double> NumericValues { get; set; } = new List<double>();
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public Dictionary<string, List<object>> ContextValues { get; set; } = new Dictionary<string, List<object>>();
    }

    /// <summary>
    /// Simple predictor for parameter values based on context and learned patterns.
    /// </summary>
    public class ParameterValuePredictor
    {
        public ParameterPrediction Predict(string parameterId, ParameterContext context)
        {
            // Simplified prediction - would use ML model in production
            return new ParameterPrediction
            {
                ParameterId = parameterId,
                PredictedValue = null,
                Confidence = 0
            };
        }
    }

    #endregion
}
