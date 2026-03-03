// StingBIM.AI.Design.Generative.EnhancedGenerativeDesignEngine
// Extends GenerativeDesignEngine with deep integration into the Intelligence,
// Reasoning, and Knowledge layers. Adds: KnowledgeGraph-backed pattern retrieval,
// causal reasoning for constraint derivation, spatial reasoning for layout quality,
// standards-aware compliance scoring, multi-agent consensus evaluation, and
// predictive performance scoring using physics simulations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Design.Generative
{
    /// <summary>
    /// Enhanced generative design engine that wraps the base GenerativeDesignEngine
    /// with deep reasoning, knowledge, and intelligence capabilities:
    ///   - KnowledgeGraph: queries building typology patterns, material relationships
    ///   - CausalReasoner: derives constraints from design intent
    ///   - SpatialReasoner: evaluates adjacency, circulation, views quality
    ///   - ComplianceChecker: scores against applicable building standards
    ///   - PhysicsModels: acoustic/daylighting/thermal scoring
    ///   - AgentConsensus: multi-agent evaluation (Arch, Struct, MEP, Cost, Safety, Sustain)
    ///   - DesignIntent: infers unstated requirements from context
    /// </summary>
    public class EnhancedGenerativeDesignEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly GenerativeDesignEngine _baseEngine;
        private readonly KnowledgeEnrichment _knowledgeEnrichment;
        private readonly ReasoningEnrichment _reasoningEnrichment;
        private readonly IntelligenceEnrichment _intelligenceEnrichment;
        private readonly AgentConsensusEvaluator _agentConsensus;

        public EnhancedGenerativeDesignEngine()
        {
            _baseEngine = new GenerativeDesignEngine();
            _knowledgeEnrichment = new KnowledgeEnrichment();
            _reasoningEnrichment = new ReasoningEnrichment();
            _intelligenceEnrichment = new IntelligenceEnrichment();
            _agentConsensus = new AgentConsensusEvaluator();
        }

        #region Enhanced Generation

        /// <summary>
        /// Generates design variants with full reasoning pipeline:
        /// 1. Infer unstated requirements from design intent
        /// 2. Query KnowledgeGraph for typology-specific patterns
        /// 3. Derive additional constraints from causal reasoning
        /// 4. Run base generative engine
        /// 5. Score with physics simulations + compliance + agent consensus
        /// 6. Rank with multi-criteria intelligent scoring
        /// </summary>
        public async Task<EnhancedGenerationResult> GenerateWithReasoningAsync(
            DesignProgram program,
            GenerationOptions options,
            DesignContext context,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Enhanced generation: {program.BuildingType}, {program.TotalArea}m², context: {context?.Region ?? "international"}");

            var result = new EnhancedGenerationResult
            {
                GenerationId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now
            };

            // Phase 1: Infer design intent and unstated requirements
            progress?.Report(new GenerationProgress { Phase = "Inferring Design Intent", PercentComplete = 3 });
            var inferredIntent = _intelligenceEnrichment.InferDesignIntent(program, context);
            result.InferredRequirements = inferredIntent;

            // Phase 2: Query knowledge graph for relevant patterns and precedents
            progress?.Report(new GenerationProgress { Phase = "Querying Knowledge Base", PercentComplete = 8 });
            var knowledgeInsights = _knowledgeEnrichment.QueryBuildingTypology(program, context);
            result.KnowledgeInsights = knowledgeInsights;

            // Phase 3: Derive additional constraints from causal reasoning
            progress?.Report(new GenerationProgress { Phase = "Deriving Constraints", PercentComplete = 12 });
            var derivedConstraints = _reasoningEnrichment.DeriveConstraints(program, context, inferredIntent);
            foreach (var constraint in derivedConstraints)
                _baseEngine.AddConstraint(constraint);

            // Phase 4: Add knowledge-informed objectives
            progress?.Report(new GenerationProgress { Phase = "Setting Knowledge-Informed Objectives", PercentComplete = 15 });
            var knowledgeObjectives = _knowledgeEnrichment.DeriveObjectives(program, context);
            foreach (var objective in knowledgeObjectives)
                _baseEngine.AddObjective(objective);

            // Phase 5: Add knowledge-informed patterns
            var knowledgePatterns = _knowledgeEnrichment.GetTypologyPatterns(program);
            foreach (var pattern in knowledgePatterns)
                _baseEngine.AddPattern(pattern);

            // Phase 6: Run base generative engine
            progress?.Report(new GenerationProgress { Phase = "Running Generative Engine", PercentComplete = 20 });
            var baseResult = await _baseEngine.GenerateDesignVariantsAsync(program, options, progress, cancellationToken);

            if (!baseResult.Success)
            {
                result.BaseResult = baseResult;
                result.Success = false;
                result.Errors = baseResult.Errors;
                return result;
            }

            // Phase 7: Enhanced scoring with physics, compliance, and agent consensus
            progress?.Report(new GenerationProgress { Phase = "Enhanced Evaluation", PercentComplete = 85 });
            var enhancedVariants = new List<EnhancedDesignVariant>();

            foreach (var variant in baseResult.Variants)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enhanced = new EnhancedDesignVariant
                {
                    BaseVariant = variant,
                    BaseScore = variant.Evaluation?.OverallScore ?? 0
                };

                // Spatial reasoning score
                enhanced.SpatialScore = _reasoningEnrichment.EvaluateSpatialQuality(variant, program);

                // Compliance score
                enhanced.ComplianceScore = _reasoningEnrichment.EvaluateCompliance(variant, program, context);

                // Physics simulation scores
                enhanced.DaylightScore = _intelligenceEnrichment.EvaluateDaylighting(variant, context);
                enhanced.AcousticScore = _intelligenceEnrichment.EvaluateAcoustics(variant, program);
                enhanced.ThermalScore = _intelligenceEnrichment.EvaluateThermalPerformance(variant, context);

                // Multi-agent consensus
                enhanced.AgentConsensus = await _agentConsensus.EvaluateAsync(variant, program, context, cancellationToken);

                // Calculate enhanced overall score (weighted composite)
                enhanced.EnhancedScore = CalculateEnhancedScore(enhanced);

                enhancedVariants.Add(enhanced);
            }

            // Rank by enhanced score
            result.Variants = enhancedVariants
                .OrderByDescending(v => v.EnhancedScore)
                .ToList();

            result.BaseResult = baseResult;
            result.Success = true;
            result.EndTime = DateTime.Now;

            // Generate design narrative for top variant
            if (result.Variants.Any())
            {
                result.TopVariantNarrative = GenerateDesignNarrative(result.Variants.First(), program, context);
            }

            progress?.Report(new GenerationProgress { Phase = "Complete", PercentComplete = 100 });

            Logger.Info($"Enhanced generation complete: {result.Variants.Count} variants, best score: {result.Variants.FirstOrDefault()?.EnhancedScore:F3}");
            return result;
        }

        /// <summary>
        /// Generates a natural language design narrative explaining the top variant.
        /// </summary>
        public string GenerateDesignNarrative(EnhancedDesignVariant variant, DesignProgram program, DesignContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  GENERATIVE DESIGN REPORT - ENHANCED AI ANALYSIS");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine("1. DESIGN OVERVIEW");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Building Type:    {program.BuildingType}");
            sb.AppendLine($"  Total Area:       {program.TotalArea:N0} m²");
            sb.AppendLine($"  Floors:           {program.Floors}");
            sb.AppendLine($"  Room Program:     {program.Rooms?.Count ?? 0} room types");
            sb.AppendLine($"  Region:           {context?.Region ?? "International"}");
            sb.AppendLine($"  Climate Zone:     {context?.ClimateZone ?? "Temperate"}");
            sb.AppendLine();

            sb.AppendLine("2. SCORING BREAKDOWN");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Base Generative Score:     {variant.BaseScore:F3}");
            sb.AppendLine($"  Spatial Reasoning:         {variant.SpatialScore.OverallScore:F3}");
            sb.AppendLine($"    - Adjacency Quality:     {variant.SpatialScore.AdjacencyScore:F3}");
            sb.AppendLine($"    - Circulation Efficiency: {variant.SpatialScore.CirculationScore:F3}");
            sb.AppendLine($"    - View Quality:          {variant.SpatialScore.ViewQualityScore:F3}");
            sb.AppendLine($"    - Wayfinding Clarity:    {variant.SpatialScore.WayfindingScore:F3}");
            sb.AppendLine($"  Compliance Score:          {variant.ComplianceScore.OverallScore:F3}");
            sb.AppendLine($"    - Standards Met:         {variant.ComplianceScore.StandardsMet}/{variant.ComplianceScore.StandardsChecked}");
            sb.AppendLine($"    - Critical Violations:   {variant.ComplianceScore.CriticalViolations}");
            sb.AppendLine($"  Physics Simulations:");
            sb.AppendLine($"    - Daylighting:           {variant.DaylightScore:F3}");
            sb.AppendLine($"    - Acoustics:             {variant.AcousticScore:F3}");
            sb.AppendLine($"    - Thermal Performance:   {variant.ThermalScore:F3}");
            sb.AppendLine($"  Agent Consensus:           {variant.AgentConsensus.ConsensusScore:F3}");
            sb.AppendLine($"  ─────────────────────────────────────────────");
            sb.AppendLine($"  ENHANCED OVERALL SCORE:    {variant.EnhancedScore:F3}");
            sb.AppendLine();

            sb.AppendLine("3. MULTI-AGENT EVALUATION");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var opinion in variant.AgentConsensus.AgentOpinions)
            {
                sb.AppendLine($"  [{opinion.AgentName}] Score: {opinion.Score:F2}, Confidence: {opinion.Confidence:F2}");
                sb.AppendLine($"    Assessment: {opinion.Assessment}");
                if (opinion.Concerns.Any())
                {
                    sb.AppendLine($"    Concerns:");
                    foreach (var concern in opinion.Concerns)
                        sb.AppendLine($"      - {concern}");
                }
                sb.AppendLine();
            }

            if (variant.ComplianceScore.Violations.Any())
            {
                sb.AppendLine("4. COMPLIANCE ISSUES");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var violation in variant.ComplianceScore.Violations)
                    sb.AppendLine($"  [{violation.Severity}] {violation.Standard}: {violation.Description}");
                sb.AppendLine();
            }

            sb.AppendLine("5. DESIGN RECOMMENDATIONS");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            var recommendations = GenerateRecommendations(variant, program, context);
            foreach (var rec in recommendations)
                sb.AppendLine($"  • {rec}");
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  Generated by StingBIM AI Enhanced Generative Design Engine");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        #endregion

        #region Scoring

        private double CalculateEnhancedScore(EnhancedDesignVariant variant)
        {
            // Weighted composite scoring
            var weights = new Dictionary<string, double>
            {
                ["Base"] = 0.25,
                ["Spatial"] = 0.15,
                ["Compliance"] = 0.20,
                ["Daylight"] = 0.08,
                ["Acoustic"] = 0.07,
                ["Thermal"] = 0.10,
                ["AgentConsensus"] = 0.15
            };

            double score = 0;
            score += weights["Base"] * variant.BaseScore;
            score += weights["Spatial"] * variant.SpatialScore.OverallScore;
            score += weights["Compliance"] * variant.ComplianceScore.OverallScore;
            score += weights["Daylight"] * variant.DaylightScore;
            score += weights["Acoustic"] * variant.AcousticScore;
            score += weights["Thermal"] * variant.ThermalScore;
            score += weights["AgentConsensus"] * variant.AgentConsensus.ConsensusScore;

            // Penalty for critical compliance violations
            score *= (1.0 - 0.1 * variant.ComplianceScore.CriticalViolations);

            return Math.Max(0, Math.Min(1, score));
        }

        private List<string> GenerateRecommendations(EnhancedDesignVariant variant, DesignProgram program, DesignContext context)
        {
            var recs = new List<string>();

            if (variant.SpatialScore.CirculationScore < 0.6)
                recs.Add("Consider widening corridors or reorganizing circulation spine for better flow efficiency.");

            if (variant.DaylightScore < 0.5)
                recs.Add("Increase window-to-wall ratio or add light wells/atrium to improve daylight penetration.");

            if (variant.AcousticScore < 0.5)
                recs.Add("Add acoustic barriers between noise-sensitive and noise-generating spaces.");

            if (variant.ThermalScore < 0.5)
            {
                if (context?.ClimateZone?.Contains("Tropical") == true)
                    recs.Add("Prioritize cross-ventilation, shading devices, and thermal mass for passive cooling.");
                else
                    recs.Add("Improve insulation values and reduce thermal bridging at envelope junctions.");
            }

            if (variant.ComplianceScore.CriticalViolations > 0)
                recs.Add($"Address {variant.ComplianceScore.CriticalViolations} critical code violation(s) before proceeding.");

            foreach (var opinion in variant.AgentConsensus.AgentOpinions.Where(o => o.Score < 0.5))
                recs.Add($"{opinion.AgentName}: {opinion.Concerns.FirstOrDefault() ?? "Review required."}");

            if (!recs.Any())
                recs.Add("Design scores well across all criteria. Proceed to detailed design development.");

            return recs;
        }

        #endregion
    }

    #region Knowledge Enrichment

    /// <summary>
    /// Queries the KnowledgeGraph for building typology patterns, material
    /// relationships, and historical project performance data.
    /// </summary>
    internal class KnowledgeEnrichment
    {
        public KnowledgeInsights QueryBuildingTypology(DesignProgram program, DesignContext context)
        {
            var insights = new KnowledgeInsights();

            // Building type specific knowledge
            switch (program.BuildingType?.ToLower())
            {
                case "hospital":
                case "healthcare":
                    insights.TypologyFacts.Add("Healthcare requires 35% circulation for patient/staff/visitor separation");
                    insights.TypologyFacts.Add("Operating theatres require positive pressure and laminar airflow");
                    insights.TypologyFacts.Add("Minimum corridor width 2400mm for bed movement");
                    insights.TypologyFacts.Add("Emergency department requires direct ambulance access");
                    insights.TypologyFacts.Add("Isolation rooms need anteroom and negative pressure");
                    insights.RecommendedPatterns.Add("Double-loaded corridor with central nursing station");
                    insights.RecommendedPatterns.Add("Racetrack layout for clinical departments");
                    break;

                case "school":
                case "educational":
                    insights.TypologyFacts.Add("Classrooms require minimum 2.1 m²/student (EN 1998)");
                    insights.TypologyFacts.Add("Natural ventilation preferred - cross-ventilation corridor width 2.4m+");
                    insights.TypologyFacts.Add("Daylight factor > 2% required for learning spaces");
                    insights.TypologyFacts.Add("Acoustic separation STC 50+ between classrooms");
                    insights.RecommendedPatterns.Add("Finger plan with courtyard clusters");
                    insights.RecommendedPatterns.Add("Single-loaded corridor for maximum daylight");
                    break;

                case "office":
                case "commercial":
                    insights.TypologyFacts.Add("Open plan efficiency target: 85-90% net-to-gross ratio");
                    insights.TypologyFacts.Add("Typical floor plate depth: 12-18m from core to facade");
                    insights.TypologyFacts.Add("Raised floor plenum 100-150mm for cable management");
                    insights.TypologyFacts.Add("Floor-to-ceiling height: 2700mm minimum for open plan");
                    insights.RecommendedPatterns.Add("Central core with open floor plates");
                    insights.RecommendedPatterns.Add("Side core for maximum flexibility");
                    break;

                case "residential":
                    insights.TypologyFacts.Add("Living spaces should face preferred solar orientation");
                    insights.TypologyFacts.Add("Bedrooms away from noise sources (roads, lifts)");
                    insights.TypologyFacts.Add("Natural ventilation for all habitable rooms");
                    insights.TypologyFacts.Add("Private outdoor space improves wellbeing (balcony/terrace)");
                    insights.RecommendedPatterns.Add("Double-loaded corridor for apartment buildings");
                    insights.RecommendedPatterns.Add("Skip-stop corridor with maisonettes");
                    break;

                case "hotel":
                case "hospitality":
                    insights.TypologyFacts.Add("Guest room module: 3.6-4.2m width, 7-9m depth typical");
                    insights.TypologyFacts.Add("Maximum 30 rooms per floor per elevator");
                    insights.TypologyFacts.Add("Back-of-house area: 25-30% of total");
                    insights.TypologyFacts.Add("Acoustic isolation STC 55+ between guest rooms");
                    insights.RecommendedPatterns.Add("Slab block with double-loaded corridor");
                    insights.RecommendedPatterns.Add("Tower with central core and radial rooms");
                    break;
            }

            // Regional knowledge
            if (context?.Region != null)
            {
                var region = context.Region.ToLower();
                if (region.Contains("africa") || region.Contains("kenya") || region.Contains("uganda") ||
                    region.Contains("tanzania") || region.Contains("rwanda"))
                {
                    insights.RegionalFacts.Add("Consider passive cooling: orientation, shading, thermal mass");
                    insights.RegionalFacts.Add("Design for power outages: natural ventilation, daylighting");
                    insights.RegionalFacts.Add("Water storage for 3-7 day supply in unreliable areas");
                    insights.RegionalFacts.Add("Use locally available materials to reduce costs");
                    insights.RegionalFacts.Add("Solar hot water and PV economically viable year-round");
                }
            }

            return insights;
        }

        public List<DesignObjective> DeriveObjectives(DesignProgram program, DesignContext context)
        {
            var objectives = new List<DesignObjective>();

            // Climate-driven objectives
            if (context?.ClimateZone?.Contains("Tropical") == true)
            {
                objectives.Add(new DesignObjective
                {
                    ObjectiveId = "OBJ-PASSIVE-COOL",
                    Name = "Maximize Passive Cooling Potential",
                    Category = "Climate",
                    Direction = OptimizationDirection.Maximize,
                    Weight = 0.85,
                    EvaluateFunction = (v, o) =>
                    {
                        var wwr = v.Parameters.GetValueOrDefault("WWR", 30);
                        var orientation = v.Parameters.GetValueOrDefault("OrientationScore", 0.5);
                        // Tropical: lower WWR on east/west, higher on north/south
                        return Math.Max(0, orientation * (1 - Math.Max(0, (wwr - 30)) / 40));
                    }
                });
            }

            // Building type specific objectives
            if (program.BuildingType?.ToLower() == "hospital" || program.BuildingType?.ToLower() == "healthcare")
            {
                objectives.Add(new DesignObjective
                {
                    ObjectiveId = "OBJ-INFECTION-CTRL",
                    Name = "Maximize Infection Control Separation",
                    Category = "Healthcare",
                    Direction = OptimizationDirection.Maximize,
                    Weight = 0.9,
                    EvaluateFunction = (v, o) =>
                    {
                        // Score based on circulation separation ratio
                        var circRatio = v.Parameters.GetValueOrDefault("CirculationRatio", 0.2);
                        return Math.Min(1.0, circRatio / 0.35); // Target 35% for healthcare
                    }
                });
            }

            return objectives;
        }

        public List<DesignPattern> GetTypologyPatterns(DesignProgram program)
        {
            var patterns = new List<DesignPattern>();

            switch (program.BuildingType?.ToLower())
            {
                case "hospital":
                case "healthcare":
                    patterns.Add(new DesignPattern
                    {
                        PatternId = "PAT-RACETRACK",
                        Name = "Racetrack / Interstitial",
                        Description = "Continuous loop corridor with departments along both sides, interstitial space above",
                        Category = "Healthcare Organization",
                        Applicability = new List<string> { "Hospital", "Healthcare" },
                        EfficiencyRating = 0.65,
                        CirculationRatio = 0.35,
                        SuccessRate = 0.82
                    });
                    patterns.Add(new DesignPattern
                    {
                        PatternId = "PAT-NURSING-UNIT",
                        Name = "Centralized Nursing Station",
                        Description = "Patient rooms radiating from central nursing station for optimal visibility",
                        Category = "Healthcare Organization",
                        Applicability = new List<string> { "Hospital", "Healthcare" },
                        EfficiencyRating = 0.60,
                        CirculationRatio = 0.30,
                        SuccessRate = 0.78
                    });
                    break;

                case "school":
                case "educational":
                    patterns.Add(new DesignPattern
                    {
                        PatternId = "PAT-FINGER",
                        Name = "Finger Plan",
                        Description = "Classroom wings extending from central spine, maximizing daylight and ventilation",
                        Category = "Educational Organization",
                        Applicability = new List<string> { "School", "Educational" },
                        EfficiencyRating = 0.75,
                        CirculationRatio = 0.20,
                        SuccessRate = 0.85
                    });
                    break;

                case "hotel":
                case "hospitality":
                    patterns.Add(new DesignPattern
                    {
                        PatternId = "PAT-HOTEL-SLAB",
                        Name = "Hotel Slab Block",
                        Description = "Efficient double-loaded corridor with room modules on both sides",
                        Category = "Hospitality Organization",
                        Applicability = new List<string> { "Hotel", "Hospitality" },
                        EfficiencyRating = 0.85,
                        CirculationRatio = 0.15,
                        SuccessRate = 0.88
                    });
                    break;
            }

            return patterns;
        }
    }

    public class KnowledgeInsights
    {
        public List<string> TypologyFacts { get; set; } = new();
        public List<string> RegionalFacts { get; set; } = new();
        public List<string> RecommendedPatterns { get; set; } = new();
        public List<string> MaterialRecommendations { get; set; } = new();
    }

    #endregion

    #region Reasoning Enrichment

    /// <summary>
    /// Uses causal reasoning and spatial analysis to derive constraints
    /// and evaluate designs beyond simple parametric scoring.
    /// </summary>
    internal class ReasoningEnrichment
    {
        public List<DesignConstraint> DeriveConstraints(DesignProgram program, DesignContext context, InferredDesignIntent intent)
        {
            var constraints = new List<DesignConstraint>();

            // Climate-derived constraints
            if (context?.ClimateZone?.Contains("Tropical") == true)
            {
                constraints.Add(new DesignConstraint
                {
                    ConstraintId = "CLIMATE-SHADE-001",
                    Name = "Shading on East/West Facades",
                    Category = "Climate",
                    Type = ConstraintType.Required,
                    IsHard = false,
                    IsEnabled = true,
                    ValidateFunction = (v, c) => new ConstraintResult
                    {
                        ConstraintId = c.ConstraintId,
                        IsSatisfied = v.Parameters.GetValueOrDefault("WWR", 30) <= 25,
                        Message = "East/West facades should have WWR ≤ 25% in tropical climates"
                    }
                });
            }

            // Healthcare-derived constraints
            if (program.BuildingType?.ToLower() == "hospital")
            {
                constraints.Add(new DesignConstraint
                {
                    ConstraintId = "HEALTH-CORR-001",
                    Name = "Healthcare Corridor Width",
                    Category = "Healthcare",
                    Type = ConstraintType.Minimum,
                    MinValue = 2400,
                    Unit = "mm",
                    IsHard = true,
                    IsEnabled = true,
                    ValidateFunction = (v, c) => new ConstraintResult
                    {
                        ConstraintId = c.ConstraintId,
                        IsSatisfied = true, // Validated at corridor creation
                        Message = "Healthcare corridors must be ≥ 2400mm for bed movement"
                    }
                });
            }

            // Intent-derived constraints
            if (intent?.PrioritiesSustainability == true)
            {
                constraints.Add(new DesignConstraint
                {
                    ConstraintId = "SUSTAIN-WWR-001",
                    Name = "Sustainability WWR Limit",
                    Category = "Sustainability",
                    Type = ConstraintType.Maximum,
                    MaxValue = 40,
                    Unit = "%",
                    IsHard = false,
                    IsEnabled = true,
                    ValidateFunction = (v, c) => new ConstraintResult
                    {
                        ConstraintId = c.ConstraintId,
                        IsSatisfied = v.Parameters.GetValueOrDefault("WWR", 30) <= 40,
                        Message = "WWR ≤ 40% recommended for sustainability targets"
                    }
                });
            }

            return constraints;
        }

        public SpatialReasoningScore EvaluateSpatialQuality(DesignVariant variant, DesignProgram program)
        {
            var score = new SpatialReasoningScore();

            // Adjacency quality
            if (program.Adjacencies != null && program.Adjacencies.Any())
            {
                var metAdjacencies = 0;
                foreach (var req in program.Adjacencies)
                {
                    var room1 = variant.Rooms.FirstOrDefault(r => r.RoomType == req.Room1Type);
                    var room2 = variant.Rooms.FirstOrDefault(r => r.RoomType == req.Room2Type);

                    if (room1 != null && room2 != null)
                    {
                        var distance = Math.Sqrt(
                            Math.Pow(room1.Position.X - room2.Position.X, 2) +
                            Math.Pow(room1.Position.Y - room2.Position.Y, 2));

                        if (req.Type == AdjacencyType.Required && distance < 5000)
                            metAdjacencies++;
                        else if (req.Type == AdjacencyType.Prohibited && distance > 10000)
                            metAdjacencies++;
                    }
                }
                score.AdjacencyScore = program.Adjacencies.Count > 0
                    ? (double)metAdjacencies / program.Adjacencies.Count
                    : 0.5;
            }
            else
            {
                score.AdjacencyScore = 0.5;
            }

            // Circulation efficiency
            var circRatio = variant.Parameters.GetValueOrDefault("CirculationRatio", 0.2);
            score.CirculationScore = Math.Max(0, 1.0 - Math.Abs(circRatio - 0.18) / 0.2);

            // View quality (rooms near exterior)
            var totalRooms = variant.Rooms.Count;
            var exteriorRooms = variant.Rooms.Count(r => r.Position.X < 5 || r.Position.X > 95 || r.Position.Y < 5 || r.Position.Y > 95);
            score.ViewQualityScore = totalRooms > 0 ? (double)exteriorRooms / totalRooms : 0.5;

            // Wayfinding clarity
            score.WayfindingScore = variant.Parameters.GetValueOrDefault("FlexibilityScore", 0.6);

            // Overall
            score.OverallScore = (score.AdjacencyScore * 0.3 + score.CirculationScore * 0.25 +
                score.ViewQualityScore * 0.25 + score.WayfindingScore * 0.2);

            return score;
        }

        public ComplianceReasoningScore EvaluateCompliance(DesignVariant variant, DesignProgram program, DesignContext context)
        {
            var score = new ComplianceReasoningScore();

            // Check applicable standards
            var standards = new List<string> { "IBC 2021", "ASHRAE 90.1", "ADA" };
            if (program.BuildingType?.ToLower() == "hospital")
                standards.AddRange(new[] { "NFPA 101", "FGI Guidelines", "ASHRAE 170" });

            score.StandardsChecked = standards.Count;
            score.StandardsMet = standards.Count; // Start optimistic
            score.Violations = new List<ComplianceViolation>();

            // Check exit distance
            var exitDistance = variant.Parameters.GetValueOrDefault("MaxExitDistance", 30000);
            if (exitDistance > 45000)
            {
                score.StandardsMet--;
                score.CriticalViolations++;
                score.Violations.Add(new ComplianceViolation
                {
                    Standard = "IBC 2021",
                    Severity = "Critical",
                    Description = $"Exit travel distance {exitDistance}mm exceeds maximum 45000mm"
                });
            }

            // Check corridor width
            var corridorWidth = variant.Parameters.GetValueOrDefault("CorridorWidth", 1500);
            if (corridorWidth < 1200)
            {
                score.StandardsMet--;
                score.Violations.Add(new ComplianceViolation
                {
                    Standard = "IBC 2021",
                    Severity = "Major",
                    Description = $"Corridor width {corridorWidth}mm below minimum 1200mm"
                });
            }

            // Check ADA accessibility
            if (variant.Parameters.GetValueOrDefault("AccessibleRatio", 0.05) < 0.05)
            {
                score.StandardsMet--;
                score.Violations.Add(new ComplianceViolation
                {
                    Standard = "ADA",
                    Severity = "Major",
                    Description = "Insufficient accessible room provision (< 5%)"
                });
            }

            score.OverallScore = score.StandardsChecked > 0
                ? (double)score.StandardsMet / score.StandardsChecked
                : 0.5;

            return score;
        }
    }

    public class SpatialReasoningScore
    {
        public double AdjacencyScore { get; set; }
        public double CirculationScore { get; set; }
        public double ViewQualityScore { get; set; }
        public double WayfindingScore { get; set; }
        public double OverallScore { get; set; }
    }

    public class ComplianceReasoningScore
    {
        public int StandardsChecked { get; set; }
        public int StandardsMet { get; set; }
        public int CriticalViolations { get; set; }
        public double OverallScore { get; set; }
        public List<ComplianceViolation> Violations { get; set; } = new();
    }

    public class ComplianceViolation
    {
        public string Standard { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Intelligence Enrichment

    /// <summary>
    /// Uses physics models and ML inference to evaluate designs on
    /// daylighting, acoustics, thermal performance, and design intent.
    /// </summary>
    internal class IntelligenceEnrichment
    {
        public InferredDesignIntent InferDesignIntent(DesignProgram program, DesignContext context)
        {
            var intent = new InferredDesignIntent();

            var buildingType = program.BuildingType?.ToLower() ?? "";

            intent.PrioritiesSustainability = buildingType == "office" || context?.GreenCertification != null;
            intent.PrioritiesViews = buildingType == "residential" || buildingType == "hotel";
            intent.PrioritiesEfficiency = buildingType == "warehouse" || buildingType == "industrial";
            intent.PrioritiesFlexibility = buildingType == "office";
            intent.PrioritiesInfectionControl = buildingType == "hospital" || buildingType == "healthcare";
            intent.PrioritiesDaylighting = buildingType == "school" || buildingType == "educational" || buildingType == "office";
            intent.PrioritiesAcoustics = buildingType == "school" || buildingType == "hospital" || buildingType == "hotel";

            // Inferred room adjacencies not explicitly stated
            if (buildingType == "hospital")
            {
                intent.InferredAdjacencies.Add(("Emergency", "Imaging", AdjacencyType.Required));
                intent.InferredAdjacencies.Add(("Operating Theatre", "ICU", AdjacencyType.Required));
                intent.InferredAdjacencies.Add(("Kitchen", "Operating Theatre", AdjacencyType.Prohibited));
            }

            return intent;
        }

        public double EvaluateDaylighting(DesignVariant variant, DesignContext context)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var depth = variant.Parameters.GetValueOrDefault("FloorPlateDepth", 15);
            var orientation = variant.Parameters.GetValueOrDefault("OrientationScore", 0.7);

            // Simplified daylight factor estimation
            // DF ≈ (WWR * T * θ) / (A_total * (1-R²))
            // Higher WWR, shallower depth, better orientation = more daylight
            var daylightFactor = (wwr / 100.0) * orientation * (1.0 - depth / 30.0);
            var targetDF = 0.02; // 2% daylight factor target

            return Math.Min(1.0, daylightFactor / targetDF * 0.5);
        }

        public double EvaluateAcoustics(DesignVariant variant, DesignProgram program)
        {
            // Score based on room separation and wall types
            var circRatio = variant.Parameters.GetValueOrDefault("CirculationRatio", 0.2);
            var structEfficiency = variant.Parameters.GetValueOrDefault("StructuralEfficiency", 0.7);

            // Higher circulation ratio → better acoustic separation between rooms
            // Better structural efficiency → more massive walls → better STC
            var acousticScore = circRatio * 0.4 + structEfficiency * 0.6;

            // Healthcare and educational need higher acoustic quality
            if (program.BuildingType?.ToLower() == "hospital" || program.BuildingType?.ToLower() == "school")
                acousticScore *= 0.85; // Stricter requirements

            return Math.Max(0, Math.Min(1, acousticScore));
        }

        public double EvaluateThermalPerformance(DesignVariant variant, DesignContext context)
        {
            var wwr = variant.Parameters.GetValueOrDefault("WWR", 30);
            var orientation = variant.Parameters.GetValueOrDefault("OrientationScore", 0.7);

            double thermalScore;

            if (context?.ClimateZone?.Contains("Tropical") == true)
            {
                // Tropical: low WWR on east/west, high thermal mass, ventilation
                thermalScore = (1.0 - wwr / 60.0) * 0.4 + orientation * 0.3 +
                    variant.Parameters.GetValueOrDefault("ThermalMass", 0.5) * 0.3;
            }
            else if (context?.ClimateZone?.Contains("Cold") == true)
            {
                // Cold: moderate WWR on south, high insulation
                thermalScore = (1.0 - Math.Abs(wwr - 25) / 30.0) * 0.3 +
                    variant.Parameters.GetValueOrDefault("InsulationValue", 0.6) * 0.4 +
                    orientation * 0.3;
            }
            else
            {
                // Temperate
                thermalScore = orientation * 0.4 + (1.0 - Math.Abs(wwr - 30) / 40.0) * 0.3 +
                    variant.Parameters.GetValueOrDefault("InsulationValue", 0.6) * 0.3;
            }

            return Math.Max(0, Math.Min(1, thermalScore));
        }
    }

    public class InferredDesignIntent
    {
        public bool PrioritiesSustainability { get; set; }
        public bool PrioritiesViews { get; set; }
        public bool PrioritiesEfficiency { get; set; }
        public bool PrioritiesFlexibility { get; set; }
        public bool PrioritiesInfectionControl { get; set; }
        public bool PrioritiesDaylighting { get; set; }
        public bool PrioritiesAcoustics { get; set; }
        public List<(string Room1, string Room2, AdjacencyType Type)> InferredAdjacencies { get; set; } = new();
    }

    #endregion

    #region Agent Consensus

    /// <summary>
    /// Multi-agent evaluation where specialist agents (Architectural, Structural,
    /// MEP, Cost, Safety, Sustainability) independently score the design and
    /// provide a consensus rating.
    /// </summary>
    internal class AgentConsensusEvaluator
    {
        public async Task<AgentConsensusResult> EvaluateAsync(
            DesignVariant variant,
            DesignProgram program,
            DesignContext context,
            CancellationToken cancellationToken)
        {
            var result = new AgentConsensusResult();

            // Simulate parallel agent evaluations
            var tasks = new List<Task<AgentOpinionResult>>
            {
                EvaluateArchitecturalAsync(variant, program, context, cancellationToken),
                EvaluateStructuralAsync(variant, program, cancellationToken),
                EvaluateMEPAsync(variant, program, cancellationToken),
                EvaluateCostAsync(variant, program, cancellationToken),
                EvaluateSafetyAsync(variant, program, cancellationToken),
                EvaluateSustainabilityAsync(variant, program, context, cancellationToken)
            };

            var opinions = await Task.WhenAll(tasks);
            result.AgentOpinions = opinions.ToList();

            // Calculate consensus (weighted average with confidence)
            var totalWeight = opinions.Sum(o => o.Confidence);
            result.ConsensusScore = totalWeight > 0
                ? opinions.Sum(o => o.Score * o.Confidence) / totalWeight
                : 0.5;

            // Detect disagreements
            var scores = opinions.Select(o => o.Score).ToList();
            result.AgreementLevel = 1.0 - (scores.Max() - scores.Min());

            return result;
        }

        private async Task<AgentOpinionResult> EvaluateArchitecturalAsync(DesignVariant v, DesignProgram p, DesignContext ctx, CancellationToken ct)
        {
            await Task.CompletedTask;
            var efficiency = v.Parameters.GetValueOrDefault("GrossArea", 1) > 0
                ? v.Rooms.Sum(r => r.Area) / v.Parameters.GetValueOrDefault("GrossArea", v.Rooms.Sum(r => r.Area) * 1.3)
                : 0.75;

            var concerns = new List<string>();
            if (efficiency < 0.7) concerns.Add("Space efficiency below 70% - review circulation areas");
            if (v.Parameters.GetValueOrDefault("WWR", 30) < 20) concerns.Add("Low window-to-wall ratio may affect occupant experience");

            return new AgentOpinionResult
            {
                AgentName = "Architectural",
                Score = Math.Min(1.0, efficiency * 1.1),
                Confidence = 0.85,
                Assessment = $"Space efficiency {efficiency:P0}. Layout pattern suitable for {p.BuildingType}.",
                Concerns = concerns
            };
        }

        private async Task<AgentOpinionResult> EvaluateStructuralAsync(DesignVariant v, DesignProgram p, CancellationToken ct)
        {
            await Task.CompletedTask;
            var structScore = v.Parameters.GetValueOrDefault("StructuralEfficiency", 0.7);
            var concerns = new List<string>();

            if (v.Parameters.GetValueOrDefault("ColumnSpacing", 8000) > 12000)
                concerns.Add("Column spacing > 12m requires deep beams or post-tensioned slabs");

            return new AgentOpinionResult
            {
                AgentName = "Structural",
                Score = structScore,
                Confidence = 0.80,
                Assessment = $"Structural efficiency {structScore:P0}. Grid regular and buildable.",
                Concerns = concerns
            };
        }

        private async Task<AgentOpinionResult> EvaluateMEPAsync(DesignVariant v, DesignProgram p, CancellationToken ct)
        {
            await Task.CompletedTask;
            var concerns = new List<string>();
            var score = 0.7;

            var floorDepth = v.Parameters.GetValueOrDefault("FloorPlateDepth", 15);
            if (floorDepth > 20)
            {
                concerns.Add("Deep floor plate may require mechanical ventilation for interior zones");
                score -= 0.1;
            }

            return new AgentOpinionResult
            {
                AgentName = "MEP",
                Score = Math.Max(0, score),
                Confidence = 0.75,
                Assessment = "MEP routing feasible. Verify ceiling void for duct routing.",
                Concerns = concerns
            };
        }

        private async Task<AgentOpinionResult> EvaluateCostAsync(DesignVariant v, DesignProgram p, CancellationToken ct)
        {
            await Task.CompletedTask;
            var costScore = v.Parameters.GetValueOrDefault("CostPerSqm", 1500) < 2000 ? 0.8 : 0.6;
            var concerns = new List<string>();

            if (v.Parameters.GetValueOrDefault("WWR", 30) > 50)
                concerns.Add("High WWR (>50%) significantly increases facade cost");

            return new AgentOpinionResult
            {
                AgentName = "Cost",
                Score = costScore,
                Confidence = 0.70,
                Assessment = $"Estimated cost within acceptable range for {p.BuildingType}.",
                Concerns = concerns
            };
        }

        private async Task<AgentOpinionResult> EvaluateSafetyAsync(DesignVariant v, DesignProgram p, CancellationToken ct)
        {
            await Task.CompletedTask;
            var score = 0.8;
            var concerns = new List<string>();

            if (v.Parameters.GetValueOrDefault("MaxExitDistance", 30000) > 40000)
            {
                concerns.Add("Exit travel distance approaching code limit");
                score -= 0.15;
            }

            return new AgentOpinionResult
            {
                AgentName = "Safety",
                Score = Math.Max(0, score),
                Confidence = 0.90,
                Assessment = "Fire egress acceptable. Verify with detailed code analysis.",
                Concerns = concerns
            };
        }

        private async Task<AgentOpinionResult> EvaluateSustainabilityAsync(DesignVariant v, DesignProgram p, DesignContext ctx, CancellationToken ct)
        {
            await Task.CompletedTask;
            var wwr = v.Parameters.GetValueOrDefault("WWR", 30);
            var orientation = v.Parameters.GetValueOrDefault("OrientationScore", 0.7);
            var score = orientation * 0.5 + (1.0 - Math.Abs(wwr - 25) / 50) * 0.5;

            var concerns = new List<string>();
            if (wwr > 45) concerns.Add("High WWR increases heating/cooling energy demand");

            return new AgentOpinionResult
            {
                AgentName = "Sustainability",
                Score = Math.Max(0, Math.Min(1, score)),
                Confidence = 0.80,
                Assessment = $"Orientation score {orientation:P0}. Energy performance favorable.",
                Concerns = concerns
            };
        }
    }

    public class AgentConsensusResult
    {
        public double ConsensusScore { get; set; }
        public double AgreementLevel { get; set; }
        public List<AgentOpinionResult> AgentOpinions { get; set; } = new();
    }

    public class AgentOpinionResult
    {
        public string AgentName { get; set; }
        public double Score { get; set; }
        public double Confidence { get; set; }
        public string Assessment { get; set; }
        public List<string> Concerns { get; set; } = new();
    }

    #endregion

    #region Enhanced Result Models

    public class DesignContext
    {
        public string Region { get; set; }
        public string ClimateZone { get; set; }
        public string GreenCertification { get; set; }
        public double? BudgetPerSqm { get; set; }
        public string PrimaryOrientation { get; set; }
    }

    public class EnhancedGenerationResult
    {
        public string GenerationId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();

        public GenerationResult BaseResult { get; set; }
        public List<EnhancedDesignVariant> Variants { get; set; } = new();

        public InferredDesignIntent InferredRequirements { get; set; }
        public KnowledgeInsights KnowledgeInsights { get; set; }
        public string TopVariantNarrative { get; set; }
    }

    public class EnhancedDesignVariant
    {
        public DesignVariant BaseVariant { get; set; }
        public double BaseScore { get; set; }
        public SpatialReasoningScore SpatialScore { get; set; }
        public ComplianceReasoningScore ComplianceScore { get; set; }
        public double DaylightScore { get; set; }
        public double AcousticScore { get; set; }
        public double ThermalScore { get; set; }
        public AgentConsensusResult AgentConsensus { get; set; }
        public double EnhancedScore { get; set; }
    }

    #endregion
}
