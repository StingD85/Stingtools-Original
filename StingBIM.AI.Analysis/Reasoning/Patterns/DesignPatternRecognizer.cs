// StingBIM.AI.Reasoning.Patterns.DesignPatternRecognizer
// Architectural design pattern recognition and suggestion engine
// Master Proposal Reference: Part 2.2 Strategy 1 - Compound Learning (Pattern Recognition)

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Reasoning.Patterns
{
    /// <summary>
    /// Recognizes architectural design patterns and suggests optimal layouts.
    /// Learns from historical designs to identify best practices.
    /// </summary>
    public class DesignPatternRecognizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<ArchitecturalPattern> _patterns;
        private readonly Dictionary<string, PatternUsageStats> _usageStats;
        private readonly double _minConfidenceThreshold = 0.6;

        public DesignPatternRecognizer()
        {
            _patterns = new List<ArchitecturalPattern>();
            _usageStats = new Dictionary<string, PatternUsageStats>();

            InitializeBuiltInPatterns();
        }

        #region Public API

        /// <summary>
        /// Recognizes patterns in the current design.
        /// </summary>
        public List<PatternMatch> RecognizePatterns(DesignContext context)
        {
            Logger.Debug($"Analyzing design for patterns. Rooms: {context.Rooms.Count}");

            var matches = new List<PatternMatch>();

            foreach (var pattern in _patterns)
            {
                var match = TryMatchPattern(pattern, context);
                if (match != null && match.Confidence >= _minConfidenceThreshold)
                {
                    matches.Add(match);
                }
            }

            // Sort by relevance and confidence
            return matches
                .OrderByDescending(m => m.Confidence * m.Relevance)
                .ToList();
        }

        /// <summary>
        /// Suggests applicable patterns for the current design context.
        /// </summary>
        public List<PatternSuggestion> SuggestPatterns(DesignContext context, string projectType)
        {
            var suggestions = new List<PatternSuggestion>();

            // Get patterns suitable for this project type
            var applicablePatterns = _patterns
                .Where(p => p.ApplicableProjectTypes.Contains(projectType, StringComparer.OrdinalIgnoreCase) ||
                            p.ApplicableProjectTypes.Contains("*"))
                .ToList();

            foreach (var pattern in applicablePatterns)
            {
                var applicability = CalculateApplicability(pattern, context);

                if (applicability > 0.4)
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = pattern,
                        Applicability = applicability,
                        Rationale = GenerateRationale(pattern, context),
                        Implementation = GenerateImplementationSteps(pattern, context)
                    });
                }
            }

            return suggestions
                .OrderByDescending(s => s.Applicability)
                .Take(5)
                .ToList();
        }

        /// <summary>
        /// Learns from a completed design to improve pattern recognition.
        /// </summary>
        public void LearnFromDesign(DesignContext context, DesignFeedback feedback)
        {
            var recognizedPatterns = RecognizePatterns(context);

            foreach (var match in recognizedPatterns)
            {
                if (!_usageStats.ContainsKey(match.Pattern.Id))
                {
                    _usageStats[match.Pattern.Id] = new PatternUsageStats();
                }

                var stats = _usageStats[match.Pattern.Id];
                stats.UsageCount++;

                if (feedback.WasSuccessful)
                {
                    stats.SuccessCount++;
                }

                stats.LastUsed = DateTime.Now;
                stats.AverageRating = (stats.AverageRating * (stats.UsageCount - 1) + feedback.Rating) / stats.UsageCount;
            }

            Logger.Info($"Learned from design. Updated {recognizedPatterns.Count} pattern stats.");
        }

        /// <summary>
        /// Gets pattern recommendations based on partial input.
        /// </summary>
        public List<PatternRecommendation> GetRecommendations(PartialDesign partial)
        {
            var recommendations = new List<PatternRecommendation>();

            // Analyze what's missing based on project type
            var requiredRooms = GetRequiredRooms(partial.ProjectType);
            var existingRoomTypes = partial.Rooms.Select(r => r.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRooms = requiredRooms.Where(r => !existingRoomTypes.Contains(r)).ToList();

            if (missingRooms.Any())
            {
                recommendations.Add(new PatternRecommendation
                {
                    Type = RecommendationType.MissingElement,
                    Title = "Complete Room Layout",
                    Description = $"Consider adding: {string.Join(", ", missingRooms)}",
                    Priority = Priority.High,
                    Elements = missingRooms
                });
            }

            // Check adjacency patterns
            foreach (var room in partial.Rooms)
            {
                var adjacencyIssues = CheckAdjacencyPattern(room, partial.Rooms);
                if (adjacencyIssues.Any())
                {
                    recommendations.Add(new PatternRecommendation
                    {
                        Type = RecommendationType.AdjacencyOptimization,
                        Title = $"Optimize {room.Type} Placement",
                        Description = string.Join("; ", adjacencyIssues),
                        Priority = Priority.Medium,
                        RelatedRoomId = room.Id
                    });
                }
            }

            // Check circulation patterns
            var circulationIssues = AnalyzeCirculationPattern(partial);
            recommendations.AddRange(circulationIssues);

            return recommendations
                .OrderByDescending(r => (int)r.Priority)
                .ToList();
        }

        #endregion

        #region Pattern Initialization

        private void InitializeBuiltInPatterns()
        {
            // ===== RESIDENTIAL PATTERNS =====

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "RES-FUNC-ZONE",
                Name = "Functional Zoning",
                Category = PatternCategory.Layout,
                Description = "Separates public, private, and service zones",
                ApplicableProjectTypes = new[] { "Residential", "Apartment", "House", "*" },
                Requirements = new PatternRequirements
                {
                    MinRooms = 4,
                    RequiredRoomTypes = new[] { "Living", "Bedroom", "Kitchen", "Bathroom" },
                    ZoneDefinitions = new Dictionary<string, string[]>
                    {
                        ["Public"] = new[] { "Living", "Dining", "Foyer" },
                        ["Private"] = new[] { "Bedroom", "Bathroom", "Study" },
                        ["Service"] = new[] { "Kitchen", "Laundry", "Storage" }
                    }
                },
                Benefits = new[]
                {
                    "Clear separation between living and sleeping areas",
                    "Service functions grouped for efficiency",
                    "Privacy gradient from public to private spaces"
                },
                ImplementationGuidelines = new[]
                {
                    "Place public zones near entry",
                    "Locate private zones away from main circulation",
                    "Service zones should be accessible but not prominent"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "RES-MASTER-SUITE",
                Name = "Master Suite Configuration",
                Category = PatternCategory.RoomConfiguration,
                Description = "Master bedroom with en-suite bathroom and walk-in closet",
                ApplicableProjectTypes = new[] { "Residential", "House", "Apartment" },
                Requirements = new PatternRequirements
                {
                    RequiredRoomTypes = new[] { "Master Bedroom", "Master Bathroom" },
                    AdjacentPairs = new[]
                    {
                        ("Master Bedroom", "Master Bathroom"),
                        ("Master Bedroom", "Walk-in Closet")
                    }
                },
                Benefits = new[]
                {
                    "Private bathroom access",
                    "Ample storage space",
                    "Separation from other bedrooms"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "RES-KITCHEN-TRIANGLE",
                Name = "Kitchen Work Triangle",
                Category = PatternCategory.RoomConfiguration,
                Description = "Efficient arrangement of sink, stove, and refrigerator",
                ApplicableProjectTypes = new[] { "Residential", "House", "Apartment", "*" },
                Requirements = new PatternRequirements
                {
                    RequiredRoomTypes = new[] { "Kitchen" },
                    SpatialConstraints = new Dictionary<string, double>
                    {
                        ["MinTrianglePerimeter"] = 3.6, // meters
                        ["MaxTrianglePerimeter"] = 8.0,
                        ["MaxSingleLeg"] = 2.7
                    }
                },
                Benefits = new[]
                {
                    "Minimized walking distance",
                    "Efficient workflow",
                    "Clear paths between work stations"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "RES-OPEN-PLAN",
                Name = "Open Plan Living",
                Category = PatternCategory.Layout,
                Description = "Combined living, dining, and kitchen space",
                ApplicableProjectTypes = new[] { "Residential", "Apartment", "House" },
                Requirements = new PatternRequirements
                {
                    CombinedSpaces = new[] { "Living", "Dining", "Kitchen" },
                    MinCombinedArea = 30.0
                },
                Benefits = new[]
                {
                    "Enhanced social interaction",
                    "Flexible space usage",
                    "Better natural light distribution",
                    "Sense of spaciousness"
                }
            });

            // ===== CIRCULATION PATTERNS =====

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "CIRC-CENTRAL-HALL",
                Name = "Central Hallway Circulation",
                Category = PatternCategory.Circulation,
                Description = "Rooms arranged along central corridor",
                ApplicableProjectTypes = new[] { "Residential", "Office", "*" },
                Requirements = new PatternRequirements
                {
                    RequiredRoomTypes = new[] { "Corridor" },
                    CirculationType = CirculationType.Corridor
                },
                Benefits = new[]
                {
                    "Clear wayfinding",
                    "Efficient circulation",
                    "Easy room access"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "CIRC-ENFILADE",
                Name = "Enfilade Circulation",
                Category = PatternCategory.Circulation,
                Description = "Rooms connected directly in sequence",
                ApplicableProjectTypes = new[] { "Residential", "Gallery", "*" },
                Requirements = new PatternRequirements
                {
                    CirculationType = CirculationType.DirectConnection,
                    MinConnectedRooms = 3
                },
                Benefits = new[]
                {
                    "Visual continuity",
                    "Maximizes floor area (no corridors)",
                    "Creates processional experience"
                }
            });

            // ===== SPATIAL QUALITY PATTERNS =====

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "SPATIAL-DOUBLE-HEIGHT",
                Name = "Double Height Space",
                Category = PatternCategory.SpatialQuality,
                Description = "Two-story volume for dramatic effect",
                ApplicableProjectTypes = new[] { "Residential", "Public", "*" },
                Requirements = new PatternRequirements
                {
                    MinCeilingHeight = 5.0,
                    ApplicableRoomTypes = new[] { "Living", "Foyer", "Gallery" }
                },
                Benefits = new[]
                {
                    "Dramatic spatial experience",
                    "Enhanced natural light",
                    "Visual connection between levels"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "SPATIAL-BORROWED-LIGHT",
                Name = "Borrowed Light",
                Category = PatternCategory.SpatialQuality,
                Description = "Interior rooms receive light from adjacent spaces",
                ApplicableProjectTypes = new[] { "*" },
                Requirements = new PatternRequirements
                {
                    GlazedPartitions = true,
                    ApplicableRoomTypes = new[] { "Corridor", "Bathroom", "Storage" }
                },
                Benefits = new[]
                {
                    "Natural light in interior rooms",
                    "Maintains visual privacy",
                    "Reduces artificial lighting needs"
                }
            });

            // ===== EFFICIENCY PATTERNS =====

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "EFF-WET-STACK",
                Name = "Wet Room Stacking",
                Category = PatternCategory.Efficiency,
                Description = "Vertically aligned bathrooms and kitchens",
                ApplicableProjectTypes = new[] { "Apartment", "Multi-family", "*" },
                Requirements = new PatternRequirements
                {
                    VerticalAlignment = new[] { "Bathroom", "Kitchen" }
                },
                Benefits = new[]
                {
                    "Reduced plumbing runs",
                    "Lower construction cost",
                    "Easier maintenance access"
                }
            });

            _patterns.Add(new ArchitecturalPattern
            {
                Id = "EFF-BACK-TO-BACK",
                Name = "Back-to-Back Wet Rooms",
                Category = PatternCategory.Efficiency,
                Description = "Bathrooms/kitchens sharing a wet wall",
                ApplicableProjectTypes = new[] { "*" },
                Requirements = new PatternRequirements
                {
                    SharedWall = new[] { "Bathroom", "Kitchen" }
                },
                Benefits = new[]
                {
                    "Minimized plumbing runs",
                    "Cost-effective installation",
                    "Simplified maintenance"
                }
            });

            Logger.Info($"Initialized {_patterns.Count} architectural patterns");
        }

        #endregion

        #region Private Methods

        private PatternMatch TryMatchPattern(ArchitecturalPattern pattern, DesignContext context)
        {
            var match = new PatternMatch
            {
                Pattern = pattern,
                Confidence = 0,
                Relevance = 0.5
            };

            var requirements = pattern.Requirements;
            var matchedCriteria = 0;
            var totalCriteria = 0;

            // Check required room types
            if (requirements.RequiredRoomTypes?.Length > 0)
            {
                totalCriteria++;
                var existingTypes = context.Rooms.Select(r => r.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var foundTypes = requirements.RequiredRoomTypes.Count(t =>
                    existingTypes.Contains(t));
                var ratio = (double)foundTypes / requirements.RequiredRoomTypes.Length;

                if (ratio >= 0.8)
                {
                    matchedCriteria++;
                    match.MatchedElements.AddRange(
                        context.Rooms.Where(r =>
                            requirements.RequiredRoomTypes.Contains(r.Type, StringComparer.OrdinalIgnoreCase))
                        .Select(r => r.Id));
                }
            }

            // Check minimum rooms
            if (requirements.MinRooms > 0)
            {
                totalCriteria++;
                if (context.Rooms.Count >= requirements.MinRooms)
                {
                    matchedCriteria++;
                }
            }

            // Check adjacent pairs
            if (requirements.AdjacentPairs?.Length > 0)
            {
                totalCriteria++;
                var adjacentPairsFound = 0;
                foreach (var (type1, type2) in requirements.AdjacentPairs)
                {
                    var room1 = context.Rooms.FirstOrDefault(r =>
                        r.Type.Equals(type1, StringComparison.OrdinalIgnoreCase));
                    var room2 = context.Rooms.FirstOrDefault(r =>
                        r.Type.Equals(type2, StringComparison.OrdinalIgnoreCase));

                    if (room1 != null && room2 != null)
                    {
                        if (context.Adjacencies.ContainsKey(room1.Id) &&
                            context.Adjacencies[room1.Id].Contains(room2.Id))
                        {
                            adjacentPairsFound++;
                        }
                    }
                }

                if (adjacentPairsFound >= requirements.AdjacentPairs.Length * 0.7)
                {
                    matchedCriteria++;
                }
            }

            // Calculate confidence
            if (totalCriteria > 0)
            {
                match.Confidence = (double)matchedCriteria / totalCriteria;
            }

            // Adjust relevance based on usage stats
            if (_usageStats.TryGetValue(pattern.Id, out var stats))
            {
                if (stats.UsageCount > 5)
                {
                    match.Relevance = stats.AverageRating / 5.0;
                }
            }

            return match.Confidence >= _minConfidenceThreshold ? match : null;
        }

        private double CalculateApplicability(ArchitecturalPattern pattern, DesignContext context)
        {
            var score = 0.5; // Base score

            // Higher score if we have some of the required elements
            var requirements = pattern.Requirements;
            if (requirements.RequiredRoomTypes?.Length > 0)
            {
                var existingTypes = context.Rooms.Select(r => r.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var foundCount = requirements.RequiredRoomTypes.Count(t => existingTypes.Contains(t));
                score += 0.3 * ((double)foundCount / requirements.RequiredRoomTypes.Length);
            }

            // Higher score if project type matches
            if (pattern.ApplicableProjectTypes.Contains(context.ProjectType, StringComparer.OrdinalIgnoreCase))
            {
                score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        private string GenerateRationale(ArchitecturalPattern pattern, DesignContext context)
        {
            var benefits = pattern.Benefits.Take(2);
            return $"This pattern provides: {string.Join(", ", benefits)}";
        }

        private List<string> GenerateImplementationSteps(ArchitecturalPattern pattern, DesignContext context)
        {
            var steps = new List<string>();

            if (pattern.ImplementationGuidelines?.Length > 0)
            {
                steps.AddRange(pattern.ImplementationGuidelines);
            }
            else
            {
                steps.Add($"Review the {pattern.Name} requirements");
                steps.Add("Analyze current layout against pattern criteria");
                steps.Add("Make adjustments to align with pattern principles");
            }

            return steps;
        }

        private List<string> GetRequiredRooms(string projectType)
        {
            return projectType?.ToLowerInvariant() switch
            {
                "residential" or "house" => new List<string>
                {
                    "Living", "Kitchen", "Bedroom", "Bathroom"
                },
                "apartment" => new List<string>
                {
                    "Living", "Kitchen", "Bedroom", "Bathroom"
                },
                "office" => new List<string>
                {
                    "Reception", "Office", "Meeting", "Bathroom"
                },
                _ => new List<string> { "Room" }
            };
        }

        private List<string> CheckAdjacencyPattern(RoomInfo room, List<RoomInfo> allRooms)
        {
            var issues = new List<string>();

            var adjacencyRules = new Dictionary<string, (string[] preferred, string[] avoided)>
            {
                ["Kitchen"] = (new[] { "Dining", "Living" }, new[] { "Bedroom" }),
                ["Bedroom"] = (new[] { "Bathroom", "Corridor" }, new[] { "Kitchen", "Garage" }),
                ["Bathroom"] = (new[] { "Bedroom", "Corridor" }, Array.Empty<string>()),
                ["Living"] = (new[] { "Dining", "Kitchen" }, Array.Empty<string>())
            };

            if (adjacencyRules.TryGetValue(room.Type, out var rules))
            {
                // Check for avoided adjacencies
                foreach (var other in allRooms.Where(r => r.Id != room.Id))
                {
                    if (rules.avoided.Contains(other.Type, StringComparer.OrdinalIgnoreCase))
                    {
                        if (room.AdjacentRoomIds?.Contains(other.Id) == true)
                        {
                            issues.Add($"{room.Type} should not be adjacent to {other.Type}");
                        }
                    }
                }
            }

            return issues;
        }

        private List<PatternRecommendation> AnalyzeCirculationPattern(PartialDesign partial)
        {
            var recommendations = new List<PatternRecommendation>();

            // Check if circulation is defined
            var hasCorridor = partial.Rooms.Any(r =>
                r.Type.Equals("Corridor", StringComparison.OrdinalIgnoreCase) ||
                r.Type.Equals("Hallway", StringComparison.OrdinalIgnoreCase));

            var roomCount = partial.Rooms.Count;

            if (roomCount > 4 && !hasCorridor)
            {
                recommendations.Add(new PatternRecommendation
                {
                    Type = RecommendationType.CirculationImprovement,
                    Title = "Add Circulation Space",
                    Description = "Multiple rooms without defined circulation. Consider adding a corridor or hallway.",
                    Priority = Priority.Medium
                });
            }

            return recommendations;
        }

        #endregion
    }

    #region Supporting Types

    public class ArchitecturalPattern
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public PatternCategory Category { get; set; }
        public string Description { get; set; }
        public string[] ApplicableProjectTypes { get; set; }
        public PatternRequirements Requirements { get; set; }
        public string[] Benefits { get; set; }
        public string[] ImplementationGuidelines { get; set; }
    }

    public class PatternRequirements
    {
        public int MinRooms { get; set; }
        public string[] RequiredRoomTypes { get; set; }
        public (string, string)[] AdjacentPairs { get; set; }
        public string[] CombinedSpaces { get; set; }
        public double MinCombinedArea { get; set; }
        public double MinCeilingHeight { get; set; }
        public string[] ApplicableRoomTypes { get; set; }
        public string[] VerticalAlignment { get; set; }
        public string[] SharedWall { get; set; }
        public bool GlazedPartitions { get; set; }
        public CirculationType CirculationType { get; set; }
        public int MinConnectedRooms { get; set; }
        public Dictionary<string, string[]> ZoneDefinitions { get; set; }
        public Dictionary<string, double> SpatialConstraints { get; set; }
    }

    public enum PatternCategory
    {
        Layout,
        RoomConfiguration,
        Circulation,
        SpatialQuality,
        Efficiency
    }

    public enum CirculationType
    {
        Corridor,
        DirectConnection,
        Open
    }

    public class PatternMatch
    {
        public ArchitecturalPattern Pattern { get; set; }
        public double Confidence { get; set; }
        public double Relevance { get; set; }
        public List<string> MatchedElements { get; set; } = new();
    }

    public class PatternSuggestion
    {
        public ArchitecturalPattern Pattern { get; set; }
        public double Applicability { get; set; }
        public string Rationale { get; set; }
        public List<string> Implementation { get; set; }
    }

    public class PatternRecommendation
    {
        public RecommendationType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Priority Priority { get; set; }
        public List<string> Elements { get; set; }
        public string RelatedRoomId { get; set; }
    }

    public enum RecommendationType
    {
        MissingElement,
        AdjacencyOptimization,
        CirculationImprovement,
        SpatialQuality
    }

    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public class DesignContext
    {
        public string ProjectType { get; set; }
        public List<RoomInfo> Rooms { get; set; } = new();
        public Dictionary<string, List<string>> Adjacencies { get; set; } = new();
    }

    public class PartialDesign
    {
        public string ProjectType { get; set; }
        public List<RoomInfo> Rooms { get; set; } = new();
    }

    public class RoomInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double Area { get; set; }
        public List<string> AdjacentRoomIds { get; set; } = new();
    }

    public class DesignFeedback
    {
        public bool WasSuccessful { get; set; }
        public double Rating { get; set; } // 1-5
        public string Comments { get; set; }
    }

    public class PatternUsageStats
    {
        public int UsageCount { get; set; }
        public int SuccessCount { get; set; }
        public double AverageRating { get; set; }
        public DateTime LastUsed { get; set; }
    }

    #endregion
}
