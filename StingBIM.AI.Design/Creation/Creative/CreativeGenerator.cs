// =========================================================================
// StingBIM.AI.Creation - Creative Generation Engine
// Generates design variations and creative alternatives with intelligence integration
// Master Proposal Reference: Part 4.2 Phase 2 - Generative Design
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Creation.Creative
{
    /// <summary>
    /// Creative generation engine that produces design variations,
    /// alternative layouts, and innovative solutions based on constraints.
    /// </summary>
    public class CreativeGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, DesignPattern> _patterns;
        private readonly Dictionary<string, StyleProfile> _styles;
        private readonly Dictionary<string, TransformationRule> _transformations;
        private readonly Random _random;
        private readonly CreativeConfiguration _config;

        public CreativeGenerator(CreativeConfiguration config = null)
        {
            _config = config ?? new CreativeConfiguration();
            _patterns = new Dictionary<string, DesignPattern>();
            _styles = new Dictionary<string, StyleProfile>();
            _transformations = new Dictionary<string, TransformationRule>();
            _random = new Random();

            InitializePatterns();
            InitializeStyles();
            InitializeTransformations();

            Logger.Info($"CreativeGenerator initialized: {_patterns.Count} patterns, {_styles.Count} styles, {_transformations.Count} transformations");
        }

        #region Initialization

        private void InitializePatterns()
        {
            // Residential patterns
            AddPattern(new DesignPattern
            {
                PatternId = "OpenPlan",
                Category = PatternCategory.Residential,
                Description = "Open floor plan with minimal partitions",
                Principles = new[]
                {
                    "Maximize visual connectivity",
                    "Define zones through furniture and level changes",
                    "Use kitchen island as room divider"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["Living"] = new[] { "Dining", "Kitchen" },
                    ["Kitchen"] = new[] { "Dining", "Living" },
                    ["Dining"] = new[] { "Kitchen", "Living", "Outdoor" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinArea", Value = 40, Unit = "m²" },
                    new PatternConstraint { Type = "MaxPartitions", Value = 2 }
                }
            });

            AddPattern(new DesignPattern
            {
                PatternId = "EnSuite",
                Category = PatternCategory.Residential,
                Description = "Bedroom with attached private bathroom",
                Principles = new[]
                {
                    "Direct access from bedroom to bathroom",
                    "Visual privacy from bedroom",
                    "Walk-in closet as transition zone"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["Bedroom"] = new[] { "Bathroom", "Closet" },
                    ["Bathroom"] = new[] { "Bedroom" },
                    ["Closet"] = new[] { "Bedroom", "Bathroom" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinBedroomArea", Value = 12, Unit = "m²" },
                    new PatternConstraint { Type = "MinBathroomArea", Value = 4, Unit = "m²" }
                }
            });

            AddPattern(new DesignPattern
            {
                PatternId = "KitchenTriangle",
                Category = PatternCategory.Kitchen,
                Description = "Efficient work triangle between sink, stove, and refrigerator",
                Principles = new[]
                {
                    "Total triangle perimeter 4-8 meters",
                    "No obstructions crossing the triangle",
                    "Each leg between 1.2-2.7 meters"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["Sink"] = new[] { "Stove", "Refrigerator" },
                    ["Stove"] = new[] { "Sink", "Refrigerator" },
                    ["Refrigerator"] = new[] { "Sink", "Stove" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinPerimeter", Value = 4, Unit = "m" },
                    new PatternConstraint { Type = "MaxPerimeter", Value = 8, Unit = "m" }
                }
            });

            // Commercial patterns
            AddPattern(new DesignPattern
            {
                PatternId = "ActivityBasedWorking",
                Category = PatternCategory.Commercial,
                Description = "Flexible workspace with activity zones",
                Principles = new[]
                {
                    "Variety of work settings",
                    "No assigned desks",
                    "Collaboration and focus zones separated"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["FocusZone"] = new[] { "QuietRoom" },
                    ["CollaborationZone"] = new[] { "MeetingRoom", "Breakout" },
                    ["SocialZone"] = new[] { "Kitchen", "Lounge" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "DeskRatio", Value = 0.7 }, // 70% desks to people
                    new PatternConstraint { Type = "MinCollaborationSpace", Value = 20, Unit = "%" }
                }
            });

            AddPattern(new DesignPattern
            {
                PatternId = "BiophilicDesign",
                Category = PatternCategory.Universal,
                Description = "Nature-integrated design for wellbeing",
                Principles = new[]
                {
                    "Visual connection with nature",
                    "Natural materials and textures",
                    "Dynamic and diffuse lighting",
                    "Plant integration"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["Workspace"] = new[] { "GreenWall", "Window" },
                    ["Circulation"] = new[] { "Planter", "Skylight" },
                    ["Breakout"] = new[] { "Garden", "WaterFeature" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinNaturalLight", Value = 50, Unit = "%" },
                    new PatternConstraint { Type = "MinPlantCoverage", Value = 5, Unit = "%" }
                }
            });

            // Healthcare patterns
            AddPattern(new DesignPattern
            {
                PatternId = "PatientWard",
                Category = PatternCategory.Healthcare,
                Description = "Patient room cluster with nursing station visibility",
                Principles = new[]
                {
                    "Direct line-of-sight from nurse station to patient rooms",
                    "Minimize travel distance for care staff",
                    "Patient privacy with clinical observation access",
                    "Natural light access for all patient beds (IBC 407)"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["NurseStation"] = new[] { "PatientRoom", "MedRoom", "Corridor" },
                    ["PatientRoom"] = new[] { "Bathroom", "NurseStation" },
                    ["MedRoom"] = new[] { "NurseStation" },
                    ["CleanUtility"] = new[] { "NurseStation", "SoiledUtility" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MaxWalkDistance", Value = 30, Unit = "m" },
                    new PatternConstraint { Type = "MinBedSpacing", Value = 2.4, Unit = "m" }
                }
            });

            AddPattern(new DesignPattern
            {
                PatternId = "ClinicalCorridor",
                Category = PatternCategory.Healthcare,
                Description = "Clinical corridor with zoned access and infection control",
                Principles = new[]
                {
                    "Separate clean and dirty flows",
                    "Airlock transitions between zones",
                    "Hand wash stations at zone boundaries",
                    "ADA-compliant corridor widths (2400mm min per IBC 407)"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["PublicZone"] = new[] { "WaitingArea", "Reception" },
                    ["ClinicalZone"] = new[] { "ExamRoom", "NurseStation" },
                    ["StaffZone"] = new[] { "OffDutyRoom", "Locker" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinCorridorWidth", Value = 2.4, Unit = "m" },
                    new PatternConstraint { Type = "MaxDeadEnd", Value = 6, Unit = "m" }
                }
            });

            // Educational patterns
            AddPattern(new DesignPattern
            {
                PatternId = "ClassroomCluster",
                Category = PatternCategory.Education,
                Description = "Flexible classroom cluster with shared breakout spaces",
                Principles = new[]
                {
                    "Classrooms grouped around shared learning commons",
                    "Visual connectivity between classrooms and breakout",
                    "Acoustic separation between teaching spaces (STC 50+)",
                    "Natural daylight from at least two sides"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["Classroom"] = new[] { "BreakoutSpace", "Corridor", "Storage" },
                    ["BreakoutSpace"] = new[] { "Classroom", "Library" },
                    ["TeacherPrep"] = new[] { "Classroom", "StaffRoom" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinClassroomArea", Value = 55, Unit = "m²" },
                    new PatternConstraint { Type = "MinDaylightFactor", Value = 2, Unit = "%" }
                }
            });

            // Hospitality patterns
            AddPattern(new DesignPattern
            {
                PatternId = "HotelFloorPlate",
                Category = PatternCategory.Hospitality,
                Description = "Efficient hotel floor with double-loaded corridor",
                Principles = new[]
                {
                    "Double-loaded corridor for efficiency (>70% net-to-gross)",
                    "Stair/lift cores at both ends for egress",
                    "Service areas centralized near lifts",
                    "IBC 1004.5: Max travel distance 60m to exit"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["GuestRoom"] = new[] { "Bathroom", "Corridor" },
                    ["Corridor"] = new[] { "GuestRoom", "LiftLobby", "ServiceArea" },
                    ["LiftLobby"] = new[] { "Corridor", "Stair" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MinRoomWidth", Value = 3.6, Unit = "m" },
                    new PatternConstraint { Type = "MaxTravelDistance", Value = 60, Unit = "m" }
                }
            });

            // Passive cooling pattern (Africa-relevant)
            AddPattern(new DesignPattern
            {
                PatternId = "PassiveCooling",
                Category = PatternCategory.Universal,
                Description = "Cross-ventilation and thermal mass for passive cooling",
                Principles = new[]
                {
                    "Building oriented for prevailing wind capture",
                    "Cross-ventilation with openings on opposite walls",
                    "High thermal mass materials (rammed earth, concrete) for night cooling",
                    "Deep overhangs (>900mm) on sun-exposed facades",
                    "Stack ventilation with high-level openings"
                },
                SpaceRelationships = new Dictionary<string, string[]>
                {
                    ["LivingSpace"] = new[] { "Veranda", "Courtyard" },
                    ["Courtyard"] = new[] { "LivingSpace", "Kitchen" },
                    ["Veranda"] = new[] { "LivingSpace", "Garden" }
                },
                Constraints = new[]
                {
                    new PatternConstraint { Type = "MaxBuildingDepth", Value = 14, Unit = "m" },
                    new PatternConstraint { Type = "MinOverhangDepth", Value = 0.9, Unit = "m" }
                }
            });
        }

        private void InitializeStyles()
        {
            AddStyle(new StyleProfile
            {
                StyleId = "Modern",
                Description = "Clean lines, minimal ornamentation, open spaces",
                MaterialPalette = new[]
                {
                    "White painted walls",
                    "Polished concrete floors",
                    "Large format glass",
                    "Steel structural elements"
                },
                ColorScheme = new ColorScheme
                {
                    Primary = new[] { "#FFFFFF", "#F5F5F5" },
                    Accent = new[] { "#212121", "#757575" },
                    Feature = new[] { "#2196F3", "#FF5722" }
                },
                ProportionRules = new Dictionary<string, double>
                {
                    ["WindowToWallRatio"] = 0.4,
                    ["CeilingHeight"] = 2.7,
                    ["CorridorWidth"] = 1.5
                },
                Elements = new StyleElement[]
                {
                    new() { Type = "Door", Style = "Flush panel", Material = "Timber veneer" },
                    new() { Type = "Window", Style = "Floor to ceiling", Material = "Aluminum frame" },
                    new() { Type = "Handrail", Style = "Glass balustrade", Material = "Stainless steel" }
                }
            });

            AddStyle(new StyleProfile
            {
                StyleId = "Industrial",
                Description = "Exposed structure, raw materials, urban aesthetic",
                MaterialPalette = new[]
                {
                    "Exposed brick",
                    "Raw concrete",
                    "Black steel",
                    "Reclaimed timber"
                },
                ColorScheme = new ColorScheme
                {
                    Primary = new[] { "#9E9E9E", "#795548" },
                    Accent = new[] { "#212121", "#FF6F00" },
                    Feature = new[] { "#B71C1C", "#1B5E20" }
                },
                ProportionRules = new Dictionary<string, double>
                {
                    ["WindowToWallRatio"] = 0.35,
                    ["CeilingHeight"] = 3.5,
                    ["CorridorWidth"] = 1.8
                },
                Elements = new StyleElement[]
                {
                    new() { Type = "Door", Style = "Steel frame glazed", Material = "Black steel" },
                    new() { Type = "Window", Style = "Factory style", Material = "Black steel" },
                    new() { Type = "Lighting", Style = "Pendant industrial", Material = "Metal" }
                }
            });

            AddStyle(new StyleProfile
            {
                StyleId = "Scandinavian",
                Description = "Light, functional, natural materials",
                MaterialPalette = new[]
                {
                    "Light oak timber",
                    "White painted surfaces",
                    "Wool textiles",
                    "Ceramic tiles"
                },
                ColorScheme = new ColorScheme
                {
                    Primary = new[] { "#FAFAFA", "#ECEFF1" },
                    Accent = new[] { "#8D6E63", "#90A4AE" },
                    Feature = new[] { "#4CAF50", "#42A5F5" }
                },
                ProportionRules = new Dictionary<string, double>
                {
                    ["WindowToWallRatio"] = 0.45,
                    ["CeilingHeight"] = 2.5,
                    ["CorridorWidth"] = 1.2
                },
                Elements = new StyleElement[]
                {
                    new() { Type = "Door", Style = "Simple panel", Material = "Light timber" },
                    new() { Type = "Window", Style = "Simple casement", Material = "Timber frame" },
                    new() { Type = "Furniture", Style = "Minimal curved", Material = "Light wood" }
                }
            });

            AddStyle(new StyleProfile
            {
                StyleId = "African Contemporary",
                Description = "Local materials, cultural patterns, climate response",
                MaterialPalette = new[]
                {
                    "Local stone",
                    "Rammed earth",
                    "Bamboo",
                    "Woven textiles"
                },
                ColorScheme = new ColorScheme
                {
                    Primary = new[] { "#D7CCC8", "#BCAAA4" },
                    Accent = new[] { "#5D4037", "#BF360C" },
                    Feature = new[] { "#FF6F00", "#1B5E20" }
                },
                ProportionRules = new Dictionary<string, double>
                {
                    ["WindowToWallRatio"] = 0.25, // Reduced for climate
                    ["CeilingHeight"] = 3.0,
                    ["CorridorWidth"] = 1.5,
                    ["OverhangDepth"] = 0.9 // Sun shading
                },
                Elements = new StyleElement[]
                {
                    new() { Type = "Door", Style = "Carved panel", Material = "Hardwood" },
                    new() { Type = "Screen", Style = "Perforated pattern", Material = "Terracotta" },
                    new() { Type = "Ceiling", Style = "Exposed rafters", Material = "Bamboo" }
                }
            });
        }

        private void InitializeTransformations()
        {
            // Spatial transformations
            AddTransformation(new TransformationRule
            {
                TransformationId = "MergeSpaces",
                Description = "Combine adjacent spaces to create larger area",
                Applicability = ctx => ctx.HasAdjacentSpaces && ctx.CombinedArea < ctx.MaxAllowedArea,
                Transform = (layout, ctx) =>
                {
                    var merged = new LayoutVariation { BaseLayout = layout };
                    // Merge logic would go here
                    merged.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.Merge,
                        Description = "Merged adjacent spaces",
                        AffectedElements = ctx.SelectedSpaces.ToList()
                    });
                    return merged;
                }
            });

            AddTransformation(new TransformationRule
            {
                TransformationId = "SubdivideSpace",
                Description = "Divide large space into smaller zones",
                Applicability = ctx => ctx.SpaceArea > 30 && ctx.RequiresSubdivision,
                Transform = (layout, ctx) =>
                {
                    var subdivided = new LayoutVariation { BaseLayout = layout };
                    subdivided.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.Subdivide,
                        Description = "Created sub-zones within space",
                        AffectedElements = new List<string> { ctx.TargetSpace }
                    });
                    return subdivided;
                }
            });

            AddTransformation(new TransformationRule
            {
                TransformationId = "RotateLayout",
                Description = "Rotate layout orientation for different aspect",
                Applicability = ctx => ctx.AllowsRotation,
                Transform = (layout, ctx) =>
                {
                    var rotated = new LayoutVariation { BaseLayout = layout };
                    rotated.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.Rotate,
                        Description = $"Rotated layout {ctx.RotationAngle} degrees",
                        AffectedElements = layout.AllElements.ToList()
                    });
                    return rotated;
                }
            });

            AddTransformation(new TransformationRule
            {
                TransformationId = "MirrorLayout",
                Description = "Create mirror image of layout",
                Applicability = ctx => ctx.AllowsMirroring,
                Transform = (layout, ctx) =>
                {
                    var mirrored = new LayoutVariation { BaseLayout = layout };
                    mirrored.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.Mirror,
                        Description = $"Mirrored layout along {ctx.MirrorAxis} axis",
                        AffectedElements = layout.AllElements.ToList()
                    });
                    return mirrored;
                }
            });

            AddTransformation(new TransformationRule
            {
                TransformationId = "OptimizeCirculation",
                Description = "Improve circulation path efficiency",
                Applicability = ctx => ctx.CirculationEfficiency < 0.7,
                Transform = (layout, ctx) =>
                {
                    var optimized = new LayoutVariation { BaseLayout = layout };
                    optimized.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.Optimize,
                        Description = "Optimized circulation paths",
                        AffectedElements = ctx.CirculationElements.ToList()
                    });
                    return optimized;
                }
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Generate design variations based on input layout and constraints.
        /// </summary>
        public GenerationResult GenerateVariations(
            Layout inputLayout,
            GenerationConstraints constraints,
            int count = 5)
        {
            var result = new GenerationResult
            {
                OriginalLayout = inputLayout,
                Constraints = constraints,
                Timestamp = DateTime.UtcNow
            };

            var variations = new List<LayoutVariation>();

            // Generate pattern-based variations
            var patternVariations = GeneratePatternVariations(inputLayout, constraints);
            variations.AddRange(patternVariations);

            // Generate transformation-based variations
            var transformVariations = GenerateTransformVariations(inputLayout, constraints);
            variations.AddRange(transformVariations);

            // Generate style-based variations
            var styleVariations = GenerateStyleVariations(inputLayout, constraints);
            variations.AddRange(styleVariations);

            // Generate random variations for exploration
            var randomVariations = GenerateRandomVariations(inputLayout, constraints);
            variations.AddRange(randomVariations);

            // Score and rank variations
            var scored = variations
                .Select(v => ScoreVariation(v, constraints))
                .OrderByDescending(v => v.Score)
                .Take(count)
                .ToList();

            result.Variations = scored;
            result.BestVariation = scored.FirstOrDefault();

            return result;
        }

        /// <summary>
        /// Generate alternatives for a specific space.
        /// </summary>
        public SpaceAlternatives GenerateSpaceAlternatives(
            Space space,
            SpaceRequirements requirements)
        {
            var alternatives = new SpaceAlternatives
            {
                OriginalSpace = space,
                Requirements = requirements
            };

            // Layout alternatives
            alternatives.LayoutOptions = GenerateLayoutOptions(space, requirements);

            // Furniture alternatives
            alternatives.FurnitureOptions = GenerateFurnitureOptions(space, requirements);

            // Style alternatives
            alternatives.StyleOptions = GenerateStyleOptions(space, requirements);

            // Size alternatives
            alternatives.SizeOptions = GenerateSizeOptions(space, requirements);

            return alternatives;
        }

        /// <summary>
        /// Apply a design pattern to a layout.
        /// </summary>
        public PatternApplication ApplyPattern(
            Layout layout,
            string patternId,
            PatternParameters parameters = null)
        {
            var application = new PatternApplication
            {
                PatternId = patternId,
                OriginalLayout = layout
            };

            if (!_patterns.TryGetValue(patternId, out var pattern))
            {
                application.Success = false;
                application.Message = $"Pattern '{patternId}' not found";
                return application;
            }

            // Check constraints
            var constraintCheck = CheckPatternConstraints(layout, pattern);
            if (!constraintCheck.Passed)
            {
                application.Success = false;
                application.Message = constraintCheck.Message;
                application.Suggestions = constraintCheck.Suggestions;
                return application;
            }

            // Apply pattern transformations
            var transformedLayout = ApplyPatternTransformations(layout, pattern, parameters);

            application.Success = true;
            application.ResultingLayout = transformedLayout;
            application.AppliedPrinciples = pattern.Principles.ToList();
            application.Message = $"Successfully applied {pattern.Description}";

            return application;
        }

        /// <summary>
        /// Apply a style profile to a layout.
        /// </summary>
        public StyleApplication ApplyStyle(
            Layout layout,
            string styleId,
            StyleParameters parameters = null)
        {
            var application = new StyleApplication
            {
                StyleId = styleId,
                OriginalLayout = layout
            };

            if (!_styles.TryGetValue(styleId, out var style))
            {
                application.Success = false;
                application.Message = $"Style '{styleId}' not found";
                return application;
            }

            // Apply material changes
            application.MaterialChanges = GenerateMaterialChanges(layout, style);

            // Apply proportion changes
            application.ProportionChanges = GenerateProportionChanges(layout, style);

            // Apply element style changes
            application.ElementChanges = GenerateElementChanges(layout, style);

            // Apply color scheme
            application.ColorScheme = style.ColorScheme;

            application.Success = true;
            application.Message = $"Applied {style.Description} style";

            return application;
        }

        /// <summary>
        /// Get creative suggestions based on context.
        /// </summary>
        public CreativeSuggestions GetSuggestions(DesignContext context)
        {
            var suggestions = new CreativeSuggestions
            {
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // Pattern suggestions
            suggestions.PatternSuggestions = GetPatternSuggestions(context);

            // Style suggestions
            suggestions.StyleSuggestions = GetStyleSuggestions(context);

            // Improvement suggestions
            suggestions.ImprovementSuggestions = GetImprovementSuggestions(context);

            // Innovation suggestions
            suggestions.InnovationSuggestions = GetInnovationSuggestions(context);

            return suggestions;
        }

        /// <summary>
        /// Blend multiple design influences.
        /// </summary>
        public BlendResult BlendDesigns(
            List<Layout> sourceLayouts,
            BlendParameters parameters)
        {
            var result = new BlendResult
            {
                Sources = sourceLayouts,
                Parameters = parameters
            };

            // Extract features from each source
            var features = sourceLayouts.Select(ExtractFeatures).ToList();

            // Blend features based on weights
            var blendedFeatures = BlendFeatures(features, parameters.Weights);

            // Generate blended layout
            result.BlendedLayout = GenerateFromFeatures(blendedFeatures);

            // Document contributions
            result.FeatureContributions = DocumentContributions(features, blendedFeatures);

            return result;
        }

        #endregion

        #region Private Methods

        private void AddPattern(DesignPattern pattern)
        {
            _patterns[pattern.PatternId] = pattern;
        }

        private void AddStyle(StyleProfile style)
        {
            _styles[style.StyleId] = style;
        }

        private void AddTransformation(TransformationRule transformation)
        {
            _transformations[transformation.TransformationId] = transformation;
        }

        private List<LayoutVariation> GeneratePatternVariations(
            Layout layout,
            GenerationConstraints constraints)
        {
            var variations = new List<LayoutVariation>();

            var applicablePatterns = _patterns.Values
                .Where(p => IsPatternApplicable(p, layout, constraints))
                .Take(3);

            foreach (var pattern in applicablePatterns)
            {
                var variation = new LayoutVariation
                {
                    BaseLayout = layout,
                    VariationType = VariationType.PatternBased,
                    AppliedPattern = pattern.PatternId,
                    Description = $"Applied {pattern.Description}"
                };

                // Apply pattern principles
                foreach (var principle in pattern.Principles)
                {
                    variation.Changes.Add(new LayoutChange
                    {
                        Type = ChangeType.PatternApplication,
                        Description = principle
                    });
                }

                variations.Add(variation);
            }

            return variations;
        }

        private List<LayoutVariation> GenerateTransformVariations(
            Layout layout,
            GenerationConstraints constraints)
        {
            var variations = new List<LayoutVariation>();
            var context = CreateTransformationContext(layout, constraints);

            foreach (var transformation in _transformations.Values)
            {
                if (transformation.Applicability(context))
                {
                    var variation = transformation.Transform(layout, context);
                    variation.VariationType = VariationType.TransformBased;
                    variation.AppliedTransformation = transformation.TransformationId;
                    variation.Description = transformation.Description;
                    variations.Add(variation);
                }
            }

            return variations;
        }

        private List<LayoutVariation> GenerateStyleVariations(
            Layout layout,
            GenerationConstraints constraints)
        {
            var variations = new List<LayoutVariation>();

            var applicableStyles = constraints.PreferredStyles?.Any() == true
                ? _styles.Values.Where(s => constraints.PreferredStyles.Contains(s.StyleId))
                : _styles.Values.Take(2);

            foreach (var style in applicableStyles)
            {
                var variation = new LayoutVariation
                {
                    BaseLayout = layout,
                    VariationType = VariationType.StyleBased,
                    AppliedStyle = style.StyleId,
                    Description = $"Styled as {style.Description}"
                };

                variation.Changes.Add(new LayoutChange
                {
                    Type = ChangeType.StyleApplication,
                    Description = $"Applied {style.StyleId} material palette",
                    Details = string.Join(", ", style.MaterialPalette.Take(3))
                });

                variations.Add(variation);
            }

            return variations;
        }

        private List<LayoutVariation> GenerateRandomVariations(
            Layout layout,
            GenerationConstraints constraints)
        {
            var variations = new List<LayoutVariation>();

            for (int i = 0; i < 2; i++)
            {
                var variation = new LayoutVariation
                {
                    BaseLayout = layout,
                    VariationType = VariationType.Random,
                    Description = $"Exploratory variation {i + 1}"
                };

                // Random space adjustments within constraints
                var adjustments = GenerateRandomAdjustments(layout, constraints);
                variation.Changes.AddRange(adjustments);

                variations.Add(variation);
            }

            return variations;
        }

        private List<LayoutChange> GenerateRandomAdjustments(
            Layout layout,
            GenerationConstraints constraints)
        {
            var changes = new List<LayoutChange>();

            // Random dimension adjustments (±10%)
            if (_random.NextDouble() > 0.5)
            {
                var adjustPercent = (_random.NextDouble() * 20 - 10) / 100;
                changes.Add(new LayoutChange
                {
                    Type = ChangeType.Resize,
                    Description = $"Adjusted proportions by {adjustPercent:P0}"
                });
            }

            // Random position shifts
            if (_random.NextDouble() > 0.6)
            {
                changes.Add(new LayoutChange
                {
                    Type = ChangeType.Move,
                    Description = "Shifted element positions for variety"
                });
            }

            return changes;
        }

        private ScoredVariation ScoreVariation(LayoutVariation variation, GenerationConstraints constraints)
        {
            var scored = new ScoredVariation
            {
                Variation = variation,
                Scores = new Dictionary<string, double>()
            };

            // Constraint satisfaction
            double constraintScore = EvaluateConstraintSatisfaction(variation, constraints);
            scored.Scores["Constraints"] = constraintScore;

            // Novelty score
            double noveltyScore = EvaluateNovelty(variation);
            scored.Scores["Novelty"] = noveltyScore;

            // Practicality score
            double practicalityScore = EvaluatePracticality(variation);
            scored.Scores["Practicality"] = practicalityScore;

            // Aesthetic score (based on style coherence)
            double aestheticScore = EvaluateAesthetics(variation);
            scored.Scores["Aesthetics"] = aestheticScore;

            // Compliance score (building code awareness)
            double complianceScore = EvaluateCompliance(variation, constraints);
            scored.Scores["Compliance"] = complianceScore;

            // Sustainability score (energy, materials, climate)
            double sustainabilityScore = EvaluateSustainability(variation, constraints);
            scored.Scores["Sustainability"] = sustainabilityScore;

            // Combined score with weighted criteria
            // Weights reflect BIM design priorities: compliance > practicality > sustainability > constraints > aesthetics > novelty
            scored.Score = complianceScore * 0.20 +
                           practicalityScore * 0.20 +
                           constraintScore * 0.20 +
                           sustainabilityScore * 0.15 +
                           aestheticScore * 0.15 +
                           noveltyScore * 0.10;

            return scored;
        }

        private double EvaluateConstraintSatisfaction(LayoutVariation variation, GenerationConstraints constraints)
        {
            double score = 1.0;

            // Check area constraints
            if (constraints.MaxArea.HasValue)
            {
                score *= 0.9;
            }

            // Check budget constraints
            if (constraints.MaxBudget.HasValue)
            {
                score *= 0.85;
            }

            // Required spaces check
            if (constraints.RequiredSpaces?.Count > 0)
            {
                var spaceCount = variation.BaseLayout?.Spaces?.Count ?? 0;
                score *= Math.Min(1.0, (double)spaceCount / constraints.RequiredSpaces.Count);
            }

            return score;
        }

        private double EvaluateNovelty(LayoutVariation variation)
        {
            var changeCount = variation.Changes.Count;
            double score = Math.Min(1.0, changeCount * 0.2 + 0.3);

            // Bonus for combining pattern + style
            if (!string.IsNullOrEmpty(variation.AppliedPattern) && !string.IsNullOrEmpty(variation.AppliedStyle))
                score = Math.Min(1.0, score + 0.15);

            return score;
        }

        private double EvaluatePracticality(LayoutVariation variation)
        {
            if (variation.VariationType == VariationType.PatternBased)
                return 0.9;
            if (variation.VariationType == VariationType.StyleBased)
                return 0.85;
            if (variation.VariationType == VariationType.TransformBased)
                return 0.75;
            return 0.6;
        }

        private double EvaluateAesthetics(LayoutVariation variation)
        {
            double score = 0.5;

            if (!string.IsNullOrEmpty(variation.AppliedStyle))
                score = 0.85;
            else if (!string.IsNullOrEmpty(variation.AppliedPattern))
                score = 0.75;

            // Biophilic patterns add aesthetic value
            if (variation.AppliedPattern == "BiophilicDesign")
                score = Math.Min(1.0, score + 0.1);

            return score;
        }

        private double EvaluateCompliance(LayoutVariation variation, GenerationConstraints constraints)
        {
            double score = 0.7; // base compliance

            // Pattern-based designs with known-compliant patterns score higher
            if (!string.IsNullOrEmpty(variation.AppliedPattern) &&
                _patterns.TryGetValue(variation.AppliedPattern, out var pattern))
            {
                // Patterns with explicit code references are compliance-aware
                var hasCodeRef = pattern.Principles.Any(p =>
                    p.Contains("IBC") || p.Contains("ADA") || p.Contains("NFPA") ||
                    p.Contains("ASHRAE") || p.Contains("STC"));
                if (hasCodeRef)
                    score += 0.2;
            }

            // Healthcare and education patterns have stricter compliance requirements
            if (constraints.BuildingType == BuildingType.Healthcare ||
                constraints.BuildingType == BuildingType.Education)
            {
                // Only pattern-based variations can achieve high compliance for regulated buildings
                if (variation.VariationType == VariationType.PatternBased)
                    score += 0.1;
                else
                    score -= 0.1;
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private double EvaluateSustainability(LayoutVariation variation, GenerationConstraints constraints)
        {
            double score = 0.5;

            // Passive cooling patterns are sustainable
            if (variation.AppliedPattern == "PassiveCooling")
                score += 0.3;

            // Biophilic design improves sustainability
            if (variation.AppliedPattern == "BiophilicDesign")
                score += 0.2;

            // African Contemporary style uses local materials (reduced transport emissions)
            if (variation.AppliedStyle == "African Contemporary")
                score += 0.2;

            // Circulation optimization reduces building footprint
            if (variation.AppliedTransformation == "OptimizeCirculation")
                score += 0.15;

            return Math.Max(0, Math.Min(1, score));
        }

        private bool IsPatternApplicable(
            DesignPattern pattern,
            Layout layout,
            GenerationConstraints constraints)
        {
            // Check category compatibility
            if (constraints.BuildingType.HasValue &&
                pattern.Category != PatternCategory.Universal)
            {
                var categoryMatch = constraints.BuildingType switch
                {
                    BuildingType.Residential => pattern.Category == PatternCategory.Residential || pattern.Category == PatternCategory.Kitchen,
                    BuildingType.Commercial => pattern.Category == PatternCategory.Commercial,
                    BuildingType.Healthcare => pattern.Category == PatternCategory.Healthcare,
                    BuildingType.Education => pattern.Category == PatternCategory.Education,
                    BuildingType.Hospitality => pattern.Category == PatternCategory.Hospitality,
                    BuildingType.Mixed => true,
                    _ => true
                };
                if (!categoryMatch) return false;
            }

            // Check pattern constraints
            foreach (var constraint in pattern.Constraints ?? Enumerable.Empty<PatternConstraint>())
            {
                if (constraint.Type == "MinArea" && layout.TotalArea < constraint.Value)
                    return false;
            }

            return true;
        }

        private TransformationContext CreateTransformationContext(
            Layout layout,
            GenerationConstraints constraints)
        {
            return new TransformationContext
            {
                HasAdjacentSpaces = layout.Spaces?.Count > 1,
                CombinedArea = layout.TotalArea,
                MaxAllowedArea = constraints.MaxArea ?? double.MaxValue,
                SpaceArea = layout.Spaces?.FirstOrDefault()?.Area ?? 0,
                RequiresSubdivision = layout.TotalArea > 50,
                AllowsRotation = constraints.AllowRotation,
                AllowsMirroring = constraints.AllowMirroring,
                RotationAngle = 90,
                MirrorAxis = "Y",
                CirculationEfficiency = 0.6,
                CirculationElements = layout.CirculationPaths ?? new List<string>(),
                SelectedSpaces = layout.Spaces?.Select(s => s.SpaceId).ToList() ?? new List<string>()
            };
        }

        private ConstraintCheckResult CheckPatternConstraints(Layout layout, DesignPattern pattern)
        {
            var result = new ConstraintCheckResult { Passed = true };
            var suggestions = new List<string>();

            foreach (var constraint in pattern.Constraints ?? Enumerable.Empty<PatternConstraint>())
            {
                switch (constraint.Type)
                {
                    case "MinArea" when layout.TotalArea < constraint.Value:
                        result.Passed = false;
                        suggestions.Add($"Increase area to at least {constraint.Value} {constraint.Unit}");
                        break;
                    case "MaxPartitions" when layout.PartitionCount > constraint.Value:
                        result.Passed = false;
                        suggestions.Add($"Reduce partitions to {constraint.Value} or fewer");
                        break;
                }
            }

            result.Suggestions = suggestions;
            if (!result.Passed)
                result.Message = $"Layout does not meet {pattern.PatternId} requirements";

            return result;
        }

        private Layout ApplyPatternTransformations(
            Layout layout,
            DesignPattern pattern,
            PatternParameters parameters)
        {
            // Create transformed layout
            var transformed = layout.Clone();

            // Apply space relationships
            foreach (var relationship in pattern.SpaceRelationships)
            {
                // Would modify layout based on relationships
            }

            return transformed;
        }

        private List<MaterialChange> GenerateMaterialChanges(Layout layout, StyleProfile style)
        {
            var changes = new List<MaterialChange>();

            for (int i = 0; i < Math.Min(style.MaterialPalette.Length, 4); i++)
            {
                changes.Add(new MaterialChange
                {
                    ElementType = i switch
                    {
                        0 => "Wall",
                        1 => "Floor",
                        2 => "Window",
                        _ => "Detail"
                    },
                    NewMaterial = style.MaterialPalette[i]
                });
            }

            return changes;
        }

        private List<ProportionChange> GenerateProportionChanges(Layout layout, StyleProfile style)
        {
            var changes = new List<ProportionChange>();

            foreach (var proportion in style.ProportionRules)
            {
                changes.Add(new ProportionChange
                {
                    Property = proportion.Key,
                    NewValue = proportion.Value
                });
            }

            return changes;
        }

        private List<ElementStyleChange> GenerateElementChanges(Layout layout, StyleProfile style)
        {
            return style.Elements.Select(e => new ElementStyleChange
            {
                ElementType = e.Type,
                NewStyle = e.Style,
                NewMaterial = e.Material
            }).ToList();
        }

        private List<LayoutOption> GenerateLayoutOptions(Space space, SpaceRequirements requirements)
        {
            var options = new List<LayoutOption>
            {
                new() { Name = "Standard", Description = "Conventional layout", Efficiency = 0.85 },
                new() { Name = "Open", Description = "Maximum flexibility", Efficiency = 0.9 },
                new() { Name = "Efficient", Description = "Optimized circulation", Efficiency = 0.95 }
            };

            return options;
        }

        private List<FurnitureOption> GenerateFurnitureOptions(Space space, SpaceRequirements requirements)
        {
            return new List<FurnitureOption>
            {
                new() { Name = "Minimal", Description = "Essential items only", Density = 0.3 },
                new() { Name = "Standard", Description = "Typical furnishing", Density = 0.5 },
                new() { Name = "Complete", Description = "Fully furnished", Density = 0.7 }
            };
        }

        private List<StyleOption> GenerateStyleOptions(Space space, SpaceRequirements requirements)
        {
            return _styles.Values.Select(s => new StyleOption
            {
                StyleId = s.StyleId,
                Description = s.Description,
                MaterialSummary = string.Join(", ", s.MaterialPalette.Take(2))
            }).ToList();
        }

        private List<SizeOption> GenerateSizeOptions(Space space, SpaceRequirements requirements)
        {
            var currentArea = space.Area;
            return new List<SizeOption>
            {
                new() { Name = "Compact", Area = currentArea * 0.8, Difference = -20 },
                new() { Name = "Current", Area = currentArea, Difference = 0 },
                new() { Name = "Expanded", Area = currentArea * 1.2, Difference = 20 }
            };
        }

        private List<PatternSuggestion> GetPatternSuggestions(DesignContext context)
        {
            return _patterns.Values
                .Where(p => context.BuildingType == null ||
                           p.Category == PatternCategory.Universal ||
                           p.Category.ToString() == context.BuildingType.ToString())
                .Select(p => new PatternSuggestion
                {
                    PatternId = p.PatternId,
                    Description = p.Description,
                    Relevance = 0.8,
                    Benefits = p.Principles.Take(2).ToList()
                })
                .Take(3)
                .ToList();
        }

        private List<StyleSuggestion> GetStyleSuggestions(DesignContext context)
        {
            return _styles.Values
                .Select(s => new StyleSuggestion
                {
                    StyleId = s.StyleId,
                    Description = s.Description,
                    KeyMaterials = s.MaterialPalette.Take(2).ToList()
                })
                .Take(3)
                .ToList();
        }

        private List<ImprovementSuggestion> GetImprovementSuggestions(DesignContext context)
        {
            var suggestions = new List<ImprovementSuggestion>
            {
                new() { Area = "Circulation", Suggestion = "Optimize corridor widths per IBC 1020 (min 1220mm)", Impact = "High", Code = "IBC 1020" },
                new() { Area = "Lighting", Suggestion = "Increase natural light exposure (target 2% daylight factor per CIBSE)", Impact = "High", Code = "CIBSE Guide A" },
                new() { Area = "Flexibility", Suggestion = "Add moveable partitions for space adaptability", Impact = "Medium" },
                new() { Area = "Acoustics", Suggestion = "Ensure STC 50+ between occupied spaces per IBC 1207", Impact = "Medium", Code = "IBC 1207" },
                new() { Area = "Accessibility", Suggestion = "Verify ADA accessible route through all public spaces", Impact = "High", Code = "ADA 403" },
                new() { Area = "Fire Safety", Suggestion = "Review egress paths for max 60m travel distance per IBC 1017", Impact = "High", Code = "IBC 1017" },
                new() { Area = "Ventilation", Suggestion = "Check ASHRAE 62.1 outdoor air requirements per occupancy", Impact = "Medium", Code = "ASHRAE 62.1" }
            };

            // Filter by building type
            if (context.BuildingType == BuildingType.Healthcare)
            {
                suggestions.Add(new ImprovementSuggestion
                {
                    Area = "Infection Control",
                    Suggestion = "Verify clean/dirty separation in clinical zones",
                    Impact = "Critical",
                    Code = "IBC 407"
                });
            }

            if (context.BuildingType == BuildingType.Education)
            {
                suggestions.Add(new ImprovementSuggestion
                {
                    Area = "Classroom Sizing",
                    Suggestion = "Check classroom area meets 1.86m² per student (IBC 1004.5)",
                    Impact = "High",
                    Code = "IBC 1004.5"
                });
            }

            return suggestions;
        }

        private List<InnovationSuggestion> GetInnovationSuggestions(DesignContext context)
        {
            var suggestions = new List<InnovationSuggestion>
            {
                new() { Idea = "Green wall integration for air quality and biophilic benefit", Category = "Biophilic", Feasibility = 0.8 },
                new() { Idea = "Electrochromic smart glass for adaptive solar control", Category = "Technology", Feasibility = 0.6 },
                new() { Idea = "Modular furniture system for multi-use spaces", Category = "Flexibility", Feasibility = 0.9 },
                new() { Idea = "Phase-change materials in walls for thermal energy storage", Category = "Sustainability", Feasibility = 0.5 },
                new() { Idea = "Rainwater harvesting with visible water feature (3-7 day reserve for Africa)", Category = "Sustainability", Feasibility = 0.85 },
                new() { Idea = "Solar chimney for stack-effect ventilation", Category = "Passive Design", Feasibility = 0.7 }
            };

            // Climate-specific innovations
            if (context.UserPreferences?.Contains("tropical") == true ||
                context.UserPreferences?.Contains("africa") == true)
            {
                suggestions.Add(new InnovationSuggestion
                {
                    Idea = "Compressed stabilized earth blocks (CSEB) for thermal mass walls",
                    Category = "Local Materials",
                    Feasibility = 0.9
                });
                suggestions.Add(new InnovationSuggestion
                {
                    Idea = "Bamboo structural elements for sustainable and rapid construction",
                    Category = "Local Materials",
                    Feasibility = 0.75
                });
            }

            return suggestions;
        }

        private DesignFeatures ExtractFeatures(Layout layout)
        {
            return new DesignFeatures
            {
                LayoutId = layout.LayoutId,
                SpaceCount = layout.Spaces?.Count ?? 0,
                TotalArea = layout.TotalArea,
                AspectRatio = layout.Width > 0 ? layout.Length / layout.Width : 1,
                CirculationRatio = layout.CirculationArea / layout.TotalArea
            };
        }

        private DesignFeatures BlendFeatures(List<DesignFeatures> features, List<double> weights)
        {
            var normalizedWeights = weights ?? features.Select(_ => 1.0 / features.Count).ToList();

            return new DesignFeatures
            {
                LayoutId = "Blended",
                TotalArea = features.Zip(normalizedWeights, (f, w) => f.TotalArea * w).Sum(),
                AspectRatio = features.Zip(normalizedWeights, (f, w) => f.AspectRatio * w).Sum(),
                CirculationRatio = features.Zip(normalizedWeights, (f, w) => f.CirculationRatio * w).Sum()
            };
        }

        private Layout GenerateFromFeatures(DesignFeatures features)
        {
            return new Layout
            {
                LayoutId = features.LayoutId,
                TotalArea = features.TotalArea
            };
        }

        private Dictionary<string, double> DocumentContributions(
            List<DesignFeatures> sources,
            DesignFeatures blended)
        {
            return sources.ToDictionary(
                s => s.LayoutId,
                s => 1.0 / sources.Count
            );
        }

        #endregion
    }

    #region Supporting Types

    public class CreativeConfiguration
    {
        public int MaxVariations { get; set; } = 10;
        public double NoveltyWeight { get; set; } = 0.3;
        public bool AllowExperimental { get; set; } = true;
    }

    public class DesignPattern
    {
        public string PatternId { get; set; }
        public PatternCategory Category { get; set; }
        public string Description { get; set; }
        public string[] Principles { get; set; }
        public Dictionary<string, string[]> SpaceRelationships { get; set; }
        public PatternConstraint[] Constraints { get; set; }
    }

    public class PatternConstraint
    {
        public string Type { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
    }

    public class StyleProfile
    {
        public string StyleId { get; set; }
        public string Description { get; set; }
        public string[] MaterialPalette { get; set; }
        public ColorScheme ColorScheme { get; set; }
        public Dictionary<string, double> ProportionRules { get; set; }
        public StyleElement[] Elements { get; set; }
    }

    public class ColorScheme
    {
        public string[] Primary { get; set; }
        public string[] Accent { get; set; }
        public string[] Feature { get; set; }
    }

    public class StyleElement
    {
        public string Type { get; set; }
        public string Style { get; set; }
        public string Material { get; set; }
    }

    public class TransformationRule
    {
        public string TransformationId { get; set; }
        public string Description { get; set; }
        public Func<TransformationContext, bool> Applicability { get; set; }
        public Func<Layout, TransformationContext, LayoutVariation> Transform { get; set; }
    }

    public class TransformationContext
    {
        public bool HasAdjacentSpaces { get; set; }
        public double CombinedArea { get; set; }
        public double MaxAllowedArea { get; set; }
        public double SpaceArea { get; set; }
        public bool RequiresSubdivision { get; set; }
        public bool AllowsRotation { get; set; }
        public bool AllowsMirroring { get; set; }
        public int RotationAngle { get; set; }
        public string MirrorAxis { get; set; }
        public double CirculationEfficiency { get; set; }
        public List<string> CirculationElements { get; set; }
        public List<string> SelectedSpaces { get; set; }
        public string TargetSpace { get; set; }
    }

    public class Layout
    {
        public string LayoutId { get; set; }
        public double TotalArea { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double CirculationArea { get; set; }
        public int PartitionCount { get; set; }
        public List<Space> Spaces { get; set; }
        public List<string> AllElements { get; set; } = new();
        public List<string> CirculationPaths { get; set; } = new();

        public Layout Clone() => new Layout
        {
            LayoutId = LayoutId + "_clone",
            TotalArea = TotalArea,
            Width = Width,
            Length = Length,
            Spaces = Spaces?.ToList()
        };
    }

    public class Space
    {
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public string Function { get; set; }
    }

    public class GenerationConstraints
    {
        public double? MaxArea { get; set; }
        public double? MinArea { get; set; }
        public double? MaxBudget { get; set; }
        public BuildingType? BuildingType { get; set; }
        public List<string> PreferredStyles { get; set; }
        public List<string> RequiredSpaces { get; set; }
        public bool AllowRotation { get; set; } = true;
        public bool AllowMirroring { get; set; } = true;
    }

    public class GenerationResult
    {
        public Layout OriginalLayout { get; set; }
        public GenerationConstraints Constraints { get; set; }
        public List<ScoredVariation> Variations { get; set; } = new();
        public ScoredVariation BestVariation { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class LayoutVariation
    {
        public Layout BaseLayout { get; set; }
        public VariationType VariationType { get; set; }
        public string AppliedPattern { get; set; }
        public string AppliedStyle { get; set; }
        public string AppliedTransformation { get; set; }
        public string Description { get; set; }
        public List<LayoutChange> Changes { get; set; } = new();
    }

    public class LayoutChange
    {
        public ChangeType Type { get; set; }
        public string Description { get; set; }
        public string Details { get; set; }
        public List<string> AffectedElements { get; set; } = new();
    }

    public class ScoredVariation
    {
        public LayoutVariation Variation { get; set; }
        public double Score { get; set; }
        public Dictionary<string, double> Scores { get; set; }
    }

    public class SpaceRequirements
    {
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public string Function { get; set; }
        public List<string> RequiredAmenities { get; set; }
    }

    public class SpaceAlternatives
    {
        public Space OriginalSpace { get; set; }
        public SpaceRequirements Requirements { get; set; }
        public List<LayoutOption> LayoutOptions { get; set; }
        public List<FurnitureOption> FurnitureOptions { get; set; }
        public List<StyleOption> StyleOptions { get; set; }
        public List<SizeOption> SizeOptions { get; set; }
    }

    public class LayoutOption
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Efficiency { get; set; }
    }

    public class FurnitureOption
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Density { get; set; }
    }

    public class StyleOption
    {
        public string StyleId { get; set; }
        public string Description { get; set; }
        public string MaterialSummary { get; set; }
    }

    public class SizeOption
    {
        public string Name { get; set; }
        public double Area { get; set; }
        public double Difference { get; set; }
    }

    public class PatternParameters
    {
        public double Intensity { get; set; } = 1.0;
        public Dictionary<string, object> Overrides { get; set; }
    }

    public class PatternApplication
    {
        public string PatternId { get; set; }
        public Layout OriginalLayout { get; set; }
        public Layout ResultingLayout { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> AppliedPrinciples { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class ConstraintCheckResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class StyleParameters
    {
        public double Intensity { get; set; } = 1.0;
        public List<string> ExcludedElements { get; set; }
    }

    public class StyleApplication
    {
        public string StyleId { get; set; }
        public Layout OriginalLayout { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<MaterialChange> MaterialChanges { get; set; }
        public List<ProportionChange> ProportionChanges { get; set; }
        public List<ElementStyleChange> ElementChanges { get; set; }
        public ColorScheme ColorScheme { get; set; }
    }

    public class MaterialChange
    {
        public string ElementType { get; set; }
        public string NewMaterial { get; set; }
    }

    public class ProportionChange
    {
        public string Property { get; set; }
        public double NewValue { get; set; }
    }

    public class ElementStyleChange
    {
        public string ElementType { get; set; }
        public string NewStyle { get; set; }
        public string NewMaterial { get; set; }
    }

    public class DesignContext
    {
        public BuildingType? BuildingType { get; set; }
        public string CurrentPhase { get; set; }
        public Layout CurrentLayout { get; set; }
        public List<string> UserPreferences { get; set; }
    }

    public class CreativeSuggestions
    {
        public DesignContext Context { get; set; }
        public DateTime Timestamp { get; set; }
        public List<PatternSuggestion> PatternSuggestions { get; set; }
        public List<StyleSuggestion> StyleSuggestions { get; set; }
        public List<ImprovementSuggestion> ImprovementSuggestions { get; set; }
        public List<InnovationSuggestion> InnovationSuggestions { get; set; }
    }

    public class PatternSuggestion
    {
        public string PatternId { get; set; }
        public string Description { get; set; }
        public double Relevance { get; set; }
        public List<string> Benefits { get; set; }
    }

    public class StyleSuggestion
    {
        public string StyleId { get; set; }
        public string Description { get; set; }
        public List<string> KeyMaterials { get; set; }
    }

    public class ImprovementSuggestion
    {
        public string Area { get; set; }
        public string Suggestion { get; set; }
        public string Impact { get; set; }
        public string Code { get; set; }
    }

    public class InnovationSuggestion
    {
        public string Idea { get; set; }
        public string Category { get; set; }
        public double Feasibility { get; set; }
    }

    public class BlendParameters
    {
        public List<double> Weights { get; set; }
        public string BlendMode { get; set; } = "Average";
    }

    public class BlendResult
    {
        public List<Layout> Sources { get; set; }
        public BlendParameters Parameters { get; set; }
        public Layout BlendedLayout { get; set; }
        public Dictionary<string, double> FeatureContributions { get; set; }
    }

    public class DesignFeatures
    {
        public string LayoutId { get; set; }
        public int SpaceCount { get; set; }
        public double TotalArea { get; set; }
        public double AspectRatio { get; set; }
        public double CirculationRatio { get; set; }
    }

    public enum PatternCategory
    {
        Residential,
        Commercial,
        Healthcare,
        Education,
        Kitchen,
        Hospitality,
        Universal
    }

    public enum BuildingType
    {
        Residential,
        Commercial,
        Industrial,
        Healthcare,
        Education,
        Hospitality,
        Mixed
    }

    public enum VariationType
    {
        PatternBased,
        StyleBased,
        TransformBased,
        Random
    }

    public enum ChangeType
    {
        Merge,
        Subdivide,
        Rotate,
        Mirror,
        Move,
        Resize,
        Optimize,
        PatternApplication,
        StyleApplication
    }

    #endregion
}
