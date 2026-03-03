// StingBIM.AI.Intelligence.Recommendations.RecommendationEngine
// Intelligent recommendation engine for BIM design suggestions
// Master Proposal Reference: Part 2.2 Strategy 5 - Recommendation Intelligence

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Intelligence.Recommendations
{
    #region Recommendation Types

    /// <summary>
    /// A design recommendation with supporting evidence.
    /// </summary>
    public class Recommendation
    {
        public string RecommendationId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public RecommendationType Type { get; set; }
        public RecommendationCategory Category { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public float Impact { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();
        public List<string> Standards { get; set; } = new List<string>();
        public List<RecommendedAction> Actions { get; set; } = new List<RecommendedAction>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string SourceAgent { get; set; }

        public float Score => Confidence * Impact * GetPriorityMultiplier();

        private float GetPriorityMultiplier()
        {
            return Priority switch
            {
                RecommendationPriority.Critical => 2.0f,
                RecommendationPriority.High => 1.5f,
                RecommendationPriority.Medium => 1.0f,
                RecommendationPriority.Low => 0.7f,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// An action to implement a recommendation.
    /// </summary>
    public class RecommendedAction
    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Description { get; set; }
        public ActionDifficulty Difficulty { get; set; }
        public string TargetElementId { get; set; }
        public string TargetCategory { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public bool IsAutomatable { get; set; }
    }

    public enum RecommendationType
    {
        Improvement,        // Make something better
        Correction,         // Fix a problem
        Addition,           // Add missing element
        Removal,            // Remove problematic element
        Modification,       // Change existing element
        Alternative,        // Suggest different approach
        Optimization        // Optimize performance
    }

    public enum RecommendationCategory
    {
        Compliance,         // Code/standard compliance
        Performance,        // Energy, acoustic, thermal
        Accessibility,      // Universal design
        Safety,             // Fire, structural safety
        Efficiency,         // Space, cost efficiency
        Quality,            // Design quality
        Sustainability,     // Environmental
        Coordination        // Cross-discipline coordination
    }

    public enum RecommendationPriority
    {
        Critical,           // Must address immediately
        High,               // Should address soon
        Medium,             // Address when convenient
        Low                 // Nice to have
    }

    public enum ActionDifficulty
    {
        Trivial,            // Single click
        Easy,               // Few clicks
        Moderate,           // Some effort
        Complex,            // Significant work
        Major               // Major redesign
    }

    #endregion

    #region Recommendation Generators

    /// <summary>
    /// Base class for recommendation generators.
    /// </summary>
    public abstract class RecommendationGenerator
    {
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public abstract string Name { get; }
        public abstract RecommendationCategory[] Categories { get; }

        public abstract Task<List<Recommendation>> GenerateAsync(
            RecommendationContext context,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Generates compliance-related recommendations.
    /// </summary>
    public class ComplianceRecommendationGenerator : RecommendationGenerator
    {
        public override string Name => "Compliance";
        public override RecommendationCategory[] Categories => new[] { RecommendationCategory.Compliance };

        private readonly Dictionary<string, List<ComplianceRule>> _rules;

        public ComplianceRecommendationGenerator()
        {
            _rules = InitializeRules();
        }

        public override async Task<List<Recommendation>> GenerateAsync(
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            var recommendations = new List<Recommendation>();

            foreach (var element in context.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_rules.TryGetValue(element.Category, out var rules))
                {
                    foreach (var rule in rules)
                    {
                        var violation = CheckRule(element, rule, context);
                        if (violation != null)
                        {
                            recommendations.Add(violation);
                        }
                    }
                }
            }

            return await Task.FromResult(recommendations);
        }

        private Recommendation CheckRule(ElementInfo element, ComplianceRule rule, RecommendationContext context)
        {
            if (!element.Parameters.TryGetValue(rule.ParameterName, out var value))
            {
                return null;
            }

            var numericValue = Convert.ToDouble(value);
            var isViolation = false;
            var actualVsRequired = "";

            switch (rule.Operator)
            {
                case ">=":
                    isViolation = numericValue < rule.RequiredValue;
                    actualVsRequired = $"{numericValue} < {rule.RequiredValue}";
                    break;
                case "<=":
                    isViolation = numericValue > rule.RequiredValue;
                    actualVsRequired = $"{numericValue} > {rule.RequiredValue}";
                    break;
                case "==":
                    isViolation = Math.Abs(numericValue - rule.RequiredValue) > 0.001;
                    actualVsRequired = $"{numericValue} != {rule.RequiredValue}";
                    break;
            }

            if (!isViolation) return null;

            return new Recommendation
            {
                Type = RecommendationType.Correction,
                Category = RecommendationCategory.Compliance,
                Priority = rule.IsCritical ? RecommendationPriority.Critical : RecommendationPriority.High,
                Title = $"{rule.Standard} Violation: {element.Category}",
                Description = rule.Description,
                Confidence = 1.0f,
                Impact = rule.IsCritical ? 1.0f : 0.8f,
                Evidence = new List<string>
                {
                    $"{rule.ParameterName}: {actualVsRequired} ({rule.Unit})",
                    $"Standard: {rule.Standard} - {rule.Clause}"
                },
                Standards = new List<string> { rule.Standard },
                Actions = new List<RecommendedAction>
                {
                    new RecommendedAction
                    {
                        Description = $"Update {rule.ParameterName} to meet {rule.Standard} requirement",
                        Difficulty = ActionDifficulty.Easy,
                        TargetElementId = element.ElementId,
                        Parameters = new Dictionary<string, object>
                        {
                            [rule.ParameterName] = rule.RequiredValue
                        },
                        IsAutomatable = true
                    }
                },
                SourceAgent = "ComplianceAgent"
            };
        }

        private Dictionary<string, List<ComplianceRule>> InitializeRules()
        {
            return new Dictionary<string, List<ComplianceRule>>
            {
                ["Doors"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Standard = "ADA",
                        Clause = "404.2.3",
                        ParameterName = "Width",
                        Operator = ">=",
                        RequiredValue = 815,
                        Unit = "mm",
                        Description = "Door clear width must be at least 815mm (32\") for wheelchair access",
                        IsCritical = true
                    },
                    new ComplianceRule
                    {
                        Standard = "IBC",
                        Clause = "1010.1.1",
                        ParameterName = "Height",
                        Operator = ">=",
                        RequiredValue = 2032,
                        Unit = "mm",
                        Description = "Door height must be at least 2032mm (80\")"
                    }
                },
                ["Corridors"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Standard = "IBC",
                        Clause = "1020.2",
                        ParameterName = "Width",
                        Operator = ">=",
                        RequiredValue = 1118,
                        Unit = "mm",
                        Description = "Corridor width must be at least 1118mm (44\") for egress"
                    }
                },
                ["Stairs"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Standard = "IBC",
                        Clause = "1011.5.2",
                        ParameterName = "RiserHeight",
                        Operator = "<=",
                        RequiredValue = 178,
                        Unit = "mm",
                        Description = "Stair riser height must not exceed 178mm (7\")"
                    },
                    new ComplianceRule
                    {
                        Standard = "IBC",
                        Clause = "1011.5.2",
                        ParameterName = "TreadDepth",
                        Operator = ">=",
                        RequiredValue = 279,
                        Unit = "mm",
                        Description = "Stair tread depth must be at least 279mm (11\")"
                    }
                },
                ["Rooms"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Standard = "IBC",
                        Clause = "1208.2",
                        ParameterName = "CeilingHeight",
                        Operator = ">=",
                        RequiredValue = 2134,
                        Unit = "mm",
                        Description = "Habitable room ceiling height must be at least 2134mm (7'-0\")"
                    }
                }
            };
        }

        private class ComplianceRule
        {
            public string Standard { get; set; }
            public string Clause { get; set; }
            public string ParameterName { get; set; }
            public string Operator { get; set; }
            public double RequiredValue { get; set; }
            public string Unit { get; set; }
            public string Description { get; set; }
            public bool IsCritical { get; set; }
        }
    }

    /// <summary>
    /// Generates spatial optimization recommendations.
    /// </summary>
    public class SpatialRecommendationGenerator : RecommendationGenerator
    {
        public override string Name => "Spatial";
        public override RecommendationCategory[] Categories => new[] { RecommendationCategory.Efficiency, RecommendationCategory.Quality };

        private readonly Dictionary<string, RoomSpatialRequirements> _roomRequirements;

        public SpatialRecommendationGenerator()
        {
            _roomRequirements = InitializeRequirements();
        }

        public override async Task<List<Recommendation>> GenerateAsync(
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            var recommendations = new List<Recommendation>();

            // Check room sizes
            foreach (var room in context.Elements.Where(e => e.Category == "Rooms"))
            {
                if (_roomRequirements.TryGetValue(room.RoomType ?? "Generic", out var reqs))
                {
                    if (room.Parameters.TryGetValue("Area", out var areaObj))
                    {
                        var area = Convert.ToDouble(areaObj);

                        if (area < reqs.MinArea)
                        {
                            recommendations.Add(CreateSizeRecommendation(room, reqs, area, "below minimum"));
                        }
                        else if (area > reqs.MaxArea * 1.5)
                        {
                            recommendations.Add(CreateSizeRecommendation(room, reqs, area, "excessively large"));
                        }
                    }
                }
            }

            // Check adjacencies
            var adjacencyIssues = CheckAdjacencies(context);
            recommendations.AddRange(adjacencyIssues);

            // Check circulation efficiency
            var circulationIssues = AnalyzeCirculation(context);
            recommendations.AddRange(circulationIssues);

            return await Task.FromResult(recommendations);
        }

        private Recommendation CreateSizeRecommendation(ElementInfo room, RoomSpatialRequirements reqs, double actualArea, string issue)
        {
            return new Recommendation
            {
                Type = RecommendationType.Modification,
                Category = RecommendationCategory.Efficiency,
                Priority = RecommendationPriority.Medium,
                Title = $"Room Size Issue: {room.Name}",
                Description = $"Room area is {issue}. Recommended: {reqs.MinArea}-{reqs.MaxArea} m\u00b2, Actual: {actualArea:F1} m\u00b2",
                Confidence = 0.9f,
                Impact = 0.7f,
                Evidence = new List<string>
                {
                    $"Room type: {room.RoomType}",
                    $"Current area: {actualArea:F1} m\u00b2",
                    $"Recommended range: {reqs.MinArea}-{reqs.MaxArea} m\u00b2"
                },
                Actions = new List<RecommendedAction>
                {
                    new RecommendedAction
                    {
                        Description = $"Adjust room boundaries to achieve target area",
                        Difficulty = ActionDifficulty.Moderate,
                        TargetElementId = room.ElementId,
                        IsAutomatable = false
                    }
                },
                SourceAgent = "SpatialAgent"
            };
        }

        private List<Recommendation> CheckAdjacencies(RecommendationContext context)
        {
            var recommendations = new List<Recommendation>();
            var rooms = context.Elements.Where(e => e.Category == "Rooms").ToList();

            // Find bathrooms not adjacent to plumbing stack
            var bathrooms = rooms.Where(r => r.RoomType == "Bathroom" || r.RoomType == "Toilet").ToList();
            var kitchens = rooms.Where(r => r.RoomType == "Kitchen").ToList();

            foreach (var bathroom in bathrooms)
            {
                var hasAdjacentWetRoom = rooms.Any(r =>
                    r.ElementId != bathroom.ElementId &&
                    (r.RoomType == "Bathroom" || r.RoomType == "Kitchen" || r.RoomType == "Laundry") &&
                    AreAdjacent(bathroom, r));

                if (!hasAdjacentWetRoom && kitchens.Any())
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = RecommendationType.Alternative,
                        Category = RecommendationCategory.Efficiency,
                        Priority = RecommendationPriority.Low,
                        Title = $"Plumbing Efficiency: {bathroom.Name}",
                        Description = "Consider locating wet rooms (bathroom, kitchen, laundry) adjacent to share plumbing risers",
                        Confidence = 0.7f,
                        Impact = 0.5f,
                        Evidence = new List<string>
                        {
                            "Shared plumbing reduces installation cost",
                            "Shorter pipe runs improve water pressure",
                            "Easier maintenance access"
                        },
                        SourceAgent = "SpatialAgent"
                    });
                }
            }

            return recommendations;
        }

        private List<Recommendation> AnalyzeCirculation(RecommendationContext context)
        {
            var recommendations = new List<Recommendation>();

            // Calculate circulation percentage
            var totalArea = context.Elements
                .Where(e => e.Category == "Rooms")
                .Sum(e => e.Parameters.TryGetValue("Area", out var a) ? Convert.ToDouble(a) : 0);

            var circulationArea = context.Elements
                .Where(e => e.Category == "Rooms" &&
                           (e.RoomType == "Corridor" || e.RoomType == "Hall" || e.RoomType == "Lobby"))
                .Sum(e => e.Parameters.TryGetValue("Area", out var a) ? Convert.ToDouble(a) : 0);

            if (totalArea > 0)
            {
                var circulationPercent = circulationArea / totalArea * 100;

                if (circulationPercent > 25)
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = RecommendationType.Optimization,
                        Category = RecommendationCategory.Efficiency,
                        Priority = RecommendationPriority.Medium,
                        Title = "High Circulation Area",
                        Description = $"Circulation space is {circulationPercent:F1}% of total area. Consider reducing to improve space efficiency.",
                        Confidence = 0.85f,
                        Impact = 0.6f,
                        Evidence = new List<string>
                        {
                            $"Circulation area: {circulationArea:F1} m\u00b2",
                            $"Total area: {totalArea:F1} m\u00b2",
                            "Typical efficient range: 15-20%"
                        },
                        SourceAgent = "SpatialAgent"
                    });
                }
            }

            return recommendations;
        }

        private bool AreAdjacent(ElementInfo room1, ElementInfo room2)
        {
            // Simplified adjacency check based on bounding boxes
            if (room1.BoundingBox == null || room2.BoundingBox == null) return false;

            var b1 = room1.BoundingBox;
            var b2 = room2.BoundingBox;

            // Check if they share a wall (within tolerance)
            const double tolerance = 0.5; // 500mm

            var xOverlap = b1.MaxX >= b2.MinX - tolerance && b1.MinX <= b2.MaxX + tolerance;
            var yOverlap = b1.MaxY >= b2.MinY - tolerance && b1.MinY <= b2.MaxY + tolerance;

            var xAdjacent = Math.Abs(b1.MaxX - b2.MinX) < tolerance || Math.Abs(b1.MinX - b2.MaxX) < tolerance;
            var yAdjacent = Math.Abs(b1.MaxY - b2.MinY) < tolerance || Math.Abs(b1.MinY - b2.MaxY) < tolerance;

            return (xAdjacent && yOverlap) || (yAdjacent && xOverlap);
        }

        private Dictionary<string, RoomSpatialRequirements> InitializeRequirements()
        {
            return new Dictionary<string, RoomSpatialRequirements>
            {
                ["Bedroom"] = new RoomSpatialRequirements { MinArea = 9, MaxArea = 25, IdealAspectRatio = 1.3 },
                ["Living Room"] = new RoomSpatialRequirements { MinArea = 15, MaxArea = 40, IdealAspectRatio = 1.5 },
                ["Kitchen"] = new RoomSpatialRequirements { MinArea = 8, MaxArea = 25, IdealAspectRatio = 1.2 },
                ["Bathroom"] = new RoomSpatialRequirements { MinArea = 4, MaxArea = 12, IdealAspectRatio = 1.5 },
                ["Office"] = new RoomSpatialRequirements { MinArea = 10, MaxArea = 20, IdealAspectRatio = 1.3 },
                ["Corridor"] = new RoomSpatialRequirements { MinArea = 2, MaxArea = 50, IdealAspectRatio = 5.0 }
            };
        }

        private class RoomSpatialRequirements
        {
            public double MinArea { get; set; }
            public double MaxArea { get; set; }
            public double IdealAspectRatio { get; set; }
        }
    }

    /// <summary>
    /// Generates sustainability recommendations.
    /// </summary>
    public class SustainabilityRecommendationGenerator : RecommendationGenerator
    {
        public override string Name => "Sustainability";
        public override RecommendationCategory[] Categories => new[] { RecommendationCategory.Sustainability, RecommendationCategory.Performance };

        public override async Task<List<Recommendation>> GenerateAsync(
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            var recommendations = new List<Recommendation>();

            // Analyze glazing ratio
            var glazingRecommendation = AnalyzeGlazingRatio(context);
            if (glazingRecommendation != null)
                recommendations.Add(glazingRecommendation);

            // Check thermal performance
            var thermalRecommendations = AnalyzeThermalPerformance(context);
            recommendations.AddRange(thermalRecommendations);

            // Daylight analysis
            var daylightRecommendations = AnalyzeDaylight(context);
            recommendations.AddRange(daylightRecommendations);

            return await Task.FromResult(recommendations);
        }

        private Recommendation AnalyzeGlazingRatio(RecommendationContext context)
        {
            var windows = context.Elements.Where(e => e.Category == "Windows").ToList();
            var walls = context.Elements.Where(e => e.Category == "Walls" && IsExteriorWall(e)).ToList();

            if (!walls.Any()) return null;

            var windowArea = windows.Sum(w => GetElementArea(w));
            var wallArea = walls.Sum(w => GetElementArea(w));

            if (wallArea <= 0) return null;

            var glazingRatio = windowArea / wallArea * 100;

            if (glazingRatio > 40)
            {
                return new Recommendation
                {
                    Type = RecommendationType.Optimization,
                    Category = RecommendationCategory.Sustainability,
                    Priority = RecommendationPriority.Medium,
                    Title = "High Glazing Ratio",
                    Description = $"Window-to-wall ratio is {glazingRatio:F1}%. Consider reducing for better thermal performance.",
                    Confidence = 0.85f,
                    Impact = 0.7f,
                    Evidence = new List<string>
                    {
                        $"Current glazing ratio: {glazingRatio:F1}%",
                        "Recommended range: 25-40%",
                        "High glazing increases heating/cooling loads"
                    },
                    Standards = new List<string> { "ASHRAE 90.1", "LEED" },
                    SourceAgent = "SustainabilityAgent"
                };
            }

            return null;
        }

        private List<Recommendation> AnalyzeThermalPerformance(RecommendationContext context)
        {
            var recommendations = new List<Recommendation>();

            // Check wall U-values
            foreach (var wall in context.Elements.Where(e => e.Category == "Walls" && IsExteriorWall(e)))
            {
                if (wall.Parameters.TryGetValue("ThermalTransmittance", out var uValueObj))
                {
                    var uValue = Convert.ToDouble(uValueObj);

                    // ASHRAE 90.1 Climate Zone 4 requirement ~0.45 W/m\u00b2K
                    if (uValue > 0.45)
                    {
                        recommendations.Add(new Recommendation
                        {
                            Type = RecommendationType.Improvement,
                            Category = RecommendationCategory.Performance,
                            Priority = RecommendationPriority.Medium,
                            Title = $"Improve Wall Insulation: {wall.Name}",
                            Description = $"Wall U-value of {uValue:F2} W/m\u00b2K exceeds recommended maximum",
                            Confidence = 0.9f,
                            Impact = 0.6f,
                            Evidence = new List<string>
                            {
                                $"Current U-value: {uValue:F2} W/m\u00b2K",
                                "Target U-value: \u22640.45 W/m\u00b2K",
                                "Better insulation reduces energy consumption"
                            },
                            Standards = new List<string> { "ASHRAE 90.1" },
                            Actions = new List<RecommendedAction>
                            {
                                new RecommendedAction
                                {
                                    Description = "Increase insulation thickness or use higher-performance materials",
                                    Difficulty = ActionDifficulty.Moderate,
                                    TargetElementId = wall.ElementId,
                                    IsAutomatable = false
                                }
                            },
                            SourceAgent = "SustainabilityAgent"
                        });
                    }
                }
            }

            return recommendations;
        }

        private List<Recommendation> AnalyzeDaylight(RecommendationContext context)
        {
            var recommendations = new List<Recommendation>();

            foreach (var room in context.Elements.Where(e => e.Category == "Rooms"))
            {
                // Check if room has windows
                var roomWindows = context.Elements
                    .Where(e => e.Category == "Windows" && IsWindowInRoom(e, room))
                    .ToList();

                if (!roomWindows.Any() && RequiresDaylight(room.RoomType))
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = RecommendationType.Addition,
                        Category = RecommendationCategory.Quality,
                        Priority = RecommendationPriority.High,
                        Title = $"No Natural Light: {room.Name}",
                        Description = "Habitable room lacks natural daylight. Consider adding windows or skylights.",
                        Confidence = 0.95f,
                        Impact = 0.8f,
                        Evidence = new List<string>
                        {
                            "Natural light improves occupant wellbeing",
                            "Building codes often require windows in habitable rooms",
                            "Reduces artificial lighting energy use"
                        },
                        Standards = new List<string> { "IBC", "LEED", "WELL" },
                        Actions = new List<RecommendedAction>
                        {
                            new RecommendedAction
                            {
                                Description = "Add window to exterior wall",
                                Difficulty = ActionDifficulty.Moderate,
                                TargetCategory = "Windows",
                                IsAutomatable = false
                            }
                        },
                        SourceAgent = "SustainabilityAgent"
                    });
                }
            }

            return recommendations;
        }

        private bool IsExteriorWall(ElementInfo wall)
        {
            if (wall.Parameters.TryGetValue("Function", out var func))
            {
                return func?.ToString()?.Contains("Exterior") ?? false;
            }
            return wall.Name?.Contains("Exterior") ?? false;
        }

        private double GetElementArea(ElementInfo element)
        {
            if (element.Parameters.TryGetValue("Area", out var area))
            {
                return Convert.ToDouble(area);
            }
            return 0;
        }

        private bool IsWindowInRoom(ElementInfo window, ElementInfo room)
        {
            // Simplified check - in production, use proper geometric intersection
            return window.Parameters.TryGetValue("Room", out var roomName) &&
                   roomName?.ToString() == room.Name;
        }

        private bool RequiresDaylight(string roomType)
        {
            var daylightRequired = new HashSet<string>
            {
                "Bedroom", "Living Room", "Office", "Study", "Kitchen", "Dining Room"
            };
            return daylightRequired.Contains(roomType ?? "");
        }
    }

    #endregion

    #region Main Engine

    /// <summary>
    /// Main recommendation engine orchestrating all generators.
    /// </summary>
    public class RecommendationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<RecommendationGenerator> _generators;
        private readonly RecommendationFilter _filter;
        private readonly RecommendationRanker _ranker;

        public RecommendationEngine()
        {
            _generators = new List<RecommendationGenerator>
            {
                new ComplianceRecommendationGenerator(),
                new SpatialRecommendationGenerator(),
                new SustainabilityRecommendationGenerator()
            };

            _filter = new RecommendationFilter();
            _ranker = new RecommendationRanker();
        }

        /// <summary>
        /// Registers a custom recommendation generator.
        /// </summary>
        public void RegisterGenerator(RecommendationGenerator generator)
        {
            _generators.Add(generator);
            Logger.Info($"Registered recommendation generator: {generator.Name}");
        }

        /// <summary>
        /// Generates recommendations for the given context.
        /// </summary>
        public async Task<RecommendationResult> GenerateRecommendationsAsync(
            RecommendationContext context,
            RecommendationOptions options = null,
            IProgress<RecommendationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new RecommendationOptions();
            var result = new RecommendationResult();

            Logger.Info($"Generating recommendations for {context.Elements.Count} elements");

            var allRecommendations = new List<Recommendation>();
            var generatorCount = _generators.Count;
            var completed = 0;

            foreach (var generator in _generators)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip if category is filtered out
                if (options.IncludeCategories != null &&
                    !generator.Categories.Any(c => options.IncludeCategories.Contains(c)))
                {
                    continue;
                }

                try
                {
                    var recommendations = await generator.GenerateAsync(context, cancellationToken);
                    allRecommendations.AddRange(recommendations);

                    Logger.Debug($"{generator.Name} generated {recommendations.Count} recommendations");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Generator {generator.Name} failed");
                }

                completed++;
                progress?.Report(new RecommendationProgress
                {
                    Phase = $"Running {generator.Name}",
                    PercentComplete = (float)completed / generatorCount
                });
            }

            // Filter recommendations
            var filtered = _filter.Filter(allRecommendations, options);

            // Rank recommendations
            var ranked = _ranker.Rank(filtered);

            // Apply limit
            result.Recommendations = ranked.Take(options.MaxRecommendations).ToList();
            result.TotalGenerated = allRecommendations.Count;
            result.TotalFiltered = allRecommendations.Count - filtered.Count;

            // Generate summary
            result.Summary = GenerateSummary(result.Recommendations);

            Logger.Info($"Generated {result.Recommendations.Count} recommendations (filtered {result.TotalFiltered})");

            return result;
        }

        /// <summary>
        /// Gets recommendations for a specific element.
        /// </summary>
        public async Task<List<Recommendation>> GetElementRecommendationsAsync(
            ElementInfo element,
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            // Create focused context with just this element
            var focusedContext = new RecommendationContext
            {
                Elements = new List<ElementInfo> { element },
                ProjectInfo = context.ProjectInfo,
                Standards = context.Standards
            };

            var result = await GenerateRecommendationsAsync(focusedContext, null, null, cancellationToken);
            return result.Recommendations;
        }

        private RecommendationSummary GenerateSummary(List<Recommendation> recommendations)
        {
            return new RecommendationSummary
            {
                TotalCount = recommendations.Count,
                CriticalCount = recommendations.Count(r => r.Priority == RecommendationPriority.Critical),
                HighCount = recommendations.Count(r => r.Priority == RecommendationPriority.High),
                ByCategory = recommendations
                    .GroupBy(r => r.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByType = recommendations
                    .GroupBy(r => r.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopIssues = recommendations
                    .Take(5)
                    .Select(r => r.Title)
                    .ToList()
            };
        }
    }

    /// <summary>
    /// Filters recommendations based on options.
    /// </summary>
    public class RecommendationFilter
    {
        public List<Recommendation> Filter(List<Recommendation> recommendations, RecommendationOptions options)
        {
            var filtered = recommendations.AsEnumerable();

            // Filter by minimum confidence
            if (options.MinConfidence > 0)
            {
                filtered = filtered.Where(r => r.Confidence >= options.MinConfidence);
            }

            // Filter by categories
            if (options.IncludeCategories != null && options.IncludeCategories.Any())
            {
                filtered = filtered.Where(r => options.IncludeCategories.Contains(r.Category));
            }

            // Filter by priorities
            if (options.MinPriority != null)
            {
                filtered = filtered.Where(r => r.Priority <= options.MinPriority);
            }

            return filtered.ToList();
        }
    }

    /// <summary>
    /// Ranks recommendations by importance.
    /// </summary>
    public class RecommendationRanker
    {
        public List<Recommendation> Rank(List<Recommendation> recommendations)
        {
            return recommendations
                .OrderByDescending(r => r.Priority == RecommendationPriority.Critical)
                .ThenByDescending(r => r.Score)
                .ThenByDescending(r => r.Confidence)
                .ToList();
        }
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Context for recommendation generation.
    /// </summary>
    public class RecommendationContext
    {
        public List<ElementInfo> Elements { get; set; } = new List<ElementInfo>();
        public ProjectInfo ProjectInfo { get; set; }
        public List<string> Standards { get; set; } = new List<string>();
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Information about an element for recommendations.
    /// </summary>
    public class ElementInfo
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string RoomType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public BoundingBoxInfo BoundingBox { get; set; }
    }

    public class BoundingBoxInfo
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    /// <summary>
    /// Project information for context.
    /// </summary>
    public class ProjectInfo
    {
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    /// <summary>
    /// Options for recommendation generation.
    /// </summary>
    public class RecommendationOptions
    {
        public float MinConfidence { get; set; } = 0.5f;
        public RecommendationPriority? MinPriority { get; set; } = null;
        public HashSet<RecommendationCategory> IncludeCategories { get; set; }
        public int MaxRecommendations { get; set; } = 50;
        public bool IncludeEvidence { get; set; } = true;
    }

    /// <summary>
    /// Result of recommendation generation.
    /// </summary>
    public class RecommendationResult
    {
        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
        public RecommendationSummary Summary { get; set; }
        public int TotalGenerated { get; set; }
        public int TotalFiltered { get; set; }
    }

    /// <summary>
    /// Summary of recommendations.
    /// </summary>
    public class RecommendationSummary
    {
        public int TotalCount { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public Dictionary<RecommendationCategory, int> ByCategory { get; set; }
        public Dictionary<RecommendationType, int> ByType { get; set; }
        public List<string> TopIssues { get; set; }
    }

    public class RecommendationProgress
    {
        public string Phase { get; set; }
        public float PercentComplete { get; set; }
    }

    #endregion
}
